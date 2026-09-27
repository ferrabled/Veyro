using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MotionRunner.Art;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// The crash/result clip sources are humanoid rigs; import them as Humanoid so the takes
    /// retarget onto the Kenney runner and the KayKit adventurers exactly like the dodge clips.
    public sealed class CrashClipImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if(!RunnerCrashBuild.Sources.Any(s=>assetPath.StartsWith(s)))return;
            var i=(ModelImporter)assetImporter;
            i.animationType=ModelImporterAnimationType.Human;
            i.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            i.materialImportMode=ModelImporterMaterialImportMode.None;
            i.importCameras=false;i.importLights=false;i.importBlendShapes=false;
            i.isReadable=false;i.addCollider=false;
        }
    }

    /// Crash and result poses (death animations + the result card). Imports takes from two CC0
    /// libraries and adds them as controller states the way RunnerPolishBuild adds the dodges.
    /// ParkArtBuild.BuildRunner calls ConfigureCrashPoses, so a full rebuild keeps the states;
    /// the menu item refreshes only them.
    public static class RunnerCrashBuild
    {
        public const string Quaternius="Assets/Art/Quaternius/";
        public static readonly string[] Sources={Quaternius};
        const string Library=Quaternius+"AnimationLibrary_Unity_Standard.fbx";

        sealed class Take
        {
            public string State,Source,Name;public bool Loop;public float Speed=1;
            public Take(string state,string source,string name,bool loop,float speed=1){State=state;Source=source;Name=name;Loop=loop;Speed=speed;}
        }

        /// The shipped set. crashWall: ran into a full-height block - staggers and falls onto
        /// the back (the hit + fall reads as a slam far better than the 0.3 s chest flinch).
        /// crashTrip: feet catch the hurdle, forward tumble. celebrate: the record dance.
        /// defeat: the card's fall to the ground.
        static readonly Take[] Final=
        {
            new Take("crashWall",Library,"Death01",false,1.7f),
            new Take("crashTrip",Library,"Roll",false,1.15f),
            new Take("celebrate",Library,"Dance_Loop",true),
            new Take("defeat",Library,"Death01",false)
        };

        /// Extra states kept only while choosing (RenderReview draws them too). Empty when shipped.
        /// (The KayKit legacy Cheer/Dance/Defeat takes were tried and dropped: that rig has no
        /// foot bones, so Unity cannot build a Humanoid avatar from it.)
        static readonly Take[] Review={};

        [MenuItem("Veyro/Art/Update crash and result animations")]
        public static void Build()
        {
            var assets=AssetDatabase.LoadAssetAtPath<ParkAssets>("Assets/Resources/Art/Park.asset");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Runner.controller");
            ConfigureCrashPoses(assets,controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[RunnerCrash] CC0 crash/result clips retargeted; chunks and dodges preserved.");
        }

        public static void ConfigureCrashPoses(ParkAssets assets, AnimatorController controller)
        {
            var all=Final.Concat(Review).ToArray();
            var clips=new Dictionary<Take,AnimationClip>();
            foreach(var group in all.GroupBy(t=>t.Source))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(group.Key)
                    ?? throw new InvalidOperationException("Missing "+group.Key);
                if(importer.animationType!=ModelImporterAnimationType.Human)
                { importer.animationType=ModelImporterAnimationType.Human; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel; importer.SaveAndReimport(); }
                var wanted=group.Select(t=>t.Name).Distinct().ToArray();
                var selected=importer.defaultClipAnimations.Where(c=>wanted.Contains(TakeName(c.name))).ToArray();
                if(selected.Length!=wanted.Length)
                    throw new InvalidOperationException("In "+group.Key+" expected takes "+string.Join(",",wanted)+" but found "+
                        string.Join(",",selected.Select(c=>c.name))+" among "+string.Join(",",importer.defaultClipAnimations.Select(c=>c.name)));
                foreach(var clip in selected)
                {
                    clip.loopTime=group.First(t=>t.Name==TakeName(clip.name)).Loop;
                    // Root stays where the controller put it: the collision box does not move when
                    // the runner dies, and the card's stage is a fixed frame. Rotation is baked to
                    // the BODY orientation (not the file's original) so a library whose rest pose
                    // faces the other way does not spin the runner round at the moment of impact.
                    clip.lockRootRotation=true;
                    clip.keepOriginalOrientation=false;
                    clip.lockRootHeightY=true;
                    clip.keepOriginalPositionY=true;
                    clip.lockRootPositionXZ=true;
                    clip.keepOriginalPositionXZ=true;
                }
                importer.clipAnimations=selected;
                importer.SaveAndReimport();
                var imported=AssetDatabase.LoadAllAssetsAtPath(group.Key).OfType<AnimationClip>()
                    .Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                foreach(var take in group)
                {
                    var clip=imported.FirstOrDefault(c=>TakeName(c.name)==take.Name)
                        ?? throw new InvalidOperationException("Take not imported: "+take.Name);
                    if(!clip.humanMotion) throw new InvalidOperationException("Not Humanoid: "+clip.name);
                    clips[take]=clip;
                }
            }
            assets.CrashWall=clips[Final[0]];
            assets.CrashTrip=clips[Final[1]];
            assets.Celebrate=clips[Final[2]];
            assets.Defeat=clips[Final[3]];
            var machine=controller.layers[0].stateMachine;
            // Review states from an earlier pass go away once they are no longer listed.
            foreach(var stale in machine.states.Select(s=>s.state).Where(s=>s.name.StartsWith("rv") && all.All(t=>t.State!=s.name)).ToArray())
                machine.RemoveState(stale);
            foreach(var take in all)
            {
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==take.State)
                    ?? machine.AddState(take.State);
                state.motion=clips[take];
                state.speed=take.Speed;
                foreach(var t in state.transitions.ToArray()) state.RemoveTransition(t);
                Debug.Log($"[RunnerCrash] {take.State} <- {clips[take].name} {clips[take].length:F2}s x{take.Speed} loop={clips[take].isLooping}");
            }
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(assets);
        }

        /// Default clip names come out as "Armature|Take" or "Take" depending on the exporter.
        static string TakeName(string clipName)
        {
            int bar=clipName.LastIndexOf('|');
            return bar>=0 ? clipName.Substring(bar+1) : clipName;
        }

        /// Frames of each pose on each skin, for a visual check without a device:
        /// builds/crash-review/<skin>-<state>-<t>.png. Time is in seconds of state playback.
        public static void RenderReview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.fog=false;
            var sun=new GameObject("Review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=0.8f;sun.transform.rotation=Quaternion.Euler(35,-25,0);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../builds/crash-review"));
            if(Directory.Exists(folder))Directory.Delete(folder,true);
            Directory.CreateDirectory(folder);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Runner.controller");
            var states=controller.layers[0].stateMachine.states.Select(s=>s.state)
                .Where(s=>Final.Concat(Review).Any(t=>t.State==s.name)).ToArray();
            foreach(string skin in new[]{"runner","ember","frost"})
            foreach(var state in states)
            foreach(float seconds in new[]{0.15f,0.4f,0.7f,1.0f,1.6f})
            {
                float length=state.motion.averageDuration/Mathf.Max(0.01f,state.speed);
                float normalized=Mathf.Min(seconds/length,0.999f);
                if(state.motion is AnimationClip c && c.isLooping) normalized=Mathf.Repeat(seconds/length,1);
                var host=new GameObject("Review",typeof(RectTransform));host.GetComponent<RectTransform>().sizeDelta=new Vector2(800,800);
                var preview=MotionRunner.Menu.RunnerPreview.Create(host.transform);
                var look=new MotionRunner.Commerce.CosmeticLoadout();look.Equip(MotionRunner.Commerce.CosmeticCatalog.Find(skin));
                preview.Show(look);preview.Focus(MotionRunner.Commerce.CosmeticSlot.CrashFx,true);
                var animator=preview.Model.GetComponentInChildren<Animator>();
                animator.Rebind();animator.Play(state.name,0,normalized);animator.Update(0);animator.Update(0.001f);
                var cam=preview.PreviewCamera;cam.backgroundColor=new Color(0.76f,0.9f,0.85f,1);var target=cam.targetTexture;
                cam.Render();RenderTexture.active=target;
                var tex=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(folder,$"{skin}-{state.name}-{seconds:F2}.png"),tex.EncodeToPNG());RenderTexture.active=null;
                UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(host);
            }
            Debug.Log("[RunnerCrash] Review rendered to "+folder);
        }

        public static void BuildAndReview(){Build();RenderReview();}
    }
}
