using System;
using System.Globalization;
using Cthangover.Core.Actions;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// <c>action rpg.teleport [map=forest] x=3 y=7 [dir=up]</c> — moves the player
    /// to a cell, on this map or another one (then the scene switches). The same
    /// effect a teleport event has, for scenarios: a cutscene that ends with the
    /// hero waking up at home, a ferryman who takes you across.
    /// </summary>
    public class RpgTeleportAction : IScenarioAction
    {
        /// <summary>DSL name.</summary>
        public string Name => "rpg.teleport";

        /// <summary>Parses the target and hands it to the world.</summary>
        public void Run(IActionContext ctx)
        {
            var target = Target(ctx);
            if (target == null)
            {
                ctx.LogWarning("RPG", "rpg.teleport: 'x' and 'y' must be numbers");
                return;
            }
            RpgWorldHost.Teleport(target);
        }

        /// <summary>
        /// The target the parameters name, or null when <c>x</c>/<c>y</c> are not integers. Numbers are read
        /// with the invariant culture: a scenario means the same cell whatever the player's language.
        /// </summary>
        public static RpgTeleport Target(IActionContext ctx)
        {
            if (!int.TryParse(ctx.GetParam("x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(ctx.GetParam("y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                return null;
            var dir = Enum.TryParse<RpgDirection>(ctx.GetParam("dir"), true, out var parsed) ? parsed : RpgDirection.Down;
            return new RpgTeleport { Map = ctx.GetParam("map"), X = x, Y = y, Dir = dir };
        }
    }
}
