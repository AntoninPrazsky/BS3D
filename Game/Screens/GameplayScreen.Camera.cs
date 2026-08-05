using Microsoft.Xna.Framework;
using Prazsky.BS3D;
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
            CameraFit fit = GameCameraFit.Solve(_cannon, Game.CannonRig.PivotToFrontBall + Constants.HALF,
                CeilingPlate.FootprintFor(_map.StageSizeX) * Constants.HALF,
                CeilingPlate.FootprintFor(_map.StageSizeZ) * Constants.HALF,
                _clusterWorldOffset.Y,
                CeilingPlate.TopFaceY(_ceilingY), //upper face of the glass, wherever the descent has it now
                GAME_FOV, Camera.AspectRatio);

            _gameCameraDistance = fit.CameraDistance;
            _gameCameraTargetY = fit.CameraTargetY;

            //The two writes the solve implies, once, at the end: the rest radius, then the walk the player
            //gets around it (W/S). The order matters — OrbitRadius parks the gun at rest, and SetAdvanceRange
            //clamps against wherever it stands and kills any glide still running — so a re-solve mid-level (a
            //resize) also resets a stroke in progress, the same reset the aim's baseline takes on the event.
            _cannon.OrbitRadius = fit.CannonOrbitRadius;
            _cannon.SetAdvanceRange(fit.CannonMinRadius, fit.CannonMaxRadius);

            Console.WriteLine($"[camera] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}, aspect {Camera.AspectRatio:F2}: "
                + $"camera {_gameCameraDistance:F1} out, aim Y {_gameCameraTargetY:F1}, "
                + $"gun orbit {_cannon.OrbitRadius:F1} ({_gameCameraDistance - _cannon.OrbitRadius:F1} in front of the lens"
                + $", walk {fit.CannonMinRadius:F1}..{fit.CannonMaxRadius:F1})");
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
