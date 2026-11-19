using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The aurora's far stand drawn as the planted wood's own spruces wherever the lens comes near it (#1163). Each spire of
    /// the stand's near ring (<see cref="AuroraStandMesh"/>, the silhouette the hills' skylines are made of) has a spruce of
    /// the planting's kind in reserve: one of <see cref="ForestScatterRenderer.CONIFER_VARIANTS"/> (<see cref="VariantFor"/>),
    /// standing on the spire's foot, turned as it is and as tall. On High and Ultra a spire within <see cref="SWAP_RADIUS"/>
    /// of the lens is not drawn (<c>Aurora.fx</c>'s <c>StandVS</c> puts it to a point, <see cref="SwapRadiusFor"/>) and the
    /// hosts' <see cref="ForestScatterRenderer"/> draws its spruce in its place (<see cref="ForestPasses"/>), in the crown its
    /// distance gives it. So what a tree is drawn as follows its distance from the lens frame by frame, wherever the lens
    /// flies: a chapter intro's run through the wood ends a dozen units short of the stand, where the spires read as funnels.
    /// The spruces cast nothing, as the spires never did: a shadow thrown from two hundred units off would sweep in across
    /// the snow each time the swap reached its tree.
    /// <para>
    /// Device-free: the instances are made once from the stand's planting (<c>AuroraBackdrop</c>, which hands it to the
    /// planting as <see cref="ForestScatter.Stand"/>), and a pass only picks among them.
    /// </para>
    /// </summary>
    public sealed class StandSpruces
    {
        /// <summary>
        /// How near the lens (horizontally, from the foot) a spire of the stand is drawn as a spruce. Past it the spire is a
        /// silhouette on a ridge, under the mist's haze; this side of it the spire's four skirts read as funnels.
        /// <c>Aurora.fx</c>'s <c>StandVS</c> applies the same test to the same floats (<see cref="Swaps"/>).
        /// </summary>
        public const float SWAP_RADIUS = 200f;

        /// <summary>Each spire's spruce, by the conifer variant it wears (indexed as <see cref="ForestScatter.Conifers"/>' buckets).</summary>
        public ModelInstance[][] Spruces { get; }

        /// <summary>Each spruce's foot (x, z) as <see cref="Spruces"/> holds them, the very floats <see cref="Swaps"/> is
        /// asked about, packed apart so a pass's test of every spire stays in the cache (nine thousand of them, a pass).</summary>
        public Vector2[][] Feet { get; }

        /// <summary>
        /// The most of each variant's spruces (indexed as <see cref="Spruces"/>) a lens anywhere can be within
        /// <see cref="SWAP_RADIUS"/> of, bounded from above (<see cref="MostWithinSwap"/>): a pass takes no more, so the arrays
        /// it fills, and the instance buffers they are drawn from, need room for the planting and this many
        /// (<see cref="ForestPasses"/>), not for every spruce of the ring.
        /// </summary>
        public int[] MostTaken { get; }

        /// <summary>The nearest any of them stands to the arena's axis, horizontally: a lens further than
        /// <see cref="SWAP_RADIUS"/> inside it swaps none (<see cref="Reaches"/>).</summary>
        public float InnerRadius { get; }

        /// <summary>
        /// Whether the swap is made at all: a measurement dial (<see cref="AuroraLayer.StandSpruces"/>, the Testbed's
        /// <c>aurora=</c>), read through <see cref="SwapRadiusFor"/> by the backdrop and the passes both, so a spire is drawn
        /// one way or the other and never both. True for every other caller.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The spruces in reserve for <paramref name="count"/> spires of a stand, each given as its foot (sunk by
        /// <paramref name="sink"/> of its scale under the ground it stands on) with its scale in <c>w</c>, and its look: the
        /// yaw's sine and cosine, the height's stretch over <paramref name="spireHeight"/> at scale 1, and its tint.
        /// </summary>
        public StandSpruces(Vector4[] places, Vector4[] looks, int count, float spireHeight, float sink, ForestTreeConfig trees)
        {
            int variants = ForestScatterRenderer.CONIFER_VARIANTS.Length;
            var variantOf = new int[count];
            var perVariant = new int[variants];
            for (int i = 0; i < count; i++)
            {
                variantOf[i] = VariantFor(i, spireHeight * places[i].W * looks[i].Z, trees);
                perVariant[variantOf[i]]++;
            }

            Spruces = new ModelInstance[variants][];
            Feet = new Vector2[variants][];
            for (int v = 0; v < variants; v++)
            {
                Spruces[v] = new ModelInstance[perVariant[v]];
                Feet[v] = new Vector2[perVariant[v]];
            }

            var filled = new int[variants];
            float inner = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector4 place = places[i], look = looks[i];
                int v = variantOf[i];

                //As tall as the spire, the spruce's own trunk and crown together, its foot on the spire's ground and planted
                //as a planted spruce is (ForestScatter.PlantedWorld). Turned as the spire is: StandVS turns a spire's x
                //axis towards (cos, sin) of its look, which is CreateRotationY of minus the look's angle
                float scale = spireHeight * place.W * look.Z / SpruceHeight(trees, ForestScatterRenderer.CONIFER_VARIANTS[v]);
                float ground = place.Y + sink * place.W;
                float yaw = -MathF.Atan2(look.X, look.Y);

                //A lean from the spire's tint, which the planting rolled independently of everything here
                float lean = ForestScatter.TREE_LEAN * Math.Clamp((look.W - 0.8f) / 0.4f, 0f, 1f);
                float leanAzimuth = yaw * 1.7f + 0.9f;

                Feet[v][filled[v]] = new Vector2(place.X, place.Z);
                Spruces[v][filled[v]++] = new ModelInstance(ForestScatter.PlantedWorld(place.X, ground, place.Z, scale, yaw, lean, leanAzimuth),
                    new Vector4(0f, 0f, 0f, 1f));
                inner = MathF.Min(inner, MathF.Sqrt(place.X * place.X + place.Z * place.Z));
            }

            MostTaken = new int[variants];
            for (int v = 0; v < variants; v++) MostTaken[v] = MostWithinSwap(Feet[v]);
            InnerRadius = count > 0 ? inner : float.MaxValue;
        }

        /// <summary>
        /// Which variant the spruce of the <paramref name="index"/>th spire wears, <paramref name="tall"/> units tall: the
        /// variants in turn from the index's, the first that stands that tall at a scale the planting itself rolls
        /// (<see cref="ForestTreeConfig.MinScale"/> to <see cref="ForestTreeConfig.MaxScale"/>), so the stand mixes them as a
        /// grove does and none is blown up past any planted tree (strictly in turn, a third of them stood outside that range, a
        /// squat variant matched to a tall spire at twice its planted size).
        /// The index's own where none fits.
        /// </summary>
        public static int VariantFor(int index, float tall, ForestTreeConfig trees)
        {
            int variants = ForestScatterRenderer.CONIFER_VARIANTS.Length;
            for (int k = 0; k < variants; k++)
            {
                int v = (index + k) % variants;
                float scale = tall / SpruceHeight(trees, ForestScatterRenderer.CONIFER_VARIANTS[v]);
                if (scale >= trees.MinScale && scale <= trees.MaxScale) return v;
            }
            return index % variants;
        }

        /// <summary>How tall a spruce of <paramref name="variant"/> stands at scale 1: its trunk and its crown, as
        /// <see cref="ForestScatterRenderer"/> builds them.</summary>
        public static float SpruceHeight(ForestTreeConfig trees, ForestScatterRenderer.SpruceVariant variant) =>
            trees.ConiferTrunkHeight * variant.Height * (variant.Stand ? trees.ConiferStandTrunk : 1f)
            + trees.ConiferCrownHeight * variant.Height;

        /// <summary>Whether a spire whose foot is at (<paramref name="x"/>, <paramref name="z"/>) is drawn as its spruce from
        /// <paramref name="lens"/> where the swap is made (<see cref="SwapRadiusFor"/>): within <see cref="SWAP_RADIUS"/> of
        /// it horizontally. <c>StandVS</c> keeps the spire on exactly the other side of the same test.</summary>
        public static bool Swaps(float x, float z, Vector3 lens)
        {
            float dx = x - lens.X, dz = z - lens.Z;
            return dx * dx + dz * dz < SWAP_RADIUS * SWAP_RADIUS;
        }

        /// <summary>
        /// How near the lens a spire is drawn as its spruce: <see cref="SWAP_RADIUS"/> on High and Ultra
        /// (<paramref name="fineMeshes"/>, the caller's <c>SceneRenderer.FineMeshes</c>) with the dial on
        /// (<see cref="Enabled"/>), and 0, none, otherwise. The backdrop hands it to <c>StandVS</c> as <c>StandSwapRadius</c>
        /// and the passes take the spruces only where it is not 0, so a spire is drawn one way or the other, never both and
        /// never neither.
        /// </summary>
        public float SwapRadiusFor(bool fineMeshes) => fineMeshes && Enabled ? SWAP_RADIUS : 0f;

        /// <summary>Whether a lens at <paramref name="lens"/> can be within <see cref="SWAP_RADIUS"/> of any spire: it stands
        /// within it of the ring's inside.</summary>
        public bool Reaches(Vector3 lens) => MathF.Sqrt(lens.X * lens.X + lens.Z * lens.Z) + SWAP_RADIUS > InnerRadius;

        //The cells MostWithinSwap counts the feet in: a quarter of the swap's radius wide
        private const int CELLS_IN_RADIUS = 4;
        private const float CELL = SWAP_RADIUS / CELLS_IN_RADIUS;

        /// <summary>
        /// At least as many of <paramref name="feet"/> as a lens anywhere is within <see cref="SWAP_RADIUS"/> of: the feet
        /// counted by cells a quarter of the radius wide, and of every cell a lens can stand in, the most the cells within the
        /// radius of it hold. A cell is within it when the gap between it and the lens's cell, a whole number of cells along
        /// each axis, is no wider than the radius; a foot in any other cell is further than the radius from every point of the
        /// lens's cell, by at least a tenth of a cell, which no rounding of a coordinate comes near.
        /// </summary>
        internal static int MostWithinSwap(Vector2[] feet)
        {
            if (feet.Length == 0) return 0;

            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
            foreach (Vector2 foot in feet)
            {
                int cx = (int)MathF.Floor(foot.X / CELL), cz = (int)MathF.Floor(foot.Y / CELL);
                x0 = Math.Min(x0, cx); x1 = Math.Max(x1, cx);
                z0 = Math.Min(z0, cz); z1 = Math.Max(z1, cz);
            }
            int width = x1 - x0 + 1, depth = z1 - z0 + 1;
            var counts = new int[width, depth];
            foreach (Vector2 foot in feet)
                counts[(int)MathF.Floor(foot.X / CELL) - x0, (int)MathF.Floor(foot.Y / CELL) - z0]++;

            //A cell (dx, dz) cells off the lens's leaves a gap of |dx| - 1 and |dz| - 1 whole cells between them
            int reach = CELLS_IN_RADIUS + 1, most = 0;
            for (int lx = -reach; lx < width + reach; lx++)
                for (int lz = -reach; lz < depth + reach; lz++)
                {
                    int sum = 0;
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int cx = lx + dx;
                        if (cx < 0 || cx >= width) continue;
                        int gx = Math.Max(Math.Abs(dx) - 1, 0);
                        for (int dz = -reach; dz <= reach; dz++)
                        {
                            int cz = lz + dz;
                            if (cz < 0 || cz >= depth) continue;
                            int gz = Math.Max(Math.Abs(dz) - 1, 0);
                            if (gx * gx + gz * gz <= CELLS_IN_RADIUS * CELLS_IN_RADIUS) sum += counts[cx, cz];
                        }
                    }
                    most = Math.Max(most, sum);
                }
            return most;
        }
    }
}
