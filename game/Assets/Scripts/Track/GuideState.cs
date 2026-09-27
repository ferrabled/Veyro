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

    /// One screen of the guide: a heading, an optional illustration and the copy under it. The
    /// copy is hand-wrapped with newlines because the mapping rows are a two-level list, not a
    /// paragraph - and because the slot under the illustration is a fixed height, so the line
    /// count is part of the content (GuideStateTests pins it).
    public readonly struct GuidePage
    {
        public readonly string Title;
        public readonly string Body;

        /// Key of the frame sequence shown above the body - `tilt` loads Art/Guide/tilt_01 …
        /// (see GuideIllustration) - or null for a page that is all copy and gets the height.
        public readonly string Illustration;

        /// Frames in the sequence, at least one when Illustration is set. One frame is a still;
        /// more cycle. Must match the files that exist - docs/GUIDE_ILLUSTRATIONS.md is the brief.
        public readonly int FrameCount;

        /// The instruction shown if this page's illustration sequence cannot load.
        public readonly string Caption;

        public GuidePage(string title, string body) : this(title, body, null, 0, null) { }

        public GuidePage(string title, string body, string illustration, int frameCount, string caption)
        {
            Title = title;
            Body = body;
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
    /// Illustrated pages get at most nine hand-wrapped lines of at most forty characters: the
    /// body sits in a fixed 370 px band under an 804x502 picture on a 1300-tall card, at 34 px
    /// (FirstRunGuide). Copy that grows past that overlaps the button on device and nowhere
    /// else, which is why GuideStateTests counts the lines.
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

        public static readonly GuidePage[] Pages =
        {
            // No picture: this page is the choice, and it gets the full height for the copy.
            new GuidePage("TWO WAYS TO PLAY",
                "TILT & TOUCH\n" +
                "  Hold the phone and tilt it to steer.\n" +
                "  Tap to jump. Works on every phone.\n" +
                "\n" +
                "CAMERA (BETA)\n" +
                "  Prop the phone up and play hands-free:\n" +
                "  your body is the controller. Pick it\n" +
                "  and it walks you through the setup,\n" +
                "  and hands the run back to tilt & touch\n" +
                "  if the camera cannot keep up.\n" +
                "\n" +
                "You can change your mind every time\n" +
                "you come back to this screen."),

            new GuidePage("TILT & TOUCH",
                "Hold the phone upright in both hands.\n" +
                "\n" +
                "TILT left or right to change lane.\n" +
                "TAP anywhere to jump.\n" +
                "SWIPE DOWN to slide.\n" +
                "\n" +
                "The runner runs by itself - all you\n" +
                "do is steer, jump and slide.",
                TiltIllustration, 4, "tilt the phone to steer, tap to jump"),

            new GuidePage("CAMERA MODE",
                "Prop the phone upright - a stand, or\n" +
                "leaning on something - front camera\n" +
                "facing you. Step back about 1.5-2 m\n" +
                "so you are in frame chest-up.\n" +
                "\n" +
                "STEP left or right to change lane.\n" +
                "HOP to jump. CROUCH to slide.\n" +
                "Step out of frame and the run pauses;\n" +
                "raise your right hand to come back.",
                CameraIllustration, 5, "prop the phone up, step back, step to steer"),

            new GuidePage("DURING A RUN",
                "Everyone runs today's track: the Daily\n" +
                "Run is seeded by the date, so scores\n" +
                "compare with a friend's.\n" +
                "\n" +
                "Coins add to your score; a chain of\n" +
                "them builds a combo, a miss resets it.\n" +
                "\n" +
                "II (bottom left) or back pauses:\n" +
                "resume, restart or leave from there.",
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
