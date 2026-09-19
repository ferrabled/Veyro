using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MotionRunner.Social;
using UnityEngine;
using UnityEngine.Networking;

namespace MotionRunner.Social.Supabase
{
    /// IProfileService over the Supabase REST surface — the only class in the game that speaks
    /// HTTP. Plain UnityWebRequest + JsonUtility on purpose (no SDK package; CLAUDE.md gotcha
    /// #10). Server contract: supabase/ (migrations + functions), spec in
    /// docs/PROFILE_LEADERBOARD_PLAN.md. The decision rules live engine-free and unit-tested
    /// in RecoveryGate / RetrySchedule (18 Sep review); this class only executes them.
    ///
    /// Failure discipline:
    ///  * Transient (no answer, 429, 5xx) is NEVER terminal: sessions are kept, submissions
    ///    stay queued with their own scheduled drain, Retry-After is honoured (up to an hour),
    ///    and everything else backs off exponentially with jitter.
    ///  * The refresh token is dropped ONLY on a definitive auth rejection (400/401/403).
    ///  * An expired session that cannot be refreshed right now counts as "no session".
    ///  * A profile that failed to load is retried on later calls even while the access token
    ///    stays valid (review R5) — token validity and profile initialization are separate.
    ///
    /// Recovery (review R1 — the invariant that matters most): a stored key belonging to
    /// another user is the device's ONLY road back to the old profile. Until its claim reaches
    /// a definitive outcome, the key is never overwritten and NO RUN IS SUBMITTED — a
    /// submission would make this fresh user a non-empty destination and permanently block the
    /// claim. The pending state survives restarts because it is derived from the key file
    /// itself, not from memory.
    ///
    /// The key file is plaintext BY DESIGN: it must survive reinstall via device backup, and
    /// Android Keystore-backed encryption keys do NOT survive reinstall. A local attacker who
    /// can read it can also read the PlayerPrefs session — same privilege level. iOS moves the
    /// key into the Keychain with T-032.
    public sealed class SupabaseProfileService : MonoBehaviour, IProfileService
    {
        const string RefreshTokenKey = "veyro.sb.refresh";
        const string PendingKey = "veyro.runs.pending";
        const string RecoveryFileName = "veyro-recovery.txt";
        const string SupportRecoveryFileName = "veyro-recovery-support.txt";

        /// Refresh the access token this many seconds before it actually expires.
        const float ExpirySlackSeconds = 60f;

        string _url;
        string _anonKey;

        string _accessToken;
        string _userId;
        float _accessExpiresAt; // Time.realtimeSinceStartup clock

        bool _sessionWork;        // serializes session/profile/recovery work
        float _retryNotBefore;    // backoff gate for that work
        float _backoff = RetrySchedule.FirstDelaySeconds;

        float _submitNotBefore;   // separate gate: a submit 429 throttles submits, not reads
        float _submitBackoff = RetrySchedule.FirstDelaySeconds;
        bool _draining;
        bool _drainScheduled;

        /// A foreign recovery key's claim is unresolved (see class comment). Derived from the
        /// key file at session time; cleared only by a definitive server outcome.
        bool _recoveryPending;

        /// Set after a successful DeleteAccount: the service goes dormant so nothing —
        /// especially not a board refresh — silently creates a replacement account inside the
        /// deletion flow (review R2). The next app launch starts fresh.
        bool _dormantAfterDeletion;

        public bool IsReady { get; private set; }

        public Profile Current { get; private set; }

        public event Action ProfileChanged;

        public static SupabaseProfileService Create(string url, string anonKey)
        {
            var go = new GameObject("SupabaseProfile");
            DontDestroyOnLoad(go);
            var service = go.AddComponent<SupabaseProfileService>();
            service._url = url.TrimEnd('/');
            service._anonKey = anonKey;
            service.StartCoroutine(service.EnsureSession());
            return service;
        }

        // ---------------- session / profile / recovery ----------------

        bool HasLiveToken => _accessToken != null &&
                             Time.realtimeSinceStartup < _accessExpiresAt;

        /// Everything is settled: nothing for EnsureSession to do.
        bool SessionSettled => HasLiveToken && IsReady && !_recoveryPending &&
            !string.IsNullOrEmpty(RecoveryCode);

