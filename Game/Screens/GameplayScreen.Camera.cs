using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
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
        /// The overview's look-at <b>tilts with the barrel's elevation</b> (<see cref="AimTilt"/>, #720): up as the
        /// gun is raised, down as it is lowered, chased on real time and zero at the aim the level opened on.
        /// </para>
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
            //fade rather than a drop. At a blend of 0 the pose below is the overview pose bit for bit — the
            //overview as the aim has tilted it (#720), whose chase runs whether the lean is in or out, so letting
            //go lands on a tilt that already follows the barrel and an interrupted hold never snaps.
            Vector3 lensMuzzle = _cannon.MuzzlePosition(Game.CannonRig.PivotToFrontBall);
            Vector3 lensAim = _cannon.AimDirection;

            //Where the lens converges (#382). UpdateShotPreview runs five lines before this in the frame and
            //has ALREADY swept the flight against the balls, so _previewBeamEnd is this frame's true first
            //contact — the number the convergence wanted all along, and it used to be thrown away while this
            //converged on the whole level's average instead. Projected onto the aim rather than taken as a
            //point, because LensTarget's contract is a point ON the shot ray: what parallax is about is DEPTH,
            //and the projection is exactly the depth of the impact along the line of sight. That also keeps
            //the curved flights of #332 honest — an arc's contact lies off the aim, and its depth does not.
            //
            //And when the sweep found nothing, today's projection is the fallback and NOT the clamp's ceiling:
            //an aim swung out over open sky has no impact to converge on, and snapping the look-at to 90 units
            //out there is precisely the flick the ease exists to prevent.
            float convergeTarget = _previewReachesCluster
                ? Vector3.Dot(_previewBeamEnd - lensMuzzle, lensAim)
                : PreciseAim.DepthToClusterCentre(lensMuzzle, lensAim,
                    new Vector3(_cannon.OrbitCenter.X, _clusterCentreY, _cannon.OrbitCenter.Z));

            _preciseAim.Step(_adsPull, elapsed, convergeTarget);

            Vector3 overviewPosition = GameCameraPositionAt(_gameCameraDistance, TrailedBearing(elapsed));
            Vector3 overviewTarget = TiltedOverviewTarget(overviewPosition,
                new Vector3(_cannon.OrbitCenter.X, _gameCameraTargetY, _cannon.OrbitCenter.Z), lensMuzzle, elapsed);

            //Taken as a VALUE, not written into the camera: the cinematic below goes on lerping over it, and
            //the base pose the shake composes onto is whatever comes out of both. The muzzle and the aim are
            //read after _cannon.Update this frame, or the lens lags the barrel and reads as jitter — and
            //without the recoil, which is the barrel's drawing offset and not where it is pointed. The depth
            //it converges at is the eased one Step took above.
            AimPose aim = _preciseAim.BlendedPose(overviewPosition, overviewTarget, GAME_FOV, lensMuzzle, lensAim);

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

            //And the chapter intro is a third Lerp, on the identical idiom (#267) — chained after the drop
            //cinematic's rather than before it for no reason beyond call order, since CameraTakeoverEngaged's
            //own remarks are what actually keep the two from ever being nonzero on the same frame. No roll:
            //an establishing shot stays level, so only the drop cinematic's tilt above ever moves BaseRoll.
            //The line's loss, on the same idiom again (#434). Before the chapter intro for no reason beyond
            //call order: the two cannot be nonzero on the same frame - an intro runs on a level's opening and
            //this on its ending - and CameraTakeoverEngaged is what actually holds that.
            float lineLoss = _lineLoss.Blend;

            if (lineLoss > 0f)
            {
                position = Vector3.Lerp(position, _lineLoss.Position, lineLoss);
                target = Vector3.Lerp(target, _lineLoss.Target, lineLoss);
                fov = MathHelper.Lerp(fov, _lineLoss.FieldOfView, lineLoss);
            }

            float intro = _chapterIntro.Blend;

            if (intro > 0f)
            {
                position = Vector3.Lerp(position, _chapterIntro.Position, intro);
                target = Vector3.Lerp(target, _chapterIntro.Target, intro);
                fov = MathHelper.Lerp(fov, _chapterIntro.FieldOfView, intro);
            }

            Camera.BasePosition = position;
            Camera.BaseTarget = target;
            Camera.FieldOfView = fov;
            Camera.BaseRoll = _cinematic.Roll * cinematic;

            Camera.Update(elapsed);

            if (_test.LogTilt) LogTiltFrame();
        }

        /// <summary>
        /// How long the lens takes to give up most of its lag behind the gun's A/D turn: the time constant of
        /// a first-order chase, so it settles asymptotically and <b>never overshoots</b> — a sight that wobbled
        /// back would read as loose machinery rather than as weight (#366).
        /// <para>
        /// It buys a trail of <c>turn rate × this</c> while a key is held: at the gun's full orbit speed
        /// (<see cref="Cannon.RotationSpeed"/>, 1 rad/s) that is about 12°, and the lens is back behind the gun
        /// within ~0.7 s of letting go. Read as a trail rather than as a delay: the frame keeps moving the
        /// whole time, it simply arrives after the gun does.
        /// </para>
        /// </summary>
        private const float BEARING_TRAIL_SECONDS = 0.22f;

        //The lens's own orbit angle, chasing the gun's. Seeded on the first frame it is asked for after a
        //fit rather than in a constructor: FitCannonAndGameCameraToLevel can put the gun somewhere new
        //between levels, and a trail that eased in from the previous level's bearing would swing the whole
        //frame around the arena on the first frame of the next one.
        private float _lensBearingAngle;
        private bool _lensBearingSeeded;

        /// <summary>
        /// The direction the overview lens stands back along: the gun's own, trailed. It lags a turn by
        /// <see cref="BEARING_TRAIL_SECONDS"/> and eases in when the gun stops, which is what makes an A/D turn
        /// read as the gun pulling away from the camera rather than as the world spinning about a fixed gun.
        /// <para>
        /// <b>Precise aim gets none of it</b>, and not by a switch: the lag is interpolated out by
        /// <see cref="PreciseAim.Blend"/> itself, so the pose ADS blends <i>from</i> is already the true
        /// bearing by the time the blend has any weight, and there is no frame on which pressing RMB moves the
        /// overview end of that Lerp. A snap-when-held would have done it at the cost of the very jump #322
        /// closed for the walk.
        /// </para>
        /// </summary>
        private Vector3 TrailedBearing(float elapsed)
        {
            Vector3 standing = _cannon.StandBearing;
            float trueAngle = MathF.Atan2(standing.Z, standing.X);

            if (!_lensBearingSeeded)
            {
                _lensBearingAngle = trueAngle;
                _lensBearingSeeded = true;
            }

            //Chased as an ANGLE and through WrapAngle, not as a lerp of the two direction vectors: a vector
            //lerp cuts the chord, so it shortens as it goes and would drift the lens off its stand-off, and it
            //takes the long way round whenever the two bearings straddle ±π.
            float behind = MathHelper.WrapAngle(trueAngle - _lensBearingAngle);

            _lensBearingAngle = MathHelper.WrapAngle(
                _lensBearingAngle + behind * (1f - MathF.Exp(-elapsed / BEARING_TRAIL_SECONDS)));

            float aimed = MathHelper.WrapAngle(
                _lensBearingAngle + MathHelper.WrapAngle(trueAngle - _lensBearingAngle) * _preciseAim.Blend);

            return new Vector3(MathF.Cos(aimed), 0f, MathF.Sin(aimed));
        }

        //The overview's tilt with the aim (#720), reseeded by a level's fit like the bearing's trail above
        private readonly AimTilt _aimTilt = new();

        //The elevation clamp the level opened against (#842), as the fit solved it (OpeningElevationLimit): the tilt's
        //zero stands under it. Held for the attempt and not read off Cannon.ElevationLimit, which LiftTallAimOnceFramed
        //raises mid-attempt while the barrel stays where it is: a zero read live would jump up to the rest there and dip
        //the frame with no hand on the aim
        private float _aimTiltOpeningLimit = Cannon.MaxElevation;

        //What the last frame's tilt measured from, was asked for and was allowed, kept for the logtilt line only
        private float _aimTiltZero, _aimTiltTarget, _aimTiltRoomUp, _aimTiltRoomDown;

        //The elevation the hand has put the barrel at: the rest plus the aim, without the rubber's overshoot (#431). The
        //rubber is the clamp refusing a push, and the gun will not fire while it is out (Cannon.ElevationRefusesShot), so
        //it is not where a shot will go; a lens that followed it would bob each time a held tall level's clamp is pushed
        private float AimedElevation => _cannon.RestElevation + _cannon.ElevationAim;

        /// <summary>
        /// The overview look-at, tilted with the barrel's elevation (<see cref="AimTilt"/>, #720): the hand's
        /// (<see cref="AimedElevation"/>), measured from the aim the level opens on — the gun's rest from where it stands,
        /// or the clamp a tall level opened against where that is lower (<see cref="_aimTiltOpeningLimit"/>) — so an
        /// untouched gun leaves the solved frame exactly as it is; stopped on the way up wherever the muzzle would sink
        /// under <see cref="AimTilt.GUN_KEEP_FRACTION"/> of the frame (the owner: some of the gun stays in view), and on
        /// the way down wherever the cluster's top row would rise past <see cref="AimTilt.TOP_KEEP_FRACTION"/> of it
        /// (<see cref="TopRowKeepRoom"/>).
        /// <para>
        /// <b>The overview end only</b>, the bearing's rule (#366): precise aim's Lerp takes this point as where it leans
        /// from, so the lean interpolates the tilt out, and the chase runs on every frame held or not. The pose the
        /// lens returns to on a release has followed the barrel the whole time.
        /// </para>
        /// <para>
        /// <b>Real time, not the simulation's</b>: <paramref name="elapsed"/> is the frame's unscaled wall time, as the
        /// bearing's trail and the lean take it, so a slowed drop does not slow the lens.
        /// </para>
        /// </summary>
        private Vector3 TiltedOverviewTarget(Vector3 lens, Vector3 lookAt, Vector3 muzzle, float elapsed)
        {
            //The zero moves with the walk (the rest steepens as the gun walks in) so a walk does not tilt the frame — the
            //walk has its own answer (#322); and on a tall level it is the clamp the level opened against (#842), which
            //is where that gun rests, and it stays there when the clamp lifts, so the raise past it is what tilts
            _aimTiltZero = MathF.Min(_cannon.RestElevation, _aimTiltOpeningLimit);

            _aimTiltRoomUp = AimTilt.GunKeepLimit(lens, lookAt, muzzle, GAME_FOV);
            _aimTiltRoomDown = TopRowKeepRoom(lens, lookAt);
            _aimTiltTarget = AimTilt.Bounded(AimTilt.TargetFor(AimedElevation - _aimTiltZero), _aimTiltRoomUp, _aimTiltRoomDown);

            return AimTilt.Tilted(lens, lookAt, _aimTilt.Step(_aimTiltTarget, elapsed));
        }

        /// <summary>
        /// How far down the overview may tilt before the cluster's top row rises past
        /// <see cref="AimTilt.TOP_KEEP_FRACTION"/> of the frame (<see cref="AimTilt.TopKeepLimit"/>): the least over the
        /// four top corners of the plate's footprint — the corners the fit framed, the nearest one binding.
        /// </summary>
        private float TopRowKeepRoom(Vector3 lens, Vector3 lookAt)
        {
            //Between a level's teardown and the next one's build there is no field to keep
            if (_map == null) return 0f;

            float room = float.MaxValue;

            for (int corner = 0; corner < 4; corner++)
                room = MathF.Min(room, AimTilt.TopKeepLimit(lens, lookAt, TopRowCorner(corner), GAME_FOV));

            return room;
        }

        /// <summary>
        /// One of the four top corners of the cluster's top row (0..3), over the plate's footprint as the fit measures
        /// it. The row is the glass's underside, where the top balls hang (their tops are socketed to it), so it follows
        /// the descent and a brake — or the framed window's top (<see cref="FramedTopY"/>) where that is lower, as it is
        /// at the opening of any field of 16 levels or more and on a tall level until the column hangs whole in the
        /// frame, so a low aim never takes what the fit framed, nor the band #842 holds, off the top of the frame.
        /// </summary>
        private Vector3 TopRowCorner(int corner)
        {
            float rowY = MathF.Min(_ceilingDescent.Y - CeilingPlate.THICKNESS * Constants.HALF, FramedTopY());
            float halfX = CeilingPlate.FootprintFor(_map.StageSizeX) * Constants.HALF;
            float halfZ = CeilingPlate.FootprintFor(_map.StageSizeZ) * Constants.HALF;

            return new Vector3(
                _cannon.OrbitCenter.X + ((corner & 1) == 0 ? -halfX : halfX),
                rowY,
                _cannon.OrbitCenter.Z + ((corner & 2) == 0 ? -halfZ : halfZ));
        }

        /// <summary>
        /// The <c>logtilt</c> line (#720), once a frame and only on request: the hand's elevation
        /// (<see cref="AimedElevation"/>) against the tilt's zero, what the
        /// tilt was asked for, the room up and down it was allowed and what it gave, the lean's blend, the camera's
        /// pitch, and where the cluster's top row, the muzzle and the beam's end land in the frame (0 the top edge, 1 the
        /// bottom, outside that range off the frame). What a press or a release does to the pose is read off
        /// consecutive lines, which no screenshot schedule can give.
        /// </summary>
        private void LogTiltFrame()
        {
            const float RAD_TO_DEG = 180f / MathF.PI;

            Vector3 forward = Camera.Target - Camera.Position;
            float pitch = MathF.Atan2(forward.Y, MathF.Sqrt(forward.X * forward.X + forward.Z * forward.Z));

            Console.WriteLine(FormattableString.Invariant(
                $"[tilt] t {WallClock:F3} elev {AimedElevation * RAD_TO_DEG:F2} zero {_aimTiltZero * RAD_TO_DEG:F2}")
                + FormattableString.Invariant(
                $" target {_aimTiltTarget * RAD_TO_DEG:F3} up {_aimTiltRoomUp * RAD_TO_DEG:F2} down {_aimTiltRoomDown * RAD_TO_DEG:F2} tilt {_aimTilt.Angle * RAD_TO_DEG:F3}")
                + FormattableString.Invariant(
                $" blend {_preciseAim.Blend:F3} pitch {pitch * RAD_TO_DEG:F3} pos {Camera.Position.X:F3},{Camera.Position.Y:F3},{Camera.Position.Z:F3}")
                + (_map == null ? " topY none" : FormattableString.Invariant(
                $" topY {MathF.Min(MathF.Min(ScreenY(TopRowCorner(0)), ScreenY(TopRowCorner(1))), MathF.Min(ScreenY(TopRowCorner(2)), ScreenY(TopRowCorner(3)))):F3}"))
                + FormattableString.Invariant(
                $" muzzleY {ScreenY(_cannon.MuzzlePosition(Game.CannonRig.PivotToFrontBall)):F3}")
                + (_previewBeamVisible
                    ? FormattableString.Invariant($" beamY {ScreenY(_previewBeamEnd):F3}{(_previewReachesCluster ? " hit" : " open")}")
                    : " beam none"));
        }

        //A world point's height in the frame through the camera as it will draw: 0 the top edge, 1 the bottom
        private float ScreenY(Vector3 point)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), Camera.View * Camera.Projection);
            return clip.W <= 0f ? float.NaN : (1f - clip.Y / clip.W) * 0.5f;
        }

        #endregion

        #region Fitting the camera and the gun to the level

        /// <summary>
        /// Where the lens sits for a given stand-off — the overview pose, built from the same expression
        /// <see cref="GameCameraFit.Solve"/> searches over, so the one pose has one home. The stand-off handed
        /// in is the level's; what comes back also carries the share of a <b>retreat</b> the lens goes along
        /// with (<see cref="GameCameraFit.CANNON_ADVANCE_FOLLOW"/>, #322), which is why the W/S walk moves the
        /// frame without the fit being re-solved. The bearing it stands back along is flattened to the
        /// horizontal; the reason is on <see cref="Cannon.StandBearing"/>, along with the camera's drop below
        /// the trunnions.
        /// <para>
        /// The bearing-less overload stands the lens on the gun's own ray, which is what the level's opening
        /// pose and the chapter intro's home pose want. The per-frame gameplay lens takes the other one and
        /// hands it a <see cref="TrailedBearing"/> instead (#366).
        /// </para>
        /// </summary>
        private Vector3 GameCameraPositionAt(float distance) => GameCameraFit.CameraPosition(_cannon, distance);

        /// <summary>As above, about a bearing the caller has trailed behind the gun's own.</summary>
        private Vector3 GameCameraPositionAt(float distance, Vector3 bearing) =>
            GameCameraFit.CameraPosition(_cannon, distance, bearing);

        /// <summary>
        /// The viewport has changed size or shape under the session: the aim's mouse baseline is stale (the
        /// delta is measured against the viewport centre, which just moved — left alone, the frame after an
        /// F11 reads a delta of half the screen and slams the barrel into its elevation clamp), and the
        /// camera's fit has to be re-solved, since which frustum axis binds flips with the aspect.
        /// </summary>
        internal void OnViewportChanged()
        {
            _mouseAim.Invalidate();

            //What the player had built up before the re-solve, beside the [camera] line's after — the pair is how
            //a resize is shown to keep the walk and the turn rather than asserted to
            if (_map != null) Console.WriteLine($"[resize] before: {DescribeStance()}");

            FitCannonAndGameCameraToLevel(keepStance: true);
        }

        /// <summary>Where the gun stands, for the log: its radius against its rest and walk, its bearing and aim.</summary>
        private string DescribeStance()
        {
            Vector3 bearing = _cannon.StandBearing;
            return $"gun at {_cannon.OrbitRadius:F2} (rest {_cannon.RestRadius:F2}, walk {_cannon.AdvanceMin:F2}..{_cannon.AdvanceMax:F2}),"
                + $" bearing {MathF.Atan2(bearing.Z, bearing.X) * (180f / MathF.PI):F1} deg,"
                + $" traverse {_cannon.Traverse * (180f / MathF.PI):F1} deg, elevation {_cannon.Elevation * (180f / MathF.PI):F1} deg";
        }

        /// <summary>
        /// Solves the gun's orbit radius and the camera's stand-off and aim height together — each depends on
        /// the other, since the camera is placed to frame the field <i>and the gun</i> while the gun is placed a
        /// fixed distance in front of the camera. The whole of it is <see cref="GameCameraFit"/>'s, shared with
        /// the Testbed since #76; what is left here is the field this level happens to have, the overview lens
        /// it is framed through, and the three assignments the solve implies. Run on every level load and every
        /// resize, never per frame.
        /// </summary>
        /// <param name="keepStance">A resize's re-solve: the player's W/S walk is kept as a share of the new
        /// range (<see cref="Cannon.Refit"/>) rather than the gun being parked at rest, which is what a level
        /// load wants and every resize used to do.</param>
        private void FitCannonAndGameCameraToLevel(bool keepStance = false)
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
            //gets around it (W/S). A level load PLACES the gun: OrbitRadius parks it at rest, and
            //SetAdvanceRange clamps against wherever it stands and kills any glide still running. A resize must
            //not — it re-solved the frame, not the level, and parking the gun there threw away the player's walk
            //and jumped the lens that follows a retreat — so it hands the three over together and Refit keeps
            //the walk as the same share of the new range. The orbit angle (A/D) survives either way.
            if (keepStance) _cannon.Refit(fit.CannonOrbitRadius, fit.CannonMinRadius, fit.CannonMaxRadius);
            else
            {
                _cannon.OrbitRadius = fit.CannonOrbitRadius;
                _cannon.SetAdvanceRange(fit.CannonMinRadius, fit.CannonMaxRadius);
            }
            //A load holds a tall level's working band where the level hangs it (#842); a resize keeps what was held and
            //only re-angles it for where the gun now stands
            if (!keepStance) HoldTallAimBand();
            _cannon.ElevationLimit = SolveElevationLimit();
            //The overview tilt's zero sits under the clamp the level opens against (#720): re-angled with it on a resize,
            //and held through the attempt when the clamp lifts
            _aimTiltOpeningLimit = OpeningElevationLimit();
            MarkBrakeReachStale();

            //The lens's trail starts behind the gun rather than easing in from where the last level left it
            //(#366) — a placed gun is a placed camera, the same rule Cannon.RestRadius follows for the walk. The aim's
            //tilt (#720) starts over on a load the same way; a resize keeps it, since the aim it follows is kept
            _lensBearingSeeded = false;
            if (!keepStance) _aimTilt.Reseed();

            Console.WriteLine($"[camera] Field {_map.StageSizeX}x{_map.StageSizeZ}x{_map.Levels}"
                + (FieldIsTallerThanFrame ? $" (framing its lowest {FRAMED_LEVELS} to y {FramedTopY():F1})" : string.Empty)
                + $", aspect {Camera.AspectRatio:F2}: "
                + $"camera {_gameCameraDistance:F1} out, aim Y {_gameCameraTargetY:F1}, "
                + $"gun orbit {_cannon.RestRadius:F1} ({_gameCameraDistance - _cannon.RestRadius:F1} in front of the lens"
                + $", walk {fit.CannonMinRadius:F1}..{fit.CannonMaxRadius:F1})"
                + $", elevation limit {_cannon.ElevationLimit * (180f / MathF.PI):F1} deg; {DescribeStance()}");

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
            //On a tall field the question is asked of the WORKING BAND the load held (#842) — the underside plus
            //the aim's headroom, where the level hung it — and against the limit the gun is held to there.
            //Everything above that band is unreachable by design until the descent brings it down into the
            //same height, and asking about it where it hangs returns "unfinishable" for a level that finishes.
            //Once the clamp has lifted (the column hangs whole in the frame) every level is asked, where it hangs
            //now, still within the cluster's held reach: the field's empty corners are growth room no shot needs.
            //
            //⚠ BUT ON A TALL FIELD THIS COMPARISON IS AGAINST ITSELF, and this comment claimed otherwise
            //until #527. SolveElevationLimit below returns min(MaxElevation, WorstElevation + margin) off
            //this very same Check over this very same band, so the band's own limit can never be what fails
            //it — a FAIL here needs the steepest cell to exceed Cannon.MaxElevation outright, which is the
            //gun's clamp and nothing to do with the band. The line is still worth printing (it is what would
            //say so the day a fit stands the gun too close), but a PASS on a tall level is not evidence the
            //level is playable. AimReachability's own class doc and BestPractices.md §10 carry it.
            bool banded = FieldIsTallerThanFrame && !_tallAimLifted;

            AimReachabilityResult reach = AimReachability.Check(
                _map, _cannon.RestRadius, RestTrunnionY, _cannon.ElevationLimit,
                banded ? _tallAimHeldBandTop : int.MaxValue,
                banded ? _clusterWorldOffset : AimWorldOffset(),
                FieldIsTallerThanFrame ? _tallAimHeldHorizontal : float.MaxValue);

            const float RAD_TO_DEG = 180f / MathF.PI;

            Console.WriteLine($"[aimcheck]{(banded ? " (the working band only)" : string.Empty)}"
                + $" steepest cell ({reach.WorstCell.X},{reach.WorstCell.Z},{reach.WorstCell.Level})"
                + $" at Y={reach.WorstCellY:F1} needs {reach.WorstElevation * RAD_TO_DEG:F1} deg"
                + $" of the limit's {_cannon.ElevationLimit * RAD_TO_DEG:F1}  ->  "
                + (reach.Pass
                    ? "PASS"
                    : $"FAIL - {reach.UnreachableCells}/{reach.TotalCells} cells out of reach (unfinishable)"));
        }

        //What a tall level's aim was solved for at the load (#842): the working band's top level and how far out the
        //cluster reached, both as the level hung before its first descent. Held for the whole attempt, so the clamp
        //reaches the same height in the world from the first shot to the last; a resize only re-angles it for where
        //the gun now stands.
        private int _tallAimHeldBandTop;
        private float _tallAimHeldHorizontal;

        //Set once what is left of the cluster hangs whole inside the framed window (#842): from then on the tall level
        //is aimed the way a short one is, at the gun's full range, until the next load. A ratchet, so a brake that lifts
        //the glass back a step does not take the freedom away again.
        private bool _tallAimLifted;

        /// <summary>
        /// How steeply the gun may be aimed on this level: its full <see cref="Cannon.MaxElevation"/> on any
        /// field the camera frames whole, and only as far as the <b>working band</b> on a tall one — the
        /// column's underside plus <see cref="TALL_AIM_HEADROOM_LEVELS"/>, <b>as the level hung it at the load</b>
        /// (<see cref="HoldTallAimBand"/>) — until the column hangs whole in the frame, when the clamp lifts
        /// (<see cref="LiftTallAimOnceFramed"/>).
        /// <para>
        /// A tall level's column reaches up out of shot, and with the full 80° the player could cut it off the
        /// glass in the first shots and aim at balls they cannot see. The limit is the same geometry
        /// <see cref="AimReachability"/> measures with, run backwards: the steepest <i>facing</i> shot, from
        /// the gun's stand-off to the band's nearest cell, plus <see cref="TALL_AIM_MARGIN"/>. So everything
        /// inside the band stays reachable — including the near edge, which is the steepest legitimate shot
        /// there is — and nothing much above it is. The band is not the top of the framed window, which is
        /// what it reached first and was far too generous: the whole column could be taken in two or three
        /// shots, because a band cut high in the window orphans the entire visible column under it.
        /// </para>
        /// <para>
        /// <b>Held, not re-solved, as the glass descends (#842, the owner's ruling).</b> From #582 to #842 it was
        /// re-solved on every ceiling step for the band where it hung then, so every step lowered the band and the
        /// clamp with it: measured in play, about 2° a step, Totem from 24.9° at the load to 10.5° by its end,
        /// while every short level kept its 80° — "in smaller levels I could shoot higher". The clamp exists so a
        /// tall level is not cut loose in its opening shots, and that is a fact about the start: held at the
        /// load's height, the descent brings more of the column into the same reach rather than taking reach
        /// away. Solved against the field's <b>cells</b> and not its balls, so the band at the load's hang
        /// (<see cref="_clusterWorldOffset"/>) is pure geometry: a resize re-solves exactly the same height in the
        /// world for the gun's new stand-off.
        /// </para>
        /// </summary>
        private float SolveElevationLimit() => _tallAimLifted ? Cannon.MaxElevation : OpeningElevationLimit();

        /// <summary>
        /// The clamp a level opens against: <see cref="Cannon.MaxElevation"/> on a field the camera frames whole, and the
        /// working band's (<see cref="SolveElevationLimit"/> says how it is solved) on a tall one, whether or not the
        /// clamp has lifted since (<see cref="LiftTallAimOnceFramed"/>). It is also where the overview's tilt measures
        /// from (#720), which must not move when the clamp does.
        /// </summary>
        private float OpeningElevationLimit()
        {
            if (!FieldIsTallerThanFrame) return Cannon.MaxElevation;

            //Asked of AimReachability rather than derived alongside it, and that is the point: the limit and
            //the check that polices it are then the SAME arithmetic and cannot disagree. Written by hand they
            //did — a limit measured to the cluster's near edge came out 1.4 degrees under what the check then
            //demanded of the ring around it, and the level reported itself unfinishable by a hair.
            //
            //WorstElevation is what the steepest cell in the working band needs and does not depend on the
            //clamp it is compared against, so passing the gun's full range here asks "how steep does this
            //level actually get", not "does it fit".
            //From the gun's REST stance, which a load stands it at and a resize re-solves: a resize keeps the
            //player's walk, and a clamp solved from wherever they had walked to would reach another height (#842)
            AimReachabilityResult needed = AimReachability.Check(
                _map, _cannon.RestRadius, RestTrunnionY, Cannon.MaxElevation,
                _tallAimHeldBandTop, _clusterWorldOffset, _tallAimHeldHorizontal);

            return MathF.Min(Cannon.MaxElevation, needed.WorstElevation + TALL_AIM_MARGIN);
        }

        /// <summary>
        /// Where the barrel pivots with the gun at its rest stance: the island's floor is a dish, so the trunnions'
        /// height follows the radius (<see cref="CannonRig.TrunnionHeightAt"/>), and the clamp and the feed's reach are
        /// asked from the rest and not from wherever the player has walked to.
        /// </summary>
        private float RestTrunnionY => CannonRig.TrunnionHeightAt(_cannon.RestRadius);

        /// <summary>
        /// Takes a tall level's working band as the level hangs at its load — the glass at rest, nothing shot —
        /// for <see cref="SolveElevationLimit"/> to hold for the whole attempt (#842), and clears the last attempt's
        /// lift. Called by a load's fit only: a resize keeps what was held.
        /// </summary>
        private void HoldTallAimBand()
        {
            _tallAimLifted = false;
            if (!FieldIsTallerThanFrame) return;

            _tallAimHeldBandTop = TallAimBandTop();
            _tallAimHeldHorizontal = TallAimHorizontalLimit();
        }

        /// <summary>
        /// Lifts a tall level's clamp to the gun's full range once what is left of the cluster hangs <b>whole
        /// inside the framed window</b> (#842): its highest occupied level at or under the window's top, where the
        /// glass is taking it. The owner's ruling — "once the level has descended low enough, the player may aim
        /// anywhere" — and the measure of "low enough" is the one the clamp exists for: nothing is left above the
        /// frame to shoot blind into, so the level has become what a short one is, and is aimed as one.
        /// <para>
        /// Asked on the frame a ceiling step begins, the one thing that brings the column down (a shot cannot take
        /// the top level away without the whole cluster falling with it), and the step is taken as already
        /// arrived — the glass slides it in well under a second. Once per step and never per frame; the log line
        /// is a rare event of the <c>[ceiling]</c> kind.
        /// </para>
        /// </summary>
        private void LiftTallAimOnceFramed()
        {
            //An empty map answers GetHighestOccupiedLevel with level 0, which would read as "framed"; and a cleared
            //level takes no more steps anyway
            if (!FieldIsTallerThanFrame || _tallAimLifted || _map.GetBallsCount() == 0) return;

            int highest = _map.GetHighestOccupiedLevel();
            int framed = FramedLevelTop();

            if (highest > framed) return;

            const float RAD_TO_DEG = 180f / MathF.PI;
            float before = _cannon.ElevationLimit;

            _tallAimLifted = true;
            _cannon.ElevationLimit = SolveElevationLimit();
            MarkBrakeReachStale();

            Console.WriteLine($"[aimlimit] lifted {before * RAD_TO_DEG:F1} -> {_cannon.ElevationLimit * RAD_TO_DEG:F1} deg:"
                + $" the cluster hangs whole in the frame (glass {_ceilingDescent.RestY - _ceilingDescent.TargetY:F2} below rest,"
                + $" top level {highest}, framed up to level {framed})");
        }

        /// <summary>
        /// Where the lattice hangs in the world <b>now</b>: <see cref="_clusterWorldOffset"/>, fixed at the load,
        /// lowered by every ceiling descent since and raised by every brake (<see cref="CeilingDescent.RestY"/> less its
        /// <see cref="CeilingDescent.TargetY"/>). What says which levels the frame holds now
        /// (<see cref="FramedLevelTop"/>), and so when a tall level's clamp lifts. The clamp itself is measured at
        /// the load's hang on purpose (<see cref="SolveElevationLimit"/>, #842). The structure's own stretch under
        /// the plate is not in it, and need not be: it is there at the load too, so the error from it does not grow
        /// with the steps.
        /// </summary>
        private Vector3 AimWorldOffset() =>
            _clusterWorldOffset - new Vector3(0f, _ceilingDescent.RestY - _ceilingDescent.TargetY, 0f);

        /// <summary>
        /// The topmost level a tall level's gun is solved and policed for — the working band's roof, the
        /// column's underside plus <see cref="TALL_AIM_HEADROOM_LEVELS"/>. Held to a band just above the
        /// underside, the player has to eat the column from the bottom, and the descent is what hands them the
        /// next of it. Read once per attempt, at the load (<see cref="HoldTallAimBand"/>, #842).
        /// <para>
        /// <b>Never above the top of what is framed</b> (<see cref="FramedLevelTop"/>), which is the rule the band
        /// narrows and must not break: a band reaching past the window is a clamp that lets the player shoot blind
        /// into the column that has not arrived. At the load it binds only on a level authored with its underside
        /// within <see cref="TALL_AIM_HEADROOM_LEVELS"/> of the window's top.
        /// </para>
        /// </summary>
        private int TallAimBandTop() => Math.Min(_map.GetLowestOccupiedLevel() + TALL_AIM_HEADROOM_LEVELS, FramedLevelTop());

        /// <summary>
        /// The highest level whose cells hang at or under the framed top (<see cref="FramedTopY"/>) where the glass
        /// is taking the column now (<see cref="AimWorldOffset"/>) — the band's ceiling at the load, and what
        /// <see cref="LiftTallAimOnceFramed"/> compares the cluster's top against.
        /// </summary>
        private int FramedLevelTop() =>
            //A hair of tolerance, because at no descent the window is exactly FRAMED_LEVELS - 1 levels up and the
            //division must not round that level out
            (int)MathF.Floor((FramedTopY() - AimWorldOffset().Y) * Constants.SQRT_TWO + 1e-3f);

        /// <summary>
        /// How far out the working band reaches: the cluster's own half-extent plus the ring a shot can land
        /// in around it. Beyond that a tall level's field is growth room no ball occupies, and asking whether
        /// the gun can look up at an empty corner is asking about a cell no shot will ever need.
        /// </summary>
        private float TallAimHorizontalLimit() => OccupiedHalfExtent() + 1f;

        /// <summary>
        /// How far the occupied cells reach horizontally from the gun's orbit centre — the cluster's own
        /// half-extent, as opposed to the field's. Walked over the map rather than taken off the footprint
        /// because a tall level is deliberately narrow inside a wider field, and it is the balls the gun has
        /// to reach. Runs on a level load and a resize, never per frame.
        /// </summary>
        private float OccupiedHalfExtent()
        {
            StaticBall[,,] balls = _map.GetStaticBallsArray();
            float furthest = 0f;

            for (byte level = 0; level < _map.Levels; level++)
                for (byte x = 0; x < _map.StageSizeX; x++)
                    for (byte z = 0; z < _map.StageSizeZ; z++)
                    {
                        if (balls[x, z, level] == null) continue;

                        //Through the cluster offset, exactly as AimReachability measures: BallsMap.Center
                        //centres on the TOP level's own bounding box, which for a narrow column inside a
                        //wide field leaves the lattice several units off the axis the gun orbits — three of
                        //them on Column. Measured without it, the column's half-extent came out at 7.1
                        //instead of 2.8, the elevation limit at 52 degrees instead of 40, and the horizontal
                        //bound below wide enough to filter almost nothing.
                        Vector3 cell = _map.GetRealCenteredPosition(new XZLevel(x, z, level)) + _clusterWorldOffset;
                        furthest = MathF.Max(furthest, MathF.Sqrt(cell.X * cell.X + cell.Z * cell.Z));
                    }

            return furthest;
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
            float glass = CeilingPlate.TopFaceY(_ceilingDescent.Y);
            float window = _clusterWorldOffset.Y + (FRAMED_LEVELS - 1) / Constants.SQRT_TWO;

            return MathF.Min(glass, window);
        }

        #endregion

        //The gun's recoil stroke used to live here as the one piece of the gun that was still this
        //executable's (only the Game animated one). It is Cannon's own since #115 — the carriage's later,
        //smaller response needed the same clock as the tube's slide, and two responses off one clock only
        //stay one clock if the gun owns it. What remains this file's is the CAMERA's share of the shot,
        //above (CannonRecoilBack's reasoning — squared stroke, drawing only — moved with it).
    }
}
