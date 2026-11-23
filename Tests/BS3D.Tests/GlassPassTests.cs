using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The front end's pass over the glass (#1214): the rules <see cref="GlassPass"/> keeps by construction, held where a
    /// reader's care could not hold them. Over the plate the lens looks a fixed tilt off straight down and keeps its air off the
    /// four cables; the excursion's heights run far, down, at the glass, and back up on the closeness's own curve; and on every
    /// shipped level, at three window shapes, from every bearing the wide turn can begin it at and at both ends of every roll,
    /// the whole flight - the blend with the wide turn included - stays clear of the plate, the cluster and the cables, never
    /// jumps, and never looks straight down or flips the frame.
    /// </summary>
    public class GlassPassTests
    {
        //The menu camera's own figures, which live in BackdropScreen (a screen the tests cannot build): its lens, the wide
        //turn's rate, and the wide leg's stand-off rules (ISLAND_FRAME_SHARE, WIDE_MAP_SHARE, WIDE_LENS_DROP, LENS_FLOOR_Y)
        private static readonly float Fov = MathF.PI / 3f;
        private const float WideRate = MathHelper.TwoPi / 90f;
        private const float IslandShare = 0.95f, WideMapShare = 0.72f, WideLensDrop = 2f;
        private static readonly float LensFloorY = ArenaIsland.TOP_Y + 4f;

        //The rolls' floors and ceilings (BackdropScreen's GLASS_* figures and their jitters)
        private static readonly (float Approach, float Close, float Retreat, float Near)[] Rolls =
        {
            (5f, 14f, 6f, 3.5f),
            (7f, 20f, 9f, 6f),
        };

        [Theory]
        [InlineData(40f, 20f)]
        [InlineData(3.5f, 20f)]
        [InlineData(40f, 4f)]
        public void TheLookIsTheTiltOffStraightDownWhereverTheLensStands(float height, float cableReach)
        {
            for (int i = 0; i < 16; i++)
            {
                float bearing = i * MathHelper.TwoPi / 16f;
                GlassPass.Pose(8f, cableReach, height, bearing, out Vector3 lens, out Vector3 target);

                Vector3 look = Vector3.Normalize(target - lens);
                float offVertical = MathF.Acos(MathHelper.Clamp(-look.Y, -1f, 1f));

                Assert.Equal(GlassPass.TILT, offVertical, 3);
                Assert.Equal(8f, target.Y, 4);
                Assert.Equal(8f + height, lens.Y, 4);

                //Inwards, from the lens's side of the axis: the top of the frame is the far side of the plate
                Assert.True(look.X * MathF.Cos(bearing) + look.Z * MathF.Sin(bearing) < 0f, $"bearing {bearing}: the look points outwards");
            }
        }

        [Fact]
        public void OverThePlateTheLensKeepsItsAirOffEveryCable()
        {
            //A small field's plate and a large one's, at every bearing and from the near height to well past the far one
            foreach (float stage in new[] { 10f, 12f, 20f })
            {
                Vector2 cable = CeilingRigging.CableOffset(stage, stage);
                float reach = cable.Length();

                for (int i = 0; i < 72; i++)
                    for (float height = 3.5f; height <= 80f; height *= 1.25f)
                    {
                        GlassPass.Pose(0f, reach, height, i * MathHelper.TwoPi / 72f, out Vector3 lens, out _);

                        float nearest = NearestCable(lens, cable.X, cable.Y);
                        Assert.True(nearest >= GlassPass.CABLE_AIR - 1e-3f,
                            $"field {stage}, bearing step {i}, height {height}: {nearest:F2} off a cable");
                    }
            }
        }

        [Theory]
        [InlineData(0.2f, 0f)]
        [InlineData(0.9f, MathHelper.PiOver2)]
        [InlineData(2.5f, MathHelper.Pi)]
        [InlineData(6.1f, MathHelper.TwoPi)]
        public void ThePassArrivesOverTheMiddleOfTheNearestSide(float bearing, float side)
        {
            Assert.Equal(side, GlassPass.FirstBearing(bearing), 4);
        }

        [Fact]
        public void TheHeightRunsFarDownAtTheGlassAndBackUpAheadOfTheRetreat()
        {
            const float approach = 5f, close = 14f, retreat = 6f;

            //Far through the whole approach, while the lens comes over
            for (float since = 0f; since <= approach; since += 0.25f)
                Assert.Equal(0f, GlassPass.Depth(since, approach, close, retreat));

            //Down within the descent's share of the close leg, and at the glass for the rest of it
            Assert.Equal(1f, GlassPass.Depth(approach + close * GlassPass.DESCENT_SHARE, approach, close, retreat), 4);
            Assert.Equal(1f, GlassPass.Depth(approach + close * 0.95f, approach, close, retreat), 4);

            //The retreat climbs ahead of the closeness's fall, and is back at the far height within its climb's share
            for (float t = 0.25f; t < retreat; t += 0.25f)
                Assert.True(GlassPass.Depth(approach + close + t, approach, close, retreat)
                    < GlassPass.Closeness(approach + close + t, approach, close, retreat), $"{t} s into the retreat");
            Assert.Equal(0f, GlassPass.Depth(approach + close + retreat * GlassPass.CLIMB_SHARE, approach, close, retreat), 4);

            //Geometric between the heights: halfway down is the geometric mean
            Assert.Equal(MathF.Sqrt(40f * 4f), GlassPass.HeightAt(40f, 4f, 0.5f), 3);
        }

        [Fact]
        public void ClearAboveWantsTheFaceAndTheCables()
        {
            Assert.True(GlassPass.ClearAbove(new Vector3(1f, 10f + GlassPass.HANG_ABOVE + 0.1f, -2f), 10f, 6f, 6f));
            Assert.False(GlassPass.ClearAbove(new Vector3(1f, 10f + GlassPass.HANG_ABOVE - 0.1f, -2f), 10f, 6f, 6f));

            //On a cable, high over the face
            Assert.False(GlassPass.ClearAbove(new Vector3(-6.2f, 30f, 5.9f), 10f, 6f, 6f));
            Assert.True(GlassPass.ClearAbove(new Vector3(-6f + 1.2f, 30f, 6f), 10f, 6f, 6f));
        }

        [Fact]
        public void TheExcursionsShapeIsTheFlightsOwn()
        {
            Assert.Equal(0f, GlassPass.Closeness(-1f, 5f, 14f, 6f));
            Assert.Equal(0.5f, GlassPass.Closeness(2.5f, 5f, 14f, 6f), 4);
            Assert.Equal(1f, GlassPass.Closeness(10f, 5f, 14f, 6f));
            Assert.Equal(0.5f, GlassPass.Closeness(22f, 5f, 14f, 6f), 4);
            Assert.Equal(0f, GlassPass.Closeness(30f, 5f, 14f, 6f));
        }

        /// <summary>
        /// The whole flight, blend and all, over every shipped level: the lens never enters the box everything the map hangs
        /// stands in (the footprint, from the cluster's lowest ball to the winches on the glass) with the cluster's own pass's
        /// air round it (CLOSE_CLEARANCE, 1.5); over the face it keeps its air off every cable; it moves no faster than a
        /// flight should, so nothing jumps; and it never looks within the tilt of straight down nor turns the frame round. The
        /// framing is <c>BackdropScreen.SolveFraming</c>'s (the same lines over the same helpers), the wide pose its
        /// <c>WideRadius</c>'s, and the far height is capped in a second run as the lowest roof over the island caps it: the
        /// Big Top's crown (<c>CircusTentConfig.CrownY</c>, the canvas a little over it) less <see cref="GlassPass.ROOF_AIR"/>.
        /// </summary>
        [Theory]
        [InlineData(1.7778f)]
        [InlineData(2.4f)]
        [InlineData(1.25f)]
        public void ShippedLevelsFlyThePassClearOfThePlateTheClusterAndTheCables(float aspect)
        {
            const float clusterAir = 1.5f, dt = 1f / 30f, fastest = 40f;

            float vertical = Fov * Constants.HALF;
            float horizontal = MathF.Atan(MathF.Tan(vertical) * aspect);

            List<string> failures = new();
            float tightestCluster = float.MaxValue, tightestCable = float.MaxValue, steepest = float.MaxValue, fastestSeen = 0f;

            foreach (string file in Shipped.LevelFiles())
            {
                Framing f = FramingFor(file);

                float wide = MathF.Max(StandOff(ArenaIsland.RADIUS, horizontal, IslandShare),
                    MathF.Max(StandOff(f.HalfHeight, vertical, WideMapShare), StandOff(f.SpanXZ, horizontal, WideMapShare)));
                float wideY = MathF.Max(LensFloorY, f.CentreY - WideLensDrop);

                foreach (float roof in new[] { float.PositiveInfinity, new CircusTentConfig().CrownY - GlassPass.ROOF_AIR })
                    foreach (var roll in Rolls)
                        for (int start = 0; start < 12; start++)
                        {
                            float angle = start * MathHelper.TwoPi / 12f + 0.13f;
                            float far = MathF.Max(MathF.Min(GlassPass.FarHeight(f.SpanXZ, MathF.Min(vertical, horizontal)), roof - f.PlateTopY), 3.5f);
                            float near = MathF.Min(roll.Near, far);
                            float bearing0 = GlassPass.FirstBearing(angle + WideRate * roll.Approach);
                            float swing = 0f;
                            bool swingSet = false;
                            Vector3 previous = default;

                            for (float since = 0f; since <= roll.Approach + roll.Close + roll.Retreat; since += dt)
                            {
                                float closeness = GlassPass.Closeness(since, roll.Approach, roll.Close, roll.Retreat);
                                float height = GlassPass.HeightAt(far, near, GlassPass.Depth(since, roll.Approach, roll.Close, roll.Retreat));
                                float bearing = bearing0 + GlassPass.TURN * GlassPass.Turn(since, roll.Approach, roll.Close);

                                GlassPass.Pose(f.PlateTopY, f.CableReach, height, bearing, out Vector3 lens, out Vector3 aim);

                                float raw = bearing - angle;
                                swing = swingSet ? GlassPass.Unwrap(swing, raw) : MathHelper.WrapAngle(raw);

                                Vector3 position = new(MathF.Cos(angle) * wide, wideY, MathF.Sin(angle) * wide);
                                Vector3 target = new(0f, f.CentreY, 0f);
                                float fov = Fov;
                                GlassPass.Blend(closeness, angle, swing, lens, aim, Fov, ref position, ref target, ref fov);

                                string at = $"{Path.GetFileName(file)} at {aspect}, start {start}, roll near {roll.Near}, roof {roof:F0}, {since:F2} s";

                                //Out of the box the map stands in: over the winches, under the lowest ball, or beside the footprint
                                float clusterMargin = MathF.Max(
                                    MathF.Max(position.Y - (f.PlateTopY + GlassPass.HANG_ABOVE), f.BottomY - clusterAir - position.Y),
                                    MathF.Max(MathF.Abs(position.X) - (f.HalfX + clusterAir), MathF.Abs(position.Z) - (f.HalfZ + clusterAir)));
                                tightestCluster = MathF.Min(tightestCluster, clusterMargin);
                                if (clusterMargin < 0f) failures.Add($"{at}: {clusterMargin:F2} inside the box the map stands in");

                                //Off every cable while over the face
                                if (position.Y > f.PlateTopY)
                                {
                                    float cable = NearestCable(position, f.CableX, f.CableZ);
                                    tightestCable = MathF.Min(tightestCable, cable);
                                    if (cable < GlassPass.CABLE_AIR * 0.5f) failures.Add($"{at}: {cable:F2} off a cable");
                                }

                                //Never within the tilt of straight down, and never looking outwards (the frame would turn over)
                                Vector3 look = Vector3.Normalize(target - position);
                                float offVertical = MathF.Acos(MathHelper.Clamp(-look.Y, -1f, 1f));
                                steepest = MathF.Min(steepest, offVertical);
                                if (offVertical < GlassPass.TILT - 1e-3f) failures.Add($"{at}: {MathHelper.ToDegrees(offVertical):F1} deg off straight down");
                                if (look.X * position.X + look.Z * position.Z > 1e-3f) failures.Add($"{at}: the look points outwards");

                                //No jump
                                if (swingSet)
                                {
                                    float speed = Vector3.Distance(previous, position) / dt;
                                    fastestSeen = MathF.Max(fastestSeen, speed);
                                    if (speed > fastest) failures.Add($"{at}: {speed:F1} units/s");
                                }

                                swingSet = true;
                                previous = position;
                                angle += WideRate * dt;
                                if (angle >= MathHelper.TwoPi) angle -= MathHelper.TwoPi;
                            }
                        }
            }

            Assert.True(failures.Count == 0,
                $"{failures.Count} failures (tightest: cluster {tightestCluster:F2}, cable {tightestCable:F2}, steepest {MathHelper.ToDegrees(steepest):F1} deg, fastest {fastestSeen:F1}/s); first: {(failures.Count > 0 ? failures[0] : "")}");
        }

        /// <summary>
        /// The plate's top face is a plate's thickness over the top level's crowns, which is how <c>BackdropScreen.SolveFraming</c>
        /// finds it from the framing's figures: the ceiling socket hangs the top level with its crowns at the underside
        /// (<see cref="BallsConstraintsBuilder.CeilingRestY"/>).
        /// </summary>
        [Theory]
        [InlineData(0f)]
        [InlineData(12.5f)]
        [InlineData(-3f)]
        public void ThePlatesTopFaceIsAThicknessOverTheTopLevelsCrowns(float ceilingCentreY)
        {
            float crowns = BallsConstraintsBuilder.CeilingRestY(ceilingCentreY) + BallRenderSet.BALL_RADIUS;

            Assert.Equal(CeilingPlate.TopFaceY(ceilingCentreY), crowns + CeilingPlate.THICKNESS, 4);
        }

        //The horizontal distance from the lens to the nearest of the four cables at (+-x, +-z)
        private static float NearestCable(Vector3 lens, float x, float z)
        {
            float dx = MathF.Abs(lens.X) - x, dz = MathF.Abs(lens.Z) - z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private static float StandOff(float reach, float halfAngle, float share) => reach / MathF.Sin(halfAngle * share);

        private readonly record struct Framing(float HalfX, float HalfZ, float HalfHeight, float CentreY, float PlateTopY, float CableX, float CableZ)
        {
            public float SpanXZ => MathF.Sqrt(HalfX * HalfX + HalfZ * HalfZ);
            public float BottomY => CentreY - HalfHeight;
            public float CableReach => MathF.Sqrt(CableX * CableX + CableZ * CableZ);
        }

        //BackdropScreen.SolveFraming over a shipped level, hung where the front end hangs it (SolveHang)
        private static Framing FramingFor(string file)
        {
            BallsMap map = new(Level.Load(file).Map);
            map.Center();

            ClusterHang.FitWorldOffset(map, out float fieldTopY);
            float topLevelY = BallsConstraintsBuilder.CeilingRestY(CeilingPlate.CentreYAbove(fieldTopY));

            XZLevel size = map.GetStaticBallsArraySize();
            byte topLevel = (byte)(size.Level - 1);
            float topY = topLevelY + BallRenderSet.BALL_RADIUS;
            float bottomY = topLevelY - (topLevel - map.GetLowestOccupiedLevel()) / Constants.SQRT_TWO - BallRenderSet.BALL_RADIUS;
            float halfX = CeilingPlate.FootprintFor(map.StageSizeX) * Constants.HALF;
            float halfZ = CeilingPlate.FootprintFor(map.StageSizeZ) * Constants.HALF;
            Vector2 cable = CeilingRigging.CableOffset(map.StageSizeX, map.StageSizeZ);

            return new Framing(halfX, halfZ, (topY - bottomY) * Constants.HALF, (topY + bottomY) * Constants.HALF,
                topY + CeilingPlate.THICKNESS, cable.X, cable.Y);
        }
    }
}
