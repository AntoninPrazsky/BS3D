using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The winners' podium on Top Players</b> (#827, the owner's decision 3): a stepped podium (<see cref="WinnersPodiumMesh"/>)
    /// with the result screen's cups standing on it, gold on the middle and highest step, silver on the left, bronze on the
    /// right (<see cref="TrophyPodium.DrawTier"/>), drawn by the page into the sharp foreground layer (#856's device).
    /// <para>
    /// <b>Placed against the page, not the world</b>, as the result screen's cup is placed against the frame: the page hands in
    /// the rectangle its tree laid the podium out in, and the podium is fitted to it at <see cref="DEPTH"/> in front of the lens,
    /// through the camera's own projection, so it holds its place at any resolution, aspect and field of view while the scene
    /// turns behind the page. It stands still, faces the lens and leans its top <see cref="TILT"/> towards it, so the steps'
    /// tops and the cups' feet read.
    /// </para>
    /// <para>
    /// <b>The cups are smaller than the result screen's and animated differently</b> (the issue's own words): no spin, bob,
    /// lean or dolly, each turns slowly on its step as on a turntable, in a phase of its own, and they arrive one by one from
    /// #3 to #1 with the reveal's overshoot, #1 last. Only the places the period has carry a cup.
    /// </para>
    /// <para>
    /// The names are not drawn here: the page writes them over the layer at <see cref="TryNameAnchor"/>, the steps' fronts as
    /// the last draw projected them, in the menu's own type.
    /// </para>
    /// </summary>
    internal sealed class WinnersPodium : IDisposable
    {
        //How far in front of the lens the podium stands, in world units: any distance fits the area through the projection,
        //and this one keeps the near plane far behind it
        private const float DEPTH = 6f;

        //How far its top leans towards the lens, in radians: enough to see the steps' tops and the cups standing on them
        private const float TILT = 0.2f;

        //How much of the area's width the podium takes, and of its height the podium and the tallest cup together
        private const float WIDTH_SHARE = 0.94f, HEIGHT_SHARE = 0.92f;

        //A cup's height against its step's top width: a little under the width, so the handles stay on the step
        private const float CUP_SHARE = 0.8f;

        //The reveal: each cup's arrival, the gap between two, and how far past its size it throws
        private const float REVEAL_SECONDS = 0.6f, REVEAL_GAP = 0.35f, OVERSHOOT = 0.35f;

        //The turntable: radians a second, and each place's own phase so no two cups face the same way
        private const float TURN_RATE = 0.6f;
        private static readonly float[] PHASES = { 0f, 2.1f, 4.2f };

        //Which of TrophyPodium's tiers stands on which place: gold, silver, bronze (the Diamond tier is not used here)
        private static readonly int[] TIER_OF_PLACE = { 0, 3, 2, 1 };

        //How much of the scene's environment each finish returns: the lacquer a varnish's glint, the gold the cups' own
        //(TrophyPodium's GOLD), so the trim and the gold cup on it are one metal
        private const float BODY_SPECULAR_AMBIENT = 0.35f, TRIM_SPECULAR_AMBIENT = 0.95f;

        private readonly GraphicsDevice _device;
        private readonly TrophyPodium _cups;
        private readonly WinnersPodiumMesh _bodyMesh, _trimMesh;
        private readonly InstancedModelRenderer _body, _trim;
        private readonly BasicEffectParams _bodyMaterial, _trimMaterial;

        private float _clock;

        //Where the last draw put each place's name (index = place), how wide its step and how tall its panel, in back-buffer pixels
        private readonly Vector2[] _nameAt = new Vector2[4];
        private readonly float[] _nameRoom = new float[4], _nameHeight = new float[4];
        private readonly bool[] _nameShown = new bool[4];

        /// <summary>The podium's renderers, for the host's lighting (<c>ApplySkyLighting</c>): the cups are the trophy's own.</summary>
        public IEnumerable<InstancedModelRenderer> Renderers
        {
            get
            {
                yield return _body;
                yield return _trim;
            }
        }

        public WinnersPodium(GraphicsDevice device, Effect instancingEffect, float ambientIntensity, TrophyPodium cups)
        {
            _device = device;
            _cups = cups;
            Vector3 ambient = Vector3.One * ambientIntensity;

            _bodyMesh = new WinnersPodiumMesh(device, WinnersPodiumMesh.Part.Body);
            _trimMesh = new WinnersPodiumMesh(device, WinnersPodiumMesh.Part.Trim);

            _body = new InstancedModelRenderer(device, _bodyMesh, WinnersPodiumMesh.BODY_DIFFUSE, instancingEffect)
            {
                SpecularAmbientStrength = BODY_SPECULAR_AMBIENT,
            };
            _bodyMaterial = new BasicEffectParams(ambient, WinnersPodiumMesh.BODY_SPECULAR, WinnersPodiumMesh.BODY_POWER, Vector3.Zero);

            _trim = new InstancedModelRenderer(device, _trimMesh, WinnersPodiumMesh.TRIM_DIFFUSE, instancingEffect)
            {
                PolishedMetal = true,
                SpecularAmbientStrength = TRIM_SPECULAR_AMBIENT,
            };
            _trimMaterial = new BasicEffectParams(ambient, WinnersPodiumMesh.TRIM_SPECULAR, WinnersPodiumMesh.TRIM_POWER, Vector3.Zero);
        }

        /// <summary>The cups arrive again from #3: the page was entered, or the period's three changed.</summary>
        public void Begin() => _clock = 0f;

        public void Update(float elapsed) => _clock += MathF.Max(0f, elapsed);

        /// <summary>
        /// Draws the podium fitted to <paramref name="area"/> (back-buffer pixels of a <paramref name="bufferWidth"/> by
        /// <paramref name="bufferHeight"/> frame), with cups on its first <paramref name="places"/> places (0 to 3), and notes
        /// where each name goes. The host has bound the layer and stated the scene's states.
        /// </summary>
        public void Draw(ICamera camera, Rectangle area, int bufferWidth, int bufferHeight, int places)
        {
            Array.Clear(_nameShown);
            if (area.Width <= 0 || area.Height <= 0) return;

            Matrix projection = camera.Projection;
            Matrix toWorld = Matrix.Invert(camera.View);

            //The area at DEPTH, in the lens's own space: its centre and its half extents
            float centreX = area.Center.X / (float)bufferWidth * 2f - 1f;
            float centreY = 1f - area.Center.Y / (float)bufferHeight * 2f;
            float halfWidth = DEPTH / projection.M11 * (area.Width / (float)bufferWidth);
            float halfHeight = DEPTH / projection.M22 * (area.Height / (float)bufferHeight);

            //The composition is the podium and the tallest cup on it: fitted whole into the area, centred in it
            float tallest = WinnersPodiumMesh.StepTop(1).Y + CUP_SHARE * WinnersPodiumMesh.StepTopWidth(1);
            float scale = MathF.Min(2f * halfWidth * WIDTH_SHARE / WinnersPodiumMesh.WIDTH, 2f * halfHeight * HEIGHT_SHARE / tallest);

            Matrix world = Matrix.CreateTranslation(0f, -tallest * 0.5f, 0f)
                * Matrix.CreateScale(scale)
                * Matrix.CreateRotationX(TILT)
                * Matrix.CreateTranslation(centreX * DEPTH / projection.M11, centreY * DEPTH / projection.M22, -DEPTH)
                * toWorld;

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            _body.Draw(camera, world, _bodyMaterial);
            _trim.Draw(camera, world, _trimMaterial);

            Matrix viewProjection = camera.View * projection;
            for (int place = 1; place <= 3; place++)
            {
                //The name's place: the step's front, wherever it lands in the frame, and the step's width there
                Vector3 front = Vector3.Transform(WinnersPodiumMesh.StepFront(place), world);
                Vector4 clip = Vector4.Transform(new Vector4(front, 1f), viewProjection);
                if (clip.W > 0f)
                {
                    _nameAt[place] = new Vector2((clip.X / clip.W * 0.5f + 0.5f) * bufferWidth, (0.5f - clip.Y / clip.W * 0.5f) * bufferHeight);
                    //The step's whole top width, not the panel's: a name may run over the panel's gold frame onto the lacquer
                    //beside it, which is dark too, before it is cut
                    _nameRoom[place] = WinnersPodiumMesh.StepTopWidth(place) * scale / (2f * halfWidth) * area.Width;
                    _nameHeight[place] = WinnersPodiumMesh.PLAQUE_HEIGHT * scale / (2f * halfHeight) * area.Height;
                    _nameShown[place] = true;
                }

                if (place > places) continue;

                //#3 first, #1 last: the place's turn in the reveal counts down from the last place there is
                float reveal = Math.Clamp((_clock - (places - place) * REVEAL_GAP) / REVEAL_SECONDS, 0f, 1f);
                if (reveal <= 0f) continue;

                float eased = MathHelper.SmoothStep(0f, 1f, reveal);
                float height = CUP_SHARE * WinnersPodiumMesh.StepTopWidth(place) * eased * (1f + OVERSHOOT * MathF.Sin(MathF.PI * eased));

                Matrix cup = Matrix.CreateScale(height / TrophyMesh.HEIGHT)
                    * Matrix.CreateRotationY(_clock * TURN_RATE + PHASES[place - 1])
                    * Matrix.CreateTranslation(WinnersPodiumMesh.StepTop(place))
                    * world;
                _cups.DrawTier(camera, TIER_OF_PLACE[place], cup);
            }
        }

        /// <summary>
        /// Where the last draw put <paramref name="place"/>'s name, the centre of its step's name panel
        /// (<see cref="WinnersPodiumMesh.StepFront"/>), how wide its step stood and how tall its panel, in pixels.
        /// </summary>
        public bool TryNameAnchor(int place, out Vector2 at, out float room, out float height)
        {
            bool shown = place is >= 1 and <= 3 && _nameShown[place];
            at = shown ? _nameAt[place] : Vector2.Zero;
            room = shown ? _nameRoom[place] : 0f;
            height = shown ? _nameHeight[place] : 0f;
            return shown;
        }

        public void Dispose()
        {
            _body?.Dispose();
            _trim?.Dispose();
            _bodyMesh?.Dispose();
            _trimMesh?.Dispose();
        }
    }
}
