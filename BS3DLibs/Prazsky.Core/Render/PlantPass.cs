using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Draws a list of <see cref="ScatterBucket"/>s through the one plant material every planted scene shares
    /// (<c>Acacia.fx</c>, loaded once by the content manager), lit from the frame's sun and dome, and casts them into the
    /// sun's map. Out of <see cref="MeadowBackdrop"/> (#609) when the forest and the mountains took the island's bank
    /// too (#608): three backdrops drawing buckets the same way is one pass, not three copies of it. The savanna keeps
    /// its own draw, which carries its fires and its per-part parameters.
    /// <para>
    /// The effect is one instance for every scene that loads it, so everything a scene may differ in (its haze
    /// distance above all) is stated every frame, never once at load.
    /// </para>
    /// </summary>
    internal sealed class PlantPass
    {
        private readonly GraphicsDevice _graphicsDevice;

        /// <summary>The shared plant effect, for a backdrop to list among its shadow receivers.</summary>
        public Effect Effect { get; }

        private readonly EffectTechnique _technique, _shadowTechnique;
        private readonly EffectParameter _view, _projection, _camera, _sunDirection, _sunColor, _zenith, _horizon,
            _diffuse, _diffuseDry, _dapple, _bark, _leaves, _addedLight, _haze, _shadowViewProjection;

        public PlantPass(GraphicsDevice device, ContentManager content)
        {
            _graphicsDevice = device;
            Effect = content.Load<Effect>("Shaders/Acacia");
            _view = Effect.Parameters["View"];
            _projection = Effect.Parameters["Projection"];
            _camera = Effect.Parameters["CameraPosition"];
            _sunDirection = Effect.Parameters["SunDirection"];
            _sunColor = Effect.Parameters["SunColor"];
            _zenith = Effect.Parameters["ZenithColor"];
            _horizon = Effect.Parameters["HorizonColor"];
            _diffuse = Effect.Parameters["DiffuseColor"];
            _diffuseDry = Effect.Parameters["DiffuseDry"];
            _dapple = Effect.Parameters["DappleStrength"];
            _bark = Effect.Parameters["BarkStrength"];
            _leaves = Effect.Parameters["LeafStrength"];
            _addedLight = Effect.Parameters["AddedLight"];
            _haze = Effect.Parameters["HorizonHazeDistance"];
            _shadowViewProjection = Effect.Parameters["ShadowViewProjection"];
            _technique = Effect.Techniques["Acacia"];
            _shadowTechnique = Effect.Techniques["ShadowCaster"];
        }

        /// <summary>
        /// Draws <paramref name="buckets"/> lit from the frame, hazed to <paramref name="hazeDistance"/>; at
        /// <paramref name="detail"/> the full tier's buckets, below it the Low tier's.
        /// </summary>
        public void Draw(in SceneFrame frame, IReadOnlyList<ScatterBucket> buckets, float hazeDistance, bool detail) =>
            Draw(frame, buckets, hazeDistance, detail, frame.SunColor, frame.ZenithLinear, frame.HorizonLinear);

        /// <summary>
        /// <see cref="Draw(in SceneFrame, IReadOnlyList{ScatterBucket}, float, bool)"/> under lights the scene states
        /// itself rather than the dome's (#646): the aurora's ground is lit by the glow of its sky, and what stands on
        /// that ground has to be lit by the same glow or it glares.
        /// </summary>
        public void Draw(in SceneFrame frame, IReadOnlyList<ScatterBucket> buckets, float hazeDistance, bool detail,
            Vector3 sunColor, Vector3 zenith, Vector3 horizon)
        {
            Effect.CurrentTechnique = _technique;
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _camera.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(sunColor);
            _zenith.SetValue(zenith);
            _horizon.SetValue(horizon);
            _haze.SetValue(hazeDistance);
            _addedLight.SetValue(Vector3.Zero);
            _leaves.SetValue(0f);

            //⚠ PUT BACK WHAT WAS FOUND (#667). This pass leaves the states the frame goes on drawing under, and it
            //used to leave Opaque: on the meadow and the mountains it is the last thing the environment draws, so
            //every translucent surface after it came out solid there and nowhere else - the gun's window pane read
            //as an opaque grey sheet over a queue it was meant to show, and the drain's glass as a dark disc.
            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState raster = _graphicsDevice.RasterizerState;

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;

            for (int i = 0; i < buckets.Count; i++)
            {
                ScatterBucket bucket = buckets[i];
                if (bucket.DetailOnly && !detail) continue;
                if (bucket.LowOnly && detail) continue;

                _diffuse.SetValue(bucket.Diffuse);
                _diffuseDry.SetValue(bucket.DiffuseDry);
                _dapple.SetValue(bucket.Dapple);
                _bark.SetValue(bucket.Bark);
                _leaves.SetValue(bucket.Leaves);
                _graphicsDevice.RasterizerState = bucket.Leaves > 0f ? RasterizerState.CullNone : RasterizerState.CullCounterClockwise;
                Effect.CurrentTechnique.Passes[0].Apply();
                DrawBucket(bucket);
            }

            _leaves.SetValue(0f);
            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = raster;
        }

        /// <summary>Casts <paramref name="buckets"/> into the sun's map (#469's path); the Low tier's crowns do not cast.</summary>
        public void DrawShadowCasters(Matrix shadowViewProjection, IReadOnlyList<ScatterBucket> buckets)
        {
            Effect.CurrentTechnique = _shadowTechnique;
            _shadowViewProjection.SetValue(shadowViewProjection);

            for (int i = 0; i < buckets.Count; i++)
            {
                ScatterBucket bucket = buckets[i];
                if (bucket.LowOnly) continue;
                _leaves.SetValue(bucket.Leaves);
                Effect.CurrentTechnique.Passes[0].Apply();
                DrawBucket(bucket);
            }

            _leaves.SetValue(0f);
            Effect.CurrentTechnique = _technique;
        }

        private void DrawBucket(ScatterBucket bucket)
        {
            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(bucket.Mesh.VertexBuffer, 0, 0),
                new VertexBufferBinding(bucket.Instances, 0, 1));
            _graphicsDevice.Indices = bucket.Mesh.IndexBuffer;
            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, bucket.Mesh.PrimitiveCount, bucket.Count);
        }
    }
}
