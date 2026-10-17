using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #877's review: the drop cinematic ends a shot as "the drop stalled" when the subject has descended by less than a unit
    /// over 0.9 seconds of the <b>world's</b> time. The world advances at most four fixed steps (4/120 s) a frame, so a frame
    /// below 30 fps lives through less than it lasted; a stall clock that counted the frame up to 0.1 s outran the world on a
    /// slow machine (an APU, a Pi at 15 fps) and cut the shot short of the 0.9 s it promises. A check is evidence only once
    /// its failing branch has been seen to fire (BestPractices section 10): it was run with the cap at 0.1 s, where the 15 fps
    /// shot ended after 0.43 s of world time (0.27 s at 10 fps), and the cap is 4/120 s now.
    /// </summary>
    public class DropCinematicStallTests
    {
        //DropCinematic.STALL_SECONDS, the world seconds a subject must hold still for the shot to end as stalled
        private const float STALL_WORLD_SECONDS = 0.9f;

        //What the world advances in one frame at most: GameplayScreen's four physics steps of 1/120 s
        private const float WORLD_PER_FRAME_MAX = 4f / 120f;

        [Theory]
        [InlineData(15f)]
        [InlineData(10f)]
        [InlineData(60f)]
        public void A_still_subject_is_not_called_stalled_before_the_world_has_held_it_for_the_stall_seconds(float hertz)
        {
            float frame = 1f / hertz;
            float worldPerFrame = MathF.Min(frame, WORLD_PER_FRAME_MAX);

            DropCinematic cinematic = new();
            Vector3 subject = new(0f, DropCinematicFlight.RELEASE_Y, 0f);

            cinematic.Begin(SceneKind.Sea, subject, new Vector3(0f, 6f, 38f), 12, new Random(1), DropCinematicFlight.SEA_LEVEL_Y);

            float world = 0f;
            for (int i = 0; i < 4000 && cinematic.Running; i++)
            {
                cinematic.Update(frame, true, subject, 0f);
                if (cinematic.Running) world += worldPerFrame;
            }

            Assert.False(cinematic.Running, "the shot never ended");
            Assert.Equal("the drop stalled", cinematic.EndedBy);
            Assert.True(world + worldPerFrame >= STALL_WORLD_SECONDS,
                $"at {hertz} fps the shot was called stalled after {world:F2} s of world time, under {STALL_WORLD_SECONDS} s");
        }
    }
}
