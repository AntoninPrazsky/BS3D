using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A drift of sand banked against the island's drum on its windward side (#550): the bank a wind leaves
    /// against anything standing in its way — deepest on the face the wind hits, thinning round the drum to
    /// nothing, and running out from the face onto the ground in the concave slope a slip face has. Built
    /// in the island's own frame (the top at y = 0, the foot at −<c>footDepth</c>), as one sheet over the
    /// ground: the top surface only, since its underside is in the ground and its inner edge inside the
    /// drum, and its two ends taper to zero height so no open edge ever shows.
    /// <para>
    /// It exists because #538 drew the same drift as a <i>tint</i> on the drum — a band of sand colour rising
    /// on the windward face — and the owner's ruling is that a shape is geometry, not a texture: a drift has
    /// a silhouette against the sky and a slope the sun rakes, and paint on a vertical wall has neither.
    /// </para>
    /// <para>
    /// Wound through <see cref="MeshBuilder"/>, which corrects every facet against the normal it is meant to
    /// show; the per-vertex normals are taken from the sheet's own tangents, so the ripples across the drift
    /// shade as ripples rather than as a flat sheet with a pattern on it.
    /// </para>
    /// </summary>
    public sealed class SandDriftMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        //Facets round the drum across the drift's arc, and steps out from the drum's face to the ground
        private const int ARC_STEPS = 96;
        private const int OUT_STEPS = 28;

        /// <param name="wind">The wind's direction over the ground, as the scene states it (x, z); need not be
        /// unit. The drift lies on the side the wind arrives from — against the face whose outward normal points
        /// back along the wind.</param>
        /// <param name="innerRadius">Where the sheet starts, inside the drum's surface so no seam between the
        /// two can open whatever the drum's wobble does there.</param>
        /// <param name="footDepth">How far below the island's top its foot lies — the ground's height in the
        /// island's frame, negative.</param>
        /// <param name="height">How high the drift stands against the face the wind hits, above the ground.</param>
        /// <param name="reach">How far out from the drum the drift runs onto the ground on that face.</param>
        /// <param name="halfArc">Half the arc round the drum the drift covers, in radians; at its ends the bank
        /// has tapered to nothing.</param>
        public SandDriftMesh(GraphicsDevice device, Vector2 wind, float innerRadius, float footDepth,
            float height, float reach, float halfArc)
        {
            Vector2 facing = wind.LengthSquared() > 1e-8f ? -Vector2.Normalize(wind) : -Vector2.UnitX;
            float centreAngle = MathF.Atan2(facing.Y, facing.X);

            //Every vertex of the sheet first, so the normals can be taken from neighbours
            var positions = new Vector3[ARC_STEPS + 1, OUT_STEPS + 1];

            for (int a = 0; a <= ARC_STEPS; a++)
            {
                float u = (float)a / ARC_STEPS * 2f - 1f;               //-1 .. 1 across the arc
                float angle = centreAngle + u * halfArc;
                float cos = MathF.Cos(angle), sin = MathF.Sin(angle);

                //How much of the drift is here: full on the face the wind hits, gone at the ends. Squared
                //cosine, so it leaves the ends flat rather than at a kink.
                float presence = MathF.Cos(u * MathHelper.PiOver2);
                presence *= presence;

                float bankHeight = height * presence;
                float bankReach = reach * (0.3f + 0.7f * presence);

                for (int t = 0; t <= OUT_STEPS; t++)
                {
                    float s = (float)t / OUT_STEPS;                     //0 at the drum, 1 at the drift's toe

                    //The slip face: meets the drum at the bank's height, falls concave and flattens out onto
                    //the ground, then sinks a little under it so the toe never floats over a grain of relief
                    float slope = (1f - s) * (1f - s) * (1f + s);
                    float y = footDepth + bankHeight * slope - 0.35f * s;

                    //Wind ripples across the drift, at right angles to the wind, faint, wandering a little along
                    //the arc and fading at the toe - a first cut at twice this amplitude and dead straight read as
                    //stripes on the shaded slope
                    y += 0.012f * presence * (1f - s) * MathF.Sin(s * 38f + 2.5f * MathF.Sin(u * 4f));

                    float radius = innerRadius + bankReach * s;
                    positions[a, t] = new Vector3(radius * cos, y, radius * sin);
                }
            }

            //Normals from the sheet's own tangents, central differences where there are two neighbours
            var normals = new Vector3[ARC_STEPS + 1, OUT_STEPS + 1];

            for (int a = 0; a <= ARC_STEPS; a++)
            {
                for (int t = 0; t <= OUT_STEPS; t++)
                {
                    Vector3 alongArc = positions[Math.Min(a + 1, ARC_STEPS), t] - positions[Math.Max(a - 1, 0), t];
                    Vector3 outward = positions[a, Math.Min(t + 1, OUT_STEPS)] - positions[a, Math.Max(t - 1, 0)];
                    Vector3 normal = Vector3.Cross(alongArc, outward);
                    if (normal.Y < 0f) normal = -normal;
                    normals[a, t] = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.Up;
                }
            }

            MeshBuilder builder = new();

            for (int a = 0; a < ARC_STEPS; a++)
            {
                for (int t = 0; t < OUT_STEPS; t++)
                {
                    Vector3 p00 = positions[a, t], p10 = positions[a + 1, t], p11 = positions[a + 1, t + 1], p01 = positions[a, t + 1];
                    Vector3 n00 = normals[a, t], n10 = normals[a + 1, t], n11 = normals[a + 1, t + 1], n01 = normals[a, t + 1];
                    Vector3 faceNormal = n00 + n10 + n11 + n01;

                    builder.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, faceNormal);
                }
            }

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(device);

            float outer = innerRadius + reach;
            BoundingSphere = new BoundingSphere(new Vector3(0f, footDepth + height * 0.5f, 0f),
                new Vector2(outer, height).Length());
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer = null;
        }
    }
}
