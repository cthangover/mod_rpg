namespace Cthangover.Mods.Rpg.Play
{
    using System.Collections.Generic;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Time;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// Fills the lighting of a <see cref="TileFrame"/> for a v2 world: time-of-day weights, the
    /// map's fixed ambient, the portable light, and the <see cref="MaxLights"/> object lights nearest
    /// to the player. Every colour is parsed before the first frame — the map's ambient here, object
    /// lights by <see cref="RpgObjectLights"/> at load or state change — so a frame only moves and
    /// sorts ready lights (plan section 4, "Кадр").
    /// </summary>
    public sealed class RpgWorldLights
    {
        /// <summary>Lights the renderers' shader takes per frame.</summary>
        public const int MaxLights = 16;

        private readonly RpgWorldSession session;
        private readonly Color? ambient;
        private readonly List<TileLight> mapLights;
        private readonly List<TileLight> candidates = new();
        private readonly List<TileLight> moving = new();
        private Vector2 focus;

        /// <summary>Reads the map's light settings once.</summary>
        public RpgWorldLights(RpgWorldSession session)
        {
            this.session = session;
            var map = session.Map;
            mapLights = RpgMapLights.Of(map);
            var cycle = map.LightMode == null || map.LightMode == "cycle";
            ambient = cycle && !string.IsNullOrEmpty(map.Ambient) && Color.HtmlIsValid(map.Ambient) ? Color.FromHtml(map.Ambient) : null;
        }

        /// <summary>Writes this frame's weights, ambient, portable light and nearest object lights into <paramref name="frame"/>.</summary>
        public void Fill(TileFrame frame)
        {
            var controller = UiLightController.Instance;
            var hour = GameData.Instance?.Runtime?.Time?.Normalized ?? 12f;
            frame.TimeWeights = LocationLighting.Weights(hour, session.Map.LightMode, controller?.IsDarkMode == true, controller?.IsUseLight != false);
            frame.Ambient = ambient;
            frame.PortableLight = null;
            if (controller != null && controller.TryGetPortableLight(out var position, out var color, out var radius, out var influence))
                frame.PortableLight = new TileLight { Position = position, Radius = radius, Color = new Color(color, color.A * influence) };
            Nearest(frame.Lights, session.Player.Position + Vector2.One / 2);
        }

        /// <summary>The object lights (static and of visible instances) nearest to <paramref name="from"/>, at most <see cref="MaxLights"/>, into <paramref name="target"/>.</summary>
        public void Nearest(List<TileLight> target, Vector2 from)
        {
            focus = from;
            target.Clear();
            candidates.Clear();
            candidates.AddRange(mapLights);
            candidates.AddRange(session.StaticLights);
            var used = 0;
            foreach (var instance in session.Instances)
            {
                if (!instance.Visible)
                    continue;
                foreach (var light in instance.Lights)
                {
                    if (used == moving.Count)
                        moving.Add(new TileLight());
                    var placed = moving[used++];
                    placed.Position = instance.Actor.Position + light.Position;
                    placed.Radius = light.Radius;
                    placed.Color = light.Color;
                    candidates.Add(placed);
                }
            }
            candidates.Sort(CompareDistance);
            for (var i = 0; i < candidates.Count && target.Count < MaxLights; i++)
                target.Add(candidates[i]);
        }

        private int CompareDistance(TileLight a, TileLight b)
            => a.Position.DistanceSquaredTo(focus).CompareTo(b.Position.DistanceSquaredTo(focus));
    }
}
