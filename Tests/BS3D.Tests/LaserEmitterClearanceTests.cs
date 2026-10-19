using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.Levels;
using Prazsky.Core;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The four laser emitters mounted inside the drain's funnel (#874) are out of the gun's way with nothing holding the
    /// gun off them, and each aims exactly at its corner of the floor net. The gun is its real pose
    /// (<see cref="Cannon.CarriageWorld"/>, <see cref="Cannon.BarrelWorld"/>) over every bearing, every radius of its walk,
    /// every aim it can take (the soft stop's overshoot included) and the footprints a level can have, tested as the wheels'
    /// discs, the boxes the rest of the carriage fits in and the tube, each a little fatter than the mesh; the emitters are
    /// their real placed matrices (<see cref="LaserEmitterRing"/>), tested as spheres through the pedestal, the yoke and the
    /// housing.
    /// </summary>
    public class LaserEmitterClearanceTests
    {
        private static readonly GameTime Frame = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));

        //Stage sizes from the smallest a level is drawn at to the largest the set uses (17), in both directions and every
        //pairing, so the net's corners stand everywhere from 3 to 9 out on either axis
        private static readonly int[] STAGES = { 5, 9, 11, 12, 13, 15, 16, 17 };

        //The footprints the gun's whole travel is swept against: the squares the set ships and the oblongs at its extremes,
        //which turn the emitters' bearings off the diagonals furthest (the sweep is the costly test, so not every pairing)
        private static readonly (int X, int Z)[] SWEPT = { (5, 5), (11, 11), (13, 13), (15, 15), (17, 17), (17, 11), (11, 17), (17, 5) };

        //Where a level's net's outermost beams stand: (int) of half the footprint plus the net's own margin
        //(LaserGrid.Fit's FOOTPRINT_MARGIN, 0.75), the expression the beams are placed by
        private static float CornerFor(int stage) => (int)((stage + CeilingPlate.FOOTPRINT_MARGIN) * 0.5f + 0.75f);

        //The net's plane: where a ball's surface touches at the moment of loss (GameplayScreen's LaserGrid.Fit call)
        private const float PLANE_Y = ClusterHang.DEATH_Y - Constants.HALF;

        //The rim, the mouth's plane
        private const float RIM_Y = ArenaIsland.TOP_Y - ArenaIsland.DISH_DEPTH;

        //The walk every shipped level is given: rest 17.5, from the drain's clearance (14 + 1.5) to 21.5
        private const float NEAR = ArenaIsland.FUNNEL_TOP_RADIUS + GameCameraFit.CANNON_DRAIN_CLEARANCE;
        private const float REST = 17.5f;
        private const float FAR = 21.5f;

        //The wheels, in the carriage's frame (trunnions at the origin, ahead is -Z): a disc each side round the axle,
        //its centre WHEEL_TRACK out and AXLE_DROP down, the rollers' envelope's radius and the plates' and rollers' width
        //either side of it - a hair over CannonRig's figures
        private const float WHEEL_X = 1.5f;
        private const float WHEEL_Y = -1.45f;
        private const float WHEEL_RADIUS = 1.17f;
        private const float WHEEL_HALF_WIDTH = 0.41f;

        //Everything else of the carriage, as boxes: the cheeks, the trunnion pins and the axle between the wheels (ahead no
        //further than the cheeks' front), and the split trail behind, down to its feet
        private static readonly (Vector3 Min, Vector3 Max)[] CARRIAGE_BOXES =
        {
            (new Vector3(-1.2f, -1.62f, -0.92f), new Vector3(1.2f, 0.5f, 0.92f)),
            (new Vector3(-1.75f, -2.5f, 0f), new Vector3(1.75f, 0.5f, 3.3f)),
        };

        //The tube: its half-length either side of the trunnions (the muzzle, 3.4 ahead, and the cascabel behind) and its widest
        //radius, the muzzle collar's crest, taken all along it
        private const float BARREL_AHEAD = 3.5f;
        private const float BARREL_BEHIND = 3.4f;
        private const float BARREL_RADIUS = 0.9f;

        //A margin over touching: a hand's breadth
        private const float MARGIN = 0.1f;

        //The furthest anything modelled above reaches from the trunnions (the trail's foot, 4.5; the tube's ends with their
        //radius, 4.4), and the recoil's shove back on top
        private const float GUN_REACH = 5.2f;

        private static IEnumerable<(float X, float Z)> Corners()
        {
            foreach (int sx in STAGES)
                foreach (int sz in STAGES)
                    yield return (CornerFor(sx), CornerFor(sz));
        }

        //Samples through the four emitters: a point and the radius around it that the emitter's metal and glass fill
        private static List<(Vector3 Point, float Radius)> EmitterSamples(float cornerX, float cornerZ)
        {
            List<(Vector3, float)> samples = new();

            for (int k = 0; k < LaserEmitterRing.COUNT; k++)
            {
                Matrix stand = LaserEmitterRing.Stand(k, cornerX, cornerZ);
                Matrix housing = LaserEmitterRing.Housing(k, cornerX, cornerZ, PLANE_Y);

                //The pedestal: a ring of points at the flange and the cup, and the middle
                for (int i = 0; i < 12; i++)
                {
                    float a = MathHelper.TwoPi * i / 12f;
                    samples.Add((Vector3.Transform(new Vector3(MathF.Cos(a) * 0.38f, 0.1f, MathF.Sin(a) * 0.38f), stand), 0.14f));
                    samples.Add((Vector3.Transform(new Vector3(MathF.Cos(a) * 0.2f, 0.22f, MathF.Sin(a) * 0.2f), stand), 0.12f));
                }

                //The yoke's arms up to the pin, and the pin's two stubs
                for (int i = 0; i <= 6; i++)
                {
                    float y = MathHelper.Lerp(0.18f, LaserEmitterMesh.PIVOT_Y + 0.14f, i / 6f);
                    samples.Add((Vector3.Transform(new Vector3(0.48f, y, 0f), stand), 0.14f));
                    samples.Add((Vector3.Transform(new Vector3(-0.48f, y, 0f), stand), 0.14f));
                }

                samples.Add((Vector3.Transform(new Vector3(0.55f, LaserEmitterMesh.PIVOT_Y, 0f), stand), 0.12f));
                samples.Add((Vector3.Transform(new Vector3(-0.55f, LaserEmitterMesh.PIVOT_Y, 0f), stand), 0.12f));

                //The housing: rings round its axis at the stations of its profile, from the rear face to the lip in front of
                //the lens, each ring of small spheres on its surface. Rings and not spheres on the axis, because the housing
                //is a flat-ended cylinder, and a sphere big enough to hold its lip would stand half its width proud of it
                foreach ((float along, float radius) in HOUSING_PROFILE)
                    for (int i = 0; i < 8; i++)
                    {
                        float a = MathHelper.TwoPi * i / 8f;
                        Vector3 local = new(MathF.Cos(a) * (radius - 0.04f), along, MathF.Sin(a) * (radius - 0.04f));
                        samples.Add((Vector3.Transform(local, housing), 0.06f));
                    }
            }

            return samples;
        }

        //The housing's profile (LaserEmitterMesh): stations along its axis from the rear face and its radius there, the
        //widest of each stretch - the taper, the body, the two ribs, the collar and its lip ahead of the lens
        private static readonly (float Along, float Radius)[] HOUSING_PROFILE =
        {
            (0f, 0.29f), (0.1f, 0.29f), (0.26f, 0.34f), (0.42f, 0.38f), (0.63f, 0.38f), (0.84f, 0.43f), (0.96f, 0.43f),
            (1.1f, 0.38f), (1.22f, 0.40f), (1.33f, 0.40f), (1.36f, LaserEmitterMesh.COLLAR_RADIUS),
            (1.45f, LaserEmitterMesh.COLLAR_RADIUS), (1.45f, 0.3f),
        };

        //The smallest gap between the samples and the carriage (its wheels and boxes) or the tube; negative is a touch
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
            Vector3 trunnions = cannon.Position;

            foreach ((Vector3 point, float radius) in samples)
            {
                //Nothing of the gun as modelled is further than GUN_REACH from its trunnions: a sample beyond it is clear
                if (Vector3.DistanceSquared(point, trunnions) > (GUN_REACH + radius) * (GUN_REACH + radius))
                {
                    worst = MathF.Min(worst, GUN_REACH);
                    continue;
                }

                Vector3 local = Vector3.Transform(point, carriage);

                foreach ((Vector3 min, Vector3 max) in CARRIAGE_BOXES)
                    worst = MathF.Min(worst, Vector3.Distance(local, Vector3.Clamp(local, min, max)) - radius);

                //A wheel: the distance to a disc about the X axis, its width and its radius
                for (int side = -1; side <= 1; side += 2)
                {
                    float dx = MathF.Max(0f, MathF.Abs(local.X - side * WHEEL_X) - WHEEL_HALF_WIDTH);
                    float dr = MathF.Max(0f, MathF.Sqrt((local.Y - WHEEL_Y) * (local.Y - WHEEL_Y) + local.Z * local.Z) - WHEEL_RADIUS);
                    worst = MathF.Min(worst, MathF.Sqrt(dx * dx + dr * dr) - radius);
                }

                float t = MathHelper.Clamp(Vector3.Dot(point - a, ab) / ab.LengthSquared(), 0f, 1f);
                worst = MathF.Min(worst, Vector3.Distance(point, a + ab * t) - radius - BARREL_RADIUS);
            }

            return worst;
        }

        private static Cannon StandAt(float bearing, float radius)
        {
            Cannon cannon = new(new Vector3(0f, 5f, 0f), REST);

            cannon.SetAdvanceRange(NEAR, FAR);
            cannon.OrbitToFace(new Vector3(MathF.Cos(bearing) * 10f, 0f, MathF.Sin(bearing) * 10f));
            cannon.OrbitRadius = radius;

            return cannon;
        }

        //The tightest gap over the whole travel: every bearing, the walk's ends and between, every aim and the soft stop's
        //overshoot, against the given samples
        private static (float Gap, string Where) Sweep(List<(Vector3, float)> samples, string label, int bearingStep)
        {
            float[] elevations = { Cannon.MinElevation, 0.1f, 0.4f, 0.9f, 1.3f };
            float[] traverses = { -Cannon.MaxTraverse, -Cannon.MaxTraverse * 0.5f, 0f, Cannon.MaxTraverse * 0.5f, Cannon.MaxTraverse };
            float tightest = float.MaxValue;
            string where = "";

            for (int degrees = 0; degrees < 360; degrees += bearingStep)
                foreach (float radius in new[] { NEAR, NEAR + 0.5f, REST, FAR })
                {
                    Cannon cannon = StandAt(MathHelper.ToRadians(degrees), radius);

                    foreach (float elevation in elevations)
                        foreach (float traverse in traverses)
                        {
                            cannon.AimTo(elevation, traverse);
                            float g = Gap(cannon, samples);
                            if (g < tightest) { tightest = g; where = $"{label} deg {degrees} r {radius} el {elevation} tr {traverse}"; }
                        }

                    //The soft stop's overshoot, the lowest the muzzle is ever drawn, square on and at the traverse's ends
                    foreach (float traverse in new[] { -Cannon.MaxTraverse, 0f, Cannon.MaxTraverse })
                    {
                        cannon.AimTo(Cannon.MinElevation, traverse);
                        for (int i = 0; i < 40; i++)
                        {
                            cannon.Aim(new Vector2(-1f, 0f), Frame);
                            cannon.Update(Frame);
                        }

                        float g = Gap(cannon, samples);
                        if (g < tightest) { tightest = g; where = $"OVERSHOOT {label} deg {degrees} r {radius} tr {traverse}"; }
                    }
                }

            return (tightest, where);
        }

        [Fact]
        public void TheCarriageAndTheBarrelNeverTouchAnEmitterAnywhereInTheirTravel()
        {
            //The wheels as modelled reach at least as far as the rig says they do
            Assert.True(MathF.Sqrt((WHEEL_X + WHEEL_HALF_WIDTH) * (WHEEL_X + WHEEL_HALF_WIDTH) + WHEEL_RADIUS * WHEEL_RADIUS) >= CannonRig.StandReach);

            float tightest = float.MaxValue;
            string where = "";

            foreach ((int sx, int sz) in SWEPT)
            {
                float cornerX = CornerFor(sx), cornerZ = CornerFor(sz);
                (float gap, string at) = Sweep(EmitterSamples(cornerX, cornerZ), $"corner {cornerX},{cornerZ}", 2);
                if (gap < tightest) { tightest = gap; where = at; }
            }

            Assert.True(tightest >= MARGIN, $"the tightest gap between the gun and an emitter is {tightest:F3} at {where}");
        }

        [Fact]
        public void TheSweepCanBeSeenToFire()
        {
            //The sweep above only means something if it finds a gun that does touch: the same emitters moved out of the funnel
            //onto the stone, a unit and a half further from the drain's axis, are run over by the gun at its near stand (the
            //old placement on the gold band, which a keep-out had to hold the gun off)
            float corner = CornerFor(17);
            List<(Vector3 Point, float Radius)> samples = EmitterSamples(corner, corner);
            List<(Vector3, float)> moved = new();

            foreach ((Vector3 point, float radius) in samples)
            {
                Vector3 outward = Vector3.Normalize(new Vector3(point.X, 0f, point.Z));
                Vector3 shifted = point + outward * 1.5f;
                shifted.Y += ArenaIsland.FloorHeightAt(new Vector2(shifted.X, shifted.Z).Length()) - RIM_Y;
                moved.Add((shifted, radius));
            }

            Assert.True(Sweep(moved, "moved", 5).Gap < 0f, "emitters standing on the stone should be touched by the gun somewhere");
        }

        [Fact]
        public void EveryEmitterIsInsideTheFunnelAndPeeksOverItsRim()
        {
            foreach ((float cornerX, float cornerZ) in Corners())
            {
                foreach ((Vector3 point, float radius) in EmitterSamples(cornerX, cornerZ))
                    Assert.True(new Vector2(point.X, point.Z).Length() + radius < ArenaIsland.FUNNEL_TOP_RADIUS,
                        $"corner {cornerX},{cornerZ}: a sample at {point} stands outside the mouth");

                for (int k = 0; k < LaserEmitterRing.COUNT; k++)
                {
                    //The foot is on the glass: on the cone from the mouth to the drawn hole
                    Vector3 foot = LaserEmitterRing.Foot(k, cornerX, cornerZ);
                    float r = new Vector2(foot.X, foot.Z).Length();
                    float wallY = RIM_Y - (ArenaIsland.FUNNEL_TOP_RADIUS - r)
                        * (RIM_Y - ArenaIsland.FUNNEL_BOTTOM_Y) / (ArenaIsland.FUNNEL_TOP_RADIUS - ArenaIsland.FUNNEL_DRAWN_HOLE_RADIUS);
                    Assert.True(MathF.Abs(foot.Y - wallY) < 1e-4f, $"emitter {k}'s foot is {foot.Y - wallY} off the glass");
                    Assert.True(foot.Y < RIM_Y, $"emitter {k}'s foot is not under the rim");

                    //And the lens stands over the rim's plane, where the player can see it from the gun
                    Assert.True(LaserEmitterRing.Lens(k, cornerX, cornerZ, PLANE_Y).Y > RIM_Y,
                        $"corner {cornerX},{cornerZ}: emitter {k}'s lens is under the rim");
                }
            }
        }

        [Fact]
        public void EachEmitterAimsExactlyAtItsCornerOfTheNet()
        {
            foreach ((float cornerX, float cornerZ) in Corners())
                for (int k = 0; k < LaserEmitterRing.COUNT; k++)
                {
                    Vector3 corner = LaserEmitterRing.NetCorner(k, cornerX, cornerZ, PLANE_Y);
                    Vector3 lens = LaserEmitterRing.Lens(k, cornerX, cornerZ, PLANE_Y);
                    Matrix housing = LaserEmitterRing.Housing(k, cornerX, cornerZ, PLANE_Y);

                    //The drawn housing's own axis (its +Y), from the drawn lens's crest: through the corner, ahead of the lens
                    Vector3 axis = Vector3.Normalize(new Vector3(housing.M21, housing.M22, housing.M23));
                    Vector3 toCorner = corner - lens;
                    float ahead = Vector3.Dot(toCorner, axis);
                    float miss = (toCorner - axis * ahead).Length();

                    Assert.True(ahead > 0.3f, $"corner {cornerX},{cornerZ}: emitter {k}'s corner is {ahead} ahead of its lens");
                    Assert.True(miss < 1e-3f, $"corner {cornerX},{cornerZ}: emitter {k}'s axis passes {miss} from its corner");

                    //The lens the beam leaves from is the drawn lens's crest
                    Vector3 crest = Vector3.Transform(new Vector3(0f, LaserEmitterMesh.LENS_APEX_Y, 0f), housing);
                    Assert.True(Vector3.Distance(crest, lens) < 1e-4f);

                    //The housing turns on the yoke's pin and nothing else: the pin's axis is its +X, and the pin's point is on its axis
                    Matrix stand = LaserEmitterRing.Stand(k, cornerX, cornerZ);
                    Assert.True(Vector3.Distance(Vector3.Normalize(stand.Right), Vector3.Normalize(housing.Right)) < 1e-4f);
                    Vector3 pin = Vector3.Transform(new Vector3(0f, LaserEmitterMesh.PIVOT_Y, 0f), stand);
                    Assert.True(Vector3.Distance(Vector3.Transform(new Vector3(0f, LaserEmitterMesh.PIVOT_ALONG, 0f), housing), pin) < 1e-4f);
                }
        }

        [Fact]
        public void TheHousingsTailClearsThePedestalHoweverItIsAimed()
        {
            //The housing turns on the pin over the cup it stands out of: its rear face, a disc at the tail, stays over the
            //cup's top wherever it is over the cup
            foreach ((float cornerX, float cornerZ) in Corners())
                for (int k = 0; k < LaserEmitterRing.COUNT; k++)
                {
                    Matrix toStand = Matrix.Invert(LaserEmitterRing.Stand(k, cornerX, cornerZ));
                    Matrix housing = LaserEmitterRing.Housing(k, cornerX, cornerZ, PLANE_Y);

                    for (int i = 0; i < 24; i++)
                    {
                        float a = MathHelper.TwoPi * i / 24f;
                        Vector3 rim = Vector3.Transform(Vector3.Transform(new Vector3(MathF.Cos(a) * 0.29f, 0f, MathF.Sin(a) * 0.29f), housing), toStand);
                        bool overCup = new Vector2(rim.X, rim.Z).Length() < LaserEmitterMesh.CUP_RADIUS + 0.05f;

                        Assert.True(!overCup || rim.Y > LaserEmitterMesh.PEDESTAL_TOP + 0.02f,
                            $"corner {cornerX},{cornerZ}: emitter {k}'s tail dips to {rim.Y} over the cup");
                    }
                }
        }

        [Fact]
        public void TheGunOrbitsRightRoundTheDrainAtItsNearestStand()
        {
            //Nothing holds the gun off the emitters any more (the keep-out of the first placement is gone): a gun walked to
            //the near end and turned a whole circle at the keys' rate keeps its radius on every frame and moves by the same
            //step each frame, with no jump anywhere round it
            Cannon cannon = StandAt(0f, NEAR);
            Vector3 last = cannon.Position;
            float largest = 0f, smallest = float.MaxValue;
            float turned = 0f;
            int frames = 0;

            while (turned < MathHelper.TwoPi && frames < 100000)
            {
                cannon.Orbit(1f);
                cannon.Update(Frame);
                frames++;

                Assert.True(MathF.Abs(cannon.OrbitRadius - NEAR) < 1e-4f, $"frame {frames}: the radius went to {cannon.OrbitRadius}");

                float step = Vector3.Distance(cannon.Position, last);
                turned += step / NEAR;
                last = cannon.Position;

                //Past the ramp's first second, the orbit runs at its full rate
                if (frames > 120) { largest = MathF.Max(largest, step); smallest = MathF.Min(smallest, step); }
            }

            Assert.True(turned >= MathHelper.TwoPi, "the gun did not get round");
            Assert.True(largest - smallest < 1e-3f, $"the orbit's step ran from {smallest} to {largest}: it jumped somewhere");
        }
    }
}
