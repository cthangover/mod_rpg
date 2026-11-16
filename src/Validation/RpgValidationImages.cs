using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Cthangover.Mods.TileRender;
using Godot;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>Per-audit image dimensions and sidecars; decoded images are disposed immediately and never uploaded to the GPU.</summary>
    public sealed class RpgValidationImages
    {
        private readonly Dictionary<string, Vector2I?> cache = new(StringComparer.Ordinal);

        private readonly Dictionary<string, TextureFileIndex> indexes = new(StringComparer.Ordinal);

        /// <summary>Unique image decodes in this audit, including failed decodes but excluding missing references.</summary>
        public int Decoded { get; private set; }

        /// <summary>Reads normal overlay/dependency-aware file groups once per image; a missing reference is report data.</summary>
        public Vector2I? Size(string group, string id)
        {
            if (!GameEnvironment.IsRuntime || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var normalized = Normalize(group, id);
            var key = group + "/" + normalized;
            if (cache.TryGetValue(key, out var size))
            {
                return size;
            }

            var files = Files.Collect(group);
            if (!indexes.TryGetValue(group, out var index))
            {
                index = new TextureFileIndex(files.Keys, ModConfig.Instance.GetTextureExtensionSet());
                indexes.Add(group, index);
            }

            if (index.Find(normalized) == null)
            {
                cache.Add(key, null);
                return null;
            }

            Decoded++;
            using var image = TextureUtils.LoadImageFromModGroup(group, normalized);
            size = image == null || image.IsEmpty() ? null : image.GetSize();
            cache.Add(key, size);
            return size;
        }

        /// <summary>Reuses the runtime sidecar contract; sheets without a sidecar retain their existing legacy behavior.</summary>
        public string SheetProblem(string id)
        {
            if (!GameEnvironment.IsRuntime || string.IsNullOrEmpty(id))
            {
                return null;
            }

            var path = Normalize("charsets", id);
            if (Path.HasExtension(path))
            {
                path = path.Substring(0, path.Length - Path.GetExtension(path).Length);
            }

            if (!Files.Collect("charsets").TryGetValue(path + ".walk.json", out var entry))
            {
                return null;
            }

            try
            {
                var json = ModManager.Instance.ReadFileText(entry.ModId, entry.FullPath);
                var animation = JsonSerializer.Deserialize<TileWalkAnimation>(json, JsonOptionsProvider.ReadOptions);
                if (animation == null)
                {
                    return entry.ModId + "/" + entry.FullPath;
                }

                animation.Validate();
                var size = Size("charsets", id);
                return size == null || size.Value.X % animation.Columns != 0 || size.Value.Y % TileWalkSheet.Rows != 0
                    ? entry.ModId + "/" + entry.FullPath : null;
            }
            catch (Exception error) when (error is JsonException or ArgumentException)
            {
                return entry.ModId + "/" + entry.FullPath + ": " + error.Message;
            }
        }

        private static string Normalize(string group, string id)
        {
            var path = ModPath.Normalize(id).TrimStart('/');
            return path.StartsWith(group + "/", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(group.Length + 1).ToLowerInvariant() : path.ToLowerInvariant();
        }
    }
}
