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
    /// <b>The route.</b> From a start among the groves (<see cref="START_MIN"/> to <see cref="START_MAX"/> out, within
    /// <see cref="START_SPREAD"/> degrees of the pose's bearing) a short straight run heading for the pose give or take
    /// <see cref="HEADING_SPREAD"/> degrees, then the shared release onto it (<see cref="IntroLanding.Release"/>), arriving
    /// along the pose's facing, halfway, or still along the run (<see cref="IntroLanding.Arrival"/>). The lens flies
    /// <see cref="ABOVE_MIN"/> to <see cref="ABOVE_MAX"/> over the ground the way the arid shots do
    /// (<see cref="AridIntroPaths.Hug"/>: rounded over the run's own travel, never under that clearance), and over the last
    /// <see cref="CLIMB_MIN"/> to <see cref="CLIMB_MAX"/> units climbs onto the pose's height while the shared landing
    /// decelerates.
    /// </para>
    /// <para>
    /// <b>The pace.</b> A cruise of <see cref="CRUISE_MIN"/> to <see cref="CRUISE_MAX"/> units a second, which low over the
    /// grass reads fast (the owner prefers faster to slower; the arid shots that look at the ground under the lens are held to
    /// 28), then the shared deceleration to rest on the pose over at least <see cref="SETTLE_SECONDS"/>; the look along the
    /// travel turned half way in towards the arena (<see cref="IntroLanding.Style.LookInward"/>), so the island stands ahead
    /// the whole way; a light bank.
    /// </para>
    /// <para>
    /// <b>Found, not assumed.</b> Rolled candidates are each laid in full and refused if they turn tighter than the speed
    /// allows, if the cut from the shot before moves the lens too little, or if any point is under the ground or the bank,
    /// nearer the axis than the pose (<see cref="IntroLanding.KeepOutFor"/>), within <see cref="MARGIN"/> of anything planted,
    /// held as its <see cref="PlantFigure"/> (every instance of every mesh, the sphere round its mesh,
    /// <see cref="SavannaScatter.Solids"/>: NOT the spacing footprints, which a bush's crown overhangs), or within
    /// <see cref="FIRE_MARGIN"/> of a campfire's hearth and flame. Among the clear ones the one nearest the length asked for,
    /// arriving along the pose's facing, past the most trees on both hands, flies. ⚠ The bank round the island is one of the
    /// planting's solids, the sphere round its mesh (35 round the island's foot), and the game's lens stands inside it: it is
    /// held as ground here (<see cref="BANK_RISE"/>), as the meadow holds its bank, or no landing would ever clear. Null for a
    /// pose inside the island's radius (the menu's crane) or one that is not clear itself (beside a fire), and the savanna then
    /// plays the reel it had before, the tour's arrival leg included.
    /// </para>
    /// <para>Built once when the intro begins, never per frame.</para>
    /// </summary>
    internal static class SavannaLanding
    {
        /// <summary>The shot's name, in the intro's log line.</summary>
        internal const string NAME = "the grass home";

        //The start: among the groves (the acacias stand 60 to 340 out, SavannaSceneConfig's AcaciaConfig, the tufts from 40),
        //this far from the axis and up to this many degrees round from the pose's bearing, so the run heads in to the island
        internal const float START_MIN = 85f, START_MAX = 115f;
        internal const float START_SPREAD = 40f;

        //The run: heading for the pose give or take this many degrees, straight for this long before the release curves it
        //onto the pose
        private const float HEADING_SPREAD = 22f;
        private const float RUN_MIN = 6f, RUN_MAX = 22f;

        //How high over the grass: about the acacia track's 2.2 (SavannaIntroShots), the highest ground at each point and
        //HUG_LATERAL either side of it, rounded over the run's own travel (AridIntroPaths.Hug)
        internal const float ABOVE_MIN = 1.8f, ABOVE_MAX = 2.6f;
        private const float HUG_LATERAL = 1.5f;

        //The climb onto the pose's height: over this much of the way's end
        private const float CLIMB_MIN = 26f, CLIMB_MAX = 42f;

        //The pace: a cruise, then the shared deceleration, which takes at least SETTLE_SECONDS (the meadow's lesson: begun
        //late, a fast cruise came onto the pose at 9 u/s in its last half second)
        internal const float CRUISE_MIN = 20f, CRUISE_MAX = 24f;
        private const float SETTLE_SECONDS = 2.4f;

        //How long the flight should be: among clear candidates the one nearest it is preferred. It replaces the tour's
        //4.5-second arrival leg (ChapterIntro.ARRIVAL_SECONDS), and the owner prefers faster to slower
        private const float TARGET_SECONDS = 5f;

        //No tighter than this at the cruise: at 24 u/s a radius of 22 is 63 degrees a second of heading; the deceleration
        //onto the pose, slowing to rest, may take RELEASE_TURN_RADIUS
        private const float MIN_TURN_RADIUS = 22f;
        private const float RELEASE_TURN_RADIUS = 10f;

        //"Among the trees": a tree whose root stands within this of the low run's line in plan has been passed, on one hand or
        //the other (a tree's own sphere keeps the lens about 20 off its trunk); each hand counts up to TREES_COUNTED, and one
        //on each hand is worth more
        private const float TREE_NEAR = 36f;
        private const int TREES_COUNTED = 4;
        private const float TREE_WORTH = 0.5f, BOTH_HANDS_WORTH = 0.75f;

        //A cut from the shot before it must move the lens this far or turn the view this much (film's thirty-degree rule, the
        //cities' 40 units): two low shots over the grass from nearly one stand read as a glitch
        internal const float CUT_TRAVEL = 40f;
        internal const float CUT_TURN = 30f;

        //The plan's spacing, and the candidates rolled
        private const float STEP = 0.5f;
        private const int CANDIDATES = 64;

        //Clearances. Every point keeps MARGIN off every solid (the savanna's other shots' 1.2) and LOWEST over the ground. A
        //fire is its hearth and its flame: a capsule from its foot up the flame's height, as wide as the hearth's stones reach
        //(SavannaLand.FireReach), and the lens keeps FIRE_MARGIN off it. The pose's own distance less ARRIVAL_SLACK is the
        //nearest the axis the lens comes.
        private const float MARGIN = 1.2f;
        private const float LOWEST = 1.2f;
        internal const float FIRE_MARGIN = 1.5f;
        private const float ARRIVAL_SLACK = 0.5f;

        //The bank round the island (IslandBerm): out to BANK_REACH from the axis it stands up to BANK_RISE over the ground it
        //was built off (its section's 1.05 and 0.75 at its wander's lift of up to 1.4), and its inner ring up to BANK_TOP over
        //the island's foot (1.35 at that lift)
        private const float BANK_REACH = ArenaIsland.RADIUS + 8f;
        private const float BANK_RISE = 1.5f;
        private const float BANK_TOP = ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT + 1.9f;

        //How far outside the island's radius the pose must stand for a landing at all: the game's lens stands 28 to 45 out
        private const float ISLAND_CLEAR = 2f;

        //A cell of the solids' grid, world units
        private const float CELL = 12f;

        /// <summary>How the grass home is timed, pointed and banked once laid (<see cref="IntroLanding.Fly"/>): a little wider
        /// than the game's lens at first, as the savanna's other shots are, easing to it; the look along the travel turned half
        /// way in towards the arena, so the island stands ahead the whole way, landing over the last 1.8 s; a light bank.</summary>
        internal static readonly IntroLanding.Style Landing = new(WideLens: 1.1f, ReleaseTimeScale: 2f, LookPitchFollow: 0.3f,
            LookDistance: 30f, LookSmoothSeconds: 0.3f, LookLandSeconds: 1.8f, LookInward: 0.5f, RollPerTurn: 0.1f,
            MaxRollDegrees: 8f, RollSmoothSeconds: 0.25f, Points: 200);

        /// <summary>
        /// The grass home over <paramref name="land"/>, landing on <paramref name="endPosition"/> looking at
        /// <paramref name="endTarget"/> with the game's <paramref name="fieldOfView"/>; null when no rolled route is clear.
        /// </summary>
        /// <param name="cutFrom">The last pose of the shot before it (its lens and what it looks at), whose cut the start must
        /// move or turn enough from; null for none.</param>
        public static IntroShot Build(SavannaLand land, Vector3 endPosition, Vector3 endTarget, float fieldOfView, Random random,
            (Vector3 Position, Vector3 Target)? cutFrom = null)
        {
            if (land == null) return null;

            Vector2 end = new(endPosition.X, endPosition.Z);
            if (end.Length() < ArenaIsland.RADIUS + ISLAND_CLEAR) return null;

            Vector2 facing = new(endTarget.X - endPosition.X, endTarget.Z - endPosition.Z);
            if (facing.LengthSquared() < 1e-6f) return null;
            facing.Normalize();

            var obstacles = new Obstacles(land, IntroLanding.KeepOutFor(endPosition, IntroGround.ARENA_KEEP_OUT + MARGIN, ARRIVAL_SLACK));
            if (!obstacles.Clear(endPosition)) return null;

            IntroLanding.Route best = IntroLanding.Best(CANDIDATES, () => Lay(land, obstacles, endPosition, facing, random, cutFrom), obstacles.Clear);
            return best == null ? null : IntroLanding.Fly(NAME, best, endPosition, endTarget, fieldOfView, Landing);
        }

        private static IntroLanding.Route Lay(SavannaLand land, Obstacles obstacles, Vector3 endPosition, Vector2 facing, Random random,
            (Vector3 Position, Vector3 Target)? cutFrom)
        {
            float startBearing = MathHelper.ToRadians(AridIntroPaths.Roll(random, -START_SPREAD, START_SPREAD));
            float startDistance = AridIntroPaths.Roll(random, START_MIN, START_MAX);
            float headingTurn = MathHelper.ToRadians(AridIntroPaths.Roll(random, -HEADING_SPREAD, HEADING_SPREAD));
            float run = AridIntroPaths.Roll(random, RUN_MIN, RUN_MAX);
            float above = AridIntroPaths.Roll(random, ABOVE_MIN, ABOVE_MAX);
            float climb = AridIntroPaths.Roll(random, CLIMB_MIN, CLIMB_MAX);
            float cruise = AridIntroPaths.Roll(random, CRUISE_MIN, CRUISE_MAX);
            double arrival = random.NextDouble();
            float noise = (float)random.NextDouble();

            Vector2 end = new(endPosition.X, endPosition.Z);
            Vector2 start = IntroGround.Heading(IntroGround.Bearing(end) + startBearing) * startDistance;
            Vector2 toPose = Vector2.Normalize(end - start);
            float heading0 = MathF.Atan2(toPose.Y, toPose.X) + headingTurn;
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

            //The run, then the release onto the pose
            var plan = new List<Vector2>();
            IntroLanding.Straight(plan, start, start + heading * run, STEP);
            int releaseStart = plan.Count - 1;
            Vector2 arrive = IntroLanding.Arrival(arrival, facing, heading);
            IntroLanding.Release(plan, plan[^1], heading, end, arrive, STEP);

            //The lengths, and where the deceleration begins: at least SETTLE_SECONDS from the end at the cruise
            var length = new float[plan.Count];
            for (int i = 1; i < plan.Count; i++) length[i] = length[i - 1] + Vector2.Distance(plan[i - 1], plan[i]);
            float total = length[^1];

            float settle = cruise * SETTLE_SECONDS / Landing.ReleaseTimeScale;
            int settleFrom = plan.Count - 2;
            while (settleFrom > 0 && total - length[settleFrom] < settle) settleFrom--;
            if (settleFrom < 1) return null;

            if (!IntroLanding.NoTighterThan(plan, 0, MIN_TURN_RADIUS, settleFrom + 3)
                || !IntroLanding.NoTighterThan(plan, Math.Max(releaseStart, settleFrom), RELEASE_TURN_RADIUS)) return null;

            //The heights: low over the ground the way a drone flies it, rounded over the run's travel at the cruise (the hug
            //takes a shot's seconds at its eased clock's peak pace, so the cruise's seconds are handed over divided by it), then
            //eased onto the pose's height over the climb
            Vector3[] hug = AridIntroPaths.Hug(plan.ToArray(), obstacles.Ground, above, HUG_LATERAL,
                seconds: IntroShot.PEAK_PACE * total / cruise);
            float sClimb = MathF.Max(total - climb, 0f);

            var path = new List<Vector3>(plan.Count);
            var speed = new float[plan.Count];
            for (int i = 0; i < plan.Count; i++)
            {
                float rise = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((length[i] - sClimb) / MathF.Max(total - sClimb, 1e-3f), 0f, 1f));
                path.Add(new Vector3(plan[i].X, MathHelper.Lerp(hug[i].Y, endPosition.Y, rise), plan[i].Y));
                speed[i] = cruise;
            }
            path[^1] = endPosition;

            //How long it will take (the shared landing's own timing), so the one nearest TARGET_SECONDS is preferred
            float estimate = length[settleFrom] / cruise + Landing.ReleaseTimeScale * (total - length[settleFrom]) / cruise;

            //Among the trees: the trees the low run (before the climb) passes within TREE_NEAR of, on each hand
            int low = 0;
            while (low + 1 < plan.Count && length[low + 1] <= sClimb) low++;
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
        /// arena nearer than the pose, every campfire's hearth and flame, and everything planted. The solids are kept in a
        /// grid, since every point of every candidate is asked and the plain plants about a thousand.
        /// </summary>
        internal sealed class Obstacles
        {
            private readonly SavannaLand _land;
            private readonly float _keepOut;
            private readonly List<PlantFigure> _solids = new();
            private readonly Dictionary<(int, int), List<int>> _byCell = new();

            /// <param name="keepOut">The nearest the axis a point may stand (<see cref="IntroLanding.KeepOutFor"/>).</param>
            public Obstacles(SavannaLand land, float keepOut)
            {
                _land = land;
                _keepOut = keepOut;

                foreach (PlantFigure solid in land.Solids)
                {
                    //The bank round the island, the one solid centred on the axis: held as ground (BANK_RISE), see the class's note
                    if (IsBank(solid)) continue;

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

            /// <summary>Whether <paramref name="solid"/> is the bank round the island (<c>IslandBerm</c>'s two meshes, the
            /// one thing planted on the axis), which the flight holds as ground rather than as a sphere.</summary>
            internal static bool IsBank(PlantFigure solid) => new Vector2(solid.Crown.X, solid.Crown.Z).Length() < ArenaIsland.RADIUS;

            private static int Cell(float v) => (int)MathF.Floor(v / CELL);

            /// <summary>The ground a lens is held over: the land, and the bank round the island where it stands.</summary>
            public float Ground(float x, float z)
            {
                float ground = _land.Height(x, z);
                return x * x + z * z < BANK_REACH * BANK_REACH ? MathF.Max(ground + BANK_RISE, BANK_TOP) : ground;
            }

            /// <summary>Whether a lens at <paramref name="p"/> is clear of everything the flight keeps off.</summary>
            public bool Clear(Vector3 p)
            {
                if (MathF.Sqrt(p.X * p.X + p.Z * p.Z) < _keepOut) return false;
                if (p.Y < Ground(p.X, p.Z) + LOWEST) return false;

                foreach (Vector3 fire in _land.Fires)
                    if (IntroPaths.DistanceToSegment(p, fire, fire + Vector3.Up * _land.FireHeight) - _land.FireReach < FIRE_MARGIN) return false;

                if (_byCell.TryGetValue((Cell(p.X), Cell(p.Z)), out List<int> near))
                    foreach (int index in near)
                    {
                        PlantFigure solid = _solids[index];
                        if (Vector3.Distance(p, solid.Crown) - solid.Reach < MARGIN) return false;
                        if (IntroPaths.DistanceToSegment(p, solid.Root, solid.Crown) - solid.Stem < MARGIN) return false;
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
    /// <param name="Acacias">The umbrella-crowned acacias (<see cref="SavannaScatter.Acacias"/>).</param>
    /// <param name="Baobabs">The baobabs (<see cref="SavannaScatter.Baobabs"/>).</param>
    /// <param name="Fires">Each campfire's foot (<see cref="SceneRenderer.SavannaCampfirePosition"/>).</param>
    /// <param name="FireReach">How far a fire's hearth and flame reach from its axis.</param>
    /// <param name="FireHeight">How tall its flame stands over its foot.</param>
    internal sealed record SavannaLand(Func<float, float, float> Height, IReadOnlyList<PlantFigure> Solids,
        IReadOnlyList<PlantFigure> Acacias, IReadOnlyList<PlantFigure> Baobabs, IReadOnlyList<Vector3> Fires, float FireReach, float FireHeight)
    {
        //A hearth's stones (SavannaBackdrop.BuildHearthStones): a ring of FlameSize * StoneRingScale, each stone jittered out to
        //HEARTH_RING_OUT of it, a stone FlameSize * StoneSizeScale times up to 1.18 for its variant, 1.34 for its lathe's
        //wobble and 1.27 for its scale. Wider than the flame's own volume (1.45 fire sizes), so it is the fire's reach.
        private const float HEARTH_RING_OUT = 1.12f;
        private const float HEARTH_STONE_REACH = 1.18f * 1.34f * 1.27f;

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
            return new SavannaLand(height, planting.Solids, planting.Acacias, planting.Baobabs, fires,
                fire.FlameSize * (fire.StoneRingScale * HEARTH_RING_OUT + fire.StoneSizeScale * HEARTH_STONE_REACH),
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