        /// The one entry point for session work. Serialized, backoff-gated, and it never
        /// invents a new anonymous identity while a transient failure hides the old one.
        IEnumerator EnsureSession()
        {
            while (_sessionWork) yield return null;
            if (_dormantAfterDeletion) yield break;
            if (SessionSettled) yield break;
            if (Time.realtimeSinceStartup < _retryNotBefore) yield break;

            _sessionWork = true;
            try
            {
                // 1. A live token may still have unfinished business: an unloaded profile
                //    (review R5) or an unresolved recovery. Retry those without touching auth.
                if (HasLiveToken)
                {
                    yield return SettleRecoveryAndProfile();
                    yield break;
                }

                // 2. Refresh whatever token we hold.
                string refresh = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
                if (!string.IsNullOrEmpty(refresh))
                {
                    var outcome = new RequestOutcome();
                    yield return RefreshSession(refresh, outcome);
                    if (HasLiveToken)
                    {
                        ClearBackoff();
                        yield return SettleRecoveryAndProfile();
                        yield break;
                    }
                    if (RetrySchedule.IsTransient(outcome.Status))
                    {
                        // The token may still be valid — back off; never mint a new
                        // anonymous user over a live identity.
                        ApplyBackoff(outcome.RetryAfterSeconds);
                        yield break;
                    }
                    // Definitive rejection: the token is dead. Fall through.
                }

                // 3. Fresh install / dead token: new anonymous user, then recovery.
                var signup = new RequestOutcome();
                yield return SignInAnonymously(signup);
                if (!HasLiveToken)
                {
                    ApplyBackoff(signup.RetryAfterSeconds);
                    yield break;
                }
                ClearBackoff();
                yield return SettleRecoveryAndProfile();
            }
            finally
            {
                _sessionWork = false;
            }
        }

        /// With a live token: resolve any pending recovery first (its outcome changes which
        /// profile this user owns), then make sure the profile is loaded, then issue a
        /// recovery key if rotation is allowed, then drain the queue.
        IEnumerator SettleRecoveryAndProfile()
        {
            var stored = ReadRecoveryKey();
            _recoveryPending = RecoveryGate.IsPending(stored.UserId, stored.Key, _userId);

            if (_recoveryPending)
            {
                long status = 0;
                RecoverResponse response = null;
                yield return CallFunction("recover-session",
                    "{\"recovery_key\":\"" + stored.Key + "\"}",
                    (s, body, _) =>
                    {
                        status = s;
                        if (body != null)
                        {
                            try { response = JsonUtility.FromJson<RecoverResponse>(body); }
                            catch (Exception) { response = null; }
                        }
                    });

                switch (RecoveryGate.Resolve(status, response?.recovered ?? false,
                            response?.reason ?? string.Empty))
                {
                    case RecoveryGate.Resolution.Claimed:
                        // The old profile is ours now. Store the rotated key under the
                        // current user; an empty rotated key self-heals via MayRotate below.
                        if (!string.IsNullOrEmpty(response.recovery_key))
                            WriteRecoveryKey(_userId, response.recovery_key);
                        else
                            DeleteRecoveryKeyFile();
                        _recoveryPending = false;
                        break;
                    case RecoveryGate.Resolution.KeyInvalid:
                        // The key recovers nothing anywhere - only now is discarding it safe.
                        DeleteRecoveryKeyFile();
                        _recoveryPending = false;
                        break;
                    case RecoveryGate.Resolution.Blocked:
                        // This install already played; the claim will never succeed from
                        // here. Preserve the foreign key separately before making room for
                        // this profile's own code. A disk failure must not lose either key.
                        if (!ArchiveRecoveryKey())
                        {
                            ApplyBackoff(0f);
                            yield break;
                        }
                        _recoveryPending = false;
                        break;
                    case RecoveryGate.Resolution.StillPending:
                        // Transient: keep the key EXACTLY as it is, keep blocking submits,
                        // retry after backoff (also across restarts - the file IS the state).
                        ApplyBackoff(0f);
                        yield break;
                }
            }

            if (!IsReady) yield return LoadProfile();
            if (!IsReady)
            {
                ApplyBackoff(0f); // profile GET failed - retried on a later call (R5)
                yield break;
            }

            // Retry-safe key issuance - but ONLY when it cannot destroy a pending road back
            // (RecoveryGate.MayRotate). A key tagged with the current user needs nothing.
            yield return EnsureRecoveryKey();
            ScheduleDrain(0f);
        }

