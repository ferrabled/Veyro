using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The profile tab's SETTINGS card: the two sound levels, set right here, then the three
    /// things that open a screen of their own (notifications, gameplay analytics, the guide).
    ///
    /// Sound used to be a SOUND link that opened a modal with two sliders (T-045). A modal for
    /// two numbers was a detour, and a slider cannot live in the scrolling page this card sits in
    /// - a vertical swipe starting on it jumps the level - so each level is ten tappable bars
    /// (VolumeSteps) and an ON/OFF pill, the pause menu's mute. Every tap saves, and the button's
    /// own click plays after the level has changed, so the SOUNDS bars are heard at the level
    /// they just set.
    public sealed class ProfileSettingsCard
    {
        public const float Height = MenuRows.TitleHeight + 2 * VolumeRowHeight + 3 * MenuRows.Height +
                                    MenuRows.BottomPadding;

        const float VolumeRowHeight = 104f;

        /// The label column of a volume row, then the bars, then the pill.
        const float VolumeLabelWidth = 190f;
        const float TogglePillWidth = 120f;
        const float BarGap = 10f;

        readonly Func<SoundSettings> _settings;
        readonly Action _notifications;
        readonly Action _analytics;
        readonly Action _guide;

        VolumeRow _music;
        VolumeRow _sfx;
        SoundSettings _subscribed;

        /// Settings arrive as a function because GameAudio may not exist (a test host, a build
        /// that failed to load audio): the rows then draw empty and ignore taps.
        public ProfileSettingsCard(Func<SoundSettings> settings, Action notifications, Action analytics,
            Action guide)
        {
            _settings = settings;
            _notifications = notifications;
            _analytics = analytics;
            _guide = guide;
        }

        public void Build(RectTransform slot)
        {
            RuntimeUi.Panel("Card", slot, MenuTheme.Card);
            MenuRows.Title(slot, "SETTINGS");

            float y = MenuRows.TitleHeight;
            _music = new VolumeRow(MenuRows.Row(slot, "Music"), "MUSIC",
                s => s.MusicVolume, s => s.MusicMuted, (s, level) => s.SetMusicVolume(level),
                (s, muted) => s.SetMusicMuted(muted), _settings);
            y = MenuRows.Place(_music.Rect, y, VolumeRowHeight, true);

            _sfx = new VolumeRow(MenuRows.Row(slot, "Sounds"), "SOUNDS",
                s => s.SfxVolume, s => s.SfxMuted, (s, level) => s.SetSfxVolume(level),
                (s, muted) => s.SetSfxMuted(muted), _settings);
            y = MenuRows.Place(_sfx.Rect, y, VolumeRowHeight, false);

            y = NavRow(slot, "Notifications", "NOTIFICATIONS", _notifications, y);
            y = NavRow(slot, "Analytics", "GAMEPLAY ANALYTICS", _analytics, y);
            NavRow(slot, "Guide", "HOW TO PLAY", _guide, y);
        }

        static float NavRow(RectTransform slot, string name, string label, Action onTap, float y)
        {
            var row = MenuRows.Row(slot, name);
            MenuRows.Tap(row, () => onTap?.Invoke());
            MenuRows.Label(row, label, MenuRows.ChevronReserve, false);
            MenuRows.Chevron(row);
            return MenuRows.Place(row, y, MenuRows.Height, false);
        }

        /// Repaints from the settings, and keeps listening while the tab is up so a change made
        /// elsewhere (the pause menu's mute toggles) cannot leave the bars stale.
        public void Refresh()
        {
            var settings = _settings?.Invoke();
            if (settings != _subscribed)
            {
                if (_subscribed != null) _subscribed.Changed -= Repaint;
                _subscribed = settings;
                if (_subscribed != null) _subscribed.Changed += Repaint;
            }
            Repaint();
        }

        void Repaint()
        {
            var settings = _settings?.Invoke();
            _music?.Repaint(settings);
            _sfx?.Repaint(settings);
        }

        public void Dispose()
        {
            if (_subscribed != null) _subscribed.Changed -= Repaint;
            _subscribed = null;
        }

        /// One channel: label, ten bars rising left to right, ON/OFF pill.
        sealed class VolumeRow
        {
            public readonly RectTransform Rect;

            readonly Func<SoundSettings, float> _level;
            readonly Func<SoundSettings, bool> _muted;
            readonly Action<SoundSettings, float> _setLevel;
            readonly Action<SoundSettings, bool> _setMuted;
            readonly Func<SoundSettings> _settings;

            readonly Image[] _bars = new Image[VolumeSteps.Count];
            readonly CosmeticPanel _pill;
            readonly Text _pillLabel;

            public VolumeRow(RectTransform row, string label,
                Func<SoundSettings, float> level, Func<SoundSettings, bool> muted,
                Action<SoundSettings, float> setLevel, Action<SoundSettings, bool> setMuted,
                Func<SoundSettings> settings)
            {
                Rect = row;
                _level = level;
                _muted = muted;
                _setLevel = setLevel;
                _setMuted = setMuted;
                _settings = settings;

                RuntimeUi.Label("Label", row, Vector2.zero, new Vector2(0f, 1f),
                    new Vector2(MenuTheme.CardPadding, 0f), new Vector2(MenuTheme.CardPadding + VolumeLabelWidth, 0f),
                    32, TextAnchor.MiddleLeft, MenuTheme.Text).text = label;

                // The bars share whatever width the card leaves between label and pill, split
                // evenly in anchor space - the stamp row's trick - so they fit any phone width.
                RuntimeUi.Element("Bars", row, out var bars);
                RuntimeUi.Stretch(bars, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(MenuTheme.CardPadding + VolumeLabelWidth, -30f),
                    new Vector2(-MenuTheme.CardPadding - TogglePillWidth - 28f, 30f));
                for (int i = 0; i < VolumeSteps.Count; i++)
                    _bars[i] = BuildBar(bars, i);

                // ON/OFF: the pause menu's mute, so a muted channel keeps its level for later.
                RuntimeUi.Element("Toggle", row, out var toggle);
                toggle.anchorMin = toggle.anchorMax = new Vector2(1f, 0.5f);
                toggle.pivot = new Vector2(1f, 0.5f);
                toggle.anchoredPosition = new Vector2(-MenuTheme.CardPadding, 0f);
                toggle.sizeDelta = new Vector2(TogglePillWidth, 58f);
                _pill = CosmeticUi.Surface(toggle, MenuTheme.Slot, 29f);
                var button = toggle.gameObject.AddComponent<Button>();
                button.targetGraphic = _pill;
                button.onClick.AddListener(ToggleMuted);
                RuntimeUi.TapSound(button);
                _pillLabel = RuntimeUi.Label("Label", toggle, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, 24, TextAnchor.MiddleCenter, MenuTheme.Text);
                _pillLabel.fontStyle = FontStyle.Bold;
            }

            /// A full-height column for the tap, with the visible bar standing in its lower part
            /// - short bars would otherwise be small targets.
            Image BuildBar(RectTransform bars, int index)
            {
                float cell = 1f / VolumeSteps.Count;
                var column = RuntimeUi.Element("Step" + index, bars, out var columnRect);
                RuntimeUi.Stretch(columnRect, new Vector2(index * cell, 0f), new Vector2((index + 1) * cell, 1f),
                    new Vector2(BarGap * 0.5f, -20f), new Vector2(-BarGap * 0.5f, 20f));
                var hit = column.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var button = column.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SetStep(index));
                RuntimeUi.TapSound(button);

                // Rising from 40% to 100% of the bar area: a volume ramp, read at a glance.
                float height = 0.4f + 0.6f * index / (VolumeSteps.Count - 1);
                var bar = RuntimeUi.Element("Bar", column.transform, out var barRect);
                RuntimeUi.Stretch(barRect, Vector2.zero, new Vector2(1f, 0f),
                    new Vector2(0f, 20f), new Vector2(0f, 20f + 60f * height));
                var panel = bar.AddComponent<CosmeticPanel>();
                panel.Radius = 5f;
                panel.raycastTarget = false;
                return panel;
            }

            void SetStep(int index)
            {
                var settings = _settings?.Invoke();
                if (settings == null) return;
                _setLevel(settings, VolumeSteps.LevelFor(index)); // also unmutes, by design
                settings.Save();
            }

            void ToggleMuted()
            {
                var settings = _settings?.Invoke();
                if (settings == null) return;
                _setMuted(settings, !_muted(settings));
                settings.Save();
            }

            public void Repaint(SoundSettings settings)
            {
                bool available = settings != null;
                bool muted = available && _muted(settings);
                int lit = available ? VolumeSteps.Lit(_level(settings), muted) : 0;

                for (int i = 0; i < _bars.Length; i++)
                {
                    _bars[i].color = i < lit ? MenuTheme.Accent : MenuTheme.Empty;
                    _bars[i].SetVerticesDirty();
                }

                _pillLabel.text = !available ? "—" : muted ? "OFF" : "ON";
                _pillLabel.color = muted || !available ? MenuTheme.Faint : MenuTheme.Text;
                _pill.color = muted || !available ? MenuTheme.Empty : MenuTheme.Slot;
                _pill.SetVerticesDirty();
            }
        }
    }
}
