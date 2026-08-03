using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The arena the game is played on, drawn: the round stone island — a cast-concrete drum with a dressed
    /// stone top and a moulded coping (<see cref="IslandMesh"/>) — the glass drain funnel bored through its
    /// middle, the two polished-gold beads that ring the drain's circles, and the dark pit shaft that backs
    /// the glass in the solid-terrain scenes. It owns the three meshes, the two procedural
    /// <see cref="SurfaceTexture"/>s, the five <see cref="InstancedModelRenderer"/>s and the world matrix; it
    /// existed value-for-value in the Testbed and in the Game until #75, down to every relief, slab and
    /// specular figure — the only numeric disagreement in the whole piece was the ground-proximity anchor,
    /// which <see cref="TOP_Y"/> now settles.
    /// <para>
    /// <b>The frame sequence does not move in here, and that is the constraint that shaped this API.</b> The
    /// draws come in three slices — <see cref="DrawIsland"/>, <see cref="DrawPit"/>,
    /// <see cref="DrawGlass"/> — precisely so each executable can keep placing them itself. The Testbed runs
    /// all three from one method; the Game splits them so its session can put the gun, the balls and the shot
    /// trails between the pit and the glass, and so its front end can run a frame with nothing between them
    /// at all. Anything that unified the three into one <c>Draw</c> would break both.
    /// </para>
    /// <para>
    /// <b>Sky-lit enrolment stays the caller's too</b>, for the reasons <see cref="SkyLightRig"/> gives: each
    /// executable has its own list and its own reasons (the Testbed's ceiling is always there, the Game's is
    /// rebuilt per level and may be null). What this class contributes is the four renderers that take the
    /// dome — see <see cref="SkyLitRenderers"/> and <see cref="AppendSkyLitTo"/> — and the pit is
    /// deliberately <b>not</b> among them, so no dome bleaches the inside of a hole in the ground.
    /// </para>
    /// <para>
    /// <b>Every figure is a constant on this type rather than a configurable record</b>, and that is load
    /// bearing rather than lazy: <c>GameplayScreen.ADS_MIN_Y = ArenaIsland.TOP_Y + 1f</c> and the drop
    /// cinematic's own <c>const float ISLAND_Y</c> are <i>constant expressions</i>, which will not compile
    /// against a property. Nothing has ever varied any of these per instance, so a config record would be
    /// speculative generality bought by breaking two consumers.
    /// </para>
    /// <para>
    /// <b>The collision floor is not here</b>, because <c>Prazsky.Core</c> cannot reference BepuPhysics: it is
    /// <c>Prazsky.BS3D.Physics.FunnelPhysics.Build</c>, which wants exactly <see cref="TOP_Y"/>,
    /// <see cref="FUNNEL_BOTTOM_Y"/>, <see cref="FUNNEL_TOP_RADIUS"/>, <see cref="FUNNEL_HOLE_RADIUS"/>,
    /// <see cref="FLOOR_RADIUS"/> and <see cref="FUNNEL_SEGMENTS"/>. Hand it those and the drawn surface and
    /// the collided one cannot drift.
    /// </para>
    /// <para>
    /// The component owns no content: the caller loads <c>Shaders/InstancedModel</c> through its own
    /// <c>ContentManager</c> (the libraries have no content pipeline — see CLAUDE.md) and hands the compiled
    /// effect in, keeping ownership of it. Nothing here looks an effect parameter up by name; all five draws
    /// go through <see cref="InstancedModelRenderer"/>, which caches its own handles at construction.
    /// </para>
    /// </summary>
    public sealed class ArenaIsland : IDisposable
    {
        /// <summary>
        /// The plane the whole assembly is built on, and the one figure both executables now read. The drawn
        /// top face of the stone (<see cref="IslandMesh"/>'s origin is the centre of its top face, so this is
        /// the translation in the world matrix), the drain's rim, the pit's mouth and the flat ring of the
        /// collision floor are <b>all</b> this plane by construction — which is what the Testbed's two extra
        /// aliases <c>FUNNEL_TOP_Y</c> and <c>PIT_TOP_Y</c> were saying, and why they are not carried over.
        /// <para>
        /// It is also the <b>ground-proximity darkening anchor</b> the balls' and the gun's renderers are
        /// given (<see cref="InstancedModelRenderer.GroundHeight"/>), and adopting it there is a
        /// <b>deliberate, visible improvement to the Testbed rather than a no-op — do not "restore" the old
        /// number.</b> The Testbed was handing the ball shader −9.49, a value its own comment admitted was
        /// "kept from the old ground-block layout, whose recessed centre block's top sat at −9.5" — geometry
        /// that has since been deleted, so the referent no longer exists. Measured against the shader's
        /// ground-occlusion range of 2.0: a ball resting on the stone got a proximity of
        /// <c>1 − 0.99/2 = 0.505</c> instead of <c>1.0</c>, i.e. about half the contact darkening it should
        /// have, and the effect faded out at y = −7.49 instead of at −6.5. The Game was already correct.
        /// </para>
        /// <para>
        /// <c>const</c> and not <c>static readonly</c>, because two consumers derive from it in constant
        /// expressions (see the class remarks).
        /// </para>
        /// </summary>
        public const float TOP_Y = -8.5f;

        //The arena is a small round island, not a big square plaza: the drain funnel in the centre, a
        //platform around it out to this radius, then its edge and the scene beyond. Kept just big enough to
        //stand the funnel in — the whole point is that the scene (sea, dunes, city…) fills the frame rather
        //than a plaza. The stone runs from the funnel's rim (FUNNEL_TOP_RADIUS, which is also the bore the
        //drain's mouth fills exactly) out to here.
        public const float RADIUS = 26f;

        /// <summary>Drop from the stone top to the drum's underside. The drop cinematic reads it, because it
        /// treats the platform as the solid it is and needs its underside.</summary>
        public const float EDGE_HEIGHT = 5f;

        /// <summary>
        /// Outer edge of the flat, level part of the top: the coping falls away past it, so this and not
        /// <see cref="RADIUS"/> is where a collision floor has to end — otherwise a ball rests on air over
        /// the wash. It is exactly what <c>FunnelPhysics.Build</c>'s <c>floorRadius</c> wants.
        /// <para>
        /// <c>static readonly</c> and not <c>const</c> only because <see cref="IslandMesh.FloorRadius"/> is a
        /// method call; nothing derives from it in a constant expression.
        /// </para>
        /// </summary>
        public static readonly float FLOOR_RADIUS = IslandMesh.FloorRadius(RADIUS);

        //Twice the drain's facet count. The mouldings are small enough that a coarse ring would show its
        //corners along the bright chamfer lines, which is exactly where the eye is drawn.
        //
        //(Forward-referencing FUNNEL_SEGMENTS is safe because both are const: a static field initialiser
        //only sees the fields declared above it, so a later reorder of a static readonly pair like this
        //would silently leave the island's segment count at zero. That trap is why the Game made the drain's
        //figures const in the first place, and all of them stay const here.)
        public const int SEGMENTS = FUNNEL_SEGMENTS * 2;

        //World units one tile of each procedural texture spans, far finer than the 30 the marble photograph
        //was mapped at — where one tile covered more than half the platform and no grain read at all.
        public const float STONE_SPAN = 4f;
        public const float CONCRETE_SPAN = 3.5f;

        //Under the figure the old flat disc carried, and the reason is worth keeping: the photograph it used
        //to project was black over half its canvas, so it was silently acting as an exposure control. Same
        //vantage, same nominal albedo, only the texture swapped: the top face measured 151 grey with the
        //broken photograph and 181 with a texture whose mean is 1 by construction — half a stop brighter for
        //a number nobody changed. Any albedo carried over from the old surface arrives overexposed.
        public static readonly Vector3 STONE_COLOR = new(0.52f, 0.51f, 0.49f);

        //Concrete: a plain cool concrete grey, within a hair of the cannon's own steel (each executable's
        //CANNON_COLOR) — and deliberately NOT tuned bluer than that. A vertical face in this world comes
        //back distinctly warm, because the key light is carried halfway to the horizon colour
        //(SkyLightRig.SKY_TINT_STRENGTH) and half of the hemisphere ambient it can see is the ground bounce,
        //which is that horizon again. That is the rig doing what it exists to do: measured in the same frame,
        //the cannon's steel reads 89,57,35 and this wall 82,51,31, and the city's facades are the same
        //family. An albedo pushed blue to cancel it would make the platform the one object in the scene that
        //does not take the light everything else does. Not darker than the stone it carries, either — a wall
        //that starts dark as well ends up a black band with no material in it. (See STONE_COLOR for why both
        //albedos read lower than they used to.)
        public static readonly Vector3 CONCRETE_COLOR = new(0.45f, 0.47f, 0.50f);

        /// <summary>
        /// Radius of the island's footprint that the solid-terrain shaders clip out of the ground, a couple
        /// of units inside the rim so the terrain tucks under the stone edge with no gap. Push it into
        /// <see cref="SceneRenderer.TerrainHoleRadius"/>.
        /// <para>
        /// The map editor deliberately leaves that property at its <c>0</c> default and must keep doing so:
        /// it draws no island, so nothing should be cut out of its terrain. Making an island available must
        /// not give the editor one as a side effect.
        /// </para>
        /// </summary>
        public const float TERRAIN_HOLE_RADIUS = RADIUS - 2f;

        //The drain funnel that replaces the recessed centre: a glass cone the shot balls fall into and roll
        //down, dropping through the hole at the bottom — below the map, where the caller's kill plane removes
        //them. The top rim is flush with the stone (TOP_Y) and sized to the old bath, so it catches the balls
        //that used to pile there; the walls are steep (~55 degrees) so the balls run down to the hole rather
        //than resting.
        public const float FUNNEL_TOP_RADIUS = 14f;   //the mouth; the island's bore is cut to exactly this, and the gold bead rings the junction
        public const float FUNNEL_HOLE_RADIUS = 1.8f; //the hole at the bottom, comfortably wider than a ball
        public const float FUNNEL_BOTTOM_Y = -27.5f;  //~19 below the rim: a wall steep enough to run a ball down
        public const int FUNNEL_SEGMENTS = 64;

        //The drain funnel is glass; the platform around it is a cast-concrete drum with a dressed stone top
        public static readonly Vector3 FUNNEL_GLASS_COLOR = new(0.42f, 0.62f, 0.72f);

        /// <summary>
        /// More opaque than the glass ceiling plate, so the drain reads clearly as a solid frosted-glass
        /// funnel rather than as an almost-invisible sheet — or as a hole.
        /// </summary>
        public const float FUNNEL_GLASS_ALPHA = 0.55f;

        //A polished-gold metal bead runs around both circles of the funnel (the wide top rim and the small
        //bottom hole), which is what makes the glass drain read at a glance — it rings exactly the junction
        //where the stone meets the glass. Drawn as two tori with the metal path (Metalness = 1): the gold
        //diffuse keeps it visible under any dome, the gold specular is its reflectance so it mirrors the sky
        //in gold rather than in the 4 % dielectric white every other surface reflects it with, and a tight
        //specular power keeps the highlight sharp.
        public static readonly Vector3 FUNNEL_RIM_COLOR = new(0.62f, 0.44f, 0.13f);   //warm gold diffuse (sRGB)
        public static readonly Vector3 FUNNEL_RIM_SPECULAR = new(1f, 0.83f, 0.48f);   //gold reflectance (sRGB)
        public const float FUNNEL_RIM_SPECULAR_POWER = 80f;                           //polished: a tight highlight
        public const float FUNNEL_RIM_TOP_TUBE = 0.5f;                                //bead radius at the mouth
        public const float FUNNEL_RIM_HOLE_TUBE = 0.3f;                               //bead radius at the hole
        public const int FUNNEL_RIM_TUBE_SEGMENTS = 16;                                //facets around each bead

        //The dark pit shaft behind the glass drain, drawn in the solid-terrain scenes only
        //(SceneRenderer.IsSolidTerrainScene: mountains, meadow, savanna, desert, forest). Those scenes are a
        //flat clearing at the island's foot, so their ground plane would slice straight across the funnel
        //just under its rim; the terrain shaders cut the island's footprint out of it (TERRAIN_HOLE_RADIUS),
        //and without a dark backing the ~55 %-opaque glass would then show the bright sky haze straight
        //through the hole and read as a glass ring lying on the ground, not as a drain. It must HUG the glass
        //funnel — a wide cone would hide behind the disc's stone ring and leave the bright hole showing
        //through the narrow (radius 14) aperture — so it shares the funnel's mouth and descends just outside
        //it, a near-black twin dropping well past the caller's kill plane. A ball runs down the glass into
        //darkness and is culled inside the pit, not against the bright sky. Near-matte (its specular ambient
        //turned right down) so no dome bleaches the well; the gold rim bead hides the mouth it shares with
        //the funnel. Visual only — the drain's own mesh is the floor. Not drawn in the sea (water fills it),
        //in the two cities (their own canyon falls away below the island) or in the sky-replacing scenes
        //(nothing down there to hide a ball against).
        public const float PIT_BOTTOM_Y = -46f;                                  //below either caller's kill plane, so balls vanish inside the pit
        public const float PIT_HOLE_RADIUS = 1.2f;                               //nearly closed at the bottom: a dark receding throat
        public static readonly Vector3 PIT_COLOR = new(0.03f, 0.03f, 0.035f);    //near-black, a touch cool

        private readonly GraphicsDevice _device;

        private readonly IslandMesh _islandMesh;
        private readonly SurfaceTexture _stoneTexture, _concreteTexture;
        private readonly InstancedModelRenderer _capRenderer, _bodyRenderer;

        private readonly FunnelMesh _funnelMesh;
        private readonly InstancedModelRenderer _funnelRenderer;
        private readonly FunnelRimsMesh _funnelRimsMesh;
        private readonly InstancedModelRenderer _funnelRimsRenderer;
        private readonly BasicEffectParams _funnelRimEffectParams;

        private readonly FunnelMesh _pitMesh;
        private readonly InstancedModelRenderer _pitRenderer;

        //One matrix, not three. The island's top face, the drain's rim and the pit's mouth are all flush with
        //TOP_Y and all centred on the world origin, so the three world matrices both executables carried
        //(_arenaDiscWorld/_islandWorld, _funnelWorld, _pitWorld) were the same translation written out three
        //times. Built once here and never touched again — nothing in this piece moves.
        private readonly Matrix _world;

        //The renderers that take the sky palette, in draw order, as a fixed array built once. The pit is
        //absent on purpose (see the class remarks and SkyLightRig's own).
        private readonly InstancedModelRenderer[] _skyLit;

        /// <summary>
        /// Builds the whole assembly here and now — the three meshes, the two procedural textures, the five
        /// renderers with every relief, slab, detail and specular figure, the gold beads' effect-params
        /// override and the one world matrix. There is nothing runtime about any of it (contrast
        /// <see cref="CeilingPlate.Fit"/>, whose footprint is the loaded level's), so it is built once and
        /// never rebuilt, and both executables construct it where they used to build the island by hand.
        /// <para>
        /// <b>The caller must run its own sky lighting after this</b>, as it did before: a fresh
        /// <see cref="InstancedModelRenderer"/> has never been told the dome's palette, so until the caller's
        /// pass reaches the four in <see cref="SkyLitRenderers"/> the island is lit by a white sky through a
        /// rig that was never decoded into radiance.
        /// </para>
        /// </summary>
        /// <param name="device">Graphics device the meshes, textures and instance buffers live on.</param>
        /// <param name="instancingEffect">The shared instancing effect (<c>Shaders/InstancedModel.fx</c>),
        /// compiled by the caller's content pipeline and handed in. Not disposed here — see the class
        /// remarks on content.</param>
        /// <param name="sceneAmbientIntensity">The caller's flat ambient fill for scene objects, which is
        /// all the gold beads' own <see cref="BasicEffectParams"/> needs from the scene: their override
        /// exists to carry <see cref="FUNNEL_RIM_SPECULAR"/> and <see cref="FUNNEL_RIM_SPECULAR_POWER"/>
        /// instead of the scene's white specular, and it must not otherwise light them differently from
        /// everything around them. Both executables pass 0.25.</param>
        public ArenaIsland(GraphicsDevice device, Effect instancingEffect, float sceneAmbientIntensity)
        {
            _device = device;

            //The arena is a small round island: a cast-concrete drum with a dressed stone top and a moulded
            //coping around its rim, the drain funnel bored through the middle (IslandMesh owns the whole
            //cross-section). It replaces the big square marble/glass plaza, whose panels ate the whole lower
            //frame and hid the scene the arena stands in — and, since the plaza, the plain extruded washer
            //that read as a cylinder with a hole in it because every edge of it was a raw 90-degree cut.
            //
            //Both textures are generated rather than loaded, and that is a fix rather than a flourish: the
            //marble photograph this used to project covers only the left half of its canvas and the rest is
            //black, so the triplanar projection multiplied roughly half of the platform by zero at any
            //detail scale — which is what left the wall reading as a dark band. These tile exactly.
            _stoneTexture = SurfaceTexture.Stone(device);
            _concreteTexture = SurfaceTexture.Concrete(device);

            _islandMesh = new IslandMesh(device, FUNNEL_TOP_RADIUS, RADIUS, EDGE_HEIGHT, SEGMENTS);

            //The dressed stone: the flat top and the coping that finishes it, coursed into slabs. The detail
            //texture is what selects the technique that reads any of this — without one the renderer falls
            //through to the plain technique and every relief setting here is silently dead. DetailBoost
            //normalises the texture to a mean of 1, so it varies the albedo without dimming it and
            //STONE_COLOR stays the honest colour of the stone.
            _capRenderer = new InstancedModelRenderer(device, _islandMesh.Cap, STONE_COLOR, instancingEffect)
            {
                DetailTexture = _stoneTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / STONE_SPAN,
                DetailBoost = 1f / _stoneTexture.LinearMean,
                DetailStrength = 0.5f,

                SurfaceReliefFrequency = 9f,
                SurfaceReliefStrength = 0.008f,

                //The joint grid is laid out in world X and Z whatever the face, so it also breaks the
                //coping's own ring into blocks — which is how a coping is actually laid.
                SlabSize = 2f,
                SlabJointWidth = 0.025f,

                //Shallower than the old flat disc's joints. The grid is cut on every face, so it also runs
                //down the coping's own ring — which is right, a coping is laid in blocks — but a groove on a
                //near-vertical face turns its walls towards the sky, and at the old depth every joint round
                //the rim came back as a bright wire rather than as a seam.
                SlabJointDepth = 0.025f,
                CavityStrength = 0.7f,

                //(No ReliefShadowStrength or ParallaxScale: the triplanar path builds its own height field
                //and never runs the self-shadow or parallax marches, so both were dead where they used to
                //be set here.)

                //A floor is seen at a grazing angle almost everywhere except right under your feet, which is
                //exactly where Fresnel puts the sky reflection at full strength. Left at 1 the stone mirrors
                //the sky into a white sheet from the middle distance out.
                //
                //This is also the dial that decides how POLISHED the top reads, and not the albedo: the
                //specular ambient is not multiplied by albedo (a reflection does not care how dark the
                //surface under it is), so halving the stone's colour barely dimmed a top face that was
                //washing out — most of its brightness was this term. Dressed stone is matte.
                SpecularAmbientStrength = 0.14f
            };

            //The concrete drum. No slab joints — it is cast, not laid — and a coarser, deeper relief than
            //the dressed stone above it, which is most of what makes the two read as different materials at
            //a distance where neither texture resolves. Barely reflective, because concrete is not.
            _bodyRenderer = new InstancedModelRenderer(device, _islandMesh.Body, CONCRETE_COLOR, instancingEffect)
            {
                DetailTexture = _concreteTexture.Texture,
                DetailTextureMapping = DetailMapping.Triplanar,
                DetailScale = 1f / CONCRETE_SPAN,
                DetailBoost = 1f / _concreteTexture.LinearMean,
                DetailStrength = 0.62f,

                //The relief is a sum of sines, and past a certain amplitude the sum stops reading as a rough
                //surface and starts reading as the waves it is made of — a regular diagonal weave across the
                //whole drum, which is what a first pass at 0.045 gave. The texture carries the roughness; the
                //relief only has to break the light over it.
                SurfaceReliefFrequency = 4.5f,
                SurfaceReliefStrength = 0.012f,
                SlabSize = 0f,
                CavityStrength = 0.85f,

                //Lower than the stone's, because concrete is rougher and barely reflective. What a vertical
                //face reflects is the horizon rather than the zenith — the brightest, warmest part of the
                //dome, and unmultiplied by albedo — so this term is also what would wash the drum out into
                //one flat sheen and take its material with it.
                SpecularAmbientStrength = 0.08f
            };

            //The drain funnel in the centre, glass. Its rim (FUNNEL_TOP_RADIUS) is flush with the stone top
            //and meets the island's bore directly, so it needs no collar (the 0 argument — the machinery
            //that filled the corners of a square pit is unused here); it descends to the hole the balls fall
            //through. Drawn translucent and with culling off (see DrawGlass) so the one-sided cone reads
            //both looking down into it and up through the hole, and it joins the caller's sky-lit list so
            //its sheen takes the dome like everything else.
            float funnelHeight = TOP_Y - FUNNEL_BOTTOM_Y;

            _funnelMesh = new FunnelMesh(device, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS, funnelHeight, FUNNEL_SEGMENTS, 0f);
            _funnelRenderer = new InstancedModelRenderer(device, _funnelMesh, FUNNEL_GLASS_COLOR, instancingEffect, FUNNEL_GLASS_ALPHA);

            //Both gold beads in one mesh (built in the funnel's own local space), so one renderer draws them
            //and they share the one world matrix. Opaque, so they go down with the opaque scene before the
            //glass the funnel composites over them; metallic (see FUNNEL_RIM_* / Metalness), with the gold
            //specular riding in as a per-draw effect-params override rather than the scene's white.
            _funnelRimsMesh = new FunnelRimsMesh(device, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS, funnelHeight,
                FUNNEL_RIM_TOP_TUBE, FUNNEL_RIM_HOLE_TUBE, FUNNEL_SEGMENTS, FUNNEL_RIM_TUBE_SEGMENTS);

            _funnelRimsRenderer = new InstancedModelRenderer(device, _funnelRimsMesh, FUNNEL_RIM_COLOR, instancingEffect)
            {
                Metalness = 1f,
                SpecularAmbientStrength = 1f
            };

            _funnelRimEffectParams = new BasicEffectParams(Vector3.One * sceneAmbientIntensity,
                FUNNEL_RIM_SPECULAR, FUNNEL_RIM_SPECULAR_POWER, Vector3.Zero);

            //The dark well behind the glass (solid-terrain scenes only — see the PIT_* remarks): a near-black
            //cone sharing the glass funnel's mouth and descending just outside it, past the kill plane, so
            //the drain reads as a deep dark well rather than as a glass ring over the bright hole cut in the
            //terrain. It reuses FunnelMesh — its wall faces inward and up, so it reads looking down into it —
            //and is drawn opaque and CullNone before the glass, which composites over it. Deliberately NOT
            //enrolled in the caller's sky lighting, and near-matte, so no dome bleaches the inside of a hole
            //in the ground; the gold rim bead hides the mouth it shares with the funnel.
            _pitMesh = new FunnelMesh(device, FUNNEL_TOP_RADIUS, PIT_HOLE_RADIUS, TOP_Y - PIT_BOTTOM_Y, FUNNEL_SEGMENTS, 0f);
            _pitRenderer = new InstancedModelRenderer(device, _pitMesh, PIT_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = 0.03f
            };

            _world = Matrix.CreateTranslation(0f, TOP_Y, 0f);

            _skyLit = new[] { _capRenderer, _bodyRenderer, _funnelRenderer, _funnelRimsRenderer };
        }

        /// <summary>
        /// The four renderers that take the sky palette, in draw order — the stone, the concrete, the glass
        /// and the gold — for a caller whose enrolment pass is change-driven rather than per-frame (the
        /// Game's iterator over its own objects). A fixed array built in the constructor: never rebuilt, and
        /// never an iterator. The pit is deliberately absent (see the class remarks).
        /// <para>
        /// A per-frame caller should use <see cref="AppendSkyLitTo"/> instead, and the reason the two exist
        /// side by side is measured: reading an <see cref="IReadOnlyList{T}"/> through <c>foreach</c>
        /// allocates an enumerator per call, and the Testbed's list is refilled on <i>every frame</i> because
        /// its overcast lerp re-derives the rig every frame. BestPractices.md §3 records that exact incident.
        /// </para>
        /// </summary>
        public IReadOnlyList<InstancedModelRenderer> SkyLitRenderers => _skyLit;

        /// <summary>
        /// Appends the same four renderers to the caller's own enrolment list, walked by index so the call
        /// allocates nothing at all — for the Testbed, whose list is refilled every frame (see
        /// <see cref="SkyLitRenderers"/> for why that distinction is worth two members).
        /// </summary>
        /// <param name="target">The caller's reused list. Cleared and refilled by the caller, never here.</param>
        public void AppendSkyLitTo(List<InstancedModelRenderer> target)
        {
            for (int i = 0; i < _skyLit.Length; i++) target.Add(_skyLit[i]);
        }

        /// <summary>
        /// The opaque platform: the stone cap, then the concrete drum. Two draws because it is two materials.
        /// <para>
        /// <b>Touches no GPU state, on purpose.</b> The island is a closed solid whose triangles are wound
        /// clockwise seen from outside (see <see cref="LatheMesh"/> and the winding convention in CLAUDE.md),
        /// so it takes the scene's ordinary back-face culling — and drawing it that way is what would
        /// <i>show</i> a winding mistake rather than hide one. Leaves the rasterizer exactly as it found it.
        /// </para>
        /// </summary>
        public void DrawIsland(ICamera camera, BasicEffectParams sceneParams)
        {
            _capRenderer.Draw(camera, _world, sceneParams);
            _bodyRenderer.Draw(camera, _world, sceneParams);
        }

        /// <summary>
        /// The dark pit shaft, in the solid-terrain scenes only, before the glass that composites over it.
        /// The classification is <see cref="SceneRenderer.IsSolidTerrainScene"/> — the shared one, hoisted in
        /// #75 out of the private copy each executable kept (the forest was once missing from both).
        /// <para>
        /// The <see cref="RasterizerState.CullNone"/> sandwich is <b>unconditional</b> while the draw inside
        /// it is not, which is deliberate: it is byte-identical to what both executables do today, including
        /// in the scenes that draw no pit at all, so there is no second state path to get wrong. The cone
        /// needs culling off because it is an open surface rather than a closed solid. The framework's
        /// cached statics, never a fresh state object — see BestPractices.md §2.
        /// </para>
        /// <para>
        /// <paramref name="scene"/> is the scene being drawn; the shaft is skipped unless its terrain has the
        /// island's footprint cut out of it.
        /// </para>
        /// </summary>
        public void DrawPit(ICamera camera, BasicEffectParams sceneParams, SceneKind scene)
        {
            _device.RasterizerState = RasterizerState.CullNone;

            if (SceneRenderer.IsSolidTerrainScene(scene)) _pitRenderer.Draw(camera, _world, sceneParams);

            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The drain's gold beads and then its glass, after the frame's opaque work: the beads are opaque, so
        /// they belong to the opaque scene and go down first, while the funnel composites over everything
        /// already in the frame — which is why this is a slice the caller places itself, after its balls and
        /// shot trails.
        /// <para>
        /// <b>One <see cref="RasterizerState.CullNone"/> spanning both, restored after.</b> The Game already
        /// wrapped them that way and the Testbed used two separate pairs; collapsing to one is
        /// image-identical, because the two draws want culling off for <i>different</i> reasons and both are
        /// satisfied by it. The beads are a closed convex tube, so the nearest face wins on depth and the
        /// winding is moot — one less thing to get wrong unseen. The glass is one open, single-sided cone
        /// that has to read both looking down into it and looking up through the hole.
        /// </para>
        /// </summary>
        public void DrawGlass(ICamera camera, BasicEffectParams sceneParams)
        {
            _device.RasterizerState = RasterizerState.CullNone;

            _funnelRimsRenderer.Draw(camera, _world, _funnelRimEffectParams);
            _funnelRenderer.Draw(camera, _world, sceneParams);

            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The three meshes, both procedural textures <b>and</b> all five renderers — everything this
        /// component made. The instancing effect is the caller's content manager's and is left alone.
        /// <para>
        /// Giving the pair one owner is also what fixes a leak: the Testbed disposed the meshes and the
        /// textures but never the island's, funnel's, rims' or pit's <see cref="InstancedModelRenderer"/>,
        /// each of which holds a native instance buffer. Process exit papered over it, so it had never been
        /// visible. The Game disposed both halves; now there is only one half to get right.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            _capRenderer?.Dispose();
            _bodyRenderer?.Dispose();
            _islandMesh?.Dispose();

            _stoneTexture?.Dispose();
            _concreteTexture?.Dispose();

            _funnelRenderer?.Dispose();
            _funnelMesh?.Dispose();
            _funnelRimsRenderer?.Dispose();
            _funnelRimsMesh?.Dispose();

            _pitRenderer?.Dispose();
            _pitMesh?.Dispose();
        }
    }
}
