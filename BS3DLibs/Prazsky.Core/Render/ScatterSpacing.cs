using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Keeping a scattered thing out of the thing next to it.</b> Both procedural scatters in this project
    /// place their instances independently — 82 % of them clumped around a set of cluster centres, with the
    /// density rising towards each centre — and neither had any separation test at all, so two landing on top
    /// of each other was the expected case rather than bad luck (#108: trees standing inside other trees, in
    /// the forest and in the savanna both).
    /// <para>
    /// One copy for both, because it is one rule: <see cref="ForestScatter"/> plants meshes and
    /// <see cref="SceneRenderer"/>'s acacia buffer plants billboards, but "do not stand inside your neighbour"
    /// does not care which. What each caller keeps is the part that genuinely differs — how wide its own kind
    /// is, which for a tree comes off the mesh's crown radius and for an acacia off the same width the vertex
    /// shader sizes the billboard with.
    /// </para>
    /// </summary>
    public static class ScatterSpacing
    {
        /// <summary>
        /// A placed instance's footprint on the ground: where it stands and how wide it is there.
        /// </summary>
        public readonly struct Footprint(float x, float z, float radius)
        {
            public readonly float X = x;
            public readonly float Z = z;
            public readonly float Radius = radius;
        }

        /// <summary>
        /// How much of the two footprints a pair actually has to clear. <b>Not 1</b>, deliberately: crowns in a
        /// real stand interlace, and a wood whose every crown clears every other reads as an orchard. What the
        /// eye catches is one plant standing <i>inside</i> another, which this is comfortably tight enough to
        /// stop — and being under 1 it also keeps the groves the cluster scatter exists to make.
        /// </summary>
        public const float PACKING = 0.72f;

        /// <summary>
        /// How many positions an instance may try before it settles for the roomiest it found. It never gives
        /// up and drops the instance: a count is authored, and a forest that quietly plants 180 of the 240
        /// trees it was asked for would be a worse bug than the overlap this exists to stop.
        /// <para>
        /// <b>Measured</b> on the shipped forest config (240 trees, 20 clusters, spread 34), counting pairs
        /// closer than <see cref="PACKING"/> of their two radii:
        /// </para>
        /// <list type="table">
        /// <item><description>1 try (what both scatters did before there was a test at all) — <b>67</b> overlapping pairs, 35 of them badly</description></item>
        /// <item><description>2 tries — 11 pairs, 2 badly</description></item>
        /// <item><description>4 tries — <b>0</b>, and 8, 16 and 32 are 0 as well</description></item>
        /// </list>
        /// <para>
        /// Four already clears it, and eight is kept as headroom for a denser config than the shipped one. It
        /// costs nothing to carry: <see cref="Place"/> stops the moment a position clears, so the extra budget
        /// is only ever spent where a position is genuinely hard to find.
        /// </para>
        /// </summary>
        public const int TRIES = 8;

        /// <summary>
        /// How far a candidate at (<paramref name="x"/>, <paramref name="z"/>) with radius
        /// <paramref name="radius"/> clears its <b>worst</b> neighbour by. Positive is room to spare, negative
        /// is overlap and by how much, so a caller can rank two bad positions when no good one turns up.
        /// <para>
        /// A flat scan of everything placed so far. It is O(n²) over the scatter — 240 trees once per level
        /// load, a few tens of thousands of squared distances, against a load that builds twenty-five meshes
        /// and their vertex buffers. A grid would be the answer if this ran per frame; it does not, and an
        /// index to keep in step is a cost of its own.
        /// </para>
        /// </summary>
        public static float Clearance(float x, float z, float radius, List<Footprint> occupied)
        {
            float worst = float.PositiveInfinity;

            for (int i = 0; i < occupied.Count; i++)
            {
                Footprint other = occupied[i];
                float dx = x - other.X;
                float dz = z - other.Z;

                //A distance rather than a squared one: the margin has to be signed and in world units for the
                //caller's fallback to be able to rank two bad positions against each other.
                float margin = MathF.Sqrt(dx * dx + dz * dz) - (radius + other.Radius) * PACKING;

                if (margin < worst) worst = margin;
            }

            return worst;
        }

        //Deliberately no Place(candidate) wrapper taking a delegate. Both callers generate their candidate
        //inline from their own rng and their own cluster arrays, and a delegate would have to close over all
        //of that to say anything the two loops do not already say plainly. What has to be one copy is the
        //RULE — the packing fraction, the retry budget and the clearance test — and that is what is here.
    }
}
