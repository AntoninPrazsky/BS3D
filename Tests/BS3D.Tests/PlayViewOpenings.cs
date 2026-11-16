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
using System.IO;

namespace BS3D.Tests
{
    /// <summary>
    /// A shipped level's play view as the session fits it (<see cref="GameCameraFit"/>, the same lines as GunPassTests and
    /// MoonIntroShotsTests) and as the overview focuses it (<see cref="OverviewFocus"/>): the gun, the balls it opens with, and
    /// the poses the lens holds, for the tests that hold something of a scene in that view (the polar camp, #1128; the sea's
    /// lighthouse and vessels, #1125). Moved out of PolarCampTests whole.
    /// </summary>
    internal static class PlayViewOpenings
    {
        //The game's vertical field of view (GameplayScreen.GAME_FOV) and the window shapes it is played at: 16:9 the narrowest
        //it targets (BS3DGame's windowed default), the owner's 3840x1600, and 16:10 and 4:3 panels narrower still. The
        //projection is Hor+, so a narrower shape only crops the sides.
        internal const float GAME_FOV = MathF.PI / 4.2f;
        internal static readonly float[] ASPECTS = { 16f / 9f, 2.4f, 1.6f, 4f / 3f };

        //How far inside every edge a point must stay to count as in the frame, as a share of the half-frame
        internal const float FRAME_MARGIN = 0.05f;

        //The gun as it stands at rest, in CannonRig's figures: the barrel BARREL_REACH either side of the trunnions along its aim,
        //its glass BORE_RADIUS + WALL_THICKNESS round (0.74) and held at BARREL_AIR more for the loaded ball's ring; each wheel
        //WHEEL_RADIUS round, WHEEL_TRACK off the barrel's axis and AXLE_DROP under the trunnions, held as a ball that big
        private const float BARREL_REACH = 3.4f, BARREL_RADIUS = 0.74f + 0.26f;
        private const float WHEEL_RADIUS = 1.15f, WHEEL_TRACK = 1.5f, AXLE_DROP = 1.45f;

        //How far the loaded ball stands ahead of the gun's trunnions (CannonRig.PivotToFrontBall, which needs the rig): about
        //what OverviewFocusTests' Zigzag muzzle stands off its gun. It only decides how far the focused look may tilt down
        private const float PIVOT_TO_FRONT_BALL = 3f;

        //How many levels of a field the play view frames (GameplayScreen.FRAMED_LEVELS): the fit's top and the centre the
        //focus falls back to are taken over at most this many
        private const int FRAMED_LEVELS = 18;

        internal static Vector3 Project(Vector3 point, Matrix viewProjection, out bool ahead)
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
        internal static bool BehindTheGun(Vector3 lens, Vector3 point, Vector3 trunnions, Vector3 aim)
        {
            for (float s = -BARREL_REACH; s <= BARREL_REACH; s += 0.25f)
                if (Off(lens, point, trunnions + aim * s) < BARREL_RADIUS) return true;

            Vector3 across = Vector3.Normalize(Vector3.Cross(new Vector3(aim.X, 0f, aim.Z), Vector3.Up));
            foreach (float side in new[] { -WHEEL_TRACK, WHEEL_TRACK })
                if (Off(lens, point, trunnions + across * side - Vector3.Up * AXLE_DROP) < WHEEL_RADIUS) return true;

            return false;
        }

        //Whether a ball the level opens with hangs on the line from the lens to a point
        internal static bool BehindABall(Vector3 lens, Vector3 point, List<Vector3> balls)
        {
            Vector3 line = point - lens;
            foreach (Vector3 ball in balls)
            {
                float t = MathHelper.Clamp(Vector3.Dot(ball - lens, line) / line.LengthSquared(), 0f, 1f);
                if (Vector3.Distance(lens + line * t, ball) < BallRenderSet.BALL_RADIUS) return true;
            }
            return false;
        }

        internal static bool InFrame(Vector3 ndc, bool ahead) =>
            ahead && MathF.Abs(ndc.X) <= 1f - FRAME_MARGIN && MathF.Abs(ndc.Y) <= 1f - FRAME_MARGIN;

        /// <summary>
        /// A level's play view as the session fits it: the gun, the balls it opens with, and the two poses the overview holds,
        /// the fit's own frame it opens on and the focused one it eases into after <c>OverviewFocus.HOLD_OPENING_SECONDS</c> and
        /// keeps while the player plays (#1006: the lens up and in, the look pulled towards the aim, the lens narrowed), the
        /// latter with its look pulled towards either aim point the session can follow at rest.
        /// </summary>
        internal sealed record Opening(string Level, Vector3 Gun, Vector3 Aim, List<Vector3> Balls, (string Name, Vector3 Lens, Vector3 Target, float Fov)[] Poses);

        internal static Opening Open(string file, float aspect)
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
    }
}
