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

        public static void BuildAndroid() => Build(PackageName, "Veyro Run", "MotionRunner.apk");

        /// The camera-feature branch's test build. Its own package name so installing it can
        /// never replace the working v1.0 game on a device, and its own APK name so it can never
        /// overwrite builds/MotionRunner.apk. The real package name takes over only when camera
        /// mode merges for the post-release update (D2).
        public static void BuildAndroidCamDev() =>
            Build("com.ferrabled.veyro.camdev", "Veyro Cam Dev", "VeyroCamDev.apk");

        static void Build(string packageName, string productName, string apkName)
        {
            EnsureScene();

            string previousPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string previousProduct = PlayerSettings.productName;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, packageName);
            PlayerSettings.productName = productName;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // No "Made with Unity" splash. Unity 6 Personal is allowed to turn it off
            // (docs/LICENSING_REVENUE.md §2.12) — the attribution line only becomes
            // required if the game ever shows a credits screen. Set here rather than left
            // to the editor UI so a headless build can never quietly ship it again.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            string outPath = Path.Combine(RepoRoot, "builds", apkName);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

            try
            {
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
                    return;
                }
                Debug.Log($"Build OK: {report.summary.outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
            }
            finally
            {
                // A dev-flavoured package name must not stick to the project after the build.
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, previousPackage);
                PlayerSettings.productName = previousProduct;
            }
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
