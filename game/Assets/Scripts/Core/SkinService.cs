using MotionRunner.Commerce;
using UnityEngine;

namespace MotionRunner.Core
{
    /// Owns which skin is equipped and paints it onto the runner. Selection persists in
    /// PlayerPrefs; ownership is re-checked on every apply, so a revoked entitlement (refund)
    /// falls back to the default runner instead of keeping paid content, and a Restore or an
    /// out-of-app promo redemption recolors the runner the moment EntitlementsChanged fires.
    public sealed class SkinService
    {
        const string EquippedKey = "veyro.skin";

        /// T-020 ships no XP: level 1 is the starting level (COSMETICS_CATALOG §3) and no
        /// catalog row uses SeasonLevel yet. T-025 replaces this constant with ladder state.
        const int SeasonLevel = 1;

        readonly IStore _store;
        readonly Renderer _renderer;

        public SkinService(IStore store, Renderer runnerRenderer)
        {
            _store = store;
            _renderer = runnerRenderer;
            _store.EntitlementsChanged += Apply;
            Apply();
        }

        public string EquippedId => PlayerPrefs.GetString(EquippedKey, CosmeticCatalog.DefaultSkinId);

        public bool IsUnlocked(string itemId) =>
            CosmeticCatalog.IsUnlocked(itemId, _store.ActiveEntitlements, SeasonLevel);

        /// False when the item is unknown or not owned - equipping never grants.
        public bool Equip(string itemId)
        {
            if (!IsUnlocked(itemId)) return false;
            PlayerPrefs.SetString(EquippedKey, itemId);
            PlayerPrefs.Save();
            Apply();
            return true;
        }

        /// Paints the equipped skin if it is (still) owned, the default otherwise.
        public void Apply()
        {
            var item = CosmeticCatalog.Find(EquippedId);
            if (item == null || !item.Rule.IsUnlocked(_store.ActiveEntitlements, SeasonLevel))
                item = CosmeticCatalog.Find(CosmeticCatalog.DefaultSkinId);
            if (_renderer != null)
                _renderer.sharedMaterial = RuntimeMaterials.Shared(ToColor(item.BodyColor));
        }

        public static Color ToColor(CosmeticColor c) => new Color(c.R, c.G, c.B);
    }
}
