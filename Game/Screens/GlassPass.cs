using Microsoft.Xna.Framework;
using System;

namespace BS3D.Screens
{
    /// <summary>What the front end does about its pass over the glass, from the <c>menuglass=</c> argument (#1214).</summary>
    internal enum MenuGlassMode
    {
        /// <summary>Now and then, wherever the tier bends what is behind the pane: what a player gets.</summary>
        Normal,

        /// <summary>Never: the menu as it was before #1214, for a before/after pair.</summary>
        Off,

        /// <summary>Every cycle's excursion is the pass over the glass, whatever the tier draws: the way to photograph one.</summary>
        Pass,
    }

    /// <summary>
    /// The geometry of the front end's pass over the glass (#1214): where the lens stands over the plate and what it looks at,
    /// how high it is at each moment of the excursion, and how that pose is mixed with the wide turn's. No device and no
    /// state, so the rules it must keep - never inside the plate or the cluster, never near a cable, never looking exactly
    /// straight down - are held by tests (<c>GlassPassTests</c>) on every shipped level and not by a reader's care.
    /// <para>
    /// <b>The one leg of the menu's flight that looks down.</b> The cluster's pass only ever looks up at the map (#599, the
    /// owner's ruling against a pass that "started too high and looked down on the map"); on 2026-11-20 the owner asked for
    /// this leg in so many words, a deliberate view of the map from above, nearly perpendicular and from a distance, closing
    /// in on the glass until the cut and what it bends are what the eye follows. The crane of the cluster's pass is untouched.
    /// </para>
    /// <para>
    /// <b>Over the plate the lens stands inside the four cables</b> (<c>CeilingRigging</c>: one at each corner, straight up
    /// to the roof or for <c>CeilingRigging.CABLE_RISE</c>), on a small circle about the island's axis, and looks
    /// <see cref="TILT"/> off straight down at the plate's top face. Coming in from the wide turn and going back out to it, it
    /// has to pass between two cables, so it does so through the <b>middle of a side</b>: the pass arrives over the middle of
    /// the side nearest the lens (<see cref="FirstBearing"/>), turns a quarter of the way round while it descends
    /// (<see cref="TURN"/>), and leaves over the middle of the next side, where the nearest cable is half a side away.
    /// </para>
    /// </summary>
    internal static class GlassPass
    {
        /// <summary>
        /// How far off straight down the lens looks over the plate. "Almost straight down" is the ask, and a little tilt
        /// is also what keeps the frame's orientation defined: the camera takes its right-hand side from the look crossed
        /// with the world's up (<c>RecoilCamera.Recalculate</c>), which has no answer for an exactly vertical look and snaps
        /// to a fixed axis there. Tilted towards the axis from the lens's side, the top of the frame is the far side of the
        /// plate, the same way round as the wide turn's, so coming over the top is a tip of the camera and not a roll.
        /// </summary>
        public static readonly float TILT = MathHelper.ToRadians(12f);

        /// <summary>
        /// How much horizontal air the lens keeps off a cable's axis while it stands over the plate. A cable is a ninth of a
        /// ball across, so a few units off the lens it is a bar across the frame; at this it is a line from the plate's
        /// corner to the frame's edge.
        /// </summary>
        public const float CABLE_AIR = 2.5f;

        /// <summary>
        /// How much of the frame's narrower half-angle the plate's footprint (its circumradius) fills from the far height:
        /// a little over half, so the far view is the map from above with its glass and the island round it, and the
        /// descent has somewhere to go. 30 units over a twelve-wide field at 16:9, 48 over a twenty-wide one.
        /// </summary>
        public const float FAR_SHARE = 0.55f;

        /// <summary>
        /// How much of the close leg the descent takes; the rest is spent at the near height, still turning, where the cut
        /// and what it bends fill the frame.
        /// </summary>
        public const float DESCENT_SHARE = 0.7f;

