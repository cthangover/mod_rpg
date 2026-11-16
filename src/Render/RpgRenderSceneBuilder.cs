using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;
using Cthangover.Mods.Rpg.Model;
using Cthangover.Mods.Rpg.Validation;
using Cthangover.Mods.TileRender;
using Godot;

namespace Cthangover.Mods.Rpg.Render
{
    /// <summary>
    /// Turns a model v2 map into the renderer-neutral <see cref = "TileRenderScene"/> (plan section 4,
    /// contract sections 4 and 8). The cost is linear in what is on the map, never
    /// groups × cells: one pass over the cells for ground and structure, then one visual per
    /// <see cref = "RpgObjectPart"/> of every static placement.
    /// <list type="bullet">
    /// <item><b>Ground</b>: the surface materials the map uses are packed into an in-memory strip
    /// (<see cref = "RpgSurfaceStrip"/>) and blended by the shared <see cref = "TileSurfaceCache"/> into
    /// 32-cell chunk textures; water cells go to <see cref = "TileRenderScene.Water"/>.</item>
    /// <item><b>Structure</b>: one visual per segment cell — upright and bottom-anchored on its cell
    /// (what a flat projection draws from <see cref = "TileVisual.Bounds"/>) with the world quad of
    /// <see cref = "RpgSegmentPlanes"/> (what a projection with height draws); a cross adds a second
    /// quad that only the isometric renderer draws.</item>
    /// <item><b>Sprites</b>: one visual per part, by geometry — <c>upright</c> (billboard on the
    /// object anchor), <c>ground</c> (flat), <c>plane</c> (one-cell vertical plane, upright in
    /// top-down), <c>quad</c> (free world quad, isometric only unless the part says otherwise).</item>
    /// </list>
    /// Dynamic placements (states, interaction, routes) and actors are not baked: the world presents
    /// them every frame as <see cref = "TileActorPose"/>s with the looks of <see cref = "PartLooks"/> and
    /// <see cref = "ActorLook"/> — an actor's sheet row depends on the projection, so even a static actor
    /// is a pose.
    /// <para>
    /// Authoring problems (a missing picture, a crop outside it, a material of the wrong size, a quad
    /// without four points) skip that one picture: each is logged once per builder as an ERROR
    /// <c>[RPG]</c> and listed in <see cref = "TileRenderScene.Warnings"/> of the scene being built, and
    /// the rest of the map still draws.
    /// </para>
    /// <para>
    /// Pictures come through the two lookups given to the constructor (file groups <c>rpg_images</c>
    /// and <c>charsets</c> at run time); their textures are borrowed. The builder owns the surface
    /// strip and the chunk textures and keeps them between builds of the same map, so rebuilding
    /// after a state change re-rasterizes nothing unless the ground changed.
    /// </para>
    /// </summary>
    public sealed class RpgRenderSceneBuilder : IDisposable
    {
        /// <summary>Id of the surface chunk visuals.</summary>
        public const string GroundVisualId = "surface/Ground";
        private const string IsometricRenderer = "isometric";
        private readonly Func<string, Texture2D> _images;
        private readonly Func<string, Texture2D> _charsets;
        private readonly TileSurfaceCache _surfaces = new();
        private readonly HashSet<string> _reported = new();
        private readonly List<string> _stripErrors = new();
        private RpgSurfaceStrip _strip;
        private string _stripKey;
        private long _revision;
        private TileRenderScene _building;
        private string _mapId;
        /// <summary>
        /// <paramref name = "images"/> returns a borrowed texture of a path in <c>rpg_images</c> (null when
        /// missing), <paramref name = "charsets"/> one of a walking sheet in <c>charsets</c>.
        /// </summary>
        public RpgRenderSceneBuilder(Func<string, Texture2D> images, Func<string, Texture2D> charsets)
        {
            _images = images;
            _charsets = charsets;
        }

        /// <summary>Surface chunks rasterized by the last <see cref = "Build"/> (0 when the ground did not change).</summary>
        public int LastBuiltSurfaceChunks { get; private set; }
        /// <summary>Bytes of the surface chunk textures currently held.</summary>
        public long SurfaceTextureBytes { get; private set; }

        /// <summary>The material strip of the current ground; null before a build or when the map has no surfaces.</summary>
        public RpgSurfaceStrip Strip
        {
            get
            {
                return _strip;
            }
        }

