using System;
using System.Collections.Generic;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>Checks affine world quads in double precision; translation never increases the permitted corner residual.</summary>
    public static class RpgQuadCheck
    {
        /// <summary>Absolute corner residual floor in world cells, fixed before the validation tests.</summary>
        public const double AbsoluteTolerance = 0.000001;

        /// <summary>Residual fraction of the longer basis edge; independent of distance from the world origin.</summary>
        public const double RelativeTolerance = 0.00001;

        /// <summary>Returns a rule code, or null for a finite, nondegenerate affine quad in three dimensions.</summary>
        public static string Problem(IReadOnlyList<double[]> points)
        {
            if (points == null || points.Count != 4)
            {
                return "quad_count";
            }

            foreach (var point in points)
            {
                if (point == null || point.Length != 3)
                {
                    return "quad_number";
                }

                foreach (var number in point)
                {
                    if (!double.IsFinite(number))
                    {
                        return "quad_number";
                    }
                }
            }

            var a = Difference(points[1], points[0]);
            var b = Difference(points[3], points[0]);
            var cross = new[]
            {
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0],
            };
            var lengthA = Length(a);
            var lengthB = Length(b);
            var area = Length(cross);
            if (!double.IsFinite(lengthA) || !double.IsFinite(lengthB) || !double.IsFinite(area)
                || lengthA == 0 || lengthB == 0 || area == 0)
            {
                return "quad_area";
            }

            var residual = Difference(Difference(points[2], points[1]), b);
            var tolerance = Math.Max(AbsoluteTolerance, RelativeTolerance * Math.Max(lengthA, lengthB));
            if (!double.IsFinite(Length(residual)) || Length(residual) > tolerance)
            {
                return "quad_affine";
            }

            return null;
        }

        /// <summary>Runtime guard for already deserialized float keys; invalid parts can be skipped independently.</summary>
        public static bool Drawable(IReadOnlyList<float[]> points)
        {
            if (points == null)
            {
                return false;
            }

            var converted = new List<double[]>();
            foreach (var point in points)
            {
                if (point == null)
                {
                    converted.Add(null);
                    continue;
                }

                var values = new double[point.Length];
                for (var i = 0; i < point.Length; i++)
                {
                    values[i] = point[i];
                }

                converted.Add(values);
            }

            return Problem(converted) == null;
        }

        private static double[] Difference(double[] left, double[] right)
        {
            return new[]
            {
                left[0] - right[0],
                left[1] - right[1],
                left[2] - right[2],
            };
        }

        private static double Length(double[] value)
        {
            return Math.Sqrt(value[0] * value[0] + value[1] * value[1] + value[2] * value[2]);
        }
    }
}
