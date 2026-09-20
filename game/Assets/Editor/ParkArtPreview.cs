using System.IO;
using MotionRunner.Art;
using MotionRunner.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Renders the actual imported assets without touching Main.unity or entering a scored run.
    public static class ParkArtPreview
    {
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var assets=ParkAssets.Load();
            var camera=new GameObject("Preview camera").AddComponent<Camera>();
            camera.transform.position=new Vector3(0,4.2f,-6.2f);
            camera.transform.rotation=Quaternion.Euler(24,0,0);
            camera.fieldOfView=65;
            camera.farClipPlane=160;
            var sun=new GameObject("Sun").AddComponent<Light>();
            sun.type=LightType.Directional;
            ParkTheme.Apply(camera,sun);
            var world=new GameObject("World");
            float z=-10;
            foreach(int index in new[]{2,3,9,10,12,7})
            {
                var definition=assets.Chunks[index].ToDefinition();
                var chunk=ChunkView.Create(definition,world.transform);
                chunk.PlaceAt(z); z+=definition.Length;
            }
            var runner=new GameObject("Preview runner");
            runner.transform.position=new Vector3(0,0.5f,0);
            RunnerVisual.Create(runner.transform);
            var animation=runner.GetComponentInChildren<Animator>();
            animation.Play("run",0,0.18f);
            animation.Update(0);
            var target=new RenderTexture(720,1280,24);
            camera.targetTexture=target;
            camera.aspect=720f/1280f;
            camera.Render();
            camera.Render();
            RenderTexture.active=target;
            var image=new Texture2D(720,1280,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,720,1280),0,0);
            image.Apply();
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../builds/art-review/park-preview.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output,image.EncodeToPNG());
            RenderTexture.active=null;
            camera.targetTexture=null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            Debug.Log("[ParkArt] Preview: "+output);
        }
    }
}
