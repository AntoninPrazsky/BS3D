using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// THE DESERT CREATURES' GAIT (#966, #1217): where a foot is, off where it stands at rest, at a moment of its cycle, in
    /// the one copy <see cref="DesertCritters"/> lays a beetle's prints by; <c>Critter.fx</c>'s <c>Foot</c> is the same lines
    /// (<c>CritterGaitTests</c> reads them there). A leg's phase is the creature's gait (<see cref="Phase"/>, its
    /// cycles over the ground walked) plus the leg's own (<see cref="CritterMesh.LegPhase"/>, a tripod). A cycle starts as
    /// the foot lifts off: for its first half the foot swings forwards through the air, easing in and out so it leaves the
    /// sand and lands on it at rest; for its second it stands on the sand at <see cref="TOUCHDOWN"/>'s place and sweeps back
    /// under the body exactly as far as the body walks over that half (<see cref="Stride"/>), so it stays where it touched
    /// down until it lifts again. ⚠ The stance once swept back on a sine, 0.14 body lengths against the 0.23 the body walked
    /// over it, and a planted foot skated 0.09 of a length over the sand, under a print that stays where it was laid.
    /// <para>
    /// ⚠ A BODY WALKING A CURVE TURNS OVER ITS PLANTED FEET (<see cref="Foot"/>): swept straight back along the body, a
    /// planted foot turned with it, 0.028 units off its print by its lift-off on the roaming beetles' sharpest curves, at the
    /// print's rim. So a planted foot is carried as the turning body sees a spot on the sand go by (round the curve's centre
    /// by the angle turned since it touched down), and the swing eases that carry out again before it lands.
    /// </para>
    /// </summary>
    internal static class CritterGait
    {
        /// <summary>Steps a body length, the gait's cycles over the ground walked: the beetle's, and the scorpion's, whose
        /// legs stand closer along its body and would cross on the beetle's longer stride.</summary>
        internal const float BEETLE_STEPS_PER_LENGTH = 2.2f, SCORPION_STEPS_PER_LENGTH = 3.5f;

        /// <summary>Where in its cycle a foot touches down: a leg's phase (radians), whole cycles apart. It lifts off half a
        /// cycle from it.</summary>
        internal const float TOUCHDOWN = MathHelper.PiOver2;

        internal static float StepsPerLength(bool scorpion) => scorpion ? SCORPION_STEPS_PER_LENGTH : BEETLE_STEPS_PER_LENGTH;

        /// <summary>How far a foot reaches forwards of where it stands at rest as it touches down (and back as it lifts
        /// off), in body lengths: a quarter of a cycle's ground, so the stance's sweep is the half cycle's.</summary>
        internal static float Stride(bool scorpion) => 0.25f / StepsPerLength(scorpion);

        /// <summary>The gait's phase (radians) of a creature of <paramref name="size"/> that has walked
        /// <paramref name="walked"/>.</summary>
        internal static float Phase(float walked, float size, bool scorpion) => walked / size * StepsPerLength(scorpion) * MathHelper.TwoPi;

        /// <summary>How far forwards of its place at rest a foot is at <paramref name="phase"/>, in strides: -1 as it lifts
        /// off, +1 as it touches down, and from there straight back as the body walks on.</summary>
        internal static float Forward(float phase)
        {
            float c = Cycle(phase);
            float u = Math.Clamp(c * 2f, 0f, 1f), v = Math.Clamp(c * 2f - 1f, 0f, 1f);
            return c < 0.5f ? 4f * u * u * (3f - 2f * u) - 2f * u - 1f : 1f - 2f * v;
        }

        /// <summary>How high a foot is at <paramref name="phase"/>, as a share of the shader's lift: nought while it stands
        /// on the sand.</summary>
        internal static float Lift(float phase)
        {
            float c = Cycle(phase);
            return c < 0.5f ? MathF.Sin(MathF.PI * Math.Clamp(c * 2f, 0f, 1f)) : 0f;
        }

        /// <summary>Whether the foot at <paramref name="phase"/> stands on the sand.</summary>
        internal static bool Planted(float phase) => Cycle(phase) >= 0.5f;

        /// <summary>
        /// How far a leg's vertex standing at <paramref name="rest"/> (x across the body, z along it, in body lengths) is
        /// carried off it at <paramref name="phase"/>, as <c>Critter.fx</c>'s <c>Foot</c> carries the leg's foot (its share
        /// down the leg scales it there): X across and Z along in body lengths, Y how high as a share of the shader's lift.
        /// <paramref name="stride"/> is the kind's (<see cref="Stride"/>) and <paramref name="turn"/> how many radians the body
        /// turns a body length it walks (the pose's curve times the creature's size), positive towards +X. Walking straight
        /// it is <see cref="Forward"/>'s strides along and <see cref="Lift"/>; on a curve a planted foot is carried as the
        /// body sees a spot on the sand go by: where it touched down, less the ground the body has walked since along its
        /// arc, turned back by the angle the body has turned (the arc's sine and versine to the third order, within a
        /// millionth of a length at the sharpest stance's 0.08 radians); the swing eases the carry it lifted off with back
        /// out to nothing as it lands, where the carry starts from nought.
        /// </summary>
        internal static Vector3 Foot(float phase, float stride, float turn, Vector2 rest)
        {
            float c = Cycle(phase);
            float u = Math.Clamp(c * 2f, 0f, 1f), v = Math.Clamp(c * 2f - 1f, 0f, 1f);
            float forward = c < 0.5f ? 4f * u * u * (3f - 2f * u) - 2f * u - 1f : 1f - 2f * v;
            float up = c < 0.5f ? MathF.Sin(MathF.PI * u) : 0f;
            //The ground the body has walked since the foot touched down (in the air: over its whole last stance), and the
            //angle it has turned over it
            float walked = 2f * stride * (c < 0.5f ? 1f : v);
            float a = turn * walked;
            Vector2 down = rest + new Vector2(0f, stride);
            Vector2 w = down - walked * new Vector2(a * (0.5f - a * a / 24f), 1f - a * a / 6f);
            float cos = MathF.Cos(a), sin = MathF.Sin(a);
            Vector2 carried = new Vector2(w.X * cos - w.Y * sin, w.X * sin + w.Y * cos) - (down - new Vector2(0f, walked));
            Vector2 off = new Vector2(0f, stride * forward) + carried * (c < 0.5f ? 1f - u * u * (3f - 2f * u) : 1f);
            return new Vector3(off.X, up, off.Y);
        }

        //How far through its cycle a foot is, 0 to 1 from its lift-off
        private static float Cycle(float phase)
        {
            float c = phase / MathHelper.TwoPi + 0.25f;
            return c - MathF.Floor(c);
        }
    }
}
