using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;
using Cthangover.Mods.Rpg.Model;
using Cthangover.Mods.TileRender;
using Godot;

namespace Cthangover.Mods.Rpg.Play
{
    /// <summary>
    /// One map's simulation in model v2 (plan section 4, contract 5–8), with no scene-tree or
    /// presentation dependency: the host supplies input and time and runs the emitted effects.
    /// <para>
    /// <b>Load.</b> Every placement is resolved once (<see cref = "RpgObjectResolve.Effective"/>: the
    /// map's or the placement's variant, then the placement fields). A <c>sprite</c> without
    /// <c>Interact</c>/<c>States</c>/<c>Route</c> is static: its footprint goes into the walk grid,
    /// its lights into <see cref = "StaticLights"/> (colours parsed here) and the placement into
    /// <see cref = "Statics"/> for the render scene — nothing about it is looked at per frame. Every
    /// other placement is an <see cref = "RpgInstance"/>.
    /// </para>
    /// <para>
    /// <b>States.</b> <see cref = "RefreshStates"/> evaluates each instance's state conditions in its
    /// own <c>self.</c> scope (the placement id) and, only when the active state changed, applies it,
    /// re-marks the walk grid and re-parses its lights. <c>Hidden</c> takes the instance off the grid,
    /// out of interaction and out of the picture.
    /// </para>
    /// <para>
    /// <b>Interaction.</b> <c>Use</c> fires while the player faces any footprint cell, or stands on a
    /// non-solid one; <c>Step</c> fires on arrival on any footprint cell, so a two-cell exit or a wide
    /// door reacts on both cells. The target selection is shared by the action key and the on-screen
    /// cue (<see cref = "InteractionTarget"/>).
    /// </para>
    /// <para>
    /// <b>Movement.</b> A step reserves its destination at once (the walk grid is re-marked for a
    /// moving instance), routes never walk into the player, and a step finished onto a <c>Step</c>
    /// trigger does not raise <see cref = "PlainStepFinished"/> (as in v1, the step belongs to the trigger).
    /// </para>
    /// </summary>
    public class RpgWorldSession
    {
        private readonly Dictionary<string, RpgInstance> instances = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<RpgInstance> ordered = new();

        private readonly List<(string Id, RpgObject Object, int X, int Y)> statics = new();

        private readonly List<TileLight> staticLights = new();

        private readonly Func<string, string, bool> evaluate;

        private readonly Random random;

        /// <summary>The map definition.</summary>
        public RpgMap Map { get; }

        /// <summary>The map's grids read against the objects (variant applied).</summary>
        public RpgMapCells Cells { get; }

        /// <summary>Walkability: terrain and statics folded at load, instances re-marked when they change or move.</summary>
        public RpgWalkGrid Walk { get; }

        /// <summary>Transient click requests reuse ordinary tick/step/arrival rules; keyboard intent takes priority.</summary>
        public RpgClickWalk ClickWalk { get; }

        /// <summary>The player; survives a change of presentation.</summary>
        public RpgActorState Player { get; } = new();

        /// <summary>The party behind the player (plan 14.H): derived from the leader's trail, never saved.</summary>
        public RpgFollowers Followers { get; } = new();

        private int placeSeen;

        /// <summary>Instances in placement order.</summary>
        public IReadOnlyList<RpgInstance> Instances
        {
            get
            {
                return ordered;
            }
        }

        /// <summary>Static sprites (effective objects at their origin) for the render scene.</summary>
        public IReadOnlyList<(string Id, RpgObject Object, int X, int Y)> Statics
        {
            get
            {
                return statics;
            }
        }

        /// <summary>Lights of the static sprites, in map cells, colours parsed at load.</summary>
        public IReadOnlyList<TileLight> StaticLights
        {
            get
            {
                return staticLights;
            }
        }

        /// <summary>Raised when the player arrives on a cell, before a step trigger or the plain step, so the host can save it.</summary>
        public event Action<RpgActorState> PlayerStepFinished;

        /// <summary>The host runs the interaction of a <c>Step</c> instance the player arrived on.</summary>
        public event Action<RpgInstance> StepTriggered;

        /// <summary>
        /// Raised when the player arrives on a cell with no <c>Step</c> trigger; the random encounters
        /// of the battles integration roll here. The world itself starts nothing.
        /// </summary>
        public event Action<RpgWorldSession> PlainStepFinished;

