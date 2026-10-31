namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The cells a placed object occupies, counted from its origin (top-left cell): its trigger
    /// area, walk mask and editor selection. <see cref="Solid"/> rows (<c>#</c> blocks, <c>.</c>
    /// walkable) refine the object's plain <c>Solid</c> flag per cell — a tree blocks only its trunk.
    /// </summary>
    public class RpgFootprint
    {
        /// <summary><c>[w, h]</c> in cells; missing or malformed = 1×1.</summary>
        public int[] Size { get; set; } = { 1, 1 };

        /// <summary><c>h</c> rows of <c>w</c> characters <c>#</c>/<c>.</c>; null = the object's <c>Solid</c> on every cell.</summary>
        public List<string> Solid { get; set; }

        /// <summary>Width in cells, at least 1.</summary>
        [JsonIgnore]
        public int Width => Size != null && Size.Length > 0 && Size[0] > 0 ? Size[0] : 1;

        /// <summary>Height in cells, at least 1.</summary>
        [JsonIgnore]
        public int Height => Size != null && Size.Length > 1 && Size[1] > 0 ? Size[1] : 1;
    }
}
