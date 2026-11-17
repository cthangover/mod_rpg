namespace Cthangover.Mods.Rpg.Tests
{
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Validation;

    /// <summary>Optional layers keep the existing empty-map contract while explicit malformed dimensions and rows stay visible.</summary>
    public sealed class RpgMapSyntaxTests
    {
        [ModTest("data_validation/map_optional_layers_and_explicit_rows")]
        static void Rows(ModTestContext ctx)
        {
            var data = JsonNode.Parse("{\"Width\":4,\"Height\":3,\"Unknown\":{\"keep\":true}}").AsObject();
            var before = data.ToJsonString();
            ModAssert.Equal(0, RpgMapSyntax.Check(data).Count, "omitted optional layers are legal");
            ModAssert.Equal(before, data.ToJsonString(), "map checks do not normalize unknown metadata");
            data["Ground"] = new JsonArray("....", "....", "....");
            data["Structure"] = new JsonArray();
            ModAssert.Equal(0, RpgMapSyntax.Check(data).Count, "explicit full rows and an empty layer are legal");
            data["Ground"][2] = "...";
            var issues = RpgMapSyntax.Check(data);
            ModAssert.Equal(1, issues.Count, "only the malformed row is reported");
            ModAssert.Equal(RpgDataRules.MapRows, issues[0].Code, "row rule is stable");
            ModAssert.Equal("Ground[2]", issues[0].Field, "the exact bad row is retained");
        }

        [ModTest("data_validation/map_dimensions_are_not_coerced")]
        static void Dimensions(ModTestContext ctx)
        {
            var data = JsonNode.Parse("{\"Width\":4.5,\"Height\":0,\"TileSize\":-1,\"PatchSize\":\"128\"}").AsObject();
            var before = data.ToJsonString();
            ModAssert.Equal(4, RpgMapSyntax.Check(data).Count, "invalid dimensions retain individual findings");
            ModAssert.Equal(before, data.ToJsonString(), "numeric strings and negative dimensions are never coerced");
        }
    }
}
