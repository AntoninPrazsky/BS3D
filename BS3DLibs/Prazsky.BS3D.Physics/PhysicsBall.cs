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