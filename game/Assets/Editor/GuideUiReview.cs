using System.IO;
using MotionRunner.Core;
using MotionRunner.Gameplay;
using MotionRunner.Menu;
using MotionRunner.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.EditorTools
{
    /// Renders every how-to-play page to builds/guide-review/guide-page-N.png at a 1080x2400
    /// phone's proportions, without a device, the way CosmeticUiReview does for the shop. Meant
    /// for two moments: a copy or layout change (does the body still clear the button?) and the
    /// day the illustrations land (do the frames sit in the slot?).
    ///
    /// Batchmode: -executeMethod MotionRunner.EditorTools.GuideUiReview.Render
    public static class GuideUiReview
    {
        const int Width = 1080;
        const int Height = 2400;
        const int ReviewLayer = 29;

        [MenuItem("Veyro/UI/Render how-to-play pages")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // What CanvasScaler would make of a 1080x2400 screen against the 1080x1920 reference
            // with matchWidthOrHeight 0.5: the canvas the phone actually lays out on, so a card
            // that clears the screen here clears it there.
            float scale = Mathf.Pow(2f, Mathf.Lerp(
                Mathf.Log(Width / RuntimeUi.ReferenceResolution.x, 2f),
                Mathf.Log(Height / RuntimeUi.ReferenceResolution.y, 2f), 0.5f));
            var canvasSize = new Vector2(Width / scale, Height / scale);

            FirstRunGuide guide = null;
            Camera cam = null;
            RenderTexture target = null;
            try
            {
                guide = FirstRunGuide.Show();
                guide.GetComponent<CanvasScaler>().enabled = false;
                guide.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rect = guide.GetComponent<RectTransform>();
                rect.sizeDelta = canvasSize;
                rect.position = Vector3.zero;
                rect.localScale = Vector3.one;

                cam = new GameObject("Guide review camera").AddComponent<Camera>();
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

                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/guide-review"));
                Directory.CreateDirectory(folder);

                var primary = guide.transform.Find("Card/Primary").GetComponent<Button>();
                for (int page = 1; page <= GuideState.Pages.Length; page++)
                {
                    Capture(guide, cam, target, Path.Combine(folder, "guide-page-" + page + ".png"));
                    var content = GuideState.Pages[page - 1];
                    if (content.HasIllustration)
                    {
                        var frame = guide.transform.Find("Card/Illustration/Frame").GetComponent<Image>();
                        if (!frame.gameObject.activeInHierarchy || frame.sprite == null)
                            throw new System.InvalidOperationException(content.Title + " is showing the fallback instead of art.");
                        for (int index = 1; index <= content.FrameCount; index++)
                        {
                            string resource = GuideIllustration.FramePath(content.Illustration, index);
                            var sprite = Resources.Load<Sprite>(resource);
                            if (sprite == null)
                                throw new System.InvalidOperationException("Missing guide sprite: " + resource);
                            frame.sprite = sprite;
                            Capture(guide, cam, target, Path.Combine(folder,
                                "guide-page-" + page + "-" + GuideIllustration.FrameName(content.Illustration, index) + ".png"));
                            Debug.Log("[GuideUiReview] Loaded " + resource + " " + sprite.rect.width + "x" + sprite.rect.height);
                        }
                    }
                    // The last page's button is the way out, which writes the seen flag to the
                    // editor's PlayerPrefs - not something a review render should do.
                    if (page < GuideState.Pages.Length) primary.onClick.Invoke();
                }
                Debug.Log("[GuideUiReview] " + GuideState.Pages.Length + " pages rendered to " + folder);
            }
            finally
            {
                if (cam != null) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
                if (target != null) Object.DestroyImmediate(target);
                if (guide != null) Object.DestroyImmediate(guide.gameObject);
                if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        static void Capture(FirstRunGuide guide, Camera cam, RenderTexture target, string path)
        {
            SetLayer(guide.transform);
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in guide.GetComponentsInChildren<Graphic>())
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
    }
}
