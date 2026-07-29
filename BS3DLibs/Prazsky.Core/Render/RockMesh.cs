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

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="radius">Half-width of the boulder at its base.</param>
        /// <param name="height">Peak height above the base.</param>
        /// <param name="segments">Facets around the axis. Few is fine — the irregularity is what breaks the
        /// silhouette, not the facet count, and a low-poly rock reads as quarried rather than machined.</param>
        public RockMesh(GraphicsDevice graphicsDevice, float radius, float height, int segments = 8)
        {
            //A flattened dome: a flat underside at the ground, a wide low shoulder, and a rounded crown that
            //sits below the peak radius so the top reads as worn rather than pointed. Every ring carries the
            //full wobble, so the whole stone is irregular, not just its middle.
            var profile = new List<LathePoint>
            {
                new(0f,        0f),
                new(radius,    0f,     crease: true),    //hard arris where the stone meets the ground
                new(radius,    height * 0.35f, wobble: 1f),
                new(radius * 0.82f, height * 0.7f,  wobble: 1f),
                new(radius * 0.5f,  height,        wobble: 1f),
                new(0f,        height, crease: true)     //closed crown
            };

            //The irregularity is a large share of the base radius — enough that two rocks of the same size do
            //not share a silhouette, but not so much the surface self-intersects.
            _lathe = new LatheMesh(graphicsDevice, profile, segments, irregularityAmplitude: radius * 0.28f);
        }

        public void Dispose() => _lathe.Dispose();
    }
}
