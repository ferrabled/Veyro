using System.Collections.Generic;
using System.IO;
using MotionRunner.Audio;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Menu;
using MotionRunner.Social;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.EditorTools
{
    /// Renders the profile tab to builds/profile-review/ at a 1080x2400 phone's proportions, the
    /// way GuideUiReview does for the guide: the top of the page, the bottom (settings and
    /// account), and the same bottom with no online profile, which is where the account card
    /// re-flows. Fakes throughout - FakeStore, FakeProfileService, an in-memory sound store - so
    /// nothing touches the network or the editor's PlayerPrefs sound levels.
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
            try
            {
                var ready = new FakeProfileService();
                ready.BecomeReady(new Profile("ffe5a1f8-8d90-4a63-bdbc-380bb370af4f", "BRAVE-EGRET-74", 0));
                RenderMenu(canvasSize, ready, folder, "profile", true);
                RenderMenu(canvasSize, new FakeProfileService(), folder, "profile-offline", false);
            }
            finally
            {
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

        static void Capture(MainMenu menu, Camera cam, RenderTexture target, string path)
        {
            SetLayer(menu.transform);
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in menu.GetComponentsInChildren<Graphic>())
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
