using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace BS3D.Audio
{
    /// <summary>
    /// The game's music since #443: generated recordings (ACE-Step 1.5, see <c>Research/AI-Music</c>), seamless
    /// loops read from <c>Music/*.ogg</c> beside the executable, plus the front end's own loop. The procedural
    /// compositions that played here until then are the About page's now (<see cref="ProceduralJukebox"/>); the
    /// fanfares stayed procedural and are still baked by <see cref="ProceduralMusic"/>, which this class owns and
    /// forwards to, so the rest of the game keeps asking one object for its music.
    /// <para>
    /// <b>The recordings are FAMILIES, keyed by name and found on disk (#486).</b> Until then a level's music
    /// named one of five <see cref="MusicTheme"/> slots and each slot's files were its own name plus
    /// <c>name-*.ogg</c> variants — which held for five pieces and one chapter with five variations, and not for
    /// the owner's ask of about ten recordings a chapter, every chapter its own. Now every <c>*.ogg</c> in the
    /// folder belongs to the family its name starts with (<c>ember.ogg</c> and <c>ember-punk-03.ogg</c> are both
    /// <c>ember</c>'s), a level's <c>music</c> string names a family — or one recording by its file's stem, which
    /// pins it — and a family with more than one recording rotates through them, one per level opening, exactly
    /// as Ember's slot did. A new family is a set of files and a name in <c>Tools/LevelGen</c>; nothing here
    /// counts them, and <see cref="MusicTheme"/> is the About page's catalogue of the procedural pieces only.
    /// </para>
    /// <para>
    /// <b>A recording is decoded when its family is first asked for, not at the splash.</b> Eleven loops were
    /// ~117 MB of PCM decoded side by side while the splash was up; a hundred would be a gigabyte, most of it
    /// for chapters the session never reaches. So a family's recordings are decoded on demand — the one about to
    /// play and the one after it, so a chapter's second level finds its next recording ready — and let go when
    /// another family takes over. The first level of a session opens its theme a fraction of a second late on
    /// the desktop, under the chapter intro's tour, and the arrival fade (#456) covers the rest.
    /// </para>
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
    /// their values: the mix the effects were tuned against did not move — until #467 raised the music, see
    /// <see cref="MUSIC_VOLUME"/>.
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
        /// The authored level of the music, under the effects — a soundtrack is not an event. A constant so the
        /// balance keeps its tuning; the player's settings rows scale it through <see cref="Gain"/>.
        /// <para>
        /// <b>0.34 → 0.5 (+3.4 dB) in #467</b>, on the owner's ear: the music sat noticeably under the effects with
        /// every row at 100 %, and the rows only attenuate, so he had no lever. The 0.34 was tuned against the
        /// procedural score, and the generated tracks (#443) were brought to the same RMS — but a full-band mix
        /// cut from a render and a sparse synthetic piece at one RMS are not one loudness, and RMS is all the
        /// bakery measures. The first of the two steps the issue proposes (0.5, then 0.7); the ear decides the
        /// second. A theme peaks under full scale, so it cannot clip alone at any gain up to 1 — the sum with a
        /// big release is what to listen for.
        /// </para>
        /// </summary>
        public const float MUSIC_VOLUME = 0.5f;

        /// <summary>The front end's loop, under even the theme: a lobby, not a dancefloor. Raised with the theme in
        /// #467 by the same ratio (0.2 → 0.29), so the lobby-to-level step #456 fades is what it was.</summary>
        public const float MENU_VOLUME = 0.29f;

        //How long a piece the player is walking away from takes to leave (#211): the theme can be left mid-chorus
        //and is the widest thing here to put down gently, while leaving the lobby should feel prompt — it is a
        //level starting.
        private const float THEME_FADE_SECONDS = 0.9f;
        private const float MENU_FADE_SECONDS = 0.5f;

        //How long the ARRIVING side ramps in (#456). Until this the arrival never ramped at all — "fading the
        //outgoing side alone already is the crossfade" — which was true only of a HAND-OVER between two
        //pieces; the first level's own arrival has nothing leaving under it to make the switch read as one,
        //and a generated loop (#443) is cut from a render's body with no prelude, so what a fresh chain's
        //first sample gave was a full-band groove at once. The theme's own arrival runs a little LONGER than
        //the lobby's leaving, so the two overlap as a real cross-fade rather than meeting at a gap; the
        //lobby's own arrival is about as long as its own leaving. Lengths are a starting point, not a
        //measurement — the owner's ear decides (#456).
        private const float THEME_ARRIVAL_SECONDS = 1.2f;
        private const float MENU_ARRIVAL_SECONDS = 0.5f;

        /// <summary>
        /// How long the game's music takes to step aside for the About page's player, and to come back after it.
        /// A little longer than the lobby's leaving: it is a record being lifted off, not a level starting.
        /// </summary>
        private const float YIELD_FADE_SECONDS = 0.7f;


        private readonly ProceduralMusic _fanfares = new();

        //The victory fanfare as a recording (#482), from the effects' folder rather than the music's: it is one of the
        //owner's chosen renders, at the effects' 44.1 kHz, and its ROOT and BPM tags are what the star chime tunes to
        private const string SFX_DIRECTORY = "Sfx";
        private const string VICTORY_FILE = "victory-fanfare";
        private Task<(float[] Pcm, ProceduralMusic.FanfareShape Shape)> _victoryLoad;

        /// <summary>
        /// One family of recordings (#486): the files that share a name before the first dash, the family's own
        /// bare file first and then its variants in name order, each decoded on demand into <see cref="Loads"/>
        /// (null until asked for; a task that finished with null is a file that could not be read, already logged).
        /// <see cref="Next"/> is which recording the next chain head plays: a family with several moves on every
        /// time a head is built for it, and a head is built exactly when a level opens (a build, a retry, a switch
        /// of piece, a Continue), so "each start of a level in this chapter plays the next one" is this one counter.
        /// </summary>
        private sealed class Family
        {
            public string Name;
            public string[] Files;
            public string[] Names;
            public Task<byte[]>[] Loads;
            public int Next;
        }

        private readonly Dictionary<string, Family> _families = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The families on disk, in the order the Settings row cycles them and the fallback rotation walks them:
        /// the five pieces the game shipped with first, in the order their <see cref="MusicTheme"/> slots had (so a
        /// level that names nothing still opens on Pulse, as it always has), then every other family by name.
        /// </summary>
        public string[] Families { get; }

        private static readonly string[] FIRST_FAMILIES = { "pulse", "bohemia", "nocturne", "mural", "ember" };

        /// <summary>The family a level asked for, and the recording it pinned within it — or −1 to rotate.</summary>
        private Family _family;
        private int _pinned = -1;

        private DynamicSoundEffectInstance _voice;

        /// <summary>The recording the sounding chain was built from, which the feed submits again — never a sibling variant.</summary>
        private Task<byte[]> _sounding;

        private bool _wanted;
        private bool _failed;

        /// <summary>A chain on its way out (#211), faded and disposed at silence; see <see cref="RetireVoice"/>.</summary>
        private DynamicSoundEffectInstance _retiring;
        private readonly MusicFade _retiringFade = new();

        /// <summary>
        /// The SOUNDING chain's own arrival (#456) — <see cref="Advance"/> sets it running every time it builds
        /// a fresh <see cref="_voice"/>, and only then: the feed's resubmit path in <see cref="Update"/> never
        /// touches it, so a loop's own wrap is untouched, exactly as the issue's exception asks.
        /// </summary>
        private readonly MusicFade _themeFade = new();

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

            string victory = Path.Combine(AppContext.BaseDirectory, SFX_DIRECTORY, VICTORY_FILE + TRACK_EXTENSION);
            if (File.Exists(victory)) _victoryLoad = LoadVictory(victory);

            //Every recording in the folder, grouped into families by the name before its first dash (#486): the
            //file names only — nothing is decoded until a family is asked for. Sorted ordinally, so a family's
            //bare file ("ember") stands before its variants ("ember-punk-01") and the variants keep their order.
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory, "*" + TRACK_EXTENSION);
                Array.Sort(files, StringComparer.Ordinal);

                Dictionary<string, List<string>> grouped = new(StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    if (string.Equals(stem, MENU_TRACK, StringComparison.OrdinalIgnoreCase)) continue;

                    int dash = stem.IndexOf('-');
                    string family = dash < 0 ? stem : stem.Substring(0, dash);

                    if (!grouped.TryGetValue(family, out List<string> members)) grouped[family] = members = new List<string>();
                    members.Add(file);
                }

                foreach ((string family, List<string> members) in grouped)
                {
                    string[] names = new string[members.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = Path.GetFileName(members[i]);

                    _families[family] = new Family
                    {
                        Name = family.ToLowerInvariant(),
                        Files = members.ToArray(),
                        Names = names,
                        Loads = new Task<byte[]>[members.Count],
                    };
                }
            }

            if (_families.Count == 0) Console.WriteLine($"[music] no recordings in '{directory}'");

            List<string> order = new();
            foreach (string first in FIRST_FAMILIES) if (_families.ContainsKey(first)) order.Add(first);
            List<string> rest = new();
            foreach (Family family in _families.Values) if (!order.Contains(family.Name)) rest.Add(family.Name);
            rest.Sort(StringComparer.Ordinal);
            order.AddRange(rest);
            Families = order.ToArray();
        }

        /// <summary>True while a fanfare is sounding; the host ducks the fireworks under it.</summary>
        public bool IsFanfarePlaying => _fanfares.IsFanfarePlaying;

        /// <summary>
        /// Which family the moment is sounding, and <b>null while it is not one</b>: the front end's own loop is
        /// playing, or the music has failed. It reads what is WANTED, so a switch caught mid-fade already names
        /// the arriving family — which is what the track picker in Settings (#279) shows.
        /// </summary>
        public string SoundingTrack => _failed || _menuWanted ? null : _family?.Name;

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
        /// What a level asks for by name, falling back to the pool's own rotation. Resolved rather than cast: the
        /// level file is hand-editable, and an unknown spelling has to mean "the default", not an exception. A name
        /// is first a <b>recording</b> — <c>ember-punk-03</c> pins that one file, which is how a level gets music of
        /// its own — and then a <b>family</b>, which rotates. <paramref name="index"/> picks when nothing is named
        /// (the level's place in its set), which is why level one still opens on Pulse.
        /// </summary>
        public void SetTheme(string named, int index)
        {
            if (Families.Length == 0) return;

            string name = named?.Trim().ToLowerInvariant();

            //The old name of Mural's slot — #264 replaced the piece, not the slot, and a hand-edited file still
            //naming the polka gets the family rather than whatever its position happens to rotate to
            if (name == "dechovka") name = "mural";

            if (!string.IsNullOrEmpty(name))
            {
                int dash = name.IndexOf('-');
                string familyName = dash < 0 ? name : name.Substring(0, dash);

                if (_families.TryGetValue(familyName, out Family family))
                {
                    int pinned = -1;
                    if (dash >= 0)
                    {
                        for (int i = 0; i < family.Names.Length; i++)
                            if (string.Equals(Path.GetFileNameWithoutExtension(family.Names[i]), name, StringComparison.OrdinalIgnoreCase))
                                pinned = i;

                        //A pinned recording that is not there: the family plays on, rotating, and the log says so
                        if (pinned < 0) Console.WriteLine($"[music] no recording '{name}', playing the {familyName} family instead");
                    }

                    SetFamily(family, pinned);
                    return;
                }

                Console.WriteLine($"[music] no family '{familyName}', falling back to the rotation");
            }

            SetFamily(_families[Families[((index % Families.Length) + Families.Length) % Families.Length]], -1);
        }

        /// <summary>A family by name, rotating — the Settings row's pick (#279). An unknown name is ignored.</summary>
        public void SetTheme(string family)
        {
            if (family != null && _families.TryGetValue(family, out Family found)) SetFamily(found, -1);
        }

        /// <summary>
        /// Which family plays, and which of its recordings if one is pinned. The sounding chain is retired by a
        /// fade (#211) — what is queued on it belongs to the piece being left — and the next <see cref="Update"/>
        /// builds the new one underneath it. The family being left lets go of its decoded recordings (#486): the
        /// retiring chain already holds the buffer it is fading out, and a chapter come back to decodes again.
        /// </summary>
        private void SetFamily(Family family, int pinned)
        {
            if (_failed) return;

            //The same family again with the same pin is the same music — a retry, a Continue — and keeps its chain
            if (family == _family && pinned == _pinned) return;

            if (_family != null && _family != family) Array.Clear(_family.Loads);

            _family = family;
            _pinned = pinned;
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
            else if (_menu != null)
            {
                //An arrival (#456): a fully faded stop rewound the loop, which restarts at its head and ramps
                //up rather than snapping to full. Arrive() is called here, at the actual Play(), rather than
                //unconditionally above — the other branch below is the file not having loaded yet, where
                //Update's own arrival covers it the same way once it has, on its own frame rather than this one.
                _menuFade.Arrive(MENU_ARRIVAL_SECONDS);
                _menu.Volume = MenuVolume;
                _menu.Play();
            }

            //else: the file has not loaded yet. Update's load-completion branch plays it once it has, and
            //arrives it there — calling Arrive() here would start the ramp's clock before there is anything
            //sounding to ramp, and a load that outlasts MENU_ARRIVAL_SECONDS would then open at full anyway.
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

            if (_victoryLoad != null && _victoryLoad.IsCompleted)
            {
                Task<(float[] Pcm, ProceduralMusic.FanfareShape Shape)> ready = _victoryLoad;
                _victoryLoad = null;
                if (ready.Result.Pcm != null) _fanfares.SetVictoryRecording(ready.Result.Pcm, ready.Result.Shape);
            }

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

                        //The common case: PlayMenu() was already called, on a file that had not loaded yet, so
                        //its own arrival branch above had nothing to Play() and left this to here (#456) — the
                        //ramp's clock starts NOW, the frame the loop actually starts sounding, not whenever the
                        //splash happened to ask for it.
                        if (_menuWanted)
                        {
                            _menuFade.Arrive(MENU_ARRIVAL_SECONDS);
                            _menu.Volume = MenuVolume;
                            _menu.Play();
                        }
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

        private float ThemeVolume => MUSIC_VOLUME * _gain * _themeFade.Applied * _yield.Applied;
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

            //Same shape as the menu's block above, for the sounding chain's own arrival (#456)
            if ((_themeFade.Advance(elapsed) | yieldMoved) && _voice != null) _voice.Volume = ThemeVolume;

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
            Family family = _family;
            if (family == null) return;

            try
            {
                for (int tried = 0; tried < family.Files.Length; tried++)
                {
                    //A pinned recording is always the one; otherwise the family's counter says which
                    int variant = _pinned >= 0 ? _pinned : family.Next;

                    Task<byte[]> track = family.Loads[variant] ??= Load(family.Files[variant]);

                    //And the one after it starts decoding now, so the chapter's next level finds it ready (#486)
                    if (_pinned < 0 && family.Files.Length > 1)
                    {
                        int following = (variant + 1) % family.Files.Length;
                        family.Loads[following] ??= Load(family.Files[following]);
                    }

                    if (!track.IsCompleted) return;

                    if (_pinned < 0) family.Next = (variant + 1) % family.Files.Length;

                    if (track.Result == null)
                    {
                        if (_pinned >= 0) return;   //a pinned file that cannot be read stays silent, as logged
                        continue;
                    }

                    DynamicSoundEffectInstance old = _voice;

                    //Arrived before the volume is read (#456), so the first sample this chain ever plays is
                    //already at the ramp's own start rather than at ThemeVolume's full figure — a fresh chain
                    //is exactly what an arrival is, whether it is level one's own or a switch's (RetireVoice
                    //has already moved whatever was sounding off to _retiring, which fades on its own terms).
                    _voice = new DynamicSoundEffectInstance(SAMPLE_RATE, AudioChannels.Stereo);
                    _themeFade.Arrive(THEME_ARRIVAL_SECONDS);
                    _voice.Volume = ThemeVolume;
                    _voice.SubmitBuffer(track.Result);
                    _sounding = track;

                    //Disposed only once the replacement exists, so a failure part-way leaves the old chain playable
                    old?.Dispose();

                    _voice.Play();

                    //Once per level opening, beside the "[levels] Loaded" line — the only way to tell from a log
                    //which of a family's recordings a level got
                    Console.WriteLine($"[music] {family.Name}: {family.Names[variant]}");
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
        /// <summary>
        /// The recording as the fanfare player takes it: interleaved stereo floats at <see cref="ProceduralMusic.SAMPLE_RATE"/>
        /// (the effects' rate, not the tracks'), and the ROOT and BPM tags <c>Tools/MusicBake --sfx --music</c> wrote into
        /// the file. Null when the file cannot be read or carries no key: an untuned chime over a recording is the fault
        /// #158 removed, so a recording without its tags is not played at all and the bake stands.
        /// </summary>
        private static Task<(float[] Pcm, ProceduralMusic.FanfareShape Shape)> LoadVictory(string path) => Task.Run(() =>
        {
            try
            {
                if (!int.TryParse(OggTrack.ReadTag(path, "ROOT"), out int root)
                    || !float.TryParse(OggTrack.ReadTag(path, "BPM"), NumberStyles.Float, CultureInfo.InvariantCulture, out float bpm))
                {
                    Console.WriteLine($"[music] the victory recording carries no ROOT/BPM tags, the fanfare stays baked: {path}");
                    return (null, default);
                }

                byte[] pcm = OggTrack.Decode(path, ProceduralMusic.SAMPLE_RATE);
                float[] samples = new float[pcm.Length / 2];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8)) / 32768f;
                return (samples, new ProceduralMusic.FanfareShape(root, bpm, victory: true));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[music] the victory recording could not be read: {exception.Message}");
                return (null, default);
            }
        });

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
