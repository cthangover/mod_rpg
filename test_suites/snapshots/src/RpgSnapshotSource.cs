namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Play;

    /// <summary>
    /// The only place that knows how the RPG mod builds a map view. The snapshots must
    /// survive the v2 migration, so stream R rewrites this adapter (and nothing else of
    /// the suite) when the map model changes (claude_plan.md R6).
    /// </summary>
    public static class RpgSnapshotSource
    {
        public static IReadOnlyList<string> MapIds()
            => Entities.Collect<RpgMap>().Keys.OrderBy(id => id, System.StringComparer.Ordinal).ToList();

        /// <summary>A session on a copy of the map without atmosphere effects: they animate, so pixels would never repeat.</summary>
        public static RpgWorldSession Session(string mapId)
        {
            var map = Entities.Collect<RpgMap>()[mapId];
            var copy = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(map));
            copy.Atmosphere = null;
            copy.Effects = new();
            return RpgWorldHost.CreateSession(copy);
        }
    }
}
