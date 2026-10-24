using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;

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
            //The alternation itself: street, plaza, swing, roofs — never the swing beside the street it is a variation of
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = Shots(false, seed, out _);

                for (int i = 0; i + 1 < shots.Length; i++)
                    Assert.NotEqual(shots[i].Name, shots[i + 1].Name);

                int street = Array.FindIndex(shots, s => s.Name == "the street");
                int swing = Array.FindIndex(shots, s => s.Name == "the swing");
                if (swing >= 0) Assert.True(swing - street >= 2, $"seed {seed}: the swing stands next to the street");
            }
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
