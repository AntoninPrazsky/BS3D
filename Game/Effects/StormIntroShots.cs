using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The storm's own shots for a chapter intro's prologue (#559): the arena over the cloud deck from high
    /// above it, a glide over the cumulus tops, and a slow push in on the one cell a strike is about to go off
    /// in — cut together, then cut to the tour's last leg. The tour is a spline round the arena at stand-off
    /// height, and the storm's subject is below and around it: the cells stand a hundred and more units out
    /// and down, and the lightning is an event of half a second somewhere among them.
    /// <para>
    /// <b>The strike is caught, not hoped for.</b> The flash is a pure function of the wall clock
    /// (<see cref="SceneRenderer.TryGetSceneEvent"/>, the very schedule the light and the thunder ride), so
    /// the builder looks ahead for the strike that will fall inside the prologue, lengthens or shortens the
    /// first two shots within their bounds so the third opens a second before it, and aims that shot at the
    /// cell it goes off in. A strike falling where no split of the first two shots can reach puts the strike
    /// shot first instead, and failing that the shot still looks at the cell the next strike picks.
    /// </para>
    /// <para>
    /// <b>The cells move and the lens keeps out of them.</b> Every cell's body is read off the field itself
    /// (<see cref="SceneRenderer.StormCell"/>, carried downwind to the shot's own seconds), and a lens point is
    /// refused when it stands inside any cell's reach — the radius and a half across, and the height and half
    /// the radius up. Inside a puff the whole frame is milk. Built once when the intro begins; nothing here runs
    /// per frame.
    /// </para>
    /// </summary>
    internal static class StormIntroShots
    {
        //The deck: a crane out and up over the arena, the lens on a point far out over the deck on the other
        //side, so the island and its cluster stand in the bottom of the frame over the cells.
        private const float DECK_FROM_RADIUS = 55f;
        private const float DECK_TO_RADIUS = 80f;
        private const float DECK_FROM_HEIGHT = 40f;
        private const float DECK_TO_HEIGHT = 110f;
        private const float DECK_LOOK_OUT = 220f;
        private const float DECK_LOOK_Y = -70f;

        //The tops: a straight run this long this far out, this high over the tallest crown it passes, the
        //look along the run pitched down so the cauliflower crowns slide under the lens.
        private const float TOPS_RUN = 90f;
        private const float TOPS_FROM_RADIUS = 170f;
        private const float TOPS_TO_RADIUS = 360f;
        private const float TOPS_CLEARANCE = 14f;
        private const float TOPS_PITCH_DOWN_DEGREES = 18f;

        //The strike: a push in from this far outside the cell's reach to this far, this high over its middle,
        //off the line from the arena by up to this angle, the look on the cell low down where the bolt runs.
        private const float STRIKE_FROM = 190f;
        private const float STRIKE_TO = 130f;
        private const float STRIKE_RISE = 18f;
        private const float STRIKE_SIDE_DEGREES = 35f;
        private const float STRIKE_SECONDS = 3.4f;

        //How far into the strike shot the flash should land: late enough that the cell is read before it
        //lights, early enough that the flicker and the fade play out inside the shot.
        private const float STRIKE_LEAD_MIN = 0.8f;
        private const float STRIKE_LEAD = 1.2f;
        private const float STRIKE_LEAD_MAX = 2.2f;

        //The bounds the first two shots may be stretched or squeezed within to land the strike.
        private const float SHOT_MIN = 2.6f;
        private const float SHOT_DEFAULT = 3.2f;
        private const float SHOT_MAX = 4.4f;

        //How far outside a cell's reach a lens must stay.
        private const float CELL_MARGIN = 10f;

        private const int CANDIDATES = 60;
        private const int PATH_POINTS = 64;

        /// <summary>
        /// The prologue for the storm being drawn, starting at <paramref name="time"/> on the renderer's wall
        /// clock, or null when the field has no cells. <paramref name="fieldOfView"/> is the intro's own
        /// gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float time, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Storm) is not StormSceneConfig storm || scenes.StormCellCount == 0) return null;

            //Where the strike falls: first as the third shot, then as the first.
            float deckSeconds = SHOT_DEFAULT, topsSeconds = SHOT_DEFAULT;
            bool strikeFirst = false;
            Vector2? strikeAt = null;

            float earliest = time + 2f * SHOT_MIN + STRIKE_LEAD_MIN;
            float latest = time + 2f * SHOT_MAX + STRIKE_LEAD_MAX;
            if (FindStrike(scenes, earliest, latest, out float onset, out Vector2 at))
            {
                //Split the two shots' combined length evenly within their bounds, so the strike lands at
                //STRIKE_LEAD into the third where it can and slides within the lead's bounds where it cannot.
                float both = Math.Clamp(onset - time - STRIKE_LEAD, 2f * SHOT_MIN, 2f * SHOT_MAX);
                deckSeconds = topsSeconds = both * 0.5f;
                strikeAt = at;
            }
            else if (FindStrike(scenes, time + STRIKE_LEAD_MIN, time + STRIKE_LEAD_MAX, out onset, out at))
            {
                strikeFirst = true;
                strikeAt = at;
            }

            float strikeStart = strikeFirst ? time : time + deckSeconds + topsSeconds;
            float deckStart = strikeFirst ? time + STRIKE_SECONDS : time;
            float topsStart = deckStart + deckSeconds;

            //No strike inside the prologue at all: look at the cell the next one picks, which is still the
            //cell the thunder a moment later will come from.
            if (strikeAt == null && scenes.TryGetSceneEvent(SceneKind.Storm, strikeStart + 1f, out SceneEvent next))
                strikeAt = new Vector2(next.At.X, next.At.Z);

            IntroShot deck = Deck(scenes, deckStart, deckSeconds, fieldOfView, random);
            IntroShot tops = Tops(scenes, storm, topsStart, topsSeconds, fieldOfView, random);
            IntroShot strike = strikeAt is Vector2 target ? Strike(scenes, storm, target, strikeStart, fieldOfView, random) : null;

            //A shot that found no clear line is left out; the deck always has one.
            IntroShot[] order = strikeFirst ? new[] { strike, deck, tops } : new[] { deck, tops, strike };
            return Array.FindAll(order, shot => shot != null);
        }

        //The first strike whose light starts inside [from, to], and where it goes off. One strike a period,
        //so stepping by a fraction of the period visits every one.
        private static bool FindStrike(SceneRenderer scenes, float from, float to, out float onset, out Vector2 at)
        {
            for (float t = from - 6f; t <= to; t += 0.25f)
            {
                if (!scenes.TryGetSceneEvent(SceneKind.Storm, t, out SceneEvent strike)) break;
                if (strike.OnsetTime < from || strike.OnsetTime > to) continue;

                //Asked at the onset itself: the strike stands in the cell where the cell is THEN.
                scenes.TryGetSceneEvent(SceneKind.Storm, strike.OnsetTime + 0.01f, out strike);
                onset = strike.OnsetTime;
                at = new Vector2(strike.At.X, strike.At.Z);
                return true;
            }

            onset = 0f;
            at = Vector2.Zero;
            return false;
        }

        /// <summary>Out and up over the arena, looking across it at the deck: the establishing view.</summary>
        private static IntroShot Deck(SceneRenderer scenes, float start, float seconds, float fieldOfView, Random random)
        {
            Vector3[] path = null;
            float look = 0f;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                var candidate = new Vector3[PATH_POINTS];

                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float s = i / (float)(PATH_POINTS - 1);
                    float r = MathHelper.Lerp(DECK_FROM_RADIUS, DECK_TO_RADIUS, s);
                    candidate[i] = new Vector3(MathF.Cos(bearing) * r, MathHelper.Lerp(DECK_FROM_HEIGHT, DECK_TO_HEIGHT, s), MathF.Sin(bearing) * r);
                }

                path = candidate;
                look = bearing + MathHelper.Pi;
                if (ClearOfCells(scenes, candidate, start, seconds)) break;
            }

            Vector3 lookAt = new(MathF.Cos(look) * DECK_LOOK_OUT, DECK_LOOK_Y, MathF.Sin(look) * DECK_LOOK_OUT);
            return new IntroShot("the deck", path, seconds, fieldOfView * 1.25f, lookAt: lookAt);
        }

        /// <summary>A glide over the cumulus crowns, just over the tallest it passes.</summary>
        private static IntroShot Tops(SceneRenderer scenes, StormSceneConfig storm, float start, float seconds, float fieldOfView, Random random)
        {
            float middle = start + seconds * 0.5f;
            int count = scenes.StormCellCount;

            float bestScore = float.MinValue;
            Vector3[] best = null;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(TOPS_FROM_RADIUS, TOPS_TO_RADIUS, (float)random.NextDouble());
                Vector2 from = new(MathF.Cos(bearing) * radius, MathF.Sin(bearing) * radius);
                float heading = bearing + (random.Next(2) == 0 ? 1f : -1f) * MathHelper.PiOver2 + MathHelper.ToRadians(MathHelper.Lerp(-25f, 25f, (float)random.NextDouble()));
                Vector2 to = from + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * TOPS_RUN;

                //The highest crown within reach of the run, and how many crowns stand close under it — a run
                //over a lone tower with open air round it is a picture of one cloud, not of the deck.
                float highest = float.MinValue;
                int under = 0;

                for (int c = 0; c < count; c++)
                {
                    Vector3 foot = scenes.StormCell(c, middle, out float r, out float h);
                    float reach = r * 1.6f + CELL_MARGIN;
                    if (DistanceToSegment(new Vector2(foot.X, foot.Z), from, to) > reach) continue;

                    float crown = foot.Y + h + r * 0.6f;
                    highest = MathF.Max(highest, crown);
                    under++;
                }

                if (under == 0) continue;

                float y = highest + TOPS_CLEARANCE;

                //Low over many crowns wins; the lens rides the tallest, so a run past one tower over low cloud
                //scores its height against it.
                float score = under * 12f - (y - storm.Clouds.BaseYMax);
                if (score <= bestScore) continue;

                var path = new Vector3[PATH_POINTS];
                for (int i = 0; i < PATH_POINTS; i++)
                {
                    Vector2 plan = Vector2.Lerp(from, to, i / (float)(PATH_POINTS - 1));
                    path[i] = new Vector3(plan.X, y, plan.Y);
                }

                if (!ClearOfCells(scenes, path, start, seconds)) continue;

                bestScore = score;
                best = path;
            }

            return best == null ? null
                : new IntroShot("the tops", best, seconds, fieldOfView * 1.2f, lookAhead: 30f, pitchDownDegrees: TOPS_PITCH_DOWN_DEGREES);
        }

        /// <summary>A slow push in on the cell the strike goes off in.</summary>
        private static IntroShot Strike(SceneRenderer scenes, StormSceneConfig storm, Vector2 strikeAt, float start, float fieldOfView, Random random)
        {
            //The cell the strike stands in: the nearest middle to it at the shot's start.
            int count = scenes.StormCellCount;
            int cell = 0;
            float nearest = float.MaxValue;
            for (int c = 0; c < count; c++)
            {
                Vector3 foot = scenes.StormCell(c, start, out _, out _);
                float d = Vector2.Distance(new Vector2(foot.X, foot.Z), strikeAt);
                if (d < nearest) { nearest = d; cell = c; }
            }

            //The bolt runs down the layer (StormClouds.fx: LayerTopY + 8 to LayerBottomY + 18), so the look
            //sits in the middle of that, where the channel and the glow are.
            float boltY = (storm.Clouds.LayerTopY + 8f + storm.Clouds.LayerBottomY + 18f) * 0.5f;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                float side = MathHelper.ToRadians(MathHelper.Lerp(-STRIKE_SIDE_DEGREES, STRIKE_SIDE_DEGREES, (float)random.NextDouble()));
                var path = new Vector3[PATH_POINTS];
                var look = new Vector3[PATH_POINTS];

                for (int i = 0; i < PATH_POINTS; i++)
                {
                    float s = i / (float)(PATH_POINTS - 1);
                    Vector3 foot = scenes.StormCell(cell, start + s * STRIKE_SECONDS, out float r, out float h);
                    Vector2 middle = new(foot.X, foot.Z);

                    //From the arena's side, turned off that line by the roll.
                    Vector2 toArena = middle.LengthSquared() > 1f ? Vector2.Normalize(-middle) : Vector2.UnitX;
                    float c = MathF.Cos(side), sn = MathF.Sin(side);
                    Vector2 back = new(toArena.X * c - toArena.Y * sn, toArena.X * sn + toArena.Y * c);

                    Vector2 plan = middle + back * (r * 1.6f + MathHelper.Lerp(STRIKE_FROM, STRIKE_TO, s));
                    path[i] = new Vector3(plan.X, foot.Y + h * 0.45f + STRIKE_RISE, plan.Y);
                    look[i] = new Vector3(foot.X, MathF.Min(boltY, foot.Y + h * 0.45f), foot.Z);
                }

                if (!ClearOfCells(scenes, path, start, STRIKE_SECONDS)) continue;

                return new IntroShot("the strike", path, STRIKE_SECONDS, fieldOfView * 1.2f, lookAtPath: look);
            }

            return null;
        }

        //True when no lens point, at the moment the lens passes it, stands inside any cell's reach. Every
        //fourth point is enough: the points are a unit or two apart and the margin is ten.
        private static bool ClearOfCells(SceneRenderer scenes, Vector3[] path, float start, float seconds)
        {
            int count = scenes.StormCellCount;

            for (int i = 0; i < path.Length; i += 4)
            {
                float t = start + seconds * i / (path.Length - 1f);
                Vector3 p = path[i];

                for (int c = 0; c < count; c++)
                {
                    Vector3 foot = scenes.StormCell(c, t, out float r, out float h);
                    float reach = r * 1.6f + CELL_MARGIN;

                    if (Vector2.DistanceSquared(new Vector2(p.X, p.Z), new Vector2(foot.X, foot.Z)) > reach * reach) continue;
                    if (p.Y < foot.Y - r * 0.6f - CELL_MARGIN || p.Y > foot.Y + h + r * 0.6f + CELL_MARGIN) continue;

                    return false;
                }
            }

            return true;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 run = to - from;
            float t = MathHelper.Clamp(Vector2.Dot(point - from, run) / MathF.Max(run.LengthSquared(), 1e-4f), 0f, 1f);
            return Vector2.Distance(point, from + run * t);
        }
    }
}
