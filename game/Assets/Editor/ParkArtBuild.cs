using System;
using System.IO;
using System.Linq;
using MotionRunner.Art;
using MotionRunner.Track;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotionRunner.EditorTools
{
    /// Explicit, repeatable authoring command; never runs automatically or rewrites a scene.
    public static class ParkArtBuild
    {
        const string Art="Assets/Art/";
        static readonly string[] Props={"tree_default","tree_oak","tree_pineRoundA","plant_bush","rock_largeA","rock_tallA","flower_redA"};
        public static void BuildAndPreview() { Build(); ParkArtPreview.Render(); }

        [MenuItem("Veyro/Art/Rebuild park assets from source")]
        public static void Build()
        {
            Directory.CreateDirectory(Art+"Prefabs");
            Directory.CreateDirectory(Art+"Materials");
            Directory.CreateDirectory(Art+"Chunks");
            Directory.CreateDirectory("Assets/Resources/Art");
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles(Art+"Kenney/Runner", "*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                if(importer.animationType!=ModelImporterAnimationType.Human)
                { importer.animationType=ModelImporterAnimationType.Human; importer.SaveAndReimport(); }
                var clips=importer.defaultClipAnimations;
                foreach(var clip in clips)
                {
                    clip.loopTime=!path.EndsWith("jump.fbx");
                    clip.lockRootRotation=true;
                    clip.keepOriginalOrientation=true;
                    clip.lockRootHeightY=true;
                    clip.lockRootPositionXZ=true;
                }
                if(clips.Length>0) { importer.clipAnimations=clips; importer.SaveAndReimport(); }
                Debug.Log($"[ParkArt] Configured {clips.Length} takes in {path}");
            }
            var manifest=AssetDatabase.LoadAssetAtPath<ParkAssets>("Assets/Resources/Art/Park.asset");
            if(manifest==null)
            {
                manifest=ScriptableObject.CreateInstance<ParkAssets>();
                AssetDatabase.CreateAsset(manifest,"Assets/Resources/Art/Park.asset");
            }
            manifest.Sky=Material("ParkSky","Veyro/Park Sky",Color.white);
            manifest.RunnerMaterial=Material("Runner","Veyro/Runner",Color.white);
            manifest.RunnerMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Kenney/Runner/skaterMaleA.png"));
            manifest.RunnerMaterial.SetColor("_OutfitColor",ParkTheme.Pink);
            EditorUtility.SetDirty(manifest.RunnerMaterial);
            manifest.Props=new GameObject[Props.Length];
            for(int i=0;i<Props.Length;i++) manifest.Props[i]=BuildProp(Props[i]);
            manifest.Run=Clip("run"); manifest.Jump=Clip("jump"); manifest.Idle=Clip("idle");
            manifest.Runner=BuildRunner(manifest);
            var definitions=ChunkLibrary.Greybox();
            manifest.Chunks=new TrackChunkAsset[definitions.Count];
            for(int i=0;i<definitions.Count;i++)
            {
                string path=Art+"Chunks/"+definitions[i].ChunkId+".asset";
                var chunk=AssetDatabase.LoadAssetAtPath<TrackChunkAsset>(path);
                if(chunk==null) { chunk=ScriptableObject.CreateInstance<TrackChunkAsset>(); AssetDatabase.CreateAsset(chunk,path); }
                chunk.Import(definitions[i],i);
                EditorUtility.SetDirty(chunk);
                manifest.Chunks[i]=chunk;
            }
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            Debug.Log("[ParkArt] Built 7 curated props, animated runner and 13 ordered chunks.");
        }

        static AnimationClip Clip(string name)
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(Art+"Kenney/Runner/"+name+".fbx").OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            if(clips.Length==0) throw new InvalidOperationException("No imported clip: "+name);
            foreach(var candidate in clips) Debug.Log($"[ParkArt] take: {candidate.name}, {candidate.length:F2}s");
            var clip=clips.FirstOrDefault(c=>c.name.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0 && c.length>0.1f)
                ?? clips.OrderByDescending(c=>c.length).First();
            Debug.Log($"[ParkArt] {name}: {clip.name}, {clip.length:F2}s, legacy={clip.legacy}");
            return clip;
        }

        static GameObject BuildRunner(ParkAssets assets)
        {
            var root=new GameObject("Runner model");
            // Humanoid retargeting bridges the model and animation exports' bind scales.
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Kenney/Runner/characterMedium.fbx");
            var model=UnityEngine.Object.Instantiate(source,root.transform);
            model.name="Character";

            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial=assets.RunnerMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=false;
            }
            var animator=model.GetComponent<Animator>();
            if(animator==null || animator.avatar==null || !animator.avatar.isValid)
                throw new InvalidOperationException("Runner humanoid avatar is invalid.");
            string controllerPath=Art+"Runner.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine=controller.layers[0].stateMachine;
            foreach(var state in machine.states) machine.RemoveState(state.state);
            foreach(var pair in new[]{("idle",assets.Idle),("run",assets.Run),("jump",assets.Jump)})
            {
                var state=machine.AddState(pair.Item1);
                state.motion=pair.Item2;
                if(pair.Item1=="idle") machine.defaultState=state;
            }
            RunnerPolishBuild.ConfigureDodges(assets,controller);
            RunnerCrashBuild.ConfigureCrashPoses(assets,controller);
            animator.runtimeAnimatorController=controller;
            animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(controller);
            animator.Rebind();
            animator.Play("idle",0,0);
            animator.Update(0);
            // Measure AFTER Mecanim applies the retargeted pose. The FBX bind-pose bounds
            // are hundreds of times larger and would normalize the runner to a pinprick.
            var bounds=BoundsOf(model);
            float scale=1.0f/bounds.size.y;
            model.transform.localScale*=scale;
            model.transform.localPosition=new Vector3(-bounds.center.x*scale,-0.5f-bounds.min.y*scale,-bounds.center.z*scale);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Art+"Prefabs/Runner.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildProp(string name)
        {
            var root=new GameObject(name);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Kenney/Nature/"+name+".fbx");
            var model=UnityEngine.Object.Instantiate(source,root.transform);
            var bounds=BoundsOf(model);
            float scale=1/bounds.size.y;
            model.transform.localScale*=scale;
            model.transform.localPosition=new Vector3(-bounds.center.x*scale,-bounds.min.y*scale,-bounds.center.z*scale);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string original=materials[i].name;
                    Color color=PropColor(original);
                    materials[i]=Material("Park_"+original,"Universal Render Pipeline/Lit",color);
                }
                renderer.sharedMaterials=materials;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Art+"Prefabs/"+name+".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static Color PropColor(string name)
        {
            string key=name.ToLowerInvariant();
            if(key.Contains("bark")||key.Contains("wood")) return ParkTheme.Hex(0x936F58);
            if(key.Contains("leaf")) return ParkTheme.Leaf;
            if(key.Contains("grass")) return ParkTheme.Mint;
            if(key.Contains("red")||key.Contains("flower")) return ParkTheme.Pink;
            if(key.Contains("yellow")) return ParkTheme.Gold;
            if(key.Contains("dirt")) return ParkTheme.Hex(0x829F94);
            return ParkTheme.Stone;
        }

        static Material Material(string name,string shader,Color color)
        {
            string path=Art+"Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                var found=Shader.Find(shader);
                if(found==null) throw new InvalidOperationException("Missing shader: "+shader);
                material=new Material(found) { name=name };
                AssetDatabase.CreateAsset(material,path);
            }
            if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",0);
            material.enableInstancing=true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) throw new InvalidOperationException("No mesh: "+go.name);
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Debug.Log($"[ParkArt] {go.name}: bounds={bounds}");
            return bounds;
        }
    }
}
