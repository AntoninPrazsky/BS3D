using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The four laser emitters on the drain's rim (#874): where the floor net comes from. Four of one mesh, in four parts
    /// (<see cref="LaserEmitterMesh"/>), each part one instanced draw of four, in the gun's own metal and glass
    /// (<see cref="CannonRig.STEEL_COLOR"/>, <see cref="CannonRig.FRAME_COLOR"/>, <see cref="CannonRig.GLASS_COLOR"/>) so
    /// they read as made by the same hand. Placed by <see cref="LaserEmitterRing"/>, per level, at the net's corners.
    /// <para>
    /// <b>The glow.</b> The lens always carries a faint ember (standby) and takes the net's own brightness when it is up
    /// (<see cref="LaserGrid.IntensityAt"/>), so the glass and the beam that leaves it are one light and pulse together.
    /// It is the renderer's <see cref="InstancedModelRenderer.EmissiveTint"/>, which the glass's alpha does not dim.
    /// </para>
    /// <para>
    /// <b>Two draws, two places in the frame.</b> The metal is opaque and goes with the gun and the level's crates
    /// (<see cref="DrawBody"/>); the lens is glass and goes with the translucents, after the drain's glass and
    /// <b>before</b> the net (<see cref="DrawLens"/>), writing no depth — the beam starts at the lens's crest, so a lens
    /// that wrote depth would hide the first hand's breadth of every feeder behind its own dome. Built and fitted with the
    /// level, lit with the rest of the scene through the host's sky-lit enrolment
    /// (<c>BS3DGame.SessionEmitters</c>).
    /// </para>
    /// </summary>
    internal sealed class LaserEmitters : IDisposable
    {
        //The ember in a lens that has no net to make: dim enough to sit under the glare threshold in any sky, there to say
        //"this is a lamp, and it is off" rather than to light anything. The armed glow is the net's own intensity times
        //ARMED_GLOW, which at the pulse's crest puts the glass far over the threshold, as the beams are
        private const float STANDBY_GLOW = 0.03f;
        private const float ARMED_GLOW = 0.5f;

        private readonly LaserEmitterMesh[] _meshes = new LaserEmitterMesh[4];
        private readonly InstancedModelRenderer _pedestal, _yoke, _housing, _lens;
        private readonly Vector3 _lensColour;

        //The four parts' worlds, rebuilt per level in Fit: the stand frame for the pedestal and the yoke, the housing's
        //for the housing and the lens
        private readonly ModelInstance[] _stands = new ModelInstance[LaserEmitterRing.COUNT];
        private readonly ModelInstance[] _housings = new ModelInstance[LaserEmitterRing.COUNT];

        private readonly InstancedModelRenderer[] _renderers;
        private bool _fitted;

        /// <param name="lensColour">The net's own red (the ceiling alarm's), in linear radiance: what the lens glows.</param>
        public LaserEmitters(GraphicsDevice device, Effect instancingEffect, Vector3 lensColour)
        {
            _lensColour = lensColour;

            _meshes[0] = new LaserEmitterMesh(device, LaserEmitterMesh.Part.Pedestal);
            _meshes[1] = new LaserEmitterMesh(device, LaserEmitterMesh.Part.Yoke);
            _meshes[2] = new LaserEmitterMesh(device, LaserEmitterMesh.Part.Housing);
            _meshes[3] = new LaserEmitterMesh(device, LaserEmitterMesh.Part.Lens);

            //The pedestal and the yoke are the carriage's iron, the housing the barrel's steel, the lens the window's glass
            _pedestal = new InstancedModelRenderer(device, _meshes[0], CannonRig.FRAME_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = CannonRig.FRAME_SPECULAR_AMBIENT
            };
            _yoke = new InstancedModelRenderer(device, _meshes[1], CannonRig.FRAME_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = CannonRig.FRAME_SPECULAR_AMBIENT
            };
            _housing = new InstancedModelRenderer(device, _meshes[2], CannonRig.STEEL_COLOR, instancingEffect)
            {
                SpecularAmbientStrength = CannonRig.SPECULAR_AMBIENT_STRENGTH
            };
            _lens = new InstancedModelRenderer(device, _meshes[3], CannonRig.GLASS_COLOR, instancingEffect, CannonRig.GLASS_ALPHA)
            {
                SpecularAmbientStrength = CannonRig.GLASS_SPECULAR_AMBIENT
            };

            _renderers = new[] { _pedestal, _yoke, _housing, _lens };
        }

        /// <summary>Every renderer, for the host's sky-lit enrolment.</summary>
        public IReadOnlyList<InstancedModelRenderer> Renderers => _renderers;

        /// <summary>
        /// Stands the four at this level's net corners (<see cref="LaserGrid.CornerX"/>, <see cref="LaserGrid.CornerZ"/>).
        /// Per level, never per frame: the matrices are kept.
        /// </summary>
        public void Fit(float cornerX, float cornerZ)
        {
            for (int k = 0; k < LaserEmitterRing.COUNT; k++)
            {
                _stands[k] = new ModelInstance(LaserEmitterRing.Stand(k, cornerX, cornerZ), new Vector4(0f, 0f, 0f, 1f));
                _housings[k] = new ModelInstance(LaserEmitterRing.Housing(k, cornerX, cornerZ), new Vector4(0f, 0f, 0f, 1f));
            }

            _fitted = true;
        }

        /// <summary>The emitters' metal, opaque, with the gun's: pedestals, yokes and housings, three instanced draws.</summary>
        public void DrawBody(ICamera camera, BasicEffectParams effectParams)
        {
            if (!_fitted) return;

            _pedestal.Draw(camera, _stands, _stands.Length, effectParams);
            _yoke.Draw(camera, _stands, _stands.Length, effectParams);
            _housing.Draw(camera, _housings, _housings.Length, effectParams);
        }

        /// <summary>
        /// The four lenses, translucent and writing no depth, glowing with <paramref name="netIntensity"/> over the standby
        /// ember (<see cref="LaserGrid.IntensityAt"/>). Puts back the depth state it found.
        /// </summary>
        public void DrawLens(GraphicsDevice device, ICamera camera, BasicEffectParams effectParams, float netIntensity)
        {
            if (!_fitted) return;

            _lens.EmissiveTint = _lensColour * (STANDBY_GLOW + ARMED_GLOW * netIntensity);

            DepthStencilState depth = device.DepthStencilState;
            device.DepthStencilState = DepthStencilState.DepthRead;

            _lens.Draw(camera, _housings, _housings.Length, effectParams);

            device.DepthStencilState = depth;
        }

        public void Dispose()
        {
            foreach (InstancedModelRenderer renderer in _renderers) renderer.Dispose();
            foreach (LaserEmitterMesh mesh in _meshes) mesh.Dispose();
        }
    }
}