        IEnumerator EnsureRecoveryKey()
        {
            string previousCode = RecoveryCode;
            var afterLoad = ReadRecoveryKey();
            if (RecoveryGate.MayRotate(afterLoad.UserId, afterLoad.Key, _userId))
            {
                yield return CallRpc("rotate_recovery_key", "{}", (status, body, _) =>
                {
                    if (status != 200) return;
                    var response = JsonUtility.FromJson<RotateKeyResponse>(body);
                    if (response != null && !string.IsNullOrEmpty(response.recovery_key))
                        WriteRecoveryKey(_userId, response.recovery_key);
                });
            }
            else if (!string.IsNullOrEmpty(afterLoad.Key) && afterLoad.UserId != _userId)
            {
                // A claim just succeeded but its rotated key was lost with the response:
                // the file still carries the OLD user tag. Re-tagging requires a fresh key.
                yield return CallRpc("rotate_recovery_key", "{}", (status, body, _) =>
                {
                    if (status != 200) return;
                    var response = JsonUtility.FromJson<RotateKeyResponse>(body);
                    if (response != null && !string.IsNullOrEmpty(response.recovery_key))
                        WriteRecoveryKey(_userId, response.recovery_key);
                });
            }
            if (string.IsNullOrEmpty(RecoveryCode))
            {
                ApplyBackoff(0f);
                if (!_keyRetryScheduled)
                {
                    _keyRetryScheduled = true;
                    StartCoroutine(RetryRecoveryKey());
                }
            }
            else if (IsReady && RecoveryCode != previousCode)
            {
                // A background retry can finish while the profile tab remains open.
                ProfileChanged?.Invoke();
            }
        }

        bool _keyRetryScheduled;

        IEnumerator RetryRecoveryKey()
        {
            yield return new WaitForSecondsRealtime(SessionRetryDelay);
            _keyRetryScheduled = false;
            yield return EnsureSession();
        }

        /// POST /auth/v1/signup with an empty body is GoTrue's anonymous sign-in (enabled in
        /// supabase/config.toml). The response carries the session and the new user id.
        IEnumerator SignInAnonymously(RequestOutcome outcome)
        {
            yield return SendJson(UnityWebRequest.kHttpVerbPOST,
                _url + "/auth/v1/signup", "{}", false,
                (status, body, retryAfter) =>
                {
                    outcome.Fill(status, retryAfter);
                    if (status == 200) AdoptSession(body);
                });
        }

        IEnumerator RefreshSession(string refreshToken, RequestOutcome outcome)
        {
            yield return SendJson(UnityWebRequest.kHttpVerbPOST,
                _url + "/auth/v1/token?grant_type=refresh_token",
                "{\"refresh_token\":\"" + refreshToken + "\"}", false,
                (status, body, retryAfter) =>
                {
                    outcome.Fill(status, retryAfter);
                    if (status == 200)
                    {
                        AdoptSession(body);
                        return;
                    }
                    // ONLY a definitive auth rejection kills the stored token.
                    if (status == 400 || status == 401 || status == 403)
                        PlayerPrefs.DeleteKey(RefreshTokenKey);
                });
        }

        void AdoptSession(string body)
        {
            var session = JsonUtility.FromJson<AuthResponse>(body);
            if (session == null || string.IsNullOrEmpty(session.access_token)) return;

            string previousUser = _userId;
            _accessToken = session.access_token;
            _userId = session.user != null ? session.user.id : _userId;
            _accessExpiresAt = Time.realtimeSinceStartup +
                               Mathf.Max(session.expires_in, 120) - ExpirySlackSeconds;

            if (previousUser != null && previousUser != _userId)
            {
                // Identity changed under us (should not happen outside signup) - the loaded
                // profile no longer describes this user.
                IsReady = false;
                Current = null;
            }

            if (!string.IsNullOrEmpty(session.refresh_token))
            {
                PlayerPrefs.SetString(RefreshTokenKey, session.refresh_token);
                PlayerPrefs.Save();
            }
        }

