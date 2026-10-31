namespace Cthangover.Mods.Rpg.Play
{
    using System.Collections.Generic;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// Turns an object's <c>Light</c> and <c>Lights</c> (contract 5.2) into renderer lights once —
    /// at map load for static placements, at a state change for dynamic ones — so no frame parses
    /// a colour string. A light's <c>At</c> is in cells from the object origin, by default the
    /// footprint centre (a 1×1 object lights its cell centre, as v1 events did).
    /// </summary>
    public static class RpgObjectLights
    {
        /// <summary>
        /// The lights of <paramref name="effective"/> (nothing when hidden), positioned at
        /// <paramref name="origin"/> + <c>At</c>. A colour that is not <c>#rrggbb</c>/<c>#rrggbbaa</c>
        /// is an ERROR naming <paramref name="owner"/> and shines white.
        /// </summary>
        public static List<TileLight> Of(RpgObject effective, Vector2 origin, string owner)
        {
            var result = new List<TileLight>();
            if (effective == null || effective.Hidden)
                return result;
            Add(result, effective, effective.Light, origin, owner);
            if (effective.Lights != null)
            {
                foreach (var light in effective.Lights)
                    Add(result, effective, light, origin, owner);
            }
            return result;
        }

        private static void Add(List<TileLight> result, RpgObject effective, RpgObjectLight light, Vector2 origin, string owner)
        {
            if (light == null)
                return;
            var at = light.At is { Length: >= 2 } a ? new Vector2(a[0], a[1]) : new Vector2(effective.Width / 2f, effective.Height / 2f);
            Color color;
            if (!string.IsNullOrEmpty(light.Color) && Color.HtmlIsValid(light.Color))
            {
                color = Color.FromHtml(light.Color);
            }
            else
            {
                GameLogger.Log("RPG", $"'{owner}': light colour '{light.Color}' is not #rrggbb or #rrggbbaa; white used", LogLevel.Error);
                color = Colors.White;
            }
            result.Add(new TileLight { Position = origin + at, Radius = light.Radius, Color = color });
        }
    }
}
