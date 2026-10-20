using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The snow drifted round the aurora's planting (#963): every trunk, snag, log, boulder and stump stands in a drift of
    /// its own, baked once from where the planting put them into a map <c>Aurora.fx</c>'s vertex shader reads, so the
    /// snow rises against the bark as geometry and never stops short of it. The owner's rule of 2026-10-19: a join
    /// blends; a cylinder stuck into a plane reads as a stage set.
    /// <para>
    /// <b>What a drift is</b>, from the references (#963, both models): the snow rises in a flare against a trunk, steep
    /// at the bark and long and gentle further out; it trails downwind of every obstacle in a lee tail; a log lies half
    /// buried with the snow ramped up both its sides; a boulder or a stump is a snow-covered hump. Under a crown the snow
    /// sees less of the sky, which is the dark round a tree's foot that seats it (<see cref="Texel"/>'s fourth channel).
    /// </para>
    /// <para>
    /// <b>Exact, and the same array on both sides.</b> A texel holds the drift's height and its slope, computed from the
    /// kernels themselves (so the normals the pixel shader takes from it are smooth across texels, where a slope taken
    /// from neighbouring heights would be flat per texel), and the open sky. The height is rounded to a half float
    /// here, as the texture stores it, and <see cref="Height"/> repeats <c>MoundHeight</c>'s bilinear arithmetic, so
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
        /// Bakes the drifts round <paramref name="planting"/> (planted on <c>TerrainMirror.AuroraBase</c> with
        /// <paramref name="config"/>'s terrain). Runs once, when the backdrop is built; a few milliseconds.
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

            //The spruces: a flare against the trunk, a lee tail, and the shade of the crown
            for (int v = 0; v < planting.Conifers.Length; v++)
            {
                ForestScatterRenderer.SpruceVariant variant = ForestScatterRenderer.CONIFER_VARIANTS[v];
                foreach (ModelInstance tree in planting.Conifers[v])
                {
                    float scale = ScaleOf(tree.World);
                    Vector2 at = new(tree.World.M41, tree.World.M43);
                    float trunk = trees.TrunkBaseRadius * variant.Width * scale;
                    float crown = trees.ConiferCrownRadius * variant.Width * scale;
                    SplatFlare(sum, slope, at, trunk, snow.TrunkDrift * scale, snow.TrunkDriftReach * scale);
                    SplatTail(sum, slope, at, wind, snow.TrunkDrift * scale * snow.LeeTailHeight, snow.LeeTail * scale, trunk * 3f + 0.6f * scale);
                    SplatShade(open, at, crown * 0.95f, snow.CanopyShade);
                }
            }

            //The snags: a trunk's drift, no crown
            for (int v = 0; v < planting.Snags.Length; v++)
            {
                float radius = terrain.Snags.Radius * ForestScatterRenderer.SNAG_SHAPES[v].Y;
                foreach (ModelInstance snag in planting.Snags[v])
                {
                    float scale = ScaleOf(snag.World);
                    Vector2 at = new(snag.World.M41, snag.World.M43);
                    SplatFlare(sum, slope, at, radius * scale, snow.TrunkDrift * scale * 0.9f, snow.TrunkDriftReach * scale);
                    SplatTail(sum, slope, at, wind, snow.TrunkDrift * scale * snow.LeeTailHeight, snow.LeeTail * scale * 0.8f, radius * scale * 3f + 0.5f);
                }
            }

            //The logs: half buried, the snow ramped up both sides and drifted deeper on the lee side
            for (int v = 0; v < planting.Logs.Length; v++)
            {
                Vector2 shape = ForestScatterRenderer.LOG_SHAPES[v];
                float length = terrain.Logs.Length * shape.X, radius = terrain.Logs.Radius * shape.Y;
                foreach (ModelInstance log in planting.Logs[v])
                {
                    float scale = ScaleOf(log.World);
                    Vector3 root = Vector3.Transform(new Vector3(-length * 0.5f, 0f, 0f), log.World);
                    Vector3 tip = Vector3.Transform(new Vector3(length * 0.5f, 0f, 0f), log.World);
                    SplatLog(sum, slope, new Vector2(root.X, root.Z), new Vector2(tip.X, tip.Z), wind, radius * scale, snow.LogDrift * radius * scale);
                }
            }

            //The boulders and the stumps: humps of snow
            for (int v = 0; v < planting.Rocks.Length; v++)
            {
                Vector2 shape = ForestScatterRenderer.ROCK_SHAPES[v];
                foreach (ModelInstance rock in planting.Rocks[v])
                {
                    float scale = ScaleOf(rock.World);
                    float radius = terrain.Rocks.Radius * shape.X * scale, height = terrain.Rocks.Height * shape.Y * scale;
                    SplatFlare(sum, slope, new Vector2(rock.World.M41, rock.World.M43), radius * 0.7f, height * 0.45f, radius * 0.7f);
                }
            }

            for (int v = 0; v < planting.Stumps.Length; v++)
            {
                Vector2 shape = ForestScatterRenderer.STUMP_SHAPES[v];
                foreach (ModelInstance stump in planting.Stumps[v])
                {
                    float scale = ScaleOf(stump.World);
                    float radius = terrain.Stumps.Radius * shape.X * scale, height = terrain.Stumps.Height * shape.Y * scale;
                    SplatFlare(sum, slope, new Vector2(stump.World.M41, stump.World.M43), radius * 0.8f, height * 0.4f, radius * 0.8f);
                }
            }

            //The soft ceiling, packed as the texture holds it - and the mirror reads the height back out of that packing,
            //so a rounding the half float makes is made on both sides
            for (int i = 0; i < sum.Length; i++)
            {
                float fade = MathF.Exp(-sum[i] / CAP);
                Vector2 s = slope[i] * fade;
                map._texels[i] = new HalfVector4(CAP * (1f - fade), s.X, s.Y, open[i]);
                map._height[i] = map._texels[i].ToVector4().X;
            }

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

        //The flare against a trunk: height h at the bark (it starts inside it, so the snow meets the wood below the
        //surface), falling by e over `reach` outward, and brought to exactly 0 at six reaches so no step is left
        private static void SplatFlare(float[] sum, Vector2[] slope, Vector2 at, float bark, float height, float reach)
        {
            const float REACHES = 6f;
            float edge = MathF.Exp(-REACHES);
            float start = bark * 0.6f;
            float outer = start + REACHES * reach;
            Window(at, outer, out int x0, out int x1, out int z0, out int z1);

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 d = TexelCentre(x, z) - at;
                    float r = d.Length();
                    if (r >= outer) continue;

                    float out_ = MathF.Max(r - start, 0f);
                    float falloff = MathF.Exp(-out_ / reach);
                    int i = z * SIZE + x;
                    sum[i] += height * (falloff - edge) / (1f - edge);

                    if (r > start && r > 1e-4f)
                        slope[i] += d / r * (-height / reach * falloff / (1f - edge));
                }
            }
        }

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

        //A log: the snow against it rises to `height` at its axis and falls away over a few radii, the lee side's
        //profile stretched downwind
        private static void SplatLog(float[] sum, Vector2[] slope, Vector2 root, Vector2 tip, Vector2 wind, float radius, float height)
        {
            Vector2 axis = tip - root;
            float length = axis.Length();
            if (length < 1e-3f) return;
            axis /= length;
            Vector2 side = new(-axis.Y, axis.X);
            float leeSign = MathF.Sign(Vector2.Dot(side, wind));

            float reach = radius * 1.4f;
            Window((root + tip) * 0.5f, length * 0.5f + reach * 6f, out int x0, out int x1, out int z0, out int z1);

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 d = TexelCentre(x, z) - root;
                    float along = Vector2.Dot(d, axis);
                    float t = MathHelper.Clamp(along, 0f, length);
                    Vector2 toPoint = d - axis * t;
                    float offset = Vector2.Dot(toPoint, side);

                    //Downwind the drift reaches twice as far
                    float sideReach = reach * (offset * leeSign > 0f ? 2f : 1f);
                    float endReach = reach;
                    float ends = along < 0f ? along : along > length ? along - length : 0f;
                    float q = offset / sideReach, e = ends / endReach;
                    float h = height * MathF.Exp(-q * q - e * e);
                    if (h < 1e-4f) continue;

                    int i = z * SIZE + x;
                    sum[i] += h;
                    slope[i] += h * (side * (-2f * q / sideReach) + axis * (-2f * e / endReach));
                }
            }
        }
    }
}
