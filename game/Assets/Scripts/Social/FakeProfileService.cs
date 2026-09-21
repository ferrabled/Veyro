using System;
using System.Collections.Generic;

namespace MotionRunner.Social
{
    /// In-memory IProfileService for EditMode tests and the Editor, mirroring FakeStore's role:
    /// the profile tab is exercisable in Play mode, submissions land in a list instead of a
    /// database, and everything resets on exit. Knobs script the outcomes the tests need.
    public sealed class FakeProfileService : IProfileService
    {
        public const string DefaultHandle = "SWIFT-FOX-42";

        readonly List<RunSubmission> _pending = new List<RunSubmission>();

        /// Every submission that reached the "server", newest last.
        public List<RunSubmission> Submitted { get; } = new List<RunSubmission>();

        /// The offline queue, visible so tests can assert what a dead network kept.
        public IReadOnlyList<RunSubmission> Pending => _pending;

        /// Scripted board handed back by FetchBoard; null means "board not available".
        public BoardResult NextBoard { get; set; }

        /// Scripted ranks for the next accepted submission.
        public int NextDailyRank { get; set; } = 1;
        public int NextAlltimeRank { get; set; } = 1;

        public bool IsReady { get; private set; }

        public Profile Current { get; private set; }

        public event Action ProfileChanged;

        public static FakeProfileService Ready()
        {
            var fake = new FakeProfileService();
            fake.BecomeReady(new Profile("fake-user", DefaultHandle, 0));
            return fake;
        }

        /// The adapter's "session + profile loaded" moment. Also drains the offline queue,
        /// which is the behaviour the real adapter must match.
        public void BecomeReady(Profile profile)
        {
            Current = profile;
            IsReady = true;
            ProfileChanged?.Invoke();

            var queued = _pending.ToArray();
            _pending.Clear();
            foreach (var run in queued) SubmitRun(run,null);
        }

        public void SubmitRun(RunSubmission run, Action<SubmitOutcome> done)
        {
            if (run == null)
            {
                done?.Invoke(new SubmitOutcome(SubmitStatus.Rejected,
                    error: new SocialError("bad_run", "null submission")));
                return;
            }
            if (!IsReady)
            {
                _pending.Insert(0, run);
                if (_pending.Count > PendingRuns.MaxEntries)
                    _pending.RemoveAt(_pending.Count - 1);
                done?.Invoke(new SubmitOutcome(SubmitStatus.Queued));
                return;
            }

            string id=run.client_run_id;
            if(!string.IsNullOrEmpty(id) && !_processed.Add(id))
            { done?.Invoke(new SubmitOutcome(SubmitStatus.Duplicate)); return; }
            Submitted.Add(run);
            if(Current!=null)
            {
                Current=new Profile(Current.UserId,Current.Handle,(int)Math.Min(int.MaxValue,(long)Current.Xp+Math.Min(Math.Max(0,run.score)/100,500)));
                ProfileChanged?.Invoke();
            }
            done?.Invoke(new SubmitOutcome(SubmitStatus.Accepted, NextDailyRank, NextAlltimeRank));
        }

        public void FetchBoard(BoardQuery query, Action<BoardResult, SocialError> done)
        {
            if (!IsReady || NextBoard == null)
            {
                done?.Invoke(null, new SocialError("not_ready", "no board available"));
                return;
            }
            done?.Invoke(NextBoard, null);
        }

        int _rerolls;
        readonly HashSet<string> _processed=new HashSet<string>();

        /// Rerolls are unlimited (owner call, 17 Sep): always a fresh handle, never a budget.
        public void RerollHandle(Action<Profile, SocialError> done)
        {
            if (!IsReady || Current == null)
            {
                done?.Invoke(null, new SocialError("not_ready", "no profile"));
                return;
            }

            _rerolls++;
            Current = new Profile(Current.UserId, "NEW-" + _rerolls, Current.Xp);
            ProfileChanged?.Invoke();
            done?.Invoke(Current, null);
        }

        /// The code a paste-import will accept, standing in for the real key file.
        public static readonly string FakeRecoveryCode = new string('a', 64);

        public string RecoveryCode => IsReady ? FakeRecoveryCode : string.Empty;

        public void ImportProfile(string recoveryCode, Action<SocialError> done)
        {
            if (!IsReady)
            {
                done?.Invoke(new SocialError("offline", "no session"));
                return;
            }
            if (recoveryCode != FakeRecoveryCode)
            {
                done?.Invoke(new SocialError("not_found", "code not recognized"));
                return;
            }
            if (Submitted.Count > 0)
            {
                done?.Invoke(new SocialError("destination_not_empty",
                    "this install has already played"));
                return;
            }
            Current = new Profile("imported-user", "IMPORTED-FOX-1", 42);
            ProfileChanged?.Invoke();
            done?.Invoke(null);
        }

        public void DeleteAccount(Action<SocialError> done)
        {
            if (!IsReady)
            {
                done?.Invoke(new SocialError("not_ready", "no profile"));
                return;
            }

            Current = null;
            IsReady = false;
            _pending.Clear();
            _processed.Clear();
            ProfileChanged?.Invoke();
            done?.Invoke(null);
        }
    }
}
