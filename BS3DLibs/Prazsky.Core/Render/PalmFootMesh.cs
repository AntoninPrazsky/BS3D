using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Where a coconut palm meets the sand (#754): the mat of fibrous roots a palm spreads round its bole, drawn as a fan
    /// of thin dark roots that leave the trunk just over the sand and dive back into it a foot's breadth or two out.
    /// The owner: "the part of the trunk that goes into the ground" could still be improved; the references
    /// (<c>C:\Users\panrd\AI\sd\out\754-klein</c> and <c>754-zimage</c>) draw a bulb with a nest of thin fibres round it, and a
    /// palm has no buttress roots, so it is fibres and not the savanna's roots (#670).
    /// <para>
    /// In units of the trunk's own radius at its foot, so one mesh serves every palm scaled by that radius; it stands
    /// on the ground's own normal and is not leaned with its palm (the palm's lean would lift one side of the fan off
    /// the sand), so the roots start inside the trunk's sunk foot at <see cref="START_RADIUS"/>, where any lean the
    /// palms take still hides their first ring. Every root is a closed tube: capped at both ends, the start inside the
    /// trunk and the end under the sand (<c>Tools/WindingCheck --openness</c>).
    /// </para>
    /// </summary>
    public sealed class PalmFootMesh : IProceduralMesh, IDisposable
    {
        //How many roots, how far in they start (in foot radii: inside the trunk, which is at least that wide there whatever
        //its lean), how far out the longest dives, and how thick they are where they leave the trunk and at their tips
        private const int ROOTS = 40;
        private const float START_RADIUS = 0.62f;
        private const float REACH = 2.0f;
        private const float ROOT_RADIUS = 0.1f, TIP_RADIUS = 0.025f;
        private const int SIDES = 4;

        private readonly UploadedMesh _mesh;

        public VertexBuffer VertexBuffer => _mesh.VertexBuffer;
        public IndexBuffer IndexBuffer => _mesh.IndexBuffer;
        public int PrimitiveCount => _mesh.PrimitiveCount;
        public BoundingSphere BoundingSphere => _mesh.BoundingSphere;

        public PalmFootMesh(GraphicsDevice device, int seed)
        {
            (List<VertexPositionNormalTexture> v, List<short> idx) = Build(seed);
            _mesh = new UploadedMesh(device, v, idx, new BoundingSphere(Vector3.Zero, REACH * 1.1f));
        }

        /// <summary>The fan's vertices and indices, with no device.</summary>
        internal static (List<VertexPositionNormalTexture> Vertices, List<short> Indices) Build(int seed)
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            Random rng = new(seed);

            for (int r = 0; r < ROOTS; r++)
            {
                //Round the bole, unevenly and densely - a mat, not a spider's legs; most reach out a foot's breadth or so, a
                //quarter are short stubs
                float angle = (r + 0.7f * (float)rng.NextDouble()) / ROOTS * MathHelper.TwoPi;
                float length = rng.NextDouble() < 0.25 ? 0.45f + 0.3f * (float)rng.NextDouble() : 0.7f + 0.3f * (float)rng.NextDouble();
                float curl = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                float lift = 0.1f + 0.12f * (float)rng.NextDouble();
                float thickness = 0.6f + 0.8f * (float)rng.NextDouble();

                //From inside the trunk just over the sand, arching out over it and diving in: the root's height over the
                //ground along its run, its angle drifting by the curl
                Vector3 At(float along, float height)
                {
                    float a = angle + curl * along;
                    float out_ = START_RADIUS + (REACH - START_RADIUS) * length * along;
                    return new Vector3(MathF.Cos(a) * out_, height, MathF.Sin(a) * out_);
                }

                Vector3[] points =
                {
                    At(0f, lift),
                    At(0.25f, lift * 0.8f),
                    At(0.55f, lift * 0.35f),
                    At(0.8f, 0.02f),
                    At(1f, -0.16f),
                };
                float rootRadius = ROOT_RADIUS * thickness;
                float[] radii = { rootRadius, rootRadius * 0.85f, rootRadius * 0.65f, (rootRadius + TIP_RADIUS) * 0.45f, TIP_RADIUS };
                TubeGeometry.AddSweep(v, idx, SIDES, points, radii, capStart: true, capEnd: true);
            }

            return (v, idx);
        }

        public void Dispose() => _mesh.Dispose();
    }
}
