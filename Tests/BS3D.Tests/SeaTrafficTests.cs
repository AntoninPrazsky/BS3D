using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using static BS3D.Tests.PlayViewOpenings;

namespace BS3D.Tests
{
    /// <summary>
    /// What stands on the open sea besides the oil platforms (#1125, the owner: the sea is "still quite empty"): the lighthouse on
    /// its rock, the container ship and the yacht (<see cref="SeaTrafficLayout"/>, <see cref="SeaTrafficMeshes"/>), held over the
    /// very triangles that are drawn: every one wound clockwise seen from outside; all of it where the swell is flat; nothing on
    /// the ship out past its hull but the bridge wings; the lanes
    /// clear of both platforms, of the lighthouse and of each other; no lens the chapter intro flies near a lane or the lighthouse;
    /// a vessel under the horizon from every lens before it wraps round; the vessels on their clock alone, which starts again
    /// when the sea is selected; the lighthouse in the play view of every shipped level at every window shape, each vessel
    /// crossing it, and what of the three the sea's prologue frames. <see cref="SeaTrafficSourceTests"/> read the sink in
    /// <c>Rig.fx</c> and which draws take it.
    /// </summary>
    public class SeaTrafficTests
    {
        private readonly ITestOutputHelper _output;

        public SeaTrafficTests(ITestOutputHelper output) => _output = output;

        public static readonly IEnumerable<object[]> Aspects = ASPECTS.Select(a => new object[] { a });

        private static readonly SeaSceneConfig Sea = new();

        //How near a lane or the lighthouse anything else may come: a leg of a platform is 8.8 wide, and the intro's lens flies
        //by at some fifty units a second (SeaIntroShotsTests' own clearance off the platforms)
        private const float CLEARANCE = 20f;

        //The farthest from the arena any lens the game flies stands: the intro's run at the nearer platform, out to some 1070
        //(held below), and the play view, the menu's orbit and the tour's last leg nearer the arena than that
        private const float LENS_REACH = 1100f;

        //How long a lane each vessel must spend in the opening play view at the least, seconds
        private const float SHIP_SECONDS = 50f, YACHT_SECONDS = 120f;

        private static List<RigVertex> Ship() => SeaTrafficMeshes.ContainerShip();
        private static List<RigVertex> Yacht() => SeaTrafficMeshes.Yacht(SeaTrafficLayout.YachtLeeward(Sea.Wind.ToVector2()));
        private static List<RigVertex> Lighthouse() => SeaTrafficMeshes.Lighthouse();

        private static IEnumerable<Vector3> Placed(List<RigVertex> mesh, Matrix frame) => mesh.Select(v => Vector3.Transform(v.Position, frame));

        //Each vessel with its mesh, its lane and how it is placed at a moment
        private static IEnumerable<(string Name, List<RigVertex> Mesh, float Lane, Func<float, Matrix> At, float Speed)> Vessels()
        {
            yield return ("the ship", Ship(), SeaTrafficLayout.SHIP_LANE, t => SeaTrafficLayout.ShipAt(t, Sea.LevelY), SeaTrafficLayout.SHIP_SPEED);
            yield return ("the yacht", Yacht(), SeaTrafficLayout.YACHT_LANE, t => SeaTrafficLayout.YachtAt(t, Sea.LevelY), SeaTrafficLayout.YACHT_SPEED);
        }

        //Where a point stands across the lanes: its distance out along their normal
        private static float Across(Vector3 point) => Vector2.Dot(new Vector2(point.X, point.Z), SeaTrafficLayout.LaneNormal);

        //The band across the lanes a vessel's hull, rigging and sails sweep along its lane (its every vertex, wherever on it)
        private static (float Near, float Far) Band(List<RigVertex> mesh, float lane)
        {
            float near = float.MaxValue, far = float.MinValue;
            foreach (Vector3 p in Placed(mesh, SeaTrafficLayout.VesselAt(0f, lane, 1f, Sea.LevelY)))
            {
                near = MathF.Min(near, Across(p));
                far = MathF.Max(far, Across(p));
            }
            return (near, far);
        }

