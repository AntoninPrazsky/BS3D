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

        /// <summary>The lower deck's underside, world Y: under it there is nothing but the jacket.</summary>
        public float DeckUnderside => Foot.Y + (OilRigLayout.DECK_Y - OilRigLayout.DECK_HALF_THICKNESS) * Scale;

        /// <summary>
        /// How far from the jacket's axis anything under <see cref="DeckUnderside"/> stands: the legs' outer corners. The bracing
        /// runs between the legs, and every boom, module and lamp stands on or over the deck.
        /// </summary>
        public float JacketReach => (OilRigLayout.LEG_SPAN * 0.5f + OilRigLayout.LEG_HALF) * MathF.Sqrt(2f) * Scale;
    }

    /// <summary>
    /// The sea's oil platforms (#969): the two rigs <see cref="OilRigLayout"/> places, built of boxes - four legs with their
    /// bracing, a deck of two levels with its modules, a lattice derrick, a crane, a flare boom with its flame, a helideck and
    /// a few steady lamps - shaded by <c>Rig.fx</c>, which cuts them at the waterline (the surface writes no depth) and puts
    /// foam round the legs. Their vertices are the world's (the effect's <c>World</c> is the identity for them); the lighthouse
    /// and the two vessels (<see cref="SeaTraffic"/>, #1125) draw through the same effect in their own frames.
    /// </summary>
    internal sealed class OilRigs : IDisposable
    {
        //Lighter than the water's own haze (700), or a rig a kilometre off is gone
        private const float HAZE = 1700f;

        //The kinds Rig.fx reads off a vertex's alpha
        private const float PAINTED = RigVertex.PAINTED, LATTICE = RigVertex.LATTICE, LIGHT = RigVertex.LIGHT;

        private static readonly Vector3 STEEL = new(0.30f, 0.31f, 0.32f);
        private static readonly Vector3 LEG_YELLOW = new(0.55f, 0.40f, 0.06f);
        private static readonly Vector3 DECK_GREY = new(0.38f, 0.39f, 0.40f);
        private static readonly Vector3 MODULE_WHITE = new(0.62f, 0.63f, 0.62f);
        private static readonly Vector3 MODULE_ORANGE = new(0.60f, 0.20f, 0.04f);
        private static readonly Vector3 HELIDECK = new(0.40f, 0.10f, 0.08f);
        private static readonly Vector3 DERRICK_RED = new(0.50f, 0.07f, 0.05f);
        private static readonly Vector3 FLAME = new(1.00f, 0.42f, 0.08f);
        private static readonly Vector3 LAMP = new(1.00f, 0.92f, 0.75f);

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly EffectParameter _world, _view, _projection, _cameraPosition, _sunDirection, _sunColor, _zenith, _horizon, _waterY;
        private readonly VertexBuffer _vertices;
        private readonly int _triangles;

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

            var v = new List<RigVertex>();
            void Add(Matrix world, Vector3 half, Vector3 color, float kind) => Box(v, world, half, color, kind);
            Parts(OilRigLayout.Near(config.LevelY), Add);
            Parts(OilRigLayout.Far(config.LevelY), Add);
            _triangles = v.Count / 3;
            _vertices = new VertexBuffer(graphicsDevice, RigVertex.Declaration, v.Count, BufferUsage.WriteOnly);
            _vertices.SetData(v.ToArray());
        }

        /// <summary>
        /// <c>Rig.fx</c> as the content manager caches it, its once-only values set: the air's haze and the horizon's sink
        /// (<see cref="SeaTrafficLayout.SINK_FROM"/>). The platforms and <see cref="SeaTraffic"/> draw through the one effect, so
        /// each sets what its own draw needs before it draws.
        /// </summary>
        internal static Effect LoadEffect(ContentManager content)
        {
            Effect effect = content.Load<Effect>("Shaders/Rig");
            effect.Parameters["RigHaze"].SetValue(HAZE);
            effect.Parameters["SinkFrom"].SetValue(SeaTrafficLayout.SINK_FROM);
            effect.Parameters["SinkScale"].SetValue(SeaTrafficLayout.SINK_SCALE);
            return effect;
        }

        /// <summary>
        /// Every box of one rig, in the order they are drawn: its world frame (the box is <c>half</c> either way along the
        /// frame's own axes about its origin), its half extents, its colour and its kind. The mesh is built from this, and the
        /// tests hold the chapter intro's lens off the same boxes.
        /// </summary>
        internal static void Parts(OilRig rig, Action<Matrix, Vector3, Vector3, float> box)
        {
            const float DECK_Y = OilRigLayout.DECK_Y, LEG_SPAN = OilRigLayout.LEG_SPAN, LEG_HALF = OilRigLayout.LEG_HALF;
            Matrix frame = rig.Frame;
            void Part(Vector3 centre, Vector3 half, Vector3 color, float kind, float tilt = 0f) =>
                box(Matrix.CreateRotationZ(tilt) * Matrix.CreateTranslation(centre) * frame, half, color, kind);

            //The legs, from under the water to the deck, and two storeys of bracing between them
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -LEG_SPAN * 0.5f : LEG_SPAN * 0.5f, z = (i & 2) == 0 ? -LEG_SPAN * 0.5f : LEG_SPAN * 0.5f;
                Part(new Vector3(x, DECK_Y * 0.5f - 6f, z), new Vector3(LEG_HALF, DECK_Y * 0.5f + 6f, LEG_HALF), LEG_YELLOW, PAINTED);
            }
            //The bracing: on every side and in two storeys a horizontal tube and an X of two diagonals, as tubes (as panels
            //of lattice they read as perforated sheets)
            const float STOREY = 18f;
            float diagonal = MathF.Atan2(STOREY, LEG_SPAN), diagonalHalf = MathF.Sqrt(STOREY * STOREY + LEG_SPAN * LEG_SPAN) * 0.5f;
            foreach (float y in new[] { 4f, 4f + STOREY })
            {
                for (int side = 0; side < 4; side++)
                {
                    //Each side as a frame along X at z = -LEG_SPAN / 2, turned to its place
                    Matrix sideTurn = Matrix.CreateTranslation(0f, 0f, -LEG_SPAN * 0.5f) * Matrix.CreateRotationY(side * MathHelper.PiOver2);
                    void Member(Vector3 centre, Vector3 half, float tilt) =>
                        box(Matrix.CreateRotationZ(tilt) * Matrix.CreateTranslation(centre) * sideTurn * frame, half, STEEL, PAINTED);
                    Member(new Vector3(0f, y + STOREY, 0f), new Vector3(LEG_SPAN * 0.5f, 0.7f, 0.7f), 0f);
                    Member(new Vector3(0f, y + STOREY * 0.5f, 0f), new Vector3(diagonalHalf, 0.55f, 0.55f), diagonal);
                    Member(new Vector3(0f, y + STOREY * 0.5f, 0f), new Vector3(diagonalHalf, 0.55f, 0.55f), -diagonal);
                }
            }

            //The deck: a lower and an upper level overhanging the jacket, modules on them
            Part(new Vector3(0f, DECK_Y, 0f), new Vector3(36f, OilRigLayout.DECK_HALF_THICKNESS, 32f), DECK_GREY, PAINTED);
            Part(new Vector3(-6f, DECK_Y + 13f, -2f), new Vector3(30f, 2.5f, 26f), DECK_GREY, PAINTED);
            Part(new Vector3(-20f, DECK_Y + 7.5f, 14f), new Vector3(12f, 4.5f, 10f), MODULE_WHITE, PAINTED);
            Part(new Vector3(14f, DECK_Y + 7.5f, -16f), new Vector3(16f, 4.5f, 12f), MODULE_ORANGE, PAINTED);
            Part(new Vector3(-14f, DECK_Y + 20f, -12f), new Vector3(10f, 4.5f, 9f), MODULE_WHITE, PAINTED);
            Part(new Vector3(6f, DECK_Y + 19f, 8f), new Vector3(7f, 3.5f, 7f), MODULE_ORANGE, PAINTED);

            //The derrick: a lattice tower over the upper deck, narrowing as it rises (three stages, the cap's top at
            //DERRICK_TOP_Y)
            float derrickX = OilRigLayout.DERRICK_AXIS.X, derrickZ = OilRigLayout.DERRICK_AXIS.Y, foot = OilRigLayout.DERRICK_FOOT_Y;
            Part(new Vector3(derrickX, foot + 14f, derrickZ), new Vector3(6f, 14f, 6f), DERRICK_RED, LATTICE);
            Part(new Vector3(derrickX, foot + 36f, derrickZ), new Vector3(4f, 8f, 4f), DERRICK_RED, LATTICE);
            Part(new Vector3(derrickX, OilRigLayout.DERRICK_TOP_Y - 3f, derrickZ), new Vector3(2.4f, 3f, 2.4f), MODULE_WHITE, PAINTED);

            //The crane: a pedestal at a corner and its boom raised over the water
            Part(new Vector3(30f, DECK_Y + 8f, 26f), new Vector3(2.2f, 5f, 2.2f), MODULE_ORANGE, PAINTED);
            Part(new Vector3(42f, DECK_Y + 20f, 26f), new Vector3(16f, 0.9f, 0.9f), LEG_YELLOW, PAINTED, 0.65f);

            //The flare: a long boom out over the water at a slant, its flame at the end
            Part(new Vector3(-58f, DECK_Y + 16f, 0f), new Vector3(26f, 1.4f, 1.4f), STEEL, PAINTED, -0.55f);
            Part(new Vector3(-80f, DECK_Y + 32f, 0f), new Vector3(2.6f, 3.6f, 2.6f), FLAME, LIGHT);

            //The helideck at the far corner, and lamps at the deck's corners and on the derrick's cap
            Part(new Vector3(26f, DECK_Y + 17f, -24f), new Vector3(12f, 0.6f, 12f), HELIDECK, PAINTED);
            foreach (var c in new[] { new Vector3(-36f, DECK_Y + 4f, -32f), new Vector3(36f, DECK_Y + 4f, -32f), new Vector3(-36f, DECK_Y + 4f, 32f), new Vector3(36f, DECK_Y + 4f, 32f), new Vector3(derrickX, OilRigLayout.DERRICK_TOP_Y + 1f, derrickZ) })
                Part(c, new Vector3(0.9f, 0.9f, 0.9f), LAMP, LIGHT);
        }

        //A box as six quads through `world`, its normals turned with it
        private static void Box(List<RigVertex> v, Matrix world, Vector3 half, Vector3 color, float kind)
        {
            var c = new Color(new Vector4(color, kind));
            Vector3[] axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            for (int a = 0; a < 3; a++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = axes[a] * sign, u = axes[(a + 1) % 3], w = axes[(a + 2) % 3];
                    float d = Vector3.Dot(half, axes[a]), hu = Vector3.Dot(half, u), hw = Vector3.Dot(half, w);
                    Vector3 middle = n * d;
                    Vector3 p00 = middle - u * hu - w * hw, p10 = middle + u * hu - w * hw, p11 = middle + u * hu + w * hw, p01 = middle - u * hu + w * hw;
                    Vector3 wn = Vector3.Normalize(Vector3.TransformNormal(n, world));
                    RigVertex V(Vector3 p) => new(Vector3.Transform(p, world), wn, c);
                    v.Add(V(p00)); v.Add(V(p10)); v.Add(V(p11));
                    v.Add(V(p00)); v.Add(V(p11)); v.Add(V(p01));
                }
            }
        }

        /// <summary>Draws the rigs, opaque and writing depth, after the water. Leaves the states as it found them.</summary>
        public void Draw(in SceneFrame frame, float waterY)
        {
            //The rigs' vertices are the world's; the effect is shared with SeaTraffic, which sets a part's own frame here
            _world.SetValue(Matrix.Identity);
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
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_vertices);
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangles);

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;
        }

        public void Dispose() => _vertices?.Dispose();
    }

    /// <summary>
    /// A vertex of what <c>Rig.fx</c> shades (the oil platforms and <see cref="SeaTraffic"/>): position, normal, colour (rgb and,
    /// in alpha, the part's kind: <see cref="PAINTED"/>, <see cref="LATTICE"/>, <see cref="CLOTH"/> or <see cref="LIGHT"/>).
    /// </summary>
    internal struct RigVertex : IVertexType
    {
        /// <summary>Plain painted steel, stone or hull: lit by the sun on its outer side and the sky.</summary>
        internal const float PAINTED = 0f;

        /// <summary>A lattice: members crossing in bays over darker steel (the derrick).</summary>
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
