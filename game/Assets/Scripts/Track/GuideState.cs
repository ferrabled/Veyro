namespace MotionRunner.Track
{
    /// How the one-time guide was left. Both exits mean the same thing to the seen flag - see
    /// GuideState.MarksSeen - but the reason is worth carrying so a later change (a "show me
    /// again next time" checkbox, say) has somewhere to land.
    public enum GuideExit
    {
        /// The player read to the end and dismissed it.
        Completed,

        /// The player skipped it, or backed out with the Android back button.
        Skipped
    }

    /// One line of a guide page. Two shapes, picked by whether the row names a thumbnail:
    ///
    ///   * a STEP - a short verb on a chip ("TILT") and what it does ("left or right to change
    ///     lane"). The mode pages are a mapping from a movement to an action, and a chip per verb
    ///     is that mapping laid out, where the old hand-wrapped paragraph made the player find it;
    ///   * a TILE - a boxed choice with the first frame of an illustration beside it, a heading
    ///     and a sentence or two. The choice page is two of these, one per way of playing.
    ///
    /// Plain text, no newlines: FirstRunGuide wraps to the card and measures what it drew.
    public readonly struct GuideRow
    {
        /// The chip's verb, or the tile's heading.
        public readonly string Key;
        public readonly string Text;

        /// A small pill after a tile's heading ("BETA"), or null.
        public readonly string Tag;

        /// Illustration key whose first frame is the tile's picture, or null for a step.
        public readonly string Thumbnail;

        public GuideRow(string key, string text) : this(key, text, null, null) { }

        public GuideRow(string key, string text, string tag, string thumbnail)
        {
            Key = key;
            Text = text;
            Tag = tag;
            Thumbnail = thumbnail;
        }

        public bool IsTile => Thumbnail != null;
    }

    /// One screen of the guide: a heading, an optional illustration, an optional lead sentence,
    /// the rows, and an optional closing note. Structured rather than one hand-wrapped string so
    /// the screen can give the verbs their own weight and let the copy fill the card's width;
    /// the length budgets in GuideState are the layout contract (GuideStateTests pins them).
    public readonly struct GuidePage
    {
        public readonly string Title;

        /// One or two sentences above the rows, or null.
        public readonly string Lead;

        public readonly GuideRow[] Rows;

        /// A quieter line under the rows, or null.
        public readonly string Note;

        /// Key of the frame sequence shown above the copy - `tilt` loads Art/Guide/tilt_01 …
        /// (see GuideIllustration) - or null for a page that is all copy and gets the height.
        public readonly string Illustration;

        /// Frames in the sequence, at least one when Illustration is set. One frame is a still;
        /// more cycle. Must match the files that exist - docs/GUIDE_ILLUSTRATIONS.md is the brief.
        public readonly int FrameCount;

        /// The instruction shown if this page's illustration sequence cannot load.
        public readonly string Caption;

        public GuidePage(string title, string lead, GuideRow[] rows, string note)
            : this(title, lead, rows, note, null, 0, null) { }

        public GuidePage(string title, string lead, GuideRow[] rows, string note,
            string illustration, int frameCount, string caption)
        {
            Title = title;
            Lead = lead;
            Rows = rows ?? new GuideRow[0];
            Note = note;
            Illustration = illustration;
            FrameCount = frameCount;
            Caption = caption;
        }

        public bool HasIllustration => Illustration != null;
    }

    /// The first-run guide's content and paging, engine-free like PauseState and ScoreState so
    /// the parts that can be wrong without anyone noticing - "does skipping stop it coming back",
    /// "does NEXT run off the end of the last page" - are unit tested rather than tapped through
    /// on a phone once.
    ///
    /// Content lives here rather than in the screen because it is data: four pages, in order -
    /// the choice the player is about to make, one page per way of playing with its picture,
    /// and what a run looks like. What it deliberately does NOT cover is camera mode's own setup
    /// - the speed gate, the permission ask, the orientation probe and "I can see you" are staged
    /// on screen by CameraStaging when the player picks camera, and saying it twice would make
    /// the second telling the wrong one.
    ///
    /// The copy is budgeted in characters, not lines: FirstRunGuide wraps each part to the card
    /// and stacks them by measured height, and the budgets below are what keeps the worst case of
    /// every part clear of the button on an 880x1480 card (the illustrated band is ~530 px, the
    /// choice page's ~1050). FirstRunGuide also reports an overflow it actually measured, and
    /// GuideUiReview fails on one, so a budget that turns out generous is caught at the desk.
    public sealed class GuideState
    {
        /// PlayerPrefs key for "this player has been shown the guide".
        public const string SeenKey = "veyro.seen_guide";

        /// What SeenKey holds once the guide has been shown.
        public const int SeenValue = 1;

        /// Illustration keys. Frame counts match the shipped assets listed in
        /// docs/GUIDE_ILLUSTRATIONS.md §1; update both when changing a sequence.
        public const string TiltIllustration = "tilt";
        public const string CameraIllustration = "camera";
        public const string RunIllustration = "run";

        // ---- the layout contract with FirstRunGuide ----

        /// Steps under a picture: three wrapped rows plus a lead and a note fill the band.
        public const int MaxSteps = 3;

        /// Tiles on a page without a picture: the band splits between them.
        public const int MaxTiles = 2;

        public const int MaxLeadLength = 110;
        public const int MaxNoteLength = 80;

        /// A chip is a verb, sized for the widest one ("SWIPE DOWN").
        public const int MaxKeyLength = 10;

        /// Two lines beside a chip.
        public const int MaxStepTextLength = 64;

        /// Seven lines beside a tile's picture.
        public const int MaxTileTextLength = 160;

        public static readonly GuidePage[] Pages =
        {
            // No picture of its own: this page is the choice, one tile per way of playing, each
            // with the first frame of that mode's own page so the two pictures are met twice.
            new GuidePage("TWO WAYS TO PLAY", null,
                new[]
                {
                    new GuideRow("TILT & TOUCH",
                        "Hold the phone, tilt to steer and tap to jump. Works on every phone.",
                        null, TiltIllustration),
                    new GuideRow("CAMERA",
                        "Prop the phone up and play hands-free: your body is the controller. " +
                        "Setup is guided, and the run hands back to tilt & touch if the camera " +
                        "cannot keep up.",
                        "BETA", CameraIllustration)
                },
                "Pick one on the run screen. You can switch every time you come back."),

            new GuidePage("TILT & TOUCH",
                "Hold the phone upright in both hands. The runner runs by itself.",
                new[]
                {
                    new GuideRow("TILT", "left or right to change lane"),
                    new GuideRow("TAP", "anywhere on the screen to jump"),
                    new GuideRow("SWIPE DOWN", "to slide")
                },
                null,
                TiltIllustration, 4, "tilt the phone to steer, tap to jump"),

            new GuidePage("CAMERA MODE",
                "Prop the phone upright, front camera facing you. Step back about 1.5-2 m so " +
                "you are in frame chest-up.",
                new[]
                {
                    new GuideRow("STEP", "left or right to change lane"),
                    new GuideRow("HOP", "to jump"),
                    new GuideRow("CROUCH", "to slide")
                },
                "Step out of frame and the run pauses; hop twice to come back.",
                CameraIllustration, 5, "prop the phone up, step back, step to steer"),

            new GuidePage("DURING A RUN", null,
                new[]
                {
                    new GuideRow("DAILY", "everyone runs today's track, so scores compare with a friend's"),
                    new GuideRow("COINS", "add to your score; a chain builds a combo, a miss resets it"),
                    new GuideRow("PAUSE", "II (bottom left) or back: resume, restart or leave")
                },
                null,
                RunIllustration, 1, "coins, the combo and the pause button")
        };

        public int Index { get; private set; }

        public GuidePage Page => Pages[Index];
        public bool IsFirst => Index == 0;
        public bool IsLast => Index == Pages.Length - 1;

        /// What the big button says. The last page's label is the dismissal, so the player never
        /// has to hunt for the way out once they have read everything.
        public string PrimaryLabel => IsLast ? "LET'S RUN" : "NEXT";

        /// False on the last page - the caller dismisses instead of paging.
        public bool Next()
        {
            if (IsLast) return false;
            Index++;
            return true;
        }

        /// False on the first page, so the BACK affordance can be hidden rather than dead.
        public bool Back()
        {
            if (IsFirst) return false;
            Index--;
            return true;
        }

        /// Whether the guide is owed on this launch, given whatever is stored under SeenKey.
        /// Anything other than "nothing stored" counts as seen: a flag written by an older
        /// build - or a value nobody expected - must not turn the guide into something that
        /// greets the player every single launch.
        public static bool ShouldShowOnLaunch(int storedFlag) => storedFlag == 0;

        /// Every way out marks the guide seen, skipping included. The alternative - only a full
        /// read counts - means the player who skips it is shown it again tomorrow, and the day
        /// after, which reads as a bug rather than as help. The exit reason is taken and ignored
        /// on purpose: this is the decision, written down where it can be tested.
        public static bool MarksSeen(GuideExit exit) => true;
    }
}
