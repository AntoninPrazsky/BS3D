using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The aurora (the eighteenth scene, #205): a forest clearing grid under a sky-replacing star-and-ribbon pass,
    /// two techniques of <c>Aurora.fx</c> in the Moon's shape, with the snow falling over it. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580 — the config, the effect, its grid, the push, the draw, the snow's
    /// own clone of <c>Snow.fx</c>, the animated light rig and the glow it shares with the ground, the viewpoint and
    /// the terrain probe <c>mirrorcheck</c> reads. The renderer's public <c>AuroraGlowColor</c> forwards here. See
    /// "The aurora" in docs/scenes.md.
    /// </summary>
    internal sealed class AuroraBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private AuroraSceneConfig _auroraConfig = new();

        //The eighteenth scene (#205), and the second in both families at once — see IsSolidTerrainScene's
        //and ReplacesSky's own docs. A forest clearing grid like Forest.fx's under a sky-replacing pass on
        //space's shared quad, two techniques in one effect, the Moon's own shape (Draw runs the
        //terrain first, depth-writing, then the sky quad depth-READ against it — the Moon's measured order,
        //see MoonBackdrop.Draw's doc; the opposite interleave was an 8x blow-up there and is not being re-measured
        //here to find out whether it still is).
        private readonly Effect _auroraEffect;
        private readonly VertexBuffer _auroraVertexBuffer;
        private readonly IndexBuffer _auroraIndexBuffer;
        private readonly int _auroraIndexCount;

        private readonly EffectTechnique _auroraSkyTechnique, _auroraTerrainTechnique;

        //Per-frame parameters, resolved once (BestPractices §1). SunColor/ZenithColor/HorizonColor are
        //per-frame here (unlike Forest.fx's dome-fed copies) because they carry the aurora's own pulsing
        //glow, not a fixed config value — see GlowColor. CameraPosition is the one uniform both
        //techniques read (the terrain's haze term and the sky quad's ray reconstruction alike), so it is
        //cached once and pushed once.
        private readonly EffectParameter _auroraOriginXZ, _auroraHoleRadius, _auroraView, _auroraProjection,
            _auroraCameraPosition, _auroraInverseViewProjection, _auroraTerrainTime, _auroraSkyTime,
            _auroraHueShift, _auroraSunColor, _auroraZenithColor, _auroraHorizonColor, _auroraSupersample;

        private const int AURORA_GRID_N = 220;
        private const float AURORA_EXTENT = 1200f;

        //The snow's own clone of Snow.fx (#580), its look pushed once in the constructor; the flake buffer is the
        //shared Services.Snowfall
        private readonly Effect _snowEffect;

        /// <summary>Loads the effect, takes its grid, caches its parameters and pushes the config at it.</summary>
        public AuroraBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Aurora (#205): the eighteenth scene, the second in both families at once — a forest
            //clearing grid like Forest.fx's under a sky-replacing star-and-ribbon pass on space's quad, two
            //techniques in one effect, exactly the Moon's own shape (see the field doc above).
            _auroraEffect = content.Load<Effect>("Shaders/Aurora");
            Services.AcquireGridMesh(AURORA_GRID_N, AURORA_EXTENT, out _auroraVertexBuffer, out _auroraIndexBuffer, out _auroraIndexCount);

            _auroraTerrainTechnique = _auroraEffect.Techniques["AuroraTerrain"];
            _auroraSkyTechnique = _auroraEffect.Techniques["AuroraSky"];

            _auroraOriginXZ = _auroraEffect.Parameters["OriginXZ"];
            _auroraHoleRadius = _auroraEffect.Parameters["IslandHoleRadius"];
            _auroraView = _auroraEffect.Parameters["View"];
            _auroraProjection = _auroraEffect.Parameters["Projection"];
            _auroraCameraPosition = _auroraEffect.Parameters["CameraPosition"];
            _auroraInverseViewProjection = _auroraEffect.Parameters["InverseViewProjection"];
            _auroraTerrainTime = _auroraEffect.Parameters["AuroraTerrainTime"];
            _auroraSkyTime = _auroraEffect.Parameters["AuroraSkyTime"];
            _auroraHueShift = _auroraEffect.Parameters["AuroraHueShift"];
            _auroraSunColor = _auroraEffect.Parameters["SunColor"];
            _auroraZenithColor = _auroraEffect.Parameters["ZenithColor"];
            _auroraHorizonColor = _auroraEffect.Parameters["HorizonColor"];
            _auroraSupersample = _auroraEffect.Parameters["SupersampleFactor"];

            ApplyAuroraParameters();

            //Its own snow (#205), through a clone of Snow.fx of its own since #580
            _snowEffect = content.Load<Effect>("Shaders/Snow").Clone();
            Snowfall.ApplyParameters(_snowEffect, _auroraConfig.Snow);
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Aurora;

        /// <inheritdoc/>
        public override SceneConfig Config => _auroraConfig;

        /// <summary>
        /// Pushes everything about the aurora scene that is fixed for as long as the config is — the ground
        /// shape (Forest.fx's own clearing-and-hills uniforms, off <c>_auroraConfig.Terrain</c>), the ribbon
        /// look and the star lattice. Not pushed here: <c>SunColor</c>/<c>ZenithColor</c>/<c>HorizonColor</c>
        /// and both time uniforms, which carry the aurora's own pulse and so go out every frame in
        /// <see cref="Draw"/> instead — the same split <c>MoonBackdrop.ApplyMoonParameters</c> makes between
        /// its fixed terrain figures and the per-frame camera/time ones.
        /// </summary>
        private void ApplyAuroraParameters()
        {
            _auroraEffect.Parameters["VoidColor"].SetValue(_auroraConfig.VoidColor.ToVector3());

            ForestSceneConfig terrain = _auroraConfig.Terrain;
            _auroraEffect.Parameters["ForestLevelY"].SetValue(terrain.LevelY);
            _auroraEffect.Parameters["HillHeight"].SetValue(terrain.HillHeight);
            _auroraEffect.Parameters["ClearingRadius"].SetValue(terrain.ClearingRadius);
            _auroraEffect.Parameters["ClearingTransition"].SetValue(terrain.ClearingTransition);
            _auroraEffect.Parameters["ClearingRelief"].SetValue(terrain.ClearingRelief);
            _auroraEffect.Parameters["FloorLumpStrength"].SetValue(terrain.FloorLumpStrength);
            _auroraEffect.Parameters["FloorLumpFrequency"].SetValue(terrain.FloorLumpFrequency);
            _auroraEffect.Parameters["ForestColor"].SetValue(terrain.ForestColor.ToVector3());
            _auroraEffect.Parameters["ForestColorDark"].SetValue(terrain.ForestColorDark.ToVector3());
            _auroraEffect.Parameters["TreelineColor"].SetValue(terrain.TreelineColor.ToVector3());
            _auroraEffect.Parameters["TreelineStrength"].SetValue(terrain.TreelineStrength);
            _auroraEffect.Parameters["AmbientStrength"].SetValue(terrain.AmbientStrength);
            _auroraEffect.Parameters["HorizonHazeDistance"].SetValue(terrain.HorizonHazeDistance);
            _auroraEffect.Parameters["WindDirection"].SetValue(terrain.Wind.ToVector2());
            _auroraEffect.Parameters["WindRippleSpeed"].SetValue(terrain.WindRippleSpeed);
            _auroraEffect.Parameters["WindRippleFrequency"].SetValue(terrain.WindRippleFrequency);
            _auroraEffect.Parameters["WindRippleStrength"].SetValue(terrain.WindRippleStrength);
            _auroraEffect.Parameters["NeedleReliefStrength"].SetValue(terrain.NeedleReliefStrength);
            _auroraEffect.Parameters["NeedleReliefFrequency"].SetValue(terrain.NeedleReliefFrequency);

            //Fixed straight up: the aurora is overhead rather than off at a dome's sun angle, and nothing
            //here ever moves it — Draw pushes the pulsing colour itself every frame instead.
            _auroraEffect.Parameters["SunDirection"].SetValue(Vector3.Up);

            AuroraSkyConfig aurora = _auroraConfig.Aurora;
            _auroraEffect.Parameters["AuroraColorLow"].SetValue(aurora.ColorLow.ToVector3());
            _auroraEffect.Parameters["AuroraColorHigh"].SetValue(aurora.ColorHigh.ToVector3());
            _auroraEffect.Parameters["AuroraIntensity"].SetValue(aurora.Intensity);
            _auroraEffect.Parameters["AuroraBandHeight"].SetValue(aurora.BandHeight);
            _auroraEffect.Parameters["AuroraBandSoftness"].SetValue(aurora.BandSoftness);
            _auroraEffect.Parameters["AuroraCurtainScale"].SetValue(aurora.CurtainScale);
            _auroraEffect.Parameters["AuroraCurtainWarp"].SetValue(aurora.CurtainWarp);
            _auroraEffect.Parameters["AuroraDriftSpeed"].SetValue(aurora.DriftSpeed);
            _auroraEffect.Parameters["AuroraMorphSpeed"].SetValue(aurora.MorphSpeed);
            _auroraEffect.Parameters["AuroraRayScale"].SetValue(aurora.RayScale);
            _auroraEffect.Parameters["AuroraRayStrength"].SetValue(aurora.RayStrength);
            _auroraEffect.Parameters["AuroraPulseSpeed"].SetValue(aurora.PulseSpeed);
            _auroraEffect.Parameters["AuroraPulseDepth"].SetValue(aurora.PulseDepth);

            SpaceStarsConfig stars = _auroraConfig.Stars;
            _auroraEffect.Parameters["StarCellScale"].SetValue(new[] { stars.BrightCellScale, stars.MediumCellScale, stars.FaintCellScale });
            _auroraEffect.Parameters["StarChance"].SetValue(new[] { stars.BrightChance, stars.MediumChance, stars.FaintChance });
            _auroraEffect.Parameters["StarPeak"].SetValue(new[] { stars.BrightPeak, stars.MediumPeak, stars.FaintPeak });
            _auroraEffect.Parameters["StarSpread"].SetValue(stars.Spread);
            _auroraEffect.Parameters["StarFalloff"].SetValue(stars.Falloff);
            _auroraEffect.Parameters["StarSpikeThreshold"].SetValue(stars.SpikeThreshold);
            _auroraEffect.Parameters["StarSpikeLength"].SetValue(stars.SpikeLength);
        }

        /// <summary>
        /// The aurora's slow hue drift at <paramref name="wallClock"/>, as the shift it adds to the sky's own
        /// low-to-high colour ramp (<c>Aurora.fx</c>'s <c>AuroraHueShift</c>): up to half of
        /// <see cref="AuroraSkyConfig.HueSwing"/> either way, positive towards <see cref="AuroraSkyConfig.ColorHigh"/>.
        /// </summary>
        private float AuroraHueShift(float wallClock) =>
            0.5f * _auroraConfig.Aurora.HueSwing * MathF.Sin(wallClock * _auroraConfig.Aurora.DriftHueSpeed);

        /// <summary>
        /// Where the band's light <b>as a whole</b> sits between <see cref="AuroraSkyConfig.ColorLow"/> (0) and
        /// <see cref="AuroraSkyConfig.ColorHigh"/> (1) before the drift moves it: the sky ramps green to violet
        /// with elevation inside the band, but the lower folds are the brighter and the larger part of what a
        /// camera near the ground sees, so the light the band throws is mostly green. Judged against captures
        /// of the sky (#462), not integrated.
        /// </summary>
        private const float AURORA_LIGHT_MIX = 0.3f;

        /// <summary>
        /// The colour mix of the light the aurora throws at <paramref name="wallClock"/> — <see cref="AURORA_LIGHT_MIX"/>
        /// moved by the very drift the sky draws (<see cref="AuroraHueShift"/>). The one number the ground's
        /// wash (<see cref="GlowColor"/>) and the light rig on the island, the gun and the balls
        /// (<see cref="TryGetLightRig"/>) both read, so neither can disagree with the sky over it.
        /// <para>
        /// <b>⚠ Until #462 there was no such agreement to have.</b> The drift was a CPU-side clock only — the
        /// sky's shader never drew it — and it swung the ground's light the whole way from pure green to pure
        /// violet, so for half of every cycle the clearing went violet under a sky that stayed green. Found
        /// when the island first took the same light and came out lavender under a green curtain.
        /// </para>
        /// </summary>
        private float AuroraLightMix(float wallClock) =>
            MathHelper.Clamp(AURORA_LIGHT_MIX + AuroraHueShift(wallClock), 0f, 1f);

        /// <summary>
        /// How finely the aurora's light rig follows the hue drift: the colour mix's 0–1 range in this many
        /// steps. A host re-lights its renderers only when a step changes the rig
        /// (<see cref="SkyLightRig.StepSceneLight"/>), and the Game's walk over them is an iterator — so a
        /// continuous hue would cost a re-light and an allocation every frame for a colour that takes over a
        /// minute to cross its range. At 128 steps a re-light comes about once a second at the drift's
        /// fastest, and each moves a tint by under one percent, which no eye catches as a step.
        /// </summary>
        private const float AURORA_RIG_STEPS = 128f;

        /// <summary>
        /// Carries a light part of the way to <paramref name="hue"/> <b>at its own brightness</b> — the idea
        /// <see cref="ForestScatterRenderer"/>'s <c>ShiftTowardsSky</c> applies to a pigment, applied to a
        /// light: what the aurora gives the island and the gun is its colour, not more or less light.
        /// </summary>
        private static Vector3 TowardsHue(Vector3 light, Vector3 hue, float strength)
        {
            float hueLuminance = ColorSpace.Luminance(hue);
            if (hueLuminance <= 1e-4f) return light;

            return Vector3.Lerp(light, hue * (ColorSpace.Luminance(light) / hueLuminance), strength);
        }

        /// <summary>The aurora's glow at <paramref name="wallClock"/>; see <see cref="SceneRenderer.AuroraGlowColor"/>.</summary>
        public Vector3 GlowColor(float wallClock)
        {
            AuroraSkyConfig aurora = _auroraConfig.Aurora;
            float drift = AuroraLightMix(wallClock);

            //A small fraction of the sky's own peak, not all of it (#205's first capture read as daylit
            //rather than night with the sky's own Intensity carried straight across): the sky pass draws
            //thin bright ribbons against a black void, but this feeds the GROUND's hemisphere term over its
            //own full dome, which integrates far more of it. Cut again after the owner's second look ("the
            //forest not so much") — 0.22 was still too bright once the rig itself was also dimmed
            //(AuroraLightingConfig's own class doc carries that half of the correction) — to a figure that
            //reads as a dark wood lit by its own aurora rather than one that looks sunlit at midnight.
            return Vector3.Lerp(aurora.ColorLow.ToVector3(), aurora.ColorHigh.ToVector3(), drift) * aurora.Intensity * 0.09f;
        }

        /// <summary>
        /// Draws the aurora scene: the forested ground first (depth-writing, opaque — Forest.fx's reduced
        /// floor, lit by <see cref="GlowColor"/> rather than a dome), then the sky quad depth-READ
        /// against it on the shared space quad — the Moon's measured order (see <see cref="MoonBackdrop.Draw"/>'s doc).
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            float cell = AURORA_EXTENT / (AURORA_GRID_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            Vector3 glow = GlowColor(frame.Time);

            _auroraOriginXZ.SetValue(new Vector2(originX, originZ));
            _auroraHoleRadius.SetValue(Services.TerrainHoleRadius);
            _auroraView.SetValue(frame.Camera.View);
            _auroraProjection.SetValue(frame.Camera.Projection);
            _auroraCameraPosition.SetValue(frame.Camera.Position);
            _auroraTerrainTime.SetValue(frame.Time);
            _auroraSunColor.SetValue(glow);
            _auroraZenithColor.SetValue(glow + _auroraConfig.GroundStarlight.ToVector3());
            _auroraHorizonColor.SetValue(_auroraConfig.Lighting.GroundAmbient.ToVector3());

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_auroraVertexBuffer);
            _graphicsDevice.Indices = _auroraIndexBuffer;
            _auroraEffect.CurrentTechnique = _auroraTerrainTechnique;
            _auroraEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _auroraIndexCount / 3);

            //Then the sky, depth-READ at the far plane: every pixel the terrain already owns is rejected
            //before the star-and-ribbon shader runs.
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _auroraInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _auroraSupersample.SetValue((float)Services.SupersampleFactor);
            _auroraSkyTime.SetValue(frame.Time);
            _auroraHueShift.SetValue(AuroraHueShift(frame.Time));

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _auroraEffect.CurrentTechnique = _auroraSkyTechnique;
            _auroraEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>The snow, through the shared flake buffer and the aurora's own clone of <c>Snow.fx</c>.</summary>
        public override void DrawOverlays(in SceneFrame frame) => Services.Snowfall.Draw(frame, _snowEffect, _auroraConfig.Snow);

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            //The aurora's takes the COLOUR of its sky (#462, the owner: "the glow should reflect its
            //light's colour onto the cannon and the island") but not its breathing — see
            //AuroraLightingConfig's class doc for why a gun that pulses with the sky reads as a fault. The
            //hue is the slow drift's (a cycle of minutes), stepped rather than continuous so a host re-lights
            //its renderers about once a second at most instead of every frame — see AnimatesLightRig.
            AuroraLightingConfig auroraLighting = _auroraConfig.Lighting;
            float steppedMix = MathF.Round(AuroraLightMix(wallClock) * AURORA_RIG_STEPS) / AURORA_RIG_STEPS;
            Vector3 hue = Vector3.Lerp(_auroraConfig.Aurora.ColorLow.ToVector3(),
                _auroraConfig.Aurora.ColorHigh.ToVector3(), steppedMix);
            rig = new SceneLightRig(
                TowardsHue(auroraLighting.SkyAmbient.ToVector3(), hue, auroraLighting.GlowTint),
                TowardsHue(auroraLighting.GroundAmbient.ToVector3(), hue, auroraLighting.GlowTint * 0.5f),
                TowardsHue(auroraLighting.KeyTint.ToVector3(), hue, auroraLighting.GlowTint),
                auroraLighting.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The aurora over the TREELINE, not overhead (#531): the point stood 260 units up until then,
            //which from a stand 10° up tilted the opening at the zenith — "looks far too high up at the
            //start", the owner's verdict on #462. It stands a little over the hills' canopy now, out past
            //the clearing, so the shot looks along the ragged line of spruce tips with the curtains over
            //it. ⚠ In the Game this stand is no longer flown at all: since #531 the aurora opens on a
            //prologue of its own shots and the tour flies only its last leg after one (ChapterIntro), so
            //what this states is the scene's viewpoint for any caller without a prologue.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _auroraConfig.Terrain.ClearingRadius + 60f,
                    _auroraConfig.Terrain.LevelY + _auroraConfig.Terrain.HillHeight + 30f),
                1.9f, 10f, 0f, "the aurora");
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _auroraEffect;
            mirror = (x, z) => TerrainMirror.Forest(x, z, _auroraConfig.Terrain);
            return true;
        }

        /// <summary>Frees the snow's clone. The terrain grid is the cache's and the effect the content manager's.</summary>
        public override void Dispose() => _snowEffect?.Dispose();
    }
}
