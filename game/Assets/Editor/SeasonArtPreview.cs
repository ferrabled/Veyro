using System.IO;
using MotionRunner.Art;
using MotionRunner.Commerce;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    public static class SeasonArtPreview
    {
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Preview camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,0.95f,-3.8f);camera.transform.rotation=Quaternion.Euler(4,0,0);
            camera.orthographic=true;camera.orthographicSize=1.1f;camera.nearClipPlane=0.1f;camera.farClipPlane=10;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ParkTheme.Hex(0xE2E9DC);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(40,-25,0);sun.intensity=0.7f;
            RenderSettings.fog=false;
            var runner=new GameObject("Runner");runner.transform.position=new Vector3(0,0.5f,0);runner.transform.rotation=Quaternion.Euler(0,180,0);
            var visual=RunnerVisual.Create(runner.transform);
            var animator=runner.GetComponentInChildren<Animator>();animator.Play("idle",0,0.2f);animator.Update(0);
            var target=new RenderTexture(576,720,24);camera.targetTexture=target;camera.aspect=0.8f;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../builds/season-review"));Directory.CreateDirectory(folder);
            foreach(string id in new[]{"cap","tophat","crown","chrome","neon","prism"})
            {
                var loadout=new CosmeticLoadout();loadout.Equip(CosmeticCatalog.Find(id));visual.ApplyLoadout(loadout);
                camera.Render();camera.Render();RenderTexture.active=target;
                var image=new Texture2D(576,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,576,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,id+".png"),image.EncodeToPNG());
                Object.DestroyImmediate(image);
                var head=animator.GetBoneTransform(HumanBodyBones.Head);
                Debug.Log("[SeasonPreview] "+id+" head="+head.position+" rotation="+head.rotation.eulerAngles);
                foreach(var mesh in runner.GetComponentsInChildren<MeshRenderer>())
                    if(mesh.name.StartsWith("Cosmetic"))Debug.Log("[SeasonPreview] "+mesh.name+" bounds="+mesh.bounds);
            }
            RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(target);
            Debug.Log("[SeasonPreview] Images saved: "+folder);
        }
    }
}
