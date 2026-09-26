using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The tropical beach (#244): a ring of sand round the island, a turquoise lagoon and the green far shore that closes
    /// the horizon, the palms, the waterline's mossy rocks and the beach's dressing standing on it, and the shared flock
    /// over the water. Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the land's effect and the
    /// lagoon's clone of <c>Sea.fx</c> each on its <see cref="TerrainPass"/>, the push, the planting, the draws, the
    /// viewpoint, the shadow map's fit, receivers and casters (the first backdrop with casters of its own), the terrain
    /// probe, and the figures the renderer's <c>TropicalPalms</c> and <c>TropicalRocks</c> forward to. See "The
    /// tropical beach" in docs/scenes.md.
    /// </summary>
    internal sealed class TropicalBackdrop : Backdrop
    {
        private TropicalSceneConfig _tropicalConfig = new();

        private readonly GraphicsDevice _graphicsDevice;

        private readonly Effect _tropicalEffect;

        //The land's camera grid and the pass that draws it (#580)
        private readonly TerrainPass _tropicalPass;

        //The lagoon's own clone of Sea.fx (#580) and its pass, over the sea's grid, which it asks the cache for
        //itself (SeaBackdrop.SEA_GRID_N over SEA_EXTENT) — the same pair of buffers, the cache's and read-only
        private readonly Effect _lagoonEffect;
        private readonly TerrainPass _lagoonPass;
        private readonly EffectParameter _lagoonPoolRadius;

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
        //TropicalSceneConfig; the backdrop reads them from _tropicalConfig. The lagoon's water is the
        //sea's own shader and grid under a clone of its own (_lagoonEffect, #580) — see DrawTropicalWater.

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

        private EffectTechnique _palmTechnique, _palmShadowTechnique;

        //The casters' map matrix on Palm.fx, cached like every other per-frame parameter (BestPractices §1)
        private EffectParameter _palmShadowViewProjection;

        /// <summary>
        /// Loads the land's effect, the lagoon's own clone of <c>Sea.fx</c> and the palms' effect, takes both grids through
        /// their <see cref="TerrainPass"/>es, pushes the config and plants the beach.
        /// </summary>
        public TropicalBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Tropical (#244): the fourteenth scene — a beach ring around the island, a turquoise lagoon
            //and the green far shore that closes the horizon. The land grid is the desert's density (the
            //gentlest slopes of any terrain scene); the lagoon's water is the sea's own shader and grid,
            //drawn over this terrain by DrawTropicalWater below through a clone of its own (#580).
            _tropicalEffect = content.Load<Effect>("Shaders/Tropical");
            _tropicalPass = new TerrainPass(Services, _tropicalEffect, TROPICAL_GRID_N, TROPICAL_EXTENT, "TropicalTime");

            ApplyTropicalParameters();

            //The lagoon: the sea's grid under its own clone of Sea.fx (#580), its water pushed once
            _lagoonEffect = content.Load<Effect>("Shaders/Sea").Clone();
            _lagoonPass = new TerrainPass(Services, _lagoonEffect, SeaBackdrop.SEA_GRID_N, SeaBackdrop.SEA_EXTENT, "SeaTime");
            _lagoonPoolRadius = _lagoonEffect.Parameters["FunnelPoolRadius"];
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
            _palmShadowViewProjection = _palmEffect.Parameters["ShadowViewProjection"];
            BuildTropicalBuffers();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Tropical;

        /// <inheritdoc/>
        public override SceneConfig Config => _tropicalConfig;

        /// <summary>The flock this scene draws; the renderer sizes the shared flock off its count.</summary>
        public BirdsConfig Birds => _tropicalConfig.Birds;

        /// <summary><see cref="SceneRenderer.TropicalPalms"/>.</summary>
        public IReadOnlyList<PlantFigure> Palms => _palmFigures;

        /// <summary><see cref="SceneRenderer.TropicalRocks"/>.</summary>
        public IReadOnlyList<PlantFigure> Rocks => _tropicalRockFigures;

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
        /// (Re)builds the tropical scatter: the palm variants and the waterline's rock variants, and each
        /// one's per-variant instance matrices. Palms are planted only on <b>dry</b> sand (a height test
        /// against the water level, which follows the wiggling waterline) and rocks only in the band
        /// straddling it, so a shore edit re-plants the whole scatter — the same contract
        /// <see cref="SavannaBackdrop.BuildSavannaScatter"/> holds. Clumped around cluster centres with a few solos, kept
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
            Random rng = new(244 + Services.SeedOffset);

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
        /// shore or config edit re-plants the scatter) and on the backdrop's own <see cref="Dispose"/>.
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

        /// <summary>
        /// The land, the lagoon over it, what stands on the sand and the flock over the water.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            //The land first (it writes depth), then the lagoon depth-read over the bed it owns,
            //then the scatter that stands on the sand, then the flock over the water.
            DrawTropicalTerrain(frame);
            DrawTropicalWater(frame);
            DrawPalms(frame);
            DrawTropicalRocks(frame);
            DrawTropicalDressing(frame);
            Services.Birds.Draw(frame, _tropicalConfig.Birds);
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
            _tropicalPass.Draw(frame, Services.TerrainHoleRadius);
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

            float clip = terrain.ShoreRadius - terrain.CoastNoise - TROPICAL_WATERLINE_BIAS;

            //The camera, the sky, the clock, the clouds and the far fade are the pass's (#580), the clip its hole
            //radius; the drain's standing pool is the sea's alone, so here it is 0
            _lagoonPoolRadius.SetValue(0f);

            //The sea's own reasoning: depth-read, not depth-write — only the open water gives the
            //depth up, and the terrain (drawn before it, opaque) still writes and owns its own.
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _lagoonPass.Draw(frame, clip);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
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
        /// <see cref="SavannaBackdrop.DrawAcaciaPart"/>'s construction on the palm effect: the mesh at stream 0 and the variant's
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

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Out over the lagoon to the far shore's ring: the tropical scene is three bands — sand,
            //turquoise water, green shore — and a look across all three is what it is.
            //
            //⚠ OVER THE PALM TOPS, NOT AMONG THEM (#555). This stood at 8 degrees while the palms were
            //12 units tall, which put the lens a little above their crowns. At 22 units the crowns reach
            //some twenty over the sand, right where an 8-degree lens stands 2.1 stand-offs out — in the
            //middle of the palm ring — so the tour opened inside a grove. 20 degrees rides over the
            //tallest crown at that radius and keeps all three bands, with the palm tops in the foreground.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _tropicalConfig.Terrain.RingRadius, _tropicalConfig.Water.LevelY + 6f),
                2.1f, 20f, 0f, "the lagoon");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get
            {
                //The sand and the palms standing on it
                yield return _tropicalEffect;
                yield return _palmEffect;
            }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _tropicalConfig.Terrain.LevelY;
            below = _tropicalConfig.Terrain.HillHeight * 0.5f;
            above = _tropicalConfig.Terrain.HillHeight;
            return true;
        }

        /// <summary>The palms and the waterline's rocks cast: a beach is palm shadows on sand.</summary>
        public override bool HasShadowCasters => true;

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
        /// <para>
        /// The map's matrix is stated here, as the acacia's caster states it. Until just after #580 it was not: this never
        /// set <c>ShadowViewProjection</c> on <c>Palm.fx</c>, so the palms and rocks cast through whatever the receivers'
        /// push had left there — the previous frame's map — and on a moving camera the beach's shadows lagged the map by
        /// a frame's snap. Found when the draw moved into this class.
        /// </para>
        /// </summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_palmMeshes == null) return;

            _palmEffect.CurrentTechnique = _palmShadowTechnique;
            _palmShadowViewProjection.SetValue(shadowViewProjection);

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

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _tropicalEffect;
            mirror = (x, z) => TerrainMirror.Tropical(x, z, _tropicalConfig);
            return true;
        }

        /// <summary>Frees the planting and the lagoon's clone; the grids are the cache's and the other effects the content manager's.</summary>
        public override void Dispose()
        {
            DisposeTropical();
            _lagoonEffect?.Dispose();
        }
    }
}
