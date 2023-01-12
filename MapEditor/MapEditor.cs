using MapEditor.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Helpers;
using Prazsky.Core.Camera;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using mgKeys = Microsoft.Xna.Framework.Input.Keys;

namespace MapEditor
{
    public class MapEditor : Game
    {
        private BasicCamera3D Camera3D;
        private Model _hrSphere;

        private BallsMap _map;
        private Selector _selector;
        private AABB _aabb;

        private bool _draw = true;

        private CameraInputHelper _cih;

        #region Graphics

        private int _windowWidth;
        private int _windowHeight;

        private GraphicsDeviceManager _graphics;
        private bool _windowed;

        public Info Info { private set; get; }

        #endregion Graphics

        public MapEditor(
            bool windowed = true,
            int windowWidth = 1920,
            int windowHeight = 1080)
        {
            _windowed = windowed;

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

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _hrSphere = Content.Load<Model>("HRGeoDome");

            _map = new BallsMap(10, 10, 10, _hrSphere);
            _selector = new Selector(Content, _map);
            _aabb = new AABB(Content);
        }

        private void SelectorControl()
        {
            if (_cih.PressedOnce(mgKeys.Up, Buttons.DPadUp)) _selector.Move(Vector3.Forward);
            if (_cih.PressedOnce(mgKeys.Down, Buttons.DPadDown)) _selector.Move(Vector3.Backward);
            if (_cih.PressedOnce(mgKeys.Left, Buttons.DPadLeft)) _selector.Move(Vector3.Left);
            if (_cih.PressedOnce(mgKeys.Right, Buttons.DPadRight)) _selector.Move(Vector3.Right);
            if (_cih.PressedOnce(mgKeys.PageUp, Buttons.RightShoulder)) _selector.Move(Vector3.Up);
            if (_cih.PressedOnce(mgKeys.PageDown, Buttons.LeftShoulder)) _selector.Move(Vector3.Down);

            if (_cih.PressedOnce(mgKeys.Space, Buttons.A)) _selector.PutBall();
            if (_cih.PressedOnce(mgKeys.Delete, Buttons.B)) _selector.RemoveBall();

            if (_cih.PressedOnce(mgKeys.NumPad1)) _selector.ChangeBallType(eBallType.Type1);
            if (_cih.PressedOnce(mgKeys.NumPad2)) _selector.ChangeBallType(eBallType.Type2);
            if (_cih.PressedOnce(mgKeys.NumPad3)) _selector.ChangeBallType(eBallType.Type3);
        }

        protected override void Update(GameTime gameTime)
        {
            if (this.IsActive)
            {
                _cih.RegisterCurrentInputState();

                if (_cih.PressedOnce(mgKeys.Escape, Buttons.Back)) Exit();

                SelectorControl();

                _cih.CameraMovement(gameTime);

                if (_cih.PressedOnce(mgKeys.F12, Buttons.Start)) Info.Visible = !Info.Visible;
                if (_cih.PressedOnce(mgKeys.M, Buttons.X)) _draw = !_draw;

                if (_cih.PressedOnce(mgKeys.N)) FullMapTest();

                if (_cih.PressedOnce(mgKeys.D1)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Forward);
                if (_cih.PressedOnce(mgKeys.D2)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Backward);
                if (_cih.PressedOnce(mgKeys.D3)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Left);
                if (_cih.PressedOnce(mgKeys.D4)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Right);
                if (_cih.PressedOnce(mgKeys.D5)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Up);
                if (_cih.PressedOnce(mgKeys.D6)) _cih.CenterCameraToMapCenter(_map.GetStaticBallsMapCenter(), Vector3.Down);

                if (_cih.PressedOnce(mgKeys.R)) _cih.RestartCamera();

                if (_cih.PressedOnce(mgKeys.F1))
                {
                    string filePath = GetFilePathByDialog(true);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        _map.SerializeAsBinary(filePath);
                        stopwatch.Stop();
                        Console.WriteLine($"Serialize Binary (ms): {stopwatch.ElapsedMilliseconds}");
                    }
                }

                if (_cih.PressedOnce(mgKeys.F2))
                {
                    string filePath = GetFilePathByDialog(false);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        _map.DeserializeBinary(filePath);
                        stopwatch.Stop();
                        Console.WriteLine($"Deserialize Binary (ms): {stopwatch.ElapsedMilliseconds}");
                    }
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

            _aabb.Draw(Camera3D);

            if (_selector != null)
            {
                GraphicsDevice.BlendState = BlendState.Additive;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                _selector.Draw(Camera3D);
            }

            base.Draw(gameTime);
        }

        private string GetFilePathByDialog(bool save)
        {
            string result = string.Empty;

            if (save)
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                    saveFileDialog.Filter = "Levels (*.bin)|*.bin";
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        result = saveFileDialog.FileName;
                    }
                }

                return result;
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                openFileDialog.Filter = "Levels (*.bin)|*.bin";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    result = openFileDialog.FileName;
                }
            }

            return result;
        }

        private void SetGraphics(bool windowed = false)
        {
            _graphics.PreferredBackBufferWidth = windowed ? _windowWidth : 3840; //GraphicsDevice.DisplayMode.Width
            _graphics.PreferredBackBufferHeight = windowed ? _windowHeight : 1600; //GraphicsDevice.DisplayMode.Height
            _graphics.IsFullScreen = !windowed;

            _graphics.SynchronizeWithVerticalRetrace = true;
            _graphics.PreferMultiSampling = true;

            _graphics.ApplyChanges();

            IsMouseVisible = false;
            IsFixedTimeStep = false;
        }

        private void _graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval = PresentInterval.Default;
            e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 16;
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
			eBallType lastBallType = eBallType.Type1;

			for (byte x = 0; x < sizeX; x++)
                for (byte z = 0; z < sizeZ; z++)
                    for (byte l = 0; l < levels; l++)
                    {
						eBallType currentBallType;
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