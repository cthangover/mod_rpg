namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;

    /// <summary>A placement keeps its overrides exactly as written (null included) and knows its map from its id.</summary>
    public class RpgPlacementTests
    {
        private const string Door = @"{""Id"": ""rpg_village/door_townhouse"", ""Object"": ""village_pack/door/wood_double"", ""X"": 40, ""Y"": 11,
            ""Variant"": ""autumn"", ""Interact"": {""Teleport"": {""Map"": ""rpg_plus_townhouse"", ""X"": 5, ""Y"": 8, ""Dir"": ""Up""}}, ""Light"": null}";

        private static RpgPlacement Read(string json) => JsonSerializer.Deserialize<RpgPlacement>(json, JsonOptionsProvider.ReadOptions);

        [ModTest("model/placement_reads_and_round_trips")]
        static void RoundTrip(ModTestContext ctx)
        {
            var original = Read(Door);
            ModAssert.Equal("rpg_village/door_townhouse", original.ID, "id");
            ModAssert.Equal("village_pack/door/wood_double", original.Object, "object");
            ModAssert.Equal(40, original.X, "x");
            ModAssert.Equal(11, original.Y, "y");
            ModAssert.Equal("autumn", original.Variant, "variant");
            ModAssert.Equal(2, original.Overrides.Count, "only object fields are overrides");

            var restored = Read(JsonSerializer.Serialize(original, JsonOptionsProvider.WriteIndentedOptions));
            ModAssert.Equal(original.ID, restored.ID, "id survives");
            ModAssert.Equal(original.Object, restored.Object, "object survives");
            ModAssert.Equal(40, restored.X, "x survives");
            ModAssert.Equal(11, restored.Y, "y survives");
            ModAssert.Equal("autumn", restored.Variant, "variant survives");
            ModAssert.True(restored.Overrides["Light"].ValueKind == JsonValueKind.Null, "a null override survives");
            ModAssert.Equal("rpg_plus_townhouse", restored.Overrides["Interact"].GetProperty("Teleport").GetProperty("Map").GetString(), "nested override survives");
        }

        [ModTest("model/placement_map_from_id")]
        static void MapFromId(ModTestContext ctx)
        {
            ModAssert.Equal("rpg_village", Read(Door).MapId, "the part before the first slash");
            ModAssert.Equal("cave", new RpgPlacement { ID = "cave/deep/chest" }.MapId, "only the first slash splits");
            ModAssert.Null(new RpgPlacement { ID = "loose" }.MapId, "no map without a slash");
            ModAssert.Null(new RpgPlacement { ID = "/x" }.MapId, "no empty map id");
            ModAssert.Null(new RpgPlacement().MapId, "no id");
            ModAssert.Null(Read(@"{""Id"": ""m/a"", ""Object"": ""o""}").Overrides, "no overrides written");
        }
    }
}
