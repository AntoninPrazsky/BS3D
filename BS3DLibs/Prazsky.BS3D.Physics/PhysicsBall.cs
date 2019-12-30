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
			HandlesTop.Handle1 = -1;
			HandlesTop.Handle2 = -1;
			HandlesTop.Handle3 = -1;
			HandlesTop.Handle4 = -1;

			HandlesMiddle.Handle1 = -1;
			HandlesMiddle.Handle2 = -1;
			HandlesMiddle.Handle2 = -1;
			HandlesMiddle.Handle2 = -1;

			HandlesBottom.Handle1 = -1;
			HandlesBottom.Handle2 = -1;
			HandlesBottom.Handle3 = -1;
			HandlesBottom.Handle4 = -1;
		}
	}

	public struct ConstraintHandles
	{
		/// <summary>
		/// ↑ || ↖
		/// </summary>
		public int Handle1; // ↑ || ↖

		/// <summary>
		/// ← || ↗
		/// </summary>
		public int Handle2; // ← || ↗

		/// <summary>
		/// → || ↙
		/// </summary>
		public int Handle3; // → || ↙

		/// <summary>
		/// ↓ || ↘
		/// </summary>
		public int Handle4; // ↓ || ↘
	}
}