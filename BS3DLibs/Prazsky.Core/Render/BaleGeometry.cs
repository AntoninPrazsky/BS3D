using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>A round hay bale lying on its side</b> (#943), with no device: a straw cylinder along X, resting on the ground,
    /// closed by a dished disc at each end. <see cref="MeadowScatter"/> uploads it.
    /// <para>
    /// <b>The ends are made of the tube's own ring.</b> Until #943 each end was a separate <c>AddCap</c> disc laid at 0.98
    /// of the ring's radius, which left a slit about 0.03 wide all round each end: open to the hollow inside the roll, and
    /// under back-face culling the owner saw straight through it ("the hay rolls clearly have a hole along their edges").
    /// The rim of each end is now a copy of the tube's last ring, vertex for vertex, so no sliver can open between
    /// the two, and every edge of the mesh belongs to exactly two triangles.
    /// </para>
    /// <para>
    /// <b>The end is dished and carries a polar UV of its own</b>, for the wound-straw shader of #971. A round baler
    /// winds the windrow in a spiral from a loose core outward, so a bale's end is concentric layers round a looser
    /// centre, a little hollow. The disc recedes <see cref="DISH"/> of the radius at its centre, along a parabola, in
    /// <see cref="DISH_STEPS"/> rings (with normals off that parabola's slope, so it shades as a dish), and its UV is
    /// <c>(<see cref="CAP_U"/> + the angle round the axis as a share of a turn, the distance from the axis as a share of the
    /// radius)</c>: u from <see cref="CAP_U"/> to <see cref="CAP_U"/> + 1, v from 0 at the core to 1 at the rim. The side's
    /// u runs 0 to 1 round and its v 0 to 1 along, so <c>uv.x &gt;= 1</c> tells an end from the side, and the end's spiral
    /// needs no second geometry change. Wound as <see cref="TubeGeometry.AddFan"/> winds its discs.
    /// </para>
    /// </summary>
    internal static class BaleGeometry
    {
        /// <summary>Facets round the bale: the tube's, and so the rim of each end. 32 since #971 (it was 18): an end close to a
        /// lens showed its corners, and <c>Acacia.fx</c>'s straw takes this figure as <c>STRAW_SIDES</c>.</summary>
        public const int SIDES = 32;

        /// <summary>How far an end's centre recedes into the bale, as a share of its radius (a quarter of a hand on a roll as
        /// tall as a post): shallow enough to read as a flat end from the front, deep enough to catch the light at a
        /// grazing angle and to give the spiral a dish to shade.</summary>
        public const float DISH = 0.06f;

        /// <summary>Rings between an end's rim and its centre, the centre's point not counted: the parabola of the dish in
        /// that many steps, and the polar UV's radius sampled as often.</summary>
        public const int DISH_STEPS = 2;

        /// <summary>Where the end's u starts: the side's runs 0 to 1, so an end's lies past it, 2 to 3.</summary>
        public const float CAP_U = 2f;

        /// <summary>The bale's vertices and indices: a tube of <paramref name="radius"/> from <c>-halfLength</c> to
        /// <c>+halfLength</c> along X, its axis a radius over the ground, and the two ends.</summary>
        public static (List<VertexPositionNormalTexture> Vertices, List<short> Indices) Build(float radius, float halfLength)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Vector3 a = new(-halfLength, radius, 0f), b = new(halfLength, radius, 0f);

            //The tube is two rings, sides only: ring 0 at a, SIDES + 1 vertices (the last on the first), then ring 1 at b
            TubeGeometry.AddSweep(v, idx, SIDES, new[] { a, b }, new[] { radius, radius });
            AddDish(v, idx, ring: 0, centre: a, outward: -Vector3.UnitX, radius, reversed: true);
            AddDish(v, idx, ring: SIDES + 1, centre: b, outward: Vector3.UnitX, radius, reversed: false);
            return (v, idx);
        }

        /// <summary>
        /// One end: a dished disc whose outer ring copies the tube's ring at <paramref name="ring"/> (SIDES + 1 vertices),
        /// <paramref name="reversed"/> false for an end that faces along the tube's axis (the same winding as
        /// <see cref="TubeGeometry.AddFan"/> gives that end), true for the end that faces back.
        /// </summary>
        private static void AddDish(List<VertexPositionNormalTexture> v, List<short> idx, int ring, Vector3 centre, Vector3 outward,
            float radius, bool reversed)
        {
            float depth = DISH * radius;
            int[] first = new int[DISH_STEPS + 1];

            for (int k = 0; k <= DISH_STEPS; k++)
            {
                //The rim is the tube's own ring (share 1); the last ring is the centre's point, once per sector so that its
                //u can follow the sector's middle
                float share = 1f - k / (float)DISH_STEPS;
                bool core = k == DISH_STEPS;
                first[k] = v.Count;
                for (int s = 0; s < (core ? SIDES : SIDES + 1); s++)
                {
                    Vector3 rim = v[ring + s].Position;
                    Vector3 radial = (rim - centre) / radius;
                    Vector3 position = k == 0 ? rim : centre + (rim - centre) * share - outward * (depth * (1f - share * share));
                    //The surface z = -depth * (1 - r^2 / R^2) along `outward`: its slope toward the centre is 2 depth r / R^2
                    Vector3 normal = Vector3.Normalize(outward - radial * (2f * depth / radius * share));
                    float turn = core ? (s + 0.5f) / SIDES : s / (float)SIDES;
                    v.Add(new VertexPositionNormalTexture(position, normal, new Vector2(CAP_U + turn, share)));
                }
            }

            for (int k = 0; k < DISH_STEPS; k++)
            {
                bool toCore = k == DISH_STEPS - 1;
                for (int s = 0; s < SIDES; s++)
                {
                    short outer = (short)(first[k] + s), outerNext = (short)(first[k] + s + 1);
                    short inner = (short)(first[k + 1] + s), innerNext = (short)(first[k + 1] + (toCore ? s : s + 1));
                    if (toCore)
                    {
                        //A triangle to the centre's point of this sector
                        Add(idx, reversed, inner, outerNext, outer);
                    }
                    else
                    {
                        Add(idx, reversed, innerNext, outerNext, outer);
                        Add(idx, reversed, innerNext, outer, inner);
                    }
                }
            }

            static void Add(List<short> idx, bool reversed, short a, short b, short c)
            {
                idx.Add(a);
                idx.Add(reversed ? c : b);
                idx.Add(reversed ? b : c);
            }
        }
    }
}
