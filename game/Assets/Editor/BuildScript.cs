using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Headless build entry points, invoked via:
    ///   Unity -batchmode -quit -projectPath game -executeMethod MotionRunner.EditorTools.BuildScript.BuildAndroid
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        // Immutable once the first Play Console upload happens, changeable freely until then.
        // Reverse-DNS of ferrabled.com, a domain the owner holds, so the namespace cannot
        // collide with another developer's. Keep the iOS bundle ID identical (T-032):
        // RevenueCat verifies Shipaton eligibility by bundle ID.
        const string PackageName = "com.ferrabled.veyro.run";

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RepoRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));

        public static void BuildAndroid()
        {
            EnsureScene();

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.productName = "Veyro Run";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // No "Made with Unity" splash. Unity 6 Personal is allowed to turn it off
            // (docs/LICENSING_REVENUE.md §2.12) — the attribution line only becomes
            // required if the game ever shows a credits screen. Set here rather than left
            // to the editor UI so a headless build can never quietly ship it again.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            string outPath = Path.Combine(RepoRoot, "builds", "MotionRunner.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"Build failed: {report.summary.result}");
                EditorApplication.Exit(1);
            }
            Debug.Log($"Build OK: {report.summary.outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
        }

        static void EnsureScene()
        {
            string absScene = Path.Combine(ProjectRoot, ScenePath);
            if (File.Exists(absScene)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(absScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
