using Microsoft.Xna.Framework.Graphics;
using System;

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
        /// The quality tier crossed <see cref="SceneRenderer.SceneDetail"/>'s line: pick the reduced or the
        /// authored program. Called only on a change, never from the constructor — which is the old
        /// <c>SelectDetailTechniques</c>' own timing, and the reason a scene draws with its effect's default
        /// technique until a host first sets the detail.
        /// </summary>
        public virtual void OnDetailChanged(float sceneDetail) { }

        /// <summary>Frees what this backdrop built. Effects are the content manager's and are not disposed.</summary>
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
        /// Where BuildQuadIndexBuffer refuses (#589): the largest quad count whose four vertices a quad stay
        /// addressable by a 16-bit index, one short of 65 536 / 4 so the last index is never 0xFFFF.
        /// </summary>
        public const int MAX_BILLBOARD_QUADS = 16383;

        /// <summary>The services over a device, the quad the renderer built on it and its seed offset.</summary>
        public BackdropServices(GraphicsDevice graphicsDevice, VertexBuffer fullScreenQuad, int seedOffset)
        {
            GraphicsDevice = graphicsDevice;
            FullScreenQuad = fullScreenQuad;
            SeedOffset = seedOffset;
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
    }
}
