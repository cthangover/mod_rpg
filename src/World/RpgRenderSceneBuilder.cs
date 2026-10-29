namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Mods.TileRender;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Converts the existing map format into renderer-neutral image primitives. No
    /// projection, collision decision or scenario execution belongs in this adapter.
    /// </summary>
    public class RpgRenderSceneBuilder : IDisposable
    {
        private readonly RpgTextures textures;
        private readonly Dictionary<RpgLayer, TileSurfaceCache> surfaceCaches = new();
        private readonly HashSet<string> reported = new();
        private long revision;
        public int LastBuiltSurfaceChunks { get; private set; }
        public long SurfaceTextureBytes { get; private set; }
        public RpgRenderSceneBuilder(RpgTextures textures) { this.textures = textures; }

        public TileRenderScene Build(RpgMap map, RpgMapGrid grid, RpgTileset tileset)
        {
            var scene = new TileRenderScene { Revision = ++revision, Size = new Vector2I(grid.Width, grid.Height), TileSize = tileset?.TileSize ?? 32,
                Zoom = Math.Max(1, map.Zoom), Atmosphere = map.Atmosphere };
            LastBuiltSurfaceChunks = 0;
            SurfaceTextureBytes = 0;
            if (tileset == null) return scene;
            var grouped = new HashSet<(int Layer, int X, int Y)>();
            AddGroups(scene, grid, tileset, grouped);
            var fragments = RpgGroupFragments.Of(tileset);
            foreach (var layer in new[] { RpgLayer.Back, RpgLayer.Main, RpgLayer.Front })
            {
                if (!string.IsNullOrEmpty(tileset.SurfaceImage) && textures.Tileset(tileset.SurfaceImage) is { } source)
                {
                    if (!surfaceCaches.TryGetValue(layer, out var cache)) surfaceCaches[layer] = cache = new TileSurfaceCache();
                    cache.Update(RpgSurfaceRenderer.Field(grid, layer, tileset), source);
                    LastBuiltSurfaceChunks += cache.LastBuiltChunks;
                    SurfaceTextureBytes += cache.TextureBytes;
                    foreach (var chunk in cache.Chunks)
                        scene.Visuals.Add(new TileVisual { Id = "surface/" + layer, Texture = chunk.Texture, Layer = (int)layer,
                            Bounds = new Rect2(chunk.Cells.Position, chunk.Cells.Size),
                            Source = new Rect2(Vector2.Zero, chunk.Texture.GetSize()) });
                }
                for (var y = 0; y < grid.Height; y++)
                for (var x = 0; x < grid.Width; x++)
                {
                    var id = grid.GetTile(layer, x, y);
                    if (id < 0) continue;
                    var info = tileset.TileOf(id);
                    if (info?.Tag == "water" && layer == RpgLayer.Back) scene.Water.Add(new Vector2(x, y));
                    if (info?.Surface != null) continue;
                    var only = grouped.Contains(((int)layer, x, y)) ? "topdown" : null;
                    if (tileset.AutotileOf(id) is { } autotile)
                    {
                        var block = textures.Autotile(autotile.Autotile, tileset.TileSize, x, y);
                        var quarters = RpgAutotiles.QuartersAt(grid, layer, x, y, id);
                        for (var q = 0; q < 4; q++)
                            scene.Visuals.Add(new TileVisual { Texture = block, Layer = (int)layer,
                                Bounds = new Rect2(x + q % 2 * 0.5f, y + q / 2 * 0.5f, 0.5f, 0.5f),
                                Source = RpgAutotiles.SourceRect(quarters[q], tileset.TileSize) });
                    }
                    else if (AtlasOf(tileset, id) is { } atlas)
                    {
                        var planes = only == null && layer != RpgLayer.Back && info?.Plane != null;
                        var fragment = only == null && fragments.TryGetValue(id, out var part) ? part : default;
                        scene.Visuals.Add(new TileVisual { Texture = atlas, Source = TileRegion(tileset, id),
                            Bounds = new Rect2(x, y, 1, 1), Anchor = new Vector2(x + 0.5f, y + 1),
                            Layer = (int)layer, Only = planes || fragment.Group != null ? "topdown" : only, Upright = layer != RpgLayer.Back });
                        if (fragment.Group != null)
                            scene.Visuals.Add(new TileVisual { Id = fragment.Group.Id, Texture = atlas, Source = TileRegion(tileset, id),
                                Bounds = new Rect2(x, y, 1, 1), Layer = (int)layer, Upright = true, Only = "isometric",
                                Anchor = new Vector2(x - fragment.Cell.X + fragment.Group.AnchorX, y - fragment.Cell.Y + fragment.Group.AnchorY) });
                        if (planes)
                            foreach (var quad in RpgTilePlanes.Quads(grid, tileset, layer, x, y, info.Plane))
                                scene.Visuals.Add(new TileVisual { Texture = atlas, Source = TileRegion(tileset, id),
                                    Bounds = new Rect2(x, y, 1, 1), Anchor = new Vector2(x + 0.5f, y + 0.5f), Layer = (int)layer,
                                    Only = "isometric", WorldQuad = quad, Upright = true });
                    }
                }
            }
            ReportPartial(scene, map.ID, grid, tileset, grouped);
            return scene;
        }

        private void ReportPartial(TileRenderScene scene, string mapId, RpgMapGrid grid, RpgTileset tileset,
            HashSet<(int Layer, int X, int Y)> used)
        {
            foreach (var group in tileset.VisualGroups ?? new List<RpgVisualGroup>())
            {
                var ids = new HashSet<int>(group.Pattern);
                var partial = false;
                for (var layer = 0; layer < 3 && !partial; layer++)
                for (var y = 0; y < grid.Height && !partial; y++)
                for (var x = 0; x < grid.Width && !partial; x++)
                    partial = !used.Contains((layer, x, y)) && ids.Contains(grid.GetTile((RpgLayer)layer, x, y));
                if (!partial) continue;
                var message = $"map '{mapId}': incomplete visual group '{group.Id}', drawing the remaining cells in place of the whole object";
                scene.Warnings.Add(message);
                if (reported.Add(mapId + "/" + tileset.ID + "/" + group.Id)) GameLogger.Log("RPG", message, LogLevel.Warning);
            }
        }

        private void AddGroups(TileRenderScene scene, RpgMapGrid grid, RpgTileset tileset,
            HashSet<(int Layer, int X, int Y)> grouped)
        {
            foreach (var group in tileset.VisualGroups ?? new List<RpgVisualGroup>())
            {
                var error = RpgVisualGroupValidator.Error(group);
                if (error != null) throw new ArgumentException(error + ": " + group?.Id);
                if (AtlasOf(tileset, group.Pattern[0]) is not { } atlas) continue;
                if (group.IsoPlanes?.Count > 0 && !PackedRectangle(group, tileset))
                    throw new ArgumentException("Architectural source must remain a packed rectangle: " + group.Id);
                for (var y = 0; y <= grid.Height - group.Height; y++)
                for (var x = 0; x <= grid.Width - group.Width; x++)
                {
                    var matched = Match(grid, group, x, y, grouped);
                    if (matched == null) continue;
                    foreach (var cell in matched) grouped.Add(cell);
                    if (group.IsoPlanes?.Count > 0)
                    {
                        if (group.Pattern.Any(id => tileset.TileOf(id)?.Layer != null))
                            AddLayeredPlanes(scene, group, x, y, tileset, atlas, matched);
                        else
                            AddPlanes(scene, group, x, y, tileset, atlas);
                        continue;
                    }
                    if (PackedRectangle(group, tileset) && matched.All(c => c.Layer == matched[0].Layer))
                    {
                        scene.Visuals.Add(new TileVisual { Id = group.Id, Texture = atlas,
                            Source = new Rect2(TileRegion(tileset, group.Pattern[0]).Position, new Vector2(group.Width, group.Height) * tileset.TileSize),
                            Bounds = new Rect2(x, y, group.Width, group.Height), Anchor = new Vector2(x + group.AnchorX, y + group.AnchorY),
                            Layer = group.Pattern.Any(id => tileset.TileOf(id)?.Layer != null) ? matched[0].Layer : 1,
                            Upright = true, Only = "isometric" });
                        continue;
                    }
                    for (var dy = 0; dy < group.Height; dy++)
                    for (var dx = 0; dx < group.Width; dx++)
                    {
                        var id = group.Pattern[dy * group.Width + dx];
                        scene.Visuals.Add(new TileVisual { Id = group.Id, Texture = atlas, Source = TileRegion(tileset, id),
                            Bounds = new Rect2(x + dx, y + dy, 1, 1), Anchor = new Vector2(x + group.AnchorX, y + group.AnchorY),
                            Layer = matched[dy * group.Width + dx].Layer, Upright = true, Only = "isometric" });
                    }
                }
            }
        }

        private static bool PackedRectangle(RpgVisualGroup group, RpgTileset tileset)
        {
            var first = RpgTileSetBuilder.AtlasCoords(tileset, group.Pattern[0]);
            for (var y = 0; y < group.Height; y++) for (var x = 0; x < group.Width; x++)
                if (RpgTileSetBuilder.AtlasCoords(tileset, group.Pattern[y * group.Width + x]) != first + new Vector2I(x, y)) return false;
            return true;
        }

        /// <summary>Exact source-cell consumption; no inference from tags, opacity or collision.</summary>
        private static List<(int Layer, int X, int Y)> Match(RpgMapGrid grid, RpgVisualGroup group, int x, int y,
            HashSet<(int Layer, int X, int Y)> used)
        {
            var first = group.Pattern[0];
            if (grid.GetTile(RpgLayer.Back, x, y) != first && grid.GetTile(RpgLayer.Main, x, y) != first && grid.GetTile(RpgLayer.Front, x, y) != first) return null;
            var result = new List<(int, int, int)>();
            for (var dy = 0; dy < group.Height; dy++)
            for (var dx = 0; dx < group.Width; dx++)
            {
                var found = -1;
                for (var layer = 0; layer < 3; layer++)
                    if (!used.Contains((layer, x + dx, y + dy)) && grid.GetTile((RpgLayer)layer, x + dx, y + dy) == group.Pattern[dy * group.Width + dx])
                    { found = layer; break; }
                if (found < 0) return null;
                result.Add((found, x + dx, y + dy));
            }
            return result;
        }

        private static Vector3 PlanePoint(Vector3[] corners, float u, float v)
            => corners[0].Lerp(corners[1], u).Lerp(corners[3].Lerp(corners[2], u), v);

        private static void AddLayeredPlanes(TileRenderScene scene, RpgVisualGroup group, int x, int y,
            RpgTileset tileset, Texture2D atlas, List<(int Layer, int X, int Y)> matched)
        {
            var start = TileRegion(tileset, group.Pattern[0]).Position;
            foreach (var plane in group.IsoPlanes)
            {
                var corners = plane.Points.Select(p => new Vector3(x + p[0], y + p[1], p[2])).ToArray();
                var crop = plane.Crop;
                for (var cy = 0; cy < crop[3]; cy++)
                    for (var cx = 0; cx < crop[2]; cx++)
                    {
                        var u = cx / (float)crop[2];
                        var v = cy / (float)crop[3];
                        var u1 = (cx + 1) / (float)crop[2];
                        var v1 = (cy + 1) / (float)crop[3];
                        scene.Visuals.Add(new TileVisual { Id = group.Id, Texture = atlas, Only = "isometric",
                            Layer = matched[(crop[1] + cy) * group.Width + crop[0] + cx].Layer,
                            Bounds = new Rect2(x, y, group.Width, group.Height),
                            Anchor = new Vector2(x + group.AnchorX, y + group.AnchorY),
                            WorldQuad = new[] { PlanePoint(corners, u, v), PlanePoint(corners, u1, v), PlanePoint(corners, u1, v1), PlanePoint(corners, u, v1) },
                            Source = new Rect2(start + new Vector2(crop[0] + cx, crop[1] + cy) * tileset.TileSize, Vector2.One * tileset.TileSize) });
                    }
            }
        }

        private static void AddPlanes(TileRenderScene scene, RpgVisualGroup group, int x, int y, RpgTileset tileset, Texture2D atlas)
        {
            var start = TileRegion(tileset, group.Pattern[0]).Position;
            foreach (var plane in group.IsoPlanes)
            {
                if (plane.Crop?.Length != 4 || plane.Points?.Length != 4) throw new ArgumentException("Invalid architectural plane: " + group.Id);
                var points = new Vector3[4];
                for (var i = 0; i < 4; i++)
                    points[i] = new Vector3(x + plane.Points[i][0], y + plane.Points[i][1], plane.Points[i][2]);
                scene.Visuals.Add(new TileVisual { Id = group.Id, Texture = atlas, Only = "isometric", Layer = 0,
                    Bounds = new Rect2(x, y, group.Width, group.Height), WorldQuad = points,
                    Source = new Rect2(start + new Vector2(plane.Crop[0], plane.Crop[1]) * tileset.TileSize,
                        new Vector2(plane.Crop[2], plane.Crop[3]) * tileset.TileSize) });
            }
        }

        /// <summary>Atlas image a tile is cut from: its own tileset's, or the one it was borrowed from.</summary>
        private Texture2D AtlasOf(RpgTileset tileset, int id) => textures.Tileset(tileset.TileOf(id)?.SourceImage ?? tileset.Image);

        public static Rect2 TileRegion(RpgTileset tileset, int id)
            => new(RpgTileSetBuilder.AtlasCoords(tileset, id) * tileset.TileSize, Vector2.One * tileset.TileSize);

        public void Dispose()
        {
            foreach (var cache in surfaceCaches.Values) cache.Dispose();
            surfaceCaches.Clear();
        }

        public TileVisual ActorLook(RpgActorState actor, RpgTileset tileset, bool player)
        {
            var page = actor.Current;
            var charset = player ? RpgPlayerLook.Charset() : page?.Sprite;
            if (!string.IsNullOrEmpty(charset))
            {
                var texture = textures.Charset(charset);
                if (texture == null) return null;
                var size = texture.GetSize() / new Vector2(3, 4) / (tileset?.TileSize ?? 32);
                return new TileVisual { Texture = texture, Bounds = new Rect2(new Vector2(0.5f - size.X / 2, 1 - size.Y), size),
                    Anchor = new Vector2(0.5f, 1), Source = new Rect2(Vector2.Zero, texture.GetSize()) };
            }
            if (page?.Tile >= 0 && tileset != null)
                return new TileVisual { Texture = AtlasOf(tileset, page.Tile), Source = TileRegion(tileset, page.Tile),
                    Bounds = new Rect2(0, 0, 1, 1), Anchor = new Vector2(0.5f, 1) };
            if (!string.IsNullOrEmpty(page?.Icon))
            {
                var texture = textures.Icon(page.Icon);
                if (texture == null) return null;
                var size = texture.GetSize() * (0.75f / Math.Max(texture.GetWidth(), texture.GetHeight()));
                return new TileVisual { Texture = texture, Source = new Rect2(Vector2.Zero, texture.GetSize()),
                    Bounds = new Rect2((Vector2.One - size) / 2, size), Anchor = new Vector2(0.5f, 1) };
            }
            return null;
        }

        /// <summary>Editor appearance without running conditions, routes or gameplay events.</summary>
        public TileVisual EventLook(RpgMapEvent mapEvent, RpgTileset tileset)
            => ActorLook(new RpgActorState(mapEvent) { Current = mapEvent }, tileset, false);
    }
}
