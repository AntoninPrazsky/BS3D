using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// Mars's own shots for a chapter intro's prologue (#559, #1115): a run low down the rover's ruts towards the arena,
    /// tilting up off them onto the island they lead to, a push at rover height towards one real mesa with its strata,
    /// and Phobos over the skyline — cut together, then cut to the tour's last leg. The tour it replaces opened on
    /// Phobos from a stand round the arena and swung from there to the map; each of these is about one thing the scene
    /// builds. Where the tier draws no ruts (the reduced ground, <see cref="SceneRenderer.MarsDrawsTracks"/>) the
    /// prologue opens as it did before them, on a crane over the rust plain with the arena in the middle of it.
    /// <para>
    /// <b>The ground has no CPU mirror, so the lens keeps off a CEILING</b>
    /// (<see cref="OffworldGround.MarsCeiling"/>): the crater field, the boulders and the pebbles at their
    /// highest, and the mesas exact — they are the one thing tall enough to hide a lens in, and the noise that
    /// raises them can clear its threshold well inside their ring. The crane and the two pushes are held a
    /// clearance over the highest ceiling under them, as one constant lift; the tracks shot flies the ceiling the
    /// way the arid runs fly their ground (<see cref="AridIntroPaths.Hug"/>, #1109), so it comes down with it as
    /// the crater field gives way to the clearing.
    /// </para>
    /// <para>
    /// <b>The ruts are fixed</b> (<see cref="MarsTracks.TRAVERSE"/>), so the tracks shot is the same run every time.
    /// <b>The mesa is a real one</b>, found by sampling <see cref="OffworldGround.MarsMesa"/> — the shader's own
    /// <c>MesaField</c> — round the ring, and approached only across open plain, so the push stays at a rover's
    /// height rather than lifting over a nearer butte. The lenses are absolute (see
    /// <see cref="GridIntroShots"/>). Built once when the intro begins.
    /// </para>
    /// </summary>
    internal static class MarsIntroShots
    {
        //How far a lens stays over the ceiling — the ceiling being the tallest boulder the lattice could roll.
        internal const float CLEARANCE = 2.5f;

        //THE TRACKS (#1115). The owner: "the intro animation does not deal with the rover tracks". The lens runs down the
        //ruts' middle line this far, low over the plain, and stops this far along them short of where they pass nearest the
        //arena: down the straight stretch where the ruts come in from the north-west heading for the island, before their
        //swing round it turns them away from it. It opens looking down at the ruts this far ahead of the lens, and tilts up
        //off them over the whole shot to end with the island's top this far over the frame's middle and the look turned
        //this share of the way from the ruts round to the island: the ruts run in from the frame's foot and lead the eye
        //to the island and the cluster over it. Heading south-east under the dome's 58-degree sun from the north-east
        //(SkyDome's dome 19), so the light falls square across the ruts from the lens's right.
        //
        //Over the middle line because that is where no stone stands (MarsBackdrop keeps every stone TRACK_CLEAR and its own
        //size off it). The height is the ceiling's, CLEARANCE over it at and TRACKS_LATERAL units round each point: the
        //crater field's bound and the boulders' put the lens 6.5 to 10 units over the plain here, low as Mars's ceiling
        //lets it fly, and higher than the Moon's tracks shot, which flies the ground's own mirror.
        private const float TRACKS_STOP_SHORT = 57f;
        private const float TRACKS_LENGTH = 45f;
        private const float TRACKS_LATERAL = 2f;
        private const float TRACKS_LOOK_AHEAD = 22f;
        private const float TRACKS_ISLAND_OVER_MIDDLE_DEGREES = 9f;
        private const float TRACKS_TURN_TO_ISLAND = 0.65f;
        private const float TRACKS_FOV_DEGREES = 50f;
        internal const float TRACKS_SECONDS = 3.6f;

        //The establishing crane (the Moon's figures, for the Moon's reason: a stand round the arena from which
        //the island sits in the middle of its plain), looking at a point this far beyond the arena.
        private const float PLAIN_FROM_RADIUS = 128f, PLAIN_TO_RADIUS = 108f;
        private const float PLAIN_FROM_HEIGHT = 22f, PLAIN_TO_HEIGHT = 36f;
        private const float PLAIN_LOOK_BEYOND = 160f;
        private const float PLAIN_FOV_DEGREES = 56f;
        private const float PLAIN_SECONDS = 3.2f;

        //The mesa: sampled round the ring this far in and out at these steps; a point counts when the mesa
        //standing on it is this much of the full height. Of those, the nearest few, and one rolled.
        private const float MESA_SEARCH_FROM = 250f, MESA_SEARCH_TO = 520f;
        private const float MESA_RADIAL_STEP = 8f, MESA_BEARING_STEP_DEGREES = 2f;
        private const float MESA_FULL = 0.75f;
        private const int MESA_CANDIDATES = 24;

        //The push: from this far short of the mesa's point to this far, low over the plain; the look pinned
        //this far up the mesa. A candidate whose approach crosses ground this far over the plain's level
        //(another butte, or a skirt of this one) is refused, so the lens stays at rover height.
        private const float MESA_FROM = 175f, MESA_TO = 125f;
        private const float MESA_LOOK_UP = 0.45f;
        private const float MESA_OPEN_PLAIN = 9f;
        private const float MESA_MAX_SKYLINE_DEGREES = 28f;
        private const float MESA_FOV_DEGREES = 46f;
        private const float MESA_SECONDS = 3.4f;

        //Phobos over a mesa: a slow push along the moon's own bearing towards a mesa standing on it, from this
        //far short of the mesa's point to this far, low over open plain — so the butte's top crosses the bottom
        //of the frame and the moon hangs over it; the frame solved so the skyline stands this share of the way up
        //it and the moon this share under its top. ⚠ The first cut stood the lens a hundred units out along the bearing with nothing
        //in front of it, and the frame was a tan sky with a black dot in it: at 38 degrees up and a degree
        //across, Phobos needs something under it to be a moon over a place rather than a speck. When no mesa
        //has an open approach along that bearing, the push stands this far out from the arena instead.
        private const float PHOBOS_FROM = 150f, PHOBOS_TO = 128f;
        private const float PHOBOS_FALLBACK_FROM_RADIUS = 100f, PHOBOS_FALLBACK_TO_RADIUS = 118f;
        private const float PHOBOS_GROUND_SHARE = 0.28f, PHOBOS_SKY_SHARE = 0.12f;
        private const float PHOBOS_SKYLINE_MIN_DEGREES = 5f, PHOBOS_OVER_SKYLINE_DEGREES = 13f;
        private const float PHOBOS_MIN_FOV_DEGREES = 22f, PHOBOS_MAX_FOV_DEGREES = 60f;
        private const float PHOBOS_SECONDS = 3.0f;

        //A fixed look-at far out along a direction stands in for the direction itself.
        private const float FAR = 5000f;

        private const int PATH_POINTS = IntroPaths.POINTS;

        /// <summary>The prologue for the Mars the renderer is drawing, or null when it has no config.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Mars) is not MarsSceneConfig mars) return null;

            return Build(mars, scenes.MarsDrawsTracks, random);
        }

        /// <summary>
        /// The prologue for a Mars of this config: the tracks where the tier draws the ruts (<paramref name="tracksDrawn"/>),
        /// the plain where it does not, then the mesa where one has an open approach, then Phobos.
        /// </summary>
        internal static IntroShot[] Build(MarsSceneConfig mars, bool tracksDrawn, Random random)
        {
            var shots = new List<IntroShot>(3) { tracksDrawn ? Tracks(mars.Terrain) : Plain(mars.Terrain, random) };

            IntroShot mesa = Mesa(mars.Terrain, random);
            if (mesa != null) shots.Add(mesa);

            shots.Add(Phobos(mars, random));

            return shots.ToArray();
        }

        /// <summary>
        /// The rover's tracks (#1115): a run low down the ruts' middle line towards the arena, the look tilting up off the ruts
        /// ahead onto the island they lead to. The same every time: the ruts do not move.
        /// </summary>
        internal static IntroShot Tracks(MarsTerrainConfig terrain)
        {
            var ruts = new RutsWalk(MarsTracks.TRAVERSE);
            float end = ruts.AlongNearest(Vector2.Zero) - TRACKS_STOP_SHORT, start = end - TRACKS_LENGTH;
            Vector2[] plan = ruts.Plan(start, end, AridIntroPaths.POINTS);

            Vector3[] path = AridIntroPaths.Hug(plan, (x, z) => OffworldGround.MarsCeiling(x, z, terrain), CLEARANCE, TRACKS_LATERAL,
                TRACKS_SECONDS);

            //The look, point by point on the path's own clock: from the ruts TRACKS_LOOK_AHEAD on (on the plain) to the island's
            //top, its heading turned TRACKS_TURN_TO_ISLAND of the way and its pitch all of it less the island's lift over the
            //frame's middle, along a smoothstep over the shot. Blended as a heading and a pitch, not as a point: a point slid
            //from twenty units off to a hundred and twenty turns the view most of the way in the first tenth of the slide
            Vector3 island = new(0f, ArenaIsland.TOP_Y, 0f);
            float under = MathHelper.ToRadians(TRACKS_ISLAND_OVER_MIDDLE_DEGREES);
            var look = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                float s = i / (float)(path.Length - 1);
                Vector2 rut = ruts.At(MathHelper.Lerp(start, end, s) + TRACKS_LOOK_AHEAD);
                Vector3 toRuts = new Vector3(rut.X, terrain.LevelY, rut.Y) - path[i];
                Vector3 toIsland = island - path[i];

                float blend = IntroPaths.Smooth(s);
                float heading = MathF.Atan2(toRuts.Z, toRuts.X);
                heading += MathHelper.WrapAngle(MathF.Atan2(toIsland.Z, toIsland.X) - heading) * TRACKS_TURN_TO_ISLAND * blend;
                float pitch = MathHelper.Lerp(Elevation(toRuts), Elevation(toIsland) - under, blend);

                look[i] = path[i] + new Vector3(MathF.Cos(pitch) * MathF.Cos(heading), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Sin(heading)) * FAR;
            }

            return new IntroShot("the tracks", path, TRACKS_SECONDS, MathHelper.ToRadians(TRACKS_FOV_DEGREES), lookAtPath: look);
        }

        //How far over the level a direction climbs, radians
        private static float Elevation(Vector3 direction) => MathF.Asin(MathHelper.Clamp(direction.Y / direction.Length(), -1f, 1f));

        /// <summary>The establishing view where no ruts are drawn: a crane up over the plain towards the arena, on a rolled bearing.</summary>
        private static IntroShot Plain(MarsTerrainConfig terrain, Random random)
        {
            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
            Vector2 view = new(MathF.Cos(bearing), MathF.Sin(bearing));

            Vector3[] path = Line(
                new Vector3(-view.X * PLAIN_FROM_RADIUS, terrain.LevelY + PLAIN_FROM_HEIGHT, -view.Y * PLAIN_FROM_RADIUS),
                new Vector3(-view.X * PLAIN_TO_RADIUS, terrain.LevelY + PLAIN_TO_HEIGHT, -view.Y * PLAIN_TO_RADIUS));
            KeepOver(path, terrain);

            Vector3 lookAt = new(view.X * PLAIN_LOOK_BEYOND, terrain.LevelY, view.Y * PLAIN_LOOK_BEYOND);

            return new IntroShot("the plain", path, PLAIN_SECONDS, MathHelper.ToRadians(PLAIN_FOV_DEGREES), lookAt: lookAt);
        }

        /// <summary>
        /// One mesa: a push at rover height towards a point on it, from the arena's side, the look pinned part-way
        /// up its cliff so the strata stand across the frame. Null when no mesa in the search band has an open
        /// approach.
        /// </summary>
        private static IntroShot Mesa(MarsTerrainConfig terrain, Random random)
        {
            List<Vector2> found = FindMesas(terrain);

            //The nearest few, rolled in turn until one has an open approach.
            int count = Math.Min(MESA_CANDIDATES, found.Count);
            var order = new List<int>(count);
            for (int i = 0; i < count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            foreach (int index in order)
            {
                Vector2 point = found[index];
                Vector2 towardsArena = -Vector2.Normalize(point);

                Vector3[] path = Line(
                    new Vector3(point.X + towardsArena.X * MESA_FROM, terrain.LevelY, point.Y + towardsArena.Y * MESA_FROM),
                    new Vector3(point.X + towardsArena.X * MESA_TO, terrain.LevelY, point.Y + towardsArena.Y * MESA_TO));
                KeepOver(path, terrain);

                if (path[0].Y > terrain.LevelY + MESA_OPEN_PLAIN + CLEARANCE) continue;

                //And not up against a cliff: the mesa's point on its own radial can lie well behind where the
                //same butte (or a nearer one) crosses the line of approach.
                if (Skyline(path[^1], towardsArena * -1f, terrain) > MathHelper.ToRadians(MESA_MAX_SKYLINE_DEGREES)) continue;

                Vector3 lookAt = new(point.X, terrain.LevelY + terrain.MesaHeight * MESA_LOOK_UP, point.Y);

                return new IntroShot("the mesa", path, MESA_SECONDS, MathHelper.ToRadians(MESA_FOV_DEGREES), lookAt: lookAt);
            }

            return null;
        }

        /// <summary>
        /// Phobos over a mesa: a slow push along the moon's own bearing towards a mesa standing on it, low over
        /// open plain, the frame's pitch and height solved from the skyline's elevation and the moon's so both
        /// stand in it.
        /// </summary>
        private static IntroShot Phobos(MarsSceneConfig mars, Random random)
        {
            MarsTerrainConfig terrain = mars.Terrain;
            Vector3 phobos = OffworldGround.PhobosDirection(mars.Moons);
            float phobosBearing = MathF.Atan2(phobos.Z, phobos.X);
            Vector2 look2 = new(MathF.Cos(phobosBearing), MathF.Sin(phobosBearing));

            float elevation = MathF.Asin(MathHelper.Clamp(phobos.Y, -1f, 1f));

            //The nearest mesas first; the lens stands short of one along the moon's bearing, over open plain, and
            //only where the skyline under the moon stands high enough to be something under it and low enough —
            //at the END of the push, where it stands highest — to leave the moon clear of it. ⚠ The first cut
            //with a mesa took the nearest one over open plain and nothing else, and its lens stood before a
            //cliff face that filled the frame: a mesa's point on its own radial says nothing about where its
            //cliff crosses the moon's bearing, which can be much nearer.
            Vector3[] path = null;
            float skyline = 0f;
            foreach (Vector2 point in FindMesas(terrain))
            {
                Vector3[] candidate = Line(
                    new Vector3(point.X - look2.X * PHOBOS_FROM, terrain.LevelY, point.Y - look2.Y * PHOBOS_FROM),
                    new Vector3(point.X - look2.X * PHOBOS_TO, terrain.LevelY, point.Y - look2.Y * PHOBOS_TO));
                KeepOver(candidate, terrain);

                if (candidate[0].Y > terrain.LevelY + MESA_OPEN_PLAIN + CLEARANCE) continue;

                float fromStart = Skyline(candidate[0], look2, terrain);
                float atEnd = Skyline(candidate[^1], look2, terrain);
                if (fromStart < MathHelper.ToRadians(PHOBOS_SKYLINE_MIN_DEGREES)) continue;
                if (atEnd > elevation - MathHelper.ToRadians(PHOBOS_OVER_SKYLINE_DEGREES)) continue;

                path = candidate;
                skyline = fromStart;
                break;
            }

            if (path == null)
            {
                float bearing = phobosBearing + (float)(random.NextDouble() - 0.5) * 0.4f;
                Vector2 along = new(MathF.Cos(bearing), MathF.Sin(bearing));

                path = Line(
                    new Vector3(along.X * PHOBOS_FALLBACK_FROM_RADIUS, terrain.LevelY, along.Y * PHOBOS_FALLBACK_FROM_RADIUS),
                    new Vector3(along.X * PHOBOS_FALLBACK_TO_RADIUS, terrain.LevelY, along.Y * PHOBOS_FALLBACK_TO_RADIUS));
                KeepOver(path, terrain);
                skyline = Skyline(path[0], look2, terrain);
            }

            Vector3 lens = path[0];
            float fov = MathHelper.Clamp((elevation - skyline) / (1f - PHOBOS_GROUND_SHARE - PHOBOS_SKY_SHARE),
                MathHelper.ToRadians(PHOBOS_MIN_FOV_DEGREES), MathHelper.ToRadians(PHOBOS_MAX_FOV_DEGREES));
            float pitch = skyline - PHOBOS_GROUND_SHARE * fov + fov * 0.5f;

            Vector3 look = new(look2.X * MathF.Cos(pitch), MathF.Sin(pitch), look2.Y * MathF.Cos(pitch));

            return new IntroShot("Phobos", path, PHOBOS_SECONDS, fov, lookAt: lens + look * FAR);
        }

        //The skyline's elevation from a lens along a bearing: the ceiling's highest angle over the lens out to
        //where the haze has closed. The ceiling is the ground at its highest, so the real skyline is at or under it.
        private static float Skyline(Vector3 lens, Vector2 along, MarsTerrainConfig terrain)
        {
            float skyline = 0f;
            for (float r = 10f; r <= 600f; r += 5f)
            {
                Vector2 at = new Vector2(lens.X, lens.Z) + along * r;
                skyline = MathF.Max(skyline, MathF.Atan2(OffworldGround.MarsCeiling(at.X, at.Y, terrain) - lens.Y, r));
            }

            return skyline;
        }

        //The nearest point along each of a fan of bearings where a mesa stands most of its full height, nearest
        //first — MesaField sampled round the ring.
        private static List<Vector2> FindMesas(MarsTerrainConfig terrain)
        {
            var found = new List<Vector2>();
            for (float degrees = 0f; degrees < 360f; degrees += MESA_BEARING_STEP_DEGREES)
            {
                float bearing = MathHelper.ToRadians(degrees);
                Vector2 along = new(MathF.Cos(bearing), MathF.Sin(bearing));

                for (float r = MESA_SEARCH_FROM; r <= MESA_SEARCH_TO; r += MESA_RADIAL_STEP)
                {
                    if (OffworldGround.MarsMesa(along.X * r, along.Y * r, terrain) < terrain.MesaHeight * MESA_FULL) continue;
                    found.Add(along * r);
                    break;
                }
            }

            found.Sort((a, b) => a.LengthSquared().CompareTo(b.LengthSquared()));
            return found;
        }

        //Holds every point of a path CLEARANCE over the highest ceiling anywhere under it, as one constant lift.
        private static void KeepOver(Vector3[] path, MarsTerrainConfig terrain) =>
            IntroPaths.KeepOver(path, (x, z) => OffworldGround.MarsCeiling(x, z, terrain), CLEARANCE);

        private static Vector3[] Line(Vector3 from, Vector3 to) => IntroPaths.Line(from, to, PATH_POINTS);
    }
}
