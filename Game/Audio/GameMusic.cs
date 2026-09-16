using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BS3D.Audio
{
    /// <summary>
    /// The game's music since #443: generated recordings (ACE-Step 1.5, see <c>Research/AI-Music</c>), one
    /// seamless loop per <see cref="MusicTheme"/> slot plus the front end's loop, read from <c>Music/*.ogg</c>
    /// beside the executable. The procedural compositions that played here until then are the About page's now
    /// (<see cref="ProceduralJukebox"/>); the fanfares stayed procedural and are still baked by
    /// <see cref="ProceduralMusic"/>, which this class owns and forwards to, so the rest of the game keeps asking
    /// one object for its music.
    /// <para>
    /// <b>What changed is where a buffer comes from, and nothing about how it is played.</b> The theme still runs
    /// through one <see cref="DynamicSoundEffectInstance"/> whose queue is fed the same buffer again while it is
    /// still sounding (#212), a change of piece still retires the sounding chain into a fading slot (#211), and
    /// the menu is still a framework-looped instance. A generated loop is cut sample-exact at whole bars
    /// (<c>loop_crossfade.py</c>, recorded in each master's sidecar), so the repeat that used to land in an
    /// authored outro's silence now runs straight on. There is no entry offset (#201) any more: a loop is cut
    /// from its render's body, so it has no prelude for a level to skip.
    /// </para>
    /// <para>
    /// The files are Ogg Vorbis at <see cref="SAMPLE_RATE"/> (#444; .wav until then), decoded on a background
    /// thread into the 16-bit stereo PCM the voice consumes (<see cref="OggTrack"/>), so the chain plays exactly
    /// the kind of buffer it always has. Not content: the pipeline's
    /// <see cref="SoundEffect"/> gives its samples back to no one, and the feed has to submit them itself. They are
    /// written by <c>Tools/MusicBake --tracks</c> from the float masters, brought to the loudness the procedural
    /// piece in the same slot measured, which is why <see cref="MUSIC_VOLUME"/> and <see cref="MENU_VOLUME"/> kept
    /// their values: the mix the effects were tuned against did not move.
    /// </para>
    /// </summary>
    public sealed class GameMusic : IDisposable
    {
        private const string MUSIC_DIRECTORY = "Music";
        private const string MENU_TRACK = "menu";
        private const string TRACK_EXTENSION = ".ogg";

        /// <summary>The rate ACE-Step renders at and every track is written at; the procedural pieces are 44.1 kHz.</summary>
        private const int SAMPLE_RATE = 48000;

        /// <summary>
        /// The authored level of the music, well under the effects — a soundtrack is not an event. A constant
        /// so the balance keeps its tuning; the player's settings rows scale it through <see cref="Gain"/>.
        /// </summary>
        public const float MUSIC_VOLUME = 0.34f;

        /// <summary>The front end's loop, under even the theme: a lobby, not a dancefloor.</summary>
        public const float MENU_VOLUME = 0.2f;

        //How long a piece the player is walking away from takes to leave (#211): the theme can be left mid-chorus
        //and is the widest thing here to put down gently, while leaving the lobby should feel prompt — it is a
        //level starting. The arrival never ramps; fading the outgoing side alone already is the crossfade.
        private const float THEME_FADE_SECONDS = 0.9f;
        private const float MENU_FADE_SECONDS = 0.5f;

        /// <summary>
        /// How long the game's music takes to step aside for the About page's player, and to come back after it.
        /// A little longer than the lobby's leaving: it is a record being lifted off, not a level starting.
        /// </summary>
        private const float YIELD_FADE_SECONDS = 0.7f;

        /// <summary>How many slots there are, read off the enum so a sixth theme needs nothing here (#279 steps it).</summary>
        public static int ThemeCount { get; } = Enum.GetValues<MusicTheme>().Length;

        private readonly ProceduralMusic _fanfares = new();

        /// <summary>
        /// Every slot's tracks, loaded on background threads at construction and never replaced — the theme's own
        /// file first, then its variants (<c>ember-*.ogg</c>) in name order. A task that finished with null is a
        /// file that could not be read, already logged; a slot with no tasks has no file at all and plays silence.
        /// </summary>
        private readonly Task<byte[]>[][] _tracks = new Task<byte[]>[ThemeCount][];

        /// <summary>Each track's file name, beside <see cref="_tracks"/>, for the one log line a level opening writes.</summary>
        private readonly string[][] _trackNames = new string[ThemeCount][];

        /// <summary>
        /// Which variant of each slot the next chain head plays. A slot with several recordings — Ember, whose
        /// punk variations the owner asked to have rotate rather than pick one — moves on every time a head is
        /// built for it, and a head is built exactly when a level opens (a build, a retry, a switch of piece, a
        /// Continue), so "each start of an Ember level plays the next one" is this one counter.
        /// </summary>
        private readonly int[] _nextVariant = new int[ThemeCount];

        private MusicTheme _theme;
        private DynamicSoundEffectInstance _voice;

        /// <summary>The recording the sounding chain was built from, which the feed submits again — never a sibling variant.</summary>
        private Task<byte[]> _sounding;

        private bool _wanted;
        private bool _failed;

        /// <summary>A chain on its way out (#211), faded and disposed at silence; see <see cref="RetireVoice"/>.</summary>
        private DynamicSoundEffectInstance _retiring;
        private readonly MusicFade _retiringFade = new();

        private Task<byte[]> _menuLoad;
        private SoundEffect _menuTrack;
        private SoundEffectInstance _menu;
        private bool _menuWanted;
        private readonly MusicFade _menuFade = new();

        /// <summary>Everything this class plays, stepping aside while the About page's player holds a piece.</summary>
        private readonly MusicFade _yield = new();

        private float _gain = 1f;

        public GameMusic()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, MUSIC_DIRECTORY);

            //The lobby first: it is what the splash hands over to, and every load below queues on the same pool —
            //on a machine with fewer cores than tracks, whatever is handed over last decodes last
            _menuLoad = Load(Path.Combine(directory, MENU_TRACK + TRACK_EXTENSION));

            for (int slot = 0; slot < ThemeCount; slot++)
            {
                string name = ((MusicTheme)slot).ToString().ToLowerInvariant();
                List<string> files = new();

                string own = Path.Combine(directory, name + TRACK_EXTENSION);
                if (File.Exists(own)) files.Add(own);

                if (Directory.Exists(directory))
                {
                    string[] variants = Directory.GetFiles(directory, name + "-*" + TRACK_EXTENSION);
                    Array.Sort(variants, StringComparer.Ordinal);
                    files.AddRange(variants);
                }

                if (files.Count == 0) Console.WriteLine($"[music] no track for {(MusicTheme)slot} in '{directory}'");

                _tracks[slot] = new Task<byte[]>[files.Count];
                _trackNames[slot] = new string[files.Count];

                for (int i = 0; i < files.Count; i++)
                {
                    _tracks[slot][i] = Load(files[i]);
                    _trackNames[slot][i] = Path.GetFileName(files[i]);
                }
            }
        }

        /// <summary>True while a fanfare is sounding; the host ducks the fireworks under it.</summary>
        public bool IsFanfarePlaying => _fanfares.IsFanfarePlaying;

        /// <summary>
        /// Which slot the moment is sounding, and <b>null while it is not one of them</b>: the front end's own loop
        /// is playing, or the music has failed. It reads what is WANTED, so a switch caught mid-fade already names
        /// the arriving piece — which is what the track picker in Settings (#279) shows.
        /// </summary>
        public MusicTheme? SoundingTheme => _failed || _menuWanted ? null : _theme;

        /// <summary>
        /// The player's volume settings (master × music), 1 for the authored level. Pushed onto whatever is already
        /// sounding — a minute-long loop is long enough that "on the next play" would mean a minute late.
        /// </summary>
        public float Gain
        {
            get => _gain;
            set
            {
                _gain = value;
                _fanfares.Gain = value;
                WriteVolumes();
            }
        }

        /// <summary>
        /// Steps the game's music aside while the About page's player holds a piece, and brings it back when it
        /// lets go. A fade on everything here rather than a stop, so the theme under a pause and the lobby loop both
        /// carry on silently and return where they are; the fanfare is left alone, since About is never over one.
        /// </summary>
        public bool Yielding
        {
            set => _yield.To(value ? 0f : 1f, YIELD_FADE_SECONDS);
        }

        /// <summary>
        /// The slot a level asks for by name, falling back to the pool's own rotation. Parsed rather than cast:
        /// the level file is hand-editable, and an unknown spelling has to mean "the default", not an exception.
        /// <paramref name="index"/> picks when nothing is named, which is why level one still opens on Pulse.
        /// </summary>
        public static MusicTheme ThemeFor(string named, int index)
        {
            if (!string.IsNullOrWhiteSpace(named))
                switch (named.Trim().ToLowerInvariant())
                {
                    case "pulse": return MusicTheme.Pulse;
                    case "bohemia": return MusicTheme.Bohemia;
                    case "nocturne": return MusicTheme.Nocturne;
                    case "mural": return MusicTheme.Mural;
                    case "ember": return MusicTheme.Ember;

                    //The slot's old name — #264 replaced the piece, not the slot, and a hand-edited file still
                    //naming the polka gets the slot rather than whatever its position happens to rotate to.
                    case "dechovka": return MusicTheme.Mural;
                }

            return (MusicTheme)(((index % ThemeCount) + ThemeCount) % ThemeCount);
        }

        /// <summary>
        /// Which slot plays. The sounding chain is retired by a fade (#211) — what is queued on it belongs to the
        /// piece being left — and the next <see cref="Update"/> builds the new one underneath it.
        /// </summary>
        public void SetTheme(MusicTheme theme)
        {
            if (_failed || theme == _theme) return;

            _theme = theme;
            RetireVoice(THEME_FADE_SECONDS);
        }

        /// <summary>Starts the theme, or does nothing if a chain is already up.</summary>
        public void Play()
        {
            if (_failed) return;

            _wanted = true;
            if (_voice == null) Advance();
        }

        /// <summary>
        /// Stops the theme <b>dead</b> — the level endings' stop, where the silence is the message and the
        /// fireworks' reports land in it. Every switch takes <see cref="FadeOut"/> instead. A still-retiring chain
        /// goes with it, or "dead" would be a half-truth on the one call whose point is the silence.
        /// </summary>
        public void Stop()
        {
            _wanted = false;

            _voice?.Dispose();
            _voice = null;
            _sounding = null;

            _retiring?.Dispose();
            _retiring = null;
        }

        /// <summary>Stops the theme by fading it (#211) — leaving to the main menu, or a retry from the pause.</summary>
        public void FadeOut()
        {
            _wanted = false;
            RetireVoice(THEME_FADE_SECONDS);
        }

        /// <summary>Starts the front end's loop, or marks it wanted until its file has loaded.</summary>
        public void PlayMenu()
        {
            if (_failed) return;

            _menuWanted = true;

            if (_menu != null && _menu.State == SoundState.Playing)
            {
                //A stop caught mid-fade: the same loop is still sounding, so it ramps back rather than snapping
                _menuFade.To(1f, MENU_FADE_SECONDS);
            }
            else
            {
                //An arrival: a fully faded stop rewound the loop, which restarts at its head at full
                _menuFade.Reset();

                if (_menu != null)
                {
                    _menu.Volume = MenuVolume;
                    _menu.Play();
                }
            }
        }

        /// <summary>Stops the front end's loop by fading — it only ever stops for a level starting over it.</summary>
        public void StopMenu()
        {
            _menuWanted = false;
            _menuFade.To(0f, MENU_FADE_SECONDS);
        }

        /// <summary>The victory fanfare, still procedural — see <see cref="ProceduralMusic.PlayVictory"/>.</summary>
        public void PlayVictory(int score, bool grand = false) => _fanfares.PlayVictory(score, grand);

        /// <summary>The defeat fanfare, still procedural — see <see cref="ProceduralMusic.PlayDefeat"/>.</summary>
        public void PlayDefeat(int score) => _fanfares.PlayDefeat(score);

        /// <summary>Retires whatever fanfare is sounding — see <see cref="ProceduralMusic.StopFanfare"/>.</summary>
        public void StopFanfare() => _fanfares.StopFanfare();

        /// <summary>The pending or sounding fanfare's key and tempo (#158) — see <see cref="ProceduralMusic.TryGetFanfare"/>.</summary>
        public bool TryGetFanfare(out ProceduralMusic.FanfareShape shape, out float secondsSounding) =>
            _fanfares.TryGetFanfare(out shape, out secondsSounding);

        /// <summary>
        /// Called once a frame, above the stack. The feed (#212): the sounding recording is put on the voice's
        /// queue again while fewer than two buffers sit on it — the count includes the one playing — so XAudio2
        /// starts the repeat the sample the current pass ends. It also realizes the menu loop the frame its file
        /// has loaded, walks the fades, and drives the fanfare player.
        /// </summary>
        public void Update(float elapsed)
        {
            if (_failed) return;

            _fanfares.Update(elapsed);
            AdvanceFades(elapsed);

            if (_menuLoad != null && _menuLoad.IsCompleted)
            {
                Task<byte[]> ready = _menuLoad;
                _menuLoad = null;

                //Guarded like the fanfare: a lobby that cannot play must not take the game down with it
                try
                {
                    if (ready.Result != null)
                    {
                        _menuTrack = new SoundEffect(ready.Result, SAMPLE_RATE, AudioChannels.Stereo);
                        _menu = _menuTrack.CreateInstance();
                        _menu.IsLooped = true;

                        //A splash clicked through fast can have a ramp already in flight before the file landed
                        _menu.Volume = MenuVolume;

                        if (_menuWanted) _menu.Play();
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the menu loop could not be realized: {exception.Message}");
                }
            }

            if (!_wanted) return;

            if (_voice == null)
            {
                Advance();
                return;
            }

            if (_sounding != null && _voice.PendingBufferCount < 2)
            {
                try
                {
                    _voice.SubmitBuffer(_sounding.Result);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the theme could not be queued again, playing on without it: {exception.Message}");
                    _failed = true;
                }
            }
        }

        private float ThemeVolume => MUSIC_VOLUME * _gain * _yield.Applied;
        private float MenuVolume => MENU_VOLUME * _gain * _menuFade.Applied * _yield.Applied;
        private float RetiringVolume => MUSIC_VOLUME * _gain * _retiringFade.Applied * _yield.Applied;

        private void WriteVolumes()
        {
            if (_voice != null) _voice.Volume = ThemeVolume;
            if (_menu != null) _menu.Volume = MenuVolume;
            if (_retiring != null) _retiring.Volume = RetiringVolume;
        }

        /// <summary>
        /// The fades, walked once a frame. Volumes are written only on the frames a fade moved them, and an
        /// instance that has arrived at silence is stopped — or, for a retired chain, disposed — right here.
        /// </summary>
        private void AdvanceFades(float elapsed)
        {
            bool yieldMoved = _yield.Advance(elapsed);

            //A non-short-circuit | so every fade walks its frame whatever the other one did
            if ((_menuFade.Advance(elapsed) | yieldMoved) && _menu != null)
            {
                _menu.Volume = MenuVolume;
                if (_menuFade.Silent) _menu.Stop();
            }

            if (yieldMoved && _voice != null) _voice.Volume = ThemeVolume;

            if ((_retiringFade.Advance(elapsed) | yieldMoved) && _retiring != null)
            {
                _retiring.Volume = RetiringVolume;

                //Disposed rather than stopped: the feed only ever submits to _voice, so a retired chain has no way back
                if (_retiringFade.Silent)
                {
                    _retiring.Dispose();
                    _retiring = null;
                }
            }
        }

        /// <summary>
        /// Hands the sounding chain to <see cref="_retiring"/> to fade out and leaves <see cref="_voice"/> null, so
        /// the next <see cref="Advance"/> builds a fresh chain under it (#211). A chain that is not actually
        /// sounding is disposed outright — there is nothing to fade.
        /// </summary>
        private void RetireVoice(float seconds)
        {
            if (_voice != null && _voice.State == SoundState.Playing)
            {
                //One slot is enough for the flows the menus have: a previous occupant is let go where it stands
                _retiring?.Dispose();

                _retiring = _voice;
                _retiringFade.Reset();
                _retiringFade.To(0f, seconds);
            }
            else
            {
                _voice?.Dispose();
            }

            _voice = null;
            _sounding = null;
        }

        /// <summary>
        /// Builds the chain's head from the slot's next recording and starts it. Everything that reaches here is a
        /// level opening, which is why the variant counter moves here and nowhere else. A recording still loading
        /// (the first seconds of a session only) leaves the head unbuilt and the counter unmoved, and Update
        /// retries every frame; an unreadable one is skipped for the next.
        /// </summary>
        private void Advance()
        {
            try
            {
                Task<byte[]>[] variants = _tracks[(int)_theme];

                for (int tried = 0; tried < variants.Length; tried++)
                {
                    int variant = _nextVariant[(int)_theme];
                    Task<byte[]> track = variants[variant];

                    if (!track.IsCompleted) return;

                    _nextVariant[(int)_theme] = (variant + 1) % variants.Length;

                    if (track.Result == null) continue;

                    DynamicSoundEffectInstance old = _voice;

                    _voice = new DynamicSoundEffectInstance(SAMPLE_RATE, AudioChannels.Stereo);
                    _voice.Volume = ThemeVolume;
                    _voice.SubmitBuffer(track.Result);
                    _sounding = track;

                    //Disposed only once the replacement exists, so a failure part-way leaves the old chain playable
                    old?.Dispose();

                    _voice.Play();

                    //Once per level opening, beside the "[levels] Loaded" line — the only way to tell from a log
                    //which of a slot's recordings a level got
                    Console.WriteLine($"[music] {_theme}: {_trackNames[(int)_theme][variant]}");
                    return;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[music] the theme could not be realized, playing on without it: {exception.Message}");
                _failed = true;
            }
        }

        /// <summary>
        /// Decodes one track on a background thread; null, logged, when the file cannot be played. Every track is
        /// started at construction, so they decode side by side while the splash is up — see
        /// <c>Tools/MusicBake --tracks</c> for what that costs.
        /// </summary>
        private static Task<byte[]> Load(string path) => Task.Run(() =>
        {
            try
            {
                return OggTrack.Decode(path, SAMPLE_RATE);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[music] '{Path.GetFileName(path)}' could not be read: {exception.Message}");
                return null;
            }
        });

        public void Dispose()
        {
            _failed = true;   //so a late Update cannot resurrect it
            _wanted = false;
            _menuWanted = false;

            _voice?.Dispose();
            _retiring?.Dispose();
            _menu?.Dispose();
            _menuTrack?.Dispose();
            _fanfares.Dispose();
        }
    }
}
