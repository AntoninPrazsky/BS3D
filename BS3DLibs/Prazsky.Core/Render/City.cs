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
        /// <summary>
        /// Every box the generator made, in generator order: the towers' boxes (one per setback tier) first, then the
        /// cornices (#678). <see cref="Visible"/> is what to draw; the first <see cref="TowerCount"/> are the towers.
        /// </summary>
        public ModelInstance[] Buildings { get; }

        /// <summary>
        /// How many of <see cref="Buildings"/> are towers' boxes, the rest being cornices. What stands on a roof or reads
        /// a roofline (<see cref="CityRooftops"/>, the chapter intro's rooftop run) walks these alone: a cornice is a
        /// slab laid over a roof, and dressed as a roof of its own it would stand a second set of dishes on the first.
        /// </summary>
        public int TowerCount { get; }

        /// <summary>
        /// How far the open ground round the island reaches, in world units: the least distance in the ground plane from the
        /// arena's axis to any box of <see cref="Buildings"/>, cornices included. A lens closer to the axis than this is in
        /// the clearing at any height; one further out can stand inside a tower. The front end's orbit is held inside it,
        /// because a narrow window stands its wide leg far enough out to pass through the first towers (the owner's report
        /// on #1039).
        /// </summary>
        public float OpenRadius { get; }

        /// <summary>
        /// The far skyline round the grid (#774): plain slabs past the last block, so a street's view ends in a city and
        /// not in the sky. Not among <see cref="Buildings"/>: drawn by <see cref="CityRooftops"/> through the plain lit
        /// technique, since in the city's own program every pixel of them cost the chapter intro's street swing up to
        /// 0.45 ms where the sky had been, and they never come near the sun's map, the swing's path or a roof.
        /// </summary>
        public ModelInstance[] FarSkyline { get; }

        /// <summary>
        /// How far above its own box a tower box's roof really stands (#678): the cornice is a solid slab over the whole
        /// roof, its top <c>CORNICE_OVER_ROOF</c> above the tier's, so a crowned box's roof is that much higher and what
        /// stands on it has to stand there, or its foot is buried in the stone. 0 for an uncrowned box.
        /// </summary>
        public float RoofRise(int towerBox) => _crowned[towerBox] ? CORNICE_OVER_ROOF : 0f;

        //Which of the tower boxes wear a cornice, by index into Buildings
        private readonly bool[] _crowned;

        /// <summary>
        /// Every plot the generator built on, in generator order, with its plan and the run of <see cref="Buildings"/> its
        /// tower's boxes take (#1066). For the tests: each plan stays inside its plot and reaches the plot's top.
        /// </summary>
        internal IReadOnlyList<CityPlot> Plots { get; }

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

        //How tall a tower must be to rise in setback tiers, and how many of those that are tall enough do (#678)
        private const float SETBACK_MIN_HEIGHT = 60f;
        private const float SETBACK_CHANCE = 0.45f;

        //The cornice a stone tower is crowned with (#678's third step): how far it stands out from the wall, how tall it
        //is, and how far its top stands over the tier's own roof. It is a solid slab over the whole roof, not a rim, so
        //that is how much it RAISES the roof (RoofRise), not a parapet round it. A pitch of window is 1.7 across and 2.2
        //up, so the slab reads as a moulding a floor deep rather than as another storey.
        private const float CORNICE_OUT = 0.5f, CORNICE_HEIGHT = 1.1f, CORNICE_OVER_ROOF = 0.4f;

        //City.fxh's facade kinds: the share of the rolls under which a tower wears the classic punched windows, and the
        //roll's salt.
        private const float CLASSIC_FACADE_SHARE = 0.5f;
        private const uint FACADE_SALT = 678u;

        //THE PLANS (#1066). The owner: every tower was squarish. A plot's footprint was one rectangle, its sides within
        //1.33 : 1 of each other, and every setback tier shrank about the same centre, so from any height the city was
        //square boxes. Now each tower rolls a plan, off the plot's own seed with this salt (the layout's generator and the
        //tiers' stream are drawn exactly as before, so every block, gap and height stays):
        //  - a plain box (PLAN_BOX), as before;
        //  - a SLAB: one side narrowed to SLAB_NARROW of itself and slid along the other;
        //  - an L: a main block across one side of the footprint and a lower wing down the other, set back WING_INSET
        //    from the main block's wall, so no two of a building's walls stand in one plane and lay their window grids
        //    side by side (each box lays out its own: two flush walls would meet in a doubled blank margin);
        //  - a tower on a PODIUM: a low base over the whole footprint and the tower standing off-centre on it;
        //  - TWIN towers on one podium, on footprints long enough to stand two.
        //and a stack of setback tiers may LEAN: each tier slides towards a side or a corner by what the step leaves over
        //TIER_STEP_MIN, so the setbacks climb up one face instead of round the middle.
        //⚠ Every box stays inside the plot's old footprint and no higher than its old top: the chapter intros' flights,
        //the menu orbit and the arena's clearing were laid among those boxes and stay clear of their parts by construction.
        private const int PLAN_SALT = 0x10_66;
        private const float PLAN_BOX = 0.34f, PLAN_SLAB = 0.18f, PLAN_ELL = 0.18f, PLAN_PODIUM = 0.18f;   //the twins take the rest
        private const float SLAB_NARROW_MIN = 0.48f, SLAB_NARROW_SPREAD = 0.14f;
        private const float WING_INSET = 1.5f;          //how far an L's wing stands back from its main block's wall
        private const float WING_OVERLAP = 0.5f;        //how far the wing reaches into the main block, so the join has no crack
        private const float ELL_MIN_SIDE = 9f;          //the footprint's long side an L needs, or the wing is a sliver
        private const float TWIN_MIN_SIDE = 14f;        //the long side two towers need, a street's gap between them
        private const float PODIUM_MIN_TOWER = 40f;     //the height a tower needs to stand on a podium at all
        private const float PODIUM_MIN_HEIGHT = 7f;     //a podium's least height: three floors
        private const float TIER_STEP_MIN = 1.5f;       //the least step a leaning tier, a podium's tower or a twin keeps from the edge below
        private const float TIER_MIN_SIDE = 2.5f;       //the narrowest a setback tier may be (the plain towers' narrowest was 2.74)
        private const float WING_BELOW = 2.2f;          //how far an L's wing stays under its main block's first roof: a floor
        private const float LEAN_SHARE = 0.55f;         //how many of the stacks lean

        /// <param name="config">The scene's own dials — both cities' layouts live on it (block pitch, radius,
        /// roofline, taper, base, and the layout seed: the same seed always gives the same city).</param>
        /// <param name="neon">Which of the two this is. <b>They are different cities, not one city relit</b>
        /// (the owner's report, #471's follow-up): a second seed and a skyline of its own, so no block carries
        /// the same tower at the same size. See <see cref="CitySceneConfig.NeonLayout"/>.</param>
        /// <param name="arenaHalfExtent">
        /// The clearing the island stands in: half-width of the play surface. Blocks whose footprint would reach
        /// into it are left out, so the arena sits in a clearing rather than inside a building.
        /// </param>
        public City(CitySceneConfig config, bool neon, float arenaHalfExtent) : this(config, neon, arenaHalfExtent, variedPlans: true)
        {
        }

        /// <param name="variedPlans">False builds every tower on its plain footprint, as before #1066: the tests compare the
        /// two cities' plots, which is how they show the plans changed no block, gap or height.</param>
        internal City(CitySceneConfig config, bool neon, float arenaHalfExtent, bool variedPlans)
        {
            CityLayout layout = config.LayoutFor(neon);
            Random random = new(layout.Seed);
            List<ModelInstance> buildings = new();
            List<ModelInstance> cornices = new();
            List<int> crowned = new();
            List<CityPlot> plots = new();

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

                            //SETBACKS (#678). Every tower was one box, so ~1800 buildings were one building at
                            //different sizes; the #671 audit's references draw a downtown of kinds - slabs, stepped
                            //Art Deco towers, a slender top on a broad base. A tall enough tower may rise in two or
                            //three TIERS, each narrower than the one under it, to the same top: the skyline's heights
                            //stay exactly what they were and only the silhouettes change. Rolled from the plot's own
                            //seed, never the layout's generator, so every block, gap and height is the same city as
                            //before. Each tier is its own box and lays out its own windows (City.fxh does it per box);
                            //every box of a tower carries its anchor (below), so they share its hue and its pattern.
                            Random shape = new(unchecked(layout.Seed * 31 + (blockX + 97) * 7919 + (blockZ + 89) * 104729 + sx * 13 + sz * 7));
                            int tiers = height > SETBACK_MIN_HEIGHT && shape.NextDouble() < SETBACK_CHANCE
                                ? (shape.NextDouble() < 0.4 ? 3 : 2) : 1;

                            //ITS KIND OF FACADE (#678), rolled here and carried to City.fxh in the instance's custom vector
                            //(x), which the city never used: it has no neighbour occlusion, and CityPS hands the lighting
                            //the no-occlusion vector itself. It was rolled in the shader first, on the tower's centre, with a
                            //copy here deciding the cornices; the two agreed tower for tower when checked, but a copy on
                            //each side of the CPU/GPU line stays right only while both are kept in step, and one roll in one
                            //place cannot disagree at all. Every box and the cornices share it, so a tower is one kind and
                            //one tone from its foot to its cornice. The plot's centre rides in (z, w), the building's
                            //identity: its tone, its neon and its windows' rhythm are one building's whatever box they
                            //are drawn on (#1066), which an off-centre box's own centre would not give.
                            Vector2 anchor = new(blockCenter.X + offsetX, blockCenter.Y + offsetZ);
                            float kind = FacadeRoll(anchor.X, anchor.Y);

                            //ITS PLAN (#1066), off a generator of its own (the plot's seed with PLAN_SALT), so the shape's
                            //stream above - the tiers and their shares - is drawn exactly as it was
                            var tower = new TowerBuilder(buildings, cornices, crowned, shape, anchor, kind);
                            int firstBox = buildings.Count;
                            CityPlan plan = CityPlan.Box;
                            if (variedPlans)
                                plan = tower.AddPlan(new Random(unchecked(layout.Seed * 31 + (blockX + 97) * 7919 + (blockZ + 89) * 104729
                                    + sx * 13 + sz * 7) ^ PLAN_SALT), sizeX, sizeZ, layout.BaseY, top, tiers);
                            else
                                tower.AddStack(anchor, sizeX, sizeZ, layout.BaseY, top, tiers, Vector2.Zero);
                            plots.Add(new CityPlot(anchor, plot, top, plan, firstBox, buildings.Count - firstBox));
                            BlockBuilt[(blockZ + layout.RadiusBlocks) * blocksPerSide + blockX + layout.RadiusBlocks] = true;
                        }
                }

            TowerCount = buildings.Count;
            Plots = plots;
            _crowned = new bool[TowerCount];
            foreach (int box in crowned) _crowned[box] = true;
            buildings.AddRange(cornices);
            Buildings = buildings.ToArray();
            var skyline = new List<ModelInstance>();
            AddFarSkyline(skyline, layout);
            FarSkyline = skyline.ToArray();

            //The bounds the per-frame pass works from. A building's world matrix is CreateScale * translation
            //with no rotation, so M41..M43 is its centre and the diagonal M11/M22/M33 its full size; half of
            //that diagonal's length is the radius of a sphere around the box, which is what the frustum test
            //and the depth key both use. Taken once here rather than per frame per building.
            Visible = new ModelInstance[Buildings.Length];
            _centres = new Vector3[Buildings.Length];
            _radii = new float[Buildings.Length];

            float farthest = 1f;
            float open = float.PositiveInfinity;
            for (int i = 0; i < Buildings.Length; i++)
            {
                Matrix world = Buildings[i].World;

                Vector3 centre = new(world.M41, world.M42, world.M43);
                float radius = 0.5f * new Vector3(world.M11, world.M22, world.M33).Length();

                _centres[i] = centre;
                _radii[i] = radius;

                farthest = MathF.Max(farthest, centre.Length() + radius);

                //The box's footprint is the rectangle of half-sizes M11/2 by M33/2 about its centre: its nearest point to
                //the axis is past each half-size on each axis, or on the axis itself where the box straddles it
                float outX = MathF.Max(0f, MathF.Abs(centre.X) - 0.5f * MathF.Abs(world.M11));
                float outZ = MathF.Max(0f, MathF.Abs(centre.Z) - 0.5f * MathF.Abs(world.M33));
                open = MathF.Min(open, MathF.Sqrt(outX * outX + outZ * outZ));
            }

            OpenRadius = open;

            //Buckets span twice the city's own reach, which is the worst case for a camera standing at one
            //edge and looking at the other. Beyond that everything lands in the last bucket, which is correct
            //— those are the farthest buildings and belong last.
            _bucketScale = DEPTH_BUCKETS / (2f * farthest);
        }

        //One tower's boxes, from its plan (#1066): every box wears the tower's facade kind, carries its anchor and, under the
        //classic punched windows, a cornice. Built and dropped per plot at construction.
        private readonly struct TowerBuilder
        {
            private readonly List<ModelInstance> _boxes, _cornices;
            private readonly List<int> _crowned;
            private readonly Random _shape;
            private readonly Vector2 _anchor;
            private readonly float _kind;

            public TowerBuilder(List<ModelInstance> boxes, List<ModelInstance> cornices, List<int> crowned, Random shape,
                Vector2 anchor, float kind)
            {
                _boxes = boxes; _cornices = cornices; _crowned = crowned; _shape = shape; _anchor = anchor; _kind = kind;
            }

            public CityPlan AddPlan(Random roll, float sizeX, float sizeZ, float bottom, float top, int tiers)
            {
                float height = top - bottom;
                float longSide = MathF.Max(sizeX, sizeZ);

                //The lean of any stack this tower raises: a side or a corner, or none
                Vector2 lean = Vector2.Zero;
                if (roll.NextDouble() < LEAN_SHARE)
                {
                    int way = roll.Next(8);
                    lean = way < 4 ? new Vector2(way < 2 ? (way == 0 ? 1f : -1f) : 0f, way < 2 ? 0f : (way == 2 ? 1f : -1f))
                        : new Vector2((way & 1) == 0 ? 1f : -1f, (way & 2) == 0 ? 1f : -1f);
                }

                double pick = roll.NextDouble();
                CityPlan plan = pick < PLAN_BOX ? CityPlan.Box
                    : pick < PLAN_BOX + PLAN_SLAB ? CityPlan.Slab
                    : pick < PLAN_BOX + PLAN_SLAB + PLAN_ELL ? CityPlan.Ell
                    : pick < PLAN_BOX + PLAN_SLAB + PLAN_ELL + PLAN_PODIUM ? CityPlan.Podium
                    : CityPlan.Twin;
                if (plan == CityPlan.Twin && longSide < TWIN_MIN_SIDE) plan = CityPlan.Podium;
                if (plan is CityPlan.Podium or CityPlan.Twin && height < PODIUM_MIN_TOWER) plan = CityPlan.Box;
                if (plan == CityPlan.Ell && longSide < ELL_MIN_SIDE) plan = CityPlan.Slab;

                switch (plan)
                {
                    case CityPlan.Slab:
                    {
                        //One side keeps its length and the other narrows, the slab slid somewhere along the narrowed side
                        bool longAlongX = roll.Next(2) == 0;
                        float narrow = SLAB_NARROW_MIN + SLAB_NARROW_SPREAD * (float)roll.NextDouble();
                        float slide = (float)roll.NextDouble() * 2f - 1f;
                        float w = longAlongX ? sizeX : sizeX * narrow, d = longAlongX ? sizeZ * narrow : sizeZ;
                        Vector2 at = _anchor + (longAlongX ? new Vector2(0f, slide * (sizeZ - d) * 0.5f) : new Vector2(slide * (sizeX - w) * 0.5f, 0f));
                        AddStack(at, w, d, bottom, top, tiers, lean);
                        break;
                    }
                    case CityPlan.Ell:
                    {
                        //Worked in the footprint's own (u, v), a half-size a by b, then turned into the world: the main block
                        //takes the far band of v across the whole of u, the wing runs down the near rest of v at the far end
                        //of u, WING_INSET short of the main block's wall at u = a
                        bool swap = roll.Next(2) == 0;
                        float su = roll.Next(2) == 0 ? 1f : -1f, sv = roll.Next(2) == 0 ? 1f : -1f;
                        float a = (swap ? sizeZ : sizeX) * 0.5f, b = (swap ? sizeX : sizeZ) * 0.5f;
                        float mainDepth = 2f * b * (0.5f + 0.15f * (float)roll.NextDouble());
                        float wingWidth = 2f * a * (0.38f + 0.17f * (float)roll.NextDouble());
                        float wingShare = 0.32f + 0.38f * (float)roll.NextDouble();

                        Vector2 anchor = _anchor;
                        Vector2 World(float u, float v) => anchor + (swap ? new Vector2(v * sv, u * su) : new Vector2(u * su, v * sv));
                        Vector2 Size(float u, float v) => swap ? new Vector2(v, u) : new Vector2(u, v);

                        Vector2 mainSize = Size(2f * a, mainDepth);
                        float firstTop = AddStack(World(0f, b - mainDepth * 0.5f), mainSize.X, mainSize.Y, bottom, top, tiers, lean);

                        //The wing is lower than the main block's first tier, so its roof meets a wall and never the sky over a setback
                        //(at least a floor lower: level with it, the two roofs and their cornices lay in one plane over
                        //the overlap and fought for the same pixels)
                        float wingTop = MathF.Min(bottom + height * wingShare, firstTop - WING_BELOW);
                        float wingFrom = -b, wingTo = b - mainDepth + WING_OVERLAP;
                        Vector2 wingSize = Size(wingWidth, wingTo - wingFrom);
                        AddBox(World(a - WING_INSET - wingWidth * 0.5f, (wingFrom + wingTo) * 0.5f), wingSize.X, wingSize.Y, bottom, wingTop);
                        break;
                    }
                    case CityPlan.Podium:
                    {
                        float podiumTop = bottom + MathF.Max(PODIUM_MIN_HEIGHT, height * (0.1f + 0.1f * (float)roll.NextDouble()));
                        AddBox(_anchor, sizeX, sizeZ, bottom, podiumTop);

                        //The tower on it, off-centre wherever the lean says (a podium's tower leans even when its tiers do not)
                        float towerX = sizeX * (0.5f + 0.22f * (float)roll.NextDouble());
                        float towerZ = sizeZ * (0.5f + 0.22f * (float)roll.NextDouble());
                        Vector2 standLean = lean == Vector2.Zero ? new Vector2(roll.Next(3) - 1, roll.Next(3) - 1) : lean;
                        Vector2 at = _anchor + new Vector2(
                            standLean.X * MathF.Max(0f, (sizeX - towerX) * 0.5f - TIER_STEP_MIN),
                            standLean.Y * MathF.Max(0f, (sizeZ - towerZ) * 0.5f - TIER_STEP_MIN));
                        AddStack(at, towerX, towerZ, podiumTop, top, tiers, lean);
                        break;
                    }
                    case CityPlan.Twin:
                    {
                        float podiumTop = bottom + MathF.Max(PODIUM_MIN_HEIGHT, height * (0.08f + 0.08f * (float)roll.NextDouble()));
                        AddBox(_anchor, sizeX, sizeZ, bottom, podiumTop);

                        //Two towers along the long side, a gap between them and TIER_STEP_MIN to each end; one reaches the
                        //plot's top and the other stops short of it, which of the two by the roll
                        bool longAlongX = sizeX >= sizeZ;
                        float length = longAlongX ? sizeX : sizeZ, across = longAlongX ? sizeZ : sizeX;
                        float room = length - 2f * TIER_STEP_MIN;
                        float width = room * (0.36f + 0.06f * (float)roll.NextDouble());
                        float depth = across * (0.55f + 0.25f * (float)roll.NextDouble());
                        float apart = (room - 2f * width) * 0.5f + width * 0.5f;
                        float shift = ((float)roll.NextDouble() * 2f - 1f) * MathF.Max(0f, (across - depth) * 0.5f - TIER_STEP_MIN);
                        float shorterTop = podiumTop + (top - podiumTop) * (0.75f + 0.2f * (float)roll.NextDouble());
                        bool firstTall = roll.Next(2) == 0;
                        for (int twin = 0; twin < 2; twin++)
                        {
                            float along = twin == 0 ? -apart : apart;
                            Vector2 at = _anchor + (longAlongX ? new Vector2(along, shift) : new Vector2(shift, along));
                            float twinTop = (twin == 0) == firstTall ? top : shorterTop;
                            AddBox(at, longAlongX ? width : depth, longAlongX ? depth : width, podiumTop, twinTop);
                        }
                        break;
                    }
                    default:
                        AddStack(_anchor, sizeX, sizeZ, bottom, top, tiers, lean);
                        break;
                }
                return plan;
            }

            /// <summary>
            /// A stack of setback tiers over the rectangle centred on <paramref name="centre"/> (#678): a tall enough tower
            /// rises in two or three, each narrower than the one under it, all reaching <paramref name="top"/>. Off the
            /// plot's shape stream, drawn in the order it always was. A tier slides towards <paramref name="lean"/> by
            /// what its step leaves over <c>TIER_STEP_MIN</c>. Returns the first tier's top.
            /// </summary>
            public float AddStack(Vector2 centre, float sizeX, float sizeZ, float bottom, float top, int tiers, Vector2 lean)
            {
                float firstTop = top, tierX = sizeX, tierZ = sizeZ;
                for (int tier = 0; tier < tiers; tier++)
                {
                    //The draws in the order they always were: the share, then the next tier's two sides
                    float share = tiers == 2 ? 0.5f + 0.22f * (float)_shape.NextDouble()
                        : tier == 0 ? 0.42f + 0.14f * (float)_shape.NextDouble() : 0.5f + 0.2f * (float)_shape.NextDouble();
                    float nextX = tierX * (0.66f + 0.16f * (float)_shape.NextDouble());
                    float nextZ = tierZ * (0.66f + 0.16f * (float)_shape.NextDouble());

                    //⚠ A tier narrower than TIER_MIN_SIDE is not raised: this one goes to the top instead. A slab's or a
                    //podium's tower shrinks twice more as it steps back, and three tiers on a small plot ended 1.4 wide, a
                    //needle whose roof the dressing split into cells of negative size (its pieces mirrored)
                    bool last = tier == tiers - 1 || MathF.Min(nextX, nextZ) < TIER_MIN_SIDE;
                    float tierTop = last ? top : bottom + (top - bottom) * share;
                    if (tier == 0) firstTop = tierTop;

                    AddBox(centre, tierX, tierZ, bottom, tierTop);
                    if (last) break;

                    bottom = tierTop;
                    centre += new Vector2(lean.X * MathF.Max(0f, (tierX - nextX) * 0.5f - TIER_STEP_MIN),
                        lean.Y * MathF.Max(0f, (tierZ - nextZ) * 0.5f - TIER_STEP_MIN));
                    tierX = nextX;
                    tierZ = nextZ;
                }
                return firstTop;
            }

            //One box from bottom to top over the rectangle centred on `centre`
            public void AddBox(Vector2 centre, float sizeX, float sizeZ, float bottom, float top)
            {
                Vector3 middle = new(centre.X, (bottom + top) * 0.5f, centre.Y);

                //Scale then translate: no rotation, so the box stays axis-aligned and its normals survive the
                //non-uniform scale
                _boxes.Add(new ModelInstance(Matrix.CreateScale(sizeX, top - bottom, sizeZ) * Matrix.CreateTranslation(middle),
                    new Vector4(_kind, 0f, _anchor.X, _anchor.Y)));

                //CORNICES (#678's third step). A stone building ends in a moulded cornice, and a box ended in a knife edge:
                //the one silhouette the play camera sees of every tower round the arena is its top against the sky. Only
                //the towers of the classic punched windows, and every box of them, the way an Art Deco setback is finished
                //at each step. A slab wider than the box: it has no windows of its own because it is shorter than two wall
                //margins, so it draws as stone in the tower's own tone (it carries the tower's roll), moulded by City.fxh
                //(the 1 in y marks it), and its underside throws the sun's shadow down the wall.
                if (_kind < CLASSIC_FACADE_SHARE)
                {
                    Vector3 cornice = new(middle.X, top + CORNICE_OVER_ROOF - CORNICE_HEIGHT * 0.5f, middle.Z);
                    _crowned.Add(_boxes.Count - 1);
                    _cornices.Add(new ModelInstance(
                        Matrix.CreateScale(sizeX + 2f * CORNICE_OUT, CORNICE_HEIGHT, sizeZ + 2f * CORNICE_OUT)
                            * Matrix.CreateTranslation(cornice), new Vector4(_kind, 1f, _anchor.X, _anchor.Y)));
                }
            }
        }

        //THE FAR SKYLINE (#774): rows of slabs round the grid past its last block, so a street's view ends in a city and
        //not in the sky. The owner: "behind the city we see 'nothing' - through the streets the end of the rendering is
        //visible". Two rows a side, each cut into slabs of its own widths, the first with thin gaps between them; the
        //second stands behind it shoulder to shoulder and rises higher, the skyline the
        //references (774-klein, 774-zimage) close the view with. Plain boxes, drawn plainly (see FarSkyline). Off a
        //generator of their own, so the city inside is untouched.
        private const int SKYLINE_SEED = 774;
        private const float SKYLINE_GAP = 1.2f;             //from the grid's last block to the first row, in block pitches
        private const float SKYLINE_ROW_DEPTH = 1.6f;       //a row's depth, in block pitches
        //Heights as shares of the grid's own roofline at its last block (the taper's end), the first row's and how much
        //higher the back row stands: about level with the last blocks, so the skyline continues the city's own and does
        //not stand over the horizon as a wall of blocks (the first cut, at 70 to 216 over the street, did)
        private const float SKYLINE_MIN_SHARE = 0.7f, SKYLINE_MAX_SHARE = 1.0f;
        private const float SKYLINE_BACK_RISE = 0.08f;

        private static void AddFarSkyline(List<ModelInstance> slabs, CityLayout layout)
        {
            Random random = new(SKYLINE_SEED);
            float edgeHeight = MathF.Max(layout.RooflineY - layout.RadiusBlocks * layout.TaperPerBlock - layout.BaseY, 20f);
            float inner = (layout.RadiusBlocks + 0.5f + SKYLINE_GAP) * layout.BlockPitch;
            float depth = SKYLINE_ROW_DEPTH * layout.BlockPitch;

            for (int row = 0; row < 2; row++)
            {
                float near = inner + row * (depth + layout.StreetWidth);
                float reach = near + depth;
                for (int side = 0; side < 4; side++)
                {
                    //Along the side from one corner to the other, the corners included, in slabs of their own widths
                    for (float along = -reach; along < reach;)
                    {
                        float width = layout.BlockPitch * (0.6f + 1.3f * (float)random.NextDouble());
                        float slabDepth = depth * (0.6f + 0.4f * (float)random.NextDouble());
                        float height = edgeHeight * (SKYLINE_MIN_SHARE + (SKYLINE_MAX_SHARE - SKYLINE_MIN_SHARE) * (float)random.NextDouble()
                            + row * SKYLINE_BACK_RISE);
                        //A taller one now and then, the skyline's landmarks
                        if (random.NextDouble() < 0.1) height *= 1.2f;
                        //Gaps in the first row only: the back row stands shoulder to shoulder behind them, so a sight
                        //line through a gap ends on a wall (a gap in each row let a slit of the horizon through where two lined up)
                        float gap = row == 0 ? 1.5f + 3f * (float)random.NextDouble() : 0f;
                        float middle = along + width * 0.5f;
                        float outward = near + slabDepth * 0.5f;

                        Vector3 centre = side switch
                        {
                            0 => new Vector3(middle, 0f, outward),
                            1 => new Vector3(middle, 0f, -outward),
                            2 => new Vector3(outward, 0f, middle),
                            _ => new Vector3(-outward, 0f, middle),
                        };
                        Vector3 size = side < 2 ? new Vector3(width, height, slabDepth) : new Vector3(slabDepth, height, width);
                        centre.Y = layout.BaseY + height * 0.5f;

                        float kind = FacadeRoll(centre.X, centre.Z);
                        slabs.Add(new ModelInstance(Matrix.CreateScale(size) * Matrix.CreateTranslation(centre),
                            new Vector4(kind, 0f, centre.X, centre.Z)));
                        along += width + gap;
                    }
                }
            }
        }

        /// <summary>
        /// The facade-kind roll for the tower standing on this centre, 0..1 (#678): the centre at 0.37 cells a unit,
        /// floored, through a murmur-style integer finaliser with the kind's salt. An integer hash and not a
        /// <c>Hash21</c>, whose exact 50 × 100-cell period (#674) would lay the kinds out in the same arrangement six times
        /// across the city; off the centre rather than the layout's generator, so the city itself is untouched.
        /// </summary>
        private static float FacadeRoll(float centreX, float centreZ)
        {
            unchecked
            {
                uint qx = (uint)(int)MathF.Floor(centreX * 0.37f);
                uint qz = (uint)(int)MathF.Floor(centreZ * 0.37f);
                uint h = qx * 0x8da6b343u ^ qz * 0xd8163841u ^ FACADE_SALT * 0xcb1ab31fu;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
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

    /// <summary>A tower's plan (#1066): what its footprint was made of. See <c>City</c>'s <c>PLAN_SALT</c>.</summary>
    internal enum CityPlan
    {
        Box,
        Slab,
        Ell,
        Podium,
        Twin,
    }

    /// <summary>
    /// One built plot (#1066): its centre (the tower's anchor), the side of its square, the top its tower reaches, its plan,
    /// and the run of <see cref="City.Buildings"/> its tower's boxes take (cornices not included).
    /// </summary>
    internal readonly record struct CityPlot(Vector2 Anchor, float Side, float Top, CityPlan Plan, int FirstBox, int BoxCount);
}