        IEnumerator LoadProfile()
        {
            if (!HasLiveToken) yield break;

            // The object accept header makes PostgREST return ONE json object, not a one-
            // element array — JsonUtility cannot parse a top-level array. Columns are explicit
            // and stay within the column-level SELECT grant (0004): recovery_key_hash is
            // deliberately not readable by clients.
            yield return SendJson(UnityWebRequest.kHttpVerbGET,
                _url + "/rest/v1/profiles?select=handle,xp", null, true,
                (status, body, _) =>
                {
                    if (status != 200) return;
                    var row = JsonUtility.FromJson<ProfileRow>(body);
                    if (row == null || string.IsNullOrEmpty(row.handle)) return;

                    Current = new Profile(_userId, row.handle, row.xp);
                    IsReady = true;
                    ProfileChanged?.Invoke();
                },
                accept: "application/vnd.pgrst.object+json");
        }

        void ApplyBackoff(float retryAfterSeconds)
        {
            float delay = RetrySchedule.Delay(_backoff, retryAfterSeconds,
                UnityEngine.Random.Range(0.8f, 1.3f));
            _retryNotBefore = Time.realtimeSinceStartup + delay;
            _backoff = RetrySchedule.NextBackoff(_backoff);
        }

        void ClearBackoff()
        {
            _backoff = RetrySchedule.FirstDelaySeconds;
            _retryNotBefore = 0f;
        }

        // ---------------- IProfileService ----------------

        public void SubmitRun(RunSubmission run, Action<SubmitOutcome> done)
        {
            if (run == null)
            {
                done?.Invoke(new SubmitOutcome(SubmitStatus.Rejected,
                    error: new SocialError("bad_run", "null submission")));
                return;
            }
            StartCoroutine(SubmitRoutine(run, done, queueOnFailure: true));
        }

        IEnumerator SubmitRoutine(RunSubmission run, Action<SubmitOutcome> done, bool queueOnFailure)
        {
            yield return EnsureSession();

            // Recovery unresolved: sending this run would make the fresh user a non-empty
            // destination and permanently block the claim (R1). Queue instead - the drain
            // runs after recovery settles.
            if (_recoveryPending || !HasLiveToken || _dormantAfterDeletion)
            {
                if (queueOnFailure) Enqueue(run);
                ScheduleDrain(SessionRetryDelay);
                done?.Invoke(new SubmitOutcome(queueOnFailure ? SubmitStatus.Queued : SubmitStatus.Rejected,
                    error: new SocialError("offline",
                        _recoveryPending ? "recovery pending" : "no session")));
                yield break;
            }

            // The submit throttle gates every submission with this token (R4), not just the
            // next session refresh.
            if (Time.realtimeSinceStartup < _submitNotBefore)
            {
                if (queueOnFailure) Enqueue(run);
                ScheduleDrain(_submitNotBefore - Time.realtimeSinceStartup);
                done?.Invoke(new SubmitOutcome(queueOnFailure ? SubmitStatus.Queued : SubmitStatus.Rejected,
                    error: new SocialError("throttled", "will retry")));
                yield break;
            }

            long resultStatus = 0;
            string resultBody = null;
            float retryAfter = 0f;
            yield return CallFunction("submit-run", JsonUtility.ToJson(run),
                (status, body, ra) => { resultStatus = status; resultBody = body; retryAfter = ra; });

            if (resultStatus == 200)
            {
                _submitBackoff = RetrySchedule.FirstDelaySeconds;
                var response = JsonUtility.FromJson<SubmitResponse>(resultBody);
                if (response == null)
                {
                    done?.Invoke(new SubmitOutcome(SubmitStatus.Rejected,
                        error: new SocialError("bad_response", "unparseable")));
                    yield break;
                }
                if (response.duplicate)
                {
                    done?.Invoke(new SubmitOutcome(SubmitStatus.Duplicate,
                        response.daily_rank, response.alltime_rank));
                    yield break;
                }
                done?.Invoke(response.accepted
                    ? new SubmitOutcome(SubmitStatus.Accepted, response.daily_rank, response.alltime_rank)
                    : new SubmitOutcome(SubmitStatus.NotAccepted,
                        error: new SocialError("flagged", response.reason ?? string.Empty)));
                yield break;
            }

            if (RetrySchedule.IsTransient(resultStatus))
            {
                float delay = RetrySchedule.Delay(_submitBackoff, retryAfter,
                    UnityEngine.Random.Range(0.8f, 1.3f));
                _submitNotBefore = Time.realtimeSinceStartup + delay;
                _submitBackoff = RetrySchedule.NextBackoff(_submitBackoff);

                if (queueOnFailure) Enqueue(run);
                ScheduleDrain(delay);
                done?.Invoke(new SubmitOutcome(queueOnFailure ? SubmitStatus.Queued : SubmitStatus.Rejected,
                    error: new SocialError(resultStatus == 0 ? "offline" : "http_" + resultStatus,
                        "will retry")));
                yield break;
            }

            // A definitive 4xx said no — retrying the same payload will say no again.
            done?.Invoke(new SubmitOutcome(SubmitStatus.Rejected,
                error: new SocialError("http_" + resultStatus, resultBody ?? string.Empty)));
        }

