namespace Cthangover.Mods.Rpg.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Validation;

    /// <summary>Severity stays equal to the accepted zero-hit inventory, rather than a duplicated code allowlist in this test.</summary>
    public sealed class RpgDataPolicyTests
    {
        [ModTest("data_validation/audited_policy_and_advisory_rules")]
        static void Policy(ModTestContext ctx)
        {
            var audit = RpgRuleAudit.Read();
            ModAssert.Equal(0, audit["Findings"].GetValue<int>(), "the fixed existing-content audit had no findings");
            ModAssert.True(audit["MapProjectionChecks"].GetValue<int>() > 0
                && audit["ObjectProjectionChecks"].GetValue<int>() > 0, "both map and standalone-object inventories ran");
            foreach (var entry in audit["Rules"].AsObject())
            {
                var rule = RpgDataRules.Find(entry.Key);
                ModAssert.NotNull(rule, "every accepted decision retains a catalogue row");
                var expected = entry.Value["BlocksSave"].GetValue<bool>();
                ModAssert.True(rule.BlocksSave == expected && RpgDataPolicy.Blocks(entry.Key) == expected,
                    "severity matches the accepted audit, including advisory decisions: " + entry.Key);
                if (expected)
                {
                    ModAssert.Equal(0, entry.Value["MapFindings"].GetValue<int>(), "blocking rule had zero map hits");
                    ModAssert.Equal(0, entry.Value["ObjectFindings"].GetValue<int>(), "blocking rule had zero object hits");
                }
            }

            ModAssert.False(RpgDataPolicy.Blocks(RpgDataRules.Projection), "additional renderer providers are advisory");
            ModAssert.False(RpgDataPolicy.Blocks(RpgDataRules.Budget), "a work budget is not a map format ban");
            ModAssert.False(RpgDataPolicy.Blocks("new_rule"), "an unaudited future rule cannot silently prohibit saving");
            var rows = new List<RpgDataIssue>
            {
                new RpgDataIssue
                {
                    Code = RpgDataRules.Projection,
                },
                new RpgDataIssue
                {
                    Code = RpgDataRules.Crop,
                    Field = "Parts[1].Crop",
                },
            };
            ModAssert.True(ReferenceEquals(rows[1], RpgDataPolicy.FirstBlocking(rows)), "the first actionable finding is retained");
            ModAssert.Equal(2, rows.Count, "severity inspection does not remove or change report rows");
            rows.RemoveAt(1);
            ModAssert.True(RpgDataPolicy.FirstBlocking(rows) == null, "warnings alone allow a write");
        }
    }
}
