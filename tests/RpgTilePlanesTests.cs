namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Plane tiles stand on a map axis through the cell centre; "auto" follows plane neighbours.</summary>
    public class RpgTilePlanesTests
    {
        private const int Fence = 12;

        private static RpgTileset Tileset()
        {
            var tileset = new RpgTileset { ID = "planes" };
            tileset.Tiles.Add(new RpgTileInfo { Index = Fence, Solid = true, Plane = "auto" });
            return tileset;
        }

        [ModTest("tile_planes/json_roundtrip")]
        static void JsonRoundtrip(ModTestContext ctx)
        {
            var restored = JsonSerializer.Deserialize<RpgTileset>(JsonSerializer.Serialize(Tileset()), JsonOptionsProvider.ReadOptions);
            ModAssert.Equal("auto", restored.TileOf(Fence).Plane, "plane mode");
            ModAssert.Equal(null, JsonSerializer.Deserialize<RpgTileInfo>("{}").Plane, "legacy tiles stay sprites");
        }

        [ModTest("tile_planes/auto_by_neighbours")]
        static void AutoByNeighbours(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(5, 5);
            grid.SetTile(RpgLayer.Main, 1, 1, Fence);
            grid.SetTile(RpgLayer.Main, 2, 1, Fence);
            grid.SetTile(RpgLayer.Main, 2, 2, Fence);
            grid.SetTile(RpgLayer.Main, 4, 4, Fence);
            grid.SetTile(RpgLayer.Front, 2, 3, Fence);
            var tileset = Tileset();

            var row = RpgTilePlanes.Quads(grid, tileset, RpgLayer.Main, 1, 1, "auto");
            ModAssert.Equal(1, row.Count, "a row end is one plane");
            ModAssert.True(row[0][0] == new Vector3(1, 1.5f, 1) && row[0][2] == new Vector3(2, 1.5f, 0), "along x through the centre, one cell high");

            ModAssert.Equal(2, RpgTilePlanes.Quads(grid, tileset, RpgLayer.Main, 2, 1, "auto").Count, "a corner has both planes");

            var column = RpgTilePlanes.Quads(grid, tileset, RpgLayer.Main, 2, 2, "auto");
            ModAssert.Equal(1, column.Count, "a column end ignores the other layer");
            ModAssert.True(column[0][0] == new Vector3(2.5f, 3, 1) && column[0][1] == new Vector3(2.5f, 2, 1),
                "along y, the image's left edge at the larger y (screen left)");

            List<Vector3[]> lone = RpgTilePlanes.Quads(grid, tileset, RpgLayer.Main, 4, 4, "auto");
            ModAssert.True(lone.Count == 1 && lone[0][0].Y == 4.5f, "a lone tile defaults to x");
            ModAssert.True(RpgTilePlanes.Quads(grid, tileset, RpgLayer.Main, 4, 4, "y")[0][0].X == 4.5f, "an explicit axis wins");
        }
    }
}
