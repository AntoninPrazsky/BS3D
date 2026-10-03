using System;

namespace BS3D.Online
{
    /// <summary>What <see cref="NicknamePrompt.Decide"/> answered, named so the run's log can say why the plate did not come.</summary>
    internal enum NicknameAsk
    {
        /// <summary>Ask: nothing decided, a server to send to, a connection, and a player at the machine.</summary>
        Ask,

        /// <summary>The player has already answered, by a nickname, by Skip, by an Off in Settings or by removing their scores.</summary>
        Decided,

        /// <summary>A run a script drives, which has no one to ask.</summary>
        Scripted,

        /// <summary>No server resolves (a local build whose settings name none), so a nickname would have nowhere to go.</summary>
        NoServer,

        /// <summary>The machine is not connected to the internet, so the question waits for a launch that is.</summary>
        NoInternet,
    }

    /// <summary>
    /// The first-launch nickname question (#763): whether the game asks a player for a nickname for the leaderboards,
    /// and what an Esc on it counts for. Plain rules over plain values, with nothing of the window in them, which is
    /// what lets the tests hold them (this file is compiled into <c>BS3D.Tests</c>).
    /// <para>
    /// <b>The question is asked until it is answered, and an Esc is not an answer the first time.</b> A confirmed name
    /// turns the boards on and Skip turns them off, both at once; Esc leaves the setting undecided so the next launch
    /// asks again, and the second Esc counts as Skip (<see cref="DISMISSALS_BEFORE_SKIP"/>) - the question is put to a
    /// player at most twice without an answer.
    /// </para>
    /// </summary>
    internal static class NicknamePrompt
    {
        /// <summary>How many times the question may be dismissed with Esc before the dismissal is taken as an answer.</summary>
        internal const int DISMISSALS_BEFORE_SKIP = 2;

        /// <summary>
        /// Whether to ask, and if not why not. Cheapest gates first, and <paramref name="internetConnected"/> last and
        /// only when everything else says ask: it is a call into Windows, made at most once a launch and never for a
        /// player who has answered.
        /// </summary>
        /// <param name="online">The player's setting: null until they have answered either way (<c>GameSettings.Online</c>).</param>
        /// <param name="serverResolves">Whether a score server resolves for this build (<c>OnlineScores.TryResolveServer</c>).</param>
        /// <param name="scripted">Whether a script is driving this run, which has no one to ask.</param>
        /// <param name="internetConnected">Whether the machine is connected to the internet. Asked lazily; see <see cref="InternetCheck"/>.</param>
        internal static NicknameAsk Decide(bool? online, bool serverResolves, bool scripted, Func<bool> internetConnected)
        {
            if (online != null) return NicknameAsk.Decided;
            if (scripted) return NicknameAsk.Scripted;
            if (!serverResolves) return NicknameAsk.NoServer;

            return internetConnected() ? NicknameAsk.Ask : NicknameAsk.NoInternet;
        }

        /// <summary>
        /// One more Esc on the question. Returns whether this one is taken as Skip, which is when it is the
        /// <see cref="DISMISSALS_BEFORE_SKIP"/>th; <paramref name="dismissed"/> is the count to keep.
        /// </summary>
        internal static bool Dismiss(int dismissedBefore, out int dismissed)
        {
            //Saturating, so a hand-edited huge count cannot wrap round to negative and ask again
            dismissed = dismissedBefore < int.MaxValue ? Math.Max(0, dismissedBefore) + 1 : int.MaxValue;
            return dismissed >= DISMISSALS_BEFORE_SKIP;
        }
    }
}
