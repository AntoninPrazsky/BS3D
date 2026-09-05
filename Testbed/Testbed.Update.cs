using Microsoft.Xna.Framework;
using Prazsky.Core.Tools;
using System;

namespace Testbed
{
    /// <summary>
    /// <b>The frame's clock</b> — one simulation step, the ageing of everything that answers a shot, the input
    /// poll and the game-mode transition animation, in the order they have to happen in.
    /// </summary>
    /// <remarks>
    /// Split out of <c>Testbed.cs</c> in #73. Two orderings in here are load-bearing and neither is obvious from
    /// the code: <c>MouseMovementDenominator</c> is assigned <i>before</i> <c>CameraMovement</c> reads it (#80),
    /// and the wall-clock steps (<see cref="Prazsky.BS3D.GameObjects.Magazine.Step"/>, the recoil, the city's
    /// windows, the clouds) sit <i>outside</i> the <c>_simulate</c> gate because they are what the gun and the
    /// world are, not something the physics is doing. The three test harnesses are ticked from one line each and
    /// live in <c>Diagnostics/</c>.
    /// </remarks>
    public partial class Testbed
    {

        protected override void Update(GameTime gameTime)
        {
            float timeStep = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 60f);
            if (timeStep == 0) timeStep = 1 / 60f;

            //Wall-clock time, not simulation time: the balls keep their pulse when the simulation is
            //paused or slowed (F5, F9), because it is what they are, not something they are doing. It reaches
            //the three ball renderers as BallRenderSet.Draw's argument, which is why it is not pushed here.
            _pulseSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

            //Ease the magazine's post-shot slide towards its resting slots. Wall-clock too, so it glides even while
            //the simulation is paused (F5, F9): the balls sliding down a tube is the gun answering the shot, not
            //something the physics is doing.
            _magazine.Step((float)gameTime.ElapsedGameTime.TotalSeconds);

            //And the recoil slides home on the same clock, for the same reason — the stroke is the shared
            //Cannon's since #115, this is only its tick
            _cannon.StepRecoil((float)gameTime.ElapsedGameTime.TotalSeconds);

            //The city runs off the same wall clock, and for the same reason: its windows are lit by people
            //who do not care whether the simulation is running
            _cityRenderer.CityWindowTime = _pulseSeconds;

            UpdateOvercast((float)gameTime.ElapsedGameTime.TotalSeconds);

            if (_simulate)
            {
                //ONE step per rendered frame, of whatever the frame took. That is this executable's own stepping
                //policy and deliberately not the Game's — the Game accumulates the frame time and spends it in
                //whole fixed steps of 1/120 s, because a step that varies with the display runs the simulation in
                //slow motion below 60 FPS and Bepu's guidance is to keep it constant ("Physics in the game" in
                //docs/game-session.md). PhysicsWorld.Step takes one step of exactly the length it is handed and
                //nothing else, so the divergence stays visible in each caller's own loop; F9's slow motion scales
                //the dt right here, where the policy lives. What the component owns is the order INSIDE a step —
                //Timestep, then flush, then the contact work — which is mandatory and per step, not per frame: a
                //handler may only record what the worker threads saw, the flush is what applies those per-worker
                //adds, and a contact queued during a step describes a world the next step has already left behind.
                _world.Step(_slowSimulation ? timeStep * Constants.HUNDREDTH : timeStep, _processContacts);

                #region Fallen balls cleanup

                int removedBalls = RemoveFallenBalls(_shotBalls) + RemoveFallenBalls(_fallingBalls);
                if (removedBalls > 0) InvalidateBallCounts();

                #endregion

                #region Shot-trail launch smear

                //Age each muzzle smear and drop it once the launch burst has faded. Inside the simulation
                //gate on purpose: a paused Testbed (P) holds the smears where they are, along with the shot
                //that left them - the Game, whose smears age every frame it updates, does it differently.
                _smears.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

                #endregion

                #region Test harnesses (Diagnostics/, #73)

                //All three inside the simulation gate, which is where they always were: F5 holds the ball rain
                //and the aim sweep along with the physics they exist to exercise. Each is null unless its switch
                //was given (BuildTestHarnesses), so there is no flag to read here — and each was an inline block
                //of a dozen lines with its own cadence, index and done-flag fields until #73.
                if (_map != null)
                {
                    _autoShootDriver?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

                    //The sweep's own gate is the caller's state, so it goes in as an argument: a shot fired in
                    //the overview leaves the camera rather than the gun, so it must wait out the entry animation.
                    _aimShootDriver?.Update((float)gameTime.ElapsedGameTime.TotalSeconds,
                        _gameMode && !_gameModeAnimStarted && !_freeModeAnimStarted);
                }

                //Not gated on a map: the whole point is to install one on top of whatever is running
                _switchMapDriver?.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

                #endregion
            }

