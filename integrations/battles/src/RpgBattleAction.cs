namespace Cthangover.Mods.Rpg.Battles
{
    using Cthangover.Core.Actions;
    using Cthangover.Core.Scenes;

    /// <summary>
    /// <c>action rpg.battle enemies=slime,slime [win_flag=slime_dead]</c> — a
    /// battle that returns to the map the player stands on. Unlike
    /// <c>battle.init</c> + <c>switch_scene Battle</c> it needs no return scene
    /// and can mark the victory with a story flag. End the scenario after it.
    /// <para>
    /// <c>win_flag=self.won</c> names the flag of the object whose interaction runs the dialog
    /// (<see cref="Cthangover.Mods.Dialog.DialogQueue.Scope"/>, contract 5.5): the wolf placement
    /// <c>rpg_forest/wolf_1</c> gets <c>rpg_forest/wolf_1:won</c>, its twin keeps its own.
    /// </para>
    /// <para>
    /// The action exists only in a game that has both the rpg world and turn-based battles
    /// (this integration); a scenario naming it elsewhere only logs it.
    /// </para>
    /// </summary>
    public class RpgBattleAction : IScenarioAction
    {
        /// <summary>DSL name.</summary>
        public string Name => "rpg.battle";

        /// <summary>Reads <c>enemies</c> and <c>win_flag</c> and starts the battle.</summary>
        public void Run(IActionContext ctx)
        {
            var enemies = ctx.GetParam("enemies");
            if (string.IsNullOrWhiteSpace(enemies))
            {
                ctx.LogWarning("RPG", "rpg.battle: missing 'enemies'");
                return;
            }
            RpgBattleService.Start(enemies, WinFlag(ctx.GetParam("win_flag"), RpgInput.FindDialogBox()?.Runtime.Dialog?.Scope));
        }

        /// <summary>
        /// The story flag a <c>win_flag</c> value names in the dialog scope <paramref name="scope"/>:
        /// <c>self.&lt;name&gt;</c> → <c>&lt;scope&gt;:&lt;name&gt;</c>, other names as written, empty → null.
        /// <c>self.</c> without a scope is an ERROR and null — the battle then sets no flag.
        /// </summary>
        public static string WinFlag(string value, string scope)
            => string.IsNullOrWhiteSpace(value) ? null : ScenarioCondition.FlagKey(value.Trim(), scope);
    }
}
