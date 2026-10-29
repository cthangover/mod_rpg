namespace Cthangover.Mods.Rpg
{
    using System.Collections.Generic;
    using Cthangover.Core.Utils;

    /// <summary>
    /// One tileset for a map that borrows objects from others (<see cref="RpgMap.Tilesets"/>). The map's own
    /// tileset keeps its ids; the k-th extra tileset's tiles and visual groups get <c>k * SlotSize + id</c> and
    /// remember their own atlas (<see cref="RpgTileInfo.SourceImage"/>), so passability, lights, autotiles,
    /// groups and the renderer keep working on a single tileset. Surfaces are not borrowed: ground comes from
    /// the map's own tileset, whose surface strip the surface chunks are built from.
    /// Removing or reordering extras changes the ids of the following slots, so editors only append.
    /// </summary>
    public static class RpgTilesetComposer
    {
        /// <summary>Id range of one tileset slot; the map's own tileset is slot 0.</summary>
        public const int SlotSize = 100000;

        /// <summary>Slot of a map id: 0 for the map's own tileset, k for the k-th extra.</summary>
        public static int Slot(int id) => id < 0 ? 0 : id / SlotSize;

        /// <summary>The primary itself without extras; otherwise a merged copy (the inputs stay untouched).</summary>
        public static RpgTileset Compose(RpgTileset primary, IReadOnlyList<RpgTileset> extras)
        {
            if (primary == null || extras == null || extras.Count == 0)
                return primary;
            var result = new RpgTileset
            {
                ID = primary.ID, Image = primary.Image, SurfaceImage = primary.SurfaceImage,
                SurfaceSize = primary.SurfaceSize, TileSize = primary.TileSize, Columns = primary.Columns,
                Tiles = new List<RpgTileInfo>(primary.Tiles ?? new List<RpgTileInfo>()),
                VisualGroups = new List<RpgVisualGroup>(primary.VisualGroups ?? new List<RpgVisualGroup>())
            };
            for (var k = 0; k < extras.Count; k++)
            {
                var extra = extras[k];
                if (extra == null)
                    continue;
                if (extra.TileSize != primary.TileSize)
                {
                    GameLogger.Log("RPG", $"tileset '{extra.ID}' has {extra.TileSize}px tiles, the map's '{primary.ID}' {primary.TileSize}px; not borrowed", LogLevel.Warning);
                    continue;
                }
                var offset = (k + 1) * SlotSize;
                foreach (var tile in extra.Tiles ?? new List<RpgTileInfo>())
                {
                    if (tile.Surface != null)
                        continue;
                    var borrowed = string.IsNullOrEmpty(tile.Autotile);
                    result.Tiles.Add(new RpgTileInfo
                    {
                        Index = offset + tile.Index, AtlasIndex = borrowed ? tile.AtlasIndex ?? tile.Index : null,
                        Diffuse = tile.Diffuse, Solid = tile.Solid, Tag = tile.Tag, Light = tile.Light, Autotile = tile.Autotile,
                        Plane = tile.Plane, Layer = tile.Layer, SourceImage = borrowed ? extra.Image : null, SourceColumns = extra.Columns
                    });
                }
                foreach (var group in extra.VisualGroups ?? new List<RpgVisualGroup>())
                {
                    var pattern = new int[group.Pattern?.Length ?? 0];
                    for (var i = 0; i < pattern.Length; i++)
                        pattern[i] = offset + group.Pattern[i];
                    result.VisualGroups.Add(new RpgVisualGroup { Id = extra.ID + "/" + group.Id, Width = group.Width, Height = group.Height,
                        Pattern = pattern, AnchorX = group.AnchorX, AnchorY = group.AnchorY, IsoPlanes = group.IsoPlanes });
                }
            }
            return result;
        }
    }
}
