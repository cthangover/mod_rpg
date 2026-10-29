namespace Cthangover.Mods.Rpg
{
    /// <summary>Crop in group cells, and four local XYZ cell coordinates (TL/TR/BR/BL).</summary>
    public class RpgVisualPlane
    {
        public int[] Crop { get; set; }
        public float[][] Points { get; set; }
    }
}