        /// <summary>
        /// Builds the static scene of <paramref name = "map"/>: ground and structure from
        /// <paramref name = "cells"/>, then the parts of every <c>sprite</c> in <paramref name = "statics"/>
        /// (effective objects — variant, placement fields and state already applied — at their origin
        /// cells). Hidden objects and actors are skipped.
        /// </summary>
        public TileRenderScene Build(RpgMap map, RpgMapCells cells, IEnumerable<(string Id, RpgObject Object, int X, int Y)> statics)
        {
            var tileSize = map?.TileSize > 0 ? map.TileSize : 32;
            var scene = new TileRenderScene
            {
                Revision = ++_revision,
                Size = new Vector2I(cells.Width, cells.Height),
                TileSize = tileSize,
                // Preserve fractional downscaling; magnification still uses whole pixels.
                Zoom = map?.Zoom is> 0f and < 1f ? map.Zoom : Math.Max(1f, (float)Math.Round(map?.Zoom ?? 2f,
                     MidpointRounding.AwayFromZero)),
                Atmosphere = map?.Atmosphere,
                Effects = map?.Effects ?? new List<Cthangover.Mods.Effects.Atmosphere.AtmospherePlacement>(),
            };
            _building = scene;
            _mapId = cells.MapId;
            try
            {
                AddGround(scene, cells, tileSize, map?.PatchSize > 0 ? map.PatchSize : 128);
                AddStructure(scene, cells);
                if (statics != null)
                {
                    foreach (var placed in statics)
                    {
                        if (placed.Object == null || placed.Object.Hidden || placed.Object.Form != RpgObjectForm.Sprite)
                        {
                            continue;
                        }

                        AddParts(scene.Visuals, placed.Id, placed.Object, new Vector2(placed.X, placed.Y), tileSize);
                    }
                }

                return scene;
            }
            finally
            {
                _building = null;
            }
        }

        /// <summary>
        /// Looks of a dynamic <c>sprite</c> for <see cref = "TileActorPose.Look"/>, one per part, relative to
        /// the object origin (the pose's <see cref = "TileActorPose.Position"/>). Empty for a hidden or
        /// invisible object. A pose draws its look upright, so other geometries keep their rectangle
        /// but lose their slant; a dynamic placement whose parts must lie flat or slant is rebuilt
        /// into the static scene instead.
        /// </summary>
        public List<TileVisual> PartLooks(RpgObject sprite, int tileSize)
        {
            var looks = new List<TileVisual>();
            if (sprite != null && !sprite.Hidden && sprite.Form == RpgObjectForm.Sprite)
            {
                _mapId = null;
                AddParts(looks, sprite.ID, sprite, Vector2.Zero, tileSize > 0 ? tileSize : 32);
            }

            return looks;
        }

        /// <summary>
        /// Look of an <c>actor</c> for a pose with <see cref = "TileActorPose.Charset"/> set: one frame of its
        /// walking sheet (<see cref = "TileWalkSheet"/>), standing bottom-centred on its cell; null when the
        /// sheet is missing (an ERROR) or the object is not an actor.
        /// </summary>
        public TileVisual ActorLook(RpgObject actor, int tileSize)
        {
            if (actor?.Form != RpgObjectForm.Actor)
            {
                return null;
            }

            var texture = string.IsNullOrEmpty(actor.Charset) ? null : _charsets(actor.Charset);
            if (texture == null)
            {
                _mapId = null;
                Error($"actor '{actor.ID}': walking sheet '{actor.Charset}' not found");
                return null;
            }

            var sheet = texture.GetSize();
            var animation = TileWalkSheet.Animation(texture);
            var size = TileWalkSheet.FrameSize(sheet, animation.Columns) / (tileSize > 0 ? tileSize : 32) * actor.Scale;
            return new TileVisual
            {
                Id = actor.ID,
                Texture = texture,
                Bounds = new Rect2(new Vector2(0.5f - size.X / 2, 1 - size.Y), size),
                Anchor = new Vector2(0.5f, 1),
                Source = TileWalkSheet.Frame(texture, actor.Column == TileWalkSheet.IdleColumn ? animation.IdleColumn : actor.Column,
                     (int)actor.Facing),
                Upright = true,
            };
        }

