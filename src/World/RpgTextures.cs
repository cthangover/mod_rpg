using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Image lookup of the RPG world: tileset atlases in the <c>tilesets</c> file
    /// group, charsets in <c>charsets</c>. Both are file groups (last mod wins),
    /// not texture groups, so their names never collide with backgrounds or
    /// icons; item pictures for <see cref="RpgMapEvent.Icon"/> come from the
    /// <c>items</c> group the inventory uses. Decoded textures are kept for the life of one world, which builds
    /// and owns an instance; the editor keeps its own.
    /// </summary>
    public class RpgTextures : System.IDisposable
    {
        /// <summary>File group of tileset images.</summary>
        public const string TilesetGroup = "tilesets";

        /// <summary>File group of charset images.</summary>
        public const string CharsetGroup = "charsets";

        /// <summary>File group of item pictures (an item's <c>Sprite</c>).</summary>
        public const string IconGroup = "items";

        private readonly Cthangover.Mods.TileRender.TileAssetCache cache = new();

        /// <summary>Tileset atlas image, or null (logged by the loader).</summary>
        public Texture2D Tileset(string path) => Load(TilesetGroup, path);

        /// <summary>
        /// An A2 block for this map cell. Multi-block sheets repeat by world position;
        /// regions are cached so every water cell sharing a phase reuses one texture.
        /// The editor and the world use this same lookup.
        /// </summary>
        public Texture2D Autotile(string path, int tileSize, int x, int y)
        {
            var sheet = Tileset(path);
            if (sheet == null)
                return null;
            var region = RpgAutotiles.VariantRect(new Vector2I(sheet.GetWidth(), sheet.GetHeight()), tileSize, new Vector2I(x, y));
            if (region.Position == Vector2.Zero && region.Size == sheet.GetSize())
                return sheet;
            var key = $"autotile/{path}/{tileSize}/{region.Position.X}/{region.Position.Y}";
            return cache.Region(key, sheet, region);
        }

        /// <summary>Charset image, or null.</summary>
        public Texture2D Charset(string path) => Load(CharsetGroup, path);

        /// <summary>Item picture, or null.</summary>
        public Texture2D Icon(string path) => Load(IconGroup, path);

        private Texture2D Load(string group, string path) => cache.Resolve(group, path);
        public void Dispose() => cache.Dispose();
    }
}
