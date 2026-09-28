using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The meadow: rolling green hills scattered with flowers on a camera-centred grid under the dome, wind combing the grass.
    /// Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>,
    /// the push, the reduced program, the draw, the viewpoint, the shadow map's fit and receiver, and the terrain probe
    /// <c>mirrorcheck</c> reads. See "The meadow (Testbed)" in docs/scenes.md.
    /// </summary>
    internal sealed class MeadowBackdrop : Backdrop
    {
        private MeadowSceneConfig _meadowConfig = new();

        //The tier the effect's program was last picked for: the constructor's, then every OnDetailChanged
        private float _sceneDetail;

        private readonly Effect _meadowEffect;

        //What stands in the meadow (#609), on the savanna's instanced path: the planting, and the one plant
        //material every scene that plants shares (Acacia.fx, loaded once by the content manager for both)
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _plantEffect;
        private readonly EffectTechnique _plantTechnique, _plantShadowTechnique;
        private readonly EffectParameter _plantView, _plantProjection, _plantCamera, _plantSunDirection, _plantSunColor,
            _plantZenith, _plantHorizon, _plantDiffuse, _plantDiffuseDry, _plantDapple, _plantBark, _plantLeaves,
            _plantAddedLight, _plantHaze, _plantShadowViewProjection;
        private MeadowScatter _scatter;

        //Its camera grid and the pass that draws it (#580)
        private readonly TerrainPass _meadowPass;

        private const int MEADOW_GRID_N = 220;
        private const float MEADOW_EXTENT = 1200f;

        //Look/tuning parameters (hills, clearing, grass colours, ambient, haze, wind, relief) and the
        //wildflowers now live in MeadowSceneConfig (Flowers = FlowersConfig); read from _meadowConfig.

        /// <summary>
        /// Loads the effect, takes its grid through its <see cref="TerrainPass"/> and pushes the config at it, picking
        /// the program for <paramref name="sceneDetail"/> (the renderer's tier at load) as it does.
        /// </summary>
        public MeadowBackdrop(BackdropServices services, ContentManager content, float sceneDetail) : base(services)
        {
            _sceneDetail = sceneDetail;

            //--- Meadow: a smooth rolling displaced grid scattered with flowers
            _meadowEffect = content.Load<Effect>("Shaders/Meadow");
            _meadowPass = new TerrainPass(Services, _meadowEffect, MEADOW_GRID_N, MEADOW_EXTENT, "MeadowTime");

            ApplyMeadowParameters();

            _graphicsDevice = services.GraphicsDevice;
            _plantEffect = content.Load<Effect>("Shaders/Acacia");
            _plantView = _plantEffect.Parameters["View"];
            _plantProjection = _plantEffect.Parameters["Projection"];
            _plantCamera = _plantEffect.Parameters["CameraPosition"];
            _plantSunDirection = _plantEffect.Parameters["SunDirection"];
            _plantSunColor = _plantEffect.Parameters["SunColor"];
            _plantZenith = _plantEffect.Parameters["ZenithColor"];
            _plantHorizon = _plantEffect.Parameters["HorizonColor"];
            _plantDiffuse = _plantEffect.Parameters["DiffuseColor"];
            _plantDiffuseDry = _plantEffect.Parameters["DiffuseDry"];
            _plantDapple = _plantEffect.Parameters["DappleStrength"];
            _plantBark = _plantEffect.Parameters["BarkStrength"];
            _plantLeaves = _plantEffect.Parameters["LeafStrength"];
            _plantAddedLight = _plantEffect.Parameters["AddedLight"];
            _plantHaze = _plantEffect.Parameters["HorizonHazeDistance"];
            _plantShadowViewProjection = _plantEffect.Parameters["ShadowViewProjection"];
            _plantTechnique = _plantEffect.Techniques["Acacia"];
            _plantShadowTechnique = _plantEffect.Techniques["ShadowCaster"];

            _scatter = new MeadowScatter(_graphicsDevice, (x, z) => TerrainMirror.Meadow(x, z, _meadowConfig),
                MeadowScatter.DEFAULT_SEED + Services.SeedOffset);
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Meadow;

        /// <inheritdoc/>
        public override SceneConfig Config => _meadowConfig;

        /// <summary>
        /// The meadow's full field or its reduced one (#281): the grass material's clumps and blade
        /// strokes are the two near-field terms that cost, and the reduced program drops both. By technique for
        /// the reason <c>SceneRenderer.SelectDetailTechniques</c> gives.
        /// </summary>
        private void SelectMeadowTechnique() =>
            _meadowEffect.CurrentTechnique = _meadowEffect.Techniques[_sceneDetail > 0.5f ? "Meadow" : "MeadowReduced"];

        private void ApplyMeadowParameters()
        {
            SelectMeadowTechnique();

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
            _meadowEffect.Parameters["GrassTipColor"].SetValue(_meadowConfig.GrassTipColor.ToVector3());
            _meadowEffect.Parameters["GrassTipStrength"].SetValue(_meadowConfig.GrassTipStrength);
            _meadowEffect.Parameters["GrassClumpSize"].SetValue(_meadowConfig.GrassClumpSize);
            _meadowEffect.Parameters["GrassClumpStrength"].SetValue(_meadowConfig.GrassClumpStrength);
            _meadowEffect.Parameters["GrassDryPatchStrength"].SetValue(_meadowConfig.GrassDryPatchStrength);
            _meadowEffect.Parameters["GrassSheenStrength"].SetValue(_meadowConfig.GrassSheenStrength);
            _meadowEffect.Parameters["GrassTranslucency"].SetValue(_meadowConfig.GrassTranslucency);
            _meadowEffect.Parameters["FlowerDensity"].SetValue(_meadowConfig.Flowers.Density);
            _meadowEffect.Parameters["FlowerSpacing"].SetValue(_meadowConfig.Flowers.Spacing);
            _meadowEffect.Parameters["FlowerSize"].SetValue(_meadowConfig.Flowers.Size);
        }

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            _sceneDetail = sceneDetail;
            SelectMeadowTechnique();
        }

        /// <summary>
        /// Draws the meadow: the grid pinned to the camera (snapped so it does not swim), rolling green hills
        /// scattered with flowers, wind combing the grass, shadowed by the shared cloud field.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _meadowPass.Draw(frame, Services.TerrainHoleRadius);
            DrawPlanting(frame);
        }

        /// <summary>
        /// Everything planted (#609), opaque and depth-writing after the grass, one instanced draw per bucket — the
        /// savanna's own draw, lit from the same sun and dome as the field. The haze distance is stated per frame:
        /// the plant material is one effect the savanna shares, and it hazes to its own distance.
        /// </summary>
        private void DrawPlanting(in SceneFrame frame)
        {
            if (_scatter == null) return;

            _plantEffect.CurrentTechnique = _plantTechnique;
            _plantView.SetValue(frame.Camera.View);
            _plantProjection.SetValue(frame.Camera.Projection);
            _plantCamera.SetValue(frame.Camera.Position);
            _plantSunDirection.SetValue(frame.SunDirection);
            _plantSunColor.SetValue(frame.SunColor);
            _plantZenith.SetValue(frame.ZenithLinear);
            _plantHorizon.SetValue(frame.HorizonLinear);
            _plantHaze.SetValue(_meadowConfig.HorizonHazeDistance);
            _plantAddedLight.SetValue(Vector3.Zero);
            _plantLeaves.SetValue(0f);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            bool detail = _sceneDetail > 0.5f;
            foreach (ScatterBucket bucket in _scatter.Buckets)
            {
                if (bucket.DetailOnly && !detail) continue;
                if (bucket.LowOnly && detail) continue;

                _plantDiffuse.SetValue(bucket.Diffuse);
                _plantDiffuseDry.SetValue(bucket.DiffuseDry);
                _plantDapple.SetValue(bucket.Dapple);
                _plantBark.SetValue(bucket.Bark);
                _plantLeaves.SetValue(bucket.Leaves);
                _graphicsDevice.RasterizerState = bucket.Leaves > 0f ? RasterizerState.CullNone : RasterizerState.CullCounterClockwise;
                _plantEffect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(bucket.Mesh.VertexBuffer, 0, 0),
                    new VertexBufferBinding(bucket.Instances, 0, 1));
                _graphicsDevice.Indices = bucket.Mesh.IndexBuffer;
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, bucket.Mesh.PrimitiveCount, bucket.Count);
            }

            _plantLeaves.SetValue(0f);
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool HasShadowCasters => _scatter != null;

        /// <summary>The planting casts into the sun's map (#609, #469's own path): the oaks, the hedges, the bales.</summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_scatter == null) return;

            _plantEffect.CurrentTechnique = _plantShadowTechnique;
            _plantShadowViewProjection.SetValue(shadowViewProjection);

            foreach (ScatterBucket bucket in _scatter.Buckets)
            {
                if (bucket.LowOnly) continue;
                _plantLeaves.SetValue(bucket.Leaves);
                _plantEffect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(bucket.Mesh.VertexBuffer, 0, 0),
                    new VertexBufferBinding(bucket.Instances, 0, 1));
                _graphicsDevice.Indices = bucket.Mesh.IndexBuffer;
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, bucket.Mesh.PrimitiveCount, bucket.Count);
            }

            _plantLeaves.SetValue(0f);
            _plantEffect.CurrentTechnique = _plantTechnique;
        }

        /// <summary>The oaks, for the meadow's intro (#609).</summary>
        public IReadOnlyList<PlantFigure> Oaks => _scatter?.Oaks;

        /// <inheritdoc/>
        public override void Dispose()
        {
            _scatter?.Dispose();
            _scatter = null;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //THE HILLS, FROM DOWN IN THE GRASS. ⚠ This shot used to name the FLOWERS and stand 70 units
            //out at one unit over the grass, on the argument that a meadow's subject is small and that
            //any of the other scenes' distances would show nothing but green. The owner played it and
            //it is the opposite that happened: 70 units is twenty-five INSIDE the flat clearing, so the
            //lens looked down at level ground with the hills starting behind the look-at, and the first
            //establishing shot of the game was a green carpet with no horizon in it at all.
            //
            //The flowers were never showable anyway — spacing 2.2 and size 0.22 — and "so the grass can
            //be seen" is about the grass's own shading: the tips, the tufts, the wind bands and the
            //translucency, all of which read at a low raking angle and none of which reads from above.
            //So the look-at goes out ONTO the rise (past ClearingRadius, a third of the way up the
            //hills) and the elevation goes NEGATIVE, which is what actually puts the lens near the
            //ground: elevation is measured from the tour's centre, and that centre is the level's own
            //camera target up at the hanging cluster — six degrees off THAT still rides high over a
            //meadow whose ground is fourteen units below the arena plane.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing,
                    _meadowConfig.ClearingRadius + _meadowConfig.ClearingTransition * 0.55f,
                    _meadowConfig.LevelY + _meadowConfig.HillHeight * 0.30f),
                1.8f, -7f, 0f, "the hills");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get
            {
                yield return _meadowEffect; //#471, and the first chapter plays here
                yield return _plantEffect;  //and what stands on it (#609)
            }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _meadowConfig.LevelY;
            below = _meadowConfig.HillHeight * 0.5f;
            above = _meadowConfig.HillHeight;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _meadowEffect;
            mirror = (x, z) => TerrainMirror.Meadow(x, z, _meadowConfig);
            return true;
        }
    }
}
