namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;
    using Cthangover.Core.Mods.Patches;

    /// <summary>
    /// One <see cref="RpgObjectForm.Sprite"/> or <see cref="RpgObjectForm.Actor"/> object placed on one
    /// map (contract section 6) — what v1 events and multi-cell tiles in <c>Main</c>/<c>Front</c>
    /// were. The id is global, <c>&lt;map&gt;/&lt;local id&gt;</c>, so another mod patches or deletes a
    /// single placement like any entity, and <c>self.&lt;name&gt;</c> of the instance is the flag
    /// <c>&lt;placement id&gt;:&lt;name&gt;</c>.
    /// <para>
    /// Every other field is a per-instance override of the object, deep-merged over it
    /// (<see cref="RpgObjectResolve.Effective"/>). The overrides are kept as written in
    /// <see cref="Overrides"/>: <c>"Interact": null</c> (a barrel that never opens) must stay
    /// different from "not overridden".
    /// </para>
    /// </summary>
    [EntityKind("rpg_placement")]
    public class RpgPlacement : IIdentifiable
    {
        /// <summary><c>&lt;map id&gt;/&lt;local id&gt;</c>; map ids contain no <c>/</c>.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Id of the placed <see cref="RpgObject"/>.</summary>
        public string Object { get; set; }

        /// <summary>Origin (top-left footprint) column.</summary>
        public int X { get; set; }

        /// <summary>Origin row.</summary>
        public int Y { get; set; }

        /// <summary>Variant of this instance, instead of the map's (a bare birch among autumn trees); null = the map's.</summary>
        public string Variant { get; set; }

        /// <summary>Object fields overridden for this instance, as written (<c>Id</c>, <c>Form</c>, <c>Extends</c> are not allowed).</summary>
        [JsonExtensionData]
        [PatchLayer(typeof(RpgObject))]
        public Dictionary<string, JsonElement> Overrides { get; set; }

        /// <summary>The map this placement belongs to: the id before the first <c>/</c>; null without one.</summary>
        [JsonIgnore]
        public string MapId
        {
            get
            {
                var slash = ID?.IndexOf('/') ?? -1;
                return slash > 0 ? ID.Substring(0, slash) : null;
            }
        }
    }
}
