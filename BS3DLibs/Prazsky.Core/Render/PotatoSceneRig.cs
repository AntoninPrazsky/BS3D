using Microsoft.Xna.Framework;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What the scenes that replace the sky state for themselves, answered without a <see cref="SceneRenderer"/> (#829):
    /// the Potato path builds none, so <see cref="SkyLightRig"/> had only the dome to light by, and the Moon, the cavern
    /// and the Grid stood in the dome's lilac where the desktop lights them with the scene's own rig
    /// (<c>Backdrop.TryGetLightRig</c>) under a void. Each answer here is the figure the scene's own backdrop gives
    /// from its config's defaults - the aurora's back light breathing with its curtains as the desktop's does
    /// (<see cref="PotatoAuroraSky.ArcBreaths"/>) - and the colour its void or its fog is, for the frame to be cleared to
    /// in place of the dome.
    /// </summary>
    public static class PotatoSceneRig
    {
        //The configs' defaults, built once: the aurora's rig animates and is asked every frame
        private static readonly CavernSceneConfig CAVERN = new();
        private static readonly GridSceneConfig GRID = new();
        private static readonly MoonSceneConfig MOON = new();
        private static readonly SpaceSceneConfig SPACE = new();
        private static readonly DreamSceneConfig DREAM = new();
        private static readonly AuroraSceneConfig AURORA = new();
        private static readonly CircusSceneConfig CIRCUS = new();

        /// <summary>The scene's own rig, as its backdrop states it on the desktop; false for a scene under the dome.</summary>
        public static bool TryGetLightRig(SceneKind scene, float wallClock, out SceneLightRig rig)
        {
            switch (scene)
            {
                case SceneKind.Cavern: rig = Of(CAVERN.Lighting.SkyAmbient, CAVERN.Lighting.GroundAmbient, CAVERN.Lighting.KeyTint, CAVERN.Lighting.BackTint); return true;
                case SceneKind.Grid: rig = Of(GRID.Lighting.SkyAmbient, GRID.Lighting.GroundAmbient, GRID.Lighting.KeyTint, GRID.Lighting.BackTint); return true;
                case SceneKind.Moon: rig = Of(MOON.Lighting.SkyAmbient, MOON.Lighting.GroundAmbient, MOON.Lighting.KeyTint, MOON.Lighting.BackTint); return true;
                case SceneKind.Space: rig = Of(SPACE.Lighting.SkyAmbient, SPACE.Lighting.GroundAmbient, SPACE.Lighting.KeyTint, SPACE.Lighting.BackTint); return true;
                case SceneKind.Dream: rig = Of(DREAM.Lighting.SkyAmbient, DREAM.Lighting.GroundAmbient, DREAM.Lighting.KeyTint, DREAM.Lighting.BackTint); return true;
                case SceneKind.Aurora: rig = AuroraRig(wallClock); return true;
                case SceneKind.Circus: rig = Of(CIRCUS.Lighting.SkyAmbient, CIRCUS.Lighting.GroundAmbient, CIRCUS.Lighting.KeyTint, CIRCUS.Lighting.BackTint); return true;
                default: rig = default; return false;
            }
        }

        /// <summary>The scene's own key direction - the Moon's low Apollo sun, the big top's spot rig, the aurora's Moon - where it states one.</summary>
        public static bool TryGetSunDirection(SceneKind scene, out Vector3 direction)
        {
            switch (scene)
            {
                case SceneKind.Moon:
                    direction = SceneRenderer.DirectionFromElevationAzimuth(Math.Clamp(MOON.Lighting.SunElevationDegrees, 1f, 89f), MOON.Lighting.SunAzimuthDegrees);
                    return true;
                case SceneKind.Circus:
                    direction = SceneRenderer.DirectionFromElevationAzimuth(Math.Clamp(CIRCUS.Lighting.KeyElevationDegrees, 1f, 89f), CIRCUS.Lighting.KeyAzimuthDegrees);
                    return true;
                case SceneKind.Aurora:
                    direction = AuroraMoon;
                    return true;
                default:
                    direction = default;
                    return false;
            }
        }

        /// <summary>
        /// The colour behind everything in a scene that replaces the sky, linear: its void, its fog, its haze. The frame is
        /// cleared to it and the dome is not drawn; a scene under the dome answers false.
        /// </summary>
        public static bool TryGetVoid(SceneKind scene, out Vector3 linear)
        {
            switch (scene)
            {
                case SceneKind.Cavern: linear = CAVERN.Rock.FogColor.ToVector3(); return true;
                case SceneKind.Grid: linear = GRID.VoidColor.ToVector3(); return true;
                case SceneKind.Moon: linear = MOON.VoidColor.ToVector3(); return true;
                case SceneKind.Space: linear = SPACE.VoidColor.ToVector3(); return true;
                case SceneKind.Dream: linear = DREAM.DeepColor.ToVector3(); return true;
                case SceneKind.Aurora: linear = AURORA.Sky.Horizon.ToVector3(); return true;
                case SceneKind.Circus: linear = CIRCUS.Lights.HazeColor.ToVector3(); return true;
                default: linear = default; return false;
            }
        }

        private static SceneLightRig Of(Rgb skyAmbient, Rgb groundAmbient, Rgb keyTint, Rgb backTint) =>
            new(skyAmbient.ToVector3(), groundAmbient.ToVector3(), keyTint.ToVector3(), backTint.ToVector3());

        //Where the aurora's Moon stands: its key, and the disc PotatoSkyAurora draws
        private static readonly Vector3 AuroraMoon = PotatoAuroraSky.MoonDirection(AURORA);

        //AuroraBackdrop.RIG_STEPS: how finely the back light follows the curtains' breath
        private const float AURORA_RIG_STEPS = 24f;

        /// <summary>
        /// The aurora's rig as <c>AuroraBackdrop.TryGetLightRig</c> states it (#963, #1044): the Moon the key, the snow's
        /// bounce from below, the sky's ambient carried part of the way to the curtains' green, and their green from behind
        /// at 0.7 to 1.3 of the authored back light as they breathe, in steps.
        /// </summary>
        private static SceneLightRig AuroraRig(float wallClock)
        {
            AuroraLightingConfig lighting = AURORA.Lighting;
            AuroraCurtainsConfig curtains = AURORA.Curtains;
            Vector3 breaths = PotatoAuroraSky.ArcBreaths(AURORA, wallClock);
            float total = 0f, peak = 0f;
            for (int k = 0; k < Math.Min(curtains.Arcs.Length, 3); k++)
            {
                float full = curtains.Intensity * curtains.Arcs[k].Brightness;
                total += full * (k == 0 ? breaths.X : k == 1 ? breaths.Y : breaths.Z);
                peak += full;
            }
            float share = peak > 1e-4f ? MathF.Round(Math.Clamp(total / peak, 0f, 1f) * AURORA_RIG_STEPS) / AURORA_RIG_STEPS : 1f;

            //The sky's ambient towards the green at its own brightness (AuroraBackdrop.TowardsHue)
            Vector3 green = curtains.Green.ToVector3();
            Vector3 sky = lighting.SkyAmbient.ToVector3();
            sky = Vector3.Lerp(sky, green * (ColorSpace.Luminance(sky) / ColorSpace.Luminance(green)), lighting.GlowTint);

            return new SceneLightRig(sky, lighting.GroundAmbient.ToVector3(), lighting.KeyTint.ToVector3(),
                lighting.BackTint.ToVector3() * (0.7f + 0.6f * share));
        }

        /// <summary>
        /// What <c>PotatoSky.fx</c> draws on the dome for a scene that replaces the sky with a picture of its own (#829):
        /// the technique and its uniforms, the scene's config figures in the slots the shader's header names. The Moon's
        /// technique is null: its sky is the void and the stars alone (<see cref="PotatoSkyFigure"/>). Null for a scene
        /// whose void is all there is (the cavern and the big top stand inside their backdrops; the Grid keeps its black).
        /// </summary>
        public sealed record PotatoSkyLook(string Technique, Vector3 Void, Vector3 ColorA, Vector3 ColorB, Vector3 ColorC, Vector3 ColorD,
            Vector3 Axis, Vector3 Axis2, Vector4 Params, Vector4 Params2);

        private static readonly Vector3 MILKY_WAY_AXIS = Vector3.Normalize(new Vector3(0.38f, 0.82f, 0.43f));
        private static readonly Vector3 NEBULA_DIRECTION = Vector3.Normalize(new Vector3(-0.55f, 0.35f, -0.76f));

        public static PotatoSkyLook SkyLookOf(SceneKind scene)
        {
            switch (scene)
            {
                case SceneKind.Space:
                {
                    SpaceMilkyWayConfig way = SPACE.MilkyWay;
                    return new PotatoSkyLook("PotatoSkySpace", SPACE.VoidColor.ToVector3(),
                        way.Color.ToVector3() * way.Brightness, SPACE.NebulaOne.Color.ToVector3() * 0.05f, new Vector3(0.95f, 0.97f, 1.05f),
                        way.CoreColor.ToVector3() * way.Brightness * 1.6f, MILKY_WAY_AXIS, NEBULA_DIRECTION, Vector4.Zero, Vector4.Zero);
                }
                case SceneKind.Aurora:
                {
                    //The desktop's night of #963 (#1044): the gradient, the glow along the horizon under the curtains' bearing
                    //and the mist at it, and the Moon; the curtains are PotatoAuroraSky's texture
                    AuroraNightSkyConfig sky = AURORA.Sky;
                    float bearing = MathHelper.ToRadians(AURORA.Curtains.BearingDegrees);
                    Vector3 disc = PotatoAuroraSky.DiscEdges(AURORA);
                    return new PotatoSkyLook("PotatoSkyAurora", sky.Horizon.ToVector3(),
                        sky.Zenith.ToVector3(), sky.Horizon.ToVector3(), sky.HorizonGlow.ToVector3(), AURORA.Mist.Color.ToVector3(),
                        AuroraMoon, new Vector3(MathF.Sin(bearing), 0f, MathF.Cos(bearing)),
                        new Vector4(sky.GradientPower, sky.HorizonGlowWidth, sky.MoonHaloWidth, 0f),
                        new Vector4(PotatoAuroraSky.SHIMMER, disc.X, disc.Y, disc.Z));
                }
                case SceneKind.Moon:
                    return new PotatoSkyLook(null, MOON.VoidColor.ToVector3(), Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Up, Vector3.Up, Vector4.Zero, Vector4.Zero);
                case SceneKind.Dream:
                {
                    DreamPaletteConfig palette = DREAM.Palette;
                    DreamBackgroundConfig background = DREAM.Background;
                    DreamGlowsConfig glows = DREAM.Glows;
                    return new PotatoSkyLook("PotatoSkyDream", DREAM.DeepColor.ToVector3(),
                        palette.A.ToVector3(), palette.B.ToVector3(), palette.C.ToVector3(), palette.D.ToVector3(), Vector3.Up, Vector3.Up,
                        new Vector4(background.SwirlScale, background.SwirlWarp, background.SpeedSlow, background.SpeedFast),
                        //Under the desktop's brightness: its marble sits under a bloom and a resolve this path has not, and at the
                        //config's figure the sky was a pastel sheet
                        new Vector4(background.RibbonSharpness, background.Brightness * 0.6f, glows.OrbBrightness * 0.5f, 40f));
                }
                default:
                    return null;
            }
        }
    }
}