        /// <summary>
        /// Builds the world. <paramref name = "objects"/> returns an object as the entity resolver gives it
        /// (<c>Extends</c> resolved; null when unknown); <paramref name = "placements"/> are this map's;
        /// <paramref name = "evaluate"/> checks a condition in a scope (<c>ScenarioCondition.Evaluate</c>).
        /// A placement of an unknown object, of a brush object or with a repeated id is an ERROR and
        /// skipped; the rest of the map still loads.
        /// </summary>
        public RpgWorldSession(RpgMap map, Func<string, RpgObject> objects, IEnumerable<RpgPlacement> placements,
            Func<string, string, bool> evaluate, Random random = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            this.evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
            this.random = random ?? new Random();
            Cells = RpgMapCells.Build(map, objects);
            Walk = new RpgWalkGrid(Cells);
            foreach (var placement in placements ?? Array.Empty<RpgPlacement>())
            {
                Load(placement, objects);
            }

            foreach (var mapEvent in map.Events ?? new List<RpgMapEvent>())
            {
                LoadEvent(mapEvent);
            }

            RefreshStates();
            placeSeen = Player.PlaceCount;
            ClickWalk = new RpgClickWalk(this);
        }

        /// <summary>Sets how many followers the party has; a change gathers them on the player.</summary>
        public void SetFollowerCount(int count)
        {
            Followers.Resize(count, Player.Cell, Player.Facing);
        }

        /// <summary>
        /// Puts the player on a cell at once (teleport, map load, arrival placement) and gathers the
        /// followers on it. A direct <see cref = "RpgActorState.Place"/> gathers them at the next tick.
        /// </summary>
        public void Relocate(Vector2I cell, RpgDirection facing)
        {
            ClickWalk.Cancel();
            Player.Place(cell, facing);
            Followers.Gather(cell, facing);
            placeSeen = Player.PlaceCount;
        }

        /// <summary>
        /// Re-evaluates every instance's states (after a story flag changed). Only an instance whose
        /// active state changed is touched: its look, walk marks and lights are replaced, its route
        /// restarts, and a state that names <c>Facing</c> turns it. Position is kept.
        /// </summary>
        public void RefreshStates()
        {
            foreach (var instance in ordered)
            {
                if (instance.IsStatic)
                {
                    continue;
                }

                var id = instance.Id;
                var index = RpgObjectResolve.ActiveState(instance.Effective, evaluate, id);
                if (index == instance.StateIndex)
                {
                    continue;
                }

                var first = instance.StateIndex == -2;
                instance.StateIndex = index;
                instance.Current = index >= 0 ? RpgObjectResolve.WithState(instance.Effective, index) : instance.Effective;
                instance.Lights = RpgObjectLights.Of(instance.Current, Vector2.Zero, id);
                instance.Actor.RouteIndex = 0;
                instance.Actor.RouteTimer = instance.Current.Route?.Every ?? 0;
                if (first || index < 0 || NamesFacing(instance.Effective.States[index]))
                {
                    instance.Actor.Face(instance.Current.Facing);
                }

                if (instance.RouteHidden)
                {
                    Walk.RemoveDynamic(id);
                }
                else
                {
                    Walk.SetDynamic(id, instance.Current, instance.Actor.Cell.X, instance.Actor.Cell.Y);
                }
            }
        }

        /// <summary>Whether the player or a walking instance may enter <paramref name = "cell"/> (contract 8).</summary>
        public bool CanEnter(Vector2I cell)
        {
            return Walk.IsWalkable(cell.X, cell.Y);
        }

        /// <summary>The instance with placement id <paramref name = "id"/>, or null.</summary>
        public RpgInstance Find(string id)
        {
            return id != null && instances.TryGetValue(id, out var instance) ? instance : null;
        }

        /// <summary>First visible instance with this trigger covering the cell, in the runtime's interaction priority order.</summary>
        public RpgInstance At(Vector2I cell, RpgInteractTrigger trigger)
        {
            foreach (var instance in ordered)
            {
                if (instance.Visible && instance.Current.Interact?.Trigger == trigger && instance.Covers(cell))
                {
                    return instance;
                }
            }

            return null;
        }

        /// <summary>
        /// What the action key would use now: a <c>Use</c> instance on the faced cell, else one whose
        /// non-solid footprint cell the player stands on; null while the player walks.
        /// </summary>
        public RpgInstance InteractionTarget()
        {
            if (Player.IsMoving)
            {
                return null;
            }

            var faced = At(Player.Cell + RpgDirections.Delta(Player.Facing), RpgInteractTrigger.Use);
            if (faced != null)
            {
                return faced;
            }

            var here = At(Player.Cell, RpgInteractTrigger.Use);
            return here != null && (!here.BlocksAt(Player.Cell)
                || Cells.Collision(Player.Cell.X, Player.Cell.Y) == RpgCollision.Open) ? here : null;
        }

