using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace Prazsky.BS3D
{
    /// <summary>
    /// Where the drop cinematic looks (#616): one point for a released group that is, by then, often two groups
    /// — the balls the dish rolls into the drain and the ones that left over the island's rim and are falling
    /// past its side to the kill plane. <b>The funnel wins whenever anything is in it.</b>
    /// <para>
    /// It replaced the plain mean of every live ball, and that mean was wrong three ways, each one a thing the
    /// owner saw: a split group was framed at the middle of the split, a point in the air over the throat's
    /// wall with neither population in shot; every cull stepped the mean, so strays reaching the kill plane one
    /// by one read as the camera hopping between targets; and since the lowest balls are the first culled, the
    /// mean <i>rose</i> as they went and the arc was asked to run backwards (the last of these is answered in
    /// the cinematic itself, which holds its progress monotonic).
    /// </para>
    /// <para>
    /// <b>Weighted, never switched.</b> Every ball carries a weight that fades continuously — out past the
    /// walkable top's arris (<see cref="RIM_FADE"/>), with depth under the stone for a ball outside the mouth
    /// (<see cref="OFF_STONE_REACH"/>, <see cref="OFF_STONE_DEPTH"/>), and towards the kill plane
    /// (<see cref="KILL_FADE"/>) — so a ball is all but weightless before anything removes it and its cull
    /// moves nothing. A ball whose weight has reached zero off the stone is latched as a <b>stray</b> and never
    /// counts again, whatever it bounces off. Only when no ball of the group carries funnel weight does the
    /// whole group (weighted by the kill-plane fade alone) frame the shot — the group that went wholly over the
    /// edge still has to film something — and the hand-over between the two is itself a blend by how much
    /// funnel weight is left (<see cref="FunnelShare"/>), not a switch.
    /// </para>
    /// <para>
    /// Pure and allocation-free per frame: it is handed positions by an id and answers a point, which is what
    /// lets <c>DropFocusTests</c> run the reported split with no simulation. The ids are the caller's (the Game
    /// uses body handles); the latch set is cleared by <see cref="Reset"/> once per cinematic.
    /// </para>
    /// </summary>
    public sealed class DropFocus
    {
        /// <summary>Over how many units past <see cref="ArenaIsland.FLOOR_RADIUS"/> — the arris a landed ball
        /// either rolls inward from or leaves the stone over — a ball's weight fades from whole to nothing.
        /// A ball dropping past the drum's side is at least a ball radius outside <see cref="ArenaIsland.RADIUS"/>,
        /// which this reaches.</summary>
        public const float RIM_FADE = 3f;

        /// <summary>How far outside the mouth's radius, and how far under the dish's lowest point, a ball has
        /// to be before it is wholly "off the stone": nothing the drain will ever take is there, since the
        /// funnel narrows from <see cref="ArenaIsland.FUNNEL_TOP_RADIUS"/> down. It catches the ball that went
        /// off the coping close to the arris and falls down the drum's side inside <see cref="RIM_FADE"/>.</summary>
        public const float OFF_STONE_REACH = 2f;

        /// <inheritdoc cref="OFF_STONE_REACH"/>
        public const float OFF_STONE_DEPTH = 2f;

        /// <summary>Over how many units above the kill plane a ball fades out of the subject, so the cull that
        /// removes it moves nothing.</summary>
        public const float KILL_FADE = 6f;

        //How much funnel weight is a whole funnel population — one full-weight ball. Below it the frame blends
        //towards the whole group, so the last funnel ball fading into the kill plane hands over rather than cuts.
        private const float SHARE_FULL = 1f;

        //A weight at or below this is nothing: the latch threshold and the "no fallback weight" guard.
        private const float NOTHING = 1e-4f;

        //The dish's lowest point, the mouth's lip: a ball centre under it and outside the mouth is off the stone
        private const float DISH_FOOT_Y = ArenaIsland.TOP_Y - ArenaIsland.DISH_DEPTH;

        private readonly float _killPlaneY;
        private readonly HashSet<int> _strays = new();

        private Vector3 _funnelSum, _allSum, _plainSum;
        private float _funnelWeight, _allWeight;
        private int _count;
        private bool _funnelEstablished;

        /// <param name="killPlaneY">The height the game culls falling balls at.</param>
        public DropFocus(float killPlaneY)
        {
            _killPlaneY = killPlaneY;
        }

        /// <summary>How much of the answer is the funnel's, 0 to 1: 1 while at least one ball's worth of funnel
        /// weight is alive, easing to 0 as the last of it goes. Valid after <see cref="TryResolve"/>.</summary>
        public float FunnelShare { get; private set; }

        /// <summary>A new cinematic: forgets which balls were strays and that a funnel population was seen.</summary>
        public void Reset()
        {
            _strays.Clear();
            _funnelEstablished = false;
            BeginFrame();
        }

        /// <summary>Starts one frame's accumulation. Call before the frame's <see cref="Add"/>s.</summary>
        public void BeginFrame()
        {
            _funnelSum = _allSum = _plainSum = Vector3.Zero;
            _funnelWeight = _allWeight = 0f;
            _count = 0;
        }

        /// <summary>One live ball of the group, where the frame draws it.</summary>
        public void Add(int id, Vector3 position)
        {
            float depth = Saturate((position.Y - _killPlaneY) / KILL_FADE);

            _plainSum += position;
            _allSum += position * depth;
            _allWeight += depth;
            _count++;

            if (_strays.Contains(id)) return;

            float onStone = OnStoneWeight(position);

            if (onStone <= NOTHING)
            {
                _strays.Add(id);
                return;
            }

            //Inside the drain proper — under the dish's lowest point and still carrying weight, which only the
            //cone's inside allows. From here on the drain has the shot, and its emptying ends it.
            if (position.Y < DISH_FOOT_Y && onStone >= 0.5f) _funnelEstablished = true;

            float weight = onStone * depth;
            _funnelSum += position * weight;
            _funnelWeight += weight;
        }

        /// <summary>
        /// This frame's point. False when there is nothing left to film: no ball was added, or a ball of the group
        /// has been inside the drain in this cinematic and the funnel weight has now run out — the drain took the
        /// shot's subject, and what is still falling past the rim is exactly what the shot must not turn to. A
        /// group that never reached the drain (it went wholly over the edge) is filmed to its end instead.
        /// </summary>
        public bool TryResolve(out Vector3 centre)
        {
            centre = Vector3.Zero;
            FunnelShare = 0f;

            if (_count == 0) return false;

            FunnelShare = Saturate(_funnelWeight / SHARE_FULL);

            if (_funnelEstablished && _funnelWeight <= NOTHING) return false;

            Vector3 all = _allWeight > NOTHING ? _allSum / _allWeight : _plainSum / _count;

            centre = _funnelWeight > NOTHING ? Vector3.Lerp(all, _funnelSum / _funnelWeight, FunnelShare) : all;
            return true;
        }

        /// <summary>
        /// How much a ball at <paramref name="position"/> is still on its way into the drain, 1 to 0, before the
        /// kill-plane fade: whole inside the arris, fading out past it, and fading with depth for a ball outside
        /// the mouth that is already under the stone's surface.
        /// </summary>
        public static float OnStoneWeight(Vector3 position)
        {
            float reach = MathF.Sqrt(position.X * position.X + position.Z * position.Z);

            float rim = 1f - Saturate((reach - ArenaIsland.FLOOR_RADIUS) / RIM_FADE);

            float offStone = Saturate((reach - ArenaIsland.FUNNEL_TOP_RADIUS) / OFF_STONE_REACH)
                             * Saturate((DISH_FOOT_Y - position.Y) / OFF_STONE_DEPTH);

            return rim * (1f - offStone);
        }

        private static float Saturate(float value) => MathHelper.Clamp(value, 0f, 1f);
    }
}
