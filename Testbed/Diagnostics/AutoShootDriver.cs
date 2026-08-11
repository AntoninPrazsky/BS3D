using Microsoft.Xna.Framework;
using System;

namespace Testbed.Diagnostics
{
    /// <summary>
    /// The <c>autoshoot</c> harness: one ball a second at a random spot over the arena, for ever. It is what
    /// makes a frame-rate reading mean anything — a Testbed sitting still draws a static cluster, while this
    /// keeps balls in flight, contacts resolving and clusters releasing, which is the load the game actually has.
    /// <para>
    /// The <b>cadence and the target box are the harness's</b>; the shot and its log line are the caller's, and
    /// that split is deliberate rather than tidy. <c>[autoshoot] FPS: …, balls drawn: …</c> is a documented CLI
    /// surface (<c>.claude/skills/verify</c> greps it), and every figure on it — the frame rate, the drawn count,
    /// the LOD split — belongs to the executable's own renderers, so the line stays where those live.
    /// </para>
    /// </summary>
    public sealed class AutoShootDriver
    {
        private const float INTERVAL_SECONDS = 1f;

        /// <summary>
        /// The box the random target is drawn from: over the arena, up where a cluster hangs, wide enough that
        /// shots land across the field rather than all into one column. Deliberately not derived from the loaded
        /// map — a fixed box means two runs on two maps are shooting the same shots.
        /// </summary>
        private const int MIN_XZ = -4, MAX_XZ = 5, MIN_Y = 4, MAX_Y = 11;

        //Its own generator, so the sequence a run fires does not shift with whatever else asks the game for a
        //random number (the magazine's next colour does, every shot)
        private static readonly Random RANDOM = new();

        //Captured once at construction, not written at the call site: a method group or lambda evaluated per
        //frame allocates a fresh delegate every time (BestPractices.md §3)
        private readonly Action<Vector3> _fire;

        private float _elapsed;

        /// <param name="fire">Shoots at the given spot and reports — see the class remarks on why the report is
        /// the caller's.</param>
        public AutoShootDriver(Action<Vector3> fire) => _fire = fire;

        /// <summary>
        /// Advances the cadence and fires when it comes round. One call from the caller's update, inside
        /// whatever gate it wants the harness to run under (the Testbed's is <c>_simulate</c>, so F5 holds the
        /// rain along with the physics it is there to load).
        /// </summary>
        public void Update(float elapsedSeconds)
        {
            _elapsed += elapsedSeconds;
            if (_elapsed < INTERVAL_SECONDS) return;

            _elapsed = 0f;
            _fire(new Vector3(RANDOM.Next(MIN_XZ, MAX_XZ), RANDOM.Next(MIN_Y, MAX_Y), RANDOM.Next(MIN_XZ, MAX_XZ)));
        }
    }
}
