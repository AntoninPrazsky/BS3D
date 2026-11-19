using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Where the sea's lighthouse stands and where its two vessels sail (#1125), as pure functions of their clock: what
    /// <see cref="SeaTraffic"/> draws and what the tests hold. The owner: the sea is "still quite empty" apart from the oil
    /// platforms; the references (<c>C:\Users\panrd\AI\sd\out\1125-zimage</c>, <c>1125-klein</c>, <c>1125-flux3</c>) put a
    /// container ship on the horizon, a yacht nearer and a lighthouse on a low rock beside the platforms.
    /// <para>
    /// <b>All three stand about half as far out as the platforms</b> (#1170, the owner: the ships and the lighthouse "should also
    /// be closer to the island where the game is played"), where the surface stands still whoever looks
    /// (<c>SeaSceneConfig.StillAt</c>, 360): the swell's light plays on there, its heave does not, so a hull cut at a flat
    /// waterline neither stands over a trough nor before a crest.
    /// </para>
    /// <para>
    /// <b>The lighthouse</b> stands <see cref="LIGHTHOUSE_DISTANCE"/> out on <see cref="LIGHTHOUSE_BEARING"/>, about 16 degrees
    /// right of the cluster in the play view the Game and the Testbed open on (the lens on +Z looking towards -Z), in front of
    /// the farther platform and a little left of it.
    /// </para>
    /// <para>
    /// <b>The vessels sail two straight, parallel lanes</b> straight across the opening play view, each nearest the arena on
    /// <see cref="LANE_BEARING"/> (straight ahead of it, behind the cluster), the yacht's <see cref="YACHT_LANE"/> out and the
    /// ship's <see cref="SHIP_LANE"/>: the yacht just beyond the lighthouse and the ship beyond the yacht, both in front of both
    /// platforms. ⚠ <b>Not oblique, as they were 880 and 1010 out</b> (nearest on a bearing of -1.0, right of the farther
    /// platform): a lane that near on that bearing runs through the nearer platform. Square to the opening view they pass the
    /// nearer platform on its arena side, some 160 off its jacket, and the chapter intro's run at it, which flies a lens low over
    /// the water anywhere from 730 to 1070 out round its bearing (<c>SeaIntroShots</c>), 230 off any of its lenses
    /// (<c>SeaTrafficTests</c> hold both).
    /// </para>
    /// <para>
    /// <b>A lane is <see cref="LANE_HALF_LENGTH"/> either way of its nearest point</b>, and a vessel at its end wraps round to
    /// the other. It has gone under the horizon by then from every lens: <c>Rig.fx</c> sinks a vessel's part that stands past
    /// <see cref="SINK_FROM"/> from the lens by the square of how far past (<see cref="SINK_SCALE"/>), the curve of a real sea
    /// that takes a ship from its waterline up, and the lanes are long enough that the wrap is under the cut from any lens the
    /// game flies. What stands still, the platforms and the lighthouse, is drawn with <see cref="NO_SINK"/> and never moves,
    /// from any lens: the Testbed's and the editor's free lenses can stand far enough out that it would.
    /// </para>
    /// <para>
    /// <b>The ship comes in from the right and the yacht from the left</b> at the vessels' zero (<see cref="SHIP_START"/>,
    /// <see cref="YACHT_START"/>), and the Game sets that zero whenever it selects the sea (<see cref="SeaTrafficClock"/>): no
    /// shipped level plays on the sea, so a player comes to it through the Scene list minutes into a session, and on the
    /// seconds since launch the vessels would be anywhere on their lanes by then. The first minutes after the selection put
    /// both in the opening view, the ship passing behind the lighthouse at about 19 s and the yacht at about 71. After that
    /// each comes round once a lane: <see cref="SHIP_SPEED"/> (about ten knots) and <see cref="YACHT_SPEED"/> (about five)
    /// at two units a metre.
    /// </para>
    /// <para>
    /// <b>The yacht runs before the wind.</b> The sea's wind (<c>SeaSceneConfig.Wind</c>) blows across the opening view, so a
    /// lane across it is a run: the mainsail eased out to leeward and the jib winged out the other side
    /// (<see cref="YachtLeeward"/>). Into the wind no sail draws, and a beam reach would run its lane out to the arena.
    /// </para>
    /// </summary>
    public static class SeaTrafficLayout
    {
        /// <summary>The bearing both lanes are nearest the arena on (from +X towards +Z, as <c>OilRigLayout</c> counts): straight
        /// ahead of the opening play view, so the lanes run square across it.</summary>
        public const float LANE_BEARING = -MathHelper.PiOver2;

        /// <summary>How far out the yacht's lane and the ship's pass the arena at their nearest.</summary>
        public const float YACHT_LANE = 450f, SHIP_LANE = 520f;

        /// <summary>How far either way of its nearest point a lane runs before a vessel wraps round to its other end.</summary>
        public const float LANE_HALF_LENGTH = 3200f;

        /// <summary>The vessels' speeds, units a second: the ship's about ten knots and the yacht's about five. Whole numbers, so
        /// both lanes come round together (<c>SeaTrafficTests</c> count the time either is in the opening view over that).</summary>
        public const float SHIP_SPEED = 10f, YACHT_SPEED = 5f;

        //Where on its lane each is at the clock's zero (along LaneAlong from the nearest point): the ship at the opening view's
        //right edge, coming left, and the yacht left of its middle, going right
        private const float SHIP_START = 300f, YACHT_START = -250f;

        /// <summary>
        /// The lighthouse: its bearing, how far out it stands and its turn about the vertical. ⚠ Not nearer the frame's middle:
        /// 400 out, from a bearing of -1.31 in a ball of the tallest levels (Balloon) hangs over its lantern in the focused play
        /// view, and from -1.35 in the gun's wheel stands before its rock. The limits move with the distance: the nearer, the
        /// taller the tower stands over the horizon (<c>SeaTrafficTests</c> hold it in every level's play view).
        /// </summary>
        public const float LIGHTHOUSE_BEARING = -1.30f, LIGHTHOUSE_DISTANCE = 400f, LIGHTHOUSE_TURN = 0.4f;

        /// <summary>
        /// How far from the lens a vessel's part begins to sink under the horizon (<c>Rig.fx</c>), and how slowly: the drop is
        /// (distance - <see cref="SINK_FROM"/>)^2 / <see cref="SINK_SCALE"/>.
        /// </summary>
        public const float SINK_FROM = 1400f, SINK_SCALE = 3400f;

        /// <summary>
        /// The <c>SinkFrom</c> what stands still is drawn with, the platforms and the lighthouse: farther than any lens can
        /// stand, so they never sink. Not <see cref="SINK_FROM"/>: a Testbed or editor lens some 180 out from the arena on the
        /// side away from the farther platform puts that platform's far corner past it, and it would tilt into the sea.
        /// </summary>
        public const float NO_SINK = 1e9f;

        /// <summary>The way from the arena to both lanes' nearest points.</summary>
        public static Vector2 LaneNormal => new(MathF.Cos(LANE_BEARING), MathF.Sin(LANE_BEARING));

        /// <summary>Along the lanes, the normal turned a quarter towards a growing bearing: left to right in the opening view.</summary>
        public static Vector2 LaneAlong => new(-MathF.Sin(LANE_BEARING), MathF.Cos(LANE_BEARING));

        /// <summary>How far a part at <paramref name="distance"/> from the lens has sunk under the horizon (<c>Rig.fx</c>'s drop).</summary>
        public static float Sink(float distance)
        {
            float past = MathF.Max(distance - SINK_FROM, 0f);
            return past * past / SINK_SCALE;
        }

        /// <summary>The ship's frame <paramref name="time"/> seconds from the vessels' zero (<see cref="SeaTrafficClock"/>),
        /// standing on water at <paramref name="waterY"/>.</summary>
        public static Matrix ShipAt(float time, float waterY) =>
            VesselAt(Along(time, SHIP_START, -SHIP_SPEED), SHIP_LANE, -1f, waterY);

        /// <summary>The yacht's frame <paramref name="time"/> seconds from the vessels' zero (<see cref="SeaTrafficClock"/>),
        /// standing on water at <paramref name="waterY"/>.</summary>
        public static Matrix YachtAt(float time, float waterY) =>
            VesselAt(Along(time, YACHT_START, YACHT_SPEED), YACHT_LANE, 1f, waterY);

        /// <summary>Where along its lane a vessel is at <paramref name="time"/>, wrapped into the lane's length.</summary>
        internal static float Along(float time, float start, float speed)
        {
            //In double: hours into a session the product outgrows a float's step
            double span = 2.0 * LANE_HALF_LENGTH;
            double s = start + speed * (double)time + LANE_HALF_LENGTH;
            return (float)(s - Math.Floor(s / span) * span - LANE_HALF_LENGTH);
        }

        /// <summary>
        /// A vessel's frame <paramref name="along"/> its lane <paramref name="lane"/> out, heading along <see cref="LaneAlong"/>
        /// (<paramref name="heading"/> 1) or against it (-1): its own +X the way it goes, +Y up from the waterline and +Z to its
        /// starboard.
        /// </summary>
        internal static Matrix VesselAt(float along, float lane, float heading, float waterY)
        {
            Vector2 at = LaneNormal * lane + LaneAlong * along, way = LaneAlong * heading;
            Matrix frame = Matrix.CreateRotationY(MathF.Atan2(-way.Y, way.X));
            frame.Translation = new Vector3(at.X, waterY, at.Y);
            return frame;
        }

        /// <summary>The lighthouse's frame, standing on water at <paramref name="waterY"/>.</summary>
        public static Matrix Lighthouse(float waterY)
        {
            Matrix frame = Matrix.CreateRotationY(LIGHTHOUSE_TURN);
            frame.Translation = new Vector3(MathF.Cos(LIGHTHOUSE_BEARING) * LIGHTHOUSE_DISTANCE, waterY,
                MathF.Sin(LIGHTHOUSE_BEARING) * LIGHTHOUSE_DISTANCE);
            return frame;
        }

        /// <summary>
        /// Which side of the yacht the wind blows to, in its own frame: +1 starboard, -1 port. The mainsail is eased out on
        /// that side and the jib winged out on the other.
        /// </summary>
        public static float YachtLeeward(Vector2 wind)
        {
            Vector2 way = LaneAlong, starboard = new(-way.Y, way.X);
            return Vector2.Dot(wind, starboard) >= 0f ? 1f : -1f;
        }
    }

    /// <summary>
    /// The vessels' seconds (#1125): the wall clock less the moment the sea was last selected while it was off the screen.
    /// The Game selects it through <see cref="SceneRenderer.Prepare"/> wherever it sets its scene (the Scene list over the
    /// front end or the pause, a level, the start), so a player who comes to the sea minutes into a session sees the ship come
    /// in at the opening view's right edge and the yacht from its left, as a launch straight into it does. A selection while
    /// the sea is on the screen (drawn less than <see cref="ON_SCREEN"/> ago: the same scene chosen again) moves nothing.
    /// The Testbed and the editor never select it, so their vessels keep the wall clock, which <c>clock=</c> pins.
    /// </summary>
    public sealed class SeaTrafficClock
    {
        /// <summary>How long after its last draw, wall-clock seconds, the sea still counts as on the screen.</summary>
        public const float ON_SCREEN = 1f;

        private float _zero, _lastRead = float.NegativeInfinity;
        private bool _selected;

        /// <summary>The host has selected the sea: the vessels start their passage again at the next read, unless the sea
        /// is on the screen.</summary>
        public void Select() => _selected = true;

        /// <summary>The vessels' seconds at <paramref name="wallClock"/>; read by every draw of them.</summary>
        public float Read(float wallClock)
        {
            if (_selected && wallClock - _lastRead > ON_SCREEN) _zero = wallClock;
            _selected = false;
            _lastRead = wallClock;
            return wallClock - _zero;
        }
    }

    /// <summary>
    /// The lighthouse on its rock, the container ship and the yacht (#1125), shaded by <c>Rig.fx</c> as the oil platforms are
    /// and drawn after them: opaque, writing depth, cut at the waterline with foam where they meet it. One buffer holds the
    /// three meshes (<see cref="SeaTrafficMeshes"/>), each in its own frame, and each is one draw with its frame
    /// (<see cref="SeaTrafficLayout"/>): the vessels' are taken off their clock (<see cref="SeaTrafficClock"/>, the wall clock
    /// from the moment the sea was selected) every frame, so they sail on while the simulation is paused. Only the vessels
    /// sink under the horizon; the lighthouse is drawn with <see cref="SeaTrafficLayout.NO_SINK"/>. The Potato path draws no
    /// sea and so none of this.
    /// </summary>
    internal sealed class SeaTraffic : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly EffectParameter _world, _view, _projection, _cameraPosition, _sunDirection, _sunColor, _zenith, _horizon, _waterY, _sinkFrom;
        private readonly VertexBuffer _vertices;
        private readonly int _shipStart, _shipTriangles, _yachtStart, _yachtTriangles, _lighthouseTriangles;
        private readonly Matrix _lighthouse;
        private readonly SeaTrafficClock _clock = new();

        public SeaTraffic(GraphicsDevice graphicsDevice, ContentManager content, SeaSceneConfig config)
        {
            _graphicsDevice = graphicsDevice;
            _effect = OilRigs.LoadEffect(content);
            _world = _effect.Parameters["World"];
            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _sunDirection = _effect.Parameters["SunDirection"];
            _sunColor = _effect.Parameters["SunColor"];
            _zenith = _effect.Parameters["ZenithColor"];
            _horizon = _effect.Parameters["HorizonColor"];
            _waterY = _effect.Parameters["WaterY"];
            _sinkFrom = _effect.Parameters["SinkFrom"];

            List<RigVertex> lighthouse = SeaTrafficMeshes.Lighthouse();
            List<RigVertex> ship = SeaTrafficMeshes.ContainerShip();
            List<RigVertex> yacht = SeaTrafficMeshes.Yacht(SeaTrafficLayout.YachtLeeward(config.Wind.ToVector2()));
            _lighthouseTriangles = lighthouse.Count / 3;
            _shipStart = lighthouse.Count;
            _shipTriangles = ship.Count / 3;
            _yachtStart = _shipStart + ship.Count;
            _yachtTriangles = yacht.Count / 3;

            var all = new RigVertex[lighthouse.Count + ship.Count + yacht.Count];
            lighthouse.CopyTo(all, 0);
            ship.CopyTo(all, _shipStart);
            yacht.CopyTo(all, _yachtStart);
            _vertices = new VertexBuffer(graphicsDevice, RigVertex.Declaration, all.Length, BufferUsage.WriteOnly);
            _vertices.SetData(all);

            _lighthouse = SeaTrafficLayout.Lighthouse(config.LevelY);
        }

        /// <summary>The host has selected the sea: the vessels start their passage again (<see cref="SeaTrafficClock.Select"/>).</summary>
        public void Select() => _clock.Select();

        /// <summary>Draws the lighthouse and the two vessels where their clock has them, opaque and writing depth, after the
        /// water. Leaves the states as it found them.</summary>
        public void Draw(in SceneFrame frame, float waterY)
        {
            float time = _clock.Read(frame.Time);

            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(frame.SunColor);
            _zenith.SetValue(frame.ZenithLinear);
            _horizon.SetValue(frame.HorizonLinear);
            _waterY.SetValue(waterY);

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState rasterizer = _graphicsDevice.RasterizerState;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            //The sails are single sheets, read from either side (Rig.fx turns their normal to the lens)
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SetVertexBuffer(_vertices);

            //The lighthouse stands still and never sinks; the vessels go under the horizon before they wrap round
            DrawPart(_lighthouse, 0, _lighthouseTriangles, SeaTrafficLayout.NO_SINK);
            DrawPart(SeaTrafficLayout.ShipAt(time, waterY), _shipStart, _shipTriangles, SeaTrafficLayout.SINK_FROM);
            DrawPart(SeaTrafficLayout.YachtAt(time, waterY), _yachtStart, _yachtTriangles, SeaTrafficLayout.SINK_FROM);

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;
        }

        private void DrawPart(Matrix world, int start, int triangles, float sinkFrom)
        {
            _world.SetValue(world);
            _sinkFrom.SetValue(sinkFrom);
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, start, triangles);
        }

        public void Dispose() => _vertices?.Dispose();
    }
}
