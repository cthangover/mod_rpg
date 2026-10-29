using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods.Testing;

namespace Cthangover.Mods.Rpg.Tests
{
    /// <summary>Material transitions are calculated from neighbours, with stable world sampling and unchanged collision.</summary>
    public class RpgSurfaceRendererTests
    {
        private static RpgTileset Tileset() => new()
        {
            Image = "test/objects", SurfaceImage = "test/surfaces", SurfaceSize = 16,
            Columns = 1,
            Tiles = new List<RpgTileInfo>
            {
                new() { Index = 0, Surface = 0 },
                new() { Index = 3, Surface = 1, Solid = true },
                new() { Index = 11, AtlasIndex = 0, Solid = true }
            }
        };

        private static byte[] Atlas()
        {
            var pixels = new byte[32 * 16 * 4];
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 32; x++)
                {
                    pixels[(y * 32 + x) * 4 + (x < 16 ? 0 : 2)] = 255;
                    pixels[(y * 32 + x) * 4 + 3] = 255;
                }
            return pixels;
        }

        [ModTest("surfaces/packing_and_materials_json_round_trip")]
        static void PackingRoundTrip(ModTestContext ctx)
        {
            var restored = JsonSerializer.Deserialize<RpgTileset>(JsonSerializer.Serialize(Tileset()));
            ModAssert.Equal("test/surfaces", restored.SurfaceImage, "surface atlas survives serialization");
            ModAssert.Equal(16, restored.SurfaceSize, "material size survives serialization");
            ModAssert.Equal(1, restored.TileOf(3).Surface.Value, "logical surface maps to its physical patch");
            ModAssert.True(restored.TileOf(3).Diffuse, "natural surfaces diffuse by default");
            ModAssert.True(restored.IsSolid(3), "blending never changes collision");
            ModAssert.Equal(0, restored.TileOf(11).AtlasIndex.Value, "objects retain logical ids after packing");
            ModAssert.Equal(0, RpgTileSetBuilder.AtlasCoords(restored, 11).X, "world and editor find the packed object");
            restored.TileOf(3).Diffuse = false;
            restored = JsonSerializer.Deserialize<RpgTileset>(JsonSerializer.Serialize(restored));
            ModAssert.False(restored.TileOf(3).Diffuse, "hard surface edges survive serialization");
        }

        [ModTest("surfaces/blend_is_continuous_deterministic_and_leaves_objects_empty")]
        static void ContinuousBlend(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(3, 1);
            grid.SetTile(RpgLayer.Back, 0, 0, 0);
            grid.SetTile(RpgLayer.Back, 1, 0, 3);
            grid.SetTile(RpgLayer.Back, 2, 0, 11);
            var first = RpgSurfaceRenderer.Render(grid, RpgLayer.Back, Tileset(), Atlas(), 32);
            var second = RpgSurfaceRenderer.Render(grid, RpgLayer.Back, Tileset(), Atlas(), 32);
            ModAssert.True(first.AsSpan().SequenceEqual(second), "same map produces the same diffusion");
            ModAssert.Equal((byte)255, first[(16 * 96 + 16) * 4], "material interior stays intact");
            var left = (16 * 96 + 31) * 4;
            var right = (16 * 96 + 32) * 4;
            ModAssert.True(first[left] > 0 && first[left + 2] > 0, "boundary mixes both source materials");
            ModAssert.True(Math.Abs(first[left] - first[right]) < 40, "no colour jump at cell boundary");
            ModAssert.Equal((byte)0, first[(16 * 96 + 64) * 4 + 3], "object cell is transparent in surface layer");
            var tileset = Tileset();
            tileset.TileOf(3).Diffuse = false;
            var hard = RpgSurfaceRenderer.Render(grid, RpgLayer.Back, tileset, Atlas(), 32);
            ModAssert.Equal((byte)255, hard[left], "hard floor boundary keeps its own material");
            ModAssert.Equal((byte)0, hard[left + 2], "hard floor does not diffuse into its neighbour");
        }

        [ModTest("surfaces/diagonal_material_reaches_shared_corner")]
        static void DiagonalBlend(ModTestContext ctx)
        {
            var grid = new RpgMapGrid(2, 2);
            grid.SetTile(RpgLayer.Back, 0, 0, 0);
            grid.SetTile(RpgLayer.Back, 1, 0, 0);
            grid.SetTile(RpgLayer.Back, 0, 1, 0);
            grid.SetTile(RpgLayer.Back, 1, 1, 3);
            var pixels = RpgSurfaceRenderer.Render(grid, RpgLayer.Back, Tileset(), Atlas(), 32);
            ModAssert.True(pixels[(31 * 64 + 31) * 4 + 2] > 0, "diagonal corner participates in the diffusion kernel");
            ModAssert.True(RpgSurfaceRenderer.Render(grid, RpgLayer.Front, Tileset(), Atlas(), 32).AsSpan().IndexOfAnyExcept((byte)0) < 0,
                "empty layers stay transparent");
        }
    }
}
