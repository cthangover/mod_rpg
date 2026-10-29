namespace Cthangover.Mods.Rpg
{
    using System.Collections.Generic;
    /// <summary>
    /// An exact legacy tile pattern describing one whole image. Matching never changes
    /// collision or event coordinates; partial patterns remain ordinary cells.
    /// </summary>
    public class RpgVisualGroup
    {
        public string Id { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] Pattern { get; set; }
        public float AnchorX { get; set; }
        public float AnchorY { get; set; }
        /// <summary>Optional same-atlas architectural planes, instead of an upright whole image.</summary>
        public List<RpgVisualPlane> IsoPlanes { get; set; }
    }
}
