using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Deep space (the ninth scene, and the first that replaces the SKY rather than the ground): one
    /// full-screen pass of <c>Space.fx</c> over the renderer's shared quad, the view ray recovered per pixel.
    /// Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its cached
    /// parameters, the push, the draw, the light rig, the viewpoint and the planetshine — as the pattern the
    /// other scenes follow. See "Space" in docs/scenes.md for what the scene does.
    /// </summary>
    internal sealed class SpaceBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private SpaceSceneConfig _spaceConfig = new();

        private readonly Effect _spaceEffect;

        //The handful of parameters that change per frame, resolved once (BestPractices §1: the by-name
        //indexer is a linear scan). Everything else is pushed by ApplySpaceParameters when a config lands.
        private readonly EffectParameter _spaceInverseViewProjection, _spaceCameraPosition, _spaceSunDirection, _spaceSupersample, _spaceTime;

        /// <summary>Loads the effect, caches its per-frame parameters and pushes the config at it.</summary>
        public SpaceBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Space: the ninth scene, and the first of the two with no ground at all (the dream below is
            //the other) — a full-screen pass whose quad is already in normalized device coordinates, so
            //nothing transforms it
            _spaceEffect = content.Load<Effect>("Shaders/Space");

            _spaceInverseViewProjection = _spaceEffect.Parameters["InverseViewProjection"];
            _spaceCameraPosition = _spaceEffect.Parameters["CameraPosition"];
            _spaceSunDirection = _spaceEffect.Parameters["SunDirection"];
            _spaceSupersample = _spaceEffect.Parameters["SupersampleFactor"];
            _spaceTime = _spaceEffect.Parameters["SpaceTime"];

            ApplySpaceParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Space;

        /// <inheritdoc/>
        public override SceneConfig Config => _spaceConfig;

        /// <summary>
        /// Pushes the whole space sky at the shader. Everything here is fixed for as long as the config is —
        /// the sky does not move, there is no wind and no weather — so this runs on a config change and never
        /// per frame; only the camera, the sun and the supersampling factor go out in <see cref="Draw"/>.
        /// <para>
        /// Two conversions happen here rather than in the shader, and both are deliberate. Angles are authored
        /// in <b>degrees</b> and arrive as radians, because a designer types "twelve degrees across". And the
        /// directions are normalized — with the galactic core <b>orthogonalised against the pole</b> — so a
        /// hand-typed pair never has to be exactly perpendicular for the bulge to sit in the plane.
        /// </para>
        /// </summary>
        private void ApplySpaceParameters()
        {
            SpaceSceneConfig space = _spaceConfig;

            _spaceEffect.Parameters["VoidColor"].SetValue(space.VoidColor.ToVector3());

            //The volume the island is inside — the one layer of this scene with depth rather than only a
            //direction, and so the only one the camera can move through (see Space.fx's StarNestVolume)
            SpaceVolumeConfig volume = space.Volume;
            _spaceEffect.Parameters["VolumeStrength"].SetValue(volume.Strength);
            _spaceEffect.Parameters["VolumeScale"].SetValue(volume.Scale);
            _spaceEffect.Parameters["VolumeDrift"].SetValue(volume.Drift);
            _spaceEffect.Parameters["VolumeSaturation"].SetValue(volume.Saturation);
            _spaceEffect.Parameters["VolumeOpacity"].SetValue(volume.Opacity);
            _spaceEffect.Parameters["VolumeTint"].SetValue(volume.Tint.ToVector3());

            SpaceStarsConfig stars = space.Stars;
            _spaceEffect.Parameters["StarCellScale"].SetValue(new[] { stars.BrightCellScale, stars.MediumCellScale, stars.FaintCellScale });
            _spaceEffect.Parameters["StarChance"].SetValue(new[] { stars.BrightChance, stars.MediumChance, stars.FaintChance });
            _spaceEffect.Parameters["StarPeak"].SetValue(new[] { stars.BrightPeak, stars.MediumPeak, stars.FaintPeak });
            _spaceEffect.Parameters["StarSpread"].SetValue(stars.Spread);
            _spaceEffect.Parameters["StarFalloff"].SetValue(stars.Falloff);
            _spaceEffect.Parameters["StarSpikeThreshold"].SetValue(stars.SpikeThreshold);
            _spaceEffect.Parameters["StarSpikeLength"].SetValue(stars.SpikeLength);

            SpaceMilkyWayConfig milkyWay = space.MilkyWay;
            Vector3 pole = SceneRenderer.SafeNormal(milkyWay.Pole.ToVector3(), Vector3.Up);

            //The bulge has to lie in the galactic plane or the band's brightest part sits off it, so whatever
            //was typed is projected onto the plane before it is used. If the two happen to be parallel the
            //projection vanishes, and any direction in the plane will do.
            Vector3 core = milkyWay.CoreDirection.ToVector3() - pole * Vector3.Dot(milkyWay.CoreDirection.ToVector3(), pole);
            core = SceneRenderer.SafeNormal(core, AnyPerpendicular(pole));

            _spaceEffect.Parameters["GalacticPole"].SetValue(pole);
            _spaceEffect.Parameters["GalacticCore"].SetValue(core);
            _spaceEffect.Parameters["MilkyWayWidth"].SetValue(milkyWay.Width);
            _spaceEffect.Parameters["MilkyWayBrightness"].SetValue(milkyWay.Brightness);
            _spaceEffect.Parameters["MilkyWayColor"].SetValue(milkyWay.Color.ToVector3());
            _spaceEffect.Parameters["MilkyWayCoreColor"].SetValue(milkyWay.CoreColor.ToVector3());
            _spaceEffect.Parameters["MilkyWayDust"].SetValue(milkyWay.Dust);
            _spaceEffect.Parameters["MilkyWayStarBoost"].SetValue(milkyWay.StarBoost);

            SpaceNebulaConfig[] nebulae = { space.NebulaOne, space.NebulaTwo, space.NebulaThree };
            Vector3[] nebulaDirections = new Vector3[nebulae.Length];
            Vector3[] nebulaColors = new Vector3[nebulae.Length];
            Vector4[] nebulaShapes = new Vector4[nebulae.Length];

            for (int i = 0; i < nebulae.Length; i++)
            {
                nebulaDirections[i] = SceneRenderer.SafeNormal(nebulae[i].Direction.ToVector3(), Vector3.Forward);
                nebulaColors[i] = nebulae[i].Color.ToVector3();
                nebulaShapes[i] = new Vector4(
                    MathHelper.ToRadians(nebulae[i].AngularRadiusDegrees),
                    nebulae[i].Strength,
                    nebulae[i].DetailScale,
                    nebulae[i].Warp);
            }

            _spaceEffect.Parameters["NebulaDirection"].SetValue(nebulaDirections);
            _spaceEffect.Parameters["NebulaColor"].SetValue(nebulaColors);
            _spaceEffect.Parameters["NebulaShape"].SetValue(nebulaShapes);

            SpaceGalaxyConfig galaxies = space.Galaxies;
            _spaceEffect.Parameters["GalaxyCellScale"].SetValue(galaxies.CellScale);
            _spaceEffect.Parameters["GalaxyChance"].SetValue(galaxies.Chance);
            _spaceEffect.Parameters["GalaxySize"].SetValue(MathHelper.ToRadians(galaxies.AngularSizeDegrees));
            _spaceEffect.Parameters["GalaxyBrightness"].SetValue(galaxies.Brightness);
            _spaceEffect.Parameters["GalaxyColor"].SetValue(galaxies.Color.ToVector3());

            SpacePlanetConfig planet = space.Planet;
            _spaceEffect.Parameters["PlanetDirection"].SetValue(SceneRenderer.SafeNormal(planet.Direction.ToVector3(), Vector3.Forward));
            _spaceEffect.Parameters["PlanetAngularRadius"].SetValue(MathHelper.ToRadians(planet.AngularRadiusDegrees));
            _spaceEffect.Parameters["PlanetAxis"].SetValue(SceneRenderer.SafeNormal(planet.Axis.ToVector3(), Vector3.Up));
            _spaceEffect.Parameters["PlanetColorLight"].SetValue(planet.ColorLight.ToVector3());
            _spaceEffect.Parameters["PlanetColorDark"].SetValue(planet.ColorDark.ToVector3());
            _spaceEffect.Parameters["PlanetStormColor"].SetValue(planet.StormColor.ToVector3());
            _spaceEffect.Parameters["PlanetRimColor"].SetValue(planet.RimColor.ToVector3());
            _spaceEffect.Parameters["PlanetBandScale"].SetValue(planet.BandScale);
            _spaceEffect.Parameters["PlanetRimStrength"].SetValue(planet.RimStrength);
            _spaceEffect.Parameters["PlanetNightAmbient"].SetValue(planet.NightAmbient);
        }

        /// <summary>
        /// Some unit vector perpendicular to <paramref name="axis"/>, mirroring <c>Space.fx</c>'s
        /// <c>BuildFrame</c>: the reference vector is swapped near the pole so the cross product cannot
        /// degenerate, whatever axis the config states. It is a <see cref="SceneRenderer.SafeNormal"/> fallback that is
        /// itself never zero, which the obvious <c>Cross(axis, Vector3.Right)</c> is not — that one collapses
        /// for an axis along X, and a zero galactic core would flatten the band's whole core gradient rather
        /// than announcing itself.
        /// </summary>
        private static Vector3 AnyPerpendicular(Vector3 axis) =>
            Vector3.Normalize(Vector3.Cross(MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right, axis));

        /// <summary>
        /// Draws deep space: one full-screen pass over a quad already in normalized device coordinates, the
        /// view ray recovered per pixel from the inverse view-projection. The odd one out among these draws,
        /// and every difference follows from replacing the <b>sky</b> rather than the ground:
        /// <list type="bullet">
        /// <item>No grid, no camera snapping and no <c>OriginXZ</c> — there is nothing on the ground to swim.</item>
        /// <item>No <c>IslandHoleRadius</c> — nothing is cut out, because nothing is drawn under the island.</item>
        /// <item>No cloud hook — space has no weather, and the caller suppresses the cloud shadow on the
        /// instanced effect so the island and the balls are not crossed by a deck that is not drawn.</item>
        /// <item><see cref="DepthStencilState.None"/> rather than the usual depth-writing opaque draw: this is
        /// the background, so it writes no depth and everything drawn after it simply covers it.</item>
        /// </list>
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            //Row vectors, as everywhere else in this project: a world point goes out through View then
            //Projection, so a clip-space corner comes back through the inverse of that product.
            _spaceInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));
            _spaceCameraPosition.SetValue(frame.Camera.Position);
            _spaceSunDirection.SetValue(frame.SunDirection);
            _spaceSupersample.SetValue((float)Services.SupersampleFactor);

            //The only animated thing in a long-exposure sky: the eye's slow drift through the volume it is
            //inside. Wall clock, like every other scene's, so it keeps moving while the simulation is paused.
            _spaceTime.SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _spaceEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            //Put back what the rest of the opaque scene wants. The depth state especially: left at None, the
            //island would not occlude the cluster and the whole frame would draw in submission order.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            SpaceLightingConfig space = _spaceConfig.Lighting;
            rig = new SceneLightRig(
                space.SkyAmbient.ToVector3(),
                space.GroundAmbient.ToVector3(),
                space.KeyTint.ToVector3(),
                space.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The planet, which is the one thing in the space scene with a POSITION — the stars, the
            //nebulae and the Milky Way are a sky and are in frame from anywhere. Behind the arena, so the
            //island hangs against it: a planet with nothing in front of it has no scale.
            viewpoint = new SceneViewpoint(
                SceneRenderer.SafeNormal(_spaceConfig.Planet.Direction.ToVector3(), -Vector3.UnitZ) * 620f,
                2.0f, 12f, 180f, "the planet");
            return true;
        }

        /// <summary>The planetshine as a scene point light; see <see cref="SceneRenderer.TryGetSpacePlanetshine"/>.</summary>
        public bool TryGetPlanetshine(out Vector3 position, out Vector3 color, out float range)
        {
            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;

            SpacePlanetConfig planet = _spaceConfig.Planet;
            SpaceLightingConfig lighting = _spaceConfig.Lighting;

            if (lighting.PlanetshineStrength <= 0f || planet.AngularRadiusDegrees <= 0f) return false;

            Vector3 direction = SceneRenderer.SafeNormal(planet.Direction.ToVector3(), Vector3.Forward);

            position = direction * lighting.PlanetshineDistance;

            //The planet's own colour is what it reflects back, and its pale bands are what most of the disc
            //is; normalized so the strength alone says how bright the fill is and the colour only says its hue
            Vector3 albedo = planet.ColorLight.ToVector3();
            float peak = MathF.Max(MathF.Max(albedo.X, albedo.Y), MathF.Max(albedo.Z, 1e-4f));

            color = albedo / peak * lighting.PlanetshineStrength;

            //The falloff is (1 - d/range)^2, so the light has to stand well inside its own range or it
            //arrives as nothing. At three times the distance it is 4/9 of full here and varies by a few per
            //cent across the island, which is what makes a point light stand in for a distant one.
            range = lighting.PlanetshineDistance * 3f;

            return true;
        }
    }
}
