namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;

    /// <summary>
    /// A v2 map's grids: legend characters to effective objects (map variant applied), rows, then
    /// strokes in order — including strokes another mod adds by patch — and the load errors of a
    /// hand-written map (wrong form in a grid, unknown character, bad shape) that leave cells empty.
    /// </summary>
    public class RpgMapCellsTests
    {
        private static readonly Dictionary<string, RpgObject> Objects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["vp/ground/grass"] = Obj(@"{""Id"": ""vp/ground/grass"", ""Form"": ""surface"", ""Surface"": {""Image"": ""ground/grass""},
                ""Variants"": {""winter"": {""Surface"": {""Image"": ""ground/snow""}}}}"),
            ["vp/ground/path"] = Obj(@"{""Id"": ""vp/ground/path"", ""Form"": ""surface"", ""Surface"": {""Image"": ""ground/path""}}"),
            ["vp/ground/water"] = Obj(@"{""Id"": ""vp/ground/water"", ""Form"": ""surface"", ""Solid"": true, ""Water"": true, ""Surface"": {""Image"": ""ground/water""}}"),
            ["vp/structure/fence"] = Obj(@"{""Id"": ""vp/structure/fence"", ""Form"": ""segment"", ""Segment"": {""Image"": ""structure/fence""}}"),
            ["vp/vegetation/oak"] = Obj(@"{""Id"": ""vp/vegetation/oak"", ""Form"": ""sprite"", ""Parts"": [{""Image"": ""vegetation/oak""}]}"),
        };

        private const string Village = @"{""Id"": ""village"", ""Width"": 4, ""Height"": 3, ""Variant"": ""winter"",
            ""Legend"": {""g"": ""vp/ground/grass"", ""p"": ""vp/ground/path"", ""w"": ""vp/ground/water"", ""f"": ""vp/structure/fence""},
            ""Ground"": [""ggpp"", ""gwpp"", ""gg.p""],
            ""Structure"": [""ff.."", ""...."", ""....""],
            ""Collision"": [""...."", ""..#o"", ""....""],
            ""Strokes"": [{""Id"": ""bridge"", ""Layer"": ""Ground"", ""Object"": ""vp/ground/path"", ""Cells"": [[1, 1]]}]}";

        private static RpgObject Obj(string json) => JsonSerializer.Deserialize<RpgObject>(json, JsonOptionsProvider.ReadOptions);

        private static RpgMap Map(string json) => JsonSerializer.Deserialize<RpgMap>(json, JsonOptionsProvider.ReadOptions);

        private static RpgObject Lookup(string id) => Objects.TryGetValue(id, out var value) ? value : null;

        private static RpgMapCells Build(string json) => RpgMapCells.Build(Map(json), Lookup);

        [ModTest("map/rows_strokes_and_variant")]
        static void RowsStrokesVariant(ModTestContext ctx)
        {
            var cells = Build(Village);
            ModAssert.Equal(0, cells.Errors.Count, "a clean map has no errors");
            ModAssert.Equal(4, cells.Width, "width");
            ModAssert.Equal("vp/ground/grass", cells.Ground(0, 0).ID, "legend character");
            ModAssert.Equal("ground/snow", cells.Ground(0, 0).Surface.Image, "the map's variant applied to brushes");
            ModAssert.Equal("ground/grass", Objects["vp/ground/grass"].Surface.Image, "the object itself unchanged");
            ModAssert.True(ReferenceEquals(cells.Ground(0, 0), cells.Ground(1, 0)), "one effective object per id");
            ModAssert.Null(cells.Ground(2, 2), "'.' is empty");
            ModAssert.Equal("vp/ground/path", cells.Ground(1, 1).ID, "the stroke paints over the water row");
            ModAssert.Equal("vp/structure/fence", cells.Structure(1, 0).ID, "structure row");
            ModAssert.Null(cells.Structure(2, 0), "no segment");
            ModAssert.True(cells.Collision(2, 1) == '#' && cells.Collision(3, 1) == 'o' && cells.Collision(0, 0) == '.', "collision codes");
            ModAssert.True(cells.Collision(-1, 0) == '#', "outside is blocked");
            ModAssert.Null(cells.Ground(4, 0), "outside has no ground");
        }

        [ModTest("map/strokes_add_from_patch")]
        static void StrokesAddFromPatch(ModTestContext ctx)
        {
            var node = JsonPatchMerger.ParseObject(Village);
            JsonPatchMerger.Apply(node, JsonPatchMerger.ParseObject(@"{""Strokes$add"": [
                {""Id"": ""BRIDGE"", ""Object"": ""vp/ground/grass""},
                {""Id"": ""mymod_pond"", ""Layer"": ""Ground"", ""Object"": ""vp/ground/water"", ""Rects"": [[2, 0, 2, 1]]},
                {""Id"": ""mymod_gate"", ""Layer"": ""Structure"", ""Object"": null, ""Cells"": [[0, 0]]},
                {""Id"": ""mymod_wall"", ""Layer"": ""Collision"", ""Code"": ""#"", ""Rects"": [[3, 2, 5, 5]]}]}"), typeof(RpgMap));
            var map = node.Deserialize<RpgMap>(JsonOptionsProvider.ReadOptions);
            ModAssert.Equal(4, map.Strokes.Count, "Strokes$add merges by id (case-insensitive) and appends new ones");

            var cells = RpgMapCells.Build(map, Lookup);
            ModAssert.Equal(0, cells.Errors.Count, "no errors");
            ModAssert.Equal("vp/ground/grass", cells.Ground(1, 1).ID, "the patched stroke paints its new object");
            ModAssert.Equal("vp/ground/water", cells.Ground(2, 0).ID, "a rect of a new stroke");
            ModAssert.Equal("vp/ground/water", cells.Ground(3, 0).ID, "the whole rect");
            ModAssert.Null(cells.Structure(0, 0), "a null object clears");
            ModAssert.Equal("vp/structure/fence", cells.Structure(1, 0).ID, "cells outside the stroke untouched");
            ModAssert.True(cells.Collision(3, 2) == '#', "collision stroke, clipped to the map");
        }

        [ModTest("map/wrong_form_in_grid_is_an_error")]
        static void WrongForm(ModTestContext ctx)
        {
            var cells = Build(@"{""Id"": ""bad"", ""Width"": 2, ""Height"": 1,
                ""Legend"": {""o"": ""vp/vegetation/oak"", ""f"": ""vp/structure/fence"", ""g"": ""vp/ground/grass""},
                ""Ground"": [""of""], ""Structure"": [""go""]}");
            ModAssert.Null(cells.Ground(0, 0), "a sprite in Ground leaves the cell empty");
            ModAssert.Null(cells.Ground(1, 0), "a segment in Ground leaves the cell empty");
            ModAssert.Null(cells.Structure(0, 0), "a surface in Structure leaves the cell empty");
            ModAssert.Equal(4, cells.Errors.Count, "one error per grid and character");
            ModAssert.True(cells.Errors[0].Contains("'vp/vegetation/oak', a sprite") && cells.Errors[0].Contains("only surface"), "names object, form and grid: " + cells.Errors[0]);
            ModAssert.True(cells.Errors[3].StartsWith("Structure") && cells.Errors[3].Contains("only segment"), "structure error: " + cells.Errors[3]);

            var stroke = Build(@"{""Id"": ""bad2"", ""Width"": 1, ""Height"": 1, ""Legend"": {""g"": ""vp/ground/grass""}, ""Ground"": [""g""],
                ""Strokes"": [{""Id"": ""s"", ""Layer"": ""Ground"", ""Object"": ""vp/structure/fence"", ""Cells"": [[0, 0]]}]}");
            ModAssert.Equal("vp/ground/grass", stroke.Ground(0, 0).ID, "a stroke with the wrong form is skipped");
            ModAssert.True(stroke.Errors.Count == 1 && stroke.Errors[0].StartsWith("Strokes[s]"), "stroke error names the stroke");
        }

        [ModTest("map/hand_written_errors_are_forgiving")]
        static void Forgiving(ModTestContext ctx)
        {
            var cells = Build(@"{""Id"": ""rough"", ""Width"": 3, ""Height"": 2,
                ""Legend"": {""g"": ""vp/ground/grass"", ""x"": ""vp/ground/nothing"", "".."": ""vp/ground/path"", ""."": ""vp/ground/path""},
                ""Ground"": [""gzz""], ""Collision"": [""..?""],
                ""Strokes"": [{""Layer"": ""Collision"", ""Code"": ""X""}, {""Id"": ""r"", ""Object"": ""vp/ground/path"", ""Rects"": [[0, 0, 1]], ""Cells"": [[2, 1]]}]}");
            ModAssert.Equal("vp/ground/grass", cells.Ground(0, 0).ID, "good cells still read");
            ModAssert.Null(cells.Ground(1, 0), "unknown character: empty");
            ModAssert.Equal("vp/ground/path", cells.Ground(2, 1).ID, "a short Ground leaves rows for strokes");
            ModAssert.True(cells.Collision(2, 0) == '.', "an unknown collision code reads as derived");
            var expected = new[]
            {
                "Legend: 'x' → unknown object", "Legend: '..'", "Legend: '.'",
                "Ground: 1 rows, the map has 2", "Ground: 'z' (first at 1,0) is not in the Legend",
                "Collision: 1 rows", "Collision: '?'", "Strokes[0]: Collision code 'X'", "Strokes[r]: a rect is not",
            };
            ModAssert.Equal(expected.Length, cells.Errors.Count, "errors: " + string.Join(" | ", cells.Errors));
            for (var i = 0; i < expected.Length; i++)
                ModAssert.True(cells.Errors[i].StartsWith(expected[i]), $"error {i}: '{cells.Errors[i]}' starts with '{expected[i]}'");

            var empty = RpgMapCells.Build(Map(@"{""Id"": ""e"", ""Width"": 2, ""Height"": 2}"), Lookup);
            ModAssert.Equal(0, empty.Errors.Count, "missing grids are empty, not errors");
            ModAssert.Null(empty.Ground(1, 1), "empty");
            ModAssert.Equal(1, RpgMapCells.Build(null, Lookup).Width, "no map: a 1×1 empty grid");
        }

        [ModTest("map/json_round_trip")]
        static void RoundTrip(ModTestContext ctx)
        {
            var original = Map(Village);
            var restored = Map(JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedOptions));
            ModAssert.Equal("village", restored.ID, "id");
            ModAssert.Equal(4, restored.Width, "width");
            ModAssert.Equal(3, restored.Height, "height");
            ModAssert.Equal(32, restored.TileSize, "tile size default");
            ModAssert.Equal(128, restored.PatchSize, "patch size default");
            ModAssert.Equal(2f, restored.Zoom, 1e-6f, "zoom default");
            ModAssert.Equal("vp/structure/fence", restored.Legend["f"], "legend");
            ModAssert.Equal("gwpp", restored.Ground[1], "ground");
            ModAssert.Equal("ff..", restored.Structure[0], "structure");
            ModAssert.Equal("..#o", restored.Collision[1], "collision");
            ModAssert.Equal("bridge", restored.Strokes[0].Id, "stroke id");
            ModAssert.True(restored.Strokes[0].Layer == RpgStrokeLayer.Ground, "stroke layer");
            ModAssert.Equal(1, restored.Strokes[0].Cells[0][1], "stroke cell");
            ModAssert.Equal("winter", restored.Variant, "variant");
            ModAssert.Equal("cycle", restored.LightMode, "light mode default");
            ModAssert.True(JsonSerializer.Serialize(original).Contains("\"Layer\":\"Ground\""), "layer written as a word");
        }
    }
}
