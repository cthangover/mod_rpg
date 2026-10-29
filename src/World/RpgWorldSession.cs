namespace Cthangover.Mods.Rpg
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// One map's simulation, with no scene-tree or presentation dependency. The host
    /// supplies input and time and handles emitted gameplay effects. All renderers
    /// observe these same reserved cells, event pages and interpolated actor positions.
    /// </summary>
    public class RpgWorldSession
    {
        private readonly Dictionary<string, RpgActorState> events = new();
        private readonly Func<string, bool> evaluate;
        private readonly Random random;

        /// <summary>Original map definition, including the encounter table.</summary>
        public RpgMap Map { get; }

        /// <summary>Parsed terrain shared by passability and presentation preparation.</summary>
        public RpgMapGrid Grid { get; }

        /// <summary>Tile rules; null retains the existing walkable-terrain fallback.</summary>
        public RpgTileset Tileset { get; }

        /// <summary>The player remains alive when a presentation is replaced.</summary>
        public RpgActorState Player { get; } = new();

        /// <summary>Event states in resolved file order, including disabled events.</summary>
        public IEnumerable<RpgActorState> Events => events.Values;

        /// <summary>Raised before a touch event or encounter, so the host can save arrival.</summary>
        public event Action<RpgActorState> PlayerStepFinished;

        /// <summary>The host executes a touch event in the existing scenario/dialog system.</summary>
        public event Action<RpgMapEvent> TouchTriggered;

        /// <summary>The host starts a battle on the existing turn-based platform.</summary>
        public event Action<string> EncounterTriggered;

        /// <summary>
        /// Builds a logical world from already resolved data. Supplying a Random makes
        /// route and encounter decisions reproducible without creating a viewport.
        /// </summary>
        public RpgWorldSession(RpgMap map, RpgTileset tileset, IEnumerable<RpgMapEvent> definitions,
            Func<string, bool> evaluate, Random random = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Tileset = tileset;
            Grid = RpgMapGrid.Parse(map);
            this.evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
            this.random = random ?? new Random();
            foreach (var definition in definitions ?? Array.Empty<RpgMapEvent>())
            {
                if (string.IsNullOrEmpty(definition.Id) || events.ContainsKey(definition.Id))
                {
                    GameLogger.Log("RPG", $"map '{map.ID}': event without a unique id at {definition.X},{definition.Y} skipped", LogLevel.Error);
                    continue;
                }
                events[definition.Id] = new RpgActorState(definition);
            }
            RefreshEvents();
        }

        /// <summary>Re-resolves pages while retaining moved cells and restarting changed routes.</summary>
        public void RefreshEvents()
        {
            foreach (var actor in events.Values)
            {
                var index = RpgEventPages.ActiveIndex(actor.Event, evaluate);
                actor.Visible = index >= 0;
                if (index < 0 || index == actor.PageIndex)
                    continue;
                var first = actor.PageIndex == -2;
                actor.PageIndex = index;
                actor.Current = RpgEventPages.Effective(actor.Event, index);
                actor.RouteIndex = 0;
                actor.RouteTimer = actor.Current.Route?.Every ?? 0;
                if (first)
                    actor.Place(new Vector2I(actor.Event.X, actor.Event.Y), actor.Current.Dir);
                else if (index > 0 && actor.Event.Pages[index - 1].Dir.HasValue)
                    actor.Face(actor.Current.Dir);
            }
        }

        /// <summary>Uses terrain overrides and the destinations reserved by visible solid events.</summary>
        public bool CanEnter(Vector2I cell)
        {
            if (!RpgPassability.IsWalkable(Grid, Tileset, cell.X, cell.Y))
                return false;
            foreach (var actor in events.Values)
                if (actor.Visible && actor.Current?.IsSolid == true && actor.Cell == cell)
                    return false;
            return true;
        }

        /// <summary>Looks up an event independently of the presence of a sprite.</summary>
        public RpgActorState FindEvent(string id) => id != null && events.TryGetValue(id, out var actor) ? actor : null;

        /// <summary>Returns the first enabled event of the requested trigger at a logical cell.</summary>
        public RpgActorState EventAt(Vector2I cell, RpgEventTrigger trigger)
        {
            foreach (var actor in events.Values)
                if (actor.Visible && actor.Cell == cell && actor.Current?.Trigger == trigger)
                    return actor;
            return null;
        }

        /// <summary>The same logical target is used for action input and the visual cue.</summary>
        public RpgActorState InteractionTarget()
        {
            if (Player.IsMoving)
                return null;
            return EventAt(Player.Cell + RpgDirections.Delta(Player.Facing), RpgEventTrigger.Action)
                ?? EventAt(Player.Cell, RpgEventTrigger.Action);
        }

        /// <summary>Turns the target toward the player and returns its effective action for the host.</summary>
        public RpgMapEvent PrepareInteraction()
        {
            var target = InteractionTarget();
            if (target == null)
                return null;
            if (target.Cell != Player.Cell)
                target.Face(RpgDirections.Opposite(Player.Facing));
            return target.Current;
        }

        /// <summary>
        /// Starts at most one route/player step per frame, then advances all existing steps.
        /// UI blockers prevent new commands, while already started steps still arrive as before.
        /// Touch events take precedence over random encounters at arrival.
        /// </summary>
        public void Tick(double delta, bool blocked, RpgDirection? heldDirection = null, bool dash = false)
        {
            if (delta < 0 || !double.IsFinite(delta))
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (!blocked)
                WalkRoutes(delta);
            if (!blocked && !Player.IsMoving && heldDirection.HasValue)
            {
                var next = Player.Cell + RpgDirections.Delta(heldDirection.Value);
                if (CanEnter(next))
                {
                    Player.Speed = dash ? 7f : RpgActorState.TilesPerSecond;
                    Player.StepTo(heldDirection.Value);
                }
                else
                    Player.Face(heldDirection.Value);
            }
            foreach (var actor in events.Values)
                actor.Advance(delta);
            if (!Player.Advance(delta))
                return;
            PlayerStepFinished?.Invoke(Player);
            var touch = EventAt(Player.Cell, RpgEventTrigger.Touch);
            if (touch != null)
            {
                TouchTriggered?.Invoke(touch.Current);
                return;
            }
            var enemies = RpgEncounterRoller.Roll(Map.Encounters, random);
            if (enemies != null)
                EncounterTriggered?.Invoke(enemies);
        }

        private void WalkRoutes(double delta)
        {
            foreach (var actor in events.Values)
            {
                var route = actor.Current?.Route;
                if (!actor.Visible || route?.Steps == null || route.Steps.Count == 0 || actor.IsMoving)
                    continue;
                actor.RouteTimer -= delta;
                if (actor.RouteTimer > 0)
                    continue;
                actor.RouteTimer = Math.Max(0.1, route.Every);
                if (actor.RouteIndex >= route.Steps.Count)
                {
                    if (!route.Repeat)
                        continue;
                    actor.RouteIndex = 0;
                }
                var step = RpgRouteStep.Resolve(route.Steps[actor.RouteIndex++], actor.Cell, Player.Cell, random);
                if (!step.Direction.HasValue)
                    continue;
                var next = actor.Cell + RpgDirections.Delta(step.Direction.Value);
                if (step.Moves && CanEnter(next) && next != Player.Cell)
                    actor.StepTo(step.Direction.Value);
                else
                    actor.Face(step.Direction.Value);
            }
        }
    }
}
