using MotionRunner.Gameplay;
using MotionRunner.Inputs;
using UnityEngine;

namespace MotionRunner.Core
{
    /// Builds the entire prototype scene from code — no authored scene content,
    /// so any empty scene boots the game (CLAUDE.md rule 1).
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;

            // Light
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Camera: behind and above the runner, looking down the track
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.16f);
            cam.fieldOfView = 65f;
            camGo.transform.position = new Vector3(0f, 4.2f, -6.2f);
            camGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

            // Road
            new GameObject("Road").AddComponent<RoadScroller>();

            // Runner
            var runner = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            runner.name = "Runner";
            runner.transform.position = new Vector3(0f, 0.5f, 0f);
            runner.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            runner.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Lit(new Color(1f, 0.55f, 0.15f));

            var controller = runner.AddComponent<RunnerController>();
            controller.Input = new CompositeInput(
                new GyroTiltInput(),
                new TouchTapInput(),
                new KeyboardInput());
        }
    }
}
