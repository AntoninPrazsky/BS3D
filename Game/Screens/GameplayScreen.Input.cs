using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Physics;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using Prazsky.Core;


namespace BS3D.Screens
{
    /// <summary>
    /// <b>What the player does</b> — the keys and buttons, the aim off a captured cursor, and the shot.
    /// </summary>
    /// <remarks>
    /// One snapshot per device per frame, taken here and passed down (<c>BestPractices.md</c> #5): a second
    /// <c>GetState()</c> in the same frame is a different reading, and an edge tested against it is a press
    /// seen twice or not at all. <see cref="UpdateInput"/> <b>reports</b> whether it paused rather than merely
    /// returning, because the manager applies a push on the following frame and the rest of the frame would
    /// otherwise run on a game that is not paused yet (#79). Split out of <c>GameplayScreen.cs</c> in #72.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        #region Input

        /// <summary>
        /// How far the <b>right</b> trigger has to be pulled to count as firing. It was this number written
        /// twice, once for the press and once for the release, which is a hazard rather than a duplication: a
        /// press threshold above the release threshold would leave a dead band where the trigger is neither
        /// held nor released and the next shot never re-arms.
        /// <para>
        /// It equals <see cref="PreciseAim.TRIGGER_THRESHOLD"/> and is deliberately not that constant. That one
        /// is the <i>left</i> trigger's — how far in the lens leans — and the two are separate decisions about
        /// separate controls that happen to agree today; tying them together would mean re-tuning the aim to
        /// change the shot.
        /// </para>
        /// </summary>
        private const float FIRE_TRIGGER_THRESHOLD = 0.5f;

        /// <summary>
        /// This frame's keyboard and pad actions: pause, the window toggles, the shot, the traverse.
        /// </summary>
        /// <returns><c>true</c> when the session was <b>paused</b> on this frame, which is the caller's signal
        /// to run no more of the frame — see the reasoning at the call site. Every other early exit here returns
        /// <c>false</c>: they hand off to <c>UpdateAim</c>, which owes the frame its <c>PreviousPad</c>.</returns>
        private bool UpdateInput(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)
        {
            KeyboardState keyboard = Keyboard.GetState();

            if (edgeInputAllowed)
            {
                //Escape (or the gamepad's Back button) pauses rather than quitting outright: quitting is a
                //menu item now, and a game that closes the instant Escape is tapped is one that loses a game.
                if (Game.IsKeyEdge(keyboard, Keys.Escape) || (pad.IsButtonDown(Buttons.Back) && !Game.PreviousPad.IsButtonDown(Buttons.Back)))
                {
                    Game.PreviousKeyboard = keyboard;
                    Game.PreviousPad = pad;
                    Game.PauseGame();

                    //Nothing else this frame, here OR in the caller: the game is paused as of now, and firing
                    //or traversing on the way out would be an action the player asked of a game that has
                    //stopped. Both snapshots are written above rather than left to UpdateAim, precisely because
                    //this is the one exit that stops the frame before UpdateAim can run.
                    return true;
                }

                if (Game.IsKeyEdge(keyboard, Keys.F11)) Game.ToggleFullscreen();

                //F12 hides the FPS overlay, the same key that hides the Testbed's text
                if (Game.IsKeyEdge(keyboard, Keys.F12)) Game.ToggleFpsOverlay();

                //While the drop cinematic has the camera the gun does not answer, and Space skips the shot
                //instead of firing. Escape is deliberately NOT the skip: it already means pause, and taking
                //it would both give one key two meanings and leave the player unable to pause during a
                //cinematic — the skip belongs with the buttons that mean "yes, go on" (Space, the left mouse
                //button and the pad's A), which are the ones the hand is already on.
                if (_cinematic.Engaged)
                {
                    if (Game.IsKeyEdge(keyboard, Keys.Space)
                        || (pad.IsButtonDown(Buttons.A) && !Game.PreviousPad.IsButtonDown(Buttons.A)))
                        _cinematic.TrySkip();

                    Game.PreviousKeyboard = keyboard;
                    return false;
                }

                //Space fires; the gamepad fires off its right trigger, read with the aim (below)
                if (Game.IsKeyEdge(keyboard, Keys.Space)) Shoot();
            }
            else if (_cinematic.Engaged)
            {
                //Edge input is being held off for a frame after a refocus, but the gun still must not answer
                Game.PreviousKeyboard = keyboard;
                return false;
            }

            //The carriage traverses on A/D and walks on W/S: turning orbits the field, walking closes on it —
            //standing nearer steepens the shot up into the cluster's underside, standing further flattens it.
            //Both are holds at the same ±1 protocol, and the walk's ends are rubber (Cannon.ADVANCE_EASE_ZONE),
            //not stops.
            if (keyboard.IsKeyDown(Keys.A)) _cannon.Orbit(CANNON_ORBIT_RATE);
            else if (keyboard.IsKeyDown(Keys.D)) _cannon.Orbit(-CANNON_ORBIT_RATE);

            if (keyboard.IsKeyDown(Keys.W)) _cannon.Advance(CANNON_ADVANCE_RATE);
            else if (keyboard.IsKeyDown(Keys.S)) _cannon.Advance(-CANNON_ADVANCE_RATE);

            Game.PreviousKeyboard = keyboard;

            return false;
        }