        //Every corner of every box of both platforms
        private static List<Vector3> RigCorners()
        {
            var corners = new List<Vector3>();
            void Collect(Matrix world, Vector3 half, Vector3 color, float kind)
            {
                for (int i = 0; i < 8; i++)
                    corners.Add(Vector3.Transform(new Vector3((i & 1) == 0 ? -half.X : half.X, (i & 2) == 0 ? -half.Y : half.Y, (i & 4) == 0 ? -half.Z : half.Z), world));
            }
            OilRigs.Parts(OilRigLayout.Near(Sea.LevelY), Collect);
            OilRigs.Parts(OilRigLayout.Far(Sea.LevelY), Collect);
            return corners;
        }

        //Every lens of the sea's prologue (SeaIntroShotsTests' rolls and suns: dome 13's and a sweep round the compass), at this
        //many points along each shot: the run at the platform flies 174 units, so they are under five apart
        private const int POSES = 40;

        private static List<Vector3?> Suns()
        {
            var suns = new List<Vector3?> { Sun(13f, 135f), null };
            for (float azimuth = 0f; azimuth < 360f; azimuth += 30f)
            {
                suns.Add(Sun(6f, azimuth));
                suns.Add(Sun(50f, azimuth));
            }
            return suns;
        }

        private static IEnumerable<(string What, Vector3 Lens)> IntroLenses()
        {
            foreach (Vector3? sun in Suns())
                for (int seed = 1; seed <= 24; seed++)
                    foreach (IntroShot shot in SeaIntroShots.Shots(Sea, sun, GAME_FOV, new Random(seed)))
                        for (int i = 0; i < POSES; i++)
                        {
                            shot.Pose(i / (POSES - 1f), out Vector3 lens, out _);
                            yield return ($"sun {sun}, seed {seed}, '{shot.Name}' at {i}/{POSES}", lens);
                        }
        }

        private static Vector3? Sun(float elevation, float azimuth)
        {
            float e = MathHelper.ToRadians(elevation), a = MathHelper.ToRadians(azimuth);
            return new Vector3(MathF.Cos(e) * MathF.Sin(a), MathF.Sin(e), MathF.Cos(e) * MathF.Cos(a));
        }

        [Fact]
        public void EveryTriangleIsWoundClockwiseSeenFromOutside()
        {
            //CLAUDE.md "Triangle winding": a triangle's (b - a) x (c - a) points away from the normals it is lit by. The sails are
            //single sheets lit from the side the wind fills, and hold the same rule against that side. Seen to fire with the
            //builder's swap taken out.
            foreach ((string name, List<RigVertex> mesh) in new[] { ("the lighthouse", Lighthouse()), ("the ship", Ship()), ("the yacht", Yacht()) })
            {
                Assert.True(mesh.Count > 0 && mesh.Count % 3 == 0, $"{name} has {mesh.Count} vertices");
                for (int i = 0; i < mesh.Count; i += 3)
                {
                    Vector3 geometric = Vector3.Normalize(Vector3.Cross(mesh[i + 1].Position - mesh[i].Position, mesh[i + 2].Position - mesh[i].Position));
                    for (int k = 0; k < 3; k++)
                    {
                        float agreement = Vector3.Dot(geometric, mesh[i + k].Normal);
                        Assert.True(agreement < 0f, $"{name}: triangle {i / 3} faces its normal at {agreement:F2}");
                    }
                }
                _output.WriteLine($"{name}: {mesh.Count / 3} triangles");
            }
        }

