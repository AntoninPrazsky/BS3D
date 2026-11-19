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
    /// <b>And where the shot snaps into the lattice, the far end is a region and not a point</b> (#1144, the
    /// owner's ask). The touch is where the shot meets the ball it hits; the ghost is drawn in the free cell
    /// beside that ball nearest the touch, which can be two thirds of a ball to either side of the line, so a
    /// line that ended on one point next to a ghost sitting somewhere else read as a promise it did not keep.
    /// Handed a <c>spread</c>, the beam opens instead: the dashes are the old line at the muzzle, where they are
    /// the aim and must stay crisp, and fade out towards the far end, while a soft glow opens under them into a
    /// cone and ends in a patch over the touch that covers every place the ghost can be
    /// (<see cref="LANDING_SPREAD"/>, measured). The ghost then reads as "the one in here", which is what it is.
    /// </para>
    /// </summary>
    /// <remarks>
    /// It borrows <c>Shaders/ShotTrail.fx</c> from <see cref="LaunchSmears"/> rather than bringing a shader of
    /// its own: that shader is already a camera-facing billboard between two world points, it fades each
    /// segment in over its first quarter and out over its last, so a <i>short</i> segment comes out as a dash
    /// with soft ends, and it lerps its half-width from the tail to the head, so a piece cut from a cone is a
    /// slice of that cone. One piece is one draw of the shared quad.
    /// <para>
    /// <b>The glow is laid as overlapping chords, not as one quad per leg</b>, and that is forced by the same
    /// fades. A piece fades in and out over its quarter ends, so pieces laid end to end leave a dark notch at every
    /// join, and a short wide piece (a dash, widened) is seen nearly end on from the overview, which foreshortens it
    /// into a bar across the beam: a cone of widened dashes reads as the rungs of a ladder. Chords
    /// <see cref="GLOW_CHUNK"/> long, a third of one apart, add up to a light that holds within ±3 % along their run
    /// (<see cref="GLOW_OVERLAP"/>), follow a bent flight chord by chord, and ease out by themselves where they stop,
    /// past the touch.
    /// </para>
    /// <para>
    /// <b>Past the touch the glow is laid square to the lens, not along the flight</b> (<see cref="OVERRUN"/>), which
    /// is what keeps the ghost inside the patch from the overview. Each piece turns about its own axis to face the
    /// camera, so the patch is a flat ribbon, and a ghost standing off the line towards the lens or away from it
    /// shows on screen along the beam's own screen direction, past the touch. Along the flight that run is
    /// foreshortened to a sliver from a lens that sees the beam nearly end on, and hidden behind the ball hit; square
    /// to the lens it shows at its full length from every view, and side on it is the flight's own direction.
    /// </para>
    /// <para>
    /// <b>The widths are pushed per draw, and they have to be.</b> <see cref="LaunchSmears"/> set them once in
    /// its constructor, correctly, on the reasoning that a compile-time constant need not be re-sent per frame
    /// (<c>BestPractices.md</c> §1 names those two parameters as its own example). That stopped being true the
    /// moment a second component shared the effect: the two want different widths, an <c>Effect</c>'s parameter
    /// values are the effect's and not the caller's, and whichever constructor ran last would otherwise decide
    /// how both look. Here every draw sends its first piece's two, and each later piece's where they differ from the
    /// last, since every piece of a cone has its own.
    /// </para>
    /// <para>
    /// <b>The layout is a pure function</b> (<see cref="Lay"/>), apart from the draw, so what the cone promises
    /// can be asked of it without a graphics device: <c>AimBeamTests</c> holds the near end to the old line, the
    /// cone to one width at one distance however the flight is cut into legs, and the patch on screen over the
    /// ghosts the measured aims put beside the touch, seen from the Game's overview and from lenses all round the beam.
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
        /// Half-width of a dash at the muzzle — of every dash, on a beam that ends in open air or on a cutter's
        /// ball. A ball's radius is <c>0.5</c>, so this is a line about three-fifths of a ball across — which
        /// sounds fat and is not: the light an additive draw of <c>ShotTrail.fx</c> adds falls as
        /// <c>(1 - across)⁴</c> (see <see cref="TENTH_LIGHT_ACROSS"/>), so only the core of that is anywhere near
        /// full strength and the edges fall away.
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
        /// Radiance boost. A little <i>over</i> <see cref="LaunchSmears"/>'s, which is not what one would guess
        /// — a smear is a shot going off and should flare, while this is up the whole time the gun is aimed — but
        /// the smear is briefly enormous where this is permanently thin, and a thin additive line over a lit
        /// cluster needs the radiance to read at all. See <see cref="WIDTH"/> on how both were arrived at.
        /// </summary>
        private const float BRIGHTNESS = 3.5f;

        /// <summary>Lowest peak channel, so the near-black ball still gives a visible grey line.</summary>
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
        /// <b>How far to the side of the beam's end the ghost can sit</b>, in world units: what the far end has to
        /// cover so the ghost is always inside it (#1144). <b>Measured, not chosen</b>: over 478 aims at the six
        /// levels <c>ShotAgreementTests</c> sweeps, each asked as the Game asks (the curved sweep with the world's
        /// gravity, then <c>ShotPlacement.TrySolveAgainstBall</c>), the ghost's centre stood off the line through
        /// the touch by 0.45 at the median, 0.61 at the 95th percentile, 0.64 at the 99th and 0.67 at the most
        /// (along the line it stood 0.34 short of the touch at the median, from 0.68 short to 0.38 past).
        /// <c>AimBeamTests</c> re-measures it and holds the patch over those ghosts on screen.
        /// <para>
        /// It is a little over a ball's width across, which is the region the owner asked for: the shot ends
        /// "somewhere in here", and the lattice decides where. The callers pass it where the shot snaps into a
        /// cell and nothing (a plain line) where it does not.
        /// </para>
        /// </summary>
        public const float LANDING_SPREAD = 0.65f;

        /// <summary>
        /// The fraction of a piece's half-width at which its light has fallen to a tenth of its core. The pixel
        /// shader's alpha goes across a piece as <c>(1 - |x|)²</c>, and an additive draw adds the colour times the
        /// alpha <b>twice</b> (<c>BlendState.Additive</c> is <c>SourceAlpha</c>/<c>One</c> over a colour already
        /// weighted by the alpha; GamePi's <c>ShotTrail.fx</c> says so at its return and curves that very
        /// product), so the light goes as <c>(1 - |x|)⁴</c> and is a tenth of the core at
        /// <c>1 - 0.1^¼</c>. A piece is therefore visible over well under half of its half-width, which is why
        /// <see cref="FarWidth"/> is the spread divided by this and not the spread itself.
        /// </summary>
        internal const float TENTH_LIGHT_ACROSS = 0.438f;

        /// <summary>
        /// How the cone opens along the flight: the glow's half-width grows as the distance travelled (a fraction of
        /// the whole) to this power, so it is still narrow where it begins and does most of its opening over the
        /// last third.
        /// </summary>
        private const float CONE_POWER = 2f;

        /// <summary>
        /// Where the hand-over begins, as a fraction of the flight: before it the beam is the dashed line it always
        /// was, and from it to the touch the dashes fade out while the glow rises under them. Past half way, so the
        /// overview, which reads the aim off the near part, keeps a crisp line over most of it.
        /// </summary>
        private const float GLOW_FROM = 0.55f;

        /// <summary>
        /// How wide a dash grows by the touch, against <see cref="WIDTH"/>: a little, so a fading dash also softens,
        /// and no more, because the overview sees the beam nearly end on and a short wide dash reads as a bar across
        /// it (the glow's remarks).
        /// </summary>
        private const float DASH_FAR_WIDTH = 1.6f * WIDTH;

        /// <summary>
        /// The glow's light at the touch, against a dash's core at the muzzle. Far wider than a dash, so far less
        /// light: as bright as the line, the patch would be a broad smear over the very cells it is there to show.
        /// Set by looking, as <see cref="WIDTH"/> was, between the two ends an additive glow has: over the moon's black
        /// sky this is a clear soft patch some two balls across (the bloom spreads it past its tenth-light edge), over
        /// the meadow's white and yellow balls in daylight a faint haze.
        /// </summary>
        internal const float GLOW_LIGHT = 0.45f;

        /// <summary>
        /// Length of one chord of the glow, in world units. Short enough that the glow eases out past the touch over
        /// about this much (the chords simply stop) and follows a flight bent by a well chord by chord; long enough
        /// that a beam's glow is a couple of dozen draws.
        /// </summary>
        private const float GLOW_CHUNK = 1.5f;

        /// <summary>
        /// How many chords overlap at any point of the glow's run: three, a third of a chord apart. That is the
        /// overlap at which the shader's quarter-length fades add up flattest (±3 %; two give ±33 %, four ±9 %).
        /// </summary>
        private const int GLOW_LAYERS = 3;

        /// <summary>
        /// The light the overlapping chords add up to along their run, in units of one chord's: the integral of the
        /// fade's square over a chord, <c>24/35</c> (a plateau of a half and two smoothstep ramps of a quarter, each
        /// contributing <c>13/35</c> of its length), times <see cref="GLOW_LAYERS"/>. Each chord is dimmed by it, so
        /// the run's light is the light asked for.
        /// </summary>
        internal const float GLOW_OVERLAP = GLOW_LAYERS * 24f / 35f;

        /// <summary>
        /// How far past the touch the glow runs before its chords stop, in world units, <b>square to the lens</b>
        /// (<see cref="FacingLens"/>): in the direction the flight carries on in on screen, with none of it into the
        /// screen. A ghost standing off the line towards the lens or away from it, up to <see cref="LANDING_SPREAD"/>,
        /// shows on screen beside the touch along the beam's screen direction, and from the overview, which sees the
        /// beam ten-odd degrees off end on, a run along the flight would show there at a fifth of its length and lie
        /// behind the ball hit. Square to the lens it shows at its full length from any view, in front of that ball;
        /// side on it is the flight's own direction, and covers a ghost standing past the touch (up to 0.38). The glow
        /// eases out over its last chord, so a ghost <see cref="LANDING_SPREAD"/> past the touch on screen keeps about
        /// half the glow's light.
        /// </summary>
        private const float OVERRUN = 1.2f;

        /// <summary>
        /// How far short of the touch the cone is fully open, in world units: as far as a ghost stands short of it
        /// (0.68 at the most, <see cref="LANDING_SPREAD"/>), so the whole landing region lies under the far width and a
        /// ghost at its near edge is not left at the side of a cone still opening.
        /// </summary>
        private const float REGION_BEHIND = 0.7f;

        /// <summary>A bound on the glow's chords, as <see cref="MAX_DASHES"/> is on the dashes: past it they are spread further apart.</summary>
        private const int MAX_GLOW_CHUNKS = 48;

        /// <summary>
        /// The glow's light on the Potato path, against the desktop's: what the Game hands the constructor there.
        /// <para>
        /// <b>Potato curves every additive layer by itself</b> (GamePi's <c>PotatoOutput.fxh</c>: it has no linear target
        /// to sum the light in first, so its <c>ShotTrail.fx</c> adds <c>ToDisplay(light)</c> in display space), and the
        /// display curve is steepest at the bottom. A single dash comes out exactly as on the desktop over a black
        /// backdrop; the glow's three overlapping chords do not, since three curved thirds add up to far more than the
        /// curved whole. At the desktop's light the patch was a white-hot flare over the meadow and a saturated blob
        /// over the moon. No one gain gives the desktop's whole profile back, because the curve bends the bright core and
        /// the dim edges in opposite directions: this one puts the glow's body (a fifth of a ball off the axis, level with
        /// the median ghost) on the desktop's display value over black, which <c>AimBeamTests</c> reckons with the curve
        /// and holds within a tenth; the axis itself clips a little (1.19 against 0.95 for a channel of one), and a ghost's
        /// median offset gets about 0.45 of display where the desktop gives 0.75, so the Potato patch reads a little
        /// smaller.
        /// </para>
        /// </summary>
        public const float POTATO_GLOW_GAIN = 0.25f;

        /// <summary>
        /// Pieces a beam can be laid as: a dash a knot of a bent flight cuts is two, and the rare one that two knots
        /// cut is three, and the glow's chords are never more than <see cref="MAX_GLOW_CHUNKS"/>.
        /// </summary>
        internal const int MAX_PIECES = 3 * MAX_DASHES + MAX_GLOW_CHUNKS;

        #endregion

        /// <summary>One quad of a laid beam: its two ends, the half-width at each and its alpha before the caller's opacity.</summary>
        internal readonly record struct Piece(Vector3 Tail, Vector3 Head, float TailWidth, float HeadWidth, float Alpha);

        private readonly GraphicsDevice _device;

        //Cached at construction, per BestPractices.md §1: the by-name indexer is a string scan of the whole
        //parameter collection, and the first four go out once a frame while the rest go out PER PIECE.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _headParam, _tailParam, _colorParam, _alphaParam;
        private readonly EffectParameter _headWidthParam, _tailWidthParam;

        private readonly EffectPass _pass;

        //The frame's layout, reused: laid into, drawn from, never reallocated
        private readonly Piece[] _pieces = new Piece[MAX_PIECES];

        private readonly float _glowGain;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        /// <param name="shotTrailEffect">The compiled <c>Shaders/ShotTrail.fx</c>, the same instance
        /// <see cref="LaunchSmears"/> is given. Handed in and never disposed here — the caller's content
        /// manager owns its lifetime.</param>
        /// <param name="glowGain">Scales the glow's light: 1 on the desktop, <see cref="POTATO_GLOW_GAIN"/> on the
        /// Potato path, whose effect curves each additive layer by itself.</param>
        public AimBeam(GraphicsDevice device, Effect shotTrailEffect, float glowGain = 1f)
        {
            _device = device;
            _glowGain = glowGain;

            _viewParam = shotTrailEffect.Parameters["View"];
            _projectionParam = shotTrailEffect.Parameters["Projection"];
            _cameraPositionParam = shotTrailEffect.Parameters["CameraPosition"];
            _headParam = shotTrailEffect.Parameters["TrailHead"];
            _tailParam = shotTrailEffect.Parameters["TrailTail"];
            _colorParam = shotTrailEffect.Parameters["TrailColor"];
            _alphaParam = shotTrailEffect.Parameters["TrailAlpha"];
            _headWidthParam = shotTrailEffect.Parameters["TrailHeadWidth"];
            _tailWidthParam = shotTrailEffect.Parameters["TrailTailWidth"];

            _pass = shotTrailEffect.CurrentTechnique.Passes[0];

            CreateQuad();
        }

        /// <summary>
        /// Draws the beam along <paramref name="path"/>, from its first point (the muzzle) to its last.
        /// </summary>
        /// <param name="camera">The frame's camera; the pieces turn to face it, and the glow past the touch is laid
        /// square to it (<see cref="OVERRUN"/>).</param>
        /// <param name="path">The flight, the muzzle first and the far end last: two points for a straight shot, the
        /// knots of the curve for one bent by a gravity well or banked off a crate. The dashes, the cone and the
        /// open fade are laid along the whole of it, so the knots do not show.</param>
        /// <param name="srgbTint">The line's colour as authored (sRGB). Decoded to linear here, its hue kept but
        /// its peak lifted to <see cref="COLOR_FLOOR"/> and then boosted, which is the same rule
        /// <see cref="LaunchSmears"/> applies to a smear and for the same reason.</param>
        /// <param name="phase">A seconds clock. Only its fractional position within a period matters, so the
        /// wall clock serves — and being the wall clock, the crawl keeps going while a pause holds the session.</param>
        /// <param name="openEnded">True when the far end is open air rather than something the shot would hit, which
        /// fades the dashes out along the last leg instead of ending them at a point. It draws no cone.</param>
        /// <param name="spread">How far to the side of the far end the shot may come to rest: <see cref="LANDING_SPREAD"/>
        /// where it snaps into a lattice cell beside the ball it touches, opening the beam into a cone and a patch
        /// over that region; zero for a plain line, as a cutter, which strikes the very ball the beam ends on, wants
        /// (and so is anything up to <see cref="WIDTH"/> × <see cref="TENTH_LIGHT_ACROSS"/>, whose far width,
        /// <see cref="FarWidth"/>, is the line's own).</param>
        /// <param name="opacity">Scales the whole beam. The Game hands over the inverse of its precise-aim blend:
        /// leaning in over the barrel the lens looks <i>along</i> the bore, so the beam foreshortens into a stack of
        /// dashes piled over the very cell it is pointing at — and it has nothing to add there, because a crosshair
        /// on a lens aimed down the shot ray already <b>is</b> the trajectory. Each mode keeps the one signal that
        /// suits it. At or below <see cref="MIN_OPACITY"/> nothing is drawn and no state is touched.</param>
        public void Draw(ICamera camera, ReadOnlySpan<Vector3> path, Vector3 srgbTint, float phase, bool openEnded,
            float spread, float opacity = 1f)
        {
            if (opacity <= MIN_OPACITY) return;

            int count = Lay(path, phase, openEnded, spread, camera.Position, _pieces, _glowGain);
            if (count == 0) return;

            Vector3 linear = ColorSpace.SrgbToLinear(srgbTint);
            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            if (peak < COLOR_FLOOR) linear *= COLOR_FLOOR / MathF.Max(peak, Constants.THOUSANDTH);

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);
            _colorParam.SetValue(linear * BRIGHTNESS);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            //The widths last sent. NaN equals nothing, so every Draw sends its first piece's
            float tailWidth = float.NaN, headWidth = float.NaN;

            for (int i = 0; i < count; i++)
            {
                ref readonly Piece piece = ref _pieces[i];

                _tailParam.SetValue(piece.Tail);
                _headParam.SetValue(piece.Head);

                //Per draw and not per construction -- see the remarks: the effect is shared with LaunchSmears and
                //LineSparks, which want widths of their own. Within this loop nothing else draws through it, so a
                //width is sent only when it changes: a plain dash's are the line's, every piece of a cone has its own
                if (piece.TailWidth != tailWidth) _tailWidthParam.SetValue(tailWidth = piece.TailWidth);
                if (piece.HeadWidth != headWidth) _headWidthParam.SetValue(headWidth = piece.HeadWidth);
                _alphaParam.SetValue(piece.Alpha * opacity);

                _pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// The glow's half-width at the touch for a <paramref name="spread"/>: wide enough that its light still reaches
        /// a tenth of its core at the spread's distance (<see cref="TENTH_LIGHT_ACROSS"/>), and never narrower than the line.
        /// </summary>
        internal static float FarWidth(float spread) => MathF.Max(WIDTH, spread / TENTH_LIGHT_ACROSS);

        /// <summary>
        /// Lays the beam out along <paramref name="path"/> as the pieces <see cref="Draw"/> draws, into
        /// <paramref name="pieces"/> (<see cref="MAX_PIECES"/> is always enough), and says how many. Pure: the same
        /// arguments give the same pieces, and nothing is allocated.
        /// </summary>
        /// <remarks>
        /// <b>Everything is measured along the whole flight</b>, from the muzzle: the dash pattern, so it crawls on
        /// through a knot; and the cone, so a beam bent by a well opens once over its whole length rather than once
        /// per leg. A dash that a knot cuts is two pieces, each with the widths at its own two ends, which makes the
        /// two halves one continuous slice of the line.
        /// <para>
        /// <b>The flight's far direction is its last leg with a length</b> (<see cref="LastLeg"/>): a stepped flight can
        /// put a knot within a thousandth of its end, and a last leg of nothing has no direction for the glow to run on
        /// past the touch in, nor a length for the open fade to run over.
        /// </para>
        /// </remarks>
        /// <param name="viewer">Where the camera is: the glow past the touch is laid square to its view (<see cref="OVERRUN"/>).</param>
        internal static int Lay(ReadOnlySpan<Vector3> path, float phase, bool openEnded, float spread, Vector3 viewer,
            Span<Piece> pieces, float glowGain = 1f)
        {
            if (path.Length < 2 || pieces.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i + 1 < path.Length; i++) total += Vector3.Distance(path[i], path[i + 1]);

            if (total < SPACING * MIN_DASH_FRACTION) return 0;

            float farWidth = openEnded ? WIDTH : FarWidth(spread);
            bool cone = farWidth > WIDTH;

            int lastLeg = LastLeg(path);

            int count = LayDashes(path, total, lastLeg, phase, openEnded, cone, pieces);
            if (cone) count = LayGlow(path, total, lastLeg, farWidth, viewer, glowGain, pieces, count);

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
        /// The dashed line: the crawl along the whole flight, the open fade over the last leg of a beam that reaches
        /// nothing, and on a cone the hand-over to the glow (<see cref="GLOW_FROM"/>), by which the dashes widen a
        /// little and fade out by the touch.
        /// </summary>
        private static int LayDashes(ReadOnlySpan<Vector3> path, float total, int finalLeg, float phase, bool openEnded,
            bool cone, Span<Piece> pieces)
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

            for (float start = origin; start < total && dashes < MAX_DASHES; start += SPACING)
            {
                float from = MathF.Max(start, 0f);
                float to = MathF.Min(start + dashLength, total);
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
                else if (cone)
                {
                    //Handed over to the glow: the dash's light falls as the glow's rises, so the two cross over rather
                    //than add up. The alpha carries the light squared (TENTH_LIGHT_ACROSS)
                    alpha = MathF.Sqrt(1f - HandOver((from + to) * Constants.HALF / total));
                    if (alpha <= Constants.THOUSANDTH) continue;
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
                            PointOnLeg(path, at, atStart, atLength, b),
                            DashWidth(a / total, cone), DashWidth(b / total, cone), alpha);
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
        /// The glow: chords of the flight <see cref="GLOW_CHUNK"/> long and a third of one apart (<see cref="GLOW_LAYERS"/>),
        /// from where the hand-over begins to <see cref="OVERRUN"/> past the touch, each as wide as the cone where its
        /// ends are and as bright as the hand-over has risen to at its middle. Static: the dashes crawl, the patch stays.
        /// </summary>
        private static int LayGlow(ReadOnlySpan<Vector3> path, float total, int finalLeg, float farWidth, Vector3 viewer,
            float gain, Span<Piece> pieces, int count)
        {
            float from = GLOW_FROM * total;
            float end = total + OVERRUN;

            //Past the touch the run turns square to the lens (OVERRUN): the far leg's direction with none of it into the screen
            Vector3 touch = path[^1];
            Vector3 overrun = FacingLens(Vector3.Normalize(path[finalLeg + 1] - path[finalLeg]), touch - viewer);

            //Where the cone is fully open: the near edge of the landing region, so the whole of it is at the far width
            float open = MathF.Max(total - REGION_BEHIND, Constants.THOUSANDTH);

            //Anchored at the far end, so the chords stop exactly where the glow is to ease out, and laid back to where
            //it begins. A run too long for the cap gets its chords spread further apart rather than cut short
            float run = end - from;
            float hop = MathF.Max(GLOW_CHUNK / GLOW_LAYERS, (run + GLOW_CHUNK) / MAX_GLOW_CHUNKS);
            float dim = 1f / (GLOW_OVERLAP * (GLOW_CHUNK / GLOW_LAYERS) / hop);

            for (float tail = end - GLOW_CHUNK; tail + GLOW_CHUNK > from && count < pieces.Length; tail -= hop)
            {
                float head = tail + GLOW_CHUNK;
                float middle = MathF.Max(tail, from) * Constants.HALF + head * Constants.HALF;

                float light = GLOW_LIGHT * gain * HandOver(middle / total) * dim;
                if (light <= Constants.THOUSANDTH * Constants.THOUSANDTH) continue;

                pieces[count++] = new Piece(GlowPoint(path, total, overrun, tail), GlowPoint(path, total, overrun, head),
                    ConeWidth(tail / open, farWidth), ConeWidth(head / open, farWidth), MathF.Sqrt(light));
            }

            return count;
        }

        /// <summary>
        /// How far the hand-over from the dashes to the glow has gone at <paramref name="along"/>, a fraction of the
        /// flight from the muzzle: 0 before <see cref="GLOW_FROM"/>, 1 at the touch and past it, a smoothstep between.
        /// </summary>
        internal static float HandOver(float along) => SmoothStep(GLOW_FROM, 1f, along);

        /// <summary>
        /// The glow's half-width at <paramref name="along"/>, a fraction of the way from the muzzle to where the cone is
        /// fully open (<see cref="REGION_BEHIND"/> short of the touch); the far width from there on.
        /// </summary>
        internal static float ConeWidth(float along, float farWidth) =>
            WIDTH + (farWidth - WIDTH) * MathF.Pow(Math.Clamp(along, 0f, 1f), CONE_POWER);

        //A dash's half-width: the line's, grown a little over the hand-over on a cone
        private static float DashWidth(float along, bool cone) =>
            cone ? MathHelper.Lerp(WIDTH, DASH_FAR_WIDTH, HandOver(along)) : WIDTH;

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        //A point at a distance along the flight, on the leg that covers it; a leg of nothing is its one point
        private static Vector3 PointOnLeg(ReadOnlySpan<Vector3> path, int leg, float legStart, float legLength, float distance) =>
            legLength > Constants.THOUSANDTH
                ? Vector3.Lerp(path[leg], path[leg + 1], (distance - legStart) / legLength)
                : path[leg];

        //A point of the glow's run: on the flight up to the touch, the leg found by walking from the muzzle (a glow is a
        //couple of dozen chords over a flight of a few legs, a few dozen where a well bends it, so the walk is cheaper than
        //any cursor kept for it), and past the touch along `overrun`, square to the lens. Before the muzzle it is the muzzle
        private static Vector3 GlowPoint(ReadOnlySpan<Vector3> path, float total, Vector3 overrun, float distance)
        {
            if (distance <= 0f) return path[0];
            if (distance >= total) return path[^1] + overrun * (distance - total);

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
