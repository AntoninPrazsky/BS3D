using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The desert: Sahara dunes on a camera-centred grid under the dome, the shared flock circling over them.
    /// Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>,
    /// the push, the draw, the viewpoint, the shadow map's fit and receiver, and the terrain probe <c>mirrorcheck</c>
    /// reads. See "The desert (Testbed)" in docs/scenes.md.
    /// </summary>
    internal sealed class DesertBackdrop : Backdrop
    {
        private DesertSceneConfig _desertConfig = new();

        private readonly Effect _desertEffect;

        //Its camera grid and the pass that draws it (#580): the skeleton this scene shared with two others
        private readonly TerrainPass _desertPass;

        //The dunes are real geometry: a camera-centred grid of this many vertices per side over this world
        //extent, displaced in the shader and snapped to a cell so they do not swim. Finer than the old dune
        //grid (200) so the crest silhouettes read smooth; the shading normal is per-pixel, so no grid shows.
        private const int DESERT_GRID_N = 360;
        private const float DESERT_EXTENT = 1000f;

        //Look/tuning parameters (dune height, clearing, ripples, dust, sand colour, wind, haze) now live in
        //DesertSceneConfig; the backdrop reads them from _desertConfig.

        /// <summary>Loads the effect, takes its grid through its <see cref="TerrainPass"/> and pushes the config at it.</summary>
        public DesertBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            //--- Desert: a flat lattice the shader displaces into Sahara dunes (per-pixel normal, no grid)
            _desertEffect = content.Load<Effect>("Shaders/Desert");
            _desertPass = new TerrainPass(Services, _desertEffect, DESERT_GRID_N, DESERT_EXTENT, "DesertTime");

            ApplyDesertParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Desert;

        /// <inheritdoc/>
        public override SceneConfig Config => _desertConfig;

        /// <summary>The flock this scene draws; the renderer sizes the shared flock off its count.</summary>
        public BirdsConfig Birds => _desertConfig.Birds;

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
            _desertEffect.Parameters["SandBounce"].SetValue(_desertConfig.SandBounce);
            _desertEffect.Parameters["HazeWarmth"].SetValue(_desertConfig.HazeWarmth);
            _desertEffect.Parameters["WindDirection"].SetValue(_desertConfig.Wind.ToVector2());
            _desertEffect.Parameters["HorizonHazeDistance"].SetValue(_desertConfig.HorizonHazeDistance);
        }

        /// <summary>
        /// Draws the Sahara dune field: the grid pinned to the camera (snapped to a cell so the dunes do not
        /// swim), lifted into dunes with distance and shaded per-pixel (no grid) by the current dome, ripples
        /// and blown dust crawling on the wind, shadowed by the shared cloud field. The desert has no point
        /// lights, so unlike the savanna it sets none.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _desertPass.Draw(frame, Services.TerrainHoleRadius);
            Services.Birds.Draw(frame, _desertConfig.Birds);
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The dune skyline, from low down: dunes read as dunes on the horizon, where one crest stands
            //against the next. From above they are a texture.
            viewpoint = new SceneViewpoint(SceneRenderer.AtBearing(bearing, 360f, _desertConfig.LevelY + 10f), 2.2f, 8f, 0f, "the dunes");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get { yield return _desertEffect; }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _desertConfig.LevelY;
            below = _desertConfig.DuneAmplitude;
            above = _desertConfig.DuneAmplitude;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _desertEffect;
            mirror = (x, z) => TerrainMirror.Desert(x, z, _desertConfig);
            return true;
        }
    }
}
