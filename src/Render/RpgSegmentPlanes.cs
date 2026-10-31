namespace Cthangover.Mods.Rpg.Render
{
    using System.Collections.Generic;
    using Cthangover.Mods.Rpg.Model;
    using Godot;

    /// <summary>
    /// World quads of one <c>segment</c> cell (contract 4.3): vertical planes through the cell centre
    /// line, z from 0 to the segment's <c>Height</c>. <c>Along: auto</c> follows the neighbours like an
    /// autotile — a left/right neighbour gives the x plane, an up/down one the y plane, both a cross,
    /// none a lone x plane — but only segments with the same <c>Connect</c> key count, so a fence
    /// never bends towards a wall that merely touches it. Corners are TL, TR, BR, BL with the
    /// picture's left edge on the screen's left in both orientations (as the v1 plane tiles).
    /// </summary>
    public static class RpgSegmentPlanes
    {
        /// <summary>The quads of cell (<paramref name="x"/>, <paramref name="y"/>); empty when the cell has no segment.</summary>
        public static List<Vector3[]> Quads(RpgMapCells cells, int x, int y)
        {
            var result = new List<Vector3[]>();
            var segment = cells.Structure(x, y);
            if (segment?.Segment == null)
                return result;
            var key = segment.Segment.ConnectKey(segment.ID);
            var alongX = segment.Segment.Along == RpgAlong.X;
            var alongY = segment.Segment.Along == RpgAlong.Y;
            if (segment.Segment.Along == RpgAlong.Auto)
            {
                alongX = Joins(cells, key, x - 1, y) || Joins(cells, key, x + 1, y);
                alongY = Joins(cells, key, x, y - 1) || Joins(cells, key, x, y + 1);
                alongX |= !alongY;
            }
            var h = segment.Segment.Height;
            if (alongX)
                result.Add(new[] { new Vector3(x, y + 0.5f, h), new Vector3(x + 1, y + 0.5f, h),
                    new Vector3(x + 1, y + 0.5f, 0), new Vector3(x, y + 0.5f, 0) });
            if (alongY)
                result.Add(new[] { new Vector3(x + 0.5f, y + 1, h), new Vector3(x + 0.5f, y, h),
                    new Vector3(x + 0.5f, y, 0), new Vector3(x + 0.5f, y + 1, 0) });
            return result;
        }

        private static bool Joins(RpgMapCells cells, string key, int x, int y)
        {
            var other = cells.Structure(x, y);
            return other?.Segment != null && string.Equals(other.Segment.ConnectKey(other.ID), key, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
