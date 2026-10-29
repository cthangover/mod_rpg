using System;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Autotiles in the RPG Maker A2 layout: one picture of 2×3 tiles describes a terrain
    /// (water, a path, a cliff top), and each map cell is assembled from four quarter tiles
    /// chosen by its eight neighbours — so an author paints "water" and the shores draw
    /// themselves. A tileset entry becomes an autotile by naming that picture
    /// (<see cref="RpgTileInfo.Autotile"/>); its index is any number outside the atlas.
    /// <para/>
    /// The block in quarter units (4 columns × 6 rows): the top-left tile is the palette
    /// preview; the top-right tile holds the four inner corners; the 2×2 tiles below are a
    /// framed patch whose outer ring gives outer corners and edges and whose middle gives the
    /// fill. For a quarter, the two neighbours along its sides and the one across its corner
    /// decide: both sides alike and the corner too — fill; both sides alike but not the
    /// corner — inner corner; only one side alike — the edge along the other; none — the
    /// outer corner. Cells past the map edge count as alike, so a terrain runs off the map
    /// without a border. The world adapter and map editor draw
    /// through <see cref="Quarters"/> and <see cref="Draw"/>, so both show the same cells.
    /// </summary>
    public static class RpgAutotiles
    {
        /// <summary>
        /// Source quarters (in quarter units of the block) for the top-left, top-right,
        /// bottom-left and bottom-right quarter of a cell, from which neighbours are alike.
        /// </summary>
        public static Vector2I[] Quarters(bool up, bool down, bool left, bool right,
            bool upLeft, bool upRight, bool downLeft, bool downRight)
            => Cthangover.Mods.TileRender.TileAutotile.Quarters(up, down, left, right, upLeft, upRight, downLeft, downRight);

        /// <summary>
        /// The quarters of the cell (<paramref name="x"/>, <paramref name="y"/>) of
        /// <paramref name="layer"/>, whose tile is the autotile <paramref name="index"/>.
        /// </summary>
        public static Vector2I[] QuartersAt(RpgMapGrid grid, RpgLayer layer, int x, int y, int index)
        {
            return Quarters(
                Alike(grid, layer, x, y - 1, index), Alike(grid, layer, x, y + 1, index),
                Alike(grid, layer, x - 1, y, index), Alike(grid, layer, x + 1, y, index),
                Alike(grid, layer, x - 1, y - 1, index), Alike(grid, layer, x + 1, y - 1, index),
                Alike(grid, layer, x - 1, y + 1, index), Alike(grid, layer, x + 1, y + 1, index));
        }

        /// <summary>Pixel rectangle of a quarter in the block picture of a tileset with <paramref name="tileSize"/>.</summary>
        public static Rect2 SourceRect(Vector2I quarter, int tileSize)
        {
            var half = tileSize / 2f;
            return new Rect2(quarter.X * half, quarter.Y * half, half, half);
        }

        /// <summary>Draws a cell of an autotile into <paramref name="cell"/> on <paramref name="target"/>.</summary>
        public static void Draw(CanvasItem target, Texture2D block, Rect2 cell, Vector2I[] quarters, int tileSize, Color modulate)
        {
            var half = cell.Size / 2f;
            for (var i = 0; i < 4; i++)
            {
                var offset = new Vector2(i % 2 * half.X, i / 2 * half.Y);
                target.DrawTextureRectRegion(block, new Rect2(cell.Position + offset, half), SourceRect(quarters[i], tileSize), modulate);
            }
        }

        /// <summary>Source rectangle of the block's preview tile (its top-left tile), for palettes.</summary>
        public static Rect2 PreviewRect(int tileSize) => new(0, 0, tileSize, tileSize);

        /// <summary>
        /// Selects one A2 block from a sheet of adjacent 2×3 blocks by map position.
        /// A single-block image keeps its original behaviour. Larger sheets let a
        /// material span several cells without introducing seams or extra terrain ids.
        /// </summary>
        public static Rect2 VariantRect(Vector2I imageSize, int tileSize, Vector2I cell)
            => Cthangover.Mods.TileRender.TileAutotile.VariantRect(imageSize, tileSize, cell);

        private static bool Alike(RpgMapGrid grid, RpgLayer layer, int x, int y, int index)
            => !grid.InBounds(x, y) || grid.GetTile(layer, x, y) == index;
    }
}
