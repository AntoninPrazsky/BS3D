using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna's tour shots before the grass home (#1156): the plain, the baobab and the acacia keep well off every tree, at
    /// the scene seeds <see cref="SavannaLandingTests"/> lands the levels' poses at and the four whose trees' spheres close every
    /// way in. A class of its own so it runs beside the landing's tests rather than after them: rolling two hundred reels is
    /// most of what it costs.
    /// </summary>
    public class SavannaTourShotsTests
    {
        private readonly ITestOutputHelper _output;

        public SavannaTourShotsTests(ITestOutputHelper output) => _output = output;

        //How far the tour's shots stay off every tree's outline at the least (measured over the test's 204 scene seeds: 5.47), and
        //how far off a tree is asked about at all
        private const float TOUR_TREE_AIR = 5f, TOUR_TREE_SEEN = 2f * TOUR_TREE_AIR;

        //How finely a shot is walked: a few seconds of a slow walk or track, measured at most 0.13 between poses
        private const int TOUR_POSES = 400;

        [Fact]
        public void The_tour_shots_keep_well_off_every_tree()
        {
            //Held by the spheres round every mesh (SavannaScatter.Solids), which are loose round a tree, the lens never comes
            //within TOUR_TREE_AIR of a tree's own outline, let alone under a crown. The three are rolled before the flight home
            //and wherever the pose stands, so the reel is rolled for the menu's crane, which builds no flight home: the home is
            //held by SavannaLandingTests
            var plains = SavannaLandingTests.LevelPlains.Value
                .Concat(SavannaLandingTests.ClosedBySpheres.Select(layout => (layout, SavannaLandingTests.Planted(layout))))
                .ToArray();
            float nearest = float.MaxValue, longestStep = 0f;
            int shots = 0;
            var near = new List<PlantOutline>();
            var track = new Vector3[TOUR_POSES + 1];
            for (int k = 0; k < plains.Length; k++)
            {
                (int layout, SavannaLandingTests.Plain plain) = plains[k];
                SavannaLand land = plain.Land;
                IntroShot[] reel = SavannaIntroShots.Build(land, SavannaLandingTests.Crane, SavannaLandingTests.GameTarget,
                    MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW, new Random(k));
                foreach (IntroShot shot in reel)
                {
                    Assert.NotEqual(SavannaLanding.NAME, shot.Name);
                    shots++;

                    //The trees within TOUR_TREE_SEEN of the shot's track: every other stands farther off every pose of it
                    Vector2 low = new(float.MaxValue), high = new(float.MinValue);
                    for (int i = 0; i <= TOUR_POSES; i++)
                    {
                        shot.Pose(i / (float)TOUR_POSES, out track[i], out _);
                        if (i > 0) longestStep = MathF.Max(longestStep, Vector3.Distance(track[i], track[i - 1]));
                        low = Vector2.Min(low, new Vector2(track[i].X, track[i].Z));
                        high = Vector2.Max(high, new Vector2(track[i].X, track[i].Z));
                    }
                    near.Clear();
                    foreach (PlantOutline tree in land.TreeOutlines)
                        if (tree.Bounds.Max.X + TOUR_TREE_SEEN >= low.X && tree.Bounds.Min.X - TOUR_TREE_SEEN <= high.X
                            && tree.Bounds.Max.Z + TOUR_TREE_SEEN >= low.Y && tree.Bounds.Min.Z - TOUR_TREE_SEEN <= high.Y)
                            near.Add(tree);

                    foreach (Vector3 p in track)
                        foreach (PlantOutline tree in near)
                        {
                            float off = tree.Off(p, TOUR_TREE_SEEN);
                            nearest = MathF.Min(nearest, off);
                            Assert.True(off >= TOUR_TREE_AIR, $"sceneseed={layout}: '{shot.Name}' {off:F2} off a tree at {tree.Root}");
                        }
                }
            }

            _output.WriteLine($"{shots} tour shots over {plains.Length} scene seeds; the nearest a tour shot comes to a tree's outline: "
                + $"{nearest:F2} (walked at most {longestStep:F3} apart)");
        }
    }
}
