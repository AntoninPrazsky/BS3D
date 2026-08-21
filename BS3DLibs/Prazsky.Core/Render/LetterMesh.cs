using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One capital letter as a solid of <b>round tubes</b> (#248): a mono-line skeleton — straight segments and
    /// elliptical arcs — swept as a circular tube with a domed end on every terminal, so a letter reads as a
    /// bent, inflated pipe rather than as a slab of type. It is the geometry the game's 3D wordmark is built
    /// from (see <c>TitleWordmark</c> in the game).
    /// <para>
    /// <b>Why tubes and not extruded font outlines.</b> The obvious road was the real Anton outlines the 2D
    /// title is set in, pulled into depth. Two things ruled against it, and the second is the one that
    /// decided it. First, cost: a closed outline with counters needs a polygon triangulator with hole
    /// merging, which is the only piece of geometry maths in this repository that would have had no
    /// precedent — and a triangulator's failures are invisible in the code and only show as a wrong picture.
    /// Second, and more important: an extruded letter's front face is <b>one flat plane with one constant
    /// normal</b>, so it takes one flat shade from the light rig and its three-dimensionality lives entirely
    /// on the bevel. A round cross-section has every normal in the half-circle facing the lens, so it carries
    /// a light-to-dark gradient, a specular streak down its length and a Fresnel rim on its silhouette — the
    /// same three things that make this game's balls read as spheres rather than as discs. A wordmark whose
    /// whole job is to say "this game is 3D" wants the shape that says it loudest.
    /// </para>
    /// <para>
    /// <b>The letterforms are authored here, not derived from a font.</b> They are a geometric mono-line
    /// alphabet (see <see cref="LetterShapes"/>) — one stroke width throughout, circles for the bowls — which
    /// is the family a tube can actually be: a face with modulated stroke weight cannot be swept with one
    /// radius, and a condensed poster face's flat terminals fight a domed end. Eleven capitals and a space
    /// are authored, which is what the game's name needs; this is a wordmark, not a typesetter.
    /// </para>
    /// <para>
    /// The letter stands on <b>y = 0</b> and is <see cref="LetterShapes.CAP_HEIGHT"/> tall in mesh units, with
    /// its left sidebearing at <b>x = 0</b>, so a caller scales by the cap height it wants and walks the pen by
    /// <see cref="Advance"/>. Nothing here knows how big a title is. The tube is centred on the plane
    /// <b>z = 0</b>, so a letter is as deep as it is thick and turning it about Y shows its round side.
    /// </para>
    /// </summary>
    public sealed class LetterMesh : IProceduralMesh, IDisposable
    {
        //FACETS AROUND THE TUBE. The wordmark stands about an eighth of the frame's height per capital, so a
        //tube is roughly a fortieth of the frame's width across — nothing like the trophy's 64, and nothing
        //like a scattered prop's 12 either, because the silhouette of every stroke is on show against the sky
        //and the specular streak runs along it. Sixteen puts the facet error on a tube of that size well under
        //a pixel at 1600x900 and still under one at 4K, since the wordmark is sized in fractions of the frame
        //rather than in pixels.
        private const int DEFAULT_SIDES = 16;

        //RINGS IN A DOMED END, from the equator to the pole. Three is a dome; two reads as a chamfered cut and
        //one is a cone. It is per TERMINAL rather than per letter, and a letter like E has eight of them, which
        //is why this is not simply raised: see the vertex budget in the class remarks below.
        private const int CAP_RINGS = 3;

        //HOW FAR AN ARC MAY DEPART FROM THE CHORD THAT REPLACES IT, in cap heights. The step count of every
        //arc is solved from this rather than fixed, so a bowl of radius 0.5 and a shoulder of radius 0.25 come
        //out equally round instead of the small one being over-sampled and the large one visibly polygonal.
        //0.0015 of a cap height is a fifth of a pixel at the size the wordmark stands on a 1080p frame, and
        //still about a third of a pixel at 4K.
        private const float ARC_TOLERANCE = 0.0015f;

        /// <summary>The character this mesh draws, as it was resolved (always upper case).</summary>
        public char Character { get; }

        /// <summary>
        /// How far the pen moves after this letter, in cap heights, <b>before</b> tracking and before the tube's
        /// own radius is accounted for. See <see cref="LetterShapes.Advance"/>.
        /// </summary>
        public float Advance { get; }

        /// <summary>The tube's radius in cap heights, as it was built. Half the stroke weight.</summary>
        public float TubeRadius { get; }

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// Builds one letter. A character with no strokes (a space) is not a mesh and throws — the caller lays
        /// spaces out by <see cref="LetterShapes.Advance"/> and never asks for geometry it cannot see.
        /// </summary>
        /// <param name="tubeRadius">
        /// Half the stroke weight, in cap heights. It is a parameter rather than a constant because the
        /// wordmark builds every letter twice — once at the stroke weight and once fatter, for the dark
        /// keyline behind it — and the two have to come off the same skeleton or the outline would not
        /// follow the letter.
        /// </param>
        /// <param name="sides">Facets around the tube; the outline pass asks for fewer, being one flat tone.</param>
        public LetterMesh(GraphicsDevice device, char character, float tubeRadius, int sides = DEFAULT_SIDES)
        {
            Character = char.ToUpperInvariant(character);
            TubeRadius = tubeRadius;

            IReadOnlyList<LetterStroke> strokes = LetterShapes.Strokes(Character);
            if (strokes == null || strokes.Count == 0)
                throw new ArgumentException($"'{character}' has no strokes to sweep.", nameof(character));

            Advance = LetterShapes.Advance(Character);

            MeshBuilder builder = new();
            foreach (LetterStroke stroke in strokes) SweepStroke(builder, stroke, tubeRadius, sides);

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(device);

            //Conservative and cheap: the letter's own em box grown by the tube, which also covers the domes.
            //Nothing culls against it (no instanced draw in this repository reads IProceduralMesh's sphere) —
            //it is the interface's contract, and a wordmark held against the lens is the last thing that
            //should ever be culled anyway.
            float halfWidth = Advance * 0.5f + tubeRadius;
            float halfHeight = LetterShapes.CAP_HEIGHT * 0.5f + tubeRadius;
            BoundingSphere = new BoundingSphere(
                new Vector3(Advance * 0.5f, LetterShapes.CAP_HEIGHT * 0.5f, 0f),
                MathF.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight + tubeRadius * tubeRadius));
        }

        /// <summary>
        /// One stroke: a circular tube swept along the stroke's own path with a hemispherical dome closing each
        /// end. The sweep's frame is <b>fixed</b> rather than parallel-transported — Z is out of the letter's
        /// plane and every path here is planar, so there is no twist to accumulate and none of a Frenet frame's
        /// flip where the curvature reverses. That is <see cref="TrophyMesh"/>'s handle, and the argument is
        /// the same one.
        /// <para>
        /// The dome is not a separate cap: it is <b>extra rings on the same sweep</b>, prepended and appended,
        /// whose radius shrinks by <c>sin θ</c> while their centre walks out along the tangent by
        /// <c>cos θ</c> and their normal tilts with them. So the wall loop below closes the letter's ends
        /// without knowing they are ends, and the tube meets its dome with no crease and no seam.
        /// </para>
        /// <para>
        /// Where two strokes share an endpoint the two domes simply overlap, and the joint reads as a ball
        /// where the strokes meet — which is what a letter drawn with a round nib looks like, and is the
        /// reason no mitring or welding is done here. The cost of that decision is the terminal count: E has
        /// eight domes and eight overlapping hemispheres, at <see cref="CAP_RINGS"/> rings apiece.
        /// </para>
        /// </summary>
        private static void SweepStroke(MeshBuilder builder, LetterStroke stroke, float radius, int sides)
        {
            //The skeleton, then the domes on either end of it. Sampled into a list rather than an array
            //because an arc's step count is solved from its own curvature, so the length is not known until
            //it has been walked; this runs once, at load, so the allocation is not on any frame's path.
            List<(Vector3 Centre, Vector3 Tangent)> path = stroke.Sample(ARC_TOLERANCE);

            int ringCount = path.Count + 2 * CAP_RINGS;
            Vector3[,] ring = new Vector3[ringCount, sides];
            Vector3[,] ringNormal = new Vector3[ringCount, sides];
            bool[] degenerate = new bool[ringCount];

            for (int r = 0; r < ringCount; r++)
            {
                //Which sample this ring belongs to, and how far round the dome it is. theta is the polar
                //angle away from the equator: 0 on the tube proper, pi/2 at a pole.
                int sample;
                float theta;
                float alongTangent;

                if (r < CAP_RINGS)
                {
                    sample = 0;
                    theta = (CAP_RINGS - r) / (float)CAP_RINGS * MathHelper.PiOver2;
                    alongTangent = -MathF.Sin(theta);        //the entry dome walks BACK along the tangent
                }
                else if (r >= CAP_RINGS + path.Count)
                {
                    sample = path.Count - 1;
                    theta = (r - (CAP_RINGS + path.Count) + 1) / (float)CAP_RINGS * MathHelper.PiOver2;
                    alongTangent = MathF.Sin(theta);
                }
                else
                {
                    sample = r - CAP_RINGS;
                    theta = 0f;
                    alongTangent = 0f;
                }

                (Vector3 centre, Vector3 tangent) = path[sample];

                float ringRadius = radius * MathF.Cos(theta);
                Vector3 ringCentre = centre + tangent * (radius * alongTangent);

                degenerate[r] = ringRadius <= 1e-5f;

                Vector3 axis1 = Vector3.UnitZ;              //out of the letter's plane
                Vector3 axis2 = Vector3.Normalize(Vector3.Cross(tangent, axis1));

                //The normal tilts with the dome: on the tube it is purely radial, at a pole purely tangential.
                float tilt = MathF.Sign(alongTangent) * MathF.Sin(theta);
                float radial = MathF.Cos(theta);

                for (int i = 0; i < sides; i++)
                {
                    float phi = i / (float)sides * MathHelper.TwoPi;
                    (float sp, float cp) = MathF.SinCos(phi);

                    Vector3 outward = axis1 * cp + axis2 * sp;

                    ring[r, i] = ringCentre + outward * ringRadius;
                    ringNormal[r, i] = Vector3.Normalize(outward * radial + tangent * tilt);
                }
            }

            //The wall, including both domes. A ring that has collapsed to a pole is emitted as triangles
            //rather than as quads with two coincident corners — a degenerate quad is two triangles of which
            //one has no area, and the builder would have to decide the winding of a triangle that has none.
            for (int r = 0; r < ringCount - 1; r++)
            {
                bool lowPole = degenerate[r];
                bool highPole = degenerate[r + 1];
                if (lowPole && highPole) continue;

                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;

                    Vector3 face = Vector3.Normalize(
                        ringNormal[r, i] + ringNormal[r, j] + ringNormal[r + 1, i] + ringNormal[r + 1, j]);

                    if (highPole)
                        builder.AddTriangle(
                            ring[r, i], ring[r, j], ring[r + 1, i],
                            ringNormal[r, i], ringNormal[r, j], ringNormal[r + 1, i], face);
                    else if (lowPole)
                        builder.AddTriangle(
                            ring[r, i], ring[r + 1, j], ring[r + 1, i],
                            ringNormal[r, i], ringNormal[r + 1, j], ringNormal[r + 1, i], face);
                    else
                        builder.AddQuad(
                            ring[r, i], ring[r, j], ring[r + 1, j], ring[r + 1, i],
                            ringNormal[r, i], ringNormal[r, j],
                            ringNormal[r + 1, j], ringNormal[r + 1, i], face);
                }
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

    /// <summary>
    /// One stroke of a letter's skeleton: either a straight segment or an elliptical arc, in the letter's own
    /// em plane (<c>x</c> right from the left sidebearing, <c>y</c> up from the baseline, <c>z = 0</c>).
    /// <para>
    /// An arc carries a <b>signed</b> sweep — <see cref="ToAngle"/> may be less than <see cref="FromAngle"/> —
    /// because the direction the pen travels is what an S and a 3 are made of, and reversing one of their two
    /// bowls turns the letter into a spiral. Elliptical rather than circular so a bowl can be wider than it is
    /// tall (D's) or narrower (B's), which is most of what tells one capital's bowl from another's.
    /// </para>
    /// </summary>
    internal readonly struct LetterStroke
    {
        private readonly bool _isArc;
        private readonly Vector2 _a, _b;         //segment: the two ends. Arc: the centre and the two radii.
        private readonly float _from, _to;       //arc only, radians

        private LetterStroke(bool isArc, Vector2 a, Vector2 b, float from, float to)
        {
            _isArc = isArc;
            _a = a;
            _b = b;
            _from = from;
            _to = to;
        }

        /// <summary>A straight segment from (<paramref name="x0"/>, <paramref name="y0"/>) to (<paramref name="x1"/>, <paramref name="y1"/>).</summary>
        public static LetterStroke Line(float x0, float y0, float x1, float y1) =>
            new(false, new Vector2(x0, y0), new Vector2(x1, y1), 0f, 0f);

        /// <summary>
        /// An elliptical arc about (<paramref name="cx"/>, <paramref name="cy"/>) with radii
        /// <paramref name="rx"/>/<paramref name="ry"/>, from <paramref name="fromDegrees"/> to
        /// <paramref name="toDegrees"/> — measured the mathematician's way (0 towards +x, 90 towards +y) and
        /// travelled in the direction the two imply, so a descending sweep is written as the larger angle
        /// first.
        /// </summary>
        public static LetterStroke Arc(float cx, float cy, float rx, float ry, float fromDegrees, float toDegrees) =>
            new(true, new Vector2(cx, cy), new Vector2(rx, ry),
                MathHelper.ToRadians(fromDegrees), MathHelper.ToRadians(toDegrees));

        /// <summary>
        /// The stroke as a chain of ring centres with the exact tangent at each — never a difference of
        /// neighbouring samples, which has nothing on one side of it at the ends and is where a swept frame
        /// goes wrong first.
        /// <para>
        /// A segment is two samples: a straight tube needs no more, and adding rings along it would only
        /// spend vertices. An arc's step count is solved from <paramref name="tolerance"/>: the sagitta of a
        /// circular arc over an angle <c>d</c> is <c>r(1 - cos(d/2))</c>, near enough <c>r d² / 8</c>, so the
        /// step that keeps the chord within the tolerance is <c>sqrt(8 t / r)</c> — taken against the
        /// <b>larger</b> radius of the ellipse, which is where the departure is worst.
        /// </para>
        /// </summary>
        public List<(Vector3 Centre, Vector3 Tangent)> Sample(float tolerance)
        {
            List<(Vector3, Vector3)> samples = new();

            if (!_isArc)
            {
                Vector3 tangent = Vector3.Normalize(new Vector3(_b.X - _a.X, _b.Y - _a.Y, 0f));
                samples.Add((new Vector3(_a.X, _a.Y, 0f), tangent));
                samples.Add((new Vector3(_b.X, _b.Y, 0f), tangent));
                return samples;
            }

            float span = _to - _from;
            float radius = MathF.Max(_b.X, _b.Y);
            float step = MathF.Sqrt(8f * tolerance / MathF.Max(radius, 1e-4f));
            int steps = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(span) / step));

            for (int i = 0; i <= steps; i++)
            {
                float angle = _from + span * i / steps;
                (float sa, float ca) = MathF.SinCos(angle);

                //The ellipse and its own derivative with respect to the parameter, which is exact. The sign of
                //the span carries the direction of travel into the tangent, which is what keeps the domes on
                //the outside of the stroke rather than inside it.
                Vector3 centre = new(_a.X + _b.X * ca, _a.Y + _b.Y * sa, 0f);
                Vector3 tangent = Vector3.Normalize(new Vector3(-_b.X * sa * span, _b.Y * ca * span, 0f));

                samples.Add((centre, tangent));
            }

            return samples;
        }
    }

    /// <summary>
    /// The mono-line capital alphabet the 3D wordmark is set in (#248): eleven letters and a space, which is
    /// what "Bubble Shooter 3D" needs. Authored here rather than read out of a font — see
    /// <see cref="LetterMesh"/>'s remarks for why a tube alphabet cannot be a text face's outlines.
    /// <para>
    /// Every glyph is drawn on one grid: the baseline at <c>y = 0</c>, the cap height at
    /// <see cref="CAP_HEIGHT"/> = 1, the left sidebearing at <c>x = 0</c>, and the advance the letter's own
    /// <b>skeleton</b> width — the tube's radius stands <i>outside</i> that on both sides, which is why a
    /// caller's tracking has to clear two radii before any of it is read as letter spacing (see
    /// <see cref="WordWidth"/>).
    /// </para>
    /// <para>
    /// The bowls are <b>circles and half-circles, not curves fitted to a drawing</b>, so the family is
    /// geometric — the Futura/DIN corner of the type world rather than the poster faces the 2D title is set
    /// in. That is deliberate on two counts: a constant stroke weight is what a single tube radius can be at
    /// all, and a round terminal wants a round letterform to sit in.
    /// </para>
    /// <para>
    /// <b>S and 3 are the two that had to be constructed rather than sketched</b>, and they are worth reading
    /// before either is touched. Both are two arcs meeting at the waist <i>with a common horizontal
    /// tangent</i>: each bowl's centre sits one bowl-radius above or below the waist, so the waist is the
    /// bottom of the upper circle and the top of the lower one, and the two curves flow through it instead of
    /// meeting at a corner. What then separates an S from a 3 is only which way each bowl is travelled — the
    /// S opens its upper bowl to the left and its lower to the right, the 3 opens both to the left. A sweep
    /// written in the wrong direction does not make a wrong-looking letter, it makes a spiral, and that is
    /// what the first pass of both of them was.
    /// </para>
    /// </summary>
    public static class LetterShapes
    {
        /// <summary>The height of a capital in mesh units. Every figure in this class is a fraction of it.</summary>
        public const float CAP_HEIGHT = 1f;

        //A space is an advance and nothing else. Narrow for a wordmark whose words are stacked on their own
        //lines anyway: it only ever separates words inside one line, which this title never has.
        private const float SPACE_ADVANCE = 0.30f;

        //THE ALPHABET. Read the class remarks on S and 3 before touching either.
        private static readonly Dictionary<char, (float Advance, LetterStroke[] Strokes)> GLYPHS = new()
        {
            //Stem, then the two bowls with their bars. The lower bowl is wider than the upper one, which is
            //what stops a B reading as an 8 — and both are half-circles travelled downwards, so they are
            //written with the larger angle first.
            ['B'] = (0.62f, new[]
            {
                LetterStroke.Line(0.00f, 0.00f, 0.00f, 1.00f),
                LetterStroke.Line(0.00f, 1.00f, 0.30f, 1.00f),
                LetterStroke.Arc(0.30f, 0.75f, 0.26f, 0.25f, 90f, -90f),
                LetterStroke.Line(0.30f, 0.50f, 0.00f, 0.50f),
                LetterStroke.Arc(0.32f, 0.25f, 0.30f, 0.25f, 90f, -90f),
                LetterStroke.Line(0.32f, 0.00f, 0.00f, 0.00f),
            }),

            //Two stems and the half-circle bottom that joins them, swept left to right through the bottom.
            ['U'] = (0.62f, new[]
            {
                LetterStroke.Line(0.00f, 1.00f, 0.00f, 0.25f),
                LetterStroke.Arc(0.31f, 0.25f, 0.31f, 0.25f, 180f, 360f),
                LetterStroke.Line(0.62f, 0.25f, 0.62f, 1.00f),
            }),

            ['L'] = (0.50f, new[]
            {
                LetterStroke.Line(0.00f, 1.00f, 0.00f, 0.00f),
                LetterStroke.Line(0.00f, 0.00f, 0.50f, 0.00f),
            }),

            //The middle bar is short of the other two, which is the one thing that keeps an E from reading as
            //a comb: three bars of one length have no hierarchy in them.
            ['E'] = (0.54f, new[]
            {
                LetterStroke.Line(0.00f, 0.00f, 0.00f, 1.00f),
                LetterStroke.Line(0.00f, 1.00f, 0.54f, 1.00f),
                LetterStroke.Line(0.00f, 0.50f, 0.48f, 0.50f),
                LetterStroke.Line(0.00f, 0.00f, 0.54f, 0.00f),
            }),

            //Both bowls start at the waist (0.29, 0.50) and are swept 250 degrees outwards: the upper one
            //anticlockwise from the bottom of its circle round to the top right, the lower one clockwise from
            //the top of its circle round to the bottom left. See the class remarks.
            ['S'] = (0.58f, new[]
            {
                LetterStroke.Arc(0.29f, 0.75f, 0.29f, 0.25f, -90f, -340f),
                LetterStroke.Arc(0.29f, 0.25f, 0.29f, 0.25f, 90f, -160f),
            }),

            ['H'] = (0.62f, new[]
            {
                LetterStroke.Line(0.00f, 0.00f, 0.00f, 1.00f),
                LetterStroke.Line(0.62f, 0.00f, 0.62f, 1.00f),
                LetterStroke.Line(0.00f, 0.50f, 0.62f, 0.50f),
            }),

            //One closed ellipse, and the only glyph here with no terminal at all — so the only one whose
            //domes are both spent on the same spot, where the sweep comes back to where it started.
            ['O'] = (0.66f, new[]
            {
                LetterStroke.Arc(0.33f, 0.50f, 0.33f, 0.50f, 0f, 360f),
            }),

            ['T'] = (0.58f, new[]
            {
                LetterStroke.Line(0.00f, 1.00f, 0.58f, 1.00f),
                LetterStroke.Line(0.29f, 0.00f, 0.29f, 1.00f),
            }),

            //B's upper half with a leg. The leg starts INSIDE the bar rather than at its end, so the joint is
            //a fork rather than a hinge — at a round nib's weight a leg hung off the bowl's tip reads as a
            //broken letter.
            ['R'] = (0.60f, new[]
            {
                LetterStroke.Line(0.00f, 0.00f, 0.00f, 1.00f),
                LetterStroke.Line(0.00f, 1.00f, 0.30f, 1.00f),
                LetterStroke.Arc(0.30f, 0.75f, 0.28f, 0.25f, 90f, -90f),
                LetterStroke.Line(0.30f, 0.50f, 0.00f, 0.50f),
                LetterStroke.Line(0.24f, 0.50f, 0.60f, 0.00f),
            }),

            //B's two bowls with the stem taken away and both openings turned to the left.
            ['3'] = (0.56f, new[]
            {
                LetterStroke.Arc(0.28f, 0.75f, 0.28f, 0.25f, -90f, 160f),
                LetterStroke.Arc(0.28f, 0.25f, 0.28f, 0.25f, 90f, -160f),
            }),

            //One bowl over the full cap height, so it is the widest glyph here and the reason the wordmark's
            //badge line is measured rather than assumed.
            ['D'] = (0.64f, new[]
            {
                LetterStroke.Line(0.00f, 0.00f, 0.00f, 1.00f),
                LetterStroke.Line(0.00f, 1.00f, 0.28f, 1.00f),
                LetterStroke.Arc(0.28f, 0.50f, 0.36f, 0.50f, 90f, -90f),
                LetterStroke.Line(0.28f, 0.00f, 0.00f, 0.00f),
            }),
        };

        /// <summary>
        /// True when this alphabet can set <paramref name="character"/> — case-insensitively, since the
        /// wordmark is set in capitals whatever case the title string is written in. A space is supported and
        /// has no strokes.
        /// </summary>
        public static bool Supports(char character)
        {
            char upper = char.ToUpperInvariant(character);
            return upper == ' ' || GLYPHS.ContainsKey(upper);
        }

        /// <summary>
        /// How far the pen moves after <paramref name="character"/>, in cap heights, before
        /// <see cref="TRACKING"/>. Zero for anything this alphabet cannot set, so a caller that has already
        /// checked <see cref="Supports"/> lays out nothing for it.
        /// </summary>
        public static float Advance(char character)
        {
            char upper = char.ToUpperInvariant(character);
            if (upper == ' ') return SPACE_ADVANCE;

            return GLYPHS.TryGetValue(upper, out var glyph) ? glyph.Advance : 0f;
        }

        /// <summary>
        /// The width one word occupies from the left edge of its first letter's skeleton to the right edge of
        /// its last, in cap heights — <b>the skeleton's width, not the ink's</b>: the tube stands a radius
        /// outside it at each end, which the caller adds because only the caller knows the stroke weight.
        /// Trailing tracking is not counted, so two words of one letter each measure their letters.
        /// <para>
        /// <paramref name="tracking"/> is the caller's and not this class's on purpose. The advances above are
        /// the skeletons' widths, so the gap between two letters' <i>ink</i> is the tracking less <b>two tube
        /// radii</b> — one off each neighbour — and only the caller knows the stroke weight it is sweeping at.
        /// A tracking figure baked in here would be daylight at one weight and an overlap at another.
        /// </para>
        /// </summary>
        public static float WordWidth(string word, float tracking)
        {
            if (string.IsNullOrEmpty(word)) return 0f;

            float width = 0f;
            for (int i = 0; i < word.Length; i++)
            {
                width += Advance(word[i]);
                if (i < word.Length - 1) width += tracking;
            }

            return width;
        }

        /// <summary>The strokes of one glyph, or null for a space and for anything unsupported.</summary>
        internal static IReadOnlyList<LetterStroke> Strokes(char character) =>
            GLYPHS.TryGetValue(char.ToUpperInvariant(character), out var glyph) ? glyph.Strokes : null;
    }
}
