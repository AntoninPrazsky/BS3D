using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Globalization;

namespace Testbed.Diagnostics
{
    /// <summary>
    /// <b><c>mirrorcheck</c> (#590): the terrain shaders' height against their CPU mirrors, read off the GPU.</b>
    /// Everything standing on a terrain — the scatters' trees and rocks, the volcano's lamps, the savanna's
    /// fires, the chapter intro's lenses — is placed by <see cref="TerrainMirror"/>, a hand-kept C# copy of the
    /// field each shader displaces its grid by. The copies said of themselves "a drift here plants trees
    /// underground or floating, and there is nothing to catch it but the eye"; this is the something else.
    /// <para>
    /// <b>How it reads.</b> The scene's own effect has a <c>HeightProbe</c> technique (<c>HeightProbe.fxh</c>)
    /// that evaluates the shader's height function — the one its vertex shader calls, not a copy of it — at the
    /// world XZ a quad carries in its texture coordinate. One quad over a <see cref="SAMPLES_PER_SIDE"/>-square
    /// <see cref="SurfaceFormat.Vector4"/> target puts a sample at every pixel centre across
    /// ±<see cref="HALF_EXTENT"/>, and each texel comes back as (x, height, z, drawn height): the GPU's own XZ
    /// travels with its answer, so the mirror is evaluated at exactly the point the shader was, and no camera,
    /// grid tessellation or interpolation between vertices stands between the two numbers compared.
    /// </para>
    /// <para>
    /// <b>What it does not check</b>: that the grid the scene draws follows the field between its vertices (a
    /// triangle is flat, the field is not — that is tessellation, the same on both sides of the mirror), and
    /// anything but the height. The volcano's mirror leaves its scoria out by design; there the verdict is on
    /// the massing the mirror claims to copy, and a second line says how far the drawn ground stands from it.
    /// </para>
    /// <para>
    /// Only the Testbed draws it, once, and exits: no frame of any executable selects the technique, so the
    /// shipping frame pays nothing for it.
    /// </para>
    /// </summary>
    internal static class TerrainMirrorCheck
    {
        /// <summary>The probe target's side, in samples: 65 536 points a scene.</summary>
        public const int SAMPLES_PER_SIDE = 256;

        /// <summary>
        /// Half the side of the square sampled, in world units round the arena: past every tree the scatters
        /// plant (the savanna's reach to 340), every lamp on the volcano's flank and every intro lens, at four
        /// units between samples.
        /// </summary>
        public const float HALF_EXTENT = 512f;

        /// <summary>
        /// The most the mirror may differ from the shader anywhere, in world units, and still pass. A tree is
        /// seated a fifth of a metre or so into its ground, so the eye's own tolerance is tenths. The mirrors built
        /// of sines and hermite ramps (savanna, beach, meadow, forest, aurora, volcano) agree with their shaders
        /// to under a thousandth — the GPU's <c>sin</c> is not <see cref="MathF.Sin"/>, and that is all that
        /// separates them — so this sits an order of magnitude above that and an order below anything the eye
        /// could see; a deliberately broken term fails it by two orders.
        /// <para>
        /// <b>The four built on the gradient noise do not pass it, and that is a finding rather than a tolerance
        /// to widen</b> (desert, mountains, outback, polar ice — <c>docs/scenes.md</c>, "The terrain mirrors"):
        /// the noise's hash is ill-conditioned in float, so the rounding the compiler's fused multiply-adds and
        /// folded constants leave on the GPU moves it by whole quanta, and the mirror stands up to sixteen units
        /// off the drawn range in the mountains.
        /// </para>
        /// </summary>
        public const float TOLERANCE = 0.01f;

