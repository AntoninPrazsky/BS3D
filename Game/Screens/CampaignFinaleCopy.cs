using System;

namespace BS3D.Screens
{
    /// <summary>
    /// What the campaign's last clear says to the player once CAMPAIGN COMPLETE has stood on the screen (#880), kept apart
    /// from the page so a test can hold it: every string to the typesetting rules (<see cref="CopyRules"/>, "Typesetting" in
    /// <c>docs/game-shell/menu-look.md</c>), every message to the same three parts, and every hint to an easter egg that
    /// exists (<see cref="Egg"/>, <c>CampaignFinaleCopyTests</c>).
    /// <para>
    /// <b>A message is two paragraphs</b>: the thanks, which also says that more levels and features are coming, in its own
    /// words each time; and <b>exactly one hint</b>, a whimsical clue in the manner of a detective story. The hint is a
    /// riddle about <i>what to look at</i>, never an instruction: it names no key, no button and no action ("press",
    /// "click" and "type" are not in any hint, the test checks). One is picked at random each time the campaign is
    /// finished, which happens once a save (a replay of the last level is an ordinary clear).
    /// </para>
    /// <para>
    /// <b>The hints point only at eggs that exist</b>, and <see cref="Egg"/> is the list they are checked against: the six
    /// of #230 (<c>docs/game-feedback/easter-eggs.md</c>) and the About page's player of the original score. An egg removed
    /// from the game is removed from <see cref="Egg"/> first, and the message that hints at it then fails to compile.
    /// </para>
    /// </summary>
    internal static class CampaignFinaleCopy
    {
        /// <summary>The easter eggs a hint may point at.</summary>
        internal enum Egg
        {
            /// <summary>The gun nods off in a level nobody touches (<c>DozingGun</c>).</summary>
            SleepingGun,

            /// <summary>Misses, two hits and misses again answer with confetti (<c>SecretShotCode</c>).</summary>
            ShotCode,

            /// <summary>The Konami code on the main menu, which the game's 3D name answers (<c>KonamiCode</c>).</summary>
            Konami,

            /// <summary>On 30 December the gun wears a party hat and the title dances (<c>BS3DGame.Birthday</c>).</summary>
            Birthday,

            /// <summary>A nickname that is a secret word puts the party hat on the gun (<c>Nickname.IsSecretWord</c>).</summary>
            CodeWord,

            /// <summary>Three identical shots flying clean out tear open a wormhole (<c>RepeatedMiss</c>).</summary>
            Wormhole,

            /// <summary>The last page of About plays the original, procedural score (<c>AboutCopy.SCORE</c>).</summary>
            OriginalScore,
        }

        /// <summary>One message: the thanks with the promise of more, the hint, and the egg the hint is about.</summary>
        internal readonly record struct Message(string Thanks, string Hint, Egg Egg);

        /// <summary>The messages. Each says thanks, says more is coming, and hints at one egg.</summary>
        internal static readonly Message[] Messages =
        {
            new("Every last level, cleared. Thank you for playing all the way to the end. New levels and new features are "
                + "already being built, so stay tuned.",
                "A detective’s note from the case file: the barrel that never tires has, on rare nights, been found "
                + "drooping. The only witness is patience.",
                Egg.SleepingGun),

            new("That was the whole campaign, and you saw it through — thank you. More levels are on their way, along with "
                + "features nobody has seen yet. Keep an eye out.",
                "Count your misses the way a detective counts alibis: a few wasted, two well spent, then a few wasted again. "
                + "Somebody in the ceiling throws a party for the pattern.",
                Egg.ShotCode),

            new("Thanks for every shot, and for finishing what you started. The next levels and a few new tricks are "
                + "coming; stay tuned.",
                "The title above the menu has a ticklish memory for an incantation from the arcades of the eighties. "
                + "Whisper it at the front door and see who answers.",
                Egg.Konami),

            new("The campaign is complete, and so is a promise: thank you for playing. There will be new levels and new "
                + "features, so do come back.",
                "Between Christmas and the new year, one single day is a celebration in this house, and the gun dresses "
                + "for it. Consult the calendar.",
                Egg.Birthday),

            new("You have cleared them all, and this game owes you a thank you. More levels and more features are on "
                + "the way — stay tuned.",
                "Even a name can be a password. A few of them open a wardrobe: the ones the foundations of this game "
                + "answer to. Introduce yourself with one of those and watch what the gun wears.",
                Egg.CodeWord),

            new("Thank you for playing every level. New ones are in the works, and so are features you have not "
                + "met yet. Stay tuned.",
                "Three identical shots into the same empty sky. The sky, they say, dislikes being asked the same question "
                + "three times, and it has a way of showing it.",
                Egg.Wormhole),

            new("That is the end of the campaign — and thank you for every level of it. There is more to come: new "
                + "levels, new features. Stay tuned.",
                "Turn every page of the credits, to the very last one. It is not a page but a performance, and its "
                + "score was never recorded, only written.",
                Egg.OriginalScore),

            new("Campaign complete. Thank you, truly, for playing. New levels and new features are coming, and the "
                + "game will be waiting for you.",
                "The old front door is more forgiving than it looks. An arcade verse older than this game, whispered to "
                + "the menu, will make the lettering above it hop for joy.",
                Egg.Konami),
        };

        /// <summary>
        /// Every string the finale draws, for the typesetting test: <c>CAMPAIGN COMPLETE</c>, then each message's thanks and
        /// hint.
        /// </summary>
        internal static System.Collections.Generic.IEnumerable<string> Copy()
        {
            yield return TITLE;

            foreach (Message message in Messages)
            {
                yield return message.Thanks;
                yield return message.Hint;
            }
        }

        /// <summary>The headline, in the words the result page's verdict already uses (<c>LevelResult.Verdict</c>).</summary>
        internal const string TITLE = "CAMPAIGN COMPLETE";

        //A message is held for a base and a share per word: 4 words a second is a comfortable reader's pace over a dark
        //plate, and the thanks is the part a player has already half read when the hint arrives
        private const float HOLD_BASE = 3.0f;
        private const float HOLD_PER_WORD = 0.22f;
        private const float MIN_HOLD = 8.0f;

        /// <summary>The longest a message is held, whatever its length.</summary>
        internal const float MAX_HOLD = 16.0f;

        /// <summary>How long a message stays up unless skipped: a base and a share per word, within a floor and <see cref="MAX_HOLD"/>.</summary>
        internal static float HoldSeconds(Message message) =>
            Math.Clamp(HOLD_BASE + Words(message) * HOLD_PER_WORD, MIN_HOLD, MAX_HOLD);

        /// <summary>
        /// How many words a message has, for how long it is held: the thanks and the hint together.
        /// </summary>
        internal static int Words(Message message) =>
            Count(message.Thanks) + Count(message.Hint);

        private static int Count(string text) =>
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
