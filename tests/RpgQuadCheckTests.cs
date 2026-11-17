namespace Cthangover.Mods.Rpg.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Validation;

    /// <summary>World geometry is checked in all three axes, at fixed tolerances and without projection-dependent area tests.</summary>
    public sealed class RpgQuadCheckTests
    {
        [ModTest("data_quad/affine_world_planes_and_small_valid_inputs")]
        static void Valid(ModTestContext ctx)
        {
            var ordinary = Quad(0, 1, 0);
            ModAssert.True(RpgQuadCheck.Problem(ordinary) == null, "unit ground quad is affine");
            var tiny = Quad(0, 0.00000001, 0);
            ModAssert.True(RpgQuadCheck.Problem(tiny) == null, "small area is not confused with zero area");
            var wall = new List<double[]>
            {
                new double[]
                {
                    0,
                    0,
                    1,
                },
                new double[]
                {
                    1,
                    0,
                    1,
                },
                new double[]
                {
                    1,
                    0,
                    0,
                },
                new double[]
                {
                    0,
                    0,
                    0,
                },
            };
            ModAssert.True(RpgQuadCheck.Problem(wall) == null, "vertical walls retain their three-dimensional area");
            wall.Reverse();
            ModAssert.True(RpgQuadCheck.Problem(wall) == null, "reverse winding remains a valid affine plane");
            var inclined = Quad(20, 3, 0);
            inclined[1][2] = 2;
            inclined[2][2] = 3;
            inclined[3][2] = 1;
            ModAssert.True(RpgQuadCheck.Problem(inclined) == null, "inclined planes need not align with an axis");
        }

        [ModTest("data_quad/fixed_threshold_translation_and_axes")]
        static void Precision(ModTestContext ctx)
        {
            ModAssert.True(RpgQuadCheck.AbsoluteTolerance == 0.000001, "absolute tolerance is fixed");
            ModAssert.True(RpgQuadCheck.RelativeTolerance == 0.00001, "relative tolerance is fixed");
            for (var axis = 0; axis < 3; axis++)
            {
                var near = Quad(0, 1, 0);
                near[2][axis] += 0.000005;
                ModAssert.True(RpgQuadCheck.Problem(near) == null, "below threshold on axis " + axis);
                var far = Quad(0, 1, 0);
                far[2][axis] += 0.00002;
                ModAssert.Equal(RpgDataRules.QuadAffine, RpgQuadCheck.Problem(far), "above threshold on axis " + axis);
                foreach (var point in far)
                {
                    for (var index = 0; index < 3; index++)
                    {
                        point[index] += 100000;
                    }
                }

                ModAssert.Equal(RpgDataRules.QuadAffine, RpgQuadCheck.Problem(far), "large translation never increases tolerance");
            }
        }

        [ModTest("data_quad/malformed_nonfinite_and_degenerate_inputs")]
        static void Invalid(ModTestContext ctx)
        {
            ModAssert.Equal(RpgDataRules.QuadCount, RpgQuadCheck.Problem(null), "missing corners");
            var shortQuad = Quad(0, 1, 0);
            shortQuad.RemoveAt(3);
            ModAssert.Equal(RpgDataRules.QuadCount, RpgQuadCheck.Problem(shortQuad), "three corners");
            shortQuad.Add(new double[]
            {
                0,
                1,
                0,
            });
            shortQuad.Add(new double[]
            {
                0,
                0,
                0,
            });
            ModAssert.Equal(RpgDataRules.QuadCount, RpgQuadCheck.Problem(shortQuad), "five corners");
            var malformed = Quad(0, 1, 0);
            malformed[0] = new double[]
            {
                0,
                0,
            };
            ModAssert.Equal(RpgDataRules.QuadNumber, RpgQuadCheck.Problem(malformed), "two-dimensional corner is invalid");
            var numbers = new[]
            {
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            };
            foreach (var number in numbers)
            {
                var bad = Quad(0, 1, 0);
                bad[2][2] = number;
                ModAssert.Equal(RpgDataRules.QuadNumber, RpgQuadCheck.Problem(bad), "nonfinite corner remains explicit");
            }

            ModAssert.Equal(RpgDataRules.QuadArea, RpgQuadCheck.Problem(Quad(0, 0, 0)), "repeated corners have zero area");
            var line = Quad(0, 1, 0);
            line[3] = new double[]
            {
                2,
                0,
                0,
            };
            ModAssert.Equal(RpgDataRules.QuadArea, RpgQuadCheck.Problem(line), "collinear basis has zero three-dimensional area");
            var trapezoid = Quad(0, 1, 0);
            trapezoid[2][0] = 2;
            ModAssert.Equal(RpgDataRules.QuadAffine, RpgQuadCheck.Problem(trapezoid), "trapezoid cannot use the affine renderer");
        }

        [ModTest("data_quad/runtime_float_guard")]
        static void Runtime(ModTestContext ctx)
        {
            var points = new List<float[]>
            {
                new float[]
                {
                    0,
                    0,
                    0,
                },
                new float[]
                {
                    1,
                    0,
                    0,
                },
                new float[]
                {
                    1,
                    1,
                    0,
                },
                new float[]
                {
                    0,
                    1,
                    0,
                },
            };
            ModAssert.True(RpgQuadCheck.Drawable(points), "finite runtime keys are accepted");
            points[2][2] = float.NaN;
            ModAssert.False(RpgQuadCheck.Drawable(points), "nonfinite runtime keys are rejected");
            ModAssert.False(RpgQuadCheck.Drawable(null), "missing runtime geometry is rejected");
            points[2] = null;
            ModAssert.False(RpgQuadCheck.Drawable(points), "null runtime corner is rejected");
        }

        private static List<double[]> Quad(double offset, double size, double z)
        {
            return new List<double[]>
            {
                new double[]
                {
                    offset,
                    offset,
                    z,
                },
                new double[]
                {
                    offset + size,
                    offset,
                    z,
                },
                new double[]
                {
                    offset + size,
                    offset + size,
                    z,
                },
                new double[]
                {
                    offset,
                    offset + size,
                    z,
                },
            };
        }
    }
}
