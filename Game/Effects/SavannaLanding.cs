using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The grass home</b> (#1108): the last shot of the savanna's chapter intro, a run low over the grass among the plain's
    /// trees in towards the island, between two of the campfires and up onto the gameplay pose behind the cannon, landing on
    /// it, so the player is carried from the plain into the game without a cut — as the meadow's brook home
    /// (<see cref="MeadowLanding"/>) and the cities' swing home (<see cref="CitySwing"/>) do. The owner: "a fly-through that
    /// starts low over the ground and ends at the position where the game camera stands behind the cannon. This is always the
    /// finale of the intro, because the game begins after it."
    /// <para>
    /// <b>The route</b>, one of two. <b>Straight in</b>: from a clear stand among the grass and the groves
    /// <see cref="START_RUN_MIN"/> to <see cref="START_RUN_MAX"/> from the pose, within <see cref="STRAIGHT_SPREAD"/> degrees of
    /// straight behind it, a short run heading for the pose give or take <see cref="HEADING_SPREAD"/> degrees and the shared
    /// release onto it (<see cref="IntroLanding.Release"/>), arriving along the pose's facing, halfway, or still along the run
    /// (<see cref="IntroLanding.Arrival"/>). <b>Round the island</b> (<see cref="ROUND_SHARE"/> of the rolls): from a clear stand
    /// <see cref="ROUND_FROM_MIN"/> to <see cref="ROUND_FROM_MAX"/> degrees round from the pose's bearing, in onto a circle
    /// outside the pose, round it, and the shared turn in down the pose's facing (<see cref="IntroLanding.TurnIn"/>), as the
    /// meadow's brook home goes round: the way onto a pose whose way straight in a grove closes. The lens flies
    /// <see cref="ABOVE_MIN"/> to <see cref="ABOVE_MAX"/> over the ground the way the arid shots do
    /// (<see cref="AridIntroPaths.Hug"/>: rounded over the run's own travel, never under that clearance), climbs onto the
    /// pose's height over <see cref="CLIMB_MIN"/> to <see cref="CLIMB_MAX"/> units, and comes over the bank round the island
    /// level with the pose (<see cref="ARRIVE_LEVEL"/>).
    /// </para>
    /// <para>
    /// <b>The pace.</b> A cruise of <see cref="CRUISE_MIN"/> to <see cref="CRUISE_MAX"/> units a second straight in and up to
    /// <see cref="CRUISE_ROUND_MAX"/> round the island, which low over the grass reads fast (the owner prefers faster to
    /// slower; the arid shots that look at the ground under the lens are held to 28), then the shared deceleration to rest on
    /// the pose over at least <see cref="SETTLE_SECONDS"/>, and round the island over the whole turn in; the look along the
    /// travel turned half way in towards the arena (<see cref="IntroLanding.Style.LookInward"/>), so the island stands ahead;
    /// a light bank. No longer than <see cref="LONGEST_SECONDS"/>.
    /// </para>
    /// <para>
    /// <b>Found, not assumed.</b> Rolled candidates are each laid in full and refused if they turn tighter than the speed
    /// allows, if the cut from the shot before moves the lens too little, or if any point is under the ground or the bank,
    /// nearer the axis than the pose (<see cref="IntroLanding.KeepOutFor"/>), within <see cref="MARGIN"/> of anything planted,
    /// held as its <see cref="PlantFigure"/> (every instance of every mesh, the sphere round its mesh,
    /// <see cref="SavannaScatter.Solids"/>: NOT the spacing footprints, which a bush's crown overhangs), or within
    /// <see cref="FIRE_MARGIN"/> of a campfire's hearth and flame; the best of the first <see cref="ENOUGH"/> clear ones flies
    /// (the length asked for, arriving along the pose's facing, past the most trees on both hands). ⚠ Three things the game's
    /// own lens stands inside are held by the pose instead of by their spheres, or no landing would clear: the bank round the
    /// island and what is planted on it (<see cref="BANK_REACH"/>), and any solid whose loose sphere reaches the pose
    /// (<see cref="POSE_SLACK"/>).
    /// </para>
    /// <para>
    /// <b>The trees, twice.</b> A tree's spheres are loose (its wood's reaches 31 round an acacia whose crown reaches 17, and
    /// down under the ground), which keeps the lens well off every tree, and at some scene seeds they close every way in to
    /// the pose (30 of 800 builds at the levels' own poses over 400 scene seeds, and the game rolls a new one every launch): a
    /// grove's spheres stand over the pose's back, or its sides, all the way up. Where they do, the search is run again with
    /// every tree held by its own outline instead (<see cref="SavannaScatter.TreeOutlines"/>, a stack of upright discs as
    /// wide as its meshes reach at each height) and kept <see cref="TREE_AIR"/> off it, which opens the grass under the crowns
    /// and between the trunks (<see cref="TreesHeldBy"/>); all 800 land. Null only for a pose inside the island's radius (the
    /// menu's crane) or one that is not clear itself (beside a fire), or where nothing clears even so, and the savanna then
    /// plays the reel it had before, the tour's arrival leg included.
    /// </para>
    /// <para>Built once when the intro begins, never per frame.</para>
    /// </summary>
    internal static class SavannaLanding
    {
        /// <summary>The shot's name, in the intro's log line.</summary>
        internal const string NAME = "the grass home";

        //The start straight in: among the grass and the groves (the tufts stand from 40 out, the acacias from 60,
        //SavannaSceneConfig), START_RUN_MIN to START_RUN_MAX from the pose and up to STRAIGHT_SPREAD degrees round from
        //straight behind it. ⚠ The stands are found first, not rolled: a tree's sphere reaches 20 to 30 round it (a mesh's
        //bounding sphere, see POSE_SLACK) and a tuft's 3 to 5, so the plain round the island is mostly closed to a lens this low
        //and its clear ways in are narrow (at one scene seed the one way in to the Gallery's pose was a corridor 10 to 20
        //wide), which rolled starts missed every time. So every STAND_STEP_DEGREES and STAND_STEP_OUT the stands are asked
        //first, at the run's middle height, and a run starts from a clear one.
        internal const float START_RUN_MIN = 40f, START_RUN_MAX = 90f;
        private const float STRAIGHT_SPREAD = 75f;
        private const float STAND_STEP_DEGREES = 3f, STAND_STEP_OUT = 5f;

        //The run: heading for the pose give or take this many degrees, straight for this long before the release curves it
        //onto the pose
        private const float HEADING_SPREAD = 10f;
        private const float RUN_MIN = 0f, RUN_MAX = 16f;

        //Or ROUND_SHARE of the time round the island: from a clear stand (asked as the straight ones are) ROUND_FROM_MIN to
        //ROUND_FROM_MAX degrees round from the pose's bearing and START_BEYOND_MIN to START_BEYOND_MAX farther out than it, in
        //onto a circle CIRCLE_BEYOND_MIN to CIRCLE_BEYOND_MAX farther out than the pose (the shared release, arriving along the
        //circle SWEEP_IN_MIN to SWEEP_IN_MAX degrees round from the start), round it, and the shared turn in onto the pose from
        //SWEEP_OUT_MIN to SWEEP_OUT_MAX degrees short of it (IntroLanding.TurnIn), partly along the turn (SLANT_SOME,
        //SLANT_MORE). ⚠ A pose hemmed in by groves (their spheres reaching it from outside) has no clear way straight in; along
        //the circle, between the fires' ring and the groves, it may have. ⚠ The turn in is tightest where it arrives, and the
        //nearer the circle is to the pose and the straighter down the facing it arrives the tighter: within 15 of the pose,
        //straight down the facing, it turned at radii of 3 to 8, so the circle stands at least 15 out from it and arrives with
        //some of the turn kept.
        private const float ROUND_SHARE = 0.4f;
        private const float ROUND_FROM_MIN = 40f, ROUND_FROM_MAX = 90f;
        internal const float START_BEYOND_MIN = 30f, START_BEYOND_MAX = 60f;
        private const float CIRCLE_BEYOND_MIN = 15f, CIRCLE_BEYOND_MAX = 25f;
        private const float SWEEP_IN_MIN = 35f, SWEEP_IN_MAX = 65f;
        private const float SWEEP_OUT_MIN = 45f, SWEEP_OUT_MAX = 75f;
        private const float SLANT_SOME = 0.3f, SLANT_MORE = 0.55f;

        //How high over the grass: over the highest ground at each point and HUG_LATERAL either side of it, rounded over the
        //run's own travel (AridIntroPaths.Hug). A little over the acacia track's 2.2 (SavannaIntroShots): a tuft's sphere, a unit
        //up and 3 to 5 round, closes nearly every way across the plain to a lens lower than that
        internal const float ABOVE_MIN = 3f, ABOVE_MAX = 4.2f;
        private const float HUG_LATERAL = 1.5f;

        //The climb onto the pose's height: over this much of the way's end
        private const float CLIMB_MIN = 32f, CLIMB_MAX = 50f;

        //The pace: a cruise, then the shared deceleration, which takes at least SETTLE_SECONDS (the meadow's lesson: begun
        //late, a fast cruise came onto the pose at 9 u/s in its last half second)
        internal const float CRUISE_MIN = 20f, CRUISE_MAX = 24f, CRUISE_ROUND_MAX = 26f;
        private const float SETTLE_SECONDS = 2.4f;

        //How long the flight should be: among clear candidates the one nearest it is preferred, and none longer than the
        //longest. It replaces the tour's 4.5-second arrival leg (ChapterIntro.ARRIVAL_SECONDS), and the owner prefers faster to
        //slower
        private const float TARGET_SECONDS = 5f, LONGEST_SECONDS = 6f;

        //No tighter than this at the cruise: at 26 u/s a radius of 16 is 93 degrees a second of heading, which the look's
        //averaging takes well under the 170 the ride is held to (70 at most over the sweep, SavannaLandingTests); the
        //deceleration onto the pose, slowing to rest, may take RELEASE_TURN_RADIUS
        private const float MIN_TURN_RADIUS = 16f;
        private const float RELEASE_TURN_RADIUS = 10f;

        //"Among the trees": a tree whose root stands within this of the run's line in plan before it levels onto the pose has
        //been passed, on one hand or the other (a tree's own sphere keeps the lens about 20 off its trunk); each hand counts up
        //to TREES_COUNTED, each tree worth about a second off the length asked for, and one on each hand worth one more
        private const float TREE_NEAR = 36f;
        private const int TREES_COUNTED = 4;
        private const float TREE_WORTH = 1f, BOTH_HANDS_WORTH = 1f;

        //A cut from the shot before it must move the lens this far or turn the view this much (film's thirty-degree rule, the
        //cities' 40 units): two low shots over the grass from nearly one stand read as a glitch
        internal const float CUT_TRAVEL = 40f;
        internal const float CUT_TURN = 30f;

        //The plan's spacing, the candidates rolled at most and the clear ones the search stops at (a third of the rolls clear
        //where the plain is open, and stopping there is what holds a build's cost: see the grass home in
        //docs/game-feedback/chapter-intro-scene-shots.md), and how many of the plan's points the first look skips (see Lay)
        private const float STEP = 0.5f;
        private const int CANDIDATES = 480, ENOUGH = 32;
        private const int SCOUT_STEP = 4;

        //Clearances. Every point keeps MARGIN off every solid (the savanna's other shots' 1.2) and LOWEST over the ground. A
        //fire is its hearth and its flame: a capsule from its foot up the flame's height, as wide as the hearth's stones reach
        //(SavannaLand.FireReach), and the lens keeps FIRE_MARGIN off it. The pose's own distance less ARRIVAL_SLACK is the
        //nearest the axis the lens comes.
        private const float MARGIN = 1.2f;
        private const float LOWEST = 1.2f;

        //⚠ A solid's sphere is its mesh's bounding sphere, and a tree's is loose: up to 31 round a big acacia whose crown reaches
        //17, so a tree 60 out reaches a game's lens 37 out (poses swept round the arena stood inside one, and no landing on them
        //was clear). A solid the pose itself stands within MARGIN and POSE_SLACK of is held at the pose's own clearance of it,
        //less POSE_SLACK: what the game's lens clears all level, a lens no nearer clears too.
        private const float POSE_SLACK = 0.5f;
        internal const float FIRE_MARGIN = 1.5f;

        //Held by its own outline (TreesHeldBy.Outlines), a tree is kept this far off what its meshes draw: farther than a
        //solid's MARGIN, for a lens passing under a crown of leaf sprays or beside a trunk at the cruise
        internal const float TREE_AIR = 3f;
        private const float ARRIVAL_SLACK = 0.5f;

        //The bank round the island (IslandBerm) and what is planted on it, its grass against the wall and its stones at its
        //foot (SavannaScatter's berm tufts and stones, all within BANK_REACH of the axis): the arena's, as the island is, and
        //the game's own lens stands over it. Held as the arena is rather than by its spheres: ⚠ the sphere round a tuft on the
        //bank reaches past the game's lens itself (at one scene seed a tuft 26.6 out reached the Gallery's pose, 30 out), and
        //held by it no landing on that pose cleared. Within BANK_REACH the lens is never nearer the axis than the pose
        //(IntroLanding.KeepOutFor) nor lower than it less BANK_SLACK: the grass leans out from the wall and falls away, so what
        //the game's lens clears a lens no nearer and no lower clears too; the climb is over ARRIVE_LEVEL short of the pose, so
        //the lens comes over the bank at the pose's height. The bank itself stands up to BANK_RISE over the ground it was built
        //off (its section's 1.05 and 0.75 at its wander's lift of up to 1.4), its inner ring up to BANK_TOP over the island's
        //foot (1.35 at that lift).
        private const float BANK_REACH = ArenaIsland.RADIUS + 8f;
        private const float BANK_SLACK = 0.5f;
        private const float BANK_RISE = 1.5f;
        private const float BANK_TOP = ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT + 1.9f;

        //How far outside the island's radius the pose must stand for a landing at all: the game's lens stands 28 to 45 out
        private const float ISLAND_CLEAR = 2f;

        //The climb is over this far short of the pose: the bank's reach from the nearest pose landed on
        private const float ARRIVE_LEVEL = BANK_REACH - (ArenaIsland.RADIUS + ISLAND_CLEAR);

        //A cell of the solids' grid, world units
        private const float CELL = 12f;

        /// <summary>How the grass home is timed, pointed and banked once laid (<see cref="IntroLanding.Fly"/>): a little wider
        /// than the game's lens at first, as the savanna's other shots are, easing to it; the look along the travel turned half
        /// way in towards the arena, so the island stands ahead the whole way, landing over the last 1.8 s; a light bank.</summary>
        internal static readonly IntroLanding.Style Landing = new(WideLens: 1.1f, ReleaseTimeScale: 2f, LookPitchFollow: 0.3f,
            LookDistance: 30f, LookSmoothSeconds: 0.3f, LookLandSeconds: 1.8f, LookInward: 0.5f, RollPerTurn: 0.1f,
            MaxRollDegrees: 8f, RollSmoothSeconds: 0.25f, Points: 200);

        /// <summary>How the trees are held (see the class's note): by the spheres round their meshes, as everything else
        /// planted is, or by their own outlines.</summary>
        internal enum TreesHeldBy { Spheres, Outlines }

        /// <summary>
        /// The grass home over <paramref name="land"/>, landing on <paramref name="endPosition"/> looking at
        /// <paramref name="endTarget"/> with the game's <paramref name="fieldOfView"/>: the trees held by their spheres, or
        /// where those leave no way in, by their outlines; null when no rolled route is clear either way.
        /// </summary>
        /// <param name="cutFrom">The last pose of the shot before it (its lens and what it looks at), whose cut the start must
        /// move or turn enough from; null for none.</param>
        public static IntroShot Build(SavannaLand land, Vector3 endPosition, Vector3 endTarget, float fieldOfView, Random random,
            (Vector3 Position, Vector3 Target)? cutFrom = null) =>
            Build(land, endPosition, endTarget, fieldOfView, random, TreesHeldBy.Spheres, cutFrom)
            ?? Build(land, endPosition, endTarget, fieldOfView, random, TreesHeldBy.Outlines, cutFrom);

        /// <summary>The grass home with the trees held one way only.</summary>
        internal static IntroShot Build(SavannaLand land, Vector3 endPosition, Vector3 endTarget, float fieldOfView, Random random,
            TreesHeldBy trees, (Vector3 Position, Vector3 Target)? cutFrom = null)
        {
            if (land == null) return null;

            Vector2 end = new(endPosition.X, endPosition.Z);
            if (end.Length() < ArenaIsland.RADIUS + ISLAND_CLEAR) return null;

            Vector2 facing = new(endTarget.X - endPosition.X, endTarget.Z - endPosition.Z);
            if (facing.LengthSquared() < 1e-6f) return null;
            facing.Normalize();

            var obstacles = new Obstacles(land, endPosition, trees);
            if (!obstacles.Clear(endPosition)) return null;

            List<Vector2> straight = StraightStarts(obstacles, endPosition, facing);
            List<(int Sign, float Bearing, float Distance)> round = RoundStarts(obstacles, endPosition);
            //The best of the first ENOUGH clear routes: a third of the rolls clear where the plain is open, and the rest
            //only cost the build its time
            int clear = 0;
            IntroLanding.Route best = IntroLanding.Best(CANDIDATES, () =>
            {
                if (clear >= ENOUGH) return null;
                IntroLanding.Route route = Lay(land, obstacles, straight, round, endPosition, facing, random, cutFrom);
                if (route == null || !IntroLanding.Clear(route.Path, obstacles.Clear)) return null;
                clear++;
                return route;
            });
            return best == null ? null : IntroLanding.Fly(NAME, best, endPosition, endTarget, fieldOfView, Landing);
        }

        /// <summary>
        /// Where a run straight in may start (see <see cref="START_RUN_MIN"/>): the stands within <see cref="STRAIGHT_SPREAD"/>
        /// degrees of straight behind the pose, every <see cref="STAND_STEP_DEGREES"/>, and <see cref="START_RUN_MIN"/> to
        /// <see cref="START_RUN_MAX"/> from it, every <see cref="STAND_STEP_OUT"/>, where a lens at the run's middle height over
        /// the ground is clear.
        /// </summary>
        private static List<Vector2> StraightStarts(Obstacles obstacles, Vector3 endPosition, Vector2 facing)
        {
            var starts = new List<Vector2>();
            Vector2 end = new(endPosition.X, endPosition.Z);
            float behind = MathF.Atan2(-facing.Y, -facing.X), above = (ABOVE_MIN + ABOVE_MAX) * 0.5f;

            for (float off = -STRAIGHT_SPREAD; off <= STRAIGHT_SPREAD; off += STAND_STEP_DEGREES)
                for (float run = START_RUN_MIN; run <= START_RUN_MAX; run += STAND_STEP_OUT)
                {
                    Vector2 at = end + IntroGround.Heading(behind + MathHelper.ToRadians(off)) * run;
                    if (obstacles.Clear(new Vector3(at.X, obstacles.Ground(at.X, at.Y) + above, at.Y))) starts.Add(at);
                }

            return starts;
        }

        /// <summary>
        /// Where a run round the island may start (see <see cref="ROUND_SHARE"/>): the stands <see cref="ROUND_FROM_MIN"/> to
        /// <see cref="ROUND_FROM_MAX"/> degrees round from the pose's bearing either way, every <see cref="STAND_STEP_DEGREES"/>,
        /// and <see cref="START_BEYOND_MIN"/> to <see cref="START_BEYOND_MAX"/> farther out than it, every
        /// <see cref="STAND_STEP_OUT"/>, where a lens at the run's middle height over the ground is clear.
        /// </summary>
        private static List<(int Sign, float Bearing, float Distance)> RoundStarts(Obstacles obstacles, Vector3 endPosition)
        {
            var starts = new List<(int, float, float)>();
            Vector2 end = new(endPosition.X, endPosition.Z);
            float poseDistance = end.Length(), poseBearing = IntroGround.Bearing(end), above = (ABOVE_MIN + ABOVE_MAX) * 0.5f;

            for (int sign = -1; sign <= 1; sign += 2)
                for (float degrees = ROUND_FROM_MIN; degrees <= ROUND_FROM_MAX; degrees += STAND_STEP_DEGREES)
                    for (float beyond = START_BEYOND_MIN; beyond <= START_BEYOND_MAX; beyond += STAND_STEP_OUT)
                    {
                        float bearing = -sign * MathHelper.ToRadians(degrees);
                        Vector2 at = IntroGround.Heading(poseBearing + bearing) * (poseDistance + beyond);
                        if (obstacles.Clear(new Vector3(at.X, obstacles.Ground(at.X, at.Y) + above, at.Y))) starts.Add((sign, bearing, poseDistance + beyond));
                    }

            return starts;
        }

        private static IntroLanding.Route Lay(SavannaLand land, Obstacles obstacles, List<Vector2> straightStarts,
            List<(int Sign, float Bearing, float Distance)> roundStarts, Vector3 endPosition, Vector2 facing, Random random,
            (Vector3 Position, Vector3 Target)? cutFrom)
        {
            Vector2 end = new(endPosition.X, endPosition.Z);
            float poseDistance = end.Length(), poseBearing = IntroGround.Bearing(end);

            bool round = random.NextDouble() < ROUND_SHARE;
            if ((round ? roundStarts.Count : straightStarts.Count) == 0) return null;

            int pick = random.Next(round ? roundStarts.Count : straightStarts.Count);
            int sign = round ? roundStarts[pick].Sign : 1;
            float startBearing = round ? roundStarts[pick].Bearing : 0f;
            float startDistance = round ? roundStarts[pick].Distance : 0f;
            float headingTurn = MathHelper.ToRadians(AridIntroPaths.Roll(random, -HEADING_SPREAD, HEADING_SPREAD));
            float run = AridIntroPaths.Roll(random, RUN_MIN, RUN_MAX);
            float radius = poseDistance + AridIntroPaths.Roll(random, CIRCLE_BEYOND_MIN, CIRCLE_BEYOND_MAX);
            float sweepIn = MathHelper.ToRadians(AridIntroPaths.Roll(random, SWEEP_IN_MIN, SWEEP_IN_MAX));
            float sweepOut = MathHelper.ToRadians(AridIntroPaths.Roll(random, SWEEP_OUT_MIN, SWEEP_OUT_MAX));
            float above = AridIntroPaths.Roll(random, ABOVE_MIN, ABOVE_MAX);
            float climb = AridIntroPaths.Roll(random, CLIMB_MIN, CLIMB_MAX);
            float cruise = AridIntroPaths.Roll(random, CRUISE_MIN, round ? CRUISE_ROUND_MAX : CRUISE_MAX);
            double arrival = random.NextDouble();
            float noise = (float)random.NextDouble();

            Vector2 start = round ? IntroGround.Heading(poseBearing + startBearing) * startDistance
                : straightStarts[pick];

            //Round the island, the sweeps onto the circle and off it share the turn from the start's bearing to the pose's
            //where they would take more than it
            float turn = MathF.Abs(startBearing);
            if (round && sweepIn + sweepOut > turn)
            {
                float share = turn / (sweepIn + sweepOut);
                sweepIn *= share;
                sweepOut *= share;
            }

            //Round the island the run heads in for the release's last control point short of the circle's entry (so it bends
            //onto the circle once, gently), straight in for the pose, give or take the heading's roll
            float entryBearing = poseBearing + startBearing + sign * sweepIn;
            Vector2 aim = round ? Entry(start, entryBearing, radius, sign) : end;
            Vector2 toAim = Vector2.Normalize(aim - start);
            float heading0 = MathF.Atan2(toAim.Y, toAim.X) + (round ? headingTurn * 0.5f : headingTurn);
            Vector2 heading = IntroGround.Heading(heading0);

            //A cut from the shot before: the lens moved far enough, or the view turned far enough
            if (cutFrom is (Vector3 previous, Vector3 previousTarget))
            {
                Vector3 first = new(start.X, obstacles.Ground(start.X, start.Y) + above, start.Y);
                Vector2 before = new(previousTarget.X - previous.X, previousTarget.Z - previous.Z);
                float turned = before.LengthSquared() < 1e-6f ? MathF.PI
                    : MathF.Abs(MathHelper.WrapAngle(heading0 - MathF.Atan2(before.Y, before.X)));
                if (Vector3.Distance(first, previous) < CUT_TRAVEL && MathHelper.ToDegrees(turned) < CUT_TURN) return null;
            }

            //The run, then the release onto the pose; or round the island, the release onto the circle, round it and the turn
            //in onto the pose
            var plan = new List<Vector2>();
            int releaseStart;
            if (!round)
            {
                //⚠ No run shorter than a step: Straight lays two points for any length, and a plan with a point twice over is
                //flown with a step in its height there (the hug spaces its points by their order along the plan)
                if (run >= STEP) IntroLanding.Straight(plan, start, start + heading * run, STEP);
                else plan.Add(start);
                releaseStart = plan.Count - 1;
                IntroLanding.Release(plan, plan[^1], heading, end, IntroLanding.Arrival(arrival, facing, heading), STEP);
            }
            else
            {
                plan.Add(start);
                IntroLanding.Release(plan, start, heading, IntroGround.Heading(entryBearing) * radius, Tangent(entryBearing, sign), STEP);

                //(and no arc shorter than a step, for the same reason: the turn in takes it)
                float arc = turn - sweepIn - sweepOut;
                if (arc * radius < STEP) arc = 0f;
                int arcSteps = (int)MathF.Ceiling(arc * radius / STEP);
                for (int i = 1; i <= arcSteps; i++) plan.Add(IntroGround.Heading(entryBearing + sign * arc * i / arcSteps) * radius);

                releaseStart = plan.Count - 1;
                float slant = arrival < 0.6 ? SLANT_SOME : SLANT_MORE;
                IntroLanding.TurnIn(plan, entryBearing + sign * arc, radius, sign, end, slant, STEP);
            }

            //Evenly spaced along its length: a release is thinned to at least a step, not to one, and the hug spaces its heights
            //by the points' order along the plan, so an uneven plan flew a ripple in its height over sloping ground (a jolt of
            //54 u/s² up the rise 112 out)
            float releaseAt = IntroLanding.PlanLength(plan.GetRange(0, releaseStart + 1));
            plan = Even(plan, STEP, out float spacing);
            releaseStart = Math.Min((int)MathF.Round(releaseAt / spacing), plan.Count - 1);

            //The lengths, and where the deceleration begins: at least SETTLE_SECONDS from the end at the cruise, and round the
            //island no later than the turn in, whose tightest turn a cruise cannot take (the meadow's rule)
            var length = new float[plan.Count];
            for (int i = 1; i < plan.Count; i++) length[i] = length[i - 1] + Vector2.Distance(plan[i - 1], plan[i]);
            float total = length[^1];

            float settle = MathF.Max(cruise * SETTLE_SECONDS / Landing.ReleaseTimeScale, round ? total - length[releaseStart] : 0f);
            int settleFrom = plan.Count - 2;
            while (settleFrom > 0 && total - length[settleFrom] < settle) settleFrom--;
            if (settleFrom < 1) return null;

            if (!IntroLanding.NoTighterThan(plan, 0, MIN_TURN_RADIUS, settleFrom + 3)
                || !IntroLanding.NoTighterThan(plan, Math.Max(releaseStart, settleFrom), RELEASE_TURN_RADIUS)) return null;

            //How long it will take (the shared landing's own timing): no longer than LONGEST_SECONDS, and the one nearest
            //TARGET_SECONDS is preferred
            float estimate = length[settleFrom] / cruise + Landing.ReleaseTimeScale * (total - length[settleFrom]) / cruise;
            if (estimate > LONGEST_SECONDS) return null;

            //A first look before the heights are flown: every SCOUT_STEP-th point at the run's height over the ground under it,
            //eased onto the pose's as below. Nearly every roll stands in a tree's sphere or a tuft's somewhere, and refused here
            //it costs no hug; the route that passes is asked again, whole, at the heights it is flown at
            float sLevel = MathF.Max(total - ARRIVE_LEVEL, 0f), sClimb = MathF.Max(sLevel - climb, 0f);
            for (int i = 0; i < plan.Count; i += SCOUT_STEP)
            {
                float rise = Rise(length[i], sClimb, sLevel);
                if (!obstacles.Clear(new Vector3(plan[i].X, MathHelper.Lerp(obstacles.Ground(plan[i].X, plan[i].Y) + above, endPosition.Y, rise), plan[i].Y)))
                    return null;
            }

            //The heights: low over the ground the way a drone flies it, rounded over the run's travel (the hug rounds at a
            //shot's peak pace, IntroShot.PEAK_PACE times the mean its seconds give, and the landing is walked on the same eased
            //clock, so the cruise's seconds are what it is handed), then eased onto the pose's height over the climb, which is
            //over ARRIVE_LEVEL short of it
            Vector3[] hug = AridIntroPaths.Hug(plan.ToArray(), obstacles.Ground, above, HUG_LATERAL,
                seconds: total / cruise);

            var path = new List<Vector3>(plan.Count);
            var speed = new float[plan.Count];
            for (int i = 0; i < plan.Count; i++)
            {
                float rise = Rise(length[i], sClimb, sLevel);
                path.Add(new Vector3(plan[i].X, MathHelper.Lerp(hug[i].Y, endPosition.Y, rise), plan[i].Y));
                speed[i] = cruise;
            }
            path[^1] = endPosition;

            //Among the trees: the trees the run passes within TREE_NEAR of before it levels onto the pose, on each hand
            int low = 0;
            while (low + 1 < plan.Count && length[low + 1] <= sLevel) low++;
            (int left, int right) = Passed(plan, low, land.Trees);
            float among = TREE_WORTH * (Math.Min(left, TREES_COUNTED) + Math.Min(right, TREES_COUNTED))
                + (left > 0 && right > 0 ? BOTH_HANDS_WORTH : 0f);

            //The best is the length asked for, arriving along the pose's facing, among the trees
            float swivel = MathF.Acos(MathHelper.Clamp(Vector2.Dot(Vector2.Normalize(plan[^1] - plan[^2]), facing), -1f, 1f));

            return new IntroLanding.Route
            {
                Path = path, Speed = speed, DecelerateFrom = settleFrom,
                Score = -MathF.Abs(estimate - TARGET_SECONDS) - 2f * swivel + among + noise * 0.5f,
            };
        }

        /// <summary>
        /// <paramref name="plan"/> laid again at even spacing along its length, as near <paramref name="step"/> as divides it
        /// (<paramref name="spacing"/>), from its first point to its last.
        /// </summary>
        private static List<Vector2> Even(List<Vector2> plan, float step, out float spacing)
        {
            float total = IntroLanding.PlanLength(plan);
            int steps = Math.Max(1, (int)MathF.Round(total / step));
            spacing = total / steps;

            var even = new List<Vector2>(steps + 1) { plan[0] };
            int i = 1;
            float before = 0f, segment = Vector2.Distance(plan[0], plan[Math.Min(1, plan.Count - 1)]);
            for (int k = 1; k < steps; k++)
            {
                float s = k * spacing;
                while (i < plan.Count - 1 && before + segment < s)
                {
                    before += segment;
                    i++;
                    segment = Vector2.Distance(plan[i - 1], plan[i]);
                }
                even.Add(Vector2.Lerp(plan[i - 1], plan[i], segment > 1e-6f ? MathHelper.Clamp((s - before) / segment, 0f, 1f) : 1f));
            }
            even.Add(plan[^1]);
            return even;
        }

        /// <summary>The circle's direction of travel at <paramref name="bearing"/>, going round the way <paramref name="sign"/>
        /// says (positive is the bearing growing).</summary>
        private static Vector2 Tangent(float bearing, int sign) => new(-sign * MathF.Sin(bearing), sign * MathF.Cos(bearing));

        //The release's last control point onto the circle at a bearing (IntroLanding.Release puts it 0.35 of the way's reach
        //back along the arrival), which a run headed for bends onto the circle once
        private static Vector2 Entry(Vector2 start, float bearing, float radius, int sign)
        {
            Vector2 entry = IntroGround.Heading(bearing) * radius;
            return entry - Tangent(bearing, sign) * (Vector2.Distance(start, entry) * 0.35f);
        }

        //How far onto the pose's height the lens has climbed at a length along the way: a quintic ease from sClimb to sLevel,
        //whose climb rate changes from nothing at both ends (a smoothstep's jumps there)
        private static float Rise(float s, float sClimb, float sLevel)
        {
            float u = MathHelper.Clamp((s - sClimb) / MathF.Max(sLevel - sClimb, 1e-3f), 0f, 1f);
            return u * u * u * (u * (u * 6f - 15f) + 10f);
        }

        /// <summary>
        /// How many of <paramref name="trees"/> the plan up to point <paramref name="last"/> passes within
        /// <see cref="TREE_NEAR"/> of in plan, on its left hand and on its right.
        /// </summary>
        internal static (int Left, int Right) Passed(List<Vector2> plan, int last, IReadOnlyList<PlantFigure> trees)
        {
            int left = 0, right = 0;
            foreach (PlantFigure tree in trees)
            {
                Vector2 root = new(tree.Root.X, tree.Root.Z);
                int nearest = 0;
                for (int i = 1; i <= last; i++)
                    if (Vector2.DistanceSquared(plan[i], root) < Vector2.DistanceSquared(plan[nearest], root)) nearest = i;
                if (Vector2.Distance(plan[nearest], root) >= TREE_NEAR) continue;

                Vector2 along = plan[Math.Min(nearest + 1, plan.Count - 1)] - plan[Math.Max(nearest - 1, 0)];
                Vector2 off = root - plan[nearest];
                if (along.X * off.Y - along.Y * off.X > 0f) left++; else right++;
            }

            return (left, right);
        }

        /// <summary>
        /// What a point of the flight may not be near (see the class's note): the ground and the bank round the island, the
        /// arena nearer than the pose, every campfire's hearth and flame, and everything planted, the trees held as the
        /// <see cref="TreesHeldBy"/> it is made with says. The solids are kept in a grid, since every point of every candidate
        /// is asked and the plain plants about a thousand.
        /// </summary>
        internal sealed class Obstacles
        {
            private readonly SavannaLand _land;
            private readonly float _keepOut, _bankFloor;
            private readonly List<PlantFigure> _solids = new();
            private readonly List<float> _need = new();
            private readonly List<PlantOutline> _trees = new();
            private readonly List<float> _treeNeed = new();

            //Each cell's solids, by their index, and its trees, by the complement of theirs
            private readonly Dictionary<(int, int), List<int>> _byCell = new();

            /// <param name="land">The plain.</param>
            /// <param name="pose">The pose landed on: no point stands nearer the axis (<see cref="IntroLanding.KeepOutFor"/>),
            /// nor lower over the bank (see <see cref="BANK_REACH"/>).</param>
            /// <param name="trees">How the trees are held: by their spheres, among the solids, or by their outlines.</param>
            public Obstacles(SavannaLand land, Vector3 pose, TreesHeldBy trees = TreesHeldBy.Spheres)
            {
                _land = land;
                _keepOut = IntroLanding.KeepOutFor(pose, IntroGround.ARENA_KEEP_OUT + MARGIN, ARRIVAL_SLACK);
                _bankFloor = pose.Y - BANK_SLACK;

                foreach (PlantFigure solid in trees == TreesHeldBy.Outlines ? land.SolidsBesideTrees : land.Solids)
                {
                    //The bank round the island and what is planted on it: held as the arena is, see BANK_REACH
                    if (IsBank(solid)) continue;

                    //Every cell the solid's sphere or capsule, with the margin, can reach into
                    float reach = MathF.Max(solid.Reach, solid.Stem) + MARGIN;
                    File(_solids.Count, MathF.Min(solid.Root.X, solid.Crown.X) - reach, MathF.Max(solid.Root.X, solid.Crown.X) + reach,
                        MathF.Min(solid.Root.Z, solid.Crown.Z) - reach, MathF.Max(solid.Root.Z, solid.Crown.Z) + reach);
                    _solids.Add(solid);
                    _need.Add(MathF.Min(MARGIN, Off(solid, pose) - POSE_SLACK));
                }

                if (trees != TreesHeldBy.Outlines) return;

                //The trees by their outlines, each kept TREE_AIR off, or where the pose stands nearer one, no nearer than the
                //pose less POSE_SLACK
                foreach (PlantOutline tree in land.TreeOutlines)
                {
                    File(~_trees.Count, tree.Bounds.Min.X - TREE_AIR, tree.Bounds.Max.X + TREE_AIR,
                        tree.Bounds.Min.Z - TREE_AIR, tree.Bounds.Max.Z + TREE_AIR);
                    _trees.Add(tree);
                    _treeNeed.Add(MathF.Min(TREE_AIR, tree.Off(pose) - POSE_SLACK));
                }
            }

            //Files a thing under every cell from (minX, minZ) to (maxX, maxZ)
            private void File(int index, float minX, float maxX, float minZ, float maxZ)
            {
                for (int i = Cell(minX); i <= Cell(maxX); i++)
                    for (int j = Cell(minZ); j <= Cell(maxZ); j++)
                    {
                        if (!_byCell.TryGetValue((i, j), out List<int> list)) _byCell[(i, j)] = list = new List<int>();
                        list.Add(index);
                    }
            }

            /// <summary>Whether <paramref name="solid"/> is the bank round the island or stands on it (<c>IslandBerm</c>'s two
            /// meshes, centred on the axis, and the grass and the stones planted on it), which the flight holds as the arena is
            /// (see <see cref="BANK_REACH"/>) rather than by its sphere.</summary>
            internal static bool IsBank(PlantFigure solid) => new Vector2(solid.Root.X, solid.Root.Z).Length() < BANK_REACH;

            private static int Cell(float v) => (int)MathF.Floor(v / CELL);

            /// <summary>How far <paramref name="p"/> stands off <paramref name="solid"/>: off its crown's sphere or its stem,
            /// whichever is nearer (negative inside).</summary>
            internal static float Off(PlantFigure solid, Vector3 p) => MathF.Min(Vector3.Distance(p, solid.Crown) - solid.Reach,
                IntroPaths.DistanceToSegment(p, solid.Root, solid.Crown) - solid.Stem);

            /// <summary>The ground a lens is held over: the land, and the bank round the island where it stands.</summary>
            public float Ground(float x, float z)
            {
                float ground = _land.Height(x, z);
                return x * x + z * z < BANK_REACH * BANK_REACH ? MathF.Max(ground + BANK_RISE, BANK_TOP) : ground;
            }

            /// <summary>Whether a lens at <paramref name="p"/> is clear of everything the flight keeps off.</summary>
            public bool Clear(Vector3 p)
            {
                float fromAxis = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
                if (fromAxis < _keepOut || (fromAxis < BANK_REACH && p.Y < _bankFloor)) return false;
                if (p.Y < Ground(p.X, p.Z) + LOWEST) return false;

                foreach (Vector3 fire in _land.Fires)
                    if (IntroPaths.DistanceToSegment(p, fire, fire + Vector3.Up * _land.FireHeight) - _land.FireReach < FIRE_MARGIN) return false;

                if (_byCell.TryGetValue((Cell(p.X), Cell(p.Z)), out List<int> near))
                    foreach (int index in near)
                    {
                        if (index >= 0 ? Off(_solids[index], p) < _need[index]
                            : _trees[~index].Off(p, TREE_AIR) < _treeNeed[~index]) return false;
                    }

                return true;
            }
        }
    }

    /// <summary>
    /// The savanna as a chapter intro's shots need it (#559, #1108), without a device: the ground, everything planted, the
    /// trees a shot is about, and the ring of campfires. <see cref="Of(SceneRenderer)"/> takes it off the live scene; a test
    /// builds it off a planting of its own through <see cref="Of(SavannaSceneConfig, SavannaScatter, Func{float, float, float}, IReadOnlyList{Vector3})"/>.
    /// </summary>
    /// <param name="Height">The ground under a point (<see cref="SceneRenderer.SavannaGroundHeight"/>, the fires' mounds in it).</param>
    /// <param name="Solids">Every instance of every mesh planted, as the sphere round its mesh (<see cref="SavannaScatter.Solids"/>).</param>
    /// <param name="TreeOutlines">Every tree planted as its own outline (<see cref="SavannaScatter.TreeOutlines"/>).</param>
    /// <param name="SolidsBesideTrees">The solids less the trees' meshes (<see cref="SavannaScatter.SolidsBesideTrees"/>).</param>
    /// <param name="Acacias">The umbrella-crowned acacias (<see cref="SavannaScatter.Acacias"/>).</param>
    /// <param name="Baobabs">The baobabs (<see cref="SavannaScatter.Baobabs"/>).</param>
    /// <param name="Fires">Each campfire's foot (<see cref="SceneRenderer.SavannaCampfirePosition"/>).</param>
    /// <param name="FireReach">How far a fire's hearth and flame reach from its axis.</param>
    /// <param name="FireHeight">How tall its flame stands over its foot.</param>
    internal sealed record SavannaLand(Func<float, float, float> Height, IReadOnlyList<PlantFigure> Solids,
        IReadOnlyList<PlantOutline> TreeOutlines, IReadOnlyList<PlantFigure> SolidsBesideTrees, IReadOnlyList<PlantFigure> Acacias,
        IReadOnlyList<PlantFigure> Baobabs, IReadOnlyList<Vector3> Fires, float FireReach, float FireHeight)
    {
        //A hearth's stones (SavannaBackdrop.BuildHearthStones; the backdrop is internal to Prazsky.Core, so its HEARTH_RING_IN
        //+ HEARTH_RING_SPREAD and HEARTH_STONE_SCALE_MIN + HEARTH_STONE_SCALE_SPREAD are copied here): a ring of FlameSize *
        //StoneRingScale, each stone laid out to HEARTH_RING_OUT of it, the sphere round the largest of the three rocks reaching
        //HEARTH_ROCK_REACH of FlameSize * StoneSizeScale from the stone's place (its lathe's wobble included), at a scale of up
        //to HEARTH_STONE_SCALE_MAX. Wider than the flame's own volume (1.45 fire sizes), so it is the fire's reach.
        //SavannaLandingTests holds it against the backdrop's figures and the rocks themselves.
        private const float HEARTH_RING_OUT = 1.12f;
        private const float HEARTH_ROCK_REACH = 1.65f, HEARTH_STONE_SCALE_MAX = 1.27f;

        /// <summary>The trees a run among them is counted against: the acacias and the baobabs.</summary>
        public IReadOnlyList<PlantFigure> Trees { get; } = Concat(Acacias, Baobabs);

        /// <summary>The savanna on screen, or null when its planting is not built.</summary>
        public static SavannaLand Of(SceneRenderer scenes)
        {
            if (scenes?.SavannaPlanting == null || scenes.GetSceneConfig(SceneKind.Savanna) is not SavannaSceneConfig config) return null;

            var fires = new Vector3[scenes.SavannaCampfireCount];
            for (int i = 0; i < fires.Length; i++) fires[i] = scenes.SavannaCampfirePosition(i);
            return Of(config, scenes.SavannaPlanting, scenes.SavannaGroundHeight, fires);
        }

        /// <summary>The savanna planted as <paramref name="planting"/> over <paramref name="height"/>, its fires at
        /// <paramref name="fires"/> and as large as <paramref name="config"/> makes them.</summary>
        public static SavannaLand Of(SavannaSceneConfig config, SavannaScatter planting, Func<float, float, float> height, IReadOnlyList<Vector3> fires)
        {
            CampfireConfig fire = config.Campfire;
            return new SavannaLand(height, planting.Solids, planting.TreeOutlines, planting.SolidsBesideTrees, planting.Acacias, planting.Baobabs, fires,
                fire.FlameSize * (fire.StoneRingScale * HEARTH_RING_OUT + fire.StoneSizeScale * HEARTH_ROCK_REACH * HEARTH_STONE_SCALE_MAX),
                fire.FlameSize * fire.FlameHeightScale);
        }

        private static List<PlantFigure> Concat(IReadOnlyList<PlantFigure> a, IReadOnlyList<PlantFigure> b)
        {
            var all = new List<PlantFigure>(a.Count + b.Count);
            all.AddRange(a);
            all.AddRange(b);
            return all;
        }
    }
}
