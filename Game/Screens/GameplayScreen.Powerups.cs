using System;

namespace BS3D.Screens
{
    /// <summary>
    /// A player-side, session-scoped resource the player activates by choice — orthogonal to both
    /// <see cref="Prazsky.BS3D.GameStructure.BallType"/> and <see cref="Prazsky.BS3D.GameStructure.BallKind"/>
    /// (#392). Activating one does not create a new kind of ball; it reaches into the existing gun/magazine
    /// machinery instead (a future direct-state kind, touching some other subsystem with no shot involved at
    /// all, is explicitly out of #392's own scope). A small byte enum, the same closed-set idiom every other
    /// axis in this codebase already is.
    /// </summary>
    internal enum PowerupKind : byte
    {
        /// <summary>Not a power-up — the charge array's own default, and what "nothing granted" reads as.</summary>
        None = 0,

        /// <summary>
        /// Exchanges the muzzle slot with the one behind it (#392's own proof of the activation contract,
        /// the shape with the fewest moving parts) — deferring the ball about to fire by one shot. The
        /// queue's only reorder; everything else this game does to a magazine changes a slot's colour, never
        /// its place.
        /// </summary>
        Swap = 1,
    }

    /// <summary>
    /// The activation contract every power-up hangs on (#392). This issue implements no power-up beyond
    /// <see cref="PowerupKind.Swap"/>, which stands here as the contract's own test — Rainbow, Bomb and
    /// whatever else #213 sketched each get their own issue and their own case in <see cref="Activate"/>.
    /// </summary>
    internal sealed partial class GameplayScreen
    {
        /// <summary>
        /// Which slots <see cref="PowerupKind.Swap"/> exchanges — the cheap, fixed operation the issue asks
        /// for rather than a picker: letting the player choose which two of five loaded slots to swap would
        /// be the first mouse-interactive HUD element this game has ever had, a materially bigger feature
        /// than the power-up itself. Deferring the ball about to fire by one shot.
        /// </summary>
        private const int SWAP_SLOT_A = 0;
        private const int SWAP_SLOT_B = 1;

        //One count per kind, sized off the enum itself so a future kind added to it needs no second number
        //kept in step by hand. Granted per level (GrantPowerupCharges, called from BuildLevel) and never
        //persisted to PlayerProgress — every other piece of session state (ScoreKeeper, the ceiling, the
        //streak) resets exactly this way on a retry, and an inventory that survived one would be the one
        //exception with no stated reason.
        private readonly int[] _powerupCharges = new int[Enum.GetValues(typeof(PowerupKind)).Length];

        /// <summary>
        /// Grants this level's starting charges. Not authored into any shipped or generated level — the
        /// testing argument (<c>powerups=swap:1</c>, <see cref="BS3DGame.ForcedPowerups"/>) is the only
        /// source today, in <c>wildcard=</c>'s own shape, so the mechanism is reachable and verifiable
        /// without touching a single one of the 105 shipped levels. Called from <c>BuildLevel</c>, which is
        /// also what a retry runs, so a retried level is granted exactly what it was granted the first time.
        /// <para>
        /// Parsed leniently, the way every testing argument in this file is: an entry naming a kind that does
        /// not exist yet (a future Rainbow or Bomb ahead of the issue that adds it, or a typo) is dropped
        /// rather than thrown over, and the rest of the list still stands.
        /// </para>
        /// </summary>
        private void GrantPowerupCharges()
        {
            Array.Clear(_powerupCharges);

            string spec = Game.ForcedPowerups;
            if (string.IsNullOrEmpty(spec)) return;

            foreach (string entry in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2) continue;

                if (!Enum.TryParse(parts[0], ignoreCase: true, out PowerupKind kind) || kind == PowerupKind.None)
                    continue;

                if (int.TryParse(parts[1], out int count) && count > 0) _powerupCharges[(int)kind] = count;
            }
        }

        /// <summary>How many charges of <paramref name="kind"/> are left — the seam a future HUD reads for
        /// its icon and count (#392 states the seam exists; the layout is that issue's, not this one's).</summary>
        internal int PowerupCharges(PowerupKind kind) => _powerupCharges[(int)kind];

        /// <summary>
        /// Whether <paramref name="kind"/> can fire right now: a charge left, and the same two guards
        /// <c>Shoot</c> already states for itself — not mid a camera takeover
        /// (<see cref="CameraTakeoverEngaged"/>) and not once the level is decided (<see cref="LevelDecided"/>).
        /// </summary>
        internal bool CanActivate(PowerupKind kind) =>
            kind != PowerupKind.None && _powerupCharges[(int)kind] > 0 && !CameraTakeoverEngaged && !LevelDecided;

        /// <summary>
        /// Spends one charge of <paramref name="kind"/> and applies its effect. A no-op, not an exception, on
        /// a kind <see cref="CanActivate"/> already refused — the caller is expected to have asked first,
        /// exactly as <c>Shoot</c>'s own callers check its guards, but the re-check here costs one comparison
        /// and closes the one door a future caller could walk through by mistake.
        /// <para>
        /// <b>Must never call <see cref="Prazsky.BS3D.Scoring.ScoreKeeper.Shot"/>, <c>Landed</c> or
        /// <c>Missed</c>, and must never feed <c>StepCeilingThisShot</c>'s cadence.</b> It is not a shot —
        /// <c>Shoot()</c> is the only method that calls any of the three today, and a power-up routed through
        /// it would silently spend budget and advance the ceiling for an action the player did not fire.
        /// </para>
        /// </summary>
        internal void Activate(PowerupKind kind)
        {
            if (!CanActivate(kind)) return;

            _powerupCharges[(int)kind]--;

            switch (kind)
            {
                case PowerupKind.Swap:
                    _magazine.SwapSlots(SWAP_SLOT_A, SWAP_SLOT_B);
                    break;
            }
        }
    }
}
