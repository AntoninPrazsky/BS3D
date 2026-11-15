using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The big top's gate home (#1118, <see cref="CircusLanding"/>): the owner's "as the last shot of the intro animation,
    /// where the camera goes through the entrance beside the audience, I want this shot to end only in the game position
    /// behind the cannon", held as the meadow's brook home is (<see cref="MeadowLandingTests"/>) over a sweep of gameplay
    /// poses round the arena: it is found for nearly every pose, it comes in low down the performers' entrance, it ends
    /// exactly on the pose with the game's own lens and no bank, it keeps inside the tent and off its seats, its king poles,
    /// its curb and its footlights, and its ride stays inside the limits the other landings are held to. The tent has no CPU
    /// geometry to test a lens against but its figures, so these are the figures (<see cref="CircusIntroShotsTests"/>).
    /// </summary>
    public class CircusLandingTests
    {
        internal const float FieldOfView = MathF.PI / 4.2f;

        //The Big Top chapter's first level (Tightrope) logs its pose: 30.0 out on the gun's bearing (90), the lens at -7.40,
        //looking at (0, -1.57, 0)
        internal static readonly Vector3 GamePosition = new(0f, -7.4f, 30f);
        internal static readonly Vector3 GameTarget = new(0f, -1.57f, 0f);

        //The game's lens stands 28-45 out (GameCameraFit) at a floored height; the sweep takes the axes exactly, as the
        //meadow's and the city's do, and each pose is flown off a roll of its own
        private static readonly float[] Radii = { 29f, 37f, 45f };
        private const int BEARINGS = 12;
        private const int ROLLS = 4;

        //How much of the sweep must get a flight home: the rest fall back to the gate and the tour's leg. The level's own pose,
        //on the gun's bearing, gets one off every roll.
        private const float MIN_BUILT_SHARE = 0.95f;

        //The ride: the meadow's limits (MeadowLandingTests) for the view's turn and pitch, the bank and the landing, and the
        //tent's own for the speed (its cruise is 18-27 u/s, the meadow's 34-42) and the length
        private const float MAX_SPEED = 35f;
        private const float MAX_YAW_RATE = 170f;
        private const float MAX_PITCH_RATE = 80f;
        private const float MAX_ROLL_DEGREES = 8f;
        private const float MAX_LANDING_SPEED = 8f;
        private const float MIN_SECONDS = 4f, MAX_SECONDS = 12f;

        //How finely the shot is walked, and how far off a solid every pose must stay where the builder keeps more at its points
        //(the lens runs a spline through them): 3 off a pole's surface there, 2 here; 2.5 off a footlight, 2 here
        private const int POSES = 1500;
        private const float POLE_CLEAR = 2f;
        private const float LAMP_CLEAR = 2f;

        //Down the gap: the lens holds the gap's middle at the gate's height until it is this far out at least (the run in
        //ends 52-60 out, CircusLanding, and the climb begins up to 8 short of its end); for a circle outside the poles (55.4
        //out at most) the run goes on to 10-14 further out than the circle, so it holds the middle until this far out
        private const float RUN_IN_TO = 68f;
        private const float OUTER_RUN_IN_TO = 70f;

        //A lens on its circle travelling within this many degrees of square to the radial line, and how many degrees inside
        //the frame's edge the axis must stand there
        private const float ACROSS_THE_RADIAL = 15f;
        private const float ARENA_IN_FRAME = 5f;

        //How far over the pose's target the cluster's top and the glass over it stand (Tightrope's field tops 7.2 over its
        //aim, 5.66 against -1.57), and how much of the frame's half height above them must stay clear on the circle: they
        //stand at most 0.47 of it up the frame over the sweep, and with the look level (no LookRise) 0.85 and more
        private const float CLUSTER_TOP = 8f;
        private const float CLUSTER_TOP_MARGIN = 0.25f;

        private static readonly CircusSceneConfig Circus = new();

        private static Vector3 Pose(int bearing, float radius)
        {
            float angle = MathHelper.TwoPi * bearing / BEARINGS;
            return new Vector3(MathF.Cos(angle) * radius, GamePosition.Y, MathF.Sin(angle) * radius);
        }

        private static IntroShot Home(Vector3 pose, int seed) =>
            CircusLanding.Build(Circus, pose, GameTarget, FieldOfView, new Random(seed));

        //Every pose of the sweep, built once for the tests that walk them
        private static readonly Lazy<List<(int Roll, int Bearing, float Radius, IntroShot Shot)>> Sweep = new(() =>
        {
            var sweep = new List<(int, int, float, IntroShot)>();
            for (int roll = 1; roll <= ROLLS; roll++)
                for (int b = 0; b < BEARINGS; b++)
                    foreach (float r in Radii)
                        sweep.Add((roll, b, r, Home(Pose(b, r), roll * 100 + b)));
            return sweep;
        });

        [Fact]
        public void A_flight_home_is_found_for_nearly_every_pose_and_for_the_levels_own_off_every_roll()
        {
            int built = 0;
            foreach (var entry in Sweep.Value)
                if (entry.Shot != null) built++;

            Assert.True(built >= MIN_BUILT_SHARE * Sweep.Value.Count, $"a flight home for {built} of {Sweep.Value.Count} poses");

            for (int seed = 1; seed <= 24; seed++)
                Assert.NotNull(Home(GamePosition, seed));
        }

        [Fact]
        public void It_lands_on_the_pose_with_the_games_lens_and_level()
        {
            foreach (var (roll, bearing, radius, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                Vector3 pose = Pose(bearing, radius);
                shot.Pose(1f, out Vector3 position, out Vector3 target);
                string where = $"roll {roll}, bearing {bearing}, radius {radius}";
                Assert.True(shot.Lands);
                Assert.Equal(CircusLanding.NAME, shot.Name);
                Assert.True(Vector3.Distance(position, pose) < 1e-3f, $"{where}: lands {Vector3.Distance(position, pose):F4} off the pose");
                Assert.True(Vector3.Distance(target, GameTarget) < 1e-3f, $"{where}: looks {Vector3.Distance(target, GameTarget):F4} off the pose's look");
                Assert.Equal(FieldOfView, shot.FieldOfViewAt(1f), 4);
                Assert.Equal(0f, shot.RollAt(1f), 4);
            }
        }

        [Fact]
        public void It_comes_in_low_down_the_performers_entrance()
        {
            float entrance = MathHelper.ToRadians(Circus.Seating.EntranceBearingDegrees);
            float gateY = Circus.Ring.FloorY + CircusIntroShots.GATE_HEIGHT;
            int insideWalked = 0, outsideWalked = 0;

            foreach (var (roll, bearing, radius, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                string where = $"roll {roll}, bearing {bearing}, radius {radius}";

                //It opens where the old gate shot did: just inside the wall, in the entrance's arch, at the gate's height
                shot.Pose(0f, out Vector3 first, out _);
                Assert.InRange(new Vector2(first.X, first.Z).Length(), CircusIntroShots.GATE_FROM - 0.1f, CircusIntroShots.GATE_FROM + 0.1f);
                Assert.True(MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(first.Z, first.X) - entrance)) < 1e-3f, $"{where}: opens off the entrance's bearing");
                Assert.InRange(first.Y, gateY - 0.05f, gateY + 0.05f);

                //Where the circle stands inside the poles (the level's own pose's), down the gap's middle at that height until
                //well past the seats' back half, and inside the gap (the 14-wide strip the old gate's rule held) until it is in
                //front of the first row. Where it stands outside them, over the front rows, down the gap's middle until it is
                //past the back half of the seats, rising as it goes (CircusLanding's OUTER_RUN_PAST)
                bool inside = CircusLanding.CircleRange(Circus, radius).From < Circus.Tent.KingPoleRadius;
                if (inside) insideWalked++; else outsideWalked++;
                for (int i = 0; i <= POSES; i++)
                {
                    shot.Pose(i / (float)POSES, out Vector3 p, out _);
                    float r = new Vector2(p.X, p.Z).Length();
                    if (r < (inside ? Circus.Seating.InnerRadius : OUTER_RUN_IN_TO)) break;

                    float across = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - entrance)) * r;
                    Assert.True(across < (inside ? Circus.Seating.EntranceWidth * 0.5f : 0.05f), $"{where}: {across:F2} off the gap's middle {r:F1} out");
                    if (inside && r > RUN_IN_TO)
                    {
                        Assert.True(across < 0.05f, $"{where}: {across:F2} off the gap's middle {r:F1} out");
                        Assert.InRange(p.Y, gateY - 0.05f, gateY + 0.05f);
                    }
                }
            }

            Assert.True(insideWalked > 0 && outsideWalked > 0, $"{insideWalked} flights inside the poles and {outsideWalked} outside");
        }

        [Fact]
        public void It_keeps_inside_the_tent_and_off_the_seats_the_poles_the_curb_and_the_footlights()
        {
            CircusTentConfig tent = Circus.Tent;
            CircusRingConfig ring = Circus.Ring;
            CircusSeatingConfig seats = Circus.Seating;
            float roofB = (tent.CrownRadius - tent.WallRadius) / (tent.CrownY - tent.WallTopY);
            float roofA = tent.WallRadius - roofB * tent.WallTopY;
            float entrance = MathHelper.ToRadians(seats.EntranceBearingDegrees);
            Vector3[] lamps = Footlights();

            int walked = 0;
            foreach (var (roll, bearing, radius, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                walked++;
                for (int i = 0; i <= POSES; i++)
                {
                    shot.Pose(i / (float)POSES, out Vector3 p, out _);
                    float r = new Vector2(p.X, p.Z).Length();
                    string where = $"roll {roll}, bearing {bearing}, radius {radius}, at {i}/{POSES} ({p.X:F1}, {p.Y:F1}, {p.Z:F1}), {r:F1} out";

                    //Inside the wall, under the canvas, over the floor, never nearer the axis than the pose itself (the island,
                    //the gun, the glass and the cluster all stand nearer) and never over the island
                    Assert.True(r < tent.WallRadius - 1f, $"{where}: outside the wall");
                    Assert.True(roofA + roofB * p.Y > r + 2f, $"{where}: through the canvas");
                    Assert.True(p.Y > ring.FloorY + 1f, $"{where}: under the floor");
                    Assert.True(r >= radius - 1f, $"{where}: nearer the axis than the pose");
                    Assert.True(r >= ArenaIsland.RADIUS + 2f, $"{where}: over the island");

                    //The seats: over the rake (the front riser's top in front of the first row) wherever it is not inside the
                    //entrance's gap
                    float across = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - entrance)) * r;
                    if (r > seats.InnerRadius - 0.5f && across >= seats.EntranceWidth * 0.5f)
                        Assert.True(p.Y > CircusIntroShots.SeatY(Circus, MathF.Max(r, seats.InnerRadius)) + 0.8f, $"{where}: in the seats' rake");

                    //The king poles, the curb and the footlights on it
                    for (int pole = 0; pole < tent.KingPoleCount; pole++)
                    {
                        float b = CircusIntroShots.PoleBearing(Circus, pole);
                        float off = Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(MathF.Cos(b), MathF.Sin(b)) * tent.KingPoleRadius) - tent.KingPoleThickness;
                        Assert.True(off >= POLE_CLEAR, $"{where}: {off:F2} off the king pole at {MathHelper.ToDegrees(b):F0} degrees");
                    }

                    if (r > ring.CurbRadius - 0.5f && r < ring.CurbRadius + ring.CurbThickness + 0.5f)
                        Assert.True(p.Y > ring.FloorY + ring.CurbHeight + 0.5f, $"{where}: in the curb");

                    foreach (Vector3 lamp in lamps)
                        Assert.True(Vector3.Distance(p, lamp) >= LAMP_CLEAR, $"{where}: {Vector3.Distance(p, lamp):F2} from a footlight");
                }
            }

            Assert.True(walked > 0);
        }

        [Fact]
        public void The_ride_stays_inside_its_limits()
        {
            float longest = 0f;
            foreach (var (roll, bearing, radius, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                string where = $"roll {roll}, bearing {bearing}, radius {radius}";
                Assert.InRange(shot.Seconds, MIN_SECONDS, MAX_SECONDS);
                longest = MathF.Max(longest, shot.Seconds);

                float dt = shot.Seconds / POSES;
                shot.Pose(0f, out Vector3 last, out Vector3 lastTarget);
                float lastYaw = Yaw(last, lastTarget), lastPitch = Pitch(last, lastTarget);

                for (int i = 1; i <= POSES; i++)
                {
                    float t = i / (float)POSES;
                    shot.Pose(t, out Vector3 position, out Vector3 target);
                    float speed = Vector3.Distance(position, last) / dt;
                    float yaw = Yaw(position, target), pitch = Pitch(position, target);

                    Assert.True(speed <= MAX_SPEED, $"{where}: {speed:F0} u/s at {t:F2}");
                    if (t > 1f - 0.5f / shot.Seconds)
                        Assert.True(speed <= MAX_LANDING_SPEED, $"{where}: still {speed:F1} u/s at {t:F3}, landing");

                    float yawRate = MathF.Abs(MathHelper.ToDegrees(MathHelper.WrapAngle(yaw - lastYaw))) / dt;
                    float pitchRate = MathF.Abs(MathHelper.ToDegrees(pitch - lastPitch)) / dt;
                    Assert.True(yawRate <= MAX_YAW_RATE, $"{where}: the view turns {yawRate:F0} deg/s at {t:F2}");
                    Assert.True(pitchRate <= MAX_PITCH_RATE, $"{where}: the view pitches {pitchRate:F0} deg/s at {t:F2}");
                    Assert.True(MathF.Abs(MathHelper.ToDegrees(shot.RollAt(t))) <= MAX_ROLL_DEGREES, $"{where}: banked past the limit at {t:F2}");

                    last = position;
                    lastYaw = yaw;
                    lastPitch = pitch;
                }
            }

            Assert.True(longest > 0f, "no flight home was walked");
        }

        [Fact]
        public void Round_the_island_the_look_keeps_the_arena_in_frame()
        {
            //The look turns in off the travel towards the arena (CircusLanding.Landing's LookInward, the route's Lean): along
            //the circle's tangent the axis stands 90 degrees off the look, out of any frame. Wherever the lens runs round the
            //island on its circle, the axis stands inside a 16:9 frame at the shot's own lens, ARENA_IN_FRAME short of its edge.
            int held = 0;
            foreach (var (roll, bearing, radius, shot) in Sweep.Value)
            {
                if (shot == null) continue;

                (float circleFrom, float circleTo) = CircusLanding.CircleRange(Circus, radius);
                shot.Pose(0f, out Vector3 last, out _);
                for (int i = 1; i <= POSES; i++)
                {
                    float t = i / (float)POSES;
                    shot.Pose(t, out Vector3 position, out Vector3 look);
                    Vector2 plan = new(position.X, position.Z), step = plan - new Vector2(last.X, last.Z);
                    last = position;

                    //On the circle: as far out as it is laid, travelling across the radial line
                    float fromAxis = plan.Length();
                    if (fromAxis < circleFrom - 1f || fromAxis > circleTo + 1f || step.LengthSquared() < 1e-8f) continue;
                    if (MathF.Abs(Vector2.Dot(Vector2.Normalize(step), plan / fromAxis)) > MathF.Sin(MathHelper.ToRadians(ACROSS_THE_RADIAL))) continue;

                    float halfWidth = MathF.Atan(MathF.Tan(shot.FieldOfViewAt(t) * 0.5f) * 16f / 9f);
                    float off = MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(GameTarget.Z - position.Z, GameTarget.X - position.X)
                        - MathF.Atan2(look.Z - position.Z, look.X - position.X)));
                    string where = $"roll {roll}, bearing {bearing}, radius {radius}, at {t:F3}";
                    Assert.True(off <= halfWidth - MathHelper.ToRadians(ARENA_IN_FRAME),
                        $"{where}: the axis {MathHelper.ToDegrees(off):F0} degrees off the look, the frame's edge {MathHelper.ToDegrees(halfWidth):F0}");

                    //And the cluster's top and the glass over it, CLUSTER_TOP over the pose's target, stand inside the frame's
                    //height, CLUSTER_TOP_MARGIN of the half height short of its top edge (the bank, 8 degrees at most, left out)
                    float top = ScreenHeight(position, look, GameTarget + Vector3.Up * CLUSTER_TOP, shot.FieldOfViewAt(t));
                    Assert.True(top <= 1f - CLUSTER_TOP_MARGIN, $"{where}: the cluster's top at {top:F2} of the frame's half height");
                    held++;
                }
            }

            Assert.True(held >= 1000, $"the lens was on the circle at only {held} poses");
        }

        [Fact]
        public void A_landing_big_top_prologue_plays_no_tour_leg_and_ends_on_the_pose()
        {
            IntroShot[] shots = CircusIntroShots.Build(Circus, GamePosition, GameTarget, FieldOfView, new Random(3));
            Assert.Equal(3, shots.Length);
            Assert.True(shots[^1].Lands);
            Assert.Equal(CircusLanding.NAME, shots[^1].Name);

            float seconds = 0f;
            foreach (IntroShot shot in shots) seconds += shot.Seconds;

            var intro = new ChapterIntro();
            intro.Begin(GameTarget, GamePosition.Length(), FieldOfView, GamePosition, GameTarget, null, new Random(1), shots);
            Assert.Equal(seconds, intro.Remaining, 3);

            while (intro.Running) intro.Update(1f / 60f);
            intro.Update(1f / 60f);

            Assert.True(Vector3.Distance(intro.Position, GamePosition) < 1e-3f, $"held {Vector3.Distance(intro.Position, GamePosition):F3} off the pose");
            Assert.True(Vector3.Distance(intro.Target, GameTarget) < 1e-3f);
            Assert.Equal(FieldOfView, intro.FieldOfView, 4);
            Assert.Equal(0f, intro.Roll, 4);
        }

        [Fact]
        public void No_flight_is_laid_into_a_pose_inside_the_islands_radius_and_the_reel_ends_on_the_gate()
        {
            //The menu's crane stands as near as 15 out, over the island: no flight from the entrance reaches it without
            //crossing the arena, so the big top plays its reel and the tour's last leg there
            var crane = new Vector3(0f, -2f, 15f);
            Assert.Null(Home(crane, 1));
            Assert.NotNull(Home(GamePosition, 1));

            IntroShot[] reel = CircusIntroShots.Build(Circus, crane, GameTarget, FieldOfView, new Random(1));
            Assert.Equal(CircusIntroShots.GATE_NAME, reel[^1].Name);
            Assert.False(reel[^1].Lands);
        }

        [Fact]
        public void The_tent_refuses_a_lens_in_the_seats_at_a_pole_on_the_curb_at_a_footlight_or_nearer_than_the_pose()
        {
            //Each of the tent's rules on its own, where the sweep might never meet it: a point is refused for the one solid it
            //stands in and taken a little way off it
            var tent = new CircusLanding.Tent(Circus, 0f);
            float floor = Circus.Ring.FloorY;

            Vector3 At(float bearingDegrees, float radius, float y)
            {
                Vector2 plan = new Vector2(MathF.Cos(MathHelper.ToRadians(bearingDegrees)), MathF.Sin(MathHelper.ToRadians(bearingDegrees))) * radius;
                return new Vector3(plan.X, y, plan.Y);
            }

            //The seats: low over the rake 70 out is refused off the gap and taken in its middle, and taken high over the rake;
            //at the gate's height just behind the front row, it is taken in the gap's middle and refused by its wall (the gap
            //is 7.1 either side of its middle 52 out)
            float seatY = CircusIntroShots.SeatY(Circus, 70f);
            float gateDegrees = Circus.Seating.EntranceBearingDegrees;
            float gateY = floor + CircusIntroShots.GATE_HEIGHT;
            Assert.False(tent.Clear(At(gateDegrees + 30f, 70f, seatY + 0.5f)), "low over the seats");
            Assert.True(tent.Clear(At(gateDegrees + 30f, 70f, seatY + 2f)), "over the seats");
            Assert.True(tent.Clear(At(gateDegrees, 70f, gateY)), "down the gap's middle");
            Assert.True(tent.Clear(At(gateDegrees, 52f, gateY)), "down the gap's middle at the front row");
            Assert.False(tent.Clear(At(gateDegrees + MathHelper.ToDegrees(6.5f / 52f), 52f, gateY)), "against the gap's wall");

            //A king pole, the curb, a footlight
            float pole = MathHelper.ToDegrees(CircusIntroShots.PoleBearing(Circus, 1));
            Assert.False(tent.Clear(At(pole, Circus.Tent.KingPoleRadius - 2.5f, floor + 6f)), "beside a king pole");
            Assert.True(tent.Clear(At(pole, Circus.Tent.KingPoleRadius - 5f, floor + 6f)), "inside the poles' ring");
            Assert.False(tent.Clear(At(pole + 20f, Circus.Ring.CurbRadius + 0.5f, floor + 2f)), "on the curb");
            Assert.True(tent.Clear(At(pole + 20f, Circus.Ring.CurbRadius + 0.5f, floor + 3.5f)), "over the curb");
            Vector3 lamp = Footlights()[0];
            Assert.False(tent.Clear(lamp + new Vector3(0f, 2f, 0f)), "at a footlight");
            Assert.True(tent.Clear(lamp + new Vector3(0f, 3f, 0f)), "over a footlight");

            //Nearer the axis than the keep-out a landing is handed (the pose's own distance, less a little): the routes never
            //come there by construction, the circle standing outside the pose and the turn in only closing on it, so it is
            //held here
            var keepOut = new CircusLanding.Tent(Circus, 30f);
            Assert.False(keepOut.Clear(At(90f, 29.5f, floor + 6f)), "nearer the axis than the pose");
            Assert.True(keepOut.Clear(At(90f, 30.5f, floor + 6f)), "as far out as the pose");

            //And the copies the Game keeps of the backdrop's footlight figures, which it cannot see, are the backdrop's own
            Assert.Equal(CircusBackdrop.FOOTLIGHT_COUNT, CircusLanding.FOOTLIGHT_COUNT);
            Assert.Equal(CircusBackdrop.FOOTLIGHT_INSET, CircusLanding.FOOTLIGHT_INSET);
            Assert.Equal(CircusBackdrop.FOOTLIGHT_HEIGHT, CircusLanding.FOOTLIGHT_HEIGHT);
        }

        //The footlights where the backdrop stands them (CircusBackdrop.FootlightPosition), off its own figures
        private static Vector3[] Footlights()
        {
            var lamps = new Vector3[CircusBackdrop.FOOTLIGHT_COUNT];
            for (int k = 0; k < lamps.Length; k++)
            {
                float bearing = (k + 0.5f) * MathHelper.TwoPi / CircusBackdrop.FOOTLIGHT_COUNT;
                float radius = Circus.Ring.CurbRadius - CircusBackdrop.FOOTLIGHT_INSET;
                lamps[k] = new Vector3(MathF.Cos(bearing) * radius, Circus.Ring.FloorY + CircusBackdrop.FOOTLIGHT_HEIGHT, MathF.Sin(bearing) * radius);
            }
            return lamps;
        }

        //Where a point stands up the frame of a level lens at POSITION looking at LOOK: 0 at the middle, 1 at the top edge
        private static float ScreenHeight(Vector3 position, Vector3 look, Vector3 point, float fieldOfView)
        {
            Vector3 forward = Vector3.Normalize(look - position);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
            Vector3 up = Vector3.Cross(right, forward);
            Vector3 d = point - position;
            return Vector3.Dot(d, up) / Vector3.Dot(d, forward) / MathF.Tan(fieldOfView * 0.5f);
        }

        private static float Yaw(Vector3 position, Vector3 target) => MathF.Atan2(target.Z - position.Z, target.X - position.X);

        private static float Pitch(Vector3 position, Vector3 target) =>
            MathF.Atan2(target.Y - position.Y, new Vector2(target.X - position.X, target.Z - position.Z).Length());
    }
}
