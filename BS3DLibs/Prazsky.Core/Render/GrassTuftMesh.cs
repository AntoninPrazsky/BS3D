using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A bunch of tall grass for the savanna (#451): a dozen or so tapering blades fanning up and out from
    /// one root, each bent over by its own amount, every blade a two-sided ribbon. Real blades rather than a
    /// spiked lump — the lump was tried first (a <see cref="FoliageMesh"/> in a spiked style) and at the
    /// sphere's sixteen slices the spikes rounded off into a yellow potato from any distance the camera
    /// stands at. What makes a tuft read as grass is the silhouette of separate blades against the field,
    /// and a blade has to be a blade.
    /// <para>
    /// Two-sided by construction: each ribbon segment is emitted twice, wound for either face with its own
    /// normal, so the draw stays under the ordinary back-face culling every other savanna mesh uses and
    /// never needs a <c>CullNone</c> state of its own. About fifty triangles a tuft.
    /// </para>
    /// </summary>
    public sealed class GrassTuftMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private const int SEGMENTS = 3;

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="radius">How far the blades reach out from the root.</param>
        /// <param name="height">How tall the tallest blade stands.</param>
        /// <param name="seed">Rolls the blades' bearings, leans and lengths.</param>
        public GrassTuftMesh(GraphicsDevice device, float radius, float height, int seed)
        {
            Random rng = new(seed);
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();

            int blades = 11 + rng.Next(5);
            float reach = 0f;
            for (int b = 0; b < blades; b++)
            {
                float yaw = MathHelper.TwoPi * b / blades + (float)(rng.NextDouble() - 0.5) * 0.5f;
                Vector3 outward = new(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
                Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.Up, outward));

                //Every blade starts a little off the root, leans out by its own angle and bends over further
                //the higher it goes - a fountain rather than a brush.
                Vector3 root = outward * (radius * 0.12f * (float)rng.NextDouble()) + Vector3.Up * 0.02f;
                float length = height * (0.65f + 0.45f * (float)rng.NextDouble());
                float lean = 0.3f + 0.6f * (float)rng.NextDouble();
                float bend = 0.25f + 0.35f * (float)rng.NextDouble();
                float width = radius * (0.10f + 0.06f * (float)rng.NextDouble());

                Vector3[] spine = new Vector3[SEGMENTS + 1];
                for (int s = 0; s <= SEGMENTS; s++)
                {
                    float t = s / (float)SEGMENTS;
                    float along = length * t;
                    Vector3 p = root
                        + outward * (along * MathF.Sin(lean) + length * bend * t * t)
                        + Vector3.Up * (along * MathF.Cos(lean) - length * bend * 0.6f * t * t);
                    spine[s] = p;
                    reach = MathF.Max(reach, p.Length());
                }

                for (int s = 0; s < SEGMENTS; s++)
                {
                    float w0 = width * (1f - s / (float)SEGMENTS);
                    float w1 = width * (1f - (s + 1) / (float)SEGMENTS);
                    Vector3 a = spine[s] - side * w0, bq = spine[s] + side * w0;
                    Vector3 c = spine[s + 1] + side * w1, d = spine[s + 1] - side * w1;
                    Vector3 tangent = spine[s + 1] - spine[s];
                    Vector3 n = Vector3.Normalize(Vector3.Cross(side, tangent));
                    TubeGeometry.AddRibbon(v, idx, a, bq, c, d, n);
                    TubeGeometry.AddRibbon(v, idx, a, bq, c, d, -n);
                }
            }

            PrimitiveCount = idx.Count / 3;
            BoundingSphere = new BoundingSphere(new Vector3(0f, height * 0.4f, 0f), reach + radius * 0.2f);
            (VertexBuffer, IndexBuffer) = TubeGeometry.Upload(device, v, idx);
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose(); VertexBuffer = null;
            IndexBuffer?.Dispose(); IndexBuffer = null;
        }
    }
}
