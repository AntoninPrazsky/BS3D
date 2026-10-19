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
        private readonly PlantPass _plants;
        private MeadowScatter _scatter;

        //Its camera grid and the pass that draws it (#580)
        private readonly TerrainPass _meadowPass;

        //The brook's groove and the pond's crater (#892): the patch that draws the cut, which the camera grid is far too
        //coarse to carry, and the water in it; drawn through the meadow's own effect by the programs resolved here once
        //(BestPractices §1). See MeadowCarve for the cut and MeadowPatchGeometry for the meshes.
        private MeadowPatch _patch;

        //The lens' frustum for the patch's chunks (#924): a class, so held and re-pointed rather than built per frame
        private readonly BoundingFrustum _patchFrustum = new(Matrix.Identity);
        private readonly EffectTechnique _carvedTechnique, _carvedReducedTechnique, _carvedDepthTechnique, _bedTechnique, _waterTechnique;

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
            _carvedTechnique = _meadowEffect.Techniques["MeadowCarved"];
            _carvedReducedTechnique = _meadowEffect.Techniques["MeadowCarvedReduced"];
            _carvedDepthTechnique = _meadowEffect.Techniques["MeadowCarvedDepth"];
            _bedTechnique = _meadowEffect.Techniques["MeadowBed"];
            _waterTechnique = _meadowEffect.Techniques["MeadowWater"];

            ApplyMeadowParameters();

            _graphicsDevice = services.GraphicsDevice;
            _patch = new MeadowPatch(_graphicsDevice, _meadowConfig);
            _plants = new PlantPass(_graphicsDevice, content);

            _scatter = new MeadowScatter(_graphicsDevice, _meadowConfig, MeadowScatter.DEFAULT_SEED + Services.SeedOffset);
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
            _meadowEffect.Parameters["PathBearing"].SetValue(_meadowConfig.PathBearing);
            _meadowEffect.Parameters["PathWidth"].SetValue(_meadowConfig.PathWidth);
            _meadowEffect.Parameters["PathMeander"].SetValue(_meadowConfig.PathMeander);
            _meadowEffect.Parameters["BrookBearing"].SetValue(_meadowConfig.BrookBearing);
            _meadowEffect.Parameters["BrookWidth"].SetValue(_meadowConfig.BrookWidth);
            _meadowEffect.Parameters["BrookMeander"].SetValue(_meadowConfig.BrookMeander);
            //The pond the brook runs into and the knoll the path climbs to its tree (#609's third round)
            (float pondX, float pondZ) = MeadowPath.PondCentre(_meadowConfig);
            _meadowEffect.Parameters["PondCentre"].SetValue(new Vector2(pondX, pondZ));
            _meadowEffect.Parameters["PondRadius"].SetValue(_meadowConfig.PondRadius);
            _meadowEffect.Parameters["PondLevel"].SetValue(TerrainMirror.MeadowNatural(pondX, pondZ, _meadowConfig));
            (float knollX, float knollZ) = MeadowPath.PathEnd(_meadowConfig);
            _meadowEffect.Parameters["KnollCentre"].SetValue(new Vector2(knollX, knollZ));
            _meadowEffect.Parameters["KnollRadius"].SetValue(_meadowConfig.KnollRadius);
            _meadowEffect.Parameters["KnollHeight"].SetValue(_meadowConfig.KnollHeight);

            //Where the camera grid's vertices fall, in cells (#892): it is snapped to whole cells round the camera, so its
            //vertices are always at (k + this) cells, and the patch eases onto exactly the triangles it draws
            _meadowEffect.Parameters["CoarseGridPhase"].SetValue(ShaderMath.Frac((MEADOW_GRID_N - 1) * 0.5f));
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
            DrawCut(frame.Camera);
            DrawPlanting(frame);
        }

        /// <summary>
        /// The brook's groove and the pond's crater (#892), into the hole the camera grid left for them: the pass has set
        /// the camera, the sky, the clouds and the far fade on the effect, and left the far ring's clip off. Opaque and
        /// uncull'd as the grid is, and after it, its chunks nearest the lens first: its program never discards, so the
        /// early depth test throws away what the grid or a nearer bank already hides. A chunk outside the lens' frustum is not
        /// drawn at all, and on the tiers that supersample the depth is laid first so that every pixel is shaded once (#924,
        /// <see cref="MeadowPatch.DrawGround"/>). On every tier the meadow draws (the Potato path has its own ground); its
        /// cost is in "The brook's groove and the pond's crater" in docs/scenes/meadow.md.
        /// </summary>
        private void DrawCut(Prazsky.Core.Camera.ICamera camera)
        {
            if (_patch == null) return;

            _patchFrustum.Matrix = camera.View * camera.Projection;

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _patch.DrawGround(_graphicsDevice, _carvedDepthTechnique.Passes[0], (_sceneDetail > 0.5f ? _carvedTechnique : _carvedReducedTechnique).Passes[0],
                _bedTechnique.Passes[0], camera.Position, _patchFrustum, Services.SupersampleFactor > 1);
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The water in the cut (#892): a surface of its own at the water's level, after the planting so a reed's foot is
        /// under it, blended over the cut's bed and reading the depth without writing it, so the banks hide its edge. The
        /// effect still holds this frame's camera, sky and clouds from <see cref="Draw"/>.
        /// </summary>
        public override void DrawGrounded(in SceneFrame frame)
        {
            if (_patch == null) return;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _waterTechnique.Passes[0].Apply();
            _patch.DrawWater(_graphicsDevice);
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Everything planted (#609), opaque and depth-writing after the grass, one instanced draw per bucket — the
        /// savanna's own draw, lit from the same sun and dome as the field. The haze distance is stated per frame:
        /// the plant material is one effect the savanna shares, and it hazes to its own distance.
        /// </summary>
        private void DrawPlanting(in SceneFrame frame)
        {
            if (_scatter == null) return;
            _plants.Draw(frame, _scatter.Buckets, _meadowConfig.HorizonHazeDistance, _sceneDetail > 0.5f, Services.FineMeshes);
        }

        /// <inheritdoc/>
        public override bool HasShadowCasters => _scatter != null;

        /// <summary>The planting casts into the sun's map (#609, #469's own path): the oaks, the hedges, the bales.</summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_scatter == null) return;
            _plants.DrawShadowCasters(shadowViewProjection, _scatter.Buckets, Services.FineMeshes);
        }

        /// <summary>The old trees, the bench, the bales and the low solids, for a camera to point at and keep out of (#609, #893).</summary>
        public MeadowFigures Figures => _scatter?.Figures;

        /// <inheritdoc/>
        public override void Dispose()
        {
            _scatter?.Dispose();
            _scatter = null;
            _patch?.Dispose();
            _patch = null;
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
                yield return _plants.Effect;  //and what stands on it (#609)
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
