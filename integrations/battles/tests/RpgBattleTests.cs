namespace Cthangover.Mods.Rpg.Battles.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>Random encounters and the win flag of <c>rpg.battle</c> (<c>battles/*</c>): the battle decisions the integration makes.</summary>
    public class RpgBattleTests
    {
        [ModTest("battles/encounter_off_without_table")]
        static void EncounterOffWithoutTable(ModTestContext ctx)
        {
            var random = new Random(1);
            ModAssert.Null(RpgEncounterRoller.Roll(null, random), "no table");
            ModAssert.Null(RpgEncounterRoller.Roll(new RpgEncounterTable { Steps = 0, Groups = new() { "a" } }, random), "steps 0");
            ModAssert.Null(RpgEncounterRoller.Roll(new RpgEncounterTable { Steps = 1 }, random), "no groups");
        }

        [ModTest("battles/encounter_every_step_picks_a_group")]
        static void EncounterEveryStepPicksAGroup(ModTestContext ctx)
        {
            var table = new RpgEncounterTable { Steps = 1, Groups = new() { "slime", "wolf,slime" } };
            var random = new Random(7);
            var seen = new HashSet<string>();
            for (var i = 0; i < 50; i++)
                seen.Add(RpgEncounterRoller.Roll(table, random));

            ModAssert.Equal(2, seen.Count, "both groups are picked, never null");
            ModAssert.True(seen.Contains("wolf,slime"), "groups come back as written");
        }

        [ModTest("battles/encounter_rate_follows_steps")]
        static void EncounterRateFollowsSteps(ModTestContext ctx)
        {
            var table = new RpgEncounterTable { Steps = 10, Groups = new() { "slime" } };
            var random = new Random(3);
            var battles = 0;
            for (var i = 0; i < 10000; i++)
            {
                if (RpgEncounterRoller.Roll(table, random) != null)
                    battles++;
            }
            ModAssert.True(battles > 800 && battles < 1200, $"about one battle in ten steps, got {battles}");
        }

        [ModTest("battles/map_encounters_field_is_read_from_extra_fields")]
        static void MapEncountersFieldIsReadFromExtraFields(ModTestContext ctx)
        {
            var json = @"{""Id"":""m"",""Width"":2,""Height"":1,""Encounters"":{""Steps"":20,""Groups"":[""slime""]}}";
            var map = JsonSerializer.Deserialize<RpgMap>(json, JsonOptionsProvider.ReadOptions);
            ModAssert.True(map.ExtraFields != null && map.ExtraFields.ContainsKey("Encounters"), "the world keeps the field as written");

            var table = RpgEncounterListener.TableOf(map);
            ModAssert.Equal(20, table.Steps, "encounter steps");
            ModAssert.Equal("slime", table.Groups[0], "encounter group");
            ModAssert.Equal(0, RpgEncounterListener.TableOf(new RpgMap { ID = "plain" }).Steps, "a map without the field has no battles");

            var again = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(map), JsonOptionsProvider.ReadOptions);
            ModAssert.Equal(20, RpgEncounterListener.TableOf(again).Steps, "the field survives a save of the map");
        }

        [ModTest("battles/broken_encounters_field_means_no_battles")]
        static void BrokenEncountersFieldMeansNoBattles(ModTestContext ctx)
        {
            var json = @"{""Id"":""m"",""Width"":2,""Height"":1,""Encounters"":""slime""}";
            var map = JsonSerializer.Deserialize<RpgMap>(json, JsonOptionsProvider.ReadOptions);
            ModAssert.Equal(0, RpgEncounterListener.TableOf(map).Steps, "a text instead of {Steps, Groups} is an ERROR and no battles");
        }

        [ModTest("battles/win_flag_self_scope")]
        static void WinFlagSelfScope(ModTestContext ctx)
        {
            ModAssert.Equal("r4_test/wolf_1:won", RpgBattleAction.WinFlag(" self.won ", "r4_test/wolf_1"), "win_flag=self.won is the placement's flag");
            ModAssert.Equal("rpg_wolf_dead", RpgBattleAction.WinFlag("rpg_wolf_dead", "r4_test/wolf_1"), "a global flag stays global");
            ModAssert.Null(RpgBattleAction.WinFlag("", "r4_test/wolf_1"), "no flag");
            ModAssert.Null(RpgBattleAction.WinFlag("self.won", null), "self. outside a scope writes nothing");
        }
    }
}
