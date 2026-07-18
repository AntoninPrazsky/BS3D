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

    public struct ConstraintHandles
    {
        /// <summary>
        /// ↑ || ↖
        /// And also used for ceiling constraint.
        /// </summary>
        public ConstraintHandle Handle1; // ↑ || ↖

        /// <summary>
        /// ← || ↗
        /// </summary>
        public ConstraintHandle Handle2; // ← || ↗

        /// <summary>
        /// → || ↙
        /// </summary>
        public ConstraintHandle Handle3; // → || ↙

        /// <summary>
        /// ↓ || ↘
        /// </summary>
        public ConstraintHandle Handle4; // ↓ || ↘

        /// <summary>
        /// Stores the handle into the first free slot. Used when attaching a shot ball after the map was built,
        /// where the direction-based slot convention of the build-time pass is not needed (removal iterates all slots anyway).
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

    public enum ConstraintType : byte
    {
        None = 0,

        // 1 2
        // 3 4
        Type1 = 1,
        Type2 = 2,
        Type3 = 3,
        Type4 = 4,

        //   5
        // 6   7
        //   8
        Type5 = 5,
        Type6 = 6,
        Type7 = 7,
        Type8 = 8,

        // 9  10
        // 11 12
        Type9 = 9,
        Type10 = 10,
        Type11 = 11,
        Type12 = 12
    }
}