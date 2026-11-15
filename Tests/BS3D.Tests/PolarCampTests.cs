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
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The polar research camp in the play view (#1128). The owner: "the camp should also be bigger and placed so that it is
    /// visible even while playing". No campaign level plays on the ice; the pause's Scene list puts any level there, so the
    /// opening pose of every shipped level is held, as the session fits it (<see cref="GameCameraFit"/>, the same lines as
    /// GunPassTests and MoonIntroShotsTests), over the camp as <see cref="PolarPlanting"/> stands it on the shipped config and
    /// the ground's mirror: in the frame, clear of the gun, not behind a ball the level opens with, over the island's deck
    /// and big enough to read. And the ice under it is clear of crevasses and of the pressure front.
    /// </summary>
    public class PolarCampTests
    {
        private readonly ITestOutputHelper _output;

        public PolarCampTests(ITestOutputHelper output) => _output = output;

        //The game's vertical field of view (GameplayScreen.GAME_FOV) and the window shapes it is played at: 16:9 the narrowest
        //it targets (BS3DGame's windowed default), the owner's 3840x1600, and 16:10 and 4:3 panels narrower still. The
        //projection is Hor+, so a narrower shape only crops the sides.
        private const float GAME_FOV = MathF.PI / 4.2f;
        private static readonly float[] ASPECTS = { 16f / 9f, 2.4f, 1.6f, 4f / 3f };
        public static readonly IEnumerable<object[]> Aspects = ASPECTS.Select(a => new object[] { a });

        //At 16:9 and wider the whole camp stands in the frame; at 16:10 and 4:3, which crop the sides, the station, its mast with
        //the flag and the vehicle parked before it (the turbine stands beyond the station's far end)
        private const float WIDE = 16f / 9f - 1e-3f;
        private static readonly string[] CORE = { "module 1", "module 2", "module 3", "station's end", "station's other end", "mast", "flag", "vehicle" };

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame
        private const float FRAME_MARGIN = 0.05f;

        //The gun as the play camera sees it at rest (MoonIntroShotsTests): its wheels' outer faces 2.13 off the barrel's axis,
        //held at 2.5, each WHEEL_RADIUS 1.15 nearer the lens at most
        private const float GUN_HALF_WIDTH = 2.5f, GUN_NEAR = 1.15f;

        //Reads at the distance: each module at least this many pixels from its floor to its roof at 1080 lines, the mast at
        //least MIN_MAST_PIXELS; at its own figures (#747) a module was 18 and the mast 50 where it stood
        private const float MIN_MODULE_PIXELS = 40f, MIN_MAST_PIXELS = 170f;

        //Sight lines are marched this far apart in plan, a fraction of the desktop grid's 2.86-unit cell
        private const float SIGHT_STEP = 0.5f;

        private static readonly PolarSceneConfig Polar = new();

        private static readonly Dictionary<(float, float), float> Heights = new();

        private static float Exact(float x, float z)
        {
            lock (Heights)
            {
                if (!Heights.TryGetValue((x, z), out float h)) Heights[(x, z)] = h = TerrainMirror.Polar(x, z, Polar);
                return h;
            }
        }

        //The ground as the desktop grid draws it (the Potato path draws no icesheet and so no camp)
        private static float Drawn(float x, float z) =>
            TerrainMirror.OnGrid(Exact, PolarBackdrop.POLAR_GRID_N, PolarBackdrop.POLAR_EXTENT, x, z);

        private static PolarPlanting.Landmark[] Camp => PolarPlanting.Landmarks(Polar.CampCentre, Polar.CampTurn, Exact);

        //What hides a point from the play lens: the island's stone, nowhere over ArenaIsland.TOP_Y out to its RADIUS, and the
        //ground as drawn past it
        private static float Occluder(float x, float z)
        {
            float y = Drawn(x, z);
            if (x * x + z * z <= ArenaIsland.RADIUS * ArenaIsland.RADIUS) y = MathF.Max(y, ArenaIsland.TOP_Y);
            return y;
        }

        //How far the sight line from the lens to a point passes over what would hide it, at its lowest: negative, hidden
        private static float SightMargin(Vector3 lens, Vector3 point)
        {
            Vector2 plan = new(point.X - lens.X, point.Z - lens.Z);
            float length = plan.Length();
            float lowest = float.MaxValue;
            for (float s = SIGHT_STEP; s < length - SIGHT_STEP; s += SIGHT_STEP)
            {
                float t = s / length;
                lowest = MathF.Min(lowest, lens.Y + (point.Y - lens.Y) * t - Occluder(lens.X + plan.X * t, lens.Z + plan.Y * t));
            }
            return lowest;
        }

        private static Vector3 Project(Vector3 point, Matrix viewProjection, out bool ahead)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
            ahead = clip.W > 0.1f;
            return new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
        }

        private static bool InFrame(Vector3 ndc, bool ahead) =>
            ahead && MathF.Abs(ndc.X) <= 1f - FRAME_MARGIN && MathF.Abs(ndc.Y) <= 1f - FRAME_MARGIN;

        /// <summary>A level's opening as the session fits it: the lens, its look, the gun and the balls it opens with.</summary>
        private sealed record Opening(string Level, Vector3 Lens, Vector3 Target, Vector3 Gun, List<Vector3> Balls);

        private static Opening Open(string file, float aspect)
        {
            BallsMap map = new(Level.Load(file).Map);
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

            //The balls the level opens with, hung from the ceiling's rest (MoonIntroShotsTests' cluster bottom)
            float topLevelY = BallsConstraintsBuilder.CeilingRestY(ceilingCentreY);
            XZLevel size = map.GetStaticBallsArraySize();
            StaticBall[,,] cells = map.GetStaticBallsArray();
            var balls = new List<Vector3>();
            for (int x = 0; x < size.X; x++)
                for (int z = 0; z < size.Z; z++)
                    for (int l = 0; l < size.Level; l++)
                    {
                        if (cells[x, z, l] == null) continue;
                        Vector3 p = map.GetRealCenteredPosition(new XZLevel(x, z, l)) + offset;
                        balls.Add(new Vector3(p.X, topLevelY - (size.Level - 1 - l) / Constants.SQRT_TWO, p.Z));
                    }

            return new Opening(Path.GetFileName(file), lens, target, cannon.Position, balls);
        }

        /// <summary>
        /// The opening frame of every shipped level, played on the ice: every landmark of the camp (at 16:10 and 4:3 the
        /// station and its mast) stands in the frame with its top and its middle, off to the side of the gun's carriage, not
        /// behind a ball the level opens with; every module stands at least half over the island's deck, the mast's top, the
        /// flag and the vehicle's cab over it too; and each module and the mast are big enough to read.
        /// </summary>
        [Theory]
        [MemberData(nameof(Aspects))]
        public void ThePlayViewSeesTheCampOverTheDeckOnEveryLevel(float aspect)
        {
            PolarPlanting.Landmark[] camp = Camp;
            bool wide = aspect >= WIDE;
            float leastOffGun = float.MaxValue, widest = 0f, leastModule = float.MaxValue, leastMast = float.MaxValue, leastSight = float.MaxValue;

            foreach (string file in Shipped.LevelFiles())
            {
                Opening open = Open(file, aspect);
                Matrix viewProjection = Matrix.CreateLookAt(open.Lens, open.Target, Vector3.Up)
                    * Matrix.CreatePerspectiveFieldOfView(GAME_FOV, aspect, 0.1f, 5000f);

                Vector3 forward = Vector3.Normalize(new Vector3(open.Target.X - open.Lens.X, 0f, open.Target.Z - open.Lens.Z));
                Vector3 right = Vector3.Cross(forward, Vector3.Up);
                float gunSide = MathF.Atan2(GUN_HALF_WIDTH, Vector3.Dot(open.Gun - open.Lens, forward) - GUN_NEAR);

                foreach (PolarPlanting.Landmark mark in camp)
                {
                    if (!wide && !CORE.Contains(mark.Name)) continue;

                    foreach (Vector3 point in new[] { mark.Top, Vector3.Lerp(mark.Foot, mark.Top, 0.5f) })
                    {
                        Vector3 ndc = Project(point, viewProjection, out bool ahead);
                        Assert.True(InFrame(ndc, ahead), $"{open.Level} at {aspect:F2}: the camp's {mark.Name} is out of the frame at {ndc.X:F2}, {ndc.Y:F2}");
                        widest = MathF.Max(widest, MathF.Abs(ndc.X));

                        float side = MathF.Atan2(MathF.Abs(Vector3.Dot(point - open.Lens, right)), Vector3.Dot(point - open.Lens, forward));
                        Assert.True(side > gunSide,
                            $"{open.Level} at {aspect:F2}: the camp's {mark.Name} stands {MathHelper.ToDegrees(side):F1} degrees off the axis, behind the gun's {MathHelper.ToDegrees(gunSide):F1}");
                        leastOffGun = MathF.Min(leastOffGun, MathHelper.ToDegrees(side - gunSide));
                    }

                    //Not behind the cluster: no ball the level opens with on the line to its top
                    Vector3 line = mark.Top - open.Lens;
                    foreach (Vector3 ball in open.Balls)
                    {
                        float t = MathHelper.Clamp(Vector3.Dot(ball - open.Lens, line) / line.LengthSquared(), 0f, 1f);
                        Assert.True(Vector3.Distance(open.Lens + line * t, ball) >= BallRenderSet.BALL_RADIUS,
                            $"{open.Level} at {aspect:F2}: a ball hangs over the camp's {mark.Name}");
                    }

                    //Over the deck: each module from its middle up, the rest from its top
                    Vector3 seen = mark.Name.StartsWith("module", StringComparison.Ordinal) ? Vector3.Lerp(mark.Foot, mark.Top, 0.5f) : mark.Top;
                    if (mark.Name != "turbine" && (wide || CORE.Contains(mark.Name)))
                    {
                        float margin = SightMargin(open.Lens, seen);
                        Assert.True(margin >= 0f, $"{open.Level} at {aspect:F2}: the camp's {mark.Name} is {-margin:F2} under the deck's line");
                        leastSight = MathF.Min(leastSight, margin);
                    }

                    float pixels = (Project(mark.Top, viewProjection, out _).Y - Project(mark.Foot, viewProjection, out _).Y) * 0.5f * 1080f;
                    if (mark.Name.StartsWith("module", StringComparison.Ordinal))
                    {
                        Assert.True(pixels >= MIN_MODULE_PIXELS, $"{open.Level} at {aspect:F2}: the camp's {mark.Name} is {pixels:F0} px tall at 1080 lines");
                        leastModule = MathF.Min(leastModule, pixels);
                    }
                    if (mark.Name == "mast")
                    {
                        Assert.True(pixels >= MIN_MAST_PIXELS, $"{open.Level} at {aspect:F2}: the mast is {pixels:F0} px tall at 1080 lines");
                        leastMast = MathF.Min(leastMast, pixels);
                    }
                }
            }

            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F2}: off the gun by {1:F1} degrees at least, out to {2:F2} of the half-frame, modules {3:F0} px and mast {4:F0} px at least, over the deck by {5:F2}",
                aspect, leastOffGun, widest, leastModule, leastMast, leastSight));
        }

        /// <summary>
        /// The ice under the whole camp is whole (#747, #1128): no crevasse within its reach, at its new size (the clearing,
        /// <see cref="PolarSceneConfig.CampClearRadius"/>, holds <see cref="PolarPlanting.Reach"/>), and the pressure front's
        /// plates stand nowhere under it.
        /// </summary>
        [Fact]
        public void TheIceUnderTheWholeCampIsClearOfCrevassesAndOfTheFront()
        {
            Vector2 centre = Polar.CampCentre;
            float reach = PolarPlanting.Reach;
            Assert.True(Polar.CampClearRadius >= reach, $"the crevasses are cleared to {Polar.CampClearRadius}, the camp reaches {reach:F1}");

            for (float dx = -reach; dx <= reach; dx += 1f)
                for (float dz = -reach; dz <= reach; dz += 1f)
                {
                    if (dx * dx + dz * dz > reach * reach) continue;
                    float x = centre.X + dx, z = centre.Y + dz;
                    Assert.True(TerrainMirror.PolarCrevasse(x, z, Polar) == 0f, $"a crevasse opens under the camp at ({x:F0}, {z:F0})");
                    Assert.True(TerrainMirror.PolarRidge(x, z, Polar) == 0f, $"the pressure front stands under the camp at ({x:F0}, {z:F0})");
                }
        }

        /// <summary>Every landmark of the camp within its reach of the camp's centre, so the clearing and the intro's keep-out hold it.</summary>
        [Fact]
        public void EveryLandmarkStandsWithinTheCampsReach()
        {
            Vector2 centre = Polar.CampCentre;
            foreach (PolarPlanting.Landmark mark in Camp)
            {
                float off = Vector2.Distance(new Vector2(mark.Top.X, mark.Top.Z), centre);
                Assert.True(off <= PolarPlanting.Reach, $"the camp's {mark.Name} stands {off:F1} from its centre, past its reach {PolarPlanting.Reach:F1}");
            }
        }
    }
}
