namespace Cthangover.Mods.Rpg.PlainWalkTests
{
    using System.Collections.Generic;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Godot;

    /// <summary>
    /// The rpg world without turn-based battles, headless: the novel template (no battle mechanic) with the
    /// <c>rpg</c> mod added, and a minimal map of this suite. Real key events walk the player; a wall stops
    /// them; a step trigger runs its commands (a self flag); the map has an <c>Encounters</c> field that
    /// would start a battle on every plain step — none starts, because the battles integration is absent.
    /// <para/>
    /// Result line: <c>[TEST_RESULT] name=rpg_plain_walk passed=N failed=M</c>, exit code 0/1, 60 s watchdog.
    /// </summary>
    public partial class RpgPlainWalkTestDriver : Node
    {
        private const float TickSeconds = 0.1f;
        private const float TimeoutSeconds = 60f;
        private const string MapId = "rpg_plain_test";
        private const string TriggerReached = "rpg_plain_test/trigger:reached";

        private readonly List<string> failures = new();
        private int passed;
        private int step;
        private int waitTicks;
        private double elapsed;

        /// <summary>Creates the driver and starts its timer.</summary>
        public RpgPlainWalkTestDriver()
        {
            Name = "RpgPlainWalkTestDriver";
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private static RpgWorldView View => RpgWorldHost.Current?.View;

        private static bool OnMap => View?.Map?.ID == MapId && !GodotSceneService.IsTransitioning;

        private static bool SceneIsMap => SceneNow() == MapId;

        private static string Flag(string name) => GameData.Instance.Runtime.Flags.TryGetValue(name, out var value) ? value : null;

        private void Tick()
        {
            if (step < 0)
                return;
            elapsed += TickSeconds;
            if (elapsed > TimeoutSeconds)
            {
                Fail($"timeout at step {step}");
                Finish();
                return;
            }

            try
            {
                RunStep();
            }
            catch (System.Exception ex)
            {
                Fail($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    // The game's entry scene comes first (--new); the map is entered from it, as a modder's scene switch would.
                    var sceneManager = ((SceneTree)Godot.Engine.GetMainLoop()).Root.GetNodeOrNull<SceneManager>("SceneManager");
                    if (!Boot.IsReady || sceneManager == null || string.IsNullOrEmpty(sceneManager.CurrentSceneName) || GodotSceneService.IsTransitioning)
                        return;
                    // The template's opening dialog is still running when the map is entered: the scene switch in the middle
                    // of it must end it (DialogBox releases its locker on a scene exit), or walking stays blocked.
                    var entryBox = RpgInput.FindDialogBox();
                    if (entryBox == null || !entryBox.IsRunning)
                    {
                        if (++waitTicks > 50)
                        {
                            Fail("the template's opening dialog did not start");
                            Finish();
                        }
                        return;
                    }
                    Check(!ModAssemblyLoader.IsModLoaded("turn_battle_base"), "turn_battle_base is not part of this run");
                    Check(ModAssemblyLoader.IsModLoaded("rpg"), "the rpg mod is loaded");
                    sceneManager.SwitchScene(MapId);
                    Next();
                    return;
                case 1:
                    if (!OnMap)
                        return;
                    Check(!RpgInput.FindDialogBox().IsRunning, "the scene switch ended the opening dialog (the box is free)");
                    Check(View.Player.Cell == new Vector2I(1, 2), $"the player starts at the spawn (1,2), got {View.Player.Cell}");
                    Check(View.Find(MapId + "/trigger") != null, "the placement of the map is on the map");
                    Check(View.Session.Map.ExtraFields != null && View.Session.Map.ExtraFields.ContainsKey("Encounters"), "the map keeps its Encounters field");
                    Press(Key.D, true);
                    Next();
                    return;
                case 2:
                    // Two plain steps (onto 2 and 4) first: with the battles integration each would start a battle.
                    if (View.Player.Cell.X < 2)
                        return;
                    Press(Key.D, false);
                    Next();
                    return;
                case 3:
                    if (View.Player.IsMoving || ++waitTicks < 3)
                        return;
                    Check(OnMap && SceneIsMap && View.Player.Cell == new Vector2I(2, 2), $"a plain step onto (2,2) starts no battle, at {View.Player.Cell}");
                    Press(Key.D, true);
                    Next();
                    return;
                case 4:
                    // The step trigger on (3,2) runs its commands; the dialog it opens is advanced with the action key.
                    if (AdvanceDialog())
                        return;
                    if (Flag(TriggerReached) != "1")
                    {
                        if (++waitTicks > 50)
                            Fail("the step trigger on (3,2) does not set its self flag");
                        return;
                    }
                    Check(true, "the step trigger runs its commands and sets its self flag");
                    Next();
                    return;
                case 5:
                    // Walking on (the key is still held): plain steps onto 4 and 5, then the wall on (6,2) stops the player.
                    if (View.Player.Cell.X < 5 || View.Player.IsMoving || ++waitTicks < 5)
                        return;
                    Press(Key.D, false);
                    Check(View.Player.Cell == new Vector2I(5, 2), $"a wall on (6,2) stops the player at (5,2), at {View.Player.Cell}");
                    Check(OnMap && SceneIsMap, "walking over plain steps starts no battle");
                    Finish();
                    return;
            }
        }

        private static string SceneNow() => ((SceneTree)Godot.Engine.GetMainLoop()).Root.GetNodeOrNull<SceneManager>("SceneManager")?.CurrentSceneName;

        private bool AdvanceDialog()
        {
            var box = RpgInput.FindDialogBox();
            if (box == null || !box.IsRunning)
                return false;
            Tap(Key.E);
            return true;
        }

        private static void Press(Key key, bool pressed)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed });
        }

        private static void Tap(Key key)
        {
            Press(key, true);
            Press(key, false);
        }

        private void Next()
        {
            step++;
            waitTicks = 0;
        }

        private void Check(bool condition, string what)
        {
            if (condition)
            {
                passed++;
                GameLogger.Log("TEST", $"[PASS] {what}");
            }
            else
            {
                Fail(what);
            }
        }

        private void Fail(string what)
        {
            failures.Add(what);
            GameLogger.Log("TEST", $"[FAIL] {what}", LogLevel.Error);
        }

        private void Finish()
        {
            step = -1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=rpg_plain_walk passed={passed} failed={failures.Count}");
            ApplicationExit.Quit(GetTree(), failures.Count == 0 ? 0 : 1);
        }
    }
}
