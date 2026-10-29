namespace Cthangover.Mods.Rpg
{
    using Cthangover.Core.Events;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// The place of the RPG world in the 2D shell. The rpg mod patches
    /// <c>base_scene.tscn</c> with this full-screen node right above the view box,
    /// below the dialog canvas and the toolbar, so dialogs, panels and
    /// notifications stay on top of the map.
    /// <para/>
    /// It follows the scene lifecycle: entering a scene whose name is a map id
    /// mounts that map (<see cref="RpgWorldView"/>) and shows the dark backdrop;
    /// any other scene leaves the host empty and invisible, so a game can mix
    /// picture-and-dialogue scenes and map scenes freely. The shell is rebuilt after battles,
    /// so the host is too; the position survives in <see cref="RpgState"/>.
    /// <para/>
    /// <see cref="Current"/> is the host of the live shell, used by teleports
    /// and the tests.
    /// </summary>
    public partial class RpgWorldHost : Control
    {
        private Control backdrop;
        private Control hud;
        private Label mapName;
        private Label prompt;

        /// <summary>The host in the running shell, or null.</summary>
        public static RpgWorldHost Current { get; private set; }

        /// <summary>The mounted map, or null.</summary>
        public RpgWorldView View { get; private set; }

        /// <summary>Logical world owned by the shell, independently of its current presentation.</summary>
        public RpgWorldSession Session { get; private set; }

        /// <summary>Subscribes to the scene lifecycle and condition changes.</summary>
        public override void _EnterTree()
        {
            Current = this;
            ModEventBus.Subscribe<SceneEnteredEvent>(OnSceneEntered);
            ModEventBus.Subscribe<SceneExitedEvent>(OnSceneExited);
            ModEventBus.Subscribe<ConditionStateChangedEvent>(OnConditionsChanged);
        }

        /// <summary>Finds the backdrop and starts hidden until a map scene is entered.</summary>
        public override void _Ready()
        {
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
                return;
            var blocked = View.Presentation == null || RpgInput.IsWalkBlocked(this);
            Session.Tick(delta, blocked, RpgInput.HeldDirection(), Input.IsPhysicalKeyPressed(Key.Shift));
            View.Present(delta);
            if (hud != null)
                hud.Visible = !RpgInput.IsWalkBlocked(this);
            if (prompt != null)
                prompt.Text = Tr(View.Presentation == null ? "rpg/ui/no_renderer" : View.InteractionTarget() != null ? "rpg/ui/interact" : "rpg/ui/walk");
        }

        /// <summary>Shell input owns gameplay; a future SubViewport receives no duplicate walking/actions.</summary>
        public override void _UnhandledInput(InputEvent @event)
        {
            if (View == null || !RpgInput.IsActionPress(@event))
                return;
            var dialogBox = RpgInput.FindDialogBox();
            if (dialogBox?.IsRunning == true)
            {
                if (!dialogBox.IsWaitingAnswer)
                    dialogBox.NextAction();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (!RpgInput.IsWalkBlocked(this) && View.Interact())
                GetViewport().SetInputAsHandled();
        }

        /// <summary>Unsubscribes; the view goes with the node.</summary>
        public override void _ExitTree()
        {
            ModEventBus.Unsubscribe<SceneEnteredEvent>(OnSceneEntered);
            ModEventBus.Unsubscribe<SceneExitedEvent>(OnSceneExited);
            ModEventBus.Unsubscribe<ConditionStateChangedEvent>(OnConditionsChanged);
            DetachSession();
            if (Current == this)
                Current = null;
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
            Callable.From(() => sceneManager.SwitchScene(mapId)).CallDeferred();
        }

        /// <summary>Mounts the map of the entered scene, if there is one.</summary>
        public void Mount(string sceneName)
        {
            Unmount();
            var map = RpgMapFactory.Instance.Get(sceneName);
            if (map == null)
                return;

            View = new RpgWorldView();
            AddChild(View);
            MoveChild(View, 1);
            Session = new RpgWorldSession(map, RpgTilesetFactory.Instance.ForMap(map),
                RpgMapEvents.Of(map.ID), ScenarioCondition.Evaluate);
            Session.PlayerStepFinished += OnPlayerStepFinished;
            Session.TouchTriggered += OnTouchTriggered;
            Session.EncounterTriggered += OnEncounterTriggered;
            View.Build(Session);
            if (mapName != null)
                mapName.Text = Tr(map.Name ?? map.ID);

            var state = RpgState.Instance.Data;
            if (state.Map == map.ID && View.Contains(state.X, state.Y))
                View.PlacePlayer(state.X, state.Y, state.Dir);
            else
                View.PlacePlayer(map.Spawn?.X ?? 0, map.Spawn?.Y ?? 0, map.Spawn?.Dir ?? RpgDirection.Down);

            if (backdrop != null)
                backdrop.Visible = true;
            Visible = true;
            GameLogger.Log("RPG", $"map '{map.ID}' mounted, player at {View.Player.Cell}");
        }

        /// <summary>Frees the mounted map and hides the host.</summary>
        public void Unmount()
        {
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
            Session.Player.Place(new Vector2I(x, y), facing);
            RpgState.Instance.Place(Session.Map.ID, x, y, facing);
            View.Present(0);
        }

        /// <summary>Executes exactly the action selected by the logical session.</summary>
        public bool Interact()
        {
            var target = Session.PrepareInteraction();
            View.Present(0);
            return target != null && RpgEventRunner.Run(target, Session.Map.ID);
        }

        private void OnSceneEntered(SceneEnteredEvent evt) => Mount(evt.SceneName);

        private void OnSceneExited(SceneExitedEvent evt) => Unmount();

        private void OnConditionsChanged(ConditionStateChangedEvent evt)
        {
            View?.RefreshEvents();
        }

        private void OnPlayerStepFinished(RpgActorState player)
            => RpgState.Instance.Place(Session.Map.ID, player.Cell.X, player.Cell.Y, player.Facing);

        private void OnTouchTriggered(RpgMapEvent mapEvent) => RpgEventRunner.Run(mapEvent, Session.Map.ID);

        private void OnEncounterTriggered(string enemies) => RpgBattleService.Start(enemies, null);

        private void DetachSession()
        {
            if (Session == null)
                return;
            Session.PlayerStepFinished -= OnPlayerStepFinished;
            Session.TouchTriggered -= OnTouchTriggered;
            Session.EncounterTriggered -= OnEncounterTriggered;
            Session = null;
        }
    }
}
