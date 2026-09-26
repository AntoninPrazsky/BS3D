using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Mars (#277): the Moon's crater field retextured rust and ochre on a camera-centred grid under an ordinary
    /// dome, and Phobos and Deimos drawn over it on the shared full-screen quad. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>, the
    /// push, the reduced program and its coarser grid (#540), the terrain and moons draws, the viewpoint, and the
    /// shadow map's fit and receiver. See "Mars" in docs/scenes.md.
    /// </summary>
    internal sealed class MarsBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private MarsSceneConfig _marsConfig = new();

        private readonly Effect _marsEffect;

        //Its camera grid and the pass that draws it (#580); a tier's crossing retakes the grid (SelectMarsTechnique, #540)
        private readonly TerrainPass _marsPass;

        //The crater field is evaluated four times a pixel (the vertex tap plus the normal's three taps),
        //the Moon's own reason for its grid density; Mars keeps it rather than the coarser desert grid.
        private const int MARS_GRID_N = 360;

        //The reduced program's grid (#540), the volcano's VOLCANO_GRID_N_REDUCED for the same reason: see SelectMarsTechnique
        private const int MARS_GRID_N_REDUCED = 256;
        private const float MARS_EXTENT = 1000f;

        //The moons pass shares the sky-replacing scenes' full-screen-quad machinery (Services.FullScreenQuad), so
        //only its two per-frame ray-reconstruction parameters are cached (BestPractices §1) — the terrain pass's
        //own are TerrainPass's.
        private EffectParameter _marsMoonsInverseViewProjection, _marsMoonsCameraPosition;
        private EffectTechnique _marsTerrainTechnique, _marsTerrainFull, _marsTerrainReduced, _marsMoonsTechnique;

        //Look/tuning parameters (clearing, craters, rust surface, dust haze, the two moons) live in
        //MarsSceneConfig; the backdrop reads them from _marsConfig.

        /// <summary>
        /// Loads the effect, takes its grid through its <see cref="TerrainPass"/>, picks the ground's program for
        /// <paramref name="sceneDetail"/> (the renderer's tier at load) and pushes the config at it.
        /// </summary>
        public MarsBackdrop(BackdropServices services, ContentManager content, float sceneDetail) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Mars (#277): the sixteenth scene - the Moon's crater field (#125) retextured rust/ochre on
            //the outback's plumbing (an ordinary dome, the shared cloud shadow, a haze-closed horizon)
            //rather than the Moon's domeless, curvature-closed one. Two techniques in one effect: the
            //terrain grid, and a small full-screen pass for Phobos and Deimos sharing the sky-replacing
            //scenes' own quad (Services.FullScreenQuad).
            _marsEffect = content.Load<Effect>("Shaders/Mars");
            _marsPass = new TerrainPass(Services, _marsEffect, MARS_GRID_N, MARS_EXTENT, null);

            //The authored ground and its reduced program; SceneDetail picks between them (SelectMarsTechnique)
            _marsTerrainFull = _marsEffect.Techniques["MarsTerrain"];
            _marsTerrainReduced = _marsEffect.Techniques["MarsTerrainReduced"];
            SelectMarsTechnique(sceneDetail);
            _marsMoonsTechnique = _marsEffect.Techniques["MarsMoons"];

            _marsMoonsInverseViewProjection = _marsEffect.Parameters["InverseViewProjection"];
            _marsMoonsCameraPosition = _marsEffect.Parameters["CameraPosition"];

            ApplyMarsParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Mars;

        /// <inheritdoc/>
        public override SceneConfig Config => _marsConfig;

        /// <summary>
        /// Pushes Mars's static tuning into <c>Mars.fx</c> — the crater field's amplitude and clearing, the
        /// rust surface, the dust haze, and Phobos's and Deimos's directions (elevation/azimuth degrees,
        /// converted here the way <see cref="SkyDome"/> converts its own <c>SUNS</c> table, so a config can
        /// never roll a zero-length direction).
        /// </summary>
        private void ApplyMarsParameters()
        {
            MarsTerrainConfig terrain = _marsConfig.Terrain;
            MarsSurfaceConfig surface = _marsConfig.Surface;
            MarsAirConfig air = _marsConfig.Air;
            MarsMoonsConfig moons = _marsConfig.Moons;

            _marsEffect.Parameters["MarsLevelY"].SetValue(terrain.LevelY);
            _marsEffect.Parameters["ClearingRadius"].SetValue(terrain.ClearingRadius);
            _marsEffect.Parameters["ClearingTransition"].SetValue(terrain.ClearingTransition);
            _marsEffect.Parameters["CraterAmplitude"].SetValue(terrain.CraterAmplitude);

            //The spacings divide a world position in the shader, so a zero would take the whole terrain
            //with it (the outback's own guard).
            _marsEffect.Parameters["RockSpacing"].SetValue(MathF.Max(terrain.RockSpacing, 1f));
            _marsEffect.Parameters["RockChance"].SetValue(terrain.RockChance);
            _marsEffect.Parameters["RockHeight"].SetValue(terrain.RockHeight);
            _marsEffect.Parameters["PebbleSpacing"].SetValue(MathF.Max(terrain.PebbleSpacing, 1f));
            _marsEffect.Parameters["PebbleChance"].SetValue(terrain.PebbleChance);
            _marsEffect.Parameters["PebbleHeight"].SetValue(terrain.PebbleHeight);

            _marsEffect.Parameters["RustColor"].SetValue(surface.RustColor.ToVector3());
            _marsEffect.Parameters["RustColorPale"].SetValue(surface.RustColorPale.ToVector3());
            _marsEffect.Parameters["EjectaBrightness"].SetValue(surface.EjectaBrightness);
            _marsEffect.Parameters["MicroReliefStrength"].SetValue(surface.MicroReliefStrength);
            _marsEffect.Parameters["GrainStrength"].SetValue(surface.GrainStrength);
            _marsEffect.Parameters["AmbientStrength"].SetValue(surface.AmbientStrength);
            _marsEffect.Parameters["BoulderColorDeep"].SetValue(surface.BoulderColorDeep.ToVector3());
            _marsEffect.Parameters["BoulderColorBright"].SetValue(surface.BoulderColorBright.ToVector3());
            _marsEffect.Parameters["RockRelief"].SetValue(surface.RockRelief);

            _marsEffect.Parameters["MesaHeight"].SetValue(terrain.MesaHeight);
            _marsEffect.Parameters["MesaInnerRadius"].SetValue(terrain.MesaInnerRadius);
            _marsEffect.Parameters["MesaThreshold"].SetValue(terrain.MesaThreshold);
            _marsEffect.Parameters["StrataColorPale"].SetValue(surface.StrataColorPale.ToVector3());
            _marsEffect.Parameters["StrataColorDark"].SetValue(surface.StrataColorDark.ToVector3());
            _marsEffect.Parameters["StrataFrequency"].SetValue(surface.StrataFrequency);
            _marsEffect.Parameters["SandColor"].SetValue(surface.SandColor.ToVector3());
            _marsEffect.Parameters["SandCoverage"].SetValue(surface.SandCoverage);
            _marsEffect.Parameters["SlabColor"].SetValue(surface.SlabColor.ToVector3());
            _marsEffect.Parameters["SlabCoverage"].SetValue(surface.SlabCoverage);

            _marsEffect.Parameters["HazeTint"].SetValue(air.HazeTint.ToVector3());
            _marsEffect.Parameters["DustStrength"].SetValue(air.DustStrength);
            _marsEffect.Parameters["HorizonHazeDistance"].SetValue(air.HorizonHazeDistance);

            _marsEffect.Parameters["PhobosDirection"].SetValue(SceneRenderer.DirectionFromElevationAzimuth(moons.PhobosElevation, moons.PhobosAzimuth));
            _marsEffect.Parameters["PhobosAngularRadius"].SetValue(MathHelper.ToRadians(MathF.Max(moons.PhobosAngularRadiusDegrees, 0f)));
            _marsEffect.Parameters["PhobosColor"].SetValue(moons.PhobosColor.ToVector3());

            _marsEffect.Parameters["DeimosDirection"].SetValue(SceneRenderer.DirectionFromElevationAzimuth(moons.DeimosElevation, moons.DeimosAzimuth));
            _marsEffect.Parameters["DeimosAngularRadius"].SetValue(MathHelper.ToRadians(MathF.Max(moons.DeimosAngularRadiusDegrees, 0f)));
            _marsEffect.Parameters["DeimosColor"].SetValue(moons.DeimosColor.ToVector3());
        }

        /// <summary>
        /// Mars's full ground or its reduced one: the sand drifts, the bedrock slabs and the strata's wobble are the
        /// added noise the reduced program drops, the mesas staying on every tier. Held as a cached technique rather
        /// than looked up, since <see cref="Draw"/> assigns it every frame.
        /// </summary>
        private void SelectMarsTechnique(float sceneDetail)
        {
            _marsTerrainTechnique = sceneDetail > 0.5f ? _marsTerrainFull : _marsTerrainReduced;

            //And a coarser grid under the reduced program since #540, the volcano's own step: on the APU at Low,
            //360 -> 256 was 1.69 ms of Mars's frame (47 cycles, 100 %) and photographed identical, where the two
            //cuts to the ground's program that were tried beside it were not - the pebbles' lattice (1.12 ms) left
            //the plain visibly emptier, and the fourth crater octave with an octave off both reliefs (0.25) took
            //the small craters that make the field read. The pass takes its grid at MARS_GRID_N before this first runs
            //(from the constructor), so at the full tier that call retakes nothing; TerrainPass.SetGrid gives the old
            //grid back to the cache rather than disposing it.
            _marsPass.SetGrid(sceneDetail > 0.5f ? MARS_GRID_N : MARS_GRID_N_REDUCED);
        }

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail) => SelectMarsTechnique(sceneDetail);

        /// <summary>The ground, then Phobos and Deimos over it.</summary>
        public override void Draw(in SceneFrame frame)
        {
            DrawMarsTerrain(frame);
            DrawMarsMoons(frame);
        }

        /// <summary>
        /// Draws Mars (#277): the grid pinned to the camera (snapped to a cell so the land does not swim),
        /// carrying the Moon's crater field retextured rust and shaded per-pixel by the current dome and
        /// the shared cloud field — the outback's plumbing, not the Moon's domeless one. No point lights
        /// and no birds of its own, like the desert and the outback.
        /// </summary>
        private void DrawMarsTerrain(in SceneFrame frame)
        {
            //The ground technique first: the moons pass leaves its own as the current one, and the pass applies the current
            _marsEffect.CurrentTechnique = _marsTerrainTechnique;
            _marsPass.Draw(frame, Services.TerrainHoleRadius);
        }

        /// <summary>
        /// Draws Phobos and Deimos: two small analytic discs on space's shared full-screen quad
        /// (<see cref="BackdropServices.FullScreenQuad"/>), depth-read against the depth <see cref="DrawMarsTerrain"/> just wrote —
        /// Moon.fx's own measured reason (<c>MoonBackdrop.Draw</c>'s doc) for reading depth after the ground
        /// rather than before it, carried over even though this pass is far cheaper than a starfield.
        /// Alpha-blended, unlike every sky-replacing scene's opaque quad pass: this composites two small
        /// discs over a dome and a terrain that are already drawn, not a full-screen backdrop of its own.
        /// </summary>
        private void DrawMarsMoons(in SceneFrame frame)
        {
            _marsMoonsInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _marsMoonsCameraPosition.SetValue(frame.Camera.Position);
            _marsEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _marsEffect.CurrentTechnique = _marsMoonsTechnique;
            _marsEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Phobos, placed the same designer-facing way the shader places it. The bigger of the two
            //moons and the higher-contrast one: Deimos is under half its angular size.
            viewpoint = new SceneViewpoint(
                SceneRenderer.DirectionFromElevationAzimuth(_marsConfig.Moons.PhobosElevation, _marsConfig.Moons.PhobosAzimuth) * 700f,
                1.9f, 16f, 180f, "Phobos");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get { yield return _marsEffect; }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _marsConfig.Terrain.LevelY;
            below = _marsConfig.Terrain.CraterAmplitude * 2f;
            above = _marsConfig.Terrain.MesaHeight * 0.5f;
            return true;
        }
    }
}
