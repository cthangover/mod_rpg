namespace Cthangover.Mods.Rpg.Model
{
    using System.Text.Json.Serialization;

    /// <summary>How one <see cref="RpgObjectPart"/> stands in the world (contract 4.4, table "Geometries").</summary>
    [JsonConverter(typeof(RpgCamelEnumConverter))]
    public enum RpgPartGeometry
    {
        /// <summary>Faces the camera: sorted by the object anchor top-down, a billboard on the anchor in isometric.</summary>
        Upright = 0,

        /// <summary>Lies flat under characters (rug, room floor).</summary>
        Ground = 1,

        /// <summary>A vertical one-cell plane through the cell centre line along <see cref="RpgObjectPart.Along"/>.</summary>
        Plane = 2,

        /// <summary>A free quadrilateral through <see cref="RpgObjectPart.Points"/> (isometric only).</summary>
        Quad = 3,
    }
}
