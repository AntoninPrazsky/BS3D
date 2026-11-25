using Prazsky.BS3D.Physics;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <see cref="PhysicsWorld.PrimeContacts"/> (#1240): the hung cluster's contacts made while the level is built, so the
    /// world's first step, on the frame a chapter's tour hands the gun over, does not create them all at once.
    /// </summary>
    public class PrimeContactsTests
    {
        //The cavern chapter's first level, the measured one: 959 balls
        private const string LEVEL = "Eleven.json";

        [Fact]
        public void PrimingMakesTheContactsAndMovesNothing()
        {
            using HungLevel hung = HungLevel.FromLevelFile(Shipped.Level(LEVEL));
            int sockets = hung.World.Simulation.Solver.CountConstraints();
            System.Numerics.Vector3[] before = Positions(hung);

            hung.World.PrimeContacts(HungLevel.TIMESTEP);

            Assert.True(hung.World.Simulation.Solver.CountConstraints() > sockets,
                "priming made no contact: the first step would still create them all");
            System.Numerics.Vector3[] after = Positions(hung);
            for (int i = 0; i < before.Length; i++) Assert.Equal(before[i], after[i]);
        }

        [Fact]
        public void APrimedClusterSwingsAsAnUnprimedOne()
        {
            //One worker each, so the two hangings step identically but for the priming (#946)
            using HungLevel primed = HungLevel.FromLevelFile(Shipped.Level(LEVEL), workerThreads: 1);
            using HungLevel plain = HungLevel.FromLevelFile(Shipped.Level(LEVEL), workerThreads: 1);

            primed.World.PrimeContacts(HungLevel.TIMESTEP);
            primed.Run(1f);
            plain.Run(1f);

            System.Numerics.Vector3[] a = Positions(primed), b = Positions(plain);
            float worst = 0f;
            for (int i = 0; i < a.Length; i++) worst = System.MathF.Max(worst, System.Numerics.Vector3.Distance(a[i], b[i]));
            Assert.True(worst < 1e-3f, $"a primed cluster stood {worst} units off an unprimed one after a second");
        }

        private static System.Numerics.Vector3[] Positions(HungLevel hung)
        {
            var positions = new System.Collections.Generic.List<System.Numerics.Vector3>();
            foreach (PhysicsBall ball in hung.Balls)
                if (ball != null) positions.Add(ball.BallReference.Pose.Position);
            return positions.ToArray();
        }
    }
}
