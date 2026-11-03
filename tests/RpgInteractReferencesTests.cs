namespace Cthangover.Mods.Rpg.Tests
{
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>
    /// <see cref="RpgInteractReferences.Of"/>: the scenario, the teleport's map, and the item and shop command lines of an
    /// interaction, each with its kind; what the editors need for the mods an own map depends on (plan 14.G16).
    /// </summary>
    public class RpgInteractReferencesTests
    {
        [ModTest("interact_refs/names_scenario_map_item_and_shop")]
        static void NamesEverything(ModTestContext ctx)
        {
            var interact = new JsonObject
            {
                ["Scenario"] = "rpg_plus/woodland/elder",
                ["Teleport"] = new JsonObject { ["Map"] = "rpg_elder_house", ["X"] = 1, ["Y"] = 2 },
                ["Commands"] = new JsonArray(JsonValue.Create("action item.add id=rpg_plus/elixir count=2"), JsonValue.Create("action trade.open shop=rpg_plus/village_merchant")),
            };
            var refs = RpgInteractReferences.Of(interact).ToList();
            ModAssert.True(refs.Count == 4, "a scenario, a map, an item and a shop");
            ModAssert.True(refs[0] == ("scenario", "rpg_plus/woodland/elder"), "the scenario is a file reference");
            ModAssert.True(refs[1] == ("rpg_map", "rpg_elder_house"), "the teleport names a map entity");
            ModAssert.True(refs[2] == ("item", "rpg_plus/elixir"), "the item line names its item");
            ModAssert.True(refs[3] == ("shop", "rpg_plus/village_merchant"), "the shop line names its shop");
        }

        [ModTest("interact_refs/other_commands_name_nothing")]
        static void OtherCommands(ModTestContext ctx)
        {
            var interact = new JsonObject
            {
                ["Commands"] = new JsonArray(JsonValue.Create("flag self.open 1"), JsonValue.Create("action item.add id=x count=abc"), JsonValue.Create("action party.add id=rpg_companion")),
            };
            ModAssert.True(!RpgInteractReferences.Of(interact).Any(), "flags, a malformed count and party changes are no reference");
        }

        [ModTest("interact_refs/empty_and_missing_give_none")]
        static void Empty(ModTestContext ctx)
        {
            ModAssert.True(!RpgInteractReferences.Of(null).Any(), "no interaction, no reference");
            ModAssert.True(!RpgInteractReferences.Of(new JsonObject { ["Trigger"] = "Use", ["Scenario"] = "" }).Any(), "an empty scenario is none");
            ModAssert.True(!RpgInteractReferences.Of(new JsonObject { ["Teleport"] = new JsonObject { ["X"] = 0 } }).Any(), "a teleport without a map is none");
        }
    }
}
