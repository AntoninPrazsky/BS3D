using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BS3D.Audio
{
    /// <summary>
    /// The Jukebox's player (#704): the game's generated recordings (<c>Game/Music</c>) played one after another, each
    /// once and then the next — the owner's "after a track, go on to the next" — with pause, next and previous, and the
    /// spectrum the About page's visualizer draws.
    /// <para>
    /// <b>Not <see cref="GameMusic"/>.</b> That one is level-shaped: a family that rotates per level, a drums layer
    /// riding the cluster's danger, loops chained for ever and no pause in place. This is the About page's player's
    /// pattern (<see cref="ProceduralJukebox"/>): its own <see cref="DynamicSoundEffectInstance"/>, its own PCM fed in
    /// half-second chunks, its own position clock for the analyser, and the game's music steps aside while it holds a
    /// recording (<see cref="GameMusic.Yielding"/>, asked by the audio director every frame).
    /// </para>
    /// <para>
    /// <b>One recording decoded at a time, and the next one ahead of it.</b> A decoded recording is 10–20 MB of 16-bit
    /// PCM, which is why <see cref="GameMusic"/> decodes a family only when a level asks for it; here the one playing is
    /// held, and the next in the list is decoded on the thread pool as soon as it starts, so the step from one to the
    /// next waits for nothing. The decode is <see cref="OggTrack.Decode(string, int)"/>, the game's own.
    /// </para>
    /// </summary>
    internal sealed class RecordingPlayer : IBandSource, IDisposable
    {
        /// <summary>One recording in the list: its file, the title the page shows, and the group it is listed under.</summary>
        internal sealed class Track
        {
            public string Path { get; init; }
            public string Title { get; init; }
            public string Group { get; init; }
        }

        //GameMusic's rate: every track in Game/Music is 48 kHz stereo, and OggTrack refuses anything else
        private const int SAMPLE_RATE = 48000;
        private const int BYTES_PER_FRAME = 4;

        //Half a second a chunk and three queued at most, as GameMusic and the About player feed theirs
        private const int CHUNK_FRAMES = SAMPLE_RATE / 2;
        private const int QUEUE_DEPTH = 3;

        private readonly SpectrumAnalyser _analyser = new(SAMPLE_RATE);

        private IReadOnlyList<Track> _tracks = Array.Empty<Track>();
        private int _index = -1;

        private Task<byte[]> _load;
        private int _loadIndex = -1;
        private Task<byte[]> _ahead;
        private int _aheadIndex = -1;

        //How many recordings in a row could not be read (the review of #704): a broken one is stepped past, and once every
        //recording in the list has failed the player stops rather than going round for ever
        private int _unreadable;

        private byte[] _pcm;
        private int _totalFrames;
        private int _submittedFrames;
        private DynamicSoundEffectInstance _voice;
        private double _position;
        private float _gain = 1f;

        /// <inheritdoc/>
        public ReadOnlySpan<float> Bands => _analyser.Bands;

        /// <summary>The list the player walks, in the order Next takes it.</summary>
        public IReadOnlyList<Track> Tracks => _tracks;

        /// <summary>The recording chosen, −1 before the first — kept when the player stops, so the page still says where it was.</summary>
        public int Index => _index;

        /// <summary>True from asking for a recording until its first chunk sounds: the decode, a fraction of a second.</summary>
        public bool IsLoading => _load != null && _voice == null;

        public bool IsPlaying => _voice != null && _voice.State == SoundState.Playing;

        /// <summary>True while a recording is chosen at all — loading, playing or paused: what the game's music steps aside for.</summary>
        public bool HoldsTrack => _load != null || _voice != null;

        /// <summary>The player's volume settings (master × music), pushed onto the sounding recording.</summary>
        public float Gain
        {
            get => _gain;
            set
            {
                _gain = value;
                if (_voice != null) _voice.Volume = GameMusic.MUSIC_VOLUME * _gain;
            }
        }

        /// <summary>Hands the player its list (the page builds it from the music folder and the level set). Stops what was playing.</summary>
        public void SetTracks(IReadOnlyList<Track> tracks)
        {
            Stop();
            _tracks = tracks ?? Array.Empty<Track>();
            _index = -1;
        }

        /// <summary>Plays the list's recording <paramref name="index"/> from its top, at once.</summary>
        public void PlayAt(int index)
        {
            if (_tracks.Count == 0) return;

            ReleaseVoice();
            _index = ((index % _tracks.Count) + _tracks.Count) % _tracks.Count;

            Track track = _tracks[_index];
            Console.WriteLine($"[jukebox] {track.Group}: {track.Title} ({_index + 1} of {_tracks.Count})");

            //One decode at a time (the review of #704): a press while one is still running leaves it to finish, and Update
            //starts this recording's when it lands, so a pad held on Next walks the list instead of starting a decode a press
            if (_load != null && !_load.IsCompleted) return;

            StartLoad();
        }

        /// <summary>The chosen recording's decode: the one decoded ahead when it is that one, its own otherwise.</summary>
        private void StartLoad()
        {
            _load = _ahead != null && _aheadIndex == _index ? _ahead : Decode(_index);
            _loadIndex = _index;
            _ahead = null;
            _aheadIndex = -1;
        }

        /// <summary>Plays, or pauses and resumes. With nothing chosen yet it starts the list's first recording.</summary>
        public void PlayPause()
        {
            if (_voice == null)
            {
                if (_load == null) PlayAt(Math.Max(0, _index));
                return;
            }

            if (_voice.State == SoundState.Playing) _voice.Pause();
            else _voice.Resume();
        }

        public void Next() => PlayAt(_index + 1);

        public void Previous() => PlayAt(_index < 0 ? 0 : _index - 1);

        /// <summary>Lets go of the recording — called when the page is left. The list and the index are kept.</summary>
        public void Stop()
        {
            ReleaseVoice();
            _load = null;
            _ahead = null;
            _aheadIndex = -1;
        }

        /// <summary>
        /// Once a frame from the audio director: takes a finished decode, keeps the queue topped up, moves on to the
        /// next recording when one has played to its end, keeps the clock and moves the bars.
        /// </summary>
        public void Update(float elapsed)
        {
            if (_load != null && _load.IsCompleted && _voice == null)
            {
                //A decode that landed for a recording the player has since moved on from: start the one now chosen
                if (_loadIndex != _index) StartLoad();
                else Open();
            }

            Feed();

            bool playing = IsPlaying;

            if (playing && _pcm != null)
            {
                _position = Math.Min(_position + elapsed, _totalFrames / (double)SAMPLE_RATE);

                //Played once, so a window past either end is quiet rather than the other end of the recording
                _analyser.Analyse(_pcm, _totalFrames, _totalFrames, _position, loops: false);
            }

            _analyser.Step(elapsed, playing);
        }

        //A file that cannot be read comes back as null rather than as a faulted task, so one decoded ahead and then dropped
        //does not end up as an unobserved exception (the review of #704); Open says what failed
        private Task<byte[]> Decode(int index)
        {
            string path = _tracks[index].Path;
            return Task.Run(() =>
            {
                try { return OggTrack.Decode(path, SAMPLE_RATE); }
                catch (Exception exception)
                {
                    Console.WriteLine($"[jukebox] '{path}' could not be read: {exception.Message}");
                    return null;
                }
            });
        }

        /// <summary>The decode is in: opens the voice on it and starts decoding the next one, so the step to it waits for nothing.</summary>
        private void Open()
        {
            Task<byte[]> done = _load;
            _load = null;

            if (done.IsFaulted || done.Result == null || done.Result.Length < BYTES_PER_FRAME)
            {
                //A broken recording is stepped past, as a jukebox skips a scratched record - not the end of the music; but
                //once every one has failed, it stops
                ReleaseVoice();
                if (++_unreadable < _tracks.Count) PlayAt(_index + 1);
                return;
            }

            _unreadable = 0;

            try
            {
                _voice = new DynamicSoundEffectInstance(SAMPLE_RATE, AudioChannels.Stereo);
                //A recycled OpenAL source keeps the last voice's pitch (GameMusic's arrival says how, #790).
                _voice.Pitch = 0f;
                _voice.Volume = GameMusic.MUSIC_VOLUME * _gain;
            }
            catch (Exception exception)
            {
                //The audio device, not the file (no endpoint at all on a machine whose outputs are off): the next recording
                //would fail the same way, so the player stops here
                Console.WriteLine($"[jukebox] the recording could not be played: {exception.Message}");
                ReleaseVoice();
                return;
            }

            _pcm = done.Result;
            _totalFrames = _pcm.Length / BYTES_PER_FRAME;
            _submittedFrames = 0;
            _position = 0;

            if (_tracks.Count > 1)
            {
                _aheadIndex = (_index + 1) % _tracks.Count;
                _ahead = Decode(_aheadIndex);
            }
        }

        /// <summary>
        /// Tops the voice's queue up in chunks, and once the whole recording has been queued and played out, goes on to
        /// the next — a voice paused by the player is left alone.
        /// </summary>
        private void Feed()
        {
            if (_voice == null || _pcm == null) return;

            while (_voice.PendingBufferCount < QUEUE_DEPTH && _submittedFrames < _totalFrames)
            {
                int count = Math.Min(_totalFrames - _submittedFrames, CHUNK_FRAMES);

                try
                {
                    _voice.SubmitBuffer(_pcm, _submittedFrames * BYTES_PER_FRAME, count * BYTES_PER_FRAME);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[jukebox] the recording could not be queued: {exception.Message}");
                    ReleaseVoice();
                    return;
                }

                _submittedFrames += count;
            }

            if (_submittedFrames > 0 && _voice.State == SoundState.Stopped) _voice.Play();

            //Played to its end: everything queued, nothing left pending, and the voice not held by a pause
            if (_submittedFrames >= _totalFrames && _voice.PendingBufferCount == 0 && _voice.State == SoundState.Playing)
                Next();
        }

        //The sounding recording and its PCM; a decode in flight is left alone (PlayAt, Stop)
        private void ReleaseVoice()
        {
            _voice?.Dispose();
            _voice = null;

            _pcm = null;
            _totalFrames = 0;
            _submittedFrames = 0;
            _position = 0;
        }

        public void Dispose() => Stop();
    }
}
