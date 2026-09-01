using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Which of the arena's five members <see cref="ArenaIsland"/> puts out — a <b>measurement</b> surface, not
    /// a look setting, and the only thing on this type that is not a fixed figure.
    /// <para>
    /// It exists because #151 could not otherwise be answered. The arena measured at roughly 27 ms of a 42 ms
    /// frame on the weakest machine, in every scene, and every candidate for <i>which</i> member carries that
    /// — the stone's seven-octave relief, the triplanar taps, the translucent drain over an opaque pit both
    /// drawn <see cref="RasterizerState.CullNone"/>, the metallic beads — was a suspect with no way to take it
    /// out of the frame and look. #155 is the standing reminder of why guessing is not enough: removing
    /// Cavern's entire second <c>ShadeWall</c> call moved its frame time by 0.03 ms.
    /// </para>
    /// <para>
    /// The Testbed's <c>arena=</c> argument drives it (<c>arena=all,-glass</c> and so on). Nothing else sets
    /// it, so every shipped frame draws <see cref="All"/>.
    /// </para>
    /// </summary>
    [Flags]
    public enum ArenaMembers
    {
        /// <summary>Nothing at all — the arena's whole contribution to the frame, in one subtraction.</summary>
        None = 0,

        /// <summary>The dressed stone top and the coping that finishes it (<see cref="IslandMesh.Cap"/>).</summary>
        Cap = 1,

        /// <summary>The cast-concrete drum under the stone (<see cref="IslandMesh.Body"/>).</summary>
        Drum = 2,

        /// <summary>The dark pit shaft behind the glass — drawn in the solid-terrain scenes only either way.</summary>
        Pit = 4,

        /// <summary>The two polished-gold beads ringing the drain's circles.</summary>
        Rims = 8,

        /// <summary>The translucent glass drain funnel.</summary>
        Glass = 16,

        /// <summary>Every member, which is what the game always draws.</summary>
        All = Cap | Drum | Pit | Rims | Glass
    }

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
    /// bearing rather than lazy: the drop cinematic's <c>const float ISLAND_Y = ArenaIsland.TOP_Y</c> is a
    /// <i>constant expression</i>, which will not compile against a property. Nothing has ever varied any
    /// of these per instance, so a config record would be speculative generality bought by breaking a
    /// consumer.
    /// </para>
    /// <para>
    /// <b>The collision floor is not here</b>, because <c>Prazsky.Core</c> cannot reference BepuPhysics: it is
    /// <c>Prazsky.BS3D.Physics.FunnelPhysics.Build</c>, which wants exactly <see cref="TOP_Y"/>,
    /// <see cref="FUNNEL_BOTTOM_Y"/>, <see cref="FUNNEL_TOP_RADIUS"/>, <see cref="FUNNEL_HOLE_RADIUS"/>,
    /// <see cref="FLOOR_RADIUS"/>, <see cref="DISH_DEPTH"/> and <see cref="FUNNEL_SEGMENTS"/>. Hand it those
    /// and the drawn surface and the collided one cannot drift.
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
        /// The plane the whole assembly is built on, and the one figure both executables now read: the stone
        /// top's <b>outer arris</b> (<see cref="IslandMesh"/>'s y = 0, so this is the translation in the
        /// island's world matrix), from which the walkable top dishes down <see cref="DISH_DEPTH"/> to the
        /// drain — so the drain's rim, its gold bead and the pit's mouth all share the <i>lower</i> plane
        /// <c>TOP_Y - DISH_DEPTH</c>, and the collision floor runs between the two by construction. (The
        /// Testbed's old aliases <c>FUNNEL_TOP_Y</c>/<c>PIT_TOP_Y</c> named a single flush plane; the dish is
        /// why there are two now, and <see cref="DISH_DEPTH"/> is the whole difference between them.)
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
        /// Outer edge of the walkable top — the dish's own arris, the one circle of it at <see cref="TOP_Y"/>:
        /// the coping falls away past it, so this and not <see cref="RADIUS"/> is where a collision floor has
        /// to end — otherwise a ball rests on air over the wash. It is exactly what
        /// <c>FunnelPhysics.Build</c>'s <c>floorRadius</c> wants.
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
        //them. The top rim sits at the foot of the stone dish (TOP_Y - DISH_DEPTH) and is sized to the old
        //bath, so it catches the balls the dish rolls in; the walls are steep (~55 degrees) so the balls run down to the hole rather
        //than resting.
        public const float FUNNEL_TOP_RADIUS = 14f;   //the mouth; the island's bore is cut to exactly this, and the gold bead rings the junction
        public const float FUNNEL_HOLE_RADIUS = 1.8f; //the hole at the bottom, comfortably wider than a ball
        public const float FUNNEL_BOTTOM_Y = -27.5f;  //~18 below the rim: a wall steep enough to run a ball down
        public const int FUNNEL_SEGMENTS = 64;

        /// <summary>
        /// How far the stone top falls from its outer arris (<see cref="FLOOR_RADIUS"/>, which stays at
        /// <see cref="TOP_Y"/>) to the drain's mouth: the walkable ring is a shallow dish (~6.4° over the
        /// 10.7-unit run), so a ball that lands on the stone rolls into the glass and out through the hole
        /// instead of coming to rest — on a big map most released balls used to land on the flat ring and
        /// simply stay there, and the wider the field the smaller the share the drain ever swallowed. Bepu
        /// spheres carry no rolling resistance (friction resists sliding, not rolling), so this grade is
        /// plenty; the drain's rim, its gold bead and the pit's mouth all sit this far below
        /// <see cref="TOP_Y"/>, and <c>FunnelPhysics.Build</c> must be handed the same figure or balls rest
        /// on air over the drawn stone.
        /// </summary>
        public const float DISH_DEPTH = 1.2f;

        /// <summary>
        /// World Y of the walkable stone at a horizontal distance <paramref name="radius"/> from the
        /// island's centre — which is the world origin: the island never moves, so a world position's own
        /// XZ length is its radius here. <see cref="TOP_Y"/> at and past the outer arris
        /// (<see cref="FLOOR_RADIUS"/>) — beyond it the coping falls away and then nothing, but a gun
        /// orbiting off the island (a big map does that) holds the arris plane, the one plane the eye takes
        /// for the ground there — falling linearly by <see cref="DISH_DEPTH"/> to the drain's mouth
        /// (<see cref="FUNNEL_TOP_RADIUS"/>), and held at the mouth's rim inside it, where there is no stone
        /// at all, only glass. The dish is a single straight lathe span and the cap has no radial
        /// irregularity (only the concrete drum wobbles — see <see cref="IslandMesh"/>), so this <b>is</b>
        /// the drawn surface and the collided one, exactly, to within their faceting.
        /// </summary>
        public static float FloorHeightAt(float radius)
        {
            if (radius >= FLOOR_RADIUS) return TOP_Y;
            if (radius <= FUNNEL_TOP_RADIUS) return TOP_Y - DISH_DEPTH;

            return TOP_Y - DISH_DEPTH * (FLOOR_RADIUS - radius) / (FLOOR_RADIUS - FUNNEL_TOP_RADIUS);
        }

        //The drain funnel is glass; the platform around it is a cast-concrete drum with a dressed stone top
        public static readonly Vector3 FUNNEL_GLASS_COLOR = new(0.42f, 0.62f, 0.72f);

        /// <summary>
        /// More opaque than the glass ceiling plate, so the drain reads clearly as a solid frosted-glass
        /// funnel rather than as an almost-invisible sheet — or as a hole.
        /// </summary>
        public const float FUNNEL_GLASS_ALPHA = 0.55f;

        //A polished-gold band runs around both circles of the funnel (the wide top rim and the small bottom
        //hole), which is what makes the glass drain read at a glance — it rings exactly the junction where
        //the stone meets the glass. Drawn with the metal path (Metalness = 1): the gold diffuse keeps it
        //visible under any dome, the gold specular is its reflectance so it mirrors the sky in gold rather
        //than in the 4 % dielectric white every other surface reflects it with, and a tight specular power
        //keeps the highlight sharp.
        //
        //It lies FLAT on the surface it rings rather than standing proud of it — the top band on the stone
        //dish, the bottom one on the glass cone — see FunnelRimsMesh on why a raised bead was the wrong
        //shape at the one junction every released ball rolls across (#94). The top band also WRAPS the lip,
        //turning the crease and running a little way down the glass, so no stone can show between the gold
        //and the glass — see FunnelRimsMesh on why burying that edge instead drew the strip it was hiding (#237).
        public static readonly Vector3 FUNNEL_RIM_COLOR = new(0.62f, 0.44f, 0.13f);   //warm gold diffuse (sRGB)
        public static readonly Vector3 FUNNEL_RIM_SPECULAR = new(1f, 0.83f, 0.48f);   //gold reflectance (sRGB)
        public const float FUNNEL_RIM_SPECULAR_POWER = 80f;                           //polished: a tight highlight

        //Radial widths, carried over from the tori these replaced: each band is as wide as its bead was
        //across, so the gold reads at the size the drain was tuned to look right at.
        public const float FUNNEL_RIM_TOP_WIDTH = 1f;                                 //band width at the mouth
        public const float FUNNEL_RIM_HOLE_WIDTH = 0.6f;                              //band width at the hole

        /// <summary>
        /// Rise per unit of radius of the walkable dish, going outwards from the drain's mouth to the outer
        /// arris — the same straight span <see cref="FloorHeightAt"/> interpolates along, as the one figure
        /// rather than as a second copy of it. It is what lets the top gold band lie on the stone instead of
        /// hovering horizontally over a sloped surface.
        /// </summary>
        public static readonly float DISH_GRADE = DISH_DEPTH / (FLOOR_RADIUS - FUNNEL_TOP_RADIUS);

        //The dark pit shaft behind the glass drain, drawn in the solid-terrain scenes only
        //(SceneRenderer.IsSolidTerrainScene: mountains, meadow, savanna, desert, forest). Those scenes are a
        //flat clearing at the island's foot, so their ground plane would slice straight across the funnel
        //just under its rim; the terrain shaders cut the island's footprint out of it (TERRAIN_HOLE_RADIUS),
        //and without a dark backing the ~55 %-opaque glass would then show the bright sky haze straight
        //through the hole and read as a glass ring lying on the ground, not as a drain. It must HUG the glass
        //funnel — a wide cone would hide behind the disc's stone ring and leave the bright hole showing
        //through the narrow (radius 14) aperture — and since #291 the hug is LITERAL: the shaft is a
        //two-band FunnelMesh polyline sharing the funnel's mouth, running PIT_SHEATH_CLEARANCE outside the
        //glass down to just past its hole, then a narrow throat on down past the caller's kill plane. It
        //was one straight cone from the mouth to PIT_BOTTOM_Y, which — being 18.5 units longer than the
        //glass on the same mouth — sat WIDER than the glass at every depth and read from outside, wherever
        //a vantage gets under a solid scene's terrain (the free camera, the editor, any terrain dip), as a
        //long dark spike nothing inside the drain accounts for: the owner's "outer shell doesn't match the
        //inside". Sheathed, the outside reads as the bowl's own underside with a drain pipe below it — the
        //same object as the view down into it. A ball still runs down the glass into darkness and is culled
        //inside the throat, not against the bright sky. Near-matte (its specular ambient turned right down)
        //so no dome bleaches the well; the gold beads hide the mouth ring it shares with the funnel and the
        //knee at the glass hole. Visual only — the drain's own mesh is the floor. Not drawn in the sea
        //(water fills it), in the two cities (their own canyon falls away below the island) or in the
        //sky-replacing scenes (nothing down there to hide a ball against).
        public const float PIT_BOTTOM_Y = -46f;                                  //below either caller's kill plane, so balls vanish inside the pit
        public const float PIT_HOLE_RADIUS = 1.2f;                               //nearly closed at the bottom: a dark receding throat
        public const float PIT_SHEATH_CLEARANCE = 0.35f;                         //how far outside the glass hole the sheath's knee sits: outside at every depth (the gap grows 0 -> this, mouth to hole), never poking through
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

        //Two matrices, two planes. The island's own mesh is built about the stone top's outer arris (TOP_Y),
        //while the drain family — the glass, its gold beads and the pit — shares the dish's low inner lip
        //(TOP_Y - DISH_DEPTH), each mesh laying its mouth at its own local y = 0. Before the dish all four
        //were flush at TOP_Y and this was one translation; DISH_DEPTH is the whole difference. Built once
        //here and never touched again — nothing in this piece moves.
        private readonly Matrix _world;
        private readonly Matrix _drainWorld;

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

            _islandMesh = new IslandMesh(device, FUNNEL_TOP_RADIUS, RADIUS, EDGE_HEIGHT, SEGMENTS, DISH_DEPTH);

            //The dressed stone: the dished top and the coping that finishes it, coursed into slabs. The detail
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

            //The drain funnel in the centre, glass. Its rim (FUNNEL_TOP_RADIUS) is flush with the stone
            //dish's low inner lip — TOP_Y - DISH_DEPTH, the plane _drainWorld puts it on — and meets the
            //island's bore directly, so it needs no collar (the 0 argument — the machinery that filled the
            //corners of a square pit is unused here); it descends to the hole the balls fall through, which
            //stays at FUNNEL_BOTTOM_Y whatever the dish takes off the top. Drawn translucent and with
            //culling off (see DrawGlass) so the one-sided cone reads both looking down into it and up
            //through the hole, and it joins the caller's sky-lit list so its sheen takes the dome like
            //everything else.
            float funnelHeight = TOP_Y - DISH_DEPTH - FUNNEL_BOTTOM_Y;

            _funnelMesh = new FunnelMesh(device, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS, funnelHeight, FUNNEL_SEGMENTS, 0f);

            //TwoSidedNormals because the cone is one open single-sided wall drawn CullNone: without it the
            //outside — which the open-below scenes and the drop cinematic's dive film from underneath — is
            //shaded with the inside's normal, and the grazing-angle sky sheen turns the glass into a bright
            //milky sheet (#291). With it the back faces shade as the outside they are, and the glass reads
            //as the same frosted cone from both sides.
            _funnelRenderer = new InstancedModelRenderer(device, _funnelMesh, FUNNEL_GLASS_COLOR, instancingEffect, FUNNEL_GLASS_ALPHA)
            {
                TwoSidedNormals = 1f
            };

            //Both gold bands in one mesh (built in the funnel's own local space), so one renderer draws them
            //and they share the one world matrix. Opaque, so they go down with the opaque scene before the
            //glass the funnel composites over them; metallic (see FUNNEL_RIM_* / Metalness), with the gold
            //specular riding in as a per-draw effect-params override rather than the scene's white. The dish
            //grade goes in because the top band lies on the stone, which is the one surface here the mesh
            //cannot work out from the funnel's own figures.
            _funnelRimsMesh = new FunnelRimsMesh(device, FUNNEL_TOP_RADIUS, FUNNEL_HOLE_RADIUS, funnelHeight,
                FUNNEL_RIM_TOP_WIDTH, FUNNEL_RIM_HOLE_WIDTH, DISH_GRADE, FUNNEL_SEGMENTS);

            _funnelRimsRenderer = new InstancedModelRenderer(device, _funnelRimsMesh, FUNNEL_RIM_COLOR, instancingEffect)
            {
                Metalness = 1f,
                SpecularAmbientStrength = 1f
            };

            _funnelRimEffectParams = new BasicEffectParams(Vector3.One * sceneAmbientIntensity,
                FUNNEL_RIM_SPECULAR, FUNNEL_RIM_SPECULAR_POWER, Vector3.Zero);

            //The dark well behind the glass (solid-terrain scenes only — see the PIT_* remarks): a near-black
            //SHEATH of the glass funnel since #291 — one band from the shared mouth to PIT_SHEATH_CLEARANCE
            //outside the glass hole, then the throat on down past the kill plane — so the drain reads as one
            //object from outside and from above, rather than as a glass bowl over an unrelated spike. It
            //reuses FunnelMesh's polyline form — its wall faces the concave side, so it reads looking down
            //into it — and is drawn opaque and CullNone before the glass, which composites over it (the
            //outside face shades right through TwoSidedNormals, like the glass). Deliberately NOT enrolled
            //in the caller's sky lighting, and near-matte, so no dome bleaches the inside of a hole in the
            //ground; the gold beads hide the mouth ring and the knee it shares with the funnel.
            _pitMesh = new FunnelMesh(device, new (float Radius, float Y)[]
            {
                (FUNNEL_TOP_RADIUS, 0f),
                (FUNNEL_HOLE_RADIUS + PIT_SHEATH_CLEARANCE, -funnelHeight),
                (PIT_HOLE_RADIUS, -(TOP_Y - DISH_DEPTH - PIT_BOTTOM_Y)),
            }, FUNNEL_SEGMENTS);
            _pitRenderer = new InstancedModelRenderer(device, _pitMesh, PIT_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = 0.03f,
                TwoSidedNormals = 1f
            };

            _world = Matrix.CreateTranslation(0f, TOP_Y, 0f);
            _drainWorld = Matrix.CreateTranslation(0f, TOP_Y - DISH_DEPTH, 0f);

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
        /// Which members the three draw slices actually put out. <see cref="ArenaMembers.All"/> in every
        /// shipped frame — only the Testbed's <c>arena=</c> argument moves it, to isolate what #151 measured.
        /// <para>
        /// A member left out is left out of the <i>draw</i> only: it keeps its mesh, its renderer and its
        /// place in <see cref="SkyLitRenderers"/>, so a sweep changes what the frame costs and nothing else
        /// about the run. The five tests cost one predictable branch each per frame.
        /// </para>
        /// </summary>
        public ArenaMembers Members { get; set; } = ArenaMembers.All;

        /// <summary>
        /// How much of the stone cap's authored surface detail is drawn: 1 is the full look, anything below
        /// it the reduced one. A plain number rather than a quality enum for the reason
        /// <see cref="SceneRenderer.SceneDetail"/> states — the tier lives in the Game and this library
        /// cannot see it — and left at 1 for a caller that never sets it, which is what the Testbed and the
        /// map editor want.
        /// <para>
        /// <b>The cap is the arena</b>: 88 % of its cost at a play camera (#151), and the first thing in
        /// this project's quality tier that reaches the arena at all — which is in every scene and on screen
        /// for every second of every level, and which the tier had never touched, because it is not a scene.
        /// Reduced, the cap's height field drops from seven relief octaves to three
        /// (<see cref="InstancedModelRenderer.CoarseSurfaceRelief"/>): <b>0.336 ms of a 10.971 ms frame</b>,
        /// measured on the reference desktop at the play camera, windowed 1920×1080 at ssaa 4.
        /// </para>
        /// <para>
        /// <b>The cap alone, and deliberately not the drum.</b> The drum is triplanar too and its relief is
        /// most of what makes the two read as different materials at a distance — and it measures at 0.004 ms,
        /// because neither camera the game ever uses sees much of a wall that faces outwards under the coping.
        /// Reducing it would trade a look for nothing.
        /// </para>
        /// </summary>
        public float SurfaceDetail
        {
            get => _surfaceDetail;
            set
            {
                _surfaceDetail = value;
                _capRenderer.CoarseSurfaceRelief = value < 1f;
            }
        }

        private float _surfaceDetail = 1f;

        /// <summary>
        /// #151's measurement probes, on the stone cap alone. Forwards to
        /// <see cref="InstancedModelRenderer.TriplanarProbe"/> on the one member that carries 88 % of the
        /// arena's cost, so a sweep can split that member's pixel shader up rather than only turning members
        /// off. Only the Testbed's <c>capprobe=</c> argument moves it; see <see cref="Members"/> for why an
        /// instrument like this is kept rather than deleted with the answer it gave.
        /// </summary>
        public int CapTriplanarProbe
        {
            get => _capRenderer.TriplanarProbe;
            set => _capRenderer.TriplanarProbe = value;
        }

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
            if ((Members & ArenaMembers.Cap) != 0) _capRenderer.Draw(camera, _world, sceneParams);
            if ((Members & ArenaMembers.Drum) != 0) _bodyRenderer.Draw(camera, _world, sceneParams);
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

            if (SceneRenderer.IsSolidTerrainScene(scene) && (Members & ArenaMembers.Pit) != 0)
                _pitRenderer.Draw(camera, _drainWorld, sceneParams);

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

            if ((Members & ArenaMembers.Rims) != 0) _funnelRimsRenderer.Draw(camera, _drainWorld, _funnelRimEffectParams);
            if ((Members & ArenaMembers.Glass) != 0) _funnelRenderer.Draw(camera, _drainWorld, sceneParams);

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
