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
        /// <param name="irregularityPhase">Offsets the wobble pattern, so two stumps of different
        /// proportions do not undulate identically — see <see cref="LatheMesh"/>.</param>
        public StumpMesh(GraphicsDevice graphicsDevice, float radius, float height, int segments = 10,
            float irregularityPhase = 0f)
        {
            float topRadius = radius * 0.78f;
            float heartwoodY = height * 0.94f;

            //Traced from the heartwood centre out across the sawn cut (facing up), up the shallow inner wall
            //to the bark rim, then down the irregular bark flank to the ground and in along the buried
            //underside — the top → outside → underside direction LatheMesh documents. The heartwood sits a
            //little below the bark rim, so the cut is not a knife-edge disc.
            var profile = new List<LathePoint>
            {
                new(0f,                    heartwoodY, crease: true),
                new(topRadius * 0.86f,     heartwoodY),                     //the sawn cut, facing the sky
                new(topRadius,             height,  crease: true),          //bark rim, hard edge
                new(radius,                height * 0.4f, wobble: 1f),
                new(radius,                0f,      crease: true),          //hard arris at the ground
                new(0f,                    0f)
            };

            //Less irregular than a rock: bark is rough, but the trunk is still a trunk.
            _lathe = new LatheMesh(graphicsDevice, profile, segments, irregularityAmplitude: radius * 0.10f,
                irregularityPhase: irregularityPhase);
        }

        public void Dispose() => _lathe.Dispose();
    }
}
