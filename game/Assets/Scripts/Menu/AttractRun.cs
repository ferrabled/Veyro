using MotionRunner.Gameplay;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Menu
{
    /// The game playing itself behind the main menu. The home tab leaves a window in the middle of
    /// the screen and this is what shows through it: the real track, the real runner, the real
    /// lane slides - steered by AutoPilot, scoring nothing.
    ///
    /// It borrows the objects GameBootstrap already built rather than making its own. There is one
    /// TrackDirector and one RunnerController in the game, and a second set for the menu would be
    /// a second set to keep in sync with every change to either. RunSession is disabled the whole
    /// time the menu is up, so nothing else is touching them.
    ///
    /// What it deliberately does NOT do:
    ///   * score, or write anything to disk - an attract run is not a run;
    ///   * resolve OBSTACLE collisions - the pilot dodges, and when it misjudges, the runner passes
    ///     through rather than the menu suddenly showing a crash screen. Coins are the exception
    ///     and CollectCoins says why;
    ///   * touch RunSession, PauseState or the HUD. Starting a real run overwrites everything this
    ///     changed (Director.BeginRun, Runner.ResetState), which is what makes handing over free.
    public sealed class AttractRun : MonoBehaviour
    {
        /// Held at a fixed, gentle difficulty instead of ramping: the menu is not a run, it is a
        /// loop that may be on screen for a minute, and a track that quietly got twice as fast
        /// while somebody read a price list is a strange thing to hand a player when they tap RUN.
        /// Difficulty 2 is early-run pace (DifficultyCurve.At: 45 s per step).
        const float FixedElapsedSeconds = 50f;

        /// Only chunks overlapping this z window around the runner hold a coin it could be
        /// touching. Same numbers as RunSession's collision pass, for the same reason.
        const float NearZMin = -4f;
        const float NearZMax = 10f;

        /// The camera pulls back and tilts down while the menu is up, so the runner sits in the
        /// window the cards leave rather than behind the mode buttons. Restored exactly on Stop,
        /// so the run always begins from the framing it was tuned with.
        ///
        /// Pulled further back than the first device build's (0, 5, -7.2): at 8.5 m the road is
        /// wider than the view, and a runner in a side lane sat two thirds of the way to the
        /// screen edge. At 9.6 m the whole three-lane road fits with the runner at ~53% down the
        /// screen - inside the window, and low enough that the track ahead of it is what fills the
        /// frame. Vertical FOV is 65 (GameBootstrap); on a portrait phone the horizontal half-angle
        /// is only ~16 degrees, which is why distance is the lever here and not pitch.
        static readonly Vector3 MenuCameraPosition = new Vector3(0f, 5.2f, -8.4f);
        static readonly Vector3 MenuCameraEuler = new Vector3(27f, 0f, 0f);

        /// A fixed seed, so the menu is the same track every time the app opens. Not the daily
        /// seed: the demo must not spoil the run the player is about to make, and a showcase
        /// stretch should be one somebody has looked at. WorldId is the shipping one so the chunks
        /// are the chunks.
        static readonly RunSeed ShowcaseSeed =
            new RunSeed(20260902, ChunkLibrary.ContentVersion, RunSeed.DefaultWorldId);

        TrackDirector _director;
        RunnerController _runner;
        Camera _camera;
        AttractInput _input;

        Vector3 _cameraPosition;
        Quaternion _cameraRotation;
        bool _cameraSaved;
        bool _running;

        public static AttractRun Begin(TrackDirector director, RunnerController runner, Camera camera)
        {
            var go = new GameObject("AttractRun");
            var attract = go.AddComponent<AttractRun>();
            attract._director = director;
            attract._runner = runner;
            attract._camera = camera;
            attract._input = new AttractInput(new ChunkClusters(director));
            attract.StartLoop();
            return attract;
        }

        void StartLoop()
        {
            if (_director == null || _runner == null) return;

            if (_camera != null)
            {
                _cameraPosition = _camera.transform.position;
                _cameraRotation = _camera.transform.rotation;
                _cameraSaved = true;
                _camera.transform.position = MenuCameraPosition;
                _camera.transform.rotation = Quaternion.Euler(MenuCameraEuler);
            }

            _runner.ResetState();
            _director.BeginRun(ShowcaseSeed);
            _running = true;
        }

        void Update()
        {
            if (!_running) return;

            // unscaledDeltaTime is wrong here and deltaTime is right: the menu runs at timeScale 1
            // (RunFlow.ApplyPhase), and if anything ever freezes time while the menu is up, the
            // demo should freeze with it rather than keep running behind a stopped game.
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            _input.Decide(_runner.Lane);
            _runner.Step(deltaTime, _input);
            _director.Advance(deltaTime, FixedElapsedSeconds);
            CollectCoins();
        }

        /// The one half of collision the demo DOES resolve. Obstacles are skipped on purpose - a
        /// menu that suddenly showed a crash screen would be worse than a pilot that clips a block
        /// once in a while - but a runner sweeping straight through a coin and leaving it hanging
        /// there does not read as "the game", it reads as a bug. Coins come back with the chunk
        /// when TrackDirector recycles it (ChunkView.PrepareForReuse), so nothing accumulates.
        ///
        /// Scores nothing: there is no ScoreState here and there is nothing to spend it on. This
        /// is the same near-z window RunSession tests, for the same reason - a chunk 100 m out has
        /// no coin the runner can be touching.
        void CollectCoins()
        {
            var runner = _runner.Bounds;
            var chunks = _director.ActiveChunks;

            for (int c = 0; c < chunks.Count; c++)
            {
                var chunk = chunks[c];
                if (chunk.EndZ < NearZMin || chunk.StartZ > NearZMax) continue;

                for (int i = 0; i < chunk.CoinCount; i++)
                {
                    if (!chunk.IsCoinAvailable(i)) continue;
                    if (!runner.Intersects(chunk.CoinBounds(i))) continue;
                    chunk.TakeCoin(i);
                }
            }
        }

        /// Hands the world back: the road empties, the runner returns to its mark and the camera
        /// goes back to the gameplay framing. Called before a real run starts and whenever the menu
        /// goes away, so the two can never both be driving the track.
        public void Stop()
        {
            _running = false;

            // Guarded on having actually saved it: a demo that never started (no director, no
            // runner) must not "restore" the camera to a default it never read.
            if (_cameraSaved && _camera != null)
            {
                _camera.transform.position = _cameraPosition;
                _camera.transform.rotation = _cameraRotation;
                _cameraSaved = false;
            }

            if (_director != null) _director.EndRun();
            if (_runner != null) _runner.ResetState();

            Destroy(gameObject);
        }

        void OnDestroy() => _running = false;
    }
}
