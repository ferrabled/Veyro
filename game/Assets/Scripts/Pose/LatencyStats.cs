using System;

namespace MotionRunner.Pose
{
    /// Rolling latency window over inference timings, and the gate that decides whether camera
    /// mode is offered at all.
    ///
    /// Clock-free, exactly like DailySeed: the caller measures and passes milliseconds in. That
    /// keeps the statistics testable headlessly and stops a Stopwatch from leaking into the
    /// engine-free assembly.
    ///
    /// Median rather than mean because inference latency on a phone is a long-tailed
    /// distribution — one thermal stall or one GPU contention spike drags a mean far enough to
    /// fail a device that plays fine.
    public sealed class LatencyStats
    {
        /// Handoff 8.2 low-end-phone policy, restated by T-013: enable camera mode only when the
        /// median landmarker inference sits under this. Above it the option is hidden, and the
        /// player keeps gyro/touch, losing nothing.
        public const double GateMs = 30.0;

        /// Handoff 8.2 step 1: "run ~20 warm inferences, measure median latency".
        public const int DefaultWarmupSamples = 20;

        readonly double[] _samples;
        readonly double[] _sorted;
        int _count;
        int _next;

        public LatencyStats(int capacity = 120)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _samples = new double[capacity];
            _sorted = new double[capacity];
        }

        public int Capacity => _samples.Length;

        /// Number of samples currently in the window, saturating at Capacity.
        public int Count => _count;

        public double Last { get; private set; }

        public void Add(double milliseconds)
        {
            if (milliseconds < 0d) throw new ArgumentOutOfRangeException(nameof(milliseconds));

            Last = milliseconds;
            _samples[_next] = milliseconds;
            _next = (_next + 1) % _samples.Length;
            if (_count < _samples.Length) _count++;
        }

        public void Clear()
        {
            _count = 0;
            _next = 0;
            Last = 0d;
        }

        /// Median of the window; 0 when empty. Even counts average the two middle samples, so a
        /// two-sample window of 10 and 20 reads 15 rather than silently picking one.
        public double Median() => Percentile(50d);

        /// Linear-interpolated percentile, p in [0,100]. p95 is the number that says whether the
        /// occasional stall is bad enough to be felt as a stutter.
        public double Percentile(double p)
        {
            if (p < 0d || p > 100d) throw new ArgumentOutOfRangeException(nameof(p));
            if (_count == 0) return 0d;

            Array.Copy(_samples, _sorted, _count);
            Array.Sort(_sorted, 0, _count);

            if (_count == 1) return _sorted[0];

            double rank = (p / 100d) * (_count - 1);
            int lower = (int)Math.Floor(rank);
            int upper = (int)Math.Ceiling(rank);
            if (lower == upper) return _sorted[lower];
            return _sorted[lower] + (rank - lower) * (_sorted[upper] - _sorted[lower]);
        }

        public double Mean()
        {
            if (_count == 0) return 0d;
            double total = 0d;
            for (int i = 0; i < _count; i++) total += _samples[i];
            return total / _count;
        }

        public double Min()
        {
            if (_count == 0) return 0d;
            double min = _samples[0];
            for (int i = 1; i < _count; i++) if (_samples[i] < min) min = _samples[i];
            return min;
        }

        public double Max()
        {
            if (_count == 0) return 0d;
            double max = _samples[0];
            for (int i = 1; i < _count; i++) if (_samples[i] > max) max = _samples[i];
            return max;
        }

        /// T-013's startup gate. Undecided until the warm-up window is full — an empty window must
        /// never read as "fast enough", or a cold start would enable camera mode on any device.
        public bool TryGate(out bool passes, int minimumSamples = DefaultWarmupSamples,
            double gateMs = GateMs)
        {
            if (_count < minimumSamples)
            {
                passes = false;
                return false;
            }

            passes = Median() < gateMs;
            return true;
        }
    }
}
