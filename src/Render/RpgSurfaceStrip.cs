namespace Cthangover.Mods.Rpg.Render
{
    using System;
    using System.Collections.Generic;
    using Godot;

    /// <summary>
    /// The surface materials one map uses, packed side by side into one in-memory strip of square
    /// patches — the input <see cref="Cthangover.Mods.TileRender.TileSurfaceCache"/> blends from. On disk
    /// every material is its own PNG (contract 4.2); the strip exists only while the map is shown,
    /// so an atlas is a rendering optimisation and never authored data. Material <c>i</c> is the
    /// patch at <c>x = i × PatchSize</c>.
    /// <para>
    /// A material that is missing, not square or of another size than the map's <c>PatchSize</c> is a
    /// load error: it gets no slot (<see cref="IndexOf"/> = -1), so its cells stay empty, and the
    /// rest of the map still draws. The strip owns its texture.
    /// </para>
    /// </summary>
    public sealed class RpgSurfaceStrip : IDisposable
    {
        private readonly Dictionary<string, int> _index = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _images = new();

        private RpgSurfaceStrip(int patchSize) { PatchSize = patchSize; }

        /// <summary>Side of every patch in pixels.</summary>
        public int PatchSize { get; }

        /// <summary>The packed strip (<c>PatchSize × count</c> by <c>PatchSize</c>, RGBA8); null when no material could be packed.</summary>
        public Texture2D Texture { get; private set; }

        /// <summary>Image paths in slot order.</summary>
        public IReadOnlyList<string> Images => _images;

        /// <summary>Slot of a material image, or -1 when it was not packed.</summary>
        public int IndexOf(string image)
            => image != null && _index.TryGetValue(image, out var i) ? i : -1;

        /// <summary>
        /// Packs <paramref name="images"/> (duplicates ignored, order kept). <paramref name="load"/> returns a
        /// borrowed texture or null; <paramref name="error"/> receives one message per rejected material.
        /// </summary>
        public static RpgSurfaceStrip Pack(IEnumerable<string> images, int patchSize, Func<string, Texture2D> load, Action<string> error)
        {
            var strip = new RpgSurfaceStrip(patchSize);
            var sources = new List<Image>();
            try
            {
                foreach (var path in images)
                {
                    if (string.IsNullOrEmpty(path) || strip._index.ContainsKey(path))
                        continue;
                    var texture = load(path);
                    if (texture == null)
                    {
                        error($"surface image '{path}' not found");
                        continue;
                    }
                    if (texture.GetWidth() != patchSize || texture.GetHeight() != patchSize)
                    {
                        error($"surface image '{path}' is {texture.GetWidth()}×{texture.GetHeight()}, the map's PatchSize is {patchSize}×{patchSize}");
                        continue;
                    }
                    var image = texture.GetImage();
                    if (image.IsCompressed())
                        image.Decompress();
                    image.Convert(Image.Format.Rgba8);
                    strip._index[path] = strip._images.Count;
                    strip._images.Add(path);
                    sources.Add(image);
                }
                if (sources.Count == 0)
                    return strip;
                using var packed = Image.CreateEmpty(patchSize * sources.Count, patchSize, false, Image.Format.Rgba8);
                for (var i = 0; i < sources.Count; i++)
                    packed.BlitRect(sources[i], new Rect2I(0, 0, patchSize, patchSize), new Vector2I(i * patchSize, 0));
                strip.Texture = ImageTexture.CreateFromImage(packed);
                return strip;
            }
            finally
            {
                foreach (var image in sources)
                    image.Dispose();
            }
        }

        /// <summary>Frees the strip texture.</summary>
        public void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
        }
    }
}
