using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Threading.Tasks;

namespace BS3D
{
    /// <summary>
    /// The level theme: a two-minute eurodance track synthesized from raw PCM and played on a loop. No tracker
    /// file, no asset, no pipeline step — the score is a handful of arrays and the instruments are oscillators,
    /// the same line the sound effects, the meshes and the surface textures all take. The same instruments
    /// also play the result fanfares and the front end's looped piece (see <see cref="BakeMenu"/>).
    /// <para>
    /// It is <b>arranged</b> rather than looped: eight sections of eight bars, each one adding or taking away
    /// parts — an intro that builds, verses, a chorus that is unmistakably the chorus, a breakdown where the
    /// drums drop out entirely, a build that puts them back, and a last chorus with everything on it. A
    /// sixteen-bar loop with no sections is a ringtone; what makes a track worth hearing for two minutes is
    /// that it keeps arriving somewhere.
    /// </para>
    /// </summary>
    public sealed class ProceduralMusic : IDisposable
    {
        private const int SAMPLE_RATE = 44100;

        //Around 128 rather than the 146 this started at, and rolled per pass inside a narrow band (see
        //Variation). 146 was fast enough to read as frantic; a slower tempo leaves room for the extra
        //percussion and for the melody to breathe — the same notes at 128 sound deliberate where at 146 they
        //sound hurried.
        private const int STEPS_PER_BEAT = 4;      //sixteenths
        private const int STEPS_PER_BAR = 16;

        private const int BARS_PER_SECTION = 8;
        private const int SECTIONS = 9;   //intro, verse, chorus, breakdown, verse, chorus, build, chorus, outro
        private const int BARS = BARS_PER_SECTION * SECTIONS;   //64 bars ≈ 2:00 at 128 BPM
        private const int TOTAL_STEPS = BARS * STEPS_PER_BAR;

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

        /// <summary>Which melody, if any, a section carries.</summary>
        private enum LeadPart { None, Verse, Chorus }

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
            public readonly bool Kick, Clap, Hats, Ride, Bass, Arp, Pad, Roll;
            public readonly LeadPart Lead;
            public readonly float Level;   //overall weight of the section, so a breakdown is quieter as well as emptier

            public Section(bool kick, bool clap, bool hats, bool ride, bool bass, bool arp, bool pad, bool roll,
                LeadPart lead, float level)
            {
                Kick = kick; Clap = clap; Hats = hats; Ride = ride; Bass = bass;
                Arp = arp; Pad = pad; Roll = roll; Lead = lead; Level = level;
            }
        }

        //                                  kick   clap   hats   ride   bass    arp    pad   roll  lead              level
        private static readonly Section[] ARRANGEMENT =
        {
            //0 INTRO. No drums at all for its first half — pad and arpeggio alone, so the track begins by
            //arriving rather than by already being under way, and the kick landing halfway through is an event.
            new(true,  false, true,  false, false, true,  true,  false, LeadPart.None,   0.62f),
            new(true,  true,  true,  false, true,  true,  false, false, LeadPart.Verse,  0.95f),  //1 verse
            new(true,  true,  true,  true,  true,  true,  false, false, LeadPart.Chorus, 1.00f),  //2 CHORUS
            //3 breakdown — drums out. Measured at 0.55 this fell to a fifth of the verse's level and a ninth of
            //its low band, which is not a breakdown but a gap; almost all of that is simply losing the kick and
            //the bass, which carry most of a mix's energy. The parts that remain are pushed up to compensate,
            //so the section reads as EMPTIER rather than as quieter — which is what a breakdown is.
            new(false, false, true,  false, false, true,  true,  false, LeadPart.None,   0.85f),
            new(true,  true,  true,  false, true,  true,  false, false, LeadPart.Verse,  0.95f),  //4 verse
            new(true,  true,  true,  true,  true,  true,  false, false, LeadPart.Chorus, 1.00f),  //5 CHORUS
            new(true,  false, true,  true,  true,  true,  false, true,  LeadPart.None,   0.90f),  //6 build — the roll
            new(true,  true,  true,  true,  true,  true,  true,  false, LeadPart.Chorus, 1.00f),  //7 CHORUS, everything
            //8 OUTRO. The parts fall away and a fade is laid over the whole section, so the track ENDS instead
            //of being cut off. That is what makes the regeneration seamless: the join between one pass and the
            //next lands in silence, so the frame or two it takes to swap buffers cannot be heard.
            new(true,  false, true,  false, true,  true,  true,  false, LeadPart.None,   0.70f)
        };

