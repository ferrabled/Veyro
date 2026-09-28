using MotionRunner.Core;
using MotionRunner.Social;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// Draws a PlayerAvatar pattern: a rounded tile in one of eight colour pairs with the critter
    /// on it as a point-filtered texture. The pattern is engine-free (Social); the colours are
    /// here, from the menu palette, so a palette tweak restyles faces without changing them.
    ///
    /// One 7x7 texture per view, rewritten in place when the name changes (a reroll), never
    /// re-created - there is nothing to leak across however many rerolls a player does.
    public sealed class PlayerAvatarView
    {
        /// Tile and ink per palette index. Every pair clears contrast against itself and sits on
        /// the paper card; the list order is part of every existing player's face, so append.
        static readonly Color[] Tiles =
        {
            MenuTheme.PassGradient, // deep teal
            MenuTheme.Accent,       // pink
            MenuTheme.PreviewTop,   // mint
            MenuTheme.Gold,
            MenuTheme.FrostCard,
            MenuTheme.EmberCard,
            MenuTheme.Owned,        // sage
            MenuTheme.Premium       // sand
        };

        static readonly Color[] Inks =
        {
            Art.ParkTheme.Paper,
            Art.ParkTheme.Paper,
            MenuTheme.Text,
            Art.ParkTheme.Paper,
            MenuTheme.Text,
            MenuTheme.Text,
            MenuTheme.Text,
            MenuTheme.Text
        };

        const float CritterInset = 0.15f;

        readonly CosmeticPanel _tile;
        readonly RawImage _critter;
        readonly RectTransform _critterRect;
        readonly Texture2D _texture;
        readonly Color32[] _pixels = new Color32[AvatarPattern.Size * AvatarPattern.Size];
        string _shown;

        public PlayerAvatarView(RectTransform slot, float radius)
        {
            var tileGo = RuntimeUi.Element("Tile", slot, out var tileRect);
            RuntimeUi.Stretch(tileRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _tile = tileGo.AddComponent<CosmeticPanel>();
            _tile.Radius = radius;
            _tile.raycastTarget = false;

            // The critter covers 70% of the tile: 7 cells, so each is a tenth of the tile with a
            // cell and a half of margin all round - enough air that it reads as a sticker.
            var critterGo = RuntimeUi.Element("Critter", slot, out _critterRect);
            RuntimeUi.Stretch(_critterRect, new Vector2(CritterInset, CritterInset),
                new Vector2(1f - CritterInset, 1f - CritterInset), Vector2.zero, Vector2.zero);
            _critter = critterGo.AddComponent<RawImage>();
            _critter.raycastTarget = false;

            _texture = new Texture2D(AvatarPattern.Size, AvatarPattern.Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PlayerAvatar"
            };
            _critter.texture = _texture;
        }

        /// Repaints for a handle. Cheap to call on every refresh: the same name is a no-op.
        public void Show(string handle)
        {
            if (_shown == handle && _shown != null) return;
            _shown = handle ?? string.Empty;

            var pattern = PlayerAvatar.For(handle);
            int palette = pattern.Palette % Tiles.Length;
            _tile.color = Tiles[palette];
            _tile.SetVerticesDirty();

            int size = AvatarPattern.Size;
            Color32 ink = Inks[palette];
            Color32 clear = new Color32(ink.r, ink.g, ink.b, 0);
            int top = size, bottom = -1;
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    bool on = pattern[column, row];
                    if (on)
                    {
                        if (row < top) top = row;
                        bottom = row;
                    }
                    // Texture rows run bottom-up; pattern rows run top-down.
                    _pixels[(size - 1 - row) * size + column] = on ? ink : clear;
                }
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);

            // Not every row is used (the top can be bare, the bottom always is), so the critter is
            // centred on its own bounding box rather than on the grid - by moving the image, in
            // half-cell steps a texture shift could not make. A cell is a tenth of the tile.
            float offset = bottom < 0 ? 0f : ((size - 1) - bottom - top) * 0.5f;
            float y = CritterInset - offset * (1f - 2f * CritterInset) / size;
            _critterRect.anchorMin = new Vector2(CritterInset, y);
            _critterRect.anchorMax = new Vector2(1f - CritterInset, y + 1f - 2f * CritterInset);
        }

        public void Dispose()
        {
            if (_texture == null) return;
            if (Application.isPlaying) Object.Destroy(_texture);
            else Object.DestroyImmediate(_texture); // the editor review renders
        }
    }
}
