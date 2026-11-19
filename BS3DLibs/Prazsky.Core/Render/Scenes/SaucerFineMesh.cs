using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// High and Ultra's saucer (#1173, #1174; <see cref="SceneRenderer.FineMeshes"/>), drawn by <c>SaucerFlight</c> through
    /// <c>Saucer.fx</c>'s fine techniques where Low and Medium keep <see cref="SaucerMesh"/>'s two lathes with the bulbs and the
    /// crew painted on them. The same craft at the same size in the same frame (<see cref="SaucerMesh"/>'s figures), built of
    /// the parts the references (<c>1173-zimage</c>, <c>1173-klein</c>, <c>1173-flux3</c>) draw on a classic chrome saucer, one
    /// mesh a material:
    /// <list type="bullet">
    /// <item><see cref="Part.Hull"/>: the hull lathed finer, a smooth convex top and underside, engraved panel lines (sixteen
    /// radial seams over the top and the underside and a concentric groove on each, <see cref="SEAM_DEPTH"/> deep), a raised
    /// collar the dome's glass stands in, a raised lip round the top's edge over a rounded rim, the belly's ring standing
    /// proud round a recessed glowing lens, a socket for every bulb and three landing legs, each a strut out and down to a
    /// pod. The texture coordinate's X names the material: <see cref="CHROME"/>, <see cref="LENS"/> or <see cref="GUNMETAL"/>.</item>
    /// <item><see cref="Part.Bulbs"/>: the rim's <see cref="RIM_BULBS"/> and the top's <see cref="TOP_BULBS"/> as small glass
    /// spheres standing proud of their sockets, where <c>Saucer.fx</c>'s rings paint them (X 0 the rim's, 1 the top's; Y the
    /// bulb's place round its ring, for the wave of brightness that runs round it).</item>
    /// <item><see cref="Part.Antenna"/>: split off the dome so it stays opaque under the glass's blend (X 1 the red ball).</item>
    /// <item><see cref="Part.Crew"/>: the cabin under the glass (its floor, a low amber wall round it, a console in the middle
    /// and a lamp under the crown) and the crew of three standing round the console looking out, a rounded body, a big head
    /// and large black almond eyes each, a little different in height and turn; X <see cref="SKIN"/>, <see cref="EYE"/>,
    /// <see cref="CABIN"/>, <see cref="LAMP"/> or <see cref="CONSOLE"/>.</item>
    /// <item><see cref="Part.Glass"/>: the dome alone, a shut solid whose foot stands in the collar and whose base lies under
    /// the hull's plate; drawn transparent after the opaque scene, its inside faces first.</item>
    /// </list>
    /// Every part is a set of shut solids wound clockwise seen from outside (<see cref="MeshBuilder"/> corrects each
    /// triangle against the normal it is meant to show; WindingCheck and <c>SaucerMeshTests</c> hold it).
    /// </summary>
    public sealed class SaucerFineMesh : IProceduralMesh, IDisposable
    {
        public enum Part { Hull, Bulbs, Antenna, Crew, Glass }

        //The hull's materials (the texture coordinate's X)
        internal const float CHROME = 0f, LENS = 1f, GUNMETAL = 2f;

        //The crew mesh's materials
        internal const float SKIN = 0f, EYE = 1f, CABIN = 2f, LAMP = 3f, CONSOLE = 4f;

        /// <summary>The bulbs round the rim and round the top's ring: <c>Saucer.fx</c>'s RIM_LIGHTS and TOP_LIGHTS, each centred
        /// where its ring's painted bulb is (half a step round from the ring's start), so its halo on the hull is round it.</summary>
        internal const int RIM_BULBS = 24, TOP_BULBS = 16;

        //A rim bulb's radius and how far out its middle stands (at the rim's bullnose, SaucerMesh.RIM_RADIUS); a top bulb's
        //radius and how far off the slope (along its normal) its middle stands
        internal const float RIM_BULB_RADIUS = 0.15f, RIM_BULB_OUT = SaucerMesh.RIM_RADIUS + 0.02f;
        internal const float TOP_BULB_RADIUS = 0.14f, TOP_BULB_LIFT = 0.06f;

        //The panel lines: how many panels round, a seam's half width (an arc length, the same at every radius) and its depth
        internal const int PANELS = 16;
        internal const float SEAM_HALF_WIDTH = 0.04f, SEAM_DEPTH = 0.03f;

        //Facets round the hull between two seams, round a small solid and round the glass
        private const int PANEL_FACETS = 6, PART_SEGMENTS = 12, GLASS_SEGMENTS = 96;

        //The cabin: its floor's height, the crew's three bearings round the axis (where Saucer.fx paints its figures) and
        //how far out they stand, and the lamp's height under the crown
        internal const float FLOOR_Y = SaucerMesh.DOME_BASE + 0.07f;
        internal static readonly float[] CREW_BEARINGS = { MathHelper.ToRadians(30f), MathHelper.ToRadians(150f), MathHelper.ToRadians(270f) };
        internal const float CREW_RADIUS = 1.05f;
        internal const float LAMP_Y = SaucerMesh.DOME_BASE + SaucerMesh.DOME_HEIGHT - 0.14f;

        //The legs: their bearings (in the middles of panels, so no seam runs into a strut: as near a third of a turn apart as
        //sixteen panels allow), where each strut leaves the hull and where its pod hangs (radius, height)
        internal static readonly float[] LEG_BEARINGS = { MidPanel(4), MidPanel(9), MidPanel(14) };
        private static readonly Vector2 STRUT_TOP = new(3.9f, -0.78f), POD_AT = new(5.05f, -1.62f);

        private static float MidPanel(int panel) => MathHelper.TwoPi * (panel + 0.5f) / PANELS;

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public SaucerFineMesh(GraphicsDevice graphicsDevice, Part part)
        {
            MeshBuilder builder = Build(part);
            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);
            BoundingSphere = new BoundingSphere(Vector3.Zero, SaucerMesh.RIM_RADIUS + RIM_BULB_RADIUS * 2f);
        }

        /// <summary>One part's triangles, with no device: what the constructor uploads and what a test reads.</summary>
        internal static MeshBuilder Build(Part part)
        {
            var b = new MeshBuilder();
            switch (part)
            {
                case Part.Hull: AddHull(b); AddSockets(b); AddLegs(b); break;
                case Part.Bulbs: AddBulbs(b); break;
                case Part.Antenna: AddAntenna(b); break;
                case Part.Crew: AddCabin(b); AddCrew(b); break;
                case Part.Glass: b.AddLathe(GlassProfile(), GLASS_SEGMENTS, Matrix.Identity, Vector2.Zero); break;
            }
            return b;
        }

        //---- the hull

        //One ring of the hull's profile: its radius and height, whether the profile creases there, whether the radial seams
        //are cut through it, and the material of the span that starts at it
        private readonly record struct Ring(float R, float Y, bool Crease, bool Seam, float Material);

        //The top's slope from the collar to the lip, and the underside's from the rim to the belly's ring: smooth convex
        //curves through the coarse hull's points (SaucerMesh.HULL), so the craft keeps its outline. The top passes through
        //SaucerMesh.TOP_RING's height at its radius, where the top's bulbs stand
        internal static float TopY(float r) => 0.88777f + 0.05277f * r - 0.023665f * r * r;
        internal static float UnderY(float r) => -1.19753f + 0.0213f * r + 0.016041f * r * r;

        //The slope's normal in the (radius, y) plane (up and out) at r
        private static Vector2 TopNormal(float r) => Vector2.Normalize(new Vector2(-(0.05277f - 0.04733f * r), 1f));

        private const float SLOPE_FROM = 2.78f, SLOPE_TO = 6.6f, UNDER_FROM = 6.72f, UNDER_TO = 3.32f;
        private const float TOP_GROOVE_A = 3.9f, TOP_GROOVE_B = 6.05f, UNDER_GROOVE = 5.38f, GROOVE_HALF = 0.04f;

        /// <summary>The concentric grooves' radii, two round the top and one round the underside: <c>Saucer.fx</c>'s
        /// <c>PanelGrooves</c>, which part the panels it rolls a shade for.</summary>
        internal static Vector3 Grooves => new(TOP_GROOVE_A, TOP_GROOVE_B, UNDER_GROOVE);

        internal static List<(float R, float Y, bool Crease, bool Seam, float Material)> HullProfile()
        {
            var p = new List<Ring>
            {
                //The plate under the cabin, then the collar the glass stands in (SaucerMesh.DOME_RADIUS), its top a dark band
                new(0f, 0.88f, false, false, CHROME),
                new(2.33f, 0.88f, true, false, CHROME),
                new(2.33f, 1.03f, true, false, GUNMETAL),
                new(2.4f, 1.075f, false, false, GUNMETAL),
                new(2.52f, 1.09f, false, false, GUNMETAL),
                new(2.64f, 1.075f, false, false, GUNMETAL),
                new(2.72f, 1.02f, true, false, CHROME),
            };

            //The top's slope, a groove cut round it twice
            Slope(p, SLOPE_FROM, SLOPE_TO, 18, TopY, TOP_GROOVE_A, TOP_GROOVE_B, top: true);

            //The lip round the top's edge, then the rim's bullnose, where the rim's bulbs stand (SaucerMesh.RIM_RADIUS at its
            //widest, its middle at the middle of SaucerMesh.RIM_BAND)
            p.Add(new(6.63f, 0.25f, true, false, GUNMETAL));
            p.Add(new(6.75f, 0.25f, true, false, GUNMETAL));
            p.Add(new(6.79f, 0.21f, true, false, CHROME));
            p.Add(new(6.8f, 0.075f, true, false, CHROME));
            float rimY = 0.5f * (SaucerMesh.RIM_BAND.X + SaucerMesh.RIM_BAND.Y), bull = 0.17f;
            for (int i = 0; i <= 8; i++)
            {
                float a = MathHelper.PiOver2 - MathHelper.Pi * i / 8f;
                p.Add(new(SaucerMesh.RIM_RADIUS - bull + bull * MathF.Cos(a), rimY + bull * MathF.Sin(a), i == 0 || i == 8, false, CHROME));
            }

            //The underside in to the belly's ring, a groove round it
            Slope(p, UNDER_FROM, UNDER_TO, 14, UnderY, UNDER_GROOVE, float.NaN, top: false);

            //The belly's ring standing proud, and the lens recessed inside it
            p.Add(new(3.3f, -1.02f, false, false, CHROME));
            p.Add(new(3.24f, -1.09f, false, false, CHROME));
            p.Add(new(3.13f, -1.135f, false, false, CHROME));
            p.Add(new(3.0f, -1.14f, false, false, CHROME));
            p.Add(new(2.9f, -1.11f, false, false, CHROME));
            p.Add(new(2.84f, -1.02f, true, false, LENS));
            p.Add(new(1.4f, -1.03f, false, false, LENS));
            p.Add(new(0f, -1.04f, false, false, LENS));

            var result = new List<(float, float, bool, bool, float)>(p.Count);
            foreach (Ring ring in p) result.Add((ring.R, ring.Y, ring.Crease, ring.Seam, ring.Material));
            return result;
        }

        //A slope sampled from `from` to `to` (either way round) on `y`, with a V groove cut round it at each of the two radii
        //given (NaN for none), every ring of it cut by the radial seams
        private static void Slope(List<Ring> p, float from, float to, int steps, Func<float, float> y, float grooveA, float grooveB, bool top)
        {
            float direction = MathF.Sign(to - from);
            for (int i = 0; i <= steps; i++)
            {
                float r = MathHelper.Lerp(from, to, i / (float)steps);
                p.Add(new(r, y(r), i == 0 || i == steps, true, CHROME));

                //A groove between this ring and the next, when one falls there
                float next = MathHelper.Lerp(from, to, (i + 1) / (float)steps);
                foreach (float groove in new[] { grooveA, grooveB })
                {
                    if (float.IsNaN(groove) || i == steps || (groove - r) * direction <= GROOVE_HALF || (next - groove) * direction <= GROOVE_HALF)
                        continue;
                    Vector2 n = top ? TopNormal(groove) : -Vector2.Normalize(new Vector2(-(0.0213f + 0.032082f * groove), 1f));
                    float before = groove - direction * GROOVE_HALF, after = groove + direction * GROOVE_HALF;
                    p.Add(new(before, y(before), true, true, CHROME));
                    p.Add(new(groove - n.X * SEAM_DEPTH, y(groove) - n.Y * SEAM_DEPTH, true, true, CHROME));
                    p.Add(new(after, y(after), true, true, CHROME));
                }
            }
        }

        /// <summary>
        /// The hull: <see cref="HullProfile"/> revolved, the radial seams cut into it. Each panel between two seams is its own
        /// strip of facets with normals smoothed round it, and each of a seam's two walls is a strip of its own, its normal the
        /// surface's tipped across by the wall's slope: a V groove whose edges stay sharp, which a strip smoothed across the
        /// seam would round away. A wall's columns are placed by arc length (<see cref="SEAM_HALF_WIDTH"/> either side of the
        /// seam, at most a fifth of a panel near the axis), so a seam is as wide at the dome's foot as at the rim. A ring the
        /// seams do not cut has its walls on the surface, shaded as the surface.
        /// </summary>
        private static void AddHull(MeshBuilder b)
        {
            List<(float R, float Y, bool Crease, bool Seam, float Material)> profile = HullProfile();
            int rings = profile.Count, spans = rings - 1;

            //Span normals in the (radius, y) plane and each ring's either side, as MeshBuilder.AddLathe takes them
            var spanNormal = new Vector2[spans];
            for (int s = 0; s < spans; s++)
            {
                var t = new Vector2(profile[s + 1].R - profile[s].R, profile[s + 1].Y - profile[s].Y);
                spanNormal[s] = t.LengthSquared() > 1e-12f ? Vector2.Normalize(new Vector2(-t.Y, t.X)) : Vector2.UnitY;
            }
            var below = new Vector2[rings];
            var above = new Vector2[rings];
            //And the one way each ring's seam is sunk, whichever span it is drawn with: by a crease's own normal either side,
            //the two spans cut the seam's middle at two points and the hull opened along every creased ring
            var sink = new Vector2[rings];
            for (int k = 0; k < rings; k++)
            {
                Vector2 before = spanNormal[Math.Max(0, k - 1)], after = spanNormal[Math.Min(spans - 1, k)];
                if (profile[k].Crease || k == 0 || k == rings - 1) { below[k] = before; above[k] = after; }
                else below[k] = above[k] = Vector2.Normalize(before + after);
                sink[k] = Vector2.Normalize(before + after);
            }

            float panel = MathHelper.TwoPi / PANELS;
            for (int s = 0; s < spans; s++)
            {
                var low = profile[s];
                var high = profile[s + 1];
                if (low.R == high.R && low.Y == high.Y) continue;
                var uv = new Vector2(low.Material, 0f);

                for (int p = 0; p < PANELS; p++)
                {
                    float seam = p * panel, nextSeam = (p + 1) * panel;

                    //The wall up out of this seam, the panel, and the wall down into the next seam: columns as (angle at the
                    //low ring, angle at the high ring, inset at each, tilt) where the tilt is -1 or 1 across a wall, 0 on a panel
                    Strip(b, low, high, above[s], below[s + 1], sink[s], sink[s + 1], spanNormal[s], uv, seam, 0f, 1, -1f, panel);
                    for (int c = 0; c < PANEL_FACETS; c++)
                        Strip(b, low, high, above[s], below[s + 1], sink[s], sink[s + 1], spanNormal[s], uv, seam, nextSeam, c, 0f, panel);
                    Strip(b, low, high, above[s], below[s + 1], sink[s], sink[s + 1], spanNormal[s], uv, nextSeam, 0f, 1, 1f, panel);
                }
            }
        }

        //The angular offset of a seam's edge from its middle at radius r: SEAM_HALF_WIDTH of arc, at most a fifth of a panel
        private static float SeamAngle(float r, float panel) => MathF.Min(SEAM_HALF_WIDTH / MathF.Max(r, 1e-4f), panel * 0.2f);

        //One column of facets up a span. With `tilt` 0 it is facet `column` of the panel from `seam` to `nextSeam`, between the
        //two seams' edges; with tilt -1 the wall from `seam`'s middle (inset) out to its edge on the side towards larger
        //angles, with tilt 1 the wall from `seam`'s edge on the side towards smaller angles in to its middle
        private static void Strip(MeshBuilder b, (float R, float Y, bool Crease, bool Seam, float Material) low,
            (float R, float Y, bool Crease, bool Seam, float Material) high, Vector2 lowNormal, Vector2 highNormal,
            Vector2 lowSink, Vector2 highSink, Vector2 spanNormal, Vector2 uv, float seam, float nextSeam, int column, float tilt, float panel)
        {
            Corner(low, lowNormal, lowSink, 0, out Vector3 p00, out Vector3 n00);
            Corner(low, lowNormal, lowSink, 1, out Vector3 p10, out Vector3 n10);
            Corner(high, highNormal, highSink, 1, out Vector3 p11, out Vector3 n11);
            Corner(high, highNormal, highSink, 0, out Vector3 p01, out Vector3 n01);

            //The winding follows the surface's own normal at the facet's middle
            float mid = 0.5f * (AngleOf(low.R, 0) + AngleOf(low.R, 1));
            var face = new Vector3(spanNormal.X * MathF.Cos(mid), spanNormal.Y, spanNormal.X * MathF.Sin(mid));

            if (low.R <= 0f) b.AddTriangle(p01, p11, p00, n01, n11, n00, uv, uv, uv, face);
            else if (high.R <= 0f) b.AddTriangle(p00, p10, p01, n00, n10, n01, uv, uv, uv, face);
            else b.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, uv, uv, uv, uv, face);

            //Where the column's `side` (0 its smaller angle, 1 its larger) is at radius r
            float AngleOf(float r, int side)
            {
                float edge = SeamAngle(r, panel);
                if (tilt < 0f) return seam + (side == 0 ? 0f : edge);
                if (tilt > 0f) return seam + (side == 0 ? -edge : 0f);
                float from = seam + edge, to = nextSeam - edge;
                return MathHelper.Lerp(from, to, (column + side) / (float)PANEL_FACETS);
            }

            void Corner((float R, float Y, bool Crease, bool Seam, float Material) ring, Vector2 normal, Vector2 sink, int side,
                out Vector3 position, out Vector3 vertexNormal)
            {
                float angle = AngleOf(ring.R, side);
                (float sin, float cos) = MathF.SinCos(angle);
                var radial = new Vector3(cos, 0f, sin);
                var around = new Vector3(-sin, 0f, cos);
                Vector3 surface = radial * normal.X + Vector3.Up * normal.Y;
                position = radial * ring.R + Vector3.Up * ring.Y;
                vertexNormal = surface;
                if (tilt == 0f || !ring.Seam || ring.R <= 0f) return;

                //A wall: its middle edge sunk by the seam's depth, its normal tipped across by the wall's slope (depth over
                //half width), away from the seam's middle
                bool middle = tilt < 0f ? side == 0 : side == 1;
                if (middle) position -= (radial * sink.X + Vector3.Up * sink.Y) * SEAM_DEPTH;
                float halfWidth = ring.R * SeamAngle(ring.R, panel);
                vertexNormal = Vector3.Normalize(surface + around * (tilt * SEAM_DEPTH / halfWidth));
            }
        }

        //A socket for every bulb, in gunmetal: a short drum the bulb sits in, its back buried in the hull
        private static void AddSockets(MeshBuilder b)
        {
            var uv = new Vector2(GUNMETAL, 0f);
            float rimY = 0.5f * (SaucerMesh.RIM_BAND.X + SaucerMesh.RIM_BAND.Y);
            LathePoint[] rimSocket = Drum(0.165f, 0f, SaucerMesh.RIM_RADIUS + 0.03f - 6.84f);
            for (int i = 0; i < RIM_BULBS; i++)
                b.AddLathe(rimSocket, PART_SEGMENTS, Along(BulbBearing(i, RIM_BULBS), new Vector3(6.84f, rimY, 0f), Vector3.UnitX), uv);

            LathePoint[] topSocket = Drum(0.17f, 0f, 0.1f);
            for (int i = 0; i < TOP_BULBS; i++)
            {
                (Vector3 at, Vector3 normal) = OnTop(SaucerMesh.TOP_RING.X);
                b.AddLathe(topSocket, PART_SEGMENTS, Along(BulbBearing(i, TOP_BULBS), at - normal * 0.06f, normal), uv);
            }
        }

        //The legs: a strut from inside the hull out and down to a pod lying along the bearing, its ends pointed
        private static void AddLegs(MeshBuilder b)
        {
            var uv = new Vector2(CHROME, 0f);
            var strutTop = new Vector3(STRUT_TOP.X, STRUT_TOP.Y, 0f);
            var pod = new Vector3(POD_AT.X, POD_AT.Y, 0f);
            Vector3 strut = pod - strutTop;
            LathePoint[] strutProfile = Drum(0.07f, 0f, strut.Length());
            LathePoint[] podProfile =
            {
                new(0f, 0.36f), new(0.07f, 0.33f), new(0.15f, 0.24f), new(0.2f, 0.1f), new(0.21f, 0f),
                new(0.19f, -0.12f), new(0.13f, -0.24f), new(0.06f, -0.31f), new(0f, -0.34f)
            };
            foreach (float bearing in LEG_BEARINGS)
            {
                b.AddLathe(strutProfile, PART_SEGMENTS, Along(bearing, strutTop, Vector3.Normalize(strut)), uv);
                //The pod along the bearing, its nose out, tipped a little nose-down
                b.AddLathe(podProfile, PART_SEGMENTS, Along(bearing, pod, Vector3.Normalize(new Vector3(1f, -0.12f, 0f))), uv);
            }
        }

        //---- the bulbs

        private static void AddBulbs(MeshBuilder b)
        {
            float rimY = 0.5f * (SaucerMesh.RIM_BAND.X + SaucerMesh.RIM_BAND.Y);
            LathePoint[] rim = Sphere(RIM_BULB_RADIUS, 7), top = Sphere(TOP_BULB_RADIUS, 7);
            for (int i = 0; i < RIM_BULBS; i++)
                b.AddLathe(rim, PART_SEGMENTS, Along(BulbBearing(i, RIM_BULBS), new Vector3(RIM_BULB_OUT, rimY, 0f), Vector3.UnitX),
                    new Vector2(0f, i));
            for (int i = 0; i < TOP_BULBS; i++)
            {
                (Vector3 at, Vector3 normal) = OnTop(SaucerMesh.TOP_RING.X);
                b.AddLathe(top, PART_SEGMENTS, Along(BulbBearing(i, TOP_BULBS), at + normal * TOP_BULB_LIFT, normal), new Vector2(1f, i));
            }
        }

        /// <summary>Bulb <paramref name="i"/>'s bearing round a ring of <paramref name="count"/>: the middle of its slot, where
        /// <c>Saucer.fx</c>'s <c>Bulbs</c> and <c>BulbHalo</c> centre it.</summary>
        internal static float BulbBearing(int i, int count) => MathHelper.TwoPi * (i + 0.5f) / count;

        /// <summary>Where a top bulb's middle stands in the craft's frame (#1173): a test's handle on the parts.</summary>
        internal static Vector3 TopBulbCentre(int i)
        {
            (Vector3 at, Vector3 normal) = OnTop(SaucerMesh.TOP_RING.X);
            return Vector3.Transform(at + normal * TOP_BULB_LIFT, Matrix.CreateRotationY(-BulbBearing(i, TOP_BULBS)));
        }

        //A point of the top's slope at radius r on bearing 0 (+X), and its normal
        private static (Vector3 At, Vector3 Normal) OnTop(float r)
        {
            Vector2 n = TopNormal(r);
            return (new Vector3(r, TopY(r), 0f), new Vector3(n.X, n.Y, 0f));
        }

        //---- the antenna

        //From the dome's crown (SaucerMesh's DOME_BASE + DOME_HEIGHT): a base cap on the glass, a thin rod with a ring round it
        //and the red ball at its top, the coarse dome's antenna's height
        internal const float ANTENNA_TOP = SaucerMesh.DOME_BASE + SaucerMesh.DOME_HEIGHT + 0.98f;

        private static void AddAntenna(MeshBuilder b)
        {
            float crown = SaucerMesh.DOME_BASE + SaucerMesh.DOME_HEIGHT;
            LathePoint[] mast =
            {
                new(0f, crown + 0.8f), new(0.032f, crown + 0.79f, crease: true),
                new(0.032f, crown + 0.42f, crease: true), new(0.055f, crown + 0.41f), new(0.065f, crown + 0.37f), new(0.055f, crown + 0.33f),
                new(0.032f, crown + 0.32f, crease: true), new(0.032f, crown + 0.12f, crease: true),
                new(0.1f, crown + 0.1f), new(0.16f, crown + 0.05f), new(0.17f, crown - 0.01f, crease: true), new(0f, crown - 0.04f)
            };
            b.AddLathe(mast, PART_SEGMENTS, Matrix.Identity, new Vector2(0f, 0f));
            float ball = 0.11f;
            b.AddLathe(Sphere(ball, 8), PART_SEGMENTS, Matrix.CreateTranslation(0f, ANTENNA_TOP - ball, 0f), new Vector2(1f, 0f));
        }

        //---- the glass

        //The dome alone: from the crown down to its foot in the collar (SaucerMesh's DOME_BASE, DOME_RADIUS), then a short wall
        //and a disc under the hull's plate, so it is shut and the foot of the glass is never seen
        internal static LathePoint[] GlassProfile()
        {
            const int RINGS = 18;
            var glass = new LathePoint[RINGS + 3];
            for (int i = 0; i <= RINGS; i++)
            {
                float a = MathHelper.PiOver2 * i / RINGS;
                glass[i] = new LathePoint(MathF.Sin(a) * SaucerMesh.DOME_RADIUS, SaucerMesh.DOME_BASE + MathF.Cos(a) * SaucerMesh.DOME_HEIGHT,
                    crease: i == RINGS);
            }
            glass[RINGS + 1] = new LathePoint(SaucerMesh.DOME_RADIUS, SaucerMesh.DOME_BASE - 0.04f, crease: true);
            glass[RINGS + 2] = new LathePoint(0f, SaucerMesh.DOME_BASE - 0.04f);
            return glass;
        }

        /// <summary>The glass's height at radius <paramref name="r"/> off the axis: where a crew member's head must stay under.</summary>
        internal static float GlassY(float r)
        {
            float s = MathF.Min(r / SaucerMesh.DOME_RADIUS, 1f);
            return SaucerMesh.DOME_BASE + MathF.Sqrt(1f - s * s) * SaucerMesh.DOME_HEIGHT;
        }

        //---- the cabin and the crew

        private static void AddCabin(MeshBuilder b)
        {
            //The floor, its underside in the hull's plate
            b.AddLathe(new LathePoint[] { new(0f, FLOOR_Y), new(2.27f, FLOOR_Y, crease: true), new(2.27f, 0.85f, crease: true), new(0f, 0.85f) },
                GLASS_SEGMENTS / 2, Matrix.Identity, new Vector2(CABIN, 0f));

            //A low wall round it inside the glass, a ring of square section: the amber the crew stand against
            b.AddLathe(new LathePoint[]
            {
                new(2.0f, 1.18f, crease: true), new(2.22f, 1.18f, crease: true), new(2.22f, FLOOR_Y - 0.02f, crease: true),
                new(2.0f, FLOOR_Y - 0.02f, crease: true), new(2.0f, 1.18f, crease: true)
            }, GLASS_SEGMENTS / 2, Matrix.Identity, new Vector2(CABIN, 0f));

            //The console in the middle and its lit top
            b.AddLathe(new LathePoint[]
            {
                new(0f, FLOOR_Y + 0.5f), new(0.24f, FLOOR_Y + 0.5f, crease: true), new(0.3f, FLOOR_Y + 0.44f),
                new(0.2f, FLOOR_Y + 0.22f), new(0.27f, FLOOR_Y + 0.02f, crease: true), new(0f, FLOOR_Y - 0.03f)
            }, PART_SEGMENTS * 2, Matrix.Identity, new Vector2(CABIN, 0f));
            b.AddLathe(Drum(0.2f, FLOOR_Y + 0.48f, FLOOR_Y + 0.53f), PART_SEGMENTS * 2, Matrix.Identity, new Vector2(CONSOLE, 0f));

            //The lamp under the crown, a flattened disc
            b.AddLathe(Sphere(1f, 8), PART_SEGMENTS * 2, Matrix.CreateScale(0.28f, 0.05f, 0.28f) * Matrix.CreateTranslation(0f, LAMP_Y, 0f),
                new Vector2(LAMP, 0f));
        }

        //Each one's height (a share of the first's) and how far it turns from straight out, so the three are not one figure
        private static readonly float[] CREW_SCALE = { 1.0f, 0.93f, 1.06f };
        private static readonly float[] CREW_TURN = { 0f, MathHelper.ToRadians(-14f), MathHelper.ToRadians(11f) };
        private static readonly float[] HEAD_TILT = { MathHelper.ToRadians(6f), MathHelper.ToRadians(-4f), 0f };
        internal const float CREW_HEIGHT = 1.4f;

        private static void AddCrew(MeshBuilder b)
        {
            for (int k = 0; k < CREW_BEARINGS.Length; k++)
            {
                //Its own frame (standing at the origin on the floor, facing +Z, CREW_HEIGHT tall) to the craft's: turned so +Z
                //faces out along its bearing (plus its turn) and stood at CREW_RADIUS on the floor
                float facing = MathHelper.PiOver2 - CREW_BEARINGS[k] + CREW_TURN[k];
                Matrix place = Matrix.CreateScale(CREW_SCALE[k] * CREW_HEIGHT / 1.21f) * Matrix.CreateRotationY(facing)
                    * Matrix.CreateTranslation(MathF.Cos(CREW_BEARINGS[k]) * CREW_RADIUS, FLOOR_Y, MathF.Sin(CREW_BEARINGS[k]) * CREW_RADIUS);
                AddFigure(b, place, HEAD_TILT[k]);
            }
        }

        //One figure, 1.21 tall in its own frame: a body narrow at the waist with sloping shoulders, thin arms, a big head wider
        //at the brow than at the chin, and two large black almond eyes slanting up at their outer corners
        private static void AddFigure(MeshBuilder b, Matrix place, float headTilt)
        {
            var skin = new Vector2(SKIN, 0f);
            LathePoint[] body =
            {
                new(0f, 0.72f), new(0.06f, 0.715f), new(0.07f, 0.66f), new(0.14f, 0.6f), new(0.16f, 0.53f), new(0.135f, 0.44f),
                new(0.11f, 0.36f), new(0.13f, 0.24f), new(0.15f, 0.1f), new(0.14f, 0.02f, crease: true), new(0f, 0f)
            };
            b.AddLathe(body, PART_SEGMENTS, Matrix.CreateScale(1f, 1f, 0.78f) * place, skin);

            //The arms hang from the shoulders, a little away from the body
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = new Vector3(0.145f * side, 0.56f, 0f);
                var hand = new Vector3(0.2f * side, 0.24f, 0.04f);
                Vector3 arm = hand - shoulder;
                b.AddLathe(Capsule(0.032f, arm.Length()), 8, Along(0f, shoulder, Vector3.Normalize(arm)) * place, skin);
            }

            //The head about the neck, tilted a little
            Matrix head = Matrix.CreateTranslation(0f, -0.7f, 0f) * Matrix.CreateRotationZ(headTilt) * Matrix.CreateTranslation(0f, 0.7f, 0f) * place;
            LathePoint[] skull =
            {
                new(0f, 1.21f), new(0.12f, 1.195f), new(0.2f, 1.155f), new(0.255f, 1.09f), new(0.275f, 1.0f), new(0.265f, 0.92f),
                new(0.225f, 0.84f), new(0.16f, 0.77f), new(0.09f, 0.72f), new(0.04f, 0.70f), new(0f, 0.695f)
            };
            b.AddLathe(skull, PART_SEGMENTS + 4, Matrix.CreateScale(1f, 1f, 0.9f) * head, skin);

            //The eyes: flattened ellipsoids sunk into the face, turned to follow it and slanting up outwards
            for (int side = -1; side <= 1; side += 2)
            {
                Matrix eye = Matrix.CreateScale(0.1f, 0.052f, 0.035f) * Matrix.CreateRotationZ(side * MathHelper.ToRadians(24f))
                    * Matrix.CreateRotationY(side * MathHelper.ToRadians(26f)) * Matrix.CreateTranslation(0.105f * side, 0.935f, 0.205f) * head;
                b.AddLathe(Sphere(1f, 6), PART_SEGMENTS, eye, new Vector2(EYE, 0f));
            }
        }

        //---- shapes

        //A sphere's profile, pole to pole, `rings` spans
        private static LathePoint[] Sphere(float radius, int rings)
        {
            var points = new LathePoint[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = MathHelper.Pi * i / rings;
                points[i] = new LathePoint(MathF.Sin(a) * radius, MathF.Cos(a) * radius);
            }
            return points;
        }

        //A closed drum of `radius` from `from` to `to` along its axis, its rims creased
        private static LathePoint[] Drum(float radius, float from, float to) =>
            new LathePoint[] { new(0f, to), new(radius, to, crease: true), new(radius, from, crease: true), new(0f, from) };

        //A capsule from 0 to `length` along its axis
        private static LathePoint[] Capsule(float radius, float length)
        {
            var points = new List<LathePoint>();
            for (int i = 0; i <= 4; i++)
            {
                float a = MathHelper.PiOver2 * i / 4f;
                points.Add(new LathePoint(MathF.Sin(a) * radius, length + MathF.Cos(a) * radius));
            }
            for (int i = 0; i <= 4; i++)
            {
                float a = MathHelper.PiOver2 + MathHelper.PiOver2 * i / 4f;
                points.Add(new LathePoint(MathF.Sin(a) * radius, MathF.Cos(a) * radius));
            }
            return points.ToArray();
        }

        /// <summary>
        /// A solid's placement: its own Y axis laid along <paramref name="axis"/> (given in the frame of bearing 0, +X out
        /// from the craft's axis) with its origin at <paramref name="at"/> in that frame, and the whole turned round the
        /// craft's axis to <paramref name="bearing"/> (radians from +X towards +Z, as <c>Saucer.fx</c>'s <c>atan2(z, x)</c>).
        /// </summary>
        private static Matrix Along(float bearing, Vector3 at, Vector3 axis)
        {
            Vector3 side = MathF.Abs(axis.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX;
            Vector3 x = Vector3.Normalize(Vector3.Cross(axis, side));
            Vector3 z = Vector3.Cross(x, axis);
            var basis = new Matrix(x.X, x.Y, x.Z, 0f, axis.X, axis.Y, axis.Z, 0f, z.X, z.Y, z.Z, 0f, at.X, at.Y, at.Z, 1f);
            return basis * Matrix.CreateRotationY(-bearing);
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }
}