        /// <summary>The <see cref = "InteractionTarget"/>; an actor in front of the player turns to face them.</summary>
        public RpgInstance PrepareInteraction()
        {
            var target = InteractionTarget();
            if (target != null && target.Current.Form == RpgObjectForm.Actor && !target.Covers(Player.Cell))
            {
                target.Actor.Face(RpgDirections.Opposite(Player.Facing));
            }

            return target;
        }

        /// <summary>
        /// Starts at most one route step per instance and one player step, then advances all steps.
        /// <paramref name = "blocked"/> (a dialog, a panel) stops new commands, while started steps still
        /// arrive. On arrival: <see cref = "PlayerStepFinished"/>, then a <c>Step</c> trigger or else
        /// <see cref = "PlainStepFinished"/>.
        /// </summary>
        public void Tick(double delta, bool blocked, RpgDirection? heldDirection = null, bool dash = false)
        {
            if (delta < 0 || !double.IsFinite(delta))
            {
                throw new ArgumentOutOfRangeException(nameof(delta));
            }

            if (heldDirection.HasValue)
            {
                ClickWalk.Cancel();
            }
            else if (blocked)
            {
                ClickWalk.CancelActive();
            }

            if (!blocked)
            {
                WalkRoutes(delta);
            }

            var direction = heldDirection;
            if (!blocked && !heldDirection.HasValue)
            {
                direction = ClickWalk.Direction();
            }

            if (!blocked && !Player.IsMoving && direction.HasValue)
            {
                // Followers are not solid for the hero: stepping onto one walks through the party (plan 14.H).
                var ahead = Player.Cell + RpgDirections.Delta(direction.Value);
                if (CanEnter(ahead))
                {
                    Player.Speed = dash ? 7f : RpgActorState.TilesPerSecond;
                    Player.StepTo(direction.Value);
                }
                else
                {
                    Player.Face(direction.Value);
                }
            }

            foreach (var instance in ordered)
            {
                instance.Actor.Advance(delta);
            }

            if (Player.PlaceCount != placeSeen)
            {
                placeSeen = Player.PlaceCount;
                Followers.Gather(Player.Cell, Player.Facing);
            }

            Followers.Tick(delta, Player.Speed);
            if (!Player.Advance(delta))
            {
                return;
            }

            // Commit the result before callbacks can open a modal or replace the map.
            ClickWalk.CompletedStep();
            Followers.Arrived(Player.Cell);
            PlayerStepFinished?.Invoke(Player);
            var step = At(Player.Cell, RpgInteractTrigger.Step);
            if (step != null)
            {
                StepTriggered?.Invoke(step);
                return;
            }

            PlainStepFinished?.Invoke(this);
        }

        private void Load(RpgPlacement placement, Func<string, RpgObject> objects)
        {
            if (placement == null || string.IsNullOrEmpty(placement.ID)
                || instances.ContainsKey(placement.ID) || StaticIdTaken(placement.ID))
            {
                GameLogger.Log("RPG", $"map '{Map.ID}': placement without a unique id ('{placement?.ID}') skipped", LogLevel.Error);
                return;
            }

            var resolved = string.IsNullOrEmpty(placement.Object) ? null : objects(placement.Object);
            if (resolved == null || (resolved.Form != RpgObjectForm.Sprite && resolved.Form != RpgObjectForm.Actor))
            {
                var why = resolved == null ? "is unknown"
                    : $"is a {resolved.Form?.ToString().ToLowerInvariant() ?? "formless"} object, not a sprite or an actor";
                GameLogger.Log("RPG", $"map '{Map.ID}': placement '{placement.ID}': object '{placement.Object}' {why}; skipped",
                    LogLevel.Error);
                return;
            }

            var effective = RpgObjectResolve.Effective(resolved, Map.Variant, placement);
            if (effective.IsDynamic || effective.Form == RpgObjectForm.Actor)
            {
                var instance = new RpgInstance(placement, effective, !effective.IsDynamic);
                instances[placement.ID] = instance;
                ordered.Add(instance);
                if (instance.IsStatic)
                {
                    instance.StateIndex = -1;
                    instance.Lights = RpgObjectLights.Of(effective, Vector2.Zero, placement.ID);
                    Walk.AddStatic(effective, placement.X, placement.Y);
                }

                return;
            }

            statics.Add((placement.ID, effective, placement.X, placement.Y));
            Walk.AddStatic(effective, placement.X, placement.Y);
            staticLights.AddRange(RpgObjectLights.Of(effective, new Vector2(placement.X, placement.Y), placement.ID));
        }

