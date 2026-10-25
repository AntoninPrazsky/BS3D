using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The Grid's light cycles (#956): three riders on the floor's lattice in right-angled runs, each trailing a wall of light
    /// (<c>LightCycle.fx</c>), in the film's early-1980s look the scene already has - a near-black body of boolean primitives
    /// lit only by its glowing seams.
    /// <para>
    /// <b>Where they ride.</b> Along the lattice every <see cref="LATTICE"/> units (the Hilbert trace's own stride), in a ring
    /// from <see cref="RING_MIN"/> to <see cref="RING_MAX"/> round the axis: clear of the island's hole and well inside the solids'
    /// ring (<c>GridTowerConfig.RadiusMin</c>, 140), so a cycle never rides through a solid. At every node a rider goes on
    /// straight or turns a right angle, from a hash of its own seed and the node's place in its run, never back the way it came
    /// and never out of the ring: the run is the same whatever the frame rate, and where a rider is at a time is its run's
    /// point at <see cref="SPEED"/> times that time. Off the wall clock, so they ride on while the game is paused.
    /// </para>
    /// <para>
    /// <b>No allocation a frame</b>: each rider keeps the last nodes of its run in a fixed ring, and the trails are written into one
    /// array a dynamic buffer takes.
    /// </para>
    /// </summary>
    internal sealed class LightCycles : IDisposable
    {
        //The riders: how many, their colours (linear, past the glare threshold so their seams and walls bloom: the blue of the
        //scene's own seams, the film's orange and its red), their seeds
        private static readonly Vector3[] COLORS =
        {
            new(0.45f, 1.60f, 2.40f),
            new(2.40f, 1.05f, 0.18f),
            new(2.30f, 0.30f, 0.22f)
        };
        private const int RIDERS = 3;

        //The lattice they ride, the ring they keep to, how fast they ride (units a second) and how often one goes straight on
        private const float LATTICE = 16f;
        private const float RING_MIN = 56f, RING_MAX = 124f;
        private const float SPEED = 24f;
        private const float STRAIGHT_SHARE = 0.6f;

        //The trail: how long a wall stands behind a rider, how tall, and how far behind the cycle's middle it starts (its back wheel)
        private const float TRAIL_LENGTH = 110f;
        private const float TRAIL_HEIGHT = 2.6f;
        private const float TRAIL_FROM = 2.6f;

        //The nodes a rider keeps: enough for the trail behind it, with the segment it is on
        private const int KEPT = (int)(TRAIL_LENGTH / LATTICE) + 4;
        private const int TRAIL_POINTS = KEPT + 2;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly EffectTechnique _bodyTechnique, _trailTechnique;
        private readonly EffectParameter _world, _view, _projection, _cycleColor;
        private readonly VertexBuffer _bodyVertices;
        private readonly int _bodyTriangles;
        private readonly DynamicVertexBuffer _trailVertices;
        private readonly VertexPositionTexture[] _trail = new VertexPositionTexture[RIDERS * TRAIL_POINTS * 2];
        private readonly int[] _trailCounts = new int[RIDERS];
        private readonly Rider[] _riders = new Rider[RIDERS];
        private readonly Vector2[] _positions = new Vector2[RIDERS], _headings = new Vector2[RIDERS];
        private readonly float _floorY;

        private static readonly BlendState ADDITIVE = BlendState.Additive;

        public LightCycles(GraphicsDevice graphicsDevice, ContentManager content, GridSceneConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _floorY = config.Terrain.LevelY;
            _effect = content.Load<Effect>("Shaders/LightCycle");
            _bodyTechnique = _effect.Techniques["LightCycleBody"];
            _trailTechnique = _effect.Techniques["LightCycleTrail"];
            _world = _effect.Parameters["World"];
            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _cycleColor = _effect.Parameters["CycleColor"];
            _effect.Parameters["BodyColor"].SetValue(config.Towers.BodyColor.ToVector3());
            _effect.Parameters["FaceLight"].SetValue(config.Towers.FaceLight.ToVector3());
            _effect.Parameters["FaceShadeFloor"].SetValue(config.Towers.FaceShadeFloor);

            CycleVertex[] body = CycleBody();
            _bodyTriangles = body.Length / 3;
            _bodyVertices = new VertexBuffer(graphicsDevice, CycleVertex.Declaration, body.Length, BufferUsage.WriteOnly);
            _bodyVertices.SetData(body);
            _trailVertices = new DynamicVertexBuffer(graphicsDevice, VertexPositionTexture.VertexDeclaration, _trail.Length, BufferUsage.WriteOnly);

            for (int i = 0; i < RIDERS; i++) _riders[i] = new Rider(7919 * (i + 1) + config.Towers.Seed);
        }

        /// <summary>Draws the cycles (opaque, writing depth) and then their walls (added over the frame, depth read only).
        /// Leaves the blend, depth and rasterizer states as it found them.</summary>
        public void Draw(in SceneFrame frame)
        {
            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState rasterizer = _graphicsDevice.RasterizerState;

            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);

            int written = 0;
            float distance = frame.Time * SPEED;
            for (int i = 0; i < RIDERS; i++)
            {
                _riders[i].At(distance, out _positions[i], out _headings[i]);
                _trailCounts[i] = _riders[i].Trail(distance, _trail, written, _floorY);
                written += _trailCounts[i];
            }
            _trailVertices.SetData(_trail, 0, written, SetDataOptions.Discard);

            //The cycles
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SetVertexBuffer(_bodyVertices);
            _effect.CurrentTechnique = _bodyTechnique;
            for (int i = 0; i < RIDERS; i++)
            {
                //Turned so the cycle's own +X runs along its heading
                Matrix world = Matrix.CreateRotationY(MathF.Atan2(-_headings[i].Y, _headings[i].X));
                world.Translation = new Vector3(_positions[i].X, _floorY, _positions[i].Y);
                _world.SetValue(world);
                _cycleColor.SetValue(COLORS[i]);
                _bodyTechnique.Passes[0].Apply();
                _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _bodyTriangles);
            }

            //The walls
            _graphicsDevice.BlendState = ADDITIVE;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.SetVertexBuffer(_trailVertices);
            _effect.CurrentTechnique = _trailTechnique;
            int first = 0;
            for (int i = 0; i < RIDERS; i++)
            {
                if (_trailCounts[i] >= 4)
                {
                    _cycleColor.SetValue(COLORS[i]);
                    _trailTechnique.Passes[0].Apply();
                    _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, first, _trailCounts[i] - 2);
                }
                first += _trailCounts[i];
            }

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;
        }

        public void Dispose()
        {
            _bodyVertices?.Dispose();
            _trailVertices?.Dispose();
        }

        /// <summary>
        /// One rider's run: the nodes it passes, made one at a time from its seed, the last <see cref="KEPT"/> of them in a ring.
        /// Every segment is one <see cref="LATTICE"/> long, so the node a distance falls after is that distance over the lattice.
        /// </summary>
        private sealed class Rider
        {
            private readonly int _seed;
            private readonly Vector2[] _nodes = new Vector2[KEPT];
            private long _made;          //how many nodes of the run have been made (the newest is _made - 1)

            public Rider(int seed)
            {
                _seed = seed;
                Restart();
            }

            private void Restart()
            {
                //A start on the lattice inside the ring, its first step along an axis
                var random = new Random(_seed);
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(RING_MIN + LATTICE, RING_MAX - LATTICE, (float)random.NextDouble());
                var start = new Vector2(MathF.Round(MathF.Cos(bearing) * radius / LATTICE) * LATTICE, MathF.Round(MathF.Sin(bearing) * radius / LATTICE) * LATTICE);
                Vector2 step = Math.Abs(start.X) > Math.Abs(start.Y) ? new Vector2(0f, LATTICE) : new Vector2(LATTICE, 0f);
                _nodes[0] = start;
                _nodes[1] = start + step;
                _made = 2;
            }

            private Vector2 Node(long index) => _nodes[(int)(index % KEPT)];

            //The next node: on straight or a right angle, by the rider's hash at this place in the run, never back and never
            //out of the ring (the way back only when nothing else is left)
            private void MakeNext()
            {
                Vector2 last = Node(_made - 1), before = Node(_made - 2);
                Vector2 ahead = last - before;
                var left = new Vector2(-ahead.Y, ahead.X);
                uint h = (uint)_seed * 0x9E3779B1u ^ (uint)_made * 0x85EBCA6Bu;
                h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
                float roll = (h & 0xFFFF) / 65535f;

                Vector2 first = roll < STRAIGHT_SHARE ? ahead : (roll < 0.5f + STRAIGHT_SHARE * 0.5f ? left : -left);
                Vector2 next = last + first;
                if (!InRing(next))
                {
                    Vector2 second = first == ahead ? (roll < 0.8f ? left : -left) : ahead;
                    next = last + second;
                    if (!InRing(next)) next = InRing(last - second) && second != ahead ? last - second : last - ahead;
                }
                _nodes[(int)(_made % KEPT)] = next;
                _made++;
            }

            private static bool InRing(Vector2 p)
            {
                float r = p.Length();
                return r >= RING_MIN && r <= RING_MAX;
            }

            /// <summary>Where the rider is after <paramref name="distance"/> along its run, and which way it is heading.</summary>
            public void At(float distance, out Vector2 position, out Vector2 heading)
            {
                long segment = (long)(distance / LATTICE);
                if (segment + 2 < _made - KEPT + 2) Restart();   //the clock went back: make the run again from its start
                while (_made < segment + 2) MakeNext();
                Vector2 a = Node(segment), b = Node(segment + 1);
                heading = (b - a) / LATTICE;
                position = Vector2.Lerp(a, b, distance / LATTICE - segment);
            }

            /// <summary>The point of the run <paramref name="distance"/> along it (made and kept).</summary>
            private Vector2 PointAt(float distance)
            {
                long segment = (long)(distance / LATTICE);
                return Vector2.Lerp(Node(segment), Node(segment + 1), distance / LATTICE - segment);
            }

            /// <summary>Writes the wall behind the rider, which is <paramref name="distance"/> along its run, into
            /// <paramref name="into"/> from <paramref name="offset"/>: a strip of pairs (foot, top) back along the run from its
            /// back wheel, through every node, to the wall's tail. Returns how many vertices it wrote.</summary>
            public int Trail(float distance, VertexPositionTexture[] into, int offset, float floorY)
            {
                float start = distance - TRAIL_FROM;
                float end = Math.Max(start - TRAIL_LENGTH, 0f);
                if (start <= end) return 0;

                int written = 0;
                Write(into, offset, ref written, PointAt(start), 0f, floorY);
                for (long k = (long)(start / LATTICE); k * LATTICE > end; k--)
                    Write(into, offset, ref written, Node(k), (start - k * LATTICE) / TRAIL_LENGTH, floorY);
                Write(into, offset, ref written, PointAt(end), (start - end) / TRAIL_LENGTH, floorY);
                return written;
            }

            private static void Write(VertexPositionTexture[] into, int offset, ref int written, Vector2 p, float age, float floorY)
            {
                into[offset + written++] = new VertexPositionTexture(new Vector3(p.X, floorY, p.Y), new Vector2(age, 0f));
                into[offset + written++] = new VertexPositionTexture(new Vector3(p.X, floorY + TRAIL_HEIGHT, p.Y), new Vector2(age, 1f));
            }
        }

        //--- The cycle's body ------------------------------------------------------------------------------------------------

        /// <summary>A vertex of the cycle: position, the face's normal, and its face-local seam data (see <c>LightCycle.fx</c>).</summary>
        private struct CycleVertex : IVertexType
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Face;

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
                new VertexElement(24, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;

            public CycleVertex(Vector3 position, Vector3 normal, Vector4 face)
            {
                Position = position;
                Normal = normal;
                Face = face;
            }
        }

        //The cycle in its own frame (+X forward, +Y up, the floor at 0), built of the film's primitives: two disc wheels, a long
        //low body between them, a cowl over the front wheel and the rider's hood behind it
        private const float WHEEL_RADIUS = 1.25f, WHEEL_WIDTH = 0.7f, WHEEL_BASE = 2.6f;
        private const int WHEEL_SIDES = 20;

        private static CycleVertex[] CycleBody()
        {
            var v = new List<CycleVertex>();
            Box(v, new Vector3(0f, 1.25f, 0f), new Vector3(2.2f, 0.45f, 0.42f));      //the body
            Box(v, new Vector3(1.5f, 1.95f, 0f), new Vector3(1.0f, 0.25f, 0.38f));    //the cowl
            Box(v, new Vector3(-0.6f, 2.0f, 0f), new Vector3(0.9f, 0.32f, 0.36f));    //the rider's hood
            Wheel(v, new Vector3(WHEEL_BASE, WHEEL_RADIUS, 0f));
            Wheel(v, new Vector3(-WHEEL_BASE, WHEEL_RADIUS, 0f));
            return v.ToArray();
        }

        //A box as six quads, each with its own face-local seams (so every edge glows)
        private static void Box(List<CycleVertex> v, Vector3 centre, Vector3 half)
        {
            Face(v, centre, half, Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY);
            Face(v, centre, half, -Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY);
            Face(v, centre, half, Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ);
            Face(v, centre, half, -Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ);
            Face(v, centre, half, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY);
            Face(v, centre, half, -Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY);
        }

        private static void Face(List<CycleVertex> v, Vector3 centre, Vector3 half, Vector3 normal, Vector3 uAxis, Vector3 vAxis)
        {
            float depth = Math.Abs(Vector3.Dot(half, normal));
            float uHalf = Math.Abs(Vector3.Dot(half, uAxis)), vHalf = Math.Abs(Vector3.Dot(half, vAxis));
            Vector3 middle = centre + normal * depth;
            Quad(v, middle - uAxis * uHalf - vAxis * vHalf, uAxis * uHalf * 2f, vAxis * vHalf * 2f, normal, uHalf * 2f, vHalf * 2f);
        }

        private static void Quad(List<CycleVertex> v, Vector3 corner, Vector3 u, Vector3 w, Vector3 normal, float uSize, float vSize, float uPin = -1f)
        {
            //uPin >= 0 pins the face-local u (a tyre's facet): its two u borders never glow
            Vector4 Corner(float a, float b) => uPin >= 0f ? new Vector4(uPin, b * vSize, 1e3f, vSize) : new Vector4(a * uSize, b * vSize, uSize, vSize);
            CycleVertex p00 = new(corner, normal, Corner(0f, 0f)), p10 = new(corner + u, normal, Corner(1f, 0f));
            CycleVertex p01 = new(corner + w, normal, Corner(0f, 1f)), p11 = new(corner + u + w, normal, Corner(1f, 1f));
            v.Add(p00); v.Add(p10); v.Add(p11);
            v.Add(p00); v.Add(p11); v.Add(p01);
        }

        //A disc wheel on the Z axle: the tyre's facets (only its two rims glow) and two caps ringed at the rim and the hub
        private static void Wheel(List<CycleVertex> v, Vector3 centre)
        {
            float halfWidth = WHEEL_WIDTH * 0.5f;
            for (int i = 0; i < WHEEL_SIDES; i++)
            {
                float a0 = MathHelper.TwoPi * i / WHEEL_SIDES, a1 = MathHelper.TwoPi * (i + 1) / WHEEL_SIDES;
                var r0 = new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f) * WHEEL_RADIUS;
                var r1 = new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f) * WHEEL_RADIUS;
                Vector3 normal = Vector3.Normalize(r0 + r1);
                Quad(v, centre + r0 - Vector3.UnitZ * halfWidth, r1 - r0, Vector3.UnitZ * WHEEL_WIDTH, normal, 1f, WHEEL_WIDTH, 500f);

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 c = centre + Vector3.UnitZ * halfWidth * side, n = Vector3.UnitZ * side;
                    Vector4 Disc(Vector3 r) => new(r.X, r.Y, WHEEL_RADIUS, -1f);
                    v.Add(new CycleVertex(c, n, Disc(Vector3.Zero)));
                    v.Add(new CycleVertex(c + r0, n, Disc(r0)));
                    v.Add(new CycleVertex(c + r1, n, Disc(r1)));
                }
            }
        }
    }
}
