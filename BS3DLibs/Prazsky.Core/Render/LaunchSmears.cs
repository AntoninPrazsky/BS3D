using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The launch smears: the colour streak a ball leaves at the muzzle as it fires, stretched along the shot
    /// and fading over a fraction of a second. A ball leaves the gun at some 200 units a second — several of
    /// its own diameters in a frame — so the shot itself is not something an eye can follow, and the smear is
    /// what sells how hard it left. One additive billboard per live smear, placed and shaded by
    /// <c>Shaders/ShotTrail.fx</c>, which carries the other half of this in its own header.
    /// <para>
    /// <b>The bright, wide end is the leading one, and that is the whole of the trap here.</b> The smear is
    /// anchored at the muzzle rather than following the ball (a shot attaches within ~0.075 s, far too brief
    /// to trail), and the muzzle end is mostly hidden behind the barrel — so a muzzle-bright streak shows
    /// only its faint tapering tip, reads as a thin thread, and looks like nothing happened. That is the
    /// mistake this replaced, and it is why <see cref="LEAD_WIDTH"/> is the larger width and the head is the
    /// far end: <see cref="Add"/> takes a muzzle and a direction and derives both ends itself, so the
    /// inversion cannot come back through a call site.
    /// </para>
    /// <para>
    /// It stood in the Testbed and the Game, every dial value-identical, until #76 — and the two had drifted
    /// in exactly the way BestPractices.md §1 describes. The Game cached its seven parameter references and
    /// set the two widths once; the Testbed re-sent both widths every frame (compile-time constants, and §1
    /// names "trail widths" as its own example of the waste) and looked five parameters up by name per frame
    /// plus four more <i>per smear</i>, each lookup a string scan of the effect's parameter collection. One
    /// copy now, on the cached side of that difference, with the pass handle cached too.
    /// </para>
    /// <para>
    /// <b>What deliberately stays with the callers.</b> <i>Which colour a ball type is</i>:
    /// <c>Prazsky.BS3D.BasicEffectParamsProvider</c> is a layer above this one, so <see cref="Add"/> takes
    /// the tint as an sRGB colour and turns it into the streak's radiance itself — the hue floor and the
    /// glare boost are here, where they cannot be got half-right, and only the type lookup is at the call
    /// site. <i>When to age</i>: <see cref="Update"/> is called from the Testbed only while its simulation
    /// runs (a paused Testbed holds its smears) and from the Game every frame. And <i>where in the frame</i>
    /// <see cref="Draw"/> goes, which is load-bearing in both — over the opaque scene, so the cluster, the
    /// gun and the island are in the depth buffer and hide what is behind them, and before any glass
    /// composites over the frame.
    /// </para>
    /// </summary>
    public sealed class LaunchSmears : IDisposable
    {
        #region The dials, one copy of each

        /// <summary>Seconds one smear lasts — long enough not to be missed.</summary>
        private const float LIFETIME = 0.45f;

        /// <summary>World length of the streak, from the muzzle along the shot.</summary>
        private const float LENGTH = 7f;

        /// <summary>
        /// Half-width at the leading (far) end: bright, wide and clear of the barrel. The larger of the two
        /// for the reason the class remarks give at length.
        /// </summary>
        private const float LEAD_WIDTH = 0.72f;

        /// <summary>Half-width at the muzzle end, which is mostly hidden behind the barrel.</summary>
        private const float MUZZLE_WIDTH = 0.42f;

        /// <summary>Radiance boost, over 1 on purpose so the streak glows and blooms through the glare.</summary>
        private const float BRIGHTNESS = 3.0f;

        /// <summary>
        /// Lowest peak channel a smear may have, so even the near-black ball leaves a faint grey streak
        /// rather than nothing at all.
        /// </summary>
        private const float COLOR_FLOOR = 0.12f;

        #endregion

        /// <summary>
        /// One live smear. Both ends are stored rather than a direction, because both are <b>fixed</b> for
        /// the smear's whole life — the ball is long past the head by the second frame — so the head is
        /// derived once in <see cref="Add"/> instead of per frame per smear in the draw walk. A struct in a
        /// <see cref="List{T}"/>, so a burst of shots allocates nothing beyond the list's own growth.
        /// </summary>
        private struct Smear
        {
            public Vector3 Muzzle;      //the tail: faint, narrow, mostly behind the barrel
            public Vector3 Head;        //the leading tip out in the open: bright and full width
            public Vector3 Radiance;    //linear, already hue-floored and boosted — the shader takes it as it is
            public float Age;
        }

        private readonly List<Smear> _smears = new();

        private readonly GraphicsDevice _device;

        //Cached at construction, per BestPractices.md §1: the by-name indexer is a string scan of the
        //effect's whole parameter collection, and these four go out once PER SMEAR.
        private readonly EffectParameter _headParam, _tailParam, _colorParam, _alphaParam;

        //And these three once per frame, for the same reason.
        private readonly EffectParameter _viewParam, _projectionParam, _cameraPositionParam;

        //The two widths, which are constants of this component but not of the effect it shares — see the
        //constructor on why they are pushed per frame rather than once.
        private readonly EffectParameter _headWidthParam, _tailWidthParam;

        //The one pass of the one technique, resolved once as well. Nothing here ever switches technique, so
        //walking CurrentTechnique.Passes[0] per smear was work with a known answer.
        private readonly EffectPass _pass;

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        /// <param name="shotTrailEffect">The compiled <c>Shaders/ShotTrail.fx</c>. Handed in and never
        /// disposed here: the caller's content manager owns its lifetime, exactly as the instancing effect is
        /// handed to <c>BallRenderSet</c>.</param>
        public LaunchSmears(GraphicsDevice device, Effect shotTrailEffect)
        {
            _device = device;

            _viewParam = shotTrailEffect.Parameters["View"];
            _projectionParam = shotTrailEffect.Parameters["Projection"];
            _cameraPositionParam = shotTrailEffect.Parameters["CameraPosition"];
            _headParam = shotTrailEffect.Parameters["TrailHead"];
            _tailParam = shotTrailEffect.Parameters["TrailTail"];
            _colorParam = shotTrailEffect.Parameters["TrailColor"];
            _alphaParam = shotTrailEffect.Parameters["TrailAlpha"];

            _pass = shotTrailEffect.CurrentTechnique.Passes[0];

            //These two used to be set right here, once, on the reasoning that a compile-time constant need not
            //be re-sent per frame — BestPractices.md §1's own example names these very two. That reasoning was
            //correct while this was the only component using the effect and stopped being correct the moment
            //AimBeam shared it: a parameter's value belongs to the EFFECT rather than to whoever set it, the two
            //want different widths, and whichever constructor ran last would have decided how both look. So they
            //go out once per Draw now, which is per frame per component and still not per primitive.
            _headWidthParam = shotTrailEffect.Parameters["TrailHeadWidth"];
            _tailWidthParam = shotTrailEffect.Parameters["TrailTailWidth"];

            CreateQuad();
        }

        /// <summary>
        /// Gives a shot its smear. The head is placed <see cref="LENGTH"/> along the aim from the muzzle and
        /// the streak then stays where it was put — it is a launch burst, not a tail.
        /// </summary>
        /// <param name="muzzle">Where the ball actually left the bore, so the streak starts on the ball the
        /// player was watching rather than at the barrel's pivot.</param>
        /// <param name="direction">The shot's direction, <b>unit</b>: it is what <see cref="LENGTH"/> is
        /// measured along, so a longer vector would make a longer smear. Both callers hand over the same
        /// normalised aim they threw the body along.</param>
        /// <param name="srgbTint">The ball's own diffuse tint, as authored — sRGB. Decoded to linear here,
        /// its hue kept but its peak lifted to <see cref="COLOR_FLOOR"/> so even the near-black ball leaves a
        /// faint grey smear, then boosted by <see cref="BRIGHTNESS"/> so the streak reads as energy and
        /// blooms through the glare. Done here rather than at the call sites because it is one rule about how
        /// a smear looks, and it was written out twice.</param>
        public void Add(Vector3 muzzle, Vector3 direction, Vector3 srgbTint)
        {
            Vector3 linear = ColorSpace.SrgbToLinear(srgbTint);

            float peak = MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
            if (peak < COLOR_FLOOR) linear *= COLOR_FLOOR / MathF.Max(peak, 1e-4f);

            _smears.Add(new Smear
            {
                Muzzle = muzzle,
                Head = muzzle + direction * LENGTH,
                Radiance = linear * BRIGHTNESS,
                Age = 0f
            });
        }

        /// <summary>
        /// Ages every live smear and drops the ones whose burst has faded. Walked backwards so a removal does
        /// not skip the next one; nothing is allocated, the enumerator included.
        /// </summary>
        /// <param name="elapsedSeconds">The frame's own elapsed time. Whether a frame counts at all is the
        /// caller's: the Testbed ages its smears only while its simulation is running, the Game every frame
        /// it updates.</param>
        public void Update(float elapsedSeconds)
        {
            for (int i = _smears.Count - 1; i >= 0; i--)
            {
                Smear smear = _smears[i];
                smear.Age += elapsedSeconds;

                if (smear.Age >= LIFETIME) _smears.RemoveAt(i);
                else _smears[i] = smear;
            }
        }

        /// <summary>Drops every live smear at once — a torn-down session has no shots in the air.</summary>
        public void Clear() => _smears.Clear();

        /// <summary>
        /// Every live smear, one billboard each. Additive and depth-read but writing no depth, like the
        /// campfire's flame: the streak glows and blooms through the glare pass, while the opaque scene in
        /// front of it still hides it.
        /// <para>
        /// <b>The GPU-state contract.</b> It states the three states it needs and puts back exactly what it
        /// found, the idiom <c>LaserGrid</c> uses — not fixed values that happen to suit whatever is drawn
        /// next. That distinction is load-bearing rather than tidy: what follows this call in both
        /// executables is <i>glass</i> (the drain's funnel, then the plate the cluster hangs from), which
        /// needs the frame's ordinary translucent baseline — and on the frames where no smear is live this
        /// method returns before touching anything, which is most frames. So the baseline the glass depends
        /// on can never have been this method's restore; it is the frame's, stated once before the scene,
        /// and all this may do is leave it as it was. The framework's cached statics throughout, never a
        /// fresh state object (BestPractices.md §2).
        /// </para>
        /// <para>
        /// Nothing is allocated and nothing is looked up by name; the four per-smear uniforms go out through
        /// the cached references and the shared quad is bound once for the whole walk.
        /// </para>
        /// </summary>
        public void Draw(ICamera camera)
        {
            if (_smears.Count == 0) return;

            _viewParam.SetValue(camera.View);
            _projectionParam.SetValue(camera.Projection);
            _cameraPositionParam.SetValue(camera.Position);

            //The smear's own taper, reclaimed from whatever else drew through this effect since — AimBeam shares
            //it and wants parallel sides. See the constructor.
            _headWidthParam.SetValue(LEAD_WIDTH);
            _tailWidthParam.SetValue(MUZZLE_WIDTH);

            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;
            RasterizerState raster = _device.RasterizerState;

            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullNone;

            _device.SetVertexBuffer(_vertexBuffer);
            _device.Indices = _indexBuffer;

            for (int i = 0; i < _smears.Count; i++)
            {
                Smear smear = _smears[i];

                //Held near-full for most of the life and dropped away at the end (1 - t²), so the smear
                //stays clearly visible instead of dimming the instant it appears — the point is that it not
                //be missed
                float t = smear.Age / LIFETIME;

                _headParam.SetValue(smear.Head);
                _tailParam.SetValue(smear.Muzzle);
                _colorParam.SetValue(smear.Radiance);
                _alphaParam.SetValue(1f - t * t);

                _pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            _device.BlendState = blend;
            _device.DepthStencilState = depth;
            _device.RasterizerState = raster;
        }

        /// <summary>
        /// The billboard: a unit quad whose texture channel carries (side in {-1,1}, along in {0 tail, 1
        /// head}); the shader places it in world space from each smear's two ends. The vertex positions are
        /// unused, so one shared quad serves every smear ever fired.
        /// </summary>
        private void CreateQuad()
        {
            VertexPositionTexture[] corners =
            {
                new(Vector3.Zero, new Vector2(-1f, 0f)), //tail, left
                new(Vector3.Zero, new Vector2(1f, 0f)),  //tail, right
                new(Vector3.Zero, new Vector2(-1f, 1f)), //head, left
                new(Vector3.Zero, new Vector2(1f, 1f))   //head, right
            };

            _vertexBuffer = new VertexBuffer(_device, VertexPositionTexture.VertexDeclaration, corners.Length,
                BufferUsage.WriteOnly);
            _vertexBuffer.SetData(corners);

            short[] indices = { 0, 1, 2, 2, 1, 3 };

            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        /// <summary>
        /// The shared quad's two buffers, which are everything this component made. <b>Not</b> the effect,
        /// which the caller's content manager owns — no reference to it is kept past the constructor, only
        /// the seven parameter handles and the one pass resolved out of it.
        /// </summary>
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = null;
            _indexBuffer = null;
        }
    }
}
