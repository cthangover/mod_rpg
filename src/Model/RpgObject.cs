namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Mods.TileRender;

    /// <summary>
    /// Anything that can appear on an RPG map in model v2 (contract
    /// <c>docs/analysis/rpg-model-v2.md</c>, section 4): ground materials, fences, trees, houses,
    /// rooms, doors, barrels, NPCs. There are no tilesets or atlases — every picture is its own
    /// PNG in <c>rpg_images</c> and every object is an ordinary entity, overridden by id,
    /// patched, inherited with <c>Extends</c> (resolved by the core <c>EntityResolver</c>).
    /// <para>
    /// The <see cref="Form"/> says which blocks matter: <see cref="Surface"/>, <see cref="Segment"/>,
    /// <see cref="Parts"/> (with <see cref="Footprint"/>/<see cref="Anchor"/>) or <see cref="Charset"/>.
    /// What a placement finally shows is computed by <see cref="RpgObjectResolve"/>: the map's
    /// variant, then the placement's overrides (both deep-merged), then at run time the active
    /// state (wholesale component replacement).
    /// </para>
    /// </summary>
    [EntityKind("rpg_object")]
    public class RpgObject : IIdentifiable
    {
        /// <summary><c>&lt;mod or pack&gt;/&lt;category&gt;/&lt;name&gt;</c>.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Locale key for editors and interaction prompts.</summary>
        public string Name { get; set; }

        /// <summary>Free palette/folder tag (<c>ground</c>, <c>vegetation</c>, <c>door</c>, …).</summary>
        public string Category { get; set; } = "misc";

        /// <summary>How the object is placed and drawn; null = not given (a validation error).</summary>
        public RpgObjectForm? Form { get; set; }

        /// <summary>Parent object id; already applied when the object comes from the entity resolver.</summary>
        public string Extends { get; set; }

        /// <summary>
        /// Partial objects by variant name (<c>winter</c>, <c>autumn</c>), deep-merged when the map's or
        /// the placement's <c>Variant</c> names them. Kept as written, so a variant can remove a
        /// component with <c>null</c>; keyed list operations inside (<c>Parts$add</c>) merge by id
        /// when the variant is applied.
        /// </summary>
        [PatchLayer(typeof(RpgObject))]
        public Dictionary<string, JsonElement> Variants { get; set; }

        /// <summary>Blocks walking on the footprint (unless <see cref="RpgFootprint.Solid"/> rows say per cell); null = by form, see <see cref="IsSolid"/>.</summary>
        public bool? Solid { get; set; }

        /// <summary>Surface only: the cell is water.</summary>
        public bool Water { get; set; }

        /// <summary>Not drawn, not solid, not interactive (set by states: a picked-up item, a won enemy).</summary>
        public bool Hidden { get; set; }

        /// <summary>Material of a surface.</summary>
        public RpgSurface Surface { get; set; }

        /// <summary>Picture of a segment.</summary>
        public RpgSegment Segment { get; set; }

        /// <summary>Occupied cells and their walk mask (sprites; actors are always 1×1).</summary>
        public RpgFootprint Footprint { get; set; }

        /// <summary><c>[x, y]</c> cells from the origin: sort point and billboard foot; null = bottom centre of the footprint.</summary>
        public float[] Anchor { get; set; }

        /// <summary>Default draw order of all parts; null = per geometry.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TileDrawOrder? DrawOrder { get; set; }

        /// <summary>Pictures of a sprite; empty = an invisible object (a trigger zone).</summary>
        [MergeKey("Id")]
        public List<RpgObjectPart> Parts { get; set; }

        /// <summary>Walking sheet of an actor (path in the <c>charsets</c> group).</summary>
        public string Charset { get; set; }

        /// <summary>Initial facing of an actor.</summary>
        public RpgDirection Facing { get; set; } = RpgDirection.Down;

        /// <summary>Idle frame column of an actor.</summary>
        public int Column { get; set; } = 1;

        /// <summary>What the player triggers.</summary>
        public RpgInteract Interact { get; set; }

        /// <summary>The main light.</summary>
        public RpgObjectLight Light { get; set; }

        /// <summary>Further lights, each with its own position.</summary>
        public List<RpgObjectLight> Lights { get; set; }

        /// <summary>Autonomous movement.</summary>
        public RpgMoveRoute Route { get; set; }

        /// <summary>Ordered states; the last one whose condition holds wins.</summary>
        [MergeKey("Id")]
        public List<RpgObjectState> States { get; set; }

        /// <summary>Whether the object blocks walking where its footprint has no own mask: <see cref="Solid"/>, by default false for surfaces and true for every other form.</summary>
        [JsonIgnore]
        public bool IsSolid => Solid ?? Form != RpgObjectForm.Surface;

        /// <summary>
        /// Whether a placement of this (effective) object needs run-time state (contract 6): it has
        /// <see cref="Interact"/>, <see cref="States"/> or a <see cref="Route"/>. Otherwise it is static —
        /// baked into the render scene and the walk grid once.
        /// </summary>
        [JsonIgnore]
        public bool IsDynamic => Interact != null || States is { Count: > 0 } || Route != null;

        /// <summary>Footprint width in cells; an actor always stands on one cell.</summary>
        [JsonIgnore]
        public int Width => Form == RpgObjectForm.Actor || Footprint == null ? 1 : Footprint.Width;

        /// <summary>Footprint height in cells; an actor always stands on one cell.</summary>
        [JsonIgnore]
        public int Height => Form == RpgObjectForm.Actor || Footprint == null ? 1 : Footprint.Height;

        /// <summary>
        /// Whether footprint cell (<paramref name="dx"/>, <paramref name="dy"/>) from the origin blocks
        /// walking: never when <see cref="Hidden"/> or outside the footprint; else the cell's
        /// <c>#</c>/<c>.</c> in <see cref="RpgFootprint.Solid"/> when the rows are given (a missing
        /// character is walkable), else <see cref="IsSolid"/>.
        /// </summary>
        public bool BlocksAt(int dx, int dy)
        {
            if (Hidden || dx < 0 || dy < 0 || dx >= Width || dy >= Height)
                return false;
            var rows = Form == RpgObjectForm.Actor ? null : Footprint?.Solid;
            if (rows == null)
                return IsSolid;
            var row = dy < rows.Count ? rows[dy] : null;
            return row != null && dx < row.Length && row[dx] == '#';
        }
    }
}
