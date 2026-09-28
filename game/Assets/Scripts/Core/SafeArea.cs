using UnityEngine;

namespace MotionRunner.Core
{
    /// Keeps a screen's content out of the parts of the display the phone does not fully own:
    /// a camera cutout (the OnePlus 6T's centred teardrop, the Nord 2's corner punch-hole), the
    /// iPhone's notch or Dynamic Island, the iOS home indicator, Android's gesture bar. The game
    /// draws edge to edge - the track and the backdrops run under all of that
    /// (`androidRenderOutsideSafeArea: 1`, the iOS default) - and only what the player has to READ
    /// or TAP is fitted inside.
    ///
    /// One mechanism for every platform: Unity reports the usable part of the screen as
    /// Screen.safeArea on Android (from the display-cutout insets) and on iOS (from the safe-area
    /// insets) alike, in pixels. This component turns that rect into anchors on a full-screen
    /// child of a canvas, and every screen builds the content it wants kept clear under that
    /// child (RuntimeUi.SafeRoot). Backdrops stay on the canvas itself, so they still bleed to the
    /// edges. Nothing here knows which phone it is on - there is no per-device table to keep up.
    ///
    /// The safe area can change while running (rotation is locked, but split-screen, a folding
    /// phone or the Device Simulator can all move it), so it is re-checked each frame: two rect
    /// compares, and a re-anchor only when something actually moved.
    /// ExecuteAlways so the editor review renders (which build screens outside Play mode) fit
    /// to a Simulated cutout too.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// A safe area to use instead of the device's, normalised 0..1 with the origin at the
        /// bottom left - for desk renders (the review tools) that want to see a cutout without a
        /// phone. Null in the game.
        public static Rect? Simulated;

        RectTransform _rect;
        Rect _applied;
        Vector2Int _screen;
        bool _hasApplied;

        void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply(true);
        }

        void Update() => Apply(false);

        void Apply(bool force)
        {
            Rect normalised = Normalised();
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && _hasApplied && normalised == _applied && screen == _screen) return;

            // Development builds say what the platform reported, once per change: the one way to
            // check a cutout on a device, since screenshots do not draw the notch.
            if (Debug.isDebugBuild && Application.isPlaying && (!_hasApplied || normalised != _applied))
                Debug.Log("[SafeArea] " + name + " safeArea=" + Screen.safeArea + " screen=" + screen.x + "x" +
                          screen.y + " anchors=" + normalised.min + ".." + normalised.max);

            _applied = normalised;
            _screen = screen;
            _hasApplied = true;
            _rect.anchorMin = normalised.min;
            _rect.anchorMax = normalised.max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        /// The safe area as fractions of the screen. Anything degenerate - a zero-sized screen in
        /// batchmode, an inverted or out-of-bounds rect from a misbehaving platform layer - falls
        /// back to the whole screen: content drawn under a cutout is a blemish, content squeezed
        /// to nothing is a broken game.
        public static Rect Normalised()
        {
            if (Simulated.HasValue) return Clamp(Simulated.Value);

            float width = Screen.width, height = Screen.height;
            if (width <= 0f || height <= 0f) return new Rect(0f, 0f, 1f, 1f);

            Rect safe = Screen.safeArea;
            return Clamp(new Rect(safe.x / width, safe.y / height, safe.width / width, safe.height / height));
        }

        static Rect Clamp(Rect r)
        {
            float xMin = Mathf.Clamp01(r.xMin), yMin = Mathf.Clamp01(r.yMin);
            float xMax = Mathf.Clamp01(r.xMax), yMax = Mathf.Clamp01(r.yMax);
            // A safe area that leaves less than half the screen is not a safe area, it is a bug.
            if (xMax - xMin < 0.5f || yMax - yMin < 0.5f) return new Rect(0f, 0f, 1f, 1f);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
