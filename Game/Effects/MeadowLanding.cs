using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The brook home</b> (#1107): the last shot of the meadow's chapter intro, a flight down the brook a little over its
    /// water, out across the pond it runs into, and on round the island onto the gameplay pose itself, so the player is
    /// carried from the meadow into the game without a cut — as the cities' swing home does (<see cref="CitySwing"/>, #972).
    /// The owner's note: "the last animation a fly-through over the brook and the pond, adjusted so that the camera flies all
    /// the way to the game position at the cannon, so the scene animation joins the game smoothly".
    /// <para>
    /// <b>The route.</b> Down the brook's centreline (<see cref="MeadowPath.BrookPoint"/>) at the old brook shot's height over
    /// the uncut field, the reeds and the bank stones passing either side and the two willows by the pond on either hand, the
    /// run swerving off the line round the one that stands nearest it (<see cref="ROOT_REACH"/>); on over the pond's water and
    /// out across its near shore through a gap between its reed clumps
    /// (<see cref="MeadowPath.PondReedClump"/>), climbing from its middle on; a curve off the brook's line onto a circle round
    /// the arena outside its keep-out (the shared release, <see cref="IntroLanding.Release"/>); round that circle, the shorter
    /// way to the gun most of the time; and the shared turn in onto the pose (<see cref="IntroLanding.TurnIn"/>). The pond
    /// lies on the far side of the island from the gun's bearing (90), so the circle is what takes the lens round the island
    /// instead of through it. Where the pose stands on the pond's own side, a release leaves the pond straight onto it.
    /// </para>
    /// <para>
    /// <b>The pace.</b> The brook at a run's pace, accelerating over the pond to a cruise round the island, then the shared
    /// deceleration to rest on the pose; the look down the brook, turning in towards the arena off it
    /// (<see cref="IntroLanding.Style.LookInward"/>), so the island, the gun and the cluster stay in the frame while the lens
    /// goes round them; a light bank into the turn.
    /// </para>
    /// <para>
    /// <b>Found, not assumed.</b> Rolled candidates (how much of the brook, the circle's radius and height, how far round the
    /// curves onto and off it sweep, the climb, the cruise, which way the lens arrives, and now and then the longer way round)
    /// are each laid in full and refused if they turn tighter than the speed allows, or if any point is under the ground,
    /// nearer the axis than the pose (<see cref="IntroLanding.KeepOutFor"/>), low in a reed clump round the pond, near a
    /// trunk's roots or in a crown, or near a low solid; the best of the rest flies. The trees are held as the meadow's low
    /// shots hold them (<see cref="MeadowIntroShots"/>): by their STEM (and its roots) wherever the lens is low over the field, because the crown's sphere is
    /// the mesh's bounding sphere and reaches below the ground — the willows' spheres cover the brook where it comes into the
    /// pond, while their canopies stand four units over it — and whole (stem and crown) wherever it is higher. Null when none is
    /// clear, or for a pose inside the island's own radius (the menu's crane, which no flight from the meadow reaches without
    /// crossing the arena), and the meadow then plays the reel it had before, the tour's arrival leg included.
    /// </para>
    /// </summary>
    internal static class MeadowLanding
    {
        /// <summary>The shot's name, in the intro's log line.</summary>
        internal const string NAME = "the brook home";

        //The brook: how much of it the lens flies down before the pond's middle, at what pace, and how high over the uncut
        //field, the old brook shot's (MeadowIntroShots), where the reeds and the bank stones pass either side
        private const float BROOK_RUN_MIN = 28f, BROOK_RUN_MAX = 38f;
        private const float BROOK_SPEED = 20f;
        internal const float BROOK_ABOVE = 1.8f;

        //The climb: from this far past the pond's middle (low until then, under the willows' crowns, see the class's note),
        //over this long a run, to the cruise's height over the highest ground the lens passes after the pond. The earlier the
        //better, by RISE_LATE_COST a unit in the score: begun 11 or more past the middle, the lens crossed the near shore at the
        //brook's height, and the crater's bank so close under it drew as a dark sawtooth band across the frame. Up to a run's
        //length past it, though, so a tree standing by the pond's arena side can still be passed under.
        private const float RISE_PAST_MIDDLE_MIN = 0f, RISE_PAST_MIDDLE_MAX = 32f;
        private const float RISE_LATE_COST = 0.05f;
        private const float CLIMB_MIN = 22f, CLIMB_MAX = 40f;
        private const float CRUISE_ABOVE_MIN = 6f, CRUISE_ABOVE_MAX = 10f;

        //The pace picks up from this far short of the pond's middle, and reaches the cruise's on the circle. A circle long
        //enough to take more than CIRCLE_SECONDS at the rolled cruise is flown faster, up to CRUISE_FASTEST: the long way round
        //a pose on the knoll's side ran to 14 seconds.
        private const float ACCELERATE_BEFORE_MIDDLE = 10f;
        private const float CRUISE_MIN = 34f, CRUISE_MAX = 40f, CRUISE_FASTEST = 42f;
        private const float CIRCLE_SECONDS = 2.5f;

        //The circle round the arena: its radius (the keep-out is 44, IntroGround.ARENA_KEEP_OUT; the planting starts at 70,
        //MeadowScatter, past the knoll's oak and the pond), and how far round the arena the curve onto it sweeps, degrees.
        private const float CIRCLE_MIN = 48f, CIRCLE_MAX = 60f;
        private const float SWEEP_IN_MIN = 30f, SWEEP_IN_MAX = 70f;

        //How far round before the pose the release leaves the circle and turns in onto it (IntroLanding.TurnIn), degrees: the
        //turn is 90 degrees and this, so a longer sweep turns wider. And how much of the arrival runs along the turn rather
        //than straight down the pose's facing, off the arrival's roll: straight down it half the time, the view landing
        //forwards on what it will look at, and a little and more along the turn the rest.
        private const float SWEEP_OUT_MIN = 30f, SWEEP_OUT_MAX = 50f;
        private const float SLANT_SOME = 0.3f, SLANT_MORE = 0.55f;

        //A pose fewer than DIRECT_TURN degrees round from the pond is reached straight off it, the deceleration over the last
        //SETTLE_LENGTH of the way: from the pond's middle the whole way at the brook's pace, it ran 12 seconds
        private const float DIRECT_TURN = 40f;
        private const float SETTLE_LENGTH = 40f;
        private const float SETTLE_SECONDS = 2.6f;

        //How often the longer way round is laid: where the knoll's oak stands across the shorter one, it is the way
        private const float LONG_WAY_SHARE = 0.25f;

        //No tighter than this off the brook: at a cruise of 40 u/s a 16-unit radius is 143 degrees a second of heading, which
        //the look's averaging and its lean take well under the 170 the ride is held to (70 at most, MeadowLandingTests); the
        //release, decelerating from the cruise to rest, turns its tightest at about half the speed and may take
        //RELEASE_TURN_RADIUS
        private const float MIN_TURN_RADIUS = 16f;
        private const float RELEASE_TURN_RADIUS = 10f;

        //How long the flight should be: among clear candidates the one nearest it is preferred
        private const float TARGET_SECONDS = 8f;

        //The plan's spacing, and the candidates rolled
        private const float STEP = 0.5f;
        private const int CANDIDATES = 48;

        //Clearances. Every point keeps MARGIN off every solid and LOWEST over the ground (the island's bank, IslandBerm, rises
        //1.35 over the island's foot within ISLAND_FOOT_REACH of the axis). A lens under LOW_LENS over the uncut field is
        //held off a tree's stem and roots alone (see the class's note). The pond's reeds stand from 0.9 inside its shore to 1.5 outside
        //and up to 4 tall (GrassTuftMesh 2.2 at a size of up to 1.8, MeadowScatter), in clumps (MeadowPath.PondReedClump), each
        //reed up to 0.04 radians off its clump's bearing; a lens lower than they are tall crosses that band only REED_GAP
        //radians or more from any clump, about MARGIN past a reed's own width at the shore's distance. None stands on the
        //brook's own bank. The pose's own distance less ARRIVAL_SLACK is the nearest the axis the lens comes.
        private const float MARGIN = 1.5f;
        private const float LOWEST = 1f;
        private const float ISLAND_FOOT_REACH = ArenaIsland.RADIUS + 8f;
        private const float ISLAND_BANK_TOP = ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT + 1.35f;
        private const float LOW_LENS = 3f;

        //A trunk's roots run out to about twice its flare's radius (MeadowTreeMesh: 2.3 to 3.2 trunk radii, the flare 1.55), and
        //the lens keeps TRUNK_AIR past them. ⚠ The willow on the brook's left as it comes into the pond stands 5.5 off its line:
        //flown down the line, the trunk filled a quarter of the frame and its roots ran under the lens. So the run swerves off
        //the line by what it lacks, over DEFLECT_UPSTREAM before the nearest point and DEFLECT_DOWNSTREAM after it; within
        //1.8 of the line by the mouth it is still over the brook's own bank, where the pond plants no reeds.
        private const float ROOT_REACH = 2.1f;
        private const float TRUNK_AIR = 2.5f;
        private const float DEFLECT_UPSTREAM = 6f, DEFLECT_DOWNSTREAM = 9f;
        private const float REED_INSIDE = 0.9f + MARGIN, REED_OUTSIDE = 1.5f + MARGIN, REED_TOP = 4f;
        private const float REED_GAP = 0.25f;
        private const float ARRIVAL_SLACK = 0.5f;

        //How far outside the island's radius the pose must stand for a landing at all: the game's lens stands 28 to 45 out
        private const float ISLAND_CLEAR = 2f;

        //A cell of the solids' grid, world units
        private const float CELL = 12f;

        /// <summary>How the brook home is timed, pointed and banked once laid (<see cref="IntroLanding.Fly"/>): a little wider
        /// than the meadow's other shots at first, easing to the game's lens; the city's look and settle, with the look turning in
        /// towards the arena off the brook (<see cref="IntroLanding.Route.Lean"/>) and landing over a longer last stretch, the
        /// turn onto the pose being a wider one; a light bank.</summary>
        internal static readonly IntroLanding.Style Landing = new(WideLens: 1.15f, ReleaseTimeScale: 2f, LookPitchFollow: 0.3f,
            LookDistance: 30f, LookSmoothSeconds: 0.3f, LookLandSeconds: 2.2f, LookInward: 0.7f, RollPerTurn: 0.1f,
            MaxRollDegrees: 8f, RollSmoothSeconds: 0.25f, Points: 240);

        /// <summary>
        /// The brook home over <paramref name="meadow"/>, landing on <paramref name="endPosition"/> looking at
        /// <paramref name="endTarget"/> with the game's <paramref name="fieldOfView"/>; null when no rolled route is clear.
        /// </summary>
        /// <param name="planted">The planting's figures (<see cref="SceneRenderer.MeadowFigures"/>); null keeps nothing out but
        /// the ground, the reeds and the arena.</param>
        public static IntroShot Build(MeadowSceneConfig meadow, MeadowFigures planted, Vector3 endPosition, Vector3 endTarget,
            float fieldOfView, Random random)
        {
            if (meadow == null) return null;

            Vector2 end = new(endPosition.X, endPosition.Z);
            if (end.Length() < ArenaIsland.RADIUS + ISLAND_CLEAR) return null;

            Vector2 facing = new(endTarget.X - endPosition.X, endTarget.Z - endPosition.Z);
            if (facing.LengthSquared() < 1e-6f) return null;
            facing.Normalize();

            var obstacles = new Obstacles(meadow, planted,
                IntroLanding.KeepOutFor(endPosition, IntroGround.ARENA_KEEP_OUT + MARGIN, ARRIVAL_SLACK));

            IntroLanding.Route best = IntroLanding.Best(CANDIDATES, () => Lay(meadow, planted?.Trees, endPosition, facing, random), obstacles.Clear);
            return best == null ? null : IntroLanding.Fly(NAME, best, endPosition, endTarget, fieldOfView, Landing);
        }

        private static IntroLanding.Route Lay(MeadowSceneConfig meadow, IReadOnlyList<PlantFigure> trees, Vector3 endPosition, Vector2 facing,
            Random random)
        {
            float run = Roll(random, BROOK_RUN_MIN, BROOK_RUN_MAX);
            float radius = Roll(random, CIRCLE_MIN, CIRCLE_MAX);
            float sweepIn = MathHelper.ToRadians(Roll(random, SWEEP_IN_MIN, SWEEP_IN_MAX));
            float risePast = Roll(random, RISE_PAST_MIDDLE_MIN, RISE_PAST_MIDDLE_MAX);
            float above = Roll(random, CRUISE_ABOVE_MIN, CRUISE_ABOVE_MAX);
            float climb = Roll(random, CLIMB_MIN, CLIMB_MAX);
            float cruise = Roll(random, CRUISE_MIN, CRUISE_MAX);
            float sweepOut = MathHelper.ToRadians(Roll(random, SWEEP_OUT_MIN, SWEEP_OUT_MAX));
            double arrival = random.NextDouble();
            float slant = arrival < 0.5 ? 0f : arrival < 0.8 ? SLANT_SOME : SLANT_MORE;
            bool longWay = random.NextDouble() < LONG_WAY_SHARE;

            Vector2 end = new(endPosition.X, endPosition.Z);
            float pondDistance = meadow.ClearingRadius * MeadowPath.BROOK_START;
            (float pondX, float pondZ) = MeadowPath.PondCentre(meadow);
            Vector2 pond = new(pondX, pondZ);

            //Which way round: the turn from the pond's bearing to the pose's, each way
            float pondBearing = IntroGround.Bearing(pond), poseBearing = IntroGround.Bearing(end);
            float anticlockwise = (poseBearing - pondBearing) % MathHelper.TwoPi;
            if (anticlockwise <= 0f) anticlockwise += MathHelper.TwoPi;
            float clockwise = MathHelper.TwoPi - anticlockwise;
            int sign = (anticlockwise <= clockwise) != longWay ? 1 : -1;
            float turn = sign > 0 ? anticlockwise : clockwise;

            //The brook, down its centreline to the pond's middle (its line runs there: MeadowPath.PondCentre)
            var plan = new List<Vector2>();
            int brookSteps = (int)MathF.Ceiling(run / STEP);
            for (int i = 0; i <= brookSteps; i++)
            {
                (float x, float z) = MeadowPath.BrookPoint(pondDistance + run * (1f - i / (float)brookSteps), 0f, meadow);
                plan.Add(new Vector2(x, z));
            }
            int brookEnd = plan.Count - 1;
            if (trees != null) PassTheTrunks(plan, trees);
            Vector2 brookHeading = Vector2.Normalize(plan[^1] - plan[^2]);

            //Off the brook's line: onto the circle and round it, then the turn in onto the pose; or, for a pose on the pond's
            //own side, the release straight off the pond, arriving along the pose's facing or halfway to the brook's line
            int circleFrom, releaseStart;
            float circle = 0f;
            Vector2 arrive;
            bool direct = MathHelper.ToDegrees(turn) < DIRECT_TURN;
            if (direct)
            {
                circleFrom = releaseStart = brookEnd;
                arrive = IntroLanding.Arrival(MathF.Min((float)arrival, 0.79f), facing, brookHeading);
                IntroLanding.Release(plan, plan[^1], brookHeading, end, arrive, STEP);
            }
            else
            {
                if (sweepIn + sweepOut > turn)
                {
                    float share = turn / (sweepIn + sweepOut);
                    sweepIn *= share;
                    sweepOut *= share;
                }

                float entryBearing = pondBearing + sign * sweepIn;
                IntroLanding.Release(plan, plan[^1], brookHeading, IntroGround.Heading(entryBearing) * radius, Tangent(entryBearing, sign), STEP);
                circleFrom = plan.Count - 1;

                float arc = turn - sweepIn - sweepOut;
                int arcSteps = (int)MathF.Ceiling(arc * radius / STEP);
                for (int i = 1; i <= arcSteps; i++)
                    plan.Add(IntroGround.Heading(entryBearing + sign * arc * i / arcSteps) * radius);
                circle = arc * radius;

                releaseStart = plan.Count - 1;
                IntroLanding.TurnIn(plan, entryBearing + sign * arc, radius, sign, end, slant, STEP);
                arrive = Vector2.Normalize(plan[^1] - plan[^2]);
            }

            if (!IntroLanding.NoTighterThan(plan, brookEnd, MIN_TURN_RADIUS, releaseStart + 3)
                || !IntroLanding.NoTighterThan(plan, releaseStart, RELEASE_TURN_RADIUS)) return null;

            //The lengths; where the climb starts (past the pond's middle), the pace picks up, it reaches the cruise's and the
            //deceleration begins (the turn in off the circle, or the last stretch of a release straight off the pond)
            var length = new float[plan.Count];
            for (int i = 1; i < plan.Count; i++) length[i] = length[i - 1] + Vector2.Distance(plan[i - 1], plan[i]);
            float total = length[^1];

            cruise = MathF.Min(MathF.Max(cruise, circle / CIRCLE_SECONDS), CRUISE_FASTEST);

            //The deceleration takes at least SETTLE_SECONDS (the shared landing's lasts ReleaseTimeScale times its length over the
            //speed it enters with): begun only at the turn in, a fast cruise came onto the pose at 9 u/s in its last half second
            float settle = MathF.Max(cruise * SETTLE_SECONDS / Landing.ReleaseTimeScale,
                direct ? SETTLE_LENGTH : total - length[releaseStart]);
            int settleFrom = plan.Count - 2;
            while (settleFrom > brookEnd && total - length[settleFrom] < settle) settleFrom--;

            float sRise = length[brookEnd] + risePast;
            float sAccelerate = length[brookEnd] - ACCELERATE_BEFORE_MIDDLE;
            float sCruise = direct ? length[settleFrom] : MathF.Max(length[circleFrom], sRise + climb * 0.5f);
            sCruise = MathF.Min(sCruise, length[settleFrom]);

            //The cruise's height: over the highest ground the lens passes after the pond
            float top = float.MinValue;
            for (int i = brookEnd; i <= releaseStart; i++) top = MathF.Max(top, TerrainMirror.MeadowGround(plan[i].X, plan[i].Y, meadow));
            float cruiseY = top + above;

            //The heights and the speeds: the brook's height over the field easing up to the cruise's over the climb, and the
            //brook's pace up to the cruise's; over the release, the height eased from where it was onto the pose's while the
            //shared landing decelerates
            var path = new List<Vector3>(plan.Count);
            var speed = new float[plan.Count];
            var lean = new float[plan.Count];
            float releaseY = 0f, sRelease = length[releaseStart];
            for (int i = 0; i < plan.Count; i++)
            {
                float s = length[i], y;
                if (i <= releaseStart)
                {
                    float low = TerrainMirror.MeadowGround(plan[i].X, plan[i].Y, meadow) + BROOK_ABOVE;
                    y = MathHelper.Lerp(low, cruiseY, MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((s - sRise) / climb, 0f, 1f)));
                    releaseY = y;
                }
                else y = MathHelper.Lerp(releaseY, endPosition.Y, MathHelper.SmoothStep(0f, 1f, (s - sRelease) / MathF.Max(total - sRelease, 1e-3f)));

                if (i <= settleFrom)
                    speed[i] = MathHelper.Lerp(BROOK_SPEED, cruise, MathHelper.SmoothStep(0f, 1f,
                        MathHelper.Clamp((s - sAccelerate) / MathF.Max(sCruise - sAccelerate, 1f), 0f, 1f)));

                //The look down the brook and over the pond, turning in towards the island as the lens comes off the brook's line
                lean[i] = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((s - length[brookEnd]) / MathF.Max(sCruise - length[brookEnd], 1f), 0f, 1f));

                path.Add(new Vector3(plan[i].X, y, plan[i].Y));
            }
            path[^1] = endPosition;

            //How long it will take (the shared landing's own timing), so the one nearest TARGET_SECONDS is preferred
            float estimate = 0f;
            for (int i = 1; i <= settleFrom; i++) estimate += (length[i] - length[i - 1]) / speed[i];
            estimate += Landing.ReleaseTimeScale * (total - length[settleFrom]) / speed[settleFrom];

            //The best is the length asked for, arriving along the pose's facing, climbing early
            float swivel = MathF.Acos(MathHelper.Clamp(Vector2.Dot(arrive, facing), -1f, 1f));

            return new IntroLanding.Route
            {
                Path = path, Speed = speed, DecelerateFrom = settleFrom, Lean = lean,
                Score = -MathF.Abs(estimate - TARGET_SECONDS) - 2f * swivel - RISE_LATE_COST * risePast + (float)random.NextDouble() * 0.5f,
            };
        }

        /// <summary>
        /// Swerves the brook's run off its line where a trunk stands nearer it than its roots and <see cref="TRUNK_AIR"/>: by
        /// what it lacks at the run's nearest point, easing in and out on a cosine either side (see <see cref="ROOT_REACH"/>).
        /// </summary>
        private static void PassTheTrunks(List<Vector2> run, IReadOnlyList<PlantFigure> trees)
        {
            var offset = new float[run.Count];
            foreach (PlantFigure tree in trees)
            {
                Vector2 trunk = new(tree.Root.X, tree.Root.Z);
                float need = tree.Stem * ROOT_REACH + TRUNK_AIR + 0.5f;

                int nearest = 0;
                for (int i = 1; i < run.Count; i++)
                    if (Vector2.DistanceSquared(run[i], trunk) < Vector2.DistanceSquared(run[nearest], trunk)) nearest = i;
                if (Vector2.Distance(run[nearest], trunk) >= need) continue;

                Vector2 across = Across(run, nearest);
                float side = Vector2.Dot(trunk - run[nearest], across);
                float lacks = side - MathF.Sign(side == 0f ? 1f : side) * MathF.Sqrt(MathF.Max(need * need - Vector2.DistanceSquared(run[nearest], trunk) + side * side, 0f));

                float s = 0f;
                for (int i = nearest; i < run.Count; i++)
                {
                    if (i > nearest) s += Vector2.Distance(run[i - 1], run[i]);
                    if (s >= DEFLECT_DOWNSTREAM) break;
                    offset[i] += lacks * Bump(s / DEFLECT_DOWNSTREAM);
                }
                s = 0f;
                for (int i = nearest - 1; i >= 0; i--)
                {
                    s += Vector2.Distance(run[i + 1], run[i]);
                    if (s >= DEFLECT_UPSTREAM) break;
                    offset[i] += lacks * Bump(s / DEFLECT_UPSTREAM);
                }
            }

            var across2 = new Vector2[run.Count];
            for (int i = 0; i < run.Count; i++) across2[i] = Across(run, i);
            for (int i = 0; i < run.Count; i++) run[i] += across2[i] * offset[i];
        }

        //A cosine ease from 1 at 0 to 0 at 1, flat at both ends
        private static float Bump(float x) => 0.5f + 0.5f * MathF.Cos(MathF.PI * MathHelper.Clamp(x, 0f, 1f));

        //The unit vector to the left of a run at a point
        private static Vector2 Across(List<Vector2> run, int i)
        {
            Vector2 along = Vector2.Normalize(run[Math.Min(i + 1, run.Count - 1)] - run[Math.Max(i - 1, 0)]);
            return new Vector2(-along.Y, along.X);
        }

        /// <summary>The circle's direction of travel at <paramref name="bearing"/>, going round the way <paramref name="sign"/> says
        /// (positive is the bearing growing).</summary>
        private static Vector2 Tangent(float bearing, int sign) => new(-sign * MathF.Sin(bearing), sign * MathF.Cos(bearing));

        private static float Roll(Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();

        /// <summary>
        /// What a point of the flight may not be near (see the class's note): the ground, the arena nearer than the pose, the
        /// pond's reed clumps, the trees and every low solid. The solids are kept in a grid, since every point of every
        /// candidate is asked and the meadow plants several hundred.
        /// </summary>
        internal sealed class Obstacles
        {
            private readonly MeadowSceneConfig _meadow;
            private readonly float _keepOut;
            private readonly Vector2 _pond;
            private readonly IReadOnlyList<PlantFigure> _trees;
            private readonly List<PlantFigure> _solids = new();
            private readonly Dictionary<(int, int), List<int>> _byCell = new();

            /// <param name="keepOut">The nearest the axis a point may stand (<see cref="IntroLanding.KeepOutFor"/>).</param>
            public Obstacles(MeadowSceneConfig meadow, MeadowFigures planted, float keepOut)
            {
                _meadow = meadow;
                _keepOut = keepOut;
                (float pondX, float pondZ) = MeadowPath.PondCentre(meadow);
                _pond = new Vector2(pondX, pondZ);
                _trees = planted?.Trees ?? Array.Empty<PlantFigure>();
                if (planted?.Solids == null) return;

                foreach (PlantFigure solid in planted.Solids)
                {
                    //Every cell the solid's sphere or capsule, with the margin, can reach into
                    float reach = MathF.Max(solid.Reach, solid.Stem) + MARGIN;
                    float minX = MathF.Min(solid.Root.X, solid.Crown.X) - reach, maxX = MathF.Max(solid.Root.X, solid.Crown.X) + reach;
                    float minZ = MathF.Min(solid.Root.Z, solid.Crown.Z) - reach, maxZ = MathF.Max(solid.Root.Z, solid.Crown.Z) + reach;

                    int index = _solids.Count;
                    _solids.Add(solid);
                    for (int i = Cell(minX); i <= Cell(maxX); i++)
                        for (int j = Cell(minZ); j <= Cell(maxZ); j++)
                        {
                            if (!_byCell.TryGetValue((i, j), out List<int> list)) _byCell[(i, j)] = list = new List<int>();
                            list.Add(index);
                        }
                }
            }

            private static int Cell(float v) => (int)MathF.Floor(v / CELL);

            /// <summary>Whether a lens at <paramref name="p"/> is clear of everything the flight keeps off.</summary>
            public bool Clear(Vector3 p)
            {
                if (MathF.Sqrt(p.X * p.X + p.Z * p.Z) < _keepOut) return false;

                float ground = TerrainMirror.Meadow(p.X, p.Z, _meadow);
                if (p.X * p.X + p.Z * p.Z < ISLAND_FOOT_REACH * ISLAND_FOOT_REACH) ground = MathF.Max(ground, ISLAND_BANK_TOP);
                if (p.Y < ground + LOWEST) return false;

                float overField = p.Y - TerrainMirror.MeadowGround(p.X, p.Z, _meadow);
                if (overField < REED_TOP + MARGIN && InReeds(p)) return false;

                foreach (PlantFigure tree in _trees)
                {
                    if (IntroPaths.DistanceToSegment(p, tree.Root, tree.Crown) - tree.Stem * ROOT_REACH < TRUNK_AIR) return false;
                    if (overField > LOW_LENS && Vector3.Distance(p, tree.Crown) - tree.Reach < MARGIN) return false;
                }

                if (_byCell.TryGetValue((Cell(p.X), Cell(p.Z)), out List<int> near))
                    foreach (int index in near)
                    {
                        PlantFigure solid = _solids[index];
                        if (Vector3.Distance(p, solid.Crown) - solid.Reach < MARGIN) return false;
                        if (IntroPaths.DistanceToSegment(p, solid.Root, solid.Crown) - solid.Stem < MARGIN) return false;
                    }

                return true;
            }

            /// <summary>Whether the point stands in the band round the pond's shore where its reeds grow, within
            /// <see cref="REED_GAP"/> of a clump, off the brook's own bank.</summary>
            public bool InReeds(Vector3 p)
            {
                float shore = MeadowPath.PondShore(p.X, p.Z, _meadow);
                if (shore <= -REED_INSIDE || shore >= REED_OUTSIDE || MeadowCarve.OnBrookBank(p.X, p.Z, _meadow)) return false;

                float a = MathF.Atan2(p.Z - _pond.Y, p.X - _pond.X);
                for (int k = -2; k <= 2; k++)
                    if (MeadowPath.PondReedClump(a + k * REED_GAP * 0.5f) > MeadowPath.POND_REEDS_FROM) return true;

                return false;
            }
        }
    }
}
