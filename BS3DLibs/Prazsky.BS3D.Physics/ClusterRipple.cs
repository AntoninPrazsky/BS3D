using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using System;
using System.Collections.Generic;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>The wave of light a landing sends through the cluster</b> — the balls touching the impact flare
    /// first, then the ones touching those, each fading as the next takes over. It is what makes the cluster
    /// read as a connected, living body rather than as a heap of independent spheres — the shot does not just
    /// stick, the thing it stuck to answers.
    /// </summary>
    /// <remarks>
    /// It travels by <b>connectivity</b> and not by distance, which is the whole of why it looks right: a wave
    /// evaluated from a world-space radius would cross the holes a played cluster is full of as if they were
    /// not there, while a walk over balls that actually touch goes <i>around</i> them — including around the
    /// hole a matched group has just left, which is the most satisfying thing it does.
    /// <para>
    /// It was the Game's <c>GameplayScreen.Ripple.cs</c> until #582, whose own doc named it the cleanest of the
    /// extractions still owed: it reads <c>PhysicsBall[,,]</c> and <see cref="BallsMap.GetNeighboringCells"/>
    /// and writes state that already lives on <see cref="PhysicsBall"/> (<see cref="PhysicsBall.RippleTime"/>,
    /// <see cref="PhysicsBall.RippleAmplitude"/>). So the one thing this object keeps is the walk's scratch — the
    /// hop grid and the queue — and the cluster is handed in <b>per call</b>, because the session's
    /// <c>BuildCluster</c> replaces the live array wholesale every level and a reference held here would go
    /// stale on the next one. What the colours of the two waves are is the host's (the renderer's
    /// <c>RippleAlarmColor</c>); this class only says which of the two a ball is in.
    /// </para>
    /// <para>
    /// A ball's flare is <b>advanced</b> by <see cref="Advance"/>, which is not called from here at all: it is
    /// the hook <see cref="ClusterCollector"/> is constructed with, and that walk is the one place every ball is
    /// visited exactly once a frame.
    /// </para>
    /// </remarks>
    public sealed class ClusterRipple
    {
        //These three decide whether it reads as a WAVE or as a flash, and the ratio between them is the whole
        //of it: the lit band is as many balls wide as the flare's length divided by the hop delay. The first
        //build had a 0.36 s flare stepping every 0.045 s — a band nine balls deep against a reach of twelve,
        //which is very nearly the whole cluster alight at once, and on screen it read as the shot flashing the
        //lot rather than as anything travelling. Keep the band around three or four balls.

        /// <summary>Seconds between one ball flaring and the ones touching it taking their turn.</summary>
        public const float HOP_SECONDS = 0.09f;

        /// <summary>How long one ball's flare takes to rise: a fast rise and a soft fall
        /// (<see cref="DECAY_SECONDS"/>), so the band reads as a wave front with a tail rather than as a row of
        /// balls switching on and off.</summary>
        public const float ATTACK_SECONDS = 0.05f;

        /// <summary>How long one ball's flare takes to fall away after <see cref="ATTACK_SECONDS"/>.</summary>
        public const float DECAY_SECONDS = 0.22f;

        /// <summary>
        /// How far the wave carries. Bounds the walk, and the flare's amplitude falls off across it so the
        /// ripple dies away instead of stopping at a hard ring of lit balls. Fourteen hops at
        /// <see cref="HOP_SECONDS"/> is a bit over a second to cross a big cluster — long enough to watch it go,
        /// short enough that the next shot is not still waiting for it.
        /// </summary>
        public const int MAX_HOPS = 14;

        //Hop count + 1 per cell, 0 meaning "not reached by this walk" — so it doubles as the visited mark and
        //needs only a clear between ripples rather than a second array. Reused rather than allocated per
        //landing, and sized to the field the level actually loaded.
        private int[,,] _hops;
        private readonly Queue<XZLevel> _queue = new();

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
        /// <param name="balls">The live cluster; null (no cluster) does nothing.</param>
        /// <param name="origin">The cell the wave starts from.</param>
        public void StartAt(PhysicsBall[,,] balls, XZLevel origin)
        {
            if (!BeginWalk(balls, out XZLevel size)) return;

            _hops[origin.X, origin.Z, origin.Level] = 1;      //reached, at hop 0
            _queue.Enqueue(origin);

            //The ball that landed, if it is still there, flares first and on its own — it is hop 0
            LightBall(balls[origin.X, origin.Z, origin.Level], 0, alarm: false);

            Walk(balls, size, alarm: false);
        }

        /// <summary>
        /// The other wave, and the other thing the cluster has to say: the glass has just stepped down. It is
        /// seeded from <b>every ball hanging on the topmost occupied level at once</b> and runs downwards, so it
        /// reads as a shock delivered by the ceiling to the whole cluster rather than as something that happened
        /// at a point — which is exactly what a descent is.
        /// <para>
        /// The alarm wave, and the ball's own colour has no say in it: the point is that every ball in the wave
        /// says the same thing. See <see cref="LightBall"/> for how the two waves share one channel.
        /// </para>
        /// </summary>
        /// <param name="balls">The live cluster; null (no cluster) does nothing.</param>
        public void StartFromTop(PhysicsBall[,,] balls)
        {
            if (!BeginWalk(balls, out XZLevel size)) return;

            //Downwards from the top: the topmost occupied level is where the cluster meets the glass, and the
            //walk only ever moves outwards from there, so the wave travels down the way the push does
            for (int level = size.Level - 1; level >= 0; level--)
            {
                bool any = false;

                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = balls[x, z, level];
                        if (ball == null) continue;

                        _hops[x, z, level] = 1;
                        _queue.Enqueue(new XZLevel(x, z, level));

                        LightBall(ball, 0, alarm: true);
                        any = true;
                    }

                if (any) break;     //the first level with anything on it is the one the glass is pressing
            }

            Walk(balls, size, alarm: true);
        }

        /// <summary>Clears the walk's scratch state and sizes it to the field. False when there is no cluster.</summary>
        private bool BeginWalk(PhysicsBall[,,] balls, out XZLevel size)
        {
            size = default;
            if (balls == null) return false;

            size = XZLevel.FromArray(balls);

            if (_hops == null || _hops.GetLength(0) != size.X
                || _hops.GetLength(1) != size.Z || _hops.GetLength(2) != size.Level)
                _hops = new int[size.X, size.Z, size.Level];
            else Array.Clear(_hops);

            _queue.Clear();

            return true;
        }

        private void Walk(PhysicsBall[,,] balls, XZLevel size, bool alarm)
        {
            while (_queue.Count > 0)
            {
                XZLevel cell = _queue.Dequeue();
                int hops = _hops[cell.X, cell.Z, cell.Level] - 1;

                if (hops >= MAX_HOPS) continue;

                //The struct enumerator, which allocates nothing since #381 (it was a yield-return iterator,
                //taken here deliberately because this runs once per landing, not once per ball per frame)
                foreach (XZLevel next in BallsMap.GetNeighboringCells(cell, size))
                {
                    if (_hops[next.X, next.Z, next.Level] != 0) continue;

                    //An empty cell stops the wave rather than passing it on — that is what makes it travel
                    //through the balls. It is left unmarked, so it costs a re-test from each of its own
                    //neighbours and nothing else.
                    PhysicsBall ball = balls[next.X, next.Z, next.Level];
                    if (ball == null) continue;

                    _hops[next.X, next.Z, next.Level] = hops + 2;
                    _queue.Enqueue(next);

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

            ball.RippleTime = -hops * HOP_SECONDS;

            //Squared falloff over the walk's reach, not linear. The far balls still take part — a wave that
            //reached them at full strength and then stopped dead would put a bright ring around nothing — but
            //the COUNT of balls at a given hop grows as its square in a packed lattice, so a linear falloff
            //leaves hundreds of them near full brightness a few hops out, which is what flooded the glare.
            float reach = hops / (float)MAX_HOPS;
            float amplitude = (1f - reach) * (1f - reach);

            //The SIGN carries which of the two waves this is — the landing's own light, or the ceiling's
            //alarm — so one per-instance float says both how bright and what colour, the way Dissolve encodes
            //its two directions in one. A ball can only be in one wave at a time, which it already could not
            //be: the newest to reach it takes it over.
            ball.RippleAmplitude = alarm ? -amplitude : amplitude;
        }

        /// <summary>
        /// Advances one ball's flare and returns how brightly it is burning this frame (negative in the alarm
        /// wave). It advances state on the ball itself, exactly as the occlusion ease and the attach glide do,
        /// so it must run once per ball per frame and no more — which is why it is the hook
        /// <see cref="ClusterCollector"/> is constructed with rather than anything the host calls: that walk is
        /// the one place every ball is visited exactly once.
        /// </summary>
        /// <param name="ball">The ball whose flare to advance.</param>
        /// <param name="elapsed">The frame's elapsed seconds.</param>
        public static float Advance(PhysicsBall ball, float elapsed)
        {
            //Zero is at rest; the sign is which wave this is, so it is the magnitude that says whether one is
            //running at all
            if (ball.RippleAmplitude == 0f) return 0f;

            ball.RippleTime += elapsed;

            //Still on its way here — the countdown has not run out
            if (ball.RippleTime < 0f) return 0f;

            if (ball.RippleTime >= ATTACK_SECONDS + DECAY_SECONDS)
            {
                //Done. Cleared rather than left to drift, so a resting ball costs one comparison a frame and
                //the float cannot accumulate over a long level.
                ball.RippleAmplitude = 0f;
                return 0f;
            }

            if (ball.RippleTime < ATTACK_SECONDS)
                return ball.RippleAmplitude * (ball.RippleTime / ATTACK_SECONDS);

            //Squared on the way down: the flare drops away quickly and then trails, which is what leaves a tail
            //behind the front instead of a hard band with an edge at each end
            float fade = 1f - (ball.RippleTime - ATTACK_SECONDS) / DECAY_SECONDS;

            return ball.RippleAmplitude * fade * fade;
        }
    }
}
