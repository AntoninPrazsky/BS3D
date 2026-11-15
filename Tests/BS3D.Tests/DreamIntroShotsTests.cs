using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The dream's prologue against its solids (#1126): the solids grew 1.4 times and went from eight to twelve, and they
    /// write no depth, so a lens inside a solid's bound marches from the lens and a solid between the lens and the island
    /// is drawn behind the island. Held over the real builder (<see cref="DreamIntroShots"/>) on the shipped config, at
    /// many rolls and many wall-clock starts: the reel still builds its three shots, and no lens of any shot, flown on the
    /// shot's own eased clock at the second it plays, comes inside a solid's bound. And the solids themselves stay out past
    /// every lens the game stands at.
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

        //How far out the solids' bounds stay from the arena's axis: past the game's lens (28 to 45 out), the front end's
        //orbit (about 44) and the tour's last leg into the pose (1.25 to 1.45 of the lens's stand-off, so up to 65 at the
        //widest field, and lower than most solids). It held at 44.6 before #1126 (the bounds were 30.4 for every solid)
        //and the surfaces at 59.9; at 1.4 times the size with the orbit lifted, the bounds at 61.3.
        private const float BOUND_KEEP_OUT = 60f;

        [Fact]
        public void EveryRollBuildsTheThreeShotsWithNoLensInsideASolid()
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
                                Assert.True(clearance > 0f,
                                    $"seed {seed} at {start} s: \"{shot.Name}\" puts the lens {clearance:F1} inside solid {solid}'s bound at {s:F2} of the shot");
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
        public void TheSolidsStayOutsideEveryLensTheGameStandsAt()
        {
            var shapes = new DreamShapesConfig();

            for (int solid = 0; solid < SceneRenderer.DREAM_SOLID_COUNT; solid++)
                for (float t = 0f; t < 1200f; t += 3.7f)
                {
                    Vector3 centre = SceneRenderer.DreamSolidCenter(shapes, solid, t, out float bound);
                    float edge = new Vector2(centre.X, centre.Z).Length() - bound;
                    Assert.True(edge >= BOUND_KEEP_OUT, $"solid {solid}'s bound comes {edge:F1} from the axis at {t} s");
                }
        }
    }
}
