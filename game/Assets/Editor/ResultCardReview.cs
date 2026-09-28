using System.IO;
using System.Reflection;
using MotionRunner.Core;
using MotionRunner.Gameplay;
using MotionRunner.Inputs;
using MotionRunner.Menu;
using MotionRunner.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.EditorTools
{
    /// Renders the result card to builds/result-review/result-{camera,tilt}.png at a 1080x2340
    /// phone's proportions (the OnePlus 6T), the way GuideUiReview does for the guide. Exists
    /// for the one state a desk cannot produce on the device: the CAMERA card, which keeps the
    /// YOU · CAMERA panel up (FaceOverlay.ResultPosition, above the card) and adds the HOP hint
    /// under it - both only reachable on a phone with a person standing in front of it.
    ///
    /// Batchmode: -executeMethod MotionRunner.EditorTools.ResultCardReview.Render
    public static class ResultCardReview
    {
        const int Width = 1080;
        const int Height = 2340;
        const int ReviewLayer = 29;

        [MenuItem("Veyro/UI/Render result card")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            float scale = Mathf.Pow(2f, Mathf.Lerp(
                Mathf.Log(Width / RuntimeUi.ReferenceResolution.x, 2f),
                Mathf.Log(Height / RuntimeUi.ReferenceResolution.y, 2f), 0.5f));
            var canvasSize = new Vector2(Width / scale, Height / scale);

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/result-review"));
            Directory.CreateDirectory(folder);

            RenderOne(canvasSize, ControlScheme.Camera, true, Path.Combine(folder, "result-camera.png"));
            RenderOne(canvasSize, ControlScheme.Camera, false, Path.Combine(folder, "result-camera-newbest.png"));
            RenderOne(canvasSize, ControlScheme.Tilt, false, Path.Combine(folder, "result-tilt.png"));
            Debug.Log("[ResultCardReview] rendered to " + folder + " (canvas " + canvasSize + ")");
        }

        static void RenderOne(Vector2 canvasSize, ControlScheme scheme, bool ordinary, string path)
        {
            RunHud hud = null;
            FaceOverlay overlay = null;
            Camera cam = null;
            RenderTexture target = null;
            try
            {
                hud = RunHud.Create();
                WorldSpace(hud.gameObject, canvasSize, 0f);

                bool camera = scheme == ControlScheme.Camera;
                hud.HopRestartAvailable = camera;
                var summary = ordinary
                    ? new RunSummary(RunMode.Daily, scheme, 3180, 41, 9, 612, 4210, 3950, "2026-09-25")
                    : new RunSummary(RunMode.Daily, scheme, 4210, 58, 12, 840, 4210, 4210, "2026-09-25",
                        true, true, CrashKind.Trip, default);
                hud.ShowResult(summary);

                // The reveal, the way SeasonIntegrationTests forces it: past the crash pose and
                // the fade, so the card is fully up and the hint (camera only) is showing.
                var type = typeof(RunHud);
                type.GetField("_resultRevealAt", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hud, Time.unscaledTime - 10f);
                var update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                update.Invoke(hud, null);

                if (camera)
                {
                    // A rig-less adapter reads as a lost face, which is exactly the picture a
                    // player standing out of frame gets: red glyph, "no face".
                    overlay = FaceOverlay.Attach(null, new CameraFaceInput(null));
                    WorldSpace(overlay.gameObject, canvasSize, -1f); // in front of the card
                    overlay.Place(onResultCard: true);
                    typeof(FaceOverlay).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(overlay, null);
                }

                cam = new GameObject("Result review camera").AddComponent<Camera>();
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

                SetLayer(hud.transform);
                if (overlay != null) SetLayer(overlay.transform);
                Canvas.ForceUpdateCanvases();
                foreach (var graphic in Object.FindObjectsByType<Graphic>())
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
            finally
            {
                if (cam != null) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
                if (target != null) Object.DestroyImmediate(target);
                if (overlay != null) Object.DestroyImmediate(overlay.gameObject);
                if (hud != null) { hud.Clear(); Object.DestroyImmediate(hud.gameObject); }
                if (EventSystem.current != null) Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        /// A screen-space canvas laid out as the phone would, but in world space where an
        /// ordinary camera can photograph it. z orders the two canvases for the camera.
        static void WorldSpace(GameObject host, Vector2 canvasSize, float z)
        {
            host.GetComponent<CanvasScaler>().enabled = false;
            host.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = canvasSize;
            rect.position = new Vector3(0f, 0f, z);
            rect.localScale = Vector3.one;
        }

        static void SetLayer(Transform root)
        {
            root.gameObject.layer = ReviewLayer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i));
        }
    }
}
