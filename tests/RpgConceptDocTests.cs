namespace Cthangover.Mods.Rpg.Model.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// The examples of <c>docs/concepts/rpg.md</c> are checked as data (<c>concept_doc/*</c>, see
    /// <see cref="RpgConceptDocChecker"/>): every fenced json block reads into its v2 DTO, names no unknown key, and
    /// names only objects, placements, maps, pictures, charsets and scenarios the loaded game has. The second case
    /// breaks a copy of the same text in known places and expects each break to be reported, so a checker that
    /// passes everything cannot pass unnoticed.
    /// </summary>
    public class RpgConceptDocTests
    {
        private const string DocPath = "res://docs/concepts/rpg.md";

        [ModTest("concept_doc/examples_are_data")]
        static void ExamplesAreData(ModTestContext ctx)
        {
            var checker = new RpgConceptDocChecker();
            var errors = checker.Check(ReadDoc());
            ModAssert.True(errors.Count == 0, "the examples of rpg.md are broken:\n" + string.Join("\n", errors));
            foreach (var kind in new[] { "object", "placement", "map", "patch" })
                ModAssert.True(checker.Seen.Contains(kind), $"the doc has an example of kind '{kind}' that the test reads");
        }

        [ModTest("concept_doc/broken_copy_is_reported")]
        static void BrokenCopyIsReported(ModTestContext ctx)
        {
            var expected = new List<string>();
            var broken = ReadDoc();
            broken = Break(broken, "\"Surface\": {\"Image\": \"ground/grass\"}", "\"Surfce\": {\"Image\": \"ground/grass\"}", "'Surfce'", expected);
            broken = Break(broken, "\"Image\": \"container/chest_open\"", "\"Image\": \"container/chest_opn\"", "picture 'container/chest_opn'", expected);
            broken = Break(broken, "\"Object\": \"village_pack/npc/merchant\"", "\"Object\": \"village_pack/npc/merchnt\"", "object 'village_pack/npc/merchnt'", expected);
            broken = Break(broken, "\"Facing\": \"Left\"", "\"Facng\": \"Left\"", "'Facng'", expected);
            broken = Break(broken, "\"Scenario\": \"rpg_plus/events/merchant\"", "\"Scenario\": \"rpg_plus/events/merchnt\"", "scenario 'rpg_plus/events/merchnt'", expected);
            broken = Break(broken, "\"Map\": \"rpg_elder_house\"", "\"Map\": \"rpg_elder_hose\"", "map 'rpg_elder_hose'", expected);
            broken = Break(broken, "\"Trigger\": \"Step\", \"Teleport\"", "\"Trigger\": \"Stepp\", \"Teleport\"", "does not read as", expected);
            broken = Break(broken, "\"Object\": \"village_pack/ground/water\"", "\"Object\": \"village_pack/ground/watr\"", "object 'village_pack/ground/watr'", expected);
            broken = Break(broken, "{\"Items\": [{\"Id\": \"rpg_village/supply_rpg_village_22_11\"", "{\"Items\": [{\"Id\": \"rpg_village/supply_rpg_village_22_111\"", "names no object, placement or map", expected);

            var errors = new RpgConceptDocChecker().Check(broken);
            ModAssert.True(errors.Count >= expected.Count, $"each break gives an error: {errors.Count} errors for {expected.Count} breaks");
            foreach (var fragment in expected)
            {
                var reported = false;
                foreach (var error in errors)
                {
                    if (error.Contains(fragment, StringComparison.Ordinal))
                    {
                        reported = true;
                        break;
                    }
                }

                ModAssert.True(reported, $"the checker reports a break with '{fragment}'");
            }
        }

        private static string ReadDoc()
        {
            ModAssert.True(GameEnvironment.IsRuntime, "rpg.md is read with Godot FileAccess, which needs the running engine");
            var file = FileAccess.Open(DocPath, FileAccess.ModeFlags.Read);
            ModAssert.NotNull(file, "docs/concepts/rpg.md opens");
            var text = file.GetAsText();
            file.Close();
            return text;
        }

        /// <summary>Replaces one unique place of the text and records the error fragment the break must produce.</summary>
        private static string Break(string text, string from, string to, string fragment, List<string> expected)
        {
            var first = text.IndexOf(from, StringComparison.Ordinal);
            ModAssert.True(first >= 0 && text.IndexOf(from, first + 1, StringComparison.Ordinal) < 0,
                $"the broken copy changes exactly one place: {from}");
            expected.Add(fragment);
            return text.Replace(from, to);
        }
    }
}
