using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The savanna's own shots for a chapter intro's prologue (#559): the plain from above with the arena and
    /// its ring of campfires in it, a slow walk round a baobab, and a track past an acacia in its grove — cut
    /// together, then cut to the tour's last leg. The spline it replaces circled the arena at stand-off
    /// height and turned to the campfire and back, which the owner read as neck-breaking turns; the plain's
    /// own things — #451's baobabs and acacias — stand a hundred units and more out, where it never went.
    /// <para>
    /// <b>Built off the planting on screen</b> (<see cref="SceneRenderer.SavannaPlanting"/>, rolled per launch
    /// and pinned by <c>sceneseed=</c>): the subjects are its own <see cref="SavannaScatter.Baobabs"/> and
    /// <see cref="SavannaScatter.Acacias"/>, and every path is held against everything it planted
    /// (<see cref="SavannaScatter.Solids"/>) before it is taken. Heights come off <see cref="SceneRenderer.SavannaGroundHeight"/>, the mirror the
    /// plants stand on. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class SavannaIntroShots
    {
        //The plain: the establishing dolly in from the rise towards the arena, over the highest ground.
        private const float PLAIN_FROM = 215f;
        private const float PLAIN_TO = 175f;
        private const float PLAIN_ABOVE_FROM = 34f;
        private const float PLAIN_ABOVE_TO = 27f;
        private const float PLAIN_SECONDS = 3.4f;

        //The baobab: an arc of this many radians round the tree at its reach plus this much air, a man's
        //height rising a little, the lens on the upper trunk where the branches spread.
        private const float BAOBAB_AIR = 11f;
        private const float BAOBAB_ARC_RADIANS = 0.9f;
        private const float BAOBAB_ABOVE_FROM = 2.4f;
        private const float BAOBAB_ABOVE_TO = 4.2f;
        private const float BAOBAB_SECONDS = 3.2f;

        //The acacia: a lateral track this long, this much air off the crown's edge, low so the umbrella
        //crown stands against the sky, the lens held on the crown — the grove behind slides past it.
        private const float ACACIA_AIR = 9f;
        private const float ACACIA_TRACK = 22f;
        private const float ACACIA_ABOVE = 2.2f;
        private const float ACACIA_SECONDS = 3.2f;
        private const float GROVE_REACH = 28f;

        //Every path keeps this much air off every solid, and off the ground.
        private const float MARGIN = 1.2f;
        private const float ABOVE_GROUND = 1.5f;

        private const int TRIES = 24;

        /// <summary>The prologue for the savanna on screen, or null when its planting is not built.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            SavannaScatter planting = scenes?.SavannaPlanting;
            if (planting == null) return null;

            //Every instance of every mesh planted, as the sphere round its mesh — NOT the spacing footprints,
            //which a bush's crown overhangs: held clear of those, the first cut's walk round the baobab
            //photographed as a frame of foliage at arm's length.
            var ground = new IntroGround(scenes.SavannaGroundHeight);
            foreach (PlantFigure solid in planting.Solids) ground.Add(solid);

            //The plain: the bearing that puts one of the fires nearest the middle of the frame, a little to one
            //side of the line to the arena — the ring of fires is the one thing here that is also a light.
            int fires = scenes.SavannaCampfireCount;
            IntroShot plain = ground.Establishing("the plain", PLAIN_FROM, PLAIN_TO, PLAIN_ABOVE_FROM, PLAIN_ABOVE_TO,
                PLAIN_SECONDS, fieldOfView * 1.1f, random, TRIES, MARGIN,
                score: bearing =>
                {
                    float nearest = MathF.PI;
                    for (int fire = 0; fire < fires; fire++)
                    {
                        Vector3 at = scenes.SavannaCampfirePosition(fire);
                        float off = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(at.Z, at.X) - bearing - 0.2f));
                        nearest = MathF.Min(nearest, off);
                    }
                    return -nearest;
                });

            return IntroGround.Cut(plain, Baobab(ground, planting.Baobabs, fieldOfView, random),
                Acacia(ground, planting.Acacias, fieldOfView, random));
        }

        /// <summary>A slow walk round a baobab, the lens on its crown.</summary>
        private static IntroShot Baobab(IntroGround ground, IReadOnlyList<PlantFigure> baobabs, float fieldOfView, Random random)
        {
            if (baobabs.Count == 0) return null;

            int first = random.Next(baobabs.Count);
            for (int n = 0; n < baobabs.Count; n++)
            {
                PlantFigure tree = baobabs[(first + n) % baobabs.Count];
                Vector2 centre = new(tree.Crown.X, tree.Crown.Z);
                float radius = tree.Reach + BAOBAB_AIR;
                Vector3 look = Vector3.Lerp(tree.Root, tree.Crown, 0.85f);

                for (int attempt = 0; attempt < TRIES; attempt++)
                {
                    float from = (float)random.NextDouble() * MathHelper.TwoPi;
                    float sign = random.Next(2) == 0 ? 1f : -1f;
                    Vector3[] path = ground.Arc(centre, radius, from, from + sign * BAOBAB_ARC_RADIANS,
                        BAOBAB_ABOVE_FROM, BAOBAB_ABOVE_TO, IntroGround.PATH_POINTS);

                    //The tree itself is the subject and is kept out by its own figure; the rest by the margin.
                    if (!ground.Clear(path, MARGIN, ABOVE_GROUND)) continue;

                    return new IntroShot("the baobab", path, BAOBAB_SECONDS, fieldOfView * 1.15f, lookAt: look);
                }
            }

            return null;
        }

        /// <summary>A lateral track past an acacia in the thickest grove, the lens on its crown.</summary>
        private static IntroShot Acacia(IntroGround ground, IReadOnlyList<PlantFigure> acacias, float fieldOfView, Random random)
        {
            if (acacias.Count == 0) return null;

            //The acacias by how many others stand within a grove's reach of them, thickest first — a lone tree
            //is a tree, one in its grove is the savanna.
            var order = new List<(int Index, int Grove)>(acacias.Count);
            for (int i = 0; i < acacias.Count; i++)
            {
                int grove = 0;
                for (int j = 0; j < acacias.Count; j++)
                    if (j != i && Vector3.Distance(acacias[i].Root, acacias[j].Root) < GROVE_REACH) grove++;
                order.Add((i, grove));
            }
            order.Sort((a, b) => b.Grove.CompareTo(a.Grove));

            for (int n = 0; n < Math.Min(order.Count, 12); n++)
            {
                PlantFigure tree = acacias[order[n].Index];
                Vector2 root = new(tree.Crown.X, tree.Crown.Z);
                float distance = tree.Reach + ACACIA_AIR;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    Vector2 away = IntroGround.Heading((float)random.NextDouble() * MathHelper.TwoPi);
                    Vector2 across = new(-away.Y, away.X);
                    Vector2 middle = root + away * distance;
                    Vector3[] path = ground.Hug(middle - across * (ACACIA_TRACK * 0.5f), middle + across * (ACACIA_TRACK * 0.5f),
                        ACACIA_ABOVE, ACACIA_ABOVE, IntroGround.PATH_POINTS);

                    if (!ground.Clear(path, MARGIN, ABOVE_GROUND)) continue;

                    return new IntroShot("the acacia", path, ACACIA_SECONDS, fieldOfView * 1.1f, lookAt: tree.Crown);
                }
            }

            return null;
        }
    }
}
