using Prazsky.BS3D.Input;

namespace Prazsky.BS3D.Scoring
{
    /// <summary>
    /// <b>The secret shot code</b> — another of #230's easter eggs, found only by playing it: three shots that miss,
    /// two that land, three more that miss. The session reports every shot as it resolves (a landing, or a shot
    /// spent on the stone or past everything) and the last eight are compared with that pattern; the Game
    /// answers a completed one with a short fall of the campaign's confetti and the party popper's crack. Nothing else — no ball, no
    /// score — so, like the sleeping gun, it touches nothing a gate measures; the six misses it costs are the price
    /// of finding it.
    /// </summary>
    public sealed class SecretShotCode : SecretCode<bool>
    {
        //Oldest first: M M M, L L, M M M. A shot reports true when it landed.
        public SecretShotCode() : base(false, false, false, true, true, false, false, false) { }
    }
}
