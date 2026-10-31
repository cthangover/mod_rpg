namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// A cell edit of an <see cref="RpgMap"/> applied after its rows: one object (or a collision
    /// code) painted over rectangles and single cells of one grid. Strokes exist so that another
    /// mod can change a few cells of a map it does not own with a patch (<c>Strokes$add</c>, merged
    /// by <see cref="Id"/>) instead of shipping the whole map; the map editor writes them for the
    /// same reason. Strokes apply in list order; cells outside the map are ignored.
    /// </summary>
    public class RpgStroke
    {
        /// <summary>Merge key; a later patch with the same id changes this stroke.</summary>
        public string Id { get; set; }

        /// <summary>The grid edited.</summary>
        public RpgStrokeLayer Layer { get; set; } = RpgStrokeLayer.Ground;

        /// <summary>Ground/Structure: the object painted (a surface / a segment); null clears the cells.</summary>
        public string Object { get; set; }

        /// <summary>Collision: <c>#</c> block, <c>o</c> open, <c>.</c> derived.</summary>
        public string Code { get; set; }

        /// <summary><c>[x, y, w, h]</c> cell rectangles.</summary>
        public List<int[]> Rects { get; set; }

        /// <summary><c>[x, y]</c> single cells.</summary>
        public List<int[]> Cells { get; set; }
    }
}
