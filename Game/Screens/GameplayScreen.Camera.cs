using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The lens and the gun's stance</b> — the game camera's pose, the precise-aim lean over the barrel,
    /// the solve that fits the camera's stand-off and the gun's orbit to a loaded level, and the recoil
    /// stroke the barrel is displaced by.
    /// </summary>
    /// <remarks>
    /// The solver and the lean are both <c>Prazsky.BS3D</c>'s since #76 (<see cref="GameCameraFit"/>,
    /// <see cref="PreciseAim"/>) and shared with the Testbed in one copy; what is here is this executable's
    /// wiring of them. The pose is handed to <see cref="Prazsky.Core.Camera.RecoilCamera"/> as a value rather
    /// than written through ordered setters, so the drop cinematic can go on lerping over it and the shake is
    /// applied on top of whatever came out. Split out of <c>GameplayScreen.cs</c> in #72.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        #region The game camera and precise aim

        /// <summary>
        /// The camera's base pose, rebuilt each frame from where the gun stands: back from the field centre
        /// along the gun's own bearing and below its trunnions, looking at the cluster. The bearing is
        /// flattened to the horizontal — taken straight from the gun's offset it tilts down by however far
        /// the gun stands below the cluster, which would eat the camera's height and put the lens on the
        /// barrel's own axis. The shake is added on top of this pose, never into it.
        /// <para>
        /// That overview is one end of a Lerp; the other is the precise-aim lean over the barrel, and
        /// <see cref="PreciseAim.Blend"/> is where between them the frame sits. Only the <b>base</b> pose is
        /// interpolated, so the two never fight: the kick is applied to whatever came out, by the camera itself.
        /// </para>
        /// </summary>
        private void UpdateCamera(float elapsed)
        {
            //The lean into precise aim, eased both ways off one reversible scalar. Stepped on every frame,
            //held or not: an unheld frame is how the lean eases back out, which is what makes losing focus a
            //fade rather than a drop. At a blend of 0 the pose below is the overview pose bit for bit, so
            //letting go re-asserts today's framing exactly and an interrupted hold never snaps.
            _preciseAim.Step(_adsHeld, elapsed);

            Vector3 overviewPosition = GameCameraPositionAt(_gameCameraDistance);
            Vector3 overviewTarget = new(_cannon.OrbitCenter.X, _gameCameraTargetY, _cannon.OrbitCenter.Z);

            //Taken as a VALUE, not written into the camera: the cinematic below goes on lerping over it, and
            //the base pose the shake composes onto is whatever comes out of both. The muzzle and the aim are
            //read after _cannon.Update this frame, or the lens lags the barrel and reads as jitter — and
            //without the recoil, which is the barrel's drawing offset and not where it is pointed. The cluster
            //centre is the whole field's middle, solved once per level: the impact face sweeps that range.
            AimPose aim = _preciseAim.BlendedPose(overviewPosition, overviewTarget, GAME_FOV,
                _cannon.MuzzlePosition(Game.CannonRig.PivotToFrontBall), _cannon.AimDirection,
                new Vector3(_cannon.OrbitCenter.X, _clusterCentreY, _cannon.OrbitCenter.Z));

            Vector3 position = aim.Position;
            Vector3 target = aim.Target;
            float fov = aim.FieldOfView;

            //The cluster profile's right-axis is taken from THIS pose — the gameplay lens the player is aiming
            //with — before the cinematic blend below swings it away. Resolved here once so the HUD does not
            //rebuild the basis, and so a drop cinematic does not turn the profile's cluster as it films.
            Vector3 gpForward = target - position;
            float gpLen = gpForward.Length();
            Vector3 gpForwardN = gpLen > 1e-4f ? gpForward / gpLen : Vector3.Forward;
            Vector3 gpRight = Vector3.Cross(gpForwardN, Vector3.Up);
            _gameplayCameraRight = gpRight.LengthSquared() > 1e-6f ? Vector3.Normalize(gpRight) : Vector3.Right;

            //And the drop cinematic is a second Lerp over the top of that one, on its own reversible scalar
            //and for the same reason: at a blend of 0 these three lines return the pose above bit for bit, so
            //a cinematic that ends — or is skipped halfway — hands the player back exactly the frame the game
            //would have given them. The tilt rides along, and is the camera's only deliberate roll.
            float cinematic = _cinematic.Blend;

            if (cinematic > 0f)
            {
                position = Vector3.Lerp(position, _cinematic.Position, cinematic);
                target = Vector3.Lerp(target, _cinematic.Target, cinematic);
                fov = MathHelper.Lerp(fov, _cinematic.FieldOfView, cinematic);
            }

            Camera.BasePosition = position;
            Camera.BaseTarget = target;
            Camera.FieldOfView = fov;
            Camera.BaseRoll = _cinematic.Roll * cinematic;

            Camera.Update(elapsed);
        }

        #endregion

        #region Fitting the camera and the gun to the level

        /// <summary>
        /// Where the lens sits for a given stand-off — the overview pose, and the very pose
        /// <see cref="GameCameraFit.Solve"/> searches over, so the one expression has one home. The bearing it
        /// stands back along is flattened to the horizontal; the reason is on
        /// <see cref="Cannon.StandBearing"/>, along with the camera's drop below the trunnions.
        /// </summary>
        private Vector3 GameCameraPositionAt(float distance) => GameCameraFit.CameraPosition(_cannon, distance);

        /// <summary>
        /// The viewport has changed size or shape under the session: the aim's mouse baseline is stale (the
        /// delta is measured against the viewport centre, which just moved — left alone, the frame after an
        /// F11 reads a delta of half the screen and slams the barrel into its elevation clamp), and the
        /// camera's fit has to be re-solved, since which frustum axis binds flips with the aspect.
        /// </summary>
        internal void OnViewportChanged()
        {
            _mouseAim.Invalidate();

            FitCannonAndGameCameraToLevel();
        }

        /// <summary>
        /// Solves the gun's orbit radius and the camera's stand-off and aim height together — each depends on
        /// the other, since the camera is placed to frame the field <i>and the gun</i> while the gun is placed a
        /// fixed distance in front of the camera. The whole of it is <see cref="GameCameraFit"/>'s, shared with
        /// the Testbed since #76; what is left here is the field this level happens to have, the overview lens
        /// it is framed through, and the three assignments the solve implies. Run on every level load and every
        /// resize, never per frame.
        /// </summary>
        private void FitCannonAndGameCameraToLevel()
        {
            if (_map == null) return;

            //The half-extents are the ceiling plate's own footprint, asked of the one helper that applies the
            //margin, so the corners that are framed ARE the corners of the glass that is drawn — written out
            //here again they would silently stop agreeing with the plate and the collidable the moment it is
            //retuned. bottomY is the field's floor in WORLD Y, and it is the one thing that differs from the
            //Testbed's caller: there the lattice frame IS the world frame, while here level 0 sits at the
            //cluster's offset. A deep level's empty growth levels are inside the fitted volume on purpose —
            //the cluster grows down into them, so they have to be in frame before the first ball lands there.
            //And topY is the top of what is FRAMED, which is the glass on every field up to FRAMED_LEVELS
            //deep — every level that exists — and the top of the window on anything taller. A tall level's
            //glass hangs above the frame and descends into it; framing it would stand the camera back far
            //enough to fit a column that is mostly not in play yet, and shrink the part that is.
            CameraFit fit = GameCameraFit.Solve(_cannon, Game.CannonRig.BarrelReach,
                CeilingPlate.FootprintFor(_map.StageSizeX) * Constants.HALF,
                CeilingPlate.FootprintFor(_map.StageSizeZ) * Constants.HALF,
                _clusterWorldOffset.Y,
                FramedTopY(),
                GAME_FOV, Camera.AspectRatio);

            _gameCameraDistance = fit.CameraDistance;
            _gameCameraTargetY = fit.CameraTargetY;

            //The two writes the solve implies, once, at the end: the rest radius, then the walk the player
            //gets around it (W/S). The order matters — OrbitRadius parks the gun at rest, and SetAdvanceRange
            //clamps against wherever it stands and kills any glide still running — so a re-solve mid-level (a
            //resize) also resets a stroke in progress, the same reset the aim's baseline takes on the event.
            _cannon.OrbitRadius = fit.CannonOrbitRadius;
            _cannon.SetAdvanceRange(fit.CannonMinRadius, fit.CannonMaxRadius);
            _cannon.ElevationLimit = SolveElevationLimit();

            Console.WriteLine($"[camera] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}"
                + (FieldIsTallerThanFrame ? $" (framing its lowest {FRAMED_LEVELS} to y {FramedTopY():F1})" : string.Empty)
                + $", aspect {Camera.AspectRatio:F2}: "
                + $"camera {_gameCameraDistance:F1} out, aim Y {_gameCameraTargetY:F1}, "
                + $"gun orbit {_cannon.OrbitRadius:F1} ({_gameCameraDistance - _cannon.OrbitRadius:F1} in front of the lens"
                + $", walk {fit.CannonMinRadius:F1}..{fit.CannonMaxRadius:F1})"
                + $", elevation limit {_cannon.ElevationLimit * (180f / MathF.PI):F1} deg");

            LogAimReachability();
        }

        /// <summary>
        /// Whether the gun, where the fit has just put it, can be aimed at every cell of this field — the
        /// check that says a level is finishable before anyone tries to finish it.
        /// <para>
        /// <b>The Testbed's <c>aimcheck</c> stopped answering this the day levels could be taller than the
        /// camera frames.</b> That check runs against the Testbed's own fit, which frames the whole field
        /// however deep it is and therefore stands the gun far enough back to see it: on <c>Column</c> it
        /// reported an orbit of 40.3 and a comfortable 43°, while the Game — framing the lowest sixteen
        /// levels — orbits at 15.5, less than half as far out, and asks a far steeper angle of the same
        /// cells. A verification that measures a geometry the player never plays is worse than none, so the
        /// executable that owns the geometry reports it.
        /// </para>
        /// </summary>
        private void LogAimReachability()
        {
            //On a tall field only the framed window is asked about, and against the limit the gun is actually
            //held to there. Its upper levels are unreachable now BY DESIGN — that is what the elevation limit
            //enforces and what the descent undoes — so asking about them returns "unfinishable" for a level
            //that finishes perfectly well. What matters is that everything in play can be reached.
            AimReachabilityResult reach = AimReachability.Check(
                _map, _cannon.OrbitRadius, _cannon.Position.Y, _cannon.ElevationLimit,
                FieldIsTallerThanFrame ? FRAMED_LEVELS - 1 : int.MaxValue,
                _clusterWorldOffset);

            const float RAD_TO_DEG = 180f / MathF.PI;

            Console.WriteLine($"[aimcheck]{(FieldIsTallerThanFrame ? " (framed window only)" : string.Empty)}"
                + $" steepest cell ({reach.WorstCell.X},{reach.WorstCell.Z},{reach.WorstCell.Level})"
                + $" at Y={reach.WorstCellY:F1} needs {reach.WorstElevation * RAD_TO_DEG:F1} deg"
                + $" of the limit's {_cannon.ElevationLimit * RAD_TO_DEG:F1}  ->  "
                + (reach.Pass
                    ? "PASS"
                    : $"FAIL - {reach.UnreachableCells}/{reach.TotalCells} cells out of reach (unfinishable)"));
        }

        /// <summary>
        /// How steeply the gun may be aimed on this level: its full <see cref="Cannon.MaxElevation"/> on any
        /// field the camera frames whole, and only as far as the <b>top of the window</b> on a tall one.
        /// <para>
        /// A tall level's column reaches up out of shot, and with the full 80° the player can aim at balls
        /// they cannot see — shooting blind into the part of the level that has not arrived yet, which reads
        /// as the level being cheatable rather than as a rule. The limit is the same geometry
        /// <see cref="AimReachability"/> measures with, run backwards: the steepest <i>facing</i> shot, from
        /// the gun's stand-off to the field's nearest corner, that still reaches the framed top. So
        /// everything inside the window stays reachable — including the near edge, which is the steepest
        /// legitimate shot there is — and nothing above it is.
        /// </para>
        /// <para>
        /// Solved with the fit rather than authored per level, because both of its inputs are the fit's: the
        /// orbit radius and the window follow the field's size, so a number written into the level file would
        /// stop agreeing with the geometry the first time either moved.
        /// </para>
        /// </summary>
        private float SolveElevationLimit()
        {
            if (!FieldIsTallerThanFrame) return Cannon.MaxElevation;

            //Floored at 1 for the same reason AimReachability floors it: a cell under the carriage must not
            //divide the angle by nothing.
            float nearest = MathF.Max(_cannon.OrbitRadius - FieldHalfDiagonal(), 1f);
            float rise = FramedTopY() - _cannon.Position.Y;

            return MathF.Atan2(rise, nearest);
        }

        /// <summary>
        /// The world Y the camera frames up to: the upper face of the glass, or the top of the window when
        /// the field is taller than <see cref="FRAMED_LEVELS"/>.
        /// <para>
        /// It reads the glass at its <b>current</b> height, which is what a mid-level re-solve (a resize)
        /// wants — but the cap is measured from the field's floor and does not move, so a tall level's frame
        /// is the same before and after a descent. That is the point: the camera holds still and the column
        /// comes down through it.
        /// </para>
        /// </summary>
        private float FramedTopY()
        {
            float glass = CeilingPlate.TopFaceY(_ceilingY);
            float window = _clusterWorldOffset.Y + (FRAMED_LEVELS - 1) / Constants.SQRT_TWO;

            return MathF.Min(glass, window);
        }

        #endregion

        #region The gun's recoil

        //What used to be the gun's geometry: the barrel's pose — the aim, the muzzle, the basis and the draw
        //matrix — is Cannon's own since #76, and the tube's figures are CannonRig's. All that is left here is
        //the stroke, because only this executable animates one.

        /// <summary>
        /// How far back along the bore the barrel is displaced by its own recoil this instant, in world units,
        /// and exactly zero once the stroke is over. Squared rather than linear in the stroke, so the shot throws
        /// the gun back at once and the return eases off, which is the shape a recoiling barrel has (the same
        /// reasoning as <see cref="CameraShake"/>'s: a linear amplitude spends most of its life mid-stroke and
        /// reads as a wobble instead of a jolt).
        /// <para>
        /// Handed to <see cref="Cannon.BarrelWorld"/> and <see cref="Magazine.Pose"/> — which is to say applied
        /// where the gun is <b>drawn</b> and nowhere else. A shot leaves along the true aim on the frame it is
        /// fired, before the barrel has moved, so nothing about where a ball goes may depend on this: neither
        /// <see cref="Shoot"/> nor <see cref="Cannon.AimDirection"/> takes it, and feeding it in there is exactly
        /// what a reader would "fix". A <b>positive</b> scalar, since the shared pose subtracts it along the
        /// bore; the shape and the decay stay here because the shared pose owns neither, on purpose.
        /// </para>
        /// </summary>
        private float CannonRecoilBack() =>
            _cannonRecoil <= 0f ? 0f : CANNON_RECOIL_BACK * _cannonRecoil * _cannonRecoil;

        #endregion
    }
}
