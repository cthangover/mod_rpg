namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods.Testing;

    /// <summary>Directions: the arithmetic every step and route uses (<c>rules/*</c>); random battles live in the battles integration.</summary>
    public class RpgRulesTests
    {
        [ModTest("rules/directions")]
        static void Directions(ModTestContext ctx)
        {
            ModAssert.Equal(-1, RpgDirections.Delta(RpgDirection.Up).Y, "up is y-1");
            ModAssert.Equal(1, RpgDirections.Delta(RpgDirection.Right).X, "right is x+1");
            ModAssert.Equal("Up", RpgDirections.Opposite(RpgDirection.Down).ToString(), "opposite of down");
            ModAssert.Equal("Left", RpgDirections.Opposite(RpgDirection.Right).ToString(), "opposite of right");
        }
    }
}
