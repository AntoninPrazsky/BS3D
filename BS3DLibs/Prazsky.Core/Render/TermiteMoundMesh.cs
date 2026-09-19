using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A termite mound for the savanna (#451): a tall spire of red earth, wide at the foot and rounded at the
    /// top, fluted down its sides. A <see cref="LatheMesh"/> with a strong irregularity so the flutes read in
    /// the silhouette; the finer vertical ridging is the shader's (<c>Acacia.fx</c>'s bark term, which a mound
    /// shares with a trunk — both are a column with its texture running up it).
    /// <para>
    /// A thin wrapper over the lathe for the reason <see cref="RockMesh"/> is: a mound is a turned solid with
    /// a wobble, and the lathe already carries the winding, the index width and the seam-closing irregularity.
    /// </para>
    /// </summary>
    public sealed class TermiteMoundMesh : IProceduralMesh, IDisposable
    {
        private readonly LatheMesh _lathe;

        public VertexBuffer VertexBuffer => _lathe.VertexBuffer;
        public IndexBuffer IndexBuffer => _lathe.IndexBuffer;
        public int PrimitiveCount => _lathe.PrimitiveCount;
        public BoundingSphere BoundingSphere => _lathe.BoundingSphere;

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="radius">Half-width at the foot.</param>
        /// <param name="height">Height of the spire above the foot.</param>
        /// <param name="irregularityPhase">Offsets the fluting, so two mounds do not share a silhouette.</param>
        public TermiteMoundMesh(GraphicsDevice graphicsDevice, float radius, float height, float irregularityPhase = 0f)
        {
            //Traced top → outside → underside, the lathe's outward direction. The wobble is full down the
            //whole flank and eased off at the very tip, which a mound rounds over smoothly; the foot spreads
            //into an apron of the same earth, the way the references' mounds sit in a skirt of it. The spire
            //itself is narrow - the reference mounds stand three to four times as tall as their column is
            //wide - so `radius` is the column's, and the apron reaches half as far again.
            var profile = new List<LathePoint>
            {
                new(0f,             height,         crease: true),
                new(radius * 0.14f, height * 0.97f, wobble: 0.5f),
                new(radius * 0.35f, height * 0.84f, wobble: 0.9f),
                new(radius * 0.55f, height * 0.64f, wobble: 1f),
                new(radius * 0.72f, height * 0.42f, wobble: 1f),
                new(radius * 0.88f, height * 0.20f, wobble: 1f),
                new(radius,         height * 0.08f, wobble: 1f),
                new(radius * 1.5f,  0f,             crease: true, wobble: 0.6f),
                new(0f,             0f)
            };

            //Sixteen facets resolve the 3- and 7-wave terms of the lathe's irregularity (see RockMesh); the
            //amplitude is a large share of the radius so the flutes show against the sky.
            _lathe = new LatheMesh(graphicsDevice, profile, 16, irregularityAmplitude: radius * 0.28f,
                irregularityPhase: irregularityPhase);
        }

        public void Dispose() => _lathe.Dispose();
    }
}
