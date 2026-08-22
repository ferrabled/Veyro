using System;

namespace MotionRunner.Pose
{
    /// Works out which way up the camera frame actually is by asking the detector.
    ///
    /// The device tells us a rotation and a mirror flag, and it is usually right, but three things
    /// can each put the frame a quarter or a half turn out: per-device front-camera quirks, the
    /// graphics API disagreeing about which corner a compute-shader UAV writes from, and the
    /// sampler's own uv origin. Encoding a rule for each is how the CV track loses the days that
    /// handoff 8.2 warns about, and the rule would still be wrong on the next backend.
    ///
    /// So instead: try the candidates, keep the one BlazePose scores highest. A sideways body
    /// scores near zero and an upright one scores near one, which makes the detector a far more
    /// reliable oracle than any amount of platform documentation. The probe doubles as the warm-up
    /// handoff 8.2 asks for, so it costs no extra time.
    ///
    /// Engine-free and clock-free: the caller runs the inference and hands the score back.
    public sealed class OrientationProbe
    {
        public readonly struct Candidate : IEquatable<Candidate>
        {
            public readonly int RotationDegrees;
            public readonly bool VerticallyFlipped;

            public Candidate(int rotationDegrees, bool verticallyFlipped)
            {
                RotationDegrees = rotationDegrees;
                VerticallyFlipped = verticallyFlipped;
            }

            public bool Equals(Candidate other) =>
                RotationDegrees == other.RotationDegrees &&
                VerticallyFlipped == other.VerticallyFlipped;

            public override bool Equals(object obj) => obj is Candidate other && Equals(other);

            public override int GetHashCode() =>
                RotationDegrees * 2 + (VerticallyFlipped ? 1 : 0);

            public override string ToString() =>
                "rot=" + RotationDegrees + ",vflip=" + VerticallyFlipped;
        }

        /// A detector score this high means the body is upright; no other candidate can beat it by
        /// enough to be worth another eight inferences, so the probe stops early. In the common
        /// case — the device told the truth — that makes the whole probe three inferences.
        public const float GoodEnoughScore = 0.9f;

        /// Below this, the winning candidate did not actually see a body — it won a contest
        /// between eight bad options on detector noise. Distinguishing the two matters: the first
        /// on-device run settled on a rotation the device had not reported, purely because nobody
        /// was in frame and 0.147 happened to beat 0.146. An unconfident probe must be discarded
        /// in favour of the device's own report and retried, never trusted.
        public const float MinimumConfidence = 0.5f;

        readonly Candidate[] _candidates;
        readonly int _samplesPerCandidate;

        int _index;
        int _samplesTaken;
        float _scoreTotal;

        public OrientationProbe(int reportedRotation, bool reportedFlip, int samplesPerCandidate = 3)
        {
            if (samplesPerCandidate <= 0)
                throw new ArgumentOutOfRangeException(nameof(samplesPerCandidate));

            _samplesPerCandidate = samplesPerCandidate;
            _candidates = BuildCandidates(reportedRotation, reportedFlip);
            Best = _candidates[0];
            BestScore = -1f;
        }

        public Candidate Best { get; private set; }
        public float BestScore { get; private set; }
        public bool IsComplete { get; private set; }

        /// The probe finished *and* the winner actually saw a body. Only then is Best worth
        /// preferring over what the device reported.
        public bool IsConfident => IsComplete && BestScore >= MinimumConfidence;

        /// The orientation to render the next probe frame with.
        public Candidate Current => _candidates[_index];

        public int CandidateCount => _candidates.Length;

        /// How many candidates have been reached, including the one in progress. Logged so the
        /// STATUS entry can say whether the device report was believed or overruled.
        public int CandidatesTried => _index + 1;

        /// Feeds back the detector score for Current. Averages over samplesPerCandidate frames
        /// before moving on, because a single frame can catch the subject mid-blur.
        public void Submit(float score)
        {
            if (IsComplete) return;

            _scoreTotal += score;
            _samplesTaken++;
            if (_samplesTaken < _samplesPerCandidate) return;

            float mean = _scoreTotal / _samplesTaken;
            if (mean > BestScore)
            {
                BestScore = mean;
                Best = _candidates[_index];
            }

            _scoreTotal = 0f;
            _samplesTaken = 0;

            if (BestScore >= GoodEnoughScore || _index == _candidates.Length - 1)
            {
                IsComplete = true;
                return;
            }

            _index++;
        }

        /// The reported orientation first — it is usually right, and being right on the first try
        /// is what keeps the probe cheap. Then its vertical mirror, which is the single most
        /// likely correction, then the remaining quarter turns.
        static Candidate[] BuildCandidates(int reportedRotation, bool reportedFlip)
        {
            int baseRotation = Normalize(reportedRotation);
            var ordered = new Candidate[8];
            int n = 0;

            ordered[n++] = new Candidate(baseRotation, reportedFlip);
            ordered[n++] = new Candidate(baseRotation, !reportedFlip);

            for (int quarter = 1; quarter < 4; quarter++)
            {
                int rotation = Normalize(baseRotation + 90 * quarter);
                ordered[n++] = new Candidate(rotation, reportedFlip);
                ordered[n++] = new Candidate(rotation, !reportedFlip);
            }

            return ordered;
        }

        static int Normalize(int rotationDegrees)
        {
            if (rotationDegrees % 90 != 0)
                throw new ArgumentOutOfRangeException(nameof(rotationDegrees),
                    "camera rotation is always a multiple of 90, got " + rotationDegrees);

            int degrees = rotationDegrees % 360;
            return degrees < 0 ? degrees + 360 : degrees;
        }
    }
}
