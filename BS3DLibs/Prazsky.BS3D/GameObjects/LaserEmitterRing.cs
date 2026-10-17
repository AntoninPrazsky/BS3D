using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace Prazsky.BS3D.GameObjects
{
    /// <summary>
    /// Where the four laser emitters stand (#874), in one copy: the session draws them from it, the floor net's feeder
    /// beams start at their lenses, and the gun's carriage is kept off them by it (<see cref="Cannon.SetKeepouts"/>), so the
    /// picture, the light and the clearance cannot disagree about where an emitter is.
    /// <para>
    /// <b>One at each corner of the net</b>, on the gold band round the drain's mouth, on the line from the drain's centre
    /// through that corner and facing the centre, so they read as the net's corners made of metal. They stand
    /// <see cref="FOOT_RADIUS"/> from the centre, which puts the pedestal on the band
    /// (<see cref="ArenaIsland.FUNNEL_TOP_RADIUS"/> to one unit past it), the housing leaning out over the glass mouth
    /// and its lens <see cref="LensRadius"/> from the centre — just outside the corner of every shipped level's net
    /// (the largest footprint's corner is 13.8 out), so a feeder runs from the lens to the corner at its longest on the
    /// small levels and not at all on the largest.
    /// </para>
    /// <para>
    /// <b>Why they are this small and where the gun is held off them.</b> The gun orbits the whole island and walks
    /// between <c>rest ± 4</c> from the drain, so there is no ring of the island that is not the gun's. What it cannot
    /// do is reach an emitter: the barrel passes over (the emitter's top, <see cref="TopAboveBand"/>, is under the tube's
    /// underside at the lowest elevation, which the clearance test checks) and the carriage is kept out by
    /// <see cref="GunFloorRadius"/> — on every shipped level the rest radius is 17.5, and the emitters' circle does not
    /// reach it, so the walk is shortened only within about ten degrees of each emitter.
    /// </para>
    /// </summary>
    public static class LaserEmitterRing
    {
        public const int COUNT = 4;

        /// <summary>How far from the island's centre the pedestal stands: on the band, a unit wide, from the mouth outwards.</summary>
        public const float FOOT_RADIUS = ArenaIsland.FUNNEL_TOP_RADIUS + 0.4f;

        /// <summary>The housing's pitch about the pin, up towards the net: the lens stands higher than the rear, so the beam leaves slightly rising.</summary>
        public const float PITCH = 0.14f;

        /// <summary>
        /// How far from the foot the emitter reaches towards the gun, in plan: the housing's rear end and its radius. The
        /// circle the gun's carriage is held out of is this about the foot, widened by the carriage's own reach.
        /// </summary>
        public const float KEEPOUT_RADIUS = 0.8f;

        /// <summary>
        /// How far the lens crests above the band, and the highest the whole emitter gets (the housing's collar at the
        /// lens end): what the barrel's underside has to clear.
        /// </summary>
        public static readonly float TopAboveBand = LensApexAbove() + LaserEmitterMesh.COLLAR_RADIUS;

        private static float LensApexAbove() =>
            LaserEmitterMesh.PIVOT_Y + (LaserEmitterMesh.LENS_APEX_Y - LaserEmitterMesh.PIVOT_ALONG) * MathF.Sin(PITCH);

        /// <summary>The horizontal distance from the centre of the lens apex: the foot's radius less the housing's reach to the lens.</summary>
        public static readonly float LensRadius = FOOT_RADIUS
            - (LaserEmitterMesh.LENS_APEX_Y - LaserEmitterMesh.PIVOT_ALONG) * MathF.Cos(PITCH);

        /// <summary>
        /// The sign pair of corner <paramref name="index"/> of the net: (+,+), (-,+), (-,-), (+,-), so the four stand at
        /// quarter turns of one another on a square field.
        /// </summary>
        public static Vector2 CornerSigns(int index) => index switch
        {
            0 => new Vector2(1f, 1f),
            1 => new Vector2(-1f, 1f),
            2 => new Vector2(-1f, -1f),
            _ => new Vector2(1f, -1f),
        };

        /// <summary>The corner's own point on the net's plane: where its two outermost beams cross.</summary>
        public static Vector3 NetCorner(int index, float cornerX, float cornerZ, float planeY)
        {
            Vector2 s = CornerSigns(index);

            return new Vector3(s.X * cornerX, planeY, s.Y * cornerZ);
        }

        /// <summary>The bearing, from the island's centre, of the line through net corner <paramref name="index"/>.</summary>
        public static float Bearing(int index, float cornerX, float cornerZ)
        {
            Vector2 s = CornerSigns(index);

            return MathF.Atan2(s.Y * cornerZ, s.X * cornerX);
        }

        /// <summary>The unit vector in plan from the centre out through the corner.</summary>
        public static Vector3 Outward(int index, float cornerX, float cornerZ)
        {
            float bearing = Bearing(index, cornerX, cornerZ);

            return new Vector3(MathF.Cos(bearing), 0f, MathF.Sin(bearing));
        }

        /// <summary>Where the pedestal's foot sits: on the band at <see cref="FOOT_RADIUS"/>, a hair into it.</summary>
        public static Vector3 Foot(int index, float cornerX, float cornerZ)
        {
            Vector3 outward = Outward(index, cornerX, cornerZ);

            return new Vector3(outward.X * FOOT_RADIUS, ArenaIsland.FloorHeightAt(FOOT_RADIUS), outward.Z * FOOT_RADIUS);
        }

        /// <summary>
        /// The stand frame's world matrix: origin on the foot, +Y up, local -Z towards the island's centre (the way the
        /// emitter faces), local +X to the side. The pedestal and the yoke are drawn with it as it is.
        /// </summary>
        public static Matrix Stand(int index, float cornerX, float cornerZ)
        {
            Vector3 outward = Outward(index, cornerX, cornerZ);
            Matrix world = Matrix.CreateWorld(Vector3.Zero, -outward, Vector3.Up);
            Vector3 foot = Foot(index, cornerX, cornerZ);

            world.M41 = foot.X;
            world.M42 = foot.Y;
            world.M43 = foot.Z;

            return world;
        }

        /// <summary>The emission axis in the stand frame: forward (-Z) and rising by <see cref="PITCH"/>.</summary>
        private static Vector3 AxisInStand => new(0f, MathF.Sin(PITCH), -MathF.Cos(PITCH));

        /// <summary>
        /// The housing's world matrix: the housing frame (axis +Y, rear face at the origin) carried into the stand frame,
        /// the pin at <see cref="LaserEmitterMesh.PIVOT_ALONG"/> along it landing on the yoke's pin.
        /// </summary>
        public static Matrix Housing(int index, float cornerX, float cornerZ)
        {
            Vector3 axis = AxisInStand;
            //Right is the stand's own +X, and the back is the axis crossed from it: the pair with the axis a right-handed basis
            Vector3 back = Vector3.Cross(Vector3.UnitX, axis);
            Vector3 rear = new Vector3(0f, LaserEmitterMesh.PIVOT_Y, 0f) - axis * LaserEmitterMesh.PIVOT_ALONG;

            Matrix local = new(
                1f, 0f, 0f, 0f,
                axis.X, axis.Y, axis.Z, 0f,
                back.X, back.Y, back.Z, 0f,
                rear.X, rear.Y, rear.Z, 1f);

            return local * Stand(index, cornerX, cornerZ);
        }

        /// <summary>The point the beam leaves from, in the world: the lens's crest.</summary>
        public static Vector3 Lens(int index, float cornerX, float cornerZ) =>
            Vector3.Transform(new Vector3(0f, LaserEmitterMesh.LENS_APEX_Y, 0f), Housing(index, cornerX, cornerZ));

        /// <summary>
        /// The emission axis in the world (unit): the housing frame's +Y. Where the lens faces; the glint on it and the
        /// beam's direction at the start follow it.
        /// </summary>
        public static Vector3 Axis(int index, float cornerX, float cornerZ) =>
            Vector3.TransformNormal(Vector3.UnitY, Housing(index, cornerX, cornerZ));

        /// <summary>
        /// The least radius the gun's trunnions may stand at on <paramref name="bearing"/> (radians from the +X axis, any
        /// range) for the carriage to clear every emitter: the outside of a circle of radius
        /// <paramref name="gunReach"/> + <see cref="KEEPOUT_RADIUS"/> about each foot, as the gun moves round it. Zero away
        /// from every emitter, so a gun at rest is never touched by it.
        /// </summary>
        /// <param name="cornerX">Where the net's outermost beams stand in X (see <c>LaserGrid.CornerX</c>).</param>
        /// <param name="cornerZ">And in Z.</param>
        /// <param name="gunReach">The carriage's reach from its trunnions towards anything inward of them
        /// (<c>CannonRig.StandReach</c>).</param>
        public static float GunFloorRadius(float bearing, float cornerX, float cornerZ, float gunReach)
        {
            float d = gunReach + KEEPOUT_RADIUS;
            float floor = 0f;

            for (int i = 0; i < COUNT; i++)
            {
                float delta = bearing - Bearing(i, cornerX, cornerZ);
                float sin = FOOT_RADIUS * MathF.Sin(delta);
                float inside = d * d - sin * sin;

                //Past the circle's flank in angle there is nothing to be held off (and the far side of the island is
                //never near a foot: cosine negative would be the circle's other intersection, which is not the gun's side)
                if (inside <= 0f || MathF.Cos(delta) <= 0f) continue;

                floor = MathF.Max(floor, FOOT_RADIUS * MathF.Cos(delta) + MathF.Sqrt(inside));
            }

            return floor;
        }
    }
}
