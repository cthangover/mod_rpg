using System;
using System.Collections.Generic;
using Cthangover.Core.Events;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Cthangover.Mods.Rpg.Model;
using Cthangover.Mods.Rpg.Play;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// The place of the RPG world in the 2D shell. The rpg mod patches
    /// <c>base_scene.tscn</c> with this full-screen node right above the view box,
    /// below the dialog canvas and the toolbar, so dialogs, panels and
    /// notifications stay on top of the map.
    /// <para/>
    /// It follows the scene lifecycle: entering a scene whose name is a map id
    /// mounts that map (<see cref = "RpgWorldView"/>) and shows the dark backdrop;
    /// any other scene leaves the host empty and invisible, so a game can mix
    /// picture-and-dialogue scenes and map scenes freely. The shell is rebuilt after battles,
    /// so the host is too; the position survives in <see cref = "RpgState"/>.
    /// <para/>
    /// <see cref = "Current"/> is the host of the live shell, used by teleports
    /// and the tests.
    /// </summary>
    public partial class RpgWorldHost : Control
    {
        private Control backdrop;

        private Control hud;

        private Label mapName;

        private Label prompt;

        private string promptKey;

        /// <summary>Optional behaviors run after simulation and before presentation.</summary>
        public static event Action<RpgWorldHost, double, bool> WorldUpdated;

        /// <summary>Background owners release the target before modal interaction turns it.</summary>
        public static event Action<RpgWorldHost, RpgInstance> InteractionStarting;

        /// <summary>The host in the running shell, or null.</summary>
        public static RpgWorldHost Current { get; private set; }

        /// <summary>The mounted map, or null.</summary>
        public RpgWorldView View { get; private set; }

        /// <summary>Logical world owned by the shell, independently of its current presentation.</summary>
        public RpgWorldSession Session { get; private set; }

        /// <summary>Authoring pause freezes world time without pausing the scene tree or tool controls.</summary>
        public bool SimulationPaused { get; set; }

        /// <summary>Subscribes to the scene lifecycle and condition changes.</summary>
        public override void _EnterTree()
        {
            Current = this;
            ModEventBus.Subscribe<SceneEnteredEvent>(OnSceneEntered);
            ModEventBus.Subscribe<SceneExitedEvent>(OnSceneExited);
            ModEventBus.Subscribe<ConditionStateChangedEvent>(OnConditionsChanged);
            ModEventBus.Subscribe<DialogEndedEvent>(OnDialogEnded);
        }

        /// <summary>Finds the backdrop and starts hidden until a map scene is entered.</summary>
        public override void _Ready()
        {
            GuiInput += OnWorldGuiInput;
            backdrop = GetNodeOrNull<Control>("Backdrop");
            hud = GetNodeOrNull<Control>("Hud");
            mapName = GetNodeOrNull<Label>("Hud/Location/Name");
            prompt = GetNodeOrNull<Label>("Hud/Controls/Prompt");
            Visible = false;
        }

        /// <summary>Updates the action hint from the same target selection as the world.</summary>
        public override void _Process(double delta)
        {
            if (View == null)
            {
                return;
            }

            RefreshSavedObjects();
            // One UI check per frame (plan section 4): it walks panels and windows.
            var walkBlocked = RpgInput.IsWalkBlockedThisFrame(this);
            var simulationBlocked = RpgInput.IsSimulationBlocked(this);
            if (walkBlocked || View.Presentation == null || SimulationPaused)
            {
                Session.ClickWalk.CancelActive();
            }

            Session.Tick(SimulationPaused ? 0 : delta, View.Presentation == null || simulationBlocked || SimulationPaused,
                walkBlocked ? null : RpgInput.HeldDirection(), Input.IsPhysicalKeyPressed(Key.Shift));
            WorldUpdated?.Invoke(this, delta, walkBlocked || View.Presentation == null);
            View.Present(delta, walkBlocked);
            if (hud != null && hud.Visible == walkBlocked)
            {
                hud.Visible = !walkBlocked;
            }

            var key = MovementPromptKey();
            if (prompt != null && key != promptKey)
            {
                promptKey = key;
                prompt.Text = Tr(key);
            }
        }

        /// <summary>A language change re-translates the cached prompt on the next frame.</summary>
        public override void _Notification(int what)
        {
            if (what == NotificationTranslationChanged)
            {
                promptKey = null;
            }
        }

        /// <summary>Shell input owns gameplay; a future SubViewport receives no duplicate walking/actions.</summary>
        public override void _UnhandledInput(InputEvent @event)
        {
            if (View == null)
            {
                return;
            }

            if (TryClickWalk(@event))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!RpgInput.IsActionPress(@event))
            {
                return;
            }

            Session.ClickWalk.Cancel();
            var dialogBox = RpgInput.FindDialogBox();
            if (dialogBox?.IsRunning == true)
            {
                if (!dialogBox.IsWaitingAnswer)
                {
                    dialogBox.NextAction();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (!RpgInput.IsWalkBlocked(this) && View.Interact())
            {
                GetViewport().SetInputAsHandled();
            }
        }

        /// <summary>Unsubscribes; the view goes with the node.</summary>
        public override void _ExitTree()
        {
            GuiInput -= OnWorldGuiInput;
            if (View != null)
            {
                Cthangover.Mods.Time.UiLightController.Instance?.SetLocationMode("cycle");
            }

            ModEventBus.Unsubscribe<SceneEnteredEvent>(OnSceneEntered);
            ModEventBus.Unsubscribe<SceneExitedEvent>(OnSceneExited);
            ModEventBus.Unsubscribe<ConditionStateChangedEvent>(OnConditionsChanged);
            ModEventBus.Unsubscribe<DialogEndedEvent>(OnDialogEnded);
            DetachSession();
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>
        /// Moves the player to a cell. On the mounted map it happens at once;
        /// otherwise the target is stored as the arrival point and the scene
        /// switches (deferred, like <c>switch_scene</c>).
        /// </summary>
        public static void Teleport(RpgTeleport target)
        {
            var host = Current;
            var mapId = string.IsNullOrEmpty(target.Map) ? host?.View?.Map?.ID : target.Map;
            if (string.IsNullOrEmpty(mapId))
            {
                GameLogger.Log("RPG", "teleport without a map while no map is mounted", LogLevel.Error);
                return;
            }

            if (host?.View != null && host.View.Map.ID == mapId)
            {
                host.View.PlacePlayer(target.X, target.Y, target.Dir);
                return;
            }

            RpgState.Instance.Place(mapId, target.X, target.Y, target.Dir);
            var sceneManager = (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.GetNodeOrNull<SceneManager>("SceneManager");
            if (sceneManager == null)
            {
                GameLogger.Log("RPG", $"teleport to '{mapId}': SceneManager not found", LogLevel.Error);
                return;
            }

            Callable.From<string>(sceneManager.SwitchScene).CallDeferred(mapId);
        }

        /// <summary>Mounts the map of the entered scene, if there is one.</summary>
        public void Mount(string sceneName)
        {
            Unmount();
            var map = MapOf(sceneName);
            if (map == null)
            {
                return;
            }

            View = new RpgWorldView();
            AddChild(View);
            MoveChild(View, 1);
            Session = CreateSession(map);
            Session.PlayerStepFinished += OnPlayerStepFinished;
            Session.StepTriggered += OnStepTriggered;
            Session.PlainStepFinished += OnPlainStepFinished;
            Cthangover.Mods.Time.UiLightController.Instance?.SetLocationMode(map.LightMode);
            View.Build(Session);
            if (mapName != null)
            {
                mapName.Text = Tr(map.Name ?? map.ID);
            }

            var state = RpgState.Instance.Data;
            if (state.Map == map.ID && View.Contains(state.X, state.Y))
            {
                View.PlacePlayer(state.X, state.Y, state.Dir);
            }
            else
            {
                View.PlacePlayer(map.Spawn?.X ?? 0, map.Spawn?.Y ?? 0, map.Spawn?.Dir ?? RpgDirection.Down);
            }

            if (backdrop != null)
            {
                backdrop.Visible = true;
            }

            Visible = true;
            GameLogger.Log("RPG", $"map '{map.ID}' mounted, player at {View.Player.Cell}");
        }

        /// <summary>Frees the mounted map and hides the host.</summary>
        public void Unmount()
        {
            if (View != null)
            {
                Cthangover.Mods.Time.UiLightController.Instance?.SetLocationMode("cycle");
            }

            if (View != null)
            {
                DetachSession();
                RemoveChild(View);
                View.QueueFree();
                View = null;
            }

            Visible = false;
        }

        /// <summary>Arrival placement updates the save independently of any presentation.</summary>
        public void PlacePlayer(int x, int y, RpgDirection facing)
        {
            Session.Relocate(new Vector2I(x, y), facing);
            RpgState.Instance.Place(Session.Map.ID, x, y, facing);
            View.Present(0);
        }

        /// <summary>Executes exactly the action selected by the logical session.</summary>
        public bool Interact()
        {
            var selected = Session.InteractionTarget();
            if (selected != null)
            {
                InteractionStarting?.Invoke(this, selected);
            }

            var target = Session.PrepareInteraction();
            View.Present(0);
            return target != null && RpgInteraction.Run(target, Session.Map.ID, RpgInput.FindDialogBox());
        }

        /// <summary>The map entity <paramref name = "id"/> (a logical scene name), or null.</summary>
        public static RpgMap MapOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var maps = Entities.Collect<RpgMap>();
            if (maps.TryGetValue(id, out var map))
            {
                return map;
            }

            foreach (var pair in maps)
            {
                if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>The placements of every mod on map <paramref name = "mapId"/> (by the id prefix), in resolver order.</summary>
        public static List<RpgPlacement> PlacementsOf(string mapId)
        {
            var result = new List<RpgPlacement>();
            foreach (var placement in Entities.Collect<RpgPlacement>().Values)
            {
                if (string.Equals(placement.MapId, mapId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(placement);
                }
            }

            return result;
        }

        /// <summary>Builds the map on resolved objects/placements, with states evaluated in their normal story scopes.</summary>
        public static RpgWorldSession CreateSession(RpgMap map)
        {
            return new(map, new RpgObjectLookup().Find, PlacementsOf(map.ID), ScenarioCondition.Evaluate);
        }

        private void OnSceneEntered(SceneEnteredEvent evt)
        {
            Mount(evt.SceneName);
        }

        private void OnSceneExited(SceneExitedEvent evt)
        {
            Unmount();
        }

        private void OnConditionsChanged(ConditionStateChangedEvent evt)
        {
            View?.RefreshStates();
        }

        // A dialog may have run party.add: the followers change without a condition event.
        private void OnDialogEnded(DialogEndedEvent evt)
        {
            View?.RefreshParty();
        }

        private void OnPlayerStepFinished(RpgActorState player)
        {
            RpgState.Instance.Place(Session.Map.ID, player.Cell.X, player.Cell.Y, player.Facing);
        }

        private void OnStepTriggered(RpgInstance instance)
        {
            RpgInteraction.Run(instance, Session.Map.ID, RpgInput.FindDialogBox());
        }

        private void OnPlainStepFinished(RpgWorldSession session)
        {
            RpgPlainStepListeners.Notify(session);
        }

        private void DetachSession()
        {
            if (Session == null)
            {
                return;
            }

            Session.ClickWalk.Cancel();
            Session.PlayerStepFinished -= OnPlayerStepFinished;
            Session.StepTriggered -= OnStepTriggered;
            Session.PlainStepFinished -= OnPlainStepFinished;
            Session = null;
        }
    }
}
