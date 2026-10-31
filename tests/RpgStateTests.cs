namespace Cthangover.Mods.Rpg.Tests
{
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>The saved position and the map file format survive a JSON round trip.</summary>
    public class RpgStateTests
    {
        [ModTest("state/save_and_load_round_trip")]
        static void SaveAndLoadRoundTrip(ModTestContext ctx)
        {
            var state = RpgState.Instance;
            var before = state.Data;
            try
            {
                state.Place("rpg_forest", 4, 7, RpgDirection.Left);
                var slot = new ModSlot();
                var saveable = new RpgSaveable();
                saveable.OnSave(slot);

                var json = JsonSerializer.Serialize(slot);
                var restored = JsonSerializer.Deserialize<ModSlot>(json);
                state.Reset();
                ModAssert.True(saveable.OnLoad(restored, out var error), error);

                ModAssert.Equal("rpg_forest", state.Data.Map, "map");
                ModAssert.Equal(4, state.Data.X, "x");
                ModAssert.Equal(7, state.Data.Y, "y");
                ModAssert.Equal("Left", state.Data.Dir.ToString(), "facing");
            }
            finally
            {
                state.Data = before;
            }
        }

        [ModTest("state/load_without_data_goes_to_spawn")]
        static void LoadWithoutDataGoesToSpawn(ModTestContext ctx)
        {
            var state = RpgState.Instance;
            var before = state.Data;
            try
            {
                state.Place("rpg_forest", 1, 1, RpgDirection.Up);
                ModAssert.True(new RpgSaveable().OnLoad(new ModSlot(), out _), "an empty slot is accepted");
                ModAssert.Null(state.Data.Map, "no map: the next entry uses the spawn");
            }
            finally
            {
                state.Data = before;
            }
        }

        [ModTest("state/map_file_round_trip")]
        static void MapFileRoundTrip(ModTestContext ctx)
        {
            var json = @"{""Id"":""m"",""Width"":2,""Height"":1,
                ""Spawn"":{""X"":1,""Y"":0,""Dir"":""left""},
                ""Atmosphere"":""woodland"",""Ambient"":""#c5d6b0"",
                ""Encounters"":{""Steps"":20,""Groups"":[""slime""]}}";

            var map = JsonSerializer.Deserialize<RpgMap>(json, Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);
            var again = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(map), Cthangover.Core.Utils.JsonOptionsProvider.ReadOptions);

            ModAssert.Equal("m", again.ID, "id");
            ModAssert.Equal("woodland", again.Atmosphere, "atmosphere survives saves and editor snapshots");
            ModAssert.Equal("#c5d6b0", again.Ambient, "fixed ambient retained");
            ModAssert.Equal("Left", again.Spawn.Dir.ToString(), "spawn facing, read in any case");
            ModAssert.True(again.ExtraFields != null && again.ExtraFields.ContainsKey("Encounters"), "fields of other mods survive a save");
            ModAssert.True(again.Zoom == 2f, "zoom default");
        }
    }
}
