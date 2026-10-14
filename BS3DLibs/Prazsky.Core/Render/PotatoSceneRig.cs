using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What the scenes that replace the sky state for themselves, answered without a <see cref="SceneRenderer"/> (#829):
    /// the Potato path builds none, so <see cref="SkyLightRig"/> had only the dome to light by, and the Moon, the cavern
    /// and the Grid stood in the dome's lilac where the desktop lights them with the scene's own rig
    /// (<c>Backdrop.TryGetLightRig</c>) under a void. Each answer here is the figure the scene's own backdrop gives
    /// from its config's defaults - the aurora's without its hue drift, which rides the curtains this path does not
    /// draw - and the colour its void or its fog is, for the frame to be cleared to in place of the dome.
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
                case SceneKind.Aurora: rig = Of(AURORA.Lighting.SkyAmbient, AURORA.Lighting.GroundAmbient, AURORA.Lighting.KeyTint, AURORA.Lighting.BackTint); return true;
                case SceneKind.Circus: rig = Of(CIRCUS.Lighting.SkyAmbient, CIRCUS.Lighting.GroundAmbient, CIRCUS.Lighting.KeyTint, CIRCUS.Lighting.BackTint); return true;
                default: rig = default; return false;
            }
        }

        /// <summary>The scene's own key direction - the Moon's low Apollo sun, the big top's spot rig - where it states one.</summary>
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
                case SceneKind.Aurora: linear = AURORA.VoidColor.ToVector3(); return true;
                case SceneKind.Circus: linear = CIRCUS.Lights.HazeColor.ToVector3(); return true;
                default: linear = default; return false;
            }
        }

        private static SceneLightRig Of(Rgb skyAmbient, Rgb groundAmbient, Rgb keyTint, Rgb backTint) =>
            new(skyAmbient.ToVector3(), groundAmbient.ToVector3(), keyTint.ToVector3(), backTint.ToVector3());
    }
}
