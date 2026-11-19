using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A weathered boulder for the forest floor: a low, rounded stone that is <b>not</b> a surface of
    /// revolution — built as a <see cref="LatheMesh"/> with a strong radial irregularity, so it reads as a
    /// glacial erratic rather than a turned pillar. The profile is a flattened dome (wider than it is tall),
    /// the irregularity large enough that the silhouette breaks visibly from round.
    /// <para>
    /// A thin mesh over <see cref="LatheMesh"/> rather than its own generator: a rock is the same kind of
    /// solid a dressed stone is, only rougher, and reusing the lathe keeps the winding, the 16/32-bit index
    /// choice and the seam-closing irregularity shared with the island and the other forest scatter meshes.
    /// </para>
    /// </summary>
    public sealed class RockMesh : IProceduralMesh, IDisposable
    {
        private readonly LatheMesh _lathe;

        public VertexBuffer VertexBuffer => _lathe.VertexBuffer;
        public IndexBuffer IndexBuffer => _lathe.IndexBuffer;
        public int PrimitiveCount => _lathe.PrimitiveCount;
        public BoundingSphere BoundingSphere => _lathe.BoundingSphere;

        /// <summary>The underside's radius, at scale 1 (#938): what a planter rests the flat bottom by.</summary>
        public float Radius { get; }

        /// <summary>The peak's height above the base, at scale 1.</summary>
        public float Height { get; }

        /// <summary>
        /// How high the stone's surface stands over a spot <paramref name="radial"/> base radii from its axis, at scale 1 (#938):
        /// the profile the lathe is traced from (the wobble left out), so a rock that rests on this one is laid on its shoulder
        /// and not in the air over it.
        /// </summary>
        public float SurfaceAt(float radial)
        {
            if (radial >= 1f) return 0f;
            if (radial <= 0.5f) return Height;
            if (radial <= 0.82f) return Height * (1f - 0.3f * (radial - 0.5f) / 0.32f);
            return Height * (0.7f - 0.35f * (radial - 0.82f) / 0.18f);
        }

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="radius">Half-width of the boulder at its base.</param>
        /// <param name="height">Peak height above the base.</param>
        /// <param name="segments">
        /// Facets around the axis, and the count is a sampling decision rather than a smoothness one:
        /// <see cref="LatheMesh.Irregularity"/> runs at 3, 7 and 13 waves per revolution, sampled at exactly
        /// one angle per facet, so any harmonic past Nyquist folds onto a lower one instead of adding detail.
        /// Sixteen facets resolve the 3- and the 7-wave terms; the 13-wave term folds onto the 3-cycle
        /// (identically at eight facets and at sixteen) and merely deepens the tri-lobe. What eight facets cost
        /// was the 7-wave term, which aliased down to a one-cycle lateral shift — leaving a gently tri-lobed
        /// dome that reads as a bun rather than a stone, whatever the amplitude was set to. Resolving the
        /// 13-wave term as well would need more than 26 facets, and about fifty to render it recognisably.
        /// </param>
        /// <param name="irregularityPhase">Offsets the wobble pattern, so two boulders of different
        /// proportions do not undulate identically — see <see cref="LatheMesh"/>.</param>
        public RockMesh(GraphicsDevice graphicsDevice, float radius, float height, int segments = 16,
            float irregularityPhase = 0f)
        {
            //A flattened dome: a worn rounded crown, a wide low shoulder, and a flat underside at the ground.
            //Traced top → outside → underside, the direction LatheMesh documents (the other way the stone is
            //inside out — same silhouette, far side drawn, shading dark). Every ring carries the full wobble,
            //so the whole stone is irregular, not just its middle.
            var profile = new List<LathePoint>
            {
                new(0f,             height, crease: true),   //worn crown
                new(radius * 0.5f,  height,         wobble: 1f),
                new(radius * 0.82f, height * 0.7f,  wobble: 1f),
                new(radius,         height * 0.35f, wobble: 1f),
                new(radius,         0f,     crease: true, wobble: 1f),   //hard arris where the stone meets the ground
                new(0f,             0f)
            };

            Radius = radius;
            Height = height;

            //The irregularity is a large share of the base radius — enough that two rocks of the same size do
            //not share a silhouette, but not so much the surface self-intersects.
            _lathe = new LatheMesh(graphicsDevice, profile, segments, irregularityAmplitude: radius * 0.34f,
                irregularityPhase: irregularityPhase);

            //The side's rings as drawn, the wobble in them: the crown's ring down to the arris, what Off reads
            Vector3[,] rings = LatheMesh.RingPositions(profile, segments, radius * 0.34f, irregularityPhase);
            _sideY = new float[SIDE_RINGS];
            _side = new float[SIDE_RINGS, segments];
            var points = new Vector3[SIDE_RINGS * segments + 1];
            points[^1] = new Vector3(0f, height, 0f);
            for (int k = 0; k < SIDE_RINGS; k++)
            {
                _sideY[k] = profile[k + 1].Y;
                for (int s = 0; s < segments; s++)
                {
                    _side[k, s] = new Vector2(rings[k + 1, s].X, rings[k + 1, s].Z).Length();
                    points[k * segments + s] = rings[k + 1, s];
                }
            }
            SurfacePoints = points;
        }

        //The rings of the side, from the crown's edge (at the full height) down to the arris on the ground, and how far each
        //reaches from the axis at each facet
        private const int SIDE_RINGS = 4;
        private readonly float[] _sideY;
        private readonly float[,] _side;

        /// <summary>The stone's own vertices, at scale 1 (#1157): every vertex of its side, the wobble in them, and the middle of
        /// its crown. What a thing wider than the stone is asked about: whether any of them stands inside it.</summary>
        internal IReadOnlyList<Vector3> SurfacePoints { get; }

        /// <summary>
        /// How far a point in the stone's own space (scale 1, the flat underside on y = 0) stands outside the stone (#1157):
        /// positive outside, negative inside, as deep as it lies within the nearer of the side, the flat top and the underside.
        /// Read off the lathe's own rings, the wobble in them, between the rings by height and between the facets by angle; the
        /// side's distance is taken across, which on a shoulder is a little more than the true one, so a point just outside a
        /// sloping shoulder reads a little farther out than it stands. What the savanna plants its grass and its trees clear of.
        /// </summary>
        internal float Off(Vector3 local)
        {
            float across = MathF.Sqrt(local.X * local.X + local.Z * local.Z);
            float side = across - SideRadius(MathF.Atan2(local.Z, local.X), MathHelper.Clamp(local.Y, 0f, Height));
            float over = local.Y > Height ? local.Y - Height : local.Y < 0f ? -local.Y : 0f;
            if (over > 0f) return side > 0f ? MathF.Sqrt(side * side + over * over) : over;
            return side > 0f ? side : MathF.Max(side, MathF.Max(local.Y - Height, -local.Y));
        }

        //How far the side reaches from the axis at a bearing (radians) and a height on 0..Height
        private float SideRadius(float bearing, float y)
        {
            int segments = _side.GetLength(1);
            float at = bearing / MathHelper.TwoPi * segments;
            if (at < 0f) at += segments;
            int s0 = Math.Min((int)at, segments - 1), s1 = (s0 + 1) % segments;
            float f = at - s0;

            int k = 0;
            while (k < SIDE_RINGS - 2 && y < _sideY[k + 1]) k++;
            float t = (_sideY[k] - y) / (_sideY[k] - _sideY[k + 1]);
            float upper = MathHelper.Lerp(_side[k, s0], _side[k, s1], f);
            float lower = MathHelper.Lerp(_side[k + 1, s0], _side[k + 1, s1], f);
            return MathHelper.Lerp(upper, lower, MathHelper.Clamp(t, 0f, 1f));
        }

        public void Dispose() => _lathe.Dispose();
    }
}
