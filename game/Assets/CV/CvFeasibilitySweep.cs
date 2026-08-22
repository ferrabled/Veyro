using System;
using System.Diagnostics;
using System.Globalization;
using MotionRunner.Pose;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace MotionRunner.Cv
{
    /// T-010b: the feasibility sweep behind OPEN_QUESTIONS 7. T-010 established that BlazePose on
    /// this device misses the 30 ms gate by 4x; this measures every untested lever that could
    /// change that verdict, in one run, so the park/invest/drop call is made on numbers:
    ///
    ///   H1  a 36x smaller model (BlazeFace short-range) instead of the pose pipeline
    ///   H2  weight quantization (fp16 / uint8) — the docs say it is a size lever, not a speed
    ///       lever, so a null result here is confirmation, not failure
    ///   H3  the GPUPixel backend, never tried (CPU already beat GPUCompute 2.5x)
    ///   H4  what actually matters for playability: inference running *while the app renders at
    ///       a 60 FPS cap* — achieved Hz, frame-time p95, hitch count — whole-schedule vs
    ///       ScheduleIterable slicing
    ///   H6  the no-ML floor: frame-diff centroid on a 160x120 buffer, plus the GPU
    ///       downsample+readback needed to feed it
    ///
    /// Everything is body-independent (see CvBenchmark) and logs one greppable "[CV] FEAS" line
    /// per measurement as it completes, so a mid-run crash still leaves partial data in logcat.
    public static class CvFeasibilitySweep
    {
        const string Tag = "[CV] FEAS";

        public static async Awaitable RunAsync(ModelAsset detector, ModelAsset lite, ModelAsset face)
        {
            int previousTargetFps = Application.targetFrameRate;
            var total = Stopwatch.StartNew();
            Log($"{Tag} BEGIN {SystemInfo.deviceModel} / {SystemInfo.graphicsDeviceName} / " +
                $"{SystemInfo.graphicsDeviceType} / {SystemInfo.processorType} x{SystemInfo.processorCount}");

            try
            {
                // ---- Latency matrix (H1/H3): uncapped frame rate, same method as CvBenchmark,
                // so the numbers sit in the same table as T-010's. Cheapest and most decisive first.
                //
                // Two whole config families are deliberately absent, both measured 22 Aug:
                //  - Quantization (H2): ModelQuantizer 2.6.1 throws on every one of these models
                //    (face+u8 NullReferenceException; lite+fp16/u8 KeyNotFoundException '362').
                //  - detector on CPU: allocating the CPU worker for the 15 MB detector OOM-killed
                //    the whole process, twice, with 5 GB free (ApplicationExitInfo LOW_MEMORY,
                //    importance=100). The detector must never run on the CPU backend.
                Application.targetFrameRate = -1;

                await Lat("face", face, BackendType.CPU, null, 5, 20);
                await LatSync("face", face, BackendType.CPU, 5, 30);
                await Lat("face", face, BackendType.GPUCompute, null, 5, 20);
                await Lat("face", face, BackendType.GPUPixel, null, 5, 20);

                await LatSync("lite", lite, BackendType.CPU, 3, 12);
                Log($"{Tag} LAT model=lite backend=GPUPixel quant=fp32 SKIPPED " +
                    "(measured 22 Aug: med=137.9 raw=461.1 — worst backend for this model)");
                Log($"{Tag} LAT model=detector backend=CPU SKIPPED (OOM-killed the process 2/2 runs)");

                // ---- H6: the no-ML floor.
                CentroidBench();
                await ReadbackBenchAsync();

                // ---- H4: live runs under a 60 FPS cap — what the game would actually feel.
                Application.targetFrameRate = 60;

                await Live("lite", lite, BackendType.CPU, null, LiveMode.Whole, 6f);
                await Live("lite", lite, BackendType.GPUCompute, null, LiveMode.Whole, 6f);
                await Live("lite", lite, BackendType.GPUCompute, null, LiveMode.Sliced, 6f);
                await Live("face", face, BackendType.CPU, null, LiveMode.Whole, 6f);
                await Live("face", face, BackendType.CPU, null, LiveMode.Sync, 6f);
                await Live("face", face, BackendType.GPUCompute, null, LiveMode.Whole, 6f);

                // ---- Order/thermal check: the same measurement T-010 ran first, repeated after
                // ~2 minutes of sustained load. Explains (or bounds) the 118-vs-170 ms anomaly.
                Application.targetFrameRate = -1;
                await Lat("lite", lite, BackendType.GPUCompute, null, 5, 20, " tag=end-repeat");
            }
            finally
            {
                Application.targetFrameRate = previousTargetFps;
            }

            Log($"{Tag} END total={total.Elapsed.TotalSeconds:F0}s");
        }

        // ---------------------------------------------------------------- latency matrix

        static async Awaitable Lat(string name, ModelAsset asset, BackendType backend,
            QuantizationType? quant, int warmup, int iterations, string tag = "")
        {
            string id = $"{Tag} LAT model={name} backend={backend} quant={QuantLabel(quant)}{tag}";
            if (asset == null)
            {
                Log($"{id} SKIPPED asset-missing");
                return;
            }

            try
            {
                Model model = ModelLoader.Load(asset);
                if (quant.HasValue) ModelQuantizer.QuantizeWeights(quant.Value, ref model);
                using Tensor<float> input = MakeInput(model);
                CvBenchmark.Result r =
                    await CvBenchmark.MeasureAsync(name, model, backend, input, warmup, iterations);
                Log(string.Format(CultureInfo.InvariantCulture,
                    "{0} med={1:F1} p95={2:F1} raw={3:F1} n={4}",
                    id, r.LatencyMedianMs, r.LatencyP95Ms, r.ThroughputMsPerInference, r.Iterations));
            }
            catch (Exception e)
            {
                Log($"{id} FAILED {e.GetType().Name}: {FirstLine(e.Message)}");
            }
        }

        /// Same measurement, but with the *blocking* readback. On the CPU backend the tensor is
        /// already in CPU memory, so this is the true cost of "schedule, wait for the jobs, read
        /// the result" with no frame-boundary quantization — the number the async medians hide
        /// (T-010's lite/CPU read 67.9 ms because 43.6 ms of jobs spans 3 frame ticks and the
        /// await resumes on the 4th; the model itself never cost 67.9).
        static async Awaitable LatSync(string name, ModelAsset asset, BackendType backend,
            int warmup, int iterations)
        {
            string id = $"{Tag} LAT model={name} backend={backend} quant=fp32 mode=sync";
            if (asset == null)
            {
                Log($"{id} SKIPPED asset-missing");
                return;
            }

            try
            {
                Model model = ModelLoader.Load(asset);
                using var worker = new Worker(model, backend);
                using Tensor<float> input = MakeInput(model);
                var stats = new LatencyStats(Math.Max(iterations, 1));
                var watch = new Stopwatch();

                for (int i = 0; i < warmup + iterations; i++)
                {
                    watch.Restart();
                    worker.Schedule(input);
                    using (Tensor _ = worker.PeekOutput(0).ReadbackAndClone()) { }
                    watch.Stop();
                    if (i >= warmup) stats.Add(watch.Elapsed.TotalMilliseconds);
                    if (i % 4 == 3) await Awaitable.NextFrameAsync();
                }

                Log(string.Format(CultureInfo.InvariantCulture,
                    "{0} med={1:F1} p95={2:F1} n={3}",
                    id, stats.Median(), stats.Percentile(95d), stats.Count));
            }
            catch (Exception e)
            {
                Log($"{id} FAILED {e.GetType().Name}: {FirstLine(e.Message)}");
            }
        }

        // ---------------------------------------------------------------- live runs (H4)

        enum LiveMode
        {
            /// Schedule everything at once, await the async readback.
            Whole,
            /// Spread per-layer dispatch over frames via ScheduleIterable (~3 ms per frame).
            Sliced,
            /// Schedule + blocking readback inside a single frame, one inference per frame.
            /// Only sane on the CPU backend, where the readback is a job sync, not a GPU stall.
            Sync
        }

        /// One model running continuously while the app renders under a 60 FPS cap. A concurrent
        /// frame sampler records what every frame cost, which is the number that decides whether
        /// the game underneath stays playable. sched_med is main-thread time spent inside
        /// Schedule()/MoveNext() per inference — the part the game pays directly.
        static async Awaitable Live(string name, ModelAsset asset, BackendType backend,
            QuantizationType? quant, LiveMode mode, float seconds)
        {
            string id = $"{Tag} LIVE model={name} backend={backend} quant={QuantLabel(quant)} " +
                        $"mode={mode.ToString().ToLowerInvariant()}";
            if (asset == null)
            {
                Log($"{id} SKIPPED asset-missing");
                return;
            }

            try
            {
                Model model = ModelLoader.Load(asset);
                if (quant.HasValue) ModelQuantizer.QuantizeWeights(quant.Value, ref model);
                using var worker = new Worker(model, backend);
                using Tensor<float> input = MakeInput(model);

                for (int i = 0; i < 3; i++)
                {
                    worker.Schedule(input);
                    using Tensor _ = await worker.PeekOutput(0).ReadbackAndCloneAsync();
                }

                var infMs = new LatencyStats(512);
                var schedMs = new LatencyStats(512);
                var sampler = new FrameSampler();
                Awaitable samplerRun = sampler.Run();

                var elapsed = Stopwatch.StartNew();
                var watch = new Stopwatch();
                var mainThread = new Stopwatch();
                int inferences = 0;
                int movesPerInference = 0;

                while (elapsed.Elapsed.TotalSeconds < seconds)
                {
                    watch.Restart();
                    if (mode == LiveMode.Sync)
                    {
                        mainThread.Restart();
                        worker.Schedule(input);
                        using (Tensor _ = worker.PeekOutput(0).ReadbackAndClone()) { }
                        mainThread.Stop();
                        watch.Stop();
                        infMs.Add(watch.Elapsed.TotalMilliseconds);
                        schedMs.Add(mainThread.Elapsed.TotalMilliseconds);
                        inferences++;
                        await Awaitable.NextFrameAsync();
                        continue;
                    }
                    if (mode == LiveMode.Whole)
                    {
                        mainThread.Restart();
                        worker.Schedule(input);
                        mainThread.Stop();
                    }
                    else
                    {
                        // Spread the per-layer dispatch over frames: at most ~3 ms of MoveNext
                        // per frame, so rendering gets the rest of the budget.
                        mainThread.Reset();
                        System.Collections.IEnumerator it = worker.ScheduleIterable(input);
                        double frameSpent = 0;
                        int moves = 0;
                        while (true)
                        {
                            mainThread.Start();
                            double before = mainThread.Elapsed.TotalMilliseconds;
                            bool more = it.MoveNext();
                            mainThread.Stop();
                            if (!more) break;
                            moves++;
                            frameSpent += mainThread.Elapsed.TotalMilliseconds - before;
                            if (frameSpent > 3.0)
                            {
                                await Awaitable.NextFrameAsync();
                                frameSpent = 0;
                            }
                        }
                        movesPerInference = moves;
                    }

                    using (Tensor _ = await worker.PeekOutput(0).ReadbackAndCloneAsync()) { }
                    watch.Stop();
                    infMs.Add(watch.Elapsed.TotalMilliseconds);
                    schedMs.Add(mainThread.Elapsed.TotalMilliseconds);
                    inferences++;
                }

                sampler.Stop();
                await samplerRun;

                double hz = inferences / elapsed.Elapsed.TotalSeconds;
                Log(string.Format(CultureInfo.InvariantCulture,
                    "{0} hz={1:F1} inf_med={2:F1} inf_p95={3:F1} sched_med={4:F2} " +
                    "frame_med={5:F1} frame_p95={6:F1} over20ms={7}/{8} n={9}{10}",
                    id, hz, infMs.Median(), infMs.Percentile(95d), schedMs.Median(),
                    sampler.FrameMs.Median(), sampler.FrameMs.Percentile(95d),
                    sampler.Over20, sampler.Frames, inferences,
                    mode == LiveMode.Sliced ? " moves=" + movesPerInference : ""));
            }
            catch (Exception e)
            {
                Log($"{id} FAILED {e.GetType().Name}: {FirstLine(e.Message)}");
            }
        }

        sealed class FrameSampler
        {
            public readonly LatencyStats FrameMs = new LatencyStats(1024);
            public int Over20;
            public int Frames;
            bool _running = true;

            public void Stop() => _running = false;

            public async Awaitable Run()
            {
                while (_running)
                {
                    await Awaitable.NextFrameAsync();
                    double ms = Time.unscaledDeltaTime * 1000d;
                    FrameMs.Add(ms);
                    Frames++;
                    if (ms > 20d) Over20++;
                }
            }
        }

        // ---------------------------------------------------------------- no-ML floor (H6)

        /// Frame differencing on a 160x120 luma buffer: abs-diff, threshold, centroid, upper-half
        /// motion sum — the entire per-frame CPU cost of the "which side is the body on, and did
        /// it move up sharply" scheme. Random buffers are the worst case (every pixel over the
        /// threshold pays the centroid adds).
        static void CentroidBench()
        {
            const int w = 160, h = 120;
            var a = new byte[w * h];
            var b = new byte[w * h];
            var rng = new System.Random(12345);
            rng.NextBytes(a);
            rng.NextBytes(b);

            var stats = new LatencyStats(64);
            var watch = new Stopwatch();
            long sink = 0;

            for (int iter = 0; iter < 50; iter++)
            {
                watch.Restart();
                long sumX = 0, sumY = 0, count = 0, upperMotion = 0;
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int d = a[row + x] - b[row + x];
                        if (d < 0) d = -d;
                        if (d > 24)
                        {
                            sumX += x;
                            sumY += y;
                            count++;
                            if (y < h / 2) upperMotion += d;
                        }
                    }
                }
                watch.Stop();
                stats.Add(watch.Elapsed.TotalMilliseconds);
                sink += sumX + sumY + count + upperMotion;
            }

            Log(string.Format(CultureInfo.InvariantCulture,
                "{0} MISC centroid_160x120 med={1:F3} p95={2:F3} n={3} sink={4}",
                Tag, stats.Median(), stats.Percentile(95d), stats.Count, sink & 0xFF));
        }

        /// The other half of H6's cost: getting pixels to the CPU. Blit 640x480 -> 160x120, then a
        /// synchronous AsyncGPUReadback (WaitForCompletion), which is the *worst case* — the async
        /// path costs near-zero main-thread time at one frame of added latency.
        static async Awaitable ReadbackBenchAsync()
        {
            RenderTexture src = RenderTexture.GetTemporary(640, 480, 0, RenderTextureFormat.ARGB32);
            RenderTexture dst = RenderTexture.GetTemporary(160, 120, 0, RenderTextureFormat.ARGB32);
            try
            {
                var stats = new LatencyStats(64);
                var watch = new Stopwatch();
                for (int i = 0; i < 40; i++)
                {
                    watch.Restart();
                    Graphics.Blit(src, dst);
                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(dst, 0, TextureFormat.RGBA32);
                    request.WaitForCompletion();
                    watch.Stop();
                    if (i >= 8) stats.Add(watch.Elapsed.TotalMilliseconds);
                    if (i % 8 == 0) await Awaitable.NextFrameAsync();
                }

                Log(string.Format(CultureInfo.InvariantCulture,
                    "{0} MISC blit+readback_160x120_sync med={1:F2} p95={2:F2} n={3}",
                    Tag, stats.Median(), stats.Percentile(95d), stats.Count));
            }
            catch (Exception e)
            {
                Log($"{Tag} MISC blit+readback FAILED {e.GetType().Name}: {FirstLine(e.Message)}");
            }
            finally
            {
                RenderTexture.ReleaseTemporary(src);
                RenderTexture.ReleaseTemporary(dst);
            }
        }

        // ---------------------------------------------------------------- helpers

        static Tensor<float> MakeInput(Model model)
        {
            DynamicTensorShape dynamicShape = model.inputs[0].shape;
            TensorShape shape = dynamicShape.IsStatic()
                ? dynamicShape.ToTensorShape()
                : new TensorShape(1, 256, 256, 3);
            return new Tensor<float>(shape);
        }

        static string QuantLabel(QuantizationType? quant) =>
            quant == null ? "fp32" : quant == QuantizationType.Float16 ? "fp16" : "u8";

        static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int i = text.IndexOf('\n');
            return i < 0 ? text : text.Substring(0, i);
        }

        static void Log(string line) => Debug.Log(line);
    }
}
