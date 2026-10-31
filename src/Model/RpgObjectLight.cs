namespace Cthangover.Mods.Rpg.Model
{
    /// <summary>
    /// A light a placed object gives off (lamp, fire, lit window). Unlike the v1 event light it
    /// has a position: <see cref="At"/> in cells from the object origin, so a cottage can light
    /// four windows (<c>Lights</c>) without four invisible events.
    /// </summary>
    public class RpgObjectLight
    {
        /// <summary>Reach in cells; the light fades to nothing there.</summary>
        public float Radius { get; set; } = 3f;

        /// <summary><c>#rrggbb</c> or <c>#rrggbbaa</c> (alpha = strength); parsed once at map load.</summary>
        public string Color { get; set; } = "#ffd8a0";

        /// <summary><c>[x, y]</c> cells from the origin; null = the footprint centre.</summary>
        public float[] At { get; set; }
    }
}
