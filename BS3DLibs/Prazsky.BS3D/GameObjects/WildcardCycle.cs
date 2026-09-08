using Prazsky.BS3D.GameStructure;
using System;

namespace Prazsky.BS3D.GameObjects
{
    /// <summary>
    /// What colour a <see cref="BallKind.Wildcard"/> is showing right now (#330) — one clock, read by every
    /// surface that draws or asks about one.
    /// <para>
    /// <b>That it is ONE clock is the whole design.</b> A wildcard is visible in five places at once — the
    /// loaded queue in the bore, the round at the muzzle, the halo around it, the aim ghost in the cell it would
    /// land in, and the ball in flight — and the magazine's own history says what happens when those disagree:
    /// two testers read a colour off the wrong ball and fired expecting a match (#175). A per-ball phase would
    /// have every wildcard on screen cycling out of step, which is that failure with the colours moving. So the
    /// phase belongs to the <i>game</i>, not to a ball: every wildcard shows the same colour at the same
    /// instant, and the player reads one answer wherever they look.
    /// </para>
    /// <para>
    /// <b>It cycles among the colours still hanging</b>, which the caller feeds it (<see cref="SetColours"/>) —
    /// the magazine's own rule, and for the same reason: a wildcard is going to <i>become</i> the colour it is
    /// showing whenever it lands beside nothing matchable, and a colour that exists nowhere in the cluster would
    /// park an unmatchable ball on the field. That is exactly the cost the Game's transmute exists to prevent,
    /// arriving through the one ball the transmute cannot reach.
    /// </para>
    /// <para>
    /// It <b>never settles</b>, and that is deliberate too: the crossing runs continuously, so a wildcard is
    /// always mid-dissolve, where a transmuting ordinary ball crosses once over
    /// <c>GameplayScreen.TRANSMUTE_SECONDS</c> and then stands still. A ball that is still crossing a second
    /// later is a wildcard; that is the cue, and it costs nothing to read.
    /// </para>
    /// </summary>
    public sealed class WildcardCycle
    {
        /// <summary>
        /// How long one colour-to-colour crossing takes, in seconds. Slow enough not to strobe (about two
        /// changes a second, near a blinking cursor's pace) and fast enough that a player watching the muzzle
        /// for a moment sees it is cycling rather than merely tinted.
        /// </summary>
        public const float CROSSING_SECONDS = 0.5f;

        //The cycle's own order, and every colour by default - what the Testbed and any caller that never sets a
        //census gets. Sized to the enum so a new colour cannot outgrow it.
        private readonly BallType[] _colours = new BallType[BallTypes.Count];
        private int _count;

        private int _index;      //which colour the crossing is coming FROM
        private float _phase;    //0 at the start of that crossing, approaching 1 at its end

        public WildcardCycle()
        {
            for (int i = 0; i < _colours.Length; i++) _colours[i] = (BallType)(i + 1);

            _count = _colours.Length;
        }

        /// <summary>The colour the crossing is coming from.</summary>
        public BallType From => _colours[_index];

        /// <summary>The colour it is crossing into.</summary>
        public BallType To => _colours[(_index + 1) % _count];

        /// <summary>How far through that crossing, 0 to 1 — the dissolve's own progress.</summary>
        public float Progress => _phase;

        /// <summary>
        /// The one colour a wildcard reads as this instant: whichever half of the crossing holds most of the
        /// ball's pixels. It is what a tint has to use — the muzzle glow, the aim beam, the ghost — where a
        /// cross-fade cannot be drawn, and it is the colour a wildcard <b>becomes</b> when it lands beside
        /// nothing matchable, so what the player saw is what they get.
        /// </summary>
        public BallType Showing => _phase < 0.5f ? From : To;

        /// <summary>
        /// Sets the colours the cycle walks, in order, ignoring anything past the thirteen. An empty span is
        /// ignored — a cleared cluster leaves the cycle on whatever it was already showing rather than on
        /// nothing at all, and there is no wildcard left to fire at it anyway.
        /// <para>
        /// The phase is kept across the change so the crossing on screen does not jump, and the index is only
        /// clamped back into range: a level whose last blue ball has just gone sees the cycle stop offering
        /// blue on its next step rather than mid-dissolve.
        /// </para>
        /// </summary>
        public void SetColours(ReadOnlySpan<BallType> colours)
        {
            if (colours.Length == 0) return;

            _count = Math.Min(colours.Length, _colours.Length);

            for (int i = 0; i < _count; i++) _colours[i] = colours[i];

            if (_index >= _count) _index = 0;
        }

        /// <summary>
        /// Advances the crossing. <b>Give it the wall clock</b>, not the simulation's step, for
        /// <see cref="Magazine.Step"/>'s reason exactly: a wildcard cycling in the bore is the gun saying what
        /// is loaded, not something the physics is doing, so it goes on cycling while the simulation is paused
        /// or slowed.
        /// </summary>
        public void Step(float elapsedSeconds)
        {
            if (_count <= 1) return;    //one colour to cycle through is a ball, and a crossing to itself is a flicker

            _phase += elapsedSeconds / CROSSING_SECONDS;

            //A while rather than an if: a long frame (a level load, a breakpoint) must not leave the phase past
            //1 for the dissolve to read as a fully cut-away ball
            while (_phase >= 1f)
            {
                _phase -= 1f;
                _index = (_index + 1) % _count;
            }
        }
    }
}
