namespace Cthangover.Mods.Rpg
{
    using System;
    using Godot;

    /// <summary>
    /// An actor in cell coordinates, independent of nodes and textures. The destination
    /// is reserved at step start; renderers interpolate Position without owning the clock.
    /// This transient state is rebuilt on map entry, while RpgState owns saved position.
    /// </summary>
    public class RpgActorState
    {
        /// <summary>Normal walking speed in cells per second.</summary>
        public const float TilesPerSecond = 4.5f;

        private Vector2 from;
        private float progress = 1f;

        /// <summary>Definition and all its pages, or null for the player.</summary>
        public RpgMapEvent Event { get; }

        /// <summary>Effective event page, resolved by the world session.</summary>
        public RpgMapEvent Current { get; internal set; }

        /// <summary>Active page index; -2 means no page has yet been resolved.</summary>
        public int PageIndex { get; internal set; } = -2;

        /// <summary>Whether conditions currently enable this event in the world.</summary>
        public bool Visible { get; internal set; } = true;

        /// <summary>Route bookkeeping belongs to the simulation, never to a sprite.</summary>
        public int RouteIndex { get; internal set; }

        /// <summary>Seconds until another route instruction may start.</summary>
        public double RouteTimer { get; internal set; }

        /// <summary>Reserved cell, updated immediately when a step starts.</summary>
        public Vector2I Cell { get; private set; }

        /// <summary>Direction in map axes, independent of camera orientation.</summary>
        public RpgDirection Facing { get; private set; }

        /// <summary>Duration uses cells per second, not projected pixel distance.</summary>
        public float Speed { get; set; } = TilesPerSecond;

        /// <summary>True until the current step reaches its reserved destination.</summary>
        public bool IsMoving => progress < 1f;

        /// <summary>Continuous top-left cell position for any projection.</summary>
        public Vector2 Position => from.Lerp(new Vector2(Cell.X, Cell.Y), progress);

        /// <summary>Continuous walk phase shared by every presentation.</summary>
        public float AnimationTime { get; private set; }

        /// <summary>Creates transient state; no Godot object is allocated.</summary>
        public RpgActorState(RpgMapEvent mapEvent = null)
        {
            Event = mapEvent;
        }

        /// <summary>Teleportation stops an unfinished step without reporting its completion.</summary>
        public void Place(Vector2I cell, RpgDirection facing)
        {
            Cell = cell;
            from = new Vector2(cell.X, cell.Y);
            Facing = facing;
            progress = 1f;
        }

        /// <summary>A blocked movement command can still turn the actor.</summary>
        public void Face(RpgDirection facing) => Facing = facing;

        /// <summary>Reserves a neighbour; the session checks passability before calling.</summary>
        public void StepTo(RpgDirection direction)
        {
            if (IsMoving)
                return;
            Facing = direction;
            from = new Vector2(Cell.X, Cell.Y);
            Cell += RpgDirections.Delta(direction);
            progress = 0f;
        }

        /// <summary>
        /// Advances an existing step and returns true exactly once at arrival.
        /// Renderers must never call this: the session is the sole time source.
        /// </summary>
        public bool Advance(double delta)
        {
            if (delta < 0 || !double.IsFinite(delta))
                throw new ArgumentOutOfRangeException(nameof(delta));
            if (!IsMoving || delta <= 0)
                return false;
            var speed = Math.Clamp(Speed, 1f, 12f);
            progress = Math.Min(1f, progress + (float)delta * speed);
            AnimationTime += (float)delta * speed * 2f;
            return !IsMoving;
        }
    }
}
