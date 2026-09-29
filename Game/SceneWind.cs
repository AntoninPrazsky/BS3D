using Microsoft.Xna.Framework;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;

namespace BS3D
{
    /// <summary>
    /// <b>How hard the air pushes on the hanging cluster in each scene</b> (#95): the physics half of the wind the shaders
    /// already draw. A scene's grass, snow and spray are combed along its own <c>Wind</c> heading; this gives the
    /// balls the same heading and a strength, so the cluster sways the way the world around it says the air is
    /// moving — and hangs still in the scenes with no air.
    /// <para>
    /// <b>The strengths are one column of numbers and a master scale, and they are judgements, not measurements.</b> They
    /// are accelerations at the top of a gust (see <see cref="WindField"/>) in units per second squared, gravity being
    /// 9.8, and they are <b>small on purpose</b>: how far a cluster sways depends far more on the level than on the wind —
    /// at the mountains' 0.16 the most compliant level shipped (Helix) sways its lowest balls about a unit, a tall stiff
    /// one (Belfry, Pagoda) half of that, the 975-ball Spyglass a tenth, and a flat picture (Pennant, Heart) not at all —
    /// and at three times that the compliant ones swung five units and resonated with the gust. The mountains and the
    /// storm are the windy ones (the mountains' snow is combed at four times the speed of the meadow's grass); the
    /// desert, the sea and the ice have steady air; the ground scenes and the beach a light one; and the scenes with no
    /// air at all (space, the Moon, Mars's thin dust, the cavern, the dream, the Grid) none. The owner reads the look;
    /// <see cref="MASTER_SCALE"/> moves every scene at once, one row moves one, and <c>wind=&lt;scale&gt;</c> on the command
    /// line multiplies the lot for a look or a measurement.
    /// </para>
    /// </summary>
    internal static class SceneWind
    {
        /// <summary>
        /// Multiplies every scene's strength: the one dial for "the sway is too much" or "too little" before a scene's
        /// own row is touched. 1 is the table as written.
        /// </summary>
        internal const float MASTER_SCALE = 1f;

        /// <summary>
        /// The ground heading used where a scene's config carries none (the aurora's forest, the cities, the volcano's
        /// fountains' own air): the mean of the headings the scenes that do have one share, all within twenty degrees of
        /// each other — mostly across the screen, so the sway reads from the play camera as a lean to one side and back.
        /// </summary>
        private static readonly Vector2 SHARED_HEADING = new(0.87f, 0.5f);

        /// <summary>The acceleration at the top of a gust in <paramref name="scene"/>, before <see cref="MASTER_SCALE"/>.</summary>
        internal static float Strength(SceneKind scene) => scene switch
        {
            SceneKind.Mountain => 0.16f,
            SceneKind.Storm => 0.18f,
            SceneKind.Polar => 0.11f,
            SceneKind.Sea => 0.10f,
            SceneKind.Volcano => 0.09f,
            SceneKind.Desert => 0.08f,
            SceneKind.Outback => 0.08f,
            SceneKind.Tropical => 0.07f,
            SceneKind.Savanna => 0.06f,
            SceneKind.Meadow => 0.06f,
            SceneKind.City => 0.05f,
            SceneKind.NeonCity => 0.05f,
            SceneKind.Forest => 0.04f,
            SceneKind.Aurora => 0.035f,
            _ => 0f, //space, the Moon, Mars, the cavern, the dream and the Grid: no air to move
        };

        /// <summary>
        /// The wind for <paramref name="scene"/> with the renderer's own heading for it, or <see cref="WindField.None"/>
        /// for a scene without air. Built once when a level is installed and read once a physics step.
        /// </summary>
        /// <param name="scene">The scene the level plays in.</param>
        /// <param name="renderer">The live renderer, for the scene's own heading; null falls back to the shared one.</param>
        /// <param name="scale">The <c>wind=</c> testing argument's multiplier, 1 for the table as shipped.</param>
        internal static WindField For(SceneKind scene, SceneRenderer renderer, float scale = 1f)
        {
            float strength = Strength(scene) * MASTER_SCALE * scale;
            if (strength <= 0f) return WindField.None;

            Vector2 heading = Heading(scene, renderer) ?? SHARED_HEADING;
            return new WindField(new System.Numerics.Vector2(heading.X, heading.Y), strength);
        }

        /// <summary>The heading the scene's own config combs its grass, snow or swell along, or null when it has none.</summary>
        private static Vector2? Heading(SceneKind scene, SceneRenderer renderer)
        {
            Vec2? wind = renderer?.GetSceneConfig(scene) switch
            {
                DesertSceneConfig c => c.Wind,
                ForestSceneConfig c => c.Wind,
                MeadowSceneConfig c => c.Wind,
                SavannaSceneConfig c => c.Wind,
                SeaSceneConfig c => c.Wind,
                PolarSceneConfig c => c.Wind,
                MountainSceneConfig c => c.Snow.Wind,
                OutbackSceneConfig c => c.Air.Wind,
                StormSceneConfig c => c.Air.Wind,
                TropicalSceneConfig c => c.Terrain.Wind,
                VolcanoSceneConfig c => c.Wind,
                _ => null,
            };

            return wind?.ToVector2();
        }
    }
}
