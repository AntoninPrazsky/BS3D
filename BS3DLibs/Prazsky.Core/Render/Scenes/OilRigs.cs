using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The sea's oil platforms (#969): two rigs far out where the swell has faded flat, built of boxes - four legs with their
    /// bracing, a deck of two levels with its modules, a lattice derrick, a crane, a flare boom with its flame, a helideck and
    /// a few steady lamps - shaded by <c>Rig.fx</c>, which cuts them at the waterline (the surface writes no depth) and puts
    /// foam round the legs.
    /// <para>
    /// <b>Where they stand.</b> Beyond <c>SeaSceneConfig.WaveFadeEnd</c> (780), where the water is a flat plane at
    /// <c>LevelY</c> and the cut at the waterline is the line the water agrees with: the nearer <see cref="NEAR_DISTANCE"/>
    /// out on <see cref="NEAR_BEARING"/> (behind the play view the Testbed opens on), a second, smaller, further on another
    /// bearing. Sizes at about two units a metre, the scenes' own: a jacket's legs some twenty metres apart, its deck twenty-five
    /// metres over the water, its derrick another forty.
    /// </para>
    /// </summary>
    internal sealed class OilRigs : IDisposable
    {
        private const float NEAR_DISTANCE = 820f, NEAR_BEARING = -1.95f;
        private const float FAR_DISTANCE = 1150f, FAR_BEARING = -1.2f, FAR_SCALE = 0.8f;

        //Lighter than the water's own haze (700), or a rig a kilometre off is gone
        private const float HAZE = 1700f;

        //The kinds Rig.fx reads off a vertex's alpha
        private const float PAINTED = 0f, LATTICE = 0.5f, LIGHT = 1f;

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
        private readonly EffectParameter _view, _projection, _cameraPosition, _sunDirection, _sunColor, _zenith, _horizon, _waterY;
        private readonly VertexBuffer _vertices;
        private readonly int _triangles;

        public OilRigs(GraphicsDevice graphicsDevice, ContentManager content, SeaSceneConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _effect = content.Load<Effect>("Shaders/Rig");
            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _sunDirection = _effect.Parameters["SunDirection"];
            _sunColor = _effect.Parameters["SunColor"];
            _zenith = _effect.Parameters["ZenithColor"];
            _horizon = _effect.Parameters["HorizonColor"];
            _waterY = _effect.Parameters["WaterY"];
            _effect.Parameters["RigHaze"].SetValue(HAZE);

            var v = new List<VertexPositionNormalColor>();
            Rig(v, At(NEAR_BEARING, NEAR_DISTANCE, config.LevelY), NEAR_BEARING + 0.6f, 1f);
            Rig(v, At(FAR_BEARING, FAR_DISTANCE, config.LevelY), FAR_BEARING - 0.9f, FAR_SCALE);
            _triangles = v.Count / 3;
            _vertices = new VertexBuffer(graphicsDevice, VertexPositionNormalColor.Declaration, v.Count, BufferUsage.WriteOnly);
            _vertices.SetData(v.ToArray());
        }

        private static Vector3 At(float bearing, float distance, float waterY) =>
            new(MathF.Cos(bearing) * distance, waterY, MathF.Sin(bearing) * distance);

        /// <summary>One rig standing on the water at <paramref name="foot"/>, turned <paramref name="turn"/> about the vertical.</summary>
        private static void Rig(List<VertexPositionNormalColor> v, Vector3 foot, float turn, float scale)
        {
            Matrix frame = Matrix.CreateScale(scale) * Matrix.CreateRotationY(turn) * Matrix.CreateTranslation(foot);
            void Part(Vector3 centre, Vector3 half, Vector3 color, float kind, float tilt = 0f) =>
                Box(v, Matrix.CreateRotationZ(tilt) * Matrix.CreateTranslation(centre) * frame, half, color, kind);

            const float DECK_Y = 50f, LEG_SPAN = 42f;
            //The legs, from under the water to the deck, and two storeys of bracing between them
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -LEG_SPAN * 0.5f : LEG_SPAN * 0.5f, z = (i & 2) == 0 ? -LEG_SPAN * 0.5f : LEG_SPAN * 0.5f;
                Part(new Vector3(x, DECK_Y * 0.5f - 6f, z), new Vector3(4.4f, DECK_Y * 0.5f + 6f, 4.4f), LEG_YELLOW, PAINTED);
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
                        Box(v, Matrix.CreateRotationZ(tilt) * Matrix.CreateTranslation(centre) * sideTurn * frame, half, STEEL, PAINTED);
                    Member(new Vector3(0f, y + STOREY, 0f), new Vector3(LEG_SPAN * 0.5f, 0.7f, 0.7f), 0f);
                    Member(new Vector3(0f, y + STOREY * 0.5f, 0f), new Vector3(diagonalHalf, 0.55f, 0.55f), diagonal);
                    Member(new Vector3(0f, y + STOREY * 0.5f, 0f), new Vector3(diagonalHalf, 0.55f, 0.55f), -diagonal);
                }
            }

            //The deck: a lower and an upper level overhanging the jacket, modules on them
            Part(new Vector3(0f, DECK_Y, 0f), new Vector3(36f, 3f, 32f), DECK_GREY, PAINTED);
            Part(new Vector3(-6f, DECK_Y + 13f, -2f), new Vector3(30f, 2.5f, 26f), DECK_GREY, PAINTED);
            Part(new Vector3(-20f, DECK_Y + 7.5f, 14f), new Vector3(12f, 4.5f, 10f), MODULE_WHITE, PAINTED);
            Part(new Vector3(14f, DECK_Y + 7.5f, -16f), new Vector3(16f, 4.5f, 12f), MODULE_ORANGE, PAINTED);
            Part(new Vector3(-14f, DECK_Y + 20f, -12f), new Vector3(10f, 4.5f, 9f), MODULE_WHITE, PAINTED);
            Part(new Vector3(6f, DECK_Y + 19f, 8f), new Vector3(7f, 3.5f, 7f), MODULE_ORANGE, PAINTED);

            //The derrick: a lattice tower over the upper deck, narrowing as it rises (three stages)
            Part(new Vector3(8f, DECK_Y + 30f, -8f), new Vector3(6f, 14f, 6f), DERRICK_RED, LATTICE);
            Part(new Vector3(8f, DECK_Y + 52f, -8f), new Vector3(4f, 8f, 4f), DERRICK_RED, LATTICE);
            Part(new Vector3(8f, DECK_Y + 63f, -8f), new Vector3(2.4f, 3f, 2.4f), MODULE_WHITE, PAINTED);

            //The crane: a pedestal at a corner and its boom raised over the water
            Part(new Vector3(30f, DECK_Y + 8f, 26f), new Vector3(2.2f, 5f, 2.2f), MODULE_ORANGE, PAINTED);
            Part(new Vector3(42f, DECK_Y + 20f, 26f), new Vector3(16f, 0.9f, 0.9f), LEG_YELLOW, PAINTED, 0.65f);

            //The flare: a long boom out over the water at a slant, its flame at the end
            Part(new Vector3(-58f, DECK_Y + 16f, 0f), new Vector3(26f, 1.4f, 1.4f), STEEL, PAINTED, -0.55f);
            Part(new Vector3(-80f, DECK_Y + 32f, 0f), new Vector3(2.6f, 3.6f, 2.6f), FLAME, LIGHT);

            //The helideck at the far corner, and lamps at the deck's corners
            Part(new Vector3(26f, DECK_Y + 17f, -24f), new Vector3(12f, 0.6f, 12f), HELIDECK, PAINTED);
            foreach (var c in new[] { new Vector3(-36f, DECK_Y + 4f, -32f), new Vector3(36f, DECK_Y + 4f, -32f), new Vector3(-36f, DECK_Y + 4f, 32f), new Vector3(36f, DECK_Y + 4f, 32f), new Vector3(8f, DECK_Y + 67f, -8f) })
                Part(c, new Vector3(0.9f, 0.9f, 0.9f), LAMP, LIGHT);
        }

        //A box as six quads through `world`, its normals turned with it
        private static void Box(List<VertexPositionNormalColor> v, Matrix world, Vector3 half, Vector3 color, float kind)
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
                    VertexPositionNormalColor V(Vector3 p) => new(Vector3.Transform(p, world), wn, c);
                    v.Add(V(p00)); v.Add(V(p10)); v.Add(V(p11));
                    v.Add(V(p00)); v.Add(V(p11)); v.Add(V(p01));
                }
            }
        }

        /// <summary>Draws the rigs, opaque and writing depth, after the water. Leaves the states as it found them.</summary>
        public void Draw(in SceneFrame frame, float waterY)
        {
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

        /// <summary>Position, normal, colour (rgb and, in alpha, the part's kind).</summary>
        private struct VertexPositionNormalColor : IVertexType
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Color Color;

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;

            public VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color)
            {
                Position = position;
                Normal = normal;
                Color = color;
            }
        }
    }
}
