using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The Grid (the twentieth scene, #393): a flat, glowing circuit-board floor, distant solids each running
    /// its own Game of Life, the landmark ring and a starless sky-replacing void, three techniques of
    /// <c>Grid.fx</c> in the Moon's terrain-then-sky order. Moved out of <see cref="SceneRenderer"/> whole in
    /// #580, the first backdrop on a terrain grid: it takes its grid through
    /// <see cref="BackdropServices.AcquireGridMesh"/> and its island cut through
    /// <see cref="BackdropServices.TerrainHoleRadius"/>. The renderer's public <c>GridSolids</c> and
    /// <c>TryGetGridRing</c> forward here. See "The Grid" in docs/scenes.md.
    /// </summary>
    internal sealed class GridBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private GridSceneConfig _gridConfig = new();

        //The twentieth scene (#393), and the third in both families at once — see IsSolidTerrainScene's and
        //ReplacesSky's own docs. A flat, glowing circuit-board floor and distant solids under a starless
        //sky-replacing void, three techniques in one effect, the Moon's own shape (Draw draws the terrain
        //and the solids first, depth-writing, then the sky quad depth-READ against them — the Moon's measured
        //order; the opposite interleave was an 8x blow-up there and is not being re-measured here to find out
        //whether it still is).
        private readonly Effect _gridEffect;
        private readonly VertexBuffer _gridVertexBuffer;
        private readonly IndexBuffer _gridIndexBuffer;
        private readonly int _gridIndexCount;

        private readonly EffectTechnique _gridSkyTechnique, _gridTerrainTechnique, _gridTowerTechnique;

        //Per-frame and per-draw parameters, resolved once (BestPractices §1).
        private readonly EffectParameter _gridOriginXZ, _gridHoleRadius, _gridView, _gridProjection,
            _gridCameraPosition, _gridInverseViewProjection, _gridLifeTextureParam, _gridLifeAgeParam, _gridLifeCentreParam;

        //The mesh is deliberately coarse — GridTerrainVS never displaces a vertex (the floor is a constant
        //Y), so nothing is lost by skipping the few-hundred-vertex density every OTHER terrain scene needs
        //to hold its own displacement. The extent matches the Moon's and the aurora's: re-centred on the camera
        //every frame, its half-width (600) is well past GridHorizonHazeDistance (420), so the floor has faded to
        //the void before its edge and the far plane (2000 since #551) never meets it at all.
        private const int GRID_MESH_N = 8;
        private const float GRID_EXTENT = 1200f;

        //The shortest step interval a board is stepped at, whatever the config says — a guard for a map editor
        //slider dragged to zero, not a tuning value.
        private const float MIN_GRID_LIFE_STEP_INTERVAL = 0.05f;

        //The distant solids (#393): built once (BuildGridTowers) as world-space geometry with no world
        //matrix and no instancing — see Grid.fx's own GridTowers header for why.
        private VertexBuffer _gridTowerVertexBuffer;
        private IndexBuffer _gridTowerIndexBuffer;

        //One independent Game of Life per solid — the owner's review asked for "different nice variants" per
        //object — with the texture it is uploaded to and the clock it steps on. GridLife owns the rules, the named
        //patterns and when a board moves on to its next one.
        private sealed class GridLifeBoard
        {
            public GridLife Life;
            public Texture2D Texture;
            public readonly Color[] UploadBuffer = new Color[GridLife.SIZE * GridLife.SIZE];

            //Where in the step interval this board ticks, 0-1, spread evenly over the solids: eighteen machines
            //each keeping its own time rather than one clock, and never every board's step and upload in one frame.
            public float Phase;
            public float NextStepTime;

            //When the current generation appeared, for the phosphor's decay — far in the past until the first
            //step, so nothing starts out glowing.
            public float LastStepTime = -1e6f;
        }

        //One board per solid, and — matched to it 1:1 by index — the (start index, primitive count) each
        //solid's own quads occupy in the ONE shared vertex/index buffer, so drawing a solid with its own board
        //still costs one combined buffer and one draw call per solid rather than a buffer each.
        private readonly List<GridLifeBoard> _gridLifeBoards = new();
        private readonly List<(int StartIndex, int PrimitiveCount)> _gridTowerRanges = new();

        //The same solids and the landmark as shapes rather than triangles (#559), recorded as they are placed:
        //the one answer to "where is that cube" for a camera that frames one (GridSolids, TryGetGridRing).
        private readonly List<GridSolid> _gridSolids = new();
        private GridRing? _gridRing;

        //A per-pixel struct rather than the shared InstancedModel vertex formats: the solids draw through their
        //own unlit, black-body GridTowers technique (see Grid.fx), which wants a baked world-space position, the
        //solid's own board coordinate and the face-local coordinate its seams are measured from — not a normal
        //or a model UV.
        private struct GridTowerVertex : IVertexType
        {
            public Vector3 Position;
            public Vector2 WindowUV;
            public Vector4 FaceLocal;

            //The face's outward normal (#512). Constant across a quad by construction, which is the point:
            //what it buys is FLAT shading, one value a face, which is what a 1982 renderer did and what makes
            //a polyhedron read as a volume with no lighting model under it at all.
            public Vector3 FaceNormal;

            public GridTowerVertex(Vector3 position, Vector2 windowUV, Vector4 faceLocal, Vector3 faceNormal)
            {
                Position = position;
                WindowUV = windowUV;
                FaceLocal = faceLocal;
                FaceNormal = faceNormal;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(20, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(36, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        /// <summary>Loads the effect, takes its grid, caches its parameters, pushes the config and builds the solids.</summary>
        public GridBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Grid (#393): the twentieth scene, the third in both families at once — a flat, glowing
            //circuit-board floor and distant solids under a starless sky-replacing void, three techniques in one
            //effect, the Moon's and the aurora's own shape (see the region doc above it). GRID_MESH_N is
            //deliberately coarse — see that constant's own doc for why a scene with no displacement can afford it.
            _gridEffect = content.Load<Effect>("Shaders/Grid");
            Services.AcquireGridMesh(GRID_MESH_N, GRID_EXTENT, out _gridVertexBuffer, out _gridIndexBuffer, out _gridIndexCount);

            _gridTerrainTechnique = _gridEffect.Techniques["GridTerrain"];
            _gridSkyTechnique = _gridEffect.Techniques["GridSky"];
            _gridTowerTechnique = _gridEffect.Techniques["GridTowers"];

            _gridOriginXZ = _gridEffect.Parameters["OriginXZ"];
            _gridHoleRadius = _gridEffect.Parameters["IslandHoleRadius"];
            _gridView = _gridEffect.Parameters["View"];
            _gridProjection = _gridEffect.Parameters["Projection"];
            _gridCameraPosition = _gridEffect.Parameters["CameraPosition"];
            _gridInverseViewProjection = _gridEffect.Parameters["InverseViewProjection"];
            _gridLifeTextureParam = _gridEffect.Parameters["GridLifeTexture"];
            _gridLifeAgeParam = _gridEffect.Parameters["GridLifeAge"];
            _gridLifeCentreParam = _gridEffect.Parameters["GridLifeCentreOffset"];

            //Pushes the terrain and tower uniforms and builds the solids' geometry, one independent Life
            //board per solid included (BuildGridTowers) - one call for both, since a later config edit
            //needs to redo exactly the same pair.
            ApplyGridParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Grid;

        /// <inheritdoc/>
        public override SceneConfig Config => _gridConfig;

        /// <summary>
        /// Pushes everything about the Grid scene that is fixed for as long as the config is, and rebuilds the
        /// solids and their boards. What does vary — the camera, and each solid's board stepping on its own clock
        /// — is set per frame and per draw in <see cref="Draw"/>.
        /// </summary>
        private void ApplyGridParameters()
        {
            _gridEffect.Parameters["VoidColor"].SetValue(_gridConfig.VoidColor.ToVector3());

            GridTerrainConfig terrain = _gridConfig.Terrain;
            _gridEffect.Parameters["GridLevelY"].SetValue(terrain.LevelY);
            _gridEffect.Parameters["GridCellSize"].SetValue(terrain.CellSize);
            _gridEffect.Parameters["GridTraceStride"].SetValue((float)Math.Max(terrain.TraceStride, 1));
            _gridEffect.Parameters["GridLineWidth"].SetValue(terrain.LineWidth);
            _gridEffect.Parameters["GridAccentWidthScale"].SetValue(terrain.AccentWidthScale);
            _gridEffect.Parameters["GridHorizonHazeDistance"].SetValue(terrain.HorizonHazeDistance);
            _gridEffect.Parameters["GridBodyColor"].SetValue(terrain.BodyColor.ToVector3());
            _gridEffect.Parameters["GridLineColor"].SetValue(terrain.LineColor.ToVector3());
            _gridEffect.Parameters["GridAccentColor"].SetValue(terrain.AccentColor.ToVector3());

            GridTowerConfig towers = _gridConfig.Towers;
            _gridEffect.Parameters["GridTowerWindowCellSize"].SetValue(towers.WindowCellSize);
            _gridEffect.Parameters["GridTowerWindowMargin"].SetValue(towers.WindowMargin);
            _gridEffect.Parameters["GridTowerBodyColor"].SetValue(towers.BodyColor.ToVector3());
            _gridEffect.Parameters["GridTowerWindowColor"].SetValue(towers.WindowColor.ToVector3());
            _gridEffect.Parameters["GridTowerEdgeWidth"].SetValue(towers.EdgeWidth);
            _gridEffect.Parameters["GridTowerEdgeColor"].SetValue(towers.EdgeColor.ToVector3());
            _gridEffect.Parameters["GridFaceLight"].SetValue(Vector3.Normalize(towers.FaceLight.ToVector3()));
            _gridEffect.Parameters["GridFaceShadeFloor"].SetValue(towers.FaceShadeFloor);
            _gridEffect.Parameters["GridPhosphorDecay"].SetValue(towers.PhosphorDecay);

            //Placement (count/radius/height/footprint/seed) and the boards only take effect through a rebuild, so
            //the parameters and the solids are pushed from one place — which is what kept the two in step while
            //the map editor's live panel (gone in #522) could re-apply a config at any moment.
            BuildGridTowers();
        }

        /// <summary>
        /// Draws the Grid scene: the flat, glowing floor first (depth-writing, opaque), then the distant
        /// solids (also opaque, also depth-writing — they stand ON the floor and must occlude both it
        /// and the sky behind them), then the sky quad depth-READ against both — the Moon's and the
        /// aurora's own measured order (see <c>SceneRenderer.DrawMoon</c>'s doc). The floor's own uniforms are
        /// a one-time push (<see cref="ApplyGridParameters"/>) — what varies here is the camera, the
        /// origin snap every terrain draw already needs, and each solid's Life board, stepped on its own
        /// clock only while this scene is the one actually being drawn.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            float cell = GRID_EXTENT / (GRID_MESH_N - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _gridOriginXZ.SetValue(new Vector2(originX, originZ));
            _gridHoleRadius.SetValue(Services.TerrainHoleRadius);
            _gridView.SetValue(frame.Camera.View);
            _gridProjection.SetValue(frame.Camera.Projection);
            _gridCameraPosition.SetValue(frame.Camera.Position);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_gridVertexBuffer);
            _graphicsDevice.Indices = _gridIndexBuffer;
            _gridEffect.CurrentTechnique = _gridTerrainTechnique;
            _gridEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _gridIndexCount / 3);

            if (_gridTowerRanges.Count > 0)
            {
                //One combined buffer, but one draw call PER SOLID (its own index range, below) rather than
                //one draw for all of them — each solid reads its own independent Life texture, and a draw
                //call can bind only one texture at a time. A few dozen extra draw calls is nothing next to
                //the city's own thousands-of-buildings frame, so this is not a cost worth avoiding.
                _graphicsDevice.SetVertexBuffer(_gridTowerVertexBuffer);
                _graphicsDevice.Indices = _gridTowerIndexBuffer;
                _gridEffect.CurrentTechnique = _gridTowerTechnique;

                //Every solid is a closed prism wound clockwise seen from outside (BoxMesh's convention), so its far
                //faces are culled rather than shaded and then hidden.
                _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

                float interval = MathF.Max(_gridConfig.Towers.LifeStepInterval, MIN_GRID_LIFE_STEP_INTERVAL);

                for (int i = 0; i < _gridTowerRanges.Count; i++)
                {
                    GridLifeBoard board = _gridLifeBoards[i];

                    //Timer-gated, not per-frame: a generation every LifeStepInterval seconds of the wall clock
                    //every other scene's animation reads, on the tick of this board's own phase. Leaving the scene
                    //and coming back fires at most one catch-up step per board, never a burst, and the next tick is
                    //found from the phase rather than from "now", so the boards stay spread across the interval.
                    if (frame.Time >= board.NextStepTime)
                    {
                        board.Life.Step();
                        UploadGridLifeTexture(board);
                        board.LastStepTime = frame.Time;

                        board.NextStepTime = (MathF.Floor(frame.Time / interval - board.Phase) + 1f + board.Phase) * interval;
                        if (board.NextStepTime <= frame.Time) board.NextStepTime += interval;
                    }

                    _gridLifeTextureParam.SetValue(board.Texture);
                    _gridLifeAgeParam.SetValue(frame.Time - board.LastStepTime);

                    //The pattern's centre rather than the board's on the middle of the face (see GridLife.CentreX).
                    _gridLifeCentreParam.SetValue(new Vector2(board.Life.CentreX, board.Life.CentreY) - new Vector2(GridLife.SIZE * 0.5f));
                    _gridEffect.CurrentTechnique.Passes[0].Apply();

                    (int startIndex, int primitiveCount) = _gridTowerRanges[i];
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, startIndex, primitiveCount);
                }

                //The sky quad below goes on drawing under the floor's own state, as the Moon's does.
                _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            }

            //Then the sky, depth-READ at the far plane: every pixel the floor or a monolith already owns is
            //rejected before the void shader runs.
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            _gridInverseViewProjection.SetValue(Matrix.Invert(frame.Camera.View * frame.Camera.Projection));

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _gridEffect.CurrentTechnique = _gridSkyTechnique;
            _gridEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Builds the distant solids (#393): <see cref="GridTowerConfig.Count"/> plain closed rectangular prisms,
        /// placed on a ring around the arena with a deterministic seed (<see cref="GridTowerConfig.Seed"/>)
        /// so the Game, the Testbed and the map editor all stand them in the same places — the map itself
        /// is shared between the three for the same reason. A <see cref="GridTowerConfig.CubeFraction"/> of
        /// them are large, close-to-equilateral cubes rather than tall towers; a simple footprint-circle retry
        /// keeps them from overlapping without a spatial structure, since this runs once at load/config-apply
        /// time on at most a few dozen solids. The same check keeps every solid off the landmark ring's
        /// footprint (#559) — and that one is never waived, see the retry. Every solid has a roof: the Testbed's
        /// free camera and the map editor's can look down on any of them, and an open top would show the void through it once the far
        /// faces are culled. Also (re)builds one <see cref="GridLifeBoard"/> per solid, seeded from the same
        /// placement stream, so every host shows the same patterns on the same solids. Idempotent and safe to
        /// call again from <see cref="ApplyGridParameters"/>: disposes whatever it last built before building the
        /// new placement.
        /// </summary>
        private void BuildGridTowers()
        {
            //The floor left between two solids' footprint circles, and between a solid and the landmark's.
            const float SOLID_GAP = 15f;

            _gridTowerVertexBuffer?.Dispose();
            _gridTowerIndexBuffer?.Dispose();
            _gridTowerVertexBuffer = null;
            _gridTowerIndexBuffer = null;

            foreach (GridLifeBoard board in _gridLifeBoards) board.Texture?.Dispose();
            _gridLifeBoards.Clear();
            _gridTowerRanges.Clear();
            _gridSolids.Clear();
            _gridRing = null;

            GridTowerConfig towers = _gridConfig.Towers;
            if (towers.Count <= 0) return;

            List<GridTowerVertex> vertices = new();
            List<short> indices = new();

            //Footprint circles of everything placed so far, for the overlap retry below - a plain list
            //rather than a spatial structure, because this runs once at load/config-apply time on at most
            //a few dozen solids, not per frame.
            List<(Vector2 Center, float Radius)> placed = new();

            //The landmark's footprint, which every solid keeps off (#559). It is built after them, from the
            //config alone and not from this stream, so the solids have to be told where it will stand: until
            //they were, sceneseed=3 stood a cube straight through the ring.
            GridRing? landmark = GridLandmarkShape();

            //Fixed seed: placement is data every host must agree on, and so, since the boards are seeded from the
            //same stream, is what each solid shows.
            Random placement = new(towers.Seed + Services.SeedOffset);

            //The first CubeFraction of Count are cubes, the rest towers - which member of the count gets
            //which shape carries no meaning (angle and radius are drawn independently either way), so there
            //is nothing to gain from shuffling the assignment.
            int cubeCount = (int)MathF.Round(towers.Count * MathHelper.Clamp(towers.CubeFraction, 0f, 1f));

            for (int i = 0; i < towers.Count; i++)
            {
                bool isCube = i < cubeCount;

                //Redrawn every attempt of the retry below, so the final, accepted draw is whichever attempt
                //broke out of (or exhausted) the loop.
                Vector3 baseCenter = default;
                float sizeX = 0f, sizeY = 0f, sizeZ = 0f;
                Vector2 xz = default;
                float footprintRadius = 0f;

                //Twenty attempts to find a draw clear of everything; past them, any draw clear of the ring
                //(see the fall-through below). The ceiling on those is a guard, not a budget: the footprint
                //takes a few percent of the band's area, so a draw that misses it is the ordinary case.
                const int MAX_PLACEMENT_ATTEMPTS = 20, MAX_RING_ATTEMPTS = 200;
                for (int attempt = 0; attempt < MAX_RING_ATTEMPTS; attempt++)
                {
                    float angle = (float)(placement.NextDouble() * MathHelper.TwoPi);
                    float radius = MathHelper.Lerp(towers.RadiusMin, towers.RadiusMax, (float)placement.NextDouble());

                    if (isCube)
                    {
                        //A big, close-to-equilateral block rather than a random footprint range: the point
                        //(the owner's own follow-up request) is a face large enough to show the shared Life
                        //grid whole, which is what reads as an abstract digital object rather than a
                        //building with windows on it. A little per-axis jitter keeps every cube from being
                        //a literally identical solid without ever approaching a tower's proportions.
                        float side = MathHelper.Lerp(towers.CubeSizeMin, towers.CubeSizeMax, (float)placement.NextDouble());
                        sizeX = side * (0.94f + 0.12f * (float)placement.NextDouble());
                        sizeY = side * (0.94f + 0.12f * (float)placement.NextDouble());
                        sizeZ = side * (0.94f + 0.12f * (float)placement.NextDouble());
                    }
                    else
                    {
                        sizeY = MathHelper.Lerp(towers.TowerHeightMin, towers.TowerHeightMax, (float)placement.NextDouble());
                        sizeX = MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)placement.NextDouble());
                        sizeZ = MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)placement.NextDouble());
                    }

                    baseCenter = new Vector3(MathF.Cos(angle) * radius, _gridConfig.Terrain.LevelY, MathF.Sin(angle) * radius);
                    xz = new Vector2(baseCenter.X, baseCenter.Z);
                    footprintRadius = 0.5f * MathF.Sqrt(sizeX * sizeX + sizeZ * sizeZ);

                    //The ring is a hard rule and the other solids a soft one: a solid standing through the
                    //ring is a solid visibly inside another, where two blocks barely touching is merely close.
                    if (landmark is GridRing ring && ring.FootprintDistance(xz) < footprintRadius + SOLID_GAP) continue;

                    bool overlaps = false;
                    foreach ((Vector2 otherXz, float otherRadius) in placed)
                    {
                        if (Vector2.Distance(xz, otherXz) < footprintRadius + otherRadius + SOLID_GAP) { overlaps = true; break; }
                    }

                    if (!overlaps || attempt >= MAX_PLACEMENT_ATTEMPTS - 1) break;
                    //Exhausting the twenty attempts falls through with the first later draw that clears the
                    //ring rather than dropping the solid silently - a rare, barely-touching pair reads better
                    //than a scene that asked for eighteen and quietly drew fewer.
                }

                placed.Add((xz, footprintRadius));
                _gridSolids.Add(new GridSolid(baseCenter, new Vector3(sizeX, sizeY, sizeZ), isCube));

                //Eight draws per tower and ten per cube, exactly as many as the reviewed layout spent after each
                //solid, so every later solid still stands where the owner saw it; the first now seeds this
                //solid's board.
                int lifeSeed = (int)(placement.NextDouble() * int.MaxValue);
                for (int draw = 1; draw < (isCube ? 10 : 8); draw++) placement.NextDouble();

                //Stepped for the first time the moment Draw asks for it (NextStepTime 0), then on its phase.
                GridLifeBoard board = new()
                {
                    Life = new GridLife(lifeSeed, tall: !isCube, towers.LifePatternGenerations),
                    Texture = new Texture2D(_graphicsDevice, GridLife.SIZE, GridLife.SIZE, false, SurfaceFormat.Color),
                    Phase = (float)i / towers.Count,
                };
                UploadGridLifeTexture(board);
                _gridLifeBoards.Add(board);

                int rangeStartIndex = indices.Count;

                float halfX = sizeX * 0.5f, halfY = sizeY * 0.5f, halfZ = sizeZ * 0.5f;
                Vector3 center = baseCenter + Vector3.Up * halfY;

                //The four sides share one board coordinate running round the perimeter (see Grid.fx's GridTowers
                //header), taken in the order they meet corner to corner — +X, -Z, -X, +Z, each side's right edge
                //the next one's left — so the board wraps round the solid like a label. Where each side starts
                //along it:
                float startNegZ = sizeZ, startNegX = sizeZ + sizeX, startPosZ = 2f * sizeZ + sizeX;

                //The side facing the arena is the one whose outward normal points most nearly back at the origin,
                //and the board's centre goes on its middle, half-way up: a pattern is stamped centred on its
                //board, so the face the play camera actually sees shows it whole.
                float facingMiddle = MathF.Abs(baseCenter.X) >= MathF.Abs(baseCenter.Z)
                    ? (baseCenter.X > 0f ? startNegX : 0f) + halfZ
                    : (baseCenter.Z > 0f ? startNegZ : startPosZ) + halfX;

                float boardCentre = 0.5f * GridLife.SIZE * towers.WindowCellSize;
                Vector2 boardOrigin = new(boardCentre - facingMiddle, boardCentre - halfY);

                //BoxMesh.AddFace's own side-face parameters, right x up = the outward normal, so the winding
                //(mirrored from BoxMesh.AddFace) reads clockwise from outside — see the repo convention in
                //CLAUDE.md.
                AddTowerFace(vertices, indices, Vector3.Forward, Vector3.Up, sizeZ, sizeY,
                    center + halfX * Vector3.Right, boardOrigin);
                AddTowerFace(vertices, indices, Vector3.Left, Vector3.Up, sizeX, sizeY,
                    center + halfZ * Vector3.Forward, boardOrigin + new Vector2(startNegZ, 0f));
                AddTowerFace(vertices, indices, Vector3.Backward, Vector3.Up, sizeZ, sizeY,
                    center + halfX * Vector3.Left, boardOrigin + new Vector2(startNegX, 0f));
                AddTowerFace(vertices, indices, Vector3.Right, Vector3.Up, sizeX, sizeY,
                    center + halfZ * Vector3.Backward, boardOrigin + new Vector2(startPosZ, 0f));

                //The roof - BoxMesh.AddFace's own top-face parameters (right = Right, up = Forward) - with the
                //board's centre on its own.
                AddTowerFace(vertices, indices, Vector3.Right, Vector3.Forward, sizeX, sizeZ,
                    center + halfY * Vector3.Up, new Vector2(boardCentre - halfX, boardCentre - halfZ));

                _gridTowerRanges.Add((rangeStartIndex, (indices.Count - rangeStartIndex) / 3));
            }

            BuildGridLandmark(vertices, indices, placement);

            _gridTowerVertexBuffer = new VertexBuffer(_graphicsDevice, GridTowerVertex.Declaration, vertices.Count, BufferUsage.WriteOnly);
            _gridTowerVertexBuffer.SetData(vertices.ToArray());

            _gridTowerIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, indices.Count, BufferUsage.WriteOnly);
            _gridTowerIndexBuffer.SetData(indices.ToArray());
        }

        /// <summary>
        /// The landmark (#512): a ring standing on edge, far out on the floor. The issue asked for a hero object
        /// with more presence than the eighteen towers and cubes, built to the same procedural-solid discipline
        /// and lit by its own seams — and of the five shapes the references drew (a stepped ziggurat, a faceted
        /// polyhedron on a pedestal, a slotted tower, a ring on edge, a stack of cubes) the ring is the one
        /// silhouette <b>nothing else in this scene has</b>. Every other solid here is a box; a ring reads as a
        /// landmark from any bearing, which is what a landmark is for.
        /// <para>
        /// It is a faceted torus — <see cref="GridLandmarkConfig.Segments"/> trapezoid segments, each four quads
        /// (outer band, inner band and the two flanks) — so it stays inside the <c>BoxMesh</c> vocabulary the
        /// scene credits to MAGI/SynthaVision: flat quads, no sculpting, nothing swept that a 1982 renderer
        /// could not have combined out of solids.
        /// </para>
        /// <para>
        /// ⚠ <b>The segment joins must not glow, or the ring is a barrel of ribs.</b> Every quad's seam shader
        /// lights all four of its borders, so a faceted ring drawn the way a tower is would show one bright rib
        /// per segment. Each quad therefore reports a face-local X pinned to the middle of an arbitrarily wide
        /// face, so its two <i>across</i> borders can never be within an edge width of the pixel — only the
        /// long borders draw, and what the eye gets is a pair of clean rails running round the ring.
        /// </para>
        /// </summary>
        private void BuildGridLandmark(List<GridTowerVertex> vertices, List<short> indices, Random placement)
        {
            if (GridLandmarkShape() is not GridRing shape) return;
            GridLandmarkConfig landmark = _gridConfig.Landmark;

            float tube = MathF.Max(landmark.TubeRadius, 0.1f);
            float halfWidth = shape.HalfWidth;
            Vector3 planeNormal = shape.PlaneNormal;
            Vector3 centre = shape.Centre;

            //(s, up, n) right-handed: s = up x n, so s x up = n. Every winding below is derived from that one
            //identity, which is what keeps the outward normals outward without a single guessed sign.
            Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.Up, planeNormal));

            int rangeStartIndex = indices.Count;

            //Its own board, seeded from the placement stream after every solid so no solid moved, and stamped
            //from the TALL deck: a spaceship convoy travelling round a ring is the one thing this shape can do
            //that a box cannot.
            int lifeSeed = (int)(placement.NextDouble() * int.MaxValue);
            GridLifeBoard board = new()
            {
                Life = new GridLife(lifeSeed, tall: true, _gridConfig.Towers.LifePatternGenerations),
                Texture = new Texture2D(_graphicsDevice, GridLife.SIZE, GridLife.SIZE, false, SurfaceFormat.Color),
                Phase = 0.5f,
            };
            UploadGridLifeTexture(board);
            _gridLifeBoards.Add(board);

            float outer = shape.OuterRadius;
            float inner = shape.InnerRadius;
            _gridRing = shape;

            //The board runs round the ring's outer band, centred on the point nearest the arena — which, the
            //plane facing the arena, is the segment at the top of the near side. Same idea as a tower's label.
            float perimeter = MathHelper.TwoPi * outer;
            float boardCentre = 0.5f * GridLife.SIZE * _gridConfig.Towers.WindowCellSize;

            for (int i = 0; i < landmark.Segments; i++)
            {
                float a0 = MathHelper.TwoPi * i / landmark.Segments;
                float a1 = MathHelper.TwoPi * (i + 1) / landmark.Segments;

                Vector3 d0 = side * MathF.Cos(a0) + Vector3.Up * MathF.Sin(a0);
                Vector3 d1 = side * MathF.Cos(a1) + Vector3.Up * MathF.Sin(a1);
                Vector3 tangent = Vector3.Normalize(d1 - d0);

                float arc0 = perimeter * i / landmark.Segments;
                float arc1 = perimeter * (i + 1) / landmark.Segments;
                float u0 = boardCentre + arc0 - 0.25f * perimeter;
                float u1 = boardCentre + arc1 - 0.25f * perimeter;

                //Outer band: right = the tangent, up = the plane's normal, so right x up = the radial
                //direction and the quad faces out of the ring.
                AddGridQuad(vertices, indices,
                    centre + d0 * outer - planeNormal * halfWidth, centre + d1 * outer - planeNormal * halfWidth,
                    centre + d1 * outer + planeNormal * halfWidth, centre + d0 * outer + planeNormal * halfWidth,
                    Vector3.Normalize(d0 + d1), u0, u1, boardCentre - halfWidth, boardCentre + halfWidth, 2f * halfWidth);

                //Inner band: the same quad at the inner radius, wound the other way round so it faces the hole.
                AddGridQuad(vertices, indices,
                    centre + d1 * inner - planeNormal * halfWidth, centre + d0 * inner - planeNormal * halfWidth,
                    centre + d0 * inner + planeNormal * halfWidth, centre + d1 * inner + planeNormal * halfWidth,
                    -Vector3.Normalize(d0 + d1), u1, u0, boardCentre - halfWidth, boardCentre + halfWidth, 2f * halfWidth);

                //The two flanks, each spanning inner to outer: right = radially out, up = ±the tangent, so
                //right x up = ±the plane's normal (d x t = n, the identity this whole block rests on).
                AddGridQuad(vertices, indices,
                    centre + d0 * inner + planeNormal * halfWidth, centre + d0 * outer + planeNormal * halfWidth,
                    centre + d1 * outer + planeNormal * halfWidth, centre + d1 * inner + planeNormal * halfWidth,
                    planeNormal, u0, u1, boardCentre - tube, boardCentre + tube, 2f * tube);

                AddGridQuad(vertices, indices,
                    centre + d1 * inner - planeNormal * halfWidth, centre + d1 * outer - planeNormal * halfWidth,
                    centre + d0 * outer - planeNormal * halfWidth, centre + d0 * inner - planeNormal * halfWidth,
                    -planeNormal, u1, u0, boardCentre - tube, boardCentre + tube, 2f * tube);
            }

            _gridTowerRanges.Add((rangeStartIndex, (indices.Count - rangeStartIndex) / 3));
        }

        /// <summary>
        /// Where the landmark stands and how big it is, from the config alone — null when it is not built. One
        /// answer for the two things that need it before and while it is built: <see cref="BuildGridTowers"/>
        /// keeps every solid off its footprint (#559), and <see cref="BuildGridLandmark"/> builds it there.
        /// </summary>
        private GridRing? GridLandmarkShape()
        {
            GridLandmarkConfig landmark = _gridConfig.Landmark;
            if (!landmark.Enabled || landmark.Segments < 3) return null;

            float ringRadius = MathF.Max(landmark.Radius, 1f);
            float tube = MathF.Max(landmark.TubeRadius, 0.1f);
            float halfWidth = MathF.Max(landmark.Width, 0.1f) * 0.5f;

            float bearing = MathHelper.ToRadians(landmark.Bearing);
            Vector3 stand = new(MathF.Cos(bearing) * landmark.Distance,
                _gridConfig.Terrain.LevelY, MathF.Sin(bearing) * landmark.Distance);

            //The ring's plane faces the arena, so the play camera sees it as a ring and not as an edge-on bar.
            Vector3 planeNormal = new(-stand.X, 0f, -stand.Z);
            planeNormal = planeNormal.LengthSquared() < 1e-6f ? Vector3.Forward : Vector3.Normalize(planeNormal);

            //Resting on the floor: the lowest point of the tube is the ground.
            Vector3 centre = stand + Vector3.Up * (ringRadius + tube);

            return new GridRing(centre, planeNormal, MathF.Max(ringRadius - tube, 0.2f), ringRadius + tube, halfWidth);
        }

        /// <summary>
        /// One quad of the landmark, corners given bottom-left, bottom-right, top-right, top-left as seen from
        /// outside — the same order and winding <see cref="AddTowerFace"/> builds, so the shared clockwise-from-
        /// outside convention holds here too. <paramref name="acrossSize"/> is the band's width, which is what
        /// the two long seams are measured against; the face-local X is pinned to the middle of a deliberately
        /// wide face so the segment joins never draw (see <see cref="BuildGridLandmark"/>).
        /// </summary>
        private static void AddGridQuad(List<GridTowerVertex> vertices, List<short> indices,
            Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector3 normal,
            float boardU0, float boardU1, float boardV0, float boardV1, float acrossSize)
        {
            const float NO_SEAM = 4096f;
            int baseIndex = vertices.Count;

            vertices.Add(new GridTowerVertex(bl, new Vector2(boardU0, boardV0), new Vector4(NO_SEAM * 0.5f, 0f, NO_SEAM, acrossSize), normal));
            vertices.Add(new GridTowerVertex(br, new Vector2(boardU1, boardV0), new Vector4(NO_SEAM * 0.5f, 0f, NO_SEAM, acrossSize), normal));
            vertices.Add(new GridTowerVertex(tr, new Vector2(boardU1, boardV1), new Vector4(NO_SEAM * 0.5f, acrossSize, NO_SEAM, acrossSize), normal));
            vertices.Add(new GridTowerVertex(tl, new Vector2(boardU0, boardV1), new Vector4(NO_SEAM * 0.5f, acrossSize, NO_SEAM, acrossSize), normal));

            indices.Add((short)baseIndex);
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 1));

            indices.Add((short)baseIndex);
            indices.Add((short)(baseIndex + 3));
            indices.Add((short)(baseIndex + 2));
        }

        //One quad face — BoxMesh.AddFace's own vertex order and winding (see its class doc). Instead of a normal
        //and a [0,1] texture UV it carries the face's place on its solid's board (boardOrigin at the face's
        //bottom-left corner, in world units) and its own local position and size, which the seams are measured
        //from. Works for a side face (up = Vector3.Up) or a roof (up = Vector3.Forward) alike, since
        //right x up = the outward normal either way (BoxMesh's own invariant).
        private static void AddTowerFace(List<GridTowerVertex> vertices, List<short> indices,
            Vector3 right, Vector3 up, float width, float height, Vector3 faceCenter, Vector2 boardOrigin)
        {
            Vector3 r = right * (width * 0.5f);
            Vector3 u = up * (height * 0.5f);

            //right x up is the outward normal — BoxMesh's own invariant, which is exactly why this one helper
            //serves a side face and a roof alike (#512).
            Vector3 normal = Vector3.Cross(right, up);

            int baseIndex = vertices.Count;

            vertices.Add(new GridTowerVertex(faceCenter - r - u, boardOrigin, new Vector4(0f, 0f, width, height), normal));
            vertices.Add(new GridTowerVertex(faceCenter + r - u, boardOrigin + new Vector2(width, 0f), new Vector4(width, 0f, width, height), normal));
            vertices.Add(new GridTowerVertex(faceCenter + r + u, boardOrigin + new Vector2(width, height), new Vector4(width, height, width, height), normal));
            vertices.Add(new GridTowerVertex(faceCenter - r + u, boardOrigin + new Vector2(0f, height), new Vector4(0f, height, width, height), normal));

            indices.Add((short)baseIndex);
            indices.Add((short)(baseIndex + 2));
            indices.Add((short)(baseIndex + 1));

            indices.Add((short)baseIndex);
            indices.Add((short)(baseIndex + 3));
            indices.Add((short)(baseIndex + 2));
        }

        /// <summary>
        /// Uploads one board to its texture — red the current generation, green the one before it, which
        /// <c>Grid.fx</c> fades as the phosphor's afterglow — through the board's own reused buffer, so a step
        /// allocates nothing (BestPractices §3, applied at this method's own, slower cadence).
        /// </summary>
        private static void UploadGridLifeTexture(GridLifeBoard board)
        {
            for (int y = 0; y < GridLife.SIZE; y++)
            {
                uint row = board.Life.Row(y);
                uint previous = board.Life.PreviousRow(y);

                for (int x = 0; x < GridLife.SIZE; x++)
                    board.UploadBuffer[y * GridLife.SIZE + x] = new Color((int)((row >> x) & 1u) * 255, (int)((previous >> x) & 1u) * 255, 0, 255);
            }

            board.Texture.SetData(board.UploadBuffer);
        }

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            //The Grid's rig is what the whole scene's light on the balls, the island and the gun comes
            //down to — see GridSceneConfig's own class doc on why nothing here goes further than a tint.
            GridLightingConfig gridLighting = _gridConfig.Lighting;
            rig = new SceneLightRig(
                gridLighting.SkyAmbient.ToVector3(),
                gridLighting.GroundAmbient.ToVector3(),
                gridLighting.KeyTint.ToVector3(),
                gridLighting.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //No landmark — the subject is the floor itself, so this reads the sea's own argument onto a
            //grid: low and close is what shows the lines raking off towards a vanishing point, where an
            //overhead look would flatten the whole pattern into a texture. Unphotographed: no shipped
            //level names this scene yet.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _gridConfig.Terrain.HorizonHazeDistance * 0.7f, _gridConfig.Terrain.LevelY),
                2.1f, 6f, 0f, "the grid");
            return true;
        }

        /// <summary>The solids as boxes; see <see cref="SceneRenderer.GridSolids"/>.</summary>
        public IReadOnlyList<GridSolid> Solids => _gridSolids;

        /// <summary>The landmark ring as a shape; see <see cref="SceneRenderer.TryGetGridRing"/>.</summary>
        public bool TryGetRing(out GridRing ring)
        {
            ring = _gridRing ?? default;
            return _gridRing.HasValue;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _gridTowerVertexBuffer?.Dispose();
            _gridTowerIndexBuffer?.Dispose();
            foreach (GridLifeBoard board in _gridLifeBoards) board.Texture?.Dispose();
        }
    }
}