        /// <summary>
        /// Aiming is the mouse, all the time: the cursor is hidden and recentred every frame off the live
        /// viewport, and the pixel delta is divided by the frame time, which cancels exactly against the
        /// frame time <see cref="Cannon.Aim"/> multiplies back in — so the aim moves a fixed amount per
        /// pixel at any frame rate. Firing is read from the same state, so the click and the aim cannot
        /// disagree about the frame they happened in.
        /// </summary>
        private void UpdateAim(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)
        {
            int centreX = GraphicsDevice.Viewport.Width / 2;
            int centreY = GraphicsDevice.Viewport.Height / 2;

            MouseState mouse = Mouse.GetState();

            //While the cinematic has the frame the barrel does not move and nothing fires — but the cursor is
            //still recentred below, so the aim is not handed back a delta measured from wherever the mouse
            //drifted to during the shot. The left button skips, matching Space and the pad's A.
            if (_cinematic.Engaged)
            {
                if (edgeInputAllowed && _mouseAim.Initialized
                    && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
                    _cinematic.TrySkip();

                //Forced rather than read: the lean is a hold, so a player still holding the right button when
                //the cinematic ends gets precise aim back, and one who let go during it does not
                _adsHeld = false;

                _mouseAim.Recentre(centreX, centreY);
                _previousMouse = mouse;

                Game.PreviousPad = pad;
                return;
            }

            _mouseAim.ApplyCursor(_cannon, mouse, centreX, centreY, gameTime);

            //The shot edge is gated on the same "a captured frame has been seen" flag the aim is: on the frame
            //the baseline is dropped there is no aim to fire along yet, so no phantom shot goes off either
            if (_mouseAim.Initialized && edgeInputAllowed
                && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
                Shoot();

            //Precise aim is a hold, not an edge, so it is read straight off this frame's state — no
            //edge-input gate: leaning the camera in is not an action that can go off by accident.
            _adsHeld = PreciseAim.ButtonHeld(mouse, pad);

            _mouseAim.Recentre(centreX, centreY);

            //Only its LeftButton is ever read (the shot's edge test above); the aim delta is measured against
            //the viewport centre, never against this, so the state captured at the top of the method serves
            _previousMouse = mouse;

            if (pad.IsConnected)
            {
                MouseAim.ApplyPad(_cannon, pad, gameTime);

                //Gated like the keyboard and the mouse: XInput reports a held trigger whether the window has
                //focus or not, so without this the click that refocuses the game would arrive alongside a
                //trigger that was never released and fire a shot the player did not ask for
                if (edgeInputAllowed && pad.Triggers.Right > FIRE_TRIGGER_THRESHOLD && _padTriggerReleased)
                {
                    Shoot();
                    _padTriggerReleased = false;
                }
                else if (pad.Triggers.Right <= FIRE_TRIGGER_THRESHOLD) _padTriggerReleased = true;
            }

            //Stored here rather than in UpdateInput, which runs first: the Back button's press edge is
            //measured against the previous *frame*, so the snapshot has to be the last thing the frame does
            //with the pad. Both methods are handed the one poll taken at the top of Update.
            Game.PreviousPad = pad;
        }

        /// <summary>
        /// Fires the ball the player can see sitting at the muzzle: a real body thrown down the bore, a launch
        /// smear along the shot, the queue shifted forward — and the camera and the barrel both kicked, which is
        /// the whole feel of the thing. Where it goes from there is the simulation's business: it may hit the
        /// cluster and attach (see <see cref="BallContactEventHandler"/>), bounce off the glass, run down the
        /// drain, or fly off the island and be culled.
        /// </summary>
        private void Shoot()
        {
            //The budget is spent, so no more shots leave the barrel — but the level is not lost here. The last
            //ball fired is still in flight and may be the one that clears the field, and a loss called now would
            //steal that win. Whether the spent budget actually loses is decided once every shot has resolved,
            //in CheckLevelLost, against the state of the field then.
            if (_score.OutOfShots) return;

            //No recoil in either of these: the shot leaves along the TRUE aim on the frame it is fired, before
            //the barrel has moved. The stroke below is drawing only — see CannonRecoilBack.
            Vector3 direction = _cannon.AimDirection;
            Vector3 muzzle = _cannon.MuzzlePosition(Game.CannonRig.PivotToFrontBall);
            BallType type = _magazine.Peek();

            PhysicsBall ball = new()
            {
                //Stamped from the world's shot template, added to the simulation and registered as a contact
                //listener in one call, in that order — a listener is keyed on a collidable reference, so the
                //body has to exist first. It is also the ONLY place anything is ever registered, which is what
                //makes "every listener is a shot still in the air" true (see AnyShotUndecided).
                BallReference = _world.AddShotBall(muzzle.ToNumerics(), direction.ToNumerics() * SHOOT_SPEED,
                    _eventHandler),
                Type = type //the colour the player saw loaded at the muzzle, so aiming for it means something
            };

            _shotBalls.Add(ball);

            //The ball is spent the instant it leaves the barrel. What it *did* takes a physics step or more to
            //resolve, so the budget and the score are driven by different events on purpose — see ScoreKeeper.
            _score.Shot();

            //The same shot drives the ceiling's descent: every ceilingStep-th shot steps the glass down. Checked
            //after Shot() so ShotsFired includes the one just fired, and the scorer owns the cadence exactly as it
            //owns the budget — the two pressures are coupled by design, so they are read in one place.
            //
            //QUEUED rather than started, because the shot has not landed yet and the descent must not collide
            //with what it is about to do — see ReleaseCeilingStep.
            if (_score.StepCeilingThisShot())
            {
                _ceilingStepsPending++;
                _ceilingStepHold = CEILING_STEP_HOLD;
            }

            //The shot's launch smear. Only the ball's authored tint goes over: decoding it to linear, lifting
            //its peak off the floor so even the black ball leaves a mark and boosting it to a glowing radiance
            //are one rule about how a smear looks, and it lives with the smear.
            _smears.Add(muzzle, direction, BasicEffectParamsProvider.GetDiffuseTintByType(type));

            //The fired ball's slot empties, the queue shifts up, a fresh colour loads at the back and the glide
            //is armed — and the transmute state rides forward with it through the hooks wired in the
            //constructor, so no slot is left dissolving out of the ball behind it.
            _magazine.Advance();

            //Set, not accumulated: a barrel's recoil stroke restarts from the top with every round, it does
            //not stack up over a burst the way the camera's trauma does.
            _cannonRecoil = 1f;

            //Fired, therefore felt. Nothing else in the frame moves the camera, so every wobble the player
            //sees is unambiguously their own shot.
            Camera.Shake.Kick(RECOIL_KICK);

            //Heard as well as felt, and heard FROM THE MUZZLE — the same point the round is spawned at above
            //and the same one the smear is drawn from, so the crack, the ball and the streak cannot disagree
            //about where the shot left. The muzzle sits a dozen-odd units dead ahead of the lens, so what this
            //buys is the barrel's swing: the crack drifts a little off centre as the gun is turned and no
            //further. Its level stays flat with no distance term, so walking the gun in with W/S cannot make
            //the game's most frequent sound swell. Nudged by a small random pitch so a burst never sounds flat.
            Game.Audio.PlayShoot(muzzle);
        }

        #endregion
    }
}
