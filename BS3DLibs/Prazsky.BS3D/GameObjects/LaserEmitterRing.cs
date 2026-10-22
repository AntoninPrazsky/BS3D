using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace Prazsky.BS3D.GameObjects
{
    /// <summary>
    /// Where the four laser emitters stand and where they aim (#874), in one copy: the session draws them from it and the
    /// floor net's four projection cones start at their lenses, so the picture and the light cannot disagree about where
    /// an emitter is or which way it points.
    /// <para>
    /// <b>Mounted inside the funnel, on its glass, just under the rim.</b> One at each corner's bearing (the line from
    /// the drain's centre through that corner of the net), its foot <see cref="FOOT_SLANT"/> down the glass from the
    /// mouth and its pedestal standing straight out of the wall (<see cref="WallNormal"/>), so the housing and the lens
    /// peek a little over the rim. Inside the mouth and under the stone's plane nothing of the gun's ever comes: the
    /// walk's near end keeps the trunnions <c>GameCameraFit.CANNON_DRAIN_CLEARANCE</c> outside the mouth, so the wheels
    /// traversed to their full reach (<c>CannonRig.StandReach</c>) overhang the glass only above the rim, and the barrel
    /// at its lowest aim passes over the lenses. So the gun orbits and walks as it would with no emitters at all, and
    /// <c>LaserEmitterClearanceTests</c> sweeps the real poses against these matrices to keep it so.
    /// </para>
    /// <para>
    /// <b>Aimed at the corner, per level.</b> The mount is fixed; the housing turns on its pin until its axis runs from
    /// the pin through its corner of this level's net (<see cref="Axis"/>), so the axis meets the corner exactly whatever
    /// the footprint. The pin lies along the rim's tangent, and the pin and the corner are both on the corner's bearing,
    /// so one swing about the pin is all the aim needs. A level that reveals the net (#933) opens with the housings at
    /// <see cref="RestAxis"/> and turns them to the aim (<see cref="HousingTurned"/>): the same swing, part of the way.
    /// </para>
    /// </summary>
    public static class LaserEmitterRing
    {
        public const int COUNT = 4;

        /// <summary>
        /// How far down the glass from the mouth (along the wall, not straight down) the pedestal's foot is set: the
        /// flange's upper edge just under the rim, over the gold band's collar, so the emitter reads as fixed to the
        /// drain's lip and its head peeks over it.
        /// </summary>
        public const float FOOT_SLANT = 0.6f;

        //The drawn glass the emitters stand on: a straight cone from the mouth down to the drawn hole (ArenaIsland's own
        //figures, the very ones FunnelMesh is built from)
        private const float RIM_Y = ArenaIsland.TOP_Y - ArenaIsland.DISH_DEPTH;
        private const float WALL_RUN = ArenaIsland.FUNNEL_TOP_RADIUS - ArenaIsland.FUNNEL_DRAWN_HOLE_RADIUS;
        private const float WALL_DROP = RIM_Y - ArenaIsland.FUNNEL_BOTTOM_Y;
        private static readonly float WALL_LENGTH = MathF.Sqrt(WALL_RUN * WALL_RUN + WALL_DROP * WALL_DROP);

        /// <summary>
        /// The glass's normal on the inside, in the (outward, up) plane of any bearing: pointing into the funnel, inward and
        /// up (the wall leans 56 degrees from level, so this rises 34 degrees). The pedestal stands along it.
        /// </summary>
        public static readonly Vector2 WallNormal = new(-WALL_DROP / WALL_LENGTH, WALL_RUN / WALL_LENGTH);

        /// <summary>Down the glass, in the same (outward, up) plane: from the mouth towards the hole.</summary>
        public static readonly Vector2 WallDown = new(-WALL_RUN / WALL_LENGTH, -WALL_DROP / WALL_LENGTH);

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

        /// <summary>A direction given in the (outward, up) plane of a bearing, carried into the world.</summary>
        private static Vector3 OnBearing(Vector3 outward, Vector2 radialUp) =>
            new(outward.X * radialUp.X, radialUp.Y, outward.Z * radialUp.X);

        /// <summary>The wall's normal at emitter <paramref name="index"/>, in the world (unit): the pedestal's axis.</summary>
        public static Vector3 Normal(int index, float cornerX, float cornerZ) =>
            OnBearing(Outward(index, cornerX, cornerZ), WallNormal);

        /// <summary>Where the pedestal's foot sits: on the glass, <see cref="FOOT_SLANT"/> down it from the mouth.</summary>
        public static Vector3 Foot(int index, float cornerX, float cornerZ)
        {
            Vector3 outward = Outward(index, cornerX, cornerZ);
            Vector2 foot = new Vector2(ArenaIsland.FUNNEL_TOP_RADIUS, RIM_Y) + WallDown * FOOT_SLANT;

            return new Vector3(outward.X * foot.X, foot.Y, outward.Z * foot.X);
        }

        /// <summary>
        /// The stand frame's world matrix: origin on the foot, +Y out of the glass (<see cref="Normal"/>), +X along the
        /// rim's tangent (the pin's axis), -Z down the glass. The pedestal and the yoke are drawn with it as it is.
        /// </summary>
        public static Matrix Stand(int index, float cornerX, float cornerZ)
        {
            Vector3 outward = Outward(index, cornerX, cornerZ);

            //Forward down the glass: CreateWorld's +X is forward x up, which with up the wall's normal is the rim's tangent
            Matrix world = Matrix.CreateWorld(Vector3.Zero, OnBearing(outward, WallDown), OnBearing(outward, WallNormal));
            Vector3 foot = Foot(index, cornerX, cornerZ);

            world.M41 = foot.X;
            world.M42 = foot.Y;
            world.M43 = foot.Z;

            return world;
        }

        /// <summary>Where the pin runs through the housing, in the world: <see cref="LaserEmitterMesh.PIVOT_Y"/> out of the foot.</summary>
        public static Vector3 Pin(int index, float cornerX, float cornerZ) =>
            Foot(index, cornerX, cornerZ) + Normal(index, cornerX, cornerZ) * LaserEmitterMesh.PIVOT_Y;

        /// <summary>
        /// The emission axis in the world (unit): from the pin straight at the corner of this level's net on its plane
        /// <paramref name="planeY"/>. The housing turns on its pin to it, so the lens faces the corner and the beam leaves
        /// along it.
        /// </summary>
        public static Vector3 Axis(int index, float cornerX, float cornerZ, float planeY) =>
            Vector3.Normalize(NetCorner(index, cornerX, cornerZ, planeY) - Pin(index, cornerX, cornerZ));

        /// <summary>
        /// The housing's world matrix: the housing frame (axis +Y, rear face at the origin) carried onto the pin, the
        /// point <see cref="LaserEmitterMesh.PIVOT_ALONG"/> along its axis landing on the yoke's pin, and the axis turned
        /// about the pin onto <see cref="Axis"/>.
        /// </summary>
        public static Matrix Housing(int index, float cornerX, float cornerZ, float planeY) =>
            HousingFacing(index, cornerX, cornerZ, Axis(index, cornerX, cornerZ, planeY));

        /// <summary>
        /// The axis the housings stand at before they are aimed (#933): straight up, which lies in the plane of every
        /// bearing and square to the pin. A lamp at attention, pointing at nothing, and the swing to the aim is then as
        /// long as the net is small: about 70 degrees on an 11-wide one, whose aim runs 21 degrees over the horizontal
        /// inwards, down to about 5 on the widest, which already point nearly up. Clear of the glass and of the pedestal's
        /// cup at every turn between (<c>LaserEmitterClearanceTests</c>).
        /// </summary>
        public static Vector3 RestAxis(int index, float cornerX, float cornerZ) => Vector3.Up;

        /// <summary>
        /// The housing's axis at <paramref name="turn"/> of the way from <see cref="RestAxis"/> to <see cref="Axis"/>
        /// (#933): 0 is at rest, 1 is exactly the aim. A turn about the pin and nothing else - both axes lie in the
        /// bearing's plane, square to the pin, so the housing swings through that plane at an even angle in
        /// <paramref name="turn"/> (the caller eases it) and the pin never moves.
        /// </summary>
        public static Vector3 AxisTurned(int index, float cornerX, float cornerZ, float planeY, float turn)
        {
            Vector3 aim = Axis(index, cornerX, cornerZ, planeY);
            if (turn >= 1f) return aim;

            Vector3 rest = RestAxis(index, cornerX, cornerZ);
            if (turn <= 0f) return rest;

            //The pin's axis, the stand's +X, and the signed angle from rest to aim about it
            Vector3 pinAxis = Vector3.Normalize(Stand(index, cornerX, cornerZ).Right);
            float full = MathF.Atan2(Vector3.Dot(pinAxis, Vector3.Cross(rest, aim)), Vector3.Dot(rest, aim));
            float angle = full * turn;

            //Rodrigues' rotation of a vector square to the axis it turns about
            return Vector3.Normalize(rest * MathF.Cos(angle) + Vector3.Cross(pinAxis, rest) * MathF.Sin(angle));
        }

        /// <summary>
        /// The housing's world matrix at <paramref name="turn"/> of the way from rest to aim (<see cref="AxisTurned"/>):
        /// <see cref="Housing"/> at 1, exactly. The session turns the four with this, once a frame while they swing.
        /// </summary>
        public static Matrix HousingTurned(int index, float cornerX, float cornerZ, float planeY, float turn) =>
            HousingFacing(index, cornerX, cornerZ, AxisTurned(index, cornerX, cornerZ, planeY, turn));

        private static Matrix HousingFacing(int index, float cornerX, float cornerZ, Vector3 axis)
        {
            Vector3 pin = Pin(index, cornerX, cornerZ);

            //The pin's own axis, the stand's +X, is perpendicular to the emission axis by construction (the pin and the
            //corner are both on the bearing's plane); the basis is re-derived from the pair anyway, so it is orthonormal
            //to the last bit and right-handed (right x axis = back)
            Vector3 back = Vector3.Normalize(Vector3.Cross(Stand(index, cornerX, cornerZ).Right, axis));
            Vector3 right = Vector3.Cross(axis, back);
            Vector3 rear = pin - axis * LaserEmitterMesh.PIVOT_ALONG;

            return new Matrix(
                right.X, right.Y, right.Z, 0f,
                axis.X, axis.Y, axis.Z, 0f,
                back.X, back.Y, back.Z, 0f,
                rear.X, rear.Y, rear.Z, 1f);
        }

        /// <summary>The point the beam leaves from, in the world: the lens's crest, the centre of its face.</summary>
        public static Vector3 Lens(int index, float cornerX, float cornerZ, float planeY) =>
            Pin(index, cornerX, cornerZ)
            + Axis(index, cornerX, cornerZ, planeY) * (LaserEmitterMesh.LENS_APEX_Y - LaserEmitterMesh.PIVOT_ALONG);
    }
}
