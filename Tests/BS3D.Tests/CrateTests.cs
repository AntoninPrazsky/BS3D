using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using NVector3 = System.Numerics.Vector3;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// #257's crate: a shot banks off a crate face as a mirror does, and the landing preview and the simulation bank it
    /// the same way — the one promise the preview exists to keep, with a wall in the flight.
    /// </summary>
    public class CrateTests
    {
        private static Crates OneCrate(NVector3 centre, NVector3 half)
        {
            Crates crates = new();
            crates.Add(new Crates.Crate(centre, half));
            return crates;
        }

        [Fact]
        public void ABallMeetsTheFaceGrownByItsRadiusAndLeavesMirrored()
        {
            //A unit-half crate at the origin, a ball of radius 0.5 coming along +X: the grown face is at x = -1.5
            Crates crates = OneCrate(NVector3.Zero, new NVector3(1f));

            Assert.True(crates.TryFindFirstFace(new NVector3(-5f, 0.2f, 0f), NVector3.UnitX, 10f, 0.5f,
                out float distance, out NVector3 normal));
            Assert.Equal(3.5f, distance, 4);
            Assert.Equal(-NVector3.UnitX, normal);

            //Mirrored: the part along the normal turns round, the rest is kept, the speed is kept
            NVector3 bounced = Crates.Reflect(new NVector3(3f, 4f, 0f), normal);
            Assert.Equal(new NVector3(-3f, 4f, 0f), bounced);
        }

        [Fact]
        public void APassingBallABallInsideAndAShortReachMeetNothing()
        {
            Crates crates = OneCrate(NVector3.Zero, new NVector3(1f));

            //Passing above the grown box
            Assert.False(crates.TryFindFirstFace(new NVector3(-5f, 1.6f, 0f), NVector3.UnitX, 10f, 0.5f, out _, out _));
            //Starting inside: it can only be there by being put there
            Assert.False(crates.TryFindFirstFace(NVector3.Zero, NVector3.UnitX, 10f, 0.5f, out _, out _));
            //The face is beyond this leg
            Assert.False(crates.TryFindFirstFace(new NVector3(-5f, 0f, 0f), NVector3.UnitX, 3f, 0.5f, out _, out _));
        }

        [Fact]
        public void AFlightEndsWhereTheBouncedLineDoes()
        {
            Crates crates = OneCrate(NVector3.Zero, new NVector3(1f));
            NVector3 position = new(-3f, 0f, 0f);
            NVector3 velocity = new(10f, 0f, 5f);

            //Meets x = -1.5 after 0.15 s, then flies back for the remaining 0.05 s
            int bounces = crates.Fly(ref position, ref velocity, 0.2f, 0.5f);

            Assert.Equal(1, bounces);
            Assert.Equal(new NVector3(-10f, 0f, 5f), velocity);
            Assert.Equal(-1.5f - 0.5f, position.X, 4);
            Assert.Equal(1f, position.Z, 4);
        }

        /// <summary>
        /// The promise: a real level hung in the real simulation, a crate standing beside it, and a shot aimed so it banks
        /// off the crate into the cluster. The ball the preview says the shot reaches is the ball the simulated shot,
        /// bounced by <see cref="Crates.BounceShots"/> in the step, touches first — and where.
        /// </summary>
        [Fact]
        public void ThePreviewAndTheSimulationBankOffACrateIntoTheSameBall()
        {
            using HungLevel hung = HungLevel.FromLevelFile(LevelPath("Pennant.json"));
            hung.Run(1f);   //settled, as a level is long before anyone fires

            //A tall crate beside the cluster, its face towards the cluster at x = 6 (grown: 5.5), long in z
            float y = LowestBallY(hung.Balls) + 1f;
            Crates crates = OneCrate(new NVector3(7f, y, -12f), new NVector3(1f, 4f, 16f));
            crates.AddStatics(hung.World.Simulation, hung.World.Events);

            //Fired from out in front, along the line to the mirror image of the cluster's middle in that face
            XVector3 muzzle = new(2f, y, -30f);
            XVector3 mirrored = new(2f * 5.5f - 0f, y, 0f);
            XVector3 velocity = XVector3.Normalize(mirrored - muzzle) * 200f;

            List<XVector3> path = new();
            Assert.True(ShotPlacement.TryFindFirstHitCurved(hung.Balls, muzzle, velocity, 1f, null,
                out PhysicsBall promised, out XVector3 promisedContact, path, crates));

            //The preview's flight turned at the face
            Assert.Contains(path, knot => MathF.Abs(knot.X - 5.5f) < 1e-3f);

            FirstTouch touch = new();
            List<PhysicsBall> shots = new();
            PhysicsBall shot = new() { BallReference = hung.World.AddShotBall(muzzle.ToNumerics(), velocity.ToNumerics(), touch) };
            shots.Add(shot);
            hung.World.PerStepForces = dt => crates.BounceShots(shots, hung.World.Events, dt, Constants.EARTH_GRAVITY);

            for (int step = 0; step < 120 && touch.Other == null; step++)
                hung.World.Step(HungLevel.TIMESTEP, () => { });

            Assert.NotNull(touch.Other);
            Assert.Equal(promised.BallReference.Handle, touch.Other.Value);

            //And it was the bounce that brought it there: the shot is moving back towards -X
            Assert.True(shot.BallReference.Velocity.Linear.X < 0f);
        }

        private sealed class FirstTouch : IContactEventHandler
        {
            public BodyHandle? Other;

            //OnContactAdded, as the game's own handler takes it: a shot's first contact is often speculative, and
            //OnStartedTouching would only report the ball it was pushed into afterwards
            public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
                XVector3 contactOffset, XVector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex)
                where TManifold : unmanaged, IContactManifold<TManifold>
            {
                if (Other != null) return;

                CollidableReference other = pair.A.Equals(eventSource) ? pair.B : pair.A;
                if (other.Mobility == CollidableMobility.Dynamic) Other = other.BodyHandle;
            }
        }

        private static float LowestBallY(PhysicsBall[,,] balls)
        {
            float lowest = float.MaxValue;
            foreach (PhysicsBall ball in balls)
                if (ball != null) lowest = MathF.Min(lowest, ball.BallReference.Pose.Position.Y);
            return lowest;
        }

        private static string LevelPath(string file)
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Game", "Levels", file);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException(file);
        }
    }
}
