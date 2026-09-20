using System;
using System.Collections.Generic;
using MotionRunner.Art;
using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    public sealed class SeasonArtImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/Art/Season/")) return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=false; importer.importCameras=false; importer.importLights=false;
            importer.isReadable=true; importer.addCollider=false; importer.materialImportMode=ModelImporterMaterialImportMode.None;
        }
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Art/Season/")) return;
            var importer=(TextureImporter)assetImporter;
            importer.maxTextureSize=128; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Compressed;
        }
    }
    public static class SeasonArtBuild
    {
        const string Folder="Assets/Art/Season";
        [MenuItem("Veyro/Art/Build season cosmetics")]
        public static void Build()
        {
            var art=AssetDatabase.LoadAssetAtPath<SeasonArt>("Assets/Resources/Art/Season.asset");
            if(art==null) { art=ScriptableObject.CreateInstance<SeasonArt>(); AssetDatabase.CreateAsset(art,"Assets/Resources/Art/Season.asset"); }
            art.Cap=Hat("Cap",0.32f);
            art.TopHat=Hat("TopHat",0.32f);
            art.Crown=Hat("Crown",0.28f);
            art.Spark=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/light_01.png");
            art.Star=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/star_01.png");
            art.Trace=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/trace_01.png");
            art.Effects=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Effects.mat");
            if(art.Effects==null)
            {
                art.Effects=new Material(Shader.Find("Veyro/CosmeticEffect"));
                AssetDatabase.CreateAsset(art.Effects,Folder+"/Effects.mat");
            }
            art.Effects.SetTexture("_BaseMap",art.Spark);
            EditorUtility.SetDirty(art); EditorUtility.SetDirty(art.Effects);
            AssetDatabase.SaveAssets();
            CosmeticCharacterBuild.Build();
            Debug.Log("[SeasonArt] Fitted CC0 headwear, characters, attachments and bounded effects ready.");
        }
        static GameObject Hat(string name,float width)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".fbx");
            var instance=UnityEngine.Object.Instantiate(source);
            var combines=new List<CombineInstance>();
            foreach(var filter in instance.GetComponentsInChildren<MeshFilter>())
                for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                    combines.Add(new CombineInstance { mesh=filter.sharedMesh, subMeshIndex=sub, transform=filter.transform.localToWorldMatrix });
            var mesh=new Mesh { name=name+" fitted", indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(combines.ToArray(),true,true);
            mesh.RecalculateBounds();
            var b=mesh.bounds;
            float scale=width/Mathf.Max(b.size.x,b.size.z);
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++) vertices[i]=(vertices[i]-new Vector3(b.center.x,b.min.y,b.center.z))*scale;
            mesh.vertices=vertices; mesh.RecalculateBounds();
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+name+"Mesh.asset");
            if(old!=null) { EditorUtility.CopySerialized(mesh,old); UnityEngine.Object.DestroyImmediate(mesh); mesh=old; }
            else AssetDatabase.CreateAsset(mesh,Folder+"/"+name+"Mesh.asset");
            var go=new GameObject(name); go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=RuntimeMaterials.Shared(name=="Crown" ? ParkTheme.Hex(0xE4B83F) : ParkTheme.Hex(0x236D73));
            // Shared runtime material is not an asset. Persist this fitted asset's own material.
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
            if(mat==null) { mat=new Material(go.GetComponent<Renderer>().sharedMaterial); AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat"); }
            go.GetComponent<Renderer>().sharedMaterial=mat;
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+name+".prefab");
            Debug.Log("[SeasonArt] "+name+" vertices="+mesh.vertexCount+" size="+mesh.bounds.size);
            UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }
    }
    public sealed class SeasonCurveBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder=>20;
        public void OnPreprocessBuild(BuildReport report)
        {
            bool game=PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(report.summary.platform)))=="com.ferrabled.veyro.run";
            if(game && (report.summary.options & BuildOptions.Development)==0 && !SeasonCurve.ProductionApproved)
                throw new BuildFailedException("Season 1 XP thresholds need owner approval before release. Development builds use the separate test curve.");
        }
    }
}
