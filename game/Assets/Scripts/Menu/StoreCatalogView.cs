using System;
using System.Collections.Generic;
using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The cosmetics catalog, as a view that builds into whatever rect it is handed (T-020). It
    /// used to be the whole StorePanel - a modal over the result screen - and everything that made
    /// that panel correct is still here; only the frame around it changed. The store is now a tab
    /// on the main menu, so the result screen's SHOP button leaves the run and lands here instead
    /// of stacking an overlay on top of a finished run.
    ///
    /// Two purchase paths, split by product (owner call, 29 Aug):
    ///   * the two skins buy DIRECTLY - one tap raises the native Google Play sheet, because
    ///     a €2.99 colour swap the player just tapped needs no sales page in front of it;
    ///   * the Season 1 pass opens the dashboard-configured RevenueCat paywall, which is the
    ///     offering that paywall was built for and stays revisable without a store release
    ///     (REVENUECAT_PLAN §5).
    /// Both are gated the same way: a row is only ever purchasable when the player does not
    /// already own it - the paywall itself takes no entitlement argument (§3.3), and entitlements
    /// stay the single source of truth for "owned".
    ///
    /// Fail-open: with the store unavailable everything shows locked, every tap completes with a
    /// message, and the tab bar always works - the player is never stuck (CLAUDE.md rule 3).
    public sealed class StoreCatalogView
    {
        readonly IStore _store;
        readonly SkinService _skins;
        readonly Dictionary<string, string> _prices = new Dictionary<string, string>();
        readonly List<(string itemId, string packageId, Text state, Image row)> _rows =
            new List<(string, string, Text, Image)>();

        Text _status;
        Text _passState;
        Image _passRow;
        bool _busy;

        /// Raised whenever entitlements may have changed, so the cards on other tabs (the season
        /// pass banner) can catch up without polling the store themselves.
        public event Action Changed;

        public StoreCatalogView(IStore store, SkinService skins)
        {
            _store = store;
            _skins = skins;
        }

        public void Build(RectTransform slot)
        {
            float pad = MenuTheme.SidePadding;

            RuntimeUi.Label("Title", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, -84f), new Vector2(-pad, -12f),
                52, TextAnchor.MiddleLeft, MenuTheme.Text).text = "COSMETICS";

            // Skin rows. Each is one tap: equip when owned, straight to the Google Play sheet when
            // not - the paywall is the pass's path, not theirs.
            float y = -104f;
            foreach (var item in CosmeticCatalog.Items)
            {
                string packageId = PackageForItem(item);
                var (state, row) = BuildRow(slot, ref y, item.DisplayName.ToUpperInvariant(),
                    SkinService.ToColor(item.BodyColor), () => OnSkinTapped(item.Id, packageId));
                _rows.Add((item.Id, packageId, state, row));
            }

            // The pass is not a skin: it has no equip action, only owned/locked state. Its reward
            // ladder ships with T-025; selling it before the ladder exists is fine for Test Store
            // verification but the row says what it is today.
            var pass = BuildRow(slot, ref y, "SEASON 1 PASS", MenuTheme.Accent, OnPassTapped);
            _passState = pass.state;
            _passRow = pass.row;

            RuntimeUi.Label("PassNote", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad + 20f, y - 52f), new Vector2(-pad - 20f, y),
                28, TextAnchor.UpperLeft, MenuTheme.Faint).text =
                "reward ladder arrives with the Season 1 update";
            y -= 72f;

            BuildButton(slot, ref y, "RESTORE PURCHASES", OnRestore);

            _status = RuntimeUi.Label("Status", slot,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(pad, y - 140f), new Vector2(-pad, y - 12f),
                30, TextAnchor.UpperCenter, MenuTheme.Dim);

            _store.EntitlementsChanged += Refresh;
            Refresh();
            FetchPrices();
        }

        /// The store SDK outlives this view whenever the player leaves the tab with a purchase or
        /// a restore in flight - switching tabs stays live on purpose (rule 3: never trapped
        /// behind commerce) - so the callback lands on destroyed UI. Every callback checks this
        /// before touching anything Build made.
        bool StillBuilt => _status != null;

        public void Dispose()
        {
            if (_store != null) _store.EntitlementsChanged -= Refresh;
            _status = null;
        }

        /// A row: colour swatch + name on the left, live state on the right, whole row tappable.
        (Text state, Image row) BuildRow(RectTransform slot, ref float y, string label,
            Color swatch, Action onTap)
        {
            var go = RuntimeUi.Element(label, slot, out var rect);
            RuntimeUi.Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.SidePadding, y - 130f),
                new Vector2(-MenuTheme.SidePadding, y));

            var image = go.AddComponent<Image>();
            image.color = MenuTheme.Slot;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onTap());

            var swatchGo = RuntimeUi.Element("Swatch", go.transform, out var swatchRect);
            swatchRect.anchorMin = new Vector2(0f, 0.5f);
            swatchRect.anchorMax = new Vector2(0f, 0.5f);
            swatchRect.anchoredPosition = new Vector2(70f, 0f);
            swatchRect.sizeDelta = new Vector2(70f, 70f);
            swatchGo.AddComponent<Image>().color = swatch;

            RuntimeUi.Label("Name", go.transform,
                new Vector2(0f, 0f), new Vector2(0.55f, 1f),
                new Vector2(130f, 0f), new Vector2(0f, 0f),
                42, TextAnchor.MiddleLeft, MenuTheme.Text).text = label;

            var state = RuntimeUi.Label("State", go.transform,
                new Vector2(0.45f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(-28f, 0f),
                34, TextAnchor.MiddleRight, MenuTheme.Dim);

            y -= 150f;
            return (state, image);
        }

        void BuildButton(RectTransform slot, ref float y, string label, Action onTap)
        {
            var go = RuntimeUi.Element(label, slot, out var rect);
            RuntimeUi.Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(MenuTheme.SidePadding, y - 110f),
                new Vector2(-MenuTheme.SidePadding, y));

            var image = go.AddComponent<Image>();
            image.color = MenuTheme.Slot;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onTap());

            RuntimeUi.Label("Label", go.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 40, TextAnchor.MiddleCenter, MenuTheme.Text).text = label;

            y -= 130f;
        }

        /// Which package of offering `default` sells this item, or null for one nothing sells on
        /// its own (the free runner, and any later bundle-only unlock). The map itself lives in
        /// Commerce so it is EditMode-tested - the direct-purchase tap depends on it.
        static string PackageForItem(CosmeticItem item) =>
            item.Rule.Kind == UnlockKind.Entitlement
                ? Entitlements.PackageFor(item.Rule.EntitlementId)
                : null;

        void OnSkinTapped(string itemId, string packageId)
        {
            if (_busy) return;
            if (_skins.IsUnlocked(itemId))
            {
                _skins.Equip(itemId);
                Refresh();
                return;
            }
            BuySkin(itemId, packageId);
        }

        void OnPassTapped()
        {
            if (_busy) return;
            if (Entitlements.Has(_store.ActiveEntitlements, Entitlements.Season1)) return;
            OpenPaywall();
        }

        /// Skins go straight to the Google Play sheet. Only ever called for something the player
        /// does not own - that is the whole gate.
        void BuySkin(string itemId, string packageId)
        {
            if (packageId == null)
            {
                // A catalog item with no package behind it: locked, but nothing to sell.
                _status.text = "this one is not for sale yet";
                return;
            }

            _busy = true;
            _status.text = "opening Google Play…";
            _store.Purchase(packageId, outcome =>
            {
                // The tap said "I want this skin", so a purchase that lands wears it - even if the
                // player left the tab while the Play sheet was up, because SkinService outlives
                // this view. The entitlement is already applied by the time this runs, so Equip
                // succeeds; it silently does nothing if it somehow is not, because equipping never
                // grants.
                if (outcome.Succeeded) _skins.Equip(itemId);

                if (!StillBuilt) return;
                _busy = false;
                _status.text = Describe(outcome, "purchase");
                Refresh();
            });
        }

        /// The pass's path: the dashboard paywall. Only ever called for something the player does
        /// not own - that is the whole gate.
        void OpenPaywall()
        {
            _busy = true;
            _status.text = "opening store…";
            _store.PresentPaywall(outcome =>
            {
                if (!StillBuilt) return;
                _busy = false;
                _status.text = Describe(outcome, "purchase");
                Refresh();
            });
        }

        void OnRestore()
        {
            if (_busy) return;
            _busy = true;
            _status.text = "restoring…";
            _store.Restore(outcome =>
            {
                if (!StillBuilt) return;
                _busy = false;
                _status.text = outcome.Succeeded ? "purchases restored" : Describe(outcome, "restore");
                Refresh();
            });
        }

        static string Describe(PurchaseOutcome outcome, string what)
        {
            switch (outcome.Status)
            {
                case PurchaseStatus.Purchased: return "purchase complete!";
                case PurchaseStatus.Restored: return "purchases restored";
                case PurchaseStatus.AlreadyOwned: return "already owned - unlocked";
                // Backing out of the Play sheet says nothing - the rows are already right.
                case PurchaseStatus.Cancelled: return string.Empty;
                case PurchaseStatus.Pending:
                    return "payment pending with Google\nit unlocks by itself once they confirm";
                default:
                    return what + " failed - you can keep playing\n" +
                           (outcome.Error != null ? outcome.Error.Message : string.Empty);
            }
        }

        public void Refresh()
        {
            if (!StillBuilt) return;

            foreach (var (itemId, packageId, state, row) in _rows)
            {
                if (_skins.EquippedId == itemId && _skins.IsUnlocked(itemId))
                {
                    state.text = "EQUIPPED";
                    state.color = MenuTheme.Accent;
                }
                else if (_skins.IsUnlocked(itemId))
                {
                    state.text = "TAP TO EQUIP";
                    state.color = MenuTheme.Text;
                }
                else
                {
                    state.text = Price(packageId);
                    state.color = MenuTheme.Dim;
                }
                row.color = _skins.IsUnlocked(itemId) ? MenuTheme.Owned : MenuTheme.Slot;
            }

            bool passOwned = Entitlements.Has(_store.ActiveEntitlements, Entitlements.Season1);
            _passState.text = passOwned ? "OWNED" : Price("season1_pass");
            _passState.color = passOwned ? MenuTheme.Accent : MenuTheme.Dim;
            _passRow.color = passOwned ? MenuTheme.Owned : MenuTheme.Slot;

            if (!_store.IsReady)
                _status.text = "store unavailable right now - you can keep playing";

            Changed?.Invoke();
        }

        string Price(string packageId) =>
            packageId != null && _prices.TryGetValue(packageId, out var price) ? price : "UNLOCK";

        void FetchPrices()
        {
            _store.FetchOffers((offers, error) =>
            {
                if (!StillBuilt || offers == null) return;
                foreach (var offer in offers)
                    if (!string.IsNullOrEmpty(offer.Id) && !string.IsNullOrEmpty(offer.LocalizedPrice))
                        _prices[offer.Id] = offer.LocalizedPrice;
                Refresh();
            });
        }
    }
}
