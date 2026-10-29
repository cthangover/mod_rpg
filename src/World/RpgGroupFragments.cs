namespace Cthangover.Mods.Rpg
{
    using System.Collections.Generic;
    using Godot;

    /// <summary>
    /// Where each tile of a visual group sits inside its object. A cell left over after another object overwrote
    /// part of a group (so the pattern no longer matches) is placed relative to the anchor of the object it came
    /// from: the isometric view draws the remaining fragment exactly as that part of the whole sprite, instead of
    /// standing every cell up at its own map cell. A tile used by several groups or twice in one keeps its first place.
    /// </summary>
    public static class RpgGroupFragments
    {
        public static Dictionary<int, (RpgVisualGroup Group, Vector2I Cell)> Of(RpgTileset tileset)
        {
            var result = new Dictionary<int, (RpgVisualGroup, Vector2I)>();
            foreach (var group in tileset.VisualGroups ?? new List<RpgVisualGroup>())
            {
                if (group.IsoPlanes?.Count > 0 || RpgVisualGroupValidator.Error(group) != null) continue;
                for (var i = 0; i < group.Pattern.Length; i++)
                    result.TryAdd(group.Pattern[i], (group, new Vector2I(i % group.Width, i / group.Width)));
            }
            return result;
        }
    }
}