        [Fact]
        public void AllOfItStandsWhereTheSwellIsFlatWhoeverLooks()
        {
            //Rig.fx cuts everything at a flat waterline, so it has to stand where Sea.fx's calm with the distance from the arena has
            //the water flat (#1124): past WaveFadeEnd. A vessel's nearest to the arena anywhere on its lane is its lane's nearest
            //point less its reach across it. Seen to fire with the yacht's lane at 780.
            foreach ((string name, List<RigVertex> mesh, float lane, _, _) in Vessels())
            {
                float nearest = Band(mesh, lane).Near;
                Assert.True(nearest >= Sea.WaveFadeEnd, $"{name} comes {nearest:F1} near the arena, inside the swell's {Sea.WaveFadeEnd}");
            }

            float rock = Placed(Lighthouse(), SeaTrafficLayout.Lighthouse(Sea.LevelY)).Min(p => new Vector2(p.X, p.Z).Length());
            Assert.True(rock >= Sea.WaveFadeEnd, $"the lighthouse's rock comes {rock:F1} near the arena, inside the swell's {Sea.WaveFadeEnd}");
        }

        [Fact]
        public void NothingOnTheShipStandsOutPastItsHullButTheBridgeWings()
        {
            //Seen from above, everything over the deck stands inside the hull's edge there (the outline round the hull's vertices
            //at SHIP_DECK_Y), or it hangs in the air beside the hull. The bridge wings are the one thing out over the side, a
            //unit (half a metre) as a real ship's are. Seen to fire with the forecastle a box again, 24 long and 26 wide, its
            //forward corners out past the bow.
            const float WINGS = 1f;
            List<RigVertex> ship = Ship();
            Vector2[] edge = ConvexHull(ship.Where(v => MathF.Abs(v.Position.Y - SeaTrafficMeshes.SHIP_DECK_Y) < 1e-3f)
                .Select(v => new Vector2(v.Position.X, v.Position.Z)));
            float farthest = float.MinValue;
            foreach (RigVertex v in ship.Where(v => v.Position.Y > SeaTrafficMeshes.SHIP_DECK_Y + 1e-3f))
            {
                float beyond = Beyond(edge, new Vector2(v.Position.X, v.Position.Z));
                farthest = MathF.Max(farthest, beyond);
                Assert.True(beyond <= WINGS + 1e-3f, $"the ship's vertex at {v.Position} stands {beyond:F2} out past its hull's edge");
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the farthest out past the hull's edge anything stands is {0:F2}", farthest));
        }

        //The convex outline round points in a plane, counter-clockwise (Andrew's monotone chain)
        private static Vector2[] ConvexHull(IEnumerable<Vector2> points)
        {
            static float Turn(Vector2 o, Vector2 a, Vector2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

            var sorted = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                foreach (Vector2 p in sorted)
                {
                    while (hull.Count >= start + 2 && Turn(hull[^2], hull[^1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
                sorted.Reverse();
            }
            return hull.ToArray();
        }

        //How far a point stands out past a counter-clockwise convex outline (negative inside it): the farthest it stands out
        //past any edge's line
        private static float Beyond(Vector2[] outline, Vector2 point)
        {
            float beyond = float.MinValue;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], way = outline[(i + 1) % outline.Length] - a;
                Vector2 outward = Vector2.Normalize(new Vector2(way.Y, -way.X));
                beyond = MathF.Max(beyond, Vector2.Dot(point - a, outward));
            }
            return beyond;
        }

        [Fact]
        public void TheLanesPassClearOfThePlatformsTheLighthouseAndEachOther()
        {
            //The lanes are straight and a vessel can be anywhere along one, so what stands across them from anything is the band
            //its hull and sails sweep. Seen to fire with the ship's lane at 1050 (the farther platform's jacket).
            var bands = Vessels().Select(v => (v.Name, Band: Band(v.Mesh, v.Lane))).ToList();
            var others = new List<(string Name, IEnumerable<Vector3> Points)>
            {
                ("a platform", RigCorners()),
                ("the lighthouse", Placed(Lighthouse(), SeaTrafficLayout.Lighthouse(Sea.LevelY)).ToList()),
            };

            foreach ((string name, (float near, float far)) in bands)
            {
                foreach ((string other, IEnumerable<Vector3> points) in others)
                    foreach (Vector3 p in points)
                    {
                        float across = Across(p);
                        float off = MathF.Max(near - across, across - far);
                        Assert.True(off >= CLEARANCE, $"{name}'s lane passes {off:F1} from {other} at {p}");
                    }
                _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} sweeps {1:F1} to {2:F1} across the lanes", name, near, far));
            }

            float between = MathF.Max(bands[1].Band.Near - bands[0].Band.Far, bands[0].Band.Near - bands[1].Band.Far);
            Assert.True(between >= CLEARANCE, $"the lanes pass {between:F1} from each other");
        }

        [Fact]
        public void NoLensTheIntroFliesComesNearALaneOrTheLighthouse()
        {
            //The run at the nearer platform flies low over the water out to some 1070 from the arena (SeaIntroShots), which is why
            //the lanes are not rings round the arena: a vessel could be anywhere along one when it plays. Seen to fire with the
            //lanes' bearing at -1.6, the platform's side of the sea.
            var bands = Vessels().Select(v => (v.Name, Band: Band(v.Mesh, v.Lane))).ToList();
            List<Vector3> rock = Placed(Lighthouse(), SeaTrafficLayout.Lighthouse(Sea.LevelY)).ToList();
            float farthest = 0f;
            foreach ((string what, Vector3 lens) in IntroLenses())
            {
                farthest = MathF.Max(farthest, new Vector2(lens.X, lens.Z).Length());
                float across = Across(lens);
                foreach ((string name, (float near, float far)) in bands)
                    Assert.True(MathF.Max(near - across, across - far) >= CLEARANCE, $"{what}: the lens at {lens} is on {name}'s lane");
                Assert.True(rock.Min(p => Vector3.Distance(p, lens)) >= CLEARANCE, $"{what}: the lens at {lens} is at the lighthouse");
            }

            Assert.True(farthest <= LENS_REACH, $"an intro lens stands {farthest:F0} from the arena, past the reach the horizon's sink is laid for");
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the intro's farthest lens stands {0:F0} from the arena", farthest));
        }

        [Fact]
        public void AVesselIsUnderTheHorizonFromEveryLensBeforeItWrapsRound()
        {
            //At either end of its lane a vessel jumps to the other: from any lens within LENS_REACH of the arena every vertex of it
            //has sunk under the waterline there (Rig.fx's drop, SeaTrafficLayout.Sink), so the jump is never seen. Seen to fire
            //with LANE_HALF_LENGTH at 2000.
            foreach ((string name, List<RigVertex> mesh, float lane, _, _) in Vessels())
                foreach (float end in new[] { -1f, 1f })
                {
                    float along = end * (SeaTrafficLayout.LANE_HALF_LENGTH - 1f);
                    foreach (float heading in new[] { -1f, 1f })
                    {
                        Matrix frame = SeaTrafficLayout.VesselAt(along, lane, heading, Sea.LevelY);
                        foreach (Vector3 p in Placed(mesh, frame))
                        {
                            float nearestLens = new Vector2(p.X, p.Z).Length() - LENS_REACH;
                            float above = p.Y - Sea.LevelY - SeaTrafficLayout.Sink(nearestLens);
                            Assert.True(above < 0f, $"{name} at its lane's end stands {above:F1} over the water from a lens {nearestLens:F0} off");
                        }
                    }
                }
        }

        [Fact]
        public void TheVesselsStartTheirPassageAgainWhenTheSeaIsSelected()
        {
            //The Game selects the sea (SceneRenderer.Prepare) wherever it sets its scene, and a player comes to the sea minutes
            //into a session: the vessels' zero is then the first draw after the selection, so the ship comes in at the opening
            //view's right edge again. Seen to fire with Read never setting the zero, and with the on-screen guard taken out.
            var clock = new SeaTrafficClock();

            //Never selected (the Testbed, the editor): the wall clock, which clock= pins
            Assert.Equal(300f, clock.Read(300f));
            Assert.Equal(300.5f, clock.Read(300.5f));

            //Selected off the screen: the next draw is the zero, and the clock runs on from it
            clock.Select();
            Assert.Equal(0f, clock.Read(420f));
            Assert.Equal(10f, clock.Read(430f));

            //Selected again while the sea is drawn every frame (the same scene chosen again): nothing moves
            float t = 430f;
            for (int frame = 0; frame < 60; frame++) clock.Read(t += 1f / 60f);
            clock.Select();
            float next = t + 1f / 60f;
            Assert.Equal(next - 420f, clock.Read(next));

            //Away for a while, and then selected: the zero again
            clock.Select();
            float back = next + SeaTrafficClock.ON_SCREEN + 30f;
            Assert.Equal(0f, clock.Read(back));

            //Away for a while without a selection (a page that draws no scene over it): the vessels' seconds run on
            float later = back + 60f;
            Assert.Equal(later - back, clock.Read(later));

            //And what the zero is for: the ship at the right edge of the opening view, coming left
            Matrix start = SeaTrafficLayout.ShipAt(0f, Sea.LevelY);
            Assert.True(Vector2.Dot(new Vector2(start.Translation.X, start.Translation.Z), SeaTrafficLayout.LaneAlong) > 0f, "the ship does not start right of its nearest point");
            Assert.True(Vector2.Dot(new Vector2(start.Right.X, start.Right.Z), SeaTrafficLayout.LaneAlong) < 0f, "the ship does not start heading left");
        }

        [Fact]
        public void TheVesselsSailOnTheClockAloneAtAnEvenPace()
        {
            //Sixty frames a second over a whole lane: each frame moves a vessel its speed's sixtieth along its lane, headed along it,
            //but for the one wrap a lane; and a lane later it is where it was. Seen to fire with the ship's speed doubled in ShipAt.
            foreach ((string name, _, float lane, Func<float, Matrix> at, float speed) in Vessels())
            {
                float period = 2f * SeaTrafficLayout.LANE_HALF_LENGTH / speed;
                int frames = (int)(period * 60f), wraps = 0;
                Matrix last = at(0f);
                for (int frame = 1; frame <= frames; frame++)
                {
                    float t = frame / 60f;
                    Matrix now = at(t);
                    float step = Vector3.Distance(now.Translation, last.Translation);
                    if (step > SeaTrafficLayout.LANE_HALF_LENGTH) wraps++;
                    else Assert.True(MathF.Abs(step - speed / 60f) < 0.01f, $"{name} moves {step:F3} in a frame at {t:F2} s");

                    Vector3 way = now.Right;
                    Assert.True(MathF.Abs(MathF.Abs(Vector2.Dot(new Vector2(way.X, way.Z), SeaTrafficLayout.LaneAlong)) - 1f) < 1e-4f, $"{name} heads off its lane at {t:F2} s");
                    Assert.True(MathF.Abs(Across(now.Translation) - lane) < 0.01f, $"{name} is off its lane at {t:F2} s");
                    last = now;
                }

                Assert.Equal(1, wraps);
                foreach (float t in new[] { 0f, 37f, 400f })
                    Assert.True(Vector3.Distance(at(t).Translation, at(t + period).Translation) < 0.05f, $"{name} is not where it was a lane later at {t} s");
            }
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void TheLighthouseStandsInThePlayViewOfEveryLevel(float aspect)
        {
            //The play view of every shipped level, played on the sea through the pause's Scene list, in every pose the overview
            //holds: the lantern, the tower's middle and the rock under it in the frame, the gun at rest and every ball the level
            //opens with off the line to them. Seen to fire with the lighthouse's bearing at -1.37 (the gun's wheel before its rock
            //on Analemma), and at -1.37 and 860 out before the rock was held (a ball of Balloon over the lantern, focused).
            Matrix frame = SeaTrafficLayout.Lighthouse(Sea.LevelY);
            Vector3 lantern = Vector3.Transform(new Vector3(0f, 53f, 0f), frame), middle = Vector3.Transform(new Vector3(0f, 30f, 0f), frame);
            Vector3 rock = Vector3.Transform(new Vector3(0f, 3f, 0f), frame);
            float widest = 0f;
            foreach (string file in Shipped.LevelFiles())
            {
                Opening open = Open(file, aspect);
                foreach ((string pose, Vector3 lens, Vector3 target, float fov) in open.Poses)
                {
                    string where = $"{open.Level} at {aspect:F2}, {pose}";
                    Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);
                    foreach ((string part, Vector3 point) in new[] { ("lantern", lantern), ("tower's middle", middle), ("rock", rock) })
                    {
                        Vector3 ndc = Project(point, viewProjection, out bool ahead);
                        Assert.True(InFrame(ndc, ahead), $"{where}: the lighthouse's {part} is out of the frame at {ndc.X:F2}, {ndc.Y:F2}");
                        Assert.False(BehindTheGun(lens, point, open.Gun, open.Aim), $"{where}: the gun stands before the lighthouse's {part}");
                        Assert.False(BehindABall(lens, point, open.Balls), $"{where}: a ball hangs over the lighthouse's {part}");
                        widest = MathF.Max(widest, MathF.Abs(ndc.X));
                    }
                }
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2}: the lighthouse out to {1:F2} of the half-frame", aspect, widest));
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void EachVesselCrossesTheOpeningPlayView(float aspect)
        {
            //The first level's opening pose (the lens on +Z looking at the cluster): over a whole lane, each vessel's middle stands in
            //the frame, out of the gun's way and over the horizon's cut for at least SHIP_SECONDS (57 at 4:3, the narrowest frame)
            //and YACHT_SECONDS, and both are in the frame for at least a third of the first two minutes from their zero, the
            //moment the Game selected the sea (SeaTrafficClock). Seen to fire with the ship's lane bearing at 0.5, behind the lens.
            //It also says how much of the time past those minutes either is in the view, and both, over the two lanes' common period.
            Opening open = Open(Shipped.LevelFiles().First(), aspect);
            (_, Vector3 lens, Vector3 target, float fov) = open.Poses[0];
            Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);

            var seen = new List<Func<float, bool>>();
            foreach ((string name, List<RigVertex> mesh, _, Func<float, Matrix> at, float speed) in Vessels())
            {
                float height = mesh.Max(v => v.Position.Y);
                bool Seen(float t)
                {
                    Vector3 middle = Vector3.Transform(new Vector3(0f, height * 0.5f, 0f), at(t));
                    bool over = SeaTrafficLayout.Sink(Vector2.Distance(new Vector2(middle.X, middle.Z), new Vector2(lens.X, lens.Z))) < height * 0.25f;
                    return over && InFrame(Project(middle, viewProjection, out bool ahead), ahead) && !BehindTheGun(lens, middle, open.Gun, open.Aim);
                }
                seen.Add(Seen);

                float period = 2f * SeaTrafficLayout.LANE_HALF_LENGTH / speed;
                int seconds = 0;
                for (float t = 0f; t < period; t += 1f) if (Seen(t)) seconds++;
                int early = 0;
                for (float t = 0f; t < 120f; t += 1f) if (Seen(t)) early++;

                float least = name == "the ship" ? SHIP_SECONDS : YACHT_SECONDS;
                Assert.True(seconds >= least, $"{name} is in the opening view {seconds} s a lane at {aspect:F2}");
                Assert.True(early >= 40, $"{name} is in the opening view {early} s of the first two minutes at {aspect:F2}");
                _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2}: {1} in the opening view {2} s a lane ({3:F0} s), {4} s of the first two minutes",
                    aspect, name, seconds, period, early));
            }

