using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using System;
using System.Threading.Tasks;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The cavern (#958): an irregular limestone hall over a still black lake, every surface of it real geometry
    /// (<see cref="CavernChamber"/>, <see cref="CavernGeometry"/>) shaded by <c>Cavern.fx</c>. The hall is built on a
    /// worker thread when a host first selects the scene (<see cref="Prepare"/>), and each detail's arrays when that detail
    /// is first wanted; the buffers are made from them on the frame thread and the arrays let go. Under supersampling it is shaded at the display's resolution into a multisampled
    /// target of its own and scaled up, the formations' depth then written into the scene's own target where one can stand
    /// in front of the arena. See "The cavern" in docs/scenes.md.
    /// </summary>
    internal sealed class CavernBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly CavernSceneConfig _config = new();
        private readonly int _seed;
        private Task<CavernChamber> _chamberBuild;
        private CavernChamber _chamber;
        private Task<CavernMeshes.Arrays> _fineBuild, _coarseBuild;
        private CavernMeshes _fine, _coarse;

        private readonly Effect _effect;
        private readonly EffectTechnique _rock, _rockReduced, _rockMirrored, _formation, _formationReduced, _formationMirrored,
            _crystal, _crystalMirrored, _glowworm, _shaft, _depth;
        private readonly EffectParameter _viewProjection, _cameraPosition, _time, _viewportSize, _lightPosition, _lightColor,
            _lightCount, _reflection, _reflectionAvailable, _glowwormPixels;

        private const int MAX_LIGHTS = 12;
        private readonly Vector4[] _lightPositions = new Vector4[MAX_LIGHTS];
        private readonly Vector4[] _lightColors = new Vector4[MAX_LIGHTS];

        /// <summary>A cluster's light reaches this far, scaled by its size; the inverse square has done most of its work by then.</summary>
        private const float LIGHT_RANGE = 150f;

        /// <summary>A cluster's light, at the shader's own falloff scale, over its colour: the pool on the rock round it.</summary>
        private const float LIGHT_INTENSITY = 3.4f;

        /// <summary>A glowworm's radius on the screen, in pixels at 1080 lines (#507: a point stays about a pixel and a half wide).</summary>
        private const float GLOWWORM_PIXELS = 1.6f;

        //The mirrored hall the lake reflects, half the bound target's size each way: a reflection on a rippled lake is
        //never sharper than that, and it is a quarter of the pixels
        private RenderTarget2D _reflectionTarget;
        private readonly RenderTargetBinding[] _bound = new RenderTargetBinding[8];

        private bool _fullDetail = true;
        private bool _wroteSceneDepth;
        private CavernMeshes _drawn;

        //The formations' depth (#755): the box round the arena the pass is scissored to
        private const float ARENA_BOUND_RADIUS = ArenaIsland.RADIUS + 4f;
        private const float ARENA_BOUND_BOTTOM = ArenaIsland.FUNNEL_BOTTOM_Y - 1f;
        private const float ARENA_BOUND_TOP = 40f;
        private static readonly RasterizerState SCISSORED = new() { CullMode = CullMode.None, ScissorTestEnable = true };
        private static readonly BlendState DEPTH_ONLY = new() { ColorWriteChannels = ColorWriteChannels.None };

        //A crystal keeps what is behind it by its transmittance and adds its own light: One, SourceAlpha. The alpha the
        //scene target carries is left as it was
        private static readonly BlendState CRYSTAL_BLEND = new()
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.SourceAlpha,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
        };

        private static readonly BlendState ADDITIVE = new()
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
        };

        /// <summary>Loads the effect, caches its parameters and pushes the config at it. The hall is not built yet.</summary>
        public CavernBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;
            _effect = content.Load<Effect>("Shaders/Cavern");

            _rock = _effect.Techniques["Rock"];
            _rockReduced = _effect.Techniques["RockReduced"];
            _rockMirrored = _effect.Techniques["RockMirrored"];
            _formation = _effect.Techniques["Formation"];
            _formationReduced = _effect.Techniques["FormationReduced"];
            _formationMirrored = _effect.Techniques["FormationMirrored"];
            _crystal = _effect.Techniques["Crystal"];
            _crystalMirrored = _effect.Techniques["CrystalMirrored"];
            _glowworm = _effect.Techniques["Glowworm"];
            _shaft = _effect.Techniques["Shaft"];
            _depth = _effect.Techniques["Depth"];

            _viewProjection = _effect.Parameters["ViewProjection"];
            _cameraPosition = _effect.Parameters["CameraPosition"];
            _time = _effect.Parameters["CavernTime"];
            _viewportSize = _effect.Parameters["ViewportSize"];
            _lightPosition = _effect.Parameters["LightPosition"];
            _lightColor = _effect.Parameters["LightColor"];
            _lightCount = _effect.Parameters["LightCount"];
            _reflection = _effect.Parameters["Reflection"];
            _reflectionAvailable = _effect.Parameters["ReflectionAvailable"];
            _glowwormPixels = _effect.Parameters["GlowwormPixels"];

            ApplyParameters();

            _seed = services.SeedOffset;
        }

        /// <summary>
        /// Starts building the hall on a worker thread, and the arrays of the detail the renderer draws now, if they are not
        /// built or building: a host calls it when it selects the scene (<see cref="SceneRenderer.Prepare"/>), and every
        /// query that needs the hall calls it too. Nothing is built for a launch that never shows the cavern.
        /// </summary>
        public override void Prepare()
        {
            if (_chamberBuild == null)
            {
                CavernSceneConfig config = _config;
                int seed = _seed;
                _chamberBuild = Task.Run(() =>
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    var chamber = new CavernChamber(config, seed);
                    Console.WriteLine($"[cavern] hall placed off the frame in {clock.ElapsedMilliseconds} ms: {chamber.Stalactites.Count} stalactites, " +
                        $"{chamber.Stalagmites.Count} stalagmites, {chamber.Columns.Count} columns, {chamber.Clusters.Count} crystal clusters, " +
                        $"{chamber.Glowworms.Count} glowworms");
                    return chamber;
                });
            }

            StartDetail(Services.FineMeshes);
        }

        //The arrays of one detail, built off the frame once the hall is placed; null once their buffers are made
        private void StartDetail(bool fine)
        {
            if (fine ? (_fine != null || _fineBuild != null) : (_coarse != null || _coarseBuild != null)) return;

            Task<CavernChamber> chamberBuild = _chamberBuild;
            CavernGeometry.Detail detail = fine ? CavernGeometry.Detail.Fine : CavernGeometry.Detail.Coarse;
            Task<CavernMeshes.Arrays> build = chamberBuild.ContinueWith(placed =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                CavernMeshes.Arrays arrays = CavernMeshes.Arrays.Build(placed.Result, detail);
                Console.WriteLine($"[cavern] {(fine ? "fine" : "coarse")} meshes built off the frame in {clock.ElapsedMilliseconds} ms: " +
                    $"rock {arrays.Rock.Vertices.Length} vertices, formations {arrays.Formations.Indices.Length / 3} triangles");
                return arrays;
            }, TaskScheduler.Default);

            if (fine) _fineBuild = build;
            else _coarseBuild = build;
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Cavern;

        /// <inheritdoc/>
        public override SceneConfig Config => _config;

        /// <summary>True: under supersampling the hall is shaded at the display's resolution, into a multisampled target
        /// with a depth buffer, and scaled up — see <see cref="SceneRenderer"/>'s <c>DrawBackdropAtDisplayResolution</c>.</summary>
        public override bool DrawsAtDisplayResolution => true;

        /// <summary>Four samples: the formations' silhouettes keep the edges supersampling would have given them.</summary>
        public override int DisplayResolutionMultiSampleCount => 4;

        /// <inheritdoc/>
        public override bool DisplayResolutionDepth => true;

        /// <summary>The hall, waiting for its placement if it has not finished (and starting it if no host has).</summary>
        public CavernChamber Chamber
        {
            get
            {
                if (_chamber == null)
                {
                    Prepare();
                    _chamber = _chamberBuild.GetAwaiter().GetResult();
                }
                return _chamber;
            }
        }

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail) => _fullDetail = sceneDetail > 0.5f;

        private void ApplyParameters()
        {
            CavernRockConfig rock = _config.Rock;
            _effect.Parameters["RockColor"].SetValue(rock.RockColor.ToVector3());
            _effect.Parameters["CalciteColor"].SetValue(rock.CalciteColor.ToVector3());
            _effect.Parameters["FogColor"].SetValue(rock.FogColor.ToVector3());
            _effect.Parameters["FogDensity"].SetValue(rock.FogDensity);
            _effect.Parameters["AmbientTop"].SetValue(_config.Lighting.SkyAmbient.ToVector3() * 2.2f);

            CavernWaterConfig water = _config.Water;
            _effect.Parameters["WaterLevelY"].SetValue(water.LevelY);
            _effect.Parameters["WaterDeepColor"].SetValue(water.DeepColor.ToVector3());
            _effect.Parameters["WaterAbsorption"].SetValue(water.Absorption);
            _effect.Parameters["WaterGlowColor"].SetValue(water.GlowColor.ToVector3());
            _effect.Parameters["RippleStrength"].SetValue(water.RippleStrength);
            _effect.Parameters["RippleScale"].SetValue(water.RippleScale);
            _effect.Parameters["RippleSpeed"].SetValue(water.RippleSpeed);
            _effect.Parameters["MistColor"].SetValue(water.MistColor.ToVector3());
            _effect.Parameters["MistDensity"].SetValue(water.MistDensity);
            _effect.Parameters["MistHeight"].SetValue(water.MistHeight);

            CavernAirConfig air = _config.Air;
            _effect.Parameters["GlowwormColor"].SetValue(air.GlowwormColor.ToVector3());
            _effect.Parameters["ShaftColor"].SetValue(air.ShaftColor.ToVector3());
            _effect.Parameters["ShaftStrength"].SetValue(air.ShaftStrength);

            CavernCrystalConfig crystals = _config.Crystals;
            _effect.Parameters["CrystalColorA"].SetValue(crystals.ColorA.ToVector3());
            _effect.Parameters["CrystalColorB"].SetValue(crystals.ColorB.ToVector3());
            _effect.Parameters["CrystalEmission"].SetValue(crystals.Emission);
            _effect.Parameters["CrystalPulseSpeed"].SetValue(crystals.PulseSpeed);
        }

        //The shafts come out of the hall's build, so they are pushed once it is in
        private bool _chamberPushed;

        private void PushChamber(CavernChamber chamber)
        {
            var source = new Vector4[2];
            var direction = new Vector4[2];
            for (int s = 0; s < 2; s++)
            {
                if (s < chamber.Shafts.Count)
                {
                    CavernShaft shaft = chamber.Shafts[s];
                    source[s] = new Vector4(shaft.Source, shaft.Radius);
                    direction[s] = new Vector4(shaft.Direction, shaft.Length);
                }
                else
                {
                    source[s] = new Vector4(0f, -1000f, 0f, 1f);
                    direction[s] = new Vector4(0f, -1f, 0f, 1f);
                }
            }
            _effect.Parameters["ShaftSource"].SetValue(source);
            _effect.Parameters["ShaftDirection"].SetValue(direction);
            _chamberPushed = true;
        }

        /// <summary>
        /// The breath of cluster <paramref name="index"/> at <paramref name="time"/>: <c>Cavern.fx</c>'s <c>CrystalPulse</c>
        /// with the same phase the cluster's prisms carry (<see cref="CavernGeometry.BuildCrystals"/>), so the light a
        /// cluster throws swells with the glow in its prisms.
        /// </summary>
        private float Pulse(CavernCluster cluster, int index, float time)
        {
            float beat = 0.5f + 0.5f * MathF.Sin(time * _config.Crystals.PulseSpeed + cluster.Phase + index * 0.37f);
            return 0.65f + 0.35f * beat * beat;
        }

        /// <summary>A cluster's colour (linear), on the cyan-magenta axis at its hue.</summary>
        public Vector3 ClusterColor(CavernCluster cluster) =>
            Vector3.Lerp(_config.Crystals.ColorA.ToVector3(), _config.Crystals.ColorB.ToVector3(), cluster.Hue);

        //The light a cluster throws is its colour paler than its glow: a crystal's light on wet stone reads as a tint, and at
        //its own saturation it painted the columns magenta and teal
        private Vector3 LightTint(CavernCluster cluster)
        {
            Vector3 colour = ClusterColor(cluster);
            float luminance = Vector3.Dot(colour, new Vector3(0.2126f, 0.7152f, 0.0722f));
            return Vector3.Lerp(colour, new Vector3(luminance), LIGHT_PALE);
        }

        /// <summary>How far a cluster's light is paled towards white.</summary>
        private const float LIGHT_PALE = 0.5f;

        private void PushLights(CavernChamber chamber, float time)
        {
            int count = Math.Min(chamber.Clusters.Count, MAX_LIGHTS);
            for (int k = 0; k < count; k++)
            {
                CavernCluster cluster = chamber.Clusters[k];
                float range = LIGHT_RANGE * cluster.Size;
                _lightPositions[k] = new Vector4(cluster.LightPosition, 1f / (range * range));
                Vector3 colour = LightTint(cluster) * (LIGHT_INTENSITY * cluster.Size * cluster.Size * Pulse(cluster, k, time) * _config.Crystals.WallLight);
                _lightColors[k] = new Vector4(colour, 0f);
            }
            _lightPosition.SetValue(_lightPositions);
            _lightColor.SetValue(_lightColors);
            _lightCount.SetValue(count);
        }

        //The buffers for the detail the renderer draws now. Its arrays are waited for if they are the only ones there are; if
        //the other detail is already up (a tier changed mid-session), that one is drawn until these are ready, so a change of
        //tier is never a hitch. Arrays are let go once they are buffers.
        private CavernMeshes Meshes()
        {
            CavernChamber chamber = Chamber;
            if (!_chamberPushed) PushChamber(chamber);

            bool fine = Services.FineMeshes;
            StartDetail(fine);
            CavernMeshes wanted = fine ? _fine : _coarse;
            if (wanted != null) return wanted;

            Task<CavernMeshes.Arrays> build = fine ? _fineBuild : _coarseBuild;
            CavernMeshes other = fine ? _coarse : _fine;
            if (other != null && !build.IsCompleted) return other;

            var meshes = new CavernMeshes(_graphicsDevice, build.GetAwaiter().GetResult());
            if (fine) { _fine = meshes; _fineBuild = null; }
            else { _coarse = meshes; _coarseBuild = null; }
            return meshes;
        }

        /// <summary>
        /// Draws the hall: first the mirrored hall into the lake's reflection, then the formations and the rock (opaque,
        /// writing depth), the crystals over them (translucent), the glowworms and the shafts (added). Everything animated
        /// runs off the frame's wall-clock time, so the cave keeps living while the simulation is paused.
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            CavernMeshes meshes = Meshes();
            CavernChamber chamber = _chamber;
            ICamera camera = frame.Camera;
            Viewport viewport = _graphicsDevice.Viewport;

            _time.SetValue(frame.Time);
            PushLights(chamber, frame.Time);

            //Low gives the mirror up (its reduced program, SceneDetail): the lake reads a dark ramp and the clusters' glints
            bool mirrored = _fullDetail && DrawReflection(meshes, chamber, camera, viewport);
            _reflectionAvailable.SetValue(mirrored ? 1f : 0f);
            if (mirrored) _reflection.SetValue(_reflectionTarget);

            //The display-resolution target comes back from the reflection pass discarded: its depth is cleared again here.
            //The scene target in the direct path was cleared by its host and keeps what it holds across the switch
            //(PreserveContents), so nothing is cleared there
            if (mirrored && Services.ShadingAtDisplayResolution)
                _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1f, 0);

            _viewProjection.SetValue(camera.View * camera.Projection);
            _cameraPosition.SetValue(camera.Position);
            _viewportSize.SetValue(new Vector2(viewport.Width, viewport.Height));
            _glowwormPixels.SetValue(GLOWWORM_PIXELS * viewport.Height / 1080f);

            DrawHall(meshes, _fullDetail ? _rock : _rockReduced, _fullDetail ? _formation : _formationReduced, _crystal,
                RasterizerState.CullCounterClockwise);

            _wroteSceneDepth = !Services.ShadingAtDisplayResolution;
            _drawn = meshes;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        private void DrawHall(CavernMeshes meshes, EffectTechnique rock, EffectTechnique formation, EffectTechnique crystal, RasterizerState cull)
        {
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = cull;

            //The formations first: they stand in front of the shell, so the shell's pixels behind them are rejected by the
            //depth test before they are shaded
            if (meshes.Formations != null)
            {
                _effect.CurrentTechnique = formation;
                _effect.CurrentTechnique.Passes[0].Apply();
                meshes.Formations.Draw(_graphicsDevice);
            }

            _effect.CurrentTechnique = rock;
            _effect.CurrentTechnique.Passes[0].Apply();
            meshes.Rock.Draw(_graphicsDevice);

            if (meshes.Crystals != null)
            {
                //Writing depth, though they blend: the arena draws after the hall, and over a cluster it would otherwise be
                //drawn through it (a nearer prism of the same cluster drawn later still blends over a farther one)
                _graphicsDevice.BlendState = CRYSTAL_BLEND;
                _graphicsDevice.DepthStencilState = DepthStencilState.Default;
                _effect.CurrentTechnique = crystal;
                _effect.CurrentTechnique.Passes[0].Apply();
                meshes.Crystals.Draw(_graphicsDevice);
            }

            _graphicsDevice.BlendState = ADDITIVE;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            if (meshes.Glowworms != null)
            {
                _effect.CurrentTechnique = _glowworm;
                _effect.CurrentTechnique.Passes[0].Apply();
                meshes.Glowworms.Draw(_graphicsDevice);
            }

            if (meshes.Shafts != null && _config.Air.ShaftStrength > 0f)
            {
                _effect.CurrentTechnique = _shaft;
                _effect.CurrentTechnique.Passes[0].Apply();
                meshes.Shafts.Draw(_graphicsDevice);
            }
        }

        /// <summary>
        /// The hall seen in the lake: the camera mirrored in the water's plane, the hall drawn through it into a target of
        /// its own (everything under the waterline clipped away), so the lake can read it at its own pixel. The winding
        /// flips in a mirror, so the culling does. False when the lens is under the water, where there is no mirror to see.
        /// </summary>
        private bool DrawReflection(CavernMeshes meshes, CavernChamber chamber, ICamera camera, Viewport viewport)
        {
            if (camera.Position.Y <= chamber.WaterY + 0.05f) return false;

            int width = Math.Max(viewport.Width / 2, 1), height = Math.Max(viewport.Height / 2, 1);
            if (_reflectionTarget == null || _reflectionTarget.Width != width || _reflectionTarget.Height != height)
            {
                _reflectionTarget?.Dispose();
                _reflectionTarget = new RenderTarget2D(_graphicsDevice, width, height, false, SurfaceFormat.HdrBlendable,
                    DepthFormat.Depth24, 0, RenderTargetUsage.DiscardContents);
            }

            int bound = _graphicsDevice.RenderTargetCount;
            _graphicsDevice.GetRenderTargets(_bound);
            _graphicsDevice.SetRenderTarget(_reflectionTarget);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1f, 0);

            Matrix mirror = Matrix.CreateReflection(new Plane(Vector3.Up, -chamber.WaterY));
            Vector3 eye = camera.Position;
            _viewProjection.SetValue(mirror * camera.View * camera.Projection);
            _cameraPosition.SetValue(new Vector3(eye.X, 2f * chamber.WaterY - eye.Y, eye.Z));
            _viewportSize.SetValue(new Vector2(width, height));
            _glowwormPixels.SetValue(GLOWWORM_PIXELS * height / 1080f);

            DrawHall(meshes, _rockMirrored, _formationMirrored, _crystalMirrored, RasterizerState.CullClockwise);

            //Back to what was bound. One target is every caller this has (the display-resolution target or the scene's),
            //and the one-target call allocates nothing
            if (bound == 0) _graphicsDevice.SetRenderTarget(null);
            else if (bound == 1) _graphicsDevice.SetRenderTarget(_bound[0].RenderTarget as RenderTarget2D);
            else _graphicsDevice.SetRenderTargets(_bound[..bound]);
            Array.Clear(_bound);
            return true;
        }

        /// <summary>
        /// The hall's depth (#755) in the scene's own target, when the hall was shaded into the display-resolution one and its
        /// depth stayed there: the island, the funnel, the gun and the cluster are hidden where a column, a stalactite, a
        /// stalagmite, a crystal, a rock island or the shore stands in front of them. Only for a lens out where one can (the play camera never
        /// pays), scissored to the arena's box on the screen. Drawn depth-only, colour writes off.
        /// </summary>
        public override void DrawDepth(in SceneFrame frame)
        {
            if (_wroteSceneDepth || _drawn == null) return;
            Vector3 lens = frame.Camera.Position;
            if (lens.X * lens.X + lens.Z * lens.Z < CavernChamber.ARENA_AIR_RADIUS * CavernChamber.ARENA_AIR_RADIUS) return;

            Matrix viewProjection = frame.Camera.View * frame.Camera.Projection;
            if (!TryScissorArena(viewProjection, out Rectangle scissor)) return;

            CavernMeshes meshes = _drawn;

            _viewProjection.SetValue(viewProjection);
            _graphicsDevice.BlendState = DEPTH_ONLY;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = SCISSORED;
            _graphicsDevice.ScissorRectangle = scissor;

            _effect.CurrentTechnique = _depth;
            _effect.CurrentTechnique.Passes[0].Apply();
            meshes.Formations?.Draw(_graphicsDevice);
            //The rock too: a rock island or the bed's shore stands between a lens out on the lake and the funnel (the cone
            //drew through an island from the intro's glide until it did). And the crystals: translucent in the hall, but the
            //arena draws after the hall, so without their depth an island behind a cluster was drawn over it
            meshes.Rock.Draw(_graphicsDevice);
            meshes.Crystals?.Draw(_graphicsDevice);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The bound target's pixels the arena's box covers, for the formations' depth pass. False when it covers none - the
        /// arena is off screen and the depth would serve nothing; the whole target when a corner stands behind the lens,
        /// where a projection says nothing useful.
        /// </summary>
        private bool TryScissorArena(Matrix viewProjection, out Rectangle scissor)
        {
            Viewport viewport = _graphicsDevice.Viewport;
            scissor = new Rectangle(0, 0, viewport.Width, viewport.Height);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector4 world = new(
                    (corner & 1) == 0 ? -ARENA_BOUND_RADIUS : ARENA_BOUND_RADIUS,
                    (corner & 2) == 0 ? ARENA_BOUND_BOTTOM : ARENA_BOUND_TOP,
                    (corner & 4) == 0 ? -ARENA_BOUND_RADIUS : ARENA_BOUND_RADIUS,
                    1f);
                Vector4 clip = Vector4.Transform(world, viewProjection);
                if (clip.W <= 1e-3f) return true;

                float x = clip.X / clip.W, y = clip.Y / clip.W;
                minX = MathF.Min(minX, x);
                maxX = MathF.Max(maxX, x);
                minY = MathF.Min(minY, y);
                maxY = MathF.Max(maxY, y);
            }

            minX = MathF.Max(minX, -1f);
            maxX = MathF.Min(maxX, 1f);
            minY = MathF.Max(minY, -1f);
            maxY = MathF.Min(maxY, 1f);
            if (minX >= maxX || minY >= maxY) return false;

            int left = (int)MathF.Floor((minX * 0.5f + 0.5f) * viewport.Width);
            int right = (int)MathF.Ceiling((maxX * 0.5f + 0.5f) * viewport.Width);
            int top = (int)MathF.Floor((0.5f - maxY * 0.5f) * viewport.Height);
            int bottom = (int)MathF.Ceiling((0.5f - minY * 0.5f) * viewport.Height);
            scissor = new Rectangle(left, top, right - left, bottom - top);
            return scissor.Width > 0 && scissor.Height > 0;
        }

        /// <inheritdoc/>
        public override bool TryGetLightRig(float wallClock, out SceneLightRig rig)
        {
            //Dim and cool - a cave lit by its own bioluminescence, the ground bounce carrying the lake's teal up onto the
            //island's underside
            CavernLightingConfig cavern = _config.Lighting;
            rig = new SceneLightRig(
                cavern.SkyAmbient.ToVector3(),
                cavern.GroundAmbient.ToVector3(),
                cavern.KeyTint.ToVector3(),
                cavern.BackTint.ToVector3());
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //Low over the lake, the island a silhouette against the hall's lights: everything this scene shows glows from
            //the shores and the water, and the vault reads over it
            viewpoint = new SceneViewpoint(SceneRenderer.AtBearing(bearing, 120f, _config.Water.LevelY + 6f),
                1.8f, 16f, 168f, "the lake");
            return true;
        }

        /// <summary>A cluster further from the axis than this does not light the arena.</summary>
        private const float ARENA_LIGHT_REACH = 130f;

        /// <summary>The scene light's colour over the cluster's: the instanced effect's falloff is quadratic over its range.</summary>
        private const float ARENA_LIGHT_STRENGTH = 1.1f;

        /// <summary>
        /// The clusters whose light reaches the arena, for the scene lights on the instanced effect: those within
        /// <see cref="ARENA_LIGHT_REACH"/> of the axis, their colour on this frame's breath. Answers how many it wrote; none
        /// until the hall is built (the light is a nicety; a frame does not wait for it).
        /// </summary>
        public int ArenaLights(float time, Vector3[] position, Vector3[] colour, float[] range, int max)
        {
            float strength = _config.Crystals.ArenaLight;
            if (strength <= 0f) return 0;
            if (_chamber == null)
            {
                Prepare();
                if (!_chamberBuild.IsCompleted) return 0;
            }

            CavernChamber chamber = Chamber;
            int count = 0;
            for (int k = 0; k < chamber.Clusters.Count && count < max; k++)
            {
                CavernCluster cluster = chamber.Clusters[k];
                Vector3 at = cluster.LightPosition;
                float distance = MathF.Sqrt(at.X * at.X + at.Z * at.Z);
                if (distance > ARENA_LIGHT_REACH) continue;
                position[count] = at;
                colour[count] = ClusterColor(cluster) * (strength * ARENA_LIGHT_STRENGTH * Pulse(cluster, k, time));
                range[count] = distance * 1.6f + 20f;
                count++;
            }
            return count;
        }

        /// <summary>Frees the buffers and the reflection target; the effect is the content manager's.</summary>
        public override void Dispose()
        {
            _fine?.Dispose();
            _coarse?.Dispose();
            _reflectionTarget?.Dispose();
        }
    }
}
