using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The Moon (the twelfth scene, #125): a displaced crater grid under a sky-replacing star-and-Earth pass,
    /// two techniques of <c>Moon.fx</c>, the terrain first and the sky depth-read over it. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580 — the config, the effect, its grid, the push, the draw, the
    /// light rig, the viewpoint, the low sun it states for itself and the earthshine. The renderer's public
    /// <c>TryGetMoonEarthshine</c> forwards here, and its <c>TryGetSunDirection</c> asks the backdrop. See
    /// "The Moon" in docs/scenes.md.
    /// </summary>
    internal sealed class MoonBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private MoonSceneConfig _moonConfig = new();

        //The twelfth scene (#125), and the first in BOTH families at once: a solid-terrain grid like the
        //desert's AND a sky-replacing pass like space's, in one effect with two techniques. Draw runs
        //the displaced crater grid first (depth-writing) and the sky quad after it, depth-READ on the
        //shared Services.FullScreenQuad — the opposite interleave is a measured 8× frame blow-up; see
        //Draw's doc.
        private readonly Effect _moonEffect;
        private readonly VertexBuffer _moonVertexBuffer;
        private readonly IndexBuffer _moonIndexBuffer;
        private readonly int _moonIndexCount;

        //Two techniques over one effect, resolved once — CurrentTechnique is assigned twice per frame here,
        //which is why the by-name lookup must not be paid per draw.
        private readonly EffectTechnique _moonSkyTechnique, _moonTerrainTechnique;

        //Per-frame parameters, resolved once (BestPractices §1). The sky pass wants the inverse
        //view-projection and the terrain pass the plain pair, so both are cached; everything else is pushed
        //by ApplyMoonParameters when a config lands. No time parameter: nothing on the Moon moves.
        private readonly EffectParameter _moonInverseViewProjection, _moonView, _moonProjection,
            _moonCameraPosition, _moonSunDirection, _moonSupersample, _moonOriginXZ, _moonHoleRadius;

        //The extent is set by where the horizon stands, not by haze reach like the atmospheric siblings: the
        //highland belt crests ~310 units out and the curvature (8e-5) closes everything behind it by
        //occlusion, so ground past ±600 can never be seen. What the crest and the curvature guarantee together
        //is that the grid's own EDGE (600 out, the corners ~848) lands beyond the occluding skyline, where it is
        //already hidden; a curvature loose enough to leave it visible puts a dead-level, camera-locked line
        //through the belt's saddles. Until #551 that line would have been the 500-unit far plane's cut, which
        //came first; the far plane is 2000 now and the Moon takes no far ring (it has no air to fade into -
        //see FarField.fxh), so the edge is the constraint and occlusion is still what answers it.
        private const int MOON_GRID_N = 360;
        private const float MOON_EXTENT = 1200f;

        /// <summary>Loads the effect, takes its grid, caches its parameters and pushes the config at it.</summary>
        public MoonBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Moon: the twelfth scene (#125), the first in both families at once — a displaced crater
            //grid like the desert's under a sky-replacing star-and-Earth pass on space's quad, two
            //techniques in one effect. Nothing on it moves, so there is no time parameter to cache.
            _moonEffect = content.Load<Effect>("Shaders/Moon");
            Services.AcquireGridMesh(MOON_GRID_N, MOON_EXTENT, out _moonVertexBuffer, out _moonIndexBuffer, out _moonIndexCount);

            _moonSkyTechnique = _moonEffect.Techniques["MoonSky"];
            _moonTerrainTechnique = _moonEffect.Techniques["MoonTerrain"];

            _moonInverseViewProjection = _moonEffect.Parameters["InverseViewProjection"];
            _moonView = _moonEffect.Parameters["View"];
            _moonProjection = _moonEffect.Parameters["Projection"];
            _moonCameraPosition = _moonEffect.Parameters["CameraPosition"];
            _moonSunDirection = _moonEffect.Parameters["SunDirection"];
            _moonSupersample = _moonEffect.Parameters["SupersampleFactor"];
            _moonOriginXZ = _moonEffect.Parameters["OriginXZ"];
            _moonHoleRadius = _moonEffect.Parameters["IslandHoleRadius"];

            ApplyMoonParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Moon;

        /// <inheritdoc/>
        public override SceneConfig Config => _moonConfig;

        private void ApplyMoonParameters()
        {
            MoonSceneConfig moon = _moonConfig;

            _moonEffect.Parameters["VoidColor"].SetValue(moon.VoidColor.ToVector3());

            MoonTerrainConfig terrain = moon.Terrain;
            _moonEffect.Parameters["MoonLevelY"].SetValue(terrain.LevelY);
            _moonEffect.Parameters["ClearingRadius"].SetValue(terrain.ClearingRadius);
            _moonEffect.Parameters["ClearingTransition"].SetValue(terrain.ClearingTransition);
            _moonEffect.Parameters["CraterAmplitude"].SetValue(terrain.CraterAmplitude);
            _moonEffect.Parameters["HighlandHeight"].SetValue(terrain.HighlandHeight);
            _moonEffect.Parameters["HighlandInnerRadius"].SetValue(terrain.HighlandInnerRadius);
            _moonEffect.Parameters["HighlandCrestRadius"].SetValue(terrain.HighlandCrestRadius);
            _moonEffect.Parameters["HighlandSaddleFloor"].SetValue(terrain.HighlandSaddleFloor);
            _moonEffect.Parameters["Curvature"].SetValue(terrain.Curvature);
            _moonEffect.Parameters["RegolithColor"].SetValue(terrain.RegolithColor.ToVector3());
            _moonEffect.Parameters["RegolithColorPale"].SetValue(terrain.RegolithColorPale.ToVector3());
            _moonEffect.Parameters["EjectaBrightness"].SetValue(terrain.EjectaBrightness);
            _moonEffect.Parameters["MicroReliefStrength"].SetValue(terrain.MicroReliefStrength);
            _moonEffect.Parameters["GrainStrength"].SetValue(terrain.GrainStrength);

            //The terrain's sun and fill are the config's own, never the frame's dome-derived ones: this
            //scene draws no dome, and a dome-derived sun on a domeless ground would be the lie
            //TryGetLightRig's doc warns about, painted onto the terrain instead of the island.
            _moonEffect.Parameters["SunColor"].SetValue(terrain.SunColor.ToVector3());
            _moonEffect.Parameters["AmbientColor"].SetValue(terrain.AmbientColor.ToVector3());

            //The earthshine the terrain shader adds as a directional fill is derived from the same figures
            //the scene-light slot uses (TryGetMoonEarthshine), so the ground and the island cannot disagree
            //about how bright the Earth is.
            MoonLightingConfig lighting = moon.Lighting;
            MoonEarthConfig earth = moon.Earth;

            Vector3 earthAlbedo = earth.CloudColor.ToVector3() * 0.6f + earth.OceanColor.ToVector3() * 0.4f;
            float earthPeak = MathF.Max(MathF.Max(earthAlbedo.X, earthAlbedo.Y), MathF.Max(earthAlbedo.Z, 1e-4f));
            bool shines = lighting.EarthshineStrength > 0f && earth.AngularRadiusDegrees > 0f;

            _moonEffect.Parameters["EarthshineColor"].SetValue(
                shines ? earthAlbedo / earthPeak * lighting.EarthshineStrength : Vector3.Zero);

            _moonEffect.Parameters["EarthDirection"].SetValue(SceneRenderer.SafeNormal(earth.Direction.ToVector3(), Vector3.Forward));
            _moonEffect.Parameters["EarthAngularRadius"].SetValue(MathHelper.ToRadians(earth.AngularRadiusDegrees));
            _moonEffect.Parameters["EarthAxis"].SetValue(SceneRenderer.SafeNormal(earth.Axis.ToVector3(), Vector3.Up));
            _moonEffect.Parameters["OceanColor"].SetValue(earth.OceanColor.ToVector3());
            _moonEffect.Parameters["LandColor"].SetValue(earth.LandColor.ToVector3());
            _moonEffect.Parameters["LandColorArid"].SetValue(earth.LandColorArid.ToVector3());
            _moonEffect.Parameters["CloudColor"].SetValue(earth.CloudColor.ToVector3());
            _moonEffect.Parameters["CloudAmount"].SetValue(earth.CloudAmount);
            _moonEffect.Parameters["RimColor"].SetValue(earth.RimColor.ToVector3());
            _moonEffect.Parameters["RimStrength"].SetValue(earth.RimStrength);
            _moonEffect.Parameters["NightAmbient"].SetValue(earth.NightAmbient);

            SpaceStarsConfig stars = moon.Stars;
            _moonEffect.Parameters["StarCellScale"].SetValue(new[] { stars.BrightCellScale, stars.MediumCellScale, stars.FaintCellScale });
            _moonEffect.Parameters["StarChance"].SetValue(new[] { stars.BrightChance, stars.MediumChance, stars.FaintChance });
            _moonEffect.Parameters["StarPeak"].SetValue(new[] { stars.BrightPeak, stars.MediumPeak, stars.FaintPeak });
            _moonEffect.Parameters["StarSpread"].SetValue(stars.Spread);
            _moonEffect.Parameters["StarFalloff"].SetValue(stars.Falloff);
            _moonEffect.Parameters["StarSpikeThreshold"].SetValue(stars.SpikeThreshold);
            _moonEffect.Parameters["StarSpikeLength"].SetValue(stars.SpikeLength);
        }

        /// <summary>
        /// Draws the Moon: two passes of one effect, because the scene is in both families at once (#125).
        /// First the terrain — the desert's displaced camera-centred grid (snapped to a cell so the craters
        /// do not swim), an ordinary depth-writing opaque draw — then the sky: space's full-screen machinery
        /// on the shared quad, <b>depth-read</b> against what the terrain just wrote, so the star shader
        /// only runs where sky is actually visible. No cloud hook and no time uniform: there is no air and
        /// nothing on the Moon moves.
        /// <para>
        /// <b>The order is measured, not stylistic.</b> The first build drew the sky first with
        /// <see cref="DepthStencilState.None"/> (the other sky-replacing scenes' state) and the terrain over
        /// it, and in the Game's frame that interleave measured <b>244 ms</b> at High on the reference APU
        /// against <b>17 ms</b> for this order — an 8× blow-up that neither pass shows alone (sky alone 8 ms,
        /// terrain alone 18) and that the Testbed's frame never reproduced. The mechanism was not chased past
        /// the fix, because depth-read-after-terrain is the right order regardless: it also stops paying for
        /// starfield pixels the ground was always going to cover.
        /// </para>
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            //Row vectors, as everywhere else: a world point goes out through View then Projection, so a
            //clip-space corner comes back through the inverse of that product.
            _moonInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _moonView.SetValue(frame.Camera.View);
            _moonProjection.SetValue(frame.Camera.Projection);
            _moonCameraPosition.SetValue(frame.Camera.Position);
            _moonSunDirection.SetValue(frame.SunDirection);
            _moonSupersample.SetValue((float)Services.SupersampleFactor);

            float cell = MOON_EXTENT / (MOON_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _moonOriginXZ.SetValue(new Vector2(originX, originZ));
            _moonHoleRadius.SetValue(Services.TerrainHoleRadius);

            //The ground first, an ordinary depth-writing opaque draw. Unlike the other three sky-replacing
            //scenes the backdrop pass is NOT unconditional here — half the frame is ground — so the terrain
            //goes in first and the sky pass reads the depth it wrote. Every state is stated, not inherited
            //(the repo rule): this is the frame's first scene draw in two of the three hosts.
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_moonVertexBuffer);
            _graphicsDevice.Indices = _moonIndexBuffer;
            _moonEffect.CurrentTechnique = _moonTerrainTechnique;
            _moonEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _moonIndexCount / 3);

            //Then the sky, depth-READ at the far plane (the quad sits at z = w): every pixel the terrain
            //already owns is rejected before the star shader runs, so the sky pass only pays for the sky
            //that is visible. DepthRead, not None — the test is what buys that, and writing is what the
            //backdrop must never do.
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _moonEffect.CurrentTechnique = _moonSkyTechnique;
            _moonEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Put back what the rest of the opaque scene wants
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetSunDirection(out Vector3 direction)
        {
            MoonLightingConfig lighting = _moonConfig.Lighting;
            direction = SceneRenderer.DirectionFromElevationAzimuth(
                Math.Clamp(lighting.SunElevationDegrees, 1f, 89f), lighting.SunAzimuthDegrees);
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            //The Moon's is the one rig whose GROUND half outshines its sky half: the sky is black and
            //the sunlit regolith below is the only diffuse source there is — Apollo photographs fill
            //their shadows from the ground, not the sky (see MoonLightingConfig).
            MoonLightingConfig moon = _moonConfig.Lighting;
            rig = new SceneLightRig(
                moon.SkyAmbient.ToVector3(),
                moon.GroundAmbient.ToVector3(),
                moon.KeyTint.ToVector3(),
                moon.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The highland belt, at the crest radius its own config states — the Moon's skyline is a
            //stated distance rather than a haze, so this is one of the few points in the table that can
            //be exactly right.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _moonConfig.Terrain.HighlandCrestRadius,
                    _moonConfig.Terrain.LevelY + _moonConfig.Terrain.HighlandHeight * 0.6f),
                2.3f, 11f, 0f, "the highlands");
            return true;
        }

        /// <summary>The earthshine as a scene point light; see <see cref="SceneRenderer.TryGetMoonEarthshine"/>.</summary>
        public bool TryGetEarthshine(out Vector3 position, out Vector3 color, out float range)
        {
            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;

            MoonEarthConfig earth = _moonConfig.Earth;
            MoonLightingConfig lighting = _moonConfig.Lighting;

            if (lighting.EarthshineStrength <= 0f || earth.AngularRadiusDegrees <= 0f) return false;

            Vector3 direction = SceneRenderer.SafeNormal(earth.Direction.ToVector3(), Vector3.Forward);

            position = direction * lighting.EarthshineDistance;

            //Earthshine is sunlight bounced off a mostly-ocean, mostly-cloud disc, so its hue is the
            //marble's own: the cloud white pulled towards the ocean blue. Normalized like the planetshine,
            //so the strength alone says how bright the fill is and the colours only say its hue.
            Vector3 albedo = earth.CloudColor.ToVector3() * 0.6f + earth.OceanColor.ToVector3() * 0.4f;
            float peak = MathF.Max(MathF.Max(albedo.X, albedo.Y), MathF.Max(albedo.Z, 1e-4f));

            color = albedo / peak * lighting.EarthshineStrength;

            range = lighting.EarthshineDistance * 3f;

            return true;
        }
    }
}
