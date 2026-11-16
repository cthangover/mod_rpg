using System;
using System.Collections.Generic;
using Cthangover.Mods.Rpg.Model;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>Checks detached typed workspace values before serialization, including values that JSON cannot encode.</summary>
    public static class RpgPartDataCheck
    {
        /// <summary>Finds invalid numeric part fields without normalizing them, rebuilding a preview or invoking Apply callbacks.</summary>
        public static IReadOnlyList<RpgDataIssue> Check(IReadOnlyList<RpgObjectPart> parts)
        {
            var issues = new List<RpgDataIssue>();
            if (parts == null)
            {
                return issues;
            }

            for (var i = 0; i < parts.Count; i++)
            {
                var path = "Parts[" + i + "]";
                var part = parts[i];
                if (part == null)
                {
                    Add(issues, "part", path);
                    continue;
                }

                Vector(issues, part.At, path + ".At", false);
                Vector(issues, part.Size, path + ".Size", true);
                Vector(issues, part.SortOffset, path + ".SortOffset", false);
                if (!Enum.IsDefined(part.Geometry))
                {
                    Add(issues, "enum", path + ".Geometry");
                }

                if (part.Geometry == RpgPartGeometry.Quad)
                {
                    var points = new List<double[]>();
                    foreach (var point in part.Points ?? new List<float[]>())
                    {
                        if (point == null)
                        {
                            points.Add(null);
                            continue;
                        }

                        var values = new double[point.Length];
                        for (var axis = 0; axis < point.Length; axis++)
                        {
                            values[axis] = point[axis];
                        }

                        points.Add(values);
                    }

                    var problem = RpgQuadCheck.Problem(points);
                    if (problem != null)
                    {
                        Add(issues, problem, path + ".Points");
                    }
                }
                else if (part.Points != null)
                {
                    foreach (var point in part.Points)
                    {
                        if (point == null)
                        {
                            continue;
                        }

                        foreach (var number in point)
                        {
                            if (!float.IsFinite(number))
                            {
                                Add(issues, "quad_number", path + ".Points");
                                break;
                            }
                        }
                    }
                }
            }

            return issues;
        }

        private static void Vector(List<RpgDataIssue> issues, float[] value, string path, bool positive)
        {
            if (value == null)
            {
                return;
            }

            var valid = value.Length == 2;
            foreach (var number in value)
            {
                valid &= float.IsFinite(number) && (!positive || number > 0);
            }

            if (!valid)
            {
                Add(issues, positive ? "size" : "vector", path);
            }
        }

        private static void Add(List<RpgDataIssue> issues, string code, string path)
        {
            issues.Add(new RpgDataIssue
            {
                Code = code,
                Field = path,
            });
        }
    }
}
