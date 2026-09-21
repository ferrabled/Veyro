#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using MotionRunner.Menu;
using MotionRunner.Gameplay;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace MotionRunner.Art
{
    /// Local development evidence. Timings separate active work from frame-presentation waits.
    public sealed class ArtPerformanceProbe : MonoBehaviour
    {
        readonly float[] _frames=new float[300];
        readonly FrameTiming[] _timing=new FrameTiming[1];
        int _count,_timings;
        double _cpu,_gpu,_wait;
        float _warmup=8;
        string _surface;
        RunSession _session;
        void Start() => _session=FindFirstObjectByType<RunSession>();
        string Configuration => $"target={Application.targetFrameRate} refresh={Screen.currentResolution.refreshRateRatio.value:F2} " +
            $"interval={OnDemandRendering.renderFrameInterval} effective={OnDemandRendering.effectiveRenderFrameRate} vsync={QualitySettings.vSyncCount}";
        void OnApplicationFocus(bool focused) { Debug.Log($"[ParkArtFocus] focus={focused} {Configuration}"); Reset(); }
        void OnApplicationPause(bool paused) { Debug.Log($"[ParkArtFocus] paused={paused} {Configuration}"); Reset(); }
        void Reset() { _count=0;_timings=0;_cpu=0;_gpu=0;_wait=0; }
        void Update()
        {
            string surface=MainMenu.IsOpen ? MainMenu.PerformanceState :
                _session!=null && _session.IsRunning ? "running" : "results/transition";
            if(surface!=_surface) { _surface=surface;Reset(); }
            if(Time.timeScale<=0) { Reset();return; }
            if(_warmup>0) { _warmup-=Time.unscaledDeltaTime;return; }
            FrameTimingManager.CaptureFrameTimings();
            if(FrameTimingManager.GetLatestTimings(1,_timing)>0)
            {
                _cpu+=_timing[0].cpuMainThreadFrameTime;_gpu+=_timing[0].gpuFrameTime;
                _wait+=_timing[0].cpuMainThreadPresentWaitTime;_timings++;
            }
            _frames[_count++]=Time.unscaledDeltaTime*1000;
            if(_count<_frames.Length)return;
            Array.Sort(_frames);int slow=0;foreach(float ms in _frames)if(ms>33.34f)slow++;
            int n=Math.Max(1,_timings);
            Debug.Log($"[ParkArtPerf] surface={_surface} frames={_frames.Length} p50={_frames[_frames.Length/2]:F1}ms p95={_frames[_frames.Length*95/100]:F1}ms over33ms={slow} " +
                $"cpu={_cpu/n:F2}ms gpu={_gpu/n:F2}ms presentWait={_wait/n:F2}ms timingSamples={_timings} {Configuration} " +
                $"mono={Profiler.GetMonoUsedSizeLong()/1048576f:F1}MB allocated={Profiler.GetTotalAllocatedMemoryLong()/1048576f:F1}MB");
            Reset();
        }
    }
}
#endif
