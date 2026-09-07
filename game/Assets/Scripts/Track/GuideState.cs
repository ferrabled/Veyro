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

    /// One screen of the guide: a heading and the copy under it. The copy is hand-wrapped with
    /// newlines because the mapping rows are a two-level list, not a paragraph.
    public readonly struct GuidePage
    {
        public readonly string Title;
        public readonly string Body;

        public GuidePage(string title, string body)
        {
            Title = title;
            Body = body;
        }
    }

    /// The first-run guide's content and paging, engine-free like PauseState and ScoreState so
    /// the parts that can be wrong without anyone noticing - "does skipping stop it coming back",
    /// "does NEXT run off the end of the last page" - are unit tested rather than tapped through
    /// on a phone once.
    ///
    /// Content lives here rather than in the screen because it is data: three pages, in order,
    /// covering the choice the player is about to make and the controls they will use. What it
    /// deliberately does NOT cover is camera mode's own setup - the speed gate, the permission
    /// ask, the orientation probe and "I can see you" are staged on screen by CameraStaging when
    /// the player picks camera, and saying it twice would make the second telling the wrong one.
    public sealed class GuideState
    {
        /// PlayerPrefs key for "this player has been shown the guide".
        public const string SeenKey = "veyro.seen_guide";

        /// What SeenKey holds once the guide has been shown.
        public const int SeenValue = 1;

        public static readonly GuidePage[] Pages =
        {
            new GuidePage("TWO WAYS TO PLAY",
                "TILT & TOUCH\n" +
                "  Hold the phone and tilt it to\n" +
                "  steer. Tap to jump. Works on\n" +
                "  every phone.\n" +
                "\n" +
                "CAMERA (BETA)\n" +
                "  Prop the phone up and play\n" +
                "  hands-free - your body is the\n" +
                "  controller. Pick it and it walks\n" +
                "  you through the setup, and it\n" +
                "  hands the run back to tilt &\n" +
                "  touch if the camera cannot\n" +
                "  keep up.\n" +
                "\n" +
                "You can change your mind every\n" +
                "time you come back to this screen."),

            new GuidePage("HOW YOU MOVE",
                "The runner runs by itself. All you\n" +
                "do is steer, jump and slide.\n" +
                "\n" +
                "STEER\n" +
                "  tilt the phone, or lean your body\n" +
                "  the runner moves to that lane\n" +
                "\n" +
                "JUMP\n" +
                "  tap anywhere, or hop\n" +
                "\n" +
                "SLIDE\n" +
                "  swipe down, or crouch\n" +
                "  nothing on the track needs it yet\n" +
                "\n" +
                "Touch keeps working in camera\n" +
                "mode - a tap still jumps."),

            new GuidePage("DURING A RUN",
                "Everyone runs the same track\n" +
                "today: the Daily Run is seeded by\n" +
                "the date, so your score and a\n" +
                "friend's are comparable.\n" +
                "\n" +
                "Coins add to your score. Taking\n" +
                "them one after another builds a\n" +
                "combo multiplier - miss one and\n" +
                "the combo starts over.\n" +
                "\n" +
                "The II button, bottom left, pauses.\n" +
                "So does the back button — and in\n" +
                "camera mode, stepping out of\n" +
                "frame. Raise your right hand to\n" +
                "come back: 3, 2, 1, run. From the\n" +
                "pause screen you can also resume,\n" +
                "restart, or leave for this menu.")
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
