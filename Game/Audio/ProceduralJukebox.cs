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
    /// <b>And it plays while the piece is still being composed (#464).</b> The render publishes what is final a
    /// bar at a time through <see cref="RenderProgress"/> — driven, saturated, never written again — and this
    /// player converts each newly final stretch to 16-bit in place and queues it on a
    /// <see cref="DynamicSoundEffectInstance"/> in half-second chunks, exactly as <see cref="GameMusic"/> feeds
    /// its loops. The first sound follows the first published bar within a frame; the render runs 30–150× real
    /// time (the issue's own table), so the queue never runs dry behind it. "Composing..." is what the page reads
    /// only until then. The menu loop is the one piece that cannot stream — its tail is folded onto its head at
    /// the very end of its render (<c>BakeMenu</c>) — so it lands whole and is queued the same way from there.
    /// </para>
    /// <para>
    /// The piece plays <b>whole and looped</b>, prelude and outro included: this is a listening, not a level, so
    /// the entry a level used to take (#201) does not apply. A dynamic voice cannot loop itself, so the loop is
    /// this player's: at the end of the piece the queue goes back to its top, once the whole of it exists.
    /// </para>
    /// </summary>
    internal sealed class ProceduralJukebox : IBandSource, IDisposable
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

        //=== The stream (#464) ===
        //A chunk is half a second and the queue holds three at most, so the first sound follows the first published
        //bar by one chunk and no more than 1.5 s ever sits queued ahead of the playhead. A frame converts at most
        //four seconds of the render to 16-bit: the compositions publish a bar at a time so that is never reached,
        //but the menu loop lands whole (77 s at once), and spreading its conversion over twenty frames keeps every
        //Update under a couple of milliseconds while its first chunks are already sounding.
        private const int CHUNK_FRAMES = ProceduralMusic.SAMPLE_RATE / 2;
        private const int QUEUE_DEPTH = 3;
        private const int CONVERT_FRAMES_PER_UPDATE = ProceduralMusic.SAMPLE_RATE * 4;
        private const int BYTES_PER_FRAME = 4;

        //The spectrum for the page's visualizer, shared with the Jukebox's recordings (#704)
        private readonly SpectrumAnalyser _analyser = new(ProceduralMusic.SAMPLE_RATE);

        private int _index;
        private Task _render;
        private RenderProgress _progress;
        private byte[] _pcm;
        private int _totalFrames;
        private int _convertedFrames;
        private int _submittedFrames;
        private DynamicSoundEffectInstance _voice;
        private double _position;
        private float _gain = 1f;

        /// <summary>Each bar's height, 0–1, for this frame. Falls to nothing when nothing is playing.</summary>
        public ReadOnlySpan<float> Bands => _analyser.Bands;

        public string PieceName => PIECES[_index].Name;

        /// <summary>1-based, for "3 / 6".</summary>
        public int PieceNumber => _index + 1;

        /// <summary>
        /// True while the chosen piece is being synthesized <b>and nothing of it has sounded yet</b> (#464): the
        /// render keeps running under the first minutes of a piece, but "Composing..." is only the wait for its
        /// first bar.
        /// </summary>
        public bool IsComposing => _render != null && _voice == null;

        public bool IsPlaying => _voice != null && _voice.State == SoundState.Playing;

        /// <summary>
        /// True while a piece is chosen at all — composing, playing or paused. What the game's own music steps
        /// aside for (<see cref="GameMusic.Yielding"/>): a paused piece is a player holding the record, not
        /// handing the room back.
        /// </summary>
        public bool HoldsPiece => _render != null || _voice != null;

        /// <summary>The player's volume settings (master × music), pushed onto the sounding piece.</summary>
        public float Gain
        {
            get => _gain;
            set
            {
                _gain = value;
                if (_voice != null) _voice.Volume = GameMusic.MUSIC_VOLUME * _gain;
            }
        }

        /// <summary>
        /// Plays the chosen piece, or pauses and resumes it. A press in the moment between asking for a piece
        /// and its first bar does nothing — the piece is already on its way.
        /// </summary>
        public void PlayPause()
        {
            if (_voice == null)
            {
                if (_render == null) Start();
                return;
            }

            if (_voice.State == SoundState.Playing) _voice.Pause();
            else _voice.Resume();
        }

        /// <summary>
        /// Moves to the next piece and plays it, at once. A render still running is <b>cancelled</b> rather than
        /// waited out (#464): it checks <see cref="RenderProgress.Cancelled"/> once a bar and unwinds within one,
        /// which is milliseconds of work, so however fast the button is pressed there is at most one render doing
        /// anything for longer than that. The one exception is the menu loop, whose render takes no progress and
        /// so cannot be told — it runs out on its own thread (0.4 s in Release, 1.6 in Debug) and its buffer is
        /// dropped.
        /// </summary>
        public void Next()
        {
            _index = (_index + 1) % PIECES.Length;
            Start();
        }

        /// <summary>Lets go of the piece — called when the About page is left. A running render is cancelled.</summary>
        public void Stop()
        {
            Release();
            _render = null;
        }

        /// <summary>
        /// Called once a frame by the host: notices a render that failed, converts and queues what the render has
        /// published since the last frame, keeps the clock, and moves the bars.
        /// </summary>
        public void Update(float elapsed)
        {
            if (_render != null && _render.IsCompleted)
            {
                Task done = _render;
                _render = null;

                if (done.IsFaulted)
                {
                    Console.WriteLine($"[music] the About page's piece could not be composed: {done.Exception?.GetBaseException().Message}");
                    Release();
                }
            }

            Feed();

            bool playing = IsPlaying;

            if (playing)
            {
                _position = (_position + elapsed) % (_totalFrames / (double)ProceduralMusic.SAMPLE_RATE);

                //It reads only what has been converted so far (#464): a frame the render has not reached is silence,
                //and the wrap onto the other end of the loop waits for the whole piece to exist
                _analyser.Analyse(_pcm, _totalFrames, _convertedFrames, _position, loops: true);
            }

            _analyser.Step(elapsed, playing);
        }

        private void Start()
        {
            Release();

            MusicTheme? theme = PIECES[_index].Theme;
            RenderProgress progress = new();
            _progress = progress;

            //A cancelled render leaves through OperationCanceledException from its next bar's publish; swallowed
            //here so the abandoned task completes quietly rather than faulting unobserved. The menu loop is never
            //streamed, so it publishes its whole self the moment it is done and the feed below treats that as one
            //very large bar.
            _render = theme is MusicTheme composition
                ? Task.Run(() =>
                {
                    try { ProceduralMusic.Render(composition, out _, progress); }
                    catch (OperationCanceledException) { }
                })
                : Task.Run(() =>
                {
                    float[] mix = ProceduralMusic.RenderMenu(out _);
                    progress.Mix = mix;
                    progress.Publish(mix.Length / 2);
                });
        }

        /// <summary>
        /// The stream's one step, once a frame: converts what the render has newly published, opens the voice on
        /// the first bar, tops the queue up in chunks, and loops the piece once all of it is there.
        /// </summary>
        private void Feed()
        {
            RenderProgress progress = _progress;
            if (progress == null) return;

            //The count first and the buffer second, always: the count is the volatile the renderer publishes AFTER
            //it has set the buffer, so a count above zero is the guarantee that the reference is there — and that
            //every frame below it is finished (RenderProgress's one contract).
            int safe = progress.SafeFrames;
            if (safe <= 0) return;

            if (_pcm == null)
            {
                _totalFrames = progress.Mix.Length / 2;
                _pcm = new byte[progress.Mix.Length * 2];
            }

            if (safe > _convertedFrames)
            {
                int to = Math.Min(safe, _convertedFrames + CONVERT_FRAMES_PER_UPDATE);
                ProceduralMusic.ToPcm(progress.Mix, _pcm, _convertedFrames * 2, to * 2);
                _convertedFrames = to;
            }

            if (_voice == null)
            {
                try
                {
                    _voice = new DynamicSoundEffectInstance(ProceduralMusic.SAMPLE_RATE, AudioChannels.Stereo);
                    //A recycled OpenAL source keeps the last voice's pitch (GameMusic's arrival says how, #790).
                    _voice.Pitch = 0f;
                    _voice.Volume = GameMusic.MUSIC_VOLUME * _gain;
                    _position = 0;
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the About page's piece could not be played: {exception.Message}");
                    Stop();
                    return;
                }
            }

            bool whole = _convertedFrames >= _totalFrames;

            while (_voice.PendingBufferCount < QUEUE_DEPTH)
            {
                int available = _convertedFrames - _submittedFrames;

                if (available <= 0)
                {
                    //The end of the piece, and it plays whole and looped: back to the top — but only once the whole
                    //of it exists. Before that the queue simply waits for the renderer, which is never behind it.
                    if (whole && _submittedFrames >= _totalFrames)
                    {
                        _submittedFrames = 0;
                        continue;
                    }
                    break;
                }

                int count = Math.Min(available, CHUNK_FRAMES);

                try
                {
                    _voice.SubmitBuffer(_pcm, _submittedFrames * BYTES_PER_FRAME, count * BYTES_PER_FRAME);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the About page's piece could not be queued: {exception.Message}");
                    Stop();
                    return;
                }

                _submittedFrames += count;
            }

            //Started on the first chunk. Paused is the player's own state and is left alone: Stopped is only ever
            //the voice before its first Play.
            if (_submittedFrames > 0 && _voice.State == SoundState.Stopped) _voice.Play();
        }

        private void Release()
        {
            _progress?.Cancel();
            _progress = null;

            _voice?.Dispose();
            _voice = null;

            _pcm = null;
            _totalFrames = 0;
            _convertedFrames = 0;
            _submittedFrames = 0;
        }

        public void Dispose() => Stop();
    }
}
