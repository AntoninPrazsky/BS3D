using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One open-ground scene's camera grid and the pass that draws it (#580): the skeleton the polar icesheet,
    /// the desert and the outback each carried a copy of, identical but for their field names and the name of
    /// the time uniform. Snaps the grid to a cell round the camera (so the ground does not swim), pushes the
    /// camera, the sky and the clock, lets the frame's cloud deck at the effect, draws opaque and
    /// <c>CullNone</c> with the far fade stated first, then the far ring over the same effect, and restores the
    /// scene block's states.
    /// <para>
    /// <b>What varies between scenes is exactly what it takes</b>: the effect (its config already pushed by its
    /// owner, once at load), the grid's density and extent, and the time uniform's name (none, for a ground on
    /// which nothing moves). Its per-frame parameters are resolved once here (BestPractices §1) where the
    /// copies set them by name every frame. Mars, the mountains, the meadow and the forest joined it as they
    /// moved (#580). The volcano did not: its flank has always left the blend state as it found it, where this
    /// states opaque and restores alpha-blend, and the fountains draw straight after it. The sea joined it with
    /// what it has more wrapped round the call — the pool's radius set before and the depth-read state around
    /// it, since the pass touches no depth state. The beach draws two: its land, which is the pass exactly, and
    /// its lagoon over the sea's grid, the sea's shape with its clip as the hole radius. The savanna sets its fires'
    /// point lights on the effect before the call.
    /// </para>
    /// <para>
    /// <b>Mars was the first to bring something</b>: a reduced program on a coarser grid (#540), so the grid can
    /// be retaken at another density (<see cref="SetGrid"/>), and a second technique on the same effect (its
    /// moons), so it states the ground's technique as the current one before each <see cref="Draw"/> — which
    /// applies whatever technique is current, exactly as the copies did.
    /// </para>
    /// <para>
    /// The grid comes from the renderer's <see cref="TerrainGridCache"/> through
    /// <see cref="BackdropServices.AcquireGridMesh"/> and is never disposed here.
    /// </para>
    /// <para>
    /// <b>A grid in blocks is drawn the way its tier can afford, and always in its rows</b> (#1097; the whole of the why
    /// is <see cref="TerrainGridCache"/>'s). The technique the backdrop left current is the ground WITH its cuts (the
    /// island's hole, the bank's cover, whatever else the scene clips). Where the scene supersamples and the effect
    /// compiled <c>&lt;its name&gt;Depth</c>, the grid's depth is laid first with that and the ground shaded on it
    /// without writing depth, and the far ring the same way (<see cref="FarField.DrawRing"/>). Otherwise the grid is
    /// one pass: with <c>&lt;its name&gt;Open</c>, where the effect has one, for every block no cut reaches, and the
    /// technique itself for the others. An effect with neither twin (the sea, and every effect on OpenGL ES, where
    /// none is compiled) draws as it always did.
    /// </para>
    /// </summary>
    internal sealed class TerrainPass
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly FarField _farField;
        private readonly BackdropServices _services;

        //Not readonly: a scene whose reduced program runs on a coarser grid retakes it (SetGrid, #540)
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private int _indexCount;

        //A grid in row order is taken in blocks instead and drawn only where the lens looks (#1035): the grid closes round the
        //camera, and its vertex program ran on every vertex of it. Null for a near-first grid, which keeps its rings (#781)
        private TerrainGridCache.Grid _blocks;
        private readonly BoundingFrustum _frustum = new(Matrix.Identity);

        //Per technique the ground is drawn with, the passes of its two twins (#1097), either null where the effect has
        //none: resolved on a technique's first draw, because the by-name indexer is a linear scan (BestPractices 1). And
        //the blocks the island's cuts reach, for the one-pass draw with an Open twin
        private readonly Dictionary<EffectTechnique, GroundTwins> _twins = new();
        private readonly TerrainCuts _cuts = new();
        private readonly float _bankReach;

        //Every height a scene's vertex program lifts its ground to, and far more: a block's box spans them, so only the lens'
        //sides, near plane and far plane decide what is drawn (#1035). The Moon's grid takes the same band
        internal const float BLOCK_LOW_Y = -1000f, BLOCK_HIGH_Y = 3000f;
        private int _gridN;
        private readonly float _extent;
        private readonly bool _nearFirst;
        private float _cell;

        private readonly EffectParameter _originXZ, _islandHoleRadius, _view, _projection, _cameraPosition,
            _sunDirection, _zenithColor, _horizonColor, _time, _sunColor, _gridCell;

        /// <summary>The scene's terrain effect, loaded (and owned) by the backdrop that draws through this pass.</summary>
        public Effect Effect { get; }

        /// <summary>
        /// Takes a grid of <paramref name="gridN"/> vertices a side over <paramref name="extent"/> and resolves
        /// <paramref name="effect"/>'s per-frame parameters, <paramref name="timeParameter"/> being the name of
        /// the scene's own clock uniform (<c>PolarTime</c>, <c>DesertTime</c>, …), or null for a ground on which
        /// nothing moves (Mars). <paramref name="nearFirst"/> takes the grid with its quads in rings from the centre out
        /// (<see cref="TerrainGridCache"/>, #781), which only a program that never clips gains from.
        /// </summary>
        public TerrainPass(BackdropServices services, Effect effect, int gridN, float extent, string timeParameter, bool nearFirst = false)
        {
            _graphicsDevice = services.GraphicsDevice;
            _farField = services.FarField;
            _services = services;
            Effect = effect;
            _extent = extent;
            _cell = extent / (gridN - 1);
            _gridN = gridN;
            _nearFirst = nearFirst;

            //A scene that wears the island's bank as its ground turns its terrain away under it (BankCover.fxh): that cut
            //reaches further out than the hole
            _bankReach = effect.Parameters["BankCoverOn"] != null ? IslandBerm.COVER_RADIUS_MAX : 0f;

            Take(gridN);

            _originXZ = effect.Parameters["OriginXZ"];
            _islandHoleRadius = effect.Parameters["IslandHoleRadius"];
            _view = effect.Parameters["View"];
            _projection = effect.Parameters["Projection"];
            _cameraPosition = effect.Parameters["CameraPosition"];
            _sunDirection = effect.Parameters["SunDirection"];
            _zenithColor = effect.Parameters["ZenithColor"];
            _horizonColor = effect.Parameters["HorizonColor"];
            _time = timeParameter == null ? null : effect.Parameters[timeParameter];
            _sunColor = effect.Parameters["SunColor"];

            //The vertex spacing, for an effect that asks (the sea, #674: which waves its vertices can carry)
            _gridCell = effect.Parameters["GridCell"];
            _gridCell?.SetValue(_cell);
        }

        /// <summary>
        /// Retakes the grid at <paramref name="gridN"/> vertices a side when it is not that already — Mars's reduced
        /// program runs on a coarser one (#540), so this runs when the tier crosses <see cref="SceneRenderer.SceneDetail"/>'s
        /// line and never per frame.
        /// </summary>
        public void SetGrid(int gridN)
        {
            if (_gridN == gridN) return;

            //Given back rather than disposed: the grid cache owns it, and at full detail another scene draws the same one
            if (_blocks != null) _services.ReleaseBlockedGrid(_gridN, _extent);
            else _services.ReleaseGridMesh(_gridN, _extent, _nearFirst);
            Take(gridN);
            _gridN = gridN;
            _cell = _extent / (gridN - 1);
            _gridCell?.SetValue(_cell);
        }

        //The grid of gridN a side from the cache: in blocks for a row-order grid, the near-first one as it was
        private void Take(int gridN)
        {
            if (_nearFirst)
            {
                _blocks = null;
                _services.AcquireGridMesh(gridN, _extent, out _vertexBuffer, out _indexBuffer, out _indexCount, nearFirst: true);
                return;
            }

            _blocks = _services.AcquireBlockedGrid(gridN, _extent);
            _vertexBuffer = _blocks.Vertices;
            _indexBuffer = _blocks.Indices;
            _indexCount = _blocks.IndexCount;
        }

        /// <summary>
        /// How far the vertex program moves a vertex sideways, world units: every block's box grows by it, or a block lying
        /// just outside a side of the lens is skipped while a vertex of it swings into view, a notch in the ground at the
        /// frame's edge. Zero for a program that only lifts; the sea and the lagoon state their waves' (#1035's review).
        /// </summary>
        public float SidewaysReach { get; set; }

        /// <summary>
        /// Whether the grid and the far ring are drawn: measurement dials (the meadow's <c>meadow=</c> layers, #1028, and the
        /// mountain's <c>mountain=</c>, #1035), each read by <see cref="Draw"/> so either can be drawn alone; true for every other caller.
        /// </summary>
        public bool DrawsGrid { get; set; } = true;

        /// <inheritdoc cref="DrawsGrid"/>
        public bool DrawsFarRing { get; set; } = true;

        /// <summary>
        /// Draws the grid round the camera and the far ring past it, the island's footprint cut out to
        /// <paramref name="holeRadius"/>. Leaves the blend state alpha and the rasterizer culling
        /// counter-clockwise, which is what the scene block expects after every terrain. <paramref name="beforeGrid"/>
        /// draws first, through the same effect with the frame's parameters and the far fade already pushed and the
        /// grid's states current: the outback's formations (#967), which the grid stops on. <paramref name="blend"/> is what the
        /// grid is drawn with, opaque unless said: the tropical lagoon is premultiplied over the sand its shallows show (#968).
        /// </summary>
        public void Draw(in SceneFrame frame, float holeRadius, Action beforeGrid = null, BlendState blend = null)
        {
            float originX = MathF.Round(frame.Camera.Position.X / _cell) * _cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / _cell) * _cell;

            _originXZ.SetValue(new Vector2(originX, originZ));
            _islandHoleRadius.SetValue(holeRadius);
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);
            _zenithColor.SetValue(frame.ZenithLinear);
            _horizonColor.SetValue(frame.HorizonLinear);
            _time?.SetValue(frame.Time);
            _sunColor.SetValue(frame.SunColor);

            frame.ApplyClouds?.Invoke(Effect);

            _graphicsDevice.BlendState = blend ?? BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _farField.Begin(Effect, frame, _extent);
            beforeGrid?.Invoke();
            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;

            //The depth first where the scene supersamples (#1097, TerrainGridCache): where pixels are the cost, as the
            //meadow's patch does (#924). Only an opaque ground on the scene block's own depth state can be laid first; the
            //lagoon is blended and the sea is drawn depth-read, and neither effect has a depth twin to find
            EffectTechnique ground = Effect.CurrentTechnique;
            bool depthFirst = _services.SupersampleFactor > 1 && blend == null
                && _graphicsDevice.DepthStencilState == DepthStencilState.Default;

            if (DrawsGrid && _blocks != null)
            {
                _frustum.Matrix = frame.Camera.View * frame.Camera.Projection;

                GroundTwins twins = TwinsOf(ground);
                EffectPass depth = depthFirst ? twins.Depth : null;
                bool[] cut = depth == null && twins.Open != null
                    ? _cuts.Blocks(_blocks, originX, originZ, _extent, MathF.Max(holeRadius, _bankReach))
                    : null;
                TerrainGridCache.DrawSeenBlocks(_graphicsDevice, _blocks, _frustum, originX, originZ, _extent, BLOCK_LOW_Y, BLOCK_HIGH_Y,
                    SidewaysReach, ground.Passes[0], twins.Open, cut, depth);
            }
            else if (DrawsGrid)
            {
                ground.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indexCount / 3);
            }
            if (DrawsFarRing) _farField.DrawRing(Effect, new Vector2(originX, originZ), _extent, depthFirst);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        private GroundTwins TwinsOf(EffectTechnique ground)
        {
            if (!_twins.TryGetValue(ground, out GroundTwins twins))
            {
                twins = GroundTwins.Of(Effect, ground);
                _twins[ground] = twins;
            }

            return twins;
        }
    }

    /// <summary>
    /// The two twins a terrain effect may compile beside a technique its ground is drawn with (#1097,
    /// <see cref="TerrainGridCache"/>): <c>&lt;name&gt;Open</c>, the same ground without its cuts, and
    /// <c>&lt;name&gt;Depth</c>, its depth alone. Either is null where the effect has none, and both are on OpenGL ES,
    /// where no effect compiles them.
    /// </summary>
    internal readonly struct GroundTwins
    {
        /// <summary>What follows a technique's name in its Open twin's and in its Depth twin's.</summary>
        public const string OPEN = "Open", DEPTH = "Depth";

        public readonly EffectPass Open, Depth;

        private GroundTwins(EffectPass open, EffectPass depth)
        {
            Open = open;
            Depth = depth;
        }

        /// <summary>The twins of <paramref name="technique"/> in <paramref name="effect"/>: two by-name lookups, so once a technique and never a frame.</summary>
        public static GroundTwins Of(Effect effect, EffectTechnique technique) =>
            new(effect.Techniques[technique.Name + OPEN]?.Passes[0], effect.Techniques[technique.Name + DEPTH]?.Passes[0]);
    }
}
