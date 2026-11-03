namespace Cthangover.Mods.Rpg.Play
{
    using System.Collections.Generic;
    using System.Text;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Dialog.Action.Impls;
    using Cthangover.Mods.Dialog.Scenarios;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>
    /// Runs the <c>Interact</c> of an instance (contract 5.1) — what <c>RpgEventRunner</c> did for v1
    /// events, plus the instance scope. A <c>Teleport</c> moves the player natively (no dialog, so a
    /// door costs nothing). Anything else becomes one dialog in the shell's dialog box: the
    /// scenario body, then the inline <c>Commands</c>, with <see cref="DialogQueue.Scope"/> set to the
    /// placement id — so <c>flag self.open 1</c>, <c>when "self.open == 1"</c> and
    /// <c>win_flag=self.won</c> address this one barrel or wolf, never its twin.
    /// <para>
    /// The commands are not glued to the body's text: they sit after it on a label, and every
    /// <c>end</c> of the body jumps there. A scenario ends with <c>end</c> (the editor's template does,
    /// and so does any branch), so without this jump the commands after it — a companion's join line —
    /// would never run.
    /// </para>
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
            string body = null;
            if (!string.IsNullOrWhiteSpace(interact.Scenario))
            {
                body = RpgScenarioSource.ReadBody(interact.Scenario);
                if (body == null)
                    GameLogger.Log("RPG", $"'{instance.Id}' on '{currentMap}': scenario '{interact.Scenario}' not found", LogLevel.Error);
            }
            var dialog = Compose(body, interact.Commands);
            if (dialog == null)
                return null;
            dialog.Scope = instance.Id;
            return dialog;
        }

        /// <summary>
        /// One queue from a scenario <paramref name="body"/> (DSL text, may be null) and the inline
        /// <paramref name="commands"/> of an interaction. The commands are converted on their own and
        /// placed after the body behind the label <see cref="CommandsLabel"/>; each <c>end</c> of the body,
        /// on any branch, becomes a <c>goto</c> to that label, so the commands run exactly once after the
        /// body however it finishes (an <c>end</c>, the last line, a branch). The commands keep their own
        /// <c>end</c>, which ends the queue after them. Null when there is nothing to play.
        /// </summary>
        public static DialogQueue Compose(string body, IEnumerable<string> commands)
        {
            var converter = new ScenarioConverter();
            var dialog = string.IsNullOrWhiteSpace(body) ? new DialogQueue() : converter.Convert(body);
            var text = new StringBuilder();
            if (commands != null)
                foreach (var line in commands)
                    text.AppendLine(line);
            if (text.Length > 0)
            {
                var tail = converter.Convert(text.ToString());
                if (tail.Queue.Count > 0)
                {
                    if (dialog.Queue.Count > 0)
                    {
                        RedirectEnds(dialog, CommandsLabel);
                        dialog.Point(CommandsLabel);
                    }
                    dialog.Queue.AddRange(tail.Queue);
                }
            }
            return dialog.Queue.Count == 0 ? null : dialog;
        }

        /// <summary>Label the commands of an interaction start at, after the scenario body.</summary>
        private const string CommandsLabel = "rpg_commands";

        /// <summary>Replaces every <c>end</c> of <paramref name="dialog"/> with a jump to <paramref name="label"/>.</summary>
        private static void RedirectEnds(DialogQueue dialog, string label)
        {
            for (var i = 0; i < dialog.Queue.Count; i++)
                if (dialog.Queue[i] is ActionEnd end)
                    dialog.Queue[i] = new ActionGoTo { GoTo = label, SourceLine = end.SourceLine };
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
