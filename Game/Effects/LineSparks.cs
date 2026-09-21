using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The shower the net throws off where the cluster crosses it (#434): the loss read as something being
    /// <b>cut</b> rather than merely announced. The camera's half of that moment is
    /// <see cref="LineLossCinematic"/>, the net's own flare is <c>LaserGrid.Flare</c>, and this is the third —
    /// sparks struck off the crossing point, thrown out and down, dying before the result page arrives.
    /// <para>
    /// <b>Built on <c>LaunchSmears</c>'s mechanism rather than on <c>Blasts</c>'s</b>, which was worked out on
    /// the issue before a line of it was written. A blast is bomb-specific — chains, staggered links, reports,
    /// a jolt and a light — and a spark shower wants none of that; what it wants is a lot of short bright
    /// streaks, which is exactly what <c>ShotTrail.fx</c> already draws for the muzzle and the aim beam. So
    /// this is the third component through that effect and it owns nothing but its own quad.
    /// </para>
    /// <para>
    /// ⚠ <b>And being the third is why the two widths go out per draw.</b> The effect's parameters belong to
    /// the effect and not to whoever set them, so a component that set its widths once at load would have
    /// whichever constructor ran last decide how all three look — <c>LaunchSmears</c>'s own constructor
    /// carries that incident at length, from when <c>AimBeam</c> became the second. A spark is far thinner
    /// than either of them, so it would have been the loudest possible way to rediscover it.
    /// </para>
    /// <para>
    /// <b>A spark is drawn as the streak from where it is back along where it came from</b> — its own
    /// velocity, scaled — so it lengthens as it accelerates and shortens as it is thrown up and slows, which
    /// is what makes a shower read as sparks and not as a starburst of fixed rays. The same argument as the
    /// shot's motion stretch (#402) arriving at a different mechanism for a different reason: that one has a
    /// sphere to stretch, this one has nothing to draw but the streak.
    /// </para>
    /// </summary>
    internal sealed class LineSparks : IDisposable
    {
        /// <summary>
        /// How many sparks one crossing throws. Enough to read as a shower at the cinematic's stand-off
        /// (<c>LineLossCinematic.STAND_OFF</c>, 13 units) without becoming a solid sheet — each is an
        /// additive billboard, so the cost is the draw count and nothing else, and forty is two frames' worth
        /// of the cluster's own ball draws.
        /// </summary>
        private const int COUNT = 40;

        /// <summary>
        /// How long a spark lives, and how long the whole shower goes on being struck. Both are read against
        /// the cinematic's own clock: it flies in over <c>MOVE_IN</c> (0.55 s) and holds for <c>HOLD</c>
        /// (1.15 s), so a shower that is over inside the first second is finished while the camera is still
        /// there to have seen it — and cannot still be burning when the result page arrives.
        /// </summary>
        private const float LIFETIME = 0.62f;

        /// <summary>
        /// How fast a spark leaves, in world units a second, and how much of that is scattered. A range
        /// rather than a figure: sparks that all leave at one speed land on a circle, which reads as an
        /// explosion's shell rather than as metal being cut.
        /// </summary>
        private const float SPEED_MIN = 9f;
        private const float SPEED_MAX = 26f;

        /// <summary>
        /// The bias in the throw, which is what says <b>cut</b> rather than <b>burst</b>: the net is a
        /// horizontal plane, so what it strikes off flies <b>outward along itself</b> and is then arced down
        /// by gravity. The horizontal spread is full and the vertical one is slightly upward.
        /// <para>
        /// ⚠ It was the other way round in the first cut — biased <i>downward</i>, on the reasoning that a cut
        /// sprays and falls — and that was wrong twice over. The crossing point is the cluster's lowest ball,
        /// which by construction is a fraction of a unit above the island's stone (measured on a staged loss:
        /// <c>y = 0.26</c>), so a downward throw put most of the shower <i>inside the floor</i> within a
        /// tenth of a second, where the opaque scene occludes it. Gravity is what should make a spark fall,
        /// not the throw.
        /// </para>
        /// </summary>
        private const float RISE = 0.55f;
        private const float FALL = 0.2f;

        /// <summary>Gravity on a spark, which is heavier than the world's: a spark is a hot chip of nothing and
        /// its arc has to finish inside <see cref="LIFETIME"/>. Not heavier still — at 42 the drop over a
        /// lifetime is eight units, which buries the shower in the stone it was struck off.</summary>
        private const float GRAVITY = 26f;

        /// <summary>
        /// Drag, per second, as the fraction of speed a spark keeps. It is what makes the streaks shorten as
        /// they go, which is most of what reads as "thrown" rather than "fired": without it every spark keeps
        /// its launch length until it vanishes and the shower looks like a firework's rays.
        /// </summary>
        private const float DRAG = 0.22f;

        /// <summary>
        /// How long a spark's streak is drawn, as seconds of its own travel. A streak is therefore its
        /// velocity times this — the faster it is going the longer it is drawn, which is what a real spark's
        /// smear on an eye or a sensor is.
        /// </summary>
        private const float STREAK_SECONDS = 0.12f;

        //Thin, and thinner at the tail: a spark is a filament, not a ribbon. Both are pushed per draw - see
        //the class doc on why that is not optional here.
        //
        //⚠ THE FIRST CUT OF THIS SHOWER WAS INVISIBLE, and finding out why is most of what these four figures
        //cost. At 0.085 and 0.02, with a streak of 0.045 s of travel, the shower was struck, updated and drawn
        //- 44 frames of it, confirmed from a diagnostic line before anything was blamed - and could not be
        //seen at all. It was not occlusion, not timing and not the shared effect's parameters: it was SCALE.
        //Set against the other two users rather than by eye, the widths were five times thinner than the
        //thinnest thing that had ever drawn through ShotTrail.fx (AimBeam's 0.15, against LaunchSmears' 0.72),
        //and the streaks were a third of a ball long.
        //
        //What settled them was a deliberately absurd pass - width 1.0, streak 0.25 s, six-second life - which
        //photographed as an unmistakable shower and proved the mechanism whole in one run. These are the step
        //back from it: a spark is thicker than the aim beam it is seen beside and far thinner than a muzzle
        //smear, and its streak runs about a ball and a half at launch. Judge any further move the same way,
        //against the other two and then on the real thing; a figure chosen by eye here is invisible or absurd
        //with nothing in between.
        private const float HEAD_WIDTH = 0.22f;
        private const float TAIL_WIDTH = 0.06f;

        /// <summary>
        /// A spark's colour in linear radiance, pushed well over 1 so it blooms through the glare pass the way
        /// the muzzle's smear does. White-hot at the head of its life and falling towards the net's own red as
        /// it cools, which is the one thing that ties the shower to the thing that struck it.
        /// </summary>
        private static readonly Vector3 HOT = new(6.0f, 4.4f, 2.2f);
        private static readonly Vector3 COOL = new(4.2f, 0.5f, 0.15f);

        private struct Spark
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
        }

        private readonly GraphicsDevice _device;

        //Fixed and reused: the shower is struck once and the array never grows, so a loss costs no allocation
        //on a frame the game is already busy ending a level on
        private readonly Spark[] _sparks = new Spark[COUNT];
        private int _live;

        //Its own stream, seeded per shower from the crossing point, so a loss at the same place looks the same
        //twice - and so nothing here can disturb a Random the simulation is drawing from
        private readonly Random _random = new(434);

        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _headParam, _tailParam, _colorParam, _alphaParam;
        private readonly EffectParameter _headWidthParam, _tailWidthParam;
        private readonly EffectPass _pass;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        public LineSparks(GraphicsDevice device, Effect shotTrailEffect)
        {
            _device = device;

            _viewParam = shotTrailEffect.Parameters["View"];
            _projectionParam = shotTrailEffect.Parameters["Projection"];
            _cameraPositionParam = shotTrailEffect.Parameters["CameraPosition"];
            _headParam = shotTrailEffect.Parameters["TrailHead"];
            _tailParam = shotTrailEffect.Parameters["TrailTail"];
            _colorParam = shotTrailEffect.Parameters["TrailColor"];
            _alphaParam = shotTrailEffect.Parameters["TrailAlpha"];
            _headWidthParam = shotTrailEffect.Parameters["TrailHeadWidth"];
            _tailWidthParam = shotTrailEffect.Parameters["TrailTailWidth"];

            _pass = shotTrailEffect.CurrentTechnique.Passes[0];

            CreateQuad();
        }

        /// <summary>Whether anything is burning — the gate a caller can skip the whole component on.</summary>
        public bool Active => _live > 0;

        /// <summary>
        /// Strikes the shower at <paramref name="crossing"/>, the point the cluster reached the line at —
        /// the same point <c>LineLossCinematic</c> flies the lens to, handed over rather than recomputed so
        /// the sparks and the camera cannot disagree about where the moment happened.
        /// </summary>
        public void Strike(Vector3 crossing)
        {
            _live = COUNT;

            for (int i = 0; i < COUNT; i++)
            {
                //Full circle horizontally; vertically biased downward - see RISE and FALL for why a cut is
                //not a burst
                float angle = (float)_random.NextDouble() * MathHelper.TwoPi;
                float vertical = (float)_random.NextDouble() * (RISE + FALL) - FALL;

                Vector3 direction = Vector3.Normalize(new Vector3(
                    MathF.Cos(angle), vertical, MathF.Sin(angle)));

                float speed = MathHelper.Lerp(SPEED_MIN, SPEED_MAX, (float)_random.NextDouble());

                _sparks[i] = new Spark
                {
                    Position = crossing,
                    Velocity = direction * speed,

                    //Staggered into the first fifth of the lifetime, so the shower is struck over a few frames
                    //rather than appearing whole: a cut throws sparks for as long as it is cutting
                    Age = -(float)_random.NextDouble() * LIFETIME * 0.2f,
                };
            }
        }

        /// <summary>
        /// Advances every live spark. On the wall clock like everything else that answers a moment rather than
        /// running the simulation — a paused frame holds the shower where it was, the way the ball heartbeat
        /// and the net's own pulse do.
        /// </summary>
        public void Update(float elapsedSeconds)
        {
            if (_live == 0) return;

            int live = 0;

            for (int i = 0; i < COUNT; i++)
            {
                ref Spark spark = ref _sparks[i];
                if (spark.Age >= LIFETIME) continue;

                spark.Age += elapsedSeconds;
                if (spark.Age <= 0f) { live++; continue; }   //still waiting its turn to be struck

                //Drag as a per-second fraction kept, so the decay is the same on a fast machine and a slow one
                spark.Velocity *= MathF.Pow(DRAG, elapsedSeconds);
                spark.Velocity.Y -= GRAVITY * elapsedSeconds;
                spark.Position += spark.Velocity * elapsedSeconds;

                if (spark.Age < LIFETIME) live++;
            }

            _live = live;
        }

        /// <summary>Puts the shower out — a level torn down mid-cinematic must not leave sparks burning into
        /// the next one, which is the ghost every effect that outlives its session becomes.</summary>
        public void Clear() => _live = 0;

        /// <summary>
        /// Every live spark, one additive billboard each, streaked back along its own velocity.
        /// <para>
        /// <b>The GPU-state contract is <c>LaunchSmears</c>'s exactly</b>: it states the three states it needs
        /// and puts back what it found, and on the overwhelming majority of frames — every frame of every
        /// level nobody is losing — it returns before touching anything at all. What follows in the frame is
        /// the drain's glass and the ceiling's, which need the frame's own translucent baseline, so this may
        /// only ever leave that baseline as it was.
        /// </para>
        /// </summary>
        public void Draw(ICamera camera)
        {
            if (_live == 0) return;

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);

            //Reclaimed from whatever else drew through this effect since - see the class doc
            _headWidthParam.SetValue(HEAD_WIDTH);
            _tailWidthParam.SetValue(TAIL_WIDTH);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            for (int i = 0; i < COUNT; i++)
            {
                Spark spark = _sparks[i];
                if (spark.Age <= 0f || spark.Age >= LIFETIME) continue;

                float t = spark.Age / LIFETIME;

                //The head is where it is; the tail is where it was STREAK_SECONDS ago, which is its velocity
                //backwards. A spark that has been slowed by the drag draws shorter, which is the whole reason
                //the tail is computed rather than placed.
                _headParam.SetValue(spark.Position);
                _tailParam.SetValue(spark.Position - spark.Velocity * STREAK_SECONDS);

                //White-hot to the net's red as it cools, and gone on a square so it holds bright and then
                //drops away rather than dimming from the first frame - the smear's own envelope
                _colorParam.SetValue(Vector3.Lerp(HOT, COOL, t));
                _alphaParam.SetValue(1f - t * t);

                _pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// The shared quad, which is everything this component made — <c>LaunchSmears</c>'s own billboard, and
        /// the same contract: the vertex positions are unused and the texture channel carries (side in
        /// {−1, 1}, along in {0 tail, 1 head}), so the shader places it in world space from the two ends.
        /// </summary>
        private void CreateQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(Vector3.Zero, new Vector2(-1f, 0f)),
                new(Vector3.Zero, new Vector2(1f, 0f)),
                new(Vector3.Zero, new Vector2(-1f, 1f)),
                new(Vector3.Zero, new Vector2(1f, 1f))
            };

            _vertexBuffer = new VertexBuffer(_device, VertexPositionTexture.VertexDeclaration, corners.Length,
                BufferUsage.WriteOnly);
            _vertexBuffer.SetData(corners);

            short[] indices = { 0, 1, 2, 2, 1, 3 };

            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length,
                BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        /// <summary>The quad's two buffers. <b>Not</b> the effect, which the caller's content manager owns.</summary>
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;
        }
    }
}
