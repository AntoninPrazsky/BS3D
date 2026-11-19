using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The plate's cables and winches (#896, <see cref="CeilingRigging"/>): a drum lets out exactly the cable the plate has come
    /// down, from the side the cable leaves, and every winch stands on the plate's flat top on every footprint the set ships.
    /// And (#1155) the cable wound on the drum is no thinner than the cable leaving it, and a far lens never sees the cable
    /// narrower than its floor on the screen.
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
        public void The_wraps_on_the_drum_lie_at_least_a_cable_apart()
        {
            //The wraps' crests, top to bottom: the profile's points that stand proud of the core and take the lathe's wobble
            float[] crests = CeilingRigging.DrumProfile()
                .Where(p => p.Wobble > 0f && p.Radius > CeilingRigging.CORE_RADIUS).Select(p => p.Y).ToArray();
            Assert.True(crests.Length >= 2, $"{crests.Length} wrap(s) on the drum");

            //A pitch under the cable's diameter winds a cable thinner than the one that leaves the drum (#1155)
            for (int i = 1; i < crests.Length; i++)
                Assert.True(crests[i - 1] - crests[i] >= 2f * CeilingRigging.CABLE_RADIUS,
                    $"wraps {crests[i - 1] - crests[i]:0.###} apart under a cable {2f * CeilingRigging.CABLE_RADIUS:0.###} across");
        }

        [Fact]
        public void A_cable_is_never_drawn_under_its_floor_on_the_screen_and_keeps_its_own_radius_near()
        {
            foreach (float fovDegrees in new[] { 30f, 45f, 62f, 75f })
            {
                float focal = 1f / MathF.Tan(MathHelper.ToRadians(fovDegrees) * Constants.HALF);
                foreach (float pixels in new[] { 540f, 900f, 1080f, 1600f, 2160f, 3200f })
                    for (float distance = 1f; distance <= 2000f; distance *= 1.05f)
                    {
                        float radius = CeilingRigging.CableRadius(distance, focal, pixels);
                        float across = 2f * radius * pixels * Constants.HALF * focal / distance;

                        Assert.InRange(radius, CeilingRigging.CABLE_RADIUS, CeilingRigging.MAX_CABLE_RADIUS);
                        if (radius < CeilingRigging.MAX_CABLE_RADIUS)
                            Assert.True(across >= CeilingRigging.MIN_CABLE_PIXELS - 1e-3f,
                                $"{fovDegrees} deg, {pixels} px, {distance:0.#} away: the cable is {across:0.##} px across");
                    }
            }

            //From the menu's orbit and the result page's swing-out, 15 to 45 off at a 45-degree lens on a 1080-high window,
            //the cable is its own radius: the floor only holds the far shots
            float near = 1f / MathF.Tan(MathHelper.ToRadians(45f) * Constants.HALF);
            foreach (float distance in new[] { 15f, 30f, 45f })
                Assert.Equal(CeilingRigging.CABLE_RADIUS, CeilingRigging.CableRadius(distance, near, 1080f));

            //A lens that is not a perspective one asks for no floor
            Assert.Equal(CeilingRigging.CABLE_RADIUS, CeilingRigging.CableRadius(300f, 0f, 1080f));
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
