namespace Cthangover.Mods.Rpg.Validation
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>Reads authored JSON without coercing numeric strings or applying normalizing DTO setters.</summary>
    internal static class RpgDataValue
    {
        internal static bool Number(JsonNode node, out double value)
        {
            value = 0;
            return node != null && node.GetValueKind() == JsonValueKind.Number
                && double.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && double.IsFinite(value) && Math.Abs(value) <= float.MaxValue;
        }

        internal static bool PositiveInteger(JsonNode node)
        {
            return Number(node, out var value) && value > 0 && value <= int.MaxValue && value == Math.Truncate(value);
        }

        internal static string Text(JsonNode node)
        {
            return node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;
        }

        internal static bool Vector(JsonNode node, int count, bool positive = false)
        {
            if (node is not JsonArray values || values.Count != count)
            {
                return false;
            }

            foreach (var item in values)
            {
                if (!Number(item, out var number) || positive && number <= 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
