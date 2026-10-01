using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// What the online client is doing, as a picture (#683): a phone's signal-strength bars in front of the boards
    /// plate's status line. A still "Sending your score..." read as a stuck line - the owner's note was that it should
    /// visibly move, "so you can see the sending is really happening".
    /// <list type="bullet">
    /// <item><see cref="SignalMode.Working"/> - a request is in flight (the clear on its way, or the boards loading):
    /// the bars light one after another, bottom to top, and start over.</item>
    /// <item><see cref="SignalMode.Done"/> - the service took the clear: every bar lit in the gold of the player's own
    /// row on the boards (<see cref="BS3DGame.BoardYouColor"/>), the one mark on the plate that is a reward.</item>
    /// <item><see cref="SignalMode.Idle"/> - offline or refused: every bar dim and still, because nothing is
    /// happening and a moving mark would promise that something is.</item>
    /// </list>
    /// Drawn straight into Myra's render pass like <see cref="MusicVisualizer"/>, from a clock the page advances: a
    /// handful of rectangles a frame, no allocation, no text re-measured.
    /// </summary>
    internal sealed class OnlineSignal : Widget
    {
        internal enum SignalMode : byte
        {
            Working,
            Done,
            Idle,
        }

        private const int BARS = 4;

        //One sweep of the bars, lighting up and starting over, in seconds: brisk enough to read as activity, slow
        //enough that each bar is seen to light
        private const float SWEEP_SECONDS = 1.2f;

        //What an unlit bar keeps of the text colour - the shape stays, so the icon reads as signal bars at rest too
        private const float UNLIT = 0.22f;

        private float _clock;

        public OnlineSignal(int height)
        {
            Height = height;
            Width = height;
            VerticalAlignment = VerticalAlignment.Center;
        }

        internal SignalMode Mode { get; private set; } = SignalMode.Idle;

        /// <summary>Sets the mode; a new <see cref="SignalMode.Working"/> starts its sweep from no bar lit.</summary>
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

            //Four bars across the square, each a gap apart, rising a quarter of the height each
            int barWidth = Math.Max(1, bounds.Width * 2 / (3 * BARS));
            int gap = Math.Max(1, (bounds.Width - barWidth * BARS) / (BARS - 1));

            //Bars lit in the sweep: none, one, ... all, then over again - BARS + 1 steps a sweep
            int lit = (int)(_clock / SWEEP_SECONDS * (BARS + 1)) % (BARS + 1);

            for (int b = 0; b < BARS; b++)
            {
                int height = Math.Max(1, bounds.Height * (b + 1) / BARS);
                Rectangle bar = new(bounds.X + b * (barWidth + gap), bounds.Bottom - height, barWidth, height);

                Color color = Mode switch
                {
                    SignalMode.Done => BS3DGame.BoardYouColor,
                    SignalMode.Working => b < lit ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT * UNLIT,
                    _ => BS3DGame.MENU_TEXT_DIM * UNLIT * 2f,
                };

                context.FillRectangle(bar, color);
            }
        }
    }
}
