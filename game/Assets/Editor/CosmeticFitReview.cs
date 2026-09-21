using System;
using System.IO;
using System.Linq;
using MotionRunner.Art;
using MotionRunner.Commerce;
using MotionRunner.Menu;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Reproducible front/side/back fitting review; never changes a player's inventory.
    public static class CosmeticFitReview
    {
        public static void Generate() { CosmeticCharacterBuild.Build(); Render(); }
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.fog=false;
            var sun=new GameObject("Fit light").AddComponent<Light>();sun.type=LightType.Directional;
            sun.intensity=0.8f;sun.transform.rotation=Quaternion.Euler(35,-25,0);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../builds/cosmetic-fit"));Directory.CreateDirectory(folder);
            foreach(string skin in new[]{"runner","ember","frost"})
            foreach(string item in new[]{"base","cap","tophat","crown","wings","shield","quiver","spellbook"})
            {
                var host=new GameObject("Fit",typeof(RectTransform));host.GetComponent<RectTransform>().sizeDelta=new Vector2(720,1000);
                var preview=RunnerPreview.Create(host.transform);var look=new CosmeticLoadout();look.Equip(CosmeticCatalog.Find(skin));
                look.Clear(CosmeticSlot.Aura);look.Clear(CosmeticSlot.Trail);
                if(item!="base")look.Equip(CosmeticCatalog.Find(item));preview.Show(look);
                var animator=preview.Model.GetComponentInChildren<Animator>();animator.Play("idle",0,0);animator.Update(0);
                preview.Focus(CosmeticSlot.Skin,true);
                var camera=preview.PreviewCamera;camera.backgroundColor=new Color(0.76f,0.90f,0.85f,1);
                // Fixed scale exposes differences between characters rather than zooming them away.
                camera.orthographicSize=1.08f;camera.transform.localPosition=new Vector3(0,0.98f,-4);camera.transform.localRotation=Quaternion.identity;
                var target=new RenderTexture(420,600,24);var old=camera.targetTexture;camera.targetTexture=target;camera.aspect=0.7f;
                var sheet=new Texture2D(1680,600,TextureFormat.RGB24,false);
                for(int side=0;side<4;side++)
                {
                    preview.Model.localRotation=Quaternion.Euler(0,180+side*90,0);camera.Render();RenderTexture.active=target;
                    sheet.ReadPixels(new Rect(0,0,420,600),side*420,0);
                }
                sheet.Apply();File.WriteAllBytes(Path.Combine(folder,skin+"-"+item+".png"),sheet.EncodeToPNG());RenderTexture.active=null;
                UnityEngine.Object.DestroyImmediate(sheet);camera.targetTexture=old;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(host);
            }
            Debug.Log("[CosmeticFitReview] 24 four-angle fitting sheets rendered.");
        }
    }
}
