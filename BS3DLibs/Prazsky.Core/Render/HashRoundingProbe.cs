using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Asks the GPU how it rounds the noise hash, once, and makes the C# copy round the same way</b> (#1134). Every
    /// terrain a CPU mirror places things on (<see cref="TerrainMirror"/>, Mars's big craters) is built on
    /// <c>Noise.fxh</c>'s hash, whose dot is two multiply-adds the driver may fuse or not: the desktop's RDNA2 fuses, the
    /// laptop's GCN does not, and a mirror rounded the other way stood up to 16 units off the drawn mountains. Making the
    /// shader's hash <c>precise</c> would have fixed the rounding on every GPU, but <c>precise</c> spreads back through
    /// every noise field's arithmetic (up to 1.2 ms at <c>Low</c> on the reference APU), so the shaders keep their hash
    /// and the copy follows the GPU instead.
    /// <para>
    /// <c>HashProbe.fx</c> hashes a <see cref="SIZE"/> square of inputs of the kind the terrains hash (a whole cell plus a
    /// seed, over the range they reach) and hands back each hash with the input it used; <see cref="Decide"/> compares
    /// them with <see cref="ShaderMath.Hash22(float, float, HashRounding)"/> under each rounding, and
    /// <see cref="ShaderMath.Rounding"/> takes the one that agrees with every sample. Anything less (no effect, no float
    /// target, an unreadable target, a GPU that rounds some third way) keeps the fused default and says so on one
    /// <c>[hash]</c> line, the mirrors then exact only on a GPU that fuses.
    /// </para>
    /// </summary>
    internal static class HashRoundingProbe
    {
        /// <summary>The target's side in pixels, so SIZE x SIZE samples. <c>HashProbe.fx</c>'s <c>HASH_PROBE_SIZE</c>.</summary>
        internal const int SIZE = 64;

        /// <summary>
        /// How far a sample may differ and still agree. The same rounding gives the same bits; a different one moves the
        /// hash by whole quanta of about 1/500, so anything between is room for a readback, not a judgement.
        /// </summary>
        private const float MATCH_TOLERANCE = 1e-5f;

        private static bool _probed;

        /// <summary>
        /// Draws the probe and sets <see cref="ShaderMath.Rounding"/>, the first time it is called in the process; every
        /// later call returns at once. <paramref name="fullScreenQuad"/> is four <see cref="VertexPosition"/> corners
        /// in normalized device coordinates, a triangle strip. The device's targets and states are put back.
        /// </summary>
        internal static void Run(GraphicsDevice device, ContentManager content, VertexBuffer fullScreenQuad)
        {
            if (_probed) return;
            _probed = true;

            Vector4[] samples;
            try
            {
                samples = Draw(device, content, fullScreenQuad);
            }
            catch (Exception ex)
            {
                //Any failure at all: the probe is an improvement on the default, never a reason not to start. MonoGame's
                //OpenGL ES backend refuses a float target or its readback with assorted exception types, and GamePi's
                //content has no HashProbe at all
                Console.WriteLine($"[hash] the probe could not be drawn ({ex.GetType().Name}: {ex.Message}): the terrain mirrors keep the {ShaderMath.Rounding.ToString().ToLowerInvariant()} hash");
                return;
            }

            Verdict verdict = Decide(samples);
            if (verdict.Rounding is HashRounding rounding)
            {
                ShaderMath.Rounding = rounding;
                Console.WriteLine($"[hash] the GPU rounds the noise hash {rounding.ToString().ToLowerInvariant()} ({verdict.Fused} of {samples.Length} samples agree fused, {verdict.Unfused} unfused): the terrain mirrors follow it");
            }
            else
            {
                Console.WriteLine($"[hash] no rounding agrees with this GPU ({verdict.Fused} of {samples.Length} samples fused, {verdict.Unfused} unfused, {verdict.Inputs} inputs read back): the terrain mirrors keep the {ShaderMath.Rounding.ToString().ToLowerInvariant()} hash and may stand off its terrain");
            }
        }

        /// <summary>What <see cref="Decide"/> read: the rounding to take (null for none) and how many samples each agreed with.</summary>
        internal readonly struct Verdict
        {
            internal Verdict(HashRounding? rounding, int fused, int unfused, int inputs)
            {
                Rounding = rounding;
                Fused = fused;
                Unfused = unfused;
                Inputs = inputs;
            }

            internal HashRounding? Rounding { get; }
            internal int Fused { get; }
            internal int Unfused { get; }

            /// <summary>How many samples handed back the input their pixel should have hashed.</summary>
            internal int Inputs { get; }
        }

        /// <summary>
        /// The input <c>HashProbe.fx</c> hashes at the pixel in <paramref name="column"/> and <paramref name="row"/>: a whole
        /// cell, centred on the origin, times (19, 23), plus a seed. Every step is exact or one rounding on either side.
        /// </summary>
        internal static Vector2 InputAt(int column, int row) =>
            new((column - SIZE / 2) * 19f + 23.7f, (row - SIZE / 2) * 23f + 57.1f);

        /// <summary>
        /// The rounding that agrees with every sample while the other does not, or null: the samples' inputs must be the
        /// ones their pixels should have hashed (a readback that lost them is no evidence), and a GPU that agrees with
        /// neither rounding everywhere, or with both, decides nothing. <paramref name="samples"/> are the probe's pixels in
        /// row order: the hash in x and y, the input it hashed in z and w.
        /// </summary>
        internal static Verdict Decide(Vector4[] samples)
        {
            int fused = 0, unfused = 0, inputs = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                Vector4 sample = samples[i];
                Vector2 expected = InputAt(i % SIZE, i / SIZE);
                if (sample.Z == expected.X && sample.W == expected.Y) inputs++;

                if (Agrees(sample, ShaderMath.Hash22(sample.Z, sample.W, HashRounding.Fused))) fused++;
                if (Agrees(sample, ShaderMath.Hash22(sample.Z, sample.W, HashRounding.Unfused))) unfused++;
            }

            int n = samples.Length;
            HashRounding? rounding = null;
            if (n > 0 && inputs == n)
            {
                if (fused == n && unfused < n) rounding = HashRounding.Fused;
                else if (unfused == n && fused < n) rounding = HashRounding.Unfused;
            }

            return new Verdict(rounding, fused, unfused, inputs);
        }

        private static bool Agrees(Vector4 sample, Vector2 hash) =>
            MathF.Abs(sample.X - hash.X) <= MATCH_TOLERANCE && MathF.Abs(sample.Y - hash.Y) <= MATCH_TOLERANCE;

        private static Vector4[] Draw(GraphicsDevice device, ContentManager content, VertexBuffer fullScreenQuad)
        {
            Effect effect = content.Load<Effect>("Shaders/HashProbe");
            using var target = new RenderTarget2D(device, SIZE, SIZE, false, SurfaceFormat.Vector4, DepthFormat.None);

            RenderTargetBinding[] targets = device.GetRenderTargets();
            BlendState blend = device.BlendState;
            DepthStencilState depth = device.DepthStencilState;
            RasterizerState rasterizer = device.RasterizerState;
            Viewport viewport = device.Viewport;
            try
            {
                device.SetRenderTarget(target);
                device.BlendState = BlendState.Opaque;
                device.DepthStencilState = DepthStencilState.None;
                device.RasterizerState = RasterizerState.CullNone;
                device.SetVertexBuffer(fullScreenQuad);
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
            finally
            {
                device.SetRenderTargets(targets);
                device.BlendState = blend;
                device.DepthStencilState = depth;
                device.RasterizerState = rasterizer;
                device.Viewport = viewport;
            }

            var samples = new Vector4[SIZE * SIZE];
            target.GetData(samples);
            return samples;
        }
    }
}
