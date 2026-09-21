using System;
using System.Collections.Generic;
using System.Linq;
using MotionRunner.Commerce;
using NUnit.Framework;
using UnityEngine;

namespace MotionRunner.Tests
{
    public sealed class CosmeticFitTests
    {
        static Type Visual => Type.GetType("MotionRunner.Art.RunnerVisual, Assembly-CSharp",true);
        static GameObject Runner(string skin,string hat=null)
        {
            var root=new GameObject("Fit test");var visual=Visual.GetMethod("Create").Invoke(null,new object[]{root.transform});
            var look=new CosmeticLoadout();look.Equip(CosmeticCatalog.Find(skin));
            look.Clear(CosmeticSlot.Aura);look.Clear(CosmeticSlot.Trail);
            if(hat!=null)look.Equip(CosmeticCatalog.Find(hat));
            Visual.GetMethod("ApplyLoadout").Invoke(visual,new object[]{look});return root;
        }
        // Read deformed vertices; renderer.bounds includes unused animation culling margins.
        static IEnumerable<Vector3> Vertices(SkinnedMeshRenderer r,bool headOnly=false)
        {
            var mesh=r.sharedMesh;var weights=mesh.boneWeights;var vertices=mesh.vertices;
            var matrices=r.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
            var head=r.GetComponentInParent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];
                if(headOnly && !r.name.EndsWith("_Head") && (r.bones[w.boneIndex0]!=head || w.weight0<=0.5f))continue;
                var v=vertices[i];yield return matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+
                    matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            }
        }
        static Bounds Geometry(GameObject root,bool head=false)
        {
            var points=root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>!r.name.Contains("Hat") && !r.name.Contains("Cape"))
                .SelectMany(r=>Vertices(r,head)).ToArray();
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);return bounds;
        }
        [TestCase("ember")][TestCase("frost")]
        public void CharactersMatchRunnerHeightAndKeepBalancedProportionsThroughAnimation(string skin)
        {
            var reference=Runner("runner");var runner=Runner(skin);
            try
            {
                var expected=Geometry(reference);var actual=Geometry(runner);
                Assert.That(actual.size.y,Is.EqualTo(expected.size.y).Within(0.025f),"Bare-head height must match the Runner.");
                Assert.That(actual.min.y,Is.EqualTo(expected.min.y).Within(0.015f),"Feet must share the same ground height.");
                Assert.Less(actual.size.x,expected.size.x*1.30f,"Premium characters must not fill substantially more of the lane.");
                var fittedHead=Geometry(runner,true);
                var a=runner.GetComponentInChildren<Animator>();
                foreach(string state in new[]{"idle","run","jump","dodgeLeft","dodgeRight"})
                {
                    a.Play(state,0,0);
                    for(int frame=0;frame<5;frame++)
                    {
                        a.Update(0.10f);
                        Assert.That(Geometry(runner,true).size.x,Is.EqualTo(fittedHead.size.x).Within(fittedHead.size.x*0.20f),
                            state+" must retain the fitted head proportions through animation.");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(reference);UnityEngine.Object.DestroyImmediate(runner); }
        }
        [TestCase("runner")][TestCase("ember")][TestCase("frost")]
        public void EveryHatSeatsWithinTheUpperHeadInsteadOfFloating(string skin)
        {
            foreach(string hat in new[]{"cap","tophat","crown"})
            {
                var root=Runner(skin,hat);
                try
                {
                    var head=Geometry(root,true);var mesh=root.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Cosmetic "+hat);
                    float lowest=mesh.sharedMesh.vertices.Min(v=>mesh.transform.TransformPoint(v).y);
                    Assert.Less(lowest,head.max.y-0.01f,skin+" "+hat+" has no contact with the head.");
                    Assert.Greater(lowest,head.min.y+head.size.y*0.45f,skin+" "+hat+" covers the face.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        [Test] public void FrostCapeHasAnInsideSurfaceAndReturnsAfterClearingBackSlot()
        {
            var root=Runner("frost");
            try
            {
                var cape=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name.Contains("Cape"));
                var mesh=cape.sharedMesh;var triangles=mesh.triangles;var vertices=mesh.vertices;
                var oriented=new HashSet<(Vector3,Vector3,Vector3)>();
                for(int i=0;i<triangles.Length;i+=3)oriented.Add((vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]));
                foreach(var face in oriented)Assert.IsTrue(oriented.Contains((face.Item1,face.Item3,face.Item2)) || oriented.Contains((face.Item3,face.Item2,face.Item1)) ||
                    oriented.Contains((face.Item2,face.Item1,face.Item3)),"Thin cape needs a reverse-facing triangle, not just a front face.");
                var look=new CosmeticLoadout();look.Equip(CosmeticCatalog.Find("frost"));look.Equip(CosmeticCatalog.Find("wings"));
                var visual=root.GetComponent(Visual);Visual.GetMethod("ApplyLoadout").Invoke(visual,new object[]{look});Assert.IsFalse(cape.enabled);
                look.Clear(CosmeticSlot.Back);Visual.GetMethod("ApplyLoadout").Invoke(visual,new object[]{look});Assert.IsTrue(cape.enabled);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
