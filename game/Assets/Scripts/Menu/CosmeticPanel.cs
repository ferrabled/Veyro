using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// Rounded, optionally graded UI surface. Native canvas geometry, no extra textures.
    public sealed class CosmeticPanel : Image
    {
        public float Radius = 24;
        public Color Bottom;
        public bool Gradient;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            Add(mesh, rect.center, rect);
            const int steps = 8;
            for (int corner = 0; corner < 4; corner++)
            {
                var centre = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                    corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (90 - corner * 90 - step * 90f / steps) * Mathf.Deg2Rad;
                    Add(mesh, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, rect);
                }
            }
            int count = 4 * (steps + 1);
            for (int i = 1; i <= count; i++) mesh.AddTriangle(0, i, i == count ? 1 : i + 1);
        }
        void Add(VertexHelper mesh, Vector2 point, Rect rect)
        {
            var tint = Gradient ? Color.Lerp(Bottom, color, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)) : color;
            mesh.AddVert(point, tint, Vector2.zero);
        }
    }
}
