using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Helpers;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Camera;
using Prazsky.Render;
using PyramidGenerator;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using s = System.Numerics;

namespace Testbed
{
	public class Testbed : Game
	{
		private BasicCamera3D Camera3D;
		private Model _hrSphere, _hrSphereRed;
		private Matrix[] _hrSphereTransformations;

		private Model _groundModel3, _topPlatform;
		private KinematicBody _ceiling;

		private Simulation _simulation;
		private SimpleThreadDispatcher _simpleThreadDispatcher;
		private BufferPool _bufferPool;

		private bool _simulate = true;
		private bool _draw = true;

		private List<StaticBody> _staticBodies;
		private List<PhysicsBall[]> _balls;

		private CameraInputHelper _cih;

		#region Grafika

		private int _windowWidth;
		private int _windowHeight;

		private GraphicsDeviceManager _graphics;
		private bool _windowed;
		private bool _preferHiDef;
		private bool _preferMultiSampling;

		public Info Info { private set; get; }

		#endregion Grafika

		public Testbed(
			bool windowed = true,
			bool preferHiDef = true,
			bool preferMultiSampling = true,
			int windowWidth = 1920,
			int windowHeight = 1080)
		{
			_windowed = windowed;
			_preferHiDef = preferHiDef;
			_preferMultiSampling = preferMultiSampling;

			_graphics = new GraphicsDeviceManager(this);

			_graphics.PreparingDeviceSettings += _graphics_PreparingDeviceSettings;
			Content.RootDirectory = "Content";

			_windowWidth = windowWidth;
			_windowHeight = windowHeight;

			SetGraphics(_windowed);
		}

		protected override void Initialize()
		{
			IsMouseVisible = true;

			Camera3D = new BasicCamera3D(new Vector3(0f, -3f, 30f), GraphicsDevice.Viewport.AspectRatio);
			Info = new Info(this) { DrawOrder = int.MaxValue };
			Components.Add(Info);

			_cih = new CameraInputHelper(Camera3D, this);

			_staticBodies = new List<StaticBody>();
			_balls = new List<PhysicsBall[]>();

			_bufferPool = new BufferPool();
			_simulation = Simulation.Create(_bufferPool, new Simu.NarrowPhaseCallbacks(), new Simu.DemoPoseIntegratorCallbacks(new s.Vector3(0f, -10f, 0f)));
			_simpleThreadDispatcher = new SimpleThreadDispatcher(Environment.ProcessorCount);

			base.Initialize();
		}

		protected override void LoadContent()
		{
			_hrSphere = Content.Load<Model>("HRGeoDome");
			_hrSphereTransformations = new Matrix[_hrSphere.Bones.Count];
			_hrSphere.CopyAbsoluteBoneTransformsTo(_hrSphereTransformations);

			_hrSphereRed = Content.Load<Model>("HRGeoDome");

			#region Ground and ceiling

			_groundModel3 = Content.Load<Model>("GroundTripleX");
			_topPlatform = Content.Load<Model>("TopPlatform");

			BuildGroundAndCeiling();

			#endregion Ground and ceiling
		}