            //Once a frame, after everything that can have moved the population and before the frame is drawn.
            //Costs nothing on the frames it is not dirty, and nothing at all while the overlay is hidden.
            RefreshBallCounts();

            //Before CameraMovement below, which is what reads it: assigned after, the fly camera turned with
            //the PREVIOUS frame's denominator — one frame stale after every frame-time change, and one whole
            //frame wrong after a resize (#80).
            _cih.MouseMovementDenominator = timeStep / Constants.THOUSANDTH;

            if (IsActive)
            {
                _cih.RegisterCurrentInputState();

                //Skip edge-driven input the frame focus returns: while the window was inactive the input state was
                //not registered, so the click (or key) that refocuses would otherwise read as a fresh press against
                //a stale "released". RegisterPreviousInputState below re-syncs it, so edges resume next frame.
                if (_wasActive)
                {
                    foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

                    //In game mode the left mouse button fires, completing the shooter idiom (hold RMB to aim, click
                    //to shoot); Space still fires too. The free-cam mouse look is gated off in game mode (last
                    //argument), so the right button means "precise aim" there instead of toggling rotate/pan.
                    if (_gameMode && _cih.PressedOnceMouse(leftButton: true, middleButton: false, rightButton: false)) ShootBall();
                }

                _cih.Update(gameTime);
                _cih.CameraMovement(gameTime, !_gameMode, !_gameMode);
                _cih.RegisterPreviousInputState();
                _wasActive = true;
            }
            else { IsMouseVisible = true; _wasActive = false; }

            UpdateCannon(gameTime);

            #region Game mode animation

            if (_gameModeAnimStarted && _gameMode)
            {
                _camera.Position = Vector3.SmoothStep(_beforeAnimationPosition, GetCanonOffsettedPos(), _gameModeAnimStep);
                _camera.Target = Vector3.SmoothStep(_beforeAnimationTarget, GetCannonOffsettedTarget(), _gameModeAnimStep * 2f);
                _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(_freeFov, GAME_FOV, _gameModeAnimStep);

                _gameModeAnimStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_gameModeAnimStep > Constants.ONE)
                {
                    _gameModeAnimStep = 0;
                    _gameModeAnimStarted = false;
                }
            }

            if (_freeModeAnimStarted && !_gameMode)
            {
                if (_freeExitFromAds)
                {
                    //Leaving straight from precise aim: ease the whole leaned pose out to the overview pose over the
                    //same animation that widens the FOV, so the camera does not teleport the ~30 units between them.
                    _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(_beforeAnimationFov, _freeFov, _gameModeAnimStep);
                    _camera.Position = Vector3.SmoothStep(_beforeAnimationPosition, GetCanonOffsettedPos(), _gameModeAnimStep);
                    _camera.Target = Vector3.SmoothStep(_beforeAnimationTarget, GetCannonOffsettedTarget(), _gameModeAnimStep);
                }
                else
                {
                    //Plain overview -> free exit: the camera is already at the overview pose, so only the FOV widens.
                    _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(GAME_FOV, _freeFov, _gameModeAnimStep);
                }

                _gameModeAnimStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_gameModeAnimStep > 1f)
                {
                    _gameModeAnimStep = 0;
                    _freeModeAnimStarted = false;
                    _freeExitFromAds = false;
                }
            }

            #endregion

            base.Update(gameTime);
        }
    }
}