        private void AddGround(TileRenderScene scene, RpgMapCells cells, int tileSize, int patchSize)
        {
            var images = new List<string>();
            for (var y = 0; y < cells.Height; y++)
            {
                for (var x = 0; x < cells.Width; x++)
                {
                    var ground = cells.Ground(x, y);
                    if (ground == null)
                    {
                        continue;
                    }

                    if (ground.Water)
                    {
                        scene.Water.Add(new Vector2(x, y));
                    }

                    if (string.IsNullOrEmpty(ground.Surface?.Image))
                    {
                        Error($"surface '{ground.ID}' has no Surface.Image");
                    }
                    else
                    {
                        images.Add(ground.Surface.Image);
                    }
                }
            }

            var key = patchSize + "|" + string.Join("|", new SortedSet<string>(images, StringComparer.OrdinalIgnoreCase));
            if (_strip == null || _stripKey != key)
            {
                _strip?.Dispose();
                _stripErrors.Clear();
                _strip = RpgSurfaceStrip.Pack(images, patchSize, _images, StripError);
                _stripKey = key;
            }
            else
            {
                foreach (var message in _stripErrors)
                {
                    Error(message);
                }
            }

            LastBuiltSurfaceChunks = 0;
            if (_strip.Texture == null)
            {
                _surfaces.Dispose();
                SurfaceTextureBytes = 0;
                return;
            }

            var field = new TileSurfaceField
            {
                Width = cells.Width,
                Height = cells.Height,
                TileSize = tileSize,
                PatchSize = patchSize,
                Materials = new int[cells.Width * cells.Height],
                Diffuse = new bool[cells.Width * cells.Height],
                DiffuseStrength = new int[cells.Width * cells.Height],
            };
            for (var y = 0; y < cells.Height; y++)
            {
                for (var x = 0; x < cells.Width; x++)
                {
                    var i = y * cells.Width + x;
                    var surface = cells.Ground(x, y)?.Surface;
                    field.Materials[i] = _strip.IndexOf(surface?.Image);
                    field.Diffuse[i] = surface?.Diffuse ?? false;
                    field.DiffuseStrength[i] = surface?.DiffuseStrength ?? 2;
                }
            }

            _surfaces.Update(field, _strip.Texture);
            LastBuiltSurfaceChunks = _surfaces.LastBuiltChunks;
            SurfaceTextureBytes = _surfaces.TextureBytes;
            foreach (var chunk in _surfaces.Chunks)
            {
                scene.Visuals.Add(new TileVisual { 
                    Id = GroundVisualId, 
                    Texture = chunk.Texture, 
                    Layer = 0, 
                    Bounds = new Rect2(chunk.Cells.Position, chunk.Cells.Size), 
                    Source = new Rect2(Vector2.Zero, chunk.Texture.GetSize()), 
                });
            }
        }

        private void AddStructure(TileRenderScene scene, RpgMapCells cells)
        {
            for (var y = 0; y < cells.Height; y++)
            {
                for (var x = 0; x < cells.Width; x++)
                {
                    var segment = cells.Structure(x, y);
                    if (segment?.Segment == null || segment.Hidden)
                    {
                        continue;
                    }

                    var texture = Picture(segment.ID, segment.Segment.Image);
                    if (texture == null)
                    {
                        continue;
                    }

                    var height = segment.Segment.Height;
                    var quads = RpgSegmentPlanes.Quads(cells, x, y);
                    for (var q = 0; q < quads.Count; q++)
                    {
                        scene.Visuals.Add(new TileVisual { 
                            Id = segment.ID, 
                            Texture = texture, 
                            Layer = 1, 
                            Upright = true, 
                            Source = new Rect2(Vector2.Zero, texture.GetSize()), 
                            Bounds = new Rect2(x, y + 1 - height, 1, height), 
                            Anchor = new Vector2(x + 0.5f, y + 1), 
                            DrawOrder = TileDrawOrder.ByPosition, 
                            WorldQuad = quads[q], 
                            Only = q == 0 ? null : IsometricRenderer, 
                        });
                    }
                }
            }
        }

        private void AddParts(List<TileVisual> target, string id, RpgObject sprite, Vector2 origin, int tileSize)
        {
            if (sprite.Parts == null)
            {
                return;
            }

            if (sprite.Anchor is { Length: >= 2 } position
                && (!float.IsFinite(position[0]) || !float.IsFinite(position[1])))
            {
                Error($"'{id}': vector at Anchor; parts skipped");
                return;
            }

            var anchor = origin + (sprite.Anchor is { Length: >= 2 } a ? new Vector2(a[0], a[1]) : new Vector2(sprite.Width / 2f,
                 sprite.Height));
            for (var i = 0; i < sprite.Parts.Count; i++)
            {
                var part = sprite.Parts[i];
                if (part == null)
                {
                    continue;
                }

                var visual = Part(id + "#" + (part.Id ?? i.ToString(System.Globalization.CultureInfo.InvariantCulture)), sprite, part,
                     origin, anchor, tileSize);
                if (visual != null)
                {
                    target.Add(visual);
                }
            }
        }

