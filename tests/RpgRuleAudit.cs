namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;

    /// <summary>Reads the accepted, immutable content-audit fixture and actual bundled translations through the mod provider.</summary>
    internal static class RpgRuleAudit
    {
        internal static JsonObject Read()
        {
            ModAssert.True(GameEnvironment.IsRuntime, "audit fixtures are read through the real mod provider");
            var text = ModManager.Instance.ReadFileText("rpg", "tests/fixtures/data_rules_audit.json");
            ModAssert.NotNull(text, "the frozen accepted audit is bundled with its tests");
            var audit = JsonNode.Parse(text).AsObject();
            var restored = JsonNode.Parse(audit.ToJsonString());
            ModAssert.True(JsonNode.DeepEquals(audit, restored), "all audit counts, decisions and provenance survive JSON");
            return audit;
        }

        internal static Dictionary<string, string> Locale(string language)
        {
            ModAssert.True(GameEnvironment.IsRuntime, "locale resources are read through the actual mod provider");
            var text = ModManager.Instance.ReadFileText("rpg", "locale/validation_" + language + ".properties");
            ModAssert.NotNull(text, "bundled validation locale exists: " + language);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in text.Split('\n'))
            {
                var equals = line.IndexOf('=');
                if (equals > 0)
                {
                    result.Add(line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
                }
            }

            return result;
        }
    }
}
