using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The Moon's own shots for a chapter intro's prologue (#559): a crane over the crater plain with the
    /// landing pad in the middle of it, a rise over the rim of one real crater with the black shadow in its bowl,
    /// and the Earth over the highland skyline on a long lens — cut together, then cut to the tour's last leg.
    /// The tour it replaces flew one spline round the arena at the stand-off's height, which from the Moon's
    /// flat clearing is a skim over grey ground; each of these is about one thing the scene builds.
    /// <para>
    /// <b>The ground has no CPU mirror, so the lens keeps off a CEILING</b>
    /// (<see cref="OffworldGround.MoonCeiling"/>): the height the terrain can never stand above at a point —
    /// every crater's lip at its highest, the mare, the highland belt and the curvature exact. Each path is held
    /// a clearance over the highest ceiling under it, so it is over the ground whatever the lattice rolled there.
    /// </para>
    /// <para>
    /// <b>The crater is a real one</b> — the largest of <c>Moon.fx</c>'s top octave in a band round the arena,
    /// found by <see cref="OffworldGround.MoonCraters"/> walking the shader's own cells — and <b>the Earth is a
    /// direction</b>: at its true 0.95° it is thirty pixels in the game's frame, so its shot is the one long lens
    /// here, solved from the crest's angle and the Earth's so that both stand in it and nothing else. The lenses
    /// are absolute (see <see cref="GridIntroShots"/>). Built once when the intro begins.
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

        //The crater: of the top octave's craters whose centres lie in this band round the arena (out where the
        //clearing's ramp has given the field most of its amplitude), the widest few; one of them is rolled. The
        //lens rises from this many radii out, at the lip's height, to this many radii and this many radii up,
        //the look pinned on the bowl — from the side away from the sun, so the shadowed wall faces the lens. ⚠ The
        //first cut rose from 2.3 radii to 1.5 and 1.1 up: twenty units from the rim that is a fast turn of the
        //view, and in play (#402's motion blur) the whole crater came out smeared; this is half the travel.
        private const float CRATER_MIN_DISTANCE = 105f, CRATER_MAX_DISTANCE = 180f;
        private const int CRATER_CANDIDATES = 3;
        private const float CRATER_FROM_RADII = 2.1f, CRATER_TO_RADII = 1.7f;
        private const float CRATER_TO_HEIGHT_RADII = 0.9f;
        private const float CRATER_OFF_SUN_DEGREES = 35f;
        private const float CRATER_FOV_DEGREES = 54f;
        private const float CRATER_SECONDS = 3.2f;

        //The Earth: a slow push towards the highlands on the Earth's own bearing, this far out from the arena,
        //low over the plain; the frame solved so the crest stands this share of the way up it and the Earth this
        //share under its top.
        private const float EARTH_FROM_RADIUS = 115f, EARTH_TO_RADIUS = 140f;
        private const float EARTH_GROUND_SHARE = 0.25f, EARTH_SKY_SHARE = 0.14f;
        private const float EARTH_MIN_FOV_DEGREES = 18f, EARTH_MAX_FOV_DEGREES = 50f;
        private const float EARTH_SECONDS = 3.4f;

        //A fixed look-at far out along a direction stands in for the direction itself.
        private const float FAR = 5000f;

        private const int PATH_POINTS = 64;

        /// <summary>The prologue for the Moon the renderer is drawing, or null when it has no config.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Moon) is not MoonSceneConfig moon) return null;

            //The Moon's own sun, which is a constant of the scene and not of any dome (#508).
            Vector3 sun = scenes.TryGetSunDirection(SceneKind.Moon, out Vector3 found) ? found : Vector3.Normalize(new Vector3(0.6f, 0.3f, 0.7f));
            Vector2 sunward = Vector2.Normalize(new Vector2(sun.X, sun.Z) + new Vector2(1e-4f, 0f));

            var shots = new List<IntroShot>(3) { Plain(moon.Terrain, sunward, random) };

            IntroShot crater = Crater(moon.Terrain, sunward, random);
            if (crater != null) shots.Add(crater);

            shots.Add(Earth(moon, random));

            return shots.ToArray();
        }

        /// <summary>
        /// The establishing view: a crane up over the plain towards the pad, lit across the frame, the highland
        /// belt closing the skyline behind it.
        /// </summary>
        private static IntroShot Plain(MoonTerrainConfig terrain, Vector2 sunward, Random random)
        {
            //Looking across the sun: the view's bearing is the sun's turned a rolled way round.
            Vector2 view = Rotate(sunward, MathHelper.ToRadians(PLAIN_ACROSS_SUN_DEGREES) * (random.Next(2) == 0 ? 1f : -1f));

            Vector3[] path = Line(
                new Vector3(-view.X * PLAIN_FROM_RADIUS, terrain.LevelY + PLAIN_FROM_HEIGHT, -view.Y * PLAIN_FROM_RADIUS),
                new Vector3(-view.X * PLAIN_TO_RADIUS, terrain.LevelY + PLAIN_TO_HEIGHT, -view.Y * PLAIN_TO_RADIUS));
            KeepOver(path, terrain);

            Vector3 lookAt = new(view.X * PLAIN_LOOK_BEYOND, terrain.LevelY, view.Y * PLAIN_LOOK_BEYOND);

            return new IntroShot("the plain", path, PLAIN_SECONDS, MathHelper.ToRadians(PLAIN_FOV_DEGREES), lookAt: lookAt);
        }

        /// <summary>
        /// One crater: a rise over its lip, from the side away from the sun, until the lens looks down into the
        /// bowl — the sunward wall in black shadow, the far rim lit. Null when the band holds no crater at all,
        /// which the shipped config never does.
        /// </summary>
        private static IntroShot Crater(MoonTerrainConfig terrain, Vector2 sunward, Random random)
        {
            var craters = new List<OffworldGround.Crater>();
            OffworldGround.MoonCraters(terrain, CRATER_MIN_DISTANCE, CRATER_MAX_DISTANCE, craters.Add);
            if (craters.Count == 0) return null;

            craters.Sort((a, b) => b.Radius.CompareTo(a.Radius));
            OffworldGround.Crater crater = craters[random.Next(Math.Min(CRATER_CANDIDATES, craters.Count))];

            //From the side away from the sun, turned a little a rolled way round so the rim is not dead square on.
            Vector2 away = Rotate(-sunward, MathHelper.ToRadians(CRATER_OFF_SUN_DEGREES) * (random.Next(2) == 0 ? 1f : -1f));
            Vector3 outward = new(away.X, 0f, away.Y);

            float rim = crater.Centre.Y + CLEARANCE;
            Vector3[] path = Line(
                crater.Centre + outward * (crater.Radius * CRATER_FROM_RADII) + Vector3.Up * (rim - crater.Centre.Y),
                crater.Centre + outward * (crater.Radius * CRATER_TO_RADII) + Vector3.Up * (crater.Radius * CRATER_TO_HEIGHT_RADII));
            KeepOver(path, terrain);

            return new IntroShot("the crater", path, CRATER_SECONDS, MathHelper.ToRadians(CRATER_FOV_DEGREES),
                lookAt: crater.Centre - Vector3.Up * (crater.Depth * 0.5f));
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
        private static void KeepOver(Vector3[] path, MoonTerrainConfig terrain)
        {
            float lift = 0f;
            foreach (Vector3 point in path)
                lift = MathF.Max(lift, OffworldGround.MoonCeiling(point.X, point.Z, terrain) + CLEARANCE - point.Y);

            for (int i = 0; i < path.Length; i++) path[i].Y += lift;
        }

        private static Vector2 Rotate(Vector2 v, float radians) =>
            new(v.X * MathF.Cos(radians) - v.Y * MathF.Sin(radians), v.X * MathF.Sin(radians) + v.Y * MathF.Cos(radians));

        private static Vector3[] Line(Vector3 from, Vector3 to)
        {
            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++) path[i] = Vector3.Lerp(from, to, i / (float)(PATH_POINTS - 1));

            return path;
        }
    }
}
