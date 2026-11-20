using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The wind turbine that powers the forest's lamps</b> (#1166, <see cref="ForestLampLayout"/>): one white three-bladed
    /// turbine on the wooded hills, far enough out to be scenery and tall enough that its rotor turns over the ridges, with a
    /// red aviation light on its nacelle. Where it stands, which way it faces, how its rotor is turned and when its light
    /// burns, as pure functions of the scene's clock: the backdrop draws it (<c>ForestTurbine</c>), the far stand clears a
    /// pad round its foot, the chapter intro's lenses keep off it, and the tests hold all three. Nothing here allocates.
    /// <para>
    /// <b>Where</b>: <see cref="DISTANCE"/> out on <see cref="BEARING"/>, inside the haze distance (<c>HorizonHazeDistance</c>,
    /// 520) so the far fade has not taken it on any tier, and to the left of the cluster in the opening play view (the lens on
    /// +Z looking towards -Z), above the treeline where the plate leaves the sky open.
    /// </para>
    /// </summary>
    public static class ForestTurbineLayout
    {
        /// <summary>The bearing it stands on, radians from +X towards +Z (as <c>OilRigLayout</c> counts).</summary>
        public const float BEARING = -2.07f;

        /// <summary>How far out from the arena's axis it stands.</summary>
        public const float DISTANCE = 446f;

        /// <summary>The tower's height from the ground to the nacelle's floor: about 65 m.</summary>
        public const float TOWER_HEIGHT = 130f;

        /// <summary>The tower's radius at its foot and at its top.</summary>
        public const float TOWER_RADIUS = 3.0f, TOWER_TOP_RADIUS = 1.6f;

        /// <summary>The rotor's radius, the hub to a blade's tip: about 20 m.</summary>
        public const float ROTOR_RADIUS = 40f;

        /// <summary>Where the hub's middle stands in the turbine's frame (its foot at the origin, the rotor facing -Z).</summary>
        public static readonly Vector3 HUB = new(0f, TOWER_HEIGHT + 2.2f, -5.6f);

        /// <summary>Where the aviation light stands in the turbine's frame: on the nacelle's roof, towards its tail.</summary>
        public static readonly Vector3 BEACON = new(0f, TOWER_HEIGHT + 5.3f, 3.4f);

        /// <summary>How fast the rotor turns, radians a second: about 14 turns a minute, a mid-sized turbine's.</summary>
        public const float ROTOR_SPEED = 1.45f;

        /// <summary>How deep the tower's foot is sunk into the hill: the ground under it is sampled at one point.</summary>
        public const float SINK = 3f;

        /// <summary>How much open ground the far stand leaves round the tower's foot (#962's stand plants none inside it).</summary>
        public const float PAD_RADIUS = 14f;

        /// <summary>The aviation light's pattern (seconds): on, off, on, off, the period of the "W-rot" flashing of a wind
        /// turbine's obstruction light, and how long it takes to come on and go off (an LED, but not a hard frame's step).</summary>
        public const float BEACON_ON = 1.0f, BEACON_GAP = 0.5f, BEACON_ON2 = 1.0f, BEACON_PERIOD = 4.0f, BEACON_RAMP = 0.06f;

        /// <summary>The aviation light's colour at full (linear radiance), past the glare threshold for its halo.</summary>
        public static readonly Vector3 BEACON_COLOUR = new(14f, 0.55f, 0.25f);

        /// <summary>Where the foot stands in the plan, x and z.</summary>
        public static Vector2 FootXZ => new(MathF.Cos(BEARING) * DISTANCE, MathF.Sin(BEARING) * DISTANCE);

        /// <summary>
        /// Which way it faces: the rotor (local -Z) into the wind, the scene's <c>Wind</c> (the direction it blows), so the nose
        /// points where the wind comes from. <c>CreateRotationY(yaw)</c> takes -Z to (-sin, 0, -cos).
        /// </summary>
        public static float Yaw(Vector2 wind)
        {
            Vector2 upwind = wind.LengthSquared() > 1e-8f ? -Vector2.Normalize(wind) : -Vector2.UnitY;
            return MathF.Atan2(-upwind.X, -upwind.Y);
        }

        /// <summary>The tower's frame in the world: its foot on <see cref="TerrainMirror.Forest"/> sunk <see cref="SINK"/>,
        /// turned by <see cref="Yaw"/>.</summary>
        public static Matrix World(ForestSceneConfig config)
        {
            Vector2 foot = FootXZ;
            float ground = config != null ? TerrainMirror.Forest(foot.X, foot.Y, config) : 0f;
            Vector2 wind = config != null ? new Vector2(config.Wind.X, config.Wind.Y) : Vector2.UnitX;
            return Matrix.CreateRotationY(Yaw(wind)) * Matrix.CreateTranslation(foot.X, ground - SINK, foot.Y);
        }

        /// <summary>The rotor's turn at <paramref name="time"/>, radians in [0, 2 pi): a pure function of the clock, so a paused
        /// game's rotor stands where the clock has it and two lenses on one clock see one rotor.</summary>
        public static float RotorAngle(float time)
        {
            float turns = time * ROTOR_SPEED / MathHelper.TwoPi;
            return (turns - MathF.Floor(turns)) * MathHelper.TwoPi;
        }

        /// <summary>The rotor's frame in the world at <paramref name="time"/>: its mesh (the hub at the origin, the blades in the
        /// XY plane) turned about its axis and set on the hub of <paramref name="tower"/>.</summary>
        public static Matrix RotorWorld(Matrix tower, float time) =>
            Matrix.CreateRotationZ(RotorAngle(time)) * Matrix.CreateTranslation(HUB) * tower;

        /// <summary>The aviation light at <paramref name="time"/>, 0 to 1: on for <see cref="BEACON_ON"/>, off for
        /// <see cref="BEACON_GAP"/>, on for <see cref="BEACON_ON2"/>, off to the end of <see cref="BEACON_PERIOD"/>, each edge a
        /// <see cref="BEACON_RAMP"/> ramp.</summary>
        public static float Beacon(float time)
        {
            float t = time - MathF.Floor(time / BEACON_PERIOD) * BEACON_PERIOD;
            float first = Pulse(t, 0f, BEACON_ON), second = Pulse(t, BEACON_ON + BEACON_GAP, BEACON_ON + BEACON_GAP + BEACON_ON2);
            return MathF.Max(first, second);
        }

        //1 between the two times, ramping up over BEACON_RAMP after the first and down over it before the second
        private static float Pulse(float t, float on, float off) =>
            MathHelper.Clamp(MathF.Min((t - on) / BEACON_RAMP, (off - t) / BEACON_RAMP), 0f, 1f);
    }
}
