using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Camera;
using System;

namespace BS3D.Audio
{
    /// <summary>
    /// Procedurally generated sound effects, synthesized from raw 16-bit PCM at startup and played back cheaply
    /// at runtime. No content files, no pipeline step — the <see cref="SoundEffect"/> constructor takes a PCM
    /// buffer directly.
    /// <para>
    /// The synthesis is a small signal chain run entirely on a float buffer: layers of additive-harmonic tones,
    /// low-passed noise transients and a sub-bass weight are mixed, the result is run through a Schroeder
    /// reverb (four parallel comb filters into two series all-passes — the classic Freeverb topology) for a
    /// sense of space, and only then is it quantized to PCM. That chain is what moves the sound off the bare
    /// beep of a first pass and gives it body, weight and a tail.
    /// </para>
    /// <para>
    /// Every play is nudged by a small random pitch, the landed sound is chosen by ball type, and the reverb
    /// decays independently per render — so no two shots and no two landings sound exactly alike.
    /// </para>
    /// <para>
    /// <b>The five sounds that have a place in the world are placed there</b> (#75): the shot leaves the
    /// muzzle, a landing sounds at the cell it stuck to, a release at the cell that broke, and a shell whistles
    /// and bursts where it actually is. They go through <see cref="Speak"/> onto a <see cref="VoiceRing"/> and
    /// are positioned by X3DAudio from a real <see cref="AudioEmitter"/> against a real
    /// <see cref="AudioListener"/> posed from the camera — an <i>angle</i> to the source rather than the
    /// hand-rolled projection onto the lens's right axis that came before it. The four that have no emitting
    /// object — the party popper and the three UI sounds — stay fire-and-forget and dead centre, deliberately.
    /// </para>
    /// <para>
    /// <b>Direction from the hardware, distance from the game.</b> X3DAudio's own attenuation is switched off
    /// (see <see cref="DISTANCE_PLATEAU"/>) so it contributes placement and nothing else, and the distance laws
    /// stay exactly where they were written — <see cref="VolumeForDistance"/>'s floor, the shot's flat level and
    /// the burst's near-flat term are all untouched. That is the whole reason to prefer this over handing the
    /// mix to the platform: none of those three is expressible through <c>Apply3D</c> at all.
    /// </para>
    /// </summary>
    public sealed class ProceduralAudio : IDisposable
    {
        /// <summary>
        /// The authored level of the effects mix — what 100 % on the settings rows means. A constant so the
        /// mix keeps its tuning; the player's rows only ever scale it, through <see cref="Gain"/>.
        /// <para>
        /// <b>Raised from 0.7 when the effects went positional, and it buys back only half of what that cost.</b>
        /// X3DAudio's output matrix sums to 1 in every direction — measured, at every distance and on both
        /// stereo and 5.1 — where the pan law it replaces wrote <i>both</i> channels at full for a centred
        /// sound. A centred sound is therefore 6.02 dB quieter through <c>Apply3D</c>, and only 3.10 dB of that
        /// is recoverable, because <see cref="SoundEffectInstance.Volume"/> throws above 1. A sound at the edge
        /// of the field loses nothing (both laws write [0, 1] hard over), so the shift is position-dependent and
        /// no single constant can undo it. If the effects end up sitting too low under the bed and the theme,
        /// the honest fix is the other end — <c>AMBIENCE_VOLUME</c>, <c>MUSIC_VOLUME</c>, <c>MENU_VOLUME</c> and
        /// <c>FANFARE_VOLUME</c> each times ~0.71 — and that is an ear's call, not arithmetic.
        /// </para>
        /// </summary>
        private const float BASE_VOLUME = 1f;

        /// <summary>
        /// What the sounds that never reach an emitter are trimmed by. Exactly the old <see cref="BASE_VOLUME"/>,
        /// and that is the point: the popper and the UI still play through the platform's centre-hot pan law, so
        /// undoing the lift above leaves them at precisely the level they were authored and heard at. Without it
        /// they would be the only thing in the game to get 3.10 dB <i>louder</i> while everything else dropped.
        /// </summary>
        private const float NON_SPATIAL_TRIM = 0.7f;

        /// <summary>
        /// The player's volume settings (master × effects), 1 for the authored mix. Written by the host when a
        /// settings row changes. A gain on the <b>next</b> play rather than on sounds already in flight — the
        /// <see cref="FireworkDuck"/> reasoning, and nothing here sounds long enough for the difference to be
        /// heard.
        /// <para>
        /// The five spatial sounds do now have handles that could be turned down mid-flight (see
        /// <see cref="VoiceRing"/>), so the reason this stays a next-play gain has changed even though the
        /// behaviour has not: walking 32 burst voices every frame to write a volume that is almost always the
        /// same one is exactly the per-frame work the render-hygiene rules forbid, for a difference that lasts
        /// as long as one report.
        /// </para>
        /// </summary>
        public float Gain { get; set; } = 1f;

        //What every play site multiplies by: the authored level under the player's setting.
        private float Level => BASE_VOLUME * Gain;

        /// <summary>
        /// How much of their normal level the fireworks play at, 1 for full. Ducked while a fanfare is
        /// sounding, because the two arrive at the same moment and the reports are broadband and loud enough
        /// to bury a tune underneath them — a bang is an event and the fanfare is the point, so the bang gives
        /// way. Set per frame by the host from <c>ProceduralMusic.IsFanfarePlaying</c>.
        /// <para>
        /// A gain on the <b>next</b> play rather than on the ones already sounding: a burst is short enough that
        /// ducking only what has yet to start is indistinguishable from ducking everything, and the alternative
        /// is a per-frame walk over every voice in the burst ring — see <see cref="Gain"/>.
        /// </para>
        /// </summary>
        public float FireworkDuck { get; set; } = 1f;

        /// <summary>What the fireworks drop to while a fanfare plays.</summary>
        public const float FIREWORK_DUCKED = 0.35f;

        private const int SAMPLE_RATE = 44100;

        //The menu's own sounds, well under the game's: a press is confirmation, not an event. The tick is far
        //quieter still, because a held direction walks the list and the tick repeats with every step.
        private const float UI_CLICK_VOLUME = 0.4f;
        private const float UI_TICK_VOLUME = 0.16f;

        //The star cue sits above the click: it is the reward the level was played for, not a menu noise, and it
        //plays over a stopped world with nothing else sounding.
        private const float STAR_VOLUME = 0.55f;
        private const float STAR_FINAL_LIFT = 1.15f;

        private readonly SoundEffect _shoot;

        //[style][type]: what a ball of that material in that colour sounds like landing (#314). A row is baked
        //on demand by PrepareLanded and then kept for the life of the process — see that method for why this is
        //not the whole 10 x 13 cross product built at startup, and why it is not built at play time either.
        private readonly SoundEffect[][] _landed;
        private readonly SoundEffect _release;
        private readonly SoundEffect _fireworkLaunch;
        private readonly SoundEffect _fireworkBurst;
        private readonly SoundEffect _partyPopper;
        private readonly SoundEffect _uiClick;
        private readonly SoundEffect _starEarned;
        private readonly Random _random = new();

        //The voices the five positional sounds are spoken through — one ring per buffer, every instance built
        //here and reused for the life of the process. Sizes are in RING SIZES below.
        private readonly VoiceRing _shootRing;
        private readonly VoiceRing[][] _landedRings;
        private readonly VoiceRing _releaseRing;
        private readonly VoiceRing _launchRing;
        private readonly VoiceRing _burstRing;

        //The ears and the mouth: one of each for the whole process, mutated in place and never re-newed. There
        //is exactly one camera in the game, so there is exactly one listener.
        private readonly AudioListener _listener = new();
        private readonly AudioEmitter _emitter = new();

        //The listener's basis, kept beside it because Speak needs it every time and AudioListener stores only
        //what X3DAudio wants. Rebuilt once a frame by UpdateListener.
        private Vector3 _listenerForward = Vector3.Forward;
        private Vector3 _listenerRight = Vector3.Right;
        private Vector3 _listenerUp = Vector3.Up;

        //False until the camera has been posed at least once — see UpdateListener.
        private bool _listenerValid;

        #region Ring sizes

        //How many voices each sound gets. Every one of these is a measured peak of CONCURRENT sounds, not a
        //guess, and the failure mode of being short is the ring cutting its own oldest voice short (audible,
        //recoverable) rather than dropping a sound (neither) — see VoiceRing.Take.

        //A shot is 0.65 s and nothing limits the fire rate: Shoot() is edge-triggered off the key, the mouse
        //and the pad trigger alike, so four voices covers anything up to ~6 shots a second, which is faster
        //than the gun can be usefully aimed.
        private const int SHOOT_VOICES = 4;

        //A landing is 0.30 s and there is one ring PER BALL TYPE, so three each is three simultaneous landings
        //of the same colour — well past what a single shot can cause.
        private const int LANDED_VOICES = 3;

        //A release is 0.6 s and only one group can come loose per landing.
        private const int RELEASE_VOICES = 3;

        //The whistle is 0.55 s, and it is the OPENING BARRAGE that sizes this rather than the steady state: at
        //the start of a display every shell slot is free, so shells go up at INTERVAL_OPENING (~14 a second)
        //until the slots run out, not at the recycling rate the display settles into. Simulated over the real
        //schedule, the peak is ten whistles at once.
        private const int LAUNCH_VOICES = 12;

        //A report is 2.6 s, and during the barrage all MAX_SHELLS shells report inside one such window — so the
        //peak is the shell count itself, by construction. Its margin is already slightly NEGATIVE: a shell slot
        //can free and refire in RISE_MIN + LIFE_MIN = 2.52 s, inside the 2.6 s report, so in the worst case a
        //report loses its last ~80 ms — which is reverb tail, under everything else in a barrage, and inaudible.
        //A longer burst bake or a shorter shell life eats into real sound, silently.
        private const int BURST_VOICES = 32;   //Fireworks.MAX_SHELLS

