using System;
using System.Collections.Generic;
using MotionRunner.CameraInput;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Social;
using UnityEngine;

namespace MotionRunner.Menu
{
    /// The app's front screen: a header, three tabs, and whichever page is up. It replaces
    /// ModeSelectMenu, which was a single screen whose only question was tilt-or-camera; that
    /// question is now one card on the middle tab.
    ///
    /// The shape is Clash Royale's, for the reason that shape exists: a runner has three things a
    /// player comes back for - the run, the things to buy, and their own numbers - and a bottom
    /// bar with the primary action in the middle makes all three one thumb-reach apart, with no
    /// screen ever more than one tap from the run.
    ///
    /// This class owns only the frame: what is on screen, which tab it is, and the two things the
    /// rest of the game needs from a menu (a chosen control scheme, a request for the guide). It
    /// builds no cards and knows nothing about seasons, streaks or prices - see MenuPage.
    public sealed class MainMenu : MonoBehaviour
    {
        /// Above the HUD, matching what ModeSelectMenu used, so a run's canvases keep sorting the
        /// way they always did.
        const int SortingOrder = 100;

        /// The menu currently owning the screen, or null. Tracked as an instance rather than a
        /// bool so the flag cannot be cleared by the wrong menu: Destroy only lands at the end of
        /// the frame, so a bool set false in OnDestroy would be a bool the *outgoing* menu turns
        /// off after the next one has turned it on. Unity's destroyed-object null also means a
        /// menu that goes away without OnDestroy running still reads as closed.
        static MainMenu _current;

        /// True while the menu owns the screen. RunSession and RunFlow read it the way they used
        /// to read StorePanel.IsOpen: a tap that lands while the menu is coming up must never also
        /// be read as "restart the run behind it".
        public static bool IsOpen => _current != null;

        /// Raised once with the chosen scheme; the rig is non-null only for camera mode.
        public event Action<bool, FaceTrackingRig> Chosen;

        /// Raised by the profile tab's "how to play". RunFlow owns which screen is up, so the menu
        /// only announces the tap.
        public event Action GuideRequested;

        /// The store view, built once and shared: the shop tab hosts it, and the season pass card
        /// on the home tab listens to it. One view means one set of SDK callbacks in flight,
        /// whichever tab is up.
        public StoreCatalogView Catalog { get; private set; }

        /// The online profile seam, for the profile tab. May be a never-ready fake (no
        /// backend configured) - pages must treat that as "boards are sample data".
        public IProfileService Profile { get; private set; }

        public MenuTab Tab { get; private set; } = MenuTab.Run;

        /// True on the shop or the profile - the state back should undo by coming home rather than
        /// by falling through to Android.
        public bool IsAwayFromHome => Tab != MenuTab.Run;

        public bool IsStagingCamera => _home != null && _home.IsStagingCamera;

        readonly Dictionary<MenuTab, MenuPage> _pages = new Dictionary<MenuTab, MenuPage>();

        IStore _store;
        HomePage _home;
        MenuTabBar _bar;
        bool _done;

        public static MainMenu Create(IStore store, SkinService skins, IProfileService profile,
            MenuTab tab)
        {
            var go = new GameObject("MainMenu");
            var menu = go.AddComponent<MainMenu>();
            menu._store = store;
            menu.Profile = profile;
            menu.Catalog = new StoreCatalogView(store, skins);
            _current = menu;
            menu.Build(tab);
            return menu;
        }

        void Build(MenuTab tab)
        {
            RuntimeUi.PortraitCanvas(gameObject, SortingOrder);

            BuildHeader();

            // The content area is what is left between the header and the tab bar. Pages fill it;
            // none of them knows how tall it is.
            RuntimeUi.Element("Content", transform, out var content);
            RuntimeUi.Stretch(content, Vector2.zero, Vector2.one,
                new Vector2(0f, MenuTheme.TabBarHeight), new Vector2(0f, -MenuTheme.HeaderHeight));

            _home = AddPage<HomePage>(MenuTab.Run, content);
            _home.Chosen += OnChosen;
            AddPage<ShopPage>(MenuTab.Shop, content);
            AddPage<ProfilePage>(MenuTab.Profile, content);

            Catalog.Changed += _home.RefreshCards;

            _bar = MenuTabBar.Create(transform);
            _bar.Tapped += Show;

            Show(tab);
        }

        /// Title block. The version line under it is DEVELOPMENT ONLY (BuildInfo): it is how the
        /// owner tells which build is on the phone, and it must never ship to Play, so the gate is
        /// Debug.isDebugBuild rather than a define somebody has to remember to clear.
        void BuildHeader()
        {
            RuntimeUi.Element("Header", transform, out var header);
            RuntimeUi.Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -MenuTheme.HeaderHeight), Vector2.zero);
            RuntimeUi.Panel("Backdrop", header, MenuTheme.Bar);

            RuntimeUi.Label("Title", header,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -128f), new Vector2(-24f, -22f),
                84, TextAnchor.MiddleCenter, MenuTheme.Text).text = "VEYRO RUN";

            if (!BuildInfo.IsDevelopment) return;

            RuntimeUi.Label("Version", header,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -176f), new Vector2(-24f, -132f),
                26, TextAnchor.MiddleCenter, MenuTheme.Faint).text = BuildInfo.VersionLabel;
        }

        TPage AddPage<TPage>(MenuTab tab, Transform content) where TPage : MenuPage
        {
            var go = new GameObject(typeof(TPage).Name);
            var page = go.AddComponent<TPage>();
            page.Attach(this, content);
            go.SetActive(false);
            _pages[tab] = page;
            return page;
        }

        /// Switches tabs. Idempotent, so the tab bar can call it for the tab already up (which is
        /// how a page gets a cheap refresh) without rebuilding anything.
        public void Show(MenuTab tab)
        {
            if (_done) return;

            Tab = tab;
            foreach (var pair in _pages) pair.Value.SetVisible(pair.Key == tab);
            _bar?.SetSelected(tab);
        }

        public void GoHome() => Show(MenuTab.Run);

        public void CancelStaging() => _home?.CancelStaging();

        /// Whether the Season 1 entitlement is held. The season pass card asks through here rather
        /// than holding the store itself: one owner of the store seam per screen.
        public bool IsPassOwned() =>
            _store != null && Entitlements.Has(_store.ActiveEntitlements, Entitlements.Season1);

        public void RequestGuide() => GuideRequested?.Invoke();

        void OnChosen(bool cameraMode, FaceTrackingRig rig)
        {
            if (_done) return;
            _done = true;

            // The menu stops owning the screen HERE, not in OnDestroy: the run starts inside the
            // Invoke below, and RunSession's Update (order 100) reads IsOpen later in this same
            // frame - a stale true there would drop the frame's input on the floor.
            if (_current == this) _current = null;

            // Hidden before it is destroyed, like the pause menu: Destroy only lands at the end of
            // the frame, and a menu drawn over the run it just started is a visible flicker.
            gameObject.SetActive(false);
            Chosen?.Invoke(cameraMode, rig);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (Catalog != null)
            {
                if (_home != null) Catalog.Changed -= _home.RefreshCards;
                Catalog.Dispose();
            }
        }
    }
}