        private const int SECTION_INTRO = 0;
        private const int SECTION_OUTRO = SECTIONS - 1;

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

        private Task<float[]> _next;      //the pass after the one playing, baking on a background thread
        private SoundEffect _track;
        private SoundEffectInstance _instance;
        private bool _wanted;             //the game wants music; the instance may still be between passes
        private bool _failed;

        //The fanfare is its own instance so it is independent of the loop: Stop() silences the level's theme
        //without cutting off the piece that is announcing the result.
        private Task<float[]> _fanfareBake;
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
        public bool IsPlaying => _instance != null && _instance.State == SoundState.Playing;

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
                if (_instance != null) _instance.Volume = MUSIC_VOLUME * _gain;
                if (_fanfare != null) _fanfare.Volume = FANFARE_VOLUME * _gain;
                if (_menu != null) _menu.Volume = MENU_VOLUME * _gain;
            }
        }

        /// <summary>
        /// Starts synthesizing the first pass at once, on a background thread. Two minutes of PCM is a couple
        /// of seconds of arithmetic, and doing it on the loading thread would be two seconds of a black
        /// window; nothing asks for the track until a level is built, which is several menus later.
        /// </summary>
        /// <param name="seed">
        /// Fixed only for testing — left null the music is different every run, which is the point of it.
        /// </param>
        public ProceduralMusic(int? seed = null)
        {
            _seeds = seed.HasValue ? new Random(seed.Value) : new Random();
            _next = StartBake();

            //The menu piece bakes alongside the first pass — it is the one wanted first, at the splash, and
            //it is a fraction of the theme's arithmetic, so it is ready well inside the menu's first seconds.
            int menuSeed = _seeds.Next();
            _menuBake = Task.Run(() => BakeMenu(menuSeed));
        }

        private Task<float[]> StartBake()
        {
            int seed = _seeds.Next();
            return Task.Run(() => Bake(seed));
        }

        /// <summary>Starts the music, or does nothing if it is already sounding.</summary>
        public void Play()
        {
            if (_failed) return;

            _wanted = true;
            if (_instance == null || _instance.State == SoundState.Stopped) Advance();
        }

        /// <summary>Stops the music. The pass baking behind it is kept — it becomes the next one played.</summary>
        public void Stop()
        {
            _wanted = false;
            _instance?.Stop();
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
        public void PlayVictory(int score) => StartFanfare(score, victory: true);

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
            _fanfare?.Stop();
        }

        private void StartFanfare(int score, bool victory)
        {
            if (_failed) return;

            //0 for nothing, 1 for a very good result. There is no natural ceiling to a score, so the reference
            //is a stated constant rather than anything derived — see FANFARE_FULL_SCORE.
            float intensity = MathHelper.Clamp(score / (float)FANFARE_FULL_SCORE, 0f, 1f);
            int seed = _seeds.Next();

            //On a background thread, like the track: a fanfare is only a few seconds of PCM, but this fires on
            //the exact frame a level ends — which is also the frame the camera is released, the fireworks
            //start and the result screen is being built — and that is the last moment to spend on synthesis.
            _fanfareBake = Task.Run(() => victory ? BakeVictory(seed, intensity) : BakeDefeat(seed, intensity));
        }

        /// <summary>
        /// Called once a frame. Its whole job is the handover: a pass is played <b>once</b>, not looped, and
        /// when it ends the next variation — baked on a background thread while this one was playing — takes
        /// over. That is what makes the music genuinely endless rather than a loop that repeats: the player
        /// never hears the same two minutes twice.
        /// <para>
        /// The frame or two the handover costs is exactly why the arrangement ends in a faded <b>outro</b>: the
        /// join lands in silence, so a gap that would be an audible glitch in the middle of a beat is
        /// inaudible at the end of a phrase.
        /// </para>
        /// </summary>
        public void Update()
        {
            if (_failed) return;

            //The fanfare first: it is realized the frame its synthesis finishes, so the piece announcing the
            //result lands as close to the result as the machine allows.
            if (_fanfareBake != null && _fanfareBake.IsCompleted)
            {
                Task<float[]> ready = _fanfareBake;
                _fanfareBake = null;

                try
                {
                    SoundEffectInstance old = _fanfare;
                    SoundEffect oldTrack = _fanfareTrack;

                    _fanfareTrack = ToSoundEffect(ready.Result);
                    _fanfare = _fanfareTrack.CreateInstance();
                    _fanfare.Volume = FANFARE_VOLUME * _gain;
                    _fanfare.Play();

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
            if (_instance != null && _instance.State != SoundState.Stopped) return;

            Advance();
        }

        /// <summary>
        /// Puts the next baked pass on, or replays the current one if the next is not ready yet — which is the
        /// right fallback, since the alternative is silence in the middle of a level. Either way another bake
        /// is started, so there is always one in hand.
        /// </summary>
        private void Advance()
        {
            //A level that starts without music is a disappointment; a level that will not start because the
            //synthesis threw is a bug the player cannot get past. Guarded for that reason, and the same one
            //LoadLevelSet's is.
            try
            {
                if (_next != null && _next.IsCompleted)
                {
                    SoundEffectInstance old = _instance;
                    SoundEffect oldTrack = _track;

                    _track = ToSoundEffect(_next.Result);
                    _instance = _track.CreateInstance();
                    _instance.Volume = MUSIC_VOLUME * _gain;

                    //Disposed only once the replacement exists, so a failure part-way through leaves the
                    //previous pass intact and playable rather than leaving the game silent.
                    old?.Dispose();
                    oldTrack?.Dispose();

                    _next = StartBake();
                }

                _instance?.Play();
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

            //A whole number of samples per step, and the track's length taken FROM that rather than from the
            //nominal tempo: rounding per step and then trusting the nominal length leaves a fraction of a step
            //of silence at the seam.
            float[] mix = new float[samplesPerStep * TOTAL_STEPS];

            for (int step = 0; step < TOTAL_STEPS; step++)
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
                float fade = sectionIndex == SECTION_OUTRO
                    ? 1f - (barInSection * STEPS_PER_BAR + inBar) / (float)(BARS_PER_SECTION * STEPS_PER_BAR)
                    : 1f;

                level *= fade * fade;
                if (level <= 0.001f) continue;

                //DRUMS ---------------------------------------------------------------------------------
                if (section.Kick && !introQuiet && inBar % 4 == 0) Kick(mix, at, level);

                if (section.Clap && !introQuiet && (inBar == 4 || inBar == 12)) Clap(mix, at, level);

                if (section.Hats && inBar % 2 == 0)
                    Hat(mix, at, open: inBar % 4 == 2, level: (introQuiet ? 0.16f : 0.30f) * level);

                //The ride: straight sixteenths, and it is most of what "more beats" means here. Only in the
                //big sections, because a sixteenth ride running under a breakdown is not a breakdown.
                if (section.Ride) Hat(mix, at, open: false, level: 0.11f * level);

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
                    float through = (barInSection * STEPS_PER_BAR + inBar) / (float)(BARS_PER_SECTION * STEPS_PER_BAR);

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
                    foreach (int note in arp) Pad(mix, at, note + transpose, secondsPerStep * 15.5f, 0.10f * level);

                //ARPEGGIO ------------------------------------------------------------------------------
                if (section.Arp)
                {
                    int arpStep = inBar % 8;
                    int index = arpStep < 4 ? arpStep : 7 - arpStep;
                    if (variation.ArpDown) index = 3 - index;

                    Arp(mix, at, arp[index] + 12 + transpose, secondsPerStep * 0.9f, 0.15f * level);
                }

                //LEAD ----------------------------------------------------------------------------------
                if (section.Lead == LeadPart.None) continue;

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

        /// <summary>
        /// The front end's piece (#46): the theme's pads with the drums left at the door, and a line of its
        /// own. Held pad chords over the same diatonic progressions, their root an octave under for warmth, a
        /// quarter-note line on the lobby's <see cref="Keys"/> — an electric-piano voice, not the theme's
        /// square Arp, which exposed at this rate read as a touch-tone phone — and a high sparkle every other
        /// bar: enough motion to say the game is alive, nothing that asks to be listened to. It rolls its own
        /// tempo, key, progression and line direction from the seed, so no two runs share a lobby.
        /// <para>
        /// It is a LOOP, and the seam is closed by construction rather than by luck: the piece is rendered
        /// with a bar of room past the loop point, and whatever rings into that room — a pad's release, an
        /// arp's tail — is <b>folded back onto the head</b> before the cut. Continuous play of a loop is
        /// exactly the head plus the previous pass's ring-out, so the join carries the same overlap every
        /// other bar boundary does and nothing marks it.
        /// </para>
        /// </summary>
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

            //Four times round the four-bar progression — about 38 s at this tempo, long enough that the ear
            //has let go of the start before the loop returns to it.
            const int LOOP_BARS = 16;
            int loopSamples = samplesPerStep * LOOP_BARS * STEPS_PER_BAR;
            int tailSamples = samplesPerStep * STEPS_PER_BAR;

            float[] mix = new float[loopSamples + tailSamples];

            for (int step = 0; step < LOOP_BARS * STEPS_PER_BAR; step++)
            {
                int at = step * samplesPerStep;
                int bar = step / STEPS_PER_BAR;
                int inBar = step % STEPS_PER_BAR;

                int chord = progression[bar % 4];
                int[] arp = CHORD_ARP[chord];

                //The chord, held the bar long, with its root an octave under for the warmth a bass would
                //otherwise bring.
                if (inBar == 0)
                {
                    foreach (int note in arp) Pad(mix, at, note + transpose, secondsPerStep * 15.5f, 0.11f);
                    Pad(mix, at, CHORD_ROOT[chord] - 12 + transpose, secondsPerStep * 15.5f, 0.09f);
                }

                //A quarter-note line on the lobby's keys — half the theme's rate, each note ringing a step
                //past the next one's start, so the line is a phrase rather than four separate plinks. The
                //theme's square Arp sat here first and read as a touch-tone phone; see Keys for why.
                if (inBar % 4 == 0)
                {
                    int arpStep = (inBar / 4) % 4;
                    int index = arpDown ? 3 - arpStep : arpStep;
                    Keys(mix, at, arp[index] + 12 + transpose, secondsPerStep * 5.5f, 0.12f);
                }

                //One high sparkle halfway through every other bar, on the same keys two octaves up — the
                //tine partial does the glitter up there, quieter than the line it decorates.
                if (inBar == 8 && bar % 2 == 1)
                    Keys(mix, at, arp[3] + 24 + transpose, secondsPerStep * 6f, 0.07f);
            }

            //Close the seam: fold what rings past the loop point back onto the head, then cut to the loop.
            float[] loop = new float[loopSamples];
            Array.Copy(mix, loop, loopSamples);
            for (int i = 0; i < tailSamples; i++) loop[i] += mix[loopSamples + i];

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
        private const int FANFARE_TAIL_STEPS = 14;

        /// <summary>
        /// The victory fanfare. A rising figure over I–IV–V–I, which is the oldest triumphant progression
        /// there is and still the one the ear reads instantly as an arrival.
        /// <para>
        /// <paramref name="intensity"/> (0…1, from the score) does not change the tune — it changes how much
        /// of the band is playing it. A modest win gets the melody and a pad; a big one adds percussion
        /// accents, an octave doubling, a sparkle arpeggio over the last chord and a longer, higher finish. The
        /// player hears how well they did before the result screen has told them.
        /// </para>
        /// </summary>
        private static float[] BakeVictory(int seed, float intensity)
        {
            Random random = new(seed);

            //Bright and quick, and rolled so two wins in a row are not the same piece.
            float bpm = 134f + (float)random.NextDouble() * 16f;
            float secondsPerStep = 60f / (bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            //Key, and an octave lower than this started. Up at C4-G4 the fanfare was a whistle with a chord
            //under it; a trombone lives here, and "deep" is a matter of register before it is a matter of
            //timbre. None of these is where the level's theme was, so it still reads as a change of scene.
            int[] roots = { 48, 50, 53, 55 };   //C3, D3, F3, G3
            int root = roots[random.Next(roots.Length)];

            //Four bars, and a fifth to let the last chord ring when the win was a big one.
            int bars = intensity > 0.55f ? 5 : 4;

            //Plus room for the last chord to RING OUT. The pad and the lead both fade themselves over their
            //nominal length, but that length runs past the final bar — without the tail the buffer simply ends
            //mid-sustain and the fanfare finishes on a click, which is a poor way to be told you won.
            float[] mix = new float[samplesPerStep * (bars * STEPS_PER_BAR + FANFARE_TAIL_STEPS)];

            //I - IV - V - I, as semitone offsets from the root.
            int[] degrees = { 0, 5, 7, 0, 0 };

            //Two melodic shapes, so the same win twice does not play the same phrase. Both rise: a fanfare
            //that falls is a lament, whatever the harmony under it does.
            int[][] shapes =
            {
                new[] { 0, 2, 3, 2 },   //root, fifth, octave, fifth — the bugle call
                new[] { 1, 2, 3, 3 }    //third, fifth, octave, octave — smoother, more modern
            };
            int[] shape = shapes[random.Next(shapes.Length)];

            for (int bar = 0; bar < bars; bar++)
            {
                int degree = degrees[bar];
                int chordRoot = root + degree;
                bool last = bar == bars - 1;

                int at = bar * STEPS_PER_BAR * samplesPerStep;

                //THE PAD, holding the chord underneath the whole bar. Always present: it is what makes the
                //fanfare sound like a band rather than like one synth line.
                foreach (int interval in MAJOR_TRIAD)
                    Pad(mix, at, chordRoot - 12 + interval, secondsPerStep * (last ? 22f : 15.5f), 0.13f + 0.06f * intensity);

                //THE PICKUP into bar 0: three quick rising notes, which is what turns the first chord into an
                //arrival instead of just a start. Overlapping, so it runs INTO the downbeat rather than
                //stopping just before it.
                if (bar == 0)
                    for (int i = 0; i < 3; i++)
                        Brass(mix, i * samplesPerStep, chordRoot - 12 + MAJOR_TRIAD[i],
                            secondsPerStep * 1.8f, 0.20f + 0.10f * intensity);

                //THE MELODY. One note on the downbeat and one halfway, except the last bar, which holds.
                float leadLevel = 0.30f + 0.14f * intensity;

                if (last)
                {
                    //The finish: the octave, held, and pushed a fifth higher again when the win was big.
                    int top = chordRoot + 12 + (intensity > 0.75f ? 7 : 0);
                    Brass(mix, at, top, secondsPerStep * 14f, leadLevel);

                    //The root under it, always — this is the chord landing, and a single line landing alone is
                    //a melody stopping rather than a piece finishing.
                    Brass(mix, at, chordRoot, secondsPerStep * 14f, leadLevel * (0.6f + 0.3f * intensity));
                }
                else
                {
                    //LENGTH 9 AGAINST AN 8-STEP GAP, which is the whole fix for the piece sounding choppy: at
                    //five steps each note died in the middle of its own half-bar and left a hole, so the tune
                    //arrived as a row of separate events. Overlapping them by a step and letting the brass
                    //release across the join is what makes it legato — one phrase rather than four notes.
                    Brass(mix, at, chordRoot + MAJOR_TRIAD[shape[bar]], secondsPerStep * 9f, leadLevel);
                    Brass(mix, at + 8 * samplesPerStep, chordRoot + MAJOR_TRIAD[(shape[bar] + 1) % 4],
                        secondsPerStep * 9f, leadLevel * 0.85f);

                    //An octave doubling once the win is worth one — the cheapest way to make a line sound
                    //bigger without writing a second one.
                    if (intensity > 0.45f)
                        Brass(mix, at, chordRoot + 12 + MAJOR_TRIAD[shape[bar]], secondsPerStep * 9f, leadLevel * 0.45f);
                }

                //PERCUSSION, from a middling win upwards: a kick and a clap on the chord changes, so the piece
                //has a body as well as a tune.
                if (intensity > 0.25f)
                {
                    Kick(mix, at, 0.7f + 0.3f * intensity);
                    Clap(mix, at + 8 * samplesPerStep, 0.5f + 0.4f * intensity);
                }

                //A tom run into the final chord when the win was a big one.
                if (intensity > 0.8f && bar == bars - 2)
                    for (int i = 0; i < 4; i++)
                        Tom(mix, at + (12 + i) * samplesPerStep, 120f + i * 22f, 0.8f);

                //SPARKLE over the last chord: a fast arpeggio climbing away. Only for a good win, and it is
                //most of what makes one feel like a celebration rather than a resolution.
                if (last && intensity > 0.6f)
                    for (int i = 0; i < 12; i++)
                        Arp(mix, at + i * samplesPerStep, chordRoot + 12 + MAJOR_TRIAD[i % 4] + 12 * (i / 4),
                            secondsPerStep * 1.4f, 0.12f * intensity);
            }

            Limit(mix, targetRms: 0.16f + 0.06f * intensity, ceiling: 0.95f);
            return mix;
        }

        /// <summary>
        /// The defeat fanfare: slow, minor and falling, and the exact inverse of the victory one in every
        /// dimension that matters — mode, direction, tempo and register.
        /// <para>
        /// It takes the same <paramref name="intensity"/> from the other end. A good score that still lost gets
        /// a fuller piece with a harmony under it and a resolution at the bottom; a poor one gets three thin
        /// notes and no resolution at all. Losing narrowly and losing badly should not sound the same, and the
        /// difference is what the player is owed for the run they had.
        /// </para>
        /// </summary>
        private static float[] BakeDefeat(int seed, float intensity)
        {
            Random random = new(seed);

            //Slow. Half the theme's tempo and less: the piece has to feel like it is running out.
            float bpm = 62f + (float)random.NextDouble() * 12f;
            float secondsPerStep = 60f / (bpm * STEPS_PER_BEAT);
            int samplesPerStep = (int)(SAMPLE_RATE * secondsPerStep);

            int[] roots = { 45, 43, 41, 40 };   //A2, G2, F2, E2 — low, and lower than the victory's
            int root = roots[random.Next(roots.Length)];

            const int bars = 4;
            float[] mix = new float[samplesPerStep * (bars * STEPS_PER_BAR + FANFARE_TAIL_STEPS)];

            //i - VI - iv - i: minor, and it sags rather than resolving anywhere bright.
            int[] degrees = { 0, 8, 5, 0 };

            //The melody falls. Two shapes, both descending, because a rising line under a loss reads as hope
            //and this is not that.
            int[][] shapes =
            {
                new[] { 3, 2, 1, 0 },   //octave down to the root
                new[] { 2, 1, 1, 0 }    //fifth, third, third, root — a smaller, more resigned fall
            };
            int[] shape = shapes[random.Next(shapes.Length)];

            for (int bar = 0; bar < bars; bar++)
            {
                int chordRoot = root + degrees[bar];
                bool last = bar == bars - 1;
                int at = bar * STEPS_PER_BAR * samplesPerStep;

                //The pad carries almost the whole piece. Thin when the run was poor, full when it was close.
                foreach (int interval in MINOR_TRIAD)
                    Pad(mix, at, chordRoot - 12 + interval, secondsPerStep * (last ? 26f : 16.5f),
                        0.10f + 0.08f * intensity);

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
                if (!last || intensity > 0.35f)
                    Brass(mix, at, chordRoot + MINOR_TRIAD[shape[bar]], secondsPerStep * (last ? 24f : 17f), leadLevel);

                //A harmony a third under the melody, for a run that deserved better.
                if (intensity > 0.55f)
                    Brass(mix, at, chordRoot + MINOR_TRIAD[shape[bar]] - 3, secondsPerStep * (last ? 24f : 17f),
                        leadLevel * 0.55f);
            }

            Limit(mix, targetRms: 0.10f + 0.05f * intensity, ceiling: 0.9f);
            return mix;
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
            float phase = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float freq = 45f + 105f * MathF.Exp(-t * 26f);
                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                float body = MathF.Sin(phase) * MathF.Exp(-t * 7.5f);
                float click = (i < 90) ? Noise(i, 11) * 0.5f * (1f - i / 90f) : 0f;

                mix[at + i] += (body * 0.95f + click) * 0.92f * level;
            }
        }

        /// <summary>
        /// The clap: three noise bursts a few milliseconds apart and then a longer tail. The stutter is what
        /// makes it a clap rather than a snare — a room full of hands does not land on one sample.
        /// </summary>
        private static void Clap(float[] mix, int at, float level)
        {
            int[] offsets = { 0, (int)(SAMPLE_RATE * 0.009f), (int)(SAMPLE_RATE * 0.018f) };

            foreach (int offset in offsets)
                for (int i = 0; i < SAMPLE_RATE * 0.03f && at + offset + i < mix.Length; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    mix[at + offset + i] += BandNoise(i, 1400f, 5200f, 23) * 0.5f * level * MathF.Exp(-t * 90f);
                }

            int tail = (int)(SAMPLE_RATE * 0.22f);
            for (int i = 0; i < tail && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                mix[at + i] += BandNoise(i, 1100f, 4200f, 29) * 0.26f * level * MathF.Exp(-t * 22f);
            }
        }

        /// <summary>The hat: high noise, very short closed and a good deal longer open.</summary>
        private static void Hat(float[] mix, int at, bool open, float level)
        {
            float decay = open ? 26f : 95f;
            int length = (int)(SAMPLE_RATE * (open ? 0.16f : 0.05f));

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                mix[at + i] += BandNoise(i, 6000f, 15000f, 37) * level * MathF.Exp(-t * decay);
            }
        }

        /// <summary>A tom, for the fills: the kick's pitch envelope over a shorter, higher, more tuned body.</summary>
        private static void Tom(float[] mix, int at, float startHz, float level)
        {
            int length = (int)(SAMPLE_RATE * 0.2f);
            float phase = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float freq = startHz * 0.55f + startHz * 0.45f * MathF.Exp(-t * 14f);
                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                mix[at + i] += (MathF.Sin(phase) * 0.75f + BandNoise(i, 300f, 2400f, 53) * 0.2f)
                    * 0.55f * level * MathF.Exp(-t * 11f);
            }
        }

        /// <summary>The snare, for the build's roll: a tone at 190 Hz under a wide band of noise.</summary>
        private static void Snare(float[] mix, int at, float level)
        {
            int length = (int)(SAMPLE_RATE * 0.13f);

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float body = MathF.Sin(2f * MathF.PI * 190f * t) * 0.35f;

                mix[at + i] += (body + BandNoise(i, 1200f, 8000f, 61) * 0.8f) * level * MathF.Exp(-t * 34f);
            }
        }

        /// <summary>
        /// The bass: a saw through a one-pole low-pass that opens with the note's own envelope — the cheapest
        /// thing that sounds like a filter sweep, and most of what a dance bass is.
        /// </summary>
        private static void Bass(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);
            float phase = 0f, lp = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Min(1f, t / 0.004f) * MathF.Exp(-t * 6.5f);

                phase += freq / SAMPLE_RATE;
                if (phase >= 1f) phase -= 1f;

                float saw = PolyBlepSaw(phase, freq / SAMPLE_RATE);

                lp += CutoffToAlpha(190f + 2600f * env) * (saw - lp);
                mix[at + i] += lp * 0.55f * level * env;
            }
        }

        /// <summary>The arpeggio: a plain square, short and quiet. The DOS-era voice, and it is meant to sound like one.</summary>
        private static void Arp(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);
            float phase = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Min(1f, t / 0.002f) * MathF.Exp(-t * 15f);

                phase += freq / SAMPLE_RATE;
                if (phase >= 1f) phase -= 1f;

                mix[at + i] += PolyBlepSquare(phase, freq / SAMPLE_RATE) * level * env;
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
        private static void Keys(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);

            for (int i = 0; i < length && at + i < mix.Length; i++)
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

                mix[at + i] += (0.62f * body + tine) * env * tremolo * level;
            }
        }

        /// <summary>
        /// The pad: three detuned saws holding a whole bar under a slow attack and a heavy low-pass. It plays
        /// where the drums do not, and its only job is to keep the quiet sections from sounding like a fault.
        /// </summary>
        private static void Pad(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);

            float[] detune = { 0.994f, 1f, 1.006f };
            float[] phases = { 0f, 0.33f, 0.66f };
            float lp = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Slow in and slow out: a pad that starts sharply is a stab.
                float env = MathF.Min(1f, t / 0.35f) * MathF.Min(1f, (seconds - t) / 0.5f);
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
                mix[at + i] += lp * level * env;
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
        private static void Brass(float[] mix, int at, int note, float seconds, float level)
        {
            int length = (int)(SAMPLE_RATE * seconds);
            float freq = Frequency(note);

            //Three saws rather than the lead's two: a section, not a soloist.
            float[] detune = { 0.9955f, 1f, 1.0045f };
            float[] phases = { 0f, 0.31f, 0.67f };

            float subPhase = 0f;
            float lp1 = 0f, lp2 = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
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

                mix[at + i] += (lp2 + sub) * level * env;
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

            float phaseA = 0f, phaseB = 0.5f, lp = 0f;

            for (int i = 0; i < length && at + i < mix.Length; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Sustains rather than decaying away: the chorus holds notes for half a bar, and an envelope
                //that has fallen to nothing by then turns a held note into a stab with a long silence after it.
                float env = MathF.Min(1f, t / 0.012f) * MathF.Min(1f, (seconds - t) / 0.12f) * MathF.Exp(-t * 0.9f);

                float vibrato = 1f + 0.004f * MathF.Sin(2f * MathF.PI * 5.2f * t) * MathF.Min(1f, t / 0.25f);

                phaseA += freqA * vibrato / SAMPLE_RATE; if (phaseA >= 1f) phaseA -= 1f;
                phaseB += freqB * vibrato / SAMPLE_RATE; if (phaseB >= 1f) phaseB -= 1f;

                float saw = PolyBlepSaw(phaseA, freqA / SAMPLE_RATE) + PolyBlepSaw(phaseB, freqB / SAMPLE_RATE);

                lp += CutoffToAlpha(4200f) * (saw * 0.5f - lp);
                mix[at + i] += lp * level * env;
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

        /// <summary>Drives the mix to a target RMS and saturates it softly — the music's own limiter.</summary>
        private static void Limit(float[] signal, float targetRms, float ceiling)
        {
            double sum = 0.0;
            for (int i = 0; i < signal.Length; i++) sum += signal[i] * (double)signal[i];

            float rms = (float)Math.Sqrt(sum / Math.Max(1, signal.Length));
            if (rms < 1e-6f) return;

            float drive = targetRms / rms;
            for (int i = 0; i < signal.Length; i++) signal[i] = MathF.Tanh(signal[i] * drive) * ceiling;
        }

        private static SoundEffect ToSoundEffect(float[] signal)
        {
            byte[] pcm = new byte[signal.Length * 2];

            for (int i = 0; i < signal.Length; i++)
            {
                short v = (short)(MathHelper.Clamp(signal[i], -1f, 1f) * short.MaxValue);
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }

            return new SoundEffect(pcm, SAMPLE_RATE, AudioChannels.Mono);
        }

        #endregion

        public void Dispose()
        {
            _failed = true;   //so a late Update cannot resurrect it
            _wanted = false;
            _menuWanted = false;

            _instance?.Dispose();
            _track?.Dispose();
            _fanfare?.Dispose();
            _fanfareTrack?.Dispose();
            _menu?.Dispose();
            _menuTrack?.Dispose();
        }
    }
}
