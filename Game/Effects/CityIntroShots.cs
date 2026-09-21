using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The city's own shots for a chapter intro's prologue (#488, and #433 inside it): a pass low down a
    /// street, a swing round a tower corner between the facades, a crane up over a plaza and a skim across
    /// the roofs with their masts and blinking beacons (#436) — cut together, then cut to the ordinary tour. Both cities have spent a whole issue's worth of detail on the street
    /// level (#399: lane lines, crossings, parked cars, plazas of trees, and at night the sodium lamps and the
    /// shops' neon) that no camera in the game had ever gone down to, and the tour cannot take it there: it is
    /// a spline round the arena, and the streets are ninety units under the island, between towers.
    /// <para>
    /// <b>Every path runs down the middle of a street, and that is what keeps it out of the towers.</b> The
    /// city stands its buildings inside their blocks (<see cref="City"/>: a building never reaches past the
    /// buildable square, and the square stops half a street short of the centre line), so any height over a
    /// street's centre line is clear. The one place a path leaves a centre line is the corner of the swing,
    /// and its fillet radius is chosen for that (see <see cref="Swing"/>).
    /// </para>
    /// <para>
    /// <b>The street's paint is flat</b> — the cars, the lane lines and the plaza's trees are drawn onto the
    /// ground from above by <c>CityStreets.fx</c>, not modelled — so the low pass looks DOWN the street rather
    /// than level along it, and the plaza is shot from above: edge-on they would read as flat paint or not at
    /// all (the trap #488 itself records).
    /// </para>
    /// <para>
    /// <b>Chosen from the city's own grid</b> (<see cref="City.BlockBuilt"/>): the street is the deepest canyon
    /// of a few rolled candidates, the swing turns round a block that has a tower on it, the plaza is a block
    /// the generator left open. Built once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class CityIntroShots
    {
        //The low pass: how high over the asphalt (a lens at eye height over a car roof), how far down the
        //street it starts and how far it travels, how steeply it looks down the street — steep enough that
        //the painted cars and crossings lie in the lower half of the frame, shallow enough that the towers
        //still rise up the rest of it.
        private const float STREET_HEIGHT = 5.5f;
        private const float STREET_START_BLOCKS = 6.5f, STREET_START_JITTER = 2f;
        private const float STREET_RUN_BLOCKS = 1.6f;
        private const float STREET_PITCH_DEGREES = 13f;
        private const float STREET_SECONDS = 3.6f;

        //The swing (#433's "like Spider-Man"): a run down one street, round the corner, off down the next,
        //dipping on the way in and climbing out — the lowest point at the corner, as a swing's is.
        private const float SWING_HEIGHT = 46f, SWING_DIP = 14f, SWING_CLIMB = 10f;
        private const float SWING_LEG_BLOCKS = 1.4f;
        private const float SWING_SECONDS = 3.0f;

        //The crane: from over the plaza's trees up to above the lower roofs, looking down at them. ⚠ Not
        //lower: the trees are discs painted on the paving, and from 16 units over them the first cut read them
        //as holes in the plaza. From twice that they are a park's canopy among the streets and cars round it.
        private const float PLAZA_FROM_HEIGHT = 30f, PLAZA_TO_HEIGHT = 58f;
        private const float PLAZA_SECONDS = 3.0f;

        //The roofs: down the middle of a street at the height of the roofs either side of it, so the masts,
        //dishes and beacons pass on both hands. Over a street's centre line nothing stands at any height (see
        //the class doc), so the lens needs no clearance over the equipment and can skim it.
        private const float ROOFS_ABOVE_MEDIAN = 4f;
        private const float ROOFS_RUN_BLOCKS = 2f;
        private const float ROOFS_PITCH_DEGREES = 22f;
        private const float ROOFS_SECONDS = 3.2f;

        //How many candidates each roll weighs; the best by its own measure is taken.
        private const int CANDIDATES = 12;

        //Points per path. Enough that a fillet of a quarter circle is smooth at the lens; cheap either way.
        private const int PATH_POINTS = 96;

        /// <summary>
        /// The prologue for <paramref name="city"/>, or null when there is no city to shoot.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(City city, float fieldOfView, Random random)
        {
            if (city == null) return null;

            var shots = new List<IntroShot>(3) { Street(city, fieldOfView, random) };

            IntroShot swing = Swing(city, fieldOfView, random);
            if (swing != null) shots.Add(swing);

            IntroShot plaza = Plaza(city, fieldOfView, random);
            if (plaza != null) shots.Add(plaza);

            //Last, because it is the highest: the cut from it to the tour's opening look down the canyon is
            //the smallest jump of height the reel makes.
            shots.Add(Roofs(city, fieldOfView, random));

            return shots.ToArray();
        }

        /// <summary>
        /// The low pass: down the middle of a street towards the centre, a lens over a car roof. Of a few
        /// rolled streets the one flanked by the most built blocks is taken — the deepest canyon — because a
        /// street with a plaza on each side is not the city the owner asked to see.
        /// </summary>
        private static IntroShot Street(City city, float fieldOfView, Random random)
        {
            float pitch = city.BlockPitch;
            int bestScore = -1;
            Vector3 from = Vector3.Zero, to = Vector3.Zero;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                bool alongZ = random.Next(2) == 0;
                int line = random.Next(-5, 5);                      //the street between blocks line and line + 1
                int side = random.Next(2) == 0 ? 1 : -1;            //which end of the street it comes in from
                float start = side * (STREET_START_BLOCKS + STREET_START_JITTER * (float)random.NextDouble()) * pitch;
                float end = start - side * STREET_RUN_BLOCKS * pitch;
                float lane = (random.Next(2) == 0 ? 1 : -1) * city.StreetWidth * 0.18f;

                int score = 0;
                int firstRow = (int)MathF.Round(MathF.Min(start, end) / pitch);
                int lastRow = (int)MathF.Round(MathF.Max(start, end) / pitch);

                for (int row = firstRow; row <= lastRow; row++)
                    score += (Built(city, alongZ, line, row) ? 1 : 0) + (Built(city, alongZ, line + 1, row) ? 1 : 0);

                if (score <= bestScore) continue;

                bestScore = score;
                float across = (line + 0.5f) * pitch + lane;
                float y = city.GroundY + STREET_HEIGHT;
                from = alongZ ? new Vector3(across, y, start) : new Vector3(start, y, across);
                to = alongZ ? new Vector3(across, y, end) : new Vector3(end, y, across);
            }

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++) path[i] = Vector3.Lerp(from, to, i / (float)(PATH_POINTS - 1));

            return new IntroShot("the street", path, STREET_SECONDS, fieldOfView * 1.1f,
                lookAhead: 20f, pitchDownDegrees: STREET_PITCH_DEGREES);
        }

        /// <summary>
        /// The swing round a tower corner (#433): along one street, a quarter turn at an intersection, and off
        /// along the cross street, dipping into the corner and climbing out.
        /// <para>
        /// <b>⚠ The corner is a fillet, and its radius is what keeps the lens out of the block it turns
        /// round.</b> A fillet of radius <c>r</c> between two perpendicular centre lines passes the inner
        /// block's corner — half a street from each line — at <c>r(1 − √2) + √2·w/2</c>, which falls as the
        /// radius grows: at <c>r</c> = 0.75 of the street width it is 3.6 units in the day city (9 wide) and 3.0
        /// in the neon one (7.5), against a building that stands at least 0.4 inside its block. A spline
        /// through the two streets and the intersection instead would cut deep into that block.
        /// </para>
        /// </summary>
        private static IntroShot Swing(City city, float fieldOfView, Random random)
        {
            float pitch = city.BlockPitch;
            float radius = city.StreetWidth * 0.75f;
            float leg = SWING_LEG_BLOCKS * pitch;

            int bestScore = -1;
            Vector2 corner = Vector2.Zero, inbound = Vector2.Zero, outbound = Vector2.Zero;

            for (int attempt = 0; attempt < CANDIDATES; attempt++)
            {
                //An intersection three to six blocks out: past the clearing the island stands in, and not so
                //far that the towers have tapered away to nothing.
                int i = random.Next(-6, 6), j = random.Next(-6, 6);
                Vector2 at = new((i + 0.5f) * pitch, (j + 0.5f) * pitch);
                float ring = MathF.Max(MathF.Abs(at.X), MathF.Abs(at.Y)) / pitch;
                if (ring < 3f || ring > 6.5f) continue;

                Vector2 d1 = random.Next(4) switch { 0 => Vector2.UnitX, 1 => -Vector2.UnitX, 2 => Vector2.UnitY, _ => -Vector2.UnitY };
                Vector2 d2 = random.Next(2) == 0 ? new Vector2(-d1.Y, d1.X) : new Vector2(d1.Y, -d1.X);

                //The block the swing turns round sits in the quadrant behind the inbound leg and towards the
                //outbound one, and it must have a tower on it — swinging round a plaza is not a swing.
                Vector2 innerBlock = at + (-d1 + d2) * (pitch * 0.5f);
                if (!BuiltAt(city, innerBlock)) continue;

                //Scored by how walled-in both legs are: the blocks either side of each leg's own street.
                int score = 0;
                for (float s = pitch * 0.5f; s <= leg; s += pitch)
                {
                    Vector2 before = at - d1 * s, after = at + d2 * s;
                    Vector2 n1 = new Vector2(-d1.Y, d1.X) * (pitch * 0.5f), n2 = new Vector2(-d2.Y, d2.X) * (pitch * 0.5f);
                    score += (BuiltAt(city, before + n1) ? 1 : 0) + (BuiltAt(city, before - n1) ? 1 : 0)
                        + (BuiltAt(city, after + n2) ? 1 : 0) + (BuiltAt(city, after - n2) ? 1 : 0);
                }

                if (score <= bestScore) continue;

                bestScore = score;
                corner = at;
                inbound = d1;
                outbound = d2;
            }

            if (bestScore < 0) return null;

            //The plan: straight in to one radius short of the intersection, a quarter circle round the fillet
            //centre, straight out. Laid out by length so the points come out evenly spaced.
            float straight = leg - radius;
            float arc = MathHelper.PiOver2 * radius;
            float total = 2f * straight + arc;
            Vector2 filletCentre = corner - inbound * radius + outbound * radius;

            var path = new Vector3[PATH_POINTS];
            for (int k = 0; k < PATH_POINTS; k++)
            {
                float u = k / (float)(PATH_POINTS - 1);
                float s = u * total;
                Vector2 plan;

                if (s < straight) plan = corner - inbound * leg + inbound * s;
                else if (s < straight + arc)
                {
                    float theta = (s - straight) / radius;
                    plan = filletCentre + radius * (-outbound * MathF.Cos(theta) + inbound * MathF.Sin(theta));
                }
                else plan = corner + outbound * (radius + (s - straight - arc));

                float y = city.GroundY + SWING_HEIGHT - SWING_DIP * MathF.Sin(MathHelper.Pi * u) + SWING_CLIMB * u;
                path[k] = new Vector3(plan.X, y, plan.Y);
            }

            return new IntroShot("the swing", path, SWING_SECONDS, fieldOfView * 1.2f,
                lookAhead: 14f, pitchDownDegrees: 4f);
        }

        /// <summary>
        /// The crane over a plaza: a block the generator left open, three to seven out, shot from above as it
        /// rises — its trees are painted onto the paving, so they read only from over them.
        /// </summary>
        private static IntroShot Plaza(City city, float fieldOfView, Random random)
        {
            float pitch = city.BlockPitch;
            var open = new List<Point>();

            for (int bx = -7; bx <= 7; bx++)
                for (int bz = -7; bz <= 7; bz++)
                {
                    int ring = Math.Max(Math.Abs(bx), Math.Abs(bz));
                    if (ring >= 3 && !Built(city, true, bx, bz)) open.Add(new Point(bx, bz));
                }

            if (open.Count == 0) return null;

            Point block = open[random.Next(open.Count)];
            Vector3 centre = new(block.X * pitch, city.GroundY, block.Y * pitch);

            //Off to one side of the plaza's middle and drifting across it while it rises, so the crane is a
            //move and not a lift; well inside the plaza's own square, where nothing stands.
            float angle = (float)random.NextDouble() * MathHelper.TwoPi;
            Vector3 drift = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (pitch * 0.2f);

            Vector3 from = centre + drift + Vector3.Up * PLAZA_FROM_HEIGHT;
            Vector3 to = centre - drift * 0.6f + Vector3.Up * PLAZA_TO_HEIGHT;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++) path[i] = Vector3.Lerp(from, to, i / (float)(PATH_POINTS - 1));

            return new IntroShot("the plaza", path, PLAZA_SECONDS, fieldOfView, lookAt: centre - drift * 0.3f);
        }

        /// <summary>
        /// The skim across the roofs: down the middle of a street four to six blocks out, at the height of the
        /// roofs either side of it — the median of the towers standing along the run, so a lone spire does not
        /// lift the lens clear of everything else — looking down on the equipment that #436 put there.
        /// </summary>
        private static IntroShot Roofs(City city, float fieldOfView, Random random)
        {
            float pitch = city.BlockPitch;
            bool alongZ = random.Next(2) == 0;
            int line = (random.Next(2) == 0 ? 1 : -1) * (4 + random.Next(2));
            int side = random.Next(2) == 0 ? 1 : -1;
            float start = side * (3f + 2f * (float)random.NextDouble()) * pitch;
            float end = start - side * ROOFS_RUN_BLOCKS * pitch;
            float across = (line + 0.5f) * pitch;

            //The tops of the towers standing along the run, either side of the street.
            var tops = new List<float>();
            float low = MathF.Min(start, end) - pitch, high = MathF.Max(start, end) + pitch;

            foreach (ModelInstance building in city.Buildings)
            {
                Matrix world = building.World;
                float x = alongZ ? world.M41 : world.M43;
                float along = alongZ ? world.M43 : world.M41;

                if (MathF.Abs(x - across) < pitch && along >= low && along <= high)
                    tops.Add(world.M42 + world.M22 * 0.5f);
            }

            tops.Sort();
            float roofline = tops.Count > 0 ? tops[tops.Count / 2] : city.GroundY + 60f;
            float y = roofline + ROOFS_ABOVE_MEDIAN;

            Vector3 from = alongZ ? new Vector3(across, y, start) : new Vector3(start, y, across);
            Vector3 to = alongZ ? new Vector3(across, y + 6f, end) : new Vector3(end, y + 6f, across);

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++) path[i] = Vector3.Lerp(from, to, i / (float)(PATH_POINTS - 1));

            return new IntroShot("the roofs", path, ROOFS_SECONDS, fieldOfView * 1.1f,
                lookAhead: 30f, pitchDownDegrees: ROOFS_PITCH_DEGREES);
        }

        //Whether the block on grid row/column (line, row) carries a tower, in the orientation of a street
        //running along Z (line is the X index) or along X (line is the Z index). Off the grid is open.
        private static bool Built(City city, bool alongZ, int line, int row) =>
            alongZ ? BuiltBlock(city, line, row) : BuiltBlock(city, row, line);

        private static bool BuiltAt(City city, Vector2 position) =>
            BuiltBlock(city, (int)MathF.Round(position.X / city.BlockPitch), (int)MathF.Round(position.Y / city.BlockPitch));

        private static bool BuiltBlock(City city, int bx, int bz)
        {
            int r = city.RadiusBlocks;
            if (bx < -r || bx > r || bz < -r || bz > r) return false;

            return city.BlockBuilt[(bz + r) * (2 * r + 1) + bx + r];
        }
    }
}
