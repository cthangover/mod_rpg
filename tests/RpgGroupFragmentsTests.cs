namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Godot;

    /// <summary>A cell of a broken object knows its place inside that object.</summary>
    public class RpgGroupFragmentsTests
    {
        [ModTest("group_fragments/place_in_object")]
        static void PlaceInObject(ModTestContext ctx)
        {
            var tileset = new RpgTileset { ID = "fragments" };
            tileset.VisualGroups.Add(new RpgVisualGroup { Id = "house", Width = 3, Height = 2, Pattern = new[] { 1, 2, 3, 4, 5, 6 }, AnchorX = 1.5f, AnchorY = 2 });
            tileset.VisualGroups.Add(new RpgVisualGroup { Id = "shed", Width = 1, Height = 2, Pattern = new[] { 6, 7 }, AnchorX = 0.5f, AnchorY = 2 });
            tileset.VisualGroups.Add(new RpgVisualGroup { Id = "bad", Width = 2, Height = 1, Pattern = new[] { 8 } });
            var fragments = RpgGroupFragments.Of(tileset);
            ModAssert.Equal("house", fragments[5].Group.Id, "owner of a cell");
            ModAssert.True(fragments[5].Cell == new Vector2I(1, 1), "column and row inside the object");
            ModAssert.Equal("house", fragments[6].Group.Id, "a shared tile keeps its first object");
            ModAssert.True(fragments[7].Cell == new Vector2I(0, 1), "second object's cells");
            ModAssert.False(fragments.ContainsKey(8), "invalid groups are ignored");
        }
    }
}
