using MotionRunner.Commerce;
using MotionRunner.Commerce.RevenueCat;
using MotionRunner.Gameplay;
using MotionRunner.Inputs;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Core
{
    /// Builds the entire scene from code - no authored scene content,
    /// so any empty scene boots the game (CLAUDE.md rule 1).
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;

            // Light
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Camera: behind and above the runner, looking down the track
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.16f);
            cam.fieldOfView = 65f;
            cam.farClipPlane = 220f;
            camGo.transform.position = new Vector3(0f, 4.2f, -6.2f);
            camGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

            // Track (T-003): owns generation, spawning and recycling.
            var director = new GameObject("Track").AddComponent<TrackDirector>();

            // Runner
            var runner = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            runner.name = "Runner";
            runner.transform.position = new Vector3(0f, TrackMetrics.RunnerRestY, 0f);
            runner.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            runner.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Lit(new Color(1f, 0.55f, 0.15f));
            // Collisions are resolved against deterministic AABBs, not PhysX (CLAUDE.md rule 4).
            var runnerCollider = runner.GetComponent<Collider>();
            if (runnerCollider != null) Object.Destroy(runnerCollider);
            var controller = runner.AddComponent<RunnerController>();

            var hud = RunHud.Create();

            // Commerce (T-020): configured at boot, fail-open - a store that never answers
            // leaves every entitlement locked and the game fully playable (rule 3's shape).
            var store = CreateStore();
            var skins = new SkinService(store, runner.GetComponent<Renderer>());
            hud.StoreRequested += () => StorePanel.Show(store, skins);

            // Session last: it drives everything above in a fixed order. Disabled until the
            // player has picked a control scheme — enabling it is what starts the first run.
            var session = new GameObject("RunSession").AddComponent<RunSession>();
            session.enabled = false;
            session.Runner = controller;
            session.Director = director;
            session.Hud = hud;

            var menu = ModeSelectMenu.Create();
            menu.Chosen += (cameraMode, rig) =>
            {
                // Touch and keyboard stay in the mix in camera mode: they cost nothing, keep the
                // result screen restartable if tracking drops, and are how a bystander pauses the
                // hands-free demo without standing in frame.
                session.Input = cameraMode
                    ? new CompositeInput(
                        new CameraFaceInput(rig),
                        new TouchTapInput(),
                        new KeyboardInput())
                    : new CompositeInput(
                        new GyroTiltInput(),
                        new TouchTapInput(),
                        new KeyboardInput());
                session.enabled = true;
            };
        }

        /// The RevenueCat SDK cannot run in the Editor (it NREs - REVENUECAT_PLAN §2.5), so
        /// Play mode gets a ready FakeStore: the store UI is exercisable, purchases grant
        /// in-memory and reset on exit. On device, a missing API key degrades to a locked,
        /// silent store rather than an error - the key arrives per build flavour via
        /// RevenueCatKeys (test key in APKs, Play key in .aab store builds, enforced at build).
        static IStore CreateStore()
        {
#if UNITY_EDITOR
            var fake = FakeStore.WithDefaultCatalog();
            fake.IsReady = true;
            return fake;
#else
            if (string.IsNullOrEmpty(RevenueCatKeys.ActiveKey))
            {
                Debug.LogWarning("[Store] No RevenueCat API key in this build flavour - store disabled, game fully playable.");
                return FakeStore.WithDefaultCatalog(); // never ready: everything locked, nothing throws
            }
            return RevenueCatStore.Create(RevenueCatKeys.ActiveKey);
#endif
        }
    }
}
