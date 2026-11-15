using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Moon's rover ruts and its one flag (#1114). The owner: "nowhere in the intro animation do I see a close-up of the
    /// wheel tracks of the lunar rover. Also there should be only one white flag on the surface, it should be bigger, and it
    /// should be visible in a close-up in the chapter's intro". Held over the real builder and the real traverse, on the
    /// shipped config and the ground's own mirror: the tracks shot runs low up the ruts' middle line, over the ground as both
    /// render paths draw it and across the sun, with the ruts in its frame and the flag closing it; and the flag stands beside
    /// the ruts where the play camera, as every Moon level fits it, sees it over the island's deck and clear of the gun.
    /// </summary>
    public class MoonIntroShotsTests
    {
        private const float FRAME = 1f / 60f;

        //The game's vertical field of view (GameplayScreen.GAME_FOV) and the window shapes it is played at: 16:9 the narrowest
        //it targets (BS3DGame's windowed default), the owner's 3840x1600, and 16:10 and 4:3 panels narrower still. The
        //projection is Hor+, so a narrower shape only crops the sides.
        private const float GAME_FOV = MathF.PI / 4.2f;
        private static readonly float[] ASPECTS = { 16f / 9f, 2.4f, 1.6f, 4f / 3f };
        public static readonly IEnumerable<object[]> Aspects = ASPECTS.Select(a => new object[] { a });

        //"A few units over the regolith": every frame of the tracks shot at least this far over the ground as each path draws
        //it, and no farther than this over the exact field under it
        private const float MIN_CLEARANCE = 1f;
        private const float MAX_HEIGHT = 6f;

        //How far off the ruts' drawn middle line the lens may wander: the rocks keep RoverTracks.HALF_WIDTH + 0.6 and their
        //own half-size off it (MoonPlanting's TRAVERSE_CLEAR), so a lens over the line has no rock under it
        private const float MAX_OFF_LINE = 0.5f;

        //How far from square across the ruts the sun may stand, horizontally, through the whole run: past it the light falls
        //along the berms, and looking down-sun a rut is flat
        private const float MAX_SUN_OFF_SQUARE_DEGREES = 40f;

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame; and the skip
        //hint's band along the bottom edge (up to 12.2 % of the frame's height, "Chapter intro prologues" in the docs)
        private const float FRAME_MARGIN = 0.05f;
        private const float SKIP_HINT_SHARE = 0.13f;

        //The ruts ahead of the lens that must stay in its frame, along the ruts from its own place
        private static readonly float[] RUTS_AHEAD = { 15f, 20f, 25f };

        //And in sight: of the RUTS_LOOKED_OVER units of ruts ahead of the lens, sampled every RUTS_SAMPLE, at least
        //MIN_RUTS_IN_SIGHT both in the frame and over the ground in every frame, at every shape. The frame's bottom edge
        //cuts the nearest and the brow of the belt's slope hides some farther on: the fewest seen is 9.5 (measured)
        private const float RUTS_LOOKED_OVER = 30f, RUTS_SAMPLE = 0.5f, MIN_RUTS_IN_SIGHT = 8f;

        //Sight lines are marched this far apart in plan, stopping a step short of the point (a point on the ground lies on
        //it): a fraction of the desktop grid's 3.34 units and the Potato grid's 10, whose drawn ground is flat between vertices
        private const float SIGHT_STEP = 0.25f;

        //The island as a sight line passes it: its stone nowhere over ArenaIsland.TOP_Y out to its RADIUS, and the desktop's
        //bank round it (IslandBerm) at most its first ring's 1.35 times its lift's 1.4 over the ground it is built off or
        //the drum's foot, out to its last ring's 4.4 times 1.6 past the drum (where IslandBerm.SurfaceY gives up)
        private const float BANK_ABOVE = 1.9f, BANK_REACH = ArenaIsland.RADIUS + 7.1f;
        private const float DRUM_FOOT_Y = ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT;

        //A close-up: at the shot's end the flag's pole spans at least this share of the frame's height
        private const float CLOSE_UP_SHARE = 0.45f;

        //The Moon's whole opening, the tour's last leg included, at most this long: 14.3 s before the tracks shot came in,
        //and the owner prefers faster over slower
        private const float MAX_OPENING_SECONDS = 15.5f;

        //Across a cut the lens must move or the view turn (the cities' figures, CityIntroShotsTests)
        private const float MIN_CUT_ANGLE_DEGREES = 22f, MIN_CUT_DISTANCE = 40f;

        //The ruts' end fade (RoverTracks.fxh's TRACK_END_FADE): past the flag they run on for more than it, so they are
        //whole where it stands
        private const float TRACK_END_FADE = 30f;

        //The sharpest bend the drawn ruts make, between two of their straight segments: what the traverse before #1114 bent
        //by in front of the arena (10.73 degrees, measured), which the front end showed as a curve. Spread over the whole
        //length the swing to the south met at 19.
        private const float MAX_BEND_DEGREES = 10.8f;

        //The gun as the play camera sees it at rest: its wheels stand WHEEL_TRACK 1.5 off the barrel's axis and their outer
        //faces 2.13 out (measured in the opening frame at 16:9), each WHEEL_RADIUS 1.15 nearer the lens at most; the barrel
        //and its magazine rise over them narrower. Held at 2.5 to either side
        private const float GUN_HALF_WIDTH = 2.5f, GUN_NEAR = 1.15f;

        //The flag in the play view at least this many pixels tall at 1080 lines (a 1.0-degree pole is 25)
        private const float MIN_PLAY_PIXELS = 18f;

        private static readonly MoonSceneConfig Moon = new();
        private static MoonTerrainConfig Terrain => Moon.Terrain;

        private static float Exact(float x, float z) => TerrainMirror.Moon(x, z, Terrain);

        private static float Desktop(float x, float z) =>
            TerrainMirror.OnGrid(Exact, MoonBackdrop.MOON_GRID_N, MoonBackdrop.MOON_EXTENT, x, z);

        private static float Potato(float x, float z) =>
            TerrainMirror.OnGrid(Exact, PotatoBackdrop.RELIEF_CELLS + 1, PotatoBackdrop.MOON_EXTENT * 2f, x, z);

        //The ground as each render path draws it, the planting's rocks and flag standing on each
        private static readonly (string Name, Func<float, float, float> Height)[] GROUNDS = { ("desktop", Desktop), ("Potato", Potato) };

        private static Vector3 Sun =>
            SceneRenderer.DirectionFromElevationAzimuth(Moon.Lighting.SunElevationDegrees, Moon.Lighting.SunAzimuthDegrees);

        //The flag as the planting stands it: its foot on the ground as the desktop draws it, its pole and bar at its size, and
        //a cloth's width to either hand at the bar, since which way the bar reaches is the planting's own sum
        private static Vector3 FlagFoot => FootOn(Desktop);
        private static Vector3 FootOn(Func<float, float, float> ground) =>
            new(MoonPlanting.FLAG.X, ground(MoonPlanting.FLAG.X, MoonPlanting.FLAG.Y), MoonPlanting.FLAG.Y);
        private const float POLE = MoonPlanting.FlagPoleHeight * MoonPlanting.FLAG_SIZE;
        private const float BAR = MoonPlanting.FlagBarY * MoonPlanting.FLAG_SIZE;
        private const float CLOTH = MoonPlanting.FlagWidth * MoonPlanting.FLAG_SIZE;
        private const float CLOTH_HEM = (MoonPlanting.FlagBarY - MoonPlanting.FlagDrop) * MoonPlanting.FLAG_SIZE;

        //What hides a point from a lens over (x, z): the ground as a path draws it, and round the arena its stone and its bank
        private static float Occluder(Func<float, float, float> ground, float x, float z)
        {
            float y = ground(x, z);
            float r2 = x * x + z * z;
            if (r2 <= BANK_REACH * BANK_REACH) y = MathF.Max(y, MathF.Max(y, DRUM_FOOT_Y) + BANK_ABOVE);
            if (r2 <= ArenaIsland.RADIUS * ArenaIsland.RADIUS) y = MathF.Max(y, ArenaIsland.TOP_Y);
            return y;
        }

        //How far the sight line from the lens to a point passes over what would hide it, at its lowest: negative, hidden. A
        //higher point over the same plan point is seen along a line higher everywhere, so a point of the pole in sight puts
        //all of the pole above it in sight too
        private static float SightMargin(Vector3 lens, Vector3 point, Func<float, float, float> ground)
        {
            Vector2 plan = new(point.X - lens.X, point.Z - lens.Z);
            float length = plan.Length();
            float lowest = float.MaxValue;
            for (float s = SIGHT_STEP; s < length - SIGHT_STEP; s += SIGHT_STEP)
            {
                float t = s / length;
                float x = lens.X + plan.X * t, z = lens.Z + plan.Y * t;
                lowest = MathF.Min(lowest, lens.Y + (point.Y - lens.Y) * t - Occluder(ground, x, z));
            }
            return lowest;
        }

        private static IEnumerable<Vector3> FlagPoints(Vector3 lens)
        {
            Vector3 foot = FlagFoot;
            Vector3 toFlag = foot - lens;
            Vector3 across = Vector3.Normalize(Vector3.Cross(toFlag, Vector3.Up));

            yield return foot;
            yield return foot + Vector3.Up * POLE;
            yield return foot + Vector3.Up * BAR + across * CLOTH;
            yield return foot + Vector3.Up * BAR - across * CLOTH;
        }

        //A point in normalized device coordinates (x right, y up, -1..1 across the frame), and whether it is ahead of the lens
        private static Vector3 Project(Vector3 point, Matrix view, Matrix projection, out bool ahead)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), view * projection);
            ahead = clip.W > 0.1f;
            return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
        }

        private static bool InFrame(Vector3 ndc, bool ahead) =>
            ahead && MathF.Abs(ndc.X) <= 1f - FRAME_MARGIN && MathF.Abs(ndc.Y) <= 1f - FRAME_MARGIN;

        //The distance along the drawn ruts to the point of them nearest a plan point
        private static float AlongRuts(Vector2 at)
        {
            float best = float.MaxValue, bestAlong = 0f;
            for (float a = 0f; a < 600f; a += 0.25f)
            {
                float d = Vector2.DistanceSquared(MoonPlanting.BesideTrack(MoonPlanting.TRAVERSE_PATH, a, 0f, out _), at);
                if (d < best) { best = d; bestAlong = a; }
            }
            return bestAlong;
        }

        private static float RutsLength()
        {
            float length = 0f;
            for (int i = 1; i < MoonPlanting.TRAVERSE_PATH.Length; i++)
                length += Vector2.Distance(MoonPlanting.TRAVERSE_PATH[i - 1], MoonPlanting.TRAVERSE_PATH[i]);
            return length;
        }

        private static IEnumerable<(Vector3 Lens, Vector3 Target, float Fov)> Frames(IntroShot shot)
        {
            int frames = (int)(shot.Seconds / FRAME);
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)(frames - 1);
                shot.Pose(t, out Vector3 lens, out Vector3 target);
                yield return (lens, target, shot.FieldOfViewAt(t));
            }
        }

        [Fact]
        public void TheFlagStandsBesideTheRutsAndTheyRunOnPastIt()
        {
            Vector2 flag = MoonPlanting.FLAG;
            float off = RoverTracks.Distance(MoonPlanting.TRAVERSE_PATH, flag.X, flag.Y);

            //Clear of the ruts and their berms, and near enough to be the ruts' own flag
            Assert.InRange(off, RoverTracks.HALF_WIDTH + 2f, 6f);
            Assert.True(AlongRuts(flag) + TRACK_END_FADE < RutsLength(), "the ruts fade under dust before they reach the flag");
        }

        [Fact]
        public void TheRutsBendNoSharperThanTheyAlwaysDidInFrontOfTheArena()
        {
            Vector2[] path = MoonPlanting.TRAVERSE_PATH;
            for (int i = 1; i + 1 < path.Length; i++)
            {
                float bend = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(
                    Vector2.Dot(Vector2.Normalize(path[i] - path[i - 1]), Vector2.Normalize(path[i + 1] - path[i])), -1f, 1f)));
                Assert.True(bend <= MAX_BEND_DEGREES, $"the ruts bend {bend:F1} degrees at their point {i}");
            }
        }

        [Fact]
        public void TheTracksShotKeepsLowOverTheGroundAsEveryPathDrawsIt()
        {
            int frame = 0;
            foreach ((Vector3 lens, _, _) in Frames(MoonIntroShots.Tracks(Terrain)))
            {
                float drawn = MathF.Max(Exact(lens.X, lens.Z), MathF.Max(Desktop(lens.X, lens.Z), Potato(lens.X, lens.Z)));
                Assert.True(lens.Y >= drawn + MIN_CLEARANCE, $"frame {frame}: the lens is {lens.Y - drawn:F2} over the ground as drawn");
                Assert.True(lens.Y <= Exact(lens.X, lens.Z) + MAX_HEIGHT, $"frame {frame}: the lens is {lens.Y - Exact(lens.X, lens.Z):F2} up, not low");
                frame++;
            }
        }

        [Fact]
        public void TheTracksShotRunsUpTheRutsMiddleLineTowardsTheFlag()
        {
            float previous = float.MaxValue;
            int frame = 0;
            foreach ((Vector3 lens, _, _) in Frames(MoonIntroShots.Tracks(Terrain)))
            {
                float off = RoverTracks.Distance(MoonPlanting.TRAVERSE_PATH, lens.X, lens.Z);
                Assert.True(off <= MAX_OFF_LINE, $"frame {frame}: the lens is {off:F2} off the ruts' middle line");

                float toFlag = Vector2.Distance(new Vector2(lens.X, lens.Z), MoonPlanting.FLAG);
                Assert.True(toFlag <= previous + 1e-3f, $"frame {frame}: the lens draws away from the flag");
                previous = toFlag;
                frame++;
            }
        }

        [Fact]
        public void TheSunRakesAcrossTheRutsTheWholeRun()
        {
            Vector2 sun = Vector2.Normalize(new Vector2(Sun.X, Sun.Z));
            Vector3 last = default;
            int frame = 0;
            foreach ((Vector3 lens, _, _) in Frames(MoonIntroShots.Tracks(Terrain)))
            {
                if (frame++ > 0)
                {
                    Vector2 heading = Vector2.Normalize(new Vector2(lens.X - last.X, lens.Z - last.Z));
                    float offSquare = MathHelper.ToDegrees(MathF.Asin(MathF.Abs(Vector2.Dot(heading, sun))));
                    Assert.True(offSquare <= MAX_SUN_OFF_SQUARE_DEGREES, $"frame {frame}: the sun stands {offSquare:F1} degrees off square across the ruts");
                }
                last = lens;
            }
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void TheTracksShotFramesTheRutsAndClosesOnTheFlag(float aspect)
        {
            List<(Vector3 Lens, Vector3 Target, float Fov)> frames = Frames(MoonIntroShots.Tracks(Terrain)).ToList();

            for (int i = 0; i < frames.Count; i += 6)
            {
                (Vector3 lens, Vector3 target, float fov) = frames[i];
                Matrix view = Matrix.CreateLookAt(lens, target, Vector3.Up);
                Matrix projection = Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);

                float along = AlongRuts(new Vector2(lens.X, lens.Z));
                foreach (float ahead in RUTS_AHEAD)
                {
                    Vector2 rut = MoonPlanting.BesideTrack(MoonPlanting.TRAVERSE_PATH, along + ahead, 0f, out _);
                    Vector3 ndc = Project(new Vector3(rut.X, Desktop(rut.X, rut.Y), rut.Y), view, projection, out bool inFront);
                    Assert.True(InFrame(ndc, inFront), $"frame {i} at {aspect:F2}: the ruts {ahead} on are out of the frame at {ndc.X:F2}, {ndc.Y:F2}");
                }

                foreach (Vector3 point in FlagPoints(lens))
                {
                    Vector3 ndc = Project(point, view, projection, out bool inFront);
                    Assert.True(InFrame(ndc, inFront), $"frame {i} at {aspect:F2}: the flag is out of the frame at {ndc.X:F2}, {ndc.Y:F2}");
                }
            }

            (Vector3 endLens, Vector3 endTarget, float endFov) = frames[^1];
            Matrix endView = Matrix.CreateLookAt(endLens, endTarget, Vector3.Up);
            Matrix endProjection = Matrix.CreatePerspectiveFieldOfView(endFov, aspect, 0.1f, 5000f);
            Vector3 foot = Project(FlagFoot, endView, endProjection, out _);
            Vector3 top = Project(FlagFoot + Vector3.Up * POLE, endView, endProjection, out _);

            Assert.True(foot.Y >= -1f + 2f * SKIP_HINT_SHARE, $"at {aspect:F2} the flag's foot ends in the skip hint's band, at {foot.Y:F2}");
            Assert.True((top.Y - foot.Y) / 2f >= CLOSE_UP_SHARE, $"at {aspect:F2} the flag ends {(top.Y - foot.Y) / 2f:P0} of the frame tall, not a close-up");
        }

        /// <summary>
        /// In the frame is not in sight: the tracks shot runs a few units over a slope that rises to the flag, and its brow
        /// hides what lies past it. Over the ground as each path draws it, every third frame: the ruts ahead are seen, the
        /// flag's bar and so the cloth's top edge and the pole over it (the brow hides the foot until the lens tops it), and
        /// at the end the whole flag from its foot.
        /// </summary>
        [Fact]
        public void TheTracksShotSeesTheRutsAndTheFlagOverTheGround()
        {
            List<(Vector3 Lens, Vector3 Target, float Fov)> frames = Frames(MoonIntroShots.Tracks(Terrain)).ToList();
            var inSight = new List<Vector3>();

            foreach ((string name, Func<float, float, float> ground) in GROUNDS)
            {
                Vector3 foot = FootOn(ground);
                for (int i = 0; i < frames.Count; i += 3)
                {
                    (Vector3 lens, Vector3 target, float fov) = frames[i];
                    float bar = SightMargin(lens, foot + Vector3.Up * BAR, ground);
                    Assert.True(bar >= 0f, $"frame {i} over the {name} ground: the flag's bar is {-bar:F2} under the ground's brow");

                    inSight.Clear();
                    float along = AlongRuts(new Vector2(lens.X, lens.Z));
                    for (float ahead = RUTS_SAMPLE; ahead <= RUTS_LOOKED_OVER; ahead += RUTS_SAMPLE)
                    {
                        Vector2 rut = MoonPlanting.BesideTrack(MoonPlanting.TRAVERSE_PATH, along + ahead, 0f, out _);
                        Vector3 point = new(rut.X, ground(rut.X, rut.Y), rut.Y);
                        if (SightMargin(lens, point, ground) >= 0f) inSight.Add(point);
                    }

                    Matrix view = Matrix.CreateLookAt(lens, target, Vector3.Up);
                    foreach (float aspect in ASPECTS)
                    {
                        Matrix projection = Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);
                        float seen = 0f;
                        foreach (Vector3 point in inSight)
                            if (InFrame(Project(point, view, projection, out bool ahead), ahead)) seen += RUTS_SAMPLE;
                        Assert.True(seen >= MIN_RUTS_IN_SIGHT, $"frame {i} at {aspect:F2} over the {name} ground: {seen} units of the ruts ahead in the frame and in sight");
                    }
                }

                (Vector3 endLens, _, _) = frames[^1];
                float end = SightMargin(endLens, foot, ground);
                Assert.True(end >= 0f, $"over the {name} ground the shot ends with the flag's foot {-end:F2} under the ground's brow");
            }
        }

        [Fact]
        public void TheMoonOpensOnThePlainThenTheTracksThenTheEarthAndNoLonger()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                IntroShot[] shots = MoonIntroShots.Build(Moon, Sun, new Random(seed));

                Assert.Equal(new[] { "the plain", "the tracks", "the Earth" }, shots.Select(shot => shot.Name).ToArray());
                Assert.True(shots.Sum(shot => shot.Seconds) + ChapterIntro.ARRIVAL_SECONDS <= MAX_OPENING_SECONDS,
                    $"the opening runs {shots.Sum(shot => shot.Seconds) + ChapterIntro.ARRIVAL_SECONDS:F1} s");

                for (int i = 0; i + 1 < shots.Length; i++)
                {
                    shots[i].Pose(1f, out Vector3 fromLens, out Vector3 fromTarget);
                    shots[i + 1].Pose(0f, out Vector3 toLens, out Vector3 toTarget);

                    float angle = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(
                        Vector3.Dot(Vector3.Normalize(fromTarget - fromLens), Vector3.Normalize(toTarget - toLens)), -1f, 1f)));
                    float distance = Vector3.Distance(fromLens, toLens);
                    Assert.True(angle >= MIN_CUT_ANGLE_DEGREES || distance >= MIN_CUT_DISTANCE,
                        $"seed {seed}: the cut from '{shots[i].Name}' turns {angle:F0} degrees and moves {distance:F0} units");
                }
            }
        }

        /// <summary>
        /// The play camera's opening pose on every Moon level, as the session fits it (<see cref="GameCameraFit"/>, the same
        /// lines over the same helpers as GunPassTests): the flag stands in the frame, its cloth in sight over the island and the
        /// ground between, clear of the gun and under the field, at least <see cref="MIN_PLAY_PIXELS"/> tall at 1080 lines.
        /// </summary>
        [Theory]
        [MemberData(nameof(Aspects))]
        public void ThePlayViewSeesTheFlagOnEveryMoonLevel(float aspect)
        {
            int levels = 0;
            foreach (string file in Shipped.LevelFiles())
            {
                Level level = Level.Load(file);
                if (level.Scene != SceneKind.Moon) continue;
                levels++;

                BallsMap map = new(level.Map);
                map.Center();
                Vector3 offset = ClusterHang.FitWorldOffset(map, out float fieldTopY);
                float ceilingCentreY = CeilingPlate.CentreYAbove(fieldTopY);
                float halfX = CeilingPlate.FootprintFor(map.StageSizeX) * Constants.HALF;
                float halfZ = CeilingPlate.FootprintFor(map.StageSizeZ) * Constants.HALF;
                float framedTop = MathF.Min(CeilingPlate.TopFaceY(ceilingCentreY), offset.Y + 17f / Constants.SQRT_TWO);

                //The gun as a session fits it, its barrel's reach the rig's measured 3.40 rounded up (GunPassTests)
                Cannon cannon = new(new Vector3(0f, 5f, 0f), 20f);
                CameraFit fit = GameCameraFit.Solve(cannon, 4f, halfX, halfZ, offset.Y, framedTop, GAME_FOV, aspect);
                cannon.OrbitRadius = fit.CannonOrbitRadius;

                Vector3 lens = GameCameraFit.CameraPosition(cannon, fit.CameraDistance);
                Vector3 target = new(cannon.OrbitCenter.X, fit.CameraTargetY, cannon.OrbitCenter.Z);
                Matrix view = Matrix.CreateLookAt(lens, target, Vector3.Up);
                Matrix projection = Matrix.CreatePerspectiveFieldOfView(GAME_FOV, aspect, 0.1f, 5000f);
                string name = Path.GetFileName(file);

                foreach (Vector3 point in FlagPoints(lens))
                {
                    Vector3 ndc = Project(point, view, projection, out bool inFront);
                    Assert.True(InFrame(ndc, inFront), $"{name} at {aspect:F2}: the flag is out of the frame at {ndc.X:F2}, {ndc.Y:F2}");
                }

                //In sight: the line to the cloth's hem, and so the whole cloth and the pole over it, clears the island's stone
                //and bank and the plain and the belt's slope past them, as each path draws them. The lens stands just over
                //the deck and the flag far over the lens, so the line climbs all the way and what could hide the flag is the
                //ground nearest it; the bottom of the pole is behind it (0.15 on the desktop, 0.6 on the Potato path)
                foreach ((string groundName, Func<float, float, float> ground) in GROUNDS)
                {
                    float margin = SightMargin(lens, FootOn(ground) + Vector3.Up * CLOTH_HEM, ground);
                    Assert.True(margin >= 0f, $"{name} at {aspect:F2} over the {groundName} ground: the cloth's hem is {-margin:F2} under the ground");
                }
                Vector3 foot = FlagFoot;

                //Clear of the gun: off to the side of its carriage as the lens sees it
                Vector3 forward = Vector3.Normalize(new Vector3(target.X - lens.X, 0f, target.Z - lens.Z));
                Vector3 right = Vector3.Cross(forward, Vector3.Up);
                float gunDepth = Vector3.Dot(cannon.Position - lens, forward) - GUN_NEAR;
                float gunSide = MathF.Atan2(GUN_HALF_WIDTH, gunDepth);
                float flagSide = MathF.Atan2(MathF.Abs(Vector3.Dot(foot - lens, right)), Vector3.Dot(foot - lens, forward));
                Assert.True(flagSide > gunSide, $"{name} at {aspect:F2}: the flag stands {MathHelper.ToDegrees(flagSide):F1} degrees off the axis, behind the gun's {MathHelper.ToDegrees(gunSide):F1}");

                //Not behind the cluster: beside the field's footprint in the frame, or under the lowest ball the level opens
                //with (GunPassTests' sphere's bottom); the field's empty lower levels hold nothing drawn
                float topLevelY = BallsConstraintsBuilder.CeilingRestY(ceilingCentreY);
                XZLevel size = map.GetStaticBallsArraySize();
                float clusterBottomY = topLevelY - (size.Level - 1 - map.GetLowestOccupiedLevel()) / Constants.SQRT_TWO - BallRenderSet.BALL_RADIUS;
                float fieldLeft = float.MaxValue, fieldRight = float.MinValue, clusterBottom = float.MaxValue;
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = Project(new Vector3(x * halfX, clusterBottomY, z * halfZ), view, projection, out _);
                        fieldLeft = MathF.Min(fieldLeft, corner.X);
                        fieldRight = MathF.Max(fieldRight, corner.X);
                        clusterBottom = MathF.Min(clusterBottom, corner.Y);
                    }
                Vector3 poleTop = Project(foot + Vector3.Up * POLE, view, projection, out _);
                Vector3 poleFoot = Project(foot, view, projection, out _);
                bool beside = poleTop.X < fieldLeft || poleTop.X > fieldRight;
                Assert.True(beside || poleTop.Y < clusterBottom, $"{name} at {aspect:F2}: the cluster hangs over the flag in the frame");

                float pixels = (poleTop.Y - poleFoot.Y) * 0.5f * 1080f;
                Assert.True(pixels >= MIN_PLAY_PIXELS, $"{name} at {aspect:F2}: the flag is {pixels:F1} px tall at 1080 lines");
            }

            Assert.True(levels > 0, "no shipped level is on the Moon");
        }
    }
}
