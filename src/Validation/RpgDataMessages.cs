namespace Cthangover.Mods.Rpg.Validation
{
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Shares rule explanations between map, object and part tools; native translation is used only inside the game.</summary>
    public static class RpgDataMessages
    {
        /// <summary>Returns an explanation independently of severity, resource identity and source attribution.</summary>
        public static string Explain(string code)
        {
            if (!GameEnvironment.IsRuntime)
            {
                return code;
            }

            var rule = RpgDataRules.Find(code) ?? RpgDataRules.Find(RpgDataRules.Unknown);
            return TranslationServer.Translate(rule.MessageKey);
        }
    }
}