        #endregion

        public ProceduralAudio()
        {
            //THE PLATEAU, SET ONCE. X3DAudio's own attenuation is a 1/d curve past the emitter's curve scaler,
            //and at the default scaler of 1 a landing 28 units out would come back 29 dB down — which is
            //precisely what VolumeForDistance's floor and the burst's near-flat term were written to avoid.
            //Measured: at this scaler the output matrix sums to exactly 1.0000 from 0 to 500 units and in every
            //direction, so the hardware contributes placement and NOTHING ELSE and every distance law in this
            //file survives verbatim. It is a plateau radius rather than an off switch (the curve does bite, at
            //20 000 units), and varying it per source would quietly put an inverse-square curve back on top of
            //ours — which is why it is written here, once, and nowhere else.
            SoundEffect.DistanceScale = DISTANCE_PLATEAU;

            //Doppler is refused, not merely unused. No velocity is ever written, so the factor would be 1
            //anyway — but the lens is not a head: the ADS lean moves it at ~140 units a second, 40 % of the
            //speed of sound, and the orbit at ~28. A listener velocity would detune everything sounding the
            //instant the player leans in or holds a turn, which is an artifact of a camera and not a sound.
            SoundEffect.DopplerScale = 0f;

            _shoot = BakeShoot();

            //The landings are the one family of sounds that is not baked here: they are (colour x material)
            //since #314, and a LEVEL NAMES ONE MATERIAL, so the row that level needs is the only one worth
            //having. PrepareLanded fills one; the vinyl's is filled now because it is what everything
            //unauthored plays, and because a first level should not pay for its row.
            _landed = new SoundEffect[BallStyleCount][];
            _landedRings = new VoiceRing[BallStyleCount][];
            PrepareLanded(BallStyle.Beach);

            _release = BakeRelease();
            _fireworkLaunch = BakeFireworkLaunch();
            _fireworkBurst = BakeFireworkBurst();
            _partyPopper = BakePartyPopper();
            _uiClick = BakeUiClick();
            _starEarned = BakeStarEarned();

            //The voices, all of them, here — an XAudio2 source voice apiece and a few ms of load. The popper
            //and the UI need none: they never reach an emitter.
            _shootRing = new VoiceRing(_shoot, SHOOT_VOICES);
            _releaseRing = new VoiceRing(_release, RELEASE_VOICES);
            _launchRing = new VoiceRing(_fireworkLaunch, LAUNCH_VOICES);
            _burstRing = new VoiceRing(_fireworkBurst, BURST_VOICES);
        }

        /// <summary>
        /// The shot leaving the barrel, spoken from the <paramref name="muzzle"/> the ball and the launch smear
        /// are spawned at — so the crack, the round and the streak cannot disagree about where the shot left.
        /// <para>
        /// Its level is deliberately <b>flat</b>, with no distance term: the gun is the player's own hardware
        /// rather than something happening out in the scene, and walking it in and out with W/S must not make
        /// the game's most frequent sound swell and duck. The muzzle sits a dozen-odd units dead ahead of the
        /// lens, so what the placement buys is the barrel's swing — a small, honest drift off centre as the gun
        /// is turned, and nothing more.
        /// </para>
        /// </summary>
        public void PlayShoot(Vector3 muzzle)
        {
            Speak(_shootRing, muzzle, NEAR_WIDEN, Level, NextPitch(0.12f));
        }

        /// <summary>
        /// Bakes the thirteen landing sounds for one material and builds their voices (#314). Call it wherever a
        /// level's <see cref="BallStyle"/> becomes known — a row already baked returns at once, so calling it per
        /// level is free after the first time that material is played.
        /// <para>
        /// <b>Why a row at a time rather than the whole cross product.</b> Ten materials times thirteen colours
        /// is 130 buffers and, at <see cref="LANDED_VOICES"/> apiece, 390 XAudio2 source voices — of which one
        /// level can ever sound thirteen. Baked lazily, a session pays for the one or two materials it actually
        /// plays and holds the 39 voices it always held.
        /// </para>
        /// <para>
        /// <b>And why not lazily at play time.</b> Baking a row is ~13 x 0.2–0.8 s of synthesis plus its reverb;
        /// doing it on the first landing of a level would spend that inside the frame that answers a shot, which
        /// is the one frame in the game that must not stall. <see cref="PlayLanded"/> still falls back to baking
        /// a row nobody prepared — once, ever, per material — because a silent landing is worse than a hitch,
        /// but the caller is expected to have asked at load.
        /// </para>
        /// </summary>
        public void PrepareLanded(BallStyle style)
        {
            int row = (int)style;
            if (row < 0 || row >= BallStyleCount || _landed[row] != null) return;

            LandedMaterial material = MaterialFor(style);

            SoundEffect[] effects = new SoundEffect[BallTypes.Count + 1];      //indexed by BallType value; slot 0 unused
            VoiceRing[] rings = new VoiceRing[effects.Length];

            for (int type = 1; type <= BallTypes.Count; type++)
            {
                effects[type] = BakeLanded(type, material);
                rings[type] = new VoiceRing(effects[type], LANDED_VOICES);
            }

            //Rings before effects, so a row is never visible half-built: PlayLanded reads _landed[row] as its
            //own guard, and the two arrays are only ever published together.
            _landedRings[row] = rings;
            _landed[row] = effects;
        }

        /// <summary>
        /// A ball snapping into the lattice. The <paramref name="type"/> selects a tone (one per colour) and the
        /// <paramref name="style"/> selects what it is made of (#314), and the sound is spoken from the cell it
        /// stuck to, so a hit on the left of the field is heard on the left.
        /// <para>
        /// It stays at the solved cell rather than following the ball's own settling glide, and the level still
        /// falls off with the <b>true</b> distance to a floor — the widening below moves the stereo image and
        /// never the loudness.
        /// </para>
        /// </summary>
        public void PlayLanded(BallType type, BallStyle style, Vector3 world)
        {
            int row = (int)style;
            if (row < 0 || row >= BallStyleCount) return;

            //The load-time bake missed this material. See PrepareLanded: a hitch once is the lesser fault.
            if (_landed[row] == null) PrepareLanded(style);

            int index = (int)type;
            if (index < 1 || index >= _landed[row].Length || _landedRings[row][index] == null) return;

            float volume = VolumeForDistance(DistanceTo(world)) * Level;

            //The jitter must stay under half the ladder's whole-tone step (1/12 octave) less a margin the ear
            //can still tell apart, or two neighbouring colours' notes could meet or swap: at the old 0.1 the
            //±1.2-semitone wobble overlapped the 2-semitone step and a low green could land under a high red.
            //It is a fraction of the note and so survives PitchScale unchanged.
            Speak(_landedRings[row][index], world, NEAR_WIDEN, volume, NextPitch(0.06f));
        }

        /// <summary>
        /// A group coming loose (#46): the lattice's snap and the freed balls popping away, one after another.
        /// <paramref name="count"/> is how many balls were cut — matched and orphaned together — and it scales
        /// the sound the way the firework burst's <c>size</c> does: a bigger release is louder and a shade
        /// deeper, which is the whole of how the ear tells a great shot from a good one before the score says
        /// so. Silent path for zero is the caller's business: a plain attach plays only the landing.
        /// </summary>
        /// <remarks>
        /// Spoken from the cell that broke and left there, deliberately, rather than following the group down:
        /// over the 0.6 s the sound lasts the freed balls fall under two units, straight down — and the stereo
        /// image is a horizontal bearing, which height does not enter at all. Tracking it would cost a held
        /// voice and an <c>Apply3D</c> every frame for a difference that cannot be heard.
        /// </remarks>
        public void PlayRelease(Vector3 world, int count)
        {
            //What counts as a FULL-SIZE release: well past this the sound has nothing more to say by getting
            //louder still. Deliberately independent of the drop cinematic's bar, which is a level's own best
            //(DropCinematic.MustBeatBestBy) — the ear has no history, and a fifteen-ball collapse should sound
            //the same whether or not the camera decided it was the biggest one yet.
            const float FULL_COUNT = 15f;
            float size = MathHelper.Clamp(count / FULL_COUNT, 0f, 1f);

            float volume = (0.45f + 0.45f * size) * VolumeForDistance(DistanceTo(world));

            Speak(_releaseRing, world, NEAR_WIDEN, MathHelper.Clamp(volume * Level, 0f, 1f),
                MathHelper.Clamp(NextPitch(0.06f) - size * 0.12f, -1f, 1f));
        }

        /// <summary>
        /// A shell leaving the ground: the rising whistle, spoken from the point it was fired from. Pitched a little
        /// each time and, because a whistle is a near-pure tone, pitched <i>widely</i> — two shells going up
        /// together on the same note read as one loud shell rather than as two.
        /// </summary>
        public void PlayFireworkLaunch(Vector3 world)
        {
            //FAR under the report, and much further under than it was. The bang is the event; the launch only
            //says one is coming, and with a shell going up every fraction of a second anything audible enough
            //to identify turns the display into a chorus of kettles. At this level it is a texture — the sense
            //that something went up — rather than a sound the ear stops to listen to.
            Speak(_launchRing, world, SKY_WIDEN, 0.08f * Level * FireworkDuck, NextPitch(0.3f));
        }

        /// <summary>
        /// A shell going off. <paramref name="size"/> (0…1) is how big the burst is: it drives the volume and,
        /// inversely, the pitch — a big shell is a deeper, louder report, which is the whole of how the ear
        /// tells a large firework from a small one at a distance.
        /// </summary>
        public void PlayFireworkBurst(Vector3 world, float size)
        {
            //A near-flat distance term. A firework IS far away — that is what it is for — so falling off the
            //way a landing does would make every burst a whisper; this only separates the near from the far.
            float distance = DistanceTo(world);
            float volume = (0.85f + 0.15f * size) * (0.8f + 0.2f * MathHelper.Clamp(1f - distance / 260f, 0f, 1f));

            Speak(_burstRing, world, SKY_WIDEN, MathHelper.Clamp(volume * Level * FireworkDuck, 0f, 1f),
                MathHelper.Clamp(NextPitch(0.12f) - size * 0.28f, -1f, 1f));
        }

