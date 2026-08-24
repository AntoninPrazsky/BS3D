using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core.Render;
using System;
using System.Diagnostics;
using System.Threading;

namespace Testbed
{
    /// <summary>
    /// <b>The frame on the screen</b> — the scene into the HDR target, the resolve, the overlay over it — and
    /// the back buffer it lands in.
    /// </summary>
    /// <remarks>
    /// Split out of <c>Testbed.cs</c> in #73. The draw order here is the file's own decision and most of it is
    /// stated at the call sites: sky, backdrop, forest, island, gun, balls, smears, then the three glass
    /// surfaces nearest-last, then the snow. Two rules that are easy to break by moving a line: the
    /// <see cref="RasterizerState"/> is <b>stated</b> rather than inherited (the tonemap leaves
    /// <c>CullNone</c> and the overlay's <c>SpriteBatch</c> leaves its own, so what the scene culled used to
    /// depend on which ran last), and <see cref="BallRenderSet.BeginFrame"/> may be opened exactly once a frame
    /// — it advances per-ball state, so a second collection would double-step it while still looking correct.
    /// </remarks>
    public partial class Testbed
    {

        protected override void Draw(GameTime gameTime)
        {
            //The scene goes through the HDR target; the crosshair and the text overlay are drawn after the
            //resolve, at native resolution and in display space, so they stay exactly as authored instead
            //of being softened by the downsample and bent by the tonemap curve
            GraphicsDevice.SetRenderTarget(_pipeline.SceneTarget);

            //Clear to the current dome's HORIZON colour (linear), not a fixed blue. The dome is a hemisphere
            //model translated to the camera and drawn without depth, so it covers everything above the
            //horizon; below it the terrain covers what it reaches. But at a wide aspect (21:9) the bottom
            //corners look below the horizon past the terrain's finite edge, and there a fixed clear colour
            //showed through as a blue band. Clearing to the horizon colour makes any such gap blend seamlessly
            //with the hazed skyline the terrain and dome both fade to there, so it is never seen as a seam.
            //The sky-replacing scenes (space, the dream) have no dome and no horizon, so they clear to black
            //instead: their pass covers every pixel of the frame, and black is what would show if it ever did not.
            GraphicsDevice.Clear(SceneRenderer.ReplacesSky(_scene) ? Color.Black : new Color(_rig.HorizonLinear));

            //The clouds run off the same wall clock the balls pulse to, so the weather keeps moving while
            //the simulation is paused or slowed. Handed to both shaders from the one field, which is what
            //keeps the cloud you look at and the shadow it throws the same cloud.
            //
            //Space is the one scene with no weather at all: the dome is not drawn (Space.fx covers the frame),
            //and the cloud coverage is zeroed on the instanced effect so the balls, island and cannon are not
            //crossed by the shadows of a deck nobody can see - InstancedModel.fx calls CloudSunlight
            //unconditionally, and a gain left standing from the scene before would go on shadowing this one.
            _clouds.Time = _pulseSeconds;

            if (SceneRenderer.ReplacesSky(_scene)) _clouds.SuppressOn(_instancingEffect);
            else
            {
                _clouds.ApplyTo(_skyEffect);
                _clouds.ApplyTo(_instancingEffect);

                _skyCameraPositionParam.SetValue(_camera.Position);

                _sky.Draw(_camera);
            }

            //The sea's submerge fade for missed balls — a no-op off the sea scene (see SceneRenderer.ApplySeaSubmerge).
            //It takes how far the LENS is under the water: since #159 the fade is released by exactly what the
            //murk at the resolve below takes over, and both read the one answer.
            _sceneRenderer.ApplySeaSubmerge(_instancingEffect, _scene,
                _sceneRenderer.LensSubmergedAmount(_scene, _camera.Position));

            //The kill plane's own fade for a ball about to be culled (#192) — scene-independent and pushed
            //every frame, unlike the sea's own: see SceneRenderer.ApplyKillPlaneFade.
            _sceneRenderer.ApplyKillPlaneFade(_instancingEffect, KILL_PLANE_Y);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Stated rather than inherited. The last thing to touch the rasterizer in a frame is the
            //SpriteBatch drawing the overlay, which leaves its own state behind, and the tonemap pass
            //before it leaves CullNone - so what the scene culled depended on which of them ran last.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            if (_draw)
            {
                //The environment — city, sea or terrain — is the backdrop and the thing seen past the island's
                //edge both. Either way the only physics floor is the drain's own mesh (FunnelPhysics.Build);
                //the round stone island is the platform, and stays in every scene.
                SceneFrame sceneFrame = BuildSceneFrame();

                //Scene point lights (campfire / neon / planetshine) onto the shared instanced effect, so the
                //balls, island, cannon and city are lit by them under every dome, on top of the sun and sky.
                //The clock is the balls' own, so the campfire's light and its flame billboard cannot drift.
                _sceneLights.Apply(_scene, _sceneRenderer, _cityConfig.NeonLook, _pulseSeconds);

                if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
                {
                    bool neon = _scene == SceneKind.NeonCity;
                    _cityRenderer.CityNeon = neon ? 1f : 0f;
                    _cityRenderer.CityWindowBrightness = neon ? _cityConfig.NeonLook.WindowBrightness : _cityConfig.WindowBrightness;
                    //Frustum-culled and ordered near to far, as the game draws it — see City.PrepareVisible
                    int visibleBuildings = _city.PrepareVisible(_camera);
                    _cityRenderer.Draw(_camera, _city.Visible, visibleBuildings, _sceneEffectParams);
                }
                else
                    //The target goes in so the cavern and the dream can be shaded at the back buffer's size and
                    //scaled up (#155). Passed rather than remembered, so it cannot be the one the pipeline held
                    //before a resize or a supersample change replaced it.
                    _sceneRenderer.DrawEnvironment(_scene, sceneFrame, _pipeline.SceneTarget);

                //The forest's scattered trees, boulders and stumps: after the terrain they stand on (with depth,
                //or they would draw through it) and before the island. The state they need is the opaque scene
                //state stated above — alpha blend, depth test and write, cull counter-clockwise — plus this
                //frame's point lights, already on the shared effect; the component touches none of it, so the
                //island's slices below are unaffected.
                if (_scene == SceneKind.Forest) _forestScatter?.Draw(_camera);

                //The round island, opaque: its stone cap and concrete drum. Then the dark pit shaft behind the
                //drain, which is drawn in the solid-terrain scenes only and brings its own culling with it.
                //Each slice owns the states its own geometry needs; where they sit in the frame is this file's
                //decision, which is the whole reason the component hands them over separately.
                _island.DrawIsland(_camera, _sceneEffectParams);
                _island.DrawPit(_camera, _sceneEffectParams, _scene);

                //Into a local because the glazing further down is drawn with the very same pose — it is set into
                //this tube, so the one matrix serves both rather than being built twice a frame
                Matrix barrelWorld = _cannon.BarrelWorld();

                _cannonRig.Draw(_camera, barrelWorld, _sceneEffectParams);
                _cannonRig.DrawCarriage(_camera, _cannon.CarriageWorld(), _cannon.WheelTravel, _sceneEffectParams);

                //Every ball on the scene, collected and then put out: one instanced draw call per type and LOD
                //level. BeginFrame empties the buckets and is the only way to fill them, which is what makes the
                //once-per-frame visit structural rather than a rule to remember — the walk below advances each
                //ball's occlusion ease and its arrival glide, so a second collection in one frame would run both
                //at double speed while the drawn frame still looked perfectly correct (see BallRenderSet's
                //remarks; it throws rather than allow it). A ref struct local by design: it allocates nothing and
                //cannot be stashed in a field to bucket the next frame's balls against this frame's camera.
                //
                //Where this sits in the frame is still this file's: over the opaque scene, so the cluster and the
                //gun are in the depth buffer, and before the shots' additive smears and the drain's glass.
                BallDrawFrame frame = _balls.BeginFrame(_camera);

                _collectedBalls = _collector.Collect(frame, (float)gameTime.ElapsedGameTime.TotalSeconds,
                    _physicsBalls, _shotBalls, _fallingBalls);

                //The loaded queue goes into the same open frame, being balls like any other
                CollectMagazineBalls(frame);

                //Wall clock, not the simulation's step: the balls keep breathing while it is paused or slowed
                _balls.Draw(_pulseSeconds);

                //The launch smears trailing the shots, over the opaque scene (which the depth buffer now holds,
                //so the cluster/cannon/platform occlude them) and additive, so they glow through the glare.
                //It states the three states it needs and puts back exactly what it found, so the frame's
                //translucent baseline - which the two glass draws below depend on - is still standing here.
                _smears.Draw(_camera);

                //The drain's gold beads and then its glass, after the shots' smears: the beads are opaque and
                //belong with the opaque scene, and the glass composites over everything already in the frame.
                _island.DrawGlass(_camera, _sceneEffectParams);

                _ceilingPlate.Renderer.Draw(_camera, _ceiling.World, _sceneEffectParams);

                //The gun's own glass last of the three, because it is far and away the nearest: the loaded queue
                //is behind it and in the depth buffer by now, and so are the drain's cone and the ceiling's plate
                //the barrel is seen against. Composited first it would let both of those bleed through it.
                _cannonRig.DrawGlass(_camera, barrelWorld, _sceneEffectParams);

                //Falling snow settles over everything, so it is drawn last, in front of what it should hide
                _sceneRenderer.DrawOverlays(_scene, sceneFrame);
            }

            //Underwater murk: only the sea has water the camera can get under, and zero (a no-op in the shader)
            //in every other scene. SceneRenderer's answer since #159 rather than this file's own arithmetic — the
            //ball shader's submerge fade is released by the same figure this tint arrives with, and two effects
            //that hand over cannot be reading two copies of one expression (this and the Game's were exactly that).
            float underwater = _sceneRenderer.LensSubmergedAmount(_scene, _camera.Position);

            //And nothing ever takes this frame out of focus: the defocus is the game's end-of-level effect,
            //and the testbed has no level that ends. Zero is a no-op in the shader and its targets are never
            //even built (see PostProcessPipeline.EnsureDefocusChain).
            _pipeline.Resolve(_pulseSeconds, underwater, 0f);

            //The crosshair, in display space after the resolve: in free mode it marks where a shot from the camera
            //goes, so it is simply there (opacity 1); in game mode it appears only as precise aim engages, fading
            //in with PreciseAim.Blend, and marks the impact point the camera converges on - the overview's screen
            //centre points at nothing in particular. Everything else about it, the below-0.01 skip included, is
            //Crosshair's.
            _crosshair.Draw(_spriteBatch, _gameMode ? _preciseAim.Blend : 1f);

            base.Draw(gameTime);

            //Last, so it counts a frame that has actually been drawn end to end (the Game logs it from the
            //same place, for the same reason)
            if (_options.LogFrameRate) LogFrameRate((float)gameTime.ElapsedGameTime.TotalSeconds);

            //After the log line, so the idle is never counted as part of the frame it follows: what the cap
            //spends is real time between presents, and the reading has already been taken by here.
            CapFrameRate();
        }

