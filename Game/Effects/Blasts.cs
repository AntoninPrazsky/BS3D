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
    /// alarm must. So it is stepped with the very time the physics is stepped with, slow motion included: a
    /// cut's blade is struck into a world that then crawls, and a flash that ran at full speed over debris crawling
    /// in slow motion would be over before the balls it threw had moved. Under the result page it
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
    /// <para>
    /// <b>And a blade's impact (#767), in the same slots and the same draw</b>: a power-up round that cuts rather than
    /// bursts — today the Cutter severing a storey (<see cref="Cut"/>) — is a white-hot flash, a long slash along the storey
    /// it cut (<see cref="CutSlash"/>) and a grinder's fan of sparks thrown off the cut towards the gun, cooling to the
    /// cutter's amber (the shader's second style), each part lasting as long as <see cref="CutBeats"/>' severing figures
    /// say. It is here and not in the Cut's code so the next power-up round's impact is one more style of the same one
    /// buffer, and it keeps the blasts' clock: the world's, slow motion included. Its sound and its jolt are the caller's
    /// (<see cref="Update"/> plays a report and returns a jolt for bombs alone), since a cut answers with its own; and a shot
    /// fired while it burns plays the rest of it out fast (<see cref="HurryBlades"/>), so it is skippable as the slow
    /// motion is.
    /// </para>
    /// <para>
    /// The slots themselves - ages, the chain's stagger, the hurry, when a slot is freed, the light - are
    /// <see cref="BlastSlots"/>', which has no device and no sound, so the tests step the ages this draws from.
    /// </para>
    /// </summary>
    internal sealed class Blasts : IDisposable
    {
        /// <summary>Concurrent blasts. Must match <c>MAX_BLASTS</c> in Blast.fx (<see cref="BlastSlots.MAX_BLASTS"/>).</summary>
        public const int MAX_BLASTS = BlastSlots.MAX_BLASTS;

        /// <summary>How far behind the bomb that set it off each link of a chain goes off (<see cref="BlastSlots.CHAIN_STAGGER_SECONDS"/>).</summary>
        public const float CHAIN_STAGGER_SECONDS = BlastSlots.CHAIN_STAGGER_SECONDS;

        //Sparks per blast, and the blade's more (#767's verdict: "the sparks are barely perceptible, so few and so briefly
        //are they shown"): a bomb draws SPARKS_PER_BLAST, a blade those and BLADE_EXTRA_SPARKS more, the shader's part 3.
        //The buffer holds every slot's flash, fireball or slash and first sparks first (BOMB_QUADS), then every slot's extra
        //sparks, and the draw takes the second run only while a blade burns (BlastSlots.AnyBlade): a frame of bombs alone
        //costs what it did before the blade's sparks were doubled. 1 552 quads and 6 208 vertices in all, inside the 16-bit
        //index limit Fireworks is held to. It was 72 sparks for both, and the first capture read the spray as a few white
        //glints.
        private const int SPARKS_PER_BLAST = 96;
        private const int BLADE_EXTRA_SPARKS = 96;
        private const int BOMB_QUADS = MAX_BLASTS * (SPARKS_PER_BLAST + 2);
        private const int ALL_QUADS = BOMB_QUADS + MAX_BLASTS * BLADE_EXTRA_SPARKS;

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly BlastSlots _slots = new();

        //Per-blast uniform staging, allocated once and refilled in place.
        private readonly Vector4[] _centres = new Vector4[MAX_BLASTS];
        private readonly Vector4[] _shapes = new Vector4[MAX_BLASTS];
        private readonly Vector4[] _axes = new Vector4[MAX_BLASTS];

        private readonly VertexBuffer _vertexBuffer;
        private readonly IndexBuffer _indexBuffer;

        //Cached parameter handles: the by-name indexer is a linear scan, and these are set every frame a blast is up.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _cameraRightParam, _cameraUpParam;
        private readonly EffectParameter _centreParam, _shapeParam, _axisParam;

        /// <summary>
        /// GamePi's Blast.fx (#789) takes the centre, the shape and the slash's axis as ONE array, blast by blast, for
        /// Fireworks.cs's reason: DesktopGL and MojoShader disagree about where two dynamically indexed arrays are, and every
        /// blast read the other's slots. Null on the desktop.
        /// </summary>
        private readonly EffectParameter _packedParam;
        private readonly Vector4[] _packed;

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
            _axisParam = effect.Parameters["BlastAxis"];
            _packedParam = effect.Parameters["BlastData"];
            if (_packedParam != null) _packed = new Vector4[MAX_BLASTS * PACKED_PER_BLAST];

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
        public void SetOff(IReadOnlyList<Detonation> detonations) => _slots.SetOff(detonations);

        //GamePi's packed array (see _packedParam): the centre, the shape and the axis of each blast in turn
        private const int PACKED_PER_BLAST = 3;

        /// <summary>
        /// A blade's impact (#767): the flash, the slash and the sparks of a power-up round severing what it struck, at
        /// <paramref name="struck"/> (world space), the slash along <paramref name="slash"/> (<see cref="CutSlash"/>: level
        /// through the struck ball, square to the shot, across the storey it severed), at <paramref name="size"/> (0 to 1,
        /// from what the cut freed). Silent and joltless here: the caller answers a cut with its own sound and knock, on the
        /// frame it happens.
        /// </summary>
        public void Cut(Vector3 struck, CutSlash slash, float size) => _slots.Cut(struck, slash, size);

        /// <summary>
        /// A shot was fired while a cut's picture is up (#765: skippable, as its slow motion is): every blade still burning
        /// plays the rest of its picture <see cref="CutBeats.HURRY_RATE"/> times as fast, so the field clears in a few frames
        /// and nothing vanishes in one (<see cref="BlastSlots.HurryBlades"/>). Returns whether a blade was hurried now.
        /// </summary>
        public bool HurryBlades() => _slots.HurryBlades();

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
            int due = _slots.Step(elapsed);
            if (due == 0) return 0f;

            //The report and the jolt of each bomb whose moment came, on the frame its age crossed zero
            float jolt = 0f;
            for (int i = 0; i < MAX_BLASTS; i++)
            {
                if ((due & (1 << i)) == 0) continue;

                ref readonly BlastSlots.Blast blast = ref _slots[i];
                audio?.PlayBlast(blast.Position, blast.Size, blast.Link);
                jolt += blast.Size;
            }

            return jolt;
        }

        /// <summary>
        /// The light the blasts throw this frame, as one light: a chain's links go off inside a third of a second
        /// a few units apart, and one light at their weighted centre, as strong as their sum, lights the cluster
        /// the way they would together — for one slot where five would crowd the scene's own lamps out.
        /// </summary>
        public bool TryGetLight(out Vector3 position, out Vector3 color, out float range) =>
            _slots.TryGetLight(out position, out color, out range);

        /// <summary>Nothing left burning — a fresh level starts clean. Called when a level is built.</summary>
        public void Reset() => _slots.Reset();

        /// <summary>
        /// Every blast in one draw call. Additive and depth-read but writing no depth — the launch smear's states,
        /// in the smear's slot of the frame, so the cluster, the gun and the island in front hide what is behind
        /// them and the glass drawn afterwards composites over it.
        /// </summary>
        public void Draw(ICamera camera)
        {
            bool any = false;

            for (int i = 0; i < MAX_BLASTS; i++)
            {
                ref readonly BlastSlots.Blast blast = ref _slots[i];

                //A link still waiting its turn is sent as a dead slot, so the shader never has to reason about a
                //negative age.
                if (!blast.Active || blast.Age < 0f)
                {
                    _shapes[i] = Vector4.Zero;
                    continue;
                }

                any = true;
                _centres[i] = new Vector4(blast.Position, blast.Age);
                _shapes[i] = new Vector4(blast.Size, 1f, blast.Blade ? 1f : 0f, blast.Blade ? blast.Slash.HalfLength : 0f);
                _axes[i] = blast.Blade ? new Vector4(blast.Slash.Axis, blast.Slash.Offset) : Vector4.Zero;
            }

            if (!any) return;

            //The billboard basis, taken from the view matrix's rows rather than rebuilt per vertex.
            Matrix view = camera.View;

            _viewParam.SetValue(view);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);
            _cameraRightParam.SetValue(new Vector3(view.M11, view.M21, view.M31));
            _cameraUpParam.SetValue(new Vector3(view.M12, view.M22, view.M32));

            if (_packedParam != null)
            {
                for (int i = 0; i < MAX_BLASTS; i++)
                {
                    _packed[i * PACKED_PER_BLAST] = _centres[i];
                    _packed[i * PACKED_PER_BLAST + 1] = _shapes[i];
                    _packed[i * PACKED_PER_BLAST + 2] = _axes[i];
                }

                _packedParam.SetValue(_packed);
            }
            else
            {
                _centreParam.SetValue(_centres);
                _shapeParam.SetValue(_shapes);
                _axisParam.SetValue(_axes);
            }

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            //The blade's extra sparks, the buffer's second run, only while a blade burns (see SPARKS_PER_BLAST)
            int quads = _slots.AnyBlade() ? ALL_QUADS : BOMB_QUADS;

            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, quads * 2);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// Builds the one static buffer. A spark's direction is baked in rather than hashed in the shader for
        /// Fireworks' reason — its position is a pure function of its age, and a direction that changed between
        /// frames would make it jitter — and every slot gets its own pattern, so two bombs of a chain going off
        /// side by side do not throw the same spray.
        /// <para>
        /// Laid out in two runs (see <see cref="SPARKS_PER_BLAST"/>): every slot's parts 0 to 2 first, then every slot's
        /// part 3. Drawn in the order they are made, slot by slot; only where each lands in the buffer differs.
        /// </para>
        /// </summary>
        private void BuildBuffers(out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer)
        {
            BlastVertex[] vertices = new BlastVertex[ALL_QUADS * 4];
            short[] indices = new short[ALL_QUADS * 6];

            Random random = new(20260914);

            //The blade's own draws (#767's verdict) - where along the slash each spark comes off, and the extra sparks'
            //directions and jitters - from a stream of their own, so a bomb's sparks are exactly what they always were
            Random bladeRandom = new(20261017);

            //The first run is written from the buffer's start and the blade's extra sparks from BOMB_QUADS on
            int v = 0, n = 0;
            int vExtra = BOMB_QUADS * 4, nExtra = BOMB_QUADS * 6;

            for (int blast = 0; blast < MAX_BLASTS; blast++)
            {
                AddQuad(vertices, indices, ref v, ref n, blast, part: 0, Vector4.Zero, Vector4.Zero);
                AddQuad(vertices, indices, ref v, ref n, blast, part: 1, Vector4.Zero, Vector4.Zero);

                for (int spark = 0; spark < SPARKS_PER_BLAST + BLADE_EXTRA_SPARKS; spark++)
                {
                    bool extra = spark >= SPARKS_PER_BLAST;
                    Random draw = extra ? bladeRandom : random;
                    int count = extra ? BLADE_EXTRA_SPARKS : SPARKS_PER_BLAST;
                    int index = extra ? spark - SPARKS_PER_BLAST : spark;

                    //Fibonacci coverage, then scattered off it hard — Fireworks' two reasons, and the scatter is
                    //larger here: a blast is fragments, and even coverage that survives the scatter reads as a
                    //designed pattern.
                    float k = (index + 0.5f) / count;
                    float y = 1f - 2f * k;
                    float r = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
                    float phi = index * 2.39996323f + blast * 0.9f + (extra ? 1.3f : 0f);

                    Vector3 direction = new Vector3(MathF.Cos(phi) * r, y, MathF.Sin(phi) * r)
                        + new Vector3(
                            (float)(draw.NextDouble() * 2.0 - 1.0),
                            (float)(draw.NextDouble() * 2.0 - 1.0),
                            (float)(draw.NextDouble() * 2.0 - 1.0)) * 0.35f;
                    direction.Normalize();

                    //A wide speed spread, weighted towards the fast end: a volume of fragments with a bright front,
                    //not a hollow shell.
                    float speed = MathF.Pow((float)draw.NextDouble(), 0.55f);

                    float lifeJitter = (float)draw.NextDouble();
                    float sizeJitter = (float)draw.NextDouble();
                    float heatJitter = (float)draw.NextDouble();

                    //Where along a blade's slash the spark comes off, -1 to 1 of the way to its ends: squared towards the
                    //struck ball, where the blade went in, so the spray is thickest there and thins along the cut
                    float place = (float)(bladeRandom.NextDouble() * 2.0 - 1.0);
                    place *= MathF.Abs(place);

                    if (extra)
                        AddQuad(vertices, indices, ref vExtra, ref nExtra, blast, part: 3,
                            new Vector4(direction, speed), new Vector4(lifeJitter, sizeJitter, heatJitter, place));
                    else
                        AddQuad(vertices, indices, ref v, ref n, blast, part: 2,
                            new Vector4(direction, speed), new Vector4(lifeJitter, sizeJitter, heatJitter, place));
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
            public Vector4 Random;   //(life jitter, size jitter, heat jitter, where along a blade's slash -1..1)

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }
    }
}
