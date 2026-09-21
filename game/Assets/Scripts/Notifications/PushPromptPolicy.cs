namespace MotionRunner.Notifications
{
    /// A cached registration and an arriving observer event take the same once-only path.
    public sealed class PushPromptPolicy
    {
        public const string SeenKey = "veyro.push.offer.seen.v1";
        public const string EnabledKey = "veyro.push.enabled.v1";
        public bool Seen { get; private set; }

        public PushPromptPolicy(bool seen) => Seen = seen;

        public bool TryOffer(PushStatus status, bool safeMenu, bool completedDaily, bool development)
        {
            if (Seen || !safeMenu || !status.Initialized || !status.Registered ||
                (!development && !completedDaily)) return false;
            Seen = true;
            return true;
        }
    }
}