        //The Game's counter restated rather than shared, because there is nothing to share it through: both are
        //a field pair and six lines against their own host's state. What matters is that the LINE is identical
        //in shape, so .claude/skills/benchmark reads either executable with one regex.
        //
        //Deliberately not InfoRenderer.CurrentFPS, which is what the overlay draws: that counter stops
        //advancing while the overlay is hidden (F12), and a benchmark run hides it.
        private float _fpsWindow;
        private int _fpsFrames;

        /// <summary>
        /// One line a second: the frame rate and every setting that changes what it means, so two runs — or two
        /// machines, or this executable against the Game — can be compared without remembering how each was
        /// launched. The arena's members are on it for the same reason the Game puts the city's drawn/total
        /// count on its own: it is a measurement run's whole subject, and a number taken with a member missing
        /// means something different from one taken with all five.
        /// </summary>
        private void LogFrameRate(float elapsed)
        {
            _fpsWindow += elapsed;
            _fpsFrames++;

            if (_fpsWindow < 1f) return;

            //Divided by the window actually measured rather than assumed to be a second: at the frame rates
            //this exists to measure, one frame overshoots it by more than a tenth
            //The clamped factor rather than the argument's: "ssaa=9" runs at 4, and the line has to say what
            //was actually shaded or it misreports the one setting that moves the number most
            Console.WriteLine($"[fps] {_fpsFrames / _fpsWindow:F1} — {_scene}, dome {_skyModelNumber}, ssaa {_supersampleFactor}x"
                + $", {GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}"
                + $", vsync {(_options.UncappedFps ? "off" : "on")}{(_options.FpsCap > 0 ? $" (cap {_options.FpsCap})" : "")}, arena {_island.Members}{(_options.CapProbe > 0 ? $", capprobe {_options.CapProbe}" : "")}, balls {_collectedBalls}");

            _fpsWindow = 0f;
            _fpsFrames = 0;
        }

