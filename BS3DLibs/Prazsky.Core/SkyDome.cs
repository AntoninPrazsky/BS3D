using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Runtime.InteropServices;

namespace Prazsky.Core
{
	/// <summary>
	/// The sky: one procedurally built dome mesh and a stored palette per sky (#113). The eighteen skies
	/// used to be eighteen .dae models built by three content pipelines; they were one shared geometry that
	/// only ever differed in vertex colour, so the geometry now lives here once (<c>SkyDome.Data.cs</c>,
	/// captured byte-for-byte from the built models) and a sky is just a palette entry. Everything
	/// observable is unchanged: the vertex layout, the triangle order (and so the winding the Testbed's and
	/// the editor's inherited cull state depends on), the sRGB palette the light rig decodes, the
	/// sRGB-to-linear buffer rewrite, and both draw paths.
	/// </summary>
	public partial class SkyDome : IDisposable
	{
		/// <summary>How many skies there are — the palette table's length, counted 1-based by callers.</summary>
		public const byte Count = 18;

		private const int VERTEX_COUNT = 92;
		private const int TRIANGLE_COUNT = 180;

		/// <summary>
		/// The exact layout the content pipeline used to emit for the dome models, kept so the buffer stays
		/// byte-identical to what shipped: position, normal, colour, stride 28.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct DomeVertex : IVertexType
		{
			public Vector3 Position;
			public Vector3 Normal;
			public Color Color;

			public static readonly VertexDeclaration Declaration = new(
				new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
				new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
				new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

			readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
		}

		private readonly GraphicsDevice _graphicsDevice;
		private readonly bool _linearVertexColors;

		//The CPU copy the palette is written into on every dome change; reused, so a change allocates nothing.
		private readonly DomeVertex[] _vertices = new DomeVertex[VERTEX_COUNT];

		private VertexBuffer _vertexBuffer;
		private IndexBuffer _indexBuffer;

		//The Effect == null draw path used to run through the BasicEffects the content pipeline baked from
		//the .dae's white material; this instance replicates that state (unlit vertex colour, verbatim).
		private BasicEffect _basicEffect;

		private Effect _effect;
		private EffectParameter _worldParam;
		private EffectParameter _viewParam;
		private EffectParameter _projectionParam;
		private EffectPass _effectPass;

		private int _domeNumber;

		/// <summary>
		/// Draws the dome with this effect instead of the owned <see cref="BasicEffect"/>, which is how the
		/// Testbed and the game put procedural clouds over the baked gradient. The effect is expected to
		/// declare <c>World</c>, <c>View</c> and <c>Projection</c> (cached here on assignment — the by-name
		/// indexer is a linear scan and this draws every frame); everything else it needs is the caller's
		/// business and is set on the effect directly. Leave it null for the plain gradient — the map editor
		/// does, and does not build a sky shader at all.
		/// </summary>
		public Effect Effect
		{
			get => _effect;
			set
			{
				_effect = value;
				_worldParam = value?.Parameters["World"];
				_viewParam = value?.Parameters["View"];
				_projectionParam = value?.Parameters["Projection"];
				_effectPass = value?.CurrentTechnique.Passes[0];
			}
		}

		/// <summary>
		/// Which sky is up, 1-based like the level format's <c>"sky"</c> byte. Assignment rewrites the
		/// vertex colours from the palette, re-extracts <see cref="ZenithColor"/>/<see cref="HorizonColor"/>
		/// and re-derives <see cref="SunDirection"/> — the contract the hosts' SetSkyDome paths rely on
		/// before re-deriving the light rig. Cheap enough to set redundantly (the game does, on every scene
		/// change): 92 vertices recoloured and re-uploaded.
		/// </summary>
		public int DomeNumber
		{
			get => _domeNumber;
			set
			{
				if (value < 1 || value > Count)
					throw new ArgumentOutOfRangeException(nameof(value), value, $"Sky dome numbers run 1..{Count}.");

				_domeNumber = value;

				(float elevation, float azimuth) = SUNS[_domeNumber - 1];
				float elevationRadians = MathHelper.ToRadians(elevation);
				float azimuthRadians = MathHelper.ToRadians(azimuth);
				float horizontal = MathF.Cos(elevationRadians);

				SunDirection = new Vector3(
					horizontal * MathF.Sin(azimuthRadians),
					MathF.Sin(elevationRadians),
					horizontal * MathF.Cos(azimuthRadians));

				ApplyPalette();
			}
		}

		/// <summary>
		/// Towards this sky's sun, normalized by construction — the direction the whole scene is lit and
		/// shadowed along, and where <c>Sky.fx</c> draws the disc. A dome states its own since #220; before
		/// that one constant lit all eighteen, so nothing in a frame said whether it was morning or dusk but
		/// the two palette colours. The figures, and what an elevation and an azimuth each decide, are in
		/// <c>SUNS</c>.
		/// <para>
		/// The light rig reads it in <c>SetSky</c> and is where callers should take it from, not from here:
		/// a scene that replaces the sky has no dome to read a sun off and the rig substitutes its own.
		/// </para>
		/// </summary>
		public Vector3 SunDirection { get; private set; }

		/// <summary>Average vertex colour near the top of the dome, sRGB (see <see cref="HorizonColor"/>).</summary>
		public Vector3 ZenithColor { get; private set; }

		/// <summary>
		/// Average vertex colour near the base of the dome. Both palette colours stay sRGB deliberately:
		/// the caller decodes them to linear on the CPU (ApplySkyLighting, through ColorSpace.SrgbToLinear)
		/// along with the rest of the light rig — extracted after the buffer's own linearization they would
		/// be decoded twice. Only the drawn geometry, which the effects write into the HDR target without
		/// knowing about any of this, is converted (see <see cref="ApplyPalette"/>).
		/// </summary>
		public Vector3 HorizonColor { get; private set; }

		/// <param name="domeNumber">The starting sky, 1..<see cref="Count"/>.</param>
		/// <param name="linearVertexColors">
		/// Converts the dome's vertex colors from sRGB to linear when the buffer is (re)built. Set this when
		/// the caller renders into a linear HDR target and tonemaps at the end of the frame — every current
		/// executable does; leave it off only for a caller drawing straight to an 8-bit back buffer in gamma
		/// space.
		/// </param>
		public SkyDome(GraphicsDevice graphicsDevice, int domeNumber, bool linearVertexColors = false)
		{
			_graphicsDevice = graphicsDevice;
			_linearVertexColors = linearVertexColors;

			for (int i = 0; i < VERTEX_COUNT; i++)
			{
				_vertices[i].Position = new Vector3(GEOMETRY[i * 6], GEOMETRY[i * 6 + 1], GEOMETRY[i * 6 + 2]);
				_vertices[i].Normal = new Vector3(GEOMETRY[i * 6 + 3], GEOMETRY[i * 6 + 4], GEOMETRY[i * 6 + 5]);
			}

			_vertexBuffer = new VertexBuffer(graphicsDevice, DomeVertex.Declaration, VERTEX_COUNT, BufferUsage.WriteOnly);
			_indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, INDICES.Length, BufferUsage.WriteOnly);
			_indexBuffer.SetData(INDICES);

			//What the content pipeline used to bake from the .dae's white phong material: unlit vertex
			//colour, verbatim (diffuse (1,1,1) and alpha 1 are BasicEffect's own defaults; lighting and
			//texturing are off by default). The specular the material carried was inert with lighting off.
			_basicEffect = new BasicEffect(graphicsDevice) { VertexColorEnabled = true };

			DomeNumber = domeNumber;
		}

