using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What one pass of <see cref="ForestScatterRenderer"/> draws (#1099, #1120), device-free so a test reaches the whole rule:
    /// the focus the near crowns stand round, moved by the pass's lens (<see cref="ForestScatterRenderer.NextFocus"/>), then of
    /// every kind the tier draws the instances whose bounds meet the pass's frustum (<see cref="ForestScatterRenderer.Meets"/>),
    /// the spruces among them split by the crown they wear (<see cref="ForestScatterRenderer.WearsNearCrown"/>). The camera's
    /// pass and the sun's both go through <see cref="Pass"/>, which moves the focus before it splits, so a tree casts the crown
    /// it is drawn in, on the frame of a cut too.
    /// <para>
    /// Allocation-free: the arrays a pass fills are made here once, each as long as that variant's largest planting (its
    /// <c>Fine</c> one, which starts with the one every tier draws), and a pass fills them and draws them before the next one
    /// fills them again (each draw uploads what it is handed there and then).
    /// </para>
    /// </summary>
    internal sealed class ForestPasses
    {
        private readonly ForestScatter _scatter;

        //Each variant's whole tree at scale 1 (trunk and every crown it can wear), the sphere a pass culls an instance by
        private readonly BoundingSphere[] _coniferBounds, _broadleafBounds, _rockBounds, _stumpBounds, _snagBounds, _logBounds;

        //The frustum a pass culls by: the camera's, or the sun's map's box. One instance, its matrix set per pass
        private readonly BoundingFrustum _frustum = new(Matrix.Identity);

        //The focus the near crowns stand round besides the arena (#1120), and the lens of the last pass, which tells a cut
        private Vector3 _focus, _lastLens;
        private bool _focused;

        /// <summary>This pass's spruces seen (their trunks and stems, and on Low and Medium their lathe crowns).</summary>
        public readonly Culled Conifers;

        /// <summary>On High and Ultra, the spruces seen split by the crown they wear: the near crown, and the rest.</summary>
        public readonly Culled NearConifers, FarConifers;

        /// <summary>Every other kind seen.</summary>
        public readonly Culled Broadleaves, Rocks, Stumps, Snags, Logs;

        public ForestPasses(ForestScatter scatter, BoundingSphere[] coniferBounds, BoundingSphere[] broadleafBounds,
            BoundingSphere[] rockBounds, BoundingSphere[] stumpBounds, BoundingSphere[] snagBounds, BoundingSphere[] logBounds)
        {
            _scatter = scatter;
            _coniferBounds = coniferBounds;
            _broadleafBounds = broadleafBounds;
            _rockBounds = rockBounds;
            _stumpBounds = stumpBounds;
            _snagBounds = snagBounds;
            _logBounds = logBounds;
            Conifers = new Culled(scatter.FineConifers);
            NearConifers = new Culled(scatter.FineConifers);
            FarConifers = new Culled(scatter.FineConifers);
            Broadleaves = new Culled(scatter.FineBroadleaves);
            Rocks = new Culled(scatter.FineRocks);
            Stumps = new Culled(scatter.FineStumps);
            Snags = new Culled(scatter.FineSnags);
            Logs = new Culled(scatter.FineLogs);
        }

        /// <summary>Where the near crowns stand round besides the arena (<see cref="ForestScatterRenderer.FOCUS_RADIUS"/>).</summary>
        public Vector3 Focus => _focus;

        /// <summary>
        /// Fills this pass's instances: moves the focus by <paramref name="lens"/> first, then takes of every kind the tier draws
        /// (<paramref name="fineMeshes"/>) those whose bounds meet <paramref name="viewProjection"/>'s frustum, and on High and
        /// Ultra splits the spruces among them by the crown they wear. A tree outside the frustum puts nothing on the screen or
        /// in the map, so the cut is exact; the split does not look at the frustum, so a tree casts the crown it is drawn in.
        /// </summary>
        /// <param name="viewProjection">The camera's view times its projection, or the sun's map's (its box is the frustum).</param>
        /// <param name="lens">This frame's camera position, in the sun's pass as in the camera's.</param>
        /// <param name="fineMeshes">The caller's <c>SceneRenderer.FineMeshes</c>: which planting and which spruce crowns.</param>
        /// <param name="nearestFirst">Whether to sort each variant's spruces and broadleaves nearest the lens first (the
        /// camera's pass, <see cref="Culled.SortFrom"/>).</param>
        public void Pass(in Matrix viewProjection, Vector3 lens, bool fineMeshes, bool nearestFirst)
        {
            _focus = ForestScatterRenderer.NextFocus(_focused, _focus, _lastLens, lens);
            _focused = true;
            _lastLens = lens;

            _frustum.Matrix = viewProjection;
            Select(fineMeshes);

            if (!nearestFirst) return;
            Conifers.SortFrom(lens);
            NearConifers.SortFrom(lens);
            FarConifers.SortFrom(lens);
            Broadleaves.SortFrom(lens);
        }

        private void Select(bool fineMeshes)
        {
            ModelInstance[][] conifers = _scatter.ConifersAt(fineMeshes);
            for (int v = 0; v < conifers.Length; v++)
            {
                ModelInstance[] bucket = conifers[v];
                ModelInstance[] seen = Conifers.Items[v], near = NearConifers.Items[v], far = FarConifers.Items[v];
                int n = 0, nn = 0, nf = 0;
                BoundingSphere bounds = _coniferBounds[v];
                for (int i = 0; i < bucket.Length; i++)
                {
                    if (!ForestScatterRenderer.Meets(_frustum, bounds, bucket[i].World)) continue;
                    seen[n++] = bucket[i];
                    if (!fineMeshes) continue;
                    if (ForestScatterRenderer.WearsNearCrown(bucket[i].World.Translation, _focus)) near[nn++] = bucket[i];
                    else far[nf++] = bucket[i];
                }
                Conifers.Counts[v] = n;
                NearConifers.Counts[v] = nn;
                FarConifers.Counts[v] = nf;
            }

            SelectKind(_scatter.BroadleavesAt(fineMeshes), _broadleafBounds, Broadleaves);
            SelectKind(_scatter.RocksAt(fineMeshes), _rockBounds, Rocks);
            SelectKind(_scatter.StumpsAt(fineMeshes), _stumpBounds, Stumps);
            SelectKind(_scatter.SnagsAt(fineMeshes), _snagBounds, Snags);
            SelectKind(_scatter.LogsAt(fineMeshes), _logBounds, Logs);
        }

        private void SelectKind(ModelInstance[][] planted, BoundingSphere[] bounds, Culled into)
        {
            for (int v = 0; v < planted.Length; v++)
            {
                ModelInstance[] bucket = planted[v], seen = into.Items[v];
                int n = 0;
                for (int i = 0; i < bucket.Length; i++)
                    if (ForestScatterRenderer.Meets(_frustum, bounds[v], bucket[i].World)) seen[n++] = bucket[i];
                into.Counts[v] = n;
            }
        }

        /// <summary>
        /// The instances of one kind a pass draws, per mesh variant: the first <see cref="Counts"/> of each of
        /// <see cref="Items"/>, written by <see cref="Pass"/> and drawn by the renderer's <c>DrawCulled</c> and
        /// <c>CastCulled</c>.
        /// </summary>
        internal sealed class Culled
        {
            public readonly ModelInstance[][] Items;
            public readonly int[] Counts;

            //Each instance's distance from the lens, squared, the sort's keys
            private readonly float[] _keys;

            public Culled(ModelInstance[][] largest)
            {
                Items = new ModelInstance[largest.Length][];
                Counts = new int[largest.Length];
                int longest = 0;
                for (int v = 0; v < largest.Length; v++)
                {
                    Items[v] = new ModelInstance[largest[v].Length];
                    longest = Math.Max(longest, largest[v].Length);
                }
                _keys = new float[longest];
            }

            /// <summary>
            /// Each variant's instances nearest <paramref name="lens"/> first (#1099), so a near tree's depth is written before the
            /// trees it hides are drawn and the GPU turns their pixels away before shading them. Allocation-free.
            /// </summary>
            public void SortFrom(Vector3 lens)
            {
                for (int v = 0; v < Items.Length; v++)
                {
                    int n = Counts[v];
                    if (n < 2) continue;
                    ModelInstance[] items = Items[v];
                    for (int i = 0; i < n; i++) _keys[i] = Vector3.DistanceSquared(items[i].World.Translation, lens);
                    Array.Sort(_keys, items, 0, n);
                }
            }
        }
    }
}
