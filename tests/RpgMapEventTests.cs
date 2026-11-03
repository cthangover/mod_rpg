namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Play;
    using Godot;

    /// <summary>Independent events round-trip and use the same two triggers and persistent scopes as legacy interactions.</summary>
    public class RpgMapEventTests
    {
        private bool enabled;
        private string scope;
        private int steps;
        private int plain;
        private bool Evaluate(string condition, string self) { scope = self; return !enabled; }
        private static RpgObject NoObjects(string id) => null;
        private void Step(RpgInstance instance) { steps++; scope = instance.Id; }
        private void Plain(RpgWorldSession session) => plain++;

        [ModTest("map_events/roundtrip_triggers_scope_condition_and_no_visuals")]
        static void EventContract(ModTestContext ctx)
        {
            var fixture = new RpgMapEventTests { enabled = true };
            var map = new RpgMap { ID = "event_map", Width = 5, Height = 4, Events = new List<RpgMapEvent>
            {
                new() { Id = "old_placement", Name = "event/name", X = 1, Y = 1, Width = 2, Height = 2,
                    Trigger = RpgInteractTrigger.Step, When = "self.enabled == 1", Commands = new List<string> { "flag self.done 1", "end" } },
                new() { Id = "use", X = 3, Y = 1, Trigger = RpgInteractTrigger.Use, Solid = true,
                    Scenario = "sample/dialog", Teleport = new RpgTeleport { Map = "other", X = 2, Y = 3, Dir = RpgDirection.Left } }
            } };
            var restored = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(map), JsonOptionsProvider.ReadOptions);
            var entry = restored.Events[0];
            ModAssert.Equal("old_placement", entry.Id, "local id roundtrip");
            ModAssert.Equal("event/name", entry.Name, "cue name roundtrip");
            ModAssert.True(entry.X == 1 && entry.Y == 1 && entry.Width == 2 && entry.Height == 2, "rectangle roundtrip");
            ModAssert.Equal("self.enabled == 1", entry.When, "condition roundtrip");
            ModAssert.Equal("flag self.done 1", entry.Commands[0], "inline commands roundtrip");
            ModAssert.Equal("sample/dialog", restored.Events[1].Scenario, "shared scenario roundtrip");
            ModAssert.True(restored.Events[1].Solid && restored.Events[1].Teleport.Dir == RpgDirection.Left, "barrier and teleport roundtrip");
            var session = new RpgWorldSession(restored, NoObjects, Array.Empty<RpgPlacement>(), fixture.Evaluate);
            session.StepTriggered += fixture.Step;
            session.PlainStepFinished += fixture.Plain;
            session.Relocate(new Vector2I(0, 2), RpgDirection.Right);
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal(1, fixture.steps, "bottom-left rectangle tile triggers on entry");
            ModAssert.Equal(0, fixture.plain, "step event suppresses encounter roll");
            ModAssert.Equal("event_map/old_placement", fixture.scope, "migrated placement scope stays unchanged");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal(2, fixture.steps, "bottom-right tile triggers too");
            var instance = session.Find("event_map/old_placement");
            ModAssert.True(instance.Current.Parts == null && !instance.Current.IsSolid && session.Statics.Count == 0, "ordinary events add no picture or collision");
            ModAssert.Equal(instance.Id, RpgInteraction.DialogOf(instance, restored.ID).Scope, "inline scenario gets the persistent event scope");
            fixture.enabled = false;
            session.RefreshStates();
            ModAssert.Null(session.At(new Vector2I(2, 2), RpgInteractTrigger.Step), "condition disables interaction");
            session.Relocate(new Vector2I(2, 1), RpgDirection.Right);
            ModAssert.Equal("event_map/use", session.InteractionTarget()?.Id, "Use finds an event on the faced cell");
            ModAssert.False(session.CanEnter(new Vector2I(3, 1)), "explicit action-door barrier is preserved");
        }
    }
}
