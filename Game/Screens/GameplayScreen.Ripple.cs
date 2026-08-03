using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The wave of light a landing sends through the cluster</b> — the balls touching the impact flare
    /// first, then the ones touching those, each fading as the next takes over.
    /// </summary>
    /// <remarks>
    /// It travels by <b>connectivity</b> and not by distance, which is the whole of why it looks right: a
    /// world-space radius would cross the holes a played cluster is full of as if they were not there, while a
    /// walk over balls that actually touch goes around them — including around the hole a matched group has
    /// just left. Its own fields sat 2 100 lines below every other field in the original, which is what a file
    /// outgrowing its layout looks like; #72 gave it a file. It reads <c>PhysicsBall[,,]</c> and
    /// <see cref="BallsMap.GetNeighboringCells"/> and writes state that already lives on
    /// <see cref="PhysicsBall"/>, so it is the cleanest of the extractions still owed —  into
    /// <c>Prazsky.BS3D.Physics</c>, taking the live array per level since <c>BuildCluster</c> replaces it
    /// wholesale.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        #region The ripple

        //A ball landing sends a wave of light out through the cluster: the balls touching the impact flare
        //first, then the ones touching those, and so on, each fading as the next takes over. It is what makes
        //the cluster read as a connected, living body rather than as a heap of independent spheres — the shot
        //does not just stick, the thing it stuck to answers.
        //
        //It travels by CONNECTIVITY and not by distance, which is the whole of why it looks right: a wave
        //evaluated from a world-space radius would cross the holes a played cluster is full of as if they were
        //not there, while a walk over balls that actually touch goes AROUND them — including around the hole
        //the matched group has just left, which is the most satisfying thing it does.

        //These three decide whether it reads as a WAVE or as a flash, and the ratio between them is the whole
        //of it: the lit band is as many balls wide as the flare's length divided by the hop delay. The first
        //build had a 0.36 s flare stepping every 0.045 s — a band nine balls deep against a reach of twelve,
        //which is very nearly the whole cluster alight at once, and on screen it read as the shot flashing the
        //lot rather than as anything travelling. Keep the band around three or four balls.

        /// <summary>Seconds between one ball flaring and the ones touching it taking their turn.</summary>
        private const float RIPPLE_HOP_SECONDS = 0.09f;

        /// <summary>How long one ball's flare lasts: a fast rise and a soft fall, so the band reads as a wave
        /// front with a tail rather than as a row of balls switching on and off.</summary>
        private const float RIPPLE_ATTACK_SECONDS = 0.05f;
        private const float RIPPLE_DECAY_SECONDS = 0.22f;

        /// <summary>
        /// How far the wave carries. Bounds the walk, and the flare's amplitude falls off across it so the
        /// ripple dies away instead of stopping at a hard ring of lit balls. Fourteen hops at the delay above
        /// is a bit over a second to cross a big cluster — long enough to watch it go, short enough that the
        /// next shot is not still waiting for it.
        /// </summary>
        private const int RIPPLE_MAX_HOPS = 14;

        //Hop count + 1 per cell, 0 meaning "not reached by this walk" — so it doubles as the visited mark and
        //needs only a clear between ripples rather than a second array. Reused rather than allocated per
        //landing, and sized to the field the level actually loaded.
        private int[,,] _rippleHops;
        private readonly Queue<XZLevel> _rippleQueue = new();

        /// <summary>
        /// Sends the wave out from the cell a ball has just landed in. A breadth-first walk over the balls that
        /// touch, so a ball's hop count is how many balls the light has to pass through to reach it — which is
        /// exactly the delay before it lights.
        /// <para>
        /// The origin cell seeds the walk whether or not a ball is still standing in it: a shot that completed
        /// a group is released along with it, so by the time this runs the cell it landed in is often empty and
        /// the wave has to start from the balls around the gap.
        /// </para>
        /// </summary>
        private void StartRipple(XZLevel origin)
        {
            if (!BeginRippleWalk(out XZLevel size)) return;

            _rippleHops[origin.X, origin.Z, origin.Level] = 1;      //reached, at hop 0
            _rippleQueue.Enqueue(origin);

            //The ball that landed, if it is still there, flares first and on its own — it is hop 0
            LightBall(_physicsBalls[origin.X, origin.Z, origin.Level], 0, alarm: false);

            WalkRipple(size, alarm: false);
        }

        /// <summary>
        /// The other wave, and the other thing the cluster has to say: the glass has just stepped down. It is
        /// seeded from <b>every ball hanging on the top level at once</b> and runs downwards, so it reads as a
        /// shock delivered by the ceiling to the whole cluster rather than as something that happened at a
        /// point — which is exactly what a descent is.
        /// <para>
        /// Red, and the ball's own colour has no say in it: the point is that every ball in the wave says the
        /// same thing. See <see cref="LightBall"/> for how the two waves share one channel.
        /// </para>
        /// </summary>
        private void StartCeilingRipple()
        {
            if (!BeginRippleWalk(out XZLevel size)) return;

            //Downwards from the top: the topmost occupied level is where the cluster meets the glass, and the
            //walk only ever moves outwards from there, so the wave travels down the way the push does
            for (int level = size.Level - 1; level >= 0; level--)
            {
                bool any = false;

                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball == null) continue;

                        _rippleHops[x, z, level] = 1;
                        _rippleQueue.Enqueue(new XZLevel(x, z, level));

                        LightBall(ball, 0, alarm: true);
                        any = true;
                    }

                if (any) break;     //the first level with anything on it is the one the glass is pressing
            }

            WalkRipple(size, alarm: true);
        }

        /// <summary>Clears the walk's scratch state and sizes it to the field. False when there is no cluster.</summary>
        private bool BeginRippleWalk(out XZLevel size)
        {
            size = default;
            if (_physicsBalls == null) return false;

            size = XZLevel.FromArray(_physicsBalls);

            if (_rippleHops == null || _rippleHops.GetLength(0) != size.X
                || _rippleHops.GetLength(1) != size.Z || _rippleHops.GetLength(2) != size.Level)
                _rippleHops = new int[size.X, size.Z, size.Level];
            else Array.Clear(_rippleHops);

            _rippleQueue.Clear();

            return true;
        }

        private void WalkRipple(XZLevel size, bool alarm)
        {
            while (_rippleQueue.Count > 0)
            {
                XZLevel cell = _rippleQueue.Dequeue();
                int hops = _rippleHops[cell.X, cell.Z, cell.Level] - 1;

                if (hops >= RIPPLE_MAX_HOPS) continue;

                //The allocating enumerator, deliberately: this runs once per landing, not once per ball per
                //frame, which is the case CountOccupiedNeighbors exists to keep clear of it
                foreach (XZLevel next in BallsMap.GetNeighboringCells(cell, size))
                {
                    if (_rippleHops[next.X, next.Z, next.Level] != 0) continue;

                    //An empty cell stops the wave rather than passing it on — that is what makes it travel
                    //through the balls. It is left unmarked, so it costs a re-test from each of its own
                    //neighbours and nothing else.
                    PhysicsBall ball = _physicsBalls[next.X, next.Z, next.Level];
                    if (ball == null) continue;

                    _rippleHops[next.X, next.Z, next.Level] = hops + 2;
                    _rippleQueue.Enqueue(next);

                    LightBall(ball, hops + 1, alarm);
                }
            }
        }

        /// <summary>
        /// Arms one ball's flare: a countdown to its turn, and how bright it will be when it comes. A ball the
        /// wave reaches again while it is still lit simply takes the newer wave — the nearest impact wins,
        /// which is what a burst of quick shots should look like.
        /// </summary>
        private static void LightBall(PhysicsBall ball, int hops, bool alarm)
        {
            if (ball == null) return;

            ball.RippleTime = -hops * RIPPLE_HOP_SECONDS;

            //Squared falloff over the walk's reach, not linear. The far balls still take part — a wave that
            //reached them at full strength and then stopped dead would put a bright ring around nothing — but
            //the COUNT of balls at a given hop grows as its square in a packed lattice, so a linear falloff
            //leaves hundreds of them near full brightness a few hops out, which is what flooded the glare.
            float reach = hops / (float)RIPPLE_MAX_HOPS;
            float amplitude = (1f - reach) * (1f - reach);

            //The SIGN carries which of the two waves this is — the landing's own light, or the ceiling's
            //alarm — so one per-instance float says both how bright and what colour, the way Dissolve encodes
            //its two directions in one. A ball can only be in one wave at a time, which it already could not
            //be: the newest to reach it takes it over.
            ball.RippleAmplitude = alarm ? -amplitude : amplitude;
        }

        /// <summary>
        /// Advances one ball's flare and returns how brightly it is burning this frame. It advances state on the
        /// ball itself, exactly as the occlusion ease and the attach glide do, so it must run once per ball per
        /// frame and no more — which is why it is not called from here at all: it is the hook
        /// <see cref="ClusterCollector"/> was constructed with (see the field), and that walk is the one place
        /// every ball is visited exactly once.
        /// </summary>
        private static float AdvanceRipple(PhysicsBall ball, float elapsed)
        {
            //Zero is at rest; the sign is which wave this is, so it is the magnitude that says whether one is
            //running at all
            if (ball.RippleAmplitude == 0f) return 0f;

            ball.RippleTime += elapsed;

            //Still on its way here — the countdown has not run out
            if (ball.RippleTime < 0f) return 0f;

            if (ball.RippleTime >= RIPPLE_ATTACK_SECONDS + RIPPLE_DECAY_SECONDS)
            {
                //Done. Cleared rather than left to drift, so a resting ball costs one comparison a frame and
                //the float cannot accumulate over a long level.
                ball.RippleAmplitude = 0f;
                return 0f;
            }

            if (ball.RippleTime < RIPPLE_ATTACK_SECONDS)
                return ball.RippleAmplitude * (ball.RippleTime / RIPPLE_ATTACK_SECONDS);

            //Squared on the way down: the flare drops away quickly and then trails, which is what leaves a tail
            //behind the front instead of a hard band with an edge at each end
            float fade = 1f - (ball.RippleTime - RIPPLE_ATTACK_SECONDS) / RIPPLE_DECAY_SECONDS;

            return ball.RippleAmplitude * fade * fade;
        }

        #endregion
    }
}
