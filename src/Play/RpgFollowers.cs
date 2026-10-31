namespace Cthangover.Mods.Rpg.Play
{
    using System;
    using System.Collections.Generic;
    using Godot;

    /// <summary>
    /// One follower of the party on the map: its actor (cell, facing, walk phase) and the trail
    /// sequence number of the cell it stands on or walks to.
    /// </summary>
    public sealed class RpgFollower
    {
        internal RpgFollower(int index) => Index = index;

        /// <summary>Place in the party behind the leader: 0 walks the leader's path one step behind.</summary>
        public int Index { get; }

        /// <summary>The follower's actor; <see cref="RpgActorState.Cell"/> is the cell it stands on or walks to.</summary>
        public RpgActorState Actor { get; } = new();

        /// <summary>The cell the follower stands on or walks to (<see cref="RpgActorState.Cell"/>).</summary>
        public Vector2I Cell => Actor.Cell;

        /// <summary>Trail sequence number of <see cref="RpgActorState.Cell"/>.</summary>
        internal long Seq;

        /// <summary>The cell the current step leaves; read only while the actor moves.</summary>
        internal Vector2I Origin;
    }

    /// <summary>
    /// The followers of the party behind the leader (plan 14.H). The leader's path is a trail of the
    /// cells it has arrived at. Follower <c>i</c> walks that trail cell by cell and stays <c>i + 1</c>
    /// steps behind the leader, so followers never leave the walked path and need no walk grid: they
    /// cannot collide with walls, with the leader or with each other.
    /// <para/>
    /// A follower waits on the leader's cell until the trail is long enough for it (so a teleport or a
    /// map load gathers the party on the leader, and it spreads out as the leader walks). Followers are not
    /// solid for the hero: when the hero walks back onto a follower, the follower's next trail target is the
    /// cell the hero has just left, so the two swap places and the party keeps following (no dead end traps
    /// the hero). Only non-player walkers (NPC routes) keep off the followers (<see cref="Occupies"/>).
    /// <para/>
    /// The positions are derived state: nothing here is saved; a session rebuilds them on every mount.
    /// Time comes from the session (<see cref="Tick"/>), never from a renderer.
    /// </summary>
    public sealed class RpgFollowers
    {
        /// <summary>Trail cells kept; far more than a party needs, so a follower never falls off its end.</summary>
        public const int TrailCapacity = 64;

        private readonly List<Vector2I> trail = new();
        private readonly List<RpgFollower> members = new();
        private long baseSeq;

        /// <summary>Followers in party order (index 0 is the nearest to the leader).</summary>
        public IReadOnlyList<RpgFollower> Members => members;

        /// <summary>Number of cells in the trail (the leader's cell included).</summary>
        public int TrailLength => trail.Count;

        /// <summary>Sequence number of the leader's arrival cell (the trail's last entry).</summary>
        public long HeadSeq => baseSeq + trail.Count - 1;

        /// <summary>
        /// Sets the number of followers. A changed count rebuilds the party and gathers it on
        /// <paramref name="leader"/>; the same count keeps everyone where they are.
        /// </summary>
        public void Resize(int count, Vector2I leader, RpgDirection facing)
        {
            count = Math.Max(0, count);
            if (count == members.Count)
                return;
            members.Clear();
            for (var i = 0; i < count; i++)
                members.Add(new RpgFollower(i));
            Gather(leader, facing);
        }

        /// <summary>Forgets the trail and puts every follower on <paramref name="cell"/> (teleport, map load).</summary>
        public void Gather(Vector2I cell, RpgDirection facing)
        {
            trail.Clear();
            trail.Add(cell);
            baseSeq = 0;
            foreach (var member in members)
            {
                member.Actor.Place(cell, facing);
                member.Seq = 0;
                member.Origin = cell;
            }
        }

        /// <summary>The leader arrived on <paramref name="cell"/>, one step from the previous trail cell.</summary>
        public void Arrived(Vector2I cell)
        {
            trail.Add(cell);
            if (trail.Count > TrailCapacity)
            {
                trail.RemoveAt(0);
                baseSeq++;
            }
        }

        /// <summary>True when a follower stands on <paramref name="cell"/> or is leaving it (NPC routes keep off it; the hero walks through).</summary>
        public bool Occupies(Vector2I cell)
        {
            foreach (var member in members)
            {
                if (member.Actor.Cell == cell)
                    return true;
                if (member.Actor.IsMoving && member.Origin == cell)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Advances every follower. A follower that is standing still and is behind its target
        /// (<c>head - (index + 1)</c>, never before the trail start) takes the next trail cell.
        /// </summary>
        public void Tick(double delta, float speed)
        {
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                member.Actor.Advance(delta);
                if (member.Actor.IsMoving)
                    continue;
                if (member.Seq < baseSeq)
                {
                    // Cannot happen with the trail capacity; snaps the follower back onto the trail instead of failing.
                    member.Actor.Place(trail[0], member.Actor.Facing);
                    member.Seq = baseSeq;
                    member.Origin = trail[0];
                    continue;
                }
                var target = Math.Max(baseSeq, HeadSeq - (i + 1));
                if (member.Seq >= target)
                    continue;

                var next = member.Seq + 1;
                var to = trail[(int)(next - baseSeq)];
                var from = member.Actor.Cell;
                var step = to - from;
                member.Seq = next;
                member.Origin = from;
                if (!IsUnit(step))
                {
                    member.Actor.Place(to, member.Actor.Facing);
                    continue;
                }
                member.Actor.Speed = speed;
                member.Actor.StepTo(DirectionOf(step));
            }
        }

        private static bool IsUnit(Vector2I step)
            => Math.Abs(step.X) + Math.Abs(step.Y) == 1;

        private static RpgDirection DirectionOf(Vector2I step)
        {
            if (step.X < 0) return RpgDirection.Left;
            if (step.X > 0) return RpgDirection.Right;
            return step.Y < 0 ? RpgDirection.Up : RpgDirection.Down;
        }
    }
}
