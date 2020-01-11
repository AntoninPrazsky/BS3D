using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Camera;
using Prazsky.Render;
using System;
using System.Collections.Generic;
using s = System.Numerics;

namespace Testbed
{
	public class Testbed : Game
	{
		private BasicCamera3D Camera3D;
		private Model _hrSphere, _hrSphereRed;
		private Matrix[] _hrSphereTransformations;

		private Model _groundModel, _groundModel2, _groundModel3;

		private Simulation _simulation;
		private SimpleThreadDispatcher _simpleThreadDispatcher;
		private BufferPool _bufferPool;

		private bool _simulate = true;
		private bool _draw = true;

		private List<StaticBody> _staticBodies;
		private List<PhysicsBall[]> _balls;

		#region Grafika

		private int _windowWidth;
		private int _windowHeight;

		private GraphicsDeviceManager _graphics;
		private bool _windowed;
		private bool _preferHiDef;
		private bool _preferMultiSampling;

		public Info Info { private set; get; }

		#endregion Grafika

		#region Ovládání

		private const float _mouseMovementDenominator = 50f;

		private GamePadState _currentGamePadState = new GamePadState();
		private KeyboardState _currentKeyboardState = new KeyboardState();
		private MouseState _currentMouseState = new MouseState();

		private GamePadState _previousGamePadState = new GamePadState();
		private KeyboardState _previousKeyboardState = new KeyboardState();
		private MouseState _previousMouseState = new MouseState();

		private int _heightHalf;
		private int _widthHalf;
		private bool _mousePanMode = false;
		private bool _mouseRotationMode = false;

		#endregion Ovládání

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

			_staticBodies = new List<StaticBody>();
			_balls = new List<PhysicsBall[]>();

			_bufferPool = new BufferPool();
			_simulation = Simulation.Create(_bufferPool, new Simu.NarrowPhaseCallbacks(), new Simu.DemoPoseIntegratorCallbacks(new s.Vector3(0f, -10f, 0f)));
			_simpleThreadDispatcher = new SimpleThreadDispatcher(Environment.ProcessorCount);

			GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None, MultiSampleAntiAlias = true };
			GraphicsDevice.BlendState = new BlendState() { AlphaSourceBlend = Blend.SourceAlpha, AlphaDestinationBlend = Blend.InverseSourceColor, ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.InverseSourceAlpha };
			GraphicsDevice.PresentationParameters.MultiSampleCount = 4;
			_graphics.ApplyChanges();