		private void BuildGroundAndCeiling()
		{
			Box groundBox = new Box(30f, 1f, 30f);

			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(0f, -10f, 0f), groundBox)));

			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(-30f, -9f, 0f), groundBox)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(30, -9f, 0f), groundBox)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(0f, -9f, 30f), groundBox)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(0f, -9f, -30f), groundBox)));

			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(-30f, -9f, -30f), groundBox)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(30, -9f, 30f), groundBox)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(-30f, -9f, 30f), groundBox)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateStatic(new s.Vector3(30f, -9f, -30f), groundBox)));

			Box box = new Box(10f, 1f, 10f);
			TypedIndex boxShapeIndex = _simulation.Shapes.Add(box);
			CollidableDescription collidableDescription = new CollidableDescription(boxShapeIndex, 0.1f);
			BodyDescription bodyDescription = BodyDescription.CreateKinematic(new s.Vector3(0f, 3.5f, 0f), collidableDescription, new BodyActivityDescription(0.01f));

			BodyHandle topBodyHandle = _simulation.Bodies.Add(in bodyDescription);
			BodyReference topBodyReference = new BodyReference(topBodyHandle, _simulation.Bodies);

			_ceiling = new KinematicBody(_topPlatform, topBodyReference);
		}

		private void LoadBallsMapTest()
		{
			BallsMap map = new BallsMap(@"G:\balls.bin", _hrSphere); //TODO: Okno systému pro otevření? Anebo všechny v adresáři a cyklovat?

			map.Center();

			_balls.Add(BallsConstraintsBuilder.BuildBallsStructure(map.GetStaticBallsArray(), ref _simulation, _ceiling.BodyReference));
			Info.CustomText = "Balls on scene: " + CountActiveBalls();
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

				//timeStep = timeStep / 5f; //Zpomalení simulace
				_simulation.Timestep(timeStep, _simpleThreadDispatcher);
			}

			if (this.IsActive)
			{
				_cih.RegisterCurrentInputState();

				if (_cih.PressedOnce(Keys.Escape, Buttons.Back)) Exit();

				_cih.CameraMovement(gameTime);

				//if (_cih.PressedOnce(Keys.F12, Buttons.Start)) Info.Visible = !Info.Visible;
				if (_cih.PressedOnce(Keys.Back, Buttons.B)) _simulate = !_simulate;
				if (_cih.PressedOnce(Keys.M, Buttons.X)) _draw = !_draw;
				//if (_cih.PressedOnce(Keys.B, Buttons.DPadRight)) BallsConstraintsBuilderTest();
				if (_cih.PressedOnce(Keys.F2, Buttons.DPadLeft)) LoadBallsMapTest();

				if (_cih.PressedOnce(Keys.Delete, Buttons.Start))
				{
					RemoveAllConstraints();
				}

				if (_cih.PressedOnce(Keys.D1)) _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Forward);
				if (_cih.PressedOnce(Keys.D2)) _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Backward);
				if (_cih.PressedOnce(Keys.D3)) _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Left);
				if (_cih.PressedOnce(Keys.D4)) _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Right);
				if (_cih.PressedOnce(Keys.D5)) _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Up);
				if (_cih.PressedOnce(Keys.D6)) _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Down);

				_cih.RegisterPreviousInputState();
			}

			base.Update(gameTime);
		}

		private void RemoveAllStatics()
		{
			foreach (var staticBody in _staticBodies)
			{
				_simulation.Statics.Remove(staticBody.StaticReference.Handle);
			}
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
			GraphicsDevice.BlendState = BlendState.AlphaBlend;
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;

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
							Matrix ballWorldMatrix = Matrix.CreateFromQuaternion(
								new Quaternion(
									_balls[x][i].BallReference.Pose.Orientation.X,
									_balls[x][i].BallReference.Pose.Orientation.Y,
									_balls[x][i].BallReference.Pose.Orientation.Z,
									_balls[x][i].BallReference.Pose.Orientation.W))
								* Matrix.CreateTranslation(
									_balls[x][i].BallReference.Pose.Position.X,
									_balls[x][i].BallReference.Pose.Position.Y,
									_balls[x][i].BallReference.Pose.Position.Z);

							ICamera camera = Camera3D; //Tak sem debil nebo jo
							ModelRenderer.Render(_hrSphere, _hrSphereTransformations, ref camera, ballWorldMatrix, BasicEffectParamsProvider.GetEffectByType(_balls[x][i].Type), true, true);
						}
					}
				}
			}
			base.Draw(gameTime);
		}

		private void SetGraphics(bool windowed = false)
		{
			const int ENUM_CURRENT_SETTINS = -1;
			DisplaySettings.DEVMODE devMode = default;
			devMode.dmSize = (short)Marshal.SizeOf(devMode);
			DisplaySettings.EnumDisplaySettings(null, ENUM_CURRENT_SETTINS, ref devMode);

			int mainScreenWidth = devMode.dmPelsWidth;
			int mainScreenHeight = devMode.dmPelsHeight;

			_graphics.PreferredBackBufferWidth = windowed ? _windowWidth : mainScreenWidth;
			_graphics.PreferredBackBufferHeight = windowed ? _windowHeight : mainScreenHeight;
			_graphics.IsFullScreen = !windowed;

			_graphics.SynchronizeWithVerticalRetrace = true;
			_graphics.PreferMultiSampling = _preferMultiSampling;
			_graphics.ApplyChanges();

			IsMouseVisible = false;
			IsFixedTimeStep = false;
		}

		private void _graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
		{
			//Obnovovací frekvence vykreslování
			e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = PresentInterval.Default; //Default

			if (_preferHiDef && e.GraphicsDeviceInformation.Adapter.IsProfileSupported(GraphicsProfile.HiDef))
				e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
			else
				e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.Reach;
		}

		private StaticReference CreateStatic(s.Vector3 position, Box boundingBox)
		{
			return new StaticReference(_simulation.Statics.Add(
				new StaticDescription(
					position, new CollidableDescription(
						_simulation.Shapes.Add(boundingBox), 0.1f))),
							_simulation.Statics);
		}

		protected override void UnloadContent()
		{
			_simulation.Dispose();
			_simpleThreadDispatcher.Dispose();
			_bufferPool.Clear();
		}
	}
}