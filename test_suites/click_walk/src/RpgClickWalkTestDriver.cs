using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods.Testing;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Mods.Interface.View;
using Cthangover.Mods.Rpg.Play;
using Cthangover.Tools.RpgEditor;
using Godot;

namespace Cthangover.Mods.Rpg.ClickWalkTests
{
    /// <summary>
    /// Real viewport clicks and keys exercise camera picking, modal ownership, the ordinary party
    /// trail and exactly-once interactions in both projections and ru/en. Existing HUD scenes supply
    /// all gameplay UI; this driver owns only temporary input events and a borrowed editor window.
    /// </summary>
    public sealed partial class RpgClickWalkTestDriver : Node
    {
        private const string MapId = "rpg_click_fixture";

        private readonly string shots;

        private RpgEditorWindow editor;

        private Vector2I expected;

        private bool started;

        private bool finished;

        private double elapsed;

        private int passed;

        private static RpgWorldHost Host => RpgWorldHost.Current;

        private static RpgWorldSession Session => Host.Session;

        /// <summary>The capture directory is outside the repository; the tree owns the driver until coordinated exit.</summary>
        public RpgClickWalkTestDriver(string shots)
        {
            this.shots = shots;
            Name = "RpgClickWalkTestDriver";
        }

        /// <summary>Waits for actual shell readiness and bounds startup without altering renderer or display settings.</summary>
        public override void _Process(double delta)
        {
            if (finished)
            {
                return;
            }

            elapsed += delta;
            if (!started && ShellReady())
            {
                started = true;
                _ = Run();
            }

            if (!started && elapsed > 90)
            {
                finished = true;
                GameLogger.Log("TEST", "[FAIL] click driver startup timed out");
                GameLogger.Log("TEST", "[TEST_RESULT] name=rpg_click_walk passed=0 failed=1");
                ApplicationExit.Quit(GetTree(), 1);
            }
        }

        private async Task Run()
        {
            var exit = 0;
            var previous = LanguageCode.Normalize(TranslationServer.GetLocale()) ?? "en";
            try
            {
                RpgInput.FindDialogBox()?.Abort();
                var roster = GameData.Instance.Runtime.CharacterData;
                roster.AddCharacterToParty("rpg_hero");
                roster.AddCharacterToParty("click_walk_follower");
                GetTree().Root.GetNode<SceneManager>("SceneManager").SwitchScene(MapId);
                await Until(MapReady, "fixture map mounted");
                RpgInput.FindDialogBox()?.Abort();
                var locales = new[] { "ru", "en" };
                var projections = new[] { "topdown", "isometric" };
                foreach (var locale in locales)
                {
                    LocaleLoader.LoadLocale(locale);
                    foreach (var projection in projections)
                    {
                        await Play(locale, projection);
                    }
                }
            }
            catch (Exception error)
            {
                exit = 1;
                if (!string.IsNullOrEmpty(shots))
                {
                    Directory.CreateDirectory(shots);
                    ViewportCapture.SavePng(GetTree().Root, Path.Combine(shots, "failure.png"));
                }

                GameLogger.Log("TEST", "[FAIL] " + error);
            }
            finally
            {
                Press(Key.S, false);
                Press(Key.E, false);
                if (GodotObject.IsInstanceValid(editor))
                {
                    editor.QueueFree();
                }

                await Frames(3);
                LocaleLoader.LoadLocale(previous);
            }

            finished = true;
            GameLogger.Log("TEST", "[TEST_RESULT] name=rpg_click_walk passed=" + passed + " failed=" + exit);
            ApplicationExit.Quit(GetTree(), exit);
        }

        private async Task Play(string locale, string projection)
        {
            Host.Mount(MapId);
            Host.PlacePlayer(1, 2, RpgDirection.Right);
            Check(Host.View.SetRenderer(projection), "actual game renderer switches to " + projection);
            await Frames(6);
            RpgInput.FindDialogBox()?.Abort();
            GameData.Instance.Runtime.Flags.Remove(MapId + "/step_target:reached");
            GameData.Instance.Runtime.Flags.Remove(MapId + "/use_target:used");
            Check(Session.Followers.Members.Count == 1, "one real party follower is visible in the mounted map");
            expected = new Vector2I(5, 2);
            await SaveShot(locale + "_" + projection + "_before");
            Click(expected);
            await Until(Arrived, "real click arrival");
            Check(Session.Player.Cell == expected && !Session.Player.IsMoving, "viewport click reaches its exact logical cell");
            await Frames(40);
            Check(Session.Followers.Members[0].Cell != Session.Player.Cell,
                "the follower spreads along the same ordinary arrival trail");
            Check(Flag(MapId + "/step_target:reached") == null, "walking does not activate a separate future trigger");
            await SaveShot(locale + "_" + projection + "_arrived");
            Host.PlacePlayer(1, 2, RpgDirection.Right);
            Click(new Vector2I(3, 2));
            await Frames(5);
            Check(Session.ClickWalk.Status == RpgClickWalkStatus.BlockedGoal && Session.Player.Cell == new Vector2I(1, 2),
                "a real click on the fence is blocked without direct motion");
            Click(new Vector2I(5, 2));
            await Until(PlayerMoving, "click reserves its first step before keyboard replacement");
            expected = Session.Player.Cell + Vector2I.Down;
            Press(Key.S, true);
            await Until(ReservedExpected, "keyboard takes priority after the already-reserved step");
            Press(Key.S, false);
            await Until(PlayerIdle, "manual step completed");
            Check(Session.ClickWalk.Status == RpgClickWalkStatus.Cancelled && Session.Player.Cell == expected,
                "keyboard cancels the goal and supplies the ordinary step");
            Host.PlacePlayer(1, 2, RpgDirection.Right);
            Click(new Vector2I(5, 2));
            await Until(PlayerMoving, "click starts before modal focus");
            editor = ToolWindow.Open<RpgEditorWindow>();
            await Frames(6);
            Check(RpgInput.IsWalkBlocked(Host), "real editor focus owns movement input");
            Check(Session.ClickWalk.Status == RpgClickWalkStatus.Cancelled, "modal ownership cancels a previous click route");
            Click(new Vector2I(5, 2));
            await Frames(5);
            Check(Session.ClickWalk.Goal == null, "clicking while a tool owns focus creates no hidden world goal");
            editor.QueueFree();
            editor = null;
            await Frames(5);
            GetTree().Root.GrabFocus();
            await Until(PlayerIdle, "already-started step settled after closing tool");
            var stopped = Session.Player.Cell;
            await Frames(15);
            Check(Session.Player.Cell == stopped, "closing the tool never resumes cancelled navigation");
            Host.PlacePlayer(1, 2, RpgDirection.Right);
            expected = new Vector2I(6, 2);
            Click(expected);
            await Until(Arrived, "step target arrival");
            await Frames(8);
            Check(Flag(MapId + "/step_target:reached") == "1", "ordinary step interaction executes at click arrival");
            Press(Key.S, true);
            await Frames(2);
            Press(Key.S, false);
            Check(Session.Player.Cell == expected && Session.Player.Facing == RpgDirection.Down,
                "facing a solid Use target preserves the current cell");
            Press(Key.E, true);
            Press(Key.E, false);
            await Frames(10);
            Check(Flag(MapId + "/use_target:used") == "1", "E remains an explicit ordinary interaction after click walking");
            await SaveShot(locale + "_" + projection + "_interaction");
            var retired = Session;
            retired.ClickWalk.Request(new Vector2I(1, 1));
            Host.Mount(MapId);
            Check(retired.ClickWalk.Goal == null, "map replacement retires the old session's goal");
            GameLogger.Log("TEST", locale + " " + projection + " native click workflow passed");
        }

