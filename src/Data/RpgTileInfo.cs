namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Properties of one atlas cell of an <see cref="RpgTileset"/>. Only cells that
    /// differ from the default (walkable, unnamed) need an entry.
    /// </summary>
    public class RpgTileInfo
    {
        /// <summary>Logical map id; AtlasIndex or Surface can give it independent physical storage.</summary>
        public int Index { get; set; }

        /// <summary>Physical cell in the object atlas; null preserves legacy row-major addressing.</summary>
        public int? AtlasIndex { get; set; }

        /// <summary>Square material patch in the separate surface atlas; null denotes an object.</summary>
        public int? Surface { get; set; }

        /// <summary>Blend this material with neighbouring surfaces; interiors can keep hard edges.</summary>
        public bool Diffuse { get; set; } = true;

        /// <summary>
        /// The tile blocks walking when it lies on the back or the main layer
        /// (front-layer tiles never block: the player walks under them). The map's
        /// collision layer can override it per cell.
        /// </summary>
        public bool Solid { get; set; }

        /// <summary>
        /// Optional author-facing tag shown by the editor palette ("grass",
        /// "tree_crown"). Not a locale key: players never see it.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>Optional authoring layer used when an object is stamped; null follows the map editor's active layer.</summary>
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public RpgLayer? Layer { get; set; }

        /// <summary>Light the tile gives off wherever it lies on a map (a lamp, a lit window); null = none.</summary>
        public RpgLight Light { get; set; }

        /// <summary>
        /// Picture of an autotile block (RPG Maker A2 layout, see <see cref="RpgAutotiles"/>) in the
        /// tilesets group; set, the entry is an autotile and <see cref="Index"/> should lie outside the atlas.
        /// </summary>
        public string Autotile { get; set; }

        /// <summary>
        /// Thin upright object (fence, wall, railing) shown in the isometric view as a vertical plane along a map
        /// axis instead of a camera-facing sprite: "x", "y", or "auto" (along neighbouring plane tiles of the same
        /// layer, see <see cref="RpgTilePlanes"/>). Null keeps the sprite; the square view always shows the sprite.
        /// </summary>
        public string Plane { get; set; }

        /// <summary>Atlas of a tile borrowed from another tileset (<see cref="RpgTilesetComposer"/>); null = the tileset's own image.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string SourceImage { get; set; }

        /// <summary>Columns of <see cref="SourceImage"/>; 0 = the tileset's own columns.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int SourceColumns { get; set; }
    }
}
