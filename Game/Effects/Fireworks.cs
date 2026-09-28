using BS3D.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The victory display: shells that rise from around the island, whistle, burst over the arena and rain
    /// down, with the report arriving as they go off. Started when a level is cleared and left to run over the
    /// result screen behind the score.
    /// <para>
    /// It lives on the <b>host</b> rather than on the gameplay screen, and that is what makes it work at all.
    /// A cleared level hands the player a result page pushed <i>over</i> the session, and what the session
    /// still runs under that page is its <i>world</i> alone (#241) — the simulation and the gun, not the
    /// level's own furniture. A celebration owned by the session would therefore freeze at the exact moment
    /// the player is meant to be watching it, and would go with the session the moment Main Menu tears it
    /// down besides. Owned by the frame it keeps running whatever is on the stack, exactly as the clouds and
    /// the wall clock do.
    /// </para>
    /// <para>
    /// One static vertex buffer and <b>one draw call</b> for the whole display: every shell and every spark is
    /// animated in <c>Fireworks.fx</c>'s vertex shader from a handful of per-shell uniforms, so nothing is
    /// rebuilt or re-uploaded per frame. The C# side owns only what the shader cannot know — when a shell goes
    /// up, where it bursts, what colour it is, and when to make a noise.
    /// </para>
    /// </summary>
    public sealed class Fireworks : IDisposable
    {
        /// <summary>Concurrent shells. Must match <c>MAX_SHELLS</c> in Fireworks.fx.</summary>
        public const int MAX_SHELLS = 32;

        //Sparks in a shell. The whole buffer is MAX_SHELLS * this quads, which has to stay under the 16-bit
        //index limit of 65 536 vertices — at 320 that is 17 920, comfortably inside it. (CreateGridMesh's own
        //32-bit lesson, from the other direction.)
        //
        //It was 120 and that was far too few, which is worth recording because the arithmetic is not obvious:
        //a burst subtends about 19° from the play camera, so 120 sparks spread over that disc are a couple of
        //hundred pixels apart and the whole thing reads as three or four lonely glints rather than as an
        //explosion. Spark COUNT and spark SIZE both have to scale with the burst radius or a bigger shell
        //looks emptier than a small one, which is exactly backwards.
        //
        //At 32 shells this is 6 400 quads, 25 600 vertices — the ceiling here is the 16-bit index buffer, and
        //MAX_SHELLS * SPARKS_PER_SHELL * 4 must stay under 65 536. Raising either without checking that is how
        //far triangles quietly start referencing the wrong vertices (the lesson CreateGridMesh's grids taught).
        private const int SPARKS_PER_SHELL = 200;

        //Where the shells are fired from and where they go off. The ring sits outside the island (radius 26)
        //so a launch is never inside the stone, and the burst ceiling is high enough that a burst clears the
        //hanging cluster and reads against open sky rather than through the map.
        private const float LAUNCH_RING_MIN = 34f, LAUNCH_RING_MAX = 110f;
        private const float LAUNCH_Y = -8f;

        //Spread wide and high, because the brief is a sky FULL of them rather than a few over the arena. The
        //play camera looks up at the cluster, so bursts below about 40 fall behind it and are never seen; the
        //ceiling is set by the far plane and by the shells staying large enough to read.
        private const float BURST_Y_MIN = 44f, BURST_Y_MAX = 122f;
        private const float BURST_SPREAD = 52f;

        //Shortened to stay tied to the launch sound, which is now half a second: a shell that goes on climbing
        //for a second after its own hiss has finished reads as two unrelated things. Snappier to watch, too.
        private const float RISE_MIN = 0.62f, RISE_MAX = 0.95f;
        private const float RADIUS_MIN = 19f, RADIUS_MAX = 40f;
        private const float LIFE_MIN = 1.9f, LIFE_MAX = 3.0f;

        //Seconds between launches, in three phases. The opening barrage is deliberately the fastest — the
        //moment the field clears wants everything at once — and it then settles; at a steady 0.17 s against a
        //rise of ~1.2 s and a life of ~2.5 s there are around twenty shells in the air.
        //
        //And then it EASES OFF. The display now runs for a minute so it is still going while the player reads
        //their score, and a full minute at the barrage's density is exhausting rather than celebratory: past
        //RELAXED_AFTER it thins to a shell every half-second, which keeps the sky busy without shouting.
        private const float INTERVAL_OPENING = 0.07f, INTERVAL_STEADY = 0.17f, INTERVAL_RELAXED = 0.52f;
        private const float OPENING_SECONDS = 2.2f, RELAXED_AFTER = 16f;

        //The hues, in linear radiance ratios, IN ORDER ROUND THE COLOUR WHEEL — the pairing below counts steps
        //round it. Every one is two channels and a ZERO (#612), and that zero is the whole trick: the resolve's
        //curve (Tonemap.fx's ACES fit) works per channel, and a spark is driven far up its shoulder, where it
        //squeezes the dominant channel under 1 and lets every minor channel catch up — so a red with a fifth of
        //green and blue in it (the old palette's) came out pink-white at its peak, whatever else was done. A
        //channel at zero stays at zero through any curve, so a spark's hue survives its own brightness. The
        //second channel is small for the same reason: it is lifted far more than the dominant one, which is
        //also why a spark drifts a little warmer (gold to orange, orange to red) as it cools, like a real ember.
        private static readonly Vector3[] PALETTE =
        {
            new(1.00f, 0.004f, 0.00f),  //red
            new(1.00f, 0.070f, 0.00f),  //orange
            new(1.00f, 0.240f, 0.00f),  //gold
            new(0.00f, 1.000f, 0.02f),  //green
            new(0.00f, 0.500f, 1.00f),  //cyan
            new(0.00f, 0.030f, 1.00f),  //blue
            new(0.06f, 0.000f, 1.00f),  //violet
            new(1.00f, 0.000f, 0.30f)   //magenta
        };

        //How bright a hue is at its peak, as LUMINANCE (#612). GLARE_THRESHOLD is 0.55 on luminance and a
        //firework that does not bloom is a coloured dot, so every hue has to be over it — but a hue's luminance
        //is mostly its green, so one multiplier for all of them (the old 4.2) put the greens at 3.4 and the reds
        //at 1.6, and the bright ones washed out first. Each hue is boosted to this luminance instead (red ×7.0,
        //green ×2.1, cyan ×3.5), within BOOST_MIN..BOOST_MAX: the floor keeps a green from going dim, the
        //ceiling holds a blue or a violet (under a tenth of a unit of luminance per unit of radiance) at ×10,
        //where it still blooms (0.94 and 0.85) and its small second channel has not yet turned it cyan or pink.
        //1.5 matches the old display's measured brightness: the display's pixels average the same value (HSV
        //V 0.46 against 0.47) over a dark sky, so the colour came back without the display getting dimmer.
        private const float SPARK_LUMINANCE = 1.5f;
        private const float BOOST_MIN = 2.0f, BOOST_MAX = 10.0f;

        //The palette as the shader is handed it, boosted once at load rather than per launch.
        private static readonly Vector3[] SHELL_COLOURS = BoostPalette();

        //How far round the wheel a shell's second colour sits from its first: three to five steps of eight, so
        //it is the complement or one of its two neighbours (#612). "Any different one" gave red with orange and
        //gold with white as often as anything — two halves nobody could tell apart.
        private const int PAIR_STEP_MIN = 3, PAIR_STEP_SPREAD = 3;

        //The spark's hot core (#612): how much of the way to white a spark goes at its brightest, and the
        //brightness it starts from. It was 70 % white over nearly its whole life (from a brightness of 0.26),
        //which is most of why the display read white — "burning, not a coloured dot" wants a hot flash when a
        //spark is fresh, not a white spark with a coloured edge.
        private const float HOT_CORE = 0.25f;
        private const float HOT_CORE_FROM = 0.6f, HOT_CORE_WIDTH = 0.3f;

        //How much of what is behind a spark it hides, per unit of the light it gives (#612). A spark was purely
        //ADDED to the frame, and light added to a bright sky can only move it towards white: over the meadow's
        //noon blue a red spark lifts the red channel and leaves the sky's green and blue where they were, so the
        //best it could ever be was pale pink — the one case no palette or boost could fix. The result page makes
        //it worse, since its defocus averages a thin streak into the sky around it before the curve.
        //
        //So a spark covers the sky by its own weight a² times this (Fireworks.fx), and the blend reads as the
        //spark's colour at radiance / SKY_COVER laid over the sky at that opacity. Over a dark sky that is
        //exactly the additive display it was, until a spark is opaque. Swept at 1.8, 2.5, 5 and 12 (and at 0.85
        //on the weight rather than its square): the display's pixels over the meadow's sky stayed at HSV
        //saturation 0.32–0.35 against the old 0.31 until 12, which reached 0.40 — and over a cloud 0.22 against
        //0.01. Higher was not photographed but computed: the colour a dim spark tends to is radiance / SKY_COVER
        //(a red of 0.58 at 12), and much past 12 it goes dark enough to read as soot on the sky.
        private const float SKY_COVER = 12f;

        private struct Shell
        {
            public Vector3 Origin;
            public Vector3 Burst;
            public Vector3 Color;
            public Vector3 ColorB;
            public float Age;            //negative while rising, 0 at the burst, then up to Life
            public float Rise;
            public float Radius;
            public float Life;
            public float Flatten;
            public float Twinkle;
            public bool Active;
            public bool Reported;        //has its bang been played
        }

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly ProceduralAudio _audio;
        private readonly Random _random = new();

        private readonly Shell[] _shells = new Shell[MAX_SHELLS];

        //Per-shell uniform staging. Allocated once and refilled in place — a fresh array per frame would be a
        //per-frame managed allocation on the gameplay path.
        private readonly Vector4[] _origins = new Vector4[MAX_SHELLS];
        private readonly Vector4[] _bursts = new Vector4[MAX_SHELLS];
        private readonly Vector4[] _colors = new Vector4[MAX_SHELLS];
        private readonly Vector4[] _colorsB = new Vector4[MAX_SHELLS];
        private readonly Vector4[] _shapes = new Vector4[MAX_SHELLS];

        private readonly VertexBuffer _vertexBuffer;
        private readonly IndexBuffer _indexBuffer;
        private readonly int _quadCount;

        //PREMULTIPLIED "over" (#612): the shader returns the light a spark adds, and in alpha how much of what is
        //behind it the spark covers (SKY_COVER), so the frame keeps the destination by what the spark has not
        //taken. At a cover of zero this is exactly the additive blend it replaced. The target's own alpha is
        //left alone — nothing reads it, and the additive state it replaced was writing it for no one. Static,
        //like every state object here (BestPractices.md).
        private static readonly BlendState SparkBlend = new()
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One
        };

        //Cached parameter handles: the by-name indexer is a linear scan, and these are set every frame.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _cameraRightParam, _cameraUpParam;
        private readonly EffectParameter _originParam, _burstParam, _colorParam, _colorBParam, _shapeParam;

        private float _remaining;        //seconds of celebration left to launch into
        private float _untilNextLaunch;
        private float _sinceStart;

        //How long this display's fastest phase lasts. A field rather than the constant, because a block
        //milestone asks for a longer barrage than an ordinary clear does - see Celebrate.
        private float _opening = OPENING_SECONDS;
        private float _delay;            //seconds still to wait before the first shell goes up
        private bool _popped;            //the opening crack has been played for this celebration

        //How many reports have gone off lately: one added per report, decaying with REPORT_CROWD_SECONDS. The audio
        //turns each report down by it (ProceduralAudio.BurstCrowding), so the loudness of the display stops following
        //its launch rate (#552). Kept here because this is the clock the reports are fired on.
        private float _reportCrowd;

        /// <summary>
        /// The time constant of the report crowd (#552). About as long as a report's boom lasts: shorter and the
        /// barrage's reports would no longer see each other, longer and a lone shell well after the barrage would
        /// still be ducked by it. At 1.2 s the steady phase (~6 reports a second) holds each report about 9 dB
        /// under a lone one and the barrage (~13) about 12, which is what keeps the stream near one report's power.
        /// </summary>
        public const float REPORT_CROWD_SECONDS = 1.2f;

        /// <summary>True while anything is still in the air, so a caller can hold a screen until it is over.</summary>
        public bool Active
        {
            get
            {
                if (_remaining > 0f) return true;
                for (int i = 0; i < _shells.Length; i++) if (_shells[i].Active) return true;
                return false;
            }
        }

        public Fireworks(GraphicsDevice device, Effect effect, ProceduralAudio audio)
        {
            _device = device;
            _effect = effect;
            _audio = audio;

            _viewParam = effect.Parameters["View"];
            _projectionParam = effect.Parameters["Projection"];
            _cameraPositionParam = effect.Parameters["CameraPosition"];
            _cameraRightParam = effect.Parameters["CameraRight"];
            _cameraUpParam = effect.Parameters["CameraUp"];
            _originParam = effect.Parameters["ShellOrigin"];
            _burstParam = effect.Parameters["ShellBurst"];
            _colorParam = effect.Parameters["ShellColor"];
            _colorBParam = effect.Parameters["ShellColorB"];
            _shapeParam = effect.Parameters["ShellShape"];

            //Set once: none of these changes for the life of the display. The size is in WORLD units and a
            //burst is 40-120 units up, so it has to be far larger than it sounds — at half a unit a spark is a
            //subpixel glint from the play camera and the burst disappears.
            effect.Parameters["SparkSize"].SetValue(1.3f);
            effect.Parameters["Gravity"].SetValue(9.4f);

            //World units of streak per (world unit per second) of screen-projected spark speed. A spark leaves
            //the burst at roughly radius * DRAG ≈ 70 u/s, so this draws it as a streak some tens of units long
            //on the flash frame and shortens it to a dot within a few tenths of a second — the line, then the
            //break-up, then the drift.
            effect.Parameters["SparkStretch"].SetValue(0.42f);

            effect.Parameters["HotCore"].SetValue(HOT_CORE);
            effect.Parameters["HotCoreFrom"].SetValue(HOT_CORE_FROM);
            effect.Parameters["HotCoreWidth"].SetValue(HOT_CORE_WIDTH);
            effect.Parameters["SkyCover"].SetValue(SKY_COVER);

            _quadCount = MAX_SHELLS * SPARKS_PER_SHELL;
            BuildBuffers(out _vertexBuffer, out _indexBuffer);
        }

        /// <summary>
        /// Start (or extend) a celebration lasting <paramref name="seconds"/> of launches. Safe to call while
        /// one is already running — it takes the longer of the two rather than restarting, so a second call
        /// cannot cut a display short.
        /// </summary>
        /// <param name="delay">
        /// Seconds to wait before the first shell goes up. It exists so the victory fanfare gets its opening
        /// statement to itself: the level ends, brass announces it, and only then does the sky start going off
        /// — which reads as a celebration answering the announcement rather than as everything happening at
        /// once and nothing being heard.
        /// </param>
        /// <param name="openingSeconds">
        /// How long the fastest phase lasts, overriding <see cref="OPENING_SECONDS"/>. It is the one dial that
        /// makes a display read as <b>bigger</b> rather than merely longer (#184): the density is already at its
        /// maximum in the opening barrage, and the player is looking at the sky in the first seconds and reading
        /// their score by the time it has eased off — so a longer total changes what nobody is watching, where a
        /// longer opening changes the only part they see. Zero or less keeps the default.
        /// </param>
        public void Celebrate(float seconds, float delay = 0f, float openingSeconds = 0f)
        {
            bool wasIdle = _remaining <= 0f;

            _remaining = MathF.Max(_remaining, seconds + delay);

            //Taken on the LONGER one for the same reason the duration is: a second call must not be able to cut
            //a display short, and a block milestone landing on top of an ordinary clear's display is exactly
            //that call. Held outside the wasIdle gate below, which only initialises a display that is starting.
            _opening = MathF.Max(_opening, openingSeconds > 0f ? openingSeconds : OPENING_SECONDS);

            if (!wasIdle) return;

            _sinceStart = 0f;
            _untilNextLaunch = 0f;
            _delay = delay;
            _popped = false;
        }

        /// <summary>Ends the display and clears anything still in the air. Called when a level is built.</summary>
        public void Stop()
        {
            _remaining = 0f;
            _delay = 0f;
            _opening = OPENING_SECONDS;
            _reportCrowd = 0f;
            for (int i = 0; i < _shells.Length; i++) _shells[i].Active = false;
        }

        /// <summary>
        /// Advances every shell, launches new ones while the celebration lasts, and plays the report of any
        /// that crosses from rising to burst on this frame.
        /// </summary>
        /// <remarks>
        /// It needs no camera any more (#75): a shell's noise is made at the shell's own position and the ears
        /// are the host's, posed once a frame from the one camera there is.
        /// <para>
        /// A report is placed once and then left, and the listener does <b>not</b> stand still under it — the
        /// result page swings the released camera around the arena for the whole display (and the
        /// <c>celebrate</c> argument runs over the orbiting backdrop instead). Over the 2.6 s a report lasts,
        /// that is a few
        /// degrees of bearing on a burst forty to a hundred and twenty units up — the same trade the release
        /// makes, and tracking it would cost a held voice and an <c>Apply3D</c> every frame for a drift the ear
        /// has nothing to compare against.
        /// </para>
        /// </remarks>
        public void Update(float elapsed)
        {
            _reportCrowd *= MathF.Exp(-elapsed / REPORT_CROWD_SECONDS);

            for (int i = 0; i < _shells.Length; i++)
            {
                if (!_shells[i].Active) continue;

                _shells[i].Age += elapsed;

                //The bang is played on the frame the age crosses zero, not when the shell was launched, so
                //the sound and the flash arrive together however long the rise took.
                if (!_shells[i].Reported && _shells[i].Age >= 0f)
                {
                    _shells[i].Reported = true;

                    //Size drives the volume and, inversely, the pitch — a big shell is a deeper, louder report
                    float size = MathHelper.Clamp((_shells[i].Radius - RADIUS_MIN) / (RADIUS_MAX - RADIUS_MIN), 0f, 1f);
                    _audio?.PlayFireworkBurst(_shells[i].Burst, size, _reportCrowd);
                    _reportCrowd += 1f;
                }

                if (_shells[i].Age > _shells[i].Life) _shells[i].Active = false;
            }

            if (_remaining <= 0f) return;

            _remaining -= elapsed;

            //The wait before the first shell.
            if (_delay > 0f)
            {
                _delay -= elapsed;
                if (_delay > 0f) return;
            }

            //The popper fires when the display STARTS rather than when the celebration was asked for, because
            //it is the fireworks' own opening crack — announcing them, not the win, which the fanfare has
            //already done. Flagged rather than hung off the delay reaching zero: at a delay of nothing that
            //branch never runs, and the opening crack of a celebration is not something to lose to an edge
            //case (the `celebrate` test argument asks for exactly that case).
            if (!_popped)
            {
                _popped = true;
                _audio?.PlayPartyPopper();
            }

            _sinceStart += elapsed;
            _untilNextLaunch -= elapsed;

            while (_untilNextLaunch <= 0f)
            {
                Launch();

                float interval =
                    _sinceStart < _opening ? INTERVAL_OPENING :
                    _sinceStart < RELAXED_AFTER ? INTERVAL_STEADY : INTERVAL_RELAXED;

                _untilNextLaunch += interval * (0.7f + (float)_random.NextDouble() * 0.6f);
            }
        }

        /// <summary>Fires one shell into a free slot, if there is one, and whistles it up.</summary>
        private void Launch()
        {
            int slot = -1;
            for (int i = 0; i < _shells.Length; i++)
                if (!_shells[i].Active) { slot = i; break; }

            //Every slot busy is not a fault: the display is already as dense as it is allowed to get, and the
            //next launch will find room. Dropping this one is what keeps MAX_SHELLS a real ceiling.
            if (slot < 0) return;

            float launchAngle = (float)(_random.NextDouble() * Math.PI * 2.0);
            float launchRadius = Lerp(LAUNCH_RING_MIN, LAUNCH_RING_MAX, (float)_random.NextDouble());

            Vector3 origin = new(
                MathF.Cos(launchAngle) * launchRadius,
                LAUNCH_Y,
                MathF.Sin(launchAngle) * launchRadius);

            //The burst drifts from over the launch point rather than being placed independently, so a shell
            //visibly goes up from where it was fired instead of sliding across the sky on its way.
            float burstAngle = launchAngle + (float)(_random.NextDouble() - 0.5) * 0.9f;
            float burstRadius = launchRadius * 0.75f + (float)_random.NextDouble() * BURST_SPREAD * 0.5f;

            Vector3 burst = new(
                MathF.Cos(burstAngle) * burstRadius,
                Lerp(BURST_Y_MIN, BURST_Y_MAX, (float)_random.NextDouble()),
                MathF.Sin(burstAngle) * burstRadius);

            float rise = Lerp(RISE_MIN, RISE_MAX, (float)_random.NextDouble());

            //Two colours per shell, and the second is picked ACROSS the wheel from the first (PAIR_STEP_MIN) —
            //a shell that comes out half gold and half orange is just a gold shell that cost an extra uniform.
            //Stepping a random distance round the palette rather than re-rolling guarantees it without a
            //rejection loop.
            int colourA = _random.Next(SHELL_COLOURS.Length);
            int colourB = (colourA + PAIR_STEP_MIN + _random.Next(PAIR_STEP_SPREAD)) % SHELL_COLOURS.Length;

            _shells[slot] = new Shell
            {
                Origin = origin,
                Burst = burst,
                Color = SHELL_COLOURS[colourA],
                ColorB = SHELL_COLOURS[colourB],
                Age = -rise,
                Rise = rise,
                Radius = Lerp(RADIUS_MIN, RADIUS_MAX, (float)_random.NextDouble()),
                Life = Lerp(LIFE_MIN, LIFE_MAX, (float)_random.NextDouble()),

                //One shell in four is a flattened ring rather than a sphere. Variety, but cheaply: the shader
                //squashes the burst directions in Y, so a ring seen off its plane is a ring and seen down it
                //is a line — which is what real ring shells do and what makes the display read as several
                //kinds of firework rather than one repeated.
                Flatten = _random.NextDouble() < 0.25 ? 0.18f + (float)_random.NextDouble() * 0.16f : 1f,
                Twinkle = 0.15f + (float)_random.NextDouble() * 0.4f,
                Active = true,
                Reported = false
            };

            _audio?.PlayFireworkLaunch(origin);
        }

        /// <summary>
        /// The whole display in one draw call. Depth-read but writing no depth, so a burst behind the cluster or
        /// a tower is hidden by it and one in the open glows over everything — the depth states the launch smear
        /// and the campfire flame use, and for the same reasons. Blended by <see cref="SparkBlend"/>: added
        /// light, plus the share of the sky behind a spark that it covers.
        /// </summary>
        public void Draw(ICamera camera)
        {
            bool any = false;
            for (int i = 0; i < _shells.Length; i++)
            {
                Shell shell = _shells[i];

                if (!shell.Active)
                {
                    //Alpha 0 is the shader's "dead slot": the quads collapse and cost nothing.
                    _colors[i] = Vector4.Zero;
                    continue;
                }

                any = true;
                _origins[i] = new Vector4(shell.Origin, shell.Rise);
                _bursts[i] = new Vector4(shell.Burst, shell.Age);
                _colors[i] = new Vector4(shell.Color, 1f);
                _colorsB[i] = new Vector4(shell.ColorB, 1f);
                _shapes[i] = new Vector4(shell.Radius, shell.Life, shell.Flatten, shell.Twinkle);
            }

            if (!any) return;

            //The billboard basis, taken from the view matrix's rows rather than rebuilt per vertex.
            Matrix view = camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);

            _viewParam.SetValue(view);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);
            _cameraRightParam.SetValue(right);
            _cameraUpParam.SetValue(up);

            _originParam.SetValue(_origins);
            _burstParam.SetValue(_bursts);
            _colorParam.SetValue(_colors);
            _colorBParam.SetValue(_colorsB);
            _shapeParam.SetValue(_shapes);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = SparkBlend;
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
        /// Builds the one static buffer. A spark's direction is baked in here rather than hashed in the shader
        /// because it must be the same every frame — the shader's whole trick is that a spark's position is a
        /// pure function of its age, and a direction that changed between frames would make it jitter.
        /// </summary>
        private void BuildBuffers(out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer)
        {
            FireworkVertex[] vertices = new FireworkVertex[_quadCount * 4];
            short[] indices = new short[_quadCount * 6];

            //A deterministic generator: the burst pattern is fixed for the life of the program, and the shells
            //are told apart by colour, place, size and shape instead. A per-shell random pattern would buy
            //nothing visible and would cost the static buffer.
            Random random = new(20260729);

            int v = 0, n = 0;
            for (int shell = 0; shell < MAX_SHELLS; shell++)
                for (int spark = 0; spark < SPARKS_PER_SHELL; spark++)
                {
                    //Fibonacci sphere: even coverage without the pole clustering a naive (angle, angle) pair
                    //gives, which would leave a firework denser at its top and bottom than around its waist.
                    float k = (spark + 0.5f) / SPARKS_PER_SHELL;
                    float y = 1f - 2f * k;
                    float r = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
                    float phi = spark * 2.39996323f;   //golden angle

                    Vector3 direction = new(MathF.Cos(phi) * r, y, MathF.Sin(phi) * r);

                    //And then JITTERED off it, which matters more than the even coverage did. A Fibonacci
                    //lattice is *too* regular: expanded uniformly it holds its pattern, and the eye picks the
                    //spiral out as a rigid structure inflating rather than as a thing coming apart — a bottle
                    //brush on a wire. A quarter-radian of scatter keeps the coverage and destroys the lattice.
                    direction += new Vector3(
                        (float)(random.NextDouble() * 2.0 - 1.0),
                        (float)(random.NextDouble() * 2.0 - 1.0),
                        (float)(random.NextDouble() * 2.0 - 1.0)) * 0.26f;
                    direction.Normalize();

                    //Speed spread, and a WIDE one. Bunched near the full radius the shell stays a surface: a
                    //clean hollow ball, which is a real firework but not one that reads as blowing apart.
                    //Spread from a third of the radius outwards it is a volume with a bright rim, and the
                    //sparks visibly separate from each other as it opens.
                    float speed = 0.34f + 0.66f * MathF.Pow((float)random.NextDouble(), 0.6f);

                    Vector4 sparkData = new(direction, speed);
                    Vector4 randomData = new(
                        (float)random.NextDouble(),                    //twinkle phase
                        (float)random.NextDouble(),                    //size jitter
                        (float)spark / SPARKS_PER_SHELL,               //trail rank on the way up
                        (float)random.NextDouble());                   //which of the shell's two colours

                    for (int corner = 0; corner < 4; corner++)
                    {
                        float cx = (corner == 0 || corner == 3) ? -1f : 1f;
                        float cy = (corner < 2) ? 1f : -1f;

                        vertices[v + corner] = new FireworkVertex
                        {
                            Slot = new Vector4(shell, k, cx, cy),
                            Spark = sparkData,
                            Random = randomData
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

            vertexBuffer = new VertexBuffer(_device, FireworkVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        //Rec. 709 luminance, the weights Glare.fx's bright pass decides by
        private static float Luminance(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;

        /// <summary>Each hue of <see cref="PALETTE"/> brought to <see cref="SPARK_LUMINANCE"/> (see there).</summary>
        private static Vector3[] BoostPalette()
        {
            Vector3[] boosted = new Vector3[PALETTE.Length];
            for (int i = 0; i < PALETTE.Length; i++)
                boosted[i] = PALETTE[i] * MathHelper.Clamp(SPARK_LUMINANCE / Luminance(PALETTE[i]), BOOST_MIN, BOOST_MAX);
            return boosted;
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }

        /// <summary>One corner of one spark's billboard. Everything the vertex shader needs to place it.</summary>
        private struct FireworkVertex : IVertexType
        {
            public Vector4 Slot;     //(shell, spark 0..1, corner x, corner y)
            public Vector4 Spark;    //(direction xyz, speed)
            public Vector4 Random;   //(twinkle phase, size jitter, trail rank, unused)

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }
    }
}
