using MotionRunner.Core;
using UnityEngine;

namespace MotionRunner.Menu
{
    /// The left tab. A frame around StoreCatalogView and nothing else - the catalog owns the store
    /// seam, this owns where it sits.
    ///
    /// It is the one place commerce lives now: the result screen's SHOP button leaves the run and
    /// selects this tab (RunFlow.QuitToMenu), which is also what makes RESTORE PURCHASES reachable
    /// without having crashed first - a judge opening the app cold finds it in two taps (§6.3).
    public sealed class ShopPage : MenuPage
    {
        StoreCatalogView _catalog;

        protected override void Build()
        {
            // A scrim, unlike the home page: a price list is read, not glanced at, and the live
            // run behind it would be moving under the text the whole time.
            RuntimeUi.Panel("Scrim", Root, MenuTheme.Scrim);

            _catalog = Menu.Catalog;
            _catalog.ViewSeason=Menu.ShowSeason;
            _catalog.PreviewCharacter=Menu.ShowCosmetic;
            _catalog.Build(Root);
        }

        public override void OnShown() => _catalog?.Refresh();
    }
}
