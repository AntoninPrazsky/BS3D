using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The ceiling-anchor regression of #561. A ball landing in a free cell of the TOP level is tied to the
    /// glass as well as to its neighbours, and that anchor is the ball's world X/Z written into the plate's
    /// frame — so <see cref="BallsConstraintsBuilder.AttachBallToStructure"/> has to be handed the world offset
    /// the structure was built with. <see cref="Prazsky.BS3D.Levels.ClusterHang.FitWorldOffset"/> was −6.5 to
    /// −8.5 in X and Z across the shipped levels, and without it the new ball was anchored that far off its
    /// cell and settled several units from where its neighbours hold it. Since <c>BallsMap.Center</c> centres on
    /// the top level's midpoint the fit's own X/Z is at most a few cells, so the level is hung
    /// <see cref="LATERAL"/> further off the axis on purpose: the attach must honour whatever offset the
    /// structure was built with, and a small one would not tell a missing offset from a present one.
    /// </summary>
    public class TopLevelAttachTests
    {
        /// <summary>How long the attached ball is left hanging before it is measured.</summary>
        private const float SETTLE_SECONDS = 6f;

        /// <summary>How far off its lattice offset from its neighbour the ball may settle.</summary>
        private const float TOLERANCE = 0.1f;

        /// <summary>
        /// A shipped level whose top level has free cells beside occupied ones. Named rather than searched for,
        /// so the gate always measures the same thing; the test asserts the cell it needs is there.
        /// </summary>
        private const string LEVEL = "Anchor.json";

        /// <summary>The deliberate extra offset in X and Z, in world units.</summary>
        private static readonly Vector3 LATERAL = new(5f, 0f, -4f);

        [Fact]
        public void BallAttachedToTheTopLevelStaysInItsCell()
        {
            float error = AttachAndMeasure(useWorldOffset: true, out float worldOffsetXZ);

            Assert.True(worldOffsetXZ > 1f, $"the level's world offset is only {worldOffsetXZ:0.00} in X/Z, which would not tell a missing offset from a present one");
            Assert.True(error < TOLERANCE, $"the attached ball settled {error:0.000} off its lattice position relative to its neighbour");
        }

        /// <summary>
        /// Hangs <see cref="LEVEL"/>, lands one ball in a free top-level cell beside an occupied one exactly as
        /// SagProbe lands a shot (a body at the cell's world position, placed in the map and the array, then
        /// <see cref="BallsConstraintsBuilder.AttachBallToStructure"/>), runs the world for
        /// <see cref="SETTLE_SECONDS"/> and returns how far the pair's separation is from the lattice's.
        /// </summary>
        /// <param name="useWorldOffset">False passes a zero offset to the attach — the #561 defect — which is
        /// how the check was seen to fail (docs/formats-and-tools.md records the numbers).</param>
        internal static float AttachAndMeasure(bool useWorldOffset, out float worldOffsetXZ)
        {
            using HungLevel hung = HungLevel.FromLevelFile(Shipped.Level(LEVEL), LATERAL);

            worldOffsetXZ = new Vector2(hung.WorldOffset.X, hung.WorldOffset.Z).Length();

            Assert.True(TryFindFreeTopCell(hung.Map, out XZLevel cell, out XZLevel neighbour),
                $"{LEVEL} has no free top-level cell beside an occupied one any more; pick another level");

            //Let the untouched cluster settle first, as a level does before the first shot lands
            hung.Run(1f);

            Vector3 rest = hung.Map.GetRealCenteredPosition(cell) + hung.WorldOffset;

            BodyHandle handle = hung.World.Simulation.Bodies.Add(BodyDescription.CreateDynamic(
                rest.ToNumerics(),
                new Sphere(BallsConstraintsBuilder.BALL_RADIUS).ComputeInertia(BallsConstraintsBuilder.BALL_MASS),
                new CollidableDescription(BallsConstraintsBuilder.GetSphereShapeIndex(hung.World.Simulation),
                    BallsConstraintsBuilder.SPECULATIVE_MARGIN),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));

            BallType type = hung.Map.GetStaticBallsArray()[neighbour.X, neighbour.Z, neighbour.Level].Type;

            PhysicsBall landed = new()
            {
                BallReference = new BodyReference(handle, hung.World.Simulation.Bodies),
                Type = type,
                ArrayPosition = cell,
            };

            hung.Map.PutBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level, type);
            hung.Balls[cell.X, cell.Z, cell.Level] = landed;

            BallsConstraintsBuilder.AttachBallToStructure(landed, hung.Balls, hung.Map, hung.World.Simulation,
                hung.Ceiling, useWorldOffset ? hung.WorldOffset.ToNumerics() : System.Numerics.Vector3.Zero);

            hung.Run(SETTLE_SECONDS);

            System.Numerics.Vector3 actual = landed.BallReference.Pose.Position
                - hung.Balls[neighbour.X, neighbour.Z, neighbour.Level].BallReference.Pose.Position;
            Vector3 lattice = hung.Map.GetRealCenteredPosition(cell) - hung.Map.GetRealCenteredPosition(neighbour);

            return (actual.ToXna() - lattice).Length();
        }

        /// <summary>
        /// The first free top-level cell (by X, then Z) with an occupied same-level neighbour. Only a same-level
        /// neighbour is asked for: on the top level nothing is above, and one below is not required.
        /// </summary>
        private static bool TryFindFreeTopCell(BallsMap map, out XZLevel cell, out XZLevel neighbour)
        {
            StaticBall[,,] balls = map.GetStaticBallsArray();
            XZLevel size = map.GetStaticBallsArraySize();
            int top = size.Level - 1;

            for (int x = 0; x < size.X; x++)
                for (int z = 0; z < size.Z; z++)
                {
                    if (balls[x, z, top] != null) continue;

                    foreach (XZLevel n in BallsMap.GetNeighboringCells(new XZLevel(x, z, top), size))
                        if (n.Level == top && balls[n.X, n.Z, n.Level] != null)
                        {
                            cell = new XZLevel(x, z, top);
                            neighbour = n;
                            return true;
                        }
                }

            cell = neighbour = default;
            return false;
        }
    }
}
