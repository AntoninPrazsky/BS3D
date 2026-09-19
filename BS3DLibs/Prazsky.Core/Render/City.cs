using Microsoft.Xna.Framework;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A procedurally generated city, drawn as one instanced box per building. No assets: the layout,
    /// the footprints and the heights all come out of a seeded generator, and the windows are evaluated
    /// in the shader from world position, so a building carries no texture and no UVs.
    /// <para>
    /// The play surface sits low among the towers, which is what the whole arrangement is for: the arena
    /// is the small round stone island in a clearing between skyscrapers, and the blocks under it are
    /// left out rather than built and hidden (#275) — no camera the game uses ever looks down the drain
    /// funnel far enough to see them. The towers stand on a street level at <see cref="GroundY"/> (#399),
    /// which <see cref="CityStreets"/> draws from the same grid this class lays the blocks out on.
    /// Buildings are scaled non-uniformly through their instance matrix, which is safe here because the
    /// boxes are axis-aligned in object space — a diagonal scale maps each face normal onto itself, so
    /// normalizing in the pixel shader recovers it without an inverse transpose.
    /// </para>
    /// Drawn by an <see cref="InstancedModelRenderer"/> with <c>CityWindowBrightness &gt; 0</c> (the shared
    /// InstancedModel city technique); the game and the map editor each own one, so the city takes part in
    /// their sky lighting like every other instanced object.
    /// </summary>
    public sealed class City
    {
        /// <summary>Every building the generator made, in generator order. <see cref="Visible"/> is what to draw.</summary>
        public ModelInstance[] Buildings { get; }

        /// <summary>
        /// The layout this city was built on, taken from its config at construction (#399). The street level
        /// reads these rather than the config itself, so a config edited after the build — the map editor's
        /// live panel — cannot draw streets through towers the city has not been rebuilt to match.
        /// </summary>
        public float BlockPitch { get; }

        /// <inheritdoc cref="BlockPitch"/>
        public float StreetWidth { get; }

        /// <summary>How many blocks the grid reaches from the centre each way; the grid is <c>2 × this + 1</c> blocks square.</summary>
        public int RadiusBlocks { get; }

        /// <summary>Where every building starts: the street level (<see cref="CitySceneConfig.BaseY"/> when built).</summary>
        public float GroundY { get; }

        /// <summary>
        /// Whether each block of the grid carries at least one building, row by row in <c>z</c> then <c>x</c>
        /// from <c>-RadiusBlocks</c>: <c>BlockBuilt[(z + R) * (2R + 1) + (x + R)]</c>. A block left out — the
        /// clearing under the arena and the generator's scattered plazas — is where the street level lays
        /// paving instead of a sidewalk, and the density of built blocks around a street is how deep a canyon
        /// it stands in.
        /// </summary>
        public bool[] BlockBuilt { get; }

        /// <summary>
        /// The buildings worth drawing this frame — those inside the frustum, ordered near to far — filled by
        /// <see cref="PrepareVisible"/>, which returns how many of them are live. Never reallocated, so a
        /// caller may hold on to it; only the first <c>count</c> entries mean anything.
        /// </summary>
        public ModelInstance[] Visible { get; }

        //Per-building bounds, kept alongside the instances so the per-frame pass never has to take a matrix
        //apart. The boxes are axis-aligned and scale-then-translate (no rotation), so the world matrix's
        //diagonal IS the size and its translation IS the centre — see the constructor.
        private readonly Vector3[] _centres;
        private readonly float[] _radii;

        //Counting-sort scratch. Front-to-back ordering only has to be APPROXIMATE — it exists so the depth
        //test rejects a hidden pixel before its shader runs, and a bucket out of place costs a few pixels of
        //overdraw, not correctness — so this is O(n) buckets rather than an O(n log n) comparison sort, and it
        //moves each instance exactly once. Both arrays are allocated with the city and cleared per frame, so
        //the pass allocates nothing (see the render-hygiene rules in BestPractices.md).
        private const int DEPTH_BUCKETS = 256;
        private readonly int[] _bucketCounts = new int[DEPTH_BUCKETS + 1];
        private readonly float _bucketScale;

        //BoundingFrustum is a CLASS, so constructing one per frame would allocate on the gameplay path. Held
        //and re-pointed instead: assigning Matrix re-derives its six planes in place.
        private readonly BoundingFrustum _frustum = new(Matrix.Identity);

        //Layout/generator parameters (block pitch, street width, radius, roofline, taper, base Y) live in
        //CitySceneConfig; the constructor reads them from the config passed in. The window look
        //and the neon relight are wired too: InstancedModelRenderer.CityConfig pushes them to the shader on
        //every city draw, and the caller reads WindowBrightness/NeonLook for the day/neon switch.

        /// <summary>The city has no neighboring-cell occlusion; the shader still expects the vector.</summary>
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        /// <param name="seed">Layout seed; the same seed always gives the same city.</param>
        /// <param name="arenaHalfExtent">
        /// Half-width of the play surface. Blocks whose footprint would reach into it are left out, so
        /// the arena sits in a clearing rather than inside a building.
        /// </param>
        /// <param name="config">The city's layout configuration (block pitch, radius, roofline, taper, base).</param>
        /// <param name="config">The scene's own dials — both cities' layouts live on it.</param>
        /// <param name="neon">Which of the two this is. <b>They are different cities, not one city relit</b>
        /// (the owner's report, #471's follow-up): a second seed and a skyline of its own, so no block carries
        /// the same tower at the same size. See <see cref="CitySceneConfig.NeonLayout"/>.</param>
        /// <param name="arenaHalfExtent">The clearing the island stands in.</param>
        public City(CitySceneConfig config, bool neon, float arenaHalfExtent)
        {
            CityLayout layout = config.LayoutFor(neon);
            Random random = new(layout.Seed);
            List<ModelInstance> buildings = new();

            float buildable = layout.BlockPitch - layout.StreetWidth;

            BlockPitch = layout.BlockPitch;
            StreetWidth = layout.StreetWidth;
            RadiusBlocks = layout.RadiusBlocks;
            GroundY = layout.BaseY;

            int blocksPerSide = 2 * layout.RadiusBlocks + 1;
            BlockBuilt = new bool[blocksPerSide * blocksPerSide];

            for (int blockX = -layout.RadiusBlocks; blockX <= layout.RadiusBlocks; blockX++)
                for (int blockZ = -layout.RadiusBlocks; blockZ <= layout.RadiusBlocks; blockZ++)
                {
                    Vector2 blockCenter = new(blockX * layout.BlockPitch, blockZ * layout.BlockPitch);

                    //Blocks under the arena are left out entirely (#275) rather than kept and cut short: no
                    //play or drop-cinematic camera angle ever looks down the funnel's narrow throat far
                    //enough to see them, so the earlier "cut-off towers open a drop" shaft was cost with no
                    //payoff. The clearing above still needs no separate check — the roofline never reaches
                    //down to it either way.
                    float clearance = arenaHalfExtent + layout.StreetWidth;
                    if (Math.Abs(blockCenter.X) < clearance + buildable * 0.5f &&
                        Math.Abs(blockCenter.Y) < clearance + buildable * 0.5f) continue;

                    //A gap in the grid every so often: a plaza, or something demolished. Without them the
                    //regularity of the streets reads as a spreadsheet rather than as a city.
                    if (random.NextDouble() < 0.07) continue;

                    int subdivisions = random.NextDouble() < 0.45 ? 2 : 1;

                    for (int sx = 0; sx < subdivisions; sx++)
                        for (int sz = 0; sz < subdivisions; sz++)
                        {
                            float plot = buildable / subdivisions;

                            //Inset each building inside its plot by a random margin, so neighbouring
                            //facades do not all line up flush along the street
                            float sizeX = plot * (float)(0.72 + random.NextDouble() * 0.24);
                            float sizeZ = plot * (float)(0.72 + random.NextDouble() * 0.24);

                            float offsetX = (sx - (subdivisions - 1) * 0.5f) * plot;
                            float offsetZ = (sz - (subdivisions - 1) * 0.5f) * plot;

                            float distanceInBlocks = MathF.Max(Math.Abs(blockX), Math.Abs(blockZ));

                            float top = layout.RooflineY
                                - distanceInBlocks * layout.TaperPerBlock
                                + (float)(random.NextDouble() * 2 - 1) * layout.RooflineSpread;

                            float height = top - layout.BaseY;
                            if (height <= 1f) continue;

                            Vector3 center = new(
                                blockCenter.X + offsetX,
                                layout.BaseY + height * 0.5f,
                                blockCenter.Y + offsetZ);

                            //Scale then translate: no rotation, so the box stays axis-aligned and its
                            //normals survive the non-uniform scale
                            Matrix world = Matrix.CreateScale(sizeX, height, sizeZ) * Matrix.CreateTranslation(center);

                            buildings.Add(new ModelInstance(world, NO_OCCLUSION));
                            BlockBuilt[(blockZ + layout.RadiusBlocks) * blocksPerSide + blockX + layout.RadiusBlocks] = true;
                        }
                }

            Buildings = buildings.ToArray();

            //The bounds the per-frame pass works from. A building's world matrix is CreateScale * translation
            //with no rotation, so M41..M43 is its centre and the diagonal M11/M22/M33 its full size; half of
            //that diagonal's length is the radius of a sphere around the box, which is what the frustum test
            //and the depth key both use. Taken once here rather than per frame per building.
            Visible = new ModelInstance[Buildings.Length];
            _centres = new Vector3[Buildings.Length];
            _radii = new float[Buildings.Length];

            float farthest = 1f;
            for (int i = 0; i < Buildings.Length; i++)
            {
                Matrix world = Buildings[i].World;

                Vector3 centre = new(world.M41, world.M42, world.M43);
                float radius = 0.5f * new Vector3(world.M11, world.M22, world.M33).Length();

                _centres[i] = centre;
                _radii[i] = radius;

                farthest = MathF.Max(farthest, centre.Length() + radius);
            }

            //Buckets span twice the city's own reach, which is the worst case for a camera standing at one
            //edge and looking at the other. Beyond that everything lands in the last bucket, which is correct
            //— those are the farthest buildings and belong last.
            _bucketScale = DEPTH_BUCKETS / (2f * farthest);
        }

        /// <summary>
        /// Picks the buildings worth drawing from where the camera stands and orders them <b>near to far</b>,
        /// into <see cref="Visible"/>; returns how many. Call once per frame before the city's draw.
        /// <para>
        /// The ordering is the point, and it is not the obvious one. Off-screen buildings are nearly free
        /// already — they are clipped before rasterization, so they cost vertex work and no pixels — whereas a
        /// building that is on screen but <i>behind another one</i> costs a full shaded pixel for every pixel
        /// it covers, and the city's pixel shader is an expensive one (a window grid, facade grain, the sun,
        /// the sky hemisphere and the cloud shadow). Drawn in generator order those hidden pixels are all
        /// shaded and then overwritten; drawn near to far the depth test rejects them before the shader runs.
        /// The frustum cull rides along because it is nearly free and it shortens this pass's own work.
        /// </para>
        /// <para>
        /// This is only sound because the city's pixel shader neither discards nor writes depth — a shader
        /// that did either would force the hardware to run it before the depth test and this would buy
        /// nothing.
        /// </para>
        /// </summary>
        public int PrepareVisible(ICamera camera)
        {
            _frustum.Matrix = camera.View * camera.Projection;
            Vector3 eye = camera.Position;

            Array.Clear(_bucketCounts, 0, _bucketCounts.Length);

            //Pass one: cull, and count how many survivors land in each depth bucket. The key is distance to
            //the building's NEAR side (centre distance less its radius), so a big tower close by sorts ahead
            //of a small one whose centre happens to be nearer — it is the near surface that does the
            //occluding.
            int visible = 0;
            for (int i = 0; i < Buildings.Length; i++)
            {
                if (_frustum.Contains(new BoundingSphere(_centres[i], _radii[i])) == ContainmentType.Disjoint) continue;

                _bucketCounts[BucketOf(_centres[i], _radii[i], eye)]++;
                visible++;
            }

            //Prefix sum: each bucket's count becomes the slot its first member takes.
            int running = 0;
            for (int b = 0; b < DEPTH_BUCKETS; b++)
            {
                int count = _bucketCounts[b];
                _bucketCounts[b] = running;
                running += count;
            }

            //Pass two: place each survivor. Repeating the cull test rather than remembering pass one's
            //verdicts keeps this allocation-free without a second scratch array, and a frustum-sphere test is
            //a handful of dot products against work the GPU is about to do per pixel.
            for (int i = 0; i < Buildings.Length; i++)
            {
                if (_frustum.Contains(new BoundingSphere(_centres[i], _radii[i])) == ContainmentType.Disjoint) continue;

                Visible[_bucketCounts[BucketOf(_centres[i], _radii[i], eye)]++] = Buildings[i];
            }

            return visible;
        }

        /// <summary>Depth bucket of a building's near side, clamped into the table.</summary>
        private int BucketOf(Vector3 centre, float radius, Vector3 eye)
        {
            float near = Vector3.Distance(centre, eye) - radius;
            int bucket = (int)(near * _bucketScale);

            if (bucket < 0) bucket = 0;
            if (bucket >= DEPTH_BUCKETS) bucket = DEPTH_BUCKETS - 1;

            return bucket;
        }
    }
}