        public void FetchBoard(BoardQuery query, Action<BoardResult, SocialError> done)
        {
            StartCoroutine(FetchBoardRoutine(query, done));
        }

        IEnumerator FetchBoardRoutine(BoardQuery query, Action<BoardResult, SocialError> done)
        {
            yield return EnsureSession();
            if (!HasLiveToken || _dormantAfterDeletion)
            {
                done?.Invoke(null, new SocialError("offline", "no session"));
                yield break;
            }

            string scope = query.Scope == BoardScope.AllTime ? "alltime" : "daily";
            string date = query.Scope == BoardScope.AllTime ? "1970-01-01" : query.DayLabel;
            string args = "{\"p_scope\":\"" + scope + "\"," +
                          "\"p_run_date\":\"" + date + "\"," +
                          "\"p_content_version\":\"" + query.ContentVersion + "\"," +
                          "\"p_world_id\":\"" + query.WorldId + "\"," +
                          "\"p_input_group\":\"" + query.InputGroup + "\"";

            LeaderboardResponse rows = null;
            long rowsStatus = 0;
            yield return CallRpc("get_leaderboard", args + ",\"p_limit\":" + query.Limit + "}",
                (status, body, _) =>
                {
                    rowsStatus = status;
                    if (status == 200) rows = JsonUtility.FromJson<LeaderboardResponse>(body);
                });
            if (rowsStatus != 200 || rows == null)
            {
                done?.Invoke(null, new SocialError("http_" + rowsStatus, "board fetch failed"));
                yield break;
            }

            MyRankResponse mine = null;
            yield return CallRpc("get_my_rank", args + "}",
                (status, body, _) =>
                {
                    if (status == 200) mine = JsonUtility.FromJson<MyRankResponse>(body);
                });

            var list = new List<BoardRow>(rows.entries?.Count ?? 0);
            if (rows.entries != null)
                foreach (var entry in rows.entries)
                    list.Add(new BoardRow(entry.rank, entry.handle, entry.score));

            done?.Invoke(new BoardResult(list, mine?.rank ?? 0, mine?.score ?? 0), null);
        }

        public void RerollHandle(Action<Profile, SocialError> done)
        {
            StartCoroutine(RerollRoutine(done));
        }

        IEnumerator RerollRoutine(Action<Profile, SocialError> done)
        {
            yield return EnsureSession();
            if (!HasLiveToken || Current == null || _dormantAfterDeletion)
            {
                done?.Invoke(Current, new SocialError("offline", "no session"));
                yield break;
            }

            yield return CallRpc("reroll_handle", "{}", (status, body, _) =>
            {
                if (status != 200)
                {
                    done?.Invoke(Current, new SocialError("http_" + status, body ?? string.Empty));
                    return;
                }
                var response = JsonUtility.FromJson<RerollResponse>(body);
                if (response == null)
                {
                    done?.Invoke(Current, new SocialError("bad_response", "unparseable"));
                    return;
                }
                if (string.IsNullOrEmpty(response.handle))
                {
                    // Unlimited rerolls: an empty handle now only means the burst throttle.
                    done?.Invoke(Current, new SocialError("throttled", "one moment between name changes"));
                    return;
                }
                Current = new Profile(Current.UserId, response.handle, Current.Xp);
                ProfileChanged?.Invoke();
                done?.Invoke(Current, null);
            });
        }

