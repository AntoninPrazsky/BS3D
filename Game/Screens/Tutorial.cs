using Prazsky.BS3D.Levels;
using Prazsky.BS3D.GameStructure;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BS3D.Screens
{
    /// <summary>
    /// The tutorial (#189): what the first two chapters teach, <b>one thing at a time</b>, as a card over the play
    /// HUD — a keycap or a button drawn from the prompt font, a line of what to do with it, and a word of
    /// praise when it is done. The game taught nothing before this; the whole of its controls was one
    /// sentence on the About page, and a player who downloads the release has never read it.
    /// <para>
    /// <b>It is a ladder across the chapter, not a lecture on level one.</b> The owner's brief was to teach
    /// slowly — a player buried under instructions in the first minutes is a player who stops reading them —
    /// so each lesson has a level it first becomes eligible at (<see cref="Definition.FromLevel"/>), and the
    /// opener carries only the three that make the game a game: aim, fire, three of a colour fall. Precise aim
    /// waits for the second level, walking the gun round for the fourth — One, the first level with a far side,
    /// since the campaign opens on three flat sheets played from the stand the gun is given (#603) — stepping
    /// it in for the fifth, and the chapter ends on the line's rule and the send-off. A lesson the player did not get to — the level
    /// lost, the card timed out, the event never happened — follows them into the next level, and past its
    /// chapter into the next one (#666), until that chapter's send-off has been read; only then is what it still
    /// owed let go. <b>The score is the second chapter's
    /// (#666)</b>: the streak and the spare shots' bonus wait for it, so the first chapter is played before it
    /// is explained (<see cref="Definition.Chapter"/>), and past the second chapter nothing is offered at all —
    /// the first two chapters <i>are</i> the tutorial, which is what the owner asked for.
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
    /// coming back on the next one. An informational lesson (the glass, the streak, the line, the budget, the line's
    /// rule, the send-off) has
    /// nothing to wait for, so it holds <see cref="INFO_SECONDS"/> and goes. Three of those are
    /// <see cref="Definition.Contextual"/>: armed at the level's start and shown only when their event fires —
    /// the glass stepping, the streak lighting, the floor alarm coming on — because "the glass steps down
    /// every eight shots" means something on the frame the glass has just stepped and nothing a minute before.
    /// A contextual card interrupts whatever card is up, which goes back to the front of the queue behind it.
    /// </para>
    /// <para>
    /// <b>The save remembers what was taught, and a level shows its own cards again</b> (<see cref="PlayerProgress.Lessons"/>,
    /// #715): a lesson completed — the action done, or the informational card read out in full — is written there,
    /// and is not offered again as a lesson the player still owes (the deferral ladder skips it). But starting a
    /// level shows again the cards that level itself introduces (<see cref="OwnLevel"/>: Pennant's aim, fire and
    /// match, Rainbow's lean and its glass and line, Amphora's send-off), whatever the save says, while the
    /// settings row is on: the owner, "a player may want to go over it again". A replay writes nothing to the
    /// save, so <see cref="PlayerProgress.Lessons"/> stays what the player really completed. Once per entry into
    /// the level: a retry inside one run remembers through <see cref="_taughtThisRun"/>, which also carries the
    /// <c>tutorial</c> argument's run, where nothing is written (see <see cref="_force"/>). The settings row
    /// (<c>BS3DGame.IsTutorialEnabled</c>, on by default) is read every frame, so switching it off from the pause
    /// takes the card down at once and switching it back on resumes where it was; with it off no card shows, a
    /// replay included. Reset progress clears the record with the stars, so a fresh start is taught afresh.
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
        internal enum Lesson
        {
            Aim, Fire, Match, LeanIn, Ceiling, Line, Traverse, Walk, Combine, Streak, Budget, LineRule, Graduated, Swap,

            //A card per special kind, the first time it is met (#735) - see Definition.Kind
            KindStone, KindGlass, KindBomb, KindZap, KindAcid, KindFrozen, KindInfectious, KindGravity, KindHeavy, KindBuckshot,
            KindWildcard,
        }

        /// <summary>What the player's hand was last on, which is what the card draws for.</summary>
        internal enum Device { KeyboardMouse, Gamepad }

        /// <summary>
        /// How the tutorial is run. <see cref="Normal"/> is every player's: gated on the save, recorded to it.
        /// The other two are the <c>tutorial</c> argument's, testing only, and both record nothing.
        /// <see cref="Force"/> offers every lesson as if none had been taught, with the real detection — a
        /// player with a finished save can be shown the cards again by playing. <see cref="Demo"/> is a reel:
        /// every lesson eligible whatever the level, the contextual ones queued like the rest, and each card
        /// counting itself done after <see cref="DEMO_SECONDS"/> — so all thirteen can be photographed in one run of
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

            /// <summary>
            /// Which chapter of the campaign teaches it, from 0 (#666): the first chapter's ladder is the controls
            /// and the rules, the second's is the score. A lesson is offered in its own chapter, and in a later one
            /// only as a lesson its chapter still owes (<see cref="Eligible"/>).
            /// </summary>
            public int Chapter;

            /// <summary>
            /// The level of its <see cref="Chapter"/> it first becomes eligible at, counted from 0 — or, when
            /// negative, counted back from the chapter's end, −1 being its last level. The chapter's closing cards
            /// count from the end because a literal index drifts when the chapter changes size: the send-off read 6
            /// from #459 to #649, and #603's three sheets had quietly turned that into Pinwheel.
            /// </summary>
            public int FromLevel;

            /// <summary>Shown by its event (<see cref="Trigger"/>) rather than at the level's start.</summary>
            public bool Contextual;

            /// <summary>Ends when the game reports the thing was done, not on a clock.</summary>
            public bool Action;

            /// <summary>
            /// The card is a <b>reward rather than a notice</b> (#459): it arrives wearing the praise's own
            /// dress — the accent, the halo, the score's spring and the chime — instead of earning it. There is
            /// exactly one, the send-off that closes the first chapter's ladder, and it is informational because there is
            /// nothing left to ask the player to do.
            /// </summary>
            public bool Celebrates;

            public string Glyph, PadGlyph;
            public string Caption, PadCaption;
            public string Detail, PadDetail;
            public string Praise;

            /// <summary>
            /// The kind a <b>kind card</b> introduces (#735), or null for every lesson of the ladder. A kind card belongs
            /// to no chapter: it is offered on any level, the first time the kind is in front of the player - queued at
            /// the start of a level whose map carries it, or, for the wildcard, which arrives in the gun, armed and shown
            /// when the first one reaches the muzzle - and taught once per player. Its glyph is the ball itself, drawn
            /// live in the frame where a keycap would stand (<see cref="BallKind"/>), so the card never shows a still of
            /// a look that has since been redrawn (#622). <see cref="Chapter"/> and <see cref="FromLevel"/> mean nothing on it.
            /// </summary>
            public BallKind? Kind;
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
        //Each stick by name, and the way the card asks it to be pushed: the right one any way (aim), the left one sideways
        //(walk round) or up and down (step in). One plain stick with no letter stood for all three, so the aim card and
        //the walk cards showed the same picture for two different sticks.
        private const string PAD_RIGHT_STICK = "↻";
        private const string PAD_LEFT_STICK_SIDEWAYS = "⇄";
        private const string PAD_LEFT_STICK_UP_DOWN = "⇅";
        private const string PAD_LEFT_TRIGGER = "↖";
        private const string PAD_RIGHT_TRIGGER = "↗";
        private const string KEY_E = "Ｅ";
        private const string PAD_X = "⇐";

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
                PadGlyph = PAD_RIGHT_STICK, PadCaption = "Aim with the right stick",
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
                Lesson = Lesson.Traverse, Key = "traverse", FromLevel = 3, Action = true,
                Glyph = KEY_A + KEY_D, Caption = "Walk the gun round the field", Detail = "Come at the cluster from another side",
                PadGlyph = PAD_LEFT_STICK_SIDEWAYS, PadCaption = "Push the left stick sideways to walk round",
                PadDetail = "Come at the cluster from another side",
                Praise = "Smooth!",
            },
            new()
            {
                Lesson = Lesson.Walk, Key = "walk", FromLevel = 4, Action = true,
                Glyph = KEY_W + KEY_S, Caption = "Step in for a steeper shot", Detail = "Closer means a shot up into the underside",
                PadGlyph = PAD_LEFT_STICK_UP_DOWN, PadCaption = "Push the left stick up to step in",
                PadDetail = "Closer means a shot up into the underside",
                Praise = "Closer!",
            },
            new()
            {
                //THE COMBINATION (#460): the three movement lessons above teach their parts one at a time and
                //nothing says they compose — but composing them IS the precise shot, and a player who has done
                //three cards separately has no reason to try holding two at once. Late in the ladder on
                //purpose: it asks for all three of its parts to be in hand.
                Lesson = Lesson.Combine, Key = "combine", FromLevel = 5, Action = true,
                Glyph = MOUSE_RIGHT + KEY_A + KEY_D, Caption = "Hold the close-up and turn with it",
                Detail = "Line the shot up from inside the close-up",
                PadGlyph = PAD_LEFT_TRIGGER + PAD_LEFT_STICK_SIDEWAYS, PadCaption = "Hold the left trigger and push the left stick",
                PadDetail = "Line the shot up from inside the close-up",
                Praise = "Together!",
            },
            new()
            {
                //THE RULE, BEFORE IT BITES (#459). The contextual `line` card above is the warning in the
                //moment — it fires when the floor's net first comes on and says what to do about it. This one
                //says what is at stake, on the opening of the level where losing to the line first becomes a
                //real risk, because a player who meets the loss with nothing having told them the rule reads
                //it as the game being unfair rather than as a rule they now know. Since #649 that is Saturn, three
                //from the chapter's end: it and Amphora after it are the chapter's anchor-starved levels (a hoop on
                //four spokes, a cup whose ears are its second load path), the two where a swing reaches the line -
                //and Amphora, the chapter's last, keeps the send-off. Counted from the end (see FromLevel) because
                //this read 6 from #459 to #649, which the three sheets inserted in front had quietly turned into
                //Pinwheel.
                Lesson = Lesson.LineRule, Key = "linerule", FromLevel = -3,
                Caption = "If the cluster reaches the line, the level is lost",
                Detail = "Keep it light — a heavy cluster hangs low and swings lower",
            },
            new()
            {
                //AND THE SEND-OFF (#459), which is the last card of the first chapter's ladder: the tutorial had no end before
                //this, so a player was never told they had been taught everything — the cards simply stopped.
                //It celebrates rather than informs (see Definition.Celebrates), because being told you are done
                //is a reward and reads as one only if it is dressed as one. "The basics" and not "everything"
                //since #666: the score's two lessons come after it, in the second chapter, and a send-off that
                //said "everything" would be the one card that lied.
                Lesson = Lesson.Graduated, Key = "graduated", FromLevel = -1, Celebrates = true,
                Caption = "That's the basics — you know how to play",
                Detail = "The rest is the adventure. Go!",
            },
            new()
            {
                //THE SCORE WAITS FOR THE SECOND CHAPTER (#666), on the owner's playtest: the tutorial's explanation
                //of the score could wait, so the player is not overloaded with new information and can play a bit
                //first. The first chapter's levels are gentle enough that a player who misses one shot in three - so
                //the streak keeps resetting - and saves no shot still two-stars them (ScoreSim's sloppy player:
                //1.85-1.99 on all ten since #664), which is what every gate there asks: the multiplier works whether or not
                //anybody has explained it. After the send-off in DEFINITIONS, as it is in play. Contextual on the
                //Gallery's first level, as it was on the Meadow's third: shown when the streak first lights.
                Lesson = Lesson.Streak, Key = "streak", Chapter = 1, FromLevel = 0, Contextual = true,
                Caption = "Hit after hit multiplies your score", Detail = "A miss resets the streak",
            },
            new()
            {
                //And the budget one level on, so the chapter's first two levels teach one idea each.
                Lesson = Lesson.Budget, Key = "budget", Chapter = 1, FromLevel = 1,
                Caption = "Spare shots pay a bonus at the end", Detail = "Clear the field in fewer for more stars",
            },
            new()
            {
                //THE SWAP (#213), the third idea of the second chapter and the first thing in the game the player
                //carries rather than has done to them. It is offered on the level it becomes available (every level from
                //the second chapter grants one, LevelSet.SwapChargesAt) and only there: an action card, done when the
                //key is pressed. The detail says the price, because a player who spends the one swap on the level's
                //first shot has nothing left for the shot that needed it. Two levels in, so the score's two lessons
                //have each had a level to themselves before a third idea arrives.
                Lesson = Lesson.Swap, Key = "swap", Chapter = 1, FromLevel = 2, Action = true,
                Glyph = KEY_E, Caption = "Swap the next two balls", Detail = "One swap a level, for when the queue lets you down",
                PadGlyph = PAD_X, PadCaption = "Press X to swap the next two balls",
                PadDetail = "One swap a level, for when the queue lets you down",
                Praise = "Swapped!",
            },

            //THE KIND CARDS (#735, the owner's playtest notes of 2026-10-03 and 2026-10-07: "a tutorial has to be shown for
            //them as soon as they first appear on the map", and of the wildcard, "I would expect a cue card saying I got a
            //rainbow ball that counts as any colour"). One per kind that can be met, informational, each a shortened
            //HelpPage.KIND_ENTRIES line checked against BallKind.cs; the Help page keeps the full sentence. In the order
            //the campaign meets them (buckshot at level 93, the bomb at 105, the zap at 107, glass at 131, stone at 136; the
            //five after them no shipped level carries yet), which is the order two on one level are shown in.
            Kind(Lesson.KindBuckshot, "kind-buckshot", BallKind.Buckshot,
                "Buckshot has to come down to clear the level", "Cut away what holds it up and it pours out"),
            Kind(Lesson.KindBomb, "kind-bomb", BallKind.Bomb,
                "Land a ball beside a bomb to set it off", "It blasts every ball around it, any colour"),
            Kind(Lesson.KindZap, "kind-zap", BallKind.Zap,
                "Land a ball beside a zap to fire it", "It takes every ball of your shot's colour"),
            Kind(Lesson.KindGlass, "kind-glass", BallKind.Transparent,
                "Glass takes the first colour that touches it", "From then on it is an ordinary ball"),
            Kind(Lesson.KindStone, "kind-stone", BallKind.Rock,
                "Stone can't be matched or shot down", "It falls only when what holds it up goes"),
            Kind(Lesson.KindAcid, "kind-acid", BallKind.Acid,
                "Land a ball beside acid to set it off", "It eats straight down until it reaches a gap"),
            Kind(Lesson.KindFrozen, "kind-frozen", BallKind.Frozen,
                "Clear a group beside the ice to break it", "Then the ball inside is an ordinary ball"),
            Kind(Lesson.KindInfectious, "kind-infectious", BallKind.Infectious,
                "A sick ball spreads every time you fire", "It turns to stone as it passes it on - match it early"),
            Kind(Lesson.KindGravity, "kind-gravity", BallKind.Gravity,
                "This ball bends the shots that pass near it", "The aim beam shows the bend"),
            Kind(Lesson.KindHeavy, "kind-heavy", BallKind.Heavy,
                "A heavy ball drags the cluster down", "What hangs from it sits nearer the line"),

            //The wildcard arrives in the gun, so its card is contextual: armed on every level until taught, and fired by the
            //first one to reach the muzzle (GameplayScreen) - the moment the owner expected it
            new()
            {
                Lesson = Lesson.KindWildcard, Key = "kind-wildcard", Kind = BallKind.Wildcard, Contextual = true,
                Caption = "The rainbow ball counts as any colour", Detail = "It becomes whatever completes a group where it lands",
            },
        };

        /// <summary>A kind card's definition (#735): informational, at no chapter, its glyph the ball.</summary>
        private static Definition Kind(Lesson lesson, string key, BallKind kind, string caption, string detail) => new()
        {
            Lesson = lesson, Key = key, Kind = kind, Caption = caption, Detail = detail,
        };

        /// <summary>How many kinds there are, for the per-kind colour table <see cref="BeginLevel"/> is handed.</summary>
        internal static readonly int KIND_COUNT = MaxKind() + 1;

        private static int MaxKind()
        {
            int max = 0;
            foreach (BallKind kind in Enum.GetValues<BallKind>()) max = Math.Max(max, (int)kind);
            return max;
        }

        /// <summary>How long a chapter of a set without chapters is — the shipped chapter's own length.</summary>
        private const int UNCHAPTERED_LEVELS = 10;

        /// <summary>How many chapters teach anything (#666): the controls and rules in the first, the score in the second.</summary>
        private const int TUTORIAL_CHAPTERS = 2;

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

        //The lessons this level shows again although the save holds them (#715): its own (OwnLevel), set by BeginLevel
        private readonly HashSet<string> _replaying = new();
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
        private bool _devicePinned;
        private string _ceilingCaption;

        //The colour each kind card's ball is drawn in this level (#735): the first ball of that kind on the map, by kind
        private readonly BallType[] _kindColours = new BallType[KIND_COUNT];

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
        /// This card arrives already wearing the praise's dress (#459) — the send-off that closes the first chapter's ladder.
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

        /// <summary>
        /// The kind the card up introduces, or null (#735): the HUD reserves the glyph's place for it and the session draws
        /// the ball there, live, in the frame - see <see cref="Definition.Kind"/>.
        /// </summary>
        internal BallKind? CardKind => _card?.Kind;

        /// <summary>The colour of the card's ball (<see cref="CardKind"/>): the first one of its kind on this level's map.</summary>
        internal BallType CardColour => _card?.Kind is BallKind kind && (int)kind < _kindColours.Length && _kindColours[(int)kind] != 0
            ? _kindColours[(int)kind]
            : BallType.Type1;

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
        /// the praise word's spring off it (the whole card's, for the send-off, which has no praise row), and the
        /// read clears it.
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
        /// Where entry <paramref name="index"/> of <paramref name="set"/> sits in the tutorial: which chapter, from
        /// 0, and how far into it. A chaptered set's chapters are its blocks; a set that names none is taught in
        /// runs of <see cref="UNCHAPTERED_LEVELS"/>. False past the first <see cref="TUTORIAL_CHAPTERS"/> chapters —
        /// nothing is taught there — and for no set at all, since the fallback pyramid is not a campaign.
        /// </summary>
        internal static bool TryPlace(LevelSet set, int index, out int chapter, out int levelInChapter, out int chapterLength)
        {
            chapter = -1;
            levelInChapter = -1;
            chapterLength = 0;
            if (set == null || index < 0 || index >= set.Count) return false;

            if (set.HasBlocks)
            {
                set.BlockRange(index, out int first, out int last);
                chapter = set.BlockNumber(index) - 1;
                levelInChapter = index - first;
                chapterLength = last - first + 1;
            }
            else
            {
                chapter = index / UNCHAPTERED_LEVELS;
                levelInChapter = index % UNCHAPTERED_LEVELS;
                chapterLength = Math.Min(UNCHAPTERED_LEVELS, set.Count - chapter * UNCHAPTERED_LEVELS);
            }

            return chapter < TUTORIAL_CHAPTERS;
        }

        /// <summary>
        /// A level is starting. Decides what this level may teach — everything eligible at this index and not
        /// yet taught — and queues the start lessons in order while arming the contextual ones. Nothing shows
        /// until the level's own opening (a chapter intro, say) has let go.
        /// </summary>
        /// <param name="chapter">The level's chapter (<see cref="TryPlace"/>), or −1 for a level the tutorial does not reach.</param>
        /// <param name="levelInChapter">How far into that chapter the level is, from 0.</param>
        /// <param name="chapterLength">How many levels the chapter has, which a lesson counted from its end is placed by.</param>
        /// <param name="ceilingStep">The level's ceiling cadence, for the glass lesson's caption; null skips that lesson.</param>
        /// <param name="swapOffered">Whether the level grants a Swap (#213); the swap lesson is skipped when it does not.</param>
        /// <param name="retry">Whether this is a retry of the level just played (Retry, Restart). A retry remembers the cards
        /// this run went through; any other start of a level is an entry into it, and shows its own cards again (#715).</param>
        /// <param name="kindsOnMap">The level's map by kind (#735), indexed by <see cref="BallKind"/>: the colour of the first
        /// ball of that kind, or zero for a kind the map does not carry. Its kind cards are offered on any level, chapter or
        /// none.</param>
        internal void BeginLevel(int chapter, int levelInChapter, int chapterLength, int? ceilingStep, bool swapOffered = false,
            bool retry = false, ReadOnlySpan<BallType> kindsOnMap = default)
        {
            Reset();

            //An entry, not a retry: what this run went through is forgotten, so the level's own cards come back (#715). In
            //the normal mode every lesson taught is also in the save, so nothing still owed is offered again by this; the
            //testing modes write nothing and keep the run's memory, which is all they have.
            if (!retry && !_force) _taughtThisRun.Clear();

            _hasLevel = chapter >= 0;

            //The kind cards first (#735): offered on any level, the ladder's chapters or none, so they are queued before
            //the chapter gate below and ahead of the ladder's own cards - a kind on the map is this level's business
            QueueKinds(kindsOnMap);

            if (!_hasLevel)
            {
                _hasLevel = _queue.Count > 0 || _armed.Count > 0;
                _gap = FIRST_CARD_DELAY;
                return;
            }

            //The one string built per level. A level whose glass holds still cannot fire the lesson anyway,
            //and it must not be armed with nothing to say.
            _ceilingCaption = ceilingStep is int step
                ? string.Format(CultureInfo.InvariantCulture, CEILING_CAPTION, step)
                : null;

            foreach (Definition lesson in DEFINITIONS)
            {
                if (lesson.Kind != null) continue;
                if (!_demo && !Eligible(lesson, chapter, levelInChapter, chapterLength)) continue;

                //A level's own cards come back on every entry into it (#715), the save notwithstanding - unless this
                //attempt's run already went through them (a retry)
                if (!_force && OwnLevel(lesson, chapter, levelInChapter, chapterLength) && _wasTaught(lesson.Key))
                    _replaying.Add(lesson.Key);

                if (Taught(lesson)) continue;
                if (lesson.Lesson == Lesson.Ceiling && _ceilingCaption == null) continue;

                //A swap is only taught where there is one to press (#213): a set with no chapters, or the testing
                //argument's zero, grants none, and a card about a key that does nothing would be a lie
                if (lesson.Lesson == Lesson.Swap && !swapOffered && !_demo) continue;

                //The reel has no events to wait for, so its contextual cards are queued like the rest
                if (lesson.Contextual && !_demo) _armed.Add(lesson);
                else _queue.Add(lesson);
            }

            if (!_demo) NothingAfterTheSendOff();

            _gap = FIRST_CARD_DELAY;
        }

        /// <summary>
        /// Queues a card for every kind on this level's map that the player has not been taught (#735), in
        /// <see cref="DEFINITIONS"/>' order, and arms the wildcard's for the first one to reach the muzzle. The reel queues
        /// all of them, each in the first colour, so one run can photograph every kind's card.
        /// </summary>
        private void QueueKinds(ReadOnlySpan<BallType> kindsOnMap)
        {
            Array.Clear(_kindColours);

            foreach (Definition lesson in DEFINITIONS)
            {
                if (lesson.Kind is not BallKind kind || Taught(lesson)) continue;

                int index = (int)kind;
                BallType colour = index < kindsOnMap.Length ? kindsOnMap[index] : 0;
                _kindColours[index] = colour != 0 ? colour : BallType.Type1;

                if (_demo) _queue.Add(lesson);
                else if (lesson.Contextual) _armed.Add(lesson);
                else if (colour != 0) _queue.Add(lesson);
            }
        }

        /// <summary>
        /// Whether <paramref name="lesson"/> may be offered on this level: in its own chapter from its level on, and
        /// in any later chapter the tutorial reaches as a lesson its chapter still owes (#666) — so a card the first
        /// chapter never got to, the send-off on a skipped Amphora included, follows the player into the second the
        /// way a deferred card always followed them from level to level.
        /// <para>
        /// A level is clamped into the chapter: counted from the start, no later than its last level; counted from
        /// the end, no earlier than its first. On a chapter shorter than the ladder every lesson is then still
        /// reachable, and the ones counted from the start land no later than the send-off, which DEFINITIONS lists
        /// after them — a short chapter crowds its cards together rather than cutting any.
        /// </para>
        /// </summary>
        private static bool Eligible(Definition lesson, int chapter, int levelInChapter, int chapterLength)
        {
            if (lesson.Chapter != chapter) return lesson.Chapter < chapter;

            return levelInChapter >= FirstLevel(lesson, chapterLength);
        }

        /// <summary>
        /// Whether <paramref name="lesson"/> is one this level introduces (#715): its own chapter, and the very level it
        /// first becomes eligible on — the cards a player re-entering the level is shown again, as opposed to the
        /// lessons the ladder carries forward because they are still owed.
        /// </summary>
        private static bool OwnLevel(Definition lesson, int chapter, int levelInChapter, int chapterLength) =>
            lesson.Chapter == chapter && levelInChapter == FirstLevel(lesson, chapterLength);

        /// <summary>
        /// The level of its chapter a lesson is first offered on, clamped into the chapter: counted from the start, no
        /// later than its last level; counted from the end, no earlier than its first (see <see cref="Eligible"/>).
        /// </summary>
        private static int FirstLevel(Definition lesson, int chapterLength) =>
            lesson.FromLevel >= 0
                ? Math.Min(lesson.FromLevel, chapterLength - 1)
                : Math.Max(0, chapterLength + lesson.FromLevel);

        /// <summary>
        /// Keeps the send-off (<see cref="Lesson.Graduated"/>) the last card of its chapter's ladder (#605). The
        /// owner was told "That's everything — you know the game" on Amphora and then "The glass steps down every 7
        /// shots": a contextual lesson still armed fired when its event came, after the send-off. So on the level
        /// that sends the player off, the contextual lessons still untaught are queued as plain cards <b>ahead</b> of
        /// it — the glass's cadence reads as well at a level's start as mid-shot — except the line's,
        /// which is a warning about this moment ("The cluster is near the line!") and would be false at the start;
        /// the rule it serves is the <see cref="Lesson.LineRule"/> card queued just before. And once the player has
        /// been sent off, nothing of that chapter is taught again. <b>Only that chapter's lessons (#666):</b> the
        /// score's, in the second chapter, come after the send-off by design and are left exactly as they are.
        /// </summary>
        private void NothingAfterTheSendOff()
        {
            Definition sendOffLesson = null;
            foreach (Definition lesson in DEFINITIONS)
                if (lesson.Lesson == Lesson.Graduated) sendOffLesson = lesson;

            if (sendOffLesson == null) return;

            //The send-off closes ITS chapter's ladder and nothing else (#666): the score's lessons after it are the
            //second chapter's own, so only the first chapter's are cut or pulled ahead of it.
            int closes = sendOffLesson.Chapter;

            //Sent off already: nothing of that chapter is OWED any more - but a level's own cards shown again on
            //entering it (#715) are not owed, they are this level's, and they stay. Asked of the save and the run, not
            //of Taught: on Amphora the send-off is itself a replay, and read as untaught it would bring back every
            //lesson of the chapter still missing from the save, queued ahead of it (the review of #715)
            if (_taughtThisRun.Contains(sendOffLesson.Key) || (!_force && _wasTaught(sendOffLesson.Key)))
            {
                for (int i = _queue.Count - 1; i >= 0; i--)
                    if (OnLadder(_queue[i], closes) && !_replaying.Contains(_queue[i].Key)) _queue.RemoveAt(i);
                for (int i = _armed.Count - 1; i >= 0; i--)
                    if (OnLadder(_armed[i], closes) && !_replaying.Contains(_armed[i].Key)) _armed.RemoveAt(i);
                return;
            }

            int sendOff = -1;
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i].Lesson == Lesson.Graduated) { sendOff = i; break; }

            if (sendOff < 0) return;

            //A LATER CHAPTER'S LESSONS WAIT FOR IT (#666). Owed on a later chapter's level — Amphora skipped, or lost
            //before its cards came up — the send-off shares that level with the score's cards, and the streak's,
            //contextual, would interrupt it the moment it lit. Neither is taught here: they are offered on the next
            //level, after the player has been sent off, which is the order the chapters promise.
            for (int i = _queue.Count - 1; i >= 0; i--)
                if (_queue[i].Kind == null && _queue[i].Chapter > closes) _queue.RemoveAt(i);
            for (int i = _armed.Count - 1; i >= 0; i--)
                if (_armed[i].Kind == null && _armed[i].Chapter > closes) _armed.RemoveAt(i);

            sendOff = _queue.IndexOf(sendOffLesson);

            for (int i = 0; i < _armed.Count; i++)
            {
                Definition armed = _armed[i];
                if (!OnLadder(armed, closes)) continue;

                if (armed.Lesson != Lesson.Line) _queue.Insert(sendOff++, armed);
                _armed.RemoveAt(i--);
            }
        }

        /// <summary>
        /// Whether <paramref name="lesson"/> is on the ladder of chapter <paramref name="chapter"/> - the send-off's rules
        /// are about the ladder alone. A kind card (#735) has no chapter: it is not cut by a send-off already read, nor
        /// pulled ahead of one, nor held back for a later chapter.
        /// </summary>
        private static bool OnLadder(Definition lesson, int chapter) => lesson.Kind == null && lesson.Chapter == chapter;

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
            _replaying.Clear();
        }

        //A replayed card (#715) counts as untaught for this level, so the send-off's rules treat it as the card it is
        private bool Taught(Definition lesson) =>
            _taughtThisRun.Contains(lesson.Key) || (!_force && !_replaying.Contains(lesson.Key) && _wasTaught(lesson.Key));

        //Recorded once, and never again for a lesson the save already holds: a replay writes nothing (#715)
        private void Teach(Definition lesson)
        {
            if (!_taughtThisRun.Add(lesson.Key) || _force || _wasTaught(lesson.Key)) return;

            _teach(lesson.Key);
        }

        #endregion

        #region The frame

        /// <summary>
        /// One frame of play. <paramref name="enabled"/> is the settings row, read here every frame so a
        /// change made from the pause lands at once; <paramref name="takeoverEngaged"/> hides the card under
        /// a camera takeover and holds the clocks of a card still needed, since a lesson shown during a drop
        /// cinematic is a lesson shown to a player watching something else; <paramref name="levelDecided"/>
        /// clears the level's remaining lessons and drops the card, a decided level having nothing left to teach.
        /// <para>
        /// ⚠ <b>A card on its way out runs to its end even under a takeover</b>, hidden by the suppress blend (#700).
        /// Every clock used to hold, the leaving fade's included: the card was hidden as the takeover began and the
        /// fade resumed from where it stood as the takeover lifted, so the card faded back in with the hiding and out
        /// with the fade, about a third of the way up for a third of a second - a flash of text already dealt with,
        /// after a drop cinematic mid-level and before the result page at a level's end.
        /// </para>
        /// </summary>
        internal void Update(float elapsed, bool enabled, bool takeoverEngaged, bool levelDecided)
        {
            if (!_hasLevel) return;

            //The hiding blend runs whatever else is held, or a takeover that began mid-card would cut it
            _suppress = MoveTowards(_suppress, takeoverEngaged ? 1f : 0f, elapsed / SUPPRESS_SECONDS);

            //A decided level drops the card outright (#700): no queue, no arrival, no praise finishing and no fade for a
            //takeover to freeze and hand back. A lesson already done is recorded (Complete records it as it praises); one
            //not done comes back with the next level's lessons as it always did. Its praise cue goes with it, so a lesson
            //done by the very shot that decides the level does not chime for a card nobody is shown.
            if (levelDecided)
            {
                _queue.Clear();
                _armed.Clear();
                _card = null;
                _phase = Phase.None;
                _presence = 0f;
                _praiseCue = false;
                return;
            }

            if (!enabled)
            {
                //The card steps aside unrecorded and keeps its place at the front of the queue, so the row
                //switched back on resumes with the lesson that was up; a praise already earned is let finish.
                if (_phase is Phase.Arriving or Phase.Shown) StepAside();

                //Off means off: a card on its way out still leaves, and nothing arrives
                if (_phase is not (Phase.Leaving or Phase.Praising)) return;
            }

            //While hidden, the clocks of a card still needed hold, so it comes back after the takeover; a card on its
            //way out (leaving, or praising towards it) runs to its end unseen, so nothing is there to come back (#700)
            if (takeoverEngaged && _phase is not (Phase.Leaving or Phase.Praising)) return;

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

                    //An action done while the card was still arriving lets the arrival finish rather than
                    //snapping the card to full size on the praise's frame (#673): the pop-in's scale is the
                    //instruction's, and at a tenth of a second in it stands about a fifth under full.
                    _presence = MathF.Min(1f, _presence + elapsed / ARRIVE_SECONDS);

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
        /// information not read out. Not on a decided level, which drops the card instead (#700).
        /// </summary>
        private void StepAside()
        {
            if (_card == null) return;

            _queue.Insert(0, _card);
            _phase = Phase.Leaving;
        }

        /// <summary>The action on the card was done: recorded, and the praise joins the instruction under it.</summary>
        private void Complete()
        {
            if (_card == null || !_card.Action || _phase is Phase.Praising or Phase.Leaving) return;

            Teach(_card);

            _phase = Phase.Praising;
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
        internal void NoteDevice(Device device)
        {
            if (!_devicePinned) _device = device;
        }

        /// <summary>
        /// Testing only (the <c>pad</c> argument): the prompts drawn for <paramref name="device"/> for the whole run,
        /// whatever is touched. A run nobody plays still sees a mouse — the pointer resting off the centre the capture
        /// recentres to reads as a hand on it — so a device merely noted at the start is gone by the first card.
        /// </summary>
        internal void PinDevice(Device device)
        {
            _device = device;
            _devicePinned = true;
        }

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