		/// <summary>
		/// Writes the current dome's palette into the vertices and re-uploads the buffer. The palette is
		/// read for <see cref="ZenithColor"/>/<see cref="HorizonColor"/> first, in sRGB; only then are the
		/// buffer's colours converted to linear (when asked to) — the same order the Model-based load used,
		/// and for the same reason (the palette's consumer decodes it itself). Rewriting from the stored
		/// sRGB table every time also makes the conversion idempotent by construction, which the old code
		/// needed a static already-linearized guard for, the content manager handing back cached buffers.
		/// </summary>
		private void ApplyPalette()
		{
			string palette = PALETTES[_domeNumber - 1];

			//The extraction the light rig was tuned against, reproduced exactly: thresholds from the mesh's
			//own vertical extent, colours as byte/255 floats, averaged in vertex order (float accumulation
			//order matters for bit-equality with the values every recorded look was derived under).
			float minY = float.MaxValue;
			float maxY = float.MinValue;

			for (int i = 0; i < VERTEX_COUNT; i++)
			{
				float y = _vertices[i].Position.Y;
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

			for (int i = 0; i < VERTEX_COUNT; i++)
			{
				byte r = HexByte(palette, i * 6);
				byte g = HexByte(palette, i * 6 + 2);
				byte b = HexByte(palette, i * 6 + 4);

				Vector3 color = new(r / 255f, g / 255f, b / 255f);
				float y = _vertices[i].Position.Y;

				if (y >= zenithThreshold) { zenithSum += color; zenithCount++; }
				if (y <= horizonThreshold) { horizonSum += color; horizonCount++; }

				//RGB only ever gets encoded - the fourth byte is alpha, a coverage fraction, and stays opaque
				if (_linearVertexColors)
				{
					r = SrgbToLinearByte(r);
					g = SrgbToLinearByte(g);
					b = SrgbToLinearByte(b);
				}

				_vertices[i].Color = new Color(r, g, b, byte.MaxValue);
			}

			ZenithColor = zenithSum / zenithCount;
			HorizonColor = horizonSum / horizonCount;

			_vertexBuffer.SetData(_vertices);
		}

		/// <summary>
		/// The exact sRGB decoding curve, quantized back to a byte. Eight bits of linear light crush the
		/// darks badly in principle, but the domes are smooth gradients that never go near black, and
		/// keeping the byte-sized vertex colour avoids widening every vertex.
		/// </summary>
		private static byte SrgbToLinearByte(byte encoded)
		{
			float c = encoded / 255f;
			float linear = c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);

			return (byte)Math.Round(MathHelper.Clamp(linear, 0f, 1f) * 255f);
		}

