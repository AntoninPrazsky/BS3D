using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Physics;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using Prazsky.Render;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Testbed.Backdrops;
using static Testbed.Simu;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Testbed
{
    public class Testbed : Game
    {
        private BasicCamera3D _camera;
        private Model _hrSphere;
        private Microsoft.Xna.Framework.Matrix[] _hrSphereTransformations;

        private Model _groundModel, _topPlatformModel;
        private KinematicBody _ceiling;
        private TypedIndex _ceilingShapeIndex;

        /// <summary>
        /// Size of the ceiling before a map is loaded; also the size the TopGrid model is modelled at.
        /// </summary>
        private static readonly float DEFAULT_CEILING_SIZE = 10f;

        private Simulation _simulation;
        private ThreadDispatcher _threadDispatcher;
        private BufferPool _bufferPool;

        private List<StaticBody> _staticBodies;
        private PhysicsBall[,,] _physicsBalls;
        private BallsMap _map;

        private CameraInputHelper _cih;

        private SkyDome _sky;
        private Model _skyModel;
        private byte _skyModelNumber = 1;

        private ButtonAction[] _actions;

        private bool _simulate = true;
        private bool _draw = true;
        private bool _gameMode = false;
        private bool _slowSimulation = false;

        private Vector3 _gameCameraOffset = Vector3.Up * 6.5f;

        #region Game mode transition animation

        private bool _gameModeAnimStarted = false;
        private bool _freeModeAnimStarted = false;
        private float _gameModeAnimStep = 0f;

        private static readonly float ANIMATION_SPEED = Constants.THOUSANDTH;

        private Vector3 _beforeAnimationPosition = Vector3.Zero;
        private Vector3 _beforeAnimationTarget = Vector3.Zero;

        #endregion

        #region Graphics

        private int _windowWidth;
        private int _windowHeight;

        private GraphicsDeviceManager _graphics;
        private bool _windowed;

        private InfoRenderer _info;

        private static readonly int MSAA_SAMPLES = 8;
        private static readonly PresentInterval PRESENTATION_INTERVAL = PresentInterval.One;
        private static float GAME_FOV = (float)Math.PI / 3.1f;
        private static float FREE_FOV = (float)Math.PI / 2.5f;
        private static Vector3 DEFAULT_CAMERA_POS = new (0f, -3f, 30f);

        #endregion Graphics

        #region Shooting

        private BodyDescription _shotBall;
        private List<PhysicsBall> _shotBalls;

        //Balls released from the structure (matched clusters and balls that lost their connection to the ceiling).
        //They are no longer part of the map, but their bodies keep falling in the simulation, so they still have to be drawn.
        //RemoveFallenBalls cleans them up once they fall out of the world or come to rest.
        private List<PhysicsBall> _fallingBalls;

        private static readonly float SHOOT_MULTIPLIER = 200f;
        private static readonly Random RANDOM = new();

        private Model _cilinderModel;
        private Cannon _cannon;

        private SpriteBatch _spriteBatch;
        private Texture2D _aimer;
        private Vector2 _aimerPos;
        private Color _aimerColor = new((byte)255, (byte)255, (byte)255, (byte)64);

        #endregion

        #region Backdrops

        Model _castleModel;
        Castle _castle;

        #endregion

        #region Contacts

        private ContactEvents _events;
        private EventHandler _eventHandler;

        #endregion

        //Map file to load right after startup (e.g. passed on the command line); mainly for testing
        private readonly string _startupMapPath;

        //Testing mode: shoots a ball at a random spot of the structure every second ("autoshoot" on the command line)
        private readonly bool _autoShoot;
        private float _autoShootElapsed;

        public Testbed(bool windowed = true, int windowWidth = 1280, int windowHeight = 800, string startupMapPath = null, bool autoShoot = false)
        {
            _windowed = windowed;
            _startupMapPath = startupMapPath;
            _autoShoot = autoShoot;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreparingDeviceSettings += Graphics_PreparingDeviceSettings;
        
            Content.RootDirectory = "Content";

            _windowWidth = windowWidth;
            _windowHeight = windowHeight;

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += Window_ClientSizeChanged;
            Window.FileDrop += Window_FileDrop;

            SetGraphics(_windowed);
        }

        private void Window_FileDrop(object sender, FileDropEventArgs e)
        {
            if (e.Files == null || e.Files.Length <= 0 || string.IsNullOrEmpty(e.Files[0])) return;
            DeserializeMapFromFile(e.Files[0]);
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;
            _info.RecomputeScale();
            ComputeAimerPosition();
        }

        protected override void Initialize()
        {
            IsMouseVisible = true;

            _camera = new BasicCamera3D(DEFAULT_CAMERA_POS, GraphicsDevice.Viewport.AspectRatio, FREE_FOV);
            _info = new InfoRenderer(this, "Content/Fonts/cascadia", "Content/Bitmaps/Controller") { DrawOrder = int.MaxValue };
            Components.Add(_info);

            _cih = new CameraInputHelper(_camera, this);

            _staticBodies = new List<StaticBody>();

            _threadDispatcher = new ThreadDispatcher(Environment.ProcessorCount);
            _bufferPool = new BufferPool();
            _events = new ContactEvents(_threadDispatcher, _bufferPool);

            _simulation = Simulation.Create(
                _bufferPool,
                new NarrowPhaseCallbacks(_events),
                new PoseIntegratorCallbacks(new System.Numerics.Vector3(0, Constants.EARTH_GRAVITY, 0)),
                new SolveDescription(8, 1));

            #region Controls

            _actions = new ButtonAction[]
            {
                new(mgKeys.Escape, Buttons.Back, Exit, "Exit"),
                new(mgKeys.F2, Buttons.DPadLeft, LoadBallsMap, "Load map"),
                new(mgKeys.F5, Buttons.B, () => _simulate = !_simulate, "Stop/start simulation"),
                new(mgKeys.F6, Buttons.X, () => _draw = !_draw, "Hide/show 3D rendering"),
                new(mgKeys.F9, () => _slowSimulation = !_slowSimulation, "Switch simulation speed"),
                new(mgKeys.F10, () => SwitchGameMode(!_gameMode), "Switch game mode"),
                new(mgKeys.F11, () => SetGraphics(_graphics.IsFullScreen), "Fullscreen/windowed"),
                new(mgKeys.F12, () => _info.Visible = !_info.Visible, "Hide/show text overlay"),
                new(mgKeys.End, Buttons.Start, RemoveAllConstraints, "Remove all constraints"),
                new(mgKeys.NumPad1, SwitchSkyDome, "Switch sky dome"),
                new(mgKeys.D1, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Forward, true), "Forward view"),
                new(mgKeys.D2, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Backward, true), "Backward view"),
                new(mgKeys.D3, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Left, true), "Left view"),
                new(mgKeys.D4, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Right, true), "Right view"),
                new(mgKeys.D5, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Up, true), "Up view"),
                new(mgKeys.D6, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Down, true), "Down view"),
                new(mgKeys.R, () => { _cih.RestartCamera(); _cannon.Restart(); }, "Restart camera"),
                new(mgKeys.Space, () => ShootBall(), "Shoot ball")
            };

            string format = "{0,-9} {1}\n";

            StringBuilder builder = new();
            foreach (var act in _actions) builder.Append(string.Format(format, act.Key.ToString(), act.Description));
            builder.Append(string.Format(format, "Arrows", "Cannon aiming"));
            builder.Append(string.Format(format, mgKeys.NumPad4.ToString(), "Move cannon left"));
            builder.Append(string.Format(format, mgKeys.NumPad6.ToString(), "Move cannon right"));
            builder.Append(string.Format(format, mgKeys.NumPad7.ToString(), "Camera orbit left"));
            builder.Append(string.Format(format, mgKeys.NumPad9.ToString(), "Camera orbit right"));

            _info.HintText = builder.ToString();

            #endregion

            InitializeShooting();

            base.Initialize();
        }

        private void SwitchGameMode(bool gameMode)
        {
            if (_gameMode == gameMode) return;
            if (_gameModeAnimStarted || _freeModeAnimStarted) return;

            _gameMode = gameMode;
            _info.ShowIcon = gameMode;

            if (_gameMode)
            {
                _gameModeAnimStarted = true;
                _beforeAnimationPosition = _camera.Position;
                _beforeAnimationTarget = _camera.Target;
            }
            else
            {
                _freeModeAnimStarted = true;
            }
        }

        protected override void LoadContent()
        {
            _hrSphere = Content.Load<Model>("Balls/DebugSphere"); //HRGeoDome

            _hrSphereTransformations = new Microsoft.Xna.Framework.Matrix[_hrSphere.Bones.Count];
            _hrSphere.CopyAbsoluteBoneTransformsTo(_hrSphereTransformations);

            #region Ground and ceiling

            _groundModel = Content.Load<Model>("GameObjects/Ground");
            _topPlatformModel = Content.Load<Model>("GameObjects/TopGrid");

            BuildGroundAndCeiling();

            #endregion Ground and ceiling

            #region Contact events

            _eventHandler = new EventHandler(_simulation, _bufferPool, _events, _ceiling, _physicsBalls, _shotBalls, _fallingBalls);
            _events.Initialize(_simulation);

            #endregion

            _skyModel = Content.Load<Model>("Skyes/SkyDome" + _skyModelNumber);
            _sky = new SkyDome(_skyModel, GraphicsDevice);

            _cilinderModel = Content.Load<Model>("GameObjects/Cilinder");
            _cannon = new Cannon(_cilinderModel, new Vector3(0f, 5f, 0f), -6.4f, 20f);

            _castleModel = Content.Load<Model>("Backdrops/Castle");
            _castle = new Castle(_castleModel, new Vector3(0f, -8.5f, -60f), Microsoft.Xna.Framework.MathHelper.Pi);

            _aimer = Content.Load<Texture2D>("Bitmaps/Aimer");
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            ComputeAimerPosition();

            if (!string.IsNullOrEmpty(_startupMapPath) && File.Exists(_startupMapPath)) DeserializeMapFromFile(_startupMapPath);
        }

        private void ComputeAimerPosition()
        {
            _aimerPos = new Vector2(GraphicsDevice.Viewport.Width / 2f - _aimer.Width / 2f, GraphicsDevice.Viewport.Height / 2f - _aimer.Height / 2f);
        }

        private void SwitchSkyDome()
        {
            if (_skyModelNumber == 18) _skyModelNumber = default;

            _skyModelNumber++;
            _skyModel = Content.Load<Model>("Skyes/SkyDome" + _skyModelNumber);
            _sky.SkyDomeModel = _skyModel;
        }

        private void BuildGroundAndCeiling()
        {
            Box groundBox = new(30f, 1f, 30f);

            _staticBodies.Add(new(_groundModel, CreateStatic(new(0f, -10f, 0f), groundBox)));

            _staticBodies.Add(new(_groundModel, CreateStatic(new(-30f, -9f, 0f), groundBox)));
            _staticBodies.Add(new(_groundModel, CreateStatic(new(30, -9f, 0f), groundBox)));
            _staticBodies.Add(new(_groundModel, CreateStatic(new(0f, -9f, 30f), groundBox)));
            _staticBodies.Add(new(_groundModel, CreateStatic(new(0f, -9f, -30f), groundBox)));

            _staticBodies.Add(new(_groundModel, CreateStatic(new(-30f, -9f, -30f), groundBox)));
            _staticBodies.Add(new(_groundModel, CreateStatic(new(30, -9f, 30f), groundBox)));
            _staticBodies.Add(new(_groundModel, CreateStatic(new(-30f, -9f, 30f), groundBox)));
            _staticBodies.Add(new(_groundModel, CreateStatic(new(30f, -9f, -30f), groundBox)));

            Box box = new(DEFAULT_CEILING_SIZE, 1f, DEFAULT_CEILING_SIZE);
            TypedIndex boxShapeIndex = _simulation.Shapes.Add(box);
            _ceilingShapeIndex = boxShapeIndex;
            CollidableDescription collidableDescription = new(boxShapeIndex, 0.1f);
            BodyDescription bodyDescription = BodyDescription.CreateKinematic(new System.Numerics.Vector3(0f, GetCeilingY(10), 0f), collidableDescription, new BodyActivityDescription(Constants.HUNDREDTH));

            BodyHandle topBodyHandle = _simulation.Bodies.Add(in bodyDescription);
            BodyReference topBodyReference = new(topBodyHandle, _simulation.Bodies);

            _ceiling = new KinematicBody(_topPlatformModel, topBodyReference, topBodyHandle);
        }

        /// <summary>
        /// The ceiling hovers this far above the centre of the top-level balls (their Y is (levels - 1)/√2).
        /// </summary>
        private static float GetCeilingY(byte levels) => (levels - 1) / Constants.SQRT_TWO + 2f;

        /// <summary>
        /// Moves and resizes the ceiling so it covers the play field of the given map and sits just above its top level.
        /// The kinematic body is kept (constraints of a previously loaded structure may still reference it);
        /// only its pose, collision shape and drawn model scale change.
        /// </summary>
        private void FitCeilingToMap(BallsMap map)
        {
            float sizeX = map.StageSizeX + 1f; //Odd levels are shifted by +0.5 and balls have radius 0.5, so add a margin
            float sizeZ = map.StageSizeZ + 1f;

            BodyReference ceilingReference = _ceiling.BodyReference;
            ceilingReference.Pose.Position = new System.Numerics.Vector3(0f, GetCeilingY(map.Levels), 0f);

            TypedIndex newShapeIndex = _simulation.Shapes.Add(new Box(sizeX, 1f, sizeZ));
            ceilingReference.SetShape(newShapeIndex);
            _simulation.Shapes.Remove(_ceilingShapeIndex);
            _ceilingShapeIndex = newShapeIndex;

            //Recreate the wrapper so the drawn model matches the new pose and size (the body and handle stay the same)
            _ceiling = new KinematicBody(_topPlatformModel, ceilingReference, _ceiling.BodyHandle,
                new Vector3(sizeX / DEFAULT_CEILING_SIZE, 1f, sizeZ / DEFAULT_CEILING_SIZE));
        }

        private void LoadBallsMap()
        {
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new())
            {
                openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                openFileDialog.Filter = Constants.MAPS_FILE_FILTER;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                }
            }

            if (string.IsNullOrEmpty(filePath)) return;

            DeserializeMapFromFile(filePath);
        }

        private void DeserializeMapFromFile(string filePath)
        {
            _map = new(filePath, _hrSphere);
            _map.Center();
            _eventHandler.Map = _map;

            FitCeilingToMap(_map);

            _physicsBalls = BallsConstraintsBuilder.BuildBallsStructure(_map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference);
            _eventHandler.PhysicsBalls = _physicsBalls;

            RecountBallsAndConstraints();
        }

        private void RecountBallsAndConstraints()
        {
            _info.CustomText = "Balls on scene: " + (_simulation.Bodies.ActiveSet.Count) + "\nConstraints count: " + _simulation.Solver.CountConstraints();
        }

        /// <summary>
        /// Y below which a ball is considered fallen out of the world (the ground sits around Y = -10).
        /// </summary>
        private static readonly float KILL_PLANE_Y = -30f;

        /// <summary>
        /// Removes balls that can no longer affect gameplay from the simulation and from the given list:
        /// balls that fell below <see cref="KILL_PLANE_Y"/> and balls that came to rest on the ground
        /// (their body fell asleep - flying or rolling bodies never sleep).
        /// </summary>
        /// <param name="unregisterListeners">Pass true for shot balls, which may still be registered as contact listeners.</param>
        /// <returns>Number of removed balls.</returns>
        private int RemoveFallenBalls(List<PhysicsBall> balls, bool unregisterListeners)
        {
            int removed = 0;

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                BodyReference body = balls[i].BallReference;

                if (body.Pose.Position.Y >= KILL_PLANE_Y && body.Awake) continue;

                if (unregisterListeners && _events.IsListener(body.CollidableReference)) _events.Unregister(body.CollidableReference);

                _simulation.Bodies.Remove(body.Handle);
                balls.RemoveAt(i);
                removed++;

#if DEBUG
                Console.WriteLine("Removed a fallen ball from the simulation");
#endif
            }

            return removed;
        }

        protected override void Update(GameTime gameTime)
        {
            float timeStep = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 60f);
            if (timeStep == 0) timeStep = 1 / 60f;

            if (_simulate)
            {
                if (_slowSimulation) _simulation.Timestep(timeStep * Constants.HUNDREDTH, _threadDispatcher);
                else _simulation.Timestep(timeStep, _threadDispatcher);

                #region Contact events

                //Flush must run right after the timestep and before ProcessQueuedContacts:
                //unregistering a listener is only safe once the pending worker adds collected during the timestep have been applied.
                _events.Flush();
                if (_eventHandler.ProcessQueuedContacts() > 0) RecountBallsAndConstraints();

                #endregion

                #region Fallen balls cleanup

                int removedBalls = RemoveFallenBalls(_shotBalls, unregisterListeners: true) + RemoveFallenBalls(_fallingBalls, unregisterListeners: false);
                if (removedBalls > 0) RecountBallsAndConstraints();

                #endregion

                #region Auto shooting (testing)

                if (_autoShoot && _map != null)
                {
                    _autoShootElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    if (_autoShootElapsed >= 1f)
                    {
                        _autoShootElapsed = 0f;
                        ShootBall(new Vector3(RANDOM.Next(-4, 5), RANDOM.Next(4, 11), RANDOM.Next(-4, 5)));
                    }
                }

                #endregion
            }

            if (IsActive)
            {
                _cih.RegisterCurrentInputState();

                foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

                _cih.Update(gameTime);
                _cih.CameraMovement(gameTime, !_gameMode);
                _cih.RegisterPreviousInputState();
            }
            else IsMouseVisible = true;

            _cih.MouseMovementDenominator = timeStep / Constants.THOUSANDTH;

            UpdateCannon(gameTime);

            #region Game mode animation

            if (_gameModeAnimStarted && _gameMode)
            {
                _camera.Position = Vector3.SmoothStep(_beforeAnimationPosition, GetCanonOffsettedPos(), _gameModeAnimStep);
                _camera.Target = Vector3.SmoothStep(_beforeAnimationTarget, GetCannonOffsettedTarget(), _gameModeAnimStep * 2f);
                _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(FREE_FOV, GAME_FOV, _gameModeAnimStep);

                _gameModeAnimStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_gameModeAnimStep > Constants.ONE)
                {
                    _gameModeAnimStep = 0;
                    _gameModeAnimStarted = false;
                }
            }

            if (_freeModeAnimStarted && !_gameMode)
            {
                _camera.FieldOfView = Microsoft.Xna.Framework.MathHelper.SmoothStep(GAME_FOV, FREE_FOV, _gameModeAnimStep);

                _gameModeAnimStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_gameModeAnimStep > 1f)
                {
                    _gameModeAnimStep = 0;
                    _freeModeAnimStarted = false;
                }
            }

            #endregion

            base.Update(gameTime);
        }

        private void RemoveAllStatics()
        {
            foreach (var staticBody in _staticBodies) _simulation.Statics.Remove(staticBody.StaticReference.Handle);
            _staticBodies.Clear();
        }

        private void RemoveAllConstraints()
        {
            if (_physicsBalls == null || _physicsBalls.Rank != 3) return;

            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                        _physicsBalls[x, z, level]?.RemoveAllConstraints(_simulation);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.LightSlateGray);

            _sky.Draw(_camera);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //TODO: GameManager for drawing balls and optimize drawing (currently it is slow)
            if (_draw)
            {
                for (int i = 0; i < _staticBodies.Count; i++) _staticBodies[i].Draw(_camera);

                _ceiling.Draw(_camera);
                _cannon.Draw(_camera);

                if (_physicsBalls != null)
                {
                    XZLevel size = XZLevel.FromArray( _physicsBalls);

                    for (byte level = 0; level < size.Level; level++)
                        for (byte x = 0; x < size.X; x++)
                            for (byte z = 0; z < size.Z; z++)
                                if (_physicsBalls[x, z, level] != null)
                                {
                                    Microsoft.Xna.Framework.Matrix ballWorldMatrix = Microsoft.Xna.Framework.Matrix.CreateFromQuaternion(new Quaternion(
                                    _physicsBalls[x, z, level].BallReference.Pose.Orientation.X,
                                    _physicsBalls[x, z, level].BallReference.Pose.Orientation.Y,
                                    _physicsBalls[x, z, level].BallReference.Pose.Orientation.Z,
                                    _physicsBalls[x, z, level].BallReference.Pose.Orientation.W))
                                * Microsoft.Xna.Framework.Matrix.CreateTranslation(
                                    _physicsBalls[x, z, level].BallReference.Pose.Position.X,
                                    _physicsBalls[x, z, level].BallReference.Pose.Position.Y,
                                    _physicsBalls[x, z, level].BallReference.Pose.Position.Z);

                                    ICamera camera = _camera;
                                    BasicEffectParams basicEffectParams = BasicEffectParamsProvider.GetEffectByType(_physicsBalls[x, z, level].Type);

                                    ModelRenderer.Render(_hrSphere, _hrSphereTransformations, ref camera, ballWorldMatrix, basicEffectParams, true, true);
                                }
                }

                DrawDynamicBalls(_shotBalls);
                DrawDynamicBalls(_fallingBalls);

                _castle.Draw(_camera);
            }

            if (!_gameMode)
            {
                _spriteBatch.Begin();
                _spriteBatch.Draw(_aimer, _aimerPos, _aimerColor);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        private void DrawDynamicBalls(List<PhysicsBall> balls)
        {
            int ballsCount = balls.Count;
            for (int i = 0; i < ballsCount; i++)
            {
                Microsoft.Xna.Framework.Matrix ballWorldMatrix = Microsoft.Xna.Framework.Matrix.CreateFromQuaternion(
                        new Quaternion(
                            balls[i].BallReference.Pose.Orientation.X,
                            balls[i].BallReference.Pose.Orientation.Y,
                            balls[i].BallReference.Pose.Orientation.Z,
                            balls[i].BallReference.Pose.Orientation.W))
                        * Microsoft.Xna.Framework.Matrix.CreateTranslation(
                            balls[i].BallReference.Pose.Position.X,
                            balls[i].BallReference.Pose.Position.Y,
                            balls[i].BallReference.Pose.Position.Z);

                ICamera camera = _camera;
                BasicEffectParams basicEffectParams = BasicEffectParamsProvider.GetEffectByType(balls[i].Type);

                ModelRenderer.Render(_hrSphere, _hrSphereTransformations, ref camera, ballWorldMatrix, basicEffectParams, true, true);
            }
        }

        private void SetGraphics(bool windowed = false)
        {
            _graphics.PreferredBackBufferWidth = windowed ? _windowWidth : GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = windowed ? _windowHeight : GraphicsDevice.DisplayMode.Height;
            _graphics.IsFullScreen = !windowed;

            _graphics.SynchronizeWithVerticalRetrace = true;

            _graphics.ApplyChanges();

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void Graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = PRESENTATION_INTERVAL;
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = MSAA_SAMPLES;
        }

        private StaticReference CreateStatic(System.Numerics.Vector3 position, Box boundingBox)
        {
            var shape = new CollidableDescription(_simulation.Shapes.Add(boundingBox), 0.1f).Shape;
            return new StaticReference(_simulation.Statics.Add(new StaticDescription(position, shape)), _simulation.Statics);
        }

        protected override void UnloadContent()
        {
            _simulation.Dispose();
            _threadDispatcher.Dispose();
            _bufferPool.Clear();
        }

        private void InitializeShooting()
        {
            var ballShape = new Sphere(BallsConstraintsBuilder.BALL_RADIUS);
            _shotBall = BodyDescription.CreateDynamic(new System.Numerics.Vector3(), ballShape.ComputeInertia(BallsConstraintsBuilder.BALL_MASS), _simulation.Shapes.Add(ballShape), Constants.HUNDREDTH);
            _shotBalls = new List<PhysicsBall>();
            _fallingBalls = new List<PhysicsBall>();
        }

        private void ShootBall(Vector3? targetOverride = null)
        {
            var sourcePosition = _gameMode ? _cannon.Position : _camera.Position;
            var shootTarget = targetOverride ?? (_gameMode ? _cannon.AimTarget : _camera.Target);

            _shotBall.Pose.Position = new System.Numerics.Vector3(sourcePosition.X, sourcePosition.Y, sourcePosition.Z);

            var direction = shootTarget - sourcePosition;
            direction.Normalize();
            direction *= SHOOT_MULTIPLIER;

            _shotBall.Velocity.Linear = new System.Numerics.Vector3(direction.X, direction.Y, direction.Z);

            BodyHandle bodyHandle = _simulation.Bodies.Add(_shotBall);

            PhysicsBall ball = new()
            {
                BallReference = new(bodyHandle, _simulation.Bodies),
                Type = (BallType)RANDOM.Next((int)BallType.Type1, (int)BallType.Type4 + 1) //Random colour so same-type clusters can form
            };

            _shotBalls.Add(ball);
            RecountBallsAndConstraints();

            #region Contact event registration

            //TODO: Unregister when removed from world
            _events.Register(_simulation.Bodies[bodyHandle].CollidableReference, _eventHandler);

            #endregion
        }

        private void UpdateCannon(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(mgKeys.NumPad4)) _cannon.Orbit(1f);
            if (Keyboard.GetState().IsKeyDown(mgKeys.NumPad6)) _cannon.Orbit(-1f);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Up)) _cannon.Aim(new Vector2(1f, 0f), gameTime);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Down)) _cannon.Aim(new Vector2(-1f, 0f), gameTime);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Left)) _cannon.Aim(new Vector2(0f, 1f), gameTime);
            if (Keyboard.GetState().IsKeyDown(mgKeys.Right)) _cannon.Aim(new Vector2(0f, -1f), gameTime);

            if (_gameMode && !_gameModeAnimStarted)
            {
                _camera.Position = GetCanonOffsettedPos();
                _camera.Target = GetCannonOffsettedTarget();
            }

            _cannon.Update(gameTime);
        }

        private Vector3 GetCannonDirection() => Vector3.Normalize(_cannon.Position - _cannon.OrbitCenter) * 10f;
        private Vector3 GetCanonOffsettedPos() => _cannon.Position + GetCannonDirection() + _gameCameraOffset;
        private Vector3 GetCannonOffsettedTarget() => _cannon.OrbitCenter - _gameCameraOffset;
    }

    #region Contact event

    //WIP
    public class EventHandler : IContactEventHandler
    {
        public Simulation Simulation;
        public BufferPool Pool;
        private ContactEvents _contactEvents;
        private KinematicBody _ceiling;
        public BallsMap Map;
        public PhysicsBall[,,] PhysicsBalls;
        public List<PhysicsBall> ShotBalls;
        public List<PhysicsBall> FallingBalls;

        public EventHandler(Simulation simulation, BufferPool pool, ContactEvents contactEvents, KinematicBody ceiling, PhysicsBall[,,] physicsBalls, List<PhysicsBall> shotBalls, List<PhysicsBall> fallingBalls)
        {
            Simulation = simulation;
            Pool = pool;
            _contactEvents = contactEvents;
            _ceiling = ceiling;
            PhysicsBalls = physicsBalls;
            ShotBalls = shotBalls;
            FallingBalls = fallingBalls;
        }

        //Contact callbacks run inside Simulation.Timestep, potentially from multiple worker threads at once.
        //Mutating the simulation (constraints, velocities) or the ContactEvents listener set from there corrupts state
        //the solver and the event system are using (this used to cause occasional NullReferenceExceptions).
        //Contacts are therefore only recorded here and processed on the main thread by ProcessQueuedContacts after the timestep.
        private readonly ConcurrentQueue<QueuedContact> _queuedContacts = new();

        private readonly struct QueuedContact
        {
            public readonly CollidableReference EventSource;
            public readonly CollidablePair Pair;
            public readonly Vector3 ContactOffset;
            public readonly Vector3 ContactNormal;
            public readonly float Depth;
            public readonly int FeatureId;
            public readonly int ContactIndex;
            public readonly int WorkerIndex;

            public QueuedContact(CollidableReference eventSource, CollidablePair pair, Vector3 contactOffset, Vector3 contactNormal,
                float depth, int featureId, int contactIndex, int workerIndex)
            {
                EventSource = eventSource;
                Pair = pair;
                ContactOffset = contactOffset;
                ContactNormal = contactNormal;
                Depth = depth;
                FeatureId = featureId;
                ContactIndex = contactIndex;
                WorkerIndex = workerIndex;
            }
        }

        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            _queuedContacts.Enqueue(new QueuedContact(eventSource, pair, contactOffset, contactNormal, depth, featureId, contactIndex, workerIndex));
        }

        /// <summary>
        /// Processes contacts recorded during the last timestep. Must be called from the main thread while the simulation is not stepping,
        /// after <see cref="ContactEvents.Flush"/>.
        /// </summary>
        /// <returns>Number of balls attached to the ceiling.</returns>
        public int ProcessQueuedContacts()
        {
            int attachedBalls = 0;
            while (_queuedContacts.TryDequeue(out QueuedContact contact))
                if (ProcessContact(contact)) attachedBalls++;
            return attachedBalls;
        }

        private bool ProcessContact(in QueuedContact contact)
        {
            CollidablePair pair = contact.Pair;

#if DEBUG
            Console.WriteLine(" → Ball collided!");
            Console.WriteLine(nameof(contact.EventSource) + " : " + contact.EventSource.ToString());
            Console.WriteLine(nameof(pair.A) + " : " + pair.A.ToString());
            Console.WriteLine(nameof(pair.B) + " : " + pair.B.ToString());
            Console.WriteLine(nameof(contact.ContactOffset) + " : " + contact.ContactOffset.ToString());
            Console.WriteLine(nameof(contact.ContactNormal) + " : " + contact.ContactNormal.ToString());
            Console.WriteLine(nameof(contact.Depth) + " : " + contact.Depth.ToString());
            Console.WriteLine(nameof(contact.FeatureId) + " : " + contact.FeatureId.ToString());
            Console.WriteLine(nameof(contact.ContactIndex) + " : " + contact.ContactIndex.ToString());
            Console.WriteLine(nameof(contact.WorkerIndex) + " : " + contact.WorkerIndex.ToString());
            Console.WriteLine();
#endif

            //Once ball touches the ground or ceiling, unregister collision event
            //TODO: This might be possible to do by checking if the Static/Kinematic body is specific object (ground block, ceiling block by BodyReference)
            if (pair.A.Mobility == CollidableMobility.Static || pair.B.Mobility == CollidableMobility.Static ||
                pair.A.Mobility == CollidableMobility.Kinematic || pair.B.Mobility == CollidableMobility.Kinematic)
            {
                //A single timestep can queue several contacts for the same ball, so the listener may have been unregistered by a previous one
                if (pair.A.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.A)) _contactEvents.Unregister(pair.A);
                if (pair.B.Mobility == CollidableMobility.Dynamic && _contactEvents.IsListener(pair.B)) _contactEvents.Unregister(pair.B);
            }

            if (Map == null)
            {
                Console.WriteLine("Map is null\n");
                return false;
            }

            //The event source is the registered listener, i.e. the shot ball
            BodyHandle shotBallHandle = contact.EventSource.BodyHandle;
            var physicsBall = ShotBalls.Where(x => x.BallReference.Handle == shotBallHandle).FirstOrDefault(); //Linq is ok since this list should be short
            if (physicsBall == null)
            {
#if DEBUG
                Console.WriteLine("Ball already attached or no longer tracked as shot, skipping");
#endif
                return false;
            }

            CollidableReference other = pair.A.Packed == contact.EventSource.Packed ? pair.B : pair.A;

            #region Find a free cell for the ball

            Vector3 allowedPosition;
            XZLevel arrayPosition;

            if (other.Mobility == CollidableMobility.Kinematic && other.BodyHandle == _ceiling.BodyHandle)
            {
#if DEBUG
                Console.WriteLine(" → CEILING HIT");
#endif
                allowedPosition = Map.PutBallAtClosestEmptyCeilingPosition(contact.ContactOffset, out arrayPosition, physicsBall.Type);
            }
            else if (other.Mobility == CollidableMobility.Dynamic && TryFindMapBall(other.BodyHandle, out PhysicsBall hitBall))
            {
#if DEBUG
                Console.WriteLine(" → STRUCTURE BALL HIT");
#endif
                //Manifold offsets are relative to the position of the pair's first collidable
                var worldContact = Simulation.Bodies[pair.A.BodyHandle].Pose.Position + contact.ContactOffset.ToNumerics();
                allowedPosition = Map.PutBallAtClosestEmptyPositionNextTo(worldContact, hitBall.ArrayPosition, out arrayPosition, physicsBall.Type);
            }
            else return false; //Ground, a loose shot ball, …

            if (allowedPosition.X == float.MinValue)
            {
#if DEBUG
                Console.WriteLine("Outside of the map or every neighbouring cell already occupied by another ball");
#endif
                return false;
            }

