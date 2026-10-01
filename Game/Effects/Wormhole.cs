using BS3D.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Camera;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The impossible shot</b> (#230): three shots fired in a row from the same place along the same line, every one
    /// of them flying clean out past the island, and a small wormhole tears open in the air in front of the last one.
    /// It swallows the shots that missed — the last one caught a few units out, any earlier one still in the air pulled
    /// back to it — spinning them down its throat as they shrink, holds a moment, and snaps shut with a pop.
    /// <para>
    /// <b>Cosmetic, by #230's own rule.</b> The issue's list asked for bonus points; the comment that drew the issue's
    /// dividing line ruled that an egg which touches the rules quietly invalidates every gate that measures them, and
    /// points are worse than that here: the online boards refuse a score over the ceiling ScoreSim simulates, which no
    /// simulated clear reaches by missing on purpose. So the balls it eats are the misses they already were — the
    /// session retires each one through the door every other miss goes through, the moment it is caught, and from then
    /// on what spins into the hole is a drawing (<see cref="Swallow"/>) that nothing in the simulation or the rules can see.
    /// </para>
    /// <para>
    /// On the <b>simulation's clock</b>, like <see cref="Blasts"/>: it is a thing happening out in the world, so a drop
    /// cinematic's slow motion slows it and a pause stops it. One quad and one draw call (<c>Wormhole.fx</c>), in the
    /// blasts' slot of the frame; the swallowed balls go into the frame's ball collection like every other ball.
    /// </para>
    /// </summary>
    internal sealed class Wormhole : IDisposable
    {
        /// <summary>The most balls one hole can be swallowing at once — a run is three shots, and this is room to spare.</summary>
        public const int MAX_GHOSTS = 8;

        /// <summary>The hole's radius at full size, in world units: a few balls across, small against the arena.</summary>
        public const float RADIUS = 4.5f;

        /// <summary>How far ahead of the escaping shot the hole opens, in world units — just in front of it, so it flies into it.</summary>
        public const float AHEAD = 6f;

        //The quad's half-size in hole radii. Must match QUAD_SPAN in Wormhole.fx.
        private const float QUAD_SPAN = 1.6f;

        //The hole's life: torn open with an overshoot, held while it swallows, held a beat longer, then shut with a pop
        private const float OPEN_SECONDS = 0.22f;
        private const float LINGER_SECONDS = 0.55f;
        private const float CLOSE_SECONDS = 0.24f;
        private const float POP_SECONDS = 0.32f;

        //A swallowed ball: pulled to the rim of the swirl, then spun down to the throat over SPIRAL_SECONDS, shrinking
        private const float PULL_SPEED = 90f;            //world units a second it is hauled back at, for the long way in
        private const float MIN_PULL_SECONDS = 0.14f;
        private const float MAX_PULL_SECONDS = 0.7f;
        private const float ENTRY_RADIUS = 0.85f;        //where on the swirl it joins the spiral, in hole radii
        private const float SPIRAL_SECONDS = 1.05f;
        private const float SPIRAL_TURNS = 2.25f;
        private const float BALL_TUMBLE = 14f;           //radians a second it tumbles as it goes down

        //The arms turn at this rate, radians a second, and faster as the hole shuts
        private const float SPIN_RATE = 4.5f;

        private struct Ghost
        {
            public Vector3 From;
            public Vector3 Velocity;
            public float Start;        //the hole's age when it was caught
            public float Pull;         //seconds from caught to the rim of the swirl
            public float Angle;        //where round the swirl it joins it
            public BallType Type;
            public BallKind Kind;
        }

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly Ghost[] _ghosts = new Ghost[MAX_GHOSTS];
        private int _ghostCount;

        private bool _active;
        private float _age;
        private float _closeAt = float.MaxValue;   //the age the shutting starts at, once every ball is down
        private float _spin;
        private Vector3 _centre, _right, _up;

        private readonly VertexBuffer _vertexBuffer;
        private readonly IndexBuffer _indexBuffer;

        //Cached parameter handles: the by-name indexer is a linear scan
        private readonly EffectParameter _viewParam, _projectionParam, _cameraRightParam, _cameraUpParam;
        private readonly EffectParameter _centreParam, _stateParam;

        public Wormhole(GraphicsDevice device, Effect effect)
        {
            _device = device;
            _effect = effect;

            _viewParam = effect.Parameters["View"];
            _projectionParam = effect.Parameters["Projection"];
            _cameraRightParam = effect.Parameters["CameraRight"];
            _cameraUpParam = effect.Parameters["CameraUp"];
            _centreParam = effect.Parameters["HoleCentre"];
            _stateParam = effect.Parameters["HoleState"];

            _vertexBuffer = new VertexBuffer(device, CornerVertex.Declaration, 4, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(new CornerVertex[]
            {
                new() { Corner = new Vector2(-1f, 1f) },
                new() { Corner = new Vector2(1f, 1f) },
                new() { Corner = new Vector2(1f, -1f) },
                new() { Corner = new Vector2(-1f, -1f) },
            });

            _indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
            _indexBuffer.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
        }

        /// <summary>Whether a hole is up — the session opens no second one over it.</summary>
        public bool Active => _active;

        /// <summary>
        /// Tears a hole open at <paramref name="centre"/>, facing the lens: <paramref name="right"/> and
        /// <paramref name="up"/> are the camera's at this moment, and the swallowed balls spiral in their plane.
        /// </summary>
        public void Open(Vector3 centre, Vector3 right, Vector3 up, ProceduralAudio audio)
        {
            _active = true;
            _age = 0f;
            _closeAt = float.MaxValue;
            _spin = 0f;
            _ghostCount = 0;
            _centre = centre;
            _right = right;
            _up = up;

            audio?.PlayWormholeOpen(centre);
        }

        /// <summary>
        /// Draws a ball the session has just retired as being swallowed: it carries on from <paramref name="from"/> at
        /// <paramref name="velocity"/>, is hauled to the swirl and spun down the throat. Ignored with no hole up or no
        /// room left — it is a drawing, and the ball is already gone either way.
        /// </summary>
        public void Swallow(Vector3 from, Vector3 velocity, BallType type, BallKind kind)
        {
            if (!_active || _ghostCount >= MAX_GHOSTS || _age >= _closeAt) return;

            //It joins the spiral on its own side of the hole, so nothing crosses the throat on the way in
            Vector3 offset = from - _centre;
            float angle = MathF.Atan2(Vector3.Dot(offset, _up), Vector3.Dot(offset, _right));

            Vector3 entry = Entry(angle);
            float pull = MathHelper.Clamp(Vector3.Distance(from, entry) / PULL_SPEED, MIN_PULL_SECONDS, MAX_PULL_SECONDS);

            _ghosts[_ghostCount++] = new Ghost
            {
                From = from,
                Velocity = velocity,
                Start = _age,
                Pull = pull,
                Angle = angle,
                Type = type,
                Kind = kind
            };
        }

        /// <summary>Nothing open — a fresh level starts clean.</summary>
        public void Reset()
        {
            _active = false;
            _ghostCount = 0;
        }

        /// <summary>Advances the hole by <paramref name="elapsed"/> seconds of the world's time, and plays its pop when it shuts.</summary>
        public void Update(float elapsed, ProceduralAudio audio)
        {
            if (!_active) return;

            float before = _age;
            _age += elapsed;

            //Shut once every ball is down and the hole has stood a beat longer - and never before it has finished opening
            if (_closeAt == float.MaxValue)
            {
                float lastDown = OPEN_SECONDS;
                for (int i = 0; i < _ghostCount; i++)
                    lastDown = MathF.Max(lastDown, _ghosts[i].Start + _ghosts[i].Pull + SPIRAL_SECONDS);

                if (_age >= lastDown) _closeAt = lastDown + LINGER_SECONDS;
            }

            float shut = _closeAt + CLOSE_SECONDS;
            if (before < shut && _age >= shut) audio?.PlayWormholePop(_centre);

            //The arms wind up as it shuts
            float closing = MathHelper.Clamp((_age - _closeAt) / CLOSE_SECONDS, 0f, 1f);
            _spin += elapsed * SPIN_RATE * (1f + 2.5f * closing);

            if (_age >= shut + POP_SECONDS) Reset();
        }

        /// <summary>The swallowed balls into the frame's collection, each where its spiral has it and as small as it has got.</summary>
        public void CollectBalls(in BallDrawFrame frame)
        {
            if (!_active) return;

            for (int i = 0; i < _ghostCount; i++)
            {
                Ghost ghost = _ghosts[i];
                float t = _age - ghost.Start;
                if (t < 0f || t >= ghost.Pull + SPIRAL_SECONDS) continue;

                Vector3 position;
                float scale;

                if (t < ghost.Pull)
                {
                    //Hauled to the rim of the swirl: a Hermite curve from where it was caught, leaving along the way it
                    //was flying, so the last shot visibly overshoots and is yanked back, and arriving along the spiral
                    float s = t / ghost.Pull;
                    Vector3 entry = Entry(ghost.Angle);

                    Vector3 leave = ghost.Velocity * ghost.Pull;
                    float reach = 1.5f * Vector3.Distance(ghost.From, entry) + 2f;
                    if (leave.LengthSquared() > reach * reach) leave = Vector3.Normalize(leave) * reach;

                    Vector3 arrive = Tangent(ghost.Angle) * (ENTRY_RADIUS * RADIUS * SpiralRate(0f) * ghost.Pull);

                    float s2 = s * s, s3 = s2 * s;
                    position = (2f * s3 - 3f * s2 + 1f) * ghost.From + (s3 - 2f * s2 + s) * leave
                        + (-2f * s3 + 3f * s2) * entry + (s3 - s2) * arrive;
                    scale = 1f;
                }
                else
                {
                    //Spun down the throat: the radius closing, the turn quickening, the ball shrinking to nothing
                    float u = (t - ghost.Pull) / SPIRAL_SECONDS;
                    float radius = ENTRY_RADIUS * RADIUS * (1f - u) * (1f - u * 0.35f);
                    float angle = ghost.Angle + MathHelper.TwoPi * SPIRAL_TURNS * (0.35f * u + 0.65f * u * u);

                    position = _centre + (_right * MathF.Cos(angle) + _up * MathF.Sin(angle)) * radius;
                    scale = MathF.Pow(1f - u, 0.85f);
                }

                if (scale <= 0.01f) continue;

                //Scaled and tumbling, the translation written straight into the fourth row
                Matrix world = Matrix.CreateFromAxisAngle(Vector3.Normalize(_right + _up * 0.6f), BALL_TUMBLE * t)
                    * Matrix.CreateScale(scale);
                world.M41 = position.X;
                world.M42 = position.Y;
                world.M43 = position.Z;

                frame.Add(ghost.Type, position, world, BallRenderSet.UNOCCLUDED, kind: ghost.Kind);
            }
        }

        /// <summary>
        /// The hole in one draw: premultiplied (its throat is black, which an additive pass cannot draw), depth-read
        /// and writing no depth, in the blasts' slot so what stands nearer the lens hides it.
        /// </summary>
        public void Draw(ICamera camera)
        {
            if (!_active) return;

            float size = HoleSize();
            float pop = _age >= _closeAt + CLOSE_SECONDS ? 1f - (_age - _closeAt - CLOSE_SECONDS) / POP_SECONDS : 0f;
            if (size <= 0f && pop <= 0f) return;

            Matrix view = camera.View;

            _viewParam.SetValue(view);
            _projectionParam.SetValue(camera.Projection);
            _cameraRightParam.SetValue(new Vector3(view.M11, view.M21, view.M31));
            _cameraUpParam.SetValue(new Vector3(view.M12, view.M22, view.M32));
            _centreParam.SetValue(new Vector4(_centre, RADIUS * QUAD_SPAN));
            _stateParam.SetValue(new Vector4(_spin, size, MathHelper.Clamp(pop, 0f, 1f), 0f));

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>The hole's size, 0..~1.1: torn open with an overshoot, held, swelling a touch and then gone as it shuts.</summary>
        private float HoleSize()
        {
            if (_age < OPEN_SECONDS)
            {
                //Ease out with an overshoot: it snaps open past its size and settles
                float s = _age / OPEN_SECONDS - 1f;
                const float back = 2.2f;
                return 1f + s * s * ((back + 1f) * s + back);
            }

            if (_age < _closeAt) return 1f;

            float c = (_age - _closeAt) / CLOSE_SECONDS;
            if (c >= 1f) return 0f;

            //A gulp: a last swell, then gone
            return c < 0.3f ? 1f + 0.12f * (c / 0.3f) : 1.12f * (1f - (c - 0.3f) / 0.7f);
        }

        //Where on the swirl a ball joins the spiral, and which way it is then going
        private Vector3 Entry(float angle) =>
            _centre + (_right * MathF.Cos(angle) + _up * MathF.Sin(angle)) * (ENTRY_RADIUS * RADIUS);

        private Vector3 Tangent(float angle) => -_right * MathF.Sin(angle) + _up * MathF.Cos(angle);

        //The spiral's turn rate in radians per second at u, the derivative of its angle
        private static float SpiralRate(float u) =>
            MathHelper.TwoPi * SPIRAL_TURNS * (0.35f + 1.3f * u) / SPIRAL_SECONDS;

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }

        /// <summary>One corner of the quad, -1..1.</summary>
        private struct CornerVertex : IVertexType
        {
            public Vector2 Corner;

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }
    }
}
