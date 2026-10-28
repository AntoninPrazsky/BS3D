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
    /// it in for the fifth, and the chapter ends on the line's rule and its closing card, the "send-off" (since #936 the advice to throw a
    /// ball away, on Amphora). A lesson the player did not get to — the level
    /// lost, the card timed out, the event never happened — follows them into the next level, and past its
    /// chapter into the next one (#666), until that chapter's send-off has been read; only then is what it still
    /// owed let go. <b>The score is the second chapter's
    /// (#666)</b>: the streak and the spare shots' bonus wait for it, so the first chapter is played before it
    /// is explained (<see cref="Definition.Chapter"/>), and past the second chapter the ladder offers nothing —
    /// the first two chapters <i>are</i> the tutorial, which is what the owner asked for. What reaches past them are the
    /// chapterless cards, of a kind (#735) or a tool (#705) met for the first time, the first ball left stuck as dead
    /// weight (#1023), and the precise-aim hint (#1012).
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
    /// match, Rainbow's lean and its glass and line, Amphora's way out), whatever the save says, while the
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
    /// <b>One thing a step, in plain imperatives</b> (#930, the owner's: "players have a short attention span - they respond
    /// to quick, clear instructions, they do not enjoy deciphering text"). A card says what to <i>do</i>, in the fewest words,
    /// with the glyph for the hand in use; why it matters is the Help page's. A card that asks for two things is two steps
    /// (<see cref="Definition.Second"/>): the combination of the close-up and a walk is the only one, its second step shown once
    /// the first has been done. <c>TutorialCopyTests</c> holds every card to it.
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
            Aim, Fire, Match, LeanIn, Ceiling, Line, Traverse, Walk, Combine, Streak, Budget, LineRule, ThrowAway, Swap,

            //The other two power-ups' cards, beside the Swap's (#705) - see Definition.Tool
            Brake, Cut,

            //A card per special kind, the first time it is met (#735) - see Definition.Kind
            KindStone, KindGlass, KindBomb, KindZap, KindAcid, KindFrozen, KindInfectious, KindGravity, KindHeavy, KindBuckshot,
            KindWildcard,

            //The precise-aim hint (#1012): a reminder shown again while it is needed - see Definition.Hint
            AimHint,

            //The dead weight's card (#1023): what a ball with pixels missing is, the first time one comes to rest - see
            //Definition.DeadBall
            DeadWeight,
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
            /// The key an earlier build recorded this lesson under, or null (#936). A save that holds it counts as having
            /// this lesson in it - the player was past the ladder's end when it was recorded, so nothing of the chapter is
            /// owed on its account - but the lesson itself is new, so it is shown again on the level it belongs to, and
            /// recorded under <see cref="Key"/> then.
            /// </summary>
            public string LegacyKey;

            /// <summary>
            /// Also armed as a contextual card on the level it belongs to (#936), for <see cref="Trigger"/> to fire at the
            /// moment it is for, once a level: the card is shown when the level opens, and shown again when the game sees the
            /// player in the very spot it advises about. Armed only once the opening showing has been read and has rested
            /// (<see cref="_repriseWaits"/>, <see cref="REPRISE_REST_SECONDS"/>), so a player never meets the same card twice
            /// running.
            /// </summary>
            public bool Reprise;

            public string Glyph, PadGlyph;
            public string Caption, PadCaption;
            public string Detail;
            public string Praise;

            /// <summary>
            /// The lesson's <b>second step</b> (#930), or null for the one-step cards every other lesson is. A card that
            /// asks for two things is two cards' worth of asking, and a player told both at once does neither: the first
            /// step stands alone until the game sees it done (<see cref="NoteTwoStepHold"/>), the card then turns to this
            /// one in place (only its words: glyph, caption, detail), and the lesson is complete, praised and recorded
            /// under <see cref="Key"/> when <i>this</i> one is done. The step is a <see cref="Definition"/> only so it
            /// shares the words' fields and the hand's fallbacks; nothing else on it is read.
            /// </summary>
            public Definition Second;

            /// <summary>
            /// The kind a <b>kind card</b> introduces (#735), or null for every lesson of the ladder. A kind card belongs
            /// to no chapter: it is offered on any level, the first time the kind is in front of the player - queued at
            /// the start of a level whose map carries it, or, for the wildcard, which arrives in the gun, armed and shown
            /// when the first one reaches the muzzle - and taught once per player. Its glyph is the ball itself, drawn
            /// live in the frame where a keycap would stand (<see cref="BallKind"/>), so the card never shows a still of
            /// a look that has since been redrawn (#622). <see cref="Chapter"/> and <see cref="FromLevel"/> mean nothing on it.
            /// </summary>
            public BallKind? Kind;

            /// <summary>
            /// A <b>power-up's card</b> (#705): the Swap's, the Brake's or the Cut's. Like a kind card it belongs to no chapter
            /// and is taught once per player, and it is offered on the first level that grants the tool - the chip and its
            /// card arrive together, never the chip alone (the owner: "one new thing at a time, each taught the first time
            /// the player sees it"). The Swap and the Cut are queued as the level opens; the Brake is contextual, armed and
            /// shown on the glass's first pressure step, the one moment it has something to give back.
            /// </summary>
            public bool Tool;

            /// <summary>
            /// A <b>hint</b> (#1012), not a lesson: the precise-aim reminder, shown when shots fired outside the lean go on
            /// dropping nothing (<see cref="PreciseAimHint"/> decides when it is due). Like a kind or a tool card it belongs to
            /// no chapter, but it is <b>never recorded</b>, so the save cannot retire it: it is armed on every level the
            /// session asks for (<see cref="BeginLevel"/>'s <c>aimHintArmed</c>), and <see cref="Trigger"/> leaves it armed,
            /// so it comes back as often as it is due - never while it is up or waiting its turn. It waits behind a card
            /// already up instead of pushing it aside. Done when the button is held, as the lean's own card is; unheld, it
            /// goes after <see cref="INFO_SECONDS"/>, which is enough to read it, rather than standing for an action card's
            /// timeout.
            /// </summary>
            public bool Hint;

            /// <summary>
            /// The <b>dead weight's card</b> (#1023): its glyph is a ball as the map draws dead weight (an ordinary ball with
            /// <c>BallRenderSet.DEAD_DISSOLVE</c> of its pixels dithered out, not breathing), in the colour of the ball that
            /// fired it (<see cref="NoteDeadWeight"/>). Like a kind card it belongs to no chapter and is taught once per player;
            /// it is armed on every level the session asks for until then (<see cref="BeginLevel"/>'s <c>deadWeightArmed</c>),
            /// and, like a hint, it waits behind a card already up: the ball it explains stays where it came to rest, so the
            /// card is never urgent.
            /// </summary>
            public bool DeadBall;
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
        private const string KEY_Q = "Ｑ";
        private const string PAD_Y = "⇑";
        private const string KEY_R = "Ｒ";

        //U+2199 is PromptFont's RB (PlayHud's Cut chip draws the same)
        private const string PAD_RIGHT_BUMPER = "↙";

        //Where the line's card sits in its chapter, counted from the chapter's end (Saturn in the campaign): also the level
        //the net starts to be shown at (NetIsShownAt)
        private const int LINE_RULE_FROM_LEVEL = -3;

        //The glass lesson's caption, formatted once per level with that level's own cadence
        private const string CEILING_CAPTION = "The glass steps down every {0} shots";

        //The precise aim's words, said by two cards: its own lesson, and the combination's first step (#930)
        private const string LEAN_CAPTION = "Hold to look down the barrel";
        private const string LEAN_PAD_CAPTION = "Hold the left trigger to look down the barrel";

        //In the order they are offered when several are eligible at once — which is also the order a player
        //who missed some catches up in. A pad caption left null falls back to the keyboard one (the lessons
        //with no key in them); a pad glyph likewise.
        private static readonly Definition[] DEFINITIONS =
        {
            new()
            {
                Lesson = Lesson.Aim, Key = "aim", FromLevel = 0, Action = true,
                Glyph = MOUSE, Caption = "Move the mouse to aim",
                PadGlyph = PAD_RIGHT_STICK, PadCaption = "Move the right stick to aim",
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
                Caption = "Match three of a colour", Detail = "Shoot beside two of the same colour",
                Praise = "Perfect!",
            },
            new()
            {
                Lesson = Lesson.LeanIn, Key = "lean", FromLevel = 1, Action = true,
                Glyph = MOUSE_RIGHT, Caption = LEAN_CAPTION,
                PadGlyph = PAD_LEFT_TRIGGER, PadCaption = LEAN_PAD_CAPTION,
                Praise = "Sharp!",
            },
            new()
            {
                Lesson = Lesson.Ceiling, Key = "ceiling", FromLevel = 1, Contextual = true,
                Caption = CEILING_CAPTION, Detail = "Watch the panel on the left",
            },
            new()
            {
                Lesson = Lesson.Line, Key = "line", FromLevel = 1, Contextual = true,
                Caption = "Drop a group to lift the cluster", Detail = "It is near the line",
            },
            new()
            {
                Lesson = Lesson.Traverse, Key = "traverse", FromLevel = 3, Action = true,
                Glyph = KEY_A + KEY_D, Caption = "Walk the gun round the field",
                PadGlyph = PAD_LEFT_STICK_SIDEWAYS, PadCaption = "Push the left stick sideways to walk round",
                Praise = "Smooth!",
            },
            new()
            {
                Lesson = Lesson.Walk, Key = "walk", FromLevel = 4, Action = true,
                Glyph = KEY_W + KEY_S, Caption = "Step closer to the cluster",
                PadGlyph = PAD_LEFT_STICK_UP_DOWN, PadCaption = "Push the left stick up to step closer",
                Praise = "Closer!",
            },
            new()
            {
                //THE COMBINATION (#460): the three movement lessons above teach their parts one at a time and
                //nothing says they compose — but composing them IS the precise shot, and a player who has done
                //three cards separately has no reason to try holding two at once. Late in the ladder on
                //purpose: it asks for all three of its parts to be in hand.
                Lesson = Lesson.Combine, Key = "combine", FromLevel = 5, Action = true,
                //Two steps since #930, the owner's "aim and move at the same time" card: the first is the lean card's own
                //words, the second the walk's, and the card turns from one to the other when the hold has been kept
                Glyph = MOUSE_RIGHT, Caption = LEAN_CAPTION,
                PadGlyph = PAD_LEFT_TRIGGER, PadCaption = LEAN_PAD_CAPTION,
                Second = new Definition
                {
                    Glyph = KEY_A + KEY_D, Caption = "Now walk the gun round",
                    PadGlyph = PAD_LEFT_STICK_SIDEWAYS, PadCaption = "Now push the left stick sideways",
                },
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
                Lesson = Lesson.LineRule, Key = "linerule", FromLevel = LINE_RULE_FROM_LEVEL,
                Caption = "Keep the cluster above the red line", Detail = "Reaching it loses the level",
            },
            new()
            {
                //THE WAY OUT OF AMPHORA (#936), the last card of the first chapter's ladder - the "send-off" the rules below
                //still call it by. It was a send-off in words ("That's the basics - you know how to play", #459, a reward
                //dressed as one) until the owner's playtest of 2026-10-19: on Amphora, the hardest level of the chapter, that
                //line arrived two levels after the card it read as the tail of, said nothing new, and nothing on the level
                //told a player whose cluster swings low what to DO about it. The way out is to throw a ball away - fire it
                //into empty space, where it adds nothing to the cluster and costs the streak, not the level - and a
                //newcomer holds on to every ball as if it had to count. So the closing card says that it is fine.
                //Informational, and it asks for one thing in the plainest words (TutorialCopyTests). The score is not taught
                //yet (#666), so the card names no multiplier: it says wasting a ball is fine and leaves the price to the
                //Help page. Its first key, "graduated", is the LegacyKey: a save that holds it was sent off by the old
                //card, owes the chapter nothing, and meets this one as Amphora's own the next time it enters the level.
                //Reprise: shown again, once a level, when the net is on and the round in the bore has nowhere to score
                //(GameplayScreen.UpdateLaserWarning) - the moment the card is about.
                Lesson = Lesson.ThrowAway, Key = "throwaway", LegacyKey = "graduated", FromLevel = -1, Reprise = true,
                Caption = "Fire a ball into empty space when stuck",
                Detail = "Wasting a ball is fine",
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
                Caption = "Keep hitting to multiply your score", Detail = "A miss resets it",
            },
            new()
            {
                //And the budget one level on, so the chapter's first two levels teach one idea each.
                Lesson = Lesson.Budget, Key = "budget", Chapter = 1, FromLevel = 1,
                Caption = "Save shots for a bonus", Detail = "Fewer shots earn more stars",
            },
            new()
            {
                //THE SWAP (#213), the first thing in the game the player carries rather than has done to them. Since #705 a
                //tool's card (Definition.Tool): offered on the first level that grants one (the third chapter's,
                //LevelSet.SwapChargesAt), with its chip; until then it was the second chapter's third level, two levels
                //after its chip. An action card, done when the key is pressed. The detail says the price, because a player
                //who spends the one swap on the level's first shot has nothing left for the shot that needed it. Its key
                //stays "swap", so a player taught it on the old schedule is not taught it again.
                Lesson = Lesson.Swap, Key = "swap", Tool = true, Action = true,
                Glyph = KEY_E, Caption = "Swap the next two balls", Detail = "One swap a level",
                PadGlyph = PAD_X,
                Praise = "Swapped!",
            },
            new()
            {
                //THE BRAKE (#213, #705): the fourth chapter's tool. Contextual - the glass's first pressure step is the moment it
                //has a step to give back, and the moment its chip lights - and an action card from there, done when the key is
                //pressed.
                Lesson = Lesson.Brake, Key = "brake", Tool = true, Contextual = true, Action = true,
                Glyph = KEY_Q, Caption = "Lift the glass back one step", Detail = "One brake a level",
                PadGlyph = PAD_Y,
                Praise = "Lifted!",
            },
            new()
            {
                //THE CUT (#213, #692, #705): the fifth chapter's tool, on the rule the owner settled on #692 - the whole storey
                //the cutter strikes, never more than the cluster's lower half, previewed lit as it is aimed. Done when the key
                //is pressed, which arms the round in the bore.
                Lesson = Lesson.Cut, Key = "cut", Tool = true, Action = true,
                Glyph = KEY_R, Caption = "Turn the next ball into a cutter", Detail = "One cut a level",
                PadGlyph = PAD_RIGHT_BUMPER,
                Praise = "Armed!",
            },

            //THE KIND CARDS (#735, the owner's playtest notes of 2026-10-03 and 2026-10-07: "a tutorial has to be shown for
            //them as soon as they first appear on the map", and of the wildcard, "I would expect a cue card saying I got a
            //rainbow ball that counts as any colour"). One per kind that can be met, informational, each a shortened
            //OddBallCopy.Entries line checked against BallKind.cs; the Help page keeps the full sentence. In the order
            //the campaign meets them (glass at level 42, the Silhouettes' Umbrella since #736; buckshot at 93, the bomb at
            //105, the zap at 107, stone at 136; the five after them no shipped level carries yet), which is the order two on
            //one level are shown in.
            Kind(Lesson.KindGlass, "kind-glass", BallKind.Transparent,
                "Glass takes the first colour that touches it", "From then on it is an ordinary ball"),
            Kind(Lesson.KindBuckshot, "kind-buckshot", BallKind.Buckshot,
                "Buckshot has to come down to clear the level", "Cut away what holds it up and it pours out"),
            Kind(Lesson.KindBomb, "kind-bomb", BallKind.Bomb,
                "Land a ball beside a bomb to set it off", "It blasts every ball around it, any colour"),
            Kind(Lesson.KindZap, "kind-zap", BallKind.Zap,
                "Land a ball beside a zap to fire it", "It takes every ball of your shot’s colour"),
            Kind(Lesson.KindStone, "kind-stone", BallKind.Rock,
                "Stone can’t be matched or shot down", "It falls only when what holds it up goes"),
            Kind(Lesson.KindAcid, "kind-acid", BallKind.Acid,
                "Land a ball beside acid to set it off", "It eats straight down until it reaches a gap"),
            Kind(Lesson.KindFrozen, "kind-frozen", BallKind.Frozen,
                "Clear a group beside the ice to break it", "Then the ball inside is an ordinary ball"),
            Kind(Lesson.KindInfectious, "kind-infectious", BallKind.Infectious,
                "A sick ball spreads every time you fire", "It turns to stone as it passes it on — match it early"),
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

            //THE PRECISE-AIM HINT (#1012), the owner's ruling after an outside player played the whole campaign from the
            //overview: shots outside the lean that go on dropping nothing say the lean is there. It names what to do, aim
            //precisely, as the owner asked ("a hint to aim with the right button"), and what the button gives in the lean
            //card's own image; its key is never written (Definition.Hint)
            new()
            {
                Lesson = Lesson.AimHint, Key = "aimhint", Hint = true, Contextual = true, Action = true,
                Glyph = MOUSE_RIGHT, Caption = "Hold to aim precisely", Detail = "A close-up down the barrel",
                PadGlyph = PAD_LEFT_TRIGGER, PadCaption = "Hold the left trigger to aim precisely",
                Praise = "Sharp!",
            },

            //DEAD WEIGHT (#1023), the owner's relay of a friend's playtest of 2026-10-26: the friend saw the balls with pixels
            //missing (#620's mark) and could not tell what they meant - that a group the cluster has let go of has wedged on
            //its way down, is shot down and counted, and a shot passes through it (#410). The mark was tuned three times for
            //how it looks (#342, #412, #620) and never taught. Informational, with the ball itself where a glyph would stand,
            //drawn as the map draws the one that fired the card
            new()
            {
                Lesson = Lesson.DeadWeight, Key = "deadweight", DeadBall = true, Contextual = true,
                Caption = "This ball is already shot down", Detail = "It just hasn’t fallen yet",
            },
        };

        /// <summary>A kind card's definition (#735): informational, at no chapter, its glyph the ball.</summary>
        private static Definition Kind(Lesson lesson, string key, BallKind kind, string caption, string detail) => new()
        {
            Lesson = lesson, Key = key, Kind = kind, Caption = caption, Detail = detail,
        };

        /// <summary>
        /// Every string a card can draw, both hands' (#806): what <c>CopyRulesTests</c> holds to the typesetting rules
        /// (<see cref="CopyRules"/>), so a card added with a straight apostrophe or a hyphen for a dash fails there.
        /// </summary>
        internal static IEnumerable<string> Copy()
        {
            foreach (Definition definition in DEFINITIONS)
            {
                for (Definition face = definition; face != null; face = face.Second)
                    foreach (string text in new[] { face.Caption, face.PadCaption, face.Detail, face.Praise })
                        if (text != null) yield return text;
            }
        }

        /// <summary>One step of a card as one hand draws it (<see cref="Steps"/>).</summary>
        /// <param name="Key">The lesson's save key.</param>
        /// <param name="Step">0 for the card's first step, 1 for its second (#930).</param>
        /// <param name="Pad">The gamepad's words, not the keyboard and mouse's.</param>
        /// <param name="Glyph">The prompt font's glyphs, or null for none (a kind card's is the ball itself).</param>
        /// <param name="Caption">The big line; the glass lesson's with its cadence put in.</param>
        /// <param name="Detail">The small line, or null.</param>
        /// <param name="Instructs">The card tells the player to <i>do</i> something: not a kind's card or the dead weight's,
        /// which say what a ball is.</param>
        internal readonly record struct StepCopy(string Key, int Step, bool Pad, string Glyph, string Caption, string Detail, bool Instructs);

        /// <summary>
        /// Every step of every card, for each hand, exactly as the card draws it (#930): what <c>TutorialCopyTests</c> holds to
        /// "one thing at a time". Read through the same <see cref="Words"/> the card is, so a hand's fallbacks are the ones
        /// the player sees.
        /// </summary>
        internal static IEnumerable<StepCopy> Steps()
        {
            foreach (Definition definition in DEFINITIONS)
            {
                bool instructs = definition.Kind == null && !definition.DeadBall;
                int step = 0;

                for (Definition face = definition; face != null; face = face.Second, step++)
                    foreach (bool pad in new[] { false, true })
                    {
                        (string glyph, string caption, string detail) = Words(face, pad);
                        if (definition.Lesson == Lesson.Ceiling)
                            caption = string.Format(CultureInfo.InvariantCulture, caption, 6);

                        yield return new StepCopy(definition.Key, step, pad, glyph, caption, detail, instructs);
                    }
            }
        }

        /// <summary>
        /// The words of one step for one hand. A pad caption left null falls back to the keyboard one (the lessons with no key
        /// in them, and the tools', whose glyph already says which button), a pad glyph likewise. The small line is the
        /// hands' alike: it names no control, since #930 cut every one that explained why.
        /// </summary>
        private static (string Glyph, string Caption, string Detail) Words(Definition face, bool pad) =>
            pad ? (face.PadGlyph ?? face.Glyph, face.PadCaption ?? face.Caption, face.Detail)
                : (face.Glyph, face.Caption, face.Detail);

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

        //A two-step card (#930, Definition.Second): the first step stands at least this long before the card turns to the
        //second - a player already holding the button when the card pops would otherwise see it flash by - and the second
        //step falls back to the first when its hold has been let go this long, since only the first says what to hold
        private const float STEP_MIN_SECONDS = 1.2f;
        private const float STEP_BACK_SECONDS = 0.5f;

        //How long a card that is also armed for its moment (Definition.Reprise, #936) rests after its opening showing before
        //the moment may fire it: a player stuck just as the card is read out is not shown the same words again at once
        private const float REPRISE_REST_SECONDS = 8f;

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

        //The card whose reprise waits for its opening showing to be read (#936, Definition.Reprise), or null; and, once it has
        //been read, the seconds of play left before the reprise is armed (infinite while the opening card is unread)
        private Definition _repriseWaits;
        private float _repriseRest = float.PositiveInfinity;
        private Phase _phase;
        private float _presence;
        private float _suppress;
        private float _age;
        private float _praise;
        private float _gap;
        private float _held;

        //Which step of a two-step card is up (#930), how long it has stood, and how long the first step's hold has been let go
        private int _step;
        private float _stepAge;
        private float _released;
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

        //The colour the dead weight's card draws its ball in (#1023): the ball that fired it, or zero before one has
        private BallType _deadColour;

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
            : 0f;

        /// <summary>
        /// The praise word once the action has been done, or null. <b>It stands beside the instruction rather
        /// than in its place since #466</b>: the caption and the detail line stay legible under it and the
        /// whole card leaves together, where before the praise replaced the caption and took the detail line
        /// away entirely — so the one moment the player had earned the right to re-read the lesson was the
        /// moment it disappeared.
        /// </summary>
        internal string Praise => Praising && _card != null ? _card.Praise : null;

        /// <summary>
        /// The kind of the ball the card up draws, or null for a card with none (#735): the HUD reserves the glyph's place for
        /// it and the session draws the ball there, live, in the frame - see <see cref="Definition.Kind"/>. The kind a kind
        /// card introduces, and an ordinary ball on the dead weight's card (#1023, <see cref="CardBallIsDead"/>).
        /// </summary>
        internal BallKind? CardKind => _card?.DeadBall == true ? BallKind.Normal : _card?.Kind;

        /// <summary>
        /// The colour of the card's ball (<see cref="CardKind"/>): the first one of its kind on this level's map, or on the
        /// dead weight's card the ball that fired it.
        /// </summary>
        internal BallType CardColour
        {
            get
            {
                if (_card?.DeadBall == true) return _deadColour != 0 ? _deadColour : BallType.Type1;

                return _card?.Kind is BallKind kind && (int)kind < _kindColours.Length && _kindColours[(int)kind] != 0
                    ? _kindColours[(int)kind]
                    : BallType.Type1;
            }
        }

        /// <summary>
        /// The card's ball is drawn as dead weight (#1023): dithered as the map dithers a ball that has come to rest above the
        /// field's floor, and still - see <see cref="Definition.DeadBall"/>.
        /// </summary>
        internal bool CardBallIsDead => _card?.DeadBall == true;

        /// <summary>
        /// Whether a card is up, arriving, leaving, or still waiting its turn in the queue: the player has something to read or
        /// do, which the "fire" hint must not talk over (#925). A contextual lesson that is merely armed does not count - it is
        /// not a card until its event fires.
        /// </summary>
        internal bool CardStands => _card != null || _queue.Count > 0;

        /// <summary>
        /// Whether the player has been taught <paramref name="lesson"/>, in the save or in this run: the session asks it of the
        /// lean's card before it arms the precise-aim hint (#1012), which reminds of what that card taught.
        /// </summary>
        internal bool HasBeenTaught(Lesson lesson)
        {
            foreach (Definition definition in DEFINITIONS)
                if (definition.Lesson == lesson) return _taughtThisRun.Contains(definition.Key) || InSave(definition);

            return false;
        }

        /// <summary>
        /// Whether <paramref name="lesson"/> is armed on this level, waiting for <see cref="Trigger"/> (#936): the session asks
        /// before it works out the moment, so a frame with nothing to fire pays for nothing.
        /// </summary>
        internal bool IsArmed(Lesson lesson)
        {
            for (int i = 0; i < _armed.Count; i++)
                if (_armed[i].Lesson == lesson) return true;

            return false;
        }

        /// <summary>
        /// The card that names the line (<see cref="Lesson.LineRule"/>) is on screen and being read (#933): arriving or up, not
        /// leaving, not praised, not stepped aside. The session blinks the floor net at its real place for as long as this
        /// holds, so the red line the card names is a red line the player has seen; it goes out with the card, whatever took
        /// it down (read out, the Tutorial row off, a decided level).
        /// </summary>
        internal bool ShowsTheLine => IsUp(Lesson.LineRule);

        /// <summary>
        /// Whether a level shows the net at its start (#933): the level the line's card is first taught on and every level
        /// after it, in the chapter it belongs to and every later one the player reaches (<see cref="TryPlace"/> gives the
        /// placement of a level of any chapter, whether or not the ladder reaches it). Not before: the first levels do not
        /// mention the line, and a player who has not reached it has no use for the sight of it. <paramref name="chapter"/> is
        /// −1 for a level that has no placement at all (no level set), which is none.
        /// </summary>
        internal static bool NetIsShownAt(int chapter, int levelInChapter, int chapterLength)
        {
            if (chapter < 0) return false;
            if (chapter > 0) return true;

            return levelInChapter >= Math.Max(0, chapterLength + LINE_RULE_FROM_LEVEL);
        }

        /// <summary>Whether the last input was the pad: what any prompt outside the cards asks to pick its glyph (#499).</summary>
        internal bool OnGamepad => _device == Device.Gamepad;

        /// <summary>The glyph for the button that skips a cinematic — the fire button, which is what skips (#499).</summary>
        internal string SkipGlyph => SkipGlyphFor(_device == Device.Gamepad);

        /// <summary>The skip's glyph for a hand, where no tutorial is watching the devices (#861: the Scene page's tour).</summary>
        internal static string SkipGlyphFor(bool gamepad) => gamepad ? PAD_RIGHT_TRIGGER : MOUSE_LEFT;

        /// <summary>The prompt font's glyphs for the card — a keycap, a mouse, a trigger — or null for none.</summary>
        internal string Glyph => _card == null ? null : Words(Face, _device == Device.Gamepad).Glyph;

        /// <summary>The line of what to do. It stays up through the praise since #466 — see <see cref="Praise"/>.</summary>
        internal string Caption
        {
            get
            {
                if (_card == null) return null;
                if (_card.Lesson == Lesson.Ceiling) return _ceilingCaption;

                return Words(Face, _device == Device.Gamepad).Caption;
            }
        }

        /// <summary>The smaller second line, or null for a one-line card. It stays up through the praise too (#466).</summary>
        internal string Detail => _card == null ? null : Words(Face, _device == Device.Gamepad).Detail;

        /// <summary>
        /// The words the card shows now (#930): the lesson's own, or its second step's once the first has been done. The
        /// praise, the kind and the clocks are always the lesson's.
        /// </summary>
        private Definition Face => _step > 0 && _card.Second != null ? _card.Second : _card;

        /// <summary>Which step of the card is up, 0 for the first (and for every one-step card) - see <see cref="Definition.Second"/>.</summary>
        internal int Step => _step;

        /// <summary>
        /// The lesson whose card is up and still asking - arriving or shown, not praising or leaving - while the player's hand is on
        /// the keyboard and mouse, or null. The keyboard's lights blink the keys it asks for (#987, <c>KeyLightModel.KeysFor</c>).
        /// </summary>
        internal Lesson? AskingLesson =>
            _card != null && _device == Device.KeyboardMouse && (_phase == Phase.Arriving || _phase == Phase.Shown) ? _card.Lesson : null;

        /// <summary>
        /// True once per completed action or finished step, on the frame it happened — the session plays the chime and kicks
        /// the praise word's spring off it (the whole card's, for a card turning to its second step, which has no praise
        /// row), and the read clears it.
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
        /// <param name="swapOffered">Whether the level grants a Swap (#213): the Swap's card is offered where it does (#705).</param>
        /// <param name="brakeOffered">Whether it grants a Brake: the Brake's card is armed for the glass's first step.</param>
        /// <param name="cutOffered">Whether it grants a Cut: the Cut's card is offered.</param>
        /// <param name="retry">Whether this is a retry of the level just played (Retry, Restart). A retry remembers the cards
        /// this run went through; any other start of a level is an entry into it, and shows its own cards again (#715).</param>
        /// <param name="kindsOnMap">The level's map by kind (#735), indexed by <see cref="BallKind"/>: the colour of the first
        /// ball of that kind, or zero for a kind the map does not carry. Its kind cards are offered on any level, chapter or
        /// none.</param>
        /// <param name="aimHintArmed">Whether the precise-aim hint is armed on this level (#1012).</param>
        /// <param name="deadWeightArmed">Whether the dead weight's card may be fired on this level (#1023): every level of
        /// play until it is taught, never the benchmark's.</param>
        internal void BeginLevel(int chapter, int levelInChapter, int chapterLength, int? ceilingStep, bool swapOffered = false,
            bool retry = false, ReadOnlySpan<BallType> kindsOnMap = default, bool brakeOffered = false, bool cutOffered = false,
            bool aimHintArmed = false, bool deadWeightArmed = false)
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

            //And the tools' (#705), for the same reason: the level that first grants one is past the ladder's chapters
            QueueTools(swapOffered, brakeOffered, cutOffered);

            //And the precise-aim hint (#1012), wherever the session arms it - on any level, the ladder's or none; the reel
            //queues it so it can be photographed
            ArmHints(aimHintArmed);

            //And the dead weight's card (#1023), likewise on any level, for the first ball that comes to rest wearing the mark
            ArmDeadWeight(deadWeightArmed);

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
                if (Chapterless(lesson)) continue;
                if (!_demo && !Eligible(lesson, chapter, levelInChapter, chapterLength)) continue;

                //A level's own cards come back on every entry into it (#715), the save notwithstanding - unless this
                //attempt's run already went through them (a retry)
                if (!_force && OwnLevel(lesson, chapter, levelInChapter, chapterLength) && InSave(lesson))
                    _replaying.Add(lesson.Key);

                if (Taught(lesson)) continue;
                if (lesson.Lesson == Lesson.Ceiling && _ceilingCaption == null) continue;

                //The reel has no events to wait for, so its contextual cards are queued like the rest
                if (lesson.Contextual && !_demo) _armed.Add(lesson);
                else _queue.Add(lesson);
            }

            if (!_demo) NothingAfterTheSendOff();

            //After the closing card's rules, which would otherwise take an armed ladder card for a contextual one still owed
            //and queue it ahead of itself: a lesson that is also armed for its moment (#936) keeps that on its own level
            if (!_demo) ArmReprises(chapter, levelInChapter, chapterLength);

            _gap = FIRST_CARD_DELAY;
        }

        /// <summary>
        /// Arms the lesson that is also a contextual card for its moment (<see cref="Definition.Reprise"/>, #936) on the level it
        /// belongs to: at once when it is not about to be shown as the level opens (a retry that has been through it), and
        /// when the opening showing has been read and has rested (<see cref="Retreat"/>, <see cref="Update"/>) when it is, so
        /// the same card is never met twice running. Every attempt at the level arms it again - an entry, as the level's own cards
        /// are shown again (#715), and a retry that has been through the opening showing, which has none to wait for.
        /// </summary>
        private void ArmReprises(int chapter, int levelInChapter, int chapterLength)
        {
            foreach (Definition lesson in DEFINITIONS)
            {
                if (!lesson.Reprise || !OwnLevel(lesson, chapter, levelInChapter, chapterLength)) continue;

                if (_queue.Contains(lesson)) _repriseWaits = lesson;
                else _armed.Add(lesson);
            }
        }

        /// <summary>
        /// Offers the card of every tool this level grants that the player has not been taught (#705): the Swap's and the
        /// Cut's queued, the Brake's armed for the glass's first pressure step. A card about a key that does nothing would
        /// be a lie, so a level that grants none - a set with no chapters, the testing argument's zero - offers none. The
        /// reel queues all three.
        /// </summary>
        private void QueueTools(bool swapOffered, bool brakeOffered, bool cutOffered)
        {
            foreach (Definition lesson in DEFINITIONS)
            {
                if (!lesson.Tool || Taught(lesson)) continue;

                bool offered = lesson.Lesson switch
                {
                    Lesson.Swap => swapOffered,
                    Lesson.Brake => brakeOffered,
                    Lesson.Cut => cutOffered,
                    _ => false,
                };

                if (_demo) _queue.Add(lesson);
                else if (!offered) continue;
                else if (lesson.Contextual) _armed.Add(lesson);
                else _queue.Add(lesson);
            }
        }

        /// <summary>
        /// Arms the hints (<see cref="Definition.Hint"/>, #1012) for this level when the session asks for them, whatever the
        /// save says, since a hint is never recorded; the reel queues them.
        /// </summary>
        private void ArmHints(bool armed)
        {
            foreach (Definition lesson in DEFINITIONS)
            {
                if (!lesson.Hint) continue;

                if (_demo) _queue.Add(lesson);
                else if (armed) _armed.Add(lesson);
            }
        }

        /// <summary>
        /// Arms the dead weight's card (<see cref="Definition.DeadBall"/>, #1023) when the session asks and the player has not
        /// been taught it, for <see cref="NoteDeadWeight"/> to fire; the reel queues it, drawn in the first colour.
        /// </summary>
        private void ArmDeadWeight(bool armed)
        {
            foreach (Definition lesson in DEFINITIONS)
            {
                if (!lesson.DeadBall || Taught(lesson)) continue;

                if (_demo) _queue.Add(lesson);
                else if (armed) _armed.Add(lesson);
            }
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
        /// Keeps the send-off (<see cref="Lesson.ThrowAway"/>) the last card of its chapter's ladder (#605). The
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
                if (lesson.Lesson == Lesson.ThrowAway) sendOffLesson = lesson;

            if (sendOffLesson == null) return;

            //The send-off closes ITS chapter's ladder and nothing else (#666): the score's lessons after it are the
            //second chapter's own, so only the first chapter's are cut or pulled ahead of it.
            int closes = sendOffLesson.Chapter;

            //Sent off already: nothing of that chapter is OWED any more - but a level's own cards shown again on
            //entering it (#715) are not owed, they are this level's, and they stay. Asked of the save and the run, not
            //of Taught: on Amphora the send-off is itself a replay, and read as untaught it would bring back every
            //lesson of the chapter still missing from the save, queued ahead of it (the review of #715)
            if (_taughtThisRun.Contains(sendOffLesson.Key) || (!_force && InSave(sendOffLesson)))
            {
                for (int i = _queue.Count - 1; i >= 0; i--)
                    if (OnLadder(_queue[i], closes) && !_replaying.Contains(_queue[i].Key)) _queue.RemoveAt(i);
                for (int i = _armed.Count - 1; i >= 0; i--)
                    if (OnLadder(_armed[i], closes) && !_replaying.Contains(_armed[i].Key)) _armed.RemoveAt(i);
                return;
            }

            int sendOff = -1;
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i].Lesson == Lesson.ThrowAway) { sendOff = i; break; }

            if (sendOff < 0) return;

            //A LATER CHAPTER'S LESSONS WAIT FOR IT (#666). Owed on a later chapter's level — Amphora skipped, or lost
            //before its cards came up — the send-off shares that level with the score's cards, and the streak's,
            //contextual, would interrupt it the moment it lit. Neither is taught here: they are offered on the next
            //level, after the player has been sent off, which is the order the chapters promise.
            for (int i = _queue.Count - 1; i >= 0; i--)
                if (!Chapterless(_queue[i]) && _queue[i].Chapter > closes) _queue.RemoveAt(i);
            for (int i = _armed.Count - 1; i >= 0; i--)
                if (!Chapterless(_armed[i]) && _armed[i].Chapter > closes) _armed.RemoveAt(i);

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
        private static bool OnLadder(Definition lesson, int chapter) => !Chapterless(lesson) && lesson.Chapter == chapter;

        /// <summary>
        /// A kind's card (#735), a tool's (#705), a hint (#1012) or the dead weight's (#1023): offered where the thing is met,
        /// at no chapter of the ladder.
        /// </summary>
        private static bool Chapterless(Definition lesson) => lesson.Kind != null || lesson.Tool || lesson.Hint || lesson.DeadBall;

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
            _step = 0;
            _stepAge = 0f;
            _released = 0f;
            _travel = 0f;
            _aimBaselined = false;
            _praiseCue = false;
            _praised = false;
            _hasLevel = false;
            _ceilingCaption = null;
            _repriseWaits = null;
            _repriseRest = float.PositiveInfinity;
            _deadColour = 0;

            _queue.Clear();
            _armed.Clear();
            _replaying.Clear();
        }

        //A replayed card (#715) counts as untaught for this level, so the send-off's rules treat it as the card it is
        private bool Taught(Definition lesson) =>
            _taughtThisRun.Contains(lesson.Key) || (!_force && !_replaying.Contains(lesson.Key) && InSave(lesson));

        //The save holds the lesson - under its key, or under the one an earlier build recorded it by (#936)
        private bool InSave(Definition lesson) =>
            _wasTaught(lesson.Key) || (lesson.LegacyKey != null && _wasTaught(lesson.LegacyKey));

        //Recorded once, and never again for a lesson the save already holds: a replay writes nothing (#715). Held under its
        //own key: a lesson the save has only under its legacy key is written under the new one when it is read (#936)
        private void Teach(Definition lesson)
        {
            //A hint is never taught for good (#1012): it comes back while it is needed
            if (lesson.Hint) return;

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

            //The rest of a card that is also armed for its moment (#936): once it is over, the moment may fire the card
            if (_repriseWaits != null && _repriseRest < float.PositiveInfinity && (_repriseRest -= elapsed) <= 0f)
            {
                _armed.Add(_repriseWaits);
                _repriseWaits = null;
            }

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
                    _stepAge += elapsed;
                    _presence = MathF.Min(1f, _presence + elapsed / ARRIVE_SECONDS);

                    if (_presence >= 1f) _phase = Phase.Shown;
                    break;

                case Phase.Shown:
                    _age += elapsed;
                    _stepAge += elapsed;

                    //An action card gives up rather than nagging, and is NOT recorded: it comes back on the
                    //next level of the chapter. An informational one has been read by now and is. The reel
                    //counts every card done on its own clock, each step of a two-step card on its own (#930).
                    if (_demo)
                    {
                        if (_stepAge >= DEMO_SECONDS)
                        {
                            if (_card.Second != null && _step == 0) AdvanceStep();
                            else if (_card.Action) Complete();
                            else Retreat(taught: true);
                        }
                    }
                    else if (_card.Action)
                    {
                        //A tool's card that times out has been read even if its key was not pressed (#705, the review): it
                        //belongs to no chapter, so unrecorded it would come back on every level of the campaign - the chip
                        //stays on screen as the reminder. A ladder's action card comes back, within its chapter, as before
                        if (_age >= (_card.Hint ? INFO_SECONDS : ACTION_TIMEOUT)) Retreat(taught: _card.Tool);
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
            _phase = Phase.Arriving;
            _presence = 0f;
            _age = 0f;
            _praise = 0f;
            _held = 0f;
            _step = 0;
            _stepAge = 0f;
            _released = 0f;
            _travel = 0f;
            _aimBaselined = false;
            _praised = false;
        }

        /// <summary>The card goes, recorded or not — see the caller for which.</summary>
        private void Retreat(bool taught)
        {
            if (_card == null) return;
            if (taught) Teach(_card);

            //The opening showing of a card that is also armed for its moment has been read: the moment may fire it once the
            //card has rested (Definition.Reprise, REPRISE_REST_SECONDS)
            if (_repriseWaits == _card) _repriseRest = REPRISE_REST_SECONDS;

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

        /// <summary>
        /// The action on the card was done: recorded, and the praise joins the instruction under it. A two-step card (#930) is
        /// done when its <b>last</b> step is: the first turns the card to the second (<see cref="AdvanceStep"/>) and records
        /// nothing, so a lesson left half done is owed again exactly as an action that timed out is.
        /// </summary>
        private void Complete()
        {
            if (_card == null || !_card.Action || _phase is Phase.Praising or Phase.Leaving) return;
            if (_card.Second != null && _step == 0) return;

            Teach(_card);

            _phase = Phase.Praising;
            _praise = 0f;
            _praised = true;
            _praiseCue = true;
        }

        /// <summary>
        /// The first step of a two-step card is done (#930): the card turns to the second in place, with the same cue a done
        /// action gives (the chime, the whole card's kick - there is no praise row to take it), because the player has just
        /// done something and should hear it.
        /// </summary>
        private void AdvanceStep()
        {
            _step = 1;
            _stepAge = 0f;
            _held = 0f;
            _released = 0f;
            _praiseCue = true;
        }

        /// <summary>The second step's hold was let go: the card is the first step again, which says what to hold. No cue.</summary>
        private void BackStep()
        {
            _step = 0;
            _stepAge = STEP_MIN_SECONDS;
            _held = 0f;
            _released = 0f;
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
            //A two-step card reads its holds through NoteTwoStepHold (#930)
            if (!IsUp(lesson) || _card.Second != null) return;

            _held = held ? _held + elapsed : 0f;

            if (_held >= HOLD_SECONDS) Complete();
        }

        /// <summary>
        /// A lesson of two steps, each a hold (#930): the precise aim's button, then a movement kept up <i>with</i> it. The
        /// first step is done after <see cref="HOLD_SECONDS"/> of <paramref name="first"/> unbroken, once it has stood
        /// <see cref="STEP_MIN_SECONDS"/>, and the card turns to the second; the lesson is done after
        /// <see cref="HOLD_SECONDS"/> of <paramref name="both"/>, which has to be the two facts of one frame - alternating the
        /// button and the movement is not the gesture. If the first is let go for <see cref="STEP_BACK_SECONDS"/> the card
        /// shows the first step again, since only that one says what to hold.
        /// </summary>
        /// <param name="first">The first step's hold this frame (the precise aim's button).</param>
        /// <param name="both">The second step's hold this frame (the button and a movement).</param>
        internal void NoteTwoStepHold(Lesson lesson, bool first, bool both, float elapsed)
        {
            //The reel counts each step on its own clock (Update): a window that has the focus reads no button held, and
            //would send the second step straight back to the first
            if (_demo || !IsUp(lesson) || _card.Second == null) return;

            if (_step == 0)
            {
                _held = first ? _held + elapsed : 0f;
                if (_held >= HOLD_SECONDS && _stepAge >= STEP_MIN_SECONDS) AdvanceStep();

                return;
            }

            _released = first ? 0f : _released + elapsed;
            if (_released >= STEP_BACK_SECONDS)
            {
                BackStep();
                return;
            }

            _held = both ? _held + elapsed : 0f;
            if (_held >= HOLD_SECONDS) Complete();
        }

        /// <summary>
        /// An action that is its own event — a shot fired, a group dropped — done if it is the card up. A tool used before
        /// its card came up (#705, the review: the key pressed in the first-card delay, or behind a kind's card) is taught
        /// then and there: the player knows it, and the card would only stand over a spent chip. A ladder's lesson reported
        /// while another card is up is ignored, as ever - a shot fired under the aim card does not complete the fire card.
        /// </summary>
        internal void Report(Lesson lesson)
        {
            if (IsUp(lesson))
            {
                Complete();
                return;
            }

            for (int i = _queue.Count - 1; i >= 0; i--)
                if (_queue[i].Lesson == lesson && _queue[i].Tool)
                {
                    Teach(_queue[i]);
                    _queue.RemoveAt(i);
                }

            for (int i = _armed.Count - 1; i >= 0; i--)
                if (_armed[i].Lesson == lesson && _armed[i].Tool)
                {
                    Teach(_armed[i]);
                    _armed.RemoveAt(i);
                }
        }

        /// <summary>
        /// Whether the throw-away lesson's moment has come (#936): the floor alarm's net is on - the cluster is within
        /// <c>LASER_WARN_STEPS</c> ceiling steps of the line - and the round in the bore has nowhere to score
        /// (<c>MatchReach</c>'s certain "none", so the card never says "throw it away" while a match is to be had; a wildcard
        /// or a cutter always counts as able). Pure, over the two facts the session reads, so the tests hold it without one.
        /// </summary>
        internal static bool ThrowAwayIsDue(bool netOn, bool boreCanScore) => netOn && !boreCanScore;

        /// <summary>
        /// A contextual lesson's event has happened. Ignored unless the lesson is armed for this level (so it
        /// fires once a level at most, and never for one already taught); otherwise it goes to the front of
        /// the queue, and a card already up gives way to it and comes back straight after - a hint (#1012) and the dead
        /// weight's card (#1023) excepted, which are shown after the card up. A hint stays armed, so it fires as often as
        /// it is due, but not while it is up or already waiting its turn. Returns whether the card was put up.
        /// </summary>
        internal bool Trigger(Lesson lesson)
        {
            int index = -1;

            for (int i = 0; i < _armed.Count; i++)
                if (_armed[i].Lesson == lesson) { index = i; break; }

            if (index < 0) return false;

            Definition triggered = _armed[index];

            if (!triggered.Hint) _armed.RemoveAt(index);
            else if (_card == triggered || _queue.Contains(triggered)) return false;

            //The card up returns behind the event's own card - unless that card is a hint (#1012), which waits behind the
            //card up instead: a reminder never pushes aside a card the player has not read. Nor does the dead weight's
            //(#1023): the ball it is about stays where it came to rest
            if (!triggered.Hint && !triggered.DeadBall && _phase is (Phase.Arriving or Phase.Shown)) StepAside();

            _queue.Insert(0, triggered);
            return true;
        }

        /// <summary>
        /// A ball has come to rest above the field's floor wearing the dead weight's mark (#1023): fires the dead weight's
        /// card, its ball drawn in <paramref name="colour"/>, if the card is armed on this level - so the first such ball of a
        /// level is the one the card shows, and a later one changes nothing.
        /// </summary>
        internal void NoteDeadWeight(BallType colour)
        {
            if (!IsArmed(Lesson.DeadWeight)) return;

            _deadColour = colour;
            Trigger(Lesson.DeadWeight);
        }

        #endregion
    }
}
