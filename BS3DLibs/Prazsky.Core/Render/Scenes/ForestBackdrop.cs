using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The forest: a mossy, needle-strewn clearing ringed by wooded hills on a camera-centred grid under the dome.
    /// Its trees, boulders and stumps are not this backdrop's: they are the hosts' <see cref="ForestScatterRenderer"/>,
    /// drawn and cast into the sun's map by the host, standing on the floor this draws through <see cref="TerrainMirror.Forest"/>.
    /// Moved out of <see cref="SceneRenderer"/> whole in #580 — the config, the effect and its <see cref="TerrainPass"/>,
    /// the push, the reduced program, the draw, the viewpoint, the shadow map's fit and receiver, and the terrain probe
    /// <c>mirrorcheck</c> reads. See "The forest" in docs/scenes.md.
    /// </summary>
    internal sealed class ForestBackdrop : Backdrop
    {
        private ForestSceneConfig _forestConfig = new();

        //The tier the effect's program was last picked for: the constructor's, then every OnDetailChanged
        private float _sceneDetail;

        private readonly Effect _forestEffect;

        //ForestHaze's murk, worked out off each frame's sky (#1121)
        private readonly EffectParameter _murk;

        //Its camera grid and the pass that draws it (#580)
        private readonly TerrainPass _forestPass;

        //The bank the island sits in (#608), and the stones set into it, through the shared plant material. The trees are the
        //hosts'; this is the one thing standing on the floor that is the scene's. Since #983 the bank itself is drawn as the
        //floor (_bank, Forest.fx's ForestBank) and only its stones in the plant material; the planting's buckets, bank and
        //stones, still cast into the sun's map
        private readonly PlantPass _plants;
        private IslandBermPlanting _berm;
        private ScatterBucket[] _bermStones;

        //The bank as one mesh, the very one the planting's two halves are cut from (the same ground, the same seed), worn in
        //the forest floor so it is the ground rising to the stone and not a mat laid round it (#983)
        private IslandBerm.BermMesh _bank;
        private readonly EffectTechnique _bankTechnique, _bankReducedTechnique;
        private readonly Action _drawBank;

        //Where the wood stands (#962): planted here rather than by the hosts' renderers, so what this backdrop dresses the
        //wood with on High and Ultra hangs on the trees they draw (SceneRenderer.ForestPlanting)
        private readonly ForestScatter _planting;

        //High and Ultra's wood on the hills past the planted trees (#962), drawn through this effect after the floor
        private ForestFarStand _farStand;

        //What the wood wears on High and Ultra (#962): the broadleaves in leaf and the ferns, through the plant material
        private ForestDressing _dressing;

        //The lamps round the island (#1166): where they stand, planned once off the planting, and their light this frame on
        //the floor, the bank and the plants (the island and the wood take it through SceneLights, off the same layout)
        private readonly ForestLampLayout _lamps;
        private readonly Vector3[] _lightPosition = new Vector3[SceneLights.MaxLights];
        private readonly Vector3[] _lightColour = new Vector3[SceneLights.MaxLights];
        private readonly float[] _lightRange = new float[SceneLights.MaxLights];
        private readonly EffectParameter _sceneLightPosition, _sceneLightColour, _sceneLightRange, _sceneLightCount;

        //And the wind turbine on the hills that powers them, drawn after the far stand
        private ForestTurbine _turbine;

        private const int FOREST_GRID_N = 220;
        private const float FOREST_EXTENT = 1200f;

        //Look/tuning parameters (hills, clearing, forest floor colours, treeline, ambient, haze, wind, needle
        //relief, floor lumps) live in ForestSceneConfig; read from _forestConfig. Its Trees/Rocks/Stumps
        //describe the scattered objects, which are ForestScatterRenderer's instanced draws rather than this
        //scene's (they were the Game's alone until #75) - only TerrainMirror.Forest is shared with them,
        //so they stand on the floor this shader draws.

        /// <summary>
        /// Loads the effect, takes its grid through its <see cref="TerrainPass"/> and pushes the config at it, picking
        /// the program for <paramref name="sceneDetail"/> (the renderer's tier at load) as it does.
        /// </summary>
        public ForestBackdrop(BackdropServices services, ContentManager content, float sceneDetail) : base(services)
        {
            _sceneDetail = sceneDetail;

            //--- Forest: a mossy needle-strewn clearing ringed by wooded hills (the eighth scene)
            _forestEffect = content.Load<Effect>("Shaders/Forest");
            _murk = _forestEffect.Parameters["ForestMurk"];
            _forestPass = new TerrainPass(Services, _forestEffect, FOREST_GRID_N, FOREST_EXTENT, "ForestTime");

            ApplyForestParameters();

            ForestSceneConfig config = _forestConfig;
            _planting = ForestScatterRenderer.Plant(ForestScatterRenderer.DEFAULT_SEED + Services.SeedOffset, config,
                (x, z) => TerrainMirror.Forest(x, z, config));
            _farStand = new ForestFarStand(services.GraphicsDevice, _forestEffect, config, _planting);
            _dressing = new ForestDressing(services.GraphicsDevice, config, _planting, ForestScatterRenderer.DEFAULT_SEED + Services.SeedOffset);
            _dressingDrawn = _dressing.Buckets;

            _lamps = ForestLampLayout.Plan(config, _planting);
            _sceneLightPosition = _forestEffect.Parameters["SceneLightPosition"];
            _sceneLightColour = _forestEffect.Parameters["SceneLightColor"];
            _sceneLightRange = _forestEffect.Parameters["SceneLightRange"];
            _sceneLightCount = _forestEffect.Parameters["SceneLightCount"];
            _turbine = new ForestTurbine(services.GraphicsDevice, _forestEffect, config);

            _plants = new PlantPass(services.GraphicsDevice, content);
            _berm = new IslandBermPlanting(services.GraphicsDevice, (x, z) => TerrainMirror.Forest(x, z, _forestConfig),
                ForestScatterRenderer.DEFAULT_SEED + Services.SeedOffset,
                earth: _forestConfig.EarthColor.ToVector3() * 2.5f, earthDry: _forestConfig.LitterColorDark.ToVector3(),
                cover: _forestConfig.LitterColor.ToVector3() * 0.62f, coverDry: _forestConfig.ForestColor.ToVector3() * 0.6f, coverDapple: 0.8f,
                stone: _forestConfig.Rocks.Color.ToVector3(), stones: BERM_STONES,
                //Mossy boulders at the foot, a few of them big, as the references drew the forest's
                stoneRadius: (IslandBerm.STONE_RADIUS_MIN, IslandBerm.STONE_RADIUS_MAX + 2f), stoneSize: (0.4f, 1.4f));
            _bermStones = _berm.Buckets[IslandBermPlanting.BANK_BUCKETS..];

            _bank = IslandBerm.Build(services.GraphicsDevice, (x, z) => TerrainMirror.Forest(x, z, _forestConfig),
                ForestScatterRenderer.DEFAULT_SEED + Services.SeedOffset, 0, IslandBerm.RINGS - 1);
            _forestEffect.Parameters["BankCoverPhase"].SetValue(IslandBerm.Phase(ForestScatterRenderer.DEFAULT_SEED + Services.SeedOffset));
            _forestEffect.Parameters["BankCoverOn"].SetValue(1f);
            _bankTechnique = _forestEffect.Techniques["ForestBank"];
            _bankReducedTechnique = _forestEffect.Techniques["ForestBankReduced"];
            _drawBank = DrawBank;
        }

        //The bank, through the floor's own program for the tier, inside the terrain pass: the frame's parameters and the far
        //fade are pushed, and the grid drawn after it is turned away wherever the bank stands over the ground
        private void DrawBank()
        {
            if (_bank == null || (_layers & ForestLayer.Bank) == 0) return;

            EffectTechnique grid = _forestEffect.CurrentTechnique;
            _forestEffect.CurrentTechnique = _sceneDetail > 0.5f ? _bankTechnique : _bankReducedTechnique;
            _forestEffect.CurrentTechnique.Passes[0].Apply();

            Services.GraphicsDevice.SetVertexBuffer(_bank.VertexBuffer);
            Services.GraphicsDevice.Indices = _bank.IndexBuffer;
            Services.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _bank.PrimitiveCount);

            _forestEffect.CurrentTechnique = grid;
        }

        private const int BERM_STONES = 14;

        /// <summary>Where the wood stands: the hosts' <see cref="ForestScatterRenderer"/> draws this planting, so what this
        /// backdrop dresses it with on High and Ultra hangs on the trees that are drawn (#962).</summary>
        public ForestScatter Planting => _planting;

        /// <summary>Where the lamps round the island stand (#1166), planned off <see cref="Planting"/>: what the hosts' lamps
        /// are drawn from and what <see cref="SceneLights"/> lights the island with.</summary>
        public ForestLampLayout Lamps => _lamps;

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Forest;

        /// <inheritdoc/>
        public override SceneConfig Config => _forestConfig;

        private void SelectForestTechnique() =>
            _forestEffect.CurrentTechnique = _forestEffect.Techniques[_sceneDetail > 0.5f ? "Forest" : "ForestReduced"];

        private void ApplyForestParameters()
        {
            SelectForestTechnique();

            _forestEffect.Parameters["ForestLevelY"].SetValue(_forestConfig.LevelY);
            _forestEffect.Parameters["HillHeight"].SetValue(_forestConfig.HillHeight);
            _forestEffect.Parameters["ClearingRadius"].SetValue(_forestConfig.ClearingRadius);
            _forestEffect.Parameters["ClearingTransition"].SetValue(_forestConfig.ClearingTransition);
            _forestEffect.Parameters["ClearingRelief"].SetValue(_forestConfig.ClearingRelief);
            _forestEffect.Parameters["FloorLumpStrength"].SetValue(_forestConfig.FloorLumpStrength);
            _forestEffect.Parameters["FloorLumpFrequency"].SetValue(_forestConfig.FloorLumpFrequency);
            _forestEffect.Parameters["ForestColor"].SetValue(_forestConfig.ForestColor.ToVector3());
            _forestEffect.Parameters["ForestColorDark"].SetValue(_forestConfig.ForestColorDark.ToVector3());
            _forestEffect.Parameters["LitterColor"].SetValue(_forestConfig.LitterColor.ToVector3());
            _forestEffect.Parameters["LitterColorDark"].SetValue(_forestConfig.LitterColorDark.ToVector3());
            _forestEffect.Parameters["EarthColor"].SetValue(_forestConfig.EarthColor.ToVector3());
            _forestEffect.Parameters["UndergrowthColor"].SetValue(_forestConfig.UndergrowthColor.ToVector3());
            _forestEffect.Parameters["DryGrassColor"].SetValue(_forestConfig.DryGrassColor.ToVector3());
            _forestEffect.Parameters["MossCoverage"].SetValue(_forestConfig.MossCoverage);
            _forestEffect.Parameters["UndergrowthCoverage"].SetValue(_forestConfig.UndergrowthCoverage);
            _forestEffect.Parameters["DryGrassCoverage"].SetValue(_forestConfig.DryGrassCoverage);
            _forestEffect.Parameters["MossHeight"].SetValue(_forestConfig.MossHeight);
            _forestEffect.Parameters["TreelineColor"].SetValue(_forestConfig.TreelineColor.ToVector3());
            _forestEffect.Parameters["TreelineStrength"].SetValue(_forestConfig.TreelineStrength);
            _forestEffect.Parameters["AmbientStrength"].SetValue(_forestConfig.AmbientStrength);
            _forestEffect.Parameters["HorizonHazeDistance"].SetValue(_forestConfig.HorizonHazeDistance);
            //Where the sky shader's deck closes over the dome, so the haze's sky behind a far tree takes the lid in (#1121)
            _forestEffect.Parameters["CloudHorizonFade"].SetValue(CloudField.HORIZON_FADE);
            _forestEffect.Parameters["WindDirection"].SetValue(_forestConfig.Wind.ToVector2());
            _forestEffect.Parameters["WindRippleSpeed"].SetValue(_forestConfig.WindRippleSpeed);
            _forestEffect.Parameters["WindRippleFrequency"].SetValue(_forestConfig.WindRippleFrequency);
            _forestEffect.Parameters["WindRippleStrength"].SetValue(_forestConfig.WindRippleStrength);
            _forestEffect.Parameters["NeedleReliefStrength"].SetValue(_forestConfig.NeedleReliefStrength);
            _forestEffect.Parameters["NeedleReliefFrequency"].SetValue(_forestConfig.NeedleReliefFrequency);
        }

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            _sceneDetail = sceneDetail;
            SelectForestTechnique();
        }

        /// <inheritdoc/>
        public override void Draw(in SceneFrame frame)
        {
            //The murk the floor and the far stand recede into (ForestHaze), off this frame's sky
            Vector3 murk = Murk(frame, _forestConfig);
            _murk.SetValue(murk);

            //The lamps' light on the floor and the bank (#1166) and on what stands on them through the plant material, on the
            //clock SceneLights lights the island on, so the pool under a pole and the light on the stone are one light
            int lights = (_layers & ForestLayer.Lamps) != 0
                ? _lamps.Lights(frame.Time, _lightPosition, _lightColour, _lightRange, SceneLights.MaxLights) : 0;
            if (lights > 0)
            {
                _sceneLightPosition.SetValue(_lightPosition);
                _sceneLightColour.SetValue(_lightColour);
                _sceneLightRange.SetValue(_lightRange);
            }
            _sceneLightCount.SetValue(lights);
            _plants.SetSceneLights(_lightPosition, _lightColour, _lightRange, lights);

            _forestPass.DrawsGrid = (_layers & ForestLayer.Floor) != 0;
            _forestPass.DrawsFarRing = (_layers & ForestLayer.FarRing) != 0;
            if ((_layers & (ForestLayer.Floor | ForestLayer.FarRing | ForestLayer.Bank)) != 0)
                _forestPass.Draw(frame, Services.TerrainHoleRadius, _drawBank);

            //The wood on the hills (#962), after the floor so the depth test turns away every tree a nearer hill hides
            if (Services.FineMeshes && (_layers & ForestLayer.FarStand) != 0) _farStand?.Draw(frame.Camera);

            //The turbine on its hill (#1166), after the floor and the stand round its foot, in their light and their haze. Every
            //tier: it is what says where the lamps' power comes from
            if ((_layers & ForestLayer.Turbine) != 0) _turbine?.Draw(frame.Time);

            //The plant material in the weather the rest of the wood stands in (#1121): under the overcast lid the floor, the far
            //stand and the hosts' trees take about half the sun (CloudSunlight), and drawn in full sun the broadleaves in leaf
            //came out the lightest thing in the wood. Hazed to the murk the floor under them recedes into first (ForestHaze),
            //and not to the light rig's horizon, whose teal is a colour no part of this sky is drawn in.
            if (_berm != null && (_layers & ForestLayer.Stones) != 0)
                _plants.DrawInWeather(frame, _bermStones, _forestConfig.HorizonHazeDistance, _sceneDetail > 0.5f, Services.FineMeshes, murk);

            //What the wood wears on High and Ultra (#962); every bucket of it is FineMeshes, so Low and Medium skip the lot
            if (_dressing != null && Services.FineMeshes)
                _plants.DrawInWeather(frame, _dressingDrawn, _forestConfig.HorizonHazeDistance, _sceneDetail > 0.5f, fineMeshes: true, murk);
        }

        /// <summary>
        /// The layers drawn (#1099, #1122): a measurement dial (<see cref="SceneRenderer.ForestLayers"/>, the Testbed's
        /// <c>forest=</c>), <see cref="ForestLayer.All"/> for every other caller. The host's wood takes the same value through its
        /// own renderer. Each layer is drawn or not on the next frame; nothing carries.
        /// </summary>
        public ForestLayer Layers
        {
            get => _layers;
            set
            {
                _layers = value;
                _dressingDrawn = _dressing == null ? Array.Empty<ScatterBucket>() : Of(_dressing, value);
            }
        }

        private ForestLayer _layers = ForestLayer.All;

        //The dressing's buckets the layers draw, all of them unless the dial says otherwise
        private ScatterBucket[] _dressingDrawn;

        //The buckets of the dressing whose kind is among the layers, drawn and cast (the cast as a whole is LeafShadow)
        private static ScatterBucket[] Of(ForestDressing dressing, ForestLayer layers)
        {
            var kept = new List<ScatterBucket>();
            for (int i = 0; i < dressing.Buckets.Length; i++)
                if ((dressing.Kinds[i] & layers) != 0) kept.Add(dressing.Buckets[i]);
            return kept.ToArray();
        }

        /// <summary>
        /// The murk the wood recedes into (#1121, <see cref="ForestSceneConfig.MurkShade"/>): the sky as it is drawn at the
        /// horizon, greyed to <see cref="ForestSceneConfig.MurkSaturation"/> of its own colour and shaded. Off the drawn dome
        /// (<see cref="SceneFrame.FarSky"/>'s direction.y 0), the light rig's horizon only where a frame hands none over.
        /// </summary>
        internal static Vector3 Murk(in SceneFrame frame, ForestSceneConfig config) =>
            Murk(frame.FarSky != null ? frame.FarSky[SkyLightRig.FAR_SKY_HORIZON] : frame.HorizonLinear, config);

        /// <summary>The murk off a given sky colour (linear): <see cref="Murk(in SceneFrame, ForestSceneConfig)"/>'s arithmetic.</summary>
        internal static Vector3 Murk(Vector3 skyAtHorizon, ForestSceneConfig config) =>
            Vector3.Lerp(new Vector3(ColorSpace.Luminance(skyAtHorizon)), skyAtHorizon, config.MurkSaturation) * config.MurkShade.ToVector3();

        /// <inheritdoc/>
        public override bool HasShadowCasters => _berm != null || _dressing != null;

        /// <summary>The bank casts into the sun's map, and on High and Ultra the broadleaves' leaves and wood (#962); the rest of
        /// the wood is cast by the hosts.</summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_berm != null) _plants.DrawShadowCasters(shadowViewProjection, _berm.Buckets, Services.FineMeshes);
            if (_dressing != null && Services.FineMeshes && (_layers & ForestLayer.LeafShadow) != 0)
                _plants.DrawShadowCasters(shadowViewProjection, _dressingDrawn, fineMeshes: true);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _berm?.Dispose();
            _berm = null;
            _bank?.Dispose();
            _bank = null;
            _farStand?.Dispose();
            _farStand = null;
            _dressing?.Dispose();
            _dressing = null;
            _turbine?.Dispose();
            _turbine = null;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The tree line at crown height, near enough that a trunk is a trunk. The forest's scatter
            //starts just outside the clearing, so this is where the trees actually are.
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, _forestConfig.ClearingRadius + 45f, _forestConfig.LevelY + 14f),
                1.7f, 10f, 0f, "the tree line");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get
            {
                yield return _forestEffect; //its floor; the trees receive through the shared effect
                yield return _plants.Effect; //and the bank round the island (#608), and High and Ultra's leaves and ferns (#962)
            }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            groundY = _forestConfig.LevelY;
            below = _forestConfig.HillHeight * 0.5f;
            above = _forestConfig.HillHeight;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _forestEffect;
            mirror = (x, z) => TerrainMirror.Forest(x, z, _forestConfig);
            return true;
        }
    }
}
