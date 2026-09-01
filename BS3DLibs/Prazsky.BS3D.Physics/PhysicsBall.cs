using BepuPhysics;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using System.Collections.Generic;

namespace Prazsky.BS3D.Physics
{
    public class PhysicsBall
    {
        public BodyReference BallReference;

        public ConstraintHandles HandlesTop;
        public ConstraintHandles HandlesMiddle;
        public ConstraintHandles HandlesBottom;

        public BallType Type;

        /// <summary>
        /// What the ball is beside its colour (#323), mirrored off its <see cref="StaticBall"/> when the
        /// structure is built. It is here because <see cref="ClusterCollector"/> walks the physics array alone
        /// and has no map to ask — and what it draws a rock as is decided per ball.
        /// </summary>
        public BallKind Kind;

        public XZLevel ArrayPosition;

        /// <summary>
        /// Occlusion data of a ball with nothing packed around it.
        /// </summary>
        public static readonly System.Numerics.Vector4 UNOCCLUDED = new(0f, 0f, 0f, 1f);

        /// <summary>
        /// Ambient occlusion the ball is currently rendered with: XYZ = direction towards its occupied
        /// neighboring cells, W = base occlusion factor. It is kept on the ball rather than in the renderer
        /// because it has to survive the ball crossing between the structure and the free balls: the occlusion
        /// is computed from the grid, which the ball joins or leaves in a single step while it has not moved
        /// yet, so the value is eased towards its new target over the following frames instead of popping.
        /// </summary>
        public System.Numerics.Vector4 Occlusion = UNOCCLUDED;

        /// <summary>
        /// Whether <see cref="Occlusion"/> already holds a value the ball has been rendered with. A ball drawn
        /// for the first time takes its occlusion as it is instead of easing into it, so a freshly built
        /// structure is shaded correctly from its first frame rather than fading into its own shading.
        /// </summary>
        public bool OcclusionInitialized;

        /// <summary>
        /// Offset the ball is drawn at relative to its body, decaying to zero. A ball is attached to the nearest
        /// free cell rather than to the spot it hit, so its body crosses up to several ball diameters the
        /// instant it lands; drawing it at its body plus a vanishing offset turns that click into a glide,
        /// without touching the simulation. An offset (rather than a smoothed position) is what decays
        /// to nothing on its own - the ball still follows every bit of the structure's swaying meanwhile, so
        /// there is nothing left over to jump when the glide ends.
        /// </summary>
        public System.Numerics.Vector3 RenderOffset;

        /// <summary>
        /// Starts drawing the ball gliding into <paramref name="attachedTo"/>, the position of the cell it has
        /// been attached to. <b>Called while the body is still at the impact point</b>, since the offset is the
        /// difference between the two; the handler moves the body into the cell on the next line (#265).
        /// </summary>
        public void StartRenderGlide(System.Numerics.Vector3 attachedTo)
        {
            RenderOffset = BallReference.Pose.Position - attachedTo;
        }

        /// <summary>
        /// The body's pose as it stood before the most recent physics step — the other end of the render
        /// interpolation (#293). The simulation advances in whole fixed steps, so under the drop cinematic's
        /// slow motion a step lands only every few rendered frames and a ball drawn straight off its body
        /// stands still and then jumps; drawn between this pose and the live one by how far the accumulator
        /// has got towards the next step, it moves every frame instead.
        /// </summary>
        public System.Numerics.Vector3 PreviousPosition;

        /// <inheritdoc cref="PreviousPosition"/>
        public System.Numerics.Quaternion PreviousOrientation;

        /// <summary>
        /// Whether <see cref="PreviousPosition"/> holds a pose it is valid to interpolate from. False until
        /// the first snapshot, and reset by <see cref="ResetPoseHistory"/> whenever the body is
        /// <i>placed</i> rather than moved by the solver — interpolating across a teleport would replay the
        /// very drag-in #265 removed. While false the ball is drawn on its live pose exactly.
        /// </summary>
        public bool PoseHistoryValid;

        /// <summary>
        /// Records the body's current pose as the interpolation's trailing end. Called once per physics step
        /// per ball, immediately before the step advances the world.
        /// </summary>
        public void SnapshotPose()
        {
            RigidPose pose = BallReference.Pose;
            PreviousPosition = pose.Position;
            PreviousOrientation = pose.Orientation;
            PoseHistoryValid = true;
        }

