using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One of the Grid's solids (#393) as the shape it is, for a host that frames a camera on it or has to keep
    /// a lens out of it (the chapter intro's prologue, #559): an axis-aligned box standing on the floor. What
    /// <see cref="SceneRenderer"/> bakes into its one shared vertex buffer and otherwise forgets — recorded as
    /// it is placed, so the answer is the placement the frame draws, <c>sceneseed</c> and all.
    /// </summary>
    public readonly struct GridSolid
    {
        /// <summary>The middle of the solid's footprint, on the floor.</summary>
        public readonly Vector3 BaseCentre;

        /// <summary>Width along X, height, depth along Z.</summary>
        public readonly Vector3 Size;

        /// <summary>A cube (a Life board on every face, wide enough to show it whole) rather than a tower.</summary>
        public readonly bool IsCube;

        public GridSolid(Vector3 baseCentre, Vector3 size, bool isCube)
        {
            BaseCentre = baseCentre;
            Size = size;
            IsCube = isCube;
        }

        /// <summary>The solid's centre, half-way up.</summary>
        public Vector3 Centre => BaseCentre + Vector3.Up * (Size.Y * 0.5f);

        /// <summary>How far a point is from the solid's surface — 0 inside it.</summary>
        public float Distance(Vector3 point)
        {
            Vector3 half = Size * 0.5f;
            Vector3 offset = point - Centre;
            Vector3 outside = new(
                MathF.Max(MathF.Abs(offset.X) - half.X, 0f),
                MathF.Max(MathF.Abs(offset.Y) - half.Y, 0f),
                MathF.Max(MathF.Abs(offset.Z) - half.Z, 0f));

            return outside.Length();
        }
    }

    /// <summary>
    /// The Grid's landmark (#512) as the shape it is: a ring standing on edge, its plane facing the arena. The
    /// faceted torus is taken as the annular slab it is cut from (between the inner and the outer radius, the
    /// band's width thick), which contains every facet — so a lens that clears this clears the ring.
    /// </summary>
    public readonly struct GridRing
    {
        /// <summary>The ring's centre, up in the air — its lowest point rests on the floor.</summary>
        public readonly Vector3 Centre;

        /// <summary>The ring's axis: the normal of its plane, pointing back at the arena.</summary>
        public readonly Vector3 PlaneNormal;

        public readonly float InnerRadius;
        public readonly float OuterRadius;
        public readonly float HalfWidth;

        public GridRing(Vector3 centre, Vector3 planeNormal, float innerRadius, float outerRadius, float halfWidth)
        {
            Centre = centre;
            PlaneNormal = planeNormal;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            HalfWidth = halfWidth;
        }

        /// <summary>How far a point is from the ring's slab — 0 inside it.</summary>
        public float Distance(Vector3 point)
        {
            Vector3 offset = point - Centre;
            float along = Vector3.Dot(offset, PlaneNormal);
            float radial = (offset - along * PlaneNormal).Length();

            float outRadial = MathF.Max(MathF.Max(InnerRadius - radial, radial - OuterRadius), 0f);
            float outAlong = MathF.Max(MathF.Abs(along) - HalfWidth, 0f);

            return MathF.Sqrt(outRadial * outRadial + outAlong * outAlong);
        }
    }
}
