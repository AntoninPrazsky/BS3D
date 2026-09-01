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
    /// <see cref="BallDrawFrame.Add"/> like every other ball — asking for <c>still</c>, which is the one thing
    /// about it this type does know (#252: a loaded round must not breathe, and the pulse is a per-renderer
    /// uniform, so that has to be a draw of its own rather than a value on the instance) — but which colours are
    /// loaded, where the bore puts them (<see cref="BorePose"/>) and the Game's transmute cross-fade are three
    /// different questions and none of them is this type's. <i>Sky-lit enrolment</i>: <see cref="Renderers"/> is exposed for it and nothing
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
        /// drowned out. Since #303 the <b>resting</b> part of this follows the occlusion in the shader (see
        /// <c>BallEmission</c> in InstancedModel.fx): added flat, it was a floor under the whole pile that no
        /// amount of AO could take the cluster below, and the single biggest reason burial did not read.
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

        #region The glass bubble (issue #258)

        /// <summary>
        /// Optical thickness of the bubble's film seen face-on, in whole waves of the reference wavelength —
        /// the pitch of the soap rainbow. Under about 1 the film sits in its first interference order and shows
        /// broad single-colour washes; over about 4 the fringes crowd into a fine oily marbling that a distant
        /// ball cannot resolve and that the shader's band-limit then has to throw away. Just over 2 is where a
        /// bubble the size of a played ball shows two or three clean bands across its rim, which is what reads
        /// as soap rather than as an oil slick.
        /// </summary>
        private const float BUBBLE_FILM_THICKNESS = 2.2f;

        /// <summary>
        /// How much of its type colour a bubble's film carries into the light it passes. Over 1 deliberately:
        /// real soap is very nearly colourless and this game cannot afford that — thirteen types have to stay
        /// apart at a glance across a whole cluster, and a bubble showing the sky faithfully would show it in
        /// every colour at once. It is the one figure to move if a colour stops being nameable, and the pair
        /// to check it against is <see cref="BUBBLE_EMISSION"/>, which carries the same hue where no sky
        /// reaches the ball at all.
        /// </summary>
        private const float BUBBLE_TINT = 3.2f;

        /// <summary>
        /// What a bubble's film hides where the eye meets it face-on, before its rim adds its own — <b>the one
        /// figure that decides whether a colour is nameable</b>, and the reason is arithmetic rather than taste.
        /// What a film does not hide is the backdrop, arriving <i>untinted</i>: the two walls together cover
        /// <c>1 − (1 − f/2)(1 − f)</c> of the pixel, so at the 0.26 this shipped with, <b>64 % of a bright sky
        /// stood behind every ball</b> and the campaign's opening block — a red pyramid over the meadow's blue
        /// sky — read pink. 0.84 left 9 %, and this leaves 5 %. The owner's word for the first version was that
        /// the balls were so transparent their colour could hardly be seen at all, and this is the number that
        /// was; the second was that they read as unnaturally see-through, which brought it here.
        /// <para>
        /// It does <b>not</b> cost the style, which is the thing to re-check before anyone lowers it again: a
        /// bubble reads as glass through its rim, its pinpoint, its iridescence, the second rim of its own far
        /// wall showing inside the first, and the fact that its brightness follows whatever is behind it — none
        /// of which this touches. Photographed at 0.26, 0.55, 0.62, 0.72, 0.84 and 0.90 on the meadow (the
        /// hardest case, the brightest sky in the campaign) and on space (the darkest).
        /// </para>
        /// <para>
        /// Raising it was <b>half</b> of what the see-through complaint needed and the smaller half. The other
        /// half is the shader's <c>BubbleScreenFade</c>: a ball with the pile in front of it was not fading at
        /// all, so four layers of shells showed through one another with equal clarity. Neither figure fixes
        /// what the other one is about — check both before moving either.
        /// </para>
        /// </summary>
        private const float BUBBLE_BODY_OPACITY = 0.9f;

        /// <summary>
        /// How much of its own colour a bubble radiates, the transparent counterpart of <see cref="EMISSION"/>.
        /// Lower than the vinyl ball's, and that is not a smaller glow but the same one: emission is added to a
        /// premultiplied output and a film covers a fraction of its pixel where a skin covers all of it, so at
        /// the skin's figure a bubble reads as a paper lantern rather than as glass with a light in it.
        /// </summary>
        private const float BUBBLE_EMISSION = 0.42f;

        /// <summary>
        /// The near wall of the shell, and the far one. They are drawn as two passes with opposite cull modes
        /// — see <see cref="Draw"/>, which carries the whole argument — and the shader turns its normal by this
        /// sign so both go through one piece of arithmetic.
        /// </summary>
        private const float BUBBLE_NEAR_WALL = 1f;

        private const float BUBBLE_FAR_WALL = -1f;

        /// <summary>
        /// How many vein bands run over a marble ball before the turbulence bends them (#305). Five is close
        /// to the beach ball's five gores on purpose: it is the spacing at which a figure on a sphere this
        /// size reads as a figure and not as a stripe, and the vinyl's had already been settled at it.
        /// </summary>
        private const float MARBLE_VEIN_FREQUENCY = 3.5f;

        /// <summary>
        /// How far the turbulence bends those bands. Rather more than the frequency, deliberately — under
        /// about the band spacing the veins still read as displaced <i>rings</i>, and it is only once the warp
        /// can carry a vein across a whole band that they wander, split and rejoin the way a real seam does.
        /// </summary>
        private const float MARBLE_VEIN_WARP = 5f;

        /// <summary>
        /// How far a vein carries the type colour towards white, and the one figure of this style the thirteen
        /// colours are spent on. It is a <b>lightening of the tint</b> and never a white overlay: white veins
        /// are a white ball with colour between them, which is the trap the beach ball's gores set for Type4
        /// and Type11 and would be the whole of Type8, whose tint is a 0.045 grey.
        /// <para>
        /// 0.55 leaves a vein plainly lighter than its body at every tint while keeping the hue in it. Well
        /// over that the pale types run together; well under, the stone reads as unfigured and the style loses
        /// its rotation cue, which lives entirely in the veins.
        /// </para>
        /// </summary>
        private const float MARBLE_VEIN_CONTRAST = 0.6f;

        /// <summary>
        /// How much of its own colour a marble ball radiates, against the vinyl's <see cref="EMISSION"/>.
        /// Lower, because a heavy opaque stone that glows as brightly as an inflatable reads as a lamp cut in
        /// the shape of a ball — and the polish gives this style a brightness of its own that the vinyl has to
        /// find in its emission. Not zero: the heartbeat is what the balls ARE, and a cluster that does not
        /// breathe on a dark dome is a cluster of grey circles.
        /// </summary>
        private const float MARBLE_EMISSION = 0.32f;

        /// <summary>
        /// How many strands are wound across a wool ball (#311), as a wave count over the object-space
        /// direction — so the diameter shows about a third of this many crossings, seven or eight at this
        /// figure. Chosen against the ball's size on screen rather than against a photograph of yarn: at the
        /// stand-off a level is played from a ball is a few dozen pixels across, and a strand has to be several
        /// of them wide or the winding averages into a flat wash and takes the rotation cue with it.
        /// </summary>
        private const float WOOL_STRAND_FREQUENCY = 24f;

        /// <summary>
        /// Peak height of a strand's ridge in world units, against the ball's <see cref="BALL_RADIUS"/> of 0.5
        /// — so a strand stands about 4 % of the radius proud. It only tilts the normal; the silhouette stays
        /// the sphere's, which is what keeps this style free of any geometry and lets it ride the same LOD
        /// ladder as every other.
        /// </summary>
        private const float WOOL_STRAND_DEPTH = 0.02f;

        /// <summary>
        /// The fuzz at the silhouette: loose fibres lit from behind, in the ball's own colour. The one figure
        /// of this style that has to be defended against a whole cluster rather than one ball — it adds light
        /// to every rim at once, and rims are most of what is visible of a ball in a pile, so what reads as
        /// softness on a single ball can read as a glow on four hundred.
        /// </summary>
        private const float WOOL_HALO = 0.15f;

        /// <summary>
        /// How much of its own colour a wool ball radiates. Between the vinyl's <see cref="EMISSION"/> and the
        /// marble's: a fibre surface scatters its own light out through a soft edge rather than off a hard one,
        /// so it carries a glow convincingly — but it has no polish to be bright with either, and at the
        /// vinyl's figure the strand relief washes out under its own emission.
        /// </summary>
        private const float WOOL_EMISSION = 0.24f;

        /// <summary>
        /// Wave count of the brush grain over a metal ball (#306). Far finer than the wool's winding, because a
        /// brushed finish is many lines and not a few: what has to survive at the stand-off a level is played
        /// from is not an individual line — it is the <i>direction</i> the highlight streaks along, and that
        /// reads even once the lines themselves are below a pixel and the band-limit has faded them.
        /// </summary>
        private const float METAL_BRUSH_FREQUENCY = 26f;

        /// <summary>
        /// Peak height of a brush ridge in world units — a third of the wool strand's, against a ball radius of
        /// 0.5. A polish direction, not a corrugation.
        /// </summary>
        private const float METAL_BRUSH_DEPTH = 0.010f;

        /// <summary>
        /// How much of the environment a metal ball mirrors. Over 1 deliberately, and it is the figure this
        /// style cannot do without: there is <b>no diffuse term underneath</b> to carry the ball, so unlike
        /// every other style's reflection dial this one is the whole of what makes it visible. Under about 1.2
        /// a cluster goes dark against everything but the brightest domes.
        /// </summary>
        private const float METAL_REFLECTANCE = 1.6f;

        /// <summary>
        /// How much of its own colour a metal ball radiates. The lowest of any style, and for a reason that is
        /// the style's own logic: a metal is the one surface here that has no inside for light to come out of,
        /// so a strong self-glow reads as the thing not being metal at all. It is not zero only because the
        /// heartbeat is what the balls ARE.
        /// </summary>
        private const float METAL_EMISSION = 0.18f;

        /// <summary>
        /// How many crack lines run through an ice ball (#307) — three fields at this frequency and its two
        /// ratios, so a ball carries a handful of them crossing rather than a craze. Deliberately low: this is
        /// a ball frozen through, not a dropped one, and a dense net reads as shattered.
        /// </summary>
        private const float ICE_CRACK_FREQUENCY = 10f;

        /// <summary>
        /// How wide a crack is, as a fraction of its line field's amplitude. A crack is a plane inside the ice
        /// seen edge-on; anything with an area is a facet, which is #308's style and not this one.
        /// </summary>
        private const float ICE_CRACK_WIDTH = 0.09f;

        /// <summary>
        /// How brightly an ice ball's silhouette goes cool and pale. Judged on a <b>cluster</b> and never on
        /// one ball: it is light added to every rim at once and a pile is mostly rims, so what reads as cold on
        /// a single ball reads as a fog over four hundred.
        /// </summary>
        private const float ICE_RIM = 0.45f;

        /// <summary>
        /// How much light an ice ball carries through itself from a source behind it — the whole of what makes
        /// it ice rather than pale marble, and far over the vinyl skin's <see cref="TRANSLUCENCY"/>, which is a
        /// thin shell letting a little light past. This is a solid that scatters all the way through.
        /// </summary>
        private const float ICE_TRANSLUCENCY = 1.1f;

        /// <summary>
        /// How much of its own colour an ice ball radiates. Near the vinyl's <see cref="EMISSION"/>: a
        /// scattering solid is the one surface here that can carry a glow honestly, because light really does
        /// come out of its inside — which is also why the emission does not flatten it the way it flattened the
        /// wool (see <see cref="WOOL_EMISSION"/>).
        /// </summary>
        private const float ICE_EMISSION = 0.44f;

        /// <summary>
        /// How finely a gem's direction is quantized (#308), and so how many faces the stone is cut into. Two
        /// is a chunky brilliant, which is what a ball a few dozen pixels across can actually show: at four the
        /// faces are smaller than the band-limit can resolve and the stone smooths back into a sphere anyway,
        /// so the extra faces cost arithmetic and buy nothing.
        /// </summary>
        private const float GEM_FACET_COUNT = 2f;

        /// <summary>
        /// How hard the height field drives the shading normal onto its own face. Under about 0.5 the faces
        /// only bend the light and the stone reads as dimpled rather than cut; this is as flat as it can be
        /// made without touching the mesh, which #271 forbids.
        /// </summary>
        private const float GEM_FACET_DEPTH = 1.4f;

        /// <summary>
        /// How deeply a gem absorbs its own colour along the view — the figure the thirteen colours are spent
        /// on here. It is what makes the stone read as something with a <i>volume</i> rather than a painted
        /// shell, and the reason it needs defending is the dark end: absorption tuned for the bright hues takes
        /// Type8, Type10, Type12 and Type13 to the same near-black, four of thirteen lost at once. The answer
        /// is <c>GemBodyFloor</c> in the shader, not a smaller figure here.
        /// </summary>
        private const float GEM_ABSORPTION = 1.1f;

        /// <summary>
        /// How much of its own colour a gem radiates. Low, like the metal's: a stone this bright already
        /// carries its hue in the girdle and the absorbed body, and a strong self-glow fills the faces in and
        /// takes the cut with it.
        /// </summary>
        private const float GEM_EMISSION = 0.22f;

        /// <summary>
        /// How far the warp field displaces the filament field on a plasma ball (#309) — the whole character
        /// of the arcs. At zero they are smooth rings; the warp is what makes them writhe, fork and rejoin,
        /// which is the difference between a plasma globe and a marbled sphere.
        /// </summary>
        private const float PLASMA_WARP = 0.9f;

        /// <summary>
        /// How brightly a filament burns, and the figure this style cannot do without: its colour is entirely
        /// emissive, so this is the only thing standing between a cluster and darkness. Well over 1 because a
        /// filament covers a small fraction of the ball and has to carry the whole of its hue.
        /// </summary>
        private const float PLASMA_GLOW = 2.4f;

        /// <summary>
        /// How fast the arcs crawl. Deliberately slow — anything quick enough to be noticed as <i>animation</i>
        /// stops reading as something alive and starts reading as a loop, which is the one way this style can
        /// fail while every individual frame of it still looks right.
        /// </summary>
        private const float PLASMA_SPEED = 0.5f;

        /// <summary>
        /// A plasma ball radiates through its filaments and not as a whole, so the flat emission every other
        /// style carries is <b>zero</b> here: the heartbeat is applied to the arcs instead (the shader does
        /// it), which is what keeps the pulse from being a second brightness fighting the first.
        /// </summary>
        private const float PLASMA_EMISSION = 0f;

        /// <summary>
        /// How many plate seams run over a lava ball (#310). Low, and lower than the ice's cracks: a cooling
        /// crust breaks into a handful of big plates, and a dense net reads as gravel rather than a shell.
        /// </summary>
        private const float LAVA_SEAM_FREQUENCY = 3.0f;

        /// <summary>
        /// How wide a seam is — twice the ice's crack and for a reason that is the whole difference between the
        /// two: a crack is a plane seen edge-on, and this is a <b>gap</b> with molten rock at the bottom of it.
        /// </summary>
        private const float LAVA_SEAM_WIDTH = 0.35f;

        /// <summary>
        /// How brightly the molten interior glows through. The whole of this style's colour: over a near-black
        /// crust there is nothing else to see the ball by, which is why it is well over 1.
        /// </summary>
        private const float LAVA_GLOW = 2.6f;

        /// <summary>
        /// Zero, and for the same reason the plasma's is (see <see cref="PLASMA_EMISSION"/>): this style
        /// radiates through its seams and not as a whole, and the heartbeat is routed into those seams in the
        /// shader. A flat emission beside a breathing seam is a ball that pulses twice as hard as its
        /// neighbours — the double-application this style was most at risk of.
        /// </summary>
        private const float LAVA_EMISSION = 0f;

        /// <summary>
        /// Wave count of a porcelain ball's crazing (#312) — the finest figure of any style here, because
        /// craquelure <i>is</i> fine. Still bounded by what a ball a few dozen pixels across can resolve, which
        /// is the lesson <see cref="METAL_BRUSH_FREQUENCY"/> paid for with an invisible one.
        /// </summary>
        private const float PORCELAIN_CRACK_FREQUENCY = 10f;

        /// <summary>
        /// How wide a hairline is — the thinnest in the set, and a third of the lava's seam. A crack in a glaze
        /// has no area at all; a wide one reads as a broken egg rather than as an antique.
        /// </summary>
        private const float PORCELAIN_CRACK_WIDTH = 0.13f;

        /// <summary>
        /// How deep and wet the glaze looks. It is the figure to watch on a bright dome: a glaze reflection is
        /// a dielectric coat and not a mirror, so the metal's brightness-not-colour departure is probably not
        /// needed here — but a dark glaze under a bright sky is where a strong coat can wash a whole ball to
        /// sky colour, and that is the case to check before raising it.
        /// </summary>
        private const float PORCELAIN_GLAZE = 0.9f;

        /// <summary>
        /// How much of its own colour a porcelain ball radiates. Between the marble's and the vinyl's: a glaze
        /// carries a glow better than bare stone because the coat scatters it, but it has a bright specular of
        /// its own and does not need the emission to be seen.
        /// </summary>
        private const float PORCELAIN_EMISSION = 0.36f;

        /// <summary>
        /// Wave count of a rock's mineral grain (#324) — but the figure is a <b>fleck field</b>, a product of
        /// three waves at about this count, so what it draws is roughly three times as fine as the number
        /// suggests.
        /// <para>
        /// It was 30, on the argument that grains too small and too many to count are what says "aggregate".
        /// True of a rock held in the hand and false of one seen across the arena: the band limit that keeps a
        /// fine figure from crawling had taken the whole grain out at any distance a level is played from, and
        /// the ball drew as a plain grey sphere. What identifies a rock at play distance is its value, the
        /// absence of gores, the absence of a heartbeat and the <i>lumps</i> — the grain is for the close
        /// range, where the drop cinematic, precise aim and the map editor look at one.
        /// </para>
        /// </summary>
        private const float STONE_GRAIN_FREQUENCY = 14f;

        /// <summary>
        /// How far a grain carries from the body grey. Strong — over half — because it is doing on its own the
        /// job the thirteen tints do for every other ball: this is the one ball in the game that has to be
        /// recognised without a colour.
        /// </summary>
        private const float STONE_GRAIN_CONTRAST = 0.55f;

        /// <summary>
        /// Peak height of a rock's relief, in world units — six times the vinyl's moulding and the largest ball
        /// figure in the set. A rock is the only <i>unfinished</i> surface here; everything else was cast,
        /// blown, wound, ground or glazed, and reads as smooth on purpose.
        /// </summary>
        private const float STONE_ROUGHNESS = 0.06f;

        /// <summary>
        /// What a rock radiates in its own grey. <b>It is drawn at <c>PulseDepth</c> zero, so this is a steady
        /// floor and never a beat</b> — the rock is the one ball in the game that does not breathe, and that,
        /// not any figure drawn on a sphere, is what names it across a whole field: motion is the first thing
        /// the eye reads and this is the only ball without it.
        /// <para>
        /// <b>It was zero, on the argument that a rock is dead and radiates nothing, and that was measured
        /// wrong three times before the cause was found.</b> The player looks UP at the cluster from the
        /// island, at its unlit side — and every other ball lights that side by two routes a rock has neither
        /// of: its own emission, and <c>TranslucencyStrength</c>, the light carried THROUGH a hollow shell from
        /// the key light behind it. A rock is solid and does not glow, so it was the only genuinely dark ball
        /// in the frame and it read as the 8-ball. Raising the body tint did nothing, and neither did the
        /// strongest ambient in the palette; the two missing terms were worth about as much as everything else
        /// put together.
        /// </para>
        /// <para>
        /// So it is set <i>above</i> the marble's 0.32 rather than below it, and the figure is standing in for
        /// the translucency the material cannot have. What it must never buy back is the beat.
        /// </para>
        /// </summary>
        private const float STONE_EMISSION = 0.45f;

        /// <summary>
        /// <b>What a bomb is drawn with, and these three numbers ARE the "armed" read</b> (#326). The casing,
        /// the grooves and the studs are what confirm a bomb once the player is close enough to aim; what
        /// names one across a whole field is the beat, and the beat is these.
        /// <para>
        /// It is the stone's own finding used the other way round. A rock is legible at any distance and under
        /// any dome because it is the one ball that does <b>not</b> breathe, motion being the first thing the
        /// eye reads; so the bomb takes the other end of that same channel — the deepest and fastest heartbeat
        /// in the game, against a cluster resting at <see cref="PULSE_DEPTH_RESTING"/>. Nothing about the
        /// figure on the casing could have done that job, because at the stand-off a level is played from a
        /// figure is a few pixels wide and a rhythm is not.
        /// </para>
        /// <para>
        /// The emission is high because the shader multiplies it by the <i>seam mask</i> rather than by the
        /// whole ball: what beats is the light coming out of the joins, so most of the surface is paying none
        /// of this. A ball flashing this deep and this fast over its whole area would strobe.
        /// </para>
        /// </summary>
        private const float BOMB_EMISSION = 1.25f;

        /// <inheritdoc cref="BOMB_EMISSION"/>
        private const float BOMB_PULSE_DEPTH = 1f;

        /// <inheritdoc cref="BOMB_EMISSION"/>
        private const float BOMB_PULSE_SPEED = 2.6f;

        /// <summary>
        /// How much of the picture behind it a clear ball takes away face-on (#325), against the dyed film's
        /// <see cref="BUBBLE_BODY_OPACITY"/>. <b>Lower, and that is the whole read of this kind</b>: a bubble is
        /// a coloured thing you can see through and this is a thing that is not there — what names it is the
        /// rim, the highlight and the seam, with as close to nothing as possible in between.
        /// <para>
        /// Not zero, though, and the reason is the same one that gave the rock its emission: the player looks
        /// <i>up</i> at the cluster, and a shell with no body at all against a bright dome is an invisible ball
        /// the shot lands beside for no reason the player can see. This is the floor at which the sphere still
        /// reads as an object from the island.
        /// </para>
        /// </summary>
        private const float HOLLOW_BODY_OPACITY = 0.06f;

        /// <summary>
        /// What clear glass radiates: nearly nothing, and <b>white rather than a colour</b>, since it has none.
        /// It is drawn at <c>PulseDepth</c> zero like the rock, so this is a steady floor and not a beat — but
        /// where the rock's figure is that it does not breathe, this one's is that it barely is. A glass ball
        /// that glowed would be announcing a colour it does not have yet.
        /// </summary>
        private const float HOLLOW_EMISSION = 0.05f;

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
        /// How dark a ball with a full first shell gets: its lighting is scaled down by up to this fraction.
        /// The whole of the occlusion until #303 — and the whole of the owner's report there: the twelve
        /// touching cells are all the count can see, so a ball one layer under the surface and one in the
        /// dead centre of the cluster read identically, and nothing past the first shell ever got darker.
        /// </summary>
        private const float OCCLUSION_STRENGTH = 0.55f;

        /// <summary>
        /// How much further burial takes it (#303): the W channel drops another this-much between a ball
        /// touching air and one <see cref="AirDepthField.MAX_DEPTH"/> shells in, so the factor bottoms out at
        /// <c>1 − OCCLUSION_STRENGTH − OCCLUSION_DEPTH_STRENGTH</c> = 0.10 — dark enough that depth finally
        /// reads, short enough of zero that a buried ball is still a ball rather than a hole.
        /// <para>
        /// <b>Mirrored in the shader</b> as <c>OcclusionFirstShellFloor</c>/<c>OcclusionDepthStrength</c>
        /// (InstancedModel.fx), which read the burial back out of W to pull the key light down — the two
        /// sides are one contract, and changing either alone reads wrong everywhere at once.
        /// </para>
        /// </summary>
        private const float OCCLUSION_DEPTH_STRENGTH = 0.35f;

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
        /// <param name="airDepth">How many shells of balls stand between this cell and open air, from
        /// <see cref="AirDepthField.DepthAt"/> — the burial the neighbour count saturates too early to see
        /// (#303). Zero for every surface ball, so the first shell's own term is untouched by it.</param>
        public static Vector4 OcclusionTarget(int occluders, Vector3 occluderDirectionSum, int airDepth) =>
            new(occluderDirectionSum / MAX_OCCLUDERS,
                1f - OCCLUSION_STRENGTH * occluders / MAX_OCCLUDERS
                   - OCCLUSION_DEPTH_STRENGTH * Math.Min(airDepth, AirDepthField.MAX_DEPTH)
                       / (float)AirDepthField.MAX_DEPTH);

        //The burial field the static-map walk reads (#303), owned here so AddMap allocates nothing per call;
        //the hanging cluster's twin lives on ClusterCollector, which owns the walk over the physics array.
        private readonly AirDepthField _mapAirDepth = new();

        internal AirDepthField MapAirDepth => _mapAirDepth;

        #endregion

        #region The instance buckets

        //One bucket per (type, LOD) pair, each becoming a single instanced draw call. Allocated lazily - most
        //frames touch a handful of the fifty-two, and a map of one colour touches four - and doubled when a
        //bucket fills, so the arrays settle at whatever the scene actually needs within the first few frames
        //and nothing is allocated per frame after that. 256 is a couple of levels of a full map: big enough
        //that the common case never grows, small enough that fifty-two of them cost nothing.
        private const int BUCKET_INITIAL_CAPACITY = 256;

        //The buckets come in TWO planes of (type, LOD) since #252: the breathing balls and the STILL ones. The
        //still plane exists for the rounds loaded in the cannon, which the owner ruled must not pulse at all —
        //the muzzle round's colour is said by its halo and by the HUD strip, and a brightness animation on the
        //ball on top of those is the thing #236 set out to remove.
        //
        //A plane rather than a flag on the instance, and that is forced rather than chosen: the pulse is a
        //per-RENDERER uniform (EmissiveStrength, PulseDepth, PulseSpeed and the rest are pushed once per draw
        //call), so "this ball does not breathe" cannot travel with the ball. It has to be a different DRAW.
        //The alternative was a sixth float on ModelInstance for the shader to multiply the emission by, which
        //means a vertex-stream change and every producer of instances touched, for five balls a frame.
        //
        //It costs the still plane's own draw calls — at most one per type actually loaded, so up to five, and
        //they are five-instance calls. The buckets are lazy, so the plane costs nothing at all in a frame with
        //no magazine (the map editor's, the menu backdrop's).
        //static readonly and not const, because both of its factors are: TYPE_COUNT comes off the BallType enum
        //(#152, so a colour added cannot be forgotten) and LodCount off the LOD table's own length.
        private static readonly int STILL_PLANE_STRIDE = TYPE_COUNT * LodCount;

        //And a THIRD region after those two planes, for the rocks (#324), on exactly the argument the still
        //plane's comment above makes: what a rock is drawn by — the technique, the emission, the pulse depth —
        //is a per-RENDERER uniform, so "this ball is stone" cannot travel with the ball either. It has to be
        //its own draw.
        //
        //It is a region and not a plane, and the difference is the point: a rock has no COLOUR, so there is
        //nothing to bucket per type and this is LodCount buckets rather than TYPE_COUNT × LodCount. That is
        //the same fact the shading rests on — the stone ignores the per-draw tint — arriving here as four
        //draw calls instead of fifty-two. A rock is never loaded in the cannon, so it needs no still twin.
        private static readonly int ROCK_REGION_START = STILL_PLANE_STRIDE * 2;

        //And a FOURTH region, on the same argument once more, for the clear glass of the transparent kind
        //(#325): colourless like the rock, so LodCount buckets and no per-type split, and its own draw because
        //what it is made of is a set of per-renderer uniforms. What it does NOT share with the rock is the
        //order — glass is transparent, so it goes out last of the balls rather than first, and under the shell
        //states rather than the caller's. See DrawHollow.
        //
        //A ball mid-crossing puts an instance in here AND one in its colour's bucket (BallDrawFrame.Route), so
        //this region can hold more than the field's transparent balls for a third of a second at a time. It is
        //sized like every other bucket — they grow on demand — and the census counts BALLS, which is why
        //DrawHollow leaves DrawnCount alone the way a bubble's second wall does.
        private static readonly int HOLLOW_REGION_START = ROCK_REGION_START + LodCount;

        //And a FIFTH, for the live bombs of #326, on the same argument a third time — and this one leans on
        //it harder than either of the others. What says a bomb is ARMED is the BEAT: EmissiveStrength,
        //PulseDepth and PulseSpeed are per-renderer uniforms, so "this ball breathes twice as deep and three
        //times as fast as the cluster it is standing in" is not something that can travel on an instance at
        //all. It is exactly the still plane's own argument (#252) with the dial turned the other way.
        //
        //Colourless like the other two, so LodCount buckets rather than TYPE_COUNT × LodCount, and opaque, so
        //it goes out with the rocks rather than after the glass. A bomb is never loaded in the cannon, so it
        //needs no still twin either.
        private static readonly int BOMB_REGION_START = HOLLOW_REGION_START + LodCount;

        private readonly ModelInstance[][] _buckets;
        private readonly int[] _counts;
        private readonly int[] _lodTotals;

        //The pulse depth the renderers were built with, kept so both passes can STATE the depth they draw at
        //rather than one of them relying on what the other left behind.
        private readonly float _pulseDepth;

        //What the balls are made of. Beach by construction, so a caller that never says anything draws exactly
        //what it drew before #258 — the Testbed and every map file authored without the field.
        private BallStyle _style = BallStyle.Beach;

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

            _pulseDepth = ripples ? PULSE_DEPTH_RIPPLING : PULSE_DEPTH_RESTING;

            //Two planes: the breathing balls, then the still ones (see STILL_PLANE_STRIDE), then a region
            //each for the three kinds that opt out of the level's style — the rocks (ROCK_REGION_START), the
            //clear glass and the bombs, in that order. Sized off the LAST of them so a region added without
            //moving this line would index past the end on its first instance rather than draw wrong.
            _buckets = new ModelInstance[BOMB_REGION_START + LodCount][];
            _counts = new int[BOMB_REGION_START + LodCount];
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
        /// What the balls are made of (#258): the moulded vinyl beach ball, or a hollow glass bubble. It picks
        /// the shading technique and, with it, the drawing states <see cref="Draw"/> puts the shell out under —
        /// which is why it lives here and not on the renderers, and why a caller sets it and then simply draws.
        /// <para>
        /// <b>State it, do not inherit it.</b> The Game has one set for the whole program and two screens hang
        /// clusters through it — the front end's preview and the played level, each carrying its own map's
        /// answer — so a screen that assumed the value it left last frame would draw the other one's style for
        /// however long it took the first one to notice. Assigning the same value again costs a comparison,
        /// deliberately, so stating it every frame is free.
        /// </para>
        /// </summary>
        public BallStyle Style
        {
            get => _style;
            set
            {
                if (_style == value) return;

                _style = value;
                ApplyStyle();
            }
        }

        /// <summary>
        /// The per-renderer figures that differ between the styles. Everything a ball IS — its colour, its
        /// heartbeat, its ripple, its dissolve, its occlusion — is the same in all of them and is set once in
        /// the constructor; what is set here is only what the surface does with light.
        /// <para>
        /// <b>One case per style, each setting what its own shading reads</b> (#304). It was two assignments
        /// against a <c>bool</c>, which pushed the bubble's three film figures at a vinyl renderer that has no
        /// use for them — harmless with two styles and the wrong shape for eight, where every style's dials
        /// would be pushed at every other style's technique on every switch.
        /// </para>
        /// </summary>
        private void ApplyStyle()
        {
            if (_renderers == null) return;

            foreach (InstancedModelRenderer renderer in _renderers)
            {
                renderer.Shading = ShadingOf(_style);

                switch (_style)
                {
                    case BallStyle.Bubble:
                        renderer.EmissiveStrength = BUBBLE_EMISSION;
                        renderer.BubbleFilmThickness = BUBBLE_FILM_THICKNESS;
                        renderer.BubbleTintStrength = BUBBLE_TINT;
                        renderer.BubbleBodyOpacity = BUBBLE_BODY_OPACITY;
                        break;

                    case BallStyle.Porcelain:
                        renderer.EmissiveStrength = PORCELAIN_EMISSION;
                        renderer.PorcelainCrackFrequency = PORCELAIN_CRACK_FREQUENCY;
                        renderer.PorcelainCrackWidth = PORCELAIN_CRACK_WIDTH;
                        renderer.PorcelainGlaze = PORCELAIN_GLAZE;
                        break;

                    case BallStyle.Lava:
                        renderer.EmissiveStrength = LAVA_EMISSION;
                        renderer.LavaSeamFrequency = LAVA_SEAM_FREQUENCY;
                        renderer.LavaSeamWidth = LAVA_SEAM_WIDTH;
                        renderer.LavaGlow = LAVA_GLOW;
                        break;

                    case BallStyle.Plasma:
                        renderer.EmissiveStrength = PLASMA_EMISSION;
                        renderer.PlasmaWarp = PLASMA_WARP;
                        renderer.PlasmaGlow = PLASMA_GLOW;
                        renderer.PlasmaSpeed = PLASMA_SPEED;
                        break;

                    case BallStyle.Gem:
                        renderer.EmissiveStrength = GEM_EMISSION;
                        renderer.GemFacetCount = GEM_FACET_COUNT;
                        renderer.GemFacetDepth = GEM_FACET_DEPTH;
                        renderer.GemAbsorption = GEM_ABSORPTION;
                        break;

                    case BallStyle.Ice:
                        renderer.EmissiveStrength = ICE_EMISSION;
                        renderer.TranslucencyStrength = ICE_TRANSLUCENCY;
                        renderer.IceCrackFrequency = ICE_CRACK_FREQUENCY;
                        renderer.IceCrackWidth = ICE_CRACK_WIDTH;
                        renderer.IceRim = ICE_RIM;
                        break;

                    case BallStyle.Metal:
                        renderer.EmissiveStrength = METAL_EMISSION;
                        renderer.MetalBrushFrequency = METAL_BRUSH_FREQUENCY;
                        renderer.MetalBrushDepth = METAL_BRUSH_DEPTH;
                        renderer.MetalReflectance = METAL_REFLECTANCE;
                        break;

                    case BallStyle.Wool:
                        renderer.EmissiveStrength = WOOL_EMISSION;
                        renderer.WoolStrandFrequency = WOOL_STRAND_FREQUENCY;
                        renderer.WoolStrandDepth = WOOL_STRAND_DEPTH;
                        renderer.WoolHalo = WOOL_HALO;
                        break;

                    case BallStyle.Marble:
                        renderer.EmissiveStrength = MARBLE_EMISSION;
                        renderer.MarbleVeinFrequency = MARBLE_VEIN_FREQUENCY;
                        renderer.MarbleVeinWarp = MARBLE_VEIN_WARP;
                        renderer.MarbleVeinContrast = MARBLE_VEIN_CONTRAST;
                        break;

                    case BallStyle.Beach:
                        renderer.EmissiveStrength = EMISSION;

                        //STATED, not inherited, and it is the ice that makes this necessary (#307). The
                        //translucency is a shared renderer property that the vinyl technique also reads, and
                        //the ice sets it to three times the skin's figure — so a set that had drawn ice and
                        //switched back would light every vinyl ball as a lantern until something else moved
                        //it. This is the same trap the shot trail's two callers paid for once.
                        renderer.TranslucencyStrength = TRANSLUCENCY;
                        break;
                }
            }
        }

        /// <summary>
        /// Which of the shader's ball techniques a style is drawn by. Two enums and a mapping rather than one
        /// enum, because they answer to different owners: <see cref="BallStyle"/> is what a level FILE names
        /// and cannot change without a format decision, while <see cref="BallShading"/> is what
        /// <c>InstancedModel.fx</c> was compiled with and lives in the game-agnostic library that cannot see
        /// the level format at all. They match one for one today and are not required to — two styles differing
        /// only in their dials would share one technique.
        /// </summary>
        private static BallShading ShadingOf(BallStyle style) => style switch
        {
            BallStyle.Beach => BallShading.Vinyl,
            BallStyle.Bubble => BallShading.Bubble,
            BallStyle.Marble => BallShading.Marble,
            BallStyle.Wool => BallShading.Wool,
            BallStyle.Metal => BallShading.Metal,
            BallStyle.Ice => BallShading.Ice,
            BallStyle.Gem => BallShading.Gem,
            BallStyle.Plasma => BallShading.Plasma,
            BallStyle.Lava => BallShading.Lava,
            BallStyle.Porcelain => BallShading.Porcelain,

            //EXHAUSTIVE, AND IT THROWS RATHER THAN FALLING BACK, because the fallback here cost a whole
            //style once (#307): this read "_ => BallShading.Vinyl", the ice was added everywhere else and
            //never listed here, and it drew as a BEACH BALL. Nothing failed — the technique compiled, the
            //uniforms were pushed at a program that does not declare them, and the only symptom was that
            //changing the ice's figures changed nothing on screen, which reads as a shader that does not
            //work rather than as one that is never selected. #304 checks that every SHADING has a technique;
            //this is the same check one level up, for every STYLE having a shading.
            //
            //Throwing is safe where a fallback would not be: only ApplyStyle calls this, on a style CHANGE
            //and never per frame, and every value that reaches it is a real enum member — BallStyles.TryParse
            //cannot produce anything else — so an unmapped one is a programming error and not bad input.
            _ => throw new ArgumentOutOfRangeException(nameof(style), style,
                $"{nameof(BallStyle)}.{style} has no {nameof(BallShading)} beside it. Add one here and a "
                + "technique to InstancedModelRenderer's table, or the style draws as whatever this fell "
                + "back to and nothing reports it.")
        };

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
        /// that type's material and diffuse tint — twice over for a glass bubble, which is a shell with two
        /// walls (see <see cref="DrawShell"/>). Closes the frame, so the next <see cref="BeginFrame"/> is legal
        /// again.
        /// <para>
        /// <b>Where</b> in the frame this is called is the caller's, and load-bearing in all three: over the
        /// opaque scene so the cluster and the gun are in the depth buffer, before the shots' additive smears
        /// and before any glass composites over them.
        /// </para>
        /// <para>
        /// <b>The drawing states are the caller's for the vinyl ball and this method's for the bubble</b>, which
        /// is the one asymmetry in here. An opaque ball is drawn under whatever the scene's own baseline is; a
        /// transparent one cannot be, because its correctness is in the states. They are put back as they were
        /// found, so the glass drawn after the balls still stands on the caller's baseline either way.
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

            //The rocks first, and OPAQUE first is the reason (#324): on a transparent style the films below are
            //blended over whatever is already in the target, so a rock drawn after them would be laid on top of
            //the bubbles it stands behind. It costs nothing on the opaque styles, where the order does not
            //matter, so it is stated once here rather than made conditional.
            DrawRocks(camera);

            //And the bombs with them (#326), on the same argument: opaque, so they belong on this side of the
            //films. After the rocks and not before, for no reason beyond a stable order — neither reads the
            //other's uniforms, both state their own.
            DrawBombs(camera);

            //Asked as the question it IS — does light get through this material — and not as "is this the
            //bubble" (#304). The two were the same answer only while the bubble was the one transparent style.
            if (BallStyles.IsTransparent(_style)) DrawShell(camera);
            else DrawBoth(camera);

            //And the clear glass LAST (#325), which is the opposite end of the frame from the rocks and for the
            //mirror-image reason: it is transparent, so everything it should show through it has to be in the
            //target already — the opaque cluster above, the island and the gun under that. On a bubble level it
            //composites over the films for the same reason, which is the honest approximation the shell draw
            //already makes among the films themselves (see DrawShell on why nothing here is sorted).
            DrawHollow(camera);
        }

        /// <summary>
        /// The rocks (#324): one instanced call per LOD that has any, drawn as stone whatever the level's balls
        /// are made of.
        /// <para>
        /// <b>A rock opts out of the map's <see cref="BallStyle"/>, and that is the whole reason this is a draw
        /// of its own.</b> The shading, the emission and the pulse depth are all per-renderer uniforms, so
        /// "this one ball is stone and does not breathe" cannot travel on the instance — the same argument the
        /// still plane rests on (#252), arriving a second time. What it buys is that the odd ball out reads as
        /// odd on all ten materials: a rock among glass bubbles and a rock among molten crusts are the same
        /// rock, which is what makes it a signal the player can learn once.
        /// </para>
        /// <para>
        /// The renderers are put back to the level's style afterwards through <see cref="ApplyStyle"/> rather
        /// than by restoring the fields this touched. Same discipline, one fewer thing to forget: what a style
        /// is, is exactly what that method pushes.
        /// </para>
        /// </summary>
        private void DrawRocks(ICamera camera)
        {
            bool any = false;
            for (int lod = 0; lod < LodCount && !any; lod++) any = _counts[ROCK_REGION_START + lod] > 0;

            //A field with no rocks in it — every level shipped today — never touches a renderer for this at
            //all, so the style stays pushed exactly as it was and this costs one compare per LOD.
            if (!any) return;

            for (int lod = 0; lod < LodCount; lod++)
            {
                InstancedModelRenderer renderer = _renderers[lod];

                renderer.Shading = BallShading.Stone;
                renderer.StoneGrainFrequency = STONE_GRAIN_FREQUENCY;
                renderer.StoneGrainContrast = STONE_GRAIN_CONTRAST;
                renderer.StoneRoughness = STONE_ROUGHNESS;
                renderer.EmissiveStrength = STONE_EMISSION;
                renderer.PulseDepth = 0f;
            }

            for (int lod = 0; lod < LodCount; lod++)
            {
                int bucketIndex = ROCK_REGION_START + lod;
                int count = _counts[bucketIndex];
                if (count == 0) continue;

                DrawnCount += count;
                _lodTotals[lod] += count;

                //No TINT — the stone technique reads none, and handing it a colour is what this kind exists not
                //to do — but its own material all the same. Passing null for both was the first build's bug:
                //the effect params carry the AMBIENT, which is the whole of a ball's unlit side, and null falls
                //back on DefaultLighting's dim blue. See BasicEffectParamsProvider.Stone.
                _renderers[lod].Draw(camera, _buckets[bucketIndex], count, BasicEffectParamsProvider.Stone, null);
            }

            ApplyStyle();
        }

        /// <summary>
        /// The live bombs (#326): one instanced call per LOD that has any, drawn as a dark ribbed casing with
        /// a charge burning in its seams — whatever the level's balls are made of, on the rock's argument
        /// exactly.
        /// <para>
        /// <b>The three uniforms it pushes ARE the effect.</b> A bomb has to read as armed at play distance on
        /// all ten materials, and what carries that is the beat rather than the figure: the deepest and fastest
        /// heartbeat in the game against a cluster resting at <see cref="PULSE_DEPTH_RESTING"/> — the stone's
        /// own finding (motion is the first thing the eye reads, and a rock is named across a field by having
        /// none) used at the other end of the same channel. That is also why this cannot travel on an instance
        /// and has to be a draw: emission, depth and speed are per-renderer.
        /// </para>
        /// <para>
        /// <b>⚠ It puts the pulse SPEED back by hand, and that is the one thing <see cref="ApplyStyle"/> cannot
        /// do for it.</b> Depth is restored for free — <see cref="DrawPlane"/> states it on every ordinary
        /// draw, which is the discipline this file keeps — but the speed is set once when the renderers are
        /// built and nothing states it per frame, so a bomb left in it would put the whole cluster on the
        /// bomb's heartbeat for the rest of the frame. Restored here rather than by teaching
        /// <c>DrawPlane</c> to state it, because that method returns early when nothing is loaded and so
        /// cannot be relied on to restore anything.
        /// </para>
        /// </summary>
        private void DrawBombs(ICamera camera)
        {
            bool any = false;
            for (int lod = 0; lod < LodCount && !any; lod++) any = _counts[BOMB_REGION_START + lod] > 0;

            //A field with no bombs in it never touches a renderer for this at all, and pays one compare per LOD.
            if (!any) return;

            for (int lod = 0; lod < LodCount; lod++)
            {
                InstancedModelRenderer renderer = _renderers[lod];

                renderer.Shading = BallShading.Bomb;
                renderer.EmissiveStrength = BOMB_EMISSION;
                renderer.PulseDepth = BOMB_PULSE_DEPTH;
                renderer.PulseSpeed = BOMB_PULSE_SPEED;
            }

            for (int lod = 0; lod < LodCount; lod++)
            {
                int bucketIndex = BOMB_REGION_START + lod;
                int count = _counts[bucketIndex];
                if (count == 0) continue;

                DrawnCount += count;
                _lodTotals[lod] += count;

                //No TINT, for the stone's reason: a bomb wearing one of the thirteen is a lie the player acts
                //on. Its own material all the same — passing null for both is what left the first rock lit by
                //DefaultLighting's dim blue. See BasicEffectParamsProvider.Bomb.
                _renderers[lod].Draw(camera, _buckets[bucketIndex], count, BasicEffectParamsProvider.Bomb, null);
            }

            for (int lod = 0; lod < LodCount; lod++) _renderers[lod].PulseSpeed = PULSE_BEATS_PER_SECOND;

            ApplyStyle();
        }

        /// <summary>
        /// The clear glass of the transparent kind (#325): the two walls of a hollow shell, exactly as
        /// <see cref="DrawShell"/> puts a bubble out, but over one colourless region and whatever the level's
        /// own balls are made of.
        /// <para>
        /// <b>It is the rock's argument (#324) with the answer turned round.</b> A kind opts out of the map's
        /// <see cref="BallStyle"/> so that "that one is different" survives all ten materials — and the whole
        /// point of THIS kind is that it has no colour to be different in, so what it opts out into is a
        /// material with no dye at all. On a bubble level it is the one undyed shell among coloured films; on a
        /// lava level it is the one thing in the frame that is not burning.
        /// </para>
        /// <para>
        /// <b>Why the shell states are repeated here rather than shared with <see cref="DrawShell"/>.</b> They
        /// are the same states and they are set twice on purpose: this draw happens on both kinds of level, and
        /// on an opaque one there is no <c>DrawShell</c> to have set them. Inheriting them would make this
        /// method correct only after a bubble draw — the exact "a uniform belongs to the renderer, not to
        /// whoever set it" trap the shot trail's two callers paid for once.
        /// </para>
        /// </summary>
        private void DrawHollow(ICamera camera)
        {
            bool any = false;
            for (int lod = 0; lod < LodCount && !any; lod++) any = _counts[HOLLOW_REGION_START + lod] > 0;

            //A field with no glass in it — every level shipped today — never touches a renderer or a device
            //state for this, and pays one compare per LOD.
            if (!any) return;

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            for (int lod = 0; lod < LodCount; lod++)
            {
                InstancedModelRenderer renderer = _renderers[lod];

                renderer.Shading = BallShading.Hollow;
                renderer.BubbleBodyOpacity = HOLLOW_BODY_OPACITY;
                renderer.EmissiveStrength = HOLLOW_EMISSION;

                //It does not breathe. The heartbeat is the cluster saying it is alive in its own colour, and
                //this ball has none — a pulsing colourless shell would be a glass ball flashing WHITE, which is
                //the loudest thing in the frame saying nothing at all.
                renderer.PulseDepth = 0f;
            }

            _device.BlendState = BlendState.AlphaBlend;

            //The far wall first, tested but not written; then the near one in the ordinary cull, writing depth.
            //Same pair, same reasons, as the bubble's two walls.
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullClockwise;
            SetShell(BUBBLE_FAR_WALL);
            DrawHollowPlane(camera);

            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            SetShell(BUBBLE_NEAR_WALL);
            DrawHollowPlane(camera);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;

            ApplyStyle();
        }

        //One wall of the glass, over the one region it has. No tint: the technique reads none, for the reason
        //the stone's draw states — handing a colour to a thing whose whole meaning is that it has none is what
        //this kind exists not to do. The ambient still comes from the effect params, or the unlit side of every
        //shell would fall back on DefaultLighting's dim blue (the rock's own first bug).
        //
        //DrawnCount is deliberately NOT advanced: the counts are a census of BALLS, and a crossing ball is
        //already counted in its colour's bucket. Adding the glass ghost would report more balls drawn than the
        //caller collected — the same reason DrawShell zeroes between its walls.
        private void DrawHollowPlane(ICamera camera)
        {
            for (int lod = 0; lod < LodCount; lod++)
            {
                int bucketIndex = HOLLOW_REGION_START + lod;
                int count = _counts[bucketIndex];
                if (count == 0) continue;

                _renderers[lod].Draw(camera, _buckets[bucketIndex], count, BasicEffectParamsProvider.Stone, null);
            }
        }

        /// <summary>
        /// Both bucket planes at their own pulse depths — the whole of an opaque ball draw, and the near-wall
        /// and far-wall halves of a bubble one.
        /// <para>
        /// The ONLY thing that differs between the two passes is the pulse depth (#252): the balls loaded in
        /// the cannon are drawn with it at zero, so they radiate their colour steadily where the cluster
        /// breathes. Both state the depth they draw at rather than one inheriting whatever the other left on
        /// the renderer — the uniform belongs to the renderer, not to whoever set it, and that is the trap the
        /// shot trail's two callers already paid for once.
        /// </para>
        /// </summary>
        private void DrawBoth(ICamera camera)
        {
            DrawPlane(camera, still: false, pulseDepth: _pulseDepth);
            DrawPlane(camera, still: true, pulseDepth: 0f);
        }

        /// <summary>
        /// The glass bubble's shell, as its two walls (#258). <b>This method is the transparency</b>: the
        /// shading is <c>InstancedModelBubble</c>'s, but what makes a bubble a bubble rather than a tinted
        /// marble is the order these two draws arrive in and the depth states they arrive under.
        /// <para>
        /// <b>Why two passes and not one.</b> A hollow sphere shows the eye its far wall through its near one.
        /// One draw shows a single wall — whichever the cull mode keeps — so a bubble drawn once is a
        /// transparent disc with a rim, and the thing that says "hollow" (the second, smaller rim inside the
        /// first) is simply absent. The far wall goes first because it is behind.
        /// </para>
        /// <para>
        /// <b>Why the balls are not sorted, which is the honest limitation here.</b> Correct transparency wants
        /// the balls drawn back to front, and that is not merely expensive but structurally impossible in this
        /// renderer: a ball's colour is a per-DRAW uniform, so a bucket is one colour and one mesh, and a
        /// global order over three thousand balls cannot be expressed as fifty-two instanced calls. What is
        /// drawn instead is bucket order — deterministic within a frame, and moving only when a ball changes
        /// LOD. The near wall <i>does</i> write depth, so the front of the cluster resolves exactly; what the
        /// approximation costs is the layering of far walls among themselves, at the alphas
        /// <see cref="BUBBLE_BODY_OPACITY"/> holds them to, inside a mass of overlapping films. It reads as
        /// depth rather than as an error, which is the only reason it is acceptable.
        /// </para>
        /// <para>
        /// <b>Why the near wall writes depth at all</b>, when not writing it would let the bubbles show one
        /// another through. Because three things drawn after the balls read that depth and are wrong without
        /// it, and one of them is not negotiable: the aim beam is the overview's only word on where the gun
        /// points, and a beam that runs straight through the cluster instead of stopping at it is a guide that
        /// lies. The muzzle round's halo is carved into a ring by this same buffer (#236) and would go back to
        /// being a wash over the round, and the ceiling's glass and the floor's laser net both composite
        /// against it. A style is allowed to change how the game looks; it is not allowed to change what the
        /// player can tell from looking.
        /// </para>
        /// </summary>
        private void DrawShell(ICamera camera)
        {
            //Put back exactly what was found, the discipline every effect in this project that changes states
            //mid-frame keeps (the smears', the glass's): the caller set a translucent baseline for the whole
            //scene and the glass drawn after the balls still stands on it.
            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            //Premultiplied, which is what the shader has always output and what the caller already has bound;
            //stated anyway, because this method's whole correctness is in its states and inheriting one of
            //them would make that a claim about the caller rather than a fact about this draw.
            _device.BlendState = BlendState.AlphaBlend;

            //The far wall: tested against the opaque scene so the island and the gun still hide it, writing
            //nothing, so the near wall of its own bubble is not rejected by it.
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullClockwise;
            SetShell(BUBBLE_FAR_WALL);
            DrawBoth(camera);

            //The censuses count BALLS, not draws. Zeroing between the walls is what keeps that true of a style
            //that puts every ball out twice: DrawnCount is what a caller compares against the number of bodies
            //it collected, and LodTotals is what says whether the ladder is doing anything at this distance —
            //both nonsense at twice their size, and neither is a measure of how much work the frame did.
            ResetCounts();

            //And the near one, in the ordinary cull and writing depth for everything that comes after the
            //balls. The procedural meshes wind clockwise seen from OUTSIDE — the convention in CLAUDE.md —
            //so this is the pair of cull modes, not the other way round.
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            SetShell(BUBBLE_NEAR_WALL);
            DrawBoth(camera);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        //Back to nothing drawn: BeginFrame's own zeroing, reused between a bubble's two walls.
        private void ResetCounts()
        {
            DrawnCount = 0;

            for (int lod = 0; lod < _lodTotals.Length; lod++) _lodTotals[lod] = 0;
        }

        //Which wall the next draw puts out. It has to agree with the cull mode set beside it: the shader turns
        //the geometric normal by this sign so both walls go through one piece of arithmetic, and a sign that
        //disagrees lights the shell inside out.
        private void SetShell(float wall)
        {
            foreach (InstancedModelRenderer renderer in _renderers) renderer.BubbleShell = wall;
        }

        /// <summary>
        /// One plane of buckets, at one pulse depth — a single instanced draw call per (type, LOD) pair that has
        /// anything in it. Both of <see cref="Draw"/>'s passes are this, which is what keeps them from drifting:
        /// there is one loop over the buckets and one place that knows how a ball is shaded (#76).
        /// </summary>
        private void DrawPlane(ICamera camera, bool still, float pulseDepth)
        {
            int plane = still ? STILL_PLANE_STRIDE : 0;

            //Nothing loaded, nothing to set up: a frame with no magazine — the editor's, the menu backdrop's —
            //never touches a renderer for the still plane at all.
            bool any = false;
            for (int i = plane; i < plane + STILL_PLANE_STRIDE && !any; i++) any = _counts[i] > 0;
            if (!any) return;

            for (int lod = 0; lod < LodCount; lod++) _renderers[lod].PulseDepth = pulseDepth;

            for (int typeIndex = 0; typeIndex < TYPE_COUNT; typeIndex++)
                for (int lod = 0; lod < LodCount; lod++)
                {
                    int bucketIndex = plane + typeIndex * LodCount + lod;
                    int count = _counts[bucketIndex];
                    if (count == 0) continue;

                    DrawnCount += count;
                    _lodTotals[lod] += count;

                    BallType type = (BallType)(typeIndex + 1);

                    _renderers[lod].Draw(camera, _buckets[bucketIndex], count,
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
        /// <param name="still">Into the still plane rather than the breathing one — the loaded rounds (#252).</param>
        internal void Store(int typeIndex, int lod, in ModelInstance instance, bool still = false) =>
            StoreAt((still ? STILL_PLANE_STRIDE : 0) + typeIndex * LodCount + lod, instance);

        //A rock, into the region that has no colour in it (#324). Its own entry point rather than a flag on
        //Store, because there is no typeIndex to pass one alongside: a caller that has decided this ball is
        //stone has thereby decided its colour is not read, and a signature that still asked for one would
        //invite somebody to believe it mattered.
        internal void StoreRock(int lod, in ModelInstance instance) => StoreAt(ROCK_REGION_START + lod, instance);

        /// <summary>Clear glass — the transparent kind, and the glass half of a crossing (#325).</summary>
        internal void StoreHollow(int lod, in ModelInstance instance) => StoreAt(HOLLOW_REGION_START + lod, instance);

        /// <summary>A live bomb (#326) — colourless like the two above, and for the same reason.</summary>
        internal void StoreBomb(int lod, in ModelInstance instance) => StoreAt(BOMB_REGION_START + lod, instance);

        private void StoreAt(int bucketIndex, in ModelInstance instance)
        {
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
        /// <param name="still">True for a ball that must <b>not breathe</b> — the rounds loaded in the cannon
        /// (#252). It cannot be a value on the instance: the pulse is a per-renderer uniform, so this routes the
        /// ball into its own bucket plane and a draw of its own. Everything else leaves it alone.</param>
        /// <param name="kind">What the ball is beside its colour (#323). A <see cref="BallKind.Rock"/> goes into
        /// the rock region and is drawn as stone; see <see cref="Route"/>.</param>
        public void Add(BallType type, Vector3 position, in Matrix world, Vector4 occlusion, float dissolve = 0f,
            float ripple = 0f, bool still = false, BallKind kind = BallKind.Normal)
        {
            int typeIndex = (int)type - 1;
            if (typeIndex < 0 || typeIndex >= BallRenderSet.TYPE_COUNT) return;

            Route(kind, typeIndex, _set.LodFor(Vector3.DistanceSquared(position, _eye)),
                new ModelInstance(world, occlusion, dissolve, ripple), still, 0f);
        }

        /// <summary>
        /// Which bucket region a ball belongs in, by its kind — the one place that knows, so the two entry
        /// points above cannot disagree about it.
        /// <para>
        /// It is a switch on the kind and not a property of it, deliberately: what a kind is <i>drawn</i> as is
        /// a rendering decision and belongs on this side of the wall, next to the regions it selects between.
        /// Every special ball type of #256 that gets a look of its own arrives here as one more case.
        /// </para>
        /// </summary>
        private void Route(BallKind kind, int typeIndex, int lod, in ModelInstance instance, bool still,
            float colourFade)
        {
            switch (kind)
            {
                case BallKind.Rock:
                    //Colourless and never loaded in the cannon, so neither the type nor the still plane
                    //reaches it. See BallRenderSet.DrawRocks.
                    _set.StoreRock(lod, instance);
                    break;

                case BallKind.Transparent:
                    //Clear glass, and no colour reaches it either — it has none yet, which is the kind (#325).
                    _set.StoreHollow(lod, instance);
                    break;

                case BallKind.Bomb:
                    //Colourless a third time (#326), and never loaded in the cannon: what a bomb says it says
                    //with its own beat, which is a set of renderer uniforms. See BallRenderSet.DrawBombs.
                    _set.StoreBomb(lod, instance);
                    break;

                default:
                    //An ordinary ball, and nearly always only that. Mid-crossing (#325) it is BOTH: the ball is
                    //logically its new colour already, so it goes into that colour's bucket, and a ghost of the
                    //glass it was goes into the hollow region — the two carrying OPPOSITE SIGNS of the dissolve,
                    //which is what makes them one cross-fade rather than two half-drawn balls.
                    //
                    //The signs are the whole trick and they are already in the shader's contract: +d keeps the
                    //pixels where the dither noise EXCEEDS d, -d keeps exactly the others, so at any d the two
                    //draws partition the ball's pixels between them with no overlap, no gap and no transparency
                    //sorting. It is the magazine's transmute cross-fade (the one thing the channel was built
                    //for), pointed at a ball in the cluster instead of one in the bore — and a cluster ball's
                    //dissolve is otherwise always zero, so the channel is free to carry it.
                    if (colourFade > 0f)
                    {
                        _set.Store(typeIndex, lod, instance.WithDissolve(-colourFade), still);
                        _set.StoreHollow(lod, instance.WithDissolve(colourFade));
                        break;
                    }

                    _set.Store(typeIndex, lod, instance, still);
                    break;
            }
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
        /// <param name="colourFade">How far a freshly coloured transparent ball is through its crossing, 0 for
        /// every other ball there has ever been (#325) — see <see cref="Route"/> for what the two ends of it
        /// are drawn as.</param>
        public void AddOriented(BallType type, Vector3 position, in Quaternion orientation, Vector4 occlusion,
            float ripple = 0f, BallKind kind = BallKind.Normal, float colourFade = 0f)
        {
            int typeIndex = (int)type - 1;
            if (typeIndex < 0 || typeIndex >= BallRenderSet.TYPE_COUNT) return;

            Matrix world = Matrix.CreateFromQuaternion(orientation);

            world.M41 = position.X;
            world.M42 = position.Y;
            world.M43 = position.Z;

            Route(kind, typeIndex, _set.LodFor(Vector3.DistanceSquared(position, _eye)),
                new ModelInstance(world, occlusion, 0f, ripple), still: false, colourFade);
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

            //Burial first, in one pass over the grid, exactly as ClusterCollector does over the physics
            //array (#303): the per-ball loop below then reads each cell's depth out of the finished field
            AirDepthField airDepth = _set.MapAirDepth;
            airDepth.Compute(balls, size);

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
                            BallRenderSet.OcclusionTarget(occluders, occluderDirectionSum,
                                airDepth.DepthAt(x, z, level)), kind: ball.Kind);

                        added++;
                    }

            return added;
        }
    }
}
