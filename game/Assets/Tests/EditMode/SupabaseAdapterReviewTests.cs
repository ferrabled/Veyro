using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MotionRunner.Social;
using NUnit.Framework;
using UnityEngine;

namespace MotionRunner.Tests
{
    public sealed class SupabaseAdapterReviewTests
    {
        const string Pending = "veyro.runs.pending";
        const string Refresh = "veyro.sb.refresh";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly Dictionary<string, string> _prefs = new Dictionary<string, string>();
        readonly Queue<(string Path, long Status, string Body)> _responses = new Queue<(string, long, string)>();
        GameObject _root;
        object _service;
        Type _type;
        string _recoveryPath;
        byte[] _savedKey;
        string _supportPath;
        byte[] _savedSupportKeys;

        [SetUp]
        public void SetUp()
        {
            // Preserve the developer's device state; no credentials leave this process.
            foreach (var key in new[] { Pending, Refresh })
            {
                _prefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
                PlayerPrefs.DeleteKey(key);
            }
            _recoveryPath = Path.Combine(Application.persistentDataPath, "veyro-recovery.txt");
            _savedKey = File.Exists(_recoveryPath) ? File.ReadAllBytes(_recoveryPath) : null;
            if (File.Exists(_recoveryPath)) File.Delete(_recoveryPath);
            _supportPath = Path.Combine(Application.persistentDataPath, "veyro-recovery-support.txt");
            _savedSupportKeys = File.Exists(_supportPath) ? File.ReadAllBytes(_supportPath) : null;
            if (File.Exists(_supportPath)) File.Delete(_supportPath);
            _root = new GameObject("SupabaseAdapterReviewTest");
            _type = Type.GetType("MotionRunner.Social.Supabase.SupabaseProfileService, MotionRunner.Social.Supabase", true);
            _service = _root.AddComponent(_type);
            Set("_url", "https://test.invalid");
            Set("_anonKey", "test");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            foreach (var pair in _prefs)
            {
                if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
                else PlayerPrefs.SetString(pair.Key, pair.Value);
            }
            PlayerPrefs.Save();
            if (_savedKey != null) File.WriteAllBytes(_recoveryPath, _savedKey);
            else if (File.Exists(_recoveryPath)) File.Delete(_recoveryPath);
            if (_savedSupportKeys != null) File.WriteAllBytes(_supportPath, _savedSupportKeys);
            else if (File.Exists(_supportPath)) File.Delete(_supportPath);
            _responses.Clear();
        }

