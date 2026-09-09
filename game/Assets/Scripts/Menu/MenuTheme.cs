using UnityEngine;

namespace MotionRunner.Menu
{
    /// The menu's palette and its layout constants, in one file so a card can be moved, resized
    /// or restyled without hunting the same colour through five classes. Every menu component
    /// reads from here and defines no colours of its own.
    ///
    /// The colours are the ones the run screens already use (RunHud, the old mode picker), kept
    /// deliberately: the menu and the game are one product, and the first art pass (T-006) is
    /// where all of it changes together.
    public static class MenuTheme
    {
        // ---- ink ----
        public static readonly Color Text = new Color(0.94f, 0.96f, 1f);
        public static readonly Color Dim = new Color(0.62f, 0.68f, 0.80f);
        public static readonly Color Faint = new Color(0.45f, 0.50f, 0.62f);
        public static readonly Color Accent = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Gold = new Color(1f, 0.86f, 0.22f);

        /// Text on top of an accent-filled button.
        public static readonly Color OnAccent = new Color(0.08f, 0.06f, 0.04f);

        // ---- surfaces ----

        /// A card. Very nearly opaque, with a couple of percent left to sit the furniture *on* the
        /// scene rather than float above it.
        ///
        /// The first device build had these at 0.92 and it was a mistake: the attract run moving
        /// behind the season-pass and streak cards put lane lines and a red obstacle straight
        /// through the text, and a card you have to read past a moving track is a card nobody
        /// reads. The window in the middle of the home page is where the game shows through; the
        /// cards are where it does not.
        public static readonly Color Card = new Color(0.11f, 0.13f, 0.20f, 0.98f);

        /// A row or slot inside a card. Opaque: rows are the tap targets and the price list.
        public static readonly Color Slot = new Color(0.18f, 0.21f, 0.30f, 1f);

        /// An empty slot - a day with no stamp, the unfilled part of a bar. Lifted clear of Card
        /// rather than a shade of it: on device the two were near enough to identical that the
        /// unstamped days read as empty space instead of as waiting slots.
        public static readonly Color Empty = new Color(0.21f, 0.24f, 0.33f, 1f);

        /// Something the player owns.
        public static readonly Color Owned = new Color(0.24f, 0.42f, 0.28f, 1f);

        /// The header and tab bars, which frame the live scene rather than sitting on it.
        public static readonly Color Bar = new Color(0.07f, 0.08f, 0.13f, 0.96f);

        /// Behind a page that is all text - the shop and the profile. The attract run keeps
        /// playing underneath, but it is not something to read a price list through, and at 0.90
        /// on device it very much was: the track was legible through both pages.
        public static readonly Color Scrim = new Color(0.05f, 0.06f, 0.10f, 0.97f);

        // ---- layout (1080x1920 design space, RuntimeUi.ReferenceResolution) ----

        /// The title block at the top, shared by every tab.
        public const float HeaderHeight = 200f;

        /// The three-tab bar along the bottom.
        public const float TabBarHeight = 176f;

        /// Left and right margin for a card inside a page.
        public const float SidePadding = 24f;

        /// Space between two stacked cards.
        public const float CardGap = 14f;

        /// Margin inside a card, between its edge and its contents.
        public const float CardPadding = 28f;
    }
}
