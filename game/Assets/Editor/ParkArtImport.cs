using UnityEditor;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    public sealed class ParkArtImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            bool kayKit=assetPath.StartsWith("Assets/Art/KayKit/");
            if(!kayKit && !assetPath.StartsWith("Assets/Art/Kenney/")) return;
            var importer=(ModelImporter)assetImporter;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.isReadable=assetPath.Contains("/Nature/"); // chunk mesh combination, once per pooled view
            importer.importCameras=false;
            importer.importLights=false;
            importer.addCollider=false;
            if(kayKit || assetPath.Contains("/Runner/"))
            {
                importer.animationType=ModelImporterAnimationType.Human;
                importer.importAnimation=true;
                importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects=false;

            }
            else importer.importAnimation=false;
        }

        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Art/Kenney/")) return;
            var importer=(TextureImporter)assetImporter;
            importer.maxTextureSize=1024;
            importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.Compressed;
        }
    }
}
