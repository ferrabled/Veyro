using System.IO;
using UnityEditor;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// The launcher icon's source art and the two adaptive-icon layers derived from it.
    ///
    /// AppIcon.png is the owner-approved "daily ticket + V monogram" (docs/store-kit/ART_DIRECTION.md
    /// §0a), downscaled from screenshots/icon.png to 1024. Android 8+ masks the launcher icon to a
    /// circle or squircle and guarantees only a 66 dp circle of the 108 dp canvas (61 % of the
    /// side) is visible, so the flat art cannot be the foreground as-is: the ticket's far corner
    /// sits at 0.89 of the half-size and the circle would clip it. Generate() therefore writes
    ///   AppIconForeground.png - the flat art scaled to ForegroundSize and centred on a transparent
    ///                           canvas (the ticket's far corner lands at 0.55 of the half-size,
    ///                           inside the 0.61 safe circle);
    ///   AppIconBackground.png - a solid fill of the art's own ground colour, sampled from the
    ///                           source's corners.
    /// The background is the art's ground and not the brand paper on purpose: the art is a paper
    /// ticket on a flat teal field (#063740, σ < 1 across the corners), so a matching field makes
    /// the scaled square melt into the background layer and the composite reads exactly like the
    /// flat icon, only masked. A paper background would show the teal square's edges through the
    /// round mask as four paper slivers. If the art is ever replaced by something with a
    /// non-uniform ground, revisit this - the corner sample would no longer describe the field.
    ///
    /// Deterministic by construction (integer supersampling, no time, no randomness): running
    /// Generate() twice on the same source produces byte-identical PNGs, so the outputs are
    /// committed as assets and regenerated only when the source changes. ProjectSetup.EnsureAppIcon
    /// wires all three into PlayerSettings.
    public static class AppIcon
    {
        public const string Folder = "Assets/Art/Icon/";
        public const string SourcePath = Folder + "AppIcon.png";
        public const string ForegroundPath = Folder + "AppIconForeground.png";
        public const string BackgroundPath = Folder + "AppIconBackground.png";

        /// Side of every icon texture, and the importer's cap. 1024 is the Play / Devpost spec.
        public const int Size = 1024;

        /// Side of the scaled art inside the foreground canvas: ~62 % of Size, rounded to an even
        /// number so the margins are integral (195 px each side).
        public const int ForegroundSize = 634;

        /// Point samples per axis per destination pixel. 4x4 = 16 samples for a ~1.6x downscale
        /// is enough for launcher art that the build resamples again to <= 432 px.
        const int Supersample = 4;

        [MenuItem("Veyro/Art/Regenerate app icon layers")]
        public static void Generate()
        {
            string absSource = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourcePath));
            if (!File.Exists(absSource))
            {
                Debug.LogWarning($"[AppIcon] Source art missing at {SourcePath}; nothing generated.");
                return;
            }

            // Decoded from the file rather than read through the importer, so the result never
            // depends on the texture's import state (readable, compressed, resized).
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!source.LoadImage(File.ReadAllBytes(absSource), false))
            {
                Debug.LogWarning($"[AppIcon] Could not decode {SourcePath}; nothing generated.");
                Object.DestroyImmediate(source);
                return;
            }
            if (source.width != source.height)
                Debug.LogWarning($"[AppIcon] Source art is {source.width}x{source.height}, not square; the layers will be distorted.");

            Color32[] src = source.GetPixels32();
            int srcWidth = source.width, srcHeight = source.height;
            Object.DestroyImmediate(source);

            Color32 ground = SampleGround(src, srcWidth, srcHeight);
            byte[] foreground = EncodeForeground(src, srcWidth, srcHeight, ground);
            byte[] background = EncodeBackground(ground);

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            File.WriteAllBytes(Path.Combine(root, ForegroundPath), foreground);
            File.WriteAllBytes(Path.Combine(root, BackgroundPath), background);
            AssetDatabase.ImportAsset(ForegroundPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BackgroundPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[AppIcon] Generated {ForegroundPath} ({foreground.Length / 1024} KB, art at " +
                      $"{ForegroundSize}/{Size} px) and {BackgroundPath} ({background.Length / 1024} KB, " +
                      $"ground #{ground.r:X2}{ground.g:X2}{ground.b:X2}) from {srcWidth}x{srcHeight} source.");
        }

        /// The flat field the ticket sits on: the mean of four 8x8 corner patches.
        static Color32 SampleGround(Color32[] px, int width, int height)
        {
            const int patch = 8;
            long r = 0, g = 0, b = 0, n = 0;
            foreach (var (x0, y0) in new[] { (0, 0), (width - patch, 0), (0, height - patch), (width - patch, height - patch) })
            {
                for (int y = y0; y < y0 + patch; y++)
                for (int x = x0; x < x0 + patch; x++)
                {
                    var c = px[y * width + x];
                    r += c.r; g += c.g; b += c.b; n++;
                }
            }
            return new Color32((byte)((r + n / 2) / n), (byte)((g + n / 2) / n), (byte)((b + n / 2) / n), 255);
        }

        /// The art resampled to ForegroundSize and centred on a Size canvas. Transparent pixels
        /// carry the ground colour with alpha 0, so any resampler that does not premultiply still
        /// blends the square's edge toward the background layer rather than toward black.
        static byte[] EncodeForeground(Color32[] src, int srcWidth, int srcHeight, Color32 ground)
        {
            var canvas = new Color32[Size * Size];
            var clear = new Color32(ground.r, ground.g, ground.b, 0);
            for (int i = 0; i < canvas.Length; i++) canvas[i] = clear;

            int offset = (Size - ForegroundSize) / 2;
            for (int dy = 0; dy < ForegroundSize; dy++)
            for (int dx = 0; dx < ForegroundSize; dx++)
                canvas[(offset + dy) * Size + offset + dx] = Resample(src, srcWidth, srcHeight, dx, dy);

            return Encode(canvas, TextureFormat.RGBA32);
        }

        static byte[] EncodeBackground(Color32 ground)
        {
            var canvas = new Color32[Size * Size];
            for (int i = 0; i < canvas.Length; i++) canvas[i] = ground;
            return Encode(canvas, TextureFormat.RGB24);
        }

        /// Box-average of Supersample² nearest-neighbour taps spread evenly over the destination
        /// pixel's footprint in the source. Integer arithmetic only, so the output is identical on
        /// every machine.
        static Color32 Resample(Color32[] src, int srcWidth, int srcHeight, int dx, int dy)
        {
            int r = 0, g = 0, b = 0, a = 0;
            for (int j = 0; j < Supersample; j++)
            for (int i = 0; i < Supersample; i++)
            {
                // Tap centre in destination space is (dx + (i + 0.5) / S); map to source with
                // integer maths: floor(((dx * S + i) * 2 + 1) * srcSize / (ForegroundSize * S * 2)).
                int sx = (int)(((long)(dx * Supersample + i) * 2 + 1) * srcWidth / ((long)ForegroundSize * Supersample * 2));
                int sy = (int)(((long)(dy * Supersample + j) * 2 + 1) * srcHeight / ((long)ForegroundSize * Supersample * 2));
                var c = src[sy * srcWidth + sx];
                r += c.r; g += c.g; b += c.b; a += c.a;
            }
            const int n = Supersample * Supersample;
            return new Color32((byte)((r + n / 2) / n), (byte)((g + n / 2) / n), (byte)((b + n / 2) / n), (byte)((a + n / 2) / n));
        }

        static byte[] Encode(Color32[] pixels, TextureFormat format)
        {
            var tex = new Texture2D(Size, Size, format, false, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }
    }
}
