using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The polar icesheet (#222): sastrugi and a crevassed pressure belt on a camera-centred grid under the dome.
    /// Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>,
    /// the push, the draw, the viewpoint, the shadow map's fit and receiver, the terrain probe <c>mirrorcheck</c> reads
    /// and the ground queries the chapter intro flies over the ice with. See "The polar icesheet" in docs/scenes.md.
    /// </summary>
    internal sealed class PolarBackdrop : Backdrop
    {
        private PolarSceneConfig _polarConfig = new();

        private readonly Effect _polarEffect;

        //Its camera grid and the pass that draws it (#580): the skeleton this scene shared with two others
        private readonly TerrainPass _polarPass;

        //The icesheet is real geometry, on the desert's terms: a camera-centred grid of this many vertices per
        //side over this world extent, displaced in the shader and snapped to a cell so it does not swim.
        //
        //Finer than the desert's and over a wider extent, and both halves are this scene rather than taste. A
        //sastrugi ridge is metres across where a dune is tens, so the cell has to be small enough to hold one;
        //and the subject is a flat expanse, which means the horizon is further away here than in any other
        //ground scene and the grid has to reach it or the haze has to start early enough to hide the edge.
        private const int POLAR_GRID_N = 420;
        private const float POLAR_EXTENT = 1200f;

        //Look/tuning parameters (the sastrugi, the pressure ridge, the crevasses, the two colours, the
        //sparkle and the transmission) live in PolarSceneConfig; the backdrop reads them from _polarConfig.

        /// <summary>Loads the effect, takes its grid through its <see cref="TerrainPass"/> and pushes the config at it.</summary>
        public PolarBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            //--- Polar (#222): the desert's machinery again, with the wind's other work on it — sastrugi and a
            //crevassed pressure belt instead of dunes, and a material that is white in reflection and cyan in
            //transmission, which is the scene rather than the terrain
            _polarEffect = content.Load<Effect>("Shaders/Polar");
            _polarPass = new TerrainPass(Services, _polarEffect, POLAR_GRID_N, POLAR_EXTENT, "PolarTime");

            ApplyPolarParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Polar;

        /// <inheritdoc/>
        public override SceneConfig Config => _polarConfig;

        private void ApplyPolarParameters()
        {
            _polarEffect.Parameters["PolarLevelY"].SetValue(_polarConfig.LevelY);
            _polarEffect.Parameters["DriftAmplitude"].SetValue(_polarConfig.DriftAmplitude);
            _polarEffect.Parameters["DriftFrequency"].SetValue(_polarConfig.DriftFrequency);
            _polarEffect.Parameters["DriftStretch"].SetValue(_polarConfig.DriftStretch);
            _polarEffect.Parameters["SwellAmplitude"].SetValue(_polarConfig.SwellAmplitude);
            _polarEffect.Parameters["ClearingRadius"].SetValue(_polarConfig.ClearingRadius);
            _polarEffect.Parameters["ClearingTransition"].SetValue(_polarConfig.ClearingTransition);
            _polarEffect.Parameters["RidgeRadius"].SetValue(_polarConfig.RidgeRadius);
            _polarEffect.Parameters["RidgeWidth"].SetValue(_polarConfig.RidgeWidth);
            _polarEffect.Parameters["RidgeHeight"].SetValue(_polarConfig.RidgeHeight);
            _polarEffect.Parameters["RidgeBearing"].SetValue(MathHelper.ToRadians(_polarConfig.RidgeBearingDegrees));
            _polarEffect.Parameters["RidgeSpan"].SetValue(MathHelper.ToRadians(_polarConfig.RidgeSpanDegrees));
            _polarEffect.Parameters["CrevasseDepth"].SetValue(_polarConfig.CrevasseDepth);
            _polarEffect.Parameters["CrevasseFrequency"].SetValue(_polarConfig.CrevasseFrequency);
            _polarEffect.Parameters["CrevasseSharpness"].SetValue(_polarConfig.CrevasseSharpness);
            _polarEffect.Parameters["CrevasseDarkening"].SetValue(_polarConfig.CrevasseDarkening);
            _polarEffect.Parameters["BlueIceThreshold"].SetValue(_polarConfig.BlueIceThreshold);
            _polarEffect.Parameters["SlabSize"].SetValue(_polarConfig.SlabSize);
            _polarEffect.Parameters["SlabTilt"].SetValue(_polarConfig.SlabTilt);
            _polarEffect.Parameters["SnowColor"].SetValue(_polarConfig.SnowColor.ToVector3());
            _polarEffect.Parameters["IceColor"].SetValue(_polarConfig.IceColor.ToVector3());
            _polarEffect.Parameters["AmbientStrength"].SetValue(_polarConfig.AmbientStrength);
            _polarEffect.Parameters["SheenStrength"].SetValue(_polarConfig.SheenStrength);
            _polarEffect.Parameters["SparkleStrength"].SetValue(_polarConfig.SparkleStrength);
            _polarEffect.Parameters["TransmissionStrength"].SetValue(_polarConfig.TransmissionStrength);
            _polarEffect.Parameters["WindDirection"].SetValue(_polarConfig.Wind.ToVector2());
            _polarEffect.Parameters["HorizonHazeDistance"].SetValue(_polarConfig.HorizonHazeDistance);
        }

        /// <summary>
        /// Draws the polar icesheet (#222): the same grid the desert uses, pinned to the camera and snapped to
        /// a cell, displaced into sastrugi with a crevassed pressure belt beyond them, shaded per-pixel by the
        /// current dome and shadowed by the shared cloud field. Like the desert it has no point lights of its
        /// own — the picture is all sun, sky and what the ice does with both — so it sets none.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _polarPass.Draw(frame, Services.TerrainHoleRadius);
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The pressure ridge, from low down and a long way out. On a flat white plain the ridge is the
            //only thing with a silhouette, and the only thing a camera can read distance against - the
            //desert's dune-skyline argument on a scene that has even less to look at. Low, because from
            //above an icesheet is a sheet of paper.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _polarConfig.RidgeRadius, _polarConfig.LevelY + _polarConfig.RidgeHeight * 0.6f),
                2.3f, 7f, 0f, "the pressure ridge");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get { yield return _polarEffect; }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _polarConfig.LevelY;
            below = _polarConfig.SwellAmplitude * 2f;
            above = _polarConfig.RidgeHeight;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _polarEffect;
            mirror = (x, z) => TerrainMirror.Polar(x, z, _polarConfig);
            return true;
        }

        /// <summary><see cref="SceneRenderer.PolarGroundHeight"/>.</summary>
        public float GroundHeight(float x, float z) => TerrainMirror.Polar(x, z, _polarConfig);

        /// <summary><see cref="SceneRenderer.PolarCrevasse"/>.</summary>
        public float Crevasse(float x, float z) => TerrainMirror.PolarCrevasse(x, z, _polarConfig);

        /// <summary><see cref="SceneRenderer.PolarRidgeAt"/>.</summary>
        public float RidgeAt(float x, float z) => TerrainMirror.PolarRidge(x, z, _polarConfig);
    }
}
