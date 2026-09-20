using System;
using System.Collections;
using System.Linq;
using MotionRunner.Track;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotionRunner.Tests
{
    public sealed class ParkArtTests
    {
        static UnityEngine.Object Manifest => AssetDatabase.LoadMainAssetAtPath("Assets/Resources/Art/Park.asset");
        static object Field(object value,string name) => value.GetType().GetField(name).GetValue(value);
        static ChunkDefinition[] Definitions()
        {
            Assert.IsNotNull(Manifest,"Run Veyro/Art/Rebuild park assets from source.");
            return ((IEnumerable)Field(Manifest,"Chunks")).Cast<object>().Select(c=>
                (ChunkDefinition)c.GetType().GetMethod("ToDefinition").Invoke(c,null)).ToArray();
        }

        [Test]
        public void AuthoredChunksPreserveEveryPublishedGameplayFieldAndTheirOrder()
        {
            var current=Definitions();
            var shipped=ChunkLibrary.Greybox();
            Assert.AreEqual(shipped.Count,current.Length);
            for(int i=0;i<current.Length;i++)
            {
                var a=current[i]; var b=shipped[i];
                Assert.IsNull(a.Validate(),a.ChunkId);
                Assert.AreEqual(b.ChunkId,a.ChunkId);
                Assert.AreEqual(b.Biome,a.Biome);
                Assert.AreEqual(b.DifficultyMin,a.DifficultyMin);
                Assert.AreEqual(b.DifficultyMax,a.DifficultyMax);
                Assert.AreEqual(b.Length,a.Length);
                Assert.AreEqual(b.EntryType,a.EntryType);
                Assert.AreEqual(b.ExitType,a.ExitType);
                Assert.AreEqual(b.RequiredSkills,a.RequiredSkills);
                Assert.AreEqual(b.Tags,a.Tags);
                Assert.AreEqual(b.Rarity,a.Rarity);
                CollectionAssert.AreEqual(b.Obstacles,a.Obstacles);
                CollectionAssert.AreEqual(b.Coins,a.Coins);
            }
        }

        [Test]
        public void SerializedLibraryProducesTheSameDailyRunsAtEveryDifficulty()
        {
            var current=Definitions();
            for(int seed=0;seed<20;seed++)
            {
                var runSeed=new RunSeed(20260901+seed,ChunkLibrary.ContentVersion,RunSeed.DefaultWorldId);
                var before=new TrackGenerator(ChunkLibrary.Greybox(),runSeed.CreateRandom());
                var after=new TrackGenerator(current,runSeed.CreateRandom());
                for(int i=0;i<500;i++) Assert.AreEqual(before.Next(1+i%8).ChunkId,after.Next(1+i%8).ChunkId);
            }
        }

        [Test]
        public void EveryAnimationBindsToTheShippedSkeleton()
        {
            var prefab=(GameObject)Field(Manifest,"Runner");
            var animation=prefab.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animation);
            Assert.IsTrue(animation.avatar.isValid);
            Assert.IsTrue(animation.avatar.isHuman);
            foreach(string name in new[]{"Run","Jump","Idle"})
            {
                var clip=(AnimationClip)Field(Manifest,name);
                Assert.IsTrue(clip.humanMotion,name);
                Assert.Greater(clip.length,0.1f);
                Assert.AreEqual(name!="Jump",clip.isLooping,name+" loop setting");
            }
            var instance=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var animator=instance.GetComponentInChildren<Animator>();
                animator.Play("run",0,0);
                animator.Update(0);
                var leg=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var before=leg.localRotation;
                animator.Update(0.25f);
                Assert.Greater(Quaternion.Angle(before,leg.localRotation),2f,"Run must animate the actual thigh.");
                var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(skin.bounds.size.y,Is.InRange(0.65f,1.25f),
                    "Retargeted runner must stay close to its one-metre collision silhouette.");
                Assert.That(animator.GetBoneTransform(HumanBodyBones.Head).position.y,
                    Is.InRange(0.1f,0.6f),"Head must be above the feet and near the controller root.");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void CuratedPropsHaveReadableMeshesAndUrpMaterialsWithoutColliders()
        {
            var props=(GameObject[])Field(Manifest,"Props");
            Assert.AreEqual(7,props.Length);
            foreach(var prefab in props)
            {
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>());
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>()) Assert.IsTrue(filter.sharedMesh.isReadable,prefab.name);
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>())
                    foreach(var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material);
                        Assert.AreEqual("Universal Render Pipeline/Lit",material.shader.name);
                    }
            }
        }
    }
}
