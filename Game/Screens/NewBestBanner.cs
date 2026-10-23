using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The "NEW BEST" of the result page, drawn as the moment it is</b> (#926): the word struck onto the page with a bounce, filled with a
    /// slowly flowing pastel rainbow, ringed by a dark outline, with sparkles thrown off it as it lands and glinting on it after. What it
    /// does and when is <see cref="NewBestCelebration"/>'s, pure and tested; this draws it.
    /// <para>
    /// <b>It stands where a widget reserves its place and is drawn over the page</b> (<see cref="MenuPage.DrawOverUi"/>), not by Myra: a Myra
    /// label draws a string in one colour, and the bounce overshoots a neighbouring line on purpose. The tree holds an empty widget of exactly the
    /// word's size (<see cref="SlotWidth"/>, <see cref="SlotHeight"/>), so the layout under it is the finished one from the first frame and
    /// nothing shuffles when the word lands; the banner is drawn about that widget's centre.
    /// </para>
    /// <para>
    /// <b>The fill is per-vertex colour</b> (<see cref="GradientTextRenderer"/>): the text goes through FontStashSharp's own renderer
    /// interface and each glyph is a quad whose corners take the rainbow where they are. No shader, so Potato and GamePi draw it as they draw the
    /// rest of the page. <b>The outline is the word itself drawn round a ring</b> under the fill (<c>OUTLINE_RING</c>, #986), the page's
    /// own dark backing in kind (<c>ShadowedLabel</c>, #521) at the banner's size, because pastel type over a bright sky needs it as white type does.
    /// </para>
    /// <para>
    /// A frame allocates nothing: the renderer and the sparkle textures are built once, the sparkles are functions of their index and the clock,
    /// and the text is a string held since the tree was built.
    /// </para>
    /// </summary>
    internal sealed class NewBestBanner
    {
        /// <summary>
        /// The word's size at rest, in design units (the menu's 2160 scale): 200 against the breakdown's body 80, so it stands two and a half
        /// times the height of the lines around it, and over the page's own title (170), because the owner's standing note is that
        /// celebrations are built too discreetly. The overshoot of the bounce takes it to about 1.4 times that, over the lines above and below
        /// it, for a moment.
        /// </summary>
        public const int FONT_DESIGN = 200;

        /// <summary>The outline's width, in design units: twice the page's own backing (3), for a word this size.</summary>
        public const int STROKE_DESIGN = 7;

        /// <summary>Air above and below the capitals in the slot, in design units: a stroke's worth and a little.</summary>
        public const int PAD_DESIGN = 16;

        //The sparkle textures' edge in pixels: drawn at most about the word's height, so this is only ever shrunk on a normal window
        private const int SPARK_SIZE = 64;

        //The dark outline, premultiplied: nearly black and nearly opaque, because the fill is light and a bright sky is what it stands on
        private static readonly Color OUTLINE = new(0, 0, 0, 225);

        //The directions the outline's copies are drawn in (#986), unit vectors round the circle: twelve, so the outline's outer edge between
        //two of them falls short of the circle by under a twelfth of the stroke (1 - cos 15 degrees), a fraction of a pixel at any size
        private const int OUTLINE_SAMPLES = 12;
        private static readonly Vector2[] OUTLINE_RING = BuildRing(OUTLINE_SAMPLES);

        private static Vector2[] BuildRing(int samples)
        {
            Vector2[] ring = new Vector2[samples];
            for (int i = 0; i < samples; i++)
            {
                float angle = MathHelper.TwoPi * i / samples;
                ring[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            return ring;
        }

        //Under each sparkle, so a white sparkle is not lost on a white cloud: premultiplied black
        private static readonly Color HALO = new(0, 0, 0, 255);

        private static GradientTextRenderer _renderer;
        private static Texture2D _spark, _glow;

        private readonly BS3DGame _game;
        private readonly SpriteFontBase _font;
        private readonly string _text;
        private readonly int _stroke;
        private readonly Vector2 _inkCentre;
        private readonly float _inkWidth, _inkHeight;

        /// <param name="game">The host, for its device.</param>
        /// <param name="text">The word.</param>
        /// <param name="font">The face at the word's resting size.</param>
        /// <param name="stroke">The outline's width in pixels.</param>
        /// <param name="pad">Air above and below the capitals in the slot, in pixels.</param>
        public NewBestBanner(BS3DGame game, string text, SpriteFontBase font, int stroke, int pad)
        {
            _game = game;
            _font = font;
            _text = text;
            _stroke = stroke;

            //The ink of the word itself, the capitals: the slot and the bounce are about its middle and not the line box's
            Bounds ink = font.TextBounds(text, Vector2.Zero);
            _inkWidth = ink.X2 - ink.X;
            _inkHeight = ink.Y2 - ink.Y;

            //Whole pixels, so the word at rest stands on the pixel grid and is as crisp as its face
            _inkCentre = new Vector2(MathF.Round((ink.X + ink.X2) * 0.5f), MathF.Round((ink.Y + ink.Y2) * 0.5f));

            SlotWidth = (int)MathF.Ceiling(_inkWidth) + 2 * stroke;
            SlotHeight = (int)MathF.Ceiling(_inkHeight) + 2 * pad;
        }

        /// <summary>The width of the place the word reserves in the page's layout, in pixels.</summary>
        public int SlotWidth { get; }

        /// <summary>The height of the place the word reserves in the page's layout, in pixels.</summary>
        public int SlotHeight { get; }

        /// <summary>
        /// Draws the word, and its sparkles, about the middle of <paramref name="slot"/> (in the frame's pixels), <paramref name="age"/> seconds
        /// after it struck; nothing before. <paramref name="clock"/> is the page's own seconds, which moves the rainbow and the gloss.
        /// </summary>
        public void Draw(SpriteBatch batch, Rectangle slot, float age, float clock)
        {
            float scale = NewBestCelebration.Scale(age);
            float alpha = NewBestCelebration.Alpha(age);
            if (alpha <= 0f || scale <= 0f) return;

            EnsureResources();

            //Rounded, so the word at rest (a scale of one) lands on whole pixels
            Vector2 centre = new(MathF.Round(slot.X + slot.Width * 0.5f), MathF.Round(slot.Y + slot.Height * 0.5f));
            Vector2 size = new(scale, scale);
            Color opacity = Color.White * alpha;

            _renderer.Begin();

            //The outline under the fill, then the fill. The outline is the plain word drawn OUTLINE_SAMPLES times round a circle of the
            //stroke's radius - a round stroke, every corner whole. FontStashSharp's own Stroked effect, which drew it until #986, left a
            //stroke-sized square empty at every outer corner of every letter (photographed at 1600x900: the owner's "squares cut out of
            //the black corners"). The copies overlap, so the outline comes out nearer opaque than OUTLINE's own alpha, which only shows
            //in the fade-in's few frames
            _renderer.Flat(OUTLINE);
            for (int i = 0; i < OUTLINE_RING.Length; i++)
                _font.DrawText(_renderer, _text, centre + OUTLINE_RING[i] * (_stroke * scale), opacity, 0f, _inkCentre, size, 0f, 0f, 0f,
                    TextStyle.None);

            _renderer.Rainbow(centre.X - _inkWidth * scale * 0.5f, centre.Y - _inkHeight * scale * 0.5f,
                _inkWidth * scale, _inkHeight * scale, clock);
            _font.DrawText(_renderer, _text, centre, opacity, 0f, _inkCentre, size, 0f, 0f, 0f, TextStyle.None);

            _renderer.End();

            DrawSparkles(batch, centre, age, clock);
        }

        private void DrawSparkles(SpriteBatch batch, Vector2 centre, float age, float clock)
        {
            float aspect = _inkWidth / _inkHeight;
            bool open = false;

            for (int i = 0; i < NewBestCelebration.BURST_COUNT; i++)
                open = DrawSparkle(batch, centre, NewBestCelebration.Burst(i, age, aspect), aspect, clock, open);

            for (int i = 0; i < NewBestCelebration.GLINT_COUNT; i++)
                open = DrawSparkle(batch, centre, NewBestCelebration.Glint(i, age, aspect), aspect, clock, open);

            if (open) batch.End();
        }

        //One sparkle: a dark halo, a four-point star in the rainbow's colour at that place on the word, a smaller one turned half a quarter
        //behind it, and a white core. The batch is begun on the first sparkle there is, so a frame with none draws nothing
        private bool DrawSparkle(SpriteBatch batch, Vector2 centre, NewBestCelebration.Sparkle sparkle, float aspect, float clock, bool open)
        {
            float strength = sparkle.Strength;
            if (strength < 0.01f) return open;

            if (!open) batch.Begin();

            Vector2 at = new(centre.X + sparkle.X * _inkHeight, centre.Y + sparkle.Y * _inkHeight);
            Vector2 origin = new(SPARK_SIZE * 0.5f, SPARK_SIZE * 0.5f);
            float fit = sparkle.Size * _inkHeight / SPARK_SIZE;

            //Where along the word it is, so a sparkle is the colour of the letters it is over
            Vector3 hue = Vector3.Lerp(NewBestCelebration.Fill(0.5f + sparkle.X / aspect, 0.5f + sparkle.Y, clock), Vector3.One, 0.4f);
            Color tint = new Color(hue.X, hue.Y, hue.Z) * strength;

            batch.Draw(_glow, at, null, HALO * (0.2f * strength), 0f, origin, fit * 1.5f, SpriteEffects.None, 0f);
            batch.Draw(_spark, at, null, tint, sparkle.Spin, origin, fit, SpriteEffects.None, 0f);
            batch.Draw(_spark, at, null, tint, sparkle.Spin + MathHelper.PiOver4, origin, fit * 0.6f, SpriteEffects.None, 0f);
            batch.Draw(_spark, at, null, Color.White * strength, sparkle.Spin, origin, fit * 0.55f, SpriteEffects.None, 0f);

            return true;
        }

        //The renderer and the two textures, built on the first frame the word is drawn and kept: a device-bound resource is not made per page
        private void EnsureResources()
        {
            GraphicsDevice device = _game.GraphicsDevice;

            if (_renderer == null || !_renderer.IsUsable || !ReferenceEquals(_renderer.GraphicsDevice, device))
                _renderer = new GradientTextRenderer(device);

            if (_spark == null || _spark.IsDisposed || _glow == null || _glow.IsDisposed)
            {
                _spark?.Dispose();
                _glow?.Dispose();
                _spark = Bake(device, star: true);
                _glow = Bake(device, star: false);
            }
        }

        /// <summary>
        /// A square of premultiplied white (the coverage in all four channels, so a tint multiplies it into a premultiplied colour). The star is
        /// four thin arms and a bright core, each falling off from the middle: no hard edge anywhere, and exactly nothing at the border. The glow
        /// is a plain soft disc.
        /// </summary>
        private static Texture2D Bake(GraphicsDevice device, bool star)
        {
            Texture2D texture = new(device, SPARK_SIZE, SPARK_SIZE);
            Color[] pixels = new Color[SPARK_SIZE * SPARK_SIZE];

            for (int y = 0; y < SPARK_SIZE; y++)
                for (int x = 0; x < SPARK_SIZE; x++)
                {
                    float u = Math.Abs((x + 0.5f) / SPARK_SIZE * 2f - 1f);
                    float v = Math.Abs((y + 0.5f) / SPARK_SIZE * 2f - 1f);

                    float coverage;
                    if (star)
                    {
                        float arms = MathF.Exp(-u * 3.4f) * MathF.Exp(-v * v * 75f) + MathF.Exp(-v * 3.4f) * MathF.Exp(-u * u * 75f);
                        float core = MathF.Exp(-(u * u + v * v) * 38f);
                        coverage = MathHelper.Clamp(arms + core, 0f, 1f) * MathHelper.SmoothStep(1f, 0.8f, MathF.Max(u, v));
                    }
                    else
                    {
                        float radius = MathF.Sqrt(u * u + v * v);
                        coverage = MathF.Exp(-radius * radius * 5f) * MathHelper.SmoothStep(1f, 0.6f, radius);
                    }

                    byte c = (byte)MathF.Round(coverage * 255f);
                    pixels[y * SPARK_SIZE + x] = new Color(c, c, c, c);
                }

            texture.SetData(pixels);
            return texture;
        }
    }
}
