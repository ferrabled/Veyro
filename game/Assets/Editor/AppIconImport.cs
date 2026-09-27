using UnityEditor;

namespace MotionRunner.EditorTools
{
    /// Import settings for the launcher-icon art under Assets/Art/Icon/, applied on import so a
    /// fresh checkout (or a scratch copy, CLAUDE.md gotcha #4) never depends on a setting that was
    /// clicked in the inspector. Unity reads the icon's pixels when it generates the mipmap-*
    /// launcher sizes, so the textures must be readable; they must stay uncompressed, because the
    /// build resamples them and ASTC block artefacts in a 48 px icon are visible; and they must
    /// never get an Android ASTC override, which would silently reintroduce the compression. None
    /// of these textures ship as runtime assets - only the build pipeline reads them.
    public sealed class AppIconImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AppIcon.Folder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            // Dilates the colour into fully transparent pixels, so downscaling the adaptive
            // foreground never bleeds black into its edge. A no-op on the opaque textures.
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.maxTextureSize = AppIcon.Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ClearPlatformTextureSettings("Android");
            importer.ClearPlatformTextureSettings("iPhone");
        }
    }
}
