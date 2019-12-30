using Microsoft.Xna.Framework;
using Prazsky.Render;

namespace Prazsky.BS3D.GameStructure
{
	public static class BasicEffectParamsProvider
	{
		public static BasicEffectParams colorRed = new BasicEffectParams(new Vector3(0.3f, 0f, 0f), new Vector3(0.8f, 0f, 0f), 0.5f, Vector3.Zero);
		public static BasicEffectParams colorGreen = new BasicEffectParams(new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.8f, 0f), 0.5f, Vector3.Zero);
		public static BasicEffectParams colorBlue = new BasicEffectParams(new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, 0.8f), 0.5f, Vector3.Zero);

		public static BasicEffectParams GetEffectByType(eBallType ballType)
		{
			switch (ballType)
			{
				case eBallType.Type1:
					return colorRed;

				case eBallType.Type2:
					return colorGreen;

				case eBallType.Type3:
					return colorBlue;

				default:
					return null;
			}
		}
	}
}