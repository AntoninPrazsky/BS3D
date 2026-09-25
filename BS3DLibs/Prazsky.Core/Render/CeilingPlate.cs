using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The translucent glass plate a hanging ball cluster is suspended from, shared by the Testbed and the
    /// Game (it existed twice, line for line, until #75): a procedurally generated slab — a <see cref="BoxMesh"/>
    /// until #541, a <see cref="CutSlabMesh"/> since — drawn through one <see cref="InstancedModelRenderer"/>,
    /// rebuilt at the exact footprint of the loaded field — no model asset, and no non-uniform scaling of a fixed
    /// mesh.
    /// <para>
    /// <b>It is cut as crystal, and in the Game it bends what is behind it (#541).</b> The Game hands the renderer
    /// a copy of the frame drawn so far (<see cref="InstancedModelRenderer.GlassBehind"/>), and the plate's
    /// technique traces each pixel's ray through the slab — in at the face it is drawn on, out through the face it
    /// reaches — and shows that copy where the ray leaves, under the plate's own lit surface. A flat face bends
    /// nothing (a parallel slab only offsets), so the bend lives in the cut: the edge ground in facets all round
    /// (<see cref="EDGE_PROFILE"/>, the corners cut round to <see cref="CORNER_RADIUS"/>), and across the top face
    /// a border of fine flutes (<see cref="RIM_BAND"/>, <see cref="FLUTE_PERIOD"/>) round a field of diamond facets
    /// (<see cref="CUT_PERIOD"/>, <see cref="CUT_SLOPE"/>) that the shader cuts rather than the mesh. The underside stays flat, because it is what the cluster hangs from. The Testbed
    /// never sets the copy, so it draws the cut plate as the plain translucent pane it always was.
    /// </para>
    /// <para>
    /// <b>The drawn plate and the physics are two objects that must be given the same figures, and nothing
    /// here can keep them in step.</b> This class owns the drawn box only; the kinematic Bepu body the
    /// cluster actually hangs from belongs to the caller — the Testbed's own <c>_ceiling</c>, and in the Game
    /// the session's (<c>GameplayScreen._ceiling</c>), because the cluster hangs off it while the glass is lit
    /// with the rest of the host's scene. So the caller sizes its collidable from <see cref="FootprintFor"/>
    /// and <see cref="THICKNESS"/>, places it at <see cref="CentreYAbove"/> — the same figures it passes to
    /// <see cref="Fit"/> — and draws the plate from that body's own pose. Then the glass and the collidable
    /// cannot disagree.
    /// </para>
        /// <para>
        /// The plate has no per-executable look values, which is why nothing here is <c>required</c> (contrast
        /// <see cref="PostProcessPipeline"/>, whose every look value is a per-executable decision): the colour,
        /// the thickness, the footprint margin and the clearance were identical in both copies, so they are
        /// this component's own constants. The one look value that has since grown a second reader is the
        /// <b>alpha</b> — the menu's preview plate fits at a stronger one (#249, see <see cref="Fit"/>) — and
        /// the one figure that genuinely differs, how high the plate hangs, is not a look value at all: the
        /// Testbed derives it from the loaded map's depth while the Game solves its field's top against the
        /// death line per level, so <see cref="CentreYAbove"/> takes that base from the caller instead of
        /// computing it from a level count.
        /// </para>
    /// <para>
    /// The plate owns no content: the caller loads <c>Shaders/InstancedModel</c> through its own
    /// <c>ContentManager</c> (the libraries have no content pipeline — see CLAUDE.md) and hands the compiled
    /// effect in. It looks up no effect parameter by name either, and has no per-frame path of its own to keep
    /// clean: <see cref="Fit"/> allocates a mesh and a renderer and runs once per level load, while the
    /// drawing — and everything per-frame about it — goes through <see cref="Renderer"/>.
    /// </para>
    /// <para>
    /// Drawing and its order stay with the caller for that reason. The glass is translucent, so it comes after
    /// everything that should be seen through it, once that is in the depth buffer and in the frame, under
    /// <see cref="BlendState.AlphaBlend"/>. For the same reason it deliberately takes no part in the balls'
    /// ambient occlusion: translucent glass lets the light through, and what keeps a released ball from
    /// flashing brighter is the occlusion easing, not the plate.
    /// </para>
    /// </summary>
    public sealed class CeilingPlate : IDisposable
    {
        /// <summary>Diffuse colour of the glass: a cold pale blue, the same figure in both executables.</summary>
        public static readonly Vector3 GLASS_COLOR = new(0.55f, 0.75f, 0.85f);

        /// <summary>
        /// Material alpha of the glass. Well below one, so the plate is translucent and the cluster hanging
        /// under it stays readable through it. How opaque the plate reads is this figure and nothing else —
        /// a comment elsewhere that restated it as a percentage was left behind by a retune and lied for it.
        /// </summary>
        public const float GLASS_ALPHA = 0.48f;

        /// <summary>
        /// Full size of the slab along Y. The caller's collidable box has to be given the same figure, and
        /// <see cref="TopFaceY"/> derives the upper face from it.
        /// </summary>
        public const float THICKNESS = 1f;

        /// <summary>
        /// Odd levels of the ball grid are shifted by +0.5 in X and Z and a ball's radius is another 0.5, so a
        /// field's worth of balls is one unit wider than its cell count; the plate covers it with that margin.
        /// <see cref="FootprintFor"/> is the one place it is applied — a collidable box, a floor net or a
        /// camera fit that needs the same footprint asks there instead of writing the <c>+ 1</c> again.
        /// </summary>
        public const float FOOTPRINT_MARGIN = 1f;

        /// <summary>
        /// How far the plate's centre hovers above the field's topmost level — see <see cref="CentreYAbove"/>,
        /// which is the only thing that applies it.
        /// <para>
        /// Note the cluster does not settle on its lattice: the ceiling <c>BallSocket</c> anchors a ball's top
        /// (local +0.5) to the plate's bottom face (local −0.5), so the top level comes to rest one unit under
        /// the plate's centre and the whole rigid structure with it. Half the clearance this constant looks
        /// like it buys is spent that way, and the top balls end up close under the glass. It is the Testbed's
        /// original behaviour, kept in the Game for parity; this is the figure to change if the cluster should
        /// ever hang exactly on its lattice.
        /// </para>
        /// </summary>
        public const float CLEARANCE = 2f;

        /// <summary>
        /// How far the underside's edge is ground back from the footprint (#541) — the face the play camera sees,
        /// since it looks up at the plate from under it: a run of prisms along the pane's edge, where the bend reads
        /// strongest. The grind itself is <see cref="EDGE_PROFILE"/>'s first three points.
        /// <para>
        /// Bounded by the cluster it hangs: the flat underside is the footprint less this on every side, and the
        /// top level's outermost balls touch the plate half a unit in from the footprint's edge
        /// (<see cref="FOOTPRINT_MARGIN"/>), so the grind must stay well inside that half or those balls would hang
        /// from a facet rather than from the glass. At a corner the underside's rim crosses the diagonal
        /// <see cref="CORNER_RADIUS"/> − (<see cref="CORNER_RADIUS"/> − this) / √2 in from both sides — 0.38, where the
        /// corner ball touches 0.5 in from both, and where the single 45° cut before it crossed at 0.38 as well.
        /// </para>
        /// </summary>
        public const float BEVEL = 0.25f;

        /// <summary>
        /// How far the top edge is ground back from the footprint (#541): the crown, wider than the underside's
        /// <see cref="BEVEL"/> because nothing hangs from the top, and ground in three facets that flatten as they
        /// climb — steep off the side, a middle one near 50°, a shallow one into the top face — the way a gem's
        /// crown is. The grind itself is <see cref="EDGE_PROFILE"/>'s last four points.
        /// </summary>
        public const float CROWN = 0.4f;

        /// <summary>
        /// The slab's edge in section, from the underside's rim up to the top's (#541) — each point how far in from
        /// the footprint's outline, and how high about the slab's centre (<see cref="THICKNESS"/> being 1, the faces
        /// are at ±0.5). Two facets under (<see cref="BEVEL"/>), a straight side band, three facets over
        /// (<see cref="CROWN"/>): six bands of facets round the plate, where the first cut had three, each one 45°.
        /// The owner's word on that first cut was that the edges could be cut more; this is that — more, narrower
        /// facets, each a prism of its own, so the rim throws the scene back in strips rather than in one. The
        /// refracting technique reads the crown's four points as well (<see cref="Fit"/>), so a ray leaving the top
        /// near the rim leaves through the facet it would in the glass.
        /// </summary>
        public static readonly Vector2[] EDGE_PROFILE =
        {
            new(BEVEL, -0.5f),
            new(0.09f, -0.41f),
            new(0f, -0.25f),
            new(0f, 0.05f),
            new(0.05f, 0.22f),
            new(0.17f, 0.38f),
            new(CROWN, 0.5f)
        };

        /// <summary>
        /// The radius each vertical corner is cut round to (#541): <see cref="CORNER_FACETS"/> flat cuts, all tangent
        /// to one circle this far in from both sides, so every band of the edge turns the corner at its own angle.
        /// It was one 45° cut 0.4 back along both sides. The same bound as <see cref="BEVEL"/>'s, taken at the
        /// corner (see there).
        /// </summary>
        public const float CORNER_RADIUS = 0.7f;

        /// <summary>How many flat cuts round each corner (#541), at equal angles between the two sides.</summary>
        public const int CORNER_FACETS = 3;

        /// <summary>
        /// The width of the border of flutes the shader cuts round the top face inside the crown (#541), in world
        /// units — about a ball. On a pane too narrow for that and a field besides, the border takes half of what the
        /// crown leaves instead (<see cref="Fit"/>), so a small pane still keeps diamonds in its middle.
        /// </summary>
        public const float RIM_BAND = 1f;

        /// <summary>
        /// The spacing of those flutes along the outline, in world units (#541): a quarter of a ball, so the border
        /// reads as a finer cut than the field's diamonds (<see cref="CUT_PERIOD"/>) — a second frequency, which is
        /// what a cut-glass tray's border is against its middle.
        /// </summary>
        public const float FLUTE_PERIOD = 0.25f;

        /// <summary>
        /// How steep the flutes' facets are, as the tangent of their tilt along the outline (#541) — 0.35 is about
        /// 19°, steeper than the diamonds' <see cref="CUT_SLOPE"/>: a finer cut has to throw harder to be seen.
        /// </summary>
        public const float FLUTE_SLOPE = 0.35f;

        /// <summary>
        /// How far the whole border leans down towards the rim, as a tangent (#541): a shallow prism the width of the
        /// border, which moves the image through it against the field's.
        /// </summary>
        public const float RIM_LEAN = 0.08f;

        /// <summary>
        /// The spacing of the diamond cut on the top face's field, in world units (#541): the pyramids the shader cuts
        /// are this far apart along each diagonal of the pane. A ball is one unit across, so a cut is about two balls
        /// wide — large enough to read as cut glass from the play camera, small enough that a pane over a small
        /// field still carries several.
        /// </summary>
        public const float CUT_PERIOD = 2f;

        /// <summary>
        /// How steep the diamond cut's facets are, as the tangent of their tilt (#541) — 0.18 is about 10°. Through
        /// glass of index 1.5 a facet that steep turns the ray leaving it by about 5°, which is a bend the eye reads
        /// in the sky behind the pane without the cluster's own glass under it looking broken.
        /// </summary>
        public const float CUT_SLOPE = 0.18f;

        private readonly GraphicsDevice _device;
        private readonly Effect _instancingEffect;

        private CutSlabMesh _mesh;

        /// <summary>
        /// Nothing is built here: the plate's footprint is the loaded field's, so there is no mesh and no
        /// renderer until the first <see cref="Fit"/>.
        /// </summary>
        /// <param name="device">Graphics device the mesh and its instance buffer live on.</param>
        /// <param name="instancingEffect">The shared instancing effect (<c>Shaders/InstancedModel.fx</c>),
        /// compiled by the caller's content pipeline and handed in — see the class remarks on content.</param>
        public CeilingPlate(GraphicsDevice device, Effect instancingEffect)
        {
            _device = device;
            _instancingEffect = instancingEffect;
        }

        /// <summary>
        /// The plate's renderer, exposed because the light rig is the scene's and not the plate's: the caller
        /// lights it in its own sky-lighting pass (through <c>SkyLightRig.ApplyToGlass</c> — the plate stands
        /// against the sky, so its directional lights follow the sky's own brightness, #156), draws it in its
        /// own frame sequence (the draw order is a per-executable decision and does not move in here), and
        /// reaches through it for <see cref="InstancedModelRenderer.EmissiveTint"/> — which is how the Game
        /// flashes the glass on the frame the ceiling steps down.
        /// <para>
        /// <b>Null until the first <see cref="Fit"/></b>, and again for the moment inside a level load when
        /// the old one has gone: a footprint that comes off the loaded field cannot be known before one is
        /// loaded. Both callers' enrolment lists already tolerate the null and have to keep doing so.
        /// </para>
        /// </summary>
        public InstancedModelRenderer Renderer { get; private set; }

        /// <summary>
        /// Footprint the plate covers a field of the given cell count with, <see cref="FOOTPRINT_MARGIN"/>
        /// included: what <see cref="Fit"/> builds, and what the collidable box, a floor net and a camera fit
        /// have to measure against. Pure, so a caller can ask before the plate is fitted and cannot read a
        /// footprint left over from the previous level.
        /// </summary>
        public static float FootprintFor(float stageSize) => stageSize + FOOTPRINT_MARGIN;

        /// <summary>
        /// Centre Y of a plate hovering <see cref="CLEARANCE"/> over a field whose topmost level of balls
        /// sits at <paramref name="fieldTopY"/> — the height for the drawn box and for the collidable alike.
        /// <para>
        /// The base is the caller's on purpose, and the clearance is all that is shared. The Testbed takes it
        /// from the loaded map (a level's height is its index over √2, so its plate rises with the map's
        /// depth); the Game hangs its field's top at a fixed world height and raises only a field deep enough
        /// that its bottom level would otherwise start past the death line (its <c>FIELD_FLOOR_MARGIN</c>).
        /// </para>
        /// </summary>
        public static float CentreYAbove(float fieldTopY) => fieldTopY + CLEARANCE;

        /// <summary>
        /// World Y of the slab's upper face, given its centre Y — what a camera fit frames against, the plate
        /// being the highest thing over the field.
        /// </summary>
        public static float TopFaceY(float centreY) => centreY + THICKNESS * Constants.HALF;

        /// <summary>
        /// Disposes the previous mesh and renderer and builds both again at the loaded field's footprint
        /// (<see cref="FootprintFor"/> of each cell count, <see cref="THICKNESS"/> thick). This is what the
        /// type exists for: the plate's size is runtime data, so a level load remakes it rather than scaling
        /// one fixed mesh.
        /// <para>
        /// <b>The caller must re-run its own sky lighting straight after this.</b> The renderer is a new
        /// object, and a new <see cref="InstancedModelRenderer"/> has never been told the dome's palette: its
        /// <see cref="InstancedModelRenderer.SkyColor"/> and <see cref="InstancedModelRenderer.GroundColor"/>
        /// are still white, its three-light rig untinted (the constructor's own
        /// <see cref="InstancedModelRenderer.SetLightTint"/> with two white tints) and
        /// <see cref="InstancedModelRenderer.LinearLightRig"/> false. Until the caller's pass reaches it, the
        /// glass is therefore lit by a white sky through a rig that was never decoded into radiance — which is
        /// visibly not the scene the rest of the frame is in.
        /// </para>
        /// <para>
        /// It changes nothing about the physics, and the body's lifetime stays entirely with the caller: the
        /// Testbed re-poses and re-shapes one body that has to survive across map loads (constraints of a
        /// previously loaded structure may still reference it), while the Game builds a fresh simulation per
        /// level and simply adds a new one. Either way that box takes the same <see cref="FootprintFor"/>,
        /// <see cref="THICKNESS"/> and <see cref="CentreYAbove"/>.
        /// </para>
        /// </summary>
        /// <param name="stageSizeX">Cell count of the field along X, margin not included.</param>
        /// <param name="stageSizeZ">Cell count of the field along Z, margin not included.</param>
        /// <param name="alpha">The glass's opacity. <see cref="GLASS_ALPHA"/> by default, and every played
        /// field takes the default; the one caller that does not is the menu's preview plate (#249), whose
        /// slab has to read as present under a bright sky at a nearly level camera — furniture it can afford
        /// to whisper, a display piece cannot.</param>
        public void Fit(float stageSizeX, float stageSizeZ, float alpha = GLASS_ALPHA)
        {
            _mesh?.Dispose();
            Renderer?.Dispose();

            float sizeX = FootprintFor(stageSizeX), sizeZ = FootprintFor(stageSizeZ);

            float cornerRadius = Math.Min(CORNER_RADIUS, Math.Min(sizeX, sizeZ) * Constants.HALF);
            _mesh = new CutSlabMesh(_device, sizeX, sizeZ, EDGE_PROFILE, cornerRadius, CORNER_FACETS);
            Renderer = new InstancedModelRenderer(_device, _mesh, GLASS_COLOR, _instancingEffect, alpha);

            //The figures the refracting technique traces the slab by (#541), stated on the renderer the mesh was
            //built for so the two cannot disagree; read only while a caller has handed it a frame to bend
            Renderer.GlassHalfExtents = new Vector3(sizeX, THICKNESS, sizeZ) * Constants.HALF;
            Renderer.GlassCutPeriod = CUT_PERIOD;
            Renderer.GlassCutSlope = CUT_SLOPE;
            Renderer.GlassCornerRadius = cornerRadius;
            Renderer.GlassCornerFacets = CORNER_FACETS;

            //The crown: the profile's last four points, as insets and as drops below the top face
            float top = EDGE_PROFILE[^1].Y;
            Vector2 c0 = EDGE_PROFILE[^4], c1 = EDGE_PROFILE[^3], c2 = EDGE_PROFILE[^2], c3 = EDGE_PROFILE[^1];
            Renderer.GlassCrownInset = new Vector4(c0.X, c1.X, c2.X, c3.X);
            Renderer.GlassCrownDrop = new Vector4(top - c0.Y, top - c1.Y, top - c2.Y, top - c3.Y);

            float band = Math.Min(RIM_BAND, (Math.Min(sizeX, sizeZ) * Constants.HALF - CROWN) * Constants.HALF);
            Renderer.GlassRim = new Vector4(Math.Max(band, 0f), FLUTE_PERIOD, FLUTE_SLOPE, RIM_LEAN);
        }

        /// <summary>
        /// The mesh and the renderer are the plate's own; the effect is the caller's content manager's.
        /// <see cref="Renderer"/> is cleared with them, so a torn-down plate reports "no plate" to a
        /// sky-lighting enrolment exactly as it does before the first <see cref="Fit"/>, rather than handing
        /// out a dead renderer.
        /// </summary>
        public void Dispose()
        {
            _mesh?.Dispose();
            _mesh = null;

            Renderer?.Dispose();
            Renderer = null;
        }
    }
}
