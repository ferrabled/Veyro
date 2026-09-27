using System;
using MotionRunner.Audio;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// The sound settings (T-045): two sliders, MUSIC and SOUNDS, and a CLOSE. Code-built on
    /// RuntimeUi like NotificationPanel, whose lifetime shape it copies exactly - RunFlow opens
    /// it off the profile tab's SOUND link, holds the one reference, and closes it on back.
    ///
    /// The sliders change the level LIVE while dragging (the music is playing under the panel,
    /// so the player hears what they are setting) and persist on release - one PlayerPrefs write
    /// per gesture, not per frame. The SOUNDS slider plays the UI tap on release, at the level
    /// just set, because there is otherwise nothing to hear from a slider for sounds that only
    /// happen in a run.
    public sealed class SoundPanel : MonoBehaviour
    {
        const float CardWidth = 920f;
        const float CardHeight = 760f;
        const float SliderWidth = 790f;

        SoundSettings _settings;
        Action _closed;
        Slider _music;
        Slider _sfx;
        Text _musicValue;
        Text _sfxValue;

        public static SoundPanel Show(SoundSettings settings, Action closed)
        {
            var panel = new GameObject("Sound settings").AddComponent<SoundPanel>();
            panel._settings = settings;
            panel._closed = closed;
            panel.Build();
            settings.Changed += panel.Refresh;
            panel.Refresh();
            return panel;
        }

        void Build()
        {
            RuntimeUi.PortraitCanvas(gameObject, 140); // the notification panel's layer: never both up
            RuntimeUi.FullScreenPanel("Dim", transform, new Color(0.04f, 0.05f, 0.09f, 0.9f));
            var card = RuntimeUi.Card("Card", transform, new Vector2(CardWidth, CardHeight), MenuTheme.Card).transform;

            Label("Title", card, 290f, 90f, 48, TextAnchor.MiddleCenter, MenuTheme.Text).text = "SOUND";

            Label("MusicLabel", card, 150f, 50f, 34, TextAnchor.MiddleLeft, MenuTheme.Text).text = "MUSIC";
            _musicValue = Label("MusicValue", card, 150f, 50f, 34, TextAnchor.MiddleRight, MenuTheme.Dim);
            _music = BuildSlider(card, "Music", 82f, _settings.MusicVolume,
                value => _settings.SetMusicVolume(value),
                () => _settings.Save());

            Label("SfxLabel", card, -30f, 50f, 34, TextAnchor.MiddleLeft, MenuTheme.Text).text = "SOUNDS";
            _sfxValue = Label("SfxValue", card, -30f, 50f, 34, TextAnchor.MiddleRight, MenuTheme.Dim);
            _sfx = BuildSlider(card, "Sfx", -98f, _settings.SfxVolume,
                value => _settings.SetSfxVolume(value),
                () =>
                {
                    _settings.Save();
                    GameAudio.Tap(); // hear the level you just set
                });

            var hint = Label("Hint", card, -195f, 44f, 24, TextAnchor.MiddleCenter, MenuTheme.Faint);
            hint.text = "levels save when you let go";

            RuntimeUi.TextButton("Close", card, new Vector2(.5f, .5f),
                new Vector2(0f, -300f), new Vector2(SliderWidth, 72f), MenuTheme.Bar,
                "CLOSE", 30, MenuTheme.Text, Close, Sfx.UiBack);
        }

        /// A uGUI Slider from code: track, fill and handle in the three-rect arrangement the
        /// component expects (the fill's parent and the handle's parent are what it drives).
        /// Everything is a plain Image in MenuTheme colours - no sprites, so it always renders.
        Slider BuildSlider(Transform card, string name, float y, float initial,
            Action<float> changed, Action released)
        {
            var go = RuntimeUi.Element(name, card, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(SliderWidth, 72f);

            // The empty track, a thin bar through the middle of the tap area.
            var track = RuntimeUi.Element("Track", rect, out var trackRect);
            RuntimeUi.Stretch(trackRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -7f), new Vector2(0f, 7f));
            track.AddComponent<Image>().color = MenuTheme.Empty;

            // The filled part, same bar; the Slider sets Fill's anchorMax.x.
            RuntimeUi.Element("FillArea", rect, out var fillArea);
            RuntimeUi.Stretch(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -7f), new Vector2(0f, 7f));
            var fill = RuntimeUi.Element("Fill", fillArea, out var fillRect);
            RuntimeUi.Stretch(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.AddComponent<Image>().color = MenuTheme.Accent;

            // The handle rides inside an area inset by half its width, so it never overhangs the
            // track at either end. The Slider stretches the handle to the area's full height and
            // only drives its x anchors, so the area IS the knob's height (56 of the 72) and the
            // handle's own sizeDelta contributes width alone.
            RuntimeUi.Element("HandleArea", rect, out var handleArea);
            RuntimeUi.Stretch(handleArea, Vector2.zero, Vector2.one, new Vector2(28f, 8f), new Vector2(-28f, -8f));
            var handle = RuntimeUi.Element("Handle", handleArea, out var handleRect);
            handleRect.sizeDelta = new Vector2(56f, 0f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = MenuTheme.Text;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(initial);
            slider.onValueChanged.AddListener(value => changed(value));

            // Release lands on the object that took the press, which the Slider makes its own
            // root - the same GameObject this component sits on.
            go.AddComponent<ReleaseRelay>().Released = released;
            return slider;
        }

        static Text Label(string name, Transform parent, float y, float height, int size,
            TextAnchor alignment, Color color)
        {
            var text = RuntimeUi.Label(name, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-SliderWidth / 2f, y - height / 2f), new Vector2(SliderWidth / 2f, y + height / 2f),
                size, alignment, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// Repaints from the settings - including a change made elsewhere (the pause menu's
        /// mute toggles) while this panel happens to be up.
        void Refresh()
        {
            if (_music == null) return;
            _music.SetValueWithoutNotify(_settings.MusicVolume);
            _sfx.SetValueWithoutNotify(_settings.SfxVolume);
            _musicValue.text = Percent(_settings.MusicVolume, _settings.MusicMuted);
            _sfxValue.text = Percent(_settings.SfxVolume, _settings.SfxMuted);
        }

        static string Percent(float level, bool muted) =>
            muted ? "MUTED" : Mathf.RoundToInt(level * 100f) + "%";

        public void Close()
        {
            _settings.Save(); // belt to the release braces: a panel closed mid-drag still keeps the level
            gameObject.SetActive(false);
            _closed?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= Refresh;
        }

        /// The pointer-up on a slider. A separate component rather than a subclass of Slider so
        /// the Slider stays the stock one and the panel owns what "released" means.
        sealed class ReleaseRelay : MonoBehaviour, IPointerUpHandler
        {
            public Action Released;
            public void OnPointerUp(PointerEventData eventData) => Released?.Invoke();
        }
    }
}
