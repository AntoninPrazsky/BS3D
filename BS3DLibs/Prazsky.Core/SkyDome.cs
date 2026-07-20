using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System.Collections.Generic;

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

		private readonly bool _linearVertexColors;

		/// <summary>
		/// Vertex buffers whose colors have already been converted to linear. The content manager caches
		/// models, so cycling back to a dome hands back the very same buffers, and without this they would
		/// be converted a second time and the sky would darken with every pass through the list.
		/// </summary>
		private static readonly HashSet<VertexBuffer> LinearizedBuffers = new();

		/// <param name="linearVertexColors">
		/// Converts the dome's vertex colors from sRGB to linear once, at load. Set this when the caller
		/// renders into a linear HDR target and tonemaps at the end of the frame (the Testbed); leave it
		/// off when drawing straight to an 8-bit back buffer in gamma space (the map editor).
		/// </param>
		public SkyDome(Model skyDome, GraphicsDevice graphicsDevice, bool linearVertexColors = false)
		{
			_linearVertexColors = linearVertexColors;
			_skyDomeModel = skyDome;
			InitializeModel();
			GraphicsDevice = graphicsDevice;
		}

		/// <summary>
		/// Average vertex color near the top of the dome. White when the dome has no vertex color channel.
		/// </summary>
		public Vector3 ZenithColor { get; private set; } = Vector3.One;

		/// <summary>
		/// Average vertex color near the base of the dome. White when the dome has no vertex color channel.
		/// </summary>
		public Vector3 HorizonColor { get; private set; } = Vector3.One;

		private void InitializeModel()
		{
			_skyDomeTransforms = new Matrix[SkyDomeModel.Bones.Count];
			SkyDomeModel.CopyAbsoluteBoneTransformsTo(_skyDomeTransforms);

			//Order matters: the palette is read first and stays in sRGB, because it is handed to the scene
			//shader as a plain color parameter and gets linearized there along with every other one. Only
			//the geometry, which BasicEffect writes into the HDR target without knowing about any of this,
			//is converted here.
			ExtractSkyColors();

			if (_linearVertexColors) LinearizeVertexColors();
		}

		/// <summary>
		/// Rewrites the dome's vertex colors from sRGB to linear, in place. The domes are drawn through
		/// <see cref="BasicEffect"/>, which has no notion of a color space and would otherwise write the
		/// display encoding straight into a linear target, leaving the tonemapper to treat a gradient of
		/// display values as if it were radiance.
		/// </summary>
		private void LinearizeVertexColors()
		{
			HashSet<VertexBuffer> processed = new();

			foreach (ModelMesh mesh in _skyDomeModel.Meshes)
			{
				foreach (ModelMeshPart part in mesh.MeshParts)
				{
					VertexBuffer buffer = part.VertexBuffer;
					if (buffer == null || !processed.Add(buffer) || !LinearizedBuffers.Add(buffer)) continue;

					int stride = buffer.VertexDeclaration.VertexStride;
					int colorOffset = -1;

					foreach (VertexElement element in buffer.VertexDeclaration.GetVertexElements())
					{
						if (element.VertexElementUsage == VertexElementUsage.Color && element.VertexElementFormat == VertexElementFormat.Color)
							colorOffset = element.Offset;
					}

					if (colorOffset < 0) continue;

					byte[] data = new byte[buffer.VertexCount * stride];
					buffer.GetData(data);

					for (int i = 0; i < buffer.VertexCount; i++)
					{
						int channel = i * stride + colorOffset;

						//RGB only - the fourth byte is alpha, which is a coverage fraction and never encoded
						for (int c = 0; c < 3; c++) data[channel + c] = SrgbToLinearByte(data[channel + c]);
					}

					buffer.SetData(data);
				}
			}
		}

		/// <summary>
		/// The exact sRGB decoding curve, quantized back to a byte. Eight bits of linear light crush the
		/// darks badly in principle, but the domes are smooth gradients that never go near black, and
		/// keeping the vertex format untouched avoids rebuilding every dome's buffers.
		/// </summary>
		private static byte SrgbToLinearByte(byte encoded)
		{
			float c = encoded / 255f;
			float linear = c <= 0.04045f ? c / 12.92f : (float)System.Math.Pow((c + 0.055f) / 1.055f, 2.4f);

			return (byte)System.Math.Round(MathHelper.Clamp(linear, 0f, 1f) * 255f);
		}

		/// <summary>
		/// Recovers the sky palette from the dome geometry: the domes are untextured vertex-colored
		/// gradients, so averaging the vertex colors of the top band gives the zenith color and
		/// averaging the bottom band gives the horizon color. Used to tint the scene lighting.
		/// </summary>
		private void ExtractSkyColors()
		{
			List<(float y, Vector3 color)> vertices = new();
			HashSet<VertexBuffer> processed = new();

			foreach (ModelMesh mesh in _skyDomeModel.Meshes)
			{
				foreach (ModelMeshPart part in mesh.MeshParts)
				{
					VertexBuffer buffer = part.VertexBuffer;
					if (buffer == null || !processed.Add(buffer)) continue;

					int stride = buffer.VertexDeclaration.VertexStride;
					int positionOffset = -1;
					int colorOffset = -1;

					foreach (VertexElement element in buffer.VertexDeclaration.GetVertexElements())
					{
						if (element.VertexElementUsage == VertexElementUsage.Position && element.VertexElementFormat == VertexElementFormat.Vector3)
							positionOffset = element.Offset;
						if (element.VertexElementUsage == VertexElementUsage.Color && element.VertexElementFormat == VertexElementFormat.Color)
							colorOffset = element.Offset;
					}

					if (positionOffset < 0 || colorOffset < 0) continue;

					byte[] data = new byte[buffer.VertexCount * stride];
					buffer.GetData(data);

					for (int i = 0; i < buffer.VertexCount; i++)
					{
						int vertexStart = i * stride;
						float y = System.BitConverter.ToSingle(data, vertexStart + positionOffset + sizeof(float));
						Vector3 color = new(
							data[vertexStart + colorOffset] / 255f,
							data[vertexStart + colorOffset + 1] / 255f,
							data[vertexStart + colorOffset + 2] / 255f);

						vertices.Add((y, color));
					}
				}
			}

			if (vertices.Count == 0)
			{
				ZenithColor = Vector3.One;
				HorizonColor = Vector3.One;
				return;
			}

			float minY = float.MaxValue;
			float maxY = float.MinValue;

			foreach ((float y, _) in vertices)
			{
				if (y < minY) minY = y;
				if (y > maxY) maxY = y;
			}

			float range = maxY - minY;
			float zenithThreshold = minY + range * 0.75f;
			float horizonThreshold = minY + range * 0.2f;

			Vector3 zenithSum = Vector3.Zero;
			Vector3 horizonSum = Vector3.Zero;
			int zenithCount = 0;
			int horizonCount = 0;

			foreach ((float y, Vector3 color) in vertices)
			{
				if (y >= zenithThreshold) { zenithSum += color; zenithCount++; }
				if (y <= horizonThreshold) { horizonSum += color; horizonCount++; }
			}

			ZenithColor = zenithCount > 0 ? zenithSum / zenithCount : Vector3.One;
			HorizonColor = horizonCount > 0 ? horizonSum / horizonCount : Vector3.One;
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