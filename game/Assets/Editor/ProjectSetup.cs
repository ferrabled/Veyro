using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MotionRunner.EditorTools
{
    /// One-shot project configuration, invoked headlessly:
    ///   Unity -batchmode -quit -projectPath game -executeMethod MotionRunner.EditorTools.ProjectSetup.SetupUrp
    public static class ProjectSetup
    {
        public static void SetupUrp()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Settings"));

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UrpRendererData.asset");
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, "Assets/Settings/UrpRendererData.asset");
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UrpPipeline.asset");
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, "Assets/Settings/UrpPipeline.asset");
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            // The URP Global Settings asset carries the runtime shader resources
            // (blit/copy-depth etc.). The editor UI creates it implicitly; headless
            // setup must ensure it exists or builds render nothing.
            EnsureUrpGlobalSettings();

            CreateBaseMaterial();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool hasGlobal = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>() != null;
            Debug.Log($"URP configured. GlobalSettings present: {hasGlobal}");
            if (!hasGlobal) EditorApplication.Exit(2);
        }

        // Runtime-created objects can't serialize material references, and in players
        // CreatePrimitive falls back to the built-in Standard material, which URP
        // renders magenta. Ship one URP-Lit material via Resources instead.
        static void CreateBaseMaterial()
        {
            const string path = "Assets/Resources/Materials/PrimitiveLit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader not found.");
                EditorApplication.Exit(3);
                return;
            }
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Materials"));
            AssetDatabase.CreateAsset(new Material(shader), path);
            Debug.Log("Base URP-Lit material created.");
        }

        static void EnsureUrpGlobalSettings()
        {
            var type = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, Unity.RenderPipelines.Universal.Runtime");
            if (type == null)
            {
                Debug.LogError("UniversalRenderPipelineGlobalSettings type not found.");
                return;
            }

            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "Ensure") continue;
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                    args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (ps[i].ParameterType == typeof(bool) ? (object)true : null);
                m.Invoke(null, args);
                Debug.Log("UniversalRenderPipelineGlobalSettings.Ensure() invoked.");
                return;
            }
            Debug.LogError("No Ensure method found on UniversalRenderPipelineGlobalSettings.");
        }
    }
}
