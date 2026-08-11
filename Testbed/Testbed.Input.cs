using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Testbed
{
    /// <summary>
    /// <b>The two modes and what drives the gun in them</b> — the free fly camera and game mode, the switch
    /// between them, and the cannon's own input while it is the thing being played.
    /// </summary>
    /// <remarks>
    /// Split out of <c>Testbed.cs</c> in #73. The split between the modes is the whole shape of it: in free mode
    /// the mouse and W/A/S/D belong to the fly camera and <see cref="UpdateCannon"/> returns after easing the
    /// pose, while in game mode the mouse aims the barrel throughout and A/D orbit the carriage with W/S walking
    /// it. Two orderings are load-bearing — the camera pose is read <i>after</i>
    /// <see cref="Prazsky.BS3D.GameObjects.Cannon.Update"/> has moved the gun this frame (#29), and the ADS
    /// pose is applied FOV → Position → Target because <c>BasicCamera3D</c>'s <c>Target</c> setter rebuilds the
    /// view last.
    /// </remarks>
    public partial class Testbed
    {

        private void SwitchGameMode(bool gameMode)
        {
            if (_gameMode == gameMode) return;
            if (_gameModeAnimStarted || _freeModeAnimStarted) return;

            _gameMode = gameMode;
            _info.ShowIcon = gameMode;

            if (_gameMode)
            {
                _cih.ResetMouseModes();       //drop any free-look pan/rotate toggle so it does not resume in game mode
                _mouseAim.Invalidate();       //the first captured frame skips its delta, so grabbing the cursor never jumps the aim
                _gameModeAnimStarted = true;
                _beforeAnimationPosition = _camera.Position;
                _beforeAnimationTarget = _camera.Target;
            }
            else
            {
                //Ease the aim back to its rest direction (~1s SmoothStep in Cannon.Update, not a snap) so the
                //gun is not left cocked at the last mouse aim - the aim persists within game mode, but a fresh
                //session starts neutral. Leaves the orbit position alone.
                _cannon.ResetAim();

                //Leaving game mode while precise aim is engaged: capture the leaned pose so the free-mode exit eases
                //it out to the overview pose (position, target and FOV), instead of snapping ~30 units in one frame.
                _freeExitFromAds = _preciseAim.Blend > 0f;
                if (_freeExitFromAds)
                {
                    _beforeAnimationPosition = _camera.Position;
                    _beforeAnimationTarget = _camera.Target;
                    _beforeAnimationFov = _camera.FieldOfView;
                }
                _preciseAim.Reset(); //the lean is dropped with no ease; the exit animation above carries the pose out
                _mouseAim.Invalidate();
                IsMouseVisible = true;
                _freeModeAnimStarted = true;
            }
        }

        private void UpdateCannon(GameTime gameTime)
        {
            //Free mode drives no cannon input — A/D belong to the fly camera's strafe and the aim stays parked —
            //so only the pose easing runs, and a barrel caught mid-traverse settles instead of freezing.
            //Returning BEFORE the snapshot below is the point: those three GetState calls are real OS queries
            //(an XInput poll for the pad), _cih already took this frame's set, and free mode — where the Testbed
            //spends most of its life — was paying both for nothing (#80).
            if (!_gameMode)
            {
                _cannon.Update(gameTime);
                return;
            }

            //One snapshot of each input device for the whole game-mode frame: every extra GetState call
            //re-queries the OS (a real XInput poll for the pad), and two reads in one frame can even
            //disagree about a key pressed between them. In game mode this is still a SECOND set after _cih's —
            //sharing that one means threading CameraInputHelper's snapshot out through a library API, which #80
            //records as declined: the Testbed is not the product, and the cost is one extra poll per device.
            KeyboardState keyboard = Keyboard.GetState();
            MouseState mouse = Mouse.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            //Orbiting the cannon around the field is on A/D and walking it towards the field and back on W/S —
            //in the free fly camera all four stay the camera's own, which is why the free-mode early-out above
            //exists. Walking closes on the cluster (a steeper shot up into its underside) or backs off for a
            //flatter one; the ends of the walk are rubber (Cannon.ADVANCE_EASE_ZONE), not stops. Neither
            //movement touches the aim: the mouse owns it (below) and holds it wherever the player leaves it.
            if (keyboard.IsKeyDown(mgKeys.A)) _cannon.Orbit(1f);
            if (keyboard.IsKeyDown(mgKeys.D)) _cannon.Orbit(-1f);

            if (keyboard.IsKeyDown(mgKeys.W)) _cannon.Advance(1f);
            if (keyboard.IsKeyDown(mgKeys.S)) _cannon.Advance(-1f);

            _cannon.Update(gameTime);

            //The camera must follow the cannon's pose from THIS frame (after Update above has moved it).
            //Reading the pose before the move made the camera lag one frame behind, so any frame-time
            //fluctuation (shooting, contact processing) showed up as the cannon jittering on screen (#29).
            if (!_gameModeAnimStarted)
            {
                //The mouse aims the cannon throughout game mode - in the overview as well as in precise aim (the
                //arrow keys are retired). The cursor is captured (hidden and re-centred) the whole time we are
                //actively playing, and the mouse delta drives Cannon.Aim before the pose is read so the camera does
                //not lag it (#29). Precise aim (RMB / left trigger) changes nothing about the aiming - it only leans
                //the camera in over the barrel and down the aim.
                //IsActive gates the capture: the gamepad trigger reads globally through XInput, and losing focus must
                //free the cursor rather than keep grabbing it (the else branch).
                if (IsActive && _map != null && _aimShootDriver == null) UpdateMouseAim(gameTime, mouse, pad);
                else { _mouseAim.Invalidate(); IsMouseVisible = true; }

                //Stepped every frame, held or not: an unheld frame is how the lean eases back out, which is what
                //makes losing focus a fade rather than a drop. Every gate on the held flag is this file's - IsActive
                //(the gamepad trigger reads globally through XInput, and an alt-tabbed window must not stay leaned
                //in), the free-mode exit animation, and a loaded field.
                bool adsHeld = IsActive && !_freeModeAnimStarted && _map != null && PreciseAim.ButtonHeld(mouse, pad);
                _preciseAim.Step(adsHeld, (float)gameTime.ElapsedGameTime.TotalSeconds);

                //The muzzle is read after _cannon.Update above, for the same reason the camera pose is (#29). The
                //cluster centre is this file's own derivation off the loaded map - PreciseAim deliberately does not
                //learn what a map is.
                AimPose aim = _preciseAim.BlendedPose(GetCanonOffsettedPos(), GetCannonOffsettedTarget(), GAME_FOV,
                    _cannon.MuzzlePosition(_cannonRig.PivotToFrontBall), _cannon.AimDirection, ClusterCentre());

                //The order FOV -> Position -> Target is required: the Target setter rebuilds the view last, with
                //world up (which is also where the ADS lens's view up comes from for free).
                _camera.FieldOfView = aim.FieldOfView;
                _camera.Position = aim.Position;
                _camera.Target = aim.Target;
            }
        }

        /// <summary>
        /// Drives the cannon's aim from the mouse throughout game mode (the overview as well as precise aim; the
        /// arrow keys are retired), and from the pad's right stick. The arithmetic and both dials are
        /// <see cref="MouseAim"/>'s since #76 — including why the delta is taken against the <b>live</b> viewport
        /// centre and divided by the frame time. What stays here is the order: the cursor is hidden, the delta
        /// applied, the cursor re-centred, and only then the pad added.
        /// </summary>
        private void UpdateMouseAim(GameTime gameTime, MouseState mouse, GamePadState pad)
        {
            int cx = GraphicsDevice.Viewport.Width / 2;
            int cy = GraphicsDevice.Viewport.Height / 2;

            IsMouseVisible = false;

            _mouseAim.ApplyCursor(_cannon, mouse, cx, cy, gameTime);
            _mouseAim.Recentre(cx, cy);

            MouseAim.ApplyPad(_cannon, pad, gameTime);
        }
    }
}
