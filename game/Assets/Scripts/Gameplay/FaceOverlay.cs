using MotionRunner.CameraInput;
using MotionRunner.Core;
using MotionRunner.Inputs;
using MotionRunner.Menu;
using MotionRunner.Pose;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Gameplay
{
    /// A small code-drawn panel that shows the player where the camera thinks they are.
    ///
    /// It represents the PLAYER, not the runner: a teal person glyph on a paper card,
    /// independent of the equipped outfit. Nobody should be able to
    /// mistake the glyph for the character they are steering — one is a picture of the person
    /// standing in the room, the other is the thing on the road.
    ///
    /// What it answers, which nothing on the phone answered before: **did I not move far enough,
    /// or did I move and the game not follow?** Those are the two halves of every camera-steering
    /// report so far and a runner that stays in the middle lane looks identical either way. So the
    /// panel draws the three lane zones to scale, marks the thresholds, puts the glyph at the
    /// player's live displacement and highlights the lane actually held. A glyph that never
    /// reaches the tick is the first problem; a glyph past the tick with no band lit is the
    /// second.
    ///
    /// ---- mirroring: exactly once, and not here -----------------------------------------------
    ///
    /// A glyph that moves the wrong way is worse than no glyph. The mirror is already applied,
    /// once, upstream: CameraFeed.MirrorForSelfie mirrors the upright frame for the front camera
    /// (FrameOrientation.ForCamera's mirrorHorizontally), so the whole pipeline downstream already
    /// works in the player's own left/right — stepping to their right increases the observation's
    /// x, which is why a positive FaceSteering axis means the right lane. Since the player is
    /// looking at the screen, their right IS the screen's right, so this panel draws deflection
    /// straight through with no flip of its own. Flipping again here would be the classic
    /// double-mirror: correct-looking in isolation and backwards against the game.
    ///
    /// The coupling is deliberate. If MirrorForSelfie were ever turned off, steering itself would
    /// invert (that is precisely the orientation-twin bug of 30 Aug), and this panel would invert
    /// with it — telling the truth about a broken build rather than hiding it.
    ///
    /// ---- two places, one component -----------------------------------------------------------
    ///
    /// * During a camera run (Attach): small, top-centre under the score readout, semi-transparent,
    ///   clear of both the track's near action area and the bottom-left pause button. Driven by the
    ///   run's own CameraFaceInput, so it shows what the runner is obeying and never a second
    ///   opinion.
    /// * While staging the camera (Framing): larger, on the mode picker and on the pause menu's
    ///   resume wait — framing yourself is exactly when you need to see this. No run is polling the
    ///   camera yet, so the panel owns a CameraFaceInput and ticks it. That makes staging a
    ///   rehearsal: stand still and the glyph centres, step sideways and the band lights, all
    ///   before a run is on the line.
    ///
    /// Rule 2 holds: this is UI reading an IGameInput adapter's published outputs, exactly like
    /// the HUD reads the score. It never touches the camera, the model or a tensor — the rig is
    /// held only to read the already-completed FaceObservation for the telemetry line.
    ///
    /// All drawing is UGUI built from code on the shared RuntimeUi plumbing (rule 1), and every
    /// element is written only when its value has actually moved: a still player costs a handful
    /// of float compares per frame.
    public sealed class FaceOverlay : MonoBehaviour
    {
        // ---- placement -----------------------------------------------------------------------

        /// Top-centre, below the score / coins / mode rows (which end at y -198) and far above the
        /// pause button. 440x180 is ~4% of the portrait screen.
        public static readonly Vector2 HudSize = new Vector2(440f, 180f);
        public static readonly Vector2 HudPosition = new Vector2(0f, -320f);
        static readonly Vector2 HudAnchor = new Vector2(0.5f, 1f);

        /// On the result card (camera mode only): centre-anchored, 20 px above the card's top
        /// edge. The card is 1420 tall and centred (RunHud), so its top is at +710; the panel's
        /// centre at +820 puts it at +730..+910 - clear of the masthead and the score it used to
        /// cover at HudPosition (PR #5 review), 50 px under the top of a 16:9 screen and well
        /// under any cutout on a taller one (the 6T's teardrop ends ~80 px in on a 2120-tall
        /// design space whose top is +1060).
        public static readonly Vector2 ResultPosition = new Vector2(0f, 820f);
        static readonly Vector2 ResultAnchor = new Vector2(0.5f, 0.5f);

        /// Bigger while staging, where there is nothing else to look at and the whole point is to
        /// get yourself into frame.
        public static readonly Vector2 StagingSize = new Vector2(700f, 280f);

        /// The gap on the mode picker between the subtitle (which ends at +460 from centre) and the
        /// top of the TILT & TOUCH button (+55).
        public static readonly Vector2 PickerPosition = new Vector2(0f, 230f);

        /// Above the pause card (which tops out at +450), over the dimmed backdrop: the card's own
        /// rows are full.
        public static readonly Vector2 PausePosition = new Vector2(0f, 620f);

        /// Canvas stack, alongside RuntimeUi's existing convention (HUD 0, picker 100, guide 120,
        /// pause 150, store 200): above the screen it belongs to, below whatever may cover it.
        public const int HudSortingOrder = 50;
        public const int PickerSortingOrder = 110;
        public const int PauseSortingOrder = 160;

        // The person stays distinct from the runner and its cosmetic outfit.
        static readonly Color BackdropColor = MenuTheme.Card;
        static readonly Color BandIdleColor = MenuTheme.Empty;
        static readonly Color BandHeldColor = MenuTheme.Owned;
        static readonly Color EnterTickColor = MenuTheme.Text;
        static readonly Color HoldTickColor = MenuTheme.Faint;
        static readonly Color CaptionColor = MenuTheme.Text;

        static readonly Color GlyphTrackingColor = MenuTheme.Text;
        static readonly Color GlyphLostColor = MenuTheme.Accent;
        static readonly Color GlyphTooFarColor = MenuTheme.Gold;

        /// A lost face re-calibrates the framing preview's neutral rather than keeping a neutral
        /// captured before the player was in position. Only in framing mode: mid-run, resetting
        /// the neutral is RunFlow's decision (it does it on a resume) and not an overlay's.
        const float FramingRecalibrateSeconds = 0.75f;

        /// Below this nothing is redrawn — a fraction of a pixel of glyph movement.
        const float MoveEpsilon = 0.004f;

        enum Health
        {
            Tracking,
            Lost,
            TooFar
        }

        // ---- wiring ----

        FaceTrackingRig _rig;

        /// The adapter whose Steering this panel reads. Owned (and ticked) in framing mode; in run
        /// mode it is the session's, already ticked by RunSession, and must not be ticked again.
        CameraFaceInput _input;
        bool _ownsInput;
        bool _framing;

        CameraTelemetry _telemetry;

        int _reportedLane;
        bool _hasReportedLane;
        int _framingLane;
        float _lostFor;

        // ---- built elements ----

        RectTransform _panel;
        bool _onResultCard;
        RectTransform _glyph;
        Image[] _glyphParts;
        Image[] _bands;
        Text _caption;
        GameObject _hintMarks;
        string _captionOverride;

        float _innerHalfWidth;
        float _stripCenterY;
        float _liftRange;
        float _glyphHeight;

        // ---- shown state, so nothing is written twice ----

        // Deliberately impossible values rather than NaN: every comparison below is "has this moved
        // more than an epsilon", and NaN fails that test, so a NaN seed would skip the first draw.
        float _shownDeflection = 999f;
        float _shownLift = 999f;
        int _shownLane = int.MinValue;
        Health _shownHealth = (Health)(-1);
        string _shownCaption = null;

        /// During a camera run: reads the session's own adapter, so the panel and the runner can
        /// never disagree.
        public static FaceOverlay Attach(FaceTrackingRig rig, CameraFaceInput input)
        {
            var overlay = New(rig, input, false);
            // Anchored to the top of the screen, where the score rows are, rather than to the
            // centre: it belongs to the readout, not to the middle of the track.
            overlay.Build(HudSortingOrder, HudAnchor, HudPosition, HudSize);
            return overlay;
        }

        /// Run variant only. Moves the panel between its run slot (under the score rows) and its
        /// result-card slot (above the card), where RunFlow.SyncOverlay keeps it up so a camera
        /// player can see they are in frame before hopping the next run in. On the card the
        /// lane bands follow the live axis, like the framing preview: there is no runner
        /// committed to a lane any more, and a band stuck on the crash lane would say the camera
        /// had frozen. Cheap to call every frame - two rect writes, only when the state flips.
        public void Place(bool onResultCard)
        {
            if (_panel == null || _onResultCard == onResultCard) return;
            _onResultCard = onResultCard;
            _panel.anchorMin = _panel.anchorMax = onResultCard ? ResultAnchor : HudAnchor;
            _panel.anchoredPosition = onResultCard ? ResultPosition : HudPosition;
            if (onResultCard) _hasReportedLane = false;
        }

        /// While a screen is staging the camera. No run is polling the camera yet, so the overlay
        /// owns the adapter and ticks it itself. Positions are centre-anchored, because both hosts
        /// lay their own content out from the middle of the screen.
        public static FaceOverlay Framing(FaceTrackingRig rig, int sortingOrder, Vector2 position)
        {
            var overlay = New(rig, new CameraFaceInput(rig), true);
            overlay.Build(sortingOrder, new Vector2(0.5f, 0.5f), position, StagingSize);
            return overlay;
        }

        static FaceOverlay New(FaceTrackingRig rig, CameraFaceInput input, bool framing)
        {
            var go = new GameObject("FaceOverlay");
            var overlay = go.AddComponent<FaceOverlay>();
            overlay._rig = rig;
            overlay._input = input;
            overlay._ownsInput = framing;
            overlay._framing = framing;
            overlay._telemetry = new CameraTelemetry(framing ? "framing" : "run");
            return overlay;
        }

        /// The lane the runner is actually committed to. RunFlow writes it every frame during a
        /// run; while framing there is no runner, so the panel works the lane out from the axis
        /// with LaneSelector's own pure rule — the same function, the same thresholds.
        public void ReportLane(int lane)
        {
            _reportedLane = lane;
            _hasReportedLane = true;
        }

        /// Hidden before it is destroyed, like every other screen here: Destroy only lands at the
        /// end of the frame.
        public void Dismiss()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        /// The resume confirm's affordance on the pause screen: a caption saying what to do
        /// ("stand still", "hop twice", "one more hop"), and - while hops are what is wanted - an
        /// up-chevron either side of the stickman's head, the way the guide draws a hop. The
        /// chevrons ride on the glyph, so they jump with it when the player does. A null caption
        /// takes the whole hint down.
        ///
        /// Called every frame of the confirm wait, so a repeat of what is already shown costs
        /// nothing: the caption is only re-laid out when it changes.
        public void ShowHopHint(string caption, bool marks)
        {
            if (marks && _hintMarks == null)
            {
                // Built like the glyph's own parts, but kept out of _glyphParts: the hint keeps
                // its instructional colour instead of turning red with a lost face - the caption
                // already narrates loss, and a red prompt reads as "stop".
                GameObject root = RuntimeUi.Element("HopHint", _glyph, out RectTransform rect);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                Chevron(rect, -_glyphHeight * 0.16f);
                Chevron(rect, _glyphHeight * 0.16f);
                _hintMarks = root;
            }

            if (_hintMarks != null && _hintMarks.activeSelf != marks) _hintMarks.SetActive(marks);
            if (caption == _captionOverride) return;
            _captionOverride = caption;
            _shownCaption = null; // force the caption redraw
        }

        /// One "^" at head height, `x` from the glyph's centre line: two bars leaning into each
        /// other, the same stroke weight as the stickman's limbs.
        void Chevron(RectTransform parent, float x)
        {
            float stroke = _glyphHeight * 0.024f;
            float length = _glyphHeight * 0.075f;
            float half = length * 0.5f * 0.7071f; // each bar's centre, half a 45-degree stroke out
            float y = _glyphHeight * 0.20f;
            foreach (int side in new[] { -1, 1 })
            {
                Image bar = Part("Chevron", new Vector2(x + side * half, y),
                    new Vector2(stroke, length), side * 45f, parent);
                bar.color = EnterTickColor;
            }
        }

        // ---- per frame -----------------------------------------------------------------------

        void Update()
        {
            FaceSteering steering = _input?.Steering;
            if (steering == null) return;

            if (_ownsInput)
            {
                _input.Tick();

                // Re-calibrate on a real loss so the preview's neutral is where the player is
                // standing now, not wherever they were when the panel appeared.
                //
                // HasPosition, not IsTracking: a face the detector is following at a marginal score
                // has not gone anywhere, and throwing the calibration away because it blurred is
                // how a preview would fight a player who is standing still. Only losing the
                // position outright (nothing above FaceSteering.MinPositionScore) counts. The glyph
                // colour still follows IsTracking, so the panel says "not sure" the moment the
                // score drops even while it keeps the neutral it has.
                if (steering.HasPosition) _lostFor = 0f;
                else
                {
                    _lostFor += Time.unscaledDeltaTime;
                    if (_lostFor >= FramingRecalibrateSeconds)
                    {
                        steering.Reset();
                        _lostFor = 0f;
                    }
                }
            }

            int lane = _hasReportedLane
                ? _reportedLane
                : _framingLane = LaneSelector.LaneFor(steering.MoveAxis, _framingLane);

            Draw(steering, lane);
            _telemetry.Sample(_rig != null ? _rig.Latest : default, steering, lane,
                _rig != null ? _rig.DetectorScore : 0f);
        }

        void Draw(FaceSteering steering, int lane)
        {
            // Lost is drawn from IsTracking (the confident tier), so the panel says "not sure" the
            // frame the score drops. Combined with the loss hold in FaceSteering, that produces the
            // reading the hold needs to be legible: a RED GLYPH THAT HAS STOPPED MOVING, with the
            // band it last reached still lit. The runner is still in that lane and the camera is no
            // longer the reason — which is a different picture from a glyph that drifts back to the
            // centre while the player stands still, and that ambiguity is what the old decay left
            // on screen.
            Health health = !steering.IsTracking ? Health.Lost
                : steering.IsTooFar ? Health.TooFar
                : Health.Tracking;

            float deflection = steering.Deflection;
            float lift = steering.Lift;

            if (Mathf.Abs(deflection - _shownDeflection) > MoveEpsilon ||
                Mathf.Abs(lift - _shownLift) > MoveEpsilon)
            {
                _shownDeflection = deflection;
                _shownLift = lift;
                _glyph.anchoredPosition = new Vector2(
                    deflection * _innerHalfWidth,
                    _stripCenterY + lift * _liftRange);
            }

            if (health != _shownHealth)
            {
                _shownHealth = health;
                Color color = health == Health.Lost ? GlyphLostColor
                    : health == Health.TooFar ? GlyphTooFarColor
                    : GlyphTrackingColor;
                for (int i = 0; i < _glyphParts.Length; i++) _glyphParts[i].color = color;
            }

            if (lane != _shownLane)
            {
                _shownLane = lane;
                for (int i = 0; i < _bands.Length; i++)
                    _bands[i].color = i - 1 == lane ? BandHeldColor : BandIdleColor;
            }

            string caption = CaptionFor(health, steering);
            if (caption != _shownCaption)
            {
                _shownCaption = caption;
                _caption.text = caption;
            }
        }

        /// Exceptions and gestures only, so a normal run has a silent panel.
        string CaptionFor(Health health, FaceSteering steering)
        {
            if (health == Health.TooFar) return "too far — step closer";
            if (health == Health.Lost) return _framing ? "can't see you" : "no face";

            // The gesture prompt only replaces the idle chatter: a health problem above always
            // outranks it, because "hop twice" is bad advice to someone the camera cannot see.
            if (_captionOverride != null) return _captionOverride;

            if (steering.IsSlideActive) return "crouch";
            if (steering.IsJumpActive) return "hop";
            return _framing ? "step left or right to try a lane" : string.Empty;
        }

        // ---- construction --------------------------------------------------------------------

        void Build(int sortingOrder, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RuntimeUi.PortraitCanvas(gameObject, sortingOrder);

            // k scales every dimension off the HUD variant, so one set of proportions serves both
            // sizes and a number tuned at one size means the same thing at the other.
            float k = size.y / HudSize.y;

            // Placed inside the safe area, like the HUD rows it sits under: the top-anchored HUD
            // position moves down with them on a phone with a cutout.
            var safe = RuntimeUi.SafeRoot(transform);
            GameObject panel = RuntimeUi.Element("Panel", safe, out RectTransform panelRect);
            panelRect.anchorMin = anchor;
            panelRect.anchorMax = anchor;
            panelRect.anchoredPosition = position;
            panelRect.sizeDelta = size;
            _panel = panelRect;

            // Nothing here is ever a raycast target: the panel sits on its own canvas above a
            // screen with live buttons, and a backdrop that eats taps is how an overlay stops
            // being free.
            var backdrop = panel.AddComponent<Image>();
            backdrop.color = BackdropColor;
            backdrop.raycastTarget = false;

            var title = RuntimeUi.Label("CameraLabel", panel.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f*k, -19f*k), new Vector2(-12f*k, -3f*k),
                Mathf.RoundToInt(14f*k), TextAnchor.MiddleCenter, MenuTheme.Dim);
            title.text = "YOU · CAMERA";

            // Padding wide enough that the glyph's arms stay inside the backdrop when it sits at
            // full deflection, which is what lets the glyph's centre line up exactly with the
            // threshold ticks instead of being squeezed inside them.
            float pad = 24f * k;
            float innerWidth = size.x - 2f * pad;
            _innerHalfWidth = innerWidth * 0.5f;

            float stripHeight = size.y * 0.52f;
            _stripCenterY = size.y * 0.04f;
            _liftRange = size.y * 0.10f;

            BuildBands(panel.transform, innerWidth, stripHeight, k, DeadZone());
            BuildGlyph(panel.transform, size.y);

            _caption = RuntimeUi.Label("Caption", panel.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(8f, size.y * 0.01f), new Vector2(-8f, size.y * 0.20f),
                Mathf.RoundToInt(size.y * 0.135f), TextAnchor.LowerCenter, CaptionColor);
            _caption.text = string.Empty;
        }

        /// The dead zone the axis is rescaled past, read off the steering this panel is showing
        /// rather than assumed, because it is what converts LaneSelector's axis thresholds into
        /// the deflection units the panel is drawn in.
        float DeadZone() => _input?.Steering?.DeadZone ?? 0.15f;

        /// Three zones drawn to scale, with the thresholds marked.
        ///
        /// The widths are not thirds: LaneSelector commits to a side lane at |axis| >= 0.5, and
        /// FaceSteering rescales the axis past its dead zone, so a side lane starts at
        /// DeadZone + 0.5 * (1 - DeadZone) = 0.575 of full deflection. The centre zone is
        /// therefore 57.5% of the range and each side zone 21.25% — which is itself worth seeing,
        /// because "the middle lane is more than half of everything you can reach" explains a lot
        /// of how camera steering feels.
        void BuildBands(Transform parent, float innerWidth, float stripHeight, float k,
            float steeringDeadZone)
        {
            float enter = steeringDeadZone + LaneSelector.EnterThreshold * (1f - steeringDeadZone);
            float hold = steeringDeadZone + LaneSelector.HoldThreshold * (1f - steeringDeadZone);

            float gap = 3f * k;
            float sideWidth = (1f - enter) * innerWidth * 0.5f - gap;
            float centreWidth = enter * innerWidth - gap;

            _bands = new Image[3];
            _bands[0] = Band(parent, "BandLeft", -(1f + enter) * 0.5f * _innerHalfWidth,
                sideWidth, stripHeight);
            _bands[1] = Band(parent, "BandCentre", 0f, centreWidth, stripHeight);
            _bands[2] = Band(parent, "BandRight", (1f + enter) * 0.5f * _innerHalfWidth,
                sideWidth, stripHeight);

            // The enter ticks land on the band boundaries on purpose: the edge a player has to
            // cross and the line they see are then the same thing. The hold ticks sit inside the
            // centre band, where a lane is given back.
            Tick(parent, "EnterLeft", -enter * _innerHalfWidth, 3f * k, stripHeight, EnterTickColor);
            Tick(parent, "EnterRight", enter * _innerHalfWidth, 3f * k, stripHeight, EnterTickColor);
            Tick(parent, "HoldLeft", -hold * _innerHalfWidth, 2f * k, stripHeight * 0.55f, HoldTickColor);
            Tick(parent, "HoldRight", hold * _innerHalfWidth, 2f * k, stripHeight * 0.55f, HoldTickColor);
        }

        Image Band(Transform parent, string name, float x, float width, float height)
        {
            GameObject go = RuntimeUi.Element(name, parent, out RectTransform rect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = new Vector2(x, _stripCenterY);
            rect.sizeDelta = new Vector2(width, height);

            var image = go.AddComponent<Image>();
            image.color = BandIdleColor;
            image.raycastTarget = false;
            return image;
        }

        void Tick(Transform parent, string name, float x, float width, float height, Color color)
        {
            GameObject go = RuntimeUi.Element(name, parent, out RectTransform rect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = new Vector2(x, _stripCenterY);
            rect.sizeDelta = new Vector2(width, height);

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// Head, arms, torso, two legs — five rectangles under one RectTransform, so moving the
        /// player costs exactly one transform write per frame however many pieces the glyph has.
        void BuildGlyph(Transform parent, float height)
        {
            _glyphHeight = height; // the hop hint is built lazily, to the same scale
            GameObject go = RuntimeUi.Element("Glyph", parent, out RectTransform rect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = new Vector2(0f, _stripCenterY);
            rect.sizeDelta = Vector2.zero;
            _glyph = rect;

            _glyphParts = new Image[5];
            _glyphParts[0] = Part("Head", new Vector2(0f, height * 0.20f),
                new Vector2(height * 0.105f, height * 0.105f), 0f);
            _glyphParts[1] = Part("Arms", new Vector2(0f, height * 0.095f),
                new Vector2(height * 0.20f, height * 0.028f), 0f);
            _glyphParts[2] = Part("Torso", new Vector2(0f, height * 0.055f),
                new Vector2(height * 0.033f, height * 0.19f), 0f);
            _glyphParts[3] = Part("LegLeft", new Vector2(-height * 0.026f, -height * 0.105f),
                new Vector2(height * 0.028f, height * 0.13f), 20f);
            _glyphParts[4] = Part("LegRight", new Vector2(height * 0.026f, -height * 0.105f),
                new Vector2(height * 0.028f, height * 0.13f), -20f);
        }

        Image Part(string name, Vector2 position, Vector2 size, float rotation,
            Transform parent = null)
        {
            GameObject go = RuntimeUi.Element(name, parent != null ? parent : _glyph,
                out RectTransform rect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            if (rotation != 0f) rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var image = go.AddComponent<Image>();
            image.color = GlyphTrackingColor;
            image.raycastTarget = false;
            return image;
        }
    }
}
