using UnityEngine;

namespace MotionRunner.Cv
{
    /// Entry point for the T-010 spike build, and nothing else.
    ///
    /// The whole MotionRunner.Cv assembly is gated on VEYRO_CV_SPIKE via defineConstraints, and
    /// only CvSpikeBuild passes that define. This #if is therefore belt and braces rather than the
    /// primary gate — but it is the one a reader sees, so it stays.
    ///
    /// The assembly-level gate is not tidiness. Measured on 22 Aug: with the CV code compiled into
    /// the release build, Unity saw WebCamTexture in the assemblies and added CAMERA to the
    /// manifest of a game that does not use a camera, and the APK grew from 29.6 MB to 44.6 MB.
    /// The cost of the gate is that the editor no longer type-checks this code by default; a spike
    /// build is what compiles it, and it fails loudly.
    ///
    /// CLAUDE.md rule 3 stands: v1.0 ships with gyro+touch whatever this spike concludes.
    public static class CvSpikeBootstrap
    {
#if VEYRO_CV_SPIKE
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
#endif
        static void Boot()
        {
            var go = new GameObject("CvSpike");
            go.AddComponent<CvSpikeController>();
        }
    }
}
