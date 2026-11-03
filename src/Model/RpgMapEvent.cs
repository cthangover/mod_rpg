namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// A map-owned rectangle of behaviour with no picture or object definition. Its local id
    /// also preserves the self-flag scope of an old placement migrated to this event.
    /// </summary>
    public class RpgMapEvent : RpgInteract
    {
        /// <summary>Stable local identity, unique among events and placements of this map.</summary>
        public string Id { get; set; }
        /// <summary>Optional locale key for the interaction cue.</summary>
        public string Name { get; set; }
        /// <summary>Leftmost tile.</summary>
        public int X { get; set; }
        /// <summary>Topmost tile.</summary>
        public int Y { get; set; }
        /// <summary>Rectangle width; one tile by default.</summary>
        public int Width { get; set; } = 1;
        /// <summary>Rectangle height; one tile by default.</summary>
        public int Height { get; set; } = 1;
        /// <summary>Optional scenario condition, evaluated in this event's self scope.</summary>
        public string When { get; set; }
        /// <summary>Optional invisible barrier, for action-key doors; false for ordinary trigger zones.</summary>
        public bool Solid { get; set; }
        /// <summary>Identity shared by editor selection, interaction and saved self flags.</summary>
        public string Scope(string map) => map + "/" + Id;
        /// <summary>Transient location for the interaction runner; never saved as a placement.</summary>
        public RpgPlacement AsPlacement(string map) => new() { ID = Scope(map), X = X, Y = Y };
        /// <summary>Transient runtime adapter. Creates no registered object and has no render parts.</summary>
        public RpgObject AsObject() => new()
        {
            Form = RpgObjectForm.Sprite,
            Name = Name,
            Solid = Solid,
            Footprint = new RpgFootprint { Size = new[] { Width, Height } },
            Interact = this,
            States = RuntimeStates()
        };

        private List<RpgObjectState> RuntimeStates()
        {
            var result = new List<RpgObjectState>();
            if (!string.IsNullOrWhiteSpace(When))
                result.Add(new RpgObjectState { When = "!(" + When + ")",
                    Components = new Dictionary<string, JsonElement> { ["Hidden"] = JsonSerializer.SerializeToElement(true) } });
            return result.Count == 0 ? null : result;
        }
    }
}