        [Test]
        public void SessionFailureSchedulesQueuedRunAndLaterDrainSubmitsWithoutResume()
        {
            _responses.Enqueue(("/auth/v1/signup", 503, "{}"));
            SubmitOutcome outcome = default;
            Execute(Routine("SubmitRoutine", Run(), (Action<SubmitOutcome>)(o => outcome = o), true));
            Assert.AreEqual(SubmitStatus.Queued, outcome.Status);
            Assert.IsTrue((bool)Get("_drainScheduled"));
            Set("_retryNotBefore", 0f); // Advance past the session gate, without a real wait.
            _responses.Enqueue(("/auth/v1/signup", 200,
                "{\"access_token\":\"test\",\"expires_in\":3600,\"user\":{\"id\":\"player\"}}"));
            ProfileAndKeyResponses();
            _responses.Enqueue(("/functions/v1/submit-run", 200, "{\"accepted\":true}"));
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200, "{\"handle\":\"RUNNER\",\"xp\":43}"));
            Execute(Routine("DrainPending"));
            Assert.AreEqual(0, PendingRuns.Decode(PlayerPrefs.GetString(Pending, "")).Count);
            Assert.AreEqual(0, _responses.Count);
        }

        [Test]
        public void PendingRecoveryDrainRetriesClaimBeforeSendingRun()
        {
            LiveSession();
            File.WriteAllText(_recoveryPath, "old-player:" + new string('a', 64));
            PlayerPrefs.SetString(Pending, PendingRuns.Append("", Run()));
            Set("_recoveryPending", true);
            _responses.Enqueue(("/functions/v1/recover-session", 200,
                "{\"recovered\":true,\"recovery_key\":\"" + new string('b', 64) + "\"}"));
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200,
                "{\"handle\":\"RECOVERED\",\"xp\":42}"));
            _responses.Enqueue(("/functions/v1/submit-run", 200, "{\"accepted\":true}"));
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200, "{\"handle\":\"RUNNER\",\"xp\":43}"));
            Execute(Routine("DrainPending"));
            Assert.AreEqual(0, _responses.Count, "drain stopped at the old recovery guard");
            Assert.AreEqual(0, PendingRuns.Decode(PlayerPrefs.GetString(Pending, "")).Count);
        }

        [Test]
        public void DuplicateResponsePreservesBothRanks()
        {
            ReadySession();
            _responses.Enqueue(("/functions/v1/submit-run", 200,
                "{\"accepted\":true,\"duplicate\":true,\"daily_rank\":3,\"alltime_rank\":7}"));
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200, "{\"handle\":\"RUNNER\",\"xp\":5}"));
            SubmitOutcome outcome = default;
            Execute(Routine("SubmitRoutine", Run(), (Action<SubmitOutcome>)(o => outcome = o), true));
            Assert.AreEqual(SubmitStatus.Duplicate, outcome.Status);
            Assert.AreEqual(3, outcome.DailyRank);
            Assert.AreEqual(7, outcome.AlltimeRank);
            Assert.AreEqual(5, ((IProfileService)_service).Current.Xp);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingImportKeyIsIssuedBeforeSuccessOrRetriedAfterFailure(bool issueSucceeds)
        {
            ReadySession();
            _responses.Enqueue(("/functions/v1/recover-session", 200, "{\"recovered\":true}"));
            _responses.Enqueue(("/rest/v1/rpc/rotate_recovery_key", issueSucceeds ? 200 : 503,
                issueSucceeds ? "{\"recovery_key\":\"" + new string('b', 64) + "\"}" : "{}"));
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200,
                "{\"handle\":\"IMPORTED\",\"xp\":42}"));
            SocialError error = null;
            bool completed = false;
            Execute(Routine("ImportRoutine", new string('a', 64), (Action<SocialError>)(e =>
            {
                error = e;
                completed = true;
                if (e == null) Assert.IsNotEmpty(((IProfileService)_service).RecoveryCode);
            })));
            Assert.IsTrue(completed);
            Assert.AreEqual(0, _responses.Count);
            if (issueSucceeds) Assert.IsNull(error);
            else
            {
                Assert.IsNotNull(error);
                Assert.IsTrue((bool)Get("_keyRetryScheduled"));
                Assert.IsFalse((bool)_type.GetProperty("SessionSettled", Private).GetValue(_service));
                int changes = 0;
                ((IProfileService)_service).ProfileChanged += () => changes++;
                Set("_retryNotBefore", 0f);
                _responses.Enqueue(("/rest/v1/rpc/rotate_recovery_key", 200,
                    "{\"recovery_key\":\"" + new string('b', 64) + "\"}"));
                Execute(Routine("EnsureSession"));
                Assert.IsNotEmpty(((IProfileService)_service).RecoveryCode);
                Assert.AreEqual(1, changes, "open profile tab was not notified when code arrived");
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BlockedRecoveryPreservesForeignKeyAndIssuesOwnCodeAcrossRestart(bool issueSucceeds)
        {
            LiveSession();
            string foreign = "old-player:" + new string('a', 64);
            File.WriteAllText(_recoveryPath, foreign);
            _responses.Enqueue(("/functions/v1/recover-session", 409,
                "{\"recovered\":false,\"reason\":\"destination_not_empty\"}"));
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200, "{\"handle\":\"CURRENT\",\"xp\":10}"));
            _responses.Enqueue(("/rest/v1/rpc/rotate_recovery_key", issueSucceeds ? 200 : 503,
                issueSucceeds ? "{\"recovery_key\":\"" + new string('b', 64) + "\"}" : "{}"));
            Execute(Routine("EnsureSession"));
            Assert.AreEqual(0, _responses.Count);
            Assert.AreEqual(foreign, File.ReadAllText(_supportPath).Trim());
            Assert.IsFalse((bool)Get("_recoveryPending"));

            // Recreate the component as on relaunch; disk state alone must prevent another claim.
            UnityEngine.Object.DestroyImmediate(_root);
            _root = new GameObject("RestartedProfileTest");
            _service = _root.AddComponent(_type);
            Set("_url", "https://test.invalid");
            Set("_anonKey", "test");
            LiveSession();
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200, "{\"handle\":\"CURRENT\",\"xp\":10}"));
            if (!issueSucceeds)
                _responses.Enqueue(("/rest/v1/rpc/rotate_recovery_key", 200,
                    "{\"recovery_key\":\"" + new string('b', 64) + "\"}"));
            Execute(Routine("EnsureSession"));
            Assert.AreEqual(new string('b', 64), ((IProfileService)_service).RecoveryCode);
            Assert.AreEqual(foreign, File.ReadAllText(_supportPath).Trim());
            Assert.AreEqual(0, _responses.Count);
            _responses.Enqueue(("/functions/v1/delete-account", 200, "{\"deleted\":true}"));
            Execute(Routine("DeleteRoutine", (Action<SocialError>)(e => Assert.IsNull(e))));
            Assert.IsFalse(File.Exists(_supportPath), "deletion must clear preserved codes too");
        }

        [Test]
        public void FailedPostRunXpRefreshRetriesOnlyTheProfileRead()
        {
            ReadySession();Set("_xpRefreshPending",true);
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp",503,"{}"));
            Execute(Routine("LoadProfile"));
            Assert.IsTrue((bool)Get("_xpRefreshPending"));
            Assert.AreEqual(0,((IProfileService)_service).Current.Xp);
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp",200,"{\"handle\":\"RUNNER\",\"xp\":12}"));
            Execute(Routine("RetryXpRefresh","player"));
            Assert.AreEqual(12,((IProfileService)_service).Current.Xp);
            Assert.IsFalse((bool)Get("_xpRefreshPending"));
        }

        [Test]
        public void OutOfOrderProfileReadsCannotRollBackRecordedXp()
        {
            ReadySession();
            var first=Routine("LoadProfile");var second=Routine("LoadProfile");
            Assert.IsTrue(first.MoveNext());Assert.IsTrue(second.MoveNext());
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var newer=(Action<long,string,float>)second.Current.GetType().GetField("done",flags).GetValue(second.Current);
            var older=(Action<long,string,float>)first.Current.GetType().GetField("done",flags).GetValue(first.Current);
            newer(200,"{\"handle\":\"RUNNER\",\"xp\":8}",0);
            older(200,"{\"handle\":\"RUNNER\",\"xp\":3}",0);
            Assert.AreEqual(8,((IProfileService)_service).Current.Xp);
        }

        void LiveSession()
        {
            Set("_userId", "player");
            Set("_accessToken", "test");
            Set("_accessExpiresAt", Time.realtimeSinceStartup + 3600f);
        }

        void ReadySession()
        {
            LiveSession();
            _type.GetProperty("IsReady").SetValue(_service, true);
            _type.GetProperty("Current").SetValue(_service, new Profile("player", "RUNNER", 0));
            File.WriteAllText(_recoveryPath, "player:" + new string('a', 64));
        }

        void ProfileAndKeyResponses()
        {
            _responses.Enqueue(("/rest/v1/profiles?select=handle,xp", 200, "{\"handle\":\"RUNNER\"}"));
            _responses.Enqueue(("/rest/v1/rpc/rotate_recovery_key", 200,
                "{\"recovery_key\":\"" + new string('b', 64) + "\"}"));
        }

        static RunSubmission Run() => new RunSubmission
        {
            client_run_id = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", mode = "daily",
            seed = 20260919, content_version = "greybox-1", world_id = "greybox",
            day_label = "2026-09-19", input_mode = "tilt", score = 100, distance_m = 100,
            duration_s = 30, app_version = "test", platform = "android_gp"
        };

        object Get(string field) => _type.GetField(field, Private).GetValue(_service);
        void Set(string field, object value) => _type.GetField(field, Private).SetValue(_service, value);
        IEnumerator Routine(string method, params object[] args) =>
            (IEnumerator)_type.GetMethod(method, Private).Invoke(_service, args);

        void Execute(IEnumerator routine)
        {
            // Intercept ONLY the network leaf, before its MoveNext creates a request. The
            // actual adapter coroutines, JSON parsing, recovery file and queue all execute.
            // This avoids adding a production-only test hook or contacting a real backend.
            var iteratorType = routine.GetType();
            if (iteratorType.Name.StartsWith("<SendJson>"))
            {
                var fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                string url = (string)iteratorType.GetField("url", fields).GetValue(routine);
                Assert.IsNotEmpty(_responses, "unexpected HTTP: " + url);
                var reply = _responses.Dequeue();
                Assert.IsTrue(url.EndsWith(reply.Path), "unexpected HTTP order: " + url);
                var done = (Action<long, string, float>)iteratorType.GetField("done", fields).GetValue(routine);
                done(reply.Status, reply.Body, 0f);
                return;
            }
            int steps = 0;
            while (routine.MoveNext())
            {
                Assert.Less(++steps, 100, "coroutine stalled");
                // Time waits are scheduler leaves, not nested network/session routines.
                if (routine.Current is IEnumerator nested && !(nested is CustomYieldInstruction)) Execute(nested);
            }
        }
    }
}
