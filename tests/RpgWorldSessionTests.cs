namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Godot;

    /// <summary>World decisions run without presentation nodes, including pages, routes and arrival effects.</summary>
    public class RpgWorldSessionTests
    {
        private readonly List<string> effects = new();
        private bool flag;

        private bool Evaluate(string condition) => string.IsNullOrEmpty(condition) || flag;

        private void OnArrival(RpgActorState actor) => effects.Add($"arrived:{actor.Cell.X},{actor.Cell.Y}");

        private void OnTouch(RpgMapEvent mapEvent) => effects.Add("touch:" + mapEvent.Id);

        private void OnEncounter(string enemies) => effects.Add("encounter:" + enemies);

        private RpgWorldSession Create(IEnumerable<RpgMapEvent> events = null, RpgEncounterTable encounters = null)
        {
            var map = new RpgMap { ID = "session_test", Width = 6, Height = 6, Encounters = encounters };
            var session = new RpgWorldSession(map, null, events, Evaluate, new Random(7));
            session.PlayerStepFinished += OnArrival;
            session.TouchTriggered += OnTouch;
            session.EncounterTriggered += OnEncounter;
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
            ModAssert.Equal(1, test.effects.Count, "an already started step completes even after UI opens");
            ModAssert.Equal("arrived:3,2", test.effects[0], "saved arrival uses logical coordinates");
            session.Tick(1, true, RpgDirection.Right);
            ModAssert.Equal(1, test.effects.Count, "no repeated completion");
        }

        [ModTest("session/touch_wins_over_encounter_and_follows_arrival")]
        static void TouchWinsOverEncounterAndFollowsArrival(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(new[]
            {
                new RpgMapEvent { Id = "pickup", X = 1, Trigger = RpgEventTrigger.Touch, Solid = false }
            }, new RpgEncounterTable { Steps = 1, Groups = new() { "wolf" } });
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal(2, test.effects.Count, "arrival and touch only");
            ModAssert.Equal("arrived:1,0", test.effects[0], "position is available before running the event");
            ModAssert.Equal("touch:pickup", test.effects[1], "touch suppresses the encounter roll");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal("encounter:wolf", test.effects[3], "a free step can start a battle");
        }

        [ModTest("session/terrain_and_hidden_events_use_logical_passability")]
        static void TerrainAndHiddenEventsUseLogicalPassability(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(new[]
            {
                new RpgMapEvent { Id = "gate", X = 1, Y = 0, Tile = 5, Condition = "enabled" }
            });
            ModAssert.True(session.CanEnter(new Vector2I(1, 0)), "hidden event does not block");
            test.flag = true;
            session.RefreshEvents();
            ModAssert.False(session.CanEnter(new Vector2I(1, 0)), "active drawn event blocks even without a sprite node");
            session.Tick(0.1, false, RpgDirection.Right);
            ModAssert.False(session.Player.IsMoving, "blocked step does not start");
            ModAssert.True(session.Player.Facing == RpgDirection.Right, "blocked command still turns");
            session.Grid.SetCollision(0, 1, RpgCollision.Block);
            ModAssert.False(session.CanEnter(new Vector2I(0, 1)), "painted collision is respected");
            ModAssert.False(session.CanEnter(new Vector2I(-1, 0)), "outside map is blocked");
        }

        [ModTest("session/action_target_and_facing_are_shared_without_view")]
        static void ActionTargetAndFacingAreSharedWithoutView(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(new[]
            {
                new RpgMapEvent { Id = "sign", X = 1, Y = 0, Tile = 1 },
                new RpgMapEvent { Id = "floor", X = 0, Y = 0, Solid = false }
            });
            session.Player.Face(RpgDirection.Right);
            ModAssert.Equal("sign", session.InteractionTarget().Event.Id, "front event takes precedence over floor");
            ModAssert.Equal("sign", session.PrepareInteraction().Id, "the executed target is the highlighted one");
            ModAssert.True(session.FindEvent("sign").Facing == RpgDirection.Left, "NPC turns toward player");
            session.Player.Face(RpgDirection.Down);
            ModAssert.Equal("floor", session.PrepareInteraction().Id, "fallback is event under the player");
            session.Player.StepTo(RpgDirection.Down);
            ModAssert.Null(session.InteractionTarget(), "no cue during a step");
            ModAssert.Null(session.PrepareInteraction(), "no action during a step");
            ModAssert.Null(session.FindEvent(null), "null selection is safe");
        }

        [ModTest("session/routes_reserve_destinations_and_respect_player")]
        static void RoutesReserveDestinationsAndRespectPlayer(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(new[]
            {
                new RpgMapEvent { Id = "a", X = 1, Y = 1, Tile = 1, Route = new() { Every = 0.1, Steps = new() { "right" } } },
                new RpgMapEvent { Id = "b", X = 3, Y = 1, Tile = 1, Route = new() { Every = 0.1, Steps = new() { "left" } } }
            });
            session.Tick(0.1, false);
            ModAssert.True(session.FindEvent("a").Cell == new Vector2I(2, 1), "first route reserves target");
            ModAssert.True(session.FindEvent("b").Cell == new Vector2I(3, 1), "second route cannot enter the reservation");
            ModAssert.True(session.FindEvent("b").Facing == RpgDirection.Left, "blocked route still faces target");
            session.Player.Place(new Vector2I(3, 1), RpgDirection.Down);
            session.Tick(1, true);
            session.Tick(0.2, false);
            ModAssert.True(session.FindEvent("a").Cell == new Vector2I(2, 1), "route does not enter player cell");
        }

        [ModTest("session/page_change_retains_position_and_restarts_route")]
        static void PageChangeRetainsPositionAndRestartsRoute(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(new[]
            {
                new RpgMapEvent
                {
                    Id = "chest", X = 1, Y = 1, Tile = 14,
                    Route = new() { Every = 0.1, Steps = new() { "right" }, Repeat = false },
                    Pages = new() { new() { Condition = "opened", Tile = 15, Dir = RpgDirection.Up } }
                }
            });
            session.Tick(1, false);
            var actor = session.FindEvent("chest");
            ModAssert.True(actor.Cell == new Vector2I(2, 1), "route moved event");
            test.flag = true;
            session.RefreshEvents();
            ModAssert.Equal(1, actor.PageIndex, "conditional page becomes active");
            ModAssert.Equal(15, actor.Current.Tile, "look describes the effective page");
            ModAssert.True(actor.Cell == new Vector2I(2, 1), "page does not reset position");
            ModAssert.True(actor.Facing == RpgDirection.Up, "explicit page direction applies");
            ModAssert.Equal(0, actor.RouteIndex, "new page restarts the route");
        }

        [ModTest("session/route_clock_pauses_while_ui_blocks_commands")]
        static void RouteClockPausesWhileUiBlocksCommands(ModTestContext ctx)
        {
            var test = new RpgWorldSessionTests();
            var session = test.Create(new[]
            {
                new RpgMapEvent { Id = "cat", X = 1, Y = 1, Route = new() { Every = 1, Steps = new() { "right" } } }
            });
            session.Tick(10, true);
            session.Tick(0.5, false);
            ModAssert.True(session.FindEvent("cat").Cell == new Vector2I(1, 1), "blocked time does not accumulate route delay");
            session.Tick(0.5, false);
            ModAssert.True(session.FindEvent("cat").Cell == new Vector2I(2, 1), "route starts after one unblocked second");
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
    }
}
