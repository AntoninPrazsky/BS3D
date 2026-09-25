using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The ground and everything standing on it, as a camera path has to see them (#559): a height field and
    /// a list of solids, and the tests a prologue shot's builder asks of a path before it takes it. What the
    /// city's shots get from its street grid for free — a line the towers cannot stand on — an open scene has
    /// to measure: the meadow's hills, the savanna's planting, the forest's trunks and the beach's palms are
    /// wherever the scene's own roll put them, and a lens path is only safe once every point of it has been
    /// held against all of them.
    /// <para>
    /// <b>Every solid is a capsule</b> — a segment and a radius — because every planted thing is either a
    /// stem (root to crown) or a mass (a crown, a rock), and a capsule with its two ends at one point is a
    /// sphere. A plant is taken at its <see cref="PlantFigure"/>: the stem capsule and the crown sphere. The
    /// arena itself is one more, a column round the axis wide enough to keep the island, the cluster, the gun
    /// and the glass all out of every shot.
    /// </para>
    /// <para>
    /// Built once when the intro begins and then only asked; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal sealed class IntroGround
    {
        //The column round the arena no shot may enter: the island's radius (26) with the cannon's orbit and
        //the hanging field's footprint inside it, and air to spare.
        public const float ARENA_KEEP_OUT = 44f;

        private readonly Func<float, float, float> _height;
        private readonly List<(Vector3 A, Vector3 B, float Radius)> _solids = new();

        public IntroGround(Func<float, float, float> height)
        {
            _height = height;
            _solids.Add((new Vector3(0f, -400f, 0f), new Vector3(0f, 400f, 0f), ARENA_KEEP_OUT));
        }

        /// <summary>The ground under a point.</summary>
        public float Height(float x, float z) => _height(x, z);

        /// <summary>A plant, as its stem and its crown.</summary>
        public void Add(PlantFigure plant)
        {
            _solids.Add((plant.Root, plant.Crown, plant.Stem));
            _solids.Add((plant.Crown, plant.Crown, plant.Reach));
        }

        /// <summary>A capsule of <paramref name="radius"/> round the segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public void Add(Vector3 a, Vector3 b, float radius) => _solids.Add((a, b, radius));

        /// <summary>
        /// Whether every point of a path keeps <paramref name="margin"/> off every solid and
        /// <paramref name="aboveGround"/> over the ground.
        /// </summary>
        public bool Clear(Vector3[] path, float margin, float aboveGround)
        {
            foreach (Vector3 p in path)
                if (p.Y < _height(p.X, p.Z) + aboveGround || Nearest(p) < margin) return false;

            return true;
        }

        /// <summary>How far a point stands off the nearest solid's surface (negative inside one).</summary>
        public float Nearest(Vector3 p)
        {
            float best = float.MaxValue;
            foreach ((Vector3 a, Vector3 b, float radius) in _solids)
            {
                float d = DistanceToSegment(p, a, b) - radius;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// A straight run over the ground from <paramref name="from"/> to <paramref name="to"/>, its height
        /// <paramref name="above0"/> → <paramref name="above1"/> over the ground under each point — a lens
        /// that follows the land's rise and fall at a stated height, as the aurora's run between its trunks
        /// does.
        /// </summary>
        public Vector3[] Hug(Vector2 from, Vector2 to, float above0, float above1, int points)
        {
            var path = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                Vector2 plan = Vector2.Lerp(from, to, t);
                path[i] = new Vector3(plan.X, _height(plan.X, plan.Y) + MathHelper.Lerp(above0, above1, t), plan.Y);
            }
            return path;
        }

        /// <summary>
        /// A straight run at heights <paramref name="above0"/> → <paramref name="above1"/> over the HIGHEST
        /// ground under the whole run — a level, steady lens for a high shot, which a hugging one would make
        /// bob over every rise.
        /// </summary>
        public Vector3[] Level(Vector2 from, Vector2 to, float above0, float above1, int points)
        {
            float top = float.MinValue;
            for (int i = 0; i < points; i++)
            {
                Vector2 plan = Vector2.Lerp(from, to, i / (float)(points - 1));
                top = MathF.Max(top, _height(plan.X, plan.Y));
            }

            var path = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                Vector2 plan = Vector2.Lerp(from, to, t);
                path[i] = new Vector3(plan.X, top + MathHelper.Lerp(above0, above1, t), plan.Y);
            }
            return path;
        }

        /// <summary>
        /// An arc round <paramref name="centre"/> at <paramref name="radius"/>, from bearing <paramref name="a0"/>
        /// to <paramref name="a1"/> (radians), its height <paramref name="above0"/> → <paramref name="above1"/>
        /// over the ground under each point. Evenly spaced, so the clock is a uniform speed.
        /// </summary>
        public Vector3[] Arc(Vector2 centre, float radius, float a0, float a1, float above0, float above1, int points)
        {
            var path = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float a = MathHelper.Lerp(a0, a1, t);
                Vector2 plan = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                path[i] = new Vector3(plan.X, _height(plan.X, plan.Y) + MathHelper.Lerp(above0, above1, t), plan.Y);
            }
            return path;
        }

        /// <summary>
        /// The establishing shot every open scene's prologue opens on: a slow, level dolly in towards the arena
        /// from <paramref name="fromRadius"/> to <paramref name="toRadius"/> on a rolled bearing,
        /// <paramref name="above0"/> → <paramref name="above1"/> over the highest ground under it, the lens on
        /// the island — so the place is seen whole, with the arena standing in it, before any shot goes close.
        /// <para>
        /// The bearing is the best of <paramref name="tries"/> rolls by <paramref name="score"/> (higher is
        /// better; null takes the first clear one), and a roll whose run is not clear by
        /// <paramref name="margin"/> is not a candidate. Where no roll clears, the whole run is lifted and
        /// rolled again — a planting tall enough to stand in every bearing still has a top.
        /// </para>
        /// </summary>
        public IntroShot Establishing(string name, float fromRadius, float toRadius, float above0, float above1,
            float seconds, float fieldOfView, Random random, int tries, float margin, Func<float, float> score = null)
        {
            Vector3 arena = new(0f, ArenaIsland.TOP_Y + 4f, 0f);

            for (int lift = 0; lift < 4; lift++)
            {
                Vector3[] best = null;
                float bestScore = float.MinValue;

                for (int attempt = 0; attempt < tries; attempt++)
                {
                    float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                    Vector2 heading = Heading(bearing);
                    Vector3[] path = Level(heading * fromRadius, heading * toRadius, above0 + lift * 12f, above1 + lift * 12f, PATH_POINTS);
                    if (!Clear(path, margin, 2f)) continue;

                    float value = score?.Invoke(bearing) ?? 0f;
                    if (best != null && value <= bestScore) continue;

                    best = path;
                    bestScore = value;
                    if (score == null) break;
                }

                if (best != null) return new IntroShot(name, best, seconds, fieldOfView, lookAt: arena);
            }

            return null;
        }

        /// <summary>Points per path; every one is held against the ground and every solid.</summary>
        public const int PATH_POINTS = 96;

        /// <summary>Drops the shots that could not be built; null when none could.</summary>
        public static IntroShot[] Cut(params IntroShot[] shots)
        {
            var kept = new List<IntroShot>(shots.Length);
            foreach (IntroShot shot in shots)
                if (shot != null) kept.Add(shot);

            return kept.Count > 0 ? kept.ToArray() : null;
        }

        /// <summary>A point's bearing from the arena's axis, radians.</summary>
        public static float Bearing(Vector2 plan) => MathF.Atan2(plan.Y, plan.X);

        /// <summary>The unit vector at a bearing.</summary>
        public static Vector2 Heading(float bearing) => new(MathF.Cos(bearing), MathF.Sin(bearing));

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float length2 = ab.LengthSquared();
            if (length2 < 1e-6f) return Vector3.Distance(p, a);

            float t = MathHelper.Clamp(Vector3.Dot(p - a, ab) / length2, 0f, 1f);
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
