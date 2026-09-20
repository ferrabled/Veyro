#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace MotionRunner.Art
{
    /// Development-build evidence only; no telemetry leaves the device.
    public sealed class ArtPerformanceProbe : MonoBehaviour
    {
        readonly float[] _frames = new float[600];
        int _count;
        float _warmup = 8;
        void Update()
        {
            if (Time.timeScale <= 0) { _count = 0; return; }
            if (_warmup > 0) { _warmup -= Time.unscaledDeltaTime; return; }
            _frames[_count++] = Time.unscaledDeltaTime * 1000;
            if (_count < _frames.Length) return;
            Array.Sort(_frames);
            int slow = 0;
            foreach (float ms in _frames) if (ms > 33.34f) slow++;
            Debug.Log($"[ParkArtPerf] frames=600 p50={_frames[300]:F1}ms p95={_frames[570]:F1}ms " +
                $"over33ms={slow} mono={Profiler.GetMonoUsedSizeLong()/1048576f:F1}MB " +
                $"allocated={Profiler.GetTotalAllocatedMemoryLong()/1048576f:F1}MB");
            _count = 0;
        }
    }
}
#endif
