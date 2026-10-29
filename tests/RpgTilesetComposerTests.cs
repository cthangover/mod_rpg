namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Godot;

    /// <summary>A map borrows objects of other tilesets in id slots; its own ids and surfaces stay as they are.</summary>
    public class RpgTilesetComposerTests
    {
        [ModTest("composer/borrowed_objects")]
        static void BorrowedObjects(ModTestContext ctx)
        {
            var desert = new RpgTileset { ID = "desert", Image = "desert/objects", Columns = 16 };
            desert.Tiles.Add(new RpgTileInfo { Index = 0, Surface = 0 });
            desert.Tiles.Add(new RpgTileInfo { Index = 100, AtlasIndex = 0, Solid = true });
            var village = new RpgTileset { ID = "village", Image = "village/objects", Columns = 12 };
            village.Tiles.Add(new RpgTileInfo { Index = 0, Surface = 0 });
            village.Tiles.Add(new RpgTileInfo { Index = 12, AtlasIndex = 177, Solid = true, Plane = "auto" });
            village.VisualGroups.Add(new RpgVisualGroup { Id = "lamp", Width = 1, Height = 1, Pattern = new[] { 12 }, AnchorX = 0.5f, AnchorY = 1 });

            ModAssert.True(ReferenceEquals(desert, RpgTilesetComposer.Compose(desert, new List<RpgTileset>())), "no extras: the tileset itself");
            var map = RpgTilesetComposer.Compose(desert, new List<RpgTileset> { village });
            var fence = map.TileOf(100012);
            ModAssert.True(fence != null && fence.Solid && fence.Plane == "auto", "borrowed tile keeps its properties");
            ModAssert.Equal("village/objects", fence.SourceImage, "borrowed tile is cut from its own atlas");
            ModAssert.True(RpgTileSetBuilder.AtlasCoords(map, 100012) == new Vector2I(177 % 12, 177 / 12), "with its own atlas columns");
            ModAssert.True(RpgTileSetBuilder.AtlasCoords(map, 100) == new Vector2I(0, 0), "own tiles unchanged");
            ModAssert.Equal(null, map.TileOf(100000)?.Tag, "surfaces are not borrowed");
            ModAssert.Equal(100012, map.VisualGroups[0].Pattern[0], "groups move to the slot");
            ModAssert.Equal(1, RpgTilesetComposer.Slot(100012), "slot of a borrowed id");
            ModAssert.Equal(2, desert.Tiles.Count, "inputs untouched");
        }
    }
}
