namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Validation;

    /// <summary>Unapplied workspace values stay editable when they cannot be serialized or sent to rendering.</summary>
    public sealed class RpgPartDataCheckTests
    {
        [ModTest("data_validation/typed_workspace_nonfinite_values_and_round_trip")]
        static void Values(ModTestContext ctx)
        {
            var part = new RpgObjectPart
            {
                Id = "wall",
                Image = "prop",
                Geometry = RpgPartGeometry.Ground,
                At = new float[] { -1, 0.5f },
                Size = new float[] { 2, 1 },
                SortOffset = new float[] { 0, -2 },
                Crop = new int[] { 0, 0, 128, 64 },
                Only = "isometric",
            };
            var parts = new List<RpgObjectPart> { part };
            var json = JsonSerializer.Serialize(part, JsonOptionsProvider.ReadOptions);
            var restored = JsonSerializer.Deserialize<RpgObjectPart>(json, JsonOptionsProvider.ReadOptions);
            ModAssert.Equal(json, JsonSerializer.Serialize(restored, JsonOptionsProvider.ReadOptions),
                "all authored part fields survive complete JSON round trip");
            ModAssert.Equal("wall", restored.Id, "merge identity is preserved");
            ModAssert.Equal("isometric", restored.Only, "projection restriction is preserved");
            ModAssert.Equal(0, RpgPartDataCheck.Check(parts).Count, "negative/fractional offsets are valid");
            part.Size[0] = float.NaN;
            var issue = RpgPartDataCheck.Check(parts)[0];
            ModAssert.Equal(RpgDataRules.Size, issue.Code, "nonfinite value is rejected before JSON serialization");
            ModAssert.Equal("Parts[0].Size", issue.Field, "the exact unapplied field is retained");
            ModAssert.True(float.IsNaN(part.Size[0]), "the invalid input remains untouched for correction");
            part.Size[0] = 2;
            ModAssert.Equal(0, RpgPartDataCheck.Check(parts).Count, "corrected input can be applied");
            ModAssert.Equal(0, RpgPartDataCheck.Check(null).Count, "an absent list means an invisible object");
        }
    }
}
