using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The picture a sky-replacing scene has on the Potato path (#829): a finer sphere round the camera, drawn with the
    /// scene's technique of <c>PotatoSky.fx</c> (<see cref="PotatoSceneRig.SkyLookOf"/>), its figure - the Milky Way,
    /// the curtains, the marble - computed per vertex as the dome's gradient is; and the stars as a batch of small
    /// screen-aligned quads built once, added over it. Per pixel both cost a move: the first cut computed the
    /// figures per pixel and paid 12 ms at 1080p. Nothing here depends on the scene's config beyond the look.
    /// </summary>
    public sealed class PotatoSkyFigure : IDisposable
    {
        /// <summary>The sphere: a vertex every 3.75 degrees, the curtains' and the marble's sines a few vertices to the wave.</summary>
        public const int RINGS = 48, SEGMENTS = 96;

        //SkyDome's own radius: drawn with no depth test, so any radius inside the far plane is at infinity
        private const float RADIUS = 12.5f;

        //How many pixels a full-brightness star is across at 1080 lines; scaled with the viewport's height
        private const float STAR_PIXELS = 2.4f;

        private readonly GraphicsDevice _device;
        private VertexBuffer _sphere, _stars;
        private IndexBuffer _sphereIndices, _starIndices;
        private readonly int _sphereTriangles, _starCount;

        //The effect's techniques and uniforms, looked up once here (BestPractices.md: the by-name indexers are linear scans,
        //and this draws every frame of four scenes). A technique is null on an effect compiled before it existed; a
        //parameter the compiler dropped is null and skipped
        private readonly Effect _effect;
        private readonly EffectTechnique _technique, _starsTechnique, _firstTechnique;
        private readonly EffectParameter _world, _view, _projection, _time, _void, _colorA, _colorB, _colorC, _colorD,
            _axis, _axis2, _params, _params2, _starSize;

        /// <summary>The scene's look: the technique and its uniforms.</summary>
        public PotatoSceneRig.PotatoSkyLook Look { get; }

        private PotatoSkyFigure(GraphicsDevice device, Effect effect, PotatoSceneRig.PotatoSkyLook look, VertexPositionColorTexture[] stars)
        {
            _device = device;
            _effect = effect;
            Look = look;

            _technique = look.Technique != null ? effect.Techniques[look.Technique] : null;
            _starsTechnique = effect.Techniques["PotatoSkyStars"];
            _firstTechnique = effect.Techniques[0];

            EffectParameterCollection parameters = effect.Parameters;
            _world = parameters["World"];
            _view = parameters["View"];
            _projection = parameters["Projection"];
            _time = parameters["SkyTime"];
            _void = parameters["SkyVoid"];
            _colorA = parameters["SkyColorA"];
            _colorB = parameters["SkyColorB"];
            _colorC = parameters["SkyColorC"];
            _colorD = parameters["SkyColorD"];
            _axis = parameters["SkyAxis"];
            _axis2 = parameters["SkyAxis2"];
            _params = parameters["SkyParams"];
            _params2 = parameters["SkyParams2"];
            _starSize = parameters["StarSize"];

            (VertexPositionColor[] vertices, int[] indices) = BuildSphere(RINGS, SEGMENTS, RADIUS);
            _sphere = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            _sphere.SetData(vertices);
            _sphereIndices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            _sphereIndices.SetData(indices);
            _sphereTriangles = indices.Length / 3;

            _starCount = stars.Length / 4;
            if (_starCount > 0)
            {
                _stars = new VertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, stars.Length, BufferUsage.WriteOnly);
                _stars.SetData(stars);
                var starIndices = new int[_starCount * 6];
                for (int i = 0; i < _starCount; i++)
                {
                    int v = i * 4, k = i * 6;
                    starIndices[k] = v; starIndices[k + 1] = v + 1; starIndices[k + 2] = v + 2;
                    starIndices[k + 3] = v; starIndices[k + 4] = v + 2; starIndices[k + 5] = v + 3;
                }
                _starIndices = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, starIndices.Length, BufferUsage.WriteOnly);
                _starIndices.SetData(starIndices);
            }
        }

        /// <summary>
        /// The figure of <paramref name="scene"/>, or null for a scene with none (<see cref="PotatoSceneRig.SkyLookOf"/>),
        /// drawn through <paramref name="effect"/>, <c>PotatoSky</c>.
        /// </summary>
        public static PotatoSkyFigure For(SceneKind scene, GraphicsDevice device, Effect effect)
        {
            PotatoSceneRig.PotatoSkyLook look = PotatoSceneRig.SkyLookOf(scene);
            if (look == null) return null;

            VertexPositionColorTexture[] stars = scene switch
            {
                //Space: a field thickened along the Milky Way; the aurora: a plain night's; the dream: none
                SceneKind.Space => BuildStars(1400, 2026, look.Axis, 0.75f),
                SceneKind.Aurora => BuildStars(900, 2027, null, 0f),
                SceneKind.Moon => BuildStars(1300, 2028, null, 0f),
                _ => Array.Empty<VertexPositionColorTexture>(),
            };
            return new PotatoSkyFigure(device, effect, look, stars);
        }

        /// <summary>A sphere of <paramref name="radius"/>, rings by segments, the colour unused; no device, for the tests.</summary>
        public static (VertexPositionColor[] Vertices, int[] Indices) BuildSphere(int rings, int segments, float radius)
        {
            var vertices = new VertexPositionColor[(rings + 1) * (segments + 1)];
            for (int ring = 0; ring <= rings; ring++)
            {
                float lat = MathHelper.Pi * ((float)ring / rings - 0.5f);
                float y = MathF.Sin(lat), c = MathF.Cos(lat);
                for (int segment = 0; segment <= segments; segment++)
                {
                    float lon = MathHelper.TwoPi * segment / segments;
                    vertices[ring * (segments + 1) + segment] = new VertexPositionColor(new Vector3(c * MathF.Cos(lon), y, c * MathF.Sin(lon)) * radius, Color.White);
                }
            }

            var indices = new List<int>(rings * segments * 6);
            for (int ring = 0; ring < rings; ring++)
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = ring * (segments + 1) + segment, b = a + 1, c = a + segments + 1, d = c + 1;
                    indices.Add(a); indices.Add(c); indices.Add(b);
                    indices.Add(b); indices.Add(c); indices.Add(d);
                }

            return (vertices, indices.ToArray());
        }

        /// <summary>
        /// The stars, four corners each: the direction in the position, the tint and the brightness in the colour, the
        /// corner in the texture coordinate. Spread over the whole sphere, most of them faint (a steep law, as real
        /// counts climb towards the faint end), and thickened along <paramref name="bandAxis"/>'s band by
        /// <paramref name="bandBoost"/> where there is one: a star off the band is kept with the chance one minus the
        /// boost, so the band holds more of the <paramref name="count"/>. Deterministic off the seed.
        /// </summary>
        public static VertexPositionColorTexture[] BuildStars(int count, int seed, Vector3? bandAxis, float bandBoost)
        {
            var rng = new Random(seed);
            var stars = new List<VertexPositionColorTexture>(count * 4);
            Vector2[] corners = { new(-1f, -1f), new(1f, -1f), new(1f, 1f), new(-1f, 1f) };

            for (int placed = 0, tries = 0; placed < count && tries < count * 20; tries++)
            {
                //Marsaglia's point on the sphere
                float u, v, s;
                do { u = 2f * (float)rng.NextDouble() - 1f; v = 2f * (float)rng.NextDouble() - 1f; s = u * u + v * v; } while (s >= 1f || s == 0f);
                float f = 2f * MathF.Sqrt(1f - s);
                Vector3 direction = new(u * f, 1f - 2f * s, v * f);

                if (bandAxis.HasValue)
                {
                    float across = Vector3.Dot(direction, bandAxis.Value);
                    float band = MathF.Exp(-across * across * 38f);
                    if (rng.NextDouble() > 1f - bandBoost + bandBoost * band) continue;
                }

                float brightness = 0.12f + 0.88f * MathF.Pow((float)rng.NextDouble(), 2.5f);
                Vector3 tint = Vector3.Lerp(new Vector3(0.75f, 0.85f, 1f), new Vector3(1f, 0.95f, 0.85f), (float)rng.NextDouble());
                var colour = new Color(tint.X, tint.Y, tint.Z, brightness);

                foreach (Vector2 corner in corners)
                    stars.Add(new VertexPositionColorTexture(direction * RADIUS, colour, corner));
                placed++;
            }

            return stars.ToArray();
        }

        /// <summary>
        /// Draws the sphere with the scene's technique and the stars over it, round the camera, with no depth test; the
        /// look's uniforms and the clock pushed first: every frame, since the effect is the dome's too. False, and nothing
        /// drawn, when the effect has no such technique (a build ahead of its compiled effect). Leaves the depth state
        /// default and the blend state alpha, as BeginPotatoSceneDraw states them after the dome.
        /// </summary>
        public bool Draw(ICamera camera, float time)
        {
            //A look with no technique is the stars alone over the void (the Moon)
            if (Look.Technique != null && _technique == null) return false;

            Effect effect = _effect;
            _world?.SetValue(Matrix.CreateTranslation(camera.Position));
            _view?.SetValue(camera.View);
            _projection?.SetValue(camera.Projection);
            _time?.SetValue(time);
            _void?.SetValue(Look.Void);
            _colorA?.SetValue(Look.ColorA);
            _colorB?.SetValue(Look.ColorB);
            _colorC?.SetValue(Look.ColorC);
            _colorD?.SetValue(Look.ColorD);
            _axis?.SetValue(Look.Axis);
            _axis2?.SetValue(Look.Axis2);
            _params?.SetValue(Look.Params);
            _params2?.SetValue(Look.Params2);

            _device.DepthStencilState = DepthStencilState.None;
            _device.BlendState = BlendState.Opaque;
            _device.RasterizerState = RasterizerState.CullNone;

            if (_technique != null)
            {
                effect.CurrentTechnique = _technique;
                _technique.Passes[0].Apply();
                _device.SetVertexBuffer(_sphere);
                _device.Indices = _sphereIndices;
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _sphereTriangles);
            }

            if (_starCount > 0 && _starsTechnique != null)
            {
                Viewport viewport = _device.Viewport;
                float pixels = STAR_PIXELS * viewport.Height / 1080f;
                _starSize?.SetValue(new Vector2(2f * pixels / viewport.Width, 2f * pixels / viewport.Height));

                _device.BlendState = BlendState.Additive;
                effect.CurrentTechnique = _starsTechnique;
                _starsTechnique.Passes[0].Apply();
                _device.SetVertexBuffer(_stars);
                _device.Indices = _starIndices;
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _starCount * 2);
            }

            effect.CurrentTechnique = _firstTechnique;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.AlphaBlend;
            return true;
        }

        public void Dispose()
        {
            _sphere?.Dispose(); _sphere = null;
            _sphereIndices?.Dispose(); _sphereIndices = null;
            _stars?.Dispose(); _stars = null;
            _starIndices?.Dispose(); _starIndices = null;
        }
    }
}
