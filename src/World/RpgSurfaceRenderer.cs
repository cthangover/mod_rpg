using System;
using Cthangover.Core.Utils;
using Godot;
using Cthangover.Mods.TileRender;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Diffuses neighbouring materials in map space at load time (and after editor changes).
    /// The content atlas contains only source patches, never edge masks or phase variants.
    /// A smooth separable kernel includes diagonal neighbours; world-anchored irregularity
    /// and material sampling continue across cell boundaries. Object cells remain transparent.
    /// The resulting layer uses the world's ordinary lighting material.
    /// </summary>
    public static class RpgSurfaceRenderer
    {
        /// <summary>Builds a transient layer texture; maps without surfaces allocate no image.</summary>
        public static Texture2D Build(RpgMapGrid grid, RpgLayer layer, RpgTileset tileset, RpgTextures textures)
        {
            if (!GameEnvironment.IsRuntime || string.IsNullOrEmpty(tileset.SurfaceImage))
                return null;
            var found = false;
            for (var y = 0; y < grid.Height && !found; y++)
                for (var x = 0; x < grid.Width && !found; x++)
                    found = tileset.TileOf(grid.GetTile(layer, x, y))?.Surface != null;
            if (!found || textures.Tileset(tileset.SurfaceImage) is not { } atlas)
                return null;
            using var source = atlas.GetImage();
            source.Convert(Image.Format.Rgba8);
            var pixels = Render(grid, layer, tileset, source.GetData(), source.GetWidth());
            using var image = Image.CreateFromData(grid.Width * tileset.TileSize, grid.Height * tileset.TileSize,
                false, Image.Format.Rgba8, pixels);
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>
        /// Renders RGBA8 pixels from a horizontal strip of square patches. This pure CPU
        /// entry point is shared by the editor, the world and tests; it calls no Godot APIs.
        /// </summary>
        public static byte[] Render(RpgMapGrid grid, RpgLayer layer, RpgTileset tileset, byte[] atlas, int atlasWidth)
            => TileSurfaceRasterizer.Render(Field(grid, layer, tileset), atlas, atlasWidth,
                new Rect2I(0, 0, grid.Width, grid.Height));

        /// <summary>Material preparation independent of pixel rasterization and map projection.</summary>
        public static TileSurfaceField Field(RpgMapGrid grid, RpgLayer layer, RpgTileset tileset)
        {
            var field = new TileSurfaceField { Width = grid.Width, Height = grid.Height,
                TileSize = tileset.TileSize, PatchSize = tileset.SurfaceSize,
                Materials = new int[grid.Width * grid.Height], Diffuse = new bool[grid.Width * grid.Height] };
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                var info = tileset.TileOf(grid.GetTile(layer, x, y));
                var i = y * grid.Width + x;
                field.Materials[i] = info?.Surface ?? -1;
                field.Diffuse[i] = info?.Diffuse ?? false;
            }
            return field;
        }
    }
}
