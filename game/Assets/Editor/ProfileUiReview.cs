using System.Collections.Generic;
using System.IO;
using MotionRunner.Art;
using MotionRunner.Audio;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Gameplay;
using MotionRunner.Menu;
using MotionRunner.Progression;
using MotionRunner.Social;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.EditorTools
{
    /// Renders the profile tab to builds/profile-review/ at a 1080x2400 phone's proportions, the
    /// way GuideUiReview does for the guide: the top of the page (player, podium, runs chart over
    /// a seeded sample history), the bottom (settings and account), the same with no online
    /// profile and no runs - where the account card re-flows and the chart is empty - and the
    /// pause card with its volume rows. Fakes throughout - FakeStore, FakeProfileService, an
    /// in-memory sound store - so nothing touches the network or the editor's sound levels, and
    /// the editor's own run history is put back afterwards.
    ///
    /// Batchmode: -executeMethod MotionRunner.EditorTools.ProfileUiReview.Render
    public static class ProfileUiReview
    {
        const int Width = 1080;
        const int Height = 2400;
        const int ReviewLayer = 29;

        [MenuItem("Veyro/UI/Render profile tab")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            float scale = Mathf.Pow(2f, Mathf.Lerp(
                Mathf.Log(Width / RuntimeUi.ReferenceResolution.x, 2f),
                Mathf.Log(Height / RuntimeUi.ReferenceResolution.y, 2f), 0.5f));
            var canvasSize = new Vector2(Width / scale, Height / scale);

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/profile-review"));
            Directory.CreateDirectory(folder);

            var sound = new SoundSettings(new MemoryStore());
            sound.SetMusicVolume(0.6f);
            GameAudio audio = GameAudio.Instance == null ? GameAudio.Create(sound) : null;

            // A week of runs for the chart, put back afterwards: the editor's own history is
            // whatever the last play session left, and a review render must not keep this.
            bool hadRuns = PlayerPrefs.HasKey(RunsKey);
            string savedRuns = PlayerPrefs.GetString(RunsKey, string.Empty);
            PlayerPrefs.SetString(RunsKey, RunHistory.Encode(SampleRuns));
            try
            {
                var ready = new FakeProfileService();
                ready.BecomeReady(new Profile("ffe5a1f8-8d90-4a63-bdbc-380bb370af4f", "BRAVE-EGRET-74", 0));
                RenderMenu(canvasSize, ready, folder, "profile", true);
                RenderHome(canvasSize, ready, Path.Combine(folder, "home.png"), false);

                // The same home screen on a phone with a centred teardrop and a gesture bar: the
                // safe area is simulated (SafeAreaFitter.Simulated) and the notch drawn in black,
                // so the render shows the title clearing it.
                SafeAreaFitter.Simulated = Rect.MinMaxRect(0f, 0.02f, 1f, 0.955f);
                RenderHome(canvasSize, ready, Path.Combine(folder, "home-cutout.png"), true);
                SafeAreaFitter.Simulated = null;

                PlayerPrefs.DeleteKey(RunsKey); // the offline render shows the empty chart
                RenderMenu(canvasSize, new FakeProfileService(), folder, "profile-offline", true);
                RenderPause(canvasSize, Path.Combine(folder, "pause.png"));
            }
            finally
            {
                SafeAreaFitter.Simulated = null;
                if (hadRuns) PlayerPrefs.SetString(RunsKey, savedRuns);
                else PlayerPrefs.DeleteKey(RunsKey);
                PlayerPrefs.Save();
                if (audio != null) Object.DestroyImmediate(audio.gameObject);
            }
            Debug.Log("[ProfileUiReview] rendered to " + folder + " (canvas " + canvasSize + ")");
        }

        static void RenderMenu(Vector2 canvasSize, IProfileService profile, string folder, string name, bool top)
        {
            var store = FakeStore.WithDefaultCatalog();
            store.IsReady = true;
            MainMenu menu = null;
            Camera cam = null;
            RenderTexture target = null;
            using (var season = new SeasonService(store, profile, SeasonCurve.Testing))
            using (var skins = new SkinService(store, season, _ => { }))
            {
                try
                {
                    menu = MainMenu.Create(store, skins, profile, MenuTab.Profile);
                    menu.GetComponent<CanvasScaler>().enabled = false;
                    menu.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    var rect = menu.GetComponent<RectTransform>();
                    rect.sizeDelta = canvasSize;
                    rect.position = Vector3.zero;
                    rect.localScale = Vector3.one;

                    cam = new GameObject("Profile review camera").AddComponent<Camera>();
                    cam.transform.position = Vector3.back * 10f;
                    cam.orthographic = true;
                    cam.orthographicSize = canvasSize.y * 0.5f;
                    cam.aspect = (float)Width / Height;
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 20f;
                    cam.cullingMask = 1 << ReviewLayer;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = MenuTheme.Scrim;
                    target = new RenderTexture(Width, Height, 24);
                    cam.targetTexture = target;

                    // The profile page's own scroll view - the shop's catalog has one too, built
                    // first and hidden, and a menu-wide lookup finds that one.
                    var scroll = menu.GetComponentInChildren<ProfilePage>(true).GetComponentInChildren<ScrollRect>(true);
                    if (top)
                    {
                        scroll.verticalNormalizedPosition = 1f;
                        Capture(menu, cam, target, Path.Combine(folder, name + "-top.png"));
                    }
                    // The bottom: content moved up by everything that does not fit the viewport.
                    Canvas.ForceUpdateCanvases();
                    float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
                    scroll.content.anchoredPosition = new Vector2(0f, Mathf.Max(0f, overflow));
                    Capture(menu, cam, target, Path.Combine(folder, name + "-bottom.png"));
                }
                finally
                {
                    if (cam != null) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
                    if (target != null) Object.DestroyImmediate(target);
                    if (menu != null) Object.DestroyImmediate(menu.gameObject);
                    if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        /// The RUN tab, with the runner rendered into its stage the way CosmeticUiReview does it
        /// (a preview camera does not render by itself outside Play mode). `notch` draws a black
        /// teardrop where a 6T's camera sits, over the simulated safe area.
        static void RenderHome(Vector2 canvasSize, IProfileService profile, string path, bool notch)
        {
            var store = FakeStore.WithDefaultCatalog();
            store.IsReady = true;
            MainMenu menu = null;
            Camera cam = null;
            RenderTexture target = null;
            Light sun = null;
            using (var season = new SeasonService(store, profile, SeasonCurve.Testing))
            using (var skins = new SkinService(store, season, _ => { }))
            {
                try
                {
                    // The empty review scene has no light; the runner needs one (CosmeticUiReview's).
                    RenderSettings.fog = false;
                    sun = new GameObject("Review sun").AddComponent<Light>();
                    sun.type = LightType.Directional;
                    sun.intensity = 0.8f;
                    sun.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

                    menu = MainMenu.Create(store, skins, profile, MenuTab.Run);
                    menu.GetComponent<CanvasScaler>().enabled = false;
                    menu.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                    var rect = menu.GetComponent<RectTransform>();
                    rect.sizeDelta = canvasSize;
                    rect.position = Vector3.zero;
                    rect.localScale = Vector3.one;

                    if (notch)
                    {
                        RuntimeUi.Element("Simulated notch", menu.transform, out var drop);
                        drop.anchorMin = drop.anchorMax = new Vector2(0.5f, 1f);
                        drop.pivot = new Vector2(0.5f, 1f);
                        drop.sizeDelta = new Vector2(96f, 70f);
                        var shape = drop.gameObject.AddComponent<CosmeticPanel>(); // CosmeticUi is internal
                        shape.color = Color.black;
                        shape.Radius = 35f;
                    }

                    cam = ReviewCamera(canvasSize, out target);
                    foreach (var preview in menu.GetComponentsInChildren<RunnerPreview>(true))
                    {
                        typeof(RunnerPreview).GetMethod(preview.gameObject.activeInHierarchy ? "OnEnable" : "OnDisable",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            ?.Invoke(preview, null);
                    }
                    RunnerCosmetics.SetLayerRecursively(menu.gameObject, ReviewLayer);
                    Canvas.ForceUpdateCanvases();
                    foreach (var preview in menu.GetComponentsInChildren<RunnerPreview>())
                    {
                        preview.Focus(preview.FocusedSlot, true);
                        preview.PreviewCamera.Render();
                    }
                    Capture(menu, cam, target, path);
                }
                finally
                {
                    if (sun != null) Object.DestroyImmediate(sun.gameObject);
                    if (cam != null) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
                    if (target != null) Object.DestroyImmediate(target);
                    if (menu != null) Object.DestroyImmediate(menu.gameObject);
                    if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
                }
            }
        }

        /// ProgressStore's history key. Private there; a review tool naming it is cheaper than
        /// widening the store's surface for a render.
        const string RunsKey = "veyro.runs";

        static readonly RunRecord[] SampleRuns =
        {
            new RunRecord("2026-09-27", true, 785, 19, 135),
            new RunRecord("2026-09-27", false, 183, 7, 63),
            new RunRecord("2026-09-24", true, 1055, 28, 255),
            new RunRecord("2026-09-24", true, 1805, 37, 255),
            new RunRecord("2026-09-24", true, 712, 18, 112),
            new RunRecord("2026-09-23", false, 402, 11, 98)
        };

        /// The pause card with its volume rows, over nothing: the run is not what is under review.
        static void RenderPause(Vector2 canvasSize, string path)
        {
            PauseMenu pause = null;
            Camera cam = null;
            RenderTexture target = null;
            try
            {
                pause = PauseMenu.Show(null);
                pause.GetComponent<CanvasScaler>().enabled = false;
                pause.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rect = pause.GetComponent<RectTransform>();
                rect.sizeDelta = canvasSize;
                rect.position = Vector3.zero;
                rect.localScale = Vector3.one;

                cam = ReviewCamera(canvasSize, out target);
                Capture(pause.transform, cam, target, path);
            }
            finally
            {
                if (cam != null) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
                if (target != null) Object.DestroyImmediate(target);
                if (pause != null) Object.DestroyImmediate(pause.gameObject);
                if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        static Camera ReviewCamera(Vector2 canvasSize, out RenderTexture target)
        {
            var cam = new GameObject("Profile review camera").AddComponent<Camera>();
            cam.transform.position = Vector3.back * 10f;
            cam.orthographic = true;
            cam.orthographicSize = canvasSize.y * 0.5f;
            cam.aspect = (float)Width / Height;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.cullingMask = 1 << ReviewLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ParkInk;
            target = new RenderTexture(Width, Height, 24);
            cam.targetTexture = target;
            return cam;
        }

        static readonly Color ParkInk = ParkTheme.Ink;

        static void Capture(MainMenu menu, Camera cam, RenderTexture target, string path) =>
            Capture(menu.transform, cam, target, path);

        static void Capture(Transform root, Camera cam, RenderTexture target, string path)
        {
            SetLayer(root);
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>())
            {
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
            }
            Canvas.ForceUpdateCanvases();

            cam.Render();
            cam.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            RenderTexture.active = null;
        }

        static void SetLayer(Transform root)
        {
            root.gameObject.layer = ReviewLayer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i));
        }

        sealed class MemoryStore : ISoundSettingsStore
        {
            readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
            public float GetFloat(string key, float fallback) => _floats.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) => _floats[key] = value;
            public int GetInt(string key, int fallback) => _ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetInt(string key, int value) => _ints[key] = value;
            public void Save() { }
        }
    }
}
