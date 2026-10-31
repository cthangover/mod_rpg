namespace Cthangover.Mods.Rpg.Model
{
    using System.Text.Json.Serialization;

    /// <summary>When the player sets off an object's <see cref="RpgInteract"/> (v1 names: <c>Action</c> → Use, <c>Touch</c> → Step).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RpgInteractTrigger
    {
        /// <summary>The action key while facing any footprint cell, or standing on a non-solid one.</summary>
        Use = 0,

        /// <summary>Entering any footprint cell (doors of any width, map exits, pickups).</summary>
        Step = 1,
    }
}