        /// <summary>
        /// The party popper that opens the celebration: one dry crack of paper and confetti, centred — and
        /// centred by construction rather than by arithmetic, since it never reaches an emitter. Deliberately
        /// close where the shells are big and wet, which is what makes the shells read as distant.
        /// </summary>
        public void PlayPartyPopper()
        {
            _partyPopper.Play(0.9f * Level * NON_SPATIAL_TRIM, NextPitch(0.08f), 0f);
        }

        /// <summary>
        /// A menu entry being pressed — mouse, pad or keyboard, every path plays this one (#46).
        /// <para>
        /// The three UI sounds are the one group that stays fire-and-forget and unplaced on purpose: the menu
        /// lives at the player's hand, not out in the scene, and the backdrop behind it is orbiting the very
        /// camera a listener would be posed from — so an emitter here would make the game's own clicks sweep
        /// left and right with the scenery.
        /// </para>
        /// </summary>
        public void PlayUiClick()
        {
            _uiClick.Play(UI_CLICK_VOLUME * Level * NON_SPATIAL_TRIM, NextPitch(0.03f), 0f);
        }

        /// <summary>
        /// The focus cursor stepping an entry: the click's own buffer pitched up — faster playback is also a
        /// shorter sound, which is what a step wants — and much quieter (see <see cref="UI_TICK_VOLUME"/>).
        /// </summary>
        public void PlayUiTick()
        {
            _uiClick.Play(UI_TICK_VOLUME * Level * NON_SPATIAL_TRIM, 0.55f + NextPitch(0.03f), 0f);
        }

        /// <summary>Backing out — Escape or B: the click pitched down, so leaving sounds lower than entering.</summary>
        public void PlayUiBack()
        {
            _uiClick.Play(UI_CLICK_VOLUME * Level * NON_SPATIAL_TRIM, -0.3f + NextPitch(0.03f), 0f);
        }

        /// <summary>
        /// One star of the result screen's rating landing (#139), <paramref name="index"/> counted from 0 of
        /// <paramref name="total"/> earned. Unplaced like the other UI sounds, and for the same reason.
        /// <para>
        /// <b>The run rises, which is what makes a rating something you HEAR being counted</b> rather than the
        /// same note four times. Unlike the release's pops this one is deliberately a TONE rather than a noise
        /// gesture.
        /// </para>
        /// <para>
        /// <b>It does NOT play into silence, and #158 is what that mistake cost.</b> This file used to say it
        /// was "the only sound in the game that plays over a stopped world with no music under it" — which was
        /// wrong when it was written: the victory fanfare is started the instant the field clears, before the
        /// result screen even exists, so every star lands inside its ~9 seconds. A fixed 880 Hz root stepping
        /// by ~2.3 semitones is in no key at all, and it agreed with the piece only when that piece happened
        /// to roll A. The pitch is the CALLER's now, taken from what the fanfare actually rolled.
        /// </para>
        /// </summary>
        /// <param name="semitones">
        /// How far off the baked A5 to sound it — the caller works this out from the fanfare that is playing
        /// underneath (#158), so the chime is a chord tone of the piece's own key rather than a fixed pitch
        /// that only agreed with it when it happened to roll A. <b>Clamped to ±12 by the platform</b>:
        /// <c>SoundEffect.Play</c>'s pitch is in octaves over −1…1, so a caller must keep its own arithmetic
        /// inside an octave rather than assume any offset can be reached.
        /// </param>
        public void PlayStarEarned(int index, int total, float semitones)
        {
            float pitch = MathHelper.Clamp(semitones / 12f + NextPitch(0.015f), -1f, 1f);

            //The last star of the run lands a shade louder — an arrival rather than one more step. It is the
            //only thing that separates the fourth star of a four-star clear from the third of a three.
            float volume = STAR_VOLUME * (index == total - 1 ? STAR_FINAL_LIFT : 1f);

            _starEarned.Play(MathHelper.Clamp(volume * Level * NON_SPATIAL_TRIM, 0f, 1f), pitch, 0f);
        }

        #region Where the ear is, and where the sound is

        /// <summary>
        /// Poses the listener from the camera. Called once a frame by the host, unconditionally — the fireworks
        /// and the menu both make noise with no session standing, so the ears cannot belong to the session.
        /// <para>
        /// <b>The lens's own shaken pose</b>, so a report heard while the camera is being knocked about agrees
        /// with the frame it is drawn in. The forward axis is recovered from the target, which for the game's
        /// camera <i>is</i> the post-shake view direction by construction.
        /// </para>
        /// <para>
        /// <b>The up axis is crossed from WORLD up rather than taken from the camera</b>, and that is a decision
        /// rather than an oversight. It costs nothing audible — the largest roll the game produces, the drop
        /// cinematic's dutch tilt, moves a sound at the edge of the field by hundredths of a decibel, because
        /// the stereo image is a horizontal bearing — and it buys two things: the soundfield can never be left
        /// tipped by a roll the camera is still carrying from a screen the player has since left, and the basis
        /// stays the one the HUD's cluster profile is built on.
        /// </para>
        /// </summary>
        public void UpdateListener(ICamera camera)
        {
            Vector3 position = camera.Position;
            Vector3 toTarget = camera.Target - position;

            float length = toTarget.Length();
            if (length <= 1e-4f) return;

            Vector3 forward = toTarget / length;

            //The same basis GameplayScreen builds its cluster profile on: right-handed, crossed against world
            //up, with a fallback for a lens pointed straight down its own up axis.
            Vector3 right = Vector3.Cross(forward, Vector3.Up);
            right = right.LengthSquared() > 1e-6f ? Vector3.Normalize(right) : Vector3.Right;
            Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

            //A NaN or an infinity reaching X3DAudio silences the WHOLE audio graph — every voice, permanently,
            //with no exception raised and no way back. So the pose is gated: anything that is not finite leaves
            //the last good one standing rather than being written.
            if (!Finite(position) || !Finite(forward) || !Finite(up)) return;

            _listener.Position = position;
            _listener.Forward = forward;

            //X3DAudio does NOT normalise the orientation it is handed and does not validate it either: an up
            //vector of the wrong length silently skews the whole image, and a degenerate one collapses it to
            //dead centre. The normalisation above is load-bearing — do not "simplify" the cross product away.
            _listener.Up = up;

            _listenerForward = forward;
            _listenerRight = right;
            _listenerUp = up;

            //Only now. Until the camera has been posed once — the first Update of a run, where it still holds
            //its default degenerate pose — there is no trustworthy place to hear from, and Speak plays centred.
            _listenerValid = true;
        }

        /// <summary>
        /// Plays a sound <b>at a place</b>: takes a voice from <paramref name="ring"/>, puts the emitter where
        /// the sound is and hands the pair to X3DAudio.
        /// <para>
        /// <b>The call order is the whole of its correctness.</b> <c>Apply3D</c> writes the voice's output
        /// matrix <i>and</i> its frequency ratio, so the pitch must be set <b>after</b> it or every random
        /// nudge and every size-encoded offset in this file is silently wiped. <c>Volume</c> is a separate
        /// setting and is safe anywhere. <c>Pan</c> must <b>never</b> be written on a placed voice: it writes
        /// the same matrix <c>Apply3D</c> does, so one assignment throws the placement away. The fallback below
        /// writes it precisely because there is <i>no</i> placement to throw away yet — with no listener there
        /// is nothing to place against, and Pan is the only way left to address that matrix.
        /// </para>
        /// <para>
        /// Nothing resets a reused voice's matrix between plays, so <c>Apply3D</c> is re-applied on every take
        /// rather than only when the position has moved — a slot replayed without it would sound at the
        /// <i>previous</i> event's position.
        /// </para>
        /// </summary>
        private void Speak(VoiceRing ring, Vector3 world, float widen, float volume, float pitch)
        {
            SoundEffectInstance voice = ring.Take();

            if (!_listenerValid)
            {
                //No trustworthy pose yet — which today means the first frame of a run and nothing else, since
                //_listenerValid is latched once and never cleared. Pan is the only way to address the output
                //matrix before there is a listener to place against, and it centres the sound; the trim matches
                //the centre-hot law it restores, see NON_SPATIAL_TRIM. If _listenerValid is ever made
                //resettable, note that this branch would then be reached with a placement already on the slot,
                //which Pan does happen to overwrite — but that is not why it is written here.
                voice.Pan = 0f;
                voice.Volume = MathHelper.Clamp(volume * NON_SPATIAL_TRIM, 0f, 1f);
                voice.Pitch = MathHelper.Clamp(pitch, -1f, 1f);
                voice.Play();
                return;
            }

            //A coincident emitter is safe (it comes back as an even spread); a non-finite one is not — see the
            //gate in UpdateListener for what a NaN costs.
            _emitter.Position = Finite(world) ? Placed(world, widen) : _listener.Position;

            voice.Volume = MathHelper.Clamp(volume, 0f, 1f);
            voice.Apply3D(_listener, _emitter);

            //After Apply3D, always. And still clamped to ±1, even though an instance would accept ten octaves
            //where Play's overload would not: this is the file where a pitch written as a multiplier once put
            //the entire game an octave sharp, and the guard rail that caught it is gone.
            voice.Pitch = MathHelper.Clamp(pitch, -1f, 1f);
            voice.Play();
        }

