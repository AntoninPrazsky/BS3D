using Microsoft.Xna.Framework.Audio;
using System;
using System.Threading.Tasks;

namespace BS3D.Audio
{
    /// <summary>
    /// The About page's player (#443): the game's original procedural score, kept as an easter egg once the level
    /// music became generated recordings. Pause, next piece, and a spectrum for the page's visualizer.
    /// <para>
    /// <b>A piece is synthesized when it is asked for, and let go when the player moves on.</b> The five
    /// compositions and the menu loop cost 0.5–4.7 s each to render (measured through <c>Tools/MusicBake</c>) and
    /// ~26 MB each to hold, so rendering all six at the splash for a page most sessions never open would be the
    /// music's old memory bill paid for an easter egg. One piece at a time, rendered through the same
    /// <see cref="ProceduralMusic.Render"/> door the tool measures, and the page says it is composing meanwhile —
    /// which, for a score written as code, is literally what is happening.
    /// </para>
    /// <para>
    /// The piece plays <b>whole and looped</b>, prelude and outro included: this is a listening, not a level, so
    /// the entry a level used to take (#201) does not apply.
    /// </para>
    /// </summary>
    internal sealed class ProceduralJukebox : IDisposable
    {
        private static readonly (string Name, MusicTheme? Theme)[] PIECES =
        {
            ("Pulse", MusicTheme.Pulse),
            ("Bohemia", MusicTheme.Bohemia),
            ("Nocturne", MusicTheme.Nocturne),
            ("Mural", MusicTheme.Mural),
            ("Ember", MusicTheme.Ember),
            ("Menu", null),
        };

        public static int PieceCount => PIECES.Length;

        /// <summary>How many bars the visualizer draws.</summary>
        public const int BAND_COUNT = 24;

        private const int WINDOW = 2048;

        //The bars span 40 Hz to 16 kHz on a log scale. A band's level is its mean power per bin against a full-scale
        //sine's peak bin under the same window, TILTED by TILT_DB_PER_OCTAVE about 1 kHz, then mapped from FLOOR_DB
        //(an empty bar) to TOP_DB (a full one). Measured over all six pieces, four readings a second: untilted, the
        //bands' medians spread from -22 dB in the bass to -80 in the top octave, so one scale either pinned the
        //bass bars or left the treble ones dead — which is exactly how the first version looked. Tilted by 6 dB an
        //octave the medians sit within -57..-45 and the 95th percentiles within -38..-28, and -65..-25 puts a
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

        private readonly double[] _window = new double[WINDOW];
        private readonly double[] _re = new double[WINDOW];
        private readonly double[] _im = new double[WINDOW];
        private readonly int[] _bandBins = new int[BAND_COUNT + 1];
        private readonly double[] _bandTilt = new double[BAND_COUNT];
        private readonly float[] _targets = new float[BAND_COUNT];
        private readonly float[] _bands = new float[BAND_COUNT];

        private int _index;
        private Task<byte[]> _render;
        private bool _restartWhenReady;
        private byte[] _pcm;
        private SoundEffect _track;
        private SoundEffectInstance _instance;
        private double _position;
        private float _gain = 1f;

        public ProceduralJukebox()
        {
            for (int i = 0; i < WINDOW; i++) _window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / WINDOW);

            //Log-spaced edges, each band at least one bin wide — at this window the low bands are narrower than a
            //bin apart, and a band with no bin in it would be a bar that never moves.
            double binHz = (double)ProceduralMusic.SAMPLE_RATE / WINDOW;
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

        /// <summary>Each bar's height, 0–1, for this frame. Falls to nothing when nothing is playing.</summary>
        public ReadOnlySpan<float> Bands => _bands;

        public string PieceName => PIECES[_index].Name;

        /// <summary>1-based, for "3 / 6".</summary>
        public int PieceNumber => _index + 1;

        /// <summary>True while the chosen piece is being synthesized.</summary>
        public bool IsComposing => _render != null;

        public bool IsPlaying => _instance != null && _instance.State == SoundState.Playing;

