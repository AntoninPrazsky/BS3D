using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The Moon's rover ruts and its one flag (#1114). The owner: "nowhere in the intro animation do I see a close-up of the
    /// wheel tracks of the lunar rover. Also there should be only one white flag on the surface, it should be bigger, and it
    /// should be visible in a close-up in the chapter's intro". Held over the real builder and the real traverse, on the
    /// shipped config and the ground's own mirror: the tracks shot runs low up the ruts, over the ground as both render paths
    /// draw it, across the sun, with the ruts in its frame and the flag closing it; and the one flag stands beside the ruts
    /// where the play camera, as every Moon level fits it, sees it over the island's deck, clear of the gun.
    /// </summary>
    public class MoonIntroShotsTests
    {
        private readonly ITestOutputHelper _out;
        public MoonIntroShotsTests(ITestOutputHelper output) => _out = output;

        private const float FRAME = 1f / 60f;

        //The game's vertical field of view (GameplayScreen.GAME_FOV) and the window shapes the game is played at: 16:9 the
        //narrowest it targets (BS3DGame's windowed default), the owner's 3840x1600, and 16:10 and 4:3 panels narrower still.
        //The projection is Hor+, so a narrower shape only crops the sides.
        private const float GAME_FOV = MathF.PI / 4.2f;
        private static readonly float[] ASPECTS = { 16f / 9f, 2.4f, 1.6f, 4f / 3f };

        //"A few units over the regolith": every frame of the tracks shot at least this far over the ground as each path
        //draws it, and no farther than this over the exact field
        private const float MIN_CLEARANCE = 1f;
        private const float MAX_HEIGHT = 5f;

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame; and the skip
        //hint's band along the bottom edge (up to 12.2 % of the frame's height, docs/game-feedback/chapter-intro-prologues.md)
        private const float FRAME_MARGIN = 0.05f;
        private const float SKIP_HINT_SHARE = 0.13f;

        private static readonly MoonSceneConfig Moon = new();
        private static MoonTerrainConfig Terrain => Moon.Terrain;

        private static float Exact(float x, float z) => TerrainMirror.Moon(x, z, Terrain);

        private static float Desktop(float x, float z) =>
            TerrainMirror.OnGrid(Exact, MoonBackdrop.MOON_GRID_N, MoonBackdrop.MOON_EXTENT, x, z);

        private static float Potato(float x, float z) =>
            TerrainMirror.OnGrid(Exact, PotatoBackdrop.RELIEF_CELLS + 1, PotatoBackdrop.MOON_EXTENT * 2f, x, z);

        private static Vector3 Sun =>
            SceneRenderer.DirectionFromElevationAzimuth(Moon.Lighting.SunElevationDegrees, Moon.Lighting.SunAzimuthDegrees);

        //The flag as the planting stands it: its foot on the ground as the desktop draws it, its pole and its bar's height at
        //its size (MoonPlanting.FlagBarY at 1), and a cloth's width either way, since which way the bar reaches is the
        //planting's own sum
        private static Vector3 FlagFoot => new(MoonPlanting.FLAG.X, Desktop(MoonPlanting.FLAG.X, MoonPlanting.FLAG.Y), MoonPlanting.FLAG.Y);
        private const float POLE = 2.4f * MoonPlanting.FLAG_SIZE;
        private const float BAR = MoonPlanting.FlagBarY * MoonPlanting.FLAG_SIZE;
        private const float CLOTH = 1.2f * MoonPlanting.FLAG_SIZE;

        [Fact]
        public void Probe()
        {
            IntroShot shot = MoonIntroShots.Tracks(Terrain);
            int frames = (int)(shot.Seconds / FRAME);
            float minClear = float.MaxValue, maxHeight = 0f, length = 0f;
            Vector3 last = default;
            for (int i = 0; i < frames; i++)
            {
                shot.Pose(i / (float)(frames - 1), out Vector3 p, out _);
                float drawn = MathF.Max(Exact(p.X, p.Z), MathF.Max(Desktop(p.X, p.Z), Potato(p.X, p.Z)));
                minClear = MathF.Min(minClear, p.Y - drawn);
                maxHeight = MathF.Max(maxHeight, p.Y - Exact(p.X, p.Z));
                if (i > 0) length += Vector3.Distance(p, last);
                last = p;
            }
            shot.Pose(0f, out Vector3 a, out Vector3 look);
            shot.Pose(1f, out Vector3 b, out _);
            Vector2[] old =
            {
                new(235f, -150f), new(170f, -128f), new(112f, -112f), new(60f, -100f), new(18f, -96f), new(-10f, -100f),
                new(-34f, -112f), new(-72f, -126f), new(-120f, -146f), new(-170f, -176f), new(-220f, -214f),
            };
            static float MaxTurn(Vector2[] path)
            {
                float max = 0f;
                for (int i = 1; i + 1 < path.Length; i++)
                    max = MathF.Max(max, MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector2.Dot(Vector2.Normalize(path[i] - path[i - 1]), Vector2.Normalize(path[i + 1] - path[i])), -1f, 1f))));
                return max;
            }
            _out.WriteLine($"old max turn {MaxTurn(RoverTracks.Resample(old)):F2}, new {MaxTurn(MoonPlanting.TRAVERSE_PATH):F2}");
            foreach (Vector2[] east in new[]
            {
                new Vector2[] { new(235f, -150f), new(120f, -114f), new(34f, -97f), new(-6f, -99f), new(-40f, -111f) },
                new Vector2[] { new(235f, -150f), new(120f, -114f), new(30f, -96f), new(-10f, -100f), new(-40f, -112f) },
                new Vector2[] { new(235f, -150f), new(125f, -116f), new(36f, -98f), new(-4f, -98f), new(-40f, -110f) },
                new Vector2[] { new(235f, -150f), new(120f, -112f), new(30f, -97f), new(-8f, -100f), new(-40f, -112f) },
                new Vector2[] { new(235f, -150f), new(115f, -112f), new(28f, -96f), new(-10f, -99f), new(-41f, -111f) },
            })
            {
                var all = new List<Vector2>(east);
                all.AddRange(MoonPlanting.TRAVERSE.Skip(5));
                Vector2[] p = RoverTracks.Resample(all);
                string turns = "";
                for (int i = 1; i + 1 < p.Length; i++)
                    turns += $" {MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(Vector2.Dot(Vector2.Normalize(p[i] - p[i - 1]), Vector2.Normalize(p[i + 1] - p[i])), -1f, 1f))):F1}";
                _out.WriteLine($"candidate max {MaxTurn(p):F2}:{turns}");
            }
            _out.WriteLine($"flag {MoonPlanting.FLAG} foot {FlagFoot}; start {a} end {b} look {look}; min clear {minClear:F2} max height {maxHeight:F2} length {length:F1} speed {length / shot.Seconds:F1}; end distance {Vector3.Distance(b, look):F1}");
        }
    }
}
