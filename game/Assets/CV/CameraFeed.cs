using System;
using MotionRunner.Pose;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace MotionRunner.Cv
{
    /// The front camera, straightened.
    ///
    /// Exposes one thing to the rest of the CV stack: an upright RenderTexture. The rotation and
    /// mirroring that Android front cameras report per-device are dealt with here, once, using the
    /// affine from the unit-tested MotionRunner.Pose.FrameOrientation — so the detector, the
    /// landmarker crop, the preview and the gesture geometry all read one coordinate space, and
    /// the overlay cannot drift out of alignment with the picture underneath it.
    ///
    /// The device-reported rotation is a starting guess, not gospel. Beyond per-device camera
    /// quirks, the compute kernel that does the straightening writes through a UAV and reads
    /// through a sampler, and those two disagree about which corner is the origin differently on
    /// GLES and Vulkan. Rather than encode a per-API rule that is wrong on the next backend,
    /// SetOrientation lets OrientationProbe settle it empirically against the one oracle that
    /// matters: whether BlazePose can find a body.
    ///
    /// T-011 owns making this robust (device rotation mid-run, camera loss, one-euro smoothing);
    /// T-010 needs it correct enough that BlazePose sees an upright body at all.
    public sealed class CameraFeed : IDisposable
    {
        /// CLAUDE.md rule 7: feed the model from a 640x480 request, not the sensor's full
        /// resolution. The driver may hand back something close rather than exact.
        public const int RequestWidth = 640;
        public const int RequestHeight = 480;
        public const int RequestFps = 30;

        WebCamTexture _webcam;
        RenderTexture _upright;
        FrameOrientation.Result _orientation;

        int _rotation;
        bool _flip;
        bool _orientationSet;
        int _builtRotation = int.MinValue;
        bool _builtFlip;
        int _builtWidth;
        int _builtHeight;

        public bool MirrorForSelfie { get; set; } = true;

        /// Null until the driver has delivered a first frame.
        public RenderTexture UprightTexture => _upright;

        public string DeviceName { get; private set; }
        public bool IsFrontFacing { get; private set; }
        public int SourceWidth => _webcam != null ? _webcam.width : 0;
        public int SourceHeight => _webcam != null ? _webcam.height : 0;
        public int ReportedRotation => _webcam != null ? _webcam.videoRotationAngle : 0;
        public bool ReportedVerticallyMirrored => _webcam != null && _webcam.videoVerticallyMirrored;

        public int Rotation => _rotation;
        public bool VerticallyFlipped => _flip;

        public static bool HasPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
            return true;
#endif
        }

        public static void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera);
#endif
        }

        /// Starts the front camera, falling back to whatever camera exists. Returns false when the
        /// device reports no cameras at all, which on Android also happens while the permission
        /// dialog is still up — callers should retry rather than give up.
        public bool TryStart()
        {
            if (_webcam != null) return true;

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0) return false;

            int chosen = 0;
            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing) continue;
                chosen = i;
                break;
            }

            DeviceName = devices[chosen].name;
            IsFrontFacing = devices[chosen].isFrontFacing;

            _webcam = new WebCamTexture(DeviceName, RequestWidth, RequestHeight, RequestFps);
            _webcam.Play();
            return true;
        }

        /// True once the driver has delivered a usable frame. Width and height read as 16x16 until
        /// then, so nothing about orientation can be decided before this returns true.
        public bool HasFrame =>
            _webcam != null && _webcam.width > 16 && _webcam.height > 16;

        public void UseDeviceReportedOrientation() =>
            SetOrientation(ReportedRotation, ReportedVerticallyMirrored);

        public void SetOrientation(int rotationDegrees, bool verticallyFlipped)
        {
            _rotation = rotationDegrees;
            _flip = verticallyFlipped;
            _orientationSet = true;
        }

        /// Straightens this frame into UprightTexture. Returns false when no new frame arrived.
        public bool TryUpdate()
        {
            if (!HasFrame || !_webcam.didUpdateThisFrame) return false;
            if (!_orientationSet) UseDeviceReportedOrientation();

            EnsureTarget();
            BlazeAffine.SampleImageUpright(_webcam, _upright, _orientation.SourceUvFromUpright);
            return true;
        }

        /// Re-straightens the frame already in the driver's buffer, without waiting for a new one.
        /// The orientation probe needs this: comparing candidates against different frames would
        /// score the subject's fidgeting rather than the orientation.
        public void Restraighten()
        {
            if (!HasFrame) return;
            if (!_orientationSet) UseDeviceReportedOrientation();

            EnsureTarget();
            BlazeAffine.SampleImageUpright(_webcam, _upright, _orientation.SourceUvFromUpright);
        }

        void EnsureTarget()
        {
            bool unchanged = _rotation == _builtRotation
                             && _flip == _builtFlip
                             && _webcam.width == _builtWidth
                             && _webcam.height == _builtHeight
                             && _upright != null;
            if (unchanged) return;

            _builtRotation = _rotation;
            _builtFlip = _flip;
            _builtWidth = _webcam.width;
            _builtHeight = _webcam.height;

            _orientation = FrameOrientation.ForCamera(
                _webcam.width, _webcam.height, _rotation, _flip,
                MirrorForSelfie && IsFrontFacing);

            if (_upright != null && (_upright.width != _orientation.Width ||
                                     _upright.height != _orientation.Height))
            {
                _upright.Release();
                UnityEngine.Object.Destroy(_upright);
                _upright = null;
            }

            if (_upright == null)
            {
                // ReadWrite.Linear means "no sRGB conversion either way", which keeps the frame
                // byte-identical through the straightening pass in both colour spaces; the encode
                // BlazePose needs happens once, in the ImageSample kernel.
                _upright = new RenderTexture(_orientation.Width, _orientation.Height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "CvUprightFrame",
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _upright.Create();
            }
        }

        public string Describe() =>
            $"'{DeviceName}' front={IsFrontFacing} source={SourceWidth}x{SourceHeight} " +
            $"reported(rot={ReportedRotation},vflip={ReportedVerticallyMirrored}) " +
            $"using(rot={_rotation},vflip={_flip}) " +
            $"upright={(_upright != null ? _upright.width + "x" + _upright.height : "-")}";

        public void Dispose()
        {
            if (_webcam != null)
            {
                _webcam.Stop();
                UnityEngine.Object.Destroy(_webcam);
                _webcam = null;
            }

            if (_upright != null)
            {
                _upright.Release();
                UnityEngine.Object.Destroy(_upright);
                _upright = null;
            }
        }
    }
}
