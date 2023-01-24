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
using System.Threading.Tasks;
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

        private static readonly int MSAA_SAMPLES = 8;
        private static readonly string FILE_FILTER = "Maps (*.json)|*.json";

        #endregion Graphics

        public MapEditor(bool windowed = true, int windowWidth = 1280, int windowHeight = 800)
        {
            _windowed = windowed;

            _graphics = new GraphicsDeviceManager(this);

            _graphics.PreparingDeviceSettings += _graphics_PreparingDeviceSettings;
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
			new Task(() => { DeserializeMapFromJsonFile(e.Files[0]); }).Start();
			
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e) => Camera3D.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

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
                new(mgKeys.Up, Buttons.DPadUp, () => _selector.Move(Vector3.Forward), "Move selector forward"),
                new(mgKeys.Down, Buttons.DPadDown,() => _selector.Move(Vector3.Backward), "Move selector backward"),
                new(mgKeys.Left, Buttons.DPadLeft,() => _selector.Move(Vector3.Left), "Move selector left"),
                new(mgKeys.Right, Buttons.DPadRight,() => _selector.Move(Vector3.Right), "Move selector right"),
                new(mgKeys.PageUp, Buttons.RightShoulder,() => _selector.Move(Vector3.Up), "Move selector up"),
                new(mgKeys.PageDown, Buttons.LeftShoulder,() => _selector.Move(Vector3.Down), "Move selector down"),

                new(mgKeys.Space, Buttons.A,() => _selector.PutBall(), "Put ball"),
                new(mgKeys.Delete, Buttons.B,() => _selector.RemoveBall(), "Remove ball"),

                new(mgKeys.NumPad1,() => _selector.ChangeBallType(BallType.Type1), "Change ball type to 1"),
                new(mgKeys.NumPad2,() => _selector.ChangeBallType(BallType.Type2), "Change ball type to 2"),
                new(mgKeys.NumPad3,() => _selector.ChangeBallType(BallType.Type3), "Change ball type to 3"),

                new(mgKeys.Escape, Buttons.Back, Exit, "Exit"),
				new(mgKeys.F11, () => SetGraphics(_graphics.IsFullScreen), "Fullscreen/windowed"),
				new(mgKeys.F12,() => Info.Visible = ! Info.Visible, "Hide/show text overlay"),

                new(mgKeys.N, Buttons.X, () => new Task(FullMapTest).Start(), "Fill entire map with balls"),
                new(mgKeys.M, () => _map.Clear(), "Clear entire map"),

                new(mgKeys.D1,() => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Forward, true), "Forward view"),
                new(mgKeys.D2, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Backward, true), "Backward view"),
                new(mgKeys.D3, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Left, true), "Left view"),
                new(mgKeys.D4, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Right, true), "Right view"),
                new(mgKeys.D5, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Up, true), "Up view"),
                new(mgKeys.D6, () => _cih.CenterCameraToMapCenter(Vector3.Zero, Vector3.Down, true), "Down view"),

                new(mgKeys.R, () => _cih.RestartCamera(), "Restart camera"),

                new(mgKeys.F1, SaveJson, "Save map to file (JSON)"),
                new(mgKeys.F2, LoadJson, "Load map from file (JSON)"),
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

        private void SaveJson()
        {
            string filePath = GetFilePathByDialog(true);
            if (!string.IsNullOrEmpty(filePath))
            {
                Stopwatch stopwatch = new();
                stopwatch.Start();
                _map.SerializeAsJson(filePath);
                stopwatch.Stop();
                Console.WriteLine($"Serialize JSON (ms): {stopwatch.ElapsedMilliseconds}");
            }
        }

        private void LoadJson()
        {
            string filePath = GetFilePathByDialog(false);
            if (!string.IsNullOrEmpty(filePath))
            {
                new Task(() => { DeserializeMapFromJsonFile(filePath); }).Start();                
            }
        }

        private void DeserializeMapFromJsonFile(string filePath)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            _map.DeserializeJson(filePath);
            stopwatch.Stop();
            Console.WriteLine($"Deserialize JSON (ms): {stopwatch.ElapsedMilliseconds}");
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
                using (SaveFileDialog saveFileDialog = new())
                {
                    saveFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                    saveFileDialog.Filter = FILE_FILTER;
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        result = saveFileDialog.FileName;
                    }
                }

                return result;
            }

            using (OpenFileDialog openFileDialog = new())
            {
                openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
                openFileDialog.Filter = FILE_FILTER;
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
            _graphics.PreferredBackBufferWidth = windowed ? _windowWidth : GraphicsDevice.DisplayMode.Width;
            _graphics.PreferredBackBufferHeight = windowed ? _windowHeight : GraphicsDevice.DisplayMode.Height;
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
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = MSAA_SAMPLES;
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

            Stopwatch stopwatch = new();
            stopwatch.Start();

            _map = new BallsMap(sizeX, sizeZ, levels, _hrSphere);

            Array ballTypes = Enum.GetValues(typeof(BallType));
            Random random = new();
            BallType lastBallType = BallType.Type1;

            for (byte x = 0; x < sizeX; x++)
                for (byte z = 0; z < sizeZ; z++)
                    for (byte l = 0; l < levels; l++)
                    {
                        BallType currentBallType;
                        do
                        {
                            currentBallType = (BallType)ballTypes.GetValue(random.Next(ballTypes.Length));
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