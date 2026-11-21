using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// THE DESERT CREATURES' GAIT (#966, #1217): how far forwards of where it stands at rest a foot is at a moment of its
    /// cycle, in the one copy <see cref="DesertCritters"/> lays a beetle's prints by; <c>Critter.fx</c>'s <c>Foot</c> is the
    /// same lines (<c>CritterGaitTests</c> reads them there). A leg's phase is the creature's gait (<see cref="Phase"/>, its
    /// cycles over the ground walked) plus the leg's own (<see cref="CritterMesh.LegPhase"/>, a tripod). A cycle starts as
    /// the foot lifts off: for its first half the foot swings forwards through the air, easing in and out so it leaves the
    /// sand and lands on it at rest; for its second it stands on the sand at <see cref="TOUCHDOWN"/>'s place and sweeps back
    /// under the body exactly as far as the body walks over that half (<see cref="Stride"/>), so it stays where it touched
    /// down until it lifts again. ⚠ The stance once swept back on a sine, 0.14 body lengths against the 0.23 the body walked
    /// over it, and a planted foot skated 0.09 of a length over the sand, under a print that stays where it was laid.
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

        //How far through its cycle a foot is, 0 to 1 from its lift-off
        private static float Cycle(float phase)
        {
            float c = phase / MathHelper.TwoPi + 0.25f;
            return c - MathF.Floor(c);
        }
    }
}
