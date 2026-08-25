using MotionRunner.Pose;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Cv
{
    /// T-010's acceptance criterion made visible: the camera feed with 33 landmarks drawn over it,
    /// plus the timing readout the task actually exists to produce.
    ///
    /// Built from code with UGUI, like RunHud, because the project has no authored scenes or
    /// prefabs (CLAUDE.md rule 1) and because UGUI is the one UI path already proven to render on
    /// the test phone. Landmarks are anchored in normalised coordinates inside the same rect that
    /// displays the frame, so the overlay is aligned with the picture by construction rather than
    /// by arithmetic that could disagree with the sampler.
    public sealed class PoseOverlay : MonoBehaviour
    {
        static readonly Color TextColor = new Color(0.94f, 0.96f, 1f);
        static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.09f, 0.78f);
        static readonly Color BoneColor = new Color(0.24f, 0.86f, 1f, 0.85f);
        static readonly Color JointColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color LostJointColor = new Color(0.45f, 0.48f, 0.56f, 0.55f);

        const float JointSize = 14f;
        const float BoneThickness = 4f;

        RawImage _feed;
        AspectRatioFitter _feedFitter;
        RectTransform _frameRect;
        RectTransform[] _joints;
        RectTransform[] _bones;
        Text _stats;
        Text _banner;

        public static PoseOverlay Create()
        {
            var go = new GameObject("CvPoseOverlay");
            var overlay = go.AddComponent<PoseOverlay>();
            overlay.Build();
            return overlay;
        }

        void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above anything the game might draw, so the spike is never hidden behind it.
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;

            // The frame, letterboxed to its own aspect. AspectRatioFitter keeps the displayed
            // rect exactly the shape of the texture, which is what lets landmarks be positioned
            // in plain normalised coordinates.
            var feedGo = new GameObject("Feed", typeof(RectTransform));
            feedGo.transform.SetParent(root, false);
            _frameRect = (RectTransform)feedGo.transform;
            _frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            _frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            _frameRect.pivot = new Vector2(0.5f, 0.5f);
            _frameRect.sizeDelta = new Vector2(1080f, 1440f);
            _feed = feedGo.AddComponent<RawImage>();
            _feed.color = Color.white;
            _feedFitter = feedGo.AddComponent<AspectRatioFitter>();
            _feedFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _feedFitter.aspectRatio = 0.75f;

            _bones = new RectTransform[PoseSkeleton.BoneCount];
            for (int i = 0; i < _bones.Length; i++)
                _bones[i] = CreateSprite("Bone" + i, _frameRect, BoneColor);

            _joints = new RectTransform[PoseFrame.JointCount];
            for (int i = 0; i < _joints.Length; i++)
            {
                _joints[i] = CreateSprite("Joint" + i, _frameRect, JointColor);
                _joints[i].sizeDelta = new Vector2(JointSize, JointSize);
            }

            _stats = CreatePanelText("Stats", root, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 24f), new Vector2(-24f, 300f), 34, TextAnchor.LowerLeft);
            _banner = CreatePanelText("Banner", root, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -140f), new Vector2(-24f, -24f), 40, TextAnchor.UpperCenter);

            SetSkeletonVisible(false);
        }

        public void SetFeed(Texture texture)
        {
            if (texture == null) return;
            _feed.texture = texture;
            _feedFitter.aspectRatio = texture.width / (float)texture.height;
        }

        public void SetBanner(string message) => _banner.text = message;

        public void SetStats(string message) => _stats.text = message;

        public void SetSkeletonVisible(bool visible)
        {
            for (int i = 0; i < _bones.Length; i++) _bones[i].gameObject.SetActive(visible);
            for (int i = 0; i < _joints.Length; i++) _joints[i].gameObject.SetActive(visible);
        }

        /// Draws all 33 landmarks. Untracked joints stay on screen in a dimmed colour rather than
        /// disappearing: "33 landmarks over the live feed" is the acceptance criterion, and a
        /// vanishing joint and a mispositioned one look the same when a joint is simply missing.
        public void Draw(PoseFrame frame)
        {
            if (!frame.HasPose)
            {
                SetSkeletonVisible(false);
                return;
            }

            SetSkeletonVisible(true);
            Vector2 size = _frameRect.rect.size;

            for (int i = 0; i < PoseFrame.JointCount; i++)
            {
                PoseLandmark landmark = frame[i];
                RectTransform joint = _joints[i];
                joint.anchorMin = joint.anchorMax = Normalized(landmark);
                joint.anchoredPosition = Vector2.zero;
                joint.GetComponent<Image>().color = landmark.IsTracked ? JointColor : LostJointColor;
            }

            for (int i = 0; i < PoseSkeleton.BoneCount; i++)
            {
                PoseLandmark a = frame[PoseSkeleton.Bones[2 * i]];
                PoseLandmark b = frame[PoseSkeleton.Bones[2 * i + 1]];
                RectTransform bone = _bones[i];

                if (!a.IsTracked || !b.IsTracked)
                {
                    bone.gameObject.SetActive(false);
                    continue;
                }

                Vector2 pa = Vector2.Scale(Normalized(a), size);
                Vector2 pb = Vector2.Scale(Normalized(b), size);
                Vector2 delta = pb - pa;
                float length = delta.magnitude;

                bone.anchorMin = bone.anchorMax = new Vector2(0.5f, 0.5f);
                bone.pivot = new Vector2(0.5f, 0.5f);
                bone.anchoredPosition = 0.5f * (pa + pb) - 0.5f * size;
                bone.sizeDelta = new Vector2(length, BoneThickness);
                bone.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        /// Landmarks arrive with y measured down from the top of the frame; UGUI anchors measure
        /// up from the bottom.
        static Vector2 Normalized(in PoseLandmark landmark) =>
            new Vector2(landmark.X, 1f - landmark.Y);

        static RectTransform CreateSprite(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(BoneThickness, BoneThickness);
            return rect;
        }

        static Text CreatePanelText(string name, RectTransform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize,
            TextAnchor alignment)
        {
            var panelGo = new GameObject(name + "Panel", typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = anchorMin;
            panelRect.anchorMax = anchorMax;
            panelRect.offsetMin = offsetMin;
            panelRect.offsetMax = offsetMax;
            var background = panelGo.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            var textGo = new GameObject(name, typeof(RectTransform));
            textGo.transform.SetParent(panelRect, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 12f);
            textRect.offsetMax = new Vector2(-16f, -12f);

            var text = textGo.AddComponent<Text>();
            text.font = OverlayFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Font _font;

        /// Same fallback chain as RunHud: legacy UGUI Text renders nothing at all when its Font is
        /// null, which on a phone looks like the overlay failing rather than the font failing.
        static Font OverlayFont()
        {
            if (_font != null) return _font;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
            {
                string[] installed = Font.GetOSInstalledFontNames();
                if (installed != null && installed.Length > 0)
                    _font = Font.CreateDynamicFontFromOSFont(installed[0], 48);
            }

            if (_font == null) Debug.LogError("[CV] no font resolved - overlay text will not render.");
            return _font;
        }
    }
}
