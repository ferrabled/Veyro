using System;
using System.IO;
using MotionRunner.Art;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Menu;
using MotionRunner.Social;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace MotionRunner.EditorTools
{
    public static class CosmeticUiReview
    {
        public static void Generate(){CosmeticCharacterBuild.Build();CosmeticCharacterBuild.RenderReview();CosmeticFitReview.Render();CosmeticThumbnailBuild.Generate();Render();}
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.fog=false;
            var sun=new GameObject("Review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=0.8f;sun.transform.rotation=Quaternion.Euler(35,-25,0);
            const string key="veyro.season.last-profile";bool had=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key);
            var store=FakeStore.WithDefaultCatalog();store.IsReady=true;
            var profile=new FakeProfileService();profile.BecomeReady(new Profile("cosmetic-editor-review","REVIEW",9));
            using(var season=new SeasonService(store,profile,SeasonCurve.Testing))using(var skins=new SkinService(store,season,_=>{}))
            {
                MainMenu menu=null;
                try
                {
                    menu=MainMenu.Create(store,skins,profile,MenuTab.Shop);
                    var canvas=menu.GetComponent<Canvas>();menu.GetComponent<CanvasScaler>().enabled=false;
                    canvas.renderMode=RenderMode.WorldSpace;
                    var rect=menu.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1080,2400);rect.position=Vector3.zero;rect.localScale=Vector3.one;
                    var cam=new GameObject("UI review camera").AddComponent<Camera>();cam.transform.position=Vector3.back*10;
                    cam.orthographic=true;cam.orthographicSize=1200;cam.aspect=1080f/2400;cam.nearClipPlane=0.1f;cam.farClipPlane=20;
                    cam.cullingMask=1<<29;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=MenuTheme.Card;
                    var target=new RenderTexture(1080,2400,24);cam.targetTexture=target;
                    void Capture(string name)
                    {
                        foreach(var p in menu.GetComponentsInChildren<RunnerPreview>(true))
                        {
                            // Normal player-loop lifecycle is not invoked in EditMode authoring.
                            var callback=typeof(RunnerPreview).GetMethod(p.gameObject.activeInHierarchy ? "OnEnable":"OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                            callback.Invoke(p,null);
                        }
                        RunnerCosmetics.SetLayerRecursively(menu.gameObject,29);Canvas.ForceUpdateCanvases();
                        foreach(var g in menu.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);}
                        Canvas.ForceUpdateCanvases();
                        foreach(var preview in menu.GetComponentsInChildren<RunnerPreview>()){preview.Focus(preview.FocusedSlot,true);preview.PreviewCamera.Render();}
                        cam.Render();cam.Render();RenderTexture.active=target;
                        var image=new Texture2D(1080,2400,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1080,2400),0,0);image.Apply();
                        var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../builds/cosmetic-review"));Directory.CreateDirectory(folder);
                        File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;
                    }
                    Capture("shop-editor");menu.ShowSeasonReward("wings");Capture("pass-editor");
                    menu.ShowCosmetic("wings");Capture("locker-editor");
                    cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(cam.gameObject);
                }
                finally
                {
                    if(menu!=null)UnityEngine.Object.DestroyImmediate(menu.gameObject);
                    if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.DeleteKey("veyro.season.xp.cosmetic-editor-review");PlayerPrefs.Save();
                }
            }
            Debug.Log("[CosmeticUiReview] Shop, timeline and locker rendered.");
        }
    }
}
