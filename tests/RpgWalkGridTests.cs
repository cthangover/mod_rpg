namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Walkability of a v2 map: brush objects (solid water, fences), footprint masks of static and
    /// dynamic placements, the collision overrides, and their order — a solid dynamic placement
    /// blocks even on a forced-open cell, a forced-open cell beats terrain and static objects.
    /// </summary>
    public class RpgWalkGridTests
    {
        private static readonly Dictionary<string, RpgObject> Objects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["grass"] = Obj(@"{""Id"": ""grass"", ""Form"": ""surface"", ""Surface"": {""Image"": ""g""}}"),
            ["water"] = Obj(@"{""Id"": ""water"", ""Form"": ""surface"", ""Solid"": true, ""Water"": true, ""Surface"": {""Image"": ""w""}}"),
            ["fence"] = Obj(@"{""Id"": ""fence"", ""Form"": ""segment"", ""Segment"": {""Image"": ""f""}}"),
            ["hedge_gap"] = Obj(@"{""Id"": ""hedge_gap"", ""Form"": ""segment"", ""Solid"": false, ""Segment"": {""Image"": ""h""}}"),
        };

        // Ground: water at (5,0), (6,0), (6,1); Structure: fences at (0,1), (1,1), a walkable hedge at (2,1);
        // Collision: 'o' at (6,0), '#' at (0,2).
        private const string Field = @"{""Id"": ""field"", ""Width"": 7, ""Height"": 3,
            ""Legend"": {""g"": ""grass"", ""w"": ""water"", ""f"": ""fence"", ""h"": ""hedge_gap""},
            ""Ground"": [""gggggww"", ""ggggggw"", ""ggggggg""],
            ""Structure"": [""......."", ""ffh...."", "".......""],
            ""Collision"": [""......o"", ""......."", ""#......""]}";

        private static RpgObject Obj(string json) => JsonSerializer.Deserialize<RpgObject>(json, JsonOptionsProvider.ReadOptions);

        private static RpgObject Lookup(string id) => Objects.TryGetValue(id, out var value) ? value : null;

        private static RpgWalkGrid Grid()
            => new(RpgMapCells.Build(JsonSerializer.Deserialize<RpgMap>(Field, JsonOptionsProvider.ReadOptions), Lookup));

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
            var oak = Obj(@"{""Id"": ""oak"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 2], ""Solid"": ["".."", "".#""]}, ""Parts"": []}");
            grid.AddStatic(oak, 3, 1);
            ModAssert.True(grid.IsWalkable(3, 1) && grid.IsWalkable(4, 1) && grid.IsWalkable(3, 2), "canopy cells walkable");
            ModAssert.False(grid.IsWalkable(4, 2), "the trunk blocks");

            var rock = Obj(@"{""Id"": ""rock"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 1]}, ""Parts"": []}");
            grid.AddStatic(rock, 5, 0);
            ModAssert.True(grid.IsWalkable(6, 0), "'o' beats a static placement");
            grid.AddStatic(rock, 6, 2);
            ModAssert.False(grid.IsWalkable(6, 2), "a footprint without a mask blocks every cell");
            ModAssert.True(grid.IsWalkable(5, 2), "cells outside the footprint");

            var rug = Obj(@"{""Id"": ""rug"", ""Form"": ""sprite"", ""Solid"": false, ""Footprint"": {""Size"": [2, 1]}, ""Parts"": []}");
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
            var door = Obj(@"{""Id"": ""door"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 1], ""Solid"": [""##""]}, ""Parts"": []}");
            grid.SetDynamic("field/door", door, 2, 0);
            ModAssert.False(grid.IsWalkable(2, 0) || grid.IsWalkable(3, 0), "a door of two cells blocks both");

            var open = Obj(@"{""Id"": ""door"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 1], ""Solid"": [""..""]}, ""Parts"": []}");
            grid.SetDynamic("field/door", open, 2, 0);
            ModAssert.True(grid.IsWalkable(2, 0) && grid.IsWalkable(3, 0), "its open state replaces the marks");

            var npc = Obj(@"{""Id"": ""npc"", ""Form"": ""actor"", ""Charset"": ""c"", ""Footprint"": {""Size"": [3, 3]}}");
            grid.SetDynamic("field/npc", npc, 6, 0);
            ModAssert.False(grid.IsWalkable(6, 0), "a solid dynamic placement blocks even a forced-open cell");
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

        [ModTest("walk/object_blocks_at")]
        static void BlocksAt(ModTestContext ctx)
        {
            var oak = Obj(@"{""Id"": ""oak"", ""Form"": ""sprite"", ""Footprint"": {""Size"": [2, 2], ""Solid"": ["".#""]}, ""Parts"": []}");
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
    }
}
