namespace Cthangover.Mods.Rpg
{
    using System.Collections.Generic;
    using Godot;

    /// <summary>
    /// World quads of a <see cref="RpgTileInfo.Plane"/> tile: a one-cell-high vertical plane through the cell centre,
    /// so the isometric view shears the same atlas sprite onto a map axis like the ground, keeping its pixels.
    /// "auto" follows neighbouring plane tiles of the same layer like an autotile: left/right neighbours give the
    /// x plane, up/down neighbours the y plane, both give a corner of two planes, none a lone x plane.
    /// Corners are TL/TR/BR/BL with the image's left edge on the screen's left in both orientations.
    /// </summary>
    public static class RpgTilePlanes
    {
        public static List<Vector3[]> Quads(RpgMapGrid grid, RpgTileset tileset, RpgLayer layer, int x, int y, string plane)
        {
            var alongX = plane == "x";
            var alongY = plane == "y";
            if (plane == "auto")
            {
                alongX = IsPlane(grid, tileset, layer, x - 1, y) || IsPlane(grid, tileset, layer, x + 1, y);
                alongY = IsPlane(grid, tileset, layer, x, y - 1) || IsPlane(grid, tileset, layer, x, y + 1);
                alongX |= !alongY;
            }
            var result = new List<Vector3[]>();
            if (alongX)
                result.Add(new[] { new Vector3(x, y + 0.5f, 1), new Vector3(x + 1, y + 0.5f, 1),
                    new Vector3(x + 1, y + 0.5f, 0), new Vector3(x, y + 0.5f, 0) });
            if (alongY)
                result.Add(new[] { new Vector3(x + 0.5f, y + 1, 1), new Vector3(x + 0.5f, y, 1),
                    new Vector3(x + 0.5f, y, 0), new Vector3(x + 0.5f, y + 1, 0) });
            return result;
        }

        private static bool IsPlane(RpgMapGrid grid, RpgTileset tileset, RpgLayer layer, int x, int y)
            => grid.GetTile(layer, x, y) is var id and >= 0 && tileset.TileOf(id)?.Plane != null;
    }
}
