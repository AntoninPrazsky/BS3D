using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The big top (#690), the twenty-first scene: <c>Circus.fx</c> over the renderer's shared full-screen quad — the
    /// canvas, the side wall, the seats, the ring, the king poles, the spotlights and the bulbs, all analytic — and then
    /// the DEPTH of what can stand in front of the arena, drawn with colour writes off: the floor, an annulus round the
    /// island, so a ball that falls past the rim vanishes into the sawdust, and the king poles, so a lens that passes
    /// outside one sees it in front of the island rather than behind it. See "The big top" in docs/scenes.md.
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
        public const int FOOTLIGHT_COUNT = 4;

        /// <summary>
        /// The pools the three spots holding on the island throw on its cap (the review of #690): the pass draws a spot's
        /// beam and its pool on the floor and the seats, but the island and the cluster are not the pass's to light, so
        /// those three beams ended in nothing. Each is a real light a few units over the spot's own wandering aim, moving
        /// with it, in the spot's colour - which is what the shared effect can give: a point light, not a cone. Four
        /// footlights and three pools are seven of <see cref="SceneLights.MaxLights"/>, the eighth left for a blast's
        /// flash.
        /// </summary>
        public const int POOL_COUNT = 3;

        //How far over the island's cap a pool's light hangs, how far it reaches and how strong it is against its spot
        private const float POOL_LIFT = 3f;
        private const float POOL_RANGE = 14f;
        private const float POOL_SHARE = 0.8f;

        //How far a lamp's apex (where its beam starts, at the lens) is from the pivot its rod hangs it by, on the ring:
        //matched by FIXTURE_BACK * 0.5 in Circus.fx, which is where the shader stands the rod. The lamp turns about the
        //middle of its can, as a lamp in a yoke does, so the rod meets the ring at every angle - it stood at the can's
        //middle with the apex on the ring until the review of #690, and slid off the ring as the lamp turned.
        private const float PIVOT_TO_APEX = 0.95f;

        /// <summary>How far a footlight reaches (the shared effect's (1 - d / range) squared): the drum strongly, and the
        /// cluster and the gun above it as a warm light from below, the way footlights light a stage.</summary>
        public const float FOOTLIGHT_RANGE = 60f;

        /// <summary>How many real lights the scene hands <see cref="SceneLights"/>: the footlights, then the pools.</summary>
        public int LightCount => FOOTLIGHT_COUNT + POOL_COUNT;

        //Where they stand: just inside the curb, a little over the sawdust, and their warm colour
        private const float FOOTLIGHT_INSET = 1.2f;
        private const float FOOTLIGHT_HEIGHT = 2.2f;
        private static readonly Vector3 FOOTLIGHT_COLOR = new(0.95f, 0.66f, 0.38f);

        //The floor's annulus: segments round, and how far past the wall it reaches so no edge of it is ever in view
        private const int FLOOR_SEGMENTS = 96;
        private const float FLOOR_REACH = 1.2f;

        //The king poles' depth: sides round each, and how much thinner than the drawn pole the depth one is, so the
        //depth never covers a pixel the pass shaded as something behind the pole
        private const int POLE_SIDES = 16;
        private const float POLE_DEPTH_SHARE = 0.96f;

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

        private readonly VertexBuffer _poleVertices;
        private readonly IndexBuffer _poleIndices;
        private readonly int _polePrimitives;

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
            BuildPoles(out _poleVertices, out _poleIndices, out _polePrimitives);
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
        private const float SPOT_REACH = 62f;

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
                SpotAim(i, time, out Vector3 pivot, out Vector3 target);
                Vector3 direction = Vector3.Normalize(target - pivot);

                _spotPositions[i] = pivot + direction * PIVOT_TO_APEX;
                _spotDirections[i] = direction;
                //The roving pair lands on pale sawdust and would burn it white at the island spots' strength
                _spotColors[i] = SPOT_TINTS[i] * lights.SpotIntensity * (i < 3 ? 1f : ROVING_SHARE);
            }
        }

        /// <summary>Spot <paramref name="i"/>'s pivot on the ring, and the point it aims at at <paramref name="time"/>.</summary>
        private void SpotAim(int i, float time, out Vector3 pivot, out Vector3 target)
        {
            CircusLightsConfig lights = _config.Lights;
            float bearing = MathHelper.ToRadians(SPOT_BEARINGS[i]);
            pivot = new Vector3(MathF.Cos(bearing) * lights.SpotRigRadius, lights.SpotRigY, MathF.Sin(bearing) * lights.SpotRigRadius);

            //EVERY LAMP TURNS (#690, the owner's ask): slowly, smoothly, never quite repeating, so the pools of light
            //drift across the scene and the beams with them - a show warming up, not a disco. Each aim is a point
            //moving on two incommensurate slow cycles of its own.
            float speed = lights.SweepSpeed;
            if (i < 3)
            {
                //Three hold on the island: their pools wander round its cap, one way round or the other, now nearer the
                //drain and now nearer the rim - on the stone between the two (the drain's mouth is fourteen across,
                //the cap twenty-six), where a pool can be seen; nearer the middle it lit the glass of the funnel
                float wander = bearing + time * speed * (i == 1 ? -0.55f : 0.45f);
                float reach = 19f + 4.5f * MathF.Sin(time * speed * 0.37f + i * 2.4f);
                target = new Vector3(MathF.Cos(wander) * reach, ArenaIsland.TOP_Y, MathF.Sin(wander) * reach);
            }
            else
            {
                //Two rove the ring and climb into the seats, round and out and back on their own phases
                float sweep = bearing + time * speed * (i == 3 ? 0.6f : -0.5f);
                float radius = 52f + 20f * MathF.Sin(time * speed * 0.29f + i * 1.7f);
                target = new Vector3(MathF.Cos(sweep) * radius, FloorOrSeatsY(radius), MathF.Sin(sweep) * radius);
            }
        }

        /// <summary>The height of what a roving spot lands on at <paramref name="radius"/>: the floor, or the rake of the seats.</summary>
        private float FloorOrSeatsY(float radius)
        {
            CircusSeatingConfig seats = _config.Seating;
            if (radius < seats.InnerRadius) return _config.Ring.FloorY;
            return _config.Ring.FloorY + seats.RowRise + (radius - seats.InnerRadius) * seats.RowRise / seats.RowDepth;
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

            //And the king poles'. The pass is behind everything, so without this a pole the lens stood outside of - the
            //front end's wide orbit passes the poles' radius, and a chapter intro may - was drawn BEHIND the island it
            //stood in front of (#690's review). With its depth the island fails the test where the pole is, and the
            //pole the pass already shaded there stays.
            if (_polePrimitives > 0)
            {
                _graphicsDevice.SetVertexBuffer(_poleVertices);
                _graphicsDevice.Indices = _poleIndices;
                _effect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _polePrimitives);
            }

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

        /// <summary>
        /// The king poles as closed-sided cylinders for the depth pass, from the floor to where each meets the canvas -
        /// the pass's own figures (<c>KingPoleRadius</c>, <c>KingPoleThickness</c>, the roof cone), built once.
        /// </summary>
        private void BuildPoles(out VertexBuffer vertices, out IndexBuffer indices, out int primitives)
        {
            CircusTentConfig tent = _config.Tent;
            int count = Math.Clamp(tent.KingPoleCount, 0, 6);
            primitives = count * POLE_SIDES * 2;
            vertices = null;
            indices = null;
            if (count == 0) return;

            //Where the canvas is at the poles' radius: r = a + b y over the roof, so y = (r - a) / b
            float roofB = (tent.CrownRadius - tent.WallRadius) / (tent.CrownY - tent.WallTopY);
            float roofA = tent.WallRadius - roofB * tent.WallTopY;
            float top = (tent.KingPoleRadius - roofA) / roofB;
            float bottom = _config.Ring.FloorY;
            float radius = tent.KingPoleThickness * POLE_DEPTH_SHARE;

            VertexPosition[] v = new VertexPosition[count * (POLE_SIDES + 1) * 2];
            short[] index = new short[primitives * 3];
            int vi = 0, ii = 0;

            for (int pole = 0; pole < count; pole++)
            {
                //The pass's own placement: (i + 0.5) of a turn over the count
                float angle = (pole + 0.5f) * MathHelper.TwoPi / count;
                Vector2 centre = new(MathF.Cos(angle) * tent.KingPoleRadius, MathF.Sin(angle) * tent.KingPoleRadius);
                int first = vi;

                for (int side = 0; side <= POLE_SIDES; side++)
                {
                    float a = side * MathHelper.TwoPi / POLE_SIDES;
                    float x = centre.X + MathF.Cos(a) * radius, z = centre.Y + MathF.Sin(a) * radius;
                    v[vi++] = new VertexPosition(new Vector3(x, bottom, z));
                    v[vi++] = new VertexPosition(new Vector3(x, top, z));
                }

                for (int side = 0; side < POLE_SIDES; side++)
                {
                    short b0 = (short)(first + side * 2), t0 = (short)(b0 + 1), b1 = (short)(b0 + 2), t1 = (short)(b0 + 3);
                    index[ii++] = b0; index[ii++] = t0; index[ii++] = b1;
                    index[ii++] = b1; index[ii++] = t0; index[ii++] = t1;
                }
            }

            vertices = new VertexBuffer(_graphicsDevice, typeof(VertexPosition), v.Length, BufferUsage.WriteOnly);
            vertices.SetData(v);
            indices = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, index.Length, BufferUsage.WriteOnly);
            indices.SetData(index);
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

        /// <summary>Real light <paramref name="index"/> of <see cref="LightCount"/> at <paramref name="time"/>: a footlight,
        /// then the pool of island spot <c>index - FOOTLIGHT_COUNT</c>, over its aim.</summary>
        public Vector3 LightPosition(int index, float time)
        {
            if (index < FOOTLIGHT_COUNT) return FootlightPosition(index);
            SpotAim(index - FOOTLIGHT_COUNT, time, out _, out Vector3 target);
            return target + new Vector3(0f, POOL_LIFT, 0f);
        }

        /// <summary>See <see cref="LightPosition"/>.</summary>
        public Vector3 LightColor(int index, float time) => index < FOOTLIGHT_COUNT
            ? FootlightColor(index, time)
            : SPOT_TINTS[index - FOOTLIGHT_COUNT] * _config.Lights.SpotIntensity * POOL_SHARE;

        /// <summary>See <see cref="LightPosition"/>.</summary>
        public float LightRange(int index) => index < FOOTLIGHT_COUNT ? FOOTLIGHT_RANGE : POOL_RANGE;

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
            _poleVertices?.Dispose();
            _poleIndices?.Dispose();
        }
    }
}
