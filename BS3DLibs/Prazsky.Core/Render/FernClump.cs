using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A clump of ferns for the daytime forest's floor on High and Ultra (#962): fronds rising out of one crown at the ground
    /// and arching out and down, each a strip of cards bent along its curve that the plant material cuts into a frond of paired
    /// leaflets (<c>Acacia.fx</c>'s <c>LeafMask</c>, <c>LeafStrength</c> 1: a pinnate leaf is what a fern's frond is). The
    /// references (#962's undergrowth, both models) draw the forest floor's ferns as exactly this: a shuttlecock of long
    /// fronds out of one centre, the inner ones younger, shorter and more upright, the outer ones lying out over the moss.
    /// <para>
    /// Device-free lists at a unit size (about a unit and a half across, a unit high), which the forest plants scaled; two-sided
    /// cards, so open by design (<c>WindingCheck</c>'s openness table names them). A frond's <c>u</c> runs from its stem at the
    /// crown to its tip and its <c>v</c> across it.
    /// </para>
    /// </summary>
    internal static class FernClump
    {
        //Cards along a frond's curve, the fronds a clump rolls (least, and how many more at most), and a frond's width against
        //its length
        private const int SEGMENTS = 5, FRONDS_MIN = 9, FRONDS_SPREAD = 6;
        private const float WIDTH = 0.34f;

        /// <summary>One clump's cards: vertices and int indices.</summary>
        public static (List<VertexPositionNormalTexture> Vertices, List<int> Indices) Build(int seed)
        {
            var vertices = new List<VertexPositionNormalTexture>();
            var indices = new List<int>();
            var rng = new Random(seed);

            int fronds = FRONDS_MIN + rng.Next(FRONDS_SPREAD);
            float spin = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int k = 0; k < fronds; k++)
            {
                //Spread round the crown on the golden angle, the inner ones (the young, last few) short and upright
                float bearing = spin + k * 2.39996f + 0.3f * ((float)rng.NextDouble() - 0.5f);
                bool young = k >= fronds - 3;
                float length = (young ? 0.55f : 0.85f) + 0.35f * (float)rng.NextDouble();
                float rise = MathHelper.ToRadians(young ? 62f + 14f * (float)rng.NextDouble() : 38f + 22f * (float)rng.NextDouble());
                AddFrond(vertices, indices, bearing, length, rise, 0.06f + 0.05f * (float)rng.NextDouble(),
                    0.35f * ((float)rng.NextDouble() - 0.5f));
            }
            return (vertices, indices);
        }

        //A frond from the crown out along `bearing`, `length` long, leaving at `rise` above the level and arching over until its
        //tip hangs; rolled about its own length by `roll`
        private static void AddFrond(List<VertexPositionNormalTexture> vertices, List<int> indices, float bearing, float length,
            float rise, float offset, float roll)
        {
            Vector3 out_ = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));
            Vector3 level = new(-out_.Z, 0f, out_.X);
            float lift = MathF.Sin(rise), reach = MathF.Cos(rise);

            //Height along the frond: up at its rise, then over; it peaks about half way out and its tip ends a little under that
            float fall = 0.78f * lift + 0.12f;
            Vector3 At(float s) => out_ * (offset + length * s * reach * (1f + 0.55f * s))
                + Vector3.Up * (length * (lift * s - fall * s * s) + 0.02f);

            int first = vertices.Count;
            for (int i = 0; i <= SEGMENTS; i++)
            {
                float s = i / (float)SEGMENTS;
                Vector3 p = At(s);
                Vector3 tangent = Vector3.Normalize(At(MathF.Min(s + 0.02f, 1f)) - At(MathF.Max(s - 0.02f, 0f)));
                Vector3 across = Vector3.Normalize(level - tangent * Vector3.Dot(level, tangent));
                Vector3 normal = Vector3.Normalize(Vector3.Cross(tangent, across));
                if (normal.Y < 0f) normal = -normal;
                //Rolled about the frond, more towards its tip, so a frond is not a flat ribbon
                float r = roll * s;
                across = Vector3.Normalize(across * MathF.Cos(r) + normal * MathF.Sin(r));
                normal = Vector3.Normalize(Vector3.Cross(tangent, across));
                if (normal.Y < 0f) normal = -normal;

                float half = length * WIDTH * 0.5f;
                vertices.Add(new VertexPositionNormalTexture(p - across * half, normal, new Vector2(s, 0f)));
                vertices.Add(new VertexPositionNormalTexture(p + across * half, normal, new Vector2(s, 1f)));
                if (i == 0) continue;

                int a = first + 2 * (i - 1);
                indices.Add(a);
                indices.Add(a + 1);
                indices.Add(a + 3);
                indices.Add(a);
                indices.Add(a + 3);
                indices.Add(a + 2);
            }
        }
    }
}
