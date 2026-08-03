using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The translucent glass plate a hanging ball cluster is suspended from, shared by the Testbed and the
    /// Game (it existed twice, line for line, until #75): a procedurally generated <see cref="BoxMesh"/>
    /// drawn through one <see cref="InstancedModelRenderer"/>, rebuilt at the exact footprint of the loaded
    /// field — no model asset, and no non-uniform scaling of a fixed mesh.
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
    /// the alpha, the thickness, the footprint margin and the clearance were identical in both copies, so
    /// they are this component's own constants. The one figure that genuinely differs — how high the plate
    /// hangs — is not a look value at all: the Testbed derives it from the loaded map's depth while the Game
    /// pins its field's top level to a fixed world height, so <see cref="CentreYAbove"/> takes that base from
    /// the caller instead of computing it from a level count.
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
        public const float GLASS_ALPHA = 0.4f;

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

        private readonly GraphicsDevice _device;
        private readonly Effect _instancingEffect;

        private BoxMesh _mesh;

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
        /// enrols it in its own sky-lighting pass, draws it in its own frame sequence (the draw order is a
        /// per-executable decision and does not move in here), and reaches through it for
        /// <see cref="InstancedModelRenderer.EmissiveTint"/> — which is how the Game flashes the glass on the
        /// frame the ceiling steps down.
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
        /// depth); the Game pins its field's top level to a fixed world height whatever its depth, because
        /// otherwise a map with more empty growth levels would hang that much higher than the camera frames.
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
        public void Fit(float stageSizeX, float stageSizeZ)
        {
            _mesh?.Dispose();
            Renderer?.Dispose();

            _mesh = new BoxMesh(_device, FootprintFor(stageSizeX), THICKNESS, FootprintFor(stageSizeZ));
            Renderer = new InstancedModelRenderer(_device, _mesh, GLASS_COLOR, _instancingEffect, GLASS_ALPHA);
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
