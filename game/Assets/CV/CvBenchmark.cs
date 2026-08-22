using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using MotionRunner.Pose;
using Unity.InferenceEngine;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MotionRunner.Cv
{
    /// Measures what one inference costs, without needing anybody in front of the camera.
    ///
    /// Two numbers per model, because they answer different questions and the gap between them is
    /// itself a finding:
    ///
    ///   Latency    schedule -> await readback, one inference at a time. What a frame actually
    ///              pays when it needs the result before it can continue, and therefore the number
    ///              T-013's gate must use.
    ///   Throughput N inferences scheduled back to back with a single readback at the end,
    ///              divided by N. The GPU cost with per-await and frame-boundary overhead removed.
    ///
    /// If throughput is far below latency, the model is fast and the harness is stalling — worth
    /// engineering around. If they agree, the model is simply that expensive on this GPU, and no
    /// amount of pipelining will save camera mode.
    ///
    /// Body-independent on purpose. A convolutional net costs the same whatever the pixels show,
    /// and T-013 has to run this gate at startup, before the player has got into position.
    public static class CvBenchmark
    {
        public readonly struct Result
        {
            public readonly string Label;
            public readonly BackendType Backend;
            public readonly double LatencyMedianMs;
            public readonly double LatencyP95Ms;
            public readonly double ThroughputMsPerInference;
            public readonly int Iterations;

            public Result(string label, BackendType backend, double latencyMedian, double latencyP95,
                double throughput, int iterations)
            {
                Label = label;
                Backend = backend;
                LatencyMedianMs = latencyMedian;
                LatencyP95Ms = latencyP95;
                ThroughputMsPerInference = throughput;
                Iterations = iterations;
            }

            public override string ToString() => string.Format(CultureInfo.InvariantCulture,
                "{0,-14} {1,-10} latency_med={2,7:F1}ms p95={3,7:F1}ms throughput={4,7:F1}ms/inf n={5}",
                Label, Backend, LatencyMedianMs, LatencyP95Ms, ThroughputMsPerInference, Iterations);
        }

        /// <param name="warmup">Discarded. The first inferences compile compute kernels and
        /// allocate buffers, and run several times slower than steady state.</param>
        public static async Awaitable<Result> MeasureAsync(string label, Model model,
            BackendType backend, Tensor<float> input, int warmup = 5, int iterations = 20)
        {
            using var worker = new Worker(model, backend);
            var stats = new LatencyStats(Math.Max(iterations, 1));
            var watch = new Stopwatch();

            for (int i = 0; i < warmup; i++)
            {
                worker.Schedule(input);
                using Tensor _ = await worker.PeekOutput(0).ReadbackAndCloneAsync();
            }

            for (int i = 0; i < iterations; i++)
            {
                watch.Restart();
                worker.Schedule(input);
                using Tensor output = await worker.PeekOutput(0).ReadbackAndCloneAsync();
                watch.Stop();
                stats.Add(watch.Elapsed.TotalMilliseconds);
            }

            // Back to back, one readback at the end: the GPU queue stays full, so per-await and
            // frame-boundary costs are amortised away and what is left is the model itself.
            watch.Restart();
            for (int i = 0; i < iterations; i++) worker.Schedule(input);
            using (Tensor _ = await worker.PeekOutput(0).ReadbackAndCloneAsync()) { }
            watch.Stop();
            double throughput = watch.Elapsed.TotalMilliseconds / iterations;

            return new Result(label, backend, stats.Median(), stats.Percentile(95d), throughput,
                iterations);
        }

        /// The whole sweep, logged as one block so it can be pasted into STATUS.md.
        ///
        /// Uncapping the frame rate for the duration matters: with targetFrameRate at 60, an
        /// awaited readback resumes on the next frame tick, so every measurement would be rounded
        /// up to a multiple of 16.7 ms and the answer would be about Unity's pacing rather than
        /// about BlazePose.
        public static async Awaitable<Result[]> RunSweepAsync(ModelAsset detector, ModelAsset lite,
            ModelAsset full)
        {
            int previousTargetFps = Application.targetFrameRate;
            Application.targetFrameRate = -1;

            var results = new System.Collections.Generic.List<Result>(4);
            var report = new StringBuilder();
            report.AppendLine("[CV] BENCH " + SystemInfo.deviceModel + " / " +
                              SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType +
                              " / gate " + LatencyStats.GateMs + "ms");

            using var detectorInput = new Tensor<float>(new TensorShape(1, 224, 224, 3));
            using var landmarkerInput = new Tensor<float>(new TensorShape(1, 256, 256, 3));

            try
            {
                if (detector != null)
                    results.Add(await MeasureAsync("detector",
                        ModelLoader.Load(detector), BackendType.GPUCompute, detectorInput));

                if (lite != null)
                    results.Add(await MeasureAsync("landmark_lite",
                        ModelLoader.Load(lite), BackendType.GPUCompute, landmarkerInput));

                if (full != null)
                    results.Add(await MeasureAsync("landmark_full",
                        ModelLoader.Load(full), BackendType.GPUCompute, landmarkerInput));

                // One CPU data point, to tell "this GPU is slow at this model" apart from "the
                // GPUCompute path is broken". Few iterations: if it is slow, it is very slow.
                if (lite != null)
                    results.Add(await MeasureAsync("landmark_lite",
                        ModelLoader.Load(lite), BackendType.CPU, landmarkerInput, 1, 5));
            }
            catch (Exception e)
            {
                report.AppendLine("[CV] BENCH aborted: " + e.Message);
            }
            finally
            {
                Application.targetFrameRate = previousTargetFps;
            }

            foreach (Result r in results) report.AppendLine("[CV] BENCH " + r);
            Debug.Log(report.ToString().TrimEnd());
            return results.ToArray();
        }

        /// Two lines fit for the on-screen panel.
        public static string Summarize(Result[] results)
        {
            if (results == null || results.Length == 0) return "benchmark unavailable";

            var text = new StringBuilder();
            foreach (Result r in results)
                text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} {1}: {2:F0} ms latency, {3:F0} ms raw",
                    r.Label, r.Backend == BackendType.GPUCompute ? "gpu" : "cpu",
                    r.LatencyMedianMs, r.ThroughputMsPerInference));
            return text.ToString().TrimEnd();
        }
    }
}
