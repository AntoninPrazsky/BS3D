using MapEditor.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Helpers;
using Prazsky.Core.Camera;
using System;
using System.Diagnostics;

namespace MapEditor
{
	public class MapEditor : Game
	{
		private BasicCamera3D Camera3D;
		private Model _hrSphere;

		private BallsMap _map;
		private Selector _selector;

		private bool _draw = true;

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

		public MapEditor(
			bool windowed = true,
			bool preferHiDef = true,
			bool preferMultiSampling = true,
			int windowWidth = 1280,
			int windowHeight = 720)
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

			Camera3D = new BasicCamera3D(new Vector3(0f, 0f, 30f), GraphicsDevice.Viewport.AspectRatio);
			Info = new Info(this) { DrawOrder = int.MaxValue };
			Components.Add(Info);

			GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None, MultiSampleAntiAlias = true };
			GraphicsDevice.BlendState = new BlendState() { AlphaSourceBlend = Blend.SourceAlpha, AlphaDestinationBlend = Blend.InverseSourceColor, ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.InverseSourceAlpha };
			GraphicsDevice.PresentationParameters.MultiSampleCount = 4;
			_graphics.ApplyChanges();

			_cih = new CameraInputHelper(Camera3D, this);
			this.Components.Add(_cih);

			base.Initialize();
		}

		protected override void LoadContent()
		{
			_hrSphere = Content.Load<Model>("HRGeoDome");

			_map = new BallsMap(10, 10, 10, _hrSphere);
			_selector = new Selector(Content, _map);
		}

		private void SelectorControl()
		{
			if (_cih.PressedOnce(Keys.Up, Buttons.DPadUp)) _selector.Move(Vector3.Forward);
			if (_cih.PressedOnce(Keys.Down, Buttons.DPadDown)) _selector.Move(Vector3.Backward);
			if (_cih.PressedOnce(Keys.Left, Buttons.DPadLeft)) _selector.Move(Vector3.Left);
			if (_cih.PressedOnce(Keys.Right, Buttons.DPadRight)) _selector.Move(Vector3.Right);
			if (_cih.PressedOnce(Keys.PageUp, Buttons.RightShoulder)) _selector.Move(Vector3.Up);
			if (_cih.PressedOnce(Keys.PageDown, Buttons.LeftShoulder)) _selector.Move(Vector3.Down);

			if (_cih.PressedOnce(Keys.Space, Buttons.A)) _selector.PutBall();
			if (_cih.PressedOnce(Keys.Delete, Buttons.B)) _selector.RemoveBall();

			if (_cih.PressedOnce(Keys.NumPad1)) _selector.ChangeBallType(eBallType.Type1);
			if (_cih.PressedOnce(Keys.NumPad2)) _selector.ChangeBallType(eBallType.Type2);
			if (_cih.PressedOnce(Keys.NumPad3)) _selector.ChangeBallType(eBallType.Type3);
		}

		protected override void Update(GameTime gameTime)
		{
			if (this.IsActive)
			{
				_cih.RegisterCurrentInputState();

				if (_cih.PressedOnce(Keys.Escape, Buttons.Back)) Exit();

				SelectorControl();

				_cih.CameraMovement(gameTime);

				if (_cih.PressedOnce(Keys.F12, Buttons.Start)) Info.Visible = !Info.Visible;
				if (_cih.PressedOnce(Keys.M, Buttons.X)) _draw = !_draw;

				if (_cih.PressedOnce(Keys.N)) FullMapTest();

				if (_cih.PressedOnce(Keys.D1)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Forward, true);
				if (_cih.PressedOnce(Keys.D2)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Backward, true);
				if (_cih.PressedOnce(Keys.D3)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Left, true);
				if (_cih.PressedOnce(Keys.D4)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Right, true);
				if (_cih.PressedOnce(Keys.D5)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Up, true);
				if (_cih.PressedOnce(Keys.D6)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Down, true);

				if (_cih.PressedOnce(Keys.R)) _cih.RestartCamera();

				if (_cih.PressedOnce(Keys.F1))
				{
					Stopwatch stopwatch = new Stopwatch();
					stopwatch.Start();
					_map.SerializeAsBinary(@"G:\balls.bin");
					stopwatch.Stop();
					Console.WriteLine($"Serialize Binary (ms): {stopwatch.ElapsedMilliseconds}");
				}

				if (_cih.PressedOnce(Keys.F2))
				{
					Stopwatch stopwatch = new Stopwatch();
					stopwatch.Start();
					_map.DeserializeBinary(@"G:\balls.bin");
					stopwatch.Stop();
					Console.WriteLine($"Deserialize Binary (ms): {stopwatch.ElapsedMilliseconds}");
				}

				_cih.RegisterPreviousInputState();
			}
			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.MidnightBlue);
			GraphicsDevice.BlendState = BlendState.AlphaBlend;
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;

			if (_draw && _map != null)
			{
				_map.Draw(Camera3D);
			}

			if (_selector != null)
			{
				GraphicsDevice.BlendState = BlendState.Additive;
				GraphicsDevice.DepthStencilState = DepthStencilState.None;

				_selector.Draw(Camera3D);
			}

			base.Draw(gameTime);
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
		}

		private void _graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
		{
			//Obnovovací frekvence vykreslování
			e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = PresentInterval.Default;

			if (_preferHiDef && e.GraphicsDeviceInformation.Adapter.IsProfileSupported(GraphicsProfile.HiDef))
				e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
			else
				e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.Reach;
		}

		protected override void UnloadContent()
		{
		}

		#region Map Tests

		private void FullMapTest()
		{
			byte sizeX = 10;
			byte sizeZ = 10;
			byte levels = 10;

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			_map = new BallsMap(sizeX, sizeZ, levels, _hrSphere);

			Array ballTypes = Enum.GetValues(typeof(eBallType));
			Random random = new Random();

			eBallType currentBallType = eBallType.Type1;
			eBallType lastBallType = eBallType.Type1;

			for (byte x = 0; x < sizeX; x++)
				for (byte z = 0; z < sizeZ; z++)
					for (byte l = 0; l < levels; l++)
					{
						do
						{
							currentBallType = (eBallType)ballTypes.GetValue(random.Next(ballTypes.Length));
						} while (currentBallType == lastBallType);

						_map.PutBallAt(x, z, l, currentBallType);

						lastBallType = currentBallType;
					}

			Info.CustomText = "Balls in map: " + _map.GetBallsCount();

			stopwatch.Stop();
			Console.WriteLine($"Single thread build took: {stopwatch.ElapsedMilliseconds} ms");

			_selector.UpdateBallsBap(_map);
		}

		#endregion Map Tests
	}
}