        private TileVisual Part(string id, RpgObject sprite, RpgObjectPart part, Vector2 origin, Vector2 anchor, int tileSize)
        {
            var input = new[]
            {
                part,
            };
            var issue = RpgDataPolicy.FirstBlocking(RpgPartDataCheck.Check(input));
            if (issue != null)
            {
                Error($"'{id}': {issue.Code} at {issue.Field}; part skipped");
                return null;
            }

            var texture = Picture(id, part.Image);
            if (texture == null)
            {
                return null;
            }

            Rect2 source;
            if (part.Crop == null)
            {
                source = new Rect2(Vector2.Zero, texture.GetSize());
            }
            else if (part.Crop.Length != 4 || part.Crop[2] <= 0 || part.Crop[3] <= 0 || part.Crop[0] < 0 || part.Crop[1] < 0 
                || (long)part.Crop[0] + part.Crop[2] > texture.GetWidth() || (long)part.Crop[1] + part.Crop[3] > texture.GetHeight())
            {
                var cropText = string.Join(", ", part.Crop);
                Error($"'{id}': Crop [{cropText}] is not inside '{part.Image}' ({texture.GetWidth()}×{texture.GetHeight()}); part skipped");
                return null;
            }
            else
            {
                source = new Rect2(part.Crop[0], part.Crop[1], part.Crop[2], part.Crop[3]);
            }

            var size = part.Size is { Length: >= 2 } s ? new Vector2(s[0], s[1]) : source.Size / tileSize;
            var at = part.At is { Length: >= 2 } p ? new Vector2(p[0], p[1]) : Vector2.Zero;
            var order = part.DrawOrder ?? sprite.DrawOrder ?? (part.Geometry == RpgPartGeometry.Ground ? TileDrawOrder.BelowCharacters 
                : TileDrawOrder.ByPosition);
            var visual = new TileVisual
            {
                Id = id,
                Texture = texture,
                Source = source,
                DrawOrder = order,
                Only = part.Only,
                Layer = order == TileDrawOrder.BelowCharacters ? 0 : 1,
                Bounds = new Rect2(origin + at, size),
                Anchor = anchor,
            };
            switch (part.Geometry)
            {
                case RpgPartGeometry.Upright:
                    visual.Upright = true;
                    break;
                case RpgPartGeometry.Plane:
                    var cell = origin + new Vector2(Mathf.Floor(at.X), Mathf.Floor(at.Y));
                    visual.Upright = true;
                    visual.Bounds = new Rect2(cell.X, cell.Y + 1 - size.Y, size.X, size.Y);
                    visual.Anchor = new Vector2(cell.X + 0.5f, cell.Y + 1);
                    visual.WorldQuad = part.Along == RpgAlong.Y ? new[]
                    {
                        new Vector3(cell.X + 0.5f, cell.Y + size.X, size.Y),
                        new Vector3(cell.X + 0.5f, cell.Y, size.Y),
                        new Vector3(cell.X + 0.5f, cell.Y, 0),
                        new Vector3(cell.X + 0.5f, cell.Y + size.X, 0)
                    }

                    : new[]
                    {
                        new Vector3(cell.X, cell.Y + 0.5f, size.Y),
                        new Vector3(cell.X + size.X, cell.Y + 0.5f, size.Y),
                        new Vector3(cell.X + size.X, cell.Y + 0.5f, 0),
                        new Vector3(cell.X, cell.Y + 0.5f, 0)
                    };
                    break;
                case RpgPartGeometry.Quad:

                    visual.WorldQuad = new Vector3[4];
                    for (var k = 0; k < 4; k++)
                    {
                        visual.WorldQuad[k] = new Vector3(origin.X + part.Points[k][0], origin.Y + part.Points[k][1], part.Points[k][2]);
                    }

                    visual.Only = part.Only ?? IsometricRenderer;
                    break;
            }

            if (part.SortOffset is { Length: >= 2 } offset)
            {
                visual.SortAnchor = visual.Anchor + new Vector2(offset[0], offset[1]);
            }

            visual.Bounds = new Rect2(anchor + (visual.Bounds.Position - anchor) * sprite.Scale, visual.Bounds.Size * sprite.Scale);
            if (visual.WorldQuad != null)
            {
                var pivot = new Vector3(anchor.X, anchor.Y, 0);
                for (var k = 0; k < visual.WorldQuad.Length; k++)
                {
                    visual.WorldQuad[k] = pivot + (visual.WorldQuad[k] - pivot) * sprite.Scale;
                }
            }

            return visual;
        }


        private Texture2D Picture(string owner, string path)
        {
            var texture = string.IsNullOrEmpty(path) ? null : _images(path);
            if (texture == null)
            {
                Error($"'{owner}': picture '{path}' not found in rpg_images");
            }

            return texture;
        }

        private void StripError(string message)
        {
            _stripErrors.Add(message);
            Error(message);
        }

        private void Error(string message)
        {
            var text = _mapId == null ? message : $"map '{_mapId}': {message}";
            _building?.Warnings.Add(text);
            if (_reported.Add(text))
            {
                GameLogger.Log("RPG", text, LogLevel.Error);
            }
        }

        /// <summary>Frees the surface strip and chunk textures; borrowed pictures stay with their owner.</summary>
        public void Dispose()
        {
            _surfaces.Dispose();
            _strip?.Dispose();
            _strip = null;
            _stripKey = null;
        }
    }
}
