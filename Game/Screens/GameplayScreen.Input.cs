using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Physics;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using Prazsky.Core;
using System;


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
        /// How far the pad's left stick has to be pushed before it turns or walks the gun (#189), and past which
        /// its right stick counts as the player's hand being on the pad. A hold and not a rate: past it the stick
        /// is a key held down, so a stick and a key move the carriage identically and neither player has a faster
        /// gun. Lower than the menu's <c>NAV_STICK_DEADZONE</c> (0.55), because a menu step is an edge to
        /// debounce and this is a hold to keep up; well above zero, because a stick at rest must not creep the
        /// carriage round the field.
        /// </summary>
        private const float PAD_WALK_DEADZONE = 0.35f;

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

                //F10 hides the FPS overlay. It was F12 — the key that also hides the Testbed's text — until
                //#191 gave F12 to the screenshot, which is where a hand reaches for one; F11 could not be
                //given up for it, being fullscreen in all three executables.
                if (Game.IsKeyEdge(keyboard, Keys.F10)) Game.ToggleFpsOverlay();

                //And F12 saves the frame the player is looking at, mid-shot and mid-cinematic included: it is
                //a request served at the end of the NEXT Draw, so what lands in the file is the frame this
                //press belongs to rather than the one already presented (see BS3DGame.Screenshot).
                if (Game.IsKeyEdge(keyboard, Keys.F12)) Game.RequestScreenshot();

                //While a camera takeover (the drop cinematic or the chapter intro) has the camera the gun does
                //not answer, and Space skips it instead of firing. Escape is deliberately NOT the skip: it
                //already means pause, and taking it would both give one key two meanings and leave the player
                //unable to pause during one — the skip belongs with the buttons that mean "yes, go on" (Space,
                //the left mouse button and the pad's A), which are the ones the hand is already on.
                if (CameraTakeoverEngaged)
                {
                    if (Game.IsKeyEdge(keyboard, Keys.Space)
                        || (pad.IsButtonDown(Buttons.A) && !Game.PreviousPad.IsButtonDown(Buttons.A)))
                        SkipCameraTakeover();

                    Game.PreviousKeyboard = keyboard;
                    return false;
                }

                //Space fires; the gamepad fires off its right trigger, read with the aim (below)
                if (Game.IsKeyEdge(keyboard, Keys.Space)) Shoot();

                //E (the pad's X) activates the one power-up this issue proves (#392) — a fixed, un-aimed
                //action, unlike a shot, so it needs no direction and no muzzle. CanActivate is asked rather
                //than assumed: a charge might be spent, the level might already be decided, or a camera
                //takeover might have started the very frame this edge fired.
                if (Game.IsKeyEdge(keyboard, Keys.E)
                    || (pad.IsButtonDown(Buttons.X) && !Game.PreviousPad.IsButtonDown(Buttons.X)))
                    if (CanActivate(PowerupKind.Swap)) Activate(PowerupKind.Swap);
            }
            else if (CameraTakeoverEngaged)
            {
                //Edge input is being held off for a frame after a refocus, but the gun still must not answer
                Game.PreviousKeyboard = keyboard;
                return false;
            }

            //The carriage traverses on A/D and walks on W/S — and since #189 on the pad's LEFT STICK, sideways
            //and up/down, which was the one thing a pad could not do: the right stick aims, the triggers fire and
            //lean, and nothing turned or walked the gun, so the tutorial's pad cards would have promised a
            //binding that did not exist. Turning orbits the field, walking closes on it — standing nearer
            //steepens the shot up into the cluster's underside, standing further flattens it. All of them are
            //holds at the same ±1 protocol (a stick past its deadzone is a key held down, not a rate), and the
            //walk's ends are rubber (Cannon.ADVANCE_EASE_ZONE), not stops.
            float stickX = pad.IsConnected ? pad.ThumbSticks.Left.X : 0f;
            float stickY = pad.IsConnected ? pad.ThumbSticks.Left.Y : 0f;

            bool traverseLeft = keyboard.IsKeyDown(Keys.A) || stickX < -PAD_WALK_DEADZONE;
            bool traverseRight = keyboard.IsKeyDown(Keys.D) || stickX > PAD_WALK_DEADZONE;
            bool walkIn = keyboard.IsKeyDown(Keys.W) || stickY > PAD_WALK_DEADZONE;
            bool walkOut = keyboard.IsKeyDown(Keys.S) || stickY < -PAD_WALK_DEADZONE;

            if (traverseLeft) _cannon.Orbit(CANNON_ORBIT_RATE);
            else if (traverseRight) _cannon.Orbit(-CANNON_ORBIT_RATE);

            if (walkIn) _cannon.Advance(CANNON_ADVANCE_RATE);
            else if (walkOut) _cannon.Advance(-CANNON_ADVANCE_RATE);

            //What the tutorial's traverse and walk lessons wait for (#189), and which device's card it draws: a
            //key here is the keyboard's hand, a stick past its deadzone the pad's
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _tutorial.NoteHold(Tutorial.Lesson.Traverse, traverseLeft || traverseRight, elapsed);
            _tutorial.NoteHold(Tutorial.Lesson.Walk, walkIn || walkOut, elapsed);

            //And what the COMBINATION lesson waits for (#460): the carriage moving at all, remembered for the
            //frame so it can be read together with the close-up's hold, which is taken further down this same
            //update. Both halves have to be true on ONE frame — the gesture the lesson teaches is holding them
            //at once, and a player alternating them would otherwise complete a card they had not performed.
            _carriageMoving = traverseLeft || traverseRight || walkIn || walkOut;

            if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.W)
                || keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Space))
                _tutorial.NoteDevice(Tutorial.Device.KeyboardMouse);
            else if (MathF.Abs(stickX) > PAD_WALK_DEADZONE || MathF.Abs(stickY) > PAD_WALK_DEADZONE)
                _tutorial.NoteDevice(Tutorial.Device.Gamepad);

            Game.PreviousKeyboard = keyboard;

            return false;
        }

        /// <summary>
        /// Aiming is the mouse, all the time: the cursor is hidden and recentred every frame off the live
        /// viewport, and the pixel delta is divided by the frame time, which cancels exactly against the
        /// frame time <see cref="Cannon.Aim"/> multiplies back in — so the aim moves a fixed amount per
        /// pixel at any frame rate. Firing is read from the same state, so the click and the aim cannot
        /// disagree about the frame they happened in.
        /// <para>
        /// <b>All of that waits on the capture, which is a state the player enters (#99).</b> A frame that
        /// warps the pointer back to the middle is a frame in which the window cannot be dragged by its title
        /// bar, resized, or left for another application — the pointer never reaches any of them. Arriving at
        /// play with the pointer already in the picture takes it at once, the menu press that put the screen
        /// on top being the opt-in (#154) — a pad or keyboard arrival with the mouse parked elsewhere stays
        /// free; after a focus loss <see cref="_cursorCaptured"/> is false, and nothing here touches the
        /// cursor until a left click lands inside the viewport. Everything the pad does is independent of it
        /// and keeps working with the pointer free.
        /// </para>
        /// </summary>
        private void UpdateAim(GameTime gameTime, bool edgeInputAllowed, GamePadState pad)
        {
            int centreX = GraphicsDevice.Viewport.Width / 2;
            int centreY = GraphicsDevice.Viewport.Height / 2;

            MouseState mouse = Mouse.GetState();

            //Which device the tutorial draws its card for (#189): the mouse moved off the centre it was put back
            //to last frame, or a button on it; else the pad's sticks, triggers or A. The keys say so in
            //UpdateInput. Read before the recentre below, which is what puts the pointer back on the centre.
            if ((_cursorCaptured && (mouse.X != centreX || mouse.Y != centreY))
                || mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed)
                _tutorial.NoteDevice(Tutorial.Device.KeyboardMouse);
            else if (pad.IsConnected
                && (pad.ThumbSticks.Right.LengthSquared() > PAD_WALK_DEADZONE * PAD_WALK_DEADZONE
                    || pad.Triggers.Left > PreciseAim.TRIGGER_THRESHOLD || pad.Triggers.Right > FIRE_TRIGGER_THRESHOLD
                    || pad.IsButtonDown(Buttons.A)))
                _tutorial.NoteDevice(Tutorial.Device.Gamepad);

            //The click that takes the cursor. Gated on edgeInputAllowed like every other edge, which is what
            //keeps the click that merely brings the window forward from also capturing — that one lands on a
            //frame the window was not active for, and a fresh press is then needed. Inside the VIEWPORT, since
            //a MouseState reads negative and past-the-edge coordinates perfectly happily when the pointer is
            //over the title bar or another window, and those clicks are precisely the ones to leave alone.
            if (!_cursorCaptured && edgeInputAllowed
                && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released
                && mouse.X >= 0 && mouse.X < GraphicsDevice.Viewport.Width
                && mouse.Y >= 0 && mouse.Y < GraphicsDevice.Viewport.Height)
            {
                _cursorCaptured = true;

                //The capturing click must not also fire a shot, and it does not — Invalidate drops the aim's
                //baseline, and the shot edge sits behind that same flag, so the swallow falls out of what is
                //already there rather than needing a rule of its own. Recentre at the foot of this method puts
                //the flag back up, so the frame after this one aims normally.
                _mouseAim.Invalidate();
            }

            //While a camera takeover has the frame the barrel does not move and nothing fires — but the
            //cursor is still recentred below, so the aim is not handed back a delta measured from wherever
            //the mouse drifted to during it. The left button skips, matching Space and the pad's A.
            if (CameraTakeoverEngaged)
            {
                if (edgeInputAllowed && _mouseAim.Initialized
                    && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
                    SkipCameraTakeover();

                //Forced rather than read: the lean is a hold, so a player still holding the right button when
                //the takeover ends gets precise aim back, and one who let go during it does not
                _adsHeld = false;

                if (_cursorCaptured) _mouseAim.Recentre(centreX, centreY);
                _previousMouse = mouse;

                Game.PreviousPad = pad;
                return;
            }

            //The player's dial times the lens's lean (#384). The lean's own term is LAST FRAME'S — _adsHeld is
            //set below and _preciseAim.Step runs later still, in UpdateCamera — and that is deliberate rather
            //than overlooked: stepping the blend up here to make it this frame's would move where the camera
            //reads the lean, which is an order this file and PreciseAim both state reasons for. A frame of lag
            //on a factor whose own ease is BLEND_TAU (0.08 s, ~90 % in 0.18 s) is far below what a hand can
            //feel; a camera reading a lean the gun has not been posed for is not.
            //And the player's own dial on the lean (#497), riding the same blend the lens's ratio rides, so it
            //is exactly 1 in the overview and the aim row's rung once the lean is in — never a step at the edge
            if (_cursorCaptured)
                _mouseAim.ApplyCursor(_cannon, mouse, centreX, centreY, gameTime,
                    Game.MouseSensitivity * MathHelper.Lerp(1f, Game.AimSensitivity, _preciseAim.Blend)
                    * _preciseAim.CursorRateScale(GAME_FOV));

            //The shot edge is gated on the same "a captured frame has been seen" flag the aim is: on the frame
            //the baseline is dropped there is no aim to fire along yet, so no phantom shot goes off either
            if (_cursorCaptured && _mouseAim.Initialized && edgeInputAllowed
                && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
                Shoot();

            //Precise aim is a hold, not an edge, so it is read straight off this frame's state — no
            //edge-input gate: leaning the camera in is not an action that can go off by accident. With the
            //cursor still free the right button is the desktop's, so only the pad's trigger can lean in: a
            //default MouseState reads every button released, which is exactly "ask the pad alone".
            _adsHeld = _cursorCaptured ? PreciseAim.ButtonHeld(mouse, pad) : PreciseAim.ButtonHeld(default, pad);

            //The lean lesson waits on the hold (#189)
            _tutorial.NoteHold(Tutorial.Lesson.LeanIn, _adsHeld, (float)gameTime.ElapsedGameTime.TotalSeconds);

            //And the combination (#460), read here rather than beside the carriage's own keys because this is
            //where the close-up's half of it is known — so the AND is taken on one frame's two facts and not
            //across two frames.
            _tutorial.NoteHold(Tutorial.Lesson.Combine, _adsHeld && _carriageMoving,
                (float)gameTime.ElapsedGameTime.TotalSeconds);

            if (_cursorCaptured) _mouseAim.Recentre(centreX, centreY);

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

            //Testing only (#402): a scripted run's hands — the barrel swung, precise aim held, a shot fired — after the
            //mouse and the pad, so the script has the last word on the pose. Nothing at all without a script.
            if (ScriptedPlay.Current is ScriptedPlay script)
            {
                if (script.TrySweep(WallClock, _cannon.Elevation, out float elevation, out float traverse))
                    _cannon.AimTo(elevation, traverse);

                _adsHeld |= script.Rmb(WallClock);

                if (script.TryTakeFire(WallClock)) Shoot();
            }

            //And the aim lesson reads the pose once the mouse and the pad have both had their say (#189)
            _tutorial.NoteAim(_cannon.Traverse, _cannon.Elevation);

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
            //The level is already decided — cleared and waiting out LEVEL_CLEARED_BEAT, or lost. The gun stops
            //answering the instant the outcome is settled rather than when the result screen finally covers
            //this one, which is a whole beat later and longer still under a drop cinematic: a shot fired into
            //that beat is spent against a level the player has already won, and it goes on to land and resolve
            //contacts on a field nobody is playing any more (#177). Same shape as the pause frame's #79, and
            //the same reason it has to be checked here — there is no covering screen to lean on this early.
            if (LevelDecided) return;

            //The budget is spent, so no more shots leave the barrel — but the level is not lost here. The last
            //ball fired is still in flight and may be the one that clears the field, and a loss called now would
            //steal that win. Whether the spent budget actually loses is decided once every shot has resolved,
            //in CheckLevelLost, against the state of the field then.
            if (_run.Score.OutOfShots) return;

            //Refused while the aim is pressed into, or stretched past, the elevation clamp (#431). The rubber lets
            //the barrel run up to ~6° past a tall level's limit, and a shot fired from the top of that stretch went
            //straight into the band the limit closes. Said out loud rather than silently eaten: the marks are already
            //blinking red, and a click that does nothing and says nothing reads as a dropped input.
            if (_cannon.ElevationRefusesShot)
            {
                Game.Audio.PlayShotRefused();
                return;
            }

            //No recoil in either of these: the shot leaves along the TRUE aim on the frame it is fired, before
            //the barrel has moved. The stroke below is drawing only — see CannonRecoilBack.
            Vector3 direction = _cannon.AimDirection;
            Vector3 muzzle = _cannon.MuzzlePosition(Game.CannonRig.PivotToFrontBall);
            //A wildcard fires as the colour it is SHOWING (#330), not as the one dealt into the slot underneath
            //it: that one is never seen and never used, and the ball has to leave the barrel wearing what the
            //player was looking at — the smear below is drawn from it, and so is the ball itself all the way to
            //the landing, where a wildcard that completes nothing keeps exactly this colour.
            BallKind kind = _magazine.Slot(0).Kind;
            BallType type = LoadedColour(0);

            PhysicsBall ball = new()
            {
                //Stamped from the world's shot template, added to the simulation and registered as a contact
                //listener in one call, in that order — a listener is keyed on a collidable reference, so the
                //body has to exist first. It is also the ONLY place anything is ever registered, which is what
                //makes "every listener is a shot still in the air" true (see AnyShotUndecided).
                BallReference = _world.AddShotBall(muzzle.ToNumerics(), direction.ToNumerics() * SHOOT_SPEED,
                    _eventHandler),
                Type = type, //the colour the player saw loaded at the muzzle, so aiming for it means something
                Kind = kind  //and what it is, which for everything but a wildcard is Normal (#330)
            };

            _shotBalls.Add(ball);

            //The ball is spent the instant it leaves the barrel. What it *did* takes a physics step or more to
            //resolve, so the budget and the score are driven by different events on purpose — see ScoreKeeper.
            _run.Score.Shot();

            //The tutorial's fire lesson is the shot leaving, not the shot landing (#189)
            _tutorial.Report(Tutorial.Lesson.Fire);

            //The same shot drives the ceiling's descent: every ceilingStep-th shot steps the glass down. Checked
            //after Shot() so ShotsFired includes the one just fired, and the scorer owns the cadence exactly as it
            //owns the budget — the two pressures are coupled by design, so they are read in one place.
            //
            //QUEUED rather than started, because the shot has not landed yet and the descent must not collide
            //with what it is about to do — see CeilingDescent.Update.
            if (_run.Score.StepCeilingThisShot()) _ceilingDescent.QueuePressureStep();

            //The shot's launch smear. Only the ball's authored tint goes over: decoding it to linear, lifting
            //its peak off the floor so even the black ball leaves a mark and boosting it to a glowing radiance
            //are one rule about how a smear looks, and it lives with the smear.
            _smears.Add(muzzle, direction, BasicEffectParamsProvider.GetDiffuseTintByType(type));

            //The fired ball's slot empties, the queue shifts up, a fresh colour loads at the back and the glide
            //is armed — and each slot's kind and transmute state ride forward with its colour as one value
            //(Magazine's MagazineSlot, #582), so no slot is left dissolving out of the ball behind it.
            _magazine.Advance();

            //The gun's own answer — the tube thrown back in its cradle, the carriage lurching a beat behind
            //it (#115). The stroke and both responses are the shared Cannon's; this is only the trigger.
            _cannon.KickRecoil();

            //Fired, therefore felt. Nothing else in the frame moves the camera, so every wobble the player
            //sees is unambiguously their own shot.
            Camera.Shake.Kick(RECOIL_KICK);

            //And felt in the hands too (#378), the same moment.
            Game.Rumble.Kick(SHOT_RUMBLE_LEFT, SHOT_RUMBLE_RIGHT, SHOT_RUMBLE_SECONDS);

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
