using System.Collections.Generic;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>
    /// Explicit policy after the clean map and object-catalog inventory. Unknown rules, optional providers and work budgets
    /// remain advisory; loading arbitrary invalid player data never silently relaxes the accepted authoring rules.
    /// </summary>
    public static class RpgDataPolicy
    {
        /// <summary>Whether an audited structural rule prevents a new save or Apply; severity does not alter detection.</summary>
        public static bool Blocks(string code)
        {
            return code is "quad_count" or "quad_number" or "quad_area" or "quad_affine"
                or "vector" or "size" or "scale" or "crop" or "material" or "image" or "sheet"
                or "part" or "duplicate_part" or "enum" or "map_size" or "map_rows"
                or "reference" or "layer_form" or "json";
        }

        /// <summary>Returns the first actionable error without modifying findings or changing a document.</summary>
        public static RpgDataIssue FirstBlocking(IEnumerable<RpgDataIssue> issues)
        {
            foreach (var issue in issues)
            {
                if (Blocks(issue.Code))
                {
                    return issue;
                }
            }

            return null;
        }
    }
}