        /// <summary>
        /// Where to put the emitter so the sound lands where the <i>ear</i> expects it. Two corrections, both
        /// applied in the listener's own frame and both to the bearing alone — the distance every caller
        /// attenuates by is taken from the true position, never from this.
        /// <list type="bullet">
        /// <item><b>The near field is widened</b> by <paramref name="widen"/>, by shortening the leg along the
        /// view axis. A cluster ten units across seen from thirty subtends about twenty degrees, so the honest
        /// bearing of a landing is <i>narrower</i> than the image the mix has had all along; without this the
        /// change would read as the stereo collapsing. Sources at or behind the lens pass through untouched,
        /// since dividing a negative forward leg would swing them through the listener.</item>
        /// <item><b>Overhead is folded towards the middle.</b> A stereo image carries a horizontal bearing and
        /// nothing else, so a shell almost directly above the lens has a bearing decided by a few units of
        /// horizontal offset — it would crack out of one speaker while the player is looking straight up at it.
        /// Scaling the sideways leg by the cosine of the elevation is what every height channel's downmix does,
        /// and it leaves anything near the horizon exactly where it is.</item>
        /// </list>
        /// </summary>
        private Vector3 Placed(Vector3 world, float widen)
        {
            Vector3 to = world - _listener.Position;

            float ahead = Vector3.Dot(to, _listenerForward);
            float side = Vector3.Dot(to, _listenerRight);
            float above = Vector3.Dot(to, _listenerUp);

            //Only ahead of the lens, and only when there is something to widen.
            if (widen > 1f && ahead > 0f) ahead /= widen;

            float length = to.Length();
            if (length > 1e-4f)
            {
                //cos(elevation) from sin, with no trigonometry: 1 on the horizon, 0 straight overhead.
                float sinElevation = MathHelper.Clamp(MathF.Abs(above) / length, 0f, 1f);
                side *= MathF.Sqrt(1f - sinElevation * sinElevation);
            }

            return _listener.Position + _listenerForward * ahead + _listenerRight * side + _listenerUp * above;
        }

        /// <summary>How far the sound is from the ear, in a straight line — the true distance, never the placed one.</summary>
        private float DistanceTo(Vector3 world) => Vector3.Distance(world, _listener.Position);

        /// <summary>Whether every component is a real number. The guard against a silent, permanent global mute.</summary>
        private static bool Finite(Vector3 v)
            => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

        #endregion

        /// <summary>Falls off gently with distance so a far landing is quieter but never inaudible.</summary>
        private static float VolumeForDistance(float distance)
        {
            const float MIN = 0.45f;
            const float FALLOFF = 60f;
            return MIN + (1f - MIN) * MathHelper.Clamp(1f - distance / FALLOFF, 0f, 1f);
        }

        /// <summary>
        /// A small random pitch offset for a play, so a burst of the same effect never sounds flat.
        /// <para>
        /// <b>Centred on ZERO, and that is not a detail.</b> <see cref="SoundEffect.Play(float, float, float)"/>
        /// takes its pitch as an offset in OCTAVES over −1…1, where 0 is the sound as baked and 1 is a full
        /// octave up. This used to return <c>1 + jitter</c> — written as if it were a frequency multiplier —
        /// which pinned every sound in the game at the top of that range: the shot, every landing, the whistle
        /// and the report all played <b>an octave above</b> what they were synthesized as. Everything sounded
        /// thin, toy-like and squeaky, and no amount of retuning the synthesis could have fixed it, because
        /// the synthesis was never what was wrong.
        /// </para>
        /// </summary>
        private float NextPitch(float amplitude) => (float)(_random.NextDouble() * 2.0 - 1.0) * amplitude;

        //How far a source may be before X3DAudio's own 1/d attenuation starts to bite. Enormous on purpose:
        //this is a PLATEAU, not an off switch, and it is what lets every distance law in this file survive the
        //move to positional audio unchanged. See the constructor for the measurement.
        private const float DISTANCE_PLATEAU = 10000f;

        //How much the near field's bearing is exaggerated — the cluster, the muzzle, a release. This replaces
        //PAN_FULL_WIDTH (18) and is the honest form of the same tuning: an angle is scale-invariant where a
        //sideways DISTANCE is not, which is why the old law needed a second constant for the sky and this does
        //not. At 1.9 the edge of the field lands within a fraction of a decibel of the image the mix has always
        //had. Set it to 1 for the plain geometric truth — which is narrower, and was tried.
        private const float NEAR_WIDEN = 1.9f;

        //And the sky's, which is none: a shell really is far out to one side, and the truth up there is already
        //wider than the old SKY_PAN_WIDTH (90) made it. Exaggerating it as well would blow the celebration open.
        private const float SKY_WIDEN = 1f;

        #region The signal chain

        /// <summary>
        /// The shot, layered like a real discharge rather than a single tone. A cannon going off is not one
        /// sound — it is a body of compressed air, the energy of the charge, the friction and the resistance —
        /// and each of those lives in a different part of the spectrum. This bakes five of them together so the
        /// shot reads as massive, present and exciting at <i>any</i> playback volume, which matters because a
        /// game is usually played quieter than a real cannon would be:
        /// <list type="bullet">
        /// <item><b>Boom</b> — a kick-drum pitch drop (150→60 Hz, fast) plus a low rumble. The weight: "boooom".
        /// Dominates the energy, and lives in the band ordinary speakers can reproduce.</item>
        /// <item><b>Zap</b> — a short buzzy sawtooth sweep (400→150 Hz). The energy of the charge itself: "vrrzzzzt".
        /// Adds midrange excitement that the boom alone does not have.</item>
        /// <item><b>Crackle</b> — high band-passed noise with a tremolo, trailing off. The air resistance and
        /// flying debris: "pšššchřchřchř". Crucially this sits in the 1.5–5 kHz band where the ear is most
        /// sensitive, so it keeps the shot audible and cutting even when the volume is turned down low.</item>
        /// <item><b>Thud</b> — the muzzle transient that opens the whole thing.</item>
        /// </list>
        /// Then a reverb tail so the layered report carries and decays like a discharge in a space.
        /// </summary>
        private SoundEffect BakeShoot()
        {
            const float duration = 0.65f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //Layer 1 — the boom: a fast pitch drop, not a slow sweep. The pitch envelope is the heart of why
            //this reads as a cannon rather than a tube: it crashes down in the first 30 ms and then sustains,
            //so the ear hears an attack followed by a ring instead of a note sliding down.
            const float pitchDropTime = 0.030f;
            const float pitchStart = 150f, pitchEnd = 60f;
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Pitch falls exponentially over the attack window, then holds at the low note and lets it ring.
                float freq = t < pitchDropTime
                    ? pitchStart * MathF.Pow(pitchEnd / pitchStart, t / pitchDropTime)
                    : pitchEnd;

                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                //A couple of harmonics for body, but few and quiet: this is not a bright tone.
                float boom = MathF.Sin(phase) + 0.30f * MathF.Sin(2f * phase) + 0.10f * MathF.Sin(3f * phase);

                //A slow amplitude decay so the note rings out and booms rather than going "plick" and vanishing.
                float amp = MathF.Exp(-t * 5f);

                signal[i] += boom * 0.6f * amp;
            }

            //The boom's grit: white noise low-passed to 190 Hz, which is the band that turns the clean tone above
            //into an explosive "rrrmmm". This band is also what small speakers can reproduce, so the weight
            //survives on monitor speakers where a sub-bass would not.
            float[] rumble = LowPassArray(MakeNoiseArray(samples), 190f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                signal[i] += rumble[i] * 0.55f * MathF.Exp(-t * 4.5f);
            }

            //Layer 2 — the zap: the energy of the charge itself, a short buzzy sawtooth sweep. A sawtooth built
            //additively (eight partials at 1/k) is what gives the buzzy "vrrzzzzt"; a sine here would just be
            //another tone. Kept short — a flash on top of the boom, not a drone — and modest in gain so it is an
            //accent that excites the midrange, never the main event.
            const float zapDuration = 0.09f;
            int zapSamples = Math.Min(samples, (int)(SAMPLE_RATE * zapDuration));
            float zapPhase = 0f;
            for (int i = 0; i < zapSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //Another fast pitch drop, but in a higher register (400→150 Hz) so it reads as the charge's whine
                //rather than the body's thump.
                float freq = 400f * MathF.Pow(150f / 400f, t / zapDuration);
                zapPhase += 2f * MathF.PI * freq / SAMPLE_RATE;

                float saw = 0f;
                for (int k = 1; k <= 8; k++) saw += MathF.Sin(k * zapPhase) / k;

                //A 4 ms attack ramp then a fast decay: a zap, not a sustained note.
                float env = MathF.Exp(-t * 22f) * MathF.Min(1f, t / 0.004f);
                signal[i] += saw * 0.18f * env;
            }

            //Layer 3 — the crackle: the air resistance and debris, a high band-passed noise with a tremolo. This
            //is the layer that makes the shot cut through at low volume: it lives in the 1.5–5 kHz band where the
            //ear is most sensitive (Fletcher-Munson), so even quiet it reads as present and exciting. The tremolo
            //at ~45 Hz is what turns steady hiss into the "chřchř" crackle texture.
            float[] air = BandPass(MakeNoiseArray(samples), 1500f, 5000f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tremolo = 0.55f + 0.45f * MathF.Sin(2f * MathF.PI * 45f * t);
                signal[i] += air[i] * 0.22f * tremolo * MathF.Exp(-t * 8f);
            }

            //Layer 4 — the muzzle thud: a short, low-cutoff noise burst that opens the whole thing, kept low so
            //it is a thud rather than the sharp click that made earlier passes read as cheap.
            AddNoiseBurst(signal, window: 0.05f, decay: 25f, gain: 1.0f, cutoff: 1200f);

            //Space: a longer reverb tail so the layered report carries and decays like a discharge in a space.
            ApplyReverb(signal, roomScale: 0.5f, wet: 0.34f, decay: 0.36f);

