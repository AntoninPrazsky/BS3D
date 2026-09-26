using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One scene's own share of <see cref="SceneRenderer"/> (#580): its config, its effects and buffers, how
    /// it pushes the one to the other, how it draws, and the questions about it that only it can answer. The
    /// renderer keeps one per migrated <see cref="SceneKind"/> in an array indexed by the kind and dispatches to
    /// it; the scenes not yet migrated still answer through the renderer's own switch arms.
    /// <para>
    /// <b>The hooks are the ones the migrated scenes need and no more.</b> The issue's full list (the shadow
    /// receivers, fit and casters, the scene event, the ground glow) arrives with the first scene that has
    /// one to answer; an abstract hook nothing overrides is a promise nobody has tested.
    /// </para>
    /// <para>
    /// A backdrop is built eagerly by the renderer's constructor, in the constructor's own order, exactly as
    /// the code it came out of was — which is what keeps a migration pixel-identical. Every default here is
    /// the answer the renderer's old <c>default:</c> arm gave, so a hook a scene does not override behaves as
    /// that scene always did.
    /// </para>
    /// </summary>
    internal abstract class Backdrop : IDisposable
    {
        /// <summary>The device and the resources several scenes share, owned by the renderer.</summary>
        protected readonly BackdropServices Services;

        /// <summary>Takes the renderer's shared services; a derived constructor loads and pushes the rest.</summary>
        protected Backdrop(BackdropServices services)
        {
            Services = services;
        }

        /// <summary>Which scene this is.</summary>
        public abstract SceneKind Kind { get; }

        /// <summary>The scene's configuration — what <see cref="SceneRenderer.GetSceneConfig"/> answers.</summary>
        public abstract SceneConfig Config { get; }

        /// <summary>The scene's opaque environment pass (<see cref="SceneRenderer.DrawEnvironment"/>).</summary>
        public abstract void Draw(in SceneFrame frame);

        /// <summary>The foreground weather drawn after the cluster (<see cref="SceneRenderer.DrawOverlays"/>);
        /// nothing by default.</summary>
        public virtual void DrawOverlays(in SceneFrame frame) { }

        /// <summary>
        /// Whether a supersampled frame shades this backdrop at the back buffer's size and scales it up
        /// (<see cref="SceneRenderer"/>'s <c>DrawBackdropAtDisplayResolution</c>). False by default: only a
        /// pure full-screen fill whose look does not depend on the sample count may.
        /// </summary>
        public virtual bool DrawsAtDisplayResolution => false;

        /// <summary>The scene's own light rig (<see cref="SceneRenderer.TryGetLightRig"/>); false takes the dome's.</summary>
        public virtual bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            rig = default;
            return false;
        }

        /// <summary>
        /// The sun the scene states for itself (<see cref="SceneRenderer.TryGetSunDirection"/>) over the dome's
        /// and the shared domeless one; false takes one of those. Only the Moon states one.
        /// </summary>
        public virtual bool TryGetSunDirection(out Vector3 direction)
        {
            direction = default;
            return false;
        }

        /// <summary>The scene's establishing viewpoint (<see cref="SceneRenderer.TryGetViewpoint"/>).</summary>
        public virtual bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            viewpoint = default;
            return false;
        }

        /// <summary>
        /// The event the scene stages on its own clock at <paramref name="time"/>
        /// (<see cref="SceneRenderer.TryGetSceneEvent"/>) — the storm's strike; false for a scene that stages
        /// nothing.
        /// </summary>
        public virtual bool TryGetSceneEvent(float time, out SceneEvent staged)
        {
            staged = default;
            return false;
        }

        /// <summary>
        /// A light on the ground that lights the underside of the cloud deck at <paramref name="time"/>
        /// (<see cref="SceneRenderer.TryGetGroundGlow"/>) — the volcano's crater; false, with a range of 1, for a
        /// scene with nothing burning under its sky.
        /// </summary>
        public virtual bool TryGetGroundGlow(float time, out Vector3 position, out Vector3 color, out float range)
        {
            position = default;
            color = default;
            range = 1f;
            return false;
        }

        /// <summary>
        /// The scene's effects that read the sun's shadow map (they include <c>Shadows.fxh</c>), gathered once at load
        /// into the renderer's one receiver list (its <c>RegisterShadowReceivers</c>) — none by default. A scene that
        /// states receivers states its <see cref="TryShadowFit"/> too: fitted but not receiving casts into a map
        /// nobody reads, receiving but not fitted is handed 0 every frame.
        /// </summary>
        public virtual IEnumerable<Effect> ShadowReceivers => Array.Empty<Effect>();

        /// <summary>
        /// Where the scene's ground sits and how far the sun shadow map's box has to reach below and above it (the
        /// renderer's <c>TryShadowFit</c>, which centres the box on the camera and adds the island's headroom and
        /// the margin); false for a scene that takes no map, which is the default.
        /// </summary>
        public virtual bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = below = above = 0f;
            return false;
        }

        /// <summary>
        /// The scene's terrain effect and its CPU mirror (<see cref="SceneRenderer.TryGetTerrainProbe"/>, the
        /// Testbed's <c>mirrorcheck</c>, #590); false for a scene with no mirror. Nothing in a frame asks it.
        /// </summary>
        public virtual bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = null;
            mirror = null;
            return false;
        }

        /// <summary>
        /// The quality tier crossed <see cref="SceneRenderer.SceneDetail"/>'s line: pick the reduced or the
        /// authored program. Called only on a change, never from the constructor — which is the old
        /// <c>SelectDetailTechniques</c>' own timing, and the reason a scene draws with its effect's default
        /// technique until a host first sets the detail.
        /// </summary>
        public virtual void OnDetailChanged(float sceneDetail) { }

        /// <summary>
        /// Frees what this backdrop built. An effect loaded from the content manager is the manager's and is not
        /// disposed; a clone of one (<c>Effect.Clone</c>, the aurora's snow) is the backdrop's own and is.
        /// </summary>
        public virtual void Dispose() { }
    }

    /// <summary>
    /// What <see cref="SceneRenderer"/> shares with every <see cref="Backdrop"/>: the device, the full-screen
    /// quad the sky-replacing passes draw over, and the caller's supersampling factor.
    /// </summary>
    internal sealed class BackdropServices
    {
        /// <summary>The device every scene draws on.</summary>
        public GraphicsDevice GraphicsDevice { get; }

        /// <summary>
        /// Four corners already in normalized device coordinates, drawn as a triangle strip — nothing
        /// transforms it. Space built it; the dream, the cavern, the Moon's and the aurora's skies, Mars's moons
        /// and the Grid's void all draw over the same one, because a corner quad has nothing scene-specific
        /// about it. Owned (and disposed) by the renderer.
        /// </summary>
        public VertexBuffer FullScreenQuad { get; }

        /// <summary>The caller's supersampling factor — <see cref="SceneRenderer.SupersampleFactor"/>, which forwards here.</summary>
        public int SupersampleFactor { get; set; } = 1;

        /// <summary>
        /// The renderer's seed offset (its constructor's <c>seedOffset</c>): what every seeded arrangement adds to its
        /// own constant, 0 being the arrangement that shipped.
        /// </summary>
        public int SeedOffset { get; }

        /// <summary>
        /// The falling snow the mountain and the aurora share (#205, a service since #580): its flake buffer and
        /// its draw. Set by the renderer where the snow stood in its constructor, before any backdrop that snows
        /// is built; the renderer disposes it.
        /// </summary>
        public Snowfall Snowfall { get; set; }

        /// <summary>
        /// The flock the savanna, the desert, the outback and the beach share (#235, a service since #580). Set by
        /// the renderer where the birds stood in its constructor, before any backdrop that draws it is built; the
        /// renderer disposes it.
        /// </summary>
        public BirdFlock Birds { get; set; }

        /// <summary>
        /// The radius cut out of every terrain around the arena — <see cref="SceneRenderer.TerrainHoleRadius"/>, which
        /// forwards here. Written by the host at any time, so a backdrop reads it at draw time.
        /// </summary>
        public float TerrainHoleRadius { get; set; }

        //Every terrain grid, one per distinct (vertices a side, extent); the renderer's, which disposes it.
        private readonly TerrainGridCache _gridCache;

        /// <summary>
        /// Where BuildQuadIndexBuffer refuses (#589): the largest quad count whose four vertices a quad stay
        /// addressable by a 16-bit index, one short of 65 536 / 4 so the last index is never 0xFFFF.
        /// </summary>
        public const int MAX_BILLBOARD_QUADS = 16383;

        /// <summary>
        /// The land past every open-ground scene's own grid and the fade into the sky (#551), a service since #580.
        /// Owned (and disposed) by the renderer.
        /// </summary>
        public FarField FarField { get; }

        /// <summary>
        /// The services over a device, the quad, the grid cache and the far field the renderer built on it, and its
        /// seed offset.
        /// </summary>
        public BackdropServices(GraphicsDevice graphicsDevice, VertexBuffer fullScreenQuad, int seedOffset, TerrainGridCache gridCache,
            FarField farField)
        {
            GraphicsDevice = graphicsDevice;
            FullScreenQuad = fullScreenQuad;
            SeedOffset = seedOffset;
            _gridCache = gridCache;
            FarField = farField;
        }

        /// <summary>
        /// The one builder of the 16-bit index buffers every billboard in the scenes is drawn through (#589) — the storm's
        /// cloud puffs and bolts, the snow, the spray, the volcano's fountains and ash, the campfire sparks and the
        /// flame: two triangles a quad over four vertices a quad, in the winding the billboard shaders here already
        /// expect. <paramref name="mirrored"/> is the flame's order, whose quads stand on their base (corner y 0..1)
        /// rather than about their middle, so both of its triangles are listed the other way round.
        /// <para>
        /// ⚠ <b>It refuses a count past <see cref="MAX_BILLBOARD_QUADS"/></b>, where a 16-bit index would wrap and
        /// the later quads would silently draw the first ones' vertices — the failure the terrain grids' own note
        /// (<see cref="TerrainGridCache"/>) records a long hunt for. Five copies of this loop stood here until #589
        /// and only the volcano's counts were clamped; the snow and the spray trusted their configs.
        /// </para>
        /// </summary>
        public IndexBuffer BuildQuadIndexBuffer(int quads, bool mirrored = false)
        {
            CheckBillboardQuads(quads);

            short[] indices = new short[quads * 6];
            for (int i = 0; i < quads; i++)
            {
                int v = i * 4;
                int o = i * 6;
                if (mirrored)
                {
                    indices[o] = (short)v; indices[o + 1] = (short)(v + 1); indices[o + 2] = (short)(v + 2);
                    indices[o + 3] = (short)(v + 2); indices[o + 4] = (short)(v + 1); indices[o + 5] = (short)(v + 3);
                }
                else
                {
                    indices[o] = (short)v; indices[o + 1] = (short)(v + 2); indices[o + 2] = (short)(v + 1);
                    indices[o + 3] = (short)(v + 1); indices[o + 4] = (short)(v + 2); indices[o + 5] = (short)(v + 3);
                }
            }

            IndexBuffer buffer = new(GraphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            buffer.SetData(indices);

            return buffer;
        }

        /// <summary>Throws when <paramref name="quads"/> would overflow a 16-bit index; see <see cref="BuildQuadIndexBuffer"/>.</summary>
        public static void CheckBillboardQuads(int quads)
        {
            if (quads > MAX_BILLBOARD_QUADS)
                throw new ArgumentOutOfRangeException(nameof(quads), quads,
                    $"A billboard buffer holds at most {MAX_BILLBOARD_QUADS} quads: its indices are 16-bit, and past that they wrap.");
        }

        /// <summary>
        /// The flat lattice grid of <paramref name="n"/> vertices a side over <paramref name="extent"/> that a
        /// terrain scene displaces, from <see cref="TerrainGridCache"/> (#589): scenes asking for the same pair
        /// share one pair of buffers, which the cache owns — a holder gives its grid back with
        /// <see cref="TerrainGridCache.Release"/> and never disposes it. The indices are 32-bit; the cache's
        /// builder carries the note on why a grid over 255 a side must never have 16-bit ones.
        /// </summary>
        public void AcquireGridMesh(int n, float extent, out VertexBuffer vertexBuffer, out IndexBuffer indexBuffer, out int indexCount)
        {
            TerrainGridCache.Grid grid = _gridCache.Acquire(n, extent);
            vertexBuffer = grid.Vertices;
            indexBuffer = grid.Indices;
            indexCount = grid.IndexCount;
        }

        /// <summary>
        /// Gives back one reference to the grid <see cref="AcquireGridMesh"/> handed out for the pair
        /// (<see cref="TerrainGridCache.Release"/>): what a scene that retakes its grid at another density does with
        /// the old one, rather than disposing a pair of buffers another scene may be drawing.
        /// </summary>
        public void ReleaseGridMesh(int n, float extent) => _gridCache.Release(n, extent);

        /// <summary>
        /// A static buffer of <paramref name="count"/> camera-facing quads, each carrying a fixed random point
        /// in the unit cube and one more random — everything a shader needs to animate a particle entirely in
        /// its vertex shader. The volcano's fountains, its plume and its ash, the campfire sparks, the mountain's
        /// snow and the sea's spray are all built from this (the last two were copies of it until #589, and their
        /// seeds make the same sequences through it). Refuses a count past <see cref="MAX_BILLBOARD_QUADS"/>.
        /// </summary>
        public void BuildBillboardParticles(int count, int seed, ref VertexBuffer vertexBuffer, ref IndexBuffer indexBuffer)
        {
            CheckBillboardQuads(count);

            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
            vertexBuffer = null;
            indexBuffer = null;

            if (count <= 0) return;

            SceneRenderer.BillboardVertex[] vertices = new SceneRenderer.BillboardVertex[count * 4];
            Random rng = new(seed);
            for (int i = 0; i < count; i++)
            {
                Vector3 basePosition = new((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                float rand = (float)rng.NextDouble();
                int v = i * 4;
                vertices[v] = new SceneRenderer.BillboardVertex(basePosition, new Vector3(-1f, 1f, rand));
                vertices[v + 1] = new SceneRenderer.BillboardVertex(basePosition, new Vector3(1f, 1f, rand));
                vertices[v + 2] = new SceneRenderer.BillboardVertex(basePosition, new Vector3(-1f, -1f, rand));
                vertices[v + 3] = new SceneRenderer.BillboardVertex(basePosition, new Vector3(1f, -1f, rand));
            }
            vertexBuffer = new VertexBuffer(GraphicsDevice, SceneRenderer.BillboardVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            indexBuffer = BuildQuadIndexBuffer(count);
        }
    }
}
