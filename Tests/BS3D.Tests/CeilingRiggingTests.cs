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
    /// narrower than its floor on the screen where it is nearest the lens, measured through a real projection.
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
        public void A_far_lens_sees_a_flat_cable_at_its_floor_where_it_is_nearest_and_a_near_lens_a_round_one_at_its_own_radius()
        {
            Vector3 foot = new(7.4f, 8.6f, -7.4f);
            const float rise = 600f;
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), 16f / 9f, 0.1f, 2000f);

            //150 off across the ground, from under the foot, level with the cable's middle and over its end: the floor holds
            //the point nearest the lens, which for those three is the foot, the point level with the lens and the end
            Vector3 away = Vector3.Normalize(new Vector3(-3f, 0f, -1f)) * 150f;
            foreach (float eyeY in new[] { -40f, 300f, 700f })
            {
                Vector3 eye = foot + away;
                eye.Y = eyeY;
                Vector3 nearest = new(foot.X, MathHelper.Clamp(eyeY, foot.Y, foot.Y + rise), foot.Z);

                //A 2x supersampled target under a 1080-high back buffer, and a target drawn under the screen's resolution:
                //the floor counts the coarser of the two
                foreach ((int target, int screen) in new[] { (2160, 1080), (1080, 1600) })
                {
                    Matrix world = CeilingRigging.CableInstance(foot, rise, eye, projection, target, screen);
                    AssertStands(world, foot, rise);

                    float across = ProjectedWidth(world, nearest, eye, projection, 1080);
                    Assert.True(MathF.Abs(across - CeilingRigging.MIN_CABLE_PIXELS) < 1e-3f,
                        $"from {eye}, on {target} over {screen} px, the cable is {across:0.###} px across at {nearest}");
                    Assert.Equal(CeilingRigging.CABLE_FLAT_DEPTH, Depth(world), 4);
                }
            }

            //12 off, level with the foot, the cable is about a dozen pixels across at its own radius: no floor, round
            Vector3 close = foot + Vector3.Normalize(new Vector3(1f, 0f, 2f)) * 12f;
            Matrix near = CeilingRigging.CableInstance(foot, rise, close, projection, 1080, 1080);
            AssertStands(near, foot, rise);
            Assert.Equal(CeilingRigging.CABLE_RADIUS, Radius(near), 5);
            Assert.Equal(1f, Depth(near), 4);
            Assert.True(ProjectedWidth(near, foot, close, projection, 1080) > 10f);

            //30 off, between the two, the depth follows the width the lens sees there
            Vector3 middle = foot + Vector3.Normalize(new Vector3(1f, 0f, 2f)) * 30f;
            Matrix eased = CeilingRigging.CableInstance(foot, rise, middle, projection, 1080, 1080);
            float width = ProjectedWidth(eased, foot, middle, projection, 1080);
            Assert.InRange(width, 3.5f, 5.5f);
            Assert.Equal(CeilingRigging.CableDepth(width), Depth(eased), 3);
        }

        //The cable's radius across the lens's view, and its depth along it as a share of that radius: the lengths of its
        //world's Z and X rows (CableWorld)
        private static float Radius(Matrix world) => new Vector3(world.M31, world.M32, world.M33).Length();
        private static float Depth(Matrix world) => new Vector3(world.M11, world.M12, world.M13).Length() / Radius(world);

        private static void AssertStands(Matrix world, Vector3 foot, float rise)
        {
            Assert.Equal(0f, Vector3.Distance(foot, world.Translation), 4);
            Assert.Equal(0f, Vector3.Distance(Vector3.Up * rise, new Vector3(world.M21, world.M22, world.M23)), 2);
        }

        //How many pixels across the drawn cable is at a point of its axis, on a frame this many pixels high: its two edges
        //across the lens's view put through a real view and projection, not through the code's own formula
        private static float ProjectedWidth(Matrix world, Vector3 at, Vector3 eye, Matrix projection, int pixelHeight)
        {
            Vector3 across = new(world.M31, world.M32, world.M33);
            Matrix viewProjection = Matrix.CreateLookAt(eye, at, Vector3.Up) * projection;
            Vector4 a = Vector4.Transform(new Vector4(at - across, 1f), viewProjection);
            Vector4 b = Vector4.Transform(new Vector4(at + across, 1f), viewProjection);

            //Normalised x to pixels: the frame's width is its height times the aspect, which M11 divides back out
            float pixelsPerUnitX = pixelHeight * Constants.HALF * projection.M22 / projection.M11;
            return MathF.Abs(a.X / a.W - b.X / b.W) * pixelsPerUnitX;
        }

        [Fact]
        public void A_thin_cable_is_drawn_flat_along_the_look_and_a_wide_one_round()
        {
            foreach (float pixels in new[] { 0.5f, 1.5f, 3f })
                Assert.Equal(CeilingRigging.CABLE_FLAT_DEPTH, CeilingRigging.CableDepth(pixels));
            foreach (float pixels in new[] { 6f, 12f, 40f })
                Assert.Equal(1f, CeilingRigging.CableDepth(pixels));

            for (float pixels = 0f, last = 0f; pixels <= 10f; pixels += 0.1f)
            {
                float depth = CeilingRigging.CableDepth(pixels);
                Assert.True(depth >= last, $"the depth falls to {depth} at {pixels:0.#} px");
                last = depth;
            }
        }

        [Fact]
        public void A_flat_cable_shades_as_one_tone_across_the_width_the_lens_sees()
        {
            Vector3 foot = new(7.4f, 8.6f, -7.4f);
            foreach (Vector3 eye in new[] { new Vector3(-160f, 39f, -70f), new Vector3(30f, -4f, 0f), new Vector3(7.4f, 20f, 30f) })
            {
                const float radius = 0.08f;
                Vector3 facing = Vector3.Normalize(new Vector3(eye.X - foot.X, 0f, eye.Z - foot.Z));

                foreach (float depth in new[] { CeilingRigging.CABLE_FLAT_DEPTH, 1f })
                {
                    Matrix world = CeilingRigging.CableWorld(foot, 600f, eye, radius, depth);

                    //What the instancing shader does with a normal (NormalToWorld: the matrix's cofactors), and the width the
                    //lens sees, which the depth must leave alone
                    Matrix normals = Matrix.Transpose(Matrix.Invert(world));
                    Assert.Equal(radius, new Vector3(world.M31, world.M32, world.M33).Length(), 4);
                    Assert.True(world.Determinant() > 0f, "the cable's world mirrors it, and its winding with it");

                    float worst = 0f;
                    for (float degrees = -180f; degrees < 180f; degrees += 0.5f)
                    {
                        float angle = MathHelper.ToRadians(degrees);
                        Vector3 radial = new(MathF.Cos(angle), 0f, MathF.Sin(angle));
                        Vector3 normal = Vector3.Normalize(Vector3.TransformNormal(radial, normals));

                        //Outward all round the squashed section
                        Assert.True(Vector3.Dot(normal, Vector3.TransformNormal(radial, world)) > 0f,
                            $"depth {depth}, at {degrees} deg the normal points into the cable");

                        //The middle 70 % of the width the lens sees: where a round rod's normal turns 44 degrees each way
                        if (radial.X > 0f && MathF.Abs(radial.Z) <= 0.7f)
                            worst = MathF.Max(worst, MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(normal, facing), -1f, 1f))));
                    }

                    if (depth < 1f) Assert.True(worst <= 15f, $"flat, from {eye} the normal turns {worst:0.#} deg off the lens");
                    else Assert.True(worst > 40f, $"round, from {eye} the normal turns only {worst:0.#} deg off the lens");
                }
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
