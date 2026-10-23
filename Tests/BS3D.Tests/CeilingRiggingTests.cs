using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The plate's cables and winches (#896, <see cref="CeilingRigging"/>): a drum lets out exactly the cable the plate has come
    /// down, from the side the cable leaves, and every winch stands on the plate's flat top on every footprint the set ships.
    /// </summary>
    public class CeilingRiggingTests
    {
        [Fact]
        public void A_plate_that_steps_down_pays_cable_out_of_every_drum_by_the_height_it_came_down()
        {
            const float step = 0.6f;

            foreach (float y in new[] { 18.3f, 7.7f, -2f })
                for (int k = 0; k < 4; k++)
                {
                    Vector3 before = new(0f, y, 0f), after = new(0f, y - step, 0f);
                    CeilingRigging.Corner(k, 5f, 6f, before, out Vector3 outward, out Vector3 axis, out Vector3 drumBefore);
                    CeilingRigging.Corner(k, 5f, 6f, after, out _, out _, out Vector3 drumAfter);

                    Matrix worldBefore = Matrix.CreateRotationY(CeilingRigging.DrumTurn(y)) * CeilingRigging.DrumFrame(outward, axis, drumBefore);
                    Matrix worldAfter = Matrix.CreateRotationY(CeilingRigging.DrumTurn(y - step)) * CeilingRigging.DrumFrame(outward, axis, drumAfter);

                    //The point of the core the cable leaves from before the step, followed through it: the drum turned it up and
                    //over, by the length of cable the step let out (an arc of the step's length on the core)
                    Vector3 local = Vector3.Transform(drumBefore + outward * CeilingRigging.CORE_RADIUS, Matrix.Invert(worldBefore));
                    Vector3 moved = Vector3.Transform(local, worldAfter) - drumAfter;

                    float angle = step / CeilingRigging.CORE_RADIUS;
                    Assert.Equal(CeilingRigging.CORE_RADIUS * MathF.Sin(angle), moved.Y, 3);
                    Assert.Equal(CeilingRigging.CORE_RADIUS * MathF.Cos(angle), Vector3.Dot(moved, outward), 3);
                    Assert.Equal(0f, Vector3.Dot(moved, axis), 3);
                }
        }

        [Fact]
        public void Every_winch_stands_inside_the_crown_on_every_shipped_footprint()
        {
            //The shipped set's fields run from 11 to 17 cells a side; the Testbed's default is 10
            for (int cells = 10; cells <= 17; cells++)
            {
                float half = CeilingPlate.FootprintFor(cells) * Constants.HALF;
                float corner = half - CeilingRigging.CORNER_INSET;

                for (int k = 0; k < 4; k++)
                {
                    CeilingRigging.Corner(k, corner, corner, Vector3.Zero, out Vector3 outward, out Vector3 axis, out Vector3 at);

                    //The flanges' rims and the blocks' outer corners, the parts that reach furthest out
                    foreach (float along in new[] { -1f, 1f })
                        foreach (float across in new[] { -1f, 1f })
                        {
                            Vector3 flange = at + axis * (along * CeilingRigging.DRUM_WIDTH * Constants.HALF) + outward * (across * CeilingRigging.FLANGE_RADIUS);
                            Assert.True(MathF.Max(MathF.Abs(flange.X), MathF.Abs(flange.Z)) < half - CeilingPlate.CROWN,
                                $"{cells} cells, corner {k}: a flange reaches {flange} over a crown that starts {half - CeilingPlate.CROWN} out");
                        }
                }
            }
        }
    }
}
