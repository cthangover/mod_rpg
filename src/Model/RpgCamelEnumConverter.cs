namespace Cthangover.Mods.Rpg.Model
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Writes the v2 model's own enums as the lower-case words of the contract
    /// (<c>"surface"</c>, <c>"upright"</c>, <c>"auto"</c>, <c>"x"</c>); reading accepts any case, so a
    /// hand-written <c>"Surface"</c> works too. Directions, triggers and draw orders keep the
    /// capitalised words (<c>Down</c>, <c>Use</c>, <c>BelowCharacters</c>) and the plain converter.
    /// </summary>
    public sealed class RpgCamelEnumConverter : JsonStringEnumConverter
    {
        /// <summary>Camel-case names, integers rejected (an index would silently pick a member).</summary>
        public RpgCamelEnumConverter() : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        {
        }
    }
}
