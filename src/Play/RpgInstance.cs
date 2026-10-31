namespace Cthangover.Mods.Rpg.Play
{
    using System.Collections.Generic;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>
    /// One placement on the mounted map at run time: what the session knows about an object that
    /// may change — the effective object (variant and placement fields applied, states still
    /// listed), the look and behaviour of its active state, and where it stands.
    /// <para>
    /// Every <c>actor</c> placement is an instance, even one without behaviour: its sheet row
    /// depends on the projection, so it is presented as a pose (contract 6, notes R3). Such a
    /// <see cref="IsStatic"/> actor is folded into the walk grid like scenery (rule 5) and never
    /// changes. Static <c>sprite</c>s are not instances at all — they are baked into the render
    /// scene (<see cref="RpgWorldSession.Statics"/>).
    /// </para>
    /// <para>
    /// The instance id is the placement id, which is also the <c>self.</c> scope of its
    /// conditions and of the dialog its interaction starts.
    /// </para>
    /// </summary>
    public sealed class RpgInstance
    {
        internal RpgInstance(RpgPlacement placement, RpgObject effective, bool isStatic)
        {
            Placement = placement;
            Effective = effective;
            Current = effective;
            IsStatic = isStatic;
            Actor.Place(new Vector2I(placement.X, placement.Y), effective.Facing);
        }

        /// <summary>The placement as written.</summary>
        public RpgPlacement Placement { get; }

        /// <summary>Placement id: the instance identity and the <c>self.</c> scope.</summary>
        public string Id => Placement.ID;

        /// <summary>The object with the map's (or the placement's) variant and the placement fields merged in; states not applied.</summary>
        public RpgObject Effective { get; }

        /// <summary><see cref="Effective"/> with the active state applied — what is drawn, blocks and reacts now.</summary>
        public RpgObject Current { get; internal set; }

        /// <summary>Index of the active state in <see cref="RpgObject.States"/>; -1 none; -2 not evaluated yet.</summary>
        public int StateIndex { get; internal set; } = -2;

        /// <summary>A behaviour-less actor: walk grid like scenery, never re-evaluated.</summary>
        public bool IsStatic { get; }

        /// <summary>Position and movement; <see cref="RpgActorState.Cell"/> is the object origin (top-left footprint cell).</summary>
        public RpgActorState Actor { get; } = new();

        /// <summary>Drawn, solid and interactive: not <see cref="RpgObject.Hidden"/> in the current state.</summary>
        public bool Visible => Current != null && !Current.Hidden;

        /// <summary>Lights of the current state, positions relative to the origin, colours already parsed.</summary>
        public IReadOnlyList<TileLight> Lights { get; internal set; } = new List<TileLight>();

        /// <summary>Whether <paramref name="cell"/> is one of the current footprint cells.</summary>
        public bool Covers(Vector2I cell)
        {
            var d = cell - Actor.Cell;
            return Current != null && d.X >= 0 && d.Y >= 0 && d.X < Current.Width && d.Y < Current.Height;
        }

        /// <summary>Whether the current footprint mask blocks <paramref name="cell"/> (false when hidden or outside).</summary>
        public bool BlocksAt(Vector2I cell)
        {
            var d = cell - Actor.Cell;
            return Current != null && Current.BlocksAt(d.X, d.Y);
        }
    }
}
