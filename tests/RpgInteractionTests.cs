namespace Cthangover.Mods.Rpg.Play.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Rpg.Model;
    using Godot;

    /// <summary>
    /// Interaction of model v2 placements (plan R4, contract 5): <c>Use</c>/<c>Step</c> on every footprint
    /// cell, states evaluated in the placement's <c>self.</c> scope (a locked door loses its teleport
    /// wholesale), <c>Hidden</c>, routes reserving their cell, lights parsed at load, and two instances
    /// of one barrel that keep their own state through a real scoped dialog.
    /// </summary>
    public class RpgInteractionTests
    {
        private const string MapId = "r4_test";

        private static readonly Dictionary<string, RpgObject> Objects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["door"] = Obj(@"{""Id"": ""door"", ""Form"": ""sprite"", ""Parts"": [],
                ""Interact"": {""Trigger"": ""Use"", ""Teleport"": {""Map"": ""house"", ""X"": 2, ""Y"": 3, ""Dir"": ""Up""}},
                ""States"": [{""Id"": ""locked"", ""When"": ""door_locked == 1"", ""Interact"": {""Commands"": [""set said locked"", ""end""]}}]}"),
            ["trigger"] = Obj(@"{""Id"": ""trigger"", ""Form"": ""sprite"", ""Solid"": false, ""Parts"": []}"),
            ["gate"] = Obj(@"{""Id"": ""gate"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 1]}, ""Parts"": [],
                ""Interact"": {""Commands"": [""set said gate"", ""end""]}}"),
            ["barrel"] = Obj(@"{""Id"": ""barrel"", ""Form"": ""sprite"", ""Parts"": [],
                ""Interact"": {""Commands"": [""flag self.open 1"", ""set barrel opened_now"", ""end""]},
                ""States"": [{""Id"": ""open"", ""When"": ""self.open == 1"", ""Interact"": {""Commands"": [""set barrel empty"", ""end""]}}]}"),
            ["wolf"] = Obj(@"{""Id"": ""wolf"", ""Form"": ""actor"", ""Charset"": ""none"", ""Facing"": ""Left"",
                ""Interact"": {""Commands"": [""action rpg.battle enemies=wolf win_flag=self.won"", ""end""]},
                ""States"": [{""Id"": ""won"", ""When"": ""self.won == 1"", ""Hidden"": true}]}"),
            ["walker"] = Obj(@"{""Id"": ""walker"", ""Form"": ""actor"", ""Charset"": ""none"", ""Route"": {""Steps"": [""right""], ""Every"": 0.5}}"),
            ["lamp"] = Obj(@"{""Id"": ""lamp"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 2]}, ""Parts"": [],
                ""Light"": {""Radius"": 2, ""Color"": ""#ff0000""}, ""Lights"": [{""At"": [0.5, 0.5], ""Color"": ""#00ff0080""}]}"),
        };

        private readonly HashSet<string> flags = new();
        private readonly List<string> scopes = new();
        private readonly List<string> effects = new();

        private static RpgObject Obj(string json) => JsonSerializer.Deserialize<RpgObject>(json, JsonOptionsProvider.ReadOptions);

        private static RpgPlacement Place(string json) => JsonSerializer.Deserialize<RpgPlacement>(json, JsonOptionsProvider.ReadOptions);

        private static RpgObject Lookup(string id) => Objects.TryGetValue(id, out var value) ? value : null;

        private bool Fake(string condition, string scope)
        {
            scopes.Add(scope);
            return flags.Contains(condition) || flags.Contains(scope + "|" + condition);
        }

        private RpgWorldSession Create(Func<string, string, bool> evaluate, params string[] placements)
        {
            var map = new RpgMap { ID = MapId, Width = 10, Height = 6 };
            var list = new List<RpgPlacement>();
            foreach (var json in placements)
                list.Add(Place(json));
            var session = new RpgWorldSession(map, Lookup, list, evaluate, new Random(3));
            session.StepTriggered += OnStep;
            session.PlayerStepFinished += OnArrival;
            return session;
        }

        private void OnStep(RpgInstance instance) => effects.Add("step:" + instance.Id);

        private void OnArrival(RpgActorState player) => effects.Add($"arrived:{player.Cell.X},{player.Cell.Y}");

        [ModTest("interaction/locked_door_loses_its_teleport")]
        static void LockedDoor(ModTestContext ctx)
        {
            var test = new RpgInteractionTests();
            test.flags.Add("door_locked == 1");
            var session = test.Create(test.Fake, @"{""Id"": ""r4_test/door"", ""Object"": ""door"", ""X"": 2, ""Y"": 2}");
            var door = session.Find("r4_test/door");
            ModAssert.Equal(0, door.StateIndex, "the locked state applies");
            ModAssert.True(test.scopes.Contains("r4_test/door"), "state conditions are evaluated in the placement's scope");
            ModAssert.False(session.CanEnter(new Vector2I(2, 2)), "a door blocks its cell");

            session.Player.Place(new Vector2I(2, 3), RpgDirection.Up);
            ModAssert.True(session.InteractionTarget() == door, "facing the door targets it");
            ModAssert.Null(RpgInteraction.TeleportOf(door, MapId), "the locked state replaced Interact wholesale: no teleport");
            var dialog = RpgInteraction.DialogOf(door, MapId);
            ModAssert.NotNull(dialog, "the locked door talks instead");
            ModAssert.Equal("r4_test/door", dialog.Scope, "its dialog runs in the placement's scope");

            test.flags.Clear();
            session.RefreshStates();
            ModAssert.Equal(-1, door.StateIndex, "unlocked: no state");
            var target = RpgInteraction.TeleportOf(session.PrepareInteraction(), MapId);
            ModAssert.NotNull(target, "the unlocked door teleports");
            ModAssert.Equal("house", target.Map, "to its map");
            ModAssert.Equal(2, target.X, "x");
            ModAssert.Equal(3, target.Y, "y");
            ModAssert.True(target.Dir == RpgDirection.Up, "facing");
            ModAssert.Null(RpgInteraction.DialogOf(door, MapId), "a teleport starts no dialog");
            ModAssert.False(session.CanEnter(new Vector2I(2, 2)), "still solid when open: the teleport fires on Use");
        }

        [ModTest("interaction/door_two_cells_wide")]
        static void TwoWideDoor(ModTestContext ctx)
        {
            var test = new RpgInteractionTests();
            var session = test.Create(test.Fake,
                @"{""Id"": ""r4_test/exit"", ""Object"": ""trigger"", ""X"": 5, ""Y"": 1, ""Footprint"": {""Size"": [2, 1]},
                   ""Interact"": {""Trigger"": ""Step"", ""Teleport"": {""Map"": ""village"", ""X"": 40, ""Y"": 12}}}",
                @"{""Id"": ""r4_test/gate"", ""Object"": ""gate"", ""X"": 1, ""Y"": 4}");

            session.Player.Place(new Vector2I(5, 2), RpgDirection.Up);
            session.Tick(1, false, RpgDirection.Up);
            ModAssert.Equal("step:r4_test/exit", test.effects[^1], "stepping on the left cell fires the exit");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal("step:r4_test/exit", test.effects[^1], "stepping on the right cell fires it too");
            ModAssert.True(session.Player.Cell == new Vector2I(6, 1), "a Step trigger without Solid is walkable");
            session.Tick(1, false, RpgDirection.Right);
            ModAssert.Equal("arrived:7,1", test.effects[^1], "past the footprint nothing fires");
            ModAssert.Equal(5, test.effects.Count, "three arrivals, two triggers");
            ModAssert.Equal("village", RpgInteraction.TeleportOf(session.Find("r4_test/exit"), MapId).Map, "the exit leaves the map");

            var gate = session.Find("r4_test/gate");
            ModAssert.False(session.CanEnter(new Vector2I(1, 4)), "the gate blocks its left cell");
            ModAssert.False(session.CanEnter(new Vector2I(2, 4)), "and its right cell");
            session.Player.Place(new Vector2I(1, 5), RpgDirection.Up);
            ModAssert.True(session.InteractionTarget() == gate, "facing the left cell targets the gate");
            session.Player.Place(new Vector2I(2, 5), RpgDirection.Up);
            ModAssert.True(session.InteractionTarget() == gate, "facing the right cell targets the same gate");
            session.Player.Place(new Vector2I(3, 4), RpgDirection.Left);
            ModAssert.True(session.InteractionTarget() == gate, "from the side as well");
            session.Player.Place(new Vector2I(3, 5), RpgDirection.Up);
            ModAssert.Null(session.InteractionTarget(), "past its footprint nothing");
        }

        [ModTest("interaction/two_barrels_are_independent", TimeoutSeconds = 10)]
        static async Task TwoBarrels(ModTestContext ctx)
        {
            var host = new Control { Name = "RpgInteractionTestHost" };
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Root.AddChild(host);
            var box = new ModScenePrefab("scenes/ui/dialog_box.tscn").Instantiate<DialogBox>();
            ModAssert.NotNull(box, "dialog box prefab");
            box.Name = "DialogBox";
            host.AddChild(box);
            await tree.ToSignal(tree, "process_frame");

            var runtime = GameData.Instance.Runtime;
            var savedFlags = runtime.Flags;
            runtime.Flags = new Dictionary<string, string>();
            try
            {
                var test = new RpgInteractionTests();
                var session = test.Create(ScenarioCondition.Evaluate,
                    @"{""Id"": ""r4_test/barrel_1"", ""Object"": ""barrel"", ""X"": 2, ""Y"": 0}",
                    @"{""Id"": ""r4_test/barrel_2"", ""Object"": ""barrel"", ""X"": 4, ""Y"": 0}");
                var first = session.Find("r4_test/barrel_1");
                var second = session.Find("r4_test/barrel_2");

                session.Player.Place(new Vector2I(2, 1), RpgDirection.Up);
                ModAssert.True(RpgInteraction.Run(session.PrepareInteraction(), MapId, box), "the first barrel's dialog runs");
                ModAssert.Equal("opened_now", box.Runtime.GetVariable("barrel"), "it opened");
                ModAssert.Equal("1", runtime.Flags.TryGetValue("r4_test/barrel_1:open", out var value) ? value : null, "self.open is the placement's flag");
                ModAssert.Equal(1, runtime.Flags.Count, "nothing else is written");

                session.RefreshStates();
                ModAssert.Equal(0, first.StateIndex, "the first barrel is open");
                ModAssert.Equal(-1, second.StateIndex, "the second barrel is still closed");
                ModAssert.True(RpgInteraction.Run(session.PrepareInteraction(), MapId, box), "the first barrel again");
                ModAssert.Equal("empty", box.Runtime.GetVariable("barrel"), "it is empty now");

                session.Player.Place(new Vector2I(4, 1), RpgDirection.Up);
                ModAssert.True(RpgInteraction.Run(session.PrepareInteraction(), MapId, box), "the second barrel's dialog runs");
                ModAssert.Equal("opened_now", box.Runtime.GetVariable("barrel"), "the twin opens on its own");
                session.RefreshStates();
                ModAssert.Equal(0, second.StateIndex, "now both are open");
                ModAssert.Equal(2, runtime.Flags.Count, "one flag per barrel");
            }
            finally
            {
                runtime.Flags = savedFlags;
                host.QueueFree();
                SceneContextNode.Instance?.ClearData();
            }
        }

        [ModTest("interaction/hidden_state_and_battle_win_flag")]
        static void HiddenAndWinFlag(ModTestContext ctx)
        {
            var test = new RpgInteractionTests();
            var session = test.Create(test.Fake, @"{""Id"": ""r4_test/wolf_1"", ""Object"": ""wolf"", ""X"": 3, ""Y"": 3}");
            var wolf = session.Find("r4_test/wolf_1");
            ModAssert.True(wolf.Visible, "the wolf stands");
            ModAssert.True(wolf.Actor.Facing == RpgDirection.Left, "with its own facing");
            ModAssert.False(session.CanEnter(new Vector2I(3, 3)), "and blocks");
            session.Player.Place(new Vector2I(4, 3), RpgDirection.Left);
            ModAssert.True(session.PrepareInteraction() == wolf, "the wolf is the target");
            ModAssert.True(wolf.Actor.Facing == RpgDirection.Right, "an actor turns to the player");

            test.flags.Add("r4_test/wolf_1|self.won == 1");
            session.RefreshStates();
            ModAssert.False(wolf.Visible, "a won wolf is hidden");
            ModAssert.True(session.CanEnter(new Vector2I(3, 3)), "hidden is not solid");
            ModAssert.Null(session.InteractionTarget(), "hidden is not interactive");
        }

        [ModTest("interaction/route_reserves_and_lights_parse_once")]
        static void RouteAndLights(ModTestContext ctx)
        {
            var test = new RpgInteractionTests();
            var session = test.Create(test.Fake,
                @"{""Id"": ""r4_test/walker"", ""Object"": ""walker"", ""X"": 0, ""Y"": 0, ""Light"": {""Color"": ""#0000ff"", ""Radius"": 1}}",
                @"{""Id"": ""r4_test/lamp"", ""Object"": ""lamp"", ""X"": 3, ""Y"": 4}");
            ModAssert.Equal(1, session.Statics.Count, "the lamp is static scenery");
            ModAssert.Equal(1, session.Instances.Count, "the walker is an instance");
            ModAssert.Equal(2, session.StaticLights.Count, "Light and Lights");
            ModAssert.True(session.StaticLights[0].Position == new Vector2(4, 5), "Light defaults to the footprint centre");
            ModAssert.True(session.StaticLights[0].Color == new Color(1, 0, 0), "colour parsed at load");
            ModAssert.True(session.StaticLights[1].Position == new Vector2(3.5f, 4.5f), "Lights entry at its At");
            ModAssert.True(Math.Abs(session.StaticLights[1].Color.A - 128 / 255f) < 0.01f, "alpha = strength");

            session.Player.Place(new Vector2I(2, 0), RpgDirection.Down);
            session.Tick(0.6, false);
            var walker = session.Find("r4_test/walker");
            ModAssert.True(walker.Actor.Cell == new Vector2I(1, 0), "the route steps right and reserves the cell at once");
            ModAssert.False(session.CanEnter(new Vector2I(1, 0)), "the reserved cell blocks");
            ModAssert.True(session.CanEnter(new Vector2I(0, 0)), "the old cell is free");
            session.Tick(0.6, false);
            session.Tick(0.6, false);
            ModAssert.True(walker.Actor.Cell == new Vector2I(1, 0), "it never walks into the player");
            ModAssert.True(walker.Actor.Facing == RpgDirection.Right, "but turns");

            var lights = new RpgWorldLights(session);
            var frame = new List<Cthangover.Mods.TileRender.TileLight>();
            lights.Nearest(frame, new Vector2(1.5f, 0.5f));
            ModAssert.Equal(3, frame.Count, "two static lights and the walker's");
            ModAssert.True(frame[0].Position == new Vector2(1.5f, 0.5f), "the walker's light follows it (centre of its cell)");
            ModAssert.True(frame[0].Color == new Color(0, 0, 1), "with the placement's colour");
        }

        [ModTest("interaction/player_look_from_leader_charset")]
        static void PlayerLook(ModTestContext ctx)
        {
            var hero = JsonSerializer.Deserialize<Cthangover.Core.Characters.CharacterInfo>(
                @"{""Id"": ""hero"", ""Charset"": ""village/hero""}", JsonOptionsProvider.ReadOptions);
            ModAssert.Equal("village/hero", RpgPlayerLook.CharsetOf(hero), "the character's Charset field");
            var plain = JsonSerializer.Deserialize<Cthangover.Core.Characters.CharacterInfo>(@"{""Id"": ""plain""}", JsonOptionsProvider.ReadOptions);
            ModAssert.Null(RpgPlayerLook.CharsetOf(plain), "no Charset");
            ModAssert.Null(RpgPlayerLook.CharsetOf(null), "no character");
        }
    }
}