            //The cannon has the same shape as the firework's report — a crack over a body — so it takes the
            //same treatment and for the same reason: peak-normalising it to the muzzle transient is what kept
            //the boom underneath thin.
            Loudness(signal, targetRms: 0.27f, ceiling: 0.98f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// What a material does to a landing (#314). Every figure a <see cref="BakeLanded"/> has that is not the
        /// colour's note or the arena's room, in one place, so a material is one line of a table rather than a
        /// branch in the synthesis — and so an eleventh one is a row rather than an edit.
        /// <para>
        /// The three things the ear actually identifies a material by, in the order it uses them: <b>how long it
        /// rings</b> (wool is dead on contact, metal sings), <b>where its partials sit</b> (a struck bar's are
        /// inharmonic and beat, a wooden one's are harmonic and thicken), and <b>how bright and hard its contact
        /// is</b> (glass ticks, wool answers with a "pff"). Weight — the sub — is the fourth and it decides
        /// whether the thing has mass: a hollow shell has almost none, a lump of stone is mostly it.
        /// </para>
        /// </summary>
        private readonly struct LandedMaterial
        {
            //Multiplies the whole colour ladder. Small hard things ring high, heavy ones low; the STEP between
            //colours is untouched, which is what keeps the thirteen countable under every material.
            public readonly float PitchScale;

            //e^-t of this on the tone: the whole difference between a ring and a thud, and the figure the
            //buffer's own length is solved from.
            public readonly float RingDecay;

            //The two partials over the fundamental: how loud, and WHERE. Ratios of 2 and 3 are harmonic and
            //thicken the note into a body; anything irrational beats against the fundamental and reads as metal.
            public readonly float Partial2;
            public readonly float Partial3;
            public readonly float Ratio2;
            public readonly float Ratio3;

            //The sub an octave down: its share of the mix against the tone, and how fast it goes. This is MASS.
            public readonly float SubLevel;
            public readonly float SubDecay;

            //The contact itself: how long the noise burst lasts, how fast it dies, how loud it is, and what it
            //is low-passed to. The cutoff is the one the ear reads as HARDNESS.
            public readonly float ClickWindow;
            public readonly float ClickDecay;
            public readonly float ClickGain;
            public readonly float ClickCutoff;

            public LandedMaterial(float pitchScale, float ringDecay, float partial2, float partial3, float ratio2,
                float ratio3, float subLevel, float subDecay, float clickWindow, float clickDecay, float clickGain,
                float clickCutoff)
            {
                PitchScale = pitchScale;
                RingDecay = ringDecay;
                Partial2 = partial2;
                Partial3 = partial3;
                Ratio2 = ratio2;
                Ratio3 = ratio3;
                SubLevel = subLevel;
                SubDecay = subDecay;
                ClickWindow = clickWindow;
                ClickDecay = clickDecay;
                ClickGain = clickGain;
                ClickCutoff = clickCutoff;
            }
        }

        //How many materials there are. Off the enum itself, #152's lesson: a hand-pinned count is how a member
        //ends up existing everywhere except in the one array that has to be indexed by it.
        private static readonly int BallStyleCount = Enum.GetValues<BallStyle>().Length;

        /// <summary>
        /// What each material sounds like landing (#314). One row per <see cref="BallStyle"/>, each stated
        /// against the vinyl — which is the row that was there before this existed, unchanged, and is still what
        /// every unauthored map plays.
        /// <para>
        /// <b>Each is read off the material's own doc comment on <see cref="BallStyle"/> rather than invented
        /// here</b>, because those were written to say what the thing IS: the wool is "the soft one, and the only
        /// one", the bubble "a film around nothing", the marble "has mass", the metal "a turned, brushed alloy",
        /// the lava "a cooling lump". A sound that disagrees with the look is worse than one that is merely
        /// plain, and this is the whole of what keeps them agreeing.
        /// </para>
        /// </summary>
        private static LandedMaterial MaterialFor(BallStyle style) => style switch
        {
            //An air-filled vinyl skin: a mid thunk with a wooden harmonic body and real weight behind it. These
            //are the figures the sound shipped with and they are the reference every row below is stated from.
            BallStyle.Beach => new LandedMaterial(1.00f, 26f, 0.50f, 0.20f, 2f, 3f, 0.55f, 30f, 0.015f, 90f, 0.90f, 4000f),

            //A film around nothing: high, thin, and almost no sub, because there is no mass in a soap bubble to
            //make one. It rings a little because it is glass, and briefly because the shell is a film.
            BallStyle.Bubble => new LandedMaterial(1.90f, 15f, 0.35f, 0.45f, 2f, 3f, 0.14f, 46f, 0.008f, 130f, 0.95f, 9000f),

            //Stone with mass, which is the whole reason it exists beside the vinyl: pitched well down, mostly
            //sub, and its partials kept quiet - a dense solid answers with a dull thud and not a chord.
            BallStyle.Marble => new LandedMaterial(0.72f, 21f, 0.28f, 0.10f, 2f, 3f, 0.80f, 26f, 0.018f, 80f, 0.75f, 2600f),

            //THE SOFT ONE, and it must be the dullest and shortest thing in the set - a ball of yarn hitting
            //another ball of yarn barely sounds at all. Everything is turned down: it dies in a twentieth of a
            //second, has no partials worth the name, and its contact is a "pff" with the tick filtered out of it.
            BallStyle.Wool => new LandedMaterial(0.80f, 58f, 0.14f, 0.00f, 2f, 3f, 0.50f, 62f, 0.020f, 60f, 0.42f, 850f),

            //Anodised alloy, and the ONE row whose partials are inharmonic: 2.76 and 5.40 are a struck bar's
            //first two, so they beat against the fundamental instead of thickening it - which is what a bell is
            //and what no envelope alone can imitate. It sings far longer than anything else here.
            BallStyle.Metal => new LandedMaterial(1.45f, 6f, 0.60f, 0.35f, 2.76f, 5.40f, 0.22f, 34f, 0.006f, 150f, 1.00f, 12000f),

            //Frozen and brittle: bright and hard like glass, but opaque and solid rather than a film, so it
            //keeps some weight the bubble has none of and stops ringing sooner than the gem.
            BallStyle.Ice => new LandedMaterial(1.32f, 19f, 0.42f, 0.28f, 2f, 3f, 0.34f, 38f, 0.009f, 120f, 0.92f, 7000f),

            //A cut stone: small, hard and dense, so the highest ring in the set after the metal's and the
            //sharpest tick. Its partials sit harmonically - a gem is a lump of quartz, not a struck bar.
            BallStyle.Gem => new LandedMaterial(1.70f, 12f, 0.50f, 0.34f, 2f, 3f, 0.20f, 42f, 0.006f, 145f, 1.00f, 11000f),

            //A globe of ionised gas: the one material with no impact to speak of. Almost no sub, a soft short
            //body, and a wide dull contact - what carries it is a slightly detuned upper partial (2.05) that
            //beats a few times a second, which is the nearest a mono buffer gets to a crackle.
            BallStyle.Plasma => new LandedMaterial(1.25f, 32f, 0.26f, 0.16f, 2.05f, 3f, 0.12f, 50f, 0.014f, 70f, 0.55f, 2000f),

            //A cooling lump of rock, and the heaviest thing in the set: the lowest note, the most sub, and a
            //contact filtered almost to a thump. Nothing about lava is bright.
            BallStyle.Lava => new LandedMaterial(0.62f, 30f, 0.24f, 0.08f, 2f, 3f, 0.88f, 24f, 0.022f, 65f, 0.70f, 1500f),

            //Glazed ceramic: hard and high and RINGING - the closest thing here to a struck teacup, which is
            //what it is. Between the ice and the gem in pitch, and it holds its note longer than either.
            BallStyle.Porcelain => new LandedMaterial(1.55f, 10f, 0.55f, 0.30f, 2f, 3f, 0.26f, 40f, 0.007f, 140f, 0.98f, 8500f),

            //An unnamed material draws as vinyl (BallRenderSet's own default) and so sounds as vinyl. This arm
            //exists only because the compiler asks for it: BallStyles.TryParse cannot produce a value outside
            //the enum, and every member above is named.
            _ => MaterialFor(BallStyle.Beach)
        };

        /// <summary>
        /// A landing: a low "thunk" with harmonic content — one base note per ball type, so each colour lands on
        /// its own pitch — fronted by a filtered click of contact and underpinned by a sub thump. Shorter and
        /// duller than the shot: a ball meeting a lattice of its own kind should sound solid, not explosive.
        /// <para>
        /// The <paramref name="material"/> is what it is MADE of (#314), and it moves every figure below except
        /// the note and the room. The note stays the colour's alone, because it is the one thing the ear is
        /// asked to count; the room stays the arena's, because a room is not a property of the ball.
        /// </para>
        /// </summary>
        private SoundEffect BakeLanded(int type, in LandedMaterial material)
        {
            //Long enough that the ring has died into it rather than being cut off at the buffer's end, which is
            //a click. Five time constants is 0.7 % of the peak, and the bounds do the rest: the ceiling stops a
            //metal ball costing a megabyte a colour, and the FLOOR IS THE SHIPPED VINYL'S OWN 0.30 s - it is not
            //the ring that needs it but the REVERB TAIL applied over the whole buffer afterwards, and a dead
            //material solved from its ring alone (wool comes out at 0.086) would have its room cut off with it.
            //That floor is also what keeps the vinyl's row arithmetically identical to the sound that shipped.
            float duration = MathHelper.Clamp(5f / material.RingDecay, 0.30f, 0.80f);
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //One step per ball type across a low register, adjacent colours exactly a whole tone apart
            //(2^(1/6) per step) rather than a fraction the ear cannot tell apart. The step is the design
            //constant, not the span: thirteen types run 150-600 Hz, still a thunk's register, and another
            //type would simply extend the ladder. (The old ladder divided a fixed 1.5-octave span by the
            //type count in disguise - five more types would either have shrunk every step below audibility
            //or, with the divisor kept, pushed the top types out of the low register entirely.)
            //
            //PitchScale moves the WHOLE ladder and never its step, which is the one thing that must not change:
            //a material is constant for a level, so transposing it costs the colours nothing, while compressing
            //the step would cost the ear the count it is actually being asked for.
            const float root = 150f;
            float freq = root * material.PitchScale * MathF.Pow(2f, (type - 1) / 6f);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = MathF.Exp(-t * material.RingDecay);

                //Additive partials: the fundamental plus two more. WHERE they sit is the material's, and it is
                //most of what says metal rather than stone - a struck bar's partials are inharmonic, so they
                //beat against the fundamental instead of thickening it, and no envelope makes a harmonic stack
                //sound like a bell.
                float tone = 0f;
                tone += MathF.Sin(2f * MathF.PI * freq * t);
                tone += material.Partial2 * MathF.Sin(2f * MathF.PI * freq * material.Ratio2 * t);
                tone += material.Partial3 * MathF.Sin(2f * MathF.PI * freq * material.Ratio3 * t);

                //A sub thump an octave below the fundamental adds the physical weight of contact - which is
                //exactly what a hollow shell has none of and a lump of stone is mostly made of.
                float sub = MathF.Sin(2f * MathF.PI * freq * 0.5f * t) * MathF.Exp(-t * material.SubDecay);

                signal[i] = (tone * (1f - material.SubLevel) * env) + (sub * material.SubLevel);
            }

            //The click of contact, and the other half of what the ear identifies a material by: a hard surface
            //ticks bright and short, a soft one answers with a dull "pff" and almost no tick at all.
            AddNoiseBurst(signal, material.ClickWindow, material.ClickDecay, material.ClickGain, material.ClickCutoff);

            //The room, and it is the ARENA's rather than the ball's: thirteen colours and ten materials all land
            //in the same place, so this is the one figure here that no material may move.
            ApplyReverb(signal, roomScale: 0.4f, wet: 0.26f, decay: 0.22f);

            Normalize(signal, 0.9f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// The menu's click: a short, dry "thock" — a felt hammer on wood, not a beep. Three parts, all inside
        /// 90 ms: a low-passed noise knock (the contact), a fast 900→420 Hz pitch drop (the body — a tone held
        /// at one frequency here reads as a beep, the landing's attack-then-ring idea at a tenth of the scale),
        /// and a touch of 150 Hz weight so the press lands rather than taps. <b>Dry on purpose</b>, the party
        /// popper's reasoning: the UI lives at the player's hand, not out in the scene, so it gets no reverb
        /// and no pan. One buffer serves all three UI sounds — the step and the back are this, repitched (see
        /// <see cref="PlayUiTick"/>/<see cref="PlayUiBack"/>).
        /// </summary>
        private SoundEffect BakeUiClick()
        {
            const float duration = 0.09f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                //The body: the pitch crashes down over the first 18 ms and then rings out — briefly.
                float freq = 900f * MathF.Pow(420f / 900f, MathF.Min(1f, t / 0.018f));
                phase += 2f * MathF.PI * freq / SAMPLE_RATE;
                signal[i] += MathF.Sin(phase) * 0.5f * MathF.Exp(-t * 70f);

                //The weight: a quiet sub bump, gone almost at once.
                signal[i] += MathF.Sin(2f * MathF.PI * 150f * t) * 0.25f * MathF.Exp(-t * 90f);
            }

            //The contact: a very short knock, low-passed so it is a "th" rather than a "ts".
            AddNoiseBurst(signal, window: 0.008f, decay: 160f, gain: 0.8f, cutoff: 3200f);

            Normalize(signal, 0.9f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// A star landing on the result screen: a small struck chime — bright, clean, and over quickly enough
        /// that four of them in a row is a run rather than a chord.
        /// <list type="bullet">
        /// <item><b>Inharmonic partials, not a harmonic stack.</b> Struck metal rings at ratios near a free
        /// bar's (1, 2.76, 5.40, 8.93) and not at whole multiples of the root, and that is the whole
        /// difference between a bell and an organ pipe — a harmonic stack here read as a game-show buzzer.</item>
        /// <item><b>The high partials die first</b>, as they do in real metal, so the sound is bright at the
        /// strike and mellows as it rings out instead of hissing evenly all the way down.</item>
        /// <item><b>Two milliseconds of noise at the front</b> is the hammer meeting the metal. Without it the
        /// tone fades <i>up</i> out of silence however steep the envelope is, which is the one thing that
        /// gives a synthesised bell away.</item>
        /// </list>
        /// </summary>
        private SoundEffect BakeStarEarned()
        {
            const float duration = 0.8f;
            const float root = 880f;

            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //TUBULAR BELL, not a free bar (#158). The first version used a free bar's ratios (1, 2.76, 5.40,
            //8.93) because they are what "struck metal" means acoustically — and that was right for the sound
            //this was designed to make, which was one played into SILENCE. It is not played into silence: the
            //victory fanfare is still going when the result screen appears, and a stack of inharmonic partials
            //over a tonal piece is dissonant in whatever key that piece rolled. A tubular bell's partials are
            //very nearly 1:2:3, which is the same spectrum a note has, so it can sit inside harmony; the 4.2
            //on top is the one inharmonic term and is what keeps it a bell rather than an organ pipe.
            float[] ratios = { 1f, 2f, 3f, 4.2f };
            float[] gains = { 1f, 0.5f, 0.26f, 0.12f };
            float[] decays = { 5.5f, 8f, 12f, 17f };

            for (int partial = 0; partial < ratios.Length; partial++)
            {
                float frequency = root * ratios[partial];

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SAMPLE_RATE;
                    signal[i] += MathF.Sin(2f * MathF.PI * frequency * t) * gains[partial] * MathF.Exp(-t * decays[partial]);
                }
            }

            //The hammer: a very short, bright tick, so the tone is struck rather than faded up
            AddNoiseBurst(signal, window: 0.002f, decay: 400f, gain: 0.35f, cutoff: 9000f);

            Normalize(signal, 0.9f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// A group coming loose: the snap of the lattice letting go, then the freed balls popping away one
        /// after another — a quick rising run of six pops. The pops are <b>band-passed noise, not tones</b>,
        /// and that is deliberate: the level theme transposes itself per pass (see <c>ProceduralMusic</c>), so
        /// a melodic run in any fixed key would land wrong against half of them, where a noise pop's "pitch"
        /// is a gesture the ear reads against nothing. The run rises (release reads as reward, even though the
        /// balls fall), accelerates slightly, and trails off — the group receding as it goes.
        /// </summary>
        private SoundEffect BakeRelease()
        {
            const float duration = 0.6f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //The snap at the front: one sharp mid-band crack — the constraints letting go — with a low thump
            //under it for the body of the break.
            AddNoiseBurst(signal, window: 0.012f, decay: 120f, gain: 1.0f, cutoff: 5500f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                signal[i] += MathF.Sin(2f * MathF.PI * 140f * t) * 0.3f * MathF.Exp(-t * 40f);
            }

            //The run: six pops, each a band-passed noise burst with a small sine blip at its centre for body.
            //Centres climb, gaps shrink a touch (an accelerando reads as "away", a ritardando as "stopping"),
            //and the last pops sit a little lower in level.
            float[] noise = MakeNoiseArray(samples);
            float[] centres = { 800f, 1100f, 1450f, 1850f, 2300f, 2800f };
            float at = 0.06f;

            for (int pop = 0; pop < centres.Length; pop++)
            {
                float centre = centres[pop];
                float[] band = BandPass(noise, centre * 0.7f, centre * 1.3f);

                int start = (int)(at * SAMPLE_RATE);
                float gain = 1f - 0.09f * pop;

                for (int i = start; i < samples; i++)
                {
                    float t = (float)(i - start) / SAMPLE_RATE;
                    if (t > 0.12f) break;

                    //A 3 ms attack then a fast decay: a pop, not a hiss.
                    float env = MathF.Exp(-t * 60f) * MathF.Min(1f, t / 0.003f);

                    signal[i] += band[i] * 0.55f * gain * env;
                    signal[i] += MathF.Sin(2f * MathF.PI * centre * t) * 0.18f * gain * env;
                }

                at += 0.062f - 0.004f * pop;
            }

            //In the scene, so it takes the landing's kind of space — a shade drier, being several small events
            //rather than one solid contact.
            ApplyReverb(signal, roomScale: 0.4f, wet: 0.22f, decay: 0.25f);

            Normalize(signal, 0.9f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// The shell going up: the rising whistle every firework opens with. It is the one sound here that is
        /// almost a pure TONE — a whistling shell is a resonant cavity, not an explosion — and that is what
        /// makes it read against a scene full of broadband noise.
        /// <list type="bullet">
        /// <item><b>The rise is the whole effect.</b> The pitch climbs 620 → 1950 Hz over the flight, which is
        /// what says "going up" without anything on screen having to. It is eased rather than linear so it
        /// slows as the shell does.</item>
        /// <item><b>Vibrato, or it reads as a test tone.</b> A few per cent of wobble at ~7 Hz is the shell
        /// spinning; dead-steady pitch sounds synthetic in a way no amount of harmonics fixes.</item>
        /// <item><b>A thin noise bed</b> band-passed around the tone: the air over the case. Quiet, but
        /// without it the whistle sits outside the scene rather than in it.</item>
        /// </list>
        /// </summary>
        private SoundEffect BakeFireworkLaunch()
        {
            //SHORT. A launch is a shell leaving a tube, not a vehicle going past: at over a second the tone
            //had time to be heard AS a tone, and a sustained tone sweeping slowly upward is a siren.
            const float duration = 0.55f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //HIGH, and this was got wrong once in each direction. It began at 620→1950 Hz, was reported as
            //squeaky and taken down to 430→1320 — but the squeak was never the synthesis, it was the octave
            //bug in NextPitch (see there), and with that fixed the lowered tone sat in the register of a horn
            //rather than a firework. A whistling shell is genuinely piercing; up here it reads as one, and it
            //is the LEVEL rather than the pitch that keeps it from being shrill.
            const float startHz = 900f, endHz = 2600f;
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float u = t / duration;

                //Eased rise: fast at first and flattening, like a shell losing speed against gravity.
                float climb = 1f - (1f - u) * (1f - u);
                float freq = startHz + (endHz - startHz) * climb;

                //The spin — a SHIMMER, not a wobble. At 2.8 % and 7 Hz this was a slow, wide warble that read
                //as "vu-vu-vu-vu" rather than as a whistle: at that rate the ear tracks each swing as its own
                //note instead of hearing one tone with a texture. Shallow and fast is what a spinning shell
                //actually does to a tone.
                freq *= 1f + 0.006f * MathF.Sin(2f * MathF.PI * 16f * t);

                phase += 2f * MathF.PI * freq / SAMPLE_RATE;

                //A whistle is nearly a sine; a little second and third keep it from being a lab tone.
                float tone = MathF.Sin(phase) + 0.22f * MathF.Sin(2f * phase) + 0.06f * MathF.Sin(3f * phase);

                //Straight in and away almost at once. The old envelope held near full for most of a second,
                //which is what let the ear settle on the pitch and hear a siren; fading it from the start
                //means the tone is a departure rather than a note.
                float env = MathF.Min(1f, t / 0.012f) * MathF.Pow(1f - u, 1.6f);

                signal[i] += tone * 0.5f * env;
            }

            //The fizz, and it now carries the launch rather than accompanying it. A firework leaving the
            //ground is a burning fuse and a jet of gas before it is a tone at all — this is the sparkler the
            //whole thing actually is, and pushing the balance this way is what stops the launch being a horn
            //with a hiss on top. Band-passed high, where a spitting fuse lives.
            float[] air = BandPass(MakeNoiseArray(samples, seed: 7717), 1400f, 9000f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float u = t / duration;

                //Thickest at the start, where the motor is doing the most work, and thinning as it climbs
                //away — the reverse of the tone, which is what makes the two read as one object.
                float env = MathF.Min(1f, t / 0.008f) * MathF.Pow(1f - u, 1.2f);
                signal[i] += air[i] * 0.55f * env;
            }

            //Barely any room on this one: the shell is climbing away into open sky, and a long tail on a rising
            //tone smears the pitch into a chord.
            ApplyReverb(signal, roomScale: 0.55f, wet: 0.12f, decay: 0.16f);

            Normalize(signal, 0.92f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// The shell going off, in three parts, because a real report is three things arriving together and
        /// leaving at different rates:
        /// <list type="bullet">
        /// <item><b>The crack</b> — a very short, bright, barely-filtered noise transient. This is the part the
        /// ear times the event by, and it has to be the first sample: any attack ramp at all turns a bang into
        /// a whoomph.</item>
        /// <item><b>The boom</b> — a fast 110 → 38 Hz pitch drop under it, carrying the weight. The same trick
        /// the cannon's boom uses, an octave lower and slower to decay, because this one is meant to sound big
        /// and far away rather than close and sharp.</item>
        /// <item><b>The crackle</b> — the burning stars, and the longest-lived of the three: high band-passed
        /// noise gated by a fast random stutter so it breaks into individual pops rather than hissing. It
        /// outlasts the boom by half a second, which is exactly what makes a firework sound like a firework
        /// and not like a gunshot.</item>
        /// </list>
        /// A long, wet reverb over the lot: this happens a hundred units up over an open scene.
        /// </summary>
        private SoundEffect BakeFireworkBurst()
        {
            const float duration = 2.6f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //The body. Deep, and it does NOT decay quickly — a big shell's report is felt for the best part of
            //a second before the roll takes over. Two oscillators an octave apart rather than one with a
            //harmonic: the sub carries the chest thump and the fundamental carries the pitch, and keeping them
            //separate lets the sub outlast the note, which is what large explosions do.
            const float pitchDropTime = 0.07f;
            const float pitchStart = 128f, pitchEnd = 33f;
            float phase = 0f, subPhase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;

                float freq = t < pitchDropTime
                    ? pitchStart * MathF.Pow(pitchEnd / pitchStart, t / pitchDropTime)
                    : pitchEnd;

                phase += 2f * MathF.PI * freq / SAMPLE_RATE;
                subPhase += 2f * MathF.PI * (freq * 0.5f) / SAMPLE_RATE;

                signal[i] += MathF.Sin(phase) * 0.85f * MathF.Exp(-t * 2.6f);
                signal[i] += MathF.Sin(subPhase) * 0.95f * MathF.Exp(-t * 1.9f);
            }

            //The blast's own noise, low-passed hard so it is pressure rather than hiss. This is most of what
            //makes the bang read as an explosion instead of as a drum.
            float[] blast = LowPassArray(MakeNoiseArray(samples, seed: 4231), 220f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                signal[i] += blast[i] * 0.9f * MathF.Exp(-t * 2.2f);
            }

            //The crack: the first few milliseconds, broadband and with no attack ramp at all. Any ramp turns a
            //bang into a whoomph, and this is the part the ear times the whole event by.
            AddNoiseBurst(signal, window: 0.03f, decay: 85f, gain: 1.5f, cutoff: 11000f);

            //The crackling stars. The stutter is what separates this from hiss: a fast, coarse random gate
            //held for a few milliseconds at a time, so the tail breaks into countable little pops.
            float[] stars = BandPass(MakeNoiseArray(samples, seed: 9091), 1700f, 6500f);
            const int holdSamples = 220;   //~5 ms per gate step
            float gate = 0f;

            for (int i = 0; i < samples; i++)
            {
                if (i % holdSamples == 0) gate = Noise(i / holdSamples, 5150) > 0.15f ? 1f : 0.15f;

                float t = (float)i / SAMPLE_RATE;

                //Starts a hair after the crack (the stars have to be thrown before they burn) and decays
                //slowest of everything here. Kept well under the body: the stars garnish the bang, and when
                //they lead it the whole thing reads as a sparkler rather than as a shell.
                float env = MathF.Min(1f, t / 0.04f) * MathF.Exp(-t * 1.7f);
                signal[i] += stars[i] * 0.22f * gate * env;
            }

            //The roll: the report coming back off everything around. This, more than the reverb, is what puts
            //the burst over a landscape instead of in a box.
            //Nine taps starting at 23 ms and stretching by an irrational ratio each time: dense enough at the
            //front to fuse into the bang itself, thinning into a rumble behind it.
            RollingEcho(signal, taps: 9, firstDelaySeconds: 0.023f, spread: 1.37f, feedback: 0.78f, mix: 0.42f);

            ApplyReverb(signal, roomScale: 1.0f, wet: 0.42f, decay: 0.7f);

            //Driven and softly saturated rather than peak-normalised — see Loudness. Normalising this to its
            //crack is exactly what left it a click with a thud behind it.
            Loudness(signal, targetRms: 0.30f, ceiling: 0.99f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// The party popper that opens the celebration: a dry paper crack and the rustle of confetti after it.
        /// Deliberately close and dry where the shells are big and wet — it is the one sound in the celebration
        /// that happens in the room rather than in the sky, which is what makes the shells read as distant.
        /// </summary>
        private SoundEffect BakePartyPopper()
        {
            const float duration = 0.55f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] signal = new float[samples];

            //The crack of the paper: bright, hard and over almost at once.
            AddNoiseBurst(signal, window: 0.02f, decay: 130f, gain: 1.0f, cutoff: 9000f);

            //A short body under it so it has a pop rather than a click.
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float freq = 220f * MathF.Pow(0.45f, t / 0.05f);
                signal[i] += MathF.Sin(2f * MathF.PI * freq * t) * 0.45f * MathF.Exp(-t * 40f);
            }

            //Confetti: a fine, dry rustle falling away. High and quiet, no reverb of its own to speak of.
            float[] confetti = BandPass(MakeNoiseArray(samples, seed: 2718), 3200f, 9000f);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                signal[i] += confetti[i] * 0.20f * MathF.Exp(-t * 6.5f) * MathF.Min(1f, t / 0.02f);
            }

            ApplyReverb(signal, roomScale: 0.35f, wet: 0.18f, decay: 0.18f);

            //Driven rather than peak-normalised, for the reason the burst is: this is a transient with a tail,
            //and scaling it by its own crack leaves the crack loud and everything under it inaudible.
            Loudness(signal, targetRms: 0.24f, ceiling: 0.98f);
            return ToSoundEffect(signal);
        }

        /// <summary>
        /// Mixes a short burst of low-passed noise into <paramref name="signal"/> at its start. White noise
        /// alone hisses; the one-pole low-pass rounds it into a transient with mass.
        /// </summary>
        private static void AddNoiseBurst(float[] signal, float window, float decay, float gain, float cutoff)
        {
            int windowSamples = (int)(SAMPLE_RATE * window);
            if (windowSamples > signal.Length) windowSamples = signal.Length;

            //One-pole low-pass: a single weighted average of the previous output, cheap and stable enough for a
            //transient. The coefficient comes from the cutoff.
            float dt = 1f / SAMPLE_RATE;
            float rc = 1f / (2f * MathF.PI * cutoff);
            float alpha = dt / (rc + dt);
            float prev = 0f;

            for (int i = 0; i < windowSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float white = Noise(i);

                //Filter the noise in place before mixing it in.
                prev += alpha * (white - prev);
                float filtered = prev;

                signal[i] += filtered * gain * MathF.Exp(-t * decay);
            }
        }

        /// <summary>
        /// A one-pole low-pass applied to a whole array, returning a new filtered array. Used to shape a noise
        /// layer into a low rumble: white noise low-passed to a few hundred Hz stops hissing and starts to boom.
        /// </summary>
        private static float[] LowPassArray(float[] input, float cutoff)
        {
            float dt = 1f / SAMPLE_RATE;
            float rc = 1f / (2f * MathF.PI * cutoff);
            float alpha = dt / (rc + dt);

            float[] output = new float[input.Length];
            float prev = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                prev += alpha * (input[i] - prev);
                output[i] = prev;
            }

            return output;
        }

        /// <summary>An array of deterministic white noise, for layers that need a sustained (if brief) noise source.</summary>
        private static float[] MakeNoiseArray(int samples) => MakeNoiseArray(samples, seed: 0);

        /// <summary>
        /// <inheritdoc cref="MakeNoiseArray(int)"/>
        /// <para>
        /// The seed matters whenever one bake mixes <b>two</b> noise layers: <see cref="Noise(int, int)"/> is a
        /// function of the sample index, so two unseeded layers are the SAME sequence and only differ by how
        /// they were filtered. Summing two filtered copies of one noise sequence is correlated — it thins out
        /// and starts to sound like a single filtered source rather than like two separate things happening.
        /// </para>
        /// </summary>
        private static float[] MakeNoiseArray(int samples, int seed)
        {
            float[] noise = new float[samples];
            for (int i = 0; i < samples; i++) noise[i] = Noise(i, seed);
            return noise;
        }

        /// <summary>
        /// A one-pole band-pass: low-pass to <paramref name="hi"/> then subtract the low-passed-to-<paramref name="lo"/>
        /// residue, isolating the band between them. Used to confine a noise layer to the ear's most sensitive
        /// range for presence at low playback volume.
        /// </summary>
        private static float[] BandPass(float[] input, float lo, float hi)
        {
            float[] lower = LowPassArray(input, hi);
            float[] band = LowPassArray(lower, lo);

            float[] output = new float[input.Length];
            for (int i = 0; i < input.Length; i++) output[i] = lower[i] - band[i];
            return output;
        }

        /// <summary>
        /// A Schroeder reverb — four parallel comb filters summed and passed through two series all-pass filters
        /// (the Freeverb topology). The combs build the dense tail; the all-passes smear it so it reads as
        /// ambience rather than distinct echoes. Processed in place, adding a "wet" tail to the dry signal.
        /// </summary>
        private static void ApplyReverb(float[] signal, float roomScale, float wet, float decay)
        {
            int length = signal.Length;

            //Comb and all-pass delay lengths in samples (prime-ish, from Freeverb's constants), scaled by the
            //room so a smaller space claps back sooner.
            int[] combDelays =
            {
                (int)(1116 * roomScale), (int)(1188 * roomScale),
                (int)(1277 * roomScale), (int)(1356 * roomScale)
            };
            int[] allpassDelays = { (int)(556 * roomScale), (int)(441 * roomScale) };

            //Feedback gain derived from the decay time: longer tail, higher feedback — but capped short of the
            //point where the comb rings forever.
            float feedback = MathHelper.Clamp(0.7f + decay * 0.25f, 0.5f, 0.84f);

            float[] wetSignal = new float[length];

            //Each comb is a feedback delay line; their staggered lengths are what make the tail dense rather
            //than a single echo.
            foreach (int delay in combDelays)
            {
                if (delay < 1) continue;
                float[] buffer = new float[delay];
                int idx = 0;
                for (int i = 0; i < length; i++)
                {
                    float output = buffer[idx];
                    buffer[idx] = signal[i] + output * feedback;
                    if (++idx >= delay) idx = 0;
                    wetSignal[i] += output;
                }
            }

            //Average the four combs before the all-passes colour them.
            float combScale = 0.25f;
            for (int i = 0; i < length; i++) wetSignal[i] *= combScale;

            //Two all-passes in series smear the comb output into an even wash.
            foreach (int delay in allpassDelays)
            {
                if (delay < 1) continue;
                float[] buffer = new float[delay];
                int idx = 0;
                for (int i = 0; i < length; i++)
                {
                    float buffered = buffer[idx];
                    float output = -wetSignal[i] + buffered;
                    buffer[idx] = wetSignal[i] + buffered * 0.5f;
                    if (++idx >= delay) idx = 0;
                    wetSignal[i] = output;
                }
            }

            //Dry/wet mix. The wet path is scaled down a touch — it is ambience, not the main event.
            float dryGain = 1f - wet * 0.5f;
            for (int i = 0; i < length; i++)
                signal[i] = signal[i] * dryGain + wetSignal[i] * wet;
        }

        /// <summary>
        /// Scales <paramref name="signal"/> so its peak magnitude reaches <paramref name="target"/>. Prevents the
        /// layered sources from clipping when summed, and keeps every effect at a comparable loudness.
        /// </summary>
        private static void Normalize(float[] signal, float target)
        {
            float peak = 0f;
            for (int i = 0; i < signal.Length; i++)
            {
                float a = MathF.Abs(signal[i]);
                if (a > peak) peak = a;
            }

            if (peak < 1e-6f) return;
            float scale = target / peak;
            for (int i = 0; i < signal.Length; i++) signal[i] *= scale;
        }

        /// <summary>
        /// Drives <paramref name="signal"/> to a target RMS and then saturates it softly to
        /// <paramref name="ceiling"/> through a <c>tanh</c>. What a big report needs instead of
        /// <see cref="Normalize"/>.
        /// <para>
        /// <b>Peak normalisation is exactly wrong for an explosion.</b> Its loudest sample is the crack — a few
        /// milliseconds tall and thin — so scaling the peak to 1 scales the <i>body</i> down by whatever that
        /// transient happened to reach, and what is left is a tick with a quiet thud behind it. That is the
        /// difference between a bang and a click, and it is why every explosion sound effect ever made is
        /// compressed rather than normalised. Driving the RMS instead sets how loud the sound actually IS, and
        /// the tanh rounds the transient off rather than clipping it square.
        /// </para>
        /// </summary>
        private static void Loudness(float[] signal, float targetRms, float ceiling)
        {
            double sum = 0.0;
            for (int i = 0; i < signal.Length; i++) sum += signal[i] * (double)signal[i];

            float rms = (float)Math.Sqrt(sum / Math.Max(1, signal.Length));
            if (rms < 1e-6f) return;

            float drive = targetRms / rms;
            for (int i = 0; i < signal.Length; i++) signal[i] = MathF.Tanh(signal[i] * drive) * ceiling;
        }

        /// <summary>
        /// Slap-back repeats: a few discrete, progressively darker and quieter copies of the signal delayed
        /// behind it. This — not the reverb — is what makes a firework sound like it went off <i>over</i>
        /// something: the report reaches you once through the air and then again off every building, hillside
        /// and water surface around, which the ear reads as a long rolling rumble after the bang. A reverb
        /// alone gives a smooth wash and no roll.
        /// </summary>
        private static void RollingEcho(float[] signal, int taps, float firstDelaySeconds, float spread, float feedback, float mix)
        {
            float[] dry = (float[])signal.Clone();

            //Delays are spaced by an IRRATIONAL ratio and start inside the ear's fusion window. Both matter,
            //and getting them wrong is audible immediately: at four taps 60-85 ms apart the ear resolves each
            //one as its own event and the report comes out as "bum-bum-bum-bum" — a flutter echo, the sound of
            //a corridor, not of a firework over a city. Under ~40 ms and closely spaced they fuse into one
            //event with a tail, and an irrational spacing stops the repeats reinforcing into a pitch.
            for (int tap = 1; tap <= taps; tap++)
            {
                int delay = (int)(SAMPLE_RATE * firstDelaySeconds * MathF.Pow(spread, tap - 1));
                if (delay >= signal.Length) break;

                //Scaled by mix as well as by the decay, and that is what keeps the ATTACK at the front. The
                //first taps land 20-60 ms behind the crack, so at a high gain they add a second wavefront on
                //top of it: the envelope stops peaking on the first sample and climbs to a maximum a tenth of
                //a second in, which the ear hears as a soft "whoomph" instead of a hard "BUM". The roll has to
                //sit clearly UNDER the direct report, not double it.
                float gain = mix * MathF.Pow(feedback, tap);

                //Each repeat is darker than the last: air and distance take the top off first, which is what
                //makes the roll recede rather than just repeat.
                float[] tapSignal = LowPassArray(dry, 2600f / (1f + tap * 0.55f));

                for (int i = delay; i < signal.Length; i++) signal[i] += tapSignal[i - delay] * gain;
            }
        }

        /// <summary>Wraps a float signal as a 16-bit little-endian PCM mono <see cref="SoundEffect"/>.</summary>
        private static SoundEffect ToSoundEffect(float[] signal)
        {
            byte[] pcm = new byte[signal.Length * 2];
            for (int i = 0; i < signal.Length; i++)
            {
                float s = MathHelper.Clamp(signal[i], -1f, 1f);
                short v = (short)(s * short.MaxValue);
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }

            return new SoundEffect(pcm, SAMPLE_RATE, AudioChannels.Mono);
        }

        /// <summary>A cheap deterministic noise source for transients — quality is irrelevant for a few ms of crackle.</summary>
        private static float Noise(int i) => Noise(i, 0);

        /// <summary><inheritdoc cref="Noise(int)"/> <paramref name="seed"/> gives an independent sequence — see <see cref="MakeNoiseArray(int, int)"/>.</summary>
        private static float Noise(int i, int seed)
        {
            uint h = (uint)(i * 2654435761u) ^ 0x9E3779B9u ^ (uint)(seed * 374761393);
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;
            return (h / (float)uint.MaxValue) * 2f - 1f;
        }

        #endregion

        public void Dispose()
        {
            //Voices first, buffers second: an instance holds a voice onto the buffer it was made from.
            _shootRing?.Dispose();
            if (_landedRings != null)
                foreach (VoiceRing[] row in _landedRings)
                    if (row != null) foreach (VoiceRing ring in row) ring?.Dispose();
            _releaseRing?.Dispose();
            _launchRing?.Dispose();
            _burstRing?.Dispose();

            _shoot?.Dispose();
            if (_landed != null)
                foreach (SoundEffect[] row in _landed)
                    if (row != null) foreach (SoundEffect effect in row) effect?.Dispose();
            _release?.Dispose();
            _fireworkLaunch?.Dispose();
            _fireworkBurst?.Dispose();
            _partyPopper?.Dispose();
            _uiClick?.Dispose();
            _starEarned?.Dispose();
        }
    }
}
