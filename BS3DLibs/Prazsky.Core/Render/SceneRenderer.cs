using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>The volcano's separately drawn layers, for <see cref="SceneRenderer.VolcanoLayers"/> (#540).</summary>
    [System.Flags]
    public enum VolcanoLayer { None = 0, Terrain = 1, Plume = 2, Jets = 4, Glow = 8, Ash = 16, All = Terrain | Plume | Jets | Glow | Ash }

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

        /// <summary>The drawn dome along its lowest directions (<see cref="SkyLightRig.FarSky"/>) — what the
        /// open-ground scenes' far ground fades into (#551). Null leaves the far fade off.</summary>
        public readonly Vector3[] FarSky;

        public SceneFrame(ICamera camera, Vector3 sunDirection, Vector3 zenithLinear, Vector3 horizonLinear,
            Vector3 sunColor, float time, Action<Effect> applyClouds, Vector3[] farSky = null)
        {
            Camera = camera;
            SunDirection = sunDirection;
            ZenithLinear = zenithLinear;
            HorizonLinear = horizonLinear;
            SunColor = sunColor;
            Time = time;
            ApplyClouds = applyClouds;
            FarSky = farSky;
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
    /// Somewhere in a scene worth looking AT, and how a camera should stand to frame it (#289) — what a shot
    /// is handed when it is asked to show off the <b>place</b> rather than the arena.
    /// <para>
    /// <b>It states the subject in world space and the stand as a recipe, and the split is deliberate.</b>
    /// Where the interesting thing is, is the scene's own business and nothing else can know it — the
    /// volcano's crater moves with its cone config, the savanna's fire with its own, and the map editor's
    /// live panel moves both while somebody watches. How far out and how high a camera should be to frame
    /// anything is the CAMERA's business, and it is the half that has to scale with the LEVEL: a tall cluster
    /// is played from further back and its establishing shot has to stand back with it. So a scene hands over
    /// one point and three numbers, and the caller multiplies them into its own solved stand-off.
    /// </para>
    /// <para>
    /// The arena stands at the world origin — the terrain hole, the island and the drain are all cut around
    /// it — which is what lets a scene state <see cref="LookAt"/> without being told where anything is.
    /// </para>
    /// </summary>
    public readonly struct SceneViewpoint
    {
        /// <summary>What the shot frames, in world space.</summary>
        public readonly Vector3 LookAt;

        /// <summary>How far out the camera should stand from the arena, as a multiple of the level's own
        /// solved stand-off — not of the distance to <see cref="LookAt"/>, which is often hundreds of units
        /// away and in three scenes is in the sky.</summary>
        public readonly float DistanceScale;

        /// <summary>How high it should ride, in degrees above the arena's horizontal.</summary>
        public readonly float ElevationDegrees;

        /// <summary>
        /// Where it should stand, in degrees around the arena from <see cref="LookAt"/>'s own bearing. Zero
        /// puts the camera between the arena and the subject looking outward — pure scenery, the island out
        /// of frame; 180 puts the subject beyond the arena, so the island and its hanging cluster stand in
        /// front of it. Everything between is the oblique three-quarter view that has both.
        /// </summary>
        public readonly float BearingOffsetDegrees;

        /// <summary>What it is, for the one log line an intro writes. ASCII and short, for the reason every
        /// other console line in this project is.</summary>
        public readonly string Name;

        public SceneViewpoint(Vector3 lookAt, float distanceScale, float elevationDegrees,
            float bearingOffsetDegrees, string name)
        {
            LookAt = lookAt;
            DistanceScale = distanceScale;
            ElevationDegrees = elevationDegrees;
            BearingOffsetDegrees = bearingOffsetDegrees;
            Name = name;
        }
    }

    /// <summary>
    /// An event a scene stages on its own clock — a lightning strike, an eruption — as the <b>sound</b>
    /// needs it (#219, #223). See <see cref="SceneRenderer.TryGetSceneEvent"/>, which is where it comes from
    /// and where the reasoning is.
    /// </summary>
    public readonly struct SceneEvent
    {
        /// <summary>Which event it is: the period's own index. Two calls that answer the same index are
        /// looking at one event, however far apart in the event they are.</summary>
        public readonly int Index;

        /// <summary>The wall-clock second at which the event's <b>light</b> began.</summary>
        public readonly float OnsetTime;

        /// <summary>Where it happened, world space.</summary>
        public readonly Vector3 At;

        /// <summary>How big it is, 0–1 — the very figure the light rides, so a loud one is a bright one.</summary>
        public readonly float Size;

        public SceneEvent(int index, float onsetTime, Vector3 at, float size)
        {
            Index = index;
            OnsetTime = onsetTime;
            At = at;
            Size = size;
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

        //Every terrain grid, one per distinct (vertices a side, extent), shared by the scenes that ask for it (#589)
        private readonly TerrainGridCache _gridCache;

        /// <summary>
        /// Radius of the arena platform's footprint, cut out of every solid terrain scene (mountains, meadow,
        /// savanna, desert) and out of the sea around the world origin so the drain funnel below the island
        /// reads as a drain into a pit rather than a bowl in flat ground - the flat clearing otherwise slices
        /// across the funnel just below its rim, hiding its depth and swallowing the balls falling through, and
        /// the sea otherwise runs its wave mesh straight through the funnel's open throat (#132). The sea's cut
        /// is an annulus rather than the full disc: inside the funnel the water survives as a calm standing
        /// pool where the glass cone crosses the mean level (see the pool derivation in <c>SeaBackdrop.Draw</c>). The
        /// Testbed sets this to the island's radius; the map editor draws no island, so it leaves it 0 (the
        /// default) and nothing is cut.
        /// </summary>
        public float TerrainHoleRadius { get => _services.TerrainHoleRadius; set => _services.TerrainHoleRadius = value; }

        /// <summary>Mean sea level of the sea scene (world Y), so the caller can tell when its camera is under
        /// the water and fade in the underwater murk.</summary>
        public float SeaLevelY => _sea.LevelY;

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
        public void ApplySeaSubmerge(Effect effect, SceneKind scene, float lensSubmerged) =>
            _sea.ApplySubmerge(effect, scene, lensSubmerged);

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
            _sea.LensSubmergedAmount(scene, cameraPosition);

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
        /// <see cref="ApplyKillPlaneFade"/>. Wider than the sea's own <see cref="SeaBackdrop.SEA_SUBMERGE_FADE"/> (3): there
        /// is no water here to slow a ball first, so by the time one nears the plane it can be falling several
        /// units a second, and too shallow a band would still read as a pop, only a slightly later one.
        /// </summary>
        private const float KILL_PLANE_FADE_DEPTH = 6f;

        /// <summary>
        /// How many scene-target texels make one output pixel — the caller's supersampling factor, which only
        /// the caller knows (the Game's moves with the quality tier). The space scene sizes its stars in
        /// <b>output</b> pixels off this: sized in texels instead, a star would come out four times dimmer at
        /// 2× than at 1×, which is the same sky looking different on two quality settings. Left at 1 it is
        /// simply the no-supersampling case, so a caller that never sets it still gets a correct sky.
        /// </summary>
        public int SupersampleFactor
        {
            get => _services.SupersampleFactor;
            set => _services.SupersampleFactor = value;
        }

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
                _farField.SceneDetail = value;

                //Selected HERE and not only in ApplyForestParameters (ForestBackdrop's since #580), which runs from the constructor and on a
                //config change and so had already run by the time a host set this — the first wiring set the
                //property, never re-selected, and drew the full-price floor at every tier while looking
                //perfectly correct. Caught by making the reduced technique output flat red for one run: the
                //floor stayed green.
                SelectDetailTechniques();
            }
        }

        private float _sceneDetail = 1f;

        /// <summary>
        /// The volcano's layers, for taking them apart (#540): the flank's terrain, the ash column, the lava jets,
        /// the blaze over the crater and the drifting ash. A <b>measurement dial</b> and nothing else — the Testbed's
        /// <c>volcano=</c> argument and alternation dial set it, and every other caller leaves it at
        /// <see cref="VolcanoLayer.All"/>. It exists because #540 found the volcano the one scene that misses
        /// <c>Low</c>'s budget on the APU and no switch separated what it draws.
        /// </summary>
        public VolcanoLayer VolcanoLayers
        {
            get => _volcano.Layers;
            set => _volcano.Layers = value;
        }

        //Scene configuration. Defaults reproduce the original hard-coded look byte-for-byte; every scene
        //reads its tuning from these instead of constants. Replaced at runtime by Apply(SceneConfig) when a
        //level is loaded (issue #32), which re-pushes the effect parameters and rebuilds the scatter/particle
        //buffers the config sizes.
        private SavannaSceneConfig _savannaConfig = new();
        private TropicalSceneConfig _tropicalConfig = new();

        #region Tropical

        private readonly Effect _tropicalEffect;

        //The lagoon's own clone of Sea.fx (#580); it draws over the sea's grid, which it asks the cache for
        //itself (SeaBackdrop.SEA_GRID_N over SEA_EXTENT) — the same pair of buffers, the cache's and read-only
        private readonly Effect _lagoonEffect;
        private readonly VertexBuffer _lagoonVertexBuffer;
        private readonly IndexBuffer _lagoonIndexBuffer;
        private readonly int _lagoonIndexCount;
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
        //sea's own shader and grid under a clone of its own (_lagoonEffect, #580) — see DrawTropicalWater.

        #endregion

        #region Palms and waterline rocks (tropical scene only)

        private readonly Effect _palmEffect;

        //Cached effect parameters for the per-frame instanced draws (the by-name indexer is a linear
        //scan, and DrawPalms/DrawTropicalRocks run them once per draw).
        private EffectParameter _palmViewParam, _palmProjectionParam,
            _palmSunDirectionParam, _palmSunColorParam, _palmZenithParam, _palmHorizonParam,
            _palmDiffuseParam, _palmDappleParam, _palmTimeParam, _palmWindParam,
            _palmSwayStrengthParam, _palmSwaySpeedParam, _palmShadingParam, _palmCameraParam;

        //Real 3D palm geometry on the acacia's path (#202): a few variants at rolled proportions and
        //structural seeds — a bowed trunk under a crown of leafleted fronds (#557) with a skirt
        //of dead ones — each variant its own instanced draw so a grove is a mix, never one shape
        //stamped out. Scatter parameters live in TropicalSceneConfig.Palms.
        private PalmMesh[] _palmMeshes;
        private StaticInstances[] _palmInstances;         //per variant; fronds and wood share the matrices
        private float[] _palmDryness;                     //per variant: how far its crown is towards the dry green

        //Every palm's and waterline rock's figure, for a host keeping a camera out of the grove (#559).
        private PlantFigure[] _palmFigures = Array.Empty<PlantFigure>();
        private PlantFigure[] _tropicalRockFigures = Array.Empty<PlantFigure>();

        //The waterline's rocks: the stone (RockMesh) and its moss cap (a LatheMesh over the same
        //profile family, on its own irregularity phase so the moss edge reads ragged against the
        //stone's own wobble) are two meshes over one instance matrix each, drawn per variant.
        private RockMesh[] _tropicalRockMeshes;
        private LatheMesh[] _tropicalMossMeshes;
        private StaticInstances[] _tropicalRockInstances; //per variant; stone and cap share the matrices

        //The beach's dressing (#445): the low green scrub and sea grass at the tree line, and the driftwood
        //lying at the waterline. Three kinds, each with its own variants and its own instance buckets, drawn
        //through the palm effect exactly as the rocks are.
        private FoliageMesh[] _tropicalScrubMeshes;
        private GrassTuftMesh[] _tropicalTuftMeshes;
        private DeadwoodMesh[] _tropicalDriftMeshes;
        private StaticInstances[] _tropicalScrubInstances;
        private StaticInstances[] _tropicalTuftInstances;
        private StaticInstances[] _tropicalDriftInstances;

        /// <summary>
        /// One variant's placed instances, uploaded once when the beach is planted (#589) — the savanna's
        /// <see cref="ScatterBucket"/> pattern (#451). Until #589 every tropical draw copied its static matrices
        /// into one shared dynamic buffer with a discard, about 1,070 instances over some twenty discards a frame,
        /// and the shadow pass copied the palms and rocks again. <see cref="Buffer"/> is null for an empty variant,
        /// which no draw reaches (they skip a zero <see cref="Count"/>).
        /// </summary>
        private sealed class StaticInstances : IDisposable
        {
            public VertexBuffer Buffer { get; private set; }
            public int Count { get; }

            public StaticInstances(GraphicsDevice device, List<ModelInstance> placed)
            {
                Count = placed.Count;
                if (Count == 0) return;

                Buffer = new VertexBuffer(device, ModelInstance.VertexDeclaration, Count, BufferUsage.WriteOnly);
                Buffer.SetData(placed.ToArray());
            }

            public void Dispose()
            {
                Buffer?.Dispose();
                Buffer = null;
            }
        }

        //Colours, stored from the config so the per-draw DiffuseColor can be set as each part draws.
        private Vector3 _palmFrondColor, _palmFrondDry, _palmTrunkColor, _tropicalStoneColor, _tropicalMossColor;
        private Vector3 _tropicalScrubColor, _tropicalTuftColor, _tropicalDriftColor;

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
        //SavannaSceneConfig; SceneRenderer reads them from _savannaConfig (and TerrainMirror.Savanna uses them too).

        #endregion

        #region Acacia (savanna scene only)

        private readonly Effect _acaciaEffect;

        //Cached effect parameters for the per-frame instanced draw (the by-name indexer is a linear scan).
        private EffectParameter _acaciaViewParam, _acaciaProjectionParam, _acaciaCameraParam,
            _acaciaSunDirectionParam, _acaciaSunColorParam, _acaciaZenithParam, _acaciaHorizonParam,
            _acaciaDiffuseParam, _acaciaDiffuseDryParam, _acaciaDappleParam, _acaciaBarkParam, _acaciaAddedLightParam,
            _acaciaHazeParam;

        //Everything standing on the savanna (#202, #451): the acacias in their four kinds, the bushes, the
        //scrub, the grass tufts, the termite mounds, the kopjes, the fallen trees and the treeline at the
        //horizon — planted by SavannaScatter on the terrain (TerrainMirror.Savanna mirrors the shader's field)
        //and handed back as buckets, one instanced draw each with its instances uploaded once. Real geometry,
        //replacing the flat billboard that read as a paper cutout: a surface of revolution has volume from
        //every angle. Scatter parameters live in SavannaSceneConfig.Acacia and .Dressing.
        private SavannaScatter _savannaScatter;

        //The one dynamic instance buffer left on this path, for the hearth stones alone — they are drawn per
        //FIRE with that fire's light, so their instances are uploaded per draw (SetDataOptions.Discard).
        private DynamicVertexBuffer _acaciaInstanceBuffer;

        //The sun's shadow map (#469, in every terrain scene since #471): rendered by DrawShadowMaps before
        //the scene pass and read by whichever effects include Shadows.fxh. One map, one target, one set of
        //uniforms — what changes per scene is which config asks for it, how the map is fitted and who casts.
        //Every seeded arrangement in every scene is shifted by this (see the constructor's parameter): the
        //savanna's planting, the beach's palms, the city's roofs, the Grid's boards. 0 is what shipped.
        private readonly int _seedOffset;

        //Where the savanna's trails step aside for what is standing on it (#476), built with the planting.
        private TrailWarpField _trailWarp;

        private SunShadowMap _sunShadowMap;
        private bool _shadowsActive;
        private EffectTechnique _acaciaTechnique, _acaciaShadowTechnique;
        private EffectTechnique _palmTechnique, _palmShadowTechnique;

        //Every receiver's five parameters, cached at load (BestPractices.md §1 — the by-name indexer is a
        //linear scan) and pushed in one indexed loop. An array of the struct rather than five fields per
        //effect: with ten terrain shaders reading the map, a named quintet each was the shape that made the
        //savanna's version look like a savanna feature instead of the infrastructure it is.
        private ShadowReceiver[] _shadowReceivers;

        //And the scenes this renderer does NOT own (#471): the city and the neon city, whose config, towers
        //and street shader are all the host's, so GetSceneConfig answers null for them. The host states its
        //own dials and its own fit once at load; everything after that is the same path as the other ten.
        private readonly Dictionary<SceneKind, HostShadowScene> _hostShadowScenes = new();

        /// <summary>A backdrop whose shadow dials and fit come from the host rather than from a
        /// <see cref="SceneConfig"/> this renderer holds — see <see cref="SetHostShadowScene"/>.</summary>
        private readonly record struct HostShadowScene(ShadowConfig Shadows, float GroundY, float Below, float Above);

        //And the SHARED instanced effect's (#470), which is the one push that reaches the island, its drain,
        //the gun, the city and every ball at once — the same argument SceneLights makes for its four. It is
        //apart from the array above because the effect is the CALLER's (each executable loads its own
        //Shaders/InstancedModel and hands it to DrawShadowMaps), so it is registered on the first frame one
        //is seen rather than at load, and re-registered if a different effect ever arrives.
        private Effect _instancedShadowEffect;
        private ShadowReceiver _instancedShadowReceiver;

        //The depth bias in world units, turned into the map's own units each frame off its depth range: about
        //three and a half texels of a 2048 map over 260 units, enough that a plate of foliage lit from above
        //does not stripe itself and small enough that a tuft still shadows its own foot.
        private const float SHADOW_BIAS_UNITS = 0.45f;

        //How high the sun has to stand (its direction's Y) for a map to be worth drawing: lower and every
        //shadow is a streak the length of the map, and at a dome's dusk the sun term is next to nothing.
        private const float SHADOW_MIN_SUN_HEIGHT = 0.08f;

        //Slack on both ends of the fitted box (TryShadowFit), so a terrain figure that is a MEAN rather than a
        //maximum — every one of them is — does not clip a hollow out of the bottom of the map or a crown off
        //the top of it. Ten units is under a texel of depth at any of the ranges in play.
        private const float SHADOW_FIT_MARGIN = 10f;

        //And how far over the island's cap the box must reach whatever the ground does: the gun standing on
        //the stone, which in every scene but the savanna, the forest and the beach is the only thing casting
        //at all. Measured off ArenaIsland.TOP_Y rather than off the terrain, because the island's height is
        //the island's and no scene's.
        private const float SHADOW_ISLAND_HEADROOM = 12f;

        private float _shadowScale = 1f;

        /// <summary>
        /// A map size to build instead of the scene's <c>ShadowConfig.MapSize</c>, or 0 for the scene's own (#484). The
        /// Testbed's <c>shadowmap=</c> dial, so 2048 and 4096 can be photographed and paired in one process: the map
        /// is rebuilt whenever the size it was built at differs, which is what makes the dial alternable.
        /// </summary>
        public int ShadowMapSizeOverride { get; set; }

        /// <summary>
        /// The most texels a side the map may have, or 0 for no cap (#484): the Game's quality tier, which holds a
        /// scene's <c>ShadowConfig.MapSize</c> (4096) under 2048 below High. A cap rather than a tier's own size
        /// because a tier only ever takes away (#298's rule) — a scene authored small must not come out larger on a
        /// lower rung. <see cref="ShadowMapSizeOverride"/> wins over it: the instrument pins, the tier limits.
        /// </summary>
        public int ShadowMapSizeCap { get; set; }

        /// <summary>
        /// What a scene's <c>ShadowConfig.MapSize</c> is multiplied by before <see cref="ShadowMapSizeCap"/>, 1 by
        /// default: the Game's <c>Ultra</c> tier writes 2, the one rung that builds a map LARGER than the authored
        /// 4096 (#484) — 8192, a 537 MB map. A factor rather than a size so a scene authored small stays
        /// proportionally small. <see cref="ShadowMapSizeOverride"/> wins over it as it does over the cap.
        /// </summary>
        public int ShadowMapSizeScale { get; set; } = 1;

        /// <summary>
        /// The size a side of the map the last frame actually drew into, or 0 when no map was drawn (a scene
        /// without one, Low, a sun under the horizon). For the Game's <c>[fps]</c> line, since a map's size is
        /// invisible in a still and a tier that sets it can only be believed if the line says what was built.
        /// </summary>
        public int ActiveShadowMapSize => _shadowsActive && _sunShadowMap != null ? _sunShadowMap.Size : 0;

        /// <summary>
        /// A global multiplier over every scene's <see cref="ShadowConfig.Strength"/>, clamped to 0..1.
        /// <b>0 means exactly what a <c>Strength</c> of 0 means</b> — no target, no caster pass, every
        /// receiver handed 0 and skipping its nine taps — so the two spellings of "no shadows" are one code
        /// path and cannot drift apart.
        /// <para>
        /// <b>It exists to be swept, and the sweep is the point.</b> Until it there was no way to measure or
        /// photograph a shadow map against its own absence <i>inside one process</i>: #469, #470 and #471
        /// each had to build a worktree of <c>main</c> and run two executables, which is the setup that
        /// produces a capture pair differing in more than the thing under test — #476 lost a round to exactly
        /// that when a uniform was pushed on a path that had not run yet, and both halves of its "on/off"
        /// comparison were off. With this, <c>alt=shadow=0;shadow=1</c> in the Testbed gives paired windows
        /// on one camera, one scene seed and one build.
        /// </para>
        /// <para>
        /// <b><c>SceneDetail</c> 0 is not a substitute</b>, although it does skip the map: it switches several
        /// scenes to a reduced program at the same time, so a pair taken across it measures a mixture and
        /// says nothing about the shadow.
        /// </para>
        /// <para>
        /// A fraction between is a look dial rather than a measurement one — it dims the shadow without
        /// changing what is drawn or how many taps are paid, so it costs the same as 1 and is useful only for
        /// judging how dark a full shadow should read. <b>The shipped value is 1</b>; nothing in the Game
        /// writes this, and it is not a quality tier (a tier drops effects, and the tier's own lever on
        /// shadows is <c>SceneDetail</c>).
        /// </para>
        /// </summary>
        public float ShadowScale
        {
            get => _shadowScale;
            set => _shadowScale = MathHelper.Clamp(value, 0f, 1f);
        }

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

        //Sub-flames per fire (#481), matching Flame.fx's own SUBFLAME_COUNT exactly: separate camera-facing
        //quads rather than one, so a fire has a silhouette that parallaxes as the view orbits it instead of
        //flipping between "fire" and "a picture of a fire" the way one quad always does. The buffer built
        //below is FLAME_SUBFLAME_COUNT quads laid end to end, X of each vertex's Position carrying which one
        //it belongs to - the shader indexes its own offset/scale/seed tables with it.
        private const int FLAME_SUBFLAME_COUNT = 3;

        //The sparks over each fire (#468): one shared buffer of MAX_SPARKS billboards, the fire's own
        //technique in Flame.fx animating them off the wall clock, drawn per fire after its flame.
        private const int MAX_SPARKS = 32;
        private VertexBuffer _sparkVertexBuffer;
        private IndexBuffer _sparkIndexBuffer;
        private EffectTechnique _flameTechnique, _sparkTechnique;

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

            return new Vector3(x, SavannaGroundHeight(x, z) + _savannaConfig.Campfire.HeightAboveTerrain, z);
        }

        /// <summary>The campfire point-light range (quadratic distance falloff), shared by every fire.</summary>
        public float SavannaCampfireRange => _savannaConfig.Campfire.Range;

        /// <summary>
        /// Everything planted on the savanna — the footprints, the acacias' and the baobabs' figures — for a
        /// host that points a camera at them or keeps one out of them (the chapter intro's prologue, #559).
        /// Null until the scatter has been built.
        /// </summary>
        public SavannaScatter SavannaPlanting => _savannaScatter;

        /// <summary>
        /// The savanna's ground height at a world point, for a host laying a camera path over the plain
        /// (#559): <see cref="TerrainMirror.Savanna"/>, the mirror the planting stands on, on the live config.
        /// </summary>
        public float SavannaGroundHeight(float x, float z) => TerrainMirror.Savanna(x, z, _savannaConfig);

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

        //The flock and its draw, a service since #580 (Render/Scenes/BirdFlock.cs)
        private readonly BirdFlock _birds;

        //One camera-facing quad, its Data carrying (u, v, a per-particle random). The campfire flame, the
        //mountain's snow and the sea's spray are drawn this way; the birds were too, until #235 made them
        //real geometry and they stopped being anything a quad could hold.
        internal struct BillboardVertex : IVertexType
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

        #region Snow (shared by the mountain and the aurora)

        //The flake buffer and its draw, shared with the aurora's backdrop through BackdropServices (#580)
        private readonly Snowfall _snowfall;

        #endregion

        #region The backdrops (#580) and what they share

        //The scenes that are their own Backdrop class (Render/Scenes), indexed by SceneKind; null for a scene
        //still drawn by this class's own switch arms. docs/scenes.md, "The backdrop classes", has the order the
        //rest are to follow in.
        private readonly Backdrop[] _backdrops = new Backdrop[SceneCatalog.Count];
        private readonly SeaBackdrop _sea;
        private readonly SpaceBackdrop _space;
        private readonly DreamBackdrop _dream;
        private readonly CavernBackdrop _cavern;
        private readonly StormBackdrop _storm;
        private readonly GridBackdrop _grid;
        private readonly MoonBackdrop _moon;
        private readonly OutbackBackdrop _outback;
        private readonly DesertBackdrop _desert;
        private readonly PolarBackdrop _polar;
        private readonly AuroraBackdrop _aurora;
        private readonly MarsBackdrop _mars;
        private readonly MountainBackdrop _mountain;
        private readonly MeadowBackdrop _meadow;
        private readonly ForestBackdrop _forest;
        private readonly VolcanoBackdrop _volcano;

        //The device, the full-screen quad, the supersampling factor, the seed offset, the billboard index
        //builder, the terrain grid cache and the island's hole radius, handed to every backdrop.
        private readonly BackdropServices _services;

        //No terrain grid: space replaces the SKY, not the ground, so the whole scene is ONE full-screen pass
        //over a quad already in normalized device coordinates, with the view ray recovered per pixel through
        //the inverse view-projection. Four corners drawn as a triangle strip, built once. Space built it and
        //every sky-replacing pass draws over it (BackdropServices.FullScreenQuad), this class's own included.
        private readonly VertexBuffer _fullScreenQuad;

        //The back-buffer-sized target the cavern and the dream are shaded into before being scaled up into the
        //caller's supersampled one, and the batch that scales them. Built on first use and rebuilt only when
        //the back buffer changes size — never per frame. See DrawBackdropAtDisplayResolution.
        private RenderTarget2D _backdropTarget;
        private SpriteBatch _backdropBatch;

        private Backdrop BackdropFor(SceneKind kind) => _backdrops[(int)kind];

        #endregion

        /// <param name="content">
        /// A content manager whose root holds the scene shaders under <c>Shaders/</c> (both executables build
        /// <c>Sea.fx</c>, <c>Savanna.fx</c>, <c>Birds.fx</c>, <c>Mountain.fx</c>, <c>Snow.fx</c>, <c>Spray.fx</c>, <c>Meadow.fx</c>
        /// out of the Testbed content directory).
        /// </param>
        /// <param name="seedOffset">
        /// Shifts every seeded arrangement in every scene (#: the owner's "let it look different each time").
        /// <b>0 is the arrangement that shipped</b>, to the plant — which is what makes a capture or a
        /// measurement reproducible at all once the default is random: pin it and you are looking at the scene
        /// everything before this was photographed against.
        /// <para>
        /// It is an OFFSET and not a seed, deliberately. Each generator keeps its own constant and adds this,
        /// so the savanna's planting and the palms' clumping stay as unlike each other as they were authored
        /// to be; one shared seed would have made every scene re-roll from the same number and quietly
        /// correlate arrangements that have nothing to do with each other.
        /// </para>
        /// </param>
        public SceneRenderer(GraphicsDevice graphicsDevice, ContentManager content, int seedOffset = 0)
        {
            _seedOffset = seedOffset;

            _graphicsDevice = graphicsDevice;
            _gridCache = new TerrainGridCache(graphicsDevice);

            //--- The full-screen quad every sky-replacing pass draws over (#580: space's until it became a
            //Backdrop) - already in normalized device coordinates, so nothing transforms it
            VertexPosition[] corners =
            {
                new(new Vector3(-1f, 1f, 0f)),
                new(new Vector3(1f, 1f, 0f)),
                new(new Vector3(-1f, -1f, 0f)),
                new(new Vector3(1f, -1f, 0f))
            };
            _fullScreenQuad = new VertexBuffer(graphicsDevice, VertexPosition.VertexDeclaration, corners.Length, BufferUsage.WriteOnly);
            _fullScreenQuad.SetData(corners);

            //--- The far field (#551): one ring every open-ground scene draws its land over past its own grid.
            _farField = new FarField(graphicsDevice);
            _services = new BackdropServices(graphicsDevice, _fullScreenQuad, seedOffset, _gridCache, _farField);

            //--- Sea and its spray: its own Backdrop since #580 (Render/Scenes), built here where the sea's code
            //stood (the spray, built after the snow until then, is built with it)
            _sea = new SeaBackdrop(_services, content);
            _backdrops[(int)SceneKind.Sea] = _sea;

            //--- Desert: its own Backdrop since #580 (Render/Scenes), built here where
            //its code stood
            _desert = new DesertBackdrop(_services, content);
            _backdrops[(int)SceneKind.Desert] = _desert;

            //--- Polar (#222): its own Backdrop since #580 (Render/Scenes), built here where
            //its code stood
            _polar = new PolarBackdrop(_services, content);
            _backdrops[(int)SceneKind.Polar] = _polar;

            //--- Outback (#112): its own Backdrop since #580 (Render/Scenes), built here where
            //its code stood
            _outback = new OutbackBackdrop(_services, content);
            _backdrops[(int)SceneKind.Outback] = _outback;

            //--- Tropical (#244): the fourteenth scene — a beach ring around the island, a turquoise lagoon
            //and the green far shore that closes the horizon. The land grid is the desert's density (the
            //gentlest slopes of any terrain scene); the lagoon's water is the sea's own shader and grid,
            //drawn over this terrain by DrawTropicalWater below through a clone of its own (#580).
            _tropicalEffect = content.Load<Effect>("Shaders/Tropical");
            AcquireGridMesh(TROPICAL_GRID_N, TROPICAL_EXTENT, out _tropicalVertexBuffer, out _tropicalIndexBuffer, out _tropicalIndexCount);

            ApplyTropicalParameters();

            //The lagoon: the sea's grid under its own clone of Sea.fx (#580), its water pushed once
            _lagoonEffect = content.Load<Effect>("Shaders/Sea").Clone();
            AcquireGridMesh(SeaBackdrop.SEA_GRID_N, SeaBackdrop.SEA_EXTENT, out _lagoonVertexBuffer, out _lagoonIndexBuffer, out _lagoonIndexCount);
            ApplyLagoonParameters();

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
            _palmShadingParam = _palmEffect.Parameters["PalmShading"];
            _palmCameraParam = _palmEffect.Parameters["CameraPosition"];
            PushPalmMaterial();
            _palmTechnique = _palmEffect.Techniques["Palm"];
            _palmShadowTechnique = _palmEffect.Techniques["ShadowCaster"];
            BuildTropicalBuffers();

            //--- Volcano (#223, #509): its own Backdrop since #580 (Render/Scenes), built here where its code stood
            _volcano = new VolcanoBackdrop(_services, content);
            _backdrops[(int)SceneKind.Volcano] = _volcano;

            //--- Mars (#277): its own Backdrop since #580 (Render/Scenes), built here where its code stood;
            //it picks its ground's program at load, so it is handed the tier the renderer starts at
            _mars = new MarsBackdrop(_services, content, _sceneDetail);
            _backdrops[(int)SceneKind.Mars] = _mars;

            //--- Storm (#219): the seventeenth scene, its own Backdrop since #580 (Render/Scenes), built here
            //where its code stood
            _storm = new StormBackdrop(_services, content);
            _backdrops[(int)SceneKind.Storm] = _storm;

            //--- Savanna: a flat lattice the shader displaces into gentle grassland (per-pixel normal, no grid)
            _savannaEffect = content.Load<Effect>("Shaders/Savanna");
            AcquireGridMesh(SAVANNA_GRID_N, SAVANNA_EXTENT, out _savannaVertexBuffer, out _savannaIndexBuffer, out _savannaIndexCount);

            ApplySavannaParameters();

            //--- Acacia: everything planted on the savanna, positioned on the ground (TerrainMirror.Savanna
            //mirrors the shader's field) and drawn as instanced geometry in Acacia.fx
            _acaciaEffect = content.Load<Effect>("Shaders/Acacia");
            _acaciaViewParam = _acaciaEffect.Parameters["View"];
            _acaciaProjectionParam = _acaciaEffect.Parameters["Projection"];
            _acaciaCameraParam = _acaciaEffect.Parameters["CameraPosition"];
            _acaciaSunDirectionParam = _acaciaEffect.Parameters["SunDirection"];
            _acaciaSunColorParam = _acaciaEffect.Parameters["SunColor"];
            _acaciaZenithParam = _acaciaEffect.Parameters["ZenithColor"];
            _acaciaHorizonParam = _acaciaEffect.Parameters["HorizonColor"];
            _acaciaDiffuseParam = _acaciaEffect.Parameters["DiffuseColor"];
            _acaciaDiffuseDryParam = _acaciaEffect.Parameters["DiffuseDry"];
            _acaciaDappleParam = _acaciaEffect.Parameters["DappleStrength"];
            _acaciaBarkParam = _acaciaEffect.Parameters["BarkStrength"];
            _acaciaAddedLightParam = _acaciaEffect.Parameters["AddedLight"];
            _acaciaHazeParam = _acaciaEffect.Parameters["HorizonHazeDistance"];
            _acaciaTechnique = _acaciaEffect.Techniques["Acacia"];
            _acaciaShadowTechnique = _acaciaEffect.Techniques["ShadowCaster"];
            ApplyAcaciaParameters();
            BuildSavannaScatter();
            BuildHearthStones();

            //--- Campfire flame: FLAME_SUBFLAME_COUNT billboards per fire (#481), one quad each, laid end to
            //end in a single buffer - Position.X of every vertex of a quad carries which sub-flame it is,
            //which is all Flame.fx needs to look up that sub-flame's own offset/scale/seed.
            _flameEffect = content.Load<Effect>("Shaders/Flame");
            BillboardVertex[] flameVertices = new BillboardVertex[FLAME_SUBFLAME_COUNT * 4];

            for (int sub = 0; sub < FLAME_SUBFLAME_COUNT; sub++)
            {
                int v = sub * 4;
                Vector3 subIndex = new(sub, 0f, 0f);
                flameVertices[v + 0] = new(subIndex, new Vector3(-1f, 0f, 0f));
                flameVertices[v + 1] = new(subIndex, new Vector3(1f, 0f, 0f));
                flameVertices[v + 2] = new(subIndex, new Vector3(-1f, 1f, 0f));
                flameVertices[v + 3] = new(subIndex, new Vector3(1f, 1f, 0f));
            }

            _flameVertexBuffer = new VertexBuffer(graphicsDevice, BillboardVertex.Declaration, flameVertices.Length, BufferUsage.WriteOnly);
            _flameVertexBuffer.SetData(flameVertices);
            _flameIndexBuffer = _services.BuildQuadIndexBuffer(FLAME_SUBFLAME_COUNT, mirrored: true);
            _flameTechnique = _flameEffect.Techniques["Flame"];
            _sparkTechnique = _flameEffect.Techniques["Sparks"];
            //The sparks (#468): one shared buffer of billboards on the fountain's pattern, each fire drawing
            //the first SparkCount of them with its own position and clock.
            BuildBillboardParticles(MAX_SPARKS, 4680, ref _sparkVertexBuffer, ref _sparkIndexBuffer);

            //--- Birds: one shared rest-pose mesh, each bird's orbit and flap cycle seeded once, and a
            //service since #580 (BirdFlock). Sized to the largest flock any of the four scenes asks for:
            //the scenes share it, and a smaller one would silently cap the others'.
            _birds = new BirdFlock(graphicsDevice, content,
                Math.Max(Math.Max(_savannaConfig.Birds.Count, _desert.Birds.Count),
                    Math.Max(_outback.Birds.Count, _tropicalConfig.Birds.Count)));
            _services.Birds = _birds;

            //--- Mountain: its own Backdrop since #580 (Render/Scenes), built here where its code stood
            _mountain = new MountainBackdrop(_services, content);
            _backdrops[(int)SceneKind.Mountain] = _mountain;

            //--- Snow: a static flake buffer, one quad per flake at a fixed point in the unit cube, animated
            //entirely in the shader (built once, never per frame) — shared by the mountain and the aurora
            //since #205, and a service (Snowfall) since #580 so the aurora's backdrop can reach it. The
            //effect is not shared: each scene draws through its own clone, its look pushed once at load.
            VertexBuffer snowVertices = null;
            IndexBuffer snowIndices = null;
            BuildBillboardParticles(_mountain.Snow.FlakeCount, 1207, ref snowVertices, ref snowIndices);
            _snowfall = new Snowfall(_graphicsDevice, snowVertices, snowIndices, _mountain.Snow.FlakeCount);
            _services.Snowfall = _snowfall;

            //--- Meadow: its own Backdrop since #580 (Render/Scenes), built here where its code stood; it picks
            //its program at load, so it is handed the tier the renderer starts at
            _meadow = new MeadowBackdrop(_services, content, _sceneDetail);
            _backdrops[(int)SceneKind.Meadow] = _meadow;

            //--- Forest: its own Backdrop since #580 (Render/Scenes), built here where its code stood; it picks
            //its program at load, so it is handed the tier the renderer starts at
            _forest = new ForestBackdrop(_services, content, _sceneDetail);
            _backdrops[(int)SceneKind.Forest] = _forest;

            //--- Space, the dream and the cavern: the ninth, tenth and eleventh scenes, the three sky-replacing
            //full-screen passes. Each is its own Backdrop since #580 (Render/Scenes), built here where its code
            //stood, so nothing loads or pushes in another order than it did.
            _space = new SpaceBackdrop(_services, content);
            _dream = new DreamBackdrop(_services, content);
            _cavern = new CavernBackdrop(_services, content);
            _backdrops[(int)SceneKind.Space] = _space;
            _backdrops[(int)SceneKind.Dream] = _dream;
            _backdrops[(int)SceneKind.Cavern] = _cavern;

            //--- Moon: the twelfth scene (#125), its own Backdrop since #580 (Render/Scenes), built here where its
            //code stood
            _moon = new MoonBackdrop(_services, content);
            _backdrops[(int)SceneKind.Moon] = _moon;

            //--- Aurora (#205): the eighteenth scene, its own Backdrop since #580 (Render/Scenes), built here
            //where its code stood
            _aurora = new AuroraBackdrop(_services, content);
            _backdrops[(int)SceneKind.Aurora] = _aurora;

            //--- Grid (#393): the twentieth scene, its own Backdrop since #580 (Render/Scenes), built here where
            //its code stood
            _grid = new GridBackdrop(_services, content);
            _backdrops[(int)SceneKind.Grid] = _grid;

            //Last, because it needs every effect above to exist: the one list of everything that reads the
            //sun's shadow map (#471).
            RegisterShadowReceivers();
        }

        /// <summary>
        /// Collects every effect of this renderer's that includes <c>Shadows.fxh</c> into the one array
        /// <see cref="DrawShadowMaps"/> pushes to, and starts them all at strength 0 — no map until a frame
        /// says otherwise, which is what their <c>[branch]</c> skips their nine taps on.
        /// <para>
        /// <b>The list is the feature's inventory</b>: a scene whose terrain shader is missing here draws no
        /// shadow however loudly its config asks for one, and a shader that includes the header but is not
        /// listed reads an unbound texture at whatever strength was last pushed to it. The shared instanced
        /// effect is deliberately not here — it is the caller's, and registers itself on the first frame it
        /// is handed in. A backdrop's receivers are its own to state (<see cref="Backdrop.ShadowReceivers"/>,
        /// #580) and are added to this list, so the inventory is the renderer's remaining scenes plus the union
        /// of the backdrops'.
        /// </para>
        /// <para>
        /// Sea and Storm are absent on purpose. The storm draws no ground at all (<c>StormClouds.fx</c> is the
        /// cloud itself, and the island in it stands on nothing a shadow could land on), and the sea is water:
        /// a shadow inside its Fresnel, foam and subsurface terms is a look decision of its own rather than a
        /// line, and one nobody has asked for. The six sky-replacing scenes have no sun over the horizon and
        /// the gate already skips them.
        /// </para>
        /// </summary>
        private void RegisterShadowReceivers()
        {
            List<Effect> effects = new()
            {
                _savannaEffect, _acaciaEffect,       //#469's two: the plain and what stands on it
                _tropicalEffect, _palmEffect,        //the sand and the palms standing on it
            };

            //...and every backdrop's own, stated beside its fit (#580)
            foreach (Backdrop backdrop in _backdrops)
                if (backdrop != null) effects.AddRange(backdrop.ShadowReceivers);

            _shadowReceivers = new ShadowReceiver[effects.Count];
            for (int i = 0; i < effects.Count; i++)
            {
                _shadowReceivers[i] = new ShadowReceiver(effects[i]);
                _shadowReceivers[i].Disable();
            }
        }

        /// <summary>
        /// Gives a <b>host-owned</b> backdrop a sun shadow map (#471): its dials, where its ground sits and how
        /// far the map's box must reach below and above it, plus whatever of the host's effects receive. Called
        /// once at load, never per frame.
        /// <para>
        /// <b>The city is why this exists and is the only caller today.</b> It is the one backdrop this
        /// renderer does not own — <see cref="GetSceneConfig"/> answers <c>null</c> for it, its towers are the
        /// host's <see cref="InstancedModelRenderer"/>s and <c>CityStreets.fx</c> is loaded by the host — so
        /// all three parts of a shadow live outside this file. Everything after the registration is the same
        /// path the other ten take: the same map, the same fit, the same nine taps.
        /// </para>
        /// <para>
        /// The casters are still the host's own business, through <c>DrawShadowMaps</c>'s <c>extraCasters</c>,
        /// exactly as the island, the gun and the forest's wood already are.
        /// </para>
        /// </summary>
        /// <param name="scene">The backdrop. Called once per kind — the city and the neon city are two.</param>
        /// <param name="shadows">Its dials. <see cref="ShadowConfig.Strength"/> 0 leaves it without a map.</param>
        /// <param name="groundY">Where its ground sits in world Y — the street level, for the city.</param>
        /// <param name="below">How far under that the map's box must reach.</param>
        /// <param name="above">And how far over it: the tallest thing that casts, which for the city is a
        /// tower.</param>
        /// <param name="receivers">The host's own effects that include <c>Shadows.fxh</c>.</param>
        public void SetHostShadowScene(SceneKind scene, ShadowConfig shadows, float groundY, float below,
            float above, params Effect[] receivers)
        {
            _hostShadowScenes[scene] = new HostShadowScene(shadows, groundY, below, above);

            if (receivers == null || receivers.Length == 0) return;

            //Appended rather than rebuilt: the load-time list is this renderer's own inventory and a host's
            //effects join it. Two registrations of the same scene (the city and the neon city share a street
            //shader) would otherwise push the same effect twice a frame, which is harmless but is a lie about
            //what the array is, so an effect already in it is skipped.
            for (int i = 0; i < receivers.Length; i++)
            {
                if (receivers[i] == null) continue;

                ShadowReceiver receiver = new(receivers[i]);
                if (!receiver.IsValid || Registered(receivers[i])) continue;

                Array.Resize(ref _shadowReceivers, _shadowReceivers.Length + 1);
                _shadowReceivers[^1] = receiver;
                _shadowReceivers[^1].Disable();
                _hostReceiverEffects.Add(receivers[i]);
            }

            bool Registered(Effect effect) => _hostReceiverEffects.Contains(effect);
        }

        private readonly List<Effect> _hostReceiverEffects = new();

        //The questions about a SceneKind that are facts of the KIND are answered by SceneCatalog since #580, one
        //table with a row per member instead of five parallel ones here. These forwarders keep every existing
        //caller compiling unchanged; new code may ask the catalog directly.

        /// <summary>Forwards to <see cref="SceneCatalog.ReplacesSky"/>, where the classification and its reasoning live.</summary>
        public static bool ReplacesSky(SceneKind kind) => SceneCatalog.ReplacesSky(kind);

        /// <summary>Forwards to <see cref="SceneCatalog.IsSolidTerrainScene"/>, where the classification and its reasoning live.</summary>
        public static bool IsSolidTerrainScene(SceneKind kind) => SceneCatalog.IsSolidTerrainScene(kind);

        /// <summary>Forwards to <see cref="SceneCatalog.OpenBelow"/>: the complement of <see cref="IsSolidTerrainScene"/>.</summary>
        public static bool OpenBelow(SceneKind kind) => SceneCatalog.OpenBelow(kind);

        /// <summary>How many <see cref="SceneKind"/>s there are; forwards to <see cref="SceneCatalog.Count"/>.</summary>
        public static int SceneCount => SceneCatalog.Count;

        /// <summary>The next scene in the enum, wrapping; forwards to <see cref="SceneCatalog.NextScene"/>.</summary>
        public static SceneKind NextScene(SceneKind kind) => SceneCatalog.NextScene(kind);

        /// <summary>The scene's display name; forwards to <see cref="SceneCatalog.DisplayName"/>.</summary>
        public static string SceneName(SceneKind kind) => SceneCatalog.DisplayName(kind);

        /// <summary>Parses a <c>scene=</c> spelling; forwards to <see cref="SceneCatalog.TryParse"/>.</summary>
        public static bool TryParseScene(string name, out SceneKind kind) => SceneCatalog.TryParse(name, out kind);

        /// <summary>Whether a scene's own light rig moves with time; forwards to <see cref="SceneCatalog.AnimatesLightRig"/>.</summary>
        public static bool AnimatesLightRig(SceneKind kind) => SceneCatalog.AnimatesLightRig(kind);

        #region Scene parameters (each config pushed to its effect and buffers; issue #32, #44)

        /// <summary>
        /// The sun a scene states for itself, overriding both the dome's and the shared domeless one, and false
        /// for every scene that takes one of those. Only the Moon does (#508): its sun stands LOW, because the
        /// Apollo photograph is long black shadows off every rock and a black crescent in every crater, and the
        /// 35 degrees every domeless scene shares draws those as slivers. <see cref="SkyLightRig"/> asks this
        /// after choosing between the dome's sun and the domeless one, so the island, the cluster, the terrain
        /// and the Earth's phase all answer the one direction.
        /// </summary>
        public bool TryGetSunDirection(SceneKind kind, out Vector3 direction)
        {
            if (BackdropFor(kind) is { } backdrop) return backdrop.TryGetSunDirection(out direction);

            direction = default;
            return false;
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
        /// <para>
        /// <b>One of them moves: the aurora's</b> (#462 — <see cref="AnimatesLightRig"/>), which takes the
        /// hue of its own sky, so <paramref name="wallClock"/> is read there and nowhere else. Every other rig
        /// is a constant of its config and ignores it.
        /// </para>
        /// </summary>
        /// <param name="kind">The scene asked about.</param>
        /// <param name="wallClock">Wall-clock seconds, the clock <see cref="AuroraGlowColor"/> and the scene's
        /// own sky run on — only an animated rig reads it.</param>
        /// <param name="rig">The scene's own rig, when it states one.</param>
        public bool TryGetLightRig(SceneKind kind, float wallClock, out SceneLightRig rig)
        {
            if (BackdropFor(kind) is { } backdrop) return backdrop.TryGetLightRig(wallClock, out rig);

            rig = default;
            return false;
        }

        /// <summary>
        /// Somewhere in this scene worth looking at, and how to stand to see it (#289) — the answer to "show
        /// the player the PLACE", which <c>ChapterIntro</c> asks once at the top of every new chapter.
        /// <para>
        /// <b>Every scene answers, and that is the point of the table rather than an accident of it.</b> The
        /// establishing shot it feeds used to look across the island's far rim on a rolled bearing whatever
        /// the backdrop was, which is a fair shot of a sea and a poor one of a volcano — the cone was as
        /// likely to be behind the camera as in front of it. Half of these scenes have a real landmark and
        /// name it (the crater, the campfire, Phobos, the planet); the other half are the same in every
        /// direction, and for those the honest answer is not "no viewpoint" but "any bearing, at THIS
        /// distance and THIS height" — a meadow wants a low, close look at the flowers and the mountains a
        /// far, raised one at the peaks, and getting that wrong is most of what made the old shot generic.
        /// </para>
        /// <para>
        /// <paramref name="bearing"/> is the caller's own roll, used by the scenes with nothing fixed to
        /// point at and ignored by the ones that have. Figures come off each scene's config wherever the
        /// scene has one, so a feature moved in code moves the viewpoint with it instead of leaving it
        /// pointing where the feature used to be.
        /// </para>
        /// </summary>
        /// <returns>Always true today. It is a Try so that a scene added without a viewpoint is a shot that
        /// falls back to the old generic sweep rather than one that throws or frames the void — the same
        /// safe-default reasoning <c>BallKinds.Removable</c> states for its own "everything but" default.</returns>
        public bool TryGetViewpoint(SceneKind kind, float bearing, out SceneViewpoint viewpoint)
        {
            if (BackdropFor(kind) is { } backdrop) return backdrop.TryGetViewpoint(bearing, out viewpoint);

            switch (kind)
            {
                //THE TWO CITIES LOOK DOWN, and they are the only ones here that do. Everything else in this
                //table is on or above the horizon; the city's own subject is the one thing under it — the
                //arena hangs over a canyon whose towers rise a hundred units out of the street, which is the
                //single fact about this backdrop a player standing on the island cannot see. The shot looks at
                //that street (CitySceneConfig.BaseY, -100 since #399; it looked 50 units under it at -150 while
                //the towers still ran down to -420). Stated in figures rather than read off a config because the
                //city is the CALLER's: this class draws none of it and holds no CitySceneConfig (see
                //ApplyConfig's own case for it).
                case SceneKind.City:
                    viewpoint = new SceneViewpoint(AtBearing(bearing, 55f, -100f), 2.0f, 34f, 135f, "the canyon");
                    return true;

                //The neon city is that same canyon after dark, and what is worth the look is the LIT
                //roofline at the top of the shaft rather than the drop down it — a dark shaft photographs as
                //nothing at night, and the windows are the whole of what this variant is.
                case SceneKind.NeonCity:
                    viewpoint = new SceneViewpoint(AtBearing(bearing, 100f, 24f), 2.0f, 10f, 160f, "the neon roofline");
                    return true;

                //A real landmark, and the only one in this table that is also a LIGHT: the fire is what the
                //savanna's night rig is built around, so a shot that has it has the scene's whole character
                //in frame. Slot 0 of however many the config asks for.
                case SceneKind.Savanna:
                    viewpoint = new SceneViewpoint(SavannaCampfirePosition(0), 1.7f, 11f, 35f, "the campfire");
                    return true;

                //Out over the lagoon to the far shore's ring: the tropical scene is three bands — sand,
                //turquoise water, green shore — and a look across all three is what it is.
                //
                //⚠ OVER THE PALM TOPS, NOT AMONG THEM (#555). This stood at 8 degrees while the palms were
                //12 units tall, which put the lens a little above their crowns. At 22 units the crowns reach
                //some twenty over the sand, right where an 8-degree lens stands 2.1 stand-offs out — in the
                //middle of the palm ring — so the tour opened inside a grove. 20 degrees rides over the
                //tallest crown at that radius and keeps all three bands, with the palm tops in the foreground.
                case SceneKind.Tropical:
                    viewpoint = new SceneViewpoint(
                        AtBearing(bearing, _tropicalConfig.Terrain.RingRadius, _tropicalConfig.Water.LevelY + 6f),
                        2.1f, 20f, 0f, "the lagoon");
                    return true;

                default:
                    viewpoint = default;
                    return false;
            }
        }

        //A point out from the arena on a bearing, at a height. The arena is at the world origin, so this is
        //the whole of the conversion — see SceneViewpoint's own remarks.
        internal static Vector3 AtBearing(float bearing, float radius, float y) =>
            new(MathF.Cos(bearing) * radius, y, MathF.Sin(bearing) * radius);

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
            if (kind == SceneKind.Space) return _space.TryGetPlanetshine(out position, out color, out range);

            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;
            return false;
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
            if (kind == SceneKind.Moon) return _moon.TryGetEarthshine(out position, out color, out range);

            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;
            return false;
        }

        /// <summary>
        /// The active configuration of one of the self-lit scenes — what a level saves as its scene. Returns
        /// null for <see cref="SceneKind.City"/>/<see cref="SceneKind.NeonCity"/>, whose config lives outside
        /// the renderer (the caller owns the <see cref="CitySceneConfig"/>).
        /// </summary>
        public SceneConfig GetSceneConfig(SceneKind kind) => BackdropFor(kind)?.Config ?? kind switch
        {
            SceneKind.Savanna => _savannaConfig,
            SceneKind.Tropical => _tropicalConfig,
            _ => null,
        };

        /// <summary>
        /// Pushes the tropical lagoon's water into its own clone of <c>Sea.fx</c>, once at load (#580) — the
        /// same set <see cref="SeaBackdrop.ApplySeaParameters"/> pushes into the sea's, off <see cref="TropicalWaterConfig"/>.
        /// </summary>
        private void ApplyLagoonParameters()
        {
            TropicalWaterConfig water = _tropicalConfig.Water;

            _lagoonEffect.Parameters["SeaLevelY"].SetValue(water.LevelY);
            _lagoonEffect.Parameters["WaterColorDeep"].SetValue(water.WaterDeep.ToVector3());
            _lagoonEffect.Parameters["WaterColorShallow"].SetValue(water.WaterShallow.ToVector3());
            _lagoonEffect.Parameters["ShallowBias"].SetValue(water.ShallowBias);
            _lagoonEffect.Parameters["WaveAmplitude"].SetValue(water.WaveAmplitude);
            _lagoonEffect.Parameters["WaveSteepness"].SetValue(water.WaveSteepness);
            _lagoonEffect.Parameters["WaveSpeed"].SetValue(water.WaveSpeed);
            _lagoonEffect.Parameters["WaveFadeStart"].SetValue(water.WaveFadeStart);
            _lagoonEffect.Parameters["WaveFadeEnd"].SetValue(water.WaveFadeEnd);
            _lagoonEffect.Parameters["ChopAmplitude"].SetValue(water.ChopAmplitude);
            _lagoonEffect.Parameters["ChopFrequency"].SetValue(water.ChopFrequency);
            _lagoonEffect.Parameters["ChopSpeed"].SetValue(water.ChopSpeed);
            _lagoonEffect.Parameters["WindDirection"].SetValue(water.Wind.ToVector2());
            _lagoonEffect.Parameters["SunGlintStrength"].SetValue(water.SunGlintStrength);
            _lagoonEffect.Parameters["SunGlintPower"].SetValue(water.SunGlintPower);
            _lagoonEffect.Parameters["FoamJacobianThreshold"].SetValue(water.FoamJacobianThreshold);
            _lagoonEffect.Parameters["FoamStrength"].SetValue(water.FoamStrength);
            _lagoonEffect.Parameters["FoamCrestStart"].SetValue(water.FoamCrestStart);
            _lagoonEffect.Parameters["FoamCrestStrength"].SetValue(water.FoamCrestStrength);
            _lagoonEffect.Parameters["FoamColor"].SetValue(water.FoamColor.ToVector3());
            _lagoonEffect.Parameters["SssStrength"].SetValue(water.SssStrength);
            _lagoonEffect.Parameters["SssColor"].SetValue(water.SssColor.ToVector3());
            _lagoonEffect.Parameters["HorizonHazeDistance"].SetValue(water.HorizonHazeDistance);
        }

        /// <summary>
        /// Pushes the tropical terrain's static tuning into <c>Tropical.fx</c> and stores the scatter's
        /// per-draw colours. The lagoon's water uniforms are <see cref="ApplyLagoonParameters"/>'s, pushed
        /// into the lagoon's own clone of <c>Sea.fx</c> (#580).
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

            PushPalmMaterial();
            _tropicalStoneColor = rocks.StoneColor.ToVector3();
            _tropicalMossColor = rocks.MossColor.ToVector3();

            TropicalDressingConfig dress = _tropicalConfig.Dressing;
            _tropicalScrubColor = dress.ScrubColor.ToVector3();
            _tropicalTuftColor = dress.TuftColor.ToVector3();
            _tropicalDriftColor = dress.DriftColor.ToVector3();
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
        public float StormFlash(float time) => _storm.Flash(time);

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
            if (kind == SceneKind.Storm) return _storm.TryGetFlash(time, out position, out color, out range);

            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;
            return false;
        }

        /// <summary>
        /// A unit direction from elevation above the horizon and azimuth from +Z towards +X, both in
        /// degrees — <see cref="SkyDome"/>'s own <c>SUNS</c> convention (see its <c>DomeNumber</c> setter),
        /// reused here so Mars's moons are placed the same designer-facing way a dome's sun is.
        /// </summary>
        internal static Vector3 DirectionFromElevationAzimuth(float elevationDegrees, float azimuthDegrees)
        {
            float elevation = MathHelper.ToRadians(elevationDegrees);
            float azimuth = MathHelper.ToRadians(azimuthDegrees);
            float horizontal = MathF.Cos(elevation);

            return new Vector3(horizontal * MathF.Sin(azimuth), MathF.Sin(elevation), horizontal * MathF.Cos(azimuth));
        }

        private void ApplySavannaParameters()
        {
            SelectSavannaTechnique();

            _savannaEffect.Parameters["SavannaLevelY"].SetValue(_savannaConfig.LevelY);
            _savannaEffect.Parameters["HillHeight"].SetValue(_savannaConfig.HillHeight);
            _savannaEffect.Parameters["ClearingRadius"].SetValue(_savannaConfig.ClearingRadius);
            _savannaEffect.Parameters["ClearingTransition"].SetValue(_savannaConfig.ClearingTransition);
            _savannaEffect.Parameters["ClearingRelief"].SetValue(_savannaConfig.ClearingRelief);
            _savannaEffect.Parameters["GrassColor"].SetValue(_savannaConfig.GrassSavanna.ToVector3());
            _savannaEffect.Parameters["GrassColorDry"].SetValue(_savannaConfig.GrassDry.ToVector3());
            _savannaEffect.Parameters["GrassColorBare"].SetValue(_savannaConfig.GrassBare.ToVector3());
            _savannaEffect.Parameters["GrassTipColor"].SetValue(_savannaConfig.GrassTipColor.ToVector3());
            _savannaEffect.Parameters["GrassTipStrength"].SetValue(_savannaConfig.GrassTipStrength);
            _savannaEffect.Parameters["TuftSize"].SetValue(_savannaConfig.TuftSize);
            _savannaEffect.Parameters["TuftStrength"].SetValue(_savannaConfig.TuftStrength);
            _savannaEffect.Parameters["GrassSheenStrength"].SetValue(_savannaConfig.GrassSheenStrength);
            _savannaEffect.Parameters["GrassTranslucency"].SetValue(_savannaConfig.GrassTranslucency);
            _savannaEffect.Parameters["AmbientStrength"].SetValue(_savannaConfig.AmbientStrength);
            _savannaEffect.Parameters["WindDirection"].SetValue(_savannaConfig.Wind.ToVector2());
            _savannaEffect.Parameters["HorizonHazeDistance"].SetValue(_savannaConfig.HorizonHazeDistance);
            _savannaEffect.Parameters["WindRippleSpeed"].SetValue(_savannaConfig.WindRippleSpeed);
            _savannaEffect.Parameters["WindRippleFrequency"].SetValue(_savannaConfig.WindRippleFrequency);
            _savannaEffect.Parameters["WindRippleStrength"].SetValue(_savannaConfig.WindRippleStrength);
            _savannaEffect.Parameters["GrassReliefStrength"].SetValue(_savannaConfig.GrassReliefStrength);
            _savannaEffect.Parameters["GrassReliefFrequency"].SetValue(_savannaConfig.GrassReliefFrequency);
            _savannaEffect.Parameters["TrailStrength"].SetValue(_savannaConfig.TrailStrength);
            _savannaEffect.Parameters["TrailWidth"].SetValue(_savannaConfig.TrailWidth);
            _savannaEffect.Parameters["TrailFrequency"].SetValue(_savannaConfig.TrailFrequency);

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
            //The colours are the buckets' own now (SavannaScatter reads the config as it builds them); what
            //the effect takes at config time is the haze distance, so a far plant fades as the ground does.
            _acaciaHazeParam.SetValue(_savannaConfig.HorizonHazeDistance);
        }

        /// <summary>
        /// (Re)builds everything planted on the savanna (<see cref="SavannaScatter"/>): the mesh variants of
        /// every kind and the instance buffers, each thing planted on the terrain — a plant's height comes
        /// off the ground it stands on, so a terrain change re-plants the whole scatter. Deterministic seed,
        /// so the same config always gives the same savanna. The fires and their hearths are handed over as
        /// ground already taken, so nothing lands in a fire.
        /// </summary>
        private void BuildSavannaScatter()
        {
            DisposeAcacia();

            CampfireConfig cf = _savannaConfig.Campfire;
            int fires = SavannaCampfireCount;
            var reserved = new List<ScatterSpacing.Footprint>(fires);
            float hearth = cf.FlameSize * (cf.StoneRingScale + cf.StoneSizeScale) + 1f;
            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 at = SavannaCampfirePosition(fire);
                reserved.Add(new ScatterSpacing.Footprint(at.X, at.Z, hearth));
            }

            _savannaScatter = new SavannaScatter(_graphicsDevice, _savannaConfig, SavannaGroundHeight, reserved,
                SavannaScatter.DEFAULT_SEED + _seedOffset);

            //And where the trails have to go round it (#476): built from the planting that has just been
            //done, so the field and the plants it bends for cannot disagree. Rebuilt with the scatter for
            //the same reason: a field left behind by a planting would send the paths round trees that are no
            //longer there.
            _trailWarp?.Dispose();
            _trailWarp = _savannaConfig.TrailAvoidOffset > 0f
                ? new TrailWarpField(_graphicsDevice, _savannaScatter.Standing,
                    _savannaConfig.TrailAvoidMinRadius, _savannaConfig.TrailAvoidReach,
                    _savannaConfig.TrailAvoidOffset, SAVANNA_EXTENT)
                : null;

            //⚠ And the uniforms are pushed HERE rather than in ApplySavannaParameters, which is where every
            //other savanna dial goes: that method runs BEFORE the planting does, so the texture it pushed
            //was always null and the warp never reached the shader. It cost one capture pair that looked
            //exactly like the feature not working — the paths were identical with it on and off, because
            //it was off both times.
            _savannaEffect.Parameters["TrailWarpTexture"].SetValue(_trailWarp?.Texture);
            _savannaEffect.Parameters["TrailWarpExtent"].SetValue(_trailWarp?.Extent ?? 1f);
            _savannaEffect.Parameters["TrailWarpAmount"].SetValue(_trailWarp == null ? 0f : _trailWarp.MaxOffset);
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
            Random rng = new(28204 + _seedOffset);
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
                    float y = SavannaGroundHeight(x, z) - size * scale * 0.2f;

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
            _savannaScatter?.Dispose();
            _savannaScatter = null;
            _acaciaInstanceBuffer?.Dispose();
            _acaciaInstanceBuffer = null;
        }

        /// <summary>
        /// (Re)builds the tropical scatter: the palm variants and the waterline's rock variants, and each
        /// one's per-variant instance matrices. Palms are planted only on <b>dry</b> sand (a height test
        /// against the water level, which follows the wiggling waterline) and rocks only in the band
        /// straddling it, so a shore edit re-plants the whole scatter — the same contract
        /// <see cref="BuildSavannaScatter"/> holds. Clumped around cluster centres with a few solos, kept
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
            Random rng = new(244 + _seedOffset);

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
            //--- The beach's dressing (#445) ----------------------------------------------------------
            //Every reference of this beach has the same three things the scene had none of: a band of low
            //green scrub and sea grass at the tree line, and driftwood lying on the sand. None of them needs
            //a new mesh - the savanna's scrub foliage, its grass tuft and its fallen log are exactly these
            //things at a different size and colour, which is the whole point of keeping the mesh library
            //game-agnostic.
            TropicalDressingConfig dressing = _tropicalConfig.Dressing;

            const int SCRUB_VARIANTS = 3, TUFT_VARIANTS = 3, DRIFT_VARIANTS = 3;
            _tropicalScrubMeshes = new FoliageMesh[SCRUB_VARIANTS];
            for (int m = 0; m < SCRUB_VARIANTS; m++)
            {
                //⚠ Taller than it is wide is wrong for a savanna bush and right for this one. At the
                //savanna's own proportions (a little over half its radius) a beach bush photographed from
                //above as a flat green puddle lying on the sand, because that is what a wide low dome IS
                //when the camera looks down on it - and the elevated three-quarter view is the one this
                //scene is framed in. Beach scrub grows in rounded clumps; near its own width in height is
                //what makes it read as a clump rather than as paint.
                float r = dressing.ScrubSize * (0.75f + 0.5f * (float)rng.NextDouble());
                float hh = r * (0.75f + 0.35f * (float)rng.NextDouble());
                _tropicalScrubMeshes[m] = new FoliageMesh(_graphicsDevice, r, hh,
                    centreY: hh * 0.8f, seed: 6200 + m, style: FoliageStyle.Scrub);
            }

            _tropicalTuftMeshes = new GrassTuftMesh[TUFT_VARIANTS];
            for (int m = 0; m < TUFT_VARIANTS; m++)
            {
                float r = dressing.TuftSize * (0.8f + 0.4f * (float)rng.NextDouble());
                _tropicalTuftMeshes[m] = new GrassTuftMesh(_graphicsDevice, r,
                    r * (1.3f + 0.6f * (float)rng.NextDouble()), 6230 + m);
            }

            _tropicalDriftMeshes = new DeadwoodMesh[DRIFT_VARIANTS];
            for (int m = 0; m < DRIFT_VARIANTS; m++)
            {
                _tropicalDriftMeshes[m] = new DeadwoodMesh(_graphicsDevice,
                    length: dressing.DriftLength * (0.7f + 0.6f * (float)rng.NextDouble()),
                    radius: dressing.DriftRadius * (0.8f + 0.5f * (float)rng.NextDouble()),
                    seed: 6260 + m);
            }

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
            var palmFigures = new List<PlantFigure>(palms.Count);
            var rockFigures = new List<PlantFigure>(rocks.Count);

            for (int i = 0; i < palms.Count; i++)
            {
                float rand = (float)rng.NextDouble();
                //A per-plant uniform scale around 1, so one variant mesh reads as several palms.
                float sizeScale = 0.75f + 0.5f * rand;
                float halfWidth = palms.FrondLength * sizeScale;

                //Everything that shapes this palm is rolled BEFORE it is placed (#555), because where it may
                //stand depends on where its crown ends up: the variant (whose trunk bows its crown off the
                //axis), the yaw that turns that bow, and the lean. See the orbit test in the loop below.
                int variant = rng.Next(PALM_VARIANTS);
                PalmMesh mesh = _palmMeshes[variant];
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float leanJitter = ((float)rng.NextDouble() - 0.5f) * 1.4f;
                float lean = 0.08f + 0.55f * (float)rng.NextDouble() * (float)rng.NextDouble();

                float x = 0f, z = 0f;
                float bestClearance = float.NegativeInfinity;
                Matrix world = Matrix.Identity;

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
                    if (TerrainMirror.Tropical(cx, cz, _tropicalConfig) < waterY + 1.1f) continue;

                    //Sunk a fraction into the sand, the forest scatter's own figure: a palm planted at the
                    //exact surface reads as standing on a pinhead from anywhere but head-on, and the flare
                    //at the root is what wants burying.
                    Vector3 basePos = new(cx, TerrainMirror.Tropical(cx, cz, _tropicalConfig) - 0.15f, cz);
                    Matrix candidate = PalmWorld(basePos, sizeScale, yaw, lean, leanJitter);

                    //⚠ THE CROWN, NOT THE ROOT, HAS TO CLEAR THE FRONT END'S ORBIT (#555). MinRadius alone was
                    //enough while a palm was 12 units tall; at the heights the owner asked for, the trunk's bow
                    //carries a crown several units off its root, and a root on the ring's inner edge could hang
                    //its crown into the orbit's wide leg. So the test is the crown's own world position less
                    //everything a frond can reach, against the widest orbit plus a unit of air.
                    Vector3 crown = Vector3.Transform(mesh.Crown, candidate);
                    float crownInner = MathF.Sqrt(crown.X * crown.X + crown.Z * crown.Z) - mesh.FrondReach * sizeScale;
                    if (crownInner < palms.OrbitClearance) continue;

                    float clearance = ScatterSpacing.Clearance(cx, cz, halfWidth, standing);

                    if (clearance > bestClearance)
                    {
                        bestClearance = clearance;
                        x = cx;
                        z = cz;
                        world = candidate;
                    }

                    if (clearance >= 0f) break;
                }

                //Never dropped for want of room (the forest's rule) — but if every candidate was in the
                //sea, or hung its crown into the orbit, this palm has nowhere to stand, and standing it in
                //the surf or in the lens's path is the worse bug.
                if (bestClearance == float.NegativeInfinity) continue;

                standing.Add(new ScatterSpacing.Footprint(x, z, halfWidth));
                palmBuckets[variant].Add(new ModelInstance(world, Vector4.Zero));
                //The figure's stem is the chord from the root to the crown; the trunk bows off that chord by a
                //fraction of how far the crown stands off the root, so the stem is widened by that much.
                Vector3 crownAt = Vector3.Transform(mesh.Crown, world);
                float bow = 0.25f * new Vector2(crownAt.X - world.M41, crownAt.Z - world.M43).Length();
                palmFigures.Add(new PlantFigure(world.Translation, crownAt,
                    mesh.FrondReach * sizeScale, palms.TrunkRadius * 1.15f * sizeScale + bow));
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

                    float h = TerrainMirror.Tropical(cx, cz, _tropicalConfig);
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

                Vector3 basePos = new(x, TerrainMirror.Tropical(x, z, _tropicalConfig) - 0.2f, z);

                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float tumble = 0.3f * (float)rng.NextDouble();
                float tumbleDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                Matrix world = Matrix.CreateScale(sizeScale)
                    * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(tumbleDir), 0f, MathF.Sin(tumbleDir)), tumble)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(basePos);

                int rockVariant = rng.Next(ROCK_VARIANTS);
                rockBuckets[rockVariant].Add(new ModelInstance(world, Vector4.Zero));
                rockFigures.Add(PlantFigure.Of(_tropicalRockMeshes[rockVariant].BoundingSphere, world, 0f));
            }

            //--- Planting the dressing (#445) ---------------------------------------------------------
            //All three are scattered AFTER the palms and the rocks, and that ordering is load-bearing for the
            //same reason the aurora's snags record: everything here draws from one rng stream, so anything
            //inserted earlier would re-roll the whole beach behind it.
            //
            //No spacing test and no clearance search: these are small, they are allowed to grow against a
            //trunk and into each other, and a tuft rejected for want of room is a tuft nobody would have
            //missed. What each one IS tested for is the ground it stands on - the scrub and the grass want
            //dry sand above the surf, the driftwood wants the wet band the sea actually throws it onto.
            var scrubBuckets = new List<ModelInstance>[SCRUB_VARIANTS];
            for (int m = 0; m < SCRUB_VARIANTS; m++) scrubBuckets[m] = new List<ModelInstance>();
            var tuftBuckets = new List<ModelInstance>[TUFT_VARIANTS];
            for (int m = 0; m < TUFT_VARIANTS; m++) tuftBuckets[m] = new List<ModelInstance>();
            var driftBuckets = new List<ModelInstance>[DRIFT_VARIANTS];
            for (int m = 0; m < DRIFT_VARIANTS; m++) driftBuckets[m] = new List<ModelInstance>();

            float dressInner = MathF.Max(dressing.MinRadius, 1f);
            float dressOuter = MathF.Max(dressing.MaxRadius, dressInner + 1f);

            for (int i = 0; i < dressing.ScrubCount + dressing.TuftCount; i++)
            {
                bool isScrub = i < dressing.ScrubCount;

                //Clumped the way the palms are, because undergrowth grows in thickets rather than evenly -
                //and around the palms' OWN cluster centres, so the green gathers where the shade is.
                float cx, cz;
                if (rng.NextDouble() < 0.78)
                {
                    int c = rng.Next(palms.Clusters);
                    float off = (float)rng.NextDouble();
                    float d = off * off * palms.ClusterSpread * 1.25f;
                    float da = (float)rng.NextDouble() * MathHelper.TwoPi;
                    cx = clusterX[c] + MathF.Cos(da) * d;
                    cz = clusterZ[c] + MathF.Sin(da) * d;
                }
                else
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float r = dressInner + (float)rng.NextDouble() * (dressOuter - dressInner);
                    cx = MathF.Cos(a) * r;
                    cz = MathF.Sin(a) * r;
                }

                float dist = MathF.Sqrt(cx * cx + cz * cz);
                if (dist < dressInner || dist > dressOuter) continue;

                float gh = TerrainMirror.Tropical(cx, cz, _tropicalConfig);
                if (gh < waterY + 0.35f) continue;   //dry sand only: nothing green grows in the surf

                float size = 0.7f + 0.6f * (float)rng.NextDouble();
                Matrix world = Matrix.CreateScale(size)
                    * Matrix.CreateRotationY((float)rng.NextDouble() * MathHelper.TwoPi)
                    * Matrix.CreateTranslation(new Vector3(cx, gh - 0.08f, cz));

                if (isScrub) scrubBuckets[rng.Next(SCRUB_VARIANTS)].Add(new ModelInstance(world, Vector4.Zero));
                else tuftBuckets[rng.Next(TUFT_VARIANTS)].Add(new ModelInstance(world, Vector4.Zero));
            }

            for (int i = 0; i < dressing.DriftCount; i++)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float r = dressInner + (float)rng.NextDouble() * (dressOuter - dressInner);
                float cx = MathF.Cos(a) * r;
                float cz = MathF.Sin(a) * r;

                //The band the sea throws a log onto and leaves it: from a little under the waterline to a
                //couple of units above, which is the rocks' own band and for the same reason.
                float gh = TerrainMirror.Tropical(cx, cz, _tropicalConfig);
                if (gh < waterY - 0.3f || gh > waterY + 2.2f) continue;

                //A log lies where the last wave left it, so it lies ALONG the waterline more often than
                //across it - the yaw is the tangent, scattered by about 50 degrees either way.
                float tangent = MathF.Atan2(cx, -cz);
                float yaw = tangent + ((float)rng.NextDouble() - 0.5f) * 1.8f;
                float size = 0.8f + 0.5f * (float)rng.NextDouble();

                Matrix world = Matrix.CreateScale(size)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(new Vector3(cx, gh, cz));

                driftBuckets[rng.Next(DRIFT_VARIANTS)].Add(new ModelInstance(world, Vector4.Zero));
            }

            _tropicalScrubInstances = new StaticInstances[SCRUB_VARIANTS];
            for (int m = 0; m < SCRUB_VARIANTS; m++) _tropicalScrubInstances[m] = new StaticInstances(_graphicsDevice, scrubBuckets[m]);
            _tropicalTuftInstances = new StaticInstances[TUFT_VARIANTS];
            for (int m = 0; m < TUFT_VARIANTS; m++) _tropicalTuftInstances[m] = new StaticInstances(_graphicsDevice, tuftBuckets[m]);
            _tropicalDriftInstances = new StaticInstances[DRIFT_VARIANTS];
            for (int m = 0; m < DRIFT_VARIANTS; m++) _tropicalDriftInstances[m] = new StaticInstances(_graphicsDevice, driftBuckets[m]);

            _palmFigures = palmFigures.ToArray();
            _tropicalRockFigures = rockFigures.ToArray();

            _palmInstances = new StaticInstances[PALM_VARIANTS];
            for (int m = 0; m < PALM_VARIANTS; m++) _palmInstances[m] = new StaticInstances(_graphicsDevice, palmBuckets[m]);
            _tropicalRockInstances = new StaticInstances[ROCK_VARIANTS];
            for (int m = 0; m < ROCK_VARIANTS; m++) _tropicalRockInstances[m] = new StaticInstances(_graphicsDevice, rockBuckets[m]);
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
        /// One palm's instance matrix: its uniform size, a free yaw about its own axis, then the lean, then
        /// the root's place on the sand.
        /// <para>
        /// ⚠ <b>THE YAW GOES BEFORE THE LEAN, and until #555 it went after it.</b> Row-vector matrices apply
        /// left to right, so <c>Scale · Lean · Yaw</c> leaned the palm seaward and then spun the leaning
        /// palm about the WORLD's Y axis by a random angle — which threw away the whole seaward bias #445
        /// built, and palms leaned in over the arena as often as out over the water (the elevated capture in
        /// #555's before set shows it plainly). Yawed first, the turn only spins the mesh's own bow and crown
        /// about the trunk, and the lean that follows is the lean stated below.
        /// </para>
        /// <para>
        /// The lean is SEAWARD and large (#445): the silhouette a palm is recognised by is its lean, every
        /// reference leans 20 to 45 degrees, and out over the water, where the light is. A leaning palm reads
        /// as wind-shaped and a tilted one as felled, which is what the bias is for — the axis is the
        /// outward bearing's, scattered by <paramref name="leanJitter"/> (±0.7 rad, about 40 degrees).
        /// Tilting +Y towards the outward unit (ux, uz) means rotating about (uz, 0, -ux): for a right-handed
        /// rotation about A the velocity of Y is A x Y, which for a horizontal A is (-Az, 0, Ax).
        /// </para>
        /// </summary>
        private static Matrix PalmWorld(Vector3 basePos, float sizeScale, float yaw, float lean, float leanJitter)
        {
            float r = MathF.Sqrt(basePos.X * basePos.X + basePos.Z * basePos.Z);
            float ux = r > 1e-3f ? basePos.X / r : 1f;
            float uz = r > 1e-3f ? basePos.Z / r : 0f;
            float leanDir = MathF.Atan2(-ux, uz) + leanJitter;
            return Matrix.CreateScale(sizeScale)
                * Matrix.CreateRotationY(yaw)
                * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(leanDir), 0f, MathF.Sin(leanDir)), lean)
                * Matrix.CreateTranslation(basePos);
        }

        private static void DisposeInstances(StaticInstances[] sets)
        {
            if (sets != null) foreach (StaticInstances set in sets) set.Dispose();
        }

        /// <summary>
        /// Disposes the palm and rock meshes and their instance buffers — called on a rebuild (a
        /// shore or config edit re-plants the scatter) and on the renderer's own <see cref="Dispose"/>.
        /// </summary>
        private void DisposeTropical()
        {
            if (_palmMeshes != null) foreach (PalmMesh mesh in _palmMeshes) mesh?.Dispose();
            if (_tropicalScrubMeshes != null) foreach (FoliageMesh mesh in _tropicalScrubMeshes) mesh?.Dispose();
            if (_tropicalTuftMeshes != null) foreach (GrassTuftMesh mesh in _tropicalTuftMeshes) mesh?.Dispose();
            if (_tropicalDriftMeshes != null) foreach (DeadwoodMesh mesh in _tropicalDriftMeshes) mesh?.Dispose();
            if (_tropicalRockMeshes != null) foreach (RockMesh mesh in _tropicalRockMeshes) mesh?.Dispose();
            if (_tropicalMossMeshes != null) foreach (LatheMesh mesh in _tropicalMossMeshes) mesh?.Dispose();
            DisposeInstances(_palmInstances);
            DisposeInstances(_tropicalRockInstances);
            DisposeInstances(_tropicalScrubInstances);
            DisposeInstances(_tropicalTuftInstances);
            DisposeInstances(_tropicalDriftInstances);
            _palmMeshes = null;
            _tropicalScrubMeshes = null;
            _tropicalTuftMeshes = null;
            _tropicalDriftMeshes = null;
            _tropicalRockMeshes = null;
            _tropicalMossMeshes = null;
        }

        #region The scenes' public queries: the volcano's, the strange scenes' things, the staged events (#580 moved their bodies)

        //The billboard particles are a service since #580 (BackdropServices.BuildBillboardParticles), the volcano's
        //backdrop building its fountains and ash through it; the renderer's own callers keep this name
        private void BuildBillboardParticles(int count, int seed, ref VertexBuffer vertexBuffer, ref IndexBuffer indexBuffer) =>
            _services.BuildBillboardParticles(count, seed, ref vertexBuffer, ref indexBuffer);

        /// <summary>
        /// The volcano's ground height at a world point, for a host laying a camera path over the cone (the
        /// chapter intro's prologue, #530): <see cref="TerrainMirror.Volcano"/> on the live config. The
        /// scoria clinker is missing from it by that mirror's own argument, so a path wants a clearance of a
        /// few units more than the picture suggests.
        /// </summary>
        public float VolcanoGroundHeight(float x, float z) => _volcano.GroundHeight(x, z);

        #region Where the strange scenes' things stand (#559)

        //The chapter intro's prologue (#559) takes each shot of ONE concrete thing a scene builds, and four of
        //the scenes build theirs in a shader: the dream's glass solids and orbs, the cavern's crystal clusters
        //and god rays, the icesheet's crevasses and pressure front. These are the host copies of the shaders'
        //own placement, kept in step by hand exactly as StormCellPosition is with StormClouds.fx — a camera
        //aimed where the shader does not draw frames empty sky. Nothing here runs per frame; the intro asks
        //once when it begins.

        /// <summary>How many glass solids <c>Dream.fx</c> draws (its <c>SHAPE_COUNT</c>).</summary>
        public const int DREAM_SOLID_COUNT = 8;

        /// <summary>How many soft orbs <c>Dream.fx</c> draws (its <c>ORB_COUNT</c>).</summary>
        public const int DREAM_ORB_COUNT = 7;

        /// <summary>
        /// Where the dream's glass solid <paramref name="index"/> stands at a wall-clock time: <c>Dream.fx</c>'s
        /// <c>ShapeCenter</c>, with <paramref name="bound"/> the radius its march is gated on (<c>ShapeSize</c>
        /// × 1.9), which the morph never leaves.
        /// </summary>
        public Vector3 DreamSolidCenter(int index, float time, out float bound) => _dream.SolidCenter(index, time, out bound);

        /// <summary>
        /// Where the dream's soft orb <paramref name="index"/> stands at a wall-clock time — <c>Dream.fx</c>'s
        /// orb loop — and its glow radius. An orb is a closest-approach gaussian with no surface, so a lens
        /// near it is inside light, not inside geometry.
        /// </summary>
        public Vector3 DreamOrbCenter(int index, float time, out float radius) => _dream.OrbCenter(index, time, out radius);

        /// <summary>How many crystal clusters <c>Cavern.fx</c> draws (its <c>CRYSTAL_COUNT</c>).</summary>
        public const int CAVERN_CRYSTAL_COUNT = 8;

        /// <summary>How many god rays <c>Cavern.fx</c> draws (its <c>RAY_COUNT</c>).</summary>
        public const int CAVERN_RAY_COUNT = 4;

        /// <summary>
        /// Where the cavern's crystal cluster <paramref name="index"/> grows: <c>Cavern.fx</c>'s
        /// <c>CrystalCenter</c>, on the wall at 0.965 of the cave's radius. Its three octahedra reach about
        /// ten units out of that point sideways and some twenty-five up and down.
        /// </summary>
        public Vector3 CavernCrystalCenter(int index) => _cavern.CrystalCenter(index);

        /// <summary>
        /// Where the cavern's god ray <paramref name="index"/> falls, in the XZ plane: <c>Cavern.fx</c>'s
        /// vertical shaft from the ceiling towards the river.
        /// </summary>
        public Vector2 CavernGodRayXZ(int index) => _cavern.GodRayXZ(index);

        /// <summary>How many cumulus cells the storm's field was built with.</summary>
        public int StormCellCount => _storm.CellCount;

        /// <summary>
        /// Storm cell <paramref name="index"/> at a wall-clock time: its foot (the middle of its base, where
        /// <c>StormCellPosition</c> has carried it), its radius and its height. A puff stands up to
        /// the radius × 1.58 out from the middle (the ring plus its own disc) and up to the height plus
        /// about half the radius above the foot — the body a lens has to keep out of.
        /// </summary>
        public Vector3 StormCell(int index, float time, out float radius, out float height) =>
            _storm.Cell(index, time, out radius, out height);

        /// <summary>The icesheet's height at a world point, for a lens path over it: <see cref="TerrainMirror.Polar"/> on the live config.</summary>
        public float PolarGroundHeight(float x, float z) => _polar.GroundHeight(x, z);

        /// <summary>How deep into a crevasse slot a point stands, 0–1: <see cref="TerrainMirror.PolarCrevasse(float, float, PolarSceneConfig)"/> on the live config.</summary>
        public float PolarCrevasse(float x, float z) => _polar.Crevasse(x, z);

        /// <summary>How much of the pressure front stands at a point, 0–1: <see cref="TerrainMirror.PolarRidge(float, float, PolarSceneConfig)"/> on the live config.</summary>
        public float PolarRidgeAt(float x, float z) => _polar.RidgeAt(x, z);

        #endregion

        /// <summary>
        /// A terrain scene's effect and its CPU mirror on the live config, for the Testbed's <c>mirrorcheck</c>
        /// (#590): the effect carries a <c>HeightProbe</c> technique (<c>HeightProbe.fxh</c>) that writes the
        /// shader's own height at a world XZ, and <paramref name="mirror"/> is the <see cref="TerrainMirror"/>
        /// field that claims to copy it. False for a scene with no mirror. Nothing in a frame calls this.
        /// </summary>
        public bool TryGetTerrainProbe(SceneKind scene, out Effect effect, out Func<float, float, float> mirror)
        {
            if (BackdropFor(scene) is { } backdrop) return backdrop.TryGetTerrainProbe(out effect, out mirror);

            (effect, mirror) = scene switch
            {
                SceneKind.Savanna => (_savannaEffect, (x, z) => TerrainMirror.Savanna(x, z, _savannaConfig)),
                SceneKind.Tropical => (_tropicalEffect, (x, z) => TerrainMirror.Tropical(x, z, _tropicalConfig)),
                _ => ((Effect)null, (Func<float, float, float>)null),
            };

            return effect != null;
        }

        /// <summary>
        /// The Grid's solids as boxes on the floor, in the order they were placed, for a host framing a camera
        /// on one (the chapter intro's prologue, #559). Empty until the Grid's config has been applied.
        /// </summary>
        public IReadOnlyList<GridSolid> GridSolids => _grid.Solids;

        /// <summary>The Grid's landmark ring as a shape (#559); false when the config has none.</summary>
        public bool TryGetGridRing(out GridRing ring) => _grid.TryGetRing(out ring);

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
        public float VolcanoEruption(float time) => _volcano.Eruption(time);

        /// <summary>
        /// The event this scene has staged on its own clock at <paramref name="time"/>, described as the
        /// <b>sound</b> needs it rather than as the light does (#219's thunder, #223's eruption boom): which
        /// event it is, when its LIGHT began, where it happened and how big it is. False for the fifteen
        /// scenes that stage nothing.
        /// <para>
        /// <b>The caller tells one event from the next by <see cref="SceneEvent.Index"/> and never by
        /// watching the envelope.</b> Both envelopes flicker within a single event on purpose — the strike
        /// has return strokes and the eruption has its own shape — so an edge detector on the brightness
        /// would fire several times for one strike and hear thunder as a stutter.
        /// </para>
        /// <para>
        /// It reports the <b>onset of the light</b> and not of the sound, because the delay between the two
        /// is what says how far away the event is, and that is the caller's arithmetic: distance over the
        /// speed of sound. Both figures come out of the same schedule the light rides
        /// (<c>StormStrikeSchedule</c>, <c>VolcanoBurstSchedule</c>), so a flash and its thunder cannot name
        /// two different strikes.
        /// </para>
        /// </summary>
        public bool TryGetSceneEvent(SceneKind kind, float time, out SceneEvent staged)
        {
            if (BackdropFor(kind) is { } backdrop) return backdrop.TryGetSceneEvent(time, out staged);

            staged = default;
            return false;
        }

        //A deterministic hash of a small integer, for the eruption schedule and the storm's strikes
        //(StormBackdrop and VolcanoBackdrop call it). A sine hash is fine here where it would not be in a shader: it runs on
        //one CPU with one rounding, and its argument stays small.
        internal static float Hash01(float n)
        {
            float s = MathF.Sin(n * 12.9898f) * 43758.5453f;
            return s - MathF.Floor(s);
        }

        /// <summary>
        /// A light on the ground that lights the underside of the cloud deck, for the hosts to hand to
        /// <see cref="CloudField.SetGroundGlow"/> every frame (#509): the volcano's crater, swelling with each
        /// burst off the same <see cref="VolcanoEruption"/> figure the jets, the plume and the crater's lamp
        /// ride, so the cloud flares with them. False for every scene with nothing burning under its sky, and
        /// for the volcano with <see cref="VolcanoSceneConfig.DeckGlow"/> at zero.
        /// </summary>
        /// <param name="time">The same wall clock the host feeds <see cref="SceneFrame.Time"/> and the scene lights.</param>
        public bool TryGetGroundGlow(SceneKind kind, float time, out Vector3 position, out Vector3 color, out float range)
        {
            if (BackdropFor(kind) is { } backdrop) return backdrop.TryGetGroundGlow(time, out position, out color, out range);

            position = default;
            color = default;
            range = 1f;
            return false;
        }

        /// <summary>
        /// How many point lights the volcano pushes, capped to the scene-light budget the shaders' arrays are
        /// sized for. Slot 0 is the crater; every other slot rides a river.
        /// </summary>
        public int VolcanoLightCount => _volcano.LightCount;

        /// <summary>The volcano's point-light range (quadratic falloff), shared by the crater and the flows.</summary>
        public float VolcanoLightRange => _volcano.LightRange;

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
        public Vector3 VolcanoLightPosition(int index, float time) => _volcano.LightPosition(index, time);

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
        public Vector3 VolcanoLightColor(float time, int index) => _volcano.LightColor(time, index);

        #endregion

        //The savanna's two programs (#281), the meadow's pair: the reduced one gives up the tuft gaps and the blade strokes
        private void SelectSavannaTechnique() =>
            _savannaEffect.CurrentTechnique = _savannaEffect.Techniques[_sceneDetail > 0.5f ? "Savanna" : "SavannaReduced"];

        /// <summary>
        /// Points every scene that has a reduced program at it or at its full one — the forest's floor first, whose
        /// measurement this is (ForestBackdrop's pick since #580, with the rest). By <b>technique</b> and not by a
        /// uniform the shader branches on: what the reduced floor gives up is occupancy, and a runtime branch
        /// skips the work while keeping the registers that cost it — measured, a uniform branch saved 0.02 ms
        /// of the 0.60 the separate program saves. See <see cref="SceneDetail"/>.
        /// </summary>
        private void SelectDetailTechniques()
        {
            SelectSavannaTechnique();

            //The dream's, the cavern's, Mars's, the mountain's, the meadow's and the forest's picks moved into their backdrops
            //with the rest of them (#580)
            foreach (Backdrop backdrop in _backdrops) backdrop?.OnDetailChanged(_sceneDetail);
        }

        /// <summary>
        /// Normalizes a config direction, falling back to <paramref name="fallback"/> for the degenerate zero
        /// vector — these are hand-typed values in a JSON file and in a property grid, where a zero is one
        /// keystroke away, and a NaN direction would take the whole sky with it.
        /// </summary>
        internal static Vector3 SafeNormal(Vector3 direction, Vector3 fallback) =>
            direction.LengthSquared() > 1e-8f ? Vector3.Normalize(direction) : fallback;

        #endregion

        #region The far field (#551)

        //The land past each open-ground scene's own grid, and the fade into the sky: a service since #580
        //(Render/Scenes/FarField.cs), which every terrain draw here and in the backdrops goes through.
        private readonly FarField _farField;

        /// <summary>Where the far ground has become the sky behind it (FarField.fxh's FarFadeToSky). Past the ring's
        /// inner edge everywhere and short of its outer edge, so nothing about where the ring ends can show.</summary>
        public const float FAR_FADE_END = FarField.FAR_FADE_END;

        #endregion

        /// <summary>A terrain grid from the shared cache; see <see cref="BackdropServices.AcquireGridMesh"/>.</summary>
        private void AcquireGridMesh(int n, float extent, out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer, out int indexCount)
            => _services.AcquireGridMesh(n, extent, out vertexBuffer, out indexBuffer, out indexCount);

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
            //The scenes that are their own Backdrop (#580) draw through it. The two full-screen analytic
            //backdrops that are worth more than the frame can afford get shaded at the back buffer's own size
            //and scaled up; every other scene draws straight into whatever the caller bound. See
            //DrawBackdropAtDisplayResolution for what that trades and why it is these two.
            Backdrop backdrop = BackdropFor(scene);
            if (backdrop != null)
            {
                if (sceneTarget != null && SupersampleFactor > 1 && backdrop.DrawsAtDisplayResolution)
                    DrawBackdropAtDisplayResolution(backdrop, frame, sceneTarget);
                else
                    backdrop.Draw(frame);

                return;
            }

            switch (scene)
            {
                case SceneKind.Savanna:
                    DrawSavanna(frame);
                    DrawAcacias(frame);
                    _birds.Draw(frame, _savannaConfig.Birds);
                    break;
                case SceneKind.Tropical:
                    //The land first (it writes depth), then the lagoon depth-read over the bed it owns,
                    //then the scatter that stands on the sand, then the flock over the water.
                    DrawTropicalTerrain(frame);
                    DrawTropicalWater(frame);
                    DrawPalms(frame);
                    DrawTropicalRocks(frame);
                    DrawTropicalDressing(frame);
                    _birds.Draw(frame, _tropicalConfig.Birds);
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
            if (BackdropFor(scene) is { } backdrop)
            {
                backdrop.DrawOverlays(frame);
                return;
            }

            if (scene == SceneKind.Savanna) DrawFlame(frame);
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

            _farField.Begin(_tropicalEffect, frame, TROPICAL_EXTENT);
            _graphicsDevice.SetVertexBuffer(_tropicalVertexBuffer);
            _graphicsDevice.Indices = _tropicalIndexBuffer;
            _tropicalEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _tropicalIndexCount / 3);
            _farField.DrawRing(_tropicalEffect, new Vector2(originX, originZ), TROPICAL_EXTENT);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the lagoon: the sea's own shader and grid (<c>Sea.fx</c> unchanged), pushed the
        /// tropical config's water values — calmer swell, turquoise body colours — over the terrain
        /// <see cref="DrawTropicalTerrain"/> just wrote. States exactly as <see cref="SeaBackdrop.Draw"/> sets
        /// them, for its own reasons: opaque and <c>CullNone</c> (one open surface,
        /// read from above and through the crests), depth-READ so anything under the surface keeps its
        /// own pixels.
        /// <para>
        /// <b>The lagoon draws through its own clone of <c>Sea.fx</c></b> (<c>_lagoonEffect</c>, #580),
        /// its water set pushed once at load by <see cref="ApplyLagoonParameters"/>. Until then the sea
        /// and the lagoon shared one effect instance, and each re-pushed its whole water set every frame
        /// so that a NumPad2/V switch, which applies no config, could not leave one scene drawing the
        /// other's water.
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

            float cell = SeaBackdrop.SEA_EXTENT / (SeaBackdrop.SEA_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            float clip = terrain.ShoreRadius - terrain.CoastNoise - TROPICAL_WATERLINE_BIAS;

            _lagoonEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _lagoonEffect.Parameters["IslandHoleRadius"].SetValue(clip);
            _lagoonEffect.Parameters["FunnelPoolRadius"].SetValue(0f);
            _lagoonEffect.Parameters["View"].SetValue(frame.Camera.View);
            _lagoonEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _lagoonEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _lagoonEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _lagoonEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _lagoonEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _lagoonEffect.Parameters["SeaTime"].SetValue(frame.Time);
            _lagoonEffect.Parameters["SunColor"].SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(_lagoonEffect);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            //The sea's own reasoning: depth-read, not depth-write — only the open water gives the
            //depth up, and the terrain (drawn before it, opaque) still writes and owns its own.
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _farField.Begin(_lagoonEffect, frame, SeaBackdrop.SEA_EXTENT);
            _graphicsDevice.SetVertexBuffer(_lagoonVertexBuffer);
            _graphicsDevice.Indices = _lagoonIndexBuffer;
            _lagoonEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _lagoonIndexCount / 3);
            _farField.DrawRing(_lagoonEffect, new Vector2(originX, originZ), SeaBackdrop.SEA_EXTENT);

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

            _farField.Begin(_savannaEffect, frame, SAVANNA_EXTENT);
            _graphicsDevice.SetVertexBuffer(_savannaVertexBuffer);
            _graphicsDevice.Indices = _savannaIndexBuffer;
            _savannaEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _savannaIndexCount / 3);
            _farField.DrawRing(_savannaEffect, new Vector2(originX, originZ), SAVANNA_EXTENT);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Every palm on the beach as a figure — the root, the crown the trunk's bow carries off it, how far the
        /// fronds reach and the trunk's thickness — for a host keeping a camera out of the grove (#559).
        /// </summary>
        public IReadOnlyList<PlantFigure> TropicalPalms => _palmFigures;

        /// <summary>The waterline's rocks as figures (their mesh's bounding sphere at the instance), for the same host.</summary>
        public IReadOnlyList<PlantFigure> TropicalRocks => _tropicalRockFigures;

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

            //Everything planted (#451): one instanced draw per bucket, each with its own material, off its
            //own static instance buffer. The Low tier skips the buckets marked as detail (the grass tufts).
            ScatterBucket[] buckets = _savannaScatter.Buckets;
            bool detail = _sceneDetail > 0.5f;
            for (int b = 0; b < buckets.Length; b++)
            {
                ScatterBucket bucket = buckets[b];
                if (bucket.DetailOnly && !detail) continue;

                _acaciaDiffuseParam.SetValue(bucket.Diffuse);
                _acaciaDiffuseDryParam.SetValue(bucket.DiffuseDry);
                _acaciaDappleParam.SetValue(bucket.Dapple);
                _acaciaBarkParam.SetValue(bucket.Bark);
                _acaciaAddedLightParam.SetValue(Vector3.Zero);
                _acaciaEffect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(bucket.Mesh.VertexBuffer, 0, 0),
                    new VertexBufferBinding(bucket.Instances, 0, 1));
                _graphicsDevice.Indices = bucket.Mesh.IndexBuffer;
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, bucket.Mesh.PrimitiveCount, bucket.Count);
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
            UploadHearthInstances(instances);

            _acaciaDiffuseParam.SetValue(diffuse);
            _acaciaDiffuseDryParam.SetValue(diffuse);   //no dryness on a stone: Custom.x is zero on every hearth instance
            _acaciaDappleParam.SetValue(dappleStrength);
            _acaciaBarkParam.SetValue(0f);
            _acaciaAddedLightParam.SetValue(addedLight);
            _acaciaEffect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(mesh.VertexBuffer, 0, 0),
                new VertexBufferBinding(_acaciaInstanceBuffer, 0, 1));
            _graphicsDevice.Indices = mesh.IndexBuffer;
            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, instances.Length);
        }

        /// <summary>The hearth stones' per-draw upload into the one dynamic instance buffer, grown as needed.</summary>
        private void UploadHearthInstances(ModelInstance[] instances)
        {
            if (_acaciaInstanceBuffer == null || _acaciaInstanceBuffer.VertexCount < instances.Length)
            {
                _acaciaInstanceBuffer?.Dispose();
                _acaciaInstanceBuffer = new DynamicVertexBuffer(_graphicsDevice, ModelInstance.VertexDeclaration,
                    instances.Length, BufferUsage.WriteOnly);
            }
            _acaciaInstanceBuffer.SetData(instances, 0, instances.Length, SetDataOptions.Discard);
        }

        /// <summary>
        /// States that the ceiling's glass hangs this frame with its centre at <paramref name="centre"/>, so the
        /// receivers shade what stands under it (#553). <b>Stated per frame, before <see cref="DrawShadowMaps"/></b>,
        /// which consumes it: a frame that does not call this has no plate shadow, so a screen that stops drawing a
        /// plate stops shadowing with it without having to say so.
        /// <para>
        /// The glass is not a caster in the map, and that rule stands (a depth map would cast it as stone — see
        /// "Sun shadows" in <c>docs/rendering.md</c>). What it casts is worked out in every receiver instead
        /// (<c>Shadows.fxh</c>'s <c>CeilingGlassShadow</c>): the plate is one axis-aligned slab, so whether a point's
        /// ray to the sun crosses it is arithmetic, and what the cut does to the light that crosses it is too.
        /// Everything about the slab is read off <paramref name="plate"/> — the renderer <see cref="CeilingPlate"/>
        /// fitted, which carries the outline and the cut the refracting technique traces — so the shadow and the
        /// drawn glass cannot disagree. It follows the map's own gates exactly: no map this frame, no glass shadow.
        /// </para>
        /// </summary>
        /// <param name="plate">The plate's renderer (<see cref="CeilingPlate.Renderer"/>); null states nothing.</param>
        /// <param name="centre">The slab's centre in world space — the translation of the world matrix it is drawn
        /// with. The plate is never rotated.</param>
        public void CastCeilingShadow(InstancedModelRenderer plate, Vector3 centre)
        {
            if (plate == null) return;

            Vector3 half = plate.GlassHalfExtents;
            float crown = plate.GlassCrownInset.W;
            _ceilingShadowCentre = new Vector4(centre, 1f);
            _ceilingShadowSize = new Vector4(half, plate.GlassCornerRadius);
            _ceilingShadowCut = new Vector4(plate.GlassCutPeriod, plate.GlassCutSlope, crown, crown + plate.GlassRim.X);
            _ceilingShadowStated = true;
        }

        //This frame's plate, as CastCeilingShadow stated it; consumed by DrawShadowMaps (#553)
        private Vector4 _ceilingShadowCentre, _ceilingShadowSize, _ceilingShadowCut;
        private bool _ceilingShadowStated;

        /// <summary>
        /// Renders this frame's sun shadow map and hands it to the receivers' effects. <b>Call it before
        /// binding the scene target</b>: the map is its own render target, and switching away from the scene
        /// target mid-frame to draw it cleared the sky already drawn into it while that target was
        /// <see cref="RenderTargetUsage.DiscardContents"/>. It is preserved since #541 (the ceiling's grab has
        /// to leave it and come back), so the switch would no longer wipe it, but it would still cost a resolve of
        /// a multisampled target for nothing — the order stands. Leaves the back buffer bound; that it leaves the GPU
        /// states as it found them is <i>not</i> promised — the caller states its own before its scene, as
        /// every executable already does after the sky.
        /// <para>
        /// <b>It is the scene's own decision since #471</b>, where it was the savanna's alone (#469): the gate
        /// is <see cref="SceneConfig.Shadows"/> on whichever backdrop is up, so a scene opts in by saying so
        /// in its config and this method names no scene to decide <i>whether</i>. It still names them to
        /// decide two things that are genuinely per scene — how the map is fitted
        /// (<see cref="TryShadowFit"/>) and which of this renderer's own scatter casts into it.
        /// </para>
        /// <para>
        /// A no-op at the Low tier, at <see cref="ShadowConfig.Strength"/> 0, at <see cref="ShadowScale"/> 0,
        /// with the sun at or below
        /// <see cref="SHADOW_MIN_SUN_HEIGHT"/>, in any scene with no fit, and — in the eight scenes whose
        /// casters are all the host's — for a caller that passes no <paramref name="extraCasters"/> at all:
        /// in every one of those each receiver is handed a strength of 0 and skips its taps, and no target is
        /// touched.
        /// </para>
        /// <para>
        /// What casts: the scene's own planting where this renderer owns it — the savanna's scatter and hearth
        /// stones through <c>Acacia.fx</c>'s <c>ShadowCaster</c>, the beach's palms and rocks through
        /// <c>Palm.fx</c>'s own — and then whatever <paramref name="extraCasters"/> draws: the island, the gun
        /// (#470) and the forest's wood, which are the host's objects and not this renderer's.
        /// </para>
        /// <para>
        /// <b>What receives is everything</b> that includes <c>Shadows.fxh</c>: the terrain shaders listed in
        /// <see cref="RegisterShadowReceivers"/>, and — once <paramref name="instancedEffect"/> is handed in —
        /// the island's cap, the drain, the gun, the city and the balls, which all read the map from that one
        /// push exactly as <see cref="SceneLights"/> reaches them from one. A caller that passes no effect
        /// still gets the scene's own shadows; it simply leaves everything drawn through the shared effect
        /// unshadowed.
        /// </para>
        /// </summary>
        /// <param name="scene">The backdrop being drawn; its config says whether it has a map.</param>
        /// <param name="camera">This frame's camera — the map is fitted round where it stands.</param>
        /// <param name="sunDirection">The direction <b>towards</b> the sun, from the dome's own rig.</param>
        /// <param name="instancedEffect">The shared <c>Shaders/InstancedModel</c> effect, so everything drawn
        /// through it receives. The caller's, and the same instance for the life of the program.</param>
        /// <param name="extraCasters">Draws the host's own casters into the map, handed the map's world →
        /// clip matrix. Called with the target bound and the states set; see
        /// <see cref="ArenaIsland.DrawShadow"/>.</param>
        public void DrawShadowMaps(SceneKind scene, ICamera camera, Vector3 sunDirection,
            Effect instancedEffect = null, Action<Matrix> extraCasters = null)
        {
            //The shared effect's parameters, cached on the first frame one is handed in (#470). It is the
            //caller's effect, so it cannot be registered at load with the rest (RegisterShadowReceivers).
            if (instancedEffect != null && !ReferenceEquals(instancedEffect, _instancedShadowEffect))
            {
                _instancedShadowEffect = instancedEffect;
                _instancedShadowReceiver = new ShadowReceiver(instancedEffect);
                _instancedShadowReceiver.Disable();
            }

            //Is there anything to cast at all? Only the savanna and the beach have planting of this
            //renderer's own; in the other eight the casters are all the host's, so a caller that registers
            //none of them — the MAP EDITOR, which draws no island, no gun and no wood — would render an empty
            //map and then pay nine taps a pixel to read that everything is lit. The editor is the caller this
            //spares, and it is the only one: both other executables always hand a callback in.
            bool sceneCasts = scene == SceneKind.Savanna || scene == SceneKind.Tropical;

            //Then the gates that cost least to fail first: does this scene ask for a map at all, is the tier
            //high enough, is the sun above the horizon, and does the scene have a ground to fit a map round.
            //The city's config lives outside this renderer, so GetSceneConfig answers null for it.
            bool wanted = false;
            Vector3 centre = Vector3.Zero;
            float yMin = 0f, yMax = 0f;
            ShadowConfig shadows = GetSceneConfig(scene)?.Shadows
                ?? (_hostShadowScenes.TryGetValue(scene, out HostShadowScene hostScene) ? hostScene.Shadows : null);
            if (shadows != null && shadows.Enabled && _shadowScale > 0f
                && (sceneCasts || extraCasters != null)
                && _sceneDetail > 0.5f && sunDirection.Y > SHADOW_MIN_SUN_HEIGHT)
            {
                wanted = TryShadowFit(scene, camera, out centre, out yMin, out yMax);
            }

            //The plate is stated per frame; whatever this frame does with it, the next one starts without it
            bool ceilingStated = _ceilingShadowStated;
            _ceilingShadowStated = false;

            if (!wanted)
            {
                if (_shadowsActive)
                {
                    _shadowsActive = false;
                    for (int i = 0; i < _shadowReceivers.Length; i++) _shadowReceivers[i].Disable();
                    _instancedShadowReceiver.Disable();
                }
                return;
            }

            //The scene's own size, scaled up by the Ultra tier and held under a lower tier's cap, unless the
            //instrument pins one outright (#484). The clamp's top is what a card should be asked for rather than
            //what D3D11 allows: at eight bytes a texel (a Single target over a Depth24 buffer) 8192 is a 537 MB
            //map, 4096 is 134 MB and 2048 is 33.5 — so Ultra's doubling of the authored 4096 IS that top.
            int size = shadows.MapSize * Math.Max(ShadowMapSizeScale, 1);
            if (ShadowMapSizeCap > 0) size = Math.Min(size, ShadowMapSizeCap);
            if (ShadowMapSizeOverride > 0) size = ShadowMapSizeOverride;
            size = Math.Clamp(size, 256, 8192);

            //⚠ Nothing but the map is disposed here. #476 left the savanna's trail-warp field's Dispose in this
            //block for two days (its home is Dispose() below), so the first map a process built — the first
            //shadowed frame after a scene's build — threw away the texture the savanna effect was still bound to.
            if (_sunShadowMap == null || _sunShadowMap.Size != size)
            {
                _sunShadowMap?.Dispose();
                _sunShadowMap = new SunShadowMap(_graphicsDevice, size);
            }

            _sunShadowMap.Fit(centre, sunDirection, shadows.Extent, yMin, yMax);

            _graphicsDevice.SetRenderTarget(_sunShadowMap.Target);
            _graphicsDevice.Clear(Color.White);
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            //This scene's own planting — the two scatters this renderer owns. The culling stays off for both:
            //two-sided blades, fans and fronds, and a closed solid drawn from both sides cannot peter-pan out
            //of its own shadow. The FOREST's wood is not here because it is not this renderer's: the hosts own
            //their ForestScatterRenderer, so it casts through extraCasters below with the island and the gun.
            switch (scene)
            {
                case SceneKind.Savanna:
                    DrawSavannaShadowCasters();
                    break;
                case SceneKind.Tropical:
                    DrawTropicalShadowCasters();
                    break;
            }

            //And whatever the caller casts (#470): the island and the gun, which are the executable's objects
            //and not this renderer's ("the setting, in one copy" — the renderer draws the scene's own scatter
            //and the host draws what stands on it). It draws through InstancedModelRenderer.DrawDepth, which
            //states the technique it needs and puts the main one back.
            extraCasters?.Invoke(_sunShadowMap.ViewProjection);

            _graphicsDevice.SetRenderTarget(null);

            //Hand the map to every receiver: the matrix, the texel, the strength, and the bias in the map's
            //own depth units. ⚠ ShadowViewProjection is pushed here AND by the caster pass above (both
            //InstancedModelRenderer.DrawDepth and the scatter's own technique set it): the same matrix and
            //the same uniform, and a caster's write is what a caller drawing into a map of its own would
            //leave behind, so this one is the frame's last word on it.
            float bias = SHADOW_BIAS_UNITS / _sunShadowMap.DepthRange;
            for (int i = 0; i < _shadowReceivers.Length; i++)
            {
                _shadowReceivers[i].Push(_sunShadowMap.Target, _sunShadowMap.ViewProjection,
                    _sunShadowMap.Texel, shadows.Strength * _shadowScale, bias);
            }

            //The shared instanced effect, which is what makes the island, the gun, the city and the balls
            //receive — one push for all of them (#470).
            _instancedShadowReceiver.Push(_sunShadowMap.Target, _sunShadowMap.ViewProjection,
                _sunShadowMap.Texel, shadows.Strength * _shadowScale, bias);

            //And the ceiling's glass, if a plate was stated this frame (#553): in w, how much of the sun its uncut
            //glass takes, 0 when there is none - which is what the receivers skip on. The scene's strength is the
            //receivers' own ShadowStrength, applied over the glass and the map together.
            PushCeilingShadow(ceilingStated ? CeilingPlate.SHADOW_TAKE : 0f);

            _shadowsActive = true;
        }

        /// <summary>The ceiling's glass to every receiver, with <paramref name="take"/> in the centre's <c>w</c> (#553).</summary>
        private void PushCeilingShadow(float take)
        {
            Vector4 centre = _ceilingShadowCentre;
            centre.W = take;
            for (int i = 0; i < _shadowReceivers.Length; i++)
                _shadowReceivers[i].PushCeiling(centre, _ceilingShadowSize, _ceilingShadowCut);
            _instancedShadowReceiver.PushCeiling(centre, _ceilingShadowSize, _ceilingShadowCut);
        }

        /// <summary>
        /// Where this scene's shadow map sits and how tall a box it spans: the camera's own ground position,
        /// and a height range from below the lowest ground the map will cover to above the tallest thing that
        /// casts into it. False for a scene with no ground of its own, which is what keeps a backdrop out of
        /// the feature even if its config asks for shadows.
        /// <para>
        /// <b>The range is computed here rather than authored in <see cref="ShadowConfig"/>, and that is the
        /// decision worth knowing.</b> It is not a designer's number: it follows the scene's own hill height
        /// and dressing, which are dials that get tuned. Written beside them as a figure it would be a second
        /// copy of the terrain's proportions and one that drifts silently — the map keeps rendering, it simply
        /// stops covering what stands in it, and nothing in the frame says why.
        /// </para>
        /// <para>
        /// A box far bigger than it needs to be is not free either: the bias is carried in the map's own depth
        /// units (<c>SHADOW_BIAS_UNITS / DepthRange</c>), so a range stretched to cover a peak two hundred
        /// units away coarsens the bias for the grass under the gun. Hence a scene's own relief rather than
        /// one number for all of them.
        /// </para>
        /// <para>
        /// <b>Which scenes are here is the same list as <see cref="RegisterShadowReceivers"/>' and has to
        /// stay so</b>: a scene fitted but not receiving casts into a map nobody reads, and a scene receiving
        /// but not fitted is handed 0 every frame. Sea and Storm are deliberately in neither — see that
        /// method for why. A scene moved into its own <see cref="Backdrop"/> (#580) states both side by side
        /// (<see cref="Backdrop.ShadowReceivers"/>, <see cref="Backdrop.TryShadowFit"/>), and is asked here
        /// before the switch below.
        /// </para>
        /// </summary>
        private bool TryShadowFit(SceneKind scene, ICamera camera, out Vector3 centre, out float yMin, out float yMax)
        {
            centre = Vector3.Zero;
            yMin = yMax = 0f;

            float groundY, below, above;

            //A host-owned backdrop states its own (#471): the city's ground is its street level and its relief
            //is the towers standing on it, neither of which this renderer has ever been told about.
            if (_hostShadowScenes.TryGetValue(scene, out HostShadowScene host))
            {
                groundY = host.GroundY;
                below = host.Below;
                above = host.Above;
            }
            else if (BackdropFor(scene) is { } backdrop)
            {
                if (!backdrop.TryShadowFit(out groundY, out below, out above)) return false;
            }
            else
            switch (scene)
            {
                case SceneKind.Savanna:
                    //#469's own fit, kept to the digit: half a rise below the plain, and a baobab and a half
                    //over the rises. It is the one that was measured and photographed, so it stays its own
                    //expression rather than joining the shared headroom below.
                    groundY = _savannaConfig.LevelY;
                    below = _savannaConfig.HillHeight * 0.5f;
                    above = _savannaConfig.HillHeight + _savannaConfig.Dressing.BaobabHeight * 1.5f;
                    break;

                case SceneKind.Tropical:
                    groundY = _tropicalConfig.Terrain.LevelY;
                    below = _tropicalConfig.Terrain.HillHeight * 0.5f;
                    above = _tropicalConfig.Terrain.HillHeight;
                    break;

                default:
                    return false;
            }

            Vector3 at = camera.Position;
            centre = new Vector3(at.X, groundY, at.Z);
            yMin = groundY - below - SHADOW_FIT_MARGIN;

            //Whatever the terrain does, the box has to clear the island's cap with the gun standing on it —
            //in every scene but the savanna and the forest those two ARE the casters, and a box fitted to a
            //flat plain would clip the very thing throwing the shadow.
            yMax = MathF.Max(groundY + above, ArenaIsland.TOP_Y + SHADOW_ISLAND_HEADROOM) + SHADOW_FIT_MARGIN;
            return true;
        }

        /// <summary>
        /// The savanna's own casters (#469): every bucket of the scatter and the ring of hearth stones,
        /// through <c>Acacia.fx</c>'s <c>ShadowCaster</c> technique. Puts the main technique back on the way
        /// out, the way <see cref="InstancedModelRenderer.DrawDepth(Matrix, ModelInstance[], int)"/> does.
        /// </summary>
        private void DrawSavannaShadowCasters()
        {
            if (_savannaScatter == null) return;

            _acaciaEffect.CurrentTechnique = _acaciaShadowTechnique;
            _acaciaEffect.Parameters["ShadowViewProjection"].SetValue(_sunShadowMap.ViewProjection);
            _acaciaEffect.CurrentTechnique.Passes[0].Apply();

            ScatterBucket[] buckets = _savannaScatter.Buckets;
            for (int b = 0; b < buckets.Length; b++)
            {
                ScatterBucket bucket = buckets[b];
                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(bucket.Mesh.VertexBuffer, 0, 0),
                    new VertexBufferBinding(bucket.Instances, 0, 1));
                _graphicsDevice.Indices = bucket.Mesh.IndexBuffer;
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, bucket.Mesh.PrimitiveCount, bucket.Count);
            }

            if (_hearthStoneInstances != null && _hearthStoneMeshes != null)
            {
                for (int fire = 0; fire < _hearthStoneInstances.Length; fire++)
                {
                    ModelInstance[] instances = _hearthStoneInstances[fire];
                    if (instances == null || instances.Length == 0) continue;
                    UploadHearthInstances(instances);
                    IProceduralMesh mesh = _hearthStoneMeshes[fire % _hearthStoneMeshes.Length];
                    _graphicsDevice.SetVertexBuffers(
                        new VertexBufferBinding(mesh.VertexBuffer, 0, 0),
                        new VertexBufferBinding(_acaciaInstanceBuffer, 0, 1));
                    _graphicsDevice.Indices = mesh.IndexBuffer;
                    _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, instances.Length);
                }
            }

            _acaciaEffect.CurrentTechnique = _acaciaTechnique;
        }

        /// <summary>
        /// The beach's own casters (#471): the palms and the waterline's rocks, through <c>Palm.fx</c>'s
        /// <c>ShadowCaster</c> — the same draws <see cref="DrawPalms"/> and <see cref="DrawTropicalRocks"/>
        /// make, with the technique swapped, so a frond's shadow is cut from the frond and not from a
        /// stand-in. Palm shadows on sand are what a beach looks like, which is why this scene has casters of
        /// its own at all while the desert and the outback make do with the island's.
        /// <para>
        /// ⚠ <b>The sway is one frame stale here.</b> The map is drawn before the scene, so the wind's clock
        /// on the effect (<c>PalmTime</c>, the wind and its speed) is still the previous frame's — only the
        /// per-draw sway strength is set below. At the beach's sway speed that is under a hundredth of a
        /// radian of phase, which moves a frond tip by a fraction of a millimetre; re-pushing this frame's
        /// clock would mean handing this method a <see cref="SceneFrame"/> it otherwise has no use for.
        /// </para>
        /// </summary>
        private void DrawTropicalShadowCasters()
        {
            if (_palmMeshes == null) return;

            _palmEffect.CurrentTechnique = _palmShadowTechnique;

            for (int m = 0; m < _palmMeshes.Length; m++)
            {
                StaticInstances instances = _palmInstances[m];
                if (instances.Count == 0) continue;

                float sway = _tropicalConfig.Palms.SwayStrength;
                DrawPalmPart(_palmMeshes[m].Fronds, instances, Vector3.Zero, dappleStrength: 0f, swayStrength: sway);
                DrawPalmPart(_palmMeshes[m].Wood, instances, Vector3.Zero, dappleStrength: 0f, swayStrength: sway);
            }

            if (_tropicalRockMeshes != null)
            {
                for (int m = 0; m < _tropicalRockMeshes.Length; m++)
                {
                    StaticInstances instances = _tropicalRockInstances[m];
                    if (instances.Count == 0) continue;

                    //No sway, for the reason DrawTropicalRocks gives: these are lathe meshes whose TEXCOORD0.x
                    //is a circumference, which this shader reads as its sway weight. At the palms' strength
                    //the stones shear open — and a sheared stone casts a sheared shadow.
                    DrawPalmPart(_tropicalRockMeshes[m], instances, Vector3.Zero, dappleStrength: 0f, swayStrength: 0f);
                }
            }

            _palmEffect.CurrentTechnique = _palmTechnique;
        }

        /// <summary>
        /// Draws the scattered palms: real 3D geometry on the acacia's path (#202), one instanced draw per
        /// mesh variant per material — a palm's leaves (the live crown, per-variant drier or greener, and the
        /// dead skirt) and its solids (the trunk, the boot and the coconuts) share the variant's per-plant
        /// matrices, both through Palm.fx's palm material (#557).
        /// Shaded from the scene's own sun and dome by <c>Palm.fx</c>, which also sways the crown on the
        /// wind off the wall clock. Opaque and depth-writing; tropical scene only, after the terrain and
        /// the water.
        /// </summary>
        private void DrawPalms(in SceneFrame frame)
        {
            ApplyPalmFrame(frame);

            for (int m = 0; m < _palmMeshes.Length; m++)
            {
                StaticInstances instances = _palmInstances[m];
                if (instances.Count == 0) continue;

                Vector3 frond = Vector3.Lerp(_palmFrondColor, _palmFrondDry, _palmDryness[m] * 0.6f);

                //The palms are the one thing here that MEANS to sway: PalmMesh bakes the weight ramp the
                //shader reads, zero along the trunk and rising to the frond tips, so the wind moves the
                //crown and never the trunk.
                float sway = _tropicalConfig.Palms.SwayStrength;

                //The leaves are single-sided and drawn UNCULLED (#557): Palm.fx turns each pixel's normal to the
                //face the camera sees, so a frond's underside is its underside rather than a copy of its top.
                //The solids after them go back to the shared culling.
                _graphicsDevice.RasterizerState = RasterizerState.CullNone;
                DrawPalmPart(_palmMeshes[m].Fronds, instances, frond, dappleStrength: 0.4f, swayStrength: sway, palmShading: 1f);
                _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                DrawPalmPart(_palmMeshes[m].Wood, instances, _palmTrunkColor, dappleStrength: 0f, swayStrength: sway, palmShading: 1f);
            }
        }

        /// <summary>
        /// The beach's dressing (#445): the low scrub and sea grass at the tree line and the driftwood at the
        /// waterline, through the palm effect like everything else standing on this sand.
        /// <para>
        /// ⚠ <b>None of it sways</b>, and that is not laziness about grass. <c>Palm.fx</c> reads
        /// <c>TEXCOORD0.x</c> as its sway weight, which <see cref="PalmMesh"/> bakes as a deliberate ramp
        /// from the trunk to the frond tip; every other mesh in the library puts something else there, and
        /// at the palms' strength the rocks sheared open (see <see cref="DrawTropicalRocks"/>). A still tuft
        /// beside a swaying palm is a smaller fault than a tuft that tears itself apart.
        /// </para>
        /// </summary>
        private void DrawTropicalDressing(in SceneFrame frame)
        {
            if (_tropicalScrubMeshes == null) return;

            ApplyPalmFrame(frame);

            for (int m = 0; m < _tropicalScrubMeshes.Length; m++)
            {
                StaticInstances instances = _tropicalScrubInstances[m];
                if (instances.Count == 0) continue;
                DrawPalmPart(_tropicalScrubMeshes[m], instances, _tropicalScrubColor, dappleStrength: 0.35f, swayStrength: 0f);
            }

            for (int m = 0; m < _tropicalTuftMeshes.Length; m++)
            {
                StaticInstances instances = _tropicalTuftInstances[m];
                if (instances.Count == 0) continue;
                DrawPalmPart(_tropicalTuftMeshes[m], instances, _tropicalTuftColor, dappleStrength: 0.25f, swayStrength: 0f);
            }

            for (int m = 0; m < _tropicalDriftMeshes.Length; m++)
            {
                StaticInstances instances = _tropicalDriftInstances[m];
                if (instances.Count == 0) continue;
                DrawPalmPart(_tropicalDriftMeshes[m], instances, _tropicalDriftColor, dappleStrength: 0f, swayStrength: 0f);
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
                StaticInstances instances = _tropicalRockInstances[m];
                if (instances.Count == 0) continue;

                //NO SWAY: a boulder does not move in the wind, and both of these meshes are LatheMeshes
                //whose TEXCOORD0.x runs 0..1 around the circumference — which Palm.fx reads as its sway
                //weight (see DrawPalmPart). Left at the palms' strength the stones sheared open.
                DrawPalmPart(_tropicalRockMeshes[m], instances, _tropicalStoneColor, dappleStrength: 0f, swayStrength: 0f);
                DrawPalmPart(_tropicalMossMeshes[m], instances, _tropicalMossColor, dappleStrength: 0.30f, swayStrength: 0f);
            }
        }

        /// <summary>
        /// The palm material's colours that the per-draw DiffuseColor cannot carry (#557), pushed once per config
        /// since nothing else drawn through the effect reads them. Called from the tropical parameters AND after
        /// the effect loads, because the constructor applies the config before the palm effect exists.
        /// </summary>
        private void PushPalmMaterial()
        {
            if (_palmEffect == null) return;

            PalmConfig palms = _tropicalConfig.Palms;
            _palmEffect.Parameters["AgedFrondColor"].SetValue(palms.AgedFrondColor.ToVector3());
            _palmEffect.Parameters["DeadFrondColor"].SetValue(palms.DeadFrondColor.ToVector3());
            _palmEffect.Parameters["CoconutColor"].SetValue(palms.CoconutColor.ToVector3());
        }

        /// <summary>
        /// Pushes the frame's shared palm-effect parameters — the two draws below run them once each, so
        /// the pushing lives in one place between them. The states are the acacia's: opaque, depth-writing
        /// solids wound like every lathe. The palms' leaves are the one exception, drawn unculled by
        /// <see cref="DrawPalms"/> around their own draw (#557).
        /// </summary>
        private void ApplyPalmFrame(in SceneFrame frame)
        {
            _palmViewParam.SetValue(frame.Camera.View);
            _palmProjectionParam.SetValue(frame.Camera.Projection);
            _palmSunDirectionParam.SetValue(frame.SunDirection);
            _palmSunColorParam.SetValue(frame.SunColor);
            _palmZenithParam.SetValue(frame.ZenithLinear);
            _palmHorizonParam.SetValue(frame.HorizonLinear);
            _palmCameraParam.SetValue(frame.Camera.Position);

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
        /// One instanced draw through <c>Palm.fx</c> of a mesh part with its per-draw material —
        /// <see cref="DrawAcaciaPart"/>'s construction on the palm effect: the mesh at stream 0 and the variant's
        /// static instances (<see cref="StaticInstances"/>, uploaded once when the beach is planted) at stream 1.
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
        /// <para>
        /// <paramref name="palmShading"/> is the same shape for the same reason (#557): 1 turns on
        /// <c>Palm.fx</c>'s palm material, which reads <c>TEXCOORD0.y</c> as <see cref="PalmMesh.Part"/>'s code.
        /// Only a <see cref="PalmMesh"/> bakes that code, so every other mesh takes the default 0 and the plain
        /// shading it always had.
        /// </para>
        /// </summary>
        private void DrawPalmPart(IProceduralMesh mesh, StaticInstances instances, Vector3 diffuse,
            float dappleStrength, float swayStrength, float palmShading = 0f)
        {
            _palmDiffuseParam.SetValue(diffuse);
            _palmDappleParam.SetValue(dappleStrength);
            _palmSwayStrengthParam.SetValue(swayStrength);
            _palmShadingParam.SetValue(palmShading);
            _palmEffect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(mesh.VertexBuffer, 0, 0),
                new VertexBufferBinding(instances.Buffer, 0, 1));
            _graphicsDevice.Indices = mesh.IndexBuffer;
            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, instances.Count);
        }

        /// <summary>
        /// Draws the visible flames: one billboard per fire at its <see cref="SavannaCampfirePosition"/>, a
        /// procedural flickering flame in the shader, drawn additively and depth-read (the terrain or platform
        /// in front hides one) but writing no depth. The light each casts is a separate scene point light.
        /// Savanna scene only, drawn last with the overlays.
        /// <para>
        /// A draw per fire rather than one instanced pass: it is <see cref="FLAME_SUBFLAME_COUNT"/> quads
        /// each (#481, six triangles), eight fires at most, once a frame and only in this scene — and the
        /// alternative is an instance buffer and a vertex format for a quad that already has neither. What
        /// varies per fire is two uniforms; the sub-flames themselves are the one buffer built once.
        /// </para>
        /// </summary>
        private void DrawFlame(in SceneFrame frame)
        {
            _flameEffect.Parameters["View"].SetValue(frame.Camera.View);
            _flameEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _flameEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _flameEffect.Parameters["FlameSize"].SetValue(_savannaConfig.Campfire.FlameSize);
            _flameEffect.Parameters["FlameHeightScale"].SetValue(_savannaConfig.Campfire.FlameHeightScale);

            //⚠ ALPHA-BLENDED AND NOT ADDITIVE SINCE #468, and that is what lets a fire be RED.
            //Additive cannot make a red flame over a bright sky: the background's own green and blue
            //stay under whatever red is added to them, so a daylit savanna's fires washed to
            //yellow-white however the colour ramp was tuned - twice. The shader already returns its
            //colour PREMULTIPLIED by the coverage, which is exactly what BlendState.AlphaBlend takes
            //(One / InverseSourceAlpha), so the dense body now REPLACES what is behind it and the
            //thin edges still add. It goes on blooming, because the colours are linear radiance over 1
            //and the glare pass reads the scene target rather than the blend.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_flameVertexBuffer);
            _graphicsDevice.Indices = _flameIndexBuffer;

            //Cached out of the loop: the by-name indexer is a linear scan, and this runs once per fire per
            //frame (BestPractices.md §1). Not fields, because nothing else in this class touches them.
            EffectParameter flamePosition = _flameEffect.Parameters["FlamePosition"];
            EffectParameter flameSeed = _flameEffect.Parameters["FlameSeed"];
            EffectParameter flameTime = _flameEffect.Parameters["FlameTime"];

            _flameEffect.CurrentTechnique = _flameTechnique;
            for (int fire = 0; fire < SavannaCampfireCount; fire++)
            {
                flamePosition.SetValue(SavannaCampfirePosition(fire));

                //The same stride and rate stretch CampfireColor uses, so a flame and the light it casts are
                //the one fire rather than two things that happen to be in the same place.
                flameSeed.SetValue(1f + fire * 0.031f);
                flameTime.SetValue(frame.Time + fire * 3.77f);

                _flameEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, FLAME_SUBFLAME_COUNT * 2);
            }

            //The sparks (#468): the same per-fire uniforms over the shared spark buffer, one draw per fire.
            int sparks = Math.Clamp(_savannaConfig.Campfire.SparkCount, 0, MAX_SPARKS);
            if (sparks > 0 && _sparkVertexBuffer != null)
            {
                _flameEffect.CurrentTechnique = _sparkTechnique;
                _graphicsDevice.SetVertexBuffer(_sparkVertexBuffer);
                _graphicsDevice.Indices = _sparkIndexBuffer;
                for (int fire = 0; fire < SavannaCampfireCount; fire++)
                {
                    flamePosition.SetValue(SavannaCampfirePosition(fire));
                    flameSeed.SetValue(1f + fire * 0.031f);
                    flameTime.SetValue(frame.Time + fire * 3.77f);
                    _flameEffect.CurrentTechnique.Passes[0].Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, sparks * 2);
                }
                _flameEffect.CurrentTechnique = _flameTechnique;
            }

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
        private void DrawBackdropAtDisplayResolution(Backdrop backdrop, in SceneFrame frame, RenderTarget2D sceneTarget)
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
                backdrop.Draw(frame);
                return;
            }

            _graphicsDevice.SetRenderTarget(_backdropTarget);

            backdrop.Draw(frame);

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
        /// The aurora's current dominant colour and brightness (linear radiance), on the same slow hue-drift
        /// clock <c>Aurora.fx</c>'s own sky pass runs (<see cref="AuroraSkyConfig.DriftHueSpeed"/>) but
        /// without that shader's curtain noise or its faster brightness pulse
        /// (<see cref="AuroraSkyConfig.PulseSpeed"/>) — a flat approximation good enough for an ambient wash,
        /// not a re-derivation of what the sky pass draws pixel for pixel. Two callers share it:
        /// <see cref="AuroraBackdrop.Draw"/> (the ground's own hemisphere ambient) and, separately, a per-frame call
        /// each host adds beside its scene lights, feeding it to the scene's own
        /// <c>ForestScatterRenderer</c> planting. <b>Deliberately not routed through the daytime forest's own
        /// tint call</b> (<c>ApplySkyLighting</c>/<c>SkyLightRig.KeyTint</c>): that call only runs on a
        /// dome/scene switch or a config edit, which is right for a dome that does not move between switches
        /// but would freeze this scene's hue at whatever it happened to be the moment the scene was entered.
        /// <para>
        /// Only the slow drift crosses to C#. The fast pulse and the curtain noise stay in the shader because
        /// they exist to give the SKY per-pixel structure a player watches move; carried onto a flat ground
        /// wash they would read as the whole clearing strobing, which is not what a real aurora's glow does
        /// to the land under it — the light on the ground breathes far less than the curtains themselves do.
        /// </para>
        /// </summary>
        public Vector3 AuroraGlowColor(float wallClock) => _aurora.GlowColor(wallClock);

        public void Dispose()
        {
            foreach (Backdrop backdrop in _backdrops) backdrop?.Dispose();
            _fullScreenQuad?.Dispose();

            _backdropTarget?.Dispose();
            _backdropBatch?.Dispose();

            _farField?.Dispose();
            _gridCache.Dispose(); //every terrain grid, each once however many scenes share it (#589); the polar one was missing until #579
            DisposeTropical();
            DisposeAcacia();
            DisposeHearthStones();
            _flameVertexBuffer?.Dispose();
            _flameIndexBuffer?.Dispose();
            _sparkVertexBuffer?.Dispose();
            _sparkIndexBuffer?.Dispose();
            _sunShadowMap?.Dispose();
            _trailWarp?.Dispose();
            _birds?.Dispose();
            _snowfall?.Dispose();
            _lagoonEffect?.Dispose();
        }
    }
}
