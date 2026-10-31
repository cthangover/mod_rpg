namespace Cthangover.Mods.Rpg.Model
{
    /// <summary>
    /// The picture of a <see cref="RpgObjectForm.Segment"/> object: one cell wide, <see cref="Height"/>
    /// cells tall. Segments are painted cell by cell into the map's <c>Structure</c> rows; in
    /// isometric each cell becomes a vertical plane whose direction comes from the neighbours
    /// sharing its <see cref="Connect"/> key, so a fence of any length is one object.
    /// </summary>
    public class RpgSegment
    {
        /// <summary>Path in the <c>rpg_images</c> group without extension.</summary>
        public string Image { get; set; }

        /// <summary>Join key; null = the object id (see <see cref="ConnectKey"/>). Different fences sharing a key join each other.</summary>
        public string Connect { get; set; }

        /// <summary>Plane direction in isometric; <see cref="RpgAlong.Auto"/> = from the neighbours.</summary>
        public RpgAlong Along { get; set; } = RpgAlong.Auto;

        /// <summary>Height of the plane in cells (z).</summary>
        public float Height { get; set; } = 1f;

        /// <summary>The effective join key of a segment of object <paramref name="objectId"/>.</summary>
        public string ConnectKey(string objectId)
            => string.IsNullOrEmpty(Connect) ? objectId : Connect;
    }
}
