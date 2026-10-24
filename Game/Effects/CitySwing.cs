using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The swing home</b> (#972): the last shot of a city's chapter intro, a fast run through the streets like a
    /// web-slinger's that lands on the gameplay pose itself, so the player is carried from the city into the game
    /// without a cut. The owner's note: "Spider-Man-style swinging fly-throughs until we finally arrive at the island
    /// and the camera gets into the game camera position".
    /// <para>
    /// <b>The route is a staircase on the street grid.</b> Down one street, a corner, along the cross street, a
    /// corner the other way, and down a last street towards the pose (the day city's two corners; the neon city's three,
    /// its own route, #973) — every leg on a street's centre line, where
    /// nothing stands at any height (see <see cref="CityIntroShots"/>), and every corner a fillet whose radius keeps the
    /// lens off the block it turns round. Then the release: a curve off the last street's line onto the pose, climbing
    /// out of the canyon into the clearing the island floats over and slowing to a stop on the pose, the look easing
    /// from along the travel onto the pose's own look.
    /// </para>
    /// <para>
    /// <b>The swing is a pendulum.</b> The corners are the tops of the swings and the legs sag between them, and the
    /// speed is the height traded for it (<c>v² = v_top² + 2 g · drop</c>): slowest round a corner, fastest at the bottom
    /// of a sag. The path is laid out by length and then walked by that speed, so its points come out evenly spaced in
    /// TIME, which is what <see cref="IntroShot"/> walks. The release decelerates on a cubic in time to rest on the pose.
    /// </para>
    /// <para>
    /// <b>It banks.</b> The lens rolls into a turn in proportion to how fast its heading turns, up to
    /// <c>MAX_ROLL_DEGREES</c>, smoothed and gone by the landing; the drop cinematic's own tilt is 4-11 degrees.
    /// </para>
    /// <para>
    /// <b>Found, not assumed.</b> Rolled candidates — which street the last leg runs down, the corners, the leg lengths,
    /// how far out the release begins — are each laid out in full and refused if any point of the path stands within
    /// <c>CLEARANCE</c> of a tower's box or the island; the best walled-in of the clear ones is flown. Null when none is
    /// clear, and the city's prologue then keeps the tour's arrival leg it had before.
    /// </para>
    /// </summary>
    internal static class CitySwing
    {
        /// <summary>What one city's swing is made of: the method is shared, the pacing and the route are each city's own.</summary>
        /// <param name="TopHeight">Over the street at a corner, the top of a swing.</param>
        /// <param name="Dip">How far a leg sags under its corners.</param>
        /// <param name="TopSpeed">Units a second round a corner, the slowest of the swing.</param>
        /// <param name="Gravity">What the pendulum trades height for speed by, units a second squared.</param>
        /// <param name="LegBlocksMin">The shortest entry and middle leg, blocks.</param>
        /// <param name="LegBlocksMax">The longest.</param>
        /// <param name="FinalBlocks">How far before the release the last corner stands, blocks.</param>
        /// <param name="ReleaseRunMin">How far behind the pose, along the last street, the curve onto it begins, shortest.</param>
        /// <param name="ReleaseRunMax">The longest.</param>
        /// <param name="WideLens">The field of view it opens at, times the game's: it eases to the game's own by the landing.</param>
        /// <param name="Corners">How many corners the staircase turns before the last street, alternating left and right.</param>
        /// <param name="ReleaseTimeScale">How long the release takes against its length over the speed it enters with: 2 is a
        /// steady deceleration to rest, more settles slower, and under 3 the cubic never runs backwards.</param>
        internal readonly record struct Style(float TopHeight, float Dip, float TopSpeed, float Gravity,
            int LegBlocksMin, int LegBlocksMax, float FinalBlocks, float ReleaseRunMin, float ReleaseRunMax, float WideLens,
            int Corners, float ReleaseTimeScale);

        //How far every point of the path stays from a tower's box (cornices included) and from the island's rim
        private const float CLEARANCE = 2f;

        //How close a corner's fillet passes the inner block's corner: a building stands at least 0.4 inside its block,
        //so the lens clears it by this plus that. The fillet's radius follows from it (see Fillet).
        private const float CORNER_CLEARANCE = 2.6f;

        //The least share of the candidates whose last street runs across the pose's facing (see Lay)
        private const float CROSS_STREET_SHARE = 0.25f;

        //How deep a sag between two corners may be against the leg it hangs in: a leg a block long sagging the full Dip
        //pitched the look over 99 degrees a second in the neon city's short legs; the day city's two-block legs sag in full
        private const float DIP_PER_LENGTH = 0.3f;

        //Where the bottom of the last sag falls between the last corner and the pose, as a share of that run: the rest
        //is the climb out of the canyon
        private const float RELEASE_BOTTOM_SHARE = 0.3f;

        //How much of the path's own rise and fall the look follows, how far out it looks, and over how long either side
        //of the moment its direction is averaged. ⚠ Along the path's own direction, not at a point some way ahead: a
        //chord to a point 0.35 s on cut every corner, and short across the turn while the drop into the next sag was
        //not, it pitched the view 140 degrees a second over each corner. Averaged, the view also turns into a corner
        //before the lens does, which takes the turn's peak down.
        private const float LOOK_PITCH_FOLLOW = 0.3f;
        private const float LOOK_DISTANCE = 30f;
        private const float LOOK_SMOOTH_SECONDS = 0.3f;

        //The last seconds, over which the look eases from along the travel onto the pose's own and the bank levels out
        private const float LOOK_LAND_SECONDS = 1.6f;

        //The bank: degrees of roll per degree a second of turn, at most MAX_ROLL_DEGREES, smoothed over ROLL_SMOOTH_SECONDS
        private const float ROLL_PER_TURN = 0.1f;
        private const float MAX_ROLL_DEGREES = 14f;
        private const float ROLL_SMOOTH_SECONDS = 0.25f;

        //How far the plan is cut before it is timed, and how many evenly timed points the shot carries
        private const float STEP = 0.5f;
        private const int POINTS = 240;

        private const int CANDIDATES = 96;

        /// <summary>The day city's swing (#972).</summary>
        internal static readonly Style Day = new(TopHeight: 80f, Dip: 20f, TopSpeed: 28f, Gravity: 46f,
            LegBlocksMin: 2, LegBlocksMax: 2, FinalBlocks: 1.5f, ReleaseRunMin: 26f, ReleaseRunMax: 40f, WideLens: 1.3f,
            Corners: 2, ReleaseTimeScale: 2f);

        /// <summary>
        /// The neon city's swing (#973): its own route and pacing, the owner's "they are two scenes and different cities".
        /// Three corners to the day city's two through its narrower streets (7.5 to 9), so it turns more and runs shorter
        /// legs, higher between the towers and sagging deeper, so the street's flat paint stays far under the lens; slower
        /// round a corner, because the narrower street's fillet is tighter, and faster at the bottom of a sag.
        /// </summary>
        internal static readonly Style Neon = new(TopHeight: 92f, Dip: 22f, TopSpeed: 22f, Gravity: 52f,
            LegBlocksMin: 1, LegBlocksMax: 2, FinalBlocks: 1.2f, ReleaseRunMin: 24f, ReleaseRunMax: 36f, WideLens: 1.35f,
            Corners: 3, ReleaseTimeScale: 2.2f);

        /// <summary>
        /// The swing home through <paramref name="city"/>, landing on <paramref name="endPosition"/> looking at
        /// <paramref name="endTarget"/> with the game's <paramref name="fieldOfView"/>; null when no rolled route is clear.
        /// </summary>
        public static IntroShot Build(City city, Vector3 endPosition, Vector3 endTarget, float fieldOfView, Random random, Style style)
        {
            if (city == null) return null;

            var towers = new Towers(city);
            Route best = null;
            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                Route route = Lay(city, towers, endPosition, endTarget, random, style);
                if (route != null && (best == null || route.Score > best.Score)) best = route;
            }

            return best == null ? null : Fly(best, endPosition, endTarget, fieldOfView, style);
        }

        //One candidate, laid out in full: the dense path, where its last sag bottoms out (the release's climb starts
        //there), the height of the swings' tops, and its score
        private sealed class Route
        {
            public List<Vector3> Path;
            public int Bottom;
            public float Top;
            public float Score;
        }

        private static Route Lay(City city, Towers towers, Vector3 endPosition, Vector3 endTarget, Random random, Style style)
        {
            float pitch = city.BlockPitch;
            Vector2 end = new(endPosition.X, endPosition.Z);
            Vector2 facing = new(endTarget.X - endPosition.X, endTarget.Z - endPosition.Z);
            if (facing.LengthSquared() < 1e-6f) return null;
            facing.Normalize();

            //The last street runs the way the pose faces along whichever axis is rolled, weighted by how much of the facing
            //lies along it, so a pose facing along Z mostly comes in down a Z street — but never less than CROSS_STREET_SHARE
            //of the time down a cross street. ⚠ A level opens with the gun on an axis (bearing 90), and in the neon city no
            //street along the facing can curve onto a pose 30 out on the axis: its streets run 12.5 either side of it and the
            //clearing ends 41 out, so the release had 11 units to move 12 across. Down the cross street that is the
            //boundary of the clearing, it comes in along the edge and swivels onto the pose. Where the facing has nothing
            //along the rolled axis, which side the lens comes in from is rolled too.
            float alongZShare = MathF.Abs(facing.Y) / (MathF.Abs(facing.X) + MathF.Abs(facing.Y));
            bool alongZ = random.NextDouble() < MathHelper.Clamp(alongZShare, CROSS_STREET_SHARE, 1f - CROSS_STREET_SHARE);
            float component = alongZ ? facing.Y : facing.X;
            float heading = MathF.Abs(component) > 0.15f ? MathF.Sign(component) : (random.Next(2) == 0 ? 1f : -1f);
            Vector2 d = alongZ ? new Vector2(0f, heading) : new Vector2(heading, 0f);
            Vector2 across = new(-d.Y, d.X);

            //Its line: the street nearest the pose across the travel, or now and then the next one over
            float lateral = Vector2.Dot(end, across);
            float line = (MathF.Round(lateral / pitch - 0.5f) + 0.5f) * pitch;
            if (random.NextDouble() < 0.3) line += MathF.Sign(lateral - line) * pitch;

            //The release begins this far behind the pose along the street; the last corner the final blocks behind that,
            //on an intersection
            float endAlong = Vector2.Dot(end, d);
            float releaseAlong = endAlong - AridIntroPaths.Roll(random, style.ReleaseRunMin, style.ReleaseRunMax);
            float cornerAlong = (MathF.Round((releaseAlong - style.FinalBlocks * pitch) / pitch - 0.5f) + 0.5f) * pitch;
            if (releaseAlong - cornerAlong < style.FinalBlocks * pitch * 0.75f) cornerAlong -= pitch;

            Vector2 release = d * releaseAlong + across * line;
            Vector2 lastCorner = d * cornerAlong + across * line;

            //Back from the last corner the legs alternate, across into it, down a street parallel to the last into the one
            //before, and so on: a staircase that keeps closing on the arena. The leg into each corner, the last corner first.
            Vector2 side = random.Next(2) == 0 ? across : -across;
            int count = Math.Max(style.Corners, 1);
            var corners = new Vector2[count];
            var inbound = new Vector2[count];
            Vector2 start = lastCorner;
            for (int k = count - 1; k >= 0; k--)
            {
                inbound[k] = (count - 1 - k) % 2 == 0 ? side : d;
                corners[k] = start;
                start -= inbound[k] * (random.Next(style.LegBlocksMin, style.LegBlocksMax + 1) * pitch);
            }

            //Inside the city and out of the clearing until the last street
            float edge = (city.RadiusBlocks - 1) * pitch;
            float radius = Fillet(city.StreetWidth);
            for (int k = -1; k < count; k++)
            {
                Vector2 p = k < 0 ? start : corners[k];
                float ring = MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y));
                if (ring > edge || ring < 2f * pitch) return null;
            }
            if (Vector2.Distance(start, corners[0]) < radius + 4f || Vector2.Distance(lastCorner, release) < radius + 4f) return null;

            //The plan, cut fine: straight and fillet for every corner, the last street, then the curve onto the pose; the
            //length at each corner's middle is the top of a swing
            var plan = new List<Vector2>();
            var tops = new float[count];
            float laid = Straight(plan, start, corners[0] - inbound[0] * radius);
            for (int k = 0; k < count; k++)
            {
                Vector2 outbound = k + 1 < count ? inbound[k + 1] : d;
                tops[k] = laid + Arc(plan, corners[k], inbound[k], outbound, radius) * 0.5f;
                laid = Straight(plan, corners[k] + outbound * radius, k + 1 < count ? corners[k + 1] - inbound[k + 1] * radius : release);
            }
            int releaseStart = plan.Count - 1;

            //Which way the lens moves as it arrives: along the pose's facing, so it comes in towards what it will look
            //at, or halfway, or still along the street with the view swivelling onto the pose as it slows. A pose just
            //inside the clearing's edge on a bearing no street runs along can only be reached the last way.
            double arrival = random.NextDouble();
            Vector2 arrive = arrival < 0.5 ? facing : arrival < 0.8 ? Vector2.Normalize(d + facing) : d;
            Release(plan, release, d, end, arrive);
            float total = PlanLength(plan);

            //No tighter onto the pose than twice a corner's fillet: the curve's radius, read over every few of its points. A corner is
            //the top of a swing and taken slowly; the release begins at the bottom of a sag, at twice the speed, and a
            //curve as tight as a corner turned the view 170 degrees a second there
            for (int i = releaseStart + 2; i + 2 < plan.Count; i += 2)
            {
                Vector2 a = plan[i] - plan[i - 2], b = plan[i + 2] - plan[i];
                float turned = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(b.Y, b.X) - MathF.Atan2(a.Y, a.X)));
                if (turned > 1e-4f && (a.Length() + b.Length()) * 0.5f / turned < 2f * radius) return null;
            }

            //The heights: the corners at the tops of the swings, a sag between them, and the climb out onto the pose
            float ground = city.GroundY;
            float top = ground + style.TopHeight;
            float sLast = tops[count - 1];
            float sBottom = sLast + (total - sLast) * RELEASE_BOTTOM_SHARE;

            //The last sag is half of one (a top down to a bottom), so it is held to the depth a whole sag twice its length may have
            float releaseDip = MathF.Min(style.Dip, 2f * (sBottom - sLast) * DIP_PER_LENGTH), bottom = top - releaseDip;

            var path = new List<Vector3>(plan.Count);
            float s = 0f;
            int bottomIndex = -1;
            for (int i = 0; i < plan.Count; i++)
            {
                if (i > 0) s += Vector2.Distance(plan[i - 1], plan[i]);
                if (bottomIndex < 0 && s >= sBottom) bottomIndex = i;

                //Every piece level at both its ends, so the swings meet over a corner without a kink: a sine sag meets the
                //next at an angle, and the look, which follows half the climb, pitched 200 degrees a second over it
                float y;
                if (s <= tops[0]) y = top - style.Dip * 0.5f * (1f + MathF.Cos(MathHelper.Pi * s / tops[0]));
                else if (s <= sLast)
                {
                    int j = 0;
                    while (s > tops[j + 1]) j++;
                    float span = tops[j + 1] - tops[j];
                    float dip = MathF.Min(style.Dip, span * DIP_PER_LENGTH);
                    y = top - dip * 0.5f * (1f - MathF.Cos(MathHelper.TwoPi * (s - tops[j]) / span));
                }
                else if (s <= sBottom) y = top - releaseDip * 0.5f * (1f - MathF.Cos(MathHelper.Pi * (s - sLast) / (sBottom - sLast)));
                else y = MathHelper.Lerp(bottom, endPosition.Y, MathHelper.SmoothStep(0f, 1f, (s - sBottom) / (total - sBottom)));

                path.Add(new Vector3(plan[i].X, y, plan[i].Y));
            }
            path[^1] = endPosition;

            foreach (Vector3 p in path)
                if (!towers.Clear(p)) return null;

            //Walled in: the built blocks either side of every leg, read every block's length
            int walled = 0;
            for (int i = 0; i < plan.Count; i += (int)(pitch / STEP))
            {
                Vector2 p = plan[i];
                Vector2 dir = Vector2.Normalize(plan[Math.Min(i + 1, plan.Count - 1)] - plan[Math.Max(i - 1, 0)]);
                Vector2 n = new Vector2(-dir.Y, dir.X) * (pitch * 0.5f);
                walled += (Built(city, p + n) ? 1 : 0) + (Built(city, p - n) ? 1 : 0);
            }

            //And the turns onto the pose: the less of the view's swivel left for the landing, and the less of a curve
            //off the street, the better
            float swivel = MathF.Acos(MathHelper.Clamp(Vector2.Dot(arrive, facing), -1f, 1f));
            float curve = MathF.Acos(MathHelper.Clamp(Vector2.Dot(d, arrive), -1f, 1f));

            return new Route
            {
                Path = path, Bottom = bottomIndex, Top = top,
                Score = walled - 4f * swivel - 2f * curve + (float)random.NextDouble() * 0.5f,
            };
        }

        //The shot off a laid route: timed by the pendulum's speed, then the look, the bank and the lens
        private static IntroShot Fly(Route route, Vector3 endPosition, Vector3 endTarget, float fieldOfView, Style style)
        {
            List<Vector3> path = route.Path;
            float top = route.Top;
            int releaseFrom = route.Bottom;

            //The time at each point: the pendulum's speed through the swing, then a cubic to rest over the release
            var length = new float[path.Count];
            for (int i = 1; i < path.Count; i++) length[i] = length[i - 1] + Vector3.Distance(path[i - 1], path[i]);

            var time = new float[path.Count];
            for (int i = 1; i <= releaseFrom; i++)
            {
                float v = MathF.Sqrt(style.TopSpeed * style.TopSpeed + 2f * style.Gravity * MathF.Max(top - path[i].Y, 0f));
                time[i] = time[i - 1] + (length[i] - length[i - 1]) / v;
            }

            //The release: distance along it on the cubic x(τ) = v0·T·h10 + L·h01, entering at the bottom's speed, at rest on the pose
            float v0 = MathF.Sqrt(style.TopSpeed * style.TopSpeed + 2f * style.Gravity * MathF.Max(top - path[releaseFrom].Y, 0f));
            float releaseLength = length[^1] - length[releaseFrom];
            float releaseSeconds = style.ReleaseTimeScale * releaseLength / v0;
            float seconds = time[releaseFrom] + releaseSeconds;

            var positions = new Vector3[POINTS];
            for (int k = 0; k < POINTS; k++)
            {
                float t = seconds * k / (POINTS - 1);
                float at;
                if (t <= time[releaseFrom]) at = LengthAtTime(time, length, releaseFrom, t);
                else
                {
                    float u = (t - time[releaseFrom]) / releaseSeconds;
                    float h10 = u * u * u - 2f * u * u + u, h01 = -2f * u * u * u + 3f * u * u;
                    at = length[releaseFrom] + v0 * releaseSeconds * h10 + releaseLength * h01;
                }

                positions[k] = PointAtLength(path, length, at);
            }
            positions[^1] = endPosition;

            //The look: along the travel, a little ahead and following half its rise and fall, easing onto the pose's own
            //look over the last seconds; the bank off the heading's rate of turn, levelling out as the look lands
            float dt = seconds / (POINTS - 1);
            var looks = new Vector3[POINTS];
            var heading = new float[POINTS];
            var land = new float[POINTS];

            //The path's own direction at each point, half its climb kept, then averaged over the window
            var direction = new Vector3[POINTS];
            for (int k = 0; k < POINTS; k++)
            {
                Vector3 tangent = positions[Math.Min(k + 1, POINTS - 1)] - positions[Math.Max(k - 1, 0)];
                if (tangent.LengthSquared() < 1e-8f) tangent = k > 0 ? direction[k - 1] : endTarget - positions[k];
                tangent.Normalize();
                tangent.Y *= LOOK_PITCH_FOLLOW;
                direction[k] = tangent;
            }

            int window = Math.Max(1, (int)MathF.Round(LOOK_SMOOTH_SECONDS / dt));
            direction = BoxBlur(direction, window);

            for (int k = 0; k < POINTS; k++)
            {
                Vector3 travel = direction[k];
                if (travel.LengthSquared() < 1e-8f) travel = endTarget - positions[k];
                travel.Normalize();

                heading[k] = MathF.Atan2(travel.Z, travel.X);
                land[k] = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((k * dt - (seconds - LOOK_LAND_SECONDS)) / LOOK_LAND_SECONDS, 0f, 1f));
                looks[k] = Vector3.Lerp(positions[k] + travel * LOOK_DISTANCE, endTarget, land[k]);
            }
            looks[^1] = endTarget;

            var roll = new float[POINTS];
            float maxRoll = MathHelper.ToRadians(MAX_ROLL_DEGREES);
            for (int k = 0; k < POINTS; k++)
            {
                int a = Math.Max(k - 1, 0), b = Math.Min(k + 1, POINTS - 1);
                float turn = MathHelper.WrapAngle(heading[b] - heading[a]) / ((b - a) * dt);
                roll[k] = MathHelper.Clamp(ROLL_PER_TURN * turn, -maxRoll, maxRoll);
            }

            int rollWindow = Math.Max(1, (int)MathF.Round(ROLL_SMOOTH_SECONDS / dt));
            for (int pass = 0; pass < 3; pass++) roll = BoxBlur(roll, rollWindow);
            for (int k = 0; k < POINTS; k++) roll[k] *= 1f - land[k];
            roll[^1] = 0f;

            return new IntroShot("the swing home", positions, seconds, fieldOfView * style.WideLens, lookAtPath: looks,
                rollPath: roll, endFieldOfView: fieldOfView, lands: true);
        }

        /// <summary>
        /// The fillet that turns a street corner as wide as the inner block allows: a quarter circle of radius <c>r</c>
        /// between two perpendicular centre lines passes the inner block's corner, half a street from each line, at
        /// <c>r(1 − √2) + √2·w/2</c>, so the widest that keeps <see cref="CORNER_CLEARANCE"/> is solved for, and capped
        /// at the street's width.
        /// </summary>
        internal static float Fillet(float streetWidth) =>
            MathF.Min((MathF.Sqrt(2f) * streetWidth * 0.5f - CORNER_CLEARANCE) / (MathF.Sqrt(2f) - 1f), streetWidth);

        /// <summary>
        /// The city's boxes by block, for asking whether a point stands clear of them: a box stands inside its block
        /// (<see cref="City"/>), so a point is asked only about the boxes of its own block and the eight round it.
        /// </summary>
        internal sealed class Towers
        {
            private readonly City _city;
            private readonly Dictionary<(int, int), List<int>> _byBlock = new();

            public Towers(City city)
            {
                _city = city;
                for (int b = 0; b < city.Buildings.Length; b++)
                {
                    Matrix box = city.Buildings[b].World;
                    var key = ((int)MathF.Round(box.M41 / city.BlockPitch), (int)MathF.Round(box.M43 / city.BlockPitch));
                    if (!_byBlock.TryGetValue(key, out List<int> list)) _byBlock[key] = list = new List<int>();
                    list.Add(b);
                }
            }

            /// <summary>Whether <paramref name="point"/> stands more than <paramref name="clearance"/> from every tower's and
            /// cornice's box, and off the island.</summary>
            public bool Clear(Vector3 point, float clearance = CLEARANCE)
            {
                float rim = new Vector2(point.X, point.Z).Length();
                if (rim < ArenaIsland.RADIUS + clearance && point.Y < ArenaIsland.TOP_Y + clearance) return false;

                int bx = (int)MathF.Round(point.X / _city.BlockPitch), bz = (int)MathF.Round(point.Z / _city.BlockPitch);
                for (int i = bx - 1; i <= bx + 1; i++)
                    for (int j = bz - 1; j <= bz + 1; j++)
                    {
                        if (!_byBlock.TryGetValue((i, j), out List<int> boxes)) continue;

                        foreach (int b in boxes)
                        {
                            Matrix box = _city.Buildings[b].World;
                            float top = box.M42 + box.M22 * 0.5f + (b < _city.TowerCount ? _city.RoofRise(b) : 0f) + clearance;
                            if (point.Y > top || point.Y < box.M42 - box.M22 * 0.5f - clearance) continue;

                            if (MathF.Abs(point.X - box.M41) < box.M11 * 0.5f + clearance
                                && MathF.Abs(point.Z - box.M43) < box.M33 * 0.5f + clearance) return false;
                        }
                    }

                return true;
            }
        }

        //A straight run, cut fine; the first point only if the plan is empty. Returns the plan's length after it.
        private static float Straight(List<Vector2> plan, Vector2 from, Vector2 to)
        {
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(from, to) / STEP));
            for (int i = plan.Count == 0 ? 0 : 1; i <= steps; i++) plan.Add(Vector2.Lerp(from, to, i / (float)steps));
            return PlanLength(plan);
        }

        //A quarter-circle fillet round an intersection, from heading `inbound` to heading `outbound`. Returns its length.
        private static float Arc(List<Vector2> plan, Vector2 corner, Vector2 inbound, Vector2 outbound, float radius)
        {
            Vector2 centre = corner - inbound * radius + outbound * radius;
            float length = MathHelper.PiOver2 * radius;
            int steps = Math.Max(2, (int)MathF.Ceiling(length / STEP));

            for (int i = 1; i <= steps; i++)
            {
                float theta = MathHelper.PiOver2 * i / steps;
                plan.Add(centre + radius * (-outbound * MathF.Cos(theta) + inbound * MathF.Sin(theta)));
            }

            return length;
        }

        //The curve off the street's line onto the pose: a cubic Bezier leaving along the street and arriving along
        //`arrive`
        private static void Release(List<Vector2> plan, Vector2 from, Vector2 heading, Vector2 to, Vector2 arrive)
        {
            float reach = Vector2.Distance(from, to);
            Vector2 c1 = from + heading * (reach * 0.45f), c2 = to - arrive * (reach * 0.35f);

            //Cut finely by parameter, then thinned to STEP so the plan's spacing stays even
            Vector2 last = plan[^1];
            for (int i = 1; i <= 400; i++)
            {
                float u = i / 400f, v = 1f - u;
                Vector2 p = v * v * v * from + 3f * v * v * u * c1 + 3f * v * u * u * c2 + u * u * u * to;
                if (Vector2.Distance(p, last) >= STEP || i == 400) { plan.Add(p); last = p; }
            }
        }

        private static float PlanLength(List<Vector2> plan)
        {
            float length = 0f;
            for (int i = 1; i < plan.Count; i++) length += Vector2.Distance(plan[i - 1], plan[i]);
            return length;
        }

        private static float LengthAtTime(float[] time, float[] length, int last, float t)
        {
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (time[mid] <= t) lo = mid; else hi = mid;
            }

            float span = time[hi] - time[lo];
            return span > 1e-6f ? MathHelper.Lerp(length[lo], length[hi], (t - time[lo]) / span) : length[lo];
        }

        private static Vector3 PointAtLength(List<Vector3> path, float[] length, float at)
        {
            int lo = 0, hi = length.Length - 1;
            if (at <= 0f) return path[0];
            if (at >= length[hi]) return path[hi];

            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (length[mid] <= at) lo = mid; else hi = mid;
            }

            float span = length[hi] - length[lo];
            return span > 1e-6f ? Vector3.Lerp(path[lo], path[hi], (at - length[lo]) / span) : path[lo];
        }

        private static Vector3[] BoxBlur(Vector3[] values, int half)
        {
            var result = new Vector3[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                Vector3 sum = Vector3.Zero;
                for (int j = Math.Max(0, i - half); j <= Math.Min(values.Length - 1, i + half); j++) sum += values[j];
                result[i] = sum;
            }

            return result;
        }

        private static float[] BoxBlur(float[] values, int half)
        {
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int j = Math.Max(0, i - half); j <= Math.Min(values.Length - 1, i + half); j++) { sum += values[j]; count++; }
                result[i] = sum / count;
            }

            return result;
        }

        private static bool Built(City city, Vector2 position)
        {
            int r = city.RadiusBlocks;
            int bx = (int)MathF.Round(position.X / city.BlockPitch), bz = (int)MathF.Round(position.Y / city.BlockPitch);
            if (bx < -r || bx > r || bz < -r || bz > r) return false;

            return city.BlockBuilt[(bz + r) * (2 * r + 1) + bx + r];
        }
    }
}
