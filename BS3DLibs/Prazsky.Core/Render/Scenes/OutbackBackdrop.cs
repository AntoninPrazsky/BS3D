using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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

        private readonly Effect _outbackEffect;

        //Its camera grid and the pass that draws it (#580): the skeleton this scene shared with two others
        private readonly TerrainPass _outbackPass;

        //The monoliths are geometry, not a painted horizon, so this grid carries a silhouette rather than only
        //a shaded surface — which is what sets the density. At 400 over 1000 the cell is 2.5 world units and a
        //formation's flank falls its whole height over some eight of them, which the mesh can hold; the same
        //flank on the desert's 360 grid would fall over seven. Above 255 a side, so the grid cache's 32-bit
        //index buffer is load-bearing here (the mountain's lesson — a 16-bit one wraps silently).
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
            _outbackEffect.Parameters["HorizonHazeDistance"].SetValue(air.HorizonHazeDistance);
            _outbackEffect.Parameters["HazeWarmth"].SetValue(air.HazeWarmth);
            _outbackEffect.Parameters["HeatShimmer"].SetValue(air.HeatShimmer);
            _outbackEffect.Parameters["WindDirection"].SetValue(air.Wind.ToVector2());
        }

        /// <summary>
        /// Draws the outback (#112): the grid pinned to the camera (snapped to a cell so the land does not
        /// swim), carrying a near-flat spinifex plain with red monoliths displaced into it, shaded per-pixel by
        /// the current dome and shadowed by the shared cloud field. Like the desert it has no point lights, so
        /// it sets none; unlike the desert its terrain carries a real silhouette, which is why the grid is finer.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _outbackPass.Draw(frame, Services.TerrainHoleRadius);
            Services.Birds.Draw(frame, _outbackConfig.Birds);
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
            get { yield return _outbackEffect; }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            //The monoliths are terrain, not props, and they are what a map here has to clear.
            groundY = _outbackConfig.Terrain.LevelY;
            below = _outbackConfig.Terrain.OutcropHeight;
            above = _outbackConfig.Terrain.RockHeight;
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
