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

        /// <summary>
        /// Grants this level's starting charges. <b>One Swap a level from the second chapter (#213)</b>, by
        /// <see cref="Prazsky.BS3D.Levels.LevelSet.SwapChargesAt"/> — a rule of the campaign and not a field of each
        /// level, so none of the 130 shipped files carries one — and, on top of it, the testing argument
        /// (<c>powerups=swap:1</c>, <see cref="SessionTestOptions.ForcedPowerups"/>) in <c>wildcard=</c>'s own shape,
        /// which is how the mechanism was reachable and verifiable before any level granted it (#392). Called from
        /// <c>BuildLevel</c>, which is also what a retry runs, so a retried level is granted exactly what it was
        /// granted the first time.
        /// <para>
        /// Parsed leniently, the way every testing argument in this file is: an entry naming a kind that does
        /// not exist yet (a future Rainbow or Bomb ahead of the issue that adds it, or a typo) is dropped
        /// rather than thrown over, and the rest of the list still stands.
        /// </para>
        /// </summary>
        private void GrantPowerupCharges(int index)
        {
            //Onto the run's own array (LevelRun.PowerupCharges), which a new level starts at zero by construction.
            //The campaign's own grant first (#213: one Swap a level from the second chapter), then the testing
            //argument on top of it — which assigns, so powerups=swap:3 is three and powerups=swap:0 is none.
            _run.PowerupCharges[(int)PowerupKind.Swap] = LevelSwapCharges(index);

            string spec = _test.ForcedPowerups;

            if (!string.IsNullOrEmpty(spec))
            {
                foreach (string entry in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = entry.Split(':');
                    if (parts.Length != 2) continue;

                    if (!Enum.TryParse(parts[0], ignoreCase: true, out PowerupKind kind) || kind == PowerupKind.None)
                        continue;

                    if (int.TryParse(parts[1], out int count) && count >= 0) _run.PowerupCharges[(int)kind] = count;
                }
            }

            _run.SwapOffered = _run.PowerupCharges[(int)PowerupKind.Swap] > 0;
        }

        /// <summary>
        /// How many Swap charges the level at <paramref name="index"/> is granted by the campaign (#213), 0 for a level
        /// in no set. Asked at install, and again by the HUD's chip to tell "spent" from "this level offers none".
        /// </summary>
        private int LevelSwapCharges(int index) => Game.LevelSet?.SwapChargesAt(index) ?? 0;

        /// <summary>How many charges of <paramref name="kind"/> are left — what the HUD's chip reads for its count (#213).</summary>
        internal int PowerupCharges(PowerupKind kind) => _run.PowerupCharges[(int)kind];

        /// <summary>
        /// Whether this level offers a Swap at all, spent or not (#213): the chip is drawn on every level that grants
        /// one and on none that does not, so a first chapter shows nothing to press. The campaign's grant or the
        /// testing argument's, whichever this level was built with.
        /// </summary>
        internal bool OffersSwap => _run.SwapOffered;

        /// <summary>
        /// Whether <paramref name="kind"/> can fire right now: a charge left, and the same two guards
        /// <c>Shoot</c> already states for itself — not mid a camera takeover
        /// (<see cref="CameraTakeoverEngaged"/>) and not once the level is decided (<see cref="LevelDecided"/>).
        /// </summary>
        internal bool CanActivate(PowerupKind kind) =>
            kind != PowerupKind.None && _run.PowerupCharges[(int)kind] > 0 && !CameraTakeoverEngaged && !LevelDecided;

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

            _run.PowerupCharges[(int)kind]--;

            switch (kind)
            {
                case PowerupKind.Swap:
                    _magazine.SwapSlots(SWAP_SLOT_A, SWAP_SLOT_B);

                    //Heard and taught (#213): the mechanism's own click, dry and unplaced like the UI's (it is the
                    //player's hand on the gun, not something out in the scene), and the tutorial card up, if it is
                    //this one, is done
                    Game.Audio.PlayUiClick();
                    _tutorial.Report(Tutorial.Lesson.Swap);
                    break;
            }
        }

        /// <summary>
        /// The press of the swap key (or the pad's X): activates a Swap when one can fire, and says no when this level
        /// offers one and it is spent — the same dry "no" the gun answers a refused shot with, so a player who
        /// pressed it a second time hears that they did, rather than nothing. Silent on a level that offers none:
        /// a first-chapter player pressing E has nothing to be refused.
        /// </summary>
        private void PressSwap()
        {
            if (CanActivate(PowerupKind.Swap)) Activate(PowerupKind.Swap);
            else if (_run.SwapOffered && !CameraTakeoverEngaged && !LevelDecided) Game.Audio.PlayShotRefused();
        }
    }
}
