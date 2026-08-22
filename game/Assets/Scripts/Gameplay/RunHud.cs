using System;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// Live readout plus the crash/result screen (T-005), built entirely from code so there is
    /// no authored scene or prefab content to merge (CLAUDE.md rule 1).
    public sealed class RunHud : MonoBehaviour
    {
        static readonly Color TextColor = new Color(0.94f, 0.96f, 1f);
        static readonly Color DimColor = new Color(0.04f, 0.05f, 0.09f, 0.82f);
        static readonly Color PanelColor = new Color(0.11f, 0.13f, 0.20f, 0.96f);
        static readonly Color ButtonColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color ComboColor = new Color(1f, 0.86f, 0.22f);
        static readonly Color ModeColor = new Color(0.62f, 0.68f, 0.80f);

        /// Raised by the restart button. RunSession also accepts a tap anywhere, because a
        /// button is a nicety and being able to start the next run is not.
        public event Action RestartRequested;

        Text _score;
        Text _coins;
        Text _combo;
        Text _mode;
        Text _resultScore;
        Text _resultBest;
        GameObject _resultPanel;

        int _shownScore = -1;
        int _shownCoins = -1;
        int _shownCombo = -1;

        static Font _font;

        public static RunHud Create()
        {
            var go = new GameObject("RunHud");
            var hud = go.AddComponent<RunHud>();
            hud.BuildCanvas();
            return hud;
        }

        public void SetLive(ScoreState score, int difficulty)
        {
            if (score.Score != _shownScore)
            {
                _shownScore = score.Score;
                _score.text = _shownScore.ToString();
            }

            if (score.Coins != _shownCoins)
            {
                _shownCoins = score.Coins;
                _coins.text = "coins " + _shownCoins;
            }

            int combo = score.Combo >= 2 ? score.Combo : 0;
            if (combo != _shownCombo)
            {
                _shownCombo = combo;
                _combo.text = combo > 0 ? "x" + score.Multiplier + "  combo " + combo : string.Empty;
            }
        }

        /// Which mode and which UTC day this run belongs to. Shown live, because "the same run on
        /// two devices" is exactly T-008's acceptance test and it should be visible in a screenshot.
        public void SetMode(RunMode mode, string dailyLabel)
        {
            _mode.text = mode == RunMode.Daily ? "DAILY · " + dailyLabel : "FREE RUN";
        }

        public void ShowResult(in RunSummary summary)
        {
            _resultScore.text = summary.Score.ToString();

            string bestLine = summary.IsDaily
                ? "DAILY · " + summary.DailyLabel + "\nbest today " + summary.DailyBest +
                  "   all-time " + summary.AllTimeBest
                : "all-time best " + summary.AllTimeBest;

            _resultBest.text = bestLine + "\n" + summary.Distance + "m   coins " + summary.Coins +
                               "   best combo " + summary.BestCombo;
            _resultPanel.SetActive(true);
        }

        public void HideResult()
        {
            _resultPanel.SetActive(false);
            _shownScore = -1;
            _shownCoins = -1;
            _shownCombo = -1;
        }

        void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _score = CreateText("Score", transform,
                new Vector2(0f, 1f), new Vector2(0.55f, 1f),
                new Vector2(32f, -140f), new Vector2(0f, -24f),
                84, TextAnchor.UpperLeft, TextColor);

            _coins = CreateText("Coins", transform,
                new Vector2(0.45f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -104f), new Vector2(-32f, -32f),
                40, TextAnchor.UpperRight, TextColor);

            _combo = CreateText("Combo", transform,
                new Vector2(0.45f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -156f), new Vector2(-32f, -104f),
                40, TextAnchor.UpperRight, ComboColor);

            _mode = CreateText("Mode", transform,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f),
                new Vector2(36f, -198f), new Vector2(0f, -148f),
                34, TextAnchor.UpperLeft, ModeColor);

            BuildResultPanel();
            _resultPanel.SetActive(false);
        }

        void BuildResultPanel()
        {
            _resultPanel = new GameObject("Result");
            var panelRect = _resultPanel.AddComponent<RectTransform>();
            _resultPanel.transform.SetParent(transform, false);
            Stretch(panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var dim = _resultPanel.AddComponent<Image>();
            dim.color = DimColor;

            var card = new GameObject("Card");
            var cardRect = card.AddComponent<RectTransform>();
            card.transform.SetParent(_resultPanel.transform, false);
            Stretch(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cardRect.sizeDelta = new Vector2(760f, 780f);
            card.AddComponent<Image>().color = PanelColor;

            CreateText("Title", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -140f), new Vector2(-24f, -48f),
                56, TextAnchor.UpperCenter, TextColor).text = "RUN OVER";

            _resultScore = CreateText("ResultScore", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -350f), new Vector2(-24f, -160f),
                140, TextAnchor.UpperCenter, TextColor);

            _resultBest = CreateText("ResultBest", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -570f), new Vector2(-24f, -355f),
                38, TextAnchor.UpperCenter, TextColor);

            BuildRestartButton(card.transform);

            // Anchored to the screen, not the card: inside the card it would sit under the
            // button, which is exactly where it landed on the first device build.
            CreateText("Hint", _resultPanel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 72f), new Vector2(-24f, 132f),
                34, TextAnchor.LowerCenter, new Color(0.72f, 0.76f, 0.85f)).text = "tap anywhere or press space";
        }

        void BuildRestartButton(Transform parent)
        {
            var go = new GameObject("Restart");
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            Stretch(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            rect.anchoredPosition = new Vector2(0f, 110f);
            rect.sizeDelta = new Vector2(480f, 130f);

            var image = go.AddComponent<Image>();
            image.color = ButtonColor;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => RestartRequested?.Invoke());

            CreateText("Label", go.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                52, TextAnchor.MiddleCenter, new Color(0.08f, 0.06f, 0.04f)).text = "RUN AGAIN";
        }

        static Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);

            var text = go.AddComponent<Text>();
            text.font = HudFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

        /// Legacy UGUI Text needs a Font object. LegacyRuntime.ttf is the built-in one in
        /// Unity 2022.2+; the fallbacks exist because a HUD that silently renders nothing is
        /// the kind of bug that only shows up on the device.
        static Font HudFont()
        {
            if (_font != null) return _font;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null) _font = Resources.Load<Font>("Fonts/HudFont");
            if (_font == null)
            {
                var installed = Font.GetOSInstalledFontNames();
                if (installed != null && installed.Length > 0)
                    _font = Font.CreateDynamicFontFromOSFont(installed[0], 48);
            }
            if (_font == null) Debug.LogError("RunHud: no font could be resolved - HUD text will not render.");
            return _font;
        }
    }
}
