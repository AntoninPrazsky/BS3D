using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Where the sea's two oil platforms stand (#969), and the figures a camera needs to frame one (#1124): the chapter
    /// intro's run in at the nearer one is laid out from these, and <see cref="OilRigs"/> builds its mesh from the same.
    /// <para>
    /// <b>Where they stand.</b> Beyond <c>SeaSceneConfig.WaveFadeEnd</c> (780), where the water is a flat plane at
    /// <c>LevelY</c> whoever looks (the swell calms with the distance from the arena as well as from the lens, see
    /// <c>SeaSceneConfig.CalmFrom</c>) and the cut at the waterline is the line the water agrees with: the nearer
    /// <see cref="NEAR_DISTANCE"/> out on <see cref="NEAR_BEARING"/>, a second, smaller, further on another bearing. Both
    /// stand AHEAD of the play view the Game and the Testbed open on (the lens on +Z looking towards -Z), in its background
    /// about 21 degrees either side of the cluster, the nearer on the left. Sizes at about two units a metre, the scenes'
    /// own: a jacket's legs some twenty metres apart, its deck twenty-five metres over the water, its derrick another
    /// forty.
    /// </para>
    /// </summary>
    public static class OilRigLayout
    {
        private const float NEAR_DISTANCE = 820f, NEAR_BEARING = -1.95f;
        private const float FAR_DISTANCE = 1150f, FAR_BEARING = -1.2f, FAR_SCALE = 0.8f;

        /// <summary>The lower deck's middle over the water, in a rig's own units (a rig at scale 1 is in world units).</summary>
        public const float DECK_Y = 50f;

        /// <summary>The lower deck's half thickness: its underside is this far under <see cref="DECK_Y"/>.</summary>
        public const float DECK_HALF_THICKNESS = 3f;

        /// <summary>Between two neighbouring legs' axes.</summary>
        public const float LEG_SPAN = 42f;

        /// <summary>A leg's half width.</summary>
        public const float LEG_HALF = 4.4f;

        /// <summary>The derrick's axis on the deck, in the rig's own frame (its X and Z).</summary>
        public static readonly Vector2 DERRICK_AXIS = new(8f, -8f);

        /// <summary>The derrick's foot over the water: it stands on the upper deck.</summary>
        public const float DERRICK_FOOT_Y = DECK_Y + 16f;

        /// <summary>The top of the derrick's cap over the water (its lamp stands on it).</summary>
        public const float DERRICK_TOP_Y = DECK_Y + 66f;

        /// <summary>The top of the jacket's bracing over the water: under it there is nothing but the jacket and, on High and
        /// Ultra's finer mesh, its boat landing, its stair tower and its pipes.</summary>
        public const float JACKET_TOP_Y = 40f;

        /// <summary>How far from the jacket's axis anything under <see cref="JACKET_TOP_Y"/> stands, down where the water meets it
        /// and a lens low over the water flies: the corners of the boat landing High and Ultra's finer mesh hangs on the jacket's
        /// -X face (the legs' outer corners stand some 36 off it).</summary>
        public const float JACKET_REACH = 37.5f;

        /// <summary>The nearer rig, standing on water at <paramref name="waterY"/>.</summary>
        public static OilRig Near(float waterY) => new(At(NEAR_BEARING, NEAR_DISTANCE, waterY), NEAR_BEARING + 0.6f, 1f);

        /// <summary>The farther, smaller rig, standing on water at <paramref name="waterY"/>.</summary>
        public static OilRig Far(float waterY) => new(At(FAR_BEARING, FAR_DISTANCE, waterY), FAR_BEARING - 0.9f, FAR_SCALE);

        private static Vector3 At(float bearing, float distance, float waterY) =>
            new(MathF.Cos(bearing) * distance, waterY, MathF.Sin(bearing) * distance);
    }

    /// <summary>One oil platform as it stands: where its jacket's axis meets the water, its turn about the vertical and its scale.</summary>
    public readonly struct OilRig
    {
        /// <summary>Where the jacket's axis meets the water.</summary>
        public Vector3 Foot { get; }

        /// <summary>Its turn about the vertical, radians.</summary>
        public float Turn { get; }

        /// <summary>Its size against the layout's figures.</summary>
        public float Scale { get; }

        /// <param name="foot">Where the jacket's axis meets the water.</param>
        /// <param name="turn">Its turn about the vertical, radians.</param>
        /// <param name="scale">Its size against the layout's figures.</param>
        public OilRig(Vector3 foot, float turn, float scale)
        {
            Foot = foot;
            Turn = turn;
            Scale = scale;
        }

        /// <summary>The rig's own frame into the world: its figures (<see cref="OilRigLayout"/>) are in this frame.</summary>
        public Matrix Frame => Matrix.CreateScale(Scale) * Matrix.CreateRotationY(Turn) * Matrix.CreateTranslation(Foot);

        /// <summary>A point of the rig's own frame in the world.</summary>
        public Vector3 ToWorld(Vector3 local) => Vector3.Transform(local, Frame);

        /// <summary>The derrick's foot, on the upper deck.</summary>
        public Vector3 DerrickFoot => ToWorld(new Vector3(OilRigLayout.DERRICK_AXIS.X, OilRigLayout.DERRICK_FOOT_Y, OilRigLayout.DERRICK_AXIS.Y));

        /// <summary>The top of the derrick.</summary>
        public Vector3 DerrickTop => ToWorld(new Vector3(OilRigLayout.DERRICK_AXIS.X, OilRigLayout.DERRICK_TOP_Y, OilRigLayout.DERRICK_AXIS.Y));

        /// <summary>The lower deck's slab's underside, world Y (the finer mesh hangs girders under it).</summary>
        public float DeckUnderside => Foot.Y + (OilRigLayout.DECK_Y - OilRigLayout.DECK_HALF_THICKNESS) * Scale;

        /// <summary>The top of the jacket's bracing, world Y (<see cref="OilRigLayout.JACKET_TOP_Y"/>).</summary>
        public float JacketTop => Foot.Y + OilRigLayout.JACKET_TOP_Y * Scale;

        /// <summary>
        /// How far from the jacket's axis anything under <see cref="JacketTop"/> stands (<see cref="OilRigLayout.JACKET_REACH"/>):
        /// the boat landing's outer corners on High and Ultra's finer mesh, the legs' outer corners (some 36) on the coarser.
        /// The bracing runs between the legs, and every girder, boom, module, lifeboat and lamp stands higher, on or under the deck.
        /// </summary>
        public float JacketReach => OilRigLayout.JACKET_REACH * Scale;
    }

    /// <summary>Which piece of a platform a part belongs to: what the tests ask of it (the intro's lens looks at the derrick, and
    /// anything else may stand in its way).</summary>
    internal enum RigPiece { Jacket, Deck, Module, Window, Derrick, Crane, Flare, Helideck, Lamp, Railing, Stair, Pipe, Lifeboat }

    /// <summary>How a part is built inside its box.</summary>
    internal enum RigShape
    {
        /// <summary>The box itself.</summary>
        Box,

        /// <summary>
        /// A tube of <see cref="RigPart.Sides"/> sides along the box's own X, its corners on the box's Y and Z axes and so its faces
        /// turned half a side off them: no face of a tube lies parallel to a deck, a wall or another part's box.
        /// </summary>
        Tube,

        /// <summary>The helideck: an eight-sided slab across the box's X and Z, its top marked (<see cref="RigVertex.HELIPAD"/>).</summary>
        Helideck,
    }

    /// <summary>
    /// One part of a platform: a box <see cref="Half"/> either way along <see cref="World"/>'s own axes about its origin, or the
    /// shape <see cref="Shape"/> inside that box, in <see cref="Color"/> and of <see cref="Kind"/> (<see cref="RigVertex"/>).
    /// </summary>
    internal readonly record struct RigPart(Matrix World, Vector3 Half, Vector3 Color, float Kind, RigShape Shape, int Sides, RigPiece Piece);

    /// <summary>
    /// The sea's oil platforms (#969): the two rigs <see cref="OilRigLayout"/> places, shaded by <c>Rig.fx</c>, which cuts them at
    /// the waterline (the surface writes no depth) and puts foam round the legs. The lighthouse and the two vessels
    /// (<see cref="SeaTraffic"/>, #1125) draw through the same effect.
    /// <para>
    /// <b>Two meshes of one rig</b> (#1169), both in the rig's own frame, each rig drawn with its own <c>World</c>. The coarser,
    /// every tier's: four legs with their bracing, a deck of two levels with its modules, a derrick whose lattice the shader
    /// paints, a crane, a flare boom with its flame, a helideck and a few steady lamps, 47 boxes. The finer, drawn where
    /// <see cref="SceneRenderer.FineMeshes"/> is on (High and Ultra) and the lens stands within <see cref="FINE_REACH"/> of the
    /// rig: what the references (<c>C:\Users\panrd\AI\sd\out\1169-zimage</c>, <c>1169-klein</c>, <c>1169-flux3</c>) show reading at
    /// the chapter intro's run, 250 to 140 off the nearer one: round legs fouled at the splash zone, a jacket of tubes braced in
    /// plan as well, conductors and risers, a boat landing with its fenders and a stair tower up to the deck, girders under it,
    /// columns and bracing between the decks, windows and doors, railings round both decks, a stair between them, a real lattice
    /// derrick on its drill floor, a crane with a lattice boom, its hook and gantry, a truss flare boom, an eight-sided helideck
    /// on posts with its net, markings and lights, lifeboats on their davits, a pipe rack, tanks and a container. Farther than
    /// <see cref="FINE_REACH"/> a rig is drawn coarse on every tier: from the play view, 800 and more out, a railing is under a
    /// pixel, and the coarser is what keeps no two faces within a depth step there.
    /// </para>
    /// <para>
    /// <b>No two faces of different paint lie in one plane facing one way</b> (<c>OilRigTests</c>): the depth buffer cannot tell
    /// them apart and they fight. On the coarser mesh nothing comes within <see cref="COARSE_GAP"/> of that (a depth step at the
    /// farther rig's far corner from the play view is under 1.9); on the finer, drawn only near, nothing that can be seen within
    /// <see cref="FINE_GAP"/> over more than a speck (a step at <see cref="FINE_REACH"/> plus the rig's own reach is 0.18). ⚠ The
    /// lower modules stood into the upper deck's slab, their ends in its end faces' planes, white and orange against grey, and
    /// flickered (#1169): they stand under it now and short of its ends.
    /// </para>
    /// </summary>
    internal sealed class OilRigs : IDisposable
    {
        //Lighter than the water's own haze (700), or a rig a kilometre off is gone
        private const float HAZE = 1700f;

        /// <summary>How near the lens, horizontally, a rig's foot must stand for the rig to be drawn in its finer mesh, where
        /// the tier draws finer meshes at all: the intro's run passes the nearer within 250, and the play view stands 800 and
        /// more from either.</summary>
        internal const float FINE_REACH = 300f;

        /// <summary>The nearest two same-facing faces of different paint may lie to one plane where both can be seen, world
        /// units, on the coarser mesh and on the finer (<c>OilRigTests</c> hold both).</summary>
        internal const float COARSE_GAP = 2.5f, FINE_GAP = 0.3f;

        //The kinds Rig.fx reads off a vertex's alpha
        private const float PAINTED = RigVertex.PAINTED, LATTICE = RigVertex.LATTICE, LIGHT = RigVertex.LIGHT;

        private static readonly Vector3 STEEL = new(0.30f, 0.31f, 0.32f);
        private static readonly Vector3 GRATING = new(0.24f, 0.25f, 0.25f);
        private static readonly Vector3 LEG_YELLOW = new(0.55f, 0.40f, 0.06f);
        private static readonly Vector3 RAIL_YELLOW = new(0.62f, 0.47f, 0.07f);
        private static readonly Vector3 FOULED = new(0.16f, 0.13f, 0.07f);
        private static readonly Vector3 FENDER = new(0.05f, 0.05f, 0.05f);
        private static readonly Vector3 DECK_GREY = new(0.38f, 0.39f, 0.40f);
        private static readonly Vector3 MODULE_WHITE = new(0.62f, 0.63f, 0.62f);
        private static readonly Vector3 MODULE_ORANGE = new(0.60f, 0.20f, 0.04f);
        private static readonly Vector3 GLASS = new(0.04f, 0.05f, 0.06f);
        private static readonly Vector3 WINDOW_LIT = new(0.30f, 0.22f, 0.12f);
        private static readonly Vector3 CONTAINER_BLUE = new(0.07f, 0.15f, 0.30f);
        private static readonly Vector3 HELIDECK = new(0.40f, 0.10f, 0.08f);
        private static readonly Vector3 NET = new(0.06f, 0.08f, 0.06f);
        private static readonly Vector3 DERRICK_RED = new(0.50f, 0.07f, 0.05f);
        private static readonly Vector3 LIFEBOAT = new(0.65f, 0.22f, 0.03f);
        private static readonly Vector3 FLAME = new(1.00f, 0.42f, 0.08f);
        private static readonly Vector3 LAMP = new(1.00f, 0.92f, 0.75f);
        private static readonly Vector3 RED_LIGHT = new(1.00f, 0.12f, 0.06f);
        private static readonly Vector3 GREEN_LIGHT = new(0.25f, 1.00f, 0.35f);

        //The rig's own frame for the meshes, which each draw stands where it is
        private static readonly OilRig LOCAL = new(Vector3.Zero, 0f, 1f);

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly EffectParameter _world, _view, _projection, _cameraPosition, _sunDirection, _sunColor, _zenith, _horizon, _waterY, _sinkFrom;
        private readonly VertexBuffer _vertices;
        private readonly int _coarseTriangles, _fineStart, _fineTriangles;
        private readonly Vector3 _nearFoot, _farFoot;
        private readonly Matrix _nearFrame, _farFrame;

        public OilRigs(GraphicsDevice graphicsDevice, ContentManager content, SeaSceneConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _effect = LoadEffect(content);
            _world = _effect.Parameters["World"];
            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _sunDirection = _effect.Parameters["SunDirection"];
            _sunColor = _effect.Parameters["SunColor"];
            _zenith = _effect.Parameters["ZenithColor"];
            _horizon = _effect.Parameters["HorizonColor"];
            _waterY = _effect.Parameters["WaterY"];
            _sinkFrom = _effect.Parameters["SinkFrom"];

            List<RigVertex> coarse = Mesh(LOCAL, false), fine = Mesh(LOCAL, true);
            _coarseTriangles = coarse.Count / 3;
            _fineStart = coarse.Count;
            _fineTriangles = fine.Count / 3;
            var all = new RigVertex[coarse.Count + fine.Count];
            coarse.CopyTo(all, 0);
            fine.CopyTo(all, _fineStart);
            _vertices = new VertexBuffer(graphicsDevice, RigVertex.Declaration, all.Length, BufferUsage.WriteOnly);
            _vertices.SetData(all);

            OilRig near = OilRigLayout.Near(config.LevelY), far = OilRigLayout.Far(config.LevelY);
            _nearFoot = near.Foot;
            _farFoot = far.Foot;
            _nearFrame = near.Frame;
            _farFrame = far.Frame;
        }

        /// <summary>
        /// <c>Rig.fx</c> as the content manager caches it, its once-only values set: the air's haze and how slowly a vessel
        /// sinks under the horizon (<see cref="SeaTrafficLayout.SINK_SCALE"/>). The platforms and <see cref="SeaTraffic"/> draw
        /// through the one effect, so each sets what its own draw needs before it draws, where the sink starts among it
        /// (<see cref="SeaTrafficLayout.NO_SINK"/> for what stands still).
        /// </summary>
        internal static Effect LoadEffect(ContentManager content)
        {
            Effect effect = content.Load<Effect>("Shaders/Rig");
            effect.Parameters["RigHaze"].SetValue(HAZE);
            effect.Parameters["SinkScale"].SetValue(SeaTrafficLayout.SINK_SCALE);
            return effect;
        }

        /// <summary>Whether a rig whose jacket stands on <paramref name="foot"/> is drawn in its finer mesh from
        /// <paramref name="lens"/>: where the tier draws finer meshes (<paramref name="fineMeshes"/>) and within
        /// <see cref="FINE_REACH"/>, or at any distance under <see cref="RenderDetail.Maximum"/> (a filming run, #1178, which must
        /// not see a rig change its mesh as a lens flies in).</summary>
        internal static bool DrawsFine(bool fineMeshes, Vector3 lens, Vector3 foot) =>
            fineMeshes && (RenderDetail.Maximum
                || Vector2.DistanceSquared(new Vector2(lens.X, lens.Z), new Vector2(foot.X, foot.Z)) < FINE_REACH * FINE_REACH);

        /// <summary>A rig's triangles, the coarser mesh or the finer, wound clockwise seen from outside: in the world for a rig
        /// that stands in it, in its own frame for <see cref="LOCAL"/> (what the buffer holds).</summary>
        internal static List<RigVertex> Mesh(OilRig rig, bool fine)
        {
            var v = new List<RigVertex>();
            Parts(rig, fine, part => Emit(v, part));
            return v;
        }

        /// <summary>
        /// Every part of one rig, coarser or finer, in the world <paramref name="rig"/> stands in. The mesh is built from this,
        /// and the tests hold the chapter intro's lens and the vessels' lanes off the same parts.
        /// </summary>
        internal static void Parts(OilRig rig, bool fine, Action<RigPart> part)
        {
            var b = new Builder(rig.Frame, part);
            Jacket(b, fine);
            Decks(b, fine);
            Derrick(b, fine);
            Crane(b, fine);
            Flare(b, fine);
            Helideck(b, fine);
            Lamps(b);
            if (!fine) return;

            BoatLanding(b);
            Railings(b);
            Lifeboats(b);
        }

        private const float DECK_Y = OilRigLayout.DECK_Y, LEG_SPAN = OilRigLayout.LEG_SPAN, LEG_HALF = OilRigLayout.LEG_HALF;
        private const float HALF_SPAN = LEG_SPAN * 0.5f;

        //Where the legs start under the water, where the splash zone's fouling ends on them, and where they end inside the
        //lower deck's slab: deep enough in that their tops stand more than COARSE_GAP under its top on the farther rig too
        private const float LEG_FOOT = -12f, SPLASH_TOP = 7f, LEG_TOP = DECK_Y - 1f;

        //The lower deck's top and underside, the upper deck's underside and top
        private const float LOWER_TOP = DECK_Y + OilRigLayout.DECK_HALF_THICKNESS, LOWER_UNDER = DECK_Y - OilRigLayout.DECK_HALF_THICKNESS;
        private const float UPPER_UNDER = DECK_Y + 10.5f, UPPER_TOP = DECK_Y + 15.5f;

        //The bracing's storeys: two of STOREY from BRACING_FOOT to the jacket's top
        private const float BRACING_FOOT = 4f, STOREY = (OilRigLayout.JACKET_TOP_Y - BRACING_FOOT) * 0.5f;

        /// <summary>The legs from under the water to the deck, and two storeys of bracing between them.</summary>
        private static void Jacket(Builder b, bool fine)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -HALF_SPAN : HALF_SPAN, z = (i & 2) == 0 ? -HALF_SPAN : HALF_SPAN;
                if (!fine)
                {
                    b.Box(new Vector3(x, (LEG_FOOT + LEG_TOP) * 0.5f, z), new Vector3(LEG_HALF, (LEG_TOP - LEG_FOOT) * 0.5f, LEG_HALF), LEG_YELLOW, PAINTED, RigPiece.Jacket);
                    continue;
                }

                //Round, and fouled from under the water to SPLASH_TOP: the splash zone's growth and rust, darker than the paint
                b.Tube(new Vector3(x, LEG_FOOT, z), new Vector3(x, SPLASH_TOP, z), LEG_HALF, 12, FOULED, RigPiece.Jacket);
                b.Tube(new Vector3(x, SPLASH_TOP, z), new Vector3(x, LEG_TOP, z), LEG_HALF, 12, LEG_YELLOW, RigPiece.Jacket);
            }

            //The bracing: on every side and in two storeys a horizontal tube and an X of two diagonals (as panels of lattice they
            //read as perforated sheets), each side a frame along X at z = -HALF_SPAN turned to its place
            float diagonal = MathF.Atan2(STOREY, LEG_SPAN), diagonalHalf = MathF.Sqrt(STOREY * STOREY + LEG_SPAN * LEG_SPAN) * 0.5f;
            for (int side = 0; side < 4; side++)
            {
                Matrix sideTurn = Matrix.CreateTranslation(0f, 0f, -HALF_SPAN) * Matrix.CreateRotationY(side * MathHelper.PiOver2);
                Vector3 S(float x, float y) => Vector3.Transform(new Vector3(x, y, 0f), sideTurn);
                foreach (float y in new[] { BRACING_FOOT, BRACING_FOOT + STOREY })
                {
                    if (!fine)
                    {
                        void Member(Vector3 centre, Vector3 half, float tilt) =>
                            b.BoxIn(Matrix.CreateRotationZ(tilt) * Matrix.CreateTranslation(centre) * sideTurn, half, STEEL, PAINTED, RigPiece.Jacket);
                        Member(new Vector3(0f, y + STOREY, 0f), new Vector3(HALF_SPAN, 0.7f, 0.7f), 0f);
                        Member(new Vector3(0f, y + STOREY * 0.5f, 0f), new Vector3(diagonalHalf, 0.55f, 0.55f), diagonal);
                        Member(new Vector3(0f, y + STOREY * 0.5f, 0f), new Vector3(diagonalHalf, 0.55f, 0.55f), -diagonal);
                        continue;
                    }

                    b.Tube(S(-HALF_SPAN, y + STOREY), S(HALF_SPAN, y + STOREY), 0.75f, 8, STEEL, RigPiece.Jacket);
                    b.Tube(S(-HALF_SPAN, y), S(HALF_SPAN, y + STOREY), 0.6f, 8, STEEL, RigPiece.Jacket);
                    b.Tube(S(-HALF_SPAN, y + STOREY), S(HALF_SPAN, y), 0.6f, 8, STEEL, RigPiece.Jacket);
                }

                //The finer jacket's foot: a horizontal at the bracing's foot as well
                if (fine) b.Tube(S(-HALF_SPAN, BRACING_FOOT), S(HALF_SPAN, BRACING_FOOT), 0.75f, 8, STEEL, RigPiece.Jacket);
            }
            if (!fine) return;

            //Braced in plan at each storey's top, corner to corner
            foreach (float y in new[] { BRACING_FOOT + STOREY, BRACING_FOOT + 2f * STOREY })
            {
                b.Tube(new Vector3(-HALF_SPAN, y, -HALF_SPAN), new Vector3(HALF_SPAN, y, HALF_SPAN), 0.55f, 8, STEEL, RigPiece.Jacket);
                b.Tube(new Vector3(HALF_SPAN, y, -HALF_SPAN), new Vector3(-HALF_SPAN, y, HALF_SPAN), 0.55f, 8, STEEL, RigPiece.Jacket);
            }

            //The wells' conductors under the derrick, held in a guide frame at each plan level, and two risers up the jacket's
            //side clamped to its horizontals
            Vector2 well = OilRigLayout.DERRICK_AXIS;
            foreach (float dx in new[] { -4f, 0f, 4f })
                foreach (float dz in new[] { -3f, 3f })
                    b.Tube(new Vector3(well.X + dx, LEG_FOOT, well.Y + dz), new Vector3(well.X + dx, LOWER_UNDER, well.Y + dz), 0.9f, 8, STEEL, RigPiece.Pipe);
            foreach (float y in new[] { BRACING_FOOT + STOREY, BRACING_FOOT + 2f * STOREY })
            {
                float x0 = well.X - 5.6f, x1 = well.X + 5.6f, z0 = well.Y - 4.6f, z1 = well.Y + 4.6f;
                b.Bar(new Vector3(x0, y, z0), new Vector3(x1, y, z0), 0.3f, STEEL, RigPiece.Pipe);
                b.Bar(new Vector3(x0, y, z1), new Vector3(x1, y, z1), 0.3f, STEEL, RigPiece.Pipe);
                b.Bar(new Vector3(x0, y, z0), new Vector3(x0, y, z1), 0.3f, STEEL, RigPiece.Pipe);
                b.Bar(new Vector3(x1, y, z0), new Vector3(x1, y, z1), 0.3f, STEEL, RigPiece.Pipe);
            }
            foreach (float z in new[] { 9f, 12.5f })
            {
                b.Tube(new Vector3(HALF_SPAN + 6.4f, LEG_FOOT, z), new Vector3(HALF_SPAN + 6.4f, LOWER_UNDER, z), 0.8f, 8, MODULE_WHITE, RigPiece.Pipe);
                foreach (float y in new[] { BRACING_FOOT + STOREY, BRACING_FOOT + 2f * STOREY })
                    b.Bar(new Vector3(HALF_SPAN, y, z), new Vector3(HALF_SPAN + 6.4f, y, z), 0.25f, STEEL, RigPiece.Pipe);
            }
        }

        /// <summary>
        /// The deck: a lower and an upper level overhanging the jacket, modules between them and on the upper. ⚠ The lower
        /// modules stand under the upper deck and short of its ends (#1169): when they stood into its slab their ends lay in its
        /// end faces' planes, white and orange against grey, and fought for the depth.
        /// </summary>
        private static void Decks(Builder b, bool fine)
        {
            b.Box(new Vector3(0f, DECK_Y, 0f), new Vector3(36f, OilRigLayout.DECK_HALF_THICKNESS, 32f), DECK_GREY, PAINTED, RigPiece.Deck);
            b.Box(new Vector3(-6f, (UPPER_UNDER + UPPER_TOP) * 0.5f, -2f), new Vector3(30f, (UPPER_TOP - UPPER_UNDER) * 0.5f, 26f), DECK_GREY, PAINTED, RigPiece.Deck);

            //Between the decks, from the lower's top to the upper's underside: a white module 2 short of the upper deck's +Z end,
            //an orange 2 short of its -Z end and out past its +X end. On the upper deck a white and an orange.
            float between = (LOWER_TOP + UPPER_UNDER) * 0.5f, betweenHalf = (UPPER_UNDER - LOWER_TOP) * 0.5f;
            b.Box(new Vector3(-20f, between, 13f), new Vector3(12f, betweenHalf, 9f), MODULE_WHITE, PAINTED, RigPiece.Module);
            b.Box(new Vector3(14f, between, -15f), new Vector3(16f, betweenHalf, 11f), MODULE_ORANGE, PAINTED, RigPiece.Module);
            b.Box(new Vector3(-14f, UPPER_TOP + 4.5f, -12f), new Vector3(10f, 4.5f, 9f), MODULE_WHITE, PAINTED, RigPiece.Module);
            b.Box(new Vector3(6f, UPPER_TOP + 3.5f, 8f), new Vector3(7f, 3.5f, 7f), MODULE_ORANGE, PAINTED, RigPiece.Module);
            if (!fine) return;

            //Girders under the lower deck along the legs' lines and the middle, both ways
            foreach (float at in new[] { -HALF_SPAN, 0f, HALF_SPAN })
            {
                b.Box(new Vector3(0f, LOWER_UNDER - 1f, at), new Vector3(36f, 1f, 0.7f), STEEL, PAINTED, RigPiece.Deck);
                b.Box(new Vector3(at, LOWER_UNDER - 1f, 0f), new Vector3(0.7f, 1f, 32f), STEEL, PAINTED, RigPiece.Deck);
            }

            //Columns between the decks along the three open sides, and an X between each two
            Vector2[][] sides =
            {
                new[] { new Vector2(-35f, -27f), new Vector2(-19f, -27f), new Vector2(-4f, -27f) },
                new[] { new Vector2(-35f, -27f), new Vector2(-35f, -12f), new Vector2(-35f, 2f) },
                new[] { new Vector2(-6f, 23f), new Vector2(8f, 23f), new Vector2(22f, 23f) },
            };
            var columns = new HashSet<Vector2>();
            foreach (Vector2[] side in sides)
                for (int i = 0; i < side.Length; i++)
                {
                    if (columns.Add(side[i]))
                        b.Tube(new Vector3(side[i].X, LOWER_TOP, side[i].Y), new Vector3(side[i].X, UPPER_UNDER, side[i].Y), 0.9f, 8, STEEL, RigPiece.Deck);
                    if (i == 0) continue;
                    Vector2 p = side[i - 1], q = side[i];
                    b.Tube(new Vector3(p.X, LOWER_TOP + 0.5f, p.Y), new Vector3(q.X, UPPER_UNDER - 0.5f, q.Y), 0.45f, 8, STEEL, RigPiece.Deck);
                    b.Tube(new Vector3(p.X, UPPER_UNDER - 0.5f, p.Y), new Vector3(q.X, LOWER_TOP + 0.5f, q.Y), 0.45f, 8, STEEL, RigPiece.Deck);
                }

            //Windows and doors: dark glass standing WINDOW_PROUD out of the wall, a few lit
            int lit = 0;
            void Windows(Vector3 wallMiddle, Vector3 outward, Vector3 along, float halfLength, float[] rows, float floor, bool door)
            {
                int count = (int)((halfLength * 2f - 1.5f) / WINDOW_PITCH);
                for (int i = 0; i < count; i++)
                {
                    float offset = (i - (count - 1) * 0.5f) * WINDOW_PITCH;
                    foreach (float y in rows)
                    {
                        bool on = lit++ % 5 == 2;
                        b.Window(wallMiddle + along * offset + Vector3.UnitY * (y - wallMiddle.Y), outward, new Vector2(0.6f, 0.5f),
                            on ? WINDOW_LIT : GLASS, on ? LIGHT : PAINTED);
                    }
                }
                if (door)
                    b.Window(new Vector3(wallMiddle.X, floor + DOOR_SILL + 1.1f, wallMiddle.Z) + along * (halfLength - 1.4f), outward, new Vector2(0.6f, 1.1f), GLASS, PAINTED);
            }
            float[] lowerRow = { LOWER_TOP + 4.5f }, upperRows = { UPPER_TOP + 3.5f, UPPER_TOP + 6.8f };
            Windows(new Vector3(-20f, between, 22f), Vector3.UnitZ, Vector3.UnitX, 12f, lowerRow, LOWER_TOP, false);
            Windows(new Vector3(-32f, between, 13f), -Vector3.UnitX, Vector3.UnitZ, 9f, lowerRow, LOWER_TOP, true);
            Windows(new Vector3(-14f, UPPER_TOP + 4.5f, -3f), Vector3.UnitZ, Vector3.UnitX, 10f, upperRows, UPPER_TOP, true);
            Windows(new Vector3(-14f, UPPER_TOP + 4.5f, -21f), -Vector3.UnitZ, Vector3.UnitX, 10f, upperRows, UPPER_TOP, false);
            Windows(new Vector3(-24f, UPPER_TOP + 4.5f, -12f), -Vector3.UnitX, Vector3.UnitZ, 9f, upperRows, UPPER_TOP, false);
            Windows(new Vector3(-4f, UPPER_TOP + 4.5f, -12f), Vector3.UnitX, Vector3.UnitZ, 9f, upperRows, UPPER_TOP, false);

            //The orange modules' doors
            b.Window(new Vector3(-2f, LOWER_TOP + DOOR_SILL + 1.1f, -9f), -Vector3.UnitX, new Vector2(0.6f, 1.1f), GLASS, PAINTED);
            b.Window(new Vector3(6f, UPPER_TOP + DOOR_SILL + 1.1f, 15f), Vector3.UnitZ, new Vector2(0.6f, 1.1f), GLASS, PAINTED);

            //On the upper deck's open end two tanks on their saddles and a container
            foreach (float z in new[] { 6f, 10.5f })
            {
                b.Tube(new Vector3(-31f, UPPER_TOP + 2.4f, z), new Vector3(-19f, UPPER_TOP + 2.4f, z), 1.7f, 12, MODULE_WHITE, RigPiece.Module);
                foreach (float x in new[] { -28.5f, -21.5f })
                    b.Box(new Vector3(x, UPPER_TOP + 0.5f, z), new Vector3(0.4f, 0.5f, 1.3f), STEEL, PAINTED, RigPiece.Module);
            }
            b.Box(new Vector3(-13f, UPPER_TOP + 1.3f, 18.5f), new Vector3(3f, 1.3f, 1.2f), CONTAINER_BLUE, PAINTED, RigPiece.Module);

            //A pipe rack along the lower deck's +Z side on its supports
            foreach (float x in new[] { -32f, -24f, -16f, -9f })
                b.Box(new Vector3(x, LOWER_TOP + 0.55f, 26.9f), new Vector3(0.25f, 0.55f, 1.9f), STEEL, PAINTED, RigPiece.Pipe);
            foreach (float z in new[] { 25.5f, 26.9f, 28.3f })
                b.Tube(new Vector3(-34f, LOWER_TOP + 1.6f, z), new Vector3(-8f, LOWER_TOP + 1.6f, z), 0.45f, 8, z < 26f ? MODULE_WHITE : STEEL, RigPiece.Pipe);

            //A stair from the lower deck up to the upper on the +Z side, landing at the upper deck's end
            b.Flight(new Vector3(-4f, LOWER_TOP, 27f), new Vector3(12f, UPPER_TOP, 27f));
            b.Box(new Vector3(13.25f, UPPER_TOP - 0.15f, 26.2f), new Vector3(1.25f, 0.15f, 2.2f), GRATING, PAINTED, RigPiece.Stair);
        }

        //The windows' spacing along a wall and how far they stand out of it: proud of the wall by more than FINE_GAP on either rig
        //(the farther is 0.8 the size), so a window never fights the wall behind it
        private const float WINDOW_PITCH = 3f, WINDOW_PROUD = 0.8f;

        //A door's sill over the floor: its foot more than FINE_GAP over the module's, which stands on the deck
        private const float DOOR_SILL = 0.7f;

        //How far the helideck's top stands over the upper deck's
        private const float HELIDECK_RISE = 3.8f;

        /// <summary>
        /// The derrick over the upper deck, its cap's top at <see cref="OilRigLayout.DERRICK_TOP_Y"/>: the coarser a tower of two
        /// stages narrowing as it rises whose lattice the shader paints; the finer a real lattice on its drill floor, four legs
        /// tapering to the crown, girts at every bay and an X on every face of every bay, a monkey board on one face.
        /// </summary>
        private static void Derrick(Builder b, bool fine)
        {
            float x = OilRigLayout.DERRICK_AXIS.X, z = OilRigLayout.DERRICK_AXIS.Y, foot = OilRigLayout.DERRICK_FOOT_Y;
            b.Box(new Vector3(x, OilRigLayout.DERRICK_TOP_Y - 3f, z), new Vector3(2.4f, 3f, 2.4f), MODULE_WHITE, PAINTED, RigPiece.Derrick);
            if (!fine)
            {
                b.Box(new Vector3(x, foot + 14f, z), new Vector3(6f, 14f, 6f), DERRICK_RED, LATTICE, RigPiece.Derrick);
                b.Box(new Vector3(x, foot + 36f, z), new Vector3(4f, 8f, 4f), DERRICK_RED, LATTICE, RigPiece.Derrick);
                return;
            }

            const int BAYS = 9;
            const float FLOOR_TOP = UPPER_TOP + 1.5f, CROWN = OilRigLayout.DERRICK_TOP_Y - 6f, BASE_HALF = 6f, CROWN_HALF = 2.5f;
            b.Box(new Vector3(x, (UPPER_TOP + FLOOR_TOP) * 0.5f, z), new Vector3(8f, (FLOOR_TOP - UPPER_TOP) * 0.5f, 8f), STEEL, PAINTED, RigPiece.Derrick);

            Vector3 Corner(int level, int corner)
            {
                float t = level / (float)BAYS, half = MathHelper.Lerp(BASE_HALF, CROWN_HALF, t);
                float sx = corner == 0 || corner == 3 ? -1f : 1f, sz = corner < 2 ? -1f : 1f;
                return new Vector3(x + sx * half, MathHelper.Lerp(FLOOR_TOP, CROWN, t), z + sz * half);
            }

            for (int corner = 0; corner < 4; corner++)
                b.Tube(Corner(0, corner), Corner(BAYS, corner), 0.45f, 8, DERRICK_RED, RigPiece.Derrick);
            //The girts from the first bay's top to the last's foot (the legs stand on the floor, the crown on them), an X on
            //every face of every bay
            for (int level = 0; level <= BAYS; level++)
                for (int face = 0; face < 4; face++)
                {
                    if (level > 0 && level < BAYS) b.Bar(Corner(level, face), Corner(level, (face + 1) % 4), 0.22f, DERRICK_RED, RigPiece.Derrick);
                    if (level == BAYS) continue;
                    b.Bar(Corner(level, face), Corner(level + 1, (face + 1) % 4), 0.16f, DERRICK_RED, RigPiece.Derrick);
                    b.Bar(Corner(level, (face + 1) % 4), Corner(level + 1, face), 0.16f, DERRICK_RED, RigPiece.Derrick);
                }

            //The monkey board two thirds up, out from the -Z face over a girt
            Vector3 board = (Corner(6, 0) + Corner(6, 1)) * 0.5f;
            b.Box(board + new Vector3(0f, 0.7f, -1.1f), new Vector3(1.8f, 0.12f, 1f), GRATING, PAINTED, RigPiece.Derrick);
        }

        /// <summary>
        /// The crane: a pedestal at the lower deck's +X +Z corner and its boom raised over the water; the finer with its cab, a
        /// lattice boom of four chords laced on every face, its gantry and pendant, the hoist line and hook, a red light at the tip.
        /// </summary>
        private static void Crane(Builder b, bool fine)
        {
            if (!fine)
            {
                //The pedestal turned an eighth of a turn, so no face of it lies parallel to the boom's that runs into it
                b.BoxIn(Matrix.CreateRotationY(MathHelper.PiOver4) * Matrix.CreateTranslation(30f, DECK_Y + 8f, 26f), new Vector3(2.2f, 5f, 2.2f), MODULE_ORANGE, PAINTED, RigPiece.Crane);
                b.Box(new Vector3(42f, DECK_Y + 20f, 26f), new Vector3(16f, 0.9f, 0.9f), LEG_YELLOW, PAINTED, RigPiece.Crane, 0.65f);
                return;
            }

            b.Tube(new Vector3(30f, LOWER_TOP, 26f), new Vector3(30f, DECK_Y + 13f, 26f), 2.2f, 12, MODULE_ORANGE, RigPiece.Crane);
            b.Box(new Vector3(30f, DECK_Y + 13.4f, 26f), new Vector3(3.2f, 0.4f, 3.2f), STEEL, PAINTED, RigPiece.Crane);
            b.Box(new Vector3(30.6f, DECK_Y + 15.8f, 27.6f), new Vector3(2.4f, 2f, 1.3f), LEG_YELLOW, PAINTED, RigPiece.Crane);
            b.Window(new Vector3(33f, DECK_Y + 16.3f, 27.6f), Vector3.UnitX, new Vector2(0.6f, 0.6f), GLASS, PAINTED);

            //The boom beside the cab, its chords clear of the cab's side by more than FINE_GAP
            Vector3 heel = new(31f, DECK_Y + 14f, BOOM_Z), tip = new(54.6f, DECK_Y + 29.7f, BOOM_Z);
            Lattice(b, heel, tip, 1.0f, 0.35f, 8, 0.16f, 0.09f, LEG_YELLOW, RigPiece.Crane);

            //The gantry behind the cab and its pendant to the tip, the hoist line down to the hook over the water
            Vector3 gantry = new(28.2f, DECK_Y + 22f, BOOM_Z);
            b.Bar(new Vector3(28.6f, DECK_Y + 13.8f, BOOM_Z), gantry, 0.25f, LEG_YELLOW, RigPiece.Crane);
            b.Bar(gantry, tip, 0.05f, STEEL, RigPiece.Crane);
            Vector3 hook = new(tip.X, DECK_Y + 7f, tip.Z);
            b.Bar(tip, hook, 0.05f, STEEL, RigPiece.Crane);
            b.Box(hook, new Vector3(0.45f, 0.6f, 0.3f), LEG_YELLOW, PAINTED, RigPiece.Crane);
            b.Box(tip + Vector3.UnitY * 0.9f, new Vector3(0.4f, 0.4f, 0.4f), RED_LIGHT, LIGHT, RigPiece.Lamp);
        }

        //The plane the finer crane's boom stands in, beside its cab
        private const float BOOM_Z = 24.4f;

        /// <summary>The flare: a long boom out over the water at a slant from the lower deck's -X edge, its flame at the end; the
        /// finer boom a truss tapering to the flare tip.</summary>
        private static void Flare(Builder b, bool fine)
        {
            //The flame turned an eighth of a turn, so no face of it lies parallel to the boom's that runs into it
            Vector3 flame = new(-80f, DECK_Y + 32f, 0f);
            b.BoxIn(Matrix.CreateRotationY(MathHelper.PiOver4) * Matrix.CreateTranslation(flame), new Vector3(2.6f, 3.6f, 2.6f), FLAME, LIGHT, RigPiece.Flare);
            if (!fine)
            {
                b.Box(new Vector3(-58f, DECK_Y + 16f, 0f), new Vector3(26f, 1.4f, 1.4f), STEEL, PAINTED, RigPiece.Flare, -0.55f);
                return;
            }

            //The coarser boom's own axis, so the flame stands where it did; a second tongue of it, smaller and leaning
            Vector3 root = new(-58f + 26f * MathF.Cos(-0.55f), DECK_Y + 16f + 26f * MathF.Sin(-0.55f), 0f);
            Vector3 end = new(-58f - 26f * MathF.Cos(-0.55f), DECK_Y + 16f - 26f * MathF.Sin(-0.55f), 0f);
            Lattice(b, root, end, 1.6f, 0.8f, 10, 0.2f, 0.1f, STEEL, RigPiece.Flare);
            b.Tube(end, end + Vector3.UnitY * 2.4f, 0.7f, 8, STEEL, RigPiece.Flare);
            b.BoxIn(Matrix.CreateRotationZ(0.35f) * Matrix.CreateTranslation(flame + new Vector3(-1.2f, 2.6f, 0.5f)), new Vector3(1.4f, 2.4f, 1.4f), FLAME, LIGHT, RigPiece.Flare);
        }

        /// <summary>
        /// The helideck over the upper deck's -Z +X corner; the finer an eight-sided slab marked on its top (<c>Rig.fx</c>'s
        /// HELIPAD), its net sloping out round the edge, green lights at its corners, beams under it and three posts down to the
        /// lower deck.
        /// </summary>
        private static void Helideck(Builder b, bool fine)
        {
            //Its top HELIDECK_RISE over the upper deck's, more than COARSE_GAP, where the two overlap seen from above
            Vector3 centre = new(26f, UPPER_TOP + HELIDECK_RISE - 0.6f, -24f);
            if (!fine)
            {
                b.Box(centre, new Vector3(12f, 0.6f, 12f), HELIDECK, PAINTED, RigPiece.Helideck);
                return;
            }

            const float RADIUS = 13f, HALF_THICKNESS = 0.6f;
            b.Helideck(centre, RADIUS, HALF_THICKNESS, STEEL);
            float under = centre.Y - HALF_THICKNESS, top = centre.Y + HALF_THICKNESS, apothem = RADIUS * MathF.Cos(MathF.PI / 8f);
            foreach (float turn in new[] { 0f, MathHelper.PiOver4, MathHelper.PiOver2, 3f * MathHelper.PiOver4 })
            {
                Vector3 way = new(MathF.Cos(turn), 0f, MathF.Sin(turn));
                b.Bar(centre - way * (apothem - 0.5f) - Vector3.UnitY * (HALF_THICKNESS + 0.4f), centre + way * (apothem - 0.5f) - Vector3.UnitY * (HALF_THICKNESS + 0.4f),
                    0.4f, STEEL, RigPiece.Helideck);
            }

            //The net round the edge, each panel sloping out and down, and the lights at the corners
            for (int k = 0; k < 8; k++)
            {
                float mid = (k + 1) * MathHelper.PiOver4, corner = MathF.PI / 8f + k * MathHelper.PiOver4;
                Vector3 outward = new(MathF.Cos(mid), 0f, MathF.Sin(mid)), along = Vector3.Cross(Vector3.Up, outward);
                Matrix panel = Matrix.CreateRotationX(0.2f) * new Matrix(along.X, along.Y, along.Z, 0f, 0f, 1f, 0f, 0f, outward.X, outward.Y, outward.Z, 0f,
                    centre.X + outward.X * (apothem + 0.8f), top - 0.25f, centre.Z + outward.Z * (apothem + 0.8f), 1f);
                b.BoxIn(panel, new Vector3(RADIUS * MathF.Sin(MathF.PI / 8f), 0.05f, 0.8f), NET, PAINTED, RigPiece.Helideck);
                b.Box(new Vector3(centre.X + MathF.Cos(corner) * (RADIUS - 0.4f), top + 0.25f, centre.Z + MathF.Sin(corner) * (RADIUS - 0.4f)),
                    new Vector3(0.25f, 0.25f, 0.25f), GREEN_LIGHT, LIGHT, RigPiece.Lamp);
            }

            //Three posts down to the lower deck, braced
            Vector3[] posts = { new(35f, LOWER_TOP, -18f), new(35f, LOWER_TOP, -30f), new(28f, LOWER_TOP, -31f) };
            float postTop = under;
            foreach (Vector3 post in posts)
                b.Tube(post, new Vector3(post.X, postTop, post.Z), 0.7f, 8, STEEL, RigPiece.Helideck);
            b.Tube(posts[0] + Vector3.UnitY * 0.5f, new Vector3(posts[1].X, postTop - 0.5f, posts[1].Z), 0.4f, 8, STEEL, RigPiece.Helideck);
            b.Tube(posts[2] + Vector3.UnitY * 0.5f, new Vector3(posts[1].X, postTop - 0.5f, posts[1].Z), 0.4f, 8, STEEL, RigPiece.Helideck);
        }

        /// <summary>
        /// The steady lamps (#401): at the lower deck's corners, over its railing's corner posts, and a beacon standing on the
        /// derrick's cap. Each top stands more than <see cref="COARSE_GAP"/> over the top it stands over, on either rig.
        /// </summary>
        private static void Lamps(Builder b)
        {
            foreach (float x in new[] { -36f, 36f })
                foreach (float z in new[] { -32f, 32f })
                    b.Box(new Vector3(x, LOWER_TOP + 3f, z), new Vector3(0.9f, 0.9f, 0.9f), LAMP, LIGHT, RigPiece.Lamp);
            Vector2 derrick = OilRigLayout.DERRICK_AXIS;
            b.Box(new Vector3(derrick.X, OilRigLayout.DERRICK_TOP_Y + 2f, derrick.Y), new Vector3(0.9f, 2f, 0.9f), LAMP, LIGHT, RigPiece.Derrick);
        }

        /// <summary>
        /// The boat landing on the jacket's -X face just over the water, its fenders, and the stair tower up from it to a hatch
        /// in the lower deck's underside: five flights zigzagging in one lane.
        /// </summary>
        private static void BoatLanding(Builder b)
        {
            const float LANDING = 4.5f, OUT = -31f, IN = -26.5f;
            b.Box(new Vector3((OUT + IN) * 0.5f, LANDING - 0.3f, 0f), new Vector3((IN - OUT) * 0.5f, 0.3f, HALF_SPAN), RAIL_YELLOW, PAINTED, RigPiece.Stair);
            foreach (float z in new[] { -19f, -7f, 7f, 19f })
                b.Bar(new Vector3(IN, LANDING - 0.3f, z), new Vector3(-HALF_SPAN, LANDING - 0.3f, z), 0.3f, STEEL, RigPiece.Stair);
            foreach (float z in new[] { -18f, -9f, 0f, 9f, 18f })
                b.Tube(new Vector3(OUT - 0.6f, -3f, z), new Vector3(OUT - 0.6f, LANDING + 1f, z), 0.5f, 8, FENDER, RigPiece.Stair);

            const float LANE = -29.5f, NEAR_END = 6f, FAR_END = 17f;
            const int FLIGHTS = 5;
            float rise = (LOWER_UNDER - LANDING) / FLIGHTS;
            for (int f = 0; f < FLIGHTS; f++)
            {
                float y0 = LANDING + f * rise, y1 = y0 + rise;
                bool outward = f % 2 == 0;
                b.Flight(new Vector3(LANE, y0, outward ? NEAR_END : FAR_END), new Vector3(LANE, y1, outward ? FAR_END : NEAR_END));
                if (f == FLIGHTS - 1) continue;
                float end = outward ? FAR_END + 0.75f : NEAR_END - 0.75f;
                b.Box(new Vector3(LANE, y1 - 0.15f, end), new Vector3(0.9f, 0.15f, 0.75f), GRATING, PAINTED, RigPiece.Stair);
            }
            foreach (float px in new[] { LANE - 1.1f, LANE + 1.1f })
                foreach (float pz in new[] { NEAR_END - 1.7f, FAR_END + 1.7f })
                    b.Tube(new Vector3(px, LANDING, pz), new Vector3(px, LOWER_UNDER, pz), 0.3f, 8, STEEL, RigPiece.Stair);
        }

        /// <summary>Railings round both decks, posts and two rails, open where the flare boom leaves the lower deck, where the stair
        /// lands on the upper and under the helideck.</summary>
        private static void Railings(Builder b)
        {
            const float L = 35.4f, LZ = 31.4f, UX0 = -35.4f, UX1 = 23.4f, UZ0 = -27.4f, UZ1 = 23.4f;
            b.Railing(new Vector3(-L, LOWER_TOP, LZ), new Vector3(L, LOWER_TOP, LZ));
            b.Railing(new Vector3(-L, LOWER_TOP, -LZ), new Vector3(L, LOWER_TOP, -LZ));
            b.Railing(new Vector3(L, LOWER_TOP, -LZ), new Vector3(L, LOWER_TOP, LZ));
            b.Railing(new Vector3(-L, LOWER_TOP, -LZ), new Vector3(-L, LOWER_TOP, -3f));
            b.Railing(new Vector3(-L, LOWER_TOP, 3f), new Vector3(-L, LOWER_TOP, LZ));

            b.Railing(new Vector3(UX0, UPPER_TOP, UZ1), new Vector3(11.5f, UPPER_TOP, UZ1));
            b.Railing(new Vector3(15f, UPPER_TOP, UZ1), new Vector3(UX1, UPPER_TOP, UZ1));
            b.Railing(new Vector3(UX0, UPPER_TOP, UZ0), new Vector3(12f, UPPER_TOP, UZ0));
            b.Railing(new Vector3(UX1, UPPER_TOP, -10f), new Vector3(UX1, UPPER_TOP, UZ1));
            b.Railing(new Vector3(UX0, UPPER_TOP, UZ0), new Vector3(UX0, UPPER_TOP, UZ1));
        }

        /// <summary>Four enclosed lifeboats hung on their davits over the lower deck's long sides, under its edge: an orange
        /// canopy rounded off at both ends over a white hull.</summary>
        private static void Lifeboats(Builder b)
        {
            foreach ((float x, float side) in new[] { (-26f, 1f), (-12f, 1f), (-18f, -1f), (-4f, -1f) })
            {
                float z = side * 35.5f, deckZ = side * 31.8f, hull = LOWER_UNDER + 2f;
                b.Tube(new Vector3(x - 6.5f, hull, z), new Vector3(x + 6.5f, hull, z), 1.9f, 8, LIFEBOAT, RigPiece.Lifeboat);
                foreach (float end in new[] { -1f, 1f })
                    b.Tube(new Vector3(x + end * 6.5f, hull - 0.2f, z), new Vector3(x + end * 7.7f, hull - 0.2f, z), 1.3f, 8, LIFEBOAT, RigPiece.Lifeboat);
                b.Box(new Vector3(x, hull - 1.3f, z), new Vector3(6f, 0.75f, 1.45f), MODULE_WHITE, PAINTED, RigPiece.Lifeboat);
                foreach (float dx in new[] { -4.5f, 4.5f })
                {
                    Vector3 post = new(x + dx, LOWER_TOP, deckZ), head = new(x + dx, LOWER_TOP + 2.5f, deckZ), over = new(x + dx, LOWER_TOP + 2.5f, z);
                    b.Bar(post, head, 0.18f, STEEL, RigPiece.Lifeboat);
                    b.Bar(head, over, 0.18f, STEEL, RigPiece.Lifeboat);
                    b.Bar(over, new Vector3(x + dx, hull + 1.9f, z), 0.05f, STEEL, RigPiece.Lifeboat);
                }
            }
        }

        /// <summary>
        /// A lattice boom or truss from <paramref name="from"/> to <paramref name="to"/>: four chords of a square section
        /// <paramref name="fromHalf"/> either way at its root and <paramref name="toHalf"/> at its end, laced on every face in a
        /// zigzag over <paramref name="bays"/> bays.
        /// </summary>
        private static void Lattice(Builder b, Vector3 from, Vector3 to, float fromHalf, float toHalf, int bays, float chordHalf, float lacingHalf, Vector3 color, RigPiece piece)
        {
            Vector3 way = Vector3.Normalize(to - from);
            Vector3 across = Vector3.Normalize(Vector3.Cross(way, Vector3.Up)), up = Vector3.Cross(across, way);
            Vector3 At(int bay, int chord)
            {
                float t = bay / (float)bays, half = MathHelper.Lerp(fromHalf, toHalf, t);
                float a = chord == 0 || chord == 3 ? -1f : 1f, u = chord < 2 ? -1f : 1f;
                return Vector3.Lerp(from, to, t) + across * (a * half) + up * (u * half);
            }

            for (int chord = 0; chord < 4; chord++)
                b.Bar(At(0, chord), At(bays, chord), chordHalf, color, piece);
            for (int bay = 0; bay < bays; bay++)
                for (int face = 0; face < 4; face++)
                {
                    int c0 = face, c1 = (face + 1) % 4;
                    if (bay % 2 == 0) b.Bar(At(bay, c0), At(bay + 1, c1), lacingHalf, color, piece);
                    else b.Bar(At(bay, c1), At(bay + 1, c0), lacingHalf, color, piece);
                }
        }

        /// <summary>Hands the parts of one rig to the caller in the world the rig stands in, from figures in the rig's own frame.</summary>
        private sealed class Builder
        {
            private readonly Matrix _frame;
            private readonly Action<RigPart> _part;

            public Builder(Matrix frame, Action<RigPart> part)
            {
                _frame = frame;
                _part = part;
            }

            /// <summary>A box about <paramref name="centre"/>, turned <paramref name="tilt"/> about the rig's Z.</summary>
            public void Box(Vector3 centre, Vector3 half, Vector3 color, float kind, RigPiece piece, float tilt = 0f) =>
                BoxIn(Matrix.CreateRotationZ(tilt) * Matrix.CreateTranslation(centre), half, color, kind, piece);

            /// <summary>A box <paramref name="half"/> either way along <paramref name="local"/>'s axes, in the rig's frame.</summary>
            public void BoxIn(Matrix local, Vector3 half, Vector3 color, float kind, RigPiece piece) =>
                _part(new RigPart(local * _frame, half, color, kind, RigShape.Box, 0, piece));

            /// <summary>A square bar from <paramref name="a"/> to <paramref name="b"/>, <paramref name="halfWidth"/> either way across.</summary>
            public void Bar(Vector3 a, Vector3 b, float halfWidth, Vector3 color, RigPiece piece) =>
                _part(new RigPart(Along(a, b) * _frame, new Vector3(Vector3.Distance(a, b) * 0.5f, halfWidth, halfWidth), color, PAINTED, RigShape.Box, 0, piece));

            /// <summary>A tube of <paramref name="sides"/> sides from <paramref name="a"/> to <paramref name="b"/>.</summary>
            public void Tube(Vector3 a, Vector3 b, float radius, int sides, Vector3 color, RigPiece piece) =>
                _part(new RigPart(Along(a, b) * _frame, new Vector3(Vector3.Distance(a, b) * 0.5f, radius, radius), color, PAINTED, RigShape.Tube, sides, piece));

            /// <summary>The helideck's slab about <paramref name="centre"/>, its corners <paramref name="radius"/> out.</summary>
            public void Helideck(Vector3 centre, float radius, float halfThickness, Vector3 color) =>
                _part(new RigPart(Matrix.CreateTranslation(centre) * _frame, new Vector3(radius, halfThickness, radius), color, PAINTED, RigShape.Helideck, 8, RigPiece.Helideck));

            /// <summary>A window or a door on a wall: <paramref name="half"/> along the wall and up it about
            /// <paramref name="onWall"/>, standing <see cref="WINDOW_PROUD"/> out of it and as far in.</summary>
            public void Window(Vector3 onWall, Vector3 outward, Vector2 half, Vector3 color, float kind)
            {
                Vector3 along = Vector3.Cross(Vector3.Up, outward);
                Matrix frame = new(along.X, along.Y, along.Z, 0f, 0f, 1f, 0f, 0f, outward.X, outward.Y, outward.Z, 0f,
                    onWall.X, onWall.Y, onWall.Z, 1f);
                BoxIn(frame, new Vector3(half.X, half.Y, WINDOW_PROUD), color, kind, RigPiece.Window);
            }

            /// <summary>A flight of stairs from <paramref name="low"/> to <paramref name="high"/> (level ends, the run horizontal):
            /// two stringers, treads every 0.75 of rise, a handrail either side.</summary>
            public void Flight(Vector3 low, Vector3 high)
            {
                Vector3 run = high - low;
                run.Y = 0f;
                Vector3 across = Vector3.Normalize(Vector3.Cross(Vector3.Normalize(run), Vector3.Up)) * 0.7f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 s = across * side;
                    Bar(low + s - Vector3.UnitY * 0.25f, high + s - Vector3.UnitY * 0.25f, 0.12f, STEEL, RigPiece.Stair);
                    Bar(low + s * 1.05f + Vector3.UnitY * 1.1f, high + s * 1.05f + Vector3.UnitY * 1.1f, 0.08f, RAIL_YELLOW, RigPiece.Railing);
                }

                int treads = Math.Max(2, (int)MathF.Round((high.Y - low.Y) / 0.75f));
                for (int i = 1; i < treads; i++)
                {
                    Vector3 at = Vector3.Lerp(low, high, i / (float)treads);
                    Vector3 x = Vector3.Normalize(across), z = Vector3.Cross(x, Vector3.Up);
                    Matrix frame = new(x.X, 0f, x.Z, 0f, 0f, 1f, 0f, 0f, z.X, 0f, z.Z, 0f, at.X, at.Y - 0.05f, at.Z, 1f);
                    BoxIn(frame, new Vector3(0.68f, 0.05f, 0.3f), GRATING, PAINTED, RigPiece.Stair);
                }
            }

            /// <summary>A railing along a deck's edge from <paramref name="a"/> to <paramref name="b"/> (at the deck's top): posts
            /// at most 4.5 apart, a top rail at 2.2 and a mid rail at 1.1.</summary>
            public void Railing(Vector3 a, Vector3 b)
            {
                int spans = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(a, b) / 4.5f));
                for (int i = 0; i <= spans; i++)
                    Box(Vector3.Lerp(a, b, i / (float)spans) + Vector3.UnitY * 1.1f, new Vector3(0.12f, 1.1f, 0.12f), RAIL_YELLOW, PAINTED, RigPiece.Railing);
                Bar(a + Vector3.UnitY * 2.2f, b + Vector3.UnitY * 2.2f, 0.12f, RAIL_YELLOW, RigPiece.Railing);
                Bar(a + Vector3.UnitY * 1.1f, b + Vector3.UnitY * 1.1f, 0.08f, RAIL_YELLOW, RigPiece.Railing);
            }
        }

        //A frame whose X runs from a to b about their middle, its Y as near the vertical as the X allows
        private static Matrix Along(Vector3 a, Vector3 b)
        {
            Vector3 x = Vector3.Normalize(b - a), z = Vector3.Cross(x, Vector3.Up);
            z = z.LengthSquared() < 1e-6f ? Vector3.UnitZ : Vector3.Normalize(z);
            Vector3 y = Vector3.Cross(z, x), m = (a + b) * 0.5f;
            return new Matrix(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, m.X, m.Y, m.Z, 1f);
        }

        /// <summary>One part as triangles through its world matrix, wound clockwise seen from outside.</summary>
        internal static void Emit(List<RigVertex> v, RigPart part)
        {
            switch (part.Shape)
            {
                case RigShape.Tube: EmitTube(v, part); break;
                case RigShape.Helideck: EmitHelideck(v, part); break;
                default: EmitBox(v, part); break;
            }
        }

        //A box as six quads, its normals turned with it
        private static void EmitBox(List<RigVertex> v, RigPart part)
        {
            Color c = Paint(part.Color, part.Kind);
            Vector3 half = part.Half;
            Vector3[] axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            for (int a = 0; a < 3; a++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = axes[a] * sign, u = axes[(a + 1) % 3], w = axes[(a + 2) % 3];
                    float d = Vector3.Dot(half, axes[a]), hu = Vector3.Dot(half, u), hw = Vector3.Dot(half, w);
                    Vector3 middle = n * d;
                    Vector3 p00 = middle - u * hu - w * hw, p10 = middle + u * hu - w * hw, p11 = middle + u * hu + w * hw, p01 = middle - u * hu + w * hw;
                    Vector3 wn = Vector3.Normalize(Vector3.TransformNormal(n, part.World));
                    Quad(v, P(p00, part), P(p10, part), P(p11, part), P(p01, part), wn, c);
                }
        }

        //A tube along the part's X: its corners at whole steps round from its Y, its sides lit smooth round it, shut at both ends
        private static void EmitTube(List<RigVertex> v, RigPart part)
        {
            Color c = Paint(part.Color, part.Kind);
            int n = part.Sides;
            float h = part.Half.X, r = MathF.Min(part.Half.Y, part.Half.Z);
            Vector3 Ring(int k, float x) { float a = k * MathHelper.TwoPi / n; return new Vector3(x, MathF.Cos(a) * r, MathF.Sin(a) * r); }
            Vector3 Normal(float a) => Vector3.Normalize(Vector3.TransformNormal(new Vector3(0f, MathF.Cos(a), MathF.Sin(a)), part.World));
            Vector3 endA = P(new Vector3(-h, 0f, 0f), part), endB = P(new Vector3(h, 0f, 0f), part);
            Vector3 axisNormal = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, part.World));
            for (int k = 0; k < n; k++)
            {
                float a0 = k * MathHelper.TwoPi / n, a1 = (k + 1) * MathHelper.TwoPi / n;
                Vector3 b0 = P(Ring(k, -h), part), b1 = P(Ring(k + 1, -h), part), t0 = P(Ring(k, h), part), t1 = P(Ring(k + 1, h), part);
                Vector3 n0 = Normal(a0), n1 = Normal(a1), face = Normal((a0 + a1) * 0.5f);
                Triangle(v, b0, b1, t1, n0, n1, n1, face, c, c, c);
                Triangle(v, b0, t1, t0, n0, n1, n0, face, c, c, c);
                Triangle(v, endA, b1, b0, -axisNormal, -axisNormal, -axisNormal, -axisNormal, c, c, c);
                Triangle(v, endB, t0, t1, axisNormal, axisNormal, axisNormal, axisNormal, c, c, c);
            }
        }

        //The helideck: an eight-sided slab, its corners an eighth of a turn round from the part's X and Z; its top marked, each
        //vertex carrying where on the deck it is (u, v from 0 to 1 across it) in its red and green for Rig.fx to draw from
        private static void EmitHelideck(List<RigVertex> v, RigPart part)
        {
            const int N = 8;
            Color c = Paint(part.Color, part.Kind);
            float r = part.Half.X, h = part.Half.Y;
            Vector3 Corner(int k, float y) { float a = MathF.PI / N + k * MathHelper.TwoPi / N; return new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r); }
            Color Mark(Vector3 local) => new(new Vector4(0.5f + 0.5f * local.X / r, 0.5f + 0.5f * local.Z / r, 0f, RigVertex.HELIPAD));
            Vector3 up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, part.World));
            Vector3 top = new(0f, h, 0f), bottom = new(0f, -h, 0f);
            for (int k = 0; k < N; k++)
            {
                Vector3 a0 = Corner(k, h), a1 = Corner(k + 1, h), d0 = Corner(k, -h), d1 = Corner(k + 1, -h);
                Triangle(v, P(top, part), P(a0, part), P(a1, part), up, up, up, up, Mark(top), Mark(a0), Mark(a1));
                Triangle(v, P(bottom, part), P(d1, part), P(d0, part), -up, -up, -up, -up, c, c, c);
                float mid = MathF.PI / N + (k + 0.5f) * MathHelper.TwoPi / N;
                Vector3 side = Vector3.Normalize(Vector3.TransformNormal(new Vector3(MathF.Cos(mid), 0f, MathF.Sin(mid)), part.World));
                Quad(v, P(d0, part), P(d1, part), P(a1, part), P(a0, part), side, c);
            }
        }

        private static Vector3 P(Vector3 local, RigPart part) => Vector3.Transform(local, part.World);

        private static Color Paint(Vector3 color, float kind) => new(new Vector4(color, kind));

        //A flat quad lit by n, as two triangles
        private static void Quad(List<RigVertex> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color color)
        {
            Triangle(v, a, b, c, n, n, n, n, color, color, color);
            Triangle(v, a, c, d, n, n, n, n, color, color, color);
        }

        //A triangle wound clockwise seen from outside (CLAUDE.md "Triangle winding"): its (b - a) x (c - a) points away from
        //facing, the way its face looks out
        private static void Triangle(List<RigVertex> v, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 facing,
            Color ca, Color cb, Color cc)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) > 0f)
            {
                (b, c) = (c, b);
                (nb, nc) = (nc, nb);
                (cb, cc) = (cc, cb);
            }
            v.Add(new RigVertex(a, na, ca));
            v.Add(new RigVertex(b, nb, cb));
            v.Add(new RigVertex(c, nc, cc));
        }

        /// <summary>
        /// Draws the rigs, opaque and writing depth, after the water, each in its finer mesh where <paramref name="fineMeshes"/>
        /// and the lens's distance say (<see cref="DrawsFine"/>). Leaves the states as it found them.
        /// </summary>
        public void Draw(in SceneFrame frame, float waterY, bool fineMeshes)
        {
            //The rigs stand still, so they never sink under the horizon from any lens; the effect is shared with SeaTraffic,
            //which sets a part's own frame and its sink there
            _sinkFrom.SetValue(SeaTrafficLayout.NO_SINK);
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(frame.SunColor);
            _zenith.SetValue(frame.ZenithLinear);
            _horizon.SetValue(frame.HorizonLinear);
            _waterY.SetValue(waterY);

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState rasterizer = _graphicsDevice.RasterizerState;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Every part is shut and wound clockwise seen from outside, so the faces turned away are culled (MonoGame's default):
            //drawn CullNone, every member of the finer rig would be shaded twice where the intro's run fills the frame with it
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            _graphicsDevice.SetVertexBuffer(_vertices);

            Vector3 lens = frame.Camera.Position;
            DrawRig(_nearFrame, DrawsFine(fineMeshes, lens, _nearFoot));
            DrawRig(_farFrame, DrawsFine(fineMeshes, lens, _farFoot));

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;
        }

        private void DrawRig(Matrix world, bool fine)
        {
            _world.SetValue(world);
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, fine ? _fineStart : 0, fine ? _fineTriangles : _coarseTriangles);
        }

        public void Dispose() => _vertices?.Dispose();
    }

    /// <summary>
    /// A vertex of what <c>Rig.fx</c> shades (the oil platforms and <see cref="SeaTraffic"/>): position, normal, colour (rgb and,
    /// in alpha, the part's kind: <see cref="PAINTED"/>, <see cref="HELIPAD"/>, <see cref="LATTICE"/>, <see cref="CLOTH"/> or
    /// <see cref="LIGHT"/>).
    /// </summary>
    internal struct RigVertex : IVertexType
    {
        /// <summary>Plain painted steel, stone or hull: lit by the sun on its outer side and the sky.</summary>
        internal const float PAINTED = 0f;

        /// <summary>A helideck's top: its markings drawn by the shader, the colour's red and green carrying where on the deck the
        /// vertex is (u, v from 0 to 1 across it) rather than a colour.</summary>
        internal const float HELIPAD = 0.25f;

        /// <summary>A lattice: members crossing in bays over darker steel (the coarser derrick).</summary>
        internal const float LATTICE = 0.5f;

        /// <summary>Sailcloth: one sheet, lit on whichever side faces the lens and glowing where the sun is behind it.</summary>
        internal const float CLOTH = 0.8f;

        /// <summary>A light: its own colour, steady.</summary>
        internal const float LIGHT = 1f;

        public Vector3 Position;
        public Vector3 Normal;
        public Color Color;

        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

        VertexDeclaration IVertexType.VertexDeclaration => Declaration;

        public RigVertex(Vector3 position, Vector3 normal, Color color)
        {
            Position = position;
            Normal = normal;
            Color = color;
        }
    }
}
