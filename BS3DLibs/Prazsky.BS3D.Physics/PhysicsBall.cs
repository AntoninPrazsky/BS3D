using BepuPhysics;
using Prazsky.BS3D.GameStructure;

namespace Prazsky.BS3D.Physics
{
	public struct PhysicsBall
	{
		public BodyReference BallReference;

		public ConstraintHandles HandlesTop;
		public ConstraintHandles HandlesMiddle;
		public ConstraintHandles HandlesBottom;

		public eBallType Type;

		public void SetEmptyConstraints()
		{
			HandlesTop.Handle1.Value = -1;
			HandlesTop.Handle2.Value = -1;
			HandlesTop.Handle3.Value = -1;
			HandlesTop.Handle4.Value = -1;

			HandlesMiddle.Handle1.Value = -1;
			HandlesMiddle.Handle2.Value = -1;
			HandlesMiddle.Handle2.Value = -1;
			HandlesMiddle.Handle2.Value = -1;

			HandlesBottom.Handle1.Value = -1;
			HandlesBottom.Handle2.Value = -1;
			HandlesBottom.Handle3.Value = -1;
			HandlesBottom.Handle4.Value = -1;
		}

		public void RemoveAllConstraints(Simulation simulation)
		{
			if (HandlesTop.Handle1.Value > 0) simulation.Solver.Remove(HandlesTop.Handle1);
			if (HandlesTop.Handle2.Value > 0) simulation.Solver.Remove(HandlesTop.Handle2);
			if (HandlesTop.Handle3.Value > 0) simulation.Solver.Remove(HandlesTop.Handle3);
			if (HandlesTop.Handle4.Value > 0) simulation.Solver.Remove(HandlesTop.Handle4);

			if (HandlesMiddle.Handle1.Value > 0) simulation.Solver.Remove(HandlesMiddle.Handle1);
			if (HandlesMiddle.Handle2.Value > 0) simulation.Solver.Remove(HandlesMiddle.Handle2);
			if (HandlesMiddle.Handle3.Value > 0) simulation.Solver.Remove(HandlesMiddle.Handle3);
			if (HandlesMiddle.Handle4.Value > 0) simulation.Solver.Remove(HandlesMiddle.Handle4);

			if (HandlesBottom.Handle1.Value > 0) simulation.Solver.Remove(HandlesBottom.Handle1);
			if (HandlesBottom.Handle2.Value > 0) simulation.Solver.Remove(HandlesBottom.Handle2);
			if (HandlesBottom.Handle3.Value > 0) simulation.Solver.Remove(HandlesBottom.Handle3);
			if (HandlesBottom.Handle4.Value > 0) simulation.Solver.Remove(HandlesBottom.Handle4);

			SetEmptyConstraints();
		}
	}

	public struct ConstraintHandles
	{
		/// <summary>
		/// ↑ || ↖
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
}