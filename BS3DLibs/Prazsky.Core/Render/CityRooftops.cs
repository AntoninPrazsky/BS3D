using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The equipment on the city's roofs (#436): lattice masts with aircraft-warning beacons, satellite dishes,
    /// 5G sector poles and air-conditioning units, placed on every building <see cref="City"/> made and drawn
    /// as one instanced draw per mesh. The owner's report was that the towers read as bare-topped; the
    /// references #441 rendered for it (a daytime roofscape, the neon city at night and a sheet of the
    /// separate pieces) are what the kinds and their proportions were drawn from.
    /// <para>
    /// <b>Where it pays is the skyline.</b> The play camera stands low among the towers and looks up past
    /// them at the cluster, so a roof is seen from below: its equipment is a silhouette against the sky. That
    /// is why the tall kinds favour the towers standing above the roofline around them, why the masts and
    /// their beacons are drawn at any distance, and why the air-conditioning units — which only a camera
    /// looking down on a roof sees at all — stop at <see cref="RooftopConfig.ClutterDistance"/>.
    /// </para>
    /// <para>
    /// <b>The placement is a function of the building, not of the order the generator made it in.</b> Each
    /// roof's random stream is seeded from its own centre, so a city rebuilt at another radius (a quality
    /// step) re-dresses the towers it shares with the old one exactly as they were.
    /// </para>
    /// <para>
    /// <b>The neon city carries more</b>, and not by a second layout: the extra pieces are rolled with the
    /// same stream at <see cref="RooftopConfig.NeonExtra"/> times the chances, kept in their own instance
    /// lists, and drawn only when the scene is the neon one — where the dishes also wear a ring of the neon's
    /// own magenta or cyan round their rims.
    /// </para>
    /// <para>
    /// <b>Sky-lit enrolment and the frame sequence stay the caller's</b>, as they do for
    /// <see cref="ForestScatterRenderer"/>: <see cref="Renderers"/> is the one array the enrolment and the
    /// disposal walk, and <see cref="Draw"/> touches no GPU state and gates on no scene — the caller draws it
    /// after the city, in the opaque scene state the city was drawn in, and only in the two city scenes.
    /// </para>
    /// </summary>
    public sealed class CityRooftops : IDisposable
    {
        /// <summary>The dressing seed; the same seed and the same city always dress the same roofs.</summary>
        public const int DEFAULT_SEED = 43621;

        /// <summary>
        /// The compass direction every dish faces, as a yaw about +Y. Dishes over a real city all point at the
        /// same stretch of sky — the geostationary arc — and a roofscape of dishes facing every which way reads
        /// as scattered props rather than as equipment.
        /// </summary>
        private const float DISH_YAW = 2.4f;

        //How far a dish's yaw strays from DISH_YAW, radians either way: different satellites on the same arc
        private const float DISH_YAW_SPREAD = 0.35f;

        //The two dish elevations built, radians: low and high on the arc
        private static readonly float[] DISH_ELEVATIONS = { 0.52f, 0.84f };

        private enum Kind { MastRed, MastWhite, Beacon, Dish0, Dish1, Sector, Hvac, Ring0Magenta, Ring0Cyan, Ring1Magenta, Ring1Cyan, Count }

        //What a kind is drawn in: painted steel for the equipment, the mast's two colours, the beacon's lamp
        private static readonly Vector3 PAINTED = new(0.60f, 0.61f, 0.60f);
        private static readonly Vector3 HVAC_GREY = new(0.50f, 0.51f, 0.50f);
        private static readonly Vector3 MAST_RED = new(0.62f, 0.10f, 0.07f);
        private static readonly Vector3 MAST_WHITE = new(0.74f, 0.74f, 0.72f);
        private static readonly Vector3 LAMP = new(0.35f, 0.04f, 0.03f);
        private static readonly Vector3 BEACON_RED = new(1f, 0.08f, 0.03f);
        private static readonly Vector3 RING_BODY = new(0.05f, 0.05f, 0.05f);

        private readonly GraphicsDevice _device;
        private readonly int _seed;
        private readonly RooftopMesh[] _meshes = new RooftopMesh[(int)Kind.Count];
        private readonly SphereMesh _beaconMesh;
        private readonly InstancedModelRenderer[] _renderers = new InstancedModelRenderer[(int)Kind.Count];
        private readonly BasicEffectParams _paint, _lamp;

        private CitySceneConfig _config;

        //Every piece of each kind: where it is and how big a sphere holds it, with the neon-only pieces at the
        //end of each list from _neonStart[kind] on. Rebuilt with the layout; the per-frame pass reads them only.
        private Matrix[][] _worlds;
        private BoundingSphere[][] _bounds;
        private int[] _neonStart;

        //This frame's survivors per kind, and their count. Sized with the layout, never reallocated per frame.
        private ModelInstance[][] _visible;
        private readonly int[] _visibleCount = new int[(int)Kind.Count];

        //BoundingFrustum is a CLASS: held and re-pointed rather than constructed per frame (see City).
        private readonly BoundingFrustum _frustum = new(Matrix.Identity);

        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        /// <summary>The renderers, one per kind, for the caller's sky-lit enrolment. Stable across <see cref="Rebuild"/>.</summary>
        public InstancedModelRenderer[] Renderers => _renderers;

        /// <summary>How many pieces the last <see cref="Draw"/> sent to the GPU, over every kind.</summary>
        public int LastDrawn { get; private set; }

        /// <summary>How many pieces the layout holds, neon extras included.</summary>
        public int Total { get; private set; }

        public CityRooftops(GraphicsDevice device, Effect instancingEffect, City city, CitySceneConfig config,
            float ambientIntensity, int seed = DEFAULT_SEED)
        {
            _device = device;
            _seed = seed;

            _meshes[(int)Kind.MastRed] = RooftopMesh.CreateMast(device, red: true);
            _meshes[(int)Kind.MastWhite] = RooftopMesh.CreateMast(device, red: false);
            _meshes[(int)Kind.Dish0] = RooftopMesh.CreateDish(device, DISH_ELEVATIONS[0]);
            _meshes[(int)Kind.Dish1] = RooftopMesh.CreateDish(device, DISH_ELEVATIONS[1]);
            _meshes[(int)Kind.Sector] = RooftopMesh.CreateSectorPole(device);
            _meshes[(int)Kind.Hvac] = RooftopMesh.CreateHvac(device);
            _meshes[(int)Kind.Ring0Magenta] = RooftopMesh.CreateDishRing(device, DISH_ELEVATIONS[0]);
            _meshes[(int)Kind.Ring0Cyan] = _meshes[(int)Kind.Ring0Magenta];
            _meshes[(int)Kind.Ring1Magenta] = RooftopMesh.CreateDishRing(device, DISH_ELEVATIONS[1]);
            _meshes[(int)Kind.Ring1Cyan] = _meshes[(int)Kind.Ring1Magenta];
            _beaconMesh = new SphereMesh(device, 1f, 10, 6);

            Vector3 ambient = Vector3.One * ambientIntensity;

            //Painted steel: a broad, dim highlight and very little sky in it — equipment on a roof is weathered
            //paint, not a mirror, and at a grazing angle a shiny one would bleach to a white stick against the
            //sky exactly as the facades did before their own specular ambient came down (0.07, see City)
            _paint = new BasicEffectParams(ambient, new Vector3(0.22f), 22f, Vector3.Zero);
            _lamp = new BasicEffectParams(ambient, new Vector3(0.6f), 60f, Vector3.Zero);

            InstancedModelRenderer Painted(IProceduralMesh mesh, Vector3 colour) =>
                new(device, mesh, colour, instancingEffect) { SpecularAmbientStrength = 0.08f };

            _renderers[(int)Kind.MastRed] = Painted(_meshes[(int)Kind.MastRed], MAST_RED);
            _renderers[(int)Kind.MastWhite] = Painted(_meshes[(int)Kind.MastWhite], MAST_WHITE);
            _renderers[(int)Kind.Dish0] = Painted(_meshes[(int)Kind.Dish0], PAINTED);
            _renderers[(int)Kind.Dish1] = Painted(_meshes[(int)Kind.Dish1], PAINTED);
            _renderers[(int)Kind.Sector] = Painted(_meshes[(int)Kind.Sector], PAINTED);
            _renderers[(int)Kind.Hvac] = Painted(_meshes[(int)Kind.Hvac], HVAC_GREY);
            _renderers[(int)Kind.Beacon] = Painted(_beaconMesh, LAMP);
            _renderers[(int)Kind.Ring0Magenta] = Painted(_meshes[(int)Kind.Ring0Magenta], RING_BODY);
            _renderers[(int)Kind.Ring0Cyan] = Painted(_meshes[(int)Kind.Ring0Cyan], RING_BODY);
            _renderers[(int)Kind.Ring1Magenta] = Painted(_meshes[(int)Kind.Ring1Magenta], RING_BODY);
            _renderers[(int)Kind.Ring1Cyan] = Painted(_meshes[(int)Kind.Ring1Cyan], RING_BODY);

            Rebuild(city, config);
        }

        /// <summary>
        /// Dresses the roofs of <paramref name="city"/> anew — for a city the caller has rebuilt (a quality step,
        /// an editor edit) or a config whose chances changed. The renderers survive it, so nothing has to be
        /// re-lit; only the placements and the instance arrays are new. A load-time cost, not a per-frame one.
        /// </summary>
        public void Rebuild(City city, CitySceneConfig config)
        {
            _config = config;

            List<Matrix>[] worlds = new List<Matrix>[(int)Kind.Count];
            List<Matrix>[] neon = new List<Matrix>[(int)Kind.Count];
            for (int k = 0; k < worlds.Length; k++) { worlds[k] = new List<Matrix>(); neon[k] = new List<Matrix>(); }

            RooftopConfig roof = config.Rooftops;

            foreach (ModelInstance building in city.Buildings)
            {
                Matrix box = building.World;
                Vector3 centre = new(box.M41, box.M42, box.M43);
                float halfX = box.M11 * 0.5f, halfZ = box.M33 * 0.5f;
                float roofY = centre.Y + box.M22 * 0.5f;

                //The roofline this tower stands against: the generator's own line at this distance, so a
                //tower counts as tall for standing out of its neighbourhood rather than for being near the middle
                float blocks = MathF.Max(MathF.Abs(centre.X), MathF.Abs(centre.Z)) / config.BlockPitch;
                bool tall = roofY > config.RooflineY - blocks * config.TaperPerBlock;

                Random random = new(RoofSeed(_seed, centre.X, centre.Z));

                Dress(worlds, random, centre, halfX, halfZ, roofY, tall, roof, 1f);

                //The neon extras, rolled on the same roof with what is left of the chance
                Dress(neon, random, centre, halfX, halfZ, roofY, tall, roof, MathF.Max(0f, roof.NeonExtra - 1f));
            }

            _worlds = new Matrix[(int)Kind.Count][];
            _bounds = new BoundingSphere[(int)Kind.Count][];
            _neonStart = new int[(int)Kind.Count];
            _visible = new ModelInstance[(int)Kind.Count][];
            Total = 0;

            for (int k = 0; k < (int)Kind.Count; k++)
            {
                _neonStart[k] = worlds[k].Count;
                worlds[k].AddRange(neon[k]);

                _worlds[k] = worlds[k].ToArray();
                _bounds[k] = new BoundingSphere[_worlds[k].Length];
                _visible[k] = new ModelInstance[Math.Max(1, _worlds[k].Length)];
                Total += _worlds[k].Length;

                IProceduralMesh mesh = k == (int)Kind.Beacon ? _beaconMesh : _meshes[k];
                for (int i = 0; i < _worlds[k].Length; i++) _bounds[k][i] = mesh.BoundingSphere.Transform(_worlds[k][i]);
            }
        }

        /// <summary>
        /// A roof's own random seed, from the dressing seed and the tower's centre. <b>Not
        /// <see cref="HashCode.Combine{T1,T2,T3}"/></b>, which is what it was for a first run and which .NET
        /// seeds afresh in every process: two launches of the same build dressed 7 880 and 7 800 pieces. An
        /// integer mix of the rounded centre is the same on every run and every machine.
        /// </summary>
        private static int RoofSeed(int seed, float x, float z)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)(int)MathF.Round(x * 4f) * 0x85EBCA6Bu;
                h = (h << 13) | (h >> 19);
                h ^= (uint)(int)MathF.Round(z * 4f) * 0xC2B2AE35u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// One roof's equipment, at <paramref name="chanceScale"/> times the configured chances. The roof is
        /// split into cells — a 3×3 grid on a big roof, 2×2 on a small one — and each piece takes a cell, so
        /// two pieces never stand in each other: the mast the middle, the dishes an edge, the 5G poles a
        /// corner, the air conditioning whatever is left.
        /// </summary>
        private static void Dress(List<Matrix>[] lists, Random random, Vector3 centre, float halfX, float halfZ,
            float roofY, bool tall, RooftopConfig roof, float chanceScale)
        {
            if (chanceScale <= 0f) return;

            int cellsX = halfX > 5f ? 3 : 2, cellsZ = halfZ > 5f ? 3 : 2;
            bool[,] taken = new bool[cellsX, cellsZ];
            float margin = 0.9f;
            float cellX = (2f * (halfX - margin)) / cellsX, cellZ = (2f * (halfZ - margin)) / cellsZ;
            float cellSize = MathF.Min(cellX, cellZ);

            Vector3 CellCentre(int cx, int cz) => new(
                centre.X - halfX + margin + (cx + 0.5f) * cellX, roofY,
                centre.Z - halfZ + margin + (cz + 0.5f) * cellZ);

            bool Chance(float p) => random.NextDouble() < p * chanceScale;
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

            bool TryTake(int cx, int cz)
            {
                if (cx < 0 || cz < 0 || cx >= cellsX || cz >= cellsZ || taken[cx, cz]) return false;
                taken[cx, cz] = true;
                return true;
            }

            //THE MAST: the middle of the roof, and tall enough to stand clear of the tower's own silhouette.
            //A beacon on its top.
            if (Chance(tall ? roof.TallMastChance : roof.MastChance) && cellSize > 1.2f)
            {
                int cx = cellsX / 2, cz = cellsZ / 2;
                if (TryTake(cx, cz))
                {
                    float height = Range(9f, 16f);
                    Vector3 foot = CellCentre(cx, cz);
                    Matrix mast = Matrix.CreateRotationY(Range(0f, MathHelper.TwoPi)) * Matrix.CreateScale(height)
                        * Matrix.CreateTranslation(foot);

                    lists[(int)Kind.MastRed].Add(mast);
                    lists[(int)Kind.MastWhite].Add(mast);
                    lists[(int)Kind.Beacon].Add(Matrix.CreateScale(height * 0.022f)
                        * Matrix.CreateTranslation(foot + Vector3.Up * (height * RooftopMesh.MAST_TOP)));
                }
            }

            //THE DISHES: an edge cell each, all facing the same stretch of sky
            int dishes = Chance(roof.DishChance) ? (Chance(roof.DishChance * 0.5f) ? 2 : 1) : 0;
            for (int d = 0; d < dishes; d++)
            {
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    bool alongX = random.Next(2) == 0;
                    int cx = alongX ? random.Next(cellsX) : (random.Next(2) == 0 ? 0 : cellsX - 1);
                    int cz = alongX ? (random.Next(2) == 0 ? 0 : cellsZ - 1) : random.Next(cellsZ);
                    if (!TryTake(cx, cz)) continue;

                    int variant = random.Next(2);
                    float size = MathF.Min(Range(1.8f, 3.2f), cellSize * 0.9f);
                    Matrix dish = Matrix.CreateRotationY(DISH_YAW + Range(-DISH_YAW_SPREAD, DISH_YAW_SPREAD))
                        * Matrix.CreateScale(size) * Matrix.CreateTranslation(CellCentre(cx, cz));

                    lists[variant == 0 ? (int)Kind.Dish0 : (int)Kind.Dish1].Add(dish);

                    //The neon ring rides with every dish; it is only drawn in the neon city, in one of the two
                    //neon colours
                    bool magenta = random.Next(2) == 0;
                    lists[variant == 0
                        ? (magenta ? (int)Kind.Ring0Magenta : (int)Kind.Ring0Cyan)
                        : (magenta ? (int)Kind.Ring1Magenta : (int)Kind.Ring1Cyan)].Add(dish);
                    break;
                }
            }

            //THE 5G POLES: a corner each
            int poles = Chance(roof.SectorPoleChance) ? (Chance(roof.SectorPoleChance * 0.5f) ? 2 : 1) : 0;
            for (int p = 0; p < poles; p++)
            {
                int cx = random.Next(2) == 0 ? 0 : cellsX - 1, cz = random.Next(2) == 0 ? 0 : cellsZ - 1;
                if (!TryTake(cx, cz)) continue;

                float height = Range(4.2f, 6.2f);
                lists[(int)Kind.Sector].Add(Matrix.CreateRotationY(Range(0f, MathHelper.TwoPi))
                    * Matrix.CreateScale(height) * Matrix.CreateTranslation(CellCentre(cx, cz)));
            }

            //THE AIR CONDITIONING: whatever cells are left, squared to the building
            if (Chance(roof.HvacChance))
            {
                int units = 1 + random.Next(3);
                for (int u = 0; u < units; u++)
                {
                    int cx = random.Next(cellsX), cz = random.Next(cellsZ);
                    if (!TryTake(cx, cz)) continue;

                    float size = MathF.Min(Range(1.0f, 1.6f), cellSize * 0.5f);
                    lists[(int)Kind.Hvac].Add(Matrix.CreateRotationY(random.Next(2) * MathHelper.PiOver2)
                        * Matrix.CreateScale(size) * Matrix.CreateTranslation(CellCentre(cx, cz)));
                }
            }
        }

        /// <summary>
        /// Culls every piece to the frustum and to its kind's distance, then draws each kind that has anything
        /// left in one instanced call. <paramref name="neon"/> adds the neon city's extra pieces and the dishes'
        /// neon rings; <paramref name="seconds"/> is the clock the beacons flash on — the wall clock, like the
        /// windows', because a city's lights do not stop when the game is paused.
        /// <para>
        /// Touches no GPU state and allocates nothing: plain indexed loops over arrays built with the layout,
        /// and the frustum is re-pointed rather than constructed.
        /// </para>
        /// </summary>
        public void Draw(ICamera camera, bool neon, float seconds)
        {
            _frustum.Matrix = camera.View * camera.Projection;
            Vector3 eye = camera.Position;
            RooftopConfig roof = _config.Rooftops;

            float clutter2 = roof.ClutterDistance * roof.ClutterDistance;
            float equipment2 = roof.EquipmentDistance * roof.EquipmentDistance;

            //The beacons flash together, as a city's warning lights are set to: a short bright flash, then dark
            float phase = seconds / MathF.Max(0.1f, roof.BeaconPeriod);
            phase -= MathF.Floor(phase);
            float flash = phase < 0.22f ? MathF.Sin(phase / 0.22f * MathHelper.Pi) : 0f;
            _renderers[(int)Kind.Beacon].EmissiveTint = BEACON_RED * (roof.BeaconBrightness * flash);

            Vector3 magenta = _config.NeonLook.Magenta.ToVector3() * roof.NeonRingBrightness;
            Vector3 cyan = _config.NeonLook.Cyan.ToVector3() * roof.NeonRingBrightness;
            _renderers[(int)Kind.Ring0Magenta].EmissiveTint = magenta;
            _renderers[(int)Kind.Ring1Magenta].EmissiveTint = magenta;
            _renderers[(int)Kind.Ring0Cyan].EmissiveTint = cyan;
            _renderers[(int)Kind.Ring1Cyan].EmissiveTint = cyan;

            int drawn = 0;

            for (int k = 0; k < (int)Kind.Count; k++)
            {
                bool ring = k >= (int)Kind.Ring0Magenta;
                if (ring && !neon) continue;

                float limit2 = k == (int)Kind.Hvac ? clutter2
                    : (k == (int)Kind.MastRed || k == (int)Kind.MastWhite || k == (int)Kind.Beacon) ? float.MaxValue
                    : equipment2;

                Matrix[] worlds = _worlds[k];
                BoundingSphere[] bounds = _bounds[k];
                ModelInstance[] visible = _visible[k];
                int end = neon ? worlds.Length : _neonStart[k];
                int count = 0;

                for (int i = 0; i < end; i++)
                {
                    if (Vector3.DistanceSquared(bounds[i].Center, eye) > limit2) continue;
                    if (_frustum.Contains(bounds[i]) == ContainmentType.Disjoint) continue;

                    visible[count++] = new ModelInstance(worlds[i], NO_OCCLUSION);
                }

                _visibleCount[k] = count;
                drawn += count;

                if (count > 0)
                    _renderers[k].Draw(camera, visible, count, k == (int)Kind.Beacon ? _lamp : _paint);
            }

            LastDrawn = drawn;
        }

        public void Dispose()
        {
            foreach (InstancedModelRenderer renderer in _renderers) renderer?.Dispose();

            //The two ring meshes are shared by their magenta and cyan kinds, so each is disposed once
            _meshes[(int)Kind.MastRed]?.Dispose();
            _meshes[(int)Kind.MastWhite]?.Dispose();
            _meshes[(int)Kind.Dish0]?.Dispose();
            _meshes[(int)Kind.Dish1]?.Dispose();
            _meshes[(int)Kind.Sector]?.Dispose();
            _meshes[(int)Kind.Hvac]?.Dispose();
            _meshes[(int)Kind.Ring0Magenta]?.Dispose();
            _meshes[(int)Kind.Ring1Magenta]?.Dispose();
            _beaconMesh?.Dispose();
        }
    }
}
