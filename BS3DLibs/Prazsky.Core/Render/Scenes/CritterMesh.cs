using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The desert's creatures' bodies (#966): a darkling beetle and a scorpion one body length long (the nose at +Z, standing
    /// on Y = 0), Low and Medium's built of ellipsoids and tubes (<see cref="Beetle"/>, <see cref="Scorpion"/>) and High and
    /// Ultra's of shaped shells, plates, beads and jointed limbs (<see cref="FineBeetle"/>, <see cref="FineScorpion"/>, #1110),
    /// the two of a kind the same size on the same legs. Each vertex carries in its texture coordinate what <c>Critter.fx</c>
    /// moves it by: X the leg's gait phase in radians (a tripod, the middle leg of a side out of step with its front and back),
    /// or <see cref="BODY"/> for the body and <see cref="TAIL"/> for a scorpion's tail and pincers; Y how far along the leg or
    /// the tail the vertex is (0 at the hip, 1 at the foot or the sting). Every triangle is wound clockwise seen from outside, against its
    /// vertices' own normals, as every procedural mesh in the repository is.
    /// </summary>
    internal static class CritterMesh
    {
        internal const float BODY = -1f, TAIL = -2f;

        internal static (VertexPositionNormalTexture[] Vertices, short[] Indices) Beetle()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            //The shell: long and domed, the head in front of it, the pronotum between
            Ellipsoid(v, idx, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.24f, 0.17f, 0.36f), BODY);
            Ellipsoid(v, idx, new Vector3(0f, 0.17f, 0.32f), new Vector3(0.15f, 0.11f, 0.1f), BODY);
            Ellipsoid(v, idx, new Vector3(0f, 0.15f, 0.44f), new Vector3(0.08f, 0.07f, 0.07f), BODY);
            //Antennae
            Leg(v, idx, new Vector3(0.04f, 0.18f, 0.48f), new Vector3(0.12f, 0.24f, 0.62f), new Vector3(0.16f, 0.22f, 0.72f), 0.012f, BODY);
            Leg(v, idx, new Vector3(-0.04f, 0.18f, 0.48f), new Vector3(-0.12f, 0.24f, 0.62f), new Vector3(-0.16f, 0.22f, 0.72f), 0.012f, BODY);
            //Six legs, long and thin, in a tripod: front and back of one side with the middle of the other
            float[] along = { 0.26f, 0.05f, -0.18f };
            for (int i = 0; i < 3; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float phase = ((i == 1) == (side > 0)) ? 0f : MathF.PI;
                    float z = along[i];
                    Leg(v, idx, new Vector3(0.15f * side, 0.14f, z), new Vector3(0.36f * side, 0.24f, z + 0.06f * (1 - i)),
                        new Vector3(0.48f * side, 0f, z + 0.12f * (1 - i)), 0.022f, phase);
                }
            return (v.ToArray(), idx.ToArray());
        }

        internal static (VertexPositionNormalTexture[] Vertices, short[] Indices) Scorpion()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();
            //The carapace and the abdomen, low and flat
            Ellipsoid(v, idx, new Vector3(0f, 0.1f, 0.12f), new Vector3(0.14f, 0.07f, 0.16f), BODY);
            Ellipsoid(v, idx, new Vector3(0f, 0.1f, -0.12f), new Vector3(0.13f, 0.075f, 0.2f), BODY);
            //The tail: six thick segments overlapping into one curl up and over the back, the bulb and the sting at its tip.
            //⚠ Five thin ones, each a short ellipsoid, read as a stack of balls
            //On a circle behind the body: from straight down at the abdomen's end, back, up over the top and forwards, a two
            //thirds turn. ⚠ Raised straight up and dropped back, it read from the side as a stack of balls
            var tail = new Vector3[7];
            for (int s = 0; s < tail.Length; s++)
            {
                float a = MathHelper.ToRadians(-90f - 235f * s / 6f);
                tail[s] = new Vector3(0f, 0.34f + MathF.Sin(a) * 0.24f, -0.33f + MathF.Cos(a) * 0.24f);
            }
            for (int s = 0; s < tail.Length - 1; s++)
            {
                Vector3 mid = (tail[s] + tail[s + 1]) * 0.5f;
                Vector3 axis = tail[s + 1] - tail[s];
                Segment(v, idx, mid, axis * 1.3f, 0.07f - 0.006f * s, TAIL, s / 6f);
            }
            Ellipsoid(v, idx, tail[6] + new Vector3(0f, -0.03f, 0.05f), new Vector3(0.055f, 0.06f, 0.075f), TAIL, 1f);
            Leg(v, idx, tail[6] + new Vector3(0f, -0.06f, 0.1f), tail[6] + new Vector3(0f, -0.1f, 0.13f), tail[6] + new Vector3(0f, -0.15f, 0.12f), 0.018f, TAIL);
            //The pincers: an arm out and forwards, a big claw at its end opening into two fingers, the references' chelae
            for (int side = -1; side <= 1; side += 2)
            {
                Leg(v, idx, new Vector3(0.08f * side, 0.1f, 0.24f), new Vector3(0.24f * side, 0.15f, 0.34f), new Vector3(0.22f * side, 0.11f, 0.48f), 0.04f, TAIL);
                Ellipsoid(v, idx, new Vector3(0.22f * side, 0.11f, 0.56f), new Vector3(0.085f, 0.055f, 0.12f), TAIL, 0.6f);
                Leg(v, idx, new Vector3(0.24f * side, 0.11f, 0.64f), new Vector3(0.25f * side, 0.11f, 0.72f), new Vector3(0.21f * side, 0.1f, 0.77f), 0.025f, TAIL);
                Leg(v, idx, new Vector3(0.19f * side, 0.11f, 0.64f), new Vector3(0.17f * side, 0.11f, 0.71f), new Vector3(0.2f * side, 0.1f, 0.76f), 0.02f, TAIL);
            }
            //Eight legs in two tripods and a pair
            float[] along = { 0.14f, 0.03f, -0.08f, -0.19f };
            for (int i = 0; i < 4; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float phase = ((i % 2 == 1) == (side > 0)) ? 0f : MathF.PI;
                    float z = along[i];
                    Leg(v, idx, new Vector3(0.11f * side, 0.09f, z), new Vector3(0.3f * side, 0.17f, z + 0.03f * (1.5f - i)),
                        new Vector3(0.4f * side, 0f, z + 0.07f * (1.5f - i)), 0.018f, phase);
                }
            return (v.ToArray(), idx.ToArray());
        }

        /// <summary>
        /// High and Ultra's darkling beetle (#1110, the owner: "raise the detail of the scorpion and the beetle"), on the
        /// references' (<c>966-zimage</c>, <c>966-klein</c>) build: the elytra one domed shell with the suture down its middle,
        /// faint ribs along it and a rim where it meets the flatter belly; a square-shouldered pronotum apart from it; a head
        /// with its eyes and mandibles; antennae of beads thickening to a club; and six jointed legs, a thick femur up to a
        /// knee above the back's middle, a thin tibia down and a tarsus of three beads on the sand. Its extents are
        /// <see cref="Beetle"/>'s, so the creature's size, its gait and everything framed on it stand.
        /// </summary>
        internal static (VertexPositionNormalTexture[] Vertices, short[] Indices) FineBeetle()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();

            //The elytra: the coarse shell's ellipsoid, blunter where it meets the pronotum, its belly flatter; the suture a groove
            //down the top's middle, three ribs either side of it fading to the ends, and the rim a lip round the shell's edge
            var shell = new Vector3(0f, 0.2f, -0.06f);
            Surface(v, idx, 40, 24, (u, w) =>
            {
                Vector3 unit = Unit(u, w);
                if (unit.Z > 0f) unit.Z *= 0.86f;
                float top = MathF.Max(unit.Y, 0f);
                if (unit.Y < 0f) unit.Y *= 0.42f;
                float suture = MathF.Exp(-(unit.X / 0.05f) * (unit.X / 0.05f)) * top;
                float ribs = 0.03f * (0.5f + 0.5f * MathF.Cos(MathHelper.TwoPi * 3.5f * unit.X)) * top * top;
                float rim = 0.035f * MathF.Exp(-((unit.Y + 0.03f) / 0.07f) * ((unit.Y + 0.03f) / 0.07f));
                float scale = 1f - 0.08f * suture + ribs;
                return shell + new Vector3(unit.X * 0.24f * (scale + rim), unit.Y * 0.17f * scale, unit.Z * 0.36f * scale);
            }, shell, BODY, _ => 0f);

            //The pronotum: a transverse shield with square shoulders, a little apart from the elytra so the waist between reads
            Bead(v, idx, 24, 12, new Vector3(0f, 0.19f, 0.335f), Vector3.UnitZ * 0.095f, new Vector2(0.16f, 0.1f), 0.7f, BODY);
            //The head, tipped down, its eyes either side and the mandibles under its front
            Bead(v, idx, 16, 10, new Vector3(0f, 0.155f, 0.455f), new Vector3(0f, -0.02f, 0.07f), new Vector2(0.085f, 0.07f), 1f, BODY);
            for (int side = -1; side <= 1; side += 2)
            {
                Bead(v, idx, 8, 6, new Vector3(0.068f * side, 0.175f, 0.47f), Vector3.UnitZ * 0.022f, new Vector2(0.02f, 0.022f), 1f, BODY);
                Limb(v, idx, new[] { new Vector3(0.03f * side, 0.125f, 0.5f), new Vector3(0.035f * side, 0.115f, 0.545f), new Vector3(0.012f * side, 0.105f, 0.565f) },
                    new[] { 0.016f, 0.012f, 0.002f }, 5, 2, BODY, 0f, 0f);

                //An antenna: eleven beads on a curve out, up and forwards, the last three a club
                Vector3 a0 = new(0.055f * side, 0.18f, 0.5f), a1 = new(0.13f * side, 0.25f, 0.6f), a2 = new(0.17f * side, 0.225f, 0.73f);
                const int BEADS = 11;
                for (int b = 0; b < BEADS; b++)
                {
                    Vector3 from = Bezier(a0, a1, a2, b / (float)BEADS), to = Bezier(a0, a1, a2, (b + 1) / (float)BEADS);
                    float radius = b >= BEADS - 3 ? 0.016f : 0.0105f;
                    Bead(v, idx, 6, 4, (from + to) * 0.5f, (to - from) * 0.62f, new Vector2(radius, radius), 1f, BODY);
                }
            }

            //Six legs in a tripod, as the coarse beetle's: the femur thick, up to a knee over the back's middle, the tibia thin
            //and down to the sand, the tarsus three beads along it; the front pair reaching forwards, the hind pair back
            float[] along = { 0.26f, 0.04f, -0.18f };
            for (int i = 0; i < 3; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float phase = ((i == 1) == (side > 0)) ? 0f : MathF.PI;
                    float z = along[i], reach = 0.07f * (1 - i);
                    Vector3[] joints =
                    {
                        new(0.11f * side, 0.13f, z), new(0.31f * side, 0.27f, z + reach * 0.6f), new(0.43f * side, 0.045f, z + reach * 1.3f),
                        new(0.46f * side, 0.025f, z + reach * 1.45f), new(0.49f * side, 0.01f, z + reach * 1.6f), new(0.52f * side, 0f, z + reach * 1.75f),
                    };
                    Limb(v, idx, joints, new[] { 0.03f, 0.024f, 0.015f, 0.011f, 0.01f, 0.005f }, 6, 4, phase, 0f, 1f);
                }
            return (v.ToArray(), idx.ToArray());
        }

        /// <summary>
        /// High and Ultra's scorpion (#1110), on the references' build: a shield-shaped carapace with its eyes, the back
        /// seven overlapping plates over a belly, the tail five keeled segments with waists between and the bulb and its curved
        /// sting, the pincers an arm of three segments ending in a swollen hand and two fingers, and eight jointed legs, femur
        /// up to a high knee and the tibia and tarsus down to the sand. The tail's curl, the pincers' reach and the legs'
        /// span are <see cref="Scorpion"/>'s, so its size and everything framed on it stand.
        /// </summary>
        internal static (VertexPositionNormalTexture[] Vertices, short[] Indices) FineScorpion()
        {
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();

            //The carapace: a flat shield, square-shouldered and narrowing to the front, the median eyes a pair of beads on it
            var carapace = new Vector3(0f, 0.105f, 0.17f);
            Surface(v, idx, 28, 14, (u, w) =>
            {
                Vector3 unit = Unit(u, w);
                float x = Signed(unit.X, 0.75f), z = Signed(unit.Z, 0.8f);
                float narrowing = 1f - 0.28f * MathF.Max(z, 0f);
                float y = unit.Y < 0f ? unit.Y * 0.6f : unit.Y;
                return carapace + new Vector3(x * 0.13f * narrowing, y * 0.055f, z * 0.12f);
            }, carapace, BODY, _ => 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                Bead(v, idx, 8, 6, new Vector3(0.022f * side, 0.158f, 0.2f), Vector3.UnitZ * 0.014f, new Vector2(0.014f, 0.012f), 1f, BODY);
                //The chelicerae, a pair of small jaws under the carapace's front
                Bead(v, idx, 8, 6, new Vector3(0.028f * side, 0.09f, 0.3f), Vector3.UnitZ * 0.03f, new Vector2(0.022f, 0.018f), 1f, BODY);
            }

            //The back: seven plates from the carapace to the tail, widest a third of the way, each tipped up at its rear so it
            //laps over the next like a shingle; the belly a long flat body under them
            float[] widths = { 0.125f, 0.142f, 0.152f, 0.154f, 0.148f, 0.134f, 0.112f };
            for (int p = 0; p < widths.Length; p++)
            {
                float z = 0.045f - p * 0.056f;
                Bead(v, idx, 22, 8, new Vector3(0f, 0.115f + 0.004f * MathF.Sin(MathF.PI * p / 6f), z), new Vector3(0f, 0.012f, -0.05f),
                    new Vector2(widths[p], 0.05f), 0.72f, BODY);
            }
            Bead(v, idx, 20, 10, new Vector3(0f, 0.085f, -0.13f), Vector3.UnitZ * 0.22f, new Vector2(0.125f, 0.045f), 0.85f, BODY);

            //The tail on the coarse scorpion's circle behind the body: five segments, each keeled (squarer across) and narrowing
            //at its ends so the joints read as waists, then the bulb and the sting at its tip
            var tail = new Vector3[7];
            for (int s = 0; s < tail.Length; s++)
            {
                float a = MathHelper.ToRadians(-90f - 235f * s / 6f);
                tail[s] = new Vector3(0f, 0.34f + MathF.Sin(a) * 0.24f, -0.33f + MathF.Cos(a) * 0.24f);
            }
            for (int s = 0; s < 5; s++)
            {
                Vector3 mid = (tail[s] + tail[s + 1]) * 0.5f, axis = tail[s + 1] - tail[s];
                float radius = 0.066f - 0.005f * s;
                int segment = s;
                Bead(v, idx, 14, 10, mid, axis * 0.56f, new Vector2(radius, radius * 0.92f), 0.78f, TAIL,
                    p => (segment + Math.Clamp(Vector3.Dot(p - tail[segment], axis) / axis.LengthSquared(), 0f, 1f)) / 6f);
            }
            Vector3 telson = tail[5] + (tail[6] - tail[5]) * 0.6f;
            Bead(v, idx, 14, 10, telson, (tail[6] - tail[5]) * 0.5f, new Vector2(0.056f, 0.062f), 1f, TAIL, _ => 0.95f);
            Vector3 sting = tail[6] + (tail[6] - tail[5]) * 0.25f;
            Limb(v, idx, new[] { telson, sting, sting + new Vector3(0f, -0.06f, 0.03f), sting + new Vector3(0f, -0.11f, 0.0f) },
                new[] { 0.03f, 0.022f, 0.012f, 0.002f }, 6, 3, TAIL, 0.95f, 1f, waists: false);

            //The pincers: an arm of three segments out and forwards, then the hand, swollen, and its two fingers, the inner
            //one the movable, a little open. The whole claw sways as one (one weight), the arm into it
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] arm = { new(0.07f * side, 0.1f, 0.25f), new(0.17f * side, 0.15f, 0.29f), new(0.25f * side, 0.15f, 0.36f), new(0.22f * side, 0.115f, 0.46f) };
                Limb(v, idx, arm, new[] { 0.035f, 0.037f, 0.034f, 0.04f }, 7, 4, TAIL, 0f, 0.5f);
                Bead(v, idx, 18, 12, new Vector3(0.222f * side, 0.112f, 0.555f), new Vector3(0.006f * side, 0f, 0.105f), new Vector2(0.074f, 0.056f), 0.9f, TAIL, _ => 0.5f);
                Limb(v, idx, new[] { new Vector3(0.245f * side, 0.112f, 0.63f), new Vector3(0.258f * side, 0.11f, 0.71f), new Vector3(0.228f * side, 0.1f, 0.78f) },
                    new[] { 0.03f, 0.018f, 0.003f }, 6, 4, TAIL, 0.5f, 0.5f, waists: false);
                Limb(v, idx, new[] { new Vector3(0.198f * side, 0.112f, 0.63f), new Vector3(0.18f * side, 0.112f, 0.7f), new Vector3(0.206f * side, 0.102f, 0.765f) },
                    new[] { 0.025f, 0.015f, 0.003f }, 6, 4, TAIL, 0.5f, 0.5f, waists: false);
            }

            //Eight legs in two tripods and a pair, as the coarse scorpion's: the femur up to a knee over the back, the tibia
            //down and the tarsus to the sand, the front pairs reaching forwards and the hind pairs back
            float[] along = { 0.14f, 0.03f, -0.08f, -0.19f };
            for (int i = 0; i < 4; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float phase = ((i % 2 == 1) == (side > 0)) ? 0f : MathF.PI;
                    float z = along[i], reach = 0.045f * (1.5f - i);
                    Vector3[] joints =
                    {
                        new(0.1f * side, 0.085f, z), new(0.24f * side, 0.185f, z + reach * 0.5f), new(0.33f * side, 0.12f, z + reach),
                        new(0.39f * side, 0.035f, z + reach * 1.4f), new(0.42f * side, 0f, z + reach * 1.6f),
                    };
                    Limb(v, idx, joints, new[] { 0.024f, 0.021f, 0.017f, 0.011f, 0.004f }, 6, 4, phase, 0f, 1f);
                }
            return (v.ToArray(), idx.ToArray());
        }

        //A point on the unit sphere: u round its Z axis, w from its back pole (Z -1) to its front (Z +1)
        private static Vector3 Unit(float u, float w)
        {
            float theta = MathHelper.TwoPi * u, phi = MathF.PI * w;
            return new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Sin(phi) * MathF.Sin(theta), -MathF.Cos(phi));
        }

        //A coordinate raised to a power keeping its sign: under one, a round outline squarer
        private static float Signed(float x, float power) => MathF.CopySign(MathF.Pow(MathF.Abs(x), power), x);

        //A quadratic Bezier curve from a through b's pull to c
        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) => (1f - t) * (1f - t) * a + 2f * t * (1f - t) * b + t * t * c;

        //A closed surface on a grid of `slices` round it by `stacks` from pole to pole, each point `at(u, w)` (u round it, the
        //seam at 0 and 1; w from pole to pole), its normal from the surface's own partial derivatives (taken a step off the
        //poles, where they vanish), turned away from `inside`; `weight` the texture coordinate's Y at each point
        private static void Surface(List<VertexPositionNormalTexture> v, List<short> idx, int slices, int stacks, Func<float, float, Vector3> at,
            Vector3 inside, float marker, Func<Vector3, float> weight)
        {
            const float STEP = 1e-3f;
            int start = v.Count;
            for (int st = 0; st <= stacks; st++)
            {
                float w = st / (float)stacks, off = Math.Clamp(w, 0.02f, 0.98f);
                for (int sl = 0; sl <= slices; sl++)
                {
                    float u = sl / (float)slices;
                    Vector3 p = at(u, w);
                    Vector3 n = Vector3.Cross(at(u + STEP, off) - at(u - STEP, off), at(u, off + STEP) - at(u, off - STEP));
                    n = n.LengthSquared() > 1e-24f ? Vector3.Normalize(n) : Vector3.Normalize(p - inside);
                    if (Vector3.Dot(n, p - inside) < 0f) n = -n;
                    v.Add(new VertexPositionNormalTexture(p, n, new Vector2(marker, weight(p))));
                }
            }
            for (int st = 0; st < stacks; st++)
                for (int sl = 0; sl < slices; sl++)
                {
                    int a = start + st * (slices + 1) + sl, b = a + slices + 1;
                    Triangle(v, idx, a, b, a + 1);
                    Triangle(v, idx, a + 1, b, b + 1);
                }
        }

        //A superellipsoid round `centre` along `axis` (its half-length), `radii` across it (to its right and up), its
        //outline squarer the further `round` is under one (1 an ellipsoid)
        private static void Bead(List<VertexPositionNormalTexture> v, List<short> idx, int slices, int stacks, Vector3 centre, Vector3 axis,
            Vector2 radii, float round, float marker, Func<Vector3, float> weight = null)
        {
            Vector3 forward = Vector3.Normalize(axis);
            Vector3 right = Vector3.Normalize(Vector3.Cross(MathF.Abs(forward.Y) > 0.95f ? Vector3.UnitZ : Vector3.Up, forward));
            Vector3 up = Vector3.Cross(forward, right);
            float length = axis.Length();
            Surface(v, idx, slices, stacks, (u, w) =>
            {
                Vector3 unit = Unit(u, w);
                return centre + right * (Signed(unit.X, round) * radii.X) + up * (Signed(unit.Y, round) * radii.Y) + forward * (Signed(unit.Z, round) * length);
            }, centre, marker, weight ?? (_ => 0f));
        }

        //A jointed limb: a tube through `joints` (a hip, its knees, a foot), its radius at each, `sides` round it and `rings`
        //along each stretch between two joints; each stretch swells in its middle and narrows at its joints, a podomere's
        //shape, unless `waists` is off. Its weight runs from `w0` at the first joint to `w1` at the last by length, as the
        //coarse legs' does from hip to foot. The hip end is left open inside the body it comes out of
        private static void Limb(List<VertexPositionNormalTexture> v, List<short> idx, Vector3[] joints, float[] radii, int sides, int rings,
            float marker, float w0, float w1, bool waists = true)
        {
            int count = (joints.Length - 1) * rings + 1;
            var centres = new Vector3[count];
            var radius = new float[count];
            for (int k = 0; k < joints.Length - 1; k++)
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    int at = k * rings + r;
                    centres[at] = Vector3.Lerp(joints[k], joints[k + 1], t);
                    radius[at] = MathHelper.Lerp(radii[k], radii[k + 1], t) * (waists ? 0.86f + 0.26f * MathF.Sin(MathF.PI * t) : 1f);
                }

            float total = 0f;
            var walked = new float[count];
            for (int i = 1; i < count; i++) walked[i] = total += Vector3.Distance(centres[i], centres[i - 1]);

            //A frame carried along it without twisting
            Vector3 dir0 = Vector3.Normalize(centres[1] - centres[0]);
            Vector3 side = Vector3.Normalize(Vector3.Cross(MathF.Abs(dir0.Y) > 0.9f ? Vector3.UnitX : Vector3.Up, dir0));
            int start = v.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Vector3.Normalize(centres[Math.Min(i + 1, count - 1)] - centres[Math.Max(i - 1, 0)]);
                side = Vector3.Normalize(side - Vector3.Dot(side, dir) * dir);
                Vector3 up = Vector3.Cross(dir, side);
                float weight = MathHelper.Lerp(w0, w1, total > 0f ? walked[i] / total : 0f);
                for (int s = 0; s <= sides; s++)
                {
                    float a = MathHelper.TwoPi * s / sides;
                    Vector3 n = side * MathF.Cos(a) + up * MathF.Sin(a);
                    v.Add(new VertexPositionNormalTexture(centres[i] + n * radius[i], n, new Vector2(marker, weight)));
                }
            }
            for (int i = 0; i < count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * (sides + 1) + s, b = a + sides + 1;
                    Triangle(v, idx, a, b, a + 1);
                    Triangle(v, idx, a + 1, b, b + 1);
                }
        }

        //An ellipsoid: a sphere of 12 x 8 scaled by the radii, normals from the scaled surface's own gradient
        private static void Ellipsoid(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 centre, Vector3 radii,
            float marker, float weight = 0f)
        {
            const int SLICES = 12, STACKS = 8;
            int start = v.Count;
            for (int st = 0; st <= STACKS; st++)
            {
                float phi = MathF.PI * st / STACKS;
                for (int sl = 0; sl <= SLICES; sl++)
                {
                    float theta = MathHelper.TwoPi * sl / SLICES;
                    var unit = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
                    Vector3 p = centre + unit * radii;
                    Vector3 n = Vector3.Normalize(unit / radii);
                    v.Add(new VertexPositionNormalTexture(p, n, new Vector2(marker, weight)));
                }
            }
            for (int st = 0; st < STACKS; st++)
                for (int sl = 0; sl < SLICES; sl++)
                {
                    int a = start + st * (SLICES + 1) + sl, b = a + SLICES + 1;
                    Triangle(v, idx, a, b, a + 1);
                    Triangle(v, idx, a + 1, b, b + 1);
                }
        }

        //A tail segment: a short ellipsoid along its own axis
        private static void Segment(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 centre, Vector3 axis, float radius,
            float marker, float weight)
        {
            int start = v.Count;
            Ellipsoid(v, idx, Vector3.Zero, new Vector3(radius, radius, axis.Length() * 0.62f), marker, weight);
            //Turned from +Z onto the axis and moved to its place
            Vector3 forward = Vector3.Normalize(axis);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));
            Vector3 up = Vector3.Cross(forward, right);
            for (int i = start; i < v.Count; i++)
            {
                VertexPositionNormalTexture x = v[i];
                Vector3 p = right * x.Position.X + up * x.Position.Y + forward * x.Position.Z + centre;
                Vector3 n = right * x.Normal.X + up * x.Normal.Y + forward * x.Normal.Z;
                v[i] = new VertexPositionNormalTexture(p, Vector3.Normalize(n), x.TextureCoordinate);
            }
        }

        //A leg: a thin tube from the hip over the knee to the foot, five-sided, its far end closed by the foot
        private static void Leg(List<VertexPositionNormalTexture> v, List<short> idx, Vector3 hip, Vector3 knee, Vector3 foot,
            float radius, float phase)
        {
            Vector3[] points = { hip, knee, foot };
            const int SIDES = 5;
            int start = v.Count;
            for (int k = 0; k < points.Length; k++)
            {
                Vector3 along = k == 0 ? points[1] - points[0] : k == points.Length - 1 ? points[k] - points[k - 1] : points[k + 1] - points[k - 1];
                along = Vector3.Normalize(along);
                Vector3 side = Vector3.Normalize(Vector3.Cross(MathF.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.Up, along));
                Vector3 up = Vector3.Cross(along, side);
                float r = radius * (k == points.Length - 1 ? 0.5f : 1f);
                float weight = k / (float)(points.Length - 1);
                for (int s = 0; s <= SIDES; s++)
                {
                    float a = MathHelper.TwoPi * s / SIDES;
                    Vector3 n = side * MathF.Cos(a) + up * MathF.Sin(a);
                    v.Add(new VertexPositionNormalTexture(points[k] + n * r, n, new Vector2(phase, weight)));
                }
            }
            for (int k = 0; k < points.Length - 1; k++)
                for (int s = 0; s < SIDES; s++)
                {
                    int a = start + k * (SIDES + 1) + s, b = a + SIDES + 1;
                    Triangle(v, idx, a, b, a + 1);
                    Triangle(v, idx, a + 1, b, b + 1);
                }
        }

        //A triangle wound clockwise seen from outside: its face normal ((b - a) x (c - a)) points into the surface, against the
        //vertices' own normals; turned over when it does not
        private static void Triangle(List<VertexPositionNormalTexture> v, List<short> idx, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(v[b].Position - v[a].Position, v[c].Position - v[a].Position);
            Vector3 normal = v[a].Normal + v[b].Normal + v[c].Normal;
            if (Vector3.Dot(face, normal) > 0f) (b, c) = (c, b);
            idx.Add((short)a);
            idx.Add((short)b);
            idx.Add((short)c);
        }
    }
}
