using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
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
    /// The front end's pass at the gun (#1040): the rules <see cref="GunPass"/> keeps by construction, held where a reader's
    /// care could not hold them. The lens stands a stand-off from the gun on its outer side through the whole sweep, never in
    /// the cluster's sphere and never past the scene's open ground, and on every shipped level it is already outside the
    /// sphere before the push-out has to move it.
    /// </summary>
    public class GunPassTests
    {
        private static readonly Vector3 Gun = new(0f, -6.7f, 17.5f);

        [Fact]
        public void TheMiddleOfTheSweepIsStraightBehindTheGun()
        {
            GunPass.Pose(Gun, 8f, 3f, 1f, 0.5f, out Vector3 lens, out Vector3 aim);

            Assert.Equal(Gun.X, lens.X, 3);
            Assert.Equal(Gun.Z + 8f, lens.Z, 3);
            Assert.Equal(Gun.Y + 3f, lens.Y, 3);
            Assert.Equal(Gun.Y + GunPass.TARGET_LIFT, aim.Y, 3);
            Assert.Equal(Gun.Z, aim.Z, 3);
        }

        [Fact]
        public void TheSweepRunsFromOneFlankToTheOtherWhateverItsDirection()
        {
            GunPass.Pose(Gun, 8f, 3f, 1f, 0f, out Vector3 start, out _);
            GunPass.Pose(Gun, 8f, 3f, 1f, 1f, out Vector3 end, out _);
            GunPass.Pose(Gun, 8f, 3f, -1f, 0f, out Vector3 startBack, out _);

            //Level with the gun on one flank, then the other: a half turn about it, its width the stand-off either side
            Assert.Equal(Gun.Z, start.Z, 2);
            Assert.Equal(Gun.Z, end.Z, 2);
            Assert.Equal(8f, MathF.Abs(start.X - Gun.X), 2);
            Assert.Equal(-(start.X - Gun.X), end.X - Gun.X, 2);

            //The other direction starts where this one ends
            Assert.Equal(end.X, startBack.X, 2);
        }

        [Theory]
        [InlineData(0f, 17.5f, 5f)]
        [InlineData(17.5f, 0f, -6f)]
        [InlineData(-12f, -12f, 7f)]
        public void TheLensStaysOnTheGunsOuterSideThroughTheSweep(float x, float z, float y)
        {
            Vector3 gun = new(x, y, z);
            float gunReach = MathF.Sqrt(x * x + z * z);

            foreach (float direction in new[] { -1f, 1f })
                for (int step = 0; step <= 40; step++)
                {
                    GunPass.Pose(gun, 9f, 4f, direction, step / 40f, out Vector3 lens, out _);

                    //No nearer the axis than the gun is, so the sweep never goes in under the cluster's footprint
                    Assert.True(MathF.Sqrt(lens.X * lens.X + lens.Z * lens.Z) >= gunReach - 1e-3f,
                        $"direction {direction}, step {step}: the lens is nearer the axis than the gun");
                }
        }

        [Theory]
        [InlineData(1f)]
        [InlineData(-1f)]
        [InlineData(0.3f)]
        public void TheSweepStartsOnTheSideTheLensIsAlreadyOn(float side)
        {
            //A wide lens off to one side of the gun's outward line
            Vector3 tangent = new(-1f, 0f, 0f);
            Vector3 lens = new Vector3(0f, 0f, 40f) + tangent * 25f * MathF.Sign(side);

            float direction = GunPass.DirectionFrom(Gun, lens);
            GunPass.Pose(Gun, 9f, 4f, direction, 0f, out Vector3 start, out _);

            Assert.True(Vector3.Dot(start - Gun, tangent) * Vector3.Dot(lens - Gun, tangent) > 0f,
                "the pass starts on the far side of the gun from the lens, so the approach would cross it");
        }

        [Fact]
        public void KeepClearLeavesALensOutsideAloneAndPutsOneInsideOnTheSurface()
        {
            Vector3 centre = new(0f, 3f, 0f);

            Vector3 outside = new(20f, 3f, 0f);
            Assert.Equal(outside, GunPass.KeepClear(outside, centre, 15f));

            Vector3 inside = new(4f, 6f, -2f);
            Vector3 pushed = GunPass.KeepClear(inside, centre, 15f);

            Assert.Equal(15f, Vector3.Distance(pushed, centre), 3);

            //Straight out along the way it was already off the centre
            Assert.Equal(0f, Vector3.Cross(Vector3.Normalize(inside - centre), Vector3.Normalize(pushed - centre)).Length(), 4);

            //At the centre it still goes somewhere: out, and clear
            Assert.Equal(15f, Vector3.Distance(GunPass.KeepClear(centre, centre, 15f), centre), 3);
        }

        [Fact]
        public void KeepClearIsContinuous()
        {
            //A lens that grazes the sphere slides round it: a step of a hundredth of a unit moves the result by little more than that.
            //The path dips 2 units into the sphere and no nearer its middle; a path through the middle would flip the push-out from
            //one side to the other, which no lens that is held on the gun's outer side comes near
            Vector3 centre = Vector3.Zero;
            Vector3 previous = GunPass.KeepClear(new Vector3(-20f, 4f, 10f), centre, 12f);

            for (int i = 1; i <= 4000; i++)
            {
                Vector3 now = GunPass.KeepClear(new Vector3(-20f + i * 0.01f, 4f, 10f), centre, 12f);

                Assert.True(Vector3.Distance(previous, now) <= 0.03f, $"a jump of {Vector3.Distance(previous, now)} at step {i}");
                previous = now;
            }
        }

        [Fact]
        public void KeepInsideBringsALensInToTheOpenGroundAndLeavesItsHeight()
        {
            Vector3 lens = new(30f, -2f, 40f);

            Vector3 held = GunPass.KeepInside(lens, 35.2f);

            Assert.Equal(35.2f, MathF.Sqrt(held.X * held.X + held.Z * held.Z), 3);
            Assert.Equal(lens.Y, held.Y);

            //A scene with no clearing (infinity), or a nonsense one, leaves the lens where it is
            Assert.Equal(lens, GunPass.KeepInside(lens, float.PositiveInfinity));
            Assert.Equal(lens, GunPass.KeepInside(lens, 0f));
            Assert.Equal(lens, GunPass.KeepInside(lens, 80f));
        }

        /// <summary>
        /// The sweep is on the gun's outer side, and the gun is held outside the footprint by <see cref="GameCameraFit"/>, so on a
        /// shipped map the lens should be outside the cluster's bounding sphere with the pass's air to spare <b>without</b> the
        /// push-out. The push-out is the guarantee for a map the shipped set does not have; if it were doing the work on the set
        /// the pass would not be the circle round the gun it is meant to be. The framing is <c>BackdropScreen.SolveFraming</c>'s
        /// (the same four lines over the same helpers) and the gun is the session's fit at the widest and narrowest windows.
        /// </summary>
        [Theory]
        [InlineData(1.7778f)]
        [InlineData(2.4f)]
        [InlineData(1.25f)]
        public void ShippedLevelsNeedNoPushOutAroundTheGun(float aspect)
        {
            //The barrel's reach is the rig's own (a graphics device builds it); the figure is its measured one (3.40, the [menu] gun placed line) rounded up
            const float barrelReach = 4f;
            const float air = 3.5f;

            //What the pass rolls: the narrowest and widest stand-off, the lowest and highest lift
            float[] standOffs = { 7.5f, 12f };
            float[] lifts = { 3f, 5f };

            List<string> failures = new();
            float tightest = float.MaxValue;

            foreach (string file in Shipped.LevelFiles())
            {
                BallsMap map = new(Level.Load(file).Map);
                map.Center();

                Vector3 offset = ClusterHang.FitWorldOffset(map, out float fieldTopY);
                float ceilingCentreY = CeilingPlate.CentreYAbove(fieldTopY);
                float topLevelY = BallsConstraintsBuilder.CeilingRestY(ceilingCentreY);

                //The cluster's bounding sphere, off BackdropScreen.SolveFraming
                XZLevel size = map.GetStaticBallsArraySize();
                byte topLevel = (byte)(size.Level - 1);
                float topY = topLevelY + BallRenderSet.BALL_RADIUS;
                float bottomY = topLevelY - (topLevel - map.GetLowestOccupiedLevel()) / Constants.SQRT_TWO - BallRenderSet.BALL_RADIUS;
                float centreY = (topY + bottomY) * Constants.HALF;
                float halfX = CeilingPlate.FootprintFor(map.StageSizeX) * Constants.HALF;
                float halfZ = CeilingPlate.FootprintFor(map.StageSizeZ) * Constants.HALF;
                float halfHeight = (topY - bottomY) * Constants.HALF;
                float span = MathF.Sqrt(halfX * halfX + halfZ * halfZ + halfHeight * halfHeight);

                //The gun as a session fits it for this field
                Cannon cannon = new(new Vector3(0f, 5f, 0f), 20f);
                float framedTop = MathF.Min(CeilingPlate.TopFaceY(ceilingCentreY), offset.Y + 17f / Constants.SQRT_TWO);
                CameraFit fit = GameCameraFit.Solve(cannon, barrelReach, halfX, halfZ, offset.Y, framedTop, MathF.PI / 4.2f, aspect);
                cannon.OrbitRadius = fit.CannonOrbitRadius;

                Vector3 centre = new(0f, centreY, 0f);

                foreach (float standOff in standOffs)
                    foreach (float lift in lifts)
                        foreach (float direction in new[] { -1f, 1f })
                            for (int step = 0; step <= 40; step++)
                            {
                                GunPass.Pose(cannon.Position, standOff, lift, direction, step / 40f, out Vector3 lens, out _);

                                float margin = Vector3.Distance(lens, centre) - (span + air);
                                tightest = MathF.Min(tightest, margin);

                                if (margin < 0f) failures.Add($"{Path.GetFileName(file)} at {aspect}: stand-off {standOff}, lift {lift}, step {step}: {margin:F2} inside");
                            }
            }

            Assert.True(failures.Count == 0,
                $"{failures.Count} lenses needed the push-out (tightest margin {tightest:F2}); first: {(failures.Count > 0 ? failures[0] : "")}");
        }
    }
}
