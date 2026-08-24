using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Threading.Tasks;

namespace BS3D.Audio
{
    /// <summary>
    /// Which composition a level plays (#120). Not a style setting and not a mood: each is a separate piece of
    /// music with its own mode, its own tunes and its own form, sharing only this file's instruments and its
    /// one rule — random parameters, never random notes.
    /// <para>
    /// A small curated pool rather than anything generative, deliberately. Every piece is hand-arranged by ear
    /// against measurements, and that craft is exactly what composing from scratch at runtime would lose.
    /// </para>
    /// </summary>
    public enum MusicTheme
    {
        /// <summary>
        /// The original: eurodance, A minor, ten sections, ~2:30 a pass. Level One's theme, and since #186 a
        /// piece with a floor under it (<see cref="SubBass"/>), two sections that are not a dance floor at all,
        /// and one harmonic shock at the last drop.
        /// </summary>
        Pulse,

        /// <summary>
        /// The second (#120): D Dorian over the same dance floor, with a string section, brass pillars and a
        /// tuned drum over it — a statement, a second subject and a coda that brings it back, twelve sections
        /// and ~3:20 a pass.
        /// </summary>
        Bohemia,

        /// <summary>
        /// The third (#162): smooth jazz — sevenths and ninths on a <b>swung</b> grid, a walking bass and a
        /// piano carrying the tune (the Rhodes's seat until #210 — a tine that bright over a quiet trio read
        /// as a glockenspiel), nine sections and ~3:00 a pass. Where the other two differ in mode over
        /// one shared dance floor, this one differs in harmony and in TIME, which is what makes it a third
        /// language rather than a third accent.
        /// </summary>
        Nocturne,

        /// <summary>
        /// The fourth (#264, replacing #164's brass band): a bass-led groove — the tune lives in a melodic
        /// sub (<b>the log drum</b>) on the set's only syncopated grid, the 3+3+2 tresillo, with a marimba
        /// answering from above. Nine sections, ~2:45 a pass, in G major. Where the others differ in mode,
        /// harmony, time or gain, this one differs in <b>where the weight falls and which register carries
        /// the tune</b>: the kick never marks all four beats, and the hook is played by the bass.
        /// </summary>
        Mural,

        /// <summary>
        /// The fifth (#163): a rock ballad — <b>power chords</b> through a distorted guitar, half-time under
        /// the verses and full time in the choruses, ten sections and ~2:25 a pass, in E minor. Where the
        /// other four differ in mode, harmony or time, this one differs in <b>gain</b>: the build is the
        /// amplifier being pushed rather than a fader being raised, and the chords have no third in them
        /// because distortion is what decides which intervals may be played at all.
        /// </summary>
        Ember
    }

    /// <summary>
    /// The level theme: a two-minute eurodance track synthesized from raw PCM and played on a loop. No tracker
    /// file, no asset, no pipeline step — the score is a handful of arrays and the instruments are oscillators,
    /// the same line the sound effects, the meshes and the surface textures all take. The same instruments
    /// also play the result fanfares and the front end's looped piece (see <see cref="BakeMenu"/>).
    /// <para>
    /// It is <b>arranged</b> rather than looped: ten sections of eight bars, each one adding or taking away
    /// parts — a prelude with no drums in it at all, an intro that builds, verses, a chorus that is
    /// unmistakably the chorus, a breeze where the floor drops away and the keys take the tune, a build that
    /// puts everything back, a last chorus arrived at through a silence and one borrowed chord, and an outro
    /// that sheds the kit. A sixteen-bar loop with no sections is a ringtone; what makes a track worth
    /// hearing for two minutes is that it keeps arriving somewhere.
    /// </para>
    /// </summary>
    public sealed class ProceduralMusic : IDisposable
    {
        private const int SAMPLE_RATE = 44100;

        /// <summary>How many compositions there are, read off the enum so adding one needs nothing here.</summary>
        private static readonly int THEME_COUNT = Enum.GetValues(typeof(MusicTheme)).Length;

        //Around 128 rather than the 146 this started at, and rolled per pass inside a narrow band (see
        //Variation). 146 was fast enough to read as frantic; a slower tempo leaves room for the extra
        //percussion and for the melody to breathe — the same notes at 128 sound deliberate where at 146 they
        //sound hurried.
        private const int STEPS_PER_BEAT = 4;      //sixteenths
        private const int STEPS_PER_BAR = 16;

        //Every composition is eight bars to a section; how MANY sections is each one's own business, and is
        //read off its own arrangement table rather than stated here — Pulse is ten (~2:30) and Bohemia twelve
        //(~3:20), and a shared SECTIONS constant is exactly the thing that would have quietly truncated the
        //second one to the first one's length.
        private const int BARS_PER_SECTION = 8;
        private const int STEPS_PER_SECTION = BARS_PER_SECTION * STEPS_PER_BAR;

        /// <summary>
        /// The authored level of the music, well under the effects — a soundtrack is not an event. A constant
        /// so the balance keeps its tuning; the player's settings rows scale it through <see cref="Gain"/>.
        /// </summary>
        public const float MUSIC_VOLUME = 0.34f;

        /// <summary>
        /// The front end's piece, under even the theme: a lobby, not a dancefloor. Scaled by <see cref="Gain"/>
        /// like everything else here.
        /// </summary>
        public const float MENU_VOLUME = 0.2f;

        //The chords available to a progression, all diatonic to A minor so any ordering of them is in key.
        //Each carries its bass root and the four notes the arpeggio and the melody are built from.
        private static readonly int[] CHORD_ROOT = { 45, 41, 48, 43, 50, 52 };   //Am F C G Dm Em

        private static readonly int[][] CHORD_ARP =
        {
            new[] { 57, 60, 64, 69 },   //Am: A3 C4 E4 A4
            new[] { 53, 57, 60, 65 },   //F : F3 A3 C4 F4
            new[] { 60, 64, 67, 72 },   //C : C4 E4 G4 C5
            new[] { 55, 59, 62, 67 },   //G : G3 B3 D4 G4
            new[] { 62, 65, 69, 74 },   //Dm: D4 F4 A4 D5
            new[] { 64, 67, 71, 76 }    //Em: E4 G4 B4 E5
        };

        //The progressions a variation may draw. Every one opens on Am and every one is four bars, so the
        //melodies below fit all of them without knowing which was chosen.
        private static readonly int[][] PROGRESSIONS =
        {
            new[] { 0, 1, 2, 3 },   //Am F  C  G   — vi-IV-I-V, the classic
            new[] { 0, 3, 1, 2 },   //Am G  F  C
            new[] { 0, 4, 1, 3 },   //Am Dm F  G
            new[] { 0, 2, 4, 3 },   //Am C  Dm G
            new[] { 0, 5, 1, 3 }    //Am Em F  G
        };

        /// <summary>
        /// One note of a melody, written as a CHORD TONE rather than as a pitch: <see cref="Tone"/> indexes
        /// the current chord's four notes and <see cref="Octave"/> shifts it in semitones.
        /// <para>
        /// Writing the melodies this way is what lets the progression be chosen at random. A melody written as
        /// absolute pitches only fits the chords it was written over; written as degrees of whatever chord is
        /// underneath, the same motif transposes itself and is consonant by construction — which is also the
        /// literal form of "one motif moved across the chords", the thing that made it a hook in the first
        /// place.
        /// </para>
        /// </summary>
        private readonly struct Note
        {
            public readonly int Step, Tone, Octave, Length;
            public Note(int step, int tone, int octave, int length) { Step = step; Tone = tone; Octave = octave; Length = length; }
        }

        //THE VERSE. Short notes with gaps in them: it has to leave room, both for the arpeggio underneath and
        //so that the chorus has somewhere to go. A verse that is already busy and already high gives the
        //chorus nothing to be bigger than.
        private static readonly Note[] VERSE =
        {
            new(0, 0, 12, 2), new(3, 1, 12, 1), new(4, 2, 12, 2), new(8, 1, 12, 3), new(12, 0, 12, 3)
        };

        //THE CHORUS, and this is what the track exists for: long notes, high, few of them, and a shape that
        //goes somewhere and comes back. Four bars, each its own line, with the peak held back for the fourth —
        //everything about it is the opposite of the verse, because that contrast is what makes a chorus a
        //chorus, not volume and not more notes.
        private static readonly Note[][] CHORUS =
        {
            new[] { new Note(0, 2, 12, 8), new Note(8, 3, 12, 4), new Note(12, 2, 12, 4) },
            new[] { new Note(0, 3, 12, 8), new Note(8, 2, 12, 4), new Note(12, 1, 12, 4) },
            new[] { new Note(0, 2, 12, 8), new Note(8, 3, 12, 4), new Note(12, 3, 12, 4) },
            new[] { new Note(0, 3, 24, 8), new Note(8, 2, 12, 6), new Note(14, 1, 12, 2) }   //the peak, an octave up
        };

        //A counter-line under the chorus, moving where the chorus holds, so the biggest sections are the
        //busiest without the melody itself getting busy.
        private static readonly Note[] COUNTER =
        {
            new(4, 1, 0, 2), new(6, 3, 0, 2), new(14, 2, 0, 2)
        };

        //The turnaround at the end of a section: a descending run down the chord that hands the phrase on.
        private static readonly Note[] FILL =
        {
            new(8, 3, 12, 1), new(10, 2, 12, 1), new(11, 1, 12, 1),
            new(12, 0, 12, 1), new(13, 3, 0, 1), new(14, 2, 0, 1), new(15, 1, 0, 1)
        };

        //THE BREEZE (#186): the piece's second tune, and the reason it exists is that a dance track with one
        //tune has only volume to make a quiet section out of. This one is carried by the KEYS over a pad and
        //the sustained floor, with no kit under it — it opens the piece, it is what the breakdown plays
        //instead of hats, and it walks the outro off, so the ear meets it three times.
        //
        //Everything about it is the chorus's opposite in the way the chorus is the verse's: it sits an octave
        //LOWER (no Octave shift at all, i.e. in the chord's own register), moves by step where the chorus
        //leaps, and the shape is a four-bar sentence — state, the same cell a step higher, an answer that
        //falls, and a held note to rest on. That is the shape Bohemia's theme is built on, and the reason to
        //borrow it here is exactly what that piece proved: a tune with somewhere to get to is what stops a
        //quiet section reading as a hole in the track.
        private static readonly Note[][] BREEZE =
        {
            new[] { new Note(0, 1, 0, 6),  new Note(8, 2, 0, 7) },
            new[] { new Note(0, 2, 0, 6),  new Note(8, 3, 0, 7) },
            new[] { new Note(0, 3, 0, 4),  new Note(6, 2, 0, 5), new Note(12, 1, 0, 4) },
            new[] { new Note(0, 0, 12, 13) }
        };

        /// <summary>Which melody, if any, a section carries.</summary>
        private enum LeadPart { None, Verse, Chorus, Breeze }

        /// <summary>
        /// Everything a single rendering of the track rolls for itself. The point of it is that no two passes
        /// are the same piece of music — a loop the player hears for the length of a level has to stop being a
        /// loop — while every choice stays inside a set that cannot sound wrong: the progressions are all
        /// diatonic, the melodies are chord tones, the extra notes land on the grid.
        /// <para>
        /// Random <i>parameters</i>, never random <i>notes</i>. That distinction is the whole difference
        /// between variation and noise.
        /// </para>
        /// </summary>
        private readonly struct Variation
        {
            public readonly float Bpm;
            public readonly int Transpose;      //semitones, so the key moves between passes
            public readonly int[] Progression;
            public readonly bool ArpDown;       //the arpeggio runs down instead of up
            public readonly float Embellish;    //chance of an extra melody note in a bar
            public readonly float Ghost;        //chance of a ghost snare on an off-beat

            public Variation(Random random)
            {
                //Around the 128 the arrangement was written at. Wide enough to be felt between passes, narrow
                //enough that the track is recognisably the same track.
                Bpm = 122f + (float)random.NextDouble() * 10f;

                //Whole tones and minor thirds only: a random semitone would put successive passes a half-step
                //apart, which is the one interval that sounds like a mistake rather than like a key change.
                int[] keys = { -3, -2, 0, 0, 2, 3, 5 };
                Transpose = keys[random.Next(keys.Length)];

                Progression = PROGRESSIONS[random.Next(PROGRESSIONS.Length)];
                ArpDown = random.NextDouble() < 0.35;
                Embellish = 0.18f + (float)random.NextDouble() * 0.22f;
                Ghost = 0.10f + (float)random.NextDouble() * 0.18f;
            }
        }

        /// <summary>What plays during one eight-bar section. The arrangement IS this table.</summary>
        private readonly struct Section
        {
            public readonly bool Kick, Clap, Hats, Ride, Bass, Sub, Arp, Pad, Roll;
            public readonly LeadPart Lead;
            public readonly float Level;   //overall weight of the section, so a breakdown is quieter as well as emptier

            public Section(bool kick, bool clap, bool hats, bool ride, bool bass, bool sub, bool arp, bool pad,
                bool roll, LeadPart lead, float level)
            {
                Kick = kick; Clap = clap; Hats = hats; Ride = ride; Bass = bass;
                Sub = sub; Arp = arp; Pad = pad; Roll = roll; Lead = lead; Level = level;
            }
        }

        //THE FORM, and #186 is what set it. The complaint was that the piece read as primitive and aimed at
        //small children, and the measurement said where that came from: the kick was on in eight sections of
        //nine — every one but the breakdown — and the sixteenth arpeggio in all nine, so the track ran at full
        //rhythmic density for almost its whole length whatever the Level column said. What was added is not a
        //dial but AIR — a prelude and a breeze that carry no kit at all and a second tune instead of one, an
        //outro that sheds it, and one silence before the last drop. The kick now plays in six sections of ten
        //and half of a seventh.
        //
        //                                  kick   clap   hats   ride   bass    sub    arp    pad   roll  lead              level
        private static readonly Section[] ARRANGEMENT =
        {
            //0 PRELUDE. No kit under it at all: the pad, the sustained floor and the keys stating the breeze,
            //with the tom run that ends every section the only struck thing in it — and there it is the kit
            //being announced rather than played. A dance track that opens on its own floor rather than on its
            //drums is the whole of the "dignity" the report asked for, and it costs nothing but patience —
            //fifteen seconds of a two-and-a-half minute piece, at the one place a listener will grant them.
            new(false, false, false, false, false, true,  false, true,  false, LeadPart.Breeze, 0.55f),
            //1 INTRO. No drums at all for its first half — pad and arpeggio over the floor, so the track
            //begins by arriving rather than by already being under way, and the kick landing halfway is an event.
            new(true,  false, true,  false, false, true,  true,  true,  false, LeadPart.None,   0.62f),
            new(true,  true,  true,  false, true,  true,  true,  false, false, LeadPart.Verse,  0.95f),  //2 verse
            new(true,  true,  true,  true,  true,  true,  true,  false, false, LeadPart.Chorus, 1.00f),  //3 CHORUS
            //4 BREEZE — the drums AND the arpeggio out, and the keys' tune in. Measured at 0.55 this section
            //fell to a fifth of the verse's level and a ninth of its low band, which is not a breakdown but a
            //gap; almost all of that is simply losing the kick and the bass, which carry most of a mix's
            //energy. The parts that remain are pushed up to compensate, so it reads as EMPTIER rather than as
            //quieter — and since #186 the floor stays under it, so what is emptied is the kit and not the
            //bottom of the piece.
            new(false, false, false, false, false, true,  false, true,  false, LeadPart.Breeze, 0.85f),
            new(true,  true,  true,  false, true,  true,  true,  false, false, LeadPart.Verse,  0.95f),  //5 verse
            new(true,  true,  true,  true,  true,  true,  true,  false, false, LeadPart.Chorus, 1.00f),  //6 CHORUS
            new(true,  false, true,  true,  true,  true,  true,  false, true,  LeadPart.None,   0.90f),  //7 build — the roll
            new(true,  true,  true,  true,  true,  true,  true,  true,  false, LeadPart.Chorus, 1.00f),  //8 CHORUS, everything
            //9 OUTRO. The kit is gone and the breeze comes back over the floor, under a fade laid across the
            //whole section, so the track ENDS instead of being cut off. That is what makes the regeneration
            //seamless: the join between one pass and the next lands in silence, so the frame or two it takes
            //to swap buffers cannot be heard. It used to fade out with the kick still going, which is a hand
            //on a fader rather than an ending.
            new(false, false, false, false, false, true,  true,  true,  false, LeadPart.Breeze, 0.70f)
        };

        //The intro is section ONE since #186 — the prelude took the top of the piece — and this is the one
        //section that holds its own drums back, so it is named rather than counted.
        private const int SECTION_INTRO = 1;

        //THE SHOCK (#186), and it is one event in one place: the report asked for something rare and climactic
        //rather than the per-bar Embellish/Ghost rolls, which fire in nearly every bar and are therefore
        //texture and not surprise. The build's last bar STOPS three beats early, and what lands in the hole is
        //a chord from outside the piece: E MAJOR, the harmonic-minor dominant, whose G# is the one note two
        //minutes of strictly diatonic A minor has not played. It resolves onto the Am the final chorus opens on
        //— guaranteed, since every progression in the pool starts there — so the surprise is a cadence and not
        //a wrong note, which is the difference between a shock and a mistake.
        //
        //It is scored for BRASS, the one voice this piece never otherwise uses. A part heard once is an event
        //by construction; the same chord on the lead would be the piece getting louder.
        private const int SECTION_BUILD = 7;

        private const int SHOCK_CUT = 4;     //step of the build's last bar where everything stops dead
        private const int SHOCK_STEP = 10;   //the stab, deliberately off the beat rather than on it
        private const float SHOCK_RING = 6f; //steps it rings, i.e. right up to the drop

        private static readonly int[] SHOCK_ARP = { 52, 56, 59, 64 };   //E major: E3 G#3 B3 E4
        private const int SHOCK_ROOT = 40;                              //E2, in the floor's own register

        /// <summary>
        /// The score at which a fanfare is at its fullest. There is no natural maximum to a level's score, so
        /// this is the one number that decides what "a big win" means — the single dial to turn if the
        /// fanfares stop matching how the player feels about their result.
        /// </summary>
        public const int FANFARE_FULL_SCORE = 6000;

        //Louder than the level's theme: a fanfare is an announcement, not a background, and on a win it has
        //the fireworks' reports to be heard alongside.
        private const float FANFARE_VOLUME = 0.55f;

        private readonly Random _seeds;

        private MusicTheme _theme;        //which composition passes are rendered from; see SetTheme

        /// <summary>
        /// One pass in hand <b>per composition</b>, not one in total (#120). With two pieces alternating by
        /// level, a change of theme is what happens at nearly every level boundary rather than once in a
        /// session, so a single slot meant every boundary threw away the pass it held and paid for a fresh
        /// bake in silence — and a Bohemia pass costs <b>12.6 s</b> to render on the development desktop
        /// against Pulse's 2.7 s (its string section is seven detuned oscillators with their own vibrato per
        /// note, where Pulse's heaviest voice is two). A slot each costs one more pending buffer, ~35 MB,
        /// and buys a switch the player does not hear as a hole.
        /// </summary>
        private readonly Task<float[]>[] _next = new Task<float[]>[THEME_COUNT];

        /// <summary>
        /// The chain the passes play through since #212: one <see cref="DynamicSoundEffectInstance"/> whose
        /// queue is fed the next pass while the current one is still sounding, so XAudio2 starts buffer N+1
        /// the sample buffer N ends — the handover the per-frame <c>State</c> poll could never give (a frame
        /// or two of dead air between passes was #212's whole complaint, and no polling loop can be tighter
        /// than its own frame). It is a <b>chain and not a loop</b> on purpose: each submitted pass is a fresh
        /// variation, which <c>IsLooped</c> on one buffer would trade away for a gapless repeat of the same
        /// two minutes. The queue holds at most one pass ahead of the sounding one
        /// (<see cref="DynamicSoundEffectInstance.PendingBufferCount"/> stays under 2 — it counts the
        /// sounding buffer too, so its two are one playing and one waiting), which is all the feed ever
        /// needs: a bake takes seconds and a pass lasts minutes.
        /// </summary>
        private DynamicSoundEffectInstance _voice;
        private bool _wanted;             //the game wants music; the chain may be mid-pass
        private bool _failed;

        //The fanfare is its own instance so it is independent of the loop: Stop() silences the level's theme
        //without cutting off the piece that is announcing the result.
        /// <summary>
        /// What a fanfare rolled for itself, so something else can play <b>in tune with it</b> (#158). The
        /// result screen's star chime needs this and nothing else does: it sounds while the fanfare is still
        /// going, and a fixed-pitch chime is only in tune when the piece happens to have rolled the one key it
        /// was baked in.
        /// </summary>
        public readonly struct FanfareShape
        {
            public readonly int Root;        //MIDI note the piece is in
            public readonly float Bpm;
            public readonly bool Victory;    //major, and the only kind the stars ever sound over

            public FanfareShape(int root, float bpm, bool victory)
            {
                Root = root; Bpm = bpm; Victory = victory;
            }
        }

        private Task<(float[] Pcm, FanfareShape Shape)> _fanfareBake;
        private FanfareShape _fanfareShape;

        //Wall clock since the fanfare actually started SOUNDING, which is what a beat grid has to be measured
        //from. It cannot be taken from the bake: the piece is synthesized on a background thread and realized
        //whenever that finishes, which on a slow machine is a good fraction of a second later.
        private readonly System.Diagnostics.Stopwatch _fanfareClock = new();

        private bool _fanfareShapeKnown;

        /// <summary>
        /// The pending or sounding fanfare's key and tempo — false when there is none at all.
        /// <para>
        /// <b>It is answered as soon as the fanfare is ASKED FOR, not when it becomes audible</b>, and that
        /// distinction is the whole of #158's fix. The piece is synthesized on a background thread and takes
        /// <i>seconds</i> on a weak machine — measured at over three here — while the result screen opens on
        /// the frame the level cleared. Anything that waited for the sound before deciding what to play with
        /// it would wait longer than the player will, so the key is rolled up front and only the beat grid
        /// needs the audio.
        /// </para>
        /// </summary>
        /// <param name="secondsSounding">
        /// How long it has actually been playing, or <b>negative when it has not started</b> — the caller can
        /// pitch itself either way, but may only align to a beat when this is real.
        /// </param>
        public bool TryGetFanfare(out FanfareShape shape, out float secondsSounding)
        {
            shape = _fanfareShape;

            secondsSounding = _fanfareClock.IsRunning && IsFanfarePlaying
                ? (float)_fanfareClock.Elapsed.TotalSeconds
                : -1f;

            return _fanfareShapeKnown;
        }
        private SoundEffect _fanfareTrack;
        private SoundEffectInstance _fanfare;

        //The front end's piece (#46). Unlike the theme it is LOOPED rather than regenerated: the menu is
        //visited for moments rather than minutes, so endless variation buys nothing there — the seam is made
        //inaudible at bake time instead (see BakeMenu). Baked once per run, so no two runs share a lobby.
        private Task<float[]> _menuBake;
        private SoundEffect _menuTrack;
        private SoundEffectInstance _menu;
        private bool _menuWanted;

        /// <summary>True while a pass is actually sounding.</summary>
        public bool IsPlaying => _voice != null && _voice.State == SoundState.Playing;

        /// <summary>
        /// True while a fanfare is sounding. The caller ducks the fireworks under it — see
        /// <c>ProceduralAudio.FireworkDuck</c>.
        /// </summary>
        public bool IsFanfarePlaying => _fanfare != null && _fanfare.State == SoundState.Playing;

        private float _gain = 1f;

        /// <summary>
        /// The player's volume settings (master × music), 1 for the authored level. Written by the host when a
        /// settings row changes, and pushed onto whatever is already sounding — unlike an effect, a two-minute
        /// pass and a nine-second fanfare are long enough that "on the next play" would mean minutes late. The
        /// fields never point at a disposed instance (see <see cref="Advance"/>), so the writes are safe.
        /// </summary>
        public float Gain
        {
            get => _gain;
            set
            {
                _gain = value;
                if (_voice != null) _voice.Volume = MUSIC_VOLUME * _gain;
                if (_fanfare != null) _fanfare.Volume = FANFARE_VOLUME * _gain;
                if (_menu != null) _menu.Volume = MENU_VOLUME * _gain;
            }
        }

        /// <summary>
        /// Starts synthesizing a first pass of <b>every</b> composition at once, on background threads. Minutes
        /// of PCM is seconds of arithmetic, and doing it on the loading thread would be seconds of a black
        /// window; nothing asks for a pass until a level is built, which is several menus later — and that same
        /// argument is why the second piece is baked here too rather than when a level first asks for it. The
        /// player reaches level two several minutes in; its pass has been ready since the splash.
        /// </summary>
        /// <param name="seed">
        /// Fixed only for testing — left null the music is different every run, which is the point of it.
        /// </param>
        public ProceduralMusic(int? seed = null)
        {
            _seeds = seed.HasValue ? new Random(seed.Value) : new Random();

            for (int theme = 0; theme < THEME_COUNT; theme++) _next[theme] = StartBake((MusicTheme)theme);

            //The menu piece bakes alongside them — it is the one wanted first, at the splash, and it is a
            //fraction of a theme's arithmetic, so it is ready well inside the menu's first seconds.
            int menuSeed = _seeds.Next();
            _menuBake = Task.Run(() => BakeMenu(menuSeed));
        }

        /// <summary>
        /// Starts a pass of one named composition. The theme is a parameter rather than read off
        /// <see cref="_theme"/> inside the task, so a bake cannot be retargeted by a change of theme that
        /// happens while it runs.
        /// </summary>
        private Task<float[]> StartBake(MusicTheme theme)
        {
            int seed = _seeds.Next();

            return Task.Run(() => theme switch
            {
                MusicTheme.Bohemia => BakeBohemia(seed),
                MusicTheme.Nocturne => BakeJazz(seed),
                MusicTheme.Mural => BakeMural(seed),
                MusicTheme.Ember => BakeEmber(seed),
                _ => Bake(seed),
            });
        }

        /// <summary>
        /// Which composition the next pass is rendered from (#120). Called from the level's own install, so a
        /// level set alternates pieces rather than replaying one for an evening.
        /// <para>
        /// The <b>sounding</b> chain is dropped outright: its passes belong to the piece that was playing,
        /// and letting it run on would play the previous level's piece for minutes. What is <b>not</b> dropped
        /// is the pass baking for the other composition (see <see cref="_next"/>): each has a slot of its
        /// own, so the piece this level wants is normally already in hand and <see cref="Update"/> builds the
        /// new chain from it the same frame. It is only ever silent here if the pass has not finished baking,
        /// which after the first level of a session cannot happen — the one for this piece has been rendering
        /// since the last boundary.
        /// </para>
        /// </summary>
        public void SetTheme(MusicTheme theme)
        {
            if (_failed || theme == _theme) return;

            _theme = theme;

            //Only if nothing is already baking for it: the pass rendered while the other piece was playing is
            //exactly the one wanted here, and starting a second would throw it away and pay for it in silence.
            _next[(int)theme] ??= StartBake(theme);

            //The chain is torn down rather than re-aimed: its queued passes belong to the theme it was built
            //for, and the next Update builds a fresh one from this theme's ready pass.
            _voice?.Dispose();
            _voice = null;
        }

        /// <summary>The composition a level asks for by name, falling back to the pool's own rotation.</summary>
        /// <param name="named">
        /// The level file's <c>music</c> field, or null when it names none. Parsed rather than cast, for the
        /// reason the scene names are: it is a hand-editable file, and an unknown spelling has to mean "the
        /// default" rather than an exception.
        /// </param>
        /// <param name="index">
        /// The level's place in the set, which is what picks when nothing is named. Cycling by position is why
        /// no level file has to say anything at all to get variety — and why level one still opens on
        /// <see cref="MusicTheme.Pulse"/>, the piece it was written for.
        /// </param>
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

