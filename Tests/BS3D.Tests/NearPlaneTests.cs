using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;
using static BS3D.Tests.PlayViewOpenings;

namespace BS3D.Tests
{
    /// <summary>
    /// The Game's near plane follows the lens's distance from the drain (#1193, <see cref="RecoilCamera.NearPlaneAt"/>). The owner
    /// saw a line-like flicker round the drain's rim and down its inner sides in the meadow's intro, from the valley shot some 200
    /// units out, and none from the play view. Measured in frames: the dark pit sheath behind the glass won the depth test over the
    /// top of the glass, where the two part from nothing at the mouth, in streaks that moved with every frame of the flight, and the
    /// gold collar's edge in teeth; with the near plane held at 0.15 over that shot the streaks were gone and at 0.3 the teeth, the
    /// shadows untouched, so the depth buffer was the cause.
    /// What a number can hold is here, over every chapter scene's intro (and Mars's, the polar camp's, the sea's and the
    /// outback's): every play view keeps the near plane it had; from every intro frame that shows the drain, the band of glass the
    /// depth buffer cannot part from the sheath spans no more of the frame than from the play view the owner sees nothing wrong
    /// in, within a tenth, except where a close subject holds the plane down; and no corner of a farther near plane reaches the
    /// ground, the cavern's rock or the city's boxes.
    /// </summary>
    public class NearPlaneTests
    {
        private readonly ITestOutputHelper _output;

        public NearPlaneTests(ITestOutputHelper output) => _output = output;

        //The near and far plane every lens that keeps the rule's lower end draws with, and the point the rule measures from
        private static readonly float NEAR = new RecoilCamera().NearPlane;
        private static readonly float FAR = new RecoilCamera().FarPlane;
        private static readonly Vector3 Subject = new RecoilCamera().DepthSubject;

        //The middle of the drain's mouth, where the glass meets the stone (the drain's own matrix, ArenaIsland._drainWorld)
        private static readonly Vector3 Mouth = new(0f, ArenaIsland.TOP_Y - ArenaIsland.DISH_DEPTH, 0f);

        //The widest window the game is played at (PlayViewOpenings.ASPECTS), which puts the near plane's corners farthest out
        private const float WIDEST_ASPECT = 2.4f;

        private const int ROLLS = 6;
        private const int POSES = 61;

        //How much wider than the widest play view's an intro frame's band may be: what the hold itself leaves, a lens
        //NEAR_HOLD_DISTANCE from the drain still drawing with NearPlane
        private const float BAND_SLACK = 1.1f;

        //The air every corner of a near plane farther than NearPlane keeps over what it could cut into: grass, stones and the
        //scatter stand on the ground the mirrors give, a unit or less of them under any shot that flies this low
        private const float CORNER_AIR = 1f;

        //A 24-bit depth step at view depth z under a perspective projection with these planes (standard Z, as RecoilCamera builds)
        private static float DepthStep(float z, float near) => z * z * (FAR - near) / (FAR * near * 16777216f);

        //How much of the frame the band of the glass nearer the sheath than a depth step spans, up to a factor every lens shares:
        //the band is as deep down the glass as the step at the mouth's far side (the glass and the sheath part linearly from the
        //mouth), and a length at depth z spans 1 / z of the frame
        private static float Band(Vector3 lens, float near)
        {
            float z = Vector3.Distance(lens, Mouth) + ArenaIsland.FUNNEL_TOP_RADIUS;
            return DepthStep(z, near) / z;
        }

        //A RecoilCamera posed so, at the window shape and with the shot's lens and roll
        private static RecoilCamera Posed(Vector3 lens, Vector3 target, float fieldOfView = MathHelper.PiOver4, float aspect = 16f / 9f,
            float roll = 0f)
        {
            var camera = new RecoilCamera { BasePosition = lens, BaseTarget = target, FieldOfView = fieldOfView, AspectRatio = aspect, BaseRoll = roll };
            camera.Recalculate();
            return camera;
        }

        //The near plane a RecoilCamera posed so builds its projection with, read back off the projection (M43 / M33)
        private static float ProjectedNear(RecoilCamera camera)
        {
            Assert.Equal(camera.EffectiveNearPlane, camera.Projection.M43 / camera.Projection.M33, 4);
            return camera.EffectiveNearPlane;
        }

