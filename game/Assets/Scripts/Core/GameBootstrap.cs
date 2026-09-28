using MotionRunner.Audio;
using MotionRunner.Commerce;
using MotionRunner.Commerce.RevenueCat;
using MotionRunner.Gameplay;
using MotionRunner.Social;
using MotionRunner.Social.Supabase;
using MotionRunner.Track;
using UnityEngine;
using MotionRunner.Art;
using MotionRunner.Notifications;
using MotionRunner.Notifications.OneSignal;

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
            var growth = GrowthRuntime.Create();
            var push = OneSignalPushService.Create();
            growth.Attach(push);
            push.Initialize(PlayerPrefs.GetInt(PushPromptPolicy.EnabledKey, 0) == 1);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            new GameObject("Art performance probe").AddComponent<ArtPerformanceProbe>();
#endif

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

            // Audio (T-045): music, one-shots and the levels behind them. Before RunFlow, whose
            // first ShowMenu starts the menu loop. Fail-open like the store - missing clips are
            // warnings, never exceptions - and every call site goes through GameAudio's statics,
            // which no-op without an instance.
            GameAudio.Create(new SoundSettings(new PlayerPrefsSoundStore()));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.16f);
            cam.cullingMask &= ~(1 << MotionRunner.Menu.RunnerPreview.Layer);
            cam.fieldOfView = 65f;
            cam.farClipPlane = 220f;
            camGo.transform.position = new Vector3(0f, 4.2f, -6.2f);
            camGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            ParkTheme.Apply(cam, light);

            // Track (T-003): owns generation, spawning and recycling.
            var director = new GameObject("Track").AddComponent<TrackDirector>();

            // Runner
            var runner = new GameObject("Runner");
            runner.name = "Runner";
            runner.transform.position = new Vector3(0f, TrackMetrics.RunnerRestY, 0f);
            // Collisions are resolved against deterministic AABBs, not PhysX (CLAUDE.md rule 4).
            var runnerCollider = runner.GetComponent<Collider>();
            if (runnerCollider != null) Object.Destroy(runnerCollider);
            var controller = runner.AddComponent<RunnerController>();
            var visual = RunnerVisual.Create(runner.transform);
            controller.Visual = visual;

            var hud = RunHud.Create();

            // Commerce (T-020): configured at boot, fail-open - a store that never answers
            // leaves every entitlement locked and the game fully playable (rule 3's shape).
            // The store UI itself lives on the main menu's shop tab now, so what is built here is
            // only the seam; RunFlow hands both to the menu.
            var store = CreateStore();


            // Profile/leaderboard backend (T-009): same fail-open shape as the store. Once the
            // profile loads, its user id becomes the RevenueCat app user id too (D13), so one
            // stable id spans purchases and boards.
            var profile = CreateProfileService();
            profile.ProfileChanged += () =>
            {
                // Profile loaded -> alias the store to it. Profile DELETED (Current null) ->
                // reset the store identity too, so the deleted UUID stops accruing provider
                // data (18 Sep review R2).
                if (profile.Current != null) store.Identify(profile.Current.UserId);
                else store.ResetIdentity();
            };
            if (profile.Current != null) store.Identify(profile.Current.UserId);
            var season = new SeasonService(store, profile,
                Debug.isDebugBuild ? SeasonCurve.Testing : SeasonCurve.Production);
            var skins = new SkinService(store, season, visual.ApplyLoadout);

            // Session last: it drives everything above in a fixed order. Disabled until the
            // player has picked a control scheme.
            var session = new GameObject("RunSession").AddComponent<RunSession>();
            session.enabled = false;
            session.Runner = controller;
            session.Director = director;
            session.Hud = hud;
            session.Profile = profile;

            // The result card's two seams back into the game. Wired here rather than in RunFlow
            // because this is the one place that holds the skins, the session and the HUD at the
            // same time; both are documented on RunHud.
            //   * Loadout  - the card renders the player's live outfit, so the runner on the
            //                screenshot is the runner they just ran with.
            //   * HoldRun  - SHARE is the only button that stays ON the card; the hold keeps the
            //                camera hop (the only gesture restart left) from firing across the
            //                share sheet's round trip. See the note on RunHud.HoldRun.
            hud.Loadout = () => skins.Effective;
            hud.HoldRun = held => session.Frozen = held;

            // Everything from here is transitions rather than construction: which screen is up,
            // which control scheme the run uses, when the camera is allowed to be on, and whether
            // the track is being driven by a run or by the menu's attract loop.
            RunFlow.Create(session, hud, store, skins, profile, push);

            // Challenge links (T-024). After RunFlow, so a cold-start link is already parked by
            // the time the menu's first Update looks for one - and warm links keep arriving
            // through the event this subscribes to.
            DeepLinks.Begin();
        }

        /// Mirrors CreateStore: the Editor exercises the profile tab against a ready fake, a
        /// build without Supabase coordinates gets a never-ready fake (boards read as sample
        /// data, submissions queue nowhere, game untouched), and a configured build talks to
        /// the real backend. Coordinates ship in SupabaseKeys - public by design, like the
        /// RevenueCat public keys.
        static IProfileService CreateProfileService()
        {
#if VEYRO_COSMETIC_QA && DEVELOPMENT_BUILD
            var qa=new FakeProfileService();
            qa.BecomeReady(new Profile("cosmetic-qa", "QA-RUNNER", 9));
            return qa;
#elif UNITY_EDITOR
            return FakeProfileService.Ready();
#else
            if (!SupabaseKeys.IsConfigured)
            {
                Debug.LogWarning("[Profile] No Supabase coordinates in this build - boards offline, game fully playable.");
                return new FakeProfileService(); // never ready: fail-open
            }
            return SupabaseProfileService.Create(SupabaseKeys.Url, SupabaseKeys.AnonKey);
#endif
        }

        /// The RevenueCat SDK cannot run in the Editor (it NREs - REVENUECAT_PLAN §2.5), so
        /// Play mode gets a ready FakeStore: the store UI is exercisable, purchases grant
        /// in-memory and reset on exit. On device, a missing API key degrades to a locked,
        /// silent store rather than an error - the key arrives per build flavour via
        /// RevenueCatKeys (test key in APKs, Play key in .aab store builds, enforced at build).
        static IStore CreateStore()
        {
#if VEYRO_COSMETIC_QA && DEVELOPMENT_BUILD
            var fixture=FakeStore.WithDefaultCatalog();fixture.IsReady=true;
            foreach(var entitlement in Entitlements.All)fixture.SetEntitlement(entitlement,true);
            return fixture;
#elif UNITY_EDITOR
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
