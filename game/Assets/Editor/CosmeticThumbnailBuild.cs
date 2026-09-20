using System.IO;
using System.Linq;
using MotionRunner.Art;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Bakes the existing CC0 model/accessories and original finishes into inexpensive UI sprites.
    public static class CosmeticThumbnailBuild
    {
        public static void Generate()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.fog=false;
            const string folder="Assets/Resources/Art/CosmeticThumbnails";
            Directory.CreateDirectory(folder);
            var sun=new GameObject("Thumbnail sun").AddComponent<Light>();sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(40,-25,0);sun.intensity=0.7f;
            foreach(var item in CosmeticCatalog.Items)
            {
                var host=new GameObject("Thumbnail",typeof(RectTransform));
                host.GetComponent<RectTransform>().sizeDelta=new Vector2(400,400);
                var preview=RunnerPreview.Create(host.transform);
                var animator=preview.Model.GetComponentInChildren<Animator>();animator.Play("idle",0,0.2f);animator.Update(0);
                var loadout=new CosmeticLoadout();loadout.Equip(item);preview.Show(loadout);preview.Focus(item.Slot,true);
                var camera=preview.PreviewCamera;camera.aspect=1;
                if(item.Slot==CosmeticSlot.Headwear)
                {
                    foreach(var skin in preview.Model.GetComponentsInChildren<SkinnedMeshRenderer>())skin.enabled=false;
                    preview.Model.Find("Runner ground shadow").gameObject.SetActive(false);
                    var hat=preview.Model.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.StartsWith("Cosmetic "));
                    camera.transform.position=hat.bounds.center+Vector3.back*4+Vector3.up*0.15f;
                    camera.transform.LookAt(hat.bounds.center);
                    camera.orthographicSize=Mathf.Max(hat.bounds.extents.y,hat.bounds.extents.x)*1.4f;
                }
                else if(item.Slot==CosmeticSlot.Back)
                {
                    foreach(var skin in preview.Model.GetComponentsInChildren<SkinnedMeshRenderer>())skin.enabled=false;
                    preview.Model.Find("Runner ground shadow").gameObject.SetActive(false);
                    var root=preview.Model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Cosmetic "+item.Id);
                    var meshes=root.GetComponentsInChildren<MeshRenderer>();var bounds=meshes[0].bounds;
                    foreach(var mesh in meshes)bounds.Encapsulate(mesh.bounds);
                    camera.transform.position=bounds.center+Vector3.back*4+Vector3.up*0.08f;camera.transform.LookAt(bounds.center);
                    camera.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.y)*1.2f;
                }
                else if(item.Slot==CosmeticSlot.Body)
                {
                    camera.transform.position=new Vector3(preview.Model.position.x,0.93f,-4);
                    camera.transform.rotation=Quaternion.identity;camera.orthographicSize=0.43f;
                }
                foreach(var effect in preview.Model.GetComponentsInChildren<ParticleSystem>())
                {
                    effect.useAutoRandomSeed=false;effect.randomSeed=42;
                    if(item.Slot==CosmeticSlot.CrashFx){effect.Clear();effect.Emit(28);effect.Simulate(0.22f,false,false);}
                    else effect.Simulate(0.65f,true,true);
                }
                var oldTarget=camera.targetTexture;
                int resolution=item.Slot==CosmeticSlot.Skin ? 512:256;
                var target=new RenderTexture(resolution,resolution,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;
                camera.Render();camera.Render();RenderTexture.active=target;
                var image=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,resolution,resolution),0,0);image.Apply();
                string path=folder+"/"+item.Id+".png";File.WriteAllBytes(path,image.EncodeToPNG());
                RenderTexture.active=null;camera.targetTexture=oldTarget;
                Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(host);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=resolution;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                Debug.Log("[CosmeticThumbnail] "+item.Id);
            }
            AssetDatabase.SaveAssets();Debug.Log("[CosmeticThumbnail] All catalog item thumbnails generated.");
        }
    }
}