        //When the next frame may be presented, on the wall clock, under fpscap=N. Stopwatch and not GameTime,
        //because what the cap spends is REAL time between presents; MonoGame's own fixed time step was the
        //other candidate and was refused, since it feeds Update a synthetic elapsed and runs it more than once
        //per Draw to catch up - which changes what the physics and every animation here are handed, in the one
        //mode whose whole purpose is to leave the frame alone and only measure it.
        private long _capNextFrameDue;

        /// <summary>
        /// Idles out the rest of the frame's period under <see cref="TestOptions.FpsCap"/>, so a scene cheaper
        /// than the cap stops running the card flat out (see that property for whose machine that crashed).
        /// A frame that already overran the period is never delayed and never made to pay it back: the debt
        /// would come out of the NEXT frame's idle and print a cheap frame as an expensive one.
        /// </summary>
        private void CapFrameRate()
        {
            if (_options.FpsCap <= 0) return;

            long period = Stopwatch.Frequency / _options.FpsCap;
            long now = Stopwatch.GetTimestamp();

            if (now >= _capNextFrameDue)
            {
                _capNextFrameDue = now + period;
                return;
            }

            //A SPIN, and deliberately never Thread.Sleep. Windows' default timer resolution is 15.6 ms, so
            //Sleep(1) hands the thread back at the next tick and costs about six milliseconds - measured here,
            //a 300 FPS cap slept its way down to 143 and a 400 FPS cap to 209, which is the instrument reading
            //its own idle instead of the frame. Spinning burns one core of twelve for the rest of the period,
            //in a mode that only ever runs under a benchmark, and holds the plateau on the cap itself.
            while (Stopwatch.GetTimestamp() < _capNextFrameDue) Thread.SpinWait(64);
            _capNextFrameDue += period;
        }

