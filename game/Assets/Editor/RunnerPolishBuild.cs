using System;
using System.Linq;
using MotionRunner.Art;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Updates only the runner animation assets, preserving authored chunk data.
    public static class RunnerPolishBuild
    {
        const string Source="Assets/Art/KayKit/Rig_Medium_MovementAdvanced.fbx";

        [MenuItem("Veyro/Art/Update lane dodge animations")]
        public static void Build()
        {
            var assets=AssetDatabase.LoadAssetAtPath<ParkAssets>("Assets/Resources/Art/Park.asset");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Runner.controller");
            ConfigureDodges(assets,controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[RunnerPolish] Two CC0 dodge clips retargeted; existing chunks preserved.");
        }

        public static void ConfigureDodges(ParkAssets assets, AnimatorController controller)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Source);
            var clips=importer.defaultClipAnimations.Where(c=>
                c.name.EndsWith("Dodge_Left",StringComparison.Ordinal) ||
                c.name.EndsWith("Dodge_Right",StringComparison.Ordinal)).ToArray();
            if(clips.Length!=2) throw new InvalidOperationException("Expected two KayKit lateral dodge takes.");
            foreach(var clip in clips)
            {
                clip.loopTime=false;
                clip.lockRootRotation=true;
                clip.keepOriginalOrientation=true;
                clip.lockRootHeightY=true;
                // Extract travel from the pose; applyRootMotion=false discards it at runtime.
                // Baking XZ into the bones would add a second sidestep to the controller's lane move.
                clip.lockRootPositionXZ=false;
                clip.keepOriginalPositionXZ=false;
            }
            importer.clipAnimations=clips;
            importer.SaveAndReimport();
            var imported=AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            assets.DodgeLeft=imported.Single(c=>c.name.EndsWith("Dodge_Left",StringComparison.Ordinal));
            assets.DodgeRight=imported.Single(c=>c.name.EndsWith("Dodge_Right",StringComparison.Ordinal));
            var machine=controller.layers[0].stateMachine;
            foreach(var pair in new[]{("dodgeLeft",assets.DodgeLeft),("dodgeRight",assets.DodgeRight)})
            {
                if(!pair.Item2.humanMotion) throw new InvalidOperationException("Dodge is not Humanoid: "+pair.Item2.name);
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==pair.Item1)
                    ?? machine.AddState(pair.Item1);
                state.motion=pair.Item2;
                state.speed=pair.Item2.length/RunnerVisual.DodgeDuration;
            }
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(assets);
        }
    }
}
