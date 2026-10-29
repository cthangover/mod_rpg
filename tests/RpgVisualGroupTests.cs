namespace Cthangover.Mods.Rpg.Tests
{
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    /// <summary>Visual metadata survives the same serializer used by content and editor snapshots.</summary>
    public class RpgVisualGroupTests
    {
        [ModTest("visual_groups/json_roundtrip")]
        static void JsonRoundtrip(ModTestContext ctx)
        {
            var tileset = new RpgTileset { ID = "room" };
            tileset.VisualGroups.Add(new RpgVisualGroup { Id = "wall", Width = 2, Height = 1, Pattern = new[] { 1000, 1001 },
                AnchorX = 1, AnchorY = 0.75f, IsoPlanes = new() { new() { Crop = new[] { 0, 0, 2, 1 },
                Points = new[] { new[] { 0f, 0, 1 }, new[] { 2f, 0, 1 }, new[] { 2f, 0, 0 }, new[] { 0f, 0, 0 } } } } });
            var restored = JsonSerializer.Deserialize<RpgTileset>(JsonSerializer.Serialize(tileset), JsonOptionsProvider.ReadOptions);
            var group = restored.VisualGroups[0];
            ModAssert.Equal("wall", group.Id, "group ID");
            ModAssert.Equal(2, group.Width, "width");
            ModAssert.Equal(1, group.Height, "height");
            ModAssert.Equal(1001, group.Pattern[1], "logical source IDs");
            ModAssert.CloseTo(0.75, group.AnchorY, what: "pivot");
            ModAssert.Equal(2, group.IsoPlanes[0].Crop[2], "plane crop");
            ModAssert.CloseTo(1, group.IsoPlanes[0].Points[0][2], what: "elevation");
            ModAssert.Equal(0, JsonSerializer.Deserialize<RpgTileset>("{}").VisualGroups.Count, "legacy default");
        }
    }
}
