namespace MotionRunner.Commerce
{
    public enum PurchaseStatus
    {
        Purchased,
        Restored,
        Cancelled,
        AlreadyOwned,
        Failed
    }

    /// A failure has to carry why, or store diagnostics are impossible - so this is a
    /// status plus an optional error, not a bare enum (REVENUECAT_PLAN §3.2).
    public readonly struct PurchaseOutcome
    {
        public PurchaseStatus Status { get; }

        /// Non-null only when Status == Failed.
        public StoreError Error { get; }

        public bool Succeeded => Status is PurchaseStatus.Purchased
                                        or PurchaseStatus.Restored
                                        or PurchaseStatus.AlreadyOwned;

        PurchaseOutcome(PurchaseStatus status, StoreError error)
        {
            Status = status;
            Error = error;
        }

        public static PurchaseOutcome Purchased() => new PurchaseOutcome(PurchaseStatus.Purchased, null);
        public static PurchaseOutcome Restored() => new PurchaseOutcome(PurchaseStatus.Restored, null);
        public static PurchaseOutcome Cancelled() => new PurchaseOutcome(PurchaseStatus.Cancelled, null);
        public static PurchaseOutcome AlreadyOwned() => new PurchaseOutcome(PurchaseStatus.AlreadyOwned, null);
        public static PurchaseOutcome Failed(StoreError error) =>
            new PurchaseOutcome(PurchaseStatus.Failed, error ?? new StoreError("unknown", "unknown store error"));
    }
}
