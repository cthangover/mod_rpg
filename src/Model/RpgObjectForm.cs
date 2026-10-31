namespace Cthangover.Mods.Rpg.Model
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// How an <see cref="RpgObject"/> gets onto a map and how it is drawn (contract
    /// <c>docs/analysis/rpg-model-v2.md</c>, 4.2–4.5). Brush forms (<see cref="Surface"/>,
    /// <see cref="Segment"/>) are painted into the map's rows and are only decoration and
    /// walkability; <see cref="Sprite"/> and <see cref="Actor"/> are placed one by one and may
    /// interact, change state and keep <c>self.</c> flags.
    /// </summary>
    [JsonConverter(typeof(RpgCamelEnumConverter))]
    public enum RpgObjectForm
    {
        /// <summary>A ground material painted into <c>Ground</c>.</summary>
        Surface = 0,

        /// <summary>A one-cell fence/wall piece painted into <c>Structure</c>, joined to neighbours.</summary>
        Segment = 1,

        /// <summary>Pictures (<c>Parts</c>) on a footprint, placed by <c>rpg_placement</c>.</summary>
        Sprite = 2,

        /// <summary>A walking-sheet character on one cell, placed by <c>rpg_placement</c>.</summary>
        Actor = 3,
    }
}
