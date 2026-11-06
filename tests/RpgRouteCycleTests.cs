namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;

    public sealed class RpgRouteCycleTests
    {
        [ModTest("routes/variants_round_trip_and_fair_cycles")]
        static void CycleChoices(ModTestContext ctx)
        {
            var original = new RpgMoveRoute { Steps = new() { "wait" }, Every = .2, Speed = 1.4f };
            for (var i=0; i<10; i++) original.Variants.Add(new() { "right", "wait:"+(i+1), "left" });
            var route = JsonSerializer.Deserialize<RpgMoveRoute>(JsonSerializer.Serialize(original));
            ModAssert.Equal(10, route.Variants.Count, "all ten alternatives survive JSON");
            ModAssert.CloseTo(.2, route.Every, what: "command interval survives");
            ModAssert.CloseTo(1.4, route.Speed, .00001, "speed survives");
            var cycle = new RpgRouteCycle(); var random = new Random(93); var previous=-1;
            for (var round=0; round<6; round++)
            {
                var seen = new HashSet<int>();
                for (var i=0; i<10; i++)
                {
                    var steps = cycle.Steps(route,random);
                    ModAssert.True(cycle.Variant != previous && seen.Add(cycle.Variant), "each route appears once per round, with no boundary repetition");
                    ModAssert.True(steps.SequenceEqual(cycle.Steps(route,random)), "choice remains stable while instructions execute");
                    previous=cycle.Variant; cycle.Next(random);
                }
            }
            var legacy = new RpgMoveRoute { Steps = new() { "up", "down" } };
            ModAssert.True(cycle.Steps(legacy,random).SequenceEqual(legacy.Steps) && cycle.Variant == -1, "legacy Steps remains unchanged");
        }
    }
}
