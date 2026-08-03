using BepuPhysics;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// The one walk over a simulated cluster that turns it into ball instances: every hanging ball, every shot
    /// in flight and every released ball on its way down, each read off its own body's pose, shaded by what is
    /// packed around it, and offset by whatever is left of its arrival glide. It stood in both playing
    /// executables — the Testbed's <c>CollectBallInstances</c> and the Game's — until #76.
    /// <para>
    /// <b>Its whole reason for existing is that each ball must be visited exactly once per frame.</b> This walk
    /// does not only read state, it <i>advances</i> three pieces of it on the ball itself: the occlusion ease,
    /// the attach glide and (through the injected hook) the ripple. Visit a ball twice and all three run at
    /// double speed while the drawn frame looks perfectly correct; miss it and all three freeze. Neither fails
    /// loudly, so the loops are in here rather than in each caller's <c>Draw</c>, and
    /// <see cref="BallRenderSet.BeginFrame"/> refuses to open a second frame before the first has been drawn.
    /// See the remarks there.
    /// </para>
    /// <para>
    /// <b>Why the occlusion is re-derived for every ball every frame</b> rather than for a new arrival alone: a
    /// ball that attaches also boxes in each neighbour it arrived next to, and a matched group that lets go
    /// opens up every ball that was touching it. It is the neighbours' shading that changes most visibly, and
    /// tracking which of them to touch is a harder question than asking the grid.
    /// </para>
    /// <para>
    /// <b>What deliberately stayed with the callers.</b> The <i>ripple</i>: whether there is one at all, where
    /// a wave starts, how far it carries and what colour it is are questions about a cluster and its rules, and
    /// only the Game asks them — so what arrives here is one hook that advances one ball's flare and answers how
    /// brightly it burns, handed over <b>once</b> at construction rather than per frame (a method group written
    /// at a per-frame call site builds a fresh delegate every time it is evaluated, and this one would be
    /// evaluated on the draw path). The <i>magazine</i>: its queue goes into the same open frame through
    /// <see cref="BallDrawFrame.Add"/>, but the Testbed draws a plain queue and the Game cross-fades transmuted
    /// colours, so the loop over the bore is each caller's. And the <i>free lists</i> are passed in rather than
    /// owned, because what puts a ball into them — a shot fired, a group released, a pile culled — is the
    /// caller's own game.
    /// </para>
    /// </summary>
    public sealed class ClusterCollector
    {
        /// <summary>
        /// Time constant of the occlusion easing, in seconds (roughly three times this to arrive). The occlusion
        /// is computed from the grid, which changes in a single step — a ball is released, another one attaches —
        /// while the balls involved have not moved yet, so without the easing they would pop brighter (a released
        /// ball and the neighbours it leaves behind) or darker (an attached ball and the neighbours it joins) a
        /// whole frame before anything visibly happened. Most visible when a matched group lets go and every ball
        /// around the new hole brightens at once.
        /// </summary>
        private const float OCCLUSION_EASE_SECONDS = 1f;

        /// <summary>
        /// Time constant of the glide a freshly attached ball is drawn with. A landed ball is snapped to the
        /// nearest free cell rather than to where it hit, so the constraints drag its body up to several diameters
        /// within a frame or two; drawing it gliding in from where it actually hit turns that click into a
        /// movement, and costs the simulation nothing.
        /// </summary>
        private const float ATTACH_GLIDE_SECONDS = 0.08f;

        //Offset below which the glide is dropped and the ball drawn on its body exactly: a twentieth of a ball
        //radius, which is under a pixel at any distance the ball is drawn from. Squared once, because what is
        //compared is a squared length.
        private const float ATTACH_GLIDE_SETTLED = 0.025f;

        private const float ATTACH_GLIDE_SETTLED_SQUARED = ATTACH_GLIDE_SETTLED * ATTACH_GLIDE_SETTLED;

        private readonly Func<PhysicsBall, float, float> _advanceRipple;

        /// <param name="advanceRipple">Advances one ball's flare by the frame's elapsed seconds and answers how
        /// brightly it is burning, 0 at rest — the Game's <c>AdvanceRipple</c>. Null for a caller with no ripple
        /// (the Testbed), which then costs one null test per <i>frame</i> rather than per ball. Handed over here
        /// and not per call, for the reason the class remarks give.</param>
        public ClusterCollector(Func<PhysicsBall, float, float> advanceRipple = null) => _advanceRipple = advanceRipple;

        /// <summary>
        /// Walks the whole simulated population into <paramref name="frame"/>'s buckets, advancing each ball's
        /// eased shading, its arrival glide and its ripple as it goes — exactly once each.
        /// </summary>
        /// <param name="frame">This frame's open collection, from <see cref="BallRenderSet.BeginFrame"/>. Taken
        /// as <c>in</c> so the caller can add its own magazine balls to the same frame afterwards.</param>
        /// <param name="elapsedSeconds">The frame's own elapsed time. Both eases are framed in seconds, so
        /// neither changes with the frame rate, and both are correct at zero: a frame that took no time advances
        /// nothing. <b>The draw clock, not the simulation's step</b> — a paused simulation still eases its
        /// shading and still slides a landing ball home, because both are purely about what is on screen.</param>
        /// <param name="cluster">The hanging structure, or null before one is built.</param>
        /// <param name="shot">Balls in flight, or null. A free-flying ball has nothing packed around it, so it
        /// eases towards <see cref="PhysicsBall.UNOCCLUDED"/> — a shot one never had anything around it, and the
        /// easing is what keeps it that way rather than snapping it there.</param>
        /// <param name="falling">Released balls on their way down, or null. They advance their ripple too: a
        /// group cut loose while the wave was passing through it keeps glowing on its way down rather than
        /// snapping dark the instant it stops being cluster.</param>
        /// <returns>How many bodies were visited — the count the Testbed reports against the number of instances
        /// drawn, the two differing by the magazine preview and not by any cull.</returns>
        public int Collect(in BallDrawFrame frame, float elapsedSeconds, PhysicsBall[,,] cluster,
            List<PhysicsBall> shot, List<PhysicsBall> falling)
        {
            //How far towards its target each ball's occlusion moves this frame, and how much of a ball's arrival
            //offset SURVIVES it. Both exponential and framed in seconds, so neither changes with the frame rate
            //(the same idiom as Magazine.Slide and SkyLightRig.StepOvercast), and both are already right at
            //elapsed == 0 - exp(0) is 1, so nothing eases and nothing decays - which is why neither needs the
            //special case the two copies carried. The Game's linear ramp and its "snap to the target on a
            //zero-length frame" were the pair being replaced here.
            float ease = 1f - MathF.Exp(-elapsedSeconds / OCCLUSION_EASE_SECONDS);
            float glideRetained = MathF.Exp(-elapsedSeconds / ATTACH_GLIDE_SECONDS);

            int visited = 0;

            if (cluster != null)
            {
                //Hoisted: the array's dimensions do not change, and this is the innermost loop in the frame
                XZLevel size = XZLevel.FromArray(cluster);

                for (int level = 0; level < size.Level; level++)
                    for (int x = 0; x < size.X; x++)
                        for (int z = 0; z < size.Z; z++)
                        {
                            PhysicsBall ball = cluster[x, z, level];
                            if (ball == null) continue;

                            //The cell is taken from the loop and not from ball.ArrayPosition: the ball IS at
                            //(x, z, level), so the indices are the truth and no field has to be in step for the
                            //shading to be read off the right cell. XZLevel is a struct, so this allocates
                            //nothing 3000 times a frame.
                            //
                            //The ceiling plate deliberately does NOT occlude: it is translucent glass, so it
                            //lets the light through (what keeps a released ball from flashing brighter is the
                            //occlusion easing, not this).
                            int occluders = BallsMap.CountOccupiedNeighbors(cluster, new XZLevel(x, z, level), size,
                                out Vector3 occluderDirectionSum);

                            //OcclusionTarget is what divides the direction sum by the occluder maximum, and it
                            //is the only thing that can build this vector at all - see BallRenderSet's remarks
                            //on what handing it over raw did to the cluster's look.
                            Collect(frame, ball, BallRenderSet.OcclusionTarget(occluders, occluderDirectionSum),
                                ease, glideRetained, elapsedSeconds);

                            visited++;
                        }
            }

            //Indexed rather than foreach: these are List<T> on a per-frame path
            if (shot != null)
                for (int i = 0; i < shot.Count; i++)
                {
                    Collect(frame, shot[i], BallRenderSet.UNOCCLUDED, ease, glideRetained, elapsedSeconds);
                    visited++;
                }

            if (falling != null)
                for (int i = 0; i < falling.Count; i++)
                {
                    Collect(frame, falling[i], BallRenderSet.UNOCCLUDED, ease, glideRetained, elapsedSeconds);
                    visited++;
                }

            return visited;
        }

        //One ball, drawn from its body: where the pose puts it plus whatever is left of its arrival glide, turned
        //the way the pose turns it, shaded by the eased occlusion and lit by however far its flare has got.
        private void Collect(in BallDrawFrame frame, PhysicsBall ball, Vector4 occlusionTarget, float ease,
            float glideRetained, float elapsedSeconds)
        {
            RigidPose pose = ball.BallReference.Pose;
            System.Numerics.Vector3 drawnAt = GlidePosition(ball, pose.Position, glideRetained);

            //Both crossings out of Bepu's vector types are the shared named ones (Prazsky.Core.Tools), which
            //this walk used to write out component by component
            frame.AddOriented(ball.Type, drawnAt.ToXna(), pose.Orientation.ToXna(),
                EaseOcclusion(ball, occlusionTarget, ease),
                _advanceRipple == null ? 0f : _advanceRipple(ball, elapsedSeconds));
        }

        /// <summary>
        /// Moves a ball's drawn occlusion towards what its surroundings now call for, and hands it back in the
        /// form the instance data wants. The <i>first</i> frame a ball is drawn takes the target straight — only
        /// changes happening in front of the player are worth easing, and a freshly built cluster has to be
        /// shaded right from its first frame rather than fading into its own shading.
        /// <para>
        /// The eased value is kept on the <see cref="PhysicsBall"/> and in its own vector space, which is why
        /// this converts at both ends: it has to survive the ball crossing between the structure and the free
        /// balls, since the grid it is derived from is something the ball joins or leaves in a single step while
        /// it has not moved at all.
        /// </para>
        /// </summary>
        private static Vector4 EaseOcclusion(PhysicsBall ball, Vector4 target, float ease)
        {
            System.Numerics.Vector4 want = new(target.X, target.Y, target.Z, target.W);

            ball.Occlusion = ball.OcclusionInitialized
                ? System.Numerics.Vector4.Lerp(ball.Occlusion, want, ease)
                : want;

            ball.OcclusionInitialized = true;

            return new Vector4(ball.Occlusion.X, ball.Occlusion.Y, ball.Occlusion.Z, ball.Occlusion.W);
        }

        /// <summary>
        /// Where a ball is drawn: on its body, plus a vanishing offset for one that has just arrived. Every
        /// other ball is drawn exactly where its body is.
        /// <para>
        /// An <i>offset</i> from the body rather than a smoothed position, which is what decays to nothing on its
        /// own: the ball still follows every bit of the structure's swaying meanwhile, so there is nothing left
        /// over to jump when the glide ends.
        /// </para>
        /// </summary>
        /// <param name="glideRetained">How much of the offset survives this frame — <c>exp(−dt/τ)</c>. The two
        /// copies named this opposite ways round (the Testbed's <c>glide</c> was the fraction <i>travelled</i>
        /// and it multiplied by <c>1 − glide</c>; the Game's was the fraction retained), which is harmless until
        /// a frame reports zero elapsed time: then the travelled form correctly leaves the offset alone while
        /// the retained form, as the Game guarded it, zeroed the glide outright. The retained form needs no
        /// complement and no guard, so it is the one kept.</param>
        private static System.Numerics.Vector3 GlidePosition(PhysicsBall ball, System.Numerics.Vector3 bodyPosition,
            float glideRetained)
        {
            //The constraints that drag the body into its cell are only solved by the next timestep, so on this
            //one frame the body is still where the ball hit and applying the offset would move it the wrong way -
            //it would draw the ball as far PAST the impact as the cell is short of it. The Game armed the flag
            //and then added the offset anyway, against what its own comment said; this is the Testbed's ordering,
            //which honours it.
            if (ball.RenderOffsetArmed)
            {
                ball.RenderOffsetArmed = false;
                return bodyPosition;
            }

            //The resting case, which is nearly every ball nearly every frame: three compares and out.
            if (ball.RenderOffset == System.Numerics.Vector3.Zero) return bodyPosition;

            ball.RenderOffset *= glideRetained;

            if (ball.RenderOffset.LengthSquared() < ATTACH_GLIDE_SETTLED_SQUARED)
            {
                ball.RenderOffset = System.Numerics.Vector3.Zero;
                return bodyPosition;
            }

            return bodyPosition + ball.RenderOffset;
        }
    }
}
