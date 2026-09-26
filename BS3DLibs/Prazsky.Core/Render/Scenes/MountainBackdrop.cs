using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The mountains: a snow basin ringed by ridged peaks on a camera-centred grid under the dome, and the falling
    /// snow over it. Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its
    /// <see cref="TerrainPass"/>, the push, the reduced program, the draw, its own clone of <c>Snow.fx</c> and the
    /// snow overlay (the flake buffer is the shared <see cref="Snowfall"/>), the viewpoint, the shadow map's fit and
    /// receiver, and the terrain probe <c>mirrorcheck</c> reads. See "The mountains (Testbed)" in docs/scenes.md.
    /// </summary>
    internal sealed class MountainBackdrop : Backdrop
    {
        private MountainSceneConfig _mountainConfig = new();

        private readonly Effect _mountainEffect;

        //Its camera grid and the pass that draws it (#580)
        private readonly TerrainPass _mountainPass;

        //Finer than the first version (240) so the craggier peaks resolve; needs the 32-bit index buffer (360*360
        //vertices overflow a 16-bit one). Per-vertex base normal, per-pixel rock relief on top (see Mountain.fx)
        private const int MOUNTAIN_GRID_N = 360;
        private const float MOUNTAIN_EXTENT = 1200f;

        //Look/tuning parameters (heights, clearing, snow/rock colours, snowline, rock relief, ambient, haze)
        //now live in MountainSceneConfig; the backdrop reads them from _mountainConfig.

        //The mountain's own clone of Snow.fx (#580), its SnowConfig's look pushed once at load; the aurora's
        //is its backdrop's. The two used to share one effect and re-push eleven values every frame, so that
        //the slots held whichever scene was actually being drawn.
        private readonly Effect _mountainSnowEffect;

        /// <summary>Loads the effect, takes its grid through its <see cref="TerrainPass"/>, pushes the config at it and clones the snow.</summary>
        public MountainBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            //--- Mountain: a ridged displaced grid
            _mountainEffect = content.Load<Effect>("Shaders/Mountain");
            _mountainPass = new TerrainPass(Services, _mountainEffect, MOUNTAIN_GRID_N, MOUNTAIN_EXTENT, null);

            ApplyMountainParameters();

            _mountainSnowEffect = content.Load<Effect>("Shaders/Snow").Clone();
            Snowfall.ApplyParameters(_mountainSnowEffect, _mountainConfig.Snow);
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Mountain;

        /// <inheritdoc/>
        public override SceneConfig Config => _mountainConfig;

        /// <summary>The snow this scene draws; the renderer sizes the shared flake buffer off its count.</summary>
        public SnowConfig Snow => _mountainConfig.Snow;

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
            _mountainEffect.Parameters["FluteSnow"].SetValue(_mountainConfig.FluteSnow);
            _mountainEffect.Parameters["AlpenglowLow"].SetValue(_mountainConfig.AlpenglowLow);
            _mountainEffect.Parameters["AlpenglowHigh"].SetValue(MathF.Max(_mountainConfig.AlpenglowHigh, _mountainConfig.AlpenglowLow + 1f));
        }

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            //The mountain, new to the reduced programs with the cavern (#298) and picked for the same reason from the
            //other end: it is the only scene the desktop still calls marginal (#296). Its pair is #208's own —
            //the snow's sastrugi drift relief and its sparkle — given up together because they arrived
            //together, and because the sparkle is a HIGHLIGHT, which is the class of thing the owner named
            //when he ruled that a tier drops effects and never resolution.
            _mountainEffect.CurrentTechnique = _mountainEffect.Techniques[sceneDetail > 0.5f ? "Mountain" : "MountainReduced"];
        }

        /// <summary>
        /// Draws the snowy range: the grid pinned to the camera (snapped to a cell so it does not swim),
        /// lifted into a snow basin ringed by peaks and shaded by the current dome, shadowed by the shared
        /// cloud field.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _mountainPass.Draw(frame, Services.TerrainHoleRadius);
        }

        /// <summary>The falling snow, through the shared flake buffer and this scene's own clone of <c>Snow.fx</c>.</summary>
        public override void DrawOverlays(in SceneFrame frame) => Services.Snowfall.Draw(frame, _mountainSnowEffect, _mountainConfig.Snow);

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Up at the range, from the furthest stand in the table: the peaks are the only subject here
            //that is genuinely tall, and the one shot that is wrong for them is a close one.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, 430f, _mountainConfig.LevelY + _mountainConfig.Height * 0.75f),
                2.4f, 15f, 0f, "the peaks");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get { yield return _mountainEffect; }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            //The peaks are the terrain's own silhouette and mostly stand outside the map's extent;
            //what has to be covered is the basin the island sits in, so half the range's height is
            //the box rather than all of it — a range fitted to an 82-unit peak coarsens the bias on
            //the snow at the gun's feet for a ridge no map reaches.
            groundY = _mountainConfig.LevelY;
            below = _mountainConfig.Height * 0.25f;
            above = _mountainConfig.Height * 0.5f;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _mountainEffect;
            mirror = (x, z) => TerrainMirror.Mountain(x, z, _mountainConfig);
            return true;
        }

        /// <summary>Frees the snow clone; the effect itself is the content manager's.</summary>
        public override void Dispose() => _mountainSnowEffect?.Dispose();
    }
}
