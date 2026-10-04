using System;

namespace BS3D.Audio
{
    /// <summary>What a spectrum display reads: each bar's height, 0–1, for this frame (#704).</summary>
    internal interface IBandSource
    {
        ReadOnlySpan<float> Bands { get; }
    }

    /// <summary>
    /// The spectrum the music visualizer draws (#443, #730), for any player that holds its music as interleaved 16-bit
    /// stereo PCM: the About page's procedural score and the Jukebox's recordings (#704) feed the one analyser, so the
    /// two pages show literally the same bars. Moved out of <c>ProceduralJukebox</c> whole, the figures with it.
    /// <para>
    /// A Hann window of the mono fold centred on the playing position, through the shared <see cref="Spectrum.Fft"/>,
    /// folded into each band's mean power and mapped onto a bar's 0–1; then the bars move towards that on the wall
    /// clock. Allocation-free: every buffer is the analyser's own, made once.
    /// </para>
    /// </summary>
    internal sealed class SpectrumAnalyser : IBandSource
    {
        /// <summary>How many bars the visualizer draws.</summary>
        public const int BAND_COUNT = 24;

        private const int WINDOW = 2048;
        private const int BYTES_PER_FRAME = 4;

        //The bars span 40 Hz to 16 kHz on a log scale. A band's level is its mean power per bin against a full-scale
        //sine's peak bin under the same window, TILTED by TILT_DB_PER_OCTAVE about 1 kHz, then mapped from FLOOR_DB
        //(an empty bar) to TOP_DB (a full one). Measured over all six procedural pieces, four readings a second:
        //untilted, the bands' medians spread from -22 dB in the bass to -80 in the top octave, so one scale either
        //pinned the bass bars or left the treble ones dead — which is exactly how the first version looked. Tilted by
        //6 dB an octave the medians sit within -57..-45 and the 95th percentiles within -38..-28, and -65..-25 puts a
        //typical bar about a third of the way up and a loud moment near the top.
        private const double LOW_HZ = 40;
        private const double HIGH_HZ = 16000;
        private const double TILT_DB_PER_OCTAVE = 6;
        private const double TILT_PIVOT_HZ = 1000;
        private const double FLOOR_DB = -65;
        private const double TOP_DB = -25;

        //A bar jumps up almost at once and falls at a steady rate, the way a meter's ballistics read — on the wall
        //clock, so the bars move the same at 30 and at 240 frames a second.
        private const float RISE_PER_SECOND = 28f;
        private const float FALL_PER_SECOND = 1.8f;

        private readonly int _sampleRate;
        private readonly double[] _window = new double[WINDOW];
        private readonly double[] _re = new double[WINDOW];
        private readonly double[] _im = new double[WINDOW];
        private readonly int[] _bandBins = new int[BAND_COUNT + 1];
        private readonly double[] _bandTilt = new double[BAND_COUNT];
        private readonly float[] _targets = new float[BAND_COUNT];
        private readonly float[] _bands = new float[BAND_COUNT];

        /// <param name="sampleRate">The PCM's rate, which the bands' bins are placed by.</param>
        public SpectrumAnalyser(int sampleRate)
        {
            _sampleRate = sampleRate;

            for (int i = 0; i < WINDOW; i++) _window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / WINDOW);

            //Log-spaced edges, each band at least one bin wide — at this window the low bands are narrower than a
            //bin apart, and a band with no bin in it would be a bar that never moves.
            double binHz = (double)sampleRate / WINDOW;
            for (int b = 0; b <= BAND_COUNT; b++)
            {
                double hz = LOW_HZ * Math.Pow(HIGH_HZ / LOW_HZ, b / (double)BAND_COUNT);
                int bin = (int)Math.Round(hz / binHz);
                _bandBins[b] = b == 0 ? Math.Max(1, bin) : Math.Max(_bandBins[b - 1] + 1, bin);
            }

            for (int b = 0; b < BAND_COUNT; b++)
            {
                double centreHz = Math.Sqrt(_bandBins[b] * (double)_bandBins[b + 1]) * binHz;
                _bandTilt[b] = TILT_DB_PER_OCTAVE * Math.Log2(centreHz / TILT_PIVOT_HZ);
            }
        }

        /// <inheritdoc/>
        public ReadOnlySpan<float> Bands => _bands;

        /// <summary>
        /// The spectrum at <paramref name="positionSeconds"/> of <paramref name="pcm"/>, set as the bars' targets. Only
        /// the first <paramref name="availableFrames"/> are read (a frame not yet there is silence); a window past
        /// either end wraps onto the other when <paramref name="loops"/> and the whole of it is there, and is silence
        /// otherwise — a loop's seam reads as the music it is, a recording played once ends in quiet.
        /// </summary>
        public void Analyse(byte[] pcm, int totalFrames, int availableFrames, double positionSeconds, bool loops)
        {
            bool whole = availableFrames >= totalFrames;
            int start = (int)(positionSeconds * _sampleRate) - WINDOW / 2;

            for (int i = 0; i < WINDOW; i++)
            {
                int frame = start + i;

                if (frame < 0 || frame >= totalFrames)
                {
                    if (!whole || !loops) { _re[i] = 0; _im[i] = 0; continue; }
                    frame = (frame % totalFrames + totalFrames) % totalFrames;
                }
                else if (frame >= availableFrames) { _re[i] = 0; _im[i] = 0; continue; }

                int at = frame * BYTES_PER_FRAME;

                short left = (short)(pcm[at] | (pcm[at + 1] << 8));
                short right = (short)(pcm[at + 2] | (pcm[at + 3] << 8));

                _re[i] = (left + right) / 65536.0 * _window[i];
                _im[i] = 0;
            }

            Spectrum.Fft(_re, _im);

            //A full-scale sine's peak bin under a Hann window has magnitude N/4
            double reference = (WINDOW / 4.0) * (WINDOW / 4.0);

            for (int b = 0; b < BAND_COUNT; b++)
            {
                double power = 0;
                int from = _bandBins[b], to = Math.Min(_bandBins[b + 1], WINDOW / 2);

                for (int bin = from; bin < to; bin++) power += _re[bin] * _re[bin] + _im[bin] * _im[bin];

                double db = 10 * Math.Log10(Math.Max(power / Math.Max(1, to - from), 1e-12) / reference) + _bandTilt[b];

                _targets[b] = (float)Math.Clamp((db - FLOOR_DB) / (TOP_DB - FLOOR_DB), 0, 1);
            }
        }

        /// <summary>Moves the bars towards the targets (or towards nothing when nothing is playing), on the wall clock.</summary>
        public void Step(float elapsed, bool playing)
        {
            float rise = 1f - MathF.Exp(-RISE_PER_SECOND * elapsed);

            for (int b = 0; b < BAND_COUNT; b++)
            {
                float target = playing ? _targets[b] : 0f;

                _bands[b] = target > _bands[b]
                    ? _bands[b] + (target - _bands[b]) * rise
                    : MathF.Max(target, _bands[b] - FALL_PER_SECOND * elapsed);
            }
        }
    }
}