        /// <summary>
        /// Adds the magazine's queued balls to this frame's collection along the cannon axis: index 0 at the
        /// muzzle (the spawn point), the rest receding back towards the breech, so the player sees the colour
        /// that will fire and the ones behind it. Drawn as real balls — the same shader, pattern and emission as
        /// every other ball, through the same buckets — and unoccluded, a ball in the bore having nothing packed
        /// around it.
        /// <para>
        /// The magazine deliberately stayed with the callers when the rest of the ball drawing was hoisted:
        /// which colours are loaded, where the bore puts them and (in the Game) the transmute cross-fade are
        /// three different questions, and none of them is <see cref="BallRenderSet"/>'s.
        /// </para>
        /// </summary>
        /// <param name="frame">The collection <see cref="Draw"/> opened, passed along as <c>in</c> rather than
        /// reopened — a second <see cref="BallRenderSet.BeginFrame"/> in one frame is exactly the double-advance
        /// this type refuses.</param>
        private void CollectMagazineBalls(in BallDrawFrame frame)
        {
            //One read of the barrel's pose for the whole queue rather than one per ball, and taken AFTER the gun
            //has been updated this frame - a pose read before the barrel moves makes the queue lag a frame behind
            //the tube it is supposed to be inside, which reads as jitter. The balls take the barrel's own basis,
            //which is what stops them skewing in their slots, and the slide is already applied per slot. The
            //Testbed animates no recoil, so it passes none.
            BorePose pose = _magazine.Pose(_cannon, _cannonRig.PivotToFrontBall);

            //BallDrawFrame.Add rather than AddOriented: BorePose.SlotWorld has already built the matrix, writing
            //the slot's translation into the barrel's own basis rather than multiplying one in, and it hands the
            //position back because the LOD is picked by distance and reading a translation out of a matrix to
            //measure one is what goes wrong the day something is scaled.
            for (int slot = 0; slot < Magazine.SIZE; slot++)
            {
                Matrix world = pose.SlotWorld(slot, out Vector3 position);

                frame.Add(_magazine.Peek(slot), position, world, BallRenderSet.UNOCCLUDED);
            }
        }

        private void SetGraphics(bool windowed = false)
        {
            _graphics.PreferredBackBufferWidth = windowed ? _options.WindowWidth : GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = windowed ? _options.WindowHeight : GraphicsDevice.DisplayMode.Height;
            _graphics.IsFullScreen = !windowed;

            _graphics.SynchronizeWithVerticalRetrace = !_options.UncappedFps;

            _graphics.ApplyChanges();

            //Null-conditional for the constructor's call, which runs before LoadContent has built the
            //pipeline (the old in-class EnsureSceneTarget guarded on GraphicsDevice == null the same way)
            _pipeline?.EnsureTarget();

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void Graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = _options.UncappedFps ? PresentInterval.Immediate : PresentInterval.One;
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;

            //The 3D scene never reaches the back buffer any more — it goes through the HDR target and
            //arrives as one already-resolved full-screen quad — so multisampling the back buffer would
            //cost memory and antialias nothing. Any MSAA now belongs on the scene target itself.
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 0;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;
            _info.RecomputeScale();
            _pipeline?.EnsureTarget();
            FitCannonAndGameCameraToMap(); //The frustum's width just changed, and the fit is checked on both axes
        }
    }
}