#if DEBUG
            Console.WriteLine("Ball placed at: " + allowedPosition);
#endif

            #endregion

            #region Attach the ball to the structure

            physicsBall.ArrayPosition = arrayPosition;

            ShotBalls.Remove(physicsBall); //Not shot anymore

            PhysicsBalls[arrayPosition.X, arrayPosition.Z, arrayPosition.Level] = physicsBall; //Part of the map now

            physicsBall.BallReference.Velocity.Linear = default; //Removing velocity from the shot
            physicsBall.BallReference.Velocity.Angular = default; //Also stop spinning, so the freshly created constraint anchors are not dragged around by residual rotation

            //Constraint anchors are computed from the static map grid (ideal positions) and rotated into each body's current local frame,
            //so they are correct even after the simulation has been running
            BallsConstraintsBuilder.AttachBallToStructure(physicsBall, PhysicsBalls, Map, Simulation, _ceiling.BodyReference);

            //Attached to the structure – no need to listen for its contacts anymore
            if (_contactEvents.IsListener(contact.EventSource)) _contactEvents.Unregister(contact.EventSource);

            #region Same-type cluster removal

            int releasedBalls = BallsConstraintsBuilder.ReleaseSameTypeCluster(physicsBall, PhysicsBalls, Map, Simulation, FallingBalls);