        /// The current profile's own recovery code (owner call, 18 Sep: player-visible). A
        /// foreign key — a pending claim's — is never exposed as "yours".
        public string RecoveryCode
        {
            get
            {
                if (_userId == null) return string.Empty;
                var stored = ReadRecoveryKey();
                return stored.UserId == _userId ? stored.Key ?? string.Empty : string.Empty;
            }
        }

        public void ImportProfile(string recoveryCode, Action<SocialError> done)
        {
            if (string.IsNullOrEmpty(recoveryCode) || recoveryCode.Trim().Length != 64)
            {
                done?.Invoke(new SocialError("bad_code", "a recovery code is 64 characters"));
                return;
            }
            StartCoroutine(ImportRoutine(recoveryCode.Trim().ToLowerInvariant(), done));
        }

        IEnumerator ImportRoutine(string code, Action<SocialError> done)
        {
            yield return EnsureSession();
            if (!HasLiveToken || _dormantAfterDeletion)
            {
                done?.Invoke(new SocialError("offline", "no session"));
                yield break;
            }

            long status = 0;
            RecoverResponse response = null;
            yield return CallFunction("recover-session",
                "{\"recovery_key\":\"" + code + "\"}",
                (s, body, _) =>
                {
                    status = s;
                    if (body != null)
                    {
                        try { response = JsonUtility.FromJson<RecoverResponse>(body); }
                        catch (Exception) { response = null; }
                    }
                });

            switch (RecoveryGate.Resolve(status, response?.recovered ?? false,
                        response?.reason ?? string.Empty))
            {
                case RecoveryGate.Resolution.Claimed:
                    if (!string.IsNullOrEmpty(response.recovery_key))
                        WriteRecoveryKey(_userId, response.recovery_key);
                    else
                        DeleteRecoveryKeyFile();
                    _recoveryPending = false;
                    // A successful self-import may have no rotated key. Obtain and persist
                    // one before reporting success; failed issuance keeps retrying this boot.
                    yield return EnsureRecoveryKey();
                    // The profile under this user changed identity: reload it.
                    IsReady = false;
                    Current = null;
                    yield return LoadProfile();
                    ScheduleDrain(0f);
                    done?.Invoke(!IsReady
                        ? new SocialError("offline", "imported — profile loads on next launch")
                        : string.IsNullOrEmpty(RecoveryCode)
                            ? new SocialError("offline", "imported — recovery code pending; keep app data until it appears")
                            : null);
                    yield break;
                case RecoveryGate.Resolution.Blocked:
                    done?.Invoke(new SocialError("destination_not_empty",
                        "this install has already played — delete its online profile, restart, then import"));
                    yield break;
                case RecoveryGate.Resolution.KeyInvalid:
                    done?.Invoke(new SocialError("not_found", "code not recognized"));
                    yield break;
                default:
                    done?.Invoke(new SocialError(status == 429 ? "throttled" : "offline",
                        "couldn't reach the server — try again"));
                    yield break;
            }
        }

        public void DeleteAccount(Action<SocialError> done)
        {
            StartCoroutine(DeleteRoutine(done));
        }

        IEnumerator DeleteRoutine(Action<SocialError> done)
        {
            yield return EnsureSession();
            if (!HasLiveToken)
            {
                done?.Invoke(new SocialError("offline", "no session"));
                yield break;
            }

            long resultStatus = 0;
            yield return CallFunction("delete-account", "{}",
                (status, _, __) => resultStatus = status);

            if (resultStatus != 200)
            {
                done?.Invoke(new SocialError("http_" + resultStatus, "delete failed"));
                yield break;
            }

            // Everything local about this identity goes with the account, and the service
            // goes DORMANT: no call may silently create a replacement account inside the
            // deletion flow (review R2). The next app launch starts fresh. Local device stats
            // (bests, history) are deliberately untouched — they are device data, and the
            // player-facing copy says so.
            _dormantAfterDeletion = true;
            _accessToken = null;
            _userId = null;
            Current = null;
            IsReady = false;
            _recoveryPending = false;
            ClearBackoff();
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(PendingKey);
            PlayerPrefs.Save();
            DeleteRecoveryKeyFile();
            DeleteRecoveryKeyFile(SupportRecoveryPath);

            ProfileChanged?.Invoke();
            done?.Invoke(null);
        }

