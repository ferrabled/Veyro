using System;
using MotionRunner.Art;
using MotionRunner.Audio;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Menu;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// Live readout plus the crash/result card (T-005), built entirely from code so there is
    /// no authored scene or prefab content to merge (CLAUDE.md rule 1). The UGUI plumbing itself
    /// lives in RuntimeUi, shared with every other screen.
    ///
    /// ---- the result card -----------------------------------------------------------------
    ///
    /// The card is the thing a player screenshots, so it is laid out as a result card and not as
    /// a readout: mode and scheme as small metadata, the score, one badge OR one "what to beat"
    /// line, the player's actual runner on a stage, three stat tiles, and the four ways on.
    ///
    /// It borrows the menu's own furniture deliberately - rounded CosmeticPanel surfaces,
    /// MenuTheme's palette and type sizes, the locker's teal-to-cream preview wash behind the
    /// character - because a share card that looks like a different app than the locker it
    /// advertises is worth less than one that does not.
    ///
    /// What it deliberately does NOT show: the multiline bests block the old screen carried. The
    /// bests are one quiet line, and only while they are still something to chase.
    public sealed class RunHud : MonoBehaviour
    {
        static readonly Color TextColor = MenuTheme.Text;

        /// The scrim behind the card. The park's own ink rather than the old blue-black: the card
        /// is paper laid on the game's dark, not on a debug overlay.
        static readonly Color DimColor =
            new Color(ParkTheme.Ink.r, ParkTheme.Ink.g, ParkTheme.Ink.b, 0.86f);

        static readonly Color ComboColor = MenuTheme.Gold;
        static readonly Color ModeColor = MenuTheme.Dim;

        // ---- card geometry, in the 1080x1920 design space (RuntimeUi.ReferenceResolution) ----
        //
        // 880 is the widest the card may be. RuntimeUi.PortraitCanvas scales at match 0.5, so a
        // 20:9 phone leaves about 966 design units of WIDTH and a 21:9 one about 943 - the card
        // therefore keeps ~30px of air each side on the tallest phone we target and 100 on a
        // 16:9 one. 1420 tall centres inside 1920 with 250 clear above and below, which is where
        // the tap hint and any notch live.
        const float CardWidth = 880f;
        const float CardHeight = 1420f;
        const float CardPad = 40f;
        const float CardRadius = 44f;

        /// The card fades in over this, once the world's crash pose has had its turn.
        const float FadeSeconds = 0.25f;

        /// Crash to fully-visible card. RunSession locks the restart gate (RUN AGAIN and the
        /// camera hop) for this long, so nothing during the crash pose can skip a card nobody
        /// has seen yet.
        public const float RevealSeconds = RunnerVisual.CrashPoseSeconds + FadeSeconds;

        /// The result loop starts this long after the stinger begins, crossfading in under its
        /// tail (GameAudio's usual 0.8 s). Unscaled, like the reveal.
        public const float ResultMusicDelaySeconds = 1.0f;

        /// The NEW BEST pill's pop, on unscaled time - the result screen can be up while the
        /// world is still at timeScale 0.
        const float BadgePopSeconds = 0.34f;
        const float BadgeStartScale = 0.55f;

        /// How long the run stays held after a SHARE tap. See HoldRun.
        const float ShareHoldSeconds = 0.5f;

        /// Raised by the RUN AGAIN button - the one way into the next run on a tilt card. In
        /// camera mode RunSession also accepts a hop (its RestartGesture), because the player is
        /// 2 m from the phone; a tap anywhere else on the card does nothing (owner call, 24 Sep).
        public event Action RestartRequested;

        /// Whether the card may promise "HOP to run again". Set by RunSession before ShowResult:
        /// true only while the camera is still the run's steering, so a camera run that fell back
        /// to tilt gets the same button-only card a tilt run gets.
        public bool HopRestartAvailable { get; set; }

        /// Raised by the SKINS & SHOP link on the result card - the store entry point (T-020).
        /// RunFlow owns what opens; the HUD only announces the tap. It leaves the run for the
        /// menu's shop tab, so this behaves exactly like QuitRequested with a different landing
        /// place - which is what makes it safe against the tap that fired it also restarting the
        /// run behind it (see the note on the Quit link below).
        public event Action StoreRequested;

        /// Raised by the pause button. RunFlow owns what pausing means; the HUD only announces
        /// the tap, exactly like the store button.
        public event Action PauseRequested;

        /// Raised by the QUIT TO MENU link on the result card. Same shape again: RunFlow owns
        /// what leaving costs, the HUD only announces the tap.
        public event Action QuitRequested;

        /// What the card's character wears. Set by GameBootstrap to the live SkinService, so the
        /// runner on the card is the runner the player just ran with, hat and back item and all.
        /// Null in a test, or in any host with no commerce layer: the card then shows the default
        /// runner rather than no runner.
        public Func<CosmeticLoadout> Loadout;

        /// Holds / releases the run loop. Set by GameBootstrap to RunSession.Frozen.
        ///
        /// It exists for exactly one caller: SHARE, the one button that STAYS on the result
        /// screen. Every other way off this card is already safe against its own tap - RUN AGAIN
        /// wants a restart, and SKINS & SHOP and QUIT TO MENU go through RunFlow.QuitToMenu ->
        /// RunSession.Stop(), which clears the pending restart and disables the session in the
        /// same frame the button fires (the 27 Aug store-tap bug).
        ///
        /// It was written against the "tap anywhere" restart: the release that opened the share
        /// sheet was the same TouchPhase.Ended TouchTapInput reported as a jump, so the card was
        /// gone the moment the player came back from the chooser. That path is retired (24 Sep;
        /// the gate is fed only by the camera hop), so on a TILT card the hold now guards
        /// nothing. It is kept for the CAMERA card: RunSession's RestartGesture keeps reading the
        /// face across the share round trip, and holding the loop for the tap's frame and for a
        /// short window after focus returns is the cheap belt against a re-acquired face being
        /// read as a hop while the chooser is closing. Not clearly dead, so not removed.
        ///
        /// Mechanism: the one the pause menu already uses. RunSession ticks input while Frozen
        /// and returns BEFORE the restart check, so anything read on those frames is consumed and
        /// dropped. RunSession's DefaultExecutionOrder(100) runs it after the EventSystem, so a
        /// freeze set inside this click handler lands on the frame of the tap that caused it. The
        /// hold releases on a short unscaled timer, re-armed whenever the app regains focus (the
        /// sheet takes focus away), and unconditionally from HideResult/Clear - a leaked hold
        /// would soft-lock the card, so every exit releases it.
        public Action<bool> HoldRun;

        Text _score;
        Text _coins;
        Text _combo;
        Text _mode;

        Text _resultScore;
        Text _resultMode;
        Text _resultScheme;
        Text _resultBest;
        Text _resultXp;
        Text _resultDistance;
        Text _resultCoins;
        Text _resultCombo;
        Text _badgeText;
        GameObject _badge;
        RectTransform _badgeRect;
        GameObject _hint;
        RectTransform _stage;
        RunnerPreview _preview;

        GameObject _resultPanel;
        CanvasGroup _resultCanvas;
        float _resultRevealAt;
        GameObject _pauseButton;

        /// Whether this card's stinger has played. Armed by ShowResult, spent by Update on the
        /// frame the card starts to fade in.
        bool _revealCued;

        /// Unscaled time at which the result loop starts, or 0 when none is pending. Set when
        /// the stinger fires; cleared by HideResult so a RUN AGAIN (or a quit) inside the delay
        /// cannot land the result loop on top of the next run.
        float _resultMusicAt;

        /// The run the card is showing - SHARE's whole payload (T-024).
        RunSummary _summary;

        float _shareHoldUntil;

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

        /// The season-XP line, filled in asynchronously by RunSession once the submission lands.
        /// A caption on the card rather than a headline: it is news about a background job.
        public void SetXpStatus(string status) { if(_resultXp!=null) _resultXp.text=status; }

        /// Built lazily by SetChallenge, and only for challenge runs - see there.
        Text _challenge;

        /// The one line a challenge run adds to the HUD (T-024): the score the link asked the
        /// player to beat, under the mode line. `target` of 0 or less clears it.
        ///
        /// Self-contained on purpose - it builds its own label the first time it is asked for
        /// one, so no other HUD method has to know it exists and the ordinary run's canvas is
        /// byte-for-byte what it always was.
        public void SetChallenge(int target)
        {
            if (target <= 0)
            {
                if (_challenge != null) _challenge.text = string.Empty;
                return;
            }

            if (_challenge == null)
                _challenge = RuntimeUi.Label("Challenge", transform,
                    new Vector2(0f, 1f), new Vector2(0.75f, 1f),
                    new Vector2(36f, -238f), new Vector2(0f, -192f),
                    32, TextAnchor.UpperLeft, ComboColor);

            _challenge.text = "CHALLENGE · beat " + ChallengeMessage.FormatScore(target);
        }

        public void ShowResult(in RunSummary summary, bool showCrashEffect=false)
        {
            _summary = summary;

            // Grouped the way the share text groups it (ChallengeMessage.FormatScore): "4 210",
            // never "4,210" - one number format across the card and the message it produces.
            _resultScore.text = ChallengeMessage.FormatScore(summary.Score);
            _resultMode.text = summary.IsDaily
                ? "DAILY · " + (string.IsNullOrEmpty(summary.DailyLabel) ? "TODAY" : summary.DailyLabel)
                : "FREE RUN";
            _resultScheme.text = SchemeName(summary.Scheme);

            // One of the two, never both: a run that set a record does not need telling what the
            // record was, and a run that did not needs to know what to beat. The best quoted is
            // the ACTIVE SCHEME's board - a camera personal best is not a claim about tilt runs
            // (owner call: the two are separate games) - which the scheme tag above says.
            bool record = summary.IsNewRecord;
            _badge.SetActive(record);
            _resultBest.gameObject.SetActive(!record);
            if (record)
                _badgeText.text = summary.NewAllTimeBest ? "NEW BEST" : "NEW DAILY BEST";
            else
                _resultBest.text = summary.IsDaily
                    ? "best today " + ChallengeMessage.FormatScore(summary.DailyBest)
                    : "all-time best " + ChallengeMessage.FormatScore(summary.AllTimeBest);

            _resultDistance.text = ChallengeMessage.FormatScore(summary.Distance) + "m";
            _resultCoins.text = summary.Coins.ToString();
            _resultCombo.text = summary.BestCombo.ToString();

            // The world's crash pose always plays now, so the card always waits it out - the old
            // 0.35s applied only when a crash cosmetic was firing. showCrashEffect no longer
            // gates the delay; it says the player OWNS a crash burst, which the card replays on
            // its own character so the cosmetic they paid for is in the screenshot.
            _badgeRect.localScale = Vector3.one * BadgeStartScale;
            _resultRevealAt = Time.unscaledTime + RunnerVisual.CrashPoseSeconds;
            _revealCued = false;
            _resultMusicAt = 0f;
            _resultCanvas.alpha = 0f;
            _resultCanvas.interactable = false;
            _resultPanel.SetActive(true);
            _hint.SetActive(false);
            _pauseButton.SetActive(false); // nothing to pause once the run is over

            ShowCharacter(summary.IsNewRecord, showCrashEffect);
        }

        /// The card's centrepiece: the player's own runner, live, wearing what it ran in.
        ///
        /// Built on the FIRST result and kept after that. The preview owns a 768x768 RenderTexture
        /// and an offscreen camera, so it must not be rendering during a run: it lives under the
        /// result panel, which HideResult deactivates, and RunnerPreview.OnDisable parks its whole
        /// stage with it. Clear() - the abandoned-run path into the menu, which builds previews of
        /// its own - destroys it outright, which is what releases the texture instead of leaving
        /// one behind per run.
        void ShowCharacter(bool celebrate, bool crashEffect)
        {
            if (_stage == null) return;
            // Defensive: the preview instantiates character art and allocates a RenderTexture.
            // A host with no graphics device (or no art) must still get a result card - losing
            // the character is a worse card, losing the card is a lost run. Nothing here may
            // throw out of RunSession.Crash, and one failed card does not condemn the next one.
            try
            {
                if (_preview == null) _preview = RunnerPreview.Create(_stage);
                _preview.Show(Look());

                // Immediate: the preview's framing normally eases in over a few frames, and the
                // card does not have a few frames before somebody screenshots it.
                if (celebrate) _preview.Focus(CosmeticSlot.Skin, true);
                else _preview.FrameFallen(); // the defeat pose ends on the floor - frame the floor
                if (_preview.Visual != null) _preview.Visual.PlayResultPose(celebrate);
                if (crashEffect) _preview.Burst();
            }
            catch (Exception error)
            {
                Debug.LogWarning("RunHud: result character unavailable - " + error.Message);
            }
        }

        CosmeticLoadout Look() => (Loadout != null ? Loadout() : null) ?? new CosmeticLoadout();

        static string SchemeName(ControlScheme scheme) =>
            scheme == ControlScheme.Camera ? "CAMERA" : "TILT";

        void Update()
        {
            TickShareHold();
            if (_resultCanvas == null || !_resultPanel.activeSelf) return;

            // The card's stinger, on the frame it starts to show - after the crash pose, on the
            // bed RunSession.Crash faded the music down to. ONE stinger per card: a new all-time
            // best gets the fanfare INSTEAD of the jingle (two stingers over each other read as
            // a glitch, and the record is the bigger news). A new daily best keeps the jingle;
            // its badge already says the rest.
            if (!_revealCued && Time.unscaledTime >= _resultRevealAt)
            {
                _revealCued = true;
                GameAudio.Play(_summary.NewAllTimeBest ? Sfx.NewBest : Sfx.ResultJingle);
                _resultMusicAt = Time.unscaledTime + ResultMusicDelaySeconds;
            }

            // Then the loop under the card, a beat later: the "best" one for ANY record (all-time
            // or daily - the badge's own rule), the other for every other run. It parks the run
            // track on the deck's other slot rather than replacing it, which is what lets RUN
            // AGAIN resume the same song mid-bar (MusicDeck).
            if (_resultMusicAt > 0f && Time.unscaledTime >= _resultMusicAt)
            {
                _resultMusicAt = 0f;
                GameAudio.PlayResultMusic(_summary.IsNewRecord);
            }

            float alpha = Mathf.Clamp01((Time.unscaledTime - _resultRevealAt) / FadeSeconds);
            _resultCanvas.alpha = alpha;

            // Interactable only once the card is fully up, and the camera hint with it: a control
            // the player cannot see is a control they cannot have meant to use. The hint exists
            // only on a card a hop can restart.
            bool visible = alpha >= 1f;
            _resultCanvas.interactable = visible;
            bool hint = visible && HopRestartAvailable;
            if (_hint.activeSelf != hint) _hint.SetActive(hint);

            if (!_badge.activeSelf) return;
            float pop = Mathf.Clamp01((Time.unscaledTime - _resultRevealAt - FadeSeconds) / BadgePopSeconds);
            _badgeRect.localScale = Vector3.one * Mathf.LerpUnclamped(BadgeStartScale, 1f, Overshoot(pop));
        }

        /// Ease-out-back: lands on 1 having gone past it, which is what reads as a stamp rather
        /// than as a fade.
        static float Overshoot(float t)
        {
            float back = t - 1f;
            return 1f + 2.70158f * back * back * back + 1.70158f * back * back;
        }

        // ---- the share hold (see HoldRun) ----

        void ShareTapped()
        {
            // Held BEFORE the sheet opens: ShareSheet.Send may hand the app straight to Android,
            // and the frame this runs on is the one that must not reach the restart check.
            _shareHoldUntil = Time.unscaledTime + ShareHoldSeconds;
            HoldRun?.Invoke(true);
            RunShare.Share(_summary);
        }

        void TickShareHold()
        {
            if (_shareHoldUntil <= 0f || Time.unscaledTime < _shareHoldUntil) return;
            ReleaseShareHold();
        }

        void ReleaseShareHold()
        {
            if (_shareHoldUntil <= 0f) return;
            _shareHoldUntil = 0f;
            HoldRun?.Invoke(false);
        }

        /// The share sheet takes focus; coming back re-arms the hold for one more short window, so
        /// whatever the platform replays on the way back is spent against a frozen session too.
        void OnApplicationFocus(bool focused)
        {
            if (focused && _shareHoldUntil > 0f) _shareHoldUntil = Time.unscaledTime + ShareHoldSeconds;
        }

        public void HideResult()
        {
            ReleaseShareHold();
            _resultMusicAt = 0f; // a loop still pending must not start over the next run
            _resultCanvas.alpha=1;_resultCanvas.interactable=true;
            _resultPanel.SetActive(false);
            SetXpStatus("");
            _pauseButton.SetActive(true);
            _shownScore = -1;
            _shownCoins = -1;
            _shownCombo = -1;
        }

        /// Wipes the readout when a run is abandoned. The mode menu that follows covers the
        /// screen, but nothing behind it should still be showing a run that no longer exists -
        /// including the card's character, whose RenderTexture the menu's own previews should not
        /// have to share the frame with.
        public void Clear()
        {
            HideResult();
            DisposePreview();
            _pauseButton.SetActive(false);
            _score.text = string.Empty;
            _coins.text = string.Empty;
            _combo.text = string.Empty;
            _mode.text = string.Empty;
        }

        void DisposePreview()
        {
            if (_preview == null) return;
            var go = _preview.gameObject;
            _preview = null;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        void BuildCanvas()
        {
            RuntimeUi.PortraitCanvas(gameObject);
            var backing = RuntimeUi.Panel("Score backing", transform, MenuTheme.Card);
            RuntimeUi.Stretch(backing.rectTransform, new Vector2(0,1), Vector2.one,
                new Vector2(0,-216), Vector2.zero);
            backing.raycastTarget = false;

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
            BuildResultCard();
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
                MenuTheme.Card,
                "II", 52, TextColor,
                () => PauseRequested?.Invoke()).gameObject;

            // Shown by HideResult, which is what starting a run calls: there is nothing to pause
            // until there is a run.
            _pauseButton.SetActive(false);
        }

        // ---- the result card ----

        void BuildResultCard()
        {
            _resultPanel = RuntimeUi.FullScreenPanel("Result", transform, DimColor);
            _resultCanvas = _resultPanel.AddComponent<CanvasGroup>();

            RuntimeUi.Element("Card", _resultPanel.transform, out var card);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            var surface = CosmeticUi.Surface(card, MenuTheme.ItemCard, CardRadius);
            surface.Gradient = true;
            surface.Bottom = MenuTheme.Card;
            surface.raycastTarget = false;

            BuildMasthead(card);
            BuildHeadline(card);
            BuildStage(card);
            BuildStats(card);

            _resultXp = Band("SeasonXp", card, 1092f, 42f, 24, MenuTheme.Faint);

            BuildButtons(card);

            // The camera card's one extra line, under the card: the player is 2 m away, so the
            // card has to say that a hop restarts and that the phone must be able to see them
            // (the YOU · CAMERA panel sits above the card meanwhile - FaceOverlay.ResultPosition).
            // Anchored to the screen, not the card: inside the card it would sit under the
            // buttons, which is exactly where the old "tap anywhere" hint landed on the first
            // device build. Shown only once the card has finished fading in, and only when a hop
            // can actually restart (HopRestartAvailable) - see Update.
            var hint = RuntimeUi.Label("Hint", _resultPanel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 72f), new Vector2(-24f, 132f),
                34, TextAnchor.LowerCenter, ParkTheme.Paper);
            hint.text = "stand where the phone can see you · HOP to run again";
            _hint = hint.gameObject;
        }

        /// Mode left, scheme right, both small and dim: metadata on a card, not a headline. The
        /// wordmark lives down on the character stage instead (BuildStage), the way a photo is
        /// signed rather than titled.
        void BuildMasthead(RectTransform card)
        {
            _resultMode = RuntimeUi.Label("ResultMode", card,
                new Vector2(0f, 1f), new Vector2(0.62f, 1f),
                new Vector2(CardPad, -68f), new Vector2(0f, -26f),
                26, TextAnchor.MiddleLeft, MenuTheme.Dim);

            _resultScheme = RuntimeUi.Label("ResultScheme", card,
                new Vector2(0.62f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -68f), new Vector2(-CardPad, -26f),
                26, TextAnchor.MiddleRight, MenuTheme.Faint);
        }

        /// SCORE, the number, then either the badge or the line that says what to beat. Both use
        /// the same band, so the card's rhythm does not change with the result.
        void BuildHeadline(RectTransform card)
        {
            Band("ScoreLabel", card, 78f, 34f, 26, MenuTheme.Faint).text = "SCORE";

            _resultScore = Band("ResultScore", card, 112f, 156f, 132, MenuTheme.Text);
            _resultScore.fontStyle = FontStyle.Bold;

            RuntimeUi.Element("Badge", card, out _badgeRect);
            _badgeRect.anchorMin = _badgeRect.anchorMax = new Vector2(0.5f, 1f);
            _badgeRect.anchoredPosition = new Vector2(0f, -320f);
            _badgeRect.sizeDelta = new Vector2(460f, 72f);
            CosmeticUi.Surface(_badgeRect, MenuTheme.Gold, 36f).raycastTarget = false;
            _badgeText = RuntimeUi.Label("Label", _badgeRect,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                34, TextAnchor.MiddleCenter, MenuTheme.OnAccent);
            _badgeText.fontStyle = FontStyle.Bold;
            _badge = _badgeRect.gameObject;
            _badge.SetActive(false);

            _resultBest = Band("ResultBest", card, 292f, 60f, 28, MenuTheme.Faint);
        }

        /// The character, on the locker's own teal-to-cream wash so the two screens read as one
        /// product. RunnerPreview brings its drag handler with it, so the card can be spun to a
        /// better angle before it is shared.
        void BuildStage(RectTransform card)
        {
            RuntimeUi.Element("Stage", card, out _stage);
            RuntimeUi.Stretch(_stage, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(CardPad, -908f), new Vector2(-CardPad, -372f));
            var wash = CosmeticUi.Surface(_stage, MenuTheme.PreviewTop, 36f);
            wash.Gradient = true;
            wash.Bottom = MenuTheme.PreviewBottom;
            wash.raycastTarget = false;

            // The signature. Small, cornered, lowercase: enough that a screenshot says which game
            // this is without the card turning into an advert.
            RuntimeUi.Label("Wordmark", _stage,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 14f), new Vector2(-20f, 54f),
                26, TextAnchor.LowerRight, MenuTheme.Faint).text = "veyro";
        }

        /// Three tiles, one row, nothing else: distance, coins, best combo. The old screen packed
        /// the same three numbers PLUS both bests and the mode into one wrapped string, which is
        /// the wall of text this card exists to replace.
        void BuildStats(RectTransform card)
        {
            RuntimeUi.Element("Stats", card, out var row);
            RuntimeUi.Stretch(row, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(CardPad, -1076f), new Vector2(-CardPad, -926f));

            _resultDistance = BuildTile(row, 0, "DISTANCE");
            _resultCoins = BuildTile(row, 1, "COINS");
            _resultCombo = BuildTile(row, 2, "BEST COMBO");
        }

        static Text BuildTile(RectTransform row, int index, string caption)
        {
            RuntimeUi.Element("Tile" + index, row, out var tile);
            RuntimeUi.Stretch(tile, new Vector2(index / 3f, 0f), new Vector2((index + 1) / 3f, 1f),
                new Vector2(7f, 0f), new Vector2(-7f, 0f));
            CosmeticUi.Surface(tile, MenuTheme.Slot, 22f).raycastTarget = false;

            var value = RuntimeUi.Label("Value", tile,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(4f, -92f), new Vector2(-4f, -16f),
                52, TextAnchor.MiddleCenter, MenuTheme.Text);
            value.fontStyle = FontStyle.Bold;

            RuntimeUi.Label("Caption", tile,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(4f, 18f), new Vector2(-4f, 62f),
                24, TextAnchor.MiddleCenter, MenuTheme.Faint).text = caption;
            return value;
        }

        /// One primary, one share, two links. RUN AGAIN keeps the pause menu's weight and colour
        /// so the thumb finds it where it finds RESUME; SHARE sits beside it because the card is
        /// the thing worth sharing and a share button below the fold is a share that never
        /// happens. The two ways OFF the card are text links: they are the rare choice, and the
        /// card breathes better without three stacked slabs under the character.
        void BuildButtons(RectTransform card)
        {
            CardButton("Restart", card, new Vector2(-142f, 200f), new Vector2(516f, 120f),
                MenuTheme.Accent, "RUN AGAIN", 50, MenuTheme.OnAccent,
                () => RestartRequested?.Invoke());

            // SHARE stays ON the card, which is exactly what makes it the one button here that
            // needs the run held - see HoldRun.
            CardButton("Share", card, new Vector2(266f, 200f), new Vector2(268f, 120f),
                MenuTheme.Slot, "SHARE", 40, MenuTheme.Text, ShareTapped);

            // Secondary on purpose: the store stays one visible tap away from every crash - which
            // is what a judge needs (§6.3) - without competing with the next run.
            CardLink("Skins", card, new Vector2(-206f, 78f), "SKINS & SHOP",
                () => StoreRequested?.Invoke());

            // Leaving is a link here for the reason it is a button on the pause menu: back is
            // otherwise the only way off this card that is not another run, and back is not
            // something a player is taught.
            //
            // The rest of the card is inert to taps now (the "tap anywhere" restart is gone), so
            // this link no longer has to defend itself against being read as one. It still runs
            // RunFlow.QuitToMenu -> RunSession.Stop(), which nulls Input and the restart gesture,
            // clears the pending restart and disables the session - strictly before RunSession's
            // own Update (DefaultExecutionOrder 100), so not even a camera hop landing on this
            // frame can start a run behind the menu (the 27 Aug store-tap bug, generalised).
            // SKINS & SHOP above takes the same route.
            CardLink("Quit", card, new Vector2(206f, 78f), "QUIT TO MENU",
                () => QuitRequested?.Invoke(), Sfx.UiBack);
        }

        /// A rounded, filled button placed from the BOTTOM of the card, which is the edge the
        /// thumb measures from.
        static Button CardButton(string name, RectTransform card, Vector2 position, Vector2 size,
            Color fill, string label, int fontSize, Color labelColor, Action onTap)
        {
            RuntimeUi.Element(name, card, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var surface = CosmeticUi.Surface(rect, fill, size.y * 0.32f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button);

            var text = RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter, labelColor);
            text.text = label;
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        /// A text-only tap target, the shape the profile tab's rows already use: the label is the
        /// graphic, so the whole rect takes the tap without a slab behind it.
        static Button CardLink(string name, RectTransform card, Vector2 position, string label,
            Action onTap, Sfx sound = Sfx.UiTap)
        {
            RuntimeUi.Element(name, card, out var rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(388f, 72f);

            var text = RuntimeUi.Label("Label", rect, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, 32, TextAnchor.MiddleCenter, MenuTheme.Dim);
            text.text = label;
            text.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() => onTap());
            RuntimeUi.TapSound(button, sound);
            return button;
        }

        /// A full-width line of the card, measured down from its top edge - the card's one layout
        /// idiom, so a band can be moved by changing one number.
        static Text Band(string name, RectTransform card, float top, float height, int fontSize, Color color)
        {
            return RuntimeUi.Label(name, card,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(CardPad, -top - height), new Vector2(-CardPad, -top),
                fontSize, TextAnchor.MiddleCenter, color);
        }
    }
}
