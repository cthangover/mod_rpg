namespace Cthangover.Mods.Rpg.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The resources an interaction names as it is written in JSON: its scenario, the map of its teleport, and the item
    /// and shop of the command lines that <c>rpg_editor</c> writes for the item and shop kinds. The editors use it to make
    /// the own mod depend on the mods that provide them (plan 14.G16); the game itself reads the same fields. The kinds
    /// are those of <c>ModDependencies</c>: <c>scenario</c> (a file), <c>rpg_map</c>, <c>item</c> and <c>shop</c> (entities).
    /// </summary>
    public static class RpgInteractReferences
    {
        /// <summary>The kind of a scenario reference (its id is the path under <c>scenarios/</c>, no extension).</summary>
        public const string ScenarioKind = "scenario";

        /// <summary>The kind of a map reference (the maps are entities of this kind).</summary>
        public const string MapKind = "rpg_map";

        /// <summary>The kind of an item reference (named by <c>action item.add</c>).</summary>
        public const string ItemKind = "item";

        /// <summary>The kind of a shop reference (named by <c>action trade.open</c>).</summary>
        public const string ShopKind = "shop";

        private static readonly Regex ItemLine = new(@"^action item\.add id=(\S+) count=(\d+)$", RegexOptions.CultureInvariant);
        private static readonly Regex ShopLine = new(@"^action trade\.open shop=(\S+)$", RegexOptions.CultureInvariant);

        /// <summary>
        /// The references of an interaction object (<c>{"Scenario": …, "Teleport": {"Map": …}, "Commands": […]}</c>); null or an
        /// empty object gives none. A command line that is not one of the item or shop forms names nothing.
        /// </summary>
        public static IEnumerable<(string Kind, string Id)> Of(JsonObject interact)
        {
            if (interact == null)
                yield break;
            if (Text(interact["Scenario"]) is { Length: > 0 } scenario)
                yield return (ScenarioKind, scenario);
            if (Text(interact["Teleport"]?["Map"]) is { Length: > 0 } map)
                yield return (MapKind, map);
            if (interact["Commands"] is not JsonArray commands)
                yield break;
            foreach (var command in commands)
            {
                var line = Text(command);
                if (line == null)
                    continue;
                var item = ItemLine.Match(line);
                if (item.Success)
                {
                    yield return (ItemKind, item.Groups[1].Value);
                    continue;
                }
                var shop = ShopLine.Match(line);
                if (shop.Success)
                    yield return (ShopKind, shop.Groups[1].Value);
            }
        }

        /// <summary>The text of a JSON string value; null for anything else.</summary>
        private static string Text(JsonNode node)
            => node is JsonValue value && value.TryGetValue(out string text) ? text : null;
    }
}
