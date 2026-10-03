using BepuPhysics;
using BepuPhysics.Collidables;
using Prazsky.BS3D.Physics;
using System;
using System.Collections.Generic;
using Xunit;
using NVector3 = System.Numerics.Vector3;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// <see cref="ShotPlacement.TryFindFirstHitOnSegment"/> over a flight cut into steps (#696). The preview's stepped flight
    /// (wells, crates, the world's gravity) and the handler's per-step sweep both ask it about a run no longer than a step,
    /// where it used to lose a ball whose closest approach lay past the segment's end: the surfaces meet half a chord before
    /// that, inside the segment, but the ball's centre was beyond it, so the segment did not see it - and the next segment
    /// did not either, because the touch had begun before its start. The ghost then named a later ball, or none, and a shot
    /// landed beside it. The unbounded straight line never had the fault, which is why the preview was honest until a flight
    /// was cut into steps.
    /// </summary>
    public class ShotPlacementSegmentTests
    {
        private static readonly float Sum = 2f * BallsConstraintsBuilder.BALL_RADIUS;

        private sealed class Scene : IDisposable
        {
            private readonly PhysicsWorld _world = new();
            public readonly PhysicsBall[,,] Balls;

            public Scene(params NVector3[] centres)
            {
                Balls = new PhysicsBall[centres.Length, 1, 1];

                for (int i = 0; i < centres.Length; i++)
                {
                    BodyHandle handle = _world.Simulation.Bodies.Add(BodyDescription.CreateKinematic(centres[i],
                        new CollidableDescription(BallsConstraintsBuilder.GetSphereShapeIndex(_world.Simulation), 0.1f),
                        new BodyActivityDescription(0.01f)));

                    Balls[i, 0, 0] = new PhysicsBall { BallReference = new BodyReference(handle, _world.Simulation.Bodies) };
                }
            }

            public void Dispose() => _world.Dispose();
        }

        [Fact]
        public void ABallWhoseClosestApproachIsPastTheSegmentIsStillMetInsideIt()
        {
            //A ball dead ahead at 10: the surfaces meet at 9, a segment that ends at 9.5 holds the touch, and the closest
            //approach (10) lies beyond it
            using Scene scene = new(new NVector3(0f, 0f, 10f));

            Assert.True(ShotPlacement.TryFindFirstHitOnSegment(scene.Balls, XVector3.Zero, XVector3.UnitZ, 9.5f, Sum,
                out PhysicsBall hit, out XVector3 contact, out float distance));

            Assert.Same(scene.Balls[0, 0, 0], hit);
            Assert.Equal(9f, distance, 3);
            Assert.Equal(9.5f, contact.Z, 3);
        }

        [Fact]
        public void ASegmentThatEndsBeforeTheTouchMeetsNothing()
        {
            using Scene scene = new(new NVector3(0f, 0f, 10f));

            Assert.False(ShotPlacement.TryFindFirstHitOnSegment(scene.Balls, XVector3.Zero, XVector3.UnitZ, 8.9f, Sum,
                out _, out _, out _));
        }

        [Fact]
        public void ABallTheSegmentStartsInsideIsNotAHit()
        {
            //Already overlapping at the origin: it is the step before's to have met, and is not met twice
            using Scene scene = new(new NVector3(0f, 0f, 0.6f));

            Assert.False(ShotPlacement.TryFindFirstHitOnSegment(scene.Balls, XVector3.Zero, XVector3.UnitZ, 3f, Sum,
                out _, out _, out _));
        }

        /// <summary>
        /// The property that matters: cut a straight line into steps of the length a shot travels in one (200 u/s over 1/120 s),
        /// and the first ball met is the one the whole line meets, in scene after scene of balls at random.
        /// </summary>
        [Fact]
        public void AFlightCutIntoStepsMeetsTheSameFirstBallAsTheWholeLine()
        {
            Random rng = new(696);
            float step = 200f * PhysicsWorld.FIXED_TIMESTEP;
            int compared = 0;

            for (int trial = 0; trial < 60; trial++)
            {
                NVector3[] centres = new NVector3[40];
                for (int i = 0; i < centres.Length; i++)
                    centres[i] = new NVector3((float)(rng.NextDouble() * 8 - 4), (float)(rng.NextDouble() * 8 - 4), (float)(rng.NextDouble() * 8 - 4));

                using Scene scene = new(centres);

                XVector3 origin = new((float)(rng.NextDouble() * 4 - 2), (float)(rng.NextDouble() * 4 - 2), -20f);
                XVector3 target = new((float)(rng.NextDouble() * 6 - 3), (float)(rng.NextDouble() * 6 - 3), 0f);
                XVector3 aim = XVector3.Normalize(target - origin);

                bool whole = ShotPlacement.TryFindFirstHit(scene.Balls, origin, aim, Sum, out PhysicsBall wholeHit, out _);

                PhysicsBall steppedHit = null;
                for (float flown = 0f; flown < 60f && steppedHit == null; flown += step)
                    ShotPlacement.TryFindFirstHitOnSegment(scene.Balls, origin + aim * flown, aim, step, Sum, out steppedHit, out _, out _);

                Assert.Equal(whole, steppedHit != null);
                if (!whole) continue;

                Assert.Same(wholeHit, steppedHit);
                compared++;
            }

            //Not a vacuous pass: most of the lines meet something
            Assert.True(compared >= 30, $"only {compared} of 60 lines met a ball");
        }
    }
}
