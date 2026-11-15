using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The city's chapter-intro prologue (#488, second round): the owner's playtest — "a quick cut that looks at the
    /// square, and straight after it the square again from a very slightly different angle; it looks like a glitch.
    /// Shots should last a while and be different from one another" — held as two rules over the real builder on both
    /// cities and a dozen rolls: every shot runs long enough to be looked at, and at every hard cut the view changes
    /// enough that it cannot read as the same picture nudged. What the eye makes of a cut in motion is the owner's to
    /// judge; what is checked here is that the two things the note names are not what the builder produces. Since #840
    /// the prologue is built over the city's real dressed roofs, so the rooftop's arc is in every one of these, and the
    /// rooftop shot is held to keep its whole mast in frame.
    /// </summary>
    public class CityIntroShotsTests
    {
        private readonly ITestOutputHelper _output;

        public CityIntroShotsTests(ITestOutputHelper output) => _output = output;

        private const int ROLLS = 12;
        private static readonly float FieldOfView = MathHelper.ToRadians(45f);

        //Long enough to be read, not a flash: the first round's shots ran 3.0-3.6 s and were the note's "quick cut"
        private const float MIN_SHOT_SECONDS = 3.9f;

        //Across a cut the lens must change — where it stands or where it looks. Either is enough on its own, but not
        //neither: a plaza shot cut to another plaza shot (or two canyon views along parallel streets) reads as the
        //same picture from a slightly different angle.
        private const float MIN_CUT_ANGLE_DEGREES = 22f;
        private const float MIN_CUT_DISTANCE = 40f;

        //The rooftop's frame (#840): the vertical field of view the game hands the intro (GameplayScreen.GAME_FOV, which
        //the shot widens), and the window shapes the owner plays at and the release is looked at in. The projection is
        //Hor+, so the shape changes the width alone - the mast is vertical, but the beacon is checked across too.
        private const float INTRO_FIELD_OF_VIEW = MathF.PI / 4.2f;
        private static readonly (int Width, int Height)[] Screens = { (3840, 1600), (1920, 1080), (1600, 900) };

        //How far inside every edge the mast's foot and the whole beacon must stay, as a share of the frame: inside with
        //room to read as the mast's tip, not grazing the edge. A little under the builder's own margin, which it holds
        //at the path's points; the lens runs a spline through them.
        private const float FRAME_MARGIN = 0.04f;

        //And the foot over the bottom edge by more than the intro's skip hint reaches up it (12.2 % of the frame's height,
        //measured on the captures at 1600 and 900 tall), which stands centred there - where the mast is - over the shot
        private const float FOOT_CLEAR = 0.13f;

        //Each candidate roof's arc rolled this many times: the start and the direction are rolled
        private const int ARC_ROLLS = 4;

        //How finely the shot is walked, poses per shot
        private const int POSES = 120;

        private static City Build(bool neon, int seed) => Build(neon, seed, out _);

        //The city and its dressed roofs, each offset by the seed the way the game adds its scene seed to both
        private static City Build(bool neon, int seed, out CityRooftops.RoofShowcases rooftops)
        {
            var config = new CitySceneConfig();
            config.Seed += seed;
            config.NeonLayout.Seed += seed;

            var city = new City(config, neon, ArenaIsland.RADIUS);
            rooftops = CityRooftops.ArrangeShowcases(city, config, CityRooftops.DEFAULT_SEED + seed);
            return city;
        }

        //The whole prologue as the game builds it, the rooftop's arc round a real dressed roof included, handing the camera
        //back to a gameplay pose on a bearing the seed turns (the lens about 40 out and level with the gun, GameCameraFit)
        private static IntroShot[] Shots(bool neon, int seed, out City city)
        {
            city = Build(neon, seed, out CityRooftops.RoofShowcases rooftops);
            GamePose(seed, out Vector3 position, out Vector3 target);
            return CityIntroShots.Build(city, rooftops, neon, position, target, FieldOfView, new Random(seed));
        }

        internal static void GamePose(int seed, out Vector3 position, out Vector3 target)
        {
            float bearing = seed * 0.83f;
            position = new Vector3(MathF.Cos(bearing) * 40f, -4f, MathF.Sin(bearing) * 40f);
            target = new Vector3(0f, 3.5f, 0f);
        }

        [Fact]
        public void EveryShotRunsLongEnoughToBeLookedAt()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (bool neon in new[] { false, true })
                {
                    IntroShot[] shots = Shots(neon, seed, out _);

                    Assert.True(shots.Length >= 3, $"seed {seed}, neon {neon}: only {shots.Length} shots");
                    foreach (IntroShot shot in shots)
                        Assert.True(shot.Seconds >= MIN_SHOT_SECONDS, $"seed {seed}, neon {neon}: '{shot.Name}' holds {shot.Seconds} s");
                }
        }

        [Fact]
        public void NoCutLandsOnTheSamePictureNudged()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (bool neon in new[] { false, true })
                {
                    IntroShot[] shots = Shots(neon, seed, out _);

                    for (int i = 0; i + 1 < shots.Length; i++)
                    {
                        shots[i].Pose(1f, out Vector3 fromPosition, out Vector3 fromTarget);
                        shots[i + 1].Pose(0f, out Vector3 toPosition, out Vector3 toTarget);

                        Vector3 before = Vector3.Normalize(fromTarget - fromPosition);
                        Vector3 after = Vector3.Normalize(toTarget - toPosition);
                        float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(before, after), -1f, 1f)));
                        float distance = Vector3.Distance(fromPosition, toPosition);

                        Assert.True(angle >= MIN_CUT_ANGLE_DEGREES || distance >= MIN_CUT_DISTANCE,
                            $"seed {seed}, neon {neon}: '{shots[i].Name}' -> '{shots[i + 1].Name}' cuts to a view {angle:F0} degrees and {distance:F0} units from the last");
                    }
                }
        }

        [Fact]
        public void NoShotEverPassesThroughATower()
        {
            //The orbit and the sway leave the centre lines the first round's paths ran down, so the claim that "any height over
            //a street's centre line is clear" no longer covers every point of every shot: each is now walked, sixty samples a
            //shot, against the real buildings' boxes (axis-aligned, scale then translate — City's own note), with a unit's margin
            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (bool neon in new[] { false, true })
                {
                    IntroShot[] shots = Shots(neon, seed, out City city);

                    foreach (IntroShot shot in shots)
                        for (int step = 0; step <= 60; step++)
                        {
                            shot.Pose(step / 60f, out Vector3 position, out _);

                            foreach (ModelInstance building in city.Buildings)
                            {
                                Matrix world = building.World;
                                Vector3 half = new Vector3(world.M11, world.M22, world.M33) * 0.5f + new Vector3(1f);
                                Vector3 offset = position - new Vector3(world.M41, world.M42, world.M43);

                                bool inside = MathF.Abs(offset.X) < half.X && MathF.Abs(offset.Y) < half.Y && MathF.Abs(offset.Z) < half.Z;
                                Assert.False(inside, $"seed {seed}, neon {neon}: '{shot.Name}' at {step}/60 stands inside a tower at {position}");
                            }
                        }
                }
        }

        [Fact]
        public void NoTwoShotsInARowAreTheSameKindOfShot()
        {
            //The alternation itself: never the swing beside the street it is a variation of, whichever swing the reel flies -
            //the old one-corner swing or the swing home (#972), which lands on the gameplay pose and so is always last
            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (bool neon in new[] { false, true })
                {
                    IntroShot[] shots = Shots(neon, seed, out _);

                    for (int i = 0; i + 1 < shots.Length; i++)
                        Assert.NotEqual(shots[i].Name, shots[i + 1].Name);

                    int street = Array.FindIndex(shots, s => s.Name == "the street");
                    int swing = Array.FindIndex(shots, s => s.Name == "the swing" || s.Name == "the swing home");
                    Assert.True(swing >= 0, $"seed {seed}, neon {neon}: the reel has no swing");
                    Assert.True(Math.Abs(swing - street) >= 2, $"seed {seed}, neon {neon}: the swing stands next to the street");

                    int lands = Array.FindIndex(shots, s => s.Lands);
                    if (lands >= 0) Assert.Equal(shots.Length - 1, lands);

                    //And since #1119, three canyon shots in one reel: the street, the facades and the swing, none beside another
                    for (int i = 0; i + 1 < shots.Length; i++)
                        Assert.False(Canyon(shots[i]) && Canyon(shots[i + 1]),
                            $"seed {seed}, neon {neon}: '{shots[i].Name}' is cut straight to '{shots[i + 1].Name}', two canyon shots");
                }
        }

        private static bool Canyon(IntroShot shot) => shot.Name is "the street" or "the facades" or "the swing" or "the swing home";

        //The facades (#1119): how far every lens stays off every box, towers and cornices (CityIntroShots' own clearance), and
        //the band the point it looks at keeps from the lens: a pane mirrors the towers at full strength to two thirds of the
        //reach the Game draws at (CityReflection.fxh fades the last third), and closer than the near end the wall is too
        //close for a look up it
        private const float FACADES_CLEARANCE = 2f;
        private const float FACADES_NEAREST = 10f;
        private const float FACADES_FARTHEST = InstancedModelRenderer.DEFAULT_CITY_REFLECTION_REACH * 2f / 3f;

        //How far over the horizon the frame's bottom edge looks: above it no ray of the frame can meet the street, whose paint is
        //flat (#783, #973). A little under the builder's own margin, which it holds at the path's points
        private const float FACADES_STREET_MARGIN_DEGREES = 2.5f;

        //How many of a shot's poses the pane at the frame's centre must mirror a tower from (the builder asks it of its seven stands)
        private const float FACADES_MIRRORED_SHARE = 0.9f;

        /// <summary>
        /// #1119, the owner: "shots are missing from below (almost street level) looking up at the tall buildings, where the
        /// reflections in the windows will be clearly visible". Both cities, a dozen rolls each, the facades walked pose by pose
        /// against the city's real boxes: every reel has them, the day city's after its plaza and the neon city's first; the lens
        /// keeps its clearance off every box; the wall it looks at is met by the view's centre inside the reflection band; the
        /// frame's bottom edge looks over the horizon, so the street's flat paint is never in the picture; and the pane at the
        /// frame's centre mirrors a tower, not the sky, the reflection the shot is for.
        /// </summary>
        [Fact]
        public void TheFacadesLookUpAWallCloseEnoughForItsPanesToMirrorTheTowers()
        {
            int poses = 0, mirrored = 0;
            float nearest = float.MaxValue, farthest = 0f, lowestEdge = float.MaxValue, tightest = float.MaxValue;

            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (bool neon in new[] { false, true })
                {
                    IntroShot[] shots = Shots(neon, seed, out City city);
                    int at = Array.FindIndex(shots, s => s.Name == "the facades");
                    Assert.True(at >= 0, $"seed {seed}, neon {neon}: the reel has no facades");
                    Assert.Equal(neon ? 0 : 2, at);
                    Assert.Equal("the plaza", shots[neon ? at + 1 : at - 1].Name);

                    IntroShot facades = shots[at];
                    int shotMirrored = 0;
                    for (int step = 0; step <= POSES; step++)
                    {
                        facades.Pose(step / (float)POSES, out Vector3 position, out Vector3 target);
                        Vector3 look = Vector3.Normalize(target - position);
                        string where = $"seed {seed}, neon {neon}, at {step}/{POSES}";

                        float clearance = Clearance(city, position);
                        tightest = MathF.Min(tightest, clearance);
                        Assert.True(clearance >= FACADES_CLEARANCE, $"{where}: the lens stands {clearance:F2} off a box");

                        float wall = FirstHit(city, position, look, 500f, out Vector3 normal);
                        nearest = MathF.Min(nearest, wall);
                        farthest = MathF.Max(farthest, wall);
                        Assert.True(wall >= FACADES_NEAREST && wall <= FACADES_FARTHEST,
                            $"{where}: the view's centre meets a wall {wall:F1} from the lens, outside {FACADES_NEAREST}-{FACADES_FARTHEST:F1}");

                        float edge = MathHelper.ToDegrees(MathF.Asin(look.Y) - facades.FieldOfView * 0.5f);
                        lowestEdge = MathF.Min(lowestEdge, edge);
                        Assert.True(edge >= FACADES_STREET_MARGIN_DEGREES,
                            $"{where}: the frame's bottom edge looks {edge:F1} degrees over the horizon, the street in frame");

                        Vector3 pane = position + look * wall;
                        Vector3 mirror = look - 2f * Vector3.Dot(look, normal) * normal;
                        if (FirstHit(city, pane + normal * 0.05f, mirror, 80f, out _) < 80f) shotMirrored++;
                    }

                    poses += POSES + 1;
                    mirrored += shotMirrored;
                    Assert.True(shotMirrored >= FACADES_MIRRORED_SHARE * (POSES + 1),
                        $"seed {seed}, neon {neon}: the pane at the frame's centre mirrors a tower from {shotMirrored} of {POSES + 1} poses");
                }

            //Not vacuous; and the figures the documents quote
            Assert.True(poses >= 2 * ROLLS * POSES, $"only {poses} poses were walked");
            _output.WriteLine($"the wall at {nearest:F1}-{farthest:F1}, the bottom edge at least {lowestEdge:F1} degrees up, the lens at "
                + $"least {tightest:F1} off a box, {mirrored} of {poses} centre panes mirroring a tower");
        }

        //How far a point stands from the nearest of the city's boxes, towers and cornices: 0 inside one
        private static float Clearance(City city, Vector3 point)
        {
            float nearest = float.MaxValue;
            foreach (ModelInstance building in city.Buildings)
            {
                Matrix world = building.World;
                Vector3 half = new Vector3(MathF.Abs(world.M11), MathF.Abs(world.M22), MathF.Abs(world.M33)) * 0.5f;
                Vector3 offset = point - new Vector3(world.M41, world.M42, world.M43);
                Vector3 outside = Vector3.Max(Vector3.Zero, new Vector3(MathF.Abs(offset.X), MathF.Abs(offset.Y), MathF.Abs(offset.Z)) - half);
                nearest = MathF.Min(nearest, outside.Length());
            }

            return nearest;
        }

        //How far along `direction` (a unit vector) a ray from `origin` first meets one of the city's boxes, and the face it meets
        //it on; infinity within `reach` of nothing. The boxes are axis-aligned, scale then translate (City's own note).
        private static float FirstHit(City city, Vector3 origin, Vector3 direction, float reach, out Vector3 normal)
        {
            float nearest = float.PositiveInfinity;
            normal = Vector3.Zero;

            foreach (ModelInstance building in city.Buildings)
            {
                Matrix world = building.World;
                Vector3 centre = new(world.M41, world.M42, world.M43);
                Vector3 half = new Vector3(MathF.Abs(world.M11), MathF.Abs(world.M22), MathF.Abs(world.M33)) * 0.5f;
                float enter = 0f, exit = MathF.Min(reach, nearest);
                int axis = -1;
                bool through = true;

                for (int a = 0; a < 3 && through; a++)
                {
                    float o = a == 0 ? origin.X : a == 1 ? origin.Y : origin.Z;
                    float d = a == 0 ? direction.X : a == 1 ? direction.Y : direction.Z;
                    float c = a == 0 ? centre.X : a == 1 ? centre.Y : centre.Z;
                    float h = a == 0 ? half.X : a == 1 ? half.Y : half.Z;

                    if (MathF.Abs(d) < 1e-6f) { through = MathF.Abs(o - c) <= h; continue; }

                    float t0 = (c - h - o) / d, t1 = (c + h - o) / d;
                    if (t0 > t1) (t0, t1) = (t1, t0);
                    if (t0 > enter) { enter = t0; axis = a; }
                    exit = MathF.Min(exit, t1);
                    through = enter <= exit;
                }

                if (!through || axis < 0) continue;

                nearest = enter;
                float sign = (axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z) > 0f ? -1f : 1f;
                normal = axis == 0 ? new Vector3(sign, 0f, 0f) : axis == 1 ? new Vector3(0f, sign, 0f) : new Vector3(0f, 0f, sign);
            }

            return nearest;
        }

        /// <summary>
        /// #840: "looking at the roof at 3840×1600, the red blinking tip of the mast is not visible" — the rooftop shot looked
        /// at the mast's lower third from a fixed height, and a tall mast's top ran off the top edge. Every candidate roof
        /// the shot may take, on both cities and a dozen rolls, its arc rolled several times, walked pose by pose through
        /// the shot's own lens at three window shapes: the mast's foot and the whole beacon (its middle, and its radius up
        /// the screen and up the world) stay inside every edge of the frame from the first degree of the arc to the last,
        /// and the foot over the skip hint at the bottom of it.
        /// </summary>
        [Fact]
        public void TheRooftopShotKeepsTheWholeMastAndItsBeaconInFrame()
        {
            int arcs = 0, tall = 0, failures = 0;
            float worst = 0f;
            string example = null;
            float limit = 1f - 2f * FRAME_MARGIN;

            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (bool neon in new[] { false, true })
                {
                    City city = Build(neon, seed, out CityRooftops.RoofShowcases rooftops);
                    CityRooftops.RoofShowcase[] candidates = CityIntroShots.RooftopCandidates(city, rooftops, neon);

                    for (int c = 0; c < candidates.Length; c++)
                    {
                        CityRooftops.RoofShowcase roof = candidates[c];
                        if (roof.MastHeight <= 0f) continue;

                        for (int roll = 0; roll < ARC_ROLLS; roll++)
                        {
                            IntroShot shot = CityIntroShots.RooftopArc(city, roof, INTRO_FIELD_OF_VIEW,
                                new Random(seed * 1000 + c * 10 + roll));
                            if (shot == null) continue;   //a tower stands in the arc: the builder passes the roof over

                            arcs++;
                            if (roof.MastHeight >= 14f) tall++;
                            bool failed = false;

                            foreach ((int width, int height) in Screens)
                            {
                                Matrix projection = Matrix.CreatePerspectiveFieldOfView(shot.FieldOfView, width / (float)height, 0.1f, 5000f);

                                for (int step = 0; step <= POSES; step++)
                                {
                                    shot.Pose(step / (float)POSES, out Vector3 position, out Vector3 target);
                                    Matrix viewProjection = Matrix.CreateLookAt(position, target, Vector3.Up) * projection;

                                    //The lens's own up, as RecoilCamera builds it: the beacon's silhouette reaches its radius
                                    //up the screen, not only up the world
                                    Vector3 forward = Vector3.Normalize(target - position);
                                    Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
                                    Vector3 up = Vector3.Cross(right, forward);

                                    Vector3 top = roof.MastTop;
                                    float r = roof.BeaconRadius;
                                    Vector3[] points =
                                    {
                                        roof.MastFoot, top, top + Vector3.Up * r, top + up * r, top - up * r, top + right * r, top - right * r,
                                    };

                                    for (int p = 0; p < points.Length; p++)
                                    {
                                        Vector4 clip = Vector4.Transform(new Vector4(points[p], 1f), viewProjection);
                                        float reach = clip.W <= 0f ? float.PositiveInfinity
                                            : MathF.Max(MathF.Abs(clip.X / clip.W), MathF.Abs(clip.Y / clip.W));

                                        //The foot (the first point) is measured against the hint's top instead of the bottom edge:
                                        //how far below the middle it stands, in the units the edges are measured in
                                        if (p == 0 && clip.W > 0f)
                                            reach = MathF.Max(reach, -clip.Y / clip.W * limit / (1f - 2f * FOOT_CLEAR));
                                        if (reach <= limit) continue;

                                        failed = true;
                                        if (reach > worst)
                                        {
                                            worst = reach;
                                            example = $"seed {seed}, neon {neon}, roof {c} (mast {roof.MastHeight:F1}, half span {roof.HalfSpan:F1}), "
                                                + $"roll {roll}, {width}x{height}, at {step}/{POSES}: "
                                                + (p == 0 ? "the foot" : "the beacon") + $" at {reach:F2} of the half frame";
                                        }
                                    }
                                }
                            }

                            if (failed) failures++;
                        }
                    }
                }

            //Not vacuous: enough arcs were walked, and tall masts among them - the ones #840's capture lost
            Assert.True(arcs >= 100, $"only {arcs} arcs were walked");
            Assert.True(tall >= 20, $"only {tall} arcs went round a mast of 14 or more");
            Assert.True(failures == 0, $"{failures} of {arcs} arcs put the beacon within {FRAME_MARGIN:P0} of the frame's edge or past it, "
                + $"or the foot under the skip hint; the worst: {example}");
        }
    }
}
