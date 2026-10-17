using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The meadow's chapter-intro prologue with its two close, slow orbits (#893): the owner's playtest asked for "a detailed
    /// low-angle shot of the bench under the tree and of the cylindrical hay bales — the camera moves in a half circle very
    /// slowly and is close to the objects". What is checked here is what a number can say: each is a half circle round its
    /// subject, <c>4.6</c> s long, over the ground by the shot's own band, keeping off the old trees' stems and every low solid and out
    /// of the water, with the whole subject in frame — and above the skip hint at the bottom of it — from the first pose to the
    /// last at three window shapes. How it reads, and how slow it feels, is the eye's.
    /// <para>
    /// <b>Two bands over the ground, and they are not one.</b> The owner's is the issue's, <see cref="OWNERS_BAND_FROM"/> to
    /// <see cref="OWNERS_BAND_TO"/> (0.6 to 1.5): a low angle, looking up at the subject, which no shot may leave. The shot's own is
    /// narrower and inside it, <c>MeadowIntroShots.ORBIT_ABOVE_FROM</c> to <c>ORBIT_ABOVE_TO</c> (0.7 rising to 1.1), which is what
    /// the lens stands at and what the test holds it to, give or take <see cref="LENS_SLACK"/> for the ground between the points the
    /// arc is built at. A test of the owner's band alone passes a lens that has wandered anywhere in it; the shot's own range is the
    /// one that says the shot is still the one that was built.
    /// </para>
    /// <para>
    /// The planting needs a graphics device (<see cref="MeadowScatter"/> uploads its meshes), so the planting here is built from
    /// the constants and the geometry it keeps without one: the real bench (its own vertices, where
    /// <see cref="MeadowScatter.BenchWorld"/> puts them), bales laid out as the planting lays them, an oak with the figure
    /// <c>MeadowTreeMesh</c> gives it, and shrubs for the rest. A scene seed is a seed of the layout here, as it is there.
    /// </para>
    /// </summary>
    public class MeadowIntroShotsTests
    {
        private const int ROLLS = 24;

        //The vertical field of view the game hands the intro (GameplayScreen.GAME_FOV), which every shot widens by a tenth
        private const float INTRO_FIELD_OF_VIEW = MathF.PI / 4.2f;

        //The window shapes the owner plays at and the release is looked at in; the projection is Hor+, so the shape changes
        //the width alone
        private static readonly (int Width, int Height)[] Screens = { (3840, 1600), (1920, 1080), (1600, 900) };

        //How finely a shot is walked, poses per shot
        private const int POSES = 120;

        //The owner's band for the lens over the ground (#893's issue), and the slack allowed on the shot's own band: the arc is
        //built at the ground's height under its points and the lens runs between them
        private const float OWNERS_BAND_FROM = 0.6f;
        private const float OWNERS_BAND_TO = 1.5f;
        private const float LENS_SLACK = 0.02f;

        //How far inside every edge the subject must stay, as a share of the frame, as the city's mast is held (#840)
        private const float FRAME_MARGIN = 0.04f;

        //And over the bottom edge by more than the skip hint reaches up it, 12.2 % of the frame's height (measured on the
        //captures, docs/game-feedback/chapter-intro-prologues.md), which is centred there: held for what stands in its width
        private const float FOOT_CLEAR = 0.13f;
        private const float HINT_HALF_WIDTH = 0.35f;

        //The knoll's oak is planted at this scale
        private const float OAK_SCALE = MeadowScatter.KNOLL_OAK_SCALE;

        //A shrub: the sphere round the three meshes, at a middling instance
        private const float SHRUB_UP = 1.6f;

        private sealed class Planting
        {
            public MeadowSceneConfig Config;
            public MeadowFigures Figures;
            public List<Vector3> BenchPoints;
            public List<List<Vector3>> BalePoints;
            public PlantFigure Oak;
        }

        //An old tree as MeadowTreeMesh grows an Oak and MeadowScatter plants it, at the middle of its draws and sunk 0.35 into the
        //ground: the sphere round the mesh (the crown's centre 11.8 up, a third of the dome's half height under the dome's, a
        //radius of the larger of the reach and the crown's height by 1.15, 17.8), the stem the flare's radius (2.0)
        private static PlantFigure Tree(float x, float z, float ground, float scale) => new(
            new Vector3(x, ground - 0.35f * scale, z), new Vector3(x, ground - 0.35f * scale + 11.8f * scale, z), 17.8f * scale, 2.0f * scale);

        private static PlantFigure Shrub(float x, float z, float ground, float radius) =>
            new(new Vector3(x, ground, z), new Vector3(x, ground + SHRUB_UP, z), radius, 0f);

        private static Planting Plant(int seed, bool shrubsOffTheArenaSide = true)
        {
            var config = new MeadowSceneConfig();
            float Height(float x, float z) => TerrainMirror.Meadow(x, z, config);
            var rng = new Random(seed);

            (float knollX, float knollZ) = MeadowPath.PathEnd(config);
            float knollGround = Height(knollX, knollZ);
            PlantFigure oak = Tree(knollX, knollZ, knollGround, OAK_SCALE);
            var trees = new List<PlantFigure> { oak };

            //Three more old trees out on the rise, as the planting keeps them (130 and out)
            for (int i = 0; i < 3; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi, d = 130f + 120f * (float)rng.NextDouble();
                float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
                trees.Add(Tree(x, z, Height(x, z), 0.9f + 0.25f * (float)rng.NextDouble()));
            }

            //The bench, with its own vertices where the planting puts them
            (float benchX, float benchZ, _) = MeadowScatter.BenchStand(knollX, knollZ);
            float benchGround = Height(benchX, benchZ);
            PlantFigure bench = MeadowScatter.BenchFigure(knollX, knollZ, benchGround);
            Matrix benchWorld = MeadowScatter.BenchWorld(knollX, knollZ, benchGround);
            var benchPoints = new List<Vector3>();
            foreach (var vertex in MeadowScatter.BenchGeometry().Vertices)
            {
                //The legs go 0.4 under the bench's origin, into the ground, where nothing of them is seen
                Vector3 at = Vector3.Transform(vertex.Position, benchWorld);
                at.Y = MathF.Max(at.Y, Height(at.X, at.Z));
                benchPoints.Add(at);
            }

            var solids = new List<PlantFigure>();

            //The bales, in groups of one to three as the planting lays them, off the path, the brook and the pond
            var bales = new List<PlantFigure>();
            var balePoints = new List<List<Vector3>>();

            bool BaleFree(float x, float z)
            {
                float r = MeadowScatter.BALE_REACH;
                if (MathF.Abs(MeadowPath.Lateral(x, z, config)) < r + 2.2f) return false;
                if (MathF.Abs(MeadowPath.BrookLateral(x, z, config)) < r + config.BrookWidth) return false;
                if (MeadowPath.PondShore(x, z, config) < r + 4f) return false;
                foreach (PlantFigure other in bales)
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(other.Root.X, other.Root.Z)) < 2f * r) return false;
                return true;
            }

            for (int i = 0, tries = 0; i < 14 && tries < 600; tries++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float d = MathF.Sqrt(MathHelper.Lerp(110f * 110f, 280f * 280f, (float)rng.NextDouble()));
                float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
                if (!BaleFree(x, z)) continue;

                int groupFirst = bales.Count;
                int group = 1 + rng.Next(3);
                for (int g = 0; g < group && i < 14; g++)
                {
                    float gx = x + g * MeadowScatter.BALE_GROUP_SPACING * MathF.Cos(i), gz = z + g * MeadowScatter.BALE_GROUP_SPACING * MathF.Sin(i);
                    if (!BaleFree(gx, gz)) continue;

                    float scale = 0.95f + 0.1f * (float)rng.NextDouble(), yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                    Matrix world = Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw)
                        * Matrix.CreateTranslation(gx, Height(gx, gz) - MeadowScatter.BALE_SINK, gz);
                    bales.Add(PlantFigure.Of(MeadowScatter.BaleBounds, world, 0f));

                    //The bale's two rims: where its silhouette is, which its bounding sphere is much taller than
                    var rim = new List<Vector3>();
                    foreach (float end in new[] { -1f, 1f })
                        for (int k = 0; k < 8; k++)
                        {
                            float turn = k * MathHelper.PiOver4;
                            rim.Add(Vector3.Transform(new Vector3(end * MeadowScatter.BALE_HALF_LENGTH,
                                MeadowScatter.BALE_RADIUS * (1f + MathF.Cos(turn)), MeadowScatter.BALE_RADIUS * MathF.Sin(turn)), world));
                        }
                    balePoints.Add(rim);
                    i++;
                }

                //A shrub or two at a random side of a group, near enough that a half circle round it has to choose its side
                if (bales.Count > groupFirst && rng.NextDouble() < 0.6)
                {
                    PlantFigure first = bales[groupFirst];
                    for (int s = 0; s < 1 + rng.Next(2); s++)
                    {
                        float around = (float)rng.NextDouble() * MathHelper.TwoPi, out0 = 7f + 3f * (float)rng.NextDouble();
                        float sx = first.Crown.X + MathF.Cos(around) * out0, sz = first.Crown.Z + MathF.Sin(around) * out0;
                        solids.Add(Shrub(sx, sz, Height(sx, sz), 2.5f + 0.8f * (float)rng.NextDouble()));
                    }
                }
            }

            //Shrubs about the field, and on the knoll's far side from the arena (behind the bench, where the arena side's
            //half circle is not: the bench is looked at from the arena side)
            for (int i = 0; i < 80; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi, d = MathF.Sqrt(MathHelper.Lerp(100f * 100f, 290f * 290f, (float)rng.NextDouble()));
                float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
                solids.Add(Shrub(x, z, Height(x, z), 2.5f + 0.8f * (float)rng.NextDouble()));
            }

            float behind = MathF.Atan2(knollZ, knollX);
            for (int i = 0; i < 6 && shrubsOffTheArenaSide; i++)
            {
                float a = behind + ((float)rng.NextDouble() - 0.5f) * 1.2f, d = 14f + 10f * (float)rng.NextDouble();
                float x = knollX + MathF.Cos(a) * d, z = knollZ + MathF.Sin(a) * d;
                solids.Add(Shrub(x, z, Height(x, z), 2.5f + 0.8f * (float)rng.NextDouble()));
            }

            solids.AddRange(bales);
            solids.Add(bench);

            return new Planting
            {
                Config = config, Oak = oak, BenchPoints = benchPoints, BalePoints = balePoints,
                Figures = new MeadowFigures(trees, bench, bales, solids),
            };
        }

        private static IntroShot[] Shots(Planting planting, int seed) =>
            MeadowIntroShots.Build(planting.Config, planting.Figures, INTRO_FIELD_OF_VIEW, new Random(seed * 7 + 1));

        private static IntroShot Named(IntroShot[] shots, string name) => Array.Find(shots, s => s.Name == name);

        private static Vector2 Plan(Vector3 p) => new(p.X, p.Z);

        //The subject of an orbit shot, as it was handed to the builder: the look every pose holds
        private static Vector3 LookOf(IntroShot shot)
        {
            shot.Pose(0.5f, out _, out Vector3 target);
            return target;
        }

        [Fact]
        public void TheBenchIsInEveryPrologueAndTheBalesInAlmostEvery()
        {
            int withBales = 0;
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = Shots(Plant(seed), seed);

                Assert.Equal("the valley", shots[0].Name);
                Assert.NotNull(Named(shots, "the bench"));
                Assert.Equal("the bench", shots[^1].Name);
                if (Named(shots, "the bales") != null) withBales++;

                for (int i = 0; i + 1 < shots.Length; i++)
                    Assert.NotEqual(shots[i].Name, shots[i + 1].Name);
            }

            Assert.True(withBales >= ROLLS * 9 / 10, $"the bales' shot was built in only {withBales} of {ROLLS} layouts");
        }

        [Fact]
        public void TheOrbitsRunTheOrderThatAlternatesWhatTheLensDoes()
        {
            IntroShot[] shots = Shots(Plant(1), 1);
            string[] names = Array.ConvertAll(shots, s => s.Name);
            Assert.Equal(new[] { "the valley", "the bales", "the brook", "the path", "the bench" }, names);
        }

        [Fact]
        public void EachOrbitIsAHalfCircleRoundItsSubjectInAboutFiveSeconds()
        {
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = Shots(Plant(seed), seed);

                foreach (string name in new[] { "the bench", "the bales" })
                {
                    IntroShot shot = Named(shots, name);
                    if (shot == null) continue;

                    Assert.InRange(shot.Seconds, 4.5f, 5f);

                    //Walked pose by pose: the lens stays one distance from the look, and goes half way round it
                    Vector3 look = LookOf(shot);
                    shot.Pose(0f, out Vector3 start, out _);
                    shot.Pose(1f, out Vector3 end, out _);
                    Vector2 first = Plan(start) - Plan(look), last = Plan(end) - Plan(look);
                    float radius = first.Length();

                    Assert.Equal(-1f, Vector2.Dot(Vector2.Normalize(first), Vector2.Normalize(last)), 2);
                    Assert.InRange(last.Length(), radius - 0.05f, radius + 0.05f);

                    float swept = 0f;
                    Vector2 before = first;
                    for (int step = 1; step <= POSES; step++)
                    {
                        shot.Pose(step / (float)POSES, out Vector3 position, out _);
                        Vector2 now = Plan(position) - Plan(look);
                        Assert.InRange(now.Length(), radius - 0.05f, radius + 0.05f);

                        swept += MathF.Atan2(before.X * now.Y - before.Y * now.X, Vector2.Dot(before, now));
                        before = now;
                    }

                    Assert.Equal(MathF.PI, MathF.Abs(swept), 1);
                }
            }
        }

        [Fact]
        public void TheRadiusIsTheSubjectsOwnFigureAndNotAConstant()
        {
            //The bench was resized once (#891) and the bales before it (#864): double the bench's figure and the lens stands
            //farther, by the bench's own growth, with no number in the builder to edit
            Planting planting = Plant(3);
            PlantFigure bench = planting.Figures.Bench;
            var bigger = new PlantFigure(bench.Root, bench.Crown, bench.Reach * 2f, bench.Stem);
            var solids = new List<PlantFigure>(planting.Figures.Solids);
            solids.Remove(bench);
            var resized = new MeadowFigures(planting.Figures.Trees, bigger, planting.Figures.Bales, solids);

            IntroShot small = MeadowIntroShots.Bench(planting.Config, planting.Figures, INTRO_FIELD_OF_VIEW, new Random(1));
            IntroShot large = MeadowIntroShots.Bench(planting.Config, resized, INTRO_FIELD_OF_VIEW, new Random(1));
            Assert.NotNull(small);
            Assert.NotNull(large);

            float Radius(IntroShot shot)
            {
                shot.Pose(0f, out Vector3 start, out _);
                return Vector2.Distance(Plan(start), Plan(LookOf(shot)));
            }

            Assert.True(Radius(large) >= Radius(small) + bench.Reach * 0.9f,
                $"the lens stands {Radius(small):F2} from a bench of reach {bench.Reach:F2} and {Radius(large):F2} from one of {bench.Reach * 2f:F2}");
        }

        [Fact]
        public void TheLensIsLowKeepsOffEverythingAndIsNeverOverTheWater()
        {
            //The shot's own band is inside the owner's, so it cannot be retuned out of what he asked for
            Assert.InRange(MeadowIntroShots.ORBIT_ABOVE_FROM, OWNERS_BAND_FROM, OWNERS_BAND_TO);
            Assert.InRange(MeadowIntroShots.ORBIT_ABOVE_TO, OWNERS_BAND_FROM, OWNERS_BAND_TO);

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                Planting planting = Plant(seed);
                IntroShot[] shots = Shots(planting, seed);
                MeadowSceneConfig config = planting.Config;

                foreach (string name in new[] { "the bench", "the bales" })
                {
                    IntroShot shot = Named(shots, name);
                    if (shot == null) continue;

                    for (int step = 0; step <= POSES; step++)
                    {
                        shot.Pose(step / (float)POSES, out Vector3 p, out _);
                        string where = $"seed {seed}, '{name}' at {step}/{POSES}, {p}";

                        float above = p.Y - TerrainMirror.Meadow(p.X, p.Z, config);
                        Assert.True(above >= MeadowIntroShots.ORBIT_ABOVE_FROM - LENS_SLACK && above <= MeadowIntroShots.ORBIT_ABOVE_TO + LENS_SLACK,
                            $"{where}: the lens is {above:F2} over the ground, the shot's own band {MeadowIntroShots.ORBIT_ABOVE_FROM} to {MeadowIntroShots.ORBIT_ABOVE_TO}");

                        //The arena's column, every old tree by its stem, every solid by its own sphere or capsule
                        Assert.True(Plan(p).Length() >= IntroGround.ARENA_KEEP_OUT, $"{where}: inside the arena's column");
                        foreach (PlantFigure tree in planting.Figures.Trees)
                            Assert.True(IntroPaths.DistanceToSegment(p, tree.Root, tree.Crown) - tree.Stem >= 0.5f, $"{where}: against a trunk");
                        foreach (PlantFigure solid in planting.Figures.Solids)
                        {
                            Assert.True(IntroPaths.DistanceToSegment(p, solid.Root, solid.Crown) - solid.Stem >= 0.5f
                                && Vector3.Distance(p, solid.Crown) - solid.Reach >= 0.5f, $"{where}: against a solid at {solid.Crown}");
                        }

                        Assert.True(MeadowPath.PondShore(p.X, p.Z, config) >= 2f, $"{where}: over the pond's edge");
                        Assert.True(MathF.Abs(MeadowPath.BrookLateral(p.X, p.Z, config)) >= config.BrookWidth * 0.5f + 2f, $"{where}: over the brook");
                    }
                }
            }
        }

        [Fact]
        public void TheBenchIsHeldAgainstTheOaksStemAndNotItsCrown()
        {
            //The fault this guards: the oak's crown is a sphere round its mesh that reaches below the ground (a centre
            //13.6 up, a radius of 20), so with the tree taken whole every lens point under it is inside a solid and the half
            //circle round the bench is refused in every direction. The oak as a sphere among the solids is that tree taken whole.
            Planting planting = Plant(5);
            MeadowFigures figures = planting.Figures;
            Assert.True(Vector3.Distance(figures.Bench.Crown, planting.Oak.Crown) < planting.Oak.Reach,
                "the bench is not inside the oak's crown sphere, which is the premise");

            IntroShot byStem = MeadowIntroShots.Bench(planting.Config, figures, INTRO_FIELD_OF_VIEW, new Random(1));
            Assert.NotNull(byStem);

            var withCrown = new List<PlantFigure>(figures.Solids) { planting.Oak };
            var whole = new MeadowFigures(figures.Trees, figures.Bench, figures.Bales, withCrown);
            Assert.Null(MeadowIntroShots.Bench(planting.Config, whole, INTRO_FIELD_OF_VIEW, new Random(1)));
        }

        [Fact]
        public void ABenchBehindAHedgeIsPassedOverAndNotFilmedThroughIt()
        {
            //A wall of shrubs across the arena's side of the bench, as close as the lens goes: no half circle clears it
            Planting planting = Plant(7);
            PlantFigure bench = planting.Figures.Bench;
            Vector2 toArena = Vector2.Normalize(-Plan(bench.Crown));
            Vector2 along = new(-toArena.Y, toArena.X);

            var solids = new List<PlantFigure>(planting.Figures.Solids);
            for (float s = -12f; s <= 12f; s += 2f)
            {
                Vector2 at = Plan(bench.Crown) + toArena * 6f + along * s;
                solids.Add(Shrub(at.X, at.Y, TerrainMirror.Meadow(at.X, at.Y, planting.Config), 3f));
            }

            var walled = new MeadowFigures(planting.Figures.Trees, bench, planting.Figures.Bales, solids);
            Assert.Null(MeadowIntroShots.Bench(planting.Config, walled, INTRO_FIELD_OF_VIEW, new Random(1)));

            //And the prologue goes on without it
            IntroShot[] shots = MeadowIntroShots.Build(planting.Config, walled, INTRO_FIELD_OF_VIEW, new Random(1));
            Assert.Null(Named(shots, "the bench"));
            Assert.NotNull(Named(shots, "the valley"));
            Assert.NotNull(Named(shots, "the path"));
        }

        [Fact]
        public void ABoulderBetweenTheLensAndTheBenchBlocksTheViewEvenWhereTheLensClearsIt()
        {
            //A boulder on the line from the arena to the bench, a little outside the bench's sphere: a lens on that line stands
            //two units off it and clears it, and sees nothing but its back. A half circle that crosses it is refused whole, at
            //whatever radius, because the view is the middle of the arc.
            Planting planting = Plant(11);
            PlantFigure bench = planting.Figures.Bench;
            Assert.NotNull(MeadowIntroShots.Bench(planting.Config, planting.Figures, INTRO_FIELD_OF_VIEW, new Random(1)));

            Vector2 at = Plan(bench.Crown) + Vector2.Normalize(-Plan(bench.Crown)) * (bench.Reach + 0.7f);
            var solids = new List<PlantFigure>(planting.Figures.Solids) { Shrub(at.X, at.Y, TerrainMirror.Meadow(at.X, at.Y, planting.Config), 1f) };
            var blocked = new MeadowFigures(planting.Figures.Trees, bench, planting.Figures.Bales, solids);

            Assert.Null(MeadowIntroShots.Bench(planting.Config, blocked, INTRO_FIELD_OF_VIEW, new Random(1)));
        }

        [Fact]
        public void TheBalesByThePondAreNeverFilmedFromOverItsWater()
        {
            //A lone bale six and a half units outside the pond's shore, on the arena's side: a lens at its radius reaches over
            //the water on the half of the circle that faces the pond, and that half is refused. Thirty rolls of the start,
            //so a builder that did not look would put the lens over the water in most of them.
            Planting planting = Plant(9);
            MeadowSceneConfig config = planting.Config;
            (float pondX, float pondZ) = MeadowPath.PondCentre(config);
            float toArena = MathF.Atan2(-pondZ, -pondX);
            float shore = config.PondRadius * MeadowPath.PondOutline(toArena);
            float x = pondX + MathF.Cos(toArena) * (shore + 6.5f), z = pondZ + MathF.Sin(toArena) * (shore + 6.5f);
            PlantFigure bale = PlantFigure.Of(MeadowScatter.BaleBounds,
                Matrix.CreateTranslation(x, TerrainMirror.Meadow(x, z, config) - MeadowScatter.BALE_SINK, z), 0f);
            var lone = new MeadowFigures(planting.Figures.Trees, planting.Figures.Bench, new[] { bale }, new[] { bale });

            int built = 0;
            for (int roll = 1; roll <= 30; roll++)
            {
                IntroShot shot = MeadowIntroShots.Bales(config, lone, INTRO_FIELD_OF_VIEW, new Random(roll));
                if (shot == null) continue;

                built++;
                for (int step = 0; step <= POSES; step++)
                {
                    shot.Pose(step / (float)POSES, out Vector3 p, out _);
                    Assert.True(MeadowPath.PondShore(p.X, p.Z, config) >= 2.5f, $"roll {roll} at {step}/{POSES}: the lens is {MeadowPath.PondShore(p.X, p.Z, config):F1} from the pond's shore");
                }
            }

            Assert.True(built > 0, "no half circle was found on the side away from the pond");
        }

        [Fact]
        public void BalesAreTriedAPairBeforeALoneBaleBeforeARowOfThreeAndTheNearestFirstWithinAShape()
        {
            //A row of three nearest the arena's middle, then a lone bale, a pair, and another lone bale farther out: standing
            //nearest the arena must not put the row first, and a lone bale must not tie with the row (both are one from two)
            PlantFigure[] Row(float x, float z, int count)
            {
                var row = new PlantFigure[count];
                for (int i = 0; i < count; i++)
                    row[i] = PlantFigure.Of(MeadowScatter.BaleBounds,
                        Matrix.CreateTranslation(x + i * MeadowScatter.BALE_GROUP_SPACING, 0f, z), 0f);
                return row;
            }

            var bales = new List<PlantFigure>();
            bales.AddRange(Row(30f, 0f, 3));
            bales.AddRange(Row(0f, 60f, 1));
            bales.AddRange(Row(0f, -100f, 2));
            bales.AddRange(Row(150f, 0f, 1));

            List<MeadowIntroShots.BaleGroup> ordered = MeadowIntroShots.Ordered(bales);

            Assert.Equal(new[] { 2, 1, 1, 3 }, ordered.ConvertAll(g => g.Count).ToArray());
            Assert.True(ordered[1].Centre.LengthSquared() < ordered[2].Centre.LengthSquared(), "the nearer lone bale is not first");
        }

        [Fact]
        public void ThePrologueWithNoPlantingIsTheLandAlone()
        {
            IntroShot[] shots = MeadowIntroShots.Build(new MeadowSceneConfig(), null, INTRO_FIELD_OF_VIEW, new Random(1));
            Assert.Equal(new[] { "the valley", "the brook", "the path" }, Array.ConvertAll(shots, s => s.Name));
            Assert.Null(MeadowIntroShots.Build(null, null, INTRO_FIELD_OF_VIEW, new Random(1)));
        }

        [Fact]
        public void TheWholePrologueRunsNoLongerThanTheCities()
        {
            //The docs state 11-16 s for a prologue and 22 for the cities (chapter-intro-prologues.md). The meadow's two orbits
            //take it to about the cities': this holds it there, the tour's last leg (4.5 s) included, for the next shot added
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                float seconds = 4.5f;
                foreach (IntroShot shot in Shots(Plant(seed), seed)) seconds += shot.Seconds;

                Assert.True(seconds <= 23f, $"seed {seed}: the meadow's intro runs {seconds:F1} s");
            }
        }

        [Fact]
        public void NoCutLandsOnTheSamePictureNudged()
        {
            //The city's rule (CityIntroShotsTests): across a cut the lens changes where it stands or where it looks
            for (int seed = 1; seed <= ROLLS; seed++)
            {
                IntroShot[] shots = Shots(Plant(seed), seed);

                for (int i = 0; i + 1 < shots.Length; i++)
                {
                    shots[i].Pose(1f, out Vector3 fromPosition, out Vector3 fromTarget);
                    shots[i + 1].Pose(0f, out Vector3 toPosition, out Vector3 toTarget);

                    Vector3 before = Vector3.Normalize(fromTarget - fromPosition);
                    Vector3 after = Vector3.Normalize(toTarget - toPosition);
                    float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector3.Dot(before, after), -1f, 1f)));
                    float distance = Vector3.Distance(fromPosition, toPosition);

                    Assert.True(angle >= 22f || distance >= 40f,
                        $"seed {seed}: '{shots[i].Name}' -> '{shots[i + 1].Name}' cuts to a view {angle:F0} degrees and {distance:F0} units from the last");
                }
            }
        }

        /// <summary>
        /// The frame (#893, step 4): the bench's every vertex and the bales' rims stay inside every edge of the frame, with room
        /// to read, from the first pose of the half circle to the last — and over the skip hint, which stands centred on the
        /// bottom edge for the whole intro, up to 12.2 % of the frame's height — at 3840x1600, 1920x1080 and 1600x900.
        /// </summary>
        [Fact]
        public void TheSubjectStaysInFrameAndOverTheSkipHintAtEveryWindowShape()
        {
            float limit = 1f - 2f * FRAME_MARGIN, floor = -(1f - 2f * FOOT_CLEAR);
            int walked = 0;
            string worst = null;
            float worstOver = 0f;

            for (int seed = 1; seed <= ROLLS; seed++)
            {
                Planting planting = Plant(seed);
                IntroShot[] shots = Shots(planting, seed);

                foreach (string name in new[] { "the bench", "the bales" })
                {
                    IntroShot shot = Named(shots, name);
                    if (shot == null) continue;

                    //What the shot is about: the bench's mesh, or the rims of the bales of the group the builder took (the one it
                    //looks at the middle of)
                    var points = new List<Vector3>();
                    if (name == "the bench") points.AddRange(planting.BenchPoints);
                    else
                    {
                        Vector3 look = LookOf(shot);
                        MeadowIntroShots.BaleGroup group = MeadowIntroShots.Groups(planting.Figures.Bales)
                            .Find(g => Vector3.Distance(g.Look, look) < 0.01f);
                        Assert.NotNull(group.Members);
                        foreach (int member in group.Members) points.AddRange(planting.BalePoints[member]);
                    }
                    Assert.NotEmpty(points);
                    walked++;

                    foreach ((int width, int height) in Screens)
                    {
                        Matrix projection = Matrix.CreatePerspectiveFieldOfView(shot.FieldOfView, width / (float)height, 0.1f, 5000f);

                        for (int step = 0; step <= POSES; step++)
                        {
                            shot.Pose(step / (float)POSES, out Vector3 position, out Vector3 target);
                            Matrix viewProjection = Matrix.CreateLookAt(position, target, Vector3.Up) * projection;

                            foreach (Vector3 point in points)
                            {
                                Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
                                float x = clip.X / clip.W, y = clip.Y / clip.W;

                                float over = clip.W <= 0f ? float.PositiveInfinity : MathF.Max(MathF.Abs(x), MathF.Abs(y)) - limit;
                                if (clip.W > 0f && MathF.Abs(x) <= HINT_HALF_WIDTH) over = MathF.Max(over, floor - y);
                                if (over <= worstOver) continue;

                                worstOver = over;
                                worst = $"seed {seed}, '{name}', {width}x{height}, at {step}/{POSES}: x {x:F2}, y {y:F2} (limit {limit:F2}, hint {floor:F2})";
                            }
                        }
                    }
                }
            }

            Assert.True(walked >= ROLLS, $"only {walked} orbits were walked");
            Assert.True(worst == null, $"the subject leaves the frame or goes under the skip hint; the worst: {worst}");
        }
    }
}