        private void LoadEvent(RpgMapEvent mapEvent)
        {
            if (mapEvent == null || string.IsNullOrWhiteSpace(mapEvent.Id) || mapEvent.Id.Contains('/')
                || mapEvent.Width < 1 || mapEvent.Height < 1 || mapEvent.X < 0 || mapEvent.Y < 0
                || mapEvent.Width > Map.Width - mapEvent.X || mapEvent.Height > Map.Height - mapEvent.Y
                || instances.ContainsKey(mapEvent.Scope(Map.ID)) || StaticIdTaken(mapEvent.Scope(Map.ID)))
            {
                GameLogger.Log("RPG", $"map '{Map.ID}': invalid or duplicate event '{mapEvent?.Id}' skipped", LogLevel.Error);
                return;
            }

            var instance = new RpgInstance(mapEvent.AsPlacement(Map.ID), mapEvent.AsObject(), false);
            instances.Add(instance.Id, instance);
            ordered.Add(instance);
        }

        private bool StaticIdTaken(string id)
        {
            foreach (var placed in statics)
            {
                if (string.Equals(placed.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NamesFacing(RpgObjectState state)
        {
            if (state?.Components == null)
            {
                return false;
            }

            foreach (var key in state.Components.Keys)
            {
                if (string.Equals(key, "Facing", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void WalkRoutes(double delta)
        {
            foreach (var instance in ordered)
            {
                var actor = instance.Actor;
                var route = instance.Current.Route;
                var steps = instance.RouteCycle.Steps(route, random);
                if (instance.RouteHeld || instance.Current.Hidden || steps == null || steps.Count == 0 || actor.IsMoving)
                {
                    continue;
                }

                actor.RouteTimer -= delta;
                if (actor.RouteTimer > 0)
                {
                    continue;
                }

                actor.RouteTimer = Math.Max(0.1, route.Every);
                if (actor.RouteIndex >= steps.Count)
                {
                    if (!route.Repeat)
                    {
                        continue;
                    }

                    actor.RouteIndex = 0;
                    instance.RouteCycle.Next(random);
                    steps = instance.RouteCycle.Steps(route, random);
                }

                var instruction = steps[actor.RouteIndex++].Trim().ToLowerInvariant();
                if (instruction == "hide")
                {
                    instance.RouteHidden = true;
                    Walk.RemoveDynamic(instance.Id);
                    continue;
                }

                if (instruction == "reset" && instance.RouteHidden)
                {
                    actor.Place(new Vector2I(instance.Placement.X, instance.Placement.Y), instance.Current.Facing);
                    continue;
                }

                if (instruction == "show")
                {
                    if (!instance.RouteHidden)
                    {
                        continue;
                    }

                    if (actor.Cell == Player.Cell || Followers.Occupies(actor.Cell) || !CanEnter(actor.Cell))
                    {
                        actor.RouteIndex--;
                        continue;
                    }

                    instance.RouteHidden = false;
                    Walk.SetDynamic(instance.Id, instance.Current, actor.Cell.X, actor.Cell.Y);
                    continue;
                }

                if (instruction.StartsWith("wait:") && double.TryParse(instruction.Substring(5), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds))
                {
                    actor.RouteTimer = Math.Clamp(seconds, 0.1, 600);
                    continue;
                }

                if (instance.RouteHidden)
                {
                    continue;
                }

                var step = RpgRouteStep.Resolve(instruction, actor.Cell, Player.Cell, random);
                if (!step.Direction.HasValue)
                {
                    continue;
                }

                var next = actor.Cell + RpgDirections.Delta(step.Direction.Value);
                if (step.Moves && next != Player.Cell && !Followers.Occupies(next) && CanEnterAs(instance, next))
                {
                    actor.Speed = float.IsFinite(route.Speed) ? Math.Clamp(route.Speed, 1f, 12f) : RpgActorState.TilesPerSecond;
                    actor.StepTo(step.Direction.Value);
                    Walk.SetDynamic(instance.Id, instance.Current, actor.Cell.X, actor.Cell.Y);
                }
                else
                {
                    actor.Face(step.Direction.Value);
                    if (step.Moves && route.RetryBlocked)
                    {
                        actor.RouteIndex--;
                    }
                }
            }
        }

        // Borrow the same self-mask subtraction without changing occupancy or its navigation revision.
        private bool CanEnterAs(RpgInstance instance, Vector2I origin)
        {
            var free = true;
            for (var dy = 0; dy < instance.Current.Height && free; dy++)
            {
                for (var dx = 0; dx < instance.Current.Width && free; dx++)
                {
                    if (instance.Current.BlocksAt(dx, dy)
                        && !Walk.IsWalkableIgnoring(instance.Id, origin.X + dx, origin.Y + dy))
                    {
                        free = false;
                    }
                }
            }

            if (!instance.Current.IsSolid && !Walk.IsWalkableIgnoring(instance.Id, origin.X, origin.Y))
            {
                free = false;
            }

            return free;
        }
    }
}
