using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
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
        /// off the crate into the cluster. The simulated shot, bounced by <see cref="Crates.BounceShots"/> in the step,
        /// flies the preview's reflected leg and reaches the cluster at the ball the preview names or a lattice neighbour
        /// of it.
        /// <para>
        /// <b>A neighbour, not the ball, and that is #696 and not the crate.</b> A bank shot meets the cluster at a slant,
        /// and on a slanting arrival Bepu's speculative contact can catch a ball beside the line before the one dead ahead;
        /// which one it catches depends on the settled cluster's pose to the last bit, and that differs between machines -
        /// this asserted the exact ball for a day, passed on the desktop and failed on the CI runner on three different
        /// neighbours (7, 9 and 15 against the promised 3). What the crate itself promises is the flight, and that is held
        /// tightly: every simulated step after the bounce lies on the preview's reflected leg.
        /// </para>
        /// <para>
        /// <b>And "the ball it touched" is every ball it touched in that step, not the first one a callback names.</b> The
        /// bank shot arrives touching four balls in one step (measured: the promised ball, two beside it on the next
        /// level and one on the level above that, two levels from the promised one), and Bepu's workers report a step's
        /// contacts in whatever order they finish, so "the first callback" was one of the four chosen by thread timing.
        /// Thirty runs of the step on a 24-core desktop gave the same four balls every time and the promised one first
        /// in 21 of them. The ball two levels away is the only one of the four that is not a neighbour, which is what the
        /// runner's failures look like (they named a ball that was no neighbour; their cells were not printed until
        /// now). The game's own handler orders a step's contacts by how far along the flight they lie for the same
        /// reason (#578); this test asks the question that does not need an order: did the shot reach the promised ball
        /// or one beside it.
        /// </para>
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
            hung.World.PerStepForces = dt => crates.BounceShots(shots, hung.World.Events, dt, Constants.EARTH_GRAVITY,
                hung.Balls, null);

            //The preview's reflected leg: from its knot at the face along the launch velocity mirrored in that face. Not
            //towards the next knot - that one is the CONTACT point on the ball's surface, up to a radius off the flight
            //line, and a chord to it parted from the true flight by 0.4 across the bank.
            int bounceKnot = path.FindIndex(knot => MathF.Abs(knot.X - 5.5f) < 1e-3f);
            XVector3 legStart = path[bounceKnot];
            XVector3 legDirection = XVector3.Normalize(new XVector3(-velocity.X, velocity.Y, velocity.Z));

            float worstOffLeg = 0f;
            int stepsOnLeg = 0;
            for (int step = 0; step < 120 && touch.Other == null; step++)
            {
                hung.World.Step(HungLevel.TIMESTEP, () => { });
                if (touch.Other != null || shot.BallReference.Velocity.Linear.X >= 0f) continue;

                //How far the simulated centre stands off the preview's leg: the straight line leaves out gravity, which
                //drops the flight about 0.04 across the bank and bent it about as much before the face
                XVector3 at = shot.BallReference.Pose.Position.ToXna() - legStart;
                worstOffLeg = MathF.Max(worstOffLeg, (at - XVector3.Dot(at, legDirection) * legDirection).Length());
                stepsOnLeg++;
            }

            Assert.NotNull(touch.Other);

            //It was the bounce that brought it there, and the flight after the bounce was the preview's
            Assert.True(shot.BallReference.Velocity.Linear.X < 0f);
            Assert.True(stepsOnLeg > 0);
            Assert.True(worstOffLeg < 0.15f, $"the simulated flight left the preview's leg by {worstOffLeg}");

            //Reached the promised ball or a lattice neighbour of it, among the balls it touched in that step (#696, the
            //<para>s above)
            HashSet<int> handles = new(touch.All);
            List<XZLevel> reached = new();
            foreach (PhysicsBall ball in hung.Balls)
                if (ball != null && handles.Contains(ball.BallReference.Handle.Value)) reached.Add(ball.ArrayPosition);

            Assert.NotEmpty(reached);

            XZLevel size = new(hung.Balls.GetLength(0), hung.Balls.GetLength(1), hung.Balls.GetLength(2));
            bool near = false;
            foreach (XZLevel cell in reached)
            {
                near |= cell.Equals(promised.ArrayPosition);
                foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(promised.ArrayPosition, size))
                    near |= neighbour.Equals(cell);
            }

            Assert.True(near, $"the shot reached {string.Join(" ", reached.ConvertAll(Cell))}, the preview named {Cell(promised.ArrayPosition)}");
        }

        /// <summary>
        /// A shot too slow to bounce is spent, not bounced (#257's review): a lossless bounce never decays, and a shot
        /// dropped off the glass onto a crate's top would have bounced there for ever with the level held open.
        /// </summary>
        [Fact]
        public void ASlowShotMeetingACrateIsSpentNotBounced()
        {
            using PhysicsWorld world = new();
            Crates crates = OneCrate(NVector3.Zero, new NVector3(1f));
            crates.AddStatics(world.Simulation, world.Events);

            FirstTouch listener = new();
            PhysicsBall shot = new() { BallReference = world.AddShotBall(new NVector3(0f, 1.52f, 0f), new NVector3(0f, -5f, 0f), listener) };
            List<PhysicsBall> spent = new();

            crates.BounceShots(new List<PhysicsBall> { shot }, world.Events, PhysicsWorld.FIXED_TIMESTEP, 0f, null, spent);

            Assert.Single(spent);
            Assert.False(world.Events.IsListener(shot.BallReference.CollidableReference));
            Assert.Equal(-5f, shot.BallReference.Velocity.Linear.Y, 4);
        }

        /// <summary>
        /// A step whose flight reaches a ball of the cluster before the face is not bounced (#257's review): the bounce
        /// puts the ball behind the face for the integrator, so Bepu would sweep the mirrored leg and fly past the ball.
        /// </summary>
        [Fact]
        public void AStepThatReachesTheClusterBeforeTheFaceIsNotBounced()
        {
            using PhysicsWorld world = new();
            Crates crates = OneCrate(new NVector3(7f, 0f, 0f), new NVector3(1f));   //grown face at x = 5.5
            crates.AddStatics(world.Simulation, world.Events);

            FirstTouch listener = new();
            PhysicsBall shot = new() { BallReference = world.AddShotBall(new NVector3(4f, 0f, 0f), new NVector3(200f, 0f, 0f), listener) };
            PhysicsBall[,,] cluster = new PhysicsBall[1, 1, 1];
            cluster[0, 0, 0] = new PhysicsBall { BallReference = world.AddShotBall(new NVector3(4.9f, 0.7f, 0f), NVector3.Zero, new FirstTouch()) };

            crates.BounceShots(new List<PhysicsBall> { shot }, world.Events, PhysicsWorld.FIXED_TIMESTEP, 0f, cluster, null);
            Assert.Equal(200f, shot.BallReference.Velocity.Linear.X, 3);

            //And without the ball in the way, the same step bounces
            crates.BounceShots(new List<PhysicsBall> { shot }, world.Events, PhysicsWorld.FIXED_TIMESTEP, 0f, null, null);
            Assert.Equal(-200f, shot.BallReference.Velocity.Linear.X, 3);
        }

        private sealed class FirstTouch : IContactEventHandler
        {
            public BodyHandle? Other;

            /// <summary>Every dynamic body contacted, in the order the workers reported them, which is no order at all.</summary>
            public readonly System.Collections.Concurrent.ConcurrentQueue<int> All = new();

            //OnContactAdded, as the game's own handler takes it: a shot's first contact is often speculative, and
            //OnStartedTouching would only report the ball it was pushed into afterwards
            public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
                XVector3 contactOffset, XVector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex)
                where TManifold : unmanaged, IContactManifold<TManifold>
            {
                CollidableReference other = pair.A.Equals(eventSource) ? pair.B : pair.A;
                if (other.Mobility != CollidableMobility.Dynamic) return;

                All.Enqueue(other.BodyHandle.Value);

                //Which of a step's simultaneous contacts is "first" is the workers' race; only that there was one is a fact
                Other ??= other.BodyHandle;
            }
        }

        private static string Cell(XZLevel cell) => $"({cell.X}, {cell.Z}, {cell.Level})";

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
