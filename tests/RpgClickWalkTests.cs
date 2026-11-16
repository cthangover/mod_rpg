using System;
using System.Collections.Generic;
using Cthangover.Core.Mods.Testing;
using Cthangover.Mods.Rpg.Model;
using Cthangover.Mods.Rpg.Play;
using Godot;

namespace Cthangover.Mods.Rpg.Tests
{
    /// <summary>Requests goals through the session API; every checked step and effect uses normal simulation and party trails.</summary>
    public sealed class RpgClickWalkTests
    {
        private readonly Dictionary<string, RpgObject> objects = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<Vector2I> arrivals = new();

        private int plain;

        private int triggered;

        private bool open;

        private RpgWorldSession observed;

        private RpgClickWalkStatus arrivalStatus;

        private RpgClickWalkStatus triggerStatus;

        [ModTest("click_walk/cardinal_path_arrival_and_party_trail")]
        static void PathAndParty(ModTestContext ctx)
        {
            var fixture = new RpgClickWalkTests();
            var map = Map();
            map.Collision = new()
            {
                "........",
                "...#....",
                "...#....",
                "...#....",
                "........",
                "........",
                "........",
                "........",
            };
            var session = fixture.Create(map);
            session.SetFollowerCount(2);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Idle, "a new session has no implicit goal");
            ModAssert.Null(session.ClickWalk.Goal, "new session goal is absent");
            ModAssert.Equal(0, session.ClickWalk.Path.Count, "no unverified path is exposed");
            var target = new Vector2I(6, 1);
            ModAssert.True(session.ClickWalk.Request(target) == RpgClickWalkStatus.Searching, "request starts a bounded search");
            session.Tick(0, false);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Walking, "small verified path becomes executable");
            ModAssert.True(session.ClickWalk.Goal == target, "requested goal is retained");
            var path = session.ClickWalk.Path;
            ModAssert.True(path[0] == new Vector2I(1, 1) && path[^1] == target, "verified path has exact endpoints");
            ModAssert.Equal(8, path.Count, "the wall is bypassed by a shortest cardinal route through row zero");
            CheckPath(session, path);
            Run(session, 1.0 / 60);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Arrived, "arrival is an explicit terminal outcome");
            ModAssert.True(session.Player.Cell == target && !session.Player.IsMoving, "the final step really finished");
            ModAssert.True(fixture.arrivalStatus == RpgClickWalkStatus.Arrived,
                "the final arrival callback already sees the terminal result in the same tick");
            ModAssert.Equal(7, fixture.arrivals.Count, "each real step emits exactly one arrival");
            ModAssert.Equal(7, fixture.plain, "ordinary plain-step effects remain attached to those arrivals");
            ModAssert.Equal(0, fixture.triggered, "navigation never invents an interaction");
            for (var index = 0; index < fixture.arrivals.Count; index++)
            {
                ModAssert.True(fixture.arrivals[index] == path[index + 1], "ordinary arrivals follow the verified path");
            }

            for (var frame = 0; frame < 120; frame++)
            {
                session.Tick(1.0 / 60, false);
            }

            ModAssert.True(session.Followers.Members[0].Cell == path[^2], "first follower uses the same trail one step behind");
            ModAssert.True(session.Followers.Members[1].Cell == path[^3], "second follower uses the same trail two steps behind");
            ModAssert.Equal(7, fixture.arrivals.Count, "idle ticks do not repeat the terminal arrival");
        }

        [ModTest("click_walk/invalid_blocked_unreachable_and_budget_outcomes")]
        static void Outcomes(ModTestContext ctx)
        {
            var fixture = new RpgClickWalkTests();
            var map = Map();
            map.Collision = new();
            for (var y = 0; y < map.Height; y++)
            {
                map.Collision.Add("....#...");
            }

            var session = fixture.Create(map);
            var start = session.Player.Cell;
            ModAssert.True(session.ClickWalk.Request(new Vector2I(-1, 0)) == RpgClickWalkStatus.InvalidGoal,
                "outside goal is rejected immediately");
            ModAssert.True(session.ClickWalk.Request(new Vector2I(4, 1)) == RpgClickWalkStatus.BlockedGoal,
                "occupied goal is rejected immediately");
            session.ClickWalk.Request(new Vector2I(6, 1));
            Run(session, 1.0 / 60);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Unreachable, "disconnected goal has an explicit outcome");
            ModAssert.Equal(0, session.ClickWalk.Path.Count, "unreachable target supplies no direct path fallback");
            ModAssert.True(session.Player.Cell == start && fixture.arrivals.Count == 0, "failed searches never start a step");
            session.ClickWalk.Request(new Vector2I(3, 3), 1);
            session.Tick(0, false);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.BudgetExhausted, "total budget is distinct from no path");
            ModAssert.Equal(1, session.ClickWalk.ExpandedNodes, "attempt never exceeds the requested work budget");
            ModAssert.Equal(0, session.ClickWalk.Path.Count, "budget exhaustion supplies no executable fallback");
            ModAssert.True(session.Player.Cell == start && !session.Player.IsMoving, "budget exhaustion leaves the player stationary");
            ModAssert.True(session.ClickWalk.Request(start) == RpgClickWalkStatus.Arrived, "current cell needs no movement");
            ModAssert.Equal(0, fixture.arrivals.Count, "already-at-goal has no synthetic arrival effect");
            session.ClickWalk.Cancel();
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Cancelled, "explicit cancellation is observable");
            ModAssert.Null(session.ClickWalk.Goal, "cancellation clears the goal");
        }

        [ModTest("click_walk/door_and_crowd_revisions_replan_safely")]
        static void Replanning(ModTestContext ctx)
        {
            var fixture = new RpgClickWalkTests();
            var door = new RpgObject
            {
                ID = "click_fixture/door_object",
                Form = RpgObjectForm.Actor,
                States = new()
                {
                    new()
                    {
                        When = "open",
                        Components = new()
                        {
                            ["Hidden"] = System.Text.Json.JsonSerializer.SerializeToElement(true),
                        },
                    },
                },
            };
            fixture.objects[door.ID] = door;
            fixture.open = true;
            var placements = new[]
            {
                new RpgPlacement
                {
                    ID = "click_fixture/door",
                    Object = door.ID,
                    X = 3,
                    Y = 1,
                },
            };
            var session = fixture.Create(Map(), placements);
            var target = new Vector2I(6, 1);
            session.ClickWalk.Request(target);
            session.Tick(0, false);
            ModAssert.True(session.Player.Cell == new Vector2I(2, 1), "first reserved step follows the open corridor");
            fixture.open = false;
            session.RefreshStates();
            var revision = session.Walk.Revision;
            ModAssert.False(session.CanEnter(new Vector2I(3, 1)), "closing the door updates actual occupancy");
            session.Tick(1, false);
            ModAssert.True(session.Walk.Revision == revision, "finishing a player step does not churn occupancy");
            session.Tick(0, false);
            CheckPath(session, session.ClickWalk.Path);
            ModAssert.False(Contains(session.ClickWalk.Path, new Vector2I(3, 1)), "replanned path avoids the closed door");
            Run(session, 1.0 / 60);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Arrived && session.Player.Cell == target,
                "door revision reaches the goal by another verified route");
            session.Relocate(new Vector2I(1, 1), RpgDirection.Right);
            fixture.open = true;
            session.RefreshStates();
            session.ClickWalk.Request(target);
            session.Tick(0, false);
            var crowd = new RpgObject
            {
                Form = RpgObjectForm.Actor
            };
            session.Walk.SetDynamic("crossing", crowd, 3, 1);
            session.Tick(1, false);
            session.Tick(0, false);
            ModAssert.False(Contains(session.ClickWalk.Path, new Vector2I(3, 1)), "new crossing occupancy invalidates the old route");
            session.Walk.RemoveDynamic("crossing");
            Run(session, 1.0 / 60);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Arrived, "a later revision can restore a shorter free route");
        }

        [ModTest("click_walk/keyboard_modal_relocation_and_step_effects")]
        static void Cancellation(ModTestContext ctx)
        {
            var fixture = new RpgClickWalkTests();
            var session = fixture.Create(Map());
            session.ClickWalk.Request(new Vector2I(6, 1));
            session.Tick(0, false);
            session.Tick(1, true);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Cancelled, "modal input ownership cancels future click intent");
            ModAssert.True(session.Player.Cell == new Vector2I(2, 1) && !session.Player.IsMoving,
                "a started step still finishes under the existing blocker contract");
            session.Tick(1, false);
            ModAssert.True(session.Player.Cell == new Vector2I(2, 1), "closing the modal never resumes a stale goal");
            session.ClickWalk.Request(new Vector2I(6, 1));
            session.Tick(1, false, RpgDirection.Down);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Cancelled, "keyboard remains primary");
            ModAssert.True(session.Player.Cell == new Vector2I(2, 2), "manual direction owns the next ordinary step");
            session.ClickWalk.Request(new Vector2I(6, 1));
            session.Relocate(new Vector2I(5, 5), RpgDirection.Left);
            ModAssert.Null(session.ClickWalk.Goal, "teleport retires the old goal");
            session.ClickWalk.Request(new Vector2I(6, 5));
            session.Player.Place(new Vector2I(1, 1), RpgDirection.Down);
            session.Tick(0, false);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Cancelled,
                "direct placements also invalidate request generation");
            var map = Map();
            map.Events = new()
            {
                new()
                {
                    Id = "step_target",
                    X = 2,
                    Y = 1,
                    Trigger = RpgInteractTrigger.Step,
                },
            };
            fixture = new RpgClickWalkTests();
            session = fixture.Create(map);
            session.ClickWalk.Request(new Vector2I(2, 1));
            Run(session, 1.0 / 60);
            ModAssert.True(fixture.triggerStatus == RpgClickWalkStatus.Arrived,
                "step interactions already see committed navigation arrival");
            session.Tick(0, true);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Arrived,
                "a modal opened by arrival does not rewrite a completed result as cancellation");
            ModAssert.Equal(1, fixture.triggered, "normal step trigger fires once on actual arrival");
            ModAssert.Equal(1, fixture.arrivals.Count, "the host's save-arrival event still fires once");
            ModAssert.Equal(0, fixture.plain, "triggered step does not also fire plain-step effects");
        }

        [ModTest("click_walk/large_map_slices_and_rate_independent_route")]
        static void Workload(ModTestContext ctx)
        {
            var fixture = new RpgClickWalkTests();
            var map = Map(128, 128);
            var session = fixture.Create(map);
            var actor = new RpgObject
            {
                Form = RpgObjectForm.Actor
            };
            for (var index = 0; index < 20; index++)
            {
                session.Walk.SetDynamic("crowd_" + index, actor, 10, 10 + index);
            }

            var target = new Vector2I(127, 127);
            session.ClickWalk.Request(target);
            session.Tick(0, false);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Searching && !session.Player.IsMoving,
                "large search yields before movement until the path is verified");
            ModAssert.True(session.ClickWalk.ExpandedNodes <= 128, "one tick respects its frontier-removal slice");
            Run(session, 1.0 / 30);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Arrived && session.Player.Cell == target,
                "128x128 map and twenty occupied NPC cells remain within the bounded request");
            ModAssert.Equal(252, fixture.arrivals.Count, "large-map route has exact shortest cardinal length");
            var slow = new RpgClickWalkTests();
            var fast = new RpgClickWalkTests();
            var slowSession = slow.Create(Map());
            var fastSession = fast.Create(Map());
            slowSession.ClickWalk.Request(new Vector2I(6, 6));
            fastSession.ClickWalk.Request(new Vector2I(6, 6));
            Run(slowSession, 1.0 / 30);
            Run(fastSession, 1.0 / 144);
            ModAssert.Equal(slow.arrivals.Count, fast.arrivals.Count, "render/update rates do not alter route steps");
            for (var index = 0; index < slow.arrivals.Count; index++)
            {
                ModAssert.True(slow.arrivals[index] == fast.arrivals[index], "tie order and arrival path are rate-independent");
            }
        }

        [ModTest("click_walk/replacement_mid_step_and_newly_occupied_goal")]
        static void Replacement(ModTestContext ctx)
        {
            var fixture = new RpgClickWalkTests();
            var session = fixture.Create(Map());
            session.ClickWalk.Request(new Vector2I(6, 1));
            session.Tick(0, false);
            ModAssert.True(session.Player.IsMoving && session.Player.Cell == new Vector2I(2, 1),
                "first step reserves its destination before a replacement request");
            var replacement = new Vector2I(2, 3);
            session.ClickWalk.Request(replacement);
            session.Tick(1, false);
            ModAssert.True(session.Player.Cell == new Vector2I(2, 1) && !session.Player.IsMoving,
                "replacement does not interrupt or teleport the existing step");
            session.Tick(0, false);
            ModAssert.True(session.ClickWalk.Path[0] == new Vector2I(2, 1),
                "replacement route starts at the completed reserved cell");
            Run(session, 1.0 / 60);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.Arrived && session.Player.Cell == replacement,
                "the replacement owns subsequent steps");
            session.Relocate(new Vector2I(1, 1), RpgDirection.Right);
            var target = new Vector2I(6, 1);
            session.ClickWalk.Request(target);
            session.Tick(0, false);
            session.Walk.SetDynamic("goal_occupant", new RpgObject { Form = RpgObjectForm.Actor }, target.X, target.Y);
            session.Tick(1, false);
            session.Tick(0, false);
            ModAssert.True(session.ClickWalk.Status == RpgClickWalkStatus.BlockedGoal,
                "a newly occupied goal becomes an explicit terminal outcome");
            ModAssert.Equal(0, session.ClickWalk.Path.Count, "stale path is retired when the goal becomes occupied");
            ModAssert.True(session.Player.Cell == new Vector2I(2, 1) && !session.Player.IsMoving,
                "the player waits safely after its already-started step");
        }

        [ModTest("click_walk/budget_validation_preserves_existing_request")]
        static void BudgetValidation(ModTestContext ctx)
        {
            var session = new RpgClickWalkTests().Create(Map());
            var target = new Vector2I(6, 1);
            session.ClickWalk.Request(target);
            var budgets = new[] { 0, 16385 };
            foreach (var budget in budgets)
            {
                var rejected = false;
                try
                {
                    session.ClickWalk.Request(new Vector2I(3, 3), budget);
                }
                catch (ArgumentOutOfRangeException)
                {
                    rejected = true;
                }

                ModAssert.True(rejected, "out-of-contract work budget is rejected");
                ModAssert.True(session.ClickWalk.Goal == target && session.ClickWalk.Status == RpgClickWalkStatus.Searching,
                    "invalid arguments do not partially replace a valid request");
                ModAssert.Equal(0, session.ClickWalk.Path.Count, "validation failure supplies no unverified path");
            }
        }

        private RpgWorldSession Create(RpgMap map, RpgPlacement[] placements = null)
        {
            var session = new RpgWorldSession(map, Lookup, placements, Evaluate, new Random(31));
            session.Relocate(new Vector2I(1, 1), RpgDirection.Right);
            observed = session;
            session.PlayerStepFinished += OnArrival;
            session.PlainStepFinished += OnPlain;
            session.StepTriggered += OnTrigger;
            return session;
        }

        private RpgObject Lookup(string id)
        {
            return objects.TryGetValue(id, out var obj) ? obj : null;
        }

        private bool Evaluate(string condition, string scope)
        {
            return string.IsNullOrWhiteSpace(condition) || open;
        }

        private void OnArrival(RpgActorState actor)
        {
            arrivals.Add(actor.Cell);
            if (observed.ClickWalk.Goal == actor.Cell)
            {
                arrivalStatus = observed.ClickWalk.Status;
            }
        }

        private void OnPlain(RpgWorldSession session)
        {
            plain++;
        }

        private void OnTrigger(RpgInstance instance)
        {
            triggered++;
            triggerStatus = observed.ClickWalk.Status;
        }

        private static RpgMap Map(int width = 8, int height = 8)
        {
            return new RpgMap
            {
                ID = "click_fixture",
                Width = width,
                Height = height,
            };
        }

        private static void Run(RpgWorldSession session, double delta)
        {
            for (var frame = 0; frame < 20000; frame++)
            {
                if (session.ClickWalk.Status is not (RpgClickWalkStatus.Searching or RpgClickWalkStatus.Walking))
                {
                    ModAssert.False(session.Player.IsMoving, "terminal navigation outcome has no unfinished navigation step");
                    return;
                }

                session.Tick(delta, false);
            }

            ModAssert.True(false, "navigation did not reach a terminal result within the bounded test clock");
        }

        private static void CheckPath(RpgWorldSession session, IReadOnlyList<Vector2I> path)
        {
            ModAssert.True(path.Count > 1, "a moving path contains a verified step");
            for (var index = 1; index < path.Count; index++)
            {
                var delta = path[index] - path[index - 1];
                ModAssert.Equal(1, Math.Abs(delta.X) + Math.Abs(delta.Y), "every path edge is cardinal");
                ModAssert.True(session.CanEnter(path[index]), "every path destination is currently free");
            }
        }

        private static bool Contains(IReadOnlyList<Vector2I> cells, Vector2I cell)
        {
            foreach (var candidate in cells)
            {
                if (candidate == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
