using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The scorpion home</b> (#1110): the last shot of the desert's chapter intro. It opens as the close view the scorpion
    /// shot was (#966): the lens low on the sand a few lengths off, the featured scorpion cued to come up out of its burrow the
    /// moment the shot is on screen and to walk across the frame (<see cref="SceneRenderer.CueDesertCritter"/>), the look
    /// following it. Then it takes off from there: the lens lifts off the sand past the scorpion, the look lets go of it and
    /// rises to the island ahead, and the flight settles onto the gameplay pose behind the gun, so the player is carried from
    /// the critter into the game without a cut, as the cities', the meadow's and the big top's are. The owner: "the final
    /// animation of the intro could also take off from this detailed view of the beetles and scorpions into the game position
    /// behind the cannon".
    /// <para>
    /// <b>Where it stands.</b> The close view is laid out from the pose: on the far side of it from the arena, out on the
    /// clearing's flat sand, so the take-off flies towards what the pose looks at, the island ahead of it the whole way, and
    /// arrives down the pose's own facing or near it (<see cref="IntroLanding.Arrival"/>). No circle round the arena is
    /// needed, whatever the pose's bearing, since the scorpion can come up anywhere. The scorpion walks to one side of the
    /// flight's line and away from it, square to the view, so the lens passes beside it rather than over it, and the island
    /// stands beyond it, off the frame's middle.
    /// </para>
    /// <para>
    /// <b>The pace.</b> A creep towards the pose while the scorpion comes up and walks, as the old shot's push in was; the take
    /// off easing up to a cruise over <see cref="RAMP_SECONDS"/>, climbing off the sand to a little over the pose's height,
    /// then the shared deceleration to rest on it (<see cref="IntroLanding.Fly"/>). The look is the shared landing's
    /// <see cref="IntroLanding.Subject"/>: on the scorpion until <see cref="LOOK_HOLD"/>, then turning off it onto the travel's.
    /// </para>
    /// <para>
    /// <b>Found, not assumed.</b> Rolled candidates (how far behind the pose and how far round it, how far off the line the
    /// scorpion stands and which side, the cruise, the climb, the height, which way the lens arrives) are each laid in full
    /// and refused if any point is under <see cref="LOWEST"/> over the sand, near the scorpion's walk or nearer the axis than
    /// the pose (<see cref="IntroLanding.KeepOutFor"/>), or if the close view does not stand on the clearing's flat sand. Null
    /// when none is clear, or for a pose inside the island's own radius (the menu's crane), and the desert then plays the reel
    /// it had before: the scorpion as a shot of its own, and the tour's arrival leg after the last one.
    /// </para>
    /// </summary>
    internal static class DesertLanding
    {
        /// <summary>The shot's name, in the intro's log line.</summary>
        internal const string NAME = "the scorpion home";

        /// <summary>The close view (#966's scorpion shot): the lens this far off the middle of the scorpion's walk, this high over
        /// the sand, the look this high over its foot.</summary>
        internal const float STAND = 3.4f, LENS_ABOVE = 0.85f, LOOK_ABOVE = 0.25f;

        /// <summary>How long into the shot the scorpion comes up (cued on the shot's first frame).</summary>
        internal const float ONSET = 0.4f;

        /// <summary>When the lens takes off, seconds into the shot on the wall clock, and when the look starts to turn off the
        /// scorpion and how long it takes. The look lets go a little before the lens lifts: a lens climbing a few units off a
        /// subject it still looks at pitches the view far faster than a drone's gimbal turns.</summary>
        internal const float LIFT = 4.4f, LOOK_HOLD = 4.4f, LOOK_RELEASE = 2.6f;

        //Where the close view stands: behind the pose, this far out past it, turned up to SPREAD degrees off the pose's own
        //line; and how far off the flight's line the scorpion's walk stands, degrees, one side or the other
        private const float DISTANCE_MIN = 22f, DISTANCE_MAX = 34f;
        private const float SPREAD = 30f;
        private const float VIEW_OFF_MIN = 12f, VIEW_OFF_MAX = 24f;

        //The hold: the lens creeps this far towards the pose until LIFT
        private const float CREEP = 0.6f;

        //The take-off, laid on the shot's clock: the pace eases from the creep's to a cruise over RAMP_SECONDS, the lens climbing
        //from the sand to the pose's height and CRUISE_OVER more from the lift to the deceleration's start (at least CLIMB_SECONDS),
        //then settling onto the pose's height while the shared landing decelerates over at least SETTLE_SECONDS. Both ease in
        //and out to the second derivative (a smootherstep): a smoothstep's acceleration steps at its ends, and the climb on one
        //jerked the lens at 526 units a second cubed in one frame (the drone limit is 150, IntroShotSmoothnessTests)
        internal const float RAMP_SECONDS = 2f, CLIMB_SECONDS = 2.2f;
        private const float CRUISE_MIN = 8f, CRUISE_MAX = 11f;
        private const float CRUISE_OVER_MIN = 0.5f, CRUISE_OVER_MAX = 2.5f;
        private const float SETTLE_SECONDS = 2.4f;

        //How long the flight should be: among clear candidates the one nearest it is preferred
        private const float TARGET_SECONDS = 9f;

        //No tighter than this off the creep's line onto the pose
        private const float TURN_RADIUS = 8f;

        //The plan's spacing, the step the take-off is laid at on the shot's clock, and the candidates rolled
        private const float STEP = 0.1f;
        private const float DT = 1f / 60f;
        private const int CANDIDATES = 48;

        //Clearances: LOWEST over the sand; CRITTER_AIR off the scorpion's walk, its line raised by CRITTER_HEIGHT (its tail's
        //top at SceneRenderer's 0.95 a length); never nearer the axis than the pose less ARRIVAL_SLACK; the close view and the
        //walk no further out than CLEARING_SHARE of the clearing's flat radius and no nearer the island than WALK_CLEAR past it
        internal const float LOWEST = 0.6f;
        internal const float CRITTER_AIR = 1.2f, CRITTER_HEIGHT = 0.6f;
        private const float ARRIVAL_SLACK = 0.5f;
        private const float CLEARING_SHARE = 0.9f;
        private const float WALK_CLEAR = 6f;

        //How far outside the island's radius the pose must stand for a landing at all: the game's lens stands 28 to 45 out
        private const float ISLAND_CLEAR = 2f;

        /// <summary>How the scorpion home is timed, pointed and banked once laid (<see cref="IntroLanding.Fly"/>): the old
        /// scorpion shot's narrow lens, easing to the game's; the meadow's settle; the look on the island ahead turned half way
        /// in and raised half way to the pose's target, following little of the lens's own climb off the sand (0.3 of it, the
        /// meadow's, tipped the view up and back down at 54 degrees a second squared as the climb began); a light bank.</summary>
        internal static readonly IntroLanding.Style Landing = new(WideLens: 0.75f, ReleaseTimeScale: 2f, LookPitchFollow: 0.1f,
            LookDistance: 30f, LookSmoothSeconds: 0.3f, LookLandSeconds: 2.2f, LookInward: 0.5f, RollPerTurn: 0.1f,
            MaxRollDegrees: 6f, RollSmoothSeconds: 0.25f, Points: 240, LookRise: 0.5f);

        /// <summary>Where the featured scorpion comes up and which way it walks.</summary>
        internal readonly record struct Walk(Vector2 Start, float Yaw);

        /// <summary>
        /// The scorpion home over <paramref name="desert"/>, landing on <paramref name="endPosition"/> looking at
        /// <paramref name="endTarget"/> with the game's <paramref name="fieldOfView"/>; null when no rolled route is clear.
        /// <paramref name="cue"/> is called on the shot's first frame with where the scorpion comes up and its heading (the
        /// scene's <see cref="SceneRenderer.CueDesertCritter"/>, <see cref="ONSET"/> on); <paramref name="walk"/> says the same.
        /// </summary>
        public static IntroShot Build(DesertSceneConfig desert, Action<Vector2, float> cue, Vector3 endPosition, Vector3 endTarget,
            float fieldOfView, Random random, out Walk walk)
        {
            walk = default;
            if (desert == null) return null;

            Vector2 end = new(endPosition.X, endPosition.Z);
            if (end.Length() < ArenaIsland.RADIUS + ISLAND_CLEAR) return null;

            Vector2 facing = new(endTarget.X - endPosition.X, endTarget.Z - endPosition.Z);
            if (facing.LengthSquared() < 1e-6f) return null;
            facing.Normalize();

            float keepOut = IntroLanding.KeepOutFor(endPosition, float.MaxValue, ARRIVAL_SLACK);
            var walks = new Dictionary<IntroLanding.Route, Walk>();
            IntroLanding.Route best = IntroLanding.Best(CANDIDATES, () =>
            {
                IntroLanding.Route route = Lay(desert, endPosition, facing, random, out Walk laid);
                if (route != null) walks[route] = laid;
                return route;
            }, p => Clear(desert, keepOut, p));
            if (best == null) return null;

            Walk chosen = walk = walks[best];
            var subject = new IntroLanding.Subject(t => Scorpion(chosen, t, desert), LOOK_HOLD, LOOK_RELEASE);
            return IntroLanding.Fly(NAME, best, endPosition, endTarget, fieldOfView, Landing, subject,
                cue == null ? null : () => cue(chosen.Start, chosen.Yaw));
        }

        /// <summary>The point the look holds at <paramref name="seconds"/> into the shot: over the scorpion's foot, held at its
        /// burrow before it comes up (<see cref="SceneRenderer.DesertFeatureAt"/>).</summary>
        internal static Vector3 Scorpion(Walk walk, float seconds, DesertSceneConfig desert) =>
            SceneRenderer.DesertFeatureAt(walk.Start, walk.Yaw, seconds - ONSET, desert) + Vector3.Up * LOOK_ABOVE;

        /// <summary>Whether a lens at <paramref name="p"/> is clear of the sand and the arena (see the class's note); the
        /// scorpion's walk is held by <see cref="Lay"/>, which knows it (<see cref="ClearOfWalk"/>).</summary>
        internal static bool Clear(DesertSceneConfig desert, float keepOut, Vector3 p) =>
            new Vector2(p.X, p.Z).Length() >= keepOut && p.Y >= TerrainMirror.Desert(p.X, p.Z, desert) + LOWEST;

        /// <summary>Whether a lens at <paramref name="p"/> keeps <see cref="CRITTER_AIR"/> off the scorpion anywhere on its walk
        /// from <paramref name="start"/> to <paramref name="finish"/> (the line raised by half its height, the air widened by the
        /// other half). It never binds on the routes laid (the lens is climbing by the time it passes the walk's line), so it
        /// is held by a test of its own.</summary>
        internal static bool ClearOfWalk(Vector3 p, Vector2 start, Vector2 finish, DesertSceneConfig desert)
        {
            Vector3 from = new(start.X, TerrainMirror.Desert(start.X, start.Y, desert) + CRITTER_HEIGHT * 0.5f, start.Y);
            Vector3 to = new(finish.X, TerrainMirror.Desert(finish.X, finish.Y, desert) + CRITTER_HEIGHT * 0.5f, finish.Y);
            return IntroPaths.DistanceToSegment(p, from, to) >= CRITTER_AIR + CRITTER_HEIGHT * 0.5f;
        }

        private static IntroLanding.Route Lay(DesertSceneConfig desert, Vector3 endPosition, Vector2 facing, Random random, out Walk walk)
        {
            walk = default;
            float distance = Roll(random, DISTANCE_MIN, DISTANCE_MAX);
            float spread = MathHelper.ToRadians(Roll(random, -SPREAD, SPREAD));
            float side = random.Next(2) == 0 ? 1f : -1f;
            float viewOff = MathHelper.ToRadians(Roll(random, VIEW_OFF_MIN, VIEW_OFF_MAX)) * side;
            float cruise = Roll(random, CRUISE_MIN, CRUISE_MAX);
            float cruiseOver = Roll(random, CRUISE_OVER_MIN, CRUISE_OVER_MAX);
            double arrival = random.NextDouble();

            //The close view: behind the pose, the lens heading for it (lead), the middle of the scorpion's walk off that line by
            //viewOff, the scorpion walking square to the view and away from the line
            Vector2 end = new(endPosition.X, endPosition.Z);
            Vector2 lead = Rotate(facing, spread);
            Vector2 lens = end - lead * distance;
            Vector2 view = Rotate(lead, viewOff);
            Vector2 middle = lens + view * STAND;
            Vector2 across = new Vector2(-view.Y, view.X) * side;

            float yaw = MathF.Atan2(across.X, across.Y);
            Vector2 walked = Flat(SceneRenderer.DesertFeatureAt(Vector2.Zero, yaw, SceneRenderer.DesertFeatureSeconds, desert));
            Vector2 start = middle - walked * 0.5f, finish = start + walked;

            float flat = desert.ClearingRadius * CLEARING_SHARE, nearest = ArenaIsland.RADIUS + WALK_CLEAR;
            foreach (Vector2 at in new[] { lens, start, finish })
                if (at.Length() > flat || at.Length() < nearest) return null;

            //The plan: the creep towards the pose, then the curve onto it, arriving along the pose's facing, halfway, or still
            //along the lead
            var plan = new List<Vector2>();
            IntroLanding.Straight(plan, lens, lens + lead * CREEP, STEP);
            int liftAt = plan.Count - 1;
            Vector2 arrive = IntroLanding.Arrival(arrival, facing, lead);
            IntroLanding.Release(plan, plan[^1], lead, end, arrive, STEP);
            if (!IntroLanding.NoTighterThan(plan, liftAt, TURN_RADIUS)) return null;

            var planLength = new float[plan.Count];
            for (int i = 1; i < plan.Count; i++) planLength[i] = planLength[i - 1] + Vector2.Distance(plan[i - 1], plan[i]);
            float total = planLength[^1];

            //Where the deceleration starts: SETTLE_SECONDS at the cruise (the shared landing's lasts ReleaseTimeScale times its
            //length over the speed it enters with)
            float settleLength = cruise * SETTLE_SECONDS / Landing.ReleaseTimeScale;
            float sSettle = total - settleLength;
            if (sSettle <= planLength[liftAt]) return null;

            //The take-off on the shot's clock, laid twice more: the lift is stated on the wall clock (the scorpion's), and the shot's
            //points are spaced on its eased one (IntroShot.Uneased), so how long the hold lasts on that clock depends on the
            //shot's length
            float hold = LIFT, seconds = 0f, entry = cruise;
            (List<float> S, List<float> T) march = default;
            for (int pass = 0; pass < 3; pass++)
            {
                march = March(planLength[liftAt], sSettle, hold, cruise, out entry);
                seconds = march.T[^1] + Landing.ReleaseTimeScale * settleLength / entry;
                hold = IntroShot.Eased(LIFT / seconds) * seconds;
            }

            //Long enough after the lift for the climb and the pace's ramp, so both are done before the deceleration
            float climbSeconds = march.T[^1] - hold;
            if (climbSeconds < MathF.Max(RAMP_SECONDS, CLIMB_SECONDS)) return null;

            //The heights: on the sand at the close view, climbing to the cruise's from the lift to the deceleration, onto the
            //pose's over it, each by when the lens is there
            float low = TerrainMirror.Desert(lens.X, lens.Y, desert) + LENS_ABOVE;
            float cruiseY = endPosition.Y + cruiseOver;
            var path = new List<Vector3>(march.S.Count + (int)(settleLength / STEP) + 2);
            var speed = new List<float>(path.Capacity);
            for (int j = 0; j < march.S.Count; j++)
            {
                Vector2 at = PlanAt(plan, planLength, march.S[j]);
                float y = low + (cruiseY - low) * Smoother((march.T[j] - hold) / climbSeconds);
                path.Add(new Vector3(at.X, y, at.Y));
                speed.Add(j == 0 ? march.S[1] / march.T[1] : Vector3.Distance(path[j - 1], path[j]) / (march.T[j] - march.T[j - 1]));
            }
            int settleFrom = path.Count - 1;

            //Over the deceleration (IntroLanding.Fly's: from the speed it enters with to rest on the pose), the heights at the
            //moments it reaches each point
            for (float s = sSettle + STEP; s < total; s += STEP)
            {
                Vector2 at = PlanAt(plan, planLength, s);
                float y = cruiseY + (endPosition.Y - cruiseY) * Smoother(Decelerated((s - sSettle) / settleLength));
                path.Add(new Vector3(at.X, y, at.Y));
                speed.Add(entry);
            }
            path.Add(endPosition);
            speed.Add(entry);

            //Off the scorpion's walk at every point
            foreach (Vector3 p in path)
                if (!ClearOfWalk(p, start, finish, desert)) return null;

            //The best is the length asked for, arriving along the pose's facing, the island behind the scorpion
            float swivel = MathF.Acos(MathHelper.Clamp(Vector2.Dot(arrive, facing), -1f, 1f));
            float offIsland = MathF.Acos(MathHelper.Clamp(Vector2.Dot(view, Vector2.Normalize(-lens)), -1f, 1f));
            walk = new Walk(start, yaw);
            return new IntroLanding.Route
            {
                Path = path, Speed = speed.ToArray(), DecelerateFrom = settleFrom,
                Score = -MathF.Abs(seconds - TARGET_SECONDS) - 2f * swivel - offIsland + (float)random.NextDouble() * 0.5f,
            };
        }

        /// <summary>
        /// The take-off on the shot's clock, a step of <see cref="DT"/> at a time: how far along the plan the lens is at each, the
        /// creep's pace until <paramref name="hold"/> (over <paramref name="creepLength"/>), then easing up to
        /// <paramref name="cruise"/> over <see cref="RAMP_SECONDS"/>, until the deceleration's start at <paramref name="sSettle"/>,
        /// where <paramref name="entry"/> is its pace.
        /// </summary>
        private static (List<float> S, List<float> T) March(float creepLength, float sSettle, float hold, float cruise, out float entry)
        {
            float creep = creepLength / hold;
            var s = new List<float> { 0f };
            var t = new List<float> { 0f };
            float at = 0f, now = 0f;
            while (at < sSettle)
            {
                float pace = Pace(now + DT * 0.5f - hold, creep, cruise);
                float step = pace * DT;
                if (at + step >= sSettle)
                {
                    now += (sSettle - at) / pace;
                    at = sSettle;
                }
                else
                {
                    now += DT;
                    at += step;
                }

                s.Add(at);
                t.Add(now);
            }

            entry = Pace(now - hold, creep, cruise);
            return (s, t);
        }

        //The pace `t` seconds after the lift: the creep's, easing up to the cruise's over RAMP_SECONDS
        private static float Pace(float t, float creep, float cruise) =>
            MathHelper.Lerp(creep, cruise, Smoother(t / RAMP_SECONDS));

        //An ease in and out to the second derivative, 0 to 1 over 0 to 1 and clamped
        private static float Smoother(float x)
        {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        //How much of its seconds the shared landing's deceleration has run when it has covered `share` of its length: its
        //distance is v0·T·h10(u) + L·h01(u) with v0·T = ReleaseTimeScale·L (IntroLanding.Fly), solved for u
        private static float Decelerated(float share)
        {
            float k = Landing.ReleaseTimeScale, lo = 0f, hi = 1f;
            share = MathHelper.Clamp(share, 0f, 1f);
            for (int i = 0; i < 32; i++)
            {
                float u = 0.5f * (lo + hi);
                float covered = k * (u * u * u - 2f * u * u + u) + (-2f * u * u * u + 3f * u * u);
                if (covered < share) lo = u; else hi = u;
            }

            return 0.5f * (lo + hi);
        }

        //The point `s` along a plan whose cumulative lengths are `length`
        private static Vector2 PlanAt(List<Vector2> plan, float[] length, float s)
        {
            if (s <= 0f) return plan[0];
            if (s >= length[^1]) return plan[^1];

            int lo = 0, hi = length.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (length[mid] <= s) lo = mid; else hi = mid;
            }

            float span = length[hi] - length[lo];
            return span > 1e-6f ? Vector2.Lerp(plan[lo], plan[hi], (s - length[lo]) / span) : plan[lo];
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

        private static float Roll(Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();
    }
}
