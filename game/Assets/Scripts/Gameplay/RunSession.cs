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
    public sealed class RunSession : MonoBehaviour
    {
        const string AllTimeBestKey = "veyro.best.alltime";
        const string DailyBestKey = "veyro.best.daily";
        const string DailyBestDateKey = "veyro.best.daily.date";

        /// Ignore restart input for a moment after a crash, so the tap that killed the player
        /// does not also skip the result screen.
        const float RestartLockoutSeconds = 0.5f;

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

        /// All-time best across every mode.
        public int AllTimeBest { get; private set; }

        /// Best on the current UTC date's Daily Run. Resets by itself when the date rolls over.
        public int DailyBest { get; private set; }

        /// "2026-08-21" — the UTC date the current run belongs to.
        public string DailyLabel { get; private set; } = string.Empty;

        int _runIndex;
        int _sessionSalt;
        float _elapsed;
        float _restartLockout;

        void Start()
        {
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
            Input.Tick();

            if (!IsRunning)
            {
                _restartLockout -= deltaTime;
                if (_restartLockout <= 0f && (Input.IsJumpPressed() || Input.IsSpecialPressed())) StartRun();
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
            _restartLockout = RestartLockoutSeconds;

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

        void RequestRestart()
        {
            // Same lockout as the tap path: the click that arrives on the frame of the crash
            // is the crash input, not a request for another run.
            if (IsRunning || _restartLockout > 0f) return;
            StartRun();
        }
    }
}
