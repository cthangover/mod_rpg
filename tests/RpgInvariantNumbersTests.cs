namespace Cthangover.Mods.Rpg.Tests
{
    using System.Globalization;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Numbers in RPG data and DSL mean the same under any player culture (plan 13.0): the test switches
    /// the thread to a hand-built culture with a decimal comma and a non-ASCII minus — ru-RU/es-ES cannot be
    /// created without ICU, and the rule must hold for any culture anyway.
    /// </summary>
    public class RpgInvariantNumbersTests
    {
        private static CultureInfo Comma()
        {
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            comma.NumberFormat.NumberGroupSeparator = " ";
            comma.NumberFormat.NegativeSign = "−";
            return comma;
        }

        [ModTest("invariant/teleport_cell_and_map_numbers")]
        static void TeleportAndMapNumbers(ModTestContext ctx)
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = Comma();
                var context = new RpgFakeActionContext();
                context.Params["map"] = "forest";
                context.Params["x"] = "-2";
                context.Params["y"] = "17";
                context.Params["dir"] = "up";
                var target = RpgTeleportAction.Target(context);
                ModAssert.NotNull(target, "an ASCII minus parses under a culture with another minus sign");
                ModAssert.Equal(-2, target.X, "x");
                ModAssert.Equal(17, target.Y, "y");
                ModAssert.True(target.Dir == RpgDirection.Up, "dir");
                context.Params["x"] = "2,5";
                ModAssert.Null(RpgTeleportAction.Target(context), "a culture-formatted number is not a cell");

                var map = JsonSerializer.Deserialize<Model.RpgMap>(@"{""Id"": ""m"", ""Width"": 2, ""Height"": 1, ""Zoom"": 1.5}");
                ModAssert.CloseTo(1.5, map.Zoom, what: "map Zoom is read with a decimal point");
                var light = JsonSerializer.Deserialize<Model.RpgObjectLight>(@"{""Radius"": 2.5, ""Color"": ""#ffd8a0""}");
                ModAssert.CloseTo(2.5, light.Radius, what: "light radius is read with a decimal point");
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }
    }
}
