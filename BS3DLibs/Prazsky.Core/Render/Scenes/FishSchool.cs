using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The sea's school of fish (#760): the owner's "simple little fish as an easter egg" for a sea that can be looked
    /// at from under the water, seen in the Sea's chapter intro (its shot under the island, #759) and in the drop
    /// cinematic's dive. Drawn only with the lens under every trough of the swell (<c>SeaBackdrop.LensUnderEveryTrough</c>). A loose ring of small silver fish circling the funnel's cone under the island, the swirl the
    /// references drew (<c>C:\Users\panrd\AI\sd\out\760-klein</c>: a school wheeling under a floating platform's dark
    /// underside with the sun's light coming down past it).
    /// <para>
    /// <b>No CPU per fish.</b> Every fish is baked into one static buffer, each vertex carrying which fish it belongs to,
    /// and <c>Fish.fx</c> places, turns and swims each one off the clock and a hash of its index: one draw for the whole
    /// school. <b>Drawn only with the lens under the water</b>, so in play, where the lens never goes under, it costs
    /// nothing.
    /// </para>
    /// </summary>
    internal sealed class FishSchool : IDisposable
    {
        //How many fish, and how long one is, nose to the tail's tips. Small against a ball (one unit across), as
        //baitfish are against anything in the frame; the shader varies each by a quarter either way.
        private const int FISH_COUNT = 110;
        private const float FISH_LENGTH = 1.3f;

        //The ring they circle: round the funnel's cone and under the island's rim, the cone's tip a little under its
        //middle, so a lens looking up at the island sees the school between it and the stone.
        private const float SCHOOL_RADIUS_IN = 13f;
        private const float SCHOOL_RADIUS_OUT = 21f;
        private const float SCHOOL_Y = ArenaIsland.FUNNEL_BOTTOM_Y + 2f;
        private const float SCHOOL_DEPTH = 6f;

        //How fast they swim along the ring, in units a second, and how fast a tail beats. Unhurried: a lap of the
        //middle of the ring (17 out) is about 33 seconds.
        private const float SWIM_SPEED = 3.2f;
        private const float TAIL_HZ = 2.6f;

        //The fish's own colours (linear): a dark blue back over a silver belly, the countershading every pelagic fish
        //has and the concept sheets drew; and the flash a silver flank throws when it turns towards the light
        private static readonly Vector3 BACK_COLOR = new(0.015f, 0.03f, 0.07f);
        private static readonly Vector3 BELLY_COLOR = new(0.70f, 0.76f, 0.80f);
        private const float FLASH_STRENGTH = 3.0f;

        //How far a fish can be seen before the water takes it: a fish at this distance keeps a third of its own
        //colour. The underwater post applies one tint to the whole frame and has no depth (Tonemap.fx), so without
        //this the far side of the ring would be as crisp as the near.
        private const float FADE_DISTANCE = 60f;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly VertexBuffer _vertices;
        private readonly int _triangles;

        //The per-frame values, resolved once (BestPractices.md, section 1)
        private readonly EffectParameter _view, _projection, _time, _cameraPosition, _light, _waterColor;

        public FishSchool(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _effect = content.Load<Effect>("Shaders/Fish");

            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _time = _effect.Parameters["Time"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _light = _effect.Parameters["Light"];
            _waterColor = _effect.Parameters["WaterColor"];

            _effect.Parameters["SchoolCentre"].SetValue(new Vector3(0f, SCHOOL_Y, 0f));
            _effect.Parameters["SchoolRadii"].SetValue(new Vector2(SCHOOL_RADIUS_IN, SCHOOL_RADIUS_OUT));
            _effect.Parameters["SchoolDepth"].SetValue(SCHOOL_DEPTH);
            _effect.Parameters["SwimSpeed"].SetValue(SWIM_SPEED);
            _effect.Parameters["TailHz"].SetValue(TAIL_HZ);
            _effect.Parameters["FishLength"].SetValue(FISH_LENGTH);
            _effect.Parameters["BackColor"].SetValue(BACK_COLOR);
            _effect.Parameters["BellyColor"].SetValue(BELLY_COLOR);
            _effect.Parameters["FlashStrength"].SetValue(FLASH_STRENGTH);
            _effect.Parameters["FadeDistance"].SetValue(FADE_DISTANCE);

            List<(Vector3 Position, Vector3 Normal, float Fin)> fish = FishMesh.Build();
            FishVertex[] vertices = new FishVertex[fish.Count * FISH_COUNT];
            for (int f = 0; f < FISH_COUNT; f++)
                for (int v = 0; v < fish.Count; v++)
                    vertices[f * fish.Count + v] = new FishVertex(fish[v].Position, fish[v].Normal, new Vector4(f, fish[v].Fin, 0f, 0f));

            _vertices = new VertexBuffer(graphicsDevice, FishVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _vertices.SetData(vertices);
            _triangles = vertices.Length / 3;
        }

        /// <summary>
        /// Draws the school, opaque and writing depth, both faces (the fins are single sheets). <paramref name="water"/> is
        /// the colour a far fish fades into; the light is the scene's own sun and dome, as much of it as reaches down.
        /// </summary>
        public void Draw(in SceneFrame frame, Vector3 water)
        {
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _time.SetValue(frame.Time);
            _cameraPosition.SetValue(frame.Camera.Position);
            _light.SetValue(frame.SunColor * 0.5f + frame.ZenithLinear);
            _waterColor.SetValue(water);

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_vertices);
            _effect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangles);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        public void Dispose() => _vertices?.Dispose();
    }
}
