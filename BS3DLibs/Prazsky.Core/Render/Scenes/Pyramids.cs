using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The desert's pyramids (#965): the Giza group far out over the dunes - Khufu, Khafre (on its higher ground, with the
    /// remains of its casing at the top), Menkaure, and the queens' small pyramids beside Khufu and Menkaure - shaded by
    /// <c>Pyramid.fx</c>. The owner reversed his own ruling of 2026-10-02 (#677, the desert stays dunes) for them.
    /// <para>
    /// <b>The figures are Giza's, compressed.</b> The sizes are the real ones at <see cref="SIZE_SCALE"/> units a metre (Khufu 139
    /// metres tall today and 230 across), and the plan is the real one - the three on their diagonal from north-east to south-
    /// west, sides on the compass, the queens east of Khufu and south of Menkaure - drawn in tighter by
    /// <see cref="SPACING_SCALE"/>, so the whole group stands inside the dunes' grid (<c>DesertBackdrop</c>'s extent, 1000) with
    /// Khufu <see cref="NEAREST"/> out and the rest receding behind it, as the photographs from the plateau's edge show them.
    /// </para>
    /// <para>
    /// <b>Standing on the dunes.</b> Each sits at the lowest of the dunes under its corners and middle (<c>TerrainMirror.Desert</c>)
    /// less a margin, so the sand meets its faces everywhere round the foot and drifts over it where a dune runs high.
    /// </para>
    /// </summary>
    internal sealed class Pyramids : IDisposable
    {
        //Units a metre for the sizes, and the share of the real spacing the plan is drawn at
        private const float SIZE_SCALE = 1.4f;
        private const float SPACING_SCALE = 0.6f;

        //How far out the nearest (Khufu) stands, the bearing it stands on from the island (radians, from +X towards +Z: behind
        //the play view the Testbed opens on), and how far the group's diagonal is turned off the line of sight, so the three
        //stand staggered side by side as the photographs show them (straight away from the island, Khufu hid Khafre)
        private const float NEAREST = 700f;
        private const float BEARING = -1.75f;
        private const float DIAGONAL_TURN = 0.95f;

        //How far the air takes to thicken to the horizon's colour over them: lighter than the dunes' (420), or they vanish
        private const float HAZE = 1600f;

        //How far a pyramid is sunk under the lowest dune round it
        private const float SINK = 4f;

        //The group: centre east and north of Khufu (metres), base side and height (metres, as they stand today), the casing
        //left at the top (share of the height) and how much higher its ground stands (metres)
        private static readonly (float East, float North, float Side, float Height, float Casing, float Raise)[] GIZA =
        {
            (0f, 0f, 230f, 139f, 0f, 0f),             //Khufu
            (-330f, -340f, 215f, 136f, 0.15f, 10f),   //Khafre, on higher ground, its casing at the top
            (-590f, -760f, 103f, 61f, 0f, 0f),        //Menkaure
            (170f, -40f, 48f, 30f, 0f, 0f),           //Khufu's queens
            (170f, -95f, 46f, 29f, 0f, 0f),
            (170f, -150f, 44f, 28f, 0f, 0f),
            (-660f, -900f, 44f, 27f, 0f, 0f),         //Menkaure's queens
            (-590f, -900f, 31f, 20f, 0f, 0f),
            (-520f, -900f, 31f, 20f, 0f, 0f)
        };

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly EffectParameter _view, _projection, _cameraPosition, _sunDirection, _sunColor, _zenith, _horizon;
        private readonly VertexBuffer _vertices;
        private readonly int _triangles;

        public Pyramids(GraphicsDevice graphicsDevice, ContentManager content, DesertSceneConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _effect = content.Load<Effect>("Shaders/Pyramid");
            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _sunDirection = _effect.Parameters["SunDirection"];
            _sunColor = _effect.Parameters["SunColor"];
            _zenith = _effect.Parameters["ZenithColor"];
            _horizon = _effect.Parameters["HorizonColor"];
            _effect.Parameters["PyramidHaze"].SetValue(HAZE);

            PyramidVertex[] vertices = Build(config);
            _triangles = vertices.Length / 3;
            _vertices = new VertexBuffer(graphicsDevice, PyramidVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _vertices.SetData(vertices);
        }

        /// <summary>Where each pyramid of the group stands in the world: its centre on the ground, its base side and height
        /// (units), its casing share. For the build and for a test.</summary>
        internal static IEnumerable<(Vector3 Centre, float Side, float Height, float Casing)> Placement(DesertSceneConfig config)
        {
            //Khufu NEAREST out along BEARING, the plan's south-west diagonal turned DIAGONAL_TURN off the line of sight
            Vector2 khufu = new Vector2(MathF.Cos(BEARING), MathF.Sin(BEARING)) * NEAREST;
            var away = new Vector2(MathF.Cos(BEARING + DIAGONAL_TURN), MathF.Sin(BEARING + DIAGONAL_TURN));
            var side = new Vector2(-away.Y, away.X);
            const float INV_SQRT2 = 0.70710678f;

            for (int i = 0; i < GIZA.Length; i++)
            {
                var g = GIZA[i];
                //South-west (east and north both negative) is away; north-west across
                float along = -(g.East + g.North) * INV_SQRT2, across = (g.North - g.East) * INV_SQRT2;
                Vector2 c = khufu + (away * along + side * across) * SIZE_SCALE * SPACING_SCALE;
                float half = g.Side * SIZE_SCALE * 0.5f;

                //The lowest dune under its middle and corners, so the sand meets every face
                float ground = TerrainMirror.Desert(c.X, c.Y, config);
                for (int k = 0; k < 4; k++)
                {
                    float a = MathHelper.PiOver4 + k * MathHelper.PiOver2;
                    ground = MathF.Min(ground, TerrainMirror.Desert(c.X + MathF.Cos(a) * half * 1.414f, c.Y + MathF.Sin(a) * half * 1.414f, config));
                }

                //Khafre's higher ground as height: lifted, its foot would stand on air over the dunes
                yield return (new Vector3(c.X, ground - SINK, c.Y), g.Side * SIZE_SCALE, (g.Height + g.Raise) * SIZE_SCALE, g.Casing);
            }
        }

        private static PyramidVertex[] Build(DesertSceneConfig config)
        {
            var v = new List<PyramidVertex>();
            int seed = 0;
            //The plan's own compass: its faces stand square to east and north, turned with the plan
            float turn = BEARING + DIAGONAL_TURN + MathHelper.Pi * 1.25f;
            foreach (var (centre, side, height, casing) in Placement(config))
            {
                float half = side * 0.5f;
                Vector3 apex = centre + Vector3.Up * height;
                var data = new Vector4(centre.Y, height, casing, seed++);
                for (int k = 0; k < 4; k++)
                {
                    float a0 = turn + k * MathHelper.PiOver2, a1 = a0 + MathHelper.PiOver2;
                    Vector3 c0 = centre + new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)) * half * 1.41421356f;
                    Vector3 c1 = centre + new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1)) * half * 1.41421356f;
                    Vector3 normal = Vector3.Normalize(Vector3.Cross(c1 - c0, apex - c0));
                    if (Vector3.Dot(normal, (c0 + c1) * 0.5f - centre) < 0f) normal = -normal;
                    v.Add(new PyramidVertex(c0, normal, data));
                    v.Add(new PyramidVertex(c1, normal, data));
                    v.Add(new PyramidVertex(apex, normal, data));
                }
            }
            return v.ToArray();
        }

        /// <summary>Draws the group, opaque and writing depth, after the dunes. Leaves the states as it found them.</summary>
        public void Draw(in SceneFrame frame)
        {
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(frame.SunColor);
            _zenith.SetValue(frame.ZenithLinear);
            _horizon.SetValue(frame.HorizonLinear);

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

        private struct PyramidVertex : IVertexType
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Data;

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;

            public PyramidVertex(Vector3 position, Vector3 normal, Vector4 data)
            {
                Position = position;
                Normal = normal;
                Data = data;
            }
        }
    }
}
