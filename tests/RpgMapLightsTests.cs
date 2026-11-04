namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Play;
    using Cthangover.Mods.Time;

    /// <summary>Shared 2D definitions survive serialization and resource overlays before adapting to map cell space.</summary>
    public class RpgMapLightsTests
    {
        [ModTest("lights/shared_definition_round_trip_and_cell_coordinates")]
        static void RoundTrip(ModTestContext ctx)
        {
            var original = new RpgMap { ID = "lights", Width = 20, Height = 10,
                Lights = new List<LightDef> { new() { Id = "owner/window", X = 0.25f, Y = 0.75f, Radius = 4.5f, Influence = 0.4f, ColorHex = "#ff880080" } } };
            var map = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(original, JsonOptionsProvider.WriteCompactOptions), JsonOptionsProvider.ReadOptions);
            ModAssert.Equal(original.ID, map.ID, "map identity");
            ModAssert.Equal("owner/window", map.Lights[0].Id, "stable source identity");
            ModAssert.Equal(0.25f, map.Lights[0].X, 0.00001f, "normalized x");
            ModAssert.Equal(0.75f, map.Lights[0].Y, 0.00001f, "normalized y");
            ModAssert.Equal(4.5f, map.Lights[0].Radius, 0.00001f, "radius");
            ModAssert.Equal(0.4f, map.Lights[0].Influence, 0.00001f, "strength");
            ModAssert.Equal("#ff880080", map.Lights[0].ColorHex, "color and alpha");
            var lights = RpgMapLights.Of(map);
            ModAssert.Equal(1, lights.Count, "one independent source without objects");
            ModAssert.Equal(5f, lights[0].Position.X, 0.00001f, "x in map cells");
            ModAssert.Equal(7.5f, lights[0].Position.Y, 0.00001f, "y in map cells");
            ModAssert.Equal(4.5f, lights[0].Radius, 0.00001f, "cell radius is projection independent");
            ModAssert.Equal(128f / 255f * 0.4f, lights[0].Color.A, 0.00001f, "shared strength times alpha");
        }

        [ModTest("lights/free_artwork_coordinates_survive_json_and_are_not_floor_clamped")]
        static void WallCoordinates(ModTestContext ctx)
        {
            var original = new RpgMap { ID = "wall", Width = 10, Height = 8, Lights = new List<LightDef>
                { new() { Id = "owner/wall", X = -0.125f, Y = 1.25f, Radius = 2, Influence = 0.7f, ColorHex = "#ffffff" } } };
            var restored = JsonSerializer.Deserialize<RpgMap>(JsonSerializer.Serialize(original, JsonOptionsProvider.WriteCompactOptions), JsonOptionsProvider.ReadOptions);
            var lights = RpgMapLights.Of(restored);
            ModAssert.Equal(1, lights.Count, "artwork beyond the floor is a valid target");
            ModAssert.True(lights[0].ViewSpace, "falloff is circular on the 2D image");
            ModAssert.Equal(-1.25f, lights[0].Position.X, 0.00001f, "negative coordinate survives");
            ModAssert.Equal(10f, lights[0].Position.Y, 0.00001f, "coordinate beyond the map survives");
            ModAssert.Equal(2f, lights[0].Radius, 0.00001f, "source radius survives");
            ModAssert.Equal(0.7f, lights[0].Color.A, 0.00001f, "source strength survives");
            ModAssert.Equal("owner/wall", restored.Lights[0].Id, "patch identity survives");
        }

        [ModTest("lights/keyed_add_edit_remove_and_later_owner_update")]
        static void Patches(ModTestContext ctx)
        {
            var owner = JsonNode.Parse("{\"Id\":\"map\",\"Lights\":[{\"id\":\"owner/a\",\"x\":0.1,\"y\":0.2,\"radius\":3,\"color\":\"#ffffff\",\"plugin\":42},{\"id\":\"owner/b\",\"x\":0.4,\"y\":0.5,\"radius\":2}]}").AsObject();
            var before = owner.ToJsonString();
            var edited = (JsonObject)owner.DeepClone();
            edited["Lights"][0]["radius"] = 6;
            edited["Lights"].AsArray().RemoveAt(1);
            edited["Lights"].AsArray().Add(JsonNode.Parse("{\"id\":\"addon/new\",\"x\":0.7,\"y\":0.8,\"radius\":4}"));
            var patch = JsonPatchDiff.Diff(owner, edited, typeof(RpgMap));
            ModAssert.NotNull(patch["Lights$add"], "edits and additions use keyed additions");
            ModAssert.NotNull(patch["Lights$remove"], "removal uses source id");
            ModAssert.Null(patch["Lights"], "the inherited list is not replaced wholesale");
            var newerOwner = (JsonObject)owner.DeepClone();
            newerOwner["Lights"][0]["color"] = "#22aaee";
            newerOwner["Lights"].AsArray().Add(JsonNode.Parse("{\"id\":\"owner/later\",\"x\":0.9,\"y\":0.9,\"radius\":1}"));
            JsonPatchMerger.Apply(newerOwner, patch, typeof(RpgMap));
            var map = JsonSerializer.Deserialize<RpgMap>(newerOwner.ToJsonString(), JsonOptionsProvider.ReadOptions);
            ModAssert.Equal(3, map.Lights.Count, "new owner source survives alongside the dependent addition");
            ModAssert.Equal("owner/a", map.Lights[0].Id, "edited id remains");
            ModAssert.Equal(6f, map.Lights[0].Radius, 0.00001f, "dependent radius wins");
            ModAssert.Equal("#22aaee", map.Lights[0].ColorHex, "unpatched owner color update survives");
            ModAssert.Equal(42, map.Lights[0].ExtraFields["plugin"].GetInt32(), "unknown extension data survives");
            ModAssert.Equal(before, owner.ToJsonString(), "original owner data is unchanged");
        }
    }
}
