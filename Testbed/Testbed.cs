using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Physics;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static Testbed.Simu;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Testbed
{
    public class Testbed : Game
    {
        private static readonly float EARTH_GRAVITY = -9.807f;

        private BasicCamera3D Camera3D;
        private Model _hrSphere;
        private Microsoft.Xna.Framework.Matrix[] _hrSphereTransformations;

        private Model _groundModel3, _topPlatform;
        private KinematicBody _ceiling;

        private Simulation _simulation;
        private ThreadDispatcher _threadDispatcher;
        private BufferPool _bufferPool;

        private bool _simulate = true;
        private bool _draw = true;

        private List<StaticBody> _staticBodies;
        private List<PhysicsBall[]> _balls;

        private CameraInputHelper _cih;

        private SkyDome _sky;
        private Model _skyModel;

        private ButtonAction[] _actions;

        #region Graphics

        private int _windowWidth;
        private int _windowHeight;

        private GraphicsDeviceManager _graphics;
        private bool _windowed;

        private TextInfoRenderer _info;

        #endregion Graphics

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

		private void Window_ClientSizeChanged(object sender, EventArgs e) => Camera3D.AspectRatio = GraphicsDevice.Viewport.AspectRatio;
		

		protected override void Initialize()
        {
            IsMouseVisible = true;

            Camera3D = new BasicCamera3D(new Vector3(0f, -3f, 30f), GraphicsDevice.Viewport.AspectRatio);
            _info = new TextInfoRenderer(this, "Content/Fonts/cascadia") { DrawOrder = int.MaxValue };
            Components.Add(_info);

            _cih = new CameraInputHelper(Camera3D, this);
            _cih.CameraOffset = -30f;

            _staticBodies = new List<StaticBody>();
            _balls = new List<PhysicsBall[]>();

            _bufferPool = new BufferPool();
            _simulation = Simulation.Create(
                _bufferPool,
                new NarrowPhaseCallbacks(),
                new PoseIntegratorCallbacks(new System.Numerics.Vector3(0, EARTH_GRAVITY, 0)),
                new SolveDescription(8, 1));

            _threadDispatcher = new ThreadDispatcher(Environment.ProcessorCount);

            #region Controls

            _actions = new ButtonAction[]
            {
                new(mgKeys.Escape, Buttons.Back, Exit, "Exit"),
                new(mgKeys.F12, () => _info.Visible = !_info.Visible, "Hide/show text overlay"),
                new(mgKeys.F5, Buttons.B, () => _simulate = !_simulate, "Stop/start simulation"),
                new(mgKeys.F6, Buttons.X, () => _draw = !_draw, "Hide/show 3D rendering"),
                new(mgKeys.F2, Buttons.DPadLeft, LoadBallsMapTest, "Load map"),
                new(mgKeys.Delete, Buttons.Start, RemoveAllConstraints, "Remove all constraints"),
                new(mgKeys.NumPad1, SwitchSkyDome, "Switch sky dome"),
                new(mgKeys.D0, PutBallAtZero, "Spawn ball at (0, 0, 0)"),
                new(mgKeys.D1, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Forward, true), "Forward view"),
				new(mgKeys.D2, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Backward, true), "Backward view"),
				new(mgKeys.D3, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Left, true), "Left view"),
				new(mgKeys.D4, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Right, true), "Right view"),
				new(mgKeys.D5, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Up, true), "Up view"),
				new(mgKeys.D6, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Down, true), "Down view"),
				new(mgKeys.R, () => _cih.RestartCamera(), "Restart camera"),
			};

			StringBuilder builder = new();
			foreach (var act in _actions) builder.Append(string.Format("{0,-9} {1}\n", act.Key.ToString(), act.Description));

			_info.HintText = builder.ToString();


			#endregion

			base.Initialize();
        }

        protected override void LoadContent()
        {
            _hrSphere = Content.Load<Model>("HRGeoDome");
            _hrSphereTransformations = new Microsoft.Xna.Framework.Matrix[_hrSphere.Bones.Count];
            _hrSphere.CopyAbsoluteBoneTransformsTo(_hrSphereTransformations);

            #region Ground and ceiling

            _groundModel3 = Content.Load<Model>("GroundTripleX");
            _topPlatform = Content.Load<Model>("TopGrid");

            BuildGroundAndCeiling();

            #endregion Ground and ceiling

            _skyModel = Content.Load<Model>("Skyes\\SkyDome" + _skyModelNumber);
            _sky = new SkyDome(_skyModel, GraphicsDevice);
        }

        private byte _skyModelNumber = 1;

        private void SwitchSkyDome()
        {
            if (_skyModelNumber == 18) _skyModelNumber = default;

            _skyModelNumber++;
            _skyModel = Content.Load<Model>("Skyes\\SkyDome" + _skyModelNumber);
            _sky.SkyDomeModel = _skyModel;
        }

        private void BuildGroundAndCeiling()
        {
            Box groundBox = new(30f, 1f, 30f);

            _staticBodies.Add(new(_groundModel3, CreateStatic(new(0f, -10f, 0f), groundBox)));

            _staticBodies.Add(new(_groundModel3, CreateStatic(new(-30f, -9f, 0f), groundBox)));
            _staticBodies.Add(new(_groundModel3, CreateStatic(new(30, -9f, 0f), groundBox)));
            _staticBodies.Add(new(_groundModel3, CreateStatic(new(0f, -9f, 30f), groundBox)));
            _staticBodies.Add(new(_groundModel3, CreateStatic(new(0f, -9f, -30f), groundBox)));

            _staticBodies.Add(new(_groundModel3, CreateStatic(new(-30f, -9f, -30f), groundBox)));
            _staticBodies.Add(new(_groundModel3, CreateStatic(new(30, -9f, 30f), groundBox)));
            _staticBodies.Add(new(_groundModel3, CreateStatic(new(-30f, -9f, 30f), groundBox)));
            _staticBodies.Add(new(_groundModel3, CreateStatic(new(30f, -9f, -30f), groundBox)));

            Box box = new Box(10f, 1f, 10f);
            TypedIndex boxShapeIndex = _simulation.Shapes.Add(box);
            CollidableDescription collidableDescription = new CollidableDescription(boxShapeIndex, 0.1f);
            BodyDescription bodyDescription = BodyDescription.CreateKinematic(new System.Numerics.Vector3(0f, 8.363961f, 0f), collidableDescription, new BodyActivityDescription(0.01f));

            BodyHandle topBodyHandle = _simulation.Bodies.Add(in bodyDescription);
            BodyReference topBodyReference = new BodyReference(topBodyHandle, _simulation.Bodies);

            _ceiling = new KinematicBody(_topPlatform, topBodyReference);
        }

        private void LoadBallsMapTest()
        {
            var filePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                openFileDialog.Filter = "Levels (*.bin)|*.bin";
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
			BallsMap map = new(filePath, _hrSphere);

			map.Center();

			_balls.Add(BallsConstraintsBuilder.BuildBallsStructure(map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference));
			_info.CustomText = "Balls on scene: " + CountActiveBalls();
			_info.CustomText += "\nConstraints count: " + _simulation.Solver.CountConstraints();
		}

        private void PutBallAtZero()
        {
            BallsMap map = new BallsMap(10, 10, 10, _hrSphere);
            map.PutBallAt(0, 0, 0, eBallType.Type1);
            _balls.Add(BallsConstraintsBuilder.BuildBallsStructure(map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference));
        }

        private int CountActiveBalls()
        {
            int result = 0;
            for (int i = 0; i < _balls.Count; i++) result += _balls[i].Length;
            return result;
        }

        protected override void Update(GameTime gameTime)
        {
            if (_simulate)
            {
                float timeStep = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1 / 60f);
                if (timeStep == 0) timeStep = 1 / 60f;

                //timeStep = timeStep / 5f; //Slow down simulation
                _simulation.Timestep(timeStep, _threadDispatcher);
            }

            if (IsActive)
            {
                _cih.RegisterCurrentInputState();

                foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

                _cih.Update(gameTime);
                _cih.CameraMovement(gameTime);
                _cih.RegisterPreviousInputState();
            }

            _cih.MouseMovementDenominator = 5000f / _info.CurrentFPS; //Higher FPS → lower number

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

            _sky.Draw(Camera3D);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //TODO: GameManager for drawing balls
            if (_draw)
            {
                for (int i = 0; i < _staticBodies.Count; i++)
                    _staticBodies[i].Draw(Camera3D);

                _ceiling.Draw(Camera3D);

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

                            ICamera camera = Camera3D;
                            BasicEffectParams basicEffectParams = BasicEffectParamsProvider.GetEffectByType(_balls[x][i].Type);

                            ModelRenderer.Render(_hrSphere, _hrSphereTransformations, ref camera, ballWorldMatrix, basicEffectParams, true, true);
                        }
                    }
                }
            }

            base.Draw(gameTime);
        }

        private void SetGraphics(bool windowed = false)
        {
            DisplaySettings.DEVMODE devMode = default;
            devMode.dmSize = (short)Marshal.SizeOf(devMode);
            DisplaySettings.EnumDisplaySettings(null, -1, ref devMode);

            int mainScreenWidth = devMode.dmPelsWidth;
            int mainScreenHeight = devMode.dmPelsHeight;

            _graphics.PreferredBackBufferWidth = windowed ? _windowWidth : mainScreenWidth;
            _graphics.PreferredBackBufferHeight = windowed ? _windowHeight : mainScreenHeight;
            _graphics.IsFullScreen = !windowed;

            _graphics.SynchronizeWithVerticalRetrace = true;

            _graphics.ApplyChanges();

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void Graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = PresentInterval.Immediate; //Default
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 16;
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
    }
}