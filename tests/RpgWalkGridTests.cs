using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods.Testing;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Rpg.Model.Tests
{
    /// <summary>
    /// Walkability of a v2 map: brush objects (solid water, fences), footprint masks of static and
    /// dynamic placements and the map's authoritative collision overrides: forced-open beats every object.
    /// </summary>
    public class RpgWalkGridTests
    {
        private static readonly Dictionary<string, RpgObject> Objects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["grass"] = Obj(@"{""Id"": ""grass"", ""Form"": ""surface"", ""Surface"": {""Image"": ""g""}}"),
            ["water"] = Obj("{\"Id\": \"water\", \"Form\": \"surface\", \"Solid\": true, \"Water\": t" +
                "rue, \"Surface\": {\"Image\": \"w\"}}"),
            ["fence"] = Obj(@"{""Id"": ""fence"", ""Form"": ""segment"", ""Segment"": {""Image"": ""f""}}"),
            ["hedge_gap"] = Obj("{\"Id\": \"hedge_gap\", \"Form\": \"segment\", \"Solid\": false, \"Segm" +
                "ent\": {\"Image\": \"h\"}}"),
        };
        // Ground: water at (5,0), (6,0), (6,1); Structure: fences at (0,1), (1,1), a walkable hedge at (2,1);
        // Collision: 'o' at (6,0), '#' at (0,2).
        private const string Field = @"{""Id"": ""field"", ""Width"": 7, ""Height"": 3,
            ""Legend"": {""g"": ""grass"", ""w"": ""water"", ""f"": ""fence"", ""h"": ""hedge_gap""},
            ""Ground"": [""gggggww"", ""ggggggw"", ""ggggggg""],
            ""Structure"": [""......."", ""ffh...."", "".......""],
            ""Collision"": [""......o"", ""......."", ""#......""]}";
        private static RpgObject Obj(string json)
        {
            return JsonSerializer.Deserialize<RpgObject>(json, JsonOptionsProvider.ReadOptions);
        }

        private static RpgObject Lookup(string id)
        {
            return Objects.TryGetValue(id, out var value) ? value : null;
        }

        private static RpgWalkGrid Grid()
        {
            return new(RpgMapCells.Build(JsonSerializer.Deserialize<RpgMap>(Field, JsonOptionsProvider.ReadOptions), Lookup));
        }

        [ModTest("walk/terrain_and_collision")]
        static void Terrain(ModTestContext ctx)
        {
            var grid = Grid();
            ModAssert.True(grid.IsWalkable(0, 0), "grass");
            ModAssert.False(grid.IsWalkable(5, 0), "solid water");
            ModAssert.True(grid.IsWalkable(6, 0), "'o' opens water");
            ModAssert.False(grid.IsWalkable(6, 1), "water without override");
            ModAssert.False(grid.IsWalkable(0, 1), "a segment is solid by default");
            ModAssert.True(grid.IsWalkable(2, 1), "a segment declared walkable");
            ModAssert.False(grid.IsWalkable(0, 2), "'#' blocks grass");
            ModAssert.False(grid.IsWalkable(-1, 0), "outside: left");
            ModAssert.False(grid.IsWalkable(7, 0), "outside: right");
            ModAssert.False(grid.IsWalkable(0, 3), "outside: below");
        }

        [ModTest("walk/static_footprint_mask")]
        static void StaticMask(ModTestContext ctx)
        {
            var grid = Grid();
            var oak = Obj("{\"Id\": \"oak\", \"Form\": \"sprite\", \"Footprint\": {\"Size\": [2, 2]" +
                ", \"Solid\": [\"..\", \".#\"]}, \"Parts\": []}");
            grid.AddStatic(oak, 3, 1);
            ModAssert.True(grid.IsWalkable(3, 1) && grid.IsWalkable(4, 1) && grid.IsWalkable(3, 2), "canopy cells walkable");
            ModAssert.False(grid.IsWalkable(4, 2), "the trunk blocks");
            var rock = Obj(@"{""Id"": ""rock"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 1]}, ""Parts"": []}");
            grid.AddStatic(rock, 5, 0);
            ModAssert.True(grid.IsWalkable(6, 0), "'o' beats a static placement");
            grid.AddStatic(rock, 6, 2);
            ModAssert.False(grid.IsWalkable(6, 2), "a footprint without a mask blocks every cell");
            ModAssert.True(grid.IsWalkable(5, 2), "cells outside the footprint");
            var rug = Obj("{\"Id\": \"rug\", \"Form\": \"sprite\", \"Solid\": false, \"Footprint\":" +
                " {\"Size\": [2, 1]}, \"Parts\": []}");
            grid.AddStatic(rug, 1, 2);
            ModAssert.True(grid.IsWalkable(1, 2) && grid.IsWalkable(2, 2), "a non-solid sprite");
            grid.AddStatic(Obj(@"{""Id"": ""ghost"", ""Form"": ""sprite"", ""Hidden"": true, ""Parts"": []}"), 2, 2);
            ModAssert.True(grid.IsWalkable(2, 2), "a hidden object never blocks");
            grid.AddStatic(rock, -1, 2);
            ModAssert.False(grid.IsWalkable(0, 2), "partly outside: the inside cells count");
        }

        [ModTest("walk/dynamic_marks_follow_state_and_moves")]
        static void Dynamic(ModTestContext ctx)
        {
            var grid = Grid();
            var door = Obj("{\"Id\": \"door\", \"Form\": \"sprite\", \"Footprint\": {\"Size\": [2, 1" +
                "], \"Solid\": [\"##\"]}, \"Parts\": []}");
            grid.SetDynamic("field/door", door, 2, 0);
            ModAssert.False(grid.IsWalkable(2, 0) || grid.IsWalkable(3, 0), "a door of two cells blocks both");
            var open = Obj("{\"Id\": \"door\", \"Form\": \"sprite\", \"Footprint\": {\"Size\": [2, 1" +
                "], \"Solid\": [\"..\"]}, \"Parts\": []}");
            grid.SetDynamic("field/door", open, 2, 0);
            ModAssert.True(grid.IsWalkable(2, 0) && grid.IsWalkable(3, 0), "its open state replaces the marks");
            var npc = Obj(@"{""Id"": ""npc"", ""Form"": ""actor"", ""Charset"": ""c"", ""Footprint"": {""Size"": [3, 3]}}");
            grid.SetDynamic("field/npc", npc, 6, 0);
            ModAssert.True(grid.IsWalkable(6, 0), "map forced-open overrides a solid dynamic placement");
            ModAssert.True(grid.IsWalkable(6, 2), "an actor stands on one cell whatever its footprint");
            grid.SetDynamic("field/npc", npc, 0, 0);
            ModAssert.True(grid.IsWalkable(6, 0), "moving clears the old cell");
            ModAssert.False(grid.IsWalkable(0, 0), "and marks the new one");
            ModAssert.True(grid.IsWalkable(1, 0), "one cell only");
            grid.SetDynamic("field/npc2", npc, 0, 0);
            grid.RemoveDynamic("FIELD/NPC");
            ModAssert.False(grid.IsWalkable(0, 0), "another object on the same cell still blocks (keys case-insensitive)");
            grid.SetDynamic("field/npc2", Obj(@"{""Id"": ""npc"", ""Form"": ""actor"", ""Hidden"": true}"), 0, 0);
            ModAssert.True(grid.IsWalkable(0, 0), "a hidden state marks nothing");
            grid.RemoveDynamic("unknown");
            grid.SetDynamic(null, npc, 0, 0);
            ModAssert.True(grid.IsWalkable(0, 0), "no key: nothing marked");
            grid.SetDynamic("field/blocker", npc, 0, 2);
            ModAssert.False(grid.IsWalkable(0, 2), "on a '#' cell");
            grid.RemoveDynamic("field/blocker");
            ModAssert.False(grid.IsWalkable(0, 2), "removing a dynamic object never opens a blocked cell");
        }

        [ModTest("walk/explicit_collision_overrides_all_dynamic_marks")]
        static void MapPriority(ModTestContext ctx)
        {
            var map = new RpgMap
            {
                ID = "priority",
                Width = 3,
                Height = 1,
                Collision = new List<string>
                {
                    "o.#"
                }
            };
            var grid = new RpgWalkGrid(RpgMapCells.Build(map, Lookup));
            var door = Obj("{\"Id\": \"door\", \"Form\": \"sprite\", \"Footprint\": {\"Size\": [3, 1" +
                "], \"Solid\": [\"###\"]}, \"Parts\": []}");
            grid.SetDynamic("door", door, 0, 0);
            grid.SetDynamic("overlap", door, 0, 0);
            ModAssert.True(grid.IsWalkable(0, 0), "o overrides overlapping dynamic masks");
            ModAssert.False(grid.IsWalkable(1, 0), ". retains dynamic collision");
            ModAssert.False(grid.IsWalkable(2, 0), "# stays blocked");
            grid.RemoveDynamic("door");
            ModAssert.True(grid.IsWalkable(0, 0), "removing one mark does not change o");
            ModAssert.False(grid.IsWalkable(1, 0), "the remaining overlap still blocks auto");
            grid.RemoveDynamic("overlap");
            ModAssert.True(grid.IsWalkable(1, 0), "auto opens when all marks leave");
            ModAssert.False(grid.IsWalkable(2, 0), "removing objects cannot clear #");
            grid.SetDynamic("door", door, 0, 0);
            ModAssert.True(grid.IsWalkable(0, 0), "a later solid state cannot override o");
        }

        [ModTest("walk/object_blocks_at")]
        static void BlocksAt(ModTestContext ctx)
        {
            var oak = Obj("{\"Id\": \"oak\", \"Form\": \"sprite\", \"Footprint\": {\"Size\": [2, 2]" +
                ", \"Solid\": [\".#\"]}, \"Parts\": []}");
            ModAssert.Equal(2, oak.Width, "width");
            ModAssert.Equal(2, oak.Height, "height");
            ModAssert.True(oak.BlocksAt(1, 0), "'#'");
            ModAssert.False(oak.BlocksAt(0, 0), "'.'");
            ModAssert.False(oak.BlocksAt(1, 1), "a missing mask row is walkable");
            ModAssert.False(oak.BlocksAt(2, 0), "outside the footprint");
            ModAssert.False(oak.BlocksAt(-1, 0), "before the footprint");
            var crate = Obj(@"{""Id"": ""crate"", ""Form"": ""sprite"", ""Parts"": []}");
            ModAssert.True(crate.BlocksAt(0, 0), "default 1×1 solid sprite");
            ModAssert.False(crate.BlocksAt(0, 1), "1×1");
            var actor = Obj(@"{""Id"": ""a"", ""Form"": ""actor"", ""Footprint"": {""Size"": [2, 2], ""Solid"": [""..""]}}");
            ModAssert.Equal(1, actor.Width, "an actor is 1×1");
            ModAssert.True(actor.BlocksAt(0, 0), "an actor ignores footprint rows");
            ModAssert.False(Obj(@"{""Id"": ""s"", ""Form"": ""surface""}").BlocksAt(0, 0), "a surface is walkable by default");
        }
        [ModTest("walk/navigation_revision_and_noop_updates")]
        static void NavigationRevision(ModTestContext ctx)
        {
            var grid = Grid();
            var initial = grid.Revision;
            var actor = Obj(@"{""Id"":""revision_actor"",""Form"":""actor""}");
            grid.SetDynamic("owner", actor, 3, 0);
            ModAssert.True(grid.Revision > initial, "new occupancy advances the navigation revision");
            var marked = grid.Revision;
            grid.SetDynamic("OWNER", actor, 3, 0);
            ModAssert.True(grid.Revision == marked, "identical marks and case-insensitive identity are a no-op");
            ModAssert.False(grid.IsWalkable(3, 0), "no-op update retains the original occupied cell");
            grid.SetDynamic("owner", actor, 4, 0);
            ModAssert.True(grid.Revision > marked, "moving a mask invalidates a stale route");
            ModAssert.True(grid.IsWalkable(3, 0) && !grid.IsWalkable(4, 0), "moving replaces the exact cell mask");
            marked = grid.Revision;
            grid.RemoveDynamic("owner");
            ModAssert.True(grid.Revision > marked && grid.IsWalkable(4, 0), "removal opens the previous cell");
            marked = grid.Revision;
            grid.RemoveDynamic("owner");
            grid.SetDynamic(null, actor, 4, 0);
            grid.SetDynamic("empty", null, 4, 0);
            ModAssert.True(grid.Revision == marked, "absent and empty owners do not invalidate routes");
            grid.AddStatic(actor, 2, 2);
            ModAssert.True(grid.Revision > marked && !grid.IsWalkable(2, 2), "new static occupancy advances the stamp");
            marked = grid.Revision;
            grid.AddStatic(actor, 2, 2);
            grid.AddStatic(actor, 6, 0);
            ModAssert.True(grid.Revision == marked, "repeated or force-open-suppressed static marks change no walking answer");
        }

        [ModTest("walk/ignore_owner_preserves_overlap_overrides_and_revision")]
        static void IgnoreOwner(ModTestContext ctx)
        {
            var grid = Grid();
            var actor = Obj(@"{""Id"":""query_actor"",""Form"":""actor""}");
            grid.SetDynamic("first", actor, 3, 0);
            var marked = grid.Revision;
            ModAssert.True(grid.IsWalkableIgnoring("FIRST", 3, 0), "only the named owner's dynamic mask is subtracted");
            ModAssert.False(grid.IsWalkableIgnoring("other", 3, 0), "an unrelated owner cannot erase occupancy");
            ModAssert.False(grid.IsWalkableIgnoring(null, 3, 0), "null owner retains all masks");
            ModAssert.True(grid.Revision == marked && !grid.IsWalkable(3, 0), "queries never remove or restore real marks");
            grid.SetDynamic("second", actor, 3, 0);
            marked = grid.Revision;
            ModAssert.False(grid.IsWalkableIgnoring("first", 3, 0), "the other overlapping owner still blocks entry");
            ModAssert.False(grid.IsWalkableIgnoring("second", 3, 0), "subtraction is symmetric for overlapping owners");
            ModAssert.False(grid.IsWalkableIgnoring("first", 0, 2), "an ignored dynamic owner cannot bypass explicit block");
            ModAssert.False(grid.IsWalkableIgnoring("first", -1, 0), "an ignored dynamic owner cannot bypass map bounds");
            ModAssert.True(grid.Revision == marked, "all overlap and boundary queries leave the stamp unchanged");
            grid.SetDynamic("first", actor, 6, 0);
            grid.SetDynamic("second", actor, 6, 0);
            marked = grid.Revision;
            ModAssert.True(grid.IsWalkable(6, 0) && grid.IsWalkableIgnoring("first", 6, 0),
                "intentional force-open beats every overlapping mask in both queries");
            ModAssert.True(grid.Revision == marked, "force-open queries also preserve the stamp");
            actor.Hidden = true;
            grid.SetDynamic("first", actor, 6, 0);
            ModAssert.True(grid.Revision > marked, "hiding removes the owner's recorded mask");
        }

    }
}
