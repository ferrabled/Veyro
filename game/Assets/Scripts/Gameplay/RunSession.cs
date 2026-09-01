using System;
using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// The run loop (T-005): start -> run -> crash -> result -> restart, plus the Daily Run
    /// (T-008): in RunMode.Daily the seed comes from the UTC date, so every device playing today
    /// runs the identical track with no server involved (D10).
    ///
    /// Owns frame order on purpose - input, then the runner, then the world, then collisions,
    /// then the HUD - so what the player sees is what was tested against.
    ///
    /// Runs late (DefaultExecutionOrder) so the EventSystem has already dispatched this frame's
    /// UI clicks: the tap that hits PAUSE has frozen the run before the run reads that same tap
    /// as a jump. Both used to land in the same frame in whichever order Unity felt like.
    [DefaultExecutionOrder(100)]
    public sealed class RunSession : MonoBehaviour
    {
        const string AllTimeBestKey = "veyro.best.alltime";
        const string DailyBestKey = "veyro.best.daily";
        const string DailyBestDateKey = "veyro.best.daily.date";

        /// Only chunks overlapping this z window around the runner are tested.
        const float NearZMin = -4f;
        const float NearZMax = 10f;

        public IGameInput Input;
        public RunnerController Runner;
        public TrackDirector Director;
        public RunHud Hud;

        public RunMode Mode = RunMode.Daily;

        /// Which content set the seed refers to (handoff 3.3).
        public string WorldId = RunSeed.DefaultWorldId;

        public ScoreState Score { get; } = new ScoreState();
        public bool IsRunning { get; private set; }
        public RunSeed CurrentSeed { get; private set; }

        /// Set by RunFlow while the pause menu is up. The world is already stopped by
        /// Time.timeScale = 0; this stops the loop as well, because a frame stepped with
        /// deltaTime 0 would still let a queued tap fire a jump. Input keeps being polled either
        /// way, so nothing a player does at the pause menu survives into the resumed run.
        public bool Frozen { get; set; }

        /// All-time best across every mode.
        public int AllTimeBest { get; private set; }

        /// Best on the current UTC date's Daily Run. Resets by itself when the date rolls over.
        public int DailyBest { get; private set; }

        /// "2026-08-21" — the UTC date the current run belongs to.
        public string DailyLabel { get; private set; } = string.Empty;

        /// When another run may start, and on which frame. Every way of asking for one - the tap
        /// anywhere, the RUN AGAIN button - goes through it, so "a restart never begins on the
        /// frame its own tap arrived" is one rule in one engine-free place rather than a habit
        /// each call site has to remember. See RestartGate.
        readonly RestartGate _restart = new RestartGate();

        int _runIndex;
        int _sessionSalt;
        float _elapsed;
        bool _started;

        void Start()
        {
            _started = true;
            AllTimeBest = PlayerPrefs.GetInt(AllTimeBestKey, 0);

            // Free mode needs a run-to-run seed source. The seed source may be arbitrary; the
            // generation it drives may not be (CLAUDE.md rule 4), which is why the tick count is
            // sampled exactly once, here, and never inside the generator.
            _sessionSalt = Environment.TickCount;

            if (Hud != null) Hud.RestartRequested += RequestRestart;
            StartRun();
        }

        void OnDestroy()
        {
            if (Hud != null) Hud.RestartRequested -= RequestRestart;
        }

        /// Starts running with a control scheme. Unity calls Start() the first time the component
        /// is enabled and never again, so the second visit from the mode picker has to kick the
        /// run itself.
        public void Begin(IGameInput input)
        {
            Input = input;
            Frozen = false;
            enabled = true;
            if (_started) StartRun();
        }

        /// Leaves the run without finishing it: the loop stops, the world empties and the runner
        /// goes back to its mark. Deliberately scores nothing - Crash() is the only thing that
        /// writes a best, so quitting or restarting mid-run cannot touch the daily or all-time
        /// bests on disk.
        public void Stop()
        {
            IsRunning = false;
            Frozen = false;
            enabled = false;
            Input = null;
            _restart.Clear();
            _elapsed = 0f;
            Score.Reset();

            if (Runner != null) Runner.ResetState();
            if (Director != null) Director.EndRun();
            if (Hud != null) Hud.Clear();
        }

        public void StartRun()
        {
            _runIndex++;
            _elapsed = 0f;
            Score.Reset();

            // Sampled per run, not once at startup: a session left open across UTC midnight rolls
            // onto the new day's track and the new day's best-score bucket.
            var utcNow = DateTime.UtcNow;
            DailyLabel = DailySeed.LabelForDate(utcNow);
            DailyBest = LoadDailyBest(DailyLabel);

            CurrentSeed = Mode == RunMode.Daily
                ? DailySeed.ForUtcDate(utcNow, WorldId)
                : new RunSeed(_sessionSalt + _runIndex * 7919, ChunkLibrary.ContentVersion, WorldId);

            Runner.ResetState();
            Director.BeginRun(CurrentSeed);

            if (Hud != null)
            {
                Hud.HideResult();
                Hud.SetMode(Mode, DailyLabel);
                Hud.SetLive(Score, Director.Difficulty);
            }

            IsRunning = true;
        }

        void Update()
        {
            if (Input == null) return;

            float deltaTime = Time.deltaTime;

            // Ticked even while frozen, on purpose: a tap that lands on the pause menu is
            // consumed here and cannot survive into the frame that resumes the run.
            Input.Tick();
            if (Frozen) return;

            if (!IsRunning)
            {
                // A restart never begins on the frame it was asked for, whether the ask was a tap
                // anywhere or the RUN AGAIN button, and never at all while the store is up: the
                // release that asks is the same TouchPhase.Ended that TouchTapInput reads as a jump
                // and that the EventSystem turns into a click, so this frame's input has to be
                // spent first. The gate holds that rule; this call site's job is that the run is
                // started HERE - after Input.Tick above, and returning before Runner.Step - so the
                // ask can never also be the new run's first jump.
                if (_restart.Tick(deltaTime, Input.IsJumpPressed() || Input.IsSpecialPressed(),
                        StorePanel.IsOpen))
                    StartRun();
                return;
            }

            _elapsed += deltaTime;
            Runner.Step(deltaTime, Input);

            float travelled = Director.Advance(deltaTime, _elapsed);
            Score.AddDistance(travelled);
            Score.Tick(deltaTime);

            if (ResolveCollisions())
            {
                Crash();
                return;
            }

            if (Hud != null) Hud.SetLive(Score, Director.Difficulty);
        }

        /// Returns true when the runner hit something. Coins are collected on the way through.
        bool ResolveCollisions()
        {
            var runner = Runner.Bounds;
            var chunks = Director.ActiveChunks;

            for (int c = 0; c < chunks.Count; c++)
            {
                var chunk = chunks[c];
                if (chunk.EndZ < NearZMin || chunk.StartZ > NearZMax) continue;

                for (int i = 0; i < chunk.CoinCount; i++)
                {
                    if (!chunk.IsCoinAvailable(i)) continue;
                    if (!runner.Intersects(chunk.CoinBounds(i))) continue;
                    chunk.TakeCoin(i);
                    Score.CollectCoin();
                }

                for (int i = 0; i < chunk.ObstacleCount; i++)
                    if (runner.Intersects(chunk.ObstacleBounds(i))) return true;
            }

            return false;
        }

        void Crash()
        {
            IsRunning = false;
            _restart.LockOut();

            int score = Score.Score;
            bool dirty = false;

            if (score > AllTimeBest)
            {
                AllTimeBest = score;
                PlayerPrefs.SetInt(AllTimeBestKey, AllTimeBest);
                dirty = true;
            }

            if (Mode == RunMode.Daily && score > DailyBest)
            {
                DailyBest = score;
                PlayerPrefs.SetInt(DailyBestKey, DailyBest);
                PlayerPrefs.SetString(DailyBestDateKey, DailyLabel);
                dirty = true;
            }

            if (dirty) PlayerPrefs.Save();

            Debug.Log("Run over. mode=" + Mode + " seed=" + CurrentSeed + " score=" + score +
                      " coins=" + Score.Coins + " distance=" + (int)Score.Distance +
                      "m chunks=" + Director.ChunksSpawned);

            if (Hud != null)
                Hud.ShowResult(new RunSummary(Mode, score, Score.Coins, Score.BestCombo,
                    (int)Score.Distance, AllTimeBest, DailyBest, DailyLabel));
        }

        /// Today's daily best, or zero if what is stored belongs to an earlier date. Keeps exactly
        /// one daily bucket on disk instead of one key per day, for ever.
        static int LoadDailyBest(string todayLabel)
        {
            if (PlayerPrefs.GetString(DailyBestDateKey, string.Empty) != todayLabel) return 0;
            return PlayerPrefs.GetInt(DailyBestKey, 0);
        }

        /// The RUN AGAIN button. QUEUED, not started: this runs inside the EventSystem's dispatch,
        /// and DefaultExecutionOrder(100) puts this component's own Update strictly AFTER that in
        /// the same frame - so starting the run here handed the click's own TouchPhase.Ended to a
        /// freshly reset runner as a first-frame jump (PR #5 review). The tap-anywhere path has
        /// always deferred, which is why only the button showed it. The gate's lockout covers the
        /// click that arrives on the frame of the crash, exactly as it does for a tap.
        void RequestRestart()
        {
            if (IsRunning) return;
            _restart.RequestFromButton();
        }
    }
}
