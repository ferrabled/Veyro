using System;
using System.Collections.Generic;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The three-tab bar along the bottom: SHOP, RUN, PROFILE, with RUN in the middle.
    ///
    /// The bar is a table (`Tabs` below) rather than three hand-placed buttons, so the order, the
    /// labels and the count are one edit each and every cell keeps the same geometry. It owns no
    /// screens - it announces a tab and MainMenu decides what that means.
    public sealed class MenuTabBar : MonoBehaviour
    {
        /// The bar, in the order it is drawn. Middle-weighted deliberately: RUN is the reason the
        /// app is open, so it is the widest cell and the only one that carries a filled emblem.
        static readonly (MenuTab Tab, string Label)[] Tabs =
        {
            (MenuTab.Shop, "SHOP"),
            (MenuTab.Run, "RUN"),
            (MenuTab.Profile, "PROFILE")
        };

        /// Height of the accent strip that marks the selected cell.
        const float MarkerHeight = 6f;

        public event Action<MenuTab> Tapped;

        readonly Dictionary<MenuTab, Cell> _cells = new Dictionary<MenuTab, Cell>();

        sealed class Cell
        {
            public Image Background;
            public Image Marker;
            public Text Label;
            public Image Emblem;
            public bool IsRun;
        }

        public static MenuTabBar Create(Transform parent)
        {
            var go = RuntimeUi.Element("TabBar", parent, out var rect);
            RuntimeUi.Stretch(rect,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, MenuTheme.TabBarHeight));

            var bar = go.AddComponent<MenuTabBar>();
            bar.Build(rect);
            return bar;
        }

        void Build(RectTransform rect)
        {
            RuntimeUi.Panel("Backdrop", rect, MenuTheme.Bar);

            float width = 1f / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var spec = Tabs[i];
                _cells[spec.Tab] = BuildCell(rect, spec.Tab, spec.Label, i * width, (i + 1) * width);
            }
        }

        Cell BuildCell(Transform parent, MenuTab tab, string label, float xMin, float xMax)
        {
            var go = RuntimeUi.Element(label, parent, out var rect);
            RuntimeUi.Stretch(rect, new Vector2(xMin, 0f), new Vector2(xMax, 1f),
                new Vector2(4f, 0f), new Vector2(-4f, 0f));

            var background = go.AddComponent<Image>();
            background.color = MenuTheme.Bar;

            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => Tapped?.Invoke(tab));

            var marker = RuntimeUi.Element("Marker", go.transform, out var markerRect);
            RuntimeUi.Stretch(markerRect, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -MarkerHeight), new Vector2(-18f, 0f));
            var markerImage = marker.AddComponent<Image>();
            markerImage.color = MenuTheme.Accent;

            bool isRun = tab == MenuTab.Run;

            // The emblem is a plain square rotated 45 degrees - a diamond. No sprite and no icon
            // font: RuntimeUi's font chain resolves to whatever the OS has, so anything outside
            // ASCII is a box on somebody's phone (CLAUDE.md gotcha #7's neighbourhood). A shape
            // built from a Graphic always renders.
            var emblem = RuntimeUi.Element("Emblem", go.transform, out var emblemRect);
            emblemRect.anchorMin = new Vector2(0.5f, 1f);
            emblemRect.anchorMax = new Vector2(0.5f, 1f);
            emblemRect.anchoredPosition = new Vector2(0f, isRun ? -52f : -46f);
            emblemRect.sizeDelta = isRun ? new Vector2(44f, 44f) : new Vector2(32f, 32f);
            emblemRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var emblemImage = emblem.AddComponent<Image>();
            emblemImage.raycastTarget = false;

            var text = RuntimeUi.Label("Label", go.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 22f), new Vector2(0f, 78f),
                isRun ? 40 : 34, TextAnchor.MiddleCenter, MenuTheme.Dim);
            text.text = label;

            return new Cell
            {
                Background = background,
                Marker = markerImage,
                Label = text,
                Emblem = emblemImage,
                IsRun = isRun
            };
        }

        /// Paints the bar for the tab that is up. Visual only - MainMenu is what actually switches
        /// pages, so the bar can never disagree with what is on screen.
        public void SetSelected(MenuTab tab)
        {
            foreach (var pair in _cells)
            {
                var cell = pair.Value;
                bool selected = pair.Key == tab;

                cell.Background.color = selected ? MenuTheme.Slot : MenuTheme.Bar;
                cell.Marker.enabled = selected;
                cell.Label.color = selected ? MenuTheme.Text : MenuTheme.Dim;
                cell.Emblem.color = selected
                    ? (cell.IsRun ? MenuTheme.Accent : MenuTheme.Text)
                    : MenuTheme.Faint;
            }
        }
    }
}