        //The widest play view's band: the bound every intro frame is held to
        private static float PlayBand()
        {
            float band = 0f;
            foreach (float aspect in ASPECTS)
                foreach (string file in Shipped.LevelFiles())
                    foreach ((_, Vector3 lens, Vector3 target, _) in Open(file, aspect).Poses)
                        band = MathF.Max(band, Band(lens, ProjectedNear(Posed(lens, target))));
            return band;
        }

        [Fact]
        public void The_rule_holds_near_the_drain_or_a_close_target_grows_with_the_drains_distance_and_stops_at_the_ceiling()
        {
            float hold = RecoilCamera.NEAR_HOLD_DISTANCE, targetHold = RecoilCamera.TARGET_HOLD_DISTANCE;

            //Within the hold of the drain, or within the target's hold of the target, however far the other
            foreach (float subject in new[] { 0f, 1f, 30f, hold })
                foreach (float target in new[] { 0f, 10f, targetHold, hold, 900f, float.PositiveInfinity })
                    Assert.Equal(NEAR, RecoilCamera.NearPlaneAt(NEAR, target, subject));
            foreach (float target in new[] { 0f, 4f, 10f, targetHold })
                foreach (float subject in new[] { hold * 2f, 600f, 1100f, float.PositiveInfinity })
                    Assert.Equal(NEAR, RecoilCamera.NearPlaneAt(NEAR, target, subject));

            //Past the hold, with the target far, the step at the drain stays where it is at the hold until the ceiling, and the
            //plane then stays at the ceiling however far; and the drain's distance decides, not the target's
            float held = DepthStep(hold, NEAR);
            float previous = NEAR;
            for (float d = hold; d < 2500f; d += 0.5f)
            {
                float near = RecoilCamera.NearPlaneAt(NEAR, 5000f, d);
                Assert.InRange(near, previous, RecoilCamera.NEAR_PLANE_CEILING);
                if (near < RecoilCamera.NEAR_PLANE_CEILING) Assert.True(DepthStep(d, near) <= held * 1.0001f, $"at {d} the step is {DepthStep(d, near)}");
                Assert.Equal(near, RecoilCamera.NearPlaneAt(NEAR, 2f * d + 100f, d));
                previous = near;
            }

            Assert.Equal(RecoilCamera.NEAR_PLANE_CEILING, RecoilCamera.NearPlaneAt(NEAR, 2000f, 2000f));
            Assert.Equal(1f, RecoilCamera.NearPlaneAt(1f, 2000f, 2000f));

            //A close target holds it down: never past NearPlane times the square of its distance over the target's hold
            for (float t = targetHold; t < 200f; t += 0.5f)
            {
                float share = t / targetHold;
                Assert.True(RecoilCamera.NearPlaneAt(NEAR, t, 600f) <= NEAR * share * share * 1.0001f, $"a target {t} off");
            }
        }