			base.Initialize();
		}

		protected override void LoadContent()
		{
			_hrSphere = Content.Load<Model>("HRGeoDome");
			_hrSphereTransformations = new Matrix[_hrSphere.Bones.Count];
			_hrSphere.CopyAbsoluteBoneTransformsTo(_hrSphereTransformations);

			_hrSphereRed = Content.Load<Model>("HRGeoDome");

			#region Ground

			_groundModel3 = Content.Load<Model>("GroundTripleX");

			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(0f, -10f, 0f), 2)));

			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(-30f, -9f, 0f), 2)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(30, -9f, 0f), 2)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(0f, -9f, 30f), 2)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(0f, -9f, -30f), 2)));

			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(-30f, -9f, -30f), 2)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(30, -9f, 30f), 2)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(-30f, -9f, 30f), 2)));
			_staticBodies.Add(new StaticBody(_groundModel3, CreateGround(new s.Vector3(30f, -9f, -30f), 2)));

			#endregion Ground
		}

		private void BallsConstraintsBuilderTest()
		{
			BallsMap map = new BallsMap(10, 10, 10, _hrSphere);

			map.PutBallAt(0, 0, 2, eBallType.Type1);
			map.PutBallAt(0, 1, 2, eBallType.Type2);
			map.PutBallAt(0, 2, 2, eBallType.Type3);
			map.PutBallAt(0, 3, 2, eBallType.Type1);

			map.PutBallAt(1, 0, 2, eBallType.Type2);
			map.PutBallAt(1, 1, 2, eBallType.Type3);
			map.PutBallAt(1, 2, 2, eBallType.Type1);
			map.PutBallAt(1, 3, 2, eBallType.Type1);

			map.PutBallAt(2, 0, 2, eBallType.Type2);
			map.PutBallAt(2, 1, 2, eBallType.Type3);
			map.PutBallAt(2, 2, 2, eBallType.Type1);
			map.PutBallAt(2, 3, 2, eBallType.Type1);

			map.PutBallAt(3, 0, 2, eBallType.Type2);
			map.PutBallAt(3, 1, 2, eBallType.Type3);
			map.PutBallAt(3, 2, 2, eBallType.Type1);
			map.PutBallAt(3, 3, 2, eBallType.Type1);

			map.PutBallAt(0, 0, 3, eBallType.Type1);
			map.PutBallAt(0, 1, 3, eBallType.Type2);
			map.PutBallAt(0, 2, 3, eBallType.Type3);
			map.PutBallAt(0, 3, 3, eBallType.Type1);

			map.PutBallAt(1, 0, 3, eBallType.Type2);
			map.PutBallAt(1, 1, 3, eBallType.Type3);
			map.PutBallAt(1, 2, 3, eBallType.Type1);
			map.PutBallAt(1, 3, 3, eBallType.Type1);

			map.PutBallAt(2, 0, 3, eBallType.Type2);
			map.PutBallAt(2, 1, 3, eBallType.Type3);
			map.PutBallAt(2, 2, 3, eBallType.Type1);
			map.PutBallAt(2, 3, 3, eBallType.Type1);

			map.PutBallAt(3, 0, 3, eBallType.Type2);
			map.PutBallAt(3, 1, 3, eBallType.Type3);
			map.PutBallAt(3, 2, 3, eBallType.Type1);
			map.PutBallAt(3, 3, 3, eBallType.Type1);

			map.PutBallAt(3, 0, 4, eBallType.Type2);
			map.PutBallAt(3, 1, 4, eBallType.Type3);
			map.PutBallAt(3, 2, 4, eBallType.Type1);
			map.PutBallAt(3, 3, 4, eBallType.Type1);

			map.PutBallAt(3, 0, 4, eBallType.Type2);
			map.PutBallAt(3, 1, 4, eBallType.Type3);
			map.PutBallAt(3, 2, 4, eBallType.Type1);
			map.PutBallAt(3, 3, 4, eBallType.Type1);

			map.PutBallAt(4, 5, 9, eBallType.Type2);
			map.PutBallAt(4, 6, 9, eBallType.Type3);

			map.PutBallAt(5, 4, 9, eBallType.Type2);
			map.PutBallAt(5, 6, 9, eBallType.Type1);
			map.PutBallAt(5, 7, 9, eBallType.Type1);

			map.PutBallAt(6, 4, 9, eBallType.Type2);
			map.PutBallAt(6, 5, 9, eBallType.Type3);
			map.PutBallAt(6, 7, 9, eBallType.Type1);

			map.PutBallAt(7, 5, 9, eBallType.Type3);
			map.PutBallAt(7, 6, 9, eBallType.Type1);

			map.PutBallAt(4, 4, 5, eBallType.Type1);
			map.PutBallAt(4, 5, 5, eBallType.Type2);
			map.PutBallAt(4, 6, 5, eBallType.Type3);
			map.PutBallAt(4, 7, 5, eBallType.Type1);

			map.PutBallAt(5, 4, 5, eBallType.Type2);
			map.PutBallAt(5, 5, 5, eBallType.Type3);
			map.PutBallAt(5, 6, 5, eBallType.Type1);
			map.PutBallAt(5, 7, 5, eBallType.Type1);

			map.PutBallAt(6, 4, 5, eBallType.Type2);
			map.PutBallAt(6, 5, 5, eBallType.Type3);
			map.PutBallAt(6, 6, 5, eBallType.Type1);
			map.PutBallAt(6, 7, 5, eBallType.Type1);

			map.PutBallAt(7, 4, 5, eBallType.Type1);
			map.PutBallAt(7, 5, 5, eBallType.Type1);
			map.PutBallAt(7, 6, 5, eBallType.Type1);
			map.PutBallAt(7, 7, 5, eBallType.Type1);

			_balls.Add(BallsConstraintsBuilder.BuildBallsStructure(map.GetStaticBallsArray(), ref _simulation));

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
				_currentKeyboardState = Keyboard.GetState();
				_currentGamePadState = GamePad.GetState(PlayerIndex.One);
				_currentMouseState = Mouse.GetState();

				if (PressedOnce(Keys.Escape, Buttons.Back)) Exit();

				CameraMovement(gameTime);

				if (PressedOnce(Keys.F12, Buttons.Start)) Info.Visible = !Info.Visible;
				if (PressedOnce(Keys.Back, Buttons.B)) _simulate = !_simulate;
				if (PressedOnce(Keys.M, Buttons.X)) _draw = !_draw;
				if (PressedOnce(Keys.B, Buttons.A)) BallsConstraintsBuilderTest();

				_previousKeyboardState = _currentKeyboardState;
				_previousGamePadState = _currentGamePadState;
				_previousMouseState = _currentMouseState;
			}

			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.AliceBlue);
			GraphicsDevice.BlendState = BlendState.AlphaBlend;
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;

			if (_draw)
			{
				for (int i = 0; i < _staticBodies.Count; i++)
					_staticBodies[i].Draw(Camera3D);

				if (_balls.Count > 0)
				{
					for (int x = 0; x < _balls.Count; x++)
					{
						for (int i = 0; i < _balls[x].Length; i++)
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

		private void CameraMovement(GameTime gameTime)
		{
			//Debug.WriteLine(Camera3D.Position.ToString() + " " + Camera3D.Target.ToString());

			#region kompletní ovládání kamery gamepadem

			if (_currentGamePadState.IsConnected)
			{
				float Z = 0f;
				if (_currentGamePadState.Triggers.Right > 0) Z = -_currentGamePadState.Triggers.Right;
				if (_currentGamePadState.Triggers.Left > 0) Z = _currentGamePadState.Triggers.Left;

				Camera3D.Move(
						_currentGamePadState.ThumbSticks.Left.X,
						_currentGamePadState.ThumbSticks.Left.Y,
						Z, gameTime);
				Camera3D.Rotate(
						_currentGamePadState.ThumbSticks.Right.Y,
						-_currentGamePadState.ThumbSticks.Right.X,
						gameTime);
			}

			#endregion kompletní ovládání kamery gamepadem

			#region ovládání kamery klávesnicí

			float speed = 1f;
			if (Keyboard.GetState().IsKeyDown(Keys.LeftShift)) speed = 3f;

			if (Keyboard.GetState().IsKeyDown(Keys.W))
				Camera3D.Move(0, 0f, -speed, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.S))
				Camera3D.Move(0, 0f, speed, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.A))
				Camera3D.Move(-speed, 0f, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.D))
				Camera3D.Move(speed, 0f, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.E))
				Camera3D.Move(0f, speed, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.Q))
				Camera3D.Move(0f, -speed, 0f, gameTime);

			#endregion ovládání kamery klávesnicí

			#region ovládání kamery myší

			if (PressedOnceMouse(leftButton: false, middleButton: false, rightButton: true))
			{
				CenterMouse();
				_mouseRotationMode = !_mouseRotationMode;
				return;
			}

			if (_currentMouseState.RightButton == ButtonState.Pressed)
				_mousePanMode = true;

			IsMouseVisible = !_mousePanMode && !_mouseRotationMode;

			if (_mouseRotationMode || _mousePanMode)
			{
				float mDeltaA = 0f;
				float mDeltaB = 0f;

				if (_currentMouseState.X != _widthHalf)
					mDeltaB = -(_currentMouseState.X - _widthHalf) / _mouseMovementDenominator;

				if (_currentMouseState.Y != _heightHalf)
					mDeltaA = -(_currentMouseState.Y - _heightHalf) / _mouseMovementDenominator;

				CenterMouse();

				if (_mouseRotationMode && !_mousePanMode)
					Camera3D.Rotate(mDeltaA, mDeltaB, gameTime);

				if (_currentMouseState.RightButton == ButtonState.Pressed)
				{
					_mousePanMode = true;
					Camera3D.Move(-mDeltaB, mDeltaA, 0f, gameTime);
				}
				else
					_mousePanMode = false;
			}

			#endregion ovládání kamery myší
		}

		private bool PressedOnce(Keys key, Buttons button)
		{
			return InputHelper.PressedOnce(
					key,
					button,
					_currentKeyboardState,
					_currentGamePadState,
					_previousKeyboardState,
					_previousGamePadState);
		}

		private bool PressedOnceMouse(bool leftButton, bool middleButton, bool rightButton)
		{
			return InputHelper.PressedOnce(
					leftButton,
					middleButton,
					rightButton,
					_currentMouseState,
					_previousMouseState);
		}

		private void SetGraphics(bool windowed = false)
		{
			_graphics.PreferredBackBufferWidth = windowed ? _windowWidth : 3840; //GraphicsDevice.DisplayMode.Width
			_graphics.PreferredBackBufferHeight = windowed ? _windowHeight : 1600; //GraphicsDevice.DisplayMode.Height
			_graphics.IsFullScreen = !windowed;

			_graphics.SynchronizeWithVerticalRetrace = true;

			_graphics.PreferMultiSampling = _preferMultiSampling;

			_graphics.ApplyChanges();

			IsMouseVisible = false;
			IsFixedTimeStep = false;

			_widthHalf = Window.ClientBounds.Width / 2;
			_heightHalf = Window.ClientBounds.Height / 2;

			CenterMouse();
		}

		private void CenterMouse()
		{
			Mouse.SetPosition(_widthHalf, _heightHalf);
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

		private StaticReference CreateGround(s.Vector3 position, byte size)
		{
			float lenght = 0f;

			switch (size)
			{
				case (0):
					lenght = 10f;
					break;

				case (1):
					lenght = 20f;
					break;

				case (2):
					lenght = 30f;
					break;

				default: throw new NotImplementedException("Unknow Ground Size: " + size);
			}

			return new StaticReference(_simulation.Statics.Add(
				new StaticDescription(
					position, new CollidableDescription(
						_simulation.Shapes.Add(
							new Box(lenght, 1f, lenght)), 0.1f))),
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