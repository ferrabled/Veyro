using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// One sound channel as a row: its name, a scale - a flat zero mark, then ten bars rising left
    /// to right - and an ON/OFF pill (the mute, which keeps the level for later). Shared by the
    /// profile tab's SETTINGS card and the pause menu, so a level looks and behaves the same
    /// wherever it is set.
    ///
    /// The scale is both a set of steps and a slider: tap a bar (or the zero mark) to jump there,
    /// or put a finger down and drag - from wherever the level is to silence, say - and the bars
    /// follow it. Inside the profile's scroll view a mostly-vertical swipe is handed to the page
    /// instead, and nothing changes until the finger has shown which way it is going (ScaleInput),
    /// so scrolling past the row never nudges a level. Music is heard live while dragging; the
    /// gesture saves once when the finger lifts, with a click at the level just set - which is how
    /// SOUNDS gets heard at all.
    public sealed class VolumeControl
    {
        public enum Channel
        {
            Music,
            Sounds
        }

        public const float PillWidth = 120f;

        const float BarGap = 10f;
        const float ScaleHeight = 60f;

        readonly Channel _channel;
        readonly Func<SoundSettings> _settings;
        readonly CosmeticPanel[] _marks = new CosmeticPanel[VolumeSteps.Positions];
        readonly CosmeticPanel _pill;
        readonly Text _pillLabel;

        /// Builds into `row`, a rect the caller has already placed. `inset` is the row's side
        /// margin; `labelWidth` the column the channel's name gets.
        public VolumeControl(RectTransform row, string label, Channel channel, Func<SoundSettings> settings,
            float labelWidth = 190f, float inset = MenuTheme.CardPadding)
        {
            _channel = channel;
            _settings = settings;

            RuntimeUi.Label("Label", row, Vector2.zero, new Vector2(0f, 1f),
                new Vector2(inset, 0f), new Vector2(inset + labelWidth, 0f),
                32, TextAnchor.MiddleLeft, MenuTheme.Text).text = label;

            // The scale takes whatever width the row leaves between label and pill, split into
            // equal cells in anchor space - the stamp row's trick - so it fits any phone width.
            // Its hit area is the row's full height: a finger does not have to find a 6 px mark.
            var scaleGo = RuntimeUi.Element("Scale", row, out var scale);
            RuntimeUi.Stretch(scale, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(inset + labelWidth, 0f), new Vector2(-inset - PillWidth - 24f, 0f));
            var hit = scaleGo.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var input = scaleGo.AddComponent<ScaleInput>();
            input.Moved = SetPosition;
            input.Released = Release;

            RuntimeUi.Element("Marks", scale, out var marks);
            RuntimeUi.Stretch(marks, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -ScaleHeight * 0.5f), new Vector2(0f, ScaleHeight * 0.5f));
            for (int i = 0; i < VolumeSteps.Positions; i++)
                _marks[i] = BuildMark(marks, i);

            RuntimeUi.Element("Toggle", row, out var toggle);
            toggle.anchorMin = toggle.anchorMax = new Vector2(1f, 0.5f);
            toggle.pivot = new Vector2(1f, 0.5f);
            toggle.anchoredPosition = new Vector2(-inset, 0f);
            toggle.sizeDelta = new Vector2(PillWidth, 58f);
            _pill = CosmeticUi.Surface(toggle, MenuTheme.Slot, 29f);
            var button = toggle.gameObject.AddComponent<Button>();
            button.targetGraphic = _pill;
            // On a touch screen a tapped button stays "selected" until something else is, and
            // the default tint left the pill a shade darker after every toggle (device pass).
            var colors = button.colors;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.AddListener(ToggleMuted);
            RuntimeUi.TapSound(button);
            _pillLabel = RuntimeUi.Label("Label", toggle, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 24, TextAnchor.MiddleCenter, MenuTheme.Text);
            _pillLabel.fontStyle = FontStyle.Bold;

            Repaint();
        }

        /// Position 0 is the zero mark - a flat dash on the baseline, the ramp's starting point -
        /// and 1..Count the bars, rising from 40% to 100% of the scale's height.
        static CosmeticPanel BuildMark(RectTransform marks, int position)
        {
            float cell = 1f / VolumeSteps.Positions;
            RuntimeUi.Element("Step" + position, marks, out var column);
            RuntimeUi.Stretch(column, new Vector2(position * cell, 0f), new Vector2((position + 1) * cell, 1f),
                new Vector2(BarGap * 0.5f, 0f), new Vector2(-BarGap * 0.5f, 0f));

            float height = position == 0
                ? 8f
                : ScaleHeight * (0.4f + 0.6f * (position - 1) / (VolumeSteps.Count - 1));
            var bar = RuntimeUi.Element("Bar", column, out var barRect);
            RuntimeUi.Stretch(barRect, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, height));
            var panel = bar.AddComponent<CosmeticPanel>();
            panel.Radius = position == 0 ? 4f : 5f;
            panel.raycastTarget = false;
            return panel;
        }

        float Level(SoundSettings s) => _channel == Channel.Music ? s.MusicVolume : s.SfxVolume;
        bool Muted(SoundSettings s) => _channel == Channel.Music ? s.MusicMuted : s.SfxMuted;

        /// Live while the finger moves: memory only. Setting a level also unmutes, by design -
        /// a player dragging MUSIC wants to hear music, whatever the pill said.
        void SetPosition(int position)
        {
            var settings = _settings?.Invoke();
            if (settings == null) return;
            float level = VolumeSteps.LevelAt(position);
            if (_channel == Channel.Music) settings.SetMusicVolume(level);
            else settings.SetSfxVolume(level);
            Repaint();
        }

        /// The gesture is over: one write, and one click at the level just set.
        void Release()
        {
            var settings = _settings?.Invoke();
            if (settings == null) return;
            settings.Save();
            GameAudio.Tap();
        }

        void ToggleMuted()
        {
            var settings = _settings?.Invoke();
            if (settings == null) return;
            if (_channel == Channel.Music) settings.SetMusicMuted(!settings.MusicMuted);
            else settings.SetSfxMuted(!settings.SfxMuted);
            settings.Save();
            Repaint();
        }

        /// Repaints from the settings. Lit marks are the fill: the zero mark is lit whenever the
        /// channel is on (the ramp always starts somewhere), bars up to the level after it; a
        /// muted channel greys out whole.
        public void Repaint()
        {
            var settings = _settings?.Invoke();
            bool available = settings != null;
            bool muted = available && Muted(settings);
            int lit = available ? VolumeSteps.Lit(Level(settings), muted) : 0;

            for (int i = 0; i < _marks.Length; i++)
            {
                bool on = available && !muted && i <= lit;
                _marks[i].color = on ? MenuTheme.Accent : MenuTheme.Empty;
            }

            _pillLabel.text = !available ? "—" : muted ? "OFF" : "ON";
            _pillLabel.color = muted || !available ? MenuTheme.Faint : MenuTheme.Text;
            _pill.color = muted || !available ? MenuTheme.Empty : MenuTheme.Slot;
        }

        /// The scale's gestures. Nothing happens on pointer-down: a swipe that starts on the scale
        /// might be a scroll, and a level that jumps under a scrolling thumb is the bug a slider
        /// had here. So a tap sets the level on release, a drag that goes sideways scrubs, and a
        /// drag that goes up or down is re-routed to the enclosing ScrollRect, if there is one -
        /// it becomes the page's drag from its first frame, as if it had started on the page.
        sealed class ScaleInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
            IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public Action<int> Moved;
            public Action Released;

            bool _scrubbing;

            public void OnPointerDown(PointerEventData eventData) => _scrubbing = false;

            public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = true;

            public void OnBeginDrag(PointerEventData eventData)
            {
                var scroll = GetComponentInParent<ScrollRect>();
                Vector2 travel = eventData.position - eventData.pressPosition;
                if (scroll != null && Mathf.Abs(travel.y) > Mathf.Abs(travel.x))
                {
                    eventData.pointerDrag = scroll.gameObject;
                    ExecuteEvents.Execute(scroll.gameObject, eventData, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(scroll.gameObject, eventData, ExecuteEvents.beginDragHandler);
                    return;
                }
                _scrubbing = true;
                Move(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (_scrubbing) Move(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (!_scrubbing) return;
                _scrubbing = false;
                Released?.Invoke();
            }

            /// A release with no drag is a tap. A release after a drag - a scrub, or a scroll this
            /// handed on - has been dealt with by OnEndDrag on whichever object owned the drag.
            public void OnPointerUp(PointerEventData eventData)
            {
                if (eventData.dragging) return;
                Move(eventData);
                Released?.Invoke();
            }

            void Move(PointerEventData eventData)
            {
                var rect = (RectTransform)transform;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                        eventData.pressEventCamera, out var local)) return;
                var bounds = rect.rect;
                Moved?.Invoke(VolumeSteps.PositionAt((local.x - bounds.xMin) / bounds.width));
            }
        }
    }
}
