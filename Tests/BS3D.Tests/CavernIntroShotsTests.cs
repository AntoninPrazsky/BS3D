using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The cavern's prologue alternates its kinds of picture (#1160, the owner: "the first shot of the animation is the ride out
    /// of the cave's tunnel - that looks good. Then comes immediately a shot from a similar angle/position onto a crystal. That
    /// should be the third shot, so the kinds of shot alternate"). Held over the real builder and real halls: the order, and
    /// what makes it matter, that the passage and the crystals are both level glides low over the lake and the shaft played
    /// between them looks up its beam. ⚠ The cut rule the cities and the volcano hold (a turn of the view or a move of the
    /// lens) does not tell the two glides apart: cut together, they stood some 350 units apart and faced nearly opposite ways,
    /// and still read as one shot, both a few units over the water looking level across it at a cluster and the island. The
    /// clearances are <see cref="CavernChamberTests"/>'.
    /// </summary>
    public class CavernIntroShotsTests
    {
        private static readonly int[] Seeds = { 0, 1, 7, 42 };
        private const int ROLLS = 6;

        //A level glide low over the lake: the lens at most this high over the water and the view within this many degrees of
        //level, at every pose of the shot; and a look up, the view at least this many degrees over level at every pose
        private const float GLIDE_HEIGHT = 20f, GLIDE_PITCH_DEGREES = 18f, LOOK_UP_DEGREES = 20f;

        private const int POSES = 60;

        private readonly ITestOutputHelper _output;

        public CavernIntroShotsTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void The_reel_is_the_passage_the_shaft_the_crystals_and_the_underside()
        {
            foreach (int seed in Seeds)
            {
                var chamber = new CavernChamber(new CavernSceneConfig(), seed);
                for (int roll = 0; roll < ROLLS; roll++)
                {
                    string[] names = CavernIntroShots.Build(chamber, MathHelper.ToRadians(45f), new Random(seed * 31 + roll)).Select(s => s.Name).ToArray();
                    Assert.Contains(names[0], new[] { "the passage", "the hall" });
                    Assert.Equal(new[] { "the shaft", "the crystals", "the underside" }, names.Skip(1).ToArray());
                }
            }
        }

        [Fact]
        public void The_two_glides_over_the_lake_stand_apart_with_a_look_up_the_shaft_between_them()
        {
            float glideHighest = 0f, glideSteepest = 0f, between = float.MaxValue;
            int passages = 0;
            foreach (int seed in Seeds)
            {
                var chamber = new CavernChamber(new CavernSceneConfig(), seed);
                for (int roll = 0; roll < ROLLS; roll++)
                {
                    IntroShot[] shots = CavernIntroShots.Build(chamber, MathHelper.ToRadians(45f), new Random(seed * 31 + roll));
                    string where = $"seed {seed}, roll {roll}";

                    //The two glides: the crystals always, the passage where the reel opens on one (the hall is high over the lake)
                    int crystals = Array.FindIndex(shots, s => s.Name == "the crystals");
                    Assert.True(crystals >= 0, $"{where}: no crystals");
                    var glides = shots[0].Name == "the passage" ? new[] { 0, crystals } : new[] { crystals };
                    if (glides.Length == 2) passages++;
                    foreach (int g in glides)
                    {
                        Range(chamber, shots[g], out float lowest, out float highest, out float least, out float most);
                        Assert.True(highest <= GLIDE_HEIGHT && MathF.Max(-least, most) <= GLIDE_PITCH_DEGREES,
                            FormattableString.Invariant($"{where}: '{shots[g].Name}' flies {lowest:F1}-{highest:F1} over the lake looking {least:F1} to {most:F1} degrees up, not a level glide"));
                        glideHighest = MathF.Max(glideHighest, highest);
                        glideSteepest = MathF.Max(glideSteepest, MathF.Max(-least, most));
                    }

                    //Never next to each other: at least one shot between them, and every one of those looks up
                    if (glides.Length < 2) continue;
                    Assert.True(crystals >= 2, $"{where}: the crystals cut straight in after the passage");
                    for (int i = 1; i < crystals; i++)
                    {
                        Range(chamber, shots[i], out _, out _, out float least, out _);
                        Assert.True(least >= LOOK_UP_DEGREES, FormattableString.Invariant($"{where}: '{shots[i].Name}' between the glides looks as little as {least:F1} degrees up"));
                        between = MathF.Min(between, least);
                    }
                }
            }

            Assert.True(passages > 0, "no reel opened on the passage, so the two glides were never tested together");
            _output.WriteLine(FormattableString.Invariant(
                $"{passages} reels open on the passage; the glides at most {glideHighest:F1} over the lake and {glideSteepest:F1} degrees off level, the shot between them at least {between:F1} degrees up"));
        }

        //The lowest and the highest the shot's lens stands over the lake, and the least and the most its view looks up (degrees,
        //negative down), over POSES poses
        private static void Range(CavernChamber chamber, IntroShot shot, out float lowest, out float highest, out float least, out float most)
        {
            lowest = least = float.MaxValue;
            highest = most = float.MinValue;
            for (int k = 0; k <= POSES; k++)
            {
                shot.Pose(k / (float)POSES, out Vector3 lens, out Vector3 look);
                Vector3 view = look - lens;
                float height = lens.Y - chamber.WaterY;
                float pitch = MathHelper.ToDegrees(MathF.Atan2(view.Y, new Vector2(view.X, view.Z).Length()));
                lowest = MathF.Min(lowest, height);
                highest = MathF.Max(highest, height);
                least = MathF.Min(least, pitch);
                most = MathF.Max(most, pitch);
            }
        }
    }
}
