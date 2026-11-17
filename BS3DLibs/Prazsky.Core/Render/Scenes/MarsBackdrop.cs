using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Mars (#277): the Moon's crater field retextured rust and ochre on a camera-centred grid under an ordinary
    /// dome, and Phobos and Deimos drawn over it on the shared full-screen quad. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>, the
    /// push, the reduced program and its coarser grid (#540), the terrain and moons draws, the viewpoint, and the
    /// shadow map's fit and receiver. See "Mars" in docs/scenes.md.
    /// </summary>
    internal sealed class MarsBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private MarsSceneConfig _marsConfig = new();

        //The bank the island sits in (#646, #608's in this scene's own material), through the shared plant material
        private readonly PlantPass _plants;
        private IslandBermPlanting _berm;

        private readonly Effect _marsEffect;

        //Its camera grid and the pass that draws it (#580); a tier's crossing retakes the grid (SelectMarsTechnique, #540)
        private readonly TerrainPass _marsPass;

        //The crater field is evaluated four times a pixel (the vertex tap plus the normal's three taps),
        //the Moon's own reason for its grid density; Mars keeps it rather than the coarser desert grid.
        private const int MARS_GRID_N = 360;

        //The reduced program's grid (#540), the volcano's VOLCANO_GRID_N_REDUCED for the same reason: see SelectMarsTechnique
        private const int MARS_GRID_N_REDUCED = 256;
        private const float MARS_EXTENT = 1000f;

        //The moons pass shares the sky-replacing scenes' full-screen-quad machinery (Services.FullScreenQuad), so
        //only its two per-frame ray-reconstruction parameters are cached (BestPractices §1) — the terrain pass's
        //own are TerrainPass's.
        private EffectParameter _marsMoonsViewRayBasis, _marsMoonsCameraPosition, _sunDirection;
        private EffectTechnique _marsTerrainTechnique, _marsTerrainFull, _marsTerrainReduced, _marsMoonsTechnique;

        //THE MOONS' BOX (#1138): the quad is drawn scissored to each moon's box on the screen, not over the whole frame. A
        //full-screen pass costs what its pixels cost to raster, depth-test and start a program on, whatever the program does,
        //and over the whole sky that was nearly all the moons cost (the figures: "What each layer costs" in docs/scenes/mars.md).
        //Each box holds every ray MoonDisc's cone test lets through (MOON_CONE_SCALE of the moon's radius and the largest
        //pixel footprint), and a pixel it leaves out is one the program writes an alpha of 0 to, which the premultiplied blend
        //leaves as it was: the picture is the full screen's to the bit. The directions and radii are the ones
        //ApplyMarsParameters hands the shader.
        private Vector3 _phobosDirection, _deimosDirection;
        private float _phobosAngularRadius, _deimosAngularRadius;
        private static readonly RasterizerState SCISSORED = new() { CullMode = CullMode.None, ScissorTestEnable = true };

        /// <summary>Mars.fx's <c>MOON_CONE_SCALE</c>: the cone <c>MoonDisc</c> runs a moon's body in, over its angular radius.</summary>
        internal const float MOON_CONE_SCALE = 1.08f;

        //THE BAKE (#1100): the ground's static fields worked out once into three maps round the arena (Mars.fx's MarsBake,
        //THE BAKE there for what is in them and why the mapping is polar), which the ground reads on High and Ultra
        //(MarsTerrainBaked) wherever the lens stands within BAKE_CAMERA_REACH of the arena. Null on OpenGL ES, which does
        //not compile them.
        private readonly EffectTechnique _marsBakeTechnique, _marsTerrainBaked;
        private readonly EffectParameter _bakeAlbedoMap, _bakeShapeMap, _bakeMasksMap;
        private RenderTarget2D _bakeAlbedo, _bakeShape, _bakeMasks;
        private RenderTargetBinding[] _bakeTargets;
        private Vector3 _bakedSun;

        /// <summary>The bake's texels: <see cref="BAKE_WIDTH"/> round the bearing, <see cref="BAKE_HEIGHT"/> out along the
        /// log of the radius. A texel is 0.09 units out and 0.05 across at 30 from the arena, 0.22 and 0.15 at 100, 0.58 and
        /// 0.46 at 300. Three maps, 16 bytes a texel: 128 MiB, 171 with the mips.</summary>
        internal const int BAKE_WIDTH = 4096, BAKE_HEIGHT = 2048;

        /// <summary>The radii the bake spans (inside <see cref="BAKE_MIN_RADIUS"/> is the island) and its log's offset, which
        /// keeps the texels near the arena from shrinking below what the lens needs there.</summary>
        internal const float BAKE_MIN_RADIUS = 5f, BAKE_MAX_RADIUS = 1000f, BAKE_RADIUS_OFFSET = 20f;

        /// <summary>
        /// How far from the arena the lens may stand and still be drawn the baked ground, world units on the plain. The
        /// front end's orbit (its wide leg 33 to 47 units out at the shipped window shapes, docs/game-shell/menu-camera.md)
        /// and the play camera stand inside it. Every shot of the chapter intro's prologue stands outside it the whole way
        /// (<c>MarsBakeReachTests</c>), so no shot turns from one ground to the other in flight, and a lens a few units over
        /// the ruts is never drawn the coarser texels the bake has out there.
        /// ⚠ Only while the window is at least about 0.73 as wide as it is tall: the wide leg stands
        /// <see cref="ArenaIsland.RADIUS"/> over the sine of the Game's <c>BackdropScreen.ISLAND_FRAME_SHARE</c> of the horizontal
        /// half-angle (68.7 units at 3:4), so a narrower
        /// window (the Game's is resizable) takes it past the reach, and the lens turns grounds as it crosses on every fly-in
        /// and retreat; at 9:16 the tour's last leg starts past it too.
        /// </summary>
        internal const float BAKE_CAMERA_REACH = 70f;

        /// <summary><c>Mars.fx</c>'s <c>MarsBakePolar</c>: the mapping from a radius to the bake's rows and back.</summary>
        internal static Vector4 BakePolar
        {
            get
            {
                float span = MathF.Log((BAKE_MAX_RADIUS + BAKE_RADIUS_OFFSET) / (BAKE_MIN_RADIUS + BAKE_RADIUS_OFFSET));
                return new Vector4(BAKE_MIN_RADIUS + BAKE_RADIUS_OFFSET, BAKE_RADIUS_OFFSET, span, 1f / span);
            }
        }

        //Look/tuning parameters (clearing, craters, rust surface, dust haze, the two moons) live in
        //MarsSceneConfig; the backdrop reads them from _marsConfig.

        /// <summary>
        /// Loads the effect, takes its grid through its <see cref="TerrainPass"/>, picks the ground's program for
        /// <paramref name="sceneDetail"/> (the renderer's tier at load) and pushes the config at it.
        /// </summary>
        public MarsBackdrop(BackdropServices services, ContentManager content, float sceneDetail) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Mars (#277): the sixteenth scene - the Moon's crater field (#125) retextured rust/ochre on
            //the outback's plumbing (an ordinary dome, the shared cloud shadow, a haze-closed horizon)
            //rather than the Moon's domeless, curvature-closed one. Two techniques in one effect: the
            //terrain grid, and a small full-screen pass for Phobos and Deimos sharing the sky-replacing
            //scenes' own quad (Services.FullScreenQuad).
            _marsEffect = content.Load<Effect>("Shaders/Mars");
            _marsPass = new TerrainPass(Services, _marsEffect, MARS_GRID_N, MARS_EXTENT, null);

            //The authored ground and its reduced program; SceneDetail picks between them (SelectMarsTechnique)
            _marsTerrainFull = _marsEffect.Techniques["MarsTerrain"];
            _marsTerrainReduced = _marsEffect.Techniques["MarsTerrainReduced"];
            SelectMarsTechnique(sceneDetail);
            _marsMoonsTechnique = _marsEffect.Techniques["MarsMoons"];

            _marsMoonsViewRayBasis = _marsEffect.Parameters["ViewRayBasis"];
            _marsMoonsCameraPosition = _marsEffect.Parameters["CameraPosition"];
            _sunDirection = _marsEffect.Parameters["SunDirection"];

            //The bake (#1100): DirectX compiles it, OpenGL ES does not, and there the ground is the exact one on every tier
            _marsBakeTechnique = _marsEffect.Techniques["MarsBake"];
            _marsTerrainBaked = _marsEffect.Techniques["MarsTerrainBaked"];
            _bakeAlbedoMap = _marsEffect.Parameters["MarsBakeAlbedo"];
            _bakeShapeMap = _marsEffect.Parameters["MarsBakeShape"];
            _bakeMasksMap = _marsEffect.Parameters["MarsBakeMasks"];
            if (_marsBakeTechnique != null)
            {
                _marsEffect.Parameters["MarsBakePolar"].SetValue(BakePolar);
                _marsEffect.Parameters["MarsBakeTexel"].SetValue(new Vector2(1f / BAKE_WIDTH, 1f / BAKE_HEIGHT));
            }

            ApplyMarsParameters();

            //The rover's tracks (#745): pressed into the plain the shader draws, along MarsTracks' traverse
            RoverTracks.Push(_marsEffect, MarsTracks.TRAVERSE_PATH);

            //The clearing round the island is the plain at LevelY: the craters and the mesas stand far out past it
            _plants = new PlantPass(_graphicsDevice, content);
            _berm = new IslandBermPlanting(_graphicsDevice, (x, z) => _marsConfig.Terrain.LevelY, BERM_SEED + Services.SeedOffset,
                earth: _marsConfig.Surface.RustColor.ToVector3(), earthDry: _marsConfig.Surface.StrataColorDark.ToVector3(),
                //The plain's rust, deeper and redder than its pale pigment: measured against the plain in the issue's
                //frame, the pale one read as a tan ring round the island
                cover: _marsConfig.Surface.RustColor.ToVector3() * new Vector3(1.5f, 1.1f, 0.65f), coverDry: _marsConfig.Surface.RustColor.ToVector3(), coverDapple: 0.5f,
                stone: _marsConfig.Surface.BoulderColorBright.ToVector3(), stones: 40,
                stoneRadius: (ArenaIsland.RADIUS + 0.8f, ArenaIsland.RADIUS + 7f), stoneSize: (0.15f, 0.7f));
            _bermStones = _berm.Buckets[IslandBermPlanting.BANK_BUCKETS..];

            //The bank itself as the plain's own rust (#983), the very mesh the planting's two halves are cut from
            _bank = IslandBerm.Build(_graphicsDevice, (x, z) => _marsConfig.Terrain.LevelY, BERM_SEED + Services.SeedOffset,
                0, IslandBerm.RINGS - 1);
            _marsEffect.Parameters["BankCoverPhase"].SetValue(IslandBerm.Phase(BERM_SEED + Services.SeedOffset));
            _marsEffect.Parameters["BankCoverOn"].SetValue(1f);
            _bankTechnique = _marsEffect.Techniques["MarsBank"];
            _bankReducedTechnique = _marsEffect.Techniques["MarsBankReduced"];
            _drawBank = DrawBank;

            _rocks = PlantRocks(_marsConfig, ROCK_SEED + Services.SeedOffset);
        }

        private const int ROCK_SEED = 7750;
        //How far a stone is sunk into the plain, a share of its size
        private const float ROCK_SINK = 0.12f;

        /// <summary>The stones of the clearing (#775): see THE STONES.</summary>
        private ScatterBucket[] PlantRocks(MarsSceneConfig config, int seed)
        {
            var rng = new Random(seed);
            var meshes = new IProceduralMesh[4];
            var at = new List<ModelInstance>[meshes.Length];
            //High and Ultra's rocks off the same instances (#777, the volcano's and the Moon's): by variant and by density, the
            //ground over each pivot in Custom.z for the stone's dust
            var fine = new List<ModelInstance>[meshes.Length, FracturedRockSet.DENSITIES];
            for (int m = 0; m < meshes.Length; m++)
            {
                var mesh = VolcanoPlanting.BlockMesh(_graphicsDevice, rng);
                _rockOwned.Add(mesh);
                meshes[m] = mesh;
                at[m] = new List<ModelInstance>();
                for (int k = 0; k < FracturedRockSet.DENSITIES; k++) fine[m, k] = new List<ModelInstance>();
            }

            float ground = config.Terrain.LevelY, to = config.Terrain.ClearingRadius;
            void Scatter(int count, float sizeMin, float sizeMax)
            {
                for (int i = 0, tries = 0; i < count && tries < count * 10; tries++)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float d = MathF.Sqrt(MathHelper.Lerp(ROCKS_FROM * ROCKS_FROM, to * to, (float)rng.NextDouble()));
                    float x = MathF.Cos(a) * d, z = MathF.Sin(a) * d;
                    float size = MathHelper.Lerp(sizeMin, sizeMax, MathF.Pow((float)rng.NextDouble(), 2f));
                    float yaw = (float)rng.NextDouble() * MathHelper.TwoPi, lean = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                    var tint = new Vector4((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.2f, 0f, 0f);
                    int variant = rng.Next(meshes.Length);

                    //Off the ruts as the shader draws them, tested after every roll a stone takes (the Moon's rule, #1114), so a
                    //change to the traverse takes away or gives back the stones near it and leaves the rest of the field where it lay
                    if (RoverTracks.Distance(MarsTracks.TRAVERSE_PATH, x, z) < TRACK_CLEAR + size) continue;

                    Matrix world = Matrix.CreateScale(size) * Matrix.CreateRotationX(lean) * Matrix.CreateRotationY(yaw)
                        * Matrix.CreateTranslation(x, ground - size * ROCK_SINK, z);
                    at[variant].Add(new ModelInstance(world, tint));
                    fine[variant, FracturedRockSet.Density(size * FracturedRockSet.BLOCK_REACH)]
                        .Add(new ModelInstance(world, new Vector4(tint.X, tint.Y, size * ROCK_SINK, 0f)));
                    i++;
                }
            }
            Scatter(ROCKS_SMALL, 0.12f, 0.42f);
            Scatter(ROCKS_MEDIUM, 0.45f, 1.1f);
            Scatter(ROCKS_LARGE, 1.2f, 2.6f);

            var buckets = new List<ScatterBucket>();
            for (int m = 0; m < meshes.Length; m++)
            {
                if (at[m].Count == 0) continue;
                //The faceted block is Low and Medium's (#777); High and Ultra draw the fractured rock below. ⚠ Flagged for the finer
                //meshes, the first cut drew no stone at all on Low and Medium and the coarse block on High (found by review)
                var bucket = new ScatterBucket(_graphicsDevice, meshes[m], at[m], BASALT, BASALT_DUSTED, dapple: 0.2f, bark: 0.3f,
                    detailOnly: false, fineMeshes: false);
                _rockOwned.Add(bucket);
                buckets.Add(bucket);
            }
            //Dusted at the foot and on top with the plain's own rust
            Vector3 dust = Vector3.Lerp(config.Surface.RustColor.ToVector3(), config.Surface.RustColorPale.ToVector3(), 0.5f);
            var set = new FracturedRockSet(_graphicsDevice, fine, new List<ModelInstance>[0, FracturedRockSet.DENSITIES], _ => 0f,
                seed * 31 + 775, BASALT, BASALT_DUSTED, dust);
            _rockOwned.Add(set);
            buckets.AddRange(set.Buckets);
            return buckets.ToArray();
        }

        //The bank (#983): drawn as the plain, its stones as plants; the planting's buckets still cast
        private ScatterBucket[] _bermStones;

        //THE STONES (#775). The owner: "the rocks need tuning; they do not look like the real photographs from Mars". Every
        //rover frame (775-zimage, 775-klein) litters the plain with dark angular basalt at every size, many pebbles and few
        //boulders; the height field's own stones were low dark mounds the grid could give no silhouette, and from any distance
        //they read as dark ovals, holes in the ground. So the stones are geometry - the volcano's broken block, as the
        //outback's gibber - in three sizes by a power law, inside the clearing, where the plain is LevelY exactly (Mars has
        //no CPU copy of its craters to stand them on further out), and off the rover's traverse.
        private readonly List<IDisposable> _rockOwned = new();
        private readonly ScatterBucket[] _rocks;
        private const int ROCKS_SMALL = 1800, ROCKS_MEDIUM = 360, ROCKS_LARGE = 45;
        private const float ROCKS_FROM = ArenaIsland.RADIUS + 8f, TRACK_CLEAR = 5f;
        private static readonly Vector3 BASALT = new(0.050f, 0.046f, 0.044f), BASALT_DUSTED = new(0.17f, 0.115f, 0.085f);
        private IslandBerm.BermMesh _bank;
        private readonly EffectTechnique _bankTechnique, _bankReducedTechnique;
        private readonly Action _drawBank;

        //Inside the terrain pass, the frame's parameters pushed: the bank in the program the tier draws the plain with
        private void DrawBank()
        {
            if (_bank == null || (Layers & MarsLayer.Bank) == 0) return;

            EffectTechnique grid = _marsEffect.CurrentTechnique;
            _marsEffect.CurrentTechnique = grid == _marsTerrainReduced ? _bankReducedTechnique : _bankTechnique;
            _marsEffect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.SetVertexBuffer(_bank.VertexBuffer);
            _graphicsDevice.Indices = _bank.IndexBuffer;
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _bank.PrimitiveCount);

            _marsEffect.CurrentTechnique = grid;
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Mars;

        /// <inheritdoc/>
        public override SceneConfig Config => _marsConfig;

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

            _marsEffect.Parameters["MesaHeight"].SetValue(terrain.MesaHeight);
            _marsEffect.Parameters["MesaInnerRadius"].SetValue(terrain.MesaInnerRadius);
            _marsEffect.Parameters["MesaThreshold"].SetValue(terrain.MesaThreshold);
            _marsEffect.Parameters["StrataColorPale"].SetValue(surface.StrataColorPale.ToVector3());
            _marsEffect.Parameters["StrataColorDark"].SetValue(surface.StrataColorDark.ToVector3());
            _marsEffect.Parameters["StrataFrequency"].SetValue(surface.StrataFrequency);
            _marsEffect.Parameters["SandColor"].SetValue(surface.SandColor.ToVector3());
            _marsEffect.Parameters["SandCoverage"].SetValue(surface.SandCoverage);
            _marsEffect.Parameters["SlabColor"].SetValue(surface.SlabColor.ToVector3());
            _marsEffect.Parameters["SlabCoverage"].SetValue(surface.SlabCoverage);

            _marsEffect.Parameters["HazeTint"].SetValue(air.HazeTint.ToVector3());
            _marsEffect.Parameters["DustStrength"].SetValue(air.DustStrength);
            _marsEffect.Parameters["HorizonHazeDistance"].SetValue(air.HorizonHazeDistance);

            //Kept as well: DrawMarsMoons scissors the pass to the boxes they give (THE MOONS' BOX)
            _phobosDirection = SceneRenderer.DirectionFromElevationAzimuth(moons.PhobosElevation, moons.PhobosAzimuth);
            _phobosAngularRadius = MathHelper.ToRadians(MathF.Max(moons.PhobosAngularRadiusDegrees, 0f));
            _deimosDirection = SceneRenderer.DirectionFromElevationAzimuth(moons.DeimosElevation, moons.DeimosAzimuth);
            _deimosAngularRadius = MathHelper.ToRadians(MathF.Max(moons.DeimosAngularRadiusDegrees, 0f));

            _marsEffect.Parameters["PhobosDirection"].SetValue(_phobosDirection);
            _marsEffect.Parameters["PhobosAngularRadius"].SetValue(_phobosAngularRadius);
            _marsEffect.Parameters["PhobosColor"].SetValue(moons.PhobosColor.ToVector3());

            _marsEffect.Parameters["DeimosDirection"].SetValue(_deimosDirection);
            _marsEffect.Parameters["DeimosAngularRadius"].SetValue(_deimosAngularRadius);
            _marsEffect.Parameters["DeimosColor"].SetValue(moons.DeimosColor.ToVector3());

            _marsEffect.Parameters["MoonTransmission"].SetValue(MathHelper.Clamp(moons.Transmission, 0f, 1f));
            _marsEffect.Parameters["MoonAirlight"].SetValue(MathHelper.Clamp(moons.Airlight, 0f, 1f));
            _marsEffect.Parameters["MarsshineColor"].SetValue(moons.MarsshineColor.ToVector3());
        }

        /// <summary>
        /// Mars's full ground or its reduced one: the sand drifts, the bedrock slabs and the strata's wobble are the
        /// added noise the reduced program drops, the mesas staying on every tier. Held as a cached technique rather
        /// than looked up, since <see cref="Draw"/> assigns it every frame.
        /// </summary>
        private void SelectMarsTechnique(float sceneDetail)
        {
            bool full = FullGroundAt(sceneDetail);
            _marsTerrainTechnique = full ? _marsTerrainFull : _marsTerrainReduced;

            //And a coarser grid under the reduced program since #540, the volcano's own step: on the APU at Low,
            //360 -> 256 was 1.69 ms of Mars's frame (47 cycles, 100 %) and photographed identical, where the two
            //cuts to the ground's program that were tried beside it were not - the pebbles' lattice (1.12 ms) left
            //the plain visibly emptier, and the fourth crater octave with an octave off both reliefs (0.25) took
            //the small craters that make the field read. The pass takes its grid at MARS_GRID_N before this first runs
            //(from the constructor), so at the full tier that call retakes nothing; TerrainPass.SetGrid gives the old
            //grid back to the cache rather than disposing it.
            _marsPass.SetGrid(full ? MARS_GRID_N : MARS_GRID_N_REDUCED);
        }

        /// <summary>
        /// Whether Mars draws its full ground at this scene detail (<see cref="SceneRenderer.SceneDetail"/>): the one
        /// threshold <see cref="SelectMarsTechnique"/> picks the program and its grid by. Only the full program draws the
        /// rover's ruts (<c>Mars.fx</c>'s <c>MarsGroundFields</c>, its <c>detail</c> branch, and the baked ground's
        /// <c>MarsGroundBaked</c>, which is drawn only where the full program would be), so this is also the question whether
        /// the chapter intro may shoot them (#1115, <see cref="SceneRenderer.MarsDrawsTracks"/>).
        /// </summary>
        internal static bool FullGroundAt(float sceneDetail) => sceneDetail > 0.5f;

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail) => SelectMarsTechnique(sceneDetail);

        /// <summary>Whether the ground draws the rover's ruts: the full program does, and the baked ground that stands in for
        /// it, the reduced one keeps its budget (Mars.fx's <c>MarsGroundFields</c>, its <c>detail</c> branch).</summary>
        internal bool DrawsTracks => _marsTerrainTechnique == _marsTerrainFull;

        /// <summary>The ground and what stands on it.</summary>
        public override void Draw(in SceneFrame frame)
        {
            if ((Layers & (MarsLayer.Ground | MarsLayer.FarRing | MarsLayer.Bank)) != 0) DrawMarsTerrain(frame);
            if (_berm != null && (Layers & MarsLayer.Stones) != 0)
                _plants.Draw(frame, _bermStones, _marsConfig.Air.HorizonHazeDistance, detail: true, Services.FineMeshes);
            if ((Layers & MarsLayer.Rocks) != 0) _plants.Draw(frame, _rocks, _marsConfig.Air.HorizonHazeDistance, detail: true, Services.FineMeshes);
        }

        /// <summary>
        /// The layers drawn (#1102): a measurement dial (<see cref="SceneRenderer.MarsLayers"/>, the Testbed's <c>mars=</c>),
        /// <see cref="MarsLayer.All"/> for every other caller. Each layer is drawn or not on the next frame; nothing carries:
        /// <see cref="MarsLayer.Bake"/> off draws the exact ground where the baked one would be and keeps the maps, so turning it
        /// back on bakes nothing.
        /// </summary>
        public MarsLayer Layers { get; set; } = MarsLayer.All;

        /// <summary>
        /// Phobos and Deimos, blended over the dome. They are drawn at the far plane and depth-read, so they show only where
        /// nothing in the frame has written depth.
        /// </summary>
        public override void DrawBlended(in SceneFrame frame)
        {
            if ((Layers & MarsLayer.Moons) != 0) DrawMarsMoons(frame);
        }

        /// <inheritdoc/>
        public override bool HasShadowCasters => true;

        /// <summary>The bank round the island (#646) and the clearing's stones (#775) cast into the sun's map.</summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if ((Layers & MarsLayer.Casters) == 0) return;
            if (_berm != null) _plants.DrawShadowCasters(shadowViewProjection, _berm.Buckets, Services.FineMeshes);
            _plants.DrawShadowCasters(shadowViewProjection, _rocks, Services.FineMeshes);
        }

        private const int BERM_SEED = 9200;

        /// <inheritdoc/>
        public override void Dispose()
        {
            _berm?.Dispose();
            _berm = null;
            _bank?.Dispose();
            _bank = null;
            foreach (IDisposable owned in _rockOwned) owned.Dispose();
            _rockOwned.Clear();
            DisposeBake();
        }

        /// <summary>
        /// Draws Mars (#277): the grid pinned to the camera (snapped to a cell so the land does not swim),
        /// carrying the Moon's crater field retextured rust and shaded per-pixel by the current dome and
        /// the shared cloud field — the outback's plumbing, not the Moon's domeless one. No point lights
        /// and no birds of its own, like the desert and the outback.
        /// </summary>
        private void DrawMarsTerrain(in SceneFrame frame)
        {
            //The ground technique first: the moons pass leaves its own as the current one, and the pass applies the current
            _marsEffect.CurrentTechnique = DrawsBaked(frame.Camera.Position) ? _marsTerrainBaked : _marsTerrainTechnique;
            _marsPass.DrawsGrid = (Layers & MarsLayer.Ground) != 0;
            _marsPass.DrawsFarRing = (Layers & MarsLayer.FarRing) != 0;
            _marsPass.Draw(frame, Services.TerrainHoleRadius, _drawBank);
        }

        //The bake is wanted where the full ground is drawn on the finer tiers (High and Ultra, the outback's sun bake's
        //line), and where the effect has it at all
        private bool BakeWanted => _marsBakeTechnique != null && Services.FineMeshes && _marsTerrainTechnique == _marsTerrainFull;

        /// <summary>
        /// Whether this frame's ground reads the bake (#1100): it is baked and wanted, and the lens stands within
        /// <see cref="BAKE_CAMERA_REACH"/> of the arena, and the measurement dial has not taken it out (<see cref="MarsLayer.Bake"/>).
        /// The bank round the island is drawn the exact ground either way (<see cref="DrawBank"/>): its program blends the
        /// plain's normal into its own mesh's.
        /// </summary>
        private bool DrawsBaked(Vector3 camera) =>
            _bakeAlbedo != null && BakeWanted && (Layers & MarsLayer.Bake) != 0
            && camera.X * camera.X + camera.Z * camera.Z < BAKE_CAMERA_REACH * BAKE_CAMERA_REACH;

        /// <summary>
        /// Bakes the ground's static fields (#1100) when they are wanted and the sun has moved since the last bake (the big
        /// craters' light follows it): one pass of <c>Mars.fx</c>'s <c>MarsBake</c> into the three maps at once, mipmapped,
        /// here where nothing of the frame is bound yet (the outback's sun bake's place, #967). Where the bake is not wanted
        /// (a lower tier, the reduced ground) its maps are given back, as they are when the host leaves Mars (<see cref="Leave"/>).
        /// </summary>
        public override void BeforeShadowMaps(Vector3 sunDirection)
        {
            if (!BakeWanted)
            {
                DisposeBake();
                return;
            }

            if (_bakeAlbedo != null && Vector3.DistanceSquared(sunDirection, _bakedSun) < 1e-8f) return;

            if (_bakeAlbedo == null)
            {
                _bakeAlbedo = new RenderTarget2D(_graphicsDevice, BAKE_WIDTH, BAKE_HEIGHT, true, SurfaceFormat.Color, DepthFormat.None);
                _bakeShape = new RenderTarget2D(_graphicsDevice, BAKE_WIDTH, BAKE_HEIGHT, true, SurfaceFormat.HalfVector4, DepthFormat.None);
                _bakeMasks = new RenderTarget2D(_graphicsDevice, BAKE_WIDTH, BAKE_HEIGHT, true, SurfaceFormat.Color, DepthFormat.None);
                _bakeTargets = new[] { new RenderTargetBinding(_bakeAlbedo), new RenderTargetBinding(_bakeShape), new RenderTargetBinding(_bakeMasks) };
            }

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState rasterizer = _graphicsDevice.RasterizerState;
            EffectTechnique technique = _marsEffect.CurrentTechnique;

            _bakeAlbedoMap.SetValue((Texture2D)null);
            _bakeShapeMap.SetValue((Texture2D)null);
            _bakeMasksMap.SetValue((Texture2D)null);
            _sunDirection.SetValue(sunDirection);

            _graphicsDevice.SetRenderTargets(_bakeTargets);
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _marsEffect.CurrentTechnique = _marsBakeTechnique;
            _marsBakeTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            _graphicsDevice.SetRenderTarget(null);

            _marsEffect.CurrentTechnique = technique;
            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;

            _bakeAlbedoMap.SetValue(_bakeAlbedo);
            _bakeShapeMap.SetValue(_bakeShape);
            _bakeMasksMap.SetValue(_bakeMasks);
            _bakedSun = sunDirection;
        }

        /// <summary>The host has turned to another scene or stopped drawing Mars's scenery: the bake's 171 MiB are given back,
        /// and baked again, in one frame, when Mars is next drawn where it is wanted.</summary>
        public override void Leave() => DisposeBake();

        private void DisposeBake()
        {
            if (_bakeAlbedo == null) return;

            _bakeAlbedoMap.SetValue((Texture2D)null);
            _bakeShapeMap.SetValue((Texture2D)null);
            _bakeMasksMap.SetValue((Texture2D)null);
            _bakeAlbedo.Dispose();
            _bakeShape.Dispose();
            _bakeMasks.Dispose();
            _bakeAlbedo = _bakeShape = _bakeMasks = null;
            _bakeTargets = null;
        }

        /// <summary>
        /// Draws Phobos and Deimos: two small analytic discs on space's shared full-screen quad
        /// (<see cref="BackdropServices.FullScreenQuad"/>), depth-read against the depth <see cref="DrawMarsTerrain"/> just wrote —
        /// Moon.fx's own measured reason (<c>MoonBackdrop.Draw</c>'s doc) for reading depth after the ground
        /// rather than before it, carried over even though this pass is far cheaper than a starfield.
        /// Alpha-blended, unlike every sky-replacing scene's opaque quad pass: this composites two small
        /// discs over a dome and a terrain that are already drawn, not a full-screen backdrop of its own.
        /// <para>
        /// Scissored to each moon's box on the screen (THE MOONS' BOX, #1138), one draw a box, or one over both where the two
        /// boxes meet so no pixel is blended twice; nothing is drawn while neither moon is in the frame. With
        /// <see cref="MarsLayer.MoonBounds"/> off (the Testbed's <c>mars=all/-moonbounds</c>) the quad covers the whole frame,
        /// the same picture.
        /// </para>
        /// </summary>
        private void DrawMarsMoons(in SceneFrame frame)
        {
            _marsMoonsViewRayBasis.SetValue(SkyRay.Basis(frame.Camera));
            _marsMoonsCameraPosition.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);

            bool bounded = (Layers & MarsLayer.MoonBounds) != 0;
            Rectangle phobos = Rectangle.Empty, deimos = Rectangle.Empty;
            if (bounded)
            {
                Viewport viewport = _graphicsDevice.Viewport;
                Matrix view = frame.Camera.View;
                Matrix projection = frame.Camera.Projection;
                float footprint = SkyRay.MaxPixelAngle(projection, viewport);
                phobos = MoonBox(_phobosDirection, _phobosAngularRadius, footprint, view, projection, viewport);
                deimos = MoonBox(_deimosDirection, _deimosAngularRadius, footprint, view, projection, viewport);

                if (HasArea(phobos) && HasArea(deimos) && phobos.Intersects(deimos))
                {
                    phobos = Rectangle.Union(phobos, deimos);
                    deimos = Rectangle.Empty;
                }
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            if (!bounded || HasArea(phobos) || HasArea(deimos))
            {
                _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                _graphicsDevice.RasterizerState = bounded ? SCISSORED : RasterizerState.CullNone;

                _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
                _marsEffect.CurrentTechnique = _marsMoonsTechnique;
                _marsEffect.CurrentTechnique.Passes[0].Apply();
                if (!bounded) _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
                else
                {
                    DrawScissored(phobos);
                    DrawScissored(deimos);
                }
            }

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        //One moon's box: the pixels whose rays MoonDisc's cone test can let through. Empty for a moon of no size, which the
        //shader does not draw either
        private static Rectangle MoonBox(Vector3 direction, float angularRadius, float footprint, Matrix view, Matrix projection,
            Viewport viewport) =>
            angularRadius > 0f
                ? SkyRay.PixelsWithin(direction, angularRadius * MOON_CONE_SCALE + footprint, view, projection, viewport)
                : Rectangle.Empty;

        private static bool HasArea(Rectangle box) => box.Width > 0 && box.Height > 0;

        private void DrawScissored(Rectangle box)
        {
            if (!HasArea(box)) return;
            _graphicsDevice.ScissorRectangle = box;
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Phobos, placed the same designer-facing way the shader places it. The bigger of the two
            //moons and the higher-contrast one: Deimos is under half its angular size.
            viewpoint = new SceneViewpoint(
                SceneRenderer.DirectionFromElevationAzimuth(_marsConfig.Moons.PhobosElevation, _marsConfig.Moons.PhobosAzimuth) * 700f,
                1.9f, 16f, 180f, "Phobos");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get
            {
                yield return _marsEffect;
                yield return _plants.Effect; //the bank round the island (#646)
            }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _marsConfig.Terrain.LevelY;
            below = _marsConfig.Terrain.CraterAmplitude * 2f;
            above = _marsConfig.Terrain.MesaHeight * 0.5f;
            return true;
        }
    }
}
