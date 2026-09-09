using System;
using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// Live readout plus the crash/result screen (T-005), built entirely from code so there is
    /// no authored scene or prefab content to merge (CLAUDE.md rule 1). The UGUI plumbing itself
    /// lives in RuntimeUi, shared with every other screen.
    public sealed class RunHud : MonoBehaviour
    {
        static readonly Color TextColor = new Color(0.94f, 0.96f, 1f);
        static readonly Color DimColor = new Color(0.04f, 0.05f, 0.09f, 0.82f);
        static readonly Color PanelColor = new Color(0.11f, 0.13f, 0.20f, 0.96f);
        static readonly Color ButtonColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color SecondaryColor = new Color(0.24f, 0.28f, 0.40f);
        static readonly Color ComboColor = new Color(1f, 0.86f, 0.22f);
        static readonly Color ModeColor = new Color(0.62f, 0.68f, 0.80f);

        /// Raised by the restart button. RunSession also accepts a tap anywhere, because a
        /// button is a nicety and being able to start the next run is not.
        public event Action RestartRequested;

        /// Raised by the SHOP button on the result screen - the store entry point (T-020).
        /// RunFlow owns what opens; the HUD only announces the tap. It leaves the run for the
        /// menu's shop tab, so this behaves exactly like QuitRequested with a different landing
        /// place - which is what makes it safe against the tap that fired it also restarting the
        /// run behind it (see the note on the Quit button below).
        public event Action StoreRequested;

        /// Raised by the pause button. RunFlow owns what pausing means; the HUD only announces
        /// the tap, exactly like the store button.
        public event Action PauseRequested;

        /// Raised by the QUIT TO MENU button on the result screen. Same shape again: RunFlow
        /// owns what leaving costs, the HUD only announces the tap.
        public event Action QuitRequested;

        Text _score;
        Text _coins;
        Text _combo;
        Text _mode;
        Text _resultScore;
        Text _resultBest;
        GameObject _resultPanel;
        GameObject _pauseButton;

        int _shownScore = -1;
        int _shownCoins = -1;
        int _shownCombo = -1;

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

        /// Which mode, which UTC day and which control scheme this run belongs to. Shown live,
        /// because "the same run on two devices" is exactly T-008's acceptance test and it should
        /// be visible in a screenshot - and since the boards split by scheme (Feature D), which
        /// board a run is playing for should be visible the same way.
        public void SetMode(RunMode mode, string dailyLabel, ControlScheme scheme)
        {
            _mode.text = (mode == RunMode.Daily ? "DAILY · " + dailyLabel : "FREE RUN")
                         + " · " + SchemeName(scheme);
        }

        public void ShowResult(in RunSummary summary)
        {
            _resultScore.text = summary.Score.ToString();

            // The bests are the active scheme's board and say so: a camera personal best is not
            // a claim about tilt runs (owner call - the two are separate games).
            string scheme = SchemeName(summary.Scheme);
            string bestLine = summary.IsDaily
                ? "DAILY · " + summary.DailyLabel + " · " + scheme +
                  "\nbest today " + summary.DailyBest + "   all-time " + summary.AllTimeBest
                : "all-time best " + summary.AllTimeBest + " · " + scheme;

            _resultBest.text = bestLine + "\n" + summary.Distance + "m   coins " + summary.Coins +
                               "   best combo " + summary.BestCombo;
            _resultPanel.SetActive(true);
            _pauseButton.SetActive(false); // nothing to pause once the run is over
        }

        static string SchemeName(ControlScheme scheme) =>
            scheme == ControlScheme.Camera ? "CAMERA" : "TILT";

        public void HideResult()
        {
            _resultPanel.SetActive(false);
            _pauseButton.SetActive(true);
            _shownScore = -1;
            _shownCoins = -1;
            _shownCombo = -1;
        }

        /// Wipes the readout when a run is abandoned. The mode menu that follows covers the
        /// screen, but nothing behind it should still be showing a run that no longer exists.
        public void Clear()
        {
            HideResult();
            _pauseButton.SetActive(false);
            _score.text = string.Empty;
            _coins.text = string.Empty;
            _combo.text = string.Empty;
            _mode.text = string.Empty;
        }

        void BuildCanvas()
        {
            RuntimeUi.PortraitCanvas(gameObject);

            _score = RuntimeUi.Label("Score", transform,
                new Vector2(0f, 1f), new Vector2(0.55f, 1f),
                new Vector2(32f, -140f), new Vector2(0f, -24f),
                84, TextAnchor.UpperLeft, TextColor);

            _coins = RuntimeUi.Label("Coins", transform,
                new Vector2(0.45f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -104f), new Vector2(-32f, -32f),
                40, TextAnchor.UpperRight, TextColor);

            _combo = RuntimeUi.Label("Combo", transform,
                new Vector2(0.45f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -156f), new Vector2(-32f, -104f),
                40, TextAnchor.UpperRight, ComboColor);

            _mode = RuntimeUi.Label("Mode", transform,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f),
                new Vector2(36f, -198f), new Vector2(0f, -148f),
                34, TextAnchor.UpperLeft, ModeColor);

            BuildPauseButton();
            BuildResultPanel();
            _resultPanel.SetActive(false);
        }

        /// Bottom-left, small and dim: the run owns the screen, and the corner nearest the thumb
        /// that is not already holding the phone up is the one a player can reach without
        /// covering the track. RunSession runs after the EventSystem (its DefaultExecutionOrder),
        /// so the tap that lands here is frozen before it can also be read as a jump.
        void BuildPauseButton()
        {
            _pauseButton = RuntimeUi.TextButton("Pause", transform,
                new Vector2(0f, 0f), new Vector2(120f, 120f), new Vector2(140f, 140f),
                new Color(0.18f, 0.21f, 0.30f, 0.85f),
                "II", 52, TextColor,
                () => PauseRequested?.Invoke()).gameObject;

            // Shown by HideResult, which is what starting a run calls: there is nothing to pause
            // until there is a run.
            _pauseButton.SetActive(false);
        }

        void BuildResultPanel()
        {
            _resultPanel = RuntimeUi.FullScreenPanel("Result", transform, DimColor);

            // 1120 tall rather than 920: the buttons below are the pause menu's stack, and QUIT
            // TO MENU is the third row it grew by. The card grew by exactly what the stack did
            // (200), so the stats block keeps the 50px of clearance above the buttons it had
            // when there were two of them.
            var card = RuntimeUi.Card("Card", _resultPanel.transform, new Vector2(760f, 1120f), PanelColor);

            RuntimeUi.Label("Title", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -140f), new Vector2(-24f, -48f),
                56, TextAnchor.UpperCenter, TextColor).text = "RUN OVER";

            _resultScore = RuntimeUi.Label("ResultScore", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -350f), new Vector2(-24f, -160f),
                140, TextAnchor.UpperCenter, TextColor);

            _resultBest = RuntimeUi.Label("ResultBest", card.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -570f), new Vector2(-24f, -355f),
                38, TextAnchor.UpperCenter, TextColor);

            // One primary and two secondaries, at the pause menu's exact sizes and y positions
            // (520x140 at 430, 480x110 at 270 and 130): the two screens offer the same kind of
            // choice, so the thumb finds RUN AGAIN where it finds RESUME and QUIT TO MENU where
            // it already is.
            RuntimeUi.TextButton("Restart", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 430f), new Vector2(520f, 140f),
                ButtonColor, "RUN AGAIN", 52, new Color(0.08f, 0.06f, 0.04f),
                () => RestartRequested?.Invoke());

            // Secondary on purpose: RUN AGAIN keeps the primary colour and size, the store is
            // one visible tap away from every crash - which is what a judge needs (§6.3).
            RuntimeUi.TextButton("Skins", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(480f, 110f),
                SecondaryColor, "SKINS & SHOP", 42, TextColor,
                () => StoreRequested?.Invoke());

            // Leaving is a button here for the reason it is one on the pause menu: back is
            // otherwise the only way off this card that is not another run, and back is not
            // something a player is taught.
            //
            // It cannot double as the "tap anywhere" restart the rest of the screen is, and it
            // needs no overlay guard to say so: the tap runs RunFlow.QuitToMenu ->
            // RunSession.Stop(), which nulls Input, clears the pending restart and disables the
            // session - and RunSession's DefaultExecutionOrder(100) puts all of that strictly
            // before the Update that would otherwise have read this same TouchPhase.Ended as a
            // jump (the 27 Aug store-tap bug). SKINS & SHOP above now takes the same route, which
            // is why it stopped needing a guard of its own too.
            RuntimeUi.TextButton("Quit", card.transform,
                new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(480f, 110f),
                SecondaryColor, "QUIT TO MENU", 42, TextColor,
                () => QuitRequested?.Invoke());

            // Anchored to the screen, not the card: inside the card it would sit under the
            // button, which is exactly where it landed on the first device build.
            RuntimeUi.Label("Hint", _resultPanel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 72f), new Vector2(-24f, 132f),
                34, TextAnchor.LowerCenter, new Color(0.72f, 0.76f, 0.85f)).text = "tap anywhere or press space";
        }
    }
}
