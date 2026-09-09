using MotionRunner.Core;
using UnityEngine;

namespace MotionRunner.Menu
{
    /// Top-down placement inside a page: each card asks for a height and is handed a full-width
    /// slot under the previous one.
    ///
    /// Deliberately not a UGUI layout group. A layout group would make every card's height a
    /// negotiation with its contents and its siblings, which is how a menu built from code stops
    /// being predictable; here a card is exactly as tall as it says it is, and reordering the
    /// screen is reordering the calls. It is also what makes the home page's transparent window
    /// onto the live run expressible at all - a gap is just a height nobody drew in.
    public sealed class MenuStack
    {
        readonly Transform _parent;
        readonly float _padding;
        float _y;

        /// The gap goes BETWEEN cards, so the first one is placed at the top offset exactly. A
        /// "have I placed anything yet" flag rather than a test on _y: the top offset is itself a
        /// negative y, so any rule phrased in terms of _y makes topOffset 0 and topOffset 12
        /// behave differently for no reason a caller could guess.
        bool _placed;

        public MenuStack(Transform parent, float topOffset = 0f, float padding = MenuTheme.SidePadding)
        {
            _parent = parent;
            _padding = padding;
            _y = -topOffset;
        }

        /// Distance from the top of the page consumed so far, as a negative offset - the y a
        /// caller needs when it wants to place something against the stack by hand.
        public float Y => _y;

        /// The next slot down. The returned RectTransform is anchored to the top of the page and
        /// stretched between the side margins, so a card only ever positions things inside it.
        public RectTransform Add(string name, float height, float gap = MenuTheme.CardGap)
        {
            if (_placed) _y -= gap;
            _placed = true;

            RuntimeUi.Element(name, _parent, out var rect);
            RuntimeUi.Stretch(rect,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(_padding, _y - height), new Vector2(-_padding, _y));

            _y -= height;
            return rect;
        }

        /// Leaves a gap nothing is drawn in - on the home page, the window the live run shows
        /// through.
        public void Skip(float height, float gap = MenuTheme.CardGap)
        {
            if (_placed) _y -= gap;
            _placed = true;
            _y -= height;
        }
    }
}
