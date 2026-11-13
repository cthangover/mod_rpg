namespace Cthangover.Mods.Rpg.PartyWalkTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Games;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.View;
    using Cthangover.Mods.Rpg.Play;
    using Godot;

    /// <summary>
    /// Plays the rpg_plus village with a companion (plan 14.H) like a player: the villager is talked to
    /// through <c>View.Interact</c>, the dialog is clicked through, then the hero walks with real key events.
    /// Steps are queued; a step returns true when it is done, a failed check is recorded and the run goes on.
    /// <para/>
    /// Result line: <c>[TEST_RESULT] name=rpg_party_walk passed=N failed=M</c>, exit code 0/1, 150 s watchdog.
    /// The suite runs in both projections (the manifest passes <c>--tile-renderer=isometric</c> for the second).
    /// </summary>
    public partial class RpgPartyWalkTestDriver : Node
    {
        private const float TickSeconds = 0.1f;
        private const float TimeoutSeconds = 150f;
        private const string Village = "rpg_village";
        private const string House = "rpg_elder_house";
        private const string Companion = "rpg_companion";
        private const string NoCharset = "rpg_party_test_nocharset";
        private const string Villager = "rpg_village/villager";
        private const string JoinedFlag = "rpg_companion_joined";
        private const string ErrorText = "has no 'Charset'";

        private static readonly Vector2I VillagerNeighbour = new(9, 11);
        private static readonly Vector2I WalkStart = new(20, 20);
        private static readonly Vector2I TeleportCell = new(25, 22);

        private readonly string shotsDir;
        private readonly List<string> failures = new();
        private readonly Queue<Func<bool>> steps = new();
        private readonly List<Vector2I> arrivals = new();
        private readonly List<string> charsetErrors = new();
        private RpgWorldSession attached;
        private Vector2I gatherCell;
        private Key reverseKey;
        private int reverseFrom;
        private int passed;
        private double elapsed;
        private bool done;
        private ulong idleWaitStarted;
        private ulong idleFrame;

        /// <summary>Creates the driver; <paramref name="shotsDir"/> null disables screenshots.</summary>
        public RpgPartyWalkTestDriver(string shotsDir)
        {
            this.shotsDir = shotsDir;
            Name = "RpgPartyWalkTestDriver";
            BuildSteps();
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private static RpgWorldView View => RpgWorldHost.Current?.View;

        private static CharacterData Roster => GameData.Instance.Runtime.CharacterData;

        private static string Flag(string name) => GameData.Instance.Runtime.Flags.TryGetValue(name, out var value) ? value : null;

        private static RpgFollower Follower0 => View?.Session?.Followers.Members.FirstOrDefault();

        private void BuildSteps()
        {
            // Boot: the prologue is clicked through, the village intro (party.add rpg_hero) too.
            Until(() => Boot.IsReady && !SkipPrologue() && View?.Map?.ID == Village, "the village is up", 900);
            Until(() => Roster.Characters.ContainsKey("rpg_hero"), "the hero joins the party", 600);
            ClickDialogs(600, "the intro dialog ends");
            Do(() => Check(RpgPartyLook.Followers().Count == 0, "the hero starts with no followers"));
            Do(() => Check(!Roster.BattleSet.Contains(Companion), "the companion is not in the party yet"));

            // Joining: the villager asks; the first answer ("ask") makes the companion join.
            Do(() => View.PlacePlayer(VillagerNeighbour.X, VillagerNeighbour.Y, RpgDirection.Up));
            Until(() => View.Player.Cell == VillagerNeighbour && !View.Player.IsMoving, "the hero stands next to the villager", 40);
            Do(() => Check(View.Find(Villager)?.Visible == true, "the villager is on the map before joining"));
            Do(() => Check(View.Interact(), "the villager's interaction starts"));
            ClickDialogs(900, "the villager's dialog ends");
            Do(() => Check(Flag(JoinedFlag) == "1", "the villager's scenario set the joined flag"));
            Do(() => Check(Roster.BattleSet.Contains(Companion), "party.add put the companion into the battle set"));
            Until(() => View.Find(Villager)?.Visible == false, "the villager leaves the map once joined", 40);
            Do(() => Check(View.Session.Followers.Members.Count == 1, $"one follower walks behind the hero, got {View.Session.Followers.Members.Count}"));
            Do(() => Check(RpgPartyLook.Followers().Count == 1 && RpgPartyLook.Followers()[0].Charset == "village/villager", "the companion has the villager's walking sheet"));

            // Trail: the hero walks right, then down; the follower stays one step behind on the walked path.
            Do(() => View.PlacePlayer(WalkStart.X, WalkStart.Y, RpgDirection.Right));
            Do(() => { arrivals.Clear(); gatherCell = WalkStart; });
            Do(() => Check(Follower0?.Cell == WalkStart, "the follower gathers on the hero when the hero is placed"));
            Hold(Key.D, () => arrivals.Count >= 3, "three steps right");
            WaitForPartyIdle();
            Do(() => CheckTrail("after three steps right"));
            Hold(Key.S, () => arrivals.Count >= 5, "two steps down");
            WaitForPartyIdle();
            Do(() => CheckTrail("after the turn down"));
            Do(() => Shot("trail"));

            // Followers are not solid for the hero: walking back onto the follower walks through the party,
            // and the follower steps aside onto the hero's previous cell (the two swap places).
            Do(() =>
            {
                reverseKey = KeyFor(Follower0.Cell - View.Player.Cell);
                reverseFrom = arrivals.Count;
                Press(reverseKey, true);
            });
            Until(() => arrivals.Count >= reverseFrom + 2, "the hero walks back through the follower", 120);
            Do(() => Press(reverseKey, false));
            WaitForPartyIdle();
            Do(() => CheckTrail("after walking back through the follower"));
            Do(() => Check(arrivals.Count >= reverseFrom + 2, "the hero passed the follower's cell"));

            // Teleport on the same map: the party gathers on the hero at once.
            Do(() => View.PlacePlayer(TeleportCell.X, TeleportCell.Y, RpgDirection.Left));
            Do(() => Check(Follower0?.Cell == TeleportCell, $"a teleport gathers the follower on the hero, at {Follower0.Cell}"));
            Do(() => Check(Follower0 != null && !Follower0.Actor.IsMoving, "the gathered follower stands still"));

            // Map load: the elder house is a different map; the party is gathered on its arrival cell, then
            // back in the village on the door cell.
            Do(() => RpgWorldHost.Teleport(new RpgTeleport { Map = House, X = 6, Y = 8, Dir = RpgDirection.Up }));
            Until(() => View?.Map?.ID == House, "the elder house is mounted", 120);
            ClickDialogs(300, "the elder house's entry text ends");
            Do(() => Check(View.Session.Followers.Members.Count == 1 && Follower0?.Cell == View.Player.Cell,
                $"a map load gathers the follower on the hero, follower {Follower0?.Cell}, hero {View.Player.Cell}"));
            Do(() => RpgWorldHost.Teleport(new RpgTeleport { Map = Village, X = 6, Y = 7, Dir = RpgDirection.Down }));
            Until(() => View?.Map?.ID == Village, "the village is mounted again", 120);
            ClickDialogs(300, "the village text ends");
            Do(() => Check(Follower0?.Cell == View.Player.Cell, "the way back gathers the follower on the hero"));

            // A follower without a walking sheet: an ERROR is logged, the party goes on without a crash.
            Do(() => GameLogger.LogEmitted += OnLog);
            Do(() => Check(Roster.AddCharacterToParty(NoCharset), "the test character without a sheet joins the party"));
            Do(() => View.RefreshParty());
            Do(() => Check(View.Session.Followers.Members.Count == 2, $"two followers now, got {View.Session.Followers.Members.Count}"));
            Do(() => Check(charsetErrors.Count == 1, $"an ERROR names the follower without a Charset once, got {charsetErrors.Count}"));
            Do(() => Check(RpgPartyLook.Followers().Any(f => f.Id == NoCharset && f.Charset == null), "the follower without a sheet is kept without one"));
            Do(() => { arrivals.Clear(); gatherCell = View.Player.Cell; });
            Hold(Key.D, () => arrivals.Count >= 2, "a step right with two followers");
            WaitForPartyIdle();
            Do(() => CheckTrail("with the follower without a sheet"));
            Do(() => Shot("charset_less"));
            Do(() => GameLogger.LogEmitted -= OnLog);
        }

        /// <summary>One queued step per tick; a step returns true once it is done.</summary>
        private void Tick()
        {
            if (done)
                return;
            elapsed += TickSeconds;
            if (elapsed > TimeoutSeconds)
            {
                Fail($"timeout, {steps.Count} step(s) left");
                Finish();
                return;
            }
            try
            {
                AttachSession();
                if (steps.Count == 0)
                {
                    Finish();
                    return;
                }
                if (steps.Peek()())
                    steps.Dequeue();
            }
            catch (Exception ex)
            {
                Fail($"{ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        // Records every arrival of the mounted session: the trail the followers must walk.
        private void AttachSession()
        {
            var session = RpgWorldHost.Current?.Session;
            if (session == attached)
                return;
            if (attached != null)
                attached.PlayerStepFinished -= OnArrival;
            attached = session;
            arrivals.Clear();
            if (attached != null)
                attached.PlayerStepFinished += OnArrival;
        }

        private void OnArrival(RpgActorState actor) => arrivals.Add(actor.Cell);

        private void OnLog(string category, string message, LogLevel level)
        {
            if (level == LogLevel.Error && message.Contains(ErrorText, StringComparison.Ordinal))
                charsetErrors.Add(message);
        }

        private void Do(Action action) => steps.Enqueue(() => { action(); return true; });

        private void Until(Func<bool> condition, string what, int maxTicks)
        {
            var ticks = 0;
            steps.Enqueue(() =>
            {
                if (condition())
                    return true;
                if (++ticks < maxTicks)
                    return false;
                Fail("timed out: " + what);
                return true;
            });
        }

        // Key release does not cancel the current step. Wait for the hero and every follower,
        // then allow two actual simulation frames before the unchanged trail assertions.
        private void WaitForPartyIdle() => steps.Enqueue(PartyIsIdle);

        private bool PartyIsIdle()
        {
            var now = Time.GetTicksMsec();
            if (idleWaitStarted == 0)
                idleWaitStarted = now;
            if (now - idleWaitStarted > 10000)
                throw new TimeoutException("hero/followers did not finish moving after key release");
            if (View?.Player.IsMoving != false || View.Session.Followers.Members.Any(FollowerIsMoving))
            {
                idleFrame = 0;
                return false;
            }
            if (idleFrame == 0)
                idleFrame = Engine.GetProcessFrames();
            if (Engine.GetProcessFrames() - idleFrame < 2)
                return false;
            idleWaitStarted = idleFrame = 0;
            return true;
        }

        private static bool FollowerIsMoving(RpgFollower follower) => follower.Actor.IsMoving;

        // Holds a key until the condition holds, then releases it.
        private void Hold(Key key, Func<bool> until, string what)
        {
            Do(() => Press(key, true));
            Until(until, what, 200);
            Do(() => Press(key, false));
        }

        // Clicks through a running dialog: the first answer of a choice, else the action key.
        private void ClickDialogs(int maxTicks, string what)
        {
            var ticks = 0;
            steps.Enqueue(() =>
            {
                var box = RpgInput.FindDialogBox();
                if (box == null || !box.IsRunning)
                    return true;
                if (++ticks >= maxTicks)
                {
                    Fail("timed out: " + what);
                    return true;
                }
                if (box.IsWaitingAnswer)
                    box.TrySelectVariant(0);
                else
                    Tap(Key.E);
                return false;
            });
        }

        private static bool BoxRunning => RpgInput.FindDialogBox()?.IsRunning == true;

        // The follower with index 0 must stand on the cell the hero reached one arrival before its last one
        // (or on the gathering cell before any arrival) and be still.
        private void CheckTrail(string when)
        {
            var follower = Follower0;
            var hero = View.Player.Cell;
            var expected = arrivals.Count >= 2 ? arrivals[^2] : gatherCell;
            Check(follower != null, $"a follower exists {when}");
            if (follower == null)
                return;
            Check(!follower.Actor.IsMoving, $"the follower has arrived {when}");
            Check(follower.Cell == expected, $"the follower walks the hero's trail {when}: at {follower.Cell}, expected {expected}");
            var gap = hero - follower.Cell;
            Check(arrivals.Count == 0 || gap.X * gap.X + gap.Y * gap.Y == 1 || follower.Cell == hero,
                $"the follower stands next to the hero {when}, gap {gap}");
        }

        private static Key KeyFor(Vector2I delta)
        {
            if (delta.X < 0) return Key.A;
            if (delta.X > 0) return Key.D;
            return delta.Y < 0 ? Key.W : Key.S;
        }

        private static RpgDirection DirectionOf(Vector2I delta)
        {
            if (delta.X < 0) return RpgDirection.Left;
            if (delta.X > 0) return RpgDirection.Right;
            return delta.Y < 0 ? RpgDirection.Up : RpgDirection.Down;
        }

        // Advances the shell's dialog-free path: the prologue's choices take their first answer.
        private static bool SkipPrologue()
        {
            var scene = ((SceneTree)Engine.GetMainLoop()).Root.GetNodeOrNull<SceneManager>("SceneManager")?.CurrentSceneName;
            if (string.IsNullOrEmpty(scene) || RpgWorldHost.Current?.View?.Map != null)
                return false;
            var box = RpgInput.FindDialogBox();
            if (box == null || !box.IsRunning)
                return false;
            if (box.IsWaitingAnswer)
                box.TrySelectVariant(0);
            else
                box.NextAction();
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

        private void Shot(string name)
        {
            if (string.IsNullOrEmpty(shotsDir) || DisplayServer.GetName() == "headless")
                return;
            DirAccess.MakeDirRecursiveAbsolute(shotsDir);
            ViewportCapture.SavePng(GetViewport(), shotsDir.TrimEnd('/') + "/rpg_party_" + name + ".png");
        }

        private void Finish()
        {
            done = true;
            Godot.Engine.TimeScale = 1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=rpg_party_walk passed={passed} failed={failures.Count}");
            Cthangover.Core.Bootstrap.ApplicationExit.Quit(GetTree(), failures.Count == 0 ? 0 : 1);
        }
    }
}
