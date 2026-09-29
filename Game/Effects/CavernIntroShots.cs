using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The cavern's own shots for a chapter intro's prologue (#559): the whole chamber from high on its wall,
    /// a glide low over the river onto a crystal cluster, the island from underneath and a skim over the rippling
    /// water — cut together, then cut to the tour's last leg. The owner's verdict on the scenes without a prologue was
    /// "neck-breaking turns instead of cuts, and the camera should always attend to one concrete thing": the
    /// tour is one spline round the arena, and every one of these things stands a hundred units and more out
    /// of it, on the wall or under the ceiling, where the spline never goes.
    /// <para>
    /// <b>There was a fourth, up a god ray into the glowworms on the vault (#656), and it is gone:</b> "we probably won't
    /// want the view of the ceiling — the ceiling and its edges don't look very good" (that is #528's, the vault
    /// reading as a perfect oval). In its place the island from underneath, as the Space prologue has one, and a low
    /// pass over the rippling water; the crystals' glide stays.
    /// </para>
    /// <para>
    /// <b>Everything is placed where <c>Cavern.fx</c> places it</b> — the clusters through
    /// <see cref="SceneRenderer.CavernCrystalCenter"/>, the shader's own arithmetic on the host — and the shell is
    /// a cylinder, a ceiling plane and a river plane with nothing between them but the island, so a lens kept
    /// inside those planes, off the wall and well clear of the island's axis cannot be inside anything. Built
    /// once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class CavernIntroShots
    {
        //The chamber: a slow arc this far round the axis, at this fraction of the cave's radius and this high
        //over the river, the lens on a point across the cave low over the water — the river's glow, the far
        //wall's crystals and the shafts all in one frame, and the island small in the middle of it.
        private const float CHAMBER_RADIUS_FRACTION = 0.70f;
        private const float CHAMBER_HEIGHT = 84f;
        private const float CHAMBER_DROP = 10f;
        private const float CHAMBER_ARC_DEGREES = 18f;
        private const float CHAMBER_SECONDS = 3.2f;

        //The crystals: a glide this high over the river towards one of the low clusters, from this far out of
        //it to this far, off to one side of the line straight in by this angle so the cluster turns as it nears.
        //⚠ Only the LOW clusters (their centre within this of the water): a high one seen from over the river
        //is a lit patch on a wall looked up at, and the river's glint under it — half the picture — is gone.
        private const float CRYSTAL_GLIDE_HEIGHT = 4.5f;
        private const float CRYSTAL_FROM = 95f;
        private const float CRYSTAL_TO = 44f;
        private const float CRYSTAL_SIDE_DEGREES = 22f;
        private const float CRYSTAL_LOW = 20f;
        private const float CRYSTAL_SECONDS = 3.2f;

        //The underside (#656): a crane up under the island's funnel, the lens this far out from the axis and this far over
        //the water (the funnel's tip hangs only 6.5 units over the river, so the lens is a few units up and never
        //under it), looking at a point on the funnel a little under the rim — the glass cone, the drum's underside
        //and the river's glow on it, which is what the scene's light being below is for.
        private const float UNDER_OUT_FROM = 38f, UNDER_OUT_TO = 31f;
        private const float UNDER_FROM_ABOVE_WATER = 2.6f, UNDER_TO_ABOVE_WATER = 9f;
        private const float UNDER_LOOK_Y = -19f;
        private const float UNDER_FOV_DEGREES = 62f;
        private const float UNDER_SECONDS = 3.0f;

        //The river: a straight run this high over the water, this far from the axis at its closest (clear of the
        //island and of every wall), this long, looking along the run and a little down — the ripples and the glow
        //they carry fill the frame, and there is nothing else in it to look at. It is the lowest lens of the set.
        private const float RIVER_HEIGHT = 1.9f;
        private const float RIVER_RADIUS_FRACTION = 0.34f;
        private const float RIVER_RUN = 70f;
        private const float RIVER_PITCH_DOWN_DEGREES = 5f;
        private const float RIVER_SECONDS = 2.8f;

        private const int PATH_POINTS = IntroPaths.FINE_POINTS;

        /// <summary>
        /// The prologue for the cavern being drawn, or null when the renderer has no cavern config.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Cavern) is not CavernSceneConfig cavern) return null;

            return new[]
            {
                Chamber(cavern, fieldOfView, random),
                Crystals(scenes, cavern, fieldOfView, random),
                Underside(cavern, random),
                River(cavern, fieldOfView, random),
            };
        }

        /// <summary>The whole chamber, from high on its wall across to the far side: the establishing view.</summary>
        private static IntroShot Chamber(CavernSceneConfig cavern, float fieldOfView, Random random)
        {
            float waterY = cavern.Water.LevelY;
            float radius = cavern.Rock.CaveRadius * CHAMBER_RADIUS_FRACTION;
            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
            float arc = MathHelper.ToRadians(CHAMBER_ARC_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                float b = bearing + arc * s;
                path[i] = new Vector3(MathF.Cos(b) * radius, waterY + CHAMBER_HEIGHT - CHAMBER_DROP * s, MathF.Sin(b) * radius);
            }

            //Across the cave and low: the look swings with the arc, so the lens pans the far wall rather
            //than holding one patch of it.
            float middle = bearing + arc * 0.5f + MathHelper.Pi;
            Vector3 lookAt = new(MathF.Cos(middle) * radius * 0.45f, waterY + 18f, MathF.Sin(middle) * radius * 0.45f);

            return new IntroShot("the cavern", path, CHAMBER_SECONDS, fieldOfView * 1.25f, lookAt: lookAt);
        }

        /// <summary>A glide low over the river onto one of the low crystal clusters.</summary>
        private static IntroShot Crystals(SceneRenderer scenes, CavernSceneConfig cavern, float fieldOfView, Random random)
        {
            float waterY = cavern.Water.LevelY;

            //The low clusters, and one of them rolled.
            int chosen = 0, lowCount = 0;
            for (int k = 0; k < SceneRenderer.CAVERN_CRYSTAL_COUNT; k++)
            {
                if (scenes.CavernCrystalCenter(k).Y > waterY + CRYSTAL_LOW) continue;
                lowCount++;
                if (random.Next(lowCount) == 0) chosen = k;
            }

            Vector3 crystal = scenes.CavernCrystalCenter(chosen);
            Vector2 inward = -Vector2.Normalize(new Vector2(crystal.X, crystal.Z));
            float side = MathHelper.ToRadians(CRYSTAL_SIDE_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
            Vector2 approach = IntroPaths.Rotate(inward, side);

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float d = MathHelper.Lerp(CRYSTAL_FROM, CRYSTAL_TO, i / (float)(PATH_POINTS - 1));
                path[i] = new Vector3(crystal.X + approach.X * d, waterY + CRYSTAL_GLIDE_HEIGHT, crystal.Z + approach.Y * d);
            }

            //A little over the cluster's centre: its tallest spar stands up from there, and the look over the
            //water keeps the river's glint of it in the bottom of the frame.
            return new IntroShot("the crystals", path, CRYSTAL_SECONDS, fieldOfView * 1.1f, lookAt: crystal + new Vector3(0f, 4f, 0f));
        }

        /// <summary>
        /// The island from underneath (#656): a crane rising a little as it closes on the funnel from one side, the look
        /// on the funnel's glass under the rim.
        /// </summary>
        private static IntroShot Underside(CavernSceneConfig cavern, Random random)
        {
            float waterY = cavern.Water.LevelY;
            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
            Vector2 outward = new(MathF.Cos(bearing), MathF.Sin(bearing));

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                float out1 = MathHelper.Lerp(UNDER_OUT_FROM, UNDER_OUT_TO, s);
                path[i] = new Vector3(outward.X * out1, waterY + MathHelper.Lerp(UNDER_FROM_ABOVE_WATER, UNDER_TO_ABOVE_WATER, s), outward.Y * out1);
            }

            return new IntroShot("the underside", path, UNDER_SECONDS, MathHelper.ToRadians(UNDER_FOV_DEGREES),
                lookAt: new Vector3(0f, UNDER_LOOK_Y, 0f));
        }

        /// <summary>A skim low over the rippling river, looking along the run.</summary>
        private static IntroShot River(CavernSceneConfig cavern, float fieldOfView, Random random)
        {
            float waterY = cavern.Water.LevelY;
            float radius = cavern.Rock.CaveRadius * RIVER_RADIUS_FRACTION;
            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
            float sign = random.Next(2) == 0 ? 1f : -1f;

            //Tangent to the circle of that radius at the bearing, run centred on the point of tangency
            Vector2 at = new(MathF.Cos(bearing) * radius, MathF.Sin(bearing) * radius);
            Vector2 along = new Vector2(-MathF.Sin(bearing), MathF.Cos(bearing)) * sign;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                Vector2 plan = at + along * ((s - 0.5f) * RIVER_RUN);
                path[i] = new Vector3(plan.X, waterY + RIVER_HEIGHT, plan.Y);
            }

            return new IntroShot("the river", path, RIVER_SECONDS, fieldOfView * 1.15f,
                lookAhead: 25f, pitchDownDegrees: RIVER_PITCH_DOWN_DEGREES);
        }
    }
}
