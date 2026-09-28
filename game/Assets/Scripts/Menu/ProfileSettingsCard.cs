using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using UnityEngine;

namespace MotionRunner.Menu
{
    /// The profile tab's SETTINGS card: the two sound levels, set right here, then the three
    /// things that open a screen of their own (notifications, gameplay analytics, the guide).
    ///
    /// Sound used to be a SOUND link that opened a modal with two sliders (T-045). A modal for
    /// two numbers was a detour, so each level is a VolumeControl row - the same control the pause
    /// menu shows - which taps, drags, and hands vertical swipes to the page's scroll view.
    public sealed class ProfileSettingsCard
    {
        public const float Height = MenuRows.TitleHeight + 2 * VolumeRowHeight + 3 * MenuRows.Height +
                                    MenuRows.BottomPadding;

        const float VolumeRowHeight = 104f;

        readonly Func<SoundSettings> _settings;
        readonly Action _notifications;
        readonly Action _analytics;
        readonly Action _guide;

        VolumeControl _music;
        VolumeControl _sfx;
        SoundSettings _subscribed;

        /// Settings arrive as a function because GameAudio may not exist (a test host, a build
        /// that failed to load audio): the rows then draw empty and ignore input.
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
            var musicRow = MenuRows.Row(slot, "Music");
            _music = new VolumeControl(musicRow, "MUSIC", VolumeControl.Channel.Music, _settings);
            y = MenuRows.Place(musicRow, y, VolumeRowHeight, true);

            var sfxRow = MenuRows.Row(slot, "Sounds");
            _sfx = new VolumeControl(sfxRow, "SOUNDS", VolumeControl.Channel.Sounds, _settings);
            y = MenuRows.Place(sfxRow, y, VolumeRowHeight, false);

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
        /// elsewhere (the pause menu's own copy of these rows) cannot leave the bars stale.
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
            _music?.Repaint();
            _sfx?.Repaint();
        }

        public void Dispose()
        {
            if (_subscribed != null) _subscribed.Changed -= Repaint;
            _subscribed = null;
        }
    }
}
