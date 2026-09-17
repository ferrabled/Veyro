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
            fake.BecomeReady(new Profile("fake-user", DefaultHandle, 3, 0));
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
            foreach (var run in queued) Submitted.Add(run);
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

            Submitted.Add(run);
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

        /// Rerolls are unlimited (owner call, 17 Sep): always a fresh handle, never a budget.
        public void RerollHandle(Action<Profile, SocialError> done)
        {
            if (!IsReady || Current == null)
            {
                done?.Invoke(null, new SocialError("not_ready", "no profile"));
                return;
            }

            _rerolls++;
            Current = new Profile(Current.UserId, "NEW-" + _rerolls,
                Current.RerollsLeft, Current.Xp);
            ProfileChanged?.Invoke();
            done?.Invoke(Current, null);
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
            ProfileChanged?.Invoke();
            done?.Invoke(null);
        }
    }
}
