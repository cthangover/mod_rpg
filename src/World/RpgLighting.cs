namespace Cthangover.Mods.Rpg
{
    using System.Collections.Generic;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Time;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>Collects logical lighting; projection and shader application belong to the renderer.</summary>
    public sealed class RpgLighting
    {
        public const int MaxLights = 16;
        public const float PlayerRadius = 3.5f;
        private static readonly Vector4 Day = new(0, 1, 0, 0);
        private static readonly Vector4 Night = new(0, 0, 0, 1);
        private readonly RpgMap map;
        private readonly List<(Vector2I Cell, RpgLight Light)> tileLights;
        private readonly List<TileLight> candidates = new();
        private Vector2 focus;
        public RpgLighting(RpgMap map, RpgMapGrid grid, RpgTileset tileset)
        {
            this.map = map;
            tileLights = tileset == null ? new() : TileLights(grid, tileset);
        }

        /// <summary>
        /// The ambient weights for an hour 0–24: night in dark mode, day while lighting is
        /// switched off, otherwise the phase blend of the time shaders.
        /// </summary>
        public static Vector4 Weights(float hour, bool darkMode, bool useLight)
        {
            if (darkMode)
                return Night;
            if (!useLight)
                return Day;
            return UiLightController.CalculateTimeWeights(hour);
        }

        /// <summary>Cells of <paramref name="grid"/> whose tile (on any layer) gives off light, with that light.</summary>
        public static List<(Vector2I Cell, RpgLight Light)> TileLights(RpgMapGrid grid, RpgTileset tileset)
        {
            var result = new List<(Vector2I, RpgLight)>();
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    foreach (var layer in new[] { RpgLayer.Back, RpgLayer.Main, RpgLayer.Front })
                    {
                        var light = tileset.LightOf(grid.GetTile(layer, x, y));
                        if (light == null)
                            continue;
                        result.Add((new Vector2I(x, y), light));
                        break;
                    }
                }
            }
            return result;
        }

        public void Fill(TileFrame frame, RpgWorldSession session)
        {
            var controller = UiLightController.Instance;
            var hour = GameData.Instance?.Runtime?.Time?.Normalized ?? 12f;
            frame.TimeWeights = Weights(hour, controller?.IsDarkMode == true, controller?.IsUseLight != false);
            frame.Ambient = !string.IsNullOrEmpty(map.Ambient) && Color.HtmlIsValid(map.Ambient) ? Color.FromHtml(map.Ambient) : null;
            frame.Lights.Clear();
            focus = session.Player.Position + Vector2.One / 2;
            frame.Lights.Add(new TileLight { Position = focus, Radius = PlayerRadius, Color = new Color(1f, 0.9f, 0.75f, 0.75f) });
            candidates.Clear();
            foreach (var (cell, light) in tileLights) Add(new Vector2(cell.X, cell.Y) + Vector2.One / 2, light);
            foreach (var actor in session.Events)
                if (actor.Visible && actor.Current?.Light != null) Add(actor.Position + Vector2.One / 2, actor.Current.Light);
            candidates.Sort(CompareDistance);
            for (var i = 0; i < candidates.Count && frame.Lights.Count < MaxLights; i++) frame.Lights.Add(candidates[i]);
        }
        private void Add(Vector2 position, RpgLight light)
            => candidates.Add(new TileLight { Position = position, Radius = light.Radius,
                Color = Color.HtmlIsValid(light.Color ?? "") ? Color.FromHtml(light.Color) : Colors.White });
        private int CompareDistance(TileLight a, TileLight b)
            => a.Position.DistanceSquaredTo(focus).CompareTo(b.Position.DistanceSquaredTo(focus));
    }
}
