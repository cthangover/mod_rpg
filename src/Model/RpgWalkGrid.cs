namespace Cthangover.Mods.Rpg.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Whether the player (or a moving actor) may enter a cell of a v2 map. The answer is layered,
    /// first rule that applies:
    /// <list type="number">
    /// <item>outside the map → blocked;</item>
    /// <item><c>Collision</c> <c>#</c> → blocked;</item>
    /// <item><c>Collision</c> <c>o</c> → open, overriding every object's collision;</item>
    /// <item>a visible dynamic placement solid there → blocked;</item>
    /// <item>a static placement solid there → blocked;</item>
    /// <item>the <c>Structure</c> segment is solid → blocked;</item>
    /// <item>the <c>Ground</c> surface is solid (water) → blocked;</item>
    /// <item>otherwise open.</item>
    /// </list>
    /// Terrain (rules 2, 3, 6, 7) and static placements are folded into one array when the map is
    /// built, so the per-step question is two array reads. Dynamic placements change at run time
    /// (states, <c>Hidden</c>, routes): the runtime re-marks one with <see cref="SetDynamic"/> when it
    /// moves or changes, and the grid keeps a count per cell so overlapping objects stay correct.
    /// </summary>
    public sealed class RpgWalkGrid
    {
        private readonly int _width;
        private readonly int _height;
        private readonly bool[] _blocked;
        private readonly bool[] _forced;
        private readonly int[] _dynamic;
        private readonly Dictionary<string, List<int>> _marks = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Folds the terrain of <paramref name="cells"/> (collision codes, segments, surfaces).</summary>
        public RpgWalkGrid(RpgMapCells cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));
            _width = cells.Width;
            _height = cells.Height;
            _blocked = new bool[_width * _height];
            _forced = new bool[_width * _height];
            _dynamic = new int[_width * _height];
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    var at = y * _width + x;
                    var code = cells.Collision(x, y);
                    if (code == RpgCollision.Block)
                        _blocked[at] = _forced[at] = true;
                    else if (code == RpgCollision.Open)
                        _forced[at] = true;
                    else
                        _blocked[at] = Solid(cells.Structure(x, y)) || Solid(cells.Ground(x, y));
                }
            }
        }

        /// <summary>
        /// Marks the solid footprint cells of a static placement (its effective object, origin
        /// <paramref name="x"/>, <paramref name="y"/>). Cells with a <c>#</c>/<c>o</c> collision code keep
        /// it; cells outside the map are ignored.
        /// </summary>
        public void AddStatic(RpgObject effective, int x, int y)
        {
            if (effective == null)
                return;
            for (var dy = 0; dy < effective.Height; dy++)
            {
                for (var dx = 0; dx < effective.Width; dx++)
                {
                    var at = Index(x + dx, y + dy);
                    if (at >= 0 && !_forced[at] && effective.BlocksAt(dx, dy))
                        _blocked[at] = true;
                }
            }
        }

        /// <summary>
        /// Replaces the marks of the dynamic placement <paramref name="key"/> (its placement id) with the
        /// solid footprint cells of <paramref name="effective"/> — the object with its current state —
        /// at origin <paramref name="x"/>, <paramref name="y"/>. A hidden or null object marks nothing.
        /// </summary>
        public void SetDynamic(string key, RpgObject effective, int x, int y)
        {
            RemoveDynamic(key);
            if (key == null || effective == null)
                return;
            List<int> marks = null;
            for (var dy = 0; dy < effective.Height; dy++)
            {
                for (var dx = 0; dx < effective.Width; dx++)
                {
                    var at = Index(x + dx, y + dy);
                    if (at < 0 || !effective.BlocksAt(dx, dy))
                        continue;
                    _dynamic[at]++;
                    marks ??= new List<int>();
                    marks.Add(at);
                }
            }
            if (marks != null)
                _marks[key] = marks;
        }

        /// <summary>Removes every mark of the dynamic placement <paramref name="key"/>.</summary>
        public void RemoveDynamic(string key)
        {
            if (key == null || !_marks.Remove(key, out var marks))
                return;
            foreach (var at in marks)
                _dynamic[at]--;
        }

        /// <summary>True when the cell can be entered (rules in the class summary).</summary>
        public bool IsWalkable(int x, int y)
        {
            var at = Index(x, y);
            return at >= 0 && !_blocked[at] && (_forced[at] || _dynamic[at] == 0);
        }

        private int Index(int x, int y)
            => x >= 0 && y >= 0 && x < _width && y < _height ? y * _width + x : -1;

        private static bool Solid(RpgObject value)
            => value != null && !value.Hidden && value.IsSolid;
    }
}
