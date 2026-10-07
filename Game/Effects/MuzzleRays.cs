using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The special round's rays</b> (#820): while the round in the bore is a special one, a burst of light turns slowly
    /// round the muzzle. The owner met the wildcard for the first time in play and did not notice it was anything but a
    /// ball: "whenever I have any special ball loaded (even Cut), I want a strong rotating glow from the cannon, in rays",
    /// so the player knows they hold something rare and enjoys firing it.
    /// <para>
    /// <b>Bolted to the barrel, not facing the lens</b> - the lesson of #425, which took the camera-facing halo out because
    /// its visible shape was whatever the tube happened not to cover from the current view. The quad lies in the plane
    /// square to the bore at the muzzle face and is drawn with the barrel's own pose, so it turns, recoils and nods off
    /// with the gun; from the play camera, behind the gun, that plane is seen nearly face on, the tube covers its middle,
    /// and the rays come out round the muzzle. Everything it draws is <c>MuzzleRays.fx</c>'s.
    /// </para>
    /// <para>
    /// Added light, depth-read: the cluster in front of the muzzle covers it, and it covers nothing - a pale glow over
    /// what is behind it, never a shape over the field. The Potato path has no effect for it and draws none.
    /// </para>
    /// </summary>
    internal sealed class MuzzleRays
    {
        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly VertexBuffer _vertexBuffer;
        private readonly IndexBuffer _indexBuffer;

        //Cached parameter handles: the by-name indexer is a linear scan
        private readonly EffectParameter _worldParam, _viewParam, _projectionParam, _shapeParam, _lightParam;

        public MuzzleRays(GraphicsDevice device, Effect effect)
        {
            _device = device;
            _effect = effect;

            _worldParam = effect.Parameters["World"];
            _viewParam = effect.Parameters["View"];
            _projectionParam = effect.Parameters["Projection"];
            _shapeParam = effect.Parameters["RayShape"];
            _lightParam = effect.Parameters["RayLight"];

            _vertexBuffer = new VertexBuffer(device, CornerVertex.Declaration, 4, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(new CornerVertex[]
            {
                new() { Corner = new Vector2(-1f, 1f) },
                new() { Corner = new Vector2(1f, 1f) },
                new() { Corner = new Vector2(1f, -1f) },
                new() { Corner = new Vector2(-1f, -1f) },
            });

            _indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
            _indexBuffer.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
        }

        /// <summary>
        /// Draws the burst for this frame. Nothing at a <paramref name="light"/> of zero.
        /// </summary>
        /// <param name="barrelWorld">The pose the barrel was drawn with this frame, recoil stroke and all - anything else and
        /// the burst slides along the tube.</param>
        /// <param name="muzzleFaceZ">The muzzle face along the bore, barrel space (<c>CannonRig.MuzzleFaceZ</c>).</param>
        /// <param name="root">Where the rays start: the collar's crest radius (<c>CannonRig.CollarRadius</c>).</param>
        /// <param name="reach">Where they end, world units from the bore's axis.</param>
        /// <param name="spin">The broad rays' turn, radians.</param>
        /// <param name="light">The radiance at a ray's root, linear, strength included: the colour of a plain burst, and
        /// only the brightness of a <paramref name="rainbow"/> one.</param>
        /// <param name="rainbow">True colours the burst by angle, one hue a broad ray - the wildcard's.</param>
        public void Draw(ICamera camera, in Matrix barrelWorld, float muzzleFaceZ, float root, float reach, float spin,
            Vector3 light, bool rainbow)
        {
            if (light == Vector3.Zero || reach <= root) return;

            //The plane square to the bore at the muzzle face: the quad's own XY is barrel space's, so only the offset
            //along the bore is added, written into the translation row rather than multiplied in
            Matrix world = barrelWorld;
            world.M41 += barrelWorld.M31 * muzzleFaceZ;
            world.M42 += barrelWorld.M32 * muzzleFaceZ;
            world.M43 += barrelWorld.M33 * muzzleFaceZ;

            _worldParam.SetValue(world);
            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _shapeParam.SetValue(new Vector4(root, reach, spin, rainbow ? 1f : 0f));
            _lightParam.SetValue(light);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        private struct CornerVertex : IVertexType
        {
            public Vector2 Corner;

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }
    }
}
