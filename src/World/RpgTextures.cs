namespace Cthangover.Mods.Rpg
{
    using Godot;

    /// <summary>
    /// Image lookup of the RPG world: object pictures in <c>rpg_images</c> (model v2, contract 3) and
    /// walking sheets in <c>charsets</c>. Both are file groups (last mod wins), referenced by their path
    /// inside the group without extension, so equal file names in different folders never collide.
    /// Decoded textures are kept for the life of one world, which builds and owns an instance.
    /// </summary>
    public class RpgTextures : System.IDisposable
    {
        /// <summary>File group of walking sheets (3 columns × 4 rows, <c>TileWalkSheet</c>).</summary>
        public const string CharsetGroup = "charsets";

        /// <summary>
        /// File group of the pictures of model v2: every object part, segment and surface material is its
        /// own PNG, referenced by its path in the group without extension.
        /// </summary>
        public const string ImageGroup = "rpg_images";

        /// <summary>File group of item pictures (an item's <c>Sprite</c>).</summary>
        public const string IconGroup = "items";

        private readonly Cthangover.Mods.TileRender.TileAssetCache cache = new();

        /// <summary>Charset image, or null.</summary>
        public Texture2D Charset(string path) => Load(CharsetGroup, path);

        /// <summary>A v2 object picture (<see cref="ImageGroup"/>), or null.</summary>
        public Texture2D Image(string path) => Load(ImageGroup, path);

        /// <summary>Item picture, or null.</summary>
        public Texture2D Icon(string path) => Load(IconGroup, path);

        private Texture2D Load(string group, string path) => cache.Resolve(group, path);

        /// <summary>Releases the decoded textures of this world.</summary>
        public void Dispose() => cache.Dispose();
    }
}
