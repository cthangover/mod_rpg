namespace Cthangover.Mods.Rpg.Play
{
    using System.Collections.Generic;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;
    using Cthangover.Mods.Time;
    using Godot;

    /// <summary>Adapts shared 2D light definitions from normalized map space to the tile renderer's cell space.</summary>
    public static class RpgMapLights
    {
        /// <summary>Converts resolved map lights once, after resource overrides and keyed patches have been applied.</summary>
        public static List<TileLight> Of(RpgMap map)
        {
            var result = new List<TileLight>();
            var ids = new HashSet<string>();
            foreach (var light in map?.Lights ?? new List<LightDef>())
            {
                if (light == null || string.IsNullOrWhiteSpace(light.Id) || !ids.Add(light.Id)
                    || !float.IsFinite(light.X) || !float.IsFinite(light.Y)
                    || !float.IsFinite(light.Radius) || light.Radius <= 0 || !float.IsFinite(light.Influence) || light.Influence < 0 || light.Influence > 1
                    || !Color.HtmlIsValid(light.ColorHex))
                {
                    GameLogger.Log("RPG", $"map '{map?.ID}': invalid light '{light?.Id}'", LogLevel.Error);
                    continue;
                }
                var color = light.ToColor();
                result.Add(new TileLight { ViewSpace = true, Position = light.ToPixelPos(new Vector2(map.Width, map.Height)), Radius = light.Radius,
                    Color = new Color(color, color.A * light.Influence) });
            }
            return result;
        }
    }
}