        /// <summary>
        /// "It costs nothing in the play view": every pose a shipped level's play view holds, at 16:9 to 4:3, is built with the near
        /// plane the game always had. Seen to fail with the hold at 30.
        /// </summary>
        [Fact]
        public void Every_play_view_keeps_the_near_plane()
        {
            float farthest = 0f;
            foreach (float aspect in ASPECTS)
                foreach (string file in Shipped.LevelFiles())
                    foreach ((string pose, Vector3 lens, Vector3 target, _) in Open(file, aspect).Poses)
                    {
                        farthest = MathF.Max(farthest, Vector3.Distance(lens, Subject));
                        Assert.True(ProjectedNear(Posed(lens, target)) == NEAR, $"{file} at {aspect:F2}, {pose}: the play view's near plane moved");
                    }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "the farthest a play view stands from the drain: {0:F1} (the hold {1})", farthest, RecoilCamera.NEAR_HOLD_DISTANCE));
        }

        /// <summary>
        /// The owner's report itself, in numbers, over every intro: from every frame in which the drain's mouth is in frame, its far
        /// side shows over the island's near rim and no ground stands between, the band of the glass the depth buffer cannot part
        /// from the dark sheath behind it spans no more of the frame than it does from the play view whose band is widest, where
        /// nobody has seen it, within <see cref="BAND_SLACK"/>; except in frames whose plane a close subject the shot looks at holds
        /// down, which are listed (a run's point ahead is no subject). At the near plane 0.05 the valley's band was 4.7 times the
        /// play view's and the volcano's village's more (seen to fail with the rule taken from the nearer of the target and the
        /// drain, as it first was, with the hold at 60, and with the Grid's push through its ring looking 10 ahead). The ceiling
        /// stops the plane past about 140, and the band from there grows again with the distance: it is back to the play view's
        /// farther out than any frame that shows the drain stands.
        /// </summary>
        [Fact]
        public void From_every_intro_frame_that_shows_the_drain_the_glass_the_buffer_cannot_part_from_the_sheath_spans_no_more_than_from_the_play_view()
        {
            float playBand = PlayBand();
            float worst = 0f, worstAtNear = 0f, farthestShown = 0f;
            string worstWhere = "", worstAtNearWhere = "";
            var held = new SortedDictionary<string, (int Frames, int Over, float Worst, float Farthest)>(StringComparer.Ordinal);

            for (int roll = 1; roll <= ROLLS; roll++)
                foreach (Reel reel in Reels(roll))
                    Walk(reel, (shot, t, camera, lens, target) =>
                    {
                        if (!ShowsTheDrain(camera, lens, reel.Ground)) return;

                        float near = ProjectedNear(camera);
                        float stand = Vector3.Distance(lens, Mouth);
                        float band = Band(lens, near) / playBand, atNear = Band(lens, NEAR) / playBand;
                        string where = $"{reel.Scene}: {shot.Name}, roll {roll} at {t:F2}, {stand:F0} off";
                        farthestShown = MathF.Max(farthestShown, stand);
                        if (atNear > worstAtNear) (worstAtNear, worstAtNearWhere) = (atNear, where);

                        //A close target holds the plane under what the drain's distance alone would give it
                        bool targetHolds = near < RecoilCamera.NearPlaneAt(NEAR, float.PositiveInfinity, Vector3.Distance(lens, Subject));
                        if (targetHolds)
                        {
                            //A run's point ahead is no subject: a run that shows the drain from afar looks far ahead
                            Assert.False(shot.LooksAhead && band > BAND_SLACK,
                                $"{where}: the band is {band:F2} of the widest play view's, the plane held at {near:F3} by the point the run looks at ahead (IntroShot.FAR_LOOK)");
                            string key = $"{reel.Scene}: {shot.Name}";
                            held.TryGetValue(key, out var h);
                            held[key] = (h.Frames + 1, h.Over + (band > BAND_SLACK ? 1 : 0), MathF.Max(h.Worst, band),
                                band > BAND_SLACK ? MathF.Max(h.Farthest, stand) : h.Farthest);
                            return;
                        }

                        if (band > worst) (worst, worstWhere) = (band, where);
                        Assert.True(band <= BAND_SLACK, $"{where}: the band is {band:F2} of the widest play view's (the plane {near:F3})");
                    });

            //Where the band at the ceiling comes back to the play view's: the step there over the distance is the play view's
            float c = RecoilCamera.NEAR_PLANE_CEILING;
            float back = playBand * FAR * c * 16777216f / (FAR - c) - ArenaIsland.FUNNEL_TOP_RADIUS;

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "every frame that shows the drain: the band at most {0:F2} of the widest play view's ({1}); at the near plane 0.05 up to {2:F2} ({3})",
                worst, worstWhere, worstAtNear, worstAtNearWhere));
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "the farthest such frame stands {0:F0} from the drain; at the ceiling the band is back to the play view's at {1:F0}", farthestShown, back));
            foreach ((string key, var h) in held)
                _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  held down by its target: {0}, {1} frames, {2} over {3} (up to {4:F2}, out to {5:F0} off)", key, h.Frames, h.Over, BAND_SLACK,
                    h.Worst, h.Farthest));

            Assert.True(worstAtNear > 2f * BAND_SLACK, "no intro stands where the depth buffer fails the drain at 0.05: this test proves nothing");
            Assert.True(farthestShown < back, $"a frame shows the drain {farthestShown:F0} off, past where the ceiling's band is the play view's ({back:F0})");
        }

        /// <summary>
        /// The other side of a farther near plane: it clips what stands nearer the lens than it. Over every shot of every intro and
        /// every roll, wherever the plane is farther than <see cref="RecoilCamera.NearPlane"/>, each corner of it at the widest window
        /// (with the shot's lens and its bank) stands at least <see cref="CORNER_AIR"/> over the ground under it, off the cavern's
        /// rock or off the city's boxes and over its street. Seen to fail with the target's hold at 5 (the orbit round the hay bales)
        /// and with the ceiling at 3 (the desert's crest).
        /// </summary>
        [Fact]
        public void No_corner_of_a_farther_near_plane_reaches_the_ground_or_the_rock_under_any_intro_lens()
        {
            float leastAir = float.MaxValue, mostNear = 0f;
            string leastWhere = "", mostWhere = "";
            int checkedFrames = 0;

            for (int roll = 1; roll <= ROLLS; roll++)
                foreach (Reel reel in Reels(roll))
                {
                    if (reel.Clearance == null) continue;
                    Walk(reel, (shot, t, _, lens, target) =>
                    {
                        RecoilCamera wide = Posed(lens, target, shot.FieldOfViewAt(t), WIDEST_ASPECT, shot.RollAt(t));
                        float near = ProjectedNear(wide);
                        string where = $"{reel.Scene}: {shot.Name}, roll {roll} at {t:F2}";
                        if (near > mostNear) (mostNear, mostWhere) = (near, where);
                        if (!(near > NEAR)) return;

                        checkedFrames++;
                        Matrix world = Matrix.Invert(wide.View);
                        float half = near * MathF.Tan(wide.FieldOfView * 0.5f);
                        foreach (Vector2 corner in Corners)
                        {
                            Vector3 point = Vector3.Transform(new Vector3(corner.X * half * WIDEST_ASPECT, corner.Y * half, -near), world);
                            float air = reel.Clearance(point);
                            if (air < leastAir) (leastAir, leastWhere) = (air, where);
                            Assert.True(air >= CORNER_AIR, $"{where}: a corner of the near plane {near:F3} stands {air:F2} off what it could cut into");
                        }
                    });
                }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "the farthest near plane {0:F3} ({1}); over {2} frames with a farther plane, its corners keep at least {3:F2} of air ({4})",
                mostNear, mostWhere, checkedFrames, leastAir, leastWhere));
            Assert.True(checkedFrames > 0, "no intro draws with a farther plane: this test proves nothing");
        }

        private static readonly Vector2[] Corners = { new(-1f, -1f), new(1f, -1f), new(-1f, 1f), new(1f, 1f) };

        /// <summary>One scene's intro on one roll: its shots, the ground under them where it has a mirror (for what hides the drain),
        /// how far a point stands off what a near plane could cut into where that is known, and the wall clock a shot posed by it
        /// reads (set to the moment of every pose).</summary>
        private sealed record Reel(string Scene, IntroShot[] Shots, Func<float, float, float> Ground, Func<Vector3, float> Clearance,
            Action<float> SetClock = null, float ClockStart = 0f);

        private delegate void PoseVisit(IntroShot shot, float t, RecoilCamera camera, Vector3 lens, Vector3 target);

        //Every pose of every shot of the reel, at 16:9 with the shot's lens and bank: the wall clock, where a shot reads one, runs
        //on from the reel's start through each shot's seconds
        private static void Walk(Reel reel, PoseVisit visit)
        {
            if (reel.Shots == null) return;
            float start = reel.ClockStart;
            foreach (IntroShot shot in reel.Shots)
            {
                if (shot == null) continue;
                for (int i = 0; i < POSES; i++)
                {
                    float t = i / (POSES - 1f);
                    reel.SetClock?.Invoke(start + t * shot.Seconds);
                    shot.Pose(t, out Vector3 lens, out Vector3 target);
                    visit(shot, t, Posed(lens, target, shot.FieldOfViewAt(t), 16f / 9f, shot.RollAt(t)), lens, target);
                }

                start += shot.Seconds;
            }
        }

        //Whether the frame shows the drain: the mouth's middle in frame, its far side over the island's near rim (a lens lower than
        //DISH_DEPTH over the island's radius and the mouth's sees the stone ring and no glass), and no ground between
        private static bool ShowsTheDrain(RecoilCamera camera, Vector3 lens, Func<float, float, float> ground)
        {
            Vector4 clip = Vector4.Transform(new Vector4(Mouth, 1f), camera.View * camera.Projection);
            if (!(clip.W > 0f) || MathF.Abs(clip.X / clip.W) >= 1f || MathF.Abs(clip.Y / clip.W) >= 1f) return false;

            float across = new Vector2(lens.X, lens.Z).Length();
            float over = ArenaIsland.DISH_DEPTH * (across + ArenaIsland.FUNNEL_TOP_RADIUS) / (ArenaIsland.RADIUS + ArenaIsland.FUNNEL_TOP_RADIUS);
            if (lens.Y - Mouth.Y <= over) return false;

            if (ground != null)
            {
                //The ground's mirror stops where the island stands: the line is walked until the island's own reach of the mouth
                float distance = Vector3.Distance(lens, Mouth);
                for (float s = 0.005f; s < 1f - (ArenaIsland.RADIUS + 4f) / MathF.Max(distance, ArenaIsland.RADIUS + 5f); s += 0.005f)
                {
                    Vector3 p = Vector3.Lerp(lens, Mouth, s);
                    if (ground(p.X, p.Z) > p.Y) return false;
                }
            }

            return true;
        }

        private static Func<Vector3, float> Over(Func<float, float, float> ground) => p => p.Y - ground(p.X, p.Z);

        //Every chapter scene's intro through its real builder on the shipped config, and Mars's, the polar camp's, the sea's and
        //the outback's; roll by roll
        private static IEnumerable<Reel> Reels(int roll)
        {
            float fov = MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW;

            MeadowIntroShotsTests.Planting planting = MeadowIntroShotsTests.Plant(roll);
            Func<float, float, float> meadow = (x, z) => TerrainMirror.Meadow(x, z, planting.Config);
            yield return new Reel("meadow", MeadowIntroShots.Build(planting.Config, planting.Figures, MeadowIntroShotsTests.GamePosition,
                MeadowIntroShotsTests.GameTarget, fov, new Random(roll * 7 + 1)), meadow, Over(meadow));

            var desert = new DesertSceneConfig();
            Func<float, float, float> sand = (x, z) => TerrainMirror.Desert(x, z, desert);
            yield return new Reel("desert", DesertIntroShots.Build(desert, null, DesertLandingTests.GamePosition, DesertLandingTests.GameTarget,
                fov, new Random(roll)), sand, Over(sand));

            var mountain = new MountainSceneConfig();
            Func<float, float, float> rock = (x, z) => TerrainMirror.Mountain(x, z, mountain);
            yield return new Reel("mountains", new[] { MountainIntroShots.Range(mountain, rock, fov, new Random(roll)),
                MountainIntroShots.Pass(rock, fov, new Random(roll)), MountainIntroShots.Summit(rock, fov, new Random(roll)) }, rock, Over(rock));

            foreach (bool neon in new[] { false, true })
            {
                IntroShot[] shots = CityIntroShotsTests.Shots(neon, roll, fov, true, out City city, out _);
                yield return new Reel(neon ? "neon city" : "city", shots, null,
                    p => MathF.Min(CityIntroShotsTests.Clearance(city, p), p.Y - city.GroundY));
            }

            var volcano = new VolcanoSceneConfig();
            Func<float, float, float> cone = (x, z) => TerrainMirror.Volcano(x, z, volcano);
            Vector3 vent = new VolcanoLava(volcano, 0).VentPosition[0];
            yield return new Reel("volcano", VolcanoIntroShots.Shots(cone, vent, volcano, fov, new Random(roll)), cone, Over(cone));
            //Low draws no village or woods, and its reel opens on the volcano's wide shot where the others open on the river
            yield return new Reel("volcano at Low", VolcanoIntroShots.Shots(cone, vent, volcano, fov, new Random(roll), land: false), cone, Over(cone));

            var moon = new MoonSceneConfig();
            Func<float, float, float> regolith = (x, z) => TerrainMirror.Moon(x, z, moon.Terrain);
            yield return new Reel("moon", MoonIntroShots.Build(moon, Vector3.Normalize(new Vector3(0.5f, 0.3f, -0.6f)), new Random(roll)),
                regolith, Over(regolith));

            //Nothing out here has a surface but the island and the saucer, which crosses a few dozen units off the lens
            yield return new Reel("space", SpaceIntroShots.Build(new SpaceSceneConfig(), null, () => 0f, new Random(roll)), null, null);

            //The solids drift on the wall clock; the shots keep DreamIntroShots.SOLID_MARGIN off them (DreamIntroShotsTests)
            yield return new Reel("dream", DreamIntroShots.Shots(new DreamShapesConfig(), 13f * roll, fov, new Random(roll)), null, null);

            var chamber = new CavernChamber(new CavernSceneConfig(), roll);
            yield return new Reel("cavern", CavernIntroShots.Build(chamber, fov, new Random(roll)), null, chamber.Clearance);

            //The stands' rake round the ring, but for the performers' entrance through them, which is the floor
            var circus = new CircusSceneConfig();
            float entrance = MathHelper.ToRadians(circus.Seating.EntranceBearingDegrees);
            Vector2 outward = new(MathF.Cos(entrance), MathF.Sin(entrance));
            Func<float, float, float> stands = (x, z) =>
            {
                var at = new Vector2(x, z);
                bool gap = Vector2.Dot(at, outward) > 0f && MathF.Abs(outward.X * z - outward.Y * x) < circus.Seating.EntranceWidth * 0.5f;
                return at.Length() >= circus.Seating.InnerRadius && !gap ? CircusIntroShots.SeatY(circus, at.Length()) : circus.Ring.FloorY;
            };
            yield return new Reel("big top", CircusIntroShots.Build(circus, CircusLandingTests.GamePosition, CircusLandingTests.GameTarget, fov,
                new Random(roll)), null, Over(stands));

            SavannaLandingTests.Plain plain = SavannaLandingTests.Planted(roll % 3);
            Func<float, float, float> grass = (x, z) => TerrainMirror.Savanna(x, z, plain.Config);
            yield return new Reel("savanna", SavannaIntroShots.Build(plain.Land, SavannaLandingTests.GamePosition, SavannaLandingTests.GameTarget,
                fov, new Random(roll)), grass, Over(grass));

            float clock = 0f, built = 3.7f + roll * 29.3f;
            GridSceneConfig grid = GridRideTests.Grid;
            yield return new Reel("grid", GridIntroShots.Shots(grid, GridRideTests.Solids(roll), GridRideTests.RingOf(grid), built, () => clock,
                new Random(roll)), null, p => p.Y - grid.Terrain.LevelY, now => clock = now, built);

            yield return new Reel("aurora", AuroraIntroShots.Build(Aurora.Planting, Aurora.Config, Aurora.Ground, fov, new Random(roll)),
                Aurora.Ground, Over(Aurora.Ground));

            var mars = new MarsSceneConfig();
            Func<float, float, float> marsCeiling = (x, z) => OffworldGround.MarsCeiling(x, z, mars.Terrain);
            yield return new Reel("mars", MarsIntroShots.Build(mars, true, new Random(roll)), marsCeiling, Over(marsCeiling));

            var polar = new PolarSceneConfig();
            Func<float, float, float> ice = (x, z) => TerrainMirror.Polar(x, z, polar);
            yield return new Reel("polar", PolarIntroShots.Build(polar, fov, new Random(roll)), ice, Over(ice));

            //The water is bounded, not mirrored: no crest stands over SeaIntroShots.CrestHeight, and past CalmFrom it is flat
            var sea = new SeaSceneConfig();
            float crest = SeaIntroShots.CrestHeight(sea);
            yield return new Reel("sea", SeaIntroShots.Shots(sea, Vector3.Normalize(new Vector3(0.69f, 0.22f, -0.69f)), fov, new Random(roll)),
                null, p => p.Y < sea.LevelY ? float.PositiveInfinity
                    : p.Y - sea.LevelY - (new Vector2(p.X, p.Z).Length() >= sea.CalmFrom ? 0f : crest));

            var outback = new OutbackSceneConfig();
            Func<float, float, float> red = (x, z) => TerrainMirror.Outback(x, z, outback);
            TerrainMirror.Formation monolith = OutbackIntroShots.Monolith(outback) ?? throw new InvalidOperationException("no monolith");
            yield return new Reel("outback", new[] { OutbackIntroShots.Plain(monolith, red, fov, new Random(roll)),
                OutbackIntroShots.Approach(outback, monolith, red, fov, new Random(roll)),
                OutbackIntroShots.Crown(outback, monolith, red, fov, new Random(roll)) }, red, Over(red));
        }

        //The aurora's wood as the game plants it, once: the snow under it is the mound map baked off that planting
        private static class Aurora
        {
            internal static readonly AuroraSceneConfig Config = new();
            internal static readonly ForestScatter Planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED, Config.Terrain,
                (x, z) => TerrainMirror.AuroraBase(x, z, Config));
            private static readonly AuroraMoundMap Mounds = AuroraMoundMap.Bake(Planting, Config);
            internal static float Ground(float x, float z) => TerrainMirror.Aurora(x, z, Config, Mounds);
        }
    }
}
