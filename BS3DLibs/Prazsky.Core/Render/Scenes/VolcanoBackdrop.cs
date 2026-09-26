using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The volcano (#223, redrawn from references in #509): the flank of an erupting cone on a camera-centred grid,
    /// its crusted lava rivers and the lake in its crater, the fountains, the plume and the blaze over the crater, the
    /// drifting ash, the eruption on its own clock and the lamps that ride the flows. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580; the renderer's public <c>Volcano*</c> queries forward here, and its
    /// <c>TryGetSceneEvent</c> and <c>TryGetGroundGlow</c> ask the backdrop. Its terrain draw is its own and not a
    /// <see cref="TerrainPass"/>: it leaves the blend state as it found it where the pass states opaque and restores
    /// alpha. See "The volcano" in docs/scenes.md.
    /// </summary>
    internal sealed class VolcanoBackdrop : Backdrop
    {
        private VolcanoSceneConfig _volcanoConfig = new();

        private readonly GraphicsDevice _graphicsDevice;

        private readonly Effect _volcanoEffect;
        private VertexBuffer _volcanoVertexBuffer; //not readonly: a tier's crossing rebuilds it (EnsureVolcanoGrid, #540)
        private IndexBuffer _volcanoIndexBuffer;
        private int _volcanoIndexCount;

        //The mountain's density and extent: the flank carries a summit against the sky, so it wants the
        //craggy grid rather than the desert's, and it needs the same 32-bit index buffer (TerrainGridCache).
        private const int VOLCANO_GRID_N = 360;

        //The reduced program's grid (#540): half the vertices, a 4.7-unit cell against 3.3. Measured on the APU at Low,
        //360 -> 256 is 0.9 ms by itself; 256 -> 192 measured a further 0.3-0.55 and was not taken - the 6.3-unit
        //cell's cone was never photographed, and 256 already puts Caldera under the budget.
        private const int VOLCANO_GRID_N_REDUCED = 256;
        private const float VOLCANO_EXTENT = 1200f;

        //Matched by MAX_RIVERS in Volcano.fx and MAX_VENTS in LavaFountain.fx. Both are shader array sizes:
        //raising either here without raising it there writes past what the shader reads.
        private const int MAX_RIVERS = 6;
        private const int MAX_VENTS = 4;

        //Where a 16-bit index buffer runs out: four vertices a particle over 65 536 addressable ones. The
        //mountain's snow silently trusted a config to stay under a limit like this; a volcano's counts are
        //dials from day one (#209 defends a 75 FPS budget this scene spends particles against), so the cap
        //is stated rather than assumed.
        private const int MAX_BILLBOARD_PARTICLES = 16000;

        //The rivers' bearings and reaches, solved once per config (BuildVolcanoBuffers) rather than per
        //frame — the shader draws the flows from these and the scene lights ride the same figures, which is
        //what keeps a lamp on the river it is lighting.
        private readonly float[] _riverBearing = new float[MAX_RIVERS];
        private readonly float[] _riverReach = new float[MAX_RIVERS];
        private int _riverCount;

        //The vents the fountains are thrown from: slot 0 the crater, the rest side vents on the flank.
        private readonly Vector3[] _ventPosition = new Vector3[MAX_VENTS];
        private readonly float[] _ventStrength = new float[MAX_VENTS];
        private int _ventCount;

        //The lava fountains and the smoke plume: ONE static billboard buffer, its first PlumeFraction drawn
        //as the plume and the rest as the jets, so neither pass pays for the other's particles. Animated
        //entirely in the vertex shader, so it is rebuilt only when a config is applied and never per frame.
        private readonly Effect _fountainEffect;
        private VertexBuffer _fountainVertexBuffer;
        private IndexBuffer _fountainIndexBuffer;
        private int _plumeQuads, _jetQuads;

        //Cached at load: the by-name Techniques indexer is a linear scan and this pass selects between the
        //three of them every frame (BestPractices.md §1).
        private readonly EffectTechnique _plumeTechnique, _jetTechnique, _glowTechnique;

        //The drifting ash, on the snowfall's machinery in its own shader (Ash.fx says why it is not Snow.fx).
        private readonly Effect _ashEffect;
        private VertexBuffer _ashVertexBuffer;
        private IndexBuffer _ashIndexBuffer;

        //Look/tuning parameters live in VolcanoSceneConfig; the backdrop reads them from _volcanoConfig.

        /// <summary>
        /// The volcano's layers (#540), which <see cref="SceneRenderer.VolcanoLayers"/> forwards to: a measurement dial
        /// the Testbed sets and every other caller leaves at <see cref="VolcanoLayer.All"/>.
        /// </summary>
        public VolcanoLayer Layers { get; set; } = VolcanoLayer.All;

        /// <summary>
        /// Loads the three effects, takes the grid, pushes the config and solves the rivers, the vents and the
        /// particle buffers.
        /// </summary>
        public VolcanoBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Volcano (#223, redrawn from references in #509): the fifteenth scene — the flank of an erupting
            //cone, its crusted lava rivers, the rivulets down it and the lake in its crater. The mountain's grid
            //density, because this terrain carries a summit against the sky; the fountains, the plume and the ash
            //are billboard buffers over two more effects, all animated in their vertex shaders and rebuilt only
            //when a config is applied.
            _volcanoEffect = content.Load<Effect>("Shaders/Volcano");
            EnsureVolcanoGrid(VOLCANO_GRID_N);

            _fountainEffect = content.Load<Effect>("Shaders/LavaFountain");
            _plumeTechnique = _fountainEffect.Techniques["Plume"];
            _jetTechnique = _fountainEffect.Techniques["Fountain"];
            _glowTechnique = _fountainEffect.Techniques["Glow"];
            _ashEffect = content.Load<Effect>("Shaders/Ash");

            ApplyVolcanoParameters();
            BuildVolcanoBuffers();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Volcano;

        /// <inheritdoc/>
        public override SceneConfig Config => _volcanoConfig;

        /// <summary>
        /// Pushes the volcano's static tuning into <c>Volcano.fx</c>, <c>LavaFountain.fx</c> and <c>Ash.fx</c>.
        /// The rivers' bearings, the vents and the two particle buffers depend on the terrain, so they are
        /// <see cref="BuildVolcanoBuffers"/>'s and are solved after this.
        /// </summary>
        private void ApplyVolcanoParameters()
        {
            VolcanoSceneConfig volcano = _volcanoConfig;

            _volcanoEffect.Parameters["VolcanoLevelY"].SetValue(volcano.LevelY);
            _volcanoEffect.Parameters["ClearingRadius"].SetValue(volcano.ClearingRadius);
            _volcanoEffect.Parameters["ClearingTransition"].SetValue(MathF.Max(volcano.ClearingTransition, 1f));
            _volcanoEffect.Parameters["ConeCenterXZ"].SetValue(volcano.ConeCenter.ToVector2());
            _volcanoEffect.Parameters["ConeRadius"].SetValue(MathF.Max(volcano.ConeRadius, 1f));
            _volcanoEffect.Parameters["ConeHeight"].SetValue(volcano.ConeHeight);
            _volcanoEffect.Parameters["ConeProfile"].SetValue(MathF.Max(volcano.ConeProfile, 0.1f));
            _volcanoEffect.Parameters["CraterRadius"].SetValue(MathF.Max(volcano.CraterRadius, 1f));
            _volcanoEffect.Parameters["CraterDepth"].SetValue(volcano.CraterDepth);
            _volcanoEffect.Parameters["GullyDepth"].SetValue(volcano.GullyDepth);

            //ROUNDED, and the shader says why: every bearing term is a multiple of this figure, and only an
            //integer multiple of atan2's angle closes across the ±π seam. A fractional count leaves a straight
            //scar running from the crater to the horizon. Rounded here rather than in the shader because it is
            //a per-config decision, not a per-vertex one.
            _volcanoEffect.Parameters["GullyCount"].SetValue(MathF.Round(MathF.Max(volcano.GullyCount, 1f)));

            _volcanoEffect.Parameters["ScoriaRelief"].SetValue(volcano.ScoriaRelief);
            _volcanoEffect.Parameters["RiverWidth"].SetValue(MathF.Max(volcano.RiverWidth, 0.5f));
            _volcanoEffect.Parameters["RiverWander"].SetValue(volcano.RiverWander);
            _volcanoEffect.Parameters["RiverSpeed"].SetValue(volcano.RiverSpeed);
            _volcanoEffect.Parameters["HaloWidth"].SetValue(MathF.Max(volcano.HaloWidth, 1.05f));
            _volcanoEffect.Parameters["RockColor"].SetValue(volcano.RockColor.ToVector3());
            _volcanoEffect.Parameters["RockColorLight"].SetValue(volcano.RockColorLight.ToVector3());
            _volcanoEffect.Parameters["ScoriaColor"].SetValue(volcano.ScoriaColor.ToVector3());
            _volcanoEffect.Parameters["LavaHot"].SetValue(volcano.LavaHot.ToVector3());
            _volcanoEffect.Parameters["LavaCool"].SetValue(volcano.LavaCool.ToVector3());
            _volcanoEffect.Parameters["CrustColor"].SetValue(volcano.CrustColor.ToVector3());
            _volcanoEffect.Parameters["CrustGlow"].SetValue(volcano.CrustGlow);
            _volcanoEffect.Parameters["CrackGlow"].SetValue(volcano.CrackGlow);
            _volcanoEffect.Parameters["PlateSize"].SetValue(MathF.Max(volcano.PlateSize, 0.1f));
            _volcanoEffect.Parameters["SheenStrength"].SetValue(volcano.SheenStrength);
            _volcanoEffect.Parameters["RivuletStrength"].SetValue(volcano.RivuletStrength);
            _volcanoEffect.Parameters["FieldCrackStrength"].SetValue(volcano.FieldCrackStrength);
            _volcanoEffect.Parameters["AmbientStrength"].SetValue(volcano.AmbientStrength);
            _volcanoEffect.Parameters["HorizonHazeDistance"].SetValue(MathF.Max(volcano.HorizonHazeDistance, 1f));
            _volcanoEffect.Parameters["HazeTint"].SetValue(volcano.HazeTint.ToVector3());
            _volcanoEffect.Parameters["HazeStrength"].SetValue(volcano.HazeStrength);
            _volcanoEffect.Parameters["WindDirection"].SetValue(volcano.Wind.ToVector2());

            LavaFountainConfig fountains = volcano.Fountains;

            _fountainEffect.Parameters["LaunchSpeed"].SetValue(fountains.Speed);
            _fountainEffect.Parameters["LaunchSpread"].SetValue(fountains.Spread);
            _fountainEffect.Parameters["BlobGravity"].SetValue(MathF.Max(fountains.Gravity, 0.1f));
            _fountainEffect.Parameters["BlobLife"].SetValue(MathF.Max(fountains.Life, 0.1f));
            _fountainEffect.Parameters["BlobSize"].SetValue(fountains.BlobSize);
            _fountainEffect.Parameters["WindDirection"].SetValue(volcano.Wind.ToVector2());
            _fountainEffect.Parameters["WindDrag"].SetValue(fountains.WindDrag);
            _fountainEffect.Parameters["StreakTime"].SetValue(MathF.Max(fountains.StreakTime, 0f));
            _fountainEffect.Parameters["EruptionBoost"].SetValue(volcano.Eruption.Boost);
            _fountainEffect.Parameters["LavaHot"].SetValue(volcano.LavaHot.ToVector3());
            _fountainEffect.Parameters["LavaCool"].SetValue(volcano.LavaCool.ToVector3());
            _fountainEffect.Parameters["PlumeColor"].SetValue(fountains.PlumeColor.ToVector3());
            _fountainEffect.Parameters["PlumeStrength"].SetValue(fountains.PlumeStrength);
            _fountainEffect.Parameters["PlumeGlow"].SetValue(fountains.PlumeGlow);

            //The plume's own figures are derived from the jets' rather than being four more dials: a column
            //rises about a third as fast as a blob is thrown, lives long enough to leave the frame, and its
            //stem is about as wide as the crater it stands in (0.8 of it until #509, which with the head the
            //shader now opens was still a tube next to the references' columns). Deriving them keeps a retuned
            //fountain and its own smoke in proportion, which is what a designer moving Speed actually wants.
            _fountainEffect.Parameters["PlumeRise"].SetValue(fountains.Speed * 0.55f);
            _fountainEffect.Parameters["PlumeSpread"].SetValue(volcano.CraterRadius * 1.1f);
            _fountainEffect.Parameters["PlumeLife"].SetValue(fountains.Life * 7f);
            _fountainEffect.Parameters["PlumeSize"].SetValue(fountains.BlobSize * 5f);

            //The blaze over the crater is sized off the crater it stands in, for the same reason.
            _fountainEffect.Parameters["GlowSize"].SetValue(volcano.CraterRadius * 1.6f);
            _fountainEffect.Parameters["GlowStrength"].SetValue(MathF.Max(fountains.GlowStrength, 0f));

            AshConfig ash = volcano.Ash;

            _ashEffect.Parameters["AshBoxSize"].SetValue(ash.BoxSize.ToVector3());
            _ashEffect.Parameters["AshFallSpeed"].SetValue(ash.FallSpeed);
            _ashEffect.Parameters["AshWind"].SetValue(ash.Wind.ToVector2());
            _ashEffect.Parameters["AshSway"].SetValue(ash.Sway);
            _ashEffect.Parameters["SpeckSize"].SetValue(ash.FlakeSize);
            _ashEffect.Parameters["AshSpin"].SetValue(ash.Spin);
            _ashEffect.Parameters["AshNearFade"].SetValue(MathF.Max(ash.NearFade, 0.1f));
            _ashEffect.Parameters["EmberFraction"].SetValue(ash.EmberFraction);
            _ashEffect.Parameters["AshColor"].SetValue(ash.AshColor.ToVector3());
            _ashEffect.Parameters["EmberColor"].SetValue(ash.EmberColor.ToVector3());
            _ashEffect.Parameters["AshOpacity"].SetValue(ash.Opacity);
        }

        /// <summary>
        /// (Re)solves everything about the volcano that depends on its terrain — the rivers' bearings and
        /// reaches, the vents the fountains are thrown from — and rebuilds the two particle buffers at the
        /// config's counts. Deterministic: same config, same volcano, every run and in every executable.
        /// </summary>
        private void BuildVolcanoBuffers()
        {
            VolcanoSceneConfig volcano = _volcanoConfig;
            Random rng = new(4177 + Services.SeedOffset);

            //--- The rivers. Radial from the cone's axis, and the FIRST one is aimed to pass the arena: that
            //is the whole point of the scene's lighting, since a flow nobody stands beside lights nothing.
            //RiverArenaOffset walks it past the island's near edge rather than straight over it.
            _riverCount = Math.Clamp(volcano.RiverCount, 1, MAX_RIVERS);

            Vector2 cone = volcano.ConeCenter.ToVector2();
            float bearingToArena = MathF.Atan2(-cone.Y, -cone.X);
            float coneToArena = cone.Length();
            float spacing = MathHelper.TwoPi / _riverCount;
            float gullyCount = MathF.Round(MathF.Max(volcano.GullyCount, 1f));

            for (int i = 0; i < _riverCount; i++)
            {
                //Evenly spread and then jittered by up to a quarter of the spacing, so the flank is not a
                //starburst — and never enough to let one river swap sides with its neighbour.
                float jitter = (float)(rng.NextDouble() - 0.5) * spacing * 0.5f;
                float wanted = bearingToArena + volcano.RiverArenaOffset + i * spacing + (i == 0 ? 0f : jitter);

                //And then SNAPPED to the nearest gully, which is the whole difference between lava lying on
                //a cone and lava running down one: water — and rock — go where the ground drains, and the
                //gullies are where this ground drains. Without it the flows crossed the channels obliquely
                //and read as paint.
                _riverBearing[i] = SnapToGully(wanted, gullyCount);

                //River 0 has to get past the arena to be worth aiming there; the others stop somewhere on the
                //flank, each at its own reach, so the fronts are not one ring around the cone.
                _riverReach[i] = i == 0
                    ? coneToArena + 90f
                    : volcano.ConeRadius * (0.70f + 0.55f * (float)rng.NextDouble());
            }

            //The snap can walk river 0 by up to half a gully, and half a gully at this distance is tens of
            //units — enough to put the flow under the island instead of past it. If it lands too close, take
            //the next gully out on the far side. The clearance wanted is the island plus a couple of river
            //widths, so the flow passes beside the play field with dark ground between.
            float clearance = ArenaIsland.RADIUS + volcano.RiverWidth * 2f;
            float perpendicular = MathF.Abs(MathF.Sin(_riverBearing[0] - bearingToArena)) * coneToArena;
            if (perpendicular < clearance)
            {
                float away = MathF.Sign(volcano.RiverArenaOffset == 0f ? 1f : volcano.RiverArenaOffset);
                _riverBearing[0] = SnapToGully(bearingToArena + away * MathHelper.TwoPi * 1.5f / gullyCount, gullyCount);
            }

            _volcanoEffect.Parameters["RiverBearing"].SetValue(_riverBearing);
            _volcanoEffect.Parameters["RiverReach"].SetValue(_riverReach);
            _volcanoEffect.Parameters["RiverCount"].SetValue(_riverCount);

            //--- The vents. Three, and fixed in code rather than being another dial: the crater, and two side
            //vents part-way down the flank on two of the rivers — which is where a side vent is, since the
            //fissure that opens is what feeds the flow. Their strengths taper so the crater is plainly the
            //main event and the spatter cones read as spatter.
            _ventCount = Math.Min(3, MAX_VENTS);

            _ventPosition[0] = new Vector3(cone.X, GroundHeight(cone.X, cone.Y) + 2f, cone.Y);
            _ventStrength[0] = 1f;

            for (int v = 1; v < _ventCount; v++)
            {
                float bearing = _riverBearing[v % _riverCount];
                float radius = volcano.ConeRadius * (0.34f + 0.16f * v);
                float x = cone.X + MathF.Cos(bearing) * radius;
                float z = cone.Y + MathF.Sin(bearing) * radius;

                _ventPosition[v] = new Vector3(x, GroundHeight(x, z) + 1.5f, z);
                _ventStrength[v] = 0.42f - 0.10f * (v - 1);
            }

            _fountainEffect.Parameters["VentPosition"].SetValue(_ventPosition);
            _fountainEffect.Parameters["VentStrength"].SetValue(_ventStrength);
            _fountainEffect.Parameters["VentCount"].SetValue(_ventCount);

            //--- The particles. One buffer for the fountains: its first slice is the plume and the rest are
            //the jets, drawn as two index ranges over the one buffer (see DrawLavaFountains) so neither pass
            //pays for the other's particles. Both counts are capped where a 16-bit index buffer runs out.
            int total = Math.Clamp(volcano.Fountains.ParticleCount, 0, MAX_BILLBOARD_PARTICLES);
            _plumeQuads = (int)(total * Math.Clamp(volcano.Fountains.PlumeFraction, 0f, 0.9f));
            _jetQuads = total - _plumeQuads;

            Services.BuildBillboardParticles(total, 8831, ref _fountainVertexBuffer, ref _fountainIndexBuffer);
            Services.BuildBillboardParticles(Math.Clamp(volcano.Ash.FlakeCount, 0, MAX_BILLBOARD_PARTICLES), 6491,
                ref _ashVertexBuffer, ref _ashIndexBuffer);
        }

        /// <summary>
        /// The bearing of the gully floor nearest <paramref name="bearing"/>, so a river can be laid in one.
        /// <para>
        /// A gully is deepest where <c>Volcano.fx</c>'s rake term peaks, i.e. where
        /// <c>b·N + 2·sin(3b) ≡ π (mod 2π)</c>. There is no closed form for that, and none is needed: the
        /// <c>2·sin(3b)</c> bend is small against <c>N</c>, so picking the branch nearest the wanted bearing
        /// and iterating <c>b ← (target − 2·sin(3b)) / N</c> is a contraction with ratio <c>6/N</c> and four
        /// passes land far inside a degree. Change the rake term in the shader and this has to change with it.
        /// </para>
        /// </summary>
        private static float SnapToGully(float bearing, float gullyCount)
        {
            float branch = MathF.Round((bearing * gullyCount + 2f * MathF.Sin(bearing * 3f) - MathF.PI) / MathHelper.TwoPi);
            float target = MathF.PI + branch * MathHelper.TwoPi;

            float b = bearing;
            for (int pass = 0; pass < 4; pass++) b = (target - 2f * MathF.Sin(b * 3f)) / gullyCount;

            return b;
        }

        /// <summary><see cref="SceneRenderer.VolcanoGroundHeight"/>.</summary>
        public float GroundHeight(float x, float z) => TerrainMirror.Volcano(x, z, _volcanoConfig);

        /// <summary><see cref="SceneRenderer.VolcanoEruption"/>.</summary>
        public float Eruption(float time)
        {
            float period = VolcanoBurstSchedule(time, out float index, out float start, out float length, out float size);
            float u = time / period;

            float p = (u - index - start) / length;
            if (p <= 0f || p >= 1f) return 0f;

            float envelope = p < 0.14f ? p / 0.14f : MathF.Pow(1f - (p - 0.14f) / 0.86f, 1.7f);

            //Not every burst is the same size: a scene whose every event is identical stops being an event.
            return envelope * size;
        }

        //The burst's SCHEDULE, in one place, for StormStrikeSchedule's reason exactly: the light and the boom
        //have to be one event, and they are only one event while one function decides when it starts.
        private float VolcanoBurstSchedule(float time, out float index, out float start, out float length, out float size)
        {
            EruptionConfig eruption = _volcanoConfig.Eruption;

            float period = MathF.Max(eruption.Period, 1f);

            index = MathF.Floor(time / period);
            start = 0.10f + 0.55f * SceneRenderer.Hash01(index);
            length = Math.Clamp(eruption.Length / period, 0.02f, 0.85f);
            size = 0.55f + 0.45f * SceneRenderer.Hash01(index + 101f);

            return period;
        }

        /// <inheritdoc/>
        public override bool TryGetSceneEvent(float time, out SceneEvent staged)
        {
            float period = VolcanoBurstSchedule(time, out float index, out float start, out float _, out float size);

            //Slot 0 is the crater and does not move, so the time is not read for it - see
            //VolcanoLightPosition. The boom comes from the crater and not from the flows: a river
            //front is silent, and the plume is what is heard.
            staged = new SceneEvent((int)index, (index + start) * period, LightPosition(0, time), size);
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetGroundGlow(float time, out Vector3 position, out Vector3 color, out float range)
        {
            VolcanoSceneConfig volcano = _volcanoConfig;
            if (volcano.DeckGlow <= 0f)
            {
                position = default;
                color = default;
                range = 1f;
                return false;
            }

            position = _ventPosition[0];
            //A shade towards the hot end of the lava's range: the cool end alone lit the deck blood-red, where
            //the references' clouds over a crater are orange.
            Vector3 lava = Vector3.Lerp(volcano.LavaCool.ToVector3(), volcano.LavaHot.ToVector3(), 0.15f);
            color = lava * volcano.DeckGlow * (0.55f + 0.9f * Eruption(time));
            range = volcano.DeckGlowRange;
            return true;
        }

        /// <summary><see cref="SceneRenderer.VolcanoLightCount"/>.</summary>
        public int LightCount => Math.Clamp(_volcanoConfig.LightCount, 1, SceneLights.MaxLights);

        /// <summary><see cref="SceneRenderer.VolcanoLightRange"/>.</summary>
        public float LightRange => _volcanoConfig.LightRange;

        /// <summary><see cref="SceneRenderer.VolcanoLightPosition"/>.</summary>
        public Vector3 LightPosition(int index, float time)
        {
            if (index <= 0) return _ventPosition[0];

            VolcanoSceneConfig volcano = _volcanoConfig;
            Vector2 cone = volcano.ConeCenter.ToVector2();

            int river = index <= 2 ? 0 : (index - 2) % _riverCount;
            float near = MathF.Max(volcano.CraterRadius, 1f) * 1.2f;
            float span = MathF.Max(_riverReach[river] - near, 1f);

            float phase = ShaderMath.Frac(time * volcano.RiverSpeed / span + index * 0.37f);
            float r = near + phase * span;

            float wander = volcano.RiverWander * MathF.Sin(r * 0.017f + river * 2.13f)
                * Math.Clamp(r / MathF.Max(volcano.ConeRadius, 1f), 0f, 1f);
            float bearing = _riverBearing[river] + wander;

            float x = cone.X + MathF.Cos(bearing) * r;
            float z = cone.Y + MathF.Sin(bearing) * r;

            //A little over the surface: a lamp buried in the ground it is lighting throws nothing sideways,
            //and the flow it stands for is a metre of molten rock lying on top of the flank, not inside it.
            return new Vector3(x, GroundHeight(x, z) + 2.5f, z);
        }

        /// <summary><see cref="SceneRenderer.VolcanoLightColor"/>.</summary>
        public Vector3 LightColor(float time, int index)
        {
            VolcanoSceneConfig volcano = _volcanoConfig;

            //Lava pulses where a fire flickers — slower rates than the campfire's, and each lamp on its own
            //stride so the flank does not breathe in unison.
            float t = time + index * 3.77f;
            float rate = 1f + index * 0.037f;
            float pulse = 0.82f + 0.18f * (0.5f * MathF.Sin(t * 3.1f * rate) + 0.3f * MathF.Sin(t * 5.3f * rate + 1.3f)
                + 0.2f * MathF.Sin(t * 2.1f * rate));

            float strength;
            if (index <= 0)
            {
                strength = 0.75f + Eruption(time) * volcano.Eruption.LightBoost;
            }
            else
            {
                int river = index <= 2 ? 0 : (index - 2) % _riverCount;
                float near = MathF.Max(volcano.CraterRadius, 1f) * 1.2f;
                float span = MathF.Max(_riverReach[river] - near, 1f);
                float phase = ShaderMath.Frac(time * volcano.RiverSpeed / span + index * 0.37f);

                //Swells in and dies out over the run, so a front never appears or vanishes on the spot
                strength = MathF.Sin(MathF.PI * phase);
            }

            return volcano.LavaHot.ToVector3() * (volcano.LightStrength * strength * pulse);
        }

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            //The volcano (#509): the two kinds of hairline the references brought in off the flows - the
            //rivulets down the cone and the cracks in the field - arrived together and are given up together.
            //The flows, the crater's lake and the sheen stay on every tier; they are what the scene is.
            //Since #540 the reduced program is also a coarser grid under it: most of what the vertex program costs is
            //paid per vertex whatever the pixels, and the reduced program's scoria has no octave the finer grid resolves.
            _volcanoEffect.CurrentTechnique = _volcanoEffect.Techniques[sceneDetail > 0.5f ? "Volcano" : "VolcanoReduced"];
            EnsureVolcanoGrid(sceneDetail > 0.5f ? VOLCANO_GRID_N : VOLCANO_GRID_N_REDUCED);
        }

        /// <summary>
        /// (Re)builds the volcano's grid at <paramref name="n"/> vertices a side when it is not that already — once at
        /// load and again only when the tier crosses <see cref="SceneRenderer.SceneDetail"/>'s line, so never per frame.
        /// </summary>
        private void EnsureVolcanoGrid(int n)
        {
            if (_volcanoVertexBuffer != null && _volcanoGridN == n) return;

            //Given back rather than disposed: the grid cache owns it, and at full detail another scene draws the same one
            if (_volcanoVertexBuffer != null) Services.ReleaseGridMesh(_volcanoGridN, VOLCANO_EXTENT);
            Services.AcquireGridMesh(n, VOLCANO_EXTENT, out _volcanoVertexBuffer, out _volcanoIndexBuffer, out _volcanoIndexCount);
            _volcanoGridN = n;
        }

        private int _volcanoGridN;

        /// <summary>The flank, then the fountains, the plume and the blaze over it.</summary>
        public override void Draw(in SceneFrame frame)
        {
            //The flank first (it writes depth), then the fountains and the plume over it — they are
            //part of the far scene rather than foreground weather, because the cluster hangs in front
            //of the cone and has to occlude it. Only the ash is an overlay.
            if ((Layers & VolcanoLayer.Terrain) != 0) DrawVolcanoTerrain(frame);
            DrawLavaFountains(frame);
        }

        /// <summary>The drifting ash, unless the layers dial has taken it off.</summary>
        public override void DrawOverlays(in SceneFrame frame)
        {
            if ((Layers & VolcanoLayer.Ash) != 0) DrawAsh(frame);
        }

        /// <summary>
        /// Draws the volcano's flank (#223): the grid pinned to the camera and snapped to a cell so the ground
        /// does not swim, displaced into the cone and its gullies, with the lava rivers drawn as an emissive
        /// band on it and the crust cracking glowing between them. Opaque and depth-writing, drawn first in
        /// the scene block, <see cref="RasterizerState.CullNone"/> (the winding is moot on a heightfield).
        /// </summary>
        private void DrawVolcanoTerrain(in SceneFrame frame)
        {
            float cell = VOLCANO_EXTENT / (_volcanoGridN - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _volcanoEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _volcanoEffect.Parameters["IslandHoleRadius"].SetValue(Services.TerrainHoleRadius);
            _volcanoEffect.Parameters["View"].SetValue(frame.Camera.View);
            _volcanoEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _volcanoEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _volcanoEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _volcanoEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _volcanoEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _volcanoEffect.Parameters["SunColor"].SetValue(frame.SunColor);
            _volcanoEffect.Parameters["VolcanoTime"].SetValue(frame.Time);

            frame.ApplyClouds?.Invoke(_volcanoEffect);

            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            Services.FarField.Begin(_volcanoEffect, frame, VOLCANO_EXTENT);
            _graphicsDevice.SetVertexBuffer(_volcanoVertexBuffer);
            _graphicsDevice.Indices = _volcanoIndexBuffer;
            _volcanoEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _volcanoIndexCount / 3);
            Services.FarField.DrawRing(_volcanoEffect, new Vector2(originX, originZ), VOLCANO_EXTENT);

            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the lava fountains and the smoke plume standing over the crater — two index ranges over the
        /// <b>one</b> particle buffer, so the plume pass never touches a jet's vertex and the jet pass never
        /// touches a puff's. Both depth-read (the flank and the cluster hide what is behind them) and writing
        /// no depth; the plume alpha-blended first, the jets additively over it, which is the order those two
        /// have to be drawn in for smoke to sit behind fire rather than over it.
        /// <para>
        /// The eruption envelope goes in here rather than being computed per particle: one figure off the wall
        /// clock (<see cref="SceneRenderer.VolcanoEruption"/>) drives the jets' reach, the plume's density and the crater's
        /// point light together, which is what makes a burst read as one event.
        /// </para>
        /// </summary>
        private void DrawLavaFountains(in SceneFrame frame)
        {
            if (_fountainVertexBuffer == null) return;

            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _fountainEffect.Parameters["View"].SetValue(frame.Camera.View);
            _fountainEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _fountainEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _fountainEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _fountainEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _fountainEffect.Parameters["FountainTime"].SetValue(frame.Time);
            _fountainEffect.Parameters["Eruption"].SetValue(Eruption(frame.Time));

            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SetVertexBuffer(_fountainVertexBuffer);
            _graphicsDevice.Indices = _fountainIndexBuffer;

            if (_plumeQuads > 0 && (Layers & VolcanoLayer.Plume) != 0)
            {
                _graphicsDevice.BlendState = BlendState.AlphaBlend;
                _fountainEffect.CurrentTechnique = _plumeTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _plumeQuads * 2);
            }

            if (_jetQuads > 0 && (Layers & VolcanoLayer.Jets) != 0)
            {
                _graphicsDevice.BlendState = BlendState.Additive;
                _fountainEffect.CurrentTechnique = _jetTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, _plumeQuads * 6, _jetQuads * 2);
            }

            //The blaze over the crater (#509): the buffer's first quad, whose corners are all the glow's vertex
            //shader reads - one quad, additive like the jets, so its order against them does not matter.
            if (_volcanoConfig.Fountains.GlowStrength > 0f && (Layers & VolcanoLayer.Glow) != 0)
            {
                _graphicsDevice.BlendState = BlendState.Additive;
                _fountainEffect.CurrentTechnique = _glowTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the drifting ash: a boxful of specks around the camera, animated entirely in the vertex
        /// shader. Alpha-blended and depth-read but writing no depth, drawn last with the overlays. Volcano
        /// scene only.
        /// </summary>
        private void DrawAsh(in SceneFrame frame)
        {
            if (_ashVertexBuffer == null) return;

            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _ashEffect.Parameters["View"].SetValue(frame.Camera.View);
            _ashEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _ashEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _ashEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _ashEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _ashEffect.Parameters["AshTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_ashVertexBuffer);
            _graphicsDevice.Indices = _ashIndexBuffer;
            _ashEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0,
                Math.Clamp(_volcanoConfig.Ash.FlakeCount, 0, MAX_BILLBOARD_PARTICLES) * 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The crater, read off the scene's own solved vent rather than off the cone's centre: slot 0
            //is the crater, it does not move, and it is where the fountain and the plume come from. This
            //is the case the whole table exists for — the old rolled bearing put the cone behind the
            //camera about as often as in front of it.
            viewpoint = new SceneViewpoint(LightPosition(0, 0f), 2.4f, 14f, 160f, "the crater");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get { yield return _volcanoEffect; }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            //Same argument as the mountain: the cone is 140 units of far-away silhouette and the flank
            //under the island is what the map covers.
            groundY = _volcanoConfig.LevelY;
            below = _volcanoConfig.ConeHeight * 0.15f;
            above = _volcanoConfig.ConeHeight * 0.3f;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _volcanoEffect;
            mirror = (x, z) => TerrainMirror.Volcano(x, z, _volcanoConfig);
            return true;
        }

        /// <summary>Frees the particle buffers; the grid is the cache's and the effects the content manager's.</summary>
        public override void Dispose()
        {
            _fountainVertexBuffer?.Dispose();
            _fountainIndexBuffer?.Dispose();
            _ashVertexBuffer?.Dispose();
            _ashIndexBuffer?.Dispose();
        }
    }
}
