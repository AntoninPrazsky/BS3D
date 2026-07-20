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

        public XZLevel ArrayPosition;

        /// <summary>
        /// Occlusion data of a ball with nothing packed around it.
        /// </summary>
        public static readonly System.Numerics.Vector4 UNOCCLUDED = new(0f, 0f, 0f, 1f);

        /// <summary>
        /// Ambient occlusion the ball is currently rendered with: XYZ = direction towards its occupied
        /// neighbouring cells, W = base occlusion factor. It is kept on the ball rather than in the renderer
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
    /// Four constraint handle slots. A ball touches at most four neighbours in each group
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