using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The crosshair: four bars around a clear centre, struck from a single white texel this component makes
    /// itself. Drawn in display space, after the tonemap resolve, so it stays exactly as authored instead of
    /// being softened by the downsample and bent by the curve.
    /// <para>
    /// <b>No bitmap.</b> The Game always drew it this way; the Testbed loaded a 246-byte
    /// <c>Bitmaps/Aimer.png</c> and stretched it, which was one content asset, one importer stanza, one
    /// resize hook and one centring calculation for four rectangles. The bitmap is retired with #76 and the
    /// procedural bars are what both draw — the same preference that made the barrel, the island and every
    /// surface texture procedural.
    /// </para>
    /// <para>
    /// <b>It is drawn only where a screen-centre mark means something.</b> That is the callers' to decide and
    /// they decide it differently: the Game shows it only as precise aim leans in, passing
    /// <c>PreciseAim.Blend</c> as the opacity, because in the overview the centre of the screen points at
    /// nothing in particular; the Testbed's free camera shoots from the lens, where the centre <i>is</i> the
    /// shot, so it passes a plain 1 there and the same blend in game mode. Hence one scalar rather than a
    /// flag — <see cref="Draw"/> fades with it and skips itself below
    /// <see cref="MIN_OPACITY"/>, so neither caller needs a visibility test of its own.
    /// </para>
    /// <para>
    /// <b>It blinks red while the gun is pressed against its elevation clamp</b> (#431). The strain is handed in,
    /// because this component knows nothing of a gun; <see cref="StrainBrightness"/> is public so that the Game's
    /// aim beam, which carries the crosshair's signals in the overview, blinks on the same wave.
    /// </para>
    /// </summary>
    public sealed class Crosshair : IDisposable
    {
        /// <summary>
        /// Below this the bars are not drawn at all: an eased blend that has all but reached zero is a
        /// four-rectangle batch nobody can see, and the ease only settles exactly at either end.
        /// </summary>
        public const float MIN_OPACITY = 0.01f;

        //Written as a scale of white rather than as R,G,B,A: SpriteBatch's default AlphaBlend expects
        //*premultiplied* colour, and a plain (255,255,255,190) is not — it would put full white down and
        //only partly occlude what is behind it, which is a solid crosshair, not a translucent one. Color's
        //float multiply scales all four channels, so this stays premultiplied through the opacity fade too.
        private static readonly Color COLOR = Color.White * 0.75f;

        /// <summary>
        /// The mark's red for "no", in display space and fully opaque. The Game's tint over a shot that cannot
        /// stick is this colour, and so is the blink at the elevation clamp, so the two refusals are one colour.
        /// </summary>
        public static readonly Color WARNING = new(236, 74, 74);

        /// <summary>
        /// How fast the mark blinks under strain. A <b>blink</b> and not a breath: the HUD's own rule is that a
        /// slow swell reads as urgency and a hard flash as a fault, and a stop the hand is pushing into is the
        /// second. Off the cluster's 1.1 Hz heartbeat, the muzzle halo's 1.6 and the ghost's 2.2, all of which
        /// share the frame with it. On the caller's wall clock, so the phase a push starts on is arbitrary — at
        /// this rate the first bright frame is never more than an eighth of a second away.
        /// </summary>
        public const float STRAIN_BLINK_HZ = 4f;

        /// <summary>
        /// How much of the mark each blink takes away at full strain: deep enough to read as on and off, short of
        /// all of it, so the mark is never briefly missing.
        /// </summary>
        public const float STRAIN_BLINK_DEPTH = 0.85f;

        //Authored for a 2160p viewport and scaled down with it, exactly as InfoRenderer's text is, so the
        //crosshair keeps its size on the screen rather than in pixels
        private const float SCALE_DIVISOR = 2160f;
        private const float ARM = 48f;          //length of one bar
        private const float GAP = 18f;          //clear space at the centre, so the mark never hides what it marks
        private const float THICKNESS = 5f;

        private readonly GraphicsDevice _device;

        private Texture2D _texel;

        /// <param name="device">The device the texel is made on, and whose viewport the bars are centred in
        /// at draw time — read per frame rather than cached, which is what makes a resize or a fullscreen
        /// switch nothing this has to be told about (the Testbed used to recentre a bitmap from its
        /// <c>ClientSizeChanged</c> hook).</param>
        public Crosshair(GraphicsDevice device)
        {
            _device = device;

            _texel = new Texture2D(device, 1, 1);
            _texel.SetData(new[] { Color.White });
        }

        /// <summary>
        /// The four bars, in one <see cref="SpriteBatch"/> pass of its own. The batch is the caller's — the
        /// Game shares one with its HUD — and this begins and ends it around the four draws, which is what
        /// both call sites did and what keeps the default sprite state (premultiplied alpha blending) with
        /// the bars that are authored for it.
        /// </summary>
        /// <param name="opacity">1 for a crosshair that is simply there, or an eased blend for one leaning
        /// in. Scales the colour, so it fades rather than snapping on; at or below
        /// <see cref="MIN_OPACITY"/> nothing is drawn and no batch is opened.</param>
        /// <param name="tint">
        /// The bars' colour before <paramref name="opacity"/> scales it. Null is the usual near-white. Pass one to
        /// say something about what is under the mark — the Game reddens it over a shot that cannot stick (#70).
        /// <b>Give a fully opaque colour</b>: it is multiplied the way <see cref="COLOR"/> is and has to stay
        /// premultiplied, so an alpha below 255 here would draw full colour and only partly cover what is behind it.
        /// </param>
        /// <param name="strain">How hard the aim is pressed against a limit it cannot pass, 0…1 — the gun's
        /// <c>Cannon.ElevationStrain</c>. Turns the bars towards <see cref="WARNING"/> and blinks them in
        /// proportion; 0 leaves the mark exactly as <paramref name="tint"/> says.</param>
        /// <param name="clock">Seconds on the caller's wall clock, which the blink runs on. Unread without strain.</param>
        public void Draw(SpriteBatch batch, float opacity = 1f, Color? tint = null, float strain = 0f, float clock = 0f)
        {
            if (opacity <= MIN_OPACITY) return;

            Viewport viewport = _device.Viewport;

            float scale = viewport.Height / SCALE_DIVISOR;

            //A bar authored five units thick is under a pixel on a small window, where rounding down would
            //leave nothing to draw at all
            int thickness = Math.Max(1, (int)(THICKNESS * scale));
            int length = Math.Max(1, (int)(ARM * scale));

            int centreX = viewport.Width / 2;
            int centreY = viewport.Height / 2;
            int inner = (int)(GAP * scale);
            int half = thickness / 2;

            //The same 0.75 the default carries, so a tinted mark sits at the neutral one's weight rather than
            //jumping forward as well as changing colour
            Color color = tint.HasValue ? tint.Value * 0.75f : COLOR;

            //Both colours carry the same alpha, so the lerp stays premultiplied; and because the strain eases out,
            //letting go fades the mark back to what it was saying rather than snapping
            if (strain > 0f) color = Color.Lerp(color, WARNING * 0.75f, MathF.Min(strain, 1f));

            color *= opacity * StrainBrightness(strain, clock);

            batch.Begin();
            batch.Draw(_texel, new Rectangle(centreX - inner - length, centreY - half, length, thickness), color);
            batch.Draw(_texel, new Rectangle(centreX + inner, centreY - half, length, thickness), color);
            batch.Draw(_texel, new Rectangle(centreX - half, centreY - inner - length, thickness, length), color);
            batch.Draw(_texel, new Rectangle(centreX - half, centreY + inner, thickness, length), color);
            batch.End();
        }

        /// <summary>
        /// The share of a mark's brightness to keep this instant while it blinks for <paramref name="strain"/>:
        /// exactly 1 with none, dipping to 1 − <see cref="STRAIN_BLINK_DEPTH"/> once a period at full strain.
        /// </summary>
        public static float StrainBrightness(float strain, float clock)
        {
            if (strain <= 0f) return 1f;

            float trough = 0.5f - 0.5f * MathF.Cos(MathHelper.TwoPi * STRAIN_BLINK_HZ * clock);

            return 1f - MathF.Min(strain, 1f) * STRAIN_BLINK_DEPTH * trough;
        }

        /// <summary>The one texel. The batch is the caller's and is left alone.</summary>
        public void Dispose()
        {
            _texel?.Dispose();
            _texel = null;
        }
    }
}
