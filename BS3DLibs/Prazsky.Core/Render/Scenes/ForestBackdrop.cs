using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest: a mossy, needle-strewn clearing ringed by wooded hills on a camera-centred grid under the dome.
    /// Its trees, boulders and stumps are not this backdrop's: they are the hosts' <see cref="ForestScatterRenderer"/>,
    /// drawn and cast into the sun's map by the host, standing on the floor this draws through <see cref="TerrainMirror.Forest"/>.
    /// Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>,
    /// the push, the reduced program, the draw, the viewpoint, the shadow map's fit and receiver, and the terrain probe
    /// <c>mirrorcheck</c> reads. See "The forest" in docs/scenes.md.
    /// </summary>
    internal sealed class ForestBackdrop : Backdrop
    {
        private ForestSceneConfig _forestConfig = new();

        //The tier the effect's program was last picked for: the constructor's, then every OnDetailChanged
        private float _sceneDetail;

        private readonly Effect _forestEffect;

        //Its camera grid and the pass that draws it (#580)
        private readonly TerrainPass _forestPass;

        private const int FOREST_GRID_N = 220;
        private const float FOREST_EXTENT = 1200f;

        //Look/tuning parameters (hills, clearing, forest floor colours, treeline, ambient, haze, wind, needle
        //relief, floor lumps) live in ForestSceneConfig; read from _forestConfig. Its Trees/Rocks/Stumps
        //describe the scattered objects, which are ForestScatterRenderer's instanced draws rather than this
        //scene's (they were the Game's alone until #75) - only TerrainMirror.Forest is shared with them,
        //so they stand on the floor this shader draws.

        /// <summary>
        /// Loads the effect, takes its grid through its <see cref="TerrainPass"/> and pushes the config at it, picking
        /// the program for <paramref name="sceneDetail"/> (the renderer's tier at load) as it does.
        /// </summary>
        public ForestBackdrop(BackdropServices services, ContentManager content, float sceneDetail) : base(services)
        {
            _sceneDetail = sceneDetail;

            //--- Forest: a mossy needle-strewn clearing ringed by wooded hills (the eighth scene)
            _forestEffect = content.Load<Effect>("Shaders/Forest");
            _forestPass = new TerrainPass(Services, _forestEffect, FOREST_GRID_N, FOREST_EXTENT, "ForestTime");

            ApplyForestParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Forest;

        /// <inheritdoc/>
        public override SceneConfig Config => _forestConfig;

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
            _forestEffect.Parameters["LitterColor"].SetValue(_forestConfig.LitterColor.ToVector3());
            _forestEffect.Parameters["LitterColorDark"].SetValue(_forestConfig.LitterColorDark.ToVector3());
            _forestEffect.Parameters["EarthColor"].SetValue(_forestConfig.EarthColor.ToVector3());
            _forestEffect.Parameters["UndergrowthColor"].SetValue(_forestConfig.UndergrowthColor.ToVector3());
            _forestEffect.Parameters["DryGrassColor"].SetValue(_forestConfig.DryGrassColor.ToVector3());
            _forestEffect.Parameters["MossCoverage"].SetValue(_forestConfig.MossCoverage);
            _forestEffect.Parameters["UndergrowthCoverage"].SetValue(_forestConfig.UndergrowthCoverage);
            _forestEffect.Parameters["DryGrassCoverage"].SetValue(_forestConfig.DryGrassCoverage);
            _forestEffect.Parameters["MossHeight"].SetValue(_forestConfig.MossHeight);
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

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            _sceneDetail = sceneDetail;
            SelectForestTechnique();
        }

        /// <inheritdoc/>
        public override void Draw(in SceneFrame frame)
        {
            _forestPass.Draw(frame, Services.TerrainHoleRadius);
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The tree line at crown height, near enough that a trunk is a trunk. The forest's scatter
            //starts just outside the clearing, so this is where the trees actually are.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _forestConfig.ClearingRadius + 45f, _forestConfig.LevelY + 14f),
                1.7f, 10f, 0f, "the tree line");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get { yield return _forestEffect; } //its floor; the trees receive through the shared effect
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _forestConfig.LevelY;
            below = _forestConfig.HillHeight * 0.5f;
            above = _forestConfig.HillHeight;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _forestEffect;
            mirror = (x, z) => TerrainMirror.Forest(x, z, _forestConfig);
            return true;
        }
    }
}
