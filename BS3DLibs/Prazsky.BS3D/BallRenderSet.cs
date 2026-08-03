using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.BS3D
{
    /// <summary>
    /// Everything it takes to draw balls: the three procedural sphere LODs, the three
    /// <see cref="InstancedModelRenderer"/>s built on them with every figure of the balls' look, the
    /// (type × LOD) instance buckets each of which becomes exactly one <c>DrawInstancedPrimitives</c> call, and
    /// the one walk that issues them. It stood in all three executables — the Testbed's and the Game's
    /// <c>LoadContent</c>, the map editor's, with the bucket bookkeeping written out a fourth time inside
    /// <c>Testbed.cs</c> alone — until #76.
    /// <para>
    /// <b>The invariant this type exists to protect: every ball is visited exactly once per frame.</b> The
    /// occlusion ease, the attach glide and the ripple all advance state that lives on the ball itself
    /// (<c>Prazsky.BS3D.Physics.PhysicsBall</c>), so a second visit advances them twice — the shading eases at
    /// double speed, a landing ball's glide collapses in half the time, and the ripple runs through the cluster
    /// too fast — while a missed visit freezes them. Neither fails loudly. So the walk itself is not the
    /// caller's to write: <see cref="BeginFrame"/> is the only way to obtain a <see cref="BallDrawFrame"/>, and
    /// it resets the buckets on the way; the walk over a hanging cluster is
    /// <c>Prazsky.BS3D.Physics.ClusterCollector.Collect</c>'s and the walk over a static map is
    /// <see cref="BallDrawFrame.AddMap"/>'s. <see cref="BeginFrame"/> also refuses to open a second frame
    /// before <see cref="Draw"/> has closed the first, which is exactly the shape of the double-advance bug.
    /// </para>
    /// <para>
    /// <b>And the ambient-occlusion direction cannot be handed over undivided, because there is no code path
    /// that produces one.</b> <see cref="OcclusionTarget"/> is the only way to build the vector the shader
    /// reads, and it divides. docs/game-session.md: "The ambient-occlusion direction is a sum of unit vectors
    /// and <b>must</b> be divided by <c>MAX_BALL_OCCLUDERS</c> before the shader sees it. Handed over raw it is
    /// up to twelve times too long, the shader's dot against it saturates over most of the ball, and every
    /// surface ball wears a hard-edged black crescent instead of the soft inward shading that makes the cluster
    /// read as one body rather than a heap of spheres. The Testbed divides […]; the game did not, and it cost
    /// the cluster its whole look."
    /// </para>
    /// <para>
    /// <b>A caller with no physics is a first-class caller.</b> The map editor has no bodies, no pulse clock of
    /// the balls' own and no ripple: it hands its <see cref="BallsMap"/> to
    /// <see cref="BallDrawFrame.AddMap"/>, passes its own scene clock to <see cref="Draw"/>, and constructs
    /// this with <c>ripples: false</c>. Nothing about the look is conditional on having a simulation — which is
    /// the point, since the whole reason that editor draws balls at all is that a map should look there the way
    /// it will play.
    /// </para>
    /// <para>
    /// <b>What deliberately stayed with the callers.</b> The <i>magazine</i>: the loaded queue goes through
    /// <see cref="BallDrawFrame.Add"/> like every other ball, but which colours are loaded, where the bore puts
    /// them (<see cref="BorePose"/>) and the Game's transmute cross-fade are three different questions and none
    /// of them is this type's. <i>Sky-lit enrolment</i>: <see cref="Renderers"/> is exposed for it and nothing
    /// else, for the reason <see cref="SkyLightRig"/> gives at length — which renderers take part is each
    /// executable's own list with its own reasons. And <i>where in the frame</i> the balls are drawn, which is
    /// load-bearing in all three (over the opaque scene, under the trails and the glass) and is why
    /// <see cref="Draw"/> is a separate call from the collection rather than folded into it.
    /// </para>
    /// </summary>
    public sealed class BallRenderSet : IDisposable
    {
        #region The eight types and the three LODs

        /// <summary>
        /// How many ball colours there are: red/green/blue/white plus cyan/magenta/yellow/black. It sizes the
        /// instance buckets, bounds the draw walk, and is what a caller counting balls per colour wants
        /// (the Game's live census). <c>const</c>, because an enum cast is a constant expression and callers
        /// size arrays with it.
        /// </summary>
        public const int TYPE_COUNT = (int)BallType.Type8;

        /// <summary>
        /// The drawn radius. The same <see cref="Constants.HALF"/> that
        /// <c>Prazsky.BS3D.Physics.BallsConstraintsBuilder.BALL_RADIUS</c> is, from the same place, so the
        /// sphere that is drawn and the sphere that collides cannot disagree — the map editor used to spell this
        /// out with a comment saying as much, having no physics assembly to ask.
        /// </summary>
        public const float BALL_RADIUS = Constants.HALF;

        //Procedurally generated sphere LODs, finest first: {slices, stacks}. Per-pixel lighting shades even the
        //coarse levels smoothly, so only the silhouette reveals the polygons - which is why the coarsest is as
        //coarse as it is.
        private static readonly int[,] LOD_RESOLUTIONS = { { 32, 24 }, { 16, 12 }, { 10, 7 } };

        //The camera distance up to which each level is used; the last level covers everything beyond the last
        //distance, so there is one fewer of these than there are levels.
        private static readonly float[] LOD_DISTANCES = { 15f, 30f };

        //Squared once at class load, because the pick compares against Vector3.DistanceSquared: monotone in the
        //distance, so it picks the same level, and it saves a square root per ball per frame (3000 of them on
        //the stress map). Derived from LOD_DISTANCES rather than written out again - two arrays of the same
        //figures is precisely the drift this file exists to end.
        private static readonly float[] LOD_DISTANCES_SQUARED = SquareEach(LOD_DISTANCES);

        /// <summary>How many mesh resolutions there are. Derived from <c>LOD_RESOLUTIONS</c>, so adding a row
        /// to that table is the whole of adding a level (and one more entry to the distances).</summary>
        public static readonly int LodCount = LOD_RESOLUTIONS.GetLength(0);

        #endregion

        #region The look

        /// <summary>
        /// Diffuse reflectance of the ball material, multiplying both pattern colours. It used to be a flat 1 —
        /// a surface that reflects every photon that reaches it, which nothing real does; white vinyl manages
        /// about this. In gamma space dropping it muted the colours, which is why it was raised in the first
        /// place, but that was the wrong composition talking: in linear light this scales radiance evenly and
        /// takes the glare off without touching the hue or the saturation. The one number to nudge if the balls
        /// want to be darker or lighter still.
        /// <para>
        /// The map editor was drawing at a flat 1 — twice the game's — while its own section comment said the
        /// balls are drawn exactly as the game draws them. That was drift, and hoisting ends it.
        /// </para>
        /// </summary>
        private const float ALBEDO = 0.5f;

        //Beach-ball pattern (concept art in issue #43): five gores in the type colour, each narrower than the
        //three the ball started out with, separated by narrower white ones, plus a white polar disc.
        private const int PATTERN_GORES = 5;

        /// <summary>
        /// How much of its own colour a ball radiates. Kept well under 1: the ball should read as lit from
        /// within, not as a lamp — past about a third it stops looking like a glowing object and starts looking
        /// like an unlit one with the brightness turned up, because the shading that gives it its shape gets
        /// drowned out.
        /// </summary>
        private const float EMISSION = 0.5f;

        /// <summary>How much light passes through the shell from a source behind the ball.</summary>
        private const float TRANSLUCENCY = 0.35f;

        /// <summary>
        /// A resting human heart, near enough. Slow on purpose — a fast pulse reads as an alarm rather than as
        /// something alive and calm.
        /// </summary>
        private const float PULSE_BEATS_PER_SECOND = 1.1f;

        /// <summary>
        /// World units one beat spans as it travels up the cluster. Comparable to the height of a full map, so
        /// a beat is visibly a wave crossing the structure rather than a uniform flash.
        /// </summary>
        private const float PULSE_WAVELENGTH = 14f;

        //Up the cluster, and the beat's phase is offset by position along it, which is what makes it read as a
        //wave passing through rather than as every ball flashing together.
        private static readonly Vector3 PULSE_DIRECTION = Vector3.Up;

        /// <summary>
        /// How deep the resting heartbeat swings where the cluster has nothing else to say with its own light.
        /// </summary>
        private const float PULSE_DEPTH_RESTING = 0.55f;

        /// <summary>
        /// How deep it swings where the ripple runs. Turned down from <see cref="PULSE_DEPTH_RESTING"/> once
        /// the ripple arrived: the cluster then has <i>two</i> things to say with its own light, and a breath
        /// that swings over half the emission drowns out the wave that runs through it on every landing. The
        /// breath is the idle state and should read as one — alive, but waiting — and the ripple is the event.
        /// <para>
        /// It is chosen by the <c>ripples</c> constructor flag rather than passed in, so the two cannot be got
        /// half-right: nobody can switch the wave on and leave the breath loud enough to hide it. The Testbed
        /// and the map editor have no ripple and keep the resting depth, which is what they drew before.
        /// </para>
        /// </summary>
        private const float PULSE_DEPTH_RIPPLING = 0.38f;

        /// <summary>
        /// How hard a ball flares as the ripple reaches it, as a multiple of its own colour at full peak. Over
        /// <c>GLARE_THRESHOLD</c> on purpose, so a lit ball blooms — that is what makes the wave read as light
        /// travelling through the cluster rather than as the balls changing shade.
        /// <para>
        /// <b>But only just over</b>, and that is the whole tuning problem here. The number of balls the wave
        /// has lit at once grows as the square of how far it has got, so a few hops in it is not one bright ball
        /// but a hundred of them — and at 1.1 that flooded the glare's bright pass, whose six streak arms then
        /// overlapped into a wash and, added before the ACES curve, blew the entire frame white for a frame. It
        /// measured as a single sample at 174 mean brightness against a 137 baseline, and it looked like a
        /// rendering fault rather than an effect.
        /// </para>
        /// </summary>
        private const float RIPPLE_STRENGTH = 0.85f;

        #endregion

        #region Neighbour-based ambient occlusion (issue #40)

        /// <summary>
        /// The most occluders a ball can have: the 12 touching neighbour cells — 4 on its own level and up to 4
        /// on each adjacent one. It is the <i>divisor</i> that makes the occlusion direction a direction and the
        /// occluder count a fraction, and <see cref="BallsMap.CountOccupiedNeighbors"/> examines exactly these
        /// twelve cells and no more, so the count it returns cannot exceed this.
        /// <para>
        /// That is why the count is <b>not</b> clamped here. The Game clamped it (<c>Math.Min(occluders,
        /// MAX_BALL_OCCLUDERS)</c>) and the Testbed and the map editor did not; the clamp can never bind, and a
        /// clamp that can never bind reads as a warning that the counter is not to be trusted — which invites
        /// the next reader to adjust the divisor instead. Both facts live in one place now: the counter's
        /// contract, and this figure.
        /// </para>
        /// </summary>
        public const int MAX_OCCLUDERS = 12;

        /// <summary>
        /// How dark a fully surrounded ball gets: its lighting is scaled down by up to this fraction.
        /// </summary>
        private const float OCCLUSION_STRENGTH = 0.55f;

        /// <summary>
        /// What a ball with nothing packed around it carries — a shot in flight, a released ball on its way
        /// down, a ball loaded in the barrel. The same value as
        /// <c>Prazsky.BS3D.Physics.PhysicsBall.UNOCCLUDED</c>, which is its twin in the physics assembly's
        /// vector space; there is nothing to keep in step because there is nothing in it but zeroes and a one.
        /// </summary>
        public static readonly Vector4 UNOCCLUDED = new(0f, 0f, 0f, 1f);

        /// <summary>
        /// The occlusion a ball's surroundings call for: XYZ the direction its occluders lie in, W how much
        /// light reaches it at all. <b>The one place either is derived</b>, and the reason it is a method rather
        /// than four lines at each of three call sites is the trap in the class remarks — the direction arrives
        /// as a <i>sum</i> of unit vectors, one per occupied neighbour, and the shader wants a direction with a
        /// weight. Nothing else can build this vector, so nothing else can forget to divide it.
        /// </summary>
        /// <param name="occluders">Occupied neighbouring cells, straight from
        /// <see cref="BallsMap.CountOccupiedNeighbors"/>. At most <see cref="MAX_OCCLUDERS"/> by that method's
        /// construction.</param>
        /// <param name="occluderDirectionSum">The sum of the unit vectors pointing at them, from the same call.
        /// Undivided — dividing it is this method's whole job.</param>
        public static Vector4 OcclusionTarget(int occluders, Vector3 occluderDirectionSum) =>
            new(occluderDirectionSum / MAX_OCCLUDERS, 1f - OCCLUSION_STRENGTH * occluders / MAX_OCCLUDERS);

        #endregion

        #region The instance buckets

        //One bucket per (type, LOD) pair, each becoming a single instanced draw call. Allocated lazily - most
        //frames touch a handful of the twenty-four, and a map of one colour touches three - and doubled when a
        //bucket fills, so the arrays settle at whatever the scene actually needs within the first few frames
        //and nothing is allocated per frame after that. 256 is a couple of levels of a full map: big enough
        //that the common case never grows, small enough that twenty-four of them cost nothing.
        private const int BUCKET_INITIAL_CAPACITY = 256;

        private readonly ModelInstance[][] _buckets;
        private readonly int[] _counts;
        private readonly int[] _lodTotals;

        #endregion

        private SphereMesh[] _meshes;
        private InstancedModelRenderer[] _renderers;

        //The camera BeginFrame was given, and by being non-null it is also "a frame is open". One field for
        //both, deliberately: it makes the two guards below the same guard. The LODs were picked against this
        //camera's position, so it is also the camera the buckets must be drawn with.
        private ICamera _frameCamera;

        /// <param name="instancingEffect">The shared <c>InstancedModel.fx</c>. Handed in and never disposed
        /// here: the content manager owns its lifetime and the city, the island, the barrel and the ceiling all
        /// draw through the same copy, so disposing it would take the whole scene down with the balls.</param>
        /// <param name="ripples">Whether this caller runs the landing ripple through its cluster
        /// (<c>Prazsky.BS3D.Physics.ClusterCollector</c>'s ripple hook, and the Game's alone today). It sets
        /// <see cref="InstancedModelRenderer.RippleStrength"/> — zero switches the shader's whole term off on a
        /// branch over the uniform, so a caller that never ripples pays nothing — and it picks the pulse depth,
        /// for the reason <see cref="PULSE_DEPTH_RIPPLING"/> gives.</param>
        /// <param name="groundHeight">Y the ground-contact half of the ambient occlusion is measured against;
        /// the balls' bellies darken as they approach it, the same way the barrel's underside does. Defaults to
        /// the island every ball in this game hangs over. The shader's ground term reaches 2 units up, i.e. to
        /// y = −6.5, and a map's own grid sits at y ≥ 0, so this is a no-op for a caller drawing a bare map with
        /// no island under it (the map editor) rather than something it has to opt out of.</param>
        public BallRenderSet(GraphicsDevice graphicsDevice, Effect instancingEffect, bool ripples = false,
            float groundHeight = ArenaIsland.TOP_Y)
        {
            _meshes = new SphereMesh[LodCount];
            _renderers = new InstancedModelRenderer[LodCount];

            for (int lod = 0; lod < LodCount; lod++)
            {
                _meshes[lod] = new SphereMesh(graphicsDevice, BALL_RADIUS, LOD_RESOLUTIONS[lod, 0], LOD_RESOLUTIONS[lod, 1]);

                //The balls are alive: they radiate their own colour on a heartbeat and pass light through their
                //shell. The beat runs up the cluster rather than firing everywhere at once.
                _renderers[lod] = new InstancedModelRenderer(graphicsDevice, _meshes[lod], ALBEDO * Vector3.One, instancingEffect)
                {
                    PatternGoreCount = PATTERN_GORES,
                    EmissiveStrength = EMISSION,
                    TranslucencyStrength = TRANSLUCENCY,
                    PulseSpeed = PULSE_BEATS_PER_SECOND,
                    PulseDepth = ripples ? PULSE_DEPTH_RIPPLING : PULSE_DEPTH_RESTING,
                    PulseDirection = PULSE_DIRECTION,
                    PulseWavelength = PULSE_WAVELENGTH,
                    RippleStrength = ripples ? RIPPLE_STRENGTH : 0f,
                    GroundHeight = groundHeight
                };
            }

            _buckets = new ModelInstance[TYPE_COUNT * LodCount][];
            _counts = new int[TYPE_COUNT * LodCount];
            _lodTotals = new int[LodCount];
        }

        /// <summary>
        /// The renderers, exposed for the one thing this type cannot do for a caller: enrolling them in that
        /// executable's sky light rig. Nothing else needs them.
        /// <para>
        /// The array itself, not a read-only view, and that is measured rather than careless: the Testbed
        /// rebuilds its enrolment list <i>every frame</i> (the overcast lerp re-applies the rig), and
        /// <c>foreach</c> over an array allocates no enumerator while <c>foreach</c> over an
        /// <see cref="IReadOnlyList{T}"/> boxes one per frame.
        /// </para>
        /// </summary>
        public InstancedModelRenderer[] Renderers => _renderers;

        /// <summary>
        /// Instances put out by the last <see cref="Draw"/> — bucket contents, the magazine preview included.
        /// There is no frustum culling (measured as saving nothing on this scene), so this differs from the
        /// number of bodies collected by the magazine balls and not by any cull.
        /// </summary>
        public int DrawnCount { get; private set; }

        /// <summary>
        /// Instances drawn per LOD level, for the Testbed's <c>autoshoot</c> log — the one reading that says
        /// whether the LOD ladder is doing anything at the distance the camera actually stands. Zeroed by
        /// <see cref="BeginFrame"/> and accumulated by <see cref="Draw"/>.
        /// <para>
        /// A read-only view here, unlike <see cref="Renderers"/>: this is read once a second by a diagnostic,
        /// so an enumerator costs nothing, and being unwritable is worth more.
        /// </para>
        /// </summary>
        public IReadOnlyList<int> LodTotals => _lodTotals;

        /// <summary>
        /// Opens this frame's collection: empties every bucket and hands back the only thing that can fill
        /// them. Call it once per frame, before anything is added, and see the class remarks for why that
        /// "once" is the whole point of the type.
        /// <para>
        /// It <b>throws</b> if the previous frame was collected and never drawn, which is the exact shape of
        /// the double-advance bug: a second collection in one frame runs the occlusion ease, the attach glide
        /// and the ripple twice over every ball while leaving the buckets looking perfectly correct.
        /// </para>
        /// </summary>
        /// <param name="camera">This frame's camera. Each ball's LOD is picked by its distance from here, so
        /// this is also the camera <see cref="Draw"/> puts the buckets out with — it is remembered rather than
        /// asked for twice, so the mesh a ball was bucketed for and the view it is drawn under cannot
        /// disagree.</param>
        public BallDrawFrame BeginFrame(ICamera camera)
        {
            if (_frameCamera != null) throw new InvalidOperationException(
                "The ball instances were collected twice in one frame: every ball would advance its occlusion " +
                "ease, its attach glide and its ripple twice while the buckets still looked correct. Collect " +
                "once, then Draw.");

            _frameCamera = camera ?? throw new ArgumentNullException(nameof(camera));

            for (int i = 0; i < _counts.Length; i++) _counts[i] = 0;
            for (int lod = 0; lod < _lodTotals.Length; lod++) _lodTotals[lod] = 0;

            return new BallDrawFrame(this, camera.Position);
        }

        /// <summary>
        /// Puts out everything collected this frame: one instanced draw call per ball type and LOD level, with
        /// that type's material and diffuse tint. Closes the frame, so the next
        /// <see cref="BeginFrame"/> is legal again.
        /// <para>
        /// <b>Where</b> in the frame this is called is the caller's, and load-bearing in all three: over the
        /// opaque scene so the cluster and the gun are in the depth buffer, before the shots' additive smears
        /// and before any glass composites over them.
        /// </para>
        /// </summary>
        /// <param name="wallClockSeconds">Seconds of <b>wall clock</b>, driving the heartbeat. Not the
        /// simulation's step: the balls keep their pulse while the simulation is paused or slowed, because it is
        /// what they are and not something they are doing. Taken as a parameter rather than left as a property
        /// so a caller cannot end up with balls that do not breathe by never having set it.</param>
        public void Draw(float wallClockSeconds)
        {
            if (_frameCamera == null) throw new InvalidOperationException(
                "BeginFrame must open the frame before the ball instances are drawn; without it the buckets " +
                "still hold the frame before this one.");

            ICamera camera = _frameCamera;
            _frameCamera = null;

            DrawnCount = 0;

            for (int lod = 0; lod < LodCount; lod++) _renderers[lod].PulseTime = wallClockSeconds;

            for (int typeIndex = 0; typeIndex < TYPE_COUNT; typeIndex++)
                for (int lod = 0; lod < LodCount; lod++)
                {
                    int count = _counts[typeIndex * LodCount + lod];
                    if (count == 0) continue;

                    DrawnCount += count;
                    _lodTotals[lod] += count;

                    BallType type = (BallType)(typeIndex + 1);

                    _renderers[lod].Draw(camera, _buckets[typeIndex * LodCount + lod], count,
                        BasicEffectParamsProvider.GetEffectByType(type),
                        BasicEffectParamsProvider.GetDiffuseTintByType(type));
                }
        }

        /// <summary>
        /// Releases the three sphere meshes and the three renderers' instance buffers. <b>Not</b> the effect,
        /// which the content manager owns and everything else in the scene shares.
        /// </summary>
        public void Dispose()
        {
            if (_meshes != null) foreach (SphereMesh mesh in _meshes) mesh?.Dispose();
            if (_renderers != null) foreach (InstancedModelRenderer renderer in _renderers) renderer?.Dispose();

            _meshes = null;
            _renderers = null;
        }

        //Mesh resolution by distance from the camera: the first level whose reach covers this ball, and the
        //coarsest for everything past the last reach. A linear scan over two thresholds, which beats anything
        //cleverer at this length.
        internal static int LodFor(float distanceSquared)
        {
            int lod = 0;
            while (lod < LOD_DISTANCES_SQUARED.Length && distanceSquared > LOD_DISTANCES_SQUARED[lod]) lod++;

            return lod;
        }

        //The one write into the buckets, which stood four times over before #76. Lazy on first use and doubling
        //when full, so nothing is allocated per frame once a scene has settled.
        internal void Store(int typeIndex, int lod, in ModelInstance instance)
        {
            int bucketIndex = typeIndex * LodCount + lod;
            ModelInstance[] bucket = _buckets[bucketIndex];
            int count = _counts[bucketIndex];

            if (bucket == null)
            {
                bucket = new ModelInstance[BUCKET_INITIAL_CAPACITY];
                _buckets[bucketIndex] = bucket;
            }
            else if (count == bucket.Length)
            {
                Array.Resize(ref bucket, bucket.Length * 2);
                _buckets[bucketIndex] = bucket;
            }

            bucket[count] = instance;
            _counts[bucketIndex] = count + 1;
        }

        private static float[] SquareEach(float[] values)
        {
            float[] squares = new float[values.Length];
            for (int i = 0; i < values.Length; i++) squares[i] = values[i] * values[i];

            return squares;
        }
    }

    /// <summary>
    /// One frame's ball collection: the only way to put an instance into a <see cref="BallRenderSet"/>'s
    /// buckets, and obtainable only from <see cref="BallRenderSet.BeginFrame"/>, which empties them first. That
    /// is the whole of its design — a caller cannot add without having reset, and cannot reset twice without
    /// being told (see the remarks there on the double advance).
    /// <para>
    /// A <c>readonly ref struct</c>, so it allocates nothing, cannot be stashed in a field and cannot outlive
    /// the frame it belongs to: a frame kept from last time would bucket this frame's balls against last
    /// frame's camera. Pass it along as <c>in</c> where a collection spans several methods — the Testbed's and
    /// the Game's magazine passes do — rather than reopening a frame.
    /// </para>
    /// </summary>
    public readonly ref struct BallDrawFrame
    {
        private readonly BallRenderSet _set;

        //Read once per frame rather than per ball: the LOD pick is the only thing that wants it, and it wants
        //it 3000 times on the stress map.
        private readonly Vector3 _eye;

        internal BallDrawFrame(BallRenderSet set, Vector3 eye)
        {
            _set = set;
            _eye = eye;
        }

        /// <summary>
        /// Buckets one ball whose world matrix the caller already holds — the loaded queue, whose placement
        /// comes off <see cref="BorePose.SlotWorld"/> and carries the barrel's own basis.
        /// <para>
        /// A colour outside the eight types is dropped in silence, which is deliberate and is the check every
        /// collector in this project has always made: a zero <see cref="BallType"/> is not a colour (the enum
        /// starts at 1, and an empty map cell is a null ball rather than a type 0), and there is nothing useful
        /// to draw for one.
        /// </para>
        /// </summary>
        /// <param name="position">Where the ball is, for the LOD pick. The matrix carries it too, but reading a
        /// translation back out of a matrix to measure a distance is the sort of thing that goes wrong once
        /// somebody scales one.</param>
        /// <param name="dissolve">Zero for everything not mid-transition — see
        /// <see cref="ModelInstance.Dissolve"/>.</param>
        /// <param name="ripple">Zero for everything not flaring — see <see cref="ModelInstance.Ripple"/>.</param>
        public void Add(BallType type, Vector3 position, in Matrix world, Vector4 occlusion, float dissolve = 0f,
            float ripple = 0f)
        {
            int typeIndex = (int)type - 1;
            if (typeIndex < 0 || typeIndex >= BallRenderSet.TYPE_COUNT) return;

            _set.Store(typeIndex, BallRenderSet.LodFor(Vector3.DistanceSquared(position, _eye)),
                new ModelInstance(world, occlusion, dissolve, ripple));
        }

        /// <summary>
        /// Buckets one ball from its position and its turn — a body's pose, which is the case that matters:
        /// this is the call the hanging cluster, the shots and the falling balls all come through, up to 3000
        /// times a frame.
        /// <para>
        /// The balls <i>turn</i>, which is what makes the beach-ball pattern readable, so the world matrix has
        /// to carry the orientation. <b>The translation is written into the matrix's fourth row rather than
        /// multiplied in</b>: <c>R × CreateTranslation(p)</c> is <i>exactly</i> <c>R</c> with that row set,
        /// because a rotation's fourth row is <c>(0,0,0,1)</c> — bit-exact, not an approximation — and a full
        /// 4×4 multiply here was the hottest needless work in the frame (BestPractices.md §6, measured on this
        /// very loop). It is done here, once, so no caller has to know the trick or be trusted to remember it.
        /// </para>
        /// </summary>
        public void AddOriented(BallType type, Vector3 position, in Quaternion orientation, Vector4 occlusion,
            float ripple = 0f)
        {
            int typeIndex = (int)type - 1;
            if (typeIndex < 0 || typeIndex >= BallRenderSet.TYPE_COUNT) return;

            Matrix world = Matrix.CreateFromQuaternion(orientation);

            world.M41 = position.X;
            world.M42 = position.Y;
            world.M43 = position.Z;

            _set.Store(typeIndex, BallRenderSet.LodFor(Vector3.DistanceSquared(position, _eye)),
                new ModelInstance(world, occlusion, 0f, ripple));
        }

        /// <summary>
        /// Buckets a whole static map, occlusion and all — the map editor's entire collection in one call, and
        /// the reason it needs no physics to draw a cluster that shades like the played one.
        /// <para>
        /// Every cell is visited exactly once, which here is a matter of correctness rather than of state: the
        /// occlusion is simply what the grid says it is, since a map on a bench has nothing to ease <i>towards</i>
        /// — no ball joins or leaves it while it is being looked at. The walk is in here all the same, so that
        /// the one loop that reads a ball array and the one function that divides the occlusion sum stay
        /// together.
        /// </para>
        /// <para>
        /// The balls are drawn unturned, because a map's balls have no orientation to draw: a translation
        /// <i>is</i> the whole transformation, and <c>Identity × T</c> is just <c>T</c> (BestPractices.md §6).
        /// </para>
        /// </summary>
        /// <param name="map">The map, or null before one is loaded — in which case nothing is added.</param>
        /// <returns>How many balls were bucketed.</returns>
        public int AddMap(BallsMap map)
        {
            if (map == null) return 0;

            StaticBall[,,] balls = map.GetStaticBallsArray();
            if (balls == null) return 0;

            XZLevel size = map.GetStaticBallsArraySize();
            int added = 0;

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        int occluders = BallsMap.CountOccupiedNeighbors(balls, new XZLevel(x, z, level), size,
                            out Vector3 occluderDirectionSum);

                        Add(ball.Type, ball.Position, Matrix.CreateTranslation(ball.Position),
                            BallRenderSet.OcclusionTarget(occluders, occluderDirectionSum));

                        added++;
                    }

            return added;
        }
    }
}
