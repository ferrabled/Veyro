using System;
using System.Collections.Generic;
using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// Pass-first storefront, followed by two distinct character cards. Live prices and ownership
    /// come from IStore. Characters purchase directly; the pass uses the configured paywall.
    /// Preview navigation never buys/equips, and leaving the shop stays possible during commerce.
    public sealed class StoreCatalogView
    {
        readonly IStore _store;
        readonly SkinService _skins;
        readonly Dictionary<string, string> _prices = new Dictionary<string, string>();
        readonly List<(string itemId, string packageId, Text state, Image row)> _rows =
            new List<(string, string, Text, Image)>();

        public Action ViewSeason;
        public Action<string> PreviewCharacter;
        Text _status;
        Text _passState;
        Image _passRow;
        bool _busy,_readyPricesRequested;
        const string Unavailable="store unavailable right now - you can keep playing";

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
            CosmeticUi.Text("Title",CosmeticUi.Rect("ShopHeading",slot,0.025f,0.975f,10,70),"MAKE IT YOUR RUN",43,MenuTheme.Text);
            var content=CosmeticUi.Scroll(slot,94,out var scroll);scroll.viewport.GetComponent<Image>().color=Color.clear;
            content.sizeDelta=new Vector2(0,1608);
            var pass=CosmeticUi.Rect("SeasonPassCard",content,0.025f,0.975f,0,420);
            var passBackground=CosmeticUi.Surface(pass,MenuTheme.Text,32);
            passBackground.Gradient=true;passBackground.Bottom=MenuTheme.PassGradient;
            CosmeticUi.Text("SeasonTag",CosmeticUi.Rect("SeasonTagSlot",pass,0.035f,0.75f,20,38),"SEASON 1  /  WORLD RUNNER",24,MenuTheme.Owned);
            CosmeticUi.Text("SeasonTitle",CosmeticUi.Rect("SeasonTitleSlot",pass,0.035f,0.75f,60,62),"A WHOLE NEW LOOK",40,MenuTheme.OnAccent);
            CosmeticUi.Text("SeasonDetails",CosmeticUi.Rect("SeasonDetailsSlot",pass,0.035f,0.66f,128,76),"Earn XP. Collect 10 premium rewards.\nWings, headwear, effects & more.",26,MenuTheme.OnAccent);
            CosmeticUi.Thumbnail(CosmeticUi.Rect("PassWings",pass,0.63f,0.995f,32,252),"wings");
            CosmeticUi.Text("FreeTrack",CosmeticUi.Rect("FreeTrackSlot",pass,0.035f,0.975f,216,42),"10 free rewards for everyone · no gameplay advantage",22,MenuTheme.Owned);
            var buy=CosmeticUi.Pill(CosmeticUi.Rect("BuyPass",pass,0.035f,0.58f,294,85),"",OnPassTapped,MenuTheme.Accent,30);
            _passState=buy.GetComponentInChildren<Text>();_passRow=buy.image;
            CosmeticUi.Pill(CosmeticUi.Rect("ViewRewards",pass,0.60f,0.965f,294,85),"VIEW REWARDS",()=>ViewSeason?.Invoke(),MenuTheme.Card,24);
            CosmeticUi.Text("CharacterHeading",CosmeticUi.Rect("CharacterHeadingSlot",content,0.025f,0.975f,444,58),"MEET YOUR NEXT RUNNER",33,MenuTheme.Text);
            BuildCharacter(content,"ember",0.025f,0.50f);
            BuildCharacter(content,"frost",0.50f,0.975f);
            CosmeticUi.Text("Compatibility",CosmeticUi.Rect("CompatibilitySlot",content,0.025f,0.975f,1304,70),"Both characters wear your collected pass items.\nOne-time purchases. Yours to keep.",25,MenuTheme.Dim,TextAnchor.MiddleCenter);
            CosmeticUi.Pill(CosmeticUi.Rect("RestorePurchases",content,0.12f,0.88f,1394,76),"RESTORE PURCHASES",OnRestore,MenuTheme.Slot,27);
            _status=CosmeticUi.Text("Status",CosmeticUi.Rect("StatusSlot",content,0.025f,0.975f,1490,96),"",25,MenuTheme.Dim,TextAnchor.UpperCenter);

            _store.EntitlementsChanged += Refresh;
            Refresh();
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

        void BuildCharacter(Transform content,string id,float left,float right)
        {
            var item=CosmeticCatalog.Find(id);var package=PackageForItem(item);
            var card=CosmeticUi.Rect(id+"Card",content,left,right,514,764);
            var background=CosmeticUi.Surface(card,id=="ember" ? MenuTheme.EmberCard : MenuTheme.FrostCard,28);
            background.Gradient=true;background.Bottom=MenuTheme.Card;
            CosmeticUi.Text("Name",CosmeticUi.Rect("NameSlot",card,0.02f,0.98f,14,56),item.DisplayName.ToUpperInvariant(),38,MenuTheme.Text,TextAnchor.MiddleCenter);
            CosmeticUi.Text("CharacterType",CosmeticUi.Rect("TypeSlot",card,0.02f,0.98f,69,34),id=="ember" ? "FIREBOUND EXPLORER":"ICEBOUND MAGE",20,MenuTheme.Dim,TextAnchor.MiddleCenter);
            CosmeticUi.Thumbnail(CosmeticUi.Rect("CharacterPortrait",card,0.02f,0.98f,101,495),id);
            var buy=CosmeticUi.Pill(CosmeticUi.Rect("Buy"+id,card,0.035f,0.965f,610,70),"",()=>OnSkinTapped(id,package),MenuTheme.Text,28);
            _rows.Add((id,package,buy.GetComponentInChildren<Text>(),buy.image));
            CosmeticUi.Pill(CosmeticUi.Rect("Preview"+id,card,0.035f,0.965f,696,50),"TRY IN LOCKER",()=>PreviewCharacter?.Invoke(id),MenuTheme.ItemCard,23);
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
            if (Entitlements.Has(_store.ActiveEntitlements, Entitlements.Season1)){ViewSeason?.Invoke();return;}
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

            // A restore that reaches the store and finds nothing still SUCCEEDS - the SDK
            // completed, there was simply no receipt to re-derive from. Reporting "purchases
            // restored" there is a lie the player can act on, and it is exactly the path a
            // player takes after DELETE ONLINE PROFILE resets the store identity (T-009).
            // The adapter applies the customer info before invoking this callback, so
            // comparing the entitlement set across the call says what actually happened.
            var before = new HashSet<string>(_store.ActiveEntitlements);
            _store.Restore(outcome =>
            {
                if (!StillBuilt) return;
                _busy = false;
                _status.text = outcome.Succeeded
                    ? DescribeRestore(before, _store.ActiveEntitlements)
                    : Describe(outcome, "restore");
                Refresh();
            });
        }

        /// What a successful restore actually did. "Nothing to restore" is the honest answer
        /// when the store account owns nothing - it is not a failure, and it is not a
        /// restoration either.
        static string DescribeRestore(HashSet<string> before, IReadOnlyCollection<string> after)
        {
            if (after.Count > 0 && !before.SetEquals(after)) return "purchases restored";
            if (after.Count > 0) return "everything you own is already unlocked";
            return "nothing to restore on this account";
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
                    state.color = MenuTheme.OnAccent;
                }
                else if (_skins.IsUnlocked(itemId))
                {
                    state.text = "TAP TO EQUIP";
                    state.color = MenuTheme.OnAccent;
                }
                else
                {
                    state.text = Price(packageId);
                    state.color = MenuTheme.OnAccent;
                }
                row.color = _skins.IsUnlocked(itemId) ? MenuTheme.Accent : MenuTheme.Text;
            }

            bool passOwned = Entitlements.Has(_store.ActiveEntitlements, Entitlements.Season1);
            _passState.text = passOwned ? "PASS OWNED  >" : "GET PASS · "+Price("season1_pass");
            _passState.color = MenuTheme.OnAccent;
            _passRow.color = passOwned ? MenuTheme.Text : MenuTheme.Accent;

            if (!_store.IsReady)
            {
                _status.text=Unavailable;_readyPricesRequested=false;
            }
            else
            {
                if(_status.text==Unavailable)_status.text=string.Empty;
                // A shop built before SDK readiness gets fresh prices when readiness arrives.
                // Set before the callback: FakeStore and cached offers can complete synchronously.
                if(!_readyPricesRequested){_readyPricesRequested=true;FetchPrices();}
            }

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
