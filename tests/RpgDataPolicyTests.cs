using System.Collections.Generic;
using Cthangover.Core.Mods.Testing;
using Cthangover.Mods.Rpg.Validation;

namespace Cthangover.Mods.Rpg.Tests
{
    /// <summary>Enforcement is an explicit audited allowlist, independent of the presence of current user mistakes.</summary>
    public sealed class RpgDataPolicyTests
    {
        [ModTest("data_validation/audited_policy_and_advisory_rules")]
        static void Policy(ModTestContext ctx)
        {
            var errors = new[]
            {
                "quad_count", "quad_number", "quad_area", "quad_affine", "vector", "size", "scale", "crop", "material",
                "image", "sheet", "part", "duplicate_part", "enum", "map_size", "map_rows", "reference", "layer_form", "json",
            };
            foreach (var rule in errors)
            {
                ModAssert.True(RpgDataPolicy.Blocks(rule), "zero-hit structural rule prevents a new invalid write: " + rule);
            }

            ModAssert.False(RpgDataPolicy.Blocks("projection"), "additional renderer providers are advisory");
            ModAssert.False(RpgDataPolicy.Blocks("budget"), "a work budget is not a map format ban");
            ModAssert.False(RpgDataPolicy.Blocks("new_rule"), "an unaudited future rule cannot silently prohibit saving");
            var rows = new List<RpgDataIssue>
            {
                new RpgDataIssue
                {
                    Code = "projection",
                },
                new RpgDataIssue
                {
                    Code = "crop",
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
