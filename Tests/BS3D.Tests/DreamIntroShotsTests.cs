using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The dream's intro against its solids (#1126): the solids grew 1.4 times and went from eight to twelve, and they
    /// write no depth, so a lens inside a solid's bound marches from the lens and a solid between the lens and the island
    /// is drawn behind the island. Held over the real builder (<see cref="DreamIntroShots"/>) on the shipped config, at
    /// many rolls and many wall-clock starts: the reel still builds its three shots, and every lens of every shot, flown on
    /// the shot's own eased clock at the second it plays, keeps the builder's margin off every solid's bound. The tour's
    /// last leg into the pose, which no builder checks, is flown through the real <see cref="ChapterIntro"/> after the
    /// reel and never enters a bound. And the bounds stay out past the game's lens and the front end's orbit.
    /// </summary>
    public class DreamIntroShotsTests
    {
        private readonly ITestOutputHelper _output;

        public DreamIntroShotsTests(ITestOutputHelper output) => _output = output;

        //The game's vertical field of view (GameplayScreen.GAME_FOV), which each shot widens from
        private const float GAME_FOV = MathF.PI / 4.2f;

        //Rolls of the intro's own random, each at every start on the wall clock: an intro begins when a level does, any
        //second of a session
        private const int ROLLS = 40;
        private const float START_STEP = 37f, LAST_START = 3600f;

        //Frames read along each shot
        private const int FRAMES = 60;

        //How much of the builder's margin (DreamIntroShots.SOLID_MARGIN, kept at the points of each path it checks) the
        //eased lens between those points may spend: the nearest measured is 9.9 off a bound against the margin's 10. A
        //lens no check placed (the sky's fixed fallback, a shot added unchecked) has no margin to keep.
        private const float MARGIN_SLACK = 1f;

        //How far out the solids' bounds stay from the arena's axis, at every height: past the game's lens (28 to 45 out)
        //and the front end's orbit (about 44). Every bound of the inner ring comes to the ring's edge, 61.1 since the rings
        //of #1219 (DreamBackdrop.SOLID_INNER_EDGE). The tour's last leg reaches further out than the lens (54 from the axis
        //at the farthest over the sweep below) and is flown against the bounds themselves.
        private const float BOUND_KEEP_OUT = 60f;

        //The game's lens, as the landing tests stand it: 28 to 45 out, a little under the gun, the look on the arena
        private static readonly float[] Radii = { 29f, 37f, 45f };
        private const float POSE_Y = -8.2f;
        private static readonly Vector3 GameTarget = new(0f, -1.2f, 0f);
        private const int BEARINGS = 12;
        private const float TOUR_START_STEP = 211f;

        [Fact]
        public void EveryRollBuildsTheThreeShotsWithEveryLensClearOfTheSolids()
        {
            var shapes = new DreamShapesConfig();
            int reels = 0, full = 0, withGlass = 0, withUnder = 0;
            float nearest = float.MaxValue;

            for (float start = 0f; start <= LAST_START; start += START_STEP)
                for (int seed = 0; seed < ROLLS; seed++)
                {
                    IntroShot[] shots = DreamIntroShots.Shots(shapes, start, GAME_FOV, new Random(seed));
                    Assert.NotNull(shots);
                    reels++;
                    if (shots.Length == 3) full++;
                    if (Array.Exists(shots, s => s.Name == "the glass")) withGlass++;
                    if (Array.Exists(shots, s => s.Name == "the underside")) withUnder++;

                    //Each shot plays from the second the shots before it end, and the lens is where its own eased clock puts it
                    float playing = start;
                    foreach (IntroShot shot in shots)
                    {
                        for (int f = 0; f <= FRAMES; f++)
                        {
                            float s = f / (float)FRAMES;
                            shot.Pose(s, out Vector3 lens, out Vector3 look);
                            float t = playing + s * shot.Seconds;

                            for (int solid = 0; solid < SceneRenderer.DREAM_SOLID_COUNT; solid++)
                            {
                                Vector3 centre = SceneRenderer.DreamSolidCenter(shapes, solid, t, out float bound);
                                float clearance = Vector3.Distance(lens, centre) - bound;
                                nearest = MathF.Min(nearest, clearance);
                                Assert.True(clearance >= DreamIntroShots.SOLID_MARGIN - MARGIN_SLACK,
                                    $"seed {seed} at {start} s: \"{shot.Name}\" puts the lens {clearance:F1} off solid {solid}'s bound at {s:F2} of the shot");
                            }
                        }

                        playing += shot.Seconds;
                    }
                }

            _output.WriteLine($"{reels} reels: {full} with all three shots, {withGlass} with the glass, {withUnder} with the underside; nearest lens {nearest:F1} off a bound");

            //Every reel is built whole, as it was with the eight smaller solids before #1126 (3,920 of 3,920 there too)
            Assert.Equal(reels, full);
        }

        [Fact]
        public void TheTourIntoThePoseNeverEntersASolid()
        {
            //The reel cuts to the tour's last leg: from the map key (1.25 to 1.45 of the stand-off out, 38 to 48 degrees up)
            //into the game's pose. Flown frame by frame through the real intro, at the second of the wall clock each frame
            //plays (no viewpoint is handed in: the leg's azimuths swing with the gun's bearing, which the sweep covers)
            var shapes = new DreamShapesConfig();
            const float FRAME = 1f / 60f;
            int intros = 0, legFrames = 0;
            float nearest = float.MaxValue, farthest = 0f;

            foreach (float radius in Radii)
                for (int bearing = 0; bearing < BEARINGS; bearing++)
                    for (float start = 0f; start <= LAST_START; start += TOUR_START_STEP)
                    {
                        float angle = MathHelper.TwoPi * bearing / BEARINGS;
                        var pose = new Vector3(MathF.Cos(angle) * radius, POSE_Y, MathF.Sin(angle) * radius);
                        var random = new Random(intros);
                        IntroShot[] reel = DreamIntroShots.Shots(shapes, start, GAME_FOV, random);

                        float reelSeconds = 0f;
                        foreach (IntroShot shot in reel) reelSeconds += shot.Seconds;

                        var intro = new ChapterIntro();
                        intro.Begin(GameTarget, radius, GAME_FOV, pose, GameTarget, null, random, reel);
                        intros++;

                        for (int frame = 0; intro.Running; frame++)
                        {
                            Assert.True(frame < 60 * 60, "the intro never ended");
                            intro.Update(FRAME);

                            //The reel's own lenses are held to the builder's margin above; this is the leg after it
                            if (!intro.Running || intro.Elapsed < reelSeconds) continue;

                            float t = start + intro.Elapsed;
                            legFrames++;
                            farthest = MathF.Max(farthest, new Vector2(intro.Position.X, intro.Position.Z).Length());

                            for (int solid = 0; solid < SceneRenderer.DREAM_SOLID_COUNT; solid++)
                            {
                                Vector3 centre = SceneRenderer.DreamSolidCenter(shapes, solid, t, out float bound);
                                float clearance = Vector3.Distance(intro.Position, centre) - bound;
                                nearest = MathF.Min(nearest, clearance);
                                Assert.True(clearance > 0f,
                                    $"pose {radius} out at bearing {bearing}, start {start} s: the lens is {-clearance:F1} inside solid {solid}'s bound {intro.Elapsed:F2} s in");
                            }
                        }
                    }

            _output.WriteLine($"{intros} intros, {legFrames} frames of the leg: nearest lens {nearest:F1} off a bound, the farthest {farthest:F1} from the axis");

            //The leg was flown at all: 4.5 s of it (ChapterIntro.ARRIVAL_SECONDS) an intro, at 60 frames a second
            Assert.True(legFrames >= intros * (int)(ChapterIntro.ARRIVAL_SECONDS * 50f), $"only {legFrames} frames of the leg over {intros} intros");
        }

        [Fact]
        public void TheSolidsBoundsStayPastTheGamesLensAndTheMenusOrbit()
        {
            //A solid's distance from the axis is its orbit's, which the clock does not change: only its bearing and its
            //height move
            var shapes = new DreamShapesConfig();

            for (int solid = 0; solid < SceneRenderer.DREAM_SOLID_COUNT; solid++)
            {
                Vector3 centre = SceneRenderer.DreamSolidCenter(shapes, solid, 0f, out float bound);
                float edge = new Vector2(centre.X, centre.Z).Length() - bound;
                Assert.True(edge >= BOUND_KEEP_OUT, $"solid {solid}'s bound comes {edge:F1} from the axis");
            }
        }
    }
}
