using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The tropical beach's own shots for a chapter intro's prologue (#559): the island from out over the
    /// lagoon, a walk along the waterline under the palms, and a crane up one palm to its crown — cut together,
    /// then cut to the tour's last leg. The spline it replaces stood over the palm tops looking out at the far
    /// shore (#555 raised it there once the palms grew to 22 units) and never went down onto the sand.
    /// <para>
    /// <b>Built off the grove on screen</b> (<see cref="SceneRenderer.TropicalPalms"/> and
    /// <see cref="SceneRenderer.TropicalRocks"/>, rolled per launch and pinned by <c>sceneseed=</c>). A palm is
    /// taken at its own figure — the trunk as a capsule from the root to the crown its bow carries off the
    /// axis, and the crown as a sphere of the fronds' reach — so a path held clear of both is clear of the
    /// palm. Heights come off <see cref="TerrainMirror.Tropical"/>, the mirror the palms stand
    /// on, and never under the lagoon's surface plus its swell. Built once when the intro begins.
    /// </para>
    /// </summary>
    internal static class TropicalIntroShots
    {
        //The lagoon: the establishing dolly in over the water towards the island, over its highest ground
        //(which out here is the water's surface).
        private const float LAGOON_FROM = 205f;
        private const float LAGOON_TO = 170f;
        private const float LAGOON_ABOVE_FROM = 30f;
        private const float LAGOON_ABOVE_TO = 24f;
        private const float LAGOON_SECONDS = 3.4f;

        //How far over the lagoon's rest level the swell and the chop can lift the water.
        private const float SWELL = 1f;

        //The beach: a walk this long along the sand this high over it, on the line where the beach stands at
        //BEACH_LINE over the water — the dry sand's edge, where the palms begin (they stand on sand 1.1 over
        //the water and up; the whole beach is only two units over it, so a line any higher is never found).
        //The waterline's rocks are held off by the clearance test like everything else. Looking along the
        //walk, a little up, so the crowns lean in over the top of the frame with the lagoon on the other hand.
        private const float BEACH_WALK = 46f;
        private const float BEACH_LINE = 1.1f;
        private const float BEACH_ABOVE = 2.4f;
        private const float BEACH_PITCH_UP_DEGREES = 5f;
        private const float BEACH_SECONDS = 3.2f;

        //The palm: a crane from the sand to a little over one tall palm's crown, this much air off the fronds'
        //reach, the lens on the crown — up from under the fronds against the sky to level with them.
        private const float PALM_AIR = 5f;
        private const float PALM_ABOVE_FROM = 1.8f;
        private const float PALM_OVER_CROWN = 2f;
        private const float PALM_SECONDS = 3.2f;

        //Off every trunk and crown by this much: one, and a walk along the beach grazed a trunk that filled a
        //fifth of the frame.
        private const float MARGIN = 1.6f;
        private const float ABOVE_GROUND = 1.2f;
        private const int TRIES = 24;

        /// <summary>The prologue for the beach on screen, or null when there is no grove.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Tropical) is not TropicalSceneConfig tropical) return null;

            IReadOnlyList<PlantFigure> palms = scenes.TropicalPalms;
            if (palms == null || palms.Count == 0) return null;

            float water = tropical.Water.LevelY + SWELL;
            var ground = new IntroGround((x, z) => MathF.Max(TerrainMirror.Tropical(x, z, tropical), water));
            foreach (PlantFigure palm in palms) ground.Add(palm);
            foreach (PlantFigure rock in scenes.TropicalRocks) ground.Add(rock);

            IntroShot lagoon = ground.Establishing("the lagoon", LAGOON_FROM, LAGOON_TO, LAGOON_ABOVE_FROM, LAGOON_ABOVE_TO,
                LAGOON_SECONDS, fieldOfView * 1.1f, random, TRIES, MARGIN);

            return IntroGround.Cut(lagoon, Beach(ground, tropical, palms, fieldOfView, random), Palm(ground, palms, fieldOfView, random));
        }

        /// <summary>Along the sand under the palms, the lagoon on the other hand.</summary>
        private static IntroShot Beach(IntroGround ground, TropicalSceneConfig tropical, IReadOnlyList<PlantFigure> palms, float fieldOfView, Random random)
        {
            float line = tropical.Water.LevelY + BEACH_LINE;
            Vector3[] best = null;
            int bestPalms = -1;

            for (int attempt = 0; attempt < TRIES * 2; attempt++)
            {
                float from = (float)random.NextDouble() * MathHelper.TwoPi;
                float sign = random.Next(2) == 0 ? 1f : -1f;

                //Walk the bearing, putting each point where the sand stands at the line: found outward from the
                //palms' own ring, the ground falling towards the water as the radius grows.
                var path = new Vector3[IntroGround.PATH_POINTS];
                float span = BEACH_WALK / tropical.Terrain.ShoreRadius;
                bool found = true;
                for (int i = 0; i < path.Length && found; i++)
                {
                    float bearing = from + sign * span * i / (path.Length - 1);
                    found = Shoreline(ground, bearing, line, tropical.Terrain.ShoreRadius, out Vector2 plan);
                    path[i] = new Vector3(plan.X, ground.Height(plan.X, plan.Y) + BEACH_ABOVE, plan.Y);
                }
                if (!found || !ground.Clear(path, MARGIN, ABOVE_GROUND)) continue;

                //The walk with the most palms beside it — under the grove, not along a bare stretch of sand.
                int beside = 0;
                foreach (PlantFigure palm in palms)
                {
                    Vector3 middle = path[path.Length / 2];
                    if (Vector2.Distance(new Vector2(palm.Root.X, palm.Root.Z), new Vector2(middle.X, middle.Z)) < BEACH_WALK * 0.6f) beside++;
                }
                if (beside <= bestPalms) continue;

                best = path;
                bestPalms = beside;
            }

            return best == null ? null
                : new IntroShot("the beach", best, BEACH_SECONDS, fieldOfView * 1.15f, lookAhead: 16f, pitchDownDegrees: -BEACH_PITCH_UP_DEGREES);
        }

        /// <summary>A crane up one of the tallest palms to its crown.</summary>
        private static IntroShot Palm(IntroGround ground, IReadOnlyList<PlantFigure> palms, float fieldOfView, Random random)
        {
            var order = new List<int>(palms.Count);
            for (int i = 0; i < palms.Count; i++) order.Add(i);
            order.Sort((a, b) => (palms[b].Crown.Y - palms[b].Root.Y).CompareTo(palms[a].Crown.Y - palms[a].Root.Y));

            for (int n = 0; n < Math.Min(order.Count, 20); n++)
            {
                PlantFigure palm = palms[order[n]];
                Vector2 crown = new(palm.Crown.X, palm.Crown.Z);
                float distance = palm.Reach + PALM_AIR;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    //Mostly from the sea's side, so the palm stands against the grove and the island; any side
                    //the grove leaves room on otherwise.
                    Vector2 away = IntroGround.Heading(attempt < 4
                        ? IntroGround.Bearing(crown) + MathHelper.ToRadians(-50f + 100f * (float)random.NextDouble())
                        : (float)random.NextDouble() * MathHelper.TwoPi);
                    Vector2 plan = crown + away * distance;
                    Vector2 back = plan + away * 3f;

                    float floor = ground.Height(plan.X, plan.Y);
                    Vector3[] path = ground.Hug(plan, back, PALM_ABOVE_FROM, palm.Crown.Y + PALM_OVER_CROWN - floor, IntroGround.PATH_POINTS);
                    if (!ground.Clear(path, MARGIN, ABOVE_GROUND)) continue;

                    return new IntroShot("the palm", path, PALM_SECONDS, fieldOfView * 1.15f, lookAt: palm.Crown);
                }
            }

            return null;
        }

        //Where along a bearing the sand stands at the line, searched outward from inside the grove's ring to
        //well past the shore; false when the ground never falls to it (a bearing through the far channel).
        private static bool Shoreline(IntroGround ground, float bearing, float line, float shore, out Vector2 plan)
        {
            Vector2 heading = IntroGround.Heading(bearing);
            float inner = shore * 0.6f, outer = shore * 1.4f;

            if (ground.Height(heading.X * inner, heading.Y * inner) < line)
            {
                plan = heading * inner;
                return false;
            }

            for (int step = 0; step < 24; step++)
            {
                float middle = 0.5f * (inner + outer);
                if (ground.Height(heading.X * middle, heading.Y * middle) > line) inner = middle;
                else outer = middle;
            }

            plan = heading * inner;
            return true;
        }
    }
}
