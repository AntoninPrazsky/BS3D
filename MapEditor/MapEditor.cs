using MapEditor.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Input;
using Prazsky.Core;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using Prazsky.Render;
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

        //A little air around the play field, so that it does not touch the edges of the screen
        private const float VIEW_MARGIN = 1.1f;

        #region Ball rendering

        //The balls are drawn exactly as the game draws them, so that a map looks here the way it will play:
        //generated sphere LODs picked by camera distance, instanced through the game's own shader.
        //See the "Ball rendering" section in CLAUDE.md
        private static readonly int[,] BALL_LOD_RESOLUTIONS = { { 32, 24 }, { 16, 12 }, { 10, 7 } };
        private static readonly float[] BALL_LOD_DISTANCES = { 15f, 30f };
        private static readonly int BALL_LOD_COUNT = 3;
        private static readonly int BALL_TYPE_COUNT = 4;
        private static readonly int BALL_PATTERN_GORES = 3;

        private static readonly float BALL_OCCLUSION_STRENGTH = 0.55f;
        private static readonly int MAX_BALL_OCCLUDERS = 12;

        private Effect _instancingEffect;
        private SphereMesh[] _ballMeshes;
        private InstancedModelRenderer[] _ballRenderers;

        //One instance bucket per ball type and LOD level; each bucket becomes a single instanced draw call
        private readonly ModelInstance[][] _ballInstances = new ModelInstance[BALL_TYPE_COUNT * BALL_LOD_COUNT][];
        private readonly int[] _ballInstanceCounts = new int[BALL_TYPE_COUNT * BALL_LOD_COUNT];

        private SkyDome _sky;

        #endregion Ball rendering

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

        public InfoRenderer Info { private set; get; }

        private static readonly int MSAA_SAMPLES = 8;

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

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            Camera3D.AspectRatio = GraphicsDevice.Viewport.AspectRatio;
            
            Info.RecomputeScale();
        }

        protected override void Initialize()
        {
            IsMouseVisible = true;

            Camera3D = new BasicCamera3D(new Vector3(5f, 3.2f, 20f), GraphicsDevice.Viewport.AspectRatio);
            Camera3D.SetCircularMovementProperties(15f);
            Info = new InfoRenderer(this, "Content/Fonts/cascadia") { DrawOrder = int.MaxValue };
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

                new(mgKeys.NumPad1,() => _selector.ChangeBallType(BallType.Type1), "Change ball type to 1 (red)"),
                new(mgKeys.NumPad2,() => _selector.ChangeBallType(BallType.Type2), "Change ball type to 2 (green)"),
                new(mgKeys.NumPad3,() => _selector.ChangeBallType(BallType.Type3), "Change ball type to 3 (blue)"),
				new(mgKeys.NumPad4,() => _selector.ChangeBallType(BallType.Type4), "Change ball type to 4 (white)"),

				new(mgKeys.Escape, Buttons.Back, Exit, "Exit"),
				new(mgKeys.F11, () => SetGraphics(_graphics.IsFullScreen), "Fullscreen/windowed"),
				new(mgKeys.F12,() => Info.Visible = ! Info.Visible, "Hide/show text overlay"),

                new(mgKeys.N, Buttons.X, () => new Task(FullMapTest).Start(), "Fill entire map with balls"),
                new(mgKeys.M, () => _map.Clear(), "Clear entire map"),

                new(mgKeys.D1,() => CenterViewOn(Vector3.Forward), "Forward view"),
                new(mgKeys.D2, () => CenterViewOn(Vector3.Backward), "Backward view"),
                new(mgKeys.D3, () => CenterViewOn(Vector3.Left), "Left view"),
                new(mgKeys.D4, () => CenterViewOn(Vector3.Right), "Right view"),
                new(mgKeys.D5, () => CenterViewOn(Vector3.Up), "Up view"),
                new(mgKeys.D6, () => CenterViewOn(Vector3.Down), "Down view"),

                new(mgKeys.R, () => _cih.RestartCamera(), "Restart camera"),

                new(mgKeys.F1, SaveJson, "Save map to file (JSON)"),
                new(mgKeys.F2, LoadJson, "Load map from file (JSON)"),
                new(mgKeys.F3, NewMap, "New map (choose play field size)"),
            };

            StringBuilder builder = new();
            foreach (var act in _actions) builder.Append(string.Format("{0,-9} {1}\n", act.Key.ToString(), act.Description));

            Info.HintText = builder.ToString();

            #endregion

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _hrSphere = Content.Load<Model>("HRGeoDome"); //Still required by BallsMap; the balls themselves are generated spheres

            _instancingEffect = Content.Load<Effect>("Shaders/InstancedModel");
            _ballMeshes = new SphereMesh[BALL_LOD_COUNT];
            _ballRenderers = new InstancedModelRenderer[BALL_LOD_COUNT];

            for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
            {
                //Constants.HALF is the ball radius the physics builder uses in the game; the editor has no physics to ask
                _ballMeshes[lod] = new SphereMesh(GraphicsDevice, Constants.HALF, BALL_LOD_RESOLUTIONS[lod, 0], BALL_LOD_RESOLUTIONS[lod, 1]);
                _ballRenderers[lod] = new InstancedModelRenderer(GraphicsDevice, _ballMeshes[lod], Vector3.One, _instancingEffect)
                {
                    PatternGoreCount = BALL_PATTERN_GORES
                };
            }

            _sky = new SkyDome(Content.Load<Model>("Skyes/SkyDome1"), GraphicsDevice);

			//10 levels for the initial ball layout plus empty levels at the bottom for the structure to grow into
			_map = new BallsMap(10, 10, 15, _hrSphere);
            _selector = new Selector(Content, _map, Camera3D);
            _aabb = new AABB(GraphicsDevice);
            _aabb.FitToMap(_map);

            ApplySkyLighting();
        }

        /// <summary>
        /// Derives the ball lighting from the sky dome exactly as the game does: hemisphere ambient (zenith
        /// colour from above, horizon colour from below) plus a light rig tinted by the same palette.
        /// </summary>
        private void ApplySkyLighting()
        {
            Vector3 zenith = _sky.ZenithColor;
            Vector3 horizon = _sky.HorizonColor;

            Vector3 keyTint = Vector3.Lerp(Vector3.One, horizon, 0.5f);
            Vector3 backTint = Vector3.Lerp(Vector3.One, zenith, 0.5f);

            foreach (InstancedModelRenderer renderer in _ballRenderers)
            {
                renderer.SkyColor = zenith * 1.3f;
                renderer.GroundColor = horizon * 0.75f; //Bounce light from below is dimmer than the sky above

                renderer.KeyLightPosition = -DefaultLighting.Light0Direction * 40f;
                renderer.SetLightTint(keyTint, backTint);
            }
        }

        /// <summary>
        /// Gathers every ball of the map into the per-type-and-LOD buckets, each of which becomes one instanced
        /// draw call. The editor map is static, so unlike the game there is nothing to ease: the occlusion is
        /// simply what the grid says it is.
        /// </summary>
        private void CollectBallInstances()
        {
            for (int i = 0; i < _ballInstanceCounts.Length; i++) _ballInstanceCounts[i] = 0;

            if (_map == null) return;

            StaticBall[,,] balls = _map.GetStaticBallsArray();
            XZLevel size = _map.GetStaticBallsArraySize();

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        int occluders = BallsMap.CountOccupiedNeighbours(balls, new XZLevel(x, z, level), size, out Vector3 occlusionSum);

                        CollectBallInstance(ball, new Vector4(
                            occlusionSum.X / MAX_BALL_OCCLUDERS,
                            occlusionSum.Y / MAX_BALL_OCCLUDERS,
                            occlusionSum.Z / MAX_BALL_OCCLUDERS,
                            1f - BALL_OCCLUSION_STRENGTH * occluders / MAX_BALL_OCCLUDERS));
                    }
        }

        private void CollectBallInstance(StaticBall ball, Vector4 occlusionData)
        {
            int typeIndex = (int)ball.Type - 1;
            if (typeIndex < 0 || typeIndex >= BALL_TYPE_COUNT) return;

            //Mesh resolution by distance from the camera
            float distance = Vector3.Distance(ball.Position, Camera3D.Position);
            int lod = 0;
            while (lod < BALL_LOD_DISTANCES.Length && distance > BALL_LOD_DISTANCES[lod]) lod++;

            int bucketIndex = typeIndex * BALL_LOD_COUNT + lod;
            ModelInstance[] bucket = _ballInstances[bucketIndex];
            int count = _ballInstanceCounts[bucketIndex];

            if (bucket == null)
            {
                bucket = new ModelInstance[256];
                _ballInstances[bucketIndex] = bucket;
            }
            else if (count == bucket.Length)
            {
                Array.Resize(ref bucket, bucket.Length * 2);
                _ballInstances[bucketIndex] = bucket;
            }

            //Editor balls never rotate, so the position is the whole transformation
            bucket[count] = new ModelInstance(Matrix.CreateTranslation(ball.Position), occlusionData);
            _ballInstanceCounts[bucketIndex] = count + 1;
        }

        private void DrawBallsInstanced()
        {
            for (int typeIndex = 0; typeIndex < BALL_TYPE_COUNT; typeIndex++)
                for (int lod = 0; lod < BALL_LOD_COUNT; lod++)
                {
                    int bucketIndex = typeIndex * BALL_LOD_COUNT + lod;

                    _ballRenderers[lod].Draw(Camera3D, _ballInstances[bucketIndex], _ballInstanceCounts[bucketIndex],
                        BasicEffectParamsProvider.GetEffectByType((BallType)(typeIndex + 1)),
                        BasicEffectParamsProvider.GetDiffuseTintByType((BallType)(typeIndex + 1)));
                }
        }

        /// <summary>
        /// Looks at the centre of the play field from the given direction, from far enough away for the whole
        /// field to fit on the screen. The direction is expected to be axis aligned, as all six preset views are.
        /// </summary>
        private void CenterViewOn(Vector3 lookDirection)
        {
            Vector3 half = _aabb.Size * Constants.HALF;

            //Which of the field dimensions ends up across the screen, up the screen and pointing at the camera.
            //Looking from above or below puts Z up the screen, because that is where the up vector of the camera
            //tips over to once it is pointing straight down or up
            float halfWidth, halfHeight, halfDepth;
            if (lookDirection.Y != 0f) (halfWidth, halfHeight, halfDepth) = (half.X, half.Z, half.Y);
            else if (lookDirection.X != 0f) (halfWidth, halfHeight, halfDepth) = (half.Z, half.Y, half.X);
            else (halfWidth, halfHeight, halfDepth) = (half.X, half.Y, half.Z);

            //The window can be resized to any shape, so neither field of view can be assumed to be the narrower one
            float horizontalFieldOfView = 2f * MathF.Atan(MathF.Tan(Camera3D.FieldOfView * Constants.HALF) * Camera3D.AspectRatio);

            float distanceToFace = MathF.Max(
                VIEW_MARGIN * halfWidth / MathF.Tan(horizontalFieldOfView * Constants.HALF),
                VIEW_MARGIN * halfHeight / MathF.Tan(Camera3D.FieldOfView * Constants.HALF));

            //The camera is placed relative to the centre of the field, so it has to clear the near half of it as well
            _cih.CameraOffset = halfDepth + distanceToFace;
            _cih.CenterCameraToMapCenter(_aabb.Center, lookDirection, true);
        }

        private void NewMap()
        {
            EnsureNotFullScreen();

            using NewMapDialog dialog = new(_map.StageSizeX, _map.StageSizeZ, _map.Levels);
            if (dialog.ShowDialog() != DialogResult.OK) return;

            _map = new BallsMap(dialog.StageSizeX, dialog.StageSizeZ, dialog.Levels, _hrSphere);
            _selector.UpdateBallsBap(_map);
            _aabb.FitToMap(_map);

            Info.CustomText = $"New map {dialog.StageSizeX} x {dialog.StageSizeZ} x {dialog.Levels}";
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
            EnsureNotFullScreen();

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
            EnsureNotFullScreen();

            string filePath = GetFilePathByDialog(false);
            if (!string.IsNullOrEmpty(filePath))
            {
                new Task(() => { DeserializeMapFromJsonFile(filePath); }).Start();                
            }
        }

        private void EnsureNotFullScreen()
        {
            if (!_graphics.IsFullScreen) return;

            SetGraphics(true);
        }

        private void DeserializeMapFromJsonFile(string filePath)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            _map.DeserializeJson(filePath);
            stopwatch.Stop();
            Console.WriteLine($"Deserialize JSON (ms): {stopwatch.ElapsedMilliseconds}");

            //The loaded map may have different play field dimensions
            _selector.UpdateBallsBap(_map);
            _aabb.FitToMap(_map);
        }

        protected override void Draw(GameTime gameTime)
        {
            CollectBallInstances();

            GraphicsDevice.Clear(Color.LightSlateGray);

            //Drawing the selector leaves additive blending behind, which would wash the sky dome out
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            _sky.Draw(Camera3D);

            DrawBallsInstanced();

            //The outline is translucent, so it is drawn over the finished scene and does not write depth
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
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
                    saveFileDialog.Filter = Constants.MAPS_FILE_FILTER;
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
                openFileDialog.Filter = Constants.MAPS_FILE_FILTER;
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
            //Fill the current map's whole play field (previously this replaced _map with a new 10×10×10 instance,
            //leaving the selector working on the orphaned old map)
            byte sizeX = _map.StageSizeX;
            byte sizeZ = _map.StageSizeZ;
            byte levels = _map.Levels;

            Stopwatch stopwatch = new();
            stopwatch.Start();

            _map.Clear();

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