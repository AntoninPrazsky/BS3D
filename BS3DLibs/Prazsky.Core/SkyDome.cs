using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;

namespace Prazsky.Core
{
	public class SkyDome
	{
		public Model SkyDomeModel
		{
			get
			{
				return _skyDomeModel;
			}
			set
			{
				_skyDomeModel = value;
				InitializeModel();
			}
		}

		public GraphicsDevice GraphicsDevice { get; set; }

		private Model _skyDomeModel;

		private Matrix[] _skyDomeTransforms;

		public SkyDome(Model skyDome, GraphicsDevice graphicsDevice)
		{
			_skyDomeModel = skyDome;
			InitializeModel();
			GraphicsDevice = graphicsDevice;
		}

		private void InitializeModel()
		{
			_skyDomeTransforms = new Matrix[SkyDomeModel.Bones.Count];
			SkyDomeModel.CopyAbsoluteBoneTransformsTo(_skyDomeTransforms);
		}

		public void Draw(ICamera camera)
		{
			SamplerState ss = new SamplerState
			{
				AddressU = TextureAddressMode.Clamp,
				AddressV = TextureAddressMode.Clamp
			};
			GraphicsDevice.SamplerStates[0] = ss;

			DepthStencilState depthStencilState = new DepthStencilState { DepthBufferEnable = false };
			GraphicsDevice.DepthStencilState = depthStencilState;

			int skyDomeModelMeshesCount = _skyDomeModel.Meshes.Count;
			for (int i = 0; i < skyDomeModelMeshesCount; i++)
			{
				int effectsCount = _skyDomeModel.Meshes[i].Effects.Count;
				for (int y = 0; y < effectsCount; y++)
				{
					Matrix worldMatrix = _skyDomeTransforms[_skyDomeModel.Meshes[i].ParentBone.Index] * Matrix.CreateTranslation(camera.Position);

					((BasicEffect)_skyDomeModel.Meshes[i].Effects[y]).World = _skyDomeTransforms[_skyDomeModel.Meshes[i].ParentBone.Index] * worldMatrix;
					((BasicEffect)_skyDomeModel.Meshes[i].Effects[y]).View = camera.View;
					((BasicEffect)_skyDomeModel.Meshes[i].Effects[y]).Projection = camera.Projection;
				}
				_skyDomeModel.Meshes[i].Draw();
			}

			depthStencilState = new DepthStencilState { DepthBufferEnable = true };
			GraphicsDevice.DepthStencilState = depthStencilState;
		}
	}
}