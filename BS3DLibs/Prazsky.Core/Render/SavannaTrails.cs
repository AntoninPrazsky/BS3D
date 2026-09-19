using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Where the savanna's worn paths run, asked on the CPU</b> (#476) — the mirror of the trail term
    /// <c>Savanna.fx</c> paints, so the planting can decline a spot that is on a path instead of learning
    /// about it from a screenshot.
    /// <para>
    /// <b>Why it exists.</b> The trails are contour lines of one low-frequency noise computed per pixel and
    /// the plants are placed on the CPU, so neither knew about the other and a track could run straight
    /// through a trunk. <see cref="TrailWarpField"/> answered the half that matters most — the path now
    /// <i>bends</i> round what stands on the plain — but a bend has a limit: in a dense clump, or where a
    /// path threads between two trunks, the warp cannot carry it clear. This is the other half, and it is
    /// belt and braces by design: what the warp could not bend round, the planting simply does not stand on.
    /// </para>
    /// <para>
    /// ⚠ <b>The test has to be against the BENT path, not the raw noise</b>, and that is the whole
    /// subtlety: a spot the unwarped trail crosses is usually a spot the warp has already carried the trail
    /// off, and refusing it would be refusing a site that is fine. So the warp is evaluated here too,
    /// through <see cref="TrailWarpField.StepAside"/> — the same rule the texture is a sampled copy of.
    /// </para>
    /// </summary>
    public static class SavannaTrails
    {
        /// <summary>
        /// How trodden a point is: 1 in the bare middle of a path, 0 off it. <b>Term for term
        /// <c>Savanna.fx</c>'s <c>trail</c></b>, less two factors that are the shader's own and not the
        /// ground's:
        /// <list type="bullet">
        /// <item><description><c>TrailStrength</c>, which is <i>how bare</i> a path is drawn rather than
        /// where it runs — a paler trail is in the same place;</description></item>
        /// <item><description>the band-limiting fade, which is a function of how big a pixel is on the
        /// ground and means nothing away from a camera.</description></item>
        /// </list>
        /// So this is the path's own shape, on 0…1, and the caller decides what fraction of it counts as
        /// standing on it.
        /// </summary>
        /// <param name="warp">The step aside in <b>world units</b> — <see cref="TrailWarpField.StepAside"/>
        /// times the offset, which is what the shader's sampled <c>TrailWarpAmount</c> comes to.</param>
        public static float Trodden(float x, float z, Vector2 warp, float frequency, float width)
        {
            float contour = MathF.Abs(ShaderMath.Noise(new Vector2(x + warp.X, z + warp.Y) * frequency + new Vector2(33f)));

            return 1f - ShaderMath.SmoothStep(width * 0.5f, width, contour);
        }

        /// <summary>
        /// The same question asked of a savanna that is still being planted: the warp is summed from
        /// <paramref name="avoidable"/> — everything standing <i>so far</i> that a path goes round — rather
        /// than sampled from a texture that will not exist until the planting is done.
        /// <para>
        /// ⚠ <b>The obstacle's own push does not answer for it.</b> At an obstacle's own centre the summed
        /// falloff cancels by symmetry (and is skipped outright at zero distance), so a plant cannot shove
        /// the path off itself and call the site clear — which is exactly right: the question is whether
        /// the path that the <i>rest</i> of the plain has already bent still comes through here.
        /// </para>
        /// </summary>
        public static float Trodden(float x, float z, IReadOnlyList<ScatterSpacing.Footprint> avoidable,
            SavannaSceneConfig config)
        {
            if (config.TrailStrength <= 0f) return 0f;

            Vector2 warp = config.TrailAvoidOffset > 0f
                ? TrailWarpField.StepAside(avoidable, config.TrailAvoidReach, x, z) * config.TrailAvoidOffset
                : Vector2.Zero;

            return Trodden(x, z, warp, config.TrailFrequency, config.TrailWidth);
        }
    }
}
