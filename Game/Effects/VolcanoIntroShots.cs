using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The volcano's own shots for a chapter intro's prologue (#530): a pass along the flank across the lava
    /// rivers, then a climb up the flank that crests the rim and looks down into the crater — cut together,
    /// then cut to the tour's last leg. The owner's verdict on #509 was that the intro "never looks INTO the
    /// volcano from above", and the standard tour cannot: it is a spline round the arena, and the crater is a
    /// bowl on a summit 250 units off, up 140, that every reference of #509 is built round.
    /// <para>
    /// <b>Every height is read off the cone itself</b> (<see cref="SceneRenderer.VolcanoGroundHeight"/>, the
    /// host's mirror of <c>Volcano.fx</c>'s height field, gullies and crater included), so a path hugs the
    /// flank by a stated clearance whatever the config's cone is, and the rim is where the mirror says it is.
    /// The rivers are found through <see cref="SceneRenderer.VolcanoLightPosition"/>, the lamps that ride
    /// them, so the flank shot arcs across river 0 — the one aimed past the arena — and its neighbour.
    /// </para>
    /// <para>
    /// <b>The crater shot passes BESIDE the axis, never over it.</b> Its look is pinned on the vent, and a lens
    /// straight above a fixed look-at has no horizontal forward left to build an up vector from; forty units
    /// abeam it looks down at about fifty degrees at the closest point, which is the picture, and it also keeps
    /// the lens out of the ash column's stem. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class VolcanoIntroShots
    {
        //The flank pass: an arc round the cone at this fraction of its radius, this many radians either side of
        //the river-0 side (the arena's), this far over the highest ground under the arc (the gullies are eleven
        //units deep here, so a constant height is what keeps the lens from bobbing over them), looking up the
        //flank at the vent. ⚠ Twenty units up and looking at a point on the river, the first cut was nothing
        //but river: three flows crossed the frame as bands of saturated orange with no cone round them. From
        //thirty-six up with the vent as the look the rivers run down the frame towards the lens and the summit
        //stands over them with its fountain, which is the "eruption from a distance" every reference drew.
        private const float FLANK_RADIUS_FRACTION = 0.60f;
        private const float FLANK_ARC_RADIANS = 0.42f;
        private const float FLANK_CLEARANCE = 36f;
        private const float FLANK_SECONDS = 3.2f;

        //The crater: a straight run from this fraction of the cone's radius on the arena's side to this much
        //beyond the axis, offset abeam of the axis, hugging the flank by CLIMB_CLEARANCE until the ramp lifts it
        //to RIM_CLEARANCE over the rim between the two radii below — so the crater opens as the lens crests.
        private const float CRATER_FROM_FRACTION = 0.44f;
        private const float CRATER_PAST_AXIS = 55f;
        private const float CRATER_ABEAM = 40f;
        private const float CLIMB_CLEARANCE = 16f;
        private const float RIM_CLEARANCE = 30f;
        private const float RAMP_FROM_FRACTION = 0.30f;
        private const float CRATER_SECONDS = 3.8f;

        //Points per path. The heights are read at every one, so the climb follows the flank's own curve.
        private const int PATH_POINTS = 96;

        /// <summary>
        /// The prologue for the volcano the renderer is drawing, or null when it has no volcano config.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Volcano) is not VolcanoSceneConfig volcano) return null;

            return new[]
            {
                Flank(scenes, volcano, fieldOfView, random),
                Crater(scenes, volcano, fieldOfView, random),
            };
        }

        /// <summary>
        /// Along the flank across the rivers: an arc round the cone at mid-flank height, centred on the side
        /// river 0 runs down, the lens on the vent so the flows stream down the frame and the eruption stands
        /// over them.
        /// </summary>
        private static IntroShot Flank(SceneRenderer scenes, VolcanoSceneConfig volcano, float fieldOfView, Random random)
        {
            Vector2 cone = volcano.ConeCenter.ToVector2();
            float radius = volcano.ConeRadius * FLANK_RADIUS_FRACTION;

            //River 0's bearing off the lamp that rides it (slot 1), at whatever point of its run the clock has
            //put it; the wander is small against the arc.
            Vector3 lamp = scenes.VolcanoLightPosition(1, 0f);
            float centre = MathF.Atan2(lamp.Z - cone.Y, lamp.X - cone.X);

            //Which way round is rolled; the arc is the same either way.
            float sign = random.Next(2) == 0 ? 1f : -1f;
            float from = centre - sign * FLANK_ARC_RADIANS;
            float to = centre + sign * FLANK_ARC_RADIANS;

            var path = new Vector3[PATH_POINTS];
            float highest = float.MinValue;

            for (int i = 0; i < PATH_POINTS; i++)
            {
                float bearing = MathHelper.Lerp(from, to, i / (float)(PATH_POINTS - 1));
                float x = cone.X + MathF.Cos(bearing) * radius;
                float z = cone.Y + MathF.Sin(bearing) * radius;
                path[i] = new Vector3(x, 0f, z);
                highest = MathF.Max(highest, scenes.VolcanoGroundHeight(x, z));
            }

            float y = highest + FLANK_CLEARANCE;
            for (int i = 0; i < PATH_POINTS; i++) path[i].Y = y;

            //The look: the vent, so the rivers run down the frame towards the lens and the summit with its
            //fountain stands over them. The rim hides the vent itself from here; what shows above it is the
            //eruption.
            return new IntroShot("the flank", path, FLANK_SECONDS, fieldOfView * 1.15f, lookAt: scenes.VolcanoLightPosition(0, 0f));
        }

        /// <summary>
        /// Up the flank and over the rim: a straight run from the arena's side of the cone to past its axis,
        /// forty units abeam of it, the lens pinned on the vent. It hugs the flank on the way up, so the rim
        /// hides the crater until the lens crests it, and then holds thirty units over the rim across the bowl.
        /// </summary>
        private static IntroShot Crater(SceneRenderer scenes, VolcanoSceneConfig volcano, float fieldOfView, Random random)
        {
            Vector2 cone = volcano.ConeCenter.ToVector2();
            Vector3 vent = scenes.VolcanoLightPosition(0, 0f);

            //Approach from the arena's side, so the run reads as leaving the play field for the summit; abeam
            //to whichever side is rolled.
            float towardsArena = MathF.Atan2(-cone.Y, -cone.X);
            Vector2 along = new(MathF.Cos(towardsArena), MathF.Sin(towardsArena));
            Vector2 abeam = new Vector2(-along.Y, along.X) * (random.Next(2) == 0 ? CRATER_ABEAM : -CRATER_ABEAM);

            Vector2 start = cone + along * (volcano.ConeRadius * CRATER_FROM_FRACTION) + abeam;
            Vector2 end = cone - along * CRATER_PAST_AXIS + abeam;

            //The rim's height, read at the crater's radius on the approach bearing (no gully reaches the rim,
            //so it is the same all round), and the crest the ramp lifts the lens to over it.
            float rimY = scenes.VolcanoGroundHeight(cone.X + along.X * volcano.CraterRadius, cone.Y + along.Y * volcano.CraterRadius);
            float crest = rimY + RIM_CLEARANCE;
            float rampFrom = volcano.ConeRadius * RAMP_FROM_FRACTION;
            float rampTo = volcano.CraterRadius + 4f;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                Vector2 plan = Vector2.Lerp(start, end, i / (float)(PATH_POINTS - 1));
                float r = Vector2.Distance(plan, cone);

                //Hugging the flank until the ramp, which runs on the distance to the axis so it is the same
                //climb whichever side the lens passes; over the bowl the crest holds, since the ground drops
                //away under it and the hug would have dived into the crater after the ash.
                float ramp = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((rampFrom - r) / MathF.Max(rampFrom - rampTo, 1f), 0f, 1f));
                float hug = scenes.VolcanoGroundHeight(plan.X, plan.Y) + CLIMB_CLEARANCE;
                float y = MathF.Max(hug, MathHelper.Lerp(hug, crest, ramp));

                path[i] = new Vector3(plan.X, y, plan.Y);
            }

            return new IntroShot("the crater", path, CRATER_SECONDS, fieldOfView * 1.15f, lookAt: vent);
        }
    }
}
