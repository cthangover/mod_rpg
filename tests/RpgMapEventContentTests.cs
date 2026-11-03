namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>Checks the migrated game's actual map data, independently of editor fixtures.</summary>
    public class RpgMapEventContentTests
    {
        [ModTest("map_events/content_scopes_rectangles_and_references")]
        static void Content(ModTestContext ctx)
        {
            var maps = Entities.Collect<RpgMap>();
            var placements = Entities.Collect<RpgPlacement>();
            var count = 0;
            foreach (var map in maps.Values)
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var placement in placements.Values) if (placement.MapId == map.ID) ids.Add(placement.ID);
                foreach (var entry in map.Events ?? new List<RpgMapEvent>())
                {
                    count++;
                    ModAssert.True(!string.IsNullOrWhiteSpace(entry.Id) && !entry.Id.Contains('/') && ids.Add(entry.Scope(map.ID)), "unique stable scope " + entry.Scope(map.ID));
                    ModAssert.True(entry.X >= 0 && entry.Y >= 0 && entry.Width > 0 && entry.Height > 0
                        && entry.Width <= map.Width - entry.X && entry.Height <= map.Height - entry.Y, "rectangle fits " + entry.Scope(map.ID));
                    ModAssert.True(entry.AsObject().Parts == null && entry.AsObject().Charset == null, "event has no rendering " + entry.Scope(map.ID));
                    if (!string.IsNullOrEmpty(entry.Scenario))
                        ModAssert.NotNull(RpgScenarioSource.ReadBody(entry.Scenario), "scenario exists for " + entry.Scope(map.ID));
                    if (entry.Teleport is { } target)
                    {
                        var targetId = string.IsNullOrEmpty(target.Map) ? map.ID : target.Map;
                        ModAssert.True(maps.TryGetValue(targetId, out var destination), "destination exists for " + entry.Scope(map.ID));
                        ModAssert.True(target.X >= 0 && target.Y >= 0 && target.X < destination.Width && target.Y < destination.Height,
                            "destination fits for " + entry.Scope(map.ID));
                    }
                }
            }
            // This suite may run on a bare engine with no content; individual events are tested separately.
            if (maps.ContainsKey("rpg_village")) ModAssert.True(count >= 28, "RPG Plus uses independent events");
            if (maps.ContainsKey("jrp_office")) ModAssert.True(count >= 19, "Junior Reaper uses independent events");
        }
    }
}