                    //The slot's old name — #264 replaced the piece, not the slot: a hand-edited file still
                    //naming the polka gets the replacement rather than silently falling to whatever its
                    //position happens to rotate to.
                    case "dechovka": return MusicTheme.Mural;
                }

            return (MusicTheme)(((index % THEME_COUNT) + THEME_COUNT) % THEME_COUNT);
        }

        /// <summary>Starts the music, or does nothing if it is already sounding.</summary>
        public void Play()
        {
            if (_failed) return;

            _wanted = true;
            if (_voice == null) Advance();
        }

        /// <summary>
        /// Stops the music. The pass baking behind it is kept — it becomes the next one played. The chain is
        /// torn down with it, for the same reason <see cref="SetTheme"/> tears its own down: a stopped chain's
        /// queue belongs to a moment that has passed, and <see cref="Play"/> starts a fresh one from the pass
        /// in hand.
        /// </summary>
        public void Stop()
        {
            _wanted = false;
            _voice?.Dispose();
            _voice = null;
        }

        /// <summary>
        /// Starts the front end's loop, or marks it wanted if its bake has not landed yet — <see cref="Update"/>
        /// realizes it the frame it is ready. The host drives this from the one question that decides it:
        /// whether a session screen is on the stack.
        /// </summary>
        public void PlayMenu()
        {
            if (_failed) return;

            _menuWanted = true;
            if (_menu != null && _menu.State != SoundState.Playing) _menu.Play();
        }

        /// <summary>Stops the front end's loop. Being a loop, stopping and starting again costs nothing.</summary>
        public void StopMenu()
        {
            _menuWanted = false;
            _menu?.Stop();
        }

        /// <summary>
        /// The victory fanfare: bright, major, rising, and scaled by how well the player did — a bigger score
        /// buys more voices, more percussion and a longer, higher final chord, so the music itself tells them
        /// what kind of win it was before the result screen has said a word.
        /// </summary>
        /// <param name="score">The level's final score, weighed against <see cref="FANFARE_FULL_SCORE"/>.</param>
        /// <param name="grand">
        /// Play it at <b>full</b> intensity whatever the score was — every voice, all the percussion and the long
        /// high final chord. It is what a finished block of levels takes (#184): the milestone is the chapter and
        /// not the last level of it, so a chapter closed on a scraped two-star clear must not sound like a poor
        /// win. There is nothing new to synthesise for it — the scaling already tops out here.
        /// </param>
        public void PlayVictory(int score, bool grand = false) => StartFanfare(score, victory: true, grand: grand);

        /// <summary>
        /// The defeat fanfare: slow, minor, falling. It takes the same scaling from the other end — a good
        /// score that still lost gets a fuller, more dignified piece, and a poor one gets a thin and bleak
        /// three notes. Losing badly and losing narrowly should not sound the same.
        /// </summary>
        public void PlayDefeat(int score) => StartFanfare(score, victory: false);

        /// <summary>
        /// Stops whatever fanfare is sounding. Called when a level is built, so the previous result's music
        /// does not play over the opening of the next attempt.
        /// </summary>
        public void StopFanfare()
        {
            _fanfareBake = null;
            _fanfareShapeKnown = false;
            _fanfareClock.Reset();
            _fanfare?.Stop();
        }

        /// <summary>
        /// Rolls a fanfare's key and tempo off <paramref name="random"/>, which the bake then carries on with.
        /// It is one method rather than a copy in each baker because the two must not drift: the shape handed
        /// to whatever plays along with the piece has to be the shape the piece was actually built from.
        /// </summary>
        private static FanfareShape RollFanfare(Random random, bool victory)
        {
            if (victory)
            {
                //The theme's own tempo band, rolled so two wins in a row are not the same piece, and MAJOR
                //up where the supersaw shines.
                float bpm = 128f + (float)random.NextDouble() * 14f;
                int[] roots = { 57, 60, 62 };   //A3, C4, D4

                return new FanfareShape(roots[random.Next(roots.Length)], bpm, victory: true);
            }

            //Slow. Half the theme's tempo and less: the piece has to feel like it is running out, and low —
            //lower than the victory's.
            float defeatBpm = 62f + (float)random.NextDouble() * 12f;
            int[] defeatRoots = { 45, 43, 41, 40 };   //A2, G2, F2, E2

            return new FanfareShape(defeatRoots[random.Next(defeatRoots.Length)], defeatBpm, victory: false);
        }

        private void StartFanfare(int score, bool victory, bool grand = false)
        {
            if (_failed) return;

            //0 for nothing, 1 for a very good result. There is no natural ceiling to a score, so the reference
            //is a stated constant rather than anything derived — see FANFARE_FULL_SCORE. A grand fanfare skips
            //the weighing entirely and takes the top of the same scale — see PlayVictory.
            float intensity = grand ? 1f : MathHelper.Clamp(score / (float)FANFARE_FULL_SCORE, 0f, 1f);
            int seed = _seeds.Next();

            //On a background thread, like the track: a fanfare is only a few seconds of PCM, but this fires on
            //the exact frame a level ends — which is also the frame the camera is released, the fireworks
            //start and the result screen is being built — and that is the last moment to spend on synthesis.
            //The key and tempo are rolled HERE, on the calling thread, and only the rendering goes to the
            //background — so anything that has to agree with this piece can know what it is immediately
            //rather than waiting seconds for the audio (#158). The same Random then carries on inside the
            //bake, so the rest of the piece rolls exactly as it did when these two were rolled in there.
            Random random = new(seed);
            FanfareShape shape = RollFanfare(random, victory);

            _fanfareShape = shape;
            _fanfareShapeKnown = true;

            _fanfareBake = Task.Run(() =>
            {
                float[] pcm = victory
                    ? BakeVictory(random, intensity, shape)
                    : BakeDefeat(random, intensity, shape);

                return (pcm, shape);
            });
        }

        /// <summary>
        /// Called once a frame. Its whole job is the feed: the chain plays <b>pass after pass</b>, each one
        /// submitted to the voice's queue while the one before it is still sounding, and what the frame does
        /// is hand over the pass whose bake finished. That is what makes the music genuinely endless rather
        /// than a loop that repeats: the player never hears the same two minutes twice — and since #212 they
        /// do not hear the join either, because XAudio2 starts the queued pass the sample the sounding one
        /// ends, where the old per-frame <c>State</c> poll could only notice a finished pass on the frame
        /// after it and leave a frame or two of dead air in the gap (#212's whole complaint).
        /// </summary>
        public void Update()
        {
            if (_failed) return;

            //The fanfare first: it is realized the frame its synthesis finishes, so the piece announcing the
            //result lands as close to the result as the machine allows.
            if (_fanfareBake != null && _fanfareBake.IsCompleted)
            {
                Task<(float[] Pcm, FanfareShape Shape)> ready = _fanfareBake;
                _fanfareBake = null;

                try
                {
                    SoundEffectInstance old = _fanfare;
                    SoundEffect oldTrack = _fanfareTrack;

                    _fanfareTrack = ToSoundEffect(ready.Result.Pcm);
                    _fanfare = _fanfareTrack.CreateInstance();
                    _fanfare.Volume = FANFARE_VOLUME * _gain;
                    _fanfare.Play();

                    //Started HERE and not where the bake was asked for: this is the frame it becomes audible,
                    //and the beat grid anything else lines up to has to be measured from that.
                    _fanfareShape = ready.Result.Shape;
                    _fanfareClock.Restart();

                    old?.Dispose();
                    oldTrack?.Dispose();
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the fanfare could not be realized: {exception.Message}");
                }
            }

            //The menu loop is realized once, the frame its synthesis finishes; being looped, it never needs
            //another. Guarded like the fanfare, and for the same reason: a lobby that cannot play must not
            //take the game down with it.
            if (_menuBake != null && _menuBake.IsCompleted)
            {
                Task<float[]> ready = _menuBake;
                _menuBake = null;

                try
                {
                    _menuTrack = ToSoundEffect(ready.Result);
                    _menu = _menuTrack.CreateInstance();
                    _menu.IsLooped = true;
                    _menu.Volume = MENU_VOLUME * _gain;

                    if (_menuWanted) _menu.Play();
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the menu piece could not be realized: {exception.Message}");
                }
            }

            if (!_wanted) return;
            if (_voice == null)
            {
                Advance();
                return;
            }

            //The feed: submit the finished pass onto the voice's queue, and only while fewer than two buffers
            //sit on it — <see cref="DynamicSoundEffectInstance.PendingBufferCount"/> counts the sounding
            //buffer too (buffers leave the queue when they FINISH, not when they start), so the gate's two
            //are one playing and one held ahead of it, which is all the chain ever needs: a bake is seconds,
            //a pass is minutes, and a third would be tens of MB paying for nothing. A pass whose bake lands
            //while the queue is full simply waits in its slot; the frames after the sounding pass ends pick
            //it up the moment the queue drains.
            Task<float[]> next = _next[(int)_theme];
            if (next != null && next.IsCompleted && _voice.PendingBufferCount < 2)
            {
                try
                {
                    _voice.SubmitBuffer(ToPcm(next.Result));
                    _next[(int)_theme] = StartBake(_theme);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"[music] the next pass could not be chained, playing on without it: {exception.Message}");
                    _failed = true;
                }
            }
        }

        /// <summary>
        /// Builds the chain's head: a fresh voice with the theme's ready pass submitted onto it, the next
        /// bake started behind that, and playback begun. Called from <see cref="Play"/> and from
        /// <see cref="Update"/> while the chain's head does not exist yet; the passes after the first arrive
        /// through <see cref="Update"/>'s feed, never through here.
        /// </summary>
        private void Advance()
        {
            //A level that starts without music is a disappointment; a level that will not start because the
            //synthesis threw is a bug the player cannot get past. Guarded for that reason, and the same one
            //LoadLevelSet's is.
            try
            {
                Task<float[]> next = _next[(int)_theme];

                //The first pass is not in hand yet (its bake still running, which is only ever the first
                //seconds of a session). Nothing to build from and nothing to replay — Update retries every
                //frame and builds the chain the moment the bake lands.
                if (next == null || !next.IsCompleted) return;

                DynamicSoundEffectInstance old = _voice;

                _voice = new DynamicSoundEffectInstance(SAMPLE_RATE, AudioChannels.Stereo);
                _voice.Volume = MUSIC_VOLUME * _gain;
                _voice.SubmitBuffer(ToPcm(next.Result));

                //Disposed only once the replacement exists, so a failure part-way through leaves the
                //previous chain intact and playable rather than leaving the game silent.
                old?.Dispose();

                //Into this composition's own slot, so it is the pass waiting when a level of this piece
                //comes round again — which is the whole reason the slots are per theme.
                _next[(int)_theme] = StartBake(_theme);

                _voice.Play();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[music] the theme could not be realized, playing on without it: {exception.Message}");
                _failed = true;
            }
        }

        #region The score

        /// <summary>
        /// Renders the whole arrangement into one float buffer. Every voice writes into the same mix
        /// additively, and the lot is soft-limited at the end.
        /// </summary>
        private static float[] Bake(int seed)
        {
            Random random = new(seed);
            Variation variation = new(random);

            float secondsPerStep = 60f / (variation.Bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int sectionOutro = ARRANGEMENT.Length - 1;
            int totalSteps = ARRANGEMENT.Length * STEPS_PER_SECTION;

            //A whole number of samples per step, and the track's length taken FROM that rather than from the
            //nominal tempo: rounding per step and then trusting the nominal length leaves a fraction of a step
            //of silence at the seam.
            float[] mix = NewMix(samplesPerStep * totalSteps);

            for (int step = 0; step < totalSteps; step++)
            {
                int at = step * samplesPerStep;
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;

                int phrase = bar % 4;                                 //where in the four-bar progression
                int chord = variation.Progression[phrase];

                int sectionIndex = bar / BARS_PER_SECTION;
                Section section = ARRANGEMENT[sectionIndex];
                int barInSection = bar % BARS_PER_SECTION;
                float level = section.Level;

                int[] arp = CHORD_ARP[chord];
                int transpose = variation.Transpose;

                bool lastBar = barInSection == BARS_PER_SECTION - 1;

                //The intro holds its drums back for its first half, so the track begins by arriving. The outro
                //fades over its whole length, which is what lets one pass hand over to the next in silence.
                bool introQuiet = sectionIndex == SECTION_INTRO && barInSection < 4;
                float fade = sectionIndex == sectionOutro
                    ? 1f - (barInSection * STEPS_PER_BAR + inBar) / (float)STEPS_PER_SECTION
                    : 1f;

                level *= fade * fade;
                if (level <= 0.001f) continue;

                //THE FLOOR (#186) ----------------------------------------------------------------------
                //One sustained note a chord, an octave under the root, held across the whole bar. This is what
                //"a real bass" turned out to mean here: the piece already had a Bass, but it is a PLUCK gone
                //in about 150 ms, so the low end was felt for a fraction of a beat in four and nothing at all
                //held the bottom of the mix. The off-beat pump deliberately stays where it is, an octave up —
                //writing it down here instead would have doubled this note and turned both to mud. A floor
                //under a pump is how the genre is actually arranged; a pump alone is what a demo sounds like.
                //
                //It DUCKS to the beat wherever a kick is playing (see SubBass) — a sustained sine and a kick
                //sharing an octave otherwise cancel each other at whatever phase they happen to meet at, and
                //the duck is both the fix and the pump the ear expects. Where there is no kick it holds flat,
                //which is the whole reason the quiet sections still have a bottom.
                if (section.Sub && inBar == 0)
                {
                    //Held PAST the bar line, so one chord's floor is still releasing while the next one's is
                    //already speaking. Ended ON the line instead, the release and the attack leave a hole at
                    //every bar line — measured at a quarter of the level in the drumless sections, where
                    //there is nothing else to cover it, which reads as the bass pulsing once a bar. The one
                    //exception is the build's last bar, where the note is cut to the stop: otherwise the
                    //shock's silence would be a silence with a bass note lying across it.
                    bool shockBar = sectionIndex == SECTION_BUILD && lastBar;
                    float held = shockBar ? SHOCK_CUT - 0.4f : STEPS_PER_BAR + 0.4f;

                    SubBass(mix, at, CHORD_ROOT[chord] - 12 + transpose, secondsPerStep * held, 0.21f * level,
                        duck: section.Kick && !introQuiet ? 0.55f : 0f,
                        beatSeconds: secondsPerStep * STEPS_PER_BEAT);
                }

                //THE SHOCK (#186) ----------------------------------------------------------------------
                //The stop and the borrowed chord before the last drop; the constants carry the reasoning.
                if (sectionIndex == SECTION_BUILD && lastBar && inBar >= SHOCK_CUT)
                {
                    if (inBar == SHOCK_STEP)
                    {
                        for (int voice = 0; voice < SHOCK_ARP.Length; voice++)
                            Brass(mix, at, SHOCK_ARP[voice] + transpose, secondsPerStep * SHOCK_RING, 0.16f * level,
                                pan: ChordPan(voice, SHOCK_ARP.Length, PAN_BRASS_SPREAD));

                        //Undamped: this one is not answering a kick, because there is no kick left to answer.
                        SubBass(mix, at, SHOCK_ROOT + transpose, secondsPerStep * SHOCK_RING, 0.42f * level,
                            duck: 0f, beatSeconds: 0f);
                    }

                    //Everything else in the bar is the silence, which is the half of it that does the work.
                    continue;
                }

                //DRUMS ---------------------------------------------------------------------------------
                if (section.Kick && !introQuiet && inBar % 4 == 0) Kick(mix, at, level);

                if (section.Clap && !introQuiet && (inBar == 4 || inBar == 12)) Clap(mix, at, level);

                if (section.Hats && inBar % 2 == 0)
                    Hat(mix, at, open: inBar % 4 == 2, level: (introQuiet ? 0.16f : 0.30f) * level);

                //The ride: straight sixteenths, and it is most of what "more beats" means here. Only in the
                //big sections, because a sixteenth ride running under a breakdown is not a breakdown.
                if (section.Ride) Hat(mix, at, open: false, level: 0.11f * level, pan: PAN_RIDE);

                //Ghost snares on the off-beats, rolled per bar. This is the variation the ear notices least
                //and misses most: it is what stops two identical bars sounding sequenced.
                if (section.Clap && !introQuiet && inBar % 4 == 3 && random.NextDouble() < variation.Ghost)
                    Snare(mix, at, 0.10f * level);

                //A tom run under the last beat of every section: the ear needs telling that something is about
                //to change.
                if (lastBar && inBar >= 12 && !introQuiet) Tom(mix, at, 138f - (inBar - 12) * 16f, level);

                //The build's snare roll: sixteenths that get louder and closer to the change, the oldest trick
                //in dance music and still the one that works.
                if (section.Roll)
                {
                    float through = (barInSection * STEPS_PER_BAR + inBar) / (float)STEPS_PER_SECTION;

                    //Doubles to thirty-seconds over the last quarter — the acceleration is what sells it.
                    bool hit = through < 0.75f ? inBar % 4 == 2 : inBar % 2 == 0;
                    if (hit) Snare(mix, at, 0.18f + 0.55f * through * through);
                }

                //BASS ----------------------------------------------------------------------------------
                if (section.Bass && !introQuiet)
                {
                    //Off-beat eighths against the kick. On the beat it doubles the kick and both go to mud;
                    //between them is what reads as a pump.
                    if (inBar % 4 == 2) Bass(mix, at, CHORD_ROOT[chord] + 12 + transpose, secondsPerStep * 1.7f, level);
                    if (inBar == 0) Bass(mix, at, CHORD_ROOT[chord] + transpose, secondsPerStep * 1.4f, level);

                    //An occasional octave jump on the last sixteenth of a beat. Always the same note, always on
                    //the grid — only its register is rolled for.
                    if (inBar % 4 == 3 && random.NextDouble() < variation.Embellish * 0.6f)
                        Bass(mix, at, CHORD_ROOT[chord] + 24 + transpose, secondsPerStep * 0.8f, level * 0.7f);
                }

                //PAD -----------------------------------------------------------------------------------
                //A held chord under the intro, the breakdown, the last chorus and the outro. This is the
                //"calm": it fills the space the drums leave without putting anything rhythmic in it.
                if (section.Pad && inBar == 0)
                    //Seated across the field by voice, so the held chord opens out instead of stacking up
                    for (int voice = 0; voice < arp.Length; voice++)
                        Pad(mix, at, arp[voice] + transpose, secondsPerStep * 15.5f, 0.10f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));

                //ARPEGGIO ------------------------------------------------------------------------------
                if (section.Arp)
                {
                    int arpStep = inBar % 8;
                    int index = arpStep < 4 ? arpStep : 7 - arpStep;
                    if (variation.ArpDown) index = 3 - index;

                    //Ping-ponged on alternate sixteenths (#119): the arp never stops, so this is what keeps
                    //the image moving through the melody's rests rather than only under its notes.
                    Arp(mix, at, arp[index] + 12 + transpose, secondsPerStep * 0.9f, 0.15f * level,
                        pan: (arpStep % 2 == 0) ? -PAN_ARP : PAN_ARP);
                }

                //LEAD ----------------------------------------------------------------------------------
                if (section.Lead == LeadPart.None) continue;

                //The breeze is taken first, ahead of the turnaround below: its sections have no kit to hand a
                //phrase on from, and the FILL is a supersaw run down the chord — precisely the voice these
                //sections exist to be a rest from. It is CENTRED and not on the keys' own seat, which is the
                //rule Nocturne's lean measured out the hard way: whatever may be the only thing sounding
                //belongs in the middle, and in the prelude this is the only thing sounding.
                if (section.Lead == LeadPart.Breeze)
                {
                    foreach (Note note in BREEZE[phrase])
                        if (note.Step == inBar)
                            Keys(mix, at, arp[note.Tone] + note.Octave + transpose,
                                secondsPerStep * (note.Length + 0.5f), 0.30f * level, pan: PAN_CENTRE);

                    continue;
                }

                if (lastBar)
                {
                    //The last bar of a section is the turnaround, whatever else that section was doing.
                    foreach (Note note in FILL)
                        if (note.Step == inBar)
                            Lead(mix, at, arp[note.Tone] + note.Octave + transpose, secondsPerStep * 1.6f, 0.30f * level);

                    continue;
                }

                bool isChorus = section.Lead == LeadPart.Chorus;
                Note[] line = isChorus ? CHORUS[phrase] : VERSE;

                foreach (Note note in line)
                    if (note.Step == inBar)
                        Lead(mix, at, arp[note.Tone] + note.Octave + transpose,
                            secondsPerStep * (note.Length + 0.6f), (isChorus ? 0.36f : 0.28f) * level);

                if (isChorus)
                    foreach (Note note in COUNTER)
                        if (note.Step == inBar)
                            Arp(mix, at, arp[note.Tone] + note.Octave + transpose,
                                secondsPerStep * (note.Length + 0.4f), 0.13f * level);

                //An embellishment: one extra chord tone on a step the written line leaves empty. Because it is
                //drawn from the chord and placed on the grid, it can only ever sound like part of the tune —
                //which is the rule the whole variation scheme runs on.
                if (!isChorus && inBar == 6 && random.NextDouble() < variation.Embellish)
                    Lead(mix, at, arp[3] + 12 + transpose, secondsPerStep * 1.2f, 0.24f * level);
            }

            //Soft-limited, not peak-normalised — see ProceduralAudio.Loudness for why that distinction matters
            //to anything with a transient in it, and a kick is nothing but transient.
            Limit(mix, targetRms: 0.20f, ceiling: 0.95f);

            return mix;
        }

        #endregion

        #region Bohemia — the second theme (#120)

        //D DORIAN, and the mode is the whole point of a second pool. Pulse is A natural minor, whose colour is
        //the flat sixth; Dorian raises that sixth — here the B natural inside the G major chord — which is the
        //sound of the folk idiom this piece is written after. One chord is the difference between "minor" and
        //"modal", and it is the MAJOR chord a step below the relative major, i.e. the one nobody expects.
        //
        //The roots are voiced so the cadence Bb -> C -> Dm walks UP by whole steps in the bass (46, 48, 50),
        //which is the most heroic move harmony owns and is why they sit where they do rather than at whatever
        //octave was nearest.
        private static readonly int[] BOHEMIA_ROOT = { 50, 43, 46, 48, 41, 45 };   //Dm G Bb C F Am

        private static readonly int[][] BOHEMIA_ARP =
        {
            new[] { 62, 65, 69, 74 },   //Dm: D4 F4 A4 D5
            new[] { 55, 59, 62, 67 },   //G : G3 B3 D4 G4   — the Dorian major IV
            new[] { 58, 62, 65, 70 },   //Bb: Bb3 D4 F4 Bb4
            new[] { 60, 64, 67, 72 },   //C : C4 E4 G4 C5
            new[] { 53, 57, 60, 65 },   //F : F3 A3 C4 F4
            new[] { 57, 60, 64, 69 }    //Am: A3 C4 E4 A4
        };

        //Every one opens on the tonic and three of the five end on C, the flat seventh, which resolves up a
        //whole step onto the Dm at the top of the loop — so the four-bar round cadences rather than merely
        //restarting. That is what a progression has to do in a piece with an arch over it.
        private static readonly int[][] BOHEMIA_PROGRESSIONS =
        {
            new[] { 0, 4, 2, 3 },   //Dm F  Bb C  — i ♭III ♭VI ♭VII, the climb
            new[] { 0, 1, 2, 3 },   //Dm G  Bb C  — the Dorian IV, then the climb
            new[] { 0, 5, 2, 3 },   //Dm Am Bb C
            new[] { 0, 3, 4, 1 },   //Dm C  F  G  — the modal turn, ending bright
            new[] { 0, 2, 3, 0 }    //Dm Bb C  Dm — the cadence stated inside the round
        };

        //THE THEME: a four-bar sentence, which is what makes it hummable and what Pulse's chorus is not.
        //Statement (bar 0), the SAME rhythmic cell a step higher (bar 1 — a sequence, and the single oldest
        //trick for making a tune sound inevitable), an answer that falls (bar 2), and the climax an octave up
        //(bar 3). Long notes and few of them: this is meant to be the tune the player leaves humming.
        private static readonly Note[][] BOHEMIA_THEME =
        {
            new[] { new Note(0, 0, 12, 6), new Note(6, 1, 12, 2), new Note(8, 2, 12, 8) },
            new[] { new Note(0, 1, 12, 6), new Note(6, 2, 12, 2), new Note(8, 3, 12, 8) },
            new[] { new Note(0, 3, 12, 4), new Note(4, 2, 12, 4), new Note(8, 1, 12, 8) },
            new[] { new Note(0, 3, 24, 10), new Note(10, 2, 24, 3), new Note(13, 3, 12, 3) }
        };

        //THE SECOND SUBJECT, and a piece with an arch needs one: somewhere to go that is not the tune and not
        //a hole. Everything about it is the theme's opposite — stepwise where the theme leaps, two notes to a
        //bar where the theme has three, and played by the keys under strings rather than by the lead. It comes
        //back in the coda over the full floor, which is the moment the whole arrangement is built towards.
        private static readonly Note[][] BOHEMIA_SECOND =
        {
            new[] { new Note(0, 2, 12, 10), new Note(10, 1, 12, 6) },
            new[] { new Note(0, 0, 12, 8), new Note(8, 1, 12, 8) },
            new[] { new Note(0, 2, 12, 10), new Note(10, 3, 12, 6) },
            new[] { new Note(0, 2, 12, 12), new Note(12, 0, 12, 4) }
        };

        //The verse: busier, so the theme has somewhere to arrive from. Same contract as Pulse's.
        private static readonly Note[] BOHEMIA_VERSE =
        {
            new(0, 0, 12, 2), new(2, 2, 12, 2), new(4, 1, 12, 3),
            new(8, 3, 12, 2), new(11, 2, 12, 1), new(12, 1, 12, 4)
        };

        //The turnaround, and it RISES where Pulse's falls — an eight-note scale run up into the next section.
        //A descending fill hands a phrase on; an ascending one throws it.
        private static readonly Note[] BOHEMIA_FILL =
        {
            new(8, 0, 0, 1), new(9, 1, 0, 1), new(10, 2, 0, 1), new(11, 3, 0, 1),
            new(12, 0, 12, 1), new(13, 1, 12, 1), new(14, 2, 12, 1), new(15, 3, 12, 1)
        };

        /// <summary>Which line, if any, a Bohemia section carries — and the voice follows from it.</summary>
        private enum BohemiaPart { None, Verse, Theme, Second }

        /// <summary>
        /// What plays during one eight-bar section of <see cref="MusicTheme.Bohemia"/>. It carries more flags
        /// than Pulse's <see cref="Section"/> because it has more to arrange: a string section, a brass
        /// underpinning and a tuned drum, none of which the dance track has any use for.
        /// </summary>
        private readonly struct BohemiaSection
        {
            public readonly bool Kick, HalfKick, Clap, Hats, Ride, Bass, Arp, Pad, Str, Brass, Timp, Roll;
            public readonly BohemiaPart Part;
            public readonly bool PartOnStrings;   //the line is bowed rather than played by the lead
            public readonly float Level;

            public BohemiaSection(bool kick, bool halfKick, bool clap, bool hats, bool ride, bool bass, bool arp,
                bool pad, bool str, bool brass, bool timp, bool roll, BohemiaPart part, bool partOnStrings, float level)
            {
                Kick = kick; HalfKick = halfKick; Clap = clap; Hats = hats; Ride = ride; Bass = bass;
                Arp = arp; Pad = pad; Str = str; Brass = brass; Timp = timp; Roll = roll;
                Part = part; PartOnStrings = partOnStrings; Level = level;
            }
        }

        //TWELVE sections against Pulse's nine, and the extra three are the reason this exists: a statement
        //before the floor arrives, a second subject in the middle, and a coda that brings it back. Roughly
        //3:20 a pass at the tempi below.
        //
        //                                   kick  half   clap  hats  ride  bass   arp   pad   str  brass timp  roll  part                    bowed  level
        private static readonly BohemiaSection[] BOHEMIA_ARRANGEMENT =
        {
            //0 INTRO. No kit and no bass at all: a timpani on the downbeat under a held string chord, which is
            //how a symphonic piece tells you it has started. The pad fills under it.
            new(false, false, false, false, false, false, false, true,  true,  false, true,  false, BohemiaPart.None,   false, 0.60f),
            //1 STATEMENT. The tune, BOWED, over a half-time kick — the floor is not here yet, and the theme
            //arriving before the beat does is what makes the beat's arrival mean something.
            new(false, true,  false, true,  false, false, true,  false, true,  true,  true,  false, BohemiaPart.Theme,  true,  0.86f),
            //2 VERSE. The floor arrives whole: four on the floor, the off-beat bass pump, the sixteenth arp.
            new(true,  false, true,  true,  false, true,  true,  false, true,  false, false, false, BohemiaPart.Verse,  false, 0.95f),
            //3 CHORUS. The theme on the lead with the strings under it and brass on the pillars.
            new(true,  false, true,  true,  true,  true,  true,  false, true,  true,  true,  false, BohemiaPart.Theme,  false, 1.00f),
            //4 SECOND SUBJECT. Kick out. Not a breakdown — a different tune, which is a far better reason to
            //take the drums away than a gap is.
            new(false, false, false, true,  false, false, true,  true,  true,  false, false, false, BohemiaPart.Second, false, 0.82f),
            //5 VERSE, the floor back under it.
            new(true,  false, true,  true,  false, true,  true,  false, true,  false, false, false, BohemiaPart.Verse,  false, 0.95f),
            //6 CHORUS.
            new(true,  false, true,  true,  true,  true,  true,  false, true,  true,  true,  false, BohemiaPart.Theme,  false, 1.00f),
            //7 DEVELOPMENT. Drums out, the theme bowed and bare over a timpani pulse — the piece looking at its
            //own tune rather than playing it.
            new(false, false, false, false, false, false, true,  true,  true,  false, true,  false, BohemiaPart.Theme,  true,  0.84f),
            //8 BUILD. The roll, the timpani accelerating under it, no melody — everything pointing forwards.
            new(true,  false, false, true,  true,  true,  true,  false, true,  false, true,  true,  BohemiaPart.None,   false, 0.92f),
            //9 CLIMAX. Everything at once, the theme doubled by the strings.
            new(true,  false, true,  true,  true,  true,  true,  true,  true,  true,  true,  false, BohemiaPart.Theme,  false, 1.00f),
            //10 CODA. The second subject returned over the full floor — the quiet middle tune made the big one,
            //which is the single most reliable way a piece can end up somewhere it started out from.
            new(true,  false, true,  true,  true,  true,  true,  true,  true,  true,  true,  false, BohemiaPart.Second, false, 0.98f),
            //11 OUTRO. Falls away under a fade, so the handover to the next pass lands in silence exactly as
            //Pulse's does.
            new(false, true,  false, true,  false, true,  true,  true,  true,  false, true,  false, BohemiaPart.None,   false, 0.70f)
        };

        /// <summary>
        /// What one rendering of <see cref="MusicTheme.Bohemia"/> rolls for itself. The same rule as
        /// <see cref="Variation"/> — random parameters, never random notes — at a slower band of tempi, because
        /// a piece carrying held string chords and a tuned drum needs the room and reads as grand at a speed
        /// where it would read as merely slow without them.
        /// </summary>
        private readonly struct BohemiaVariation
        {
            public readonly float Bpm;
            public readonly int Transpose;
            public readonly int[] Progression;
            public readonly bool ArpDown;
            public readonly float Embellish;
            public readonly float Flam;      //chance of a grace note ahead of a timpani stroke

            public BohemiaVariation(Random random)
            {
                Bpm = 112f + (float)random.NextDouble() * 10f;

                int[] keys = { -3, -2, 0, 0, 2, 3, 5 };
                Transpose = keys[random.Next(keys.Length)];

                Progression = BOHEMIA_PROGRESSIONS[random.Next(BOHEMIA_PROGRESSIONS.Length)];
                ArpDown = random.NextDouble() < 0.35;
                Embellish = 0.16f + (float)random.NextDouble() * 0.20f;
                Flam = 0.18f + (float)random.NextDouble() * 0.24f;
            }
        }

        /// <summary>
        /// Renders <see cref="MusicTheme.Bohemia"/>: the second level theme (#120), a modal piece with a real
        /// arch over a dance floor. It shares this file's instruments, its chord-tone melodies and its limiter
        /// with <see cref="Bake"/> and nothing else — its own mode, its own tunes, its own twelve-section form.
        /// <para>
        /// The floor is deliberately kept underneath it rather than replaced. BS3D's musical language is the
        /// eurodance of the first theme; a purely orchestral piece would be a better piece of music and a worse
        /// piece of <i>this game</i>, and the player switching levels should hear a second work by the same
        /// hand, not a second soundtrack.
        /// </para>
        /// </summary>
        private static float[] BakeBohemia(int seed)
        {
            Random random = new(seed);
            BohemiaVariation variation = new(random);

            float secondsPerStep = 60f / (variation.Bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int sectionOutro = BOHEMIA_ARRANGEMENT.Length - 1;
            int totalSteps = BOHEMIA_ARRANGEMENT.Length * STEPS_PER_SECTION;

            float[] mix = NewMix(samplesPerStep * totalSteps);

            for (int step = 0; step < totalSteps; step++)
            {
                int at = step * samplesPerStep;
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;

                int phrase = bar % 4;
                int chord = variation.Progression[phrase];

                int sectionIndex = bar / BARS_PER_SECTION;
                BohemiaSection section = BOHEMIA_ARRANGEMENT[sectionIndex];
                int barInSection = bar % BARS_PER_SECTION;

                int[] arp = BOHEMIA_ARP[chord];
                int root = BOHEMIA_ROOT[chord];
                int transpose = variation.Transpose;

                bool lastBar = barInSection == BARS_PER_SECTION - 1;

                float fade = sectionIndex == sectionOutro
                    ? 1f - (barInSection * STEPS_PER_BAR + inBar) / (float)STEPS_PER_SECTION
                    : 1f;

                float level = section.Level * fade * fade;
                if (level <= 0.001f) continue;

                //DRUMS ---------------------------------------------------------------------------------
                if (section.Kick && inBar % 4 == 0) Kick(mix, at, level);

                //The statement's half-time kick: beats one and three only. It is the same drum saying "this is
                //not the dance yet", and it costs one flag.
                if (section.HalfKick && inBar % 8 == 0) Kick(mix, at, 0.85f * level);

                if (section.Clap && (inBar == 4 || inBar == 12)) Clap(mix, at, level);

                if (section.Hats && inBar % 2 == 0)
                    Hat(mix, at, open: inBar % 4 == 2, level: 0.26f * level);

                if (section.Ride) Hat(mix, at, open: false, level: 0.10f * level, pan: PAN_RIDE);

                //THE TIMPANI. On the bar's downbeat, tuned to the chord's own root two octaves down, so it is
                //playing the harmony rather than marking time — which is the whole reason this piece has a
                //pitched drum and Pulse does not.
                if (section.Timp && inBar == 0)
                {
                    //A grace note a sixteenth ahead of the stroke, rolled per bar. A flam is what a timpanist
                    //does instead of playing a bar-line exactly, and it is the one thing that stops a drum on
                    //every downbeat sounding like a metronome with a pitch.
                    if (random.NextDouble() < variation.Flam && at >= samplesPerStep)
                        Timpani(mix, at - samplesPerStep, root - 12 + transpose, 0.22f * level);

                    Timpani(mix, at, root - 12 + transpose, 0.62f * level);
                }

                //The cadence stroke: the fourth bar of every phrase gets a second timpani on beat three, which
                //is where the harmony is turning back to the tonic.
                if (section.Timp && phrase == 3 && inBar == 8)
                    Timpani(mix, at, root - 12 + transpose, 0.40f * level);

                if (lastBar && inBar >= 12 && (section.Kick || section.HalfKick))
                    Tom(mix, at, 138f - (inBar - 12) * 16f, level);

                if (section.Roll)
                {
                    float through = (barInSection * STEPS_PER_BAR + inBar) / (float)STEPS_PER_SECTION;

                    bool hit = through < 0.75f ? inBar % 4 == 2 : inBar % 2 == 0;
                    if (hit) Snare(mix, at, 0.18f + 0.55f * through * through);

                    //The timpani accelerates with the roll: every bar in the first half, every half bar in the
                    //second. A build that only gets louder is a fade run backwards.
                    if (inBar == 0 || (through > 0.5f && inBar == 8))
                        Timpani(mix, at, root - 12 + transpose, (0.35f + 0.45f * through) * level);
                }

                //BASS ----------------------------------------------------------------------------------
                if (section.Bass)
                {
                    if (inBar % 4 == 2) Bass(mix, at, root + 12 + transpose, secondsPerStep * 1.7f, level);
                    if (inBar == 0) Bass(mix, at, root + transpose, secondsPerStep * 1.4f, level);

                    if (inBar % 4 == 3 && random.NextDouble() < variation.Embellish * 0.6f)
                        Bass(mix, at, root + 24 + transpose, secondsPerStep * 0.8f, level * 0.7f);
                }

                //STRINGS -------------------------------------------------------------------------------
                //The bed: the whole chord held across the bar. It is what the piece stands on, and it is at a
                //low level per note precisely because there are four of them sounding at once.
                if (section.Str && inBar == 0)
                    foreach (int note in arp)
                        Strings(mix, at, note + transpose, secondsPerStep * 15.4f, 0.055f * level);

                //PAD -----------------------------------------------------------------------------------
                if (section.Pad && inBar == 0)
                    for (int voice = 0; voice < arp.Length; voice++)
                        Pad(mix, at, arp[voice] + transpose, secondsPerStep * 15.5f, 0.07f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));

                //BRASS ---------------------------------------------------------------------------------
                //The pillars: the chord root low and long under the downbeat, and the fifth above it in the
                //biggest sections. Brass here is architecture rather than melody — it is what the tune leans on.
                if (section.Brass && inBar == 0)
                {
                    Brass(mix, at, root + 12 + transpose, secondsPerStep * 7.2f, 0.16f * level);

                    //The upper voice takes a seat; the pillar above it stays centre, being the piece's harmonic
                    //floor rather than a line in a section.
                    if (section.Ride)
                        Brass(mix, at, arp[2] + transpose, secondsPerStep * 7.2f, 0.10f * level,
                            pan: PAN_BRASS_SPREAD);
                }

                //ARPEGGIO ------------------------------------------------------------------------------
                if (section.Arp)
                {
                    int arpStep = inBar % 8;
                    int index = arpStep < 4 ? arpStep : 7 - arpStep;
                    if (variation.ArpDown) index = 3 - index;

                    //Ping-ponged as Pulse's is — the same figure wants the same treatment in both pieces
                    Arp(mix, at, arp[index] + 12 + transpose, secondsPerStep * 0.9f, 0.13f * level,
                        pan: (arpStep % 2 == 0) ? -PAN_ARP : PAN_ARP);
                }

                //THE LINE ------------------------------------------------------------------------------
                if (section.Part == BohemiaPart.None) continue;

                if (lastBar)
                {
                    foreach (Note note in BOHEMIA_FILL)
                        if (note.Step == inBar)
                            Lead(mix, at, arp[note.Tone] + note.Octave + transpose, secondsPerStep * 1.5f, 0.28f * level);

                    continue;
                }

                Note[] line = section.Part switch
                {
                    BohemiaPart.Theme => BOHEMIA_THEME[phrase],
                    BohemiaPart.Second => BOHEMIA_SECOND[phrase],
                    _ => BOHEMIA_VERSE
                };

                foreach (Note note in line)
                {
                    if (note.Step != inBar) continue;

                    int pitch = arp[note.Tone] + note.Octave + transpose;
                    float seconds = secondsPerStep * (note.Length + 0.6f);

                    if (section.PartOnStrings)
                    {
                        //Bowed, and doubled an octave down: a single string line up in the lead's register is
                        //thin, and the octave underneath is what a section actually sounds like.
                        Strings(mix, at, pitch, seconds, 0.20f * level);
                        Strings(mix, at, pitch - 12, seconds, 0.13f * level);
                    }
                    else if (section.Part == BohemiaPart.Second)
                    {
                        //The lyrical subject is struck rather than blown: the keys state it and the strings
                        //hold under it, which is the orchestration that tells the ear this is the other tune.
                        Keys(mix, at, pitch, seconds, 0.30f * level);
                        if (section.Str) Strings(mix, at, pitch - 12, seconds, 0.11f * level);
                    }
                    else
                    {
                        Lead(mix, at, pitch, seconds, (section.Part == BohemiaPart.Theme ? 0.34f : 0.27f) * level);

                        //The climax doubles the tune with the section an octave down — the one place both
                        //families play the same notes, which is what makes it the climax.
                        if (section.Part == BohemiaPart.Theme && section.Pad)
                            Strings(mix, at, pitch - 12, seconds, 0.15f * level);
                    }
                }

                //One extra chord tone in the gap the verse leaves, on the grid and drawn from the chord — the
                //same rule the whole variation scheme runs on.
                if (section.Part == BohemiaPart.Verse && inBar == 6 && random.NextDouble() < variation.Embellish)
                    Lead(mix, at, arp[3] + 12 + transpose, secondsPerStep * 1.2f, 0.22f * level);
            }

            Limit(mix, targetRms: 0.20f, ceiling: 0.95f);

            return mix;
        }

        #endregion

        #region Nocturne — the third theme (#162)

        //SMOOTH JAZZ, and what makes it a third pool rather than a third mood is HARMONY and TIME, not timbre.
        //Pulse is A natural minor over four-on-the-floor; Bohemia is D Dorian over the same floor. Both are
        //triads on a straight grid. This is sevenths and ninths on a SWUNG grid, which is a different language
        //rather than a different accent — and it is the reason this piece needed almost no new synthesis: the
        //genre's identity is in the chords and the placement, both of which are tables.
        //
        //It is in C major, deliberately the plainest key in the set: the colour here comes from the extensions
        //and the ii-V motion, and a piece whose whole point is its harmony should not also be fighting a key.

        //Roots for the walking bass, low and close together so the walk steps rather than leaps.
        private static readonly int[] JAZZ_ROOT = { 36, 38, 40, 41, 43, 45, 47, 45 };   //C D E F G A B A(7)

        //Close-position sevenths around middle C. Every one is a FOUR-note chord where Pulse and Bohemia use
        //triads-plus-octave, and that is the single biggest reason this reads as jazz: the seventh is not a
        //decoration here, it is the chord. They have to work both held (Pad) and broken (Arp), which is why
        //they stay in close position rather than spreading into the rootless voicings a pianist would use.
        private static readonly int[][] JAZZ_ARP =
        {
            new[] { 60, 64, 67, 71 },   //0 Cmaj7  C  E  G  B
            new[] { 62, 65, 69, 72 },   //1 Dm7    D  F  A  C
            new[] { 64, 67, 71, 74 },   //2 Em7    E  G  B  D
            new[] { 65, 69, 72, 76 },   //3 Fmaj7  F  A  C  E
            new[] { 67, 71, 74, 77 },   //4 G7     G  B  D  F   — the dominant, the only chord with a tritone
            new[] { 69, 72, 76, 79 },   //5 Am7    A  C  E  G
            new[] { 71, 74, 77, 81 },   //6 Bm7b5  B  D  F  A
            new[] { 69, 73, 76, 79 }    //7 A7     A  C# E  G   — secondary dominant; the C# is the one accidental
        };

        //Every one is built on ii-V motion, which is what the idiom is made of. Three resolve onto the tonic
        //and two turn back round instead, so a four-bar round can either land or keep going — the difference
        //between a piece that sits still and one that circles.
        private static readonly int[][] JAZZ_PROGRESSIONS =
        {
            new[] { 1, 4, 0, 0 },   //Dm7 G7  Cmaj7 Cmaj7 — the ii-V-I, stated plainly
            new[] { 1, 4, 0, 5 },   //Dm7 G7  Cmaj7 Am7   — lands, then leans to the relative minor
            new[] { 0, 5, 1, 4 },   //Cmaj7 Am7 Dm7 G7    — the turnaround; ends ON the dominant, so it circles
            new[] { 3, 4, 0, 5 },   //Fmaj7 G7 Cmaj7 Am7  — the plagal opening
            new[] { 6, 7, 1, 4 }    //Bm7b5 A7 Dm7 G7     — the minor ii-V into the major one, the darkest of the five
        };

        private enum JazzPart { None, Head, Bridge }

        //THE HEAD: long, few, and mostly on the extensions rather than the root. Written as chord tones like
        //everything else here, so index 3 IS the seventh of whatever is underneath — which is why this tune
        //sounds like jazz over any of the five progressions rather than only over the one it was written on.
        private static readonly Note[][] JAZZ_HEAD =
        {
            new[] { new Note(0, 3, 0, 6), new Note(6, 2, 0, 4), new Note(12, 1, 0, 4) },
            new[] { new Note(0, 2, 0, 8), new Note(10, 3, 0, 6) },
            new[] { new Note(0, 3, 12, 6), new Note(6, 2, 0, 6), new Note(12, 0, 12, 4) },
            new[] { new Note(0, 1, 12, 10), new Note(10, 3, 0, 6) }
        };

        //THE BRIDGE: higher, and it moves in steps where the head leaps — the same relation Bohemia's second
        //subject has to its theme, and for the same reason. It is what the middle of the form is for.
        private static readonly Note[][] JAZZ_BRIDGE =
        {
            new[] { new Note(0, 2, 12, 4), new Note(4, 3, 12, 4), new Note(8, 2, 12, 8) },
            new[] { new Note(0, 1, 12, 4), new Note(4, 2, 12, 4), new Note(8, 3, 12, 8) },
            new[] { new Note(0, 3, 12, 6), new Note(6, 2, 12, 4), new Note(12, 1, 12, 4) },
            new[] { new Note(0, 0, 12, 12) }
        };

        private readonly struct JazzSection
        {
            public readonly bool Kick, Ride, Brush, Bass, Pad, Comp;
            public readonly JazzPart Part;
            public readonly float Level;

            public JazzSection(bool kick, bool ride, bool brush, bool bass, bool pad, bool comp,
                JazzPart part, float level)
            {
                Kick = kick; Ride = ride; Brush = brush; Bass = bass; Pad = pad; Comp = comp;
                Part = part; Level = level;
            }
        }

        //Nine sections, and the form is the genre's own rather than a pop one: head, head, solo, head. What
        //changes between them is DENSITY, not volume — this is the one piece here that never gets loud.
        //
        //                              kick   ride  brush  bass   pad   comp  part            level
        private static readonly JazzSection[] JAZZ_ARRANGEMENT =
        {
            //0 INTRO. Keys comping alone over a held pad: the chords stated before anything counts time.
            //The dial is 0.88 and not the 0.55 it was authored at, and #218 is why: this section has two
            //voices in it and every other one has a walking bass, so when the bass came back the limiter's
            //drive fell and the intro went with it — measured 0.030 against the heads' 0.21, which is 17 dB
            //and a listener reaching for the volume knob. Trimming a thin section UP is what the dial is for.
            new(false, false, false, false, true,  true,  JazzPart.None,   0.88f),
            //1 The bass walks in. Still no ride — the walk IS the time here, which is how the idiom does it.
            new(false, false, false, true,  true,  true,  JazzPart.None,   0.72f),
            //2 HEAD, the full trio: walk, ride, brushes.
            new(true,  true,  true,  true,  false, true,  JazzPart.Head,   0.90f),
            //3 HEAD repeated — a jazz form states its tune twice before it does anything with it.
            new(true,  true,  true,  true,  false, true,  JazzPart.Head,   0.92f),
            //4 BRIDGE.
            new(true,  true,  true,  true,  true,  true,  JazzPart.Bridge, 0.95f),
            //5 SOLO. No written tune at all: the comp and the walk carry it, which is what a solo section IS.
            //Leaving the melody out is the arrangement's boldest move and the cheapest.
            new(true,  true,  true,  true,  false, true,  JazzPart.None,   0.88f),
            //6 HEAD again, out of the solo.
            new(true,  true,  true,  true,  false, true,  JazzPart.Head,   0.94f),
            //7 BRIDGE, fullest.
            new(true,  true,  true,  true,  true,  true,  JazzPart.Bridge, 1.00f),
            //8 OUTRO. Everything falls away under the fade, so the join to the next pass lands in silence
            //exactly as the other two pieces' do.
            new(false, false, true,  true,  true,  true,  JazzPart.None,   0.62f)
        };

        /// <summary>
        /// What one rendering of <see cref="MusicTheme.Nocturne"/> rolls. The same rule as the other two —
        /// random parameters, never random notes — at the slowest tempi in the set, because swung eighths at
        /// a dance tempo stop swinging and start sounding merely early.
        /// </summary>
        private readonly struct JazzVariation
        {
            public readonly float Bpm;
            public readonly int Transpose;
            public readonly int[] Progression;
            public readonly float Embellish;

            public JazzVariation(Random random)
            {
                Bpm = 88f + (float)random.NextDouble() * 16f;   //88-104

                //Whole tones and minor thirds only, the rule Pulse states: a random semitone would put two
                //passes a half-step apart, the one interval that sounds like a mistake rather than a key.
                int[] steps = { -3, -2, 0, 2, 3 };
                Transpose = steps[random.Next(steps.Length)];

                Progression = JAZZ_PROGRESSIONS[random.Next(JAZZ_PROGRESSIONS.Length)];
                Embellish = 0.3f + (float)random.NextDouble() * 0.4f;
            }
        }

        /// <summary>
        /// How late a swung off-beat eighth lands, as a fraction of a sixteenth. <b>This is the single thing
        /// that makes the piece jazz rather than slow pop</b>, and it is worth more than any of the chord
        /// tables: the same notes on a straight grid read as a ballad. Two thirds of the way to the next
        /// sixteenth is the triplet feel a swing eighth actually is (2:1); a little under it, because a fully
        /// mechanical 2:1 reads as a shuffle rather than as a swing.
        /// </summary>
        private const float SWING = 0.62f;

        /// <summary>
        /// Where a step lands once the swing is applied. Only the off-beat EIGHTHS move — steps 2, 6, 10, 14
        /// of the bar. The sixteenths between them are left where they are: swinging those too is what makes
        /// a shuffle, and a walking bass on the beat must not move at all or the time itself wobbles.
        /// </summary>
        private static int SwungAt(int step, int samplesPerStep)
        {
            bool offBeatEighth = step % 4 == 2;

            return step * samplesPerStep + (offBeatEighth ? (int)(samplesPerStep * (SWING - 0.5f) * 2f) : 0);
        }

        /// <summary>
        /// The walking bass: one note on every beat, and the line WALKS — it steps to a neighbour or slides
        /// chromatically into the next chord rather than restating the root. That motion is the genre's
        /// backbone, and it is why this piece has a bass part written as an algorithm instead of as a figure.
        /// <para>
        /// Beat 0 is always the chord's root, so the harmony is never in doubt; beats 1 and 2 take chord tones
        /// from the voicing above; beat 3 is the <b>approach</b> — a semitone under the next chord's root,
        /// which is the one note that makes a walk sound inevitable rather than merely busy.
        /// </para>
        /// </summary>
        private static int WalkingNote(int beat, int[] arp, int root, int nextRoot)
        {
            switch (beat)
            {
                case 0: return root;
                case 1: return root + (arp[1] - arp[0]);          //up to the third, in the chord's own spacing
                case 2: return root + (arp[2] - arp[0]);          //and the fifth
                default: return nextRoot - 1;                     //the chromatic approach from below
            }
        }

        /// <summary>
        /// Nocturne (#162): smooth jazz — a trio playing sevenths on a swung grid. Nine sections, ~3:00 a pass.
        /// It shares every instrument with the other two pieces except the bass, and that one exception is the
        /// point: a dance bass is a saw held through a filter, and a walking bass is <i>plucked</i>.
        /// </summary>
        private static float[] BakeJazz(int seed)
        {
            Random random = new(seed);
            JazzVariation variation = new(random);

            float secondsPerStep = 60f / (variation.Bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int sectionOutro = JAZZ_ARRANGEMENT.Length - 1;
            int totalSteps = JAZZ_ARRANGEMENT.Length * STEPS_PER_SECTION;

            //A bar of room past the end, so a note struck on the last beat rings out instead of being cut
            float[] mix = NewMix(samplesPerStep * (totalSteps + STEPS_PER_BAR));

            for (int step = 0; step < totalSteps; step++)
            {
                int at = SwungAt(step, samplesPerStep);
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;

                int phrase = bar % 4;
                int chord = variation.Progression[phrase];
                int nextChord = variation.Progression[(phrase + 1) % 4];

                int sectionIndex = bar / BARS_PER_SECTION;
                JazzSection section = JAZZ_ARRANGEMENT[sectionIndex];
                int barInSection = bar % BARS_PER_SECTION;

                int[] arp = JAZZ_ARP[chord];
                int root = JAZZ_ROOT[chord];
                int transpose = variation.Transpose;

                float fade = sectionIndex == sectionOutro
                    ? 1f - (barInSection * STEPS_PER_BAR + inBar) / (float)STEPS_PER_SECTION
                    : 1f;

                float level = section.Level * fade * fade;
                if (level <= 0.001f) continue;

                //DRUMS -----------------------------------------------------------------------------------
                //The kick is FEATHERED, not driven: quiet, on one and three, felt rather than heard. A jazz
                //kick that lands like Pulse's would make the whole thing a pop song in a dinner jacket.
                if (section.Kick && inBar % 8 == 0) Kick(mix, at, 0.28f * level);

                //The ride is the time-keeper here, and its pattern is the idiom's own: beat, then the swung
                //"and" of the beat, on two and four. Straight eighths all the way through would be a rock hat.
                if (section.Ride)
                {
                    if (inBar % 4 == 0) Hat(mix, at, open: false, level: 0.16f * level, pan: PAN_RIDE);
                    if (inBar == 6 || inBar == 14) Hat(mix, at, open: false, level: 0.11f * level, pan: PAN_RIDE);
                }

                //Brushes on two and four: the clap's noise burst at a fraction of its level, which is close
                //enough to a brush's swish once it is this quiet, and one voice this piece did not need to add.
                if (section.Brush && (inBar == 4 || inBar == 12)) Clap(mix, at, 0.22f * level);

                //THE WALK --------------------------------------------------------------------------------
                if (section.Bass && inBar % 4 == 0)
                {
                    int beat = inBar / 4;
                    int note = WalkingNote(beat, arp, root, JAZZ_ROOT[nextChord]) + transpose;

                    //Just short of the beat: a walking bass note is stopped by the next one, which is what
                    //gives the line its pulse rather than a legato drone.
                    UprightBass(mix, at, note, secondsPerStep * 3.4f, 0.55f * level);
                }

                //THE COMP --------------------------------------------------------------------------------
                //Keys, off the beat and sparse. Comping is defined by where it does NOT play: on the beat it
                //would double the walk and the two would fight for the same slot.
                //Its four notes are seated ACROSS the field rather than all on the keys' own seat: a chord is
                //what two hands are doing at once, and stacking it on one spot is also what was left of the
                //piece's lean once the tune moved to the middle.
                if (section.Comp && (inBar == 2 || inBar == 10))
                {
                    for (int voice = 0; voice < arp.Length; voice++)
                        Keys(mix, at, arp[voice] + transpose - 12, secondsPerStep * 5.5f, 0.085f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));
                }

                //An extra stab on the "and" of four, rolled per pass — the one place this piece is allowed to
                //be busy, and it is what stops eight bars of comping being eight identical bars.
                if (section.Comp && inBar == 14 && random.NextDouble() < variation.Embellish)
                {
                    for (int voice = 0; voice < arp.Length; voice++)
                        Keys(mix, at, arp[voice] + transpose - 12, secondsPerStep * 2.5f, 0.06f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));
                }

                //THE PAD ---------------------------------------------------------------------------------
                if (section.Pad && inBar == 0)
                    for (int voice = 0; voice < arp.Length; voice++)
                        Pad(mix, at, arp[voice] + transpose - 12, secondsPerStep * 15.5f, 0.075f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));

                //THE TUNE --------------------------------------------------------------------------------
                if (section.Part == JazzPart.None) continue;

                Note[] line = section.Part == JazzPart.Head ? JAZZ_HEAD[phrase] : JAZZ_BRIDGE[phrase];

                foreach (Note note in line)
                {
                    if (note.Step != inBar) continue;

                    int pitch = arp[note.Tone] + note.Octave + transpose;

                    //On the piano (#210), the one voice this theme had to invent after all: the tune began on
                    //the Rhodes because that voice was already here and IS the mellow lead this genre wants —
                    //but a tine that bright, exposed over nothing but a walk and brushes, reads as a
                    //glockenspiel. The Rhodes keeps the comp below; the soloist changed instruments.
                    //
                    //CENTRED, as it was on the Keys: leaving the tune on an off-centre seat leaned the whole
                    //piece 1.9 dB (measured). The soloist stands in the middle and the accompanist sits to
                    //one side, which is also how the trio would.
                    Piano(mix, at, pitch, secondsPerStep * (note.Length + 1.5f), 0.30f * level, pan: PAN_CENTRE);
                }
            }

            //The SAME target the other two take, and that is not a detail: this piece is quieter by
            //arrangement — a trio against a dance floor and an orchestra — and letting it also be quieter by
            //level would put a 2 dB drop between two levels of one set. It measured exactly that at 0.16
            //before this number was matched to Pulse's. The intimacy has to come from what is playing.
            Limit(mix, targetRms: 0.20f, ceiling: 0.95f);

            return mix;
        }

        #endregion

        #region Mural — the fourth theme (#264)

        //A BASS-LED GROOVE, and its axis is neither mode nor harmony: it is WHERE THE WEIGHT FALLS and WHICH
        //REGISTER CARRIES THE TUNE. The other four keep the bass as accompaniment under a treble tune and put
        //their low end squarely on the beat — Pulse and Bohemia four-on-the-floor, Nocturne's walk, Ember's
        //backbeat. Here the kick never marks all four beats: the grid is the 3+3+2 TRESILLO (hits on 0-3-6
        //and 8-11-14 — the victory fanfare already calls those eight counts the most danceable there are, and
        //this piece builds its floor on that sentence), and THE HOOK IS THE BASS LINE: a melodic, pitched sub
        //— the "log drum" the bass-first pop of the 2020s is built on — plays the tune at 41–98 Hz and
        //everything above it answers. It replaced the Moravian brass band of #164, whose complaint was
        //measured before it was filed: 34 % of its energy below 200 Hz against the set's 57–72.
        //
        //G MAJOR, plain triads. The set had no plain-major pop piece (Nocturne's C major reads as jazz
        //because its chords are sevenths), and the savanna daylight this piece plays under wants sunny and
        //open. The colour lives in the groove, not in extensions — the polka recorded that lesson about
        //itself, and it survives the piece. The one exception is the held pad, which voices a NINTH over the
        //chord: held colour only, never the bass and never a melody index.

        //The riff's own register: fundamentals 41–98 Hz across the transpose roll, the window SubBass proved.
        private static readonly int[] MURAL_ROOT = { 31, 36, 38, 40, 33, 35 };   //G1 C2 D2 E2 A1 B1

        private static readonly int[][] MURAL_ARP =
        {
            new[] { 55, 59, 62, 67 },   //0 G : G3 B3 D4 G4
            new[] { 60, 64, 67, 72 },   //1 C : C4 E4 G4 C5
            new[] { 62, 66, 69, 74 },   //2 D : D4 F#4 A4 D5
            new[] { 64, 67, 71, 76 },   //3 Em: E4 G4 B4 E5
            new[] { 57, 60, 64, 69 },   //4 Am: A3 C4 E4 A4
            new[] { 59, 62, 66, 71 }    //5 Bm: B3 D4 F#4 B4
        };

        //The ninth of each chord, for the pad alone; -1 where it is not diatonic (Bm's would be C#), and the
        //pad simply plays that chord plain — which is what keeps the roll's five progressions interchangeable.
        private static readonly int[] MURAL_NINTH = { 69, 74, 76, 78, 71, -1 };

        //Every one opens on G, so the riff and the melodies fit all five without knowing which was rolled,
        //and four of the five end on D, the dominant, resolving onto the G at the top of the round — a
        //four-bar loop that cadences rather than restarts (Bohemia's lesson). The third ends plagally on C,
        //kept for variety: the one round that leans home instead of walking there.
        private static readonly int[][] MURAL_PROGRESSIONS =
        {
            new[] { 0, 5, 1, 2 },   //G Bm C  D — the iii, the one bittersweet bar in the pool
            new[] { 0, 3, 1, 2 },   //G Em C  D — the doo-wop turn, the sunniest of the five
            new[] { 0, 2, 3, 1 },   //G D  Em C — the axis progression, modern pop's own; the plagal round
            new[] { 0, 4, 1, 2 },   //G Am C  D
            new[] { 0, 1, 0, 2 }    //G C  G  D — the plainest, gallery-simple
        };

        //The riff's intervals over the chord root: root, fifth, octave — a perfect fifth sits over every
        //chord in the pool, so the line needs no per-chord table. The THIRD is deliberately absent: two bass
        //notes a third apart under 100 Hz read as mud rather than harmony, the same physics that keeps
        //Ember's power chords thirdless.
        private static readonly int[] MURAL_RIFF_INTERVAL = { 0, 7, 12 };

        /// <summary>Which line, if any, a Mural section carries above the riff.</summary>
        private enum MuralPart { None, Verse, Hook, Sentence }

        //THE RIFF, the piece's hook: the double tresillo, with Tone indexing MURAL_RIFF_INTERVAL rather than
        //a chord arp. The verse states it grounded; the chorus leaps to the octave, which is the lift — the
        //same contrast every verse/chorus pair in this file is written on, played by the bass because here
        //the bass is the singer. The bar's CLOSING gesture (step 14) is not in these arrays: it is rolled —
        //see MURAL_TAIL_* and the push in the bake.
        private static readonly Note[] MURAL_RIFF_VERSE =
        {
            new(0, 0, 0, 3), new(3, 0, 0, 3), new(6, 1, 0, 2), new(8, 0, 0, 2), new(11, 1, 0, 3)
        };

        private static readonly Note[] MURAL_RIFF_CHORUS =
        {
            new(0, 0, 0, 3), new(3, 2, 0, 3), new(6, 1, 0, 2), new(8, 0, 0, 2), new(11, 2, 0, 3)
        };

        //Two authored closing gestures, swapped per pass (the ArpDown analogue): the fifth stepping down into
        //the next root, or the octave thrown up off it. Variety in the hook's last word, no random note.
        private static readonly Note[] MURAL_TAIL_DOWN = { new(14, 1, 0, 2) };
        private static readonly Note[] MURAL_TAIL_UP = { new(14, 2, 0, 2) };

        //THE VERSE, answered from above: a marimba speaking in the cells the riff leaves empty. The bass owns
        //0-3-6 / 8-11-14; the answer lives between them, so tune and groove interlock rather than compete —
        //the oom-pah's division of labour, kept from the piece this replaced because it was the one thing
        //about it that worked. One CALL cell and one ANSWER cell, a bar each, the answer landing on the root;
        //which of the two states first is rolled per pass.
        private static readonly Note[] MURAL_CALL =
        {
            new(2, 2, 0, 1), new(5, 3, 0, 1), new(7, 2, 0, 2), new(13, 1, 0, 2)
        };

        private static readonly Note[] MURAL_ANSWER =
        {
            new(2, 3, 0, 1), new(5, 2, 0, 1), new(10, 1, 0, 2), new(13, 0, 0, 3)
        };

        //THE HOOK, the chorus tune: the riff's contour restated up top, on long notes at the tresillo's own
        //starts, carried by the Keys with the marimba doubling an octave above — two genuinely different
        //timbres in octaves read as a big chorus where a unison doubling reads as a louder verse. The peak is
        //held back for the fourth bar, the file's own chorus rule.
        private static readonly Note[][] MURAL_HOOK =
        {
            new[] { new Note(0, 2, 0, 5), new Note(6, 3, 0, 4), new Note(11, 2, 0, 4) },
            new[] { new Note(0, 3, 0, 5), new Note(6, 2, 0, 4), new Note(11, 1, 0, 4) },
            new[] { new Note(0, 2, 0, 5), new Note(6, 3, 0, 4), new Note(11, 3, 0, 4) },
            new[] { new Note(0, 3, 12, 8), new Note(11, 2, 0, 4) }
        };

        //THE SENTENCE, for the prelude and the outro: state, the same cell a step higher, an answer that
        //falls, a held note to rest on — the four-bar shape Bohemia proved and Pulse's breeze borrowed,
        //because it is what stops a quiet section reading as a hole. On the marimba, in the chord's own
        //register, over the flat floor.
        private static readonly Note[][] MURAL_SENTENCE =
        {
            new[] { new Note(0, 0, 0, 5), new Note(6, 1, 0, 2), new Note(8, 2, 0, 8) },
            new[] { new Note(0, 1, 0, 5), new Note(6, 2, 0, 2), new Note(8, 3, 0, 8) },
            new[] { new Note(0, 3, 0, 4), new Note(4, 2, 0, 4), new Note(8, 1, 0, 6) },
            new[] { new Note(0, 0, 12, 12) }
        };

        //THE CHANT, the final chorus's own event: the marimba abandons its answer role and chants the riff's
        //tresillo on chord tones — melody become rhythm, the modern drop-hook, and a third carrier of the
        //groove on the speakers that lose the sub entirely. It replaces the octave doubling there, never the
        //Keys tune.
        private static readonly Note[] MURAL_CHANT =
        {
            new(0, 0, 12, 2), new(3, 1, 12, 2), new(6, 2, 12, 2),
            new(8, 3, 12, 2), new(11, 2, 12, 2), new(14, 1, 12, 2)
        };

        private readonly struct MuralSection
        {
            public readonly bool Kick, Snap, Shaker, Sixteenths, Riff, ChorusRiff, Floor, Pad, Stab;
            public readonly MuralPart Part;
            public readonly bool Chant;
            public readonly float Level;

            public MuralSection(bool kick, bool snap, bool shaker, bool sixteenths, bool riff, bool chorusRiff,
                bool floor, bool pad, bool stab, MuralPart part, bool chant, float level)
            {
                Kick = kick; Snap = snap; Shaker = shaker; Sixteenths = sixteenths; Riff = riff;
                ChorusRiff = chorusRiff; Floor = floor; Pad = pad; Stab = stab;
                Part = part; Chant = chant; Level = level;
            }
        }

        //Nine sections, ~2:45 a pass, and the arc is the idiom's own: the bass introduces itself before any
        //tune does, and the piece's one rare event is the bass left ALONE — where Pulse's shock borrows a
        //chord from outside, Mural's is its own most characteristic voice with everything else stripped away.
        //
        //                                kick   snap  shakr  16th   riff  chor.  floor  pad   stab  part                chant  level
        private static readonly MuralSection[] MURAL_ARRANGEMENT =
        {
            //0 PRELUDE. Pad and the flat floor under the marimba's sentence, no kit and no riff — the piece
            //opens on its warmth, and the groove arriving next is an event because nothing walked in with it.
            new(false, false, false, false, false, false, true,  true,  false, MuralPart.Sentence, false, 0.42f),
            //1 GROOVE-IN. The riff enters with the kit and NO tune: the bass line is the hook, so the player
            //meets it as the piece's lead character before the marimba ever speaks. The kick holds out of the
            //first half, so its landing is the section's own arrival.
            new(true,  true,  true,  false, true,  false, false, false, false, MuralPart.None,     false, 0.85f),
            //2 VERSE. The marimba answers the riff from the cells the riff leaves empty.
            new(true,  true,  true,  false, true,  false, false, false, false, MuralPart.Verse,    false, 0.90f),
            //3 CHORUS. The hook on the Keys with the marimba an octave over it, the riff leaping to octaves,
            //the shaker doubling to sixteenths, the pad's ninth underneath: the lift is what plays, not a dial.
            new(true,  true,  true,  true,  true,  true,  false, true,  true,  MuralPart.Hook,     false, 1.00f),
            //4 VERSE, denser by the roll (rim knocks, embellished answers) rather than by parts.
            new(true,  true,  true,  false, true,  false, false, false, false, MuralPart.Verse,    false, 0.90f),
            //5 CHORUS.
            new(true,  true,  true,  true,  true,  true,  false, true,  true,  MuralPart.Hook,     false, 1.00f),
            //6 THE BREAK — the identity moment. The kit thins to a heartbeat and the bass takes the HOOK
            //itself, an octave over its riff (98–196 Hz, so the thesis survives a phone speaker); the last
            //two bars strip even that cover away and the log drum states the hook naked, a tom pickup handing
            //the piece to the final chorus. Emptier, not quieter — and what remains IS the bass, so the low
            //band holds where a conventional breakdown collapses (#186's lesson, inverted).
            new(true,  true,  true,  false, false, false, false, false, false, MuralPart.None,     false, 0.85f),
            //7 FINAL CHORUS. Everything, plus the piece's second event: the marimba stops doubling the tune
            //and CHANTS the riff's own tresillo over it — melody become rhythm for the biggest eight bars.
            new(true,  true,  true,  true,  true,  true,  false, true,  true,  MuralPart.Hook,     true,  1.00f),
            //8 OUTRO. The kit is gone, the sentence comes back over the flat floor, and the fade across the
            //whole section lands the join to the next pass in silence like the other four.
            new(false, false, false, false, false, false, true,  true,  false, MuralPart.Sentence, false, 0.65f)
        };

        //The named sections: the groove-in holds its kick back (the arrival is the event), and the break is
        //where the bass sings and then stands alone.
        private const int MURAL_SECTION_GROOVE = 1;
        private const int MURAL_SECTION_BREAK = 6;

        //How far off centre the shaker's alternate steps sit. It is this piece's image keeper — the one part
        //that never stops — so it takes the ping-pong Pulse's arp takes, a step narrower: a shaker is a pair
        //of hands, not a synthesizer.
        private const float PAN_SHAKER = 0.55f;

        //How far apart the marimba's two strikes sit around the note's own seat. The piano's precedent, for
        //the piano's reason — the width lives inside the note (two strikes a few cents apart) and the note
        //stays put — so it IS the piano's figure: if that idiom's width is ever retuned, both voices move.
        private const float PAN_MARIMBA_SPREAD = PAN_PIANO_SPREAD;

        //How far to either side the final chorus's chant ping-pongs, hit by hit. Wider than the shaker: it
        //is the loudest section's image keeper, and the marimba pair riding it keeps the hits from reading
        //as hard-panned points.
        private const float PAN_CHANT = 0.5f;

        //Where the verse's call and answer cells sit: either side, answering each other across the field —
        //two genuinely different phrases on two seats, the polka's own counter-line lesson (two answering
        //parts belong on two sides). Only in the verses, where the groove always runs underneath; the
        //sentence, which CAN be nearly alone, stays in the middle.
        private const float PAN_MARIMBA_CALL = -0.30f;
        private const float PAN_MARIMBA_ANSWER = 0.30f;

        /// <summary>
        /// What one rendering of <see cref="MusicTheme.Mural"/> rolls. The band is the set's one true tempo
        /// gap — Nocturne tops out at 104 and Bohemia starts at 112 — and it is narrow because the groove
        /// blurs above it and sags below it; the felt pulse is the half-bar sway at ~52–56, the heartbeat
        /// register, while the shaker keeps the sixteenths danceable.
        /// </summary>
        private readonly struct MuralVariation
        {
            public readonly float Bpm;
            public readonly int Transpose;
            public readonly int[] Progression;
            public readonly bool TailUp;        //the riff's closing gesture: octave up, or fifth stepping down
            public readonly bool CallLeads;     //which half of the verse motif states first
            public readonly bool PushAccents;   //the shaker leans a sixteenth ahead of the off-beats
            public readonly float Glide;        //chance per bar that the tail SLIDES into the next bar's root
            public readonly float Embellish;    //chance of an extra answer note in a verse bar
            public readonly float Knock;        //chance of the rim knock on step 7

            public MuralVariation(Random random)
            {
                Bpm = 104f + (float)random.NextDouble() * 8f;

                //Whole tones and minor thirds only, the rule Pulse states. -3 puts the lowest root at E1,
                //41 Hz — still inside the window SubBass proved audible through its harmonic ladder.
                int[] steps = { -3, -2, 0, 0, 2, 3 };
                Transpose = steps[random.Next(steps.Length)];

                Progression = MURAL_PROGRESSIONS[random.Next(MURAL_PROGRESSIONS.Length)];
                TailUp = random.NextDouble() < 0.5;
                CallLeads = random.NextDouble() < 0.65;
                PushAccents = random.NextDouble() < 0.4;
                Glide = 0.20f + (float)random.NextDouble() * 0.25f;
                Embellish = 0.25f + (float)random.NextDouble() * 0.30f;
                Knock = 0.20f + (float)random.NextDouble() * 0.30f;
            }
        }

        /// <summary>
        /// Mural (#264): a bass-led groove — the tune lives in the bass. Nine sections, ~2:45 a pass, in
        /// G major on a 3+3+2 tresillo grid. It adds two voices, the <see cref="LogDrum"/> it is written for
        /// and the <see cref="Marimba"/> that answers it, and retired the polka's clarinet with the polka.
        /// </summary>
        private static float[] BakeMural(int seed)
        {
            Random random = new(seed);
            MuralVariation variation = new(random);

            float secondsPerStep = 60f / (variation.Bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int sectionOutro = MURAL_ARRANGEMENT.Length - 1;
            int totalSteps = MURAL_ARRANGEMENT.Length * STEPS_PER_SECTION;

            //A bar of room past the end, so the outro's last floor note rings out instead of being cut.
            float[] mix = NewMix(samplesPerStep * (totalSteps + STEPS_PER_BAR));

            for (int step = 0; step < totalSteps; step++)
            {
                int at = step * samplesPerStep;
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;

                int phrase = bar % 4;
                int chord = variation.Progression[phrase];
                int nextChord = variation.Progression[(phrase + 1) % 4];

                int sectionIndex = bar / BARS_PER_SECTION;
                MuralSection section = MURAL_ARRANGEMENT[sectionIndex];
                int barInSection = bar % BARS_PER_SECTION;

                int[] arp = MURAL_ARP[chord];
                int root = MURAL_ROOT[chord];
                int transpose = variation.Transpose;

                bool lastBar = barInSection == BARS_PER_SECTION - 1;

                float fade = sectionIndex == sectionOutro
                    ? 1f - (barInSection * STEPS_PER_BAR + inBar) / (float)STEPS_PER_SECTION
                    : 1f;

                float level = section.Level * fade * fade;
                if (level <= 0.001f) continue;

                //The groove-in holds its kick out of its first half, so the kick's landing is the arrival —
                //Pulse's intro plays the same card. The break's last two bars are the piece's one rare event:
                //the bass alone, everything else silent.
                bool grooveQuiet = sectionIndex == MURAL_SECTION_GROOVE && barInSection < 4;
                bool breakSection = sectionIndex == MURAL_SECTION_BREAK;
                bool naked = breakSection && barInSection >= 6;

                //THE BREAK'S BASS -------------------------------------------------------------------------
                //The bass takes the hook itself, an octave over its riff: the melody tables one octave down
                //put every note between the chord's root + 12 and + 24 — 98–330 Hz at the authored key, to
                //392 across the transpose roll — which is where a phone speaker genuinely hears it. The
                //hook's own octave shift is deliberately dropped down here: the peak bar's +12 would push a
                //bass voice to ~660 Hz, a mid part rather than a bass singing. In the naked bars it is the
                //only thing sounding; a tom pickup at the very end hands the piece to the final chorus.
                if (breakSection)
                {
                    foreach (Note note in MURAL_HOOK[phrase])
                        if (note.Step == inBar)
                            LogDrum(mix, at, arp[note.Tone] + transpose - 12,
                                secondsPerStep * (note.Length + 0.3f), 0.5f * level);

                    if (naked)
                    {
                        if (barInSection == 7 && inBar >= 12)
                            Tom(mix, at, 96f + (inBar - 12) * 14f, 0.8f * level);

                        //Everything below is the silence the naked bars are made of.
                        continue;
                    }
                }

                //DRUMS ------------------------------------------------------------------------------------
                //The kick on the bar and on the AND of beat three — never all four beats: the tresillo is the
                //grid, and a floor under it would put the piece back on the dance floor it exists to leave.
                if (section.Kick && !grooveQuiet)
                {
                    if (breakSection) { if (inBar == 0) Kick(mix, at, 0.85f * level); }
                    else if (inBar == 0 || inBar == 10) Kick(mix, at, 0.9f * level);
                }

                if (section.Snap && (inBar == 4 || inBar == 12)) Clap(mix, at, 0.8f * level);

                //THE SHAKER, this piece's timekeeper and its image: eighths under the verses, sixteenths
                //under the choruses, ping-ponged on alternate steps — the one part that never stops, so it is
                //what keeps the field alive through the tune's rests (Pulse's arp lesson, on a pair of
                //hands). Which steps it leans on is rolled per pass: the off-beat eighths, or a sixteenth
                //ahead of them.
                if (section.Shaker)
                {
                    bool plays = section.Sixteenths || inBar % 2 == 0;
                    if (breakSection) plays = inBar % 4 == 2;   //thinned to a pulse under the singing bass

                    if (plays)
                    {
                        //The break's pulse keeps ONE authored level: the accent roll says where the groove
                        //leans, and the break has no groove to lean — left coupled, the roll silently became
                        //a 2x level switch on the section's only timekeeper.
                        bool lean = !breakSection
                            && (variation.PushAccents ? inBar % 4 == 3 : inBar % 4 == 2);

                        Hat(mix, at, open: false,
                            level: (breakSection ? 0.18f : lean ? 0.30f : 0.16f) * level,
                            pan: inBar % 2 == 0 ? -PAN_SHAKER : PAN_SHAKER);
                    }

                    //The open lift on the phrase's last push, the one place the kit looks up.
                    if (!breakSection && inBar == 14 && barInSection % 4 == 3)
                        Hat(mix, at, open: true, level: 0.22f * level, pan: PAN_HAT);
                }

                //The rim knock: a high tight tom on the seventh step, rolled per bar — the hand-drum detail
                //that stops eight sparse bars reading as sequenced. Seated by Tom's own pitch rule.
                if (section.Riff && inBar == 7 && random.NextDouble() < variation.Knock)
                    Tom(mix, at, 420f, 0.22f * level);

                //A two-hit rising tom pickup out of every full section: the handoff, ascending because the
                //groove leans forward.
                if (section.Kick && !grooveQuiet && !breakSection && lastBar && inBar >= 14)
                    Tom(mix, at, 96f + (inBar - 14) * 26f, 0.6f * level);

                //THE RIFF ---------------------------------------------------------------------------------
                if (section.Riff)
                {
                    Note[] riff = section.ChorusRiff ? MURAL_RIFF_CHORUS : MURAL_RIFF_VERSE;

                    foreach (Note note in riff)
                        if (note.Step == inBar)
                            LogDrum(mix, at, root + MURAL_RIFF_INTERVAL[note.Tone] + transpose,
                                secondsPerStep * (note.Length + 0.3f), 0.46f * level);

                    //The closing gesture: the rolled tail, or — the roll willing — the PUSH: the next bar's
                    //root an eighth early (step 14 of 16), SLIDING into the downbeat from a whole tone
                    //below. The walking bass's approach note reborn as a portamento, and the one
                    //unmistakably modern gesture the voice allows itself; kept small and off the downbeat,
                    //or the slide becomes the cartoon a log drum must never be.
                    if (inBar == 14)
                    {
                        if (random.NextDouble() < variation.Glide)
                        {
                            int target = MURAL_ROOT[nextChord] + transpose;
                            LogDrum(mix, at, target, secondsPerStep * 2.3f, 0.42f * level,
                                glideFrom: target - 2);
                        }
                        else
                        {
                            Note[] tail = variation.TailUp ? MURAL_TAIL_UP : MURAL_TAIL_DOWN;
                            foreach (Note note in tail)
                                LogDrum(mix, at, root + MURAL_RIFF_INTERVAL[note.Tone] + transpose,
                                    secondsPerStep * (note.Length + 0.3f), 0.40f * level);
                        }
                    }
                }

                //THE FLOOR --------------------------------------------------------------------------------
                //Only where the riff is NOT playing: under the groove the log drum's own tails are the floor,
                //and a sustained note beneath them would be the doubling-to-mud fault #186 documents. In the
                //kitless sections it holds dead flat — there is no kick to duck to — which is what gives the
                //prelude and the outro a bottom.
                if (section.Floor && inBar == 0)
                    SubBass(mix, at, root + transpose, secondsPerStep * (STEPS_PER_BAR + 0.4f), 0.20f * level,
                        duck: 0f, beatSeconds: 0f);

                //THE PAD ----------------------------------------------------------------------------------
                //An octave under the melody tables, with the NINTH voiced in where it is diatonic — the one
                //piece of modern-pop sugar this piece allows itself, and only here: held colour, never the
                //bass and never a melody index.
                if (section.Pad && inBar == 0)
                {
                    for (int voice = 0; voice < arp.Length; voice++)
                        Pad(mix, at, arp[voice] - 12 + transpose, secondsPerStep * 15.5f, 0.08f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));

                    if (MURAL_NINTH[chord] >= 0)
                        Pad(mix, at, MURAL_NINTH[chord] - 12 + transpose, secondsPerStep * 15.5f, 0.05f * level);
                }

                //THE STAB ---------------------------------------------------------------------------------
                //A short keys chord with the kick's off-beat push: the chorus's comp, on the one step nothing
                //else owns (the riff holds 8 and 11, the backbeat 12 — 10 is the kick's own push).
                if (section.Stab && inBar == 10)
                    for (int voice = 0; voice < arp.Length; voice++)
                        Keys(mix, at, arp[voice] - 12 + transpose, secondsPerStep * 2.5f, 0.09f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));

                //THE CHANT --------------------------------------------------------------------------------
                //The final chorus only: the marimba chants the riff's own tresillo on chord tones, ping-
                //ponged hit by hit — melody become rhythm, and a third carrier of the groove on the speakers
                //that lose the sub entirely. Seated by the HIT's index, the same stability argument ChordPan
                //makes for a chord's voices.
                if (section.Chant)
                    for (int hit = 0; hit < MURAL_CHANT.Length; hit++)
                    {
                        Note note = MURAL_CHANT[hit];
                        if (note.Step != inBar) continue;

                        Marimba(mix, at, arp[note.Tone] + note.Octave + transpose,
                            secondsPerStep * (note.Length + 0.5f), 0.20f * level,
                            pan: hit % 2 == 0 ? -PAN_CHANT : PAN_CHANT);
                    }

                //THE TUNE ---------------------------------------------------------------------------------
                if (section.Part == MuralPart.None) continue;

                if (section.Part == MuralPart.Sentence)
                {
                    //Centred: in the prelude this is very nearly the only thing sounding, and whatever may
                    //play alone belongs in the middle.
                    foreach (Note note in MURAL_SENTENCE[phrase])
                        if (note.Step == inBar)
                            Marimba(mix, at, arp[note.Tone] + note.Octave + transpose,
                                secondsPerStep * (note.Length + 0.5f), 0.30f * level, pan: PAN_CENTRE);

                    continue;
                }

                if (section.Part == MuralPart.Verse)
                {
                    //Call and answer, a bar each, in the cells the riff leaves empty, ANSWERING ACROSS THE
                    //FIELD — the call from one side, the answer from the other. Which states first is the
                    //pass's own; the answer lands on the root, so every second bar comes home.
                    bool callBar = (bar % 2 == 0) == variation.CallLeads;
                    Note[] cell = callBar ? MURAL_CALL : MURAL_ANSWER;
                    float seat = callBar ? PAN_MARIMBA_CALL : PAN_MARIMBA_ANSWER;

                    foreach (Note note in cell)
                        if (note.Step == inBar)
                            Marimba(mix, at, arp[note.Tone] + note.Octave + transpose,
                                secondsPerStep * (note.Length + 0.5f), 0.30f * level, pan: seat);

                    //One extra chord tone in a cell the written bar leaves empty — on the grid, drawn from
                    //the chord, so it can only ever sound like part of the tune. It takes the bar's own seat.
                    if (inBar == 9 && random.NextDouble() < variation.Embellish)
                        Marimba(mix, at, arp[2] + transpose, secondsPerStep * 1.5f, 0.22f * level,
                            pan: seat);

                    continue;
                }

                //The hook: the Keys carry it dead centre — it is the tune of the piece's biggest sections,
                //and the rule this file learned twice is that whatever carries a piece belongs in the middle.
                //The marimba doubles an octave up, except where the chant already owns that register.
                foreach (Note note in MURAL_HOOK[phrase])
                {
                    if (note.Step != inBar) continue;

                    int pitch = arp[note.Tone] + note.Octave + transpose;

                    Keys(mix, at, pitch, secondsPerStep * (note.Length + 1.2f), 0.30f * level,
                        pan: PAN_CENTRE);

                    if (!section.Chant)
                        Marimba(mix, at, pitch + 12, secondsPerStep * (note.Length + 0.5f), 0.17f * level,
                            pan: PAN_CENTRE);
                }
            }

            //The set's own target: a groove is not quieter than a dance track, and a level step between two
            //entries of one set is the thing this number exists to prevent.
            Limit(mix, targetRms: 0.20f, ceiling: 0.95f);

            return mix;
        }

        #endregion

        #region Ember — the fifth theme (#163)

        //A ROCK BALLAD, and what makes it a fifth piece rather than a fifth chord pool is GAIN. Pulse and
        //Bohemia differ in mode over one dance floor, Nocturne in harmony and in time, Mural in its grid and
        //its register; this one differs in the amplifier. Three consequences follow from that one fact, and
        //each of them is a decision the other four pieces never had to make.
        //
        //THE CHORDS HAVE NO THIRD IN THEM. Distortion is a non-linearity, so notes played into one do not
        //merely add — they multiply, and the sum and difference tones of every pair land back in the signal.
        //A fifth is a 3:2 ratio and its intermodulation products are the same notes again an octave or two
        //away; a major third is 5:4 and its products are not, which is why a driven third turns to porridge
        //and a driven fifth turns to a wall. Rock plays root-fifth-octave because of what the amp does to
        //anything else, and this piece plays them for the same reason. The chord's third is left to the pad,
        //the keys and the tune, which are clean.
        //
        //THE BUILD IS THE GAIN AND NOT THE FADER. Every section carries a Drive: a picked 1.1 in the verses,
        //a crunch of 4.5 in the pre-chorus, 9 in the choruses and 11 under the solo. The waveshaper is
        //normalised at its output, so pushing it changes the SPECTRUM and not the level — a chorus is louder
        //because it has more harmonics and more parts in it, which is the lesson every measurement in this
        //file keeps arriving at from a different direction.
        //
        //THE TIME HALVES AND DOUBLES. The verses are HALF-TIME — the snare on the third beat alone, so a bar
        //notated at ~134 is felt at ~67 — and the choruses put the backbeat on two and four, which doubles the
        //felt tempo with no dial moved. That is what a power ballad's chorus actually does, and it is also
        //what keeps sixteenths available for the fills at a tempo that feels like seventy.
        //
        //E minor, because the lowest note on a guitar is E2 and that is why so much of this music lives there.

        //The guitar's own roots, low on the neck and inside a fifth of one another, so the progression walks
        //rather than leaps. The bass plays these as written and the floor an octave under them.
        private static readonly int[] EMBER_ROOT = { 40, 48, 43, 50, 45, 47 };   //Em C G D Am Bm

        //Root-position triads plus the octave, in the register the pad and the tune share. The tune indexes
        //them as chord tones like every melody in this file, so tone 0 is always the root of whatever is
        //underneath — which is what lets the progression be rolled per pass.
        private static readonly int[][] EMBER_ARP =
        {
            new[] { 52, 55, 59, 64 },   //0 Em  E3 G3 B3 E4
            new[] { 60, 64, 67, 72 },   //1 C   C4 E4 G4 C5
            new[] { 55, 59, 62, 67 },   //2 G   G3 B3 D4 G4
            new[] { 62, 66, 69, 74 },   //3 D   D4 F#4 A4 D5
            new[] { 57, 60, 64, 69 },   //4 Am  A3 C4 E4 A4
            new[] { 59, 62, 66, 71 }    //5 Bm  B3 D4 F#4 B4
        };

        //Four bars, every one opening at home. The last of the five is the one worth naming: it uses the MINOR
        //v, and the piece has no raised leading tone anywhere. Rock is modal — the flat seventh is its colour,
        //and a D sharp under an E minor would be a classical cadence in a piece that has not earned one. It is
        //the exact opposite of Pulse's shock, which borrows that leading tone once and makes an event of it.
        private static readonly int[][] EMBER_PROGRESSIONS =
        {
            new[] { 0, 1, 2, 3 },   //Em C  G  D   — i VI III VII, the ballad progression itself
            new[] { 0, 2, 1, 3 },   //Em G  C  D
            new[] { 0, 4, 1, 3 },   //Em Am C  D
            new[] { 0, 3, 4, 1 },   //Em D  Am C   — the falling one
            new[] { 0, 2, 5, 3 }    //Em G  Bm D   — the minor v, and no leading tone in it
        };

        private enum EmberPart { None, Verse, Chorus, Solo }

        //THE VERSE, carried by the keys: stepwise, in the chord's own register, and it starts AFTER the
        //downbeat in three bars of four, because a sung line breathes where a synth line does not. Short
        //notes and a lot of them against the chorus's long ones — the same contrast Pulse's verse is written
        //for, at half the speed.
        private static readonly Note[][] EMBER_VERSE =
        {
            new[] { new Note(2, 0, 0, 3), new Note(6, 1, 0, 3), new Note(10, 2, 0, 5) },
            new[] { new Note(2, 1, 0, 3), new Note(6, 0, 0, 3), new Note(10, 1, 0, 6) },
            new[] { new Note(2, 2, 0, 3), new Note(6, 1, 0, 3), new Note(10, 3, 0, 5) },
            new[] { new Note(0, 2, 0, 6), new Note(8, 1, 0, 7) }
        };

        //THE CHORUS, on the driven guitar and an octave up: climb, hold, the same cell a step higher, and one
        //note held through the last bar. The sequence in the third bar is the oldest trick there is for making
        //a tune sound inevitable, and it is the one Bohemia's theme is built on; what is different here is the
        //fourth bar, which is a single note where every other piece in this file puts a phrase. A ballad's
        //hook is the note the singer holds, and holding it is what the section is arranged around.
        private static readonly Note[][] EMBER_CHORUS =
        {
            new[] { new Note(0, 0, 12, 6), new Note(6, 1, 12, 4), new Note(10, 2, 12, 6) },
            new[] { new Note(0, 3, 12, 12), new Note(12, 2, 12, 4) },
            new[] { new Note(0, 1, 12, 6), new Note(6, 2, 12, 4), new Note(10, 3, 12, 6) },
            new[] { new Note(0, 2, 12, 15) }
        };

        //THE SOLO, and a rock ballad without one is a pop song. It is the chorus's material moving: sixteenths
        //where the tune holds, the top of the range where the tune sits under it, and it lands on a held note
        //of its own so the final chorus arrives out of a ring rather than out of a run.
        //
        //It stays inside a guitar's actual range — arp[3] + 12 tops out at D6, which is the twenty-second fret
        //of the top string. That is the file's own lesson about register belonging to the instrument, and the
        //reason the peak is not written an octave higher the way Pulse's chorus is: Pulse's peak is a synth.
        private static readonly Note[][] EMBER_SOLO =
        {
            new[] { new Note(0, 3, 12, 2), new Note(2, 2, 12, 2), new Note(4, 1, 12, 3), new Note(8, 2, 12, 3), new Note(12, 3, 12, 4) },
            new[] { new Note(0, 2, 12, 2), new Note(2, 3, 12, 4), new Note(8, 1, 12, 2), new Note(10, 2, 12, 6) },
            new[] { new Note(0, 3, 12, 3), new Note(4, 2, 12, 2), new Note(6, 3, 12, 2), new Note(8, 2, 12, 3), new Note(12, 1, 12, 4) },
            new[] { new Note(0, 3, 12, 11), new Note(12, 2, 12, 4) }
        };

        //The picking pattern, one note to an eighth: the thumb takes the bass string at each half-bar and the
        //fingers walk the chord between. Every ballad opens on this texture, and here it is also the only
        //thing sounding before the drums arrive and the last thing left after they go.
        private static readonly int[] EMBER_PICK = { 0, 1, 2, 3, 0, 2, 1, 3 };

        private readonly struct EmberSection
        {
            public readonly bool Kit;      //drums at all
            public readonly bool Full;     //full time rather than half — the backbeat on two and four
            public readonly bool Bass, Floor, Clean, Power, Pad, Str;
            public readonly EmberPart Part;
            public readonly float Drive;   //how hard the amp is pushed; read by the power chords and the lead
            public readonly float Level;

            public EmberSection(bool kit, bool full, bool bass, bool floor, bool clean, bool power, bool pad,
                bool str, EmberPart part, float drive, float level)
            {
                Kit = kit; Full = full; Bass = bass; Floor = floor; Clean = clean;
                Power = power; Pad = pad; Str = str; Part = part; Drive = drive; Level = level;
            }
        }

        //The form is the idiom's own: intro, verse, pre-chorus, CHORUS, verse, pre-chorus, CHORUS, solo,
        //CHORUS, outro. Ten sections, ~2:25 a pass. Everything the genre does with dynamics is in this table
        //and in the Drive column beside it — the verses drop back to a picked guitar under a half-time kit,
        //the pre-chorus puts the amp into crunch, and the chorus doubles the backbeat and opens the gain.
        //
        //                                 kit    full   bass  floor  clean  power  pad    str   part               drive  level
        private static readonly EmberSection[] EMBER_ARRANGEMENT =
        {
            //0 INTRO. The picked guitar and a pad over the floor, no kit at all — the piece states its chords
            //on the instrument it is about, and the drums arriving in the verse is the first event in it.
            new(false, false, false, true,  true,  false, true,  false, EmberPart.None,   1.1f, 0.55f),
            //1 VERSE. Half-time: the snare on the third beat alone, which is the whole of why a piece at 134
            //feels like a piece at 67. The tune is on the keys and the guitar is still clean.
            new(true,  false, true,  true,  true,  false, true,  false, EmberPart.Verse,  1.1f, 0.82f),
            //2 PRE-CHORUS. The amp comes up and the power chords arrive under the same tune and the same
            //half-time kit — so what changes going into the chorus is the gain and the time, and neither of
            //them is a volume. The tom fill across this section's last bar is written by the bake rather than
            //flagged here (see the fill below).
            new(true,  false, true,  true,  true,  true,  true,  false, EmberPart.Verse,  4.5f, 0.92f),
            //3 CHORUS. Full time, both rhythm guitars, the crash on the downbeat and the tune on the lead.
            new(true,  true,  true,  true,  false, true,  true,  false, EmberPart.Chorus, 9f,   1.00f),
            //4 VERSE. Back down, and the drop after a chorus is what makes the next one bigger. The floor and
            //the pad stay: a section that is quiet rather than absent is the thing #186 measured on Pulse.
            new(true,  false, true,  true,  true,  false, true,  false, EmberPart.Verse,  1.1f, 0.84f),
            //5 PRE-CHORUS.
            new(true,  false, true,  true,  true,  true,  true,  false, EmberPart.Verse,  4.5f, 0.94f),
            //6 CHORUS.
            new(true,  true,  true,  true,  false, true,  true,  false, EmberPart.Chorus, 9f,   1.00f),
            //7 SOLO, with the string section arriving under it. The strings are held back for the last third
            //of the piece deliberately — it is what the idiom does, and it is the one way left to make the
            //final chorus bigger than the two that came before it without touching a level.
            new(true,  true,  true,  true,  false, true,  true,  true,  EmberPart.Solo,   11f,  1.00f),
            //8 CHORUS, the last one and the fullest: the tune back over the strings.
            new(true,  true,  true,  true,  false, true,  true,  true,  EmberPart.Chorus, 9f,   1.00f),
            //9 OUTRO. The kit and the wall are gone and the picked guitar is left alone over the fade, so the
            //piece ends where it started and the join to the next pass lands in silence like the other four.
            new(false, false, false, true,  true,  false, true,  false, EmberPart.None,   1.1f, 0.60f)
        };

        /// <summary>The picked guitar's amp, and it is a constant: the clean part stays clean all the way
        /// through, and it is the <i>other</i> guitar that is driven. Not zero — a valve amp at the edge of
        /// breakup is what "clean" means on this instrument, and a perfectly linear one sounds like a DI.</summary>
        private const float EMBER_CLEAN_DRIVE = 1.1f;

        //The two rhythm guitars, wide and opposite. This is the piece's whole image and it is #119's rule
        //stated in the idiom's own practice: a rock mix double-tracks the rhythm part and puts the two takes
        //hard either side, and it works because they are two PERFORMANCES — different tuning by a hair,
        //different pick attack, different phase — rather than one signal at two levels.
        private const float PAN_GUITAR_SPREAD = 0.78f;

        //The picked guitar is double-tracked too, narrower than the wall. It was ONE guitar at -0.38 first,
        //answered by the keys on the right, and the piece measured 0.51 dB left against a set that holds
        //0.15: the intro and the outro have no keys in them at all, so a third of the piece was a lone part
        //sitting off centre with nothing opposite it. Two takes either side is the fix that keeps the width —
        //centring it would have made the intro mono — and it is the same thing the wall does one gain up.
        private const float PAN_GUITAR_PICKED = 0.45f;

        /// <summary>The crash, opposite the ride: the two cymbals of a chorus, one either side of the kit.</summary>
        private const float PAN_CRASH = 0.26f;

        /// <summary>
        /// What one rendering of <see cref="MusicTheme.Ember"/> rolls. The tempo band is the piece's one
        /// oddity: it is <b>notated</b> at 128–140 and <b>felt</b> at half that, because the verses put the
        /// snare on the third beat alone. A ballad written at 67 would leave a sixteenth two thirds of a
        /// second long, which is too coarse a grid for a fill or a solo to be played on.
        /// </summary>
        private readonly struct EmberVariation
        {
            public readonly float Bpm;
            public readonly int Transpose;
            public readonly int[] Progression;
            public readonly float Embellish;   //chance of the bass filling into the next bar

            public EmberVariation(Random random)
            {
                Bpm = 128f + (float)random.NextDouble() * 12f;   //128-140, felt as 64-70

                //Whole tones and minor thirds only, the rule Pulse states.
                int[] steps = { -3, -2, 0, 0, 2, 3 };
                Transpose = steps[random.Next(steps.Length)];

                Progression = EMBER_PROGRESSIONS[random.Next(EMBER_PROGRESSIONS.Length)];
                Embellish = 0.25f + (float)random.NextDouble() * 0.30f;
            }
        }

        /// <summary>
        /// Ember (#163): a rock ballad. Ten sections, ~2:25 a pass, in E minor, and it adds two voices — the
        /// electric guitar it is written for and the crash its choruses arrive on. Everything else is the
        /// kit, the bass, the floor, the keys, the pad and the string section the other four pieces already
        /// play.
        /// </summary>
        private static float[] BakeEmber(int seed)
        {
            Random random = new(seed);
            EmberVariation variation = new(random);

            float secondsPerStep = 60f / (variation.Bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int sectionOutro = EMBER_ARRANGEMENT.Length - 1;
            int totalSteps = EMBER_ARRANGEMENT.Length * STEPS_PER_SECTION;

            //A bar of room past the end: the last chorus's crash rings for well over a second, and a cymbal
            //cut off mid-wash is the click the fanfares' own tail constants exist to prevent.
            float[] mix = NewMix(samplesPerStep * (totalSteps + STEPS_PER_BAR));

            for (int step = 0; step < totalSteps; step++)
            {
                int at = step * samplesPerStep;
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;

                int phrase = bar % 4;
                int chord = variation.Progression[phrase];

                int sectionIndex = bar / BARS_PER_SECTION;
                EmberSection section = EMBER_ARRANGEMENT[sectionIndex];
                int barInSection = bar % BARS_PER_SECTION;

                int[] arp = EMBER_ARP[chord];
                int root = EMBER_ROOT[chord];
                int transpose = variation.Transpose;

                bool lastBar = barInSection == BARS_PER_SECTION - 1;

                float fade = sectionIndex == sectionOutro
                    ? 1f - (barInSection * STEPS_PER_BAR + inBar) / (float)STEPS_PER_SECTION
                    : 1f;

                float level = section.Level * fade * fade;
                if (level <= 0.001f) continue;

                //THE FILL is written where it is NEEDED rather than flagged in the table: it belongs in the
                //last bar before the time doubles, which the arrangement already states by putting Full on
                //the next section. So the two pre-choruses get it and the chorus that runs into the solo does
                //not — both correct, and neither is a column anybody has to keep in step.
                bool intoFullTime = !section.Full
                    && sectionIndex + 1 < EMBER_ARRANGEMENT.Length
                    && EMBER_ARRANGEMENT[sectionIndex + 1].Full;

                bool filling = intoFullTime && lastBar && inBar >= 8;

                //THE KIT ----------------------------------------------------------------------------------
                if (section.Kit)
                {
                    if (section.Full)
                    {
                        //Full time: the backbeat on two and four with a kick answering it either side. The
                        //felt tempo doubles here, and that — not a fader — is what a chorus opening up IS.
                        if (inBar == 0 || inBar == 6 || inBar == 8 || inBar == 14) Kick(mix, at, 0.95f * level);
                        if (inBar == 4 || inBar == 12) Snare(mix, at, 0.32f * level);

                        //The ride rather than the hats: a rock chorus is ridden, and the ride's own seat is
                        //across the kit from where the verses' hats were.
                        if (inBar % 2 == 0) Hat(mix, at, open: false, level: 0.13f * level, pan: PAN_RIDE);

                        //The crash on the first downbeat of the section and again halfway through it: the
                        //arrival is worth announcing once and reminding of once, and a crash on every bar is
                        //a drummer nobody wants to record.
                        //Struck about as hard as the snare — measured, 0.7 puts its peak where a backbeat's
                        //is, and a crash a drummer has to be asked to hit is not what a chorus arrives on.
                        if (inBar == 0 && barInSection % 4 == 0) Crash(mix, at, 0.7f * level);
                    }
                    else if (!filling)
                    {
                        //HALF TIME. The snare on the third beat alone is the entire trick: the bar is notated
                        //at ~134 and felt at ~67, so the piece is slow without a slow grid under it.
                        if (inBar == 0 || inBar == 10) Kick(mix, at, 0.9f * level);
                        if (inBar == 8) Snare(mix, at, 0.30f * level);
                        if (inBar % 2 == 0) Hat(mix, at, open: inBar == 14, level: 0.20f * level);
                    }

                    //THE CRESCENDO FILL. Eighths, then sixteenths, and louder as it goes — a fill accelerates
                    //and grows, which is what makes it a hand-off rather than a decoration. Tom seats itself
                    //by pitch, so a descending run travels across the kit on its own.
                    if (filling)
                    {
                        float through = (inBar - 8) / 7f;
                        bool hit = inBar >= 12 || inBar % 2 == 0;   //eighths for a beat, then sixteenths

                        if (inBar == 8) Snare(mix, at, 0.30f * level);
                        if (hit) Tom(mix, at, 150f - (inBar - 8) * 11f, (0.55f + 0.85f * through) * level);
                    }

                    //A short run off the end of a full-time section, so a chorus hands on rather than stops.
                    if (section.Full && lastBar && inBar >= 13)
                        Tom(mix, at, 132f - (inBar - 13) * 18f, 0.9f * level);
                }

                //THE FLOOR --------------------------------------------------------------------------------
                //Held a whole bar and a little past it, ducking to whichever pulse the drummer is playing —
                //every beat under a full-time chorus, every OTHER beat under a half-time verse. The duck
                //follows the kick because the duck exists to keep the two out of each other's way (#186), so
                //a piece whose kick pattern halves has to halve it too.
                if (section.Floor && inBar == 0)
                {
                    SubBass(mix, at, root - 12 + transpose, secondsPerStep * (STEPS_PER_BAR + 0.4f),
                        0.26f * level,
                        duck: section.Kit ? 0.5f : 0f,
                        beatSeconds: secondsPerStep * (section.Full ? 4f : 8f));
                }

                //THE BASS ---------------------------------------------------------------------------------
                //Quarter notes under a chorus and half notes under a verse: the bass player doubles up with
                //the drummer, which is the same event in another register.
                if (section.Bass)
                {
                    bool onBeat = section.Full ? inBar % 4 == 0 : inBar % 8 == 0;
                    if (onBeat) Bass(mix, at, root + transpose, secondsPerStep * 3.4f, level);

                    //The fill into the next bar, up to the fifth. Rolled per pass, so it is variation rather
                    //than a figure the ear learns.
                    if (inBar == 14 && random.NextDouble() < variation.Embellish)
                        Bass(mix, at, root + 7 + transpose, secondsPerStep * 1.6f, 0.8f * level);
                }

                //THE PAD ----------------------------------------------------------------------------------
                //An octave under the chord tables, out of the lead's way: the tune is written at arp + 12 and
                //a pad in the same octave as its own melody is mud rather than support.
                if (section.Pad && inBar == 0)
                    for (int voice = 0; voice < arp.Length; voice++)
                        Pad(mix, at, arp[voice] - 12 + transpose, secondsPerStep * 15.5f, 0.075f * level,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));

                //THE STRINGS ------------------------------------------------------------------------------
                if (section.Str && inBar == 0)
                    foreach (int note in arp)
                        Strings(mix, at, note + transpose, secondsPerStep * 15.4f, 0.05f * level);

                //THE WALL ---------------------------------------------------------------------------------
                //Two guitars, two takes, hard either side, struck on the bar and again on the half — a ballad
                //RINGS where a rock song chugs, so the chord is let sound and re-struck rather than played in
                //eighths. Both are power chords: root, fifth, octave, and the Guitar voice sums them into one
                //waveshaper because that is where the sound comes from (see its own remarks).
                if (section.Power && (inBar == 0 || inBar == 8))
                {
                    float held = secondsPerStep * (STEPS_PER_BAR * 0.5f + 1.2f);
                    float hit = (inBar == 0 ? 0.30f : 0.25f) * level;

                    //Which take sits on which side alternates with every strike, and that is not a taste — it
                    //is what a measurement forced. Two takes seated symmetrically do not balance: the piece
                    //read 0.2 dB left with both of them at ±0.78 and the wall on its own measuring 0.3 dB
                    //RIGHT, which is arithmetic until the cross-term is written down. The bass and the floor
                    //play the guitar's own root, so each take is CORRELATED with what is in the middle, and
                    //the two correlations differ (the takes are detuned either side of the pitch, so they
                    //beat against the centre at different rates). What leans the mix is that cross-term and
                    //not either take's level. Swapping the sides twice a bar cancels it, and it is inaudible
                    //by construction — both sides carry the same chord either way, and the two differ by
                    //under five cents of tuning.
                    int left = inBar == 0 ? 0 : 1;

                    Guitar(mix, at, root + transpose, held, hit, section.Drive,
                        power: true, take: left, pan: -PAN_GUITAR_SPREAD);
                    Guitar(mix, at, root + transpose, held, hit, section.Drive,
                        power: true, take: 1 - left, pan: PAN_GUITAR_SPREAD);
                }

                //THE PICKED GUITAR ------------------------------------------------------------------------
                //Let ring: every note lasts more than twice the gap to the next one, so the pattern is a wash
                //and not eight plinks. That overlap is the whole texture and it costs nothing but length.
                if (section.Clean && inBar % 2 == 0)
                {
                    int eighth = inBar / 2;
                    bool thumb = eighth % 4 == 0;
                    int pitch = arp[EMBER_PICK[eighth]] - (thumb ? 12 : 0) + transpose;
                    float picked = (thumb ? 0.16f : 0.12f) * level;

                    //Sides alternate note by note, for the reason the wall's do — and the half-bar is folded
                    //in, so that the two THUMB notes, which are the loud ones, do not both land on the same
                    //take. Alternating on the note index alone put them there and measured 0.34 dB of lean in
                    //the verses; what has to alternate is the energy and not the count.
                    int left = (eighth % 2) ^ (eighth / 4);

                    Guitar(mix, at, pitch, secondsPerStep * 4.6f, picked, EMBER_CLEAN_DRIVE,
                        take: left, pan: -PAN_GUITAR_PICKED);
                    Guitar(mix, at, pitch, secondsPerStep * 4.6f, picked, EMBER_CLEAN_DRIVE,
                        take: 1 - left, pan: PAN_GUITAR_PICKED);
                }

                //THE TUNE ---------------------------------------------------------------------------------
                if (section.Part == EmberPart.None) continue;

                Note[] line = section.Part switch
                {
                    EmberPart.Chorus => EMBER_CHORUS[phrase],
                    EmberPart.Solo => EMBER_SOLO[phrase],
                    _ => EMBER_VERSE[phrase]
                };

                foreach (Note note in line)
                {
                    if (note.Step != inBar) continue;

                    int pitch = arp[note.Tone] + note.Octave + transpose;

                    //Legato, by the arithmetic the fanfares record: every note runs past the next one's start,
                    //so a phrase releases across its own joins instead of arriving as a row of events.
                    float held = secondsPerStep * (note.Length + 1.2f);

                    if (section.Part == EmberPart.Verse)
                    {
                        //The verse is sung, and the keys are what sings it — CENTRED, off their own seat,
                        //because here they are the tune and the rule this file learned on Nocturne is that
                        //whatever carries a piece alone belongs in the middle. The guitar either side of it
                        //is what the width is for.
                        Keys(mix, at, pitch, held, 0.24f * level, pan: PAN_CENTRE);
                    }
                    else
                    {
                        //The lead guitar, and it is CENTRED: it is the only thing carrying the tune, and the
                        //rule this file learned twice is that whatever may be alone belongs in the middle.
                        //The wall either side of it is what makes that read as wide rather than as narrow.
                        Guitar(mix, at, pitch, held, 0.26f * level, section.Drive);
                    }
                }
            }

            //The set's own target. A ballad is not quieter than a groove; what makes it a ballad is what is in
            //it, and a level step between two entries of one set is the thing this number exists to prevent.
            Limit(mix, targetRms: 0.20f, ceiling: 0.95f);

            return mix;
        }

        #endregion

        #region The front end's piece

        /// <summary>
        /// The front end's piece (#46): a small arrangement of its own rather than one texture. It opens on
        /// held pad chords over the theme's diatonic progressions, their root an octave under for warmth,
        /// with a quarter-note line on the lobby's <see cref="Keys"/> — an electric-piano voice, not the
        /// theme's square Arp, which exposed at this rate read as a touch-tone phone — and a high sparkle
        /// every other bar; after two rounds the <b>groove</b> arrives (kick, off-beat hats, the theme's own
        /// bass figure), a step under the theme's energy so the lobby stays a lobby; then the <b>refrain</b>
        /// (<see cref="MENU_HOOK"/>) is stated twice over it and the groove walks it off. It rolls its own
        /// tempo, key, progression and line direction from the seed, so no two runs share a lobby.
        /// <para>
        /// It is a LOOP, and the seam is closed by construction rather than by luck: the piece is rendered
        /// with a bar of room past the loop point, and whatever rings into that room — a pad's release, an
        /// arp's tail — is <b>folded back onto the head</b> before the cut. Continuous play of a loop is
        /// exactly the head plus the previous pass's ring-out, so the join carries the same overlap every
        /// other bar boundary does and nothing marks it.
        /// </para>
        /// </summary>
        //THE LOBBY'S REFRAIN, written as chord tones like every melody here, so it transposes itself across
        //whatever progression the run rolled and is consonant by construction. One syncopated motif per bar —
        //bounce off the top, land on the third, a two-note pickup into the next bar — stated identically
        //across the first three chords, which is what makes it a hook rather than a line.
        private static readonly Note[] MENU_HOOK =
        {
            new(0, 3, 12, 4), new(4, 2, 12, 2), new(6, 3, 12, 2), new(8, 1, 12, 6), new(14, 2, 12, 2)
        };

        //The fourth bar resolves instead of bouncing: up onto the held octave root, so each pass of the hook
        //ENDS somewhere rather than merely stopping — the difference between a refrain and a loop.
        private static readonly Note[] MENU_HOOK_CLOSE =
        {
            new(0, 3, 12, 4), new(4, 2, 12, 4), new(8, 0, 24, 8)
        };

        private static float[] BakeMenu(int seed)
        {
            Random random = new(seed);

            //Unhurried but moving, and rolled inside a narrow band like the theme's. It started at 80–92 and
            //dragged; up here the line walks instead of trudging, while the pads keep it a lobby rather than
            //a dancefloor — with no kick this is still a harmonic rhythm more than a beat.
            float bpm = 94f + (float)random.NextDouble() * 12f;
            float secondsPerStep = 60f / (bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int[] progression = PROGRESSIONS[random.Next(PROGRESSIONS.Length)];
            int[] keys = { -3, -2, 0, 0, 2, 3, 5 };   //the Variation's own set: whole tones and minor thirds
            int transpose = keys[random.Next(keys.Length)];
            bool arpDown = random.NextDouble() < 0.35;

            //The arrangement, grown by ear in three asks: two rounds of the progression on pads and keys
            //alone, the groove for two (once the opening has said itself, the bass arrives), the REFRAIN for
            //two — the hook stated twice over the running groove — and the groove again to walk it off. The
            //loop runs about 77 s, so the refrain returns now and then rather than nagging, and wrapping from
            //the full groove back to the bare opening reads as a breakdown rather than a restart.
            const int INTRO_BARS = 8;
            const int REFRAIN_START = 16;
            const int REFRAIN_END = 24;
            const int LOOP_BARS = 32;
            int loopSamples = samplesPerStep * LOOP_BARS * STEPS_PER_BAR;
            int tailSamples = samplesPerStep * STEPS_PER_BAR;

            float[] mix = NewMix(loopSamples + tailSamples);

            for (int step = 0; step < LOOP_BARS * STEPS_PER_BAR; step++)
            {
                int at = step * samplesPerStep;
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;

                int chord = progression[bar % 4];
                int[] arp = CHORD_ARP[chord];

                bool groove = bar >= INTRO_BARS;
                bool refrain = bar >= REFRAIN_START && bar < REFRAIN_END;

                //The chord, held the bar long, with its root an octave under for the warmth a bass would
                //otherwise bring.
                if (inBar == 0)
                {
                    for (int voice = 0; voice < arp.Length; voice++)
                        Pad(mix, at, arp[voice] + transpose, secondsPerStep * 15.5f, 0.11f,
                            pan: ChordPan(voice, arp.Length, PAN_PAD_SPREAD));

                    //The root an octave under is the lobby's bass, so it stays in the middle with the rest of
                    //the low end rather than taking a seat in the chord above it.
                    Pad(mix, at, CHORD_ROOT[chord] - 12 + transpose, secondsPerStep * 15.5f, 0.09f);
                }

                //The groove: a four-on-the-floor kick, off-beat hats and the theme's own off-beat bass —
                //unmistakably a beat, still a lobby's: no clap, no ride, no build, and everything a step
                //under the theme's levels. The kick landing on the section's first downbeat is the arrival,
                //the intro's own trick.
                if (groove)
                {
                    if (inBar % 4 == 0) Kick(mix, at, 0.55f);
                    if (inBar % 2 == 0) Hat(mix, at, open: inBar % 4 == 2, level: 0.16f);

                    //The theme's bass figure at the theme's registers: off-beat eighths against the kick
                    //(on the beat the two double and go to mud), and the root restated on the bar.
                    if (inBar % 4 == 2) Bass(mix, at, CHORD_ROOT[chord] + 12 + transpose, secondsPerStep * 1.7f, 0.75f);
                    if (inBar == 0) Bass(mix, at, CHORD_ROOT[chord] + transpose, secondsPerStep * 1.4f, 0.65f);
                }

                //A quarter-note line on the lobby's keys — half the theme's rate, each note ringing a step
                //past the next one's start, so the line is a phrase rather than four separate plinks. The
                //theme's square Arp sat here first and read as a touch-tone phone; see Keys for why. It
                //stands aside for the refrain: two keys lines at once is mud, not counterpoint.
                if (!refrain && inBar % 4 == 0)
                {
                    int arpStep = (inBar / 4) % 4;
                    int index = arpDown ? 3 - arpStep : arpStep;
                    Keys(mix, at, arp[index] + 12 + transpose, secondsPerStep * 5.5f, 0.12f);
                }

                //One high sparkle halfway through every other bar, on the same keys two octaves up — the
                //tine partial does the glitter up there, quieter than the line it decorates. It rests over
                //the refrain too: the hook holds a note exactly where it would land.
                if (!refrain && inBar == 8 && bar % 2 == 1)
                    Keys(mix, at, arp[3] + 24 + transpose, secondsPerStep * 6f, 0.07f);

                //THE REFRAIN: the hook, a shade over the line's level because it is the one thing here that
                //asks to be listened to, closing on the held octave every fourth bar.
                if (refrain)
                {
                    Note[] hook = bar % 4 == 3 ? MENU_HOOK_CLOSE : MENU_HOOK;

                    foreach (Note note in hook)
                        if (note.Step == inBar)
                            Keys(mix, at, arp[note.Tone] + note.Octave + transpose,
                                secondsPerStep * (note.Length + 1.5f), 0.15f);
                }
            }

            //Close the seam: fold what rings past the loop point back onto the head, then cut to the loop.
            //Both counts are FRAMES, so every index into the interleaved buffer is twice one — the fold has to
            //carry both channels of the tail onto both channels of the head or the seam opens in one ear only.
            float[] loop = NewMix(loopSamples);
            Array.Copy(mix, loop, loopSamples * 2);
            for (int i = 0; i < tailSamples * 2; i++) loop[i] += mix[loopSamples * 2 + i];

            Limit(loop, targetRms: 0.12f, ceiling: 0.9f);
            return loop;
        }

        #endregion

        #region The fanfares

        //A major triad and its extensions, as semitone offsets from the piece's root. The victory fanfare is
        //MAJOR where the level's theme is minor, and that single change of mode does most of the work: after
        //two minutes in A minor, a major chord is unmistakably "you won" before a note of melody has played.
        private static readonly int[] MAJOR_TRIAD = { 0, 4, 7, 12 };
        private static readonly int[] MINOR_TRIAD = { 0, 3, 7, 12 };

        //Steps of room past the last bar, so the closing chord can ring out and fade instead of the buffer
        //ending under it — without this the pad is cut off mid-sustain and the piece finishes on a click,
        //which is a poor way to be told anything. Sized off the longest closing note (26 steps from the top of
        //a 16-step bar, so ten past the end) with a little margin; the pad fades itself to nothing over its
        //own last half-second, so any more than that is silence nobody hears.
        //ONE PER PIECE, and that split is the whole point of the pair. The room a fanfare needs past its last
        //bar is decided by its own LONGEST closing note, so a single number for both is a number that fits
        //neither: #185 cut it to 8 for the victory — whose pad now stops at 14 steps, so 8 is ample — and that
        //same cut truncated the DEFEAT's closing pad, which is held 26 steps from the top of its last bar and
        //therefore needs 10 past the end before its own half-second fade is even reached. Measured at the
        //shared 8: the defeat's last five milliseconds sat at 37-39 % of the piece's own peak, i.e. it was
        //cut off mid-note — a click, which is exactly the fault these constants exist to prevent. At 14 both
        //pieces measure 0.0 %. Change either only against the piece's own longest note.
        private const int VICTORY_TAIL_STEPS = 8;
        private const int DEFEAT_TAIL_STEPS = 14;

        /// <summary>
        /// Above this the loss was close enough to deserve the <b>fuller</b> piece: the harmony under the
        /// melody, and since #190 a fourth bar to carry it. One constant for both, so "the run deserved better"
        /// is a single step the piece grows by rather than two thresholds that could drift apart.
        /// </summary>
        private const float DEFEAT_FULLER = 0.55f;

        /// <summary>
        /// The victory fanfare: a eurodance DROP, not a herald's call. The first version was a lone trombone
        /// rising over a pad — dignified, and reported as exactly that: old, cheap, nothing to dance to. But
        /// the game's musical language is the theme's eurodance, and in that language a celebration is the
        /// drop — a four-on-the-floor kick, the off-beat bass pump, and the supersaw lead punching a
        /// syncopated hook over I–V–vi–IV, the most euphoric progression pop owns. The player just won; the
        /// music should make them want to dance, not stand to attention.
        /// <para>
        /// <paramref name="intensity"/> (0…1, from the score) changes how much of the floor is moving, never
        /// the tune: a modest win gets the kick, the bass and the hook; a bigger one adds claps, hats, the
        /// sixteenth arpeggio, an octave doubling, the snare-roll build in front and the sparkle run over the
        /// held finish. The player hears what kind of win it was before the result screen says a word.
        /// </para>
        /// </summary>
        private static float[] BakeVictory(Random random, float intensity, FanfareShape shape)
        {
            //Key and tempo are the caller's now (RollFanfare), so whatever plays along with this piece knows
            //them the moment it is asked for rather than when it finishes rendering — see TryGetFanfare.
            float bpm = shape.Bpm;
            float secondsPerStep = 60f / (bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            //MAJOR, and back up where the supersaw shines. The old register lesson (down at C3, "deep before
            //timbre") was a TROMBONE'S lesson and stays true for the defeat below; the theme's own chorus
            //already proves the supersaw carries this register over a full mix.
            int root = shape.Root;

            //I–V–vi–IV as triads (semitone offsets from the root), then the held close. A build bar stands in
            //front once the win is worth announcing.
            int[][] chords =
            {
                new[] { 0, 4, 7 },      //I
                new[] { 7, 11, 14 },    //V
                new[] { 9, 12, 16 },    //vi — the one minor bar, which is what makes the IV lift after it
                new[] { 5, 9, 12 }      //IV
            };

            //WHICH of them the shortened drop plays, and it is not simply the first three (#185). Taking bars
            //0-1-2 would give I–V–vi and drop the IV entirely — losing the one chord the vi exists to set up,
            //which is the lift the whole progression is written for. I–vi–IV keeps the shape that matters:
            //home, the minor turn, and the lift out of it onto the close.
            int[] dropChords = { 0, 2, 3 };

            //TIGHTENED IN #185, which reported the piece as forced rather than punchy — and the report was
            //about LENGTH and DENSITY, not about the eurodance identity, so the drop stays and everything
            //around it is cut back. Three bars rather than four, and the extra build bar reserved for a
            //genuinely big win instead of any score over a third of the reference: at 0.35 almost every
            //non-trivial clear got it, which is how an ordinary win came to run for nearly thirteen seconds.
            //
            //Worth reading against the class doc above: the version BEFORE this one was reported as "old,
            //cheap, nothing to dance to". The two complaints bound the target between bare and busy rather
            //than cancelling out, so this trims the drop without going back to a herald's call.
            int buildBars = intensity > 0.75f ? 1 : 0;
            const int DROP_BARS = 3;
            int bars = buildBars + DROP_BARS + 1;   //build, the drop, the held close

            //Plus room for the close to RING OUT — without the tail the buffer ends mid-sustain and the
            //fanfare finishes on a click, which is a poor way to be told you won.
            float[] mix = NewMix(samplesPerStep * (bars * STEPS_PER_BAR + VICTORY_TAIL_STEPS));

            //THE HOOK's rhythm: two tresillos per bar — hits on 0,3,6 and 8,11,14, the 3-3-2 clave that is
            //the most danceable eight counts there are. The melody walks the chord's own tones, top-heavy,
            //and pushes UP onto the octave at each bar's end; two contours rolled so wins differ.
            int[] hookSteps = { 0, 3, 6, 8, 11, 14 };
            int[][] contours =
            {
                new[] { 2, 1, 2, 1, 2, 3 },   //fifth-third bounce, octave push
                new[] { 0, 2, 1, 2, 3, 3 }    //root up through the chord, octave held twice
            };
            int[] contour = contours[random.Next(contours.Length)];

            for (int bar = 0; bar < bars; bar++)
            {
                int at = bar * STEPS_PER_BAR * samplesPerStep;
                bool isBuild = bar < buildBars;
                bool isClose = bar == bars - 1;
                int dropBar = bar - buildBars;

                if (isBuild)
                {
                    //THE BUILD: the theme's own snare roll doubling to thirty-seconds, a sixteenth arpeggio
                    //ladder climbing two octaves of the tonic, and the kick already walking underneath — one
                    //bar that says "here it comes", which is half of why a drop lands.
                    for (int step = 0; step < STEPS_PER_BAR; step++)
                    {
                        float through = step / (float)STEPS_PER_BAR;
                        bool hit = through < 0.75f ? step % 2 == 0 : true;
                        if (hit) Snare(mix, at + step * samplesPerStep, 0.15f + 0.5f * through * through);

                        Arp(mix, at + step * samplesPerStep, root + chords[0][step % 3] + 12 * (step / 6),
                            secondsPerStep * 1.2f, 0.10f + 0.08f * through,
                            pan: (step % 2 == 0) ? -PAN_ARP : PAN_ARP);

                        if (step % 4 == 0) Kick(mix, at + step * samplesPerStep, 0.5f + 0.3f * through);
                    }

                    continue;
                }

                int[] chord = isClose ? chords[0] : chords[dropChords[dropBar]];
                int chordRoot = root + chord[0];

                //THE PAD holds the chord under everything, sub root beneath it for the weight the kick rides.
                //Shorter than it was and struck rather than swelled (#185): at 15.5/22 steps it was still
                //ringing a bar and a half after the hook had finished, which is most of what read as the
                //fanfare outstaying itself, and a 0.35 s swell under a drop arrives after the beat it is
                //supposed to land on.
                const float PadAttack = 0.12f;

                for (int voice = 0; voice < chord.Length; voice++)
                    Pad(mix, at, root + chord[voice], secondsPerStep * (isClose ? 14f : 10f),
                        0.11f + 0.05f * intensity, pan: ChordPan(voice, chord.Length, PAN_PAD_SPREAD),
                        attack: PadAttack);

                //The sub root stays centre: it is the weight the kick rides, not a voice of the chord
                Pad(mix, at, chordRoot - 12, secondsPerStep * (isClose ? 14f : 10f), 0.10f, attack: PadAttack);

                if (isClose)
                {
                    //THE CLOSE: the tonic landed and HELD — the lead on the octave (a fifth higher again for
                    //a big win), the last kick on the downbeat, and the sparkle run climbing away over it.
                    int top = root + 12 + (intensity > 0.75f ? 19 : 12);
                    Lead(mix, at, top, secondsPerStep * 9f, 0.30f + 0.12f * intensity);
                    Lead(mix, at, root + 12, secondsPerStep * 9f, 0.22f);

                    Kick(mix, at, 0.8f + 0.2f * intensity);

                    //The sparkle run is the last thing to arrive and the first to be cut: eight steps rather
                    //than twelve, and only for a genuinely big win.
                    if (intensity > 0.75f)
                        for (int i = 0; i < 8; i++)
                            Arp(mix, at + i * samplesPerStep, root + 12 + chord[i % 3] + 12 * (i / 4),
                                secondsPerStep * 1.4f, 0.12f * intensity,
                                pan: (i % 2 == 0) ? -PAN_ARP : PAN_ARP);

                    continue;
                }

                //THE DROP. Four on the floor — ALWAYS, whatever the score: the beat is the celebration now,
                //not a garnish a modest win goes without.
                for (int beat = 0; beat < 4; beat++)
                    Kick(mix, at + beat * 4 * samplesPerStep, 0.75f + 0.25f * intensity);

                //The off-beat bass pump, the theme's own figure: between the kicks, never on them.
                for (int beat = 0; beat < 4; beat++)
                    Bass(mix, at + (beat * 4 + 2) * samplesPerStep, chordRoot - 12, secondsPerStep * 1.7f,
                        0.8f + 0.2f * intensity);

                //Claps on two and four, hats on the eighths with the off-beats open, the sixteenth arpeggio
                //running through — each arriving as the win grows, so the floor fills with the score.
                //
                //SPACED FURTHER APART in #185. At 0.2/0.35/0.45/0.5 all four of these stacked on top of the
                //kick, the bass and the pad by an intensity of about a half — a BELOW-average score — so seven
                //voices at once was the ordinary case rather than the exceptional one. Spread over 0.3 to 0.8,
                //an ordinary win now gets the floor and the hook and a big one gets the lot.
                if (intensity > 0.3f)
                {
                    Clap(mix, at + 4 * samplesPerStep, 0.5f + 0.3f * intensity);
                    Clap(mix, at + 12 * samplesPerStep, 0.5f + 0.3f * intensity);
                }

                if (intensity > 0.5f)
                    for (int step = 0; step < STEPS_PER_BAR; step += 2)
                        Hat(mix, at + step * samplesPerStep, open: step % 4 == 2, level: 0.24f);

                if (intensity > 0.7f)
                    for (int step = 0; step < STEPS_PER_BAR; step += 1)
                        if (step % 2 == 1)
                            Arp(mix, at + step * samplesPerStep, root + chord[step % 3] + 12,
                                secondsPerStep * 0.9f, 0.09f,
                                pan: (step % 4 == 1) ? -PAN_ARP : PAN_ARP);

                //THE HOOK, on the theme's own supersaw. Notes run a step past the next hit's start (the
                //legato arithmetic), and the whole line doubles an octave up once the win is worth it.
                float leadLevel = 0.26f + 0.12f * intensity;

                for (int h = 0; h < hookSteps.Length; h++)
                {
                    int gap = (h + 1 < hookSteps.Length ? hookSteps[h + 1] : STEPS_PER_BAR) - hookSteps[h];
                    int note = root + 12 + (contour[h] < 3 ? chord[contour[h]] : chord[0] + 12);

                    Lead(mix, at + hookSteps[h] * samplesPerStep, note, secondsPerStep * (gap + 1f), leadLevel);

                    if (intensity > 0.8f)
                        Lead(mix, at + hookSteps[h] * samplesPerStep, note + 12, secondsPerStep * (gap + 1f), leadLevel * 0.4f);
                }

                //A tom run into the close when the win was a big one.
                if (intensity > 0.8f && dropBar == DROP_BARS - 1)
                    for (int i = 0; i < 4; i++)
                        Tom(mix, at + (12 + i) * samplesPerStep, 120f + i * 22f, 0.8f);
            }

            Limit(mix, targetRms: 0.17f + 0.06f * intensity, ceiling: 0.95f);
            return mix;
        }

        /// <summary>
        /// The defeat fanfare: slow, minor and falling, and the exact inverse of the victory one in every
        /// dimension that matters — mode, direction, tempo and register.
        /// <para>
        /// It takes the same <paramref name="intensity"/> from the other end. A good score that still lost gets
        /// a fuller piece — a fourth bar, a harmony under the melody and a resolution at the bottom; a poor one
        /// gets a couple of thin notes over three bars and no resolution at all. Losing narrowly and losing
        /// badly should not sound the same, and the difference is what the player is owed for the run they had.
        /// </para>
        /// <para>
        /// Since #190 that difference is the piece's <b>length</b> as well as its density: it ran four bars
        /// whatever had happened, which at this tempo measured 17.5 s over a screen that had just said the
        /// player lost. See the bar count for why the time can only come out of the bars.
        /// </para>
        /// </summary>
        private static float[] BakeDefeat(Random random, float intensity, FanfareShape shape)
        {
            //Key and tempo are the caller's — see BakeVictory and TryGetFanfare.
            float bpm = shape.Bpm;
            float secondsPerStep = 60f / (bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int root = shape.Root;

            //THE LENGTH IS THE INTENSITY'S NOW, not a constant (#190). It ran four bars at 62-74 BPM whatever
            //the run had been — a measured 17.5 s over a screen that has just told the player they lost, and
            //nearly twice the victory's own length once #185 had shortened that. Only the bars can pay for it:
            //the tempo is the piece's whole character (it has to feel like it is running out) and the tail is
            //what stops it ending on a click. So three bars for a loss, and the fourth only where the run was
            //close enough to have earned the fuller piece — the SAME threshold that puts the harmony under the
            //melody, so the piece grows in one step rather than by two figures that could drift apart.
            int bars = intensity > DEFEAT_FULLER ? 4 : 3;
            float[] mix = NewMix(samplesPerStep * (bars * STEPS_PER_BAR + DEFEAT_TAIL_STEPS));

            //i - VI - iv - i: minor, and it sags rather than resolving anywhere bright.
            int[] degrees = { 0, 8, 5, 0 };

            //The melody falls. Two shapes, both descending, because a rising line under a loss reads as hope
            //and this is not that.
            int[][] shapes =
            {
                new[] { 3, 2, 1, 0 },   //octave down to the root
                new[] { 2, 1, 1, 0 }    //fifth, third, third, root — a smaller, more resigned fall
            };
            int[] fall = shapes[random.Next(shapes.Length)];   //renamed off "shape": the out parameter owns that name now

            for (int bar = 0; bar < bars; bar++)
            {
                //WHICH bar goes when there are three: the third, its chord and its melody note together. The iv
                //is a step on the way home, where the VI before it is the sag the whole piece is written round
                //and the i after it is the landing — so a three-bar i - VI - i still sags and still lands, and
                //the melody still falls (3-2-0 or 2-1-0 against the four-bar 3-2-1-0). Dropping the VI instead
                //would leave a plain plagal cadence with none of the piece's colour. #185 settled the same
                //question for the victory's I-V-vi-IV from the other end and kept the chord the progression
                //exists for; this is that rule applied to a falling one. The four-bar arrays above stay the
                //canonical statement of the progression rather than being re-authored per length.
                int source = bars == 4 || bar < 2 ? bar : bar + 1;

                int chordRoot = root + degrees[source];
                bool last = bar == bars - 1;
                int at = bar * STEPS_PER_BAR * samplesPerStep;

                //The pad carries almost the whole piece. Thin when the run was poor, full when it was close.
                for (int voice = 0; voice < MINOR_TRIAD.Length; voice++)
                    Pad(mix, at, chordRoot - 12 + MINOR_TRIAD[voice], secondsPerStep * (last ? 26f : 16.5f),
                        0.10f + 0.08f * intensity, pan: ChordPan(voice, MINOR_TRIAD.Length, PAN_PAD_SPREAD));

                //One long melody note a bar, falling. No percussion anywhere: a beat would give it momentum,
                //and momentum is the one thing this must not have.
                float leadLevel = 0.22f + 0.10f * intensity;

                //The last note is only played if there is something to resolve to — a poor run ends on the
                //third and simply stops, which leaves it hanging. That unresolved ending is the bleakest thing
                //in the piece and it costs one condition.
                //
                //Seventeen steps against a sixteen-step bar: each note runs a step past the next one's start,
                //so the trombone releases across the join and the line is legato rather than a row of separate
                //sighs. It matters more here than in the victory, because a slow piece leaves bigger holes.
                //The melody stays CENTRE, the lead's own rule: it is the tune. It was seated to one side of a
                //symmetric pair first, and that is a trap this arrangement sets — the harmony below is
                //conditional, so on any run under the threshold the melody sang alone from one speaker and the
                //whole piece leaned 1.4 dB (measured). A part that may play alone cannot take half a pair's seat.
                if (!last || intensity > 0.35f)
                    Brass(mix, at, chordRoot + MINOR_TRIAD[fall[source]], secondsPerStep * (last ? 24f : 17f),
                        leadLevel);

                //A harmony a third under the melody, for a run that deserved better. This one is the part that
                //moves off centre — it only ever sounds WITH the melody, so it reads as a second player beside
                //the first rather than as the tune wandering, and at 0.55 of the level it barely tilts the piece.
                if (intensity > DEFEAT_FULLER)
                    Brass(mix, at, chordRoot + MINOR_TRIAD[fall[source]] - 3, secondsPerStep * (last ? 24f : 17f),
                        leadLevel * 0.55f, pan: PAN_BRASS_SPREAD);
            }

            Limit(mix, targetRms: 0.10f + 0.05f * intensity, ceiling: 0.9f);
            return mix;
        }

        #endregion

        #region The stereo image (#119)

        //Every piece here was baked dead centre: one mono buffer every voice summed into, so a two-minute
        //arrangement of a dozen instruments played back as a single point between the speakers. The buffer is
        //INTERLEAVED STEREO now — frame n is mix[2n] left, mix[2n + 1] right — and each instrument has a seat.
        //
        //Two rules decide the seating, and the second is the one that is easy to get wrong.
        //
        //LOW STAYS CENTRE. The kick, the bass and the timpani are panned nowhere at all. Below roughly 200 Hz
        //the ear takes almost no direction from level difference (the wavelength dwarfs the head), so panning
        //them buys no width — and it costs: a hard-panned low end loses half its power the moment anything
        //downmixes to mono, which is every laptop and phone speaker this game will ever be played on. The
        //snare sits with them because the backbeat is what the kick is answered by, and a centre it wanders
        //from reads as the mix drifting. Per docs/game-feedback.md, 61-70 % of the energy of these pieces is
        //below 200 Hz, so this rule is most of the mix by energy and none of it by width.
        //
        //WIDTH COMES FROM WHAT IS ALREADY DETUNED. The lead is two saws twelve cents apart and the string
        //section is seven; those were written to beat against each other, and beating voices pulled APART is
        //the oldest width there is — far wider than moving a finished mono note off centre, because the two
        //sides are genuinely different signals rather than the same one at two levels. So those two spread
        //internally (their detuned voices take their own seats) while the NOTE stays where it was.
        //
        //Nothing here is randomised per pass. A pass rolls its tempo, key and progression (see "Random
        //parameters, never random notes"); where the hi-hat sits is not a composition decision, and an image
        //that moved between passes would read as the mix being unstable rather than as variation.

        /// <summary>How far off centre a thing sits: -1 hard left, 0 centre, +1 hard right.</summary>
        private const float PAN_CENTRE = 0f;

        //The kit, seated as a kit is: hats to one side, the ride answering them from the other. Modest —
        //hard-panned cymbals are a 1960s artifact and read as a fault on headphones.
        private const float PAN_HAT = 0.34f;
        private const float PAN_RIDE = -0.30f;

        //The arp ping-pongs between these on alternate sixteenths, which is the one width effect this genre
        //is actually built on. It never stops running (see "The parts are arranged around the kick"), so it
        //is also what keeps the image alive through the melody's rests.
        private const float PAN_ARP = 0.62f;

        //Left of the middle, opposite the hats — but not far. In the lobby piece the keys ARE the line, and a
        //part carrying a piece on its own tilts the whole thing as far as it is moved: at -0.38 the loop
        //measured 0.47 dB left overall. Enough to separate them from the hats, little enough that the piece
        //still sits in the middle.
        private const float PAN_KEYS = -0.24f;

        //The seated sections: a spread applied ACROSS THE NOTES of a chord rather than to the section as a
        //whole, so a held chord occupies the field the way players on a stage do instead of arriving as one
        //wide blur. PitchPan turns a note into its seat.
        private const float PAN_PAD_SPREAD = 0.55f;
        private const float PAN_BRASS_SPREAD = 0.40f;

        /// <summary>How far apart the string section's seven detuned voices are seated — the widest thing here.</summary>
        private const float PAN_STRINGS_SPREAD = 0.85f;

        /// <summary>How far apart the supersaw's two detuned saws sit. The note itself stays centred.</summary>
        private const float PAN_LEAD_SPREAD = 0.55f;

        /// <summary>
        /// How far apart the piano's two beating strings sit — barely, by the standard of this file. The note
        /// stays centred (#119: width comes from what is already detuned); the strings of one unison are
        /// centimetres apart on the frame, not metres in the room, and a piano is one point on the stage.
        /// </summary>
        private const float PAN_PIANO_SPREAD = 0.16f;

        /// <summary>The clap's three bursts land across the field — a room full of hands is not one point.</summary>
        private const float PAN_CLAP_SPREAD = 0.45f;

        /// <summary>A tom fill travels across the kit as it descends, which is what a fill on real toms does.</summary>
        private const float PAN_TOM_SPREAD = 0.5f;

        /// <summary>
        /// A pan (-1…+1) as the pair of gains it becomes. <b>Constant power</b> (the sine/cosine law, not a
        /// linear crossfade): the two gains square to 1 rather than summing to it, so a part panned off centre
        /// keeps the loudness it had in the middle. A linear law dips about 3 dB at the extremes, which on the
        /// ping-ponging arp would read as the part getting quieter every other sixteenth.
        /// <para>
        /// Called once per note, never per sample — the gains are constant for the whole of a note.
        /// </para>
        /// </summary>
        private static void PanGains(float pan, out float gainLeft, out float gainRight)
        {
            float angle = (MathHelper.Clamp(pan, -1f, 1f) * 0.5f + 0.5f) * MathF.PI * 0.5f;

            gainLeft = MathF.Cos(angle);
            gainRight = MathF.Sin(angle);
        }

        /// <summary>
        /// Seats voice <paramref name="voice"/> of a <paramref name="voices"/>-note chord, evenly and
        /// symmetrically across <paramref name="spread"/>: the bottom note to one side, the top to the other,
        /// a lone note dead centre.
        /// <para>
        /// <b>It is the voice's INDEX and deliberately not its pitch.</b> Seating by pitch was tried first and
        /// is a trap: every pass rolls its own key (see "Random parameters, never random notes"), so a seat
        /// derived from the note number rotates with the transpose, and the same chord shape lands in a
        /// different place in every pass — an image that moves between passes, which reads as the mix being
        /// unstable rather than as variation. It also leaves the whole piece leaning to whichever side the
        /// key happened to put the chord tones on. The index carries neither problem: it is a property of the
        /// arrangement, which is the same in every pass.
        /// </para>
        /// </summary>
        private static float ChordPan(int voice, int voices, float spread) =>
            voices <= 1 ? PAN_CENTRE : ((voice / (float)(voices - 1)) * 2f - 1f) * spread;

        /// <summary>How many stereo frames the mix holds — its length is two floats per frame.</summary>
        private static int Frames(float[] mix) => mix.Length / 2;

        /// <summary>A mix buffer for <paramref name="frames"/> stereo frames.</summary>
        private static float[] NewMix(int frames) => new float[frames * 2];

        /// <summary>
        /// Adds one sample to one frame of the interleaved mix, at a gain pair from <see cref="PanGains"/>.
        /// <b>This is the one place the interleaving is stated</b> — every instrument writes through it, so
        /// none of them has to know the layout, and a voice that grew a second channel of its own (the lead,
        /// the strings) simply calls it twice.
        /// </summary>
        private static void Add(float[] mix, int frame, float value, float gainLeft, float gainRight)
        {
            int offset = frame * 2;

            mix[offset] += value * gainLeft;
            mix[offset + 1] += value * gainRight;
        }

        #endregion

        #region The instruments

        /// <summary>
        /// The kick: a sine whose pitch collapses from 150 Hz to 45 in forty milliseconds, plus a click on the
        /// first samples. The pitch envelope IS the kick — a fixed low sine is a hum, and the drop is what the
        /// ear reads as something being struck.
        /// </summary>
        private static void Kick(float[] mix, int at, float level)
        {
            int length = (int)(SAMPLE_RATE * 0.28f);
            int frames = Frames(mix);
            float phase = 0f;

            //Dead centre, and the clearest case of the rule: this is the lowest thing in the piece.
            PanGains(PAN_CENTRE, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float freq = 45f + 105f * MathF.Exp(-t * 26f);
                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                float body = MathF.Sin(phase) * MathF.Exp(-t * 7.5f);
                float click = (i < 90) ? Noise(i, 11) * 0.5f * (1f - i / 90f) : 0f;

                Add(mix, at + i, (body * 0.95f + click) * 0.92f * level, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The clap: three noise bursts a few milliseconds apart and then a longer tail. The stutter is what
        /// makes it a clap rather than a snare — a room full of hands does not land on one sample.
        /// </summary>
        private static void Clap(float[] mix, int at, float level)
        {
            int[] offsets = { 0, (int)(SAMPLE_RATE * 0.009f), (int)(SAMPLE_RATE * 0.018f) };

            //The three bursts land in three PLACES as well as at three times, which is the same observation
            //the stutter itself came from: a room full of hands is not one point either in time or in space.
            float[] pans = { -PAN_CLAP_SPREAD, PAN_CLAP_SPREAD, -PAN_CLAP_SPREAD * 0.4f };

            int frames = Frames(mix);

            for (int burst = 0; burst < offsets.Length; burst++)
            {
                int offset = offsets[burst];
                PanGains(pans[burst], out float burstLeft, out float burstRight);

                for (int i = 0; i < SAMPLE_RATE * 0.03f && at + offset + i < frames; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    Add(mix, at + offset + i, BandNoise(i, 1400f, 5200f, 23) * 0.5f * level * MathF.Exp(-t * 90f),
                        burstLeft, burstRight);
                }
            }

            //The tail is the room the three landed in, so it sits in the middle of them rather than to a side
            PanGains(PAN_CENTRE, out float tailLeft, out float tailRight);

            int tail = (int)(SAMPLE_RATE * 0.22f);
            for (int i = 0; i < tail && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                Add(mix, at + i, BandNoise(i, 1100f, 4200f, 29) * 0.26f * level * MathF.Exp(-t * 22f),
                    tailLeft, tailRight);
            }
        }

        /// <summary>The hat: high noise, very short closed and a good deal longer open.</summary>
        /// <param name="pan">
        /// Where on the kit it is. Defaults to the hats' own seat; the ride passes <see cref="PAN_RIDE"/>, so
        /// the two answer each other across the field instead of both arriving from the same spot.
        /// </param>
        private static void Hat(float[] mix, int at, bool open, float level, float pan = PAN_HAT)
        {
            float decay = open ? 26f : 95f;
            int length = (int)(SAMPLE_RATE * (open ? 0.16f : 0.05f));
            int frames = Frames(mix);

            PanGains(pan, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                Add(mix, at + i, BandNoise(i, 6000f, 15000f, 37) * level * MathF.Exp(-t * decay),
                    gainLeft, gainRight);
            }
        }

        /// <summary>A tom, for the fills: the kick's pitch envelope over a shorter, higher, more tuned body.</summary>
        private static void Tom(float[] mix, int at, float startHz, float level)
        {
            int length = (int)(SAMPLE_RATE * 0.2f);
            int frames = Frames(mix);
            float phase = 0f;

            //Seated by its PITCH, so the descending fill the arrangement writes (138 Hz down in steps of 16)
            //travels across the kit as it falls — which is what a fill on real toms does, and it costs the
            //caller nothing to say. The band here spans roughly 60-140 Hz of start pitch.
            PanGains(MathHelper.Clamp((startHz - 100f) / 60f, -1f, 1f) * PAN_TOM_SPREAD,
                out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float freq = startHz * 0.55f + startHz * 0.45f * MathF.Exp(-t * 14f);
                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                Add(mix, at + i, (MathF.Sin(phase) * 0.75f + BandNoise(i, 300f, 2400f, 53) * 0.2f)
                    * 0.55f * level * MathF.Exp(-t * 11f), gainLeft, gainRight);
            }
        }

        /// <summary>The snare, for the build's roll: a tone at 190 Hz under a wide band of noise.</summary>
        private static void Snare(float[] mix, int at, float level)
        {
            int length = (int)(SAMPLE_RATE * 0.13f);
            int frames = Frames(mix);

            //With the kick: the backbeat is what the kick is answered by, and a centre it wanders from reads
            //as the whole mix drifting rather than as width.
            PanGains(PAN_CENTRE, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float body = MathF.Sin(2f * MathF.PI * 190f * t) * 0.35f;

                Add(mix, at + i, (body + BandNoise(i, 1200f, 8000f, 61) * 0.8f) * level * MathF.Exp(-t * 34f),
                    gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The crash (#163): the cymbal a chorus arrives on, and it is <see cref="Hat"/>'s own material with a
        /// hundred times the ring — the difference between the two voices is the envelope and the band, not
        /// the source. Two envelopes over one another: the strike, gone in a tenth of a second, and the wash
        /// under it, which is still sounding a second and a half later. A short crash is an open hat, and a
        /// crash without the low body under it is a hiss rather than a struck plate.
        /// </summary>
        private static void Crash(float[] mix, int at, float level, float pan = PAN_CRASH)
        {
            int length = (int)(SAMPLE_RATE * 1.6f);
            int frames = Frames(mix);
            float body = 0f;

            PanGains(pan, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //The plate: the strike on top of the wash, both of the same noise.
                float top = BandNoise(i, 3000f, 15000f, 137) * (0.55f * MathF.Exp(-t * 24f) + 0.45f * MathF.Exp(-t * 1.9f));

                //The bell under it, one pole rather than a second band — the ear is judging that there IS
                //something low here, not where its skirts are.
                body += CutoffToAlpha(1400f) * (Noise(i, 139) - body);

                Add(mix, at + i, (top + body * 0.5f * MathF.Exp(-t * 3.4f)) * level, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The bass: a saw through a one-pole low-pass that opens with the note's own envelope — the cheapest
        /// thing that sounds like a filter sweep, and most of what a dance bass is.
        /// </summary>
        private static void Bass(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);
            float phase = 0f, lp = 0f;

            //Centre with the kick, and for the mono-collapse reason above rather than for taste: this is the
            //part that has to survive a phone speaker intact.
            PanGains(PAN_CENTRE, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Min(1f, t / 0.004f) * MathF.Exp(-t * 6.5f);

                phase += freq / SAMPLE_RATE;
                if (phase >= 1f) phase -= 1f;

                float saw = PolyBlepSaw(phase, freq / SAMPLE_RATE);

                lp += CutoffToAlpha(190f + 2600f * env) * (saw - lp);
                Add(mix, at + i, lp * 0.55f * level * env, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The floor (#186): a sustained low note, and the answer to "there is no real bass in this piece".
        /// <see cref="Bass"/> is not one — it is a <b>pluck</b>, an exponential gone in about 150 ms, so what
        /// it gives is a pump and not a bottom. This holds a whole bar.
        /// <list type="bullet">
        /// <item><b>A sine with a short harmonic ladder over it</b> (a second at 0.24, a third at 0.08), and
        /// the ladder is not colour — it is audibility. The fundamental here runs 37–82 Hz, which a laptop or
        /// a phone speaker simply does not reproduce; the harmonics do, and the ear reconstructs the missing
        /// fundamental from them. A pure sine is the "correct" sub and is silent on most of the hardware this
        /// will be played on.</item>
        /// <item><b>It ducks to the beat.</b> A sustained note in the kick's own octave meets it at whatever
        /// phase it happens to and either doubles or cancels — the classic way a mix loses its low end while
        /// every part is individually right. Ducking is the genre's own answer and it is also the pump the ear
        /// expects, so the fix and the effect are the same line. <paramref name="duck"/> is 0 wherever no kick
        /// is playing, and then this holds dead flat, which is what gives the drumless sections a bottom.</item>
        /// <item><b>It is centred</b>, by the low-end rule the stereo region states, and more plainly than
        /// anything else here: it is the lowest sustained thing in the piece.</item>
        /// </list>
        /// </summary>
        /// <param name="duck">How far it drops on each beat, 0 for not at all.</param>
        /// <param name="beatSeconds">The beat it ducks to; ignored when <paramref name="duck"/> is 0.</param>
        private static void SubBass(float[] mix, int at, int note, float seconds, float level,
            float duck, float beatSeconds)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);
            float phase = 0f;

            PanGains(PAN_CENTRE, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Long enough in not to click at these frequencies (a 20 ms ramp is under a cycle and a half at
                //55 Hz), flat through the middle, and out over a tenth of a second so one chord's floor reaches
                //the next one's rather than leaving a hole on every bar line.
                float env = MathF.Min(1f, t / 0.02f) * MathF.Min(1f, (seconds - t) / 0.1f);
                if (env <= 0f) continue;

                if (duck > 0f && beatSeconds > 0f)
                {
                    float sinceBeat = t - MathF.Floor(t / beatSeconds) * beatSeconds;
                    env *= 1f - duck * MathF.Exp(-sinceBeat * 9f);
                }

                phase += 2f * MathF.PI * freq / SAMPLE_RATE;
                if (phase >= MathF.Tau) phase -= MathF.Tau;   //wrapped whole, so the harmonics below stay continuous

                float body = MathF.Sin(phase) + 0.24f * MathF.Sin(phase * 2f) + 0.08f * MathF.Sin(phase * 3f);

                Add(mix, at + i, body * level * env, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The log drum (#264): the bass that carries <see cref="MusicTheme.Mural"/>'s tune, and the voice
        /// the bass-first pop of the 2020s is named by. It is a recombination of three things this file
        /// already proved — which is why it comes out as the real article rather than an imitation, because
        /// the real article is exactly this synthesis:
        /// <list type="bullet">
        /// <item><b>A pitch that drops and SETTLES.</b> The strike starts 2.4× sharp and lands on the note
        /// within ~45 ms — the <see cref="Timpani"/>'s own distinction: a sweep that keeps falling is a tom,
        /// one that settles is tuned. The settle is what separates this voice from <see cref="Kick"/> by
        /// construction: a log drum is a bass NOTE with a knock in front, not a drum at a wrong pitch.</item>
        /// <item><b><see cref="SubBass"/>'s harmonic ladder, one rung longer</b> (0.28 / 0.12 / 0.04), then a
        /// gentle normalised waveshaper (<see cref="Guitar"/>'s tanh at a fraction of its drive). The
        /// fundamentals run 41–98 Hz, which a phone reproduces not at all; the ladder and the shaper's
        /// products are the audibility, not the colour — and because the TUNE itself lives down here, this
        /// voice depends on that lesson harder than SubBass ever did.</item>
        /// <item><b>A knock across the attack</b> — a few milliseconds of low band noise — so the rhythm
        /// still arrives on speakers that lose every trace of the pitch.</item>
        /// <item><b>It can SLIDE.</b> Given <paramref name="glideFrom"/> the pitch approaches from that note
        /// instead of knocking — the walking bass's approach note as a portamento. The caller keeps it rare,
        /// small and off the downbeats, or the slide becomes a cartoon.</item>
        /// <item><b>It is centred</b>, unconditionally: the lowest and the loudest sustained thing in its
        /// piece, which is the low-stays-centre rule twice over.</item>
        /// </list>
        /// </summary>
        /// <param name="glideFrom">MIDI note the pitch slides in from, or -1 for the struck knock.</param>
        private static void LogDrum(float[] mix, int at, int note, float seconds, float level, int glideFrom = -1)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);

            //The strike, or the slide: an exponential settle onto the note either way — sharp and fast for
            //the knock, from the approach note and slower for the glide.
            float startRatio = glideFrom >= 0 ? Frequency(glideFrom) / freq : 2.4f;
            float settle = glideFrom >= 0 ? 34f : 80f;

            //The drive and its normalisation belong together: the ladder below peaks at 1.44, and
            //tanh(1.44 × 1.25) over tanh(1.8) puts the driven peak back at one (Guitar's own arithmetic).
            float normalise = 1f / MathF.Tanh(1.8f);

            float phase = 0f, lp = 0f;

            PanGains(PAN_CENTRE, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //~20 ms in — long enough that where a kick shares the step, the kick's transient owns the
                //strike and this is its body, the genre's own layering — then a sung decay, out over 60 ms.
                //Skipped, never stopped: the ramp is zero on the first samples (#218's lesson).
                float env = MathF.Min(1f, t / 0.02f) * MathF.Exp(-t * 4.5f) * MathF.Min(1f, (seconds - t) / 0.06f);
                if (env <= 0.0005f) continue;

                float f = freq * (1f + (startRatio - 1f) * MathF.Exp(-t * settle));
                phase += 2f * MathF.PI * f / SAMPLE_RATE;
                if (phase >= MathF.Tau) phase -= MathF.Tau;   //wrapped whole, so the ladder stays continuous

                //The ladder rides the one wrapped accumulator, so the harmonics track the settle exactly.
                float body = MathF.Sin(phase) + 0.28f * MathF.Sin(phase * 2f)
                    + 0.12f * MathF.Sin(phase * 3f) + 0.04f * MathF.Sin(phase * 4f);

                //Driven gently and normalised (spectrum, not level), then rounded by one pole so the shaper's
                //top end stays rubber rather than buzz.
                float driven = MathF.Tanh(body * 1.25f) * normalise;
                lp += CutoffToAlpha(700f) * (driven - lp);

                float knock = t < 0.008f ? BandNoise(i, 200f, 1500f, 173) * 0.4f * (1f - t / 0.008f) : 0f;

                Add(mix, at + i, (lp * 0.8f + knock) * level * env, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The marimba (#264): <see cref="MusicTheme.Mural"/>'s answering voice — a struck BAR, and the
        /// acoustics is the recipe:
        /// <list type="bullet">
        /// <item><b>The bar's tuned second mode sits near TWO octaves up</b> — 4.02× the fundamental, not the
        /// string's 2× or the tine's bright 3.93×: a marimba bar is deliberately carved until that mode lands
        /// there, and it is the interval the ear names the instrument by. Slightly off the exact double
        /// octave, for the reason the Keys' tine is off its series: dead in tune reads as an organ stop. It
        /// dies several times faster than the fundamental, so the note goes from woody to round as it rings —
        /// and being near-harmonic it stays WARM where a 5–6× partial goes metallic, which is the ringtone
        /// trap (#210's glockenspiel, one register down).</item>
        /// <item><b>A mallet, not a hammer:</b> a few milliseconds of mid-band thump, and a filter that
        /// closes as the note dies (<see cref="UprightBass"/>'s trick — a struck bar dulls, it does not
        /// switch off).</item>
        /// <item><b>Two strikes a few cents apart</b>, seated just off either side of the note's own seat —
        /// the piano's two-string idiom: the width lives inside the note and the note stays put.</item>
        /// </list>
        /// </summary>
        private static void Marimba(float[] mix, int at, int note, float seconds, float level, float pan = PAN_CENTRE)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);

            float[] detune = { 0.9985f, 1.0015f };
            float[] phases = { 0f, 0.41f };

            float[] gainLeft = new float[2];
            float[] gainRight = new float[2];
            for (int s = 0; s < 2; s++)
                PanGains(pan + (s == 0 ? -PAN_MARIMBA_SPREAD : PAN_MARIMBA_SPREAD), out gainLeft[s], out gainRight[s]);

            float[] lp = { 0f, 0f };

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Struck at once, rings about half a second, released before the buffer runs out. Skipped,
                //never stopped: the attack ramp is zero on the first samples (#218's lesson).
                float env = MathF.Min(1f, t / 0.0015f) * MathF.Exp(-t * 5.5f) * MathF.Min(1f, (seconds - t) / 0.05f);
                if (env <= 0.0006f) continue;

                //The mallet: one thump, both strikes.
                float mallet = t < 0.005f ? BandNoise(i, 400f, 3000f, 167) * 0.3f * (1f - t / 0.005f) : 0f;

                //Closing as it dies — bright at the strike, round in the tail.
                float alpha = CutoffToAlpha(600f + 5200f * env);

                for (int s = 0; s < 2; s++)
                {
                    float f = freq * detune[s];

                    float value = MathF.Sin(2f * MathF.PI * f * t + phases[s])
                        + 0.4f * MathF.Sin(2f * MathF.PI * f * 4.02f * t + phases[s]) * MathF.Exp(-t * 28f);

                    lp[s] += alpha * (value * 0.6f + mallet - lp[s]);

                    Add(mix, at + i, lp[s] * env * level, gainLeft[s], gainRight[s]);
                }
            }
        }

        /// <summary>
        /// The upright bass (#162): the one voice Nocturne had to add, and the reason is the attack rather
        /// than the pitch. <see cref="Bass"/> is a saw held open through a filter that sweeps with the note —
        /// a dance bass, and what it does is <i>sustain</i>. A double bass is <b>plucked</b>: the string is
        /// pulled and released, so almost all of the sound is in the first fifty milliseconds and what follows
        /// is a woody body dying away. Four things make it one:
        /// <list type="bullet">
        /// <item><b>A triangle, not a saw.</b> A gut-and-wood string is nearly all fundamental with a little
        /// odd harmonic over it; a saw has every harmonic and comes out as a synth however it is filtered.</item>
        /// <item><b>The pluck is a separate transient</b> — a few milliseconds of filtered noise, the finger
        /// leaving the string — because a bass with no finger noise reads as a sine and not as a player.</item>
        /// <item><b>The filter closes as the note decays</b> rather than opening: a plucked string loses its
        /// top end first, which is the opposite of the dance bass's sweep and is most of the difference.</item>
        /// <item><b>It is centred</b>, with the low-end rule the stereo region states.</item>
        /// </list>
        /// </summary>
        private static void UprightBass(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);
            float phase = 0f, lp = 0f;

            PanGains(PAN_CENTRE, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Struck at once and gone steadily: a plucked string has no attack to speak of and no sustain.
                float env = MathF.Min(1f, t / 0.006f) * MathF.Exp(-t * 3.2f);

                //Skipped, not stopped: an envelope with an attack ramp is zero BEFORE its peak, so a break
                //here ended the note on its first sample and the walk was silent from #162 until #218. The
                //loop is bounded by `seconds` anyway; the guard only zeroes inaudible samples.
                if (env <= 0.0005f) continue;

                phase += freq / SAMPLE_RATE;
                if (phase >= 1f) phase -= 1f;

                //Triangle from the phase: |2x-1| folded, which is a pure odd-harmonic shape with the harmonics
                //falling off as 1/n^2 - the string's own spectrum, near enough, and free of the saw's buzz.
                float triangle = 4f * MathF.Abs(phase - 0.5f) - 1f;

                //The body closes down as it dies. Starting near 900 Hz and settling towards 200 is what makes
                //it read as wood rather than as a filter sweep in the other direction.
                lp += CutoffToAlpha(200f + 700f * env) * (triangle - lp);

                float pluck = t < 0.012f ? Noise(i, 97) * 0.28f * (1f - t / 0.012f) : 0f;

                Add(mix, at + i, (lp * 0.9f + pluck) * level * env, gainLeft, gainRight);
            }
        }

        /// <summary>The arpeggio: a plain square, short and quiet. The DOS-era voice, and it is meant to sound like one.</summary>
        /// <param name="pan">
        /// Where this note sits. The arrangement ping-pongs it (see <see cref="PAN_ARP"/>) — the sixteenths
        /// never stop, so this is what keeps the image moving through the melody's rests.
        /// </param>
        private static void Arp(float[] mix, int at, int note, float seconds, float level, float pan = PAN_ARP)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);
            float phase = 0f;

            PanGains(pan, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Min(1f, t / 0.002f) * MathF.Exp(-t * 15f);

                phase += freq / SAMPLE_RATE;
                if (phase >= 1f) phase -= 1f;

                Add(mix, at + i, PolyBlepSquare(phase, freq / SAMPLE_RATE) * level * env, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The lobby's keys: an electric-piano voice for the menu piece's line. The theme's <see cref="Arp"/>
        /// is a bare square with a fast decay — a texture inside a full mix, but exposed at quarter notes over
        /// nothing but pads it read as a touch-tone phone, because a beep is what a bare square <i>is</i>.
        /// Four things make this a struck key instead: a fundamental-heavy body (sine plus a soft octave), a
        /// bright <b>inharmonic</b> "tine" partial that fades several times faster than the body — the attack
        /// is where the ear decides "piano", and its quick fade is what stops it beeping — a decay long enough
        /// to carry a slow quarter note instead of plinking into a hole, and a shallow slow tremolo so the
        /// ring stays alive on the way down.
        /// </summary>
        /// <param name="pan">
        /// Where it sits. Defaults to the keys' own seat — but Nocturne plays the comp AND the tune on this
        /// one voice, and leaving both on that seat leaned the whole piece 1.9 dB left (measured). A part that
        /// carries a piece on its own belongs in the middle; it is the accompanist that moves.
        /// </param>
        private static void Keys(float[] mix, int at, int note, float seconds, float level, float pan = PAN_KEYS)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);

            //Left of centre, opposite the hats: in the lobby piece the keys carry the line over almost nothing
            //else, so giving them their own side is most of what stops that piece sounding like one speaker.
            PanGains(pan, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //3 ms in, a long musical ring, a soft release out.
                float env = MathF.Min(1f, t / 0.003f) * MathF.Exp(-t * 3.2f) * MathF.Min(1f, (seconds - t) / 0.15f);
                if (env <= 0f) continue;

                float body = MathF.Sin(2f * MathF.PI * freq * t) + 0.35f * MathF.Sin(2f * MathF.PI * freq * 2f * t);

                //The hammer on the tine: slightly OFF the harmonic series (3.93, not 4 — dead in tune it
                //reads as an organ stop), and gone in a fraction of the body's ring.
                float tine = 0.55f * MathF.Sin(2f * MathF.PI * freq * 3.93f * t) * MathF.Exp(-t * 9f);

                float tremolo = 1f + 0.06f * MathF.Sin(2f * MathF.PI * 4.6f * t);

                Add(mix, at + i, (0.62f * body + tine) * env * tremolo * level, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The piano (#210): Nocturne's lead. The tune went on <see cref="Keys"/> when the theme was written,
        /// which cost nothing and was meant to read as a Rhodes — but a Rhodes's bright inharmonic tine,
        /// exposed at 0.30 over nothing but a walk and brushes, reads as a glockenspiel (#210), so the piece
        /// that had "almost no new synthesis" grew its one new voice after all. What makes it a piano rather
        /// than a louder Keys is the STRING, and the string brings three things a tine has none of: partials
        /// stretched <b>sharp</b> by the string's own stiffness (n·√(1+B·n²), not n — a piano is slightly out
        /// of tune with itself, and dead in tune is the organ stop the tine's own comment warns about), each
        /// partial dying at its own rate so the note <b>darkens</b> as it rings instead of fading evenly, and
        /// two strings a couple of cents apart — which beat, faster and faster in the upper partials, and
        /// that shimmer is most of how the ear names the instrument at all. Over it a felt hammer (a few
        /// milliseconds of dull thump) and the <see cref="UprightBass"/> trick, a filter that closes as the
        /// note dies: a struck string dulls, it does not switch off.
        /// </summary>
        /// <param name="pan">
        /// Where the note sits. Nocturne plays the tune dead centre — the soloist stands in the middle and
        /// the accompanist sits to one side, the seating the Rhodes's own comment records, and for the same
        /// measured reason: a part carrying a piece on its own tilts it as far as it is moved. The two
        /// strings spread <see cref="PAN_PIANO_SPREAD"/> apart around that seat on their own (#119), so the
        /// beat reads as width inside the note rather than as the note moving.
        /// </param>
        private static void Piano(float[] mix, int at, int note, float seconds, float level, float pan = PAN_CENTRE)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);

            //Two strings ±0.12 % apart (about two cents), starting out of phase so the beat is there from
            //the first cycle. The detune is per STRING and not per partial, so the beat rate climbs with n
            //exactly as it does on the instrument — the shimmy is faster at the top of the spectrum — and
            //each string takes its own seat and its own closing filter, so the two sides are genuinely
            // different signals rather than one at two levels.
            float[] stringDetune = { 0.9988f, 1.0012f };
            float[] stringPhase = { 0f, 0.37f };

            float[] gainLeft = new float[2];
            float[] gainRight = new float[2];
            for (int s = 0; s < 2; s++)
                PanGains(pan + (s == 0 ? -PAN_PIANO_SPREAD : PAN_PIANO_SPREAD), out gainLeft[s], out gainRight[s]);

            //The stretched partials and their slopes, both fixed for the whole note: frequency n·√(1+B·n²),
            //amplitude falling as 1/(n·√n), decay quickening with n. That last column is the note
            //darkening — by the tail only the low partials are left, which is why a long piano note
            //ends rounder than it began.
            float[] partialFreq = new float[7];
            float[] partialAmp = new float[7];
            float[] partialDecay = new float[7];
            for (int n = 1; n <= 7; n++)
            {
                partialFreq[n - 1] = n * freq * MathF.Sqrt(1f + 0.0004f * n * n);
                partialAmp[n - 1] = 1f / (n * MathF.Sqrt(n));
                partialDecay[n - 1] = 0.7f + 0.5f * n;
            }

            float[] lp = { 0f, 0f };

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //2 ms of hammer, then the two-stage death a struck string dies in: a fast early slope as
                //the high partials go, over a tail that is still ringing when the attack is long gone.
                float env = MathF.Min(1f, t / 0.002f) * (0.78f * MathF.Exp(-t * 3.4f) + 0.22f * MathF.Exp(-t * 0.55f))
                    * MathF.Min(1f, (seconds - t) / 0.12f);

                //The same lesson #218 taught UprightBass, this voice's own fault the first time around: the
                //2 ms attack ramp is zero on the first samples, so a break here shipped a silent piano.
                if (env <= 0.0006f) continue;

                //The felt hammer: low, dull and over in ten milliseconds, fed into the same closing filter
                //as the string so it thumps rather than ticks. One hammer, both strings.
                float hammer = t < 0.01f ? BandNoise(i, 60f, 1800f, 41) * 0.34f * (1f - t / 0.01f) : 0f;

                //Closing as it dies (the UprightBass trick, an octave up): bright at the strike, settled
                //towards the round tail.
                float alpha = CutoffToAlpha(500f + 7500f * env);

                for (int s = 0; s < 2; s++)
                {
                    float value = 0f;

                    for (int n = 0; n < 7; n++)
                        value += partialAmp[n] * MathF.Exp(-t * partialDecay[n])
                            * MathF.Sin(2f * MathF.PI * partialFreq[n] * stringDetune[s] * t + stringPhase[s]);

                    lp[s] += alpha * (value * 0.55f + hammer * 0.5f - lp[s]);

                    Add(mix, at + i, lp[s] * env * level, gainLeft[s], gainRight[s]);
                }
            }
        }

        /// <summary>
        /// The pad: three detuned saws holding a whole bar under a slow attack and a heavy low-pass. It plays
        /// where the drums do not, and its only job is to keep the quiet sections from sounding like a fault.
        /// </summary>
        /// <param name="pan">
        /// Where this note of the chord sits — the caller seats them with <see cref="ChordPan"/>, so a chord
        /// laid down one note at a time comes out spread across the field rather than stacked on one spot.
        /// The pad is what holds the quiet sections, and a wide one is the difference between those sections
        /// sounding empty and sounding open.
        /// </param>
        /// <param name="attack">
        /// How long it takes to speak. The default is the swell a pad wants under a quiet section; the victory
        /// fanfare passes a much faster one (#185), because there the same voice is being used as a <b>hit</b>
        /// and a 0.35 s swell under a drop reads as the music arriving late. It is a parameter and not a new
        /// value because this voice is shared by all four pieces, and every one of them but the fanfare wants
        /// the slow one.
        /// </param>
        private static void Pad(float[] mix, int at, int note, float seconds, float level,
            float pan = PAN_CENTRE, float attack = 0.35f)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);
            float freq = Frequency(note);

            PanGains(pan, out float gainLeft, out float gainRight);

            float[] detune = { 0.994f, 1f, 1.006f };
            float[] phases = { 0f, 0.33f, 0.66f };
            float lp = 0f;

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Slow in and slow out: a pad that starts sharply is a stab.
                float env = MathF.Min(1f, t / attack) * MathF.Min(1f, (seconds - t) / 0.5f);
                if (env <= 0f) continue;

                float sum = 0f;
                for (int d = 0; d < 3; d++)
                {
                    float f = freq * detune[d];
                    phases[d] += f / SAMPLE_RATE;
                    if (phases[d] >= 1f) phases[d] -= 1f;
                    sum += PolyBlepSaw(phases[d], f / SAMPLE_RATE);
                }

                lp += CutoffToAlpha(1500f) * (sum / 3f - lp);
                Add(mix, at + i, lp * level * env, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The fanfare's voice: a trombone rather than a synth lead. Four things make it one, and the second
        /// is the one that actually matters:
        /// <list type="bullet">
        /// <item><b>A filter envelope that opens WITH the note.</b> Brass gets brighter as it gets louder —
        /// blow harder and the harmonics come up — so the cutoff sweeps from a muffled 550 Hz to wide open
        /// over the attack and settles back as the note holds. A fixed filter over the same oscillators is a
        /// synth pad; this one line is most of the difference between the two.</item>
        /// <item><b>A slow speech.</b> ~55 ms of attack, because a trombone does not start instantly, and a
        /// touch of breath noise across it — the "blat" of the note being articulated.</item>
        /// <item><b>A sub an octave down</b>, which is the whole of the depth. Three detuned saws alone read as
        /// bright and thin whatever else is done to them.</item>
        /// <item><b>It SUSTAINS.</b> No decay to speak of until the release, so a held note is held and
        /// successive notes run into one another instead of each dying in its own gap.</item>
        /// </list>
        /// </summary>
        /// <param name="pan">Where in the section it sits; a lone line stays centred.</param>
        private static void Brass(float[] mix, int at, int note, float seconds, float level, float pan = PAN_CENTRE)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);

            //Three saws rather than the lead's two: a section, not a soloist.
            float[] detune = { 0.9955f, 1f, 1.0045f };
            float[] phases = { 0f, 0.31f, 0.67f };

            float subPhase = 0f;
            float lp1 = 0f, lp2 = 0f;

            int frames = Frames(mix);

            //The section takes the seat the caller gives it — but its SUB does not. That octave-down sine is
            //the weight of the voice and sits under 100 Hz for most of the range this plays, which is exactly
            //the band the seating rules keep in the middle, so it is written separately below.
            PanGains(pan, out float gainLeft, out float gainRight);
            PanGains(PAN_CENTRE, out float subLeft, out float subRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Slow in, flat through the middle, and a release that is long enough to overlap the next note.
                float attack = MathF.Min(1f, t / 0.055f);
                float release = MathF.Min(1f, (seconds - t) / 0.18f);
                float env = attack * release;
                if (env <= 0f) continue;

                //The brass envelope: bright on the way in, settling to a warmer sustain.
                float bite = MathF.Exp(-t * 5.5f);
                float cutoff = 550f + 3100f * attack * (0.45f + 0.55f * bite);

                float sum = 0f;
                for (int d = 0; d < 3; d++)
                {
                    float f = freq * detune[d];
                    phases[d] += f / SAMPLE_RATE;
                    if (phases[d] >= 1f) phases[d] -= 1f;
                    sum += PolyBlepSaw(phases[d], f / SAMPLE_RATE);
                }
                sum /= 3f;

                //The breath, only across the attack.
                if (t < 0.07f) sum += Noise(i, 71) * 0.22f * (1f - t / 0.07f);

                //Two poles rather than one: a single pole is 6 dB an octave and leaves the top end hissing
                //through, which is exactly what stops a saw sounding like an instrument.
                float alpha = CutoffToAlpha(cutoff);
                lp1 += alpha * (sum - lp1);
                lp2 += alpha * (lp1 - lp2);

                //The sub, an octave down. Sine rather than saw: it is there for weight, not for colour.
                subPhase += 2f * MathF.PI * (freq * 0.5f) / SAMPLE_RATE;
                float sub = MathF.Sin(subPhase) * 0.5f;

                Add(mix, at + i, lp2 * level * env, gainLeft, gainRight);
                Add(mix, at + i, sub * level * env, subLeft, subRight);
            }
        }

        /// <summary>
        /// The string section (#120), and it is the one voice <see cref="MusicTheme.Bohemia"/> could not be
        /// written without: everything else in this file is a dance instrument, and a piece meant to sound
        /// symphonic played entirely on them is a dance track with pretensions.
        /// <para>
        /// Four things separate a section from the <see cref="Pad"/>'s three detuned saws, and the first two do
        /// most of the work:
        /// </para>
        /// <list type="bullet">
        /// <item><b>Seven oscillators spread UNEVENLY.</b> Evenly spaced detunes beat against each other at one
        /// rate and read as a chorus effect on one instrument; irrational-ish spacings give the dense,
        /// directionless shimmer of many players who each missed the pitch by their own amount.</item>
        /// <item><b>The vibrato is per oscillator and FADES IN.</b> A section does not start vibrating — the
        /// players lean into the note — and if they all leaned at the same rate it would be one violinist
        /// through a chorus pedal. Each has its own rate near 5 Hz and its own phase.</item>
        /// <item><b>A bow, not a key.</b> ~110 ms of attack shaped so the note arrives rather than starts, and
        /// a release long enough that successive notes overlap into a line.</item>
        /// <item><b>Two poles, and the cutoff rides the envelope</b> — a section played louder is brighter, the
        /// same reason <see cref="Brass"/> sweeps. One pole leaves the top hissing, which is what stops a bank
        /// of saws sounding like an instrument.</item>
        /// </list>
        /// </summary>
        private static void Strings(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);

            //Uneven on purpose (see the remarks): no two gaps here are the same, so no single beat frequency
            //dominates and the ensemble has no rate the ear can lock onto.
            float[] detune = { 0.9931f, 0.9968f, 0.9989f, 1f, 1.0013f, 1.0041f, 1.0074f };
            float[] phases = { 0.11f, 0.42f, 0.77f, 0f, 0.29f, 0.63f, 0.91f };
            float[] vibRate = { 4.7f, 5.1f, 5.4f, 4.9f, 5.6f, 5.0f, 4.4f };

            //THE WIDEST THING IN EITHER PIECE, and it costs nothing that was not already being computed. These
            //seven voices were detuned to beat against one another; seating them across the stage makes the
            //beating a MOVEMENT across the field rather than a wobble at one point, which is the whole
            //difference between a sample of a string section and the sound of one. Index 3 is the in-tune
            //voice and sits in the middle, exactly as the detune table has it.
            //
            //Two filter states, because the two sides are now genuinely different signals — running one filter
            //on the sum and panning the result afterwards would give the same wobble at one point again.
            float lp1L = 0f, lp2L = 0f, lp1R = 0f, lp2R = 0f;

            float[] voiceLeft = new float[7], voiceRight = new float[7];
            for (int d = 0; d < 7; d++)
                PanGains((d - 3) / 3f * PAN_STRINGS_SPREAD, out voiceLeft[d], out voiceRight[d]);

            int frames = Frames(mix);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float attack = MathF.Min(1f, t / 0.11f);
                float release = MathF.Min(1f, (seconds - t) / 0.30f);
                float env = attack * attack * release;   //squared, so the bow speaks rather than steps in
                if (env <= 0f) continue;

                //Leaning into the note: no vibrato at the attack, full by about a third of a second.
                float lean = 0.0042f * MathF.Min(1f, MathF.Max(0f, (t - 0.12f) / 0.34f));

                float sumLeft = 0f, sumRight = 0f;
                for (int d = 0; d < 7; d++)
                {
                    float f = freq * detune[d] * (1f + lean * MathF.Sin(2f * MathF.PI * vibRate[d] * t + d));
                    phases[d] += f / SAMPLE_RATE;
                    if (phases[d] >= 1f) phases[d] -= 1f;

                    float saw = PolyBlepSaw(phases[d], f / SAMPLE_RATE);
                    sumLeft += saw * voiceLeft[d];
                    sumRight += saw * voiceRight[d];
                }

                float alpha = CutoffToAlpha(700f + 2600f * env);
                lp1L += alpha * (sumLeft / 7f - lp1L);
                lp2L += alpha * (lp1L - lp2L);
                lp1R += alpha * (sumRight / 7f - lp1R);
                lp2R += alpha * (lp1R - lp2R);

                //Already panned per voice, so this writes the two channels straight rather than through a pan
                Add(mix, at + i, lp2L * level * env, 1f, 0f);
                Add(mix, at + i, lp2R * level * env, 0f, 1f);
            }
        }

        /// <summary>
        /// The timpani (#120): the drum a symphonic piece announces itself with, and the one percussion voice
        /// here that carries a PITCH — so it can play the bass of a cadence rather than merely mark a beat.
        /// <para>
        /// It is not <see cref="Tom"/> with a lower number. A tom is 200 ms of skin; a kettle is a tuned
        /// membrane over a resonating bowl, so this holds its pitch (the sweep settles within a fifth of a
        /// second instead of falling through the note) and rings for well over a second, and the stick's noise
        /// is a brief crack across the attack rather than a band running through the body. The long ring is the
        /// whole point: it is what fills the bar under a held string chord, and a short one reads as a floor tom.
        /// </para>
        /// </summary>
        private static void Timpani(float[] mix, int at, int note, float level)
        {
            int length = (int)(SAMPLE_RATE * 1.5f);
            int frames = Frames(mix);
            float freq = Frequency(note);
            float phase = 0f, harmonic = 0f;

            //Centre with the kick and the bass: it plays the bass of a cadence an octave under the root, which
            //is the lowest pitched thing in Bohemia and squarely inside the band the seating rules anchor.
            PanGains(PAN_CENTRE, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //A short sweep that SETTLES: the head is tightest at the strike and relaxes onto the tuned
                //pitch. A kettle that keeps falling is a tom.
                float f = freq * (1f + 0.16f * MathF.Exp(-t * 26f));
                phase += 2f * MathF.PI * f / SAMPLE_RATE;
                harmonic += 2f * MathF.PI * f * 2.9f / SAMPLE_RATE;   //inharmonic, as a real head's modes are

                //The bowl rings long; its overtone dies fast, which is what leaves a clean low note behind.
                float body = MathF.Sin(phase) * MathF.Exp(-t * 1.7f)
                    + MathF.Sin(harmonic) * 0.18f * MathF.Exp(-t * 9f);

                //The stick, and only at the very start.
                float crack = t < 0.012f ? BandNoise(i, 900f, 6000f, 83) * 0.5f * (1f - t / 0.012f) : 0f;

                Add(mix, at + i, (body * 0.8f + crack) * level, gainLeft, gainRight);
            }
        }

        /// <summary>
        /// The lead: two saws detuned about twelve cents apart. That beating between them is the whole
        /// "supersaw" the genre is built on, and two oscillators are enough — the width comes from the detune,
        /// not from the count.
        /// </summary>
        private static void Lead(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);

            float freqA = freq * 0.9965f;
            float freqB = freq * 1.0035f;

            float phaseA = 0f, phaseB = 0.5f;

            //The supersaw's width, and the reason it is done HERE rather than by panning the finished note:
            //the twelve cents between these two saws is what the voice is built on (see "The lead is two saws
            //detuned about twelve cents apart"), so pulling them apart makes the beating sweep between the
            //speakers. The NOTE stays centred — the lead is the tune, and a tune that wanders is a fault.
            //A filter each, or the two would be summed back to one signal before they were ever separated.
            float lpA = 0f, lpB = 0f;

            PanGains(-PAN_LEAD_SPREAD, out float leftA, out float rightA);
            PanGains(PAN_LEAD_SPREAD, out float leftB, out float rightB);

            int frames = Frames(mix);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Sustains rather than decaying away: the chorus holds notes for half a bar, and an envelope
                //that has fallen to nothing by then turns a held note into a stab with a long silence after it.
                float env = MathF.Min(1f, t / 0.012f) * MathF.Min(1f, (seconds - t) / 0.12f) * MathF.Exp(-t * 0.9f);

                float vibrato = 1f + 0.004f * MathF.Sin(2f * MathF.PI * 5.2f * t) * MathF.Min(1f, t / 0.25f);

                phaseA += freqA * vibrato / SAMPLE_RATE; if (phaseA >= 1f) phaseA -= 1f;
                phaseB += freqB * vibrato / SAMPLE_RATE; if (phaseB >= 1f) phaseB -= 1f;

                float alpha = CutoffToAlpha(4200f);
                lpA += alpha * (PolyBlepSaw(phaseA, freqA / SAMPLE_RATE) * 0.5f - lpA);
                lpB += alpha * (PolyBlepSaw(phaseB, freqB / SAMPLE_RATE) * 0.5f - lpB);

                Add(mix, at + i, lpA * level * env, leftA, rightA);
                Add(mix, at + i, lpB * level * env, leftB, rightB);
            }
        }

        /// <summary>
        /// The electric guitar (#163): the one voice here whose spectrum is set by what happens <b>after</b>
        /// the oscillators rather than by which oscillator it is. Everything else in this file shapes a
        /// waveform and filters it; this one drives a waveform into a non-linearity, which <i>adds</i>
        /// harmonics that were never generated — and that difference is the whole instrument.
        /// <list type="bullet">
        /// <item><b>The strings are summed before the drive, never after.</b> A non-linearity multiplies as
        /// well as adds, so two notes played into one produce their sum and difference tones as well as
        /// themselves. That is why <paramref name="power"/> renders the whole chord here instead of the
        /// arrangement calling this three times: driving each note separately and mixing the results is a
        /// mix of three guitars, and it is a completely different sound.</item>
        /// <item><b>Which is also why the chord has no third in it.</b> Root, fifth and octave are 2:3:4, so
        /// every intermodulation product lands on a note already in the chord; a major third is 5:4 and its
        /// products do not. A driven power chord is a wall and a driven triad is porridge — rock's harmony
        /// follows from its amplifier, and this voice is where that is enforced.</item>
        /// <item><b>Filtered before the amp and filtered after it</b>, because a guitar rig is a tone control,
        /// then a valve stage, then a speaker in a box. The pre-filter keeps the saw's top harmonics out of
        /// the waveshaper (drive them and the result is fizz rather than grind); the two-pole cabinet at 4 kHz
        /// is what stops the output sounding like a distortion pedal into a desk. The cabinet's <b>high-pass
        /// at 110 Hz</b> is load-bearing in the mix rather than in the tone: it is what lets a wall of
        /// guitars sit over the bass and the floor instead of on top of them.</item>
        /// <item><b>The output is normalised by the drive.</b> <c>tanh(drive · x) / tanh(drive)</c> keeps the
        /// peak where it was, so pushing the amp changes the spectrum and not the level — which is what makes
        /// the arrangement's Drive column a musical dial rather than a second fader.</item>
        /// <item><b>Distortion is compression, so a driven note sustains.</b> The decay falls with the drive:
        /// a picked clean note is gone in a couple of seconds and a saturated one holds. That is one line and
        /// it is most of why the choruses feel bigger than the verses.</item>
        /// </list>
        /// </summary>
        /// <param name="drive">How hard the amp is pushed: ~1 is a valve at the edge of breakup, 4-5 crunch,
        /// 9-11 a saturated lead.</param>
        /// <param name="power">Render the root, the fifth and the octave through one waveshaper.</param>
        /// <param name="take">
        /// Which of two performances this is. The rhythm part is <b>double-tracked</b> — the same chord played
        /// twice and seated hard either side — and the takes are tuned a hair apart and start at different
        /// points in their cycles, so the two sides are genuinely different signals. That is #119's rule
        /// (width comes from different signals, never from one signal at two levels) arriving as the idiom's
        /// own studio practice.
        /// </param>
        private static void Guitar(float[] mix, int at, int note, float seconds, float level, float drive,
            bool power = false, int take = 0, float pan = PAN_CENTRE)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            int frames = Frames(mix);

            float stretch = take == 0 ? 0.9986f : 1.0013f;
            int strings = power ? 3 : 1;

            float[] freq = { Frequency(note) * stretch, Frequency(note + 7) * stretch, Frequency(note + 12) * stretch };
            float[] phase = take == 0 ? new[] { 0f, 0.37f, 0.71f } : new[] { 0.19f, 0.83f, 0.44f };

            float decay = 2.6f / (1f + 0.5f * drive);
            float normalise = 1f / MathF.Tanh(drive * 1.6f);

            float pre = 0f, cab1 = 0f, cab2 = 0f, low = 0f;
            float cabAlpha = CutoffToAlpha(4000f), toneAlpha = CutoffToAlpha(2600f), lowAlpha = CutoffToAlpha(110f);

            PanGains(pan, out float gainLeft, out float gainRight);

            for (int i = 0; i < length && at + i < frames; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float env = MathF.Min(1f, t / 0.004f) * MathF.Min(1f, (seconds - t) / 0.09f) * MathF.Exp(-t * decay);
                if (env <= 0f) continue;

                //A held single note is leaned into; a chord is not — a guitarist vibratos the tune and lets
                //the wall stand still.
                float vibrato = power
                    ? 1f
                    : 1f + 0.005f * MathF.Sin(2f * MathF.PI * 5.6f * t)
                        * MathF.Min(1f, MathF.Max(0f, (t - 0.18f) / 0.35f));

                float sum = 0f;
                for (int s = 0; s < strings; s++)
                {
                    float f = freq[s] * vibrato;
                    phase[s] += f / SAMPLE_RATE;
                    if (phase[s] >= 1f) phase[s] -= 1f;
                    sum += PolyBlepSaw(phase[s], f / SAMPLE_RATE);
                }
                sum /= strings;

                //The plectrum, and it goes in BEFORE the amp: a pick attack driven with the note is part of
                //the sound, where one added afterwards is a click laid over it.
                if (t < 0.01f) sum += Noise(i, 149 + take) * 0.35f * (1f - t / 0.01f);

                pre += toneAlpha * (sum - pre);

                float driven = MathF.Tanh(pre * drive * 1.6f) * normalise;

                cab1 += cabAlpha * (driven - cab1);
                cab2 += cabAlpha * (cab1 - cab2);
                low += lowAlpha * (cab2 - low);

                Add(mix, at + i, (cab2 - low) * level * env, gainLeft, gainRight);
            }
        }

        #endregion

        #region Oscillators and helpers

        private static float Frequency(int midiNote) => 440f * MathF.Pow(2f, (midiNote - 69) / 12f);

        private static float CutoffToAlpha(float cutoff)
        {
            float dt = 1f / SAMPLE_RATE;
            float rc = 1f / (2f * MathF.PI * cutoff);
            return dt / (rc + dt);
        }

        /// <summary>
        /// A saw, anti-aliased by PolyBLEP: the naive ramp with a polynomial correction applied either side of
        /// its discontinuity.
        /// <para>
        /// This matters more here than anywhere else in the project. A naive saw or square has energy at every
        /// harmonic, and at 44.1 kHz everything above Nyquist folds back down as inharmonic tones — on a lead
        /// playing an A5 that is a chorus of wrong notes moving the opposite way to the melody, which reads as
        /// cheapness rather than as a bug. Two lines of correction remove nearly all of it, and unlike summing
        /// band-limited harmonics it costs the same at any pitch.
        /// </para>
        /// </summary>
        private static float PolyBlepSaw(float phase, float increment) => 2f * phase - 1f - PolyBlep(phase, increment);

        /// <summary>A square, anti-aliased the same way — a correction at each of its two edges per cycle.</summary>
        private static float PolyBlepSquare(float phase, float increment)
        {
            float value = phase < 0.5f ? 1f : -1f;

            value -= PolyBlep(phase, increment);
            value += PolyBlep((phase + 0.5f) % 1f, increment);

            return value;
        }

        /// <summary>The correction itself: a parabola spanning one sample either side of the discontinuity.</summary>
        private static float PolyBlep(float t, float dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }

            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }

            return 0f;
        }

        /// <summary>Deterministic white noise, seeded so two layers in one instrument are not the same sequence.</summary>
        private static float Noise(int i, int seed)
        {
            uint h = (uint)(i * 2654435761u) ^ 0x9E3779B9u ^ (uint)(seed * 374761393);
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;
            return (h / (float)uint.MaxValue) * 2f - 1f;
        }

        /// <summary>
        /// Band-passed noise: the difference of two smoothed reads of the same sequence. Not a textbook
        /// band-pass, but for a hat or a clap the ear is judging the band and the decay, not the skirts.
        /// </summary>
        private static float BandNoise(int i, float low, float high, int seed)
        {
            float a = 0f, b = 0f;
            float alphaHigh = CutoffToAlpha(high);
            float alphaLow = CutoffToAlpha(low);

            for (int k = Math.Max(0, i - 24); k <= i; k++)
            {
                float white = Noise(k, seed);
                a += alphaHigh * (white - a);
                b += alphaLow * (white - b);
            }

            return a - b;
        }

        /// <summary>
        /// Drives the mix to a target RMS and saturates it softly — the music's own limiter.
        /// <para>
        /// It reads the interleaved stereo buffer as one signal, and that is deliberate rather than incidental
        /// (#119): the RMS is taken across both channels and the drive it derives is a <b>single number
        /// applied to every sample</b>, so the stereo image cannot move. A limiter that measured and drove the
        /// two channels independently would turn every loud moment on one side into a level change on that
        /// side alone, which is the image pumping — the classic way to wreck a mix while making each channel
        /// individually correct. If this ever grows a real envelope follower, the gain must stay linked.
        /// </para>
        /// </summary>
        private static void Limit(float[] signal, float targetRms, float ceiling)
        {
            double sum = 0.0;
            for (int i = 0; i < signal.Length; i++) sum += signal[i] * (double)signal[i];

            float rms = (float)Math.Sqrt(sum / Math.Max(1, signal.Length));
            if (rms < 1e-6f) return;

            float drive = targetRms / rms;
            for (int i = 0; i < signal.Length; i++) signal[i] = MathF.Tanh(signal[i] * drive) * ceiling;
        }

        /// <summary>
        /// The 16-bit stereo PCM the XAudio2 side of every bake plays: the interleaved float mix clamped and
        /// scaled to shorts. One conversion for both of its callers — the one-shot <see cref="SoundEffect"/>
        /// buffers and the chain's <see cref="DynamicSoundEffectInstance"/> submissions — so the two can not
        /// drift in format.
        /// </summary>
        private static byte[] ToPcm(float[] signal)
        {
            byte[] pcm = new byte[signal.Length * 2];

            for (int i = 0; i < signal.Length; i++)
            {
                short v = (short)(MathHelper.Clamp(signal[i], -1f, 1f) * short.MaxValue);
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }

            return pcm;
        }

        private static SoundEffect ToSoundEffect(float[] signal)
        {
            //Stereo since #119. The buffer is already interleaved left-then-right, which is exactly the layout
            //16-bit PCM wants, so the conversion above needs no notion of channels — only this line does.
            return new SoundEffect(ToPcm(signal), SAMPLE_RATE, AudioChannels.Stereo);
        }

        #endregion

        public void Dispose()
        {
            _failed = true;   //so a late Update cannot resurrect it
            _wanted = false;
            _menuWanted = false;

            _voice?.Dispose();
            _fanfare?.Dispose();
            _fanfareTrack?.Dispose();
            _menu?.Dispose();
            _menuTrack?.Dispose();
        }
    }
}
