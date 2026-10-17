using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The bench under the meadow's old oak (#891): the owner could hardly find it, because it was built at one unit to a
    /// metre in a scene whose metre is two (the fence's, #698, and the bales', #864). What is checked here is what can be
    /// stated without a device: the mesh's size against the tree it stands by, its bounding sphere (the culling and the
    /// sun's shadow casting read it), and that it stands clear of the bare ground and the roots at the foot of the oak.
    /// How it reads on the screen is the eye's.
    /// </summary>
    public class MeadowBenchTests
    {
        //The knoll oak's trunk at its widest, as MeadowTreeMesh grows it (the oak's 1.3 radius, the seed's 0.92..1.08)
        //and MeadowScatter plants it
        private const float OAK_TRUNK_RADIUS_MAX = 1.3f * 1.08f * MeadowScatter.KNOLL_OAK_SCALE;

        //Its longest surface root, from the same figures: a root reaches 3.2 trunk radii out from the axis at the most
        private const float OAK_ROOT_REACH_MAX = OAK_TRUNK_RADIUS_MAX * 3.2f;

        private static (float X, float Z) Knoll() => MeadowPath.PathEnd(new MeadowSceneConfig());

        [Fact]
        public void TheBenchIsAsLongAsTheOaksTrunkIsThick()
        {
            //The fault: a bench shorter than the trunk beside it is thick (1.9 against 2.75 to 3.2)
            var (vertices, _, _) = MeadowScatter.BenchGeometry();
            float min = float.MaxValue, max = float.MinValue;
            foreach (var vertex in vertices)
            {
                min = Math.Min(min, vertex.Position.X);
                max = Math.Max(max, vertex.Position.X);
            }

            Assert.True(max - min >= 2f * OAK_TRUNK_RADIUS_MAX,
                $"the bench is {max - min:F2} long and the oak's trunk is up to {2f * OAK_TRUNK_RADIUS_MAX:F2} thick");
        }

        [Fact]
        public void TheBoundingSphereHoldsTheWholeBenchAndNotMuchMore()
        {
            //Scaling the mesh and not its sphere culls a bench that is in view (and drops its shadow) at the edge of the
            //frame; a sphere left too big keeps drawing it when it is far off. Every vertex inside, the farthest one near the rim.
            var (vertices, _, bounds) = MeadowScatter.BenchGeometry();
            float farthest = 0f;
            foreach (var vertex in vertices)
                farthest = Math.Max(farthest, Vector3.Distance(vertex.Position, bounds.Center));

            Assert.True(farthest <= bounds.Radius, $"a vertex lies {farthest:F2} from the sphere's centre, past its radius {bounds.Radius:F2}");
            Assert.True(farthest >= 0.8f * bounds.Radius, $"the farthest vertex is {farthest:F2} out in a sphere of {bounds.Radius:F2}");
        }

        [Fact]
        public void TheBenchStandsPastTheTroddenGroundAndTheRootsOfTheOak()
        {
            //The back corner is the bench's nearest part to the trunk: measured on the real mesh, placed as the planting places it
            (float knollX, float knollZ) = Knoll();
            (float x, float z, float yaw) = MeadowScatter.BenchStand(knollX, knollZ);
            Matrix world = Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, 0f, z);

            var (vertices, _, _) = MeadowScatter.BenchGeometry();
            float nearest = float.MaxValue;
            foreach (var vertex in vertices)
            {
                Vector3 p = Vector3.Transform(vertex.Position, world);
                nearest = Math.Min(nearest, MathF.Sqrt((p.X - knollX) * (p.X - knollX) + (p.Z - knollZ) * (p.Z - knollZ)));
            }

            Assert.True(nearest >= MeadowScatter.TREE_BARE + MeadowScatter.TREE_BARE_EDGE,
                $"the bench reaches {nearest:F2} from the trunk, onto the trodden patch that ends at {MeadowScatter.TREE_BARE + MeadowScatter.TREE_BARE_EDGE:F2}");
            Assert.True(nearest >= OAK_ROOT_REACH_MAX, $"the bench reaches {nearest:F2} from the trunk, into roots that reach {OAK_ROOT_REACH_MAX:F2}");
        }

        [Fact]
        public void TheBenchStandsOutFromTheTrunkAndFacesTheArena()
        {
            //What the planting did before the placement became a function of its own
            (float knollX, float knollZ) = Knoll();
            (float x, float z, float yaw) = MeadowScatter.BenchStand(knollX, knollZ);
            Assert.Equal(MeadowScatter.BENCH_OUT, MathF.Sqrt((x - knollX) * (x - knollX) + (z - knollZ) * (z - knollZ)), 3);

            //Local +Z, the way its face looks, turned by the yaw, points from the bench's side of the oak to the arena's centre
            Vector3 facing = Vector3.TransformNormal(Vector3.UnitZ, Matrix.CreateRotationY(yaw));
            Vector2 toArena = Vector2.Normalize(new Vector2(-knollX, -knollZ));
            Assert.Equal(1f, facing.X * toArena.X + facing.Z * toArena.Y, 4);
        }
    }
}
