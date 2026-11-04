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

    /// <summary>World decisions run without presentation nodes: blockers, step triggers before plain steps, states, routes, dash.</summary>
    public class RpgWorldSessionTests
    {
        private static readonly Dictionary<string, RpgObject> Objects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["pickup"] = Obj(@"{""Id"": ""pickup"", ""Form"": ""sprite"", ""Solid"": false, ""Parts"": [], ""Interact"": {""Trigger"": ""Step"", ""Commands"": [""end""]}}"),
            ["gate"] = Obj(@"{""Id"": ""gate"", ""Form"": ""sprite"", ""Parts"": [], ""Hidden"": true, ""States"": [{""When"": ""enabled"", ""Hidden"": false}]}"),
            ["sign"] = Obj(@"{""Id"": ""sign"", ""Form"": ""actor"", ""Charset"": ""none"", ""Interact"": {""Commands"": [""end""]}}"),
            ["floor"] = Obj(@"{""Id"": ""floor"", ""Form"": ""sprite"", ""Solid"": false, ""Parts"": [], ""Interact"": {""Commands"": [""end""]}}"),
            ["walker"] = Obj(@"{""Id"": ""walker"", ""Form"": ""actor"", ""Charset"": ""none""}"),
            ["chest"] = Obj(@"{""Id"": ""chest"", ""Form"": ""actor"", ""Charset"": ""none"", ""Route"": {""Every"": 0.1, ""Steps"": [""right""], ""Repeat"": false},
                ""States"": [{""When"": ""opened"", ""Facing"": ""Up"", ""Column"": 2}]}"),
        };

        private readonly List<string> effects = new();
        private bool flag;

        private static RpgObject Obj(string json) => JsonSerializer.Deserialize<RpgObject>(json, JsonOptionsProvider.ReadOptions);

        private static RpgObject Lookup(string id) => Objects.TryGetValue(id, out var value) ? value : null;

        private bool Evaluate(string condition, string scope) => string.IsNullOrEmpty(condition) || flag;

        private void OnArrival(RpgActorState actor) => effects.Add($"arrived:{actor.Cell.X},{actor.Cell.Y}");

        private void OnStep(RpgInstance instance) => effects.Add("step:" + instance.Id);

        private void OnPlain(RpgWorldSession session) => effects.Add($"plain:{session.Player.Cell.X},{session.Player.Cell.Y}");

        private RpgWorldSession Create(params string[] placements)
        {
            var map = new RpgMap { ID = "s", Width = 6, Height = 6 };
            var list = new List<RpgPlacement>();
            foreach (var json in placements)
                list.Add(JsonSerializer.Deserialize<RpgPlacement>(json, JsonOptionsProvider.ReadOptions));
            var session = new RpgWorldSession(map, Lookup, list, Evaluate, new Random(7));
            session.PlayerStepFinished += OnArrival;
            session.StepTriggered += OnStep;
            session.PlainStepFinished += OnPlain;
            return session;
        }

        [ModTest("session/blockers_stop_commands_but_finish_existing_step")]
        static void BlockersStopCommandsButFinishExistingStep(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create();
            session.Player.Place(new Vector2I(2, 2), RpgDirection.Down);
            session.Tick(0.1, true, RpgDirection.Right);
            ModAssert.True(session.Player.Cell == new Vector2I(2, 2), "blocked input cannot reserve a step");
            session.Tick(0.1, false, RpgDirection.Right);
            ModAssert.True(session.Player.Cell == new Vector2I(3, 2), "accepted command reserves destination");
            ModAssert.Equal(0, test.effects.Count, "no arrival while interpolating");
            session.Tick(1, true, RpgDirection.Right);
            ModAssert.Equal(2, test.effects.Count, "an already started step completes even after UI opens");
            ModAssert.Equal("arrived:3,2", test.effects[0], "saved arrival uses logical coordinates");
            ModAssert.Equal("plain:3,2", test.effects[1], "the plain step follows the arrival");
            session.Tick(1, true, RpgDirection.Right);
            ModAssert.Equal(2, test.effects.Count, "no repeated completion");
        }

        [ModTest("session/step_trigger_suppresses_plain_step")]
        static void StepTriggerSuppressesPlainStep(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(@"{""Id"": ""s/pickup"", ""Object"": ""pickup"", ""X"": 1, ""Y"": 0}");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal(2, test.effects.Count, "arrival and step only");
            ModAssert.Equal("arrived:1,0", test.effects[0], "position is available before running the interaction");
            ModAssert.Equal("step:s/pickup", test.effects[1], "a step trigger does not raise the plain step");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal("arrived:2,0", test.effects[2], "the next arrival is reported");
            ModAssert.Equal("plain:2,0", test.effects[3], "a free step raises the plain step, after the arrival");
        }

        [ModTest("session/map_override_allows_solid_step_events_and_npcs")]
        static void MapOverridesEvents(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var map = new RpgMap { ID = "s", Width = 6, Height = 6, Collision = new List<string> { ".oo#.." } };
            var placements = new[]
            {
                JsonSerializer.Deserialize<RpgPlacement>(@"{""Id"": ""s/event"", ""Object"": ""pickup"", ""X"": 1, ""Y"": 0, ""Solid"": true}", JsonOptionsProvider.ReadOptions),
                new RpgPlacement { ID = "s/sign", Object = "sign", X = 2, Y = 0 },
                new RpgPlacement { ID = "s/floor", Object = "floor", X = 3, Y = 0 },
            };
            var session = new RpgWorldSession(map, Lookup, placements, test.Evaluate);
            session.StepTriggered += test.OnStep;
            session.Relocate(Vector2I.Zero, RpgDirection.Right);
            ModAssert.True(session.Find("s/event").Current.IsSolid, "event's own solidity remains intact");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal(1, session.Player.Cell.X, "player enters the solid event's o cell");
            ModAssert.Equal("step:s/event", test.effects[0], "opening collision preserves the step trigger");
            session.RefreshStates();
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal(2, session.Player.Cell.X, "player enters the NPC's o cell after state refresh");
            session.Player.Face(RpgDirection.Up);
            ModAssert.True(session.InteractionTarget() == session.Find("s/sign"), "Use interaction remains available on the opened cell");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal(2, session.Player.Cell.X, "map # overrides a non-solid floor event");
        }

        [ModTest("session/hidden_states_and_collision_use_logical_passability")]
        static void HiddenStatesAndCollision(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(@"{""Id"": ""s/gate"", ""Object"": ""gate"", ""X"": 1, ""Y"": 0}");
            ModAssert.True(session.CanEnter(new Vector2I(1, 0)), "a hidden object does not block");
            test.flag = true;
            session.RefreshStates();
            ModAssert.False(session.CanEnter(new Vector2I(1, 0)), "a state that shows it makes it solid");
            session.Tick(0.1, false, RpgDirection.Right);
            ModAssert.False(session.Player.IsMoving, "blocked step does not start");
            ModAssert.True(session.Player.Facing == RpgDirection.Right, "blocked command still turns");
            ModAssert.False(session.CanEnter(new Vector2I(-1, 0)), "outside map is blocked");
        }

        [ModTest("session/action_target_and_facing_are_shared_without_view")]
        static void ActionTargetAndFacingAreSharedWithoutView(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(
                @"{""Id"": ""s/sign"", ""Object"": ""sign"", ""X"": 1, ""Y"": 0}",
                @"{""Id"": ""s/floor"", ""Object"": ""floor"", ""X"": 0, ""Y"": 0}");
            session.Player.Face(RpgDirection.Right);
            ModAssert.Equal("s/sign", session.InteractionTarget().Id, "the faced instance takes precedence over the floor");
            ModAssert.Equal("s/sign", session.PrepareInteraction().Id, "the executed target is the highlighted one");
            ModAssert.True(session.Find("s/sign").Actor.Facing == RpgDirection.Left, "an actor turns toward the player");
            session.Player.Face(RpgDirection.Down);
            ModAssert.Equal("s/floor", session.PrepareInteraction().Id, "fallback is the non-solid object under the player");
            session.Player.StepTo(RpgDirection.Down);
            ModAssert.Null(session.InteractionTarget(), "no cue during a step");
            ModAssert.Null(session.PrepareInteraction(), "no action during a step");
            ModAssert.Null(session.Find(null), "null selection is safe");
        }

        [ModTest("session/routes_reserve_destinations_and_respect_player")]
        static void RoutesReserveDestinationsAndRespectPlayer(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(
                @"{""Id"": ""s/a"", ""Object"": ""walker"", ""X"": 1, ""Y"": 1, ""Route"": {""Every"": 0.1, ""Steps"": [""right""]}}",
                @"{""Id"": ""s/b"", ""Object"": ""walker"", ""X"": 3, ""Y"": 1, ""Route"": {""Every"": 0.1, ""Steps"": [""left""]}}");
            session.Tick(0.1, false);
            ModAssert.True(session.Find("s/a").Actor.Cell == new Vector2I(2, 1), "first route reserves target");
            ModAssert.True(session.Find("s/b").Actor.Cell == new Vector2I(3, 1), "second route cannot enter the reservation");
            ModAssert.True(session.Find("s/b").Actor.Facing == RpgDirection.Left, "blocked route still faces target");
            session.Player.Place(new Vector2I(3, 1), RpgDirection.Down);
            session.Tick(1, true);
            session.Tick(0.2, false);
            ModAssert.True(session.Find("s/a").Actor.Cell == new Vector2I(2, 1), "route does not enter player cell");
        }

        [ModTest("session/state_change_retains_position_and_restarts_route")]
        static void StateChangeRetainsPositionAndRestartsRoute(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(@"{""Id"": ""s/chest"", ""Object"": ""chest"", ""X"": 1, ""Y"": 1}");
            session.Tick(1, false);
            var chest = session.Find("s/chest");
            ModAssert.True(chest.Actor.Cell == new Vector2I(2, 1), "route moved the instance");
            test.flag = true;
            session.RefreshStates();
            ModAssert.Equal(0, chest.StateIndex, "the state becomes active");
            ModAssert.Equal(2, chest.Current.Column, "the look describes the active state");
            ModAssert.True(chest.Actor.Cell == new Vector2I(2, 1), "a state does not reset position");
            ModAssert.True(chest.Actor.Facing == RpgDirection.Up, "a state naming Facing turns it");
            ModAssert.Equal(0, chest.Actor.RouteIndex, "a new state restarts the route");
            ModAssert.False(session.CanEnter(new Vector2I(2, 1)), "the moved instance blocks its new cell");
            ModAssert.True(session.CanEnter(new Vector2I(1, 1)), "and not the old one");
        }

        [ModTest("session/route_clock_pauses_while_ui_blocks_commands")]
        static void RouteClockPausesWhileUiBlocksCommands(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(@"{""Id"": ""s/cat"", ""Object"": ""walker"", ""X"": 1, ""Y"": 1, ""Route"": {""Every"": 1, ""Steps"": [""right""]}}");
            session.Tick(10, true);
            session.Tick(0.5, false);
            ModAssert.True(session.Find("s/cat").Actor.Cell == new Vector2I(1, 1), "blocked time does not accumulate route delay");
            session.Tick(0.5, false);
            ModAssert.True(session.Find("s/cat").Actor.Cell == new Vector2I(2, 1), "route starts after one unblocked second");
        }

        [ModTest("session/dash_changes_duration_not_destination")]
        static void DashChangesDurationNotDestination(ModTestContext ctx)
        {
            var normal = new RpgWorldSessionTests().Create();
            var dash = new RpgWorldSessionTests().Create();
            normal.Tick(0.15, false, RpgDirection.Right);
            dash.Tick(0.15, false, RpgDirection.Right, true);
            ModAssert.True(normal.Player.Cell == dash.Player.Cell, "same logical step");
            ModAssert.True(normal.Player.IsMoving, "normal step takes more time");
            ModAssert.False(dash.Player.IsMoving, "dash arrives sooner");
        }
        [ModTest("session/route_absence_respawn_collision_speed_and_json")]
        static void RouteAbsenceRespawn(ModTestContext ctx)
        {
            var route = new RpgMoveRoute { Every = 0.1, Speed = 1.25f, RetryBlocked = true,
                Steps = new List<string> { "hide", "wait:0.2", "reset", "show", "right", "left" } };
            var restored = JsonSerializer.Deserialize<RpgMoveRoute>(JsonSerializer.Serialize(route));
            ModAssert.Equal(route.Speed, restored.Speed, 1e-6f, "speed survives JSON");
            ModAssert.True(route.RetryBlocked == restored.RetryBlocked, "blocked retry survives JSON");
            ModAssert.Equal(string.Join(",", route.Steps), string.Join(",", restored.Steps), "all lifecycle steps survive JSON");
            Objects["crowd_test"] = new RpgObject { ID = "crowd_test", Form = RpgObjectForm.Actor, Route = restored };
            try
            {
                var session = new RpgWorldSessionTests().Create("{\"Id\":\"s/crowd\",\"Object\":\"crowd_test\",\"X\":1,\"Y\":1}");
                session.Player.Place(new Vector2I(5, 5), RpgDirection.Down);
                var npc = session.Find("s/crowd");
                session.Tick(0.11, false);
                ModAssert.True(!npc.Visible && session.CanEnter(new Vector2I(1, 1)), "departure removes visibility and collision");
                session.Tick(0.11, false);
                session.Tick(0.21, false);
                session.Player.Place(new Vector2I(1, 1), RpgDirection.Down);
                session.Tick(0.11, false);
                ModAssert.True(!npc.Visible, "never respawn on the player");
                session.Player.Place(new Vector2I(5, 5), RpgDirection.Down);
                session.Tick(0.11, false);
                ModAssert.True(npc.Visible && !session.CanEnter(new Vector2I(1, 1)), "return restores visibility and collision");
                session.Tick(0.11, false);
                ModAssert.True(npc.Actor.IsMoving && npc.Actor.Position.X < 1.5f, "walking uses slow route speed");
            }
            finally { Objects.Remove("crowd_test"); }
        }
    }
}