        /// <summary>How far round the axis the lens turns across the close leg: a quarter, from the middle of one side to the next.</summary>
        public static readonly float TURN = MathHelper.PiOver2;

        /// <summary>
        /// How far under a roof (the cavern's vault, the Big Top's canvas; <c>SceneRenderer.RoofAbove</c>) the lens stays:
        /// the far height is capped there.
        /// </summary>
        public const float ROOF_AIR = 8f;

        /// <summary>
        /// How far over a plate's top face a lens has to stand to be clear of everything hung from it (<see cref="ClearAbove"/>):
        /// over the winches at its corners, which stand a little short of a unit on the glass.
        /// </summary>
        public const float HANG_ABOVE = 1.5f;

        /// <summary>And how far off each cable's axis, horizontally, for <see cref="ClearAbove"/>.</summary>
        public const float HANG_CABLE_AIR = 1f;

        /// <summary>
        /// Where in an excursion the camera is, as one reversible scalar: 0 out on the wide turn, 1 at the pass. Smoothstepped
        /// at both ends, so the flight leaves the wide leg at rest and arrives at rest. <paramref name="since"/> is the time
        /// since the wide leg ended. Every excursion's shape, the cluster's and the gun's as well (<c>BackdropScreen.Closeness</c>).
        /// </summary>
        public static float Closeness(float since, float approach, float close, float retreat)
        {
            if (since <= 0f) return 0f;
            if (since < approach) return MathHelper.SmoothStep(0f, 1f, since / approach);

            since -= approach;
            if (since < close) return 1f;

            return MathHelper.SmoothStep(1f, 0f, MathHelper.Clamp((since - close) / retreat, 0f, 1f));
        }

        /// <summary>
        /// How far down towards the glass the pass is: 0 at the far height, through the approach, 1 at the near height. The
        /// descent takes <see cref="DESCENT_SHARE"/> of the close leg and arrives at rest; the retreat climbs on the very
        /// curve <see cref="Closeness"/> falls on, so the lens rises as it pulls out and passes over the plate's rim high.
        /// </summary>
        public static float Depth(float since, float approach, float close, float retreat)
        {
            if (since <= approach) return 0f;

            since -= approach;
            if (since < close) return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(since / (close * DESCENT_SHARE), 0f, 1f));

            return MathHelper.SmoothStep(1f, 0f, MathHelper.Clamp((since - close) / retreat, 0f, 1f));
        }

