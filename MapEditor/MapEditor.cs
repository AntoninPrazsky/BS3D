using MapEditor.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Input;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        private CameraInputHelper _cih;
        private ButtonAction[] _actions;

        #region Graphics

        private int _windowWidth;
        private int _windowHeight;

        private GraphicsDeviceManager _graphics;
        private bool _windowed;

        public TextInfoRenderer Info { private set; get; }

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
            Info = new TextInfoRenderer(this, "Content/Fonts/cascadia") { DrawOrder = int.MaxValue };
            Components.Add(Info);

            _cih = new CameraInputHelper(Camera3D, this);

            #region Controls

            _actions = new ButtonAction[]
            {
                new ButtonAction(mgKeys.Up, Buttons.DPadUp, () => _selector.Move(Vector3.Forward), "Move selector forward"),
				new ButtonAction(mgKeys.Down, Buttons.DPadDown, () => _selector.Move(Vector3.Backward), "Move selector backward"),
				new ButtonAction(mgKeys.Left, Buttons.DPadLeft, () => _selector.Move(Vector3.Left), "Move selector left"),
				new ButtonAction(mgKeys.Right, Buttons.DPadRight, () => _selector.Move(Vector3.Right), "Move selector right"),
				new ButtonAction(mgKeys.PageUp, Buttons.RightShoulder, () => _selector.Move(Vector3.Up), "Move selector up"),
				new ButtonAction(mgKeys.PageDown, Buttons.LeftShoulder, () => _selector.Move(Vector3.Down), "Move selector down"),

				new ButtonAction(mgKeys.Space, Buttons.A, () => _selector.PutBall(), "Put ball"),
				new ButtonAction(mgKeys.Delete, Buttons.B, () => _selector.RemoveBall(), "Remove ball"),

				new ButtonAction(mgKeys.NumPad1, () => _selector.ChangeBallType(eBallType.Type1), "Change ball type to 1"),
				new ButtonAction(mgKeys.NumPad2, () => _selector.ChangeBallType(eBallType.Type2), "Change ball type to 2"),
				new ButtonAction(mgKeys.NumPad3, () => _selector.ChangeBallType(eBallType.Type3), "Change ball type to 3"),

				new ButtonAction(mgKeys.Escape, Buttons.Back, Exit, "Exit"),
				new ButtonAction(mgKeys.F12, () => Info.Visible = !Info.Visible, "Hide/show text overlay"),

				new ButtonAction(mgKeys.N, Buttons.X, FullMapTest, "Fill entire map with balls"),

				new ButtonAction(mgKeys.D1, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Forward, true), "Forward view"),
				new ButtonAction(mgKeys.D2, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Backward, true), "Backward view"),
				new ButtonAction(mgKeys.D3, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Left, true), "Left view"),
				new ButtonAction(mgKeys.D4, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Right, true), "Right view"),
				new ButtonAction(mgKeys.D5, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Up, true), "Up view"),
				new ButtonAction(mgKeys.D6, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Down, true), "Down view"),

				new ButtonAction(mgKeys.R, () => _cih.RestartCamera(), "Restart camera"),

				new ButtonAction(mgKeys.F1, Save, "Save map to file"),
				new ButtonAction(mgKeys.F2, Load, "Load map from file"),
			};

			StringBuilder builder = new();
			foreach (var act in _actions) builder.Append(string.Format("{0,-9} {1}\n", act.Key.ToString(), act.Description));

			Info.HintText = builder.ToString();

			#endregion

			base.Initialize();
        }

        protected override void LoadContent()
        {
            _hrSphere = Content.Load<Model>("HRGeoDome");

            _map = new BallsMap(10, 10, 10, _hrSphere);
            _selector = new Selector(Content, _map);
            _aabb = new AABB(Content);
        }

        protected override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            _cih.RegisterCurrentInputState();

			foreach (var action in _actions) if (_cih.PressedOnce(action.Key, action.Button)) action.Method();

			_cih.CameraMovement(gameTime);
            _cih.RegisterPreviousInputState();
            
            _cih.Update(gameTime);

            base.Update(gameTime);
        }

        private void Save()
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

        private void Load()
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

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.MidnightBlue);
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            if (_map != null) _map.Draw(Camera3D);

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