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
    /// judge; what is checked here is that the two things the note names are not what the builder produces.
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

        private static City Build(bool neon, int seed)
        {
            var config = new CitySceneConfig();
            config.Seed += seed;
            config.NeonLayout.Seed += seed;

            return new City(config, neon, ArenaIsland.RADIUS);
        }

        [Fact]
        public void EveryShotRunsLongEnoughToBeLookedAt()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
                foreach (bool neon in new[] { false, true })
                {
                    IntroShot[] shots = CityIntroShots.Build(Build(neon, seed), FieldOfView, new Random(seed));

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
                    IntroShot[] shots = CityIntroShots.Build(Build(neon, seed), FieldOfView, new Random(seed));

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
                    City city = Build(neon, seed);
                    IntroShot[] shots = CityIntroShots.Build(city, FieldOfView, new Random(seed));

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
                IntroShot[] shots = CityIntroShots.Build(Build(false, seed), FieldOfView, new Random(seed));

                for (int i = 0; i + 1 < shots.Length; i++)
                    Assert.NotEqual(shots[i].Name, shots[i + 1].Name);

                int street = Array.FindIndex(shots, s => s.Name == "the street");
                int swing = Array.FindIndex(shots, s => s.Name == "the swing");
                if (swing >= 0) Assert.True(swing - street >= 2, $"seed {seed}: the swing stands next to the street");
            }
        }
    }
}
