using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The meadow's own shots for a chapter intro's prologue (#559): the valley from a hilltop, the flowers at
    /// the grass's own height, and a climb up a slope to its skyline — cut together, then cut to the tour's
    /// last leg. The meadow opens the whole campaign, so this is the first thing the game shows a new player
    /// after its menu; the owner's word on the spline it replaces was that it turned the camera round like a
    /// neck, and that a cut should do the job a turn did.
    /// <para>
    /// <b>The meadow has nothing standing on it</b> — the grass, the flowers and the wind are all in
    /// <c>Meadow.fx</c> — so each shot is about a piece of the LAND: the basin the arena stands in, the flowers
    /// that are only ever readable from a hand's height (#447 found them invisible from the tour's stands), and
    /// the rise of a hill against the sky. Every height is read off
    /// <see cref="TerrainMirror.Meadow"/>, the mirror of the shader's field, so the lens holds its
    /// stated height over the grass whatever the config's hills are. Built once when the intro begins.
    /// </para>
    /// </summary>
    internal static class MeadowIntroShots
    {
        //The valley: a level dolly in from the hills towards the arena, this high over the highest ground
        //under it, on the hilliest of a few rolled bearings — a view from a hilltop down into the basin.
        private const float VALLEY_FROM = 195f;
        private const float VALLEY_TO = 160f;
        private const float VALLEY_ABOVE_FROM = 26f;
        private const float VALLEY_ABOVE_TO = 18f;
        private const float VALLEY_SECONDS = 3.4f;

        //The flowers: a run outward across the clearing a hand over the grass, looking a little down so the
        //rosettes pass under the lens and the hills stand at the top of the frame. From just outside the arena's
        //keep-out to the foot of the hills.
        private const float FLOWERS_FROM = 56f;
        private const float FLOWERS_TO = 96f;
        private const float FLOWERS_ABOVE = 0.9f;
        private const float FLOWERS_PITCH_DOWN_DEGREES = 12f;
        private const float FLOWERS_SECONDS = 3.2f;

        //The crest: a climb up the steepest of a few rolled slopes at a man's height, looking along the rise a
        //touch down (up, it was a frame of sky with a strip of grass under it),
        //so the grass runs up the frame to a skyline with the clouds over it.
        private const float CREST_FROM = 150f;
        private const float CREST_TO = 205f;
        private const float CREST_ABOVE = 1.8f;
        private const float CREST_PITCH_DOWN_DEGREES = 2f;
        private const float CREST_SECONDS = 3.2f;

        private const int TRIES = 24;

        /// <summary>The prologue for the meadow, or null when there is no meadow config.</summary>
        public static IntroShot[] Build(MeadowSceneConfig meadow, float fieldOfView, Random random)
        {
            if (meadow == null) return null;

            var ground = new IntroGround((x, z) => TerrainMirror.Meadow(x, z, meadow));

            //The hilliest bearing: the ground at the run's start, where the lens stands on the hill.
            IntroShot valley = ground.Establishing("the valley", VALLEY_FROM, VALLEY_TO, VALLEY_ABOVE_FROM, VALLEY_ABOVE_TO,
                VALLEY_SECONDS, fieldOfView * 1.1f, random, TRIES, margin: 2f,
                score: bearing => ground.Height(MathF.Cos(bearing) * VALLEY_FROM, MathF.Sin(bearing) * VALLEY_FROM));

            return IntroGround.Cut(valley, Flowers(ground, fieldOfView, random), Crest(ground, fieldOfView, random));
        }

        /// <summary>Outward across the clearing a hand over the grass, the flowers passing under the lens.</summary>
        private static IntroShot Flowers(IntroGround ground, float fieldOfView, Random random)
        {
            for (int attempt = 0; attempt < TRIES; attempt++)
            {
                Vector2 heading = IntroGround.Heading((float)random.NextDouble() * MathHelper.TwoPi);
                Vector3[] path = ground.Hug(heading * FLOWERS_FROM, heading * FLOWERS_TO, FLOWERS_ABOVE, FLOWERS_ABOVE, IntroGround.PATH_POINTS);
                if (!ground.Clear(path, 1f, FLOWERS_ABOVE * 0.8f)) continue;

                return new IntroShot("the flowers", path, FLOWERS_SECONDS, fieldOfView * 1.1f,
                    lookAhead: 14f, pitchDownDegrees: FLOWERS_PITCH_DOWN_DEGREES);
            }

            return null;
        }

        /// <summary>Up the steepest of a few rolled slopes, looking along the rise to the skyline.</summary>
        private static IntroShot Crest(IntroGround ground, float fieldOfView, Random random)
        {
            Vector3[] best = null;
            float bestClimb = float.MinValue;

            for (int attempt = 0; attempt < TRIES; attempt++)
            {
                Vector2 heading = IntroGround.Heading((float)random.NextDouble() * MathHelper.TwoPi);
                Vector3[] path = ground.Hug(heading * CREST_FROM, heading * CREST_TO, CREST_ABOVE, CREST_ABOVE, IntroGround.PATH_POINTS);
                if (!ground.Clear(path, 1f, CREST_ABOVE * 0.8f)) continue;

                //Up the slope, not across it: a run along a side slope photographs as a tilted horizon, so
                //the fall across the run's middle counts against its climb.
                Vector2 across = new(-heading.Y, heading.X);
                Vector2 middle = heading * (0.5f * (CREST_FROM + CREST_TO));
                float sideways = MathF.Abs(ground.Height(middle.X + across.X * 12f, middle.Y + across.Y * 12f)
                    - ground.Height(middle.X - across.X * 12f, middle.Y - across.Y * 12f));
                float climb = path[^1].Y - path[0].Y - 2f * sideways;
                if (climb <= bestClimb) continue;

                best = path;
                bestClimb = climb;
            }

            return best == null ? null
                : new IntroShot("the crest", best, CREST_SECONDS, fieldOfView * 1.1f,
                    lookAhead: 16f, pitchDownDegrees: CREST_PITCH_DOWN_DEGREES);
        }
    }
}
