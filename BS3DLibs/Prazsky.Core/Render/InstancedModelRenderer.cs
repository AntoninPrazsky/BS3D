using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Render
{
    /// <summary>
    /// Renders many instances of a single three-dimensional model using GPU (hardware) instancing:
    /// one draw call per model mesh part, no matter how many instances there are.
    /// Lighting mirrors <see cref="BasicEffect"/> with default lighting enabled and per-pixel shading,
    /// so instanced models look the same as those rendered through <see cref="ModelRenderer"/>.
    /// </summary>
    public class InstancedModelRenderer : IDisposable
    {
        #region BasicEffect.EnableDefaultLighting values (the standard XNA three-light rig)

        private static readonly Vector3 DEFAULT_AMBIENT_LIGHT_COLOR = new(0.05333332f, 0.09882354f, 0.1819608f);

        private static readonly Vector3 LIGHT0_DIRECTION = new(-0.5265408f, -0.5735765f, -0.6275069f);
        private static readonly Vector3 LIGHT0_DIFFUSE = new(1f, 0.9607844f, 0.8078432f);
        private static readonly Vector3 LIGHT0_SPECULAR = new(1f, 0.9607844f, 0.8078432f);

        private static readonly Vector3 LIGHT1_DIRECTION = new(0.7198464f, 0.3420201f, 0.4293262f);
        private static readonly Vector3 LIGHT1_DIFFUSE = new(0.9647059f, 0.7607844f, 0.4078432f);
        private static readonly Vector3 LIGHT1_SPECULAR = Vector3.Zero;

        private static readonly Vector3 LIGHT2_DIRECTION = new(0.4545195f, -0.7660444f, 0.4545195f);
        private static readonly Vector3 LIGHT2_DIFFUSE = new(0.3231373f, 0.3607844f, 0.3937255f);
        private static readonly Vector3 LIGHT2_SPECULAR = new(0.3231373f, 0.3607844f, 0.3937255f);

        private static readonly Vector3 DEFAULT_SPECULAR_COLOR = Vector3.One;
        private const float DEFAULT_SPECULAR_POWER = 16f;

        #endregion

        //The per-instance world matrix travels in a second vertex stream as four rows (TEXCOORD1-TEXCOORD4)
        private static readonly VertexDeclaration INSTANCE_VERTEX_DECLARATION = new(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
            new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4));

        private struct MeshPartData
        {
            public VertexBuffer VertexBuffer;
            public IndexBuffer IndexBuffer;
            public int VertexOffset;
            public int StartIndex;
            public int PrimitiveCount;
            public Matrix BoneTransform;
            public Vector4 DiffuseColor;
            public Vector3 EmissiveColor;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly MeshPartData[] _parts;
        private DynamicVertexBuffer _instanceBuffer;

        private readonly EffectParameter _viewParam;
        private readonly EffectParameter _projectionParam;
        private readonly EffectParameter _boneParam;
        private readonly EffectParameter _eyePositionParam;
        private readonly EffectParameter _diffuseColorParam;
        private readonly EffectParameter _emissiveColorParam;
        private readonly EffectParameter _specularColorParam;
        private readonly EffectParameter _specularPowerParam;

        /// <summary>
        /// Bounding sphere of the whole model in model space (bone transforms applied). Useful for frustum culling.
        /// </summary>
        public BoundingSphere BoundingSphere { get; }

        /// <summary>
        /// Creates a renderer for drawing many instances of the given model.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device to draw with (requires <see cref="GraphicsProfile.HiDef"/>).</param>
        /// <param name="model">Three-dimensional model whose instances will be rendered.
        /// Material colors are taken from the <see cref="BasicEffect"/>s the model was loaded with.</param>
        /// <param name="effect">The instancing effect (Shaders/InstancedModel.fx compiled by the content pipeline).</param>
        public InstancedModelRenderer(GraphicsDevice graphicsDevice, Model model, Effect effect)
        {
            _graphicsDevice = graphicsDevice;
            _effect = effect;

            Matrix[] boneTransforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);

            List<MeshPartData> parts = new();
            BoundingSphere bounds = default;
            bool firstMesh = true;

            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix boneTransform = boneTransforms[mesh.ParentBone.Index];

                BoundingSphere meshBounds = mesh.BoundingSphere.Transform(boneTransform);
                bounds = firstMesh ? meshBounds : BoundingSphere.CreateMerged(bounds, meshBounds);
                firstMesh = false;

                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    Vector3 diffuse = Vector3.One;
                    Vector3 emissive = Vector3.Zero;
                    float alpha = 1f;

                    if (part.Effect is BasicEffect material)
                    {
                        diffuse = material.DiffuseColor;
                        emissive = material.EmissiveColor;
                        alpha = material.Alpha;
                    }

                    parts.Add(new MeshPartData
                    {
                        VertexBuffer = part.VertexBuffer,
                        IndexBuffer = part.IndexBuffer,
                        VertexOffset = part.VertexOffset,
                        StartIndex = part.StartIndex,
                        PrimitiveCount = part.PrimitiveCount,
                        BoneTransform = boneTransform,
                        DiffuseColor = new Vector4(diffuse, alpha),
                        EmissiveColor = emissive
                    });
                }
            }

            _parts = parts.ToArray();
            BoundingSphere = bounds;

            _viewParam = _effect.Parameters["View"];
            _projectionParam = _effect.Parameters["Projection"];
            _boneParam = _effect.Parameters["Bone"];
            _eyePositionParam = _effect.Parameters["EyePosition"];
            _diffuseColorParam = _effect.Parameters["DiffuseColor"];
            _emissiveColorParam = _effect.Parameters["EmissiveColor"];
            _specularColorParam = _effect.Parameters["SpecularColor"];
            _specularPowerParam = _effect.Parameters["SpecularPower"];

            //The light rig never changes, so it is uploaded only once
            _effect.Parameters["DirLight0Direction"].SetValue(LIGHT0_DIRECTION);
            _effect.Parameters["DirLight0DiffuseColor"].SetValue(LIGHT0_DIFFUSE);
            _effect.Parameters["DirLight0SpecularColor"].SetValue(LIGHT0_SPECULAR);
            _effect.Parameters["DirLight1Direction"].SetValue(LIGHT1_DIRECTION);
            _effect.Parameters["DirLight1DiffuseColor"].SetValue(LIGHT1_DIFFUSE);
            _effect.Parameters["DirLight1SpecularColor"].SetValue(LIGHT1_SPECULAR);
            _effect.Parameters["DirLight2Direction"].SetValue(LIGHT2_DIRECTION);
            _effect.Parameters["DirLight2DiffuseColor"].SetValue(LIGHT2_DIFFUSE);
            _effect.Parameters["DirLight2SpecularColor"].SetValue(LIGHT2_SPECULAR);
        }

        /// <summary>
        /// Draws the given instances of the model in one draw call per model mesh part.
        /// </summary>
        /// <param name="camera">A camera that looks at the resulting rendering.</param>
        /// <param name="instances">World matrices of the individual instances. Only the first <paramref name="instanceCount"/> entries are drawn.</param>
        /// <param name="instanceCount">Number of instances to draw.</param>
        /// <param name="effectParams">Lighting parameters shared by all the instances
        /// (<see cref="BasicEffectParams.AmbientLightColor"/>, specular and emissive colors are applied;
        /// zero vectors fall back to the <see cref="BasicEffect"/> defaults, like in <see cref="ModelRenderer"/>).</param>
        public void Draw(ICamera camera, Matrix[] instances, int instanceCount, BasicEffectParams effectParams)
        {
            if (instanceCount <= 0) return;

            EnsureInstanceBufferCapacity(instances.Length);
            _instanceBuffer.SetData(instances, 0, instanceCount, SetDataOptions.Discard);

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _eyePositionParam.SetValue(camera.Position);

            Vector3 ambientLightColor = DEFAULT_AMBIENT_LIGHT_COLOR;
            Vector3 specularColor = DEFAULT_SPECULAR_COLOR;
            float specularPower = DEFAULT_SPECULAR_POWER;
            Vector3 emissiveColor = Vector3.Zero;

            if (effectParams != null)
            {
                if (effectParams.AmbientLightColor != Vector3.Zero) ambientLightColor = effectParams.AmbientLightColor;
                if (effectParams.SpecularColor != Vector3.Zero)
                {
                    specularColor = effectParams.SpecularColor;
                    specularPower = effectParams.SpecularPower;
                }
                if (effectParams.EmmisiveColor != Vector3.Zero) emissiveColor = effectParams.EmmisiveColor;
            }

            _specularColorParam.SetValue(specularColor);
            _specularPowerParam.SetValue(specularPower);

            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                _boneParam.SetValue(part.BoneTransform);
                _diffuseColorParam.SetValue(part.DiffuseColor);

                //Ambient light is folded into the emissive color, exactly like BasicEffect does on the CPU side
                Vector3 diffuse = new(part.DiffuseColor.X, part.DiffuseColor.Y, part.DiffuseColor.Z);
                _emissiveColorParam.SetValue(part.EmissiveColor + emissiveColor + ambientLightColor * diffuse);

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                    new VertexBufferBinding(_instanceBuffer, 0, 1));
                _graphicsDevice.Indices = part.IndexBuffer;

                _effect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, instanceCount);
            }
        }

        private void EnsureInstanceBufferCapacity(int instanceCapacity)
        {
            if (_instanceBuffer != null && _instanceBuffer.VertexCount >= instanceCapacity) return;

            _instanceBuffer?.Dispose();
            _instanceBuffer = new DynamicVertexBuffer(_graphicsDevice, INSTANCE_VERTEX_DECLARATION, instanceCapacity, BufferUsage.WriteOnly);
        }

        public void Dispose()
        {
            _instanceBuffer?.Dispose();
            _instanceBuffer = null;
        }
    }
}
