using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest's lamps as the hosts draw them (#1166): the poles, their ironwork, the shades' linings and the bulbs of
    /// <see cref="ForestLampLayout"/>, through the one shared instanced effect, as the wood they stand beside is drawn
    /// (<see cref="ForestScatterRenderer"/>) and with its matte dressing, so the scene's sun, its dome, the sun's map and the
    /// lamps' own lights (<see cref="SceneLights"/>) light them as they light the trees. The light itself is not this class's:
    /// <see cref="SceneLights"/> pushes it onto the shared effect, and the backdrop onto the floor's and the plants' own.
    /// <para>
    /// <b>The caller keeps the scene gate and the place in the frame</b>, as with the wood and the fireflies: each host draws
    /// this after the wood when the scene is the forest, casts it into the sun's map with the wood, and enrols
    /// <see cref="Renderers"/> in its sky-lit list.
    /// </para>
    /// <para>
    /// <b>The bulb is black and shows only as its glow</b>, as a firefly does (<see cref="ForestFireflies"/>): an emissive tint
    /// carried far over the glare threshold, so the bloom makes its halo, at the lamp's own
    /// <see cref="ForestLampLayout.Brightness"/> on the scene's clock, so the old lamp's glass stutters with its light.
    /// </para>
    /// </summary>
    public sealed class ForestLamps : IDisposable
    {
        //The materials (linear): a pole of old weathered spruce gone grey, dark green enamel on the ironwork's paint, the
        //lining's white enamel, a little warmer than white with age
        private static readonly Vector3 WOOD = new(0.085f, 0.072f, 0.058f);
        private static readonly Vector3 IRON = new(0.030f, 0.045f, 0.036f);
        private static readonly Vector3 ENAMEL = new(0.62f, 0.58f, 0.50f);

        //The material diffuse the meshes are built with: a tinted draw reduces it to luminance times 1.25, which at 0.8 lands
        //the tint as passed (ForestScatterRenderer's SCATTER_MATERIAL_DIFFUSE, the same argument)
        private static readonly Vector3 MATERIAL_DIFFUSE = Vector3.One * 0.8f;

        //The wood's tiling of the bark texture, a pole's grain running up it (the wood's TRUNK_BARK_SPAN)
        private const float WOOD_SPAN = 2.2f;

        private readonly UploadedMesh[] _meshes;
        private readonly InstancedModelRenderer _wood, _iron, _lining, _bulb;
        private readonly SurfaceTexture _bark;
        private readonly BasicEffectParams _woodParams, _ironParams, _liningParams, _bulbParams;
        private readonly Vector3 _woodTint, _ironTint, _liningTint;
        private readonly ModelInstance[] _instances;
        private readonly ModelInstance[] _one = new ModelInstance[1];
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        /// <summary>Whether the lamps are drawn and cast (the Testbed's <c>forest=</c> dial, <see cref="ForestLayer.Lamps"/>);
        /// on for every other caller.</summary>
        public bool Shown { get; set; } = true;

        /// <param name="device">The device the meshes and their instance buffers live on.</param>
        /// <param name="instancingEffect">The shared instancing effect, as every scene fixture draws through.</param>
        /// <param name="layout">Where the lamps stand: the backdrop's (<c>SceneRenderer.ForestLamps</c>).</param>
        /// <param name="sceneAmbientIntensity">The host's scene ambient, as it hands <see cref="ForestScatterRenderer"/>.</param>
        public ForestLamps(GraphicsDevice device, Effect instancingEffect, ForestLampLayout layout, float sceneAmbientIntensity)
        {
            _meshes = new[]
            {
                Upload(device, ForestLampMeshes.Wood()), Upload(device, ForestLampMeshes.Iron()),
                Upload(device, ForestLampMeshes.Lining()), Upload(device, ForestLampMeshes.Bulb()),
            };

            _bark = SurfaceTexture.Bark(device);
            _wood = new InstancedModelRenderer(device, _meshes[0], MATERIAL_DIFFUSE, instancingEffect)
            {
                DetailTexture = _bark.Texture,
                DetailScale = 1f / WOOD_SPAN,
                DetailBoost = 1f / _bark.LinearMean,
                DetailStrength = 0.6f,
                SurfaceReliefFrequency = 9f,
                SurfaceReliefStrength = 0.012f,
                CavityStrength = 0.5f,
                SpecularAmbientStrength = 0.06f,
            };
            _iron = new InstancedModelRenderer(device, _meshes[1], MATERIAL_DIFFUSE, instancingEffect) { SpecularAmbientStrength = 0.35f };
            _lining = new InstancedModelRenderer(device, _meshes[2], MATERIAL_DIFFUSE, instancingEffect) { SpecularAmbientStrength = 0.3f };
            _bulb = new InstancedModelRenderer(device, _meshes[3], Vector3.Zero, instancingEffect);
            Renderers = new[] { _wood, _iron, _lining };

            Vector3 ambient = Vector3.One * sceneAmbientIntensity;
            _woodParams = new BasicEffectParams(ambient, new Vector3(0.05f, 0.045f, 0.04f), 10f, Vector3.Zero);
            //Enamel is glossy: a tight highlight the bulb and the sky put on the shade's back
            _ironParams = new BasicEffectParams(ambient, new Vector3(0.25f), 28f, Vector3.Zero);
            _liningParams = new BasicEffectParams(ambient, new Vector3(0.3f), 24f, Vector3.Zero);
            //The bulb's black body (a ZERO specular means "no override", the renderer's white one, so a dim one)
            _bulbParams = new BasicEffectParams(Vector3.Zero, new Vector3(0.004f), 8f, Vector3.Zero);

            _woodTint = ColorSpace.LinearToSrgb(WOOD);
            _ironTint = ColorSpace.LinearToSrgb(IRON);
            _liningTint = ColorSpace.LinearToSrgb(ENAMEL);

            _instances = new ModelInstance[layout.Lamps.Count];
            for (int i = 0; i < _instances.Length; i++) _instances[i] = new ModelInstance(layout.Lamps[i].World, NO_OCCLUSION);
        }

        private static UploadedMesh Upload(GraphicsDevice device, MeshBuilder builder)
        {
            (List<VertexPositionNormalTexture> vertices, List<int> indices) = builder.Geometry;
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            foreach (VertexPositionNormalTexture v in vertices)
            {
                min = Vector3.Min(min, v.Position);
                max = Vector3.Max(max, v.Position);
            }
            Vector3 centre = (min + max) * 0.5f;
            return new UploadedMesh(device, vertices, indices, new BoundingSphere(centre, (max - centre).Length()));
        }

        /// <summary>The renderers a host enrols in its sky-lit list: the pole, the ironwork and the lining (the bulb is black
        /// and lit by nothing). An array, as <see cref="ForestScatterRenderer.Renderers"/> is: a host that refills its list every
        /// frame walks it without boxing an enumerator.</summary>
        public InstancedModelRenderer[] Renderers { get; }

        /// <summary>
        /// Draws the lamps, the bulbs at <paramref name="wallClock"/>'s brightness. Allocates nothing; touches no GPU state
        /// beyond the renderers' own draws.
        /// </summary>
        /// <param name="wallClock">The clock <see cref="SceneLights"/> is handed, so the old lamp's glass and its light stutter
        /// together.</param>
        public void Draw(ICamera camera, float wallClock)
        {
            if (!Shown) return;

            int count = _instances.Length;
            _wood.Draw(camera, _instances, count, _woodParams, _woodTint);
            _iron.Draw(camera, _instances, count, _ironParams, _ironTint);
            _lining.Draw(camera, _instances, count, _liningParams, _liningTint);

            //Each bulb at its own brightness: one draw a lamp
            for (int i = 0; i < count; i++)
            {
                _bulb.EmissiveTint = ForestLampLayout.LightColour(i, wallClock) * (ForestLampLayout.GLOW / ForestLampLayout.COLOUR.Y);
                _one[0] = _instances[i];
                _bulb.Draw(camera, _one, 1, _bulbParams);
            }
        }

        /// <summary>Casts the poles and their ironwork into the sun's map (the bulbs and linings sit under the shades).</summary>
        public void DrawShadow(Matrix shadowViewProjection)
        {
            if (!Shown) return;
            _wood.DrawDepth(shadowViewProjection, _instances, _instances.Length);
            _iron.DrawDepth(shadowViewProjection, _instances, _instances.Length);
        }

        public void Dispose()
        {
            _wood?.Dispose();
            _iron?.Dispose();
            _lining?.Dispose();
            _bulb?.Dispose();
            _bark?.Dispose();
            foreach (UploadedMesh mesh in _meshes) mesh?.Dispose();
        }
    }
}
