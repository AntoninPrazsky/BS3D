using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The sea: a camera-centred grid displaced into Gerstner swell with foam, subsurface light and a Fresnel
    /// reflection of the dome, the calm pool standing in the drain, and the blown spray over it. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580 — the config, the effect's clone and its <see cref="TerrainPass"/>,
    /// the push, the draw, the spray, the viewpoint, and the submerge figures the renderer's <c>SeaLevelY</c>,
    /// <c>ApplySeaSubmerge</c> and <c>LensSubmergedAmount</c> forward to. It states no shadow receiver or fit: a
    /// shadow inside the water's Fresnel, foam and subsurface terms is a look decision nobody has asked for
    /// (<c>RegisterShadowReceivers</c>). See "The sea (Testbed)" in docs/scenes.md.
    /// </summary>
    internal sealed class SeaBackdrop : Backdrop
    {
        private SeaSceneConfig _seaConfig = new();

        private readonly GraphicsDevice _graphicsDevice;

        private readonly Effect _seaEffect;

        //Its camera grid and the pass that draws it (#580). The grid is the cache's and the tropical lagoon draws
        //over the same one (it asks the cache for SEA_GRID_N over SEA_EXTENT), so neither scene owns it.
        private readonly TerrainPass _seaPass;

        //The pool's radius is the one per-frame value the sea sets past the pass's ten (resolved once, BestPractices §1)
        private readonly EffectParameter _funnelPoolRadius;

        //The sea is real geometry now, like the dunes: a camera-centred grid this many vertices per side over
        //this world extent, displaced by Gerstner waves in the shader and snapped to a cell on the CPU each
        //frame so it does not swim. Dense enough for the dominant swell to read as smooth geometry; the fine
        //chop is added per pixel. (Grid density is the natural Low/Med/High/Ultra dial once graphics settings land.)
        internal const int SEA_GRID_N = 380;
        internal const float SEA_EXTENT = 1600f;

        //How far the pool's edge is buried INTO the drain's glass cone (world units): the water's rim ends
        //inside the wall rather than a chord-width short of it, so the funnel's 64-segment faceting can never
        //open a sliver of sky between the water and the glass — the same buried-edge reasoning as the gold
        //bands' EDGE_SINK (#109). See the pool derivation in Draw (#132).
        private const float POOL_WALL_BIAS = 0.15f;

        //Look/tuning parameters (water level & colours, waves, chop, wind, sun glint, foam, subsurface, haze)
        //now live in SeaSceneConfig; the backdrop reads them from _seaConfig (spray via _seaConfig.Spray).

        private readonly Effect _sprayEffect;
        private VertexBuffer _sprayVertexBuffer;
        private IndexBuffer _sprayIndexBuffer;

        //Spray parameters (particle count/size/colour/opacity, box, level, wind, rise, turbulence) now live in
        //SeaSceneConfig.Spray (SprayConfig); the backdrop reads them from _seaConfig.Spray. The glare-safe
        //spray colour still matters: its luminance must stay under GLARE_THRESHOLD or it blooms - see CLAUDE.md.

        /// <summary>
        /// Loads the water's own clone of <c>Sea.fx</c> and the spray, takes the grid through its <see cref="TerrainPass"/>,
        /// pushes the config at both and builds the spray's particles.
        /// </summary>
        public SeaBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Sea: a camera-centred grid displaced into Gerstner waves; its pass snaps it to a cell and sets
            //the mean level. Drawn CullNone (one open surface, read from above and through the crests).
            //Its own clone of Sea.fx (#580): the tropical lagoon draws through another, so neither can leave
            //its water in the other's slots.
            _seaEffect = content.Load<Effect>("Shaders/Sea").Clone();
            _seaPass = new TerrainPass(Services, _seaEffect, SEA_GRID_N, SEA_EXTENT, "SeaTime");
            _funnelPoolRadius = _seaEffect.Parameters["FunnelPoolRadius"];

            ApplySeaParameters();

            //--- Spray: a static billboard buffer for the sea's blown spray and spindrift, animated entirely
            //in the shader like the snow. Same position+data billboard vertex.
            _sprayEffect = content.Load<Effect>("Shaders/Spray");
            ApplySprayParameters();

            BuildSprayBuffers();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Sea;

        /// <inheritdoc/>
        public override SceneConfig Config => _seaConfig;

        /// <summary><see cref="SceneRenderer.SeaLevelY"/>.</summary>
        public float LevelY => _seaConfig.LevelY;

        /// <summary><see cref="SceneRenderer.ApplySeaSubmerge"/>.</summary>
        public void ApplySubmerge(Effect effect, SceneKind scene, float lensSubmerged)
        {
            var p = effect.Parameters;
            if (scene != SceneKind.Sea)
            {
                p["SeaFadeDepth"]?.SetValue(0f);
                return;
            }

            p["SeaLevelY"].SetValue(_seaConfig.LevelY);
            p["SeaFadeDepth"].SetValue(SEA_SUBMERGE_FADE);
            p["SeaSubmergeTint"].SetValue(_seaConfig.WaterDeep.ToVector3());
            p["SeaLensSubmerged"].SetValue(lensSubmerged);
        }

        /// <summary><see cref="SceneRenderer.LensSubmergedAmount"/>.</summary>
        public float LensSubmergedAmount(SceneKind scene, Vector3 cameraPosition) =>
            scene == SceneKind.Sea
                ? MathHelper.Clamp((_seaConfig.LevelY + 0.5f - cameraPosition.Y) / UNDERWATER_FADE_DEPTH, 0f, 1f)
                : 0f;

        /// <summary>
        /// How far under the surface the lens has to be for the water to read as fully closed over it — the
        /// murk's own ramp, and since #159 the fade's release as well.
        /// </summary>
        private const float UNDERWATER_FADE_DEPTH = 7f;

        /// <summary>World units below the sea surface over which a missed ball fades from solid to gone — short,
        /// so it reads as being swallowed by the water rather than lingering under it.
        /// <para>
        /// It used to say the kill plane is far enough below this that a ball is off the screen long before the
        /// simulation drops it. That holds only while the lens is <b>above</b> the water: since #159 the fade is
        /// released as the camera goes under, so a ball watched from down there stays drawn all the way to the
        /// kill plane and is culled in one frame when it arrives. That pop is real, it is not this constant's to
        /// fix, and five other <c>OpenBelow</c> scenes have always shown it — see the issue filed for it.
        /// </para></summary>
        private const float SEA_SUBMERGE_FADE = 3f;

        private void ApplySeaParameters()
        {
            _seaEffect.Parameters["SeaLevelY"].SetValue(_seaConfig.LevelY);
            _seaEffect.Parameters["WaterColorDeep"].SetValue(_seaConfig.WaterDeep.ToVector3());
            _seaEffect.Parameters["WaterColorShallow"].SetValue(_seaConfig.WaterShallow.ToVector3());
            _seaEffect.Parameters["ShallowBias"].SetValue(_seaConfig.ShallowBias);
            _seaEffect.Parameters["WaveAmplitude"].SetValue(_seaConfig.WaveAmplitude);
            _seaEffect.Parameters["WaveSteepness"].SetValue(_seaConfig.WaveSteepness);
            _seaEffect.Parameters["WaveSpeed"].SetValue(_seaConfig.WaveSpeed);
            _seaEffect.Parameters["WaveFadeStart"].SetValue(_seaConfig.WaveFadeStart);
            _seaEffect.Parameters["WaveFadeEnd"].SetValue(_seaConfig.WaveFadeEnd);
            _seaEffect.Parameters["ChopAmplitude"].SetValue(_seaConfig.ChopAmplitude);
            _seaEffect.Parameters["ChopFrequency"].SetValue(_seaConfig.ChopFrequency);
            _seaEffect.Parameters["ChopSpeed"].SetValue(_seaConfig.ChopSpeed);
            _seaEffect.Parameters["WindDirection"].SetValue(_seaConfig.Wind.ToVector2());
            _seaEffect.Parameters["SunGlintStrength"].SetValue(_seaConfig.SunGlintStrength);
            _seaEffect.Parameters["SunGlintPower"].SetValue(_seaConfig.SunGlintPower);
            _seaEffect.Parameters["FoamJacobianThreshold"].SetValue(_seaConfig.FoamJacobianThreshold);
            _seaEffect.Parameters["FoamStrength"].SetValue(_seaConfig.FoamStrength);
            _seaEffect.Parameters["FoamCrestStart"].SetValue(_seaConfig.FoamCrestStart);
            _seaEffect.Parameters["FoamCrestStrength"].SetValue(_seaConfig.FoamCrestStrength);
            _seaEffect.Parameters["FoamColor"].SetValue(_seaConfig.FoamColor.ToVector3());
            _seaEffect.Parameters["SssStrength"].SetValue(_seaConfig.SssStrength);
            _seaEffect.Parameters["SssColor"].SetValue(_seaConfig.SssColor.ToVector3());
            _seaEffect.Parameters["HorizonHazeDistance"].SetValue(_seaConfig.HorizonHazeDistance);
        }

        private void ApplySprayParameters()
        {
            _sprayEffect.Parameters["SprayBoxSize"].SetValue(_seaConfig.Spray.BoxSize.ToVector3());
            _sprayEffect.Parameters["SprayLevelY"].SetValue(_seaConfig.LevelY + _seaConfig.Spray.LevelYAboveSea);
            _sprayEffect.Parameters["SprayWind"].SetValue(_seaConfig.Spray.Wind.ToVector2());
            _sprayEffect.Parameters["SprayRise"].SetValue(_seaConfig.Spray.Rise);
            _sprayEffect.Parameters["SprayTurb"].SetValue(_seaConfig.Spray.Turbulence);
            _sprayEffect.Parameters["DropletSize"].SetValue(_seaConfig.Spray.DropletSize);
            _sprayEffect.Parameters["SprayColor"].SetValue(_seaConfig.Spray.Color.ToVector3());
            _sprayEffect.Parameters["SprayOpacity"].SetValue(_seaConfig.Spray.Opacity);
        }

        /// <summary>(Re)builds the spray's particle buffer at the config's particle count. Deterministic seed.</summary>
        private void BuildSprayBuffers() =>
            Services.BuildBillboardParticles(_seaConfig.Spray.ParticleCount, 5023, ref _sprayVertexBuffer, ref _sprayIndexBuffer);

        /// <summary>
        /// Draws the sea: a camera-centred grid (snapped to a cell so the waves do not swim) displaced into
        /// Gerstner swell with foam, subsurface scattering and a Fresnel reflection of the current dome,
        /// shadowed by the same cloud field as the rest of the scene.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            //The pool standing in the drain (#132): the cut around the island keeps a calm disc of water
            //where the funnel's glass cone crosses the mean level, and discards only the annulus hidden
            //inside the island's stone. The radius is the cone's own at LevelY — the same straight span
            //FunnelMesh is built from, so the water and the glass cannot drift — buried POOL_WALL_BIAS into
            //the glass so no sliver of the wall shows under the water's edge (the buried-edge lesson of
            //#109). Clamping the span keeps a config that floods the rim or sits below the hole sane. With
            //TerrainHoleRadius 0 (the map editor) the shader cuts nothing and ignores this figure entirely.
            float drainRimY = ArenaIsland.TOP_Y - ArenaIsland.DISH_DEPTH;
            float poolT = Math.Clamp((drainRimY - _seaConfig.LevelY) / (drainRimY - ArenaIsland.FUNNEL_BOTTOM_Y), 0f, 1f);
            float poolRadius = MathHelper.Lerp(ArenaIsland.FUNNEL_TOP_RADIUS, ArenaIsland.FUNNEL_HOLE_RADIUS, poolT)
                + POOL_WALL_BIAS;

            //The camera, the sky, the clock, the clouds and the far fade are the pass's (#580); the pool is the sea's own
            _funnelPoolRadius.SetValue(poolRadius);

            //The config-static water values are the sea's own since #580 and were pushed once, at load
            //(ApplySeaParameters): the tropical lagoon draws through its own clone of Sea.fx now, so it
            //can no longer leave its water in this effect's slots — which is why they were re-pushed here
            //every frame until then.

            //Depth-read, not depth-write: a missed ball falls through the surface and the sea has to stop
            //claiming the depth under the waterline so the ball's own pixels run (and fade) instead of being
            //depth-killed by the surface plane. The island draws after this and is opaque, so it still writes
            //and owns its own depth; only the open water gives the depth up (#131).
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            //Opaque and CullNone (one open surface, read from above and through the crests), and it puts back
            //alpha-blend and the culling; the depth state is the sea's to restore
            _seaPass.Draw(frame, Services.TerrainHoleRadius);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        /// <summary>The blown spray and spindrift over the water.</summary>
        public override void DrawOverlays(in SceneFrame frame) => DrawSpray(frame);

        /// <summary>
        /// Draws the sea's blown spray and spindrift: the static billboard buffer animated in the shader, in a
        /// thin slab that follows the camera in XZ but clings to the water surface in Y. Alpha-blended and
        /// depth-read (the waves and the platform occlude the particles behind them) but writing no depth. Sea
        /// scene only.
        /// </summary>
        private void DrawSpray(in SceneFrame frame)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _sprayEffect.Parameters["View"].SetValue(frame.Camera.View);
            _sprayEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _sprayEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _sprayEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _sprayEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _sprayEffect.Parameters["SprayTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_sprayVertexBuffer);
            _graphicsDevice.Indices = _sprayIndexBuffer;
            _sprayEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _seaConfig.Spray.ParticleCount * 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Low and level, out to the water: what a sea IS from a few metres up is the glint and the
            //horizon, and any height at all trades that for a plan view of chop.
            viewpoint = new SceneViewpoint(SceneRenderer.AtBearing(bearing, 520f, _seaConfig.LevelY), 2.1f, 6f, 0f, "the open water");
            return true;
        }

        /// <summary>Frees the water's clone and the spray's buffers; the grid is the cache's and the spray's effect the content manager's.</summary>
        public override void Dispose()
        {
            _seaEffect?.Dispose();
            _sprayVertexBuffer?.Dispose();
            _sprayIndexBuffer?.Dispose();
        }
    }
}
