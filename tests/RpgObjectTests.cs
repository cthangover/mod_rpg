namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.TileRender;

    /// <summary>
    /// The v2 object DTO reads the contract's JSON, survives a serialise/read round trip without
    /// losing written-as-null state components or variants, and inherits through the core
    /// <c>Extends</c> resolution (the first stage of an object's resolution).
    /// </summary>
    public class RpgObjectTests
    {
        private const string Barrel = @"{
            ""Id"": ""village_pack/container/barrel"", ""Form"": ""sprite"", ""Name"": ""village_pack/object/barrel/name"", ""Category"": ""container"",
            ""Footprint"": {""Size"": [1, 1], ""Solid"": [""#""]}, ""Anchor"": [0.5, 1], ""DrawOrder"": ""AboveCharacters"",
            ""Parts"": [{""Id"": ""body"", ""Image"": ""container/barrel_closed"", ""Crop"": [0, 0, 32, 48], ""At"": [0, -0.5], ""Size"": [1, 1.5],
                       ""Geometry"": ""upright"", ""SortOffset"": [0, 0.25], ""DrawOrder"": ""ByPosition""},
                      {""Id"": ""roof"", ""Image"": ""container/lid"", ""Geometry"": ""Quad"", ""Only"": ""isometric"",
                       ""Points"": [[0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]]},
                      {""Id"": ""side"", ""Image"": ""container/side"", ""Geometry"": ""plane"", ""Along"": ""y""}],
            ""Interact"": {""Trigger"": ""Use"", ""Commands"": [""flag self.open 1"", ""ptext \""You open the barrel.\""""]},
            ""Light"": {""Radius"": 2, ""Color"": ""#ffaa00"", ""At"": [0.5, 0.5]},
            ""Lights"": [{""Radius"": 1.5}],
            ""Route"": {""Steps"": [""wait""], ""Every"": 2},
            ""Variants"": {""winter"": {""Parts$add"": [{""Id"": ""body"", ""Image"": ""container/barrel_snow""}], ""Light"": null}},
            ""States"": [{""Id"": ""open"", ""When"": ""self.open == 1"", ""Interact"": {""Commands"": [""ptext \""Empty.\""""]}, ""Light"": null}]
        }";

        private static RpgObject Read(string json) => JsonSerializer.Deserialize<RpgObject>(json, JsonOptionsProvider.ReadOptions);

        [ModTest("model/object_reads_contract_json")]
        static void ReadsContractJson(ModTestContext ctx)
        {
            var barrel = Read(Barrel);
            ModAssert.Equal("village_pack/container/barrel", barrel.ID, "id");
            ModAssert.True(barrel.Form == RpgObjectForm.Sprite, "lower-case form word");
            ModAssert.True(barrel.DrawOrder == TileDrawOrder.AboveCharacters, "object draw order");
            ModAssert.Equal(3, barrel.Parts.Count, "parts");
            ModAssert.True(barrel.Parts[0].Geometry == RpgPartGeometry.Upright, "upright");
            ModAssert.True(barrel.Parts[0].DrawOrder == TileDrawOrder.ByPosition, "part draw order");
            ModAssert.Equal(48, barrel.Parts[0].Crop[3], "crop in pixels");
            ModAssert.Equal(-0.5f, barrel.Parts[0].At[1], 1e-6f, "negative offset in cells");
            ModAssert.True(barrel.Parts[1].Geometry == RpgPartGeometry.Quad, "capitalised geometry word reads too");
            ModAssert.Equal(4, barrel.Parts[1].Points.Count, "quad corners");
            ModAssert.True(barrel.Parts[1].Along == RpgAlong.X, "part plane default x");
            ModAssert.Null(barrel.Parts[1].DrawOrder, "part without draw order");
            ModAssert.True(barrel.Parts[2].Along == RpgAlong.Y, "plane along y");
            ModAssert.True(barrel.Interact.Trigger == RpgInteractTrigger.Use, "trigger");
            ModAssert.Equal(2, barrel.Interact.Commands.Count, "commands");
            ModAssert.Equal(0.5f, barrel.Light.At[0], 1e-6f, "light position");
            ModAssert.Equal(1.5f, barrel.Lights[0].Radius, 1e-6f, "further light");
            ModAssert.Equal("#ffd8a0", barrel.Lights[0].Color, "light colour default");
            ModAssert.CloseTo(2, barrel.Route.Every, 1e-9, "route");
            ModAssert.True(barrel.IsSolid, "a sprite is solid by default");
            ModAssert.True(barrel.Facing == RpgDirection.Down, "facing default");
            ModAssert.Equal(1, barrel.Column, "column default");
            ModAssert.Equal(1, barrel.Footprint.Width, "footprint width");
            ModAssert.True(barrel.States[0].Components.ContainsKey("Interact"), "state component kept as written");
            ModAssert.True(barrel.States[0].Components["Light"].ValueKind == JsonValueKind.Null, "state null kept as written");
        }

        [ModTest("model/object_json_round_trip")]
        static void RoundTrip(ModTestContext ctx)
        {
            var original = Read(Barrel);
            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedOptions);
            ModAssert.True(json.Contains("\"sprite\""), "form written as the contract word");
            ModAssert.True(json.Contains("\"upright\"") && json.Contains("\"quad\"") && json.Contains("\"y\""), "geometry and axis words");
            ModAssert.True(json.Contains("\"AboveCharacters\"") && json.Contains("\"Use\""), "draw order and trigger words");

            var restored = Read(json);
            ModAssert.Equal(original.ID, restored.ID, "id");
            ModAssert.Equal(original.Name, restored.Name, "name");
            ModAssert.Equal(original.Category, restored.Category, "category");
            ModAssert.True(restored.Form == RpgObjectForm.Sprite, "form");
            ModAssert.True(restored.DrawOrder == TileDrawOrder.AboveCharacters, "draw order");
            ModAssert.Equal(original.Footprint.Solid[0], restored.Footprint.Solid[0], "footprint mask");
            ModAssert.Equal(1f, restored.Anchor[1], 1e-6f, "anchor");
            ModAssert.Equal(3, restored.Parts.Count, "parts");
            ModAssert.Equal("body", restored.Parts[0].Id, "part id");
            ModAssert.Equal(32, restored.Parts[0].Crop[2], "crop");
            ModAssert.Equal(1.5f, restored.Parts[0].Size[1], 1e-6f, "size");
            ModAssert.Equal(0.25f, restored.Parts[0].SortOffset[1], 1e-6f, "sort offset");
            ModAssert.True(restored.Parts[0].DrawOrder == TileDrawOrder.ByPosition, "part draw order");
            ModAssert.Null(restored.Parts[1].DrawOrder, "part draw order stays unset");
            ModAssert.Equal("isometric", restored.Parts[1].Only, "only");
            ModAssert.Equal(1f, restored.Parts[1].Points[2][2], 1e-6f, "quad z");
            ModAssert.True(restored.Parts[2].Geometry == RpgPartGeometry.Plane && restored.Parts[2].Along == RpgAlong.Y, "plane");
            ModAssert.Equal(original.Interact.Commands[1], restored.Interact.Commands[1], "commands");
            ModAssert.Equal("#ffaa00", restored.Light.Color, "light");
            ModAssert.Equal(1, restored.Lights.Count, "lights");
            ModAssert.Equal("wait", restored.Route.Steps[0], "route");
            ModAssert.True(restored.States[0].Components["Light"].ValueKind == JsonValueKind.Null, "state null survives");
            ModAssert.Equal("self.open == 1", restored.States[0].When, "state condition");
            ModAssert.Equal("open", restored.States[0].Id, "state id");
            var winter = restored.Variants["winter"];
            ModAssert.True(winter.GetProperty("Light").ValueKind == JsonValueKind.Null, "variant null survives");
            ModAssert.True(winter.TryGetProperty("Parts$add", out _), "variant operation survives");
        }

        [ModTest("model/brush_forms_read")]
        static void BrushForms(ModTestContext ctx)
        {
            var grass = Read(@"{""Id"": ""village_pack/ground/grass"", ""Form"": ""Surface"", ""Surface"": {""Image"": ""ground/grass""}}");
            ModAssert.True(grass.Form == RpgObjectForm.Surface, "capitalised form reads");
            ModAssert.False(grass.IsSolid, "a surface is walkable by default");
            ModAssert.True(grass.Surface.Diffuse, "diffuse default");
            ModAssert.Equal(2, grass.Surface.DiffuseStrength, "diffuse strength default");

            var water = Read(@"{""Id"": ""w"", ""Form"": ""surface"", ""Solid"": true, ""Water"": true, ""Surface"": {""Image"": ""ground/water"", ""Diffuse"": false}}");
            ModAssert.True(water.IsSolid && water.Water, "solid water");
            ModAssert.False(water.Surface.Diffuse, "crisp material");

            var fence = Read(@"{""Id"": ""village_pack/structure/fence"", ""Form"": ""segment"", ""Segment"": {""Image"": ""structure/fence"", ""Height"": 1.5}}");
            ModAssert.True(fence.IsSolid, "a segment is solid by default");
            ModAssert.True(fence.Segment.Along == RpgAlong.Auto, "segment direction default");
            ModAssert.Equal("village_pack/structure/fence", fence.Segment.ConnectKey(fence.ID), "connect key defaults to the object id");
            ModAssert.Equal(1.5f, fence.Segment.Height, 1e-6f, "height");

            var hedge = Read(@"{""Id"": ""h"", ""Form"": ""segment"", ""Solid"": false, ""Segment"": {""Image"": ""structure/hedge"", ""Connect"": ""fence"", ""Along"": ""x""}}");
            ModAssert.Equal("fence", hedge.Segment.ConnectKey(hedge.ID), "explicit connect key");
            ModAssert.False(hedge.IsSolid, "explicitly walkable segment");

            var yeti = Read(@"{""Id"": ""y"", ""Form"": ""actor"", ""Charset"": ""snow/yeti"", ""Facing"": ""left"", ""Column"": 0}");
            ModAssert.True(yeti.Form == RpgObjectForm.Actor && yeti.Facing == RpgDirection.Left, "actor facing");
            ModAssert.Equal(0, yeti.Column, "column");
            ModAssert.Null(Read(@"{""Id"": ""n""}").Form, "missing form stays unset for the validator");
            ModAssert.Equal(1, new RpgFootprint { Size = null }.Height, "malformed footprint size is 1×1");
        }

        [ModTest("model/form_rejects_integers")]
        static void FormRejectsIntegers(ModTestContext ctx)
        {
            ModAssert.Throws(ReadIntegerForm, "an index must not pick a form", anyException: true);
        }

        private static void ReadIntegerForm() => Read(@"{""Id"": ""x"", ""Form"": 2}");

        /// <summary>
        /// Stage 1 of the resolution order: the core's generic <c>Extends</c> resolution on the v2
        /// DTO — the child's written JSON deep-merges over the fully resolved parent, so
        /// <c>Commands$add</c> appends after the parent's text, <c>null</c> drops the parent's light,
        /// <c>Parts$add</c> merges by part id (the <c>[MergeKey]</c> on <see cref="RpgObject.Parts"/>).
        /// </summary>
        [ModTest("model/extends_through_core_resolution")]
        static void ExtendsThroughCore(ModTestContext ctx)
        {
            const string supply = @"{""Id"": ""rpg_plus/supply_barrel"", ""Extends"": ""village_pack/container/barrel"",
                ""Interact"": {""Commands$add"": [""action item.add id=money/coin count=2""]}, ""Light"": null,
                ""Parts$add"": [{""Id"": ""body"", ""Image"": ""container/supply_barrel""}, {""Id"": ""flag"", ""Image"": ""container/flag""}]}";
            const string grand = @"{""Id"": ""rpg_plus/big_supply"", ""Extends"": ""rpg_plus/supply_barrel"", ""Footprint"": {""Size"": [2, 1]}}";
            var items = new Dictionary<string, RpgObject>
            {
                ["village_pack/container/barrel"] = Read(Barrel),
                ["rpg_plus/supply_barrel"] = Read(supply),
                ["rpg_plus/big_supply"] = Read(grand),
            };
            var written = new Dictionary<string, JsonObject>
            {
                ["rpg_plus/supply_barrel"] = (JsonObject)JsonNode.Parse(supply),
                ["rpg_plus/big_supply"] = (JsonObject)JsonNode.Parse(grand),
            };

            var resolved = new EntityExtendsResolution<RpgObject>("rpg_object", items, written, null).ResolveAll();
            var child = resolved["rpg_plus/supply_barrel"];
            ModAssert.Equal("rpg_plus/supply_barrel", child.ID, "own id");
            ModAssert.True(child.Form == RpgObjectForm.Sprite, "form inherited");
            ModAssert.Equal("village_pack/object/barrel/name", child.Name, "name inherited");
            ModAssert.Equal(3, child.Interact.Commands.Count, "Commands$add appends");
            ModAssert.Equal("action item.add id=money/coin count=2", child.Interact.Commands[2], "after the parent's text");
            ModAssert.Null(child.Light, "null removes the parent's light");
            ModAssert.Equal(1, child.Lights.Count, "further lights inherited");
            ModAssert.Equal(4, child.Parts.Count, "a new part appended, an existing one merged");
            ModAssert.Equal("container/supply_barrel", child.Parts[0].Image, "part merged by id");
            ModAssert.Equal(48, child.Parts[0].Crop[3], "merged part keeps the parent's crop");
            ModAssert.Equal(1, child.States.Count, "states inherited");
            ModAssert.True(child.Variants.ContainsKey("winter"), "variants inherited");

            var grandChild = resolved["rpg_plus/big_supply"];
            ModAssert.Equal(2, grandChild.Footprint.Width, "own footprint size");
            ModAssert.Equal("#", grandChild.Footprint.Solid[0], "footprint mask merged in from the grandparent");
            ModAssert.Equal(3, grandChild.Interact.Commands.Count, "the parent is resolved completely first");
        }

        [ModTest("model/extends_cycle_skips_members")]
        static void ExtendsCycle(ModTestContext ctx)
        {
            const string a = @"{""Id"": ""a"", ""Form"": ""sprite"", ""Extends"": ""b"", ""Parts"": []}";
            const string b = @"{""Id"": ""b"", ""Form"": ""sprite"", ""Extends"": ""a"", ""Parts"": []}";
            var items = new Dictionary<string, RpgObject> { ["a"] = Read(a), ["b"] = Read(b), ["c"] = Read(@"{""Id"": ""c"", ""Form"": ""actor""}") };
            var written = new Dictionary<string, JsonObject> { ["a"] = (JsonObject)JsonNode.Parse(a), ["b"] = (JsonObject)JsonNode.Parse(b) };
            var resolved = new EntityExtendsResolution<RpgObject>("rpg_object", items, written, null).ResolveAll();
            ModAssert.False(resolved.ContainsKey("a") || resolved.ContainsKey("b"), "cycle members skipped");
            ModAssert.True(resolved.ContainsKey("c"), "others kept");
        }
    }
}
