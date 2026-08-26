using System;
using System.Collections.Generic;
using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// The cosmetics store, reached from the result screen (T-020). Code-first UGUI like
    /// RunHud/ModeSelectMenu (CLAUDE.md rule 1). It lists the catalog with live lock state,
    /// equips owned skins, opens the dashboard-configured RevenueCat paywall for anything
    /// locked (gating the call site - the paywall itself takes no entitlement argument,
    /// REVENUECAT_PLAN §3.3), and carries the visible Restore button judges need (§6.3).
    ///
    /// Fail-open: with the store unavailable everything shows locked, every tap completes
    /// with a message, and CLOSE always works - the player is never stuck (rule 3's shape).
    public sealed class StorePanel : MonoBehaviour
    {
        static readonly Color TextColor = new Color(0.94f, 0.96f, 1f);
        static readonly Color DimColor = new Color(0.04f, 0.05f, 0.09f, 0.88f);
        static readonly Color PanelColor = new Color(0.11f, 0.13f, 0.20f, 0.98f);
        static readonly Color AccentColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color RowColor = new Color(0.18f, 0.21f, 0.30f);
        static readonly Color OwnedColor = new Color(0.24f, 0.42f, 0.28f);
        static readonly Color StatusColor = new Color(0.72f, 0.76f, 0.85f);

        /// True while a store panel is on screen. RunSession checks it so the tap that opens
        /// the store (or taps inside it) can never double as the "tap anywhere to restart"
        /// input - jump fires on the same TouchPhase.Ended as the button click.
        public static bool IsOpen { get; private set; }

        IStore _store;
        SkinService _skins;
        Text _status;
        readonly List<(string itemId, string packageId, Text state)> _rows =
            new List<(string, string, Text)>();
        Text _passState;
        readonly Dictionary<string, string> _prices = new Dictionary<string, string>();
        bool _busy;

        static Font _font;

        public static StorePanel Show(IStore store, SkinService skins)
        {
            var go = new GameObject("StorePanel");
            var panel = go.AddComponent<StorePanel>();
            IsOpen = true;
            panel._store = store;
            panel._skins = skins;
            panel.Build();
            store.EntitlementsChanged += panel.RefreshRows;
            panel.RefreshRows();
            panel.FetchPrices();
            return panel;
        }

        void OnDestroy()
        {
            IsOpen = false;
            if (_store != null) _store.EntitlementsChanged -= RefreshRows;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above HUD and result screen

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var dim = new GameObject("Dim");
            var dimRect = dim.AddComponent<RectTransform>();
            dim.transform.SetParent(transform, false);
            Stretch(dimRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.AddComponent<Image>().color = DimColor;

            var card = new GameObject("Card");
            var cardRect = card.AddComponent<RectTransform>();
            card.transform.SetParent(transform, false);
            Stretch(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cardRect.sizeDelta = new Vector2(880f, 1280f);
            card.AddComponent<Image>().color = PanelColor;

            CreateText("Title", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -120f), new Vector2(-24f, -40f),
                62, TextAnchor.UpperCenter, TextColor).text = "COSMETICS";

            // Skin rows. Each is one tap: equip when owned, open the paywall when not.
            float y = -170f;
            foreach (var item in CosmeticCatalog.Items)
            {
                string packageId = PackageForItem(item);
                _rows.Add((item.Id, packageId, BuildRow(card.transform, ref y,
                    item.DisplayName.ToUpperInvariant(), SkinService.ToColor(item.BodyColor),
                    () => OnSkinTapped(item.Id, packageId))));
            }

            // The pass is not a skin: it has no equip action, only owned/locked state.
            // Its reward ladder ships with T-025; selling it before the ladder exists is
            // fine for Test Store verification but the row says what it is today.
            _passState = BuildRow(card.transform, ref y, "SEASON 1 PASS", AccentColor,
                OnPassTapped);
            CreateText("PassNote", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(48f, y - 50f), new Vector2(-48f, y),
                28, TextAnchor.UpperLeft, StatusColor).text = "reward ladder arrives with the Season 1 update";
            y -= 70f;

            BuildButton(card.transform, ref y, "RESTORE PURCHASES", RowColor, OnRestore);

            _status = CreateText("Status", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, y - 130f), new Vector2(-32f, y - 8f),
                30, TextAnchor.UpperCenter, StatusColor);

            var close = new GameObject("Close");
            var closeRect = close.AddComponent<RectTransform>();
            close.transform.SetParent(card.transform, false);
            Stretch(closeRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            closeRect.anchoredPosition = new Vector2(0f, 100f);
            closeRect.sizeDelta = new Vector2(480f, 120f);
            var closeImage = close.AddComponent<Image>();
            closeImage.color = AccentColor;
            var closeButton = close.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(() => Destroy(gameObject));
            CreateText("Label", close.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                48, TextAnchor.MiddleCenter, new Color(0.08f, 0.06f, 0.04f)).text = "CLOSE";
        }

        /// A row: colour swatch + name on the left, live state on the right, whole row tappable.
        Text BuildRow(Transform parent, ref float y, string label, Color swatch, Action onTap)
        {
            var go = new GameObject(label);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, y - 130f), new Vector2(-32f, y));

            var image = go.AddComponent<Image>();
            image.color = RowColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onTap());

            var swatchGo = new GameObject("Swatch");
            var swatchRect = swatchGo.AddComponent<RectTransform>();
            swatchGo.transform.SetParent(go.transform, false);
            Stretch(swatchRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            swatchRect.anchoredPosition = new Vector2(70f, 0f);
            swatchRect.sizeDelta = new Vector2(70f, 70f);
            swatchGo.AddComponent<Image>().color = swatch;

            CreateText("Name", go.transform,
                new Vector2(0f, 0f), new Vector2(0.55f, 1f),
                new Vector2(130f, 0f), new Vector2(0f, 0f),
                42, TextAnchor.MiddleLeft, TextColor).text = label;

            var state = CreateText("State", go.transform,
                new Vector2(0.45f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(-28f, 0f),
                34, TextAnchor.MiddleRight, StatusColor);

            y -= 150f;
            return state;
        }

        void BuildButton(Transform parent, ref float y, string label, Color color, Action onTap)
        {
            var go = new GameObject(label);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            Stretch(rect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, y - 110f), new Vector2(-32f, y));

            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onTap());

            CreateText("Label", go.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                40, TextAnchor.MiddleCenter, TextColor).text = label;

            y -= 130f;
        }

        static string PackageForItem(CosmeticItem item)
        {
            // Package identifiers as configured in offering `default` (COSMETICS_CATALOG §2).
            if (item.Rule.Kind != UnlockKind.Entitlement) return null;
            if (item.Rule.EntitlementId == Entitlements.SkinEmber) return "skin_ember";
            if (item.Rule.EntitlementId == Entitlements.SkinFrost) return "skin_frost";
            return null;
        }

        void OnSkinTapped(string itemId, string packageId)
        {
            if (_busy) return;
            if (_skins.IsUnlocked(itemId))
            {
                _skins.Equip(itemId);
                RefreshRows();
                return;
            }
            OpenPaywall();
        }

        void OnPassTapped()
        {
            if (_busy) return;
            if (Entitlements.Has(_store.ActiveEntitlements, Entitlements.Season1)) return;
            OpenPaywall();
        }

        /// Only ever called for something the player does not own - that is the whole gate.
        void OpenPaywall()
        {
            _busy = true;
            _status.text = "opening store…";
            _store.PresentPaywall(outcome =>
            {
                _busy = false;
                _status.text = Describe(outcome, "purchase");
                RefreshRows();
            });
        }

        void OnRestore()
        {
            if (_busy) return;
            _busy = true;
            _status.text = "restoring…";
            _store.Restore(outcome =>
            {
                _busy = false;
                _status.text = outcome.Succeeded ? "purchases restored" : Describe(outcome, "restore");
                RefreshRows();
            });
        }

        static string Describe(PurchaseOutcome outcome, string what)
        {
            switch (outcome.Status)
            {
                case PurchaseStatus.Purchased: return "purchase complete!";
                case PurchaseStatus.Restored: return "purchases restored";
                case PurchaseStatus.AlreadyOwned: return "already owned - unlocked";
                case PurchaseStatus.Cancelled: return string.Empty;
                default:
                    return what + " failed - you can keep playing\n" +
                           (outcome.Error != null ? outcome.Error.Message : string.Empty);
            }
        }

        void RefreshRows()
        {
            if (this == null || _status == null) return;

            foreach (var (itemId, packageId, state) in _rows)
            {
                if (_skins.EquippedId == itemId && _skins.IsUnlocked(itemId))
                {
                    state.text = "EQUIPPED";
                    state.color = AccentColor;
                }
                else if (_skins.IsUnlocked(itemId))
                {
                    state.text = "TAP TO EQUIP";
                    state.color = TextColor;
                }
                else
                {
                    state.text = Price(packageId);
                    state.color = StatusColor;
                }
                state.transform.parent.GetComponent<Image>().color =
                    _skins.IsUnlocked(itemId) ? OwnedColor : RowColor;
            }

            bool passOwned = Entitlements.Has(_store.ActiveEntitlements, Entitlements.Season1);
            _passState.text = passOwned ? "OWNED" : Price("season1_pass");
            _passState.color = passOwned ? AccentColor : StatusColor;
            _passState.transform.parent.GetComponent<Image>().color = passOwned ? OwnedColor : RowColor;

            if (!_store.IsReady)
                _status.text = "store unavailable right now - you can keep playing";
        }

        string Price(string packageId) =>
            packageId != null && _prices.TryGetValue(packageId, out var price) ? price : "UNLOCK";

        void FetchPrices()
        {
            _store.FetchOffers((offers, error) =>
            {
                if (this == null || _status == null || offers == null) return;
                foreach (var offer in offers)
                    if (!string.IsNullOrEmpty(offer.Id) && !string.IsNullOrEmpty(offer.LocalizedPrice))
                        _prices[offer.Id] = offer.LocalizedPrice;
                RefreshRows();
            });
        }

        // ---- same UGUI plumbing as RunHud/ModeSelectMenu; third screen, still duplicated -
        // extracting the shared helper is a cleanup for a quiet moment, not this change. ----

        static Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);

            var text = go.AddComponent<Text>();
            text.font = PanelFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static Font PanelFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }
}
