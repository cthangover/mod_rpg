namespace Cthangover.Mods.Rpg.Play.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Dialog;

    /// <summary>
    /// The inline <c>Commands</c> of an interaction after its scenario body (<see cref="RpgInteraction.Compose"/>):
    /// they run exactly once, after the body, however the body finishes — an <c>end</c>, a branch that
    /// ends, or its last line. The queue runs on a box-less <see cref="DialogRuntime"/>; <c>cset n=n+1</c>
    /// counts the passes, so a command that ran twice or never shows in <c>n</c>.
    /// </summary>
    public class RpgInteractionCommandsTests
    {
        private const string Pick = "rpg_test_pick";

        private static readonly string[] Commands = { "cset n=n+1", "set joined 1", "end" };

        [ModTest("interaction/commands_after_end_run_once")]
        static void CommandsAfterEnd(ModTestContext ctx)
        {
            var dlg = RpgInteraction.Compose(Lines("set n 0", "set said 1", "end"), Commands);
            ModAssert.NotNull(dlg, "a body with commands makes a queue");
            var runtime = Play(dlg);
            ModAssert.Equal("1", runtime.GetVariable("said"), "the body ran");
            ModAssert.Equal("1", runtime.GetVariable("joined"), "the commands after the body's end ran");
            ModAssert.Equal("1", runtime.GetVariable("n"), "the commands ran exactly once");
            ModAssert.True(runtime.IsEnd, "the queue ended after the commands");
        }

        [ModTest("interaction/commands_after_two_ending_branches")]
        static void CommandsAfterBranches(ModTestContext ctx)
        {
            var body = Lines(
                "set n 0",
                "when \"" + Pick + " == 1\" -> :second",
                "set said first",
                "end",
                ":second",
                "set said second",
                "end");
            var runtime = GameData.Instance.Runtime;
            var savedFlags = runtime.Flags;
            runtime.Flags = new Dictionary<string, string>();
            try
            {
                var first = Play(RpgInteraction.Compose(body, Commands));
                ModAssert.Equal("first", first.GetVariable("said"), "the branch without the flag runs its own end");
                ModAssert.Equal("1", first.GetVariable("n"), "the first branch's end led to the commands, once");
                ModAssert.Equal("1", first.GetVariable("joined"), "the first branch's commands ran");

                runtime.Flags[Pick] = "1";
                var second = Play(RpgInteraction.Compose(body, Commands));
                ModAssert.Equal("second", second.GetVariable("said"), "the branch with the flag runs its own end");
                ModAssert.Equal("1", second.GetVariable("n"), "the second branch's end led to the commands, once");
                ModAssert.Equal("1", second.GetVariable("joined"), "the second branch's commands ran");
            }
            finally
            {
                runtime.Flags = savedFlags;
            }
        }

        [ModTest("interaction/commands_after_body_without_end")]
        static void CommandsAfterBodyWithoutEnd(ModTestContext ctx)
        {
            var dlg = RpgInteraction.Compose(Lines("set n 0", "set said 1"), Commands);
            ModAssert.NotNull(dlg, "a body with commands makes a queue");
            var runtime = Play(dlg);
            ModAssert.Equal("1", runtime.GetVariable("said"), "the body ran");
            ModAssert.Equal("1", runtime.GetVariable("joined"), "the body's last line falls into the commands");
            ModAssert.Equal("1", runtime.GetVariable("n"), "the commands ran exactly once");
        }

        [ModTest("interaction/commands_without_body")]
        static void CommandsWithoutBody(ModTestContext ctx)
        {
            var dlg = RpgInteraction.Compose(null, new[] { "set joined 1", "end" });
            ModAssert.NotNull(dlg, "commands alone make a queue");
            var runtime = Play(dlg);
            ModAssert.Equal("1", runtime.GetVariable("joined"), "the commands ran");
            ModAssert.Null(RpgInteraction.Compose(null, new List<string>()), "nothing to say, no queue");
        }

        private static string Lines(params string[] lines) => string.Join("\n", lines);

        private static DialogRuntime Play(DialogQueue dlg)
        {
            var runtime = new DialogRuntime();
            runtime.Dialog = dlg;
            runtime.SetDialogQueueAndRun(null, dlg.Queue, null);
            runtime.Run();
            return runtime;
        }
    }
}
