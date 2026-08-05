using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// The gun's <b>hardware</b>: the procedural barrel and the instanced renderer that draws it, with every
    /// figure the tube is cut to. It stood in both executables — the Testbed's <c>LoadContent</c> and the Game's
    /// <c>BS3DGame.LoadContent</c> — value-for-value identical down to the segment count and the steel's colour,
    /// until #76.
    /// <para>
    /// The barrel has to stay a <b>tube</b> with only a slit along the top, or it reads as an open trough. The
    /// slit is there for the <b>magazine</b>: the queue of loaded balls nests in the bore and only a strip of
    /// each shows through, which is enough to read its colour — and you cannot aim a shot whose colour you
    /// cannot see. So the slot's width is not a look, it is how much of the queue reads, and it has to keep
    /// reading from a camera that is not straight above the barrel.
    /// </para>
    /// <para>
    /// <b>It owns no content.</b> The instancing <see cref="Effect"/> is handed in and is emphatically not
    /// disposed here: it is the shared <c>InstancedModel.fx</c>, one copy per executable, loaded through the
    /// content manager that owns its lifetime and shared with the balls, the city, the island and the ceiling.
    /// Disposing it here would take all of them down with the gun.
    /// </para>
    /// <para>
    /// <b>What deliberately stayed with the callers.</b> The <i>magazine</i> — the queue of
    /// <see cref="GameStructure.BallType"/>s, its slide animation, and in the Game its colour transmutation —
    /// belongs to whichever loop is playing the game, and the two are genuinely different loops; this type is
    /// told only how many slots there are and how far apart, because that is all the tube's length is. The
    /// <i>pose</i> is <see cref="GameObjects.Cannon"/>'s, so what to pass <see cref="Draw"/> comes from there
    /// and not from here. And enrolling <see cref="Renderer"/> in the sky's light rig is the caller's too, for
    /// the reason <c>Prazsky.Core.Render.SkyLightRig</c> gives at length: which renderers take part is each
    /// executable's own list with its own reasons.
    /// </para>
    /// </summary>
    public sealed class CannonRig : IDisposable
    {
        /// <summary>
        /// Inner radius of the tube. A little over the ball radius, so a ball nests <b>entirely inside</b> the
        /// bore with its centre on the axis and nothing protruding — which is what makes the slot the only thing
        /// that shows the queue at all.
        /// </summary>
        public const float BORE_RADIUS = 0.6f;

        /// <summary>Radial thickness of the steel; the outer radius is this plus <see cref="BORE_RADIUS"/>.
        /// Thin, but the slot's cut edges are closed by rim faces so it does not read as paper-thin.</summary>
        public const float WALL_THICKNESS = 0.14f;

        /// <summary>
        /// Half-width of the top slot, in radians from straight up — so a ~57° window. Sized by what it has to
        /// show rather than by looks: because the balls sit entirely inside the bore (centres on the axis, radius
        /// <see cref="Constants.HALF"/> against a bore of <see cref="BORE_RADIUS"/>), the near rim occludes them
        /// as soon as the eye is off the slot's own axis. At the original 0.36 (a 41° slot) the loaded queue was
        /// invisible from anywhere but straight above, which is nowhere the game's camera ever stands.
        /// </summary>
        public const float SLOT_HALF_ANGLE = 0.5f;

        //Angular segments across the solid wall arc. Enough that the tube's silhouette reads as round at the
        //size it is drawn - it is a foreground prop a few units from the lens, and there is exactly one of it.
        private const int WALL_SEGMENTS = 24;

        //The ball radius, which is what closes the tube flush with the outer surface of the balls at either end:
        //the muzzle lip sits this far ahead of the head-of-queue ball's centre and the breech this far behind the
        //last one's, so the loaded queue is exactly enclosed with no lip cutting through a ball.
        private const float BALL_RADIUS = Constants.HALF;

        //Steel grey, in sRGB - the shader linearizes it, like every other material colour in this project.
        private static readonly Vector3 STEEL_COLOR = new(0.42f, 0.44f, 0.48f);

        //Metal takes a good deal of the sky as reflection. There is no UV set on the mesh and so no detail
        //texture: the barrel is plain steel whose whole sheen is this specular ambient reflecting the dome.
        private const float SPECULAR_AMBIENT_STRENGTH = 0.5f;

        #region The carriage's figures

        //The carriage: the frame the barrel's trunnions ride in and the wheels it walks on (W/S — see
        //Cannon.Advance). Everything below is in the carriage's own frame, relative to the trunnions, and
        //the one figure that reaches outside it is the wheels' ground line — which is why TRUNNION_HEIGHT
        //below is defined off these figures rather than the other way round: the gun has no collider and
        //nothing else to stand on (the stone below dishes away, and on a big map the orbit leaves the island
        //entirely), so grazing the one plane the eye takes for the ground is what makes the float read as
        //standing.
        private const float WHEEL_RADIUS = 1.15f;   //outer radius, rim tube included
        private const float WHEEL_TUBE = 0.11f;     //the felloe's half-thickness
        private const float HUB_RADIUS = 0.17f;
        private const float HUB_HALF_WIDTH = 0.12f;
        private const int WHEEL_SPOKES = 8;
        private const float WHEEL_TRACK = 1.5f;     //each wheel's centre off the barrel's axis

        private const float AXLE_DROP = 0.95f;      //the axle line below the trunnions
        private const float AXLE_RADIUS = 0.13f;

        private const float CHEEK_INNER_X = BORE_RADIUS + WALL_THICKNESS + 0.04f; //hugging the tube, never clipping it
        private const float CHEEK_THICKNESS = 0.14f;
        private const float CHEEK_TOP_Y = 0.2f;     //a little above the trunnion axis the plates hold
        private const float CHEEK_HALF_LENGTH = 0.85f;

        //Where the split trail's +X leg ends (mirrored for the other): outward past the wheel, down to just
        //over the arris plane, and back behind the breech's recoil sweep — the split exists because the
        //breech dips exactly where one central beam would stand (see GunCarriageMesh).
        private static readonly Vector3 TRAIL_END = new(1.55f, -1.9f, 3.05f);

        /// <summary>
        /// Where the trunnions belong for the wheels to touch ground: the carriage hangs
        /// <see cref="AXLE_DROP"/> + <see cref="WHEEL_RADIUS"/> below them, so their height is the island's
        /// arris plane (<c>ArenaIsland.TOP_Y</c>) plus that stack. Both executables construct their
        /// <c>Cannon</c> with this, which makes the wheels' graze true <b>by construction</b> — it used to be
        /// a −6.4 literal in each, tied to these figures only by a comment — and resizing the wheels now
        /// moves the gun, exactly as it would a real carriage.
        /// </summary>
        public const float TRUNNION_HEIGHT = ArenaIsland.TOP_Y + AXLE_DROP + WHEEL_RADIUS;

        //The woodwork and the ironwork: a warm matte wood for the wheels, a darker iron than the barrel's
        //steel for the frame — the barrel keeps its sheen, the undercarriage barely reflects.
        private static readonly Vector3 WHEEL_COLOR = new(0.40f, 0.29f, 0.19f);
        private static readonly Vector3 FRAME_COLOR = new(0.30f, 0.30f, 0.33f);
        private const float WHEEL_SPECULAR_AMBIENT = 0.08f;
        private const float FRAME_SPECULAR_AMBIENT = 0.22f;

        #endregion

        private CannonMesh _mesh;
        private InstancedModelRenderer _renderer;

        private GunCarriageMesh _carriageMesh;
        private InstancedModelRenderer _carriageRenderer;
        private GunWheelMesh _wheelMesh;
        private InstancedModelRenderer _wheelRenderer;

        //The two wheels of one draw, refilled per frame — the array is reused, never reallocated
        private readonly ModelInstance[] _wheelInstances = new ModelInstance[2];

        /// <param name="instancingEffect">The shared <c>InstancedModel.fx</c>. Handed in, never disposed — see
        /// the class remarks.</param>
        /// <param name="magazineSize">How many balls the barrel is loaded with. The queue is the caller's; this
        /// is only how long the tube has to be to hold it.</param>
        /// <param name="magazineSpacing">How far apart the loaded balls sit along the bore, in world units (a
        /// ball diameter, as both executables space them).</param>
        public CannonRig(GraphicsDevice graphicsDevice, Effect instancingEffect, int magazineSize, float magazineSpacing)
        {
            PivotToFrontBall = (magazineSize - 1) * magazineSpacing * Constants.HALF;

            //Modelled about the TRUNNIONS and not the muzzle: the head-of-queue ball sits PivotToFrontBall ahead
            //of the local origin and the queue recedes behind it, which puts the tube's own midpoint at the
            //origin - so the draw matrix's translation is the pivot and aiming turns the barrel about it (see
            //Cannon.BarrelWorld). Pivoting about the muzzle instead swings the whole barrel and its loaded queue
            //from the tip, which is the one place a gun does not pivot.
            float muzzleZ = -(PivotToFrontBall + BALL_RADIUS);
            float breechZ = (magazineSize - 1) * magazineSpacing - PivotToFrontBall + BALL_RADIUS;

            _mesh = new CannonMesh(graphicsDevice, BORE_RADIUS, WALL_THICKNESS, muzzleZ, breechZ, SLOT_HALF_ANGLE,
                WALL_SEGMENTS);

            _renderer = new InstancedModelRenderer(graphicsDevice, _mesh, STEEL_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = SPECULAR_AMBIENT_STRENGTH,

                //The ground darkens the barrel's underside just as it darkens the ball bellies, and the gun
                //stands on the island, so the height it is darkened against is the island's top face
                GroundHeight = ArenaIsland.TOP_Y
            };

            //The carriage under the tube: the frame the trunnions ride in and the pair of wheels the advance
            //walk (W/S) rolls. Sized off the barrel's own figures, so a retuned bore moves the cheeks with it.
            _carriageMesh = new GunCarriageMesh(graphicsDevice, CHEEK_INNER_X, CHEEK_THICKNESS, CHEEK_TOP_Y,
                AXLE_DROP, CHEEK_HALF_LENGTH, AXLE_RADIUS, WHEEL_TRACK + HUB_HALF_WIDTH * 0.5f, TRAIL_END);

            _carriageRenderer = new InstancedModelRenderer(graphicsDevice, _carriageMesh, FRAME_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = FRAME_SPECULAR_AMBIENT,
                GroundHeight = ArenaIsland.TOP_Y
            };

            _wheelMesh = new GunWheelMesh(graphicsDevice, WHEEL_RADIUS, WHEEL_TUBE, HUB_RADIUS, HUB_HALF_WIDTH,
                WHEEL_SPOKES);

            _wheelRenderer = new InstancedModelRenderer(graphicsDevice, _wheelMesh, WHEEL_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = WHEEL_SPECULAR_AMBIENT,
                GroundHeight = ArenaIsland.TOP_Y
            };
        }

        /// <summary>
        /// How far ahead of the trunnions the head-of-queue ball sits: half the loaded queue's length, since the
        /// tube is laid out about its own midpoint. The one figure of the hardware the pose needs — pass it to
        /// <see cref="GameObjects.Cannon.MuzzlePosition"/> — and derived here rather than spelled out by each
        /// caller, so the barrel that was built and the muzzle that is fired from cannot disagree.
        /// </summary>
        public float PivotToFrontBall { get; }

        /// <summary>
        /// The renderer, exposed for the one thing <see cref="Draw"/> cannot do for a caller: enrolling it in
        /// that executable's sky light rig. Nothing else needs it.
        /// </summary>
        public InstancedModelRenderer Renderer => _renderer;

        /// <summary>The carriage frame's renderer, for the same sky-lighting enrolment as <see cref="Renderer"/>.</summary>
        public InstancedModelRenderer CarriageRenderer => _carriageRenderer;

        /// <summary>The wheels' renderer, for the same sky-lighting enrolment as <see cref="Renderer"/>.</summary>
        public InstancedModelRenderer WheelRenderer => _wheelRenderer;

        /// <summary>Draws the barrel, as a single instance so it takes the same hemisphere ambient, positional
        /// key light and per-pixel shading as the instanced balls around it.</summary>
        /// <param name="world">This frame's pose, from <see cref="GameObjects.Cannon.BarrelWorld"/>.</param>
        public void Draw(ICamera camera, Matrix world, BasicEffectParams effectParams) =>
            _renderer.Draw(camera, world, effectParams);

        /// <summary>
        /// Draws the carriage under the barrel: the frame as one instance, the two wheels as one two-instance
        /// draw, spun by how much ground the advance walk has covered. Drawn with the barrel, wherever the
        /// caller draws that.
        /// </summary>
        /// <param name="carriageWorld">This frame's pose, from <see cref="GameObjects.Cannon.CarriageWorld"/> —
        /// level, yawed with the aim, and deliberately without the recoil the barrel takes: the tube slides in
        /// the cradle, the carriage holds its ground.</param>
        /// <param name="advanceTravel"><see cref="GameObjects.Cannon.AdvanceTravel"/>: signed ground covered,
        /// positive toward the field. The wheels' angle is <c>travel / radius</c>, so they turn exactly as fast
        /// as the ground passes and slow into the walk's rubber ends with it.</param>
        public void DrawCarriage(ICamera camera, Matrix carriageWorld, float advanceTravel,
            BasicEffectParams effectParams)
        {
            _carriageRenderer.Draw(camera, carriageWorld, effectParams);

            //Walking toward the field is motion along local -Z; rolling with it takes the wheel's top the same
            //way, which about the +X axle is a negative rotation. Wrapped per circumference before the divide,
            //so a long session's travel cannot walk the angle out into float noise.
            float roll = -(advanceTravel % (MathHelper.TwoPi * WHEEL_RADIUS)) / WHEEL_RADIUS;

            //One rotation built once; each wheel writes its own place on the axle into the fourth row and the
            //pair composes with the carriage pose — the two genuine matrix multiplies this prop costs per frame
            Matrix wheel = Matrix.CreateRotationX(roll);
            wheel.M42 = -AXLE_DROP;

            wheel.M41 = -WHEEL_TRACK;
            _wheelInstances[0] = new ModelInstance(wheel * carriageWorld, new Vector4(0f, 0f, 0f, 1f));

            wheel.M41 = WHEEL_TRACK;
            _wheelInstances[1] = new ModelInstance(wheel * carriageWorld, new Vector4(0f, 0f, 0f, 1f));

            _wheelRenderer.Draw(camera, _wheelInstances, 2, effectParams);
        }

        /// <summary>
        /// Releases the barrel's buffers and the renderer's instance buffer. <b>Not</b> the effect, which the
        /// content manager owns and everything else in the scene shares.
        /// </summary>
        public void Dispose()
        {
            _mesh?.Dispose();
            _mesh = null;
            _renderer?.Dispose();
            _renderer = null;

            _carriageMesh?.Dispose();
            _carriageMesh = null;
            _carriageRenderer?.Dispose();
            _carriageRenderer = null;

            _wheelMesh?.Dispose();
            _wheelMesh = null;
            _wheelRenderer?.Dispose();
            _wheelRenderer = null;
        }
    }
}
