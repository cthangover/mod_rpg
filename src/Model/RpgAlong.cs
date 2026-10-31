namespace Cthangover.Mods.Rpg.Model
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Direction of a vertical plane in the isometric view: a segment's plane
    /// (<see cref="Auto"/> = from the neighbours with the same connect key) or a
    /// <see cref="RpgPartGeometry.Plane"/> part (only <see cref="X"/>/<see cref="Y"/>).
    /// </summary>
    [JsonConverter(typeof(RpgCamelEnumConverter))]
    public enum RpgAlong
    {
        /// <summary>Segments only: along x with a left/right neighbour, along y with an up/down one, a cross with both.</summary>
        Auto = 0,

        /// <summary>Along the map's x axis (columns).</summary>
        X = 1,

        /// <summary>Along the map's y axis (rows).</summary>
        Y = 2,
    }
}
