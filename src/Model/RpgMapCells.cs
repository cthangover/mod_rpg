namespace Cthangover.Mods.Rpg.Model
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The parsed grids of an <see cref="RpgMap"/>: per cell the effective surface, the effective
    /// segment (the map's <c>Variant</c> already applied) and the collision code, after the rows
    /// and then every <see cref="RpgStroke"/> in order. This is what the renderer and the walk grid
    /// read; the map file keeps text rows.
    /// <para>
    /// Building is forgiving, because maps are edited by hand and patched by other mods: a short
    /// row leaves empty cells, a character missing from the legend, an unknown object or an object
    /// of the wrong form in a grid leaves the cell empty — each is reported once (per grid and
    /// character or stroke) in <see cref="Errors"/> and as an ERROR log, and the map still loads.
    /// The grid always has the map's declared size.
    /// </para>
    /// </summary>
    public sealed class RpgMapCells
    {
        private readonly RpgObject[] _ground;
        private readonly RpgObject[] _structure;
        private readonly char[] _collision;
        private readonly List<string> _errors = new();

        private RpgMapCells(string mapId, int width, int height)
        {
            MapId = mapId;
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            _ground = new RpgObject[Width * Height];
            _structure = new RpgObject[Width * Height];
            _collision = new char[Width * Height];
            Array.Fill(_collision, RpgCollision.Auto);
        }

        /// <summary>Id of the map the cells belong to.</summary>
        public string MapId { get; }

        /// <summary>Columns.</summary>
        public int Width { get; }

        /// <summary>Rows.</summary>
        public int Height { get; }

        /// <summary>Authoring errors found while building, in the order met.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>True when the cell lies on the map.</summary>
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Effective surface object of a cell; null for empty or outside.</summary>
        public RpgObject Ground(int x, int y) => InBounds(x, y) ? _ground[y * Width + x] : null;

        /// <summary>Effective segment object of a cell; null for empty or outside.</summary>
        public RpgObject Structure(int x, int y) => InBounds(x, y) ? _structure[y * Width + x] : null;

        /// <summary>Collision code (<see cref="RpgCollision"/>); outside the map it is <see cref="RpgCollision.Block"/>.</summary>
        public char Collision(int x, int y) => InBounds(x, y) ? _collision[y * Width + x] : RpgCollision.Block;

        /// <summary>
        /// Reads <paramref name="map"/>'s grids. <paramref name="objects"/> returns an object as the entity
        /// resolver gives it (null when unknown); the map's variant is applied here, once per object.
        /// </summary>
        public static RpgMapCells Build(RpgMap map, Func<string, RpgObject> objects)
        {
            var cells = new RpgMapCells(map?.ID, map?.Width ?? 1, map?.Height ?? 1);
            if (map == null)
                return cells;
            if (map.Width < 1 || map.Height < 1)
                cells.Error($"size {map.Width}×{map.Height} is not a map; read as {cells.Width}×{cells.Height}");

            var effective = new Dictionary<string, RpgObject>(StringComparer.OrdinalIgnoreCase);
            var legend = cells.ReadLegend(map, objects, effective);
            cells.ReadRows(RpgStrokeLayer.Ground, map.Ground, legend);
            cells.ReadRows(RpgStrokeLayer.Structure, map.Structure, legend);
            cells.ReadCollisionRows(map.Collision);

            var index = 0;
            foreach (var stroke in map.Strokes ?? new List<RpgStroke>())
            {
                if (stroke != null)
                    cells.ApplyStroke(stroke, index, map.Variant, objects, effective);
                index++;
            }
            return cells;
        }

        private Dictionary<char, RpgObject> ReadLegend(RpgMap map, Func<string, RpgObject> objects, Dictionary<string, RpgObject> effective)
        {
            var legend = new Dictionary<char, RpgObject>();
            if (map.Legend == null)
                return legend;
            foreach (var pair in map.Legend)
            {
                if (pair.Key == null || pair.Key.Length != 1 || pair.Key[0] == RpgCollision.Auto || char.IsWhiteSpace(pair.Key[0]))
                {
                    Error($"Legend: '{pair.Key}' is not a cell character (one character, not '.' or whitespace)");
                    continue;
                }
                var value = Effective(pair.Value, map.Variant, objects, effective);
                if (value == null)
                    Error($"Legend: '{pair.Key}' → unknown object '{pair.Value}'");
                else
                    legend[pair.Key[0]] = value;
            }
            return legend;
        }

        private void ReadRows(RpgStrokeLayer layer, List<string> rows, Dictionary<char, RpgObject> legend)
        {
            if (rows == null || rows.Count == 0)
                return;
            CheckShape(layer, rows);
            var target = layer == RpgStrokeLayer.Ground ? _ground : _structure;
            var reported = new HashSet<char>();
            for (var y = 0; y < Height && y < rows.Count; y++)
            {
                var row = rows[y] ?? string.Empty;
                for (var x = 0; x < Width && x < row.Length; x++)
                {
                    var c = row[x];
                    if (c == RpgCollision.Auto)
                        continue;
                    if (!legend.TryGetValue(c, out var value))
                    {
                        if (reported.Add(c))
                            Error($"{layer}: '{c}' (first at {x},{y}) is not in the Legend; cell left empty");
                        continue;
                    }
                    if (!Fits(layer, value))
                    {
                        if (reported.Add(c))
                            Error($"{layer}: '{c}' (first at {x},{y}) is '{value.ID}', a {FormName(value)}, but {layer} takes only {Expected(layer)} objects; cell left empty");
                        continue;
                    }
                    target[y * Width + x] = value;
                }
            }
        }

        private void ReadCollisionRows(List<string> rows)
        {
            if (rows == null || rows.Count == 0)
                return;
            CheckShape(RpgStrokeLayer.Collision, rows);
            var reported = new HashSet<char>();
            for (var y = 0; y < Height && y < rows.Count; y++)
            {
                var row = rows[y] ?? string.Empty;
                for (var x = 0; x < Width && x < row.Length; x++)
                {
                    if (IsCollisionCode(row[x]))
                        _collision[y * Width + x] = row[x];
                    else if (reported.Add(row[x]))
                        Error($"Collision: '{row[x]}' (first at {x},{y}) is not '.', '#' or 'o'; read as '.'");
                }
            }
        }

        private void ApplyStroke(RpgStroke stroke, int index, string variant, Func<string, RpgObject> objects, Dictionary<string, RpgObject> effective)
        {
            var name = $"Strokes[{stroke.Id ?? index.ToString()}]";
            RpgObject value = null;
            var code = RpgCollision.Auto;
            if (stroke.Layer == RpgStrokeLayer.Collision)
            {
                if (string.IsNullOrEmpty(stroke.Code) || stroke.Code.Length != 1 || !IsCollisionCode(stroke.Code[0]))
                {
                    Error($"{name}: Collision code '{stroke.Code}' is not '.', '#' or 'o'; stroke skipped");
                    return;
                }
                code = stroke.Code[0];
            }
            else if (!string.IsNullOrEmpty(stroke.Object))
            {
                value = Effective(stroke.Object, variant, objects, effective);
                if (value == null)
                {
                    Error($"{name}: unknown object '{stroke.Object}'; stroke skipped");
                    return;
                }
                if (!Fits(stroke.Layer, value))
                {
                    Error($"{name}: '{value.ID}' is a {FormName(value)}, but {stroke.Layer} takes only {Expected(stroke.Layer)} objects; stroke skipped");
                    return;
                }
            }

            foreach (var rect in stroke.Rects ?? new List<int[]>())
            {
                if (rect == null || rect.Length != 4)
                {
                    Error($"{name}: a rect is not [x, y, w, h]; skipped");
                    continue;
                }
                for (var y = rect[1]; y < rect[1] + rect[3]; y++)
                {
                    for (var x = rect[0]; x < rect[0] + rect[2]; x++)
                        Paint(stroke.Layer, x, y, value, code);
                }
            }
            foreach (var cell in stroke.Cells ?? new List<int[]>())
            {
                if (cell == null || cell.Length != 2)
                {
                    Error($"{name}: a cell is not [x, y]; skipped");
                    continue;
                }
                Paint(stroke.Layer, cell[0], cell[1], value, code);
            }
        }

        private void Paint(RpgStrokeLayer layer, int x, int y, RpgObject value, char code)
        {
            if (!InBounds(x, y))
                return;
            var at = y * Width + x;
            switch (layer)
            {
                case RpgStrokeLayer.Ground:
                    _ground[at] = value;
                    break;
                case RpgStrokeLayer.Structure:
                    _structure[at] = value;
                    break;
                default:
                    _collision[at] = code;
                    break;
            }
        }

        private void CheckShape(RpgStrokeLayer layer, List<string> rows)
        {
            if (rows.Count != Height)
                Error($"{layer}: {rows.Count} rows, the map has {Height}");
            for (var y = 0; y < rows.Count && y < Height; y++)
            {
                var length = rows[y]?.Length ?? 0;
                if (length != Width)
                    Error($"{layer}: row {y} has {length} cells, the map has {Width}");
            }
        }

        private static RpgObject Effective(string id, string variant, Func<string, RpgObject> objects, Dictionary<string, RpgObject> effective)
        {
            if (string.IsNullOrEmpty(id) || objects == null)
                return null;
            if (effective.TryGetValue(id, out var known))
                return known;
            var value = RpgObjectResolve.Effective(objects(id), variant);
            effective[id] = value;
            return value;
        }

        private static bool Fits(RpgStrokeLayer layer, RpgObject value)
            => value.Form == (layer == RpgStrokeLayer.Ground ? RpgObjectForm.Surface : RpgObjectForm.Segment);

        private static string Expected(RpgStrokeLayer layer)
            => layer == RpgStrokeLayer.Ground ? "surface" : "segment";

        private static string FormName(RpgObject value)
            => value.Form?.ToString().ToLowerInvariant() ?? "object without Form";

        private static bool IsCollisionCode(char c)
            => c == RpgCollision.Auto || c == RpgCollision.Block || c == RpgCollision.Open;

        private void Error(string message)
        {
            _errors.Add(message);
            GameLogger.Log("RPG", $"map '{MapId}': {message}", LogLevel.Error);
        }
    }
}
