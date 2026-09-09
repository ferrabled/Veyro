using UnityEngine;

namespace MotionRunner.Core
{
    /// Which build the player is holding. Exists for one screen line — the version under the
    /// menu title — and one rule: that line is a DEVELOPMENT-ONLY affordance. The Play build
    /// shows nothing.
    ///
    /// `Debug.isDebugBuild` is the gate rather than a define of our own, because it is true for
    /// exactly the two things "development build" means here and nothing else: the Editor, and a
    /// player built with BuildOptions.Development (BuildScript.BuildAndroidDev). The .aab that
    /// goes to Play, and the keyless release APK, are both non-development, so neither can ever
    /// show it — there is no define to forget to clear.
    ///
    /// The version string itself is Application.version, i.e. PlayerSettings.bundleVersion, which
    /// BuildScript rewrites for the duration of a development build into
    /// `1.0.0-dev.20260902-1422.5aeab74` (semver + build time + git short SHA) and restores
    /// afterwards. That same string is the APK's android:versionName, so
    /// `adb shell dumpsys package com.ferrabled.veyro.run | grep versionName` answers "which build
    /// is on this phone?" without launching it.
    public static class BuildInfo
    {
        /// Semver + build stamp for a dev APK; the plain marketing version everywhere else.
        public static string Version => Application.version;

        /// Whether the version line should be on screen at all.
        public static bool IsDevelopment => Debug.isDebugBuild;

        /// What that line says. In the Editor the version is whatever ProjectSettings holds (no
        /// build ran, so there is no stamp), which would otherwise read like a release build.
        public static string VersionLabel
        {
            get
            {
                string label = "v" + Version;
#if UNITY_EDITOR
                label += "  ·  editor";
#endif
                return label;
            }
        }
    }
}
