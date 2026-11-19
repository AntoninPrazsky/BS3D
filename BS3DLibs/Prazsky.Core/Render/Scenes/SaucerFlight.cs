using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The flying saucer in space (#955): one rigid craft on a slow loop far out round the station, a lathed hull with a ring
    /// of lights round its rim, a glass dome with its crew in it and a glow under its belly (<c>Saucer.fx</c>). Placed off
    /// the wall clock, so it flies on while the simulation is paused, and drawn after the sky's quad writing depth, so the
    /// station, the gun and the cluster stand in front of it where they are nearer the lens, and it in front of them where it is.
    /// <para>
    /// <b>Where it flies.</b> <see cref="ORBIT_RADIUS"/> out, well inside the camera's far plane
    /// (<see cref="Camera.BasicCamera3D.DEFAULT_FAR_PLANE_DISTANCE"/>), where from the play camera and the menu's orbit it is
    /// some fifty pixels across at 1080p: a thing noticed passing, not a prop that fights the cluster. No lens that films the
    /// station from round it comes near the loop by chance; <b>the one that meets the craft cues it</b> (#1117): the space
    /// chapter's intro opens on a still shot of the planet that the craft crosses close to the lens, and a
    /// <see cref="SaucerPass"/> turns the loop on to bring it there at the moment the shot is laid round, takes it off the loop
    /// for the crossing and eases it back after. Without a pass the loop is a pure function of the wall clock.
    /// </para>
    /// <para>
    /// <b>Two crafts by tier.</b> Low and Medium draw <see cref="SaucerMesh"/>'s two lathes, the bulbs and the crew painted on
    /// them; High and Ultra (<see cref="SceneRenderer.FineMeshes"/>, #1173, #1174) draw <see cref="SaucerFineMesh"/>'s parts:
    /// the hull with its seams, rim, belly ring and legs, the bulbs, the antenna and the cabin with its crew opaque here, and
    /// the dome's glass after the opaque scene (<see cref="DrawGlass"/>), so what stands behind it shows through it.
    /// </para>
    /// </summary>
    internal sealed class SaucerFlight : IDisposable
    {
        //The loop: its radius, its height over the world's origin and how far it rises and falls, how long one turn takes and
        //where on it the craft starts; the bank into the turn, a slow rock about it and the craft's spin about its own axis
        internal const float ORBIT_RADIUS = 240f;
        internal const float ORBIT_Y = 30f;
        internal const float ORBIT_BOB = 14f;
        private const float ORBIT_SECONDS = 70f;
        private const float ORBIT_PHASE = 2.2f;
        internal const float BANK = 0.22f;
        private const float ROCK = 0.06f;
        private const float SPIN = 0.8f;

        /// <summary>The loop's pace, units a second: its circumference over the seconds a turn takes.</summary>
        internal const float LOOP_SPEED = MathHelper.TwoPi * ORBIT_RADIUS / ORBIT_SECONDS;

        //The pass the intro cued (#1117), or none
        private SaucerPass _pass;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Effect _effect;
        private readonly EffectTechnique _hullTechnique, _domeTechnique;
        private readonly EffectTechnique _hullFineTechnique, _bulbTechnique, _antennaTechnique, _crewTechnique, _glassTechnique;
        private readonly EffectParameter _world, _view, _projection, _cameraPosition, _time, _sunDirection, _sunColor;
        private readonly EffectParameter _skyAmbient, _groundAmbient, _lampPosition, _glassSide;
        private readonly SaucerMesh _mesh;

        //High and Ultra's parts, by SaucerFineMesh.Part
        private readonly SaucerFineMesh[] _fine;

        //What this frame's opaque draw laid, for the glass after the opaque scene: whether it drew the fine craft, and where
        private bool _drewFine;
        private Matrix _drawnAt;

        public SaucerFlight(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _effect = content.Load<Effect>("Shaders/Saucer");
            _hullTechnique = _effect.Techniques["SaucerHull"];
            _domeTechnique = _effect.Techniques["SaucerDome"];
            _hullFineTechnique = _effect.Techniques["SaucerHullFine"];
            _bulbTechnique = _effect.Techniques["SaucerBulb"];
            _antennaTechnique = _effect.Techniques["SaucerAntenna"];
            _crewTechnique = _effect.Techniques["SaucerCrew"];
            _glassTechnique = _effect.Techniques["SaucerGlass"];
            _world = _effect.Parameters["World"];
            _view = _effect.Parameters["View"];
            _projection = _effect.Parameters["Projection"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _time = _effect.Parameters["Time"];
            _sunDirection = _effect.Parameters["SunDirection"];
            _sunColor = _effect.Parameters["SunColor"];

            _skyAmbient = _effect.Parameters["SkyAmbient"];
            _groundAmbient = _effect.Parameters["GroundAmbient"];
            _lampPosition = _effect.Parameters["LampPosition"];
            _glassSide = _effect.Parameters["GlassSide"];
            _effect.Parameters["RimRadius"].SetValue(SaucerMesh.RIM_RADIUS);
            _effect.Parameters["RimBand"].SetValue(SaucerMesh.RIM_BAND);
            _effect.Parameters["UnderY"].SetValue(SaucerMesh.UNDER_Y);
            _effect.Parameters["DomeSpan"].SetValue(new Vector2(SaucerMesh.DOME_BASE, SaucerMesh.DOME_HEIGHT));
            _effect.Parameters["TopRing"].SetValue(SaucerMesh.TOP_RING);
            _effect.Parameters["PanelGrooves"].SetValue(SaucerFineMesh.Grooves);
            _effect.Parameters["Panels"].SetValue((float)SaucerFineMesh.PANELS);

            _mesh = new SaucerMesh(graphicsDevice);
            var parts = Enum.GetValues<SaucerFineMesh.Part>();
            _fine = new SaucerFineMesh[parts.Length];
            foreach (SaucerFineMesh.Part part in parts) _fine[(int)part] = new SaucerFineMesh(graphicsDevice, part);
        }

        /// <summary>The loop's angle about the station at <paramref name="time"/>, radians, turned on by <paramref name="shift"/>.</summary>
        internal static float LoopAngle(float time, float shift) => ORBIT_PHASE + shift + time * MathHelper.TwoPi / ORBIT_SECONDS;

        /// <summary>Where the craft is and how it lies at <paramref name="time"/>: on its loop, banked into the turn, rocking,
        /// spinning about its own axis. The loop with no pass; the Potato path draws this.</summary>
        internal static Matrix WorldAt(float time) => WorldAt(time, default);

        /// <summary>
        /// Where the craft is and how it lies at <paramref name="time"/> with <paramref name="pass"/> cued (#1117): on its loop
        /// turned on by the pass's shift, and as far onto the pass's run as its <see cref="SaucerPass.Weight"/> says, there level
        /// and leaning its outer side down. With no pass (<c>default</c>), the loop as it always flew.
        /// </summary>
        internal static Matrix WorldAt(float time, in SaucerPass pass)
        {
            float angle = LoopAngle(time, pass.LoopShift);
            var centre = new Vector3(MathF.Cos(angle) * ORBIT_RADIUS, ORBIT_Y + ORBIT_BOB * MathF.Sin(time * 0.21f), MathF.Sin(angle) * ORBIT_RADIUS);
            float bank = BANK;

            float run = pass.Weight(time);
            if (run > 0f)
            {
                centre = Vector3.Lerp(centre, pass.RunAt(time), run);
                bank = MathHelper.Lerp(BANK, SaucerPass.LEAN, run);
            }

            //After the turn about Y by -angle the craft's own X points out from the station, so a turn about Z tips its outer
            //side up: banked into the turn
            Matrix world = Matrix.CreateRotationY(-time * SPIN)
                * Matrix.CreateRotationZ(bank + ROCK * MathF.Sin(time * 0.7f))
                * Matrix.CreateRotationY(-angle);
            world.Translation = centre;
            return world;
        }

        /// <summary>Cues <paramref name="pass"/> (#1117), replacing any before it; see <see cref="SaucerPass"/>.</summary>
        public void Cue(in SaucerPass pass) => _pass = pass;

        /// <summary>Draws the craft, opaque and writing depth, lit by the scene's own rig (<paramref name="lighting"/>, the
        /// config's, which a level may replace): <paramref name="fine"/> High and Ultra's parts (<see cref="SaucerFineMesh"/>)
        /// without their glass, which <see cref="DrawGlass"/> lays after the opaque scene, else Low and Medium's two lathes.
        /// Leaves the depth, blend and rasterizer states as it found them.</summary>
        public void Draw(in SceneFrame frame, SpaceLightingConfig lighting, bool fine)
        {
            _skyAmbient.SetValue(lighting.SkyAmbient.ToVector3());
            _groundAmbient.SetValue(lighting.GroundAmbient.ToVector3());
            _drawnAt = WorldAt(frame.Time, _pass);
            _drewFine = fine;
            _world.SetValue(_drawnAt);
            _lampPosition.SetValue(Vector3.Transform(new Vector3(0f, SaucerFineMesh.LAMP_Y, 0f), _drawnAt));
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _time.SetValue(frame.Time);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(frame.SunColor);

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState rasterizer = _graphicsDevice.RasterizerState;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            if (fine)
            {
                DrawMesh(_fine[(int)SaucerFineMesh.Part.Hull], _hullFineTechnique);
                DrawMesh(_fine[(int)SaucerFineMesh.Part.Bulbs], _bulbTechnique);
                DrawMesh(_fine[(int)SaucerFineMesh.Part.Antenna], _antennaTechnique);
                DrawMesh(_fine[(int)SaucerFineMesh.Part.Crew], _crewTechnique);
            }
            else
            {
                DrawMesh(_mesh.Hull, _hullTechnique);
                DrawMesh(_mesh.Dome, _domeTechnique);
            }

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;
        }

        /// <summary>
        /// High and Ultra's dome of glass (#1174), after the opaque scene and everything that writes depth, so the crew, the
        /// cabin and whatever stands beyond the craft show through it, and anything nearer the lens that writes depth covers
        /// it: premultiplied, reading the depth and writing none, its inside faces (the far side of the dome) first and its
        /// outside ones over them. Where <see cref="Draw"/> last put the craft, and nothing when that drew Low and Medium's
        /// craft, whose dome is opaque. Leaves the depth, blend and rasterizer states as it found them.
        /// <para>
        /// From the backdrop's overlay slot (<see cref="SceneRenderer.DrawOverlays"/>), the last of the scene, for the intro's
        /// pass: there the craft crosses in front of the station, and what the hosts draw after the setting (the cluster, the
        /// gun) must show through the glass, not be painted over it. The trade is in play and the menu's orbit: the
        /// translucent layers that write no depth and stand nearer the lens (the ceiling's pane, the drain's and the gun's
        /// glass, the aim beam, the smears, the blasts, the fireworks) are drawn before it and lie under it where they cross
        /// it, and seen through the ceiling's plate the glass is laid unbent over a hull the plate bends (the mismatch
        /// <see cref="SceneRenderer.DrawGrounded"/> exists to avoid, #641). Accepted, because there the craft is some fifty
        /// pixels across at 1080p and its dome a third of that.
        /// </para>
        /// </summary>
        public void DrawGlass()
        {
            if (!_drewFine) return;
            _world.SetValue(_drawnAt);

            BlendState blend = _graphicsDevice.BlendState;
            DepthStencilState depth = _graphicsDevice.DepthStencilState;
            RasterizerState rasterizer = _graphicsDevice.RasterizerState;
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            //The faces wound away from the lens are the dome's inside: culling the ones wound towards it leaves them
            SaucerFineMesh glass = _fine[(int)SaucerFineMesh.Part.Glass];
            _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
            _glassSide.SetValue(-1f);
            DrawMesh(glass, _glassTechnique);
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            _glassSide.SetValue(1f);
            DrawMesh(glass, _glassTechnique);

            _graphicsDevice.BlendState = blend;
            _graphicsDevice.DepthStencilState = depth;
            _graphicsDevice.RasterizerState = rasterizer;
        }

        private void DrawMesh(IProceduralMesh mesh, EffectTechnique technique)
        {
            _graphicsDevice.SetVertexBuffer(mesh.VertexBuffer);
            _graphicsDevice.Indices = mesh.IndexBuffer;
            _effect.CurrentTechnique = technique;
            technique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
        }

        public void Dispose()
        {
            _mesh?.Dispose();
            if (_fine != null) foreach (SaucerFineMesh part in _fine) part?.Dispose();
        }
    }
}
