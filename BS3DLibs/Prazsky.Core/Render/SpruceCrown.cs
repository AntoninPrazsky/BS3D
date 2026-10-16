using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The whorls of one spruce crown as <see cref="TreeMesh"/> rolled them for its lathe: each tier's skirt edge (its
    /// radius and the height it droops to), the crown's span and the lathe's wobble. What High and Ultra's crown
    /// (<see cref="SpruceCrownGeometry"/>, #733) lays its boughs out by, so the two crowns of one variant are one tree:
    /// the same height, the same width at every height and the same lean of the wobble, where one is a stack of skirts and
    /// the other a stand of boughs.
    /// </summary>
    public sealed class SpruceWhorlPlan
    {
        /// <summary>The crown's authored radius (the widest skirt before its jitter) and height.</summary>
        public float Radius { get; }
        public float Height { get; }

        /// <summary>Where the crown starts: the trunk's top.</summary>
        public float BaseY { get; }

        /// <summary>The lathe's wobble phase and peak amplitude (<see cref="LatheMesh.Irregularity"/>).</summary>
        public float Phase { get; }
        public float WobbleAmplitude { get; }

        /// <summary>How uneven the whorls are (<see cref="ForestTreeConfig.ConiferRaggedness"/>).</summary>
        public float Raggedness { get; }

        /// <summary>Each tier's skirt edge, top tier first: its radius and the height it droops to.</summary>
        public IReadOnlyList<float> SkirtRadii { get; }
        public IReadOnlyList<float> SkirtHeights { get; }

        /// <summary>The crown's tip.</summary>
        public float Top => BaseY + Height;

        /// <summary>
        /// The farthest the lathe reaches from the axis: its widest skirt and the wobble's peak. The boughs reach no further,
        /// so a host keeping a camera out of the tree by the lathe's figures keeps it out of either crown.
        /// </summary>
        public float Reach { get; }

        public SpruceWhorlPlan(float radius, float height, float baseY, float phase, float wobbleAmplitude, float raggedness,
            float[] skirtRadii, float[] skirtHeights)
        {
            Radius = radius;
            Height = height;
            BaseY = baseY;
            Phase = phase;
            WobbleAmplitude = wobbleAmplitude;
            Raggedness = raggedness;
            SkirtRadii = skirtRadii;
            SkirtHeights = skirtHeights;

            float widest = 0f;
            foreach (float r in skirtRadii) widest = MathF.Max(widest, r);
            Reach = widest + wobbleAmplitude;
        }

        /// <summary>
        /// The crown's outline at a height: the hull through the skirt edges, from the tip down, and the lowest skirt's
        /// radius below it. The lathe's tucks run inside it; the boughs reach out to it.
        /// </summary>
        public float Envelope(float y)
        {
            if (y >= Top) return 0f;

            float aboveRadius = 0f, aboveY = Top;
            for (int t = 0; t < SkirtRadii.Count; t++)
            {
                float r = SkirtRadii[t], h = SkirtHeights[t];
                if (y >= h)
                {
                    float span = MathF.Max(aboveY - h, 1e-4f);
                    return MathHelper.Lerp(r, aboveRadius, (y - h) / span);
                }
                aboveRadius = r;
                aboveY = h;
            }
            return aboveRadius;
        }
    }

    /// <summary>
    /// How finely a <see cref="SpruceCrownGeometry"/> is built: how far apart its whorls stand, how many arms each has and
    /// how many rings an arm is made of. Two of them (#733): <see cref="Near"/> for the spruces round the arena, where a
    /// tree stands large on the screen, and <see cref="Far"/> for the rest of the wood — the same kind of crown, arms that
    /// leave the stem, hang and turn up, at about a fifth of the triangles. The first build, every spruce of the aurora's eight
    /// hundred at full detail, cost 0.92 ms a frame, and the cost is the triangles: it measured the same at a quarter of
    /// the pixels.
    /// </summary>
    public sealed class SpruceDetail
    {
        /// <summary>World units between two whorls, before the jitter.</summary>
        public float WhorlSpacing { get; init; }

        /// <summary>Arms a whorl rolls, from the first figure up by the second (exclusive), and the same for the smaller
        /// arms between two whorls.</summary>
        public int Arms { get; init; }
        public int ArmsSpread { get; init; }
        public int Between { get; init; }
        public int BetweenSpread { get; init; }

        /// <summary>An arm's half-width as a share of its run.</summary>
        public float Width { get; init; }

        /// <summary>
        /// Where along an arm its rings stand (from the stem, 0, to short of the tip, which closes it), and the foliage's
        /// half-width, the fringe's hang and the ridge's rise at each, as shares of the arm's maxima. The hang rises and
        /// falls ring by ring where there are rings enough: the branchlets' tips seen from the side.
        /// </summary>
        public float[] RingT { get; init; }
        public float[] RingWidth { get; init; }
        public float[] RingHang { get; init; }
        public float[] RingRidge { get; init; }

        /// <summary>The rings an arm shorter than <see cref="SpruceCrownGeometry.SHORT_ARM"/> keeps; null keeps them all.</summary>
        public int[] ShortRings { get; init; }

        /// <summary>How many of the arms carry a lump of snow, against the near crown's share.</summary>
        public float SnowShare { get; init; } = 1f;

        /// <summary>
        /// The interior's radius as a share of the outline (<see cref="SpruceCrownGeometry.ShadeProfileFor"/>): narrow under
        /// the near crown's many arms, fatter under the far crown's few, which alone left a far spruce a pole with a few
        /// sprigs on it — a stand read as a wood of masts.
        /// </summary>
        public float Shade { get; init; }

        /// <summary>Added to the seed, so the two details of one variant roll their own arms.</summary>
        public int Salt { get; init; }

        /// <summary>The spruces round the arena: a whorl every 1.15 units, five rings an arm.</summary>
        public static readonly SpruceDetail Near = new()
        {
            WhorlSpacing = 1.15f,
            Arms = 5, ArmsSpread = 3, Between = 1, BetweenSpread = 2,
            Width = 0.13f,
            RingT = new[] { 0f, 0.22f, 0.45f, 0.66f, 0.86f },
            RingWidth = new[] { 0.25f, 0.75f, 1f, 0.8f, 0.45f },
            RingHang = new[] { 0.35f, 1f, 0.55f, 1f, 0.45f },
            RingRidge = new[] { 0.3f, 0.85f, 1f, 0.75f, 0.4f },
            ShortRings = new[] { 0, 2, 4 },
            Shade = 0.2f,
            Salt = 0,
        };

        /// <summary>The rest of the wood: fewer, wider arms of two rings, the whorls nearly twice as far apart.</summary>
        public static readonly SpruceDetail Far = new()
        {
            WhorlSpacing = 1.9f,
            Arms = 4, ArmsSpread = 2, Between = 0, BetweenSpread = 2,
            Width = 0.3f,
            RingT = new[] { 0f, 0.5f },
            RingWidth = new[] { 0.35f, 1f },
            RingHang = new[] { 0.4f, 1f },
            RingRidge = new[] { 0.4f, 1f },
            ShortRings = null,
            SnowShare = 0.7f,
            Shade = 0.38f,
            Salt = 17,
        };
    }

    /// <summary>
    /// High and Ultra's spruce crown (#733): the whorls as <b>separate boughs</b>, built on the CPU (so a test can read it
    /// without a device) and uploaded by <see cref="TreeMesh"/>. The owner, of the aurora's wood on Ultra: "as they look now
    /// they could pass for Low, maybe Medium, but not High or Ultra" — a lathe of smooth skirts reads as lampshades on a
    /// pole once a tree is large on a 4K screen.
    /// <para>
    /// <b>What the references drew</b> (#733's set, both models): a spruce's silhouette is the tips of its boughs, each
    /// poking out on its own at its own length and height and turned up at the end; a bough is an arm that leaves the stem,
    /// sags and curls its tip up, with a plate of foliage over it and branchlets hanging under it in a fringe; the snow lies
    /// in lumps on the arms; between the arms the crown is dark and, high up, open round the leader. So a bough here is an
    /// arm of a few rings (<see cref="SpruceDetail"/>) with a diamond cross-section: a ridge over the arm, two side edges a
    /// little below it, and a keel hanging under it, deeper low in the crown — the fringe. The top and the underside keep
    /// normals of their own across the side edges, so a plate is lit on top and dark beneath. Arms come in whorls of four to
    /// seven with a few smaller arms between the whorls, each reaching the plan's outline at the height its tip ends at,
    /// ragged by the config's raggedness and the lathe's own wobble, so the variant leans the way its coarse crown does. High
    /// in the crown an arm climbs; low down it hangs and curls up harder. A thin leader closes the top.
    /// </para>
    /// <para>
    /// <b>The snow is geometry</b> (<see cref="SnowVertices"/>): a soft lump on the plate of half the arms, drawn in the
    /// snow's own colour. The lathe takes its snow through the triplanar dust term, which whitens a face by how squarely it
    /// faces up; on a wood of flat plates that whitened every bough, and the near-black spruces #462 stood on the snow went
    /// pale. The boughs keep only a frost of the dust term, and the lumps carry the snow.
    /// </para>
    /// <para>
    /// <b>The interior is a mesh of its own</b> (<see cref="ShadeProfileFor"/>, drawn in a darker tint by the scatter): a
    /// narrow lathe at a share of the outline (<see cref="SpruceDetail.Shade"/>), which the arms root inside. Without it a gap between
    /// the arms opens onto the arms on the far side of the tree, lit as brightly as the near ones, and the crown reads as a
    /// wire frame; with it the gaps are the shaded old needles round the stem.
    /// </para>
    /// <para>
    /// The arms' root rings stand inside the interior and are left open. Every triangle is wound clockwise seen from
    /// outside (CLAUDE.md), checked against the outward direction it is built for as <see cref="MeshBuilder"/> does.
    /// </para>
    /// </summary>
    public sealed class SpruceCrownGeometry
    {
        /// <summary>The interior's facets round the axis.</summary>
        public const int SHADE_SEGMENTS = 10;

        /// <summary>An arm shorter than this, from the stem to its tip (world units), keeps <see cref="SpruceDetail.ShortRings"/>.</summary>
        public const float SHORT_ARM = 1f;

        //How far past the lathe's outline the arms reach: the tips poke out of the outline the skirts' edges drew, as a
        //spruce's do out of the cone the eye sees it as
        private const float REACH = 1.08f;

        //The share of the arms that carry a lump of snow, whorl arms and the smaller ones between the whorls
        private const float SNOW_ON_ARMS = 0.5f;
        private const float SNOW_ON_SMALL_ARMS = 0.3f;

        /// <summary>The boughs and the leader, one draw.</summary>
        public List<VertexPositionNormalTexture> Vertices => _needles.Vertices;
        public List<int> Indices => _needles.Indices;

        /// <summary>
        /// The snow lying in lumps on some of the arms, one draw of its own in the snow's colour: a scene with no crown snow
        /// (<see cref="ForestTreeConfig.CrownSnow"/> 0, the daytime forest) does not draw it.
        /// </summary>
        public List<VertexPositionNormalTexture> SnowVertices => _snow.Vertices;
        public List<int> SnowIndices => _snow.Indices;

        /// <summary>How many arms the crown has, leader excluded, and how many of them carry a lump of snow.</summary>
        public int Boughs { get; private set; }
        public int SnowLumps { get; private set; }

        /// <summary>Round every vertex of the boughs; the snow lies on them.</summary>
        public BoundingSphere Bounds { get; private set; }

        private readonly Part _needles = new(), _snow = new();
        private readonly SpruceDetail _detail;

        private SpruceCrownGeometry(SpruceDetail detail) => _detail = detail;

        /// <summary>Builds the crown for one plan at one detail. The same plan, seed and detail always build the same crown.</summary>
        public static SpruceCrownGeometry Build(SpruceWhorlPlan plan, int seed, SpruceDetail detail)
        {
            var crown = new SpruceCrownGeometry(detail);
            crown.Grow(plan, new Random(unchecked(seed * 7919 + 733 + detail.Salt)));
            return crown;
        }

        /// <summary>
        /// The interior lathe's profile, top → outside → underside (<see cref="LatheMesh"/>'s direction): from an eighth of
        /// the crown under its tip (above that the crown is open round the leader, as the references draw it) down to the
        /// lowest skirt, at the detail's share of the outline (<see cref="SpruceDetail.Shade"/>). It takes the same share of
        /// the lathe's wobble.
        /// </summary>
        public static List<LathePoint> ShadeProfileFor(SpruceWhorlPlan plan, SpruceDetail detail)
        {
            const int RINGS = 8;
            float bottom = plan.SkirtHeights[plan.SkirtHeights.Count - 1];
            float top = plan.Top - plan.Height * 0.12f;
            float thinnest = plan.Radius * 0.03f;

            var profile = new List<LathePoint> { new(0f, top) };
            for (int k = 1; k <= RINGS; k++)
            {
                float at = MathHelper.Lerp(top, bottom + 0.3f, k / (float)RINGS);
                profile.Add(new LathePoint(MathF.Max(detail.Shade * plan.Envelope(at), thinnest), at, wobble: 1f));
            }
            profile.Add(new LathePoint(0f, bottom));
            return profile;
        }

        private void Grow(SpruceWhorlPlan plan, Random rng)
        {
            float top = plan.Top;
            float rootRadius = plan.Radius * 0.05f;
            float spacing = _detail.WhorlSpacing;

            //The whorls, from just under the leader down to the crown's foot, each turned on from the last by the golden
            //angle so no two stack their arms in one plane
            float firstWhorl = top - MathF.Min(spacing, 1.1f) * (0.9f + 0.3f * (float)rng.NextDouble());
            float y = firstWhorl;
            float turn = (float)rng.NextDouble() * MathHelper.TwoPi;

            while (y > plan.BaseY - spacing * 0.2f)
            {
                float f = Math.Clamp((top - y) / plan.Height, 0f, 1f);

                int arms = _detail.Arms + rng.Next(_detail.ArmsSpread);
                for (int a = 0; a < arms; a++)
                {
                    float bearing = turn + MathHelper.TwoPi * (a + 0.35f * ((float)rng.NextDouble() - 0.5f)) / arms;
                    AddArm(plan, rng, bearing, y + spacing * 0.12f * ((float)rng.NextDouble() - 0.5f), f, 1f, rootRadius);
                }

                //A few smaller arms between this whorl and the next — a spruce's inter-whorl branches
                float step = spacing * (0.8f + 0.4f * (float)rng.NextDouble());
                int between = f > 0.12f ? _detail.Between + rng.Next(_detail.BetweenSpread) : 0;
                for (int a = 0; a < between; a++)
                {
                    float bearing = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float at = y - step * (0.35f + 0.3f * (float)rng.NextDouble());
                    AddArm(plan, rng, bearing, at, Math.Clamp((top - at) / plan.Height, 0f, 1f), 0.62f, rootRadius);
                }

                turn += 2.39996f + 0.4f * ((float)rng.NextDouble() - 0.5f);
                y -= step;
            }

            AddLeader(plan, firstWhorl, top);

            _needles.FinishNormals();
            _snow.FinishNormals();

            //Bounds round every vertex, about the axis
            float lowest = float.MaxValue, highest = float.MinValue, widest = 0f;
            foreach (VertexPositionNormalTexture vertex in Vertices)
            {
                lowest = MathF.Min(lowest, vertex.Position.Y);
                highest = MathF.Max(highest, vertex.Position.Y);
                widest = MathF.Max(widest, new Vector2(vertex.Position.X, vertex.Position.Z).Length());
            }
            Bounds = new BoundingSphere(new Vector3(0f, (lowest + highest) * 0.5f, 0f),
                new Vector2(widest, (highest - lowest) * 0.5f).Length());
        }

        /// <summary>
        /// One arm: from the stem at <paramref name="rootY"/> out along <paramref name="bearing"/> to the outline, its spine a
        /// cubic that climbs or hangs by where in the crown it grows (<paramref name="f"/>, 0 at the top, 1 at the foot), sags
        /// and turns its tip up. Some carry a lump of snow on the middle of their plate.
        /// </summary>
        private void AddArm(SpruceWhorlPlan plan, Random rng, float bearing, float rootY, float f, float lengthShare, float rootRadius)
        {
            //The spine's height as a share of its run, at t along it: s0 t - sag t^2 + curl t^3. High arms climb, low ones
            //hang and curl up harder at the end
            float slope = MathHelper.Lerp(0.55f, -0.5f, MathF.Pow(f, 0.8f)) + 0.24f * ((float)rng.NextDouble() - 0.5f);
            float sag = 0.2f + 0.1f * (float)rng.NextDouble();
            float curl = MathHelper.Lerp(0.1f, 0.34f, f) * (0.7f + 0.6f * (float)rng.NextDouble());
            float rise = slope - sag + curl;

            //The reach: the outline where the tip ends up (twice round, since the tip's height depends on the reach),
            //ragged by the config's raggedness and the lathe's wobble at this bearing, so a variant leans the way its coarse
            //crown does; now and then an arm broken short
            float reach = plan.Envelope(rootY);
            reach = plan.Envelope(rootY + (reach - rootRadius) * rise);
            reach = MathF.Max(reach, plan.Radius * 0.1f);
            float wobble = plan.WobbleAmplitude / MathF.Max(plan.Radius, 1e-3f)
                * LatheMesh.Irregularity(bearing + plan.Phase, rootY + plan.Phase);
            float jitter = 1f + plan.Raggedness * 0.12f * (2f * (float)rng.NextDouble() - 1f);
            if (rng.NextDouble() < 0.06) jitter *= 0.55f;
            reach = MathF.Min(reach * REACH * lengthShare * jitter * (1f + 0.5f * wobble), plan.Reach);

            float run = MathF.Max(reach - rootRadius, plan.Radius * 0.08f);
            float width = run * _detail.Width * (0.85f + 0.3f * (float)rng.NextDouble());
            float hang = run * MathHelper.Lerp(0.18f, 0.4f, f) * (0.8f + 0.4f * (float)rng.NextDouble());
            float ridge = run * 0.15f;

            //The plate rolls a little about the arm, so the whorl's plates do not all lie in one cone
            float roll = 0.3f * ((float)rng.NextDouble() - 0.5f);

            //Whether the arm carries snow, and how much: on its plate where it is flattest, the ring the middle of the arm
            //is built from
            bool lump = rng.NextDouble() < (lengthShare < 1f ? SNOW_ON_SMALL_ARMS : SNOW_ON_ARMS) * _detail.SnowShare;
            float lumpSize = 0.8f + 0.5f * (float)rng.NextDouble();

            Vector3 outward = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));
            Vector3 side = new(-outward.Z, 0f, outward.X);

            Vector3 Spine(float t) => new Vector3(0f, rootY + run * t * (slope + t * (-sag + t * curl)), 0f)
                + outward * (rootRadius + run * t);

            int[] rings = run < SHORT_ARM ? _detail.ShortRings : null;
            int ringCount = rings?.Length ?? _detail.RingT.Length;
            int lumpRing = ringCount / 2;

            int previous = -1;
            Vector3 previousCentre = Vector3.Zero, previousCrest = Vector3.Zero;
            for (int r = 0; r < ringCount; r++)
            {
                int k = rings == null ? r : rings[r];
                float t = _detail.RingT[k];

                Vector3 centre = Spine(t);
                Vector3 along = Vector3.Normalize(Spine(MathF.Min(t + 0.05f, 1f)) - Spine(MathF.Max(t - 0.05f, 0f)));
                Vector3 up = Vector3.Normalize(Vector3.Cross(side, along));
                Vector3 across = side * MathF.Cos(roll) + up * MathF.Sin(roll);
                up = Vector3.Normalize(Vector3.Cross(across, along));

                float w = width * _detail.RingWidth[k];
                float h = hang * _detail.RingHang[k];
                float ridgeHeight = ridge * _detail.RingRidge[k] * (0.6f + 0.9f * (float)rng.NextDouble());

                Vector3 left = centre - across * w - Vector3.Up * (0.3f * h);
                Vector3 right = centre + across * w - Vector3.Up * (0.3f * h);
                Vector3 crest = centre + up * ridgeHeight;
                Vector3 keel = centre - Vector3.Up * h;

                int at = _needles.Count;
                _needles.Add(left); _needles.Add(crest); _needles.Add(right);   //the top: at, at+1, at+2
                _needles.Add(left); _needles.Add(keel); _needles.Add(right);    //the underside: at+3, at+4, at+5

                if (previous >= 0)
                {
                    Vector3 axis = (centre + previousCentre) * 0.5f;
                    _needles.Quad(previous, previous + 1, at + 1, at, axis);           //the top, left of the ridge
                    _needles.Quad(previous + 1, previous + 2, at + 2, at + 1, axis);   //the top, right of it
                    _needles.Quad(previous + 3, previous + 4, at + 4, at + 3, axis);   //the underside, left of the keel
                    _needles.Quad(previous + 4, previous + 5, at + 5, at + 4, axis);   //and right
                }

                //The lump: a soft mound along the ridge from short of this ring's crest back towards the last ring's to
                //short of the next ring's, spilling over the plate's edges
                if (lump && r == lumpRing && r > 0)
                {
                    //Towards the next ring's crest, or the tip on an arm whose last ring this is
                    Vector3 nextCrest = Spine(1f);
                    if (r + 1 < ringCount)
                    {
                        int next = rings == null ? r + 1 : rings[r + 1];
                        nextCrest = Spine(_detail.RingT[next]) + up * ridge * _detail.RingRidge[next] * 0.8f;
                    }
                    AddLump(previousCrest, crest, nextCrest, across, up, centre, w, h, lumpSize);
                }

                previous = at;
                previousCentre = centre;
                previousCrest = crest;
            }

            //The tip: both surfaces close on one point, each with a vertex of its own
            Vector3 tip = Spine(1f);
            int tipTop = _needles.Count;
            _needles.Add(tip);
            int tipUnder = _needles.Count;
            _needles.Add(tip);
            Vector3 tipAxis = (tip + previousCentre) * 0.5f;
            _needles.Triangle(previous, previous + 1, tipTop, tipAxis);
            _needles.Triangle(previous + 1, previous + 2, tipTop, tipAxis);
            _needles.Triangle(previous + 3, previous + 4, tipUnder, tipAxis);
            _needles.Triangle(previous + 4, previous + 5, tipUnder, tipAxis);

            Boughs++;
        }

        /// <summary>
        /// A lump of snow on an arm's plate: a pillow over the crest, pointed at both ends along the arm, its flanks spilling
        /// a little past the plate's edges, and closed underneath (inside the arm) so it is a solid. One cross-section of five
        /// points (the edges, the shoulders, the top) between the two ends; the top shares its normals, so it is lit as one
        /// soft heap rather than as facets.
        /// </summary>
        private void AddLump(Vector3 back, Vector3 crest, Vector3 front, Vector3 across, Vector3 up, Vector3 centre,
            float halfWidth, float hang, float size)
        {
            Vector3 a = Vector3.Lerp(crest, back, 0.45f);
            Vector3 b = Vector3.Lerp(crest, front, 0.5f);
            float spill = halfWidth * 1.1f * size;
            float rise = halfWidth * 0.6f * size;
            Vector3 floor = centre - Vector3.Up * (0.28f * hang);

            int at = _snow.Count;
            _snow.Add(floor - across * spill);
            _snow.Add(crest - across * (spill * 0.6f) + up * (rise * 0.7f));
            _snow.Add(crest + up * rise);
            _snow.Add(crest + across * (spill * 0.6f) + up * (rise * 0.7f));
            _snow.Add(floor + across * spill);
            int ends = _snow.Count;
            _snow.Add(a);
            _snow.Add(b);

            //The heap's inside: under its top, over the plate
            Vector3 inside = (crest + floor) * 0.5f;
            for (int k = 0; k < 4; k++)
            {
                _snow.Triangle(ends, at + k, at + k + 1, inside);       //the back half
                _snow.Triangle(ends + 1, at + k + 1, at + k, inside);   //the front half
            }
            _snow.Triangle(ends, at + 4, at, inside);                   //and the floor
            _snow.Triangle(ends + 1, at, at + 4, inside);

            SnowLumps++;
        }

        //The leader: a thin five-sided spike from inside the first whorl to the crown's tip
        private void AddLeader(SpruceWhorlPlan plan, float firstWhorl, float top)
        {
            const int SIDES = 5;
            float baseY = firstWhorl - 0.3f;
            float radius = 0.03f * plan.Radius + 0.04f;

            int ring = _needles.Count;
            for (int s = 0; s < SIDES; s++)
            {
                float angle = MathHelper.TwoPi * s / SIDES;
                _needles.Add(new Vector3(radius * MathF.Cos(angle), baseY, radius * MathF.Sin(angle)));
            }
            int tip = _needles.Count;
            _needles.Add(new Vector3(0f, top, 0f));

            Vector3 axis = new(0f, (baseY + top) * 0.5f, 0f);
            for (int s = 0; s < SIDES; s++)
                _needles.Triangle(ring + s, ring + (s + 1) % SIDES, tip, axis);
        }

        /// <summary>One draw's worth of the crown: its vertices, indices and the outward normals summed into them.</summary>
        private sealed class Part
        {
            public readonly List<VertexPositionNormalTexture> Vertices = new();
            public readonly List<int> Indices = new();
            private readonly List<Vector3> _normalSums = new();

            public int Count => Vertices.Count;

            public void Add(Vector3 position)
            {
                Vertices.Add(new VertexPositionNormalTexture(position, Vector3.Up, Vector2.Zero));
                _normalSums.Add(Vector3.Zero);
            }

            //Two triangles; a quad with a repeated corner is the one triangle it really is
            public void Quad(int a, int b, int c, int d, Vector3 inside)
            {
                if (a != b && b != c && a != c) Triangle(a, b, c, inside);
                if (a != c && c != d && a != d) Triangle(a, c, d, inside);
            }

            /// <summary>
            /// A triangle facing away from <paramref name="inside"/>: wound so its <c>(b - a) × (c - a)</c> points inward,
            /// the front face under MonoGame's default culling (CLAUDE.md), and its outward normal summed by area into its
            /// corners.
            /// </summary>
            public void Triangle(int a, int b, int c, Vector3 inside)
            {
                Vector3 pa = Vertices[a].Position, pb = Vertices[b].Position, pc = Vertices[c].Position;
                Vector3 cross = Vector3.Cross(pb - pa, pc - pa);
                Vector3 outward = (pa + pb + pc) / 3f - inside;

                if (Vector3.Dot(cross, outward) > 0f)
                {
                    (b, c) = (c, b);
                    cross = -cross;
                }

                Indices.Add(a);
                Indices.Add(b);
                Indices.Add(c);

                //cross points inward now, and its length is twice the area: the outward normal, area-weighted
                _normalSums[a] -= cross;
                _normalSums[b] -= cross;
                _normalSums[c] -= cross;
            }

            //The summed normals, normalised into the vertices
            public void FinishNormals()
            {
                for (int v = 0; v < Vertices.Count; v++)
                {
                    VertexPositionNormalTexture vertex = Vertices[v];
                    Vector3 n = _normalSums[v];
                    vertex.Normal = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Up;
                    Vertices[v] = vertex;
                }
            }
        }
    }
}
