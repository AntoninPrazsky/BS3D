using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The big top's own shots for a chapter intro's prologue (#752). The owner: "the flight through the big top needs
    /// improving. So that we see the ceiling. So that we have a view from a spectator's place forward to the island. And
    /// cuts." The Big Top had no prologue at all: the generic tour flew, inside the king poles' ring, and the canvas only
    /// ever showed at the top of the frame. Three shots, each in a different place, direction and height from its
    /// neighbour, cut together, then cut to the tour's last leg:
    /// <list type="bullet">
    /// <item><b>The canvas</b>: low on the ring floor beside a king pole, the look climbing the pole to where it meets the
    /// canvas and on up the roof towards the crown.</item>
    /// <item><b>The seats</b>: a seat high in the stands, at a sitting eye's height over its row, a slow dolly along the
    /// row with the look across the ring on the island, the way the audience sees it.</item>
    /// <item><b>The gate</b>: in through the performers' entrance, low, the island ahead: the way the act comes in.</item>
    /// </list>
    /// Everything is placed off the tent's own figures (<see cref="CircusSceneConfig"/>): the poles where the pass puts
    /// them, the rake of the seats as <c>Circus.fx</c> draws it, the entrance gap. Built once when the intro begins.
    /// </summary>
    internal static class CircusIntroShots
    {
        //The canvas: the lens this far round from a pole and this far out, rising from this high over the floor to this,
        //the look climbing the pole from this high and on up the roof to this share of the way from the pole's top to the
        //crown
        private const float CANVAS_BESIDE_DEGREES = 20f;
        private const float CANVAS_RADIUS = 34f;
        private const float CANVAS_FROM_HEIGHT = 2.5f, CANVAS_TO_HEIGHT = 6f;
        private const float CANVAS_LOOK_FROM = 4f;
        private const float CANVAS_TOWARD_CROWN = 0.7f;
        private const float CANVAS_SECONDS = 4.2f;

        //The seats: a row this far out, a seated eye this high over it, a dolly this far round, kept this far off a pole's
        //bearing (one would stand across the view) and off the entrance; the look on the island this high over the floor
        private const float SEAT_RADIUS = 80f;
        private const float SEAT_EYE = 1.15f;
        private const float SEAT_DOLLY_DEGREES = 12f;
        private const float SEAT_POLE_CLEAR_DEGREES = 14f;
        private const float SEAT_ENTRANCE_CLEAR_DEGREES = 26f;
        private const float SEAT_LOOK_HEIGHT = 5f;
        private const float SEAT_SECONDS = 3.6f;

        //The gate: in along the entrance's bearing from this far out to this, this high over the floor
        private const float GATE_FROM = 94f, GATE_TO = 66f;
        private const float GATE_HEIGHT = 2.6f;
        private const float GATE_SECONDS = 3.2f;

        private const int PATH_POINTS = IntroPaths.POINTS;

        /// <summary>
        /// The prologue for the big top being drawn, or null when the renderer has no circus config.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Circus) is not CircusSceneConfig circus) return null;
            return Build(circus, fieldOfView, random);
        }

        /// <summary>The prologue off a config, with no renderer: what the tests fly.</summary>
        internal static IntroShot[] Build(CircusSceneConfig circus, float fieldOfView, Random random) => new[]
        {
            Canvas(circus, fieldOfView, random),
            Seats(circus, fieldOfView, random),
            Gate(circus, fieldOfView),
        };

        /// <summary>The bearing of king pole <paramref name="pole"/>, as the pass and the poles' depth mesh place it.</summary>
        internal static float PoleBearing(CircusSceneConfig circus, int pole) =>
            (pole + 0.5f) * MathHelper.TwoPi / Math.Clamp(circus.Tent.KingPoleCount, 1, 6);

        /// <summary>Where the canvas meets the king poles' radius: the roof is a cone r = a + b y from the wall's top to the crown.</summary>
        internal static float PoleTopY(CircusSceneConfig circus)
        {
            CircusTentConfig tent = circus.Tent;
            float b = (tent.CrownRadius - tent.WallRadius) / (tent.CrownY - tent.WallTopY);
            float a = tent.WallRadius - b * tent.WallTopY;
            return (tent.KingPoleRadius - a) / b;
        }

        /// <summary>The top of the seats' rake at <paramref name="radius"/>, as <c>Circus.fx</c> draws it.</summary>
        internal static float SeatY(CircusSceneConfig circus, float radius)
        {
            CircusSeatingConfig seats = circus.Seating;
            return circus.Ring.FloorY + seats.RowRise + (radius - seats.InnerRadius) * seats.RowRise / seats.RowDepth;
        }

        /// <summary>Low beside a king pole, the look up the pole and over the canvas towards the crown.</summary>
        private static IntroShot Canvas(CircusSceneConfig circus, float fieldOfView, Random random)
        {
            CircusTentConfig tent = circus.Tent;
            float floor = circus.Ring.FloorY;
            float pole = PoleBearing(circus, random.Next(Math.Max(tent.KingPoleCount, 1)));
            float side = MathHelper.ToRadians(CANVAS_BESIDE_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
            float lens = pole + side;

            Vector3 poleFoot = new(MathF.Cos(pole) * tent.KingPoleRadius, floor, MathF.Sin(pole) * tent.KingPoleRadius);
            float top = PoleTopY(circus);
            Vector3 poleTop = new(poleFoot.X, top, poleFoot.Z);
            Vector3 nearCrown = Vector3.Lerp(poleTop, new Vector3(0f, tent.CrownY, 0f), CANVAS_TOWARD_CROWN);

            var path = new Vector3[PATH_POINTS];
            var look = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                path[i] = new Vector3(MathF.Cos(lens) * CANVAS_RADIUS, floor + MathHelper.Lerp(CANVAS_FROM_HEIGHT, CANVAS_TO_HEIGHT, s),
                    MathF.Sin(lens) * CANVAS_RADIUS);

                //Up the pole over the first half, eased, then on over the canvas towards the crown
                float up = MathHelper.Clamp(s / 0.55f, 0f, 1f);
                up = up * up * (3f - 2f * up);
                Vector3 onPole = new(poleFoot.X, floor + MathHelper.Lerp(CANVAS_LOOK_FROM, top, up), poleFoot.Z);
                float over = MathHelper.Clamp((s - 0.55f) / 0.45f, 0f, 1f);
                look[i] = Vector3.Lerp(onPole, nearCrown, over * over * (3f - 2f * over));
            }

            return new IntroShot("the canvas", path, CANVAS_SECONDS, fieldOfView * 1.45f, lookAtPath: look);
        }

        /// <summary>A seat high in the stands, a slow dolly along the row, the look across the ring on the island.</summary>
        private static IntroShot Seats(CircusSceneConfig circus, float fieldOfView, Random random)
        {
            float entrance = MathHelper.ToRadians(circus.Seating.EntranceBearingDegrees);
            float dolly = MathHelper.ToRadians(SEAT_DOLLY_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
            float start = 0f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                start = (float)random.NextDouble() * MathHelper.TwoPi;
                if (Clear(circus, start, dolly, entrance)) break;
            }

            float eye = SeatY(circus, SEAT_RADIUS) + SEAT_EYE;
            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float b = start + dolly * (i / (float)(PATH_POINTS - 1));
                path[i] = new Vector3(MathF.Cos(b) * SEAT_RADIUS, eye, MathF.Sin(b) * SEAT_RADIUS);
            }

            return new IntroShot("the seats", path, SEAT_SECONDS, fieldOfView * 1.15f,
                lookAt: new Vector3(0f, circus.Ring.FloorY + SEAT_LOOK_HEIGHT, 0f));
        }

        //Whether a dolly from START round by DOLLY keeps off every pole's bearing (a pole would stand across the view to the
        //island) and off the entrance gap, where there are no seats to sit in
        private static bool Clear(CircusSceneConfig circus, float start, float dolly, float entrance)
        {
            float poleClear = MathHelper.ToRadians(SEAT_POLE_CLEAR_DEGREES);
            float entranceClear = MathHelper.ToRadians(SEAT_ENTRANCE_CLEAR_DEGREES);
            for (int k = 0; k <= 4; k++)
            {
                float b = start + dolly * (k / 4f);
                if (MathF.Abs(MathHelper.WrapAngle(b - entrance)) < entranceClear) return false;
                for (int p = 0; p < circus.Tent.KingPoleCount; p++)
                    if (MathF.Abs(MathHelper.WrapAngle(b - PoleBearing(circus, p))) < poleClear) return false;
            }
            return true;
        }

        /// <summary>In through the performers' entrance, low, the island ahead.</summary>
        private static IntroShot Gate(CircusSceneConfig circus, float fieldOfView)
        {
            float entrance = MathHelper.ToRadians(circus.Seating.EntranceBearingDegrees);
            Vector2 outward = new(MathF.Cos(entrance), MathF.Sin(entrance));
            float y = circus.Ring.FloorY + GATE_HEIGHT;

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                float r = MathHelper.Lerp(GATE_FROM, GATE_TO, s * s * (3f - 2f * s));
                path[i] = new Vector3(outward.X * r, y, outward.Y * r);
            }

            return new IntroShot("the gate", path, GATE_SECONDS, fieldOfView * 1.2f,
                lookAt: new Vector3(0f, circus.Ring.FloorY + 6f, 0f));
        }
    }
}
