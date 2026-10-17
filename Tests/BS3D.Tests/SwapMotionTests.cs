using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.Core.Tools;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #871: the Swap's picture is two rounds sliding through each other along the bore (<see cref="SwapMotion"/>). The magazine
    /// has exchanged the slots before it starts, so it is a picture and not a rule; what has to hold of it is geometry and time:
    /// each round starts where the other stood and ends where it belongs, stays inside the bore, is cut by the dither as exact
    /// complements where the two overlap, and runs on the frame's own seconds so it is the same at any refresh rate.
    /// </summary>
    public class SwapMotionTests
    {
        private static readonly Vector3 Muzzle = new(0f, 0f, 0f);
        private static readonly Vector3 Behind = new(0f, 0f, -Magazine.SPACING);
        private static readonly Vector3 Side = Vector3.UnitX;

        private static void Place(int slot, float progress, out Vector3 position, out Vector3 velocity, out float dissolve) =>
            SwapMotion.Round(slot, progress, Muzzle, Behind, Side, out position, out velocity, out dissolve);

        [Fact]
        public void Each_round_starts_where_the_other_stood_and_ends_in_its_own_slot_at_rest()
        {
            Place(0, 0f, out Vector3 muzzleStart, out Vector3 muzzleVelocity, out float muzzleDissolve);
            Place(1, 0f, out Vector3 behindStart, out Vector3 behindVelocity, out float behindDissolve);
            Assert.Equal(Behind, muzzleStart);
            Assert.Equal(Muzzle, behindStart);

            Place(0, 1f, out Vector3 muzzleEnd, out Vector3 muzzleEndVelocity, out float muzzleEndDissolve);
            Place(1, 1f, out Vector3 behindEnd, out Vector3 behindEndVelocity, out float behindEndDissolve);
            Assert.True(Vector3.Distance(muzzleEnd, Muzzle) < 1e-4f);
            Assert.True(Vector3.Distance(behindEnd, Behind) < 1e-4f);

            //At rest and whole at both ends: nothing pops in or out of the picture
            foreach (Vector3 v in new[] { muzzleVelocity, behindVelocity, muzzleEndVelocity, behindEndVelocity })
                Assert.True(v.Length() < 1e-3f, $"a round is moving at an end of the swap: {v.Length():F4}");
            foreach (float d in new[] { muzzleDissolve, behindDissolve, muzzleEndDissolve, behindEndDissolve })
                Assert.Equal(0f, d);
        }

        [Fact]
        public void The_velocity_is_the_derivative_of_the_position()
        {
            for (int slot = 0; slot < 2; slot++)
                for (float p = 0.05f; p < 0.96f; p += 0.1f)
                {
                    const float h = 1e-3f;
                    Place(slot, p - h, out Vector3 before, out _, out _);
                    Place(slot, p + h, out Vector3 after, out _, out _);
                    Place(slot, p, out _, out Vector3 velocity, out _);

                    Vector3 numeric = (after - before) / (2f * h * SwapMotion.SECONDS);
                    Assert.True(Vector3.Distance(numeric, velocity) < 0.02f * MathF.Max(1f, velocity.Length()),
                        $"slot {slot} at {p:F2}: numeric {numeric} against stated {velocity}");
                }
        }

        /// <summary>
        /// The bore is 0.6 round a ball of radius 0.5: a round that bows further than the difference pushes through the barrel's
        /// wall, and the first build did (its dithered surface showed on the outside of the tube).
        /// </summary>
        [Fact]
        public void A_round_never_leaves_the_bore()
        {
            float room = CannonRig.BORE_RADIUS - Constants.HALF;
            Assert.True(SwapMotion.ARC * Magazine.SPACING <= room + 1e-6f, "the arc is more than the bore leaves");

            for (int slot = 0; slot < 2; slot++)
                for (float p = 0f; p <= 1f; p += 0.01f)
                {
                    Place(slot, p, out Vector3 position, out _, out _);
                    Assert.True(MathF.Abs(position.X) <= room + 1e-5f, $"slot {slot} at {p:F2} is {position.X:F3} off the axis");
                }
        }

        /// <summary>
        /// The two rounds pass through one another, so where they overlap they must not both be written: at the crossing the muzzle's
        /// round keeps one half of the noise and the other round keeps the other half, exact complements of the dither.
        /// </summary>
        [Fact]
        public void The_two_are_exact_complements_at_the_crossing_and_whole_at_the_ends()
        {
            Place(0, 0.5f, out _, out _, out float going);
            Place(1, 0.5f, out _, out _, out float arriving);

            //Positive goes (keeps the noise above the value), negative arrives (keeps the noise below its magnitude)
            Assert.True(going > 0f && arriving < 0f);
            float keptByMuzzle = 1f - going;
            float keptByOther = -arriving;
            Assert.InRange(keptByMuzzle + keptByOther, 0.999f, 1.001f);
            Assert.Equal(SwapMotion.MAX_FADE, going, 3);

            //Eased in and out: a half-dissolved ball on the first frame would pop
            Place(0, 0.02f, out _, out _, out float early);
            Assert.InRange(early, 0f, SwapMotion.MAX_FADE * 0.1f);
            for (float p = 0f; p <= 1f; p += 0.01f)
                Assert.InRange(SwapMotion.Fade(p), 0f, SwapMotion.MAX_FADE + 1e-6f);
        }

        [Fact]
        public void The_age_is_the_frames_own_seconds_so_the_swap_is_the_same_at_any_refresh_rate()
        {
            foreach (int hz in new[] { 30, 60, 75, 144 })
            {
                SwapMotion motion = new();
                motion.Start();

                float distance = 0f;
                int frames = 0;
                float step = 1f / hz;

                while (motion.Active)
                {
                    //The path covered over the frame, integrated from the closed-form rate on the age at the frame's start
                    distance += SwapMotion.Rate(motion.Progress) * step * Magazine.SPACING;
                    motion.Step(step);
                    frames++;
                    Assert.True(frames < 1000);
                }

                //It ends on the first frame past its length, whatever the rate, and no later (a float sum may land a frame either side
                //of an exact multiple)
                Assert.InRange(frames, (int)MathF.Floor(SwapMotion.SECONDS * hz), (int)MathF.Ceiling(SwapMotion.SECONDS * hz) + 1);

                //And a round has crossed its slot, within the rate's own rectangle-rule error
                Assert.InRange(distance, Magazine.SPACING * (1f - 6f / hz), Magazine.SPACING * (1f + 6f / hz));
            }
        }

        [Fact]
        public void A_shot_cancels_it_a_pause_holds_it_and_a_capture_pins_it()
        {
            SwapMotion motion = new();
            motion.Step(1f);
            Assert.False(motion.Active);

            motion.Start();
            motion.Step(0.1f);
            Assert.True(motion.Active);
            Assert.Equal(0.1f, motion.Age, 4);

            //A pause steps nothing: the same age is the same picture
            float held = motion.Progress;
            Assert.Equal(held, motion.Progress);

            motion.Pin(0.2f);
            Assert.Equal(0.2f, motion.Age, 4);
            motion.Pin(5f);
            Assert.True(motion.Active, "pinning past the end must keep the picture alive for its frame");

            motion.Cancel();
            Assert.False(motion.Active);
            Assert.Equal(0f, motion.Progress);

            //A second press restarts it from the beginning
            motion.Start();
            motion.Step(0.2f);
            motion.Start();
            Assert.Equal(0f, motion.Age);
        }

        [Fact]
        public void It_is_the_length_of_the_magazines_own_swap_dissolve()
        {
            Assert.Equal(Magazine.SWAP_FADE_SECONDS, SwapMotion.SECONDS);
        }
    }
}
