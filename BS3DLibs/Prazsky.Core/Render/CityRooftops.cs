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
    /// <b>The neon city carries more</b>, and not by a second layout: after the day's dressing each roof rolls
    /// again on the same stream, at <see cref="RooftopConfig.NeonExtra"/> minus one times the chances, into the
    /// cells the day's dressing left (#850) — so an extra mast stands only on a roof whose middle cell the day's
    /// left empty. The extras are kept in their own instance lists and drawn only when the scene is the neon
    /// one, where the dishes also wear a ring of the neon's own magenta or cyan round their rims.
    /// </para>
    /// <para>
    /// <b>The beacons flash in clusters, not together</b> (#902: "such synchronisation across a whole city is improbable"):
    /// <see cref="BEACON_CLUSTERS"/> kinds over the one beacon mesh, each with its own renderer, period, phase and shade of
    /// red, and a mast's beacon goes to the cluster its foot's <see cref="BEACON_CELL"/> hashes to
    /// (<see cref="BeaconCluster"/>), so a neighbourhood's towers share a rhythm and the city does not. That costs a draw
    /// per cluster instead of one, and no shader channel: the tint is read at each <c>Draw</c>.
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

        //The beacons are eight kinds over one mesh, one per flashing cluster (#902: BEACON_CLUSTERS counts them off these, and
        //the three BEACON_ tables below need an entry each: the tests ask every cluster). The rings stay last: Draw tells
        //them by `>= Ring0Magenta`.
        private enum Kind
        {
            MastRed, MastWhite, Beacon0, Beacon1, Beacon2, Beacon3, Beacon4, Beacon5, Beacon6, Beacon7,
            Dish0, Dish1, Sector, Hvac, Ring0Magenta, Ring0Cyan, Ring1Magenta, Ring1Cyan, Count
        }

        /// <summary>How many clusters the masts' beacons are split into, each flashing on a clock of its own (#902).</summary>
        internal const int BEACON_CLUSTERS = (int)Kind.Beacon7 - (int)Kind.Beacon0 + 1;

        /// <summary>
        /// The side, in world units, of the square cells a mast's beacon cluster is hashed from (#902): two of the day city's
        /// blocks (2.4 of the neon city's), so the towers of a neighbourhood flash to one rhythm and the next neighbourhood to
        /// another, with about a hundred and fifty cells across a city and a mast or two in each.
        /// </summary>
        internal const float BEACON_CELL = 60f;

        //The share of a beacon's period it is lit for, as it always was: a short flash, then dark
        private const float BEACON_ON = 0.22f;

        //Each cluster's own flash (#902), indexed by cluster; cluster 0 is the one beacon the city had before, to the bit.
        //The period is a share of RooftopConfig.BeaconPeriod, 0.75 to 1.02: ±25 % round the 1.6 s #619 ruled a readable
        //blinker would put some past the 1.67 s the rooftop intro's three flashes in five seconds allow (chapter-intro-
        //prologues.md), so the spread is taken down from it instead (1.2 to 1.63 s). The phase is a share of the
        //cluster's own period, the golden-ratio sequence so that no two clusters start close. The green of the red runs
        //0.03 to 0.16: a deep red to an orange-red, every one over GLARE_THRESHOLD at BeaconBrightness (the test holds
        //the margin). The order is scrambled on purpose: the hash picks a cluster, so the table's neighbours are not
        //the city's.
        private static readonly float[] BEACON_PERIOD_SHARE = { 1.00f, 0.78f, 0.90f, 1.02f, 0.84f, 0.96f, 0.75f, 0.93f };
        private static readonly float[] BEACON_PHASE = { 0.000f, 0.618f, 0.236f, 0.854f, 0.472f, 0.090f, 0.708f, 0.326f };
        private static readonly float[] BEACON_GREEN = { 0.08f, 0.03f, 0.14f, 0.05f, 0.16f, 0.11f, 0.065f, 0.13f };

        //The red channel and the blue of every beacon's red; the green is the cluster's own
        private const float BEACON_RED_R = 1f, BEACON_RED_B = 0.03f;

        /// <summary>One beacon cluster's red at its peak, before <see cref="RooftopConfig.BeaconBrightness"/> (#902).</summary>
        internal static Vector3 BeaconRed(int cluster) => new(BEACON_RED_R, BEACON_GREEN[cluster], BEACON_RED_B);

        /// <summary>The length of one beacon cluster's period in seconds, from the config's <paramref name="period"/> (#902).</summary>
        internal static float BeaconPeriodOf(int cluster, float period) => MathF.Max(0.1f, period) * BEACON_PERIOD_SHARE[cluster];

        /// <summary>
        /// How lit one cluster's beacons are at <paramref name="seconds"/> on the wall clock, 0 (dark) to 1 (the peak of the
        /// flash): a sine over the first <see cref="BEACON_ON"/> of the cluster's own period, shifted by its own phase (#902).
        /// </summary>
        internal static float BeaconFlash(int cluster, float seconds, float period)
        {
            float phase = seconds / BeaconPeriodOf(cluster, period) + BEACON_PHASE[cluster];
            phase -= MathF.Floor(phase);
            return phase < BEACON_ON ? MathF.Sin(phase / BEACON_ON * MathHelper.Pi) : 0f;
        }

        /// <summary>
        /// The cluster a mast's beacon flashes with (#902): a hash of the <see cref="BEACON_CELL"/> its foot stands in and of
        /// the dressing <paramref name="seed"/>, so neighbouring towers share a rhythm. <b>Only the foot's position goes
        /// in, never the roof's random stream</b>: the mast is rolled first on that stream, and a draw from it for the
        /// cluster would shift every later roll and dress the whole city anew (<see cref="Dress"/>). An integer mix, like
        /// <see cref="RoofSeed"/>, the same on every run and machine.
        /// </summary>
        internal static int BeaconCluster(float footX, float footZ, int seed)
        {
            unchecked
            {
                int cellX = (int)MathF.Floor(footX / BEACON_CELL), cellZ = (int)MathF.Floor(footZ / BEACON_CELL);
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)cellX * 0x85EBCA6Bu;
                h = (h << 13) | (h >> 19);
                h ^= (uint)cellZ * 0xC2B2AE35u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (int)(((ulong)h * BEACON_CLUSTERS) >> 32);
            }
        }

        private static bool IsBeacon(int kind) => kind >= (int)Kind.Beacon0 && kind < (int)Kind.Beacon0 + BEACON_CLUSTERS;

        //What a kind is drawn in: painted steel for the equipment, the mast's two colours, the beacon's lamp
        private static readonly Vector3 PAINTED = new(0.60f, 0.61f, 0.60f);
        private static readonly Vector3 HVAC_GREY = new(0.50f, 0.51f, 0.50f);
        private static readonly Vector3 MAST_RED = new(0.62f, 0.10f, 0.07f);
        private static readonly Vector3 MAST_WHITE = new(0.74f, 0.74f, 0.72f);
        private static readonly Vector3 LAMP = new(0.35f, 0.04f, 0.03f);
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

        /// <summary>A mast's beacon is a sphere of this share of its mast's height in radius (0.2–0.35 units).</summary>
        public const float BEACON_SHARE = 0.022f;

        /// <summary>
        /// One tower's roof as a chapter intro's showcase would frame it (#780): the middle of the roof (on the cornice's
        /// slab where it has one), half the roof's larger span, the mast's height (0 for a roof without one), how much
        /// equipment stands on it, a mast counting most, and where the mast's foot stands when it has one: the roof's
        /// middle cell, which on a roof split 2×2 is not the roof's middle (<see cref="Dress"/>). See
        /// <see cref="RoofShowcases"/>.
        /// </summary>
        public readonly record struct RoofShowcase(int Building, Vector3 Centre, float HalfSpan, float MastHeight, float Score,
            Vector3 MastFoot)
        {
            /// <summary>The middle of the beacon on the mast's top (#840: what a shot of the roof must keep in frame).</summary>
            public Vector3 MastTop => MastFoot + Vector3.Up * (MastHeight * RooftopMesh.MAST_TOP);

            /// <summary>The beacon's radius, which is scaled with its mast (<see cref="BEACON_SHARE"/>).</summary>
            public float BeaconRadius => MastHeight * BEACON_SHARE;
        }

        /// <summary>
        /// Every tower's roof as the dressing left it, for a chapter intro's shot round one of them (#780). Plain values
        /// with no device in them, so the intro's tests ask the very layout the draw shows
        /// (<see cref="ArrangeShowcases"/>); a running game reads it off <see cref="Showcases"/>.
        /// </summary>
        public sealed class RoofShowcases
        {
            //Every tower's roof with the day's dressing; what the neon city's extras add to its score and the height of the
            //mast they put up (0 for none, and always 0 where the day's dressing put one: the two share the roof's cells and
            //a mast takes the middle one, #850); and how far each roof stands over the tallest other roof round it
            private readonly RoofShowcase[] _roofs;
            private readonly float[] _neonScore, _neonMast, _standout;

            internal RoofShowcases(RoofShowcase[] roofs, float[] neonScore, float[] neonMast, float[] standout)
            {
                _roofs = roofs;
                _neonScore = neonScore;
                _neonMast = neonMast;
                _standout = standout;
            }

            /// <summary>
            /// The <paramref name="count"/> best-dressed roofs whose middles stand between <paramref name="nearest"/> and
            /// <paramref name="farthest"/> from the arena's axis and over every other roof round them, best first — for a
            /// shot that circles one roof's equipment close and from above (#780: "close shots of the building roofs, of
            /// the antennas, the camera circling round them, looking at them more from above"). With
            /// <paramref name="neon"/> the neon city's extra pieces count too, and a roof whose mast only the extras put
            /// up shows it (a roof carries one mast at most, #850). Allocates its answer: it is asked once when an intro
            /// is built, never per frame.
            /// </summary>
            public RoofShowcase[] Best(bool neon, float nearest, float farthest, int count)
            {
                List<RoofShowcase> found = new();
                for (int b = 0; b < _roofs.Length; b++)
                {
                    RoofShowcase roof = _roofs[b];
                    float distance = new Vector2(roof.Centre.X, roof.Centre.Z).Length();
                    if (distance < nearest || distance > farthest || roof.Score <= 0f || _standout[b] < SHOW_STANDOUT) continue;
                    found.Add(neon
                        ? roof with { Score = roof.Score + _neonScore[b], MastHeight = roof.MastHeight > 0f ? roof.MastHeight : _neonMast[b] }
                        : roof);
                }

                found.Sort((a, b) => b.Score.CompareTo(a.Score));
                if (found.Count > count) found.RemoveRange(count, found.Count - count);
                return found.ToArray();
            }
        }

        /// <summary>The roofs as the last <see cref="Rebuild"/> dressed them, for a chapter intro's showcase (#780).</summary>
        public RoofShowcases Showcases { get; private set; }

        //A showcased roof stands at least this high over every other roof within SHOW_NEIGHBOURHOOD of its middle: the
        //first cut took the best-dressed roof whatever stood round it, and on half the seeds the lens over it looked
        //down a canyon of taller towers at walls, the roof a small dark square at the bottom
        private const float SHOW_STANDOUT = 0f;
        private const float SHOW_NEIGHBOURHOOD = 26f;

        //What a piece is worth to a showcase: the mast and its beacon are the roof's silhouette, a dish its shape
        private const float SHOW_MAST = 4f, SHOW_DISH = 2f, SHOW_SECTOR = 1.5f, SHOW_HVAC = 0.5f;

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
            for (int c = 0; c < BEACON_CLUSTERS; c++) _renderers[(int)Kind.Beacon0 + c] = Painted(_beaconMesh, LAMP);
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
            Showcases = Arrange(city, config, _seed, out List<Matrix>[] worlds, out List<Matrix>[] neon);

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

                IProceduralMesh mesh = IsBeacon(k) ? _beaconMesh : _meshes[k];
                for (int i = 0; i < _worlds[k].Length; i++) _bounds[k][i] = mesh.BoundingSphere.Transform(_worlds[k][i]);
            }
        }

        /// <summary>
        /// The roofs <paramref name="city"/> is dressed with under <paramref name="seed"/>, as <see cref="Showcases"/> would
        /// hold them, without a device: the very layout a <see cref="CityRooftops"/> of that seed draws, for a caller that
        /// has no renderer (the chapter intro's tests, #840). Allocates the whole layout; never per frame.
        /// </summary>
        public static RoofShowcases ArrangeShowcases(City city, CitySceneConfig config, int seed) =>
            Arrange(city, config, seed, out _, out _);

        /// <summary>
        /// One piece of the layout: what it is (a mast with its beacon, a dish with its ring, a 5G pole, an air-conditioning
        /// unit — one piece whatever meshes draw it), where its foot stands (the middle of the cell it took), and whether it
        /// is one of the neon city's extras.
        /// </summary>
        internal readonly record struct RoofPiece(string Kind, Vector3 Foot, bool Neon);

        //The kinds that are pieces of their own: a mast's white half and its beacon, and a dish's ring, ride with another
        private static readonly Kind[] PIECE_KINDS = { Kind.MastRed, Kind.Dish0, Kind.Dish1, Kind.Sector, Kind.Hvac };

        /// <summary>
        /// Every piece <paramref name="city"/> is dressed with under <paramref name="seed"/>, the day's and the neon city's
        /// extras, without a device: the layout a <see cref="CityRooftops"/> of that seed draws, for the tests that hold
        /// what it promises (#850: no two pieces of one roof in one cell). Allocates the whole layout; never per frame.
        /// </summary>
        internal static RoofPiece[] ArrangePieces(City city, CitySceneConfig config, int seed)
        {
            Arrange(city, config, seed, out List<Matrix>[] worlds, out List<Matrix>[] neon);

            var pieces = new List<RoofPiece>();
            foreach (Kind kind in PIECE_KINDS)
            {
                foreach (Matrix world in worlds[(int)kind]) pieces.Add(new RoofPiece(kind.ToString(), world.Translation, false));
                foreach (Matrix world in neon[(int)kind]) pieces.Add(new RoofPiece(kind.ToString(), world.Translation, true));
            }

            return pieces.ToArray();
        }

        /// <summary>
        /// Every mast's beacon <paramref name="city"/> is dressed with under <paramref name="seed"/>, as the cluster list it was
        /// put in, without a device (#902): <see cref="RoofPiece.Kind"/> is <c>Beacon</c> and the cluster's number, and
        /// <see cref="RoofPiece.Foot"/> the beacon's own position, which stands over its mast's foot (same X and Z). For the
        /// tests that hold a mast's cluster is a function of where it stands. Allocates the whole layout; never per frame.
        /// </summary>
        internal static RoofPiece[] ArrangeBeacons(City city, CitySceneConfig config, int seed)
        {
            Arrange(city, config, seed, out List<Matrix>[] worlds, out List<Matrix>[] neon);

            var beacons = new List<RoofPiece>();
            for (int c = 0; c < BEACON_CLUSTERS; c++)
            {
                string name = "Beacon" + c;
                foreach (Matrix world in worlds[(int)Kind.Beacon0 + c]) beacons.Add(new RoofPiece(name, world.Translation, false));
                foreach (Matrix world in neon[(int)Kind.Beacon0 + c]) beacons.Add(new RoofPiece(name, world.Translation, true));
            }

            return beacons.ToArray();
        }

        //The layout: every piece of each kind (the day's dressing in WORLDS, the neon city's extras in NEON), and the roofs
        //for a showcase (#780). Plain arithmetic over the city's boxes; Rebuild turns the lists into the draw's arrays.
        private static RoofShowcases Arrange(City city, CitySceneConfig config, int seed,
            out List<Matrix>[] worlds, out List<Matrix>[] neon)
        {
            worlds = new List<Matrix>[(int)Kind.Count];
            neon = new List<Matrix>[(int)Kind.Count];
            for (int k = 0; k < worlds.Length; k++) { worlds[k] = new List<Matrix>(); neon[k] = new List<Matrix>(); }

            RooftopConfig roof = config.Rooftops;
            var roofs = new RoofShowcase[city.TowerCount];
            var neonScore = new float[city.TowerCount];
            var neonMast = new float[city.TowerCount];
            var standout = new float[city.TowerCount];

            //The towers alone: the cornices after them are slabs round roofs, not roofs (City.TowerCount)
            for (int b = 0; b < city.TowerCount; b++)
            {
                Matrix box = city.Buildings[b].World;
                Vector3 centre = new(box.M41, box.M42, box.M43);
                float halfX = box.M11 * 0.5f, halfZ = box.M33 * 0.5f;
                float roofY = centre.Y + box.M22 * 0.5f + city.RoofRise(b);   //on the cornice's slab where it has one

                //The roofline this tower stands against: the generator's own line at this distance, so a
                //tower counts as tall for standing out of its neighbourhood rather than for being near the middle
                float blocks = MathF.Max(MathF.Abs(centre.X), MathF.Abs(centre.Z)) / config.BlockPitch;
                bool tall = roofY > config.RooflineY - blocks * config.TaperPerBlock;

                Random random = new(RoofSeed(seed, centre.X, centre.Z));

                //⚠ ONE CELL GRID PER ROOF, shared by the day's dressing and the neon extras (#850). On a grid each, the
                //extras did not know which cells the day's pieces had taken, and stood a second, shorter mast inside the
                //day's (both take the middle cell), a dish or an air-conditioning unit inside another, on about one
                //neon-city piece in seven. Shared, the extras fill only the cells the day's dressing left.
                bool[,] taken = new bool[CellsAcross(halfX), CellsAcross(halfZ)];

                int masts = worlds[(int)Kind.MastRed].Count, dishes = worlds[(int)Kind.Dish0].Count + worlds[(int)Kind.Dish1].Count;
                int sectors = worlds[(int)Kind.Sector].Count, hvac = worlds[(int)Kind.Hvac].Count;

                Dress(worlds, random, taken, centre, halfX, halfZ, roofY, tall, roof, 1f, seed);

                //What this roof was given, for the showcase (#780)
                bool mast = worlds[(int)Kind.MastRed].Count > masts;
                float score = SHOW_MAST * (worlds[(int)Kind.MastRed].Count - masts)
                    + SHOW_DISH * (worlds[(int)Kind.Dish0].Count + worlds[(int)Kind.Dish1].Count - dishes)
                    + SHOW_SECTOR * (worlds[(int)Kind.Sector].Count - sectors) + SHOW_HVAC * (worlds[(int)Kind.Hvac].Count - hvac);

                //The neon extras, rolled on the same roof with what is left of the chance, in the cells the day's left
                int neonMasts = neon[(int)Kind.MastRed].Count;
                int neonBefore = 0;
                for (int k = 0; k < neon.Length; k++) neonBefore += neon[k].Count;
                Dress(neon, random, taken, centre, halfX, halfZ, roofY, tall, roof, MathF.Max(0f, roof.NeonExtra - 1f), seed);
                int neonAfter = 0;
                for (int k = 0; k < neon.Length; k++) neonAfter += neon[k].Count;
                neonScore[b] = SHOW_DISH * 0.5f * (neonAfter - neonBefore);

                //A mast's matrix is a yaw, its height and its foot (Dress), so M22 is the height and the translation the
                //foot. A roof carries one mast at most, since the mast always takes the middle cell and the grid is shared:
                //the day's, or where the day's dressing put none, the neon extras'
                Matrix? standing = mast ? worlds[(int)Kind.MastRed][^1] : null;
                if (neon[(int)Kind.MastRed].Count > neonMasts)
                {
                    neonMast[b] = neon[(int)Kind.MastRed][^1].M22;
                    standing ??= neon[(int)Kind.MastRed][^1];
                }

                roofs[b] = new RoofShowcase(b, new Vector3(centre.X, roofY, centre.Z), MathF.Max(halfX, halfZ),
                    mast ? standing.Value.M22 : 0f, score, standing?.Translation ?? new Vector3(centre.X, roofY, centre.Z));
            }

            //How far each roof stands over the tallest other roof round it, for the showcase (#780)
            for (int b = 0; b < roofs.Length; b++)
            {
                float highest = float.MinValue;
                for (int o = 0; o < roofs.Length; o++)
                {
                    if (o == b) continue;
                    Vector3 other = roofs[o].Centre;
                    float dx = other.X - roofs[b].Centre.X, dz = other.Z - roofs[b].Centre.Z;
                    if (dx * dx + dz * dz < SHOW_NEIGHBOURHOOD * SHOW_NEIGHBOURHOOD && other.Y > highest) highest = other.Y;
                }
                standout[b] = highest == float.MinValue ? float.MaxValue : roofs[b].Centre.Y - highest;
            }

            return new RoofShowcases(roofs, neonScore, neonMast, standout);
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

        //A roof's cells along one side, from half its span: a 3×3 grid on a big roof, 2×2 on a small one
        private static int CellsAcross(float half) => half > 5f ? 3 : 2;

        /// <summary>
        /// One roof's equipment, at <paramref name="chanceScale"/> times the configured chances. The roof is
        /// split into cells (<see cref="CellsAcross"/>) and each piece takes a cell, so two pieces never stand
        /// in each other: the mast the middle, the dishes an edge, the 5G poles a corner, the air conditioning
        /// whatever is left. <paramref name="taken"/> is the roof's grid and the caller's, so a second call on
        /// the same roof (the neon extras) fills only the cells the first left (#850).
        /// </summary>
        private static void Dress(List<Matrix>[] lists, Random random, bool[,] taken, Vector3 centre, float halfX, float halfZ,
            float roofY, bool tall, RooftopConfig roof, float chanceScale, int seed)
        {
            if (chanceScale <= 0f) return;

            int cellsX = taken.GetLength(0), cellsZ = taken.GetLength(1);
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

                    //⚠ The beacon's cluster is a hash of where the foot stands (BeaconCluster, #902) and takes NO roll from
                    //the roof's stream: a Range or a Next here would shift every roll after the mast, on every roof, and
                    //dress the whole city anew
                    lists[(int)Kind.Beacon0 + BeaconCluster(foot.X, foot.Z, seed)].Add(Matrix.CreateScale(height * BEACON_SHARE)
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

            //The beacons flash cluster by cluster (#902): each cluster is its own renderer and so its own tint, read at its
            //Draw, with a period, a phase and a shade of red of its own, so no city's worth of them beats in unison. Each is
            //still a short bright flash, then dark
            for (int c = 0; c < BEACON_CLUSTERS; c++)
                _renderers[(int)Kind.Beacon0 + c].EmissiveTint = BeaconRed(c) * (roof.BeaconBrightness * BeaconFlash(c, seconds, roof.BeaconPeriod));

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
                    : (k == (int)Kind.MastRed || k == (int)Kind.MastWhite || IsBeacon(k)) ? float.MaxValue
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
                    _renderers[k].Draw(camera, visible, count, IsBeacon(k) ? _lamp : _paint);
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
