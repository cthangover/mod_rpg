using System;
using System.Collections.Generic;
using Godot;

namespace Cthangover.Mods.Rpg.Play
{
    /// <summary>
    /// Session-owned transient cardinal navigation. A* uses deterministic Manhattan priorities,
    /// at most 128 frontier removals per tick and an explicit total attempt budget. Occupancy changes
    /// invalidate the snapshot; only ordinary Tick owns steps, follower trails and arrival effects.
    /// Nothing here is serialized or rendered, and failed searches never substitute direct motion.
    /// </summary>
    public sealed class RpgClickWalk
    {
        private static readonly RpgDirection[] Directions =
        {
            RpgDirection.Up,
            RpgDirection.Right,
            RpgDirection.Down,
            RpgDirection.Left,
        };

        private readonly RpgWorldSession session;

        private readonly Dictionary<Vector2I, (Vector2I Parent, int Cost)> records = new();

        private readonly PriorityQueue<(Vector2I Cell, int Cost), (long Score, int Depth, long Tie)> pending = new();

        private IReadOnlyList<Vector2I> path = Array.Empty<Vector2I>();

        private Vector2I origin;

        private long revision;

        private long tie;

        private int placement;

        private int budget;

        private int next;

        /// <summary>Current outcome; terminal failures contain no executable path.</summary>
        public RpgClickWalkStatus Status { get; private set; }

        /// <summary>Last requested coordinate, retained for terminal feedback; cancellation clears it.</summary>
        public Vector2I? Goal { get; private set; }

        /// <summary>Immutable executable path including start/goal; empty before verification and after any terminal outcome.</summary>
        public IReadOnlyList<Vector2I> Path => path;

        /// <summary>Frontier removals in the current occupancy attempt, bounded by the request's total budget.</summary>
        public int ExpandedNodes { get; private set; }

        internal RpgClickWalk(RpgWorldSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// Replaces the transient goal. Invalid/blocked goals fail immediately; search advances on ticks.
        /// The budget must be 1..16,384 and applies separately to each occupancy attempt. A request made
        /// during a step starts from its reserved destination after that step finishes.
        /// </summary>
        public RpgClickWalkStatus Request(Vector2I goal, int nodeBudget = 16384)
        {
            if (nodeBudget < 1 || nodeBudget > 16384)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeBudget));
            }

            Goal = goal;
            budget = nodeBudget;
            placement = session.Player.PlaceCount;
            StartSearch();
            return Status;
        }

        /// <summary>Cancels future intent without teleporting or interrupting a step already started by the session.</summary>
        public void Cancel()
        {
            if (Status == RpgClickWalkStatus.Idle && Goal == null)
            {
                return;
            }

            Goal = null;
            Finish(RpgClickWalkStatus.Cancelled);
        }

        internal void CancelActive()
        {
            if (Status is RpgClickWalkStatus.Searching or RpgClickWalkStatus.Walking)
            {
                Cancel();
            }
        }

        internal void CompletedStep()
        {
            if (Goal == session.Player.Cell && (Status is RpgClickWalkStatus.Searching or RpgClickWalkStatus.Walking))
            {
                Finish(RpgClickWalkStatus.Arrived);
            }
        }

        internal RpgDirection? Direction()
        {
            if (Status is not (RpgClickWalkStatus.Searching or RpgClickWalkStatus.Walking))
            {
                return null;
            }

            if (session.Player.PlaceCount != placement)
            {
                Cancel();
                return null;
            }

            if (session.Player.IsMoving)
            {
                return null;
            }

            if (session.Player.Cell == Goal)
            {
                Finish(RpgClickWalkStatus.Arrived);
                return null;
            }

            if (revision != session.Walk.Revision)
            {
                StartSearch();
            }

            if (Status == RpgClickWalkStatus.Searching)
            {
                SearchSlice();
            }

            if (Status != RpgClickWalkStatus.Walking)
            {
                return null;
            }

            while (next < path.Count && path[next] == session.Player.Cell)
            {
                next++;
            }

            if (next >= path.Count)
            {
                StartSearch();
                return null;
            }

            var cell = path[next];
            var delta = cell - session.Player.Cell;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) != 1 || !session.CanEnter(cell))
            {
                StartSearch();
                return null;
            }

            return delta.X > 0 ? RpgDirection.Right : delta.X < 0 ? RpgDirection.Left
                : delta.Y > 0 ? RpgDirection.Down : RpgDirection.Up;
        }

        private void StartSearch()
        {
            Finish(RpgClickWalkStatus.Searching);
            ExpandedNodes = 0;
            revision = session.Walk.Revision;
            origin = session.Player.Cell;
            var goal = Goal.Value;
            if (!session.Cells.InBounds(goal.X, goal.Y))
            {
                Finish(RpgClickWalkStatus.InvalidGoal);
                return;
            }

            if (!session.CanEnter(goal))
            {
                Finish(RpgClickWalkStatus.BlockedGoal);
                return;
            }

            if (origin == goal && !session.Player.IsMoving)
            {
                Finish(RpgClickWalkStatus.Arrived);
                return;
            }

            records[origin] = (origin, 0);
            tie = 0;
            Enqueue(origin, 0);
        }

        private void SearchSlice()
        {
            for (var spent = 0; spent < 128 && pending.Count > 0; spent++)
            {
                if (ExpandedNodes >= budget)
                {
                    Finish(RpgClickWalkStatus.BudgetExhausted);
                    return;
                }

                var current = pending.Dequeue();
                ExpandedNodes++;
                if (records[current.Cell].Cost != current.Cost)
                {
                    continue;
                }

                if (current.Cell == Goal)
                {
                    BuildPath(current.Cell);
                    return;
                }

                foreach (var direction in Directions)
                {
                    var cell = current.Cell + RpgDirections.Delta(direction);
                    if (!session.Cells.InBounds(cell.X, cell.Y) || !session.CanEnter(cell))
                    {
                        continue;
                    }

                    var cost = current.Cost + 1;
                    if (records.TryGetValue(cell, out var previous) && previous.Cost <= cost)
                    {
                        continue;
                    }

                    records[cell] = (current.Cell, cost);
                    Enqueue(cell, cost);
                }
            }

            if (pending.Count == 0)
            {
                Finish(RpgClickWalkStatus.Unreachable);
            }
            else if (ExpandedNodes >= budget)
            {
                Finish(RpgClickWalkStatus.BudgetExhausted);
            }
        }

        private void Enqueue(Vector2I cell, int cost)
        {
            var delta = Goal.Value - cell;
            var distance = Math.Abs((long)delta.X) + Math.Abs((long)delta.Y);
            pending.Enqueue((cell, cost), (distance + cost, -cost, tie++));
        }

        private void BuildPath(Vector2I goal)
        {
            var cells = new List<Vector2I>();
            for (var cell = goal; ; cell = records[cell].Parent)
            {
                cells.Add(cell);
                if (cell == origin)
                {
                    break;
                }
            }

            cells.Reverse();
            path = cells.AsReadOnly();
            next = 1;
            pending.Clear();
            records.Clear();
            Status = RpgClickWalkStatus.Walking;
        }

        private void Finish(RpgClickWalkStatus status)
        {
            pending.Clear();
            records.Clear();
            path = Array.Empty<Vector2I>();
            next = 0;
            Status = status;
        }
    }
}
