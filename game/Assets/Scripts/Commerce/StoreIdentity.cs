namespace MotionRunner.Commerce
{
    /// Serializes native login/logout operations. A completed stale login still changed the
    /// SDK's identity, so remember it for cleanup, but never publish its customer info.
    public sealed class StoreIdentity
    {
        public string Desired { get; private set; }
        public string Applied { get; private set; }
        public bool Busy { get; private set; }
        public bool ResetPending { get; private set; }
        int _generation;

        public void Identify(string userId)
        {
            if (string.IsNullOrEmpty(userId) || userId == Desired) return;
            Desired = userId;
            _generation++;
        }

        public void Reset()
        {
            if (ResetPending && Desired == null) return;
            Desired = null;
            ResetPending = true;
            _generation++;
        }

        public bool TryBegin(out string userId, out int generation)
        {
            userId = ResetPending ? null : Desired;
            generation = _generation;
            if (Busy || (!ResetPending && Desired == Applied)) return false;
            Busy = true;
            return true;
        }

        public bool Complete(string userId, int generation, bool succeeded)
        {
            Busy = false;
            if (!succeeded) return false;
            Applied = userId;
            if (userId == null) ResetPending = false;
            return generation == _generation && userId == Desired && !ResetPending;
        }

        public bool Settled => !Busy && !ResetPending && Applied == Desired;
    }
}