        // ---------------- offline queue ----------------

        void Enqueue(RunSubmission run)
        {
            PlayerPrefs.SetString(PendingKey,
                PendingRuns.Append(PlayerPrefs.GetString(PendingKey, string.Empty), run));
            PlayerPrefs.Save();
        }

        /// The queue has its own retry loop (review R4): every transient failure schedules a
        /// future drain instead of waiting for the next profile load or app restart.
        void ScheduleDrain(float delaySeconds)
        {
            if (_drainScheduled || _dormantAfterDeletion) return;
            if (PlayerPrefs.GetString(PendingKey, string.Empty).Length == 0) return;
            _drainScheduled = true;
            StartCoroutine(DrainAfter(delaySeconds));
        }

        IEnumerator DrainAfter(float delaySeconds)
        {
            if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);
            _drainScheduled = false;
            yield return DrainPending();
        }

        IEnumerator DrainPending()
        {
            if (_draining || _dormantAfterDeletion) yield break;
            _draining = true;
            try
            {
                var queued = PendingRuns.Decode(PlayerPrefs.GetString(PendingKey, string.Empty));
                foreach (var run in queued)
                {
                    bool settled = false;
                    bool keep = false;
                    // queueOnFailure false: the run is ALREADY queued; re-queueing would dupe.
                    yield return SubmitRoutine(run, outcome =>
                    {
                        settled = true;
                        keep = outcome.Status == SubmitStatus.Queued ||
                               (outcome.Status == SubmitStatus.Rejected &&
                                outcome.Error != null &&
                                (outcome.Error.Code == "offline" ||
                                 outcome.Error.Code == "throttled" ||
                                 outcome.Error.Code.StartsWith("http_5") ||
                                 outcome.Error.Code == "http_429"));
                    }, queueOnFailure: false);

                    if (!settled || keep)
                    {
                        // Transient again: keep the rest queued and try later on the submit
                        // gate's own schedule.
                        float delay = Mathf.Max(_submitNotBefore - Time.realtimeSinceStartup,
                            SessionRetryDelay);
                        _draining = false;
                        ScheduleDrain(delay);
                        yield break;
                    }

                    PlayerPrefs.SetString(PendingKey,
                        PendingRuns.Remove(PlayerPrefs.GetString(PendingKey, string.Empty),
                            run.client_run_id));
                    PlayerPrefs.Save();
                }
            }
            finally
            {
                _draining = false;
            }
        }

        /// Coming back to the foreground is the cheapest "connectivity may be back" signal:
        /// kick the session (which retries recovery/profile) and the queue.
        void OnApplicationPause(bool paused)
        {
            if (paused || _dormantAfterDeletion) return;
            StartCoroutine(EnsureSession());
            ScheduleDrain(1f);
        }

        float SessionRetryDelay => Mathf.Max(_retryNotBefore - Time.realtimeSinceStartup,
            RetrySchedule.FirstDelaySeconds);

        // ---------------- recovery key storage ----------------
        // A file under persistentDataPath: covered by Android Auto Backup (the mechanism that
        // demonstrably restores this app's data across reinstall — STATUS 29 Aug). Format
        // "<userId>:<hexKey>" so a stale key is recognized. Plaintext by design — see the
        // class comment. iOS: Keychain with T-032.

        internal readonly struct StoredKey
        {
            public readonly string UserId;
            public readonly string Key;

            public StoredKey(string userId, string key)
            {
                UserId = userId;
                Key = key;
            }
        }

        static string RecoveryPath => Path.Combine(Application.persistentDataPath, RecoveryFileName);
        static string SupportRecoveryPath => Path.Combine(Application.persistentDataPath, SupportRecoveryFileName);

