using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The volcano's lava as figures rather than as a drawing (#795): the rivers' bearings and reaches, the vents, the
    /// eruption's clock and the lamps that ride the flows. <see cref="VolcanoBackdrop"/> draws from one of these, and
    /// the Raspberry Pi's Potato path - which builds no backdrop at all - keeps one of its own so the cluster takes the
    /// same flows' light (<see cref="SceneLights.Volcano"/>). Moved out of the backdrop whole: nothing in it touches the
    /// GPU, and the arithmetic, the seed and the order of the random draws are the backdrop's own, so the same config
    /// and seed solve the same volcano in both.
    /// </summary>
    public sealed class VolcanoLava
    {
        //Matched by MAX_RIVERS in Volcano.fx and MAX_VENTS in LavaFountain.fx. Both are shader array sizes:
        //raising either here without raising it there writes past what the shader reads.
        internal const int MAX_RIVERS = 6;
        internal const int MAX_VENTS = 4;

        private readonly VolcanoSceneConfig _volcanoConfig;

        //The rivers' bearings and reaches, solved once per config rather than per frame — the shader draws the
        //flows from these and the scene lights ride the same figures, which is what keeps a lamp on the river it is
        //lighting.
        private readonly float[] _riverBearing = new float[MAX_RIVERS];
        private readonly float[] _riverReach = new float[MAX_RIVERS];

        //The vents the fountains are thrown from: slot 0 the crater, the rest side vents on the flank.
        private readonly Vector3[] _ventPosition = new Vector3[MAX_VENTS];
        private readonly float[] _ventStrength = new float[MAX_VENTS];

        /// <summary>The rivers' bearings round the cone's axis, <see cref="RiverCount"/> of them used.</summary>
        public float[] RiverBearing => _riverBearing;

        /// <summary>How far down the flank each river runs from the cone's axis.</summary>
        public float[] RiverReach => _riverReach;

        /// <summary>How many of the river slots are rivers.</summary>
        public int RiverCount { get; }

        /// <summary>The vents: slot 0 the crater, then the side vents on the flank.</summary>
        public Vector3[] VentPosition => _ventPosition;

        /// <summary>Each vent's strength, the crater's 1.</summary>
        public float[] VentStrength => _ventStrength;

        /// <summary>How many of the vent slots are vents.</summary>
        public int VentCount { get; }

        /// <summary>
        /// Solves the rivers and the vents for <paramref name="volcano"/> on the scene seed's offset. Deterministic:
        /// same config, same seed, same volcano, every run and in every executable.
        /// </summary>
        public VolcanoLava(VolcanoSceneConfig volcano, int seedOffset)
        {
            _volcanoConfig = volcano;
            Random rng = new(4177 + seedOffset);

            //--- The rivers. Radial from the cone's axis, and the FIRST one is aimed to pass the arena: that
            //is the whole point of the scene's lighting, since a flow nobody stands beside lights nothing.
            //RiverArenaOffset walks it past the island's near edge rather than straight over it.
            RiverCount = Math.Clamp(volcano.RiverCount, 1, MAX_RIVERS);

            Vector2 cone = volcano.ConeCenter.ToVector2();
            float bearingToArena = MathF.Atan2(-cone.Y, -cone.X);
            float coneToArena = cone.Length();
            float spacing = MathHelper.TwoPi / RiverCount;
            float gullyCount = MathF.Round(MathF.Max(volcano.GullyCount, 1f));

            for (int i = 0; i < RiverCount; i++)
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

            //--- The vents. Three, and fixed in code rather than being another dial: the crater, and two side
            //vents part-way down the flank on two of the rivers — which is where a side vent is, since the
            //fissure that opens is what feeds the flow. Their strengths taper so the crater is plainly the
            //main event and the spatter cones read as spatter.
            VentCount = Math.Min(3, MAX_VENTS);

            _ventPosition[0] = new Vector3(cone.X, GroundHeight(cone.X, cone.Y) + 2f, cone.Y);
            _ventStrength[0] = 1f;

            for (int v = 1; v < VentCount; v++)
            {
                float bearing = _riverBearing[v % RiverCount];
                float radius = volcano.ConeRadius * (0.34f + 0.16f * v);
                float x = cone.X + MathF.Cos(bearing) * radius;
                float z = cone.Y + MathF.Sin(bearing) * radius;

                _ventPosition[v] = new Vector3(x, GroundHeight(x, z) + 1.5f, z);
                _ventStrength[v] = 0.42f - 0.10f * (v - 1);
            }
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
            float period = BurstSchedule(time, out float index, out float start, out float length, out float size);
            float u = time / period;

            float p = (u - index - start) / length;
            if (p <= 0f || p >= 1f) return 0f;

            float envelope = p < 0.14f ? p / 0.14f : MathF.Pow(1f - (p - 0.14f) / 0.86f, 1.7f);

            //Not every burst is the same size: a scene whose every event is identical stops being an event.
            return envelope * size;
        }

        /// <summary>
        /// The burst's SCHEDULE, in one place, for StormStrikeSchedule's reason exactly: the light and the boom have to
        /// be one event, and they are only one event while one function decides when it starts. Answers the period.
        /// </summary>
        internal float BurstSchedule(float time, out float index, out float start, out float length, out float size)
        {
            EruptionConfig eruption = _volcanoConfig.Eruption;

            float period = MathF.Max(eruption.Period, 1f);

            index = MathF.Floor(time / period);
            start = 0.10f + 0.55f * SceneRenderer.Hash01(index);
            length = Math.Clamp(eruption.Length / period, 0.02f, 0.85f);
            size = 0.55f + 0.45f * SceneRenderer.Hash01(index + 101f);

            return period;
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

            int river = index <= 2 ? 0 : (index - 2) % RiverCount;
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
                int river = index <= 2 ? 0 : (index - 2) % RiverCount;
                float near = MathF.Max(volcano.CraterRadius, 1f) * 1.2f;
                float span = MathF.Max(_riverReach[river] - near, 1f);
                float phase = ShaderMath.Frac(time * volcano.RiverSpeed / span + index * 0.37f);

                //Swells in and dies out over the run, so a front never appears or vanishes on the spot
                strength = MathF.Sin(MathF.PI * phase);
            }

            return volcano.LavaHot.ToVector3() * (volcano.LightStrength * strength * pulse);
        }
    }
}
