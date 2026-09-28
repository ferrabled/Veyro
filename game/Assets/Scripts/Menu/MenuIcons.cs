using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The few icons the UI draws - a chevron, a cross, a tick - built from rotated strokes rather
    /// than glyphs. The legacy runtime font's arrows and ticks sit on the baseline, vary by device
    /// and are sometimes absent (CLAUDE.md gotcha #7's neighbourhood); two thin rectangles render
    /// identically everywhere and take any colour from MenuTheme.
    internal static class MenuIcons
    {
        /// ">" (or "<" with `left`), centred in `box`.
        public static void Chevron(RectTransform box, Color color, bool left = false, float length = 20f,
            float thickness = 4f)
        {
            float d = length * 0.3f;
            float x = left ? -d * 0.2f : d * 0.2f;
            Stroke(box, "Upper", new Vector2(x, d), left ? 45f : -45f, length, thickness, color);
            Stroke(box, "Lower", new Vector2(x, -d), left ? -45f : 45f, length, thickness, color);
        }

        /// "X", centred in `box`.
        public static void Cross(RectTransform box, Color color, float length = 30f, float thickness = 4f)
        {
            Stroke(box, "Rising", Vector2.zero, 45f, length, thickness, color);
            Stroke(box, "Falling", Vector2.zero, -45f, length, thickness, color);
        }

        /// A tick, centred in `box`: a short stroke down-right and a long one up-right from it.
        public static void Tick(RectTransform box, Color color, float size = 30f, float thickness = 5f)
        {
            float shortLeg = size * 0.42f, longLeg = size * 0.85f;
            // The two legs meet at the tick's low point, a little left of centre and below it.
            var joint = new Vector2(-size * 0.12f, -size * 0.2f);
            Stroke(box, "Short", joint + Rotate(new Vector2(-shortLeg * 0.5f, 0f), -45f), -45f, shortLeg, thickness, color);
            Stroke(box, "Long", joint + Rotate(new Vector2(longLeg * 0.5f, 0f), 45f), 45f, longLeg, thickness, color);
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static void Stroke(RectTransform box, string name, Vector2 centre, float degrees, float length,
            float thickness, Color color)
        {
            var go = RuntimeUi.Element(name, box, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
            rect.sizeDelta = new Vector2(length, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            var panel = go.AddComponent<CosmeticPanel>();
            panel.color = color;
            panel.Radius = thickness * 0.5f;
            panel.raycastTarget = false;
        }
    }
}