            //Both lanes come round together every 6000 s (500 s and 1200 s a lane)
            const int COMMON = 6000;
            int either = 0, both = 0;
            for (int t = 0; t < COMMON; t++)
            {
                bool ship = seen[0](t), yacht = seen[1](t);
                if (ship || yacht) either++;
                if (ship && yacht) both++;
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F2}: either vessel in the opening view {1:F0} % of the time, both {2:F1} %",
                aspect, 100f * either / COMMON, 100f * both / COMMON));
        }

        [Fact]
        public void ThePrologueFramesTheLighthouseAndAVesselAsTheDocsSay()
        {
            //The sea's prologue is not aimed at the lighthouse or the vessels (the run at the rig is held off them, above), so
            //what it frames of them is what its rolls happen to point at. Every roll and sun of IntroLenses, the shots in the
            //order they play, the vessels' clock starting with the prologue (a pick in the Scene list selects the sea and then
            //flies it; a level on the sea builds, selecting it, and then flies it), at the game's lens and 16:9, geometry only:
            //the lantern, and each vessel's middle over the horizon's cut, in the frame at some pose of some shot over the
            //water. docs/scenes/sea.md and SeaIntroShots say the lighthouse in about three rolls in four and a vessel in five in
            //six, so a change to the prologue or the layout that moves either says so here. Seen to fire with the lighthouse's
            //bearing at 1.8 (every roll) and -2.6 (five in six), and with the ship starting 1500 along its lane (a vessel in 57 %).
            const float ASPECT = 16f / 9f;
            Vector3 lantern = Vector3.Transform(new Vector3(0f, 53f, 0f), SeaTrafficLayout.Lighthouse(Sea.LevelY));
            var heights = Vessels().Select(v => v.Mesh.Max(p => p.Position.Y)).ToArray();
            var ats = Vessels().Select(v => v.At).ToArray();

            int rolls = 0, lighthouse = 0, ship = 0, yacht = 0, any = 0;
            foreach (Vector3? sun in Suns())
                for (int seed = 1; seed <= 24; seed++)
                {
                    rolls++;
                    bool sawLighthouse = false;
                    var sawVessel = new bool[2];
                    float clock = 0f;
                    foreach (IntroShot shot in SeaIntroShots.Shots(Sea, sun, GAME_FOV, new Random(seed)))
                    {
                        for (int i = 0; i < POSES; i++)
                        {
                            float u = i / (POSES - 1f);
                            shot.Pose(u, out Vector3 lens, out Vector3 target);
                            if (lens.Y < Sea.LevelY) continue;

                            Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up)
                                * Matrix.CreatePerspectiveFieldOfView(shot.FieldOfViewAt(u), ASPECT, 0.1f, 5000f);
                            sawLighthouse |= InFrame(Project(lantern, viewProjection, out bool ahead), ahead);

                            for (int v = 0; v < 2; v++)
                            {
                                Vector3 middle = Vector3.Transform(new Vector3(0f, heights[v] * 0.5f, 0f), ats[v](clock + u * shot.Seconds));
                                bool over = SeaTrafficLayout.Sink(Vector2.Distance(new Vector2(middle.X, middle.Z), new Vector2(lens.X, lens.Z))) < heights[v] * 0.25f;
                                sawVessel[v] |= over && InFrame(Project(middle, viewProjection, out bool vesselAhead), vesselAhead);
                            }
                        }
                        clock += shot.Seconds;
                    }

                    if (sawLighthouse) lighthouse++;
                    if (sawVessel[0]) ship++;
                    if (sawVessel[1]) yacht++;
                    if (sawVessel[0] || sawVessel[1]) any++;
                }

            _output.WriteLine($"of {rolls} rolls the prologue frames the lighthouse in {lighthouse}, the ship in {ship}, the yacht in {yacht}, a vessel in {any}");
            Assert.True(lighthouse >= 0.70f * rolls && lighthouse <= 0.80f * rolls,
                $"the prologue frames the lighthouse in {lighthouse} rolls of {rolls}, not about three in four as the docs say");
            Assert.True(any >= 0.78f * rolls && any <= 0.88f * rolls,
                $"the prologue frames a vessel in {any} rolls of {rolls}, not about five in six as the docs say");
        }
    }
}
