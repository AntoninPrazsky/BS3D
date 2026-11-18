using Microsoft.Xna.Framework;
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

        /// <summary>
        /// <paramref name="slabs"/> with their offsets turned by <paramref name="yaw"/>, as <c>Matrix.CreateRotationY</c> turns a point
        /// (#1157): a thing whose slabs stand off its pivot along its own axes (a fallen log along +X) is asked where it will lie,
        /// once the yaw it will be planted at is known. The heights and the radii are unchanged.
        /// </summary>
        public static Slab[] Turned(Slab[] slabs, float yaw)
        {
            float cos = MathF.Cos(yaw), sin = MathF.Sin(yaw);
            var turned = new Slab[slabs.Length];
            for (int i = 0; i < slabs.Length; i++)
            {
                Slab s = slabs[i];
                turned[i] = new Slab(s.Bottom, s.Top, s.Radius, s.OffsetX * cos + s.OffsetZ * sin, -s.OffsetX * sin + s.OffsetZ * cos);
            }
            return turned;
        }

        /// <summary>A rock as it stands (#1157): its mesh, where it is, and the world into its own space.</summary>
        internal readonly struct Stone
        {
            public readonly RockMesh Rock;
            public readonly Matrix World, ToRock;
            public readonly float Scale;

            /// <summary>Where it stands in plan, and how far its mesh's sphere reaches from there: a quick reject.</summary>
            public readonly Vector2 At;
            public readonly float Reach;

            public Stone(RockMesh rock, Matrix world)
            {
                Rock = rock;
                World = world;
                ToRock = Matrix.Invert(world);
                Scale = new Vector3(world.M11, world.M12, world.M13).Length();
                At = new Vector2(world.M41, world.M43);
                BoundingSphere sphere = rock.BoundingSphere;
                Reach = (sphere.Center.Length() + sphere.Radius) * Scale;
            }
        }

        /// <summary>Points round a slab's rim at its widest, at the least: more on a wide slab, so they stand no farther
        /// apart than <see cref="BODY_POINT_SPACING"/>.</summary>
        private const int RIM_POINTS = 12;

        /// <summary>How far apart a body's points stand round its rims and up its axis, at scale 1.</summary>
        private const float BODY_POINT_SPACING = 1f;

        /// <summary>
        /// The points of a body of slabs, in its own space at scale 1, that a rock is asked about (#1157): each slab's axis and a
        /// ring round it at <see cref="TOLERANCE"/> inside its radius (the share the volume rule lets two things sink into each
        /// other), from its bottom to its top, every <see cref="BODY_POINT_SPACING"/> or closer. A rock narrower than the gaps
        /// between them is caught the other way round, by its own points inside the slab (<see cref="StoneClearance"/>).
        /// </summary>
        internal static void BodyPoints(Slab[] body, List<Vector3> into)
        {
            foreach (Slab slab in body)
            {
                float height = slab.Top - slab.Bottom, rim = slab.Radius * (1f - TOLERANCE);
                int levels = Math.Max(2, (int)MathF.Ceiling(height / BODY_POINT_SPACING) + 1);
                int around = Math.Max(RIM_POINTS, (int)MathF.Ceiling(MathHelper.TwoPi * rim / BODY_POINT_SPACING));
                for (int l = 0; l < levels; l++)
                {
                    float y = slab.Bottom + height * l / (levels - 1);
                    into.Add(new Vector3(slab.OffsetX, y, slab.OffsetZ));
                    for (int a = 0; a < around; a++)
                    {
                        float angle = MathHelper.TwoPi * a / around;
                        into.Add(new Vector3(slab.OffsetX + MathF.Cos(angle) * rim, y, slab.OffsetZ + MathF.Sin(angle) * rim));
                    }
                }
            }
        }

        /// <summary>
        /// How far a thing made of <paramref name="body"/> (slabs at scale 1 in its own space, planted by
        /// <paramref name="world"/>) clears the nearest of <paramref name="stones"/>, in world units (#1157): positive is room,
        /// negative how deep the two stand in each other past <see cref="TOLERANCE"/> of the slab's radius. Asked both ways
        /// round: each of the body's points (<see cref="BodyPoints"/>) against the rock's own surface (<see cref="RockMesh.Off"/>),
        /// and each of the rock's own points (<see cref="RockMesh.SurfacePoints"/>) against the slabs, so a rock smaller than a
        /// wide slab is not missed between its points. Only what stands over <paramref name="ground"/> is asked: a root and a
        /// rock's foot meeting under the grass are not seen. <paramref name="points"/> is the caller's scratch list.
        /// </summary>
        internal static float StoneClearance(Matrix world, Slab[] body, IReadOnlyList<Stone> stones, Func<float, float, float> ground,
            List<Vector3> points)
        {
            float clearance = float.PositiveInfinity;
            if (stones.Count == 0) return clearance;

            float scale = new Vector3(world.M11, world.M12, world.M13).Length();
            Vector2 at = new(world.M41, world.M43);
            float reach = 0f;
            foreach (Slab slab in body)
                reach = MathF.Max(reach, new Vector2(slab.OffsetX, slab.OffsetZ).Length() + slab.Radius + MathF.Max(MathF.Abs(slab.Top), MathF.Abs(slab.Bottom)));
            reach *= scale;

            Matrix toBody = default;
            bool inverted = false;
            points.Clear();

            foreach (Stone stone in stones)
            {
                if (Vector2.Distance(at, stone.At) > reach + stone.Reach) continue;

                //The body's points against the rock's surface
                if (points.Count == 0) BodyPoints(body, points);
                foreach (Vector3 local in points)
                {
                    Vector3 p = Vector3.Transform(local, world);
                    if (p.Y < ground(p.X, p.Z)) continue;
                    clearance = MathF.Min(clearance, stone.Rock.Off(Vector3.Transform(p, stone.ToRock)) * stone.Scale);
                }

                //And the rock's points against the slabs
                if (!inverted) { toBody = Matrix.Invert(world); inverted = true; }
                foreach (Vector3 local in stone.Rock.SurfacePoints)
                {
                    Vector3 p = Vector3.Transform(local, stone.World);
                    if (p.Y < ground(p.X, p.Z)) continue;
                    Vector3 q = Vector3.Transform(p, toBody);
                    foreach (Slab slab in body)
                    {
                        if (q.Y < slab.Bottom || q.Y > slab.Top) continue;
                        float across = new Vector2(q.X - slab.OffsetX, q.Z - slab.OffsetZ).Length();
                        float inside = slab.Radius * (1f - TOLERANCE) - across;
                        if (inside > 0f) clearance = MathF.Min(clearance, -inside * scale);
                    }
                }
            }
            return clearance;
        }

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