        /// <summary>How far through its quarter turn the pass is (<see cref="TURN"/>): across the close leg, at rest at both ends.</summary>
        public static float Turn(float since, float approach, float close) =>
            MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((since - approach) / close, 0f, 1f));

        /// <summary>
        /// The lens's height over the plate's top face at <paramref name="depth"/>: geometric between the two heights rather
        /// than linear, so the glass grows in the frame at a steady rate all the way down instead of rushing at the end.
        /// </summary>
        public static float HeightAt(float far, float near, float depth) => far * MathF.Pow(near / far, depth);

        /// <summary>
        /// The far height over the plate's top face: <paramref name="plateReach"/> (the footprint's circumradius) framed to
        /// <see cref="FAR_SHARE"/> of <paramref name="narrowHalfAngle"/>, so the plate is whole at any bearing and any window shape.
        /// </summary>
        public static float FarHeight(float plateReach, float narrowHalfAngle) =>
            plateReach / MathF.Sin(narrowHalfAngle * FAR_SHARE);

        /// <summary>
        /// The bearing the pass arrives over: the middle of the plate's side nearest <paramref name="bearing"/>. The plate is
        /// never turned, so its sides face the axes and their middles are the multiples of a right angle.
        /// </summary>
        public static float FirstBearing(float bearing) =>
            MathF.Round(bearing / MathHelper.PiOver2) * MathHelper.PiOver2;

        /// <summary>
        /// The lens over the plate and its aim, <paramref name="height"/> over the top face at <paramref name="plateTopY"/>,
        /// on the side of <paramref name="bearing"/>. The lens stands out along the bearing far enough to look
        /// <see cref="TILT"/> down at the plate's centre - or, where that would bring it within <see cref="CABLE_AIR"/> of a
        /// cable (<paramref name="cableReach"/>, how far the cables stand off the axis), no further out than that, and the aim
        /// moves past the centre by the rest so the tilt is the same.
        /// </summary>
        public static void Pose(float plateTopY, float cableReach, float height, float bearing, out Vector3 lens, out Vector3 target)
        {
            Vector3 outward = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));

            float reach = height * MathF.Tan(TILT);
            float offset = MathF.Min(reach, MathF.Max(cableReach - CABLE_AIR, 0f));

            lens = outward * offset + Vector3.Up * (plateTopY + height);
            target = -outward * (reach - offset) + Vector3.Up * plateTopY;
        }

        /// <summary>
        /// Mixes the wide pose (<paramref name="position"/>, <paramref name="target"/>, <paramref name="fieldOfView"/>, the lens
        /// at bearing <paramref name="angle"/>) with the pass's (<paramref name="lens"/>, <paramref name="aim"/>,
        /// <paramref name="fov"/>) by <paramref name="closeness"/>. <b>Polar about the island's axis</b>, as the gun's pass is:
        /// the reach, the bearing and the height each linearly, the bearing by <paramref name="swing"/> (the pass's bearing
        /// less the wide turn's, carried unwrapped so it never jumps the other way round). A straight line from the wide
        /// stand-off to the lens over the plate would be no different here, but the bearing has to be steered between the
        /// cables, and a polar blend is what steers it.
        /// </summary>
        public static void Blend(float closeness, float angle, float swing, Vector3 lens, Vector3 aim, float fov,
            ref Vector3 position, ref Vector3 target, ref float fieldOfView)
        {
            float wideReach = MathF.Sqrt(position.X * position.X + position.Z * position.Z);
            float glassReach = MathF.Sqrt(lens.X * lens.X + lens.Z * lens.Z);
            float reach = MathHelper.Lerp(wideReach, glassReach, closeness);
            float bearing = angle + closeness * swing;

            position = new Vector3(MathF.Cos(bearing) * reach, MathHelper.Lerp(position.Y, lens.Y, closeness), MathF.Sin(bearing) * reach);
            target = Vector3.Lerp(target, aim, closeness);
            fieldOfView = MathHelper.Lerp(fieldOfView, fov, closeness);
        }

        /// <summary>The reading of <paramref name="raw"/> nearest <paramref name="previous"/> among its 2-pi-apart readings.</summary>
        public static float Unwrap(float previous, float raw) =>
            raw + MathHelper.TwoPi * MathF.Round((previous - raw) / MathHelper.TwoPi);

        /// <summary>
        /// Whether <paramref name="lens"/> stands clear of a plate whose top face is at <paramref name="plateTopY"/> from
        /// above: <see cref="HANG_ABOVE"/> over the face, and <see cref="HANG_CABLE_AIR"/> off each of the four cables
        /// (<paramref name="cableX"/>, <paramref name="cableZ"/> off the axis, mirrored). Everything a map hangs is under the
        /// face but the winches on it and the cables over it, so a lens that clears those is not inside the map - which is
        /// what the front end asks before it hangs one (#408), and the one way a lens over the glass is clear of it.
        /// </summary>
        public static bool ClearAbove(Vector3 lens, float plateTopY, float cableX, float cableZ)
        {
            if (lens.Y < plateTopY + HANG_ABOVE) return false;

            float dx = MathF.Abs(lens.X) - cableX, dz = MathF.Abs(lens.Z) - cableZ;

            //The nearest of the four cables is the one in the lens's own quadrant
            return dx * dx + dz * dz >= HANG_CABLE_AIR * HANG_CABLE_AIR;
        }
    }
}
