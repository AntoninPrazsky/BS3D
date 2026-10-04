using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;
using System.Threading.Tasks;

namespace BS3D.Screens
{
    /// <summary>
    /// What the online client is doing, as a picture (#683, #723): a wire globe in front of the boards plate's status
    /// line. A still "Sending your score..." read as a stuck line - the owner's note was that it should visibly move, "so
    /// you can see the sending is really happening" - and the first picture, a phone's signal-strength bars, read as being
    /// about to phone somebody (#723). The globe is a circle, an equator and three meridians whose widths follow the
    /// rotation: a meridian is a great circle, which seen from the side is an ellipse as tall as the globe and as wide as
    /// the sine of its longitude from the viewer, so as the globe turns the ellipses open and close and the next one
    /// takes their place.
    /// <list type="bullet">
    /// <item><see cref="SignalMode.Working"/> - a request is in flight (the clear on its way, or the boards loading):
    /// the globe turns, in the menu's own text colour.</item>
    /// <item><see cref="SignalMode.Done"/> - the service took the clear: the globe settles, in the colour of the player's own
    /// row on the boards (<see cref="BS3DGame.BoardYouColor"/>): their score is on the board now, said in the colour that
    /// says "you" everywhere on the plate (#739).</item>
    /// <item><see cref="SignalMode.Idle"/> - offline or refused: the globe dim and still, because nothing is happening
    /// and a moving mark would promise that something is.</item>
    /// </list>
    /// <para>
    /// <b>Baked once, drawn as an image.</b> The widget only has rectangles to draw with, and a wire at this size wants
    /// anti-aliasing, so one strip of <see cref="FRAMES"/> frames is rasterised on the CPU the first time a size is asked
    /// for (the strip holds exactly one period: three meridians 60 degrees apart look the same again after 60 degrees)
    /// and kept until the size changes, as the page's own tree is rebuilt then. A frame is a quad drawn one to one from
    /// the strip, in premultiplied white tinted by the mode, so a turn costs nothing a frame beyond the draw and no frame
    /// ever needs a texture upload. No land and no sea, and no asset: the owner asked for the wire alone.
    /// </para>
    /// </summary>
    internal sealed class OnlineSignal : Widget
    {
        internal enum SignalMode : byte
        {
            Working,
            Done,
            Idle,
        }

        /// <summary>Frames in the strip, one period of the turn: 60 degrees in 3 degree steps, which turns without a visible step.</summary>
        private const int FRAMES = 20;

        private const int MERIDIANS = 3;

        //The frame a still globe shows: the middle of the period, where the meridians stand at 30, 90 and 150 degrees and so
        //open as two half-width ellipses inside the outline - the globe at its plainest. Frame 0 has one meridian edge-on in
        //the middle, which stands still as a crosshair and not as a globe.
        private const int REST_FRAME = FRAMES / 2;

        //One period of the strip, in seconds: a full turn is MERIDIANS * 2 of these, brisk enough to read as activity and
        //slow enough that the ellipses are followed
        private const float LOOP_SECONDS = 0.6f;

        //The wire's width in pixels at the size it is baked for: about a twentieth of the globe, never under a pixel and
        //a third, which is what still reads as a line on a bright backdrop at 1600x900
        private const float WIRE_SHARE = 1f / 20f;
        private const float WIRE_MIN = 1.35f;

        //Points of each ellipse polyline the baker measures distances to: a polygon of this many sides is off a circle of
        //thirty pixels' radius by about a fifteenth of a pixel, and the bake costs about ten milliseconds once, on the frame
        //the result page's tree is built
        private const int ELLIPSE_POINTS = 48;

        //What the dim, still globe keeps of the dim text colour
        private const float IDLE_SHARE = 0.85f;

        private static Texture2D _strip;
        private static int _stripSize;

        private float _clock;

        public OnlineSignal(int height)
        {
            Height = height;
            Width = height;
            VerticalAlignment = VerticalAlignment.Center;

            EnsureStrip(Math.Max(8, height));
        }

        internal SignalMode Mode { get; private set; } = SignalMode.Idle;

        /// <summary>Sets the mode; a new <see cref="SignalMode.Working"/> starts its turn from the first frame.</summary>
        internal void Show(SignalMode mode)
        {
            if (mode == Mode) return;

            Mode = mode;
            _clock = 0f;
        }

        /// <summary>The page's clock, once a frame while the plate is up.</summary>
        internal void Advance(float elapsed) => _clock += elapsed;

