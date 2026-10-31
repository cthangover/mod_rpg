namespace Cthangover.Mods.Rpg.Model
{
    using System.Text.Json.Serialization;

    /// <summary>Which grid of an <see cref="RpgMap"/> an <see cref="RpgStroke"/> edits.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RpgStrokeLayer
    {
        /// <summary>Surfaces (<c>Ground</c> rows).</summary>
        Ground = 0,

        /// <summary>Segments (<c>Structure</c> rows).</summary>
        Structure = 1,

        /// <summary>Collision codes (<c>Collision</c> rows).</summary>
        Collision = 2,
    }
}
