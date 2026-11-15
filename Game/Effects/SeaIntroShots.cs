using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The sea's own shots for a chapter intro's prologue (#559): a low pass round the island's flank at the
    /// waterline, a drift under the water looking up at the island, with a school of fish wheeling round the
    /// funnel's cone between the lens and the stone (#759, #760), a run low over the open sea in at the nearer oil
    /// platform (#1124), and a flight low over the water that climbs to the sun the glint comes from (#652) — cut
    /// together, then cut to the tour's last leg. What the sea builds is the swell (<c>Sea.fx</c>'s twenty Gerstner
    /// waves), the glint the sun lays across it, the one rock in it, which is the island, and the two platforms far out
    /// (<see cref="OilRigLayout"/>), its one landmark.
    /// <para>
    /// <b>It opened on the island from far out and high, and the owner threw that shot out (#652):</b> "the high,
    /// distant view where the island is small in the middle doesn't work — an empty, boring view". One rock and a
    /// lot of sea from 235 units is exactly what the scene is, and nothing in it to look at. The island is now the
    /// first shot's subject from close at the waterline, and the far view is replaced by the one the owner asked
    /// for: skimming the waves through the glitter, then pitching up and rising until the lens is on the sun, so the
    /// viewer sees the glitter <i>come from</i> it.
    /// </para>
    /// <para>
    /// <b>The second shot was a skim over the swell into the glint, and the owner read the cut out of it as a glitch
    /// (#759):</b> "the two scenes separated by a cut are almost identical, so the cut feels more like a jerk". Both ran
    /// a straight line along the same heading into the sun at the lens's floor — the swell's 4.2 and the climb's 3.6
    /// were both clamped up to 4.18, 3° down at 1.15 and 1.2 of the lens — and the only difference was a jump back
    /// along the line and to one side, over featureless swell. His answer (4 Oct) was the shot the sea lacked: under
    /// the water, with the fish (#760). It is a different place, not a different number, so the cut carries the
    /// viewer somewhere instead of stuttering.
    /// </para>
    /// <para>
    /// <b>No shot looked at the platforms (#1124, the owner: "in the intro the camera does not deal with the oil
    /// platforms").</b> The nearer stands 820 out, past the water's calm, and cannot come nearer: its legs are cut at a
    /// flat waterline. So the lens goes out to it: a cut may put the lens anywhere, and the run starts already
    /// <see cref="RIG_FROM"/> off the rig, beyond it, coming in over the open sea with the island on the horizon behind
    /// it, the rig growing until it towers over the lens. It plays between the depths and the climb, so a low run over
    /// the water is never next to another (the island's flank is one, the climb starts as one): the depths cut to it from
    /// under the water, and its last look, up the derrick, cuts to the climb's first, level over the glint. The tour paid
    /// for it out of the other three (the island 3.0 to 2.8 s, the depths 3.2 to 3.0, the climb 4.6 to 4.0), so it grew
    /// by 2.6 s and not 3.6.
    /// </para>
    /// <para>
    /// <b>The water is the only ground, and it is bounded rather than mirrored.</b> No crest stands higher than
    /// <see cref="CrestHeight"/> over the mean level, so a lens held <see cref="LENS_CLEARANCE"/> over that above
    /// <see cref="SeaSceneConfig.LevelY"/> is never under water. The bound was the six waves' weights summed (2.92 of
    /// <see cref="SeaSceneConfig.WaveAmplitude"/>), every crest at once; #674's twenty sum to 5.0, and twenty waves at
    /// independent phases never come near that — over eight million points at random times the highest stood at 3.93 —
    /// so the bound is <see cref="CREST_REACH"/> above what was seen, and the clearance is what keeps the shots where
    /// #652 framed them (the flank at its own height, the climb starting 0.27 higher than it did). The depths are the
    /// one shot under the water, and stand far enough under it that no trough reaches the lens. The run at the rig keeps
    /// the same bound, though the water out there is flat (<see cref="SeaSceneConfig.CalmFrom"/>).
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
        private const float CLIMB_SECONDS = 4.0f;

        //With no sun to climb to (a dome that gives none), the elevation the climb ends at
        private const float CLIMB_FALLBACK_ELEVATION_DEGREES = 32f;

        //The depths (#759, #760): an arc round under the island, from this far out from its axis to this far, from
        //this far under the mean level up to this far, the look on the island's underside a little over the funnel's
        //tip. From under the school's ring (FishSchool: round the cone, a couple of units over its tip) the lens
        //looks up through the fish at the stone, the frame the references drew (C:\Users\panrd\AI\sd\out\760-klein):
        //looking up is also what keeps the lower murk, a flat pale plain under the water, out of the frame.
        private const float DEPTHS_RADIUS_FROM = 26f;
        private const float DEPTHS_RADIUS_TO = 22f;
        private const float DEPTHS_UNDER_FROM = 24f;
        private const float DEPTHS_UNDER_TO = 20f;
        private const float DEPTHS_SWEEP_RADIANS = 0.55f;
        private const float DEPTHS_LOOK_Y = ArenaIsland.FUNNEL_BOTTOM_Y + 8f;
        private const float DEPTHS_SECONDS = 3.0f;

        //The island: an arc round its flank at this radius (the rim is at ArenaIsland.RADIUS, 26), this low
        //over the water, the lens on the island's side a little under its rim, so the water meets the rock
        //across the frame and the rim and the hanging field stand over it.
        private const float FLANK_RADIUS = 47f;
        private const float FLANK_HEIGHT = 4.6f;
        private const float FLANK_SWEEP_RADIANS = 0.62f;
        private const float FLANK_SECONDS = 2.8f;

        //The rig (#1124): a straight run low over the water in at the nearer platform from the open sea beyond it, coming in
        //this many degrees round from the line through the rig to the arena, either way, from this far off the rig's axis to
        //this far, where the rig stands this many degrees off the run's heading: the run passes it by rather than flying at
        //it, so it never comes nearer the axis than RIG_TO * sin(RIG_PASS), well outside the legs. The run is turned off the
        //line one way and passes the rig on the other hand, so the island stands on the horizon beside the rig at the start
        //and crosses behind it. In from beyond and not out from the arena's side: looking out, the water would end some 800
        //past the rig, where the far ring does. The look climbs from the jacket's middle (this share of the deck's height) to
        //the derrick's axis at the deck, so the rig grows from the horizon and at the end stands over the lens from the
        //waterline to the derrick's cap.
        //⚠ THE END STANDS OFF. From the waterline the deck and the modules on it hide the derrick's foot from anywhere, and
        //more of it the nearer the lens: ended 70 off the axis the lens was under the deck's edge and saw none of it, only the
        //deck's dark underside and the legs; at 100, the cap. At 140 the upper half of it stands over the deck.
        private const float RIG_FROM = 300f;
        private const float RIG_TO = 140f;
        private const float RIG_APPROACH_MIN_DEGREES = 10f;
        private const float RIG_APPROACH_MAX_DEGREES = 25f;
        private const float RIG_PASS_DEGREES = 75f;
        private const float RIG_LOOK_FROM = 0.5f;
        private const float RIG_SECONDS = 3.6f;

        /// <summary>The highest a crest can stand over the mean level, in units of the swell's amplitude, plus the chop's.</summary>
        internal static float CrestHeight(SeaSceneConfig sea) => sea.WaveAmplitude * CREST_REACH + sea.ChopAmplitude;

    //How high the swell reaches, in its own amplitude: 3.93 seen over eight million samples of Sea.fx's twenty waves (their
    //weights sum to 5.0, a crest of every wave at once that independent phases never approach). Re-measure on a new set.
    private const float CREST_REACH = 4.2f;

    //How far over the highest crest a lens is held
    private const float LENS_CLEARANCE = 1.5f;

        /// <summary>
        /// The prologue for the sea being drawn, or null when there is no sea config. <paramref name="sunDirection"/>
        /// is the direction towards the sun the dome lights the scene from (the host's rig), or null.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, Vector3? sunDirection, float fieldOfView, Random random) =>
            scenes?.GetSceneConfig(SceneKind.Sea) is SeaSceneConfig sea ? Shots(sea, sunDirection, fieldOfView, random) : null;

        /// <summary>The prologue over <paramref name="sea"/>'s figures: no device in it, so the tests build it as the game does.</summary>
        internal static IntroShot[] Shots(SeaSceneConfig sea, Vector3? sunDirection, float fieldOfView, Random random)
        {
            //Into the sun, where there is one to go into; otherwise a rolled heading.
            Vector2 sun = sunDirection is Vector3 s ? new Vector2(s.X, s.Z) : Vector2.Zero;
            Vector2 heading = sun.LengthSquared() > 0.01f
                ? Vector2.Normalize(sun)
                : AridIntroPaths.Bearing(AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi));

            //The sun's height above the horizon, for the climb's last look
            float elevation = sunDirection is Vector3 toSun && toSun.LengthSquared() > 1e-6f
                ? MathF.Asin(MathHelper.Clamp(Vector3.Normalize(toSun).Y, -1f, 1f))
                : MathHelper.ToRadians(CLIMB_FALLBACK_ELEVATION_DEGREES);

            //Rolled in the order the three were before the rig came (#1124), so a seed= flies the island, the depths and the
            //climb it always did
            IntroShot flank = Flank(sea, fieldOfView, random);
            IntroShot depths = Depths(sea, fieldOfView, random);
            IntroShot climb = Climb(sea, heading, elevation, fieldOfView, random);
            IntroShot rig = Rig(sea, OilRigLayout.Near(sea.LevelY), fieldOfView, random);

            return new[] { flank, depths, rig, climb };
        }

        /// <summary>
        /// The rig (#1124): a run low over the open sea in at the nearer platform from beyond it, the look on the jacket while
        /// the rig grows on the horizon and climbing to the deck as the lens comes in and passes it, the rig over the lens.
        /// </summary>
        private static IntroShot Rig(SeaSceneConfig sea, OilRig rig, float fieldOfView, Random random)
        {
            Vector2 foot = new(rig.Foot.X, rig.Foot.Z);
            Vector2 inward = Vector2.Normalize(-foot);

            //Coming in round from the line to the arena by the approach, the rig on the other hand at the end
            float side = random.Next(2) == 0 ? 1f : -1f;
            Vector2 heading = Turned(inward, side * MathHelper.ToRadians(AridIntroPaths.Roll(random, RIG_APPROACH_MIN_DEGREES, RIG_APPROACH_MAX_DEGREES)));
            float pass = MathHelper.ToRadians(RIG_PASS_DEGREES);
            Vector2 end = foot - Turned(heading, -side * pass) * RIG_TO;

            //Back along the heading until the rig is RIG_FROM off
            float run = -RIG_TO * MathF.Cos(pass) + MathF.Sqrt(RIG_FROM * RIG_FROM - RIG_TO * RIG_TO * MathF.Sin(pass) * MathF.Sin(pass));
            Vector2[] plan = AridIntroPaths.Line(end - heading * run, end);

            float y = sea.LevelY + CrestHeight(sea) + LENS_CLEARANCE;
            Vector3 jacket = rig.Foot + Vector3.Up * (OilRigLayout.DECK_Y * RIG_LOOK_FROM * rig.Scale);
            Vector3 deck = rig.ToWorld(new Vector3(OilRigLayout.DERRICK_AXIS.X, OilRigLayout.DECK_Y, OilRigLayout.DERRICK_AXIS.Y));

            var path = new Vector3[plan.Length];
            var look = new Vector3[plan.Length];
            for (int i = 0; i < plan.Length; i++)
            {
                float s = i / (float)(plan.Length - 1);
                path[i] = new Vector3(plan[i].X, y, plan[i].Y);

                //On the jacket while the rig is far, up to the deck under the derrick over the second half
                float climb = MathHelper.Clamp((s - 0.35f) / 0.65f, 0f, 1f);
                look[i] = Vector3.Lerp(jacket, deck, climb * climb * (3f - 2f * climb));
            }

            return new IntroShot("the rig", path, RIG_SECONDS, fieldOfView * 1.2f, lookAtPath: look);
        }

        //A ground direction turned by an angle, from +X towards +Z as AridIntroPaths.Bearing counts
        private static Vector2 Turned(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
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
        /// Under the water (#759, #760): an arc round under the island that rises a little as it closes in, looking up at
        /// the stone's underside and the funnel's cone, the school wheeling between.
        /// </summary>
        private static IntroShot Depths(SeaSceneConfig sea, float fieldOfView, Random random)
        {
            float from = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            float sign = random.Next(2) == 0 ? 1f : -1f;

            Vector2[] plan = AridIntroPaths.Arc(Vector2.Zero, from, from + sign * DEPTHS_SWEEP_RADIANS, DEPTHS_RADIUS_FROM, DEPTHS_RADIUS_TO);
            var path = new Vector3[plan.Length];
            for (int i = 0; i < plan.Length; i++)
            {
                float s = i / (float)(plan.Length - 1);
                float under = MathHelper.Lerp(DEPTHS_UNDER_FROM, DEPTHS_UNDER_TO, s * s * (3f - 2f * s));
                path[i] = new Vector3(plan[i].X, sea.LevelY - under, plan[i].Y);
            }

            return new IntroShot("the depths", path, DEPTHS_SECONDS, fieldOfView * 1.15f,
                lookAt: new Vector3(0f, DEPTHS_LOOK_Y, 0f));
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