        /// <summary>Forgets the pose history — see <see cref="PoseHistoryValid"/> for when that is required.</summary>
        public void ResetPoseHistory() => PoseHistoryValid = false;

        /// <summary>
        /// The pose the ball is drawn at: the live body pose, or — when a history stands and the frame sits
        /// between two steps — the blend between the previous step's pose and the live one. <paramref
        /// name="alpha"/> is how far the physics accumulator has got towards the next step (0..1); at 1, or
        /// with no valid history, this is the live pose bit for bit.
        /// </summary>
        public void InterpolatedPose(float alpha, out System.Numerics.Vector3 position,
            out System.Numerics.Quaternion orientation)
        {
            RigidPose pose = BallReference.Pose;

            if (!PoseHistoryValid || alpha >= 1f)
            {
                position = pose.Position;
                orientation = pose.Orientation;
                return;
            }

            position = System.Numerics.Vector3.Lerp(PreviousPosition, pose.Position, alpha);
            orientation = System.Numerics.Quaternion.Slerp(PreviousOrientation, pose.Orientation, alpha);
        }

        /// <summary>
        /// Seconds into this ball's own flare as the ripple passes through it. It starts <b>negative</b> — a
        /// countdown to its turn, one step per ball between it and the impact — runs up through the flare, and
        /// is done once it passes the flare's length.
        /// <para>
        /// Kept on the ball for the reason <see cref="Occlusion"/> is: a ball crosses between the structure
        /// and the falling balls in a single step, and a group cut loose while it was lit should keep glowing
        /// on its way down rather than snapping dark at the moment it stops being part of the cluster.
        /// </para>
        /// </summary>
        public float RippleTime;

        /// <summary>
        /// How brightly this ball flares at the peak of its turn, 0 = not lit at all (which is the resting
        /// state, and what everything the ripple never reached carries). Falls off with distance from the
        /// impact, so the wave dies away instead of stopping at a hard edge.
        /// </summary>
        public float RippleAmplitude;

        /// <summary>
        /// Seconds left of this ball's clear-to-colour crossing (#325), counting down to zero — nonzero only
        /// on a <see cref="BallKind.Transparent"/> ball that a landing has just coloured, and only for
        /// <c>ClusterCollector.COLOUR_FADE_SECONDS</c>.
        /// <para>
        /// Kept on the ball for the reason <see cref="RippleTime"/> is, and advanced by the same walk: the ball
        /// may be released mid-crossing (the colouring can complete the very group that removes it), and a ball
        /// that snapped to full colour the instant it stopped being cluster would flash on its way down. The
        /// <b>logical</b> colour changed the moment the shot landed and this timer has no say in it — it is
        /// cosmetic, exactly as the magazine's transmute dissolve is, and for the same reason: firing at a
        /// half-faded ball must match the colour it already is.
        /// </para>
        /// </summary>
        public float ColourFadeRemaining;

        public void SetEmptyConstraints()
        {
            HandlesTop.Handle1.Value = -1;
            HandlesTop.Handle2.Value = -1;
            HandlesTop.Handle3.Value = -1;
            HandlesTop.Handle4.Value = -1;

            HandlesMiddle.Handle1.Value = -1;
            HandlesMiddle.Handle2.Value = -1;
            HandlesMiddle.Handle3.Value = -1;
            HandlesMiddle.Handle4.Value = -1;

            HandlesBottom.Handle1.Value = -1;
            HandlesBottom.Handle2.Value = -1;
            HandlesBottom.Handle3.Value = -1;
            HandlesBottom.Handle4.Value = -1;
        }

        public PhysicsBall()
        {
            SetEmptyConstraints();
        }

        /// <summary>
        /// Adds all stored constraint handles (slots with a non-negative value) into <paramref name="into"/>.
        /// </summary>
        public void CollectConstraintHandles(List<ConstraintHandle> into)
        {
            HandlesTop.CollectStored(into);
            HandlesMiddle.CollectStored(into);
            HandlesBottom.CollectStored(into);
        }

        /// <summary>
        /// Clears every slot holding the given handle (without touching the simulation).
        /// Used when the constraint was removed through the other ball of the pair, so the stale
        /// handle value cannot alias a different constraint once the solver reuses the index.
        /// </summary>
        public void ClearStoredHandle(ConstraintHandle handle)
        {
            HandlesTop.ClearStored(handle);
            HandlesMiddle.ClearStored(handle);
            HandlesBottom.ClearStored(handle);
        }