        /// <summary>
        /// Runs the check on <paramref name="scene"/> and prints its verdict. Returns the exit code: 0 pass,
        /// 1 fail, 2 a scene with no terrain mirror. Leaves no render target bound and the effect's technique as
        /// it found it.
        /// </summary>
        public static int Run(GraphicsDevice device, SceneRenderer scenes, SceneKind scene)
        {
            string name = scene.ToString().ToLowerInvariant();
            if (!scenes.TryGetTerrainProbe(scene, out Effect effect, out Func<float, float, float> mirror))
            {
                Console.WriteLine($"[mirrorcheck] {name}: no CPU terrain mirror in this scene; nothing to check");
                return 2;
            }

            Vector4[] samples = Probe(device, effect);

            double sum = 0, sumDrawn = 0;
            float max = 0f, maxDrawn = 0f;
            Vector2 worst = Vector2.Zero, worstDrawn = Vector2.Zero;
            int over = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                Vector4 s = samples[i];
                float cpu = mirror(s.X, s.Z);

                float dh = MathF.Abs(cpu - s.Y);
                if (!(dh <= max)) //a NaN on either side lands here and fails the check below
                {
                    max = float.IsNaN(dh) ? float.PositiveInfinity : dh;
                    worst = new Vector2(s.X, s.Z);
                }
                if (!(dh <= TOLERANCE)) over++;
                sum += dh;

                float dhDrawn = MathF.Abs(cpu - s.W);
                if (dhDrawn > maxDrawn)
                {
                    maxDrawn = dhDrawn;
                    worstDrawn = new Vector2(s.X, s.Z);
                }
                sumDrawn += dhDrawn;
            }

            bool pass = max <= TOLERANCE;
            CultureInfo inv = CultureInfo.InvariantCulture;
            Console.WriteLine(string.Format(inv,
                "[mirrorcheck] {0} n={1} max|dh|={2:0.######} mean|dh|={3:0.#######} worst at ({4:0.#},{5:0.#}) over tolerance {6} of {1} tol={7} {8}",
                name, samples.Length, max, sum / samples.Length, worst.X, worst.Y, over, TOLERANCE, pass ? "PASS" : "FAIL"));

            //Only where the shader draws a term its mirror leaves out (the volcano's scoria): how far off the
            //drawn ground the mirror's answer stands, which is what a caller's clearance has to cover.
            if (maxDrawn > max)
            {
                Console.WriteLine(string.Format(inv,
                    "[mirrorcheck] {0} drawn ground (terms the mirror leaves out): max|dh|={1:0.####} mean|dh|={2:0.####} worst at ({3:0.#},{4:0.#})",
                    name, maxDrawn, sumDrawn / samples.Length, worstDrawn.X, worstDrawn.Y));
            }

            return pass ? 0 : 1;
        }

        private static Vector4[] Probe(GraphicsDevice device, Effect effect)
        {
            //Corners in clip space, carrying their world XZ: the rasteriser interpolates it to every pixel
            //centre, and the probe hands it back with the height, so which way up the target is never matters.
            float h = HALF_EXTENT;
            VertexPositionTexture[] quad =
            {
                new(new Vector3(-1f, 1f, 0f), new Vector2(-h, -h)),
                new(new Vector3(1f, 1f, 0f), new Vector2(h, -h)),
                new(new Vector3(-1f, -1f, 0f), new Vector2(-h, h)),
                new(new Vector3(1f, -1f, 0f), new Vector2(h, h)),
            };

            using RenderTarget2D target = new(device, SAMPLES_PER_SIDE, SAMPLES_PER_SIDE, false,
                SurfaceFormat.Vector4, DepthFormat.None);

            EffectTechnique previous = effect.CurrentTechnique;
            device.SetRenderTarget(target);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = effect.Techniques["HeightProbe"];
            effect.CurrentTechnique.Passes[0].Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);

            device.SetRenderTarget(null);
            effect.CurrentTechnique = previous;

            Vector4[] samples = new Vector4[SAMPLES_PER_SIDE * SAMPLES_PER_SIDE];
            target.GetData(samples);
            return samples;
        }
    }
}
