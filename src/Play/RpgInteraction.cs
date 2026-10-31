namespace Cthangover.Mods.Rpg.Play
{
    using System.Text;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Dialog.Scenarios;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>
    /// Runs the <c>Interact</c> of an instance (contract 5.1) — what <c>RpgEventRunner</c> did for v1
    /// events, plus the instance scope. A <c>Teleport</c> moves the player natively (no dialog, so a
    /// door costs nothing). Anything else becomes one dialog in the shell's dialog box: the
    /// scenario body, then the inline <c>Commands</c>, with <see cref="DialogQueue.Scope"/> set to the
    /// placement id — so <c>flag self.open 1</c>, <c>when "self.open == 1"</c> and
    /// <c>win_flag=self.won</c> address this one barrel or wolf, never its twin.
    /// </summary>
    public static class RpgInteraction
    {
        /// <summary>
        /// The teleport target of <paramref name="instance"/>'s current interaction (an empty <c>Map</c>
        /// means <paramref name="currentMap"/>), or null when it does not teleport.
        /// </summary>
        public static RpgTeleport TeleportOf(RpgInstance instance, string currentMap)
        {
            var target = instance?.Current?.Interact?.Teleport;
            if (target == null)
                return null;
            return new RpgTeleport
            {
                Map = string.IsNullOrEmpty(target.Map) ? currentMap : target.Map,
                X = target.X,
                Y = target.Y,
                Dir = target.Dir,
            };
        }

        /// <summary>
        /// The dialog of <paramref name="instance"/>'s current interaction, scoped to the instance; null
        /// when there is nothing to say (a teleport, no scenario and no commands). A missing scenario
        /// file is an ERROR, the commands still play.
        /// </summary>
        public static DialogQueue DialogOf(RpgInstance instance, string currentMap)
        {
            var interact = instance?.Current?.Interact;
            if (interact == null || interact.Teleport != null)
                return null;
            var text = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(interact.Scenario))
            {
                var body = RpgScenarioSource.ReadBody(interact.Scenario);
                if (body == null)
                    GameLogger.Log("RPG", $"'{instance.Id}' on '{currentMap}': scenario '{interact.Scenario}' not found", LogLevel.Error);
                else
                    text.AppendLine(body);
            }
            foreach (var line in interact.Commands ?? new())
                text.AppendLine(line);
            if (text.Length == 0)
                return null;
            var dialog = new ScenarioConverter().Convert(text.ToString());
            if (dialog?.Queue == null || dialog.Queue.Count == 0)
                return null;
            dialog.Scope = instance.Id;
            return dialog;
        }

        /// <summary>
        /// Runs the interaction: teleports at once, or starts the scoped dialog in
        /// <paramref name="dialogBox"/>. False when there is nothing to run or the box is missing or busy.
        /// </summary>
        public static bool Run(RpgInstance instance, string currentMap, DialogBox dialogBox)
        {
            var teleport = TeleportOf(instance, currentMap);
            if (teleport != null)
            {
                RpgWorldHost.Teleport(teleport);
                return true;
            }
            if (dialogBox == null || dialogBox.IsRunning)
                return false;
            var dialog = DialogOf(instance, currentMap);
            if (dialog == null)
                return false;
            GameLogger.Log("RPG", $"interaction '{instance.Id}' on '{currentMap}'");
            return dialogBox.SetDialogQueueAndRun(dialog, null, 0, null);
        }
    }
}
