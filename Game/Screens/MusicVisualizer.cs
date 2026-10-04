using BS3D.Audio;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Prazsky.Core.Tools;
using System;
using System.Diagnostics;

namespace BS3D.Screens
{
    /// <summary>
    /// The About page's spectrum (#443, #730): one column of LED segments per <see cref="ProceduralJukebox"/> band,
    /// standing on a common baseline, with a <b>peak cap</b> over each that holds for a moment and then falls back onto
    /// the column — the spectrum display of an old radio or a hi-fi receiver, drawn straight into Myra's render pass so it
    /// lays out in the page's column like any other widget. It reads the bands the jukebox computed in its own update and
    /// allocates nothing, since it draws every frame the page is up.
    /// <para>
    /// <b>Colour is the owner's ruling here, against the menu's own rule (#730).</b> The menu marks state by brightness,
    /// never by hue (<c>BS3DGame.Menu.cs</c>), and this widget was grey for that reason. He asked for it colourful, "like
    /// the colourful analogue visualisers on old radios", so a segment is lit in the colour of the height it stands at
    /// (<see cref="VisualizerPalette"/>): the green-to-yellow-to-red stack of an LED meter, or one hue a band across the
    /// spectrum. It is an instrument, not menu chrome, and the exception is stated here and in <c>docs/game-shell.md</c> so
    /// the rule does not read as broken. The palette is a property so the Jukebox page (#704) can set its own; the About page
    /// shows <see cref="VisualizerPalette.Spectrum"/> on the game's birthday only (#764).
    /// </para>
    /// <para>
    /// <b>A column is segments, and a segment fades rather than switches.</b> Its brightness is how far the level has
    /// reached into it, so the top segment of a rising column swells instead of popping and the display moves smoothly
    /// at any frame rate; the segments above the level stay as dim glass, so a silent display still reads as an
    /// instrument. <b>The caps fall under gravity, in seconds and not frames</b>: a cap rises with its column, holds for
    /// <see cref="HOLD_SECONDS"/>, then accelerates down at <see cref="GRAVITY"/> (column heights a second squared) until
    /// it meets the column again, and the clock is the wall clock read inside the draw, capped so a page that was not
    /// drawn for a while does not let them fall through the floor in one step. With the music paused the bands drop to
    /// nothing and the caps fall onto the stubs and rest there.
    /// </para>
    /// </summary>
    internal sealed class MusicVisualizer : Widget
    {
        /// <summary>How the segments of a column are coloured.</summary>
        internal enum VisualizerPalette : byte
        {
            /// <summary>Green low in the column, then yellow, orange and a red top: the LED meter of a radio.</summary>
            Led,

            /// <summary>One hue per band across the spectrum, low bands red to high bands violet, all through the column.</summary>
            Spectrum,
        }

        //A floor under every column, so a silent display still reads as an instrument rather than as a gap
        private const float STUB = 0.035f;

        //Segments in a column, and the share of a segment's pitch that is the dark gap between two
        private const int SEGMENTS = 18;
        private const float SEGMENT_GAP_SHARE = 0.22f;

        //What an unlit segment keeps of its colour: dim glass, there to say where the lit ones will be
        private const float UNLIT = 0.11f;

        //The peak cap: held this long after it was last pushed up, then dropped from rest at this acceleration, in column
        //heights a second squared - so a cap crossing the whole column falls in about 0.6 s, which reads as dropped and not
        //as slid
        private const float HOLD_SECONDS = 0.33f;
        private const float GRAVITY = 5.5f;

        //How thick a cap is, as a share of a segment's height, and the dark line left between it and the column's top
        private const float CAP_THICKNESS = 0.55f;
        private const int CAP_GAP = 2;

        //The longest step the caps are advanced by: a page not drawn for a while (another screen over it, a minimised
        //window) must not hand them one enormous step
        private const float MAX_STEP_SECONDS = 0.1f;

        //The LED palette's four bands, by height up the column, and its colours. Hard steps, as LED meters have them; the
        //smooth part is each segment's own brightness. The shares are read off the references drawn for the issue (a hi-fi
        //receiver's columns, a mixing desk's meters, both models): green low, yellow in the middle, and a long orange and red
        //top - about a third of the column is the hot end, which is what makes a loud passage read as loud
        private const float LED_YELLOW_FROM = 0.40f;
        private const float LED_ORANGE_FROM = 0.62f;
        private const float LED_RED_FROM = 0.78f;
        private static readonly Color LED_GREEN = new(56, 226, 108);
        private static readonly Color LED_YELLOW = new(246, 224, 70);
        private static readonly Color LED_ORANGE = new(255, 150, 40);
        private static readonly Color LED_RED = new(244, 62, 54);

        //The glow round a lit segment, which the references all have and a flat rectangle does not: a slightly larger,
        //faint copy under it, as a share of the segment's pitch and of its colour
        private const float HALO_SIZE = 0.14f;
        private const float HALO_STRENGTH = 0.20f;

        //How much of a cap's colour is white, so it reads as the brightest thing in its column
        private const float CAP_WHITE = 0.55f;

        private readonly ProceduralJukebox _jukebox;
        private readonly int _gap;

