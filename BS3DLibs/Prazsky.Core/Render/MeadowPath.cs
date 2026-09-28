using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The meadow's footpath</b> (#609, the owner's "a little path"): one winding line out of the clearing's edge
    /// over the hills, in the same arithmetic <c>Meadow.fx</c> draws it with (<c>PathLateral</c> there), so what is
    /// planted stays off it and the first fence can run beside it. The CPU copy of a shader field, like
    /// <see cref="TerrainMirror"/> — if one changes, the other has to.
    /// </summary>
    public static class MeadowPath
    {
        /// <summary>Where the path starts, as a share of the clearing's radius — its edge, just short of the hills.</summary>
        public const float START = 0.72f;

        /// <summary>
        /// How far (x, z) stands to the side of the path's centreline, in world units: negative on one side, positive on
        /// the other, <see cref="float.MaxValue"/> off its length (before its start, or the far side of the arena).
        /// </summary>
        public static float Lateral(float x, float z, MeadowSceneConfig config)
        {
            float d = MathF.Sqrt(x * x + z * z);
            if (d < config.ClearingRadius * START) return float.MaxValue;

            float angle = MathF.Atan2(z, x) - config.PathBearing;
            angle = MathF.IEEERemainder(angle, MathF.PI * 2f);
            if (MathF.Abs(angle) > MathF.PI * 0.5f) return float.MaxValue;

            return d * angle - Wander(d, config);
        }

        /// <summary>The centreline's own sideways wander at distance <paramref name="d"/> from the arena.</summary>
        public static float Wander(float d, MeadowSceneConfig config) =>
            config.PathMeander * (0.7f * MathF.Sin(d * 0.021f + 0.6f) + 0.3f * MathF.Sin(d * 0.057f + 2.1f));
    }
}
