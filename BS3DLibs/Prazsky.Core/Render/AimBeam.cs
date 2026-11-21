using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Where the gun is pointing</b> — a dashed line of light out of the muzzle along the aim, crawling
    /// towards its far end. It answers the question the overview camera cannot: that lens looks <i>at</i> the
    /// cluster rather than along the bore, so the screen's centre is not where the gun points and a crosshair
    /// there would name the wrong spot (which is why the game only draws one while precise aim is leaning in).
    /// Without this, aiming from the overview is read off the barrel's foreshortened angle alone.
    /// <para>
    /// <b>The far end carries the meaning.</b> The caller ends the beam at whatever a shot fired now would
    /// reach, so it stops at the ball it would touch, and hands the colour to match: the loaded ball's tint
    /// where the shot sticks, a refusal colour where it will not. Ended in open air instead — the aim reaching
    /// nothing at all — the dashes are faded out along the way (<c>openEnded</c>) so the line dies away rather
    /// than stopping in mid-air as if something were there.
    /// </para>
    /// <para>
    /// <b>And where the shot snaps into the lattice, the line splits into a funnel over the region it lands in</b>
    /// (#1144 asked for a region, #1196 for it drawn as lines). The touch is where the shot meets the ball it hits;
    /// the ghost is drawn in the free cell beside that ball nearest the touch, which can be two thirds of a ball to
    /// either side of the line, so a line that ended on one point next to a ghost sitting somewhere else read as a
    /// promise it did not keep. Handed a <c>spread</c>, the beam is the old dashed line up to
    /// <see cref="SPLIT_FROM"/> of the flight, where it is the aim and must stay exactly as it was, and there it splits
    /// into <see cref="RAYS"/> thin crisp lines: the middle one runs on along the flight to the touch, the others open
    /// out to either side until they stand the spread off it (<see cref="LANDING_SPREAD"/>, measured), and all of them
    /// run on a little past the touch and fade out. Together they outline a funnel over every place the ghost can be,
    /// and the ghost then reads as "the one in here", which is what it is.
    /// </para>
    /// </summary>
    /// <remarks>
    /// It borrows <c>Shaders/ShotTrail.fx</c> from <see cref="LaunchSmears"/> rather than bringing a shader of
    /// its own: that shader is already a camera-facing billboard between two world points. Its first technique
    /// fades each segment in over its first quarter and out over its last, so a <i>short</i> segment comes out as a
    /// dash with soft ends, drawn additively and bright enough to bloom: the dashes. Its second, <c>AimRay</c>, draws the
    /// same billboard as a crisp line, covered whole across but for a pixel's anti-aliasing at its edges, its alpha
    /// running from one end's to the other's, blended over what is behind it: the rays. One piece is one draw of the
    /// shared quad.
    /// <para>
    /// <b>The rays are lines, not light</b>, which is the owner's verdict on the soft glow #1144 drew there: "it works
    /// more like a glow, and it is not obvious what it is". So they are laid over the frame with premultiplied alpha
    /// rather than added to it, in a colour whose luminance is held under the glare pass's threshold
    /// (<see cref="RayColor"/>): a pixel they cover is a blend of the scene and that colour, so it is never brighter than
    /// the brighter of the two and they put no bloom of their own anywhere.
    /// </para>
    /// <para>
    /// <b>The rays are unbroken, where the line before them is dashed</b>, and that is forced by the overview. It sees
    /// the beam ten-odd degrees off end on, so a dash foreshortens to a dot, and dots on neighbouring rays would stand
    /// level with each other across the funnel and read as the rungs of a ladder, as #1144 found widened dashes do.
    /// </para>
    /// <para>
    /// <b>The rays spread square to the lens</b>, which is what keeps them apart on screen from that same overview. At
    /// each point they are cut at they open along the way across the flight that is also across the view ray to it
    /// (<see cref="Across"/>), so the spread shows at its full size from any view; a fan in any fixed plane would fold
    /// onto the line from a lens that looks along that plane. <b>Past the touch they run on square to the lens too</b>
    /// (<see cref="OVERRUN"/>), which is what keeps the ghost inside the funnel from the overview: a ghost standing off
    /// the line towards the lens or away from it shows on screen along the beam, past the touch or short of it, where a
    /// run along the flight would show at a fifth of its length and lie behind the ball hit.
    /// </para>
    /// <para>
    /// <b>The widths are pushed per draw, and they have to be.</b> <see cref="LaunchSmears"/> set them once in
    /// its constructor, correctly, on the reasoning that a compile-time constant need not be re-sent per frame
    /// (<c>BestPractices.md</c> §1 names those two parameters as its own example). That stopped being true the
    /// moment a second component shared the effect: the two want different widths, an <c>Effect</c>'s parameter
    /// values are the effect's and not the caller's, and whichever constructor ran last would otherwise decide
    /// how both look. Here every draw sends its first piece's two, and each later piece's where they differ from the
    /// last (the rays' differ from the dashes').
    /// </para>
    /// <para>
    /// <b>The layout is a pure function</b> (<see cref="Lay"/>), apart from the draw, so what the funnel promises
    /// can be asked of it without a graphics device: <c>AimBeamTests</c> holds the near end to the old line, the rays to
    /// one funnel however the flight is cut into legs, and the funnel on screen round the ghosts the measured aims put
    /// beside the touch, seen from the Game's overview and from lenses all round the beam.
    /// </para>
    /// </remarks>
    public sealed class AimBeam : IDisposable
    {
        #region The dials

        /// <summary>World length of one dash plus the gap after it — the pattern's period along the line.</summary>
        private const float SPACING = 1.1f;

        /// <summary>How much of a period is lit. Under a half so the line reads as dashes rather than as a rod.</summary>
        private const float DASH_FRACTION = 0.42f;

        /// <summary>
        /// Half-width of a dash. A ball's radius is <c>0.5</c>, so this is a line about three-fifths of a ball
        /// across — which sounds fat and is not: the alpha of <c>ShotTrail.fx</c>'s streak falls across it as
        /// <c>(1 - across)²</c> and an additive draw weighs the alpha twice, so the light goes as
        /// <c>(1 - across)⁴</c> and only the core of that is anywhere near full strength.
        /// <para>
        /// <b>Both this and <see cref="BRIGHTNESS"/> were set by looking, and the first guesses were wrong by a
        /// lot.</b> 0.085 and 1.35 were reasoned from the ball's radius and from the smear's boost, and the beam
        /// came out <i>invisible</i> at the overview's stand-off — the mode it exists for. The values here were
        /// bracketed from the other side: 0.45 and 8 are unmistakable and far too heavy, and these sit between.
        /// Anything derived from a ball's size rather than from what a few dozen pixels of an additive line
        /// actually look like over a bright cluster should be expected to be wrong the same way.
        /// </para>
        /// </summary>
        internal const float WIDTH = 0.15f;

        /// <summary>
        /// Radiance boost of the dashes. A little <i>over</i> <see cref="LaunchSmears"/>'s, which is not what one would
        /// guess — a smear is a shot going off and should flare, while this is up the whole time the gun is aimed — but
        /// the smear is briefly enormous where this is permanently thin, and a thin additive line over a lit
        /// cluster needs the radiance to read at all. See <see cref="WIDTH"/> on how both were arrived at.
        /// </summary>
        private const float BRIGHTNESS = 3.5f;

        /// <summary>Lowest peak channel of the dashes, so the near-black ball still gives a visible grey line.</summary>
        private const float COLOR_FLOOR = 0.2f;

        /// <summary>
        /// World units a second the dash pattern crawls towards the far end. Slow: a hint of travel, not a
        /// barber's pole. The pattern is laid along the whole flight from <c>phase * CRAWL</c>, so on a beam bent
        /// by a gravity well (#332) the dashes run on through every knot instead of restarting at each.
        /// </summary>
        private const float CRAWL = 1.6f;

        /// <summary>
        /// A dash shorter than this fraction of a full one is skipped. The pattern is clipped at both ends of
        /// the line, so without this the ends flicker a one-pixel sliver in and out as the crawl moves.
        /// </summary>
        private const float MIN_DASH_FRACTION = 0.25f;

        /// <summary>
        /// Where the fade starts on an open-ended beam, as a fraction of its last leg. Before it the line is
        /// full strength; after it the dashes ease to nothing by the tip.
        /// </summary>
        private const float OPEN_FADE_FROM = 0.35f;

        /// <summary>
        /// A hard cap on dashes per beam, so a caller handing over an absurd length cannot turn one frame into
        /// thousands of draws. At <see cref="SPACING"/> this is ~140 world units: past the longest flight the Game
        /// hands over, a stepped one (a gravity well's, a crate's) that reaches nothing and runs out its flight budget
        /// a hundred units on, so such a beam is laid whole and its open fade on its last leg is never the part cut off.
        /// </summary>
        private const int MAX_DASHES = 128;

        /// <summary>
        /// Below this the beam is not drawn at all — an eased fade that has all but reached zero is a row of
        /// invisible quads and a pile of state changes for nothing. <see cref="Crosshair.MIN_OPACITY"/>'s
        /// reasoning, for the mark this one hands over to.
        /// </summary>
        public const float MIN_OPACITY = 0.01f;

        /// <summary>
        /// <b>How far to the side of the beam's end the ghost can sit</b>, in world units: what the funnel has to
        /// cover so the ghost is always inside it (#1144). <b>Measured, not chosen</b>: over 478 aims at the six
        /// levels <c>ShotAgreementTests</c> sweeps, each asked as the Game asks (the curved sweep with the world's
        /// gravity, then <c>ShotPlacement.TrySolveAgainstBall</c>), the ghost's centre stood off the line through
        /// the touch by 0.45 at the median, 0.61 at the 95th percentile, 0.64 at the 99th and 0.67 at the most
        /// (along the line it stood 0.34 short of the touch at the median, from 0.68 short to 0.38 past).
        /// <c>AimBeamTests</c> re-measures it and holds the funnel round those ghosts on screen.
        /// <para>
        /// It is a little over a ball's width across, which is the region the owner asked for: the shot ends
        /// "somewhere in here", and the lattice decides where. The callers pass it where the shot snaps into a
        /// cell and nothing (a plain line) where it does not.
        /// </para>
        /// </summary>
        public const float LANDING_SPREAD = 0.65f;

        /// <summary>
        /// Where the line splits into the rays, as a fraction of the flight: before it the beam is the dashed line it
        /// always was, from it the rays. Past half way, so the overview, which reads the aim off the near part, keeps
        /// the dashed line over most of it.
        /// </summary>
        internal const float SPLIT_FROM = 0.55f;

        /// <summary>
        /// How many lines the beam splits into: the middle one along the flight, the aim itself, and two to each side,
        /// half and all the way out. Odd, so there is a middle one; five, the most the owner's "several lines" was taken
        /// to allow, so the outer pair draw the funnel's edges and the inner pair stand between them and the aim.
        /// </summary>
        internal const int RAYS = 5;

        /// <summary>
        /// Half-width of a ray, in world units: from the overview, which stands some twenty units off the split and
        /// twenty-five off the touch, a line about five pixels across where it splits and four at the touch at a 1080-line
        /// frame (the outermost of them anti-aliased). Thin beside a dash, whose soft profile shows a band about twice as
        /// wide (<see cref="WIDTH"/>), because five of them share a ball's width and must read as five lines: neighbouring
        /// rays stand 12 to 17 pixels apart at the touch on that frame.
        /// </summary>
        internal const float RAY_WIDTH = 0.035f;

        /// <summary>
        /// How far short of the touch the funnel is fully open, in world units: as far as a ghost stands short of it
        /// (0.68 at the most, <see cref="LANDING_SPREAD"/>), so the whole landing region lies inside the outer rays and a
        /// ghost at its near edge is not left outside a funnel still opening. From there on the rays run parallel.
        /// </summary>
        internal const float REGION_BEHIND = 0.7f;

        /// <summary>
        /// How far past the touch the rays run before they have faded out, in world units, <b>square to the lens</b>
        /// (<see cref="FacingLens"/>): in the direction the flight carries on in on screen, with none of it into the
        /// screen. A ghost standing off the line towards the lens or away from it, up to <see cref="LANDING_SPREAD"/>,
        /// shows on screen beside the touch along the beam's screen direction; square to the lens the run shows at its
        /// full length from any view, in front of the ball hit, and side on it is the flight's own direction, which
        /// covers a ghost standing past the touch (up to 0.38).
        /// </summary>
        private const float OVERRUN = 1.2f;

        /// <summary>
        /// The rays' alpha where they split off the line. Under one, so each is a line laid over the scene with a little
        /// of it showing through: the lines "become transparent", in the owner's word, where the dashes were solid light.
        /// </summary>
        internal const float RAY_ALPHA = 0.9f;

        /// <summary>
        /// The share of <see cref="RAY_ALPHA"/> the rays keep at the touch: they fade along the flight to it and then out
        /// to nothing over <see cref="OVERRUN"/>, so the funnel is clearest where it opens and dies away past the region.
        /// </summary>
        private const float RAY_ALPHA_AT_TOUCH = 0.6f;

        /// <summary>The rays' colour on its brightest channel, in linear radiance: the tint's own hue at full strength.</summary>
        private const float RAY_PEAK = 1f;

        /// <summary>
        /// The most luminance the rays' colour may have, in linear radiance: under the glare pass's threshold (the
        /// executables' <c>GLARE_THRESHOLD</c>, 0.55), so a ray never blooms. A white or yellow tint is dimmed to it; a
        /// red or blue one at <see cref="RAY_PEAK"/> is under it already.
        /// </summary>
        internal const float RAY_LUMINANCE = 0.45f;

        /// <summary>
        /// How far the flight may stray from a ray's chord, in world units, before the rays are cut at a knot of it. The
        /// Game's flight is stepped at the simulation's step (a knot every unit and a half or so) and falls a few
        /// hundredths over it, which no chord shows; a gravity well's bend is cut as finely as it needs.
        /// </summary>
        private const float RAY_CHORD_TOLERANCE = 0.01f;

        /// <summary>
        /// The most points the rays can be cut at along the flight: the split, where the funnel is fully open, the touch
        /// and the knots between where the flight bends past <see cref="RAY_CHORD_TOLERANCE"/>. Past it a bend is
        /// spanned by a coarser chord rather than drawn with more pieces.
        /// </summary>
        private const int MAX_STATIONS = 64;

        /// <summary>
        /// Pieces a beam can be laid as: a dash a knot of a bent flight cuts is two, and the rare one that two knots
        /// cut is three; each ray is a piece between neighbouring stations and one past the touch.
        /// </summary>
        internal const int MAX_PIECES = 3 * MAX_DASHES + RAYS * MAX_STATIONS;

        #endregion

        /// <summary>Which of the shader's two techniques a piece is drawn with, and so how it is blended.</summary>
        internal enum PieceKind : byte
        {
            /// <summary>The soft streak, added to the frame: the dashed line.</summary>
            Dash,

            /// <summary>The crisp line, blended over the frame: the rays the far end splits into.</summary>
            Ray
        }

        /// <summary>
        /// One quad of a laid beam: its two ends, the half-width and the alpha at each before the caller's opacity (a
        /// dash's two alphas are one), which technique draws it, and for a ray which of them it is: its lane, from
        /// <c>-(RAYS - 1) / 2</c> on one side through 0 along the flight to <c>(RAYS - 1) / 2</c> on the other.
        /// </summary>
        internal readonly record struct Piece(Vector3 Tail, Vector3 Head, float TailWidth, float HeadWidth, float TailAlpha,
            float HeadAlpha, PieceKind Kind, int Lane);

        private readonly GraphicsDevice _device;

        //Cached at construction, per BestPractices.md §1: the by-name indexer is a string scan of the whole
        //parameter collection, and the first four go out once a frame while the rest go out PER PIECE.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _headParam, _tailParam, _colorParam, _alphaParam;
        private readonly EffectParameter _headWidthParam, _tailWidthParam;
        private readonly EffectParameter _rayTailAlphaParam, _rayHeadAlphaParam;

        //The dashes' pass and the rays': by name, since LaunchSmears and LineSparks draw the effect's first technique
        //through the pass they cached, and nothing here changes which technique is current
        private readonly EffectPass _dashPass, _rayPass;

        //The frame's layout, reused: laid into, drawn from, never reallocated
        private readonly Piece[] _pieces = new Piece[MAX_PIECES];

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        /// <param name="shotTrailEffect">The compiled <c>Shaders/ShotTrail.fx</c>, the same instance
        /// <see cref="LaunchSmears"/> is given, with its <c>AimRay</c> technique (the Potato path's port has one too).
        /// Handed in and never disposed here — the caller's content manager owns its lifetime.</param>
        public AimBeam(GraphicsDevice device, Effect shotTrailEffect)
        {
            _device = device;

            _viewParam = shotTrailEffect.Parameters["View"];
            _projectionParam = shotTrailEffect.Parameters["Projection"];
            _cameraPositionParam = shotTrailEffect.Parameters["CameraPosition"];
            _headParam = shotTrailEffect.Parameters["TrailHead"];
            _tailParam = shotTrailEffect.Parameters["TrailTail"];
            _colorParam = shotTrailEffect.Parameters["TrailColor"];
            _alphaParam = shotTrailEffect.Parameters["TrailAlpha"];
            _headWidthParam = shotTrailEffect.Parameters["TrailHeadWidth"];
            _tailWidthParam = shotTrailEffect.Parameters["TrailTailWidth"];
            _rayTailAlphaParam = shotTrailEffect.Parameters["RayTailAlpha"];
            _rayHeadAlphaParam = shotTrailEffect.Parameters["RayHeadAlpha"];

            _dashPass = shotTrailEffect.Techniques["ShotTrail"].Passes[0];
            _rayPass = shotTrailEffect.Techniques["AimRay"].Passes[0];

            CreateQuad();
        }

        /// <summary>
        /// Draws the beam along <paramref name="path"/>, from its first point (the muzzle) to its last.
        /// </summary>
        /// <param name="camera">The frame's camera; the pieces turn to face it, and the rays spread and run past the touch
        /// square to it (<see cref="Across"/>, <see cref="OVERRUN"/>).</param>
        /// <param name="path">The flight, the muzzle first and the far end last: two points for a straight shot, the
        /// knots of the curve for one bent by a gravity well or banked off a crate. The dashes, the rays and the
        /// open fade are laid along the whole of it, so the knots do not show.</param>
        /// <param name="srgbTint">The line's colour as authored (sRGB). Decoded to linear here; for the dashes its hue
        /// kept but its peak lifted to <see cref="COLOR_FLOOR"/> and then boosted, which is the same rule
        /// <see cref="LaunchSmears"/> applies to a smear and for the same reason, and for the rays
        /// <see cref="RayColor"/>.</param>
        /// <param name="phase">A seconds clock. Only its fractional position within a period matters, so the
        /// wall clock serves — and being the wall clock, the crawl keeps going while a pause holds the session.</param>
        /// <param name="openEnded">True when the far end is open air rather than something the shot would hit, which
        /// fades the dashes out along the last leg instead of ending them at a point. It splits into no rays.</param>
        /// <param name="spread">How far to the side of the far end the shot may come to rest: <see cref="LANDING_SPREAD"/>
        /// where it snaps into a lattice cell beside the ball it touches, splitting the beam into a funnel of rays over
        /// that region; zero for a plain line, as a cutter, which strikes the very ball the beam ends on, wants (and so is
        /// anything up to <see cref="WIDTH"/>, a funnel no wider than the line).</param>
        /// <param name="opacity">Scales the whole beam. The Game hands over the inverse of its precise-aim blend:
        /// leaning in over the barrel the lens looks <i>along</i> the bore, so the beam foreshortens into a stack of
        /// dashes piled over the very cell it is pointing at — and it has nothing to add there, because a crosshair
        /// on a lens aimed down the shot ray already <b>is</b> the trajectory. Each mode keeps the one signal that
        /// suits it. At or below <see cref="MIN_OPACITY"/> nothing is drawn and no state is touched.</param>
        public void Draw(ICamera camera, ReadOnlySpan<Vector3> path, Vector3 srgbTint, float phase, bool openEnded,
            float spread, float opacity = 1f)
        {
            if (opacity <= MIN_OPACITY) return;

            int count = Lay(path, phase, openEnded, spread, camera.Position, _pieces);
            if (count == 0) return;

            Vector3 linear = ColorSpace.SrgbToLinear(srgbTint);
            Vector3 rayColor = RayColor(linear);

            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            if (peak < COLOR_FLOOR) linear *= COLOR_FLOOR / MathF.Max(peak, Constants.THOUSANDTH);
            Vector3 dashColor = linear * BRIGHTNESS;

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            //The widths last sent. NaN equals nothing, so every Draw sends its first piece's
            float tailWidth = float.NaN, headWidth = float.NaN;

            //The technique the pieces are on: the dashes are laid first and the rays after them, so it changes once
            EffectPass pass = null;
            PieceKind kind = PieceKind.Dash;

            for (int i = 0; i < count; i++)
            {
                ref readonly Piece piece = ref _pieces[i];

                if (pass == null || piece.Kind != kind)
                {
                    kind = piece.Kind;
                    bool ray = kind == PieceKind.Ray;

                    //A dash is light added to the frame, a ray a line laid over it (the remarks)
                    _device.BlendState = ray ? BlendState.AlphaBlend : BlendState.Additive;
                    _colorParam.SetValue(ray ? rayColor : dashColor);
                    pass = ray ? _rayPass : _dashPass;
                }

                _tailParam.SetValue(piece.Tail);
                _headParam.SetValue(piece.Head);

                //Per draw and not per construction -- see the remarks: the effect is shared with LaunchSmears and
                //LineSparks, which want widths of their own. Within this loop nothing else draws through it, so a
                //width is sent only when it changes: once for the dashes and once for the rays
                if (piece.TailWidth != tailWidth) _tailWidthParam.SetValue(tailWidth = piece.TailWidth);
                if (piece.HeadWidth != headWidth) _headWidthParam.SetValue(headWidth = piece.HeadWidth);

                if (kind == PieceKind.Ray)
                {
                    _rayTailAlphaParam.SetValue(piece.TailAlpha * opacity);
                    _rayHeadAlphaParam.SetValue(piece.HeadAlpha * opacity);
                }
                else
                {
                    _alphaParam.SetValue(piece.TailAlpha * opacity);
                }

                pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// The rays' colour for a <paramref name="linear"/> tint: its hue at <see cref="RAY_PEAK"/> on its brightest
        /// channel (grey for black, which has none), dimmed to <see cref="RAY_LUMINANCE"/> where it would read brighter.
        /// Under the glare threshold for every tint, so a ray blooms nowhere.
        /// </summary>
        internal static Vector3 RayColor(Vector3 linear)
        {
            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            Vector3 color = peak > Constants.THOUSANDTH ? linear * (RAY_PEAK / peak) : new Vector3(RAY_PEAK);

            float luminance = ColorSpace.Luminance(color);
            return luminance > RAY_LUMINANCE ? color * (RAY_LUMINANCE / luminance) : color;
        }

        /// <summary>
        /// Lays the beam out along <paramref name="path"/> as the pieces <see cref="Draw"/> draws, into
        /// <paramref name="pieces"/> (<see cref="MAX_PIECES"/> is always enough), and says how many: the dashes first,
        /// then the rays lane by lane, each lane's pieces from the split outwards. Pure: the same arguments give the same
        /// pieces, and nothing is allocated.
        /// </summary>
        /// <remarks>
        /// <b>Everything is measured along the whole flight</b>, from the muzzle: the dash pattern, so it crawls on
        /// through a knot; and the funnel, so a beam bent by a well splits once and opens once over its whole length
        /// rather than once per leg. A dash that a knot cuts is two pieces, which make one continuous slice of the line;
        /// the rays are cut only where the flight bends off their chord (<see cref="RAY_CHORD_TOLERANCE"/>).
        /// <para>
        /// <b>The flight's far direction is its last leg with a length</b> (<see cref="LastLeg"/>): a stepped flight can
        /// put a knot within a thousandth of its end, and a last leg of nothing has no direction for the rays to run on
        /// past the touch in, nor a length for the open fade to run over.
        /// </para>
        /// </remarks>
        /// <param name="viewer">Where the camera is: the rays spread and run past the touch square to its view.</param>
        internal static int Lay(ReadOnlySpan<Vector3> path, float phase, bool openEnded, float spread, Vector3 viewer,
            Span<Piece> pieces)
        {
            if (path.Length < 2 || pieces.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i + 1 < path.Length; i++) total += Vector3.Distance(path[i], path[i + 1]);

            if (total < SPACING * MIN_DASH_FRACTION) return 0;

            //A funnel no wider than the line is the line
            bool fan = !openEnded && spread > WIDTH;
            float split = fan ? SPLIT_FROM * total : total;

            int lastLeg = LastLeg(path);

            int count = LayDashes(path, split, lastLeg, phase, openEnded, pieces);
            if (fan) count = LayRays(path, total, split, lastLeg, spread, viewer, pieces, count);

            return count;
        }

        /// <summary>The last leg of <paramref name="path"/> longer than a thousandth, or the first leg when none is.</summary>
        private static int LastLeg(ReadOnlySpan<Vector3> path)
        {
            for (int leg = path.Length - 2; leg > 0; leg--)
                if (Vector3.Distance(path[leg], path[leg + 1]) > Constants.THOUSANDTH) return leg;

            return 0;
        }

        /// <summary>
        /// <paramref name="direction"/> turned square to the view ray <paramref name="view"/>: its part across the
        /// view, made unit, which is the way it runs on screen at its full length. Unchanged when it has no such part
        /// (a lens looking exactly along it) or there is no view to speak of.
        /// </summary>
        internal static Vector3 FacingLens(Vector3 direction, Vector3 view)
        {
            float viewLength = view.Length();
            if (viewLength <= Constants.THOUSANDTH) return direction;

            Vector3 along = view / viewLength;
            Vector3 across = direction - along * Vector3.Dot(direction, along);
            float acrossLength = across.Length();

            return acrossLength > Constants.THOUSANDTH ? across / acrossLength : direction;
        }

        /// <summary>
        /// The way across <paramref name="direction"/> that is also across the view ray <paramref name="view"/>, made
        /// unit: the one a spread shows at its full size along on screen, square to the line's own screen direction.
        /// Any way across the direction when the lens looks exactly along it (nothing is seen of a spread then anyway).
        /// </summary>
        internal static Vector3 Across(Vector3 direction, Vector3 view)
        {
            Vector3 across = Vector3.Cross(direction, view);
            float length = across.Length();
            if (length > Constants.THOUSANDTH * MathF.Max(direction.Length() * view.Length(), Constants.THOUSANDTH))
                return across / length;

            across = Vector3.Cross(direction, MathF.Abs(direction.Y) < 0.9f * direction.Length() ? Vector3.UnitY : Vector3.UnitX);
            length = across.Length();
            return length > 0f ? across / length : Vector3.UnitX;
        }

        /// <summary>
        /// The dashed line from the muzzle to <paramref name="end"/>, a distance along the flight (the touch, or where the
        /// line splits): the crawl along the whole flight, and the open fade over the last leg of a beam that reaches
        /// nothing. Exactly the line the beam always was, up to where it ends.
        /// </summary>
        private static int LayDashes(ReadOnlySpan<Vector3> path, float end, int finalLeg, float phase, bool openEnded,
            Span<Piece> pieces)
        {
            //The open fade runs over the last leg, as it did when each leg was drawn on its own: a bank shot that
            //reaches nothing keeps its first leg whole and dies away after the bounce
            float lastLegStart = 0f;
            for (int i = 0; i < finalLeg; i++) lastLegStart += Vector3.Distance(path[i], path[i + 1]);
            float lastLeg = Vector3.Distance(path[finalLeg], path[finalLeg + 1]);

            float dashLength = SPACING * DASH_FRACTION;
            float minDash = SPACING * MIN_DASH_FRACTION;
            int count = 0, dashes = 0;

            //The leg the walk is on, and how far along the flight it starts. Dashes only move forward, so this
            //only ever advances
            int leg = 0;
            float legStart = 0f, legLength = Vector3.Distance(path[0], path[1]);

            //The pattern's origin walks towards the target and wraps every period, so the dashes appear to
            //travel without any one of them being tracked. Starting one period behind the muzzle is what lets a
            //dash be clipped INTO existence at the near end rather than popping in whole.
            float origin = (phase * CRAWL) % SPACING - SPACING;

            for (float start = origin; start < end && dashes < MAX_DASHES; start += SPACING)
            {
                float from = MathF.Max(start, 0f);
                float to = MathF.Min(start + dashLength, end);
                if (to - from < minDash) continue;

                dashes++;

                float alpha = 1f;

                if (openEnded)
                {
                    //Eased over the tail of the line rather than cut, so the beam thins out into the air
                    float t = lastLeg > Constants.THOUSANDTH
                        ? ((to - lastLegStart) / lastLeg - OPEN_FADE_FROM) / (1f - OPEN_FADE_FROM)
                        : 1f;
                    if (t > 0f) alpha = 1f - t * t;
                    if (alpha <= 0f) continue;
                }

                while (leg + 2 < path.Length && legStart + legLength <= from)
                {
                    legStart += legLength;
                    leg++;
                    legLength = Vector3.Distance(path[leg], path[leg + 1]);
                }

                //The dash, cut at every knot it crosses
                int at = leg;
                float atStart = legStart, atLength = legLength, a = from;

                while (count < pieces.Length)
                {
                    float b = MathF.Min(to, atStart + atLength);

                    if (b - a > Constants.THOUSANDTH)
                    {
                        pieces[count++] = new Piece(PointOnLeg(path, at, atStart, atLength, a),
                            PointOnLeg(path, at, atStart, atLength, b), WIDTH, WIDTH, alpha, alpha, PieceKind.Dash, 0);
                    }

                    if (b >= to || at + 2 >= path.Length) break;

                    a = b;
                    atStart += atLength;
                    at++;
                    atLength = Vector3.Distance(path[at], path[at + 1]);
                }

                if (count == pieces.Length) break;
            }

            return count;
        }

        /// <summary>
        /// The rays: <see cref="RAYS"/> lines from <paramref name="split"/>, a distance along the flight, through the
        /// touch and <see cref="OVERRUN"/> past it. The middle one runs along the flight; the others open straight out
        /// across it, square to the lens, until they stand <paramref name="spread"/> (all the way out) or half of it off
        /// it <see cref="REGION_BEHIND"/> short of the touch, and run parallel from there. Each fades from
        /// <see cref="RAY_ALPHA"/> at the split to <see cref="RAY_ALPHA_AT_TOUCH"/> of it at the touch, and to nothing at
        /// its far end. Static: the dashes crawl, the funnel stays.
        /// </summary>
        private static int LayRays(ReadOnlySpan<Vector3> path, float total, float split, int finalLeg, float spread,
            Vector3 viewer, Span<Piece> pieces, int count)
        {
            //Where the funnel is fully open: the near edge of the landing region, or half way from the split on a flight
            //too short for that
            float open = MathF.Max(total - REGION_BEHIND, split + (total - split) * Constants.HALF);

            Span<float> stations = stackalloc float[MAX_STATIONS];
            int n = Stations(path, total, split, open, stations);

            //Past the touch the rays run on square to the lens (OVERRUN): the far leg's direction with none of it into the screen
            Vector3 touch = path[^1];
            Vector3 far = Vector3.Normalize(path[finalLeg + 1] - path[finalLeg]);
            Vector3 overrun = FacingLens(far, touch - viewer);

            //Each station's point on the flight, and the way across it the rays spread there: across the flight's
            //direction at the station (between its neighbours; at the touch the far leg's, so the spread there is square
            //to the run past it) and across the view ray to it
            Span<Vector3> points = stackalloc Vector3[MAX_STATIONS];
            Span<Vector3> sides = stackalloc Vector3[MAX_STATIONS];
            for (int i = 0; i < n; i++) points[i] = PointAt(path, stations[i]);
            for (int i = 0; i < n; i++)
            {
                Vector3 along = i == n - 1 ? far : points[i + 1] - points[Math.Max(i - 1, 0)];
                sides[i] = Across(along, points[i] - viewer);
            }

            int half = (RAYS - 1) / 2;

            for (int lane = -half; lane <= half && count < pieces.Length; lane++)
            {
                float share = spread * lane / half;

                //From the split, where every ray leaves the line
                Vector3 from = points[0];
                float fromAlpha = RAY_ALPHA;

                for (int i = 1; i < n && count < pieces.Length; i++)
                {
                    Vector3 to = points[i] + sides[i] * (share * Opening(stations[i], split, open));
                    float toAlpha = RAY_ALPHA * MathHelper.Lerp(1f, RAY_ALPHA_AT_TOUCH, (stations[i] - split) / (total - split));

                    pieces[count++] = new Piece(from, to, RAY_WIDTH, RAY_WIDTH, fromAlpha, toAlpha, PieceKind.Ray, lane);

                    from = to;
                    fromAlpha = toAlpha;
                }

                //And on past the touch, square to the lens, still fully open, fading out
                if (count < pieces.Length)
                    pieces[count++] = new Piece(from, touch + overrun * OVERRUN + sides[n - 1] * share, RAY_WIDTH, RAY_WIDTH,
                        fromAlpha, 0f, PieceKind.Ray, lane);
            }

            return count;
        }

        //How far the funnel has opened at a distance along the flight: nothing at the split, all the way at `open`
        private static float Opening(float distance, float split, float open) =>
            Math.Clamp((distance - split) / (open - split), 0f, 1f);

        /// <summary>
        /// The distances along the flight the rays are cut at, into <paramref name="stations"/>, nearest first, and how
        /// many: the split, where the funnel is fully open (<paramref name="open"/>), the touch, and between them each knot
        /// past which the flight would stray further than <see cref="RAY_CHORD_TOLERANCE"/> off a single chord. A straight
        /// flight, however many legs it is cut into, is the first three.
        /// </summary>
        private static int Stations(ReadOnlySpan<Vector3> path, float total, float split, float open, Span<float> stations)
        {
            int n = 0;
            stations[n++] = split;

            //The chord being grown runs from the last station, `anchor`, to the furthest knot it is known to hold to,
            //`held`, which becomes a station only once the chord to the next knot breaks
            float anchor = split, held = float.NaN;
            bool opened = false;

            float knot = 0f;
            for (int i = 1; i + 1 < path.Length; i++)
            {
                knot += Vector3.Distance(path[i - 1], path[i]);
                if (knot <= split + Constants.THOUSANDTH) continue;
                if (knot >= total - Constants.THOUSANDTH) break;

                //Room kept for what the rest of this pass and the two forced stations after it can add, two each
                if (n + 7 > stations.Length) break;

                if (!opened && open <= knot + Constants.THOUSANDTH)
                {
                    Grow(path, stations, ref n, ref anchor, ref held, open, forced: true);
                    opened = true;
                }

                if (knot > open + Constants.THOUSANDTH || knot < open - Constants.THOUSANDTH)
                    Grow(path, stations, ref n, ref anchor, ref held, knot, forced: false);
            }

            if (!opened) Grow(path, stations, ref n, ref anchor, ref held, open, forced: true);
            Grow(path, stations, ref n, ref anchor, ref held, total, forced: true);

            return n;
        }

        //One more candidate for the chord being grown: where the chord from the anchor to it no longer holds, the last
        //knot it did hold to becomes a station; a forced candidate is a station whatever the chord does
        private static void Grow(ReadOnlySpan<Vector3> path, Span<float> stations, ref int n, ref float anchor, ref float held,
            float candidate, bool forced)
        {
            if (!float.IsNaN(held) && !ChordHolds(path, anchor, candidate))
            {
                stations[n++] = held;
                anchor = held;
            }

            if (forced)
            {
                stations[n++] = candidate;
                anchor = candidate;
                held = float.NaN;
            }
            else
            {
                held = candidate;
            }
        }

        //Whether every knot of the flight strictly between two distances along it lies within RAY_CHORD_TOLERANCE of the
        //chord between them
        private static bool ChordHolds(ReadOnlySpan<Vector3> path, float from, float to)
        {
            Vector3 a = PointAt(path, from), b = PointAt(path, to);
            Vector3 chord = b - a;
            float lengthSquared = chord.LengthSquared();

            float knot = 0f;
            for (int i = 1; i + 1 < path.Length; i++)
            {
                knot += Vector3.Distance(path[i - 1], path[i]);
                if (knot <= from) continue;
                if (knot >= to) break;

                float t = lengthSquared > 0f ? Math.Clamp(Vector3.Dot(path[i] - a, chord) / lengthSquared, 0f, 1f) : 0f;
                if (Vector3.DistanceSquared(path[i], a + chord * t) > RAY_CHORD_TOLERANCE * RAY_CHORD_TOLERANCE) return false;
            }

            return true;
        }

        //A point at a distance along the flight, on the leg that covers it; a leg of nothing is its one point
        private static Vector3 PointOnLeg(ReadOnlySpan<Vector3> path, int leg, float legStart, float legLength, float distance) =>
            legLength > Constants.THOUSANDTH
                ? Vector3.Lerp(path[leg], path[leg + 1], (distance - legStart) / legLength)
                : path[leg];

        //A point at a distance along the flight, the leg found by walking from the muzzle (the rays ask for a few dozen
        //over a flight of a few dozen knots at the most, so the walk is cheaper than any cursor kept for it). Before the
        //muzzle it is the muzzle, past the far end the far end
        private static Vector3 PointAt(ReadOnlySpan<Vector3> path, float distance)
        {
            if (distance <= 0f) return path[0];

            float legStart = 0f;
            for (int leg = 0; leg + 1 < path.Length; leg++)
            {
                float legLength = Vector3.Distance(path[leg], path[leg + 1]);
                if (distance <= legStart + legLength) return PointOnLeg(path, leg, legStart, legLength, distance);

                legStart += legLength;
            }

            return path[^1];
        }

        /// <summary>
        /// The billboard, exactly <see cref="LaunchSmears"/>'s: a unit quad whose texture channel carries
        /// (side in {-1,1}, along in {0 tail, 1 head}), placed in world space by the shader from the two ends.
        /// The vertex positions are unused, so this one quad serves every piece of every frame.
        /// </summary>
        private void CreateQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(Vector3.Zero, new Vector2(-1f, 0f)), //tail, left
                new(Vector3.Zero, new Vector2(1f, 0f)),  //tail, right
                new(Vector3.Zero, new Vector2(-1f, 1f)), //head, left
                new(Vector3.Zero, new Vector2(1f, 1f))   //head, right
            };

            _vertexBuffer = new VertexBuffer(_device, VertexPositionTexture.VertexDeclaration, corners.Length,
                BufferUsage.WriteOnly);
            _vertexBuffer.SetData(corners);

            short[] indices = { 0, 1, 2, 2, 1, 3 };

            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        /// <summary>
        /// The quad's two buffers, which are everything this component made — <b>not</b> the effect, which the
        /// caller's content manager owns.
        /// </summary>
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;
        }
    }
}
