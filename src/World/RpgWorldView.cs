namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Events;
    using Cthangover.Core.Games;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Play;
    using Cthangover.Mods.Rpg.Render;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// RPG-to-renderer adapter of the mounted map. The host owns the simulation
    /// (<see cref="RpgWorldSession"/>); implementation mods own all visible geometry. The static
    /// scene (ground, structure, static sprites) is built once per map; every frame only poses
    /// move: the player, actors, and the parts of dynamic sprites. A pose's look is rebuilt only
    /// when its instance's active state changed. A renderer can be replaced while a step is in
    /// progress without rebuilding the session or rerolling encounters.
    /// </summary>
    public partial class RpgWorldView : Node2D
    {
        private const string PlayerPose = "player";

        private readonly Dictionary<string, List<TileActorPose>> instancePoses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RpgObject> shownLooks = new(StringComparer.OrdinalIgnoreCase);
        private readonly RpgTextures textures = new();
        private readonly TileFrame frame = new() { CameraOnArtwork = true };
        private readonly Dictionary<string, TileActorPose> followerPoses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TileVisual> followerLooks = new(StringComparer.Ordinal);
        private List<(string Id, string Charset)> party = new();
        private TileActorPose player;
        private string playerCharset;
        private ImageTexture placeholder;
        private TileRenderScene scene;
        private RpgRenderSceneBuilder builder;
        private RpgWorldLights lights;

        /// <summary>The simulated world.</summary>
        public RpgWorldSession Session { get; private set; }

        /// <summary>The mounted map.</summary>
        public RpgMap Map => Session.Map;

        /// <summary>The player's state.</summary>
        public RpgActorState Player => Session.Player;

        /// <summary>Pixels per cell of the map's pictures.</summary>
        public int TileSize => Map.TileSize > 0 ? Map.TileSize : 32;

        /// <summary>The live renderer, or null.</summary>
        public ITileWorldPresentation Presentation { get; private set; }

        /// <summary>Id of the live renderer.</summary>
        public string RendererId { get; private set; }

        /// <summary>The static scene of the map as last built.</summary>
        public TileRenderScene Scene => scene;

        /// <inheritdoc />
        public override void _EnterTree()
        {
            TileRendererRegistry.Instance.Removing += OnRendererRemoving;
            ModEventBus.Subscribe<ModSetReloadedEvent>(OnModsReloaded);
        }

        /// <inheritdoc />
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
            placeholder?.Dispose();
            placeholder = null;
        }

        /// <summary>Builds the static scene of the session's map and mounts the configured renderer.</summary>
        public void Build(RpgWorldSession session)
        {
            Session = session;
            Name = "World";
            builder = new RpgRenderSceneBuilder(textures.Image, textures.Charset);
            scene = builder.Build(Map, session.Cells, session.Statics);
            lights = new RpgWorldLights(session);
            playerCharset = RpgPlayerLook.LeaderCharset();
            party = RpgPartyLook.Followers();
            Session.SetFollowerCount(party.Count);
            UpdateFrame(RpgInput.IsWalkBlockedThisFrame(GetParent()));
            var id = ActiveGame.GetSetting("tile.renderer");
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--tile-renderer=", StringComparison.Ordinal))
                    id = arg.Substring(16);
            }
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

        /// <summary>Places the player through the host (saving the arrival), or directly without one.</summary>
        public void PlacePlayer(int x, int y, RpgDirection facing)
        {
            if (GetParent() is RpgWorldHost host)
            {
                host.PlacePlayer(x, y, facing);
            }
            else
            {
                Session.Relocate(new Vector2I(x, y), facing);
                Present(0);
            }
        }

        /// <summary>Re-evaluates the instances' states (a story flag changed) and shows the result.</summary>
        public void RefreshStates()
        {
            Session.RefreshStates();
            // The party may have changed with the story (a class chosen, a leader replaced).
            var charset = RpgPlayerLook.LeaderCharset();
            if (charset != playerCharset && player != null)
                player.Look = null;
            playerCharset = charset;
            ReloadParty();
            Present(0);
        }

        /// <summary>A dialog ended and may have changed the party (<c>party.add</c>): the followers follow the new roster.</summary>
        public void RefreshParty()
        {
            if (Session != null && ReloadParty())
                Present(0);
        }

        // Re-reads the followers; true when the party changed (the followers were then regathered on the leader).
        private bool ReloadParty()
        {
            var next = RpgPartyLook.Followers();
            if (RpgPartyLook.Same(next, party))
                return false;
            party = next;
            Session.SetFollowerCount(party.Count);
            foreach (var pose in followerPoses.Values)
                frame.Actors.Remove(pose);
            followerPoses.Clear();
            return true;
        }

        /// <summary>Whether the cell lies on the map.</summary>
        public bool Contains(int x, int y) => Session.Cells.InBounds(x, y);

        /// <summary>Whether the player may enter the cell.</summary>
        public bool CanEnter(Vector2I cell) => Session.CanEnter(cell);

        /// <summary>The instance with this placement id, or null.</summary>
        public RpgInstance Find(string id) => Session.Find(id);

        /// <summary>What the action key would use now.</summary>
        public RpgInstance InteractionTarget() => Session.InteractionTarget();

        /// <summary>Runs the interaction target through the host.</summary>
        public bool Interact() => GetParent() is RpgWorldHost host && host.Interact();

        /// <summary>Presents a frame, checking the UI blockers itself.</summary>
        public void Present(double delta) => Present(delta, RpgInput.IsWalkBlockedThisFrame(GetParent()));

        /// <summary>Presents a frame; <paramref name="walkBlocked"/> is this frame's UI check, done once by the host.</summary>
        public void Present(double delta, bool walkBlocked)
        {
            UpdateFrame(walkBlocked);
            if (Presentation == null)
                return;
            Presentation.Resize(GetParent() is Control host ? host.Size : GetViewportRect().Size);
            Presentation.Present(frame, delta);
        }

        private void OnRendererRemoving(string id)
        {
            if (RendererId != id)
                return;
            Presentation?.Dispose();
            Presentation = null;
        }

        private void OnModsReloaded(ModSetReloadedEvent evt)
        {
            if (evt.Applied && Presentation == null && scene != null)
                SetRenderer(RendererId);
        }

        private void UpdateFrame(bool walkBlocked)
        {
            frame.Focus = Session.Player.Position + Vector2.One / 2;
            var target = walkBlocked ? null : Session.InteractionTarget();
            frame.InteractionCell = target == null ? null : Cue(target);
            FillPlayer();
            FillFollowers();
            foreach (var instance in Session.Instances)
                FillInstance(instance);
            lights.Fill(frame);
        }

        // The cue marks the target's footprint cell the player faces (or stands on).
        private Vector2I Cue(RpgInstance target)
        {
            var faced = Session.Player.Cell + RpgDirections.Delta(Session.Player.Facing);
            return target.Covers(faced) ? faced : Session.Player.Cell;
        }

        private void FillPlayer()
        {
            if (player == null)
            {
                player = new TileActorPose { Id = PlayerPose, Charset = true };
                frame.Actors.Add(player);
            }
            if (player.Look == null)
            {
                var charset = playerCharset;
                var look = new RpgObject { ID = PlayerPose, Form = RpgObjectForm.Actor, Charset = charset };
                player.Look = (charset == null ? null : builder.ActorLook(look, TileSize)) ?? Placeholder();
            }
            player.Position = Session.Player.Position;
            player.Visible = true;
            player.Facing = (int)Session.Player.Facing;
            player.Column = Column(Session.Player, player.Look);
        }

        // The followers' poses: each one is its party member's walking sheet on the follower's own trail position.
        // A follower without a sheet is not drawn (its ERROR is logged by RpgPartyLook).
        private void FillFollowers()
        {
            var members = Session.Followers.Members;
            for (var i = 0; i < party.Count && i < members.Count; i++)
            {
                var (id, charset) = party[i];
                var member = members[i];
                if (!followerPoses.TryGetValue(id, out var pose))
                {
                    pose = new TileActorPose { Id = "follower/" + id, Charset = true };
                    followerPoses[id] = pose;
                    frame.Actors.Add(pose);
                }
                pose.Look = FollowerLook(charset);
                pose.Visible = pose.Look != null;
                pose.Position = member.Actor.Position;
                pose.Facing = (int)member.Actor.Facing;
                pose.Column = Column(member.Actor, pose.Look);
            }
        }

        private TileVisual FollowerLook(string charset)
        {
            if (charset == null)
                return null;
            if (!followerLooks.TryGetValue(charset, out var look))
            {
                look = builder.ActorLook(new RpgObject { ID = "follower/" + charset, Form = RpgObjectForm.Actor, Charset = charset }, TileSize);
                followerLooks[charset] = look;
            }
            return look;
        }

        private void FillInstance(RpgInstance instance)
        {
            if (!instancePoses.TryGetValue(instance.Id, out var poses))
            {
                poses = new List<TileActorPose>();
                instancePoses[instance.Id] = poses;
            }
            var actor = instance.Current.Form == RpgObjectForm.Actor;
            if (!shownLooks.TryGetValue(instance.Id, out var shown) || shown != instance.Current)
            {
                shownLooks[instance.Id] = instance.Current;
                var looks = new List<TileVisual>();
                if (actor)
                {
                    var look = !instance.Current.Hidden ? builder.ActorLook(instance.Current, TileSize) : null;
                    if (look != null)
                        looks.Add(look);
                }
                else
                {
                    looks.AddRange(builder.PartLooks(instance.Current, TileSize));
                }
                for (var i = poses.Count; i < looks.Count; i++)
                {
                    var pose = new TileActorPose { Id = "instance/" + instance.Id + "#" + i };
                    poses.Add(pose);
                    frame.Actors.Add(pose);
                }
                for (var i = 0; i < poses.Count; i++)
                {
                    poses[i].Look = i < looks.Count ? looks[i] : null;
                    poses[i].Charset = actor;
                }
            }
            foreach (var pose in poses)
            {
                pose.Position = instance.Actor.Position;
                pose.Visible = instance.Visible && pose.Look != null;
                pose.Facing = (int)instance.Actor.Facing;
                pose.Column = actor && !instance.Actor.IsMoving
                    ? instance.Current.Column == TileWalkSheet.IdleColumn
                        ? TileWalkSheet.Animation(pose.Look?.Texture).IdleColumn : instance.Current.Column
                    : Column(instance.Actor, pose.Look);
            }
        }

        private static int Column(RpgActorState state, TileVisual look)
            => TileWalkSheet.Animation(look?.Texture).Column(state.AnimationTime, state.IsMoving);

        // The visible stand-in for a leader without a walking sheet (an ERROR is already logged).
        private TileVisual Placeholder()
        {
            if (placeholder == null)
            {
                using var image = Image.CreateEmpty(3, 4, false, Image.Format.Rgba8);
                image.Fill(new Color(1, 0, 1));
                placeholder = ImageTexture.CreateFromImage(image);
            }
            return new TileVisual
            {
                Id = PlayerPose, Texture = placeholder, Upright = true,
                Bounds = new Rect2(0.25f, 0.25f, 0.5f, 0.75f), Anchor = new Vector2(0.5f, 1),
                Source = new Rect2(0, 0, 1, 1),
            };
        }
    }
}
