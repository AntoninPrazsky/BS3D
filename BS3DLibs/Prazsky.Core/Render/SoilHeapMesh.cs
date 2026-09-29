using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The heap of earth at a tree's foot on the savanna (#670): a low, lobed mound of the plain's own bare soil that
    /// the trunk rises out of — what every reference of an acacia's foot drew first, red earth piled round the bark
    /// with the leaves and the grass in it. Built at unit radius and scaled uniformly by the planting, so its normals
    /// stay true; the height is a share of the radius, and a flatter heap is a second mesh rather than a squashed one.
    /// <para>
    /// A thin wrapper over <see cref="LatheMesh"/> for the reason <see cref="TermiteMoundMesh"/> is: a turned solid
    /// with a wobble, and the lathe carries the winding, the index width and the seam-closing irregularity.
    /// </para>
    /// </summary>
    public sealed class SoilHeapMesh : IProceduralMesh, IDisposable
    {
        private readonly LatheMesh _lathe;

        public VertexBuffer VertexBuffer => _lathe.VertexBuffer;
        public IndexBuffer IndexBuffer => _lathe.IndexBuffer;
        public int PrimitiveCount => _lathe.PrimitiveCount;
        public BoundingSphere BoundingSphere => _lathe.BoundingSphere;

        /// <summary>The heap's height at its middle, as a share of its radius.</summary>
        public float Height { get; }

        /// <summary>
        /// How high the heap stands at <paramref name="along"/> of its radius out from the middle, as a share of its
        /// radius — the profile below, smoothed: what a tuft planted on it stands on (0 past the rim).
        /// </summary>
        public float SurfaceAt(float along) => along >= 1f ? 0f : Height * (1f - along * along);

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="height">The height at the middle as a share of the unit radius.</param>
        /// <param name="irregularityPhase">Offsets the lobes, so two heaps do not share a rim.</param>
        public SoilHeapMesh(GraphicsDevice graphicsDevice, float height, float irregularityPhase = 0f)
        {
            Height = height;

            //Traced top → outside → underside, the lathe's outward direction: a broad low dome whose rim runs a
            //little under the ground, so no side of it shows an edge on a slope. The wobble is full on the flank,
            //where it lobes the rim the way spilled soil spreads, and eased off over the crown the trunk rises from.
            var profile = new List<LathePoint>
            {
                new(0f,    height,          crease: true),
                new(0.3f,  height * 0.92f,  wobble: 0.4f),
                new(0.55f, height * 0.7f,   wobble: 0.8f),
                new(0.78f, height * 0.38f,  wobble: 1f),
                new(0.95f, height * 0.1f,   wobble: 1f),
                new(1.1f,  -height * 0.35f, crease: true, wobble: 0.9f),
                new(0f,    -height * 0.35f)
            };

            _lathe = new LatheMesh(graphicsDevice, profile, 20, irregularityAmplitude: 0.16f, irregularityPhase: irregularityPhase);
        }

        public void Dispose() => _lathe.Dispose();
    }
}
