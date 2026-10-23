using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The outback (#112): a spinifex plain with red monoliths on a camera-centred grid under the dome, the shared
    /// flock circling over it. Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>,
    /// the push, the draw, the viewpoint, the shadow map's fit and receiver, and the terrain probe <c>mirrorcheck</c>
    /// reads. See "The outback" in docs/scenes.md.
    /// </summary>
    internal sealed class OutbackBackdrop : Backdrop
    {
        private OutbackSceneConfig _outbackConfig = new();

        //The bank the island sits in (#646, #608's in this scene's own material), through the shared plant material
        private readonly PlantPass _plants;
        private IslandBermPlanting _berm;

        private readonly Effect _outbackEffect;

        //Its camera grid and the pass that draws it (#580): the skeleton this scene shared with two others
        private readonly TerrainPass _outbackPass;

        //The formations as geometry of their own (#967), drawn through the pass before its grid, which stops on them;
        //the delegate is made once, never per frame
        private OutbackRockDiscs _rockDiscs;
        private readonly EffectTechnique _rockTechnique;
        private readonly Action _drawRock;

        //The formations' cast shadows over the land (#967), baked through the effect's OutbackSunBake when the sun moves.
        //A square of SUN_BAKE_EXTENT round the arena: the far ring's reach. 2048 a side is 1.5 units a texel; Low and
        //Medium bake 1024 (one pass, at the scene's first frame and on a change of dome).
        private const float SUN_BAKE_EXTENT = 3072f;
        private const int SUN_BAKE_SIZE = 2048, SUN_BAKE_SIZE_COARSE = 1024;
        private const float SUN_BAKE_STRENGTH = 0.9f;
        private RenderTarget2D _sunBake;
        private Vector3 _bakedSun;
        private readonly EffectTechnique _bakeTechnique;
        private readonly EffectParameter _bakeArea, _bakeSun, _terrainShadowMap, _terrainShadowArea;

        //The plain's grid. It carried the monoliths until #967, which set its density (a flank falling over some
        //eight 2.5-unit cells); the formations have their own discs now (OutbackRockDiscs) and the grid only the
        //swell and the boulders' footprints it stops on, but it keeps the density its shading was tuned at. Above
        //255 a side, so the grid cache's 32-bit index buffer is load-bearing here (the mountain's lesson — a 16-bit
        //one wraps silently).
        private const int OUTBACK_GRID_N = 400;
        private const float OUTBACK_EXTENT = 1000f;

        //Look/tuning parameters (plain, monoliths, rock and ground materials, dust, shimmer) live in
        //OutbackSceneConfig; the backdrop reads them from _outbackConfig.

        /// <summary>Loads the effect, takes its grid through its <see cref="TerrainPass"/> and pushes the config at it.</summary>
        public OutbackBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            //--- Outback (#112): the desert's machinery with rock on it — the same flat lattice, displaced into
            //a near-flat spinifex plain with red monoliths standing on a jittered single-cell lattice
            _outbackEffect = content.Load<Effect>("Shaders/Outback");
            _outbackPass = new TerrainPass(Services, _outbackEffect, OUTBACK_GRID_N, OUTBACK_EXTENT, "OutbackTime");

            ApplyOutbackParameters();

            _rockTechnique = _outbackEffect.Techniques["OutbackRock"];
            _bakeTechnique = _outbackEffect.Techniques["OutbackSunBake"];
            _bakeArea = _outbackEffect.Parameters["BakeArea"];
            _bakeSun = _outbackEffect.Parameters["BakeSun"];
            _terrainShadowMap = _outbackEffect.Parameters["TerrainShadowMap"];
            _terrainShadowArea = _outbackEffect.Parameters["TerrainShadowArea"];
            _terrainShadowArea.SetValue(Vector4.Zero);
            _rockDiscs = new OutbackRockDiscs(services.GraphicsDevice, _outbackConfig);
            _bankTechnique = _outbackEffect.Techniques["OutbackBank"];
            _drawRock = () =>
            {
                DrawBank();
                _rockDiscs?.Draw(_outbackEffect, _rockTechnique);
            };

            _plants = new PlantPass(services.GraphicsDevice, content);
            _berm = new IslandBermPlanting(services.GraphicsDevice, (x, z) => TerrainMirror.Outback(x, z, _outbackConfig), BERM_SEED + Services.SeedOffset,
                earth: _outbackConfig.Surface.SoilColor.ToVector3() * 0.7f, earthDry: _outbackConfig.Surface.RockColorDeep.ToVector3(),
                cover: _outbackConfig.Surface.SoilColor.ToVector3(), coverDry: _outbackConfig.Surface.SoilColorPale.ToVector3(), coverDapple: 0.5f,
                stone: _outbackConfig.Surface.RockColorBright.ToVector3(), stones: 28,
                stoneRadius: (ArenaIsland.RADIUS + 1f, ArenaIsland.RADIUS + 6.5f), stoneSize: (0.15f, 0.6f));
            _bermStones = _berm.Buckets[IslandBermPlanting.BANK_BUCKETS..];

            //The bank itself as the plain (#983), the very mesh the planting's two halves are cut from
            _bank = IslandBerm.Build(services.GraphicsDevice, (x, z) => TerrainMirror.Outback(x, z, _outbackConfig),
                BERM_SEED + Services.SeedOffset, 0, IslandBerm.RINGS - 1);
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Outback;

        /// <inheritdoc/>
        public override SceneConfig Config => _outbackConfig;

        /// <summary>The flock this scene draws; the renderer sizes the shared flock off its count.</summary>
        public BirdsConfig Birds => _outbackConfig.Birds;

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
            _outbackEffect.Parameters["ClusterChance"].SetValue(terrain.ClusterChance);
            _outbackEffect.Parameters["ClusterHeight"].SetValue(terrain.ClusterHeight);

            _outbackEffect.Parameters["RockColorDeep"].SetValue(surface.RockColorDeep.ToVector3());
            _outbackEffect.Parameters["RockColorBright"].SetValue(surface.RockColorBright.ToVector3());
            _outbackEffect.Parameters["VarnishColor"].SetValue(surface.VarnishColor.ToVector3());
            _outbackEffect.Parameters["VarnishStrength"].SetValue(surface.VarnishStrength);
            _outbackEffect.Parameters["VarnishGloss"].SetValue(surface.VarnishGloss);
            _outbackEffect.Parameters["FlakeColor"].SetValue(surface.FlakeColor.ToVector3());
            _outbackEffect.Parameters["FlakeStrength"].SetValue(surface.FlakeStrength);
            _outbackEffect.Parameters["CaveShade"].SetValue(surface.CaveShade);
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
            _outbackEffect.Parameters["SoilBounce"].SetValue(surface.SoilBounce);

            _outbackEffect.Parameters["HazeTint"].SetValue(air.HazeTint.ToVector3());
            _outbackEffect.Parameters["DustStrength"].SetValue(air.DustStrength);
            _outbackEffect.Parameters["HazeDistance"].SetValue(MathF.Max(air.HazeDistance, 1f));
            _outbackEffect.Parameters["HorizonHazeDistance"].SetValue(air.HorizonHazeDistance);
            _outbackEffect.Parameters["HeatShimmer"].SetValue(air.HeatShimmer);
            _outbackEffect.Parameters["WindDirection"].SetValue(air.Wind.ToVector2());
        }

        /// <summary>
        /// Draws the outback (#112): the formations' discs (#967), then the grid pinned to the camera (snapped to a cell
        /// so the land does not swim) carrying the near-flat spinifex plain that stops on their footprints, and the far
        /// ring past it, all shaded per pixel by the current dome, the formations' baked shadows and the shared cloud
        /// field. Like the desert it has no point lights, so it sets none.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _rockDiscs?.Cull(frame.Camera.View * frame.Camera.Projection, Services.FineMeshes);
            _outbackPass.Draw(frame, Services.TerrainHoleRadius, _drawRock);
            if (_berm != null) _plants.Draw(frame, _bermStones, _outbackConfig.Air.HorizonHazeDistance, detail: true, Services.FineMeshes);
            Services.Birds.Draw(frame, _outbackConfig.Birds);
        }

        /// <inheritdoc/>
        public override bool HasShadowCasters => _berm != null;

        /// <summary>The bank round the island casts into the sun's map (#646).</summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_berm != null) _plants.DrawShadowCasters(shadowViewProjection, _berm.Buckets, Services.FineMeshes);
        }

        private const int BERM_SEED = 9300;

        //The bank (#983): drawn as the plain inside the terrain pass, its stones as plants; the planting's buckets still cast
        private ScatterBucket[] _bermStones;
        private IslandBerm.BermMesh _bank;
        private readonly EffectTechnique _bankTechnique;

        private void DrawBank()
        {
            if (_bank == null) return;

            EffectTechnique grid = _outbackEffect.CurrentTechnique;
            _outbackEffect.CurrentTechnique = _bankTechnique;
            _outbackEffect.CurrentTechnique.Passes[0].Apply();

            Services.GraphicsDevice.SetVertexBuffer(_bank.VertexBuffer);
            Services.GraphicsDevice.Indices = _bank.IndexBuffer;
            Services.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _bank.PrimitiveCount);

            _outbackEffect.CurrentTechnique = grid;
        }

        /// <summary>
        /// Bakes the formations' cast shadows over the land (#967) when the sun has moved since the last bake: one pass of
        /// <c>Outback.fx</c>'s <c>OutbackSunBake</c> into a square of <see cref="SUN_BAKE_EXTENT"/> round the arena, each
        /// texel walking a ray to the sun through the height field. A sun under the shadow map's own floor bakes nothing
        /// and the land takes no shadow from it, as the map's receivers do.
        /// </summary>
        public override void BeforeShadowMaps(Vector3 sunDirection)
        {
            if (sunDirection.Y <= SceneRenderer.SHADOW_MIN_SUN_HEIGHT)
            {
                _terrainShadowArea.SetValue(Vector4.Zero);
                _bakedSun = Vector3.Zero;
                return;
            }

            int size = Services.FineMeshes ? SUN_BAKE_SIZE : SUN_BAKE_SIZE_COARSE;
            if (_sunBake != null && _sunBake.Width == size && Vector3.DistanceSquared(sunDirection, _bakedSun) < 1e-8f) return;

            GraphicsDevice device = Services.GraphicsDevice;
            if (_sunBake == null || _sunBake.Width != size)
            {
                _sunBake?.Dispose();
                _sunBake = new RenderTarget2D(device, size, size, false, SurfaceFormat.HalfSingle, DepthFormat.None);
            }

            BlendState blend = device.BlendState;
            DepthStencilState depth = device.DepthStencilState;
            RasterizerState rasterizer = device.RasterizerState;
            EffectTechnique technique = _outbackEffect.CurrentTechnique;

            float corner = -SUN_BAKE_EXTENT * Constants.HALF;
            _bakeArea.SetValue(new Vector4(corner, corner, SUN_BAKE_EXTENT, 1f / size));
            _bakeSun.SetValue(Vector3.Normalize(sunDirection));
            _terrainShadowMap.SetValue((Texture2D)null);

            device.SetRenderTarget(_sunBake);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SetVertexBuffer(Services.FullScreenQuad);
            _outbackEffect.CurrentTechnique = _bakeTechnique;
            _bakeTechnique.Passes[0].Apply();
            device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            device.SetRenderTarget(null);

            _outbackEffect.CurrentTechnique = technique;
            device.BlendState = blend;
            device.DepthStencilState = depth;
            device.RasterizerState = rasterizer;

            _terrainShadowMap.SetValue(_sunBake);
            _terrainShadowArea.SetValue(new Vector4(corner, corner, 1f / SUN_BAKE_EXTENT, SUN_BAKE_STRENGTH));
            _bakedSun = sunDirection;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _berm?.Dispose();
            _bank?.Dispose();
            _bank = null;
            _berm = null;
            _rockDiscs?.Dispose();
            _rockDiscs = null;
            _sunBake?.Dispose();
            _sunBake = null;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The monoliths. They stand alone on a flat plain with nothing between them, which is exactly
            //the arrangement a low three-quarter look reads and an overhead one destroys.
            viewpoint = new SceneViewpoint(SceneRenderer.AtBearing(bearing, 330f, _outbackConfig.Terrain.LevelY + 30f),
                2.3f, 12f, 20f, "the monoliths");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get
            {
                yield return _outbackEffect;
                yield return _plants.Effect; //the bank round the island (#646)
            }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            //The monoliths are terrain, not props, and they are what a map here has to clear.
            groundY = _outbackConfig.Terrain.LevelY;
            below = _outbackConfig.Terrain.OutcropHeight;
            above = _outbackConfig.Terrain.RockHeight * MathF.Max(_outbackConfig.Terrain.ClusterHeight, 1f);
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _outbackEffect;
            mirror = (x, z) => TerrainMirror.Outback(x, z, _outbackConfig);
            return true;
        }
    }
}
