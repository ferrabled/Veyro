using System.Collections.Generic;

namespace MotionRunner.Commerce
{
    public enum CosmeticSlot
    {
        /// Character identity is independent of accessory slots. Append new slots to preserve saves.
        Skin, Body, Trail, Headwear, Aura, CrashFx, Back
    }

    public enum SeasonTrack
    {
        Free,
        Pass
    }

    public enum UnlockKind
    {
        Default,
        Entitlement,
        SeasonLevel
    }

    /// How a cosmetic item unlocks: owned by everyone, owned via a store entitlement, or
    /// reached on a Season track (COSMETICS_CATALOG §5). SeasonLevel exists as a rule shape
    /// for T-025; T-020 ships no season items and no XP.
    public readonly struct UnlockRule
    {
        public UnlockKind Kind { get; }

        /// Entitlement rules only.
        public string EntitlementId { get; }

        /// SeasonLevel rules only.
        public SeasonTrack Track { get; }
        public int Level { get; }

        UnlockRule(UnlockKind kind, string entitlementId, SeasonTrack track, int level)
        {
            Kind = kind;
            EntitlementId = entitlementId;
            Track = track;
            Level = level;
        }

        public static UnlockRule Default() =>
            new UnlockRule(UnlockKind.Default, null, SeasonTrack.Free, 0);

        public static UnlockRule Entitlement(string entitlementId) =>
            new UnlockRule(UnlockKind.Entitlement, entitlementId, SeasonTrack.Free, 0);

        public static UnlockRule SeasonLevel(SeasonTrack track, int level) =>
            new UnlockRule(UnlockKind.SeasonLevel, null, track, level);

        /// Pure and fail-open: an empty entitlement set (store not ready) grants nothing
        /// beyond defaults and never throws. Pass-track items need the level AND season1.
        public bool IsUnlocked(IReadOnlyCollection<string> activeEntitlements, int seasonLevel)
        {
            switch (Kind)
            {
                case UnlockKind.Default:
                    return true;
                case UnlockKind.Entitlement:
                    return Entitlements.Has(activeEntitlements, EntitlementId);
                case UnlockKind.SeasonLevel:
                    if (seasonLevel < Level) return false;
                    return Track == SeasonTrack.Free ||
                           Entitlements.Has(activeEntitlements, Entitlements.Season1);
                default:
                    return false;
            }
        }
    }

