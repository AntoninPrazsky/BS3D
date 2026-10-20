using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Resting a thing on a slope</b> (#938). The savanna planted its flat-bottomed dressing at the height of the ground
    /// under its centre and then tipped it by a random lean: on a slope the downhill side hung in the air and the lean lifted the
    /// edge of the flat underside clear of the earth — the owner's "unnatural positions and rotations" in the chapter intro.
    /// Measured on the shipped plain (<c>sceneseed</c> 0 to 4): 36 of 60 fallen trees had an end more than 0.3 units above the
    /// ground and 44 an end more than 0.3 under it, up to 1.9 either way on a log a metre thick, and the kopjes' lesser rocks
    /// showed their flat orange undersides over the grass.
    /// <para>
    /// Both are answered here as the plain's trees were by <c>SavannaScatter.LowestUnder</c> (#658): from the ground itself.
    /// <see cref="RestHeight"/> puts a solid with a flat underside where its whole underside rim is under the ground, whatever
    /// the slope and the lean it was given; <see cref="LayLog"/> lays a stick along the ground, pitched to the slope under it
    /// and sunk, instead of level at the height of its middle. Pure functions of a ground height, so a test holds them
    /// without a device.
    /// </para>
    /// </summary>
    public static class GroundSettling
    {
        /// <summary>How many points round an underside's rim are asked of the ground.</summary>
        public const int RIM_SAMPLES = 16;

        /// <summary>How many points along a log are asked of the ground.</summary>
        public const int LOG_SAMPLES = 9;

        /// <summary>
        /// The height to stand a solid with a flat underside at, so that its whole underside rim is <paramref name="burial"/>
        /// under the ground. The rim is a circle of <paramref name="rimRadius"/> in the solid's own frame (at the scale 1 of
        /// <paramref name="orientation"/>), carried to the world by <paramref name="orientation"/> — its scale, lean and
        /// yaw, translation left out — so a tipped solid is asked about where its tipped rim really lies. Its pivot is the
        /// centre of the underside.
        /// </summary>
        public static float RestHeight(Func<float, float, float> ground, float x, float z, float rimRadius, Matrix orientation, float burial)
        {
            float rest = ground(x, z);
            for (int k = 0; k < RIM_SAMPLES; k++)
            {
                float around = MathHelper.TwoPi * k / RIM_SAMPLES;
                Vector3 rim = Vector3.TransformNormal(new Vector3(MathF.Cos(around) * rimRadius, 0f, MathF.Sin(around) * rimRadius), orientation);
                rest = MathF.Min(rest, ground(x + rim.X, z + rim.Z) - rim.Y);
            }
            return rest - burial;
        }

        /// <summary>
        /// The height to stand a solid with a flat underside at when it perches on something rather than sinking into the ground: the
        /// highest point of the support under its tipped rim, less <paramref name="embed"/>, so it sits on the top of what carries it
        /// and its edge overhangs where the support falls away. <paramref name="support"/> is the height of what it stands on at a spot,
        /// or <see cref="float.NegativeInfinity"/> where there is none (the rim carried past the edge of a rock). <see cref="float.NaN"/>
        /// when no point of the rim and its centre is supported: the caller rests it on the ground instead.
        /// </summary>
        public static float PerchHeight(Func<float, float, float> support, float x, float z, float rimRadius, Matrix orientation, float embed)
        {
            float perch = support(x, z);
            for (int k = 0; k < RIM_SAMPLES; k++)
            {
                float around = MathHelper.TwoPi * k / RIM_SAMPLES;
                Vector3 rim = Vector3.TransformNormal(new Vector3(MathF.Cos(around) * rimRadius, 0f, MathF.Sin(around) * rimRadius), orientation);
                perch = MathF.Max(perch, support(x + rim.X, z + rim.Z) - rim.Y);
            }
            return float.IsNegativeInfinity(perch) ? float.NaN : perch - embed;
        }

        /// <summary>
        /// Lays a stick that lies along its own +X (a fallen log) on the ground: the pitch to turn it about its Z by (positive
        /// raises +X; applied before the yaw) and the height of its pivot. <paramref name="yaw"/> is the
        /// <c>Matrix.CreateRotationY</c> angle it is turned by afterwards, which sends +X to <c>(cos yaw, 0, -sin yaw)</c>.
        /// The pitch is the ground's own slope along that line (its height a half length either side of the pivot); the
        /// pivot is where the underside — <paramref name="underside"/> under the pivot's line, a negative number — touches
        /// the lowest point of the run and is <paramref name="sink"/> into the ground there, and everywhere else the ground
        /// rises round it, so no part of the log hangs in the air.
        /// </summary>
        /// <param name="length">End to end, at the log's scale.</param>
        /// <param name="underside">Where the underside lies against the pivot's height, in the log's own frame, at its scale.</param>
        /// <param name="sink">How far the underside is pressed into the lowest point, in world units.</param>
        public static (float Pitch, float Y) LayLog(Func<float, float, float> ground, float x, float z, float yaw, float length, float underside, float sink)
        {
            float dx = MathF.Cos(yaw), dz = -MathF.Sin(yaw);
            float pitch = 0f;
            for (int pass = 0; pass < 3; pass++)
            {
                float half = length * 0.5f * MathF.Cos(pitch);
                float rise = ground(x + dx * half, z + dz * half) - ground(x - dx * half, z - dz * half);
                pitch = MathF.Atan2(rise, 2f * half);
            }

            //The underside against the ground along the run: the pivot goes where the underside meets the ground first
            float rest = float.MaxValue;
            for (int k = 0; k < LOG_SAMPLES; k++)
            {
                float t = k / (LOG_SAMPLES - 1f) - 0.5f;
                float along = length * t * MathF.Cos(pitch);
                float line = length * t * MathF.Sin(pitch) + underside;
                rest = MathF.Min(rest, ground(x + dx * along, z + dz * along) - line);
            }
            return (pitch, rest - sink);
        }
    }
}
