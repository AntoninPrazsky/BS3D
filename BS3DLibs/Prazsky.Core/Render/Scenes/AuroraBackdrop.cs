using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The aurora (#205, rebuilt from the ground up in #963): a moonlit snowfield in a boreal clearing under a great folded
    /// aurora. Owns <c>Aurora.fx</c> and everything it draws with: the two terrain grids, the planting the hosts' wood is
    /// drawn from (<see cref="Planting"/>), the snow drifted round it (<see cref="AuroraMoundMap"/>), the far stand on the
    /// hills, the arcs of the aurora and the light they throw, the Moon and its shadow, the mist, and the falling snow.
    /// See docs/scenes/aurora.md.
    /// </summary>
    internal sealed class AuroraBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly AuroraSceneConfig _config = new();

        private readonly Effect _effect;
        private readonly EffectTechnique _snowFine, _snowCoarse, _snowFineReduced, _snowCoarseReduced, _stand, _sky;

        //Per-frame parameters, resolved once (BestPractices §1)
        private readonly EffectParameter _view, _projection, _viewRayBasis, _cameraPosition, _time, _originXZ, _gridCell,
            _holeRadius, _fineOriginXZ, _fineHalfExtent, _coarseOriginXZ, _auroraLightDirection, _auroraLight, _arcPlace,
            _supersample, _fogDrift;

        //The fine grid round the camera carries the snow's drifts and the mounds round the trunks as geometry; the coarse
        //one runs out to the hills and sinks under the fine one wherever it draws (see Aurora.fx's SnowVS). Neither clips.
        private const int FINE_GRID_N = 481;
        private const float FINE_EXTENT = 360f;
        private const int COARSE_GRID_N = 220;
        private const float COARSE_EXTENT = 1200f;
        private readonly VertexBuffer _fineVertices, _coarseVertices;
        private readonly IndexBuffer _fineIndices, _coarseIndices;
        private readonly int _fineIndexCount, _coarseIndexCount;

        //The planting and the snow drifted round it
        private readonly ForestScatter _planting;
        private readonly AuroraMoundMap _mounds;
        private readonly Texture2D _moundTexture;

        //The far stand: one spire mesh, a few thousand instances
        private readonly AuroraStandMesh _standMesh;
        private readonly VertexBuffer _standInstances;
        private readonly int _standCount;

        //The arcs, as the shader takes them: their places (brightness written per frame) and their shapes, fixed
        private const int ARCS = 3;
        private readonly Vector4[] _arcPlaces, _arcShapes, _arcBends;
        private readonly Vector3[] _arcCentres;
        private readonly int _arcCount;
        private readonly float _arcPeak;
        private readonly Vector3 _moonDirection;

        //The snow's own clone of Snow.fx (#580), its look pushed once; the flake buffer is the shared Services.Snowfall
        private readonly Effect _snowEffect;
        private readonly Snowfall.Look _snowLook;

        private float _sceneDetail = 1f;

        /// <summary>Loads the effect, takes its grids, plants the wood, bakes the drifts round it, plants the far stand and pushes the config.</summary>
        public AuroraBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;
            _effect = content.Load<Effect>("Shaders/Aurora");

            _snowFine = _effect.Techniques["AuroraSnowFine"];
            _snowCoarse = _effect.Techniques["AuroraSnowCoarse"];
            _snowFineReduced = _effect.Techniques["AuroraSnowFineReduced"];
            _snowCoarseReduced = _effect.Techniques["AuroraSnowCoarseReduced"];
            _stand = _effect.Techniques["AuroraStand"];
            _sky = _effect.Techniques["AuroraSky"];

            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _viewRayBasis = _effect.Parameters["ViewRayBasis"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _time = _effect.Parameters["AuroraTime"];
            _originXZ = _effect.Parameters["OriginXZ"];
            _gridCell = _effect.Parameters["GridCell"];
            _holeRadius = _effect.Parameters["IslandHoleRadius"];
            _fineOriginXZ = _effect.Parameters["FineOriginXZ"];
            _fineHalfExtent = _effect.Parameters["FineHalfExtent"];
            _coarseOriginXZ = _effect.Parameters["CoarseOriginXZ"];
            _effect.Parameters["CoarseReach"].SetValue(COARSE_EXTENT * 0.5f);
            _auroraLightDirection = _effect.Parameters["AuroraLightDirection"];
            _auroraLight = _effect.Parameters["AuroraLight"];
            _arcPlace = _effect.Parameters["ArcPlace"];
            _supersample = _effect.Parameters["SupersampleFactor"];
            _fogDrift = _effect.Parameters["SceneFogDrift"];

            Services.AcquireGridMesh(FINE_GRID_N, FINE_EXTENT, out _fineVertices, out _fineIndices, out _fineIndexCount, nearFirst: true);
            Services.AcquireGridMesh(COARSE_GRID_N, COARSE_EXTENT, out _coarseVertices, out _coarseIndices, out _coarseIndexCount);

            //The wood stands on the snow before its own drifts (AuroraBase); the drifts are then baked round where it stands
            AuroraSceneConfig config = _config;
            _planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED + Services.SeedOffset, config.Terrain,
                (x, z) => TerrainMirror.AuroraBase(x, z, config));
            _mounds = AuroraMoundMap.Bake(_planting, config);
            _moundTexture = new Texture2D(_graphicsDevice, AuroraMoundMap.SIZE, AuroraMoundMap.SIZE, false, SurfaceFormat.HalfVector4);
            _moundTexture.SetData(_mounds.Texels);

            _moonDirection = SceneRenderer.DirectionFromElevationAzimuth(
                Math.Clamp(config.Sky.MoonElevationDegrees, 1f, 89f), config.Sky.MoonAzimuthDegrees);

            _arcCount = Math.Min(config.Curtains.Arcs.Length, ARCS);
            _arcPlaces = new Vector4[ARCS];
            _arcShapes = new Vector4[ARCS];
            _arcBends = new Vector4[ARCS];
            _arcCentres = new Vector3[ARCS];
            float bearing = MathHelper.ToRadians(config.Curtains.BearingDegrees);
            for (int k = 0; k < _arcCount; k++)
            {
                AuroraArcConfig arc = config.Curtains.Arcs[k];
                float turn = bearing + MathHelper.ToRadians(arc.AngleDegrees);

                //Aurora.fx reads a ray's place across an arc as dir.z cos(a) - dir.x sin(a), which runs along the arc's own
                //bearing (sin turn, cos turn) when a = -turn
                _arcPlaces[k] = new Vector4(arc.Offset, -turn, arc.FoldDepth, 0f);
                _arcShapes[k] = new Vector4(arc.LowerEdge, arc.Height, arc.FoldFrequency, arc.Seed);
                _arcBends[k] = new Vector4(arc.BendDepth, arc.BendFrequency, arc.LitFrequency, 0f);
                _arcPeak += config.Curtains.Intensity * arc.Brightness;

                //Where its light comes from: the middle of the curtain, a quarter of the way up it
                Vector2 under = new Vector2(MathF.Sin(turn), MathF.Cos(turn)) * arc.Offset;
                _arcCentres[k] = Vector3.Normalize(new Vector3(under.X, arc.LowerEdge + arc.Height * 0.25f, under.Y));
            }

            _standMesh = new AuroraStandMesh(_graphicsDevice, config.FarStand);
            (_standInstances, _standCount) = PlantStand(_graphicsDevice, config);

            ApplyParameters();

            _snowEffect = content.Load<Effect>("Shaders/Snow").Clone();
            _snowLook = Snowfall.Prepare(_snowEffect, config.Snow);
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Aurora;

        /// <inheritdoc/>
        public override SceneConfig Config => _config;

        /// <summary>Where the wood stands: the hosts' <see cref="ForestScatterRenderer"/> draws this planting, so the snow is drifted round the trees that are drawn.</summary>
        public ForestScatter Planting => _planting;

        /// <summary>The ground as drawn, mounds and all (<c>Aurora.fx</c>'s <c>SnowHeight</c>): what a lens laid over the snow keeps off.</summary>
        public float GroundHeight(float x, float z) => TerrainMirror.Aurora(x, z, _config, _mounds);

        /// <summary>Everything fixed for as long as the config is: the field, the snow's look, the sky, the stars, the stand, the mist.</summary>
        private void ApplyParameters()
        {
            ForestSceneConfig terrain = _config.Terrain;
            Set("ForestLevelY", terrain.LevelY);
            Set("HillHeight", terrain.HillHeight);
            Set("ClearingRadius", terrain.ClearingRadius);
            Set("ClearingTransition", terrain.ClearingTransition);
            Set("ClearingRelief", terrain.ClearingRelief);
            Set("FloorLumpStrength", terrain.FloorLumpStrength);
            Set("FloorLumpFrequency", terrain.FloorLumpFrequency);

            AuroraSnowfieldConfig snow = _config.Snowfield;
            _effect.Parameters["DriftWind"].SetValue(TerrainMirror.SnowWind(snow));
            Set("DriftHeight", snow.DriftHeight);
            Set("DriftAlong", snow.DriftAlong);
            Set("DriftAcross", snow.DriftAcross);
            Set("BankRadius", snow.BankRadius);
            Set("BankHeight", snow.BankHeight);
            Set("BankReach", snow.BankReach);
            Set("BankLee", snow.BankLee);
            _effect.Parameters["MoundMap"].SetValue(_moundTexture);
            _effect.Parameters["MoundOrigin"].SetValue(AuroraMoundMap.ORIGIN);
            Set("MoundTexel", AuroraMoundMap.TEXEL);
            Set("MoundSize", AuroraMoundMap.SIZE);
            Set("SnowAlbedo", snow.Albedo);
            Set("SnowDeepTint", snow.DeepTint);
            Set("SastrugiStrength", snow.SastrugiStrength);
            Set("SastrugiScale", snow.SastrugiScale);
            Set("SparkleDensity", snow.SparkleDensity);
            Set("SparkleStrength", snow.SparkleStrength);

            AuroraNightSkyConfig sky = _config.Sky;
            _effect.Parameters["MoonDirection"].SetValue(_moonDirection);
            Set("MoonLight", sky.MoonLight);
            Set("SkyLightZenith", sky.SkyLightZenith);
            Set("SkyLightHorizon", sky.SkyLightHorizon);
            Set("SkyZenith", sky.Zenith);
            Set("SkyHorizon", sky.Horizon);
            Set("SkyGradientPower", sky.GradientPower);
            Set("HorizonGlow", sky.HorizonGlow);
            Set("HorizonGlowWidth", sky.HorizonGlowWidth);
            Set("MoonDisc", sky.MoonDisc);
            Set("MoonRadius", MathHelper.ToRadians(sky.MoonRadiusDegrees));
            Set("MoonHalo", sky.MoonHalo);
            Set("MoonHaloWidth", sky.MoonHaloWidth);

            AuroraCurtainsConfig curtains = _config.Curtains;
            float bearing = MathHelper.ToRadians(curtains.BearingDegrees);
            _effect.Parameters["AuroraBearing"].SetValue(new Vector2(MathF.Sin(bearing), MathF.Cos(bearing)));
            _effect.Parameters["ArcShape"].SetValue(_arcShapes);
            _effect.Parameters["ArcBend"].SetValue(_arcBends);
            Set("AuroraGreen", curtains.Green);
            Set("AuroraRed", curtains.Red);
            Set("AuroraFringe", curtains.Fringe);
            Set("AuroraRayScale", curtains.RayScale);
            Set("AuroraRayStrength", curtains.RayStrength);
            Set("AuroraRaySpeed", curtains.RaySpeed);
            Set("AuroraFoldSpeed", curtains.FoldSpeed);
            Set("AuroraEdge", curtains.EdgeSoftness);
            Set("AuroraLength", curtains.Length);

            SpaceStarsConfig stars = _config.Stars;
            _effect.Parameters["StarCellScale"].SetValue(new[] { stars.BrightCellScale, stars.MediumCellScale, stars.FaintCellScale });
            _effect.Parameters["StarChance"].SetValue(new[] { stars.BrightChance, stars.MediumChance, stars.FaintChance });
            _effect.Parameters["StarPeak"].SetValue(new[] { stars.BrightPeak, stars.MediumPeak, stars.FaintPeak });
            Set("StarSpread", stars.Spread);
            Set("StarFalloff", stars.Falloff);
            Set("StarSpikeThreshold", stars.SpikeThreshold);
            Set("StarSpikeLength", stars.SpikeLength);

            Set("StandNeedles", _config.FarStand.Needles);
            Set("StandSnow", _config.FarStand.Snow);

            PushFog(_effect, Vector2.Zero);
        }

        private void Set(string name, float value) => _effect.Parameters[name].SetValue(value);
        private void Set(string name, Rgb value) => _effect.Parameters[name].SetValue(value.ToVector3());

        /// <summary>
        /// The mist into an effect that includes <c>SceneFog.fxh</c> — the aurora's own, and the shared instanced one the
        /// renderer hands it every frame (<see cref="SceneRenderer"/>'s <c>DrawShadowMaps</c>), so the wood and the snow
        /// it stands in are veiled alike. <paramref name="drift"/> is how far the banks have blown.
        /// </summary>
        public void PushFog(Effect effect, Vector2 drift)
        {
            AuroraMistConfig mist = _config.Mist;
            EffectParameterCollection parameters = effect.Parameters;
            parameters["SceneFogColor"]?.SetValue(mist.Color.ToVector3());
            parameters["SceneFogDensity"]?.SetValue(mist.Density);
            parameters["SceneFogBaseY"]?.SetValue(mist.BaseY);
            parameters["SceneFogScaleHeight"]?.SetValue(mist.ScaleHeight);
            parameters["SceneFogStart"]?.SetValue(mist.Start);
            parameters["SceneFogHaze"]?.SetValue(mist.Haze);
            parameters["SceneFogPatchiness"]?.SetValue(mist.Patchiness);
            parameters["SceneFogDrift"]?.SetValue(drift);
        }

        /// <summary>The clock of the last frame drawn: what the renderer blows the shared effect's mist on by, a frame behind.</summary>
        public float LastTime { get; private set; }

        /// <summary>How far the mist's banks have blown by <paramref name="wallClock"/>: downwind, slowly.</summary>
        public Vector2 FogDrift(float wallClock) => -TerrainMirror.SnowWind(_config.Snowfield) * (_config.Mist.DriftSpeed * wallClock);

        /// <summary>
        /// How bright arc <paramref name="k"/> is at <paramref name="wallClock"/>: its own breath, slow, on its own phase,
        /// so the light wanders from curtain to curtain. The one clock the sky, the snow's light and the rig all read.
        /// </summary>
        private float ArcBrightness(int k, float wallClock)
        {
            AuroraCurtainsConfig curtains = _config.Curtains;
            AuroraArcConfig arc = curtains.Arcs[k];
            float phase = wallClock * curtains.PulseSpeed + arc.PulsePhase;
            float breath = 0.5f + 0.5f * MathF.Sin(phase) * (0.75f + 0.25f * MathF.Sin(phase * 0.37f + 1.3f));
            return curtains.Intensity * arc.Brightness * (1f - curtains.PulseDepth * breath);
        }

        /// <summary>
        /// The curtains' light at <paramref name="wallClock"/>: from where the arcs hang, weighted by how bright each is
        /// now, in their green. What the snow takes per frame, and what the rig's back light follows in steps.
        /// </summary>
        private void AuroraLightAt(float wallClock, out Vector3 direction, out Vector3 light, out float total)
        {
            Vector3 sum = Vector3.Zero;
            total = 0f;
            for (int k = 0; k < _arcCount; k++)
            {
                float brightness = ArcBrightness(k, wallClock);
                sum += _arcCentres[k] * brightness;
                total += brightness;
            }

            direction = sum.LengthSquared() > 1e-8f ? Vector3.Normalize(sum) : Vector3.Up;
            light = _config.Curtains.Green.ToVector3() * (total * _config.Curtains.GroundLight);
        }

        /// <summary>
        /// The tint the wood's pigments shift towards (<see cref="SceneRenderer.AuroraGlowColor"/>, the hosts'
        /// <c>ApplySkyTint</c>): mostly the Moon's white, a little of the curtains' green. Constant, so the hosts encode
        /// the wood's tints once.
        /// </summary>
        public Vector3 GlowColor(float wallClock) => Vector3.Lerp(Vector3.One, GreenAtWhite(), 0.2f);

        //The curtains' green at the brightness of white, the hue a light is carried towards
        private Vector3 GreenAtWhite()
        {
            Vector3 green = _config.Curtains.Green.ToVector3();
            return green / Math.Max(ColorSpace.Luminance(green), 1e-4f);
        }

        /// <summary>
        /// Draws the snowfield (the fine grid round the camera, then the coarse one), the far stand, and the sky depth-read
        /// behind them. The wood is the host's, drawn after this.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            float time = frame.Time;
            LastTime = time;
            Vector3 eye = frame.Camera.Position;

            for (int k = 0; k < _arcCount; k++) _arcPlaces[k].W = ArcBrightness(k, time);
            AuroraLightAt(time, out Vector3 lightDirection, out Vector3 light, out _);

            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(eye);
            _time.SetValue(time);
            _holeRadius.SetValue(Services.TerrainHoleRadius);
            _auroraLightDirection.SetValue(lightDirection);
            _auroraLight.SetValue(light);
            _arcPlace.SetValue(_arcPlaces);
            _fogDrift.SetValue(FogDrift(time));

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            bool detail = _sceneDetail > 0.5f;

            //The fine grid first, near first: everything the coarse grid would draw under it is then rejected by depth
            float fineCell = FINE_EXTENT / (FINE_GRID_N - 1);
            Vector2 fineOrigin = new(MathF.Round(eye.X / fineCell) * fineCell, MathF.Round(eye.Z / fineCell) * fineCell);
            _originXZ.SetValue(fineOrigin);
            _gridCell.SetValue(fineCell);
            _fineOriginXZ.SetValue(fineOrigin);
            _fineHalfExtent.SetValue(FINE_EXTENT * 0.5f);
            DrawGrid(detail ? _snowFine : _snowFineReduced, _fineVertices, _fineIndices, _fineIndexCount);

            float coarseCell = COARSE_EXTENT / (COARSE_GRID_N - 1);
            Vector2 coarseOrigin = new(MathF.Round(eye.X / coarseCell) * coarseCell, MathF.Round(eye.Z / coarseCell) * coarseCell);
            _originXZ.SetValue(coarseOrigin);
            _coarseOriginXZ.SetValue(coarseOrigin);
            _gridCell.SetValue(coarseCell);
            DrawGrid(detail ? _snowCoarse : _snowCoarseReduced, _coarseVertices, _coarseIndices, _coarseIndexCount);

            //The far stand, its spires wound clockwise from outside like every procedural mesh
            if (_standCount > 0)
            {
                _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                InstanceBinding.Set(_graphicsDevice, _standMesh.VertexBuffer, 0, _standInstances, 0);
                _graphicsDevice.Indices = _standMesh.IndexBuffer;
                _effect.CurrentTechnique = _stand;
                _effect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, _standMesh.PrimitiveCount, _standCount);
            }

            //Then the sky, depth-READ at the far plane: every pixel the ground or the stand owns is rejected before it runs
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _viewRayBasis.SetValue(SkyRay.Basis(frame.Camera));
            _supersample.SetValue((float)Services.SupersampleFactor);
            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _effect.CurrentTechnique = _sky;
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        private void DrawGrid(EffectTechnique technique, VertexBuffer vertices, IndexBuffer indices, int indexCount)
        {
            _graphicsDevice.SetVertexBuffer(vertices);
            _graphicsDevice.Indices = indices;
            _effect.CurrentTechnique = technique;
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indexCount / 3);
        }

        /// <summary>The falling snow, through the shared flake buffer and the aurora's own clone of <c>Snow.fx</c>.</summary>
        public override void DrawOverlays(in SceneFrame frame) => Services.Snowfall.Draw(frame, _snowLook, _config.Snow);

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail) => _sceneDetail = sceneDetail;

        /// <summary>The Moon: the key light of everything the rig lights, and the sun of the shadow map.</summary>
        public override bool TryGetSunDirection(out Vector3 direction)
        {
            direction = _moonDirection;
            return true;
        }

        /// <summary>How finely the rig's back light follows the curtains' breath: their total brightness in this many steps of its range.</summary>
        private const float RIG_STEPS = 24f;

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            //The Moon is the key, steady; the snow's bounce from below; the curtains' green from behind and above, its
            //brightness following theirs in steps (a host re-lights only on a step, SkyLightRig.StepSceneLight, and its
            //walk over the renderers allocates), between 0.7 and 1.3 of the authored back light so nothing strobes
            AuroraLightingConfig lighting = _config.Lighting;
            AuroraLightAt(wallClock, out _, out _, out float total);
            float share = _arcPeak > 1e-4f ? MathF.Round(Math.Clamp(total / _arcPeak, 0f, 1f) * RIG_STEPS) / RIG_STEPS : 1f;

            rig = new SceneLightRig(
                TowardsHue(lighting.SkyAmbient.ToVector3(), GreenAtWhite(), lighting.GlowTint),
                lighting.GroundAmbient.ToVector3(),
                lighting.KeyTint.ToVector3(),
                lighting.BackTint.ToVector3() * (0.7f + 0.6f * share));
            return true;
        }

        //Carries a light part of the way to a hue at its own brightness: its colour, not more or less light
        private static Vector3 TowardsHue(Vector3 light, Vector3 hue, float strength)
        {
            float hueLuminance = ColorSpace.Luminance(hue);
            if (hueLuminance <= 1e-4f) return light;

            return Vector3.Lerp(light, hue * (ColorSpace.Luminance(light) / hueLuminance), strength);
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Over the treeline, out past the clearing, so a lens without a prologue looks along the spruce tips with the
            //curtains over them (#531). The Game opens the aurora on its own prologue (AuroraIntroShots).
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _config.Terrain.ClearingRadius + 60f,
                    _config.Terrain.LevelY + _config.Terrain.HillHeight + 30f),
                1.9f, 10f, 0f, "the aurora");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get { yield return _effect; } //the snow and the stand; the wood receives through the shared effect
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _config.Terrain.LevelY;
            below = _config.Terrain.HillHeight * 0.5f;
            above = _config.Terrain.HillHeight;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _effect;
            mirror = GroundHeight;
            return true;
        }

        /// <summary>Frees the snow's clone, the mound map and the far stand. The grids are the cache's and the effect the content manager's.</summary>
        public override void Dispose()
        {
            _snowEffect?.Dispose();
            _moundTexture?.Dispose();
            _standMesh?.Dispose();
            _standInstances?.Dispose();
        }

        #region The far stand

        /// <summary>A far spruce's instance: its foot and scale, then its yaw's sine and cosine, its height's stretch and its needles' tint.</summary>
        private readonly struct StandInstance : IVertexType
        {
            public readonly Vector4 Place;
            public readonly Vector4 Look;

            public StandInstance(Vector4 place, Vector4 look)
            {
                Place = place;
                Look = look;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        //The far stand's planting: spread over the ring by area, left open where a glade field says so, planted on the
        //snow (AuroraBase) a little sunk, nearest the arena first so the depth test rejects what the near ones hide
        private static (VertexBuffer, int) PlantStand(GraphicsDevice device, AuroraSceneConfig config)
        {
            AuroraFarStandConfig stand = config.FarStand;
            var rng = new Random(stand.Seed);
            var planted = new List<StandInstance>(stand.Count);

            float inner2 = stand.InnerRadius * stand.InnerRadius, outer2 = stand.OuterRadius * stand.OuterRadius;
            for (int attempt = 0; attempt < stand.Count * 3 && planted.Count < stand.Count; attempt++)
            {
                float r = MathF.Sqrt(MathHelper.Lerp(inner2, outer2, (float)rng.NextDouble()));
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;

                //Glades: open ground where a broad field is low
                float glade = 0.5f + 0.8f * ShaderMath.Noise(new Vector2(x * 0.011f + 31.7f, z * 0.011f + 5.3f));
                if (glade < stand.Glades) continue;

                float scale = MathHelper.Lerp(stand.MinScale, stand.MaxScale, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float stretch = 0.85f + 0.35f * (float)rng.NextDouble();
                float tint = 0.8f + 0.4f * (float)rng.NextDouble();
                float y = TerrainMirror.AuroraBase(x, z, config) - 0.4f * scale;

                planted.Add(new StandInstance(new Vector4(x, y, z, scale), new Vector4(MathF.Sin(yaw), MathF.Cos(yaw), stretch, tint)));
            }

            planted.Sort((p, q) => (p.Place.X * p.Place.X + p.Place.Z * p.Place.Z).CompareTo(q.Place.X * q.Place.X + q.Place.Z * q.Place.Z));

            var buffer = new VertexBuffer(device, StandInstance.Declaration, Math.Max(planted.Count, 1), BufferUsage.WriteOnly);
            if (planted.Count > 0) buffer.SetData(planted.ToArray());
            return (buffer, planted.Count);
        }

        #endregion
    }
}
