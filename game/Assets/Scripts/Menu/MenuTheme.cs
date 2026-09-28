using UnityEngine;

namespace MotionRunner.Menu
{
    /// The menu's palette and its layout constants, in one file so a card can be moved, resized
    /// or restyled without hunting the same colour through five classes. Every menu component
    /// reads from here and defines no colours of its own.
    ///
    /// T-006 shares the site's teal/pink/paper anchors with the park and run screens.
    public static class MenuTheme
    {
        // ---- ink ----
        public static readonly Color Text = Art.ParkTheme.Ink;
        public static readonly Color Dim = Art.ParkTheme.Hex(0x456A69);
        public static readonly Color Faint = Art.ParkTheme.Hex(0x5D776D);
        public static readonly Color Accent = Art.ParkTheme.Pink;
        public static readonly Color Gold = Art.ParkTheme.Hex(0xBD8423);

        /// Second and third place on the leaderboard podium, next to Gold for first: a sage
        /// silver and a clay bronze, pulled toward the park palette rather than metallic.
        public static readonly Color Silver = Art.ParkTheme.Hex(0x8FA5A2);
        public static readonly Color Bronze = Art.ParkTheme.Hex(0xB9744A);

        /// Text on top of an accent-filled button.
        public static readonly Color OnAccent = Art.ParkTheme.Paper;

        // ---- surfaces ----

        /// A card. Very nearly opaque, with a couple of percent left to sit the furniture *on* the
        /// scene rather than float above it.
        ///
        /// The first device build had these at 0.92 and it was a mistake: the attract run moving
        /// behind the season-pass and streak cards put lane lines and a red obstacle straight
        /// through the text, and a card you have to read past a moving track is a card nobody
        /// reads. The window in the middle of the home page is where the game shows through; the
        /// cards are where it does not.
        public static readonly Color Card = new Color(0.984f, 0.961f, 0.914f, 0.98f);

        /// A row or slot inside a card. Opaque: rows are the tap targets and the price list.
        public static readonly Color Slot = Art.ParkTheme.Hex(0xE2E9DC);

        /// An empty slot - a day with no stamp, the unfilled part of a bar. Lifted clear of Card
        /// rather than a shade of it: on device the two were near enough to identical that the
        /// unstamped days read as empty space instead of as waiting slots.
        public static readonly Color Empty = Art.ParkTheme.Hex(0xD4DFD0);

        /// Something the player owns.
        public static readonly Color Owned = Art.ParkTheme.Hex(0xA9C7AE);
        public static readonly Color PreviewTop = Art.ParkTheme.Hex(0xB9E5DC);
        public static readonly Color PreviewBottom = Art.ParkTheme.Hex(0xE9EFD5);
        public static readonly Color ItemCard = Art.ParkTheme.Hex(0xFFFCF4);
        public static readonly Color PassGradient = Art.ParkTheme.Hex(0x296461);
        public static readonly Color EmberCard = Art.ParkTheme.Hex(0xFFDEB5);
        public static readonly Color FrostCard = Art.ParkTheme.Hex(0xC4E8F5);
        public static readonly Color Premium = Art.ParkTheme.Hex(0xEEDDAE);

        /// The header and tab bars, which frame the live scene rather than sitting on it.
        public static readonly Color Bar = new Color(0.984f, 0.961f, 0.914f, 0.99f);

        /// Behind a page that is all text - the shop and the profile. The attract run keeps
        /// playing underneath, but it is not something to read a price list through, and at 0.90
        /// on device it very much was: the track was legible through both pages.
        public static readonly Color Scrim = new Color(0.91f, 0.93f, 0.86f, 0.98f);

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
