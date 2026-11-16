using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Mods.Testing;
using Cthangover.Mods.Rpg.Validation;
using Godot;

namespace Cthangover.Mods.Rpg.Tests
{
    /// <summary>Authored shape, pixels and world units remain distinct; diagnostics never rewrite their source JSON.</summary>
    public sealed class RpgDataCheckTests
    {
        private const string Sprite = "{\"Id\":\"check/prop\",\"Form\":\"sprite\",\"Anchor\":[-1,0.5],"
            + "\"Parts\":[{\"Image\":\"prop\",\"At\":[-2,0.25],\"Size\":[1,2],\"SortOffset\":[-3,2],"
            + "\"Crop\":[0,0,128,128]}],\"Unknown\":{\"kept\":42}}";

        [ModTest("data_validation/origins_crop_edges_and_unknown_fields")]
        static void Valid(ModTestContext ctx)
        {
            var data = JsonNode.Parse(Sprite).AsObject();
            var before = data.ToJsonString();
            ModAssert.Equal(0, RpgDataCheck.Object(data, "isometric", 128, Size).Count,
                "negative/fractional offsets and exact crop boundary are valid");
            ModAssert.Equal(before, data.ToJsonString(), "validation leaves all authored bytes and unknown metadata unchanged");
            data["Parts"] = new JsonArray();
            ModAssert.Equal(0, RpgDataCheck.Object(data, "topdown", 128, Size).Count, "invisible triggers need no picture");
            data["Form"] = "actor";
            data["Hidden"] = true;
            ModAssert.Equal(0, RpgDataCheck.Object(data, "topdown", 128, Size).Count,
                "a hidden state may remove its walking sheet");
        }

        [ModTest("data_validation/exact_field_paths_and_independent_parts")]
        static void Fields(ModTestContext ctx)
        {
            var data = JsonNode.Parse(Sprite).AsObject();
            data["Anchor"] = new JsonArray(1, 2, 3);
            data["Parts"][0]["Crop"] = new JsonArray(127, 0, 2, 128);
            data["Parts"][0]["Size"] = new JsonArray(0, 1);
            var before = data.ToJsonString();
            var issues = RpgDataCheck.Object(data, "isometric", 128, Size);
            ModAssert.True(Contains(issues, "vector", "Anchor"), "origin shape is named exactly");
            ModAssert.True(Contains(issues, "crop", "Parts[0].Crop"), "overflow is checked in pixels");
            ModAssert.True(Contains(issues, "size", "Parts[0].Size"), "zero world size is named exactly");
            ModAssert.Equal(3, issues.Count, "one invalid part does not manufacture errors for unrelated fields");
            ModAssert.Equal(before, data.ToJsonString(), "failed validation does not normalize input");
        }

        [ModTest("data_validation/material_missing_picture_and_projection_scope")]
        static void Images(ModTestContext ctx)
        {
            var surface = JsonNode.Parse("{\"Form\":\"surface\",\"Surface\":{\"Image\":\"prop\"}}").AsObject();
            ModAssert.Equal(0, RpgDataCheck.Object(surface, "topdown", 128, Size).Count, "matching square material");
            ModAssert.Equal(0, RpgDataCheck.Object(surface, "topdown", 0, Size).Count,
                "an object without a map context accepts a square without inventing its PatchSize");
            ModAssert.True(Contains(RpgDataCheck.Object(surface, "isometric", 512, Size), "material", "Surface.Image"),
                "material follows the map patch size rather than tile size");
            var data = JsonNode.Parse(Sprite).AsObject();
            data["Parts"][0]["Image"] = "missing";
            ModAssert.True(Contains(RpgDataCheck.Object(data, "isometric", 128, Size), "image", "Parts[0].Image"),
                "missing image is report data");
            data["Parts"][0]["Only"] = "isometric";
            ModAssert.Equal(0, RpgDataCheck.Object(data, "topdown", 128, Size).Count,
                "the other projection does not request an unused picture");
            data["Parts"][0]["Only"] = "extra_projection";
            ModAssert.True(Contains(RpgDataCheck.Object(data, "topdown", 128, Size), "projection", "Parts[0].Only"),
                "an additional provider is an explicit advisory finding");
        }

        [ModTest("data_validation/authored_partial_fields_precede_dto_normalization")]
        static void Authored(ModTestContext ctx)
        {
            var data = JsonNode.Parse("{\"Scale\":101,\"Variants\":{\"winter\":{\"Anchor\":[0]}},"
                + "\"Parts$add\":[{\"Id\":\"wall\",\"SortOffset\":[-2,0.5]}],"
                + "\"States\":[{\"Id\":\"open\",\"Parts\":[{\"Id\":\"wall\",\"Size\":[0,1]}]}]}").AsObject();
            var before = data.ToJsonString();
            var issues = RpgDataCheck.Authored(data, "isometric", 128, Size);
            ModAssert.Equal(3, issues.Count, "only present invalid fields are checked before normalization");
            ModAssert.True(Contains(issues, "scale", "Scale"), "clamping cannot hide authored scale 101");
            ModAssert.True(Contains(issues, "vector", "Variants[winter].Anchor"), "partial variant gets a qualified field");
            ModAssert.True(Contains(issues, "size", "States[0].Parts[0].Size"), "partial state is not a complete object");
            ModAssert.Equal(before, data.ToJsonString(), "raw partial input remains untouched");
        }

        [ModTest("data_validation/diagnostic_json_round_trip")]
        static void Serialization(ModTestContext ctx)
        {
            var source = new RpgDataIssue
            {
                Code = "crop",
                Field = "Parts[wall].Crop",
                Detail = "128×256",
                Resource = "check/room",
                Placement = "check_map/room",
                Source = "content/objects/room.json",
                Projection = "isometric",
                Context = "variant:winter/state:1",
                Uses = 3,
            };
            var json = JsonSerializer.Serialize(source);
            var restored = JsonSerializer.Deserialize<RpgDataIssue>(json);
            ModAssert.Equal(source.Code, restored.Code, "code survives JSON");
            ModAssert.Equal(source.Field, restored.Field, "field survives JSON");
            ModAssert.Equal(source.Detail, restored.Detail, "detail survives JSON");
            ModAssert.Equal(source.Resource, restored.Resource, "resource survives JSON");
            ModAssert.Equal(source.Placement, restored.Placement, "placement survives JSON");
            ModAssert.Equal(source.Source, restored.Source, "source survives JSON");
            ModAssert.Equal(source.Projection, restored.Projection, "projection survives JSON");
            ModAssert.Equal(source.Context, restored.Context, "context survives JSON");
            ModAssert.Equal(source.Uses, restored.Uses, "coalesced use count survives JSON");
        }

        private static Vector2I? Size(string group, string id)
        {
            return id == "missing" ? null : new Vector2I(128, 128);
        }

        private static bool Contains(IReadOnlyList<RpgDataIssue> issues, string code, string field)
        {
            foreach (var issue in issues)
            {
                if (issue.Code == code && issue.Field == field)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
