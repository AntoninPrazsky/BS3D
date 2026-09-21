using Prazsky.BS3D.Levels;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BS3D.Screens
{
    /// <summary>
    /// The tutorial (#189): what the first chapter teaches, <b>one thing at a time</b>, as a card over the play
    /// HUD — a keycap or a button drawn from the prompt font, a line of what to do with it, and a word of
    /// praise when it is done. The game taught nothing before this; the whole of its controls was one
    /// sentence on the About page, and a player who downloads the release has never read it.
    /// <para>
    /// <b>It is a ladder across the chapter, not a lecture on level one.</b> The owner's brief was to teach
    /// slowly — a player buried under instructions in the first minutes is a player who stops reading them —
    /// so each lesson has a level it first becomes eligible at (<see cref="Definition.FromLevel"/>), and the
    /// opener carries only the three that make the game a game: aim, fire, three of a colour fall. Precise aim
    /// waits for the second level, walking the gun round for the third, stepping it in for the fourth, and the
    /// later levels of the chapter are left to be played. A lesson the player did not get to — the level
    /// lost, the card timed out, the event never happened — follows them into the next level of the chapter,
    /// so nothing is ever skipped for good; it is only ever deferred. Past the chapter's last level nothing is
    /// offered at all: the chapter <i>is</i> the tutorial, which is what the owner asked for.
    /// </para>
    /// <para>
    /// <b>Two kinds of lesson, told apart by how they end.</b> An <see cref="Definition.Action"/> lesson stays
    /// up until the game reports the thing was done — the barrel actually moved, a shot actually left, a group
    /// actually fell — and is then <b>joined</b> by its praise word, which is what makes it feel like a game
    /// rather than a manual: the card is a small dare, and doing the thing wins it. Joined rather than replaced
    /// since #466, and held to <see cref="MIN_READ_SECONDS"/> whatever happens: the card used to flip on the
    /// frame of the action and take its detail line with it, so the lessons a player does fastest — which are
    /// the first ones they meet — showed their instruction for a fraction of a second. It is never blocking — the gun answers
    /// throughout — and it gives up after <see cref="ACTION_TIMEOUT"/> rather than nagging for the whole level,
    /// coming back on the next one. An informational lesson (the glass, the streak, the line, the budget) has
    /// nothing to wait for, so it holds <see cref="INFO_SECONDS"/> and goes. Three of those are
    /// <see cref="Definition.Contextual"/>: armed at the level's start and shown only when their event fires —
    /// the glass stepping, the streak lighting, the floor alarm coming on — because "the glass steps down
    /// every eight shots" means something on the frame the glass has just stepped and nothing a minute before.
    /// A contextual card interrupts whatever card is up, which goes back to the front of the queue behind it.
    /// </para>
    /// <para>
    /// <b>Taught once, ever, and the save is what remembers</b> (<see cref="PlayerProgress.Lessons"/>): a lesson
    /// completed — the action done, or the informational card read out in full — is written there and never
    /// offered again, so replaying the opener for a better rating is not a second tutorial, and neither is a
    /// second launch. A retry inside one run remembers the same way through <see cref="_taughtThisRun"/>, which
    /// also carries the <c>tutorial</c> argument's run, where nothing is written (see <see cref="_force"/>). The
    /// settings row (<c>BS3DGame.IsTutorialEnabled</c>, on by default) is read every frame, so switching it off
    /// from the pause takes the card down at once and switching it back on resumes where it was; Reset
    /// progress clears the record with the stars, so a fresh start is taught afresh.
    /// </para>
    /// <para>
    /// <b>The device decides the picture.</b> The last device the player touched (<see cref="NoteDevice"/>)
    /// picks a keycap-and-mouse card or a gamepad one, live, so a player who picks up the pad mid-card sees
    /// the trigger and not the mouse button. Every gamepad prompt names a binding that exists: the left stick's
    /// traverse and walk were added with this feature (<c>GameplayScreen.UpdateInput</c>), because a card
    /// promising A/D to a pad player would have been a card lying.
    /// </para>
    /// <para>
    /// This class owns the rules and the state and draws nothing; <c>PlayHud.DrawTutorial</c> draws what it
    /// reads here, the way the HUD is handed the score keeper. It allocates nothing per frame: the captions
    /// are literals, the one formatted string (the glass's cadence) is built once per level.
    /// </para>
    /// </summary>
    internal sealed class Tutorial
    {
        internal enum Lesson { Aim, Fire, Match, LeanIn, Ceiling, Line, Traverse, Walk, Combine, Streak, Budget, LineRule, Graduated }

        /// <summary>What the player's hand was last on, which is what the card draws for.</summary>
        internal enum Device { KeyboardMouse, Gamepad }

        /// <summary>
        /// How the tutorial is run. <see cref="Normal"/> is every player's: gated on the save, recorded to it.
        /// The other two are the <c>tutorial</c> argument's, testing only, and both record nothing.
        /// <see cref="Force"/> offers every lesson as if none had been taught, with the real detection — a
        /// player with a finished save can be shown the cards again by playing. <see cref="Demo"/> is a reel:
        /// every lesson eligible whatever the level, the contextual ones queued like the rest, and each card
        /// counting itself done after <see cref="DEMO_SECONDS"/> — so all ten can be photographed in one run of
        /// a game nothing can press a key in (see the repository's note on synthetic input), which is the
        /// <c>celebrate</c> reasoning for a state a script cannot otherwise reach.
        /// </summary>
        internal enum Mode { Normal, Force, Demo }

        private enum Phase { None, Arriving, Shown, Praising, Leaving }

        private sealed class Definition
        {
            public Lesson Lesson;

            /// <summary>The name the save records it under. Stable: renaming it re-teaches every player.</summary>
            public string Key;

            /// <summary>The level of the tutorial chapter it first becomes eligible at, counted from 0.</summary>
            public int FromLevel;

            /// <summary>Shown by its event (<see cref="Trigger"/>) rather than at the level's start.</summary>
            public bool Contextual;

            /// <summary>Ends when the game reports the thing was done, not on a clock.</summary>
            public bool Action;

            /// <summary>
            /// The card is a <b>reward rather than a notice</b> (#459): it arrives wearing the praise's own
            /// dress — the accent, the halo, the score's spring and the chime — instead of earning it. There is
            /// exactly one, the send-off that closes the ladder, and it is informational because there is
            /// nothing left to ask the player to do.
            /// </summary>
            public bool Celebrates;

            public string Glyph, PadGlyph;
            public string Caption, PadCaption;
            public string Detail, PadDetail;
            public string Praise;
        }

        //PromptFont's own codepoints (Game/Content/Fonts/PromptFont.ttf, glyphs.json in its release): the keycaps
        //sit on the fullwidth Latin letters, the mouse and the Xbox buttons in the arrows and dingbats blocks.
        private const string KEY_W = "Ｗ";
        private const string KEY_A = "Ａ";
        private const string KEY_S = "Ｓ";
        private const string KEY_D = "Ｄ";
        private const string MOUSE = "⟼";
        private const string MOUSE_LEFT = "⟵";
        private const string MOUSE_RIGHT = "⟶";
        private const string PAD_STICK = "⇍";
        private const string PAD_LEFT_TRIGGER = "↖";
        private const string PAD_RIGHT_TRIGGER = "↗";

        //The glass lesson's caption, formatted once per level with that level's own cadence
        private const string CEILING_CAPTION = "The glass steps down every {0} shots";

        //In the order they are offered when several are eligible at once — which is also the order a player
        //who missed some catches up in. A pad caption left null falls back to the keyboard one (the lessons
        //with no key in them); a pad glyph likewise.
        private static readonly Definition[] DEFINITIONS =
        {
            new()
            {
                Lesson = Lesson.Aim, Key = "aim", FromLevel = 0, Action = true,
                Glyph = MOUSE, Caption = "Move the mouse to aim",
                PadGlyph = PAD_STICK, PadCaption = "Aim with the right stick",
                Praise = "Nice!",
            },
            new()
            {
                Lesson = Lesson.Fire, Key = "fire", FromLevel = 0, Action = true,
                Glyph = MOUSE_LEFT, Caption = "Click to fire", Detail = "Space fires too",
                PadGlyph = PAD_RIGHT_TRIGGER, PadCaption = "Pull the right trigger to fire",
                Praise = "Boom!",
            },
            new()
            {
                Lesson = Lesson.Match, Key = "match", FromLevel = 0, Action = true,
                Caption = "Three of a colour together fall", Detail = "And everything hanging under them",
                Praise = "Perfect!",
            },
            new()
            {
                Lesson = Lesson.LeanIn, Key = "lean", FromLevel = 1, Action = true,
                Glyph = MOUSE_RIGHT, Caption = "Hold to look down the barrel", Detail = "A close-up for the precise shot",
                PadGlyph = PAD_LEFT_TRIGGER, PadCaption = "Hold the left trigger to look down the barrel",
                PadDetail = "A close-up for the precise shot",
                Praise = "Sharp!",
            },
            new()
            {
                Lesson = Lesson.Ceiling, Key = "ceiling", FromLevel = 1, Contextual = true,
                Caption = CEILING_CAPTION, Detail = "The panel on the left shows how far it has come",
            },
            new()
            {
                Lesson = Lesson.Line, Key = "line", FromLevel = 1, Contextual = true,
                Caption = "The cluster is near the line!", Detail = "Drop a group to lift it. Under the line you have one second",
            },
            new()
            {
                Lesson = Lesson.Traverse, Key = "traverse", FromLevel = 2, Action = true,
                Glyph = KEY_A + KEY_D, Caption = "Walk the gun round the field", Detail = "Come at the cluster from another side",
                PadGlyph = PAD_STICK, PadCaption = "Push the left stick sideways to walk round",
                PadDetail = "Come at the cluster from another side",
                Praise = "Smooth!",
            },
            new()
            {
                Lesson = Lesson.Walk, Key = "walk", FromLevel = 3, Action = true,
                Glyph = KEY_W + KEY_S, Caption = "Step in for a steeper shot", Detail = "Closer means a shot up into the underside",
                PadGlyph = PAD_STICK, PadCaption = "Push the left stick up to step in",
                PadDetail = "Closer means a shot up into the underside",
                Praise = "Closer!",
            },
            new()
            {
                //THE COMBINATION (#460): the three movement lessons above teach their parts one at a time and
                //nothing says they compose — but composing them IS the precise shot, and a player who has done
                //three cards separately has no reason to try holding two at once. Late in the ladder on
                //purpose: it asks for all three of its parts to be in hand.
                Lesson = Lesson.Combine, Key = "combine", FromLevel = 4, Action = true,
                Glyph = MOUSE_RIGHT + KEY_A + KEY_D, Caption = "Hold the close-up and turn with it",
                Detail = "Line the shot up from inside the close-up",
                PadGlyph = PAD_LEFT_TRIGGER + PAD_STICK, PadCaption = "Hold the left trigger and push the stick",
                PadDetail = "Line the shot up from inside the close-up",
                Praise = "Together!",
            },
            new()
            {
                Lesson = Lesson.Streak, Key = "streak", FromLevel = 4, Contextual = true,
                Caption = "Hit after hit multiplies your score", Detail = "A miss resets the streak",
            },
            new()
            {
                Lesson = Lesson.Budget, Key = "budget", FromLevel = 5,
                Caption = "Spare shots pay a bonus at the end", Detail = "Clear the field in fewer for more stars",
            },
            new()
            {
                //THE RULE, BEFORE IT BITES (#459). The contextual `line` card above is the warning in the
                //moment — it fires when the floor's net first comes on and says what to do about it. This one
                //says what is at stake, on the opening of the level where losing to the line first becomes a
                //real risk, because a player who meets the loss with nothing having told them the rule reads
                //it as the game being unfair rather than as a rule they now know.
                Lesson = Lesson.LineRule, Key = "linerule", FromLevel = 6,
                Caption = "If the cluster reaches the line, the level is lost",
                Detail = "Keep it light — a heavy cluster hangs low and swings lower",
            },
            new()
            {
                //AND THE SEND-OFF (#459), which is the last card of the ladder: the tutorial had no end before
                //this, so a player was never told they had been taught everything — the cards simply stopped.
                //It celebrates rather than informs (see Definition.Celebrates), because being told you are done
                //is a reward and reads as one only if it is dressed as one.
                Lesson = Lesson.Graduated, Key = "graduated", FromLevel = 6, Celebrates = true,
                Caption = "That's everything — you know the game",
                Detail = "The rest is the adventure. Go!",
            },
        };

        /// <summary>How many levels a set without chapters is taught over — the shipped chapter's own length.</summary>
        private const int UNCHAPTERED_LEVELS = 10;

        //The card's clocks, all wall seconds of play: the pop-in, the retreat, how long it hides under a
        //camera takeover's blend, the praise, an informational card's hold, an action card's patience, the
        //quiet before the first card of a level (after the chapter intro has let go) and between two cards.
        private const float ARRIVE_SECONDS = 0.4f;
        private const float LEAVE_SECONDS = 0.35f;
        private const float SUPPRESS_SECONDS = 0.25f;
        private const float PRAISE_SECONDS = 2.2f;

        //How long an action card's INSTRUCTION is guaranteed to stand, counted from the moment the card is up
        //(#466). Until #466 there was no such guarantee at all and the card flipped to its praise word on the
        //frame the action landed — so the fastest lessons, which are the first ones a new player meets, showed
        //their instruction for a fraction of a second and the owner's report was that the text "just flashes
        //past and the player has no time to read it". The action is still recorded on its own frame and the
        //chime and the score's spring still fire there; only the TEXT waits.
        private const float MIN_READ_SECONDS = 2.8f;

        //The praise's flare is its own, shorter clock than the praise itself: the glow is a flash answering the
        //moment, and stretched over the whole hold it would read as a word that simply glows.
        private const float PRAISE_FLARE_SECONDS = 0.9f;
        private const float INFO_SECONDS = 7f;
        private const float ACTION_TIMEOUT = 22f;
        private const float FIRST_CARD_DELAY = 0.9f;
        private const float BETWEEN_CARDS = 0.7f;

        //How long a card of the demo reel stands before it counts itself done (Mode.Demo)
        private const float DEMO_SECONDS = 4f;

        //What counts as having done it: the aim swung this far in total (radians, about three and a half
        //degrees — a nudge, not a sweep), or a hold kept up this long without a break
        private const float AIM_TRAVEL = 0.06f;
        private const float HOLD_SECONDS = 0.35f;

        private readonly Func<string, bool> _wasTaught;
        private readonly Action<string> _teach;

        /// <summary>
        /// Testing only (the <c>tutorial</c> argument, <see cref="Mode.Force"/> and <see cref="Mode.Demo"/>): every
        /// lesson is offered as if never taught and none is recorded, so the cards can be looked at on a save
        /// that has long since finished the chapter — and the owner's save is left exactly as it was, which is
        /// this repository's rule for every scripted run.
        /// </summary>
        private readonly bool _force;

        /// <summary>The reel (<see cref="Mode.Demo"/>): everything eligible, every card done on a clock.</summary>
        private readonly bool _demo;

        private readonly HashSet<string> _taughtThisRun = new();
        private readonly List<Definition> _queue = new(DEFINITIONS.Length);
        private readonly List<Definition> _armed = new(DEFINITIONS.Length);

        private Definition _card;
        private Phase _phase;
        private float _presence;
        private float _suppress;
        private float _age;
        private float _praise;
        private float _gap;
        private float _held;
        private float _travel;
        private float _lastTraverse, _lastElevation;
        private bool _aimBaselined;
        private bool _praiseCue;
        private bool _praised;
        private bool _hasLevel;
        private Device _device;
        private string _ceilingCaption;

        /// <param name="wasTaught">Whether the save records a lesson, by key.</param>
        /// <param name="teach">Records a lesson in the save, by key.</param>
        /// <param name="mode">See <see cref="Mode"/>.</param>
        internal Tutorial(Func<string, bool> wasTaught, Action<string> teach, Mode mode)
        {
            _wasTaught = wasTaught ?? throw new ArgumentNullException(nameof(wasTaught));
            _teach = teach ?? throw new ArgumentNullException(nameof(teach));
            _force = mode != Mode.Normal;
            _demo = mode == Mode.Demo;
        }

        #region What the HUD reads

        /// <summary>How far the card is on screen, 0…1 — zero is nothing to draw. Folds in a takeover's hiding.</summary>
        internal float Presence => _presence * (1f - _suppress);

        /// <summary>Seconds the card has been up, for its idle bob.</summary>
        internal float Age => _age;

        /// <summary>
        /// The card is showing its praise word: an action just done. Held through the retreat that follows, or
        /// the word would flip back to the instruction on its way out.
        /// </summary>
        internal bool Praising => _phase == Phase.Praising || (_phase == Phase.Leaving && _praised);

        /// <summary>1 on the frame the praise lands, falling to 0 as it ends — the flash's own clock, which is
        /// shorter than the praise's hold (see <see cref="PRAISE_FLARE_SECONDS"/>).</summary>
        internal float PraiseHeat => _phase == Phase.Praising
            ? MathF.Max(0f, 1f - _praise / PRAISE_FLARE_SECONDS)
            : Celebrating ? MathF.Max(0f, 1f - _age / PRAISE_FLARE_SECONDS) : 0f;

        /// <summary>
        /// This card arrives already wearing the praise's dress (#459) — the send-off that closes the ladder.
        /// The HUD gives its caption the accent and the halo, and the cue fires on the frame it appears rather
        /// than on a frame the player earned, because there is nothing left here to earn.
        /// </summary>
        internal bool Celebrating => _card != null && _card.Celebrates;

        /// <summary>
        /// The praise word once the action has been done, or null. <b>It stands beside the instruction rather
        /// than in its place since #466</b>: the caption and the detail line stay legible under it and the
        /// whole card leaves together, where before the praise replaced the caption and took the detail line
        /// away entirely — so the one moment the player had earned the right to re-read the lesson was the
        /// moment it disappeared.
        /// </summary>
        internal string Praise => Praising && _card != null ? _card.Praise : null;

        /// <summary>Whether the last input was the pad: what any prompt outside the cards asks to pick its glyph (#499).</summary>
        internal bool OnGamepad => _device == Device.Gamepad;

        /// <summary>The glyph for the button that skips a cinematic — the fire button, which is what skips (#499).</summary>
        internal string SkipGlyph => _device == Device.Gamepad ? PAD_RIGHT_TRIGGER : MOUSE_LEFT;

        /// <summary>The prompt font's glyphs for the card — a keycap, a mouse, a trigger — or null for none.</summary>
        internal string Glyph => _card == null ? null
            : _device == Device.Gamepad ? _card.PadGlyph ?? _card.Glyph : _card.Glyph;

        /// <summary>The line of what to do. It stays up through the praise since #466 — see <see cref="Praise"/>.</summary>
        internal string Caption
        {
            get
            {
                if (_card == null) return null;
                if (_card.Lesson == Lesson.Ceiling) return _ceilingCaption;

                return _device == Device.Gamepad ? _card.PadCaption ?? _card.Caption : _card.Caption;
            }
        }

        /// <summary>The smaller second line, or null for a one-line card. It stays up through the praise too (#466).</summary>
        internal string Detail => _card == null ? null
            : _device == Device.Gamepad && _card.PadCaption != null ? _card.PadDetail : _card.Detail;

        /// <summary>
        /// True once per completed action, on the frame it completed — the session plays the chime and kicks
        /// the card off it, and the read clears it.
        /// </summary>
        internal bool TakePraiseCue()
        {
            bool cue = _praiseCue;
            _praiseCue = false;

            return cue;
        }

        #endregion

        #region The level

        /// <summary>
        /// The last level index the tutorial reaches in <paramref name="set"/>: the first chapter's last
        /// entry, or the first <see cref="UNCHAPTERED_LEVELS"/> of a set that names no chapters. −1 for no set
        /// at all — the fallback pyramid is not a campaign and teaches nothing.
        /// </summary>
        internal static int LastLevelOf(LevelSet set)
        {
            if (set == null || set.Count == 0) return -1;

            if (set.HasBlocks)
            {
                set.BlockRange(0, out _, out int last);
                return last;
            }

            return Math.Min(set.Count, UNCHAPTERED_LEVELS) - 1;
        }

        /// <summary>
        /// A level is starting. Decides what this level may teach — everything eligible at this index and not
        /// yet taught — and queues the start lessons in order while arming the contextual ones. Nothing shows
        /// until the level's own opening (a chapter intro, say) has let go.
        /// </summary>
        /// <param name="levelIndex">The level's place in the set, from 0.</param>
        /// <param name="lastIndex">The tutorial chapter's last index (<see cref="LastLevelOf"/>); past it, nothing is taught.</param>
        /// <param name="ceilingStep">The level's ceiling cadence, for the glass lesson's caption; null skips that lesson.</param>
        internal void BeginLevel(int levelIndex, int lastIndex, int? ceilingStep)
        {
            Reset();

            _hasLevel = levelIndex >= 0 && levelIndex <= lastIndex;
            if (!_hasLevel) return;

            //The one string built per level. A level whose glass holds still cannot fire the lesson anyway,
            //and it must not be armed with nothing to say.
            _ceilingCaption = ceilingStep is int step
                ? string.Format(CultureInfo.InvariantCulture, CEILING_CAPTION, step)
                : null;

            foreach (Definition lesson in DEFINITIONS)
            {
                if ((levelIndex < lesson.FromLevel && !_demo) || Taught(lesson)) continue;
                if (lesson.Lesson == Lesson.Ceiling && _ceilingCaption == null) continue;

                //The reel has no events to wait for, so its contextual cards are queued like the rest
                if (lesson.Contextual && !_demo) _armed.Add(lesson);
                else _queue.Add(lesson);
            }

            _gap = FIRST_CARD_DELAY;
        }

        /// <summary>Drops everything, for a session being torn down under it — and the first thing a new level does.</summary>
        internal void Reset()
        {
            _card = null;
            _phase = Phase.None;
            _presence = 0f;
            _suppress = 0f;
            _age = 0f;
            _praise = 0f;
            _gap = 0f;
            _held = 0f;
            _travel = 0f;
            _aimBaselined = false;
            _praiseCue = false;
            _praised = false;
            _hasLevel = false;
            _ceilingCaption = null;

            _queue.Clear();
            _armed.Clear();
        }

        private bool Taught(Definition lesson) =>
            _taughtThisRun.Contains(lesson.Key) || (!_force && _wasTaught(lesson.Key));

        private void Teach(Definition lesson)
        {
            if (!_taughtThisRun.Add(lesson.Key) || _force) return;

            _teach(lesson.Key);
        }

        #endregion

        #region The frame

        /// <summary>
        /// One frame of play. <paramref name="enabled"/> is the settings row, read here every frame so a
        /// change made from the pause lands at once; <paramref name="takeoverEngaged"/> hides the card under
        /// a camera takeover and holds every clock, since a lesson shown during a drop cinematic is a lesson
        /// shown to a player watching something else; <paramref name="levelDecided"/> clears the level's
        /// remaining lessons and takes the card down, a decided level having nothing left to teach.
        /// </summary>
        internal void Update(float elapsed, bool enabled, bool takeoverEngaged, bool levelDecided)
        {
            if (!_hasLevel) return;

            //The hiding blend runs whatever else is held, or a takeover that began mid-card would cut it
            _suppress = MoveTowards(_suppress, takeoverEngaged ? 1f : 0f, elapsed / SUPPRESS_SECONDS);

            if (levelDecided)
            {
                _queue.Clear();
                _armed.Clear();
            }

            if (!enabled || levelDecided)
            {
                //The card steps aside unrecorded and keeps its place at the front of the queue, so the row
                //switched back on resumes with the lesson that was up; a praise already earned is let finish.
                //A decided level has cleared the queue above, so there the card simply goes.
                if (_phase is Phase.Arriving or Phase.Shown) StepAside();

                //Off means off: a card on its way out still leaves, and nothing arrives
                if (_phase is not (Phase.Leaving or Phase.Praising)) return;
            }

            //Every clock below holds while hidden. The blend above has already begun taking the card down.
            if (takeoverEngaged) return;

            switch (_phase)
            {
                case Phase.None:
                    _gap -= elapsed;

                    if (_gap <= 0f && _queue.Count > 0)
                    {
                        Show(_queue[0]);
                        _queue.RemoveAt(0);
                    }
                    break;

                case Phase.Arriving:
                    _age += elapsed;
                    _presence = MathF.Min(1f, _presence + elapsed / ARRIVE_SECONDS);

                    if (_presence >= 1f) _phase = Phase.Shown;
                    break;

                case Phase.Shown:
                    _age += elapsed;

                    //An action card gives up rather than nagging, and is NOT recorded: it comes back on the
                    //next level of the chapter. An informational one has been read by now and is. The reel
                    //counts every card done on its own clock.
                    if (_demo)
                    {
                        if (_age >= DEMO_SECONDS)
                        {
                            if (_card.Action) Complete();
                            else Retreat(taught: true);
                        }
                    }
                    else if (_card.Action)
                    {
                        if (_age >= ACTION_TIMEOUT) Retreat(taught: false);
                    }
                    else if (_age >= INFO_SECONDS) Retreat(taught: true);
                    break;

                case Phase.Praising:
                    _age += elapsed;
                    _praise += elapsed;

                    //Both clocks, not either (#466): the praise has had its hold AND the instruction has stood
                    //long enough to be read. An action done in the first half-second — the common case on the
                    //fire and aim lessons — is what the second half of that is for.
                    if (_praise >= PRAISE_SECONDS && _age >= MIN_READ_SECONDS) _phase = Phase.Leaving;
                    break;

                case Phase.Leaving:
                    _presence -= elapsed / LEAVE_SECONDS;

                    if (_presence <= 0f)
                    {
                        _presence = 0f;
                        _card = null;
                        _phase = Phase.None;
                        _gap = BETWEEN_CARDS;
                    }
                    break;
            }
        }

        private void Show(Definition lesson)
        {
            _card = lesson;

            //A celebrating card is its own praise (#459): the chime and the score's spring go off as it lands.
            if (lesson.Celebrates) _praiseCue = true;
            _phase = Phase.Arriving;
            _presence = 0f;
            _age = 0f;
            _praise = 0f;
            _held = 0f;
            _travel = 0f;
            _aimBaselined = false;
            _praised = false;
        }

        /// <summary>The card goes, recorded or not — see the caller for which.</summary>
        private void Retreat(bool taught)
        {
            if (_card == null) return;
            if (taught) Teach(_card);

            _phase = Phase.Leaving;
        }

        /// <summary>
        /// The card up gives way — to a contextual card's event, or to the tutorial being switched off — and goes
        /// back to the front of the queue to return afterwards, unrecorded: its action was not done, or its
        /// information not read out.
        /// </summary>
        private void StepAside()
        {
            if (_card == null) return;

            _queue.Insert(0, _card);
            _phase = Phase.Leaving;
        }

        /// <summary>The action on the card was done: recorded, and the praise goes up in its place.</summary>
        private void Complete()
        {
            if (_card == null || !_card.Action || _phase is Phase.Praising or Phase.Leaving) return;

            Teach(_card);

            _phase = Phase.Praising;
            _presence = 1f;
            _praise = 0f;
            _praised = true;
            _praiseCue = true;
        }

        /// <summary>Whether <paramref name="lesson"/> is the card up and still waiting to be done.</summary>
        private bool IsUp(Lesson lesson) =>
            _card != null && _card.Lesson == lesson && _phase is Phase.Arriving or Phase.Shown;

        private static float MoveTowards(float value, float target, float step) =>
            value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);

        #endregion

        #region What the game reports

        /// <summary>The last device the player's hand was on. Cheap; called on every frame that saw input.</summary>
        internal void NoteDevice(Device device) => _device = device;

        /// <summary>
        /// The gun's aim this frame, for the aim lesson: it is done once the aim has travelled
        /// <see cref="AIM_TRAVEL"/> in total since the card came up. Read off the pose rather than the input so
        /// a mouse and a stick are judged alike, and baselined on the card's first frame so the pose the level
        /// opened with is not counted as a swing.
        /// </summary>
        internal void NoteAim(float traverse, float elevation)
        {
            if (!IsUp(Lesson.Aim))
            {
                _aimBaselined = false;
                return;
            }

            if (!_aimBaselined)
            {
                _lastTraverse = traverse;
                _lastElevation = elevation;
                _aimBaselined = true;
                return;
            }

            _travel += MathF.Abs(traverse - _lastTraverse) + MathF.Abs(elevation - _lastElevation);
            _lastTraverse = traverse;
            _lastElevation = elevation;

            if (_travel >= AIM_TRAVEL) Complete();
        }

        /// <summary>
        /// A hold the lesson asks for — precise aim's button, the traverse keys, the walk keys — held or not
        /// this frame. Done after <see cref="HOLD_SECONDS"/> of it unbroken; a tap is not the gesture.
        /// </summary>
        internal void NoteHold(Lesson lesson, bool held, float elapsed)
        {
            if (!IsUp(lesson)) return;

            _held = held ? _held + elapsed : 0f;

            if (_held >= HOLD_SECONDS) Complete();
        }

        /// <summary>An action that is its own event — a shot fired, a group dropped — done if it is the card up.</summary>
        internal void Report(Lesson lesson)
        {
            if (IsUp(lesson)) Complete();
        }

        /// <summary>
        /// A contextual lesson's event has happened. Ignored unless the lesson is armed for this level (so it
        /// fires once a level at most, and never for one already taught); otherwise it goes to the front of
        /// the queue, and a card already up gives way to it and comes back straight after.
        /// </summary>
        internal void Trigger(Lesson lesson)
        {
            int index = -1;

            for (int i = 0; i < _armed.Count; i++)
                if (_armed[i].Lesson == lesson) { index = i; break; }

            if (index < 0) return;

            Definition triggered = _armed[index];
            _armed.RemoveAt(index);

            //The card up returns behind the event's own card
            if (_phase is Phase.Arriving or Phase.Shown) StepAside();

            _queue.Insert(0, triggered);
        }

        #endregion
    }
}
