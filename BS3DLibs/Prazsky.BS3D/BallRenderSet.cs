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
    /// Everything it takes to draw balls: the procedural sphere LODs and the
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
    /// reads, and it divides by <see cref="MAX_OCCLUDERS"/> inside itself. That direction arrives as a
    /// <i>sum</i> of unit vectors, one per occupied neighbour, so raw it is up to twelve times too long: the
    /// shader's dot against it then saturates over most of the ball and every surface ball wears a hard-edged
    /// black crescent instead of the soft inward shading that makes a cluster read as one body rather than a
    /// heap of spheres. The Game shipped without the division and it cost the cluster its whole look — the
    /// history is under "The ambient-occlusion direction" in docs/game-session.md. Paraphrased rather than
    /// quoted on purpose: a verbatim quotation of another file is one more thing that can drift out of step
    /// with it.
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
        #region The ball types and the LOD ladder

        /// <summary>
        /// How many ball colours there are: red/green/blue/white plus cyan/magenta/yellow/black, and since
        /// #152 orange/brown/silver/navy/olive. It sizes the instance buckets, bounds the draw walk, and is
        /// what a caller counting balls per colour wants (the Game's live census). The count itself is
        /// <see cref="BallTypes.Count"/>, derived from the enum — this used to be a hand-pinned
        /// <c>const (int)BallType.Type8</c>, and a member added without repointing it existed in logic and
        /// physics but was silently never drawn. Every consumer sizes, bounds or wraps at runtime, so
        /// nothing needed the compile-time constant.
        /// </summary>
        public static readonly int TYPE_COUNT = BallTypes.Count;

        /// <summary>
        /// The drawn radius. The same <see cref="Constants.HALF"/> that
        /// <c>Prazsky.BS3D.Physics.BallsConstraintsBuilder.BALL_RADIUS</c> is, from the same place, so the
        /// sphere that is drawn and the sphere that collides cannot disagree — the map editor used to spell this
        /// out with a comment saying as much, having no physics assembly to ask.
        /// </summary>
        public const float BALL_RADIUS = Constants.HALF;

        //Procedurally generated sphere LODs, finest first: {slices, stacks}. Per-pixel lighting shades even the
        //coarse levels smoothly, so only the SILHOUETTE reveals the polygons - which is the one thing the ladder
        //below is calibrated against, and the only thing worth calibrating it against.
        //
        //Stacks are half the slices throughout, which is the balanced ratio and not a saving made by eye: slices
        //divide 360 degrees around the equator while stacks divide 180 from pole to pole, so equal angular steps
        //want stacks = slices / 2. It matters because BOTH bound the silhouette - a sphere's outline is a full
        //circle whose left and right extremes fall on slice boundaries and whose top and bottom fall on stack
        //boundaries - so at this ratio one number (the slice count) describes the whole outline. The ladder this
        //replaced ran 4:3 ({32,24}, {16,12}, {10,7}), which spent a third more triangles on stacks that were
        //already finer than the slices they were paired with.
        private static readonly int[,] LOD_RESOLUTIONS = { { 64, 32 }, { 40, 20 }, { 24, 12 }, { 14, 7 } };

        /// <summary>
        /// How far a level's silhouette may fall short of a true circle before the next finer one is used, in
        /// <b>display</b> pixels. It is the whole of the ladder's calibration: every threshold below is derived
        /// from it and from the slice counts above, so there is no second table of distances to drift out of step
        /// with the meshes.
        /// <para>
        /// A polygonal outline sags off the circle it approximates by <c>r · (1 − cos(π / slices))</c> at the
        /// middle of each segment, which projects to <c>projectedRadius · (1 − cos(π / slices))</c> pixels
        /// whatever the distance and the lens — so a budget in pixels is the natural form, and the thresholds
        /// come out of inverting it.
        /// </para>
        /// <para>
        /// <b>Calibrated against the defect it fixes.</b> The ladder this replaced was keyed on raw camera
        /// distance with its coarsest level (10 slices) covering everything past 30 units, and the Game's play
        /// camera stands 33 out — so <i>every</i> ball in a played level was drawn at the coarsest mesh
        /// available. Measured on a 1616×939 window that is a projected radius of ~18 px and a sag of
        /// <b>0.88 px</b>, which is plainly faceted on screen: the outlines break into countable straight runs
        /// and the dark balls on the cluster's edge show corners against the sky. 0.35 is 2.5× inside that, with
        /// the margin deliberate — faceting is a <i>systematic</i> deviation the eye follows along an edge
        /// rather than isolated noise, so it is noticed well below the threshold where a single pixel of error
        /// would be.
        /// </para>
        /// </summary>
        private const float SILHOUETTE_BUDGET_PIXELS = 0.35f;

        /// <summary>How many mesh resolutions there are. Derived from <c>LOD_RESOLUTIONS</c>, so adding a row
        /// to that table is the whole of adding a level — the thresholds follow from it.</summary>
        public static readonly int LodCount = LOD_RESOLUTIONS.GetLength(0);

        /// <summary>
        /// The projected ball radius, in display pixels, at or above which each level is used — one fewer than
        /// there are levels, the coarsest covering everything smaller.
        /// <para>
        /// Entry <c>i</c> is the largest projected radius at which level <c>i + 1</c> is still inside
        /// <see cref="SILHOUETTE_BUDGET_PIXELS"/>: a ball is drawn at level <c>i</c> exactly when the next
        /// coarser level would no longer do. That is why this is derived and not written out — it is a statement
        /// about <c>LOD_RESOLUTIONS</c>, and a hand-written copy of it would go stale the first time a row of
        /// that table moved. With the table above it comes out at {113.5, 40.9, 14.0}.
        /// </para>
        /// </summary>
        private static readonly float[] LOD_MIN_PIXEL_RADIUS = AdequateUpTo(LOD_RESOLUTIONS);

        /// <summary>
        /// The largest projected radius, in display pixels, at which a sphere of this many slices still keeps
        /// its silhouette sag inside <see cref="SILHOUETTE_BUDGET_PIXELS"/>. Skips the finest level: nothing is
        /// ever chosen for being too big for level 0, which covers everything above the last threshold.
        /// </summary>
        private static float[] AdequateUpTo(int[,] resolutions)
        {
            float[] limits = new float[resolutions.GetLength(0) - 1];

            for (int lod = 0; lod < limits.Length; lod++)
            {
                //The NEXT level down is the one whose adequacy this threshold is about
                int slices = resolutions[lod + 1, 0];

                limits[lod] = SILHOUETTE_BUDGET_PIXELS / (1f - MathF.Cos(MathF.PI / slices));
            }

            return limits;
        }

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
        /// How wide one cell of the dissolve's dither is, in <b>display</b> pixels — the blocks a ball being
        /// re-coloured goes away in, and the ones the landing preview's ghost is cut into. The shader wants it in
        /// target pixels, which <see cref="DissolveCellTargetPixels"/> converts to.
        /// <para>
        /// A block of the screen and not a cube of the world, which is the whole point: cells in the ball's own
        /// object space turn with it and take its perspective, so they read as lumpy three-dimensional mottling
        /// of the surface rather than as pixelation of the picture, which is what the effect is saying.
        /// </para>
        /// <para>
        /// One display pixel — the literal floor, and it survives the resolve because every target sample
        /// inside one display pixel decides alike (that is what multiplying by the supersample factor buys).
        /// <para>
        /// <b>It was three, on the argument that one would read as a haze or a film grain</b> — as the ball
        /// being <i>faint</i>, which is the reading the dither was chosen over a transparency to avoid. Played
        /// rather than reasoned about, that came out the other way round: at three the blocks are coarse
        /// enough to read as a <i>mosaic laid over</i> the ball instead of as the ball resolving, and the
        /// author asked for a cell that matches a monitor pixel. The old prediction is kept here because it is
        /// the thing to re-test if the transition ever stops reading as pixels at all — it is a claim about
        /// how a display size looks, and the display is what decides it.
        /// </para>
        /// </summary>
        private const float DISSOLVE_DISPLAY_PIXELS = 1f;

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
        //frames touch a handful of the fifty-two, and a map of one colour touches four - and doubled when a
        //bucket fills, so the arrays settle at whatever the scene actually needs within the first few frames
        //and nothing is allocated per frame after that. 256 is a couple of levels of a full map: big enough
        //that the common case never grows, small enough that fifty-two of them cost nothing.
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

        //Held for the two things the frame has to ask the device rather than the caller: the BACK BUFFER's
        //height, which is the display resolution the silhouette budget is stated in, and the bound viewport's,
        //which during the scene pass is the supersampled target and so gives the dither its pixel size. Both are
        //read per frame rather than cached, so a resize, a fullscreen switch and a quality change all just work.
        private readonly GraphicsDevice _device;

        //This frame's LOD_MIN_PIXEL_RADIUS turned into squared camera distances - the form the per-ball pick
        //wants, since it compares against Vector3.DistanceSquared and a square root per ball per frame is 3000
        //of them on the stress map. Allocated once and refilled by BeginFrame: the projection changes every
        //frame (precise aim leans the lens in, the recoil punches the FOV), so these cannot be static, but
        //nothing about them may allocate either.
        private readonly float[] _lodDistanceSquared;

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
            _device = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

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
            _lodDistanceSquared = new float[LOD_MIN_PIXEL_RADIUS.Length];
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
        /// The flat colour the cluster flares in when a wave carries the ceiling's meaning rather than a
        /// landing's — red by default, which is what a descent forced on the player is. Set it before
        /// starting a wave that means something else; a tall level's glass steps down to <i>hand</i> the
        /// player more of its column, and that is not an alarm.
        /// <para>
        /// One value for the whole set, which is honest: a ball can only be in one wave at a time (the
        /// newest to reach it takes it over), so there is never a frame with two meanings in the cluster.
        /// </para>
        /// </summary>
        public Vector3 RippleAlarmColor
        {
            get => _renderers[0].RippleAlarmColor;
            set { foreach (InstancedModelRenderer renderer in _renderers) renderer.RippleAlarmColor = value; }
        }

        /// <summary>
        /// How many pixels of the scene's render target make one pixel of the finished picture — the caller's
        /// supersampling factor. It exists for exactly one thing: sizing the dissolve's dither cell, which is
        /// authored in <see cref="DISSOLVE_DISPLAY_PIXELS"/> of the <i>display</i> while the shader can only
        /// measure the target it is drawing into.
        /// <para>
        /// The same shape as <see cref="SceneRenderer.SupersampleFactor"/>, deliberately — the space scene sizes
        /// its stars in output pixels for the same reason and is told the same way, so this is one more consumer
        /// of a number the three executables already keep. Left unset it is 1, which is right for a caller that
        /// does not supersample and merely coarse for one that does; it cannot be wrong in the direction that
        /// breaks the effect, since only a cell <i>under</i> a display pixel is averaged away by the resolve.
        /// </para>
        /// <para>
        /// Asked of the caller rather than measured off the device, though the ratio of the bound viewport to the
        /// back buffer would give it: <see cref="BeginFrame"/> runs <b>before</b> the scene target is bound in the
        /// Game and <b>after</b> it in the Testbed and the map editor, so what a device read means here depends on
        /// which executable is asking — the same asymmetry that makes the LOD pick read the back buffer's size,
        /// which is true whatever is bound (see <see cref="SolveLodDistances"/>).
        /// </para>
        /// </summary>
        public int SupersampleFactor { get; set; } = 1;

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

            SolveLodDistances(camera);

            return new BallDrawFrame(this, camera.Position);
        }

        /// <summary>
        /// Turns this frame's <see cref="LOD_MIN_PIXEL_RADIUS"/> into the squared camera distances the per-ball
        /// pick compares against, so the pick itself stays the two-compare scan it has always been while the
        /// thresholds it scans are stated in pixels.
        /// </summary>
        /// <remarks>
        /// <b>Why the pick is on projected size and not on raw distance.</b> A distance ladder is a statement
        /// about one camera, and there are three cameras here — the Testbed's free lens that can sit on top of a
        /// ball, the map editor's, and the Game's play lens fixed a solved 33-odd units out. The old ladder was
        /// calibrated for the first and put every ball of a played level on the coarsest mesh in the third
        /// (see <see cref="SILHOUETTE_BUDGET_PIXELS"/>). Projected size is the thing the eye actually judges, and
        /// a threshold in pixels is true for every lens, every window and every display at once.
        /// <para>
        /// The scale comes out of the projection matrix rather than a field of view, and that is what makes it
        /// free and what makes it correct: <c>Projection.M22</c> is <c>1 / tan(fov / 2)</c> for every
        /// <see cref="Matrix.CreatePerspectiveFieldOfView"/>, and <c>RecoilCamera</c> rebuilds its projection
        /// each frame with precise aim's narrower lens and the recoil's FOV punch already folded in — so leaning
        /// in over the barrel raises the ball's projected size and sharpens its mesh with no wiring at all.
        /// A point at depth <c>d</c> and height <c>h</c> lands at NDC <c>h · M22 / d</c>, and NDC spans the
        /// viewport height over 2, so a ball's projected radius in pixels is
        /// <c>BALL_RADIUS · M22 · height / (2 · d)</c> — inverted here for the distance at which it equals a
        /// threshold.
        /// </para>
        /// <para>
        /// <b>The height is the back buffer's, not the render target's</b>, and that is the one subtle choice.
        /// The scene is drawn into a supersampled target and box-filtered down, so what the player sees is a
        /// display pixel — and a facet is a <i>systematic</i> deviation of the outline that averaging softens
        /// but does not remove, unlike the stair-stepping supersampling is there for. Budgeting in target pixels
        /// would therefore make the balls finer the moment supersampling was turned up, for no visible gain, and
        /// coarser on the weak machine that turned it off, where the outline is on show.
        /// </para>
        /// <para>
        /// It also happens to be the only reading that is <i>safe</i> here, and that is worth knowing before
        /// anyone reaches for <c>Viewport</c> instead. The back buffer's size is true whatever is bound; the
        /// viewport's is not, and the three executables do not agree about what is bound at this moment. The Game
        /// collects its balls <b>before</b> it binds the supersampled scene target, while the Testbed and the map
        /// editor bind it first and collect after — so a viewport read would mean the display in one and the
        /// supersampled target in the other two, and the same ladder would sit at different thresholds in each.
        /// </para>
        /// <para>
        /// Distance and not depth, deliberately: an off-axis ball is further from the eye than its depth, so its
        /// projected size is slightly <i>over</i>-estimated and it may take a finer mesh than it strictly needs.
        /// That is the harmless direction, and it costs one subtraction per ball instead of a view transform.
        /// </para>
        /// </remarks>
        private void SolveLodDistances(ICamera camera)
        {
            //Half the back buffer's height times the projection's vertical scale: pixels per world unit at unit
            //depth, which divided by a threshold in pixels is the distance at which a ball shrinks to it.
            float pixelScale = BALL_RADIUS * camera.Projection.M22
                * _device.PresentationParameters.BackBufferHeight * Constants.HALF;

            for (int lod = 0; lod < _lodDistanceSquared.Length; lod++)
            {
                float distance = pixelScale / LOD_MIN_PIXEL_RADIUS[lod];

                _lodDistanceSquared[lod] = distance * distance;
            }
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

            //The dither's cell, converted from the display pixels it is authored in to the target pixels the
            //shader can measure. Clamped at 1 so a caller that reported a nonsense factor still cuts on whole
            //target pixels rather than dividing the screen position by zero.
            float dissolvePixels = DISSOLVE_DISPLAY_PIXELS * Math.Max(1, SupersampleFactor);

            for (int lod = 0; lod < LodCount; lod++)
            {
                _renderers[lod].PulseTime = wallClockSeconds;
                _renderers[lod].DissolvePixelSize = dissolvePixels;
            }

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
        /// Releases every sphere mesh and every renderer's instance buffer. <b>Not</b> the effect,
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
        //coarsest for everything past the last reach. A linear scan over three thresholds, which beats anything
        //cleverer at this length. The reaches are this frame's, solved from the projected-size thresholds by
        //SolveLodDistances - which is why this is no longer static.
        internal int LodFor(float distanceSquared)
        {
            int lod = 0;
            while (lod < _lodDistanceSquared.Length && distanceSquared > _lodDistanceSquared[lod]) lod++;

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
        /// A colour outside the known types is dropped in silence, which is deliberate and is the check every
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

            _set.Store(typeIndex, _set.LodFor(Vector3.DistanceSquared(position, _eye)),
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

            _set.Store(typeIndex, _set.LodFor(Vector3.DistanceSquared(position, _eye)),
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
        public int AddMap(BallsMap map) => AddMap(map, Vector3.Zero);

        /// <summary>
        /// <see cref="AddMap(BallsMap)"/> hung at an offset: the grid frame a map stores its balls in is not
        /// the world frame a field is played in, and this is the whole of the difference. The menu's backdrop
        /// is the caller — it hands the very offset a session derives for the same map, so the preview hangs
        /// exactly where the map will hang when it is played.
        /// </summary>
        /// <param name="map">The map, or null before one is loaded — in which case nothing is added.</param>
        /// <param name="worldOffset">Added to every ball's grid-frame position, XZ and Y alike.</param>
        /// <returns>How many balls were bucketed.</returns>
        public int AddMap(BallsMap map, Vector3 worldOffset)
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

                        Vector3 position = ball.Position + worldOffset;
                        Add(ball.Type, position, Matrix.CreateTranslation(position),
                            BallRenderSet.OcclusionTarget(occluders, occluderDirectionSum));

                        added++;
                    }

            return added;
        }
    }
}
