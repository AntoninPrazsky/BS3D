using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The cavern (the eleventh scene, the third sky-replacing pass): <c>Cavern.fx</c> over the renderer's
    /// shared full-screen quad, shaded at display resolution under supersampling. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580; the renderer's public <c>CavernCrystalCenter</c> and
    /// <c>CavernGodRayXZ</c> forward here. See "The cavern" in docs/scenes.md.
    /// </summary>
    internal sealed class CavernBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private CavernSceneConfig _cavernConfig = new();

        //The eleventh scene, the third sky-replacing pass — the same machinery again.
        private readonly Effect _cavernEffect;
        private readonly EffectParameter _cavernViewRayBasis, _cavernCameraPosition, _cavernTime;

        //The formations' depth (#755): its technique and matrix, whether the scene pass draws formations at all (only the
        //full program does), and how far off the axis a lens has to stand before one can come between it and the arena
        private readonly EffectTechnique _depthTechnique;
        private readonly EffectParameter _cavernViewProjection;
        private bool _drawsFormations = true;
        private float _formationsClearRadius;

        private static readonly BlendState DEPTH_ONLY = new() { ColorWriteChannels = ColorWriteChannels.None };

        //Where the formations' depth can matter at all (#755): only where the arena draws - the island and its drain, the
        //gun walking round it, the cluster and the glass over them. A box round it, generous on every side, projected
        //to the screen and the pass scissored to it: from the intro's first shot the arena is a small part of the frame,
        //and the pass marched every formation across the whole of it, +1.37 ms at 1600x900 x2 for depth nothing used.
        private const float ARENA_BOUND_RADIUS = ArenaIsland.RADIUS + 4f;
        private const float ARENA_BOUND_BOTTOM = ArenaIsland.FUNNEL_BOTTOM_Y - 1f;
        private const float ARENA_BOUND_TOP = 40f;
        private static readonly RasterizerState SCISSORED = new() { CullMode = CullMode.None, ScissorTestEnable = true };

        /// <summary>Loads the effect, caches its per-frame parameters and pushes the config at it.</summary>
        public CavernBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Cavern: the eleventh scene, the third sky-replacing pass, on the same shared quad.
            _cavernEffect = content.Load<Effect>("Shaders/Cavern");

            _cavernViewRayBasis = _cavernEffect.Parameters["ViewRayBasis"];
            _cavernCameraPosition = _cavernEffect.Parameters["CameraPosition"];
            _cavernTime = _cavernEffect.Parameters["CavernTime"];
            _cavernViewProjection = _cavernEffect.Parameters["ViewProjection"];
            _depthTechnique = _cavernEffect.Techniques["CavernDepth"];

            ApplyCavernParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Cavern;

        /// <inheritdoc/>
        public override SceneConfig Config => _cavernConfig;

        /// <summary>
        /// True: a supersampled frame shades this pass at the back buffer's size and scales it up — see
        /// <see cref="SceneRenderer"/>'s <c>DrawBackdropAtDisplayResolution</c> for the measurement and for why
        /// it is the dream and the cavern and not space.
        /// </summary>
        public override bool DrawsAtDisplayResolution => true;

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            //The cavern is BACK (#298), and it left and returned for different reasons — see Cavern.fx's own
            //note at the techniques. It went in #250, when the pair it used to drop was cut from the authored
            //scene outright; it returned because it is the one scene the ladder could not help any other way,
            //being immune to supersampling by #155's construction. Its new pair is the wall's bump gradient
            //(twelve octaves of 3D noise a pixel) and the crack network.
            _cavernEffect.CurrentTechnique = _cavernEffect.Techniques[sceneDetail > 0.5f ? "Cavern" : "CavernReduced"];
            _drawsFormations = sceneDetail > 0.5f;
        }

        private void ApplyCavernParameters()
        {
            CavernSceneConfig cavern = _cavernConfig;

            CavernRockConfig rock = cavern.Rock;
            _cavernEffect.Parameters["CaveRadius"].SetValue(rock.CaveRadius);

            //Cavern.fx's own figures: the nearest stalactite group's anchor stands 0.3 of the radius off the axis, and a
            //group spreads 15 round its anchor with cones up to 6.5 x 1.35 thick; the columns stand at 0.46 and further.
            //So nothing of them comes nearer the axis than this, and a lens inside it has none between it and anything
            //on the arena (#755) - the play camera, the menu's orbit; the chapter intro's first shot is 168 out.
            _formationsClearRadius = 0.3f * rock.CaveRadius - 24f;
            _cavernEffect.Parameters["CaveCeilingY"].SetValue(rock.CeilingY);
            _cavernEffect.Parameters["RockColor"].SetValue(rock.RockColor.ToVector3());
            _cavernEffect.Parameters["VeinColor"].SetValue(rock.VeinColor.ToVector3());
            _cavernEffect.Parameters["FogColor"].SetValue(rock.FogColor.ToVector3());
            _cavernEffect.Parameters["FogDensity"].SetValue(rock.FogDensity);

            CavernWaterConfig water = cavern.Water;
            _cavernEffect.Parameters["WaterLevelY"].SetValue(water.LevelY);
            _cavernEffect.Parameters["WaterDeepColor"].SetValue(water.DeepColor.ToVector3());
            _cavernEffect.Parameters["WaterGlowColor"].SetValue(water.GlowColor.ToVector3());
            _cavernEffect.Parameters["WaveScale"].SetValue(water.WaveScale);
            _cavernEffect.Parameters["WaveSpeed"].SetValue(water.WaveSpeed);
            _cavernEffect.Parameters["WaveAmplitude"].SetValue(water.WaveAmplitude);
            _cavernEffect.Parameters["CausticStrength"].SetValue(water.CausticStrength);
            _cavernEffect.Parameters["MistColor"].SetValue(water.MistColor.ToVector3());
            _cavernEffect.Parameters["MistDensity"].SetValue(water.MistDensity);
            _cavernEffect.Parameters["MistHeight"].SetValue(water.MistHeight);

            CavernAirConfig air = cavern.Air;
            _cavernEffect.Parameters["GodRayColor"].SetValue(air.GodRayColor.ToVector3());
            _cavernEffect.Parameters["GodRayStrength"].SetValue(air.GodRayStrength);
            _cavernEffect.Parameters["GlowwormColor"].SetValue(air.GlowwormColor.ToVector3());
            _cavernEffect.Parameters["SporeColor"].SetValue(air.SporeColor.ToVector3());
            _cavernEffect.Parameters["SporeBrightness"].SetValue(air.SporeBrightness);

            CavernCrystalConfig crystals = cavern.Crystals;
            _cavernEffect.Parameters["CrystalColorA"].SetValue(crystals.ColorA.ToVector3());
            _cavernEffect.Parameters["CrystalColorB"].SetValue(crystals.ColorB.ToVector3());
            _cavernEffect.Parameters["CrystalEmission"].SetValue(crystals.Emission);
            _cavernEffect.Parameters["CrystalPulseSpeed"].SetValue(crystals.PulseSpeed);
            _cavernEffect.Parameters["CrystalWallLight"].SetValue(crystals.WallLight);
        }

        /// <summary>
        /// Draws the cavern: the third sky-replacing pass, over the shared full-screen quad. Everything animated —
        /// the river, the god rays' breath, the crystals' pulse, the rising spores — runs off the frame's
        /// wall-clock time, so the cave keeps living while the simulation is paused.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            _cavernViewRayBasis.SetValue(SkyRay.Basis(frame.Camera));
            _cavernCameraPosition.SetValue(frame.Camera.Position);
            _cavernTime.SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _cavernEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            //Space's rule (SpaceBackdrop.Draw): the depth state left at None would draw the rest of the frame in submission order.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The depth of the stalactites and columns (#755): Cavern.fx's <c>CavernDepth</c>, colour writes off, so the
        /// island, the funnel, the gun and the cluster are hidden where a formation stands in front of them. The scene
        /// pass writes no depth - it is a background, and under supersampling it is shaded into a target of its own and
        /// scaled up - so every formation stood at infinite depth and the arena drew over it: the chapter intro's first
        /// shot, out among the columns, showed the island through one. Only when the formations are drawn (the full
        /// program) and only for a lens outside <see cref="_formationsClearRadius"/>, where one can stand between it and
        /// the arena: the play camera never pays for it.
        /// </summary>
        public override void DrawDepth(in SceneFrame frame)
        {
            Vector3 lens = frame.Camera.Position;
            if (!_drawsFormations || lens.X * lens.X + lens.Z * lens.Z < _formationsClearRadius * _formationsClearRadius) return;

            Matrix viewProjection = frame.Camera.View * frame.Camera.Projection;
            if (!TryScissorArena(viewProjection, out Rectangle scissor)) return;

            _cavernViewRayBasis.SetValue(SkyRay.Basis(frame.Camera));
            _cavernCameraPosition.SetValue(lens);
            _cavernViewProjection.SetValue(viewProjection);

            _graphicsDevice.BlendState = DEPTH_ONLY;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = SCISSORED;
            _graphicsDevice.ScissorRectangle = scissor;

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _depthTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The bound target's pixels the arena's box covers (see <see cref="ARENA_BOUND_RADIUS"/>), for the formations'
        /// depth pass. False when it covers none - the arena is off screen and the depth would serve nothing; the whole
        /// target when a corner stands behind the lens, where a projection says nothing useful.
        /// </summary>
        private bool TryScissorArena(Matrix viewProjection, out Rectangle scissor)
        {
            Viewport viewport = _graphicsDevice.Viewport;
            scissor = new Rectangle(0, 0, viewport.Width, viewport.Height);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector4 world = new(
                    (corner & 1) == 0 ? -ARENA_BOUND_RADIUS : ARENA_BOUND_RADIUS,
                    (corner & 2) == 0 ? ARENA_BOUND_BOTTOM : ARENA_BOUND_TOP,
                    (corner & 4) == 0 ? -ARENA_BOUND_RADIUS : ARENA_BOUND_RADIUS,
                    1f);
                Vector4 clip = Vector4.Transform(world, viewProjection);
                if (clip.W <= 1e-3f) return true;

                float x = clip.X / clip.W, y = clip.Y / clip.W;
                minX = MathF.Min(minX, x);
                maxX = MathF.Max(maxX, x);
                minY = MathF.Min(minY, y);
                maxY = MathF.Max(maxY, y);
            }

            minX = MathF.Max(minX, -1f);
            maxX = MathF.Min(maxX, 1f);
            minY = MathF.Max(minY, -1f);
            maxY = MathF.Min(maxY, 1f);
            if (minX >= maxX || minY >= maxY) return false;

            int left = (int)MathF.Floor((minX * 0.5f + 0.5f) * viewport.Width);
            int right = (int)MathF.Ceiling((maxX * 0.5f + 0.5f) * viewport.Width);
            int top = (int)MathF.Floor((0.5f - maxY * 0.5f) * viewport.Height);
            int bottom = (int)MathF.Ceiling((0.5f - minY * 0.5f) * viewport.Height);
            scissor = new Rectangle(left, top, right - left, bottom - top);
            return scissor.Width > 0 && scissor.Height > 0;
        }

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            //The cavern's is dim and cool — a cave lit by its own bioluminescence, the ground bounce
            //carrying the river's teal up onto the island's underside.
            CavernLightingConfig cavern = _cavernConfig.Lighting;
            rig = new SceneLightRig(
                cavern.SkyAmbient.ToVector3(),
                cavern.GroundAmbient.ToVector3(),
                cavern.KeyTint.ToVector3(),
                cavern.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Down at the water, not up at the roof — and the first draft did aim at the roof, on the
            //argument that a cavern's defining feature is overhead and the gameplay camera never looks
            //up. Photographed, that shot is very nearly BLACK: the ceiling is unlit rock a hundred units
            //off, and everything this scene has to show glows from BELOW. The river is the light source,
            //so the island stands between the lens and it and reads as a silhouette over the glow.
            viewpoint = new SceneViewpoint(SceneRenderer.AtBearing(bearing, 130f, _cavernConfig.Water.LevelY + 2f),
                1.8f, 16f, 168f, "the river");
            return true;
        }

        /// <summary>See <see cref="SceneRenderer.CavernCrystalCenter"/>.</summary>
        public Vector3 CrystalCenter(int index)
        {
            float k = index;
            float angle = k * 2.39996f + 0.7f;
            float radius = _cavernConfig.Rock.CaveRadius * 0.965f;
            float y = _cavernConfig.Water.LevelY + 6f + (k * 37f) % 70f;

            return new Vector3(MathF.Cos(angle) * radius, y, MathF.Sin(angle) * radius);
        }

        /// <summary>See <see cref="SceneRenderer.CavernGodRayXZ"/>.</summary>
        public Vector2 GodRayXZ(int index)
        {
            float r = index;
            float angle = r * 1.62f + 0.4f;
            float radius = _cavernConfig.Rock.CaveRadius * (0.30f + 0.14f * ShaderMath.Frac(r * 0.53f));

            return new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }
    }
}
