using BS3D.Screens;
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
    /// play view of every shipped level is held, as the session fits it (<see cref="GameCameraFit"/>, the same lines as
    /// GunPassTests and MoonIntroShotsTests) and as the overview focuses it (<see cref="OverviewFocus"/>), over the camp as
    /// <see cref="PolarPlanting"/> stands it on the shipped config and the ground's mirror: in the frame, behind neither the gun
    /// nor a ball the level opens with, over the island's deck, clear of the HUD's cluster profile and big enough to read.
    /// And the ice under it is clear of crevasses and of the pressure front.
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

        //The gun as it stands at rest, in CannonRig's figures: the barrel BARREL_REACH either side of the trunnions along its aim,
        //its glass BORE_RADIUS + WALL_THICKNESS round (0.74) and held at BARREL_AIR more for the loaded ball's ring; each wheel
        //WHEEL_RADIUS round, WHEEL_TRACK off the barrel's axis and AXLE_DROP under the trunnions, held as a ball that big
        private const float BARREL_REACH = 3.4f, BARREL_RADIUS = 0.74f + 0.26f;
        private const float WHEEL_RADIUS = 1.15f, WHEEL_TRACK = 1.5f, AXLE_DROP = 1.45f;

        //Reads at the distance: each module at least this many pixels from its floor to its roof at 1080 lines, the mast at
        //least MIN_MAST_PIXELS; at its own figures (#747) a module was 18 and the mast 50 where it stood
        private const float MIN_MODULE_PIXELS = 40f, MIN_MAST_PIXELS = 170f;

        //The HUD's cluster profile down the left edge (PlayHud's PROFILE_MARGIN and PROFILE_WIDTH, in 2160-line design units,
        //scaled by the window's height): at 16:9 and wider the mast's top and the flag stand right of it, at 16:10 and 4:3,
        //which squeeze the frame's width under it, they stand behind its lower part
        private const float PROFILE_RIGHT = (92f + 480f) / 2160f;

        //How far the loaded ball stands ahead of the gun's trunnions (CannonRig.PivotToFrontBall, which needs the rig): about
        //what OverviewFocusTests' Zigzag muzzle stands off its gun. It only decides how far the focused look may tilt down
        private const float PIVOT_TO_FRONT_BALL = 3f;

        //How many levels of a field the play view frames (GameplayScreen.FRAMED_LEVELS): the fit's top and the centre the
        //focus falls back to are taken over at most this many
        private const int FRAMED_LEVELS = 18;

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

        //The nearest the line from a to b passes a point
        private static float Off(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 line = b - a;
            float t = MathHelper.Clamp(Vector3.Dot(point - a, line) / line.LengthSquared(), 0f, 1f);
            return Vector3.Distance(a + line * t, point);
        }

        //Whether the gun at rest stands on the line from the lens to a point: within its barrel's radius of the barrel's axis
        //anywhere along it, or within a wheel's radius of a wheel's middle
        private static bool BehindTheGun(Vector3 lens, Vector3 point, Vector3 trunnions, Vector3 aim)
        {
            for (float s = -BARREL_REACH; s <= BARREL_REACH; s += 0.25f)
                if (Off(lens, point, trunnions + aim * s) < BARREL_RADIUS) return true;

            Vector3 across = Vector3.Normalize(Vector3.Cross(new Vector3(aim.X, 0f, aim.Z), Vector3.Up));
            foreach (float side in new[] { -WHEEL_TRACK, WHEEL_TRACK })
                if (Off(lens, point, trunnions + across * side - Vector3.Up * AXLE_DROP) < WHEEL_RADIUS) return true;

            return false;
        }

        private static bool InFrame(Vector3 ndc, bool ahead) =>
            ahead && MathF.Abs(ndc.X) <= 1f - FRAME_MARGIN && MathF.Abs(ndc.Y) <= 1f - FRAME_MARGIN;

        /// <summary>
        /// A level's play view as the session fits it: the gun, the balls it opens with, and the two poses the overview holds,
        /// the fit's own frame it opens on and the focused one it eases into after <c>OverviewFocus.HOLD_OPENING_SECONDS</c> and
        /// keeps while the player plays (#1006: the lens up and in, the look pulled towards the aim, the lens narrowed), the
        /// latter with its look pulled towards either aim point the session can follow at rest.
        /// </summary>
        private sealed record Opening(string Level, Vector3 Gun, Vector3 Aim, List<Vector3> Balls, (string Name, Vector3 Lens, Vector3 Target, float Fov)[] Poses);

        private static Opening Open(string file, float aspect)
        {
            BallsMap map = new(Level.Load(file).Map);
            map.Center();
            Vector3 offset = ClusterHang.FitWorldOffset(map, out float fieldTopY);
            float ceilingCentreY = CeilingPlate.CentreYAbove(fieldTopY);
            float halfX = CeilingPlate.FootprintFor(map.StageSizeX) * Constants.HALF;
            float halfZ = CeilingPlate.FootprintFor(map.StageSizeZ) * Constants.HALF;
            float framedTop = MathF.Min(CeilingPlate.TopFaceY(ceilingCentreY), offset.Y + (FRAMED_LEVELS - 1) / Constants.SQRT_TWO);

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

            //The focused pose, its aim point where the session puts it (GameplayScreen.Camera, OverviewFocus.FollowAim, seeded so
            //no chase lags it): on the aim from the muzzle at rest, at the depth of the beam's first contact, or of the cluster's
            //centre (the framed window's middle, GameplayScreen.FitFieldToMap) when the beam meets nothing. Both are held: the
            //beam here a straight line, its contact where the shot ball would touch the first ball it meets. The top row is the
            //glass's underside over the plate's corners (GameplayScreen.TopRowCorner)
            Vector3 muzzle = cannon.MuzzlePosition(PIVOT_TO_FRONT_BALL), aim = cannon.AimDirection;
            float centreY = Math.Min(size.Level - 1, FRAMED_LEVELS - 1) * Constants.HALF / Constants.SQRT_TWO + offset.Y;
            var depths = new List<(string Pose, float Depth)>
            {
                ("focused on the cluster's centre",
                    PreciseAim.DepthToClusterCentre(muzzle, aim, new Vector3(cannon.OrbitCenter.X, centreY, cannon.OrbitCenter.Z))),
            };
            if (FirstContact(muzzle, aim, balls) is float contact) depths.Add(("focused on the first ball", contact));

            float rowY = MathF.Min(ceilingCentreY - CeilingPlate.THICKNESS * Constants.HALF, framedTop);
            Vector3[] topRow =
            {
                new(-halfX, rowY, -halfZ), new(halfX, rowY, -halfZ), new(-halfX, rowY, halfZ), new(halfX, rowY, halfZ),
            };
            var poses = new List<(string Name, Vector3 Lens, Vector3 Target, float Fov)> { ("opening", lens, target, GAME_FOV) };
            foreach ((string pose, float depth) in depths)
            {
                Vector3 aimPoint = new OverviewFocus().FollowAim(muzzle, aim, depth, 0f);
                AimPose focused = OverviewFocus.Focused(lens, target, GAME_FOV, aimPoint, muzzle, topRow);
                poses.Add((pose, focused.Position, focused.Target, focused.FieldOfView));
            }

            return new Opening(Path.GetFileName(file), cannon.Position, aim, balls, poses.ToArray());
        }

        //How far along the aim from the muzzle a straight shot first touches a ball, or null when it touches none
        private static float? FirstContact(Vector3 muzzle, Vector3 aim, List<Vector3> balls)
        {
            const float TOUCH = BallRenderSet.BALL_RADIUS * 2f;
            float? nearest = null;
            foreach (Vector3 ball in balls)
            {
                float along = Vector3.Dot(ball - muzzle, aim);
                float offSquared = (ball - muzzle).LengthSquared() - along * along;
                if (along <= 0f || offSquared > TOUCH * TOUCH) continue;
                float depth = along - MathF.Sqrt(TOUCH * TOUCH - offSquared);
                if (nearest is not float n || depth < n) nearest = depth;
            }
            return nearest;
        }

        /// <summary>
        /// The play view of every shipped level, played on the ice, in both poses the overview holds: every landmark of the camp
        /// (at 16:10 and 4:3 the station, its mast and flag and the vehicle) stands in the frame with its top and its middle,
        /// the gun at rest and the balls the level opens with off the line to it; every module stands at least half over the island's deck,
        /// the mast's top, the flag and the vehicle's cab over it too; at 16:9 and wider the mast's top and the flag stand
        /// clear of the HUD's cluster profile; and each module and the mast are big enough to read.
        /// </summary>
        [Theory]
        [MemberData(nameof(Aspects))]
        public void ThePlayViewSeesTheCampOverTheDeckOnEveryLevel(float aspect)
        {
            PolarPlanting.Landmark[] camp = Camp;
            bool wide = aspect >= WIDE;
            float aimUp = 0f, leastOffProfile = float.MaxValue, widest = 0f, leastModule = float.MaxValue, leastMast = float.MaxValue, leastSight = float.MaxValue;
            int levels = 0, contacts = 0;

            foreach (string file in Shipped.LevelFiles())
            {
                Opening open = Open(file, aspect);
                aimUp = MathHelper.ToDegrees(MathF.Asin(open.Aim.Y));
                levels++;
                if (open.Poses.Length > 2) contacts++;
                foreach ((string pose, Vector3 lens, Vector3 target, float fov) in open.Poses)
                {
                    string where = $"{open.Level} at {aspect:F2}, {pose}";
                    Matrix viewProjection = Matrix.CreateLookAt(lens, target, Vector3.Up)
                        * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.1f, 5000f);


                    foreach (PolarPlanting.Landmark mark in camp)
                    {
                        if (!wide && !CORE.Contains(mark.Name)) continue;

                        foreach (Vector3 point in new[] { mark.Top, Vector3.Lerp(mark.Foot, mark.Top, 0.5f) })
                        {
                            Vector3 ndc = Project(point, viewProjection, out bool ahead);
                            Assert.True(InFrame(ndc, ahead), $"{where}: the camp's {mark.Name} is out of the frame at {ndc.X:F2}, {ndc.Y:F2}");
                            widest = MathF.Max(widest, MathF.Abs(ndc.X));

                            Assert.False(BehindTheGun(lens, point, open.Gun, open.Aim), $"{where}: the gun stands before the camp's {mark.Name}");
                        }

                        //Not behind the cluster: no ball the level opens with on the line to its top
                        Vector3 line = mark.Top - lens;
                        foreach (Vector3 ball in open.Balls)
                        {
                            float t = MathHelper.Clamp(Vector3.Dot(ball - lens, line) / line.LengthSquared(), 0f, 1f);
                            Assert.True(Vector3.Distance(lens + line * t, ball) >= BallRenderSet.BALL_RADIUS,
                                $"{where}: a ball hangs over the camp's {mark.Name}");
                        }

                        //Over the deck: each module from its middle up, the rest from its top
                        Vector3 seen = mark.Name.StartsWith("module", StringComparison.Ordinal) ? Vector3.Lerp(mark.Foot, mark.Top, 0.5f) : mark.Top;
                        if (mark.Name != "turbine" && (wide || CORE.Contains(mark.Name)))
                        {
                            float margin = SightMargin(lens, seen);
                            Assert.True(margin >= 0f, $"{where}: the camp's {mark.Name} is {-margin:F2} under the deck's line");
                            leastSight = MathF.Min(leastSight, margin);
                        }

                        //Clear of the cluster profile, its right edge as a share of the frame's width from the left
                        if (wide && (mark.Name == "mast" || mark.Name == "flag"))
                        {
                            float fromLeft = (Project(mark.Top, viewProjection, out _).X + 1f) * 0.5f;
                            leastOffProfile = MathF.Min(leastOffProfile, fromLeft - PROFILE_RIGHT / aspect);
                            Assert.True(fromLeft > PROFILE_RIGHT / aspect,
                                $"{where}: the camp's {mark.Name} stands behind the HUD's cluster profile, {fromLeft:F3} of the width in");
                        }

                        float pixels = (Project(mark.Top, viewProjection, out _).Y - Project(mark.Foot, viewProjection, out _).Y) * 0.5f * 1080f;
                        if (mark.Name.StartsWith("module", StringComparison.Ordinal))
                        {
                            Assert.True(pixels >= MIN_MODULE_PIXELS, $"{where}: the camp's {mark.Name} is {pixels:F0} px tall at 1080 lines");
                            leastModule = MathF.Min(leastModule, pixels);
                        }
                        if (mark.Name == "mast")
                        {
                            Assert.True(pixels >= MIN_MAST_PIXELS, $"{where}: the mast is {pixels:F0} px tall at 1080 lines");
                            leastMast = MathF.Min(leastMast, pixels);
                        }
                    }
                }
            }

            if (!wide) leastOffProfile = 0f;
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F2}: out to {1:F2} of the half-frame, modules {2:F0} px and mast {3:F0} px at least, over the deck by {4:F2}, right of the profile by {5:F3} of the width; the gun aims {6:F1} degrees up, its beam meeting a ball on {7} of {8} levels",
                aspect, widest, leastModule, leastMast, leastSight, leastOffProfile, aimUp, contacts, levels));
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

        /// <summary>
        /// The sun's map reaches over the whole camp (#1128): every landmark's top, the mast's on the swell it stands on, under
        /// the top of the box the renderer fits over the icesheet, where a caster over it would be clipped out of the map.
        /// </summary>
        [Fact]
        public void TheSunsMapReachesOverTheWholeCamp()
        {
            float top = SceneRenderer.ShadowBoxTop(Polar.LevelY, PolarBackdrop.ShadowAbove(Polar));
            float highest = float.MinValue;
            foreach (PolarPlanting.Landmark mark in Camp)
            {
                Assert.True(mark.Top.Y <= top, $"the camp's {mark.Name} stands {mark.Top.Y:F2} high, over the sun's box at {top:F2}");
                highest = MathF.Max(highest, mark.Top.Y);
            }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the camp's highest point {0:F2}, the box's top {1:F2}", highest, top));
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
