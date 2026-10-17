using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The four laser emitters on the drain's rim (#874) are never touched by the gun: the carriage is held outside their
    /// circles by <see cref="Cannon.SetKeepouts"/> and the barrel passes over them, for every bearing the gun can stand at,
    /// every radius of its walk, every aim it can take (the soft stop's overshoot included) and the footprints a level
    /// can have. The gun is its real pose (<see cref="Cannon.CarriageWorld"/>, <see cref="Cannon.BarrelWorld"/>), tested as
    /// the boxes and the tube it is drawn as, each a little fatter than the mesh; the emitters are their real placed
    /// matrices (<see cref="LaserEmitterRing"/>), tested as the capsule of the housing and the cylinder of the pedestal.
    /// </summary>
    public class LaserEmitterClearanceTests
    {
        private static readonly GameTime Frame = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));

        //Where the shipped levels' nets' outermost beams stand: (int) of half the footprint plus the net's own margin, for the
        //stage sizes the level set uses from the smallest to 17
        private static readonly int[] STAGES = { 5, 9, 13, 17 };

        private static float CornerFor(int stage) => (int)((stage + CeilingPlate.FOOTPRINT_MARGIN) * 0.5f + 0.75f);

        //The carriage as a box in its own frame: the wheels' outer faces and the trail's feet laterally, the wheel's front
        //forward, the trail's end behind, from under the stone to over the cheeks
        private static readonly Vector3 CARRIAGE_MIN = new(-1.9f, -2.8f, -1.17f);
        private static readonly Vector3 CARRIAGE_MAX = new(1.9f, 0.5f, 3.3f);

        //The tube: its half-length either side of the trunnions (the muzzle, 3.4 ahead, and the cascabel behind) and its widest
        //radius, the muzzle collar's crest
        private const float BARREL_AHEAD = 3.5f;
        private const float BARREL_BEHIND = 3.4f;
        private const float BARREL_RADIUS = 0.9f;

        //The housing as a capsule from its rear face to its lens, at the collar's radius; the pedestal as a short cylinder at the foot
        private const float HOUSING_LENGTH = 1.45f;

        //A margin over touching: a hand's breadth
        private const float MARGIN = 0.1f;

        private static IEnumerable<(float X, float Z)> Corners()
        {
            foreach (int sx in STAGES)
                foreach (int sz in STAGES)
                    yield return (CornerFor(sx), CornerFor(sz));
        }

        //Samples through the four emitters: a point and the radius around it that the emitter's metal fills
        private static List<(Vector3 Point, float Radius)> EmitterSamples(float cornerX, float cornerZ)
        {
            List<(Vector3, float)> samples = new();

            for (int k = 0; k < LaserEmitterRing.COUNT; k++)
            {
                Matrix stand = LaserEmitterRing.Stand(k, cornerX, cornerZ);
                Matrix housing = LaserEmitterRing.Housing(k, cornerX, cornerZ);

                //The pedestal: a ring of points at the flange, and the middle, standing 0.15 up
                for (int i = 0; i < 12; i++)
                {
                    float a = MathHelper.TwoPi * i / 12f;
                    samples.Add((Vector3.Transform(new Vector3(MathF.Cos(a) * 0.35f, 0.15f, MathF.Sin(a) * 0.35f), stand), 0.2f));
                }

                //The pin's two stubs and the arms they stand on
                samples.Add((Vector3.Transform(new Vector3(0.55f, LaserEmitterMesh.PIVOT_Y, 0f), stand), 0.12f));
                samples.Add((Vector3.Transform(new Vector3(-0.55f, LaserEmitterMesh.PIVOT_Y, 0f), stand), 0.12f));

                //The housing, a capsule's worth of spheres from the rear face to the lens
                for (int i = 0; i <= 14; i++)
                {
                    float along = HOUSING_LENGTH * i / 14f;
                    samples.Add((Vector3.Transform(new Vector3(0f, along, 0f), housing), HousingRadiusAt(along)));
                }
            }

            return samples;
        }

        //The housing's radius along its axis from the rear face, from its profile (LaserEmitterMesh): the taper, the body, the
        //ribs and the collar, taken at their widest over each stretch
        private static float HousingRadiusAt(float along) =>
            along < 0.42f ? 0.38f : along < 1.2f ? (along > 0.84f && along < 0.98f ? 0.43f : 0.38f) : along < 1.33f ? 0.40f : LaserEmitterMesh.COLLAR_RADIUS;

        //The smallest gap between the emitters' metal and the carriage's box, or the tube; negative is a touch
        private static float Gap(Cannon cannon, List<(Vector3 Point, float Radius)> samples)
        {
            Matrix carriage = Matrix.Invert(cannon.CarriageWorld());
            Matrix barrel = cannon.BarrelWorld();
            Vector3 pivot = barrel.Translation;
            Vector3 forward = Vector3.Normalize(Vector3.TransformNormal(Vector3.Forward, barrel));
            Vector3 a = pivot - forward * BARREL_BEHIND;
            Vector3 b = pivot + forward * BARREL_AHEAD;
            Vector3 ab = b - a;

            float worst = float.MaxValue;

            foreach ((Vector3 point, float radius) in samples)
            {
                Vector3 local = Vector3.Transform(point, carriage);
                Vector3 nearest = Vector3.Clamp(local, CARRIAGE_MIN, CARRIAGE_MAX);
                worst = MathF.Min(worst, Vector3.Distance(local, nearest) - radius);

                float t = MathHelper.Clamp(Vector3.Dot(point - a, ab) / ab.LengthSquared(), 0f, 1f);
                worst = MathF.Min(worst, Vector3.Distance(point, a + ab * t) - radius - BARREL_RADIUS);
            }

            return worst;
        }

        private static Cannon StandAt(float bearing, float radius, float cornerX, float cornerZ)
        {
            Cannon cannon = new(new Vector3(0f, 5f, 0f), 17.5f);

            //The walk the camera fit gives every shipped level: rest 17.5, from 15.5 to 21.5
            cannon.SetAdvanceRange(15.5f, 21.5f);
            cannon.SetKeepouts(cornerX, cornerZ);
            cannon.OrbitToFace(new Vector3(MathF.Cos(bearing) * 10f, 0f, MathF.Sin(bearing) * 10f));
            cannon.OrbitRadius = radius;

            return cannon;
        }

        [Fact]
        public void TheCarriageAndTheBarrelNeverTouchAnEmitterAnywhereInTheirTravel()
        {
            float[] elevations = { Cannon.MinElevation, 0.1f, 0.4f, 0.9f, 1.3f };
            float[] traverses = { -Cannon.MaxTraverse, 0f, Cannon.MaxTraverse };
            float tightest = float.MaxValue;
            string where = "";

            foreach ((float cornerX, float cornerZ) in Corners())
            {
                List<(Vector3, float)> samples = EmitterSamples(cornerX, cornerZ);

                for (int degrees = 0; degrees < 360; degrees += 2)
                    foreach (float radius in new[] { 15.5f, 16.5f, 17.5f, 19f, 21.5f })
                    {
                        Cannon cannon = StandAt(MathHelper.ToRadians(degrees), radius, cornerX, cornerZ);

                        foreach (float elevation in elevations)
                            foreach (float traverse in traverses)
                            {
                                cannon.AimTo(elevation, traverse);
                                float g = Gap(cannon, samples);
                                if (g < tightest) { tightest = g; where = $"corner {cornerX},{cornerZ} deg {degrees} r {radius} (now {cannon.OrbitRadius}) el {elevation} tr {traverse}"; }
                            }

                        //The soft stop's overshoot, the lowest the muzzle is ever drawn
                        for (int i = 0; i < 40; i++)
                        {
                            cannon.Aim(new Vector2(-1f, 0f), Frame);
                            cannon.Update(Frame);
                        }

                        float g2 = Gap(cannon, samples);
                        if (g2 < tightest) { tightest = g2; where = $"OVERSHOOT corner {cornerX},{cornerZ} deg {degrees} r {radius} (now {cannon.OrbitRadius})"; }
                    }
            }

            Assert.True(tightest >= MARGIN, $"the tightest gap between the gun and an emitter is {tightest:F3} at {where}");
        }

        [Fact]
        public void TheKeepoutCanBeSeenToFire()
        {
            //The test above only means something if the keep-out is what holds the gun off: with it unset, a gun walked in
            //on an emitter's bearing does touch it
            float cornerX = CornerFor(17), cornerZ = CornerFor(17);
            float bearing = LaserEmitterRing.Bearing(0, cornerX, cornerZ);
            List<(Vector3, float)> samples = EmitterSamples(cornerX, cornerZ);

            Cannon free = new(new Vector3(0f, 5f, 0f), 17.5f);
            free.SetAdvanceRange(15.5f, 21.5f);
            free.OrbitToFace(new Vector3(MathF.Cos(bearing) * 10f, 0f, MathF.Sin(bearing) * 10f));
            free.OrbitRadius = 15.5f;
            free.AimTo(Cannon.MinElevation, 0f);

            Assert.True(Gap(free, samples) < 0f, "a gun with no keep-out, walked in on an emitter, should touch it");

            Cannon held = StandAt(bearing, 15.5f, cornerX, cornerZ);
            held.AimTo(Cannon.MinElevation, 0f);

            Assert.True(held.OrbitRadius > 15.5f);
            Assert.True(Gap(held, samples) >= MARGIN);
        }

        [Fact]
        public void TheGunAtRestIsNeverMovedByAnEmitter()
        {
            //Every shipped level rests the gun at 17.5: the keep-out has to leave that radius alone at every bearing, or the
            //camera follows a gun the player did not move
            const float REST = 17.5f;

            foreach ((float cornerX, float cornerZ) in Corners())
                for (float bearing = 0f; bearing < MathHelper.TwoPi; bearing += 0.01f)
                    Assert.True(LaserEmitterRing.GunFloorRadius(bearing, cornerX, cornerZ, CannonRig.StandReach) < REST,
                        $"corner {cornerX},{cornerZ} bearing {bearing}");
        }

        [Fact]
        public void TheEmittersStayUnderTheBarrelAtItsLowestAimStandingAnywhere()
        {
            //The barrel's underside over an emitter, at the lowest aim, with the gun as near as it may stand: the emitter's
            //top is under it. A bound by arithmetic that the sweep above checks sample by sample.
            float stand = LaserEmitterRing.FOOT_RADIUS + CannonRig.StandReach + LaserEmitterRing.KEEPOUT_RADIUS;
            float trunnionY = CannonRig.TrunnionHeightAt(stand);
            float footY = ArenaIsland.FloorHeightAt(LaserEmitterRing.FOOT_RADIUS);
            float lowest = Cannon.MinElevation - Cannon.ELEVATION_OVERSHOOT;
            float axisAtEmitter = trunnionY + MathF.Tan(lowest) * (CannonRig.StandReach + LaserEmitterRing.KEEPOUT_RADIUS);

            Assert.True(axisAtEmitter - BARREL_RADIUS > footY + LaserEmitterRing.TopAboveBand);
        }
    }
}
