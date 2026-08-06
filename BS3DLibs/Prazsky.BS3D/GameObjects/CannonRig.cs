using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// The gun's <b>hardware</b>: the procedural barrel, the pane of glass glazing its loading window and the
    /// instanced renderers that draw them, with every figure the tube is cut to. It stood in both executables —
    /// the Testbed's <c>LoadContent</c> and the Game's <c>BS3DGame.LoadContent</c> — value-for-value identical
    /// down to the segment count and the steel's colour, until #76.
    /// <para>
    /// The barrel has to stay a <b>tube</b> with only a slit along the top, or it reads as an open trough. The
    /// slit is there for the <b>magazine</b>: the queue of loaded balls nests in the bore and only a strip of
    /// each shows through, which is enough to read its colour — and you cannot aim a shot whose colour you
    /// cannot see. So the slot's width is not a look, it is how much of the queue reads, and it has to keep
    /// reading from a camera that is not straight above the barrel. The slit is <b>glazed</b>, with the ball at
    /// the muzzle notched out of the pane: the glass is what tells the player which round fires next, by
    /// covering the four that do not (see "The slot's glazing" below and <see cref="CannonGlassMesh"/>). The
    /// breech — the end the player's camera looks at — is <b>closed</b>: a dome carrying a cascabel knob,
    /// hiding the chamber the freshest round waits out the post-shot glide in (<see cref="CHAMBER_DEPTH"/>),
    /// where an open hole used to mirror the muzzle's.
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

        #region The slot's glazing

        //The window is GLAZED: a pane of deep blue glass fills it, with the head-of-queue ball notched out of
        //the pane's front edge, so the round about to fire is open to the air while the four behind it read
        //through glass (CannonGlassMesh carries the geometry and the reasons). It is the gun saying which ball
        //fires next by covering every ball but that one - no mark on the HUD, and nothing that has to be kept
        //in step with the queue: the notch is cut at the front slot, and the queue glides through it.
        //
        //The glass's COLOUR and ALPHA were CeilingPlate's, by reference, until the pane was looked at from the
        //vantage the game plays from and turned out not to READ: a pale colour at a low alpha subtracts about
        //as much light as it adds back, so the four covered rounds came out very slightly hazed and nothing
        //more. A window nobody notices says nothing about which round fires next, which is the only reason
        //this pane exists. So both are the pane's own now, joining the sky reflection that always was - and
        //they part from the plate's in opposite directions at once, the colour down and the alpha up, which
        //is why one pair of constants could never have served both. See GLASS_COLOR and GLASS_ALPHA below.
        //
        //The plate and the pane are still the same MATERIAL; what differs is the duty. Nothing is read through
        //the plate, so it may be a pale tint that mostly announces itself by its own sheen. Everything about
        //this pane is read through it, so it has to work by TRANSMISSION: what it takes away is the whole
        //signal, and what it adds back is what destroys it.
        //
        //Which is the trap in this material, and the reason a darker colour and a higher alpha go together
        //rather than either alone. The shader decodes the material colour PREMULTIPLIED by alpha
        //(SrgbToLinear(DiffuseColor.rgb), and InstancedModelRenderer.Draw hands it colour x alpha), then
        //AlphaBlend adds it over the frame times (1 - alpha). So the pane is one constant veil plus a fraction
        //of what is behind it. The veil is an ADD, and an add is what flattens hue: a pale veil over a red
        //ball puts as much blue and green into it as red, and the queue's colours are exactly what the next
        //shots are planned on (Magazine's remarks: you cannot aim a shot whose colour you cannot see). The
        //multiply is the safe half - it dims every channel in proportion and leaves the hue alone. Hence a
        //DARK glass at a HIGHER alpha: the darkening does the work, the veil only tints it cold, and a ball
        //under this pane is a dimmer ball of its own colour rather than a bluer ball of no colour.

        //The pane's own diffuse colour: a deep, cold blue where the plate's is a pale one
        //(CeilingPlate.GLASS_COLOR). Dark on purpose and not for taste - it is the veil the paragraph above
        //is about, so every bit of lightness in it is lightness added to a ball whose colour has to survive
        //being read through it. Blue on purpose too, and far more saturated than the plate's: there is little
        //of it, and what there is has to say "glass" rather than "shadow" on its own.
        //
        //What matters is this colour TIMES the alpha, and each channel of it separately, which is the thing
        //that is easy to get wrong here: the shader is handed the product. Deepening the blue while raising
        //the alpha left the blue channel's product almost exactly where it started (0.85 x 0.4 against
        //0.50 x 0.62), so the pane turned markedly bluer and barely darker - measured on a glazed ball, the
        //blue channel moved by four levels out of 173. Every channel has to come down, the blue included; the
        //saturation is in the RATIO the channels keep, not in leaving one of them high.
        private static readonly Vector3 GLASS_COLOR = new(0.10f, 0.19f, 0.34f);

        //How much of the frame behind the pane it keeps out: well over the plate's 0.4, so a covered round
        //loses about a third of the light it kept before and the round in the notch stands clear of the four
        //behind it. This is the figure that actually does the job, and it is also the one that costs - it is
        //bounded above by the queue staying legible, not by looks, and the balls dimmest to begin with (the
        //dark types) are what bound it: a black round is identified by its white gores alone, and a pane
        //dark enough to swallow those turns its slot from "a black round" into "an empty slot". Retune it
        //against a full magazine and not an empty bore.
        //
        //And retune it knowing WHAT IS ACTUALLY BEHIND IT from the game camera, which is not what the notch
        //suggests. Drawn opaque for a moment as a test, the pane fills the whole slot from that vantage: the
        //barrel hides its own muzzle end, so the open round shows only as the small ellipse of its cap at the
        //very top of the window and everything else in the queue is read THROUGH the glass. So this figure
        //dims the entire queue to buy contrast on one ellipse. That is the trade, and it is why the answer is
        //a pane that is dark and CLEAR rather than one that is merely thick - see GLASS_COLOR above and
        //GLASS_SKY_REFLECTION below, both of which buy the same contrast without spending legibility.
        private const float GLASS_ALPHA = 0.62f;

        //Radial thickness of the pane. Seated on the bore, so its outer face stands at BORE_RADIUS + this,
        //which has to stay under the slimmest steel the window crosses (the chase dip, outer - 0.075, i.e.
        //0.665 at the shipped bore and wall) or the glass proves proud of the tube it is set into.
        private const float GLASS_THICKNESS = 0.05f;

        //The reveal the pane is held off the window's steel by on both cheeks and at the lip the window ends
        //at. Not a look: a glass face flush with a steel one is two coplanar surfaces fighting over the depth
        //buffer, which flickers. It reads as the shadow line of a pane seated in a frame, which is what it is.
        private const float GLASS_SEAT = 0.02f;

        //Steps across the pane, which are also the steps round the notch's ellipse - the oval is the curve the
        //eye reads on this prop, so it gets more of them than the tube's own wall does.
        private const int GLASS_SEGMENTS = 16;

        //How much of the dome the pane reflects, and the figure that has to be held DOWN while the two above
        //are pushed up. Measured rather than tasted - at the game camera the barrel is seen from behind and
        //slightly above, which is a GRAZING angle across this pane, and grazing is exactly where the shader's
        //Fresnel term takes the sky reflection to its maximum (0.04 head-on, but 0.67 at a cosine of 0.08 and
        //a full mirror edge-on). At the full strength every other surface here takes, the pane came out an
        //opaque blue sheet from the one vantage the game actually plays from: the queue's colours were gone,
        //and a window that hides the queue is a window with no reason to exist (Magazine's remarks). The plate
        //has no such duty - nothing is read THROUGH it - which is why it can keep the full reflection and this
        //cannot.
        //
        //Written as a PRODUCT because the product is what the frame sees. The shader multiplies the reflected
        //environment by the material alpha as well (ShadePixel: "* color.a"), so this dial and GLASS_ALPHA are
        //not independent: the alpha raised to darken the pane would have brightened its sky reflection by the
        //very same factor, and at a grazing angle put back exactly the veil the darkening was meant to take
        //away. Stating the product is what stops that happening by the side door.
        //
        //And it is well down on the 0.22 the pane shipped with, which is the second thing the retune found.
        //From the game camera this reflection, not the glass's own colour, is the LARGEST term the pane adds -
        //at that cosine it is worth more than the whole diffuse body of the pane - and being a reflection of
        //the sky it is achromatic and pale. That is what a pane made of it looks like: not glass but HAZE,
        //balls that read slightly out of focus rather than balls read through something. The tint can only
        //show once the pale wash over it is down, so the darkening and this cut are one change and not two.
        private const float GLASS_SKY_REFLECTION = 0.13f;

        private const float GLASS_SPECULAR_AMBIENT = GLASS_SKY_REFLECTION / GLASS_ALPHA;

        #endregion

        /// <summary>
        /// How deep the chamber runs behind the breech face — exactly a ball radius, and deliberately so,
        /// because three lengths are this one figure: the dome's inner cavity (nesting the parked round's back
        /// hemisphere, grazing at the apex), the hood the slot stops short of the breech face by (the parked
        /// round's front half hides under it, flush with the slot's back edge), and how far behind its resting
        /// slot the magazine parks the freshest round while the post-shot glide runs
        /// (<see cref="BorePose.SlotPosition"/> clamps its share of the slide on this). The glide is why the
        /// breech used to be open: the fresh round was dealt a whole slot behind the tube and slid in through
        /// the hole. The dome that closed the hole works only because these three agree, so they are one
        /// constant rather than three.
        /// </summary>
        public const float CHAMBER_DEPTH = BALL_RADIUS;

        //Angular segments across the solid wall arc. Enough that the tube's silhouette reads as round at the
        //size it is drawn - it is a foreground prop a few units from the lens, and there is exactly one of it.
        private const int WALL_SEGMENTS = 24;

        //The ball radius, which is what closes the tube flush with the outer surface of the balls at either end:
        //the muzzle lip sits this far ahead of the head-of-queue ball's centre and the breech face this far
        //behind the last one's, so the loaded queue is exactly enclosed with nothing cutting through a ball —
        //the dome and the chamber it hides lie beyond that face.
        private const float BALL_RADIUS = Constants.HALF;

        //Steel grey, in sRGB - the shader linearizes it, like every other material colour in this project.
        private static readonly Vector3 STEEL_COLOR = new(0.42f, 0.44f, 0.48f);

        //Metal takes a good deal of the sky as reflection. There is no UV set on the mesh and so no detail
        //texture: the barrel is plain steel whose whole sheen is this specular ambient reflecting the dome.
        private const float SPECULAR_AMBIENT_STRENGTH = 0.5f;

        #region The carriage's figures

        //The carriage: the frame the barrel's trunnions ride in and the wheels it walks on (W/S — see
        //Cannon.Advance). Everything below is in the carriage's own frame, relative to the trunnions, and
        //the one figure that reaches outside it is the wheels' ground line — which is why TrunnionHeightAt
        //below is defined off these figures rather than the other way round: the gun has no collider, so
        //where the wheels touch is a drawing fact, and the stone they touch is the island's dished top
        //(ArenaIsland.FloorHeightAt). Past the arris there is nothing to stand on at all (on a big map the
        //orbit leaves the island entirely), and there the stance holds the arris plane — the one plane the
        //eye takes for the ground.
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

        //The trunnion pins the cheeks visibly hold the tube by: buried into the barrel's wall (never its bore)
        //and proud of the plates' outer faces by a boss. They are the CARRIAGE's, not the tube's, and that is
        //load-bearing: a pin is coaxial with the elevation axis, so a fixed one looks identical as the tube
        //elevates — but the Game's recoil slides the tube, and pins riding it would visibly tear along the
        //plates that hold them.
        private const float TRUNNION_RADIUS = 0.17f;
        private const float TRUNNION_INNER_X = BORE_RADIUS + 0.02f;                     //inside the wall at every aim
        private const float TRUNNION_OUTER_X = CHEEK_INNER_X + CHEEK_THICKNESS + 0.06f; //the boss past the plate
        private const float CHEEK_TOP_Y = 0.2f;     //a little above the trunnion axis the plates hold
        private const float CHEEK_HALF_LENGTH = 0.85f;

        //Where the split trail's +X leg ends (mirrored for the other): outward past the wheel, down to just
        //over the wheels' ground plane, and back behind the breech's recoil sweep — the split exists because
        //the breech dips exactly where one central beam would stand (see GunCarriageMesh).
        private static readonly Vector3 TRAIL_END = new(1.55f, -1.9f, 3.05f);

        /// <summary>
        /// Where the trunnions belong for the wheels to touch the ground the gun actually stands over: the
        /// stone at its radius (<see cref="ArenaIsland.FloorHeightAt"/> — the walkable top is a dish, not a
        /// plane) plus the stack the carriage hangs below the pins (<see cref="AXLE_DROP"/> +
        /// <see cref="WHEEL_RADIUS"/>). <c>Cannon</c> re-seats its own Y through this on every move, which
        /// makes the wheels' contact true <b>by construction</b> wherever the walk stands — it used to be a
        /// constant cut to the island's outer arris plane, which read as standing only at the arris itself:
        /// everywhere the dish had fallen away below the wheels the gun visibly floated, and the shipped
        /// small maps stand it near the drain, where the fall is the whole <c>DISH_DEPTH</c>. Resizing the
        /// wheels still moves the gun, exactly as it would a real carriage.
        /// </summary>
        public static float TrunnionHeightAt(float orbitRadius) =>
            ArenaIsland.FloorHeightAt(orbitRadius) + AXLE_DROP + WHEEL_RADIUS;

        /// <summary>
        /// The ground's rise per unit of radius under the standing carriage, taken as the <b>secant across
        /// the stance's own footprint</b> — from the wheels' contact at <paramref name="orbitRadius"/> to
        /// the trail's feet <see cref="TRAIL_END"/>.Z further out — rather than the surface's derivative at
        /// a point: a rigid carriage bridging the dish's arris stands at the slope between its two contacts,
        /// so taking the secant eases the pitch across that crease over the carriage's own length instead of
        /// snapping it as the wheels roll over. Positive (the dish rises outward), and exactly the dish's
        /// ~6.4° grade while the whole stance is on it; zero once the whole stance is at or past the arris,
        /// which keeps a big-map orbit — off the island entirely — level, as it always was.
        /// <c>Cannon.CarriageWorld</c> turns it into the stance's basis; the barrel is deliberately not
        /// touched by it (the tube elevates about the trunnions in world frame, wherever the cradle leans).
        /// </summary>
        public static float StanceGradeAt(float orbitRadius) =>
            (ArenaIsland.FloorHeightAt(orbitRadius + TRAIL_END.Z) - ArenaIsland.FloorHeightAt(orbitRadius))
                / TRAIL_END.Z;

        //The woodwork and the ironwork: a warm matte wood for the wheels, a darker iron than the barrel's
        //steel for the frame — the barrel keeps its sheen, the undercarriage barely reflects.
        private static readonly Vector3 WHEEL_COLOR = new(0.40f, 0.29f, 0.19f);
        private static readonly Vector3 FRAME_COLOR = new(0.30f, 0.30f, 0.33f);
        private const float WHEEL_SPECULAR_AMBIENT = 0.08f;
        private const float FRAME_SPECULAR_AMBIENT = 0.22f;

        #endregion

        private CannonMesh _mesh;
        private InstancedModelRenderer _renderer;

        private CannonGlassMesh _glassMesh;
        private InstancedModelRenderer _glassRenderer;

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

            //The slot stops a chamber's depth ahead of the breech face, and the dome behind that face hides a
            //cavity of the same depth: the hood and the nest the parked round waits under (CHAMBER_DEPTH).
            //One local rather than the expression twice: the glazing below has to end at the very lip the
            //window ends at, and two copies of it are two things free to drift.
            float slotEndZ = breechZ - CHAMBER_DEPTH;

            _mesh = new CannonMesh(graphicsDevice, BORE_RADIUS, WALL_THICKNESS, muzzleZ, breechZ, SLOT_HALF_ANGLE,
                slotEndZ, CHAMBER_DEPTH, WALL_SEGMENTS);

            //Since the dome and its cascabel, the breech side outreaches the muzzle side — taken off the mesh
            //actually built, so the tube that is drawn and the box that frames it cannot disagree
            BarrelReach = MathF.Max(-muzzleZ, _mesh.PoleZ);

            //The ground darkens the barrel's underside just as it darkens the ball bellies. No GroundHeight
            //is set here: the gun stands on the island's DISH, so the height it is darkened against is the
            //stone under wherever the walk has it standing — Draw and DrawCarriage anchor it per frame off
            //the pose's own translation, and a load-time value would only be overwritten.
            _renderer = new InstancedModelRenderer(graphicsDevice, _mesh, STEEL_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = SPECULAR_AMBIENT_STRENGTH
            };

            //The window's glazing, cut to the very figures the window was: the front ball's centre is exactly
            //PivotToFrontBall ahead of the trunnions (which is what that figure means), so the notch is over
            //the round that fires by construction rather than by agreement — and it ends at the same lip.
            _glassMesh = new CannonGlassMesh(graphicsDevice, BORE_RADIUS, GLASS_THICKNESS, GLASS_SEAT,
                SLOT_HALF_ANGLE, slotEndZ, -PivotToFrontBall, BALL_RADIUS, GLASS_SEGMENTS);

            //The pane's own three figures — a deeper blue, a higher alpha and a sky reflection held down
            //against that alpha, all three for the reasons in the region above. Its ground-darkening anchor
            //rides the stone under the gun (DrawGlass).
            _glassRenderer = new InstancedModelRenderer(graphicsDevice, _glassMesh, GLASS_COLOR,
                instancingEffect, GLASS_ALPHA)
            {
                SpecularAmbientStrength = GLASS_SPECULAR_AMBIENT
            };

            //The carriage under the tube: the frame the trunnions ride in and the pair of wheels the advance
            //walk (W/S) rolls. Sized off the barrel's own figures, so a retuned bore moves the cheeks with it.
            _carriageMesh = new GunCarriageMesh(graphicsDevice, CHEEK_INNER_X, CHEEK_THICKNESS, CHEEK_TOP_Y,
                AXLE_DROP, CHEEK_HALF_LENGTH, AXLE_RADIUS, WHEEL_TRACK + HUB_HALF_WIDTH * 0.5f, TRAIL_END,
                TRUNNION_RADIUS, TRUNNION_INNER_X, TRUNNION_OUTER_X);

            _carriageRenderer = new InstancedModelRenderer(graphicsDevice, _carriageMesh, FRAME_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = FRAME_SPECULAR_AMBIENT
            };

            _wheelMesh = new GunWheelMesh(graphicsDevice, WHEEL_RADIUS, WHEEL_TUBE, HUB_RADIUS, HUB_HALF_WIDTH,
                WHEEL_SPOKES);

            _wheelRenderer = new InstancedModelRenderer(graphicsDevice, _wheelMesh, WHEEL_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = WHEEL_SPECULAR_AMBIENT
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
        /// How far the barrel reaches from the trunnions at its furthest, whichever end that is — since the
        /// breech dome and cascabel, the breech side. What <c>GameCameraFit</c>'s box around the gun is sized
        /// off (each caller hands it to <c>Solve</c> as <c>cannonReach</c>), so the fit frames the cascabel
        /// rather than cropping it at the bottom of the frame.
        /// </summary>
        public float BarrelReach { get; }

        /// <summary>
        /// The renderer, exposed for the one thing <see cref="Draw"/> cannot do for a caller: enrolling it in
        /// that executable's sky light rig. Nothing else needs it.
        /// </summary>
        public InstancedModelRenderer Renderer => _renderer;

        /// <summary>
        /// The glazing's renderer, for the same sky-lighting enrolment as <see cref="Renderer"/> — and it wants
        /// it more than the steel does: a pane of glass is mostly the sky it reflects, and one left out of the
        /// rig would hold a white dome's palette under every scene.
        /// </summary>
        public InstancedModelRenderer GlassRenderer => _glassRenderer;

        /// <summary>The carriage frame's renderer, for the same sky-lighting enrolment as <see cref="Renderer"/>.</summary>
        public InstancedModelRenderer CarriageRenderer => _carriageRenderer;

        /// <summary>The wheels' renderer, for the same sky-lighting enrolment as <see cref="Renderer"/>.</summary>
        public InstancedModelRenderer WheelRenderer => _wheelRenderer;

        /// <summary>Draws the barrel, as a single instance so it takes the same hemisphere ambient, positional
        /// key light and per-pixel shading as the instanced balls around it.</summary>
        /// <param name="world">This frame's pose, from <see cref="GameObjects.Cannon.BarrelWorld"/>.</param>
        public void Draw(ICamera camera, Matrix world, BasicEffectParams effectParams)
        {
            //The ground-darkening anchor rides the stone actually under the gun: the walk carries the
            //carriage down the island's dish, and against the fixed arris plane this used to anchor to, a
            //gun standing at the drain's mouth sat the whole DISH_DEPTH below the anchor and the darkening
            //term saturated over everything below the trunnions. The pose's translation is where the gun
            //stands, so the floor is read off it rather than grown into a parameter every caller must pass.
            _renderer.GroundHeight =
                ArenaIsland.FloorHeightAt(MathF.Sqrt(world.M41 * world.M41 + world.M43 * world.M43));

            _renderer.Draw(camera, world, effectParams);
        }

        /// <summary>
        /// Draws the pane that glazes the loading window. <b>Translucent, so where it goes in the frame is the
        /// caller's and it does not go with <see cref="Draw"/></b>: everything it is meant to be seen through —
        /// the loaded queue above all — has to be in the depth buffer and in the frame first, under
        /// <see cref="BlendState.AlphaBlend"/>. Both executables draw it last of their translucent surfaces,
        /// after the drain's glass and the ceiling's, because it is far and away the nearest of the three and a
        /// nearer pane composited first is a nearer pane the farther ones bleed through.
        /// </summary>
        /// <param name="world">The <b>same</b> pose the barrel was drawn with this frame, recoil stroke and all
        /// (<see cref="GameObjects.Cannon.BarrelWorld"/>) — pass anything else and the glass slides along the
        /// tube it is set into. Which is why callers take the matrix into a local and hand it to both.</param>
        public void DrawGlass(ICamera camera, Matrix world, BasicEffectParams effectParams)
        {
            //The same per-frame ground anchor as Draw's, off the same translation and for the same reason: the
            //pane is part of the gun, so it has to be darkened against the stone the gun stands on and not
            //against one the walk has left behind
            _glassRenderer.GroundHeight =
                ArenaIsland.FloorHeightAt(MathF.Sqrt(world.M41 * world.M41 + world.M43 * world.M43));

            _glassRenderer.Draw(camera, world, effectParams);
        }

        /// <summary>
        /// Draws the carriage under the barrel: the frame as one instance, the two wheels as one two-instance
        /// draw, spun by how much ground the advance walk has covered. Drawn with the barrel, wherever the
        /// caller draws that.
        /// </summary>
        /// <param name="carriageWorld">This frame's pose, from <see cref="GameObjects.Cannon.CarriageWorld"/> —
        /// seated on the stone it stands over, yawed with the aim, and deliberately without the recoil the
        /// barrel takes: the tube slides in the cradle, the carriage holds its ground.</param>
        /// <param name="advanceTravel"><see cref="GameObjects.Cannon.AdvanceTravel"/>: signed ground covered,
        /// positive toward the field. The wheels' angle is <c>travel / radius</c>, so they turn as fast as the
        /// ground passes and slow into the walk's rubber ends with it. (The travel is the walk's horizontal
        /// step; on the dish's ~6.4° grade the true rolled arc is ~0.6 % longer, which no eye reads off a
        /// spoked wheel.)</param>
        public void DrawCarriage(ICamera camera, Matrix carriageWorld, float advanceTravel,
            BasicEffectParams effectParams)
        {
            //Same per-frame ground anchor as Draw's, off the same translation, for the same reason
            float ground = ArenaIsland.FloorHeightAt(
                MathF.Sqrt(carriageWorld.M41 * carriageWorld.M41 + carriageWorld.M43 * carriageWorld.M43));
            _carriageRenderer.GroundHeight = ground;
            _wheelRenderer.GroundHeight = ground;

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

            _glassMesh?.Dispose();
            _glassMesh = null;
            _glassRenderer?.Dispose();
            _glassRenderer = null;

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
