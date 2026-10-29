namespace Cthangover.Mods.Rpg.Tests
{
    using Cthangover.Core.Mods.Testing;
    /// <summary>Malformed profiles fail before renderer creation rather than stretching unrelated atlas pixels.</summary>
    public class RpgVisualGroupValidatorTests
    {
        [ModTest("visual_groups/invalid_geometry_is_reported")]
        static void InvalidGeometryIsReported(ModTestContext ctx)
        {
            var group = new RpgVisualGroup { Id = "wall", Width = 2, Height = 1, Pattern = new[] { 4, 5 } };
            ModAssert.Null(RpgVisualGroupValidator.Error(group), "valid sprite group");
            group.Pattern = new[] { 4 };
            ModAssert.NotNull(RpgVisualGroupValidator.Error(group), "incomplete pattern");
            group.Pattern = new[] { 4, 5 };
            group.IsoPlanes = new() { new() { Crop = new[] { 0, 0, 3, 1 }, Points = new[] {
                new[] { 0f, 0, 1 }, new[] { 2f, 0, 1 }, new[] { 2f, 0, 0 }, new[] { 0f, 0, 0 } } } };
            ModAssert.NotNull(RpgVisualGroupValidator.Error(group), "crop outside packed group");
            group.IsoPlanes[0].Crop[2] = 2;
            ModAssert.Null(RpgVisualGroupValidator.Error(group), "valid architectural quad");
            group.IsoPlanes[0].Points[2][2] = float.NaN;
            ModAssert.NotNull(RpgVisualGroupValidator.Error(group), "non-finite geometry");
        }
    }
}
