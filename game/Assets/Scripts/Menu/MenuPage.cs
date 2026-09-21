using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// Which of the three screens the tab bar is on. The order is the order they sit in the bar,
    /// with Run in the middle where the thumb lands - the point of the layout.
    public enum MenuTab
    {
        Shop = 0,
        Run = 1,
        Profile = 2
    }

    /// One tab's screen. The contract is small on purpose: a page fills the content area, is told
    /// when it comes and goes, and knows nothing about the bar that switches between them or about
    /// the two other pages. Adding a fourth tab is a new subclass plus one row in MainMenu's table.
    ///
    /// Pages are MonoBehaviours because one of them (the home page) has per-frame work - camera
    /// staging - and because a page going away should take its own GameObject with it. The cards
    /// *inside* a page are plain classes: they build once and refresh on demand, which is all a
    /// menu card ever does.
    public abstract class MenuPage : MonoBehaviour
    {
        /// The page's own full-size rect inside the content area.
        public RectTransform Root { get; private set; }

        protected MainMenu Menu { get; private set; }

        /// Built once, by MainMenu. Split from Build so a subclass has its menu and its rect
        /// before any of its own code runs.
        public void Attach(MainMenu menu, Transform content)
        {
            Menu = menu;
            transform.SetParent(content, false);

            Root = gameObject.AddComponent<RectTransform>();
            RuntimeUi.Stretch(Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // A page owns its draw batches. Keep its changing masks/preview geometry out of
            // the shell's header/tab batches, while inheriting the shell's scale and sorting.
            gameObject.AddComponent<Canvas>().overrideSorting=false;
            gameObject.AddComponent<GraphicRaycaster>();

            Build();
        }

        protected abstract void Build();

        /// Shown or hidden by the tab bar. Refreshing on show rather than on a timer is what keeps
        /// the profile's numbers and the shop's prices current without any of them polling: the
        /// only way to see a page is to switch to it.
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible)
            {
                if (visible) OnShown();
                return;
            }

            gameObject.SetActive(visible);
            if (visible) OnShown();
            else OnHidden();
        }

        public virtual void OnShown()
        {
        }

        public virtual void OnHidden()
        {
        }
    }
}
