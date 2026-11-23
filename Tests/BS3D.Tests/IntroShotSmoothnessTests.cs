using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The chapter-intro shots that fly over rough ground ride like drone footage (#645, #530, #1109, #1112). Every
    /// shot whose height <see cref="AridIntroPaths.Hug"/> lays — the desert's erg, pyramids and crest, the
    /// mountains' range, pass and summit, the outback's plain, approach and crown, the volcano's crater run and its orbit
    /// round the village (#1127), the Moon's and Mars's tracks (#1114, #1115) and the polar camp's arc (#1128) — the
    /// desert's shots low over its sand (#1110: the beetle, the scorpion and the scorpion home's take-off) and every shot of
    /// the meadow's (#1187: the valley, the two low orbits, the path, the brook and the brook home, on the test planting of
    /// <see cref="MeadowIntroShotsTests"/>) is built through its real builder on the shipped scene config over a dozen rolls
    /// (the meadow's two orbits over <see cref="ORBIT_ROLLS"/>, a layout a roll) and sampled at sixty frames a second on its
    /// own eased clock.
    /// <para>
    /// <b>What the limits are, and why.</b> Drone footage changes height and tilt slowly: a gimbal tilts at most about
    /// twenty degrees a second and takes half a second or more to get there, and the craft's climb rate changes over a
    /// second, never from one frame to the next. So the view pitches at most <see cref="MAX_PITCH_RATE"/> and its tilt
    /// changes by at most <see cref="MAX_PITCH_ACCELERATION"/> (the full tilt rate in half a second); the lens's
    /// vertical acceleration peaks at <see cref="MAX_VERTICAL_ACCELERATION"/> and changes by no more than
    /// <see cref="MAX_VERTICAL_JERK"/>, 2.5 u/s² from one frame to the next. Before #1109 the box averages' corners put
    /// 36-198 u/s² peaks and 1700-10400 u/s³ jumps into ten of these shots and 71-186 °/s² of tilt into five ("the
    /// camera judders over the dunes"); now the worst are 13 u/s², 122 u/s³ and 35 °/s². Peaks, not means: a judder is
    /// one bad frame in a hundred good ones, which an rms hides.
    /// </para>
    /// <para>
    /// <b>And sideways</b> (#1187): the view's turn is held as its tilt is, at most <see cref="MAX_YAW_RATE"/> and reached in no
    /// less than half a second (<see cref="MAX_YAW_ACCELERATION"/>), and the lens's whole acceleration changes by no more
    /// than <see cref="MAX_JERK"/>, 5 u/s² from one frame to the next along any axis. The meadow's brook home, a flight round
    /// the island at up to 42 u/s whose route is pieces laid end to end, turned the view at 56 °/s, swung it at 224 °/s² and
    /// jumped the lens at 3111 u/s³ where the pieces met; the worst of every other shot here were 84 °/s² (the scorpion
    /// home) and 289 u/s³ (the volcano's village, an orbit at 60 u/s).
    /// </para>
    /// <para>
    /// ⚠ <b>Smooth is not allowed to mean high</b>: a lens flown well over the ground is trivially smooth. So every
    /// frame keeps its shot's clearance over the ground under it, and a shot with no designed floor stays on average
    /// within <see cref="MAX_MEAN_LIFT"/> of its clearance on every roll (on its worst roll the pass, over the roughest
    /// ground, flies 8.8 over it on average and 14.8 at its highest frame, the crest 6.4 and 10.4, the pyramids' run 5.8
    /// and 12.1). The highest frame is not held: the lift over a cut crest is a plateau (see
    /// <see cref="AridIntroPaths.LIFT_SECONDS"/>) whose height is the crest's cut however wide it is, so a bound on it
    /// failed only where this mean already does (a 2 s rounding), and never with the lift made five times as wide.
    /// The cut is a discontinuity of everything, so the derivatives are
    /// measured from <see cref="EDGE_FRAMES"/> frames inside each end (the speed at the cut has its own test below).
    /// </para>
    /// </summary>
    public class IntroShotSmoothnessTests
    {
        private const int ROLLS = 12;
        private const double FRAME = 1.0 / 60.0;
        private const int EDGE_FRAMES = 3;

        //The meadow's two orbits (#1187) are cheap to build, and what takes one past the limits is the ground under one
        //layout in a hundred or more: a crease of the cut field under the arc (260 u/s³ on roll 52 with the height held over
        //the ground point by point) or a hillside running down across it (the view tilted at 20.6 and 21.0 on rolls 498 and
        //938 with no arc refused for it). Over a dozen rolls neither was ever met.
        private const int ORBIT_ROLLS = 1000;

        //What drone footage is, in world units and degrees per second (see the class remarks). (internal: MeadowLandingTests
        //holds the brook home to them over its whole sweep of poses)
        internal const double MAX_VERTICAL_ACCELERATION = 20.0;
        internal const double MAX_VERTICAL_JERK = 150.0;
        internal const double MAX_PITCH_RATE = 20.0;
        internal const double MAX_PITCH_ACCELERATION = 40.0;
        internal const double MAX_YAW_RATE = 50.0;

        //And sideways (#1187): the view's turn reached in no less than half a second, as its tilt is, and the lens's whole
        //acceleration changing by no more than 5 u/s² from one frame to the next, along any axis. The meadow's brook home, held
        //to neither, swung the view at 224 °/s² and jumped the lens at 3111 u/s³ where the pieces of its route met.
        internal const double MAX_YAW_ACCELERATION = 100.0;
        internal const double MAX_JERK = 300.0;

        //The low runs along their own travel and the crater's climb were "too fast" at 30-59 u/s (#645).
        private const double MAX_SPEED = 28.0;

        //How far over its clearance a shot without a designed floor may fly on average.
        private const double MAX_MEAN_LIFT = 9.0;

        //The lens is checked at sixty frames between the path's points, where the spline may sag a hair under them.
        private const float CLEARANCE_TOLERANCE = 0.05f;

        private static readonly float FieldOfView = MathHelper.ToRadians(45f);

        private readonly ITestOutputHelper _output;

        public IntroShotSmoothnessTests(ITestOutputHelper output) => _output = output;

        /// <summary>One shot: how to build it for a roll, the ground under it, the clearance it keeps, whether it is held
        /// to the speed limit, whether it has no designed floor (so its mean height over the clearance is held), and over how
        /// many rolls.</summary>
        private sealed record Case(Func<Random, IntroShot> Build, Func<float, float, float> Ground, float Clearance, bool Slow, bool Floorless,
            int Rolls = ROLLS);

        private static Dictionary<string, Case> Cases()
        {
            var desert = new DesertSceneConfig();
            Func<float, float, float> sand = (x, z) => TerrainMirror.Desert(x, z, desert);
            var mountain = new MountainSceneConfig();
            Func<float, float, float> rock = (x, z) => TerrainMirror.Mountain(x, z, mountain);
            var outback = new OutbackSceneConfig();
            Func<float, float, float> plain = (x, z) => TerrainMirror.Outback(x, z, outback);
            TerrainMirror.Formation monolith = OutbackIntroShots.Monolith(outback) ?? throw new InvalidOperationException("no monolith");
            var volcano = new VolcanoSceneConfig();
            Func<float, float, float> cone = (x, z) => TerrainMirror.Volcano(x, z, volcano);
            Vector2 centre = volcano.ConeCenter.ToVector2();
            Vector3 vent = new(centre.X, cone(centre.X, centre.Y) + 2f, centre.Y);
            var moon = new MoonSceneConfig();
            Func<float, float, float> regolith = (x, z) => TerrainMirror.Moon(x, z, moon.Terrain);
            Vector3 rope = DesertLandingTests.GamePosition, ropeTarget = DesertLandingTests.GameTarget;
            var mars = new MarsSceneConfig();
            Func<float, float, float> marsCeiling = (x, z) => OffworldGround.MarsCeiling(x, z, mars.Terrain);
            var polar = new PolarSceneConfig();
            Func<float, float, float> ice = (x, z) => TerrainMirror.Polar(x, z, polar);
            var meadow = new MeadowSceneConfig();
            Func<float, float, float> field = (x, z) => TerrainMirror.MeadowGround(x, z, meadow);
            Func<float, float, float> meadowCut = (x, z) => TerrainMirror.Meadow(x, z, meadow);

            return new Dictionary<string, Case>
            {
                ["desert: the erg"] = new(r => DesertIntroShots.Erg(sand, FieldOfView, r), sand, DesertIntroShots.WIDE_CLEARANCE, false, false),
                ["desert: the pyramids"] = new(r => DesertIntroShots.PyramidsShot(desert, sand, FieldOfView, r), sand, DesertIntroShots.PYRAMIDS_CLEARANCE, false, true),
                ["desert: the crest"] = new(r => DesertIntroShots.Crest(desert, sand, FieldOfView, r), sand, DesertIntroShots.CREST_CLEARANCE, true, true),
                //The two close shots (#1110): the beetle dollying beside its beetle, and the scorpion where no flight home is clear
                ["desert: the beetle"] = new(r => DesertIntroShots.Beetle(null, desert, sand, FieldOfView, r), sand, DesertIntroShots.BEETLE_LENS_ABOVE, true, true),
                ["desert: the scorpion"] = new(r => DesertIntroShots.Scorpion(null, desert, sand, FieldOfView, r), sand,
                    DesertIntroShots.SCORPION_CLEARANCE * 0.9f, true, true),
                //The take-off from the scorpion onto Rope's pose (#1110): its height is the pose's by design, so it has a floor
                ["desert: the scorpion home"] = new(r => DesertLanding.Build(desert, null, rope, ropeTarget, FieldOfView, r, out _), sand,
                    DesertLanding.LOWEST, true, false),
                ["mountains: the range"] = new(r => MountainIntroShots.Range(mountain, rock, FieldOfView, r), rock, MountainIntroShots.WIDE_CLEARANCE, false, false),
                ["mountains: the pass"] = new(r => MountainIntroShots.Pass(rock, FieldOfView, r), rock, MountainIntroShots.PASS_CLEARANCE, true, true),
                ["mountains: the summit"] = new(r => MountainIntroShots.Summit(rock, FieldOfView, r), rock, MountainIntroShots.SUMMIT_CLEARANCE, false, false),
                ["outback: the plain"] = new(r => OutbackIntroShots.Plain(monolith, plain, FieldOfView, r), plain, OutbackIntroShots.WIDE_CLEARANCE, false, false),
                ["outback: the approach"] = new(r => OutbackIntroShots.Approach(outback, monolith, plain, FieldOfView, r), plain, OutbackIntroShots.APPROACH_CLEARANCE, false, true),
                ["outback: the crown"] = new(r => OutbackIntroShots.Crown(outback, monolith, plain, FieldOfView, r), plain, OutbackIntroShots.CRANE_CLEARANCE, false, false),
                ["volcano: the crater"] = new(r => VolcanoIntroShots.Crater(cone, vent, volcano, FieldOfView, r), cone, VolcanoIntroShots.CLIMB_CLEARANCE, true, false),
                //An orbit high over the land (#1127), not a run along the ground: held to the ride, not to the low runs' speed
                ["volcano: the village"] = new(r => VolcanoIntroShots.Village(cone, vent, volcano, FieldOfView, r), cone,
                    VolcanoCountryPlanting.Tallest + VolcanoIntroShots.TREE_AIR, false, true),
                //The ruts and the flag are fixed (#1114): every roll is the same run.
                ["moon: the tracks"] = new(_ => MoonIntroShots.Tracks(moon.Terrain), regolith, MoonIntroShots.TRACKS_CLEARANCE, true, true),
                //So are Mars's (#1115), and Mars has no mirror of its ground: the lens flies its ceiling, the clearance over that.
                ["mars: the tracks"] = new(_ => MarsIntroShots.Tracks(mars.Terrain), marsCeiling, MarsIntroShots.CLEARANCE, true, true),
                //An arc at one height over the highest ice under it (#1128), the lens on the camp: held to the ride all the same
                ["polar: the camp"] = new(r => PolarIntroShots.Camp(polar, FieldOfView, r), ice, PolarIntroShots.CAMP_HEIGHT, true, false),
                //The meadow (#1187): the valley's level dolly over its highest ground, the two low orbits on the cut ground, the
                //walk up the path and, where no flight home is clear, the run down the brook, each over the uncut field it was
                //laid on; and the brook home, a flight round the island at up to 42 u/s (MeadowLanding), not a low run
                ["meadow: the valley"] = new(Meadow("the valley"), field, MeadowIntroShots.VALLEY_ABOVE_TO, true, false),
                ["meadow: the bales"] = new(Orbit(bales: true), meadowCut, MeadowIntroShots.ORBIT_ABOVE_FROM, true, true, ORBIT_ROLLS),
                ["meadow: the path"] = new(Meadow("the path"), field, MeadowIntroShots.PATH_ABOVE, true, true),
                ["meadow: the bench"] = new(Orbit(bales: false), meadowCut, MeadowIntroShots.ORBIT_ABOVE_FROM, true, true, ORBIT_ROLLS),
                ["meadow: the brook"] = new(Meadow("the brook", landing: false), field, MeadowLanding.BROOK_ABOVE, true, true),
                ["meadow: the brook home"] = new(Meadow(MeadowLanding.NAME), (x, z) => MeadowLanding.Floor(x, z, meadow), MeadowLanding.LOWEST, false, false),
            };
        }

        /// <summary>
        /// A meadow shot (#1187) as the prologue builds it, on <see cref="MeadowIntroShotsTests"/>' planting (the planting needs a
        /// device, so the test lays one from its constants) at the level's own pose, a layout off each roll; a layout that has
        /// no such shot is passed over for the next. <paramref name="landing"/> false asks it of a pose no
        /// flight home reaches (the menu's crane over the island), where the reel keeps its run down the brook.
        /// </summary>
        private static Func<Random, IntroShot> Meadow(string name, bool landing = true) => r =>
        {
            Vector3 pose = landing ? MeadowIntroShotsTests.GamePosition : new Vector3(0f, -2f, 15f);
            for (int layout = 0; layout < 8; layout++)
            {
                MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(r.Next(1, 10000));
                IntroShot shot = Array.Find(MeadowIntroShots.Build(planting.Config, planting.Figures, pose, MeadowIntroShotsTests.GameTarget,
                    MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW, r), s => s.Name == name);
                if (shot != null) return shot;
            }

            throw new InvalidOperationException($"no layout of eight had '{name}'");
        };

        /// <summary>
        /// One of the meadow's two orbits (#1187) straight off its builder (<see cref="MeadowIntroShots.Bales"/>,
        /// <see cref="MeadowIntroShots.Bench"/>), which the prologue calls with its own roll, on a layout off each roll: the
        /// whole prologue lays the brook home too, which is too dear to build <see cref="ORBIT_ROLLS"/> times.
        /// </summary>
        private static Func<Random, IntroShot> Orbit(bool bales) => r =>
        {
            for (int layout = 0; layout < 8; layout++)
            {
                MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(r.Next(1, 10000));
                IntroShot shot = bales
                    ? MeadowIntroShots.Bales(planting.Config, planting.Figures, MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW, r)
                    : MeadowIntroShots.Bench(planting.Config, planting.Figures, MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW, r);
                if (shot != null) return shot;
            }

            throw new InvalidOperationException($"no layout of eight had {(bales ? "the bales" : "the bench")}");
        };

        public static IEnumerable<object[]> Shots()
        {
            foreach (string name in Cases().Keys) yield return new object[] { name };
        }

        [Theory]
        [MemberData(nameof(Shots))]
        public void ATerrainShotRidesLikeDroneFootage(string name)
        {
            Case shot = Cases()[name];
            var worst = new Ride { LeastClearance = double.MaxValue };
            var rides = new Ride[shot.Rolls + 1];
            for (int seed = 1; seed <= shot.Rolls; seed++) worst = worst.Worst(rides[seed] = Measure(shot.Build(new Random(seed)), shot.Ground));

            //The worst over the rolls first, so a failing shot still says how far over it is
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0}: speed {1:F1} u/s, vertical {2:F1} u/s² {3:F0} u/s³, pitch {4:F1}°/s {5:F0}°/s², yaw {6:F1}°/s {10:F0}°/s², jerk {11:F0} u/s³, clearance least {7:F2} mean {8:F1} highest {9:F1}",
                name, worst.Speed, worst.PeakVerticalAcceleration, worst.PeakVerticalJerk, worst.PeakPitchRate, worst.PeakPitchAcceleration,
                worst.PeakYawRate, worst.LeastClearance, worst.MeanClearance, worst.HighestClearance, worst.PeakYawAcceleration, worst.PeakJerk));

            for (int seed = 1; seed <= shot.Rolls; seed++)
            {
                Ride ride = rides[seed];

                Assert.True(ride.LeastClearance >= shot.Clearance - CLEARANCE_TOLERANCE,
                    $"{name} (roll {seed}): the lens is {ride.LeastClearance:F2} over the ground, under its clearance {shot.Clearance}");
                if (shot.Slow) Assert.True(ride.Speed <= MAX_SPEED, $"{name} (roll {seed}): {ride.Speed:F1} units a second is too fast");
                if (shot.Floorless)
                    Assert.True(ride.MeanClearance - shot.Clearance <= MAX_MEAN_LIFT,
                        $"{name} (roll {seed}): the lens flies {ride.MeanClearance - shot.Clearance:F1} over its clearance on average; smooth by flying high is not a fix");

                Assert.True(ride.PeakVerticalAcceleration <= MAX_VERTICAL_ACCELERATION,
                    $"{name} (roll {seed}): vertical acceleration peaks at {ride.PeakVerticalAcceleration:F1} u/s² at t={ride.PeakVerticalAccelerationAt:F2}");
                Assert.True(ride.PeakVerticalJerk <= MAX_VERTICAL_JERK,
                    $"{name} (roll {seed}): vertical acceleration jumps at {ride.PeakVerticalJerk:F0} u/s³ at t={ride.PeakVerticalJerkAt:F2}");
                Assert.True(ride.PeakPitchRate <= MAX_PITCH_RATE, $"{name} (roll {seed}): the view pitches {ride.PeakPitchRate:F1} degrees a second");
                Assert.True(ride.PeakPitchAcceleration <= MAX_PITCH_ACCELERATION,
                    $"{name} (roll {seed}): the view's tilt changes at {ride.PeakPitchAcceleration:F0} degrees a second squared at t={ride.PeakPitchAccelerationAt:F2}");
                Assert.True(ride.PeakYawRate <= MAX_YAW_RATE, $"{name} (roll {seed}): the view turns {ride.PeakYawRate:F1} degrees a second");
                Assert.True(ride.PeakYawAcceleration <= MAX_YAW_ACCELERATION,
                    $"{name} (roll {seed}): the view's turn changes at {ride.PeakYawAcceleration:F0} degrees a second squared at t={ride.PeakYawAccelerationAt:F2}");
                Assert.True(ride.PeakJerk <= MAX_JERK, $"{name} (roll {seed}): the lens's acceleration jumps at {ride.PeakJerk:F0} u/s³ at t={ride.PeakJerkAt:F2}");
            }
        }

        /// <summary>
        /// A shot leaves its cut and comes into the next at its own speed (#1109): the path's end point does not stand
        /// in for its own missing neighbour on the spline, which halved the lens's speed over the first and last point of
        /// every shot. Along a straight, evenly spaced path the first frame's step and the last's are the size of their
        /// neighbours' (the clock's ease changes by under one per cent in a frame there), and the shot still ends exactly
        /// on its last point, as a landing needs.
        /// </summary>
        [Fact]
        public void AShotKeepsItsSpeedThroughTheCut()
        {
            var path = new Vector3[AridIntroPaths.POINTS];
            for (int i = 0; i < path.Length; i++) path[i] = new Vector3(i * 0.8f, 5f, 0f);
            var shot = new IntroShot("a straight run", path, 3.2f, FieldOfView);

            int frames = (int)(shot.Seconds / FRAME);
            Vector3 Lens(int frame)
            {
                shot.Pose(frame / (float)(frames - 1), out Vector3 position, out _);
                return position;
            }

            float first = Vector3.Distance(Lens(0), Lens(1)), second = Vector3.Distance(Lens(1), Lens(2));
            float last = Vector3.Distance(Lens(frames - 2), Lens(frames - 1)), beforeLast = Vector3.Distance(Lens(frames - 3), Lens(frames - 2));

            Assert.True(first >= 0.97f * second, $"the lens leaves the cut at {first / second:P0} of its speed a frame later");
            Assert.True(last >= 0.97f * beforeLast, $"the lens comes into the cut at {last / beforeLast:P0} of its speed a frame earlier");
            Assert.Equal(path[^1].X, Lens(frames - 1).X, 3);
        }

        /// <summary>
        /// A designed floor is flown where it was laid (#1109): over ground that never lifts it, a straight climb and a
        /// level height come out of <see cref="AridIntroPaths.Hug"/> exactly as laid, and a crane's smoothstep keeps its
        /// foot and its top (the rounding takes its S towards a straight climb in between). What the rounding takes off a
        /// floor's own curve is not lifted back: that lift carried every crane's top over its design, the outback crown
        /// 4.5 units over its own on every roll.
        /// </summary>
        [Fact]
        public void ADesignedFloorIsFlownWhereItWasLaid()
        {
            Vector2[] plan = AridIntroPaths.Line(Vector2.Zero, new Vector2(80f, 0f));
            int last = plan.Length - 1;
            Func<float, float, float> flat = (_, _) => 0f;

            float Smoothstep(int i, float from, float to)
            {
                float s = i / (float)last;
                return MathHelper.Lerp(from, to, s * s * (3f - 2f * s));
            }

            Vector3[] crane = AridIntroPaths.Hug(plan, flat, 4f, 3f, 3.2f, i => Smoothstep(i, 32f, 56f));
            Assert.Equal(32f, crane[0].Y, 2);
            Assert.Equal(56f, crane[last].Y, 2);

            Vector3[] climb = AridIntroPaths.Hug(plan, flat, 4f, 3f, 3.2f, i => MathHelper.Lerp(48f, 62f, i / (float)last));
            Vector3[] level = AridIntroPaths.Hug(plan, flat, 4f, 3f, 3.2f, _ => 30f);
            for (int i = 0; i <= last; i++)
            {
                Assert.True(MathF.Abs(climb[i].Y - MathHelper.Lerp(48f, 62f, i / (float)last)) < 0.01f, $"the straight climb is off its line at point {i}: {climb[i].Y:F3}");
                Assert.True(MathF.Abs(level[i].Y - 30f) < 0.01f, $"the level height is off it at point {i}: {level[i].Y:F3}");
            }

            //The real crown, every roll: its top is the crown's own height plus CRANE_OVER_CROWN.
            var outback = new OutbackSceneConfig();
            Func<float, float, float> plain = (x, z) => TerrainMirror.Outback(x, z, outback);
            TerrainMirror.Formation rock = OutbackIntroShots.Monolith(outback) ?? throw new InvalidOperationException("no monolith");
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                OutbackIntroShots.Crown(outback, rock, plain, FieldOfView, new Random(seed)).Pose(1f, out Vector3 top, out _);
                Assert.True(MathF.Abs(top.Y - (rock.CrownY + OutbackIntroShots.CRANE_OVER_CROWN)) < 0.05f,
                    $"the crown (roll {seed}) ends at {top.Y:F2}, its design at {rock.CrownY + OutbackIntroShots.CRANE_OVER_CROWN:F2}");
            }
        }

        /// <summary>The peaks of one shot's ride (<see cref="Measure"/>), or the worst over several.</summary>
        internal sealed class Ride
        {
            public double Speed, LeastClearance, MeanClearance, HighestClearance;
            public double PeakVerticalAcceleration, PeakVerticalAccelerationAt, PeakVerticalJerk, PeakVerticalJerkAt;
            public double PeakPitchRate, PeakPitchAcceleration, PeakPitchAccelerationAt, PeakYawRate;
            public double PeakYawAcceleration, PeakYawAccelerationAt, PeakJerk, PeakJerkAt;

            public Ride Worst(Ride other) => new()
            {
                Speed = Math.Max(Speed, other.Speed),
                LeastClearance = Math.Min(LeastClearance, other.LeastClearance),
                MeanClearance = Math.Max(MeanClearance, other.MeanClearance),
                HighestClearance = Math.Max(HighestClearance, other.HighestClearance),
                PeakVerticalAcceleration = Math.Max(PeakVerticalAcceleration, other.PeakVerticalAcceleration),
                PeakVerticalJerk = Math.Max(PeakVerticalJerk, other.PeakVerticalJerk),
                PeakPitchRate = Math.Max(PeakPitchRate, other.PeakPitchRate),
                PeakPitchAcceleration = Math.Max(PeakPitchAcceleration, other.PeakPitchAcceleration),
                PeakYawRate = Math.Max(PeakYawRate, other.PeakYawRate),
                PeakYawAcceleration = Math.Max(PeakYawAcceleration, other.PeakYawAcceleration),
                PeakJerk = Math.Max(PeakJerk, other.PeakJerk),
            };
        }

        /// <summary>The shot at sixty frames a second on its own clock: the lens's height, the view's pitch and yaw, and the
        /// clearance over <paramref name="ground"/>.</summary>
        internal static Ride Measure(IntroShot shot, Func<float, float, float> ground)
        {
            int frames = (int)(shot.Seconds / FRAME);
            var position = new Vector3[frames];
            var pitch = new double[frames];
            var yaw = new double[frames];
            var ride = new Ride { LeastClearance = double.MaxValue };

            for (int i = 0; i < frames; i++)
            {
                shot.Pose(i / (float)(frames - 1), out Vector3 p, out Vector3 target);
                Vector3 forward = Vector3.Normalize(target - p);
                position[i] = p;
                pitch[i] = MathHelper.ToDegrees(MathF.Asin(MathHelper.Clamp(forward.Y, -1f, 1f)));

                //Unwrapped, so a heading through south is not a jump of 360 degrees.
                double heading = MathHelper.ToDegrees(MathF.Atan2(forward.X, forward.Z));
                while (i > 0 && heading - yaw[i - 1] > 180.0) heading -= 360.0;
                while (i > 0 && heading - yaw[i - 1] < -180.0) heading += 360.0;
                yaw[i] = heading;

                double clearance = p.Y - ground(p.X, p.Z);
                ride.LeastClearance = Math.Min(ride.LeastClearance, clearance);
                ride.HighestClearance = Math.Max(ride.HighestClearance, clearance);
                ride.MeanClearance += clearance / frames;
            }

            double dt = shot.Seconds / (frames - 1);
            double travelled = 0;
            for (int i = 1; i < frames; i++)
            {
                travelled += Vector3.Distance(position[i], position[i - 1]);
                ride.PeakPitchRate = Math.Max(ride.PeakPitchRate, Math.Abs(pitch[i] - pitch[i - 1]) / dt);
                ride.PeakYawRate = Math.Max(ride.PeakYawRate, Math.Abs(yaw[i] - yaw[i - 1]) / dt);
            }

            ride.Speed = travelled / shot.Seconds;

            //Second and third differences, EDGE_FRAMES inside each end.
            for (int i = EDGE_FRAMES; i < frames - EDGE_FRAMES; i++)
            {
                double t = i / (double)(frames - 1);
                double acceleration = Math.Abs(position[i + 1].Y - 2.0 * position[i].Y + position[i - 1].Y) / (dt * dt);
                if (acceleration > ride.PeakVerticalAcceleration) (ride.PeakVerticalAcceleration, ride.PeakVerticalAccelerationAt) = (acceleration, t);

                double tilt = Math.Abs(pitch[i + 1] - 2.0 * pitch[i] + pitch[i - 1]) / (dt * dt);
                if (tilt > ride.PeakPitchAcceleration) (ride.PeakPitchAcceleration, ride.PeakPitchAccelerationAt) = (tilt, t);

                double swing = Math.Abs(yaw[i + 1] - 2.0 * yaw[i] + yaw[i - 1]) / (dt * dt);
                if (swing > ride.PeakYawAcceleration) (ride.PeakYawAcceleration, ride.PeakYawAccelerationAt) = (swing, t);

                if (i + 2 >= frames - EDGE_FRAMES + 1) continue;

                double jerk = Math.Abs(position[i + 2].Y - 3.0 * position[i + 1].Y + 3.0 * position[i].Y - position[i - 1].Y) / (dt * dt * dt);
                if (jerk > ride.PeakVerticalJerk) (ride.PeakVerticalJerk, ride.PeakVerticalJerkAt) = (jerk, t);

                double whole = (position[i + 2] - 3f * position[i + 1] + 3f * position[i] - position[i - 1]).Length() / (dt * dt * dt);
                if (whole > ride.PeakJerk) (ride.PeakJerk, ride.PeakJerkAt) = (whole, t);
            }

            return ride;
        }
    }
}
