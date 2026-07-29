using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A cut tree stump: a short, slightly tapering log with a flat sawn top — the hard arris where the bark
    /// meets the cut is the detail that makes it read as a stump rather than a post. Built as a
    /// <see cref="LatheMesh"/> with a modest irregularity (the bark is rough, but a stump is still roughly
    /// cylindrical), and a small inner ring at the top to suggest the heartwood without modelling rings.
    /// </summary>
    public sealed class StumpMesh : IProceduralMesh, IDisposable
    {
        private readonly LatheMesh _lathe;

        public VertexBuffer VertexBuffer => _lathe.VertexBuffer;
        public IndexBuffer IndexBuffer => _lathe.IndexBuffer;
        public int PrimitiveCount => _lathe.PrimitiveCount;
        public BoundingSphere BoundingSphere => _lathe.BoundingSphere;

        /// <param name="graphicsDevice">The device the buffers are created on.</param>
        /// <param name="radius">Radius at the base (the top is narrower — a stump flares at the roots).</param>
        /// <param name="height">Height of the cut stump.</param>
        /// <param name="segments">Facets around the axis.</param>
        public StumpMesh(GraphicsDevice graphicsDevice, float radius, float height, int segments = 10)
        {
            float topRadius = radius * 0.78f;

            //Up the side (bark, irregular), a hard crease at the sawn top, then a shallow inset across the
            //cut so it is not a knife-edge disc — the heartwood sits a little below the bark rim.
            var profile = new List<LathePoint>
            {
                new(0f,         0f),
                new(radius,     0f,      crease: true),    //hard arris at the ground
                new(radius,     height * 0.4f, wobble: 1f),
                new(topRadius,  height,  wobble: 0.6f),
                new(topRadius,  height,  crease: true),    //the saw cut: bark rim, hard edge
                new(topRadius * 0.86f, height - height * 0.06f),  //heartwood sits just below the rim
                new(0f,         height - height * 0.06f, crease: true)
            };

            //Less irregular than a rock: bark is rough, but the trunk is still a trunk.
            _lathe = new LatheMesh(graphicsDevice, profile, segments, irregularityAmplitude: radius * 0.10f);
        }

        public void Dispose() => _lathe.Dispose();
    }
}