        static bool ArchiveRecoveryKey()
        {
            try
            {
                // Append preserves earlier blocked profiles too; retries after a failed delete
                // are deduplicated. Delete the active copy only after the archive is durable.
                string key = File.ReadAllText(RecoveryPath).Trim();
                var saved = File.Exists(SupportRecoveryPath)
                    ? new HashSet<string>(File.ReadAllLines(SupportRecoveryPath))
                    : new HashSet<string>();
                if (!saved.Contains(key)) File.AppendAllText(SupportRecoveryPath, key + Environment.NewLine);
                File.Delete(RecoveryPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Profile] could not preserve blocked recovery key: " + e.Message);
                return false;
            }
        }

        static StoredKey ReadRecoveryKey()
        {
            try
            {
                if (!File.Exists(RecoveryPath)) return default;
                var text = File.ReadAllText(RecoveryPath).Trim();
                int split = text.IndexOf(':');
                if (split <= 0) return new StoredKey(string.Empty, text); // pre-hardening format
                return new StoredKey(text.Substring(0, split), text.Substring(split + 1));
            }
            catch (Exception)
            {
                return default;
            }
        }

        static void WriteRecoveryKey(string userId, string key)
        {
            try
            {
                File.WriteAllText(RecoveryPath, userId + ":" + key);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Profile] could not persist recovery key: " + e.Message);
            }
        }

        static void DeleteRecoveryKeyFile(string path = null)
        {
            try
            {
                path = path ?? RecoveryPath;
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // Losing this delete only means a dead key lingers in a backup; it recovers nothing.
            }
        }

        // ---------------- HTTP plumbing ----------------

        sealed class RequestOutcome
        {
            public long Status;
            public float RetryAfterSeconds;

            public void Fill(long status, float retryAfter)
            {
                Status = status;
                RetryAfterSeconds = retryAfter;
            }
        }

        IEnumerator CallFunction(string name, string bodyJson, Action<long, string, float> done) =>
            SendJson(UnityWebRequest.kHttpVerbPOST, _url + "/functions/v1/" + name,
                bodyJson, true, done);

        IEnumerator CallRpc(string name, string bodyJson, Action<long, string, float> done) =>
            SendJson(UnityWebRequest.kHttpVerbPOST, _url + "/rest/v1/rpc/" + name,
                bodyJson, true, done);

        /// One request. `done(status, body, retryAfterSeconds)` always runs; status 0 = no
        /// HTTP answer at all (offline, DNS, timeout).
        IEnumerator SendJson(string method, string url, string bodyJson, bool authenticated,
            Action<long, string, float> done, string accept = null)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                if (bodyJson != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("apikey", _anonKey);
                if (authenticated && _accessToken != null)
                    request.SetRequestHeader("Authorization", "Bearer " + _accessToken);
                if (accept != null) request.SetRequestHeader("Accept", accept);
                request.timeout = 15;

                yield return request.SendWebRequest();

                float retryAfter = 0f;
                string retryHeader = request.GetResponseHeader("Retry-After");
                if (!string.IsNullOrEmpty(retryHeader))
                    float.TryParse(retryHeader, out retryAfter);

                string body = request.downloadHandler != null ? request.downloadHandler.text : null;
                done?.Invoke(request.responseCode, body, retryAfter);
            }
        }

        // ---------------- wire DTOs (JsonUtility: public fields = JSON keys) ----------------

#pragma warning disable 0649
        [Serializable]
        class AuthResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public AuthUser user;
        }

        [Serializable]
        class AuthUser
        {
            public string id;
        }

        [Serializable]
        class ProfileRow
        {
            public string handle;
            public int xp;
        }

        [Serializable]
        class SubmitResponse
        {
            public bool accepted;
            public bool duplicate;
            public string reason;
            public int daily_rank;
            public int alltime_rank;
        }

        [Serializable]
        class LeaderboardResponse
        {
            public List<LeaderboardRowDto> entries;
        }

        [Serializable]
        class LeaderboardRowDto
        {
            public string handle;
            public int score;
            public int rank;
        }

        [Serializable]
        class MyRankResponse
        {
            public int rank;
            public int score;
        }

        [Serializable]
        class RerollResponse
        {
            public string handle;
        }

        [Serializable]
        class RecoverResponse
        {
            public bool recovered;
            public string recovery_key;
            public string reason;
        }

        [Serializable]
        class RotateKeyResponse
        {
            public string recovery_key;
        }
#pragma warning restore 0649
    }
}
