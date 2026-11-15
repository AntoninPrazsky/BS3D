using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// Mars's rover ruts in the chapter intro (#1115). The owner: "the intro animation does not deal with the rover tracks".
    /// Held over the real builder, the real traverse and the shipped config: the ruts bend gently enough to be seen close,
    /// the tracks shot runs down their middle line over Mars's ceiling with the ruts ahead of it in its frame and the island
    /// they lead to closing it, it opens the prologue where the tier draws the ruts and the plain opens it where it does
    /// not, and the opening stays short.
    /// </summary>
    public class MarsIntroShotsTests
    {
        private const float FRAME = 1f / 60f;

        //The window shapes the game is played at: 16:9 the narrowest it targets, the owner's 3840x1600, 16:10 and 4:3
        private static readonly float[] ASPECTS = { 16f / 9f, 2.4f, 1.6f, 4f / 3f };
        public static readonly IEnumerable<object[]> Aspects = ASPECTS.Select(a => new object[] { a });

        //How far off the ruts' drawn middle line the lens may wander: the stones keep MarsBackdrop's TRACK_CLEAR (5) and their
        //own size off it, so a lens over the line has none under it
        private const float MAX_OFF_LINE = 0.5f;

        //Low: no frame more than this over the ceiling (its clearance and what the drone's rounding adds), so smooth is never
        //bought by flying high. 2.95 at the most, measured
        private const float MAX_OVER_CEILING = MarsIntroShots.CLEARANCE + 1f;

        //The lens is checked at sixty frames between the path's points, where the spline may sag a hair under them
        private const float CLEARANCE_TOLERANCE = 0.05f;

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame
        private const float FRAME_MARGIN = 0.05f;

        //The ruts ahead of the lens that must stay in its frame, along them from its own place; and of the RUTS_LOOKED_OVER
        //units ahead, sampled every RUTS_SAMPLE, at least MIN_RUTS_IN_FRAME in it in every frame at every shape (19 at the
        //fewest, measured). Mars has no mirror of its ground, so in the frame is all that can be held: not in sight
        private static readonly float[] RUTS_AHEAD = { 15f, 20f, 25f };
        private const float RUTS_LOOKED_OVER = 30f, RUTS_SAMPLE = 0.5f, MIN_RUTS_IN_FRAME = 15f;

        //The island at the end: its top's rim to either hand and a point over its middle where the cluster hangs
        private const float CLUSTER_OVER_ISLAND = 15f;

        //The sharpest bend the drawn ruts may make between two of their straight segments: the Moon's (MoonIntroShotsTests,
        //10.8) rounded down. The first traverse met at 18.2 in its swing (measured), a polygon in the shot's close-up
        private const float MAX_BEND_DEGREES = 10f;

        //Mars's whole opening, the tour's last leg included, at most this long: 14.1 s before the tracks shot came in, and the
        //owner prefers faster over slower
        private const float MAX_OPENING_SECONDS = 14.6f;

        //Across a cut the lens must move or the view turn (the cities' figures, CityIntroShotsTests)
        private const float MIN_CUT_ANGLE_DEGREES = 22f, MIN_CUT_DISTANCE = 40f;

        private static readonly MarsSceneConfig Mars = new();
        private static MarsTerrainConfig Terrain => Mars.Terrain;

        private static float Ceiling(float x, float z) => OffworldGround.MarsCeiling(x, z, Terrain);

        private static readonly RutsWalk Ruts = new(MarsTracks.TRAVERSE);

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

        private static bool InFrame(Vector3 point, Matrix viewProjection)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
            if (clip.W <= 0.1f) return false;
            return MathF.Abs(clip.X / clip.W) <= 1f - FRAME_MARGIN && MathF.Abs(clip.Y / clip.W) <= 1f - FRAME_MARGIN;
        }

        //The ruts' middle line on the plain, this far along them
        private static Vector3 RutAt(float along)
        {
            Vector2 at = Ruts.At(along);
            return new Vector3(at.X, Terrain.LevelY, at.Y);
        }

        [Fact]
        public void TheRutsBendGentlyEnoughToBeSeenClose()
        {
            Vector2[] path = MarsTracks.TRAVERSE_PATH;
            for (int i = 1; i + 1 < path.Length; i++)
            {
                float bend = MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(
                    Vector2.Dot(Vector2.Normalize(path[i] - path[i - 1]), Vector2.Normalize(path[i + 1] - path[i])), -1f, 1f)));
                Assert.True(bend <= MAX_BEND_DEGREES, $"the ruts bend {bend:F1} degrees at their point {i}");
            }
        }

        [Fact]
        public void TheTracksShotKeepsLowOverTheCeiling()
        {
            int frame = 0;
            foreach ((Vector3 lens, _, _) in Frames(MarsIntroShots.Tracks(Terrain)))
            {
                float over = lens.Y - Ceiling(lens.X, lens.Z);
                Assert.True(over >= MarsIntroShots.CLEARANCE - CLEARANCE_TOLERANCE, $"frame {frame}: the lens is {over:F2} over the ceiling");
                Assert.True(over <= MAX_OVER_CEILING, $"frame {frame}: the lens is {over:F2} over the ceiling, not low");
                frame++;
            }
        }

        [Fact]
        public void TheTracksShotRunsDownTheRutsMiddleLineTowardsTheArena()
        {
            float previous = float.MaxValue;
            int frame = 0;
            foreach ((Vector3 lens, _, _) in Frames(MarsIntroShots.Tracks(Terrain)))
            {
                float off = RoverTracks.Distance(MarsTracks.TRAVERSE_PATH, lens.X, lens.Z);
                Assert.True(off <= MAX_OFF_LINE, $"frame {frame}: the lens is {off:F2} off the ruts' middle line");

                float toArena = new Vector2(lens.X, lens.Z).Length();
                Assert.True(toArena <= previous + 1e-3f, $"frame {frame}: the lens draws away from the arena");
                previous = toArena;
                frame++;
            }
        }

        [Theory]
        [MemberData(nameof(Aspects))]
        public void TheTracksShotFramesTheRutsAheadAndEndsOnTheIsland(float aspect)
        {
            List<(Vector3 Lens, Vector3 Target, float Fov)> frames = Frames(MarsIntroShots.Tracks(Terrain)).ToList();

            for (int i = 0; i < frames.Count; i += 3)
            {
                (Vector3 lens, Vector3 target, float fov) = frames[i];
                Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up)
                    * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);

                float along = Ruts.AlongNearest(new Vector2(lens.X, lens.Z));
                foreach (float ahead in RUTS_AHEAD)
                    Assert.True(InFrame(RutAt(along + ahead), viewProjection), $"frame {i} at {aspect:F2}: the ruts {ahead} on are out of the frame");

                float inFrame = 0f;
                for (float ahead = RUTS_SAMPLE; ahead <= RUTS_LOOKED_OVER; ahead += RUTS_SAMPLE)
                    if (InFrame(RutAt(along + ahead), viewProjection)) inFrame += RUTS_SAMPLE;
                Assert.True(inFrame >= MIN_RUTS_IN_FRAME, $"frame {i} at {aspect:F2}: {inFrame} units of the ruts ahead in the frame");
            }

            (Vector3 endLens, Vector3 endTarget, float endFov) = frames[^1];
            Matrix end = Matrix.CreateLookAt(endLens, endTarget, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(endFov, aspect, 0.1f, 5000f);
            Vector3 forward = Vector3.Normalize(new Vector3(endTarget.X - endLens.X, 0f, endTarget.Z - endLens.Z));
            Vector3 across = Vector3.Cross(forward, Vector3.Up) * ArenaIsland.RADIUS;
            Vector3 top = new(0f, ArenaIsland.TOP_Y, 0f);

            Assert.True(InFrame(top - across, end) && InFrame(top + across, end), $"at {aspect:F2} the shot ends with the island cut by the frame");
            Assert.True(InFrame(top + Vector3.Up * CLUSTER_OVER_ISLAND, end), $"at {aspect:F2} the shot ends with the cluster out of the frame");
        }

        [Fact]
        public void TheTracksOpenThePrologueWhereTheyAreDrawnAndThePlainWhereTheyAreNot()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                IntroShot[] shots = MarsIntroShots.Build(Mars, tracksDrawn: true, new Random(seed));
                Assert.Equal("the tracks", shots[0].Name);
                Assert.Equal("Phobos", shots[^1].Name);
                Assert.True(shots.Sum(shot => shot.Seconds) + ChapterIntro.ARRIVAL_SECONDS <= MAX_OPENING_SECONDS,
                    $"seed {seed}: the opening runs {shots.Sum(shot => shot.Seconds) + ChapterIntro.ARRIVAL_SECONDS:F1} s");

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

                Assert.Equal("the plain", MarsIntroShots.Build(Mars, tracksDrawn: false, new Random(seed))[0].Name);
            }
        }
    }
}
