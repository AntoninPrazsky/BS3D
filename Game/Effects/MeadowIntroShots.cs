using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The meadow's own shots for a chapter intro's prologue (#559, #609, #893): the valley from a hilltop, then a slow
    /// half circle round a group of hay bales, then down the brook at the height of its reeds to the pond it runs into,
    /// then along the footpath beside its fence to the old oak on its knoll, and last a slow half circle round the bench
    /// under that oak — cut together, then cut to the tour's last leg. The meadow opens the whole campaign, so this is
    /// the first thing the game shows a new player after its menu.
    /// <para>
    /// <b>Every one is about a thing since #609.</b> Until then the meadow had nothing standing on it and its shots were
    /// about pieces of the land — the flowers at a hand's height and the climb of a slope; the rule the prologues are held
    /// to (#559) is "each shot about one object", and the owner praised the intros that follow concrete things. The brook
    /// and the path are analytic lines on the field (<see cref="MeadowPath"/>), the very ones <c>Meadow.fx</c> draws, so a
    /// shot can follow one without asking the planting; and the planting keeps off both, so a lens on either centreline has
    /// nothing to run into. Every height is read off <see cref="TerrainMirror.Meadow"/>. Built once when the intro begins.
    /// </para>
    /// <para>
    /// <b>The brook and the path walk TOWARDS what their line arrives at since #609's third round</b>, and keep the lens
    /// on it: the owner found them stopping short in the grass, and they arrive at a pond and at an old tree now
    /// (<see cref="MeadowPath.PondCentre"/>, <see cref="MeadowPath.PathEnd"/>). Until then both shots ran outwards, away
    /// from the arena, which was also away from anything; walked the other way the brook is followed downstream and the
    /// path is walked the way a walker walks it, to the tree.
    /// </para>
    /// <para>
    /// <b>The bales and the bench are looked at close and low, from a lens that goes half way round them</b> (#893, the
    /// owner's playtest: "a detailed low-angle shot of the bench under the tree and of the cylindrical hay bales — the
    /// camera moves in a half circle very slowly and is close to the objects"). Each is <see cref="IntroGround.Arc"/> of
    /// <see cref="MathF.PI"/> round the subject, 0.7 rising to 1.1 over the ground (the owner's note says 0.6 to 1.5),
    /// <see cref="ORBIT_SECONDS"/> long, the lens held on the subject's middle. The radius is the subject's own figure and
    /// nothing written here — the bench and the bales were resized once each (#891, #864) and will be again — searched out
    /// from the figure's sphere until the whole subject is in frame, so it follows them. ⚠ <b>The lens keeps off the
    /// old trees by their STEM, not their crown</b> (<see cref="MeadowFigures.Trees"/>): the oak's crown sphere is its
    /// mesh's bounding sphere, which reaches below the ground, so a lens anywhere under the tree is inside it and every
    /// arc round the bench would be refused. What else stands low (<see cref="MeadowFigures.Solids"/>) is kept out of by
    /// its own sphere or capsule, and a lens never crosses the brook or the pond, whose bed is under water.
    /// </para>
    /// </summary>
    internal static class MeadowIntroShots
    {
        //The valley: a level dolly in from the hills towards the arena, this high over the highest ground
        //under it, on the hilliest of a few rolled bearings — a view from a hilltop down into the basin. 2.8 s since the
        //bench and the bales were added (#893), at the speed it had: the run is as much shorter as the time (35 units in
        //3.4 s before).
        private const float VALLEY_FROM = 195f;
        private const float VALLEY_TO = 167f;
        private const float VALLEY_ABOVE_FROM = 26f;
        private const float VALLEY_ABOVE_TO = 18f;
        private const float VALLEY_SECONDS = 2.8f;

        //The brook: down its centreline a little over the water, the reeds and the stones passing either side, the lens
        //on the pond ahead. A stretch of it that ends short of the pond's bank, where the brook's own banks are planted.
        private const float BROOK_FROM = 22f;              //past the brook's inner end, where the stretch finishes, clear of the pond's reeds
        private const float BROOK_RUN = 32f;
        private const float BROOK_ABOVE = 1.8f;             //at 1.1 the bank stones and the reeds filled the corners of the frame
        private const float BROOK_SECONDS = 3.2f;

        //The path: along its centreline at a walker's height, the fence beside it, the lens on the old tree it leads to,
        //stopping short of its crown
        private const float PATH_FROM = 22f;               //past the path's end, where the walk finishes
        private const float PATH_RUN = 40f;
        private const float PATH_ABOVE = 1.7f;
        private const float PATH_TREE_LOOK_UP = 7f;         //the point on the tree the lens holds: the trunk under the crown
        private const float PATH_SECONDS = 3.2f;

        //The two orbits (#893): a half circle each, this long — the owner's "very slowly": half a turn in 4.6 s is 39
        //degrees a second round the subject, which is 4 units a second along the bench's radius and 7 along a pair of
        //bales' (the brook shot's 10). The lens stands this high over the ground under it, rising a little: a low angle,
        //looking up at the subject. Over 48 layouts the bales' mean radius was 10.2 with the lens at 0.7 to 1.1 and 10.3 at 0.9
        //to 1.3: it is the subject's height (3.2 for a bale) against the lens's 47 degrees that sets the radius, not the lens's.
        private const float ORBIT_SECONDS = 4.6f;
        private const float ORBIT_ABOVE_FROM = 0.7f;
        private const float ORBIT_ABOVE_TO = 1.1f;

        //The lens is never nearer the ground than this, whatever the ground does between the points the path is built at
        private const float LOWEST_LENS = 0.5f;

        //The nearest the lens comes: the subject's sphere and this much air (the sphere is the mesh's bounding figure, loose
        //round a bench's flat shape, so it stands farther off than it reads). The radius grows from there in steps until the
        //whole subject is in frame, and no farther than this multiple of the nearest: a lens farther out is not close, and
        //round a wide group it is not slow.
        private const float ORBIT_AIR = 2.2f;
        private const float RADIUS_STEP = 0.75f;
        private const float RADIUS_LIMIT = 1.75f;

        //The frame the subject must stay in (the test of it, MeadowIntroShotsTests, holds it to 0.92 and -0.74 over the poses it
        //walks, and this is the same asked at a few stands along the path and a little inside it): the 16:9 window, the
        //narrowest the game is played in (the projection is Hor+, so a wider window only adds room at the sides); the share of
        //the frame's half height and half width a point may reach; and how far down a point standing in the skip hint's width
        //may go, the hint taking up to 12.2 % of the frame's height at the bottom centre
        private const float NARROWEST_ASPECT = 16f / 9f;
        private const float FRAME_LIMIT = 0.88f;
        private const float FRAME_FLOOR = -0.70f;
        private const float HINT_HALF_WIDTH = 0.35f;
        private const int FRAME_STANDS = 13;

        //A subject's footprint, as the figure gives it: a bench or a bale stands inside a circle of this share of its sphere's
        //radius (the bale's 2.15 of 2.52, the bench's 1.94 of 2.6), and as high as twice its sphere's centre is over its root.
        //Held as a cylinder, which a bench's flat shape and a bale on its side both fit inside.
        private const float FOOTPRINT_SHARE = 0.85f;
        private const int RIM_POINTS = 8;

        //Two bales are one group when their spheres stand within this much of touching
        private const float BALE_LINK_GAP = 0.8f;

        //Every lens point keeps this much air off every solid (the savanna's 1.2, a little more where the lens is lower than
        //anything it passes), and is never closer than this to the water's edge, past which the reeds stand (the pond's clumps
        //reach 1.5 over its shore and the brook's reach 1.5 past its bank)
        private const float MARGIN = 1.5f;
        private const float WATER_MARGIN = 3f;

        //A sight line to the subject ends this much outside its sphere, not on it: ending on the surface, rounding decides
        //whether the last sample is inside the subject's own solid, and the shot is refused by a coin
        private const float SIGHT_SLACK = 0.5f;

        //The solids that count for an orbit are the ones within its radius and this much more, so as to hold the whole
        //of a fence span whose ends are both farther off than its middle
        private const float SOLID_PAD = 8f;

        private const int TRIES = 24;

        /// <summary>The prologue for the meadow, or null when there is no meadow config.</summary>
        /// <param name="planted">The planting's figures (<see cref="SceneRenderer.MeadowFigures"/>): the old trees, up to 36
        /// tall since #609's third round and kept out of the valley shot's flight, which rides 18 to 26 over the highest
        /// ground under it; the bench and the bales the two orbits are about; and what else stands low. Null keeps nothing
        /// out and has no bench and bales to show.</param>
        public static IntroShot[] Build(MeadowSceneConfig meadow, MeadowFigures planted, float fieldOfView, Random random)
        {
            if (meadow == null) return null;

            var ground = new IntroGround((x, z) => TerrainMirror.Meadow(x, z, meadow));
            if (planted?.Trees != null)
                foreach (PlantFigure tree in planted.Trees) ground.Add(tree);

            //The hilliest bearing: the ground at the run's start, where the lens stands on the hill.
            IntroShot valley = ground.Establishing("the valley", VALLEY_FROM, VALLEY_TO, VALLEY_ABOVE_FROM, VALLEY_ABOVE_TO,
                VALLEY_SECONDS, fieldOfView * 1.1f, random, TRIES, margin: 2f,
                score: bearing => ground.Height(MathF.Cos(bearing) * VALLEY_FROM, MathF.Sin(bearing) * VALLEY_FROM));

            //The order alternates what the lens does: the valley, a slow orbit, two runs along the land, and the orbit the
            //last run arrives at — the bench stands under the tree the path leads to
            return IntroGround.Cut(valley, Bales(meadow, planted, fieldOfView, random), Brook(meadow, fieldOfView),
                Path(meadow, fieldOfView), Bench(meadow, planted, fieldOfView, random));
        }

        /// <summary>Down the brook a little over the water, the reeds passing either side, to the pond it runs into.</summary>
        private static IntroShot Brook(MeadowSceneConfig meadow, float fieldOfView)
        {
            float end = meadow.ClearingRadius * MeadowPath.BROOK_START + BROOK_FROM;
            var path = new Vector3[IntroPaths.FINE_POINTS];
            for (int i = 0; i < path.Length; i++)
            {
                float d = end + BROOK_RUN * (1f - i / (path.Length - 1f));
                (float x, float z) = MeadowPath.BrookPoint(d, 0f, meadow);
                path[i] = new Vector3(x, TerrainMirror.Meadow(x, z, meadow) + BROOK_ABOVE, z);
            }

            (float pondX, float pondZ) = MeadowPath.PondCentre(meadow);
            return new IntroShot("the brook", path, BROOK_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(pondX, TerrainMirror.Meadow(pondX, pondZ, meadow), pondZ));
        }

        /// <summary>Along the footpath at a walker's height, the fence beside it, to the old tree it leads to.</summary>
        private static IntroShot Path(MeadowSceneConfig meadow, float fieldOfView)
        {
            float end = meadow.ClearingRadius * MeadowPath.START + PATH_FROM;
            var path = new Vector3[IntroPaths.FINE_POINTS];
            for (int i = 0; i < path.Length; i++)
            {
                float d = end + PATH_RUN * (1f - i / (path.Length - 1f));
                float angle = meadow.PathBearing + MeadowPath.Wander(d, meadow) / d;
                float x = MathF.Cos(angle) * d, z = MathF.Sin(angle) * d;
                path[i] = new Vector3(x, TerrainMirror.Meadow(x, z, meadow) + PATH_ABOVE, z);
            }

            (float treeX, float treeZ) = MeadowPath.PathEnd(meadow);
            return new IntroShot("the path", path, PATH_SECONDS, fieldOfView * 1.1f,
                lookAt: new Vector3(treeX, TerrainMirror.Meadow(treeX, treeZ, meadow) + PATH_TREE_LOOK_UP, treeZ));
        }

        /// <summary>
        /// Half a turn round the bench under the old oak (#893), on the arena's side of it: the bench faces the arena and the
        /// oak stands behind it, so the lens sweeps across the seat's face with the trunk and the crown for its background.
        /// Null when the planting has no bench, or when no half circle on that side clears what stands there.
        /// </summary>
        internal static IntroShot Bench(MeadowSceneConfig meadow, MeadowFigures planted, float fieldOfView, Random random)
        {
            if (planted == null || planted.Bench.Reach <= 0f) return null;

            PlantFigure bench = planted.Bench;
            Vector2 centre = new(bench.Crown.X, bench.Crown.Z);
            float nearest = bench.Reach + ORBIT_AIR;
            IntroGround low = LowGround(meadow, planted, centre, nearest * RADIUS_LIMIT);

            //The facing is the arena's direction, to within the few degrees the bench stands round the oak from it
            //(MeadowScatter.BENCH_ROUND): the half circle runs from the bench's one flank to the other through its face
            float toArena = IntroGround.Bearing(-centre);
            float first = random.Next(2) == 0 ? 1f : -1f;
            var members = new[] { bench };

            for (float radius = nearest; radius <= nearest * RADIUS_LIMIT; radius += RADIUS_STEP)
                for (int turn = 0; turn < 2; turn++)
                {
                    float sign = turn == 0 ? first : -first;
                    IntroShot shot = Orbit("the bench", low, meadow, bench.Crown, members, bench.Reach, radius,
                        toArena - sign * MathF.PI * 0.5f, sign, fieldOfView);
                    if (shot != null) return shot;
                }

            return null;
        }

        /// <summary>
        /// Half a turn round a group of hay bales (#893): the group is the bales that stand within a touch of one another,
        /// a pair before a lone bale before a row of three, nearest the arena first. A group is passed over where no half
        /// circle of its own radius clears what stands round it.
        /// </summary>
        internal static IntroShot Bales(MeadowSceneConfig meadow, MeadowFigures planted, float fieldOfView, Random random)
        {
            if (planted == null || planted.Bales == null || planted.Bales.Count == 0) return null;

            foreach (BaleGroup group in Ordered(planted.Bales))
            {
                float nearest = group.Reach + ORBIT_AIR;
                Vector2 centre = new(group.Look.X, group.Look.Z);
                IntroGround low = LowGround(meadow, planted, centre, nearest * RADIUS_LIMIT);

                var members = new PlantFigure[group.Count];
                for (int m = 0; m < members.Length; m++) members[m] = planted.Bales[group.Members[m]];

                for (float radius = nearest; radius <= nearest * RADIUS_LIMIT; radius += RADIUS_STEP)
                    for (int attempt = 0; attempt < TRIES; attempt++)
                    {
                        float from = (float)random.NextDouble() * MathHelper.TwoPi;
                        float sign = random.Next(2) == 0 ? 1f : -1f;
                        IntroShot shot = Orbit("the bales", low, meadow, group.Look, members, group.Reach, radius, from, sign, fieldOfView);
                        if (shot != null) return shot;
                    }
            }

            return null;
        }

        /// <summary>
        /// One half circle (<see cref="MathF.PI"/>) round <paramref name="look"/> from the bearing <paramref name="from"/> in
        /// the direction <paramref name="sign"/>, or null when it is not good: the subject (<paramref name="members"/>) is in
        /// frame from every stand along it, the lens is never over the water, every point keeps <see cref="MARGIN"/> off every
        /// solid, and the subject is seen from every stand.
        /// </summary>
        private static IntroShot Orbit(string name, IntroGround low, MeadowSceneConfig meadow, Vector3 look,
            PlantFigure[] members, float reach, float radius, float from, float sign, float fieldOfView)
        {
            Vector3[] path = low.Arc(new Vector2(look.X, look.Z), radius, from, from + sign * MathF.PI,
                ORBIT_ABOVE_FROM, ORBIT_ABOVE_TO, IntroGround.PATH_POINTS);

            float lens = fieldOfView * 1.1f;
            if (!Dry(meadow, path)) return null;
            if (!Framed(path, look, members, lens)) return null;
            if (!low.Clear(path, MARGIN, LOWEST_LENS)) return null;
            if (!low.SightClear(path, look, reach + SIGHT_SLACK)) return null;

            return new IntroShot(name, path, ORBIT_SECONDS, lens, lookAt: look);
        }

        /// <summary>
        /// Whether the whole subject stays in frame from <see cref="FRAME_STANDS"/> stands along the path, in the narrowest
        /// window the game is played in, and its foot over the skip hint. Close and low, a subject is cut by the frame's
        /// bottom edge long before its sides: it is 3.2 high at four units, which is the lens's whole view at an angle
        /// of forty-seven degrees. The subject is a cylinder over each figure's footprint (<see cref="FOOTPRINT_SHARE"/>),
        /// as high as twice its centre stands over its root.
        /// </summary>
        private static bool Framed(Vector3[] path, Vector3 look, PlantFigure[] members, float fieldOfView)
        {
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(fieldOfView, NARROWEST_ASPECT, 0.1f, 5000f);

            for (int stand = 0; stand < FRAME_STANDS; stand++)
            {
                Vector3 lens = path[(int)MathF.Round(stand / (FRAME_STANDS - 1f) * (path.Length - 1))];
                Matrix viewProjection = Matrix.CreateLookAt(lens, look, Vector3.Up) * projection;

                foreach (PlantFigure member in members)
                {
                    float footprint = member.Reach * FOOTPRINT_SHARE, height = (member.Crown.Y - member.Root.Y) * 2f;
                    for (int k = 0; k < RIM_POINTS; k++)
                    {
                        float turn = k * MathHelper.TwoPi / RIM_POINTS;
                        Vector3 foot = new(member.Root.X + MathF.Cos(turn) * footprint, member.Root.Y, member.Root.Z + MathF.Sin(turn) * footprint);
                        if (!InFrame(viewProjection, foot) || !InFrame(viewProjection, foot + Vector3.Up * height)) return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Whether a point is inside the frame's limits and, standing in the skip hint's width, over its top.</summary>
        private static bool InFrame(Matrix viewProjection, Vector3 point)
        {
            Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
            if (clip.W <= 0f) return false;

            float x = clip.X / clip.W, y = clip.Y / clip.W;
            if (MathF.Abs(x) > FRAME_LIMIT || MathF.Abs(y) > FRAME_LIMIT) return false;

            return MathF.Abs(x) > HINT_HALF_WIDTH || y >= FRAME_FLOOR;
        }

        /// <summary>Whether no point of a path is over the pond or the brook, or near enough to stand in the reeds that grow on them.</summary>
        private static bool Dry(MeadowSceneConfig meadow, Vector3[] path)
        {
            foreach (Vector3 p in path)
                if (MeadowPath.PondShore(p.X, p.Z, meadow) < WATER_MARGIN
                    || MathF.Abs(MeadowPath.BrookLateral(p.X, p.Z, meadow)) < meadow.BrookWidth * 0.5f + WATER_MARGIN)
                    return false;

            return true;
        }

        /// <summary>
        /// The ground for the two low shots: the meadow's height and what a lens at a walker's height can run into — the
        /// arena's column, every old tree by its STEM and no more (see the class's note), and the low solids within reach of
        /// the orbit round <paramref name="centre"/>. Only the near ones, because every point of every try is held against
        /// every solid and the meadow plants several hundred.
        /// </summary>
        private static IntroGround LowGround(MeadowSceneConfig meadow, MeadowFigures planted, Vector2 centre, float radius)
        {
            var low = new IntroGround((x, z) => TerrainMirror.Meadow(x, z, meadow));

            if (planted.Trees != null)
                foreach (PlantFigure tree in planted.Trees) low.Add(tree.Root, tree.Crown, tree.Stem);

            if (planted.Solids != null)
                foreach (PlantFigure solid in planted.Solids)
                {
                    float near = MathF.Min(Vector2.Distance(centre, new Vector2(solid.Root.X, solid.Root.Z)),
                        Vector2.Distance(centre, new Vector2(solid.Crown.X, solid.Crown.Z)));
                    if (near - solid.Reach - solid.Stem < radius + SOLID_PAD) low.Add(solid);
                }

            return low;
        }

        /// <summary>One group of bales: where to look (the middle of their spheres), how far their spheres reach from there, and which of the bales they are.</summary>
        internal readonly record struct BaleGroup(Vector3 Look, float Reach, int[] Members)
        {
            /// <summary>How many bales.</summary>
            public int Count => Members.Length;

            /// <summary>Where it stands in plan.</summary>
            public Vector2 Centre => new(Look.X, Look.Z);
        }

        /// <summary>
        /// The order a group is tried in: a pair, then a lone bale, then a row of three (then anything longer, shortest first).
        /// The lone bale ranks before the row of three, which "the distance from two" would not say: both are one away from it.
        /// </summary>
        internal static int ShapeRank(int count) => count == 2 ? 0 : count == 1 ? 1 : count;

        /// <summary>
        /// The groups of <paramref name="bales"/> (<see cref="Groups"/>) in the order <see cref="Bales"/> tries them: by
        /// <see cref="ShapeRank"/>, and within a shape the nearest the arena's middle first.
        /// </summary>
        internal static List<BaleGroup> Ordered(IReadOnlyList<PlantFigure> bales)
        {
            List<BaleGroup> groups = Groups(bales);
            groups.Sort((a, b) =>
            {
                int byShape = ShapeRank(a.Count).CompareTo(ShapeRank(b.Count));
                return byShape != 0 ? byShape : a.Centre.LengthSquared().CompareTo(b.Centre.LengthSquared());
            });
            return groups;
        }

        /// <summary>The bales gathered into groups: any two within <see cref="BALE_LINK_GAP"/> of touching are one.</summary>
        internal static List<BaleGroup> Groups(IReadOnlyList<PlantFigure> bales)
        {
            //Each bale's group by a representative, found by walking links; a handful of bales, so no cleverness
            var leader = new int[bales.Count];
            for (int i = 0; i < leader.Length; i++) leader[i] = i;

            int Find(int i)
            {
                while (leader[i] != i) i = leader[i] = leader[leader[i]];
                return i;
            }

            for (int i = 0; i < bales.Count; i++)
                for (int j = i + 1; j < bales.Count; j++)
                {
                    float apart = Vector3.Distance(bales[i].Crown, bales[j].Crown);
                    if (apart <= bales[i].Reach + bales[j].Reach + BALE_LINK_GAP) leader[Find(j)] = Find(i);
                }

            var groups = new List<BaleGroup>();
            for (int i = 0; i < bales.Count; i++)
            {
                if (Find(i) != i) continue;

                var members = new List<int>();
                for (int j = 0; j < bales.Count; j++)
                    if (Find(j) == i) members.Add(j);

                Vector3 look = Vector3.Zero;
                foreach (int member in members) look += bales[member].Crown;
                look /= members.Count;

                float reach = 0f;
                foreach (int member in members)
                    reach = MathF.Max(reach, Vector2.Distance(new Vector2(look.X, look.Z), new Vector2(bales[member].Crown.X, bales[member].Crown.Z))
                        + bales[member].Reach);

                groups.Add(new BaleGroup(look, reach, members.ToArray()));
            }

            return groups;
        }
    }
}
