using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What one pass of <see cref="ForestScatterRenderer"/> draws (#1099, #1120, #1163), device-free so a test reaches the whole
    /// rule: of every kind the tier draws, the instances whose bounds meet the pass's frustum
    /// (<see cref="ForestScatterRenderer.Meets"/>), and on High and Ultra the spruces among them split by the crown their
    /// distance from this pass's lens gives them (<see cref="ForestScatterRenderer.WearsNearCrown"/>), with the spruces of a
    /// far stand's spires the lens is within the swap of among them (<see cref="ForestScatter.Stand"/>,
    /// <see cref="StandSpruces.Swaps"/>) in the camera's pass. The camera's pass and the sun's both go through
    /// <see cref="Pass"/> with the frame's lens, so a planted tree casts the crown it is drawn in, and nothing carries from one
    /// frame to the next. The stand's spruces cast nothing, as the spires they stand in for never did: a shadow thrown from
    /// two hundred units off would sweep in across the snow each time the swap reached its tree.
    /// <para>
    /// Allocation-free: the arrays a pass fills are made here once, each as long as that variant's largest planting (its
    /// <c>Fine</c> one, which starts with the one every tier draws) and its share of the stand's spruces, and a pass fills them
    /// and draws them before the next one fills them again (each draw uploads what it is handed there and then).
    /// </para>
    /// </summary>
    internal sealed class ForestPasses
    {
        private readonly ForestScatter _scatter;

        //Each variant's whole tree at scale 1 (trunk and every crown it can wear), the sphere a pass culls an instance by
        private readonly BoundingSphere[] _coniferBounds, _broadleafBounds, _rockBounds, _stumpBounds, _snagBounds, _logBounds;

        //The frustum a pass culls by: the camera's, or the sun's map's box. One instance, its matrix set per pass
        private readonly BoundingFrustum _frustum = new(Matrix.Identity);

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
            //The stand's spruces after the planting's, so a variant's arrays hold every spruce a pass can take
            ModelInstance[][] stand = scatter.Stand?.Spruces;
            Conifers = new Culled(scatter.FineConifers, stand);
            NearConifers = new Culled(scatter.FineConifers, stand);
            FarConifers = new Culled(scatter.FineConifers, stand);
            Broadleaves = new Culled(scatter.FineBroadleaves);
            Rocks = new Culled(scatter.FineRocks);
            Stumps = new Culled(scatter.FineStumps);
            Snags = new Culled(scatter.FineSnags);
            Logs = new Culled(scatter.FineLogs);
        }

        /// <summary>
        /// Fills this pass's instances: takes of every kind the tier draws (<paramref name="fineMeshes"/>) those whose bounds
        /// meet <paramref name="viewProjection"/>'s frustum, in the camera's pass on High and Ultra with the stand's spruces
        /// <paramref name="lens"/> is within the swap of, and there splits the spruces among them by the crown their distance
        /// from <paramref name="lens"/> gives them. A tree outside the frustum puts nothing on the screen or in the map, so the
        /// cut is exact; the split does not look at the frustum, so a tree casts the crown it is drawn in.
        /// </summary>
        /// <param name="viewProjection">The camera's view times its projection, or the sun's map's (its box is the frustum).</param>
        /// <param name="lens">This frame's camera position, in the sun's pass as in the camera's.</param>
        /// <param name="fineMeshes">The caller's <c>SceneRenderer.FineMeshes</c>: which planting and which spruce crowns.</param>
        /// <param name="camera">Whether this is the camera's pass, not the sun's: it takes the stand's spruces, and it sorts
        /// each variant's spruces and broadleaves nearest the lens first (<see cref="Culled.SortFrom"/>).</param>
        public void Pass(in Matrix viewProjection, Vector3 lens, bool fineMeshes, bool camera)
        {
            _frustum.Matrix = viewProjection;
            Select(fineMeshes, lens, camera);

            if (!camera) return;
            Conifers.SortFrom(lens);
            NearConifers.SortFrom(lens);
            FarConifers.SortFrom(lens);
            Broadleaves.SortFrom(lens);
        }

        private void Select(bool fineMeshes, Vector3 lens, bool camera)
        {
            ModelInstance[][] conifers = _scatter.ConifersAt(fineMeshes);
            StandSpruces stand = _scatter.Stand;
            bool swaps = camera && fineMeshes && stand != null && stand.Reaches(lens);
            for (int v = 0; v < conifers.Length; v++)
            {
                ModelInstance[] seen = Conifers.Items[v], near = NearConifers.Items[v], far = FarConifers.Items[v];
                int n = 0, nn = 0, nf = 0;
                BoundingSphere bounds = _coniferBounds[v];

                ModelInstance[] bucket = conifers[v];
                for (int i = 0; i < bucket.Length; i++)
                {
                    if (!ForestScatterRenderer.Meets(_frustum, bounds, bucket[i].World)) continue;
                    seen[n++] = bucket[i];
                    if (!fineMeshes) continue;
                    if (ForestScatterRenderer.WearsNearCrown(bucket[i].World.Translation, lens)) near[nn++] = bucket[i];
                    else far[nf++] = bucket[i];
                }

                //The far stand's spires the lens is within the swap of, as spruces (#1163): the backdrop draws none of them
                if (swaps)
                {
                    ModelInstance[] spruces = stand.Spruces[v];
                    Vector2[] feet = stand.Feet[v];
                    for (int i = 0; i < spruces.Length; i++)
                    {
                        if (!StandSpruces.Swaps(feet[i].X, feet[i].Y, lens)) continue;
                        if (!ForestScatterRenderer.Meets(_frustum, bounds, spruces[i].World)) continue;
                        seen[n++] = spruces[i];
                        if (ForestScatterRenderer.WearsNearCrown(spruces[i].World.Translation, lens)) near[nn++] = spruces[i];
                        else far[nf++] = spruces[i];
                    }
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

            /// <summary>Room for each variant's <paramref name="largest"/> planting and, where given, its <paramref name="more"/>.</summary>
            public Culled(ModelInstance[][] largest, ModelInstance[][] more = null)
            {
                Items = new ModelInstance[largest.Length][];
                Counts = new int[largest.Length];
                int longest = 0;
                for (int v = 0; v < largest.Length; v++)
                {
                    int length = largest[v].Length + (more != null ? more[v].Length : 0);
                    Items[v] = new ModelInstance[length];
                    longest = Math.Max(longest, length);
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
