using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The volcano (#223, redrawn from references in #509): the flank of an erupting cone on a camera-centred grid,
    /// its crusted lava rivers and the lake in its crater, the fountains, the plume and the blaze over the crater, the
    /// drifting ash, the eruption on its own clock and the lamps that ride the flows. Moved out of
    /// <see cref="SceneRenderer"/> whole in #580; the renderer's public <c>Volcano*</c> queries forward here, and its
    /// <c>TryGetSceneEvent</c> and <c>TryGetGroundGlow</c> ask the backdrop. Its terrain draw is its own and not a
    /// <see cref="TerrainPass"/>: it leaves the blend state as it found it where the pass states opaque and restores
    /// alpha. See "The volcano" in docs/scenes.md.
    /// </summary>
    internal sealed class VolcanoBackdrop : Backdrop
    {
        private VolcanoSceneConfig _volcanoConfig = new();

        private readonly GraphicsDevice _graphicsDevice;

        private readonly Effect _volcanoEffect;
        private VertexBuffer _volcanoVertexBuffer; //not readonly: a tier's crossing rebuilds it (EnsureVolcanoGrid, #540)
        private IndexBuffer _volcanoIndexBuffer;
        private int _volcanoIndexCount;

        //The mountain's density and extent: the flank carries a summit against the sky, so it wants the
        //craggy grid rather than the desert's, and it needs the same 32-bit index buffer (TerrainGridCache).
        private const int VOLCANO_GRID_N = 360;

        //The reduced program's grid (#540): half the vertices, a 4.7-unit cell against 3.3. Measured on the APU at Low,
        //360 -> 256 is 0.9 ms by itself; 256 -> 192 measured a further 0.3-0.55 and was not taken - the 6.3-unit
        //cell's cone was never photographed, and 256 already puts Caldera under the budget.
        private const int VOLCANO_GRID_N_REDUCED = 256;
        private const float VOLCANO_EXTENT = 1200f;

        //Where a 16-bit index buffer runs out: four vertices a particle over 65 536 addressable ones. The
        //mountain's snow silently trusted a config to stay under a limit like this; a volcano's counts are
        //dials from day one (#209 defends a 75 FPS budget this scene spends particles against), so the cap
        //is stated rather than assumed.
        private const int MAX_BILLBOARD_PARTICLES = 16000;

        //The rivers, the vents, the eruption's clock and the lamps on the flows (#795): solved once from the config
        //(BuildVolcanoBuffers), and the figures the shader draws the flows from are the ones the scene lights ride,
        //which is what keeps a lamp on the river it is lighting. Its own class so a host with no backdrop can solve them.
        private VolcanoLava _lava;

        //The lava fountains and the smoke plume: ONE static billboard buffer, its first PlumeFraction drawn
        //as the plume and the rest as the jets, so neither pass pays for the other's particles. Animated
        //entirely in the vertex shader, so it is rebuilt only when a config is applied and never per frame.
        private readonly Effect _fountainEffect;
        private VertexBuffer _fountainVertexBuffer;
        private IndexBuffer _fountainIndexBuffer;
        private int _plumeQuads, _jetQuads;

        //Cached at load: the by-name Techniques indexer is a linear scan and this pass selects between the
        //three of them every frame (BestPractices.md §1).
        private readonly EffectTechnique _plumeTechnique, _jetTechnique, _glowTechnique;

        //The steam of the fumaroles on the lava field (#679): its own billboard buffer through LavaFountain.fx's
        //Steam technique, and the vents it rises from
        private const int MAX_FUMAROLES = 12;
        private readonly EffectTechnique _steamTechnique;
        private VertexBuffer _steamVertexBuffer;
        private IndexBuffer _steamIndexBuffer;
        private int _steamQuads;
        private readonly Vector3[] _fumarolePosition = new Vector3[MAX_FUMAROLES];
        private readonly float[] _fumaroleStrength = new float[MAX_FUMAROLES];

        //What stands on the lava field (#679's second step): blocks, bombs and the spatter cones under the strongest
        //fumaroles, drawn through the planted scenes' shared material and cast into the sun's map
        private readonly PlantPass _plants;
        private VolcanoPlanting _planting;

        //How many of the fumaroles stand on a spatter cone, the strongest first, and the cones' figures
        private const int SPATTER_CONES = 3;
        private const float CONE_RADIUS = 6f, CONE_HEIGHT = 4f;

        //The drifting ash, on the snowfall's machinery in its own shader (Ash.fx says why it is not Snow.fx).
        private readonly Effect _ashEffect;
        private VertexBuffer _ashVertexBuffer;
        private IndexBuffer _ashIndexBuffer;

        //Look/tuning parameters live in VolcanoSceneConfig; the backdrop reads them from _volcanoConfig.

        /// <summary>
        /// The volcano's layers (#540), which <see cref="SceneRenderer.VolcanoLayers"/> forwards to: a measurement dial
        /// the Testbed sets and every other caller leaves at <see cref="VolcanoLayer.All"/>.
        /// </summary>
        public VolcanoLayer Layers { get; set; } = VolcanoLayer.All;

        /// <summary>
        /// Loads the three effects, takes the grid, pushes the config and solves the rivers, the vents and the
        /// particle buffers.
        /// </summary>
        public VolcanoBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Volcano (#223, redrawn from references in #509): the fifteenth scene — the flank of an erupting
            //cone, its crusted lava rivers, the rivulets down it and the lake in its crater. The mountain's grid
            //density, because this terrain carries a summit against the sky; the fountains, the plume and the ash
            //are billboard buffers over two more effects, all animated in their vertex shaders and rebuilt only
            //when a config is applied.
            _volcanoEffect = content.Load<Effect>("Shaders/Volcano");
            EnsureVolcanoGrid(VOLCANO_GRID_N);

            _fountainEffect = content.Load<Effect>("Shaders/LavaFountain");
            _plumeTechnique = _fountainEffect.Techniques["Plume"];
            _steamTechnique = _fountainEffect.Techniques["Steam"];
            _jetTechnique = _fountainEffect.Techniques["Fountain"];
            _glowTechnique = _fountainEffect.Techniques["Glow"];
            _ashEffect = content.Load<Effect>("Shaders/Ash");
            _plants = new PlantPass(_graphicsDevice, content);

            ApplyVolcanoParameters();
            BuildVolcanoBuffers();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Volcano;

        /// <inheritdoc/>
        public override SceneConfig Config => _volcanoConfig;

        /// <summary>
        /// Pushes the volcano's static tuning into <c>Volcano.fx</c>, <c>LavaFountain.fx</c> and <c>Ash.fx</c>.
        /// The rivers' bearings, the vents and the two particle buffers depend on the terrain, so they are
        /// <see cref="BuildVolcanoBuffers"/>'s and are solved after this.
        /// </summary>
        private void ApplyVolcanoParameters()
        {
            VolcanoSceneConfig volcano = _volcanoConfig;

            _volcanoEffect.Parameters["VolcanoLevelY"].SetValue(volcano.LevelY);
            _volcanoEffect.Parameters["ClearingRadius"].SetValue(volcano.ClearingRadius);
            _volcanoEffect.Parameters["ClearingTransition"].SetValue(MathF.Max(volcano.ClearingTransition, 1f));
            _volcanoEffect.Parameters["ConeCenterXZ"].SetValue(volcano.ConeCenter.ToVector2());
            _volcanoEffect.Parameters["ConeRadius"].SetValue(MathF.Max(volcano.ConeRadius, 1f));
            _volcanoEffect.Parameters["ConeHeight"].SetValue(volcano.ConeHeight);
            _volcanoEffect.Parameters["ConeProfile"].SetValue(MathF.Max(volcano.ConeProfile, 0.1f));
            _volcanoEffect.Parameters["CraterRadius"].SetValue(MathF.Max(volcano.CraterRadius, 1f));
            _volcanoEffect.Parameters["CraterDepth"].SetValue(volcano.CraterDepth);
            _volcanoEffect.Parameters["GullyDepth"].SetValue(volcano.GullyDepth);

            //ROUNDED, and the shader says why: every bearing term is a multiple of this figure, and only an
            //integer multiple of atan2's angle closes across the ±π seam. A fractional count leaves a straight
            //scar running from the crater to the horizon. Rounded here rather than in the shader because it is
            //a per-config decision, not a per-vertex one.
            _volcanoEffect.Parameters["GullyCount"].SetValue(MathF.Round(MathF.Max(volcano.GullyCount, 1f)));

            _volcanoEffect.Parameters["ScoriaRelief"].SetValue(volcano.ScoriaRelief);
            _volcanoEffect.Parameters["RiverWidth"].SetValue(MathF.Max(volcano.RiverWidth, 0.5f));
            _volcanoEffect.Parameters["RiverWander"].SetValue(volcano.RiverWander);
            _volcanoEffect.Parameters["RiverSpeed"].SetValue(volcano.RiverSpeed);
            _volcanoEffect.Parameters["HaloWidth"].SetValue(MathF.Max(volcano.HaloWidth, 1.05f));
            _volcanoEffect.Parameters["RockColor"].SetValue(volcano.RockColor.ToVector3());
            _volcanoEffect.Parameters["RockColorLight"].SetValue(volcano.RockColorLight.ToVector3());
            _volcanoEffect.Parameters["ScoriaColor"].SetValue(volcano.ScoriaColor.ToVector3());
            _volcanoEffect.Parameters["LavaHot"].SetValue(volcano.LavaHot.ToVector3());
            _volcanoEffect.Parameters["LavaCool"].SetValue(volcano.LavaCool.ToVector3());
            _volcanoEffect.Parameters["CrustColor"].SetValue(volcano.CrustColor.ToVector3());
            _volcanoEffect.Parameters["CrustGlow"].SetValue(volcano.CrustGlow);
            _volcanoEffect.Parameters["CrackGlow"].SetValue(volcano.CrackGlow);
            _volcanoEffect.Parameters["PlateSize"].SetValue(MathF.Max(volcano.PlateSize, 0.1f));
            _volcanoEffect.Parameters["SheenStrength"].SetValue(volcano.SheenStrength);
            _volcanoEffect.Parameters["RivuletStrength"].SetValue(volcano.RivuletStrength);
            _volcanoEffect.Parameters["FieldCrackStrength"].SetValue(volcano.FieldCrackStrength);
            _volcanoEffect.Parameters["AmbientStrength"].SetValue(volcano.AmbientStrength);
            _volcanoEffect.Parameters["HorizonHazeDistance"].SetValue(MathF.Max(volcano.HorizonHazeDistance, 1f));
            _volcanoEffect.Parameters["HazeTint"].SetValue(volcano.HazeTint.ToVector3());
            _volcanoEffect.Parameters["HazeStrength"].SetValue(volcano.HazeStrength);
            _volcanoEffect.Parameters["WindDirection"].SetValue(volcano.Wind.ToVector2());

            LavaFountainConfig fountains = volcano.Fountains;

            _fountainEffect.Parameters["LaunchSpeed"].SetValue(fountains.Speed);
            _fountainEffect.Parameters["LaunchSpread"].SetValue(fountains.Spread);
            _fountainEffect.Parameters["BlobGravity"].SetValue(MathF.Max(fountains.Gravity, 0.1f));
            _fountainEffect.Parameters["BlobLife"].SetValue(MathF.Max(fountains.Life, 0.1f));
            _fountainEffect.Parameters["BlobSize"].SetValue(fountains.BlobSize);
            _fountainEffect.Parameters["WindDirection"].SetValue(volcano.Wind.ToVector2());
            _fountainEffect.Parameters["WindDrag"].SetValue(fountains.WindDrag);
            _fountainEffect.Parameters["StreakTime"].SetValue(MathF.Max(fountains.StreakTime, 0f));
            _fountainEffect.Parameters["EruptionBoost"].SetValue(volcano.Eruption.Boost);
            _fountainEffect.Parameters["LavaHot"].SetValue(volcano.LavaHot.ToVector3());
            _fountainEffect.Parameters["LavaCool"].SetValue(volcano.LavaCool.ToVector3());
            _fountainEffect.Parameters["PlumeColor"].SetValue(fountains.PlumeColor.ToVector3());
            _fountainEffect.Parameters["PlumeStrength"].SetValue(fountains.PlumeStrength);
            _fountainEffect.Parameters["PlumeGlow"].SetValue(fountains.PlumeGlow);
            SteamConfig steamLook = volcano.Steam;
            _fountainEffect.Parameters["SteamColor"].SetValue(steamLook.Color.ToVector3());
            _fountainEffect.Parameters["SteamStrength"].SetValue(steamLook.Strength);
            _fountainEffect.Parameters["SteamRise"].SetValue(steamLook.Rise);
            _fountainEffect.Parameters["SteamLife"].SetValue(steamLook.Life);
            _fountainEffect.Parameters["SteamSize"].SetValue(steamLook.Size);
            _fountainEffect.Parameters["SteamGlow"].SetValue(steamLook.Glow);

            //The plume's own figures are derived from the jets' rather than being four more dials: a column
            //rises about a third as fast as a blob is thrown, lives long enough to leave the frame, and its
            //stem is about as wide as the crater it stands in (0.8 of it until #509, which with the head the
            //shader now opens was still a tube next to the references' columns). Deriving them keeps a retuned
            //fountain and its own smoke in proportion, which is what a designer moving Speed actually wants.
            _fountainEffect.Parameters["PlumeRise"].SetValue(fountains.Speed * 0.55f);
            _fountainEffect.Parameters["PlumeSpread"].SetValue(volcano.CraterRadius * 1.1f);
            _fountainEffect.Parameters["PlumeLife"].SetValue(fountains.Life * 7f);
            _fountainEffect.Parameters["PlumeSize"].SetValue(fountains.BlobSize * 5f);

            //The blaze over the crater is sized off the crater it stands in, for the same reason.
            _fountainEffect.Parameters["GlowSize"].SetValue(volcano.CraterRadius * 1.6f);
            _fountainEffect.Parameters["GlowStrength"].SetValue(MathF.Max(fountains.GlowStrength, 0f));

            AshConfig ash = volcano.Ash;

            _ashEffect.Parameters["AshBoxSize"].SetValue(ash.BoxSize.ToVector3());
            _ashEffect.Parameters["AshFallSpeed"].SetValue(ash.FallSpeed);
            _ashEffect.Parameters["AshWind"].SetValue(ash.Wind.ToVector2());
            _ashEffect.Parameters["AshSway"].SetValue(ash.Sway);
            _ashEffect.Parameters["SpeckSize"].SetValue(ash.FlakeSize);
            _ashEffect.Parameters["AshSpin"].SetValue(ash.Spin);
            _ashEffect.Parameters["AshNearFade"].SetValue(MathF.Max(ash.NearFade, 0.1f));
            _ashEffect.Parameters["EmberFraction"].SetValue(ash.EmberFraction);
            _ashEffect.Parameters["AshColor"].SetValue(ash.AshColor.ToVector3());
            _ashEffect.Parameters["EmberColor"].SetValue(ash.EmberColor.ToVector3());
            _ashEffect.Parameters["AshOpacity"].SetValue(ash.Opacity);
        }

        /// <summary>
        /// (Re)solves everything about the volcano that depends on its terrain — the rivers' bearings and
        /// reaches, the vents the fountains are thrown from — and rebuilds the two particle buffers at the
        /// config's counts. Deterministic: same config, same volcano, every run and in every executable.
        /// </summary>
        private void BuildVolcanoBuffers()
        {
            VolcanoSceneConfig volcano = _volcanoConfig;
            _lava = new VolcanoLava(volcano, Services.SeedOffset);
            Vector2 cone = volcano.ConeCenter.ToVector2();

            _volcanoEffect.Parameters["RiverBearing"].SetValue(_lava.RiverBearing);
            _volcanoEffect.Parameters["RiverReach"].SetValue(_lava.RiverReach);
            _volcanoEffect.Parameters["RiverCount"].SetValue(_lava.RiverCount);

            _fountainEffect.Parameters["VentPosition"].SetValue(_lava.VentPosition);
            _fountainEffect.Parameters["VentStrength"].SetValue(_lava.VentStrength);
            _fountainEffect.Parameters["VentCount"].SetValue(_lava.VentCount);

            //--- The particles. One buffer for the fountains: its first slice is the plume and the rest are
            //the jets, drawn as two index ranges over the one buffer (see DrawLavaFountains) so neither pass
            //pays for the other's particles. Both counts are capped where a 16-bit index buffer runs out.
            int total = Math.Clamp(volcano.Fountains.ParticleCount, 0, MAX_BILLBOARD_PARTICLES);
            _plumeQuads = (int)(total * Math.Clamp(volcano.Fountains.PlumeFraction, 0f, 0.9f));
            _jetQuads = total - _plumeQuads;

            Services.BuildBillboardParticles(total, 8831, ref _fountainVertexBuffer, ref _fountainIndexBuffer);
            Services.BuildBillboardParticles(Math.Clamp(volcano.Ash.FlakeCount, 0, MAX_BILLBOARD_PARTICLES), 6491,
                ref _ashVertexBuffer, ref _ashIndexBuffer);

            //--- The fumaroles (#679): vents at rolled bearings and distances on the plain, never on the island, in
            //the cone's crater or on a lava river's line (river 0 is the one that passes the arena), each its own
            //strength so the columns are not a row of equal chimneys
            SteamConfig steam = volcano.Steam;
            Random steamRng = new(6790 + Services.SeedOffset);
            int fumaroles = 0;
            for (int tries = 0; fumaroles < Math.Clamp(steam.VentCount, 0, MAX_FUMAROLES) && tries < 200; tries++)
            {
                float bearing = (float)steamRng.NextDouble() * MathHelper.TwoPi;
                float reach = MathHelper.Lerp(steam.NearestVent, steam.FarthestVent, MathF.Sqrt((float)steamRng.NextDouble()));
                float x = MathF.Cos(bearing) * reach, z = MathF.Sin(bearing) * reach;
                float strength = 0.6f + 0.4f * (float)steamRng.NextDouble();
                if (Vector2.Distance(new Vector2(x, z), cone) < volcano.ConeRadius * 0.35f) continue;
                if (DistanceToRiver(new Vector2(x, z), cone, _lava.RiverBearing[0]) < volcano.RiverWidth * 2.5f) continue;
                _fumarolePosition[fumaroles] = new Vector3(x, GroundHeight(x, z) + 0.3f, z);
                _fumaroleStrength[fumaroles] = strength;
                fumaroles++;
            }
            //The strongest few stand on a spatter cone, and steam from its crater (#679's second step)
            var cones = new List<(Vector3 Foot, float Radius, float Height)>();
            var byStrength = new List<int>();
            for (int f = 0; f < fumaroles; f++) byStrength.Add(f);
            byStrength.Sort((a, b) => _fumaroleStrength[b].CompareTo(_fumaroleStrength[a]));
            for (int k = 0; k < Math.Min(SPATTER_CONES, fumaroles); k++)
            {
                int f = byStrength[k];
                float radius = CONE_RADIUS * (0.8f + 0.4f * _fumaroleStrength[f]);
                float coneHeight = CONE_HEIGHT * (0.75f + 0.5f * _fumaroleStrength[f]);
                Vector3 vent = _fumarolePosition[f];
                //Its foot on the lowest ground under it, and the steam out of its crater
                float foot = MathF.Min(GroundHeight(vent.X, vent.Z), MathF.Min(GroundHeight(vent.X + radius, vent.Z),
                    MathF.Min(GroundHeight(vent.X - radius, vent.Z), MathF.Min(GroundHeight(vent.X, vent.Z + radius), GroundHeight(vent.X, vent.Z - radius)))));
                cones.Add((new Vector3(vent.X, foot, vent.Z), radius, coneHeight));
                _fumarolePosition[f] = new Vector3(vent.X, foot + coneHeight * 0.8f, vent.Z);
            }

            bool KeepOut(float x, float z)
            {
                Vector2 p = new(x, z);
                if (Vector2.Distance(p, cone) < volcano.ConeRadius * 0.3f) return true;
                for (int r = 0; r < _lava.RiverCount; r++)
                {
                    Vector2 along = new(MathF.Cos(_lava.RiverBearing[r]), MathF.Sin(_lava.RiverBearing[r]));
                    float t = Vector2.Dot(p - cone, along);
                    if (t > 0f && t < _lava.RiverReach[r] && Vector2.Distance(p - cone, along * t) < volcano.RiverWidth * 1.6f) return true;
                }
                for (int f = 0; f < fumaroles; f++)
                    if (Vector2.Distance(p, new Vector2(_fumarolePosition[f].X, _fumarolePosition[f].Z)) < 3f) return true;
                return false;
            }

            _planting?.Dispose();
            _planting = new VolcanoPlanting(_graphicsDevice, GroundHeight, KeepOut, cones, 6795 + Services.SeedOffset);

            _fountainEffect.Parameters["FumarolePosition"].SetValue(_fumarolePosition);
            _fountainEffect.Parameters["FumaroleStrength"].SetValue(_fumaroleStrength);
            _fountainEffect.Parameters["FumaroleCount"].SetValue(Math.Max(fumaroles, 1));
            _steamQuads = fumaroles > 0 ? Math.Clamp(steam.ParticleCount, 0, MAX_BILLBOARD_PARTICLES) : 0;
            Services.BuildBillboardParticles(_steamQuads, 6792, ref _steamVertexBuffer, ref _steamIndexBuffer);
        }

        //How far a point stands from a river's line: the ray from the cone's axis along its bearing (#679's vents keep off it)
        private static float DistanceToRiver(Vector2 point, Vector2 cone, float bearing)
        {
            Vector2 along = new(MathF.Cos(bearing), MathF.Sin(bearing));
            Vector2 offset = point - cone;
            float t = MathF.Max(Vector2.Dot(offset, along), 0f);
            return Vector2.Distance(offset, along * t);
        }

        /// <summary><see cref="SceneRenderer.VolcanoGroundHeight"/>.</summary>
        public float GroundHeight(float x, float z) => _lava.GroundHeight(x, z);

        /// <summary><see cref="SceneRenderer.VolcanoEruption"/>.</summary>
        public float Eruption(float time) => _lava.Eruption(time);

        /// <inheritdoc/>
        public override bool TryGetSceneEvent(float time, out SceneEvent staged)
        {
            float period = _lava.BurstSchedule(time, out float index, out float start, out float _, out float size);

            //Slot 0 is the crater and does not move, so the time is not read for it - see
            //VolcanoLightPosition. The boom comes from the crater and not from the flows: a river
            //front is silent, and the plume is what is heard.
            staged = new SceneEvent((int)index, (index + start) * period, LightPosition(0, time), size);
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetGroundGlow(float time, out Vector3 position, out Vector3 color, out float range)
        {
            VolcanoSceneConfig volcano = _volcanoConfig;
            if (volcano.DeckGlow <= 0f)
            {
                position = default;
                color = default;
                range = 1f;
                return false;
            }

            position = _lava.VentPosition[0];
            //A shade towards the hot end of the lava's range: the cool end alone lit the deck blood-red, where
            //the references' clouds over a crater are orange.
            Vector3 lava = Vector3.Lerp(volcano.LavaCool.ToVector3(), volcano.LavaHot.ToVector3(), 0.15f);
            color = lava * volcano.DeckGlow * (0.55f + 0.9f * Eruption(time));
            range = volcano.DeckGlowRange;
            return true;
        }

        /// <summary><see cref="SceneRenderer.VolcanoLightCount"/>.</summary>
        public int LightCount => _lava.LightCount;

        /// <summary><see cref="SceneRenderer.VolcanoLightRange"/>.</summary>
        public float LightRange => _lava.LightRange;

        /// <summary><see cref="SceneRenderer.VolcanoLightPosition"/>.</summary>
        public Vector3 LightPosition(int index, float time) => _lava.LightPosition(index, time);

        /// <summary><see cref="SceneRenderer.VolcanoLightColor"/>.</summary>
        public Vector3 LightColor(float time, int index) => _lava.LightColor(time, index);

        /// <summary>The flows as figures, for <see cref="SceneRenderer.VolcanoLava"/>.</summary>
        public VolcanoLava Lava => _lava;

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            //The volcano (#509): the two kinds of hairline the references brought in off the flows - the
            //rivulets down the cone and the cracks in the field - arrived together and are given up together.
            //The flows, the crater's lake and the sheen stay on every tier; they are what the scene is.
            //Since #540 the reduced program is also a coarser grid under it: most of what the vertex program costs is
            //paid per vertex whatever the pixels, and the reduced program's scoria has no octave the finer grid resolves.
            _volcanoEffect.CurrentTechnique = _volcanoEffect.Techniques[sceneDetail > 0.5f ? "Volcano" : "VolcanoReduced"];
            EnsureVolcanoGrid(sceneDetail > 0.5f ? VOLCANO_GRID_N : VOLCANO_GRID_N_REDUCED);
        }

        /// <summary>
        /// (Re)builds the volcano's grid at <paramref name="n"/> vertices a side when it is not that already — once at
        /// load and again only when the tier crosses <see cref="SceneRenderer.SceneDetail"/>'s line, so never per frame.
        /// </summary>
        private void EnsureVolcanoGrid(int n)
        {
            if (_volcanoVertexBuffer != null && _volcanoGridN == n) return;

            //Given back rather than disposed: the grid cache owns it, and at full detail another scene draws the same one
            if (_volcanoVertexBuffer != null) Services.ReleaseGridMesh(_volcanoGridN, VOLCANO_EXTENT);
            Services.AcquireGridMesh(n, VOLCANO_EXTENT, out _volcanoVertexBuffer, out _volcanoIndexBuffer, out _volcanoIndexCount);
            _volcanoGridN = n;
        }

        private int _volcanoGridN;

        /// <summary>The flank, then the fountains, the plume and the blaze over it.</summary>
        public override void Draw(in SceneFrame frame)
        {
            //The flank first (it writes depth), then the fountains and the plume over it — they are
            //part of the far scene rather than foreground weather, because the cluster hangs in front
            //of the cone and has to occlude it. Only the ash is an overlay.
            if ((Layers & VolcanoLayer.Terrain) != 0) DrawVolcanoTerrain(frame);
            //What stands on the field (#679), opaque and depth-writing, before anything translucent over it
            if (_planting != null && (Layers & VolcanoLayer.Terrain) != 0)
                _plants.Draw(frame, _planting.Buckets, _volcanoConfig.HorizonHazeDistance, detail: true);
            DrawLavaFountains(frame);
        }

        /// <summary>The drifting ash, unless the layers dial has taken it off.</summary>
        public override void DrawOverlays(in SceneFrame frame)
        {
            if ((Layers & VolcanoLayer.Ash) != 0) DrawAsh(frame);
        }

        /// <summary>
        /// Draws the volcano's flank (#223): the grid pinned to the camera and snapped to a cell so the ground
        /// does not swim, displaced into the cone and its gullies, with the lava rivers drawn as an emissive
        /// band on it and the crust cracking glowing between them. Opaque and depth-writing, drawn first in
        /// the scene block, <see cref="RasterizerState.CullNone"/> (the winding is moot on a heightfield).
        /// </summary>
        private void DrawVolcanoTerrain(in SceneFrame frame)
        {
            float cell = VOLCANO_EXTENT / (_volcanoGridN - 1);
            float originX = MathF.Round(frame.Camera.Position.X / cell) * cell;
            float originZ = MathF.Round(frame.Camera.Position.Z / cell) * cell;

            _volcanoEffect.Parameters["OriginXZ"].SetValue(new Vector2(originX, originZ));
            _volcanoEffect.Parameters["IslandHoleRadius"].SetValue(Services.TerrainHoleRadius);
            _volcanoEffect.Parameters["View"].SetValue(frame.Camera.View);
            _volcanoEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _volcanoEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _volcanoEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _volcanoEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _volcanoEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _volcanoEffect.Parameters["SunColor"].SetValue(frame.SunColor);
            _volcanoEffect.Parameters["VolcanoTime"].SetValue(frame.Time);

            frame.ApplyClouds?.Invoke(_volcanoEffect);

            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            Services.FarField.Begin(_volcanoEffect, frame, VOLCANO_EXTENT);
            _graphicsDevice.SetVertexBuffer(_volcanoVertexBuffer);
            _graphicsDevice.Indices = _volcanoIndexBuffer;
            _volcanoEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _volcanoIndexCount / 3);
            Services.FarField.DrawRing(_volcanoEffect, new Vector2(originX, originZ), VOLCANO_EXTENT);

            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the lava fountains and the smoke plume standing over the crater — two index ranges over the
        /// <b>one</b> particle buffer, so the plume pass never touches a jet's vertex and the jet pass never
        /// touches a puff's. Both depth-read (the flank and the cluster hide what is behind them) and writing
        /// no depth; the plume alpha-blended first, the jets additively over it, which is the order those two
        /// have to be drawn in for smoke to sit behind fire rather than over it.
        /// <para>
        /// The eruption envelope goes in here rather than being computed per particle: one figure off the wall
        /// clock (<see cref="SceneRenderer.VolcanoEruption"/>) drives the jets' reach, the plume's density and the crater's
        /// point light together, which is what makes a burst read as one event.
        /// </para>
        /// </summary>
        private void DrawLavaFountains(in SceneFrame frame)
        {
            if (_fountainVertexBuffer == null) return;

            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _fountainEffect.Parameters["View"].SetValue(frame.Camera.View);
            _fountainEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _fountainEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _fountainEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _fountainEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _fountainEffect.Parameters["FountainTime"].SetValue(frame.Time);
            _fountainEffect.Parameters["Eruption"].SetValue(Eruption(frame.Time));

            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SetVertexBuffer(_fountainVertexBuffer);
            _graphicsDevice.Indices = _fountainIndexBuffer;

            //The fumaroles' steam first: nearer the ground and fainter than the plume, alpha-blended the same way
            if (_steamQuads > 0 && _steamVertexBuffer != null && (Layers & VolcanoLayer.Plume) != 0)
            {
                _graphicsDevice.BlendState = BlendState.AlphaBlend;
                _graphicsDevice.SetVertexBuffer(_steamVertexBuffer);
                _graphicsDevice.Indices = _steamIndexBuffer;
                _fountainEffect.CurrentTechnique = _steamTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _steamQuads * 2);
                _graphicsDevice.SetVertexBuffer(_fountainVertexBuffer);
                _graphicsDevice.Indices = _fountainIndexBuffer;
            }

            if (_plumeQuads > 0 && (Layers & VolcanoLayer.Plume) != 0)
            {
                _graphicsDevice.BlendState = BlendState.AlphaBlend;
                _fountainEffect.CurrentTechnique = _plumeTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _plumeQuads * 2);
            }

            if (_jetQuads > 0 && (Layers & VolcanoLayer.Jets) != 0)
            {
                _graphicsDevice.BlendState = BlendState.Additive;
                _fountainEffect.CurrentTechnique = _jetTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, _plumeQuads * 6, _jetQuads * 2);
            }

            //The blaze over the crater (#509): the buffer's first quad, whose corners are all the glow's vertex
            //shader reads - one quad, additive like the jets, so its order against them does not matter.
            if (_volcanoConfig.Fountains.GlowStrength > 0f && (Layers & VolcanoLayer.Glow) != 0)
            {
                _graphicsDevice.BlendState = BlendState.Additive;
                _fountainEffect.CurrentTechnique = _glowTechnique;
                _fountainEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// Draws the drifting ash: a boxful of specks around the camera, animated entirely in the vertex
        /// shader. Alpha-blended and depth-read but writing no depth, drawn last with the overlays. Volcano
        /// scene only.
        /// </summary>
        private void DrawAsh(in SceneFrame frame)
        {
            if (_ashVertexBuffer == null) return;

            Matrix inverseView = Matrix.Invert(frame.Camera.View);

            _ashEffect.Parameters["View"].SetValue(frame.Camera.View);
            _ashEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _ashEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _ashEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _ashEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _ashEffect.Parameters["AshTime"].SetValue(frame.Time);

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_ashVertexBuffer);
            _graphicsDevice.Indices = _ashIndexBuffer;
            _ashEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0,
                Math.Clamp(_volcanoConfig.Ash.FlakeCount, 0, MAX_BILLBOARD_PARTICLES) * 2);

            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The crater, read off the scene's own solved vent rather than off the cone's centre: slot 0
            //is the crater, it does not move, and it is where the fountain and the plume come from. This
            //is the case the whole table exists for — the old rolled bearing put the cone behind the
            //camera about as often as in front of it.
            viewpoint = new SceneViewpoint(LightPosition(0, 0f), 2.4f, 14f, 160f, "the crater");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get
            {
                yield return _volcanoEffect;
                yield return _plants.Effect;   //the blocks, bombs and cones on the field (#679)
            }
        }

        /// <inheritdoc/>
        public override bool HasShadowCasters => _planting != null;

        /// <summary>The blocks, the bombs and the spatter cones cast into the sun's map (#679), as every planted scene's do.</summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_planting != null) _plants.DrawShadowCasters(shadowViewProjection, _planting.Buckets);
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            //Same argument as the mountain: the cone is 140 units of far-away silhouette and the flank
            //under the island is what the map covers.
            groundY = _volcanoConfig.LevelY;
            below = _volcanoConfig.ConeHeight * 0.15f;
            above = _volcanoConfig.ConeHeight * 0.3f;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _volcanoEffect;
            mirror = (x, z) => TerrainMirror.Volcano(x, z, _volcanoConfig);
            return true;
        }

        /// <summary>Frees the particle buffers; the grid is the cache's and the effects the content manager's.</summary>
        public override void Dispose()
        {
            _fountainVertexBuffer?.Dispose();
            _fountainIndexBuffer?.Dispose();
            _ashVertexBuffer?.Dispose();
            _ashIndexBuffer?.Dispose();
            _steamVertexBuffer?.Dispose();
            _steamIndexBuffer?.Dispose();
            _planting?.Dispose();
            _planting = null;
        }
    }
}
