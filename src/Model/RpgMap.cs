namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Mods.Effects.Atmosphere;

    /// <summary>
    /// One RPG map in model v2 (contract section 8): its size and three grids of one character
    /// per cell — <see cref="Ground"/> (surface objects), <see cref="Structure"/> (segment objects)
    /// and <see cref="Collision"/> codes — with a <see cref="Legend"/> from character to object id,
    /// then <see cref="Strokes"/> on top. Scenery uses <see cref="RpgPlacement"/>;
    /// invisible scenario rectangles live directly in <see cref="Events"/>. The id is also
    /// the logical scene that shows the map, as in v1.
    /// <para>
    /// The grids are read by <see cref="RpgMapCells.Build"/>; walkability is <see cref="RpgWalkGrid"/>.
    /// </para>
    /// </summary>
    [EntityKind("rpg_map")]
    public class RpgMap : IIdentifiable
    {
        /// <summary>Map id = logical scene name; contains no <c>/</c> (placement ids start with it).</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Locale key of the display name.</summary>
        public string Name { get; set; }

        /// <summary>Columns.</summary>
        public int Width { get; set; }

        /// <summary>Rows.</summary>
        public int Height { get; set; }

        /// <summary>Pixels per cell of every picture used on this map.</summary>
        public int TileSize { get; set; } = 32;

        /// <summary>Side in pixels of every surface material patch on this map.</summary>
        public int PatchSize { get; set; } = 128;

        /// <summary>View zoom.</summary>
        public float Zoom { get; set; } = 2f;

        /// <summary>One character → object id; <c>.</c> is reserved for an empty cell.</summary>
        public Dictionary<string, string> Legend { get; set; } = new();

        /// <summary><see cref="Height"/> rows of <see cref="Width"/> legend characters; surfaces only.</summary>
        public List<string> Ground { get; set; } = new();

        /// <summary>Rows of legend characters; segments only.</summary>
        public List<string> Structure { get; set; } = new();

        /// <summary>Rows of <c>.</c> (derived), <c>#</c> (block), <c>o</c> (force open).</summary>
        public List<string> Collision { get; set; } = new();

        /// <summary>Cell edits applied after the rows, in order.</summary>
        [MergeKey("Id")]
        public List<RpgStroke> Strokes { get; set; } = new();

        /// <summary>Invisible scenario rectangles stored with the map, independent of object definitions and placements.</summary>
        [MergeKey("Id")]
        public List<RpgMapEvent> Events { get; set; } = new();

        /// <summary>Default entry cell and facing.</summary>
        public RpgTeleport Spawn { get; set; } = new();

        /// <summary>Variant name applied to every object on the map (a placement may name its own).</summary>
        public string Variant { get; set; }

        /// <summary>Atmosphere profile id; empty keeps interiors still.</summary>
        public string Atmosphere { get; set; }

        /// <summary>Fixed light colour (<c>#rrggbb</c>); null = the time of day.</summary>
        public string Ambient { get; set; }

        /// <summary>Additional whole-map atmosphere profiles and painted masks.</summary>
        public List<AtmospherePlacement> Effects { get; set; } = new();

        /// <summary><c>cycle</c> follows the clock; <c>day</c>/<c>night</c> fix this map.</summary>
        public string LightMode { get; set; } = "cycle";

        /// <summary>
        /// Fields this mod does not read, kept as written: the battles integration (<c>rpg__battles</c>) reads
        /// <c>Encounters</c> (random battles, <c>{Steps, Groups}</c>) from here, so the world has no battle type.
        /// </summary>
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement> ExtraFields { get; set; }
    }
}
