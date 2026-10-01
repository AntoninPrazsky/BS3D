using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The level's crates as they are drawn (#257): each one a <see cref="CrateMesh"/> in four parts, one renderer a
    /// part, standing at the crate's centre. Built with the level and disposed with it; its renderers are lit with the
    /// rest of the scene through the host's sky-lit enrolment (<c>BS3DGame.SessionCrates</c>), which is what keeps a
    /// renderer made after the scene's lighting from drawing under the library's default rig.
    /// </summary>
    internal sealed class CrateField : IDisposable
    {
        //Warm boards, a darker frame, a near-black core between the boards, grey steel on the corners: the crate both
        //sets of design references drew
        private static readonly Vector3 BOARDS = new(0.66f, 0.43f, 0.22f);
        private static readonly Vector3 FRAME = new(0.36f, 0.21f, 0.10f);
        private static readonly Vector3 CORE = new(0.07f, 0.045f, 0.03f);
        private static readonly Vector3 STEEL = new(0.52f, 0.54f, 0.58f);

        private readonly List<CrateMesh> _meshes = new();
        private readonly List<InstancedModelRenderer> _renderers = new();
        private readonly List<Matrix> _worlds = new();

        public CrateField(GraphicsDevice device, Effect instancingEffect, Crates crates)
        {
            for (int i = 0; i < crates.Count; i++)
            {
                Crates.Crate crate = crates[i];
                Vector3 size = new(crate.HalfSize.X * 2f, crate.HalfSize.Y * 2f, crate.HalfSize.Z * 2f);
                Matrix world = Matrix.CreateTranslation(crate.Centre.X, crate.Centre.Y, crate.Centre.Z);

                Add(device, instancingEffect, size, CrateMesh.Part.Core, CORE, world);
                Add(device, instancingEffect, size, CrateMesh.Part.Boards, BOARDS, world);
                Add(device, instancingEffect, size, CrateMesh.Part.Frame, FRAME, world);
                Add(device, instancingEffect, size, CrateMesh.Part.Brackets, STEEL, world);
            }
        }

        private void Add(GraphicsDevice device, Effect effect, Vector3 size, CrateMesh.Part part, Vector3 colour, Matrix world)
        {
            CrateMesh mesh = new(device, size, part);
            _meshes.Add(mesh);
            _renderers.Add(new InstancedModelRenderer(device, mesh, colour, effect));
            _worlds.Add(world);
        }

        /// <summary>Every renderer, for the host's sky-lit enrolment.</summary>
        public IReadOnlyList<InstancedModelRenderer> Renderers => _renderers;

        /// <summary>The crates, opaque, with the rest of the session's solid objects.</summary>
        public void Draw(ICamera camera, BasicEffectParams effectParams)
        {
            for (int i = 0; i < _renderers.Count; i++) _renderers[i].Draw(camera, _worlds[i], effectParams);
        }

        public void Dispose()
        {
            foreach (InstancedModelRenderer renderer in _renderers) renderer.Dispose();
            foreach (CrateMesh mesh in _meshes) mesh.Dispose();
            _renderers.Clear();
            _meshes.Clear();
            _worlds.Clear();
        }
    }
}
