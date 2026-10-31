namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Cthangover.Mods.TileRender;

    /// <summary>
    /// One picture of a <see cref="RpgObjectForm.Sprite"/> object: a PNG (or a <see cref="Crop"/> of
    /// it) on a rectangle of cells inside the footprint, standing in the world by its
    /// <see cref="Geometry"/>. Most objects have one part; a gate has two planes, a room a
    /// whole top-down picture plus isometric floor and wall quads selected by <see cref="Only"/>.
    /// Parts merge by <see cref="Id"/> (<c>Parts$add</c> in patches, variants and placements).
    /// </summary>
    public class RpgObjectPart
    {
        /// <summary>Merge key; optional for single-part objects.</summary>
        public string Id { get; set; }

        /// <summary>Path in the <c>rpg_images</c> group without extension.</summary>
        public string Image { get; set; }

        /// <summary><c>[x, y, w, h]</c> in pixels of the picture; null = the whole picture.</summary>
        public int[] Crop { get; set; }

        /// <summary><c>[x, y]</c> cells: top-left of the part relative to the object origin; null = <c>[0, 0]</c>; may be negative.</summary>
        public float[] At { get; set; }

        /// <summary><c>[w, h]</c> cells; null = crop size / map <c>TileSize</c>.</summary>
        public float[] Size { get; set; }

        /// <summary>How the part stands in the world.</summary>
        public RpgPartGeometry Geometry { get; set; } = RpgPartGeometry.Upright;

        /// <summary>Plane direction of a <see cref="RpgPartGeometry.Plane"/> part.</summary>
        public RpgAlong Along { get; set; } = RpgAlong.X;

        /// <summary><see cref="RpgPartGeometry.Quad"/> only: four <c>[x, y, z]</c> world corners (TL, TR, BR, BL of the crop), relative to the origin.</summary>
        public List<float[]> Points { get; set; }

        /// <summary>Renderer id (<c>topdown</c>, <c>isometric</c>, …) this part is limited to; null = every projection.</summary>
        public string Only { get; set; }

        /// <summary>Explicit draw order; null = the object's, else the geometry's default.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TileDrawOrder? DrawOrder { get; set; }

        /// <summary><c>[dx, dy]</c> cells added to the sort point; null = none.</summary>
        public float[] SortOffset { get; set; }
    }
}
