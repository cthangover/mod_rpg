namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Cell codes of a map's <c>Collision</c> rows (contract 8). They override what the
    /// ground, structure and static objects say: most cells stay <see cref="Auto"/>, and the
    /// author paints exceptions (an invisible wall at the cliff edge, a walkable gap in a hedge).
    /// </summary>
    public static class RpgCollision
    {
        /// <summary>Passability comes from the objects on the cell.</summary>
        public const char Auto = '.';

        /// <summary>Always blocked.</summary>
        public const char Block = '#';

        /// <summary>Walkable whatever the terrain and static objects say (a dynamic placement still blocks).</summary>
        public const char Open = 'o';
    }
}
