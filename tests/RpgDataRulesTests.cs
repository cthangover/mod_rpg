namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Validation;

    /// <summary>Every immutable catalogue row has an audited decision and a nonempty real explanation in both bundled locales.</summary>
    public sealed class RpgDataRulesTests
    {
        [ModTest("data_validation/rule_catalogue_audit_and_ru_en")]
        static void Catalogue(ModTestContext ctx)
        {
            var audit = RpgRuleAudit.Read();
            var decisions = audit["Rules"].AsObject();
            ModAssert.Equal(decisions.Count, RpgDataRules.All.Count, "no unaudited catalogue row is added or lost");
            var codes = new HashSet<string>();
            var languages = new[]
            {
                "ru",
                "en",
            };
            foreach (var language in languages)
            {
                var locale = RpgRuleAudit.Locale(language);
                foreach (var rule in RpgDataRules.All)
                {
                    ModAssert.True(!string.IsNullOrWhiteSpace(rule.Code), "every rule has an identity");
                    ModAssert.True(ReferenceEquals(rule, RpgDataRules.Find(rule.Code)), "lookup returns canonical metadata");
                    ModAssert.True(locale.TryGetValue(rule.MessageKey, out var text)
                        && !string.IsNullOrWhiteSpace(text) && text != rule.MessageKey,
                        "every rule has an actual " + language + " explanation: " + rule.Code);
                    ModAssert.True(decisions.ContainsKey(rule.Code), "a rule cannot bypass the frozen audit: " + rule.Code);
                    if (language == "ru")
                    {
                        ModAssert.True(codes.Add(rule.Code), "rule identities are unique");
                    }
                }
            }

            ModAssert.True(RpgDataRules.Find("unregistered_rule") == null, "lookup does not invent metadata");
            ModAssert.True(RpgDataRules.Find(null) == null, "null stays an unknown identity");
        }
    }
}
