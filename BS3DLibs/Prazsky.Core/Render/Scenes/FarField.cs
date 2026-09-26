using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The far field (#551): the one ring of land every open-ground scene draws past its own camera grid, and the
    /// fade that makes wherever the land ends invisible. Moved out of <see cref="SceneRenderer"/> as a service in
    /// #580 (<see cref="BackdropServices.FarField"/>), because the backdrops that draw a terrain all need it and
    /// none of them owns it. The shader half, and the whole of the why, is <c>FarField.fxh</c>; see
    /// "The far field" in docs/scenes.md.
    /// </summary>
    internal sealed class FarField : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;

        //The land past each open-ground scene's own grid, and the fade that makes wherever it ends invisible. The
        //shader half, and the whole of the why, is FarField.fxh; this is the mesh and the per-draw plumbing.
        //
        //Until #551 every camera clipped at 500 and the grids reached 500-800 from the camera, so the backdrops were
        //cut off twice over: by the far plane where the haze had not finished (the volcano's plain, the icesheet, the
        //sea's horizon), and by the grid's own edge where it had (Mars's mesas and the desert's dunes, standing as
        //flat haze-coloured cards against a sky of another colour). Enlarging the grids was the obvious fix and the
        //wrong one: a camera grid is fine everywhere, so reaching 1500 at today's cells is about nine times the
        //vertices, and the far land does not need the near land's density - it needs a constant ANGLE per cell.

        //The ring reaches from inside every camera grid to past the point the fade completes. The inner radius has to
        //sit inside the grid on every side for any camera the Game uses, and the grid is centred on the CAMERA while
        //the ring is centred on the ARENA: the smallest grid (+-500) still covers 340 units round the arena with the
        //lens 110 off it on a diagonal, and the Game's cameras stand within about 90 (the play pose 36 out, the
        //chapter intro's furthest viewpoint 2.4 stand-offs). 200 leaves room for the Testbed's free camera to wander
        //three hundred units out before a gap can open on the arena's far side. A camera further out than that can
        //see one - the ring is the arena's, by design, because a ring that followed the camera would swim.
        private const float FAR_RING_INNER = 200f;
        private const float FAR_RING_OUTER = 1500f;

        //Around: 432 cells, 0.83 degrees each seen from the arena. Out: each ring twice as far from the last as a
        //cell is wide, because a far ground is seen edge-on and its depth spacing is foreshortened to nothing -
        //the silhouettes are made by the ANGULAR spacing, and that is what the count buys.
        private const int FAR_RING_SEGMENTS = 432;
        private const float FAR_RING_RADIAL_ASPECT = 2f;

        //How far inside the camera grid's edge the ring takes over: a strip where both surfaces are drawn, so the
        //two different tessellations of the same ground can never open a crack of sky between them.
        private const float FAR_RING_OVERLAP = 6f;

        /// <summary>Where the far ground has become the sky behind it (FarField.fxh's FarFadeToSky). Past the ring's
        /// inner edge everywhere and short of its outer edge, so nothing about where the ring ends can show.</summary>
        public const float FAR_FADE_END = 1300f;

        //Where the fade may begin at the latest. A scene's own haze distance is where it begins otherwise (see
        //FarFieldSlots), but three scenes state 700-900 and their haze ends in the rig's HorizonColor, which is not
        //the sky's: begun that late, the last few hundred units - all of them squeezed into the pixel or two at the
        //skyline - stayed that colour and drew a thin line of it along the whole horizon (the icesheet's was teal
        //under a lilac sky).
        private const float FAR_FADE_START_LATEST = 600f;

        private VertexBuffer _farRingVertexBuffer;
        private IndexBuffer _farRingIndexBuffer;
        private int _farRingRows;
        private float _farRingGrowth;

        //Per effect, cached on its first draw (BestPractices §1: the by-name indexer is a linear scan, and every
        //open-ground draw sets these every frame). The haze distance is read BACK from the effect: it is where each
        //scene has already said its own ground has become air, which is where the last step to the sky begins.
        private sealed class FarFieldSlots
        {
            public EffectParameter Origin, Ring, Fade, Sky, Haze;
        }

        private readonly Dictionary<Effect, FarFieldSlots> _farFieldSlots = new();

        //The technique the ring draws with, per technique the camera grid drew with: the scene's REDUCED program
        //where it has one ("<name>Reduced" - Mars, the mountain, the volcano, the meadow, the savanna, the forest),
        //itself otherwise. Everything a reduced program gives up is fine detail - sand ripples, sastrugi, rivulets,
        //sparkle - and past the grid's edge all of it is under a pixel and inside the haze, while the silhouettes,
        //which are what the ring is for, are the same height function in both. On Mars it measured within noise of
        //the full program (the ring's cost is its new pixels' count, not their program - docs/scenes.md, "The far
        //field"); it stays because it cannot cost more. Cached because the by-name indexer is a linear scan.
        private readonly Dictionary<EffectTechnique, EffectTechnique> _farRingTechniques = new();

        private EffectTechnique FarRingTechnique(Effect effect)
        {
            EffectTechnique grid = effect.CurrentTechnique;
            if (!_farRingTechniques.TryGetValue(grid, out EffectTechnique ring))
            {
                ring = effect.Techniques[grid.Name + "Reduced"] ?? grid;
                _farRingTechniques[grid] = ring;
            }

            return ring;
        }

        private FarFieldSlots FarSlots(Effect effect)
        {
            if (!_farFieldSlots.TryGetValue(effect, out FarFieldSlots slots))
            {
                slots = new FarFieldSlots
                {
                    Origin = effect.Parameters["OriginXZ"],
                    Ring = effect.Parameters["FarRing"],
                    Fade = effect.Parameters["FarFade"],
                    Sky = effect.Parameters["FarSky"],
                    Haze = effect.Parameters["HorizonHazeDistance"],
                };
                _farFieldSlots[effect] = slots;
            }

            return slots;
        }

        /// <summary>
        /// States the far fade for this frame's draws of <paramref name="effect"/>: the dome it fades into and the
        /// distances it runs over, and the ring clip OFF for the camera grid that draws first. A frame with no
        /// <see cref="SceneFrame.FarSky"/> gets no fade at all.
        /// </summary>
        public void Begin(Effect effect, in SceneFrame frame, float gridExtent)
        {
            FarFieldSlots slots = FarSlots(effect);
            slots.Ring?.SetValue(Vector4.Zero);

            if (frame.FarSky == null || slots.Fade == null)
            {
                slots.Fade?.SetValue(Vector2.Zero);
                return;
            }

            float start = MathF.Min(slots.Haze?.GetValueSingle() ?? FAR_FADE_START_LATEST, FAR_FADE_START_LATEST);
            float end = FAR_FADE_END;

            //The reduced tier draws no ring (FarRingDrawn), so its fade has to be complete inside the camera grid
            //instead: by the largest circle round the lens the grid covers on every side. Nearer than the ring's
            //fade, so a Low player sees less far - but still no edge, which is the part #551 is about.
            if (!FarRingDrawn)
            {
                end = gridExtent * Constants.HALF - FAR_RING_OVERLAP;
                start = MathF.Min(start, end * FAR_FADE_REDUCED_START);
            }

            slots.Sky.SetValue(frame.FarSky);
            slots.Fade.SetValue(new Vector2(start, end));
        }

        //Where the reduced tier's fade begins, as a share of where it must end: the grid's covered radius.
        private const float FAR_FADE_REDUCED_START = 0.85f;

        /// <summary>
        /// Whether the far ring is drawn: on every tier but the reduced one. Measured on Mars from a low camera
        /// looking out over the mesas, the ring costs most of a millisecond on the desktop at 4K (docs/scenes.md,
        /// "The far field"), and the reduced tier exists for the machine that cannot spare one - so there the
        /// land ends at the grid, faded into the sky before it does (<see cref="Begin"/>).
        /// </summary>
        private bool FarRingDrawn => SceneDetail > 0.5f;

        /// <summary>
        /// Draws <paramref name="effect"/>'s current technique a second time over the far ring, straight after the
        /// camera grid: the ring's vertices are already world positions, so the scene's own vertex shader runs
        /// unchanged with its origin at zero, and <c>FarRingClip</c> gives every pixel inside the camera grid back to
        /// the grid. The caller's device state (blend, rasterizer, depth) carries over, which is what makes the ring
        /// the same surface as the grid. Leaves the origin and the clip as the grid had them.
        /// </summary>
        public void DrawRing(Effect effect, Vector2 gridOrigin, float gridExtent)
        {
            FarFieldSlots slots = FarSlots(effect);
            if (slots.Ring == null || !FarRingDrawn) return;

            slots.Ring.SetValue(new Vector4(gridOrigin.X, gridOrigin.Y, gridExtent * Constants.HALF - FAR_RING_OVERLAP, 1f));
            slots.Origin.SetValue(Vector2.Zero);

            //The rings wholly inside the camera grid are not drawn at all. The rows run outward in the index buffer, so
            //skipping them is a start offset: the largest circle round the arena the grid still covers is its half-width
            //less the grid's own offset from the arena, and every row inside that (less one row, for the overlap) would
            //only be clipped away pixel by pixel. For the Game's cameras that is two fifths of the ring's rows.
            float covered = gridExtent * Constants.HALF - MathF.Max(MathF.Abs(gridOrigin.X), MathF.Abs(gridOrigin.Y)) - FAR_RING_OVERLAP;
            int firstRow = covered > FAR_RING_INNER
                ? Math.Clamp((int)(MathF.Log(covered / FAR_RING_INNER) / MathF.Log(_farRingGrowth)) - 1, 0, _farRingRows - 2)
                : 0;

            EffectTechnique grid = effect.CurrentTechnique;
            effect.CurrentTechnique = FarRingTechnique(effect);

            _graphicsDevice.SetVertexBuffer(_farRingVertexBuffer);
            _graphicsDevice.Indices = _farRingIndexBuffer;
            effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, firstRow * FAR_RING_SEGMENTS * 6,
                (_farRingRows - 1 - firstRow) * FAR_RING_SEGMENTS * 2);

            effect.CurrentTechnique = grid;
            slots.Ring.SetValue(Vector4.Zero);
            slots.Origin.SetValue(gridOrigin);
        }

        /// <summary>
        /// The ring: <see cref="FAR_RING_SEGMENTS"/> around, rings spaced geometrically from
        /// <see cref="FAR_RING_INNER"/> to <see cref="FAR_RING_OUTER"/> so a cell subtends the same angle from the
        /// arena at every radius, flat at y 0 like the camera grids (the scenes lift it). World positions, centred on
        /// the arena, built once. Drawn CullNone like every grid, so the winding does not matter.
        /// </summary>
        private void CreateFarRingMesh()
        {
            float step = FAR_RING_RADIAL_ASPECT * MathHelper.TwoPi / FAR_RING_SEGMENTS;
            int rings = (int)MathF.Ceiling(MathF.Log(FAR_RING_OUTER / FAR_RING_INNER) / MathF.Log(1f + step)) + 1;
            _farRingRows = rings;
            _farRingGrowth = 1f + step;
            int around = FAR_RING_SEGMENTS + 1;

            VertexPosition[] vertices = new VertexPosition[rings * around];
            for (int ring = 0; ring < rings; ring++)
            {
                float radius = MathF.Min(FAR_RING_INNER * MathF.Pow(1f + step, ring), FAR_RING_OUTER);
                for (int segment = 0; segment < around; segment++)
                {
                    //The last column repeats the first at exactly the same angle, so the seam closes bit for bit.
                    float angle = MathHelper.TwoPi * (segment % FAR_RING_SEGMENTS) / FAR_RING_SEGMENTS;
                    vertices[ring * around + segment] = new VertexPosition(
                        new Vector3(radius * MathF.Cos(angle), 0f, radius * MathF.Sin(angle)));
                }
            }

            _farRingVertexBuffer = new VertexBuffer(_graphicsDevice, VertexPosition.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            _farRingVertexBuffer.SetData(vertices);

            int[] indices = new int[(rings - 1) * FAR_RING_SEGMENTS * 6];
            int i = 0;
            for (int ring = 0; ring < rings - 1; ring++)
                for (int segment = 0; segment < FAR_RING_SEGMENTS; segment++)
                {
                    int a = ring * around + segment;
                    int b = a + 1;
                    int c = a + around;
                    int d = c + 1;

                    indices[i++] = a; indices[i++] = c; indices[i++] = b;
                    indices[i++] = b; indices[i++] = c; indices[i++] = d;
                }

            _farRingIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            _farRingIndexBuffer.SetData(indices);
        }

        /// <summary>
        /// The quality tier, as <see cref="SceneRenderer.SceneDetail"/> states it; the renderer forwards every change
        /// here. Only the far ring reads it (<see cref="FarRingDrawn"/>).
        /// </summary>
        public float SceneDetail { get; set; } = 1f;

        /// <summary>Builds the ring, once.</summary>
        public FarField(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            CreateFarRingMesh();
        }

        /// <summary>Frees the ring.</summary>
        public void Dispose()
        {
            _farRingVertexBuffer?.Dispose();
            _farRingIndexBuffer?.Dispose();
        }
    }
}
