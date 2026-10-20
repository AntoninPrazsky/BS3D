using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System;
using System.Threading.Tasks;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The snow the wind laid round the aurora's planting at a scale the ground's grid carries (#963): a lee tail trailing
    /// downwind of every trunk and snag, and the crowns' shade on the snow (the dark round a tree's foot that seats it,
    /// <see cref="Texel"/>'s fourth channel), baked once from where the planting put them into a map <c>Aurora.fx</c>'s
    /// vertex shader reads. The drift rising against the wood itself is <see cref="AuroraCollars"/>': it rises over less
    /// than a unit, and baked here at a unit a texel under a grid of 0.75 it came out smeared over two units and every
    /// foot read as a cylinder stuck into a plane.
    /// <para>
    /// <b>Exact, and the same array on both sides.</b> A texel holds the height and its slope, computed from the kernels
    /// themselves (so the normals the pixel shader takes from it are smooth across texels, where a slope taken from
    /// neighbouring heights would be flat per texel), and the open sky. The height is rounded to a half float as the
    /// texture stores it, and <see cref="Height"/> repeats <c>MoundHeight</c>'s bilinear arithmetic, so
    /// <c>TerrainMirror.Aurora</c> and the drawn snow agree to the rounding (<c>mirrorcheck</c>).
    /// </para>
    /// <para>
    /// Kernels add, then saturate softly (<see cref="CAP"/>): two trees side by side share one drift that is not twice
    /// as tall.
    /// </para>
    /// </summary>
    public sealed class AuroraMoundMap
    {
        /// <summary>Texels a side.</summary>
        public const int SIZE = 1024;

        /// <summary>World units a texel: the map covers the planting out to 512 units from the arena's axis.</summary>
        public const float TEXEL = 1f;

        /// <summary>World XZ of the map's corner.</summary>
        public static readonly Vector2 ORIGIN = new(-SIZE * TEXEL * 0.5f, -SIZE * TEXEL * 0.5f);

        /// <summary>The soft ceiling the summed drifts approach: no drift stands taller than this, however many trees share it.</summary>
        public const float CAP = 1.6f;

        //The texels as the texture holds them, and their heights unpacked from those very half floats for the mirror
        private readonly HalfVector4[] _texels;
        private readonly float[] _height;

        private AuroraMoundMap()
        {
            _texels = new HalfVector4[SIZE * SIZE];
            _height = new float[SIZE * SIZE];
        }

        /// <summary>The texel at (<paramref name="x"/>, <paramref name="z"/>) of the map, as the texture stores it: height, slope along X, slope along Z, open sky.</summary>
        public Vector4 Texel(int x, int z) => _texels[z * SIZE + x].ToVector4();

        /// <summary>The whole map for the texture, row by row (Z): the array itself, not a copy.</summary>
        public HalfVector4[] Texels => _texels;

        /// <summary>
        /// The drift's height at a world point: <c>Aurora.fx</c>'s <c>MoundHeight</c>, line for line — four texels round
        /// the point, weighted bilinearly by hand; zero off the map.
        /// </summary>
        public float Height(float x, float z)
        {
            float tx = (x - ORIGIN.X) / TEXEL - 0.5f;
            float tz = (z - ORIGIN.Y) / TEXEL - 0.5f;
            float cx = MathF.Floor(tx), cz = MathF.Floor(tz);
            if (MathF.Min(cx, cz) < 0f || MathF.Max(cx, cz) > SIZE - 2f) return 0f;

            float fx = tx - cx, fz = tz - cz;
            int i = (int)cz * SIZE + (int)cx;
            float a = _height[i], b = _height[i + 1], c = _height[i + SIZE], d = _height[i + SIZE + 1];

            return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fz);
        }

        /// <summary>
        /// Bakes the lee tails and the shade round <paramref name="planting"/> (planted on <c>TerrainMirror.AuroraBase</c>
        /// with <paramref name="config"/>'s terrain). Runs once, on the thread that builds the backdrop, at every start of
        /// every executable (the renderer builds every backdrop eagerly); the texels nothing touched are written as one
        /// packed constant, so it is the splats that cost.
        /// </summary>
        public static AuroraMoundMap Bake(ForestScatter planting, AuroraSceneConfig config)
        {
            var map = new AuroraMoundMap();
            var sum = new float[SIZE * SIZE];
            var slope = new Vector2[SIZE * SIZE];
            var open = new float[SIZE * SIZE];
            Array.Fill(open, 1f);

            AuroraSnowfieldConfig snow = config.Snowfield;
            ForestSceneConfig terrain = config.Terrain;
            ForestTreeConfig trees = terrain.Trees;
            Vector2 wind = TerrainMirror.SnowWind(snow);

            //The spruces: a lee tail, and the shade of the crown
            for (int v = 0; v < planting.Conifers.Length; v++)
            {
                ForestScatterRenderer.SpruceVariant variant = ForestScatterRenderer.CONIFER_VARIANTS[v];
                foreach (ModelInstance tree in planting.Conifers[v])
                {
                    float scale = ScaleOf(tree.World);
                    Vector2 at = new(tree.World.M41, tree.World.M43);
                    float trunk = trees.TrunkBaseRadius * variant.Width * scale;
                    float crown = trees.ConiferCrownRadius * variant.Width * scale;
                    SplatTail(sum, slope, at, wind, snow.TrunkDrift * scale * snow.LeeTailHeight, snow.LeeTail * scale, trunk * 3f + 0.6f * scale);
                    SplatShade(open, at, crown * 0.95f, snow.CanopyShade);
                }
            }

            //The snags: a lee tail, no crown
            for (int v = 0; v < planting.Snags.Length; v++)
            {
                float radius = terrain.Snags.Radius * ForestScatterRenderer.SNAG_SHAPES[v].Y;
                foreach (ModelInstance snag in planting.Snags[v])
                {
                    float scale = ScaleOf(snag.World);
                    Vector2 at = new(snag.World.M41, snag.World.M43);
                    SplatTail(sum, slope, at, wind, snow.TrunkDrift * scale * snow.LeeTailHeight, snow.LeeTail * scale * 0.8f, radius * scale * 3f + 0.5f);
                }
            }

            //The soft ceiling, packed as the texture holds it - and the mirror reads the height back out of that packing,
            //so a rounding the half float makes is made on both sides
            //Row by row in parallel: every texel is its own, and the half-float packing is the bake's dearest step
            var untouched = new HalfVector4(0f, 0f, 0f, 1f);
            float untouchedHeight = untouched.ToVector4().X;
            Parallel.For(0, SIZE, row =>
            {
                for (int i = row * SIZE, end = i + SIZE; i < end; i++)
                {
                    if (sum[i] == 0f && open[i] == 1f)
                    {
                        map._texels[i] = untouched;
                        map._height[i] = untouchedHeight;
                        continue;
                    }

                    float fade = MathF.Exp(-sum[i] / CAP);
                    Vector2 s = slope[i] * fade;
                    map._texels[i] = new HalfVector4(CAP * (1f - fade), s.X, s.Y, open[i]);
                    map._height[i] = map._texels[i].ToVector4().X;
                }
            });

            return map;
        }

        //The uniform scale of a planted instance: the length of its world's first row (scale x yaw x lean, no shear)
        private static float ScaleOf(Matrix world) => new Vector3(world.M11, world.M12, world.M13).Length();

        //The texel window round a point, clamped to the map
        private static void Window(Vector2 at, float reach, out int x0, out int x1, out int z0, out int z1)
        {
            x0 = Math.Max(0, (int)MathF.Floor((at.X - reach - ORIGIN.X) / TEXEL));
            x1 = Math.Min(SIZE - 1, (int)MathF.Ceiling((at.X + reach - ORIGIN.X) / TEXEL));
            z0 = Math.Max(0, (int)MathF.Floor((at.Y - reach - ORIGIN.Y) / TEXEL));
            z1 = Math.Min(SIZE - 1, (int)MathF.Ceiling((at.Y + reach - ORIGIN.Y) / TEXEL));
        }

        private static Vector2 TexelCentre(int x, int z) => new(ORIGIN.X + (x + 0.5f) * TEXEL, ORIGIN.Y + (z + 0.5f) * TEXEL);

        //The lee tail: a long low drift trailing downwind of an obstacle, Gaussian along and across the wind
        private static void SplatTail(float[] sum, Vector2[] slope, Vector2 at, Vector2 wind, float height, float length, float width)
        {
            if (height <= 0f || length <= 0f) return;

            Vector2 across = new(-wind.Y, wind.X);
            float centre = length * 0.55f, spread = length * 0.5f;
            Window(at + wind * centre, spread * 2.6f + width * 2.6f, out int x0, out int x1, out int z0, out int z1);

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 d = TexelCentre(x, z) - at;
                    float a = (Vector2.Dot(d, wind) - centre) / spread;
                    float b = Vector2.Dot(d, across) / width;
                    float h = height * MathF.Exp(-a * a - b * b);
                    if (h < 1e-4f) continue;

                    int i = z * SIZE + x;
                    sum[i] += h;
                    slope[i] += h * (wind * (-2f * a / spread) + across * (-2f * b / width));
                }
            }
        }

        //The shade under a crown: the share of sky the snow loses, Gaussian round the trunk
        private static void SplatShade(float[] open, Vector2 at, float radius, float depth)
        {
            if (depth <= 0f) return;

            Window(at, radius * 2.2f, out int x0, out int x1, out int z0, out int z1);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float r = Vector2.Distance(TexelCentre(x, z), at) / radius;
                    open[z * SIZE + x] *= 1f - depth * MathF.Exp(-r * r);
                }
            }
        }
    }
}
