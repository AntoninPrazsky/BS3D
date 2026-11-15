using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The C# side of the arithmetic the shaders draw with.</b> A CPU mirror of a shader field is how
    /// several things in this project are placed on ground they cannot see — the acacias stand on
    /// <c>Savanna.fx</c>'s terrain, the palms on <c>Tropical.fx</c>'s, the light rig is dimmed by the same
    /// cloud cover the sky shades with, and <see cref="SavannaTrails"/> asks whether a spot is on a worn
    /// path before anything is planted there. Every one of those mirrors needs the same two primitives, and
    /// a second transcription of either is a place for the two sides to part ways silently.
    /// <para>
    /// <b>The whole value of a mirror is that it agrees</b>, so these are copied line for line from
    /// <c>Clouds.fxh</c> rather than written afresh: scalar rather than vectorised and componentwise rather
    /// than clever, because every line has to correspond to a line of HLSL that can be read beside it.
    /// </para>
    /// <para>
    /// <b>The one copy</b> since #590: <see cref="SceneRenderer"/> carried private <c>SmoothStep</c> and
    /// <c>Frac</c> with the same bodies from before this file existed, and they folded in here when every
    /// terrain mirror moved into <see cref="TerrainMirror"/>.
    /// </para>
    /// </summary>
    internal static class ShaderMath
    {
        /// <summary>
        /// GLSL/HLSL <c>smoothstep</c> as the shaders spell it: <b>clamp first, then hermite</b>. Not
        /// <see cref="MathHelper.SmoothStep"/>, which is a different function — the forest's mirror records
        /// the trap, and a mirror that uses the wrong one is wrong exactly where the field is steepest.
        /// </summary>
        public static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = MathHelper.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// <c>Clouds.fxh</c>'s <c>CloudNoise</c>: gradient noise on the unit grid, quintic-faded. Built out
        /// of frac/dot/multiply only — no sine — precisely so that it can come out the same here as there;
        /// a sine-based hash is exactly where the two sides would part ways.
        /// </summary>
        public static float Noise(Vector2 p)
        {
            float cellX = MathF.Floor(p.X);
            float cellY = MathF.Floor(p.Y);

            float fx = p.X - cellX;
            float fy = p.Y - cellY;

            //Quintic, matching the shader: the sky is shaded off this field's slope, so its second
            //derivative has to be continuous as well
            float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

            float a = Dot(Hash22(cellX, cellY), fx, fy);
            float b = Dot(Hash22(cellX + 1f, cellY), fx - 1f, fy);
            float c = Dot(Hash22(cellX, cellY + 1f), fx, fy - 1f);
            float d = Dot(Hash22(cellX + 1f, cellY + 1f), fx - 1f, fy - 1f);

            return MathHelper.Lerp(MathHelper.Lerp(a, b, ux), MathHelper.Lerp(c, d, ux), uy);
        }

        /// <summary>HLSL <c>frac</c>: <c>x - floor(x)</c>, in 0..1 for a negative argument too.</summary>
        internal static float Frac(float value) => value - MathF.Floor(value);

        /// <summary>
        /// <c>Noise.fxh</c>'s <c>NoiseHash22</c> (and <c>Clouds.fxh</c>'s hash, the same lines): two values in
        /// -1..1 off a point. Internal rather than private since #559, for <see cref="TerrainMirror"/>'s
        /// outback formations, which roll their placement off it directly as <c>Outback.fx</c> does.
        /// </summary>
        internal static Vector2 Hash22(float px, float py) => Hash22(px, py, Rounding);

        /// <summary>
        /// How this machine's GPU rounds the hash's dot, which every mirror built on <see cref="Hash22(float, float)"/> follows (#1134).
        /// <see cref="HashRoundingProbe"/> sets it from the GPU's own answer when the first <see cref="SceneRenderer"/> is
        /// made, before any backdrop places anything; until then, and wherever the probe cannot draw, it is
        /// <see cref="HashRounding.Fused"/>, the rounding #598 measured on the desktop.
        /// </summary>
        internal static HashRounding Rounding = HashRounding.Fused;

        /// <summary><see cref="Hash22(float, float)"/> under a stated rounding: the probe compares both.</summary>
        internal static Vector2 Hash22(float px, float py, HashRounding rounding)
        {
            float x = Frac(px * 0.1031f);
            float y = Frac(py * 0.1030f);
            float z = Frac(px * 0.0973f);

            //The HLSL's dot(p3, p3.yzx + 33.33) is two multiply-adds after fxc (#598), and whether a multiply-add rounds
            //once or twice is the DRIVER's choice: the desktop's RDNA2 fuses, the laptop's GCN does not (#1134). The
            //product lands near 20 000, where a float carries a fraction to about 1/500, so one rounding more or less
            //moves the hash by whole quanta: a mirror rounded otherwise than its GPU stood up to 16 units off the drawn
            //mountains (the Testbed's mirrorcheck). Both orders are the GPU's mads, innermost first.
            float d = rounding == HashRounding.Fused
                ? MathF.FusedMultiplyAdd(z, x + 33.33f, MathF.FusedMultiplyAdd(y, z + 33.33f, x * (y + 33.33f)))
                : z * (x + 33.33f) + (y * (z + 33.33f) + x * (y + 33.33f));

            x += d;
            y += d;
            z += d;

            return new Vector2(Frac((x + y) * z) * 2f - 1f, Frac((x + z) * y) * 2f - 1f);
        }

        private static float Dot(Vector2 gradient, float x, float y) => gradient.X * x + gradient.Y * y;
    }

    /// <summary>How a GPU rounds the noise hash's multiply-adds (#1134): see <see cref="ShaderMath.Rounding"/>.</summary>
    internal enum HashRounding
    {
        /// <summary>Once per multiply-add (the desktop's RDNA2).</summary>
        Fused,

        /// <summary>After every product and every sum (the laptop's GCN).</summary>
        Unfused
    }
}