        public override void InternalRender(RenderContext context)
        {
            Rectangle bounds = ActualBounds;

            //Only a turning globe moves; the others stand on the rest frame
            int frame = Mode == SignalMode.Working ? (int)(_clock / LOOP_SECONDS * FRAMES) % FRAMES : REST_FRAME;

            Color tint = Mode switch
            {
                SignalMode.Done => BS3DGame.BoardYouColor,
                SignalMode.Working => BS3DGame.MENU_TEXT,
                _ => BS3DGame.MENU_TEXT_DIM * IDLE_SHARE,
            };

            //One to one, from the strip's frame into a rectangle of the same size: no resampling, so the wire keeps the
            //width it was baked at, centred in the widget's own bounds
            Rectangle source = new(frame * _stripSize, 0, _stripSize, _stripSize);
            Rectangle target = new(bounds.X + (bounds.Width - _stripSize) / 2, bounds.Y + (bounds.Height - _stripSize) / 2,
                _stripSize, _stripSize);

            context.Draw(_strip, target, source, tint);
        }

        /// <summary>Makes the strip for <paramref name="size"/> pixels if it is not the one already held.</summary>
        private static void EnsureStrip(int size)
        {
            if (_strip != null && _stripSize == size && !_strip.IsDisposed) return;

            _strip?.Dispose();
            _strip = Bake(size);
            _stripSize = size;
        }

        /// <summary>
        /// The strip: <see cref="FRAMES"/> square frames side by side, each the globe at one step of the turn, as premultiplied
        /// white (the coverage in all four channels) so a tint multiplies it into a premultiplied colour. A pixel's coverage
        /// is how far inside the wire's half width its centre is, to the nearest point of any line of the frame - exact for
        /// the straight and the edge-on cases that a formula for an ellipse's distance gets wrong, and cheap at this size.
        /// </summary>
        private static Texture2D Bake(int size)
        {
            float wire = MathF.Max(WIRE_MIN, size * WIRE_SHARE);
            float half = wire * 0.5f;

            //A pixel of margin all round, so a frame never bleeds into the next one when it is sampled
            float radius = size * 0.5f - half - 1f;

            Color[] pixels = new Color[FRAMES * size * size];

            //The globe's lines as segments, rebuilt per frame: the circle and the equator never change, the meridians do
            int segmentCount = ELLIPSE_POINTS + 1 + MERIDIANS * ELLIPSE_POINTS;

            //The frames are independent and write columns of their own, so they are baked side by side: a hundred
            //milliseconds on one core is a hitch on the frame the result page opens on, and a tenth of that is not
            Parallel.For(0, FRAMES, frame =>
            {
                Vector2[] from = new Vector2[segmentCount];
                Vector2[] to = new Vector2[segmentCount];
                int n = 0;

                //The outline
                for (int i = 0; i < ELLIPSE_POINTS; i++)
                {
                    from[n] = Ellipse(radius, radius, i * MathHelper.TwoPi / ELLIPSE_POINTS);
                    to[n] = Ellipse(radius, radius, (i + 1) * MathHelper.TwoPi / ELLIPSE_POINTS);
                    n++;
                }

                //The equator, edge-on
                from[n] = new Vector2(-radius, 0f);
                to[n] = new Vector2(radius, 0f);
                n++;

                //The meridians, half a turn shared between them
                float step = MathHelper.Pi / MERIDIANS;

                for (int m = 0; m < MERIDIANS; m++)
                {
                    float longitude = frame * step / FRAMES + m * step;
                    float across = radius * MathF.Abs(MathF.Sin(longitude));

                    for (int i = 0; i < ELLIPSE_POINTS; i++)
                    {
                        from[n] = Ellipse(across, radius, i * MathHelper.TwoPi / ELLIPSE_POINTS);
                        to[n] = Ellipse(across, radius, (i + 1) * MathHelper.TwoPi / ELLIPSE_POINTS);
                        n++;
                    }
                }

                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 at = new(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);

                        float nearest = float.MaxValue;
                        for (int s = 0; s < n; s++)
                            nearest = MathF.Min(nearest, DistanceToSegment(at, from[s], to[s]));

                        float coverage = MathHelper.Clamp(half + 0.5f - nearest, 0f, 1f);
                        byte c = (byte)(coverage * 255f + 0.5f);

                        pixels[y * FRAMES * size + frame * size + x] = new Color(c, c, c, c);
                    }
            });

            Texture2D strip = new(MyraEnvironment.GraphicsDevice, FRAMES * size, size);
            strip.SetData(pixels);
            return strip;
        }

        private static Vector2 Ellipse(float across, float down, float angle) =>
            new(across * MathF.Cos(angle), down * MathF.Sin(angle));

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.LengthSquared();
            float t = lengthSquared <= 0f ? 0f : MathHelper.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
