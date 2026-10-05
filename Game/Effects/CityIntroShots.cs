using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The city's own shots for a chapter intro's prologue (#488, and #433 inside it): a pass down a
    /// street from four storeys up, an orbit over a plaza from above, a swing round a tower corner between the facades and an
    /// arc round one dressed roof, its mast, dishes and beacon close and from above (#780; #436's equipment) — in that order, which alternates what the
    /// lens sees so that no cut lands on the same picture nudged (the owner's second-round note) — cut together,
    /// then cut to the ordinary tour. Both cities have spent a whole issue's worth of detail on the street
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
    /// ground from above by <c>CityStreets.fx</c>, not modelled — so the street pass looks DOWN the street rather
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
        //The street pass: how high over the asphalt, how far down the street it starts and how far it travels, how
        //steeply it looks down the street — steep enough that the painted cars and crossings lie in the lower half of
        //the frame, shallow enough that the towers still rise up the rest of it. ⚠ From above, not at a car's roof: the
        //cars, the lane lines and the crossings are paint on the ground, and the owner's verdict on the first cut (5.5
        //units up, 13 degrees down) was that a lens that low sees them flat - "I would look at it a little more from
        //above". Three storeys up and looking down, the paint is a plan of the street. And once more (#783, 2026-10-04):
        //from 18 units at 30 degrees the flat cars and markings still showed up close ("the detail on the streets is
        //missing, and looking this close, it shows"), so 26 at 36 - still under the swing's lowest point (46 - 14 = 32)
        //and the plaza's start (32), so the shot keeps its own height in the reel.
        private const float STREET_HEIGHT = 26f;
        private const float STREET_START_BLOCKS = 6.5f, STREET_START_JITTER = 2f;
        private const float STREET_RUN_BLOCKS = 2.0f;
        private const float STREET_PITCH_DEGREES = 36f;
        private const float STREET_SECONDS = 4.5f;

        //The swing (#433's "like Spider-Man"): a run down one street, round the corner, off down the next,
        //dipping on the way in and climbing out — the lowest point at the corner, as a swing's is.
        private const float SWING_HEIGHT = 46f, SWING_DIP = 14f, SWING_CLIMB = 10f;
        private const float SWING_LEG_BLOCKS = 1.8f;
        private const float SWING_SECONDS = 4.0f;

        //The orbit: round the plaza's trees while rising to above the lower roofs, looking down at them. ⚠ Not
        //lower: the trees are discs painted on the paving, and from 16 units over them the first cut read them
        //as holes in the plaza. From twice that they are a park's canopy among the streets and cars round it.
        private const float PLAZA_FROM_HEIGHT = 32f, PLAZA_TO_HEIGHT = 54f;
        private const float PLAZA_ORBIT_RADIUS_BLOCKS = 0.45f, PLAZA_ORBIT_DEGREES = 80f;
        private const float PLAZA_SECONDS = 4.0f;

        //The roofs: down the middle of a street at the height of the roofs either side of it, so the masts,
        //dishes and beacons pass on both hands. Over a street's centre line nothing stands at any height (see
        //the class doc), so the lens needs no clearance over the equipment and can skim it.
        private const float ROOFS_ABOVE_MEDIAN = 4f;
        private const float ROOFS_RUN_BLOCKS = 2.4f;
        private const float ROOFS_PITCH_DEGREES = 22f;
        private const float ROOFS_SECONDS = 5.5f;

        //The roofs' lens sways this far off the street's centre line and back as it goes (a street is 7.5–9 wide, so it
        //stays well inside it): a straight skim at a constant height reads as one long dolly, and the sway is what gives
        //the masts and dishes their parallax as they pass.
        private const float ROOFS_SWAY_UNITS = 2.2f;

        //The rooftop (#780): a slow arc round one dressed roof - its mast, dishes and beacon - close and from above, the
        //owner's "the camera could circle round them for a moment and look at them more from above". The roof is the
        //best-dressed one between these distances from the arena's axis (the island stands over the middle); the lens
        //circles it this far out past the roof's edge, this high over the mast's middle, looking at the mast's lower
        //third, through this many degrees. A roof whose arc a taller neighbour stands in is passed over for the next.
        private const float ROOFTOP_NEAREST_BLOCKS = 2f, ROOFTOP_FARTHEST_BLOCKS = 6f;
        private const float ROOFTOP_OUT = 9f;
        private const float ROOFTOP_ABOVE = 10f;
        private const float ROOFTOP_LOOK_SHARE = 0.3f;
        private const float ROOFTOP_SWEEP_DEGREES = 85f;
        private const float ROOFTOP_SECONDS = 5.0f;
        private const float ROOFTOP_CLEARANCE = 3f;
        private const int ROOFTOP_CANDIDATES = 8;

        //How many candidates each roll weighs; the best by its own measure is taken.
        private const int CANDIDATES = 12;

        //Points per path. Enough that a fillet of a quarter circle is smooth at the lens; cheap either way.
        private const int PATH_POINTS = IntroPaths.FINE_POINTS;

        /// <summary>
        /// The prologue for <paramref name="city"/>, or null when there is no city to shoot.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// <paramref name="rooftops"/> is the city's roof equipment, for the rooftop's showcase (#780), and
        /// <paramref name="neon"/> whether it is the neon city's, whose extra pieces count.
        /// </summary>
        public static IntroShot[] Build(City city, CityRooftops rooftops, bool neon, float fieldOfView, Random random)
        {
            if (city == null) return null;

            //The order alternates what the lens sees, so that no two shots in a row are a variation of one (#488's second
            //round: "a quick cut that looks at the square, and straight after it the square again from a very slightly
            //different angle — it looks like a glitch"): the street from the ground looking down it, the plaza from
            //above, the swing between the towers, the roofs over the top. The swing turns in from the OTHER axis than
            //the street ran along, so its walls do not stand where the street's did.
            var shots = new List<IntroShot>(4) { Street(city, fieldOfView, random, out bool streetAlongZ) };

            IntroShot plaza = Plaza(city, fieldOfView, random);
            if (plaza != null) shots.Add(plaza);

            IntroShot swing = Swing(city, fieldOfView, random, streetAlongZ);
            if (swing != null) shots.Add(swing);

            //Last, because it is the highest: the cut from it to the tour's opening look down the canyon is
            //the smallest jump of height the reel makes. Since #780 an arc round one dressed roof, close and from above,
            //where a roof can be found for it; the skim down a street at roof height where none can.
            shots.Add(Rooftop(city, rooftops, neon, fieldOfView, random) ?? Roofs(city, fieldOfView, random));

            return shots.ToArray();
        }

        /// <summary>
        /// The street pass: down the middle of a street towards the centre, four storeys up and looking down. Of a few
        /// rolled streets the one flanked by the most built blocks is taken — the deepest canyon — because a
        /// street with a plaza on each side is not the city the owner asked to see.
        /// </summary>
        private static IntroShot Street(City city, float fieldOfView, Random random, out bool streetAlongZ)
        {
            float pitch = city.BlockPitch;
            int bestScore = -1;
            Vector3 from = Vector3.Zero, to = Vector3.Zero;
            streetAlongZ = true;

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
                streetAlongZ = alongZ;
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
        private static IntroShot Swing(City city, float fieldOfView, Random random, bool streetAlongZ)
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

                //Coming in along the axis the low street did NOT run along (#488): a street along Z is answered by a swing
                //that starts along X, so the two shots' facades stand at different angles to the frame
                float sign = random.Next(2) == 0 ? 1f : -1f;
                Vector2 d1 = streetAlongZ ? new Vector2(sign, 0f) : new Vector2(0f, sign);
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
        /// The orbit over a plaza: a block the generator left open, three to seven out, shot from above as the lens
        /// arcs round it and rises — its trees are painted onto the paving, so they read only from over them.
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

            //An arc round the plaza while it rises, the lens kept on its middle (#488): the crane it was, a lift over one
            //spot with the view straight down, was seven seconds' worth of the same picture once the tour's own look
            //at the arena was cut in after it. Off the middle by PLAZA_ORBIT_RADIUS_BLOCKS of a block — over the plaza
            //and the streets round it, where nothing stands at any height (a building's square stops half a street
            //short of the centre line, so the free ground reaches a little over half a pitch from the middle) — so
            //the lens looks in at about two thirds of a right angle and the towers round the block lean into the frame.
            float startAngle = (float)random.NextDouble() * MathHelper.TwoPi;
            float sweep = MathHelper.ToRadians(PLAZA_ORBIT_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
            float orbit = pitch * PLAZA_ORBIT_RADIUS_BLOCKS;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float u = i / (float)(PATH_POINTS - 1);
                float angle = startAngle + sweep * u;
                path[i] = centre + new Vector3(MathF.Cos(angle) * orbit, MathHelper.Lerp(PLAZA_FROM_HEIGHT, PLAZA_TO_HEIGHT, u),
                    MathF.Sin(angle) * orbit);
            }

            return new IntroShot("the plaza", path, PLAZA_SECONDS, fieldOfView, lookAt: centre);
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

            //The towers alone: a cornice's top is its tower's roof and a parapet's height over it (City.TowerCount)
            for (int b = 0; b < city.TowerCount; b++)
            {
                Matrix world = city.Buildings[b].World;
                float x = alongZ ? world.M41 : world.M43;
                float along = alongZ ? world.M43 : world.M41;

                if (MathF.Abs(x - across) < pitch && along >= low && along <= high)
                    tops.Add(world.M42 + world.M22 * 0.5f + city.RoofRise(b));
            }

            tops.Sort();
            float roofline = tops.Count > 0 ? tops[tops.Count / 2] : city.GroundY + 60f;
            float y = roofline + ROOFS_ABOVE_MEDIAN;

            Vector3 from = alongZ ? new Vector3(across, y, start) : new Vector3(start, y, across);
            Vector3 to = alongZ ? new Vector3(across, y + 6f, end) : new Vector3(end, y + 6f, across);

            //Swaying across the street as it runs (ROOFS_SWAY_UNITS): one full period, so it starts and ends on the
            //centre line the tour's cut expects
            Vector3 sideways = alongZ ? Vector3.UnitX : Vector3.UnitZ;
            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float u = i / (float)(PATH_POINTS - 1);
                path[i] = Vector3.Lerp(from, to, u) + sideways * (ROOFS_SWAY_UNITS * MathF.Sin(MathHelper.TwoPi * u));
            }

            return new IntroShot("the roofs", path, ROOFS_SECONDS, fieldOfView * 1.1f,
                lookAhead: 30f, pitchDownDegrees: ROOFS_PITCH_DEGREES);
        }

        /// <summary>
        /// The rooftop (#780): an arc round the best-dressed roof in reach whose arc no other tower stands in, the lens
        /// over the mast's middle and outside the roof's edge, looking down at the equipment. The skim it replaced ran
        /// down a street at roof height, so the masts and dishes passed on both hands at speed and were never looked AT -
        /// the owner did not see the roofs shown in either city. Null when no roof is dressed or every arc is blocked.
        /// </summary>
        private static IntroShot Rooftop(City city, CityRooftops rooftops, bool neon, float fieldOfView, Random random)
        {
            if (rooftops == null) return null;

            float pitch = city.BlockPitch;
            CityRooftops.RoofShowcase[] roofs = rooftops.Showcase(neon, ROOFTOP_NEAREST_BLOCKS * pitch,
                ROOFTOP_FARTHEST_BLOCKS * pitch, ROOFTOP_CANDIDATES);

            foreach (CityRooftops.RoofShowcase roof in roofs)
            {
                float radius = roof.HalfSpan + ROOFTOP_OUT;
                float height = roof.Centre.Y + MathF.Max(roof.MastHeight, 4f) * 0.5f + ROOFTOP_ABOVE;
                Vector3 look = roof.Centre + Vector3.Up * (roof.MastHeight * ROOFTOP_LOOK_SHARE);

                //Two tries at the arc's start: from the side facing the arena first, which keeps the island behind the
                //roof rather than behind the lens, then a rolled one
                float facing = MathF.Atan2(-roof.Centre.Z, -roof.Centre.X);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    float from = attempt == 0 ? facing : (float)(random.NextDouble() * MathHelper.TwoPi);
                    float sweep = MathHelper.ToRadians(ROOFTOP_SWEEP_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);

                    var path = new Vector3[PATH_POINTS];
                    bool clear = true;
                    for (int i = 0; i < PATH_POINTS && clear; i++)
                    {
                        float u = i / (float)(PATH_POINTS - 1);
                        float angle = from - sweep * 0.5f + sweep * u;
                        path[i] = new Vector3(roof.Centre.X + MathF.Cos(angle) * radius, height + 2f * u,
                            roof.Centre.Z + MathF.Sin(angle) * radius);
                        clear = ClearOfTowers(city, path[i], roof.Building);
                    }

                    if (clear)
                        return new IntroShot("the rooftop", path, ROOFTOP_SECONDS, fieldOfView * 1.05f, lookAt: look);
                }
            }

            return null;
        }

        //Whether a lens point stands outside every tower but the showcased one, each grown by ROOFTOP_CLEARANCE, up to
        //its roof's top - the cornices included, which stand round roofs
        private static bool ClearOfTowers(City city, Vector3 point, int except)
        {
            for (int b = 0; b < city.Buildings.Length; b++)
            {
                if (b == except) continue;

                Matrix box = city.Buildings[b].World;
                float top = box.M42 + box.M22 * 0.5f + (b < city.TowerCount ? city.RoofRise(b) : 0f) + ROOFTOP_CLEARANCE;
                if (point.Y > top) continue;

                if (MathF.Abs(point.X - box.M41) < box.M11 * 0.5f + ROOFTOP_CLEARANCE
                    && MathF.Abs(point.Z - box.M43) < box.M33 * 0.5f + ROOFTOP_CLEARANCE) return false;
            }

            return true;
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
