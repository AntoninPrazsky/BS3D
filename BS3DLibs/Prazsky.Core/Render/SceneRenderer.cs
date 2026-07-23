using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Which environment the arena stands in. City is the default; Sea, Savanna, Desert, Mountain, Meadow and
    /// NeonCity swap the city (and only the city) for open water, a savanna, a Sahara of dunes, a snowy range,
    /// a flowering meadow, or the same city lit up in neon. Both the game and the map editor cycle these.
    /// </summary>
    public enum SceneKind { City, Sea, Savanna, Desert, Mountain, Meadow, NeonCity }

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

        //Scene configuration. Defaults reproduce the current look byte-for-byte; the desert, mountain and
        //meadow scenes read their tuning from these instead of constants. A runtime setter/apply (for a
        //loaded level or the live editor) is wired under issue #32; the remaining scenes follow.
        private readonly DesertSceneConfig _desertConfig = new();
        private readonly MountainSceneConfig _mountainConfig = new();
        private readonly MeadowSceneConfig _meadowConfig = new();

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

        /// <summary>
        /// Mean water level. Lowered well below the platform (its recessed glass reaches to about -10.7) so a
        /// rough sea has headroom for its crests without them poking up through the panels — the arena now
        /// stands a little above churning water rather than sitting right at a mirror-calm waterline.
        /// </summary>
        private const float SEA_LEVEL_Y = -13f;

        //Deep body and the paler up-facing shade (linear reflectances, multiplied by skylight in the shader).
        //Darker and greener than the calm version, for storm water.
        private static readonly Vector3 WATER_COLOR_DEEP = new(0.02f, 0.05f, 0.07f);
        private static readonly Vector3 WATER_COLOR_SHALLOW = new(0.07f, 0.17f, 0.19f);

        //Dominant swell height (world units), crest sharpness (0..1 Gerstner steepness) and a speed multiplier
        //on the dispersion-derived wave speed. Amplitude is kept so the tallest crests stay below the platform.
        private const float WAVE_AMPLITUDE = 0.6f;
        private const float WAVE_STEEPNESS = 0.95f;
        private const float WAVE_SPEED = 1.2f;

        //Waves fade to flat between these camera distances. Kept out to nearly the grid half-extent (800) so
        //the far sea keeps a wavy, foam-flecked horizon rather than melting into a flat line too early.
        private const float WAVE_FADE_START = 340f;
        private const float WAVE_FADE_END = 780f;

        //Fine wind chop layered per pixel over the swell: peak height, ripples per world unit, scroll speed,
        //and the wind it crawls along (a roughly unit direction in XZ). Strong, so the surface is broken and
        //rough rather than a smooth mirror between the crests.
        private const float CHOP_AMPLITUDE = 0.16f;
        private const float CHOP_FREQUENCY = 1.0f;
        private const float CHOP_SPEED = 1.6f;
        private static readonly Vector2 SEA_WIND = new(0.94f, 0.34f);

        private const float SUN_GLINT_STRENGTH = 12f;
        private const float SUN_GLINT_POWER = 500f;

        //Whitecap foam: how far the wave Jacobian must fold before foam shows (nearer 1 = more), that fold
        //foam's strength, where on a crest height-driven foam begins (0..1) and its strength, and the foam
        //color. Tuned heavy for a storm - foam over much of the upper crest and wherever the waves pinch.
        private const float FOAM_JACOBIAN_THRESHOLD = 0.5f;
        private const float FOAM_STRENGTH = 1.1f;
        private const float FOAM_CREST_START = 0.72f;
        private const float FOAM_CREST_STRENGTH = 0.7f;
        private static readonly Vector3 FOAM_COLOR = new(0.8f, 0.85f, 0.9f);

        //Subsurface scattering: strength of the crest glow when the sun is behind a wave, and the warm
        //green-blue that light takes coming through the water.
        private const float SSS_STRENGTH = 0.7f;
        private static readonly Vector3 SSS_COLOR = new(0.15f, 0.55f, 0.50f);

        private const float SEA_HORIZON_HAZE_DISTANCE = 700f;

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
        private const float SAVANNA_LEVEL_Y = -13.5f;
        private const float SAVANNA_HILL_HEIGHT = 34f;
        private const float SAVANNA_CLEARING_RADIUS = 90f;
        private const float SAVANNA_CLEARING_TRANSITION = 130f;
        private const float SAVANNA_CLEARING_RELIEF = 1.6f;

        //Grass (linear): a green flush, a drier golden base and bare reddish earth, blended in patches. A
        //savanna is dry gold with green flushes and scuffed earth, so the dry tone dominates. Plus how much sky
        //fills the flats, and the horizon fade.
        private static readonly Vector3 GRASS_COLOR_SAVANNA = new(0.13f, 0.33f, 0.06f);   //lush green (dominant now)
        private static readonly Vector3 GRASS_COLOR_DRY = new(0.40f, 0.31f, 0.10f);       //dry golden grass (patches)
        private static readonly Vector3 GRASS_COLOR_BARE = new(0.26f, 0.15f, 0.08f);      //bare reddish earth
        private const float SAVANNA_AMBIENT_STRENGTH = 0.7f;
        private static readonly Vector2 SAVANNA_WIND = new(0.86f, 0.51f);
        private const float SAVANNA_HORIZON_HAZE_DISTANCE = 520f;

        //Wind combing the grass, and the fine grass texture (a normal-tilting height field)
        private const float SAVANNA_WIND_RIPPLE_SPEED = 1.2f;
        private const float SAVANNA_WIND_RIPPLE_FREQUENCY = 0.14f;
        private const float SAVANNA_WIND_RIPPLE_STRENGTH = 0.1f;
        private const float SAVANNA_GRASS_RELIEF_STRENGTH = 0.05f;
        private const float SAVANNA_GRASS_RELIEF_FREQUENCY = 2f;

        #endregion

        #region Acacia (savanna scene only)

        private readonly Effect _acaciaEffect;
        private readonly VertexBuffer _acaciaVertexBuffer;
        private readonly IndexBuffer _acaciaIndexBuffer;

        //Scattered acacia trees and low bushes over the savanna: upright billboards positioned on the ground
        //here, drawn as a flat-topped tree or a rounded clump in the shader. Alpha-tested and depth-writing, so
        //they occlude the terrain and each other. Plants gather in clumps (as they do on a real savanna) around
        //a set of cluster centres, with a few solitary ones. One buffer carries both: the packed random is
        //[0,1) for a tree and [1,2) for a bush.
        private const int ACACIA_COUNT = 120;
        private const float ACACIA_BUSH_FRACTION = 0.45f;
        private const float ACACIA_WIDTH = 6f;     //base half-width of a tree crown
        private const float ACACIA_HEIGHT = 9f;
        private const float ACACIA_MIN_RADIUS = 42f;   //clear of the island
        private const float ACACIA_MAX_RADIUS = 340f;
        private const int ACACIA_CLUSTERS = 24;
        private const float ACACIA_CLUSTER_SPREAD = 30f;
        private static readonly Vector3 ACACIA_CANOPY_COLOR = new(0.09f, 0.17f, 0.05f); //acacia green (linear)
        private static readonly Vector3 ACACIA_CANOPY_DRY = new(0.22f, 0.22f, 0.07f);   //drier yellow-green
        private static readonly Vector3 ACACIA_TRUNK_COLOR = new(0.09f, 0.06f, 0.035f); //dark brown (linear)

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
        public static readonly Vector3 SavannaCampfirePosition = new(28f, SavannaTerrainHeight(28f, -18f) + 0.2f, -18f);
        public const float SAVANNA_CAMPFIRE_RANGE = 32f;
        private const float FLAME_SIZE = 2.3f;
        //Warm fire colour in LINEAR radiance, kept bright (over 1) so it casts real warm light, not a tint
        private static readonly Vector3 CAMPFIRE_BASE_COLOR = new(2.4f, 1.0f, 0.32f);

        /// <summary>The flickering campfire colour at a wall-clock time, so the grass light, the balls light
        /// and the flame all pulse together.</summary>
        public static Vector3 CampfireColor(float time)
        {
            float flicker = 0.72f + 0.28f * (0.5f * MathF.Sin(time * 11f) + 0.3f * MathF.Sin(time * 17f + 1.3f) + 0.2f * MathF.Sin(time * 7f));
            return CAMPFIRE_BASE_COLOR * flicker;
        }

        #endregion

        #region Birds (savanna and desert scenes)

        private readonly Effect _birdsEffect;
        private readonly DynamicVertexBuffer _birdVertexBuffer;
        private readonly IndexBuffer _birdIndexBuffer;
        private readonly BirdVertex[] _birdVertices;

        private readonly float[] _birdRadius, _birdAltitude, _birdOrbitSpeed, _birdOrbitPhase, _birdFlapSpeed, _birdFlapPhase, _birdBobSpeed;

        private const int BIRD_COUNT = 9;
        private const float BIRD_WINGSPAN = 6f;
        private const float BIRD_ASPECT = 0.55f; //Height of the billboard as a fraction of its width
        private const float BIRD_BOB = 2.5f; //How far a bird drifts up and down over its circle
        private static readonly Vector3 BIRD_COLOR = new(0.02f, 0.017f, 0.014f); //Near-black silhouette (linear)

        //Where the flock circles: a fixed point out over the dunes, well above the cluster so the birds sit
        //against the sky. Each bird orbits it at its own radius, altitude, phase and (slow) speed.
        private static readonly Vector3 BIRD_FLOCK_CENTER = new(-20f, 34f, -75f);

        //One camera-facing quad per bird; Data carries (u along the wingspan, v vertical, flap phase).
        //Also reused for the snow flakes.
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
        private readonly VertexBuffer _snowVertexBuffer;
        private readonly IndexBuffer _snowIndexBuffer;

        //Snowfall parameters (flake count/size/colour/opacity, box, fall speed, wind, sway) now live in
        //MountainSceneConfig.Snow (SnowConfig); SceneRenderer reads them from _mountainConfig.Snow.

        #endregion

        #region Spray (sea scene only)

        private readonly Effect _sprayEffect;
        private readonly VertexBuffer _sprayVertexBuffer;
        private readonly IndexBuffer _sprayIndexBuffer;

        //Blown spray and spindrift over the sea: a thin slab of billboards hugging the water, whipped downwind
        //by the storm. One buffer, two reads (fine droplets + faint mist wisps), split per particle in the shader.
        private const int SPRAY_PARTICLE_COUNT = 2000;
        //Wide slab in XZ (follows the camera), thin in Y - the spray clings to the surface rather than filling a volume
        private static readonly Vector3 SPRAY_BOX_SIZE = new(200f, 16f, 200f);
        //Centred just above the mean sea level (SEA_LEVEL_Y) so the spray sits on the water at any camera height
        private const float SPRAY_LEVEL_Y = SEA_LEVEL_Y + 2f;
        //Strong downwind drift, aligned with the sea's own wind (SEA_WIND ~ (0.94, 0.34)) but much faster
        private static readonly Vector2 SPRAY_WIND = new(30f, 11f);
        private const float SPRAY_RISE = 3f;   //slow vertical churn
        private const float SPRAY_TURB = 1.6f; //per-particle turbulent sway
        private const float SPRAY_DROPLET_SIZE = 0.12f;
        //Cool grey-blue, its LUMINANCE deliberately just under GLARE_THRESHOLD (0.38). At a grazing angle the
        //view ray threads hundreds of particles through the thin slab and they stack to full opacity, so any
        //colour above the threshold - however low the per-particle alpha - blooms into a starfield once it
        //accumulates. A glare-safe colour is the only robust fix in the HDR pass: the spray reads as cold
        //storm haze/spindrift rather than white foam-spray, but it never glares. (A brighter, whiter spray
        //would need a higher glare threshold or a post-resolve pass - see CLAUDE.md.)
        private static readonly Vector3 SPRAY_COLOR = new(0.33f, 0.37f, 0.43f);
        private const float SPRAY_OPACITY = 0.38f;

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

            _seaEffect.Parameters["SeaLevelY"].SetValue(SEA_LEVEL_Y);
            _seaEffect.Parameters["WaterColorDeep"].SetValue(WATER_COLOR_DEEP);
            _seaEffect.Parameters["WaterColorShallow"].SetValue(WATER_COLOR_SHALLOW);
            _seaEffect.Parameters["WaveAmplitude"].SetValue(WAVE_AMPLITUDE);
            _seaEffect.Parameters["WaveSteepness"].SetValue(WAVE_STEEPNESS);
            _seaEffect.Parameters["WaveSpeed"].SetValue(WAVE_SPEED);
            _seaEffect.Parameters["WaveFadeStart"].SetValue(WAVE_FADE_START);
            _seaEffect.Parameters["WaveFadeEnd"].SetValue(WAVE_FADE_END);
            _seaEffect.Parameters["ChopAmplitude"].SetValue(CHOP_AMPLITUDE);
            _seaEffect.Parameters["ChopFrequency"].SetValue(CHOP_FREQUENCY);
            _seaEffect.Parameters["ChopSpeed"].SetValue(CHOP_SPEED);
            _seaEffect.Parameters["WindDirection"].SetValue(SEA_WIND);
            _seaEffect.Parameters["SunGlintStrength"].SetValue(SUN_GLINT_STRENGTH);
            _seaEffect.Parameters["SunGlintPower"].SetValue(SUN_GLINT_POWER);
            _seaEffect.Parameters["FoamJacobianThreshold"].SetValue(FOAM_JACOBIAN_THRESHOLD);
            _seaEffect.Parameters["FoamStrength"].SetValue(FOAM_STRENGTH);
            _seaEffect.Parameters["FoamCrestStart"].SetValue(FOAM_CREST_START);
            _seaEffect.Parameters["FoamCrestStrength"].SetValue(FOAM_CREST_STRENGTH);
            _seaEffect.Parameters["FoamColor"].SetValue(FOAM_COLOR);
            _seaEffect.Parameters["SssStrength"].SetValue(SSS_STRENGTH);
            _seaEffect.Parameters["SssColor"].SetValue(SSS_COLOR);
            _seaEffect.Parameters["HorizonHazeDistance"].SetValue(SEA_HORIZON_HAZE_DISTANCE);

            //--- Desert: a flat lattice the shader displaces into Sahara dunes (per-pixel normal, no grid)
            _desertEffect = content.Load<Effect>("Shaders/Desert");
            CreateGridMesh(DESERT_GRID_N, DESERT_EXTENT, out _desertVertexBuffer, out _desertIndexBuffer, out _desertIndexCount);

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

            //--- Savanna: a flat lattice the shader displaces into gentle grassland (per-pixel normal, no grid)
            _savannaEffect = content.Load<Effect>("Shaders/Savanna");
            CreateGridMesh(SAVANNA_GRID_N, SAVANNA_EXTENT, out _savannaVertexBuffer, out _savannaIndexBuffer, out _savannaIndexCount);

            _savannaEffect.Parameters["SavannaLevelY"].SetValue(SAVANNA_LEVEL_Y);
            _savannaEffect.Parameters["HillHeight"].SetValue(SAVANNA_HILL_HEIGHT);
            _savannaEffect.Parameters["ClearingRadius"].SetValue(SAVANNA_CLEARING_RADIUS);
            _savannaEffect.Parameters["ClearingTransition"].SetValue(SAVANNA_CLEARING_TRANSITION);
            _savannaEffect.Parameters["ClearingRelief"].SetValue(SAVANNA_CLEARING_RELIEF);
            _savannaEffect.Parameters["GrassColor"].SetValue(GRASS_COLOR_SAVANNA);
            _savannaEffect.Parameters["GrassColorDry"].SetValue(GRASS_COLOR_DRY);
            _savannaEffect.Parameters["GrassColorBare"].SetValue(GRASS_COLOR_BARE);
            _savannaEffect.Parameters["AmbientStrength"].SetValue(SAVANNA_AMBIENT_STRENGTH);
            _savannaEffect.Parameters["WindDirection"].SetValue(SAVANNA_WIND);
            _savannaEffect.Parameters["HorizonHazeDistance"].SetValue(SAVANNA_HORIZON_HAZE_DISTANCE);
            _savannaEffect.Parameters["WindRippleSpeed"].SetValue(SAVANNA_WIND_RIPPLE_SPEED);
            _savannaEffect.Parameters["WindRippleFrequency"].SetValue(SAVANNA_WIND_RIPPLE_FREQUENCY);
            _savannaEffect.Parameters["WindRippleStrength"].SetValue(SAVANNA_WIND_RIPPLE_STRENGTH);
            _savannaEffect.Parameters["GrassReliefStrength"].SetValue(SAVANNA_GRASS_RELIEF_STRENGTH);
            _savannaEffect.Parameters["GrassReliefFrequency"].SetValue(SAVANNA_GRASS_RELIEF_FREQUENCY);

            //--- Acacia: a static billboard buffer of trees scattered over the savanna, positioned on the
            //ground (SavannaTerrainHeight mirrors the shader's field) and drawn as a flat-topped tree in Acacia.fx
            _acaciaEffect = content.Load<Effect>("Shaders/Acacia");
            _acaciaEffect.Parameters["TreeWidth"].SetValue(ACACIA_WIDTH);
            _acaciaEffect.Parameters["TreeHeight"].SetValue(ACACIA_HEIGHT);
            _acaciaEffect.Parameters["CanopyColor"].SetValue(ACACIA_CANOPY_COLOR);
            _acaciaEffect.Parameters["CanopyColorDry"].SetValue(ACACIA_CANOPY_DRY);
            _acaciaEffect.Parameters["TrunkColor"].SetValue(ACACIA_TRUNK_COLOR);

            Random acaciaRng = new(90125);

            //Cluster centres the plants gather around, so the savanna reads as clumps of trees rather than an
            //even scatter. A minority of plants are placed solo.
            float[] clusterX = new float[ACACIA_CLUSTERS];
            float[] clusterZ = new float[ACACIA_CLUSTERS];
            for (int c = 0; c < ACACIA_CLUSTERS; c++)
            {
                float ca = (float)acaciaRng.NextDouble() * MathHelper.TwoPi;
                float cr = ACACIA_MIN_RADIUS + (float)acaciaRng.NextDouble() * (ACACIA_MAX_RADIUS - ACACIA_MIN_RADIUS);
                clusterX[c] = MathF.Cos(ca) * cr;
                clusterZ[c] = MathF.Sin(ca) * cr;
            }

            BirdVertex[] acaciaVertices = new BirdVertex[ACACIA_COUNT * 4];
            for (int i = 0; i < ACACIA_COUNT; i++)
            {
                float x, z;
                if (acaciaRng.NextDouble() < 0.82) //most plants clump around a cluster centre
                {
                    int c = acaciaRng.Next(ACACIA_CLUSTERS);
                    float off = (float)acaciaRng.NextDouble();
                    float d = off * off * ACACIA_CLUSTER_SPREAD; //denser towards the centre
                    float da = (float)acaciaRng.NextDouble() * MathHelper.TwoPi;
                    x = clusterX[c] + MathF.Cos(da) * d;
                    z = clusterZ[c] + MathF.Sin(da) * d;
                }
                else //the odd solitary plant, anywhere in the ring
                {
                    float a = (float)acaciaRng.NextDouble() * MathHelper.TwoPi;
                    float r = ACACIA_MIN_RADIUS + (float)acaciaRng.NextDouble() * (ACACIA_MAX_RADIUS - ACACIA_MIN_RADIUS);
                    x = MathF.Cos(a) * r;
                    z = MathF.Sin(a) * r;
                }

                //Keep clear of the island
                float dist = MathF.Sqrt(x * x + z * z);
                if (dist < ACACIA_MIN_RADIUS && dist > 0.01f)
                {
                    x *= ACACIA_MIN_RADIUS / dist;
                    z *= ACACIA_MIN_RADIUS / dist;
                }

                Vector3 basePos = new(x, SavannaTerrainHeight(x, z), z);

                //Packed random: [0,1) a tree, [1,2) a bush
                float rand = (float)acaciaRng.NextDouble();
                float packed = acaciaRng.NextDouble() < ACACIA_BUSH_FRACTION ? 1f + rand : rand;

                int v = i * 4;
                acaciaVertices[v] = new BirdVertex(basePos, new Vector3(-1f, 0f, packed));
                acaciaVertices[v + 1] = new BirdVertex(basePos, new Vector3(1f, 0f, packed));
                acaciaVertices[v + 2] = new BirdVertex(basePos, new Vector3(-1f, 1f, packed));
                acaciaVertices[v + 3] = new BirdVertex(basePos, new Vector3(1f, 1f, packed));
            }
            _acaciaVertexBuffer = new VertexBuffer(graphicsDevice, BirdVertex.Declaration, acaciaVertices.Length, BufferUsage.WriteOnly);
            _acaciaVertexBuffer.SetData(acaciaVertices);

            short[] acaciaIndices = new short[ACACIA_COUNT * 6];
            for (int i = 0; i < ACACIA_COUNT; i++)
            {
                int v = i * 4;
                int o = i * 6;
                acaciaIndices[o] = (short)v; acaciaIndices[o + 1] = (short)(v + 1); acaciaIndices[o + 2] = (short)(v + 2);
                acaciaIndices[o + 3] = (short)(v + 2); acaciaIndices[o + 4] = (short)(v + 1); acaciaIndices[o + 5] = (short)(v + 3);
            }
            _acaciaIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, acaciaIndices.Length, BufferUsage.WriteOnly);
            _acaciaIndexBuffer.SetData(acaciaIndices);

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
            _birdsEffect.Parameters["BirdColor"].SetValue(BIRD_COLOR);

            _birdVertices = new BirdVertex[BIRD_COUNT * 4];
            _birdVertexBuffer = new DynamicVertexBuffer(graphicsDevice, BirdVertex.Declaration, BIRD_COUNT * 4, BufferUsage.WriteOnly);

            short[] birdIndices = new short[BIRD_COUNT * 6];
            for (int i = 0; i < BIRD_COUNT; i++)
            {
                int v = i * 4;
                int o = i * 6;
                birdIndices[o] = (short)v; birdIndices[o + 1] = (short)(v + 2); birdIndices[o + 2] = (short)(v + 1);
                birdIndices[o + 3] = (short)(v + 1); birdIndices[o + 4] = (short)(v + 2); birdIndices[o + 5] = (short)(v + 3);
            }
            _birdIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, birdIndices.Length, BufferUsage.WriteOnly);
            _birdIndexBuffer.SetData(birdIndices);

            _birdRadius = new float[BIRD_COUNT];
            _birdAltitude = new float[BIRD_COUNT];
            _birdOrbitSpeed = new float[BIRD_COUNT];
            _birdOrbitPhase = new float[BIRD_COUNT];
            _birdFlapSpeed = new float[BIRD_COUNT];
            _birdFlapPhase = new float[BIRD_COUNT];
            _birdBobSpeed = new float[BIRD_COUNT];

            //Deterministic, so the flock is the same every run. All circle the same way, like a kettle of
            //vultures riding one thermal, each at its own radius, height and unhurried pace.
            Random birdRng = new(4242);
            for (int i = 0; i < BIRD_COUNT; i++)
            {
                _birdRadius[i] = 28f + (float)birdRng.NextDouble() * 34f;
                _birdAltitude[i] = (float)(birdRng.NextDouble() * 2.0 - 1.0) * 10f;
                _birdOrbitSpeed[i] = 0.10f + (float)birdRng.NextDouble() * 0.12f;
                _birdOrbitPhase[i] = (float)birdRng.NextDouble() * MathHelper.TwoPi;
                _birdFlapSpeed[i] = 2.2f + (float)birdRng.NextDouble() * 2.2f;
                _birdFlapPhase[i] = (float)birdRng.NextDouble() * MathHelper.TwoPi;
                _birdBobSpeed[i] = 0.4f + (float)birdRng.NextDouble() * 0.5f;
            }

            //--- Mountain: a ridged displaced grid
            _mountainEffect = content.Load<Effect>("Shaders/Mountain");
            CreateGridMesh(MOUNTAIN_GRID_N, MOUNTAIN_EXTENT, out _mountainVertexBuffer, out _mountainIndexBuffer, out _mountainIndexCount);

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

            //--- Snow: a static flake buffer, one quad per flake at a fixed point in the unit cube, animated
            //entirely in the shader (so it is never rebuilt). Reuses the position+data billboard vertex.
            _snowEffect = content.Load<Effect>("Shaders/Snow");
            _snowEffect.Parameters["SnowBoxSize"].SetValue(_mountainConfig.Snow.BoxSize.ToVector3());
            _snowEffect.Parameters["SnowFallSpeed"].SetValue(_mountainConfig.Snow.FallSpeed);
            _snowEffect.Parameters["SnowWind"].SetValue(_mountainConfig.Snow.Wind.ToVector2());
            _snowEffect.Parameters["SnowSway"].SetValue(_mountainConfig.Snow.Sway);
            _snowEffect.Parameters["FlakeSize"].SetValue(_mountainConfig.Snow.FlakeSize);
            _snowEffect.Parameters["SnowColor"].SetValue(_mountainConfig.Snow.FlakeColor.ToVector3());
            _snowEffect.Parameters["SnowOpacity"].SetValue(_mountainConfig.Snow.Opacity);

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
            _snowVertexBuffer = new VertexBuffer(graphicsDevice, BirdVertex.Declaration, snowVertices.Length, BufferUsage.WriteOnly);
            _snowVertexBuffer.SetData(snowVertices);

            short[] snowIndices = new short[_mountainConfig.Snow.FlakeCount * 6];
            for (int i = 0; i < _mountainConfig.Snow.FlakeCount; i++)
            {
                int v = i * 4;
                int o = i * 6;
                snowIndices[o] = (short)v; snowIndices[o + 1] = (short)(v + 2); snowIndices[o + 2] = (short)(v + 1);
                snowIndices[o + 3] = (short)(v + 1); snowIndices[o + 4] = (short)(v + 2); snowIndices[o + 5] = (short)(v + 3);
            }
            _snowIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, snowIndices.Length, BufferUsage.WriteOnly);
            _snowIndexBuffer.SetData(snowIndices);

            //--- Spray: a static billboard buffer for the sea's blown spray and spindrift, animated entirely
            //in the shader like the snow (never rebuilt). Same position+data billboard vertex.
            _sprayEffect = content.Load<Effect>("Shaders/Spray");
            _sprayEffect.Parameters["SprayBoxSize"].SetValue(SPRAY_BOX_SIZE);
            _sprayEffect.Parameters["SprayLevelY"].SetValue(SPRAY_LEVEL_Y);
            _sprayEffect.Parameters["SprayWind"].SetValue(SPRAY_WIND);
            _sprayEffect.Parameters["SprayRise"].SetValue(SPRAY_RISE);
            _sprayEffect.Parameters["SprayTurb"].SetValue(SPRAY_TURB);
            _sprayEffect.Parameters["DropletSize"].SetValue(SPRAY_DROPLET_SIZE);
            _sprayEffect.Parameters["SprayColor"].SetValue(SPRAY_COLOR);
            _sprayEffect.Parameters["SprayOpacity"].SetValue(SPRAY_OPACITY);

            BirdVertex[] sprayVertices = new BirdVertex[SPRAY_PARTICLE_COUNT * 4];
            Random sprayRng = new(5023);
            for (int i = 0; i < SPRAY_PARTICLE_COUNT; i++)
            {
                Vector3 basePosition = new((float)sprayRng.NextDouble(), (float)sprayRng.NextDouble(), (float)sprayRng.NextDouble());
                float rand = (float)sprayRng.NextDouble();
                int v = i * 4;
                sprayVertices[v] = new BirdVertex(basePosition, new Vector3(-1f, 1f, rand));
                sprayVertices[v + 1] = new BirdVertex(basePosition, new Vector3(1f, 1f, rand));
                sprayVertices[v + 2] = new BirdVertex(basePosition, new Vector3(-1f, -1f, rand));
                sprayVertices[v + 3] = new BirdVertex(basePosition, new Vector3(1f, -1f, rand));
            }
            _sprayVertexBuffer = new VertexBuffer(graphicsDevice, BirdVertex.Declaration, sprayVertices.Length, BufferUsage.WriteOnly);
            _sprayVertexBuffer.SetData(sprayVertices);

            short[] sprayIndices = new short[SPRAY_PARTICLE_COUNT * 6];
            for (int i = 0; i < SPRAY_PARTICLE_COUNT; i++)
            {
                int v = i * 4;
                int o = i * 6;
                sprayIndices[o] = (short)v; sprayIndices[o + 1] = (short)(v + 2); sprayIndices[o + 2] = (short)(v + 1);
                sprayIndices[o + 3] = (short)(v + 1); sprayIndices[o + 4] = (short)(v + 2); sprayIndices[o + 5] = (short)(v + 3);
            }
            _sprayIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, sprayIndices.Length, BufferUsage.WriteOnly);
            _sprayIndexBuffer.SetData(sprayIndices);

            //--- Meadow: a smooth rolling displaced grid scattered with flowers
            _meadowEffect = content.Load<Effect>("Shaders/Meadow");
            CreateGridMesh(MEADOW_GRID_N, MEADOW_EXTENT, out _meadowVertexBuffer, out _meadowIndexBuffer, out _meadowIndexCount);

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

        /// <summary>
        /// Builds a flat lattice grid: <paramref name="n"/> vertices per side over <paramref name="extent"/>,
        /// centred on the origin. The desert, mountain and meadow shaders recentre it on the camera and lift
        /// it into dunes, peaks or hills; it is drawn CullNone, so the winding does not matter. (Indices run
        /// up to n*n-1 in a 16-bit buffer, so keep n at 256 or below.)
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
        /// the Sahara dunes (with the same birds), the snowy range or the meadow. A no-op for
        /// <see cref="SceneKind.City"/>/<see cref="SceneKind.NeonCity"/>,
        /// which the caller draws itself. Opaque, so it stands in for the city as the thing the arena glass
        /// shows beneath it; it leaves the alpha-blend / back-face-cull state the rest of the opaque scene wants.
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
                    DrawBirds(frame);
                    break;
                case SceneKind.Desert:
                    DrawDesert(frame);
                    DrawBirds(frame);
                    break;
                case SceneKind.Mountain:
                    DrawMountain(frame);
                    break;
                case SceneKind.Meadow:
                    DrawMeadow(frame);
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
            _savannaLightRange[0] = SAVANNA_CAMPFIRE_RANGE;
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
        private static float SavannaTerrainHeight(float x, float z)
        {
            float dist = MathF.Sqrt(x * x + z * z);
            float t = MathHelper.Clamp((dist - SAVANNA_CLEARING_RADIUS) / SAVANNA_CLEARING_TRANSITION, 0f, 1f);
            float ramp = t * t * (3f - 2f * t); //smoothstep, as in the shader

            float rolling = 0.5f * MathF.Sin(x * 0.016f + z * 0.012f)
                + 0.3f * MathF.Sin(x * -0.011f + z * 0.020f + 1.5f)
                + 0.2f * MathF.Sin(x * 0.026f + z * 0.021f + 3.0f);

            float gentle = SAVANNA_CLEARING_RELIEF * (MathF.Sin(x * 0.04f + z * 0.03f) + 0.6f * MathF.Sin(x * -0.055f + z * 0.048f + 2.1f));

            return SAVANNA_LEVEL_Y + gentle + SAVANNA_HILL_HEIGHT * ramp * (rolling * 0.5f + 0.5f);
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
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, ACACIA_COUNT * 2);

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
            _flameEffect.Parameters["FlameSize"].SetValue(FLAME_SIZE);
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
        /// Draws the flock: each bird circles <see cref="BIRD_FLOCK_CENTER"/> on its own slow orbit, built
        /// into a camera-facing billboard here and flapped in the shader. Alpha-blended and depth-tested (the
        /// terrain or the platform in front hides one) but writing no depth. Called in the savanna and desert
        /// scenes, which share the one flock.
        /// </summary>
        private void DrawBirds(in SceneFrame frame)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);
            Vector3 right = inverseView.Right * (BIRD_WINGSPAN * Constants.HALF);
            Vector3 up = inverseView.Up * (BIRD_WINGSPAN * Constants.HALF * BIRD_ASPECT);

            for (int i = 0; i < BIRD_COUNT; i++)
            {
                float angle = frame.Time * _birdOrbitSpeed[i] + _birdOrbitPhase[i];
                float bob = MathF.Sin(frame.Time * _birdBobSpeed[i] + _birdOrbitPhase[i]) * BIRD_BOB;

                Vector3 center = BIRD_FLOCK_CENTER + new Vector3(
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

            _birdsEffect.Parameters["View"].SetValue(frame.Camera.View);
            _birdsEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_birdVertexBuffer);
            _graphicsDevice.Indices = _birdIndexBuffer;
            _birdsEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, BIRD_COUNT * 2);

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
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, SPRAY_PARTICLE_COUNT * 2);

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

        public void Dispose()
        {
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
        }
    }
}
