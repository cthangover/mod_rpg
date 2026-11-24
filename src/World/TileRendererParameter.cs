namespace Cthangover.Mods.Rpg
{
    using Cthangover.Core.Games.LaunchParameters;

    /// <summary>Selects the renderer used when the tile world is created.</summary>
    public sealed class TileRendererParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the default value.</summary>
        public TileRendererParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public TileRendererParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "tile-renderer";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Selects the renderer used when the tile world is created.";
    }
}
