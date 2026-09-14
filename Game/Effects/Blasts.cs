using BS3D.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// A bomb going off (#389): the flash, the fireball and the sparks at the place each blast actually
    /// happened, the report spoken from there, and the light it throws on the cluster, the island and the gun.
    /// (There was a shock ring too, and the first capture in the running game threw it out — a clean hoop with an
    /// exact circular edge reads as a halo drawn over the cluster, not as anything exploding; see Blast.fx.)
    /// Until #389 a detonation had none of it — the only tells were the bomb's own pulsing shell before it went
    /// and its victims' trajectories after, and the victims were not even thrown the right way.
    /// <para>
    /// <b>Session-owned, on the SIMULATION's clock.</b> A blast is a thing that happens in the world, like the
    /// balls it throws — not a rule of the level, which is why it does not stamp the wall clock the way the floor
    /// alarm must. So it is stepped with the very time the physics is stepped with, slow motion included: the
    /// biggest blasts are exactly the ones that engage the drop cinematic, and a flash that ran at full speed over
    /// debris crawling in slow motion would be over before the balls it threw had moved. Under the result page it
    /// is stepped with the world (#241); under a pause it stops with it.
    /// </para>
    /// <para>
    /// <b>A chain is played as a chain.</b> The rule sets every bomb of a chain off inside one landing, and the
    /// debris of all of them is thrown on the same step — but a chain of five going off as one flash reads as one
    /// big bomb. Each link is staggered by <see cref="CHAIN_STAGGER_SECONDS"/> behind the one that set it off, the
    /// flash, the report and the jolt together. It is short on purpose: the debris is already flying, and a
    /// flash that trails its own fragments by more than a few frames reads as a second event.
    /// </para>
    /// <para>
    /// One static vertex buffer and <b>one draw call</b> for every blast at once, animated in <c>Blast.fx</c>'s
    /// vertex shader off two per-blast uniforms — <see cref="Fireworks"/>' idiom exactly, at the arena's scale.
    /// </para>
    /// </summary>
    internal sealed class Blasts : IDisposable
    {
        /// <summary>Concurrent blasts. Must match <c>MAX_BLASTS</c> in Blast.fx.</summary>
        public const int MAX_BLASTS = 8;

        //Sparks per blast. The buffer is MAX_BLASTS * (this + 2) quads — the flash and the fireball ride beside
        //them — which at 96 is 784 quads and 3 136 vertices, far inside the 16-bit index limit Fireworks is held
        //to. It was 72, and the first capture read the spray as a few white glints.
        private const int SPARKS_PER_BLAST = 96;
        private const int QUADS_PER_BLAST = SPARKS_PER_BLAST + 2;

        /// <summary>
        /// How long a slot stays taken. Must cover Blast.fx's longest-lived part — a spark at the top of its life
        /// jitter, <c>SPARK_SECONDS</c> × 1.45 = 0.87 s — or a spark is cut off mid-fade.
        /// </summary>
        private const float LIFE_SECONDS = 0.9f;

        /// <summary>
        /// How far behind the bomb that set it off each link of a chain goes off, in seconds. See the class
        /// remarks for why it is this short; a chain of five, the longest one landing sets off in the campaign
        /// (Sill, Paroxysm), is over in under a third of a second.
        /// </summary>
        public const float CHAIN_STAGGER_SECONDS = 0.07f;

        /// <summary>
        /// Balls destroyed at which a blast counts as full size. A blast's radius holds a couple of dozen cells of
        /// a packed cluster, and a bomb on a cluster's underside takes about half that — measured over the shipped
        /// bomb levels, 13 a bomb on Vent and Paroxysm and 23 on Sill.
        /// </summary>
        private const float FULL_SIZE_DESTROYED = 24f;

        /// <summary>The smallest a blast is drawn and heard, so a bomb that took only itself still plainly goes off.</summary>
        private const float MIN_SIZE = 0.35f;

        //The light the blast throws (see SceneLights.SetFlash): how long it lasts, how far it reaches and what it
        //is at full strength. Linear radiance, far over 1 on purpose — a point light's term is attenuated by the
        //square of the distance through its range and by N·L, and at 1 per channel a ball two units away took a
        //faint warm tint rather than being lit up.
        //
        //It started at (5, 2.2, 0.7) for 0.38 s and the first capture could barely find it on the cluster: the
        //balls round a blast took a faint warm cast and nothing read as having been LIT.
        private const float LIGHT_SECONDS = 0.45f;
        private const float LIGHT_RANGE = 10f;
        private static readonly Vector3 LIGHT_COLOR = new(8.5f, 3.6f, 1.0f);

        //The most the summed light of a chain may reach, as a multiple of LIGHT_COLOR. Five links going off in a
        //third of a second overlap, and uncapped their sum floods the whole cluster white.
        private const float LIGHT_CEILING = 2.5f;

        private struct Blast
        {
            public Vector3 Position;
            public float Age;            //negative while its link waits its turn, 0 at the moment it goes off
            public float Size;           //0..1
            public int Link;             //how far down its chain, 0 for a bomb the landing set off itself
            public bool Active;
            public bool Reported;        //has its report been played and its jolt handed out
        }

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly Blast[] _blasts = new Blast[MAX_BLASTS];

        //Per-blast uniform staging, allocated once and refilled in place.
        private readonly Vector4[] _centres = new Vector4[MAX_BLASTS];
        private readonly Vector4[] _shapes = new Vector4[MAX_BLASTS];

        private readonly VertexBuffer _vertexBuffer;
        private readonly IndexBuffer _indexBuffer;
        private readonly int _quadCount;

        //Cached parameter handles: the by-name indexer is a linear scan, and these are set every frame a blast is up.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _cameraRightParam, _cameraUpParam;
        private readonly EffectParameter _centreParam, _shapeParam;

        public Blasts(GraphicsDevice device, Effect effect)
        {
            _device = device;
            _effect = effect;

            _viewParam = effect.Parameters["View"];
            _projectionParam = effect.Parameters["Projection"];
            _cameraPositionParam = effect.Parameters["CameraPosition"];
            _cameraRightParam = effect.Parameters["CameraRight"];
            _cameraUpParam = effect.Parameters["CameraUp"];
            _centreParam = effect.Parameters["BlastCentre"];
            _shapeParam = effect.Parameters["BlastShape"];

            _quadCount = MAX_BLASTS * QUADS_PER_BLAST;
            BuildBuffers(out _vertexBuffer, out _indexBuffer);
        }

        /// <summary>
        /// Sets off every blast of a landing, each link of the chain queued behind the bomb that reached it.
        /// <para>
        /// <b>Copies what it needs out of <paramref name="detonations"/> here and now</b>: the list is the contact
        /// handler's own and the next landing refills it (see <c>BallLanding.Detonations</c>), while a chain plays
        /// out over several frames after this call returns.
        /// </para>
        /// <para>
        /// A ninth blast takes the <b>oldest</b> slot rather than being dropped: the oldest is the one furthest
        /// through its fade, so taking it loses the least that is still being looked at.
        /// </para>
        /// </summary>
        public void SetOff(IReadOnlyList<Detonation> detonations)
        {
            for (int i = 0; i < detonations.Count; i++)
            {
                Detonation detonation = detonations[i];

                int slot = FreeOrOldestSlot();

                _blasts[slot] = new Blast
                {
                    Position = detonation.World,
                    Age = -detonation.Link * CHAIN_STAGGER_SECONDS,
                    Link = detonation.Link,
                    Size = MathHelper.Lerp(MIN_SIZE, 1f,
                        MathHelper.Clamp(detonation.Destroyed / FULL_SIZE_DESTROYED, 0f, 1f)),
                    Active = true,
                    Reported = false
                };
            }
        }

        private int FreeOrOldestSlot()
        {
            int oldest = 0;

            for (int i = 0; i < _blasts.Length; i++)
            {
                if (!_blasts[i].Active) return i;
                if (_blasts[i].Age > _blasts[oldest].Age) oldest = i;
            }

            return oldest;
        }

        /// <summary>
        /// Advances every blast by <paramref name="elapsed"/> seconds of the <b>world's</b> time, and plays the
        /// report of each one whose moment has come.
        /// </summary>
        /// <returns>
        /// How much jolt the blasts that went off <b>this frame</b> ask of the camera — the sum of their sizes,
        /// zero on every other frame. Returned rather than applied, because the camera is the session's to move
        /// and under the result page it is not the session's at all.
        /// </returns>
        /// <param name="audio">
        /// Where each report is played. Passed per call rather than captured at construction, because the session
        /// that owns this is built before the host synthesizes its sounds — a captured reference would be a null
        /// for the life of the program. May be null, and then the blasts are silent.
        /// </param>
        public float Update(float elapsed, ProceduralAudio audio)
        {
            float jolt = 0f;

            for (int i = 0; i < _blasts.Length; i++)
            {
                if (!_blasts[i].Active) continue;

                _blasts[i].Age += elapsed;

                //On the frame the age crosses zero, not when the chain was queued, so the report, the jolt and the
                //flash arrive together however far down the chain the link was.
                if (!_blasts[i].Reported && _blasts[i].Age >= 0f)
                {
                    _blasts[i].Reported = true;

                    audio?.PlayBlast(_blasts[i].Position, _blasts[i].Size, _blasts[i].Link);
                    jolt += _blasts[i].Size;
                }

                if (_blasts[i].Age > LIFE_SECONDS) _blasts[i].Active = false;
            }

            return jolt;
        }

        /// <summary>
        /// The light the blasts throw this frame, as one light: a chain's links go off inside a third of a second
        /// a few units apart, and one light at their weighted centre, as strong as their sum, lights the cluster
        /// the way they would together — for one slot where five would crowd the scene's own lamps out.
        /// </summary>
        public bool TryGetLight(out Vector3 position, out Vector3 color, out float range)
        {
            float total = 0f, largest = 0f;
            Vector3 weighted = Vector3.Zero;

            for (int i = 0; i < _blasts.Length; i++)
            {
                Blast blast = _blasts[i];
                if (!blast.Active || blast.Age < 0f || blast.Age >= LIGHT_SECONDS) continue;

                //Squared, the ceiling flash's rule: unmistakable on the frame it goes off, thinned well before its end.
                float left = 1f - blast.Age / LIGHT_SECONDS;
                float strength = blast.Size * left * left;

                total += strength;
                weighted += blast.Position * strength;
                largest = MathF.Max(largest, blast.Size);
            }

            if (total <= 0f)
            {
                position = color = Vector3.Zero;
                range = 0f;
                return false;
            }

            position = weighted / total;
            color = LIGHT_COLOR * MathF.Min(total, LIGHT_CEILING);
            range = LIGHT_RANGE * (0.7f + 0.3f * largest);
            return true;
        }

        /// <summary>Nothing left burning — a fresh level starts clean. Called when a level is built.</summary>
        public void Reset()
        {
            for (int i = 0; i < _blasts.Length; i++) _blasts[i].Active = false;
        }

        /// <summary>
        /// Every blast in one draw call. Additive and depth-read but writing no depth — the launch smear's states,
        /// in the smear's slot of the frame, so the cluster, the gun and the island in front hide what is behind
        /// them and the glass drawn afterwards composites over it.
        /// </summary>
        public void Draw(ICamera camera)
        {
            bool any = false;

            for (int i = 0; i < _blasts.Length; i++)
            {
                Blast blast = _blasts[i];

                //A link still waiting its turn is sent as a dead slot, so the shader never has to reason about a
                //negative age.
                if (!blast.Active || blast.Age < 0f)
                {
                    _shapes[i] = Vector4.Zero;
                    continue;
                }

                any = true;
                _centres[i] = new Vector4(blast.Position, blast.Age);
                _shapes[i] = new Vector4(blast.Size, 1f, 0f, 0f);
            }

            if (!any) return;

            //The billboard basis, taken from the view matrix's rows rather than rebuilt per vertex.
            Matrix view = camera.View;

            _viewParam.SetValue(view);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);
            _cameraRightParam.SetValue(new Vector3(view.M11, view.M21, view.M31));
            _cameraUpParam.SetValue(new Vector3(view.M12, view.M22, view.M32));

            _centreParam.SetValue(_centres);
            _shapeParam.SetValue(_shapes);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _quadCount * 2);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// Builds the one static buffer. A spark's direction is baked in rather than hashed in the shader for
        /// Fireworks' reason — its position is a pure function of its age, and a direction that changed between
        /// frames would make it jitter — and every slot gets its own pattern, so two bombs of a chain going off
        /// side by side do not throw the same spray.
        /// </summary>
        private void BuildBuffers(out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer)
        {
            BlastVertex[] vertices = new BlastVertex[_quadCount * 4];
            short[] indices = new short[_quadCount * 6];

            Random random = new(20260914);

            int v = 0, n = 0;

            for (int blast = 0; blast < MAX_BLASTS; blast++)
            {
                AddQuad(vertices, indices, ref v, ref n, blast, part: 0, Vector4.Zero, Vector4.Zero);
                AddQuad(vertices, indices, ref v, ref n, blast, part: 1, Vector4.Zero, Vector4.Zero);

                for (int spark = 0; spark < SPARKS_PER_BLAST; spark++)
                {
                    //Fibonacci coverage, then scattered off it hard — Fireworks' two reasons, and the scatter is
                    //larger here: a blast is fragments, and even coverage that survives the scatter reads as a
                    //designed pattern.
                    float k = (spark + 0.5f) / SPARKS_PER_BLAST;
                    float y = 1f - 2f * k;
                    float r = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
                    float phi = spark * 2.39996323f + blast * 0.9f;

                    Vector3 direction = new Vector3(MathF.Cos(phi) * r, y, MathF.Sin(phi) * r)
                        + new Vector3(
                            (float)(random.NextDouble() * 2.0 - 1.0),
                            (float)(random.NextDouble() * 2.0 - 1.0),
                            (float)(random.NextDouble() * 2.0 - 1.0)) * 0.35f;
                    direction.Normalize();

                    //A wide speed spread, weighted towards the fast end: a volume of fragments with a bright front,
                    //not a hollow shell.
                    float speed = MathF.Pow((float)random.NextDouble(), 0.55f);

                    AddQuad(vertices, indices, ref v, ref n, blast, part: 2,
                        new Vector4(direction, speed),
                        new Vector4((float)random.NextDouble(), (float)random.NextDouble(),
                            (float)random.NextDouble(), 0f));
                }
            }

            vertexBuffer = new VertexBuffer(_device, BlastVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
        }

        private static void AddQuad(BlastVertex[] vertices, short[] indices, ref int v, ref int n, int blast, int part,
            Vector4 spark, Vector4 random)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = (corner == 0 || corner == 3) ? -1f : 1f;
                float cy = (corner < 2) ? 1f : -1f;

                vertices[v + corner] = new BlastVertex
                {
                    Slot = new Vector4(blast, part, cx, cy),
                    Spark = spark,
                    Random = random
                };
            }

            indices[n++] = (short)(v + 0);
            indices[n++] = (short)(v + 1);
            indices[n++] = (short)(v + 2);
            indices[n++] = (short)(v + 0);
            indices[n++] = (short)(v + 2);
            indices[n++] = (short)(v + 3);

            v += 4;
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }

        /// <summary>One corner of one quad. Everything the vertex shader needs to place it.</summary>
        private struct BlastVertex : IVertexType
        {
            public Vector4 Slot;     //(blast, part, corner x, corner y)
            public Vector4 Spark;    //(direction xyz, speed)
            public Vector4 Random;   //(life jitter, size jitter, heat jitter, unused)

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }
    }
}
