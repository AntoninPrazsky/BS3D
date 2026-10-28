using Microsoft.Xna.Framework;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The geometry of the front end's pass at the gun (#1040): where the lens stands and what it looks at, as a pure
    /// function of where the gun stands and how far the pass has got. No device and no state, so the rules the pass must
    /// keep — a stand-off from the gun, never inside the cluster's bounding sphere, never past the scene's open ground —
    /// are held by tests (<c>GunPassTests</c>) and not by a reader's care.
    /// <para>
    /// <b>The lens turns round the gun on its outer side.</b> The gun stands outside the cluster's footprint by at least four
    /// units (<see cref="Prazsky.BS3D.GameCameraFit"/> holds it that far), so a lens circling the gun at a stand-off keeps well
    /// clear of the cluster while it is on the side away from the axis. The arc is therefore centred <i>behind</i> the gun,
    /// where a player's own lens stands, and runs <see cref="SWEEP"/> across it: from beside the gun, round its breech, to
    /// beside it again on the other side. The inner half of a full circle is never flown: it goes in under the cluster's
    /// footprint, and the pass has no business there. <see cref="KeepClear"/> is the guarantee for a map whose bounding sphere
    /// reaches further than the footprint says.
    /// </para>
    /// </summary>
    internal static class GunPass
    {
        /// <summary>
        /// How far round the gun the lens turns across the whole pass, radians: 180 degrees, from directly beside the gun on one
        /// flank, round over its breech, to directly beside it on the other. Enough to read as turning round the gun (both flanks
        /// in profile and the breech between them), and no further: past 90 degrees to either side the lens comes in towards the
        /// cluster's side of the gun, where a 220 degree sweep was photographed skimming the cluster's lowest balls (#1040).
        /// </summary>
        public static readonly float SWEEP = MathHelper.ToRadians(180f);

        /// <summary>
        /// How far over the trunnions the lens looks: the loading window sits on the barrel's top face and the round in it is
        /// the detail the pass comes to see, so the aim is a little above the pins, which also lets the gun stand low in the frame
        /// with the cluster over it.
        /// </summary>
        public const float TARGET_LIFT = 1.2f;

        /// <summary>
        /// The lens and its aim at <paramref name="progress"/> of the sweep (0 at its start, 1 at its end).
        /// </summary>
        /// <param name="gun">The gun's trunnions in the world.</param>
        /// <param name="standOff">The horizontal distance of the lens from the trunnions.</param>
        /// <param name="lift">How far over the trunnions the lens stands.</param>
        /// <param name="direction">+1 or -1: which way round the gun the sweep runs.</param>
        /// <param name="progress">How far through the sweep, 0 to 1.</param>
        public static void Pose(Vector3 gun, float standOff, float lift, float direction, float progress,
            out Vector3 position, out Vector3 target)
        {
            Vector3 outward = Outward(gun);
            Vector3 tangent = new(-outward.Z, 0f, outward.X);

            //Zero behind the gun, along the outward line from the axis through it
            float bearing = direction * SWEEP * (progress - 0.5f);

            position = gun + (outward * MathF.Cos(bearing) + tangent * MathF.Sin(bearing)) * standOff + Vector3.Up * lift;
            target = gun + Vector3.Up * TARGET_LIFT;
        }

        /// <summary>
        /// Which way round the gun the sweep should run for a lens that stands at <paramref name="lens"/> when the pass
        /// begins: the way that starts it on the side of the gun the lens is already on, so the approach swings the shorter
        /// way round the island instead of across it. +1 or -1.
        /// </summary>
        public static float DirectionFrom(Vector3 gun, Vector3 lens)
        {
            Vector3 outward = Outward(gun);
            Vector3 tangent = new(-outward.Z, 0f, outward.X);

            //A sweep of direction d starts at bearing -d * SWEEP / 2, whose sine is -d times a positive number, so the start
            //is on the side of the lens when d is minus the side's sign
            float side = Vector3.Dot(lens - gun, tangent);

            return side >= 0f ? -1f : 1f;
        }

        /// <summary>The horizontal unit vector from the axis out through the gun; straight back where the gun stands on the axis.</summary>
        private static Vector3 Outward(Vector3 gun)
        {
            Vector3 outward = new(gun.X, 0f, gun.Z);

            return outward.LengthSquared() < 1e-6f ? Vector3.Backward : Vector3.Normalize(outward);
        }

        /// <summary>
        /// The lens pushed out of a sphere it must not be inside, straight away from the sphere's centre: the cluster's
        /// bounding sphere plus the pass's air (#408). Continuous in the lens, so a lens skimming the sphere slides round it
        /// and never jumps. A lens exactly at the centre goes out along the back axis.
        /// </summary>
        public static Vector3 KeepClear(Vector3 lens, Vector3 centre, float radius)
        {
            Vector3 away = lens - centre;
            float distance = away.Length();

            if (distance >= radius) return lens;
            if (distance < 1e-4f) return centre + Vector3.Backward * radius;

            return centre + away * (radius / distance);
        }

        /// <summary>
        /// The lens brought in, horizontally, to <paramref name="openRadius"/> of the axis where it stands further out: the
        /// scene's open ground (#1039), whose edge is a city's first facade. Infinity or a non-positive radius leaves it.
        /// </summary>
        public static Vector3 KeepInside(Vector3 lens, float openRadius)
        {
            if (!float.IsFinite(openRadius) || openRadius <= 0f) return lens;

            float reach = MathF.Sqrt(lens.X * lens.X + lens.Z * lens.Z);
            if (reach <= openRadius) return lens;

            float scale = openRadius / reach;

            return new Vector3(lens.X * scale, lens.Y, lens.Z * scale);
        }
    }
}
