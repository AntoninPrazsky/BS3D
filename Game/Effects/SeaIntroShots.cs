using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The sea's own shots for a chapter intro's prologue (#559): the island alone on the open water, a skim
    /// over the swell into the sun's glint, and a low pass round the island's flank at the waterline — cut
    /// together, then cut to the tour's last leg. The sea has no landmark of its own; what it builds is the
    /// swell (<c>Sea.fx</c>'s six Gerstner waves), the glint the sun lays across it, and the one rock in it,
    /// which is the island.
    /// <para>
    /// <b>The water is the only ground, and it is bounded rather than mirrored.</b> The six waves' weights sum to
    /// 2.92 of <see cref="SeaSceneConfig.WaveAmplitude"/> and the chop adds its own amplitude, so no crest stands
    /// higher than <see cref="CrestHeight"/> over the mean level whatever the clock — a lens held that far plus a
    /// margin over <see cref="SeaSceneConfig.LevelY"/> is never under water.
    /// </para>
    /// </summary>
    internal static class SeaIntroShots
    {
        //The open water: a slow arc round the arena this far out, this much of a turn, dollying in a little and
        //craning up, the lens on the island. The establishing view: one rock, a lot of sea, the horizon.
        private const float WIDE_FROM_RADIUS = 235f;
        private const float WIDE_TO_RADIUS = 195f;
        private const float WIDE_SWEEP_RADIANS = 0.30f;
        private const float WIDE_FROM_HEIGHT = 34f;
        private const float WIDE_TO_HEIGHT = 58f;
        private const float WIDE_SECONDS = 3.2f;

        //The swell: a run this long this high over the mean level, into the sun so the glint lies down the
        //middle of the frame, abeam of the arena by this much (so the island is never on the line), pitched
        //this far down so the waves fill the lower half.
        private const float SWELL_RUN = 70f;
        private const float SWELL_HEIGHT = 4.2f;
        private const float SWELL_ABEAM_MIN = 85f;
        private const float SWELL_ABEAM_MAX = 140f;
        private const float SWELL_PITCH_DOWN_DEGREES = 3f;
        private const float SWELL_SECONDS = 3.0f;

        //The island: an arc round its flank at this radius (the rim is at ArenaIsland.RADIUS, 26), this low
        //over the water, the lens on the island's side a little under its rim, so the water meets the rock
        //across the frame and the rim and the hanging field stand over it.
        private const float FLANK_RADIUS = 47f;
        private const float FLANK_HEIGHT = 4.6f;
        private const float FLANK_SWEEP_RADIANS = 0.62f;
        private const float FLANK_SECONDS = 3.0f;

        /// <summary>The highest a crest can stand over the mean level, in units of the swell's amplitude, plus the chop's.</summary>
        private static float CrestHeight(SeaSceneConfig sea) => sea.WaveAmplitude * 2.92f + sea.ChopAmplitude;

        /// <summary>
        /// The prologue for the sea being drawn, or null when there is no sea config. <paramref name="sunDirection"/>
        /// is the direction towards the sun the dome lights the scene from (the host's rig), or null.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, Vector3? sunDirection, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Sea) is not SeaSceneConfig sea) return null;

            //Into the sun, where there is one to go into; otherwise a rolled heading.
            Vector2 sun = sunDirection is Vector3 s ? new Vector2(s.X, s.Z) : Vector2.Zero;
            Vector2 heading = sun.LengthSquared() > 0.01f
                ? Vector2.Normalize(sun)
                : AridIntroPaths.Bearing(AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi));

            return new[]
            {
                OpenWater(sea, heading, fieldOfView, random),
                Swell(sea, heading, fieldOfView, random),
                Flank(sea, fieldOfView, random),
            };
        }

        /// <summary>
        /// The establishing view: from well out over the water, looking back at the island with the sun behind
        /// it — so the glint runs across the sea towards it — arcing a little and craning up.
        /// </summary>
        private static IntroShot OpenWater(SeaSceneConfig sea, Vector2 heading, float fieldOfView, Random random)
        {
            float sunBearing = MathF.Atan2(heading.Y, heading.X);

            //Opposite the sun, a little to one side, so the island stands in the glint's path.
            float from = sunBearing + MathHelper.Pi + AridIntroPaths.Roll(random, -0.35f, 0.35f);
            float sign = random.Next(2) == 0 ? 1f : -1f;

            Vector2[] plan = AridIntroPaths.Arc(Vector2.Zero, from, from + sign * WIDE_SWEEP_RADIANS, WIDE_FROM_RADIUS, WIDE_TO_RADIUS);
            var path = new Vector3[plan.Length];
            for (int i = 0; i < plan.Length; i++)
            {
                float s = i / (float)(plan.Length - 1);
                path[i] = new Vector3(plan[i].X, sea.LevelY + MathHelper.Lerp(WIDE_FROM_HEIGHT, WIDE_TO_HEIGHT, s * s * (3f - 2f * s)), plan[i].Y);
            }

            return new IntroShot("the open water", path, WIDE_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(0f, ArenaIsland.TOP_Y, 0f));
        }

        /// <summary>
        /// Over the swell into the sun: a run a few units over the crests, abeam of the arena, the glint lying
        /// down the frame ahead.
        /// </summary>
        private static IntroShot Swell(SeaSceneConfig sea, Vector2 heading, float fieldOfView, Random random)
        {
            Vector2 abeam = new Vector2(-heading.Y, heading.X) * (random.Next(2) == 0 ? 1f : -1f)
                * AridIntroPaths.Roll(random, SWELL_ABEAM_MIN, SWELL_ABEAM_MAX);

            //Starting behind the arena's line and running along the heading: the closest the run comes to the
            //island is the abeam distance itself.
            Vector2 from = abeam - heading * (SWELL_RUN * AridIntroPaths.Roll(random, 0.2f, 0.8f));
            Vector2 to = from + heading * SWELL_RUN;

            float y = sea.LevelY + MathF.Max(SWELL_HEIGHT, CrestHeight(sea) + 2f);
            Vector2[] plan = AridIntroPaths.Line(from, to);
            var path = new Vector3[plan.Length];
            for (int i = 0; i < plan.Length; i++) path[i] = new Vector3(plan[i].X, y, plan[i].Y);

            return new IntroShot("the swell", path, SWELL_SECONDS, fieldOfView * 1.15f,
                lookAhead: 20f, pitchDownDegrees: SWELL_PITCH_DOWN_DEGREES);
        }

        /// <summary>
        /// Round the island at the waterline: an arc a little way off its flank, the lens on its side just under
        /// the rim, so the sea meets the rock across the frame.
        /// </summary>
        private static IntroShot Flank(SeaSceneConfig sea, float fieldOfView, Random random)
        {
            float from = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            float sign = random.Next(2) == 0 ? 1f : -1f;

            Vector2[] plan = AridIntroPaths.Arc(Vector2.Zero, from, from + sign * FLANK_SWEEP_RADIANS, FLANK_RADIUS, FLANK_RADIUS);
            float y = sea.LevelY + MathF.Max(FLANK_HEIGHT, CrestHeight(sea) + 2f);
            var path = new Vector3[plan.Length];
            for (int i = 0; i < plan.Length; i++) path[i] = new Vector3(plan[i].X, y, plan[i].Y);

            return new IntroShot("the island", path, FLANK_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(0f, ArenaIsland.TOP_Y - 2f, 0f));
        }
    }
}
