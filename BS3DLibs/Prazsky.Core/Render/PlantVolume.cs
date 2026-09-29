using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One vertical cylinder of a planted thing's volume, at scale 1 and relative to the thing's pivot on the ground: a
    /// trunk is one, a crown tier another, a boulder a squat one (#653). <see cref="OffsetX"/>/<see cref="OffsetZ"/>
    /// put its axis off the pivot's (a crown that sits to one side of its trunk).
    /// </summary>
    public readonly struct Slab(float bottom, float top, float radius, float offsetX = 0f, float offsetZ = 0f)
    {
        public readonly float Bottom = bottom;
        public readonly float Top = top;
        public readonly float Radius = radius;
        public readonly float OffsetX = offsetX;
        public readonly float OffsetZ = offsetZ;
    }

    /// <summary>
    /// <b>Keeping planted things apart by what they are made of, not by the width of the spot they stand on</b> (#653).
    /// <see cref="ScatterSpacing"/> keeps footprints apart, and a footprint is a single radius — the spacing radius, the
    /// crown's reach — at <see cref="ScatterSpacing.PACKING"/> of the sum, which lets two crowns interlace by design. That
    /// is right for a wood, and it is not the whole of the rule: it says nothing about a <i>trunk</i>, which is thin and
    /// stands well inside its footprint, nor about height, so a low bush and a high crown that never meet were held
    /// apart as though they did while two trunks and a crown that did meet were not. The owner's flythrough found trees
    /// standing through other trees, rocks and mounds.
    /// <para>
    /// Each thing is a few <see cref="Slab"/>s — vertical cylinders with a bottom and a top. Two things clip when a slab of
    /// one and a slab of the other overlap in height <b>and</b> their axes are closer than their radii sum, less a
    /// tolerance (<see cref="TOLERANCE"/> of the smaller radius): "a little is fine", the owner said, but not noticeably.
    /// Cheap on purpose (a handful of slabs a thing, a distance and two compares a pair), because it runs for every
    /// proposal of every plant while a level loads.
    /// </para>
    /// </summary>
    public static class PlantVolumes
    {
        /// <summary>
        /// How far a pair of slabs may sink into each other, as a share of the SMALLER radius of the two. Zero would make
        /// every tree stand exactly clear of its neighbours, which reads as an orchard (see
        /// <see cref="ScatterSpacing.PACKING"/>); a small share lets a branch reach into a neighbour's leaves, and a
        /// bush's edge into a trunk's flare, and keeps the groves.
        /// </summary>
        public const float TOLERANCE = 0.15f;

        /// <summary>A placed thing: where it stands, how big it is and the slabs it is made of.</summary>
        public readonly struct Placed
        {
            public readonly float X, Z, GroundY, Scale, Reach;
            public readonly Slab[] Slabs;

            /// <summary>
            /// Works out <see cref="Reach"/> — the farthest any slab's edge lies from the pivot, at this scale, which is a
            /// quick reject for pairs that cannot touch.
            /// </summary>
            public Placed(float x, float z, float groundY, float scale, Slab[] slabs)
            {
                X = x; Z = z; GroundY = groundY; Scale = scale; Slabs = slabs;

                float reach = 0f;
                for (int i = 0; i < slabs.Length; i++)
                    reach = MathF.Max(reach, MathF.Sqrt(slabs[i].OffsetX * slabs[i].OffsetX + slabs[i].OffsetZ * slabs[i].OffsetZ) + slabs[i].Radius);
                Reach = reach * scale;
            }
        }

        /// <summary>
        /// How far a thing at (<paramref name="x"/>, <paramref name="z"/>) made of <paramref name="slabs"/> at
        /// <paramref name="scale"/>, standing on <paramref name="groundY"/>, clears its <b>worst</b> neighbour by, in world
        /// units and signed like <see cref="ScatterSpacing.Clearance"/>: positive is room, negative is how far the two
        /// sink into each other past the tolerance. Only slabs that meet in height are compared.
        /// </summary>
        public static float Clearance(float x, float z, float groundY, float scale, Slab[] slabs, List<Placed> placed)
        {
            float worst = float.PositiveInfinity;

            float reach = 0f;
            for (int i = 0; i < slabs.Length; i++)
                reach = MathF.Max(reach, MathF.Sqrt(slabs[i].OffsetX * slabs[i].OffsetX + slabs[i].OffsetZ * slabs[i].OffsetZ) + slabs[i].Radius);
            reach *= scale;

            for (int p = 0; p < placed.Count; p++)
            {
                Placed other = placed[p];
                float dx = x - other.X;
                float dz = z - other.Z;

                //Farther apart than the two reaches: no slab of one can touch a slab of the other
                float span = reach + other.Reach;
                if (dx * dx + dz * dz > span * span) continue;

                for (int a = 0; a < slabs.Length; a++)
                {
                    Slab sa = slabs[a];
                    float aBottom = groundY + sa.Bottom * scale;
                    float aTop = groundY + sa.Top * scale;
                    float aRadius = sa.Radius * scale;

                    for (int b = 0; b < other.Slabs.Length; b++)
                    {
                        Slab sb = other.Slabs[b];

                        //They have to meet in height
                        if (aTop < other.GroundY + sb.Bottom * other.Scale || aBottom > other.GroundY + sb.Top * other.Scale) continue;

                        float bRadius = sb.Radius * other.Scale;
                        float ex = dx + (sa.OffsetX * scale - sb.OffsetX * other.Scale);
                        float ez = dz + (sa.OffsetZ * scale - sb.OffsetZ * other.Scale);
                        float margin = MathF.Sqrt(ex * ex + ez * ez) - (aRadius + bRadius) + TOLERANCE * MathF.Min(aRadius, bRadius);

                        if (margin < worst) worst = margin;
                    }
                }
            }

            return worst;
        }
    }
}