		private static byte HexByte(string hex, int index) => (byte)((Nibble(hex[index]) << 4) | Nibble(hex[index + 1]));

		private static int Nibble(char c) => c <= '9' ? c - '0' : c - 'A' + 10;

		public void Draw(ICamera camera)
		{
			//The framework's cached states, never fresh instances: this runs every frame in all three
			//executables, and a state object constructed here backs a native D3D11 state that is never
			//disposed — per-frame construction is a steady leak of finalizer-queue objects.
			_graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
			_graphicsDevice.DepthStencilState = DepthStencilState.None;

			if (_effect == null) DrawWithBasicEffect(camera); else DrawWithCustomEffect(camera);

			_graphicsDevice.DepthStencilState = DepthStencilState.Default;
		}

		/// <summary>The plain path: the owned <see cref="BasicEffect"/>, gradient and nothing else.</summary>
		private void DrawWithBasicEffect(ICamera camera)
		{
			_basicEffect.World = Matrix.CreateTranslation(camera.Position);
			_basicEffect.View = camera.View;
			_basicEffect.Projection = camera.Projection;

			_graphicsDevice.SetVertexBuffer(_vertexBuffer);
			_graphicsDevice.Indices = _indexBuffer;

			_basicEffect.CurrentTechnique.Passes[0].Apply();
			_graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TRIANGLE_COUNT);
		}

		/// <summary>
		/// Issues the dome against <see cref="Effect"/>. The dome is translated to the camera every frame,
		/// which is what puts it at infinity — and it is also what lets the sky shader recover the view ray
		/// as nothing more than the world position minus the camera. (The loaded models carried an identity
		/// bone transform on top of this; there is deliberately no equivalent left to multiply in.)
		/// </summary>
		private void DrawWithCustomEffect(ICamera camera)
		{
			_worldParam.SetValue(Matrix.CreateTranslation(camera.Position));
			_viewParam.SetValue(camera.View);
			_projectionParam.SetValue(camera.Projection);

			_graphicsDevice.SetVertexBuffer(_vertexBuffer);
			_graphicsDevice.Indices = _indexBuffer;

			_effectPass.Apply();
			_graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TRIANGLE_COUNT);
		}

		/// <summary>
		/// Both buffers and the owned <see cref="BasicEffect"/> — never <see cref="Effect"/>, which the
		/// caller's content manager owns.
		/// </summary>
		public void Dispose()
		{
			_vertexBuffer?.Dispose();
			_vertexBuffer = null;
			_indexBuffer?.Dispose();
			_indexBuffer = null;
			_basicEffect?.Dispose();
			_basicEffect = null;
		}
	}
}