#if DEBUG
            if (releasedBalls > 0) Console.WriteLine($"Released a cluster of {releasedBalls} balls of type {physicsBall.Type}");
#endif

            #endregion

            #endregion

            return true;
        }

        private bool TryFindMapBall(BodyHandle handle, out PhysicsBall ball)
        {
            ball = null;
            if (PhysicsBalls == null) return false;

            XZLevel size = XZLevel.FromArray(PhysicsBalls);

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                    {
                        PhysicsBall candidate = PhysicsBalls[x, z, level];
                        if (candidate != null && candidate.BallReference.Handle == handle)
                        {
                            ball = candidate;
                            return true;
                        }
                    }

            return false;
        }
    }

    #endregion

    #region IContactEventHandler

    public interface IContactEventHandler
    {
        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnContactRemoved<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int removedFeatureId, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnStartedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnStoppedTouching<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnPairCreated<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        //void OnPairUpdated<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold> { }
        void OnPairEnded(CollidableReference eventSource, CollidablePair pair) { }

        #endregion
    }
}

/*

Ball's contact points position from its origin (0,0,0):
        [ X,   Y,   Z ]
TOP   : [ 0,   0,   0.5]
DOWN  : [ 0,   0,  -0.5]
LEFT  : [ 0,  -0.5, 0]
RIGT  : [ 0,   0.5, 0]
FRONT : [ 0.5, 0,   0]
BACK  : [-0.5, 0,   0]

TOP-LEFT-BACK   : [-0.25, -0.25,  0.353553]
TOP-RIGHT-BACK  : [-0.25,  0.25,  0.353553]
TOP-LEFT-FRONT  : [0.25,  -0.25,  0.353553]
TOP-RIGHT-FRONT : [0.25,   0.25,  0.353553]

BOTTOM-LEFT-BACK   : [-0.25, -0.25, -0.353553]
BOTTOM-RIGHT-BACK  : [-0.25,  0.25, -0.353553]
BOTTOM-LEFT-FRONT  : [ 0.25, -0.25, -0.353553]
BOTTOM-RIGHT-FRONT : [ 0.25,  0.25, -0.353553]

*/