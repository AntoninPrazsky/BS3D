using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The meadow's own shots for a chapter intro's prologue (#559, #609): the valley from a hilltop, then down the
    /// brook at the height of its reeds to the pond it runs into, then along the footpath beside its fence to the old
    /// oak on its knoll — cut together, then cut to the tour's last leg. The meadow opens the whole campaign, so this
    /// is the first thing the game shows a new player after its menu.
    /// <para>
    /// <b>Two of the three are about things since #609.</b> Until then the meadow had nothing standing on it and its
    /// shots were about pieces of the land — the flowers at a hand's height and the climb of a slope; the rule the
    /// prologues are held to (#559) is "each shot about one object", and the owner praised the intros that follow
    /// concrete things. The brook and the path are analytic lines on the field (<see cref="MeadowPath"/>), the very
    /// ones <c>Meadow.fx</c> draws, so a shot can follow one without asking the planting; and the planting keeps off
    /// both, so a lens on either centreline has nothing to run into. Every height is read off
    /// <see cref="TerrainMirror.Meadow"/>. Built once when the intro begins.
    /// </para>
    /// <para>
    /// <b>Both walk TOWARDS what their line arrives at since #609's third round</b>, and keep the lens on it: the owner
    /// found the brook and the path stopping short in the grass, and they arrive at a pond and at an old tree now
    /// (<see cref="MeadowPath.PondCentre"/>, <see cref="MeadowPath.PathEnd"/>). Until then both shots ran outwards,
    /// away from the arena, which was also away from anything; walked the other way the brook is followed downstream
    /// and the path is walked the way a walker walks it, to the tree.
    /// </para>
    /// </summary>
    internal static class MeadowIntroShots
    {
        //The valley: a level dolly in from the hills towards the arena, this high over the highest ground
        //under it, on the hilliest of a few rolled bearings — a view from a hilltop down into the basin.
        private const float VALLEY_FROM = 195f;
        private const float VALLEY_TO = 160f;
        private const float VALLEY_ABOVE_FROM = 26f;
        private const float VALLEY_ABOVE_TO = 18f;
        private const float VALLEY_SECONDS = 3.4f;

        //The brook: down its centreline a little over the water, the reeds and the stones passing either side, the lens
        //on the pond ahead. A stretch of it that ends short of the pond's bank, where the brook's own banks are planted.
        private const float BROOK_FROM = 22f;              //past the brook's inner end, where the stretch finishes, clear of the pond's reeds
        private const float BROOK_RUN = 32f;
        private const float BROOK_ABOVE = 1.8f;             //at 1.1 the bank stones and the reeds filled the corners of the frame
        private const float BROOK_SECONDS = 3.2f;

        //The path: along its centreline at a walker's height, the fence beside it, the lens on the old tree it leads to,
        //stopping short of its crown
        private const float PATH_FROM = 22f;               //past the path's end, where the walk finishes
        private const float PATH_RUN = 40f;
        private const float PATH_ABOVE = 1.7f;
        private const float PATH_TREE_LOOK_UP = 7f;         //the point on the tree the lens holds: the trunk under the crown
        private const float PATH_SECONDS = 3.2f;

        private const int TRIES = 24;

        /// <summary>The prologue for the meadow, or null when there is no meadow config.</summary>
        public static IntroShot[] Build(MeadowSceneConfig meadow, float fieldOfView, Random random)
        {
            if (meadow == null) return null;

            var ground = new IntroGround((x, z) => TerrainMirror.Meadow(x, z, meadow));

            //The hilliest bearing: the ground at the run's start, where the lens stands on the hill.
            IntroShot valley = ground.Establishing("the valley", VALLEY_FROM, VALLEY_TO, VALLEY_ABOVE_FROM, VALLEY_ABOVE_TO,
                VALLEY_SECONDS, fieldOfView * 1.1f, random, TRIES, margin: 2f,
                score: bearing => ground.Height(MathF.Cos(bearing) * VALLEY_FROM, MathF.Sin(bearing) * VALLEY_FROM));

            return IntroGround.Cut(valley, Brook(meadow, fieldOfView), Path(meadow, fieldOfView));
        }

        /// <summary>Down the brook a little over the water, the reeds passing either side, to the pond it runs into.</summary>
        private static IntroShot Brook(MeadowSceneConfig meadow, float fieldOfView)
        {
            float end = meadow.ClearingRadius * MeadowPath.BROOK_START + BROOK_FROM;
            var path = new Vector3[IntroPaths.FINE_POINTS];
            for (int i = 0; i < path.Length; i++)
            {
                float d = end + BROOK_RUN * (1f - i / (path.Length - 1f));
                (float x, float z) = MeadowPath.BrookPoint(d, 0f, meadow);
                path[i] = new Vector3(x, TerrainMirror.Meadow(x, z, meadow) + BROOK_ABOVE, z);
            }

            (float pondX, float pondZ) = MeadowPath.PondCentre(meadow);
            return new IntroShot("the brook", path, BROOK_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(pondX, TerrainMirror.Meadow(pondX, pondZ, meadow), pondZ));
        }

        /// <summary>Along the footpath at a walker's height, the fence beside it, to the old tree it leads to.</summary>
        private static IntroShot Path(MeadowSceneConfig meadow, float fieldOfView)
        {
            float end = meadow.ClearingRadius * MeadowPath.START + PATH_FROM;
            var path = new Vector3[IntroPaths.FINE_POINTS];
            for (int i = 0; i < path.Length; i++)
            {
                float d = end + PATH_RUN * (1f - i / (path.Length - 1f));
                float angle = meadow.PathBearing + MeadowPath.Wander(d, meadow) / d;
                float x = MathF.Cos(angle) * d, z = MathF.Sin(angle) * d;
                path[i] = new Vector3(x, TerrainMirror.Meadow(x, z, meadow) + PATH_ABOVE, z);
            }

            (float treeX, float treeZ) = MeadowPath.PathEnd(meadow);
            return new IntroShot("the path", path, PATH_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(treeX, TerrainMirror.Meadow(treeX, treeZ, meadow) + PATH_TREE_LOOK_UP, treeZ));
        }
    }
}
