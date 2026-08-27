using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Which environment the arena stands in. City is the default; Sea, Savanna, Desert, Mountain, Meadow,
    /// NeonCity, Forest and Outback swap the city (and only the city) for open water, a savanna, a Sahara of
    /// dunes, a snowy range, a flowering meadow, the same city lit up in neon, a forest clearing, or the
    /// red-rock Australian outback. Tropical swaps it for a beach — sand, palms and mossy rocks around the
    /// island, a turquoise lagoon beyond it, and the green far shore that closes the horizon. Volcano swaps it
    /// for the flank of an erupting cone: black basalt cut by rivers of lava, fountains over the crater and
    /// drifting ash — the first scene whose <b>ground is the light</b>. Mars swaps it for rust-red cratered
    /// ground under a dusty, horizon-bright sky of its own — the Moon's crater field ported and retextured,
    /// but kept an ordinary atmospheric backdrop rather than a second sky-replacing scene, because the real
    /// Mars (unlike the Moon) keeps a thin atmosphere.
    /// <para>
    /// <see cref="Space"/> is the one that is not like the others: it replaces the <b>sky</b> rather than the
    /// ground, so the island floats in deep space and there is no terrain, no horizon and no weather at all.
    /// The dream and the cavern followed it; the <see cref="Moon"/> is the first to take <b>both</b> halves at
    /// once — real cratered ground under a replaced, atmosphere-free sky (see <see cref="SceneRenderer.ReplacesSky"/>).
    /// </para>
    /// <para>
    /// <b>Which of them an executable can reach is not the same question as which it can draw</b> — all three
    /// draw all of them. The Testbed's NumPad2 and the map editor's V both cycle <c>% 7</c>, i.e. over the seven
    /// scenes a map is authored against; the scenes past the end of that cycle — the forest, space, the dream,
    /// the cavern, the Moon, the outback, the tropical beach, the volcano and Mars — are reached in the
    /// Testbed with <c>scene=</c>, in the game from its scene menu (or its random launch pick), and in the
    /// editor only by loading a level whose config names one of them.
    /// </para>
    /// <para>
    /// <b>New kinds are appended, never inserted.</b> Nothing persists the enum numerically — a level stores
    /// its backdrop as a <see cref="SceneConfig"/> under a string discriminator — but the declared order is
    /// what the scene picker, <see cref="SceneRenderer.SceneName"/> and the ambience bed all index by, and
    /// <see cref="SceneRenderer.CycleLength"/> is a prefix of it.
    /// </para>
    /// </summary>
    public enum SceneKind { City, Sea, Savanna, Desert, Mountain, Meadow, NeonCity, Forest, Space, Dream, Cavern, Moon, Outback, Tropical, Volcano, Mars, Storm }

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
        /// savanna, desert) and out of the sea around the world origin so the drain funnel below the island
        /// reads as a drain into a pit rather than a bowl in flat ground - the flat clearing otherwise slices
        /// across the funnel just below its rim, hiding its depth and swallowing the balls falling through, and
        /// the sea otherwise runs its wave mesh straight through the funnel's open throat (#132). The sea's cut
        /// is an annulus rather than the full disc: inside the funnel the water survives as a calm standing
        /// pool where the glass cone crosses the mean level (see the pool derivation in <c>DrawSea</c>). The
        /// Testbed sets this to the island's radius; the map editor draws no island, so it leaves it 0 (the
        /// default) and nothing is cut.
        /// </summary>
        public float TerrainHoleRadius { get; set; }

        /// <summary>Mean sea level of the sea scene (world Y), so the caller can tell when its camera is under
        /// the water and fade in the underwater murk.</summary>
        public float SeaLevelY => _seaConfig.LevelY;

        /// <summary>
        /// Pushes the sea's submerge-fade uniforms onto the shared instancing effect, so a missed ball dims into
        /// dark water rather than vanishing the instant it crosses the surface (#131). A no-op (SeaFadeDepth = 0,
        /// which the shader gates the whole fade on) on every scene but the sea, where it sets the level, the
        /// deep-water tint and the band over which a ball fades out. The mirror of <see cref="CloudField.ApplyTo"/>
        /// for a sea-specific uniform set the ball shader needs.
        /// </summary>
        /// <param name="lensSubmerged">
        /// How far the <b>lens</b> is under the water, 0–1, from <see cref="LensSubmergedAmount"/> — the same
        /// number the caller hands the tonemap for its murk. The fade is released by exactly this (#159), so the
        /// two effects hand over rather than one of them leaning on the other being there: see the shader.
        /// </param>
        public void ApplySeaSubmerge(Effect effect, SceneKind scene, float lensSubmerged)
        {
            var p = effect.Parameters;
            if (scene != SceneKind.Sea)
            {
                p["SeaFadeDepth"]?.SetValue(0f);
                return;
            }

            p["SeaLevelY"].SetValue(_seaConfig.LevelY);
            p["SeaFadeDepth"].SetValue(SEA_SUBMERGE_FADE);
            p["SeaSubmergeTint"].SetValue(_seaConfig.WaterDeep.ToVector3());
            p["SeaLensSubmerged"].SetValue(lensSubmerged);
        }

        /// <summary>
        /// How far the <b>lens</b> is under the sea, 0 above the surface to 1 well below it — <b>the</b> figure
        /// for that question, asked by the tonemap's underwater murk and by the ball shader's submerge fade, so
        /// the two cannot disagree about whether the camera is in the water (#159).
        /// <para>
        /// It lived twice, once in each host, together with a second copy of the 7-unit band; that was harmless
        /// while only the murk read it and became a hazard the moment the fade did too — a fade released on one
        /// reading of "submerged" and a tint arriving on another is the pair of effects visibly disagreeing.
        /// Zero off the sea, the only scene with water a camera can get under.
        /// </para>
        /// <para>
        /// Measured a touch <b>above</b> the mean level (the 0.5), so partial submersion already begins to
        /// count: the surface is a wave field displaced by up to ±0.76 units, so a lens exactly at the mean is
        /// as likely to be inside a crest as in a trough's air, and the allowance is what keeps the answer from
        /// flickering as the swell passes.
        /// </para>
        /// </summary>
        public float LensSubmergedAmount(SceneKind scene, Vector3 cameraPosition) =>
            scene == SceneKind.Sea
                ? MathHelper.Clamp((_seaConfig.LevelY + 0.5f - cameraPosition.Y) / UNDERWATER_FADE_DEPTH, 0f, 1f)
                : 0f;

        /// <summary>
        /// How far under the surface the lens has to be for the water to read as fully closed over it — the
        /// murk's own ramp, and since #159 the fade's release as well.
        /// </summary>
        private const float UNDERWATER_FADE_DEPTH = 7f;

        /// <summary>
        /// Pushes the fade band above the kill plane onto the shared instancing effect (#192), so a ball
        /// dissolves over the last few units before the host is about to delete it rather than winking out in
        /// the one frame its body crosses the plane. Unconditional and scene-independent, unlike
        /// <see cref="ApplySeaSubmerge"/>: the plane is not any one scene's, it is the host's own physics rule
        /// (a missed ball falling out of the world), so the fade is pushed the same way every frame regardless
        /// of what is on screen. The pop it covers only ever showed in the six <see cref="OpenBelow"/> scenes,
        /// where the drop cinematic can put the lens below the island and film the whole fall; every solid
        /// terrain scene hides a falling ball behind the ground long before it gets this close, so pushing it
        /// there too is harmless. The map editor never calls this — it has no simulated ball to fade — and
        /// <c>KillPlaneFadeDepth</c> stays at its compiled default of 0 there, which the shader's own gate
        /// reads as off.
        /// </summary>
        /// <param name="killPlaneY">
        /// The host's own kill-plane height (the Game's <c>GameplayScreen.KILL_PLANE_Y</c>, the Testbed's own
        /// copy of the same value) — handed in rather than owned here, the way the sea level is not: each
        /// host's stepping policy is deliberately its own (see CLAUDE.md's "Prazsky.BS3D.Physics" remarks).
        /// </param>
        public void ApplyKillPlaneFade(Effect effect, float killPlaneY)
        {
            var p = effect.Parameters;
            p["KillPlaneY"].SetValue(killPlaneY);
            p["KillPlaneFadeDepth"].SetValue(KILL_PLANE_FADE_DEPTH);
        }

        /// <summary>
        /// How many world units above the kill plane a falling ball fades out over — see
        /// <see cref="ApplyKillPlaneFade"/>. Wider than the sea's own <see cref="SEA_SUBMERGE_FADE"/> (3): there
        /// is no water here to slow a ball first, so by the time one nears the plane it can be falling several
        /// units a second, and too shallow a band would still read as a pop, only a slightly later one.
        /// </summary>
        private const float KILL_PLANE_FADE_DEPTH = 6f;

        /// <summary>World units below the sea surface over which a missed ball fades from solid to gone — short,
        /// so it reads as being swallowed by the water rather than lingering under it.
        /// <para>
        /// It used to say the kill plane is far enough below this that a ball is off the screen long before the
        /// simulation drops it. That holds only while the lens is <b>above</b> the water: since #159 the fade is
        /// released as the camera goes under, so a ball watched from down there stays drawn all the way to the
        /// kill plane and is culled in one frame when it arrives. That pop is real, it is not this constant's to
        /// fix, and five other <c>OpenBelow</c> scenes have always shown it — see the issue filed for it.
        /// </para></summary>
        private const float SEA_SUBMERGE_FADE = 3f;

        /// <summary>
        /// How many scene-target texels make one output pixel — the caller's supersampling factor, which only
        /// the caller knows (the Game's moves with the quality tier). The space scene sizes its stars in
        /// <b>output</b> pixels off this: sized in texels instead, a star would come out four times dimmer at
        /// 2× than at 1×, which is the same sky looking different on two quality settings. Left at 1 it is
        /// simply the no-supersampling case, so a caller that never sets it still gets a correct sky.
        /// </summary>
        public int SupersampleFactor { get; set; } = 1;

        /// <summary>
        /// Whether the scene shaders may draw their <b>expensive extras</b> — the forest floor's triplanar
        /// normal variation and its procedural tree shadows, and the dream's four. 1 is the authored look;
        /// 0 is the reduced one, and each scene that has a reduced program compiles it as a second technique.
        /// <para>
        /// A plain number rather than a quality enum for <see cref="SupersampleFactor"/>'s reason: the tier
        /// lives in the Game and this library cannot see it, so the host converts. Left at 1, a caller that
        /// never sets it draws the full look, which is what the Testbed and the map editor want — they are
        /// where the scene is tuned and looked at.
        /// </para>
        /// <para>
        /// <b>Measured</b>, front end at 1600×900 on the desktop GPU, dome 13, nocap. Forest: the two extras
        /// together cost <b>2.69 → 2.09 ms</b>, and cutting either one <i>alone</i> saves nothing at all —
        /// 2.71 and 2.72.
        /// </para>
        /// <para>
        /// <b>These scenes are occupancy-bound rather than work-bound</b>, which is the whole reason this is one
        /// switch per scene rather than a dial per feature: only crossing back over the threshold buys
        /// anything, so a pair of removals is the smallest useful step and a third adds nothing.
        /// </para>
        /// <para>
        /// <b>The cavern was the third customer and is not one any more (#250).</b> Its pair — the full wall
        /// shading inside the water's reflection, and the full spore count — measured <b>4.98 → 3.33 ms</b>
        /// here, against 5.01 / 5.02 / 4.97 / 5.01 for each of the four single reductions on their own, which
        /// is where the occupancy reading above comes from. The owner then traded that scene's reflections and
        /// waves away outright, so the pair is cut from the shipped shader and there is one cavern technique
        /// left for every tier. See "The forest" and "The cavern" in docs/scenes.md.
        /// </para>
        /// </summary>
        public float SceneDetail
        {
            get => _sceneDetail;
            set
            {
                if (value == _sceneDetail) return;

                _sceneDetail = value;

                //Selected HERE and not only in ApplyForestParameters, which runs from the constructor and on a
                //config change and so had already run by the time a host set this — the first wiring set the
                //property, never re-selected, and drew the full-price floor at every tier while looking
                //perfectly correct. Caught by making the reduced technique output flat red for one run: the
                //floor stayed green.
                SelectDetailTechniques();
            }
        }

        private float _sceneDetail = 1f;

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
        private MoonSceneConfig _moonConfig = new();
        private OutbackSceneConfig _outbackConfig = new();
        private TropicalSceneConfig _tropicalConfig = new();
        private VolcanoSceneConfig _volcanoConfig = new();
        private MarsSceneConfig _marsConfig = new();
        private StormSceneConfig _stormConfig = new();

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

        //How far the pool's edge is buried INTO the drain's glass cone (world units): the water's rim ends
        //inside the wall rather than a chord-width short of it, so the funnel's 64-segment faceting can never
        //open a sliver of sky between the water and the glass — the same buried-edge reasoning as the gold
        //bands' EDGE_SINK (#109). See the pool derivation in DrawSea (#132).
        private const float POOL_WALL_BIAS = 0.15f;

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

        #region Outback

        private readonly Effect _outbackEffect;
        private readonly VertexBuffer _outbackVertexBuffer;
        private readonly IndexBuffer _outbackIndexBuffer;
        private readonly int _outbackIndexCount;

        //The monoliths are geometry, not a painted horizon, so this grid carries a silhouette rather than only
        //a shaded surface — which is what sets the density. At 400 over 1000 the cell is 2.5 world units and a
        //formation's flank falls its whole height over some eight of them, which the mesh can hold; the same
        //flank on the desert's 360 grid would fall over seven. Above 255 a side, so CreateGridMesh's 32-bit
        //index buffer is load-bearing here (the mountain's lesson — a 16-bit one wraps silently).
        private const int OUTBACK_GRID_N = 400;
        private const float OUTBACK_EXTENT = 1000f;

        //Look/tuning parameters (plain, monoliths, rock and ground materials, dust, shimmer) live in
        //OutbackSceneConfig; SceneRenderer reads them from _outbackConfig.

        #endregion

        #region Tropical

        private readonly Effect _tropicalEffect;
        private readonly VertexBuffer _tropicalVertexBuffer;
        private readonly IndexBuffer _tropicalIndexBuffer;
        private readonly int _tropicalIndexCount;

        //The beach and the far shore ridge are real geometry on the desert's grid density: the slopes
        //here are the gentlest of any terrain scene (a beach, then a rounded jungle ridge), the
        //shading normal is per-pixel so no grid shows, and the silhouette is far away. 360 over 1000.
        private const int TROPICAL_GRID_N = 360;
        private const float TROPICAL_EXTENT = 1000f;

        //How far inside the innermost waterline the lagoon's clip sits: Sea.fx flattens the swell over
        //a 4-unit calm band inside its clip radius, plus a unit tucked under the beach slope, so the
        //surf laps onto the sand rather than breaking against a circle (see DrawTropicalWater).
        private const float TROPICAL_WATERLINE_BIAS = 5f;

        //Look/tuning parameters (the beach profile, the water, the palms, the rocks) live in
        //TropicalSceneConfig; SceneRenderer reads them from _tropicalConfig. The lagoon's water is the
        //sea's own effect and grid — see DrawTropicalWater for how the two scenes share it.

        #endregion

        #region Palms and waterline rocks (tropical scene only)

        private readonly Effect _palmEffect;

        //Cached effect parameters for the per-frame instanced draws (the by-name indexer is a linear
        //scan, and DrawPalms/DrawTropicalRocks run them once per draw).
        private EffectParameter _palmViewParam, _palmProjectionParam,
            _palmSunDirectionParam, _palmSunColorParam, _palmZenithParam, _palmHorizonParam,
            _palmDiffuseParam, _palmDappleParam, _palmTimeParam, _palmWindParam,
            _palmSwayStrengthParam, _palmSwaySpeedParam;

        //Real 3D palm geometry on the acacia's path (#202): a few variants at rolled proportions and
        //structural seeds — a bowed ring-scarred trunk under a crown of drooping fronds with a skirt
        //of dead ones — each variant its own instanced draw so a grove is a mix, never one shape
        //stamped out. Scatter parameters live in TropicalSceneConfig.Palms.
        private PalmMesh[] _palmMeshes;
        private ModelInstance[][] _palmInstances;         //per variant; fronds and wood share the matrices
        private float[] _palmDryness;                     //per variant: how far its crown is towards the dry green
        private DynamicVertexBuffer _palmInstanceBuffer;  //shared, re-uploaded per draw (SetDataOptions.Discard)

        //The waterline's rocks: the stone (RockMesh) and its moss cap (a LatheMesh over the same
        //profile family, on its own irregularity phase so the moss edge reads ragged against the
        //stone's own wobble) are two meshes over one instance matrix each, drawn per variant.
        private RockMesh[] _tropicalRockMeshes;
        private LatheMesh[] _tropicalMossMeshes;
        private ModelInstance[][] _tropicalRockInstances; //per variant; stone and cap share the matrices

        //Colours, stored from the config so the per-draw DiffuseColor can be set as each part draws.
        private Vector3 _palmFrondColor, _palmFrondDry, _palmTrunkColor, _tropicalStoneColor, _tropicalMossColor;

        #endregion

        #region Volcano

        private readonly Effect _volcanoEffect;
        private readonly VertexBuffer _volcanoVertexBuffer;
        private readonly IndexBuffer _volcanoIndexBuffer;
        private readonly int _volcanoIndexCount;

        //The mountain's density and extent: the flank carries a summit against the sky, so it wants the
        //craggy grid rather than the desert's, and it needs the same 32-bit index buffer (CreateGridMesh).
        private const int VOLCANO_GRID_N = 360;
        private const float VOLCANO_EXTENT = 1200f;

        //Matched by MAX_RIVERS in Volcano.fx and MAX_VENTS in LavaFountain.fx. Both are shader array sizes:
        //raising either here without raising it there writes past what the shader reads.
        private const int MAX_RIVERS = 6;
        private const int MAX_VENTS = 4;

        //Where a 16-bit index buffer runs out: four vertices a particle over 65 536 addressable ones. The
        //mountain's snow silently trusted a config to stay under a limit like this; a volcano's counts are
        //dials from day one (#209 defends a 75 FPS budget this scene spends particles against), so the cap
        //is stated rather than assumed.
        private const int MAX_BILLBOARD_PARTICLES = 16000;

        //The rivers' bearings and reaches, solved once per config (BuildVolcanoBuffers) rather than per
        //frame — the shader draws the flows from these and the scene lights ride the same figures, which is
        //what keeps a lamp on the river it is lighting.
        private readonly float[] _riverBearing = new float[MAX_RIVERS];
        private readonly float[] _riverReach = new float[MAX_RIVERS];
        private int _riverCount;

        //The vents the fountains are thrown from: slot 0 the crater, the rest side vents on the flank.
        private readonly Vector3[] _ventPosition = new Vector3[MAX_VENTS];
        private readonly float[] _ventStrength = new float[MAX_VENTS];
        private int _ventCount;

        //The lava fountains and the smoke plume: ONE static billboard buffer, its first PlumeFraction drawn
        //as the plume and the rest as the jets, so neither pass pays for the other's particles. Animated
        //entirely in the vertex shader, so it is rebuilt only when a config is applied and never per frame.
        private readonly Effect _fountainEffect;
        private VertexBuffer _fountainVertexBuffer;
        private IndexBuffer _fountainIndexBuffer;
        private int _plumeQuads, _jetQuads;

        //Cached at load: the by-name Techniques indexer is a linear scan and this pass selects between the
        //two of them twice a frame (BestPractices.md §1).
        private readonly EffectTechnique _plumeTechnique, _jetTechnique;

        //The drifting ash, on the snowfall's machinery in its own shader (Ash.fx says why it is not Snow.fx).
        private readonly Effect _ashEffect;
        private VertexBuffer _ashVertexBuffer;
        private IndexBuffer _ashIndexBuffer;

        //Look/tuning parameters live in VolcanoSceneConfig; SceneRenderer reads them from _volcanoConfig.

        #endregion

        #region Mars

        private readonly Effect _marsEffect;
        private readonly VertexBuffer _marsVertexBuffer;
        private readonly IndexBuffer _marsIndexBuffer;
        private readonly int _marsIndexCount;

        //The crater field is evaluated four times a pixel (the vertex tap plus the normal's three taps),
        //the Moon's own reason for its grid density; Mars keeps it rather than the coarser desert grid.
        private const int MARS_GRID_N = 360;
        private const float MARS_EXTENT = 1000f;

        //The moons pass shares the sky-replacing scenes' full-screen-quad machinery (_spaceQuad), so only
        //its two per-frame ray-reconstruction parameters are cached (BestPractices §1) — the terrain pass
        //follows the outback's and the volcano's own practice of setting the rest by name each draw.
        private EffectParameter _marsMoonsInverseViewProjection, _marsMoonsCameraPosition;
        private EffectTechnique _marsTerrainTechnique, _marsMoonsTechnique;

        //Look/tuning parameters (clearing, craters, rust surface, dust haze, the two moons) live in
        //MarsSceneConfig; SceneRenderer reads them from _marsConfig.

        #endregion

        #region Storm

        private readonly Effect _stormEffect;
        private readonly VertexBuffer _stormVertexBuffer;
        private readonly IndexBuffer _stormIndexBuffer;
        private readonly int _stormIndexCount;

        //The mountain's density and a wide extent, because this terrain carries a SILHOUETTE and not only a
        //shaded surface: a turret's flank falls forty-odd units over a handful of cells, and the whole scene
        //depends on those crests reading against the sky (see StormDeckConfig's own note). Over 255 a side,
        //so CreateGridMesh's 32-bit index buffer is load-bearing here (the mountain's lesson).
        private const int STORM_GRID_N = 360;
        private const float STORM_EXTENT = 1400f;

        //Look/tuning parameters (the deck, the turrets, the cloud, the flash, the air) live in
        //StormSceneConfig; SceneRenderer reads them from _stormConfig.

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

        //Cached effect parameters for the per-frame instanced draw (the by-name indexer is a linear scan).
        private EffectParameter _acaciaViewParam, _acaciaProjectionParam, _acaciaCameraParam,
            _acaciaSunDirectionParam, _acaciaSunColorParam, _acaciaZenithParam, _acaciaHorizonParam,
            _acaciaDiffuseParam, _acaciaDappleParam, _acaciaAddedLightParam;

        //Real 3D acacia geometry (#202): a few tree variants (a bark trunk under a wide flat-topped umbrella
        //canopy) and a few bush variants (a low rounded clump), each its own instanced draw so a grove is not
        //one shape stamped out. Plants gather in clumps around a set of cluster centres, as they do on a real
        //savanna, with a few solitary ones. Replaces the flat billboard that read as a paper cutout — a surface
        //of revolution has volume from every angle. Scatter parameters live in SavannaSceneConfig.Acacia.
        private AcaciaMesh[] _acaciaTreeMeshes;
        private FoliageMesh[] _acaciaBushMeshes;
        private ModelInstance[][] _acaciaTreeInstances;    //per tree variant; canopy and trunk share the matrices
        private ModelInstance[][] _acaciaBushInstances;    //per bush variant
        private float[] _acaciaTreeDryness;                //per tree variant: how far its canopy is towards the dry green
        private DynamicVertexBuffer _acaciaInstanceBuffer; //shared, re-uploaded per draw (SetDataOptions.Discard)

        //Canopy/trunk colours, stored from the config so the per-draw DiffuseColor can be set as each part draws.
        private Vector3 _acaciaCanopyColor, _acaciaCanopyDry, _acaciaTrunkColor;

        //The campfires' hearths (#282): a ring of stones set around each fire, and the scorched ground under
        //it. The stones ride the acacia's own instanced path - same shader, same lighting as everything else
        //planted on this terrain - with one draw per FIRE rather than per mesh variant, because what differs
        //between two rings is the firelight their own fire is casting at this instant.
        private RockMesh[] _hearthStoneMeshes;
        private ModelInstance[][] _hearthStoneInstances;  //per fire; index into _hearthStoneMeshes by fire % variants
        private Vector3 _hearthStoneColor;
        private float _hearthStoneFirelight;
        private readonly Vector3[] _hearthPositions = new Vector3[MAX_SCENE_LIGHTS];

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

        /// <summary>
        /// How many campfires ring the island, capped to the scene-light budget the shaders' arrays are sized
        /// for. Every caller that walks the fires — the grass's lights, the balls' and island's lights, and
        /// the flame billboards — counts with this one.
        /// </summary>
        public int SavannaCampfireCount => Math.Clamp(_savannaConfig.Campfire.Count, 1, SceneLights.MaxLights);

        /// <summary>
        /// The world position of fire <paramref name="index"/>: evenly spaced around the circle the config's
        /// <c>GroundXZ</c> sits on, starting at it, and lifted to the terrain height where it lands.
        /// <para>
        /// The radius and the ring's rotation both come out of that one config point rather than being fields
        /// of their own, which is what lets a config written when there was a single fire keep placing that
        /// fire exactly where it stood. The Y is derived live, so a terrain edit in the editor moves every
        /// fire with the ground.
        /// </para>
        /// </summary>
        public Vector3 SavannaCampfirePosition(int index)
        {
            Vec2 anchor = _savannaConfig.Campfire.GroundXZ;

            float radius = MathF.Sqrt(anchor.X * anchor.X + anchor.Y * anchor.Y);
            float angle = MathF.Atan2(anchor.Y, anchor.X) + index * MathHelper.TwoPi / SavannaCampfireCount;

            float x = MathF.Cos(angle) * radius;
            float z = MathF.Sin(angle) * radius;

            return new Vector3(x, SavannaTerrainHeight(x, z) + _savannaConfig.Campfire.HeightAboveTerrain, z);
        }

        /// <summary>The campfire point-light range (quadratic distance falloff), shared by every fire.</summary>
        public float SavannaCampfireRange => _savannaConfig.Campfire.Range;

        /// <summary>
        /// The flickering colour of fire <paramref name="index"/> at a wall-clock time, so its grass light,
        /// its light on the balls and its flame all pulse together.
        /// <para>
        /// <b>Each fire burns on its own clock.</b> The index offsets the time and also stretches the rates by
        /// a few per cent, so the ring never beats in unison — an offset alone would leave eight fires running
        /// the identical pattern a moment apart, which the eye picks up as a rotating wave around the island
        /// the moment two of them are in shot together.
        /// </para>
        /// </summary>
        public Vector3 CampfireColor(float time, int index)
        {
            //Irrational-ish stride, so no two fires land on the same phase and the ring does not repeat after
            //a few of them however many there are.
            float t = time + index * 3.77f;
            float rate = 1f + index * 0.031f;

            float flicker = 0.72f + 0.28f * (0.5f * MathF.Sin(t * 11f * rate) + 0.3f * MathF.Sin(t * 17f * rate + 1.3f) + 0.2f * MathF.Sin(t * 7f * rate));

            return _savannaConfig.Campfire.BaseColor.ToVector3() * flicker;
        }

        #endregion

        #region Birds (savanna, desert and outback scenes)

        private readonly Effect _birdsEffect;
        private readonly BirdMesh _birdMesh;

        //Cached at load: DrawBirds sets three of these PER BIRD, and the by-name indexer is a linear scan
        //(BestPractices.md, section 1).
        private readonly EffectParameter _birdWorldParam, _birdViewParam, _birdProjectionParam, _birdColorParam,
            _birdSunDirectionParam, _birdSunColorParam, _birdZenithParam, _birdHorizonParam,
            _birdFlapPhaseParam, _birdFlapAmountParam;

        private float[] _birdRadius, _birdAltitude, _birdOrbitSpeed, _birdOrbitPhase, _birdBobSpeed,
            _birdFlapPeriod, _birdFlapCyclePhase, _birdFlapBeats, _birdBurstFraction;

        //The band of orbits the flock is seeded over. The lean is taken from these same two numbers, so a
        //wide circle cannot end up leaning like a tight one however either is retuned.
        private const float BIRD_RADIUS_MIN = 28f;
        private const float BIRD_RADIUS_SPAN = 34f;

        //How hard a bird banks, on the tightest orbit and on the widest. A circling bird MUST lean - it is
        //half of what made the old billboard read as mechanical, since a camera-facing quad is always
        //upright. The lean follows the turn, but it is taken from the radius rather than from the honest
        //atan(v^2 / (g*r)): this flock deliberately circles far slower than a real kettle of vultures does
        //(unhurried is the whole look), and at these speeds that formula asks for about four degrees on the
        //wide orbits and seventy on the tight ones. The band below is what a soaring bird actually holds.
        private const float BIRD_BANK_TIGHT = 0.52f;
        private const float BIRD_BANK_WIDE = 0.26f;

        //How far the lean wanders, and how fast. A bird trimming its circle is never quite settled.
        private const float BIRD_BANK_DRIFT = 0.06f;
        private const float BIRD_BANK_DRIFT_SPEED = 0.23f;

        //The cycle a bird repeats: one burst of wingbeats, then a long glide. The burst is not rolled
        //here — it is derived from the beat rate in SeedBirdFlock, and capped so it cannot swallow the glide.
        private const float BIRD_CYCLE_MIN = 6.5f;
        private const float BIRD_CYCLE_SPAN = 7f;
        private const float BIRD_BEAT_HZ_MIN = 2.1f;
        private const float BIRD_BEAT_HZ_SPAN = 1.1f;
        private const float BIRD_BURST_MAX = 0.40f;

        //What is left of the wingbeat while a bird glides: the wings still breathe, they are just not
        //beating. Also the floor under the burst's envelope, so amount is continuous across both edges.
        private const float BIRD_GLIDE_TRIM = 0.06f;

        //Flock parameters (count, wingspan, bob, colour, flock centre) live in BirdsConfig, shared by the
        //savanna, desert and outback configs; DrawBirds reads the active scene's Birds config each frame. The
        //mesh is one shared rest pose - what makes one bird differ from another is its world matrix and its
        //two flap uniforms - so only the per-bird state above is sized from the counts, in SeedBirdFlock.

        //One camera-facing quad, its Data carrying (u, v, a per-particle random). The campfire flame, the
        //mountain's snow and the sea's spray are drawn this way; the birds were too, until #235 made them
        //real geometry and they stopped being anything a quad could hold.
        private struct BillboardVertex : IVertexType
        {
            public Vector3 Position;
            public Vector3 Data;

            public BillboardVertex(Vector3 position, Vector3 data)
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

        //Snowfall parameters (flake count/size/shape/colour/opacity, box, fall speed, wind, sway) now live in
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
        //describe the scattered objects, which are ForestScatterRenderer's instanced draws rather than this
        //scene's (they were the Game's alone until #75) - only ForestTerrainHeight below is shared with them,
        //so they stand on the floor this shader draws.

        #endregion

        #region Space

        private readonly Effect _spaceEffect;

        //No terrain grid: space replaces the SKY, not the ground, so the whole scene is ONE full-screen pass
        //over a quad already in normalized device coordinates, with the view ray recovered per pixel through
        //the inverse view-projection. Four corners drawn as a triangle strip, built once.
        private readonly VertexBuffer _spaceQuad;

        //The handful of parameters that change per frame, resolved once (BestPractices §1: the by-name
        //indexer is a linear scan). Everything else is pushed by ApplySpaceParameters when a config lands.
        private readonly EffectParameter _spaceInverseViewProjection, _spaceCameraPosition, _spaceSunDirection, _spaceSupersample, _spaceTime;

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

        #region Moon

        //The twelfth scene (#125), and the first in BOTH families at once: a solid-terrain grid like the
        //desert's AND a sky-replacing pass like space's, in one effect with two techniques. DrawMoon runs
        //the displaced crater grid first (depth-writing) and the sky quad after it, depth-READ on the
        //shared _spaceQuad — the opposite interleave is a measured 8× frame blow-up; see DrawMoon's doc.
        private readonly Effect _moonEffect;
        private readonly VertexBuffer _moonVertexBuffer;
        private readonly IndexBuffer _moonIndexBuffer;
        private readonly int _moonIndexCount;

        //Two techniques over one effect, resolved once — CurrentTechnique is assigned twice per frame here,
        //which is why the by-name lookup must not be paid per draw.
        private readonly EffectTechnique _moonSkyTechnique, _moonTerrainTechnique;

        //The back-buffer-sized target the cavern and the dream are shaded into before being scaled up into the
        //caller's supersampled one, and the batch that scales them. Built on first use and rebuilt only when
        //the back buffer changes size — never per frame. See DrawBackdropAtDisplayResolution.
        private RenderTarget2D _backdropTarget;
        private SpriteBatch _backdropBatch;

        //Per-frame parameters, resolved once (BestPractices §1). The sky pass wants the inverse
        //view-projection and the terrain pass the plain pair, so both are cached; everything else is pushed
        //by ApplyMoonParameters when a config lands. No time parameter: nothing on the Moon moves.
        private readonly EffectParameter _moonInverseViewProjection, _moonView, _moonProjection,
            _moonCameraPosition, _moonSunDirection, _moonSupersample, _moonOriginXZ, _moonHoleRadius;

        //The extent is set by where the horizon stands, not by haze reach like the atmospheric siblings: the
        //highland belt crests ~310 units out and the curvature (8e-5) closes everything behind it by
        //occlusion, so ground past ±600 can never be seen. The grid itself runs past the Game camera's
        //500-unit far plane (corners ~848 out) — what the crest and the curvature guarantee together is that
        //the far-plane cut lands BEYOND the occluding skyline, where it is already hidden; a curvature loose
        //enough to leave the cut visible puts a dead-level, camera-locked clip line through the belt's
        //saddles.
        private const int MOON_GRID_N = 360;
        private const float MOON_EXTENT = 1200f;

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

            //--- Outback (#112): the desert's machinery with rock on it — the same flat lattice, displaced into
            //a near-flat spinifex plain with red monoliths standing on a jittered single-cell lattice
            _outbackEffect = content.Load<Effect>("Shaders/Outback");
            CreateGridMesh(OUTBACK_GRID_N, OUTBACK_EXTENT, out _outbackVertexBuffer, out _outbackIndexBuffer, out _outbackIndexCount);

            ApplyOutbackParameters();

            //--- Tropical (#244): the fourteenth scene — a beach ring around the island, a turquoise lagoon
            //and the green far shore that closes the horizon. The land grid is the desert's density (the
            //gentlest slopes of any terrain scene); the lagoon's water is the sea's own effect and grid,
            //drawn over this terrain by DrawTropicalWater below.
            _tropicalEffect = content.Load<Effect>("Shaders/Tropical");
            CreateGridMesh(TROPICAL_GRID_N, TROPICAL_EXTENT, out _tropicalVertexBuffer, out _tropicalIndexBuffer, out _tropicalIndexCount);

            ApplyTropicalParameters();

            //--- Palms and the waterline's mossy rocks: instanced procedural geometry on the acacia's path
            //(#202), shaded by Palm.fx — the acacia's sun-and-dome lighting with the palm's own sway.
            _palmEffect = content.Load<Effect>("Shaders/Palm");
            _palmViewParam = _palmEffect.Parameters["View"];
            _palmProjectionParam = _palmEffect.Parameters["Projection"];
            _palmSunDirectionParam = _palmEffect.Parameters["SunDirection"];
            _palmSunColorParam = _palmEffect.Parameters["SunColor"];
            _palmZenithParam = _palmEffect.Parameters["ZenithColor"];
            _palmHorizonParam = _palmEffect.Parameters["HorizonColor"];
            _palmDiffuseParam = _palmEffect.Parameters["DiffuseColor"];
            _palmDappleParam = _palmEffect.Parameters["DappleStrength"];
            _palmTimeParam = _palmEffect.Parameters["PalmTime"];
            _palmWindParam = _palmEffect.Parameters["WindDirection"];
            _palmSwayStrengthParam = _palmEffect.Parameters["SwayStrength"];
            _palmSwaySpeedParam = _palmEffect.Parameters["SwaySpeed"];
            BuildTropicalBuffers();

            //--- Volcano (#223): the fifteenth scene — the flank of an erupting cone, its lava rivers and the
            //crackle glow between its crust plates. The mountain's grid density, because this terrain carries
            //a summit against the sky; the fountains, the plume and the ash are three billboard buffers over
            //two more effects, all animated in their vertex shaders and rebuilt only when a config is applied.
            _volcanoEffect = content.Load<Effect>("Shaders/Volcano");
            CreateGridMesh(VOLCANO_GRID_N, VOLCANO_EXTENT, out _volcanoVertexBuffer, out _volcanoIndexBuffer, out _volcanoIndexCount);

            _fountainEffect = content.Load<Effect>("Shaders/LavaFountain");
            _plumeTechnique = _fountainEffect.Techniques["Plume"];
            _jetTechnique = _fountainEffect.Techniques["Fountain"];
            _ashEffect = content.Load<Effect>("Shaders/Ash");

            ApplyVolcanoParameters();
            BuildVolcanoBuffers();

            //--- Mars (#277): the sixteenth scene - the Moon's crater field (#125) retextured rust/ochre on
            //the outback's plumbing (an ordinary dome, the shared cloud shadow, a haze-closed horizon)
            //rather than the Moon's domeless, curvature-closed one. Two techniques in one effect: the
            //terrain grid, and a small full-screen pass for Phobos and Deimos sharing the sky-replacing
            //scenes' own quad (_spaceQuad).
            _marsEffect = content.Load<Effect>("Shaders/Mars");
            CreateGridMesh(MARS_GRID_N, MARS_EXTENT, out _marsVertexBuffer, out _marsIndexBuffer, out _marsIndexCount);

            _marsTerrainTechnique = _marsEffect.Techniques["MarsTerrain"];
            _marsMoonsTechnique = _marsEffect.Techniques["MarsMoons"];

            _marsMoonsInverseViewProjection = _marsEffect.Parameters["InverseViewProjection"];
            _marsMoonsCameraPosition = _marsEffect.Parameters["CameraPosition"];

            ApplyMarsParameters();

            //--- Storm (#219): the seventeenth scene — a deck of storm cloud at the island's foot with
            //convective turrets towering out of it and lightning flashing inside them. Built as TERRAIN and
            //deliberately not on the shared cloud field: Storm.fx's header has the three mechanisms that
            //rule that out, and DrawStorm below is the one terrain draw that does NOT invoke the cloud hook.
            _stormEffect = content.Load<Effect>("Shaders/Storm");
            CreateGridMesh(STORM_GRID_N, STORM_EXTENT, out _stormVertexBuffer, out _stormIndexBuffer, out _stormIndexCount);

            ApplyStormParameters();

            //--- Savanna: a flat lattice the shader displaces into gentle grassland (per-pixel normal, no grid)
            _savannaEffect = content.Load<Effect>("Shaders/Savanna");
            CreateGridMesh(SAVANNA_GRID_N, SAVANNA_EXTENT, out _savannaVertexBuffer, out _savannaIndexBuffer, out _savannaIndexCount);

            ApplySavannaParameters();

            //--- Acacia: a static billboard buffer of trees scattered over the savanna, positioned on the
            //ground (SavannaTerrainHeight mirrors the shader's field) and drawn as a flat-topped tree in Acacia.fx
            _acaciaEffect = content.Load<Effect>("Shaders/Acacia");
            _acaciaViewParam = _acaciaEffect.Parameters["View"];
            _acaciaProjectionParam = _acaciaEffect.Parameters["Projection"];
            _acaciaCameraParam = _acaciaEffect.Parameters["CameraPosition"];
            _acaciaSunDirectionParam = _acaciaEffect.Parameters["SunDirection"];
            _acaciaSunColorParam = _acaciaEffect.Parameters["SunColor"];
            _acaciaZenithParam = _acaciaEffect.Parameters["ZenithColor"];
            _acaciaHorizonParam = _acaciaEffect.Parameters["HorizonColor"];
            _acaciaDiffuseParam = _acaciaEffect.Parameters["DiffuseColor"];
            _acaciaDappleParam = _acaciaEffect.Parameters["DappleStrength"];
            _acaciaAddedLightParam = _acaciaEffect.Parameters["AddedLight"];
            ApplyAcaciaParameters();
            BuildAcaciaBuffers();
            BuildHearthStones();

            //--- Campfire flame: one billboard drawn as a procedural flame at the campfire position
            _flameEffect = content.Load<Effect>("Shaders/Flame");
            BillboardVertex[] flameVertices =
            {
                new(Vector3.Zero, new Vector3(-1f, 0f, 0f)),
                new(Vector3.Zero, new Vector3(1f, 0f, 0f)),
                new(Vector3.Zero, new Vector3(-1f, 1f, 0f)),
                new(Vector3.Zero, new Vector3(1f, 1f, 0f))
            };
            _flameVertexBuffer = new VertexBuffer(graphicsDevice, BillboardVertex.Declaration, 4, BufferUsage.WriteOnly);
            _flameVertexBuffer.SetData(flameVertices);
            short[] flameIndices = { 0, 1, 2, 2, 1, 3 };
            _flameIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
            _flameIndexBuffer.SetData(flameIndices);

            //--- Birds: one shared rest-pose mesh, and each bird's orbit and flap cycle seeded once
            _birdsEffect = content.Load<Effect>("Shaders/Birds");
            _birdMesh = new BirdMesh(graphicsDevice);

            _birdWorldParam = _birdsEffect.Parameters["World"];
            _birdViewParam = _birdsEffect.Parameters["View"];
            _birdProjectionParam = _birdsEffect.Parameters["Projection"];
            _birdColorParam = _birdsEffect.Parameters["BirdColor"];
            _birdSunDirectionParam = _birdsEffect.Parameters["SunDirection"];
            _birdSunColorParam = _birdsEffect.Parameters["SunColor"];
            _birdZenithParam = _birdsEffect.Parameters["ZenithColor"];
            _birdHorizonParam = _birdsEffect.Parameters["HorizonColor"];
            _birdFlapPhaseParam = _birdsEffect.Parameters["FlapPhase"];
            _birdFlapAmountParam = _birdsEffect.Parameters["FlapAmount"];

            SeedBirdFlock();

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
            _spaceTime = _spaceEffect.Parameters["SpaceTime"];

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

            //--- Moon: the twelfth scene (#125), the first in both families at once — a displaced crater
            //grid like the desert's under a sky-replacing star-and-Earth pass on space's quad, two
            //techniques in one effect. Nothing on it moves, so there is no time parameter to cache.
            _moonEffect = content.Load<Effect>("Shaders/Moon");
            CreateGridMesh(MOON_GRID_N, MOON_EXTENT, out _moonVertexBuffer, out _moonIndexBuffer, out _moonIndexCount);

            _moonSkyTechnique = _moonEffect.Techniques["MoonSky"];
            _moonTerrainTechnique = _moonEffect.Techniques["MoonTerrain"];

            _moonInverseViewProjection = _moonEffect.Parameters["InverseViewProjection"];
            _moonView = _moonEffect.Parameters["View"];
            _moonProjection = _moonEffect.Parameters["Projection"];
            _moonCameraPosition = _moonEffect.Parameters["CameraPosition"];
            _moonSunDirection = _moonEffect.Parameters["SunDirection"];
            _moonSupersample = _moonEffect.Parameters["SupersampleFactor"];
            _moonOriginXZ = _moonEffect.Parameters["OriginXZ"];
            _moonHoleRadius = _moonEffect.Parameters["IslandHoleRadius"];

            ApplyMoonParameters();
        }

        /// <summary>
        /// True for the scenes that replace the SKY rather than the ground — space, the dream, the cavern
        /// and the Moon. The caller draws no dome and no cloud deck in these, suppresses the cloud shadow on
        /// the instanced effect, clears to black (the pass covers every pixel; black is what would show if it
        /// ever did not), and takes the scene's own light rig through <see cref="TryGetLightRig"/>.
        /// <para>
        /// <b>The Moon (#125) is deliberately in this set AND in <see cref="IsSolidTerrainScene"/>, the first
        /// scene in both.</b> The two families were exact complements of what they draw — a dome over ground,
        /// or a backdrop with no ground — until the Moon wanted real cratered ground under a black, starlit,
        /// domeless sky. Every question this flag answers (dome, clouds, clear colour, light rig) the Moon
        /// answers the sky-replacing way, and every question <see cref="IsSolidTerrainScene"/> answers (the
        /// terrain hole, the pit shaft, <see cref="OpenBelow"/>) it answers the terrain way; no caller asks
        /// either flag anything the other one owns, which is what makes holding both memberships sound.
        /// </para>
        /// </summary>
        public static bool ReplacesSky(SceneKind kind) =>
            kind is SceneKind.Space or SceneKind.Dream or SceneKind.Cavern or SceneKind.Moon;

        /// <summary>
        /// True for the solid-ground backdrops — mountains, meadow, savanna, desert, forest, outback, the
        /// tropical beach, the volcano and Mars — whose terrain is a flat clearing at the island's foot with the island's footprint
        /// cut out of it (<see cref="TerrainHoleRadius"/>), and which therefore need the dark pit shaft drawn
        /// behind the drain's glass: a hole alone lets the ~55 %-opaque glass show what is behind it straight
        /// through and the drain reads as a glass ring lying on the ground. The sea fills the drain with
        /// water, the two cities have their own canyon falling away below the island, and space, the dream
        /// and the cavern have nothing down there to hide a ball against — none of them needs it.
        /// <para>
        /// The Moon is here <b>and</b> in <see cref="ReplacesSky"/> — the first scene in both families (the
        /// note there says why that is sound). It needs the shaft for the terrain reason with the sky-replacing
        /// twist: without it the drain's glass would show the <i>starfield</i> through a hole in the ground,
        /// which reads as a glass ring over the night sky. The tropical beach is the first scene with water
        /// <i>and</i> this membership — its water starts past the beach, well outside the hole, so under the
        /// island there is sand and the shaft answers for it exactly as it does for the meadow.
        /// </para>
        /// <para>
        /// It existed as a private copy in the Testbed and the Game until #75, and the forest was once missing
        /// from <b>both</b> — which is the exact failure a duplicated classification invites, and the reason
        /// this and every other question about a <see cref="SceneKind"/> are answered here.
        /// </para>
        /// </summary>
        public static bool IsSolidTerrainScene(SceneKind kind) =>
            kind is SceneKind.Mountain or SceneKind.Meadow or SceneKind.Savanna or SceneKind.Desert
                or SceneKind.Forest or SceneKind.Moon or SceneKind.Outback or SceneKind.Tropical
                or SceneKind.Volcano or SceneKind.Mars or SceneKind.Storm;

        /// <summary>
        /// Whether there is a vantage <b>under</b> the island from which the balls pouring out of the drain can
        /// still be seen — what the drop cinematic asks before it decides whether to dive beneath the stone or
        /// stay above it and look down the drain's throat.
        /// <para>
        /// It is exactly the complement of <see cref="IsSolidTerrainScene"/>, and that is a consequence rather
        /// than a coincidence: the pit shaft those scenes need is opaque and near-black, so the very thing that
        /// makes the drain read from above is what closes the view from below. Defined as the negation so a
        /// twelfth scene is one decision instead of two silently disagreeing lists — it was two hand-kept sets
        /// in different files until #75. Split them again only if a scene ever wants the shaft and the dive
        /// both, and say why on the spot.
        /// </para>
        /// </summary>
        public static bool OpenBelow(SceneKind kind) => !IsSolidTerrainScene(kind);

        /// <summary>How many <see cref="SceneKind"/>s there are; a scene picker and a random pick size off it.</summary>
        public static int SceneCount => SCENE_NAMES.Length;

        /// <summary>
        /// How many scenes the Testbed's NumPad2 and the map editor's V walk, which is deliberately <b>not</b>
        /// <see cref="SceneCount"/>: the cycle stays on the seven scenes a map is authored against, and the
        /// forest, space, the dream and the cavern sit past its end — reached with <c>scene=</c> on any command
        /// line, or from the game's own scene menu. Both executables wrote the 7 as a bare literal until #75.
        /// </summary>
        public const int CycleLength = 7;

        //In the declared order of SceneKind, so a picker can index it by the enum's own value. "Mountains"
        //reads better than the singular enum member and is deliberately not "corrected" to match it; the
        //parse keys below are the singular ones, because those are what a command line already takes.
        private static readonly string[] SCENE_NAMES =
            { "City", "Sea", "Savanna", "Desert", "Mountains", "Meadow", "Neon City", "Forest", "Space", "Dream", "Cavern", "Moon", "Outback", "Tropical", "Volcano", "Mars", "Storm" };

        /// <summary>
        /// The scene's name for a menu or a log line. Display text, not a parse key — see
        /// <see cref="TryParseScene"/> for the spellings a command line takes.
        /// </summary>
        public static string SceneName(SceneKind kind) => SCENE_NAMES[(int)kind];

        /// <summary>
        /// Parses the names every executable's <c>scene=</c> switch takes, so one benchmark or screenshot
        /// script drives any of them unchanged — the Testbed grew an if/else chain, the Game a switch and the
        /// two had to be kept in step by hand until #75. <c>mountain</c> and <c>neon</c> rather than
        /// <c>mountains</c> and <c>neoncity</c>: they are the names that already existed.
        /// </summary>
        public static bool TryParseScene(string name, out SceneKind kind)
        {
            switch (name?.ToLowerInvariant())
            {
                case "city": kind = SceneKind.City; return true;
                case "sea": kind = SceneKind.Sea; return true;
                case "savanna": kind = SceneKind.Savanna; return true;
                case "desert": kind = SceneKind.Desert; return true;
                case "mountain": kind = SceneKind.Mountain; return true;
                case "meadow": kind = SceneKind.Meadow; return true;
                case "neon": kind = SceneKind.NeonCity; return true;
                case "forest": kind = SceneKind.Forest; return true;
                case "space": kind = SceneKind.Space; return true;
                case "dream": kind = SceneKind.Dream; return true;
                case "cavern": kind = SceneKind.Cavern; return true;
                case "moon": kind = SceneKind.Moon; return true;
                case "outback": kind = SceneKind.Outback; return true;
                case "tropical": kind = SceneKind.Tropical; return true;
                case "volcano": kind = SceneKind.Volcano; return true;
                case "mars": kind = SceneKind.Mars; return true;
                case "storm": kind = SceneKind.Storm; return true;
                default: kind = default; return false;
            }
        }

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
                    SeedBirdFlock();        //the shared flock is sized from all the scenes that draw it
                    break;
                case OutbackSceneConfig outback:
                    _outbackConfig = outback;
                    ApplyOutbackParameters();
                    SeedBirdFlock();        //the shared flock is sized from all the scenes that draw it
                    break;
                case TropicalSceneConfig tropical:
                    _tropicalConfig = tropical;
                    ApplyTropicalParameters();
                    BuildTropicalBuffers(); //palm and rock positions depend on the terrain, so the change re-plants them
                    SeedBirdFlock();        //the shared flock is sized from all the scenes that draw it
                    break;
                case VolcanoSceneConfig volcano:
                    _volcanoConfig = volcano;
                    ApplyVolcanoParameters();
                    BuildVolcanoBuffers(); //the vents and the rivers stand on the terrain, so a terrain edit re-solves them
                    break;
                case MarsSceneConfig mars:
                    _marsConfig = mars;
                    ApplyMarsParameters();
                    break;
                case StormSceneConfig storm:
                    _stormConfig = storm;
                    ApplyStormParameters();
                    break;
                case SavannaSceneConfig savanna:
                    _savannaConfig = savanna;
                    ApplySavannaParameters();
                    ApplyAcaciaParameters();
                    BuildAcaciaBuffers();   //tree positions depend on the terrain, so the terrain change rebuilds them
                    BuildHearthStones();    //and so do the fires' own hearths, which stand on it too
                    SeedBirdFlock();        //the shared flock is sized from all the scenes that draw it
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
                case MoonSceneConfig moon:
                    _moonConfig = moon;
                    ApplyMoonParameters();
                    break;
                case CitySceneConfig:
                    break;
            }
        }

        /// <summary>
        /// The light rig a scene states for itself instead of taking the sky dome's, and false when it takes
        /// the dome's like every other one. The <b>sky-replacing</b> scenes state one as a group — space, the
        /// dream and the cavern (<see cref="ReplacesSky"/>) — each with its own colours, and they have to: they
        /// draw no dome, so a dome-derived rig would be a lie — and the specific lie is expensive, because the
        /// darkest dome halves the sun through the key tint and takes the metallic drain beads with it. See
        /// <see cref="SpaceLightingConfig"/> for the argument in full; it was written when space was the only
        /// one and this doc went on saying so for two scenes longer than it was true.
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

                //The Moon's is the one rig whose GROUND half outshines its sky half: the sky is black and
                //the sunlit regolith below is the only diffuse source there is — Apollo photographs fill
                //their shadows from the ground, not the sky (see MoonLightingConfig).
                case SceneKind.Moon:
                    MoonLightingConfig moon = _moonConfig.Lighting;
                    rig = new SceneLightRig(
                        moon.SkyAmbient.ToVector3(),
                        moon.GroundAmbient.ToVector3(),
                        moon.KeyTint.ToVector3(),
                        moon.BackTint.ToVector3());
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
        /// The Moon scene's earthshine, as a scene point light the caller can drop into a slot — the space
        /// planetshine's argument restated for the Earth: a real light rather than more ambient, so it is
        /// directional and the metallic drain beads get a highlight out of it, standing far enough off along
        /// the Earth's own direction that it reads as parallel light. False when there is no Earth, no
        /// earthshine or the scene is not the Moon.
        /// </summary>
        public bool TryGetMoonEarthshine(SceneKind kind, out Vector3 position, out Vector3 color, out float range)
        {
            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;

            MoonEarthConfig earth = _moonConfig.Earth;
            MoonLightingConfig lighting = _moonConfig.Lighting;

            if (kind != SceneKind.Moon || lighting.EarthshineStrength <= 0f || earth.AngularRadiusDegrees <= 0f) return false;

            Vector3 direction = SafeNormal(earth.Direction.ToVector3(), Vector3.Forward);

            position = direction * lighting.EarthshineDistance;

            //Earthshine is sunlight bounced off a mostly-ocean, mostly-cloud disc, so its hue is the
            //marble's own: the cloud white pulled towards the ocean blue. Normalized like the planetshine,
            //so the strength alone says how bright the fill is and the colours only say its hue.
            Vector3 albedo = earth.CloudColor.ToVector3() * 0.6f + earth.OceanColor.ToVector3() * 0.4f;
            float peak = MathF.Max(MathF.Max(albedo.X, albedo.Y), MathF.Max(albedo.Z, 1e-4f));

            color = albedo / peak * lighting.EarthshineStrength;

            range = lighting.EarthshineDistance * 3f;

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
            SceneKind.Moon => _moonConfig,
            SceneKind.Outback => _outbackConfig,
            SceneKind.Tropical => _tropicalConfig,
            SceneKind.Volcano => _volcanoConfig,
            SceneKind.Mars => _marsConfig,
            SceneKind.Storm => _stormConfig,
            _ => null,
        };

        private void ApplySeaParameters()
        {
            _seaEffect.Parameters["SeaLevelY"].SetValue(_seaConfig.LevelY);
            _seaEffect.Parameters["WaterColorDeep"].SetValue(_seaConfig.WaterDeep.ToVector3());
            _seaEffect.Parameters["WaterColorShallow"].SetValue(_seaConfig.WaterShallow.ToVector3());
            _seaEffect.Parameters["ShallowBias"].SetValue(_seaConfig.ShallowBias);
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
            _desertEffect.Parameters["DustStrength"].SetValue(_desertConfig.DustStrength);
            _desertEffect.Parameters["DustSpeed"].SetValue(_desertConfig.DustSpeed);
            _desertEffect.Parameters["DustStart"].SetValue(_desertConfig.DustStart);
            _desertEffect.Parameters["SandColor"].SetValue(_desertConfig.SandColor.ToVector3());
            _desertEffect.Parameters["SandColorPale"].SetValue(_desertConfig.SandColorPale.ToVector3());
            _desertEffect.Parameters["SheenStrength"].SetValue(_desertConfig.SheenStrength);
            _desertEffect.Parameters["AmbientStrength"].SetValue(_desertConfig.AmbientStrength);
            _desertEffect.Parameters["WindDirection"].SetValue(_desertConfig.Wind.ToVector2());
            _desertEffect.Parameters["HorizonHazeDistance"].SetValue(_desertConfig.HorizonHazeDistance);
        }

        private void ApplyOutbackParameters()
        {
            OutbackTerrainConfig terrain = _outbackConfig.Terrain;
            OutbackSurfaceConfig surface = _outbackConfig.Surface;
            OutbackAirConfig air = _outbackConfig.Air;

            _outbackEffect.Parameters["OutbackLevelY"].SetValue(terrain.LevelY);
            _outbackEffect.Parameters["PlainRelief"].SetValue(terrain.PlainRelief);
            _outbackEffect.Parameters["ClearingRadius"].SetValue(terrain.ClearingRadius);
            _outbackEffect.Parameters["ClearingTransition"].SetValue(terrain.ClearingTransition);

            //The spacings divide a world position in the shader, so a zero would take the whole terrain with it
            //(a NaN height field is a mesh that vanishes, and the property grid is one keystroke from a zero).
            _outbackEffect.Parameters["RockSpacing"].SetValue(MathF.Max(terrain.RockSpacing, 1f));
            _outbackEffect.Parameters["RockChance"].SetValue(terrain.RockChance);
            _outbackEffect.Parameters["RockHeight"].SetValue(terrain.RockHeight);
            _outbackEffect.Parameters["OutcropSpacing"].SetValue(MathF.Max(terrain.OutcropSpacing, 1f));
            _outbackEffect.Parameters["OutcropChance"].SetValue(terrain.OutcropChance);
            _outbackEffect.Parameters["OutcropHeight"].SetValue(terrain.OutcropHeight);

            _outbackEffect.Parameters["RockColorDeep"].SetValue(surface.RockColorDeep.ToVector3());
            _outbackEffect.Parameters["RockColorBright"].SetValue(surface.RockColorBright.ToVector3());
            _outbackEffect.Parameters["VarnishColor"].SetValue(surface.VarnishColor.ToVector3());
            _outbackEffect.Parameters["VarnishStrength"].SetValue(surface.VarnishStrength);
            _outbackEffect.Parameters["VarnishGloss"].SetValue(surface.VarnishGloss);
            _outbackEffect.Parameters["RibCount"].SetValue(surface.RibCount);
            _outbackEffect.Parameters["RibDepth"].SetValue(surface.RibDepth);
            _outbackEffect.Parameters["RockRelief"].SetValue(surface.RockRelief);
            _outbackEffect.Parameters["SoilColor"].SetValue(surface.SoilColor.ToVector3());
            _outbackEffect.Parameters["SoilColorPale"].SetValue(surface.SoilColorPale.ToVector3());
            _outbackEffect.Parameters["SpinifexColor"].SetValue(surface.SpinifexColor.ToVector3());
            _outbackEffect.Parameters["SpinifexSpacing"].SetValue(MathF.Max(surface.SpinifexSpacing, 0.05f));
            _outbackEffect.Parameters["SpinifexCover"].SetValue(surface.SpinifexCover);
            _outbackEffect.Parameters["SpinifexRelief"].SetValue(surface.SpinifexRelief);
            _outbackEffect.Parameters["AmbientStrength"].SetValue(surface.AmbientStrength);

            _outbackEffect.Parameters["HazeTint"].SetValue(air.HazeTint.ToVector3());
            _outbackEffect.Parameters["DustStrength"].SetValue(air.DustStrength);
            _outbackEffect.Parameters["HorizonHazeDistance"].SetValue(air.HorizonHazeDistance);
            _outbackEffect.Parameters["HeatShimmer"].SetValue(air.HeatShimmer);
            _outbackEffect.Parameters["WindDirection"].SetValue(air.Wind.ToVector2());
        }

        /// <summary>
        /// Pushes the tropical terrain's static tuning into <c>Tropical.fx</c> and stores the scatter's
        /// per-draw colours. The lagoon's water uniforms are deliberately NOT touched here — the sea
        /// effect belongs to whichever water draw ran last, and each of the two pushes its whole set
        /// per frame (see <see cref="DrawTropicalWater"/>).
        /// </summary>
        private void ApplyTropicalParameters()
        {
            TropicalTerrainConfig terrain = _tropicalConfig.Terrain;
            PalmConfig palms = _tropicalConfig.Palms;
            TropicalRockConfig rocks = _tropicalConfig.Rocks;

            _tropicalEffect.Parameters["TropicalLevelY"].SetValue(terrain.LevelY);
            _tropicalEffect.Parameters["ClearingRelief"].SetValue(terrain.ClearingRelief);
            _tropicalEffect.Parameters["ShoreRadius"].SetValue(terrain.ShoreRadius);
            _tropicalEffect.Parameters["CoastNoise"].SetValue(terrain.CoastNoise);
            _tropicalEffect.Parameters["BeachRise"].SetValue(MathF.Max(terrain.BeachRise, 0.5f));
            _tropicalEffect.Parameters["BeachRun"].SetValue(MathF.Max(terrain.BeachRun, 0.5f));
            _tropicalEffect.Parameters["SeabedY"].SetValue(terrain.SeabedY);
            _tropicalEffect.Parameters["RingRadius"].SetValue(terrain.RingRadius);
            _tropicalEffect.Parameters["RingNoise"].SetValue(terrain.RingNoise);
            _tropicalEffect.Parameters["RingWidth"].SetValue(MathF.Max(terrain.RingWidth, 1f));
            _tropicalEffect.Parameters["HillHeight"].SetValue(terrain.HillHeight);
            _tropicalEffect.Parameters["ChannelBearing"].SetValue(terrain.ChannelBearing);
            _tropicalEffect.Parameters["ChannelSharpness"].SetValue(MathF.Max(terrain.ChannelSharpness, 1f));

            //The terrain reads the water level so the wet sand band and the far shore's fringe sit on
            //the waterline the water itself draws (Sea.fx) — the two cannot drift apart.
            _tropicalEffect.Parameters["WaterLevelY"].SetValue(_tropicalConfig.Water.LevelY);

            _tropicalEffect.Parameters["SandColor"].SetValue(terrain.SandColor.ToVector3());
            _tropicalEffect.Parameters["SandColorPale"].SetValue(terrain.SandColorPale.ToVector3());
            _tropicalEffect.Parameters["VegetationColor"].SetValue(terrain.VegetationColor.ToVector3());
            _tropicalEffect.Parameters["VegetationDry"].SetValue(terrain.VegetationDry.ToVector3());
            _tropicalEffect.Parameters["CanopyWindStrength"].SetValue(terrain.CanopyWindStrength);
            _tropicalEffect.Parameters["CanopyRelief"].SetValue(terrain.CanopyRelief);
            _tropicalEffect.Parameters["SandRelief"].SetValue(terrain.SandRelief);
            _tropicalEffect.Parameters["AmbientStrength"].SetValue(terrain.AmbientStrength);
            _tropicalEffect.Parameters["WindDirection"].SetValue(terrain.Wind.ToVector2());
            _tropicalEffect.Parameters["HazeTint"].SetValue(terrain.HazeTint.ToVector3());
            _tropicalEffect.Parameters["HazeStrength"].SetValue(terrain.HazeStrength);
            _tropicalEffect.Parameters["HorizonHazeDistance"].SetValue(terrain.HorizonHazeDistance);

            //Stored rather than pushed: the palms' and rocks' colours are the per-draw DiffuseColor now,
            //set as each mesh part draws in DrawPalms and DrawTropicalRocks.
            _palmFrondColor = palms.FrondColor.ToVector3();
            _palmFrondDry = palms.FrondDry.ToVector3();
            _palmTrunkColor = palms.TrunkColor.ToVector3();
            _tropicalStoneColor = rocks.StoneColor.ToVector3();
            _tropicalMossColor = rocks.MossColor.ToVector3();
        }

        /// <summary>
        /// Pushes the volcano's static tuning into <c>Volcano.fx</c>, <c>LavaFountain.fx</c> and <c>Ash.fx</c>.
        /// The rivers' bearings, the vents and the two particle buffers depend on the terrain, so they are
        /// <see cref="BuildVolcanoBuffers"/>'s and are solved after this.
        /// </summary>
        private void ApplyVolcanoParameters()
        {
            VolcanoSceneConfig volcano = _volcanoConfig;

            _volcanoEffect.Parameters["VolcanoLevelY"].SetValue(volcano.LevelY);
            _volcanoEffect.Parameters["ClearingRadius"].SetValue(volcano.ClearingRadius);
            _volcanoEffect.Parameters["ClearingTransition"].SetValue(MathF.Max(volcano.ClearingTransition, 1f));
            _volcanoEffect.Parameters["ConeCenterXZ"].SetValue(volcano.ConeCenter.ToVector2());
            _volcanoEffect.Parameters["ConeRadius"].SetValue(MathF.Max(volcano.ConeRadius, 1f));
            _volcanoEffect.Parameters["ConeHeight"].SetValue(volcano.ConeHeight);
            _volcanoEffect.Parameters["ConeProfile"].SetValue(MathF.Max(volcano.ConeProfile, 0.1f));
            _volcanoEffect.Parameters["CraterRadius"].SetValue(MathF.Max(volcano.CraterRadius, 1f));
            _volcanoEffect.Parameters["CraterDepth"].SetValue(volcano.CraterDepth);
            _volcanoEffect.Parameters["GullyDepth"].SetValue(volcano.GullyDepth);

            //ROUNDED, and the shader says why: every bearing term is a multiple of this figure, and only an
            //integer multiple of atan2's angle closes across the ±π seam. A fractional count leaves a straight
            //scar running from the crater to the horizon. Rounded here rather than in the shader because it is
            //a per-config decision, not a per-vertex one.
            _volcanoEffect.Parameters["GullyCount"].SetValue(MathF.Round(MathF.Max(volcano.GullyCount, 1f)));

            _volcanoEffect.Parameters["ScoriaRelief"].SetValue(volcano.ScoriaRelief);
            _volcanoEffect.Parameters["RiverWidth"].SetValue(MathF.Max(volcano.RiverWidth, 0.5f));
            _volcanoEffect.Parameters["RiverWander"].SetValue(volcano.RiverWander);
            _volcanoEffect.Parameters["RiverSpeed"].SetValue(volcano.RiverSpeed);
            _volcanoEffect.Parameters["HaloWidth"].SetValue(MathF.Max(volcano.HaloWidth, 1.05f));
            _volcanoEffect.Parameters["RockColor"].SetValue(volcano.RockColor.ToVector3());
            _volcanoEffect.Parameters["RockColorLight"].SetValue(volcano.RockColorLight.ToVector3());
            _volcanoEffect.Parameters["LavaHot"].SetValue(volcano.LavaHot.ToVector3());
            _volcanoEffect.Parameters["LavaCool"].SetValue(volcano.LavaCool.ToVector3());
            _volcanoEffect.Parameters["SeamGlow"].SetValue(volcano.SeamGlow);
            _volcanoEffect.Parameters["PlateSize"].SetValue(MathF.Max(volcano.PlateSize, 0.1f));
            _volcanoEffect.Parameters["AmbientStrength"].SetValue(volcano.AmbientStrength);
            _volcanoEffect.Parameters["HorizonHazeDistance"].SetValue(MathF.Max(volcano.HorizonHazeDistance, 1f));
            _volcanoEffect.Parameters["HazeTint"].SetValue(volcano.HazeTint.ToVector3());
            _volcanoEffect.Parameters["HazeStrength"].SetValue(volcano.HazeStrength);
            _volcanoEffect.Parameters["WindDirection"].SetValue(volcano.Wind.ToVector2());

            LavaFountainConfig fountains = volcano.Fountains;

            _fountainEffect.Parameters["LaunchSpeed"].SetValue(fountains.Speed);
            _fountainEffect.Parameters["LaunchSpread"].SetValue(fountains.Spread);
            _fountainEffect.Parameters["BlobGravity"].SetValue(MathF.Max(fountains.Gravity, 0.1f));
            _fountainEffect.Parameters["BlobLife"].SetValue(MathF.Max(fountains.Life, 0.1f));
            _fountainEffect.Parameters["BlobSize"].SetValue(fountains.BlobSize);
            _fountainEffect.Parameters["WindDirection"].SetValue(volcano.Wind.ToVector2());
            _fountainEffect.Parameters["WindDrag"].SetValue(fountains.WindDrag);
            _fountainEffect.Parameters["EruptionBoost"].SetValue(volcano.Eruption.Boost);
            _fountainEffect.Parameters["LavaHot"].SetValue(volcano.LavaHot.ToVector3());
            _fountainEffect.Parameters["LavaCool"].SetValue(volcano.LavaCool.ToVector3());
            _fountainEffect.Parameters["PlumeColor"].SetValue(fountains.PlumeColor.ToVector3());
            _fountainEffect.Parameters["PlumeStrength"].SetValue(fountains.PlumeStrength);

            //The plume's own figures are derived from the jets' rather than being four more dials: a column
            //rises about a third as fast as a blob is thrown, lives long enough to leave the frame, and
            //spreads to a good fraction of the crater it is standing in. Deriving them keeps a retuned
            //fountain and its own smoke in proportion, which is what a designer moving Speed actually wants.
            _fountainEffect.Parameters["PlumeRise"].SetValue(fountains.Speed * 0.55f);
            _fountainEffect.Parameters["PlumeSpread"].SetValue(volcano.CraterRadius * 0.8f);
            _fountainEffect.Parameters["PlumeLife"].SetValue(fountains.Life * 7f);
            _fountainEffect.Parameters["PlumeSize"].SetValue(fountains.BlobSize * 5f);

            AshConfig ash = volcano.Ash;

            _ashEffect.Parameters["AshBoxSize"].SetValue(ash.BoxSize.ToVector3());
            _ashEffect.Parameters["AshFallSpeed"].SetValue(ash.FallSpeed);
            _ashEffect.Parameters["AshWind"].SetValue(ash.Wind.ToVector2());
            _ashEffect.Parameters["AshSway"].SetValue(ash.Sway);
            _ashEffect.Parameters["SpeckSize"].SetValue(ash.FlakeSize);
            _ashEffect.Parameters["AshSpin"].SetValue(ash.Spin);
            _ashEffect.Parameters["AshNearFade"].SetValue(MathF.Max(ash.NearFade, 0.1f));
            _ashEffect.Parameters["EmberFraction"].SetValue(ash.EmberFraction);
            _ashEffect.Parameters["AshColor"].SetValue(ash.AshColor.ToVector3());
            _ashEffect.Parameters["EmberColor"].SetValue(ash.EmberColor.ToVector3());
            _ashEffect.Parameters["AshOpacity"].SetValue(ash.Opacity);
        }

        /// <summary>
        /// Pushes Mars's static tuning into <c>Mars.fx</c> — the crater field's amplitude and clearing, the
        /// rust surface, the dust haze, and Phobos's and Deimos's directions (elevation/azimuth degrees,
        /// converted here the way <see cref="SkyDome"/> converts its own <c>SUNS</c> table, so a config can
        /// never roll a zero-length direction).
        /// </summary>
        private void ApplyMarsParameters()
        {
            MarsTerrainConfig terrain = _marsConfig.Terrain;
            MarsSurfaceConfig surface = _marsConfig.Surface;
            MarsAirConfig air = _marsConfig.Air;
            MarsMoonsConfig moons = _marsConfig.Moons;

            _marsEffect.Parameters["MarsLevelY"].SetValue(terrain.LevelY);
            _marsEffect.Parameters["ClearingRadius"].SetValue(terrain.ClearingRadius);
            _marsEffect.Parameters["ClearingTransition"].SetValue(terrain.ClearingTransition);
            _marsEffect.Parameters["CraterAmplitude"].SetValue(terrain.CraterAmplitude);

            //The spacings divide a world position in the shader, so a zero would take the whole terrain
            //with it (the outback's own guard).
            _marsEffect.Parameters["RockSpacing"].SetValue(MathF.Max(terrain.RockSpacing, 1f));
            _marsEffect.Parameters["RockChance"].SetValue(terrain.RockChance);
            _marsEffect.Parameters["RockHeight"].SetValue(terrain.RockHeight);
            _marsEffect.Parameters["PebbleSpacing"].SetValue(MathF.Max(terrain.PebbleSpacing, 1f));
            _marsEffect.Parameters["PebbleChance"].SetValue(terrain.PebbleChance);
            _marsEffect.Parameters["PebbleHeight"].SetValue(terrain.PebbleHeight);

            _marsEffect.Parameters["RustColor"].SetValue(surface.RustColor.ToVector3());
            _marsEffect.Parameters["RustColorPale"].SetValue(surface.RustColorPale.ToVector3());
            _marsEffect.Parameters["EjectaBrightness"].SetValue(surface.EjectaBrightness);
            _marsEffect.Parameters["MicroReliefStrength"].SetValue(surface.MicroReliefStrength);
            _marsEffect.Parameters["GrainStrength"].SetValue(surface.GrainStrength);
            _marsEffect.Parameters["AmbientStrength"].SetValue(surface.AmbientStrength);
            _marsEffect.Parameters["BoulderColorDeep"].SetValue(surface.BoulderColorDeep.ToVector3());
            _marsEffect.Parameters["BoulderColorBright"].SetValue(surface.BoulderColorBright.ToVector3());
            _marsEffect.Parameters["RockRelief"].SetValue(surface.RockRelief);

            _marsEffect.Parameters["HazeTint"].SetValue(air.HazeTint.ToVector3());
            _marsEffect.Parameters["DustStrength"].SetValue(air.DustStrength);
            _marsEffect.Parameters["HorizonHazeDistance"].SetValue(air.HorizonHazeDistance);

            _marsEffect.Parameters["PhobosDirection"].SetValue(DirectionFromElevationAzimuth(moons.PhobosElevation, moons.PhobosAzimuth));
            _marsEffect.Parameters["PhobosAngularRadius"].SetValue(MathHelper.ToRadians(MathF.Max(moons.PhobosAngularRadiusDegrees, 0f)));
            _marsEffect.Parameters["PhobosColor"].SetValue(moons.PhobosColor.ToVector3());

            _marsEffect.Parameters["DeimosDirection"].SetValue(DirectionFromElevationAzimuth(moons.DeimosElevation, moons.DeimosAzimuth));
            _marsEffect.Parameters["DeimosAngularRadius"].SetValue(MathHelper.ToRadians(MathF.Max(moons.DeimosAngularRadiusDegrees, 0f)));
            _marsEffect.Parameters["DeimosColor"].SetValue(moons.DeimosColor.ToVector3());
        }

        /// <summary>Pushes the storm's static tuning into <c>Storm.fx</c>. The flash's own per-frame values
        /// are pushed by <see cref="DrawStorm"/>, since they come off the wall clock.</summary>
        private void ApplyStormParameters()
        {
            StormDeckConfig deck = _stormConfig.Deck;
            StormSurfaceConfig surface = _stormConfig.Surface;
            StormAirConfig air = _stormConfig.Air;

            _stormEffect.Parameters["StormLevelY"].SetValue(deck.LevelY);
            _stormEffect.Parameters["ClearingRadius"].SetValue(deck.ClearingRadius);
            _stormEffect.Parameters["ClearingTransition"].SetValue(MathF.Max(deck.ClearingTransition, 1f));
            _stormEffect.Parameters["BillowHeight"].SetValue(deck.BillowHeight);

            //The spacing divides a world position in the shader, so a zero would take the whole deck with it
            //(a NaN height field is a mesh that vanishes, and the property grid is one keystroke from a zero).
            _stormEffect.Parameters["TurretSpacing"].SetValue(MathF.Max(deck.TurretSpacing, 1f));
            _stormEffect.Parameters["TurretChance"].SetValue(deck.TurretChance);
            _stormEffect.Parameters["TurretHeight"].SetValue(deck.TurretHeight);
            _stormEffect.Parameters["AnvilSpread"].SetValue(MathF.Max(deck.AnvilSpread, 1f));

            _stormEffect.Parameters["TopColor"].SetValue(surface.TopColor.ToVector3());
            _stormEffect.Parameters["ShadeColor"].SetValue(surface.ShadeColor.ToVector3());
            _stormEffect.Parameters["BaseColor"].SetValue(surface.BaseColor.ToVector3());
            _stormEffect.Parameters["BillowRelief"].SetValue(surface.BillowRelief);
            _stormEffect.Parameters["SilverStrength"].SetValue(surface.SilverStrength);
            _stormEffect.Parameters["AmbientStrength"].SetValue(surface.AmbientStrength);

            _stormEffect.Parameters["FlashColor"].SetValue(_stormConfig.Flash.Color.ToVector3());
            _stormEffect.Parameters["FlashDeckGlow"].SetValue(
                _stormConfig.Flash.DeckGlow * (_stormConfig.Deck.LightningFlashesInside ? 1f : 0f));

            //How far a strike's glow carries across the deck. ⚠ It is its OWN dial and not a figure derived
            //from the turret spacing, which is what the first build did (spacing x 0.8 = 136 units) — and
            //136 units around a strike standing 173 to 348 units out is a patch smaller than the gap to it,
            //so the glow landed almost entirely outside the frame and the flash read as not working at all.
            //Diagnosed by removing the falloff outright: the same build then blew the whole deck from a mean
            //luminance of 189 to 255, which is what said the plumbing was sound and the radius was not.
            _stormEffect.Parameters["FlashReach"].SetValue(MathF.Max(_stormConfig.Flash.GlowReach, 1f));

            _stormEffect.Parameters["HazeTint"].SetValue(air.HazeTint.ToVector3());
            _stormEffect.Parameters["HorizonHazeDistance"].SetValue(MathF.Max(air.HorizonHazeDistance, 1f));
            _stormEffect.Parameters["HazeStrength"].SetValue(air.HazeStrength);
            _stormEffect.Parameters["WindDirection"].SetValue(air.Wind.ToVector2());
            _stormEffect.Parameters["DriftSpeed"].SetValue(air.DriftSpeed);
        }

        /// <summary>
        /// The storm's lightning envelope at a wall-clock time: 0 between strikes, rising to 1 at a
        /// strike's peak. <b>A pure function of the clock with no state at all</b>, exactly as
        /// <see cref="VolcanoEruption"/> is and for the same reason — the Game, the Testbed and the map
        /// editor then all see the same strike at the same second, nothing has to be saved or synchronised,
        /// and (the reason it is <c>public</c>) a sound can hang off it without owning the schedule.
        /// <para>
        /// The shape is a flash and not a burst: a near-instant attack, a fast decay, and a <b>flicker</b>
        /// over the whole envelope, because real lightning is a train of return strokes and that stutter is
        /// most of what says "lightning" rather than "a light being switched on". Each strike's moment
        /// within its period and its size are hashed off the period's own index, so it never becomes a
        /// metronome — the volcano's own rule.
        /// </para>
        /// </summary>
        public float StormFlash(float time)
        {
            StormFlashConfig flash = _stormConfig.Flash;

            float period = MathF.Max(flash.Period, 0.5f);
            float u = time / period;
            float index = MathF.Floor(u);

            float start = 0.08f + 0.62f * Hash01(index);
            float length = Math.Clamp(MathF.Max(flash.Length, 0.05f) / period, 0.01f, 0.7f);

            float p = (u - index - start) / length;
            if (p <= 0f || p >= 1f) return 0f;

            //A hard attack over the first 6 % and a fast power decay after it.
            float envelope = p < 0.06f ? p / 0.06f : MathF.Pow(1f - (p - 0.06f) / 0.94f, 2.6f);

            //The return strokes. Rectified so every flicker is a brightening rather than a sign change, and
            //floored well above zero so the channel never goes fully dark mid-strike (which reads as two
            //separate strikes rather than one stuttering one).
            float flicker = MathF.Max(flash.Flicker, 0f);
            if (flicker > 0f)
                envelope *= 0.55f + 0.45f * MathF.Abs(MathF.Cos(p * MathHelper.Pi * flicker));

            //Not every strike is the same size: a scene whose every event is identical stops having events.
            return envelope * (0.5f + 0.5f * Hash01(index + 313f));
        }

        /// <summary>
        /// Where the current strike stands, in the XZ plane. Hashed off the same period index the envelope
        /// is, so the flash's glow and the light it throws cannot disagree about which cell went off — and
        /// held out past the clearing, because a strike inside the ring the island stands in would be a
        /// bolt in the play field rather than weather in the distance.
        /// </summary>
        private Vector2 StormFlashCenter(float time)
        {
            float period = MathF.Max(_stormConfig.Flash.Period, 0.5f);
            float index = MathF.Floor(time / period);

            float bearing = Hash01(index + 57f) * MathHelper.TwoPi;
            float reach = _stormConfig.Deck.ClearingRadius + 60f
                + Hash01(index + 991f) * MathF.Max(_stormConfig.Deck.TurretSpacing, 1f) * 1.2f;

            return new Vector2(MathF.Cos(bearing) * reach, MathF.Sin(bearing) * reach);
        }

        /// <summary>
        /// The storm's flash as a scene point light the caller can drop into a slot — the Moon's earthshine
        /// and the space planetshine's own recipe, and the answer to "how does a flash reach the arena when
        /// the deck throwing it is out of the play frame".
        /// <para>
        /// <b>A lamp far along −Y, not a rig override.</b> At this distance <c>dot(N, L)</c> is ≈ 1 on every
        /// downward-facing normal and ≈ 0 on every upward one, which is precisely "the deck below flashed":
        /// the undersides of the balls, the island's coping and the gun's carriage catch it and their tops
        /// keep the dome. It is additive over the dome's own rig by construction, is pushed every frame by
        /// both hosts already, and reaches every instanced surface through the shared effect — including the
        /// drain's gold beads, which have almost nothing but reflections to show.
        /// </para>
        /// <para>
        /// <b>⚠ The range must comfortably exceed the distance or the lamp arrives as literally nothing</b>:
        /// the shader's attenuation is <c>saturate(1 − dist/range)²</c>, which is exactly 0 at
        /// <c>dist ≥ range</c>. Hence the planetshine's 3× recipe, which puts the attenuation at 4/9 over
        /// the arena and varies it by a few per cent across it.
        /// </para>
        /// </summary>
        public bool TryGetStormFlash(SceneKind kind, float time, out Vector3 position, out Vector3 color, out float range)
        {
            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;

            StormFlashConfig flash = _stormConfig.Flash;

            if (kind != SceneKind.Storm || flash.LightStrength <= 0f) return false;

            float envelope = StormFlash(time);
            if (envelope <= 0f) return false;

            float distance = MathF.Max(flash.LightDistance, 1f);

            //Under the island and leaning towards the cell that actually went off, so the fill has a
            //direction rather than being a flat uplight — but overwhelmingly below, which is what makes it
            //read as the deck and not as a second sun.
            Vector2 at = StormFlashCenter(time);
            Vector3 towards = SafeNormal(new Vector3(at.X * 0.25f, -distance, at.Y * 0.25f), -Vector3.UnitY);

            position = towards * distance;

            //Normalized like the planetshine and the earthshine, so LightStrength alone says how bright the
            //fill is and the colour only says its hue.
            Vector3 tint = flash.Color.ToVector3();
            float peak = MathF.Max(MathF.Max(tint.X, tint.Y), MathF.Max(tint.Z, 1e-4f));

            color = tint / peak * (flash.LightStrength * envelope);

            range = distance * 3f;

            return true;
        }

        /// <summary>
        /// Draws the storm (#219): the grid pinned to the camera (snapped to a cell so the deck does not
        /// swim), carrying the cloud deck and its turrets, shaded per-pixel by the current dome.
        /// <para>
        /// <b>It deliberately does NOT invoke <see cref="SceneFrame.ApplyClouds"/></b>, alone among the
        /// terrain draws. The hook pushes the sky's own weather into a scene effect's <c>Cloud*</c>
        /// namespace, and this deck neither wants a cloud shadow cast on it (it <i>is</i> the cloud) nor
        /// could survive one: <c>CloudSunlight</c> above the shared plane degenerates to the point's own
        /// column and returns about the shadow floor. <c>Storm.fx</c>'s header has the whole argument.
        /// </para>
        /// </summary>
        private void DrawStorm(in SceneFrame frame)
        {
            float cell = STORM_EXTENT / (STORM_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _stormEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _stormEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _stormEffect.Parameters["View"].SetValue(frame.Camera.View);
            _stormEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _stormEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _stormEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _stormEffect.Parameters["SunColor"].SetValue(frame.SunColor);
            _stormEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _stormEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _stormEffect.Parameters["StormTime"].SetValue(frame.Time);

            //The strike, off the same clock and the same hashed period index the light rig's own lamp reads,
            //so the glow in the cloud and the flash on the arena cannot disagree about which cell went off.
            _stormEffect.Parameters["FlashEnvelope"].SetValue(StormFlash(frame.Time));
            _stormEffect.Parameters["FlashCenterXZ"].SetValue(StormFlashCenter(frame.Time));

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_stormVertexBuffer);
            _graphicsDevice.Indices = _stormIndexBuffer;
            _stormEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _stormIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// A unit direction from elevation above the horizon and azimuth from +Z towards +X, both in
        /// degrees — <see cref="SkyDome"/>'s own <c>SUNS</c> convention (see its <c>DomeNumber</c> setter),
        /// reused here so Mars's moons are placed the same designer-facing way a dome's sun is.
        /// </summary>
        private static Vector3 DirectionFromElevationAzimuth(float elevationDegrees, float azimuthDegrees)
        {
            float elevation = MathHelper.ToRadians(elevationDegrees);
            float azimuth = MathHelper.ToRadians(azimuthDegrees);
            float horizontal = MathF.Cos(elevation);

            return new Vector3(horizontal * MathF.Sin(azimuth), MathF.Sin(elevation), horizontal * MathF.Cos(azimuth));
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

            ApplyHearthParameters();
        }

        /// <summary>
        /// The hearth uniforms <c>Savanna.fx</c> burns the ground with (#282), pushed at config time rather
        /// than per frame: the fires stand on static terrain at config-derived places, so every one of these
        /// is constant until the config or the terrain changes — which is when this runs.
        /// <para>
        /// <c>HearthNear</c>/<c>HearthFar</c> are the ring's own extent, measured here <b>from the positions
        /// themselves</b> rather than re-derived from the config's ring rule in the shader: it is the early-out
        /// that keeps the per-pixel hearth loop off the rest of the field, and a second copy of the placement
        /// rule is exactly how a scene grows a fault nobody can see (#297).
        /// </para>
        /// </summary>
        private void ApplyHearthParameters()
        {
            CampfireConfig cf = _savannaConfig.Campfire;
            int fires = SavannaCampfireCount;

            float near = float.MaxValue, far = 0f;
            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 at = SavannaCampfirePosition(fire);
                _hearthPositions[fire] = at;

                float radius = MathF.Sqrt(at.X * at.X + at.Z * at.Z);
                near = MathF.Min(near, radius);
                far = MathF.Max(far, radius);
            }

            _savannaEffect.Parameters["HearthPosition"].SetValue(_hearthPositions);
            _savannaEffect.Parameters["HearthCount"].SetValue(fires);
            _savannaEffect.Parameters["HearthRadius"].SetValue(cf.FlameSize * cf.HearthRadiusScale);
            _savannaEffect.Parameters["HearthNear"].SetValue(near);
            _savannaEffect.Parameters["HearthFar"].SetValue(far);
            _savannaEffect.Parameters["HearthAsh"].SetValue(cf.HearthAsh.ToVector3());
            _savannaEffect.Parameters["HearthChar"].SetValue(cf.HearthChar.ToVector3());
        }

        private void ApplyAcaciaParameters()
        {
            AcaciaConfig ac = _savannaConfig.Acacia;
            //Stored rather than pushed: the colours are the per-draw DiffuseColor now (the canopy green, its
            //drier shade, and the trunk brown), set as each mesh part draws in DrawAcacias.
            _acaciaCanopyColor = ac.CanopyColor.ToVector3();
            _acaciaCanopyDry = ac.CanopyDry.ToVector3();
            _acaciaTrunkColor = ac.TrunkColor.ToVector3();
        }

        /// <summary>
        /// (Re)builds the acacia scatter: the tree and bush mesh variants and the per-variant instance
        /// matrices, each plant planted on the terrain in a clump around a cluster centre (or, for a few,
        /// solo). The meshes are real 3D geometry now (#202), so this rebuilds them too — a plant's height
        /// comes off the ground it stands on, so a terrain change re-plants the whole scatter. Deterministic
        /// seed, so the same config always gives the same savanna.
        /// </summary>
        private void BuildAcaciaBuffers()
        {
            DisposeAcacia();

            AcaciaConfig ac = _savannaConfig.Acacia;
            Random rng = new(90125);

            //A handful of variants of each kind, built at rolled proportions and structural seeds, so a grove
            //is a mix rather than one tree stamped out — the eye reads the repeat before it reads the tree
            //(the forest's own lesson). ac.Width is the canopy half-width the billboard was cut to; the 3D tree
            //keeps that footprint, with a slim trunk a fraction of it and a canopy a fraction of the height.
            const int TREE_VARIANTS = 4, BUSH_VARIANTS = 2;
            _acaciaTreeMeshes = new AcaciaMesh[TREE_VARIANTS];
            _acaciaTreeDryness = new float[TREE_VARIANTS];
            for (int m = 0; m < TREE_VARIANTS; m++)
            {
                float w = 0.8f + 0.45f * (float)rng.NextDouble();
                float h = 0.85f + 0.4f * (float)rng.NextDouble();
                _acaciaTreeMeshes[m] = new AcaciaMesh(_graphicsDevice,
                    trunkRadius: ac.Width * 0.09f * w,
                    treeHeight: ac.Height * h,
                    canopyRadius: ac.Width * w,
                    seed: 4100 + m);
                _acaciaTreeDryness[m] = (float)rng.NextDouble();
            }

            _acaciaBushMeshes = new FoliageMesh[BUSH_VARIANTS];
            for (int m = 0; m < BUSH_VARIANTS; m++)
            {
                float br = ac.Width * (0.5f + 0.2f * (float)rng.NextDouble());
                float bh = ac.Height * (0.22f + 0.08f * (float)rng.NextDouble());
                _acaciaBushMeshes[m] = new FoliageMesh(_graphicsDevice, br, bh, centreY: bh, seed: 4200 + m);
            }

            var treeBuckets = new List<ModelInstance>[TREE_VARIANTS];
            for (int m = 0; m < TREE_VARIANTS; m++) treeBuckets[m] = new List<ModelInstance>();
            var bushBuckets = new List<ModelInstance>[BUSH_VARIANTS];
            for (int m = 0; m < BUSH_VARIANTS; m++) bushBuckets[m] = new List<ModelInstance>();

            //Cluster centres the plants gather around, so the savanna reads as clumps of trees rather than an
            //even scatter. A minority of plants are placed solo.
            float[] clusterX = new float[ac.Clusters];
            float[] clusterZ = new float[ac.Clusters];
            for (int c = 0; c < ac.Clusters; c++)
            {
                float ca = (float)rng.NextDouble() * MathHelper.TwoPi;
                float cr = ac.MinRadius + (float)rng.NextDouble() * (ac.MaxRadius - ac.MinRadius);
                clusterX[c] = MathF.Cos(ca) * cr;
                clusterZ[c] = MathF.Sin(ca) * cr;
            }

            //What is already standing, so the next plant can be kept out of it — ScatterSpacing's rule, one
            //copy with the forest's (#108): with most plants clumping around a centre whose density rises
            //inwards, two landing on top of each other is the expected case without it.
            List<ScatterSpacing.Footprint> standing = new(ac.Count);

            for (int i = 0; i < ac.Count; i++)
            {
                //Rolled BEFORE the position, because the position depends on how wide this plant is: a bush
                //needs a third of a tree's room and should not be pushed out as though it needed all of it.
                float rand = (float)rng.NextDouble();
                bool isBush = rng.NextDouble() < ac.BushFraction;
                //A per-plant uniform scale around 1, so one variant mesh reads as several trees.
                float sizeScale = isBush ? 0.7f + 0.6f * rand : 0.8f + 0.5f * rand;
                float halfWidth = (isBush ? ac.Width * 0.5f : ac.Width) * sizeScale;

                float x = 0f, z = 0f;
                float bestClearance = float.NegativeInfinity;

                for (int attempt = 0; attempt < ScatterSpacing.TRIES; attempt++)
                {
                    float cx, cz;
                    if (rng.NextDouble() < 0.82) //most plants clump around a cluster centre
                    {
                        int c = rng.Next(ac.Clusters);
                        float off = (float)rng.NextDouble();
                        float d = off * off * ac.ClusterSpread; //denser towards the centre
                        float da = (float)rng.NextDouble() * MathHelper.TwoPi;
                        cx = clusterX[c] + MathF.Cos(da) * d;
                        cz = clusterZ[c] + MathF.Sin(da) * d;
                    }
                    else //the odd solitary plant, anywhere in the ring
                    {
                        float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                        float r = ac.MinRadius + (float)rng.NextDouble() * (ac.MaxRadius - ac.MinRadius);
                        cx = MathF.Cos(a) * r;
                        cz = MathF.Sin(a) * r;
                    }

                    //Keep clear of the island
                    float dist = MathF.Sqrt(cx * cx + cz * cz);
                    if (dist < ac.MinRadius && dist > 0.01f)
                    {
                        cx *= ac.MinRadius / dist;
                        cz *= ac.MinRadius / dist;
                    }

                    float clearance = ScatterSpacing.Clearance(cx, cz, halfWidth, standing);

                    if (clearance > bestClearance)
                    {
                        bestClearance = clearance;
                        x = cx;
                        z = cz;
                    }

                    if (clearance >= 0f) break;
                }

                standing.Add(new ScatterSpacing.Footprint(x, z, halfWidth));

                Vector3 basePos = new(x, SavannaTerrainHeight(x, z), z);

                //The plant's own frame: a small lean off vertical (a leaning tree reads as a tree, a tilted one
                //as a felled one — the forest's TREE_LEAN), a free yaw, the uniform size, and planted on the
                //ground. Scale first so it stays uniform, then the tilt and spin, then the translation.
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float lean = 0.06f * (float)rng.NextDouble();
                float leanDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                Matrix world = Matrix.CreateScale(sizeScale)
                    * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(leanDir), 0f, MathF.Sin(leanDir)), lean)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(basePos);
                var instance = new ModelInstance(world, Vector4.Zero);

                if (isBush) bushBuckets[rng.Next(BUSH_VARIANTS)].Add(instance);
                else treeBuckets[rng.Next(TREE_VARIANTS)].Add(instance);
            }

            _acaciaTreeInstances = new ModelInstance[TREE_VARIANTS][];
            for (int m = 0; m < TREE_VARIANTS; m++) _acaciaTreeInstances[m] = treeBuckets[m].ToArray();
            _acaciaBushInstances = new ModelInstance[BUSH_VARIANTS][];
            for (int m = 0; m < BUSH_VARIANTS; m++) _acaciaBushInstances[m] = bushBuckets[m].ToArray();
        }


        /// <summary>
        /// (Re)builds the ring of stones around each fire (#282): a few boulders of a handful of variants,
        /// set into the ground at their own spot on the terrain, rolled once and kept.
        /// <para>
        /// <b>Everything is sized off <see cref="CampfireConfig.FlameSize"/></b> rather than in world units,
        /// so a hearth belongs to the fire standing in it — these flames are 14 units tall at the shipped
        /// config, and a hearth measured once by hand would be a kerb of pebbles the day somebody widened
        /// them. The stones are sunk by a fraction of their own height, which is what makes a stone read as
        /// SET into the earth rather than resting on it: a lathe's flat underside meeting a rolling terrain
        /// at exactly ground level shows daylight under one side of every stone on a slope.
        /// </para>
        /// </summary>
        private void BuildHearthStones()
        {
            DisposeHearthStones();

            CampfireConfig cf = _savannaConfig.Campfire;
            _hearthStoneColor = cf.StoneColor.ToVector3();
            _hearthStoneFirelight = cf.StoneFirelight;

            int stones = Math.Max(0, cf.StoneCount);
            if (stones == 0) return;

            float size = cf.FlameSize * cf.StoneSizeScale;
            float ring = cf.FlameSize * cf.StoneRingScale;

            //Three shapes rather than one, for the reason the acacias have four: the eye reads the repeat
            //before it reads the stone. A ring takes one of them, so two neighbouring hearths differ as
            //wholes as well - which is what a camera walking the island past several of them shows.
            const int VARIANTS = 3;
            _hearthStoneMeshes = new RockMesh[VARIANTS];
            for (int v = 0; v < VARIANTS; v++)
            {
                _hearthStoneMeshes[v] = new RockMesh(_graphicsDevice,
                    radius: size * (0.82f + 0.18f * v),
                    height: size * (0.78f - 0.14f * v),
                    irregularityPhase: 1.7f * v);
            }

            int fires = SavannaCampfireCount;
            Random rng = new(28204);
            _hearthStoneInstances = new ModelInstance[fires][];

            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 at = SavannaCampfirePosition(fire);
                ModelInstance[] ring_ = new ModelInstance[stones];

                for (int s = 0; s < stones; s++)
                {
                    //Evenly spaced and then jittered, both in angle and in how far out it sits: a ring of
                    //stones laid by hand is regular in intent and irregular in fact.
                    float angle = (s + (float)rng.NextDouble() * 0.4f - 0.2f) * MathHelper.TwoPi / stones;
                    float radius = ring * (0.88f + 0.24f * (float)rng.NextDouble());

                    float x = at.X + MathF.Cos(angle) * radius;
                    float z = at.Z + MathF.Sin(angle) * radius;

                    float scale = 0.72f + 0.55f * (float)rng.NextDouble();
                    float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float tiltDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float tilt = 0.10f + 0.16f * (float)rng.NextDouble();

                    //Sunk by a fifth of its own height. The scale rides in the same matrix, so the sink has
                    //to be scaled with it or the small stones bury and the big ones float.
                    float y = SavannaTerrainHeight(x, z) - size * scale * 0.2f;

                    Matrix world = Matrix.CreateScale(scale)
                        * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(tiltDir), 0f, MathF.Sin(tiltDir)), tilt)
                        * Matrix.CreateRotationY(yaw)
                        * Matrix.CreateTranslation(x, y, z);

                    ring_[s] = new ModelInstance(world, Vector4.Zero);
                }

                _hearthStoneInstances[fire] = ring_;
            }
        }

        /// <summary>Disposes the hearth stone meshes — called on a rebuild and on teardown, like the acacias'.</summary>
        private void DisposeHearthStones()
        {
            if (_hearthStoneMeshes != null) foreach (RockMesh mesh in _hearthStoneMeshes) mesh?.Dispose();
            _hearthStoneMeshes = null;
            _hearthStoneInstances = null;
        }

        /// <summary>
        /// Disposes the acacia meshes and the shared instance buffer — called on a rebuild (a terrain or config
        /// change re-plants the scatter) and on the renderer's own <see cref="Dispose"/>.
        /// </summary>
        private void DisposeAcacia()
        {
            if (_acaciaTreeMeshes != null) foreach (AcaciaMesh mesh in _acaciaTreeMeshes) mesh?.Dispose();
            if (_acaciaBushMeshes != null) foreach (FoliageMesh mesh in _acaciaBushMeshes) mesh?.Dispose();
            _acaciaInstanceBuffer?.Dispose();
            _acaciaInstanceBuffer = null;
            _acaciaTreeMeshes = null;
            _acaciaBushMeshes = null;
        }

        /// <summary>
        /// (Re)builds the tropical scatter: the palm variants and the waterline's rock variants, and each
        /// one's per-variant instance matrices. Palms are planted only on <b>dry</b> sand (a height test
        /// against the water level, which follows the wiggling waterline) and rocks only in the band
        /// straddling it, so a shore edit re-plants the whole scatter — the same contract
        /// <see cref="BuildAcaciaBuffers"/> holds. Clumped around cluster centres with a few solos, kept
        /// out of each other by <see cref="ScatterSpacing"/>'s rule; the palms share one occupancy list,
        /// the rocks keep their own (a boulder at a palm's foot is what a beach looks like — the forest's
        /// own split). Deterministic seed, so the same config always gives the same beach.
        /// </summary>
        private void BuildTropicalBuffers()
        {
            DisposeTropical();

            TropicalTerrainConfig terrain = _tropicalConfig.Terrain;
            PalmConfig palms = _tropicalConfig.Palms;
            TropicalRockConfig rocks = _tropicalConfig.Rocks;
            float waterY = _tropicalConfig.Water.LevelY;
            Random rng = new(244);

            //--- The palm variants: rolled proportions and structural seeds, so a grove is a mix rather
            //than one palm stamped out. The variety is in the mesh and never in a per-instance stretch
            //(the shader transforms normals by the world matrix — a squashed palm would shade as the
            //shape it was authored at).
            const int PALM_VARIANTS = 4;
            _palmMeshes = new PalmMesh[PALM_VARIANTS];
            _palmDryness = new float[PALM_VARIANTS];
            for (int m = 0; m < PALM_VARIANTS; m++)
            {
                float h = 0.82f + 0.36f * (float)rng.NextDouble();
                _palmMeshes[m] = new PalmMesh(_graphicsDevice,
                    trunkRadius: palms.TrunkRadius * (0.85f + 0.3f * (float)rng.NextDouble()),
                    height: palms.Height * h,
                    frondLength: palms.FrondLength * (0.8f + 0.4f * (float)rng.NextDouble()),
                    seed: 6100 + m);
                _palmDryness[m] = (float)rng.NextDouble();
            }

            //--- The rock variants: the stone (RockMesh, the forest's own boulder) and its moss cap —
            //a low lathe dome whose rim is buried in the stone's upper flank and whose own irregularity
            //phase runs against the stone's, so where the green meets the grey is a ragged line that
            //no two rocks share. The cap is a second mesh over the same instance, which is why its
            //offset is baked into its profile rather than into the instance matrix.
            const int ROCK_VARIANTS = 3;
            _tropicalRockMeshes = new RockMesh[ROCK_VARIANTS];
            _tropicalMossMeshes = new LatheMesh[ROCK_VARIANTS];
            for (int m = 0; m < ROCK_VARIANTS; m++)
            {
                float w = 0.75f + 0.5f * (float)rng.NextDouble();
                float hh = 0.7f + 0.6f * (float)rng.NextDouble();
                _tropicalRockMeshes[m] = new RockMesh(_graphicsDevice,
                    radius: rocks.Radius * w, height: rocks.Height * hh,
                    irregularityPhase: 0.31f * m);
                _tropicalMossMeshes[m] = BuildMossCap(rocks.Radius * w, rocks.Height * hh, 0.57f + 0.22f * m);
            }

            var palmBuckets = new List<ModelInstance>[PALM_VARIANTS];
            for (int m = 0; m < PALM_VARIANTS; m++) palmBuckets[m] = new List<ModelInstance>();
            var rockBuckets = new List<ModelInstance>[ROCK_VARIANTS];
            for (int m = 0; m < ROCK_VARIANTS; m++) rockBuckets[m] = new List<ModelInstance>();

            //Cluster centres the palms gather around, in the dry ring.
            float[] clusterX = new float[palms.Clusters];
            float[] clusterZ = new float[palms.Clusters];
            for (int c = 0; c < palms.Clusters; c++)
            {
                float ca = (float)rng.NextDouble() * MathHelper.TwoPi;
                float cr = palms.MinRadius + (float)rng.NextDouble() * (palms.MaxRadius - palms.MinRadius);
                clusterX[c] = MathF.Cos(ca) * cr;
                clusterZ[c] = MathF.Sin(ca) * cr;
            }

            List<ScatterSpacing.Footprint> standing = new(palms.Count);

            for (int i = 0; i < palms.Count; i++)
            {
                float rand = (float)rng.NextDouble();
                //A per-plant uniform scale around 1, so one variant mesh reads as several palms.
                float sizeScale = 0.75f + 0.5f * rand;
                float halfWidth = palms.FrondLength * sizeScale;

                float x = 0f, z = 0f;
                float bestClearance = float.NegativeInfinity;

                for (int attempt = 0; attempt < ScatterSpacing.TRIES; attempt++)
                {
                    float cx, cz;
                    if (rng.NextDouble() < 0.82) //most palms clump around a cluster centre
                    {
                        int c = rng.Next(palms.Clusters);
                        float off = (float)rng.NextDouble();
                        float d = off * off * palms.ClusterSpread; //denser towards the centre
                        float da = (float)rng.NextDouble() * MathHelper.TwoPi;
                        cx = clusterX[c] + MathF.Cos(da) * d;
                        cz = clusterZ[c] + MathF.Sin(da) * d;
                    }
                    else //the odd solitary palm, anywhere in the ring
                    {
                        float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                        float r = palms.MinRadius + (float)rng.NextDouble() * (palms.MaxRadius - palms.MinRadius);
                        cx = MathF.Cos(a) * r;
                        cz = MathF.Sin(a) * r;
                    }

                    //Keep clear of the island
                    float dist = MathF.Sqrt(cx * cx + cz * cz);
                    if (dist < palms.MinRadius && dist > 0.01f)
                    {
                        cx *= palms.MinRadius / dist;
                        cz *= palms.MinRadius / dist;
                    }

                    //Only on DRY sand: a palm planted where the surf reaches is standing in the sea. The
                    //margin keeps the crown's swaying tips clear of the waterline rather than only the
                    //trunk's root. A candidate that fails this is simply not a candidate.
                    if (TropicalTerrainHeight(cx, cz, _tropicalConfig) < waterY + 1.1f) continue;

                    float clearance = ScatterSpacing.Clearance(cx, cz, halfWidth, standing);

                    if (clearance > bestClearance)
                    {
                        bestClearance = clearance;
                        x = cx;
                        z = cz;
                    }

                    if (clearance >= 0f) break;
                }

                //Never dropped for want of room (the forest's rule) — but if every candidate was in the
                //sea this palm has no ground to stand on, and standing it in the surf is the worse bug.
                if (bestClearance == float.NegativeInfinity) continue;

                standing.Add(new ScatterSpacing.Footprint(x, z, halfWidth));

                //Sunk a fraction into the sand, the forest scatter's own figure: a palm planted at the
                //exact surface reads as standing on a pinhead from anywhere but head-on, and the flare
                //at the root is what wants burying.
                Vector3 basePos = new(x, TropicalTerrainHeight(x, z, _tropicalConfig) - 0.15f, z);

                //The palm's own frame: the trunk's bow is in the mesh, so the instance takes only a
                //small lean (a leaning palm reads as wind-shaped, a tilted one as felled), a free yaw
                //and the uniform size.
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float lean = 0.05f * (float)rng.NextDouble();
                float leanDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                Matrix world = Matrix.CreateScale(sizeScale)
                    * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(leanDir), 0f, MathF.Sin(leanDir)), lean)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(basePos);

                palmBuckets[rng.Next(PALM_VARIANTS)].Add(new ModelInstance(world, Vector4.Zero));
            }

            //The rocks: strung along the waterline by the height band alone, which follows the coast's
            //wiggle exactly — a rock half in the water is what the band is for. Their own occupancy
            //list, and a tumble a boulder washed by surf has earned (sunk a little, so the turn never
            //floats a face above the sand).
            List<ScatterSpacing.Footprint> rockStanding = new(rocks.Count);
            for (int i = 0; i < rocks.Count; i++)
            {
                float sizeScale = 0.7f + 0.6f * (float)rng.NextDouble();
                float halfWidth = rocks.Radius * sizeScale;

                float x = 0f, z = 0f;
                float bestClearance = float.NegativeInfinity;

                for (int attempt = 0; attempt < ScatterSpacing.TRIES; attempt++)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float r = rocks.MinRadius + (float)rng.NextDouble() * (rocks.MaxRadius - rocks.MinRadius);
                    float cx = MathF.Cos(a) * r;
                    float cz = MathF.Sin(a) * r;

                    float h = TropicalTerrainHeight(cx, cz, _tropicalConfig);
                    if (h < waterY - 0.5f || h > waterY + 2.6f) continue; //the waterline band, and only it

                    float clearance = ScatterSpacing.Clearance(cx, cz, halfWidth, rockStanding);

                    if (clearance > bestClearance)
                    {
                        bestClearance = clearance;
                        x = cx;
                        z = cz;
                    }

                    if (clearance >= 0f) break;
                }

                if (bestClearance == float.NegativeInfinity) continue;

                rockStanding.Add(new ScatterSpacing.Footprint(x, z, halfWidth));

                Vector3 basePos = new(x, TropicalTerrainHeight(x, z, _tropicalConfig) - 0.2f, z);

                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float tumble = 0.3f * (float)rng.NextDouble();
                float tumbleDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                Matrix world = Matrix.CreateScale(sizeScale)
                    * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(tumbleDir), 0f, MathF.Sin(tumbleDir)), tumble)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(basePos);

                rockBuckets[rng.Next(ROCK_VARIANTS)].Add(new ModelInstance(world, Vector4.Zero));
            }

            _palmInstances = new ModelInstance[PALM_VARIANTS][];
            for (int m = 0; m < PALM_VARIANTS; m++) _palmInstances[m] = palmBuckets[m].ToArray();
            _tropicalRockInstances = new ModelInstance[ROCK_VARIANTS][];
            for (int m = 0; m < ROCK_VARIANTS; m++) _tropicalRockInstances[m] = rockBuckets[m].ToArray();
        }

        /// <summary>
        /// The moss cap over a waterline rock: a low lathe dome, its rim buried in the stone's upper
        /// flank and its crown a shade over the stone's own, traced top → outside → underside as
        /// <see cref="LatheMesh"/> documents. The cap is drawn at 0.84 of the stone's radius with its
        /// rim well under the stone's surface at that radius (the stone is a dome — its flank falls
        /// away outwards, so a cap as wide as the stone itself would float over its rim), and the
        /// irregularity amplitude is the stone's own share of the radius, so the cap's silhouette
        /// breaks as hard as the boulder's does under it. Where the green emerges over the grey is the
        /// two wobbles disagreeing, which no two rocks share.
        /// </summary>
        private LatheMesh BuildMossCap(float radius, float height, float irregularityPhase)
        {
            float capRadius = radius * 0.84f;
            float crownY = height * 1.04f;
            float rimY = height * 0.45f;

            var profile = new List<LathePoint>
            {
                new(0f, crownY, crease: true),                                  //the moss's crown
                new(capRadius * 0.34f, crownY, wobble: 1f),
                new(capRadius * 0.66f, rimY + (crownY - rimY) * 0.55f, wobble: 1f),
                new(capRadius, rimY, crease: true, wobble: 1f),                //where the green meets the grey
                new(capRadius * 0.74f, rimY - 0.18f, wobble: 1f),              //tucked under, sunk into the stone
                new(0f, rimY - 0.18f)
            };

            return new LatheMesh(_graphicsDevice, profile, 16, irregularityAmplitude: capRadius * 0.30f,
                irregularityPhase: irregularityPhase);
        }

        /// <summary>
        /// Disposes the palm and rock meshes and the shared instance buffer — called on a rebuild (a
        /// shore or config edit re-plants the scatter) and on the renderer's own <see cref="Dispose"/>.
        /// </summary>
        private void DisposeTropical()
        {
            if (_palmMeshes != null) foreach (PalmMesh mesh in _palmMeshes) mesh?.Dispose();
            if (_tropicalRockMeshes != null) foreach (RockMesh mesh in _tropicalRockMeshes) mesh?.Dispose();
            if (_tropicalMossMeshes != null) foreach (LatheMesh mesh in _tropicalMossMeshes) mesh?.Dispose();
            _palmInstanceBuffer?.Dispose();
            _palmInstanceBuffer = null;
            _palmMeshes = null;
            _tropicalRockMeshes = null;
            _tropicalMossMeshes = null;
        }

        #region Volcano: the rivers, the vents, the eruption and its lights

        /// <summary>
        /// (Re)solves everything about the volcano that depends on its terrain — the rivers' bearings and
        /// reaches, the vents the fountains are thrown from — and rebuilds the two particle buffers at the
        /// config's counts. Deterministic: same config, same volcano, every run and in every executable.
        /// </summary>
        private void BuildVolcanoBuffers()
        {
            VolcanoSceneConfig volcano = _volcanoConfig;
            Random rng = new(4177);

            //--- The rivers. Radial from the cone's axis, and the FIRST one is aimed to pass the arena: that
            //is the whole point of the scene's lighting, since a flow nobody stands beside lights nothing.
            //RiverArenaOffset walks it past the island's near edge rather than straight over it.
            _riverCount = Math.Clamp(volcano.RiverCount, 1, MAX_RIVERS);

            Vector2 cone = volcano.ConeCenter.ToVector2();
            float bearingToArena = MathF.Atan2(-cone.Y, -cone.X);
            float coneToArena = cone.Length();
            float spacing = MathHelper.TwoPi / _riverCount;
            float gullyCount = MathF.Round(MathF.Max(volcano.GullyCount, 1f));

            for (int i = 0; i < _riverCount; i++)
            {
                //Evenly spread and then jittered by up to a quarter of the spacing, so the flank is not a
                //starburst — and never enough to let one river swap sides with its neighbour.
                float jitter = (float)(rng.NextDouble() - 0.5) * spacing * 0.5f;
                float wanted = bearingToArena + volcano.RiverArenaOffset + i * spacing + (i == 0 ? 0f : jitter);

                //And then SNAPPED to the nearest gully, which is the whole difference between lava lying on
                //a cone and lava running down one: water — and rock — go where the ground drains, and the
                //gullies are where this ground drains. Without it the flows crossed the channels obliquely
                //and read as paint.
                _riverBearing[i] = SnapToGully(wanted, gullyCount);

                //River 0 has to get past the arena to be worth aiming there; the others stop somewhere on the
                //flank, each at its own reach, so the fronts are not one ring around the cone.
                _riverReach[i] = i == 0
                    ? coneToArena + 90f
                    : volcano.ConeRadius * (0.70f + 0.55f * (float)rng.NextDouble());
            }

            //The snap can walk river 0 by up to half a gully, and half a gully at this distance is tens of
            //units — enough to put the flow under the island instead of past it. If it lands too close, take
            //the next gully out on the far side. The clearance wanted is the island plus a couple of river
            //widths, so the flow passes beside the play field with dark ground between.
            float clearance = ArenaIsland.RADIUS + volcano.RiverWidth * 2f;
            float perpendicular = MathF.Abs(MathF.Sin(_riverBearing[0] - bearingToArena)) * coneToArena;
            if (perpendicular < clearance)
            {
                float away = MathF.Sign(volcano.RiverArenaOffset == 0f ? 1f : volcano.RiverArenaOffset);
                _riverBearing[0] = SnapToGully(bearingToArena + away * MathHelper.TwoPi * 1.5f / gullyCount, gullyCount);
            }

            _volcanoEffect.Parameters["RiverBearing"].SetValue(_riverBearing);
            _volcanoEffect.Parameters["RiverReach"].SetValue(_riverReach);
            _volcanoEffect.Parameters["RiverCount"].SetValue(_riverCount);

            //--- The vents. Three, and fixed in code rather than being another dial: the crater, and two side
            //vents part-way down the flank on two of the rivers — which is where a side vent is, since the
            //fissure that opens is what feeds the flow. Their strengths taper so the crater is plainly the
            //main event and the spatter cones read as spatter.
            _ventCount = Math.Min(3, MAX_VENTS);

            _ventPosition[0] = new Vector3(cone.X, VolcanoGroundY(cone.X, cone.Y) + 2f, cone.Y);
            _ventStrength[0] = 1f;

            for (int v = 1; v < _ventCount; v++)
            {
                float bearing = _riverBearing[v % _riverCount];
                float radius = volcano.ConeRadius * (0.34f + 0.16f * v);
                float x = cone.X + MathF.Cos(bearing) * radius;
                float z = cone.Y + MathF.Sin(bearing) * radius;

                _ventPosition[v] = new Vector3(x, VolcanoGroundY(x, z) + 1.5f, z);
                _ventStrength[v] = 0.42f - 0.10f * (v - 1);
            }

            _fountainEffect.Parameters["VentPosition"].SetValue(_ventPosition);
            _fountainEffect.Parameters["VentStrength"].SetValue(_ventStrength);
            _fountainEffect.Parameters["VentCount"].SetValue(_ventCount);

            //--- The particles. One buffer for the fountains: its first slice is the plume and the rest are
            //the jets, drawn as two index ranges over the one buffer (see DrawLavaFountains) so neither pass
            //pays for the other's particles. Both counts are capped where a 16-bit index buffer runs out.
            int total = Math.Clamp(volcano.Fountains.ParticleCount, 0, MAX_BILLBOARD_PARTICLES);
            _plumeQuads = (int)(total * Math.Clamp(volcano.Fountains.PlumeFraction, 0f, 0.9f));
            _jetQuads = total - _plumeQuads;

            BuildBillboardParticles(total, 8831, ref _fountainVertexBuffer, ref _fountainIndexBuffer);
            BuildBillboardParticles(Math.Clamp(volcano.Ash.FlakeCount, 0, MAX_BILLBOARD_PARTICLES), 6491,
                ref _ashVertexBuffer, ref _ashIndexBuffer);
        }

        /// <summary>
        /// The bearing of the gully floor nearest <paramref name="bearing"/>, so a river can be laid in one.
        /// <para>
        /// A gully is deepest where <c>Volcano.fx</c>'s rake term peaks, i.e. where
        /// <c>b·N + 2·sin(3b) ≡ π (mod 2π)</c>. There is no closed form for that, and none is needed: the
        /// <c>2·sin(3b)</c> bend is small against <c>N</c>, so picking the branch nearest the wanted bearing
        /// and iterating <c>b ← (target − 2·sin(3b)) / N</c> is a contraction with ratio <c>6/N</c> and four
        /// passes land far inside a degree. Change the rake term in the shader and this has to change with it.
        /// </para>
        /// </summary>
        private static float SnapToGully(float bearing, float gullyCount)
        {
            float branch = MathF.Round((bearing * gullyCount + 2f * MathF.Sin(bearing * 3f) - MathF.PI) / MathHelper.TwoPi);
            float target = MathF.PI + branch * MathHelper.TwoPi;

            float b = bearing;
            for (int pass = 0; pass < 4; pass++) b = (target - 2f * MathF.Sin(b * 3f)) / gullyCount;

            return b;
        }

        /// <summary>
        /// A static buffer of <paramref name="count"/> camera-facing quads, each carrying a fixed random point
        /// in the unit cube and one more random — everything a shader needs to animate a particle entirely in
        /// its vertex shader. The volcano's fountains, its plume and its ash are all built from this.
        /// </summary>
        private void BuildBillboardParticles(int count, int seed, ref VertexBuffer vertexBuffer, ref IndexBuffer indexBuffer)
        {
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
            vertexBuffer = null;
            indexBuffer = null;

            if (count <= 0) return;

            BillboardVertex[] vertices = new BillboardVertex[count * 4];
            Random rng = new(seed);
            for (int i = 0; i < count; i++)
            {
                Vector3 basePosition = new((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                float rand = (float)rng.NextDouble();
                int v = i * 4;
                vertices[v] = new BillboardVertex(basePosition, new Vector3(-1f, 1f, rand));
                vertices[v + 1] = new BillboardVertex(basePosition, new Vector3(1f, 1f, rand));
                vertices[v + 2] = new BillboardVertex(basePosition, new Vector3(-1f, -1f, rand));
                vertices[v + 3] = new BillboardVertex(basePosition, new Vector3(1f, -1f, rand));
            }
            vertexBuffer = new VertexBuffer(_graphicsDevice, BillboardVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            short[] indices = new short[count * 6];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4;
                int o = i * 6;
                indices[o] = (short)v; indices[o + 1] = (short)(v + 2); indices[o + 2] = (short)(v + 1);
                indices[o + 3] = (short)(v + 1); indices[o + 4] = (short)(v + 2); indices[o + 5] = (short)(v + 3);
            }
            indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
        }

        /// <summary>
        /// The volcano's ground height at a world point: <c>Volcano.fx</c>'s <c>TerrainHeight</c> without its
        /// scoria fBm term, which is the one thing this mirror leaves out and can afford to — three units of
        /// clinker under a lamp or a vent is invisible, and reproducing four octaves of gradient noise on the
        /// CPU to place them would be the tail wagging the dog. Everything that decides where the cone, the
        /// crater and the gullies are is here term for term.
        /// </summary>
        private float VolcanoGroundY(float x, float z)
        {
            VolcanoSceneConfig volcano = _volcanoConfig;

            float ramp = SmoothStep(volcano.ClearingRadius, volcano.ClearingRadius + MathF.Max(volcano.ClearingTransition, 1f),
                MathF.Sqrt(x * x + z * z));

            Vector2 cone = volcano.ConeCenter.ToVector2();
            float dx = x - cone.X;
            float dz = z - cone.Y;
            float r = MathF.Sqrt(dx * dx + dz * dz);
            float bearing = MathF.Atan2(dz, dx);

            //Clamped to CraterRadius, not r itself - Volcano.fx's VolcanoMassing has the why: evaluated at r
            //the flank is maximal exactly at the vent for any profile, so the crater term below could only
            //ever steepen the approach to a point, never move the true summit off it. The clamp is what
            //plateaus the flank at the rim's own height, which is the surface the bowl is cut into.
            float craterRadius = MathF.Max(volcano.CraterRadius, 1f);
            float flankRadius = MathF.Max(r, craterRadius);
            float t = Math.Clamp(1f - flankRadius / MathF.Max(volcano.ConeRadius, 1f), 0f, 1f);
            float flank = volcano.ConeHeight * MathF.Pow(t, MathF.Max(volcano.ConeProfile, 0.1f));

            float crater = volcano.CraterDepth * SmoothStep(craterRadius, 0f, r);

            float gullyCount = MathF.Round(MathF.Max(volcano.GullyCount, 1f));
            float rake = 0.5f - 0.5f * MathF.Cos(bearing * gullyCount + 2f * MathF.Sin(bearing * 3f));
            float gullyBand = SmoothStep(craterRadius * 1.15f, volcano.ConeRadius * 0.45f, r)
                * SmoothStep(volcano.ConeRadius * 1.05f, volcano.ConeRadius * 0.62f, r);

            return volcano.LevelY + ramp * (flank - crater - volcano.GullyDepth * rake * gullyBand);
        }

        /// <summary>
        /// How hard the volcano is erupting at a wall-clock time, 0 between bursts and up to 1 at the peak of
        /// one. A pure function of the clock with no state, so the game, the Testbed and the map editor all
        /// see the same eruption at the same second, and so a burst can drive the jets, the plume and the
        /// crater's light off ONE figure rather than three that drift apart.
        /// <para>
        /// The schedule is irregular by construction: each period contains one burst, but where in the period
        /// it starts and how big it is are hashed off the period's own index, so the eruption never becomes a
        /// metronome — the failure a fixed interval always ends in, and the one #219's lightning has to solve
        /// too. The envelope is a fast attack and a long decay, which is the shape of the thing: a volcano
        /// goes off and then subsides. <b>Light first, sound a beat behind</b> — the sound is not built yet
        /// (it wants to land with #219's thunder rather than be invented twice), and this is where it hangs.
        /// </para>
        /// </summary>
        public float VolcanoEruption(float time)
        {
            EruptionConfig eruption = _volcanoConfig.Eruption;

            float period = MathF.Max(eruption.Period, 1f);
            float u = time / period;
            float index = MathF.Floor(u);

            float start = 0.10f + 0.55f * Hash01(index);
            float length = Math.Clamp(eruption.Length / period, 0.02f, 0.85f);

            float p = (u - index - start) / length;
            if (p <= 0f || p >= 1f) return 0f;

            float envelope = p < 0.14f ? p / 0.14f : MathF.Pow(1f - (p - 0.14f) / 0.86f, 1.7f);

            //Not every burst is the same size: a scene whose every event is identical stops being an event.
            return envelope * (0.55f + 0.45f * Hash01(index + 101f));
        }

        //A deterministic hash of a small integer, for the eruption schedule. A sine hash is fine here where it
        //would not be in a shader: it runs on one CPU with one rounding, and its argument stays small.
        private static float Hash01(float n)
        {
            float s = MathF.Sin(n * 12.9898f) * 43758.5453f;
            return s - MathF.Floor(s);
        }

        /// <summary>
        /// How many point lights the volcano pushes, capped to the scene-light budget the shaders' arrays are
        /// sized for. Slot 0 is the crater; every other slot rides a river.
        /// </summary>
        public int VolcanoLightCount => Math.Clamp(_volcanoConfig.LightCount, 1, SceneLights.MaxLights);

        /// <summary>The volcano's point-light range (quadratic falloff), shared by the crater and the flows.</summary>
        public float VolcanoLightRange => _volcanoConfig.LightRange;

        /// <summary>
        /// Where light <paramref name="index"/> stands at a wall-clock time. Slot 0 is the crater and does not
        /// move; every other slot is a <b>flow front travelling downhill</b>, which is the whole reason this
        /// takes a time at all — eight fixed lamps under a scene whose lava visibly moves would read as a lit
        /// set rather than as a burning one.
        /// <para>
        /// Slots 1 and 2 both ride river 0 — the one aimed past the arena — a third of a span apart, so there
        /// is nearly always a front near the play field; the rest take the other rivers in turn. The bearing
        /// mirrors <c>Volcano.fx</c>'s own wander term, so a light sits <i>on</i> its river rather than beside
        /// it: change one and change the other.
        /// </para>
        /// </summary>
        public Vector3 VolcanoLightPosition(int index, float time)
        {
            if (index <= 0) return _ventPosition[0];

            VolcanoSceneConfig volcano = _volcanoConfig;
            Vector2 cone = volcano.ConeCenter.ToVector2();

            int river = index <= 2 ? 0 : (index - 2) % _riverCount;
            float near = MathF.Max(volcano.CraterRadius, 1f) * 1.2f;
            float span = MathF.Max(_riverReach[river] - near, 1f);

            float phase = Frac(time * volcano.RiverSpeed / span + index * 0.37f);
            float r = near + phase * span;

            float wander = volcano.RiverWander * MathF.Sin(r * 0.017f + river * 2.13f)
                * Math.Clamp(r / MathF.Max(volcano.ConeRadius, 1f), 0f, 1f);
            float bearing = _riverBearing[river] + wander;

            float x = cone.X + MathF.Cos(bearing) * r;
            float z = cone.Y + MathF.Sin(bearing) * r;

            //A little over the surface: a lamp buried in the ground it is lighting throws nothing sideways,
            //and the flow it stands for is a metre of molten rock lying on top of the flank, not inside it.
            return new Vector3(x, VolcanoGroundY(x, z) + 2.5f, z);
        }

        /// <summary>
        /// The colour of light <paramref name="index"/> at a wall-clock time. The crater takes the eruption's
        /// envelope on top of its base glow; a flow front swells and dies over its run down the flank, so it
        /// arrives, passes and is gone rather than blinking out when it wraps.
        /// <para>
        /// <b>Everything here is scaled by <see cref="VolcanoSceneConfig.LightStrength"/>, and that is the
        /// readability dial, not a brightness taste.</b> The cluster hangs over this scene with red and orange
        /// balls in it; the dome keeps lighting their tops, and what the ground is allowed to add underneath
        /// has to stay under-light rather than becoming a tint. Turn it up and a red ball stops being one.
        /// </para>
        /// </summary>
        public Vector3 VolcanoLightColor(float time, int index)
        {
            VolcanoSceneConfig volcano = _volcanoConfig;

            //Lava pulses where a fire flickers — slower rates than the campfire's, and each lamp on its own
            //stride so the flank does not breathe in unison.
            float t = time + index * 3.77f;
            float rate = 1f + index * 0.037f;
            float pulse = 0.82f + 0.18f * (0.5f * MathF.Sin(t * 3.1f * rate) + 0.3f * MathF.Sin(t * 5.3f * rate + 1.3f)
                + 0.2f * MathF.Sin(t * 2.1f * rate));

            float strength;
            if (index <= 0)
            {
                strength = 0.75f + VolcanoEruption(time) * volcano.Eruption.LightBoost;
            }
            else
            {
                int river = index <= 2 ? 0 : (index - 2) % _riverCount;
                float near = MathF.Max(volcano.CraterRadius, 1f) * 1.2f;
                float span = MathF.Max(_riverReach[river] - near, 1f);
                float phase = Frac(time * volcano.RiverSpeed / span + index * 0.37f);

                //Swells in and dies out over the run, so a front never appears or vanishes on the spot
                strength = MathF.Sin(MathF.PI * phase);
            }

            return volcano.LavaHot.ToVector3() * (volcano.LightStrength * strength * pulse);
        }

        private static float Frac(float value) => value - MathF.Floor(value);

        #endregion

        /// <summary>
        /// (Re)seeds the shared bird flock: each bird's orbit and drift, and the cycle of wingbeat bursts and
        /// glides it flies. The savanna, desert, outback and tropical scenes share it, so it is sized to the
        /// largest of the four configs' counts — otherwise a desert level's flock would be silently capped to
        /// the savanna's count (and so on, since no scene rebuilds on a NumPad2 switch). <see cref="DrawBirds"/>
        /// caps its draw to the active scene's count; colour, size and centre are read per frame from that
        /// scene's Birds config. Deterministic seed, so the flock is the same every run.
        /// <para>
        /// There is no geometry here any more: <see cref="BirdMesh"/> is one rest pose shared by every bird
        /// and does not depend on the count, so it is built once alongside the effect.
        /// </para>
        /// </summary>
        private void SeedBirdFlock()
        {
            int birdCount = Math.Max(Math.Max(_savannaConfig.Birds.Count, _desertConfig.Birds.Count),
                Math.Max(_outbackConfig.Birds.Count, _tropicalConfig.Birds.Count));

            _birdRadius = new float[birdCount];
            _birdAltitude = new float[birdCount];
            _birdOrbitSpeed = new float[birdCount];
            _birdOrbitPhase = new float[birdCount];
            _birdBobSpeed = new float[birdCount];
            _birdFlapPeriod = new float[birdCount];
            _birdFlapCyclePhase = new float[birdCount];
            _birdFlapBeats = new float[birdCount];
            _birdBurstFraction = new float[birdCount];

            //Deterministic, so the flock is the same every run. All circle the same way, like a kettle of
            //vultures riding one thermal, each at its own radius, height and unhurried pace.
            Random birdRng = new(4242);
            for (int i = 0; i < birdCount; i++)
            {
                _birdRadius[i] = BIRD_RADIUS_MIN + (float)birdRng.NextDouble() * BIRD_RADIUS_SPAN;
                _birdAltitude[i] = (float)(birdRng.NextDouble() * 2.0 - 1.0) * 10f;
                _birdOrbitSpeed[i] = 0.10f + (float)birdRng.NextDouble() * 0.12f;
                _birdOrbitPhase[i] = (float)birdRng.NextDouble() * MathHelper.TwoPi;
                _birdBobSpeed[i] = 0.4f + (float)birdRng.NextDouble() * 0.5f;

                //A soaring bird GLIDES most of the time and beats its wings in short bursts. The old flock
                //beat at a fixed rate for ever, which is the other half of what read as mechanical: a
                //metronome that never rests and never hurries. Each bird gets its own long cycle and a WHOLE
                //number of beats to spend in it — whole, so a burst begins and ends with the stroke at its
                //neutral point and the wings can be handed back to the glide without a step.
                //
                //⚠ The burst's LENGTH is DERIVED from the two things that actually read — how many beats it
                //is, and how fast this bird beats. Rolling it on its own instead let a two-beat burst spread
                //across a long window and come out at 0.8 Hz, which reads as slow motion rather than as a
                //bird; a soaring bird beats about two to three times a second.
                _birdFlapBeats[i] = 2 + birdRng.Next(4);
                _birdFlapPeriod[i] = BIRD_CYCLE_MIN + (float)birdRng.NextDouble() * BIRD_CYCLE_SPAN;
                _birdFlapCyclePhase[i] = (float)birdRng.NextDouble();

                float beatRate = BIRD_BEAT_HZ_MIN + (float)birdRng.NextDouble() * BIRD_BEAT_HZ_SPAN;
                _birdBurstFraction[i] = MathF.Min(_birdFlapBeats[i] / (beatRate * _birdFlapPeriod[i]), BIRD_BURST_MAX);
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
            _snowEffect.Parameters["SnowSpin"].SetValue(_mountainConfig.Snow.Spin);
            _snowEffect.Parameters["SnowLobing"].SetValue(_mountainConfig.Snow.Lobing);
            _snowEffect.Parameters["SnowNearFade"].SetValue(_mountainConfig.Snow.NearFade);
            _snowEffect.Parameters["SnowTwinkle"].SetValue(_mountainConfig.Snow.Twinkle);
            _snowEffect.Parameters["SnowColor"].SetValue(_mountainConfig.Snow.FlakeColor.ToVector3());
            _snowEffect.Parameters["SnowOpacity"].SetValue(_mountainConfig.Snow.Opacity);
        }

        /// <summary>(Re)builds the snowfall's flake buffer at the config's flake count. Deterministic seed.</summary>
        private void BuildSnowBuffers()
        {
            _snowVertexBuffer?.Dispose();
            _snowIndexBuffer?.Dispose();

            BillboardVertex[] snowVertices = new BillboardVertex[_mountainConfig.Snow.FlakeCount * 4];
            Random snowRng = new(1207);
            for (int i = 0; i < _mountainConfig.Snow.FlakeCount; i++)
            {
                Vector3 basePosition = new((float)snowRng.NextDouble(), (float)snowRng.NextDouble(), (float)snowRng.NextDouble());
                float rand = (float)snowRng.NextDouble();
                int v = i * 4;
                snowVertices[v] = new BillboardVertex(basePosition, new Vector3(-1f, 1f, rand));
                snowVertices[v + 1] = new BillboardVertex(basePosition, new Vector3(1f, 1f, rand));
                snowVertices[v + 2] = new BillboardVertex(basePosition, new Vector3(-1f, -1f, rand));
                snowVertices[v + 3] = new BillboardVertex(basePosition, new Vector3(1f, -1f, rand));
            }
            _snowVertexBuffer = new VertexBuffer(_graphicsDevice, BillboardVertex.Declaration, snowVertices.Length, BufferUsage.WriteOnly);
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

            BillboardVertex[] sprayVertices = new BillboardVertex[_seaConfig.Spray.ParticleCount * 4];
            Random sprayRng = new(5023);
            for (int i = 0; i < _seaConfig.Spray.ParticleCount; i++)
            {
                Vector3 basePosition = new((float)sprayRng.NextDouble(), (float)sprayRng.NextDouble(), (float)sprayRng.NextDouble());
                float rand = (float)sprayRng.NextDouble();
                int v = i * 4;
                sprayVertices[v] = new BillboardVertex(basePosition, new Vector3(-1f, 1f, rand));
                sprayVertices[v + 1] = new BillboardVertex(basePosition, new Vector3(1f, 1f, rand));
                sprayVertices[v + 2] = new BillboardVertex(basePosition, new Vector3(-1f, -1f, rand));
                sprayVertices[v + 3] = new BillboardVertex(basePosition, new Vector3(1f, -1f, rand));
            }
            _sprayVertexBuffer = new VertexBuffer(_graphicsDevice, BillboardVertex.Declaration, sprayVertices.Length, BufferUsage.WriteOnly);
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

        /// <summary>
        /// Points the forest effect at the full floor or the reduced one. By <b>technique</b> and not by a
        /// uniform the shader branches on: what the reduced floor gives up is occupancy, and a runtime branch
        /// skips the work while keeping the registers that cost it — measured, a uniform branch saved 0.02 ms
        /// of the 0.60 the separate program saves. See <see cref="SceneDetail"/>.
        /// </summary>
        private void SelectDetailTechniques()
        {
            SelectForestTechnique();

            //The cavern is deliberately NOT here any more (#250). Its pair — the water's second wall shade and
            //the spore count — was cut from the scene itself rather than from a second program, so there is one
            //technique left and every tier draws it. Its effect is loaded in the constructor like the two below,
            //so it was never a null-check that kept it here.

            //The dream's four: the background's second evaluation in the reflection, most of the sparks, most
            //of each spark's trail, and an octave off both warp layers — the last being the only reduction in
            //any of these three scenes that pays on its own, since it is the only one on every pixel.
            _dreamEffect.CurrentTechnique = _dreamEffect.Techniques[_sceneDetail > 0.5f ? "Dream" : "DreamReduced"];
        }

        private void SelectForestTechnique() =>
            _forestEffect.CurrentTechnique = _forestEffect.Techniques[_sceneDetail > 0.5f ? "Forest" : "ForestReduced"];

        private void ApplyForestParameters()
        {
            SelectForestTechnique();

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

            //The volume the island is inside — the one layer of this scene with depth rather than only a
            //direction, and so the only one the camera can move through (see Space.fx's StarNestVolume)
            SpaceVolumeConfig volume = space.Volume;
            _spaceEffect.Parameters["VolumeStrength"].SetValue(volume.Strength);
            _spaceEffect.Parameters["VolumeScale"].SetValue(volume.Scale);
            _spaceEffect.Parameters["VolumeDrift"].SetValue(volume.Drift);
            _spaceEffect.Parameters["VolumeSaturation"].SetValue(volume.Saturation);
            _spaceEffect.Parameters["VolumeOpacity"].SetValue(volume.Opacity);
            _spaceEffect.Parameters["VolumeTint"].SetValue(volume.Tint.ToVector3());

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

        private void ApplyMoonParameters()
        {
            MoonSceneConfig moon = _moonConfig;

            _moonEffect.Parameters["VoidColor"].SetValue(moon.VoidColor.ToVector3());

            MoonTerrainConfig terrain = moon.Terrain;
            _moonEffect.Parameters["MoonLevelY"].SetValue(terrain.LevelY);
            _moonEffect.Parameters["ClearingRadius"].SetValue(terrain.ClearingRadius);
            _moonEffect.Parameters["ClearingTransition"].SetValue(terrain.ClearingTransition);
            _moonEffect.Parameters["CraterAmplitude"].SetValue(terrain.CraterAmplitude);
            _moonEffect.Parameters["HighlandHeight"].SetValue(terrain.HighlandHeight);
            _moonEffect.Parameters["HighlandInnerRadius"].SetValue(terrain.HighlandInnerRadius);
            _moonEffect.Parameters["HighlandCrestRadius"].SetValue(terrain.HighlandCrestRadius);
            _moonEffect.Parameters["HighlandSaddleFloor"].SetValue(terrain.HighlandSaddleFloor);
            _moonEffect.Parameters["Curvature"].SetValue(terrain.Curvature);
            _moonEffect.Parameters["RegolithColor"].SetValue(terrain.RegolithColor.ToVector3());
            _moonEffect.Parameters["RegolithColorPale"].SetValue(terrain.RegolithColorPale.ToVector3());
            _moonEffect.Parameters["EjectaBrightness"].SetValue(terrain.EjectaBrightness);
            _moonEffect.Parameters["MicroReliefStrength"].SetValue(terrain.MicroReliefStrength);
            _moonEffect.Parameters["GrainStrength"].SetValue(terrain.GrainStrength);

            //The terrain's sun and fill are the config's own, never the frame's dome-derived ones: this
            //scene draws no dome, and a dome-derived sun on a domeless ground would be the lie
            //TryGetLightRig's doc warns about, painted onto the terrain instead of the island.
            _moonEffect.Parameters["SunColor"].SetValue(terrain.SunColor.ToVector3());
            _moonEffect.Parameters["AmbientColor"].SetValue(terrain.AmbientColor.ToVector3());

            //The earthshine the terrain shader adds as a directional fill is derived from the same figures
            //the scene-light slot uses (TryGetMoonEarthshine), so the ground and the island cannot disagree
            //about how bright the Earth is.
            MoonLightingConfig lighting = moon.Lighting;
            MoonEarthConfig earth = moon.Earth;

            Vector3 earthAlbedo = earth.CloudColor.ToVector3() * 0.6f + earth.OceanColor.ToVector3() * 0.4f;
            float earthPeak = MathF.Max(MathF.Max(earthAlbedo.X, earthAlbedo.Y), MathF.Max(earthAlbedo.Z, 1e-4f));
            bool shines = lighting.EarthshineStrength > 0f && earth.AngularRadiusDegrees > 0f;

            _moonEffect.Parameters["EarthshineColor"].SetValue(
                shines ? earthAlbedo / earthPeak * lighting.EarthshineStrength : Vector3.Zero);

            _moonEffect.Parameters["EarthDirection"].SetValue(SafeNormal(earth.Direction.ToVector3(), Vector3.Forward));
            _moonEffect.Parameters["EarthAngularRadius"].SetValue(MathHelper.ToRadians(earth.AngularRadiusDegrees));
            _moonEffect.Parameters["EarthAxis"].SetValue(SafeNormal(earth.Axis.ToVector3(), Vector3.Up));
            _moonEffect.Parameters["OceanColor"].SetValue(earth.OceanColor.ToVector3());
            _moonEffect.Parameters["LandColor"].SetValue(earth.LandColor.ToVector3());
            _moonEffect.Parameters["LandColorArid"].SetValue(earth.LandColorArid.ToVector3());
            _moonEffect.Parameters["CloudColor"].SetValue(earth.CloudColor.ToVector3());
            _moonEffect.Parameters["CloudAmount"].SetValue(earth.CloudAmount);
            _moonEffect.Parameters["RimColor"].SetValue(earth.RimColor.ToVector3());
            _moonEffect.Parameters["RimStrength"].SetValue(earth.RimStrength);
            _moonEffect.Parameters["NightAmbient"].SetValue(earth.NightAmbient);

            SpaceStarsConfig stars = moon.Stars;
            _moonEffect.Parameters["StarCellScale"].SetValue(new[] { stars.BrightCellScale, stars.MediumCellScale, stars.FaintCellScale });
            _moonEffect.Parameters["StarChance"].SetValue(new[] { stars.BrightChance, stars.MediumChance, stars.FaintChance });
            _moonEffect.Parameters["StarPeak"].SetValue(new[] { stars.BrightPeak, stars.MediumPeak, stars.FaintPeak });
            _moonEffect.Parameters["StarSpread"].SetValue(stars.Spread);
            _moonEffect.Parameters["StarFalloff"].SetValue(stars.Falloff);
            _moonEffect.Parameters["StarSpikeThreshold"].SetValue(stars.SpikeThreshold);
            _moonEffect.Parameters["StarSpikeLength"].SetValue(stars.SpikeLength);
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
            _cavernEffect.Parameters["WaveAmplitude"].SetValue(water.WaveAmplitude);
            _cavernEffect.Parameters["CausticStrength"].SetValue(water.CausticStrength);
            _cavernEffect.Parameters["MistColor"].SetValue(water.MistColor.ToVector3());
            _cavernEffect.Parameters["MistDensity"].SetValue(water.MistDensity);
            _cavernEffect.Parameters["MistHeight"].SetValue(water.MistHeight);

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
        /// the Sahara dunes (with the same birds), the snowy range, the meadow, the forest floor, deep space,
        /// the dream, the cavern or the Moon. A no-op for <see cref="SceneKind.City"/>/<see cref="SceneKind.NeonCity"/>,
        /// which the caller draws itself. Opaque, so it stands in for the city as the thing the arena glass
        /// shows beneath it; it leaves the alpha-blend / back-face-cull state the rest of the opaque scene wants.
        /// <para>
        /// The four sky-replacing draws also touch the <b>depth</b> state: space, the dream and the cavern
        /// are backgrounds rather than geometry, so they draw with <see cref="DepthStencilState.None"/>,
        /// while the Moon writes depth with its terrain and then reads it under its sky quad
        /// (<see cref="DepthStencilState.DepthRead"/>) — and every one of them restores
        /// <see cref="DepthStencilState.Default"/> on the way out.
        /// </para>
        /// </summary>
        public void DrawEnvironment(SceneKind scene, in SceneFrame frame, RenderTarget2D sceneTarget = null)
        {
            //The two full-screen analytic backdrops that are worth more than the frame can afford get shaded
            //at the back buffer's own size and scaled up; every other scene draws straight into whatever the
            //caller bound. See DrawBackdropAtDisplayResolution for what that trades and why it is these two.
            if (sceneTarget != null && SupersampleFactor > 1 && (scene == SceneKind.Cavern || scene == SceneKind.Dream))
            {
                DrawBackdropAtDisplayResolution(scene, frame, sceneTarget);
                return;
            }

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
                case SceneKind.Outback:
                    DrawOutback(frame);
                    DrawBirds(frame, _outbackConfig.Birds);
                    break;
                case SceneKind.Tropical:
                    //The land first (it writes depth), then the lagoon depth-read over the bed it owns,
                    //then the scatter that stands on the sand, then the flock over the water.
                    DrawTropicalTerrain(frame);
                    DrawTropicalWater(frame);
                    DrawPalms(frame);
                    DrawTropicalRocks(frame);
                    DrawBirds(frame, _tropicalConfig.Birds);
                    break;
                case SceneKind.Volcano:
                    //The flank first (it writes depth), then the fountains and the plume over it — they are
                    //part of the far scene rather than foreground weather, because the cluster hangs in front
                    //of the cone and has to occlude it. Only the ash is an overlay.
                    DrawVolcanoTerrain(frame);
                    DrawLavaFountains(frame);
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
                case SceneKind.Moon:
                    DrawMoon(frame);
                    break;
                case SceneKind.Mars:
                    DrawMarsTerrain(frame);
                    DrawMarsMoons(frame);
                    break;
                case SceneKind.Storm:
                    DrawStorm(frame);
                    break;
            }
        }

        /// <summary>
        /// Draws the foreground weather that belongs after the opaque scene and the cluster: falling snow in
        /// the mountain scene, blown spray and spindrift in the sea scene, drifting ash in the volcano.
        /// Alpha-blended and depth-read (the terrain/water and the cluster occlude the particles behind them)
        /// but writing no depth. A no-op for every other scene.
        /// <para>
        /// The volcano's <i>fountains</i> are deliberately not here: they stand on the far cone, so the
        /// cluster has to occlude them and they belong with the environment (see
        /// <see cref="DrawEnvironment"/>). Only the ash is genuinely in front of everything.
        /// </para>
        /// </summary>
        public void DrawOverlays(SceneKind scene, in SceneFrame frame)
        {
            if (scene == SceneKind.Mountain) DrawSnow(frame);
            else if (scene == SceneKind.Sea) DrawSpray(frame);
            else if (scene == SceneKind.Savanna) DrawFlame(frame);
            else if (scene == SceneKind.Volcano) DrawAsh(frame);
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

            //The pool standing in the drain (#132): the cut around the island keeps a calm disc of water
            //where the funnel's glass cone crosses the mean level, and discards only the annulus hidden
            //inside the island's stone. The radius is the cone's own at LevelY — the same straight span
            //FunnelMesh is built from, so the water and the glass cannot drift — buried POOL_WALL_BIAS into
            //the glass so no sliver of the wall shows under the water's edge (the buried-edge lesson of
            //#109). Clamping the span keeps a config that floods the rim or sits below the hole sane. With
            //TerrainHoleRadius 0 (the map editor) the shader cuts nothing and ignores this figure entirely.
            float drainRimY = ArenaIsland.TOP_Y - ArenaIsland.DISH_DEPTH;
            float poolT = Math.Clamp((drainRimY - _seaConfig.LevelY) / (drainRimY - ArenaIsland.FUNNEL_BOTTOM_Y), 0f, 1f);
            float poolRadius = MathHelper.Lerp(ArenaIsland.FUNNEL_TOP_RADIUS, ArenaIsland.FUNNEL_HOLE_RADIUS, poolT)
                + POOL_WALL_BIAS;

            _seaEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _seaEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _seaEffect.Parameters["FunnelPoolRadius"].SetValue(poolRadius);
            _seaEffect.Parameters["View"].SetValue(frame.Camera.View);
            _seaEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _seaEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _seaEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _seaEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _seaEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _seaEffect.Parameters["SeaTime"].SetValue(frame.Time);
            _seaEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            //The config-static water values are re-pushed EVERY FRAME as well, not left to
            //ApplySeaParameters: the tropical lagoon shares this one effect and pushes its own whole
            //set from DrawTropicalWater, and a NumPad2/V switch applies no config — so without this
            //the open sea would draw the lagoon's water from the moment the two scenes were switched
            //through. A handful of SetValues once a frame; the alternative is a bug nobody reports
            //because it still looks like water.
            _seaEffect.Parameters["SeaLevelY"].SetValue(_seaConfig.LevelY);
            _seaEffect.Parameters["WaterColorDeep"].SetValue(_seaConfig.WaterDeep.ToVector3());
            _seaEffect.Parameters["WaterColorShallow"].SetValue(_seaConfig.WaterShallow.ToVector3());
            _seaEffect.Parameters["ShallowBias"].SetValue(_seaConfig.ShallowBias);
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

            frame.ApplyClouds?.Invoke(_seaEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            //Depth-read, not depth-write: a missed ball falls through the surface and the sea has to stop
            //claiming the depth under the waterline so the ball's own pixels run (and fade) instead of being
            //depth-killed by the surface plane. The island draws after this and is opaque, so it still writes
            //and owns its own depth; only the open water gives the depth up (#131).
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _graphicsDevice.SetVertexBuffer(_seaVertexBuffer);
            _graphicsDevice.Indices = _seaIndexBuffer;
            _seaEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _seaIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
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
        /// Draws the outback (#112): the grid pinned to the camera (snapped to a cell so the land does not
        /// swim), carrying a near-flat spinifex plain with red monoliths displaced into it, shaded per-pixel by
        /// the current dome and shadowed by the shared cloud field. Like the desert it has no point lights, so
        /// it sets none; unlike the desert its terrain carries a real silhouette, which is why the grid is finer.
        /// </summary>
        private void DrawOutback(in SceneFrame frame)
        {
            float cell = OUTBACK_EXTENT / (OUTBACK_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _outbackEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _outbackEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _outbackEffect.Parameters["View"].SetValue(frame.Camera.View);
            _outbackEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _outbackEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _outbackEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _outbackEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _outbackEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _outbackEffect.Parameters["OutbackTime"].SetValue(frame.Time);
            _outbackEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_outbackEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_outbackVertexBuffer);
            _graphicsDevice.Indices = _outbackIndexBuffer;
            _outbackEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _outbackIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the tropical beach (#244): the grid pinned to the camera (snapped to a cell so it does
        /// not swim), carrying the sand ring, the slope under the waterline, the lagoon bed and the far
        /// shore ridge, shaded per-pixel by the current dome and shadowed by the shared cloud field.
        /// The first of the two land draws — the lagoon's water reads the depth this writes. No point
        /// lights of its own, so like the desert and the outback it sets none.
        /// </summary>
        private void DrawTropicalTerrain(in SceneFrame frame)
        {
            float cell = TROPICAL_EXTENT / (TROPICAL_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _tropicalEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _tropicalEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _tropicalEffect.Parameters["View"].SetValue(frame.Camera.View);
            _tropicalEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _tropicalEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _tropicalEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _tropicalEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _tropicalEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _tropicalEffect.Parameters["TropicalTime"].SetValue(frame.Time);
            _tropicalEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_tropicalEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_tropicalVertexBuffer);
            _graphicsDevice.Indices = _tropicalIndexBuffer;
            _tropicalEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _tropicalIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the lagoon: the sea's own effect and grid (<c>Sea.fx</c> unchanged), pushed the
        /// tropical config's water values — calmer swell, turquoise body colours — over the terrain
        /// <see cref="DrawTropicalTerrain"/> just wrote. States exactly as <see cref="DrawSea"/> sets
        /// them, for <see cref="DrawSea"/>'s own reasons: opaque and <c>CullNone</c> (one open surface,
        /// read from above and through the crests), depth-READ so anything under the surface keeps its
        /// own pixels.
        /// <para>
        /// <b>The whole water set is pushed every frame, the config-static values included</b> — and
        /// <see cref="DrawSea"/> re-pushes its own from its side for the same reason: the sea and the
        /// lagoon share this one effect instance, and a NumPad2/V switch applies no config. Pushing
        /// only the per-frame half would leave whichever scene switched in drawing the other's water
        /// until some level re-applied one of them — the exact trap the shared flock's sizing exists
        /// to avoid, in shader-parameter form.
        /// </para>
        /// <para>
        /// The clip radius is the innermost the wiggling waterline ever reaches, less the shoulder
        /// <c>Sea.fx</c> flattens the swell over: inside it the water is under dry sand and
        /// depth-rejected anyway, so the clip exists to give the calm band a coast to die against —
        /// the surf laps onto the beach instead of breaking against a circle. The pool radius is 0:
        /// the drain's standing pool is the sea scene's own arrangement, and there is no funnel
        /// crossing this water anywhere.
        /// </para>
        /// </summary>
        private void DrawTropicalWater(in SceneFrame frame)
        {
            TropicalTerrainConfig terrain = _tropicalConfig.Terrain;
            TropicalWaterConfig water = _tropicalConfig.Water;

            float cell = SEA_EXTENT / (SEA_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            float clip = terrain.ShoreRadius - terrain.CoastNoise - TROPICAL_WATERLINE_BIAS;

            _seaEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _seaEffect.Parameters["IslandHoleRadius"].SetValue(clip);
            _seaEffect.Parameters["FunnelPoolRadius"].SetValue(0f);
            _seaEffect.Parameters["View"].SetValue(frame.Camera.View);
            _seaEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _seaEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _seaEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _seaEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _seaEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _seaEffect.Parameters["SeaTime"].SetValue(frame.Time);
            _seaEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            _seaEffect.Parameters["SeaLevelY"].SetValue(water.LevelY);
            _seaEffect.Parameters["WaterColorDeep"].SetValue(water.WaterDeep.ToVector3());
            _seaEffect.Parameters["WaterColorShallow"].SetValue(water.WaterShallow.ToVector3());
            _seaEffect.Parameters["ShallowBias"].SetValue(water.ShallowBias);
            _seaEffect.Parameters["WaveAmplitude"].SetValue(water.WaveAmplitude);
            _seaEffect.Parameters["WaveSteepness"].SetValue(water.WaveSteepness);
            _seaEffect.Parameters["WaveSpeed"].SetValue(water.WaveSpeed);
            _seaEffect.Parameters["WaveFadeStart"].SetValue(water.WaveFadeStart);
            _seaEffect.Parameters["WaveFadeEnd"].SetValue(water.WaveFadeEnd);
            _seaEffect.Parameters["ChopAmplitude"].SetValue(water.ChopAmplitude);
            _seaEffect.Parameters["ChopFrequency"].SetValue(water.ChopFrequency);
            _seaEffect.Parameters["ChopSpeed"].SetValue(water.ChopSpeed);
            _seaEffect.Parameters["WindDirection"].SetValue(water.Wind.ToVector2());
            _seaEffect.Parameters["SunGlintStrength"].SetValue(water.SunGlintStrength);
            _seaEffect.Parameters["SunGlintPower"].SetValue(water.SunGlintPower);
            _seaEffect.Parameters["FoamJacobianThreshold"].SetValue(water.FoamJacobianThreshold);
            _seaEffect.Parameters["FoamStrength"].SetValue(water.FoamStrength);
            _seaEffect.Parameters["FoamCrestStart"].SetValue(water.FoamCrestStart);
            _seaEffect.Parameters["FoamCrestStrength"].SetValue(water.FoamCrestStrength);
            _seaEffect.Parameters["FoamColor"].SetValue(water.FoamColor.ToVector3());
            _seaEffect.Parameters["SssStrength"].SetValue(water.SssStrength);
            _seaEffect.Parameters["SssColor"].SetValue(water.SssColor.ToVector3());
            _seaEffect.Parameters["HorizonHazeDistance"].SetValue(water.HorizonHazeDistance);

            frame.ApplyClouds?.Invoke(_seaEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            //DrawSea's own reasoning: depth-read, not depth-write — only the open water gives the
            //depth up, and the terrain (drawn before it, opaque) still writes and owns its own.
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _graphicsDevice.SetVertexBuffer(_seaVertexBuffer);
            _graphicsDevice.Indices = _seaIndexBuffer;
            _seaEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _seaIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
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

            //The ring of campfires lights the grass around it (real point lights, present under every dome)
            int fires = SavannaCampfireCount;

            for (int fire = 0; fire < fires; fire++)
            {
                _savannaLightPos[fire] = SavannaCampfirePosition(fire);
                _savannaLightColor[fire] = CampfireColor(frame.Time, fire);
                _savannaLightRange[fire] = SavannaCampfireRange;
            }

            _savannaEffect.Parameters["SceneLightPosition"].SetValue(_savannaLightPos);
            _savannaEffect.Parameters["SceneLightColor"].SetValue(_savannaLightColor);
            _savannaEffect.Parameters["SceneLightRange"].SetValue(_savannaLightRange);
            _savannaEffect.Parameters["SceneLightCount"].SetValue(fires);

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
        /// The tropical terrain height at a world point, mirroring <c>Tropical.fx</c>'s
        /// <c>TropicalHeight</c> term for term (and its <c>CoastRadius</c>/<c>ShoreRingRadius</c>/
        /// <c>ChannelMask</c> beside it), so the palms and the waterline's rocks can be planted on the
        /// ground the shader draws. Static and config-taking for the forest's reasons. Keep this and
        /// the shader in the same change: a drift here plants palms in the surf, and there is nothing
        /// to catch it but the eye.
        /// </summary>
        public static float TropicalTerrainHeight(float x, float z, TropicalSceneConfig config)
        {
            TropicalTerrainConfig terrain = config.Terrain;

            float r = MathF.Sqrt(x * x + z * z);
            float b = MathF.Atan2(z, x);

            float gentle = terrain.ClearingRelief * 0.5f
                * (MathF.Sin(x * 0.043f + z * 0.031f) + 0.6f * MathF.Sin(-x * 0.052f + z * 0.046f + 2.1f));

            float d = r - TropicalCoastRadius(b, terrain);

            //GLSL smoothstep(edge0, edge1, x) is clamp-then-hermite, which MathHelper.SmoothStep is not
            //(the forest's comment records the trap) — spelled out here as the shader spells it.
            float toWaterline = SmoothStep(-terrain.BeachRise, 0f, d);
            float toBed = SmoothStep(0f, terrain.BeachRun, d);

            float h = MathHelper.Lerp(terrain.LevelY + gentle, config.Water.LevelY, toWaterline);
            h = MathHelper.Lerp(h, terrain.SeabedY, toBed);

            float ring = SmoothStep(0f, terrain.RingWidth, r - TropicalRingRadius(b, terrain))
                * (1f - TropicalChannelMask(b, terrain));

            float qx = x + 26f * MathF.Sin(z * 0.011f + 2f);
            float qz = z + 26f * MathF.Sin(x * 0.013f + 5f);

            float rolling = 0.40f * MathF.Sin(qx * 0.020f + qz * 0.015f)
                + 0.27f * MathF.Sin(-qx * 0.013f + qz * 0.024f + 1.5f)
                + 0.19f * MathF.Sin(qx * 0.031f + qz * 0.026f + 3.0f)
                + 0.14f * MathF.Sin(-qx * 0.056f + qz * 0.041f + 0.7f);

            h += ring * terrain.HillHeight * (0.55f + 0.45f * (0.5f + 0.5f * rolling));

            return h;
        }

        //The waterline's radius at a bearing — Tropical.fx's CoastRadius, in one change with it.
        private static float TropicalCoastRadius(float b, TropicalTerrainConfig terrain) =>
            terrain.ShoreRadius + terrain.CoastNoise
                * (0.45f * MathF.Sin(2f * b + 0.7f)
                    + 0.35f * MathF.Sin(3f * b + 1.3f)
                    + 0.20f * MathF.Sin(5f * b + 4.1f));

        //The far shore's coastline — Tropical.fx's ShoreRingRadius.
        private static float TropicalRingRadius(float b, TropicalTerrainConfig terrain) =>
            terrain.RingRadius + terrain.RingNoise
                * (0.40f * MathF.Sin(2f * b + 2.9f)
                    + 0.34f * MathF.Sin(3f * b + 0.6f)
                    + 0.26f * MathF.Sin(7f * b + 3.4f));

        //The channel through the far ridge — Tropical.fx's ChannelMask.
        private static float TropicalChannelMask(float b, TropicalTerrainConfig terrain) =>
            MathF.Pow(MathF.Max(0f, MathF.Cos(b - terrain.ChannelBearing)), terrain.ChannelSharpness);

        //GLSL smoothstep as the shaders spell it: clamp-then-hermite over the raw value.
        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = MathHelper.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
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
        /// Draws the scattered acacia trees and bushes: real 3D geometry (#202), one instanced draw per mesh
        /// variant per material — a tree's canopy (dappled green) and its trunk (brown) share the variant's
        /// per-plant matrices, a bush is its canopy alone. Shaded from the scene's own sun and dome, so a tree
        /// sits in the savanna's light. Opaque and depth-writing; savanna scene only, after the terrain.
        /// </summary>
        private void DrawAcacias(in SceneFrame frame)
        {
            _acaciaViewParam.SetValue(frame.Camera.View);
            _acaciaProjectionParam.SetValue(frame.Camera.Projection);
            _acaciaCameraParam.SetValue(frame.Camera.Position);
            _acaciaSunDirectionParam.SetValue(frame.SunDirection);
            _acaciaSunColorParam.SetValue(frame.SunColor);
            _acaciaZenithParam.SetValue(frame.ZenithLinear);
            _acaciaHorizonParam.SetValue(frame.HorizonLinear);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise; //real solids, wound like every lathe

            //Trees: the canopy (dappled green, per-variant drier or greener) then the trunk (plain brown), both
            //off the one set of per-plant matrices.
            for (int m = 0; m < _acaciaTreeMeshes.Length; m++)
            {
                ModelInstance[] instances = _acaciaTreeInstances[m];
                if (instances.Length == 0) continue;

                Vector3 canopy = Vector3.Lerp(_acaciaCanopyColor, _acaciaCanopyDry, _acaciaTreeDryness[m] * 0.7f);
                DrawAcaciaPart(_acaciaTreeMeshes[m].Canopy, instances, canopy, dappleStrength: 0.6f);
                DrawAcaciaPart(_acaciaTreeMeshes[m].Wood, instances, _acaciaTrunkColor, dappleStrength: 0f);
            }

            //Bushes: the canopy alone, drier and dappled.
            for (int m = 0; m < _acaciaBushMeshes.Length; m++)
            {
                ModelInstance[] instances = _acaciaBushInstances[m];
                if (instances.Length == 0) continue;
                DrawAcaciaPart(_acaciaBushMeshes[m], instances, _acaciaCanopyDry, dappleStrength: 0.5f);
            }

            //And the hearths the fires stand in: one draw per fire, because the firelight on a ring is its
            //own fire's and they do not flicker together.
            DrawHearthStones(frame);
        }

        /// <summary>
        /// Draws the ring of stones around each campfire (#282) on the acacia's own instanced path, one draw
        /// per fire.
        /// <para>
        /// <b>The firelight is a per-draw additive, not a ninth point light.</b> Every stone of a ring stands
        /// at one distance from one fire, so the attenuation a point light would solve per pixel is a
        /// constant here — worked out once against the same quadratic falloff <c>Savanna.fx</c> uses on the
        /// ground, so a stone and the grass beside it are lit by the one fire rather than by two rules. It is
        /// multiplied by the stone's own albedo, because what reaches the eye is firelight reflected off
        /// basalt and not the flame itself, and it rides <see cref="CampfireColor"/> at this frame's time so
        /// the ring breathes with the fire it belongs to.
        /// </para>
        /// </summary>
        private void DrawHearthStones(in SceneFrame frame)
        {
            if (_hearthStoneInstances == null || _hearthStoneMeshes == null) return;

            CampfireConfig cf = _savannaConfig.Campfire;
            float ring = cf.FlameSize * cf.StoneRingScale;

            //The ground's own falloff, at the one distance every stone of a ring stands at.
            float atten = MathHelper.Clamp(1f - ring / MathF.Max(SavannaCampfireRange, 1e-4f), 0f, 1f);
            atten *= atten;

            for (int fire = 0; fire < _hearthStoneInstances.Length; fire++)
            {
                ModelInstance[] instances = _hearthStoneInstances[fire];
                if (instances == null || instances.Length == 0) continue;

                Vector3 firelight = _hearthStoneColor * CampfireColor(frame.Time, fire) * (_hearthStoneFirelight * atten);

                DrawAcaciaPart(_hearthStoneMeshes[fire % _hearthStoneMeshes.Length], instances,
                    _hearthStoneColor, dappleStrength: 0f, addedLight: firelight);
            }
        }

        /// <summary>
        /// One instanced draw of a mesh part with its per-draw material: the instances are re-uploaded to the
        /// one shared dynamic buffer (<see cref="SetDataOptions.Discard"/>, so the GPU is not stalled on the
        /// last draw), the mesh's vertices bound at stream 0 and the instances at stream 1 — exactly as
        /// <see cref="InstancedModelRenderer"/> does it.
        /// </summary>
        private void DrawAcaciaPart(IProceduralMesh mesh, ModelInstance[] instances, Vector3 diffuse, float dappleStrength,
            Vector3 addedLight = default)
        {
            if (_acaciaInstanceBuffer == null || _acaciaInstanceBuffer.VertexCount < instances.Length)
            {
                _acaciaInstanceBuffer?.Dispose();
                _acaciaInstanceBuffer = new DynamicVertexBuffer(_graphicsDevice, ModelInstance.VertexDeclaration,
                    instances.Length, BufferUsage.WriteOnly);
            }
            _acaciaInstanceBuffer.SetData(instances, 0, instances.Length, SetDataOptions.Discard);

            _acaciaDiffuseParam.SetValue(diffuse);
            _acaciaDappleParam.SetValue(dappleStrength);
            _acaciaAddedLightParam.SetValue(addedLight);
            _acaciaEffect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(mesh.VertexBuffer, 0, 0),
                new VertexBufferBinding(_acaciaInstanceBuffer, 0, 1));
            _graphicsDevice.Indices = mesh.IndexBuffer;
            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, instances.Length);
        }

        /// <summary>
        /// Draws the scattered palms: real 3D geometry on the acacia's path (#202), one instanced draw per
        /// mesh variant per material — a palm's crown (dappled green, per-variant drier or greener) and its
        /// wood (the trunk and the dead-frond skirt, plain brown) share the variant's per-plant matrices.
        /// Shaded from the scene's own sun and dome by <c>Palm.fx</c>, which also sways the crown on the
        /// wind off the wall clock. Opaque and depth-writing; tropical scene only, after the terrain and
        /// the water.
        /// </summary>
        private void DrawPalms(in SceneFrame frame)
        {
            ApplyPalmFrame(frame);

            for (int m = 0; m < _palmMeshes.Length; m++)
            {
                ModelInstance[] instances = _palmInstances[m];
                if (instances.Length == 0) continue;

                Vector3 frond = Vector3.Lerp(_palmFrondColor, _palmFrondDry, _palmDryness[m] * 0.6f);

                //The palms are the one thing here that MEANS to sway: PalmMesh bakes the weight ramp the
                //shader reads, zero along the trunk and rising to the frond tips, so the wind moves the
                //crown and never the trunk.
                float sway = _tropicalConfig.Palms.SwayStrength;

                DrawPalmPart(_palmMeshes[m].Fronds, instances, frond, dappleStrength: 0.55f, swayStrength: sway);
                DrawPalmPart(_palmMeshes[m].Wood, instances, _palmTrunkColor, dappleStrength: 0f, swayStrength: sway);
            }
        }

        /// <summary>
        /// Draws the waterline's rocks: the stone (grey-brown, plain) and its moss cap (green, a light
        /// mottle so the moss is foliage and not paint) over the same per-plant matrices, shaded by the
        /// same <c>Palm.fx</c>. Opaque and depth-writing; tropical scene only, after the palms.
        /// </summary>
        private void DrawTropicalRocks(in SceneFrame frame)
        {
            ApplyPalmFrame(frame);

            for (int m = 0; m < _tropicalRockMeshes.Length; m++)
            {
                ModelInstance[] instances = _tropicalRockInstances[m];
                if (instances.Length == 0) continue;

                //NO SWAY: a boulder does not move in the wind, and both of these meshes are LatheMeshes
                //whose TEXCOORD0.x runs 0..1 around the circumference — which Palm.fx reads as its sway
                //weight (see DrawPalmPart). Left at the palms' strength the stones sheared open.
                DrawPalmPart(_tropicalRockMeshes[m], instances, _tropicalStoneColor, dappleStrength: 0f, swayStrength: 0f);
                DrawPalmPart(_tropicalMossMeshes[m], instances, _tropicalMossColor, dappleStrength: 0.30f, swayStrength: 0f);
            }
        }

        /// <summary>
        /// Pushes the frame's shared palm-effect parameters — the two draws below run them once each, so
        /// the pushing lives in one place between them. The states are the acacia's: opaque, depth-writing
        /// solids wound like every lathe (the fronds are double-sided GEOMETRY rather than a culling
        /// exception, so the shared clockwise culling holds).
        /// </summary>
        private void ApplyPalmFrame(in SceneFrame frame)
        {
            _palmViewParam.SetValue(frame.Camera.View);
            _palmProjectionParam.SetValue(frame.Camera.Projection);
            _palmSunDirectionParam.SetValue(frame.SunDirection);
            _palmSunColorParam.SetValue(frame.SunColor);
            _palmZenithParam.SetValue(frame.ZenithLinear);
            _palmHorizonParam.SetValue(frame.HorizonLinear);

            //The wind off the wall clock, aligned with the one the waves and the canopy ride — a beach
            //whose palms swayed against their own surf would read as two weathers.
            //
            //The sway's STRENGTH is deliberately not here: it is a per-part argument of DrawPalmPart, whose
            //doc says why (a mesh with real texture UVs would otherwise inherit the palms' own sway).
            _palmTimeParam.SetValue(frame.Time);
            _palmWindParam.SetValue(_tropicalConfig.Terrain.Wind.ToVector2());
            _palmSwaySpeedParam.SetValue(_tropicalConfig.Palms.SwaySpeed);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// One instanced draw of a mesh part with its per-draw material — <see cref="DrawAcaciaPart"/>'s
        /// construction on the palm effect and buffer: instances re-uploaded to the one shared dynamic
        /// buffer (<see cref="SetDataOptions.Discard"/>), the mesh at stream 0 and the instances at
        /// stream 1.
        /// </summary>
        /// <summary>
        /// One instanced draw through <c>Palm.fx</c>.
        /// <para>
        /// <b><paramref name="swayStrength"/> is a per-part argument and not a per-frame one, which is
        /// #268's rock fault in one line.</b> <c>Palm.fx</c> reads the mesh's <c>TEXCOORD0.x</c> — an
        /// ordinary texture coordinate — as its sway weight, on the understanding that
        /// <see cref="PalmMesh"/> bakes a deliberate 0-along-the-trunk-to-1-at-the-frond-tip ramp there.
        /// Every other mesh drawn through this effect carries <i>real</i> UVs, and <see cref="LatheMesh"/>
        /// (which is what both a <see cref="RockMesh"/> and its moss cap are) writes
        /// <c>s / segments</c> — 0 to 1 <b>around the circumference</b>. Set once for the whole frame, the
        /// palms' own strength therefore reached the waterline rocks and swung one side of every ring at
        /// full frond-tip weight while the other side stood still: the stones did not merely drift in the
        /// wind, they sheared. Passing it per part is what makes a mesh unable to inherit a sway nobody
        /// meant it to have.
        /// </para>
        /// </summary>
        private void DrawPalmPart(IProceduralMesh mesh, ModelInstance[] instances, Vector3 diffuse,
            float dappleStrength, float swayStrength)
        {
            if (_palmInstanceBuffer == null || _palmInstanceBuffer.VertexCount < instances.Length)
            {
                _palmInstanceBuffer?.Dispose();
                _palmInstanceBuffer = new DynamicVertexBuffer(_graphicsDevice, ModelInstance.VertexDeclaration,
                    instances.Length, BufferUsage.WriteOnly);
            }
            _palmInstanceBuffer.SetData(instances, 0, instances.Length, SetDataOptions.Discard);

            _palmDiffuseParam.SetValue(diffuse);
            _palmDappleParam.SetValue(dappleStrength);
            _palmSwayStrengthParam.SetValue(swayStrength);
            _palmEffect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(mesh.VertexBuffer, 0, 0),
                new VertexBufferBinding(_palmInstanceBuffer, 0, 1));
            _graphicsDevice.Indices = mesh.IndexBuffer;
            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, instances.Length);
        }

        /// <summary>
        /// Draws the volcano's flank (#223): the grid pinned to the camera and snapped to a cell so the ground
        /// does not swim, displaced into the cone and its gullies, with the lava rivers drawn as an emissive
        /// band on it and the crust cracking glowing between them. Opaque and depth-writing, drawn first in
        /// the scene block, <see cref="RasterizerState.CullNone"/> (the winding is moot on a heightfield).
        /// </summary>
        private void DrawVolcanoTerrain(in SceneFrame frame)
        {
            float cell = VOLCANO_EXTENT / (VOLCANO_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _volcanoEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _volcanoEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _volcanoEffect.Parameters["View"].SetValue(frame.Camera.View);
            _volcanoEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _volcanoEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _volcanoEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _volcanoEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _volcanoEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _volcanoEffect.Parameters["SunColor"].SetValue(frame.SunColor);
            _volcanoEffect.Parameters["VolcanoTime"].SetValue(frame.Time);

            frame.ApplyClouds?.Invoke(_volcanoEffect);

            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_volcanoVertexBuffer);
            _graphicsDevice.Indices = _volcanoIndexBuffer;
            _volcanoEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _volcanoIndexCount / 3);

            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the lava fountains and the smoke plume standing over the crater — two index ranges over the
        /// <b>one</b> particle buffer, so the plume pass never touches a jet's vertex and the jet pass never
        /// touches a puff's. Both depth-read (the flank and the cluster hide what is behind them) and writing
        /// no depth; the plume alpha-blended first, the jets additively over it, which is the order those two
        /// have to be drawn in for smoke to sit behind fire rather than over it.
        /// <para>
        /// The eruption envelope goes in here rather than being computed per particle: one figure off the wall
        /// clock (<see cref="VolcanoEruption"/>) drives the jets' reach, the plume's density and the crater's
        /// point light together, which is what makes a burst read as one event.
        /// </para>
        /// </summary>
        private void DrawLavaFountains(in SceneFrame frame)
        {
            if (_fountainVertexBuffer == null) return;

            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _fountainEffect.Parameters["View"].SetValue(frame.Camera.View);
            _fountainEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _fountainEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _fountainEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _fountainEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _fountainEffect.Parameters["FountainTime"].SetValue(frame.Time);
            _fountainEffect.Parameters["Eruption"].SetValue(VolcanoEruption(frame.Time));

            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SetVertexBuffer(_fountainVertexBuffer);
            _graphicsDevice.Indices = _fountainIndexBuffer;

            if (_plumeQuads > 0)
            {
                _graphicsDevice.BlendState = BlendState.AlphaBlend;
                _fountainEffect.CurrentTechnique = _plumeTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _plumeQuads * 2);
            }

            if (_jetQuads > 0)
            {
                _graphicsDevice.BlendState = BlendState.Additive;
                _fountainEffect.CurrentTechnique = _jetTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, _plumeQuads * 6, _jetQuads * 2);
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the drifting ash: a boxful of specks around the camera, animated entirely in the vertex
        /// shader. Alpha-blended and depth-read but writing no depth, drawn last with the overlays. Volcano
        /// scene only.
        /// </summary>
        private void DrawAsh(in SceneFrame frame)
        {
            if (_ashVertexBuffer == null) return;

            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _ashEffect.Parameters["View"].SetValue(frame.Camera.View);
            _ashEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _ashEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _ashEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _ashEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _ashEffect.Parameters["AshTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_ashVertexBuffer);
            _graphicsDevice.Indices = _ashIndexBuffer;
            _ashEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0,
                Math.Clamp(_volcanoConfig.Ash.FlakeCount, 0, MAX_BILLBOARD_PARTICLES) * 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws Mars (#277): the grid pinned to the camera (snapped to a cell so the land does not swim),
        /// carrying the Moon's crater field retextured rust and shaded per-pixel by the current dome and
        /// the shared cloud field — the outback's plumbing, not the Moon's domeless one. No point lights
        /// and no birds of its own, like the desert and the outback.
        /// </summary>
        private void DrawMarsTerrain(in SceneFrame frame)
        {
            float cell = MARS_EXTENT / (MARS_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _marsEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _marsEffect.Parameters["IslandHoleRadius"].SetValue(TerrainHoleRadius);
            _marsEffect.Parameters["View"].SetValue(frame.Camera.View);
            _marsEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _marsEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _marsEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _marsEffect.Parameters["SunColor"].SetValue(frame.SunColor);
            _marsEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _marsEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);

            frame.ApplyClouds?.Invoke(_marsEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_marsVertexBuffer);
            _graphicsDevice.Indices = _marsIndexBuffer;
            _marsEffect.CurrentTechnique = _marsTerrainTechnique;
            _marsEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _marsIndexCount / 3);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws Phobos and Deimos: two small analytic discs on space's shared full-screen quad
        /// (<c>_spaceQuad</c>), depth-read against the depth <see cref="DrawMarsTerrain"/> just wrote —
        /// Moon.fx's own measured reason (its <c>DrawMoon</c> doc) for reading depth after the ground
        /// rather than before it, carried over even though this pass is far cheaper than a starfield.
        /// Alpha-blended, unlike every sky-replacing scene's opaque quad pass: this composites two small
        /// discs over a dome and a terrain that are already drawn, not a full-screen backdrop of its own.
        /// </summary>
        private void DrawMarsMoons(in SceneFrame frame)
        {
            _marsMoonsInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _marsMoonsCameraPosition.SetValue(frame.Camera.Position);
            _marsEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_spaceQuad);
            _marsEffect.CurrentTechnique = _marsMoonsTechnique;
            _marsEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the visible flames: one billboard per fire at its <see cref="SavannaCampfirePosition"/>, a
        /// procedural flickering flame in the shader, drawn additively and depth-read (the terrain or platform
        /// in front hides one) but writing no depth. The light each casts is a separate scene point light.
        /// Savanna scene only, drawn last with the overlays.
        /// <para>
        /// A draw per fire rather than one instanced pass: it is two triangles each, eight of them at most,
        /// once a frame and only in this scene — and the alternative is an instance buffer and a vertex format
        /// for a quad that already has neither. What varies per fire is two uniforms.
        /// </para>
        /// </summary>
        private void DrawFlame(in SceneFrame frame)
        {
            _flameEffect.Parameters["View"].SetValue(frame.Camera.View);
            _flameEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _flameEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _flameEffect.Parameters["FlameSize"].SetValue(_savannaConfig.Campfire.FlameSize);
            _flameEffect.Parameters["FlameHeightScale"].SetValue(_savannaConfig.Campfire.FlameHeightScale);

            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_flameVertexBuffer);
            _graphicsDevice.Indices = _flameIndexBuffer;

            //Cached out of the loop: the by-name indexer is a linear scan, and this runs once per fire per
            //frame (BestPractices.md §1). Not fields, because nothing else in this class touches them.
            EffectParameter flamePosition = _flameEffect.Parameters["FlamePosition"];
            EffectParameter flameSeed = _flameEffect.Parameters["FlameSeed"];
            EffectParameter flameTime = _flameEffect.Parameters["FlameTime"];

            for (int fire = 0; fire < SavannaCampfireCount; fire++)
            {
                flamePosition.SetValue(SavannaCampfirePosition(fire));

                //The same stride and rate stretch CampfireColor uses, so a flame and the light it casts are
                //the one fire rather than two things that happen to be in the same place.
                flameSeed.SetValue(1f + fire * 0.031f);
                flameTime.SetValue(frame.Time + fire * 3.77f);

                _flameEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the flock: each bird circles the config's flock centre on its own slow orbit, banked into
        /// the turn it is flying, beating or gliding on a cycle of its own. One draw per bird of the one
        /// shared <see cref="BirdMesh"/>, opaque and depth-writing. Called in the savanna, desert and outback
        /// scenes, which share the flock; <paramref name="birds"/> is the active scene's config, and its
        /// count is capped to the seeded state's size.
        /// <para>
        /// A draw per bird rather than one instanced pass, for <see cref="DrawFlame"/>'s reason: what varies
        /// per bird is three uniforms, there are nine of them at most and only in three scenes, and the
        /// alternative is an instance buffer and a vertex format for something that has neither. The mesh is
        /// bound once outside the loop, so a bird costs three parameter writes and a draw.
        /// </para>
        /// </summary>
        private void DrawBirds(in SceneFrame frame, BirdsConfig birds)
        {
            _birdColorParam.SetValue(birds.Color.ToVector3());
            _birdViewParam.SetValue(frame.Camera.View);
            _birdProjectionParam.SetValue(frame.Camera.Projection);
            _birdSunDirectionParam.SetValue(frame.SunDirection);
            _birdSunColorParam.SetValue(frame.SunColor);
            _birdZenithParam.SetValue(frame.ZenithLinear);
            _birdHorizonParam.SetValue(frame.HorizonLinear);

            //Opaque and depth-writing, where the billboard was alpha-blended and depth-read: a bird is a
            //solid now, and its own breast has to be able to hide the far wing behind it.
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            //CullNone: the wings, the primaries and the tail are single sheets with no back to them, and the
            //pixel shader turns the normal towards the camera rather than the rasteriser dropping the face.
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_birdMesh.VertexBuffer);
            _graphicsDevice.Indices = _birdMesh.IndexBuffer;

            Vector3 flockCenter = birds.FlockCenter.ToVector3();
            Matrix scale = Matrix.CreateScale(birds.Wingspan);

            int count = Math.Min(birds.Count, _birdRadius.Length);
            for (int i = 0; i < count; i++)
            {
                float radius = _birdRadius[i];
                float angle = frame.Time * _birdOrbitSpeed[i] + _birdOrbitPhase[i];
                float bobPhase = frame.Time * _birdBobSpeed[i] + _birdOrbitPhase[i];

                Vector3 center = flockCenter + new Vector3(
                    MathF.Cos(angle) * radius,
                    _birdAltitude[i] + MathF.Sin(bobPhase) * birds.Bob,
                    MathF.Sin(angle) * radius);

                //The nose goes where the bird is actually going: the circle's horizontal tangent plus the
                //climb its drift is doing at this instant. A bird therefore tips up as it rises and down as
                //it sinks, off the derivative of its own bob rather than off a dial that says how far to tip.
                float horizontalSpeed = _birdOrbitSpeed[i] * radius;
                float climbRate = MathF.Cos(bobPhase) * _birdBobSpeed[i] * birds.Bob;
                Vector3 forward = new(
                    -MathF.Sin(angle) * horizontalSpeed,
                    climbRate,
                    MathF.Cos(angle) * horizontalSpeed);

                //Banked into the turn. Leaning the bird's UP vector towards the inside of its circle is the
                //whole of it — CreateWorld squares the basis up from there, so there is no hand-rolled cross
                //product here to get the handedness wrong in.
                float lean = MathHelper.Lerp(BIRD_BANK_TIGHT, BIRD_BANK_WIDE,
                        MathHelper.Clamp((radius - BIRD_RADIUS_MIN) / BIRD_RADIUS_SPAN, 0f, 1f))
                    + BIRD_BANK_DRIFT * MathF.Sin(frame.Time * BIRD_BANK_DRIFT_SPEED + _birdOrbitPhase[i]);

                Vector3 inward = new(-MathF.Cos(angle), 0f, -MathF.Sin(angle));
                Vector3 up = Vector3.Up * MathF.Cos(lean) + inward * MathF.Sin(lean);

                ResolveFlap(i, frame.Time, out float flapPhase, out float flapAmount);

                _birdWorldParam.SetValue(scale * Matrix.CreateWorld(center, forward, up));
                _birdFlapPhaseParam.SetValue(flapPhase);
                _birdFlapAmountParam.SetValue(flapAmount);

                _birdsEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _birdMesh.PrimitiveCount);
            }

            //Restore the scene block's states for the draws that follow
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Where bird <paramref name="index"/> is in its wingbeat, and how much of a wingbeat it is doing at
        /// all. A soaring bird spends most of its time gliding and beats in short bursts; the old flock beat
        /// at one fixed rate for ever, which is half of why it read as mechanical.
        /// <para>
        /// <b>Both edges of the burst are continuous, by construction rather than by smoothing.</b> A burst
        /// runs a WHOLE number of beats, so it ends on the same neutral point of the stroke it started on,
        /// and the glide that follows sweeps exactly one slow turn of the same phase — so phase is continuous
        /// across both boundaries without ever being tracked between frames. The envelope is floored at
        /// <see cref="BIRD_GLIDE_TRIM"/>, which is the value the glide holds, so the amount does not step
        /// either. Nothing here is stateful: a bird's whole flap is a function of the wall clock.
        /// </para>
        /// </summary>
        private void ResolveFlap(int index, float time, out float phase, out float amount)
        {
            float cycle = time / _birdFlapPeriod[index] + _birdFlapCyclePhase[index];
            cycle -= MathF.Floor(cycle);

            float burst = _birdBurstFraction[index];
            if (cycle < burst)
            {
                float progress = cycle / burst;
                phase = progress * _birdFlapBeats[index] * MathHelper.TwoPi;
                amount = MathF.Max(MathF.Sin(MathF.PI * progress), BIRD_GLIDE_TRIM);
            }
            else
            {
                phase = (cycle - burst) / (1f - burst) * MathHelper.TwoPi;
                amount = BIRD_GLIDE_TRIM;
            }
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

            //The only animated thing in a long-exposure sky: the eye's slow drift through the volume it is
            //inside. Wall clock, like every other scene's, so it keeps moving while the simulation is paused.
            _spaceTime.SetValue(frame.Time);

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
        /// Shades a sky-replacing backdrop into a target the size of the <b>back buffer</b> and scales it up
        /// into the caller's supersampled scene target, which is a quarter of the pixels at <c>ssaa 2</c> and a
        /// sixteenth at <c>4</c>. What it gives up is the backdrop's <i>supersampling</i> — not its resolution:
        /// the cave still comes out at every pixel the display has, and the balls, the arena and the gun drawn
        /// over it keep every sample they had, because they are still drawn into the full-size target.
        /// <para>
        /// <b>Why this and not another feature cut.</b> Measured on the reference 6900 XT at a fixed camera,
        /// 1600×900: the cavern pass costs 1.5 ms at ssaa 1, 5.2 at 2 and 21.0 at 4 — 4.04× for 4× the pixels
        /// across the top step, and a fitted fixed cost of essentially zero. It is pure fill, so pixel count is
        /// the only dial that moves it, which is the same finding #102 reached from the other side when every
        /// individual feature it removed saved nothing. At fullscreen <c>High</c> the scene measured 21.0 ms
        /// against a 6.4 ms frame without it, and <see cref="SceneDetail"/>'s reduced program — the cut #155
        /// asked for, already in the code since #102 — is a 0.715× that lands at 16.8 ms, i.e. 59 FPS. Only the
        /// resolution reaches 75.
        /// </para>
        /// <para>
        /// <b>It is these two scenes and not all four that replace the sky.</b> Space sizes its stars in
        /// <i>output</i> pixels off <see cref="SupersampleFactor"/> (see that property), so shading it into a
        /// smaller target would change how big and how bright its stars come out — and it costs 0.7 ms over a
        /// bare frame anyway, so there is nothing to buy. The Moon draws its sky depth-read behind real
        /// terrain, so it is not a full-screen pass at all by the time it is shaded.
        /// </para>
        /// <para>
        /// The blit restores the states the opaque scene expects, exactly as the direct path does — the
        /// backdrop's own draw does it too, but it does it while the small target is still bound, and
        /// <see cref="SpriteBatch"/> then leaves its own behind.
        /// </para>
        /// </summary>
        private void DrawBackdropAtDisplayResolution(SceneKind scene, in SceneFrame frame, RenderTarget2D sceneTarget)
        {
            int width = _graphicsDevice.PresentationParameters.BackBufferWidth;
            int height = _graphicsDevice.PresentationParameters.BackBufferHeight;

            //A minimized window reports a zero back buffer and a zero-sized target is a device error — the
            //same guard PostProcessPipeline.EnsureTarget carries, for the same reason. Falling through draws
            //the backdrop at full price, which is correct and merely expensive.
            if (width > 0 && height > 0 && (_backdropTarget == null || _backdropTarget.Width != width || _backdropTarget.Height != height))
            {
                _backdropTarget?.Dispose();

                //Linear radiance like the target it feeds, and no depth: the pass runs with the depth state
                //off, so there is nothing to write.
                _backdropTarget = new RenderTarget2D(_graphicsDevice, width, height, false, SurfaceFormat.HdrBlendable,
                    DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
            }

            if (_backdropTarget == null)
            {
                if (scene == SceneKind.Cavern) DrawCavern(frame); else DrawDream(frame);
                return;
            }

            _graphicsDevice.SetRenderTarget(_backdropTarget);

            if (scene == SceneKind.Cavern) DrawCavern(frame); else DrawDream(frame);

            _graphicsDevice.SetRenderTarget(sceneTarget);

            _backdropBatch ??= new SpriteBatch(_graphicsDevice);

            //Opaque because the backdrop is the frame's ground floor and covers every pixel of it; linear
            //clamp because a bilinear stretch of a smooth analytic field is what makes this cost nothing to
            //look at, and point sampling would show the smaller grid as blocks.
            _backdropBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            _backdropBatch.Draw(_backdropTarget, new Rectangle(0, 0, sceneTarget.Width, sceneTarget.Height), Color.White);
            _backdropBatch.End();

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

        /// <summary>
        /// Draws the Moon: two passes of one effect, because the scene is in both families at once (#125).
        /// First the terrain — the desert's displaced camera-centred grid (snapped to a cell so the craters
        /// do not swim), an ordinary depth-writing opaque draw — then the sky: space's full-screen machinery
        /// on the shared quad, <b>depth-read</b> against what the terrain just wrote, so the star shader
        /// only runs where sky is actually visible. No cloud hook and no time uniform: there is no air and
        /// nothing on the Moon moves.
        /// <para>
        /// <b>The order is measured, not stylistic.</b> The first build drew the sky first with
        /// <see cref="DepthStencilState.None"/> (the other sky-replacing scenes' state) and the terrain over
        /// it, and in the Game's frame that interleave measured <b>244 ms</b> at High on the reference APU
        /// against <b>17 ms</b> for this order — an 8× blow-up that neither pass shows alone (sky alone 8 ms,
        /// terrain alone 18) and that the Testbed's frame never reproduced. The mechanism was not chased past
        /// the fix, because depth-read-after-terrain is the right order regardless: it also stops paying for
        /// starfield pixels the ground was always going to cover.
        /// </para>
        /// </summary>
        private void DrawMoon(in SceneFrame frame)
        {
            //Row vectors, as everywhere else: a world point goes out through View then Projection, so a
            //clip-space corner comes back through the inverse of that product.
            _moonInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _moonView.SetValue(frame.Camera.View);
            _moonProjection.SetValue(frame.Camera.Projection);
            _moonCameraPosition.SetValue(frame.Camera.Position);
            _moonSunDirection.SetValue(frame.SunDirection);
            _moonSupersample.SetValue((float)SupersampleFactor);

            float cell = MOON_EXTENT / (MOON_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _moonOriginXZ.SetValue(new Vector2(originX, originZ));
            _moonHoleRadius.SetValue(TerrainHoleRadius);

            //The ground first, an ordinary depth-writing opaque draw. Unlike the other three sky-replacing
            //scenes the backdrop pass is NOT unconditional here — half the frame is ground — so the terrain
            //goes in first and the sky pass reads the depth it wrote. Every state is stated, not inherited
            //(the repo rule): this is the frame's first scene draw in two of the three hosts.
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_moonVertexBuffer);
            _graphicsDevice.Indices = _moonIndexBuffer;
            _moonEffect.CurrentTechnique = _moonTerrainTechnique;
            _moonEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _moonIndexCount / 3);

            //Then the sky, depth-READ at the far plane (the quad sits at z = w): every pixel the terrain
            //already owns is rejected before the star shader runs, so the sky pass only pays for the sky
            //that is visible. DepthRead, not None — the test is what buys that, and writing is what the
            //backdrop must never do.
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _graphicsDevice.SetVertexBuffer(_spaceQuad);
            _moonEffect.CurrentTechnique = _moonSkyTechnique;
            _moonEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Put back what the rest of the opaque scene wants
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        public void Dispose()
        {
            _spaceQuad?.Dispose();

            _backdropTarget?.Dispose();
            _backdropBatch?.Dispose();

            _seaVertexBuffer?.Dispose();
            _seaIndexBuffer?.Dispose();
            _desertVertexBuffer?.Dispose();
            _desertIndexBuffer?.Dispose();
            _outbackVertexBuffer?.Dispose();
            _outbackIndexBuffer?.Dispose();
            _tropicalVertexBuffer?.Dispose();
            _tropicalIndexBuffer?.Dispose();
            DisposeTropical();
            _volcanoVertexBuffer?.Dispose();
            _volcanoIndexBuffer?.Dispose();
            _fountainVertexBuffer?.Dispose();
            _fountainIndexBuffer?.Dispose();
            _ashVertexBuffer?.Dispose();
            _ashIndexBuffer?.Dispose();
            _savannaVertexBuffer?.Dispose();
            _savannaIndexBuffer?.Dispose();
            DisposeAcacia();
            DisposeHearthStones();
            _flameVertexBuffer?.Dispose();
            _flameIndexBuffer?.Dispose();
            _birdMesh?.Dispose();
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
            _moonVertexBuffer?.Dispose();
            _moonIndexBuffer?.Dispose();
            _marsVertexBuffer?.Dispose();
            _marsIndexBuffer?.Dispose();
            _stormVertexBuffer?.Dispose();
            _stormIndexBuffer?.Dispose();
        }
    }
}
