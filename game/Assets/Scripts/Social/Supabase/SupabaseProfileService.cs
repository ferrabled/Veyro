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
    /// docs/PROFILE_LEADERBOARD_PLAN.md.
    ///
    /// Failure discipline (17 Sep hardening):
    ///  * Transient (no HTTP answer, 429, 5xx) is NEVER terminal: sessions are kept,
    ///    submissions stay queued, and retries back off exponentially with jitter, honouring
    ///    Retry-After when the server sends one.
    ///  * The refresh token is dropped ONLY on a definitive auth rejection (400/401/403) —
    ///    a throttled or failing server must not cost the player their identity, and a
    ///    transient failure must never mint a new anonymous account over a live one.
    ///  * An expired session that cannot be refreshed right now counts as "no session":
    ///    nothing is ever sent with a stale token.
    ///
    /// Identity (plan §1): anonymous sign-in at first use; refresh token in PlayerPrefs for
    /// ordinary relaunches; the RECOVERY KEY in a persistentDataPath file that Android Auto
    /// Backup carries across reinstall. The key file is tagged with the user id it belongs
    /// to, and the server's rotate_recovery_key() is retry-safe — so a lost response or a
    /// claim that rotated the key server-side just means the next boot rotates again and
    /// overwrites the stale file. iOS moves the key into the Keychain with T-032.
    ///
    /// The key file is plaintext BY DESIGN: it must survive reinstall via device backup, and
    /// Android Keystore-backed encryption keys do NOT survive reinstall — encrypting with one
    /// would break exactly the property the key exists for. A local attacker who can read
    /// this file can also read the PlayerPrefs session, which is the same level of access.
    public sealed class SupabaseProfileService : MonoBehaviour, IProfileService
    {
        const string RefreshTokenKey = "veyro.sb.refresh";
        const string PendingKey = "veyro.runs.pending";
        const string RecoveryFileName = "veyro-recovery.txt";

        /// Refresh the access token this many seconds before it actually expires.
        const float ExpirySlackSeconds = 60f;

        /// Transient-failure backoff bounds (seconds). Doubles per failure, jittered.
        const float BackoffFirstSeconds = 5f;
        const float BackoffMaxSeconds = 300f;

        string _url;
        string _anonKey;

        string _accessToken;
        string _userId;
        float _accessExpiresAt; // Time.realtimeSinceStartup clock

        bool _sessionWork;       // serializes Boot/refresh — never two in flight
        float _retryNotBefore;   // backoff gate for session work
        float _backoffSeconds = BackoffFirstSeconds;

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

        // ---------------- session ----------------

        bool HasLiveToken => _accessToken != null &&
                             Time.realtimeSinceStartup < _accessExpiresAt;

        /// The one session entry point. Serialized (a second caller waits for the first),
        /// backoff-gated, and it never invents a new anonymous identity while a transient
        /// failure hides the old one.
        IEnumerator EnsureSession()
        {
            while (_sessionWork) yield return null;
            if (HasLiveToken) yield break;
            if (Time.realtimeSinceStartup < _retryNotBefore) yield break;

            _sessionWork = true;
            try
            {
                // 1. Refresh whatever token we hold.
                string refresh = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
                if (!string.IsNullOrEmpty(refresh))
                {
                    var outcome = new RequestOutcome();
                    yield return RefreshSession(refresh, outcome);
                    if (HasLiveToken)
                    {
                        ClearBackoff();
                        yield return LoadProfileAndFinish();
                        yield break;
                    }
                    if (outcome.Transient)
                    {
                        // The token may still be perfectly valid — back off, try later,
                        // and DO NOT fall through to creating a new anonymous user.
                        ApplyBackoff(outcome.RetryAfterSeconds);
                        yield break;
                    }
                    // Definitive rejection: the token is dead. Fall through to a fresh
                    // identity (+ recovery-key claim).
                }

                // 2. Fresh install / dead token: new anonymous user.
                var signup = new RequestOutcome();
                yield return SignInAnonymously(signup);
                if (!HasLiveToken)
                {
                    ApplyBackoff(signup.RetryAfterSeconds);
                    yield break;
                }
                ClearBackoff();

                // 3. If a recovery key survived in the device backup, claim the old profile
                //    onto this fresh user. The key file is tagged with the user it belonged
                //    to; any stale key is overwritten by the rotate below.
                var stored = ReadRecoveryKey();
                if (!string.IsNullOrEmpty(stored.Key) && stored.UserId != _userId)
                {
                    yield return CallFunction("recover-session",
                        "{\"recovery_key\":\"" + stored.Key + "\"}",
                        (status, body, _) =>
                        {
                            if (status != 200) return;
                            var response = JsonUtility.FromJson<RecoverResponse>(body);
                            if (response == null || !response.recovered) return;
                            if (!string.IsNullOrEmpty(response.recovery_key))
                                WriteRecoveryKey(_userId, response.recovery_key);
                        });
                }

                yield return LoadProfileAndFinish();
            }
            finally
            {
                _sessionWork = false;
            }
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
                    // ONLY a definitive auth rejection kills the stored token. 429 and 5xx
                    // and no-answer keep it: the server being busy is not the player's
                    // identity being invalid.
                    if (status == 400 || status == 401 || status == 403)
                        PlayerPrefs.DeleteKey(RefreshTokenKey);
                });
        }

        void AdoptSession(string body)
        {
            var session = JsonUtility.FromJson<AuthResponse>(body);
            if (session == null || string.IsNullOrEmpty(session.access_token)) return;

            _accessToken = session.access_token;
            _userId = session.user != null ? session.user.id : _userId;
            _accessExpiresAt = Time.realtimeSinceStartup +
                               Mathf.Max(session.expires_in, 120) - ExpirySlackSeconds;

            if (!string.IsNullOrEmpty(session.refresh_token))
            {
                PlayerPrefs.SetString(RefreshTokenKey, session.refresh_token);
                PlayerPrefs.Save();
            }
        }

        IEnumerator LoadProfileAndFinish()
        {
            if (!HasLiveToken) yield break;

            // The object accept header makes PostgREST return ONE json object, not a one-
            // element array — JsonUtility cannot parse a top-level array. Columns are explicit
            // and must stay within the column-level SELECT grant (0004): recovery_key_hash is
            // deliberately not readable by clients.
            yield return SendJson(UnityWebRequest.kHttpVerbGET,
                _url + "/rest/v1/profiles?select=handle,rerolls_left,xp", null, true,
                (status, body, _) =>
                {
                    if (status != 200) return;
                    var row = JsonUtility.FromJson<ProfileRow>(body);
                    if (row == null || string.IsNullOrEmpty(row.handle)) return;

                    Current = new Profile(_userId, row.handle, row.rerolls_left, row.xp);
                    IsReady = true;
                    ProfileChanged?.Invoke();
                },
                accept: "application/vnd.pgrst.object+json");

            if (!IsReady) yield break;

            // Retry-safe key issuance: no key on file, or a key that belongs to another user
            // (pre-claim leftover, lost rotate response) → rotate. The old key stops working
            // server-side, so stale credentials are never accepted indefinitely.
            var stored = ReadRecoveryKey();
            if (string.IsNullOrEmpty(stored.Key) || stored.UserId != _userId)
            {
                yield return CallRpc("rotate_recovery_key", "{}", (status, body, _) =>
                {
                    if (status != 200) return;
                    var response = JsonUtility.FromJson<RotateKeyResponse>(body);
                    if (response != null && !string.IsNullOrEmpty(response.recovery_key))
                        WriteRecoveryKey(_userId, response.recovery_key);
                });
            }

            StartCoroutine(DrainPending());
        }

        void ApplyBackoff(float retryAfterSeconds)
        {
            float delay = retryAfterSeconds > 0f
                ? retryAfterSeconds
                : _backoffSeconds * UnityEngine.Random.Range(0.8f, 1.3f);
            _retryNotBefore = Time.realtimeSinceStartup + Mathf.Min(delay, BackoffMaxSeconds);
            _backoffSeconds = Mathf.Min(_backoffSeconds * 2f, BackoffMaxSeconds);
        }

        void ClearBackoff()
        {
            _backoffSeconds = BackoffFirstSeconds;
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
            if (!HasLiveToken)
            {
                if (queueOnFailure) Enqueue(run);
                done?.Invoke(new SubmitOutcome(queueOnFailure ? SubmitStatus.Queued : SubmitStatus.Rejected,
                    error: new SocialError("offline", "no session")));
                yield break;
            }

            long resultStatus = 0;
            string resultBody = null;
            float retryAfter = 0f;
            yield return CallFunction("submit-run", JsonUtility.ToJson(run),
                (status, body, ra) => { resultStatus = status; resultBody = body; retryAfter = ra; });

            if (resultStatus == 200)
            {
                var response = JsonUtility.FromJson<SubmitResponse>(resultBody);
                if (response == null)
                {
                    done?.Invoke(new SubmitOutcome(SubmitStatus.Rejected,
                        error: new SocialError("bad_response", "unparseable")));
                    yield break;
                }
                if (response.duplicate)
                {
                    done?.Invoke(new SubmitOutcome(SubmitStatus.Duplicate));
                    yield break;
                }
                done?.Invoke(response.accepted
                    ? new SubmitOutcome(SubmitStatus.Accepted, response.daily_rank, response.alltime_rank)
                    : new SubmitOutcome(SubmitStatus.NotAccepted,
                        error: new SocialError("flagged", response.reason ?? string.Empty)));
                yield break;
            }

            // Transient (no answer, throttled, server error): worth retrying later. 429 also
            // arms the backoff so the drain does not hammer a throttling server.
            bool transient = resultStatus == 0 || resultStatus == 429 || resultStatus >= 500;
            if (transient)
            {
                if (resultStatus == 429) ApplyBackoff(retryAfter);
                if (queueOnFailure) Enqueue(run);
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
            if (!HasLiveToken)
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
            if (!HasLiveToken || Current == null)
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
                    done?.Invoke(Current, new SocialError("no_rerolls", "reroll budget spent"));
                    return;
                }
                Current = new Profile(Current.UserId, response.handle, response.rerolls_left, Current.Xp);
                ProfileChanged?.Invoke();
                done?.Invoke(Current, null);
            });
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

            // Everything local about this identity goes with the account (plan §5): session,
            // recovery key, queued runs. The next launch starts a fresh profile.
            _accessToken = null;
            _userId = null;
            Current = null;
            IsReady = false;
            ClearBackoff();
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(PendingKey);
            PlayerPrefs.Save();
            DeleteRecoveryKeyFile();

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

        IEnumerator DrainPending()
        {
            var queued = PendingRuns.Decode(PlayerPrefs.GetString(PendingKey, string.Empty));
            foreach (var run in queued)
            {
                bool settled = false;
                bool keep = false;
                // queueOnFailure false: the run is ALREADY in the queue; re-queueing would dupe.
                yield return SubmitRoutine(run, outcome =>
                {
                    settled = true;
                    keep = outcome.Status == SubmitStatus.Queued ||
                           (outcome.Status == SubmitStatus.Rejected &&
                            outcome.Error != null &&
                            (outcome.Error.Code == "offline" ||
                             outcome.Error.Code.StartsWith("http_5") ||
                             outcome.Error.Code == "http_429"));
                }, queueOnFailure: false);

                if (!settled || keep) yield break; // transient again — keep the rest queued

                PlayerPrefs.SetString(PendingKey,
                    PendingRuns.Remove(PlayerPrefs.GetString(PendingKey, string.Empty),
                        run.client_run_id));
                PlayerPrefs.Save();
            }
        }

        // ---------------- recovery key storage ----------------
        // A file under persistentDataPath: covered by Android Auto Backup (the mechanism that
        // demonstrably restores this app's data across reinstall — STATUS 29 Aug). Format
        // "<userId>:<hexKey>" so a stale key (rotated away, or belonging to a pre-claim user)
        // is recognized and replaced by rotate_recovery_key. Plaintext by design — see the
        // class comment. iOS: Keychain with T-032.

        readonly struct StoredKey
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

        static void DeleteRecoveryKeyFile()
        {
            try
            {
                if (File.Exists(RecoveryPath)) File.Delete(RecoveryPath);
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

            public bool Transient => Status == 0 || Status == 429 || Status >= 500;

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
            public int rerolls_left;
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
            public int rerolls_left;
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
