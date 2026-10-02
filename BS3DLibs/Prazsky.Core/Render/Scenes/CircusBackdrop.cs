using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The big top (#690), the twenty-first scene: <c>Circus.fx</c> over the renderer's shared full-screen quad — the
    /// canvas, the side wall, the seats, the ring, the king poles, the spotlights and the bulbs, all analytic — and then
    /// the floor's depth, an annulus round the island drawn with colour writes off, so a ball that falls past the rim
    /// vanishes into the sawdust. See "The big top" in docs/scenes.md.
    /// </summary>
    internal sealed class CircusBackdrop : Backdrop
    {
        /// <summary>The spots, matched by <c>SPOT_COUNT</c> in <c>Circus.fx</c>.</summary>
        public const int SPOT_COUNT = 5;

        /// <summary>
        /// The footlights round the ring, matched by <c>FOOTLIGHT_COUNT</c> in <c>Circus.fx</c>: real lights
        /// (<see cref="SceneLights"/>) as well as lamps the pass draws, because the island's drum is a vertical wall and the
        /// key from the rig overhead grazes it - under the key alone it stood black against the ring. A ring of lamps
        /// lights it from every side the camera can orbit to.
        /// </summary>
        public const int FOOTLIGHT_COUNT = 6;

        /// <summary>How far a footlight reaches (the shared effect's (1 - d / range) squared): past the drum, short of the
        /// cluster's top.</summary>
        public const float FOOTLIGHT_RANGE = 60f;

        //Where they stand: just inside the curb, a little over the sawdust, and their warm colour
        private const float FOOTLIGHT_INSET = 1.2f;
        private const float FOOTLIGHT_HEIGHT = 2.2f;
        private static readonly Vector3 FOOTLIGHT_COLOR = new(0.95f, 0.66f, 0.38f);

        //The floor's annulus: segments round, and how far past the wall it reaches so no edge of it is ever in view
        private const int FLOOR_SEGMENTS = 96;
        private const float FLOOR_REACH = 1.2f;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly CircusSceneConfig _config = new();

        private readonly Effect _effect;
        private readonly EffectTechnique _floorDepthTechnique;
        private EffectTechnique _sceneTechnique;
        private readonly EffectTechnique _fullTechnique, _reducedTechnique;
        private readonly EffectParameter _viewRayBasis, _cameraPosition, _time, _pixelAngle, _worldViewProjection;
        private readonly EffectParameter _spotPosition, _spotDirection, _spotColor;

        //Written in place every frame: the two roving spots move, and a fresh array a frame would be an allocation on the
        //gameplay path (BestPractices.md)
        private readonly Vector3[] _spotPositions = new Vector3[SPOT_COUNT];
        private readonly Vector3[] _spotDirections = new Vector3[SPOT_COUNT];
        private readonly Vector3[] _spotColors = new Vector3[SPOT_COUNT];

        private readonly VertexBuffer _floorVertices;
        private readonly IndexBuffer _floorIndices;
        private float _floorInnerRadius = -1f;

        //Colour writes off: the floor pass writes depth and nothing anyone can see. Built once, never per frame.
        private static readonly BlendState DEPTH_ONLY = new() { ColorWriteChannels = ColorWriteChannels.None };

        /// <summary>Loads the effect, caches its per-frame parameters, pushes the config and builds the floor's annulus.</summary>
        public CircusBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;
            _effect = content.Load<Effect>("Shaders/Circus");

            _fullTechnique = _effect.Techniques["Circus"];
            _reducedTechnique = _effect.Techniques["CircusReduced"];
            _floorDepthTechnique = _effect.Techniques["CircusFloorDepth"];
            _sceneTechnique = _fullTechnique;

            _viewRayBasis = _effect.Parameters["ViewRayBasis"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _time = _effect.Parameters["CircusTime"];
            _pixelAngle = _effect.Parameters["PixelAngle"];
            _worldViewProjection = _effect.Parameters["WorldViewProjection"];
            _spotPosition = _effect.Parameters["SpotPosition"];
            _spotDirection = _effect.Parameters["SpotDirection"];
            _spotColor = _effect.Parameters["SpotColor"];

            ApplyParameters();

            _floorVertices = new VertexBuffer(_graphicsDevice, typeof(VertexPosition), (FLOOR_SEGMENTS + 1) * 2, BufferUsage.WriteOnly);
            _floorIndices = BuildFloorIndices();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Circus;

        /// <inheritdoc/>
        public override SceneConfig Config => _config;

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            //The reduced program drops the beams, the one part of the pass that integrates five cones per pixel
            _sceneTechnique = sceneDetail > 0.5f ? _fullTechnique : _reducedTechnique;
        }

        private void ApplyParameters()
        {
            CircusTentConfig tent = _config.Tent;
            _effect.Parameters["WallRadius"].SetValue(tent.WallRadius);
            _effect.Parameters["WallTopY"].SetValue(tent.WallTopY);
            _effect.Parameters["CrownRadius"].SetValue(tent.CrownRadius);
            _effect.Parameters["CrownY"].SetValue(tent.CrownY);
            _effect.Parameters["PanelCount"].SetValue((float)tent.PanelCount);
            _effect.Parameters["PanelSag"].SetValue(tent.PanelSag);
            _effect.Parameters["CanvasRed"].SetValue(tent.Red.ToVector3());
            _effect.Parameters["CanvasCream"].SetValue(tent.Cream.ToVector3());
            _effect.Parameters["CurtainColor"].SetValue(tent.Curtain.ToVector3());
            _effect.Parameters["KingPoleCount"].SetValue((float)Math.Clamp(tent.KingPoleCount, 0, 6));
            _effect.Parameters["KingPoleRadius"].SetValue(tent.KingPoleRadius);
            _effect.Parameters["KingPoleThickness"].SetValue(tent.KingPoleThickness);
            _effect.Parameters["PoleColor"].SetValue(tent.PoleColor.ToVector3());

            CircusRingConfig ring = _config.Ring;
            _effect.Parameters["FloorY"].SetValue(ring.FloorY);
            _effect.Parameters["CurbRadius"].SetValue(ring.CurbRadius);
            _effect.Parameters["CurbHeight"].SetValue(ring.CurbHeight);
            _effect.Parameters["CurbThickness"].SetValue(ring.CurbThickness);
            _effect.Parameters["CurbRed"].SetValue(ring.CurbRed.ToVector3());
            _effect.Parameters["CurbGold"].SetValue(ring.CurbGold.ToVector3());
            _effect.Parameters["SawdustColor"].SetValue(ring.Sawdust.ToVector3());
            _effect.Parameters["FloorColor"].SetValue(ring.Floor.ToVector3());

            CircusSeatingConfig seats = _config.Seating;
            _effect.Parameters["SeatInner"].SetValue(seats.InnerRadius);
            _effect.Parameters["SeatOuter"].SetValue(seats.OuterRadius);
            _effect.Parameters["RowDepth"].SetValue(seats.RowDepth);
            _effect.Parameters["RowRise"].SetValue(seats.RowRise);
            _effect.Parameters["SeatRed"].SetValue(seats.SeatRed.ToVector3());
            _effect.Parameters["SeatWood"].SetValue(seats.Wood.ToVector3());
            _effect.Parameters["AisleCount"].SetValue((float)seats.AisleCount);
            _effect.Parameters["AisleWidth"].SetValue(seats.AisleWidth);
            _effect.Parameters["EntranceBearing"].SetValue(MathHelper.ToRadians(seats.EntranceBearingDegrees));
            _effect.Parameters["EntranceWidth"].SetValue(seats.EntranceWidth);

            CircusLightsConfig lights = _config.Lights;
            float half = MathHelper.ToRadians(lights.SpotHalfAngleDegrees);
            _effect.Parameters["SpotCosOuter"].SetValue(MathF.Cos(half * 1.15f));
            _effect.Parameters["SpotCosInner"].SetValue(MathF.Cos(half * 0.85f));
            _effect.Parameters["SpotReach"].SetValue(SPOT_REACH);
            _effect.Parameters["BeamStrength"].SetValue(lights.BeamStrength);
            _effect.Parameters["BulbStrings"].SetValue((float)Math.Clamp(lights.BulbStrings, 0, 24));
            _effect.Parameters["BulbSpacing"].SetValue(lights.BulbSpacing);
            _effect.Parameters["BulbSag"].SetValue(lights.BulbSag);
            _effect.Parameters["BulbColor"].SetValue(lights.BulbColor.ToVector3());
            _effect.Parameters["HazeColor"].SetValue(lights.HazeColor.ToVector3());
            _effect.Parameters["HazeDensity"].SetValue(lights.HazeDensity);
            _effect.Parameters["HouseLight"].SetValue(lights.HouseLight.ToVector3());
            _effect.Parameters["FootlightInset"].SetValue(FOOTLIGHT_INSET);
            _effect.Parameters["FootlightHeight"].SetValue(FOOTLIGHT_HEIGHT);
            _effect.Parameters["FootlightColor"].SetValue(FOOTLIGHT_COLOR);
        }

        //How bright the two roving spots are against the three on the island
        private const float ROVING_SHARE = 0.55f;

        //The distance a spot's pool has its stated intensity at: about the rig's height over the floor, so a spot aimed
        //straight down lands at the intensity the config states
        private const float SPOT_REACH = 66f;

        //The five spots: where on the rig each hangs (bearing, degrees), its colour, and what it aims at. The first three
        //hold on the island, from three sides, so it stands in a pool of crossed light; the last two rove the ring.
        private static readonly float[] SPOT_BEARINGS = { 20f, 140f, 260f, 80f, 320f };
        private static readonly Vector3[] SPOT_TINTS =
        {
            new(1.00f, 0.90f, 0.74f),
            new(1.00f, 0.62f, 0.30f),
            new(1.00f, 0.42f, 0.62f),
            new(0.50f, 0.76f, 1.00f),
            new(1.00f, 0.86f, 0.62f),
        };

        /// <summary>Where the spots hang and what they aim at this instant, off the wall clock.</summary>
        private void UpdateSpots(float time)
        {
            CircusLightsConfig lights = _config.Lights;

            for (int i = 0; i < SPOT_COUNT; i++)
            {
                float bearing = MathHelper.ToRadians(SPOT_BEARINGS[i]);
                Vector3 position = new(MathF.Cos(bearing) * lights.SpotRigRadius, lights.SpotRigY, MathF.Sin(bearing) * lights.SpotRigRadius);

                Vector3 target;
                if (i < 3)
                {
                    //On the island: a little off its centre towards the spot's own side, on its cap
                    target = new Vector3(MathF.Cos(bearing) * 6f, ArenaIsland.TOP_Y, MathF.Sin(bearing) * 6f);
                }
                else
                {
                    //Roving the ring: a slow sweep round the sawdust between the island and the curb, each on its own phase
                    float sweep = time * lights.SweepSpeed * (i == 3 ? 1f : -0.8f) + i * 2.1f;
                    float radius = 33f + 4f * MathF.Sin(time * lights.SweepSpeed * 1.7f + i);
                    target = new Vector3(MathF.Cos(sweep) * radius, _config.Ring.FloorY, MathF.Sin(sweep) * radius);
                }

                _spotPositions[i] = position;
                _spotDirections[i] = Vector3.Normalize(target - position);
                //The roving pair lands on pale sawdust and would burn it white at the island spots' strength
                _spotColors[i] = SPOT_TINTS[i] * lights.SpotIntensity * (i < 3 ? 1f : ROVING_SHARE);
            }
        }

        /// <summary>
        /// Draws the big top: the analytic pass over the shared quad with the depth state off, then the floor's annulus
        /// writing depth only. Everything animated — the roving spots, the bulbs' faint flicker — runs off the frame's
        /// wall-clock time, so the tent keeps living while the simulation is paused.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            UpdateSpots(frame.Time);

            _viewRayBasis.SetValue(SkyRay.Basis(frame.Camera));
            _cameraPosition.SetValue(frame.Camera.Position);
            _time.SetValue(frame.Time);
            _pixelAngle.SetValue(2f / MathF.Max(frame.Camera.Projection.M22 * _graphicsDevice.Viewport.Height, 1f));
            _spotPosition.SetValue(_spotPositions);
            _spotDirection.SetValue(_spotDirections);
            _spotColor.SetValue(_spotColors);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(Services.FullScreenQuad);
            _effect.CurrentTechnique = _sceneTechnique;
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            //The floor's depth, round the island's own hole: rebuilt only when the hole changes, which is a level change
            if (_floorInnerRadius != Services.TerrainHoleRadius) FillFloor(Services.TerrainHoleRadius);

            _graphicsDevice.BlendState = DEPTH_ONLY;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _worldViewProjection.SetValue(frame.Camera.View * frame.Camera.Projection);
            _graphicsDevice.SetVertexBuffer(_floorVertices);
            _graphicsDevice.Indices = _floorIndices;
            _effect.CurrentTechnique = _floorDepthTechnique;
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, FLOOR_SEGMENTS * 2);

            //Space's rule (SpaceBackdrop.Draw): the rest of the frame expects these
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        private void FillFloor(float innerRadius)
        {
            float outer = _config.Tent.WallRadius * FLOOR_REACH;
            float y = _config.Ring.FloorY;
            VertexPosition[] vertices = new VertexPosition[(FLOOR_SEGMENTS + 1) * 2];

            for (int i = 0; i <= FLOOR_SEGMENTS; i++)
            {
                float angle = i * MathHelper.TwoPi / FLOOR_SEGMENTS;
                float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
                vertices[i * 2] = new VertexPosition(new Vector3(cos * innerRadius, y, sin * innerRadius));
                vertices[i * 2 + 1] = new VertexPosition(new Vector3(cos * outer, y, sin * outer));
            }

            _floorVertices.SetData(vertices);
            _floorInnerRadius = innerRadius;
        }

        private IndexBuffer BuildFloorIndices()
        {
            //Culling is off for the pass, so the winding is the convention's only for its own sake
            short[] indices = new short[FLOOR_SEGMENTS * 6];
            for (int i = 0; i < FLOOR_SEGMENTS; i++)
            {
                short a = (short)(i * 2), b = (short)(i * 2 + 1), c = (short)(i * 2 + 2), d = (short)(i * 2 + 3);
                indices[i * 6 + 0] = a;
                indices[i * 6 + 1] = c;
                indices[i * 6 + 2] = b;
                indices[i * 6 + 3] = b;
                indices[i * 6 + 4] = c;
                indices[i * 6 + 5] = d;
            }

            IndexBuffer buffer = new(_graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            buffer.SetData(indices);
            return buffer;
        }

        /// <summary>Where footlight <paramref name="index"/> stands - the same place the pass draws its lamp.</summary>
        public Vector3 FootlightPosition(int index)
        {
            float bearing = (index + 0.5f) * MathHelper.TwoPi / FOOTLIGHT_COUNT;
            float radius = _config.Ring.CurbRadius - FOOTLIGHT_INSET;
            return new Vector3(MathF.Cos(bearing) * radius, _config.Ring.FloorY + FOOTLIGHT_HEIGHT, MathF.Sin(bearing) * radius);
        }

        /// <summary>Footlight <paramref name="index"/>'s colour at <paramref name="time"/>: steady, as stage lamps are, but for
        /// a breath so slight it is only life.</summary>
        public Vector3 FootlightColor(int index, float time) =>
            FOOTLIGHT_COLOR * (0.96f + 0.04f * MathF.Sin(time * 0.7f + index * 2.3f));

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            CircusLightingConfig lighting = _config.Lighting;
            rig = new SceneLightRig(lighting.SkyAmbient.ToVector3(), lighting.GroundAmbient.ToVector3(),
                lighting.KeyTint.ToVector3(), lighting.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetSunDirection(out Vector3 direction)
        {
            //The key comes from the spot rig overhead, not from a sun nobody can see through the canvas
            CircusLightingConfig lighting = _config.Lighting;
            direction = SceneRenderer.DirectionFromElevationAzimuth(
                Math.Clamp(lighting.KeyElevationDegrees, 1f, 89f), lighting.KeyAzimuthDegrees);
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The performers' entrance across the ring, with the island and its cluster in front of it and the seats and
            //the bulbs round it. Kept inside the king poles' ring: this pass is drawn behind everything, so a lens that
            //stood behind a pole would see the pole drawn behind the island it ought to hide.
            float entrance = MathHelper.ToRadians(_config.Seating.EntranceBearingDegrees);
            Vector3 gate = new(MathF.Cos(entrance) * _config.Tent.WallRadius, _config.Ring.FloorY + 8f,
                MathF.Sin(entrance) * _config.Tent.WallRadius);
            viewpoint = new SceneViewpoint(gate, 1.1f, 10f, 180f, "the ring");
            return true;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _floorVertices.Dispose();
            _floorIndices.Dispose();
        }
    }
}