        public void RemoveAllConstraints(Simulation simulation)
        {
            if (HandlesTop.Handle1.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesTop.Handle1)) simulation.Solver.Remove(HandlesTop.Handle1);
                HandlesTop.Handle1.Value = -1;
            }
            if (HandlesTop.Handle2.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesTop.Handle2)) simulation.Solver.Remove(HandlesTop.Handle2);
                HandlesTop.Handle2.Value = -1;
            }
            if (HandlesTop.Handle3.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesTop.Handle3)) simulation.Solver.Remove(HandlesTop.Handle3);
                HandlesTop.Handle3.Value = -1;
            }
            if (HandlesTop.Handle4.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesTop.Handle4)) simulation.Solver.Remove(HandlesTop.Handle4);
                HandlesTop.Handle4.Value = -1;
            }

            if (HandlesMiddle.Handle1.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesMiddle.Handle1)) simulation.Solver.Remove(HandlesMiddle.Handle1);
                HandlesMiddle.Handle1.Value = -1;
            }
            if (HandlesMiddle.Handle2.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesMiddle.Handle2)) simulation.Solver.Remove(HandlesMiddle.Handle2);
                HandlesMiddle.Handle2.Value = -1;
            }
            if (HandlesMiddle.Handle3.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesMiddle.Handle3)) simulation.Solver.Remove(HandlesMiddle.Handle3);
                HandlesMiddle.Handle3.Value = -1;
            }
            if (HandlesMiddle.Handle4.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesMiddle.Handle4)) simulation.Solver.Remove(HandlesMiddle.Handle4);
                HandlesMiddle.Handle4.Value = -1;
            }

            if (HandlesBottom.Handle1.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesBottom.Handle1)) simulation.Solver.Remove(HandlesBottom.Handle1);
                HandlesBottom.Handle1.Value = -1;
            }
            if (HandlesBottom.Handle2.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesBottom.Handle2)) simulation.Solver.Remove(HandlesBottom.Handle2);
                HandlesBottom.Handle2.Value = -1;
            }
            if (HandlesBottom.Handle3.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesBottom.Handle3)) simulation.Solver.Remove(HandlesBottom.Handle3);
                HandlesBottom.Handle3.Value = -1;
            }
            if (HandlesBottom.Handle4.Value >= 0)
            {
                if (simulation.Solver.ConstraintExists(HandlesBottom.Handle4)) simulation.Solver.Remove(HandlesBottom.Handle4);
                HandlesBottom.Handle4.Value = -1;
            }
        }
    }

    /// <summary>
    /// Four constraint handle slots. A ball touches at most four neighbors in each group
    /// (same level, level above incl. ceiling, level below), so four slots always suffice.
    /// Slots are filled in no particular order (removal iterates all of them anyway).
    /// </summary>
    public struct ConstraintHandles
    {
        public ConstraintHandle Handle1;
        public ConstraintHandle Handle2;
        public ConstraintHandle Handle3;
        public ConstraintHandle Handle4;

        /// <summary>
        /// Stores the handle into the first free slot.
        /// Returns false when all four slots are already taken; the constraint then stays tracked only on the other ball of the pair.
        /// </summary>
        public bool TryStore(ConstraintHandle handle)
        {
            if (Handle1.Value < 0) { Handle1 = handle; return true; }
            if (Handle2.Value < 0) { Handle2 = handle; return true; }
            if (Handle3.Value < 0) { Handle3 = handle; return true; }
            if (Handle4.Value < 0) { Handle4 = handle; return true; }
            return false;
        }

        /// <summary>
        /// Adds all stored handles (slots with a non-negative value) into <paramref name="into"/>.
        /// </summary>
        public void CollectStored(List<ConstraintHandle> into)
        {
            if (Handle1.Value >= 0) into.Add(Handle1);
            if (Handle2.Value >= 0) into.Add(Handle2);
            if (Handle3.Value >= 0) into.Add(Handle3);
            if (Handle4.Value >= 0) into.Add(Handle4);
        }

        /// <summary>
        /// Resets every slot holding the given handle back to the empty (-1) value.
        /// </summary>
        public void ClearStored(ConstraintHandle handle)
        {
            if (Handle1.Value == handle.Value) Handle1.Value = -1;
            if (Handle2.Value == handle.Value) Handle2.Value = -1;
            if (Handle3.Value == handle.Value) Handle3.Value = -1;
            if (Handle4.Value == handle.Value) Handle4.Value = -1;
        }
    }
}