using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The Moon's own shots for a chapter intro's prologue (#559, #1114): a crane over the crater plain with the
    /// landing pad in the middle of it, a run low along the lunar rover's ruts up the highland belt's slope to the white
    /// flag beside them, and the Earth over the highland skyline on a long lens — cut together, then cut to the tour's
    /// last leg. The tour it replaces flew one spline round the arena at the stand-off's height, which from the Moon's
    /// flat clearing is a skim over grey ground; each of these is about one thing the scene builds.
    /// <para>
    /// <b>The crane and the Earth keep off a CEILING; the tracks follow the ground.</b> The two high or far lenses are held
    /// a clearance over <see cref="OffworldGround.MoonCeiling"/>, the height the terrain can never stand above at a point
    /// (every crater's lip at its highest, the mare, the highland belt and the curvature exact), as one constant lift, so
    /// they are over the ground whatever the lattice rolled there. The tracks shot flies a couple of units over the
    /// regolith, where a bound that takes every lip at its highest is a lens in the sky, so it follows the ground's own CPU
    /// mirror, <see cref="TerrainMirror.Moon"/> (#680), through <see cref="AridIntroPaths.Hug"/>: the highest ground a few
    /// units round each point, plus its clearance, rounded so it rises before a rim and never through it.
    /// </para>
    /// <para>
    /// <b>The ruts and the flag are fixed</b> (<see cref="MoonPlanting.TRAVERSE"/>, <see cref="MoonPlanting.FLAG"/>), so the
    /// tracks shot is the same run every time, and <b>the Earth is a direction</b>: at its true 0.95° it is thirty pixels
    /// in the game's frame, so its shot is the one long lens here, solved from the crest's angle and the Earth's so that
    /// both stand in it and nothing else. The lenses are absolute (see <see cref="GridIntroShots"/>). Built once when the
    /// intro begins.
    /// </para>
    /// </summary>
    internal static class MoonIntroShots
    {
        //How far a lens stays over the ceiling. The ceiling is already the highest lip the lattice could have
        //rolled, so this is the lens's own margin, not a guess at the ground's.
        private const float CLEARANCE = 3f;

        //The establishing crane: from this far out and this high over the plain's level to the second pair,
        //looking at a point this far beyond the arena (⚠ forty, and the first cut was two thirds foreground
        //regolith with the pad a small disc under the skyline; this far, the pad stands in the middle of its
        //plain with the highland belt behind it) — across the sun, so every crater between is lit on one
        //side and shadowed on the other, which is the only light that reads relief on a plain.
        private const float PLAIN_FROM_RADIUS = 128f, PLAIN_TO_RADIUS = 108f;
        private const float PLAIN_FROM_HEIGHT = 26f, PLAIN_TO_HEIGHT = 42f;
        private const float PLAIN_LOOK_BEYOND = 160f;
        private const float PLAIN_ACROSS_SUN_DEGREES = 80f;
        private const float PLAIN_FOV_DEGREES = 56f;
        private const float PLAIN_SECONDS = 3.2f;

        //THE TRACKS (#1114). The owner: "nowhere in the intro animation do I see a close-up of the wheel tracks of the lunar
        //rover", and the one flag "visible in a close-up". The lens runs up the ruts' middle line this far, low over the
        //regolith, and stops this far short of the flag's place along them, the look held on the flag the whole way: the
        //ruts lead up the slope to it and it grows from a white mark to a close-up, the lens easing from the wider field of
        //view to the narrower as it comes. Up the straight stretch where the rover climbed the belt, heading south-east
        //across the 16-degree sun (MoonLightingConfig's azimuth 40), so the light comes from the lens's right a little
        //behind and rakes the berms and the cleats, every one with its shadow (looking down-sun a rut is flat).
        //
        //Over the middle line because that is where no rock stands: MoonPlanting keeps every rock off the ruts by more than
        //the ruts' width and its own half-size, and off the flag by twelve units and more, inside which the lens stops.
        //
        //The height: TRACKS_CLEARANCE over the highest ground at and TRACKS_LATERAL units round each point, flown the way
        //the arid runs fly it (AridIntroPaths.Hug: rounded over the shot's travel, lifted back where a rim was cut, #1109),
        //so the lens rides 2.5 to 5.5 units over the regolith and still clears what the grid draws, whose triangles stand up to two units off the exact field across the
        //middle octave's rims (MoonPlanting's height). The look at TRACKS_LOOK_HEIGHT over the flag's foot holds the whole
        //flag in the frame at the end (its bar stands 5.5 over the ground) and its foot over the skip hint.
        private const float TRACKS_LENGTH = 48f;
        private const float TRACKS_STOP_SHORT = 11f;
        internal const float TRACKS_CLEARANCE = 2.2f;
        private const float TRACKS_LATERAL = 2f;
        private const float TRACKS_LOOK_HEIGHT = 2.4f;
        private const float TRACKS_FROM_FOV_DEGREES = 50f, TRACKS_TO_FOV_DEGREES = 40f;
        private const float TRACKS_SECONDS = 4.2f;

        //The Earth: a slow push towards the highlands on the Earth's own bearing, this far out from the arena,
        //low over the plain; the frame solved so the crest stands this share of the way up it and the Earth this
        //share under its top.
        private const float EARTH_FROM_RADIUS = 115f, EARTH_TO_RADIUS = 140f;
        private const float EARTH_GROUND_SHARE = 0.25f, EARTH_SKY_SHARE = 0.14f;
        private const float EARTH_MIN_FOV_DEGREES = 18f, EARTH_MAX_FOV_DEGREES = 50f;
        private const float EARTH_SECONDS = 3.4f;

        //A fixed look-at far out along a direction stands in for the direction itself.
        private const float FAR = 5000f;

        private const int PATH_POINTS = IntroPaths.POINTS;

        /// <summary>The prologue for the Moon the renderer is drawing, or null when it has no config.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Moon) is not MoonSceneConfig moon) return null;

            //The Moon's own sun, which is a constant of the scene and not of any dome (#508).
            Vector3 sun = scenes.TryGetSunDirection(SceneKind.Moon, out Vector3 found) ? found : Vector3.Normalize(new Vector3(0.6f, 0.3f, 0.7f));

            return Build(moon, sun, random);
        }

        /// <summary>The prologue for a Moon of this config under this sun: the plain, the tracks, the Earth.</summary>
        internal static IntroShot[] Build(MoonSceneConfig moon, Vector3 sun, Random random)
        {
            Vector2 sunward = Vector2.Normalize(new Vector2(sun.X, sun.Z) + new Vector2(1e-4f, 0f));

            return new[] { Plain(moon.Terrain, sunward, random), Tracks(moon.Terrain), Earth(moon, random) };
        }

        /// <summary>
        /// The establishing view: a crane up over the plain towards the pad, lit across the frame, the highland
        /// belt closing the skyline behind it.
        /// </summary>
        private static IntroShot Plain(MoonTerrainConfig terrain, Vector2 sunward, Random random)
        {
            //Looking across the sun: the view's bearing is the sun's turned a rolled way round.
            Vector2 view = IntroPaths.Rotate(sunward, MathHelper.ToRadians(PLAIN_ACROSS_SUN_DEGREES) * (random.Next(2) == 0 ? 1f : -1f));

            Vector3[] path = Line(
                new Vector3(-view.X * PLAIN_FROM_RADIUS, terrain.LevelY + PLAIN_FROM_HEIGHT, -view.Y * PLAIN_FROM_RADIUS),
                new Vector3(-view.X * PLAIN_TO_RADIUS, terrain.LevelY + PLAIN_TO_HEIGHT, -view.Y * PLAIN_TO_RADIUS));
            KeepOver(path, terrain);

            Vector3 lookAt = new(view.X * PLAIN_LOOK_BEYOND, terrain.LevelY, view.Y * PLAIN_LOOK_BEYOND);

            return new IntroShot("the plain", path, PLAIN_SECONDS, MathHelper.ToRadians(PLAIN_FOV_DEGREES), lookAt: lookAt);
        }

        /// <summary>
        /// The rover's tracks (#1114): a run low up the ruts' middle line towards the flag beside them, the look on the flag,
        /// ending on it close up. The same every time: the ruts and the flag do not move.
        /// </summary>
        internal static IntroShot Tracks(MoonTerrainConfig terrain)
        {
            Func<float, float, float> ground = (x, z) => TerrainMirror.Moon(x, z, terrain);

            //Up the ruts to the flag's place along them, short of it
            var ruts = new RutsWalk(MoonPlanting.TRAVERSE);
            Vector2 flag = MoonPlanting.FLAG;
            float end = ruts.AlongNearest(flag) - TRACKS_STOP_SHORT, start = end - TRACKS_LENGTH;
            Vector2[] plan = ruts.Plan(start, end, AridIntroPaths.POINTS);

            Vector3[] path = AridIntroPaths.Hug(plan, ground, TRACKS_CLEARANCE, TRACKS_LATERAL, TRACKS_SECONDS);
            Vector3 lookAt = new(flag.X, ground(flag.X, flag.Y) + TRACKS_LOOK_HEIGHT, flag.Y);

            return new IntroShot("the tracks", path, TRACKS_SECONDS, MathHelper.ToRadians(TRACKS_FROM_FOV_DEGREES), lookAt: lookAt,
                endFieldOfView: MathHelper.ToRadians(TRACKS_TO_FOV_DEGREES));
        }

        /// <summary>
        /// The Earth over the highlands, on a long lens: a slow push along the Earth's own bearing, low over the
        /// plain, the frame's pitch and height solved from the crest's elevation and the Earth's so the skyline
        /// runs along the bottom of the frame and the marble hangs near the top.
        /// </summary>
        private static IntroShot Earth(MoonSceneConfig moon, Random random)
        {
            MoonTerrainConfig terrain = moon.Terrain;
            Vector3 earth = moon.Earth.Direction.ToVector3();
            earth = earth.LengthSquared() > 1e-8f ? Vector3.Normalize(earth) : Vector3.Normalize(new Vector3(-0.33f, 0.4f, -0.86f));

            //A few degrees either side of the Earth's bearing, so the crest under it is not always the same.
            float bearing = MathF.Atan2(earth.Z, earth.X) + (float)(random.NextDouble() - 0.5) * 0.3f;
            Vector2 along = new(MathF.Cos(bearing), MathF.Sin(bearing));

            Vector3[] path = Line(
                new Vector3(along.X * EARTH_FROM_RADIUS, terrain.LevelY, along.Y * EARTH_FROM_RADIUS),
                new Vector3(along.X * EARTH_TO_RADIUS, terrain.LevelY, along.Y * EARTH_TO_RADIUS));
            KeepOver(path, terrain);

            //The crest's elevation from where the push starts: the highest the ceiling stands, seen from the lens,
            //out along the bearing past the belt. The ceiling is the ground at its highest, so the real skyline
            //is at or under this.
            Vector3 lens = path[0];
            float crest = -MathHelper.PiOver2;
            for (float r = EARTH_FROM_RADIUS + 10f; r <= terrain.HighlandCrestRadius + 250f; r += 5f)
            {
                float height = OffworldGround.MoonCeiling(along.X * r, along.Y * r, terrain) - lens.Y;
                crest = MathF.Max(crest, MathF.Atan2(height, r - EARTH_FROM_RADIUS));
            }

            float earthElevation = MathF.Asin(earth.Y);
            float fov = MathHelper.Clamp((earthElevation - crest) / (1f - EARTH_GROUND_SHARE - EARTH_SKY_SHARE),
                MathHelper.ToRadians(EARTH_MIN_FOV_DEGREES), MathHelper.ToRadians(EARTH_MAX_FOV_DEGREES));
            float pitch = crest - EARTH_GROUND_SHARE * fov + fov * 0.5f;

            //Looking along the Earth's own bearing, not the push's, so the marble stands on the frame's middle.
            float earthBearing = MathF.Atan2(earth.Z, earth.X);
            Vector3 look = new(MathF.Cos(earthBearing) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(earthBearing) * MathF.Cos(pitch));

            return new IntroShot("the Earth", path, EARTH_SECONDS, fov, lookAt: lens + look * FAR);
        }

        //Holds every point of a path CLEARANCE over the highest ceiling anywhere under the path, as one height
        //change for the whole of it: a constant lift, so a path laid level stays level and a crane stays a crane.
        private static void KeepOver(Vector3[] path, MoonTerrainConfig terrain) =>
            IntroPaths.KeepOver(path, (x, z) => OffworldGround.MoonCeiling(x, z, terrain), CLEARANCE);

        private static Vector3[] Line(Vector3 from, Vector3 to) => IntroPaths.Line(from, to, PATH_POINTS);
    }
}
