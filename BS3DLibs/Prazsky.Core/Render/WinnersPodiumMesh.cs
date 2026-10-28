using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The winners' podium of the Top Players page (#827): three stepped blocks side by side, the first place
    /// in the middle and the highest, the second on its left and the third on its right, on one low stepped
    /// plinth, each block carrying a gold-framed name panel, a gold numeral over it, and a flat top for its
    /// player's cup (<see cref="TrophyMesh"/>) to stand on. No text is modelled: the page draws each name in 2D
    /// at the projected <see cref="StepFront"/>, which is why the panel is a frame round bare lacquer and not a
    /// plate.
    /// <para>
    /// <b>Drawn from references rendered for it (#827, Z-Image and klein, four prompts each).</b> What the
    /// pictures that read as a podium agreed on, and what a mesh can be made of: lacquered blocks under a gold
    /// cornice and over a gold base moulding, a gold-framed panel on each face (klein's marble podium; Z-Image's
    /// lacquer had a gold plate there, which would have shut a name's letters out of the one place the page draws
    /// them), gold along the corners, and a plinth with a step in it and a line of gold on the step. The candy
    /// podium, soft slabs with no edges at all, was the one a mesh could not follow, and the one that left the
    /// cups nothing to stand on. The proportions are the Z-Image lacquer podium's: the second place about three
    /// quarters of the way up the first, the third half way, and the plinth a hand's breadth wider than the blocks.
    /// </para>
    /// <para>
    /// <b>Two parts, two materials.</b> <see cref="Part.Body"/> is the plinth and the three blocks, drawn as
    /// deep lacquer (<see cref="BODY_DIFFUSE"/>); <see cref="Part.Trim"/> is everything gold: the cornice and
    /// the base moulding round each block, the posts on the blocks' front corners, the panel frames, the
    /// numerals and the bead on the plinth, drawn as <b>polished gold</b> (the <c>GOLD</c> finish of the
    /// trophy's own presentation, <see cref="TRIM_SPECULAR"/>). The two are built in one frame, so they are
    /// drawn with the same world matrix and fit to the unit.
    /// </para>
    /// <para>
    /// <b>Units and frame.</b> Mesh units, the origin on the ground at the middle of the footprint, +Y up, the
    /// fronts facing <b>+Z</b>, the camera's side. <see cref="WIDTH"/> is about three of them, so the page
    /// scales it by the width it wants; every placement it needs is a method here
    /// (<see cref="StepTop"/>, <see cref="StepFront"/>, <see cref="StepTopWidth"/>) so that nothing outside
    /// knows a block's size. A cup is <see cref="TrophyMesh.HEIGHT"/> tall and a little over half that across
    /// at the lip, so the page scales it to a fraction of <see cref="StepTopWidth"/> and puts its foot on
    /// <see cref="StepTop"/>.
    /// </para>
    /// <para>
    /// <b>Wound through <see cref="MeshBuilder"/></b>, which corrects every face against the normal it is meant
    /// to show, so no solid here can come out inside-out. Every solid is closed (the logic tests and the
    /// openness gate hold it), and the parts overlap rather than touch: the gold rings are sunk into the block
    /// they hug, the blocks into the plinth, so no sliver of the ground shows through a seam.
    /// </para>
    /// </summary>
    public sealed class WinnersPodiumMesh : IProceduralMesh, IDisposable
    {
        /// <summary>The two materials: the lacquered plinth and blocks, and the gold on them.</summary>
        public enum Part { Body, Trim }

        /// <summary>The overall extent along X: the plinth's width.</summary>
        public const float WIDTH = 3f;

        /// <summary>The overall extent along Z: the plinth's depth.</summary>
        public const float DEPTH = 1.2f;

        /// <summary>The overall height: the top of the first place's cornice.</summary>
        public const float HEIGHT = PLINTH_HEIGHT + LOWEST_STEP + 2f * STEP_RISE + CORNICE_LIP;

        /// <summary>
        /// The width and height of the bare panel inside each name frame: the area the page draws a name in,
        /// centred on <see cref="StepFront"/>. A name is set in light letters on it - the panel is dark lacquer.
        /// </summary>
        public const float PLAQUE_WIDTH = 0.60f, PLAQUE_HEIGHT = 0.15f;

        //THE SUGGESTED MATERIALS, in the vocabulary of the trophy's own Finish records (TrophyPodium, in the Game):
        //diffuse, specular, specular power. The body is a deep indigo lacquer - it is dark enough to carry light
        //letters on the name panel and cold enough that the gold, the silver and the bronze cups all stand out
        //of it, which a mahogany would not do for the bronze. The trim is the GOLD finish verbatim, so a gold
        //cup standing on it cannot be told from the rings round it by anything but its shape.
        public static readonly Vector3 BODY_DIFFUSE = new(0.11f, 0.045f, 0.27f);
        public static readonly Vector3 BODY_SPECULAR = new(0.60f, 0.55f, 0.70f);
        public const float BODY_POWER = 110f;
        public static readonly Vector3 TRIM_DIFFUSE = new(0.470f, 0.320f, 0.080f);
        public static readonly Vector3 TRIM_SPECULAR = new(0.98f, 0.78f, 0.40f);
        public const float TRIM_POWER = 140f;

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        //THE BLOCKS. Square, touching along X, centred in depth on the plinth. The third place is the lowest and
        //each place above it rises by the same step, so the three tops are a flight of stairs.
        private const float PLINTH_HEIGHT = 0.10f;
        private const float BLOCK = 0.90f;
        private const float LOWEST_STEP = 0.66f, STEP_RISE = 0.33f;

        //The block's vertical corners are cut on the slant: the lacquer then catches a line of light down each
        //edge, and where two blocks touch the cuts meet in a groove that says they are two blocks. The top
        //edge is cut the same (BLOCK_BEVEL), under the cornice, where nothing shows it but the cornice's own
        //underside.
        private const float BLOCK_CUT = 0.03f, BLOCK_BEVEL = 0.018f;

        //A block's foot is sunk this far into the plinth, so the join is an intersection and not a seam.
        private const float SINK = 0.01f;

        //THE GOLD BORDER round a block's top: the cornice reaches this far in over the top face, so the flat a
        //cup stands on is narrower than the block by twice it. The cornice's lip stands CORNICE_LIP over the
        //face, a bead of gold the cup's foot clears.
        private const float TOP_BORDER = 0.04f;
        private const float CORNICE_LIP = 0.004f;

        //The name panel's centre above the plinth: the same on every block, so the three names stand on one line
        //whatever the steps do. The numeral is placed from the block's top instead, so it follows the stairs.
        private const float PLAQUE_Y = 0.17f, FRAME_BAR = 0.030f;

        //A numeral hangs from the block's top, under the cornice and a gap, and is the larger the higher the place:
        //the numerals then climb with the stairs and the first is the loudest thing on the face. The third is
        //bounded by the panel under it, which is why it is the one that sets the others' scale at all.
        private const float NUMERAL_HANG = 0.115f, NUMERAL_HALF = 0.112f;

        //THE NUMERALS are round tubes, the same family as the game's wordmark (LetterMesh): a stroke with a
        //domed end, so a numeral reads as bent gold wire and takes a highlight down its whole length where a
        //flat extrusion would take one flat shade. Squashed in depth, a bead set into the face and not a
        //pipe laid on it.
        private const float STROKE_RADIUS = 0.022f, STROKE_DEPTH = 0.016f, STROKE_SPACING = 0.014f;
        private const int STROKE_SIDES = 10, DOME_RINGS = 3;

        //The radius a stroke's corner is rounded by: a little over the tube's own, so the inside of the bend keeps
        //a radius of its own and does not fold over itself
        private const float STROKE_BEND = 0.03f;

        /// <param name="part">Which material's geometry to build.</param>
        public WinnersPodiumMesh(GraphicsDevice graphicsDevice, Part part)
        {
            (VertexBuffer vertices, IndexBuffer indices, int primitives) = Compose(part).Build(graphicsDevice);

            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;

            //Both parts share one sphere, so a cull cannot take the gold and leave the lacquer standing
            BoundingSphere = new BoundingSphere(new Vector3(0f, HEIGHT * 0.5f, 0f),
                MathF.Sqrt(WIDTH * WIDTH + HEIGHT * HEIGHT + DEPTH * DEPTH) * 0.5f);
        }

        /// <summary>
        /// The centre of a step's top face, where a cup's foot stands: <paramref name="place"/> 1 is the middle and
        /// the highest, 2 is on the left (-X) and 3 on the right (+X).
        /// </summary>
        public static Vector3 StepTop(int place) => new(CentreX(place), PLINTH_HEIGHT + StepHeight(place), 0f);

        /// <summary>
        /// Where the name is drawn: the middle of the bare panel on the step's front face, on the face itself. The
        /// panel is centred on the same height under all three steps and sized <see cref="PLAQUE_WIDTH"/> by
        /// <see cref="PLAQUE_HEIGHT"/>.
        /// </summary>
        public static Vector3 StepFront(int place) => new(CentreX(place), PLINTH_HEIGHT + PLAQUE_Y, BLOCK * 0.5f);

        /// <summary>
        /// How wide the flat of a step's top is along X, inside its gold border: what a cup is scaled against.
        /// </summary>
        public static float StepTopWidth(int place)
        {
            CentreX(place);
            return BLOCK - 2f * TOP_BORDER;
        }

        //Place 1 in the middle, 2 on the left, 3 on the right: the order a podium is read in
        private static float CentreX(int place) => place switch
        {
            1 => 0f,
            2 => -BLOCK,
            3 => BLOCK,
            _ => throw new ArgumentOutOfRangeException(nameof(place), place, "A podium has places 1, 2 and 3.")
        };

        //How high a block stands over the plinth
        private static float StepHeight(int place)
        {
            CentreX(place);
            return LOWEST_STEP + STEP_RISE * (3 - place);
        }

        /// <summary>
        /// The part's geometry with no device - what the constructor uploads, and what a test reads to hold the
        /// mesh closed and the placements true without a GPU.
        /// </summary>
        internal static MeshBuilder Compose(Part part)
        {
            MeshBuilder builder = new();

            if (part == Part.Body) BuildBody(builder);
            else BuildTrim(builder);

            return builder;
        }

        //----------------------------------------------------------------------------------------------------------
        //THE PROFILES. A profile is a cross-section in (U, V): U outward from the path it is swept round, V up
        //(along the sweep's normal). An OPEN profile is a solid's side, walked from its foot up to its top, and
        //the sweep closes it with a flat cap at each end; a RING profile is a closed loop, walked counter-clockwise,
        //and the sweep makes it a band with no cap. Smooth marks a point whose two sides share one normal: a
        //rounded moulding rather than a crisp edge.

        private readonly record struct Pt(float U, float V, bool Smooth = false);

        //The plinth, relative to its flat top: a lower tier, a ledge for the bead to lie on, an upper tier, and a
        //broad bevel onto the top. Its path is the top's outline, so U 0 is where the top ends.
        private static readonly Pt[] PLINTH =
        {
            new(0.100f, 0f), new(0.100f, 0.042f), new(0.090f, 0.052f),
            new(0.066f, 0.052f), new(0.066f, 0.072f), new(0.040f, PLINTH_HEIGHT), new(0f, PLINTH_HEIGHT)
        };

        private const float PLINTH_HALF_X = 1.40f, PLINTH_HALF_Z = 0.50f, PLINTH_CUT = 0.07f;

        //The cornice, relative to the block's wall and its top (V 0): an under-bead, a neck, a crown that
        //overhangs, and a flat that runs in over the top face as the border. Its lowest and innermost part is
        //inside the block.
        private static readonly Pt[] CORNICE =
        {
            new(-TOP_BORDER, -0.080f), new(0.016f, -0.080f), new(0.016f, -0.062f), new(0.008f, -0.054f),
            new(0.008f, -0.036f), new(0.026f, -0.036f), new(0.026f, -0.010f), new(0.016f, CORNICE_LIP),
            new(-TOP_BORDER + 0.006f, CORNICE_LIP), new(-TOP_BORDER, CORNICE_LIP - 0.006f)
        };

        //The base moulding, relative to the block's wall and the plinth's top: a toe, a step and a fillet
        private static readonly Pt[] BASE =
        {
            new(-0.020f, -0.004f), new(0.020f, -0.004f), new(0.020f, 0.014f), new(0.014f, 0.022f),
            new(0.014f, 0.030f), new(0.004f, 0.038f), new(-0.020f, 0.038f)
        };

        //The panel frame, relative to the panel's edge and the face it stands on: a bar with a chamfered top
        //and a bevelled inner edge, standing proud of the lacquer
        private static readonly Pt[] FRAME =
        {
            new(0f, -0.004f), new(FRAME_BAR, -0.004f), new(FRAME_BAR, 0.010f), new(FRAME_BAR - 0.008f, 0.019f),
            new(0.008f, 0.019f), new(0f, 0.011f)
        };

        //----------------------------------------------------------------------------------------------------------
        //WHAT IS BUILT

        private static void BuildBody(MeshBuilder builder)
        {
            //THE CHIPS: the flaws a lacquered slab that has stood in a hall for years would have, and a render of
            //one that was just made would not - two corners of the plinth's lower tier knocked in, the front right
            //worse than the front left, the gold bead still standing over the gap as a bead of metal would. The
            //owner's rule for a surface that would otherwise be mathematically perfect (#640): the eye reads a flaw
            //as a thing that was here before it was looked at. The drop is kept short of the ledge's own width, or
            //the corner's ledge would fold over itself and show the inside of the solid.
            Chip[] chips =
            {
                new(2, 2, 0.016f, 0.020f), new(2, 1, 0.032f, 0.028f),
                new(3, 2, 0.008f, 0.012f), new(3, 1, 0.016f, 0.016f)
            };

            Sweep(builder, Floor(0f, 0f), Octagon(PLINTH_HALF_X, PLINTH_HALF_Z, PLINTH_CUT), PLINTH, false, chips);

            for (int place = 1; place <= 3; place++)
            {
                float height = StepHeight(place);
                Pt[] block = { new(0f, -SINK), new(0f, height - BLOCK_BEVEL), new(-BLOCK_BEVEL, height) };

                Sweep(builder, Floor(CentreX(place), PLINTH_HEIGHT), Octagon(BLOCK * 0.5f, BLOCK * 0.5f, BLOCK_CUT), block, false);
            }
        }

        private static void BuildTrim(MeshBuilder builder)
        {
            //The bead on the plinth's ledge: a half round of gold, rounded on its smooth points, lying on the
            //ledge between the lower tier's chamfer and the upper tier's wall
            const float BEAD_U = 0.078f, BEAD_V = 0.052f, BEAD_R = 0.0095f;
            Pt[] bead = new Pt[9];
            bead[0] = new Pt(BEAD_U + BEAD_R, BEAD_V - 0.004f);
            for (int i = 0; i <= 6; i++)
            {
                float angle = i * MathHelper.Pi / 6f;
                bead[i + 1] = new Pt(BEAD_U + BEAD_R * MathF.Cos(angle), BEAD_V + BEAD_R * MathF.Sin(angle), i is > 0 and < 6);
            }
            bead[8] = new Pt(BEAD_U - BEAD_R, BEAD_V - 0.004f);

            Sweep(builder, Floor(0f, 0f), Octagon(PLINTH_HALF_X, PLINTH_HALF_Z, PLINTH_CUT), bead, true);

            Vector2[] wall = Octagon(BLOCK * 0.5f, BLOCK * 0.5f, BLOCK_CUT);

            for (int place = 1; place <= 3; place++)
            {
                float centre = CentreX(place), height = StepHeight(place);

                Sweep(builder, Floor(centre, PLINTH_HEIGHT), wall, BASE, true);
                Sweep(builder, Floor(centre, PLINTH_HEIGHT + height), wall, CORNICE, true);

                //The posts: on the two corners that face the camera, standing on the base moulding and ending
                //inside the cornice. Each is laid square to its corner's slanted facet
                foreach (float side in new[] { -1f, 1f })
                {
                    float facet = BLOCK * 0.5f - BLOCK_CUT * 0.5f;
                    Vector3 outward = Vector3.Normalize(new Vector3(side, 0f, 1f));
                    Vector3 along = new(-outward.Z, 0f, outward.X);
                    const float FOOT = 0.020f, TOP = 0.060f;

                    Frame post = new(new Vector3(centre + side * facet, PLINTH_HEIGHT + FOOT, facet), outward, along, Vector3.UnitY);
                    float rise = height - TOP - FOOT;
                    Pt[] bar = { new(0f, 0f), new(0f, rise - 0.008f), new(-0.008f, rise) };

                    Sweep(builder, post, Octagon(0.012f, 0.026f, 0f), bar, false);
                }

                //The panel frame, on the front face at the panel's height
                Frame face = new(new Vector3(centre, PLINTH_HEIGHT + PLAQUE_Y, BLOCK * 0.5f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
                Sweep(builder, face, Octagon(PLAQUE_WIDTH * 0.5f, PLAQUE_HEIGHT * 0.5f, 0.014f), FRAME, true);

                //The numeral, from the block's top down, on the same face
                float size = place switch { 1 => 1.6f, 2 => 1.3f, _ => 1.05f };
                Frame glyph = new(new Vector3(centre, PLINTH_HEIGHT + height - NUMERAL_HANG - size * NUMERAL_HALF, BLOCK * 0.5f + 0.002f),
                    Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

                foreach (List<Vector2> path in Numeral(place))
                    Stroke(builder, glyph, path, size);
            }
        }

        //----------------------------------------------------------------------------------------------------------
        //SWEEPING A PROFILE ROUND A PATH

        /// <summary>
        /// A frame a sweep is laid in: <paramref name="U"/> and <paramref name="V"/> span the plane of the path,
        /// <paramref name="N"/> is the profile's V axis - up, for a block on the ground, +Z for a panel on a face.
        /// </summary>
        private readonly record struct Frame(Vector3 Origin, Vector3 U, Vector3 V, Vector3 N);

        //A frame lying on the ground at a block's footing: the path in X and Z, the profile's height in Y
        private static Frame Floor(float x, float y) => new(new Vector3(x, y, 0f), Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY);

        /// <summary>
        /// A vertex of the swept path that a flaw moves: <paramref name="Vertex"/> of the path at profile point
        /// <paramref name="Point"/> is pulled <paramref name="Inset"/> in towards the path's middle and
        /// <paramref name="Drop"/> down its normal. The quads that meet it stop being planar and are shaded by their
        /// own triangles, which is what a chip is.
        /// </summary>
        private readonly record struct Chip(int Vertex, int Point, float Inset, float Drop);

        /// <summary>
        /// An octagon about the origin, counter-clockwise in (U, V): a rectangle of half sizes
        /// <paramref name="halfU"/> by <paramref name="halfV"/> with each corner cut off by <paramref name="cut"/>,
        /// or the plain rectangle's four corners when the cut is zero. Vertex 0 is at +U, low V and they run round
        /// from there, so on a ground frame (V is Z) vertices 2 and 3 are the front's two ends.
        /// </summary>
        private static Vector2[] Octagon(float halfU, float halfV, float cut)
        {
            if (cut <= 0f)
                return new Vector2[] { new(halfU, -halfV), new(halfU, halfV), new(-halfU, halfV), new(-halfU, -halfV) };

            return new Vector2[]
            {
                new(halfU, -halfV + cut), new(halfU, halfV - cut), new(halfU - cut, halfV), new(-halfU + cut, halfV),
                new(-halfU, halfV - cut), new(-halfU, -halfV + cut), new(-halfU + cut, -halfV), new(halfU - cut, -halfV)
            };
        }

        /// <summary>
        /// Sweeps <paramref name="profile"/> round the convex <paramref name="path"/> (counter-clockwise in
        /// (U, V)), every point offset outward by its U with the corners MITRED, so a bar round a block's wall
        /// keeps the same width on the slant as on the flat. Each side is a strip of planar quads shaded flat, or
        /// smooth along the profile where a point says so. An open profile is capped at both ends, the first
        /// cap facing -N and the last +N; a ring is closed on itself.
        /// </summary>
        private static void Sweep(MeshBuilder builder, Frame frame, Vector2[] path, Pt[] profile, bool ring, Chip[] chips = null)
        {
            int sides = path.Length, points = profile.Length;
            int segments = ring ? points : points - 1;

            //The outward normal of each side, in the path's plane
            Vector2[] edge = new Vector2[sides];
            for (int k = 0; k < sides; k++)
            {
                Vector2 along = path[(k + 1) % sides] - path[k];
                edge[k] = Vector2.Normalize(new Vector2(along.Y, -along.X));
            }

            //The profile's own normals, one per segment, in (U, V): the walk's direction turned a quarter right
            Vector2[] segment = new Vector2[segments];
            for (int s = 0; s < segments; s++)
            {
                Vector2 step = new Vector2(profile[(s + 1) % points].U - profile[s].U, profile[(s + 1) % points].V - profile[s].V);
                segment[s] = step.LengthSquared() > 1e-10f ? Vector2.Normalize(new Vector2(step.Y, -step.X)) : Vector2.Zero;
            }

            //The normal a profile point shows to the segment it ends or starts: its own segment's, or, where the point
            //is smooth and has a neighbour on the other side, the mean of the two
            Vector2 NormalAt(int point, int own, bool atHigh)
            {
                if (!profile[point].Smooth) return segment[own];

                int other = atHigh ? own + 1 : own - 1;
                if (ring) other = (other + segments) % segments;
                else if (other < 0 || other >= segments) return segment[own];

                Vector2 mean = segment[own] + segment[other];
                return mean.LengthSquared() > 1e-8f ? Vector2.Normalize(mean) : segment[own];
            }

            Vector2 Miter(int k) => (edge[(k + sides - 1) % sides] + edge[k % sides]) / (1f + Vector2.Dot(edge[(k + sides - 1) % sides], edge[k % sides]));

            bool Chipped(int k, int point)
            {
                if (chips == null) return false;
                foreach (Chip chip in chips)
                    if (chip.Vertex == k % sides && chip.Point == point) return true;
                return false;
            }

            Vector3 Corner(int k, int point)
            {
                k %= sides;
                Vector2 miter = Miter(k);
                Vector2 at = path[k] + miter * profile[point].U;
                float v = profile[point].V;

                if (chips != null)
                    foreach (Chip chip in chips)
                        if (chip.Vertex == k && chip.Point == point)
                        {
                            at -= Vector2.Normalize(miter) * chip.Inset;
                            v -= chip.Drop;
                        }

                return frame.Origin + frame.U * at.X + frame.V * at.Y + frame.N * v;
            }

            Vector3 Lift(Vector2 profileNormal, int k) =>
                (frame.U * edge[k].X + frame.V * edge[k].Y) * profileNormal.X + frame.N * profileNormal.Y;

            for (int s = 0; s < segments; s++)
            {
                int low = s, high = (s + 1) % points;
                if (segment[s] == Vector2.Zero) continue;

                for (int k = 0; k < sides; k++)
                {
                    Vector3 p00 = Corner(k, low), p10 = Corner(k + 1, low), p11 = Corner(k + 1, high), p01 = Corner(k, high);
                    Vector3 face = Lift(segment[s], k);

                    if (Chipped(k, low) || Chipped(k + 1, low) || Chipped(k + 1, high) || Chipped(k, high))
                    {
                        AddFlat(builder, p00, p10, p11, face);
                        AddFlat(builder, p00, p11, p01, face);
                    }
                    else
                    {
                        Vector3 n00 = Lift(NormalAt(low, s, false), k), n10 = n00;
                        Vector3 n11 = Lift(NormalAt(high, s, true), k), n01 = n11;
                        builder.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, face);
                    }
                }
            }

            if (ring) return;

            //The caps: a fan from the path's middle, so a chipped corner needs nothing of it but its own vertex
            Cap(builder, sides, Corner, 0, -frame.N);
            Cap(builder, sides, Corner, points - 1, frame.N);
        }

        private static void Cap(MeshBuilder builder, int sides, Func<int, int, Vector3> corner, int point, Vector3 facing)
        {
            Vector3 middle = Vector3.Zero;
            for (int k = 0; k < sides; k++) middle += corner(k, point);
            middle /= sides;

            for (int k = 0; k < sides; k++)
            {
                Vector3 a = corner(k, point), b = corner(k + 1, point);
                AddFlat(builder, middle, a, b, facing);
            }
        }

        //One triangle shaded by its own geometric normal, turned to the side that `outward` says is out
        private static void AddFlat(MeshBuilder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 1e-14f) return;
            normal = Vector3.Normalize(normal);
            if (Vector3.Dot(normal, outward) < 0f) normal = -normal;

            builder.AddTriangle(a, b, c, normal, normal, normal, normal);
        }

        //----------------------------------------------------------------------------------------------------------
        //THE NUMERALS

        /// <summary>
        /// One stroke as a tube of an elliptic section swept along <paramref name="path"/> in the frame's plane (U, V),
        /// squashed along N, with a domed end on both terminals, all <paramref name="size"/> times the glyph's own. The frame is fixed rather than transported,
        /// because the path is planar and N is always square to it - the reason the trophy's handle takes the same.
        /// </summary>
        private static void Stroke(MeshBuilder builder, Frame frame, List<Vector2> path, float size)
        {
            int stations = path.Count;
            float radius = STROKE_RADIUS * size, depth = STROKE_DEPTH * size;
            Vector3[,] position = new Vector3[stations, STROKE_SIDES];
            Vector3[,] normal = new Vector3[stations, STROKE_SIDES];
            Vector2[] tangent = new Vector2[stations];

            for (int k = 0; k < stations; k++)
            {
                tangent[k] = Vector2.Normalize(path[Math.Min(k + 1, stations - 1)] - path[Math.Max(k - 1, 0)]);

                Vector3 across = frame.U * -tangent[k].Y + frame.V * tangent[k].X;
                Vector3 centre = frame.Origin + (frame.U * path[k].X + frame.V * path[k].Y) * size;

                for (int i = 0; i < STROKE_SIDES; i++)
                {
                    (float sin, float cos) = MathF.SinCos(i / (float)STROKE_SIDES * MathHelper.TwoPi);

                    position[k, i] = centre + across * (cos * radius) + frame.N * (sin * depth);
                    normal[k, i] = Vector3.Normalize(across * (cos / radius) + frame.N * (sin / depth));
                }
            }

            for (int k = 0; k + 1 < stations; k++)
                for (int i = 0; i < STROKE_SIDES; i++)
                {
                    int j = (i + 1) % STROKE_SIDES;
                    Vector3 face = normal[k, i] + normal[k, j] + normal[k + 1, i] + normal[k + 1, j];

                    builder.AddQuad(position[k, i], position[k, j], position[k + 1, j], position[k + 1, i],
                        normal[k, i], normal[k, j], normal[k + 1, j], normal[k + 1, i], face);
                }

            Dome(builder, frame, path[0] * size, tangent[0], -1f, radius, depth, position, normal, 0);
            Dome(builder, frame, path[stations - 1] * size, tangent[stations - 1], 1f, radius, depth, position, normal, stations - 1);
        }

        //The terminal's dome: DOME_RINGS rings from the end ring to a point, on the ellipsoid whose axes are the
        //tube's own two and its radius along the stroke - the gradient of which is the normal
        private static void Dome(MeshBuilder builder, Frame frame, Vector2 end, Vector2 tangent, float sign,
            float radius, float depth, Vector3[,] position, Vector3[,] normal, int station)
        {
            //Across is the sweep's own at this station, so the dome's rings take the end ring's points one for one;
            //along runs out of the stroke, which is against its tangent at the start
            Vector3 centre = frame.Origin + frame.U * end.X + frame.V * end.Y;
            Vector3 along = (frame.U * tangent.X + frame.V * tangent.Y) * sign;
            Vector3 across = frame.U * -tangent.Y + frame.V * tangent.X;

            Vector3[] previous = new Vector3[STROKE_SIDES], previousNormal = new Vector3[STROKE_SIDES];
            for (int i = 0; i < STROKE_SIDES; i++)
            {
                previous[i] = position[station, i];
                previousNormal[i] = normal[station, i];
            }

            for (int ring = 1; ring <= DOME_RINGS; ring++)
            {
                float rise = ring / (float)DOME_RINGS * MathHelper.PiOver2;
                (float sinRise, float cosRise) = MathF.SinCos(rise);

                bool apex = ring == DOME_RINGS;
                int count = apex ? 1 : STROKE_SIDES;
                Vector3[] next = new Vector3[count], nextNormal = new Vector3[count];

                for (int i = 0; i < count; i++)
                {
                    (float sin, float cos) = MathF.SinCos(i / (float)STROKE_SIDES * MathHelper.TwoPi);

                    next[i] = centre + along * (radius * sinRise)
                        + (across * (cos * radius) + frame.N * (sin * depth)) * cosRise;
                    nextNormal[i] = Vector3.Normalize(along * (sinRise / radius)
                        + (across * (cos / radius) + frame.N * (sin / depth)) * cosRise);
                }

                for (int i = 0; i < STROKE_SIDES; i++)
                {
                    int j = (i + 1) % STROKE_SIDES;

                    if (apex)
                    {
                        Vector3 face = previousNormal[i] + previousNormal[j] + nextNormal[0];
                        builder.AddTriangle(previous[i], previous[j], next[0], previousNormal[i], previousNormal[j], nextNormal[0], face);
                    }
                    else
                    {
                        Vector3 face = previousNormal[i] + previousNormal[j] + nextNormal[i] + nextNormal[j];
                        builder.AddQuad(previous[i], previous[j], next[j], next[i],
                            previousNormal[i], previousNormal[j], nextNormal[j], nextNormal[i], face);
                    }
                }

                previous = next;
                previousNormal = nextNormal;
            }
        }

        /// <summary>
        /// The strokes of a place's numeral, in glyph units about its middle: eighteen hundredths tall, built
        /// from straight legs and circular arcs only, so that every bend has a radius the tube can take and the
        /// path's tangent never jumps where two pieces meet.
        /// </summary>
        private static List<List<Vector2>> Numeral(int place)
        {
            List<List<Vector2>> strokes = new();
            const float HALF = 0.09f;

            switch (place)
            {
                case 1:
                    //A stem, the flag that leans off its top and a foot: three straight tubes, whose domes round
                    //the joins where they cross
                    strokes.Add(new List<Vector2> { new(0f, HALF), new(0f, -HALF) });
                    strokes.Add(new List<Vector2> { new(-0.057f, 0.040f), new(0f, HALF) });
                    strokes.Add(new List<Vector2> { new(-0.052f, -HALF), new(0.052f, -HALF) });
                    break;

                case 2:
                {
                    //The bowl over the top, then the line that leaves it as a tangent down to the bottom left,
                    //rounded into the base: the tangent point is solved, so the stroke has no kink where the arc
                    //hands over to the line
                    Vector2 bowl = new(0f, 0.045f), corner = new(-0.052f, -HALF), end = new(0.056f, -HALF);
                    const float R = 0.045f;

                    Vector2 toCorner = corner - bowl;
                    float reach = MathF.Atan2(toCorner.Y, toCorner.X), spread = MathF.Acos(R / toCorner.Length());
                    float leave = reach + spread;

                    List<Vector2> path = new();
                    Arc(path, bowl, R, R, 158f, MathHelper.ToDegrees(leave));
                    Fillet(path, path[^1], corner, end);
                    Add(path, end);
                    strokes.Add(path);
                    break;
                }

                case 3:
                {
                    //Two bowls, one over the other, the lower the wider. The lower stroke starts with the bar of the
                    //middle, so the upper one ends inside it
                    List<Vector2> upper = new();
                    Arc(upper, new Vector2(0f, 0.045f), 0.046f, 0.045f, 150f, -90f);
                    strokes.Add(upper);

                    List<Vector2> lower = new();
                    Add(lower, new Vector2(-0.024f, 0f));
                    Arc(lower, new Vector2(0f, -0.045f), 0.052f, 0.045f, 90f, -150f);
                    strokes.Add(lower);
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(place), place, "A podium has places 1, 2 and 3.");
            }

            return strokes;
        }

        //A point onto a path, unless it is on the last one: a zero-length step has no tangent
        private static void Add(List<Vector2> path, Vector2 point)
        {
            if (path.Count == 0 || Vector2.DistanceSquared(path[^1], point) > 1e-8f) path.Add(point);
        }

        //An elliptic arc from one angle to another, clockwise when the second is the smaller, sampled at STROKE_SPACING
        private static void Arc(List<Vector2> path, Vector2 centre, float radiusU, float radiusV, float fromDegrees, float toDegrees)
        {
            float from = MathHelper.ToRadians(fromDegrees), to = MathHelper.ToRadians(toDegrees);
            int steps = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(to - from) * (radiusU + radiusV) * 0.5f / STROKE_SPACING));

            for (int i = 0; i <= steps; i++)
            {
                (float sin, float cos) = MathF.SinCos(MathHelper.Lerp(from, to, i / (float)steps));
                Add(path, centre + new Vector2(radiusU * cos, radiusV * sin));
            }
        }

        /// <summary>
        /// The corner at <paramref name="corner"/> of the legs from <paramref name="previous"/> and to
        /// <paramref name="next"/>, rounded by a circle of <see cref="STROKE_BEND"/>: the two legs' tangent points
        /// and the arc between them go onto the path, and the legs themselves stay as they are.
        /// </summary>
        private static void Fillet(List<Vector2> path, Vector2 previous, Vector2 corner, Vector2 next)
        {
            Vector2 a = Vector2.Normalize(previous - corner), c = Vector2.Normalize(next - corner);
            float opening = MathF.Acos(Math.Clamp(Vector2.Dot(a, c), -1f, 1f));
            float tangent = STROKE_BEND / MathF.Tan(opening * 0.5f);
            Vector2 centre = corner + Vector2.Normalize(a + c) * (STROKE_BEND / MathF.Sin(opening * 0.5f));

            Vector2 from = corner + a * tangent, to = corner + c * tangent;
            float start = MathF.Atan2(from.Y - centre.Y, from.X - centre.X);
            float turn = MathF.Atan2(to.Y - centre.Y, to.X - centre.X) - start;
            while (turn > MathHelper.Pi) turn -= MathHelper.TwoPi;
            while (turn < -MathHelper.Pi) turn += MathHelper.TwoPi;

            int steps = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(turn) * STROKE_BEND / STROKE_SPACING));
            for (int i = 0; i <= steps; i++)
            {
                (float sin, float cos) = MathF.SinCos(start + turn * i / steps);
                Add(path, centre + new Vector2(cos, sin) * STROKE_BEND);
            }
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();

            VertexBuffer = null;
            IndexBuffer = null;
        }
    }
}
