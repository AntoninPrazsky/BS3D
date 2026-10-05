using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
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
    /// <para>
    /// <b>A recording with a drums layer is fed in chunks and mixed (#495).</b> When <c>Music/Drums/</c> holds a file of
    /// the recording's own name (<see cref="DrumLayer"/>), the chain is not handed the whole loop again and again: it is
    /// fed <see cref="LAYER_CHUNK_FRAMES"/> at a time, each chunk the recording with its drums at the gain
    /// <see cref="Intensity"/> asks for, ramped across the chunk. A recording without one plays exactly as it always did.
    /// </para>
    /// </summary>
    public sealed class GameMusic : IDisposable
    {
        private const string MUSIC_DIRECTORY = "Music";
        private const string MENU_TRACK = "menu";

        /// <summary>The pause's own loop (#668): <c>Music/pause.ogg</c>, kept out of the families like the menu's.</summary>
        private const string PAUSE_TRACK = "pause";
        private const string TRACK_EXTENSION = ".ogg";

        /// <summary>The rate ACE-Step renders at and every track is written at; the procedural pieces are 44.1 kHz.</summary>
        private const int SAMPLE_RATE = 48000;

        /// <summary>Where a recording's drums layer lives (#495): a folder under the tracks', so the families never count it.</summary>
        private const string DRUMS_DIRECTORY = "Drums";

        /// <summary>
        /// How much of a layered recording one chunk of the feed is (#495): a quarter of a second, and
        /// <see cref="LAYER_CHUNKS_AHEAD"/> of them kept queued — two seconds, which is what the voice plays on through a
        /// stall before it would run dry. The whole-loop feed it replaces could not run dry at all, and a layered chain
        /// is started in the middle of a level's build, before the physics, the cluster and the first frame (the review
        /// of #495), so the margin is generous; the price is that a change of <see cref="Intensity"/> is heard up to two
        /// seconds late, which the slow ramps below make little of.
        /// </summary>
        private const int LAYER_CHUNK_FRAMES = 12000;
        private const int LAYER_CHUNKS_AHEAD = 8;

        //How fast the drums follow the danger, in units of full level a second: up in about two seconds - the cluster
        //coming down is heard promptly - and back down over five, so a good shot is a slow exhale rather than a cut.
        private const float DRUMS_RISE_PER_SECOND = 0.5f;
        private const float DRUMS_FALL_PER_SECOND = 0.2f;

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

        /// <summary>
        /// The pause's loop (#668), at the lobby's level: the owner asked for "calming lobby/elevator music", and the
        /// lobby is the one piece already tuned to sit under a menu rather than to carry a level.
        /// </summary>
        public const float PAUSE_VOLUME = MENU_VOLUME;

        /// <summary>
        /// How long the level's theme takes to step aside for the pause, and the pause's loop to arrive — and the
        /// same back again on resume (#668). The lobby's own lengths: a pause is a menu opening, and it should feel
        /// as prompt as one. The theme is not stopped, only faded: it carries on unheard and returns where it was,
        /// exactly as it does for the About page's player (<see cref="Yielding"/>).
        /// </summary>
        private const float PAUSE_FADE_SECONDS = MENU_FADE_SECONDS;

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
        //owner's chosen renders, at the effects' 44.1 kHz
        private const string SFX_DIRECTORY = "Sfx";
        private const string VICTORY_FILE = "victory-fanfare";
        private Task<float[]> _victoryLoad;

        //The defeat's, the owner's pick of nine renders (#446), the same way
        private const string DEFEAT_FILE = "defeat-fanfare";
        private Task<float[]> _defeatLoad;

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

            /// <summary>Each recording's drums layer (#495), or null where it has none; decoded on demand beside it.</summary>
            public string[] DrumFiles;
            public Task<byte[]>[] DrumLoads;
        }

        private readonly Dictionary<string, Family> _families = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The families on disk, in the order the Settings row cycles them and the fallback rotation walks them:
        /// the five pieces the game shipped with first, in the order their <see cref="MusicTheme"/> slots had (so a
        /// level that names nothing still opens on Pulse, as it always has) — the Tower's <c>summit</c> in the second
        /// slot since #280, where the <c>bohemia</c> family it replaced stood — then every other family by name.
        /// </summary>
        public string[] Families { get; }

        private static readonly string[] FIRST_FAMILIES = { "pulse", "summit", "nocturne", "mural", "ember" };

        /// <summary>The family a level asked for, and the recording it pinned within it — or −1 to rotate.</summary>
        private Family _family;
        private int _pinned = -1;

        private DynamicSoundEffectInstance _voice;

        /// <summary>The recording the sounding chain was built from, which the feed submits again — never a sibling variant.</summary>
        private Task<byte[]> _sounding;

        //The sounding chain's drums layer (#495), null when its recording has none and the feed submits it whole; where
        //the next chunk starts, the drums' gain at the end of the last chunk, and the ring the chunks are written into
        private byte[] _soundingDrums;
        private int _layerCursor;
        private float _layerGain = 1f;
        //One chunk's scratch, reused: MonoGame's SubmitBuffer copies what it is handed into a pooled buffer of its own
        //before XAudio2 reads it (read out of its IL by the review of #495), so a chunk can be rewritten the moment it
        //is submitted. The first cut kept a ring of twelve on the belief that the array itself was pinned.
        private readonly byte[] _layerChunk = new byte[LAYER_CHUNK_FRAMES * 4];
        private float _intensity = 1f;

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

        //The pause's loop (#668), shaped exactly like the lobby's above, and the fade the level's theme is put under
        //while it plays — the theme's own, so the lobby and the About page's yielding are left alone
        private Task<byte[]> _pauseLoad;
        private SoundEffect _pauseTrack;
        private SoundEffectInstance _pause;
        private bool _pausing;
        private readonly MusicFade _pauseFade = new();
        private readonly MusicFade _pauseDuck = new();

        /// <summary>Everything this class plays, stepping aside while the About page's player holds a piece.</summary>
        private readonly MusicFade _yield = new();

        private float _gain = 1f;

        public GameMusic()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, MUSIC_DIRECTORY);

            //The lobby first: it is what the splash hands over to, and every load below queues on the same pool —
            //on a machine with fewer cores than tracks, whatever is handed over last decodes last
            _menuLoad = Load(Path.Combine(directory, MENU_TRACK + TRACK_EXTENSION));

            //The pause's loop after it (#668). A missing file is not an error: the pause then keeps the theme, as it
            //always did, rather than falling silent
            string pause = Path.Combine(directory, PAUSE_TRACK + TRACK_EXTENSION);
            if (File.Exists(pause)) _pauseLoad = Load(pause);

            string victory = Path.Combine(AppContext.BaseDirectory, SFX_DIRECTORY, VICTORY_FILE + TRACK_EXTENSION);
            if (File.Exists(victory)) _victoryLoad = LoadVictory(victory);

            string defeat = Path.Combine(AppContext.BaseDirectory, SFX_DIRECTORY, DEFEAT_FILE + TRACK_EXTENSION);
            if (File.Exists(defeat)) _defeatLoad = LoadVictory(defeat);

            //Every recording in the folder, grouped into families by the name before its first dash (#486): the
            //file names only — nothing is decoded until a family is asked for.
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory, "*" + TRACK_EXTENSION);
                Array.Sort(files, StringComparer.Ordinal);

                Dictionary<string, List<string>> grouped = new(StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    if (string.Equals(stem, MENU_TRACK, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(stem, PAUSE_TRACK, StringComparison.OrdinalIgnoreCase)) continue;

                    int dash = stem.IndexOf('-');
                    string family = dash < 0 ? stem : stem.Substring(0, dash);

                    if (!grouped.TryGetValue(family, out List<string> members)) grouped[family] = members = new List<string>();
                    members.Add(file);
                }

                foreach ((string family, List<string> members) in grouped)
                {
                    //The family's bare file first, then its variants: an ordinal sort puts "ember-punk-01.ogg" before
                    //"ember.ogg" ('-' sorts under '.'), and a fresh launch is meant to open a chapter on the bare one
                    members.Sort((a, b) =>
                    {
                        bool bareA = string.Equals(Path.GetFileNameWithoutExtension(a), family, StringComparison.OrdinalIgnoreCase);
                        bool bareB = string.Equals(Path.GetFileNameWithoutExtension(b), family, StringComparison.OrdinalIgnoreCase);
                        return bareA == bareB ? string.CompareOrdinal(a, b) : bareA ? -1 : 1;
                    });

                    string[] names = new string[members.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = Path.GetFileName(members[i]);

                    //And each recording's drums layer, where there is one (#495)
                    string[] drums = new string[members.Count];
                    for (int i = 0; i < drums.Length; i++)
                    {
                        string layer = Path.Combine(directory, DRUMS_DIRECTORY, names[i]);
                        if (File.Exists(layer)) drums[i] = layer;
                    }

                    _families[family] = new Family
                    {
                        Name = family.ToLowerInvariant(),
                        Files = members.ToArray(),
                        Names = names,
                        Loads = new Task<byte[]>[members.Count],
                        DrumFiles = drums,
                        DrumLoads = new Task<byte[]>[members.Count],
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
        /// How much danger the level is in, 0 to 1 (#495): the session sets it every frame from how close the cluster
        /// hangs to the line, and a recording with a drums layer plays its drums from <see cref="DrumLayer.CALM_GAIN"/> at 0 to
        /// full at 1, following at <c>DRUMS_RISE_PER_SECOND</c> and <c>DRUMS_FALL_PER_SECOND</c> of the music's own
        /// time. 1 until anything says otherwise, so music nobody asks about plays as recorded.
        /// </summary>
        public float Intensity
        {
            get => _intensity;
            set => _intensity = Math.Clamp(value, 0f, 1f);
        }

        /// <summary>The drums' gain the current <see cref="Intensity"/> asks for.</summary>
        private float DrumsTarget => DrumLayer.CALM_GAIN + (1f - DrumLayer.CALM_GAIN) * _intensity;

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
        /// The pause's music (#668): true puts the level's theme under a fade and brings the pause's own calm loop in,
        /// false the reverse. Asked every frame from the stack, like <see cref="Yielding"/>, so every way into and out
        /// of the pause — Escape, the page's Resume, a Restart, a Main Menu — reaches it without anyone remembering to
        /// say so. The theme is only faded, never stopped, so it carries on unheard and resumes where it stood; a
        /// Restart keeps the same chain (a retry is the same music, <see cref="SetFamily"/>) and so resumes too. The
        /// loop starts from its head each time: a fully faded stop rewinds it. Without the file the theme simply plays
        /// on under the pause, as it did before #668.
        /// </summary>
        public bool Pausing
        {
            set
            {
                if (value == _pausing) return;
                _pausing = value;

                if (_pauseTrack == null && _pauseLoad == null) return;

                _pauseDuck.To(value ? 0f : 1f, PAUSE_FADE_SECONDS);

                if (!value)
                {
                    _pauseFade.To(0f, PAUSE_FADE_SECONDS);
                    return;
                }

                if (_pause == null) return;   //still loading: Update starts it the frame it lands, if still wanted

                if (_pause.State == SoundState.Playing) _pauseFade.To(1f, PAUSE_FADE_SECONDS);
                else
                {
                    _pauseFade.Arrive(PAUSE_FADE_SECONDS);
                    _pause.Volume = PauseVolume;
                    _pause.Play();
                }
            }
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
            //naming the polka gets the family rather than whatever its position happens to rotate to. The Tower's
            //bohemia family went the same way in #280 (it read as brass-band folk), and its levels play summit.
            //A level that names no music at all (a map saved by the editor, a test level) reaches here with null, and
            //StartsWith on it threw out of the game loop from 7a8ff7ce until the null was checked
            if (name == "dechovka") name = "mural";
            else if (name == "bohemia" || name?.StartsWith("bohemia-", StringComparison.Ordinal) == true) name = "summit";

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

            if (_family != null && _family != family)
            {
                Array.Clear(_family.Loads);
                Array.Clear(_family.DrumLoads);
            }

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
            _soundingDrums = null;

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

        /// <summary>The defeat fanfare: the recording when the file is there (#446), else the bake — see <see cref="ProceduralMusic.PlayDefeat"/>.</summary>
        public void PlayDefeat(int score) => _fanfares.PlayDefeat(score);

        /// <summary>Retires whatever fanfare is sounding — see <see cref="ProceduralMusic.StopFanfare"/>.</summary>
        public void StopFanfare() => _fanfares.StopFanfare();

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
                Task<float[]> ready = _victoryLoad;
                _victoryLoad = null;
                if (ready.Result != null) _fanfares.SetVictoryRecording(ready.Result);
            }

            if (_defeatLoad != null && _defeatLoad.IsCompleted)
            {
                Task<float[]> ready = _defeatLoad;
                _defeatLoad = null;
                if (ready.Result != null) _fanfares.SetDefeatRecording(ready.Result);
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

            if (_pauseLoad != null && _pauseLoad.IsCompleted)
            {
                Task<byte[]> ready = _pauseLoad;
                _pauseLoad = null;

                //Guarded like the lobby: a pause loop that cannot play leaves the theme to play under the pause
                try
                {
                    if (ready.Result != null)
                    {
                        _pauseTrack = new SoundEffect(ready.Result, SAMPLE_RATE, AudioChannels.Stereo);
                        _pause = _pauseTrack.CreateInstance();
                        _pause.IsLooped = true;
                        _pause.Volume = 0f;

                        if (_pausing)
                        {
                            _pauseFade.Arrive(PAUSE_FADE_SECONDS);
                            _pause.Volume = PauseVolume;
                            _pause.Play();
                        }
                    }
                    else _pauseDuck.To(1f, PAUSE_FADE_SECONDS);
                }
                catch (Exception exception)
                {
                    _pause = null;
                    _pauseDuck.To(1f, PAUSE_FADE_SECONDS);
                    Console.WriteLine($"[music] the pause loop could not be realized: {exception.Message}");
                }
            }

            if (!_wanted) return;

            if (_voice == null)
            {
                Advance();
                return;
            }

            if (_soundingDrums != null)
            {
                try
                {
                    FeedLayered();
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the theme could not be queued again, playing on without it: {exception.Message}");
                    _failed = true;
                }
            }
            else if (_sounding != null && _voice.PendingBufferCount < 2)
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

        private float ThemeVolume => MUSIC_VOLUME * _gain * _themeFade.Applied * _yield.Applied * _pauseDuck.Applied;
        private float MenuVolume => MENU_VOLUME * _gain * _menuFade.Applied * _yield.Applied;
        private float RetiringVolume => MUSIC_VOLUME * _gain * _retiringFade.Applied * _yield.Applied * _pauseDuck.Applied;
        private float PauseVolume => PAUSE_VOLUME * _gain * _pauseFade.Applied * _yield.Applied;

        private void WriteVolumes()
        {
            if (_voice != null) _voice.Volume = ThemeVolume;
            if (_menu != null) _menu.Volume = MenuVolume;
            if (_retiring != null) _retiring.Volume = RetiringVolume;
            if (_pause != null) _pause.Volume = PauseVolume;
        }

        /// <summary>
        /// The fades, walked once a frame. Volumes are written only on the frames a fade moved them, and an
        /// instance that has arrived at silence is stopped — or, for a retired chain, disposed — right here.
        /// </summary>
        private void AdvanceFades(float elapsed)
        {
            bool yieldMoved = _yield.Advance(elapsed);
            bool duckMoved = _pauseDuck.Advance(elapsed);

            //The pause's loop, on the lobby's shape: stopped (and so rewound) once it has faded to silence
            if ((_pauseFade.Advance(elapsed) | yieldMoved) && _pause != null)
            {
                _pause.Volume = PauseVolume;
                if (_pauseFade.Silent) _pause.Stop();
            }

            //A non-short-circuit | so every fade walks its frame whatever the other one did
            if ((_menuFade.Advance(elapsed) | yieldMoved) && _menu != null)
            {
                _menu.Volume = MenuVolume;
                if (_menuFade.Silent) _menu.Stop();
            }

            //Same shape as the menu's block above, for the sounding chain's own arrival (#456)
            if ((_themeFade.Advance(elapsed) | yieldMoved | duckMoved) && _voice != null) _voice.Volume = ThemeVolume;

            if ((_retiringFade.Advance(elapsed) | yieldMoved | duckMoved) && _retiring != null)
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
            _soundingDrums = null;
        }

        /// <summary>
        /// The layered feed (#495): chunks of the recording mixed with its drums at the gain <see cref="Intensity"/>
        /// asks for, queued until <see cref="LAYER_CHUNKS_AHEAD"/> wait on the voice. The gain moves towards its
        /// target by the chunk's own length of time at the rise or fall rate, ramped across the chunk, so how fast the
        /// drums come and go is the music's time and not the frame rate's.
        /// </summary>
        private void FeedLayered()
        {
            byte[] full = _sounding.Result;
            float seconds = LAYER_CHUNK_FRAMES / (float)SAMPLE_RATE;

            //Bounded as well as counted: a voice whose pending count lagged its submits would otherwise spin here for good
            for (int queued = 0; queued < LAYER_CHUNKS_AHEAD && _voice.PendingBufferCount < LAYER_CHUNKS_AHEAD; queued++)
            {
                float target = DrumsTarget;
                float from = _layerGain;
                float to = target > from
                    ? Math.Min(target, from + DRUMS_RISE_PER_SECOND * seconds)
                    : Math.Max(target, from - DRUMS_FALL_PER_SECOND * seconds);

                _layerCursor = DrumLayer.Mix(full, _soundingDrums, _layerCursor, LAYER_CHUNK_FRAMES, from, to, _layerChunk);
                _layerGain = to;

                _voice.SubmitBuffer(_layerChunk, 0, _layerChunk.Length);
            }
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

                    //Its drums layer beside it (#495), when it has one
                    Task<byte[]> drums = family.DrumFiles[variant] == null
                        ? null
                        : family.DrumLoads[variant] ??= Load(family.DrumFiles[variant]);

                    //And the one after it starts decoding now, so the chapter's next level finds it ready (#486)
                    if (_pinned < 0 && family.Files.Length > 1)
                    {
                        int following = (variant + 1) % family.Files.Length;
                        family.Loads[following] ??= Load(family.Files[following]);
                        if (family.DrumFiles[following] != null) family.DrumLoads[following] ??= Load(family.DrumFiles[following]);
                    }

                    if (!track.IsCompleted || drums?.IsCompleted == false) return;

                    if (_pinned < 0) family.Next = (variant + 1) % family.Files.Length;

                    //And every other recording of the family is let go - all but this one and the one after it (the
                    //review of #495): nothing else freed a variant once it had played, so a chapter of ten levels on one
                    //family ended up holding all ten decoded, and with their drums layers the Meadow's would be some
                    //90 MB more. A chain that is still sounding keeps nothing of these: SubmitBuffer copied what it played.
                    for (int other = 0; other < family.Files.Length; other++)
                    {
                        if (other == variant || (_pinned < 0 && other == (variant + 1) % family.Files.Length)) continue;
                        family.Loads[other] = null;
                        family.DrumLoads[other] = null;
                    }

                    if (track.Result == null)
                    {
                        if (_pinned >= 0) return;   //a pinned file that cannot be read stays silent, as logged
                        continue;
                    }

                    //Once per level opening, beside the "[levels] Loaded" line — the only way to tell from a log
                    //which of a family's recordings a level got. Before the voice is built rather than after it
                    //plays, so the record stands even on a machine whose audio device is missing (the desktop's
                    //monitor asleep takes its HDMI endpoint with it, and XAudio2 then has no device to make a
                    //voice on) — the one case the catch below exists for.
                    //A layer that could not be read, or is not its recording's length, is dropped: the recording plays whole
                    byte[] layer = drums?.Result;
                    if (layer != null && layer.Length != track.Result.Length)
                    {
                        Console.WriteLine($"[music] the drums of {family.Names[variant]} are not its length, playing it without them");
                        layer = null;
                    }

                    Console.WriteLine($"[music] {family.Name}: {family.Names[variant]}{(layer != null ? " (drums layered)" : "")}");

                    DynamicSoundEffectInstance old = _voice;

                    //Arrived before the volume is read (#456), so the first sample this chain ever plays is
                    //already at the ramp's own start rather than at ThemeVolume's full figure — a fresh chain
                    //is exactly what an arrival is, whether it is level one's own or a switch's (RetireVoice
                    //has already moved whatever was sounding off to _retiring, which fades on its own terms).
                    _voice = new DynamicSoundEffectInstance(SAMPLE_RATE, AudioChannels.Stereo);
                    //Said outright, not left to the default: on OpenAL (GamePi, #790) a new stream is handed a recycled
                    //source that still holds the last voice's AL_PITCH (a pitched-down shot left 0.616 on it, measured),
                    //and the theme would play that much slower and lower. XAudio2's new voice is at 0 anyway.
                    _voice.Pitch = 0f;
                    _themeFade.Arrive(THEME_ARRIVAL_SECONDS);
                    _voice.Volume = ThemeVolume;
                    _sounding = track;
                    _soundingDrums = layer;

                    if (layer != null)
                    {
                        //A fresh chain starts where the danger already is rather than ramping to it from full
                        _layerCursor = 0;
                        _layerGain = DrumsTarget;
                        FeedLayered();
                    }
                    else _voice.SubmitBuffer(track.Result);

                    //Disposed only once the replacement exists, so a failure part-way leaves the old chain playable
                    old?.Dispose();

                    _voice.Play();
                    return;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[music] the theme could not be realized (no audio device?), playing on without it: {exception.Message}");
                _failed = true;
            }
        }

        /// <summary>
        /// The recording as the fanfare player takes it: interleaved stereo floats at <see cref="ProceduralMusic.SAMPLE_RATE"/>
        /// (the effects' rate, not the tracks'). Null when the file cannot be read, and the bake stands. The ROOT and
        /// BPM tags <c>Tools/MusicBake --sfx --music</c> writes are no longer read: they tuned the star chime to the
        /// recording (#158), and the chime has had a fixed key of its own since #613.
        /// </summary>
        private static Task<float[]> LoadVictory(string path) => Task.Run(() =>
        {
            try
            {
                byte[] pcm = OggTrack.Decode(path, ProceduralMusic.SAMPLE_RATE);
                float[] samples = new float[pcm.Length / 2];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8)) / 32768f;
                return samples;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[music] the fanfare recording could not be read: {exception.Message}");
                return null;
            }
        });

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
            _pause?.Dispose();
            _pauseTrack?.Dispose();
            _fanfares.Dispose();
        }
    }
}
