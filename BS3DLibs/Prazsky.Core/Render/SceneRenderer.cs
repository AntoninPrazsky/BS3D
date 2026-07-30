using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Which environment the arena stands in. City is the default; Sea, Savanna, Desert, Mountain, Meadow,
    /// NeonCity and Forest swap the city (and only the city) for open water, a savanna, a Sahara of dunes, a
    /// snowy range, a flowering meadow, the same city lit up in neon, or a forest clearing.
    /// <para>
    /// <see cref="Space"/> is the one that is not like the others: it replaces the <b>sky</b> rather than the
    /// ground, so the island floats in deep space and there is no terrain, no horizon and no weather at all.
    /// </para>
    /// <para>
    /// <b>Which of them an executable can reach is not the same question as which it can draw</b> — all three
    /// draw all of them. The Testbed's NumPad2 and the map editor's V both cycle <c>% 7</c>, i.e. over the seven
    /// scenes a map is authored against; <see cref="Forest"/> and <see cref="Space"/> sit past the end of that
    /// cycle, reached in the Testbed with <c>scene=</c>, in the game from its scene menu (or its random launch
    /// pick), and in the editor only by loading a level whose config names one of them.
    /// </para>
    /// </summary>
    public enum SceneKind { City, Sea, Savanna, Desert, Mountain, Meadow, NeonCity, Forest, Space, Dream, Cavern }

    /// <summary>
    /// The per-frame inputs a scene needs that are not its own static tuning: the camera, the sun direction,
    /// the sky palette in <b>linear</b> radiance (zenith and horizon), the sun's own radiance already tinted
    /// by the dome (for the sea glint and the warm cast on the terrain), the wall-clock time its motion runs
    /// off, and an optional hook that hands the shared cloud field to an effect. <see cref="ApplyClouds"/> is
    /// null when there is no weather to apply — the map editor draws no clouds, so it leaves the cloud
    /// uniforms at zero and <c>CloudSunlight</c> returns a flat 1.0 (full sun, no shadow).
    /// </summary>
    public readonly struct SceneFrame
    {
        public readonly ICamera Camera;
        public readonly Vector3 SunDirection;
        public readonly Vector3 ZenithLinear;
        public readonly Vector3 HorizonLinear;
        public readonly Vector3 SunColor;
        public readonly float Time;
        public readonly Action<Effect> ApplyClouds;

        public SceneFrame(ICamera camera, Vector3 sunDirection, Vector3 zenithLinear, Vector3 horizonLinear,
            Vector3 sunColor, float time, Action<Effect> applyClouds)
        {
            Camera = camera;
            SunDirection = sunDirection;
            ZenithLinear = zenithLinear;
            HorizonLinear = horizonLinear;
            SunColor = sunColor;
            Time = time;
            ApplyClouds = applyClouds;
        }
    }

    /// <summary>
    /// A light rig a scene states for itself in place of the one derived from the sky dome — the hemisphere
    /// ambient from above and below, and the tints the key and back lights take. Only the space scene has one
    /// (see <see cref="SceneRenderer.TryGetLightRig"/>); every other scene's rig is the dome's. All four are
    /// <b>linear</b> radiance, already scaled: they are what the renderer's <c>SkyColor</c>/<c>GroundColor</c>
    /// and <c>SetLightTint</c> take, not something to be scaled again on the way in.
    /// </summary>
    public readonly struct SceneLightRig
    {
        public readonly Vector3 SkyAmbient;
        public readonly Vector3 GroundAmbient;
        public readonly Vector3 KeyTint;
        public readonly Vector3 BackTint;

        public SceneLightRig(Vector3 skyAmbient, Vector3 groundAmbient, Vector3 keyTint, Vector3 backTint)
        {
            SkyAmbient = skyAmbient;
            GroundAmbient = groundAmbient;
            KeyTint = keyTint;
            BackTint = backTint;
        }
    }

    /// <summary>
    /// The switchable outdoor backdrops shared by the game and the map editor, so a scene looks the same in
    /// both: the sea, the savanna (with its acacias and circling birds), the desert (Sahara dunes, with the
    /// same flock of birds), the snowy mountains (with falling snow) and the flowering meadow. Each is a
    /// self-lit dedicated shader — it computes its own lighting from the sun and the sky palette handed over
    /// in a <see cref="SceneFrame"/> — so this owns their effects, meshes and tuning and nothing else in the
    /// frame has to know about them.
    /// <para>
    /// The City/NeonCity is deliberately <b>not</b> here: the city buildings are drawn through the shared
    /// <c>InstancedModel</c> city technique by an <see cref="InstancedModelRenderer"/> the caller owns, so
    /// they take part in the caller's sky lighting like every other instanced object (see <see cref="City"/>).
    /// </para>
    /// See the "The sea/savanna/desert/mountains/meadow" sections in CLAUDE.md for what each one is doing.
    /// </summary>
    public sealed class SceneRenderer : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;

        /// <summary>
        /// Radius of the arena platform's footprint, cut out of every solid terrain scene (mountains, meadow,
        /// savanna, desert) around the world origin so the drain funnel below the island reads as a drain into a
        /// pit rather than a bowl in flat ground - the flat clearing otherwise slices across the funnel just
        /// below its rim, hiding its depth and swallowing the balls falling through. The Testbed sets this to the
        /// island's radius; the map editor draws no island, so it leaves it 0 (the default) and nothing is cut.
        /// The water scenes are not cut - the funnel draining into the sea is fine as it is.
        /// </summary>
        public float TerrainHoleRadius { get; set; }

        /// <summary>Mean sea level of the sea scene (world Y), so the caller can tell when its camera is under
        /// the water and fade in the underwater murk.</summary>
        public float SeaLevelY => _seaConfig.LevelY;

        /// <summary>
        /// How many scene-target texels make one output pixel — the caller's supersampling factor, which only
        /// the caller knows (the Game's moves with the quality tier). The space scene sizes its stars in
        /// <b>output</b> pixels off this: sized in texels instead, a star would come out four times dimmer at
        /// 2× than at 1×, which is the same sky looking different on two quality settings. Left at 1 it is
        /// simply the no-supersampling case, so a caller that never sets it still gets a correct sky.
        /// </summary>
        public int SupersampleFactor { get; set; } = 1;

        //Scene configuration. Defaults reproduce the original hard-coded look byte-for-byte; every scene
        //reads its tuning from these instead of constants. Replaced at runtime by Apply(SceneConfig) when a
        //level is loaded (issue #32), which re-pushes the effect parameters and rebuilds the scatter/particle
        //buffers the config sizes.
        private SeaSceneConfig _seaConfig = new();
        private DesertSceneConfig _desertConfig = new();
        private SavannaSceneConfig _savannaConfig = new();
        private MountainSceneConfig _mountainConfig = new();
        private MeadowSceneConfig _meadowConfig = new();
        private ForestSceneConfig _forestConfig = new();
        private SpaceSceneConfig _spaceConfig = new();
        private DreamSceneConfig _dreamConfig = new();
        private CavernSceneConfig _cavernConfig = new();

        #region Sea

        private readonly Effect _seaEffect;
        private readonly VertexBuffer _seaVertexBuffer;
        private readonly IndexBuffer _seaIndexBuffer;
        private readonly int _seaIndexCount;

        //The sea is real geometry now, like the dunes: a camera-centred grid this many vertices per side over
        //this world extent, displaced by Gerstner waves in the shader and snapped to a cell on the CPU each
        //frame so it does not swim. Dense enough for the dominant swell to read as smooth geometry; the fine
        //chop is added per pixel. (Grid density is the natural Low/Med/High/Ultra dial once graphics settings land.)
        private const int SEA_GRID_N = 380;
        private const float SEA_EXTENT = 1600f;

        //Look/tuning parameters (water level & colours, waves, chop, wind, sun glint, foam, subsurface, haze)
        //now live in SeaSceneConfig; SceneRenderer reads them from _seaConfig (spray via _seaConfig.Spray).

        #endregion

        #region Desert

        private readonly Effect _desertEffect;
        private readonly VertexBuffer _desertVertexBuffer;
        private readonly IndexBuffer _desertIndexBuffer;
        private readonly int _desertIndexCount;

        //The dunes are real geometry: a camera-centred grid of this many vertices per side over this world
        //extent, displaced in the shader and snapped to a cell so they do not swim. Finer than the old dune
        //grid (200) so the crest silhouettes read smooth; the shading normal is per-pixel, so no grid shows.
        private const int DESERT_GRID_N = 360;
        private const float DESERT_EXTENT = 1000f;

        //Look/tuning parameters (dune height, clearing, ripples, dust, sand colour, wind, haze) now live in
        //DesertSceneConfig; SceneRenderer reads them from _desertConfig.

        #endregion

        #region Savanna

        private readonly Effect _savannaEffect;
        private readonly VertexBuffer _savannaVertexBuffer;
        private readonly IndexBuffer _savannaIndexBuffer;
        private readonly int _savannaIndexCount;

        //Open grassland is real geometry: a camera-centred grid of this many vertices per side over this world
        //extent, displaced in the shader and snapped to a cell so it does not swim. Finer than the old dune
        //grid (200) so the silhouette is smooth; the shading normal is per-pixel, so the grid no longer shows.
        private const int SAVANNA_GRID_N = 400;
        private const float SAVANNA_EXTENT = 1200f;

        //Gentle rolling grassland: flat in a clearing the island stands in (world origin), rising into low
        //rises with distance. Flatter than the meadow's hills - a savanna is open. Mean grass level sits at the
        //island's foot; ClearingRelief is a soft undulation even inside the clearing.
        //Look/tuning parameters (level, hills, clearing, grass colours, ambient, wind, haze, relief) now live in
        //SavannaSceneConfig; SceneRenderer reads them from _savannaConfig (and SavannaTerrainHeight uses them too).

        #endregion

        #region Acacia (savanna scene only)

        private readonly Effect _acaciaEffect;
        private VertexBuffer _acaciaVertexBuffer;
        private IndexBuffer _acaciaIndexBuffer;

        //Scattered acacia trees and low bushes over the savanna: upright billboards positioned on the ground
        //here, drawn as a flat-topped tree or a rounded clump in the shader. Alpha-tested and depth-writing, so
        //they occlude the terrain and each other. Plants gather in clumps (as they do on a real savanna) around
        //a set of cluster centres, with a few solitary ones. One buffer carries both: the packed random is
        //[0,1) for a tree and [1,2) for a bush.
        //Acacia scatter parameters (count, bush fraction, size, radii, clusters, canopy/trunk colours) now live
        //in SavannaSceneConfig.Acacia (AcaciaConfig); SceneRenderer reads them from _savannaConfig.Acacia.

        #endregion

        #region Campfire (savanna scene only)

        private readonly Effect _flameEffect;
        private readonly VertexBuffer _flameVertexBuffer;
        private readonly IndexBuffer _flameIndexBuffer;

        //Maximum scene point lights, matching MAX_SCENE_LIGHTS in InstancedModel.fx / Savanna.fx
        private const int MAX_SCENE_LIGHTS = 8;
        private readonly Vector3[] _savannaLightPos = new Vector3[MAX_SCENE_LIGHTS];
        private readonly Vector3[] _savannaLightColor = new Vector3[MAX_SCENE_LIGHTS];
        private readonly float[] _savannaLightRange = new float[MAX_SCENE_LIGHTS];

        //The savanna's campfire: a real point light that warms the grass, the island and the balls near it
        //(set on the savanna effect here and on the instanced effect by the Testbed), plus the visible flame
        //billboard below. It sits on the ground just off the island. Position/colour are public so the Testbed
        //can set the same light on the balls and island, and they flicker together off the one clock.
        //Just off the island in the grass, in front of its far edge and a little to the side, so it is in the
        //game camera's view (the camera sits at ~(0,-3,30) looking down -Z) and lights the island edge and grass.
        //Campfire parameters (ground position, range, flame size, base colour) live in
        //SavannaSceneConfig.Campfire (CampfireConfig). Position/range/colour stay public (the Testbed sets the
        //same point light on the balls and island) but are now instance members derived from the config.

        /// <summary>The campfire's world position: the config's ground XZ, lifted to the terrain height there.</summary>
        public Vector3 SavannaCampfirePosition => new(
            _savannaConfig.Campfire.GroundXZ.X,
            SavannaTerrainHeight(_savannaConfig.Campfire.GroundXZ.X, _savannaConfig.Campfire.GroundXZ.Y) + _savannaConfig.Campfire.HeightAboveTerrain,
            _savannaConfig.Campfire.GroundXZ.Y);

        /// <summary>The campfire point-light range (quadratic distance falloff).</summary>
        public float SavannaCampfireRange => _savannaConfig.Campfire.Range;

        /// <summary>The flickering campfire colour at a wall-clock time, so the grass light, the balls light
        /// and the flame all pulse together.</summary>
        public Vector3 CampfireColor(float time)
        {
            float flicker = 0.72f + 0.28f * (0.5f * MathF.Sin(time * 11f) + 0.3f * MathF.Sin(time * 17f + 1.3f) + 0.2f * MathF.Sin(time * 7f));
            return _savannaConfig.Campfire.BaseColor.ToVector3() * flicker;
        }

        #endregion

        #region Birds (savanna and desert scenes)

        private readonly Effect _birdsEffect;
        private DynamicVertexBuffer _birdVertexBuffer;
        private IndexBuffer _birdIndexBuffer;
        private BirdVertex[] _birdVertices;

        private float[] _birdRadius, _birdAltitude, _birdOrbitSpeed, _birdOrbitPhase, _birdFlapSpeed, _birdFlapPhase, _birdBobSpeed;

        //Flock parameters (count, wingspan, aspect, bob, colour, flock centre) now live in BirdsConfig, shared
        //by the savanna and desert configs; DrawBirds reads the active scene's Birds config each frame. The
        //shared buffer and per-bird orbit state are sized once from the savanna config's count.

        //One camera-facing quad per bird; Data carries (u along the wingspan, v vertical, flap phase).
        //Also reused for the flame, acacia, snow and spray billboards.
        private struct BirdVertex : IVertexType
        {
            public Vector3 Position;
            public Vector3 Data;

            public BirdVertex(Vector3 position, Vector3 data)
            {
                Position = position;
                Data = data;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        #endregion

        #region Mountain

        private readonly Effect _mountainEffect;
        private readonly VertexBuffer _mountainVertexBuffer;
        private readonly IndexBuffer _mountainIndexBuffer;
        private readonly int _mountainIndexCount;

        //Finer than the first version (240) so the craggier peaks resolve; needs the 32-bit index buffer (360*360
        //vertices overflow a 16-bit one). Per-vertex base normal, per-pixel rock relief on top (see Mountain.fx)
        private const int MOUNTAIN_GRID_N = 360;
        private const float MOUNTAIN_EXTENT = 1200f;

        //Look/tuning parameters (heights, clearing, snow/rock colours, snowline, rock relief, ambient, haze)
        //now live in MountainSceneConfig; SceneRenderer reads them from _mountainConfig.

        #endregion

        #region Snow (mountain scene only)

        private readonly Effect _snowEffect;
        private VertexBuffer _snowVertexBuffer;
        private IndexBuffer _snowIndexBuffer;

        //Snowfall parameters (flake count/size/colour/opacity, box, fall speed, wind, sway) now live in
        //MountainSceneConfig.Snow (SnowConfig); SceneRenderer reads them from _mountainConfig.Snow.

        #endregion

        #region Spray (sea scene only)

        private readonly Effect _sprayEffect;
        private VertexBuffer _sprayVertexBuffer;
        private IndexBuffer _sprayIndexBuffer;

        //Spray parameters (particle count/size/colour/opacity, box, level, wind, rise, turbulence) now live in
        //SeaSceneConfig.Spray (SprayConfig); SceneRenderer reads them from _seaConfig.Spray. The glare-safe
        //spray colour still matters: its luminance must stay under GLARE_THRESHOLD or it blooms - see CLAUDE.md.

        #endregion

        #region Meadow

        private readonly Effect _meadowEffect;
        private readonly VertexBuffer _meadowVertexBuffer;
        private readonly IndexBuffer _meadowIndexBuffer;
        private readonly int _meadowIndexCount;

        private const int MEADOW_GRID_N = 220;
        private const float MEADOW_EXTENT = 1200f;

        //Look/tuning parameters (hills, clearing, grass colours, ambient, haze, wind, relief) and the
        //wildflowers now live in MeadowSceneConfig (Flowers = FlowersConfig); read from _meadowConfig.

        #endregion

        #region Forest

        private readonly Effect _forestEffect;
        private readonly VertexBuffer _forestVertexBuffer;
        private readonly IndexBuffer _forestIndexBuffer;
        private readonly int _forestIndexCount;

        private const int FOREST_GRID_N = 220;
        private const float FOREST_EXTENT = 1200f;

        //Look/tuning parameters (hills, clearing, forest floor colours, treeline, ambient, haze, wind, needle
        //relief, floor lumps) live in ForestSceneConfig; read from _forestConfig. Its Trees/Rocks/Stumps
        //describe the scattered objects, which are the Game's own instanced draws rather than this scene's -
        //only ForestTerrainHeight below is shared with them, so they stand on the floor this shader draws.

        #endregion

        #region Space

        private readonly Effect _spaceEffect;

        //No terrain grid: space replaces the SKY, not the ground, so the whole scene is ONE full-screen pass
        //over a quad already in normalized device coordinates, with the view ray recovered per pixel through
        //the inverse view-projection. Four corners drawn as a triangle strip, built once.
        private readonly VertexBuffer _spaceQuad;

        //The handful of parameters that change per frame, resolved once (BestPractices §1: the by-name
        //indexer is a linear scan). Everything else is pushed by ApplySpaceParameters when a config lands.
        private readonly EffectParameter _spaceInverseViewProjection, _spaceCameraPosition, _spaceSunDirection, _spaceSupersample;

        #endregion

        #region Dream

        //The tenth scene, and the second that replaces the SKY rather than the ground (see Space above): the
        //same full-screen-quad machinery, sharing _spaceQuad, with its own effect and per-frame parameters.
        private readonly Effect _dreamEffect;
        private readonly EffectParameter _dreamInverseViewProjection, _dreamCameraPosition, _dreamTime;

        #endregion

        #region Cavern

        //The eleventh scene, the third sky-replacing pass — the same machinery again.
        private readonly Effect _cavernEffect;
        private readonly EffectParameter _cavernInverseViewProjection, _cavernCameraPosition, _cavernTime;

        #endregion

        /// <param name="content">
        /// A content manager whose root holds the scene shaders under <c>Shaders/</c> (both executables build
        /// <c>Sea.fx</c>, <c>Savanna.fx</c>, <c>Birds.fx</c>, <c>Mountain.fx</c>, <c>Snow.fx</c>, <c>Spray.fx</c>, <c>Meadow.fx</c>
        /// out of the Testbed content directory).
        /// </param>
        public SceneRenderer(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;

            //--- Sea: a camera-centred grid displaced into Gerstner waves; DrawSea snaps it to a cell and sets
            //the mean level. Drawn CullNone (one open surface, read from above and through the crests).
            _seaEffect = content.Load<Effect>("Shaders/Sea");
            CreateGridMesh(SEA_GRID_N, SEA_EXTENT, out _seaVertexBuffer, out _seaIndexBuffer, out _seaIndexCount);

            ApplySeaParameters();

            //--- Desert: a flat lattice the shader displaces into Sahara dunes (per-pixel normal, no grid)
            _desertEffect = content.Load<Effect>("Shaders/Desert");
            CreateGridMesh(DESERT_GRID_N, DESERT_EXTENT, out _desertVertexBuffer, out _desertIndexBuffer, out _desertIndexCount);

            ApplyDesertParameters();

            //--- Savanna: a flat lattice the shader displaces into gentle grassland (per-pixel normal, no grid)
            _savannaEffect = content.Load<Effect>("Shaders/Savanna");
            CreateGridMesh(SAVANNA_GRID_N, SAVANNA_EXTENT, out _savannaVertexBuffer, out _savannaIndexBuffer, out _savannaIndexCount);

            ApplySavannaParameters();

            //--- Acacia: a static billboard buffer of trees scattered over the savanna, positioned on the
            //ground (SavannaTerrainHeight mirrors the shader's field) and drawn as a flat-topped tree in Acacia.fx
            _acaciaEffect = content.Load<Effect>("Shaders/Acacia");
            ApplyAcaciaParameters();
            BuildAcaciaBuffers();

            //--- Campfire flame: one billboard drawn as a procedural flame at the campfire position
            _flameEffect = content.Load<Effect>("Shaders/Flame");
            BirdVertex[] flameVertices =
            {
                new(Vector3.Zero, new Vector3(-1f, 0f, 0f)),
                new(Vector3.Zero, new Vector3(1f, 0f, 0f)),
                new(Vector3.Zero, new Vector3(-1f, 1f, 0f)),
                new(Vector3.Zero, new Vector3(1f, 1f, 0f))
            };
            _flameVertexBuffer = new VertexBuffer(graphicsDevice, BirdVertex.Declaration, 4, BufferUsage.WriteOnly);
            _flameVertexBuffer.SetData(flameVertices);
            short[] flameIndices = { 0, 1, 2, 2, 1, 3 };
            _flameIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
            _flameIndexBuffer.SetData(flameIndices);

            //--- Birds: a dynamic billboard buffer, static indices, and each bird's orbit and flap seeded once
            _birdsEffect = content.Load<Effect>("Shaders/Birds");
            BuildBirdBuffers();

            //--- Mountain: a ridged displaced grid
            _mountainEffect = content.Load<Effect>("Shaders/Mountain");
            CreateGridMesh(MOUNTAIN_GRID_N, MOUNTAIN_EXTENT, out _mountainVertexBuffer, out _mountainIndexBuffer, out _mountainIndexCount);

            ApplyMountainParameters();

            //--- Snow: a static flake buffer, one quad per flake at a fixed point in the unit cube, animated
            //entirely in the shader (so it is only rebuilt when a mountain config is applied, never per frame).
            _snowEffect = content.Load<Effect>("Shaders/Snow");
            ApplySnowParameters();
            BuildSnowBuffers();

            //--- Spray: a static billboard buffer for the sea's blown spray and spindrift, animated entirely
            //in the shader like the snow. Same position+data billboard vertex.
            _sprayEffect = content.Load<Effect>("Shaders/Spray");
            ApplySprayParameters();

            BuildSprayBuffers();

            //--- Meadow: a smooth rolling displaced grid scattered with flowers
            _meadowEffect = content.Load<Effect>("Shaders/Meadow");
            CreateGridMesh(MEADOW_GRID_N, MEADOW_EXTENT, out _meadowVertexBuffer, out _meadowIndexBuffer, out _meadowIndexCount);

            ApplyMeadowParameters();

            //--- Forest: a mossy needle-strewn clearing ringed by wooded hills (the eighth scene)
            _forestEffect = content.Load<Effect>("Shaders/Forest");
            CreateGridMesh(FOREST_GRID_N, FOREST_EXTENT, out _forestVertexBuffer, out _forestIndexBuffer, out _forestIndexCount);

            ApplyForestParameters();

            //--- Space: the ninth scene, and the first of the two with no ground at all (the dream below is
            //the other) — a full-screen pass whose quad is already in normalized device coordinates, so
            //nothing transforms it
            _spaceEffect = content.Load<Effect>("Shaders/Space");

            VertexPosition[] corners =
            {
                new(new Vector3(-1f, 1f, 0f)),
                new(new Vector3(1f, 1f, 0f)),
                new(new Vector3(-1f, -1f, 0f)),
                new(new Vector3(1f, -1f, 0f))
            };
            _spaceQuad = new VertexBuffer(graphicsDevice, VertexPosition.VertexDeclaration, corners.Length, BufferUsage.WriteOnly);
            _spaceQuad.SetData(corners);

            _spaceInverseViewProjection = _spaceEffect.Parameters["InverseViewProjection"];
            _spaceCameraPosition = _spaceEffect.Parameters["CameraPosition"];
            _spaceSunDirection = _spaceEffect.Parameters["SunDirection"];
            _spaceSupersample = _spaceEffect.Parameters["SupersampleFactor"];

            ApplySpaceParameters();

            //--- Dream: the tenth scene, the second sky-replacing pass. It shares space's quad — a corner
            //quad in normalized device coordinates has nothing scene-specific about it.
            _dreamEffect = content.Load<Effect>("Shaders/Dream");

            _dreamInverseViewProjection = _dreamEffect.Parameters["InverseViewProjection"];
            _dreamCameraPosition = _dreamEffect.Parameters["CameraPosition"];
            _dreamTime = _dreamEffect.Parameters["DreamTime"];

            ApplyDreamParameters();

            //--- Cavern: the eleventh scene, the third sky-replacing pass, on the same shared quad.
            _cavernEffect = content.Load<Effect>("Shaders/Cavern");

            _cavernInverseViewProjection = _cavernEffect.Parameters["InverseViewProjection"];
            _cavernCameraPosition = _cavernEffect.Parameters["CameraPosition"];
            _cavernTime = _cavernEffect.Parameters["CavernTime"];

            ApplyCavernParameters();
        }

        /// <summary>
        /// True for the scenes that replace the SKY rather than the ground — space, the dream and the
        /// cavern. The caller draws no dome and no cloud deck in these, suppresses the cloud shadow on the
        /// instanced effect, clears to black (the pass covers every pixel; black is what would show if it
        /// ever did not), and takes the scene's own light rig through <see cref="TryGetLightRig"/>.
        /// </summary>
        public static bool ReplacesSky(SceneKind kind) => kind is SceneKind.Space or SceneKind.Dream or SceneKind.Cavern;

        #region Scene-config apply (issue #32)

        /// <summary>
        /// Applies a scene configuration at runtime — the path a loaded level takes. Re-pushes the scene's
        /// effect parameters and rebuilds the scatter/particle buffers the config sizes (acacias, birds,
        /// snow, spray). A <see cref="CitySceneConfig"/> is a no-op here: the city lives outside the
        /// SceneRenderer (<see cref="City"/> + the instanced city technique), so its caller applies it.
        /// </summary>
        public void Apply(SceneConfig config)
        {
            switch (config)
            {
                case SeaSceneConfig sea:
                    _seaConfig = sea;
                    ApplySeaParameters();
                    ApplySprayParameters();
                    BuildSprayBuffers();
                    break;
                case DesertSceneConfig desert:
                    _desertConfig = desert; //terrain params re-pushed below; birds read per frame from the config
                    ApplyDesertParameters();
                    BuildBirdBuffers();     //the shared flock is sized from both arid scenes' counts, so the desert's own count is honoured
                    break;
                case SavannaSceneConfig savanna:
                    _savannaConfig = savanna;
                    ApplySavannaParameters();
                    ApplyAcaciaParameters();
                    BuildAcaciaBuffers();   //tree positions depend on the terrain, so the terrain change rebuilds them
                    BuildBirdBuffers();     //the shared flock is sized from both arid scenes' counts
                    break;
                case MountainSceneConfig mountain:
                    _mountainConfig = mountain;
                    ApplyMountainParameters();
                    ApplySnowParameters();
                    BuildSnowBuffers();
                    break;
                case MeadowSceneConfig meadow:
                    _meadowConfig = meadow;
                    ApplyMeadowParameters();
                    break;
                case ForestSceneConfig forest:
                    _forestConfig = forest;
                    ApplyForestParameters();
                    break;
                case SpaceSceneConfig space:
                    _spaceConfig = space;
                    ApplySpaceParameters();
                    break;
                case DreamSceneConfig dream:
                    _dreamConfig = dream;
                    ApplyDreamParameters();
                    break;
                case CavernSceneConfig cavern:
                    _cavernConfig = cavern;
                    ApplyCavernParameters();
                    break;
                case CitySceneConfig:
                    break;
            }
        }

        /// <summary>
        /// The light rig a scene states for itself instead of taking the sky dome's, and false when it takes
        /// the dome's like every other one. Only <see cref="SceneKind.Space"/> states one, and it has to:
        /// it draws no dome, so a dome-derived rig would be a lie — and the specific lie is expensive, because
        /// the darkest dome halves the sun through the key tint and takes the metallic drain beads with it.
        /// See <see cref="SpaceLightingConfig"/> for the argument in full.
        /// <para>
        /// The caller applies this in its own <c>ApplySkyLighting</c> in place of the four dome-derived
        /// values, and everything else there — the key light's position, the renderers it walks — is unchanged.
        /// </para>
        /// </summary>
        public bool TryGetLightRig(SceneKind kind, out SceneLightRig rig)
        {
            switch (kind)
            {
                case SceneKind.Space:
                    SpaceLightingConfig space = _spaceConfig.Lighting;
                    rig = new SceneLightRig(
                        space.SkyAmbient.ToVector3(),
                        space.GroundAmbient.ToVector3(),
                        space.KeyTint.ToVector3(),
                        space.BackTint.ToVector3());
                    return true;

                //The dream states one for the same reason and one more: its rig is deliberately COLOURED
                //(violet over teal, a rose key against a cyan fill), so the island, the gun and the balls
                //sit in the hallucination instead of standing greyly in front of it.
                case SceneKind.Dream:
                    DreamLightingConfig dream = _dreamConfig.Lighting;
                    rig = new SceneLightRig(
                        dream.SkyAmbient.ToVector3(),
                        dream.GroundAmbient.ToVector3(),
                        dream.KeyTint.ToVector3(),
                        dream.BackTint.ToVector3());
                    return true;

                //The cavern's is dim and cool — a cave lit by its own bioluminescence, the ground bounce
                //carrying the river's teal up onto the island's underside.
                case SceneKind.Cavern:
                    CavernLightingConfig cavern = _cavernConfig.Lighting;
                    rig = new SceneLightRig(
                        cavern.SkyAmbient.ToVector3(),
                        cavern.GroundAmbient.ToVector3(),
                        cavern.KeyTint.ToVector3(),
                        cavern.BackTint.ToVector3());
                    return true;

                default:
                    rig = default;
                    return false;
            }
        }

        /// <summary>
        /// The space scene's planetshine, as a scene point light the caller can drop into a slot: the light the
        /// planet throws back onto the island's flank. False when there is no planet, no planetshine or the
        /// scene is not space.
        /// <para>
        /// A point light rather than more ambient, deliberately. Ambient is directionless, so raising it to get
        /// a coloured flank flattens the whole scene instead; and a real light also puts a highlight back into
        /// the drain's gold beads, which being metallic have almost nothing but reflections to show. It stands
        /// far enough off that the falloff barely varies across the island, so it reads as directional.
        /// </para>
        /// </summary>
        public bool TryGetSpacePlanetshine(SceneKind kind, out Vector3 position, out Vector3 color, out float range)
        {
            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;

            SpacePlanetConfig planet = _spaceConfig.Planet;
            SpaceLightingConfig lighting = _spaceConfig.Lighting;

            if (kind != SceneKind.Space || lighting.PlanetshineStrength <= 0f || planet.AngularRadiusDegrees <= 0f) return false;

            Vector3 direction = SafeNormal(planet.Direction.ToVector3(), Vector3.Forward);

            position = direction * lighting.PlanetshineDistance;

            //The planet's own colour is what it reflects back, and its pale bands are what most of the disc
            //is; normalized so the strength alone says how bright the fill is and the colour only says its hue
            Vector3 albedo = planet.ColorLight.ToVector3();
            float peak = MathF.Max(MathF.Max(albedo.X, albedo.Y), MathF.Max(albedo.Z, 1e-4f));

            color = albedo / peak * lighting.PlanetshineStrength;

            //The falloff is (1 - d/range)^2, so the light has to stand well inside its own range or it
            //arrives as nothing. At three times the distance it is 4/9 of full here and varies by a few per
            //cent across the island, which is what makes a point light stand in for a distant one.
            range = lighting.PlanetshineDistance * 3f;

            return true;
        }

        /// <summary>
        /// The active configuration of one of the self-lit scenes — what a level saves as its scene. Returns
        /// null for <see cref="SceneKind.City"/>/<see cref="SceneKind.NeonCity"/>, whose config lives outside
        /// the renderer (the caller owns the <see cref="CitySceneConfig"/>).
        /// </summary>
        public SceneConfig GetSceneConfig(SceneKind kind) => kind switch
        {
            SceneKind.Sea => _seaConfig,
            SceneKind.Desert => _desertConfig,
            SceneKind.Savanna => _savannaConfig,
            SceneKind.Mountain => _mountainConfig,
            SceneKind.Meadow => _meadowConfig,
            SceneKind.Forest => _forestConfig,
            SceneKind.Space => _spaceConfig,
            SceneKind.Dream => _dreamConfig,
            SceneKind.Cavern => _cavernConfig,
            _ => null,
        };

        private void ApplySeaParameters()
        {
            _seaEffect.Parameters["SeaLevelY"].SetValue(_seaConfig.LevelY);
            _seaEffect.Parameters["WaterColorDeep"].SetValue(_seaConfig.WaterDeep.ToVector3());
            _seaEffect.Parameters["WaterColorShallow"].SetValue(_seaConfig.WaterShallow.ToVector3());
            _seaEffect.Parameters["WaveAmplitude"].SetValue(_seaConfig.WaveAmplitude);
            _seaEffect.Parameters["WaveSteepness"].SetValue(_seaConfig.WaveSteepness);
            _seaEffect.Parameters["WaveSpeed"].SetValue(_seaConfig.WaveSpeed);
            _seaEffect.Parameters["WaveFadeStart"].SetValue(_seaConfig.WaveFadeStart);
            _seaEffect.Parameters["WaveFadeEnd"].SetValue(_seaConfig.WaveFadeEnd);
            _seaEffect.Parameters["ChopAmplitude"].SetValue(_seaConfig.ChopAmplitude);
            _seaEffect.Parameters["ChopFrequency"].SetValue(_seaConfig.ChopFrequency);
            _seaEffect.Parameters["ChopSpeed"].SetValue(_seaConfig.ChopSpeed);
            _seaEffect.Parameters["WindDirection"].SetValue(_seaConfig.Wind.ToVector2());
            _seaEffect.Parameters["SunGlintStrength"].SetValue(_seaConfig.SunGlintStrength);
            _seaEffect.Parameters["SunGlintPower"].SetValue(_seaConfig.SunGlintPower);
            _seaEffect.Parameters["FoamJacobianThreshold"].SetValue(_seaConfig.FoamJacobianThreshold);
            _seaEffect.Parameters["FoamStrength"].SetValue(_seaConfig.FoamStrength);
            _seaEffect.Parameters["FoamCrestStart"].SetValue(_seaConfig.FoamCrestStart);
            _seaEffect.Parameters["FoamCrestStrength"].SetValue(_seaConfig.FoamCrestStrength);
            _seaEffect.Parameters["FoamColor"].SetValue(_seaConfig.FoamColor.ToVector3());
            _seaEffect.Parameters["SssStrength"].SetValue(_seaConfig.SssStrength);
            _seaEffect.Parameters["SssColor"].SetValue(_seaConfig.SssColor.ToVector3());
            _seaEffect.Parameters["HorizonHazeDistance"].SetValue(_seaConfig.HorizonHazeDistance);
        }

        private void ApplyDesertParameters()
        {
            _desertEffect.Parameters["DesertLevelY"].SetValue(_desertConfig.LevelY);
            _desertEffect.Parameters["DuneAmplitude"].SetValue(_desertConfig.DuneAmplitude);
            _desertEffect.Parameters["ClearingRadius"].SetValue(_desertConfig.ClearingRadius);
            _desertEffect.Parameters["ClearingTransition"].SetValue(_desertConfig.ClearingTransition);
            _desertEffect.Parameters["RippleAmplitude"].SetValue(_desertConfig.RippleAmplitude);
            _desertEffect.Parameters["RippleFrequency"].SetValue(_desertConfig.RippleFrequency);
            _desertEffect.Parameters["RippleSpeed"].SetValue(_desertConfig.RippleSpeed);
            _desertEffect.Parameters["DustStrength"].SetValue(_desertConfig.DustStrength);
            _desertEffect.Parameters["DustSpeed"].SetValue(_desertConfig.DustSpeed);
            _desertEffect.Parameters["DustStart"].SetValue(_desertConfig.DustStart);
            _desertEffect.Parameters["SandColor"].SetValue(_desertConfig.SandColor.ToVector3());
            _desertEffect.Parameters["AmbientStrength"].SetValue(_desertConfig.AmbientStrength);
            _desertEffect.Parameters["WindDirection"].SetValue(_desertConfig.Wind.ToVector2());
            _desertEffect.Parameters["HorizonHazeDistance"].SetValue(_desertConfig.HorizonHazeDistance);
        }

        private void ApplySavannaParameters()
        {
            _savannaEffect.Parameters["SavannaLevelY"].SetValue(_savannaConfig.LevelY);
            _savannaEffect.Parameters["HillHeight"].SetValue(_savannaConfig.HillHeight);
            _savannaEffect.Parameters["ClearingRadius"].SetValue(_savannaConfig.ClearingRadius);
            _savannaEffect.Parameters["ClearingTransition"].SetValue(_savannaConfig.ClearingTransition);
            _savannaEffect.Parameters["ClearingRelief"].SetValue(_savannaConfig.ClearingRelief);
            _savannaEffect.Parameters["GrassColor"].SetValue(_savannaConfig.GrassSavanna.ToVector3());
            _savannaEffect.Parameters["GrassColorDry"].SetValue(_savannaConfig.GrassDry.ToVector3());
            _savannaEffect.Parameters["GrassColorBare"].SetValue(_savannaConfig.GrassBare.ToVector3());
            _savannaEffect.Parameters["AmbientStrength"].SetValue(_savannaConfig.AmbientStrength);
            _savannaEffect.Parameters["WindDirection"].SetValue(_savannaConfig.Wind.ToVector2());
            _savannaEffect.Parameters["HorizonHazeDistance"].SetValue(_savannaConfig.HorizonHazeDistance);
            _savannaEffect.Parameters["WindRippleSpeed"].SetValue(_savannaConfig.WindRippleSpeed);
            _savannaEffect.Parameters["WindRippleFrequency"].SetValue(_savannaConfig.WindRippleFrequency);
            _savannaEffect.Parameters["WindRippleStrength"].SetValue(_savannaConfig.WindRippleStrength);
            _savannaEffect.Parameters["GrassReliefStrength"].SetValue(_savannaConfig.GrassReliefStrength);
            _savannaEffect.Parameters["GrassReliefFrequency"].SetValue(_savannaConfig.GrassReliefFrequency);
        }

        private void ApplyAcaciaParameters()
        {
            AcaciaConfig ac = _savannaConfig.Acacia;
            _acaciaEffect.Parameters["TreeWidth"].SetValue(ac.Width);
            _acaciaEffect.Parameters["TreeHeight"].SetValue(ac.Height);
            _acaciaEffect.Parameters["CanopyColor"].SetValue(ac.CanopyColor.ToVector3());
            _acaciaEffect.Parameters["CanopyColorDry"].SetValue(ac.CanopyDry.ToVector3());
            _acaciaEffect.Parameters["TrunkColor"].SetValue(ac.TrunkColor.ToVector3());
        }

        /// <summary>
        /// (Re)builds the acacia scatter: cluster centres, per-plant positions on the terrain and the
        /// billboard buffers. Deterministic seed, so the same config always gives the same savanna.
        /// </summary>
        private void BuildAcaciaBuffers()
        {
            _acaciaVertexBuffer?.Dispose();
            _acaciaIndexBuffer?.Dispose();

            AcaciaConfig ac = _savannaConfig.Acacia;
            Random acaciaRng = new(90125);

            //Cluster centres the plants gather around, so the savanna reads as clumps of trees rather than an
            //even scatter. A minority of plants are placed solo.
            float[] clusterX = new float[ac.Clusters];
            float[] clusterZ = new float[ac.Clusters];
            for (int c = 0; c < ac.Clusters; c++)
            {
                float ca = (float)acaciaRng.NextDouble() * MathHelper.TwoPi;
                float cr = ac.MinRadius + (float)acaciaRng.NextDouble() * (ac.MaxRadius - ac.MinRadius);
                clusterX[c] = MathF.Cos(ca) * cr;
                clusterZ[c] = MathF.Sin(ca) * cr;
            }

            BirdVertex[] acaciaVertices = new BirdVertex[ac.Count * 4];
            for (int i = 0; i < ac.Count; i++)
            {
                float x, z;
                if (acaciaRng.NextDouble() < 0.82) //most plants clump around a cluster centre
                {
                    int c = acaciaRng.Next(ac.Clusters);
                    float off = (float)acaciaRng.NextDouble();
                    float d = off * off * ac.ClusterSpread; //denser towards the centre
                    float da = (float)acaciaRng.NextDouble() * MathHelper.TwoPi;
                    x = clusterX[c] + MathF.Cos(da) * d;
                    z = clusterZ[c] + MathF.Sin(da) * d;
                }
                else //the odd solitary plant, anywhere in the ring
                {
                    float a = (float)acaciaRng.NextDouble() * MathHelper.TwoPi;
                    float r = ac.MinRadius + (float)acaciaRng.NextDouble() * (ac.MaxRadius - ac.MinRadius);
                    x = MathF.Cos(a) * r;
                    z = MathF.Sin(a) * r;
                }

                //Keep clear of the island
                float dist = MathF.Sqrt(x * x + z * z);
                if (dist < ac.MinRadius && dist > 0.01f)
                {
                    x *= ac.MinRadius / dist;
                    z *= ac.MinRadius / dist;
                }

                Vector3 basePos = new(x, SavannaTerrainHeight(x, z), z);

                //Packed random: [0,1) a tree, [1,2) a bush
                float rand = (float)acaciaRng.NextDouble();
                float packed = acaciaRng.NextDouble() < ac.BushFraction ? 1f + rand : rand;

                int v = i * 4;
                acaciaVertices[v] = new BirdVertex(basePos, new Vector3(-1f, 0f, packed));
                acaciaVertices[v + 1] = new BirdVertex(basePos, new Vector3(1f, 0f, packed));
                acaciaVertices[v + 2] = new BirdVertex(basePos, new Vector3(-1f, 1f, packed));
                acaciaVertices[v + 3] = new BirdVertex(basePos, new Vector3(1f, 1f, packed));
            }
            _acaciaVertexBuffer = new VertexBuffer(_graphicsDevice, BirdVertex.Declaration, acaciaVertices.Length, BufferUsage.WriteOnly);
            _acaciaVertexBuffer.SetData(acaciaVertices);

            short[] acaciaIndices = new short[ac.Count * 6];
            for (int i = 0; i < ac.Count; i++)
            {
                int v = i * 4;
                int o = i * 6;
                acaciaIndices[o] = (short)v; acaciaIndices[o + 1] = (short)(v + 1); acaciaIndices[o + 2] = (short)(v + 2);
                acaciaIndices[o + 3] = (short)(v + 2); acaciaIndices[o + 4] = (short)(v + 1); acaciaIndices[o + 5] = (short)(v + 3);
            }
            _acaciaIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, acaciaIndices.Length, BufferUsage.WriteOnly);
            _acaciaIndexBuffer.SetData(acaciaIndices);
        }

        /// <summary>
        /// (Re)builds the shared bird flock. The savanna and desert scenes draw from the one buffer, so it is
        /// sized to the larger of the two configs' counts — otherwise a desert level's flock would be silently
        /// capped to the savanna's count (and vice versa, since neither scene rebuilds on a NumPad2 switch).
        /// <see cref="DrawBirds"/> caps its draw to the active scene's count; colour/size/centre are read per
        /// frame from that scene's Birds config. Deterministic seed, so the flock is the same every run.
        /// </summary>
        private void BuildBirdBuffers()
        {
            _birdVertexBuffer?.Dispose();
            _birdIndexBuffer?.Dispose();

            int birdCount = Math.Max(_savannaConfig.Birds.Count, _desertConfig.Birds.Count);
            _birdVertices = new BirdVertex[birdCount * 4];
            _birdVertexBuffer = new DynamicVertexBuffer(_graphicsDevice, BirdVertex.Declaration, birdCount * 4, BufferUsage.WriteOnly);

            short[] birdIndices = new short[birdCount * 6];
            for (int i = 0; i < birdCount; i++)
            {
                int v = i * 4;
                int o = i * 6;
                birdIndices[o] = (short)v; birdIndices[o + 1] = (short)(v + 2); birdIndices[o + 2] = (short)(v + 1);
                birdIndices[o + 3] = (short)(v + 1); birdIndices[o + 4] = (short)(v + 2); birdIndices[o + 5] = (short)(v + 3);
            }
            _birdIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, birdIndices.Length, BufferUsage.WriteOnly);
            _birdIndexBuffer.SetData(birdIndices);

            _birdRadius = new float[birdCount];
            _birdAltitude = new float[birdCount];
            _birdOrbitSpeed = new float[birdCount];
            _birdOrbitPhase = new float[birdCount];
            _birdFlapSpeed = new float[birdCount];
            _birdFlapPhase = new float[birdCount];
            _birdBobSpeed = new float[birdCount];

            //Deterministic, so the flock is the same every run. All circle the same way, like a kettle of
            //vultures riding one thermal, each at its own radius, height and unhurried pace.
            Random birdRng = new(4242);
            for (int i = 0; i < birdCount; i++)
            {
                _birdRadius[i] = 28f + (float)birdRng.NextDouble() * 34f;
                _birdAltitude[i] = (float)(birdRng.NextDouble() * 2.0 - 1.0) * 10f;
                _birdOrbitSpeed[i] = 0.10f + (float)birdRng.NextDouble() * 0.12f;
                _birdOrbitPhase[i] = (float)birdRng.NextDouble() * MathHelper.TwoPi;
                _birdFlapSpeed[i] = 2.2f + (float)birdRng.NextDouble() * 2.2f;
                _birdFlapPhase[i] = (float)birdRng.NextDouble() * MathHelper.TwoPi;
                _birdBobSpeed[i] = 0.4f + (float)birdRng.NextDouble() * 0.5f;
            }
        }

        private void ApplyMountainParameters()
        {
            _mountainEffect.Parameters["MountainLevelY"].SetValue(_mountainConfig.LevelY);
            _mountainEffect.Parameters["MountainHeight"].SetValue(_mountainConfig.Height);
            _mountainEffect.Parameters["ClearingRadius"].SetValue(_mountainConfig.ClearingRadius);
            _mountainEffect.Parameters["ClearingTransition"].SetValue(_mountainConfig.ClearingTransition);
            _mountainEffect.Parameters["ClearingRelief"].SetValue(_mountainConfig.ClearingRelief);
            _mountainEffect.Parameters["SnowColor"].SetValue(_mountainConfig.SnowColor.ToVector3());
            _mountainEffect.Parameters["RockColor"].SetValue(_mountainConfig.RockColor.ToVector3());
            _mountainEffect.Parameters["RockColorLight"].SetValue(_mountainConfig.RockColorLight.ToVector3());
            _mountainEffect.Parameters["RockSlope"].SetValue(_mountainConfig.RockSlope);
            _mountainEffect.Parameters["SnowSlope"].SetValue(_mountainConfig.SnowSlope);
            _mountainEffect.Parameters["SnowlineLow"].SetValue(_mountainConfig.SnowlineLow);
            _mountainEffect.Parameters["SnowlineHigh"].SetValue(_mountainConfig.SnowlineHigh);
            _mountainEffect.Parameters["RockReliefStrength"].SetValue(_mountainConfig.RockReliefStrength);
            _mountainEffect.Parameters["RockReliefFrequency"].SetValue(_mountainConfig.RockReliefFrequency);
            _mountainEffect.Parameters["AmbientStrength"].SetValue(_mountainConfig.AmbientStrength);
            _mountainEffect.Parameters["HorizonHazeDistance"].SetValue(_mountainConfig.HorizonHazeDistance);
        }

        private void ApplySnowParameters()
        {
            _snowEffect.Parameters["SnowBoxSize"].SetValue(_mountainConfig.Snow.BoxSize.ToVector3());
            _snowEffect.Parameters["SnowFallSpeed"].SetValue(_mountainConfig.Snow.FallSpeed);
            _snowEffect.Parameters["SnowWind"].SetValue(_mountainConfig.Snow.Wind.ToVector2());
            _snowEffect.Parameters["SnowSway"].SetValue(_mountainConfig.Snow.Sway);
            _snowEffect.Parameters["FlakeSize"].SetValue(_mountainConfig.Snow.FlakeSize);
            _snowEffect.Parameters["SnowColor"].SetValue(_mountainConfig.Snow.FlakeColor.ToVector3());
            _snowEffect.Parameters["SnowOpacity"].SetValue(_mountainConfig.Snow.Opacity);
        }

        /// <summary>(Re)builds the snowfall's flake buffer at the config's flake count. Deterministic seed.</summary>
        private void BuildSnowBuffers()
        {
            _snowVertexBuffer?.Dispose();
            _snowIndexBuffer?.Dispose();

            BirdVertex[] snowVertices = new BirdVertex[_mountainConfig.Snow.FlakeCount * 4];
            Random snowRng = new(1207);
            for (int i = 0; i < _mountainConfig.Snow.FlakeCount; i++)
            {
                Vector3 basePosition = new((float)snowRng.NextDouble(), (float)snowRng.NextDouble(), (float)snowRng.NextDouble());
                float rand = (float)snowRng.NextDouble();
                int v = i * 4;
                snowVertices[v] = new BirdVertex(basePosition, new Vector3(-1f, 1f, rand));
                snowVertices[v + 1] = new BirdVertex(basePosition, new Vector3(1f, 1f, rand));
                snowVertices[v + 2] = new BirdVertex(basePosition, new Vector3(-1f, -1f, rand));
                snowVertices[v + 3] = new BirdVertex(basePosition, new Vector3(1f, -1f, rand));
            }
            _snowVertexBuffer = new VertexBuffer(_graphicsDevice, BirdVertex.Declaration, snowVertices.Length, BufferUsage.WriteOnly);
            _snowVertexBuffer.SetData(snowVertices);

            short[] snowIndices = new short[_mountainConfig.Snow.FlakeCount * 6];
            for (int i = 0; i < _mountainConfig.Snow.FlakeCount; i++)
            {
                int v = i * 4;
                int o = i * 6;
                snowIndices[o] = (short)v; snowIndices[o + 1] = (short)(v + 2); snowIndices[o + 2] = (short)(v + 1);
                snowIndices[o + 3] = (short)(v + 1); snowIndices[o + 4] = (short)(v + 2); snowIndices[o + 5] = (short)(v + 3);
            }
            _snowIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, snowIndices.Length, BufferUsage.WriteOnly);
            _snowIndexBuffer.SetData(snowIndices);
        }

        private void ApplySprayParameters()
        {
            _sprayEffect.Parameters["SprayBoxSize"].SetValue(_seaConfig.Spray.BoxSize.ToVector3());
            _sprayEffect.Parameters["SprayLevelY"].SetValue(_seaConfig.LevelY + _seaConfig.Spray.LevelYAboveSea);
            _sprayEffect.Parameters["SprayWind"].SetValue(_seaConfig.Spray.Wind.ToVector2());
            _sprayEffect.Parameters["SprayRise"].SetValue(_seaConfig.Spray.Rise);
            _sprayEffect.Parameters["SprayTurb"].SetValue(_seaConfig.Spray.Turbulence);
            _sprayEffect.Parameters["DropletSize"].SetValue(_seaConfig.Spray.DropletSize);
            _sprayEffect.Parameters["SprayColor"].SetValue(_seaConfig.Spray.Color.ToVector3());
            _sprayEffect.Parameters["SprayOpacity"].SetValue(_seaConfig.Spray.Opacity);
        }

        /// <summary>(Re)builds the spray's particle buffer at the config's particle count. Deterministic seed.</summary>
        private void BuildSprayBuffers()
        {
            _sprayVertexBuffer?.Dispose();
            _sprayIndexBuffer?.Dispose();

            BirdVertex[] sprayVertices = new BirdVertex[_seaConfig.Spray.ParticleCount * 4];
            Random sprayRng = new(5023);
            for (int i = 0; i < _seaConfig.Spray.ParticleCount; i++)
            {
                Vector3 basePosition = new((float)sprayRng.NextDouble(), (float)sprayRng.NextDouble(), (float)sprayRng.NextDouble());
                float rand = (float)sprayRng.NextDouble();
                int v = i * 4;
                sprayVertices[v] = new BirdVertex(basePosition, new Vector3(-1f, 1f, rand));
                sprayVertices[v + 1] = new BirdVertex(basePosition, new Vector3(1f, 1f, rand));
                sprayVertices[v + 2] = new BirdVertex(basePosition, new Vector3(-1f, -1f, rand));
                sprayVertices[v + 3] = new BirdVertex(basePosition, new Vector3(1f, -1f, rand));
            }
            _sprayVertexBuffer = new VertexBuffer(_graphicsDevice, BirdVertex.Declaration, sprayVertices.Length, BufferUsage.WriteOnly);
            _sprayVertexBuffer.SetData(sprayVertices);

            short[] sprayIndices = new short[_seaConfig.Spray.ParticleCount * 6];
            for (int i = 0; i < _seaConfig.Spray.ParticleCount; i++)
            {
                int v = i * 4;
                int o = i * 6;
                sprayIndices[o] = (short)v; sprayIndices[o + 1] = (short)(v + 2); sprayIndices[o + 2] = (short)(v + 1);
                sprayIndices[o + 3] = (short)(v + 1); sprayIndices[o + 4] = (short)(v + 2); sprayIndices[o + 5] = (short)(v + 3);
            }
            _sprayIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, sprayIndices.Length, BufferUsage.WriteOnly);
            _sprayIndexBuffer.SetData(sprayIndices);
        }

        private void ApplyMeadowParameters()
        {
            _meadowEffect.Parameters["MeadowLevelY"].SetValue(_meadowConfig.LevelY);
            _meadowEffect.Parameters["HillHeight"].SetValue(_meadowConfig.HillHeight);
            _meadowEffect.Parameters["ClearingRadius"].SetValue(_meadowConfig.ClearingRadius);
            _meadowEffect.Parameters["ClearingTransition"].SetValue(_meadowConfig.ClearingTransition);
            _meadowEffect.Parameters["ClearingRelief"].SetValue(_meadowConfig.ClearingRelief);
            _meadowEffect.Parameters["GrassColor"].SetValue(_meadowConfig.GrassColor.ToVector3());
            _meadowEffect.Parameters["GrassColorDark"].SetValue(_meadowConfig.GrassColorDark.ToVector3());
            _meadowEffect.Parameters["AmbientStrength"].SetValue(_meadowConfig.AmbientStrength);
            _meadowEffect.Parameters["HorizonHazeDistance"].SetValue(_meadowConfig.HorizonHazeDistance);
            _meadowEffect.Parameters["WindDirection"].SetValue(_meadowConfig.Wind.ToVector2());
            _meadowEffect.Parameters["WindRippleSpeed"].SetValue(_meadowConfig.WindRippleSpeed);
            _meadowEffect.Parameters["WindRippleFrequency"].SetValue(_meadowConfig.WindRippleFrequency);
            _meadowEffect.Parameters["WindRippleStrength"].SetValue(_meadowConfig.WindRippleStrength);
            _meadowEffect.Parameters["GrassReliefStrength"].SetValue(_meadowConfig.GrassReliefStrength);
            _meadowEffect.Parameters["GrassReliefFrequency"].SetValue(_meadowConfig.GrassReliefFrequency);
            _meadowEffect.Parameters["FlowerDensity"].SetValue(_meadowConfig.Flowers.Density);
            _meadowEffect.Parameters["FlowerSpacing"].SetValue(_meadowConfig.Flowers.Spacing);
            _meadowEffect.Parameters["FlowerSize"].SetValue(_meadowConfig.Flowers.Size);
        }

        private void ApplyForestParameters()
        {
            _forestEffect.Parameters["ForestLevelY"].SetValue(_forestConfig.LevelY);
            _forestEffect.Parameters["HillHeight"].SetValue(_forestConfig.HillHeight);
            _forestEffect.Parameters["ClearingRadius"].SetValue(_forestConfig.ClearingRadius);
            _forestEffect.Parameters["ClearingTransition"].SetValue(_forestConfig.ClearingTransition);
            _forestEffect.Parameters["ClearingRelief"].SetValue(_forestConfig.ClearingRelief);
            _forestEffect.Parameters["FloorLumpStrength"].SetValue(_forestConfig.FloorLumpStrength);
            _forestEffect.Parameters["FloorLumpFrequency"].SetValue(_forestConfig.FloorLumpFrequency);
            _forestEffect.Parameters["ForestColor"].SetValue(_forestConfig.ForestColor.ToVector3());
            _forestEffect.Parameters["ForestColorDark"].SetValue(_forestConfig.ForestColorDark.ToVector3());
            _forestEffect.Parameters["TreelineColor"].SetValue(_forestConfig.TreelineColor.ToVector3());
            _forestEffect.Parameters["TreelineStrength"].SetValue(_forestConfig.TreelineStrength);
            _forestEffect.Parameters["AmbientStrength"].SetValue(_forestConfig.AmbientStrength);
            _forestEffect.Parameters["HorizonHazeDistance"].SetValue(_forestConfig.HorizonHazeDistance);
            _forestEffect.Parameters["WindDirection"].SetValue(_forestConfig.Wind.ToVector2());
            _forestEffect.Parameters["WindRippleSpeed"].SetValue(_forestConfig.WindRippleSpeed);
            _forestEffect.Parameters["WindRippleFrequency"].SetValue(_forestConfig.WindRippleFrequency);
            _forestEffect.Parameters["WindRippleStrength"].SetValue(_forestConfig.WindRippleStrength);
            _forestEffect.Parameters["NeedleReliefStrength"].SetValue(_forestConfig.NeedleReliefStrength);
            _forestEffect.Parameters["NeedleReliefFrequency"].SetValue(_forestConfig.NeedleReliefFrequency);
        }

        /// <summary>
        /// Pushes the whole space sky at the shader. Everything here is fixed for as long as the config is —
        /// the sky does not move, there is no wind and no weather — so this runs on a config change and never
        /// per frame; only the camera, the sun and the supersampling factor go out in <see cref="DrawSpace"/>.
        /// <para>
        /// Two conversions happen here rather than in the shader, and both are deliberate. Angles are authored
        /// in <b>degrees</b> and arrive as radians, because a designer types "twelve degrees across". And the
        /// directions are normalized — with the galactic core <b>orthogonalised against the pole</b> — so a
        /// hand-typed pair never has to be exactly perpendicular for the bulge to sit in the plane.
        /// </para>
        /// </summary>
        private void ApplySpaceParameters()
        {
            SpaceSceneConfig space = _spaceConfig;

            _spaceEffect.Parameters["VoidColor"].SetValue(space.VoidColor.ToVector3());

            SpaceStarsConfig stars = space.Stars;
            _spaceEffect.Parameters["StarCellScale"].SetValue(new[] { stars.BrightCellScale, stars.MediumCellScale, stars.FaintCellScale });
            _spaceEffect.Parameters["StarChance"].SetValue(new[] { stars.BrightChance, stars.MediumChance, stars.FaintChance });
            _spaceEffect.Parameters["StarPeak"].SetValue(new[] { stars.BrightPeak, stars.MediumPeak, stars.FaintPeak });
            _spaceEffect.Parameters["StarSpread"].SetValue(stars.Spread);
            _spaceEffect.Parameters["StarFalloff"].SetValue(stars.Falloff);
            _spaceEffect.Parameters["StarSpikeThreshold"].SetValue(stars.SpikeThreshold);
            _spaceEffect.Parameters["StarSpikeLength"].SetValue(stars.SpikeLength);

            SpaceMilkyWayConfig milkyWay = space.MilkyWay;
            Vector3 pole = SafeNormal(milkyWay.Pole.ToVector3(), Vector3.Up);

            //The bulge has to lie in the galactic plane or the band's brightest part sits off it, so whatever
            //was typed is projected onto the plane before it is used. If the two happen to be parallel the
            //projection vanishes, and any direction in the plane will do.
            Vector3 core = milkyWay.CoreDirection.ToVector3() - pole * Vector3.Dot(milkyWay.CoreDirection.ToVector3(), pole);
            core = SafeNormal(core, AnyPerpendicular(pole));

            _spaceEffect.Parameters["GalacticPole"].SetValue(pole);
            _spaceEffect.Parameters["GalacticCore"].SetValue(core);
            _spaceEffect.Parameters["MilkyWayWidth"].SetValue(milkyWay.Width);
            _spaceEffect.Parameters["MilkyWayBrightness"].SetValue(milkyWay.Brightness);
            _spaceEffect.Parameters["MilkyWayColor"].SetValue(milkyWay.Color.ToVector3());
            _spaceEffect.Parameters["MilkyWayCoreColor"].SetValue(milkyWay.CoreColor.ToVector3());
            _spaceEffect.Parameters["MilkyWayDust"].SetValue(milkyWay.Dust);
            _spaceEffect.Parameters["MilkyWayStarBoost"].SetValue(milkyWay.StarBoost);

            SpaceNebulaConfig[] nebulae = { space.NebulaOne, space.NebulaTwo, space.NebulaThree };
            Vector3[] nebulaDirections = new Vector3[nebulae.Length];
            Vector3[] nebulaColors = new Vector3[nebulae.Length];
            Vector4[] nebulaShapes = new Vector4[nebulae.Length];

            for (int i = 0; i < nebulae.Length; i++)
            {
                nebulaDirections[i] = SafeNormal(nebulae[i].Direction.ToVector3(), Vector3.Forward);
                nebulaColors[i] = nebulae[i].Color.ToVector3();
                nebulaShapes[i] = new Vector4(
                    MathHelper.ToRadians(nebulae[i].AngularRadiusDegrees),
                    nebulae[i].Strength,
                    nebulae[i].DetailScale,
                    nebulae[i].Warp);
            }

            _spaceEffect.Parameters["NebulaDirection"].SetValue(nebulaDirections);
            _spaceEffect.Parameters["NebulaColor"].SetValue(nebulaColors);
            _spaceEffect.Parameters["NebulaShape"].SetValue(nebulaShapes);

            SpaceGalaxyConfig galaxies = space.Galaxies;
            _spaceEffect.Parameters["GalaxyCellScale"].SetValue(galaxies.CellScale);
            _spaceEffect.Parameters["GalaxyChance"].SetValue(galaxies.Chance);
            _spaceEffect.Parameters["GalaxySize"].SetValue(MathHelper.ToRadians(galaxies.AngularSizeDegrees));
            _spaceEffect.Parameters["GalaxyBrightness"].SetValue(galaxies.Brightness);
            _spaceEffect.Parameters["GalaxyColor"].SetValue(galaxies.Color.ToVector3());

            SpacePlanetConfig planet = space.Planet;
            _spaceEffect.Parameters["PlanetDirection"].SetValue(SafeNormal(planet.Direction.ToVector3(), Vector3.Forward));
            _spaceEffect.Parameters["PlanetAngularRadius"].SetValue(MathHelper.ToRadians(planet.AngularRadiusDegrees));
            _spaceEffect.Parameters["PlanetAxis"].SetValue(SafeNormal(planet.Axis.ToVector3(), Vector3.Up));
            _spaceEffect.Parameters["PlanetColorLight"].SetValue(planet.ColorLight.ToVector3());
            _spaceEffect.Parameters["PlanetColorDark"].SetValue(planet.ColorDark.ToVector3());
            _spaceEffect.Parameters["PlanetStormColor"].SetValue(planet.StormColor.ToVector3());
            _spaceEffect.Parameters["PlanetRimColor"].SetValue(planet.RimColor.ToVector3());
            _spaceEffect.Parameters["PlanetBandScale"].SetValue(planet.BandScale);
            _spaceEffect.Parameters["PlanetRimStrength"].SetValue(planet.RimStrength);
            _spaceEffect.Parameters["PlanetNightAmbient"].SetValue(planet.NightAmbient);
        }

        private void ApplyDreamParameters()
        {
            DreamSceneConfig dream = _dreamConfig;

            _dreamEffect.Parameters["DeepColor"].SetValue(dream.DeepColor.ToVector3());

            DreamPaletteConfig palette = dream.Palette;
            _dreamEffect.Parameters["PaletteA"].SetValue(palette.A.ToVector3());
            _dreamEffect.Parameters["PaletteB"].SetValue(palette.B.ToVector3());
            _dreamEffect.Parameters["PaletteC"].SetValue(palette.C.ToVector3());
            _dreamEffect.Parameters["PaletteD"].SetValue(palette.D.ToVector3());

            DreamBackgroundConfig background = dream.Background;
            _dreamEffect.Parameters["SwirlScale"].SetValue(background.SwirlScale);
            _dreamEffect.Parameters["SwirlWarp"].SetValue(background.SwirlWarp);
            _dreamEffect.Parameters["SwirlSpeedSlow"].SetValue(background.SpeedSlow);
            _dreamEffect.Parameters["SwirlSpeedFast"].SetValue(background.SpeedFast);
            _dreamEffect.Parameters["RibbonSharpness"].SetValue(background.RibbonSharpness);
            _dreamEffect.Parameters["BackgroundBrightness"].SetValue(background.Brightness);

            DreamShapesConfig shapes = dream.Shapes;
            _dreamEffect.Parameters["ShapeOrbitRadius"].SetValue(shapes.OrbitRadius);
            _dreamEffect.Parameters["ShapeSize"].SetValue(shapes.Size);
            _dreamEffect.Parameters["ShapeMorphSpeed"].SetValue(shapes.MorphSpeed);
            _dreamEffect.Parameters["ShapeEmission"].SetValue(shapes.Emission);
            _dreamEffect.Parameters["ShapeReflection"].SetValue(shapes.Reflection);

            DreamGlowsConfig glows = dream.Glows;
            _dreamEffect.Parameters["OrbRadius"].SetValue(glows.OrbRadius);
            _dreamEffect.Parameters["OrbBrightness"].SetValue(glows.OrbBrightness);
            _dreamEffect.Parameters["SparkBrightness"].SetValue(glows.SparkBrightness);
            _dreamEffect.Parameters["SparkSpeed"].SetValue(glows.SparkSpeed);
        }

        private void ApplyCavernParameters()
        {
            CavernSceneConfig cavern = _cavernConfig;

            CavernRockConfig rock = cavern.Rock;
            _cavernEffect.Parameters["CaveRadius"].SetValue(rock.CaveRadius);
            _cavernEffect.Parameters["CaveCeilingY"].SetValue(rock.CeilingY);
            _cavernEffect.Parameters["RockColor"].SetValue(rock.RockColor.ToVector3());
            _cavernEffect.Parameters["VeinColor"].SetValue(rock.VeinColor.ToVector3());
            _cavernEffect.Parameters["FogColor"].SetValue(rock.FogColor.ToVector3());
            _cavernEffect.Parameters["FogDensity"].SetValue(rock.FogDensity);

            CavernWaterConfig water = cavern.Water;
            _cavernEffect.Parameters["WaterLevelY"].SetValue(water.LevelY);
            _cavernEffect.Parameters["WaterDeepColor"].SetValue(water.DeepColor.ToVector3());
            _cavernEffect.Parameters["WaterGlowColor"].SetValue(water.GlowColor.ToVector3());
            _cavernEffect.Parameters["WaveScale"].SetValue(water.WaveScale);
            _cavernEffect.Parameters["WaveSpeed"].SetValue(water.WaveSpeed);
            _cavernEffect.Parameters["CausticStrength"].SetValue(water.CausticStrength);

            CavernAirConfig air = cavern.Air;
            _cavernEffect.Parameters["GodRayColor"].SetValue(air.GodRayColor.ToVector3());
            _cavernEffect.Parameters["GodRayStrength"].SetValue(air.GodRayStrength);
            _cavernEffect.Parameters["SporeColor"].SetValue(air.SporeColor.ToVector3());
            _cavernEffect.Parameters["SporeBrightness"].SetValue(air.SporeBrightness);

            CavernCrystalConfig crystals = cavern.Crystals;
            _cavernEffect.Parameters["CrystalColorA"].SetValue(crystals.ColorA.ToVector3());
            _cavernEffect.Parameters["CrystalColorB"].SetValue(crystals.ColorB.ToVector3());
            _cavernEffect.Parameters["CrystalEmission"].SetValue(crystals.Emission);
            _cavernEffect.Parameters["CrystalPulseSpeed"].SetValue(crystals.PulseSpeed);
            _cavernEffect.Parameters["CrystalWallLight"].SetValue(crystals.WallLight);
        }

        /// <summary>
        /// Normalizes a config direction, falling back to <paramref name="fallback"/> for the degenerate zero
        /// vector — these are hand-typed values in a JSON file and in a property grid, where a zero is one
        /// keystroke away, and a NaN direction would take the whole sky with it.
        /// </summary>
        private static Vector3 SafeNormal(Vector3 direction, Vector3 fallback) =>
            direction.LengthSquared() > 1e-8f ? Vector3.Normalize(direction) : fallback;

        /// <summary>
        /// Some unit vector perpendicular to <paramref name="axis"/>, mirroring <c>Space.fx</c>'s
        /// <c>BuildFrame</c>: the reference vector is swapped near the pole so the cross product cannot
        /// degenerate, whatever axis the config states. It is a <see cref="SafeNormal"/> fallback that is
        /// itself never zero, which the obvious <c>Cross(axis, Vector3.Right)</c> is not — that one collapses
        /// for an axis along X, and a zero galactic core would flatten the band's whole core gradient rather
        /// than announcing itself.
        /// </summary>
        private static Vector3 AnyPerpendicular(Vector3 axis) =>
            Vector3.Normalize(Vector3.Cross(MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right, axis));

        #endregion

        /// <summary>
        /// Builds a flat lattice grid: <paramref name="n"/> vertices per side over <paramref name="extent"/>,
        /// centred on the origin. The sea, savanna, desert, mountain and meadow shaders recentre it on the
        /// camera and lift it into waves, dunes, peaks or hills; it is drawn CullNone, so the winding does
        /// not matter. Indices are 32-bit: every one of these grids runs past 255 vertices a side, where a
        /// 16-bit index silently wraps (see the inline note below — that wrap has already cost one long hunt).
        /// </summary>
        private void CreateGridMesh(int n, float extent, out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer, out int indexCount)
        {
            float half = extent * Constants.HALF;
            float step = extent / (n - 1);

            VertexPosition[] vertices = new VertexPosition[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    vertices[z * n + x] = new VertexPosition(new Vector3(-half + x * step, 0f, -half + z * step));

            vertexBuffer = new VertexBuffer(_graphicsDevice, VertexPosition.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            //32-bit indices: these grids run to hundreds of vertices a side (the mountain at 360, the savanna
            //at 400 = 160k vertices), well past the 65 536 a 16-bit index can address. A 16-bit index silently
            //wraps at that point, so triangles reference the wrong vertices and stretch into garbage. On a near
            //flat field (sea, savanna) the garbage stays down near the surface and hides; on the mountain's tall
            //peaks it stretched into long dark bands across the whole sky. Wrong cause chased for a while - it
            //looked like a glare/shading artifact - so: a grid over 255 a side MUST use 32-bit indices.
            int[] indices = new int[(n - 1) * (n - 1) * 6];
            int i = 0;
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int a = z * n + x;
                    int b = z * n + x + 1;
                    int c = (z + 1) * n + x;
                    int d = (z + 1) * n + x + 1;

                    indices[i++] = a; indices[i++] = c; indices[i++] = b;
                    indices[i++] = b; indices[i++] = c; indices[i++] = d;
                }

            indexCount = i;
            indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
        }

        /// <summary>
        /// Draws the far environment for a natural scene — the sea, the savanna (with its acacias and birds),
        /// the Sahara dunes (with the same birds), the snowy range, the meadow, the forest floor, or deep space.
        /// A no-op for <see cref="SceneKind.City"/>/<see cref="SceneKind.NeonCity"/>,
        /// which the caller draws itself. Opaque, so it stands in for the city as the thing the arena glass
        /// shows beneath it; it leaves the alpha-blend / back-face-cull state the rest of the opaque scene wants.
        /// <para>
        /// <see cref="SceneKind.Space"/> and <see cref="SceneKind.Dream"/> are the two cases that also touch
        /// the <b>depth</b> state — they are backgrounds rather than geometry, so they draw with
        /// <see cref="DepthStencilState.None"/> — and each restores <see cref="DepthStencilState.Default"/>
        /// on the way out as well.
        /// </para>
        /// </summary>
        public void DrawEnvironment(SceneKind scene, in SceneFrame frame)
        {
            switch (scene)
            {
                case SceneKind.Sea:
                    DrawSea(frame);
                    break;
                case SceneKind.Savanna:
                    DrawSavanna(frame);
                    DrawAcacias(frame);
                    DrawBirds(frame, _savannaConfig.Birds);
                    break;
                case SceneKind.Desert:
                    DrawDesert(frame);
                    DrawBirds(frame, _desertConfig.Birds);
                    break;
                case SceneKind.Mountain:
                    DrawMountain(frame);
                    break;
                case SceneKind.Meadow:
                    DrawMeadow(frame);
                    break;
                case SceneKind.Forest:
                    DrawForest(frame);
                    break;
                case SceneKind.Space:
                    DrawSpace(frame);
                    break;
                case SceneKind.Dream:
                    DrawDream(frame);
                    break;
                case SceneKind.Cavern:
                    DrawCavern(frame);
                    break;
            }
        }

        /// <summary>
        /// Draws the foreground weather that belongs after the opaque scene and the cluster: falling snow in
        /// the mountain scene, blown spray and spindrift in the sea scene. Alpha-blended and depth-read (the
        /// terrain/water and the cluster occlude the particles behind them) but writing no depth. A no-op for
        /// every other scene.
        /// </summary>
        public void DrawOverlays(SceneKind scene, in SceneFrame frame)
        {
            if (scene == SceneKind.Mountain) DrawSnow(frame);
            else if (scene == SceneKind.Sea) DrawSpray(frame);
            else if (scene == SceneKind.Savanna) DrawFlame(frame);
        }

        /// <summary>
        /// Draws the sea: a camera-centred grid (snapped to a cell so the waves do not swim) displaced into
        /// Gerstner swell with foam, subsurface scattering and a Fresnel reflection of the current dome,
        /// shadowed by the same cloud field as the rest of the scene.
        /// </summary>
        private void DrawSea(in SceneFrame frame)
        {
            float cell = SEA_EXTENT / (SEA_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _seaEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _seaEffect.Parameters["View"].SetValue(frame.Camera.View);
            _seaEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _seaEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _seaEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _seaEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _seaEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _seaEffect.Parameters["SeaTime"].SetValue(frame.Time);
            _seaEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_seaEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_seaVertexBuffer);
            _graphicsDevice.Indices = _seaIndexBuffer;
            _seaEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _seaIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the Sahara dune field: the grid pinned to the camera (snapped to a cell so the dunes do not
        /// swim), lifted into dunes with distance and shaded per-pixel (no grid) by the current dome, ripples
        /// and blown dust crawling on the wind, shadowed by the shared cloud field. The desert has no point
        /// lights, so unlike the savanna it sets none.
        /// </summary>
        private void DrawDesert(in SceneFrame frame)
        {
            float cell = DESERT_EXTENT / (DESERT_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _desertEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _desertEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _desertEffect.Parameters["View"].SetValue(frame.Camera.View);
            _desertEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _desertEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _desertEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _desertEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _desertEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _desertEffect.Parameters["DesertTime"].SetValue(frame.Time);
            _desertEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_desertEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_desertVertexBuffer);
            _graphicsDevice.Indices = _desertIndexBuffer;
            _desertEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _desertIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the savanna grassland: the grid pinned to the camera (snapped to a cell so it does not swim),
        /// rolled gently and shaded per-pixel (no grid) by the current dome, shadowed by the shared cloud field.
        /// </summary>
        private void DrawSavanna(in SceneFrame frame)
        {
            float cell = SAVANNA_EXTENT / (SAVANNA_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _savannaEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _savannaEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _savannaEffect.Parameters["View"].SetValue(frame.Camera.View);
            _savannaEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _savannaEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _savannaEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _savannaEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _savannaEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _savannaEffect.Parameters["SavannaTime"].SetValue(frame.Time);
            _savannaEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            //The campfire lights the grass around it (a real point light, present under every dome)
            _savannaLightPos[0] = SavannaCampfirePosition;
            _savannaLightColor[0] = CampfireColor(frame.Time);
            _savannaLightRange[0] = SavannaCampfireRange;
            _savannaEffect.Parameters["SceneLightPosition"].SetValue(_savannaLightPos);
            _savannaEffect.Parameters["SceneLightColor"].SetValue(_savannaLightColor);
            _savannaEffect.Parameters["SceneLightRange"].SetValue(_savannaLightRange);
            _savannaEffect.Parameters["SceneLightCount"].SetValue(1);

            frame.ApplyClouds?.Invoke(_savannaEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_savannaVertexBuffer);
            _graphicsDevice.Indices = _savannaIndexBuffer;
            _savannaEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _savannaIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The savanna terrain height at a world point, mirroring <c>Savanna.fx</c>'s <c>TerrainHeight</c>, so the
        /// acacia trees can be planted on the ground the shader draws.
        /// </summary>
        private float SavannaTerrainHeight(float x, float z)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            float t = MathHelper.Clamp((dist - _savannaConfig.ClearingRadius) / _savannaConfig.ClearingTransition, 0f, 1f);
            float ramp = t * t * (3f - 2f * t); //smoothstep, as in the shader

            float rolling = 0.5f * MathF.Sin(x * 0.016f + z * 0.012f)
                + 0.3f * MathF.Sin(x * -0.011f + z * 0.020f + 1.5f)
                + 0.2f * MathF.Sin(x * 0.026f + z * 0.021f + 3.0f);

            float gentle = _savannaConfig.ClearingRelief * (MathF.Sin(x * 0.04f + z * 0.03f) + 0.6f * MathF.Sin(x * -0.055f + z * 0.048f + 2.1f));

            return _savannaConfig.LevelY + gentle + _savannaConfig.HillHeight * ramp * (rolling * 0.5f + 0.5f);
        }

        /// <summary>
        /// The forest terrain height at a world point, mirroring <see cref="ForestSceneConfig"/>'s
        /// <c>Forest.fx</c> <c>TerrainHeight</c> field. Static and config-taking (rather than reading
        /// <c>_forestConfig</c>) so the forest scatter can plant trees on the ground the shader draws before
        /// the renderer itself exists, and so it stays in step with whatever config the caller holds. Keep this
        /// and the shader's <c>TerrainHeight</c> in the same change: a drift here plants trees underground or
        /// floating, and there is nothing to catch it but the eye.
        /// </summary>
        public static float ForestTerrainHeight(float x, float z, ForestSceneConfig config)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            //GLSL smoothstep(edge0, edge1, x) = hermite over the clamped (x-edge0)/(edge1-edge0). MonoGame's
            //MathHelper.SmoothStep is NOT that: it takes (value1, value2, amount) with amount in 0..1, so
            //passing it the raw distance (hundreds of units) makes the ramp explode and the scatter plants trees
            //thousands of units up. Mirroring the savanna's clamp-then-hermite instead, which matches Forest.fx.
            float t = MathHelper.Clamp((dist - config.ClearingRadius) / config.ClearingTransition, 0f, 1f);
            float ramp = t * t * (3f - 2f * t);

            //The domain warp, five octaves and the lump mask all mirror Forest.fx's TerrainHeight term for
            //term — see there for why each exists. Kept in ONE change with the shader.
            float qx = x + 26f * MathF.Sin(z * 0.011f + 2f);
            float qz = z + 26f * MathF.Sin(x * 0.013f + 5f);

            float rolling = 0.40f * MathF.Sin(qx * 0.020f + qz * 0.015f)
                + 0.26f * MathF.Sin(qx * -0.013f + qz * 0.024f + 1.5f)
                + 0.17f * MathF.Sin(qx * 0.031f + qz * 0.026f + 3.0f)
                + 0.10f * MathF.Sin(qx * 0.056f + qz * -0.041f + 0.7f)
                + 0.07f * MathF.Sin(qx * -0.083f + qz * 0.062f + 2.4f);

            float basin = config.ClearingRelief * MathF.Sin(x * 0.05f + z * 0.035f);

            float f = config.FloorLumpFrequency;
            float mask = 0.55f + 0.45f * MathF.Sin(x * 0.021f + z * -0.017f + 4f);
            float lumps = MathF.Sin(x * f + z * f * 0.7f)
                + 0.5f * MathF.Sin(x * -f * 0.8f + z * f * 1.1f + 2.0f)
                + 0.35f * MathF.Sin(x * f * 1.9f + z * f * 1.4f + 5.1f);
            float lumpHeight = config.FloorLumpStrength * lumps * mask * (1.0f - ramp * 0.5f);

            return config.LevelY + basin + lumpHeight + config.HillHeight * ramp * (rolling * 0.5f + 0.5f);
        }

        /// <summary>
        /// Draws the scattered acacia trees: the static billboard buffer, each tree faced upright towards the
        /// camera and drawn as a flat-topped tree in the shader. Alpha-tested and depth-writing (not blended),
        /// so trees occlude the terrain and each other correctly. Savanna scene only, after the terrain.
        /// </summary>
        private void DrawAcacias(in SceneFrame frame)
        {
            _acaciaEffect.Parameters["View"].SetValue(frame.Camera.View);
            _acaciaEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _acaciaEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _acaciaEffect.Parameters["SunColor"].SetValue(frame.SunColor);
            _acaciaEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _acaciaEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default; //depth write on: alpha-tested foliage
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_acaciaVertexBuffer);
            _graphicsDevice.Indices = _acaciaIndexBuffer;
            _acaciaEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _savannaConfig.Acacia.Count * 2);

            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the campfire's visible flame: one billboard at <see cref="SavannaCampfirePosition"/>, a
        /// procedural flickering flame in the shader, drawn additively and depth-read (the terrain or platform
        /// in front hides it) but writing no depth. The light it casts is a separate scene point light. Savanna
        /// scene only, drawn last with the overlays.
        /// </summary>
        private void DrawFlame(in SceneFrame frame)
        {
            _flameEffect.Parameters["View"].SetValue(frame.Camera.View);
            _flameEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _flameEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _flameEffect.Parameters["FlamePosition"].SetValue(SavannaCampfirePosition);
            _flameEffect.Parameters["FlameSize"].SetValue(_savannaConfig.Campfire.FlameSize);
            _flameEffect.Parameters["FlameTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_flameVertexBuffer);
            _graphicsDevice.Indices = _flameIndexBuffer;
            _flameEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the flock: each bird circles the config's flock centre on its own slow orbit, built into a
        /// camera-facing billboard here and flapped in the shader. Alpha-blended and depth-tested (the terrain
        /// or the platform in front hides one) but writing no depth. Called in the savanna and desert scenes,
        /// which share the one flock buffer; <paramref name="birds"/> is the active scene's flock config and
        /// its count is capped to the shared buffer's size.
        /// </summary>
        private void DrawBirds(in SceneFrame frame, BirdsConfig birds)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);
            Vector3 right = inverseView.Right * (birds.Wingspan * Constants.HALF);
            Vector3 up = inverseView.Up * (birds.Wingspan * Constants.HALF * birds.Aspect);

            Vector3 flockCenter = birds.FlockCenter.ToVector3();
            int count = Math.Min(birds.Count, _birdRadius.Length);
            for (int i = 0; i < count; i++)
            {
                float angle = frame.Time * _birdOrbitSpeed[i] + _birdOrbitPhase[i];
                float bob = MathF.Sin(frame.Time * _birdBobSpeed[i] + _birdOrbitPhase[i]) * birds.Bob;

                Vector3 center = flockCenter + new Vector3(
                    MathF.Cos(angle) * _birdRadius[i],
                    _birdAltitude[i] + bob,
                    MathF.Sin(angle) * _birdRadius[i]);

                float flap = frame.Time * _birdFlapSpeed[i] + _birdFlapPhase[i];

                int v = i * 4;
                _birdVertices[v] = new BirdVertex(center - right + up, new Vector3(-1f, 1f, flap));
                _birdVertices[v + 1] = new BirdVertex(center + right + up, new Vector3(1f, 1f, flap));
                _birdVertices[v + 2] = new BirdVertex(center - right - up, new Vector3(-1f, -1f, flap));
                _birdVertices[v + 3] = new BirdVertex(center + right - up, new Vector3(1f, -1f, flap));
            }

            _birdVertexBuffer.SetData(_birdVertices);

            _birdsEffect.Parameters["BirdColor"].SetValue(birds.Color.ToVector3());
            _birdsEffect.Parameters["View"].SetValue(frame.Camera.View);
            _birdsEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_birdVertexBuffer);
            _graphicsDevice.Indices = _birdIndexBuffer;
            _birdsEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count * 2);

            //Restore the scene block's states for the opaque draws that follow
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the snowy range: the grid pinned to the camera (snapped to a cell so it does not swim),
        /// lifted into a snow basin ringed by peaks and shaded by the current dome, shadowed by the shared
        /// cloud field.
        /// </summary>
        private void DrawMountain(in SceneFrame frame)
        {
            float cell = MOUNTAIN_EXTENT / (MOUNTAIN_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _mountainEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _mountainEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _mountainEffect.Parameters["View"].SetValue(frame.Camera.View);
            _mountainEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _mountainEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _mountainEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _mountainEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _mountainEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _mountainEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_mountainEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_mountainVertexBuffer);
            _graphicsDevice.Indices = _mountainIndexBuffer;
            _mountainEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _mountainIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the falling snow: the static flake buffer animated in the shader, in a box that follows the
        /// camera. Alpha-blended and depth-read (so the terrain and the cluster occlude the flakes behind
        /// them) but writing no depth. Mountain scene only.
        /// </summary>
        private void DrawSnow(in SceneFrame frame)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _snowEffect.Parameters["View"].SetValue(frame.Camera.View);
            _snowEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _snowEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _snowEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _snowEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _snowEffect.Parameters["SnowTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_snowVertexBuffer);
            _graphicsDevice.Indices = _snowIndexBuffer;
            _snowEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _mountainConfig.Snow.FlakeCount * 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the sea's blown spray and spindrift: the static billboard buffer animated in the shader, in a
        /// thin slab that follows the camera in XZ but clings to the water surface in Y. Alpha-blended and
        /// depth-read (the waves and the platform occlude the particles behind them) but writing no depth. Sea
        /// scene only.
        /// </summary>
        private void DrawSpray(in SceneFrame frame)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _sprayEffect.Parameters["View"].SetValue(frame.Camera.View);
            _sprayEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _sprayEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _sprayEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _sprayEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _sprayEffect.Parameters["SprayTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_sprayVertexBuffer);
            _graphicsDevice.Indices = _sprayIndexBuffer;
            _sprayEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _seaConfig.Spray.ParticleCount * 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the meadow: the grid pinned to the camera (snapped so it does not swim), rolling green hills
        /// scattered with flowers, wind combing the grass, shadowed by the shared cloud field.
        /// </summary>
        private void DrawMeadow(in SceneFrame frame)
        {
            float cell = MEADOW_EXTENT / (MEADOW_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _meadowEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _meadowEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _meadowEffect.Parameters["View"].SetValue(frame.Camera.View);
            _meadowEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _meadowEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _meadowEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _meadowEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _meadowEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _meadowEffect.Parameters["MeadowTime"].SetValue(frame.Time);
            _meadowEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_meadowEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_meadowVertexBuffer);
            _graphicsDevice.Indices = _meadowIndexBuffer;
            _meadowEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _meadowIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        private void DrawForest(in SceneFrame frame)
        {
            float cell = FOREST_EXTENT / (FOREST_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _forestEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _forestEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _forestEffect.Parameters["View"].SetValue(frame.Camera.View);
            _forestEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _forestEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _forestEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _forestEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _forestEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _forestEffect.Parameters["ForestTime"].SetValue(frame.Time);
            _forestEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_forestEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_forestVertexBuffer);
            _graphicsDevice.Indices = _forestIndexBuffer;
            _forestEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _forestIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws deep space: one full-screen pass over a quad already in normalized device coordinates, the
        /// view ray recovered per pixel from the inverse view-projection. The odd one out among these draws,
        /// and every difference follows from replacing the <b>sky</b> rather than the ground:
        /// <list type="bullet">
        /// <item>No grid, no camera snapping and no <c>OriginXZ</c> — there is nothing on the ground to swim.</item>
        /// <item>No <c>IslandHoleRadius</c> — nothing is cut out, because nothing is drawn under the island.</item>
        /// <item>No cloud hook — space has no weather, and the caller suppresses the cloud shadow on the
        /// instanced effect so the island and the balls are not crossed by a deck that is not drawn.</item>
        /// <item><see cref="DepthStencilState.None"/> rather than the usual depth-writing opaque draw: this is
        /// the background, so it writes no depth and everything drawn after it simply covers it.</item>
        /// </list>
        /// </summary>
        private void DrawSpace(in SceneFrame frame)
        {
            //Row vectors, as everywhere else in this project: a world point goes out through View then
            //Projection, so a clip-space corner comes back through the inverse of that product.
            _spaceInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _spaceCameraPosition.SetValue(frame.Camera.Position);
            _spaceSunDirection.SetValue(frame.SunDirection);
            _spaceSupersample.SetValue((float)SupersampleFactor);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_spaceQuad);
            _spaceEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            //Put back what the rest of the opaque scene wants. The depth state especially: left at None, the
            //island would not occlude the cluster and the whole frame would draw in submission order.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the dream: the second sky-replacing pass, over space's own quad. Everything animated in it
        /// runs off the frame's wall-clock time — the marbling, the tumbling solids, the orbs' breathing and
        /// the sparks keep moving while the simulation is paused, like the clouds and the balls' pulse.
        /// </summary>
        private void DrawDream(in SceneFrame frame)
        {
            _dreamInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _dreamCameraPosition.SetValue(frame.Camera.Position);
            _dreamTime.SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_spaceQuad);
            _dreamEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            //DrawSpace's rule: the depth state left at None would draw the rest of the frame in submission order.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the cavern: the third sky-replacing pass, over space's own quad. Everything animated —
        /// the river, the god rays' breath, the crystals' pulse, the rising spores — runs off the frame's
        /// wall-clock time, so the cave keeps living while the simulation is paused.
        /// </summary>
        private void DrawCavern(in SceneFrame frame)
        {
            _cavernInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _cavernCameraPosition.SetValue(frame.Camera.Position);
            _cavernTime.SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_spaceQuad);
            _cavernEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            //DrawSpace's rule: the depth state left at None would draw the rest of the frame in submission order.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        public void Dispose()
        {
            _spaceQuad?.Dispose();

            _seaVertexBuffer?.Dispose();
            _seaIndexBuffer?.Dispose();
            _desertVertexBuffer?.Dispose();
            _desertIndexBuffer?.Dispose();
            _savannaVertexBuffer?.Dispose();
            _savannaIndexBuffer?.Dispose();
            _acaciaVertexBuffer?.Dispose();
            _acaciaIndexBuffer?.Dispose();
            _flameVertexBuffer?.Dispose();
            _flameIndexBuffer?.Dispose();
            _birdVertexBuffer?.Dispose();
            _birdIndexBuffer?.Dispose();
            _mountainVertexBuffer?.Dispose();
            _mountainIndexBuffer?.Dispose();
            _snowVertexBuffer?.Dispose();
            _snowIndexBuffer?.Dispose();
            _sprayVertexBuffer?.Dispose();
            _sprayIndexBuffer?.Dispose();
            _meadowVertexBuffer?.Dispose();
            _meadowIndexBuffer?.Dispose();
            _forestVertexBuffer?.Dispose();
            _forestIndexBuffer?.Dispose();
        }
    }
}
