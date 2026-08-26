namespace MotionRunner.Commerce
{
    /// One purchasable package of the current offering, flattened to plain data.
    public readonly struct StoreOffer
    {
        /// Package identifier as configured in the offering: skin_ember / skin_frost / season1_pass.
        public string Id { get; }

        /// Store-localized display name.
        public string Title { get; }

        /// Store-localized price string, e.g. "2,99 €". Display-only; never parse it.
        public string LocalizedPrice { get; }

        /// The entitlement buying this grants (Entitlements.ForPackage).
        public string EntitlementId { get; }

        public StoreOffer(string id, string title, string localizedPrice, string entitlementId)
        {
            Id = id;
            Title = title;
            LocalizedPrice = localizedPrice;
            EntitlementId = entitlementId;
        }
    }
}
