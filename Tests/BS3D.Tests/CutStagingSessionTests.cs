using BS3D.Effects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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
    /// angle is held once the press is over and its top is felt once. Since #767's verdict also the slash's place (level
    /// through the struck ball, square to the shot, across the storey the cut destroyed) and the severing's picture: it
    /// plays out the same at any frame rate, its sparks read for a second, its flash never outlasts the slow motion, a shot
    /// hurries it, and both Blast.fx files carry its figures.
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

        //The way the tests' cutter flies: up a little, and off the lattice's rows, so a slash square to the lens or to a row
        //would not pass for one square to the shot
        private static readonly Microsoft.Xna.Framework.Vector3 Shot = Microsoft.Xna.Framework.Vector3.Normalize(new(0.45f, 0.3f, -1f));

        private static CutStaging Strike(CutSlab slab, bool slowMotion = true) => Strike(slab, Shot, out _, slowMotion);

        private static CutStaging Strike(CutSlab slab, Microsoft.Xna.Framework.Vector3 shot, out CutSlash slash, bool slowMotion = true)
        {
            var staging = new CutStaging();
            staging.Strike(slab.Cell, slab.Struck, shot, Microsoft.Xna.Framework.Vector3.Zero, slab.Falling, slab.Result.Total,
                slab.Result.Orphaned, slab.Hung.Balls, slowMotion, out slash, out _);
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

        /// <summary>
        /// #767's verdict: "the light stripe - the cut - is not oriented perpendicular to the shot, nor the way the storey is
        /// really cut". The slash lies in the struck storey's plane (level, through the struck ball's centre, at the height of
        /// every ball the cut destroyed) and square to the shot's level heading, whatever the heading; it spans every destroyed
        /// ball of the storey plus a ball past each end; and a struck storey of one ball still gets the shortest slash, centred
        /// on the struck ball.
        /// </summary>
        [Fact]
        public void The_slash_lies_in_the_storey_square_to_the_shot_across_what_was_cut()
        {
            using CutSlab slab = new();

            foreach (Microsoft.Xna.Framework.Vector3 shot in new[] { Shot, new(0f, 0.2f, -1f), new(-1f, 0.6f, -0.3f), new(0.7f, 0.1f, 0.7f) })
            {
                Strike(slab, shot, out CutSlash slash);

                var heading = Microsoft.Xna.Framework.Vector3.Normalize(new(shot.X, 0f, shot.Z));
                _output.WriteLine($"shot {shot}: axis {slash.Axis}, offset {slash.Offset:F2}, half {slash.HalfLength:F2}");

                //Square to the shot's heading, level, unit, and turned so that up x axis points back at the gun
                Assert.Equal(0f, slash.Axis.Y, 5);
                Assert.Equal(1f, slash.Axis.Length(), 4);
                Assert.Equal(0f, Microsoft.Xna.Framework.Vector3.Dot(slash.Axis, heading), 4);
                Assert.True(Microsoft.Xna.Framework.Vector3.Dot(Microsoft.Xna.Framework.Vector3.Cross(Microsoft.Xna.Framework.Vector3.Up, slash.Axis), heading) < -0.999f,
                    "up x axis does not point back along the shot, where the sparks are thrown");

                //In the storey's plane: the line through the struck ball runs at its height, which is every destroyed ball's
                int first = slab.Falling.Count - slab.Result.Total;
                float low = float.MaxValue, high = float.MinValue;
                for (int i = first; i < slab.Falling.Count; i++)
                {
                    PhysicsBall ball = slab.Falling[i];
                    if (ball.ArrayPosition.Level != slab.Cell.Level) continue;

                    var at = ball.BallReference.Pose.Position.ToXna();
                    Assert.Equal(slab.Struck.Y, at.Y, 3);

                    float along = Microsoft.Xna.Framework.Vector3.Dot(at - slab.Struck, slash.Axis);
                    low = MathF.Min(low, along);
                    high = MathF.Max(high, along);
                }

                Assert.Equal(slab.Struck.Y, slash.Middle(slab.Struck).Y, 5);

                //Across what was cut, a ball past each end, and no further
                Assert.Equal(low - CutSlash.OVERRUN, slash.Offset - slash.HalfLength, 3);
                Assert.Equal(high + CutSlash.OVERRUN, slash.Offset + slash.HalfLength, 3);
            }

            //A storey of one: the shortest slash, centred on the struck ball
            CutSlash lone = CutSlash.Across(CutSlash.AxisFor(Shot, Microsoft.Xna.Framework.Vector3.Zero), 0f, 0f);
            Assert.Equal(0f, lone.Offset, 5);
            Assert.Equal(CutSlash.MIN_HALF_LENGTH, lone.HalfLength, 5);
            Assert.True(2f * lone.HalfLength >= 3f, "the shortest slash is under three balls");

            //A shot that knows no heading takes the gun's aim's
            Assert.Equal(CutSlash.AxisFor(Shot, Microsoft.Xna.Framework.Vector3.Zero), CutSlash.AxisFor(Microsoft.Xna.Framework.Vector3.Zero, Shot));
        }

        /// <summary>
        /// The severing's picture runs on the world's clock, which the slow motion scales: the blasts' own slots
        /// (<see cref="BlastSlots"/>, whose ages the draw reads), stepped with the session's world time as the game steps them,
        /// put the blade's light out and free its slot at the same wall time at 30, 60, 75 and 144 Hz as at the reference rate,
        /// to a frame. (Seen to fail with the slots aging by a sixtieth a step instead of by the time handed in.)
        /// </summary>
        [Fact]
        public void The_severing_burns_out_at_the_same_wall_time_at_any_frame_rate()
        {
            (double Dark, double Freed) BurnsOut(float hz)
            {
                using CutSlab slab = new();
                CutStaging staging = Strike(slab, Shot, out CutSlash slash);
                BlastSlots slots = new();
                slots.Cut(slab.Struck, slash, 0.6f);

                float dt = 1f / hz;
                double wall = 0.0, dark = -1.0;
                while (wall < 10.0)
                {
                    staging.StepWall(dt);
                    float scale = staging.TimeScale;
                    staging.StepWorld(dt * scale);
                    slots.Step(dt * scale);
                    wall += dt;

                    if (dark < 0.0 && !slots.TryGetLight(out _, out _, out _)) dark = wall;
                    if (!slots[0].Active) return (dark, wall);
                }

                return (dark, wall);
            }

            (double referenceDark, double referenceFreed) = BurnsOut(REFERENCE_HZ);
            _output.WriteLine($"reference ({REFERENCE_HZ} Hz): the light is out {referenceDark:F3} s and the slot freed {referenceFreed:F3} s after the strike");
            Assert.True(referenceFreed > CutBeats.LIFE_SECONDS, "the slow motion did not stretch the picture at all");
            Assert.True(referenceDark > 0.0 && referenceDark < referenceFreed, "the light outlived the slot");

            foreach (float hz in FrameRates)
            {
                (double dark, double freed) = BurnsOut(hz);
                _output.WriteLine($"{hz} Hz: dark {dark:F3} s, freed {freed:F3} s");

                //A frame's rounding either way: the frame that crosses the end, and the slow motion's own (its world time is
                //the same to 0.85 of a frame, The_slow_motion_costs_the_same_world_time_at_any_frame_rate)
                Assert.InRange(dark, referenceDark - 1.0 / hz - 1e-3, referenceDark + 1.0 / hz + 1e-3);
                Assert.InRange(freed, referenceFreed - 1.0 / hz - 1e-3, referenceFreed + 1.0 / hz + 1e-3);
            }
        }

        /// <summary>
        /// A shot fired while the picture burns plays the rest of it <see cref="CutBeats.HURRY_RATE"/> times as fast, through
        /// the slots the draw reads: whenever it comes, the slot is freed within <see cref="CutBeats.HurriedSecondsLeft"/>
        /// (a third of a second at most) and at the same wall time at 30, 60 and 144 Hz; the light fades out over frames, at
        /// 60 Hz never losing more than a quarter of its full strength in one (the stepped hurry it replaced put out half of
        /// it in one frame and is seen to fail this); a second shot does not hurry it again, and a bomb is never hurried.
        /// </summary>
        [Fact]
        public void A_shot_plays_the_rest_of_the_picture_out_fast_and_puts_nothing_out_in_one_frame()
        {
            CutSlash lone = CutSlash.Across(Microsoft.Xna.Framework.Vector3.UnitX, 0f, 0f);

            //The light's colour at the blade's full strength: what one frame's drop is measured against
            BlastSlots fresh = new();
            fresh.Cut(Microsoft.Xna.Framework.Vector3.Zero, lone, 1f);
            fresh.TryGetLight(out _, out Microsoft.Xna.Framework.Vector3 fullColour, out _);
            float full = fullColour.Length();

            (double Freed, float WorstDrop) HurriedAt(float age, float hz)
            {
                BlastSlots slots = new();
                slots.Cut(Microsoft.Xna.Framework.Vector3.Zero, lone, 1f);

                //Up to the shot at the world's full speed, in fine steps (the shot ends the slow motion)
                const float fine = 1f / 2000f;
                while (slots[0].Age + fine <= age) slots.Step(fine);

                Assert.True(slots.HurryBlades(), "a burning blade was not hurried");
                Assert.False(slots.HurryBlades(), "a second shot hurried it again");

                float dt = 1f / hz, worst = 0f;
                float before = slots.TryGetLight(out _, out Microsoft.Xna.Framework.Vector3 colour, out _) ? colour.Length() : 0f;
                double wall = 0.0;
                while (slots[0].Active && wall < 5.0)
                {
                    slots.Step(dt);
                    wall += dt;

                    float now = slots.TryGetLight(out _, out colour, out _) ? colour.Length() : 0f;
                    worst = MathF.Max(worst, before - now);
                    before = now;
                }

                return (wall, worst);
            }

            foreach (float age in new[] { 0f, 0.3f, 0.9f, CutBeats.SLASH_SECONDS - 0.1f })
            {
                (double freed, float drop) = HurriedAt(age, 60f);
                float share = drop / full;
                _output.WriteLine($"a shot at {age:F2} s: gone {freed:F3} s later at 60 Hz, the light's worst frame {share:P0} of its full strength");

                Assert.True(freed <= CutBeats.HurriedSecondsLeft(age) + 1.0 / 60.0 + 1e-4, $"a shot at {age} s left {freed:F3} s of the picture");
                Assert.True(freed <= CutBeats.LIFE_SECONDS / CutBeats.HURRY_RATE + 1.0 / 60.0 + 1e-4, "a hurried picture outlasts a third of a second");
                Assert.True(share <= 0.25f, $"a shot at {age} s put out {share:P0} of the light in one frame");

                foreach (float hz in new[] { 30f, 144f })
                    Assert.InRange(HurriedAt(age, hz).Freed, freed - 1.0 / 30.0 - 1e-3, freed + 1.0 / 30.0 + 1e-3);
            }

            //A bomb ages at the world's pace whatever is fired
            BlastSlots bombs = new();
            bombs.SetOff(new[] { new Detonation(Microsoft.Xna.Framework.Vector3.Zero, 0, 12) });
            Assert.False(bombs.HurryBlades(), "a bomb was hurried");
            bombs.Step(0.1f);
            Assert.Equal(0.1f, bombs[0].Age, 5);
        }

        /// <summary>
        /// "The sparks are barely perceptible, so few and so briefly are they shown": seven sparks in ten now live a second
        /// or more; the slash lasts twice the first cut's 0.9 s; the slot outlives every part; the light lasts with them.
        /// </summary>
        [Fact]
        public void The_sparks_read_for_a_second_and_the_slot_outlives_every_part()
        {
            //A spark's life is SPARK_SECONDS times a jitter spread evenly between LEAST and MOST
            float shareOverASecond = (CutBeats.SPARK_LIFE_MOST - 1f / CutBeats.SPARK_SECONDS) / (CutBeats.SPARK_LIFE_MOST - CutBeats.SPARK_LIFE_LEAST);
            _output.WriteLine($"sparks living a second or more: {shareOverASecond:P0}");
            Assert.True(shareOverASecond >= 0.7f, $"only {shareOverASecond:P0} of the sparks live a second");

            Assert.True(CutBeats.SLASH_SECONDS >= 1.8f, "the slash is not twice the first cut's");
            Assert.True(CutBeats.LIFE_SECONDS >= CutBeats.SLASH_SECONDS, "the slot is freed before the slash has burnt out");
            Assert.True(CutBeats.LIFE_SECONDS >= CutBeats.SPARK_SECONDS * CutBeats.SPARK_LIFE_MOST + CutBeats.SPARK_RUN_SECONDS,
                "the slot is freed before the longest-lived spark has died");

            Assert.True(CutBeats.LightStrength(0.5f, 0f) > 0.1f, "the light is gone in half a second");
            Assert.True(CutBeats.LightStrength(0f, 0f) >= 0.5f, "a cut that freed nothing lights the balls at under half strength");
            Assert.Equal(0f, CutBeats.LightStrength(CutBeats.LIGHT_SECONDS, 1f));
        }

        /// <summary>
        /// The flash is the one part bright and wide enough to hide the field: it is under a tenth of its light by the time the
        /// shortest slow motion (a cut that freed nothing) has let its world time pass, so it never hides the field past the
        /// slow motion.
        /// </summary>
        [Fact]
        public void The_flash_never_outlasts_the_slow_motion()
        {
            float shortest = CutBeats.WorldSecondsOfSlowMotion(0f, false);
            _output.WriteLine($"the shortest slow motion lets {shortest:F3} s of the world pass; the flash is at {CutBeats.FlashLeft(shortest):F3} then");
            Assert.True(CutBeats.FlashLeft(shortest) < 0.1f, "the flash outlasts the shortest slow motion");
        }

        /// <summary>
        /// Both Blast.fx files (the desktop's, and GamePi's, which the Pi and the Windows Potato run draw) carry the severing's
        /// figures as CutBeats states them: the shaders cannot be handed them, so a figure changed on one side alone would
        /// cut a slot off mid-fade or leave the tests holding the wrong picture.
        /// </summary>
        [Theory]
        [InlineData("BS3DLibs/Prazsky.Shaders/Content/Shaders/Blast.fx")]
        [InlineData("GamePi/Shaders/Blast.fx")]
        public void Both_shaders_carry_the_severings_figures(string relative)
        {
            string shader = File.ReadAllText(FindUnderRepository(relative.Replace('/', Path.DirectorySeparatorChar)));

            float Figure(string name)
            {
                Match match = Regex.Match(shader, @"static const float " + name + @" = ([0-9.]+);");
                Assert.True(match.Success, $"{relative} states no {name}");
                return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }

            Assert.Equal(CutBeats.SLASH_SECONDS, Figure("SLASH_SECONDS"), 4);
            Assert.Equal(CutBeats.FLASH_SECONDS, Figure("BLADE_FLASH_SECONDS"), 4);
            Assert.Equal(CutBeats.SPARK_SECONDS, Figure("BLADE_SPARK_SECONDS"), 4);
            Assert.Equal(CutBeats.SPARK_RUN_SECONDS, Figure("BLADE_SPARK_RUN"), 4);
            Assert.Contains("(0.55 + 0.9 * input.Random.x)", shader);
            Assert.Equal(CutBeats.SPARK_LIFE_LEAST, 0.55f, 4);
            Assert.Equal(CutBeats.SPARK_LIFE_MOST, 0.55f + 0.9f, 4);
        }

        private static string FindUnderRepository(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
