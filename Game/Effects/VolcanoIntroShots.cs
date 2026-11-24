using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The volcano's own shots for a chapter intro's prologue (#530, #975, #1127, #1220): a helicopter's run high up the river's
    /// valley between the ranges with the village on the left and the cone on the right, a cut to the cone from its foot as a burst goes off, a
    /// climb up the flank that crests the rim and looks down into the crater, then an orbit round the village on the land past
    /// the field that ends with the cone smoking over it — cut together, then cut to the tour's last leg. The owner's verdict
    /// on #509 was that the intro "never looks INTO the volcano from above", and the standard tour cannot: it is a spline round
    /// the arena, and the crater is a bowl on a summit 250 units off, up 140, that every reference of #509 is built round. His
    /// note on #975 was that the first two shots were the same picture (both from the arena's side with the lens on the vent):
    /// "start with a global view of the volcano, then a cut to the volcano spewing, then the continuation". His note on #1127,
    /// that the player never notices the land at all ("there should also be shots of the village, the river, the mountains
    /// and the forests in the distance"): the play camera faces the cone, the land is behind it, and only the intro looks at
    /// it. And on #1220, that the first shot should be taken from the river: "the camera flies in over the river between the
    /// mountains, past the forest; on the left the village is then visible, on the right the volcano", flown "much higher - as
    /// if it were a view from a helicopter".
    /// <para>
    /// <b>The river is the reel's global view where the land is drawn.</b> It holds the vent, the cone's foot and the island
    /// in the frame throughout, which is what the volcano's own wide shot (five hundred units out, high) was there for, and it
    /// stands in for that shot rather than before it: the two are one picture of the cone from afar, which is what #975 turned
    /// down, and the reel is held to its length (<c>VolcanoIntroShotsTests</c>) with no room for both.
    /// </para>
    /// <para>
    /// <b>Each shot stands somewhere else than the one before it.</b> The river runs high up its valley on the far side of the
    /// arena from the cone, eight hundred to a thousand units from it; the eruption is seen from the cone's foot, low, round
    /// to one side; the crater run climbs from the arena's side; the village is orbited on the far side of the arena again,
    /// seven hundred units from the cone, two cuts after the river.
    /// <c>VolcanoIntroShotsTests</c> holds every cut to a lens that moves at least <c>MIN_CUT_DISTANCE</c> AND a view
    /// that turns at least <c>MIN_CUT_ANGLE_DEGREES</c>, stricter than the cities' either-or, because a view that only
    /// slides along its own line is still the same picture. The village comes last, after the crater: it ends looking at
    /// the cone from the arena's side, much as the crater run starts out looking, and cut ahead of the crater the view
    /// turned as little as 23 degrees on some rolls. The same rule holds the cut OUT of the reel into the tour's last leg,
    /// which opens looking steeply down at the arena from the gun's side: the line from the village to the cone passes
    /// over the arena, so the two views can share a heading, and the village's last look is held nearly level
    /// (<see cref="VILLAGE_END_DOWN_DEGREES"/>) so that the tilt alone turns the view <see cref="CUT_TO_THE_MAP_DEGREES"/>.
    /// </para>
    /// <para>
    /// <b>The river and the village are shot only where they stand</b> (<see cref="SceneRenderer.VolcanoCountryDrawn"/>): Low
    /// draws the land's ground but none of its woods, snags or houses, and there the reel is the three shots it was before
    /// #1127, the volcano's wide shot first.
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
        private const float VIEW_SECONDS = 3.2f;

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
        private const float ERUPTION_SECONDS = 3.4f;

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
        //is rounded like the mountains' and the desert's runs (`AridIntroPaths.Hug`), the ramp over the rim entering
        //as its floor. It climbs ahead of the flank: the heights rounded are the highest within CLIMB_RISE_SECONDS of
        //travel either side (the dilation this run was approved with, #530), so the lens stays as high over the
        //flank as it flew before #1109 rounded the rest of it.
        private const float CRATER_FROM_FRACTION = 0.34f;
        private const float CRATER_PAST_AXIS = 55f;
        private const float CRATER_ABEAM = 50f;
        internal const float CLIMB_CLEARANCE = 16f;
        private const float CLIMB_LATERAL = 8f;
        private const float CLIMB_RISE_SECONDS = 1.2f;
        private const float RIM_CLEARANCE = 30f;
        private const float RAMP_FROM_FRACTION = 0.30f;
        private const float CRATER_SECONDS = 5.4f;

        //How far either side of a level path the ground is read, so a gully's wall beside the line cannot stand
        //into the lens.
        private const float LEVEL_LATERAL = 10f;

        //The village (#1127): an orbit round the village's middle, from VILLAGE_RADIUS_FROM out to VILLAGE_RADIUS_TO, swinging
        //between the two sweeps round to a bearing rolled within VILLAGE_END_OFF_DEGREES of the line from the cone's axis
        //through the village, so it ends with the cone behind the village. The lens flies TREE_AIR over the tallest thing the
        //land stands (VolcanoCountryPlanting.Tallest) above the highest ground within that thing's reach, as AridIntroPaths.Hug
        //lays a drone's height, so it climbs the hillside behind the village as it comes round. The look starts on the village,
        //VILLAGE_LOOK_RISE over the ground at its middle (its roofs), and turns to the bearing VILLAGE_LOOK_SHARE of the way from
        //the village towards a point SUMMIT_OVER_VENT over the vent, tilted no more than VILLAGE_END_DOWN_DEGREES down, so the
        //village stands in the frame's lower half, clear of the skip hint along its foot, and the cone and its column over it.
        //Round the arc the view turns 17 to 25 degrees a second on average, about the pace of the cities' plaza orbit (80
        //degrees in 4 s), and peaks at 27 over the 12 rolls IntroShotSmoothnessTests builds and 28.5 over 64; the test holds it
        //to its MAX_YAW_RATE.
        private const float VILLAGE_RADIUS_FROM = 150f;
        private const float VILLAGE_RADIUS_TO = 140f;
        private const float VILLAGE_SWEEP_MIN_DEGREES = 65f;
        private const float VILLAGE_SWEEP_MAX_DEGREES = 85f;
        private const float VILLAGE_END_OFF_DEGREES = 12f;
        internal const float TREE_AIR = 6f;
        private const float VILLAGE_LOOK_RISE = 6f;
        private const float VILLAGE_LOOK_SHARE = 0.4f;
        private const float SUMMIT_OVER_VENT = 40f;
        private const float VILLAGE_SECONDS = 3.6f;

        //⚠ THE CUT OUT OF THE REEL IS A CUT TOO. The tour's last leg opens looking down at the arena's centre from
        //ChapterIntro.MAP_KEY_MIN_ELEVATION_DEGREES or more, on the gun's side, and the village's last look, at the cone over
        //the village, runs across the arena from that same side: the two can share a heading. Looking down the 11 to 12
        //degrees the blend alone gives, the cut turned as little as 28 degrees (36 of 400 rolls under the reel's 30). Held to
        //VILLAGE_END_DOWN_DEGREES down, the tilt alone turns it CUT_TO_THE_MAP_DEGREES, whichever way the gun faces.
        //
        //The lens is the wider for it, VILLAGE_FOV of the play frame: the lens stands 53 or more over the roofs, which sit
        //21 to 23 degrees under it at the end, and at the crater's 1.15 the village's middle went down to 0.70 of the frame's
        //half-height under its middle, by the skip hint.
        private const float CUT_TO_THE_MAP_DEGREES = 33f;
        private const float VILLAGE_END_DOWN_DEGREES = ChapterIntro.MAP_KEY_MIN_ELEVATION_DEGREES - CUT_TO_THE_MAP_DEGREES;
        private const float VILLAGE_FOV = 1.35f;

        //The river (#1220): a helicopter's run up the river's east arm (its run back out from its turn beside the village, on +X:
        //docs/scenes/volcano.md) where its valley opens out of the ranges towards the field, over its course from RIVER_FROM to
        //RIVER_TO (VolcanoCountry.RIVER_COURSE, measured from its source; both moved together by up to RIVER_ROLL). The owner's
        //verdict on the low run first built here, ten to seventeen over the water: "raise the camera well away from the river"
        //rather than give the banks and the woods the detail a lens among them needs, "as if it were a view from a helicopter.
        //But it must be smooth and cinematic - dramatic, even". So it starts RIVER_HIGH_FROM over the water, over the hills
        //either side of the valley's mouth (their tops 50 to 85 over the water within 300 of it there), and comes down at a
        //steady rate to RIVER_HIGH_TO by the end, banking into the river's bend (RIVER_BANK_DEGREES) and turning its look with
        //it, as the village comes in on the left. Never lower than RIVER_CLEARANCE over the ground within
        //VolcanoCountryPlanting.Reach of the lens: three times the tallest thing the land stands (VolcanoCountryPlanting.Tallest,
        //the church's spire), so whatever stands under the lens is a third of its height or less, and the woods, the banks and
        //the water read as land, not as close detail.
        //
        //The look, in plan: a bearing between the village's roofs and the vent as the lens sees them, RIVER_PAN_FROM of the way
        //round from the roofs to the vent at the cut, so it opens on the cone over the island with the village at the frame's
        //left edge, and turning with the helicopter to RIVER_PAN_TO, halfway, by the end (on a smoothstep), so the village comes
        //in on the left and the cone with its column stands on the right (only the east arm puts them so: up the west arm the
        //village is on the right). In height, RIVER_LOOK_SHARE of the way from the roofs' elevation to that of a point
        //RIVER_LOOK_OVER_VENT over the vent, so the jets and the column's stem stay in the frame over the cone while the lens
        //comes down towards the village. The lens widens from RIVER_FOV to RIVER_END_FOV of the play frame as the two draw apart.
        //
        //⚠ UNDER THE CLOUD DECK. The volcano's sky is the storm's (WeatherPreset.Storm), whose deck is a plane 140 over the
        //world's zero, the lowest any weather lays, and the sky shader finds the deck by crossing the view ray up through that
        //plane from below (Sky.fx), which a lens over it cannot. The run starts some 30 under it: started 170 over the water, a
        //lens at 152 stood over it.
        //
        //⚠ WITHIN ABOUT 680 OF THE DRAIN, SO NOT FARTHER UP THE VALLEY. From up here the lens sees down into the drain's mouth on
        //the island, and past about 680 off it the band of glass the depth buffer cannot part from the dark sheath behind it is
        //wider than from the play view, whatever the near plane (RecoilCamera.NEAR_PLANE_CEILING, NearPlaneTests): the flicker
        //#1193 took off the meadow's valley. A run up the valley between the higher ranges (from 1830 along) stood 810 off. Nor
        //much nearer the turn: past about 1590 along the village and the vent draw apart until no frame holds both (61 degrees
        //apart at 1600 along, 70 at 1560). So the run is a slow glide of about a hundred units, and the bank, the turn of the
        //look and the descent are what move.
        //
        //⚠ THE FLIGHT IS A CURVE THROUGH THE COURSE, NOT THE MEANDERING WATER. From this high the meanders are the water's own
        //business: laid on the centre line rounded by a Gaussian of 70 or 80 units of the course (the low run's was 25), the
        //run's turn changed its way with the meanders, a bank laid on it rocked from one side to the other, and the lens jerked
        //at up to 303 u/s³ over the 300 IntroShotSmoothnessTests holds. A quadratic curve through the course
        //(VolcanoCountry.RiverCourse) at both ends and halfway turns one way only, the water winding under it within about 20 of
        //the lens's track.
        private const float RIVER_FROM = 1694f;
        private const float RIVER_TO = 1600f;
        private const float RIVER_ROLL = 3f;
        internal const float RIVER_HIGH_FROM = 128f;
        internal const float RIVER_HIGH_TO = 100f;
        internal static readonly float RIVER_CLEARANCE = 3f * VolcanoCountryPlanting.Tallest;
        private const float RIVER_LOOK_OVER_VENT = 60f;
        private const float RIVER_LOOK_SHARE = 0.5f;
        private const float RIVER_PAN_FROM = 0.8f;
        private const float RIVER_PAN_TO = 0.5f;
        private const float RIVER_LOOK_REACH = 300f;
        private const float RIVER_FOV = 1.1f;
        private const float RIVER_END_FOV = 1.15f;
        internal const float RIVER_SECONDS = 3.5f;

        /// <summary>The helicopter's bank into the river's bend at its deepest, halfway through the run: eased in from level at
        /// the cut and back out to level by the next (a sine squared over the shot).</summary>
        internal const float RIVER_BANK_DEGREES = 7f;

        //How many points the curve is read at before it is laid evenly along its length
        private const int RIVER_CURVE_POINTS = 400;

        /// <summary>
        /// The prologue for the volcano the renderer is drawing, starting at <paramref name="time"/> on its wall
        /// clock, or null when it has no volcano config; cues the burst the eruption shot meets, and cues it again
        /// off <paramref name="clock"/> as that shot comes on screen if the intro has fallen behind.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float time, Func<float> clock, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Volcano) is not VolcanoSceneConfig volcano) return null;

            bool land = scenes.VolcanoCountryDrawn;
            scenes.CueVolcanoBurst(time, BurstOnset(time, land));
            return Shots(scenes.VolcanoGroundHeight, scenes.VolcanoLightPosition(0, 0f), volcano, fieldOfView, random,
                eruptionStarts: () =>
                {
                    float onset = Recue(time, clock?.Invoke() ?? time + OpeningSeconds(land), land);
                    if (!float.IsNaN(onset)) scenes.CueVolcanoBurst(time, onset);
                },
                land: land);
        }

        /// <summary>
        /// The burst's second the eruption shot wants when it comes on screen at <paramref name="now"/> on the wall
        /// clock, for an intro built at <paramref name="time"/>; NaN when the cue made at the build already lands it.
        /// <paramref name="land"/> is whether the reel opens on the river (see <see cref="Shots"/>).
        /// </summary>
        internal static float Recue(float time, float now, bool land)
        {
            float onset = now + BURST_LEAD;
            return MathF.Abs(onset - BurstOnset(time, land)) > RECUE_TOLERANCE ? onset : float.NaN;
        }

        /// <summary>The second the cued burst goes off, for a prologue that starts at <paramref name="time"/> and opens on the
        /// river where <paramref name="land"/> is drawn, on the volcano where not.</summary>
        internal static float BurstOnset(float time, bool land) => time + OpeningSeconds(land) + BURST_LEAD;

        /// <summary>How long the shot the eruption follows runs: the river's where the land is drawn, the volcano's where not.</summary>
        internal static float OpeningSeconds(bool land) => land ? RIVER_SECONDS : VIEW_SECONDS;

        /// <summary>
        /// The shots off the ground, the vent and the config alone, for a test with no renderer. The volcano and the
        /// eruption stand on opposite sides of the arena's bearing, which side rolled. <paramref name="eruptionStarts"/> is
        /// called as the eruption shot comes on screen. <paramref name="land"/> is whether the tier draws the land's village and
        /// woods: where it does, the reel opens on the river in place of the volcano (#1220) and ends on the village (#1127);
        /// where not, it is the three shots it was before either.
        /// </summary>
        internal static IntroShot[] Shots(Func<float, float, float> ground, Vector3 vent, VolcanoSceneConfig volcano, float fieldOfView,
            Random random, Action eruptionStarts = null, bool land = true)
        {
            float side = random.Next(2) == 0 ? 1f : -1f;

            IntroShot whole = Volcano(ground, vent, volcano, side, fieldOfView, random);
            IntroShot eruption = Eruption(ground, vent, volcano, -side, fieldOfView, random, eruptionStarts);
            IntroShot crater = Crater(ground, vent, volcano, fieldOfView, random);

            //The village and the river are rolled LAST, off a generator of their own seeded by one draw of the reel's: the other
            //three roll exactly what they rolled before #1127, and whether these two are rolled at all (the tier) never moves
            //what the session deals after the intro (the benchmark plays one seed on every tier). The volcano is rolled on every
            //tier for the same reason, and shown only where the river does not stand in for it.
            var landRandom = new Random(random.Next());
            if (!land) return new[] { whole, eruption, crater };

            IntroShot village = Village(ground, vent, volcano, fieldOfView, landRandom);
            return new[] { River(ground, vent, volcano, fieldOfView, landRandom), eruption, crater, village };
        }

        /// <summary>
        /// Up the river out of the ranges (#1220, the owner: "the camera flies in over the river between the mountains, past the
        /// forest; on the left the village is then visible, on the right the volcano", and on the first cut, flown low over the
        /// water: "as if it were a view from a helicopter"): high over its east arm, upstream along its course and down towards
        /// the land round the village, banking into the river's bend and turning with it from the cone over the island onto the
        /// village on the left and the cone with its column on the right. The reel's establishing view where the land is drawn:
        /// it holds the vent, the cone's foot and the island in the frame from its first frame to its last, as the volcano's did.
        /// </summary>
        internal static IntroShot River(Func<float, float, float> ground, Vector3 vent, VolcanoSceneConfig volcano, float fieldOfView,
            Random random)
        {
            VolcanoCountry country = VolcanoCountry.Of(volcano);
            float shift = AridIntroPaths.Roll(random, -RIVER_ROLL, RIVER_ROLL);
            float from = RIVER_FROM + shift, to = RIVER_TO + shift;

            //A quadratic curve through the course at both ends and halfway (its control point set so it passes through the
            //middle), laid evenly along its length
            Vector2 start = country.RiverCourse(from, out _), end = country.RiverCourse(to, out _);
            Vector2 control = 2f * country.RiverCourse(0.5f * (from + to), out _) - 0.5f * (start + end);
            var line = new Vector2[RIVER_CURVE_POINTS];
            for (int k = 0; k < line.Length; k++)
            {
                float u = k / (float)(line.Length - 1);
                line[k] = (1f - u) * (1f - u) * start + 2f * u * (1f - u) * control + u * u * end;
            }

            Vector2[] plan = AridIntroPaths.Even(line);

            //Coming down at a steady rate over the water (the water lies level along the whole arm: a river never climbs,
            //VolcanoCountry), which the ground lifts wherever it or what the land stands on it would come nearer than
            //RIVER_CLEARANCE
            Vector3[] path = AridIntroPaths.Hug(plan, ground, RIVER_CLEARANCE, VolcanoCountryPlanting.Reach, RIVER_SECONDS, i =>
            {
                float water = volcano.LevelY + country.River(plan[i].X, plan[i].Y).Level;
                return water + MathHelper.Lerp(RIVER_HIGH_FROM, RIVER_HIGH_TO, i / (float)(plan.Length - 1));
            });

            //The look, as the lens sees the village and the vent from where it is: in plan turning from most of the way round to
            //the vent onto halfway between them, in height part of the way from the village's roofs up to a point over the vent
            Vector2 village = volcano.Country.Village.ToVector2(), cone = new(vent.X, vent.Z);
            float roofs = ground(village.X, village.Y) + VILLAGE_LOOK_RISE, overVent = vent.Y + RIVER_LOOK_OVER_VENT;
            var look = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 at = new(path[i].X, path[i].Z);
                float toVillage = MathF.Atan2(village.Y - at.Y, village.X - at.X);
                float toCone = MathF.Atan2(cone.Y - at.Y, cone.X - at.X);
                float towardsCone = MathHelper.Lerp(RIVER_PAN_FROM, RIVER_PAN_TO, MathHelper.SmoothStep(0f, 1f, i / (float)(path.Length - 1)));
                float bearing = toVillage + towardsCone * MathHelper.WrapAngle(toCone - toVillage);
                float pitch = MathHelper.Lerp(MathF.Atan2(roofs - path[i].Y, Vector2.Distance(at, village)),
                    MathF.Atan2(overVent - path[i].Y, Vector2.Distance(at, cone)), RIVER_LOOK_SHARE);
                look[i] = path[i] + new Vector3(MathF.Cos(bearing) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(bearing) * MathF.Cos(pitch))
                    * RIVER_LOOK_REACH;
            }

            //The bank: into the way the run turns (a helicopter's roll is positive to the right, and a heading that falls, the
            //way MathF.Atan2(z, x) runs, turns left), level at both cuts
            Vector2 leaving = plan[1] - plan[0], arriving = plan[^1] - plan[^2];
            float turn = MathF.Sign(MathHelper.WrapAngle(MathF.Atan2(arriving.Y, arriving.X) - MathF.Atan2(leaving.Y, leaving.X)));
            var roll = new float[path.Length];
            for (int i = 0; i < roll.Length; i++)
            {
                float swing = MathF.Sin(MathF.PI * i / (roll.Length - 1));
                roll[i] = turn * MathHelper.ToRadians(RIVER_BANK_DEGREES) * swing * swing;
            }

            return new IntroShot("the river", path, RIVER_SECONDS, fieldOfView * RIVER_FOV, lookAtPath: look, rollPath: roll,
                endFieldOfView: fieldOfView * RIVER_END_FOV);
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
            //and the hug would have dived into the crater after the ash. The ramp is the height's FLOOR, climbed ahead
            //of and rounded with the flank's highest ground round each point, so its turns are rounded too (the lens
            //passes at most 0.3 units under the ramp itself) while the flank's own clearance is kept by the lift.
            Vector2[] plan = AridIntroPaths.Line(start, end);
            Vector3[] path = AridIntroPaths.Hug(plan, ground, CLIMB_CLEARANCE, CLIMB_LATERAL, CRATER_SECONDS, i =>
            {
                float r = Vector2.Distance(plan[i], cone);
                float ramp = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((rampFrom - r) / MathF.Max(rampFrom - rampTo, 1f), 0f, 1f));
                return MathHelper.Lerp(ground(plan[i].X, plan[i].Y) + CLIMB_CLEARANCE, crest, ramp);
            }, rise: CLIMB_RISE_SECONDS);

            return new IntroShot("the crater", path, CRATER_SECONDS, fieldOfView * 1.15f, lookAt: vent);
        }

        /// <summary>
        /// The land past the field (#1127): an orbit round the village, the church and its houses on their river, that comes
        /// round onto the far side of it from the cone, so it opens on the village against its woods and ranges and ends on the
        /// village with the cone smoking over it. The play camera never faces this way, so it is the one look a player gets
        /// at the land.
        /// </summary>
        internal static IntroShot Village(Func<float, float, float> ground, Vector3 vent, VolcanoSceneConfig volcano, float fieldOfView,
            Random random)
        {
            Vector2 cone = volcano.ConeCenter.ToVector2();
            Vector2 village = volcano.Country.Village.ToVector2();

            //Ending beyond the village from the cone, so the cone stands behind it; coming round from either side
            Vector2 away = village - cone;
            float end = MathF.Atan2(away.Y, away.X)
                + MathHelper.ToRadians(AridIntroPaths.Roll(random, -VILLAGE_END_OFF_DEGREES, VILLAGE_END_OFF_DEGREES));
            float sweep = MathHelper.ToRadians(AridIntroPaths.Roll(random, VILLAGE_SWEEP_MIN_DEGREES, VILLAGE_SWEEP_MAX_DEGREES))
                * (random.Next(2) == 0 ? 1f : -1f);

            Vector2[] plan = AridIntroPaths.Arc(village, end + sweep, end, VILLAGE_RADIUS_FROM, VILLAGE_RADIUS_TO);
            Vector3[] path = AridIntroPaths.Hug(plan, ground, VolcanoCountryPlanting.Tallest + TREE_AIR, VolcanoCountryPlanting.Reach,
                VILLAGE_SECONDS);

            //The look: from the village's roofs to a direction part of the way from them to the column over the cone, as seen
            //from the lens's last point, at the village's distance from it; on that bearing, tilted down no more than the cut
            //into the tour's leg allows (see VILLAGE_END_DOWN_DEGREES)
            Vector3 roofs = new(village.X, ground(village.X, village.Y) + VILLAGE_LOOK_RISE, village.Y);
            Vector3 summit = vent + new Vector3(0f, SUMMIT_OVER_VENT, 0f);
            Vector3 last = path[^1];
            Vector3 between = Vector3.Normalize(Vector3.Lerp(Vector3.Normalize(roofs - last), Vector3.Normalize(summit - last), VILLAGE_LOOK_SHARE));

            float lowest = -MathHelper.ToRadians(VILLAGE_END_DOWN_DEGREES);
            if (MathF.Asin(between.Y) < lowest)
            {
                Vector2 bearing = Vector2.Normalize(new Vector2(between.X, between.Z));
                between = new Vector3(bearing.X * MathF.Cos(lowest), MathF.Sin(lowest), bearing.Y * MathF.Cos(lowest));
            }

            Vector3 framed = last + between * Vector3.Distance(last, roofs);

            var look = new Vector3[path.Length];
            for (int i = 0; i < look.Length; i++) look[i] = Vector3.Lerp(roofs, framed, i / (float)(look.Length - 1));

            return new IntroShot("the village", path, VILLAGE_SECONDS, fieldOfView * VILLAGE_FOV, lookAtPath: look);
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
