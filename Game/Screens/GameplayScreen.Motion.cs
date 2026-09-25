using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The motion blur's side of the session</b> (#402) — what moved over the shutter and where it was when the
    /// shutter opened: the camera, the barrel and the carriage from a short record of their poses, the balls from
    /// their bodies' own velocities (<c>ClusterCollector</c>), and the rounds in the bore from the barrel they ride.
    /// The passes themselves are <see cref="MotionBlur"/>'s and the pipeline's; this file only says what goes into
    /// them and when.
    /// </summary>
    /// <remarks>
    /// <b>Why a record of poses and not last frame's pose.</b> The shutter is a length of time
    /// (<see cref="MotionBlur.SHUTTER_SECONDS"/>), not a frame, so the smear is the same at 60 Hz and at 144 — which is
    /// the owner's whole complaint answered: a one-frame difference at his desktop's refresh is under a tenth of the
    /// barrel's width on the fastest flick there is (#402's second comment measured it). See <see cref="ShutterHistory"/>.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        #region Motion blur

        //The camera's view × projection, the barrel's and the carriage's world matrices, each over the last shutter
        //and a little more. Wall-clock stamped: what the shutter covers is time on screen, so a pause stands the
        //record still and a slow-motion cinematic shortens the smear by itself, with nothing to scale.
        private readonly ShutterHistory _cameraShutter = new(), _barrelShutter = new(), _carriageShutter = new();

        //This frame's poses and their shutter-opening counterparts, taken once by PrepareMotion and read by the
        //magazine's collection, the gun's draw and the velocity pass alike, so no two of them can disagree.
        private Matrix _motionBarrel, _motionBarrelThen, _motionCarriage, _motionCarriageThen, _motionCameraThen;

        //inverse(barrel now) × barrel then: what carries anything set in the tube back to where it was
        private Matrix _motionBarrelBack;

        //Whether PrepareMotion ran this frame — the frame's one answer to "is this frame blurred"
        private bool _motionThisFrame;

        //The wall clock when the simulation last stepped, and how fast it was running: the balls' smear comes off
        //their velocities, which a paused world still holds, so a world that is not stepping must smear nothing
        private float _simulatedAt = float.NegativeInfinity;
        private float _simulationTimeScale = 1f;

        //Cached rather than a method group per frame (a method group is a new delegate each time it is passed)
        private Action<MotionBlur> _drawMotionVelocity;

        /// <summary>
        /// A camera that jumps further than this in one frame has CUT, not moved — a takeover starting, a level's
        /// first frame — and its record is dropped, or the frame after a cut would smear the whole picture from the
        /// old view to the new one. Two world units is several times the fastest the lens ever travels in a frame
        /// (the cinematic's swing included) and far under any cut.
        /// </summary>
        private const float CAMERA_CUT_DISTANCE = 2f;

        /// <summary>
        /// How much of the <b>camera's own</b> motion over the shutter the frame is smeared by, against the whole of
        /// every object's. Measured before it was set, at the full share: a 118°/s swing in precise aim smeared the
        /// entire frame to the cap, cluster and all — the very target the player is lining up — and the recoil's kick
        /// took every shot's first tenth of a second out of focus. Two fifths, with the lens ratio below, still
        /// smeared a leaned-in 118°/s swing some sixty pixels at 1080 lines, cluster and all; a quarter halves the
        /// kick and leaves the swing about thirty-five — plainly felt (a kick that blurs the frame for a beat is the
        /// shot landing in the player's hands) while the balls being aimed at keep their pattern. The gun is exempted
        /// from the difference while the lens is leaned in over it — see <see cref="PinToLens"/>.
        /// </summary>
        private const float CAMERA_SHARE = 0.25f;

        //The overview lens's vertical scale, 1 / tan(fov / 2) — what Projection.M22 reads when nothing is leaned in. A
        //property, not a static field: GAME_FOV lives in another part of this partial class, and the order static
        //initializers run across partial files is not defined.
        private static float OverviewLensScale => 1f / MathF.Tan(GAME_FOV * 0.5f);

        /// <summary>Notes that the simulation stepped this frame, at <paramref name="timeScale"/> of real time.</summary>
        private void NoteSimulated(float timeScale)
        {
            _simulatedAt = WallClock;
            _simulationTimeScale = timeScale;
        }

        /// <summary>
        /// How much simulated time the balls' shutter covers this frame: the shutter scaled by how fast the world is
        /// running, or nothing when it did not step at all this frame (the pause, a page over a finished level that
        /// has stopped it).
        /// </summary>
        private float BallShutterSeconds =>
            WallClock - _simulatedAt < 0.25f ? MotionBlur.SHUTTER_SECONDS * _simulationTimeScale : 0f;

        /// <summary>
        /// Takes this frame's poses into their records and reads each back a shutter ago. Called before the balls are
        /// collected, since the rounds in the bore are carried back by the barrel's motion.
        /// </summary>
        private void PrepareMotion(in Matrix barrelWorld, in Matrix carriageWorld)
        {
            float now = WallClock;
            float then = now - MotionBlur.SHUTTER_SECONDS;

            Matrix viewProjection = Camera.View * Camera.Projection;

            //A cut drops the record, so the frame after it starts from a still camera — and so does a GAP: a record
            //last written seconds ago (the row switched back on, the session drawn again after the menus) would have
            //the shutter read a straight line from that old pose to this one and smear the whole frame across it
            if (Vector3.DistanceSquared(Camera.Position, _lastCameraPosition) > CAMERA_CUT_DISTANCE * CAMERA_CUT_DISTANCE
                || now - _lastMotionAt > MotionBlur.SHUTTER_SECONDS * 2f)
            {
                _cameraShutter.Clear();
                _barrelShutter.Clear();
                _carriageShutter.Clear();
            }
            _lastCameraPosition = Camera.Position;
            _lastMotionAt = now;

            _cameraShutter.Push(now, viewProjection);
            _barrelShutter.Push(now, barrelWorld);
            _carriageShutter.Push(now, carriageWorld);

            //The camera's share of its own motion: a straight line from where it is to where it was, cut short — and
            //cut shorter still through a narrower lens. Leaned in, the lens magnifies every degree the aim turns by
            //the ratio of the two lenses' scales, so the same swing that smears the overview a little smeared the
            //whole leaned-in frame to the cap (measured: a 118°/s swing in precise aim, at CAMERA_SHARE alone). The
            //ratio holds the camera's smear to what that swing would draw through the overview's lens.
            float lensRatio = MathF.Min(1f, OverviewLensScale / Camera.Projection.M22);
            _motionCameraThen = Matrix.Lerp(viewProjection, _cameraShutter.At(then, viewProjection),
                CAMERA_SHARE * lensRatio);

            float lean = _preciseAim.Blend;
            _motionBarrel = barrelWorld;
            _motionBarrelThen = PinToLens(barrelWorld, _barrelShutter.At(then, barrelWorld), viewProjection, lean);
            _motionCarriage = carriageWorld;
            _motionCarriageThen = PinToLens(carriageWorld, _carriageShutter.At(then, carriageWorld), viewProjection, lean);
            _motionBarrelBack = Matrix.Invert(barrelWorld) * _motionBarrelThen;

            _motionThisFrame = true;
        }

        private Vector3 _lastCameraPosition = new(float.MaxValue);
        private float _lastMotionAt = float.NegativeInfinity;

        /// <summary>
        /// The gun's shutter pose, taken <paramref name="lean"/> of the way towards the one that holds it exactly where
        /// it is on screen. Leaned in over the barrel the lens rides the gun, so the gun stands still in the frame and
        /// must not smear — which the camera's cut-down share (<see cref="CAMERA_SHARE"/>) would otherwise break: the
        /// barrel's whole world motion against a quarter of the lens's would leave three quarters of a swing smeared
        /// across a tube that is not moving on screen at all. The pinned pose is this frame's placed back through this
        /// frame's camera and forward through the shutter's, <c>world × VP_now × VP_then⁻¹</c> — projective rather than
        /// rigid, which the velocity shader carries through its w without minding. In the overview (lean 0) the gun is
        /// a thing in the world and keeps its own motion.
        /// </summary>
        private Matrix PinToLens(in Matrix world, in Matrix worldThen, in Matrix viewProjection, float lean)
        {
            if (lean <= 0f) return worldThen;

            Matrix pinned = world * viewProjection * Matrix.Invert(_motionCameraThen);
            return Matrix.Lerp(worldThen, pinned, lean);
        }

        /// <summary>
        /// Where a round in the bore stood when the shutter opened, for <see cref="BallDrawFrame.Add"/>: its pose now
        /// carried back by the barrel's own motion. The default (no pose) on a frame that is not blurred, which keeps
        /// it out of the motion record.
        /// </summary>
        private Matrix RoundShutterWorld(in Matrix world) => _motionThisFrame ? world * _motionBarrelBack : default;

        /// <summary>
        /// The velocity pass, run by the host between the scene's last draw and the resolve (BS3DGame.FinishSceneDraw):
        /// the gun whole, then the balls. The background depth is the cluster's distance — the one depth the camera's motion
        /// is reprojected at for everything this does not draw, which keeps the cluster still in the frame while the
        /// overview camera orbits it, as it truly is (see <c>BackgroundReproject</c> in MotionBlur.fx).
        /// </summary>
        private void DrawMotionVelocity(MotionBlur blur)
        {
            if (!_motionThisFrame) return;

            //The cluster's DISTANCE, not its view depth. The two agree wherever the lens looks at the cluster, which in
            //play is always; but the chapter intro's prologue (#559) flies shots that look away from the arena, and there
            //the view depth goes to nothing or below it — MotionBlur clamps it to a tenth of a unit, which reprojected the
            //whole background as a wall a tenth of a unit in front of a moving lens: every star on the Moon's Earth shot
            //drawn as a streak radiating from the direction of travel. A distance is positive and continuous through any
            //turn, and far from the arena it is far, which is where the prologue's scenery is.
            Vector3 cluster = new(_cannon.OrbitCenter.X, _clusterCentreY, _cannon.OrbitCenter.Z);
            float depth = Vector3.Distance(cluster, Camera.Position);

            if (!blur.BeginVelocity(Camera.View, Camera.Projection, _motionCameraThen, depth)) return;

            Game.CannonRig.DrawMotion(blur, _motionBarrel, _motionBarrelThen, _motionCarriage, _motionCarriageThen, collar: true);
            Game.Balls.DrawMotion(blur);
        }

        #endregion
    }
}
