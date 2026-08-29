using System.Collections.Generic;
using System.Linq;

namespace MotionRunner.Commerce
{
    /// The three entitlement ids, exactly as configured in the RevenueCat dashboard
    /// (docs/COSMETICS_CATALOG.md §2). One per product, deliberately no single "cosmetics"
    /// flag - packaging (a later bundle granting all three) stays a dashboard decision
    /// with zero app-code change.
    public static class Entitlements
    {
        public const string SkinEmber = "skin_ember";
        public const string SkinFrost = "skin_frost";
        public const string Season1 = "season1";

        public static readonly IReadOnlyList<string> All = new[] { SkinEmber, SkinFrost, Season1 };

        public static bool Has(IReadOnlyCollection<string> active, string id) =>
            active != null && active.Contains(id);

        /// Which entitlement a package of offering `default` grants. Package identifiers are
        /// the custom ids typed into the dashboard (COSMETICS_CATALOG §2); the grant itself
        /// happens server-side - this map only labels offers for the UI. Null for a package
        /// this build does not know (a later dashboard addition must not crash an old build).
        public static string ForPackage(string packageId) => packageId switch
        {
            "skin_ember" => SkinEmber,
            "skin_frost" => SkinFrost,
            "season1_pass" => Season1,
            _ => null
        };

        /// The inverse: which package sells a given entitlement. The direct-purchase path
        /// needs it - a locked catalog item knows only its entitlement, and IStore.Purchase
        /// takes a package id. Null for an entitlement nothing in this offering sells on its
        /// own (a future bundle-only unlock), which the call site must treat as "not for sale"
        /// rather than as an error.
        public static string PackageFor(string entitlementId) => entitlementId switch
        {
            SkinEmber => "skin_ember",
            SkinFrost => "skin_frost",
            Season1 => "season1_pass",
            _ => null
        };
    }
}
