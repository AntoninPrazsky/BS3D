using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The volcano's own shots for a chapter intro's prologue (#530, #975): the whole volcano from far out, a cut to
    /// the cone from its foot as a burst goes off, then a climb up the flank that crests the rim and looks down into
    /// the crater — cut together, then cut to the tour's last leg. The owner's verdict on #509 was that the intro
    /// "never looks INTO the volcano from above", and the standard tour cannot: it is a spline round the arena, and
    /// the crater is a bowl on a summit 250 units off, up 140, that every reference of #509 is built round. His note
    /// on #975 was that the first two shots were the same picture (both from the arena's side with the lens on the
    /// vent): "start with a global view of the volcano, then a cut to the volcano spewing, then the continuation".
    /// <para>
    /// <b>The three stand on three different sides and at three distances.</b> The volcano is seen from five hundred
    /// units out, high, a little round from the arena's side so the island stands in the same frame as the cone; the
    /// eruption from the cone's foot, low, ninety degrees and more round the other way; the crater run climbs from
    /// the arena's side. <c>VolcanoIntroShotsTests</c> holds every cut to a lens that moves at least
    /// <c>MIN_CUT_DISTANCE</c> AND a view that turns at least <c>MIN_CUT_ANGLE_DEGREES</c>, stricter than the cities'
    /// either-or, because a view that only slides along its own line is still the same picture.
    /// </para>
    /// <para>
    /// <b>The burst is cued, not hoped for.</b> A burst is on for 4.5 s of every 19, jittered inside its period, so a
    /// shot of a few seconds met one by luck about a third of the time, and the storm's way (stretching the shots to
    /// land on the next event) cannot reach events nineteen seconds apart. So the builder cues one
    /// (<see cref="SceneRenderer.CueVolcanoBurst"/>): it goes off <see cref="BURST_LEAD"/> into the eruption shot at
    /// its full size, the natural bursts that would crowd it give way, and the light, the jets, the deck's glow and
    /// the boom all ride it because they all read the one schedule. It is cued twice: when the intro is built, so the
    /// natural bursts that would start in the first shot give way, and again on the eruption shot's first frame
    /// (<see cref="IntroShot.Start"/>) if the intro's own clock has fallen behind the wall clock by then — a pause
    /// stops it, and so does a hitch (<c>ChapterIntro.MAX_INTRO_STEP</c>), while the burst runs on the wall clock and
    /// would otherwise go off behind the pause page or in the shot before.
    /// </para>
    /// <para>
    /// <b>Every height is read off the cone itself</b> (<see cref="SceneRenderer.VolcanoGroundHeight"/>, the host's
    /// mirror of <c>Volcano.fx</c>'s height field, gullies and crater included), so a path clears the ground by a
    /// stated margin whatever the config's cone is, and the rim is where the mirror says it is.
    /// </para>
    /// <para>
    /// <b>The crater shot passes BESIDE the axis, never over it.</b> Its look is pinned on the vent, and a lens
    /// straight above a fixed look-at has no horizontal forward left to build an up vector from; abeam, it looks
    /// steeply down into the bowl at the closest point, which is the picture (fifty degrees at forty units
    /// abeam, the first cut; #645 moved it out to fifty, where the vent turns the view more slowly), and it also
    /// keeps the lens out of the ash column's stem. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class VolcanoIntroShots
    {
        //The volcano: a slow push in from VIEW_FROM to VIEW_TO units off the cone's axis, VIEW_HEIGHT over the highest
        //ground under it, on a bearing rolled between the two angles round from the arena's side, drifting
        //VIEW_DRIFT_DEGREES back towards it so the island comes into the frame as the shot runs. The look is on the
        //axis VIEW_LOOK_UNDER_VENT under the vent: the cone, the plain round it and the column over it in one frame.
        private const float VIEW_FROM = 600f;
        private const float VIEW_TO = 520f;
        private const float VIEW_HEIGHT = 120f;
        private const float VIEW_OFF_MIN_DEGREES = 22f;
        private const float VIEW_OFF_MAX_DEGREES = 45f;
        private const float VIEW_DRIFT_DEGREES = 8f;
        private const float VIEW_LOOK_UNDER_VENT = 30f;
        private const float VIEW_SECONDS = 3.6f;

        //The eruption: a push in along one bearing from ERUPTION_FROM to ERUPTION_TO off the axis — the cone's foot,
        //its radius being 215 — ERUPTION_CLEARANCE over the ground, the bearing rolled between the two angles round
        //the OTHER way from the arena's side, the look ERUPTION_LOOK_OVER_VENT over the vent so the rim sits under the
        //middle of a narrower frame and the jets and the column fill the rest.
        private const float ERUPTION_FROM = 310f;
        private const float ERUPTION_TO = 270f;
        private const float ERUPTION_CLEARANCE = 12f;
        private const float ERUPTION_OFF_MIN_DEGREES = 85f;
        private const float ERUPTION_OFF_MAX_DEGREES = 125f;
        private const float ERUPTION_LOOK_OVER_VENT = 40f;
        private const float ERUPTION_FOV = 0.8f;
        private const float ERUPTION_SECONDS = 3.8f;

        /// <summary>How far into the eruption shot the cued burst goes off: long enough for the cone to be read
        /// before it lights, short enough that the attack and most of the decay play out inside the shot.</summary>
        internal const float BURST_LEAD = 0.9f;

        //How far the eruption shot's real start may be from the one the intro was built for before the burst is cued
        //again for it: under it, a cue would only restart a burst already lined up.
        private const float RECUE_TOLERANCE = 0.1f;

        //The crater: a straight run from this fraction of the cone's radius on the arena's side to this much
        //beyond the axis, offset abeam of the axis, hugging the flank by CLIMB_CLEARANCE until the ramp lifts it
        //to RIM_CLEARANCE over the rim between the two radii below — so the crater opens as the lens crests.
        //
        //⚠ SLOW AND SMOOTH, NOT A JUDDER (#645, #530). The first cut read the ground under every one of the path's
        //points and flew 150 units in 3.8 s: measured over sixteen rolls that is 59 u/s, a vertical speed of up
        //to 129 u/s, a vertical acceleration of 253 u/s² rms and up to 1141 (the flank's gullies, one bump per
        //point), and a view pitching 46 degrees a second rms and up to 142 - the owner's "the camera judders as it
        //approaches the top". Now the run starts nearer (CRATER_FROM_FRACTION 0.34), lasts 5.4 s, and the height
        //is dilated and blurred like the mountains' and the desert's runs (`AridIntroPaths.Hug`), the ramp over
        //the rim entering as its floor.
        private const float CRATER_FROM_FRACTION = 0.34f;
        private const float CRATER_PAST_AXIS = 55f;
        private const float CRATER_ABEAM = 50f;
        private const float CLIMB_CLEARANCE = 16f;
        private const float CLIMB_LATERAL = 8f;
        private const int CLIMB_WINDOW = 21;
        private const int CLIMB_SMOOTHING = 3;
        private const float RIM_CLEARANCE = 30f;
        private const float RAMP_FROM_FRACTION = 0.30f;
        private const float CRATER_SECONDS = 5.4f;

        //How far either side of a level path the ground is read, so a gully's wall beside the line cannot stand
        //into the lens.
        private const float LEVEL_LATERAL = 10f;

        /// <summary>
        /// The prologue for the volcano the renderer is drawing, starting at <paramref name="time"/> on its wall
        /// clock, or null when it has no volcano config; cues the burst the eruption shot meets, and cues it again
        /// off <paramref name="clock"/> as that shot comes on screen if the intro has fallen behind.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float time, Func<float> clock, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Volcano) is not VolcanoSceneConfig volcano) return null;

            scenes.CueVolcanoBurst(time, BurstOnset(time));
            return Shots(scenes.VolcanoGroundHeight, scenes.VolcanoLightPosition(0, 0f), volcano, fieldOfView, random,
                eruptionStarts: () =>
                {
                    float onset = Recue(time, clock?.Invoke() ?? time + VIEW_SECONDS);
                    if (!float.IsNaN(onset)) scenes.CueVolcanoBurst(time, onset);
                });
        }

        /// <summary>
        /// The burst's second the eruption shot wants when it comes on screen at <paramref name="now"/> on the wall
        /// clock, for an intro built at <paramref name="time"/>; NaN when the cue made at the build already lands it.
        /// </summary>
        internal static float Recue(float time, float now)
        {
            float onset = now + BURST_LEAD;
            return MathF.Abs(onset - BurstOnset(time)) > RECUE_TOLERANCE ? onset : float.NaN;
        }

        /// <summary>The second the cued burst goes off, for a prologue that starts at <paramref name="time"/>.</summary>
        internal static float BurstOnset(float time) => time + VIEW_SECONDS + BURST_LEAD;

        /// <summary>
        /// The three shots off the ground, the vent and the config alone, for a test with no renderer. The volcano
        /// and the eruption stand on opposite sides of the arena's bearing, which side rolled.
        /// <paramref name="eruptionStarts"/> is called as the eruption shot comes on screen.
        /// </summary>
        internal static IntroShot[] Shots(Func<float, float, float> ground, Vector3 vent, VolcanoSceneConfig volcano, float fieldOfView,
            Random random, Action eruptionStarts = null)
        {
            float side = random.Next(2) == 0 ? 1f : -1f;

            return new[]
            {
                Volcano(ground, vent, volcano, side, fieldOfView, random),
                Eruption(ground, vent, volcano, -side, fieldOfView, random, eruptionStarts),
                Crater(ground, vent, volcano, fieldOfView, random),
            };
        }

        /// <summary>
        /// The whole volcano from far out and high: the cone with its rivers and its column, the plain round it,
        /// and the island standing to one side of it as the lens drifts back round towards the arena.
        /// </summary>
        private static IntroShot Volcano(Func<float, float, float> ground, Vector3 vent, VolcanoSceneConfig volcano, float side,
            float fieldOfView, Random random)
        {
            Vector2 cone = volcano.ConeCenter.ToVector2();
            float towardsArena = MathF.Atan2(-cone.Y, -cone.X);
            float off = MathHelper.ToRadians(AridIntroPaths.Roll(random, VIEW_OFF_MIN_DEGREES, VIEW_OFF_MAX_DEGREES));

            Vector2[] plan = AridIntroPaths.Arc(cone, towardsArena + side * off,
                towardsArena + side * (off - MathHelper.ToRadians(VIEW_DRIFT_DEGREES)), VIEW_FROM, VIEW_TO);

            return new IntroShot("the volcano", Level(plan, ground, VIEW_HEIGHT), VIEW_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(cone.X, vent.Y - VIEW_LOOK_UNDER_VENT, cone.Y));
        }

        /// <summary>
        /// The cone from its foot as it goes off: a low push in towards the axis, the look over the vent, so the
        /// flank rises through the bottom of the frame to the rim and the burst's jets and the column stand over it.
        /// </summary>
        private static IntroShot Eruption(Func<float, float, float> ground, Vector3 vent, VolcanoSceneConfig volcano, float side,
            float fieldOfView, Random random, Action starts)
        {
            Vector2 cone = volcano.ConeCenter.ToVector2();
            float towardsArena = MathF.Atan2(-cone.Y, -cone.X);
            Vector2 bearing = AridIntroPaths.Bearing(towardsArena
                + side * MathHelper.ToRadians(AridIntroPaths.Roll(random, ERUPTION_OFF_MIN_DEGREES, ERUPTION_OFF_MAX_DEGREES)));

            Vector2[] plan = AridIntroPaths.Line(cone + bearing * ERUPTION_FROM, cone + bearing * ERUPTION_TO);

            return new IntroShot("the eruption", Level(plan, ground, ERUPTION_CLEARANCE), ERUPTION_SECONDS, fieldOfView * ERUPTION_FOV,
                lookAt: vent + new Vector3(0f, ERUPTION_LOOK_OVER_VENT, 0f), onStart: starts);
        }

        /// <summary>
        /// Up the flank and over the rim: a straight run from the arena's side of the cone to past its axis,
        /// forty units abeam of it, the lens pinned on the vent. It hugs the flank on the way up, so the rim
        /// hides the crater until the lens crests it, and then holds thirty units over the rim across the bowl.
        /// </summary>
        internal static IntroShot Crater(Func<float, float, float> ground, Vector3 vent, VolcanoSceneConfig volcano, float fieldOfView, Random random)
        {
            Vector2 cone = volcano.ConeCenter.ToVector2();

            //Approach from the arena's side, so the run reads as leaving the play field for the summit; abeam
            //to whichever side is rolled.
            float towardsArena = MathF.Atan2(-cone.Y, -cone.X);
            Vector2 along = new(MathF.Cos(towardsArena), MathF.Sin(towardsArena));
            Vector2 abeam = new Vector2(-along.Y, along.X) * (random.Next(2) == 0 ? CRATER_ABEAM : -CRATER_ABEAM);

            Vector2 start = cone + along * (volcano.ConeRadius * CRATER_FROM_FRACTION) + abeam;
            Vector2 end = cone - along * CRATER_PAST_AXIS + abeam;

            //The rim's height, read at the crater's radius on the approach bearing (no gully reaches the rim,
            //so it is the same all round), and the crest the ramp lifts the lens to over it.
            float rimY = ground(cone.X + along.X * volcano.CraterRadius, cone.Y + along.Y * volcano.CraterRadius);
            float crest = rimY + RIM_CLEARANCE;
            float rampFrom = volcano.ConeRadius * RAMP_FROM_FRACTION;
            float rampTo = volcano.CraterRadius + 4f;

            //Hugging the flank until the ramp, which runs on the distance to the axis so it is the same climb
            //whichever side the lens passes; over the bowl the crest holds, since the ground drops away under it
            //and the hug would have dived into the crater after the ash. The ramp is the height's FLOOR: the hug
            //itself (the flank's highest ground round each point, dilated and blurred) can only lift it.
            Vector2[] plan = AridIntroPaths.Line(start, end);
            Vector3[] path = AridIntroPaths.Hug(plan, ground, CLIMB_CLEARANCE, CLIMB_LATERAL, CLIMB_WINDOW, i =>
            {
                float r = Vector2.Distance(plan[i], cone);
                float ramp = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((rampFrom - r) / MathF.Max(rampFrom - rampTo, 1f), 0f, 1f));
                return MathHelper.Lerp(ground(plan[i].X, plan[i].Y) + CLIMB_CLEARANCE, crest, ramp);
            }, CLIMB_SMOOTHING);

            return new IntroShot("the crater", path, CRATER_SECONDS, fieldOfView * 1.15f, lookAt: vent);
        }

        //A path at ONE height, the clearance over the highest ground under the plan and beside it: on the plain and
        //across the gullies a lens that followed the ground would bob, and a level one reads as a crane.
        private static Vector3[] Level(Vector2[] plan, Func<float, float, float> ground, float clearance)
        {
            float highest = float.MinValue;
            foreach (Vector2 p in plan)
                for (int k = -1; k <= 1; k++)
                    for (int m = -1; m <= 1; m++)
                        highest = MathF.Max(highest, ground(p.X + k * LEVEL_LATERAL, p.Y + m * LEVEL_LATERAL));

            var path = new Vector3[plan.Length];
            for (int i = 0; i < plan.Length; i++) path[i] = new Vector3(plan[i].X, highest + clearance, plan[i].Y);

            return path;
        }
    }
}
