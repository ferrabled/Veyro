using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MotionRunner.EditorTools
{
    /// One-shot project configuration, invoked headlessly:
    ///   Unity -batchmode -quit -projectPath game -executeMethod MotionRunner.EditorTools.ProjectSetup.SetupUrp
    ///   Unity -batchmode -quit -projectPath game -executeMethod MotionRunner.EditorTools.ProjectSetup.ApplyAppIcon
    public static class ProjectSetup
    {
        /// Batchmode entry for the launcher icon alone. Exit code 4 when the art is missing, so a
        /// CI step cannot pass while the build would ship Unity's default icon.
        public static void ApplyAppIcon()
        {
            bool applied = EnsureAppIcon();
            AssetDatabase.SaveAssets();
            if (!applied) EditorApplication.Exit(4);
        }

        /// Points PlayerSettings at the icon art under Assets/Art/Icon/ (see AppIcon for the
        /// layers): the flat icon as the default for every platform, and on Android the adaptive
        /// pair (background, foreground) plus the flat icon for the round and legacy kinds. Idempotent,
        /// and re-run before every Android build, so a fresh checkout built headlessly carries the
        /// icon without anyone ever having clicked it into the inspector. A missing texture logs a
        /// warning and leaves that kind as it was - the icon must never fail a build.
        /// Returns false when nothing could be applied because the flat art is missing.
        public static bool EnsureAppIcon()
        {
            var flat = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcon.SourcePath);
            if (flat == null)
            {
                Debug.LogWarning($"[AppIcon] {AppIcon.SourcePath} not found; the build keeps whatever icon PlayerSettings already has.");
                return false;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { flat }, IconKind.Any);

            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcon.BackgroundPath);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcon.ForegroundPath);
            if (background != null && foreground != null)
                SetAndroidIcons(AndroidPlatformIconKind.Adaptive, background, foreground);
            else
                Debug.LogWarning($"[AppIcon] Adaptive layers missing ({AppIcon.BackgroundPath}, {AppIcon.ForegroundPath}); " +
                                 "run Veyro/Art/Regenerate app icon layers. Adaptive icon left unchanged.");

            SetAndroidIcons(AndroidPlatformIconKind.Round, flat);
            SetAndroidIcons(AndroidPlatformIconKind.Legacy, flat);
            Debug.Log($"[AppIcon] Applied {AppIcon.SourcePath} as default/round/legacy icon" +
                      (background != null && foreground != null ? " and the adaptive layers." : "."));
            return true;
        }

        /// Layers are positional: for the adaptive kind Unity takes [0] as background, [1] as
        /// foreground; the single-layer kinds read only [0].
        static void SetAndroidIcons(PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (icons == null || icons.Length == 0)
            {
                Debug.LogWarning($"[AppIcon] No Android icon slots reported for kind {kind}; skipped.");
                return;
            }
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }

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
            EnsureAppIcon();

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