    /// Engine-free colour so this assembly stays noEngineReferences; the Unity side converts.
    public readonly struct CosmeticColor
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public CosmeticColor(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    public sealed class CosmeticItem
    {
        public string Id { get; }
        public string DisplayName { get; }
        public bool Legacy => Id=="sunset" || Id=="sky" || Id=="plum" || Id=="chrome";
        public CosmeticSlot Slot { get; }
        public UnlockRule Rule { get; }

        /// What the item changes in game. T-020: the runner body colour
        /// (GameBootstrap applies it via RuntimeMaterials); T-025 adds glow/trail/aura.
        public CosmeticColor BodyColor { get; }

        public CosmeticItem(string id, string displayName, CosmeticSlot slot, UnlockRule rule, CosmeticColor bodyColor)
        {
            Id = id;
            DisplayName = displayName;
            Slot = slot;
            Rule = rule;
            BodyColor = bodyColor;
        }
    }

    /// Item id -> what it changes in game, with its unlock rule. Pure data; the store never
    /// hears about items (money buys products, products grant entitlements, the app checks
    /// entitlements, items are game data - COSMETICS_CATALOG §1).
    public static class CosmeticCatalog
    {
        public const string DefaultSkinId = "runner";
        public const string EmberSkinId = "ember";
        public const string FrostSkinId = "frost";

        /// T-020 catalog: the default runner plus the two premium skins. The Season 1 ladder
        /// (10x2 items, SeasonLevel rules) lands with T-025 - same rows, more of them.
        public static readonly IReadOnlyList<CosmeticItem> Items = new[]
        {
            // Colour must match the original hard-coded runner tint, so a build without any
            // entitlement looks exactly like every build before T-020.
            new CosmeticItem(DefaultSkinId, "Runner", CosmeticSlot.Skin,
                UnlockRule.Default(), new CosmeticColor(1f, 0.55f, 0.15f)),

            // Ember/Frost as colour swaps for now; full presets (glow, trail, aura) are T-025.
            new CosmeticItem(EmberSkinId, "Ember", CosmeticSlot.Skin,
                UnlockRule.Entitlement(Entitlements.SkinEmber), new CosmeticColor(0.80f, 0.16f, 0.04f)),

            new CosmeticItem(FrostSkinId, "Frost", CosmeticSlot.Skin,
                UnlockRule.Entitlement(Entitlements.SkinFrost), new CosmeticColor(0.72f, 0.88f, 1f)),
            Reward("mint", "Mint", CosmeticSlot.Body, SeasonTrack.Free, 1, 0x62CFAB),
            Reward("white", "White", CosmeticSlot.Trail, SeasonTrack.Free, 2, 0xFFF9E9),
            Reward("sunset", "Sunset Orange", CosmeticSlot.Body, SeasonTrack.Free, 3, 0xF78353),
            Reward("sky", "Sky Blue", CosmeticSlot.Body, SeasonTrack.Free, 4, 0x65BBDD),
            Reward("cap", "Disc Cap", CosmeticSlot.Headwear, SeasonTrack.Free, 5, 0x267B79),
            Reward("plum", "Plum", CosmeticSlot.Body, SeasonTrack.Free, 6, 0x905A95),
            Reward("shadow", "Shadow", CosmeticSlot.Trail, SeasonTrack.Free, 7, 0x23545A),
            Reward("charcoal", "Charcoal", CosmeticSlot.Body, SeasonTrack.Free, 8, 0x33474C),
            Reward("confetti", "Confetti", CosmeticSlot.CrashFx, SeasonTrack.Free, 9, 0xEF3A8B),
            Reward("gold", "Matte Gold", CosmeticSlot.Body, SeasonTrack.Free, 10, 0xD8AC43),
            Reward("neon", "Neon Lime", CosmeticSlot.Body, SeasonTrack.Pass, 1, 0xBEED4C),
            Reward("cyan", "Cyan Pulse", CosmeticSlot.Trail, SeasonTrack.Pass, 2, 0x5AE2DE),
            Reward("tophat", "Top Hat", CosmeticSlot.Headwear, SeasonTrack.Pass, 3, 0x215C65),
            Reward("firefly", "Firefly", CosmeticSlot.Aura, SeasonTrack.Pass, 4, 0xFADE6B),
            Reward("chrome", "Chrome", CosmeticSlot.Body, SeasonTrack.Pass, 5, 0xD1E8E7),
            Reward("twin", "Twin", CosmeticSlot.Trail, SeasonTrack.Pass, 6, 0xEA599D),
            Reward("crown", "Crown", CosmeticSlot.Headwear, SeasonTrack.Pass, 7, 0xE4B83F),
            Reward("aurora", "Aurora", CosmeticSlot.Trail, SeasonTrack.Pass, 8, 0x68D6B7),
            Reward("comet", "Comet", CosmeticSlot.Aura, SeasonTrack.Pass, 9, 0xB9B4F5),
            Reward("prism", "Prism", CosmeticSlot.Skin, SeasonTrack.Pass, 10, 0xBAE4D4),
            Reward("quiver", "Wayfinder Quiver", CosmeticSlot.Back, SeasonTrack.Free, 3, 0xBC8354),
            Reward("spellbook", "Moonbound Tome", CosmeticSlot.Back, SeasonTrack.Free, 4, 0x656EA3),
            Reward("shield", "Sunshield", CosmeticSlot.Back, SeasonTrack.Free, 6, 0xD8AC43),
            Reward("wings", "Sky Wings", CosmeticSlot.Back, SeasonTrack.Pass, 5, 0xDDEEEB)
        };

        static CosmeticItem Reward(string id, string name, CosmeticSlot slot, SeasonTrack track, int level, int rgb) =>
            new CosmeticItem(id, name, slot, UnlockRule.SeasonLevel(track, level),
                new CosmeticColor(((rgb >> 16) & 255)/255f, ((rgb >> 8) & 255)/255f, (rgb & 255)/255f));

        public static CosmeticItem RewardAt(SeasonTrack track, int level)
        {
            foreach (var item in Items)
                if (!item.Legacy && item.Rule.Kind == UnlockKind.SeasonLevel && item.Rule.Track == track && item.Rule.Level == level)
                    return item;
            return null;
        }

        public static CosmeticItem Find(string id)
        {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].Id == id)
                    return Items[i];
            return null;
        }

        public static bool IsUnlocked(string id, IReadOnlyCollection<string> activeEntitlements, int seasonLevel)
        {
            var item = Find(id);
            return item != null && item.Rule.IsUnlocked(activeEntitlements, seasonLevel);
        }
    }
}
