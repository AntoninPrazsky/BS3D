using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The outback's formations drawn as geometry of their own (#967): one polar disc mesh, instanced over every
    /// formation's footprint (<see cref="TerrainMirror.OutbackFootprints"/>) and displaced in <c>Outback.fx</c>'s
    /// <c>OutbackRock</c> by the whole height field, where the plain's camera grid and far ring stop on the same
    /// footprints. See "One height field, two kinds of mesh" in docs/scenes/outback.md.
    /// <para>
    /// <b>Why the formations left the camera grid.</b> A near-vertical wall needs several vertices across its fall, and
    /// the grid's 2.5-unit cells held a wall no narrower than 0.30 of a formation's radius; past the grid (some 500 units
    /// from the lens) the far ring's 432 cells round the arena are about fifteen pixels each at 1080p, so a far
    /// formation came out as a flat-topped trapezoid with 45-degree sides, the "hats" of #967. The disc puts its rings
    /// where the formation is, at every distance.
    /// </para>
    /// <para>
    /// <b>Detail by distance from the arena and by tier</b>, the split chosen once at build: a monolith near the play
    /// area takes the fine disc on High and Ultra (<see cref="SceneRenderer.FineMeshes"/>) and the coarse one below, a
    /// far one the coarse; the boulders take discs of their own. <b>Only the footprints in the view are drawn</b>: each
    /// frame they are tested against the camera's frustum and copied into a dynamic instance buffer (most of them stand
    /// behind or beside the lens, and a disc's vertex shader runs the whole height field). Each instance is the ellipse
    /// a hair larger than the footprint (the rim's chords circumscribe it, plus <see cref="RIM_PAD"/>), so the plain that
    /// stops on the exact ellipse never shows a crack.
    /// </para>
    /// </summary>
    internal sealed class OutbackRockDiscs : IDisposable
    {
        //Segments round and rings out: the fine monolith disc puts about a unit between rings across a loaf's wall and
        //1.6 between segments round it; the boulders are 7 units tall on a 12-unit footprint
        private const int MONOLITH_FINE_SEGMENTS = 160, MONOLITH_FINE_RINGS = 80;
        private const int MONOLITH_COARSE_SEGMENTS = 96, MONOLITH_COARSE_RINGS = 40;
        private const int OUTCROP_NEAR_SEGMENTS = 48, OUTCROP_NEAR_RINGS = 16;
        private const int OUTCROP_FAR_SEGMENTS = 24, OUTCROP_FAR_RINGS = 8;

        //Where the finer discs stop, from the arena: past it a formation is a few hundred pixels across at most
        private const float MONOLITH_NEAR_RADIUS = 650f;
        private const float OUTCROP_NEAR_RADIUS = 450f;

        /// <summary>How far out formations are drawn: the far ring's own reach (FarField.FAR_RING_OUTER) less a footprint,
        /// past which the land has faded into the sky. <c>Outback.fx</c>'s ROCK_FADE_END is this.</summary>
        public const float SEARCH_RADIUS = 1450f;

        //World units the disc reaches past its footprint, over the chords' circumscription
        private const float RIM_PAD = 0.5f;

        //What a footprint's culling sphere reaches above the plain: the tallest formation, a cluster's dome included
        private const float CULL_HEIGHT = 60f;

        private readonly GraphicsDevice _device;
        private readonly Mesh _monolithFine, _monolithCoarse, _outcropNear, _outcropFar;
        private readonly Group[] _groups;
        private readonly BoundingFrustum _frustum = new(Matrix.Identity);

        public OutbackRockDiscs(GraphicsDevice device, OutbackSceneConfig config)
        {
            _device = device;
            List<TerrainMirror.Footprint> footprints = TerrainMirror.OutbackFootprints(config, SEARCH_RADIUS);
            float plainY = config.Terrain.LevelY;

            _monolithFine = new Mesh(device, MONOLITH_FINE_SEGMENTS, MONOLITH_FINE_RINGS);
            _monolithCoarse = new Mesh(device, MONOLITH_COARSE_SEGMENTS, MONOLITH_COARSE_RINGS);
            _outcropNear = new Mesh(device, OUTCROP_NEAR_SEGMENTS, OUTCROP_NEAR_RINGS);
            _outcropFar = new Mesh(device, OUTCROP_FAR_SEGMENTS, OUTCROP_FAR_RINGS);

            //Near first: the depth test then rejects what the near formations hide
            _groups = new[]
            {
                new Group(device, footprints, plainY, f => !f.Outcrop && f.Centre.Length() <= MONOLITH_NEAR_RADIUS, _monolithFine, _monolithCoarse),
                new Group(device, footprints, plainY, f => f.Outcrop && f.Centre.Length() <= OUTCROP_NEAR_RADIUS, _outcropNear, _outcropNear),
                new Group(device, footprints, plainY, f => !f.Outcrop && f.Centre.Length() > MONOLITH_NEAR_RADIUS, _monolithCoarse, _monolithCoarse),
                new Group(device, footprints, plainY, f => f.Outcrop && f.Centre.Length() > OUTCROP_NEAR_RADIUS, _outcropFar, _outcropFar),
            };
        }

        /// <summary>
        /// Picks the footprints in <paramref name="viewProjection"/>'s frustum and uploads them, at the detail
        /// <paramref name="fine"/> asks for. Once a frame, before <see cref="Draw"/>; no allocation.
        /// </summary>
        public void Cull(Matrix viewProjection, bool fine)
        {
            _frustum.Matrix = viewProjection;
            foreach (Group group in _groups) group.Cull(_frustum, fine);
        }

        /// <summary>
        /// Draws what <see cref="Cull"/> picked through <paramref name="technique"/>, the effect's per-frame parameters
        /// already pushed by the terrain pass (whose states, opaque and two-sided, are current). Puts the effect's
        /// technique back.
        /// </summary>
        public void Draw(Effect effect, EffectTechnique technique)
        {
            EffectTechnique previous = effect.CurrentTechnique;
            effect.CurrentTechnique = technique;

            foreach (Group group in _groups)
            {
                if (group.Visible == 0) continue;

                InstanceBinding.Set(_device, group.Drawn.Vertices, 0, group.Instances, 0);
                _device.Indices = group.Drawn.Indices;
                technique.Passes[0].Apply();
                _device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, group.Drawn.Primitives, group.Visible);
            }

            effect.CurrentTechnique = previous;
        }

        public void Dispose()
        {
            foreach (Group group in _groups) group.Dispose();
            _monolithFine.Dispose();
            _monolithCoarse.Dispose();
            _outcropNear.Dispose();
            _outcropFar.Dispose();
        }

        /// <summary>The unit disc at one detail: a centre and rings of segments, evenly spaced out to 1.</summary>
        private sealed class Mesh : IDisposable
        {
            public readonly VertexBuffer Vertices;
            public readonly IndexBuffer Indices;
            public readonly int Primitives;

            /// <summary>1/cos(pi/segments): the rim's chords lie inside the circle through its vertices, and scaled out by
            /// this they circumscribe the footprint.</summary>
            public readonly float Circumscribe;

            public Mesh(GraphicsDevice device, int segments, int rings)
            {
                //Drawn two-sided like every terrain grid, so the winding does not matter: a height-field patch, open by nature
                var vertices = new Vector2[1 + rings * segments];
                for (int ring = 1; ring <= rings; ring++)
                {
                    float u = ring / (float)rings;
                    for (int s = 0; s < segments; s++)
                    {
                        float angle = MathHelper.TwoPi * s / segments;
                        vertices[1 + (ring - 1) * segments + s] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * u;
                    }
                }

                var indices = new List<short>(segments * 6 * rings);
                for (int s = 0; s < segments; s++)
                {
                    indices.Add(0);
                    indices.Add((short)(1 + s));
                    indices.Add((short)(1 + (s + 1) % segments));
                }

                for (int ring = 1; ring < rings; ring++)
                {
                    int inner = 1 + (ring - 1) * segments, outer = 1 + ring * segments;
                    for (int s = 0; s < segments; s++)
                    {
                        int next = (s + 1) % segments;
                        short a = (short)(inner + s), b = (short)(inner + next), c = (short)(outer + s), d = (short)(outer + next);
                        indices.Add(a); indices.Add(c); indices.Add(b);
                        indices.Add(b); indices.Add(c); indices.Add(d);
                    }
                }

                Primitives = indices.Count / 3;
                Circumscribe = 1f / MathF.Cos(MathF.PI / segments);
                Vertices = new VertexBuffer(device, new VertexDeclaration(
                    new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0)), vertices.Length, BufferUsage.WriteOnly);
                Vertices.SetData(vertices);
                Indices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Count, BufferUsage.WriteOnly);
                Indices.SetData(indices.ToArray());
            }

            public void Dispose()
            {
                Vertices.Dispose();
                Indices.Dispose();
            }
        }

        /// <summary>The footprints that take one pair of meshes (High's and below), and the frame's visible share of them.</summary>
        private sealed class Group : IDisposable
        {
            private readonly TerrainMirror.Footprint[] _footprints;
            private readonly BoundingSphere[] _bounds;
            private readonly RockInstance[] _visible;
            private readonly Mesh _fine, _coarse;

            public readonly DynamicVertexBuffer Instances;
            public Mesh Drawn;
            public int Visible;

            public Group(GraphicsDevice device, List<TerrainMirror.Footprint> footprints, float plainY,
                Func<TerrainMirror.Footprint, bool> takes, Mesh fine, Mesh coarse)
            {
                _footprints = footprints.FindAll(f => takes(f)).ToArray();
                _bounds = new BoundingSphere[_footprints.Length];
                for (int i = 0; i < _footprints.Length; i++)
                {
                    TerrainMirror.Footprint f = _footprints[i];
                    float reach = f.SemiLong + RIM_PAD;
                    _bounds[i] = new BoundingSphere(new Vector3(f.Centre.X, plainY + CULL_HEIGHT * 0.5f, f.Centre.Y),
                        MathF.Sqrt(reach * reach + CULL_HEIGHT * CULL_HEIGHT * 0.25f) * fine.Circumscribe);
                }

                _visible = new RockInstance[Math.Max(_footprints.Length, 1)];
                _fine = fine;
                _coarse = coarse;
                Drawn = fine;
                Instances = new DynamicVertexBuffer(device, RockInstance.Declaration, _visible.Length, BufferUsage.WriteOnly);
            }

            public void Cull(BoundingFrustum frustum, bool fine)
            {
                Drawn = fine ? _fine : _coarse;
                float scale = Drawn.Circumscribe;
                Visible = 0;

                for (int i = 0; i < _footprints.Length; i++)
                {
                    if (frustum.Contains(_bounds[i]) == ContainmentType.Disjoint) continue;

                    TerrainMirror.Footprint f = _footprints[i];
                    _visible[Visible++] = new RockInstance(
                        new Vector4(f.Centre.X, f.Centre.Y, f.Axis.X, f.Axis.Y),
                        new Vector4(f.SemiLong * scale + RIM_PAD, f.SemiShort * scale + RIM_PAD, f.Outcrop ? 1f : 0f, 0f));
                }

                if (Visible > 0) Instances.SetData(_visible, 0, Visible, SetDataOptions.Discard);
            }

            public void Dispose() => Instances.Dispose();
        }

        /// <summary>A footprint: its centre and long axis in world XZ, then its two semi-axes and whether it is a boulder.</summary>
        private readonly struct RockInstance : IVertexType
        {
            public readonly Vector4 Placement;
            public readonly Vector4 Size;

            public RockInstance(Vector4 placement, Vector4 size)
            {
                Placement = placement;
                Size = size;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }
    }
}
