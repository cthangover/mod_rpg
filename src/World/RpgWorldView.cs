namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Games;
    using Cthangover.Core.Events;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// Compatibility facade and RPG-to-renderer adapter. The host owns simulation;
    /// implementation mods own all visible geometry. A renderer can be replaced while
    /// a step is in progress without rebuilding this session or rerolling encounters.
    /// </summary>
    public partial class RpgWorldView : Node2D
    {
        private readonly Dictionary<string, RpgActorState> eventActors = new();
        private readonly Dictionary<string, RpgMapEvent> shownPages = new();
        private readonly Dictionary<string, TileActorPose> poses = new();
        private readonly RpgTextures textures = new();
        private readonly TileFrame frame = new();
        private TileRenderScene scene;
        private RpgRenderSceneBuilder builder;
        private RpgLighting lighting;
        public RpgWorldSession Session { get; private set; }
        public RpgMap Map => Session.Map;
        public RpgTileset Tileset => Session.Tileset;
        public RpgActorState Player => Session.Player;
        public int TileSize => Tileset?.TileSize ?? 32;
        public ITileWorldPresentation Presentation { get; private set; }
        public string RendererId { get; private set; }

        public override void _EnterTree()
        {
            TileRendererRegistry.Instance.Removing += OnRendererRemoving;
            ModEventBus.Subscribe<ModSetReloadedEvent>(OnModsReloaded);
        }
        public override void _ExitTree()
        {
            TileRendererRegistry.Instance.Removing -= OnRendererRemoving;
            ModEventBus.Unsubscribe<ModSetReloadedEvent>(OnModsReloaded);
            Presentation?.Dispose();
            Presentation = null;
            scene?.Release();
            scene = null;
            builder?.Dispose();
            textures.Dispose();
        }

        public void Build(RpgWorldSession session)
        {
            Session = session;
            Name = "World";
            builder = new RpgRenderSceneBuilder(textures);
            scene = builder.Build(Map, session.Grid, Tileset);
            lighting = new RpgLighting(Map, session.Grid, Tileset);
            foreach (var state in session.Events) eventActors[state.Event.Id] = state;
            UpdateFrame();
            var id = ActiveGame.GetSetting("tile.renderer");
            foreach (var arg in OS.GetCmdlineUserArgs())
                if (arg.StartsWith("--tile-renderer=")) id = arg.Substring(16);
            SetRenderer(id);
        }

        /// <summary>Transactional replacement: a failed candidate leaves the current presentation alive.</summary>
        public bool SetRenderer(string id)
        {
            ITileWorldPresentation candidate = null;
            try
            {
                var factory = TileRendererRegistry.Instance.Resolve(id);
                candidate = factory.Create();
                AddChild(candidate.Root);
                candidate.Root.Visible = false;
                candidate.Mount(scene);
                candidate.Resize(GetParent() is Control host ? host.Size : GetViewportRect().Size);
                candidate.Present(frame, 0);
                Presentation?.Dispose();
                Presentation = candidate;
                RendererId = factory.Id;
                Presentation.Root.Visible = true;
                return true;
            }
            catch (Exception ex)
            {
                candidate?.Dispose();
                GameLogger.Log("RPG", ex.Message, LogLevel.Error);
                return false;
            }
        }

        private void OnRendererRemoving(string id)
        {
            if (RendererId != id) return;
            Presentation?.Dispose();
            Presentation = null;
        }

        private void OnModsReloaded(ModSetReloadedEvent evt)
        {
            if (evt.Applied && Presentation == null && scene != null) SetRenderer(RendererId);
        }

        public void PlacePlayer(int x, int y, RpgDirection facing)
        {
            if (GetParent() is RpgWorldHost host) host.PlacePlayer(x, y, facing);
            else { Session.Player.Place(new Vector2I(x, y), facing); Present(0); }
        }
        public void RefreshEvents() { Session.RefreshEvents(); Present(0); }
        public bool Contains(int x, int y) => Session.Grid.InBounds(x, y);
        public bool CanEnter(Vector2I cell) => Session.CanEnter(cell);
        public RpgActorState FindEvent(string id) => id != null && eventActors.TryGetValue(id, out var actor) ? actor : null;
        public RpgActorState EventAt(Vector2I cell, RpgEventTrigger trigger) => FindEvent(Session.EventAt(cell, trigger)?.Event.Id);
        public RpgActorState InteractionTarget() => FindEvent(Session.InteractionTarget()?.Event.Id);
        public bool Interact() => GetParent() is RpgWorldHost host && host.Interact();

        public void Present(double delta)
        {
            UpdateFrame();
            if (Presentation == null) return;
            Presentation.Resize(GetParent() is Control host ? host.Size : GetViewportRect().Size);
            Presentation.Present(frame, delta);
        }

        private void UpdateFrame()
        {
            frame.Focus = Session.Player.Position + Vector2.One / 2;
            frame.InteractionCell = RpgInput.IsWalkBlocked(GetParent()) ? null : Session.InteractionTarget()?.Cell;
            FillActor("player", Session.Player, true);
            foreach (var actor in eventActors.Values)
            {
                FillActor("event/" + actor.Event.Id, actor, false);
            }
            lighting.Fill(frame, Session);
        }

        private void FillActor(string id, RpgActorState state, bool player)
        {
            if (!poses.TryGetValue(id, out var pose))
            {
                pose = new TileActorPose { Id = id };
                poses[id] = pose;
                frame.Actors.Add(pose);
            }
            if (pose.Look == null || !shownPages.TryGetValue(id, out var page) || page != state.Current)
            {
                pose.Look = builder.ActorLook(state, Tileset, player);
                shownPages[id] = state.Current;
            }
            pose.Position = state.Position;
            pose.Visible = state.Visible;
            pose.Charset = player || !string.IsNullOrEmpty(state.Current?.Sprite);
            pose.Facing = (int)state.Facing;
            var cycle = (int)state.AnimationTime % 4;
            pose.Column = !state.IsMoving ? 1 : cycle == 3 ? 1 : cycle;
        }
    }
}
