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
    /// reach and says what that is (<see cref="FarEnd"/>), and hands the colour to match: the loaded ball's tint
    /// where the shot sticks, a refusal colour where it will not. Ended in open air — the aim reaching
    /// nothing at all — the dashes are faded out along the way so the line dies away rather than stopping in
    /// mid-air as if something were there.
    /// </para>
    /// <para>
    /// <b>And where the shot snaps into the lattice, its far dashes widen a little and fade out at the ball</b> (#1144
    /// asked for the region the shot lands in; the owner turned down a soft glow and then a fan of lines for it, #1196, and
    /// asked for the one dashed line, thin and quiet, whose dashes "widen slightly and smoothly towards the end, turn
    /// transparent, and stop at the ball"). On a <see cref="FarEnd.Landing"/> the beam is the plain line up to
    /// <see cref="FAR_FROM"/> of its length; over the rest its dashes widen along one smooth curve to
    /// <see cref="FAR_WIDTH"/>, lengthen with it (<see cref="FAR_STRETCH"/>, <see cref="FAR_DASH_FRACTION"/>) and give up
    /// their light, each dash along itself, to nothing where the beam reaches the ball it touches. A
    /// <see cref="FarEnd.Strike"/> (a cutter's, which strikes that ball itself and has no cell to show) is the plain line at
    /// full strength, and it stops there too. Either way it is one dashed line from the muzzle on, and nothing is drawn
    /// over the ball or past it.
    /// </para>
    /// <para>
    /// <b>And a landing's line opens into a thin three-pronged fork that fades out</b> (#1235, the owner's sketch, "so that it
    /// is a little visible that the ball does not land exactly where the line ends"): from <see cref="FORK_FROM"/> of the way
    /// to where the lens sees the beam reach the ball, two prongs peel off the line on either side along a smoothstep (no
    /// corner, not a pitchfork), part to the measured landing region (<see cref="FORK_SPREAD"/>) by <see cref="FORK_FULL"/>
    /// and run on parallel, dashed with the line's own dashes at the line's width, a little fainter than it
    /// (<see cref="FORK_LIGHT"/>), and fading out with it at the ball. They part square to the lens's line of sight, so the
    /// overview, which sees the beam nearly end on, shows them apart: 74 px across at the median on a 1080-line frame.
    /// The middle line is laid exactly as without them (<see cref="LayProngs"/>).
    /// </para>
    /// </summary>
    /// <remarks>
    /// It borrows <c>Shaders/ShotTrail.fx</c> from <see cref="LaunchSmears"/> rather than bringing a shader of
    /// its own: that shader is already a camera-facing billboard between two world points, and it fades each
    /// segment in over its first quarter and out over its last, so a <i>short</i> segment comes out as a dash
    /// with soft ends and no new code at all; its two half-widths are lerped along the billboard, so a dash that widens
    /// is a slim trapezoid. One piece is one draw of the shared quad.
    /// <para>
    /// <b>Where the beam reaches the ball is where the lens sees it reach the ball</b> (<see cref="VisibleEnd"/>). The touch
    /// lies on the ball's near surface, and the overview sees the beam six to twenty degrees off end on, so the beam's last
    /// unit or few lie in front of that ball on screen: a line run to the touch draws over the ball it is about to hit, as
    /// the fan of lines did ("it covers the ball"). So a landing's fade and a strike's line both end where the beam's line
    /// of sight first meets the ball's disc, and the dashes are laid no further; seen from the side the two are the same
    /// point, the touch.
    /// </para>
    /// <para>
    /// <b>The far dashes are laid along the flight and faded by the lens.</b> Where they widen and lengthen is measured
    /// along the flight to the touch, which the camera does not move; only where their light runs out, and where they stop,
    /// come from where the lens sees the beam reach the ball. So a lens leaning in or shaken by a shot changes how bright the
    /// far dashes are and not where they are. ⚠ A change in the flight does move them: the far part is laid from the touch,
    /// so when the aim slides the touch over a ball, and most of all when it moves to another ball, every far dash moves
    /// with it, not only the tip (the plain line's dashes are laid from the muzzle and never do).
    /// </para>
    /// <para>
    /// <b>A far dash fades along itself, not dash by dash.</b> The far dashes are long (the period grows to
    /// <see cref="FAR_STRETCH"/> times <see cref="SPACING"/>) and the fade has half a beam to run over, a few dashes, so a dash
    /// holding one alpha left the last one drawn with up to nearly half the line's light and a gap short of the ball. So
    /// every piece carries the fade's alpha at each of its two ends and the shader lerps between them
    /// (<c>TrailHeadFade</c>): the dash crawling into the ball reaches it at nothing, and the one a gap behind it ends at
    /// next to nothing.
    /// </para>
    /// <para>
    /// <b>The far end widens and still never grows bolder.</b> The light a dash adds goes as the square of its alpha
    /// (the additive blend weighs the alpha twice) times its cross-section, so a dash twice as wide at the same alpha would
    /// carry twice the light. Over the far part the alpha is cut by the square root of the widening as well as faded
    /// (<see cref="FarAlpha"/>): the light a dash adds across its whole width falls with the fade, never above the
    /// line's, and its brightest point falls faster still, so the far end is fainter where it is wider.
    /// </para>
    /// <para>
    /// <b>The far dashes grow longer with their width, or the overview reads them as rungs.</b> That lens sees a dash's
    /// length at a fifth of itself or less while its width (the billboard faces the lens) shows whole: #1144 found widened
    /// dashes of the line's length squash into bars across it, level with each other like the rungs of a ladder. So the
    /// pattern's period and the share of it lit grow along the width's curve (<see cref="FAR_STRETCH"/>,
    /// <see cref="FAR_DASH_FRACTION"/>), the length further than the width: each far dash is a near one drawn longer for
    /// its width, and the crawl carries the dashes outwards faster as they grow.
    /// </para>
    /// <para>
    /// <b>The widths and the alpha are pushed per draw, and they have to be.</b> <see cref="LaunchSmears"/> set the widths
    /// once in its constructor, correctly, on the reasoning that a compile-time constant need not be re-sent per frame
    /// (<c>BestPractices.md</c> §1 names those two parameters as its own example). That stopped being true the
    /// moment a second component shared the effect: the two want different widths, an <c>Effect</c>'s parameter
    /// values are the effect's and not the caller's, and whichever constructor ran last would otherwise decide
    /// how both look. Here every draw sends its first piece's two, and each later piece's where they differ from the
    /// last (a far dash's differ from the one before it). <c>TrailHeadFade</c> is the other way round: only this component
    /// sets it, the other two draw with the 0 an effect starts with, so it is sent per piece where it differs and put back
    /// to 0 after the last.
    /// </para>
    /// <para>
    /// <b>The layout is a pure function</b> (<see cref="Lay"/>), apart from the draw, so what the beam promises can be
    /// asked of it without a graphics device: <c>AimBeamTests</c> holds the near end to the plain line, the width to one
    /// smooth curve however the flight is cut into legs, nothing past the touch, the far dashes never bolder than the line
    /// and faded to next to nothing by the last one at every point of the crawl, and, through the Game's overview, the far
    /// dashes longer than wide and neither a landing's beam nor a strike's over the ball the shot touches.
    /// </para>
    /// </remarks>
    public sealed class AimBeam : IDisposable
    {
        #region The dials

        /// <summary>
        /// World length of one dash plus the gap after it — the pattern's period along the line, up to where the far
        /// dashes start growing (<see cref="FAR_STRETCH"/>).
        /// </summary>
        internal const float SPACING = 1.1f;

        /// <summary>How much of a period is lit. Under a half so the line reads as dashes rather than as a rod.</summary>
        internal const float DASH_FRACTION = 0.42f;

        /// <summary>
        /// Half-width of a dash. A ball is <c>1</c> across, so this is a line about a quarter of a ball wide — which
        /// sounds fat and is not: the alpha of <c>ShotTrail.fx</c>'s streak falls across it as <c>(1 - across)²</c> and an
        /// additive draw weighs the alpha twice, so the light goes as <c>(1 - across)⁴</c>, nearly nine-tenths of it inside
        /// the middle third of that.
        /// <para>
        /// <b>Both this and <see cref="BRIGHTNESS"/> were set by looking, and the first guesses were wrong both ways.</b>
        /// 0.085 and 1.35 were reasoned from the ball's radius and from the smear's boost, and the beam came out
        /// <i>invisible</i> at the overview's stand-off, the mode it exists for; 0.45 and 8 were unmistakable and far too
        /// heavy. 0.15 and 3.5 between them were the line for a long time, and the owner then found it loud: "the aim beam
        /// must not disturb: thin, not as conspicuous and bold" (#1196). Against the black of the moon its core bloomed into a
        /// glowing bead a dash, and next to the muzzle, eleven units from the overview's lens, it was a fat stroke; so it is
        /// a fifth thinner and a third dimmer now. Anything derived from a ball's size rather than from what a few dozen
        /// pixels of an additive line actually look like over a bright cluster should be expected to be wrong the same way.
        /// </para>
        /// </summary>
        internal const float WIDTH = 0.12f;

        /// <summary>
        /// Radiance boost of the dashes. Over <see cref="LaunchSmears"/>'s, which is not what one would guess (a smear is
        /// a shot going off and should flare, while this is up the whole time the gun is aimed), but the smear is briefly
        /// enormous where this is permanently thin, and a thin additive line over a lit cluster needs the radiance to read
        /// at all: in daylight over white balls it is a faint line at this. See <see cref="WIDTH"/> on how both were arrived at.
        /// </summary>
        private const float BRIGHTNESS = 2.4f;

        /// <summary>Lowest peak channel of the dashes, so the near-black ball still gives a visible grey line.</summary>
        private const float COLOR_FLOOR = 0.2f;

        /// <summary>
        /// World units a second the dash pattern crawls towards the far end, where its period is
        /// <see cref="SPACING"/>. Slow: a hint of travel, not a barber's pole. The pattern is laid along the whole flight
        /// from <c>phase * CRAWL</c>, so on a beam bent by a gravity well (#332) the dashes run on through every knot
        /// instead of restarting at each; over the far part, where the period grows, they move on that much faster.
        /// </summary>
        internal const float CRAWL = 1.6f;

        /// <summary>
        /// A dash clipped shorter than this fraction of itself whole is skipped. The pattern is clipped at both ends of
        /// the line, so without this the ends flicker a one-pixel sliver in and out as the crawl moves. Of the dash itself
        /// and not of its period: a far dash's period grows to over three times the line's, and a quarter of that skipped
        /// the dash crawling into the ball while it was still nearly a ball long, leaving the last two units empty.
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
        /// reasoning, for the mark this one hands over to. A far dash whose own alpha has faded under it is skipped too.
        /// </summary>
        public const float MIN_OPACITY = 0.01f;

        /// <summary>
        /// <b>How far to the side of the beam's end the ghost can sit</b>, in world units (#1144). <b>Measured, not
        /// chosen</b>: over 478 aims at the six levels <c>ShotAgreementTests</c> sweeps, each asked as the Game asks (the
        /// curved sweep with the world's gravity, then <c>ShotPlacement.TrySolveAgainstBall</c>), the ghost's centre stood
        /// off the line through the touch by 0.45 at the median, 0.61 at the 95th percentile, 0.64 at the 99th and 0.67 at
        /// the most (along the line it stood 0.34 short of the touch at the median, from 0.68 short to 0.38 past).
        /// <c>AimBeamTests</c> re-measures it and says how much of it <see cref="FAR_WIDTH"/> reaches: the beam's far end
        /// points at the region, it does not outline it (the owner wanted it narrow, #1196).
        /// </summary>
        internal const float LANDING_SPREAD = 0.65f;

        /// <summary>
        /// Where the far part begins, as a fraction of the beam's length: before it the beam is the plain line, from it its
        /// dashes widen and lengthen (that share of the flight on, to the touch) and fade (that share of the length to where
        /// the lens sees the beam reach the ball on, to there, <see cref="VisibleEnd"/>). Half way, so the overview, which
        /// reads the aim off the near part, keeps the plain line over half of it, and the fade has the other half to be
        /// gradual in.
        /// </summary>
        internal const float FAR_FROM = 0.5f;

        /// <summary>
        /// Half-width of a dash at the touch, in world units: a little over twice the line's
        /// (<see cref="WIDTH"/>), which is what it takes to widen "slightly", in the owner's word, on screen at all: the
        /// overview stands half again as far from the far end as from the middle of the beam, so a width that grew less would
        /// shrink there. Seen from it at a 1080-line frame the dashes' half-light width goes from about 5 px half way to about
        /// 6.5 px at the last dash with a tenth of the light, still well under the measured landing region's reach
        /// (<see cref="LANDING_SPREAD"/>). The light has all but gone by where the dashes are widest (<see cref="FarAlpha"/>),
        /// so the widening shows as the line's last lit dashes growing a little, not as a broad end.
        /// </summary>
        internal const float FAR_WIDTH = 0.26f;

        /// <summary>
        /// The pattern's period at the touch, over <see cref="SPACING"/>: the far dashes lengthen along
        /// the same curve they widen along, by the period and by the share of it lit (<see cref="FAR_DASH_FRACTION"/>), so
        /// a dash there is four and a half times the line's length at a little over twice its width, and the overview, which
        /// sees it nearly end on, shows it no squatter than the line's own dashes there rather than as a rung (the remarks;
        /// <c>AimBeamTests</c> holds it over the measured aims).
        /// </summary>
        internal const float FAR_STRETCH = 3.2f;

        /// <summary>
        /// The share of a period lit at the touch, against <see cref="DASH_FRACTION"/> on the plain line: the far dashes take
        /// more of their period as they lengthen, so the gaps between them grow less than the dashes do. A gap still grows,
        /// and the last dash drawn can end one gap short of the ball, or a little more, as the crawl carries the one before
        /// it in; it is the fade along each dash (<see cref="FarAlpha"/>) that has that dash's head at next to nothing then.
        /// </summary>
        internal const float FAR_DASH_FRACTION = 0.6f;

        /// <summary>
        /// The ball's radius, as <c>BallRenderSet.BALL_RADIUS</c> and the physics' own have it (this library sees
        /// neither): the size of the disc the ball the shot touches shows the lens (<see cref="VisibleEnd"/>).
        /// </summary>
        private const float BALL_RADIUS = Constants.HALF;

        /// <summary>
        /// The step the far part's period is integrated over, in world units (<see cref="Advance"/>). The period bends over
        /// the far part's whole length, several units, so at a tenth of a unit the midpoint rule puts every dash within two
        /// thousandths of a unit of where a march two hundred times finer would, over far parts four to twelve units long.
        /// </summary>
        private const float MARCH = 0.1f;

        /// <summary>
        /// The step the search for where the beam reaches the ball's disc walks back from the touch in, in world units
        /// (<see cref="VisibleEnd"/>), before it halves the step it crossed in down to a thousandth.
        /// </summary>
        private const float SIGHT_STEP = 0.25f;

        /// <summary>
        /// Pieces a beam can be laid as: a dash a knot of a bent flight cuts is two, and the rare one that two knots
        /// cut is three.
        /// </summary>
        internal const int MAX_PIECES = 3 * MAX_DASHES;

        /// <summary>
        /// <b>How far to the side the fork's two outer prongs part</b>, in world units, at their widest (#1235): the
        /// measured landing region (<see cref="LANDING_SPREAD"/>), so the fork says what the owner asked it to say, "that the
        /// ball does not land exactly where the line ends", and no more.
        /// </summary>
        internal const float FORK_SPREAD = LANDING_SPREAD;

        /// <summary>
        /// Where the fork's outer prongs start to peel off the line, as a share of the beam's length to where the lens sees it
        /// reach the ball (<see cref="VisibleEnd"/>). Earlier than the far part (<see cref="FAR_FROM"/>) on purpose: the overview
        /// sees the beam foreshortened, the muzzle eleven units off and the far end twenty-five, so the far half of the flight
        /// is only its last third on screen, mostly over the cluster already, and a fork opening there was hidden among the
        /// balls. From 0.35 of the flight the fork takes about the last two fifths of the beam on screen, as the owner's sketch
        /// has it, and opens in the open air under the cluster.
        /// </summary>
        internal const float FORK_FROM = 0.35f;

        /// <summary>
        /// Where the outer prongs have parted to <see cref="FORK_SPREAD"/>, as the same share: along a smoothstep from
        /// <see cref="FORK_FROM"/> (no corner where they leave the line), after which they run on parallel and fade out with the
        /// line at the ball. At 0.65 of the flight, about five sixths of the beam on screen, while a third of the line's
        /// light is still left there.
        /// </summary>
        internal const float FORK_FULL = 0.65f;

        /// <summary>
        /// How far apart the outer prongs are, as a share of <see cref="FORK_SPREAD"/>, when they have their whole light
        /// (<see cref="FORK_LIGHT"/>): they gain it along a smoothstep as they leave the line, so a prong still lying over it
        /// adds next to nothing to it, and one clear of it (a third of the spread is about two of the line's widths aside)
        /// shows whole. Tied to the whole opening, a prong had a quarter of its light half way open and was lost against a
        /// daylight sky.
        /// </summary>
        internal const float FORK_SHOW = 0.35f;

        /// <summary>
        /// An outer prong's alpha, once clear of the line, against the plain line's under the same fade, at the plain line's
        /// own width: a little under it, so no prong is bolder than the line the owner accepted and the middle one reads as
        /// the aim, and so the farthest prong ends within the tenth of the line's light the line itself ends at (a prong does
        /// not widen, so at the same fade its core is brighter than the middle dash's; at 1 it ended at 0.106). At 0.8 tied to
        /// how far it had opened, a prong carried a fifth of the line's light half way open and did not show against a
        /// daylight sky.
        /// </summary>
        internal const float FORK_LIGHT = 0.9f;

        /// <summary>Pieces the fork's two outer prongs can be laid as: two for every piece of the middle line.</summary>
        internal const int MAX_FORK_PIECES = 2 * MAX_PIECES;

        #endregion

        /// <summary>What the beam's far end is: what a shot fired now would reach, and so how the beam ends.</summary>
        public enum FarEnd
        {
            /// <summary>Nothing: the dashes fade out along the flight's last leg, so the line dies away in the air.</summary>
            OpenAir,

            /// <summary>
            /// The shot snaps into a lattice cell beside the ball it touches: the far dashes widen, lengthen and fade out
            /// where the lens sees the beam reach that ball (the class summary). A refused shot is one too, in its colour.
            /// </summary>
            Landing,

            /// <summary>
            /// The shot strikes the ball it touches and stays out of the lattice (a cutter): the plain line at full strength,
            /// stopping where the lens sees the beam reach that ball, as a landing's fade does.
            /// </summary>
            Strike
        }

        /// <summary>
        /// One quad of a laid beam: its two ends, the half-width at each, and the alpha at each before the caller's opacity
        /// (one alpha all along a dash of the plain line or of an open beam's fade; the far part's fade at each end over a
        /// landing's far part, lerped between them by the shader).
        /// </summary>
        internal readonly record struct Piece(Vector3 Tail, Vector3 Head, float TailWidth, float HeadWidth, float TailAlpha,
            float HeadAlpha);

        private readonly GraphicsDevice _device;

        //Cached at construction, per BestPractices.md §1: the by-name indexer is a string scan of the whole
        //parameter collection, and the first four go out once a frame while the rest go out PER PIECE.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;
        private readonly EffectParameter _headParam, _tailParam, _colorParam, _alphaParam, _headFadeParam;
        private readonly EffectParameter _headWidthParam, _tailWidthParam;

        private readonly EffectPass _pass;

        //The frame's layout, reused: laid into, drawn from, never reallocated. The fork's outer prongs apart (#1235)
        private readonly Piece[] _pieces = new Piece[MAX_PIECES];
        private readonly Piece[] _forks = new Piece[MAX_FORK_PIECES];

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        /// <param name="shotTrailEffect">The compiled <c>Shaders/ShotTrail.fx</c>, the same instance
        /// <see cref="LaunchSmears"/> is given. Handed in and never disposed here — the caller's content
        /// manager owns its lifetime.</param>
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
            _headFadeParam = shotTrailEffect.Parameters["TrailHeadFade"];
            _headWidthParam = shotTrailEffect.Parameters["TrailHeadWidth"];
            _tailWidthParam = shotTrailEffect.Parameters["TrailTailWidth"];

            _pass = shotTrailEffect.CurrentTechnique.Passes[0];

            CreateQuad();
        }

        /// <summary>
        /// Draws the beam along <paramref name="path"/>, from its first point (the muzzle) to its last.
        /// </summary>
        /// <param name="camera">The frame's camera; the dashes turn to face it, and a landing or a strike ends where it sees
        /// the beam reach <paramref name="ballTouched"/>.</param>
        /// <param name="path">The flight, the muzzle first and the far end last: two points for a straight shot, the
        /// knots of the curve for one bent by a gravity well or banked off a crate. The dashes, the far part and the
        /// open fade are laid along the whole of it, so the knots do not show.</param>
        /// <param name="srgbTint">The line's colour as authored (sRGB). Decoded to linear here, its hue kept but
        /// its peak lifted to <see cref="COLOR_FLOOR"/> and then boosted, which is the same rule
        /// <see cref="LaunchSmears"/> applies to a smear and for the same reason.</param>
        /// <param name="phase">A seconds clock. Only its fractional position within a period matters, so the
        /// wall clock serves — and being the wall clock, the crawl keeps going while a pause holds the session.</param>
        /// <param name="farEnd">What the far end is (<see cref="FarEnd"/>): open air, which fades the dashes out along the
        /// last leg, none of them widening; a landing beside the ball touched, whose far dashes widen, lengthen and fade out
        /// where the beam reaches that ball; or a strike on it, the plain line stopping there.</param>
        /// <param name="ballTouched">The centre of the ball the shot touches, for a landing or a strike; unread for open
        /// air.</param>
        /// <param name="opacity">Scales the whole beam. The Game hands over the inverse of its precise-aim blend:
        /// leaning in over the barrel the lens looks <i>along</i> the bore, so the beam foreshortens into a stack of
        /// dashes piled over the very cell it is pointing at — and it has nothing to add there, because a crosshair
        /// on a lens aimed down the shot ray already <b>is</b> the trajectory. Each mode keeps the one signal that
        /// suits it. At or below <see cref="MIN_OPACITY"/> nothing is drawn and no state is touched.</param>
        public void Draw(ICamera camera, ReadOnlySpan<Vector3> path, Vector3 srgbTint, float phase, FarEnd farEnd,
            Vector3 ballTouched, float opacity = 1f)
        {
            if (opacity <= MIN_OPACITY) return;

            int count = Lay(path, phase, farEnd, ballTouched, camera.Position, _pieces, _forks, out int forkCount);
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

            //The head fade as every Draw leaves the effect, and as the smear and the sparks, which never set it, draw with
            float headFade = 0f;

            DrawPieces(_pieces, count, opacity, ref tailWidth, ref headWidth, ref headFade);
            DrawPieces(_forks, forkCount, opacity, ref tailWidth, ref headWidth, ref headFade);

            if (headFade != 0f) _headFadeParam.SetValue(0f);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        //The pieces of one laid run, the middle line's or the fork's, each one draw of the shared quad; the widths and the
        //head fade last sent are carried between the runs, so nothing is sent twice
        private void DrawPieces(Piece[] pieces, int count, float opacity, ref float tailWidth, ref float headWidth,
            ref float headFade)
        {
            for (int i = 0; i < count; i++)
            {
                ref readonly Piece piece = ref pieces[i];

                _tailParam.SetValue(piece.Tail);
                _headParam.SetValue(piece.Head);

                //Per draw and not per construction -- see the remarks: the effect is shared with LaunchSmears and
                //LineSparks, which want widths of their own. Within this loop nothing else draws through it, so a
                //width is sent only when it changes: once for the plain line, and per dash over the far part
                if (piece.TailWidth != tailWidth) _tailWidthParam.SetValue(tailWidth = piece.TailWidth);
                if (piece.HeadWidth != headWidth) _headWidthParam.SetValue(headWidth = piece.HeadWidth);

                //The alpha at the tail, and the share of it gone by the head, which the shader lerps along: 0 on the plain
                //line, so sent only over a landing's far part. Negative where a fork's prong brightens along a dash as it
                //peels off the line (the shader lerps it up as readily)
                float fade = piece.TailAlpha > 0f ? 1f - piece.HeadAlpha / piece.TailAlpha : 0f;
                if (fade != headFade) _headFadeParam.SetValue(headFade = fade);

                _alphaParam.SetValue(piece.TailAlpha * opacity);

                _pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }

        /// <summary>
        /// Lays the beam out along <paramref name="path"/> as the pieces <see cref="Draw"/> draws, into
        /// <paramref name="pieces"/> (<see cref="MAX_PIECES"/> is always enough), and says how many, from the muzzle
        /// outwards. Pure: the same arguments give the same pieces, and nothing is allocated.
        /// </summary>
        /// <remarks>
        /// <b>Everything is measured along the whole flight</b>, from the muzzle: the dash pattern, so it crawls on
        /// through a knot, and the far part, so a beam bent by a well widens and fades once over its whole length rather
        /// than once per leg. A dash that a knot cuts is two pieces, which make one continuous slice of the line, each
        /// with the widths and the alphas its own two ends stand at.
        /// <para>
        /// <b>The flight's last leg is its last one with a length</b> (<see cref="LastLeg"/>): a stepped flight can
        /// put a knot within a thousandth of its end, and a last leg of nothing has no length for the open fade to run over.
        /// </para>
        /// </remarks>
        /// <param name="ballTouched">The centre of the ball the shot touches, for a landing or a strike.</param>
        /// <param name="viewer">Where the camera is: a landing or a strike ends where it sees the beam reach the ball
        /// touched.</param>
        internal static int Lay(ReadOnlySpan<Vector3> path, float phase, FarEnd farEnd, Vector3 ballTouched, Vector3 viewer,
            Span<Piece> pieces) => Lay(path, phase, farEnd, ballTouched, viewer, pieces, Span<Piece>.Empty, out _);

        /// <summary>
        /// <see cref="Lay(ReadOnlySpan{Vector3}, float, FarEnd, Vector3, Vector3, Span{Piece})"/>, and the fork's two outer
        /// prongs over a landing's far part into <paramref name="forks"/> (<see cref="MAX_FORK_PIECES"/> is always enough),
        /// <paramref name="forkCount"/> of them (#1235). The middle line is laid exactly as without them.
        /// </summary>
        internal static int Lay(ReadOnlySpan<Vector3> path, float phase, FarEnd farEnd, Vector3 ballTouched, Vector3 viewer,
            Span<Piece> pieces, Span<Piece> forks, out int forkCount)
        {
            forkCount = 0;
            if (path.Length < 2 || pieces.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i + 1 < path.Length; i++) total += Vector3.Distance(path[i], path[i + 1]);

            //A beam that reaches nothing is the fading line, out to its end; one that reaches a ball stops where the lens
            //sees it reach that ball, and over a landing its far dashes grow along the flight and fade out by there
            float end = total;
            FarPart far = FarPart.None;

            if (farEnd != FarEnd.OpenAir)
            {
                end = VisibleEnd(path, total, ballTouched, viewer);
                if (farEnd == FarEnd.Landing)
                    far = new FarPart(FAR_FROM * total, total, FAR_FROM * end, end, FORK_FROM * end, FORK_FULL * end);
            }

            if (end < SPACING * MIN_DASH_FRACTION) return 0;

            return LayDashes(path, end, far, LastLeg(path), phase, farEnd == FarEnd.OpenAir, pieces, viewer, forks,
                ref forkCount);
        }

        /// <summary>
        /// A landing's far part: where its dashes widen and lengthen, from <see cref="From"/> to <see cref="To"/> along the
        /// flight (<see cref="FAR_FROM"/> of it on, to the touch), and where their light fades, from <see cref="FadeFrom"/>
        /// to nothing at <see cref="FadeTo"/> (<see cref="FAR_FROM"/> of the way to where the lens sees the beam reach the
        /// ball on, to there). <see cref="None"/> is the plain line's: nothing grows and nothing fades.
        /// </summary>
        private readonly record struct FarPart(float From, float To, float FadeFrom, float FadeTo, float ForkFrom, float ForkTo)
        {
            public static FarPart None => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity,
                float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

            /// <summary>A dash's half-width at a distance along the flight.</summary>
            public float Width(float distance) => FarWidth(FarAlong(distance, From, To));

            /// <summary>The pattern's period at a distance along the flight.</summary>
            public float Period(float distance) => FarPeriod(FarAlong(distance, From, To));

            /// <summary>The share of a period lit by a dash starting at a distance along the flight.</summary>
            public float Fraction(float distance) => FarFraction(FarAlong(distance, From, To));

            /// <summary>The alpha at a distance along the flight: exactly 1 short of both the far part and its fade.</summary>
            public float Alpha(float distance) => FarAlpha(FarAlong(distance, FadeFrom, FadeTo), Width(distance));

            /// <summary>How far the fork's outer prongs have parted at a distance along the flight, 0 to 1 (#1235).</summary>
            public float Fork(float distance) => Ease(FarAlong(distance, ForkFrom, ForkTo));

            /// <summary>The alpha of a dash of the plain line's width under the fade at a distance along the flight: the
            /// fork's prongs', which never widen (#1235).</summary>
            public float ThinAlpha(float distance) => FarAlpha(FarAlong(distance, FadeFrom, FadeTo), WIDTH);
        }


        /// <summary>The last leg of <paramref name="path"/> longer than a thousandth, or the first leg when none is.</summary>
        private static int LastLeg(ReadOnlySpan<Vector3> path)
        {
            for (int leg = path.Length - 2; leg > 0; leg--)
                if (Vector3.Distance(path[leg], path[leg + 1]) > Constants.THOUSANDTH) return leg;

            return 0;
        }

        /// <summary>
        /// How far along the flight, seen from <paramref name="viewer"/>, the beam reaches the disc the ball it touches
        /// (centred on <paramref name="ball"/>) shows: the touch, <paramref name="total"/>, where the touch itself is not
        /// over that disc, as from the side; short of it where the lens sees the beam's last stretch in front of the ball,
        /// as the overview does. Walked back from the touch in steps of <see cref="SIGHT_STEP"/>, then halved down to a
        /// thousandth between the last point over the disc and the first clear of it.
        /// </summary>
        internal static float VisibleEnd(ReadOnlySpan<Vector3> path, float total, Vector3 ball, Vector3 viewer)
        {
            if (!OverBall(PointAt(path, total), ball, viewer)) return total;

            float over = total, clear = -1f;
            for (float distance = total - SIGHT_STEP; distance > 0f; distance -= SIGHT_STEP)
            {
                if (!OverBall(PointAt(path, distance), ball, viewer))
                {
                    clear = distance;
                    break;
                }

                over = distance;
            }

            //The walk stepped past the muzzle: the muzzle itself, or, over the disc too, the whole flight in front of the
            //ball, which only a lens on the line of fire sees, and that lens sees nothing of the beam anyway
            if (clear < 0f)
            {
                if (OverBall(path[0], ball, viewer)) return 0f;
                clear = 0f;
            }

            while (over - clear > Constants.THOUSANDTH)
            {
                float middle = (over + clear) * Constants.HALF;
                if (OverBall(PointAt(path, middle), ball, viewer)) over = middle;
                else clear = middle;
            }

            return clear;
        }

        //Whether a point shows over the ball's disc from the viewer: the sight line through it passes within the ball's
        //radius of its centre. In front of the ball such a point is drawn over it; behind it, it is hidden by it
        private static bool OverBall(Vector3 point, Vector3 ball, Vector3 viewer)
        {
            Vector3 sight = point - viewer;
            float length = sight.Length();
            if (length <= Constants.THOUSANDTH) return false;

            Vector3 toBall = ball - viewer;
            float along = Vector3.Dot(toBall, sight) / length;
            if (along <= 0f) return false;

            return toBall.LengthSquared() - along * along < BALL_RADIUS * BALL_RADIUS;
        }

        /// <summary>
        /// How far along the far part (or along its fade) a distance along the beam stands, from 0 at
        /// <paramref name="farFrom"/> (and everywhere on a beam with no far part, whose <paramref name="farFrom"/> is
        /// infinite) to 1 at <paramref name="end"/>, the touch (or where the beam reaches the ball); held at those two outside
        /// them.
        /// </summary>
        internal static float FarAlong(float distance, float farFrom, float end)
        {
            if (distance <= farFrom) return 0f;
            if (distance >= end) return 1f;

            return (distance - farFrom) / (end - farFrom);
        }

        /// <summary>
        /// The curve the far dashes widen and lengthen along, and their light fades along (<see cref="FarAlpha"/>), over
        /// <see cref="FarAlong"/>: a smoothstep, so it leaves the plain line with no corner and arrives at the touch with none
        /// either.
        /// </summary>
        internal static float Ease(float along) => along * along * (3f - 2f * along);

        /// <summary>A dash's half-width at a point of the far part: <see cref="WIDTH"/> widening to <see cref="FAR_WIDTH"/>.</summary>
        internal static float FarWidth(float along) => WIDTH + (FAR_WIDTH - WIDTH) * Ease(along);

        /// <summary>The pattern's period at a point of the far part: <see cref="SPACING"/> growing to <see cref="FAR_STRETCH"/> times it.</summary>
        internal static float FarPeriod(float along) => SPACING * (1f + (FAR_STRETCH - 1f) * Ease(along));

        /// <summary>The share of a period a dash starting at a point of the far part lights: <see cref="DASH_FRACTION"/> growing to <see cref="FAR_DASH_FRACTION"/>.</summary>
        internal static float FarFraction(float along) => DASH_FRACTION + (FAR_DASH_FRACTION - DASH_FRACTION) * Ease(along);

        /// <summary>
        /// A far dash's alpha at a point of the far part, <paramref name="along"/> its fade, where the dash is
        /// <paramref name="width"/> wide (half-width): 1 on the plain line, 0 where the beam reaches the ball. Its square,
        /// the light the dash adds at its core, is the fade <c>1 - </c><see cref="Ease"/> (flat where it leaves the plain
        /// line, so no corner there, and flat again as it reaches the ball, so the light dies away there with no end to see)
        /// cut by the widening, <see cref="WIDTH"/> over <paramref name="width"/>, so the light across its whole width goes
        /// as the fade alone: never over the line's, and gone at the ball. It falls early enough that a dash ending a gap
        /// short of the ball, all the crawl can leave there at times, ends at next to nothing.
        /// </summary>
        internal static float FarAlpha(float along, float width) => MathF.Sqrt((1f - Ease(along)) * WIDTH / width);

        /// <summary>
        /// The distance along the flight <paramref name="periods"/> of the dash pattern past <paramref name="distance"/>:
        /// <see cref="SPACING"/> a period short of the far part, by exactly the plain line's arithmetic, and
        /// <see cref="FarPeriod"/> a period over it, integrated in steps of <see cref="MARCH"/>.
        /// </summary>
        private static float Advance(float distance, float periods, in FarPart far)
        {
            float steady = periods * SPACING;
            if (distance + steady <= far.From) return distance + steady;

            if (distance < far.From)
            {
                periods -= (far.From - distance) / SPACING;
                distance = far.From;
            }

            while (periods > 0f)
            {
                //Past the touch the period is the far one, and stays it
                if (distance >= far.To) return distance + periods * FarPeriod(1f);

                float step = MathF.Min(MARCH, far.To - distance);
                float period = far.Period(distance + step * Constants.HALF);
                if (periods * period <= step) return distance + periods * period;

                distance += step;
                periods -= step / period;
            }

            return distance;
        }

        /// <summary>
        /// The dashed line from the muzzle to <paramref name="end"/> along the flight (the touch, or where the beam reaches
        /// the ball touched): the crawl along the whole flight, a landing's far part (<paramref name="far"/>), and the
        /// open fade over the last leg of a beam that reaches nothing.
        /// </summary>
        private static int LayDashes(ReadOnlySpan<Vector3> path, float end, in FarPart far, int finalLeg, float phase,
            bool openEnded, Span<Piece> pieces, Vector3 viewer, Span<Piece> forks, ref int forkCount)
        {
            //The open fade runs over the last leg, as it did when each leg was drawn on its own: a bank shot that
            //reaches nothing keeps its first leg whole and dies away after the bounce
            float lastLegStart = 0f;
            for (int i = 0; i < finalLeg; i++) lastLegStart += Vector3.Distance(path[i], path[i + 1]);
            float lastLeg = Vector3.Distance(path[finalLeg], path[finalLeg + 1]);

            int count = 0, dashes = 0;

            //The leg the walk is on, and how far along the flight it starts. Dashes only move forward, so this
            //only ever advances
            int leg = 0;
            float legStart = 0f, legLength = Vector3.Distance(path[0], path[1]);

            //The pattern's origin walks towards the target and wraps every period, so the dashes appear to
            //travel without any one of them being tracked. Starting one period behind the muzzle is what lets a
            //dash be clipped INTO existence at the near end rather than popping in whole; and since every dash is laid
            //on from there, the far dashes, whose period grows, wrap with the near ones.
            float origin = (phase * CRAWL) % SPACING - SPACING;

            for (float start = origin; start < end && dashes < MAX_DASHES;)
            {
                //The dash whole, and clipped at the muzzle and at the end
                float length = Advance(start, far.Fraction(start), far) - start;
                float from = MathF.Max(start, 0f), to = MathF.Min(start + length, end);
                start = Advance(start, 1f, far);

                //A landing's alpha at the dash's tail, its brightest point, the fade only ever falling (1 short of the far
                //part, and all along any other beam). A clipped sliver is skipped as much shorter as it is fainter: it
                //flickers only as brightly as it is lit, and the dash crawling into the ball, all but faded out, is kept
                //until it is gone rather than leaving its last stretch empty
                float tailAlpha = openEnded ? 1f : far.Alpha(from);
                if (to - from < length * MIN_DASH_FRACTION * tailAlpha * tailAlpha) continue;

                dashes++;

                //An open beam's fade holds one alpha a dash; a landing's (and the plain line's 1) is taken at each end of
                //every piece below
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
                else if (tailAlpha <= MIN_OPACITY) continue;

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
                        Vector3 tail = PointOnLeg(path, at, atStart, atLength, a), head = PointOnLeg(path, at, atStart, atLength, b);
                        pieces[count++] = new Piece(tail, head, far.Width(a), far.Width(b),
                            openEnded ? alpha : far.Alpha(a), openEnded ? alpha : far.Alpha(b));

                        //The fork's outer prongs beside it, over a landing's far part (#1235)
                        if (!openEnded && forkCount + 2 <= forks.Length)
                            forkCount = LayProngs(path, at, tail, head, a, b, far, viewer, forks, forkCount);
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
        /// The fork's two outer prongs beside one piece of the middle line, from <paramref name="a"/> to <paramref name="b"/>
        /// along the flight on leg <paramref name="leg"/> (#1235): the owner's sketch, the line opening at its end into a
        /// thin three-pronged fork that fades out, "so that it is a little visible that the ball does not land exactly where
        /// the line ends". Each prong is the piece moved aside by how far the fork has opened there (<see cref="FORK_FROM"/> to
        /// <see cref="FORK_FULL"/>) of <see cref="FORK_SPREAD"/>, square to
        /// the flight and to the lens's line of sight (<see cref="Across"/>), so the two part on screen on either side of the
        /// line whatever the angle; at the line's own <see cref="WIDTH"/>, never widened; and with the plain line's alpha under
        /// the same fade, times <see cref="FORK_LIGHT"/>, gained as they leave it (<see cref="FORK_SHOW"/>), so they peel off it
        /// from nothing and fade out with it at the ball. Laid only where they have parted at all: the plain line, a strike and an open beam have none.
        /// </summary>
        private static int LayProngs(ReadOnlySpan<Vector3> path, int leg, Vector3 tail, Vector3 head, float a, float b,
            in FarPart far, Vector3 viewer, Span<Piece> forks, int forkCount)
        {
            float openA = far.Fork(a), openB = far.Fork(b);
            if (openB <= 0f) return forkCount;

            float alphaA = far.ThinAlpha(a) * Ease(MathF.Min(1f, openA / FORK_SHOW)) * FORK_LIGHT;
            float alphaB = far.ThinAlpha(b) * Ease(MathF.Min(1f, openB / FORK_SHOW)) * FORK_LIGHT;
            if (MathF.Max(alphaA, alphaB) <= MIN_OPACITY) return forkCount;

            //A tail at nothing would leave the shader no alpha to lerp from: a fiftieth of the head's, all but nothing
            alphaA = MathF.Max(alphaA, alphaB * 0.02f);

            Vector3 flight = path[leg + 1] - path[leg];
            Vector3 asideA = Across(flight, tail - viewer) * (FORK_SPREAD * openA);
            Vector3 asideB = Across(flight, head - viewer) * (FORK_SPREAD * openB);

            forks[forkCount++] = new Piece(tail + asideA, head + asideB, WIDTH, WIDTH, alphaA, alphaB);
            forks[forkCount++] = new Piece(tail - asideA, head - asideB, WIDTH, WIDTH, alphaA, alphaB);
            return forkCount;
        }

        /// <summary>
        /// The unit direction square to <paramref name="flight"/> and to the lens's line of <paramref name="sight"/>, the one
        /// a step aside shows longest on screen (the fan of #1196 parted its rays the same way). Seen straight along the
        /// flight there is none: square to the flight and up then, or to the flight and across the world.
        /// </summary>
        internal static Vector3 Across(Vector3 flight, Vector3 sight)
        {
            Vector3 across = Vector3.Cross(flight, sight);
            if (across.LengthSquared() < 1e-8f) across = Vector3.Cross(flight, Vector3.Up);
            if (across.LengthSquared() < 1e-8f) across = Vector3.Cross(flight, Vector3.Right);
            return Vector3.Normalize(across);
        }

        //A point at a distance along the flight, on the leg that covers it; a leg of nothing is its one point
        private static Vector3 PointOnLeg(ReadOnlySpan<Vector3> path, int leg, float legStart, float legLength, float distance) =>
            legLength > Constants.THOUSANDTH
                ? Vector3.Lerp(path[leg], path[leg + 1], (distance - legStart) / legLength)
                : path[leg];

        //A point at a distance along the flight, the leg found by walking from the muzzle (VisibleEnd asks for a few dozen
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
