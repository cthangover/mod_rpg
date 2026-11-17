namespace Cthangover.Mods.Rpg.Validation
{
    using System.Collections.Generic;

    /// <summary>
    /// Explicit policy after the clean map and object-catalog inventory. Unknown rules, optional providers and work budgets
    /// remain advisory; loading arbitrary invalid player data never silently relaxes the accepted authoring rules.
    /// </summary>
    public static class RpgDataPolicy
    {
        /// <summary>Whether an audited structural rule prevents a new save or Apply; severity does not alter detection.</summary>
        public static bool Blocks(string code)
        {
            return RpgDataRules.Find(code)?.BlocksSave == true;
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
