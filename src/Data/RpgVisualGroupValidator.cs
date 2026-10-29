namespace Cthangover.Mods.Rpg
{
    using System;
    /// <summary>Rejects ambiguous source geometry before it reaches a renderer; validation never changes map cells.</summary>
    public static class RpgVisualGroupValidator
    {
        public static string Error(RpgVisualGroup group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.Id)) return "visual group requires an ID";
            if (group.Width <= 0 || group.Height <= 0 || group.Pattern?.Length != (long)group.Width * group.Height)
                return "visual group pattern dimensions differ";
            if (!float.IsFinite(group.AnchorX) || !float.IsFinite(group.AnchorY)) return "non-finite group anchor";
            foreach (var id in group.Pattern) if (id < 0) return "visual group contains an empty logical ID";
            if (group.IsoPlanes == null) return null;
            foreach (var plane in group.IsoPlanes)
            {
                if (plane?.Crop?.Length != 4 || plane.Points?.Length != 4) return "plane requires a crop and four corners";
                var crop = plane.Crop;
                if (crop[0] < 0 || crop[1] < 0 || crop[2] <= 0 || crop[3] <= 0 ||
                    (long)crop[0] + crop[2] > group.Width || (long)crop[1] + crop[3] > group.Height) return "plane crop exceeds its source group";
                foreach (var point in plane.Points)
                {
                    if (point?.Length != 3) return "plane corners require XYZ coordinates";
                    foreach (var value in point) if (!float.IsFinite(value)) return "non-finite plane corner";
                }
                for (var axis = 0; axis < 3; axis++)
                    if (Math.Abs(plane.Points[0][axis] + plane.Points[2][axis] - plane.Points[1][axis] - plane.Points[3][axis]) > 0.001f)
                        return "plane corners must form an affine quad";
            }
            return null;
        }
    }
}
