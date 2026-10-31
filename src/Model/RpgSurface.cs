namespace Cthangover.Mods.Rpg.Model
{
    /// <summary>
    /// The material of a <see cref="RpgObjectForm.Surface"/> object: a square tileable patch whose
    /// side is the map's <c>PatchSize</c>. The renderer packs the materials a map uses into one
    /// in-memory strip for <c>TileSurfaceCache</c>, so a material is one PNG on disk and never
    /// part of an authored atlas.
    /// </summary>
    public class RpgSurface
    {
        /// <summary>Path in the <c>rpg_images</c> group without extension, e.g. <c>ground/grass</c>.</summary>
        public string Image { get; set; }

        /// <summary>Blend into neighbouring materials (<c>TileSurfaceRasterizer</c>); off for crisp floors.</summary>
        public bool Diffuse { get; set; } = true;

        /// <summary>Blend width level of the diffusion.</summary>
        public int DiffuseStrength { get; set; } = 2;
    }
}
