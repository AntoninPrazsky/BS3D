using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Physics;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using Prazsky.Render;
using System;
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

        private Simulation _simulation;
        private ThreadDispatcher _threadDispatcher;
        private BufferPool _bufferPool;

        private List<StaticBody> _staticBodies;
        private List<PhysicsBall[]> _balls;
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
        private static readonly float SHOOT_MULTIPLIER = 200f;

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

        public Testbed(bool windowed = true, int windowWidth = 1280, int windowHeight = 800)
        {
            _windowed = windowed;

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
            _balls = new List<PhysicsBall[]>();

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
                new(mgKeys.D0, PutBallAtZero, "Spawn ball at (0, 0, 0)"),
                new(mgKeys.D1, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Forward, true), "Forward view"),
                new(mgKeys.D2, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Backward, true), "Backward view"),
                new(mgKeys.D3, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Left, true), "Left view"),
                new(mgKeys.D4, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Right, true), "Right view"),
                new(mgKeys.D5, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Up, true), "Up view"),
                new(mgKeys.D6, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Down, true), "Down view"),
                new(mgKeys.R, () => { _cih.RestartCamera(); _cannon.Restart(); }, "Restart camera"),
                new(mgKeys.Space, ShootBall, "Shoot ball")
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

            _eventHandler = new EventHandler(_simulation, _bufferPool, _events, _ceiling, _balls, _shotBalls);
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

            Box box = new(10f, 1f, 10f);
            TypedIndex boxShapeIndex = _simulation.Shapes.Add(box);
            CollidableDescription collidableDescription = new(boxShapeIndex, 0.1f);
            BodyDescription bodyDescription = BodyDescription.CreateKinematic(new System.Numerics.Vector3(0f, 8.363961f, 0f), collidableDescription, new BodyActivityDescription(Constants.HUNDREDTH));

            BodyHandle topBodyHandle = _simulation.Bodies.Add(in bodyDescription);
            BodyReference topBodyReference = new(topBodyHandle, _simulation.Bodies);

            _ceiling = new KinematicBody(_topPlatformModel, topBodyReference, topBodyHandle);
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

            _balls.Add(BallsConstraintsBuilder.BuildBallsStructure(_map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference));
            RecountBallsAndConstraints();
        }

        private void RecountBallsAndConstraints()
        {
            _info.CustomText = "Balls on scene: " + (_simulation.Bodies.ActiveSet.Count);
            _info.CustomText += "\nConstraints count: " + _simulation.Solver.CountConstraints();
        }

        private void PutBallAtZero()
        {
            BallsMap map = new(10, 10, 10, _hrSphere);
            map.PutBallAt(0, 0, 0, BallType.Type1);

            var ball = BallsConstraintsBuilder.BuildBallsStructure(map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference);
            _balls.Add(ball);

            RecountBallsAndConstraints();
        }

        protected override void Update(GameTime gameTime)
        {
            float timeStep = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 60f);
            if (timeStep == 0) timeStep = 1 / 60f;

            if (_simulate)
            {
                #region Contact events

                _events.Flush();

                #endregion

                if (_slowSimulation) _simulation.Timestep(timeStep / 100f, _threadDispatcher);
                else _simulation.Timestep(timeStep, _threadDispatcher);

			}

            if (IsActive)
            {
                _cih.RegisterCurrentInputState();

                foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

                _cih.Update(gameTime);
                _cih.CameraMovement(gameTime, !_gameMode);
                _cih.RegisterPreviousInputState();
            }

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
            if (_balls.Count > 0)
            {
                int ballsCount = _balls.Count;
                for (int x = 0; x < ballsCount; x++)
                {
                    int ballsXLength = _balls[x].Length;
                    for (int i = 0; i < ballsXLength; i++)
                    {
                        _balls[x][i].RemoveAllConstraints(_simulation);
                    }
                }
            }
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

                if (_balls.Count > 0)
                {
                    int ballsCount = _balls.Count;
                    for (int x = 0; x < ballsCount; x++)
                    {
                        int ballsXLength = _balls[x].Length;
                        for (int i = 0; i < ballsXLength; i++)
                        {
                            Microsoft.Xna.Framework.Matrix ballWorldMatrix = Microsoft.Xna.Framework.Matrix.CreateFromQuaternion(
                                new Quaternion(
                                    _balls[x][i].BallReference.Pose.Orientation.X,
                                    _balls[x][i].BallReference.Pose.Orientation.Y,
                                    _balls[x][i].BallReference.Pose.Orientation.Z,
                                    _balls[x][i].BallReference.Pose.Orientation.W))
                                * Microsoft.Xna.Framework.Matrix.CreateTranslation(
                                    _balls[x][i].BallReference.Pose.Position.X,
                                    _balls[x][i].BallReference.Pose.Position.Y,
                                    _balls[x][i].BallReference.Pose.Position.Z);

                            ICamera camera = _camera;
                            BasicEffectParams basicEffectParams = BasicEffectParamsProvider.GetEffectByType(_balls[x][i].Type);

                            ModelRenderer.Render(_hrSphere, _hrSphereTransformations, ref camera, ballWorldMatrix, basicEffectParams, true, true);
                        }
                    }
                }

                if (_shotBalls.Count > 0)
                {
                    int ballsCount = _shotBalls.Count;
                    for (int i = 0; i < ballsCount; i++)
                    {
                        Microsoft.Xna.Framework.Matrix ballWorldMatrix = Microsoft.Xna.Framework.Matrix.CreateFromQuaternion(
                                new Quaternion(
                                    _shotBalls[i].BallReference.Pose.Orientation.X,
                                    _shotBalls[i].BallReference.Pose.Orientation.Y,
                                    _shotBalls[i].BallReference.Pose.Orientation.Z,
                                    _shotBalls[i].BallReference.Pose.Orientation.W))
                                * Microsoft.Xna.Framework.Matrix.CreateTranslation(
                                    _shotBalls[i].BallReference.Pose.Position.X,
                                    _shotBalls[i].BallReference.Pose.Position.Y,
                                    _shotBalls[i].BallReference.Pose.Position.Z);

                        ICamera camera = _camera;
                        BasicEffectParams basicEffectParams = BasicEffectParamsProvider.GetEffectByType(_shotBalls[i].Type);

                        ModelRenderer.Render(_hrSphere, _hrSphereTransformations, ref camera, ballWorldMatrix, basicEffectParams, true, true);
                    }
                }

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
        }

        private void ShootBall()
        {
            var sourcePosition = _gameMode ? _cannon.Position : _camera.Position;
            var shootTarget = _gameMode ? _cannon.AimTarget : _camera.Target;

            _shotBall.Pose.Position = new System.Numerics.Vector3(sourcePosition.X, sourcePosition.Y, sourcePosition.Z);

            var direction = shootTarget - sourcePosition;
            direction.Normalize();
            direction *= SHOOT_MULTIPLIER;

            _shotBall.Velocity.Linear = new System.Numerics.Vector3(direction.X, direction.Y, direction.Z);

            BodyHandle bodyHandle = _simulation.Bodies.Add(_shotBall);

            PhysicsBall ball = new()
            {
                BallReference = new(bodyHandle, _simulation.Bodies),
                Type = BallType.Type4
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
        public List<PhysicsBall[]> Balls;
        public List<PhysicsBall> ShotBalls;

        public EventHandler(Simulation simulation, BufferPool pool, ContactEvents contactEvents, KinematicBody ceiling, List<PhysicsBall[]> balls, List<PhysicsBall> shotBalls)
        {
            Simulation = simulation;
            Pool = pool;
            _contactEvents = contactEvents;
            _ceiling = ceiling;
            Balls = balls;
            ShotBalls = shotBalls;
        }

        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            Vector3 contactOffset, Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            //TODO

#if DEBUG
            Console.WriteLine(" → Ball collided!");
            Console.WriteLine(nameof(eventSource) + " : " + eventSource.ToString());
            Console.WriteLine(nameof(pair.A) + " : " + pair.A.ToString());
            Console.WriteLine(nameof(pair.B) + " : " + pair.B.ToString());
            Console.WriteLine(nameof(contactOffset) + " : " + contactOffset.ToString());
            Console.WriteLine(nameof(contactNormal) + " : " + contactNormal.ToString());
            Console.WriteLine(nameof(depth) + " : " + depth.ToString());
            Console.WriteLine(nameof(featureId) + " : " + featureId.ToString());
            Console.WriteLine(nameof(contactIndex) + " : " + contactIndex.ToString());
            Console.WriteLine(nameof(workerIndex) + " : " + workerIndex.ToString());
            Console.WriteLine();
#endif

            //Once ball touches the ground or ceiling, unregister collision event
            //TODO: This might be possible to do by checking if the Static/Kinematic body is specific object (ground block, ceiling block by BodyReference)
            if (pair.A.Mobility == CollidableMobility.Static || pair.B.Mobility == CollidableMobility.Static ||
                pair.A.Mobility == CollidableMobility.Kinematic || pair.B.Mobility == CollidableMobility.Kinematic)
            {
                if (pair.A.Mobility == CollidableMobility.Dynamic) _contactEvents.Unregister(pair.A.BodyHandle);
                if (pair.B.Mobility == CollidableMobility.Dynamic) _contactEvents.Unregister(pair.B.BodyHandle);
            }

			if (Map == null)
			{
				Console.WriteLine("Map is null\n");
				return;
			}

			if (pair.A.BodyHandle != _ceiling.BodyHandle) return;

			#region Connect ball to the ceiling
#if DEBUG
			Console.WriteLine(" → CEILING HIT");
#endif

            Vector3 allowedPosition = Map.PutBallAtClosestEmptyCeilingPosition(contactOffset);

            if (allowedPosition.X == float.MinValue)
            {
#if DEBUG
                Console.WriteLine("Outside of the ceiling or ceiling space already occupied by another ball");
#endif
                return;
            }

#if DEBUG
            Console.WriteLine("Ball placed at: " + allowedPosition);
#endif

			System.Numerics.Vector3 offsetBall = new(0f, BallsConstraintsBuilder.BALL_RADIUS, 0f);
			System.Numerics.Vector3 offsetCeiling = new(allowedPosition.X, -BallsConstraintsBuilder.BALL_RADIUS, allowedPosition.Z);

            BallSocket ballSocket = new()
            {
                LocalOffsetA = offsetBall,
                LocalOffsetB = offsetCeiling,
                SpringSettings = BallsConstraintsBuilder.SPRING_SETTINGS
            };

            var constraintHandle = Simulation.Solver.Add(pair.B.BodyHandle, _ceiling.BodyHandle, ballSocket);
            
            //take shot physics ball, add constraint to it, and add it to the Balls list

            var physicsBall = ShotBalls.Where(x => x.BallReference.Handle == pair.B.BodyHandle).FirstOrDefault(); //Linq is ok since this list should be short
            if (physicsBall.BallReference.Handle != pair.B.BodyHandle) throw new Exception("This should not happen, investigate why it did.");

            ShotBalls.Remove(physicsBall); //Not shot anymore

            physicsBall.HandlesTop.Handle1 = constraintHandle;

            Balls.Add(new PhysicsBall[] { physicsBall }); //Part of map now

            #endregion
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