        private void Click(Vector2I cell)
        {
            var local = Host.View.Presentation.ProjectToView(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0));
            Check(Host.View.Presentation.TryPickGround(local, out var picked) && picked == cell,
                "the real presentation reverses the projected click to the same logical cell");
            var point = Host.View.GetGlobalTransformWithCanvas() * local;
            using var motion = new InputEventMouseMotion
            {
                Position = point,
                GlobalPosition = point,
            };
            Host.GetViewport().PushInput(motion);
            GameLogger.Log("TEST", "click cell=" + cell + " viewport=" + point + " blocked="
                + RpgInput.IsWalkBlocked(Host) + " hovered=" + Host.GetViewport().GuiGetHoveredControl()?.GetPath());
            using var press = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = point,
                GlobalPosition = point,
            };
            using var release = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Position = point,
                GlobalPosition = point,
            };
            Host.GetViewport().PushInput(press);
            Host.GetViewport().PushInput(release);
        }

        private static void Press(Key code, bool pressed)
        {
            using var key = new InputEventKey
            {
                PhysicalKeycode = code,
                Keycode = code,
                Pressed = pressed,
            };
            Input.ParseInputEvent(key);
        }

        private static string Flag(string key)
        {
            return GameData.Instance.Runtime.Flags.TryGetValue(key, out var value) ? value : null;
        }

        private bool ShellReady()
        {
            var manager = GetTree().Root.GetNodeOrNull<SceneManager>("SceneManager");
            return Boot.IsReady && GetTree().CurrentScene != null && Host != null
                && !string.IsNullOrEmpty(manager?.CurrentSceneName) && !GodotSceneService.IsTransitioning;
        }

        private static bool MapReady()
        {
            return Host?.Session?.Map.ID == MapId && !GodotSceneService.IsTransitioning;
        }

        private bool Arrived()
        {
            return Session.ClickWalk.Status == RpgClickWalkStatus.Arrived && Session.Player.Cell == expected;
        }

        private bool ReservedExpected()
        {
            return Session.Player.Cell == expected;
        }

        private static bool PlayerIdle()
        {
            return !Session.Player.IsMoving;
        }

        private static bool PlayerMoving()
        {
            return Session.Player.IsMoving;
        }

        private async Task Until(Func<bool> ready, string description)
        {
            var watchdog = Stopwatch.StartNew();
            while (watchdog.Elapsed.TotalSeconds < 15)
            {
                if (ready())
                {
                    return;
                }

                await Frames(1);
            }

            throw new TimeoutException(description + "; status=" + Host?.Session?.ClickWalk.Status);
        }

        private static async Task Frames(int count)
        {
            var tree = (SceneTree)Godot.Engine.GetMainLoop();
            for (var frame = 0; frame < count; frame++)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }

        private void Check(bool condition, string description)
        {
            ModAssert.True(condition, description);
            passed++;
        }

        private bool DesktopCaptured()
        {
            return File.Exists(Path.Combine(shots, "desktop.done"));
        }

        private async Task SaveShot(string name)
        {
            if (string.IsNullOrEmpty(shots))
            {
                return;
            }

            Directory.CreateDirectory(shots);
            Check(ViewportCapture.SavePng(GetTree().Root, Path.Combine(shots, name + ".png")),
                "capture uses the shared colour pipeline");
            if (name == "ru_topdown_arrived" && System.Environment.GetEnvironmentVariable("ISO2D_CLICK_DISPLAY") == "1")
            {
                File.WriteAllText(Path.Combine(shots, "desktop.ready"), "ready");
                await Until(DesktopCaptured, "native display comparison capture");
            }
        }
    }
}
