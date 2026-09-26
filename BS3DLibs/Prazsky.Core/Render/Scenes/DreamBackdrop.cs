using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The dream (the tenth scene, the second sky-replacing pass): <c>Dream.fx</c> over the renderer's shared
    /// full-screen quad, shaded at display resolution under supersampling. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580; the renderer's public <c>DreamSolidCenter</c> and
    /// <c>DreamOrbCenter</c> forward here. See "The dream" in docs/scenes.md.
    /// </summary>
    internal sealed class DreamBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private DreamSceneConfig _dreamConfig = new();

        //The tenth scene, and the second that replaces the SKY rather than the ground (see SpaceBackdrop): the
        //same full-screen-quad machinery on the renderer's shared quad, with its own effect and per-frame parameters.
        private readonly Effect _dreamEffect;
        private readonly EffectParameter _dreamViewRayBasis, _dreamCameraPosition, _dreamTime;

        /// <summary>Loads the effect, caches its per-frame parameters and pushes the config at it.</summary>
        public DreamBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Dream: the tenth scene, the second sky-replacing pass. It shares the full-screen quad — a corner
            //quad in normalized device coordinates has nothing scene-specific about it.
            _dreamEffect = content.Load<Effect>("Shaders/Dream");

            _dreamViewRayBasis = _dreamEffect.Parameters["ViewRayBasis"];
            _dreamCameraPosition = _dreamEffect.Parameters["CameraPosition"];
            _dreamTime = _dreamEffect.Parameters["DreamTime"];

            ApplyDreamParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Dream;

        /// <inheritdoc/>
        public override SceneConfig Config => _dreamConfig;

        /// <summary>
        /// True: a supersampled frame shades this pass at the back buffer's size and scales it up — see
        /// <see cref="SceneRenderer"/>'s <c>DrawBackdropAtDisplayResolution</c> for the measurement and for why
        /// it is the dream and the cavern and not space.
        /// </summary>
        public override bool DrawsAtDisplayResolution => true;

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            //The dream's four: the background's second evaluation in the reflection, most of the sparks, most
            //of each spark's trail, and an octave off both warp layers — the last being the only reduction in
            //any of these three scenes that pays on its own, since it is the only one on every pixel.
            _dreamEffect.CurrentTechnique = _dreamEffect.Techniques[sceneDetail > 0.5f ? "Dream" : "DreamReduced"];
        }

        private void ApplyDreamParameters()
        {
            DreamSceneConfig dream = _dreamConfig;

            _dreamEffect.Parameters["DeepColor"].SetValue(dream.DeepColor.ToVector3());

            DreamPaletteConfig palette = dream.Palette;
            _dreamEffect.Parameters["PaletteA"].SetValue(palette.A.ToVector3());
            _dreamEffect.Parameters["PaletteB"].SetValue(palette.B.ToVector3());
            _dreamEffect.Parameters["PaletteC"].SetValue(palette.C.ToVector3());
            _dreamEffect.Parameters["PaletteD"].SetValue(palette.D.ToVector3());

            DreamBackgroundConfig background = dream.Background;
            _dreamEffect.Parameters["SwirlScale"].SetValue(background.SwirlScale);
            _dreamEffect.Parameters["SwirlWarp"].SetValue(background.SwirlWarp);
            _dreamEffect.Parameters["SwirlSpeedSlow"].SetValue(background.SpeedSlow);
            _dreamEffect.Parameters["SwirlSpeedFast"].SetValue(background.SpeedFast);
            _dreamEffect.Parameters["RibbonSharpness"].SetValue(background.RibbonSharpness);
            _dreamEffect.Parameters["BackgroundBrightness"].SetValue(background.Brightness);

            DreamShapesConfig shapes = dream.Shapes;
            _dreamEffect.Parameters["ShapeOrbitRadius"].SetValue(shapes.OrbitRadius);
            _dreamEffect.Parameters["ShapeSize"].SetValue(shapes.Size);
            _dreamEffect.Parameters["ShapeMorphSpeed"].SetValue(shapes.MorphSpeed);
            _dreamEffect.Parameters["ShapeEmission"].SetValue(shapes.Emission);
            _dreamEffect.Parameters["ShapeReflection"].SetValue(shapes.Reflection);
            _dreamEffect.Parameters["ShapeAbsorption"].SetValue(shapes.Absorption);

            DreamGlowsConfig glows = dream.Glows;
            _dreamEffect.Parameters["OrbRadius"].SetValue(glows.OrbRadius);
            _dreamEffect.Parameters["OrbBrightness"].SetValue(glows.OrbBrightness);
            _dreamEffect.Parameters["SparkBrightness"].SetValue(glows.SparkBrightness);
            _dreamEffect.Parameters["SparkSpeed"].SetValue(glows.SparkSpeed);
        }

        /// <summary>
        /// Draws the dream: the second sky-replacing pass, over the shared full-screen quad. Everything animated in it
        /// runs off the frame's wall-clock time — the marbling, the tumbling solids, the orbs' breathing and
        /// the sparks keep moving while the simulation is paused, like the clouds and the balls' pulse.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _dreamViewRayBasis.SetValue(SkyRay.Basis(frame.Camera));
            _dreamCameraPosition.SetValue(frame.Camera.Position);
            _dreamTime.SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _dreamEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            //Space's rule (SpaceBackdrop.Draw): the depth state left at None would draw the rest of the frame in submission order.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            //The dream states one for the same reason and one more: its rig is deliberately COLOURED
            //(violet over teal, a rose key against a cyan fill), so the island, the gun and the balls
            //sit in the hallucination instead of standing greyly in front of it.
            DreamLightingConfig dream = _dreamConfig.Lighting;
            rig = new SceneLightRig(
                dream.SkyAmbient.ToVector3(),
                dream.GroundAmbient.ToVector3(),
                dream.KeyTint.ToVector3(),
                dream.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The marbled sky, at the radius the solids roam at and only a little above the island — this
            //scene has no ground and no horizon, so every direction is sky and the only thing a shot can
            //get WRONG is framing nothing else. Behind the arena and low, so the island and its cluster
            //are silhouetted against it: photographed at 26 up the lens tilted off them entirely and the
            //frame was marbling and two orbs, which is a wallpaper rather than a place.
            viewpoint = new SceneViewpoint(SceneRenderer.AtBearing(bearing, _dreamConfig.Shapes.OrbitRadius, 6f),
                1.9f, 10f, 168f, "the marbled sky");
            return true;
        }

        /// <summary>See <see cref="SceneRenderer.DreamSolidCenter"/>.</summary>
        public Vector3 SolidCenter(int index, float time, out float bound)
        {
            float i = index;
            float orbit = _dreamConfig.Shapes.OrbitRadius;
            float a = time * (0.020f + 0.011f * ShaderMath.Frac(i * 0.371f)) + i * 2.399f;
            float r = orbit * (0.78f + 0.22f * MathF.Sin(i * 5.3f));
            float y = 26f + 46f * MathF.Sin(time * 0.013f + i * 2.7f);

            bound = _dreamConfig.Shapes.Size * 1.9f;
            return new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r);
        }

        /// <summary>See <see cref="SceneRenderer.DreamOrbCenter"/>.</summary>
        public Vector3 OrbCenter(int index, float time, out float radius)
        {
            float o = index;
            float orbit = _dreamConfig.Shapes.OrbitRadius * 1.25f;

            radius = _dreamConfig.Glows.OrbRadius * (0.7f + 0.3f * MathF.Sin(o * 7f));
            return new Vector3(
                MathF.Cos(time * 0.009f + o * 2.1f) * orbit,
                15f + 60f * MathF.Sin(time * 0.007f + o * 3.3f),
                MathF.Sin(time * 0.011f + o * 1.3f) * orbit);
        }
    }
}