        //The caps (Prazsky.Core.Tools.PeakHold): made once, and stepped from the draw because the widget has no Update
        private readonly PeakHold _caps;
        private long _lastTicks;

        /// <summary>How the segments are coloured; the Jukebox page sets its own (#704).</summary>
        public VisualizerPalette Palette { get; set; } = VisualizerPalette.Led;

        public MusicVisualizer(ProceduralJukebox jukebox, int width, int height, int gap)
        {
            _jukebox = jukebox;
            _gap = gap;

            _caps = new PeakHold(jukebox.Bands.Length, HOLD_SECONDS, GRAVITY);

            Width = width;
            Height = height;
        }

        public override void InternalRender(RenderContext context)
        {
            ReadOnlySpan<float> bands = _jukebox.Bands;
            Rectangle bounds = ActualBounds;

            Step(bands);

            int count = bands.Length;
            int barWidth = Math.Max(1, (bounds.Width - _gap * (count - 1)) / count);
            int used = barWidth * count + _gap * (count - 1);
            int x = bounds.X + (bounds.Width - used) / 2;

            float pitch = bounds.Height / (float)SEGMENTS;
            float segmentGap = MathF.Max(1f, pitch * SEGMENT_GAP_SHARE);
            int capHeight = Math.Max(2, (int)MathF.Round((pitch - segmentGap) * CAP_THICKNESS));

            for (int b = 0; b < count; b++)
            {
                float level = MathF.Max(STUB, bands[b]);

                for (int s = 0; s < SEGMENTS; s++)
                {
                    //How far the level has reached into this segment: a whole one below it, none above, and the top one lit
                    //in proportion, so a rising column swells instead of stepping
                    float lit = Math.Clamp(level * SEGMENTS - s, 0f, 1f);

                    Color colour = SegmentColour(b, count, (s + 0.5f) / SEGMENTS);
                    float brightness = UNLIT + (1f - UNLIT) * lit;

                    int top = bounds.Bottom - (int)MathF.Round((s + 1) * pitch - segmentGap);
                    int bottom = bounds.Bottom - (int)MathF.Round(s * pitch);
                    Rectangle segment = new(x, top, barWidth, Math.Max(1, bottom - top));

                    if (lit > 0f)
                    {
                        int halo = Math.Max(1, (int)MathF.Round(pitch * HALO_SIZE));
                        FlatBrush.Fill(context, new Rectangle(segment.X - halo, segment.Y - halo, segment.Width + 2 * halo, segment.Height + 2 * halo),
                            colour * (HALO_STRENGTH * lit));
                    }

                    FlatBrush.Fill(context, segment, colour * brightness);
                }

                //The cap: its own height, a little above the column's top, thin, and nearly white in the colour of the
                //height it hangs at - drawn only when it has left the column, so a cap resting on a column is the column
                float peak = MathF.Max(_caps[b], STUB);
                if (peak > level + 0.5f / SEGMENTS)
                {
                    int capBottom = bounds.Bottom - (int)MathF.Round(peak * bounds.Height) - CAP_GAP;
                    Color capColour = Color.Lerp(SegmentColour(b, count, peak), Color.White, CAP_WHITE);

                    FlatBrush.Fill(context, new Rectangle(x, capBottom - capHeight, barWidth, capHeight), capColour);
                }

                x += barWidth + _gap;
            }
        }

        /// <summary>
        /// Advances every cap by the wall-clock time since the last draw: up with its column, held, then falling
        /// (<see cref="HOLD_SECONDS"/>, <see cref="GRAVITY"/>). In seconds, so the fall is the same at 60 and at 240 Hz.
        /// </summary>
        private void Step(ReadOnlySpan<float> bands)
        {
            long now = Stopwatch.GetTimestamp();
            float elapsed = _lastTicks == 0 ? 0f : MathF.Min((float)Stopwatch.GetElapsedTime(_lastTicks, now).TotalSeconds, MAX_STEP_SECONDS);
            _lastTicks = now;

            _caps.Step(bands, elapsed);
        }

        /// <summary>The colour a segment <paramref name="height"/> of the way up the column of band <paramref name="band"/> is lit in.</summary>
        private Color SegmentColour(int band, int bands, float height)
        {
            if (Palette == VisualizerPalette.Spectrum)
            {
                //Red at the low end round to violet at the high one - 300 degrees, so the last band is not the first again
                return Hue(300f * band / MathF.Max(1, bands - 1));
            }

            return height >= LED_RED_FROM ? LED_RED
                : height >= LED_ORANGE_FROM ? LED_ORANGE
                : height >= LED_YELLOW_FROM ? LED_YELLOW
                : LED_GREEN;
        }

        /// <summary>A fully saturated colour at <paramref name="degrees"/> round the wheel, red at zero.</summary>
        private static Color Hue(float degrees)
        {
            float sector = degrees / 60f;
            int i = (int)MathF.Floor(sector) % 6;
            float f = sector - MathF.Floor(sector);

            (float r, float g, float b) = i switch
            {
                0 => (1f, f, 0f),
                1 => (1f - f, 1f, 0f),
                2 => (0f, 1f, f),
                3 => (0f, 1f - f, 1f),
                4 => (f, 0f, 1f),
                _ => (1f, 0f, 1f - f),
            };

            return new Color(r, g, b);
        }
    }
}
