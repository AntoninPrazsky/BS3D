using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The Swap's picture</b> (#871): the two rounds at the muzzle slide through each other along the bore, each smeared along
    /// its path, with the dither at its heaviest where they interpenetrate. <see cref="SwapMotion"/> says where each round is at
    /// an age; this file steps it, places the two rounds with it and gives each a streak. The magazine has exchanged the slots
    /// already (<see cref="Magazine.SwapSlots"/>), so none of this is a rule and the next shot is never held back by it.
    /// <para>
    /// <b>Where it reaches and where it does not.</b> The two rounds in the barrel, the muzzle's and the one behind it, in the
    /// overview and in precise aim alike (<c>CollectMagazineBalls</c> places them, so both lenses and the front end see one
    /// picture). The 2D strip in the corner cannot be reached by it and keeps its discs' short cross-fade, and the dither of a
    /// re-coloured round (a colour that died, #176) is unchanged. On the Potato path the rounds slide and cross-fade the same,
    /// the dither being the model shader's own there; it has no motion blur, so the smear is the streak alone.
    /// </para>
    /// </summary>
    internal sealed partial class GameplayScreen
    {
        #region The Swap's picture

        /// <summary>How much of its velocity a round's streak trails behind it, in seconds: the length is the speed over this.</summary>
        private const float SWAP_STREAK_TRAIL_SECONDS = 0.14f;

        /// <summary>The streak's half-width at the round, in world units: a round's radius is half of one, so most of the streak's
        /// head is hidden behind the round itself and what shows is the tail.</summary>
        private const float SWAP_STREAK_HALF_WIDTH = 0.42f;

        /// <summary>How much of the streak shows at the picture's fastest, 0 to 1: it is a smear, not a beam.</summary>
        private const float SWAP_STREAK_ALPHA = 0.85f;

        /// <summary>
        /// Ages the Swap's picture by the frame's own seconds (the clock the magazine's dissolve runs on), and under
        /// <c>swapshot=</c> holds its age at the next stated moment for the one frame that is saved there.
        /// </summary>
        private void StepSwapMotion(float elapsed)
        {
            if (!_swapMotion.Active) return;

            if (_test.SwapShotSeconds != null && _swapShotNext < _test.SwapShotSeconds.Count && _swapMotion.Age + elapsed >= _test.SwapShotSeconds[_swapShotNext])
            {
                float at = _test.SwapShotSeconds[_swapShotNext++];
                _swapMotion.Pin(at);
                Game.RequestScreenshot();
                Console.WriteLine(FormattableString.Invariant($"[swapshot] +{_swapMotion.Age:F3} s of {SwapMotion.SECONDS:F2}"));
                return;
            }

            _swapMotion.Step(elapsed);
        }

        /// <summary>
        /// Places the round now held by <paramref name="slot"/> (0 or 1) where the Swap's picture has it: its world matrix's
        /// translation written over the standing one (the barrel's own basis stays), the new position, its velocity and the dither.
        /// </summary>
        private void PlaceSwapRound(in BorePose pose, int slot, ref Matrix world, out Vector3 position, out Vector3 velocity,
            out float dissolve)
        {
            SwapMotion.Round(slot, _swapMotion.Progress, pose.SlotPosition(0), pose.SlotPosition(1), world.Right,
                out position, out velocity, out dissolve);

            world.M41 = position.X;
            world.M42 = position.Y;
            world.M43 = position.Z;
        }

        /// <summary>
        /// Where a round moving at <paramref name="velocity"/> stood when the motion blur's shutter opened, stretched by
        /// <see cref="SwapMotion.SMEAR_GAIN"/> so the blur is seen: the world matrix with its translation carried back along the
        /// path. The caller hands it to <see cref="RoundShutterWorld"/>, which carries it back by the barrel's own motion too.
        /// </summary>
        private static Matrix SmearedWorld(in Matrix world, in Vector3 velocity)
        {
            Matrix then = world;
            float back = MotionBlur.SHUTTER_SECONDS * SwapMotion.SMEAR_GAIN;

            then.M41 -= velocity.X * back;
            then.M42 -= velocity.Y * back;
            then.M43 -= velocity.Z * back;
            return then;
        }

        /// <summary>
        /// A streak behind each of the two rounds for this frame, a comet's tail along its own velocity in its own colour: the
        /// smear that shows on every path, the motion blur being the Game's alone and the play camera looking along the bore, which
        /// foreshortens a slide to nothing. Added from the draw, drawn with the launch smears and gone once drawn.
        /// </summary>
        private void AddSwapStreaks()
        {
            if (!_swapMotion.Active) return;

            BorePose pose = _magazine.Pose(_cannon, Game.CannonRig.PivotToFrontBall);
            float peakSpeed = 1.5f * Magazine.SPACING / SwapMotion.SECONDS;

            for (int slot = 0; slot < 2; slot++)
            {
                Matrix world = pose.SlotWorld(slot, out _);
                PlaceSwapRound(pose, slot, ref world, out Vector3 position, out Vector3 velocity, out _);

                float speed = velocity.Length();
                float alpha = SWAP_STREAK_ALPHA * MathF.Min(1f, speed / peakSpeed);
                if (alpha < 0.02f) continue;

                Vector3 direction = velocity / speed;
                _smears.AddStreak(position - direction * (speed * SWAP_STREAK_TRAIL_SECONDS), position, LoadedTint(slot), alpha,
                    SWAP_STREAK_HALF_WIDTH);
            }
        }

        #endregion
    }
}
