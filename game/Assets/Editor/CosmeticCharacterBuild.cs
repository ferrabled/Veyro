using System;
using System.IO;
using System.Linq;
using MotionRunner.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
namespace MotionRunner.EditorTools
{
    public sealed class AttachmentImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/Art/Attachments/"))return;
            var i=(ModelImporter)assetImporter;i.importAnimation=false;i.isReadable=true;i.addCollider=false;i.importCameras=false;i.importLights=false;
        }
    }
    public static class CosmeticCharacterBuild
    {
        const string Folder="Assets/Art/Attachments";
        public static void Build()
        {
            var art=AssetDatabase.LoadAssetAtPath<CharacterArt>("Assets/Resources/Art/Characters.asset");
            if(art==null){art=ScriptableObject.CreateInstance<CharacterArt>();AssetDatabase.CreateAsset(art,"Assets/Resources/Art/Characters.asset");}
            art.EmberMaterial=CharacterMaterial("Ember","barbarian",1);
            art.FrostMaterial=CharacterMaterial("Frost","mage",2);
            art.Ember=Character("Ember","Barbarian",art.EmberMaterial);
            art.Frost=Character("Frost","Mage",art.FrostMaterial);
            var runner=UnityEngine.Object.Instantiate(ParkAssets.Load().Runner);FitRig(runner);
            PrefabUtility.SaveAsPrefabAsset(runner,"Assets/Art/Prefabs/Runner.prefab");UnityEngine.Object.DestroyImmediate(runner);
            var season=SeasonArt.Load();
            season.Cap=Hat("Cap");season.TopHat=Hat("TopHat");season.Crown=Hat("Crown");
            var shadow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Season/Shadow.mat");
            if(shadow==null){shadow=new Material(season.Effects);AssetDatabase.CreateAsset(shadow,"Assets/Art/Season/Shadow.mat");}
            shadow.SetTexture("_BaseMap",Texture2D.whiteTexture);shadow.SetFloat("_Contact",1);shadow.SetColor("_BaseColor",new Color(0.07f,0.17f,0.16f,0.28f));
            season.Shadow=shadow;EditorUtility.SetDirty(shadow);EditorUtility.SetDirty(season);
            art.Wings=Attachment("Wings","AngelWing",1.0f,new Color(0.90f,0.97f,1),true);
            art.Quiver=Attachment("Quiver","quiver",0.52f,new Color(0.63f,0.37f,0.18f));
            art.Shield=Attachment("Shield","shield_round",0.47f,new Color(0.91f,0.65f,0.22f));
            art.Spellbook=Attachment("Spellbook","spellbook_closed",0.37f,new Color(0.40f,0.39f,0.73f));
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();Debug.Log("[CharacterArt] Characters, bone sockets and four attachments built.");
        }
        static Material CharacterMaterial(string name,string texture,float atlas)
        {
            string path="Assets/Art/KayKit/Adventurers/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Veyro/Runner"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/KayKit/Adventurers/"+texture+"_texture.png"));
            mat.SetFloat("_AtlasMode",atlas);EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject Character(string name,string source,Material material)
        {
            var root=new GameObject(name+" character");
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/KayKit/Adventurers/"+source+".fbx"),root.transform);
            var a=model.GetComponent<Animator>();a.runtimeAnimatorController=ParkAssets.Load().Runner.GetComponentInChildren<Animator>().runtimeAnimatorController;
            a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();a.Play("idle",0,0);a.Update(0.01f);
            // Keep the source characters' rounded proportions, with only a mild silhouette adjustment.
            // Height is normalized uniformly below; strong X/Z compression makes faces and bodies look stretched.
            a.GetBoneTransform(HumanBodyBones.Head).localScale=Vector3.one*0.80f;
            model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(0.86f,1,0.90f));
            foreach(var r in model.GetComponentsInChildren<Renderer>()){r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;}
            foreach(var cape in model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name.Contains("Cape")))
                cape.sharedMesh=TwoSidedCape(cape.sharedMesh,name);
            // Normalize to the same foot/head height, excluding the removable native hats.
            Bounds bounds=new Bounds();bool first=true;
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(r.name.Contains("Hat"))continue;
                var b=PosedBounds(r);if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
            }
            var reference=UnityEngine.Object.Instantiate(ParkAssets.Load().Runner);
            var referenceAnimator=reference.GetComponentInChildren<Animator>();referenceAnimator.Rebind();referenceAnimator.Play("idle",0,0);referenceAnimator.Update(0.01f);
            var referenceBounds=PosedBounds(reference.GetComponentInChildren<SkinnedMeshRenderer>());
            UnityEngine.Object.DestroyImmediate(reference);
            float scale=referenceBounds.size.y/bounds.size.y;model.transform.localScale*=scale;
            model.transform.localPosition=new Vector3(-bounds.center.x*scale,referenceBounds.min.y-bounds.min.y*scale,0);
            FitRig(root,true);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Art/KayKit/Adventurers/"+name+".prefab");
            Debug.Log("[CharacterArt] "+name+" scale="+scale+" height="+bounds.size.y);
            UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        static Bounds PosedBounds(SkinnedMeshRenderer renderer)
        {
            var vertices=WorldVertices(renderer);var bounds=new Bounds(vertices[0],Vector3.zero);
            foreach(var v in vertices)bounds.Encapsulate(v);return bounds;
        }
        static Vector3[] WorldVertices(SkinnedMeshRenderer r)
        {
            var mesh=r.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var matrices=new Matrix4x4[r.bones.Length];
            for(int i=0;i<matrices.Length;i++)matrices[i]=r.bones[i].localToWorldMatrix*mesh.bindposes[i];
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var v=vertices[i];
                vertices[i]=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+
                    matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            }
            return vertices;
        }
        static Mesh TwoSidedCape(Mesh source,string name)
        {
            // Give the thin cloth an inside surface. No extra renderer/material or per-frame work.
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name=name+" two-sided cape";
            int n=source.vertexCount;mesh.vertices=source.vertices.Concat(source.vertices).ToArray();
            mesh.normals=source.normals.Concat(source.normals.Select(v=>-v)).ToArray();
            mesh.uv=source.uv.Concat(source.uv).ToArray();mesh.boneWeights=source.boneWeights.Concat(source.boneWeights).ToArray();
            var triangles=source.triangles;var both=new int[triangles.Length*2];Array.Copy(triangles,both,triangles.Length);
            for(int i=0;i<triangles.Length;i+=3){both[triangles.Length+i]=triangles[i]+n;both[triangles.Length+i+1]=triangles[i+2]+n;both[triangles.Length+i+2]=triangles[i+1]+n;}
            mesh.triangles=both;mesh.RecalculateBounds();
            string path="Assets/Art/KayKit/Adventurers/"+name+"Cape.asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }
        static void FitRig(GameObject root,bool posed=false)
        {
            var rig=root.GetComponent<CharacterRig>() ?? root.AddComponent<CharacterRig>();
            if(rig.HatSocket!=null)UnityEngine.Object.DestroyImmediate(rig.HatSocket.gameObject);
            if(rig.BackSocket!=null)UnityEngine.Object.DestroyImmediate(rig.BackSocket.gameObject);
            var a=root.GetComponentInChildren<Animator>();if(!posed){a.Rebind();a.Play("idle",0,0);a.Update(0.01f);}
            var head=a.GetBoneTransform(HumanBodyBones.Head);var chest=a.GetBoneTransform(HumanBodyBones.Chest) ?? a.GetBoneTransform(HumanBodyBones.Spine);
            bool first=true;var bounds=new Bounds();var headMeshes=new List<(Vector3[] vertices,int[] triangles)>();
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(r.name.Contains("Hat"))continue;
                var vertices=WorldVertices(r);
                var weights=r.sharedMesh.boneWeights;int bone=Array.IndexOf(r.bones,head);
                if(r.name.EndsWith("_Head"))headMeshes.Add((vertices,r.sharedMesh.triangles));
                else if(bone>=0)
                {
                    var triangles=r.sharedMesh.triangles;var headTriangles=new List<int>();
                    for(int t=0;t<triangles.Length;t+=3)
                        if(Enumerable.Range(0,3).All(k=>{var w=weights[triangles[t+k]];return w.boneIndex0==bone && w.weight0>0.5f;}))
                            headTriangles.AddRange(new[]{triangles[t],triangles[t+1],triangles[t+2]});
                    headMeshes.Add((vertices,headTriangles.ToArray()));
                }
                for(int i=0;i<vertices.Length;i++)
                {
                    bool belongs=r.name.EndsWith("_Head");
                    if(!belongs && i<weights.Length){var w=weights[i];belongs=(w.boneIndex0==bone && w.weight0>0.5f)||(w.boneIndex1==bone && w.weight1>0.5f);}
                    if(!belongs)continue;var v=vertices[i];if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);
                }
            }
            if(first)throw new InvalidOperationException("No head geometry: "+root.name);
            rig.HatSocket=new GameObject("Hat socket").transform;rig.HatSocket.SetParent(head,false);
            bool original=!root.name.Contains("Frost") && !root.name.Contains("Ember");
            rig.HatSocket.position=new Vector3(bounds.center.x,bounds.max.y-bounds.size.y*(original ? 0.29f:0.32f),bounds.center.z);
            rig.HatSocket.rotation=root.transform.rotation;
            rig.HeadWidth=bounds.size.x*1.04f;rig.HeadDepth=bounds.size.z*0.96f;
            CharacterRig.HatFit Fit(float drop,float padding,bool cover=false)
            {
                float y=bounds.max.y-bounds.size.y*drop;var section=Section(headMeshes,y);
                if(cover)foreach(var mesh in headMeshes)foreach(int index in mesh.triangles)
                    if(mesh.vertices[index].y>y)section.Encapsulate(mesh.vertices[index]);
                float width=section.size.x*padding,depth=section.size.z*padding;
                return new CharacterRig.HatFit{Size=new Vector3(width,Mathf.Sqrt(width*depth),depth),
                    Offset=new Vector3(section.center.x,y,section.center.z)-rig.HatSocket.position};
            }
            rig.Cap=original ? new CharacterRig.HatFit{Size=new Vector3(rig.HeadWidth,rig.HeadWidth,rig.HeadDepth)} : Fit(0.32f,1.15f,true);
            if(!original)rig.Cap.Size.y=Mathf.Max(rig.Cap.Size.y,bounds.size.y*0.32f*2.35f);
            rig.TopHat=Fit(0.20f,1.075f,true);
            // Frost's squared hair corners need room inside the crown's polygonal band.
            rig.Crown=Fit(0.13f,root.name.Contains("Frost") ? 1.24f:1.08f,true);
            rig.BackSocket=new GameObject("Back socket").transform;rig.BackSocket.SetParent(chest,false);
            var torso=TorsoBounds(root,a);
            float backY=torso.center.y+torso.size.y*0.10f;
            // Intersect the central torso, excluding sleeves, belts and pouches projecting behind it.
            var torsoMeshes=root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>!r.name.Contains("Cape") && !r.name.Contains("Hat") && !r.name.EndsWith("_Head"))
                .Select(r=>(WorldVertices(r),r.sharedMesh.triangles)).ToList();
            float backZ=CentralBack(torsoMeshes,backY,torso.center.x,torso.size.x*0.12f);
            float lowerZ=CentralBack(torsoMeshes,backY-0.04f,torso.center.x,torso.size.x*0.2f);
            float upperZ=CentralBack(torsoMeshes,backY+0.04f,torso.center.x,torso.size.x*0.2f);
            float backAngle=Mathf.Clamp(Mathf.Atan2(upperZ-lowerZ,0.08f)*Mathf.Rad2Deg,-20,20);
            rig.BackSocket.position=new Vector3(torso.center.x,backY,backZ+0.008f);
            rig.BackSocket.rotation=root.transform.rotation*Quaternion.Euler(backAngle,0,0);
            Debug.Log("[CharacterArt] "+root.name+" torso="+torso+" back="+rig.BackSocket.position);
            rig.StockHeadwear=root.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("Hat")).ToArray();
            rig.StockBackwear=root.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("Cape")).ToArray();
            Debug.Log("[CharacterArt] "+root.name+" head="+bounds+" seat="+rig.HatSocket.position+" width="+rig.HeadWidth+" depth="+rig.HeadDepth);
        }
        static IEnumerable<Vector3> SectionPoints(List<(Vector3[] vertices,int[] triangles)> meshes,float y)
        {
            foreach(var mesh in meshes)for(int t=0;t<mesh.triangles.Length;t+=3)for(int k=0;k<3;k++)
            {
                var a=mesh.vertices[mesh.triangles[t+k]];var b=mesh.vertices[mesh.triangles[t+(k+1)%3]];
                if((a.y<=y && b.y>y)||(b.y<=y && a.y>y))yield return Vector3.Lerp(a,b,(y-a.y)/(b.y-a.y));
            }
        }
        static Bounds Section(List<(Vector3[] vertices,int[] triangles)> meshes,float y)
        {
            var points=SectionPoints(meshes,y).ToArray();if(points.Length==0)throw new InvalidOperationException("Empty fitting section at "+y);
            var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);return b;
        }
        static float CentralBack(List<(Vector3[] vertices,int[] triangles)> meshes,float y,float x,float halfWidth)
        {
            var points=SectionPoints(meshes,y).Where(v=>Mathf.Abs(v.x-x)<halfWidth).ToArray();
            if(points.Length==0)throw new InvalidOperationException("No central torso section");return points.Min(v=>v.z);
        }
        static Bounds TorsoBounds(GameObject root,Animator animator)
        {
            // Kenney stores the shirt in the combined body mesh. Its actual spine-weighted
            // vertices provide the back surface; a guessed torso box leaves items in mid-air.
            var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest=animator.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
            var upper=animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? chest;
            bool first=true;var bounds=new Bounds();
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(renderer.name.Contains("Cape") || renderer.name.Contains("Hat") || renderer.name.EndsWith("_Head"))continue;
                var vertices=WorldVertices(renderer);var weights=renderer.sharedMesh.boneWeights;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];var bone=renderer.bones[w.boneIndex0];
                    if(w.weight0<0.35f || (bone!=spine && bone!=chest && bone!=upper))continue;
                    if(first){bounds=new Bounds(vertices[i],Vector3.zero);first=false;}else bounds.Encapsulate(vertices[i]);
                }
            }
            if(first)throw new InvalidOperationException("No torso geometry: "+root.name);
            return bounds;
        }
        static Mesh Combined(string path,Quaternion rotation)
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var parts=new List<CombineInstance>();
            foreach(var f in go.GetComponentsInChildren<MeshFilter>())for(int i=0;i<f.sharedMesh.subMeshCount;i++)
                parts.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=Matrix4x4.Rotate(rotation)*f.transform.localToWorldMatrix});
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray(),true,true);mesh.RecalculateBounds();UnityEngine.Object.DestroyImmediate(go);return mesh;
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            old.Clear();old.indexFormat=mesh.indexFormat;old.vertices=mesh.vertices;old.normals=mesh.normals;old.uv=mesh.uv;old.triangles=mesh.triangles;old.RecalculateBounds();EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;
        }
        static GameObject Hat(string name)
        {
            var mesh=Combined("Assets/Art/Season/"+name+".fbx",Quaternion.Euler(0,name=="Cap" ? -90:0,0));
            var b=mesh.bounds;var v=mesh.vertices;
            // The cap's opening is behind its bill. Measure its crown (upper half), not total length.
            var crown=new Bounds();bool first=true;
            foreach(var p in v)if(p.y>b.min.y+b.size.y*0.45f){if(first){crown=new Bounds(p,Vector3.zero);first=false;}else crown.Encapsulate(p);}
            if(name!="Cap")
            {
                float level=b.min.y+b.size.y*(name=="TopHat" ? 0.23f:0.06f);
                crown=Section(new List<(Vector3[],int[])>{(v,mesh.triangles)},level);
            }
            float width=crown.size.x,depth=crown.size.z;
            if(name=="TopHat")
            {
                // The curled brim also crosses the lower crown: use the straight shaft's
                // taper to find its opening, rather than including the brim in that diameter.
                var low=Section(new List<(Vector3[],int[])>{(v,mesh.triangles)},b.min.y+b.size.y*0.45f);
                var high=Section(new List<(Vector3[],int[])>{(v,mesh.triangles)},b.min.y+b.size.y*0.75f);
                var opening=Vector3.LerpUnclamped(low.size,high.size,(0.10f-0.45f)/0.30f);
                width=opening.x;depth=opening.z;crown.center=low.center;
            }
            for(int i=0;i<v.Length;i++)v[i]=new Vector3((v[i].x-crown.center.x)/width,(v[i].y-b.min.y)/width,(v[i].z-crown.center.z)/depth);
            mesh.vertices=v;mesh.RecalculateBounds();mesh=SaveMesh(mesh,"Assets/Art/Season/"+name+"Mesh.asset");
            var go=new GameObject(name);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Season/"+name+".mat");
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,"Assets/Art/Season/"+name+".prefab");UnityEngine.Object.DestroyImmediate(go);return prefab;
        }
        static GameObject Attachment(string name,string source,float height,Color color,bool wings=false)
        {
            // The supplied props face +Z (and the book's cover +X). Point the decorated
            // face out of the back, leaving the shield handle / flat book cover against it.
            float yaw=source=="quiver" || source=="shield_round" ? 180:source=="spellbook_closed" ? 90:0;
            var mesh=Combined(Folder+"/"+source+".fbx",Quaternion.Euler(0,yaw,0));var b=mesh.bounds;var vertices=mesh.vertices;
            float scale=height/b.size.y;
            for(int i=0;i<vertices.Length;i++)vertices[i]=(vertices[i]-new Vector3(wings ? b.max.x : b.center.x,b.center.y,b.center.z))*scale;
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh=SaveMesh(mesh,Folder+"/"+name+"Mesh.asset");
            string materialPath=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",0.15f);mat.SetFloat("_Cull",0);
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+(wings ? "AngelWing.png" : source=="quiver" ? "rogue_texture.png" : source=="shield_round" ? "knight_texture.png" : "mage_texture.png")));
            if(!wings)mat.SetColor("_BaseColor",source=="shield_round" ? new Color(1,0.83f,0.47f):Color.white);
            EditorUtility.SetDirty(mat);
            var root=new GameObject(name);
            for(int side=0;side<(wings ? 2:1);side++)
            {
                var go=new GameObject(wings ? side==0 ? "Left wing":"Right wing" : name);go.transform.SetParent(root.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
                if(wings){go.transform.localScale=new Vector3(side==0 ? 1:-1,1,1);go.transform.localRotation=Quaternion.Euler(0,side==0 ? -18:18,side==0 ? -12:12);}
                if(source=="quiver")
                {
                    // Carry it diagonally beside the neck, keeping arrow tips out of the head.
                    go.transform.localRotation=Quaternion.Euler(0,0,-18);
                    go.transform.localPosition=new Vector3(0.065f,-0.04f,0);
                }
            }
            if(wings){root.transform.localScale=Vector3.one*0.40f;root.AddComponent<CosmeticWings>();}
            // Put the item's front against z=0 so each character's socket is its back surface.
            float front=wings ? root.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices
                .Where(v=>Mathf.Abs(v.x)<0.08f).Select(v=>f.transform.TransformPoint(v).z)).Max()
                : root.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.z);
            foreach(Transform child in root.transform)child.localPosition-=Vector3.forward*front/root.transform.localScale.z;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        public static void BuildAndReview(){Build();RenderReview();}
        public static void RenderReview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.fog=false;
            var sun=new GameObject("Review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=0.8f;sun.transform.rotation=Quaternion.Euler(35,-25,0);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../builds/cosmetic-review"));Directory.CreateDirectory(folder);
            foreach(string skin in new[]{"runner","ember","frost"})foreach(string item in new[]{"base","cap","wings","shield","quiver","spellbook","shadow"})
            {
                var host=new GameObject("Review",typeof(RectTransform));host.GetComponent<RectTransform>().sizeDelta=new Vector2(800,1000);
                var preview=MotionRunner.Menu.RunnerPreview.Create(host.transform);var look=new MotionRunner.Commerce.CosmeticLoadout();look.Equip(MotionRunner.Commerce.CosmeticCatalog.Find(skin));
                if(item!="base")look.Equip(MotionRunner.Commerce.CosmeticCatalog.Find(item));preview.Show(look);
                var slot=item=="base" ? MotionRunner.Commerce.CosmeticSlot.Skin:MotionRunner.Commerce.CosmeticCatalog.Find(item).Slot;
                preview.Focus(slot,true);
                foreach(var ps in preview.Model.GetComponentsInChildren<ParticleSystem>()){ps.useAutoRandomSeed=false;ps.randomSeed=42;ps.Simulate(0.5f,true,true);}
                var cam=preview.PreviewCamera;cam.backgroundColor=new Color(0.76f,0.9f,0.85f,1);var target=cam.targetTexture;
                cam.Render();cam.Render();RenderTexture.active=target;
                var tex=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,target.width,target.height),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(folder,skin+"-"+item+".png"),tex.EncodeToPNG());RenderTexture.active=null;
                UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(host);
            }
            Debug.Log("[CharacterArt] Review rendered.");
        }
    }
}
