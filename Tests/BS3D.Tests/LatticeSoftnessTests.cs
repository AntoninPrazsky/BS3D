using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The lattice's own spring (#690): <see cref="LatticeSoftness"/> re-describes every socket between two balls and leaves
    /// the glass's own as built, after the build and after a landing. The sockets are told apart here by the bodies they
    /// join (the ceiling body's own constraint list), not by the handle slots' convention the class itself reads, so a
    /// slip in that convention shows up as a softened anchor or a stiff socket left behind.
    /// </summary>
    public class LatticeSoftnessTests
    {
        private static readonly SpringSettings SOFT = new(3f, 0.3f);

        /// <summary>A small structure with every kind of socket: a top row on the glass, a column under it, a foot.</summary>
        private static BallsMap SmallStructure()
        {
            BallsMap map = new(5, 5, 6);
            for (byte x = 1; x <= 3; x++) map.PutBallAt(x, 2, 5, BallType.Type1);
            map.PutBallAt(2, 2, 4, BallType.Type2);
            map.PutBallAt(2, 2, 3, BallType.Type3);
            map.PutBallAt(1, 2, 3, BallType.Type3);
            return map;
        }

        [Fact]
        public void Every_ball_socket_takes_the_lattice_spring_and_every_anchor_keeps_its_own()
        {
            using HungLevel hung = new(SmallStructure());

            int described = LatticeSoftness.Apply(hung.Balls, hung.World.Simulation, SOFT);

            Assert.True(described > 0);
            AssertSprings(hung);
        }

        [Fact]
        public void A_landed_ball_joins_at_the_lattice_spring_and_its_anchor_at_its_own()
        {
            using HungLevel hung = new(SmallStructure());
            LatticeSoftness.Apply(hung.Balls, hung.World.Simulation, SOFT);

            //One under the column, and one on the top level, which also takes an anchor to the glass
            PhysicsBall low = Land(hung, new XZLevel(2, 2, 2));
            PhysicsBall top = Land(hung, new XZLevel(4, 2, 5));
            int topLevel = hung.Balls.GetLength(2) - 1;
            LatticeSoftness.ApplyToBall(low, topLevel, hung.World.Simulation, SOFT);
            LatticeSoftness.ApplyToBall(top, topLevel, hung.World.Simulation, SOFT);

            AssertSprings(hung);
        }

        [Fact]
        public void The_start_swing_hands_the_builders_spring_back_to_anchors_only_not_to_a_reused_handle()
        {
            using HungLevel hung = new(SmallStructure());
            hung.World.BeginStartSwing(hung.Balls);
            LatticeSoftness.Apply(hung.Balls, hung.World.Simulation, SOFT);
            int topLevel = hung.Balls.GetLength(2) - 1;

            //A top-level ball cut in the swing's first second frees its anchor's handle, and Bepu hands a freed handle to
            //the next constraint made. A landing that makes two sockets takes the anchor's: (2, 2, 2), found by trying every
            //free cell of this structure - and checked here, so the test fails if a change ever stops it being reused.
            hung.Run(0.2f);
            List<ConstraintHandle> anchor = new();
            hung.Balls[3, 2, topLevel].HandlesTop.CollectStored(anchor);
            List<PhysicsBall> released = new();
            BallsConstraintsBuilder.CutBall(new XZLevel(3, 2, topLevel), hung.Balls, hung.Map, hung.World.Simulation, released);
            PhysicsBall landed = Land(hung, new XZLevel(2, 2, 2));
            List<ConstraintHandle> landedSockets = new();
            landed.CollectConstraintHandles(landedSockets);
            Assert.Contains(anchor[0], landedSockets);
            LatticeSoftness.ApplyToBall(landed, topLevel, hung.World.Simulation, SOFT);

            //Past the swing's hold and ease, when it hands its sockets back the builder's spring
            hung.Run(2.5f);

            AssertSprings(hung);
        }

        /// <summary>The glass's sockets at the builder's spring, every other ball socket at <see cref="SOFT"/>.</summary>
        private static void AssertSprings(HungLevel hung)
        {
            Simulation simulation = hung.World.Simulation;
            HashSet<int> anchors = Sockets(simulation, hung.Ceiling.Handle);
            Assert.NotEmpty(anchors);

            HashSet<int> all = new();
            foreach (PhysicsBall ball in hung.Balls)
                if (ball != null) all.UnionWith(Sockets(simulation, ball.BallReference.Handle));

            Assert.True(all.Count > anchors.Count);

            foreach (int value in all)
            {
                simulation.Solver.GetDescription(new ConstraintHandle(value), out BallSocket socket);
                SpringSettings expected = anchors.Contains(value) ? BallsConstraintsBuilder.SPRING_SETTINGS : SOFT;

                Assert.Equal(expected.Frequency, socket.SpringSettings.Frequency, 3);
                Assert.Equal(expected.DampingRatio, socket.SpringSettings.DampingRatio, 3);
            }
        }

        private static HashSet<int> Sockets(Simulation simulation, BodyHandle body)
        {
            HashSet<int> found = new();
            ref var constraints = ref simulation.Bodies[body].Constraints;

            for (int i = 0; i < constraints.Count; i++)
            {
                ConstraintHandle handle = constraints[i].ConnectingConstraintHandle;
                if (simulation.Solver.HandleToConstraint[handle.Value].TypeId == BallSocket.ConstraintTypeId) found.Add(handle.Value);
            }

            return found;
        }

        /// <summary>A ball landed into <paramref name="cell"/> the way SagProbe and <c>ClusterInvariantTests</c> land one.</summary>
        private static PhysicsBall Land(HungLevel hung, XZLevel cell)
        {
            Vector3 rest = hung.Map.GetRealCenteredPosition(cell) + hung.WorldOffset;

            BodyHandle handle = hung.World.Simulation.Bodies.Add(BodyDescription.CreateDynamic(
                rest.ToNumerics(),
                new Sphere(BallsConstraintsBuilder.BALL_RADIUS).ComputeInertia(BallsConstraintsBuilder.BALL_MASS),
                new CollidableDescription(BallsConstraintsBuilder.GetSphereShapeIndex(hung.World.Simulation),
                    BallsConstraintsBuilder.SPECULATIVE_MARGIN),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));

            PhysicsBall landed = new()
            {
                BallReference = new BodyReference(handle, hung.World.Simulation.Bodies),
                Type = BallType.Type4,
                ArrayPosition = cell,
            };

            hung.Map.PutBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level, BallType.Type4);
            hung.Balls[cell.X, cell.Z, cell.Level] = landed;

            BallsConstraintsBuilder.AttachBallToStructure(landed, hung.Balls, hung.Map, hung.World.Simulation,
                hung.Ceiling, hung.WorldOffset.ToNumerics());

            return landed;
        }
    }
}
