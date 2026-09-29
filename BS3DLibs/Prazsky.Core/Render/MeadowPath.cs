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

        /// <summary>Where the brook starts, as a share of the clearing's radius.</summary>
        public const float BROOK_START = 0.82f;

        /// <summary>How far (x, z) stands to the side of the brook's centreline — <c>Meadow.fx</c>'s <c>BrookLateral</c>.</summary>
        public static float BrookLateral(float x, float z, MeadowSceneConfig config)
        {
            float d = MathF.Sqrt(x * x + z * z);
            if (d < config.ClearingRadius * BROOK_START) return float.MaxValue;

            float angle = MathF.Atan2(z, x) - config.BrookBearing;
            angle = MathF.IEEERemainder(angle, MathF.PI * 2f);
            if (MathF.Abs(angle) > MathF.PI * 0.5f) return float.MaxValue;

            return d * angle - BrookWander(d, config);
        }

        /// <summary>The brook's centreline wander at distance <paramref name="d"/>.</summary>
        public static float BrookWander(float d, MeadowSceneConfig config) =>
            config.BrookMeander * (0.6f * MathF.Sin(d * 0.017f + 2.4f) + 0.4f * MathF.Sin(d * 0.049f + 0.3f));

        /// <summary>The point <paramref name="side"/> units to the side of the brook's centreline, <paramref name="d"/> from the arena.</summary>
        public static (float X, float Z) BrookPoint(float d, float side, MeadowSceneConfig config)
        {
            float angle = config.BrookBearing + (BrookWander(d, config) + side) / d;
            return (MathF.Cos(angle) * d, MathF.Sin(angle) * d);
        }

        /// <summary>
        /// Where the pond the brook runs into lies (#609's third round): on the brook's centreline where its inner end
        /// used to stop short in the grass, so the end is under the pond's water. <c>Meadow.fx</c> takes it as
        /// <c>PondCentre</c>.
        /// </summary>
        public static (float X, float Z) PondCentre(MeadowSceneConfig config) =>
            BrookPoint(config.ClearingRadius * BROOK_START, 0f, config);

        /// <summary>
        /// How far (x, z) stands outside the pond's shore, in world units, negative on the water — <c>Meadow.fx</c>'s
        /// <c>PondShore</c>, less the ragged edge's noise (a third of a unit at most).
        /// </summary>
        public static float PondShore(float x, float z, MeadowSceneConfig config)
        {
            (float cx, float cz) = PondCentre(config);
            float qx = x - cx, qz = z - cz;
            return MathF.Sqrt(qx * qx + qz * qz) - config.PondRadius * PondOutline(MathF.Atan2(qz, qx));
        }

        /// <summary>The pond's shore as a multiple of its mean radius at bearing <paramref name="a"/> round its centre:
        /// three harmonics, so it is a soft irregular round and never a circle.</summary>
        public static float PondOutline(float a) =>
            1f + 0.16f * MathF.Sin(2f * a + 0.7f) + 0.09f * MathF.Sin(3f * a + 2.3f) + 0.05f * MathF.Sin(5f * a + 4.1f);

        /// <summary>
        /// Where the footpath arrives (#609's third round): its centreline at its inner end, on the knoll's top, where
        /// the old tree stands. The owner: "a path leads somewhere, to a tree or a small hill". <c>Meadow.fx</c> takes
        /// it as <c>KnollCentre</c>.
        /// </summary>
        public static (float X, float Z) PathEnd(MeadowSceneConfig config)
        {
            float d = config.ClearingRadius * START;
            float angle = config.PathBearing + Wander(d, config) / d;
            return (MathF.Cos(angle) * d, MathF.Sin(angle) * d);
        }
    }
}
