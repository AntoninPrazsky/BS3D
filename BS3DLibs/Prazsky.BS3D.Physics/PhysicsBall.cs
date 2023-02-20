using BepuPhysics;
using Prazsky.BS3D.GameStructure;

namespace Prazsky.BS3D.Physics
{
    public class PhysicsBall
    {
        public BodyReference BallReference;

        public ConstraintHandles HandlesTop;
        public ConstraintHandles HandlesMiddle;
        public ConstraintHandles HandlesBottom;

        public BallType Type;

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