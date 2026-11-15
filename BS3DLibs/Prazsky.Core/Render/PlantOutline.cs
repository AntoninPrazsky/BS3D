using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A mesh's own outline about its upright axis (#1108): the mesh as a stack of upright discs, each band of
    /// <see cref="BAND"/> of height as wide as the mesh reaches from the axis there, in the mesh's own space. Where a
    /// mesh's bounding sphere is loose (an acacia's wood's reaches 31 round a tree whose crown reaches 17, and down under
    /// the ground), this is the tree: the trunk as thin as it is up to the fork, the flat crown as wide and as deep as its
    /// tiers and their leaf sprays. What a camera that has to fly low among the trees is kept off when the spheres leave it
    /// no way (<see cref="PlantOutline"/>).
    /// <para>
    /// Conservative by construction, from the triangles themselves: a triangle's distance from the axis is greatest at one
    /// of its corners (the distance from a line is convex along any segment), so the widest corner of every triangle is
    /// counted in every band the triangle spans, and nothing the mesh draws stands outside the stack.
    /// </para>
    /// </summary>
    public sealed class PlantProfile
    {
        /// <summary>How tall each band is, in the mesh's units.</summary>
        public const float BAND = 0.5f;

        //Each band's radius; negative for a band no triangle reaches into
        private readonly float[] _radius;

        /// <summary>The lowest point of the mesh, in its own space.</summary>
        public float Bottom { get; }

        /// <summary>The top of the highest band.</summary>
        public float Top => Bottom + _radius.Length * BAND;

        /// <summary>How far the mesh reaches from its axis at the widest.</summary>
        public float Widest { get; }

        private PlantProfile(float bottom, float[] radius)
        {
            Bottom = bottom;
            _radius = radius;
            foreach (float r in radius) Widest = MathF.Max(Widest, r);
        }

        /// <summary>
        /// How far a point in the mesh's own space stands off the stack (zero or less inside it: inside, no deeper than its
        /// band reaches), or <paramref name="within"/> when it stands at least that far off: only the bands within that much
        /// of its height are asked.
        /// </summary>
        public float Off(Vector3 local, float within = float.PositiveInfinity)
        {
            float across = MathF.Sqrt(local.X * local.X + local.Z * local.Z);
            int from = 0, to = _radius.Length - 1;
            if (!float.IsPositiveInfinity(within))
            {
                from = Math.Max(from, (int)MathF.Floor((local.Y - within - Bottom) / BAND));
                to = Math.Min(to, (int)MathF.Floor((local.Y + within - Bottom) / BAND));
            }

            float nearest = within;
            for (int i = from; i <= to; i++)
            {
                if (_radius[i] < 0f) continue;
                float low = Bottom + i * BAND;
                float side = across - _radius[i], up = MathF.Max(low - local.Y, local.Y - (low + BAND));
                float off = side > 0f || up > 0f
                    ? MathF.Sqrt(MathF.Max(side, 0f) * MathF.Max(side, 0f) + MathF.Max(up, 0f) * MathF.Max(up, 0f))
                    : MathF.Max(side, up);
                nearest = MathF.Min(nearest, off);
            }
            return nearest;
        }

        /// <summary>Gathers the triangles of a mesh's parts (a tree's wood, its crown, its leaves) into one profile.</summary>
        public sealed class Builder
        {
            private readonly List<(float Low, float High, float Across)> _triangles = new();

            /// <summary>Every triangle of a part, its vertices indexed by <paramref name="indices"/>.</summary>
            public Builder Add(IReadOnlyList<VertexPositionNormalTexture> vertices, IReadOnlyList<short> indices)
            {
                for (int i = 0; i + 2 < indices.Count; i += 3)
                    Triangle(vertices[indices[i]].Position, vertices[indices[i + 1]].Position, vertices[indices[i + 2]].Position);
                return this;
            }

            /// <summary>The same over int indices (the leaf sprays).</summary>
            public Builder Add(IReadOnlyList<VertexPositionNormalTexture> vertices, IReadOnlyList<int> indices)
            {
                for (int i = 0; i + 2 < indices.Count; i += 3)
                    Triangle(vertices[indices[i]].Position, vertices[indices[i + 1]].Position, vertices[indices[i + 2]].Position);
                return this;
            }

            private void Triangle(Vector3 a, Vector3 b, Vector3 c) => _triangles.Add((MathF.Min(a.Y, MathF.Min(b.Y, c.Y)),
                MathF.Max(a.Y, MathF.Max(b.Y, c.Y)), MathF.Max(Across(a), MathF.Max(Across(b), Across(c)))));

            private static float Across(Vector3 p) => MathF.Sqrt(p.X * p.X + p.Z * p.Z);

            /// <summary>The profile of everything added; null when nothing was.</summary>
            public PlantProfile Build()
            {
                if (_triangles.Count == 0) return null;

                float bottom = float.MaxValue, top = float.MinValue;
                foreach ((float low, float high, _) in _triangles)
                {
                    bottom = MathF.Min(bottom, low);
                    top = MathF.Max(top, high);
                }

                //Each triangle in every band it spans, its top on a band's floor counted in the band below (or the stack
                //stood half a band over a mesh whose height is a whole number of bands), a flat one in the band it lies in
                var radius = new float[Math.Max(1, (int)MathF.Ceiling((top - bottom) / BAND))];
                Array.Fill(radius, -1f);
                foreach ((float low, float high, float across) in _triangles)
                {
                    int from = Math.Min(radius.Length - 1, (int)MathF.Floor((low - bottom) / BAND));
                    int to = Math.Clamp((int)MathF.Ceiling((high - bottom) / BAND) - 1, from, radius.Length - 1);
                    for (int i = from; i <= to; i++) radius[i] = MathF.Max(radius[i], across);
                }
                return new PlantProfile(bottom, radius);
            }
        }
    }

    /// <summary>
    /// One planted instance of a mesh as its <see cref="PlantProfile"/> under the instance's world matrix (a uniform scale, a
    /// lean, a turn and a place, as every scatter plants): how far a point in the world stands off what is drawn there.
    /// </summary>
    public readonly struct PlantOutline
    {
        /// <summary>The mesh's own outline.</summary>
        public readonly PlantProfile Profile;

        /// <summary>The world into the mesh's own space.</summary>
        public readonly Matrix ToMesh;

        /// <summary>The instance's scale, what a distance in the mesh's space is times in the world.</summary>
        public readonly float Scale;

        /// <summary>Where it stands on the ground (the instance's pivot).</summary>
        public readonly Vector3 Root;

        /// <summary>The box in the world the whole stack stands inside, its lean included.</summary>
        public readonly BoundingBox Bounds;

        public PlantOutline(PlantProfile profile, Matrix world)
        {
            Profile = profile;
            ToMesh = Matrix.Invert(world);
            Scale = new Vector3(world.M11, world.M12, world.M13).Length();
            Root = world.Translation;

            float w = profile.Widest;
            Vector3 low = new(float.MaxValue), high = new(float.MinValue);
            foreach (float x in new[] { -w, w })
                foreach (float y in new[] { profile.Bottom, profile.Top })
                    foreach (float z in new[] { -w, w })
                    {
                        Vector3 corner = Vector3.Transform(new Vector3(x, y, z), world);
                        low = Vector3.Min(low, corner);
                        high = Vector3.Max(high, corner);
                    }
            Bounds = new BoundingBox(low, high);
        }

        /// <summary>
        /// How far <paramref name="p"/> stands off the instance (zero or less inside its outline), or <paramref name="within"/>
        /// when it stands at least that far off.
        /// </summary>
        public float Off(Vector3 p, float within = float.PositiveInfinity)
        {
            //⚠ "Farther than within" is answered with within itself, not with it carried into the mesh's space and back: the
            //round trip came out a hair short of it, and a caller asking whether a point is within that much of the tree heard
            //yes from every point round it
            float limit = within / Scale, off = Profile.Off(Vector3.Transform(p, ToMesh), limit);
            return off >= limit ? within : off * Scale;
        }
    }
}
