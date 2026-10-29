namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Mods.Testing;

    /// <summary>A desert map draws a village object whole, cut from the village atlas.</summary>
    public class RpgBorrowedRenderTests
    {
        [ModTest("composer/desert_map_draws_village_lamp")]
        static void DesertMapDrawsVillageLamp(ModTestContext ctx)
        {
            var map = new RpgMap { ID = "borrow_test", Tileset = "desert", Tilesets = new List<string> { "village" }, Width = 6, Height = 6 };
            var tileset = RpgTilesetFactory.Instance.ForMap(map);
            ModAssert.True(tileset != null && tileset.TileOf(100029) != null, "village lamp borrowed into slot 1");
            var grid = new RpgMapGrid(6, 6);
            grid.SetTile(RpgLayer.Front, 2, 2, 100029);
            grid.SetTile(RpgLayer.Main, 2, 3, 100028);
            using var textures = new RpgTextures();
            using var builder = new RpgRenderSceneBuilder(textures);
            var scene = builder.Build(map, grid, tileset);
            var lamp = scene.Visuals.Where(v => v.Id == "village/lamp").ToList();
            ModAssert.True(lamp.Count > 0, "the lamp is matched as a whole object");
            ModAssert.True(lamp.All(v => ReferenceEquals(v.Texture, textures.Tileset("village/objects"))), "drawn from the village atlas");
            ModAssert.False(RpgPassability.IsWalkable(grid, tileset, 2, 3), "its solid post blocks on the desert map");
            scene.Release();
        }
    }
}
