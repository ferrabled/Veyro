using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// The run loop (T-005): start -> run -> crash -> result -> restart.
    ///
    /// Owns frame order on purpose - input, then the runner, then the world, then collisions,
    /// then the HUD - so what the player sees is what was tested against.
    public sealed class RunSession : MonoBehaviour
    {
        const string BestScoreKey = "motionrunner.best_score";

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

        /// Which content set the seed refers to (handoff 3.3). T-008 replaces the per-session
        /// seed with one derived from the UTC date; the world id stays part of the tuple.
        public string WorldId = RunSeed.DefaultWorldId;

        public ScoreState Score { get; } = new ScoreState();
        public bool IsRunning { get; private set; }
        public int BestScore { get; private set; }
        public RunSeed CurrentSeed { get; private set; }

        int _runIndex;
        int _sessionSalt;
        float _elapsed;
        float _restartLockout;

        void Start()
        {
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

            // A run is a seed. The seed source may be arbitrary; the generation it drives may
            // not be (CLAUDE.md rule 4), which is why the tick count is sampled exactly once,
            // here, and never inside the generator.
            _sessionSalt = System.Environment.TickCount;

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
            CurrentSeed = new RunSeed(_sessionSalt + _runIndex * 7919, Application.version, WorldId);

            Runner.ResetState();
            Director.BeginRun(CurrentSeed);

            if (Hud != null)
            {
                Hud.HideResult();
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

            if (Score.Score > BestScore)
            {
                BestScore = Score.Score;
                PlayerPrefs.SetInt(BestScoreKey, BestScore);
                PlayerPrefs.Save();
            }

            Debug.Log("Run over. seed=" + CurrentSeed + " score=" + Score.Score +
                      " coins=" + Score.Coins + " distance=" + (int)Score.Distance +
                      "m chunks=" + Director.ChunksSpawned);

            if (Hud != null) Hud.ShowResult(Score.Score, BestScore, Score.Coins, Score.BestCombo);
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
