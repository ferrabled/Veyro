using MotionRunner.Track;
using UnityEditor;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Import settings for the how-to-play illustrations, applied the moment a PNG lands under
    /// Assets/Resources/Art/Guide/. The approved masters live in screenshots/guide/; copying
    /// them here is the whole import workflow (docs/GUIDE_ILLUSTRATIONS.md). Without this
    /// a fresh PNG imports as a Default texture with mipmaps, and Resources.Load<Sprite> on it
    /// returns null, which would make the guide show its fallback caption instead of the art.
    ///
    /// The art is an opaque paper-coloured print, approximately 16:10: a single full-rect sprite,
    /// no mips (it is only ever drawn at one size on a canvas), capped at 1024 wide so a 1586 px
    /// source costs the APK what an 804 px slot needs, sRGB, ASTC on Android.
    public sealed class GuideArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GuideIllustration.AssetFolder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;

            // FullRect rather than the default Tight mesh: the frames are opaque rectangles, so a
            // traced outline would be wasted work and a second seam to get wrong.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 1024,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 50
            });
        }
    }
}
