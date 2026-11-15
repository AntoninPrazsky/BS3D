using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The desert's own shots for a chapter intro's prologue (#559): the erg round the island from high over the
    /// dunes, the pyramids (#1068), a beetle walking low over the sand and digging back in (#1110), a low run downwind that
    /// climbs a dune's windward slope and crests it over the slip face — cut together, and last the scorpion home
    /// (<see cref="DesertLanding"/>, #1110): a close view of a scorpion coming up out of the sand and walking, that takes off
    /// from it and lands on the gameplay pose. Where no flight home is clear, the scorpion is a shot of its own and the reel
    /// cuts to the tour's last leg after it.
    /// <para>
    /// The owner (#1110): "give them more time in the intro", the critters. The two close shots are cued, not waited for: each
    /// cues the scene's featured beetle or scorpion (<see cref="SceneRenderer.CueDesertCritter"/>) and its look follows the
    /// creature where its life, a pure function of the clock, says it is (<see cref="SceneRenderer.DesertFeatureAt"/>). The time
    /// is found in the shots round them: the flock shot (2.9 s) is gone, and the crest is 0.6 s shorter at the pace it had.
    /// </para>
    /// <para>
    /// ⚠ <b>The creature is re-cued on every frame of its shot</b> (<see cref="IntroShot.Track"/>), its onset counted back from
    /// how far into the shot the intro's clock is: the scene draws it on the wall clock, which a pause does not stop and a
    /// hitch does not hold to a frame's worth (<c>ChapterIntro.MAX_INTRO_STEP</c>), and the look is laid on the shot's own
    /// seconds. Cued once, on the shot's first frame, a pause or a hitch left the creature ahead of its look for the rest of the
    /// shot, the look on sand it had left or the beetle gone under before the cut. A pause still lets it walk on under the
    /// menu; the shot's next frame puts it back.
    /// </para>
    /// <para>
    /// <b>The dunes are read off <see cref="TerrainMirror.Desert"/></b>, the CPU mirror of <c>Desert.fx</c>'s
    /// own height field, so the crest run knows where the crests are and the lens clears each one by a stated
    /// height (<see cref="AridIntroPaths.Hug"/>). The dunes are not seeded by <c>sceneseed=</c>, so every roll here
    /// chooses among the same land. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class DesertIntroShots
    {
        //The erg: a slow arc round the arena this far out, dollying in and craning up over the dunes, the lens on
        //the island. The ground is cleared by at least WIDE_CLEARANCE wherever the arc crosses a crest.
        private const float WIDE_FROM_RADIUS = 250f;
        private const float WIDE_TO_RADIUS = 210f;
        private const float WIDE_SWEEP_RADIANS = 0.28f;
        private const float WIDE_FROM_HEIGHT = 32f;
        private const float WIDE_TO_HEIGHT = 56f;
        internal const float WIDE_CLEARANCE = 12f;
        private const float WIDE_SECONDS = 3.2f;

        //The crest: a run this long DOWNWIND — up a windward slope, over the knife edge, down the slip face — at
        //least this high over the highest sand at and beside the line, somewhere in the band of full dunes past the
        //clearing's ramp. Of CREST_CANDIDATES rolled runs the one with the most relief under it is taken, since a
        //run along an interdune flat is a shot of nothing. Pitched down a little so the sand is the frame.
        //
        //⚠ SLOW AND SMOOTH, NOT A ROLLER COASTER (#645). The first cut ran 95 units in 3.3 s (30 u/s) over a
        //five-point window: measured over sixteen rolls, the lens's vertical acceleration was 49 u/s² rms and up
        //to 450, and the view pitched 45 degrees a second rms and up to 159 — "jerky, and too fast, like a
        //roller coaster" in the owner's words. Now 61 units in 3.4 s (18 u/s), its height rounded like a drone's
        //flight (`AridIntroPaths.Hug`, #1109); 72 in 4.0 s until #1110 gave the critters the time.
        private const float CREST_RUN = 61f;
        internal const float CREST_CLEARANCE = 2.6f;
        private const float CREST_LATERAL = 2.5f;
        private const float CREST_INNER = 215f;
        private const float CREST_OUTER = 360f;
        private const int CREST_CANDIDATES = 48;
        private const float CREST_PITCH_DOWN_DEGREES = 7f;
        private const float CREST_PITCH_DAMPING = 0.6f;
        private const float CREST_SECONDS = 3.4f;

        //The scorpion (#966), where no scorpion home is clear: a low lens this far from the burrow and this high over the sand,
        //the burrow somewhere in the clearing at a radius between these, the lens easing in this much over the shot; the
        //scorpion comes up this long into it, walks across the frame with the look on it, and the shot holds it through its
        //walk and SCORPION_AFTER more (#1110: it cut away mid-step)
        internal const float SCORPION_STAND = 3.4f, SCORPION_CLEARANCE = 0.85f, SCORPION_PUSH = 0.7f;
        private const float SCORPION_INNER = 42f, SCORPION_OUTER = 70f;
        internal const float SCORPION_ONSET = 0.4f;
        private const float SCORPION_AFTER = 0.3f;
        internal static readonly float ScorpionSeconds = SCORPION_ONSET + SceneRenderer.DesertFeatureWalkEnds() + SCORPION_AFTER;

        //The beetle (#1110): the featured beetle cued so that it has come up and walked BEETLE_WALKED seconds by the shot's
        //first frame, its double line of prints behind it, then walks on, stops and digs back in nose first in a puff of sand,
        //the dig done as the shot cuts (SceneRenderer.DesertFeatureWalkEnds and DesertFeatureSeconds say when: its life ends on
        //the shot's last moment, which the re-cue every frame keeps it to, so neither it, its hole nor its prints go early).
        //Somewhere on the clearing's flat sand between BEETLE_INNER and BEETLE_OUTER, walking round the island; the lens on the
        //island's side of it, so it
        //looks out at the dunes, BEETLE_SIDE off its line and BEETLE_AHEAD ahead along it, BEETLE_LENS_ABOVE over the sand,
        //dollying with its pace smoothed over BEETLE_SMOOTH either side; the look BEETLE_LOOK_ABOVE over its foot, so it walks
        //in the lower third with the far dunes along the top edge; BEETLE_LENS of the game's lens. From 0.9 over the sand the
        //lens looks down on it at about the twenty degrees the references do, so its trail lies open behind it.
        internal const float BEETLE_SIDE = 2.2f, BEETLE_AHEAD = 0.5f, BEETLE_LENS_ABOVE = 0.9f, BEETLE_LOOK_ABOVE = 0.35f;
        private const float BEETLE_INNER = 40f, BEETLE_OUTER = 68f;
        internal const float BEETLE_WALKED = 1.2f, BEETLE_SECONDS = 3.6f, BEETLE_LENS = 0.7f;
        private const float BEETLE_SMOOTH = 0.6f;
        //The pyramids (#1068, the owner: "the shot of the pyramids is missing"): a run this far out from Khufu to this far,
        //low over the dunes, on the arena's side of the group and turned off the line to the arena by a roll in this range,
        //so the three stand staggered rather than one behind another; the lens between Khufu and Khafre, this share up
        //Khufu's height. The arena stays behind the lens: it is the far side of the whole run.
        private const float PYRAMIDS_FROM = 560f, PYRAMIDS_TO = 400f;
        internal const float PYRAMIDS_CLEARANCE = 7f;
        private const float PYRAMIDS_TURN_MIN = 0.3f, PYRAMIDS_TURN_MAX = 0.6f;
        private const float PYRAMIDS_LOOK_FRACTION = 0.3f;
        private const float PYRAMIDS_SECONDS = 4.2f;

        /// <summary>The prologue for the desert being drawn, or null when there is no desert config.</summary>
        /// <param name="clock">The game's wall clock, which the scene draws the beetle and the scorpion on and their onsets are
        /// counted from (<see cref="IntroContext.Clock"/>).</param>
        /// <param name="endPosition">The pose the intro hands the camera back at (<see cref="IntroContext.GamePosition"/>),
        /// which the scorpion home lands on.</param>
        /// <param name="endTarget">What that pose looks at.</param>
        public static IntroShot[] Build(SceneRenderer scenes, Func<float> clock, Vector3 endPosition, Vector3 endTarget, float fieldOfView,
            Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Desert) is not DesertSceneConfig desert) return null;
            return Build(desert, (at, yaw, onset, scorpion) => scenes.CueDesertCritter(at, yaw, (clock?.Invoke() ?? 0f) + onset, scorpion),
                endPosition, endTarget, fieldOfView, random);
        }

        /// <summary>
        /// The prologue off a config, with no renderer: what the tests fly. <paramref name="cue"/> cues the featured beetle or
        /// scorpion (where it comes up, its heading, its onset in seconds from the moment of the call, and whether it is the
        /// scorpion), on every frame of its shot (see the class's note), null for none. The erg, the pyramids, the beetle, the
        /// crest and the scorpion home where one is clear (#1110), which lands on the pose; where none is, the scorpion as a shot
        /// of its own last, and the tour's arrival leg after it.
        /// </summary>
        internal static IntroShot[] Build(DesertSceneConfig desert, Action<Vector2, float, float, bool> cue, Vector3 endPosition,
            Vector3 endTarget, float fieldOfView, Random random)
        {
            float Ground(float x, float z) => TerrainMirror.Desert(x, z, desert);

            //The erg, the pyramids, the scorpion and the crest rolled in the order they always were, the beetle's and the
            //landing's rolls after them
            IntroShot erg = Erg(Ground, fieldOfView, random);
            IntroShot pyramids = PyramidsShot(desert, Ground, fieldOfView, random);
            IntroShot scorpion = Scorpion(cue, desert, Ground, fieldOfView, random);
            IntroShot crest = Crest(desert, Ground, fieldOfView, random);
            IntroShot beetle = Beetle(cue, desert, Ground, fieldOfView, random);
            IntroShot home = DesertLanding.Build(desert, cue == null ? null : (at, yaw, onset) => cue(at, yaw, onset, true),
                endPosition, endTarget, fieldOfView, random, out _);

            //The two close shots apart, the crest between them: two low shots over the sand in a row read as one
            var shots = new System.Collections.Generic.List<IntroShot> { erg };
            if (pyramids != null) shots.Add(pyramids);
            shots.Add(beetle);
            shots.Add(crest);
            shots.Add(home ?? scorpion);
            return shots.ToArray();
        }

        /// <summary>
        /// A shot whose lens and look are given on its own seconds (<paramref name="lens"/> and <paramref name="look"/> at each
        /// second of it), as its creature's life is, <paramref name="onFrame"/> keeping the creature on them: the points are laid
        /// where the shot's eased clock shows them (<see cref="IntroShot.Uneased"/>), so the look is on the creature at every frame.
        /// </summary>
        private static IntroShot OnTheClock(string name, Func<float, Vector3> lens, Func<float, Vector3> look, float seconds,
            float fieldOfView, Action<float> onFrame)
        {
            var path = new Vector3[AridIntroPaths.POINTS];
            var looks = new Vector3[AridIntroPaths.POINTS];
            for (int k = 0; k < path.Length; k++)
            {
                float shown = IntroShot.Uneased(k / (float)(path.Length - 1)) * seconds;
                path[k] = lens(shown);
                looks[k] = look(shown);
            }

            return new IntroShot(name, path, seconds, fieldOfView, lookAtPath: looks, onFrame: onFrame);
        }

        /// <summary>
        /// A darkling beetle low over the sand (#1110, the owner's "give them more time in the intro"): the featured beetle cued
        /// to be walking already on the shot's first frame (<see cref="BEETLE_WALKED"/>), its trail of prints behind it, the lens
        /// beside it dollying with its pace, the look on it; it stops and digs back in nose first, the dig done as the shot cuts.
        /// </summary>
        internal static IntroShot Beetle(Action<Vector2, float, float, bool> cue, DesertSceneConfig desert, Func<float, float, float> ground,
            float fieldOfView, Random random)
        {
            Vector2 outward = AridIntroPaths.Bearing(AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi));
            Vector2 middle = outward * AridIntroPaths.Roll(random, BEETLE_INNER, BEETLE_OUTER);
            Vector2 heading = new Vector2(-outward.Y, outward.X) * (random.Next(2) == 0 ? 1f : -1f);
            float yaw = MathF.Atan2(heading.X, heading.Y);

            //Where it comes up: the shot's half of its walk centred on the middle
            float onset = -(SceneRenderer.DESERT_FEATURE_EMERGE_SECONDS + BEETLE_WALKED);
            float Along(float seconds) =>
                Vector2.Distance(Flat(SceneRenderer.DesertFeatureAt(Vector2.Zero, yaw, seconds - onset, desert, scorpion: false)), Vector2.Zero);
            Vector2 start = middle - heading * (0.5f * (Along(0f) + Along(BEETLE_SECONDS)));

            Vector3 Foot(float seconds) => SceneRenderer.DesertFeatureAt(start, yaw, seconds - onset, desert, scorpion: false);

            //The lens's way along its line: its pace averaged over BEETLE_SMOOTH either side, so the lens rides through its
            //stops and starts and slows to rest as it stops
            float Smoothed(float seconds)
            {
                const int TAPS = 12;
                float sum = 0f;
                for (int k = 0; k <= TAPS; k++) sum += Along(seconds + BEETLE_SMOOTH * (2f * k / TAPS - 1f));
                return sum / (TAPS + 1);
            }

            Vector3 Lens(float seconds)
            {
                Vector2 at = start + heading * (Smoothed(seconds) + BEETLE_AHEAD) - outward * BEETLE_SIDE;
                return new Vector3(at.X, ground(at.X, at.Y) + BEETLE_LENS_ABOVE, at.Y);
            }

            return OnTheClock("the beetle", Lens, seconds => Foot(seconds) + Vector3.Up * BEETLE_LOOK_ABOVE, BEETLE_SECONDS,
                fieldOfView * BEETLE_LENS, cue == null ? null : seconds => cue(start, yaw, onset - seconds, false));
        }

        /// <summary>
        /// The Giza group from the sand (#1068): low over the dunes towards Khufu from the arena's side, turned off the line to
        /// the arena so Khafre stands beside Khufu rather than behind it, the lens a little up their faces.
        /// </summary>
        internal static IntroShot PyramidsShot(DesertSceneConfig desert, Func<float, float, float> ground, float fieldOfView, Random random)
        {
            var placement = TerrainMirror.DesertPyramids(desert);
            if (placement.Count < 2) return null;

            Vector3 khufu = placement[0].Centre, khafre = placement[1].Centre;
            var foot = new Vector2(khufu.X, khufu.Z);
            Vector2 towardsArena = foot.LengthSquared() > 1e-4f ? -Vector2.Normalize(foot) : Vector2.UnitX;
            float side = random.Next(2) == 0 ? 1f : -1f;
            Vector2 outward = AridIntroPaths.Bearing(MathF.Atan2(towardsArena.Y, towardsArena.X)
                + side * AridIntroPaths.Roll(random, PYRAMIDS_TURN_MIN, PYRAMIDS_TURN_MAX));

            Vector2 from = foot + outward * PYRAMIDS_FROM, to = foot + outward * PYRAMIDS_TO;
            Vector3[] path = AridIntroPaths.Hug(AridIntroPaths.Line(from, to), ground, PYRAMIDS_CLEARANCE, 3f, PYRAMIDS_SECONDS);

            Vector3 between = Vector3.Lerp(khufu, khafre, 0.35f);
            var lookAt = new Vector3(between.X, khufu.Y + placement[0].Height * PYRAMIDS_LOOK_FRACTION, between.Z);
            return new IntroShot("the pyramids", path, PYRAMIDS_SECONDS, fieldOfView, lookAt: lookAt);
        }

        /// <summary>
        /// A scorpion coming up out of the sand (#966, the owner's "players must feel: wow"): the lens low on the sand a few
        /// lengths off a burrow in the clearing, easing in, and the scorpion cued to come up nose first in a puff of sand the
        /// moment the shot is on screen (<see cref="SceneRenderer.CueDesertCritter"/>), then to walk across the frame, the look
        /// following it through its walk (#1110). Since #1110 only where no scorpion home is clear: that one opens on the same
        /// close view and takes off from it.
        /// </summary>
        internal static IntroShot Scorpion(Action<Vector2, float, float, bool> cue, DesertSceneConfig desert, Func<float, float, float> ground,
            float fieldOfView, Random random)
        {
            float bearing = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            Vector2 burrow = AridIntroPaths.Bearing(bearing) * AridIntroPaths.Roll(random, SCORPION_INNER, SCORPION_OUTER);
            float view = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            Vector2 back = AridIntroPaths.Bearing(view);
            Vector2 from = burrow + back * SCORPION_STAND, to = burrow + back * (SCORPION_STAND - SCORPION_PUSH);
            //Across the frame: square to the lens's line, to one side or the other. Yaw runs from +Z towards +X
            Vector2 across = new Vector2(-back.Y, back.X) * (random.Next(2) == 0 ? 1f : -1f);
            float yaw = MathF.Atan2(across.X, across.Y);
            //The burrow a little back from the middle of the frame, so the walk crosses it
            Vector2 start = burrow - across * 0.6f;
            float seconds = ScorpionSeconds;

            Vector3 Lens(float shown)
            {
                float s = shown / seconds;
                Vector2 at = Vector2.Lerp(from, to, s);
                return new Vector3(at.X, ground(at.X, at.Y) + SCORPION_CLEARANCE * (1f - 0.1f * s), at.Y);
            }

            return OnTheClock("the scorpion", Lens, shown => ScorpionAt(start, yaw, shown, desert), seconds, fieldOfView * 0.75f,
                cue == null ? null : seconds => cue(start, yaw, SCORPION_ONSET - seconds, true));
        }

        /// <summary>Where the look holds the scorpion cued at <paramref name="start"/> along <paramref name="yaw"/>,
        /// <paramref name="shown"/> seconds into its shot: over its foot, at its burrow before it comes up.</summary>
        internal static Vector3 ScorpionAt(Vector2 start, float yaw, float shown, DesertSceneConfig desert) =>
            SceneRenderer.DesertFeatureAt(start, yaw, shown - SCORPION_ONSET, desert) + Vector3.Up * DesertLanding.LOOK_ABOVE;

        /// <summary>The establishing view: over the dunes looking back at the island standing in its clearing.</summary>
        internal static IntroShot Erg(Func<float, float, float> ground, float fieldOfView, Random random)
        {
            float from = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
            float sign = random.Next(2) == 0 ? 1f : -1f;
            Vector2[] plan = AridIntroPaths.Arc(Vector2.Zero, from, from + sign * WIDE_SWEEP_RADIANS, WIDE_FROM_RADIUS, WIDE_TO_RADIUS);

            //The crane is the designed height, its foot and top flown as laid and its smoothstep rounded
            //(AridIntroPaths.Hug); the dunes can only lift it.
            float level = ground(0f, 0f);
            Vector3[] path = AridIntroPaths.Hug(plan, ground, WIDE_CLEARANCE, 4f, WIDE_SECONDS,
                i => level + Crane(i, plan.Length, WIDE_FROM_HEIGHT, WIDE_TO_HEIGHT));

            return new IntroShot("the erg", path, WIDE_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(0f, ArenaIsland.TOP_Y, 0f));
        }

        /// <summary>
        /// Low and downwind over the dunes: the lens climbs a windward slope and tips over the crest, so the next
        /// dune's slip face opens under it. Of a few dozen runs rolled in the band of full dunes, the one with the
        /// most relief under it.
        /// </summary>
        internal static IntroShot Crest(DesertSceneConfig desert, Func<float, float, float> ground, float fieldOfView, Random random)
        {
            Vector2 wind = TerrainMirror.DesertWind(desert);

            float bestRelief = -1f;
            Vector2 bestFrom = Vector2.Zero, bestTo = Vector2.Zero;

            for (int attempt = 0; attempt < CREST_CANDIDATES; attempt++)
            {
                float bearing = AridIntroPaths.Roll(random, 0f, MathHelper.TwoPi);
                Vector2 middle = AridIntroPaths.Bearing(bearing) * AridIntroPaths.Roll(random, CREST_INNER + CREST_RUN * 0.5f, CREST_OUTER - CREST_RUN * 0.5f);
                Vector2 from = middle - wind * (CREST_RUN * 0.5f);
                Vector2 to = middle + wind * (CREST_RUN * 0.5f);

                //Both ends in the band, so the run never drops into the clearing's ramp.
                if (from.Length() < CREST_INNER || to.Length() < CREST_INNER) continue;

                float low = float.MaxValue, high = float.MinValue;
                for (int k = 0; k <= 16; k++)
                {
                    Vector2 at = Vector2.Lerp(from, to, k / 16f);
                    float h = ground(at.X, at.Y);
                    low = MathF.Min(low, h);
                    high = MathF.Max(high, h);
                }

                if (high - low <= bestRelief) continue;

                bestRelief = high - low;
                bestFrom = from;
                bestTo = to;
            }

            //Every candidate refused only if the band is thinner than the run, which the shipped config is not;
            //then the run goes straight downwind through the band's middle.
            if (bestRelief < 0f)
            {
                Vector2 across = new(-wind.Y, wind.X);
                bestFrom = across * ((CREST_INNER + CREST_OUTER) * 0.5f) - wind * (CREST_RUN * 0.5f);
                bestTo = bestFrom + wind * CREST_RUN;
            }

            Vector3[] path = AridIntroPaths.Hug(AridIntroPaths.Line(bestFrom, bestTo), ground, CREST_CLEARANCE, CREST_LATERAL, CREST_SECONDS);

            return new IntroShot("the crest", path, CREST_SECONDS, fieldOfView * 1.15f,
                lookAhead: 16f, pitchDownDegrees: CREST_PITCH_DOWN_DEGREES, pitchDamping: CREST_PITCH_DAMPING);
        }

        private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

        //A crane's height at point i: smoothstepped between the two.
        private static float Crane(int i, int count, float from, float to)
        {
            float s = i / (float)(count - 1);
            return MathHelper.Lerp(from, to, s * s * (3f - 2f * s));
        }
    }
}
