using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A coconut palm as real 3D geometry (#244, the leaflets #557), built the way <see cref="AcaciaMesh"/> is:
    /// two meshes, two materials, drawn as one instance. <see cref="Wood"/> is the solid part — a slim trunk
    /// that bows over on a rolled curve, the fibrous boot of old frond bases it ends in, and the bunch of
    /// coconuts hanging under the crown. <see cref="Fronds"/> is everything leafy: eight to eleven live fronds
    /// radiating from the trunk's tip and the skirt of dead ones hanging beneath them (a live crown over a bare
    /// trunk reads as a lollipop; the skirt is half of what says "palm").
    /// <para>
    /// <b>A frond is a midrib with leaflets, not a blade (#557).</b> Until #557 each frond was one tapered strip,
    /// and a crown of strips is what the owner described as "paper or plastic models at best": a strip is a
    /// flat card with one outline, and every reference of a coconut palm shows the opposite — a feathery head
    /// that the sky shows through, whose outline is a fringe. Each frond is now a narrow midrib strip carrying
    /// two rows of narrow leaflets, angled forward towards the tip and folded down from the midrib in a shallow
    /// V (the V is what makes a frond read as a frond from below — it hangs, it does not float), each leaflet
    /// bent down along its own length, longest mid-frond and short at both ends, with a few missing or torn.
    /// </para>
    /// <para>
    /// <b>The leafy mesh is SINGLE-sided and drawn with <c>CullNone</c></b>; <c>Palm.fx</c> turns the normal to
    /// whichever face the camera sees (<c>SV_IsFrontFace</c>). The strips were double-sided geometry — every quad
    /// added twice — with both faces carrying the TOP face's normal, so the underside of a frond lit exactly as
    /// its top did: nothing on a palm was ever in its own shade, which is half of why it read as a card. Drawing
    /// the leaves unculled halves the triangles and lets the underside be the underside, and the transmission
    /// term needs to know which face it is looking at anyway.
    /// </para>
    /// <para>
    /// <b>The vertex carries two numbers the shader reads.</b> UV.x is the SWAY WEIGHT: 0 on everything solid,
    /// rising along each frond towards its tip, so the wind moves the crown and never the trunk. UV.y says
    /// WHAT the vertex is and where on it, since one draw holds several materials (<see cref="Part"/>):
    /// its integer part is the part's code and its fraction a coordinate on the part — how far out along a
    /// leaflet (0 at the midrib, which is also what the midrib strip itself carries), or how far up the trunk.
    /// <c>Palm.fx</c>'s <c>PalmSurface</c> decodes it; the two files are one contract.
    /// </para>
    /// </summary>
    public sealed class PalmMesh : IDisposable
    {
        public IProceduralMesh Wood { get; }
        public IProceduralMesh Fronds { get; }

        /// <summary>The crown — the trunk's tip, where every frond starts — in the mesh's own frame. The
        /// trunk's bow carries it off the Y axis in a rolled direction, so a scatter that has to keep crowns
        /// out of somewhere (the front end's orbit, #555) asks for it rather than assuming it is overhead.</summary>
        public Vector3 Crown { get; }

        /// <summary>The furthest a frond reaches from <see cref="Crown"/>: the longest roll
        /// <c>FrondMesh</c> can give one (1.15 × the base length). A frond's spine never runs further from
        /// the crown than its own length, and the leaflets are kept inside it too (see <c>LeafletLength</c>),
        /// so this bounds the crown's reach in every direction.</summary>
        public float FrondReach { get; }

        /// <summary>
        /// What a vertex belongs to, as the integer part of its UV.y (<c>Palm.fx</c>'s <c>PalmSurface</c> holds
        /// the same table). The live fronds take one code per AGE, so a crown can yellow from the outside in —
        /// the oldest, lowest ring of fronds going ochre before it dies and hangs as the skirt.
        /// </summary>
        public enum Part
        {
            /// <summary>The trunk: UV.y = −1 − the fraction of the way up (so ≤ −1).</summary>
            Trunk = -1,
            /// <summary>A live frond of the youngest age; ages 1–3 follow at +2 each (3, 5, 7).</summary>
            LiveFrond = 1,
            /// <summary>A dead frond of the skirt.</summary>
            DeadFrond = 11,
            /// <summary>A coconut.</summary>
            Coconut = 13,
            /// <summary>The boot of old frond bases at the top of the trunk.</summary>
            Boot = 15,
        }

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="trunkRadius">Base trunk radius; the root's bole is a multiple of it (see <c>RootRadius</c>).</param>
        /// <param name="height">Height of the crown (the trunk's tip) above the ground.</param>
        /// <param name="frondLength">Base length of a crown frond — how wide the crown reads.</param>
        /// <param name="seed">Structural seed; every roll below comes off it, so no two variants are alike.</param>
        public PalmMesh(GraphicsDevice device, float trunkRadius, float height, float frondLength, int seed)
        {
            Random rng = new(seed);

            //The trunk's bow. Palms curve — grown on a shore wind, never machined straight up — and the
            //curve accumulates towards the crown (t², a palm bending under its own crown), which is why
            //the trunk cannot be a lathe: a lathe is strictly about the Y axis.
            float bow = height * (0.10f + 0.14f * (float)rng.NextDouble());
            float bowAngle = (float)rng.NextDouble() * MathHelper.TwoPi;
            Vector3 bowDir = new(MathF.Cos(bowAngle), 0f, MathF.Sin(bowAngle));
            Vector3 crown = bowDir * bow + Vector3.Up * height;
            Crown = crown;
            FrondReach = frondLength * 1.15f;

            Wood = new WoodMesh(device, trunkRadius, height, bowDir, bow, crown, rng);
            Fronds = new FrondMesh(device, crown, frondLength, rng);
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Fronds as IDisposable)?.Dispose();
        }

        /// <summary>
        /// One frond from <paramref name="crown"/> out along a horizontal direction: a narrow midrib whose spine
        /// arcs up briefly then droops (a palm frond leaves the crown pointing up and turns down under its own
        /// weight), carrying <paramref name="pairs"/> pairs of leaflets. Single-sided; the top face is the one
        /// wound front.
        /// </summary>
        /// <param name="fold">How far each leaflet is folded DOWN from the frond's plane at its base, radians —
        /// the V a frond hangs in. Deeper towards the tip, and far deeper on a dead frond.</param>
        /// <param name="code">The <see cref="Part"/> code the vertices carry.</param>
        private static void AddFrond(MeshBuilder builder, Vector3 crown, Vector3 dir, float length,
            float rise, float droop, int pairs, float fold, float leafletScale, float code, Random rng)
        {
            Vector3 perp = new(-dir.Z, 0f, dir.X);

            Vector3 Spine(float t) =>
                crown + dir * (t * length) + Vector3.Up * (length * (rise * t - droop * t * t));

            Vector3 Tangent(float t) =>
                Vector3.Normalize(dir + Vector3.Up * (rise - 2f * droop * t));

            //--- The midrib: a narrow strip tapering to the tip, carrying the leaflet base's coordinate (0), so
            //the shader colours it as the pale rachis a leaflet grows out of.
            const int RIB_SEGMENTS = 7;
            float ribHalf = length * 0.016f;
            Vector3 prevC = Spine(0f);
            float prevW = ribHalf;
            for (int s = 0; s < RIB_SEGMENTS; s++)
            {
                float t = (s + 1f) / RIB_SEGMENTS;
                Vector3 center = Spine(t);
                float half = ribHalf * (1f - 0.7f * t);
                Vector3 normal = Vector3.Normalize(Vector3.Cross(perp, center - prevC));

                float t0 = (float)s / RIB_SEGMENTS;
                Vector2 uv0 = new(t0, code), uv1 = new(t, code);
                builder.AddQuad(prevC - perp * prevW, prevC + perp * prevW, center + perp * half, center - perp * half,
                    normal, normal, normal, normal, uv0, uv0, uv1, uv1, normal);

                prevC = center;
                prevW = half;
            }

            //--- The leaflets. A station per pair along the midrib, jittered so the fringe is not a comb, and
            //none on the first seventh: the bare pale stalk a frond leaves the crown on is what the back-lit
            //references show most plainly from below, the crown's spokes.
            for (int k = 0; k < pairs; k++)
            {
                float t = 0.14f + 0.83f * (k + 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.6f) / pairs;
                Vector3 p = Spine(t);
                Vector3 tangent = Tangent(t);
                Vector3 up = Vector3.Normalize(Vector3.Cross(perp, tangent));

                //Longest a little inside mid-frond, short at the base and shorter still at the tip — the frond's
                //outline is the leaflets' lengths, a long pointed ellipse. ⚠ The tip's are kept short enough that
                //a leaflet never reaches past FrondReach: the orbit test (#555) trusts that bound.
                float profile = MathF.Sin(MathHelper.Pi * MathF.Pow((t - 0.1f) / 0.9f, 0.8f));
                float baseLength = length * 0.3f * leafletScale * (0.25f + 0.75f * profile);

                for (int side = -1; side <= 1; side += 2)
                {
                    //A few leaflets torn off or missing: a crown the sky shows through in ragged gaps rather than
                    //in a regular comb.
                    if (rng.NextDouble() < 0.07) continue;

                    //Clamped so the leaflet's reach out along the frond (at most ~0.75 of its length: it leaves
                    //the midrib at 33-45 degrees off square, and droops) never carries past the frond's own end.
                    float leafLength = MathF.Min(baseLength * (0.82f + 0.3f * (float)rng.NextDouble()),
                        (1f - t) * length * 1.3f);
                    float halfWidth = length * 0.014f * (0.85f + 0.3f * (float)rng.NextDouble());

                    //Forward towards the tip (a leaflet leaves the midrib at 50–60 degrees to it, not square)
                    //and folded down out of the frond's plane — the V.
                    float forward = 0.55f + 0.2f * (float)rng.NextDouble();
                    float down = fold * (0.7f + 0.6f * t) + ((float)rng.NextDouble() - 0.5f) * 0.25f;
                    Vector3 across = perp * side * MathF.Cos(forward) + tangent * MathF.Sin(forward);
                    Vector3 leafDir = Vector3.Normalize(across * MathF.Cos(down) - up * MathF.Sin(down));

                    //The leaflet's own width axis, and its face: turned so the face's normal is on the frond's
                    //upper side (the midrib's top face is the front face of the whole frond).
                    Vector3 widthAxis = Vector3.Normalize(Vector3.Cross(leafDir, up));
                    Vector3 face = Vector3.Normalize(Vector3.Cross(widthAxis, leafDir));
                    if (Vector3.Dot(face, up) < 0f) face = -face;

                    //Bent down along its length under its own weight: two segments, the outer one hanging.
                    Vector3 root = p + perp * side * ribHalf * (1f - 0.7f * t);
                    Vector3 mid = root + leafDir * (leafLength * 0.5f) - up * (leafLength * 0.07f);
                    Vector3 tip = root + leafDir * (leafLength * 0.95f) - up * (leafLength * 0.32f);

                    //A leaflet is creased along its centre: its two edges' normals lean out either way, so the
                    //light breaks across it rather than lying on it evenly — a flat card's tell, removed cheaply.
                    Vector3 nEdgeA = Vector3.Normalize(face + widthAxis * 0.45f);
                    Vector3 nEdgeB = Vector3.Normalize(face - widthAxis * 0.45f);
                    Vector3 tipFace = Vector3.Normalize(Vector3.Cross(widthAxis, tip - mid));
                    if (Vector3.Dot(tipFace, face) < 0f) tipFace = -tipFace;

                    float w0 = halfWidth * 0.55f, w1 = halfWidth;
                    Vector2 uvRoot = new(t, code), uvMid = new(t, code + 0.5f), uvTip = new(t, code + 0.99f);

                    builder.AddQuad(root - widthAxis * w0, root + widthAxis * w0, mid + widthAxis * w1, mid - widthAxis * w1,
                        nEdgeB, nEdgeA, nEdgeA, nEdgeB, uvRoot, uvRoot, uvMid, uvMid, face);
                    builder.AddTriangle(mid - widthAxis * w1, mid + widthAxis * w1, tip,
                        nEdgeB, nEdgeA, tipFace, uvMid, uvMid, uvTip, face);
                }
            }
        }

        /// <summary>The trunk, its boot of old frond bases and the coconuts: the palm's solids, one draw.</summary>
        private sealed class WoodMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            public WoodMesh(GraphicsDevice device, float trunkRadius, float height, Vector3 bowDir,
                float bow, Vector3 crown, Random rng)
            {
                MeshBuilder builder = new();

                //--- The trunk: one swept tube along the bow, its rings laid square to the path in the bow's own
                //plane. ⚠ It used to be a chain of separate tube segments, each building its ring frame off its
                //OWN axis, so where two segments met their rings did not share a single vertex — every joint was
                //a crack the sky showed through, a row of bright cyan ticks up every trunk in #557's before
                //captures. The bow is planar (bowDir and Up), so one binormal serves the whole trunk and each
                //ring's in-plane axis comes off the path's direction at that station, shared by the segments
                //either side of it (MeshBuilder.AddSweptTube's construction, with the radius varying).
                const int RINGS = 16, SIDES = 10;
                Vector3 binormal = Vector3.Normalize(Vector3.Cross(bowDir, Vector3.Up));

                Vector3 Centre(float t) => bowDir * (bow * t * t) + Vector3.Up * (height * t);

                Vector3[] ringDir = new Vector3[RINGS + 1];
                for (int r = 0; r <= RINGS; r++)
                {
                    float t = r / (float)RINGS;
                    Vector3 tangent = Centre(Math.Min(t + 0.01f, 1f)) - Centre(Math.Max(t - 0.01f, 0f));
                    ringDir[r] = Vector3.Normalize(Vector3.Cross(tangent, binormal));
                }

                for (int r = 0; r < RINGS; r++)
                {
                    float t0 = r / (float)RINGS, t1 = (r + 1) / (float)RINGS;
                    Vector3 c0 = Centre(t0), c1 = Centre(t1);
                    float r0 = RootRadius(trunkRadius, t0), r1 = RootRadius(trunkRadius, t1);
                    Vector2 uv0 = new(0f, (float)Part.Trunk - t0), uv1 = new(0f, (float)Part.Trunk - t1);

                    for (int s = 0; s < SIDES; s++)
                    {
                        float a0 = MathHelper.TwoPi * s / SIDES, a1 = MathHelper.TwoPi * (s + 1) / SIDES;
                        Vector3 d00 = binormal * MathF.Cos(a0) + ringDir[r] * MathF.Sin(a0);
                        Vector3 d01 = binormal * MathF.Cos(a1) + ringDir[r] * MathF.Sin(a1);
                        Vector3 d10 = binormal * MathF.Cos(a0) + ringDir[r + 1] * MathF.Sin(a0);
                        Vector3 d11 = binormal * MathF.Cos(a1) + ringDir[r + 1] * MathF.Sin(a1);

                        builder.AddQuad(c0 + d00 * r0, c0 + d01 * r0, c1 + d11 * r1, c1 + d10 * r1,
                            d00, d01, d11, d10, uv0, uv0, uv1, uv1, d00 + d01 + d11 + d10);
                    }
                }

                //--- The boot: the swollen knot of old frond bases the crown grows out of, a short fat spindle
                //over the trunk's tip. Without it the fronds sprout from the end of a pole like a feather duster.
                Vector3 axis = Vector3.Normalize(Centre(1f) - Centre(0.97f));
                Vector3 bootA = Vector3.Normalize(Vector3.Cross(axis, binormal));
                const int BOOT_RINGS = 4;
                float bootLength = trunkRadius * 3.2f;
                Vector3 bootBase = crown - axis * (bootLength * 0.55f);
                Vector2 uvBoot = new(0f, (float)Part.Boot);
                for (int r = 0; r < BOOT_RINGS; r++)
                {
                    float u0 = r / (float)BOOT_RINGS, u1 = (r + 1) / (float)BOOT_RINGS;
                    float br0 = BootRadius(trunkRadius, u0), br1 = BootRadius(trunkRadius, u1);
                    Vector3 c0 = bootBase + axis * (bootLength * u0), c1 = bootBase + axis * (bootLength * u1);

                    for (int s = 0; s < SIDES; s++)
                    {
                        float a0 = MathHelper.TwoPi * s / SIDES, a1 = MathHelper.TwoPi * (s + 1) / SIDES;
                        Vector3 d0 = binormal * MathF.Cos(a0) + bootA * MathF.Sin(a0);
                        Vector3 d1 = binormal * MathF.Cos(a1) + bootA * MathF.Sin(a1);
                        //The normals lean with the spindle's swell, so it shades as a knot and not as a drum.
                        float slope0 = (BootRadius(trunkRadius, u0 + 0.05f) - BootRadius(trunkRadius, u0 - 0.05f)) / (bootLength * 0.1f);
                        float slope1 = (BootRadius(trunkRadius, u1 + 0.05f) - BootRadius(trunkRadius, u1 - 0.05f)) / (bootLength * 0.1f);
                        Vector3 n00 = d0 - axis * slope0, n01 = d1 - axis * slope0;
                        Vector3 n10 = d0 - axis * slope1, n11 = d1 - axis * slope1;

                        builder.AddQuad(c0 + d0 * br0, c0 + d1 * br0, c1 + d1 * br1, c1 + d0 * br1,
                            n00, n01, n11, n10, uvBoot, uvBoot, uvBoot, uvBoot, d0 + d1);
                    }
                }

                //--- The coconuts: a bunch of six to ten hanging just under the crown, round the boot. Small
                //against a 22-unit palm, and from the play camera a few dark dots at the crown's heart — which is
                //exactly what they are in every reference, and what makes the crown's centre read as a crown.
                int nuts = 6 + rng.Next(5);
                float nutRadius = trunkRadius * 0.75f;
                for (int n = 0; n < nuts; n++)
                {
                    float a = MathHelper.TwoPi * n / nuts + (float)(rng.NextDouble() - 0.5) * 0.7f;
                    float drop = bootLength * (0.35f + 0.5f * (float)rng.NextDouble());
                    float reach = trunkRadius * (1.45f + 0.35f * (float)rng.NextDouble());
                    Vector3 around = binormal * MathF.Cos(a) + bootA * MathF.Sin(a);
                    Vector3 centre = crown - axis * drop + around * reach;
                    //A per-nut tint in the fraction: green, yellowing or brown.
                    AddSphere(builder, centre, nutRadius * (0.85f + 0.3f * (float)rng.NextDouble()),
                        (float)Part.Coconut + 0.99f * (float)rng.NextDouble());
                }

                (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
                VertexBuffer = vertices;
                IndexBuffer = indices;
                PrimitiveCount = primitives;
                BoundingSphere = new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), height * 0.6f + bow);
            }

            //The trunk's radius along its run: a bole flared to 1.55× at the root that has settled within
            //the first quarter of the trunk, then a near-uniform pole slimming to 0.88× at the crown.
            //
            //⚠ The flare used to be a straight taper, 1.55× to 0.85× over the WHOLE trunk (#555): at 12
            //units that read as a sturdy young palm, and at the heights the palms stand at now it made every
            //trunk a cone — 1.2× still at half height, a column rather than a pole. A coconut palm's
            //swelling is at its foot only; above it the trunk runs nearly even to the crown.
            //
            //The ring scars are not in the radius any more (#557): at eight to a trunk they were eight gentle
            //bulges nobody could see, where a real trunk carries a scar every few centimetres. They are
            //Palm.fx's now, band-limited, at the density a trunk actually has.
            private static float RootRadius(float trunkRadius, float t)
            {
                float foot = 1f - t;
                foot *= foot * foot;
                return trunkRadius * (1f - 0.12f * t + 0.55f * foot * foot);
            }

            //The boot's spindle: the trunk's own radius at its foot, swelling to 1.5× and closing over the top.
            private static float BootRadius(float trunkRadius, float u)
            {
                u = MathHelper.Clamp(u, 0f, 1f);
                return trunkRadius * (0.88f + 0.62f * MathF.Sin(MathHelper.Pi * MathF.Pow(u, 0.75f))) * (u > 0.999f ? 0.35f : 1f);
            }

            //A small UV sphere, smooth-shaded and wound outward — a coconut (MeshBuilder corrects the winding).
            private static void AddSphere(MeshBuilder builder, Vector3 centre, float radius, float code)
            {
                const int SLICES = 8, STACKS = 5;
                Vector2 uv = new(0f, code);
                for (int i = 0; i < STACKS; i++)
                {
                    float p0 = MathHelper.Pi * i / STACKS, p1 = MathHelper.Pi * (i + 1) / STACKS;
                    for (int j = 0; j < SLICES; j++)
                    {
                        float q0 = MathHelper.TwoPi * j / SLICES, q1 = MathHelper.TwoPi * (j + 1) / SLICES;
                        Vector3 n00 = SpherePoint(p0, q0), n01 = SpherePoint(p0, q1);
                        Vector3 n10 = SpherePoint(p1, q0), n11 = SpherePoint(p1, q1);
                        Vector3 face = n00 + n01 + n10 + n11;

                        if (i > 0)
                            builder.AddTriangle(centre + n00 * radius, centre + n01 * radius, centre + n11 * radius,
                                n00, n01, n11, uv, uv, uv, face);
                        if (i < STACKS - 1)
                            builder.AddTriangle(centre + n00 * radius, centre + n11 * radius, centre + n10 * radius,
                                n00, n11, n10, uv, uv, uv, face);
                    }
                }
            }

            private static Vector3 SpherePoint(float polar, float azimuth) =>
                new(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }

        /// <summary>The leaves: the live crown and the dead skirt under it, one draw.</summary>
        private sealed class FrondMesh : IProceduralMesh, IDisposable
        {
            public VertexBuffer VertexBuffer { get; private set; }
            public IndexBuffer IndexBuffer { get; private set; }
            public int PrimitiveCount { get; }
            public BoundingSphere BoundingSphere { get; }

            /// <summary>Leaflet pairs on a live frond. A real coconut frond carries a hundred or more; 26 is
            /// what keeps the leaflets a few pixels wide from the play camera (#557) — thinner and at that range
            /// they would sparkle rather than read.</summary>
            private const int LIVE_PAIRS = 40;
            private const int DEAD_PAIRS = 18;

            public FrondMesh(GraphicsDevice device, Vector3 crown, float frondLength, Random rng)
            {
                MeshBuilder builder = new();

                //Eight to eleven fronds, evenly spread with a jittered bearing. Each gets its own length, rise
                //and droop — a palm's crown is a shock of leaves at every angle it can hold, not one shape
                //revolved (the forest's variant lesson, within the one tree). The ones that droop most are the
                //oldest, and they take the older age codes: a crown yellows from its lowest fronds.
                int count = 8 + rng.Next(4);
                float baseYaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                for (int f = 0; f < count; f++)
                {
                    float bearing = baseYaw + MathHelper.TwoPi * f / count + (float)(rng.NextDouble() - 0.5) * 0.3f;
                    Vector3 dir = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));

                    float lift = (float)rng.NextDouble();
                    float droop = 0.52f + 0.22f * (float)rng.NextDouble();
                    float rise = 0.16f + 0.14f * lift;
                    int age = Math.Clamp((int)((droop - 0.52f) / 0.22f * 3.2f + (float)rng.NextDouble() * 0.9f), 0, 3);

                    AddFrond(builder, crown, dir,
                        length: frondLength * (0.8f + 0.35f * (float)rng.NextDouble()), //FrondReach is this roll's ceiling
                        rise: rise, droop: droop, pairs: LIVE_PAIRS,
                        fold: 0.5f, leafletScale: 1f,
                        code: (float)Part.LiveFrond + 2f * age, rng: rng);
                }

                //The skirt of dead fronds hanging from the crown: droop-heavy, shorter, their leaflets folded
                //nearly shut against the midrib the way a dead frond's hang. They sway too (their sway weight
                //is the frond coordinate like any frond) — dead leaves are the palm's lightest moving part.
                int skirt = 4 + rng.Next(3);
                float skirtYaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                for (int f = 0; f < skirt; f++)
                {
                    float bearing = skirtYaw + MathHelper.TwoPi * f / skirt + (float)(rng.NextDouble() - 0.5) * 0.5f;
                    Vector3 dir = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));

                    AddFrond(builder, crown, dir,
                        length: frondLength * 0.7f * (0.8f + 0.3f * (float)rng.NextDouble()),
                        rise: 0.04f + 0.03f * (float)rng.NextDouble(),
                        droop: 1.05f + 0.25f * (float)rng.NextDouble(),
                        pairs: DEAD_PAIRS, fold: 0.85f, leafletScale: 0.8f,
                        code: (float)Part.DeadFrond, rng: rng);
                }

                (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
                VertexBuffer = vertices;
                IndexBuffer = indices;
                PrimitiveCount = primitives;
                BoundingSphere = new BoundingSphere(crown, frondLength * 1.2f);
            }

            public void Dispose()
            {
                VertexBuffer?.Dispose(); VertexBuffer = null;
                IndexBuffer?.Dispose(); IndexBuffer = null;
            }
        }
    }
}
