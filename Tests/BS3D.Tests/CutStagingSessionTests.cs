using BS3D.Effects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// #767: the Cut's staging as the session runs it (<see cref="CutStaging"/>), on a real cut of a real hung slab
    /// (<see cref="BallsConstraintsBuilder.CutBall"/>), stepped the way <c>GameplayScreen</c> steps it every frame: the wall
    /// clock's beats first (<see cref="CutStaging.StepWall"/>), then the world at <see cref="CutStaging.TimeScale"/> of the
    /// frame (<see cref="CutStaging.StepWorld"/>). What they hold: the slow motion and the cut faces' heat are the same at
    /// 30, 60, 75 and 144 frames a second (and a stepping that counts frames instead of seconds is seen to fail the same
    /// check), a shot ends the slow motion, there is none under the drop cinematic, only the cut's own balls glow (the last
    /// N of the falling list), the face above the cut glows and <see cref="CutStaging.Reset"/> puts every glow out, the charge's
    /// angle is held once the press is over and its top is felt once.
    /// </summary>
    public class CutStagingSessionTests
    {
        private static readonly float[] FrameRates = { 30f, 60f, 75f, 144f };

        //The reference the rates are held to: the same stepping at a rate no machine runs
        private const float REFERENCE_HZ = 2000f;

        private readonly ITestOutputHelper _output;

        public CutStagingSessionTests(ITestOutputHelper output) => _output = output;

        /// <summary>A 5x5 slab four storeys deep hung from the glass, cut through its third storey from the top: the storey
        /// (25 balls) destroyed and the bottom one (25) orphaned.</summary>
        private sealed class CutSlab : IDisposable
        {
            public HungLevel Hung { get; }
            public List<PhysicsBall> Falling { get; } = new();
            public BallsReleased Result { get; }
            public XZLevel Cell { get; }
            public Microsoft.Xna.Framework.Vector3 Struck { get; }
            public int Top { get; }

            public CutSlab(int olderFalling = 0)
            {
                var map = new BallsMap(5, 5, 8);
                Top = map.Levels - 1;
                for (int level = Top - 3; level <= Top; level++)
                    for (int x = 0; x < 5; x++)
                        for (int z = 0; z < 5; z++)
                            map.PutBallAt((byte)x, (byte)z, (byte)level, BallType.Type4);

                Hung = new HungLevel(map);
                Cell = new XZLevel(2, 2, Top - 2);
                Struck = Hung.Balls[Cell.X, Cell.Z, Cell.Level].BallReference.Pose.Position.ToXna();

                //Older entries in the list (the game's falling list holds every ball still on its way down): balls of the top
                //storey, which the cut neither frees nor faces
                for (int i = 0; i < olderFalling; i++) Falling.Add(Hung.Balls[i, 0, Top]);

                //The cutter itself goes in just before the cut's balls, as the contact handler puts it
                Falling.Add(new PhysicsBall { Kind = BallKind.Cutter });

                Result = BallsConstraintsBuilder.CutBall(Cell, Hung.Balls, Hung.Map, Hung.World.Simulation, Falling);
            }

            public void Dispose() => Hung.Dispose();
        }

        private static CutStaging Strike(CutSlab slab, bool slowMotion = true)
        {
            var staging = new CutStaging();
            staging.Strike(slab.Cell, slab.Struck, slab.Falling, slab.Result.Total, slab.Result.Orphaned, slab.Hung.Balls,
                slowMotion, out _, out _);
            return staging;
        }

        [Fact]
        public void The_slab_is_cut_as_the_tests_assume()
        {
            using CutSlab slab = new();
            Assert.Equal(25, slab.Result.Destroyed);
            Assert.Equal(25, slab.Result.Orphaned);
        }

        /// <summary>
        /// The game's frame, as <c>GameplayScreen</c> runs it after a strike, at <paramref name="hz"/>: the wall clock's beats,
        /// then the world at the slow motion's scale. <paramref name="wallStep"/> is what the staging is told a frame lasted -
        /// the frame itself, unless the test plants the fault of a stepping that counts frames. Returns the world time the
        /// slow motion cost over two seconds of wall time, and one storey ball's glow when the world reached half a second.
        /// </summary>
        private static (double Lost, float GlowAtHalf) RunFrames(float hz, Func<float, float> wallStep, Func<float, float> worldStep)
        {
            using CutSlab slab = new();
            CutStaging staging = Strike(slab);

            PhysicsBall storeyBall = null;
            for (int i = slab.Falling.Count - slab.Result.Total; i < slab.Falling.Count; i++)
                if (slab.Falling[i].ArrayPosition.Level == slab.Cell.Level) { storeyBall = slab.Falling[i]; break; }

            float dt = 1f / hz;
            double world = 0.0, wall = 0.0;
            float glowAtHalf = -1f;

            while (wall < 2.0)
            {
                staging.StepWall(wallStep(dt));
                float scale = staging.TimeScale;
                staging.StepWorld(worldStep(dt * scale));

                wall += dt;
                world += dt * scale;
                if (glowAtHalf < 0f && world >= 0.5) glowAtHalf = storeyBall.CutPreviewGlow;
            }

            return (wall - world, glowAtHalf);
        }

        /// <summary>
        /// The slow motion costs the world the same time at every frame rate, to the one frame's rounding a frame-stepped
        /// curve that moves by at most 0.85 of the world's speed up and down can have; and the planted fault - the slow
        /// motion's timeline advanced by a fixed sixtieth a frame, a stepping that counts frames instead of seconds - is
        /// seen to fail the very same check at 30 and at 144 Hz, so the check bites.
        /// </summary>
        [Fact]
        public void The_slow_motion_costs_the_same_world_time_at_any_frame_rate()
        {
            double reference = RunFrames(REFERENCE_HZ, dt => dt, dt => dt).Lost;
            _output.WriteLine($"reference ({REFERENCE_HZ} Hz): the world loses {reference:F4} s");
            Assert.True(reference > 0.3, $"the slow motion cost the world only {reference:F3} s");

            foreach (float hz in FrameRates)
            {
                double lost = RunFrames(hz, dt => dt, dt => dt).Lost;
                double tolerance = 0.85 / hz + 1e-4;
                _output.WriteLine($"{hz} Hz: loses {lost:F4} s, {Math.Abs(lost - reference):F4} off (allowed {tolerance:F4})");
                Assert.True(Math.Abs(lost - reference) <= tolerance, $"{hz} Hz: the world loses {lost:F4} s against {reference:F4}");
            }

            foreach (float hz in new[] { 30f, 144f })
            {
                double faulty = RunFrames(hz, dt => 1f / 60f, dt => dt).Lost;
                _output.WriteLine($"planted fault at {hz} Hz (a sixtieth a frame): loses {faulty:F4} s");
                Assert.True(Math.Abs(faulty - reference) > 0.85 / hz + 1e-4, $"the check passed a frame-counting stepping at {hz} Hz");
            }
        }

        /// <summary>
        /// The cut faces cool on the world's clock: a storey ball's glow when the world has run half a second is the same at
        /// every frame rate, to a frame's cooling; a heat stepped a fixed sixtieth a frame is seen to fail it.
        /// </summary>
        [Fact]
        public void The_cut_faces_cool_the_same_at_any_frame_rate()
        {
            float reference = RunFrames(REFERENCE_HZ, dt => dt, dt => dt).GlowAtHalf;
            Assert.True(reference > 0.1f, $"the storey's glow at half a second is only {reference:F3}");

            //The heat falls at most FALL_GLOW / FALL_GLOW_COOL_SECONDS a second, and the world moves at most a frame past half
            //a second before it is read
            float slope = CutStaging.FALL_GLOW / CutBeats.FALL_GLOW_COOL_SECONDS;

            foreach (float hz in FrameRates)
            {
                float glow = RunFrames(hz, dt => dt, dt => dt).GlowAtHalf;
                _output.WriteLine($"{hz} Hz: glow {glow:F4} against {reference:F4}");
                Assert.InRange(glow, reference - slope / hz - 1e-3f, reference + 1e-3f);
            }

            float faulty = RunFrames(30f, dt => dt, dt => 1f / 60f).GlowAtHalf;
            _output.WriteLine($"planted fault at 30 Hz (the heat a sixtieth a frame): glow {faulty:F4}");
            Assert.False(faulty >= reference - slope / 30f - 1e-3f && faulty <= reference + 1e-3f, "the check passed a frame-counting heat");
        }

        /// <summary>A shot fired during the slow motion ends it at once - and only it: the faces go on cooling.</summary>
        [Fact]
        public void A_shot_ends_the_slow_motion_and_nothing_else()
        {
            using CutSlab slab = new();
            CutStaging staging = Strike(slab);

            staging.StepWall(0.05f);
            Assert.True(staging.Slowed);
            Assert.True(staging.TimeScale < 1f);

            staging.Skip();

            Assert.False(staging.Slowed);
            Assert.Equal(1f, staging.TimeScale);

            staging.StepWorld(0.05f);
            Assert.True(slab.Falling[^1].CutPreviewGlow > 0f, "the skip put the cut's glow out with the slow motion");
        }

        /// <summary>Under the drop cinematic the cut adds no slow motion of its own; its glows are armed all the same.</summary>
        [Fact]
        public void There_is_no_slow_motion_under_the_cinematic()
        {
            using CutSlab slab = new();
            CutStaging staging = Strike(slab, slowMotion: false);

            for (int i = 0; i < 120; i++)
            {
                staging.StepWall(1f / 60f);
                Assert.False(staging.Slowed);
                Assert.Equal(1f, staging.TimeScale);
                staging.StepWorld(1f / 60f);
            }

            using CutSlab fresh = new();
            Strike(fresh, slowMotion: false);
            Assert.True(fresh.Falling[^1].CutPreviewGlow > 0f, "the cinematic took the cut's glow with its slow motion");
        }

        /// <summary>
        /// Only the cut's own balls glow - the last <c>Released.Total</c> of the falling list - and the storey the blade went
        /// through glows the hottest: older balls in the list (a release still on its way down) and the cutter take none.
        /// </summary>
        [Fact]
        public void Only_the_cuts_own_balls_glow_and_the_storey_the_hottest()
        {
            using CutSlab slab = new(olderFalling: 4);
            Strike(slab);

            int first = slab.Falling.Count - slab.Result.Total;
            Assert.Equal(5, first);

            for (int i = 0; i < first; i++)
                Assert.Equal(0f, slab.Falling[i].CutPreviewGlow);

            float storey = 0f, below = 0f;
            for (int i = first; i < slab.Falling.Count; i++)
            {
                PhysicsBall ball = slab.Falling[i];
                Assert.True(ball.CutPreviewGlow > 0f, $"a freed ball at ({ball.ArrayPosition.X},{ball.ArrayPosition.Z},{ball.ArrayPosition.Level}) is dark");
                if (ball.ArrayPosition.Level == slab.Cell.Level) storey = MathF.Max(storey, ball.CutPreviewGlow);
                else below = MathF.Max(below, ball.CutPreviewGlow);
            }

            _output.WriteLine($"storey {storey:F3}, the storey under it {below:F3}");
            Assert.True(storey > below, "the storey the blade went through is not the hottest");
            Assert.Equal(CutStaging.FALL_GLOW, storey, 2);
        }

        /// <summary>
        /// The other face of the cut: every ball still hanging in the storey above it glows, the storey above that does not,
        /// and <see cref="CutStaging.Reset"/> puts every glow it lit out.
        /// </summary>
        [Fact]
        public void The_face_above_the_cut_glows_and_Reset_puts_everything_out()
        {
            using CutSlab slab = new();
            CutStaging staging = Strike(slab);

            int faceLit = 0;
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 5; z++)
                {
                    PhysicsBall face = slab.Hung.Balls[x, z, slab.Cell.Level + 1];
                    PhysicsBall top = slab.Hung.Balls[x, z, slab.Top];
                    Assert.NotNull(face);
                    if (face.CutPreviewGlow > 0f) faceLit++;
                    Assert.Equal(0f, top.CutPreviewGlow);
                }

            Assert.Equal(25, faceLit);

            staging.Reset();

            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 5; z++)
                    Assert.Equal(0f, slab.Hung.Balls[x, z, slab.Cell.Level + 1].CutPreviewGlow);
            foreach (PhysicsBall ball in slab.Falling) Assert.Equal(0f, ball.CutPreviewGlow);
            Assert.False(staging.Slowed);
        }

        /// <summary>When the faces have cooled they are put out for good, not left a hair above zero.</summary>
        [Fact]
        public void The_glows_go_out_when_the_heat_runs_out()
        {
            using CutSlab slab = new();
            CutStaging staging = Strike(slab);

            for (float t = 0f; t < CutBeats.FALL_GLOW_SECONDS + 0.1f; t += 1f / 60f) staging.StepWorld(1f / 60f);

            foreach (PhysicsBall ball in slab.Falling) Assert.Equal(0f, ball.CutPreviewGlow);
            Assert.Equal(0f, slab.Hung.Balls[2, 2, slab.Cell.Level + 1].CutPreviewGlow);
        }

        /// <summary>
        /// The charge is the round's: its angle runs from the press and is held once the press is over (a Swap that moves the
        /// round about cannot make it leap), the glow comes back to rest, the top is felt exactly once at any frame rate, and
        /// firing the round hands everything back.
        /// </summary>
        [Fact]
        public void The_charge_is_felt_once_and_its_angle_is_held()
        {
            foreach (float hz in FrameRates)
            {
                var staging = new CutStaging();
                staging.Press();

                int topped = 0;
                float dt = 1f / hz;
                for (float t = 0f; t < CutBeats.PRESS_SECONDS + 0.5f; t += dt)
                {
                    staging.StepWall(dt);
                    if (staging.ToppedOut) topped++;
                }

                Assert.Equal(1, topped);
                Assert.Equal(1f, staging.Glow);
                Assert.Equal((0f, 0f, 0f, 0f), staging.PadHold);

                float held = staging.SpinOffset;
                staging.StepWall(2f);
                Assert.Equal(held, staging.SpinOffset);
                Assert.Equal(CutBeats.SpinOffset(CutBeats.PRESS_SECONDS), held, 4);

                staging.Fired();
                Assert.Equal(0f, staging.SpinOffset);
                staging.StepWall(CutBeats.PINCH_IN_SECONDS);
                Assert.True(staging.FovScale < 1f, "the flight did not pinch the lens");
            }
        }
    }
}
