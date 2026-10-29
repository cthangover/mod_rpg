namespace Cthangover.Mods.Rpg
{
    using Godot;
    /// <summary>
    /// Resolves logical tile IDs to source atlas coordinates for the palette and
    /// scene adapter. Rendering and projection belong to the tile renderer mods.
    /// </summary>
    public static class RpgTileSetBuilder
    {
        /// <summary>Legacy single-atlas source id for tools consuming atlas metadata.</summary>
        public const int SourceId = 0;

        /// <summary>Atlas coordinate of a row-major index.</summary>
        public static Vector2I AtlasCoords(RpgTileset tileset, int index)
        {
            index = tileset.TileOf(index)?.AtlasIndex ?? index;
            var columns = System.Math.Max(1, tileset.Columns);
            return new Vector2I(index % columns, index / columns);
        }

        /// <summary>Number of tiles the image holds.</summary>
        public static int TileCount(RpgTileset tileset, Texture2D texture)
        {
            if (texture == null || tileset.TileSize <= 0)
                return 0;
            return (texture.GetWidth() / tileset.TileSize) * (texture.GetHeight() / tileset.TileSize);
        }
    }
}
