using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// What a chapter-intro prologue may be built from (#596): the live scene and the things the game planted or
    /// placed for it. The builders take different parts of it — the cities their city, the wood its planting, the
    /// sea the dome's sun, the dream and the storm the wall clock their moving subjects will be at — so it is
    /// handed over whole rather than as one argument list per scene.
    /// </summary>
    /// <param name="Renderer">The live scene renderer; null before there is one.</param>
    /// <param name="City">The city being drawn, the day one's or the neon one's.</param>
    /// <param name="ForestScatter">The forest's planting.</param>
    /// <param name="AuroraScatter">The aurora's wood.</param>
    /// <param name="SunDirection">The dome's sun, when there is a rig.</param>
    /// <param name="WallClock">The game's wall clock now, which the moving subjects are laid out on.</param>
    internal sealed record IntroContext(SceneRenderer Renderer, City City, ForestScatterRenderer ForestScatter,
        ForestScatterRenderer AuroraScatter, Vector3? SunDirection, float WallClock);

    /// <summary>
    /// <b>The live scene's own shots to cut together ahead of its chapter tour</b>, or null for a scene with none —
    /// the dispatch over the scenes' builders, out of the host's menu partial where it stood until #596.
    /// <para>
    /// The two cities have them: their street level, plazas and canyons are where no spline round the arena can go,
    /// and they are built off the city this game is drawing right now — so the shots stand in the very streets on
    /// screen (#488). And the volcano (#530): the tour looks at the cone from outside and never over its rim, and
    /// the crater is the one picture every reference of #509 is built round. And the aurora (#531): its wood
    /// stands outside the clearing where the tour's spline never goes, and the shots thread the very spruces this
    /// game planted, off the planting itself. And since #559 the open-ground scenes, each off its own land — the
    /// meadow, the savanna, the forest and the tropical beach through <see cref="IntroGround"/>, off the land's
    /// height mirror and the scene's own planting; four scenes whose things a shader builds: the dream's glass and
    /// orbs, the cavern's crystals and god rays, the storm's cells and its strike, the icesheet's crevasses and
    /// front, found through the renderer's host copies of the shaders' own placement (the dream's and the storm's
    /// subjects move, so theirs are laid out on the wall clock they will play in) — and the four scenes off the
    /// Earth, the Moon, Mars, space and the Grid, each shot about one thing the scene builds: a real crater, a real
    /// mesa, the planet and the drain, one cube and the landmark ring.
    /// </para>
    /// <para>
    /// A declarative shot list was considered and is the wrong move (#596's own verdict): each builder
    /// <i>searches</i> its scene's procedural placement with reject-and-retry loops, which is logic rather than
    /// data. What is shared is the small geometry in <see cref="IntroPaths"/>.
    /// </para>
    /// </summary>
    internal static class IntroPrologues
    {
        /// <param name="scene">The scene the tour is flying.</param>
        /// <param name="context">What the builders may be built from.</param>
        /// <param name="fieldOfView">The frame the tour ends on, which each shot widens from.</param>
        /// <param name="random">The intro's own roll.</param>
        internal static IntroShot[] For(SceneKind scene, IntroContext context, float fieldOfView, Random random)
        {
            SceneRenderer renderer = context.Renderer;

            return scene switch
            {
                SceneKind.City or SceneKind.NeonCity => CityIntroShots.Build(context.City, fieldOfView, random),
                SceneKind.Volcano => VolcanoIntroShots.Build(renderer, fieldOfView, random),
                SceneKind.Aurora => AuroraIntroShots.Build(context.AuroraScatter,
                    renderer?.GetSceneConfig(SceneKind.Aurora) as AuroraSceneConfig, fieldOfView, random),
                //#559: the sea, the desert, the outback and the mountains, off their own terrain (TerrainMirror)
                //and, for the sea, the dome's sun, which its swell shot heads into.
                SceneKind.Sea => SeaIntroShots.Build(renderer, context.SunDirection, fieldOfView, random),
                SceneKind.Desert => DesertIntroShots.Build(renderer, fieldOfView, random),
                SceneKind.Outback => OutbackIntroShots.Build(renderer, fieldOfView, random),
                SceneKind.Mountain => MountainIntroShots.Build(renderer, fieldOfView, random),
                SceneKind.Meadow => MeadowIntroShots.Build(
                    renderer?.GetSceneConfig(SceneKind.Meadow) as MeadowSceneConfig, renderer?.MeadowTrees, fieldOfView, random),
                SceneKind.Savanna => SavannaIntroShots.Build(renderer, fieldOfView, random),
                SceneKind.Forest => ForestIntroShots.Build(context.ForestScatter,
                    renderer?.GetSceneConfig(SceneKind.Forest) as ForestSceneConfig, fieldOfView, random),
                SceneKind.Tropical => TropicalIntroShots.Build(renderer, fieldOfView, random),
                SceneKind.Dream => DreamIntroShots.Build(renderer, context.WallClock, fieldOfView, random),
                SceneKind.Cavern => CavernIntroShots.Build(renderer, fieldOfView, random),
                SceneKind.Storm => StormIntroShots.Build(renderer, context.WallClock, fieldOfView, random),
                SceneKind.Polar => PolarIntroShots.Build(renderer, fieldOfView, random),
                //#559: the four off the Earth, on absolute lenses (see GridIntroShots) — the Moon's and Mars's
                //ground through OffworldGround's ceilings, the Grid's solids through the renderer's own record.
                SceneKind.Moon => MoonIntroShots.Build(renderer, random),
                SceneKind.Mars => MarsIntroShots.Build(renderer, random),
                SceneKind.Space => SpaceIntroShots.Build(renderer, random),
                SceneKind.Grid => GridIntroShots.Build(renderer, random),
                _ => null,
            };
        }
    }
}
