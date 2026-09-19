using System;
using MotionRunner.Core;
using MotionRunner.Menu;
using MotionRunner.Progression;
using MotionRunner.Social;
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
        /// Only chunks overlapping this z window around the runner are tested.
        const float NearZMin = -4f;
        const float NearZMax = 10f;

        public IGameInput Input;
        public RunnerController Runner;
        public TrackDirector Director;
        public RunHud Hud;

        /// The online profile seam (T-009). Optional: null or a not-ready service means runs
        /// stay local, exactly like a store with no key (rule 3's fail-open shape).
        public IProfileService Profile;

        public RunMode Mode = RunMode.Daily;

        /// Who steers this run - camera or tilt. Set at Begin (RunFlow knows what the player
        /// picked) and deliberately NOT changed by DropCameraMode: a camera run that finishes on
        /// the tilt fallback still scores as the camera run it was chosen to be. The boards
        /// split on this (Feature D, owner call: camera and tilt are separate games), while the
        /// seed does not - the same daily track drives both schemes.
        public ControlScheme Scheme { get; private set; } = ControlScheme.Tilt;

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

        /// All-time best on the CURRENT SCHEME's board. Loaded per run rather than once at
        /// startup, because the player can quit to the picker and come back on the other scheme
        /// within one session.
        public int AllTimeBest { get; private set; }

        /// Best on the current UTC date's Daily Run, on the current scheme's board. Resets by
        /// itself when the date rolls over.
        public int DailyBest { get; private set; }

        /// "2026-08-21" — the UTC date the current run belongs to.
        public string DailyLabel { get; private set; } = string.Empty;

        /// When another run may start, and on which frame. Every way of asking for one - the tap
        /// anywhere, the RUN AGAIN button - goes through it, so "a restart never begins on the
        /// frame its own tap arrived" is one rule in one engine-free place rather than a habit
        /// each call site has to remember. See RestartGate.
        readonly RestartGate _restart = new RestartGate();

        /// The per-scheme best boards (engine-free; the PlayerPrefs adapter is the only Unity in
        /// the path). Owns the key shapes and the one-time legacy migration.
        readonly BestBoard _board = new BestBoard(new PlayerPrefsScoreStore());

        int _runIndex;
        int _sessionSalt;
        float _elapsed;
        bool _started;

        /// True once THIS run's camera gave up and the run fell back to tilt+touch. Scheme
        /// stays Camera for the local boards (Feature D: "still scores as the camera run it
        /// was chosen to be"); the SHARED board instead demotes such a run to the standard
        /// group (owner call, 3 Sep) - so the submission needs the fact the local boards
        /// deliberately ignore. Set by RunFlow, reset every StartRun.
        bool _cameraDropped;

        /// The UTC day the current run belongs to, sampled with DailyLabel in StartRun. The streak
        /// card counts days, not labels, so the conversion happens once per run rather than being
        /// re-parsed out of the label at the moment of a crash.
        int _dayNumber;

        void Start()
        {
            _started = true;

            // Legacy single-board scores become tilt's history, exactly once (idempotent, and
            // the legacy keys stay on disk untouched). Before any board is read.
            _board.Migrate();

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
        public void Begin(IGameInput input, ControlScheme scheme)
        {
            Input = input;
            Scheme = scheme;
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

        /// RunFlow's DropCameraMode notifies the session so the run's submission can say
        /// "camera_fallback" (see _cameraDropped).
        public void NoteCameraDropped() => _cameraDropped = true;

        public void StartRun()
        {
            _runIndex++;
            _elapsed = 0f;
            _cameraDropped = false;
            Score.Reset();

            // Sampled per run, not once at startup: a session left open across UTC midnight rolls
            // onto the new day's track and the new day's best-score bucket. The bests come off
            // the current scheme's board for the same per-run reason - the scheme can change
            // between two runs of one session.
            var utcNow = DateTime.UtcNow;
            DailyLabel = DailySeed.LabelForDate(utcNow);
            _dayNumber = ProgressStore.DayNumberFor(utcNow);
            DailyBest = _board.DailyBest(Scheme, DailyLabel);
            AllTimeBest = _board.AllTimeBest(Scheme);

            CurrentSeed = Mode == RunMode.Daily
                ? DailySeed.ForUtcDate(utcNow, WorldId)
                : new RunSeed(_sessionSalt + _runIndex * 7919, ChunkLibrary.ContentVersion, WorldId);

            Runner.ResetState();
            Director.BeginRun(CurrentSeed);

            if (Hud != null)
            {
                Hud.HideResult();
                Hud.SetMode(Mode, DailyLabel, Scheme);
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
                // anywhere or the RUN AGAIN button, and never at all while the main menu is up:
                // the release that asks is the same TouchPhase.Ended that TouchTapInput reads as a
                // jump and that the EventSystem turns into a click, so this frame's input has to be
                // spent first. The gate holds that rule; this call site's job is that the run is
                // started HERE - after Input.Tick above, and returning before Runner.Step - so the
                // ask can never also be the new run's first jump.
                //
                // The overlay used to be StorePanel, which opened ON TOP of the result screen (the
                // 27 Aug store-tap bug). The store is a menu tab now, so the screen that owns the
                // taps is the menu itself - and QuitToMenu disables this component in the same
                // frame the button fires, which makes this the belt to that braces.
                if (_restart.Tick(deltaTime, Input.IsJumpPressed() || Input.IsSpecialPressed(),
                        MainMenu.IsOpen))
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

            // The board owns which keys a scheme may touch (unit-tested there); this method only
            // decides WHEN a run scores, which is unchanged: crashes score, quits do not.
            int score = Score.Score;

            if (_board.RecordAllTime(Scheme, score)) AllTimeBest = score;
            if (Mode == RunMode.Daily && _board.RecordDaily(Scheme, DailyLabel, score))
                DailyBest = score;

            // A FINISHED run is what earns the day's stamp and a row in the profile's history -
            // the same bar Crash already sets for a best score. Quitting or restarting mid-run
            // goes through Stop(), which writes nothing, so a streak cannot be farmed by starting
            // runs and leaving them.
            //
            // Only the Daily Run stamps a day: the card is the daily loop made visible, and a free
            // run is not a day's run. It still lands in the history list, which is a log of what
            // was played rather than a record of what counted.
            if (Mode == RunMode.Daily) ProgressStore.StampDay(_dayNumber);

            ProgressStore.AppendRun(new RunRecord(DailyLabel, Mode == RunMode.Daily,
                score, Score.Coins, (int)Score.Distance));

            // Unconditional, where the pre-menu version flushed only on a new best: every finished
            // run now writes a history row whether or not it beat anything, so there is always
            // something to flush. One Save for the boards, the streak and the row together - the
            // batching BestBoard's Save-free contract asks for.
            ProgressStore.Flush();

            SubmitToBoards(score);

            Debug.Log("Run over. mode=" + Mode + " scheme=" + Scheme + " seed=" + CurrentSeed +
                      " score=" + score + " coins=" + Score.Coins +
                      " distance=" + (int)Score.Distance + "m chunks=" + Director.ChunksSpawned);

            if (Hud != null)
                Hud.ShowResult(new RunSummary(Mode, Scheme, score, Score.Coins, Score.BestCombo,
                    (int)Score.Distance, AllTimeBest, DailyBest, DailyLabel));
        }

        /// Hands the finished run to the shared boards (T-009). Fire-and-forget from Crash:
        /// every finished run participates (joining is automatic - owner call, 17 Sep); the
        /// service queues offline and never throws, so a dead backend costs this call site
        /// nothing. Only Crash submits, for the same reason only Crash scores: a quit or
        /// restart is not a result.
        void SubmitToBoards(int score)
        {
            if (Profile == null) return;

            Profile.SubmitRun(new RunSubmission
            {
                client_run_id = Guid.NewGuid().ToString(),
                mode = Mode == RunMode.Daily ? RunSubmission.ModeDaily : RunSubmission.ModeFree,
                seed = CurrentSeed.Seed,
                content_version = CurrentSeed.ContentVersion,
                world_id = CurrentSeed.WorldId,
                day_label = DailyLabel,
                input_mode = InputModes.For(Scheme == ControlScheme.Camera, _cameraDropped),
                score = score,
                distance_m = Score.Distance,
                coins = Score.Coins,
                best_combo = Score.BestCombo,
                duration_s = _elapsed,
                app_version = Application.version,
                // The Galaxy build flavour flips this constant when T-033 ships one.
#if UNITY_IOS
                platform = SubmissionPlatform.Ios
#else
                platform = SubmissionPlatform.AndroidGooglePlay
#endif
            }, null);
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
