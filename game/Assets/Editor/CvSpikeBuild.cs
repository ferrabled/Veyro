using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotionRunner.EditorTools
{
    /// Builds the T-010 CV spike as a *separate* APK:
    ///   Unity -batchmode -quit -projectPath game -buildTarget Android \
    ///         -executeMethod MotionRunner.EditorTools.CvSpikeBuild.BuildAndroid
    ///
    /// Separate on purpose, on three axes:
    ///   - its own package name, so it installs alongside the game and cannot replace the build
    ///     the owner is playing;
    ///   - its own scripting define, passed only for this build. MotionRunner.Cv is gated on it by
    ///     defineConstraints, so the release build contains no CV code at all — measured on
    ///     22 Aug, leaving it compiled in cost a spurious CAMERA permission and 15 MB of APK;
    ///   - its own model staging, so the 20 MB of BlazePose weights live in Resources only while
    ///     this build runs and never inflate the release APK.
    ///
    /// CLAUDE.md rule 3: none of this may gate v1.0. Deleting this file and Assets/CV would leave
    /// the game exactly as it is.
    public static class CvSpikeBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PackageName = "com.ferrabled.veyro.cvspike";
        const string ProductName = "Veyro CV Spike";
        const string SpikeDefine = "VEYRO_CV_SPIKE";
        const string StagedDir = "Assets/CV/Resources/Cv";
        const string SourceDirName = "CvModels";

        static readonly string[] ModelFiles =
        {
            "pose_detection.onnx",
            "pose_landmarks_detector_lite.onnx",
            "pose_landmarks_detector_full.onnx",
            // T-010b (OPEN_QUESTIONS 7): BlazeFace short-range, the 36x-smaller H1 candidate.
            // From huggingface.co/unity/inference-engine-blaze-face; StageModels skips it if absent.
            "blaze_face_short_range.onnx"
        };

        // Committed spike assets the runtime loads via Resources.Load. They live in Data/ rather
        // than a Resources/ folder because Unity ships Resources content in EVERY build, gated
        // assembly or not — measured 22 Aug (T-010b): the release APK silently carried the anchor
        // table and the affine compute shader. Staged alongside the models, unstaged in finally.
        const string DataDir = "Assets/CV/Data";
        static readonly string[] DataFiles = { "anchors.csv", "ImageTransform.compute" };

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RepoRoot => Path.GetFullPath(Path.Combine(ProjectRoot, ".."));
        static string SourceDir => Path.Combine(ProjectRoot, SourceDirName);

        [MenuItem("Veyro/CV Spike/Stage BlazePose models")]
        public static void StageModels()
        {
            if (!Directory.Exists(SourceDir))
            {
                Debug.LogError($"[CV] {SourceDir} is missing. Fetch the models first:\n" +
                               "  curl -L -o game/CvModels/<file> " +
                               "https://huggingface.co/unity/inference-engine-blaze-pose/resolve/main/models/<file>");
                return;
            }

            Directory.CreateDirectory(Path.Combine(ProjectRoot, StagedDir));
            int staged = 0;

            foreach (string file in ModelFiles)
            {
                string src = Path.Combine(SourceDir, file);
                if (!File.Exists(src))
                {
                    Debug.LogWarning("[CV] model not found, skipping: " + src);
                    continue;
                }

                staged += StageFile(src, file);
            }

            foreach (string file in DataFiles)
            {
                string src = Path.Combine(ProjectRoot, DataDir, file);
                if (!File.Exists(src))
                {
                    // These are committed; missing means the checkout is broken, not optional.
                    Debug.LogError("[CV] committed spike asset missing: " + src);
                    continue;
                }

                staged += StageFile(src, file);
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CV] staged {staged} file(s) into {StagedDir}");
        }

        static int StageFile(string src, string file)
        {
            string dst = Path.Combine(ProjectRoot, StagedDir, file);
            if (!File.Exists(dst) || new FileInfo(src).Length != new FileInfo(dst).Length)
                File.Copy(src, dst, true);

            AssetDatabase.ImportAsset(StagedDir + "/" + file, ImportAssetOptions.ForceSynchronousImport);
            return 1;
        }

        [MenuItem("Veyro/CV Spike/Unstage BlazePose models")]
        public static void UnstageModels()
        {
            // The whole Resources tree under Assets/CV is build-time staging (see DataDir note) —
            // if any of it survives into the repo, the next release APK ships it. Delete it all.
            if (!AssetDatabase.IsValidFolder("Assets/CV/Resources")) return;
            AssetDatabase.DeleteAsset("Assets/CV/Resources");
            AssetDatabase.Refresh();
            Debug.Log($"[CV] unstaged {StagedDir}");
        }

        public static void BuildAndroid()
        {
            EnsureScene();
            StageModels();

            string previousPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string previousProduct = PlayerSettings.productName;

            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
                PlayerSettings.productName = ProductName;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SplashScreen.show = false;
                PlayerSettings.SplashScreen.showUnityLogo = false;

                string outPath = Path.Combine(RepoRoot, "builds", "VeyroCvSpike.apk");
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                    // Only this build gets the hook that starts the camera.
                    extraScriptingDefines = new[] { SpikeDefine }
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Debug.LogError($"[CV] spike build failed: {report.summary.result}");
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log($"[CV] spike build OK: {report.summary.outputPath} " +
                          $"({report.summary.totalSize / (1024 * 1024)} MB)");
            }
            finally
            {
                // Leaving the weights staged would put them in the next release APK; leaving the
                // package name would overwrite the game on the phone. Both must be undone even if
                // the build threw.
                UnstageModels();
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
