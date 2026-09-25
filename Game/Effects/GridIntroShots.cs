using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The Grid's own shots for a chapter intro's prologue (#559): a crane up over the floor with the arena in
    /// the middle of its ring of solids, a slow slide past the face of one cube while its Game of Life runs,
    /// and a push through the landmark ring — cut together, then cut to the tour's last leg. The tour it
    /// replaces flew one spline round the arena at a few units over the floor and swung the lens between the
    /// solids (the owner's "neck-breaking turns"); each of these is about one object the scene builds.
    /// <para>
    /// <b>Every solid is a box the renderer placed</b> (<see cref="SceneRenderer.GridSolids"/>,
    /// <see cref="SceneRenderer.TryGetGridRing"/> — recorded as they are built, so under any <c>sceneseed</c>
    /// the shots frame the very solids on screen), which makes the Grid the one scene here where "is the lens
    /// inside something" has an exact answer. Every path is sampled against every solid, the ring and the
    /// floor, and a candidate that comes within <see cref="CLEARANCE"/> of any of them is refused and the next
    /// one rolled. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// <para>
    /// <b>The lenses are absolute, not the caller's frame widened</b> (the cities', the volcano's and the
    /// aurora's shots scale the frame they are handed, which is 60° in the scene menu's tour and 43° in play):
    /// a shot framed on one cube's face has to frame the same face in both, or the tour replayed from the
    /// menu is not a review of what play will show.
    /// </para>
    /// </summary>
    internal static class GridIntroShots
    {
        //How close a lens may come to any solid, the ring or the floor. The solids glow along every edge, and a
        //lens nearer than this reads the nearest one as a wall of seam.
        private const float CLEARANCE = 10f;

        //The establishing crane: from this far out and this high over the floor, rising to the second pair,
        //turning a few degrees round the arena on the way — on the far side of the arena from the ring, so the
        //ring stands across the floor behind the island. The look is pinned on a point on the floor beyond the
        //arena, so the island sits in the lower middle with the solids ringing it.
        private const float GRID_FROM_RADIUS = 118f, GRID_TO_RADIUS = 100f;
        private const float GRID_FROM_HEIGHT = 36f, GRID_TO_HEIGHT = 66f;
        private const float GRID_TURN_DEGREES = 9f;
        private const float GRID_LOOK_BEYOND = 80f;
        private const float GRID_FOV_DEGREES = 58f;
        private const float GRID_SECONDS = 3.2f;

        //The cube: a slide along the face that looks at the arena — the face every board's pattern is stamped
        //in the middle of — at this multiple of the face's height out from it, this fraction of the face's
        //width either side of its middle, the look pinned on the face's centre.
        private const float CUBE_STAND_OFF = 1.25f;
        private const float CUBE_SLIDE = 0.35f;
        private const float CUBE_FOV_DEGREES = 52f;
        private const float CUBE_SECONDS = 3.0f;

        //The ring: a straight push along its axis, from the arena's side through the hole to a little past its
        //plane, below the centre by this fraction of the hole so the floor's lines run through it, looking along
        //the travel and a little down. The run is shortened, at either end, while a solid stands in it.
        private const float RING_FROM = 125f, RING_PAST = 30f;
        private const float RING_BELOW_CENTRE = 0.3f;
        private const float RING_PITCH_DEGREES = 5f;
        private const float RING_FOV_DEGREES = 58f;

        //Wider than CLEARANCE: the push looks along its travel, so a solid it passes at ten units fills the
        //side of the frame for the last second — ⚠ which the first inward run did, ending on a cube's glowing
        //edge at what read as arm's length; at 22, and at 15, a tower still swept past the side of the frame like a
        //wall. When no run clears this — two seeds of four — the ring is framed whole from a stand in front of it
        //instead (RING_FACE_*), which needs only its own spot and a clear line to the ring to be clear.
        private const float RING_CLEARANCE = 30f;

        //The ring framed whole: a sideways slide this far either side of a stand these many outer radii out along
        //its axis, or turned this far round from it, the look pinned on the ring's centre.
        private const float RING_FACE_SLIDE = 22f;
        private static readonly float[] RING_FACE_OUTS = { 1.9f, 1.5f, 2.5f };
        private static readonly float[] RING_FACE_TURNS_DEGREES = { 0f, 25f, -25f, 50f, -50f };
        private const float RING_SECONDS = 3.2f;

        //How many bearings the establishing crane tries before it gives up, and how many points each path
        //carries (every one is tested).
        private const int ATTEMPTS = 24;
        private const int PATH_POINTS = 64;

        /// <summary>
        /// The prologue for the Grid the renderer is drawing, or null when it has none to show.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Grid) is not GridSceneConfig grid) return null;

            IReadOnlyList<GridSolid> solids = scenes.GridSolids;
            GridRing? ring = scenes.TryGetGridRing(out GridRing found) ? found : null;
            float floorY = grid.Terrain.LevelY;

            var shots = new List<IntroShot>(3);
            AddIfAny(shots, Establishing(solids, ring, floorY, random));
            AddIfAny(shots, Cube(solids, ring, floorY, random));
            if (ring is GridRing landmark) AddIfAny(shots, Ring(solids, landmark, floorY));

            return shots.Count > 0 ? shots.ToArray() : null;
        }

        /// <summary>
        /// The establishing view: a crane up and a little round, from just inside the ring of solids, looking
        /// across the arena at the solids on its far side — the island in the lower middle of the frame, the
        /// floor's lines running out from under it, the cubes and towers standing round.
        /// </summary>
        private static IntroShot Establishing(IReadOnlyList<GridSolid> solids, GridRing? ring, float floorY, Random random)
        {
            //On the far side of the arena from the ring when there is one, so it stands behind the island.
            float preferred = ring is GridRing r ? MathF.Atan2(-r.Centre.Z, -r.Centre.X) : (float)random.NextDouble() * MathHelper.TwoPi;
            float turn = MathHelper.ToRadians(GRID_TURN_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);

            for (int attempt = 0; attempt < ATTEMPTS; attempt++)
            {
                //The preferred bearing first, then wider and wider either side of it.
                float spread = attempt == 0 ? 0f : MathHelper.ToRadians(15f) * ((attempt + 1) / 2) * (attempt % 2 == 0 ? 1f : -1f);
                float bearing = preferred + spread;

                var path = new Vector3[PATH_POINTS];
                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float s = i / (float)(PATH_POINTS - 1);
                    float a = bearing + turn * s;
                    float radius = MathHelper.Lerp(GRID_FROM_RADIUS, GRID_TO_RADIUS, s);
                    path[i] = new Vector3(MathF.Cos(a) * radius, floorY + MathHelper.Lerp(GRID_FROM_HEIGHT, GRID_TO_HEIGHT, s), MathF.Sin(a) * radius);
                }

                if (!Clear(path, solids, ring, floorY)) continue;

                float middle = bearing + turn * 0.5f;
                Vector3 lookAt = new(-MathF.Cos(middle) * GRID_LOOK_BEYOND, floorY, -MathF.Sin(middle) * GRID_LOOK_BEYOND);

                return new IntroShot("the grid", path, GRID_SECONDS, MathHelper.ToRadians(GRID_FOV_DEGREES), lookAt: lookAt);
            }

            return null;
        }

        /// <summary>
        /// One cube's face while its board runs: a slow slide across the face that looks at the arena, the look
        /// pinned on the face's middle, where every pattern is stamped (see <c>BuildGridTowers</c>). Of the cubes
        /// whose slide is clear, a rolled one; a tower when no cube's is.
        /// </summary>
        private static IntroShot Cube(IReadOnlyList<GridSolid> solids, GridRing? ring, float floorY, Random random)
        {
            var order = new List<int>(solids.Count);
            for (int i = 0; i < solids.Count; i++) order.Add(i);

            //Cubes before towers, each group in a rolled order.
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            order.Sort((a, b) => solids[b].IsCube.CompareTo(solids[a].IsCube));

            foreach (int index in order)
            {
                GridSolid solid = solids[index];
                Vector3 centre = solid.Centre;
                Vector3 half = solid.Size * 0.5f;

                //The face whose outward normal points most nearly back at the arena — BuildGridTowers' own rule
                //for which face the board's centre is put on.
                bool alongX = MathF.Abs(solid.BaseCentre.X) >= MathF.Abs(solid.BaseCentre.Z);
                Vector3 normal = alongX
                    ? new Vector3(-MathF.Sign(solid.BaseCentre.X), 0f, 0f)
                    : new Vector3(0f, 0f, -MathF.Sign(solid.BaseCentre.Z));
                Vector3 across = alongX ? Vector3.UnitZ : Vector3.UnitX;
                float depth = alongX ? half.X : half.Z;
                float width = alongX ? solid.Size.Z : solid.Size.X;

                Vector3 face = centre + normal * depth;
                float standOff = solid.Size.Y * CUBE_STAND_OFF;
                float side = random.Next(2) == 0 ? 1f : -1f;

                var path = new Vector3[PATH_POINTS];
                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float slide = MathHelper.Lerp(-CUBE_SLIDE, CUBE_SLIDE, i / (float)(PATH_POINTS - 1)) * width * side;
                    path[i] = face + normal * standOff + across * slide;
                }

                //Clear of every OTHER solid, the ring and the floor, and outside the arena: the slide stands on
                //the arena's side of the cube, which is also the side the island is on.
                if (!Clear(path, solids, ring, floorY, skip: index)) continue;
                if (new Vector2(path[0].X, path[0].Z).Length() < 45f || new Vector2(path[^1].X, path[^1].Z).Length() < 45f) continue;

                //And nothing standing between the lens and the face. ⚠ Clearance alone let a tower stand in front
                //of the cube on one seed: the slide kept ten units off it, and the last second of the shot was the
                //tower's dark side filling the frame.
                if (!InSight(path, face, solids, ring, floorY, index)) continue;

                return new IntroShot(solid.IsCube ? "the cube" : "the tower", path, CUBE_SECONDS,
                    MathHelper.ToRadians(CUBE_FOV_DEGREES), lookAt: face);
            }

            return null;
        }

        /// <summary>
        /// Through the landmark: a straight push along the ring's axis, through the hole and a little past it,
        /// looking along the travel. From the far side in towards the arena first — so the push ends looking
        /// across the floor at the island among its solids, which the tour's last leg then flies to — and from
        /// the arena's side outwards when a solid stands in that run: outwards ends looking into the void past
        /// the ring (⚠ the first cut tried it first, and on one seed the last second was empty floor). Shortened
        /// at either end while a solid is in the way; when even the shortest run either way passes a solid too
        /// near, a slide in front of the ring framing it whole; left out when that is not clear either.
        /// </summary>
        private static IntroShot Ring(IReadOnlyList<GridSolid> solids, GridRing ring, float floorY)
        {
            Vector3 side = Vector3.Normalize(Vector3.Cross(ring.PlaneNormal, Vector3.Up));

            for (float from = RING_FROM; from >= 50f; from -= 15f)
            {
                for (float past = RING_PAST; past >= 0f; past -= 10f)
                {
                    IntroShot shot = RingRun(solids, ring, floorY, side, from, past);
                    if (shot != null) return shot;
                }
            }

            //No push is clear: frame the ring whole from in front of it — the arena's side first, square on and
            //then turned further and further round its axis, nearer and further — from the first stand that is
            //clear and has the ring in sight.
            foreach (float way in RING_WAYS_FACE)
            {
                foreach (float turn in RING_FACE_TURNS_DEGREES)
                {
                    foreach (float out1 in RING_FACE_OUTS)
                    {
                        Vector3 stand = Vector3.Transform(ring.PlaneNormal * way, Matrix.CreateRotationY(MathHelper.ToRadians(turn)));
                        Vector3 across = Vector3.Normalize(Vector3.Cross(stand, Vector3.Up));
                        Vector3 middle = ring.Centre + stand * (ring.OuterRadius * out1) - Vector3.Up * (ring.InnerRadius * RING_BELOW_CENTRE);
                        Vector3[] path = Line(middle - across * RING_FACE_SLIDE, middle + across * RING_FACE_SLIDE);

                        if (!Clear(path, solids, ring, floorY)) continue;

                        //In sight of the ring's centre and of either side of its band, so no solid stands across it.
                        if (!InSight(path, ring.Centre, solids, null, floorY) ||
                            !InSight(path, ring.Centre + side * ring.OuterRadius * 0.85f, solids, null, floorY) ||
                            !InSight(path, ring.Centre - side * ring.OuterRadius * 0.85f, solids, null, floorY)) continue;

                        return new IntroShot("the ring", path, RING_SECONDS, MathHelper.ToRadians(RING_FOV_DEGREES), lookAt: ring.Centre);
                    }
                }
            }

            return null;
        }

        //One length of push, tried inwards from the far side first and then outwards from the arena's side, and
        //through the middle of the hole's lower half first, then off to either side of it or higher up — anywhere
        //in the hole is through the ring, and a solid beside the axis can leave one side of the hole clear.
        private static IntroShot RingRun(IReadOnlyList<GridSolid> solids, GridRing ring, float floorY, Vector3 side,
            float from, float past)
        {
            foreach (float way in RING_WAYS)
            {
                foreach (Vector2 offset in RING_OFFSETS)
                {
                    Vector3 centre = ring.Centre + side * (offset.X * ring.InnerRadius) + Vector3.Up * (offset.Y * ring.InnerRadius);
                    Vector3 axis = ring.PlaneNormal * way;
                    Vector3[] path = Line(centre + axis * from, centre - axis * past);
                    if (!Clear(path, solids, ring, floorY, clearance: RING_CLEARANCE)) continue;

                    return new IntroShot("the ring", path, RING_SECONDS, MathHelper.ToRadians(RING_FOV_DEGREES),
                        lookAhead: 40f, pitchDownDegrees: RING_PITCH_DEGREES);
                }
            }

            return null;
        }

        private static readonly float[] RING_WAYS = { -1f, 1f };
        private static readonly float[] RING_WAYS_FACE = { 1f, -1f };

        //Where in the hole the push goes, in inner radii across (along the ring's side axis) and up from its
        //centre: the lower middle first (RING_BELOW_CENTRE), so the floor's lines run through the frame.
        private static readonly Vector2[] RING_OFFSETS =
        {
            new(0f, -RING_BELOW_CENTRE), new(0.35f, -RING_BELOW_CENTRE), new(-0.35f, -RING_BELOW_CENTRE), new(0f, 0.2f),
        };

        //Whether every point of a path keeps CLEARANCE off every solid (bar one, when a shot is OF that solid and
        //measures its own stand-off), the ring and the floor.
        private static bool Clear(Vector3[] path, IReadOnlyList<GridSolid> solids, GridRing? ring, float floorY,
            int skip = -1, float clearance = CLEARANCE)
        {
            foreach (Vector3 point in path)
            {
                if (point.Y < floorY + clearance) return false;
                if (ring is GridRing r && r.Distance(point) < clearance) return false;

                for (int i = 0; i < solids.Count; i++)
                    if (i != skip && solids[i].Distance(point) < clearance) return false;
            }

            return true;
        }

        //Whether the straight lines from the start, the middle and the end of a path to a target cross no solid
        //(bar the one the target is on) — the shot's subject in sight all the way along it.
        private static bool InSight(Vector3[] path, Vector3 target, IReadOnlyList<GridSolid> solids, GridRing? ring,
            float floorY, int skip = -1)
        {
            foreach (Vector3 from in new[] { path[0], path[path.Length / 2], path[^1] })
                if (!Clear(Line(from, target), solids, ring, floorY - 1f, skip, clearance: 0.5f)) return false;

            return true;
        }

        private static Vector3[] Line(Vector3 from, Vector3 to)
        {
            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++) path[i] = Vector3.Lerp(from, to, i / (float)(PATH_POINTS - 1));

            return path;
        }

        private static void AddIfAny(List<IntroShot> shots, IntroShot shot)
        {
            if (shot != null) shots.Add(shot);
        }
    }
}
