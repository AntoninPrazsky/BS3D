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

        //The ring reaches from inside every camera grid to past the point the fade completes. The grid is centred on the
        //CAMERA while the ring is centred on the ARENA (a ring that followed the camera would swim), so the ring's rows
        //from FAR_RING_INNER out are what a camera round the arena needs: the smallest grid (+-500) still covers 340 units
        //round the arena with the lens 110 off it on a diagonal, and the Game's cameras in play and on the menu stand
        //within about 90 (the play pose 36 out, the chapter intro's tour 2.4 stand-offs). Inside FAR_RING_INNER the mesh
        //is a DISK down to the arena itself, its rows evenly spaced at the depth of the ring's first cell, for a lens
        //whose grid does not reach round the arena: the sea's chapter intro flies one out to an oil rig 820 out (#1124),
        //some 890 to 1070 from the arena, where the grid (+-800) covers little or none of the arena's water, and without
        //the disk the island stood on a hard edge of the water some twenty degrees wide while the horizon either side
        //faded softly.
        //Every other view's grid covers the disk, and its rows are skipped like any covered row (DrawRing), so what those
        //views draw is unchanged. The ring's OUTER edge still assumes a camera near the arena: it is FAR_RING_OUTER from
        //the arena and the fade completes FAR_FADE_END from the lens, so at the end of the run at the rig, the look turned
        //well off the arena, the ring ends about 1000 to 1300 off the lens at the frame's side edge, short of a full fade.
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

        /// <summary>
        /// Where the fade may begin at the latest. A scene's own haze distance is where it begins otherwise (see
        /// FarFieldSlots), but three scenes state 700-900 and their haze ends in the rig's HorizonColor, which is not
        /// the sky's: begun that late, the last few hundred units - all of them squeezed into the pixel or two at the
        /// skyline - stayed that colour and drew a thin line of it along the whole horizon (the icesheet's was teal
        /// under a lilac sky, its haze having made the ground wholly teal by 900). A ground still mostly its OWN colour where
        /// the fade begins has no foreign colour to squeeze into those pixels: they show its own edge going into the sky, and
        /// its pass may state a later latest start (<see cref="TerrainPass.FarFadeStartLatest"/>; the outback's plain is
        /// 0.85 its own colour at its 1000, #1123). A ground whose haze is strong by then, towards HorizonColor, may not.
        /// </summary>
        public const float FAR_FADE_START_LATEST = 600f;

        private VertexBuffer _farRingVertexBuffer;
        private IndexBuffer _farRingIndexBuffer;

        //Every row of the mesh, the disk's included, and the disk's alone: the ring's first row is row _farDiskRows.
        private int _farRingRows;
        private int _farDiskRows;
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
        //
        //And beside it the pass of that technique's depth alone ("<its name>Depth", #1097), null where the effect compiled
        //none (OpenGL ES, the sea, the outback): see DrawRing.
        private readonly Dictionary<EffectTechnique, (EffectTechnique Ring, EffectPass Depth)> _farRingTechniques = new();

        private (EffectTechnique Ring, EffectPass Depth) FarRingTechnique(Effect effect)
        {
            EffectTechnique grid = effect.CurrentTechnique;
            if (!_farRingTechniques.TryGetValue(grid, out (EffectTechnique Ring, EffectPass Depth) ring))
            {
                EffectTechnique technique = effect.Techniques[grid.Name + "Reduced"] ?? grid;
                ring = (technique, TerrainGridCache.DepthTwin(effect, technique));
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
        /// <see cref="SceneFrame.FarSky"/> gets no fade at all. <paramref name="latestStart"/> is the pass's own
        /// <see cref="TerrainPass.FarFadeStartLatest"/>.
        /// </summary>
        public void Begin(Effect effect, in SceneFrame frame, float gridExtent, float latestStart = FAR_FADE_START_LATEST)
        {
            FarFieldSlots slots = FarSlots(effect);
            slots.Ring?.SetValue(Vector4.Zero);

            if (frame.FarSky == null || slots.Fade == null)
            {
                slots.Fade?.SetValue(Vector2.Zero);
                return;
            }

            float start = FadeStart(slots.Haze?.GetValueSingle(), latestStart);
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

        /// <summary>
        /// Where the ring's tier begins the last fade: the scene's own haze distance (null where its effect declares none,
        /// as OpenGL ES drops a uniform the program never reads) or <paramref name="latestStart"/>, whichever is nearer.
        /// </summary>
        internal static float FadeStart(float? sceneHaze, float latestStart) => MathF.Min(sceneHaze ?? latestStart, latestStart);

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
        /// <para>
        /// With <paramref name="depthFirst"/>, and where the effect compiled the ring technique's depth alone, the ring is
        /// drawn twice (#1097): its depth first, laid by its cuts and nothing else with no colour written, then its
        /// program on that depth with <see cref="DepthStencilState.DepthRead"/>. A ring pixel the ground in front of it
        /// hides is thrown away before the program runs; the ring's program may discard, so drawn once it writes its
        /// depth only after it has run and nothing in its own draw is rejected early. The picture is the single draw's
        /// bit for bit: the same triangles pass, in the same order. The caller asks only where the scene supersamples and the ring is opaque on
        /// <see cref="DepthStencilState.Default"/>; the depth state is put back as it was found. The volcano never asks:
        /// its ring shaded on its laid depth measured dearer than drawn once (<see cref="VolcanoBackdrop"/>).
        /// </para>
        /// </summary>
        public void DrawRing(Effect effect, Vector2 gridOrigin, float gridExtent, bool depthFirst = false)
        {
            FarFieldSlots slots = FarSlots(effect);
            if (slots.Ring == null || !FarRingDrawn) return;

            slots.Ring.SetValue(new Vector4(gridOrigin.X, gridOrigin.Y, gridExtent * Constants.HALF - FAR_RING_OVERLAP, 1f));
            slots.Origin.SetValue(Vector2.Zero);

            //The rows wholly inside the camera grid are not drawn at all. The rows run outward in the index buffer, so
            //skipping them is a start offset: the largest circle round the arena the grid still covers is its half-width
            //less the grid's own offset from the arena, and every row inside that (less one row, for the overlap) would
            //only be clipped away pixel by pixel. For the Game's cameras that is the whole disk and two fifths of the
            //ring's rows; a lens whose grid covers less than FAR_RING_INNER round the arena (or none of it) draws the disk
            //from the row inside what it covers.
            float covered = gridExtent * Constants.HALF - MathF.Max(MathF.Abs(gridOrigin.X), MathF.Abs(gridOrigin.Y)) - FAR_RING_OVERLAP;
            int firstRow = covered > FAR_RING_INNER
                ? _farDiskRows + Math.Clamp((int)(MathF.Log(covered / FAR_RING_INNER) / MathF.Log(_farRingGrowth)) - 1, 0, _farRingRows - _farDiskRows - 2)
                : Math.Max((int)(covered / FAR_RING_INNER * _farDiskRows) - 1, 0);

            EffectTechnique grid = effect.CurrentTechnique;
            (EffectTechnique ring, EffectPass depth) = FarRingTechnique(effect);
            effect.CurrentTechnique = ring;

            _graphicsDevice.SetVertexBuffer(_farRingVertexBuffer);
            _graphicsDevice.Indices = _farRingIndexBuffer;

            int start = firstRow * FAR_RING_SEGMENTS * 6, triangles = (_farRingRows - 1 - firstRow) * FAR_RING_SEGMENTS * 2;
            if (depthFirst && depth != null)
            {
                BlendState blend = _graphicsDevice.BlendState;
                DepthStencilState depthState = _graphicsDevice.DepthStencilState;

                _graphicsDevice.BlendState = TerrainGridCache.DEPTH_ONLY;
                depth.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, start, triangles);

                _graphicsDevice.BlendState = blend;
                _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
                ring.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, start, triangles);
                _graphicsDevice.DepthStencilState = depthState;
            }
            else
            {
                ring.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, start, triangles);
            }

            effect.CurrentTechnique = grid;
            slots.Ring.SetValue(Vector4.Zero);
            slots.Origin.SetValue(gridOrigin);
        }

        /// <summary>
        /// The ring: <see cref="FAR_RING_SEGMENTS"/> around, rings spaced geometrically from
        /// <see cref="FAR_RING_INNER"/> to <see cref="FAR_RING_OUTER"/> so a cell subtends the same angle from the
        /// arena at every radius, and inside it the disk, rows evenly spaced at the depth of the ring's first cell
        /// from the arena itself (its first row is the centre, so its first cells are triangles), flat at y 0 like the
        /// camera grids (the scenes lift it). World positions, centred on the arena, built once. Drawn CullNone like
        /// every grid, so the winding does not matter.
        /// </summary>
        private void CreateFarRingMesh()
        {
            float step = FAR_RING_RADIAL_ASPECT * MathHelper.TwoPi / FAR_RING_SEGMENTS;
            int disk = (int)MathF.Ceiling(1f / step);
            int rings = (int)MathF.Ceiling(MathF.Log(FAR_RING_OUTER / FAR_RING_INNER) / MathF.Log(1f + step)) + 1;
            _farDiskRows = disk;
            _farRingRows = disk + rings;
            _farRingGrowth = 1f + step;
            int around = FAR_RING_SEGMENTS + 1;

            VertexPosition[] vertices = new VertexPosition[_farRingRows * around];
            for (int row = 0; row < _farRingRows; row++)
            {
                //The ring's radii are computed as they were before the disk was added, so its rows are the same to the bit
                int ring = row - disk;
                float radius = ring < 0
                    ? FAR_RING_INNER * row / disk
                    : MathF.Min(FAR_RING_INNER * MathF.Pow(1f + step, ring), FAR_RING_OUTER);
                for (int segment = 0; segment < around; segment++)
                {
                    //The last column repeats the first at exactly the same angle, so the seam closes bit for bit.
                    float angle = MathHelper.TwoPi * (segment % FAR_RING_SEGMENTS) / FAR_RING_SEGMENTS;
                    vertices[row * around + segment] = new VertexPosition(
                        new Vector3(radius * MathF.Cos(angle), 0f, radius * MathF.Sin(angle)));
                }
            }

            _farRingVertexBuffer = new VertexBuffer(_graphicsDevice, VertexPosition.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            _farRingVertexBuffer.SetData(vertices);

            int[] indices = new int[(_farRingRows - 1) * FAR_RING_SEGMENTS * 6];
            int i = 0;
            for (int row = 0; row < _farRingRows - 1; row++)
                for (int segment = 0; segment < FAR_RING_SEGMENTS; segment++)
                {
                    int a = row * around + segment;
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
