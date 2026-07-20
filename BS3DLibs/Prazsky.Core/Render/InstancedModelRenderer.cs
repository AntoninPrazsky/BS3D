using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Renders many instances of a single three-dimensional model using GPU (hardware) instancing:
    /// one draw call per model mesh part, no matter how many instances there are.
    /// Lighting mirrors <see cref="BasicEffect"/> with default lighting enabled and per-pixel shading,
    /// so instanced models look the same as those rendered through <see cref="ModelRenderer"/>.
    /// </summary>
    public class InstancedModelRenderer : IDisposable
    {
        private static readonly Vector3 DEFAULT_SPECULAR_COLOR = Vector3.One;
        private const float DEFAULT_SPECULAR_POWER = 16f;

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
            public Vector3 SpecularColor;
            public float SpecularPower;
            public Texture2D Texture;
            public string MeshName;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly MeshPartData[] _parts;
        private DynamicVertexBuffer _instanceBuffer;

        private EffectParameter _viewParam;
        private EffectParameter _projectionParam;
        private EffectParameter _boneParam;
        private EffectParameter _eyePositionParam;
        private EffectParameter _diffuseColorParam;
        private EffectParameter _emissiveColorParam;
        private EffectParameter _ambientColorParam;
        private EffectParameter _specularColorParam;
        private EffectParameter _specularPowerParam;
        private EffectParameter _skyColorParam;
        private EffectParameter _groundColorParam;
        private EffectParameter _keyLightPositionParam;
        private EffectParameter _lightViewProjectionParam;
        private EffectParameter _groundHeightParam;
        private EffectParameter _textureParam;
        private EffectParameter _detailScaleParam;
        private EffectParameter _detailStrengthParam;
        private EffectParameter _detailBoostParam;
        private EffectParameter _masonryStrengthParam;
        private EffectParameter _normalMapParam;
        private EffectParameter _normalStrengthParam;
        private EffectParameter _surfaceReliefStrengthParam;
        private EffectParameter _surfaceReliefFrequencyParam;
        private EffectParameter _surfaceStyleParam;
        private EffectParameter _patternPrimaryColorParam;
        private EffectParameter _patternSecondaryColorParam;
        private EffectParameter _patternGoreCountParam;
        private EffectParameter _patternGoreThresholdParam;
        private EffectParameter _patternCapExtentParam;
        private EffectParameter _patternReliefStrengthParam;
        private EffectParameter _patternSheenStrengthParam;
        private EffectTechnique _mainTechnique;
        private EffectTechnique _texturedTechnique;
        private EffectTechnique _triplanarTechnique;
        private EffectTechnique _detailUVTechnique;
        private EffectTechnique _detailUVNormalTechnique;
        private EffectTechnique _patternTechnique;
        private EffectTechnique _depthTechnique;

        /// <summary>
        /// Optional detail texture modulating the material colors of a model that carries no texture
        /// of its own. Applied to the opaque mesh parts only; translucent parts (e.g. glass) stay clean.
        /// See <see cref="DetailTextureMapping"/> for how it is placed on the surface.
        /// </summary>
        public Texture2D DetailTexture { get; set; }

        /// <summary>
        /// How <see cref="DetailTexture"/> is mapped onto the surface. Objects that move or rotate must
        /// use <see cref="Render.DetailMapping.ModelUVs"/>, otherwise the world-space projection makes
        /// the texture swim across them.
        /// </summary>
        public DetailMapping DetailTextureMapping { get; set; } = DetailMapping.Triplanar;

        /// <summary>
        /// Size of the detail texture: world units per tile = 1 / <see cref="DetailScale"/> for
        /// <see cref="Render.DetailMapping.Triplanar"/>, tiles per UV span for <see cref="Render.DetailMapping.ModelUVs"/>.
        /// </summary>
        public float DetailScale { get; set; } = 0.25f;

        /// <summary>How strongly the detail texture modulates the material color (0 = not at all, 1 = fully).</summary>
        public float DetailStrength { get; set; } = 0.5f;

        /// <summary>Brightness compensation so a mid-gray detail texture does not darken the material.</summary>
        public float DetailBoost { get; set; } = 1f;

        /// <summary>
        /// How strongly the procedural construction pattern of a mesh's <see cref="SurfaceStyle"/> shows
        /// on vertical surfaces (0 = plain material). Only applies to <see cref="Render.DetailMapping.Triplanar"/>.
        /// </summary>
        public float MasonryStrength { get; set; }

        private readonly Dictionary<string, SurfaceStyle> _meshStyles = new();

        /// <summary>
        /// Declares what a named mesh of the model is made of. Meshes left undeclared fall back to
        /// <see cref="SurfaceStyle.Masonry"/>, which is the behavior a stone-all-over model had before
        /// styles existed. Names are the model's own mesh names, so a model that is several materials
        /// at once (a castle of stone walls, a timber door and glazing) can say so.
        /// </summary>
        public void SetMeshSurfaceStyle(string meshName, SurfaceStyle style) => _meshStyles[meshName] = style;

        /// <summary>
        /// Optional tangent-space normal map accompanying <see cref="DetailTexture"/>, giving the surface
        /// relief instead of just color variation. Only applies to <see cref="Render.DetailMapping.ModelUVs"/>;
        /// the tangent frame is derived in the shader, so the model needs no tangent vertex data.
        /// </summary>
        public Texture2D DetailNormalMap { get; set; }

        /// <summary>How far <see cref="DetailNormalMap"/> tilts the surface normal (0 = flat).</summary>
        public float DetailNormalStrength { get; set; } = 1f;

        /// <summary>
        /// Peak height of the procedural micro-relief of this model's surface, in world units
        /// (0 = the flat shading of a geometrically perfect surface). It only tilts the normal, so the
        /// silhouette stays exactly as modeled; what changes is that the surface catches light
        /// unevenly the way a real material does. Unlike <see cref="DetailNormalMap"/> it needs no
        /// texture, does not tile, and keeps its detail right down to the pixel that can still show it.
        /// </summary>
        public float SurfaceReliefStrength { get; set; }

        /// <summary>
        /// Base wave count per world unit of <see cref="SurfaceReliefStrength"/>: larger values give a
        /// finer grain. Four more octaves ride on top at rising frequencies, each fading out on its own
        /// once a screen pixel grows past half its wavelength.
        /// </summary>
        public float SurfaceReliefFrequency { get; set; } = 10f;

        /// <summary>
        /// Number of primary-colored gores of the procedural beach-ball pattern (segments around
        /// the object = twice this; 0 = no pattern). The pattern is evaluated in the model's own
        /// object space, so it turns with the object and makes a rolling ball's rotation readable.
        /// Applies to untextured opaque mesh parts; the diffuse tint passed to
        /// <see cref="Draw(ICamera, ModelInstance[], int, BasicEffectParams, Vector3?)"/> becomes
        /// the primary gore color and the material diffuse shades the whole pattern.
        /// </summary>
        public int PatternGoreCount { get; set; }

        /// <summary>Color of the other gores and of the polar discs of the beach-ball pattern.</summary>
        public Vector3 PatternSecondaryColor { get; set; } = Vector3.One;

        /// <summary>
        /// Fraction of each pair of segments taken by the primary-colored gore: 0.5 gives gores of
        /// equal width, more leaves the secondary color as the narrower strip between them.
        /// </summary>
        public float PatternGoreWidth { get; set; } = 0.62f;

        /// <summary>
        /// Where the polar discs of the beach-ball pattern start, as the |Y| of the object-space
        /// direction (1 = the pole itself).
        /// </summary>
        public float PatternCapExtent { get; set; } = 0.9f;

        /// <summary>
        /// Amplitude of the molded micro-relief of the patterned surface, in world units
        /// (0 = a mathematically smooth sphere). It only tilts the normal, so what it changes is the
        /// way the highlight breaks up — the silhouette stays a clean circle.
        /// </summary>
        public float PatternReliefStrength { get; set; } = 0.007f;

        /// <summary>
        /// How strongly the patterned surface catches the sky color at grazing angles (0 = matte).
        /// </summary>
        public float PatternSheenStrength { get; set; } = 0.12f;

        /// <summary>
        /// Sky color of the hemisphere ambient light (received by upward-facing surfaces).
        /// White reproduces a constant ambient term.
        /// </summary>
        public Vector3 SkyColor { get; set; } = Vector3.One;

        /// <summary>
        /// Ground color of the hemisphere ambient light (received by downward-facing surfaces).
        /// White reproduces a constant ambient term.
        /// </summary>
        public Vector3 GroundColor { get; set; } = Vector3.One;

        /// <summary>
        /// World position of the key light (a positional "sun"). The default sits far away along the
        /// default key light direction, which is indistinguishable from a directional light.
        /// </summary>
        public Vector3 KeyLightPosition { get; set; } = -DefaultLighting.Light0Direction * 1000f;

        /// <summary>
        /// Y of the ground plane for the ground-contact part of the ambient occlusion
        /// (downward-facing surface near the ground darkens). The default is far enough below
        /// everything to have no effect.
        /// </summary>
        public float GroundHeight { get; set; } = -10000f;

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
                    Vector3 specular = DEFAULT_SPECULAR_COLOR;
                    float specularPower = DEFAULT_SPECULAR_POWER;
                    float alpha = 1f;
                    Texture2D texture = null;

                    if (part.Effect is BasicEffect material)
                    {
                        diffuse = material.DiffuseColor;
                        emissive = material.EmissiveColor;
                        specular = material.SpecularColor;
                        specularPower = material.SpecularPower;
                        alpha = material.Alpha;
                        if (material.TextureEnabled) texture = material.Texture;
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
                        EmissiveColor = emissive,
                        SpecularColor = specular,
                        SpecularPower = specularPower,
                        Texture = texture,
                        MeshName = mesh.Name
                    });
                }
            }

            _parts = parts.ToArray();
            BoundingSphere = bounds;

            InitializeEffect();
        }

        /// <summary>
        /// Creates a renderer for drawing many instances of a procedurally generated mesh
        /// (e.g. a <see cref="SphereMesh"/> or <see cref="BoxMesh"/>) with the given material diffuse color.
        /// An <paramref name="alpha"/> below one makes the mesh translucent
        /// (draw it after the opaque scene, under <see cref="BlendState.AlphaBlend"/>).
        /// </summary>
        public InstancedModelRenderer(GraphicsDevice graphicsDevice, IProceduralMesh mesh, Vector3 materialDiffuseColor, Effect effect, float alpha = 1f)
        {
            _graphicsDevice = graphicsDevice;
            _effect = effect;

            _parts = new[]
            {
                new MeshPartData
                {
                    VertexBuffer = mesh.VertexBuffer,
                    IndexBuffer = mesh.IndexBuffer,
                    VertexOffset = 0,
                    StartIndex = 0,
                    PrimitiveCount = mesh.PrimitiveCount,
                    BoneTransform = Matrix.Identity,
                    DiffuseColor = new Vector4(materialDiffuseColor, alpha),
                    EmissiveColor = Vector3.Zero,
                    SpecularColor = DEFAULT_SPECULAR_COLOR,
                    SpecularPower = DEFAULT_SPECULAR_POWER
                }
            };

            BoundingSphere = mesh.BoundingSphere;

            InitializeEffect();
        }

        private void InitializeEffect()
        {
            _viewParam = _effect.Parameters["View"];
            _projectionParam = _effect.Parameters["Projection"];
            _boneParam = _effect.Parameters["Bone"];
            _eyePositionParam = _effect.Parameters["EyePosition"];
            _diffuseColorParam = _effect.Parameters["DiffuseColor"];
            _emissiveColorParam = _effect.Parameters["EmissiveColor"];
            _ambientColorParam = _effect.Parameters["AmbientColor"];
            _specularColorParam = _effect.Parameters["SpecularColor"];
            _specularPowerParam = _effect.Parameters["SpecularPower"];
            _skyColorParam = _effect.Parameters["SkyColor"];
            _groundColorParam = _effect.Parameters["GroundColor"];

            _effect.Parameters["DirLight1Direction"].SetValue(DefaultLighting.Light1Direction);
            _effect.Parameters["DirLight2Direction"].SetValue(DefaultLighting.Light2Direction);
            _keyLightPositionParam = _effect.Parameters["KeyLightPosition"];
            _lightViewProjectionParam = _effect.Parameters["LightViewProjection"];
            _groundHeightParam = _effect.Parameters["GroundHeight"];

            _textureParam = _effect.Parameters["Texture"];
            _detailScaleParam = _effect.Parameters["DetailScale"];
            _detailStrengthParam = _effect.Parameters["DetailStrength"];
            _detailBoostParam = _effect.Parameters["DetailBoost"];
            _masonryStrengthParam = _effect.Parameters["MasonryStrength"];
            _normalMapParam = _effect.Parameters["NormalMapTexture"];
            _normalStrengthParam = _effect.Parameters["NormalStrength"];
            _surfaceReliefStrengthParam = _effect.Parameters["SurfaceReliefStrength"];
            _surfaceReliefFrequencyParam = _effect.Parameters["SurfaceReliefFrequency"];
            _surfaceStyleParam = _effect.Parameters["SurfaceStyle"];
            _patternPrimaryColorParam = _effect.Parameters["PatternPrimaryColor"];
            _patternSecondaryColorParam = _effect.Parameters["PatternSecondaryColor"];
            _patternGoreCountParam = _effect.Parameters["PatternGoreCount"];
            _patternGoreThresholdParam = _effect.Parameters["PatternGoreThreshold"];
            _patternCapExtentParam = _effect.Parameters["PatternCapExtent"];
            _patternReliefStrengthParam = _effect.Parameters["PatternReliefStrength"];
            _patternSheenStrengthParam = _effect.Parameters["PatternSheenStrength"];
            _mainTechnique = _effect.Techniques["InstancedModel"];
            _texturedTechnique = _effect.Techniques["InstancedModelTextured"];
            _triplanarTechnique = _effect.Techniques["InstancedModelTriplanar"];
            _detailUVTechnique = _effect.Techniques["InstancedModelDetailUV"];
            _detailUVNormalTechnique = _effect.Techniques["InstancedModelDetailUVNormal"];
            _patternTechnique = _effect.Techniques["InstancedModelPattern"];
            _depthTechnique = _effect.Techniques["InstancedDepth"];
            _effect.CurrentTechnique = _mainTechnique;

            SetLightTint(Vector3.One, Vector3.One);
        }

        /// <summary>
        /// Draws the given instances into the currently bound shadow map render target:
        /// depth only, from the light's point of view. One draw call per model mesh part.
        /// </summary>
        public void DrawDepth(Matrix lightViewProjection, ModelInstance[] instances, int instanceCount)
        {
            if (instanceCount <= 0) return;

            EnsureInstanceBufferCapacity(instances.Length);
            _instanceBuffer.SetData(instances, 0, instanceCount, SetDataOptions.Discard);

            _lightViewProjectionParam.SetValue(lightViewProjection);
            _effect.CurrentTechnique = _depthTechnique;

            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                _boneParam.SetValue(part.BoneTransform);

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                    new VertexBufferBinding(_instanceBuffer, 0, 1));
                _graphicsDevice.Indices = part.IndexBuffer;

                _effect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, instanceCount);
            }

            _effect.CurrentTechnique = _mainTechnique;
        }

        /// <summary>
        /// Tints the default three-light rig, e.g. by the sky dome palette: the key and fill lights
        /// (the "sun" side) by <paramref name="keyTint"/>, the back light by <paramref name="backTint"/>.
        /// White tints reproduce the untinted <see cref="BasicEffect"/> default lighting.
        /// </summary>
        public void SetLightTint(Vector3 keyTint, Vector3 backTint)
        {
            _effect.Parameters["DirLight0DiffuseColor"].SetValue(DefaultLighting.Light0Diffuse * keyTint);
            _effect.Parameters["DirLight0SpecularColor"].SetValue(DefaultLighting.Light0Specular * keyTint);
            _effect.Parameters["DirLight1DiffuseColor"].SetValue(DefaultLighting.Light1Diffuse * keyTint);
            _effect.Parameters["DirLight1SpecularColor"].SetValue(DefaultLighting.Light1Specular * keyTint);
            _effect.Parameters["DirLight2DiffuseColor"].SetValue(DefaultLighting.Light2Diffuse * backTint);
            _effect.Parameters["DirLight2SpecularColor"].SetValue(DefaultLighting.Light2Specular * backTint);
        }

        /// <summary>
        /// Draws the given instances of the model in one draw call per model mesh part.
        /// </summary>
        /// <param name="camera">A camera that looks at the resulting rendering.</param>
        /// <param name="instances">Per-instance data (world matrix + custom vector). Only the first <paramref name="instanceCount"/> entries are drawn.</param>
        /// <param name="instanceCount">Number of instances to draw.</param>
        /// <param name="effectParams">Lighting parameters shared by all the instances
        /// (<see cref="BasicEffectParams.AmbientLightColor"/>, specular and emissive colors are applied;
        /// zero vectors fall back to the <see cref="BasicEffect"/> defaults, like in <see cref="ModelRenderer"/>).</param>
        /// <param name="diffuseTint">Optional recolor of the model (e.g. the ball type color): the material
        /// diffuse colors are reduced to their luminance (keeping the patch pattern as shades) and multiplied
        /// by this tint, so the whole instance reads as one color. Null keeps the material colors unchanged.</param>
        public void Draw(ICamera camera, ModelInstance[] instances, int instanceCount, BasicEffectParams effectParams, Vector3? diffuseTint = null)
        {
            if (instanceCount <= 0) return;

            EnsureInstanceBufferCapacity(instances.Length);
            _instanceBuffer.SetData(instances, 0, instanceCount, SetDataOptions.Discard);

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _eyePositionParam.SetValue(camera.Position);

            Vector3 ambientLightColor = DefaultLighting.AmbientLightColor;
            bool overrideSpecular = false;
            Vector3 specularColor = DEFAULT_SPECULAR_COLOR;
            float specularPower = DEFAULT_SPECULAR_POWER;
            Vector3 emissiveColor = Vector3.Zero;

            if (effectParams != null)
            {
                if (effectParams.AmbientLightColor != Vector3.Zero) ambientLightColor = effectParams.AmbientLightColor;
                if (effectParams.SpecularColor != Vector3.Zero)
                {
                    overrideSpecular = true;
                    specularColor = effectParams.SpecularColor;
                    specularPower = effectParams.SpecularPower;
                }
                if (effectParams.EmmisiveColor != Vector3.Zero) emissiveColor = effectParams.EmmisiveColor;
            }

            _skyColorParam.SetValue(SkyColor);
            _groundColorParam.SetValue(GroundColor);
            _keyLightPositionParam.SetValue(KeyLightPosition);
            _groundHeightParam.SetValue(GroundHeight);

            //Set unconditionally rather than inside a technique branch: several techniques read these and
            //the effect is shared between every renderer, so a value left behind by the previous Draw
            //would show up as relief on a model that asked for none
            _surfaceReliefStrengthParam.SetValue(SurfaceReliefStrength);
            _surfaceReliefFrequencyParam.SetValue(SurfaceReliefFrequency);

            for (int i = 0; i < _parts.Length; i++)
            {
                ref MeshPartData part = ref _parts[i];

                _boneParam.SetValue(part.BoneTransform);

                //What this mesh is made of, so a timber door does not come out clad in stone courses
                SurfaceStyle style = part.MeshName != null && _meshStyles.TryGetValue(part.MeshName, out SurfaceStyle declared)
                    ? declared
                    : SurfaceStyle.Masonry;

                _surfaceStyleParam.SetValue((float)(int)style);

                Vector3 diffuse = new(part.DiffuseColor.X, part.DiffuseColor.Y, part.DiffuseColor.Z);

                //With the beach-ball pattern the tint colors the pattern instead of the material:
                //the material diffuse stays the neutral shade multiplying both pattern colors
                bool usePattern = PatternGoreCount > 0 && part.Texture == null && part.DiffuseColor.W >= 1f;

                if (diffuseTint.HasValue && !usePattern)
                {
                    //Luminance (Rec. 601) preserves the patch pattern as shades; the boost compensates
                    //for the brightest material being 0.8 instead of pure white
                    float luminance = diffuse.X * 0.299f + diffuse.Y * 0.587f + diffuse.Z * 0.114f;
                    diffuse = diffuseTint.Value * (luminance * 1.25f);
                }

                //BasicEffect premultiplies the non-specular terms by alpha on the CPU; do the same
                //so translucent mesh parts blend identically under BlendState.AlphaBlend
                float alpha = part.DiffuseColor.W;
                _diffuseColorParam.SetValue(new Vector4(diffuse * alpha, alpha));

                //The ambient tint is premultiplied by the material diffuse (like BasicEffect does on the CPU side);
                //the shader modulates it per pixel by the sky/ground hemisphere colors
                _ambientColorParam.SetValue(ambientLightColor * diffuse * alpha);
                _emissiveColorParam.SetValue((part.EmissiveColor + emissiveColor) * alpha);
                _specularColorParam.SetValue(overrideSpecular ? specularColor : part.SpecularColor);
                _specularPowerParam.SetValue(overrideSpecular ? specularPower : part.SpecularPower);

                //Mesh parts with their own texture sample it through UVs; parts of a UV-less model
                //can get a triplanar world-space detail texture instead (opaque parts only)
                if (part.Texture != null)
                {
                    _effect.CurrentTechnique = _texturedTechnique;
                    _textureParam.SetValue(part.Texture);
                }
                else if (usePattern)
                {
                    _effect.CurrentTechnique = _patternTechnique;
                    _patternPrimaryColorParam.SetValue(diffuseTint ?? Vector3.One);
                    _patternSecondaryColorParam.SetValue(PatternSecondaryColor);
                    _patternGoreCountParam.SetValue((float)PatternGoreCount);

                    //Thresholding sin(azimuth) at -cos(pi * width) hands the primary gore exactly that
                    //fraction of each pair of segments; the even split lands on zero, as before
                    _patternGoreThresholdParam.SetValue(-MathF.Cos(MathF.PI * PatternGoreWidth));
                    _patternCapExtentParam.SetValue(PatternCapExtent);
                    _patternReliefStrengthParam.SetValue(PatternReliefStrength);
                    _patternSheenStrengthParam.SetValue(PatternSheenStrength);
                }
                else if (DetailTexture != null && part.DiffuseColor.W >= 1f)
                {
                    bool useModelUVs = DetailTextureMapping == DetailMapping.ModelUVs;
                    bool useNormalMap = useModelUVs && DetailNormalMap != null;

                    _effect.CurrentTechnique = useNormalMap ? _detailUVNormalTechnique
                        : useModelUVs ? _detailUVTechnique
                        : _triplanarTechnique;

                    _textureParam.SetValue(DetailTexture);
                    _detailScaleParam.SetValue(DetailScale);
                    _detailStrengthParam.SetValue(DetailStrength);
                    _detailBoostParam.SetValue(DetailBoost);
                    _masonryStrengthParam.SetValue(MasonryStrength);

                    if (useNormalMap)
                    {
                        _normalMapParam.SetValue(DetailNormalMap);
                        _normalStrengthParam.SetValue(DetailNormalStrength);
                    }
                }
                else
                {
                    _effect.CurrentTechnique = _mainTechnique;
                }

                _graphicsDevice.SetVertexBuffers(
                    new VertexBufferBinding(part.VertexBuffer, part.VertexOffset, 0),
                    new VertexBufferBinding(_instanceBuffer, 0, 1));
                _graphicsDevice.Indices = part.IndexBuffer;

                _effect.CurrentTechnique.Passes[0].Apply();

                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, part.StartIndex, part.PrimitiveCount, instanceCount);
            }

            _effect.CurrentTechnique = _mainTechnique;
        }

        private readonly ModelInstance[] _singleInstance = new ModelInstance[1];

        /// <summary>
        /// Draws a single instance of the model at the given world matrix, with no ambient occlusion.
        /// Meant for unique scene objects (cannon, backdrops, ground), so they receive the same lighting
        /// (hemisphere sky ambient, positional key light, per-pixel shading) as the instanced balls.
        /// </summary>
        public void Draw(ICamera camera, Matrix world, BasicEffectParams effectParams)
        {
            _singleInstance[0] = new ModelInstance(world, new Vector4(0f, 0f, 0f, 1f));
            Draw(camera, _singleInstance, 1, effectParams);
        }

        private void EnsureInstanceBufferCapacity(int instanceCapacity)
        {
            if (_instanceBuffer != null && _instanceBuffer.VertexCount >= instanceCapacity) return;

            _instanceBuffer?.Dispose();
            _instanceBuffer = new DynamicVertexBuffer(_graphicsDevice, ModelInstance.VertexDeclaration, instanceCapacity, BufferUsage.WriteOnly);
        }

        public void Dispose()
        {
            _instanceBuffer?.Dispose();
            _instanceBuffer = null;
        }
    }
}
