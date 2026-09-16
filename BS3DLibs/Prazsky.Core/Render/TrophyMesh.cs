using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A trophy cup (#183): a jewelled plinth, a fluted stem between two collars, a gadrooned knop, a tall bowl
    /// with a raised band under its turned-out lip, and — on the top two tiers, Gold and Diamond, since #232 —
    /// a handle on each side. Built procedurally like everything else here rather than loaded, for the reason
    /// the sky domes stopped being eighteen <c>.dae</c> files in #113.
    /// <para>
    /// <b>Taller and articulated since #429.</b> The owner's report was that every tier read as plain and
    /// primitive, and the ask was a cup that is taller and more decorated, with gems in the spirit of the
    /// Crown of Saint Wenceslas. The profile was redrawn against locally generated references (#441): the
    /// bowl is two thirds as wide against the same height, the plinth carries a drum for a row of stones, the
    /// stem has collars and flutes, and the bowl a band for a second row. The stones, their settings and the
    /// beaded mouldings are NOT part of this mesh — they are small instanced meshes (<see cref="TrophyOrnaments"/>)
    /// placed on the surfaces this class names (<see cref="Ornament"/>, <see cref="DRUM_RADIUS"/>, …), because a
    /// stone is a different material from the metal it is set in, and one mesh is one material.
    /// </para>
    /// <para>
    /// The body is a surface of revolution and could have been a <see cref="LatheMesh"/>, which is what the
    /// issue's own sketch proposed. It is built here on <see cref="MeshBuilder"/> instead for one reason: the
    /// handles are <b>not</b> surfaces of revolution about the cup's axis, and a trophy that arrives as two
    /// meshes is two renderers, two materials to keep in step and two draw calls for one object. Built together
    /// they are one buffer, one material and one draw — and the builder's winding correction covers the swept
    /// handles as readily as the revolved body, which is the whole point of it being a service.
    /// </para>
    /// <para>
    /// The cup stands on <b>y = 0</b> and is <see cref="HEIGHT"/> tall in mesh units, so a caller scales by the
    /// height it wants and translates to where the foot should sit. Nothing here knows how big a trophy is.
    /// </para>
    /// </summary>
    public sealed class TrophyMesh : IProceduralMesh, IDisposable
    {
        /// <summary>The cup's overall height in mesh units, foot to lip. The profile below is authored to it.</summary>
        public const float HEIGHT = 1f;

        //Facets around the axis. A trophy is presented CLOSE and is the one object on screen the player is
        //asked to look at — and since #226 it stands most of the frame's height tall — so it cannot be the
        //twelve or sixteen a scattered prop gets away with: a faceted silhouette on a mirror-finish surface
        //reads as a low-poly model rather than as metal.
        //
        //NINETY-SIX since #429, and not for the silhouette: the narrower bowl is round at 64. The flutes and
        //the gadroons (see REEDING) are waves AROUND the axis, and a wave wants several vertices across it or
        //it is a zig-zag — 96 gives the stem's twelve flutes eight apiece and the knop's sixteen gadroons six.
        //
        //THE SAME FIGURE SERVES THE CRYSTAL, which had a coarser faceted body of its own twice and has it
        //no more. #231 cut the facets into the geometry (24 segments, authored rings, flat normals), and
        //the rim came out a 24-gon; #271's first rework kept them in the normals alone — the same chords
        //as every tier with each ring normal's azimuth snapped to 24 directions, flat bands on a round
        //silhouette — and the owner's ruling held through both, in the same words: a cup has a cup's shape,
        //"crystal sharp" is an optical sharpness, and visible edges are edges whether they are geometric or
        //shading. Every tier draws this one smooth revolve; the crystal's look is its material's (see
        //TrophyPodium — transparency, the Fresnel rim, the reflected environment). The flutes do not reopen
        //that ruling: they are rounded waves on smooth normals, a shape, and there is no edge anywhere in them.
        private const int SEGMENTS = 96;

        //THE PROFILE'S OWN RESOLUTION, which the segment count cannot give: the silhouette seen side-on is
        //the profile, and the authored rings are few enough that the bowl's flare read as five straight
        //chords — at a third of the frame that passed, at presentation size it read as low-poly however
        //many facets the axis had. The smooth runs between creases are therefore SUBDIVIDED (see
        // <see cref="DensifyProfile"/>) into a curve, three samples a span, without a single authored
        //coordinate moving. Three was once the most that fitted under the builder's 16-bit ceiling; since
        //#429 the builder takes 32-bit indices past it, and three stays because the curve is round with it.
        private const int PROFILE_SUBDIVISIONS = 3;

        //Facets around the handle's own tube, and steps along its sweep. Fewer facets than the body's: a
        //handle is a tenth of the bowl's diameter, so the same angular error is a tenth of the pixels. More
        //steps than the old arc had, because since #429 the sweep is a cubic that turns twice.
        private const int HANDLE_SIDES = 20, HANDLE_STEPS = 40;

        //THE HANDLE'S ROOTS ARE BURIED, AND THE BOWL'S WALL IS THINNER THAN THE TUBE. Those two facts do not
        //fit together at full thickness, and the cup shipped with the lower roots half out in the open (#228):
        //the end ring's lowest point stood 0.020 OUTSIDE the bowl, which is what a player sees as a cut pipe
        //hanging off the side of the trophy. So the tube TAPERS towards each root, and the roots are planted
        //where the taper fits: both end rings are at least 0.012 inside the body and no part of the tube
        //comes within 0.012 of the bowl's hollow — verified against the DENSIFIED profile, which is the
        //surface that is actually drawn, rather than against the authored rings, and re-verified for #429's
        //profile, where the upper root is planted in the raised band (the one place the upper wall has depth)
        //and the lower in the solid base under the bowl's floor, which #429 made deeper for it.
        //
        //The taper is short enough that it is spent before the tube leaves the cup, so what shows outside is
        //a handle of very nearly full thickness narrowing slightly as it enters — which is what a cast root
        //looks like — and not a stalk.
        private const float HANDLE_TUBE_RADIUS = 0.025f;
        private const float HANDLE_ROOT_RADIUS = 0.015f;
        private const float HANDLE_ROOT_SPAN = 0.035f;

        /// <summary>
        /// One ring of the body's cross-section. <paramref name="Crease"/> keeps the runs above and below it
        /// on their own normals — a lip turning back on itself, or a plinth meeting its own wall, has to break
        /// or the flat above it reads as curved for the last span before the edge.
        /// </summary>
        private readonly record struct Ring(float Radius, float Y, bool Crease = false);

        /// <summary>
        /// The plinth's drum: a short vertical wall between two steps, which carries the lower row of stones.
        /// Straight by construction — both its rings are creases, so <see cref="DensifyProfile"/> leaves the
        /// span alone and a stone set on it sits on exactly this radius.
        /// </summary>
        public const float DRUM_RADIUS = 0.216f, DRUM_BOTTOM_Y = 0.028f, DRUM_TOP_Y = 0.110f;

        /// <summary>
        /// The raised band under the lip, which carries the upper row of stones and — at its two edges — two
        /// beaded mouldings. Straight between creases for <see cref="DRUM_RADIUS"/>'s reason.
        /// </summary>
        public const float BAND_RADIUS = 0.304f, BAND_BOTTOM_Y = 0.862f, BAND_TOP_Y = 0.942f;

        /// <summary>The height of the calyx row: the stones round the bowl's own foot, on its curved flare.</summary>
        public const float CALYX_Y = 0.700f;

        //THE PROFILE, traced as one continuous polyline: from the centre of the underside, out along the foot,
        //up the jewelled drum, in and up the trumpet into the stem, over the collars and the knop, out into
        //the bowl, over the band and the lip, and back DOWN THE INSIDE to the centre of the bowl's floor. Both
        //ends touch the axis, so the solid is closed by the profile itself and needs no separate caps.
        //
        //The bowl being hollow is not decoration: this thing is shown from above as it turns, and a cup filled
        //in flat at the brim reads as a lamp.
        //
        //It was authored coarser than this and photographed badly twice, which is worth keeping: with a single
        //point at the knop and one flat disc for a foot, the cup read as an eggcup on a washer — the knop came
        //out as a hard bowtie because two straight runs met at a point, and a base with no step to it has no
        //weight (#183). Then it read as PLAIN (#429): a wide low bowl on a short stem is a sports-day cup, and
        //the owner's words were taller, more luxurious, gems. Neither is a shading problem, and no material
        //fixed either: a silhouette is read before a surface is.
        //
        //#429's proportions come from the references #441 generated: the bowl is about 0.61 across the lip
        //against 0.88 before, the stem a third of the height, and every change of section is a moulding —
        //a step, a collar, a band — because an unbroken run is what reads as turned on a lathe in one pass.
        private static readonly Ring[] PROFILE =
        {
            new(0.000f, 0.000f),
            new(0.238f, 0.000f, true),               //the foot's underside, out to its edge
            new(0.238f, 0.018f, true),               //the foot rim's own wall
            new(DRUM_RADIUS, DRUM_BOTTOM_Y, true),   //a step in onto the drum
            new(DRUM_RADIUS, DRUM_TOP_Y, true),      //the drum: the lower row of stones is set on this wall
            new(0.196f, 0.118f, true),               //and a step in off it
            new(0.158f, 0.134f),                     //the trumpet rising off the plinth
            new(0.110f, 0.162f),
            new(0.080f, 0.203f),
            new(0.064f, 0.238f),
            new(0.072f, 0.251f),                     //the lower collar, a round moulding
            new(0.081f, 0.262f),
            new(0.071f, 0.273f),
            new(0.055f, 0.290f),                     //the stem, fluted (REEDING), widening a little upwards
            new(0.057f, 0.380f),
            new(0.063f, 0.462f),
            new(0.075f, 0.474f),                     //the upper collar
            new(0.083f, 0.485f),
            new(0.073f, 0.496f),
            new(0.070f, 0.507f),                     //the knop, gadrooned (REEDING), in five points so it is round
            new(0.097f, 0.526f),
            new(0.109f, 0.548f),
            new(0.099f, 0.570f),
            new(0.071f, 0.589f),
            new(0.057f, 0.604f),                     //the neck
            new(0.078f, 0.622f),                     //the bowl's own foot flares off it
            new(0.130f, 0.648f),
            new(0.186f, 0.690f),
            new(0.234f, 0.742f),
            new(0.265f, 0.800f),
            new(0.280f, 0.842f),
            new(0.288f, 0.856f, true),               //the band: a step out…
            new(BAND_RADIUS, BAND_BOTTOM_Y, true),
            new(BAND_RADIUS, BAND_TOP_Y, true),      //…its wall, where the upper row of stones is set…
            new(0.292f, 0.950f, true),               //…and a step back in
            new(0.300f, 0.972f),
            new(0.322f, 0.998f, true),               //the lip, turned out
            new(0.304f, 0.990f, true),               //and back down inside
            new(0.281f, 0.964f),
            new(0.262f, 0.928f),                     //thinner inside the band than outside it, so the
            new(0.248f, 0.860f),                     //handle's upper root has metal round it
            new(0.217f, 0.780f),
            new(0.168f, 0.716f),
            new(0.100f, 0.692f),
            new(0.000f, 0.678f)                      //the bowl's inside floor, high enough over the neck to
                                                     //bury the handles' lower roots in solid metal
        };

        /// <summary>
        /// A band of the body that is reeded around the axis: flutes on the stem, gadroons on the knop. The
        /// radius is multiplied by <c>1 − Depth·w(y)·(½ + ½cos(Count·θ))</c>, where <c>w</c> eases in and out
        /// over <see cref="Ramp"/> at each end so the waves grow out of the smooth surface instead of starting
        /// at a line — a start that was a line would be exactly the visible edge #271 ruled out.
        /// </summary>
        private readonly record struct Reeding(float Bottom, float Top, float Ramp, int Count, float Depth);

        //Both ends of both bands sit under a collar or the knop's own swell, so where a wave fades out is
        //where the eye is already reading a moulding. The counts divide SEGMENTS, or the last wave would
        //meet the first at a different phase and split the body down one seam.
        private static readonly Reeding[] REEDING =
        {
            new(0.282f, 0.468f, 0.020f, 12, 0.16f),   //the stem's flutes
            new(0.510f, 0.586f, 0.012f, 16, 0.09f)    //the knop's gadroons
        };

        //THE HANDLE IS A CUBIC BÉZIER since #429 — a quadratic before that, and a circular arc before that.
        //A handle has to meet the bowl at BOTH ends whatever the bowl's profile does between them, and struck
        //as a circle it did not: the ends landed where the arc's own geometry put them, the upper one buried
        //and the lower one hanging in the air. A Bézier is stated by its ENDS — both are written inside the
        //body at their own heights, so they are buried by construction — and the control points are then free
        //to say how the handle bows without moving either. The quadratic could only bow; the taller cup wants
        //the handle to rise ABOVE its upper root and come down into it, the way a trophy's ear does, and that
        //takes a second control point.
        //
        //"Buried" was a claim rather than a fact until #228 measured it — see HANDLE_ROOT_RADIUS.
        private static readonly Vector2 HANDLE_LOW = new(0.068f, 0.650f);     //in the solid base under the floor
        private static readonly Vector2 HANDLE_OUT = new(0.470f, 0.580f);     //first control: out and a little down
        private static readonly Vector2 HANDLE_UP = new(0.600f, 1.170f);      //second: up past the lip
        private static readonly Vector2 HANDLE_HIGH = new(0.281f, 0.902f);    //in the raised band

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="handles">
        /// Whether to fit the two side handles. Gold and Diamond's own detail since #232 (Diamond alone,
        /// #183): Bronze and Silver stay plain, so the two pairs are told apart by shape before any colour
        /// has said anything.
        /// </param>
        public TrophyMesh(GraphicsDevice graphicsDevice, bool handles)
        {
            MeshBuilder builder = new();

            BuildBody(builder);

            if (handles)
            {
                BuildHandle(builder, +1f);
                BuildHandle(builder, -1f);
            }

            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(graphicsDevice);

            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;

            //Centred on the cup's own middle rather than on its foot, so a culling test is not lopsided. The
            //radius takes the handles whether they are fitted or not — a bounding sphere a little too big
            //costs a cull that would not have happened, and getting it wrong the other way pops the subject of
            //the whole moment out of frame.
            BoundingSphere = new BoundingSphere(new Vector3(0f, HEIGHT * 0.5f, 0f), 0.80f);
        }

        /// <summary>
        /// Where an ornament sits: a local-to-cup matrix that scales a unit ornament to <paramref name="size"/>,
        /// turns its <b>+Y</b> onto the surface's outward <paramref name="normal"/> (given in the profile's
        /// radius–height plane) and puts it at <paramref name="radius"/>, <paramref name="y"/>, at
        /// <paramref name="angle"/> about the axis. <see cref="TrophyOrnaments"/> authors every ornament
        /// standing on its own +Y for exactly this.
        /// </summary>
        public static Matrix Ornament(float radius, float y, Vector2 normal, float angle, float size)
        {
            (float sin, float cos) = MathF.SinCos(angle);

            Vector3 outward = new(cos, 0f, sin);                  //away from the axis
            Vector3 around = new(-sin, 0f, cos);                  //along the ring
            Vector3 up = normal.X * outward + normal.Y * Vector3.Up;

            //A right-handed frame with +Y on the normal: X along the ring, Z = X × Y
            Vector3 third = Vector3.Cross(around, up);

            Matrix placement = new(
                around.X, around.Y, around.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                third.X, third.Y, third.Z, 0f,
                radius * cos, y, radius * sin, 1f);

            return Matrix.CreateScale(size) * placement;
        }

        /// <summary>
        /// The OUTSIDE surface at height <paramref name="y"/>: its radius and its outward normal in the
        /// radius–height plane, read off the densified profile that is actually drawn — so a stone placed on
        /// the curved calyx sits on the curve and not on the chord between two authored rings. Only the run
        /// from the foot to the lip is searched; the inside of the bowl is never an answer.
        /// </summary>
        public static (float Radius, Vector2 Normal) OuterSurface(float y)
        {
            Ring[] profile = DensifyProfile();
            int lip = LipIndex(profile);

            for (int i = 0; i < lip; i++)
            {
                Ring low = profile[i], high = profile[i + 1];
                if (high.Y <= low.Y || y < low.Y || y > high.Y) continue;

                float t = (y - low.Y) / (high.Y - low.Y);
                float dr = high.Radius - low.Radius, dy = high.Y - low.Y;

                return (MathHelper.Lerp(low.Radius, high.Radius, t), Vector2.Normalize(new Vector2(dy, -dr)));
            }

            throw new ArgumentOutOfRangeException(nameof(y), y, "No outside surface at that height.");
        }

        //The lip is the highest ring; everything before it is the outside, everything after it the inside
        private static int LipIndex(Ring[] profile)
        {
            int lip = 0;
            for (int i = 1; i < profile.Length; i++) if (profile[i].Y > profile[lip].Y) lip = i;
            return lip;
        }

        /// <summary>
        /// Subdivides the profile's smooth spans into curves, leaving every span that touches a CREASE
        /// straight — a crease is an edge someone chose (the lip turning over, the plinth's courses), and a
        /// curve driven through it would round that choice away. The samples come from a CENTRIPETAL
        /// Catmull-Rom through the neighbouring rings: centripetal rather than uniform because the authored
        /// rings sit at wildly uneven spacings — hundredths apart through the collars, tenths along the
        /// bowl's flare — and a uniform parameterization through spacing like that bulges where a section
        /// changes gear, which on a mirror finish is a dent the eye finds at once.
        /// </summary>
        private static Ring[] DensifyProfile()
        {
            List<Ring> dense = new(PROFILE.Length * (PROFILE_SUBDIVISIONS + 1));

            for (int i = 0; i < PROFILE.Length - 1; i++)
            {
                dense.Add(PROFILE[i]);

                if (PROFILE[i].Crease || PROFILE[i + 1].Crease) continue;

                Vector2 p0 = RingPoint(Math.Max(0, i - 1));
                Vector2 p1 = RingPoint(i);
                Vector2 p2 = RingPoint(i + 1);
                Vector2 p3 = RingPoint(Math.Min(PROFILE.Length - 1, i + 2));

                for (int s = 1; s <= PROFILE_SUBDIVISIONS; s++)
                {
                    Vector2 sample = CentripetalCatmullRom(p0, p1, p2, p3, s / (float)(PROFILE_SUBDIVISIONS + 1));
                    dense.Add(new Ring(sample.X, sample.Y));
                }
            }

            dense.Add(PROFILE[^1]);
            return dense.ToArray();

            static Vector2 RingPoint(int index) => new(PROFILE[index].Radius, PROFILE[index].Y);
        }

        /// <summary>
        /// One point of a centripetal Catmull-Rom spline (Barry–Goldman), evaluated at <paramref name="t"/>
        /// across the segment <paramref name="p1"/>–<paramref name="p2"/> with <paramref name="p0"/> and
        /// <paramref name="p3"/> lending the tangents. Clamped callers (repeating an end ring) make the
        /// tangent one-sided, which is what the profile's first and last spans want.
        /// </summary>
        private static Vector2 CentripetalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            //Centripetal knots: each interval's length is the SQUARE ROOT of the distance it spans, so the
            //parameter slows down through tight sections instead of swinging through them. The epsilon
            //keeps a coincident pair (a clamped end) from dividing by zero.
            float t0 = 0f;
            float t1 = MathF.Sqrt((p1 - p0).Length()) + 1e-6f;
            float t2 = t1 + MathF.Sqrt((p2 - p1).Length()) + 1e-6f;
            float t3 = t2 + MathF.Sqrt((p3 - p2).Length()) + 1e-6f;

            float tt = t1 + t * (t2 - t1);

            Vector2 a1 = p0 * ((t1 - tt) / (t1 - t0)) + p1 * ((tt - t0) / (t1 - t0));
            Vector2 a2 = p1 * ((t2 - tt) / (t2 - t1)) + p2 * ((tt - t1) / (t2 - t1));
            Vector2 a3 = p2 * ((t3 - tt) / (t3 - t2)) + p3 * ((tt - t2) / (t3 - t2));

            Vector2 b1 = a1 * ((t2 - tt) / (t2 - t1)) + a2 * ((tt - t1) / (t2 - t1));

            //The one line this file got wrong the first time (#227): BOTH upper rows of the pyramid are
            //interpolated on the EVALUATED segment's own interval (t1, t2) — b2 is a mix of a2 and a3 at
            //the same fraction b1 mixes a1 and a2, not at a3's own fraction on (t2, t3). Parameterized
            //that way — the way it briefly shipped — the weights go negative for every t below t2, the
            //spline extrapolates outside its control rings instead of interpolating between them, and one
            //oversized ring sweeps into a sail that covers the cup.
            Vector2 b2 = a2 * ((t2 - tt) / (t2 - t1)) + a3 * ((tt - t1) / (t2 - t1));

            return b1 * ((t2 - tt) / (t2 - t1)) + b2 * ((tt - t1) / (t2 - t1));
        }

        /// <summary>
        /// How far the reeding pulls the radius in at height <paramref name="y"/> and angle
        /// <paramref name="angle"/>, as a multiplier (1 = untouched), and its two partial derivatives — the
        /// normals need both, because a groove tilts the surface around the axis AND, where a band fades in,
        /// along it.
        /// </summary>
        private static (float Scale, float DAngle, float DY) Reed(float y, float angle)
        {
            foreach (Reeding band in REEDING)
            {
                if (y <= band.Bottom || y >= band.Top) continue;

                //The window and its slope: a smoothstep up over the first Ramp, down over the last
                float rise = Ease(band.Bottom, band.Bottom + band.Ramp, y, out float riseSlope);
                float fall = 1f - Ease(band.Top - band.Ramp, band.Top, y, out float fallSlope);
                float window = rise * fall;
                float windowSlope = riseSlope * fall - rise * fallSlope;

                (float s, float c) = MathF.SinCos(band.Count * angle);
                float wave = 0.5f + 0.5f * c;
                float waveSlope = -0.5f * band.Count * s;

                return (1f - band.Depth * window * wave,
                    -band.Depth * window * waveSlope,
                    -band.Depth * windowSlope * wave);
            }

            return (1f, 0f, 0f);

            static float Ease(float from, float to, float x, out float slope)
            {
                float u = Math.Clamp((x - from) / (to - from), 0f, 1f);
                slope = (u > 0f && u < 1f) ? 6f * u * (1f - u) / (to - from) : 0f;
                return u * u * (3f - 2f * u);
            }
        }

        /// <summary>
        /// Revolves <see cref="PROFILE"/> about the Y axis. Normals are computed from the profile rather than
        /// from the triangles: for a segment running <c>(dr, dy)</c> in the (radius, y) plane the outward
        /// normal is <c>(dy, -dr)</c> — the tangent turned so its radial part points away from the axis, which
        /// is what makes an underside face down and the inside of the bowl face up and in. Where the body is
        /// reeded, the normal is the cross product of the reeded surface's own two tangents instead.
        /// </summary>
        private static void BuildBody(MeshBuilder builder)
        {
            //The densified section, not the authored one — the authored rings are the shape's meaning, this
            //is the shape itself. Every tier draws it, crystal included (#271): "cut" proved to be a word
            //for edges the owner did not want, whichever layer they were cut into.
            Ring[] profile = DensifyProfile();
            int spans = profile.Length - 1;

            //Per-span normal in the (radial, y) plane, then per-ring normals either side of each ring. They
            //differ only at a crease, which is exactly what a crease is.
            Vector2[] spanNormal = new Vector2[spans];
            for (int s = 0; s < spans; s++)
            {
                float dr = profile[s + 1].Radius - profile[s].Radius;
                float dy = profile[s + 1].Y - profile[s].Y;
                spanNormal[s] = Vector2.Normalize(new Vector2(dy, -dr));
            }

            Vector2[] below = new Vector2[profile.Length];   //the normal a ring shows to the span under it
            Vector2[] above = new Vector2[profile.Length];   //and to the span over it

            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 lower = spanNormal[Math.Max(0, i - 1)];
                Vector2 upper = spanNormal[Math.Min(spans - 1, i)];

                if (profile[i].Crease)
                {
                    below[i] = lower;
                    above[i] = upper;
                }
                else
                {
                    Vector2 mean = lower + upper;
                    Vector2 smooth = mean.LengthSquared() > 1e-8f ? Vector2.Normalize(mean) : upper;
                    below[i] = smooth;
                    above[i] = smooth;
                }
            }

            for (int s = 0; s < spans; s++)
            {
                Ring low = profile[s], high = profile[s + 1];

                //A span with no length in either direction would give a degenerate quad and a zero normal
                if (low.Radius == high.Radius && low.Y == high.Y) continue;

                for (int i = 0; i < SEGMENTS; i++)
                {
                    float a0 = i / (float)SEGMENTS * MathHelper.TwoPi;
                    float a1 = (i + 1) / (float)SEGMENTS * MathHelper.TwoPi;

                    (Vector3 p00, Vector3 n00) = Surface(low, above[s], a0);
                    (Vector3 p10, Vector3 n10) = Surface(low, above[s], a1);
                    (Vector3 p11, Vector3 n11) = Surface(high, below[s + 1], a1);
                    (Vector3 p01, Vector3 n01) = Surface(high, below[s + 1], a0);

                    //The face normal handed to the builder is the span's own, swung to the middle of this
                    //facet: it decides the winding, and it must not be one of the smoothed vertex normals —
                    //at the bowl's floor those tilt far enough to flip a quad the wrong way round.
                    Vector2 mid = spanNormal[s];
                    float ac = MathF.Cos((a0 + a1) * 0.5f), asn = MathF.Sin((a0 + a1) * 0.5f);
                    Vector3 face = new(mid.X * ac, mid.Y, mid.X * asn);

                    //Degenerate at the axis: the first and last rings are single points, so one edge of the
                    //quad collapses and it is a triangle. Adding it as a quad would add a zero-area triangle,
                    //which is harmless but pointless — take the triangle instead.
                    if (low.Radius <= 0f) builder.AddTriangle(p01, p11, p00, n01, n11, n00, face);
                    else if (high.Radius <= 0f) builder.AddTriangle(p00, p10, p01, n00, n10, n01, face);
                    else builder.AddQuad(p00, p10, p11, p01, n00, n10, n11, n01, face);
                }
            }
        }

        /// <summary>
        /// One vertex of the body: the ring at <paramref name="angle"/>, reeded where a band says so. Outside a
        /// band this is the plain revolve — the ring's radius and its profile normal swung round the axis.
        /// Inside one, the position is the reeded radius and the normal is <c>∂P/∂s × ∂P/∂θ</c>, taken with the
        /// profile tangent <c>(−n.y, n.x)</c> so the arc length along the section needs no bookkeeping.
        /// </summary>
        private static (Vector3 Position, Vector3 Normal) Surface(Ring ring, Vector2 normal, float angle)
        {
            (float sin, float cos) = MathF.SinCos(angle);
            Vector3 radial = new(cos, 0f, sin);

            (float scale, float dAngle, float dY) = ring.Radius > 0f ? Reed(ring.Y, angle) : (1f, 0f, 0f);
            float r = ring.Radius * scale;

            Vector3 position = radial * r + Vector3.Up * ring.Y;
            Vector3 plain = new(normal.X * cos, normal.Y, normal.X * sin);

            if (dAngle == 0f && dY == 0f) return (position, plain);

            Vector3 around = new(-sin, 0f, cos);

            //Along the section: the profile tangent, with the reeded radius changing as y does
            float tr = -normal.Y, ty = normal.X;
            Vector3 alongSection = radial * (tr * scale + ring.Radius * dY * ty) + Vector3.Up * ty;

            //Around the axis: the circle's own tangent, plus the radius moving in and out of the flutes
            Vector3 alongRing = radial * (ring.Radius * dAngle) + around * r;

            Vector3 reeded = Vector3.Cross(alongRing, alongSection);
            if (Vector3.Dot(reeded, plain) < 0f) reeded = -reeded;

            return (position, reeded.LengthSquared() > 1e-12f ? Vector3.Normalize(reeded) : plain);
        }

        /// <summary>
        /// The tube's radius at <paramref name="t"/> along the sweep: <see cref="HANDLE_TUBE_RADIUS"/> along
        /// the arc, easing down to <see cref="HANDLE_ROOT_RADIUS"/> over the last
        /// <see cref="HANDLE_ROOT_SPAN"/> at each end — see that constant for why the roots have to be
        /// thinner than the handle. Smoothstepped rather than lerped so the taper meets the full tube with no
        /// crease in it; <see cref="MathHelper.SmoothStep(float, float, float)"/> clamps its own amount, so
        /// the whole middle of the sweep falls out at the full radius.
        /// </summary>
        private static float TubeRadius(float t) =>
            MathHelper.SmoothStep(HANDLE_ROOT_RADIUS, HANDLE_TUBE_RADIUS, MathF.Min(t, 1f - t) / HANDLE_ROOT_SPAN);

        /// <summary>
        /// One handle: a circular tube swept along a cubic Bézier standing in the plane of the cup's axis and
        /// <paramref name="side"/>'s X. The sweep's frame is fixed rather than parallel-transported, because
        /// the path is planar — Z is always perpendicular to it, so there is no twist to accumulate and none
        /// of a Frenet frame's trouble where the curvature flips, which a cubic that turns twice does.
        /// </summary>
        private static void BuildHandle(MeshBuilder builder, float side)
        {
            Vector3[,] ring = new Vector3[HANDLE_STEPS + 1, HANDLE_SIDES];
            Vector3[,] ringNormal = new Vector3[HANDLE_STEPS + 1, HANDLE_SIDES];

            //The two roots' own discs, kept so the caps below can be laid on them
            Vector3 lowCentre = Vector3.Zero, lowFacing = Vector3.Zero;
            Vector3 highCentre = Vector3.Zero, highFacing = Vector3.Zero;

            for (int step = 0; step <= HANDLE_STEPS; step++)
            {
                float t = step / (float)HANDLE_STEPS;

                //The cubic and its own derivative, both in the plane z = 0, then mirrored onto the wanted side.
                //The derivative is exact rather than a difference of neighbouring samples, so the frame is
                //stable at the ends where a difference would have nothing on one side of it.
                (Vector2 path, Vector2 slope) = HandlePath(t);

                Vector3 centre = new(side * path.X, path.Y, 0f);
                Vector3 tangent = Vector3.Normalize(new Vector3(side * slope.X, slope.Y, 0f));

                Vector3 axis1 = Vector3.UnitZ;                              //out of the sweep's plane
                Vector3 axis2 = Vector3.Normalize(Vector3.Cross(tangent, axis1));

                float radius = TubeRadius(t);

                for (int i = 0; i < HANDLE_SIDES; i++)
                {
                    float phi = i / (float)HANDLE_SIDES * MathHelper.TwoPi;
                    (float sp, float cp) = MathF.SinCos(phi);

                    Vector3 normal = axis1 * cp + axis2 * sp;
                    ringNormal[step, i] = normal;
                    ring[step, i] = centre + normal * radius;
                }

                if (step == 0) (lowCentre, lowFacing) = (centre, -tangent);
                else if (step == HANDLE_STEPS) (highCentre, highFacing) = (centre, tangent);
            }

            //The wall
            for (int step = 0; step < HANDLE_STEPS; step++)
                for (int i = 0; i < HANDLE_SIDES; i++)
                {
                    int j = (i + 1) % HANDLE_SIDES;

                    Vector3 face = Vector3.Normalize(
                        ringNormal[step, i] + ringNormal[step, j]
                        + ringNormal[step + 1, i] + ringNormal[step + 1, j]);

                    builder.AddQuad(
                        ring[step, i], ring[step, j], ring[step + 1, j], ring[step + 1, i],
                        ringNormal[step, i], ringNormal[step, j],
                        ringNormal[step + 1, j], ringNormal[step + 1, i],
                        face);
                }

            //And the two roots CLOSED. They were left open while "both ends are buried" was an assumption
            //and a cap there was triangles nobody could see; since #228 it is a measured fact, and the caps
            //go on anyway for a reason the assumption never covered — the DIAMOND cup is crystal, so
            //everything inside the body is on show, and an open end reads through the glass as a cut pipe
            //with the far side of the room visible down its bore. Forty triangles a handle.
            AddRootCap(builder, ring, 0, lowCentre, lowFacing);
            AddRootCap(builder, ring, HANDLE_STEPS, highCentre, highFacing);
        }

        /// <summary>The handle's centreline at <paramref name="t"/> in the (radius, y) plane, and its derivative.</summary>
        private static (Vector2 Path, Vector2 Slope) HandlePath(float t)
        {
            float u = 1f - t;

            Vector2 path = u * u * u * HANDLE_LOW + 3f * u * u * t * HANDLE_OUT
                + 3f * u * t * t * HANDLE_UP + t * t * t * HANDLE_HIGH;
            Vector2 slope = 3f * u * u * (HANDLE_OUT - HANDLE_LOW) + 6f * u * t * (HANDLE_UP - HANDLE_OUT)
                + 3f * t * t * (HANDLE_HIGH - HANDLE_UP);

            return (path, slope);
        }

        /// <summary>
        /// Closes one end of a handle with a triangle fan from the root disc's own centre, facing
        /// <paramref name="facing"/> — the sweep's tangent, reversed at the low end so both caps look out of
        /// the tube rather than along it.
        /// </summary>
        private static void AddRootCap(MeshBuilder builder, Vector3[,] ring, int step, Vector3 centre, Vector3 facing)
        {
            for (int i = 0; i < HANDLE_SIDES; i++)
            {
                int j = (i + 1) % HANDLE_SIDES;

                builder.AddTriangle(centre, ring[step, i], ring[step, j], facing, facing, facing, facing);
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