        /// <summary>
        /// True while a piece is chosen at all — composing, playing or paused. What the game's own music steps
        /// aside for (<see cref="GameMusic.Yielding"/>): a paused piece is a player holding the record, not
        /// handing the room back.
        /// </summary>
        public bool HoldsPiece => _render != null || _instance != null;

        /// <summary>The player's volume settings (master × music), pushed onto the sounding piece.</summary>
        public float Gain
        {
            get => _gain;
            set
            {
                _gain = value;
                if (_instance != null) _instance.Volume = GameMusic.MUSIC_VOLUME * _gain;
            }
        }

        /// <summary>Plays the chosen piece, or pauses and resumes it. A press while it composes waits for nothing.</summary>
        public void PlayPause()
        {
            if (_render != null) return;

            if (_instance == null) Start();
            else if (_instance.State == SoundState.Playing) _instance.Pause();
            else _instance.Resume();
        }

        /// <summary>
        /// Moves to the next piece and plays it. Pressed while one is still composing, the choice moves on and the
        /// newest is rendered the moment the running render lands — one render at a time, however fast the button
        /// is pressed, since each is seconds of work and tens of megabytes.
        /// </summary>
        public void Next()
        {
            _index = (_index + 1) % PIECES.Length;

            if (_render != null) _restartWhenReady = true;
            else Start();
        }

        /// <summary>Lets go of the piece — called when the About page is left.</summary>
        public void Stop()
        {
            Release();
            _render = null;
            _restartWhenReady = false;
        }

        /// <summary>Called once a frame by the host: realizes a finished render, keeps the clock, and moves the bars.</summary>
        public void Update(float elapsed)
        {
            if (_render != null && _render.IsCompleted)
            {
                Task<byte[]> ready = _render;
                _render = null;

                if (_restartWhenReady)
                {
                    _restartWhenReady = false;
                    Start();
                }
                else Realize(ready);
            }

            bool playing = IsPlaying;

            if (playing)
            {
                int frames = _pcm.Length / 4;
                _position = (_position + elapsed) % (frames / (double)ProceduralMusic.SAMPLE_RATE);
                Analyse(frames);
            }

            float rise = 1f - MathF.Exp(-RISE_PER_SECOND * elapsed);

            for (int b = 0; b < BAND_COUNT; b++)
            {
                float target = playing ? _targets[b] : 0f;

                _bands[b] = target > _bands[b]
                    ? _bands[b] + (target - _bands[b]) * rise
                    : MathF.Max(target, _bands[b] - FALL_PER_SECOND * elapsed);
            }
        }

        private void Start()
        {
            Release();

            MusicTheme? theme = PIECES[_index].Theme;

            _render = Task.Run(() => ProceduralMusic.ToPcm(theme is MusicTheme composition
                ? ProceduralMusic.Render(composition, out _)
                : ProceduralMusic.RenderMenu(out _)));
        }

        private void Realize(Task<byte[]> ready)
        {
            try
            {
                _pcm = ready.Result;
                _track = new SoundEffect(_pcm, ProceduralMusic.SAMPLE_RATE, AudioChannels.Stereo);
                _instance = _track.CreateInstance();
                _instance.IsLooped = true;
                _instance.Volume = GameMusic.MUSIC_VOLUME * _gain;
                _instance.Play();
                _position = 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[music] the About page's piece could not be played: {exception.Message}");
                Release();
            }
        }

        private void Release()
        {
            _instance?.Dispose();
            _instance = null;
            _track?.Dispose();
            _track = null;
            _pcm = null;
        }

        /// <summary>
        /// The spectrum at the playing position: a window of the mono fold centred on it, through the shared FFT,
        /// folded into each band's mean power and mapped onto a bar's 0–1. Allocation-free: every buffer is the
        /// jukebox's own.
        /// </summary>
        private void Analyse(int frames)
        {
            int start = (int)(_position * ProceduralMusic.SAMPLE_RATE) - WINDOW / 2;

            for (int i = 0; i < WINDOW; i++)
            {
                int frame = ((start + i) % frames + frames) % frames;
                int at = frame * 4;

                short left = (short)(_pcm[at] | (_pcm[at + 1] << 8));
                short right = (short)(_pcm[at + 2] | (_pcm[at + 3] << 8));

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

        public void Dispose() => Stop();
    }
}
