using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The flock the savanna, the desert, the outback and the tropical beach share (#235): one rest-pose
    /// <see cref="BirdMesh"/>, each bird's orbit, bank and cycle of wingbeats and glides seeded once, drawn one
    /// draw per bird. A service since #580 (<see cref="BackdropServices.Birds"/>) so a scene moved into its own
    /// <see cref="Backdrop"/> can still draw it; which scene's <see cref="BirdsConfig"/> it draws with is the
    /// caller's, every frame.
    /// </summary>
    internal sealed class BirdFlock : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;

        private readonly Effect _birdsEffect;
        private readonly BirdMesh _birdMesh;

        //Cached at load: Draw sets three of these PER BIRD, and the by-name indexer is a linear scan
        //(BestPractices.md, section 1).
        private readonly EffectParameter _birdWorldParam, _birdViewParam, _birdProjectionParam, _birdColorParam,
            _birdSunDirectionParam, _birdSunColorParam, _birdZenithParam, _birdHorizonParam,
            _birdFlapPhaseParam, _birdFlapAmountParam;

        private float[] _birdRadius, _birdAltitude, _birdOrbitSpeed, _birdOrbitPhase, _birdBobSpeed,
            _birdFlapPeriod, _birdFlapCyclePhase, _birdFlapBeats, _birdBurstFraction;

        //The band of orbits the flock is seeded over. The lean is taken from these same two numbers, so a
        //wide circle cannot end up leaning like a tight one however either is retuned.
        private const float BIRD_RADIUS_MIN = 28f;
        private const float BIRD_RADIUS_SPAN = 34f;

        //How hard a bird banks, on the tightest orbit and on the widest. A circling bird MUST lean - it is
        //half of what made the old billboard read as mechanical, since a camera-facing quad is always
        //upright. The lean follows the turn, but it is taken from the radius rather than from the honest
        //atan(v^2 / (g*r)): this flock deliberately circles far slower than a real kettle of vultures does
        //(unhurried is the whole look), and at these speeds that formula asks for about four degrees on the
        //wide orbits and seventy on the tight ones. The band below is what a soaring bird actually holds.
        private const float BIRD_BANK_TIGHT = 0.52f;
        private const float BIRD_BANK_WIDE = 0.26f;

        //How far the lean wanders, and how fast. A bird trimming its circle is never quite settled.
        private const float BIRD_BANK_DRIFT = 0.06f;
        private const float BIRD_BANK_DRIFT_SPEED = 0.23f;

        //The cycle a bird repeats: one burst of wingbeats, then a long glide. The burst is not rolled
        //here — it is derived from the beat rate in SeedBirdFlock, and capped so it cannot swallow the glide.
        private const float BIRD_CYCLE_MIN = 6.5f;
        private const float BIRD_CYCLE_SPAN = 7f;
        private const float BIRD_BEAT_HZ_MIN = 2.1f;
        private const float BIRD_BEAT_HZ_SPAN = 1.1f;
        private const float BIRD_BURST_MAX = 0.40f;

        //What is left of the wingbeat while a bird glides: the wings still breathe, they are just not
        //beating. Also the floor under the burst's envelope, so amount is continuous across both edges.
        private const float BIRD_GLIDE_TRIM = 0.06f;

        //Flock parameters (count, wingspan, bob, colour, flock centre) live in BirdsConfig, shared by the
        //savanna, desert and outback configs; Draw reads the active scene's Birds config each frame. The
        //mesh is one shared rest pose - what makes one bird differ from another is its world matrix and its
        //two flap uniforms - so only the per-bird state above is sized from the counts, in SeedBirdFlock.

        /// <summary>
        /// Loads <c>Birds.fx</c>, builds the one rest-pose mesh and seeds <paramref name="birdCount"/> birds — the
        /// largest count any scene that draws the flock asks for, which the renderer works out (see
        /// <see cref="SeedBirdFlock"/>).
        /// </summary>
        public BirdFlock(GraphicsDevice graphicsDevice, ContentManager content, int birdCount)
        {
            _graphicsDevice = graphicsDevice;

            //--- Birds: one shared rest-pose mesh, and each bird's orbit and flap cycle seeded once
            _birdsEffect = content.Load<Effect>("Shaders/Birds");
            _birdMesh = new BirdMesh(graphicsDevice);

            _birdWorldParam = _birdsEffect.Parameters["World"];
            _birdViewParam = _birdsEffect.Parameters["View"];
            _birdProjectionParam = _birdsEffect.Parameters["Projection"];
            _birdColorParam = _birdsEffect.Parameters["BirdColor"];
            _birdSunDirectionParam = _birdsEffect.Parameters["SunDirection"];
            _birdSunColorParam = _birdsEffect.Parameters["SunColor"];
            _birdZenithParam = _birdsEffect.Parameters["ZenithColor"];
            _birdHorizonParam = _birdsEffect.Parameters["HorizonColor"];
            _birdFlapPhaseParam = _birdsEffect.Parameters["FlapPhase"];
            _birdFlapAmountParam = _birdsEffect.Parameters["FlapAmount"];

            SeedBirdFlock(birdCount);
        }

        /// <summary>
        /// Seeds the shared bird flock, once: each bird's orbit and drift, and the cycle of wingbeat bursts and
        /// glides it flies. The savanna, desert, outback and tropical scenes share it, so it is sized to the
        /// largest of the four configs' counts (<paramref name="birdCount"/>, which the renderer works out) — otherwise a desert level's flock would be silently capped to
        /// the savanna's count (and so on, since no scene rebuilds on a NumPad2 switch). <see cref="Draw"/>
        /// caps its draw to the active scene's count; colour, size and centre are read per frame from that
        /// scene's Birds config. Deterministic seed, so the flock is the same every run.
        /// <para>
        /// There is no geometry here any more: <see cref="BirdMesh"/> is one rest pose shared by every bird
        /// and does not depend on the count, so it is built once alongside the effect.
        /// </para>
        /// </summary>
        private void SeedBirdFlock(int birdCount)
        {
            _birdRadius = new float[birdCount];
            _birdAltitude = new float[birdCount];
            _birdOrbitSpeed = new float[birdCount];
            _birdOrbitPhase = new float[birdCount];
            _birdBobSpeed = new float[birdCount];
            _birdFlapPeriod = new float[birdCount];
            _birdFlapCyclePhase = new float[birdCount];
            _birdFlapBeats = new float[birdCount];
            _birdBurstFraction = new float[birdCount];

            //Deterministic, so the flock is the same every run. All circle the same way, like a kettle of
            //vultures riding one thermal, each at its own radius, height and unhurried pace.
            Random birdRng = new(4242);
            for (int i = 0; i < birdCount; i++)
            {
                _birdRadius[i] = BIRD_RADIUS_MIN + (float)birdRng.NextDouble() * BIRD_RADIUS_SPAN;
                _birdAltitude[i] = (float)(birdRng.NextDouble() * 2.0 - 1.0) * 10f;
                _birdOrbitSpeed[i] = 0.10f + (float)birdRng.NextDouble() * 0.12f;
                _birdOrbitPhase[i] = (float)birdRng.NextDouble() * MathHelper.TwoPi;
                _birdBobSpeed[i] = 0.4f + (float)birdRng.NextDouble() * 0.5f;

                //A soaring bird GLIDES most of the time and beats its wings in short bursts. The old flock
                //beat at a fixed rate for ever, which is the other half of what read as mechanical: a
                //metronome that never rests and never hurries. Each bird gets its own long cycle and a WHOLE
                //number of beats to spend in it — whole, so a burst begins and ends with the stroke at its
                //neutral point and the wings can be handed back to the glide without a step.
                //
                //⚠ The burst's LENGTH is DERIVED from the two things that actually read — how many beats it
                //is, and how fast this bird beats. Rolling it on its own instead let a two-beat burst spread
                //across a long window and come out at 0.8 Hz, which reads as slow motion rather than as a
                //bird; a soaring bird beats about two to three times a second.
                _birdFlapBeats[i] = 2 + birdRng.Next(4);
                _birdFlapPeriod[i] = BIRD_CYCLE_MIN + (float)birdRng.NextDouble() * BIRD_CYCLE_SPAN;
                _birdFlapCyclePhase[i] = (float)birdRng.NextDouble();

                float beatRate = BIRD_BEAT_HZ_MIN + (float)birdRng.NextDouble() * BIRD_BEAT_HZ_SPAN;
                _birdBurstFraction[i] = MathF.Min(_birdFlapBeats[i] / (beatRate * _birdFlapPeriod[i]), BIRD_BURST_MAX);
            }
        }

        /// <summary>
        /// Draws the flock: each bird circles the config's flock centre on its own slow orbit, banked into
        /// the turn it is flying, beating or gliding on a cycle of its own. One draw per bird of the one
        /// shared <see cref="BirdMesh"/>, opaque and depth-writing. Called in the savanna, desert and outback
        /// scenes, which share the flock; <paramref name="birds"/> is the active scene's config, and its
        /// count is capped to the seeded state's size.
        /// <para>
        /// A draw per bird rather than one instanced pass, for <c>SceneRenderer.DrawFlame</c>'s reason: what varies
        /// per bird is three uniforms, there are nine of them at most and only in three scenes, and the
        /// alternative is an instance buffer and a vertex format for something that has neither. The mesh is
        /// bound once outside the loop, so a bird costs three parameter writes and a draw.
        /// </para>
        /// </summary>
        public void Draw(in SceneFrame frame, BirdsConfig birds)
        {
            _birdColorParam.SetValue(birds.Color.ToVector3());
            _birdViewParam.SetValue(frame.Camera.View);
            _birdProjectionParam.SetValue(frame.Camera.Projection);
            _birdSunDirectionParam.SetValue(frame.SunDirection);
            _birdSunColorParam.SetValue(frame.SunColor);
            _birdZenithParam.SetValue(frame.ZenithLinear);
            _birdHorizonParam.SetValue(frame.HorizonLinear);

            //Opaque and depth-writing, where the billboard was alpha-blended and depth-read: a bird is a
            //solid now, and its own breast has to be able to hide the far wing behind it.
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            //CullNone: the wings, the primaries and the tail are single sheets with no back to them, and the
            //pixel shader turns the normal towards the camera rather than the rasteriser dropping the face.
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_birdMesh.VertexBuffer);
            _graphicsDevice.Indices = _birdMesh.IndexBuffer;

            Vector3 flockCenter = birds.FlockCenter.ToVector3();
            Matrix scale = Matrix.CreateScale(birds.Wingspan);

            int count = Math.Min(birds.Count, _birdRadius.Length);
            for (int i = 0; i < count; i++)
            {
                float radius = _birdRadius[i];
                float angle = frame.Time * _birdOrbitSpeed[i] + _birdOrbitPhase[i];
                float bobPhase = frame.Time * _birdBobSpeed[i] + _birdOrbitPhase[i];

                Vector3 center = flockCenter + new Vector3(
                    MathF.Cos(angle) * radius,
                    _birdAltitude[i] + MathF.Sin(bobPhase) * birds.Bob,
                    MathF.Sin(angle) * radius);

                //The nose goes where the bird is actually going: the circle's horizontal tangent plus the
                //climb its drift is doing at this instant. A bird therefore tips up as it rises and down as
                //it sinks, off the derivative of its own bob rather than off a dial that says how far to tip.
                float horizontalSpeed = _birdOrbitSpeed[i] * radius;
                float climbRate = MathF.Cos(bobPhase) * _birdBobSpeed[i] * birds.Bob;
                Vector3 forward = new(
                    -MathF.Sin(angle) * horizontalSpeed,
                    climbRate,
                    MathF.Cos(angle) * horizontalSpeed);

                //Banked into the turn. Leaning the bird's UP vector towards the inside of its circle is the
                //whole of it — CreateWorld squares the basis up from there, so there is no hand-rolled cross
                //product here to get the handedness wrong in.
                float lean = MathHelper.Lerp(BIRD_BANK_TIGHT, BIRD_BANK_WIDE,
                        MathHelper.Clamp((radius - BIRD_RADIUS_MIN) / BIRD_RADIUS_SPAN, 0f, 1f))
                    + BIRD_BANK_DRIFT * MathF.Sin(frame.Time * BIRD_BANK_DRIFT_SPEED + _birdOrbitPhase[i]);

                Vector3 inward = new(-MathF.Cos(angle), 0f, -MathF.Sin(angle));
                Vector3 up = Vector3.Up * MathF.Cos(lean) + inward * MathF.Sin(lean);

                ResolveFlap(i, frame.Time, out float flapPhase, out float flapAmount);

                _birdWorldParam.SetValue(scale * Matrix.CreateWorld(center, forward, up));
                _birdFlapPhaseParam.SetValue(flapPhase);
                _birdFlapAmountParam.SetValue(flapAmount);

                _birdsEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _birdMesh.PrimitiveCount);
            }

            //Restore the scene block's states for the draws that follow
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Where bird <paramref name="index"/> is in its wingbeat, and how much of a wingbeat it is doing at
        /// all. A soaring bird spends most of its time gliding and beats in short bursts; the old flock beat
        /// at one fixed rate for ever, which is half of why it read as mechanical.
        /// <para>
        /// <b>Both edges of the burst are continuous, by construction rather than by smoothing.</b> A burst
        /// runs a WHOLE number of beats, so it ends on the same neutral point of the stroke it started on,
        /// and the glide that follows sweeps exactly one slow turn of the same phase — so phase is continuous
        /// across both boundaries without ever being tracked between frames. The envelope is floored at
        /// <see cref="BIRD_GLIDE_TRIM"/>, which is the value the glide holds, so the amount does not step
        /// either. Nothing here is stateful: a bird's whole flap is a function of the wall clock.
        /// </para>
        /// </summary>
        private void ResolveFlap(int index, float time, out float phase, out float amount)
        {
            float cycle = time / _birdFlapPeriod[index] + _birdFlapCyclePhase[index];
            cycle -= MathF.Floor(cycle);

            float burst = _birdBurstFraction[index];
            if (cycle < burst)
            {
                float progress = cycle / burst;
                phase = progress * _birdFlapBeats[index] * MathHelper.TwoPi;
                amount = MathF.Max(MathF.Sin(MathF.PI * progress), BIRD_GLIDE_TRIM);
            }
            else
            {
                phase = (cycle - burst) / (1f - burst) * MathHelper.TwoPi;
                amount = BIRD_GLIDE_TRIM;
            }
        }

        /// <summary>Frees the mesh. The effect is the content manager's.</summary>
        public void Dispose() => _birdMesh?.Dispose();
    }
}
