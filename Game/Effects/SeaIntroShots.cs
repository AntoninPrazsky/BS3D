using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The sea's own shots for a chapter intro's prologue (#559): a low pass round the island's flank at the
    /// waterline, a skim over the swell into the sun's glint, and a flight low over the water that climbs to the
    /// sun the glint comes from (#652) — cut together, then cut to the tour's last leg. The sea has no landmark of
    /// its own; what it builds is the swell (<c>Sea.fx</c>'s twenty Gerstner waves), the glint the sun lays across it,
    /// and the one rock in it, which is the island.
    /// <para>
    /// <b>It opened on the island from far out and high, and the owner threw that shot out (#652):</b> "the high,
    /// distant view where the island is small in the middle doesn't work — an empty, boring view". One rock and a
    /// lot of sea from 235 units is exactly what the scene is, and nothing in it to look at. The island is now the
    /// first shot's subject from close at the waterline, and the far view is replaced by the one the owner asked
    /// for: skimming the waves through the glitter, then pitching up and rising until the lens is on the sun, so the
    /// viewer sees the glitter <i>come from</i> it.
    /// </para>
    /// <para>
    /// <b>The water is the only ground, and it is bounded rather than mirrored.</b> No crest stands higher than
    /// <see cref="CrestHeight"/> over the mean level, so a lens held <see cref="LENS_CLEARANCE"/> over that above
    /// <see cref="SeaSceneConfig.LevelY"/> is never under water. The bound was the six waves' weights summed (2.92 of
    /// <see cref="SeaSceneConfig.WaveAmplitude"/>), every crest at once; #674's twenty sum to 5.0, and twenty waves at
    /// independent phases never come near that — over eight million points at random times the highest stood at 3.93 —
    /// so the bound is <see cref="CREST_REACH"/> above what was seen, and the clearance is what keeps the shots where
    /// #652 framed them (the skim and the flank at their own heights, the climb starting 0.27 higher than it did).
    /// </para>
    /// </summary>
    internal static class SeaIntroShots
    {
        //The climb (#652): a run this long towards the sun, abeam of the arena by this much, from this far over the crests
        //up to this high, the look starting a little below the horizon on the glint ahead and ending on the sun itself,
        //swung up between them over the last three quarters of the run. The rise is eased the same way, so the lens
        //skims the waves for the first moments — the glitter fills the lower half — and then lifts away from them.
        private const float CLIMB_RUN = 95f;
        private const float CLIMB_ABEAM_MIN = 75f;
        private const float CLIMB_ABEAM_MAX = 125f;
        private const float CLIMB_FROM_HEIGHT = 3.6f;
        private const float CLIMB_TO_HEIGHT = 34f;
        private const float CLIMB_FIRST_PITCH_DEGREES = -3f;
        private const float CLIMB_LOOK_FAR = 400f;
        private const float CLIMB_SECONDS = 4.6f;

        //With no sun to climb to (a dome that gives none), the elevation the climb ends at
        private const float CLIMB_FALLBACK_ELEVATION_DEGREES = 32f;

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
        private static float CrestHeight(SeaSceneConfig sea) => sea.WaveAmplitude * CREST_REACH + sea.ChopAmplitude;

    //How high the swell reaches, in its own amplitude: 3.93 seen over eight million samples of Sea.fx's twenty waves (their
    //weights sum to 5.0, a crest of every wave at once that independent phases never approach). Re-measure on a new set.
    private const float CREST_REACH = 4.2f;

    //How far over the highest crest a lens is held
    private const float LENS_CLEARANCE = 1.5f;

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

            //The sun's height above the horizon, for the climb's last look
            float elevation = sunDirection is Vector3 toSun && toSun.LengthSquared() > 1e-6f
                ? MathF.Asin(MathHelper.Clamp(Vector3.Normalize(toSun).Y, -1f, 1f))
                : MathHelper.ToRadians(CLIMB_FALLBACK_ELEVATION_DEGREES);

            return new[]
            {
                Flank(sea, fieldOfView, random),
                Swell(sea, heading, fieldOfView, random),
                Climb(sea, heading, elevation, fieldOfView, random),
            };
        }

        /// <summary>
        /// The climb (#652): a run over the crests towards the sun, abeam of the arena, that rises while the look swings
        /// up from the glint ahead to the sun itself — the last frames are the sun and the sky round it, and the glitter
        /// the lens has been skimming lies under them, leading to it.
        /// </summary>
        private static IntroShot Climb(SeaSceneConfig sea, Vector2 heading, float sunElevation, float fieldOfView, Random random)
        {
            Vector2 abeam = new Vector2(-heading.Y, heading.X) * (random.Next(2) == 0 ? 1f : -1f)
                * AridIntroPaths.Roll(random, CLIMB_ABEAM_MIN, CLIMB_ABEAM_MAX);

            //Starting well behind the arena's line: the closest the run comes to the island is the abeam distance.
            Vector2 from = abeam - heading * (CLIMB_RUN * AridIntroPaths.Roll(random, 0.25f, 0.6f));
            Vector2 to = from + heading * CLIMB_RUN;

            float low = sea.LevelY + MathF.Max(CLIMB_FROM_HEIGHT, CrestHeight(sea) + LENS_CLEARANCE);
            float high = sea.LevelY + CLIMB_TO_HEIGHT;

            Vector2[] plan = AridIntroPaths.Line(from, to);
            var path = new Vector3[plan.Length];
            var look = new Vector3[plan.Length];

            for (int i = 0; i < plan.Length; i++)
            {
                float s = i / (float)(plan.Length - 1);
                float lift = s * s * (3f - 2f * s);
                path[i] = new Vector3(plan[i].X, MathHelper.Lerp(low, high, lift), plan[i].Y);

                //The look: level with the run at first, a touch down on the glint, then up to the sun
                float swing = MathHelper.Clamp((s - 0.25f) / 0.75f, 0f, 1f);
                swing = swing * swing * (3f - 2f * swing);
                float pitch = MathHelper.Lerp(MathHelper.ToRadians(CLIMB_FIRST_PITCH_DEGREES), sunElevation, swing);
                Vector3 direction = new(heading.X * MathF.Cos(pitch), MathF.Sin(pitch), heading.Y * MathF.Cos(pitch));
                look[i] = path[i] + direction * CLIMB_LOOK_FAR;
            }

            return new IntroShot("the climb to the sun", path, CLIMB_SECONDS, fieldOfView * 1.2f, lookAtPath: look);
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

            float y = sea.LevelY + MathF.Max(SWELL_HEIGHT, CrestHeight(sea) + LENS_CLEARANCE);
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
            float y = sea.LevelY + MathF.Max(FLANK_HEIGHT, CrestHeight(sea) + LENS_CLEARANCE);
            var path = new Vector3[plan.Length];
            for (int i = 0; i < plan.Length; i++) path[i] = new Vector3(plan[i].X, y, plan[i].Y);

            return new IntroShot("the island", path, FLANK_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(0f, ArenaIsland.TOP_Y - 2f, 0f));
        }
    }
}
