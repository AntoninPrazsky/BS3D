using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The cavern's hall as geometry, built once on the CPU (#958): the vault and walls, the lake's bed and its shores,
    /// and where every formation, crystal, glowworm and light shaft stands. No device: the backdrop turns its arrays into
    /// buffers (<see cref="CavernMeshes"/>), the Raspberry Pi's path draws the same hall at its own detail, the chapter
    /// intro places its lenses against it, and the tests ask it how far a point stands from rock.
    /// <para>
    /// <b>The shell is one star-shaped surface round <see cref="Centre"/></b>: every direction out of the centre meets
    /// it once, at <see cref="ShellRadius(Vector3)"/>, so it is closed by construction and its mesh is a cube sphere with no pole
    /// and no seam (the shape is a function of the direction alone, and the six faces meet on directions they share).
    /// What it is a function OF is what makes it a cave and not an oval (#778, the owner: "no real cave is oval"): an
    /// elongated plan, lobes and bays a fifth of the radius deep, bulges, lumps, domed pockets in the vault, bedding
    /// ledges on the walls and passages running off at the waterline into the dark. The lake's bed is a height field of
    /// its own (<see cref="FloorHeight"/>): deep in the middle, rising to beaches at some walls and plunging at others,
    /// with rock islands standing out of the water to carry crystals.
    /// </para>
    /// <para>
    /// Everything is a pure function of the config and the seed, so two builds of one seed are the same hall.
    /// </para>
    /// </summary>
    public sealed class CavernChamber
    {
        /// <summary>How far over the lake the shell's centre stands. Every direction from it meets the shell once.</summary>
        public const float CENTRE_ABOVE_WATER = 24f;

        /// <summary>Nothing of the formations comes nearer the arena's axis than this below the vault's crown height
        /// limit (<see cref="ARENA_AIR_TOP"/>): the cluster, the ceiling glass and the gun's orbit live inside it.</summary>
        public const float ARENA_AIR_RADIUS = 52f;

        /// <summary>Over the arena, a hanging formation's tip stays above this height.</summary>
        public const float ARENA_AIR_TOP = 62f;

        private const int WALL_TABLE = 720;

        public CavernSceneConfig Config { get; }
        public float WaterY { get; }
        public Vector3 Centre { get; }

        private readonly float _yawCos, _yawSin, _planA, _planB, _vaultUp, _vaultDown;
        private readonly Vector3 _shift;
        private readonly Vector3[] _tunnelAxis;
        private readonly float[] _tunnelWidth, _tunnelDepth;
        private readonly float[] _wallAtWater = new float[WALL_TABLE];

        public readonly List<CavernOutcrop> Outcrops = new();
        public readonly List<CavernCluster> Clusters = new();
        public readonly List<CavernColumn> Columns = new();
        public readonly List<CavernHanging> Stalactites = new();
        public readonly List<CavernHanging> Stalagmites = new();
        public readonly List<CavernGlowworm> Glowworms = new();
        public readonly List<CavernShaft> Shafts = new();

        public CavernChamber(CavernSceneConfig config, int seedOffset = 0)
        {
            Config = config;
            WaterY = config.Water.LevelY;
            Centre = new Vector3(0f, WaterY + CENTRE_ABOVE_WATER, 0f);

            CavernRockConfig rock = config.Rock;
            var rng = new Random(rock.Seed * 7919 + seedOffset * 104729);

            float yaw = (float)(rng.NextDouble() * MathHelper.Pi);
            _yawCos = MathF.Cos(yaw);
            _yawSin = MathF.Sin(yaw);
            float stretch = MathF.Sqrt(MathF.Max(rock.Elongation, 0.5f));
            _planA = rock.HallRadius * stretch;
            _planB = rock.HallRadius / stretch;
            _vaultUp = MathF.Max(rock.VaultHeight - CENTRE_ABOVE_WATER, 30f);
            _vaultDown = 90f;
            _shift = new Vector3((float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 40f);

            //Passages off the hall: three mouths at the waterline, a third of the way round from each other with a roll, each
            //running back into the dark. A cave whose walls close all round reads as a room; a mouth leading off reads as a cave
            _tunnelAxis = new Vector3[3];
            _tunnelWidth = new float[3];
            _tunnelDepth = new float[3];
            float tunnelStart = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int t = 0; t < 3; t++)
            {
                float bearing = tunnelStart + t * MathHelper.TwoPi / 3f + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                float elevation = -0.04f + 0.10f * (float)rng.NextDouble();
                _tunnelAxis[t] = Vector3.Normalize(new Vector3(MathF.Cos(bearing), elevation, MathF.Sin(bearing)));
                _tunnelWidth[t] = 0.10f + 0.06f * (float)rng.NextDouble();
                _tunnelDepth[t] = 0.9f + 0.6f * (float)rng.NextDouble();
            }

            for (int i = 0; i < WALL_TABLE; i++)
            {
                float bearing = i * MathHelper.TwoPi / WALL_TABLE;
                _wallAtWater[i] = FindWall(new Vector2(MathF.Cos(bearing), MathF.Sin(bearing)), WaterY + 2f);
            }

            PlaceOutcrops(rng);
            PlaceClusters(rng);
            PlaceColumns(rng);
            PlaceStalactites(rng);
            PlaceStalagmites(rng);
            PlaceShafts(rng);
            PlaceGlowworms(rng, config.Air.GlowwormCount);
        }

        #region The shell

        /// <summary>
        /// How far the shell stands from <see cref="Centre"/> along the unit direction <paramref name="u"/>.
        /// <paramref name="medium"/> is the same without the finest relief: how far a vertex stands in a recess, which the
        /// mesh's occlusion reads.
        /// </summary>
        public float ShellRadius(Vector3 u, out float medium)
        {
            float ex = u.X * _yawCos + u.Z * _yawSin;
            float ez = -u.X * _yawSin + u.Z * _yawCos;
            float v = u.Y / (u.Y >= 0f ? _vaultUp : _vaultDown);
            float baseRadius = 1f / MathF.Sqrt(ex * ex / (_planA * _planA) + ez * ez / (_planB * _planB) + v * v);

            Vector3 q = u + _shift;
            //The low frequencies first and strongest (#778's ask): lobes and bays a fifth of the radius and more, bulges on
            //them, then lumps; in the log, so a bay and a bulge scale with the hall rather than adding to it
            float lobes = CavernNoise.Fbm(q * 1.15f, 2);
            float bulges = CavernNoise.Fbm(q * 3.3f + new Vector3(17f, 3f, 9f), 3);
            float lumps = CavernNoise.Fbm(q * 10.5f + new Vector3(31f, 7f, 13f), 2);
            //Crags: creased joints and blocks a couple of units across, the finest relief the mesh carries
            float crags = CavernNoise.Ridged(q * 34f + new Vector3(3f, 71f, 19f), 2);
            float log = 0.62f * lobes + 0.16f * bulges;
            float up = ShaderMath.SmoothStep(0.18f, 0.55f, u.Y);

            //Domed pockets in the vault: round recesses where the cellular field is near a site, deeper towards the crown
            float pocket = 0f;
            if (up > 0f)
            {
                float cell = CavernNoise.Cellular(q * 7f);
                float dome = MathF.Max(0f, 1f - cell / 0.62f);
                pocket = dome * dome * up * 0.13f;
            }

            //The passages: the shell runs back along each mouth's axis, the mouth a soft cone round it
            float tunnel = 0f;
            for (int t = 0; t < _tunnelAxis.Length; t++)
            {
                float c = Vector3.Dot(u, _tunnelAxis[t]);
                if (c < 0.9f) continue;
                float angle = MathF.Acos(MathF.Min(c, 1f));
                float g = MathF.Exp(-(angle * angle) / (_tunnelWidth[t] * _tunnelWidth[t]));
                tunnel += _tunnelDepth[t] * g;
            }

            medium = baseRadius * MathF.Exp(log) * (1f + pocket + tunnel);
            float radius = baseRadius * MathF.Exp(log + 0.045f * lumps - 0.016f * crags) * (1f + pocket + tunnel);

            //Bedding ledges on the walls: limestone lies in beds, and where a harder bed outcrops the wall steps in under a
            //narrow shelf. Each bed rolls its own depth (squared, so most are slight) and only patches of wall carry them,
            //or the walls would be banded like contour lines (the mountain's zebra, docs/scenes/mountains.md)
            float wall = 1f - up;
            if (wall > 0f)
            {
                float y = Centre.Y + radius * u.Y;
                float warp = 11f * CavernNoise.Fbm(q * 2.6f + new Vector3(5f, 41f, 2f), 2);
                float s = (y + warp) / 13f;
                float bed = MathF.Floor(s);
                float f = s - bed;
                float roll = CavernNoise.Hash((int)bed, 958, 11);
                float depth = 1.5f + 6f * roll * roll;
                float band = ShaderMath.SmoothStep(0.50f, 0.68f, f) * (1f - ShaderMath.SmoothStep(0.86f, 1f, f));
                float patch = ShaderMath.SmoothStep(0.08f, 0.32f, CavernNoise.Fbm(q * 2.4f + new Vector3(23f, 5f, 61f), 2));
                float ledge = depth * band * patch * wall;
                radius -= ledge;
                medium -= ledge;
            }

            return radius;
        }

        /// <inheritdoc cref="ShellRadius(Vector3, out float)"/>
        public float ShellRadius(Vector3 u) => ShellRadius(u, out _);

        /// <summary>The shell's point in the unit direction <paramref name="u"/> from the centre.</summary>
        public Vector3 ShellPoint(Vector3 u) => Centre + u * ShellRadius(u);

        /// <summary>Whether <paramref name="p"/> is inside the shell by at least <paramref name="margin"/> (radially).</summary>
        public bool InsideShell(Vector3 p, float margin = 0f)
        {
            Vector3 d = p - Centre;
            float length = d.Length();
            if (length < 1e-3f) return true;
            return length + margin < ShellRadius(d / length);
        }

        /// <summary>The shell's normal at the unit direction <paramref name="u"/>, facing into the hall, by central
        /// differences of the shell over a tangent frame that is a function of the direction alone.</summary>
        public Vector3 ShellNormal(Vector3 u, float epsilon = 0.0025f)
        {
            Vector3 helper = MathF.Abs(u.Y) < 0.9f ? Vector3.Up : Vector3.Right;
            Vector3 t1 = Vector3.Normalize(Vector3.Cross(u, helper));
            Vector3 t2 = Vector3.Cross(u, t1);

            Vector3 a = ShellPoint(Vector3.Normalize(u + t1 * epsilon));
            Vector3 b = ShellPoint(Vector3.Normalize(u - t1 * epsilon));
            Vector3 c = ShellPoint(Vector3.Normalize(u + t2 * epsilon));
            Vector3 d = ShellPoint(Vector3.Normalize(u - t2 * epsilon));
            Vector3 n = Vector3.Cross(a - b, c - d);
            if (Vector3.Dot(n, u) > 0f) n = -n;
            return Vector3.Normalize(n);
        }

        /// <summary>The hall's wall at height <paramref name="y"/> along the horizontal <paramref name="bearing"/> (a unit
        /// vector in XZ): the first distance from the axis where the shell closes. Marched, then bisected.</summary>
        public float FindWall(Vector2 bearing, float y)
        {
            float r = 30f, step = 6f;
            while (r < 900f && InsideShell(new Vector3(bearing.X * r, y, bearing.Y * r))) r += step;
            float lo = r - step, hi = r;
            for (int i = 0; i < 18; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (InsideShell(new Vector3(bearing.X * mid, y, bearing.Y * mid))) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        /// <summary>The vault straight over (<paramref name="x"/>, <paramref name="z"/>): where a vertical line from the lake
        /// first leaves the shell.</summary>
        public float VaultHeight(float x, float z)
        {
            float y = WaterY, step = 4f;
            while (y < WaterY + 400f && InsideShell(new Vector3(x, y, z))) y += step;
            float lo = y - step, hi = y;
            for (int i = 0; i < 16; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (InsideShell(new Vector3(x, mid, z))) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        /// <summary>The wall's distance from the axis at the waterline along the bearing (<paramref name="x"/>, <paramref name="z"/>),
        /// off the table built at construction.</summary>
        public float WallAtWater(float x, float z)
        {
            float bearing = MathF.Atan2(z, x);
            if (bearing < 0f) bearing += MathHelper.TwoPi;
            float f = bearing / MathHelper.TwoPi * WALL_TABLE;
            int i = (int)MathF.Floor(f);
            float t = f - i;
            float a = _wallAtWater[((i % WALL_TABLE) + WALL_TABLE) % WALL_TABLE];
            float b = _wallAtWater[(((i + 1) % WALL_TABLE) + WALL_TABLE) % WALL_TABLE];
            return a + (b - a) * t;
        }

        #endregion

        #region The lake's bed

        /// <summary>
        /// The lake's bed and shores at (<paramref name="x"/>, <paramref name="z"/>): deep in the middle, rising towards the
        /// wall to a beach where the shore is gentle and staying under the water where the wall plunges, rubble along every
        /// waterline, the rock islands standing out of it, and rising on past the wall (hidden behind it) so the bed always
        /// closes the hall from below.
        /// </summary>
        public float FloorHeight(float x, float z)
        {
            float r = MathF.Sqrt(x * x + z * z);
            Vector2 bearing = r > 1e-3f ? new Vector2(x / r, z / r) : Vector2.UnitX;
            float wall = WallAtWater(x, z);
            Vector3 p = new(x, 0f, z);

            float bed = WaterY - 26f - 12f * CavernNoise.Fbm(p * 0.011f + _shift, 2);
            float beach = ShaderMath.SmoothStep(-0.02f, 0.30f, CavernNoise.Fbm(new Vector3(bearing.X, 0f, bearing.Y) * 1.7f + _shift * 0.5f, 2));
            float width = 8f + 44f * beach;
            float t = ShaderMath.SmoothStep(wall - width, wall + 3f, r);
            float shoreTop = WaterY + (-4f + 9f * beach) + 2.5f * CavernNoise.Fbm(p * 0.045f + _shift, 2);
            float h = bed + (shoreTop - bed) * t;
            h += MathF.Max(0f, r - wall) * 0.6f;

            foreach (CavernOutcrop o in Outcrops)
            {
                float d = Vector2.Distance(new Vector2(x, z), o.Centre) / o.Radius;
                if (d >= 1.6f) continue;
                float lump = 1f + 0.35f * CavernNoise.Fbm(p * 0.21f + new Vector3(o.Centre.X, 3f, o.Centre.Y), 2);
                float k = MathF.Max(0f, 1f - d * d / (1.6f * 1.6f));
                float mound = (WaterY - 14f) + (o.Height + 14f) * k * k * lump;
                h = SmoothMax(h, mound, 3f);
            }

            //Rubble: boulders along the waterline, wherever the bed comes near it
            float shore = ShaderMath.SmoothStep(WaterY - 5f, WaterY + 1f, h);
            if (shore > 0f) h += 1.8f * shore * (CavernNoise.Ridged(p * 0.16f + _shift, 2) - 0.25f);
            return h;
        }

        private static float SmoothMax(float a, float b, float k)
        {
            float hh = Math.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f);
            return a + (b - a) * hh + k * hh * (1f - hh);
        }

        /// <summary>The bed's normal, by central differences.</summary>
        public Vector3 FloorNormal(float x, float z, float e = 0.8f)
        {
            float hx = FloorHeight(x + e, z) - FloorHeight(x - e, z);
            float hz = FloorHeight(x, z + e) - FloorHeight(x, z - e);
            return Vector3.Normalize(new Vector3(-hx, 2f * e, -hz));
        }

        #endregion

        #region Placement

        private void PlaceOutcrops(Random rng)
        {
            //Rock islands in the lake, standing between the arena and the walls: what the crystals and stalagmites stand on,
            //and what breaks the water into a lake rather than a floor
            float start = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int i = 0; i < 7; i++)
            {
                float bearing = start + i * 2.39996f + ((float)rng.NextDouble() - 0.5f) * 0.4f;
                Vector2 dir = new(MathF.Cos(bearing), MathF.Sin(bearing));
                float wall = WallAtWater(dir.X, dir.Y);
                float r = MathHelper.Lerp(64f, MathF.Max(80f, wall * 0.7f), (float)rng.NextDouble());
                Outcrops.Add(new CavernOutcrop(dir * r, MathHelper.Lerp(8f, 17f, (float)rng.NextDouble()),
                    MathHelper.Lerp(2f, 8f, (float)rng.NextDouble())));
            }
        }

        private void PlaceClusters(Random rng)
        {
            CavernCrystalConfig crystals = Config.Crystals;
            int index = 0;

            //One on the crown of most outcrops
            foreach (CavernOutcrop o in Outcrops)
            {
                if (rng.NextDouble() < 0.25) continue;
                Vector2 at = o.Centre + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * o.Radius * 0.4f;
                AddCluster(rng, at, index++, MathHelper.Lerp(1.3f, 1.9f, (float)rng.NextDouble()));
            }

            //And along the shore at the wall's foot, on beaches, a few round the hall
            for (int attempt = 0; attempt < 300 && index < 8; attempt++)
            {
                float bearing = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector2 dir = new(MathF.Cos(bearing), MathF.Sin(bearing));
                float wall = WallAtWater(dir.X, dir.Y);
                Vector2 at = dir * (wall - 4f - 10f * (float)rng.NextDouble());
                if (FloorHeight(at.X, at.Y) < WaterY - 0.5f) continue;
                bool crowded = false;
                foreach (CavernCluster c in Clusters)
                    if (Vector2.Distance(new Vector2(c.Base.X, c.Base.Z), at) < 70f) { crowded = true; break; }
                if (crowded) continue;
                AddCluster(rng, at, index++, MathHelper.Lerp(1.4f, 2.2f, (float)rng.NextDouble()));
            }

            PlaceWallClusters(rng, index);
        }

        private void AddCluster(Random rng, Vector2 at, int index, float size)
        {
            float y = FloorHeight(at.X, at.Y);
            Vector3 normal = FloorNormal(at.X, at.Y, 1.5f);
            AddCluster(rng, new Vector3(at.X, y - 0.6f, at.Y), Vector3.Normalize(Vector3.Lerp(Vector3.Up, normal, 0.6f)), index, size);
        }

        //Up on the walls: clusters grown out of ledges between the waterline and the vault, where the play camera, looking
        //across the hall over the island, sees them glow and light the rock round them
        private void PlaceWallClusters(Random rng, int index)
        {
            for (int attempt = 0; attempt < 4000 && Clusters.Count < 12; attempt++)
            {
                Vector3 u = RandomUpper(rng, -0.06f, 0.24f);
                Vector3 p = ShellPoint(u);
                if (p.Y < WaterY + 8f || p.Y > WaterY + 52f) continue;
                Vector3 n = ShellNormal(u, 0.01f);
                if (n.Y < (attempt < 2500 ? 0.22f : 0.0f)) continue;
                bool crowded = false;
                foreach (CavernCluster c in Clusters)
                    if (Vector3.Distance(c.Base, p) < 60f) { crowded = true; break; }
                if (crowded) continue;
                Vector3 up = Vector3.Normalize(n + Vector3.Up * 0.7f);
                AddCluster(rng, p - n * 0.8f, up, index++, MathHelper.Lerp(1.1f, 1.7f, (float)rng.NextDouble()));
            }
        }

        private void AddCluster(Random rng, Vector3 foot, Vector3 up, int index, float size)
        {
            //Alternating cyan and magenta round the hall with a lean either way, so neighbours differ
            float hue = (index % 2 == 0 ? 0.08f : 0.92f) + ((float)rng.NextDouble() - 0.5f) * 0.16f;
            var cluster = new CavernCluster(foot, up, Math.Clamp(hue, 0f, 1f), (float)rng.NextDouble() * MathHelper.TwoPi, size);

            int prisms = 6 + rng.Next(6);
            Vector3 side = Vector3.Normalize(Vector3.Cross(up, MathF.Abs(up.Y) > 0.9f ? Vector3.Right : Vector3.Up));
            Vector3 other = Vector3.Cross(up, side);
            for (int k = 0; k < prisms; k++)
            {
                bool main = k == 0;
                float lean = main ? 0.12f : MathHelper.Lerp(0.25f, 0.75f, (float)rng.NextDouble());
                float around = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector3 axis = Vector3.Normalize(up + (side * MathF.Cos(around) + other * MathF.Sin(around)) * lean);
                float length = size * (main ? MathHelper.Lerp(11f, 15f, (float)rng.NextDouble()) : MathHelper.Lerp(3.5f, 10f, (float)rng.NextDouble()));
                float radius = size * (main ? 2.1f : MathHelper.Lerp(0.7f, 1.6f, (float)rng.NextDouble()));
                float spread = main ? 0f : size * MathHelper.Lerp(1.2f, 3.6f, (float)rng.NextDouble());
                Vector3 baseAt = foot + (side * MathF.Cos(around) + other * MathF.Sin(around)) * spread - axis * radius * 0.6f;
                cluster.Prisms.Add(new CavernPrism(baseAt, axis, radius, length, radius * MathHelper.Lerp(1.3f, 2.2f, (float)rng.NextDouble()),
                    (float)rng.NextDouble() * MathHelper.TwoPi));
            }

            Clusters.Add(cluster);
        }

        private void PlaceColumns(Random rng)
        {
            //Flowstone columns standing in the lake, a stalactite grown down to its stalagmite: the one formation spanning
            //the water to the vault, and what stands the hall up. Clear of the arena's air and of each other, never in front
            //of a crystal cluster's island
            for (int attempt = 0; attempt < 400 && Columns.Count < 5; attempt++)
            {
                float bearing = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector2 dir = new(MathF.Cos(bearing), MathF.Sin(bearing));
                float wall = WallAtWater(dir.X, dir.Y);
                float r = MathHelper.Lerp(95f, wall * 0.82f, (float)rng.NextDouble());
                Vector2 at = dir * r;
                bool clear = true;
                foreach (CavernColumn c in Columns) if (Vector2.Distance(c.Axis, at) < 80f) clear = false;
                foreach (CavernOutcrop o in Outcrops) if (Vector2.Distance(o.Centre, at) < o.Radius + 18f) clear = false;
                if (!clear) continue;
                float bottom = MathF.Min(FloorHeight(at.X, at.Y), WaterY - 4f) - 2f;
                float top = VaultHeight(at.X, at.Y) + 4f;
                if (top - bottom < 40f || top - bottom > 220f) continue;
                Columns.Add(new CavernColumn(at, bottom, top, MathHelper.Lerp(5.5f, 10f, (float)rng.NextDouble()), (float)rng.NextDouble() * 100f));
            }
        }

        private void PlaceStalactites(Random rng)
        {
            //CURTAINS first (#757): rows of stalactites hanging along a line on the vault, the longest in the middle of the
            //row, the shortest at its ends - the fringe every reference hangs where the ceiling bends down into the wall.
            //Then scattered singles over the rest of the vault
            int curtains = 0;
            for (int attempt = 0; attempt < 3000 && curtains < 34; attempt++)
            {
                Vector3 u = RandomUpper(rng, 0.10f, 0.80f);
                Vector3 n = ShellNormal(u);
                if (n.Y > -0.35f) continue;
                Vector3 start = ShellPoint(u);
                Vector3 along = Vector3.Normalize(Vector3.Cross(Vector3.Up, new Vector3(u.X, 0f, u.Z)));
                along = Vector3.Normalize(along + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.8f);
                int count = 10 + rng.Next(18);
                float spacing = MathHelper.Lerp(1.6f, 3.2f, (float)rng.NextDouble());
                float longest = MathHelper.Lerp(10f, 34f, (float)rng.NextDouble());
                bool placed = false;
                for (int k = 0; k < count; k++)
                {
                    float s = (k - count * 0.5f) * spacing;
                    Vector3 guess = start + along * s + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 1.6f;
                    guess = ShellPoint(Vector3.Normalize(guess - Centre));
                    float envelope = 1f - MathF.Pow(MathF.Abs(k - count * 0.5f) / (count * 0.5f + 0.5f), 2f);
                    float length = longest * (0.25f + 0.75f * envelope) * MathHelper.Lerp(0.6f, 1.15f, (float)rng.NextDouble());
                    float girth = MathHelper.Lerp(0.7f, 1.8f, (float)rng.NextDouble()) * (0.6f + 0.4f * length / 30f);
                    if (TryHang(guess, length, girth, rng, out CavernHanging hanging)) { Stalactites.Add(hanging); placed = true; }
                }
                if (placed) curtains++;
            }

            for (int attempt = 0; attempt < 6000 && Stalactites.Count < 760; attempt++)
            {
                Vector3 u = RandomUpper(rng, 0.10f, 0.97f);
                Vector3 p = ShellPoint(u);
                float length = MathHelper.Lerp(4f, 26f, MathF.Pow((float)rng.NextDouble(), 1.6f));
                float girth = MathHelper.Lerp(0.6f, 3.4f, (float)rng.NextDouble()) * (0.5f + 0.5f * length / 26f);
                //Clustered, as drip lines are: a single is kept where a slow field says the rock drips
                float drip = CavernNoise.Fbm(u * 6f + _shift, 2);
                if (drip < -0.05f) continue;
                if (TryHang(p, length, girth, rng, out CavernHanging hanging)) Stalactites.Add(hanging);
            }
        }

        private Vector3 RandomUpper(Random rng, float minY, float maxY)
        {
            float y = MathHelper.Lerp(minY, maxY, (float)rng.NextDouble());
            float bearing = (float)rng.NextDouble() * MathHelper.TwoPi;
            float h = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
            return new Vector3(MathF.Cos(bearing) * h, y, MathF.Sin(bearing) * h);
        }

        //A stalactite hung from the vault at `anchor`, a point ON the shell: its root up in the rock, its tip clear of the
        //arena's air and of the lake, on a downward-facing patch of vault (a cone hung on a wall would stand out of it sideways)
        private bool TryHang(Vector3 anchor, float length, float girth, Random rng, out CavernHanging hanging)
        {
            hanging = default;
            Vector3 near = anchor;
            Vector3 d = near - Centre;
            if (d.LengthSquared() < 1f) return false;
            Vector3 u = Vector3.Normalize(d);
            if (u.Y < 0.06f) return false;
            Vector3 n = ShellNormal(u);
            if (n.Y > -0.35f) return false;
            float top = anchor.Y;
            if (top < WaterY + 30f) return false;
            //The root goes up into the rock by more on a sloping patch, where the vault falls away from one side of the cone
            float embed = 2f + 6f * (1f + n.Y);
            Vector3 lean = Vector3.Normalize(new Vector3(((float)rng.NextDouble() - 0.5f) * 0.08f, -1f, ((float)rng.NextDouble() - 0.5f) * 0.08f));
            Vector3 root = new(near.X, top + embed, near.Z);
            //The tip, lean and all, clear of the arena's air and of the lake
            Vector3 tip = root + lean * (length + embed);
            if (tip.X * tip.X + tip.Z * tip.Z < ARENA_AIR_RADIUS * ARENA_AIR_RADIUS && tip.Y < ARENA_AIR_TOP) return false;
            if (tip.Y < WaterY + 12f) return false;
            hanging = new CavernHanging(root, lean, length + embed, girth, (float)rng.NextDouble() * 100f, rng.Next(3));
            return true;
        }

        private void PlaceStalagmites(Random rng)
        {
            //On the rock islands, round the clusters, and along the beaches - a stalagmite grows where water drips onto
            //rock that stands out of the lake
            foreach (CavernOutcrop o in Outcrops)
            {
                int count = 2 + rng.Next(4);
                for (int k = 0; k < count; k++)
                {
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    Vector2 at = o.Centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * o.Radius * MathHelper.Lerp(0.15f, 0.7f, (float)rng.NextDouble());
                    TryStand(at, MathHelper.Lerp(3f, 13f, (float)rng.NextDouble()), rng);
                }
            }

            for (int attempt = 0; attempt < 2000 && Stalagmites.Count < 90; attempt++)
            {
                float bearing = (float)rng.NextDouble() * MathHelper.TwoPi;
                Vector2 dir = new(MathF.Cos(bearing), MathF.Sin(bearing));
                float wall = WallAtWater(dir.X, dir.Y);
                Vector2 at = dir * (wall - 2f - 40f * (float)rng.NextDouble());
                TryStand(at, MathHelper.Lerp(2.5f, 16f, MathF.Pow((float)rng.NextDouble(), 1.5f)), rng);
            }
        }

        private void TryStand(Vector2 at, float height, Random rng)
        {
            float y = FloorHeight(at.X, at.Y);
            if (y < WaterY + 0.4f) return;
            foreach (CavernCluster c in Clusters)
                if (Vector2.Distance(new Vector2(c.Base.X, c.Base.Z), at) < 5f * c.Size) return;
            float girth = MathHelper.Lerp(0.9f, 2.4f, (float)rng.NextDouble()) * (0.55f + 0.45f * height / 12f);
            Stalagmites.Add(new CavernHanging(new Vector3(at.X, y - 1.5f, at.Y), Vector3.Up, height + 1.5f, girth,
                (float)rng.NextDouble() * 100f, rng.Next(3)));
        }

        private void PlaceShafts(Random rng)
        {
            //Two shafts of pale light through cracks in the vault, slanting down to the lake beside the arena: never through
            //the air over the arena, where they would fall across the cluster
            float start = (float)rng.NextDouble() * MathHelper.TwoPi;
            for (int i = 0; i < 2; i++)
            {
                float bearing = start + i * MathHelper.Pi + ((float)rng.NextDouble() - 0.5f) * 0.8f;
                Vector2 dir = new(MathF.Cos(bearing), MathF.Sin(bearing));
                float r = MathHelper.Lerp(80f, 125f, (float)rng.NextDouble());
                Vector2 landing = dir * r;
                Vector2 tilt = new Vector2(-dir.Y, dir.X) * MathHelper.Lerp(-0.25f, 0.25f, (float)rng.NextDouble()) + dir * 0.18f;
                Vector3 direction = Vector3.Normalize(new Vector3(tilt.X, -1f, tilt.Y));
                //Up from the landing along the beam to the vault
                Vector3 source = new(landing.X, WaterY, landing.Y);
                float step = 2f;
                while (InsideShell(source - direction * step) && source.Y < WaterY + 400f) source -= direction * step;
                float landY = MathF.Max(FloorHeight(landing.X, landing.Y), WaterY);
                Vector3 land = new(landing.X, landY, landing.Y);
                Shafts.Add(new CavernShaft(source, direction, Vector3.Distance(source, land), MathHelper.Lerp(5.5f, 8f, (float)rng.NextDouble()), land));
            }
        }

        private void PlaceGlowworms(Random rng, int count)
        {
            //The constellation (#507): on the vault's downward-facing rock, dense in patches and bare between them, never
            //down the walls or into the passages. A glowworm is a point on the real surface, lifted a little off it
            for (int attempt = 0; attempt < count * 12 && Glowworms.Count < count; attempt++)
            {
                Vector3 u = RandomUpper(rng, 0.08f, 1f);
                float patch = CavernNoise.Fbm(u * 3.4f + _shift * 1.7f, 3);
                float density = ShaderMath.SmoothStep(-0.02f, 0.30f, patch);
                if (rng.NextDouble() > density) continue;
                Vector3 p = ShellPoint(u);
                Vector3 n = ShellNormal(u, 0.004f);
                if (n.Y > -0.15f) continue;
                Glowworms.Add(new CavernGlowworm(p + n * 0.25f, (float)rng.NextDouble() * MathHelper.TwoPi,
                    MathHelper.Lerp(0.7f, 1.35f, (float)rng.NextDouble())));
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// About how far <paramref name="p"/> stands from rock, water or a formation (a lower bound only where the shell's
        /// radial distance is one): what a lens path is checked against. Negative inside something.
        /// </summary>
        public float Clearance(Vector3 p)
        {
            Vector3 d = p - Centre;
            float length = MathF.Max(d.Length(), 1e-3f);
            float clearance = (ShellRadius(d / length) - length) * 0.8f;
            clearance = MathF.Min(clearance, p.Y - MathF.Max(FloorHeight(p.X, p.Z), WaterY));

            foreach (CavernColumn c in Columns)
                clearance = MathF.Min(clearance, Vector2.Distance(new Vector2(p.X, p.Z), c.Axis) - c.Girth * 1.6f);
            foreach (CavernHanging s in Stalactites)
                clearance = MathF.Min(clearance, SegmentDistance(p, s.Root, s.Root + s.Direction * s.Length) - s.Girth);
            foreach (CavernHanging s in Stalagmites)
                clearance = MathF.Min(clearance, SegmentDistance(p, s.Root, s.Root + s.Direction * s.Length) - s.Girth);
            foreach (CavernCluster c in Clusters)
                clearance = MathF.Min(clearance, Vector3.Distance(p, c.Base + c.Up * 6f * c.Size) - 9f * c.Size);
            return clearance;
        }

        private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Math.Clamp(Vector3.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
            return Vector3.Distance(p, a + ab * t);
        }

        #endregion
    }

    /// <summary>A rock island in the lake: its centre in XZ, its radius and how far it stands out of the water.</summary>
    public readonly struct CavernOutcrop
    {
        public readonly Vector2 Centre;
        public readonly float Radius, Height;

        public CavernOutcrop(Vector2 centre, float radius, float height)
        {
            Centre = centre;
            Radius = radius;
            Height = height;
        }
    }

    /// <summary>A crystal cluster: where it grows, which way is up from its rock, its hue on the cyan-magenta axis, the
    /// phase of its breath, its size, and its prisms.</summary>
    public sealed class CavernCluster
    {
        public readonly Vector3 Base, Up;
        public readonly float Hue, Phase, Size;
        public readonly List<CavernPrism> Prisms = new();

        public CavernCluster(Vector3 foot, Vector3 up, float hue, float phase, float size)
        {
            Base = foot;
            Up = up;
            Hue = hue;
            Phase = phase;
            Size = size;
        }

        /// <summary>Where the cluster's light is taken from: up its axis, in the middle of its prisms.</summary>
        public Vector3 LightPosition => Base + Up * (5f * Size);
    }

    /// <summary>One crystal: a six-sided prism from <see cref="Base"/> along <see cref="Axis"/>, <see cref="Radius"/> to a
    /// corner, <see cref="Length"/> to the shoulder, then a pyramid <see cref="Tip"/> long; turned <see cref="Turn"/> round its axis.</summary>
    public readonly struct CavernPrism
    {
        public readonly Vector3 Base, Axis;
        public readonly float Radius, Length, Tip, Turn;

        public CavernPrism(Vector3 baseAt, Vector3 axis, float radius, float length, float tip, float turn)
        {
            Base = baseAt;
            Axis = axis;
            Radius = radius;
            Length = length;
            Tip = tip;
            Turn = turn;
        }
    }

    /// <summary>A flowstone column: its axis in XZ, its foot and head (both buried), its girth and a seed.</summary>
    public readonly struct CavernColumn
    {
        public readonly Vector2 Axis;
        public readonly float Bottom, Top, Girth, Seed;

        public CavernColumn(Vector2 axis, float bottom, float top, float girth, float seed)
        {
            Axis = axis;
            Bottom = bottom;
            Top = top;
            Girth = girth;
            Seed = seed;
        }
    }

    /// <summary>A stalactite or stalagmite: its root (buried in the rock), the direction it grows, its length, its girth at
    /// the root, a seed and which of the shapes it takes.</summary>
    public readonly struct CavernHanging
    {
        public readonly Vector3 Root, Direction;
        public readonly float Length, Girth, Seed;
        public readonly int Shape;

        public CavernHanging(Vector3 root, Vector3 direction, float length, float girth, float seed, int shape)
        {
            Root = root;
            Direction = direction;
            Length = length;
            Girth = girth;
            Seed = seed;
            Shape = shape;
        }
    }

    /// <summary>A glowworm: its point, the phase of its breath and its size against the others.</summary>
    public readonly struct CavernGlowworm
    {
        public readonly Vector3 Position;
        public readonly float Phase, Size;

        public CavernGlowworm(Vector3 position, float phase, float size)
        {
            Position = position;
            Phase = phase;
            Size = size;
        }
    }

    /// <summary>A shaft of light: the crack it enters by, the way it falls, how far, its radius at the crack, and where it lands.</summary>
    public readonly struct CavernShaft
    {
        public readonly Vector3 Source, Direction, Landing;
        public readonly float Length, Radius;

        public CavernShaft(Vector3 source, Vector3 direction, float length, float radius, Vector3 landing)
        {
            Source = source;
            Direction = direction;
            Length = length;
            Radius = radius;
            Landing = landing;
        }
    }
}
