using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The savanna: open rolling grassland on a camera-centred grid, everything planted on it (<see cref="SavannaScatter"/>:
    /// the acacias, the baobabs, the palms, the scrub, the tufts, the mounds, the kopjes, the fallen trees and the
    /// treeline), the trails that step round what stands on them, the ring of campfires in their hearths with their
    /// point lights, flames and sparks, and the shared flock over it. The last scene moved out of
    /// <see cref="SceneRenderer"/> in #580, whole — the config, the effects, the push, the planting, the draws, the
    /// viewpoint, the shadow map's fit, receivers and casters, the terrain probe, and the queries the renderer's
    /// <c>Savanna*</c> members and <c>CampfireColor</c> forward to. See "The savanna (Testbed)" in docs/scenes.md.
    /// </summary>
    internal sealed class SavannaBackdrop : Backdrop
    {
        private SavannaSceneConfig _savannaConfig = new();

        private readonly GraphicsDevice _graphicsDevice;

        //The tier the grass's and the planting's programs were last picked for: the constructor's, then every OnDetailChanged
        private float _sceneDetail;

        private readonly Effect _savannaEffect;

        //Its camera grid and the pass that draws it (#580)
        private readonly TerrainPass _savannaPass;

        //Open grassland is real geometry: a camera-centred grid of this many vertices per side over this world
        //extent, displaced in the shader and snapped to a cell so it does not swim. Finer than the old dune
        //grid (200) so the silhouette is smooth; the shading normal is per-pixel, so the grid no longer shows.
        private const int SAVANNA_GRID_N = 400;
        private const float SAVANNA_EXTENT = 1200f;

        //Gentle rolling grassland: flat in a clearing the island stands in (world origin), rising into low
        //rises with distance. Flatter than the meadow's hills - a savanna is open. Mean grass level sits at the
        //island's foot; ClearingRelief is a soft undulation even inside the clearing.
        //Look/tuning parameters (level, hills, clearing, grass colours, ambient, wind, haze, relief) now live in
        //SavannaSceneConfig; the backdrop reads them from _savannaConfig (and TerrainMirror.Savanna uses them too).

        private readonly Effect _acaciaEffect;

        //Cached effect parameters for the per-frame instanced draw (the by-name indexer is a linear scan).
        private EffectParameter _acaciaViewParam, _acaciaProjectionParam, _acaciaCameraParam,
            _acaciaSunDirectionParam, _acaciaSunColorParam, _acaciaZenithParam, _acaciaHorizonParam,
            _acaciaDiffuseParam, _acaciaDiffuseDryParam, _acaciaDappleParam, _acaciaBarkParam, _acaciaLeavesParam, _acaciaAddedLightParam,
            _acaciaHazeParam, _acaciaFootEarthParam;

        //Everything standing on the savanna (#202, #451): the acacias in their four kinds, the bushes, the
        //scrub, the grass tufts, the termite mounds, the kopjes, the fallen trees and the treeline at the
        //horizon — planted by SavannaScatter on the terrain (TerrainMirror.Savanna mirrors the shader's field)
        //and handed back as buckets, one instanced draw each with its instances uploaded once. Real geometry,
        //replacing the flat billboard that read as a paper cutout: a surface of revolution has volume from
        //every angle. Scatter parameters live in SavannaSceneConfig.Acacia and .Dressing.
        private SavannaScatter _savannaScatter;

        //The one dynamic instance buffer left on this path, for the hearth stones alone — they are drawn per
        //FIRE with that fire's light, so their instances are uploaded per draw (SetDataOptions.Discard).
        private DynamicVertexBuffer _acaciaInstanceBuffer;

        //Where the savanna's trails step aside for what is standing on it (#476), built with the planting.
        private TrailWarpField _trailWarp;

        private EffectTechnique _acaciaTechnique, _acaciaShadowTechnique;

        //The campfires' hearths (#282): a ring of stones set around each fire, and the scorched ground under
        //it. The stones ride the acacia's own instanced path - same shader, same lighting as everything else
        //planted on this terrain - with one draw per FIRE rather than per mesh variant, because what differs
        //between two rings is the firelight their own fire is casting at this instant.
        private RockMesh[] _hearthStoneMeshes;
        private ModelInstance[][] _hearthStoneInstances;  //per fire; index into _hearthStoneMeshes by fire % variants
        private Vector3 _hearthStoneColor;
        private float _hearthStoneFirelight;
        private readonly Vector3[] _hearthPositions = new Vector3[MAX_SCENE_LIGHTS];
        private readonly float[] _hearthLifts = new float[MAX_SCENE_LIGHTS];  //how high each fire's mound stands at its centre (#887)

        private readonly Effect _flameEffect;
        private readonly VertexBuffer _flameVertexBuffer;
        private readonly IndexBuffer _flameIndexBuffer;

        //Sub-flames per fire (#481), matching Flame.fx's own SUBFLAME_COUNT exactly: separate camera-facing
        //quads rather than one, so a fire has a silhouette that parallaxes as the view orbits it instead of
        //flipping between "fire" and "a picture of a fire" the way one quad always does. The buffer built
        //below is FLAME_SUBFLAME_COUNT quads laid end to end, X of each vertex's Position carrying which one
        //it belongs to - the shader indexes its own offset/scale/seed tables with it.
        private const int FLAME_SUBFLAME_COUNT = 3;

        //The sparks over each fire (#468): one shared buffer of MAX_SPARKS billboards, the fire's own
        //technique in Flame.fx animating them off the wall clock, drawn per fire after its flame.
        private const int MAX_SPARKS = 32;
        private VertexBuffer _sparkVertexBuffer;
        private IndexBuffer _sparkIndexBuffer;
        private EffectTechnique _flameTechnique, _sparkTechnique;

        //THE FIRE AS A VOLUME (#886): on High and Ultra (SceneRenderer.FineMeshes) each fire is its bounding box, every pixel
        //of which marches its view ray through the fire's own 3D field (Flame.fx's FlameVolume). The box is the unit one -
        //X and Z -1 to 1, Y 0 to 1 - the vertex shader stretching it round the hearth; its faces are wound clockwise seen
        //from outside, the front MonoGame's default cull keeps, so a pixel is shaded once, on the box's near side. A lens
        //inside the box (FLAME_VOLUME_CLEAR of it) would see none of its faces, so that fire falls back to the billboards.
        private readonly VertexBuffer _flameBoxVertexBuffer;
        private readonly IndexBuffer _flameBoxIndexBuffer;
        private EffectTechnique _flameVolumeTechnique;
        internal const float FLAME_VOLUME_RADIUS = 1.45f;  //Flame.fx's own, in fire sizes
        private const float FLAME_VOLUME_CLEAR = 0.5f;     //world units round the box a lens must stand outside
        private readonly Texture3D _flameNoise;

        //The volume's plasma, baked once (Flame.fx's FlameFbm): gradient noise that repeats every FLAME_NOISE_PERIOD units,
        //FLAME_NOISE_TEXELS_PER_UNIT texels a unit, so the shader's wrap sampler tiles it without a seam
        internal const int FLAME_NOISE_PERIOD = 8, FLAME_NOISE_TEXELS_PER_UNIT = 4;

        //Maximum scene point lights, matching MAX_SCENE_LIGHTS in InstancedModel.fx / Savanna.fx
        private const int MAX_SCENE_LIGHTS = 8;
        private readonly Vector3[] _savannaLightPos = new Vector3[MAX_SCENE_LIGHTS];
        private readonly Vector3[] _savannaLightColor = new Vector3[MAX_SCENE_LIGHTS];
        private readonly float[] _savannaLightRange = new float[MAX_SCENE_LIGHTS];

        //The savanna's campfire: a real point light that warms the grass, the island and the balls near it
        //(set on the savanna effect here and on the instanced effect by the Testbed), plus the visible flame
        //billboard below. It sits on the ground just off the island. Position/colour are public so the Testbed
        //can set the same light on the balls and island, and they flicker together off the one clock.
        //Just off the island in the grass, in front of its far edge and a little to the side, so it is in the
        //game camera's view (the camera sits at ~(0,-3,30) looking down -Z) and lights the island edge and grass.
        //Campfire parameters (ground position, range, flame size, base colour) live in
        //SavannaSceneConfig.Campfire (CampfireConfig). Position/range/colour stay public (the Testbed sets the
        //same point light on the balls and island) but are now instance members derived from the config.

        /// <summary>
        /// Loads the grass's, the planting's and the fires' effects, takes the grid through its <see cref="TerrainPass"/>,
        /// pushes the config (picking the grass's program for <paramref name="sceneDetail"/>, the renderer's tier at load),
        /// plants the savanna and its hearths, and builds the flames and the sparks.
        /// </summary>
        public SavannaBackdrop(BackdropServices services, ContentManager content, float sceneDetail) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;
            _sceneDetail = sceneDetail;

            //--- Savanna: a flat lattice the shader displaces into gentle grassland (per-pixel normal, no grid)
            _savannaEffect = content.Load<Effect>("Shaders/Savanna");
            _savannaPass = new TerrainPass(Services, _savannaEffect, SAVANNA_GRID_N, SAVANNA_EXTENT, "SavannaTime");

            ApplySavannaParameters();

            //--- Acacia: everything planted on the savanna, positioned on the ground (TerrainMirror.Savanna
            //mirrors the shader's field) and drawn as instanced geometry in Acacia.fx
            _acaciaEffect = content.Load<Effect>("Shaders/Acacia");
            _acaciaViewParam = _acaciaEffect.Parameters["View"];
            _acaciaProjectionParam = _acaciaEffect.Parameters["Projection"];
            _acaciaCameraParam = _acaciaEffect.Parameters["CameraPosition"];
            _acaciaSunDirectionParam = _acaciaEffect.Parameters["SunDirection"];
            _acaciaSunColorParam = _acaciaEffect.Parameters["SunColor"];
            _acaciaZenithParam = _acaciaEffect.Parameters["ZenithColor"];
            _acaciaHorizonParam = _acaciaEffect.Parameters["HorizonColor"];
            _acaciaDiffuseParam = _acaciaEffect.Parameters["DiffuseColor"];
            _acaciaDiffuseDryParam = _acaciaEffect.Parameters["DiffuseDry"];
            _acaciaDappleParam = _acaciaEffect.Parameters["DappleStrength"];
            _acaciaLeavesParam = _acaciaEffect.Parameters["LeafStrength"];
            _acaciaBarkParam = _acaciaEffect.Parameters["BarkStrength"];
            _acaciaAddedLightParam = _acaciaEffect.Parameters["AddedLight"];
            _acaciaHazeParam = _acaciaEffect.Parameters["HorizonHazeDistance"];
            _acaciaFootEarthParam = _acaciaEffect.Parameters["FootEarth"];
            _acaciaTechnique = _acaciaEffect.Techniques["Acacia"];
            _acaciaShadowTechnique = _acaciaEffect.Techniques["ShadowCaster"];
            ApplyAcaciaParameters();
            BuildSavannaScatter();
            BuildHearthStones();

            //--- Campfire flame: FLAME_SUBFLAME_COUNT billboards per fire (#481), one quad each, laid end to
            //end in a single buffer - Position.X of every vertex of a quad carries which sub-flame it is,
            //which is all Flame.fx needs to look up that sub-flame's own offset/scale/seed.
            _flameEffect = content.Load<Effect>("Shaders/Flame");
            SceneRenderer.BillboardVertex[] flameVertices = new SceneRenderer.BillboardVertex[FLAME_SUBFLAME_COUNT * 4];

            for (int sub = 0; sub < FLAME_SUBFLAME_COUNT; sub++)
            {
                int v = sub * 4;
                Vector3 subIndex = new(sub, 0f, 0f);
                flameVertices[v + 0] = new(subIndex, new Vector3(-1f, 0f, 0f));
                flameVertices[v + 1] = new(subIndex, new Vector3(1f, 0f, 0f));
                flameVertices[v + 2] = new(subIndex, new Vector3(-1f, 1f, 0f));
                flameVertices[v + 3] = new(subIndex, new Vector3(1f, 1f, 0f));
            }

            _flameVertexBuffer = new VertexBuffer(_graphicsDevice, SceneRenderer.BillboardVertex.Declaration, flameVertices.Length, BufferUsage.WriteOnly);
            _flameVertexBuffer.SetData(flameVertices);
            _flameIndexBuffer = Services.BuildQuadIndexBuffer(FLAME_SUBFLAME_COUNT, mirrored: true);
            _flameTechnique = _flameEffect.Techniques["Flame"];
            _sparkTechnique = _flameEffect.Techniques["Sparks"];
            //The sparks (#468): one shared buffer of billboards on the fountain's pattern, each fire drawing
            //the first SparkCount of them with its own position and clock.
            Services.BuildBillboardParticles(MAX_SPARKS, 4680, ref _sparkVertexBuffer, ref _sparkIndexBuffer);

            (SceneRenderer.BillboardVertex[] boxVertices, short[] boxIndices) = FlameBox();
            _flameBoxVertexBuffer = new VertexBuffer(_graphicsDevice, SceneRenderer.BillboardVertex.Declaration, boxVertices.Length, BufferUsage.WriteOnly);
            _flameBoxVertexBuffer.SetData(boxVertices);
            _flameBoxIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, boxIndices.Length, BufferUsage.WriteOnly);
            _flameBoxIndexBuffer.SetData(boxIndices);
            _flameVolumeTechnique = _flameEffect.Techniques["FlameVolume"];
            int side = FLAME_NOISE_PERIOD * FLAME_NOISE_TEXELS_PER_UNIT;
            _flameNoise = new Texture3D(_graphicsDevice, side, side, side, false, SurfaceFormat.Single);
            _flameNoise.SetData(FlameNoiseTexels());
            _flameEffect.Parameters["FlameNoiseVolume"].SetValue(_flameNoise);
        }

        /// <summary>
        /// The fire volume's noise (#886): Perlin's gradient noise with its lattice wrapped every
        /// <see cref="FLAME_NOISE_PERIOD"/> units, sampled <see cref="FLAME_NOISE_TEXELS_PER_UNIT"/> times a unit along each
        /// axis, X fastest. The gradients are a fixed roll, so every run bakes the same plasma.
        /// </summary>
        internal static float[] FlameNoiseTexels()
        {
            const int period = FLAME_NOISE_PERIOD, perUnit = FLAME_NOISE_TEXELS_PER_UNIT, side = period * perUnit;
            var rng = new Random(886);
            var gradients = new Vector3[period * period * period];
            for (int g = 0; g < gradients.Length; g++)
            {
                Vector3 d;
                do d = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                while (d.LengthSquared() < 0.05f || d.LengthSquared() > 1f);
                gradients[g] = Vector3.Normalize(d);
            }
            static float Fade(float f) => f * f * f * (f * (f * 6f - 15f) + 10f);

            var texels = new float[side * side * side];
            for (int z = 0; z < side; z++)
                for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++)
                    {
                        Vector3 p = new Vector3(x, y, z) / perUnit;
                        int ix = (int)MathF.Floor(p.X), iy = (int)MathF.Floor(p.Y), iz = (int)MathF.Floor(p.Z);
                        Vector3 f = p - new Vector3(ix, iy, iz);
                        float value = 0f;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            int cx = corner & 1, cy = (corner >> 1) & 1, cz = (corner >> 2) & 1;
                            Vector3 g = gradients[(ix + cx) % period + period * ((iy + cy) % period) + period * period * ((iz + cz) % period)];
                            float weight = (cx == 1 ? Fade(f.X) : 1f - Fade(f.X)) * (cy == 1 ? Fade(f.Y) : 1f - Fade(f.Y))
                                * (cz == 1 ? Fade(f.Z) : 1f - Fade(f.Z));
                            value += weight * Vector3.Dot(g, f - new Vector3(cx, cy, cz));
                        }
                        texels[x + side * (y + side * z)] = value;
                    }
            return texels;
        }

        /// <summary>
        /// The unit box a fire's volume is drawn through (#886): X and Z -1 to 1, Y 0 to 1, each face's two triangles wound
        /// clockwise seen from outside (the repository's rule: <c>(b - a) x (c - a)</c> points into the box).
        /// </summary>
        internal static (SceneRenderer.BillboardVertex[] Vertices, short[] Indices) FlameBox()
        {
            var vertices = new SceneRenderer.BillboardVertex[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = new(new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? 0f : 1f, (i & 4) == 0 ? -1f : 1f), Vector3.Zero);

            //Each face as four corners round it; the winding is chosen against the face's outward normal
            int[][] faces =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 },   //X -1, X +1
                new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 },   //Y 0, Y 1
                new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 },   //Z -1, Z +1
            };
            var indices = new List<short>(36);
            foreach (int[] f in faces)
            {
                Vector3 a = vertices[f[0]].Position, b = vertices[f[1]].Position, c = vertices[f[2]].Position;
                Vector3 outward = (a + c) * 0.5f - new Vector3(0f, 0.5f, 0f);
                bool inward = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f;
                int[] order = inward ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                foreach (int k in order) indices.Add((short)f[k]);
            }
            return (vertices, indices.ToArray());
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Savanna;

        /// <inheritdoc/>
        public override SceneConfig Config => _savannaConfig;

        /// <summary>The flock this scene draws; the renderer sizes the shared flock off its count.</summary>
        public BirdsConfig Birds => _savannaConfig.Birds;

        /// <summary><see cref="SceneRenderer.SavannaCampfireCount"/>.</summary>
        public int CampfireCount => CampfireRingCount(_savannaConfig);

        /// <summary>How many fires ring the island for <paramref name="config"/>, capped to the scene-light budget.</summary>
        internal static int CampfireRingCount(SavannaSceneConfig config) =>
            Math.Clamp(config.Campfire.Count, 1, SceneLights.MaxLights);

        /// <summary><see cref="SceneRenderer.SavannaCampfirePosition"/>.</summary>
        public Vector3 CampfirePosition(int index) =>
            CampfireRingPoint(_savannaConfig, index, _savannaConfig.Campfire.HeightAboveTerrain);

        /// <summary><see cref="SceneRenderer.SavannaCampfireLightPosition"/>.</summary>
        public Vector3 CampfireLightPosition(int index) =>
            CampfireRingPoint(_savannaConfig, index, _savannaConfig.Campfire.LightHeightAboveTerrain);

        /// <summary>
        /// Where fire <paramref name="index"/> of the ring stands, <paramref name="heightAboveTerrain"/> over the ground
        /// under it: the one copy of the ring's rule, which the flame, the grass's light and the light the island, the
        /// gun and the balls take (#885) all read, each at its own height. Static and device-free, so a test can state
        /// what the lights do without a graphics device.
        /// </summary>
        internal static Vector3 CampfireRingPoint(SavannaSceneConfig config, int index, float heightAboveTerrain)
        {
            Vec2 at = CampfireRingXZ(config, index);

            return new Vector3(at.X, TerrainMirror.Savanna(at.X, at.Y, config) + heightAboveTerrain, at.Y);
        }

        /// <summary>
        /// Where fire <paramref name="index"/> of the ring stands on the ground plane (X, Z in the vector's X, Y): <see cref="CampfireConfig.GroundXZ"/>
        /// is fire zero and the rest are evenly spaced round the circle it sits on. The ring's rule in its one copy, which the ground's mounds
        /// (<see cref="TerrainMirror.SavannaMound"/>, #887) read as well as <see cref="CampfireRingPoint"/>, since the mound under a fire is
        /// part of the height it stands at.
        /// </summary>
        internal static Vec2 CampfireRingXZ(SavannaSceneConfig config, int index)
        {
            Vec2 anchor = config.Campfire.GroundXZ;

            float radius = MathF.Sqrt(anchor.X * anchor.X + anchor.Y * anchor.Y);
            float angle = MathF.Atan2(anchor.Y, anchor.X) + index * MathHelper.TwoPi / CampfireRingCount(config);

            return new Vec2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }

        /// <summary><see cref="SceneRenderer.SavannaCampfireRange"/>.</summary>
        public float CampfireRange => _savannaConfig.Campfire.Range;

        /// <summary><see cref="SceneRenderer.SavannaPlanting"/>.</summary>
        public SavannaScatter Planting => _savannaScatter;

        /// <summary><see cref="SceneRenderer.SavannaGroundHeight"/>.</summary>
        public float GroundHeight(float x, float z) => TerrainMirror.Savanna(x, z, _savannaConfig);

        /// <summary><see cref="SceneRenderer.CampfireColor"/>.</summary>
        public Vector3 CampfireColor(float time, int index)
        {
            //Irrational-ish stride, so no two fires land on the same phase and the ring does not repeat after
            //a few of them however many there are.
            float t = time + index * 3.77f;
            float rate = 1f + index * 0.031f;

            float flicker = 0.72f + 0.28f * (0.5f * MathF.Sin(t * 11f * rate) + 0.3f * MathF.Sin(t * 17f * rate + 1.3f) + 0.2f * MathF.Sin(t * 7f * rate));

            return _savannaConfig.Campfire.BaseColor.ToVector3() * flicker;
        }

        private void ApplySavannaParameters()
        {
            SelectSavannaTechnique();

            _savannaEffect.Parameters["SavannaLevelY"].SetValue(_savannaConfig.LevelY);
            _savannaEffect.Parameters["HillHeight"].SetValue(_savannaConfig.HillHeight);
            _savannaEffect.Parameters["ClearingRadius"].SetValue(_savannaConfig.ClearingRadius);
            _savannaEffect.Parameters["ClearingTransition"].SetValue(_savannaConfig.ClearingTransition);
            _savannaEffect.Parameters["ClearingRelief"].SetValue(_savannaConfig.ClearingRelief);
            _savannaEffect.Parameters["GrassColor"].SetValue(_savannaConfig.GrassSavanna.ToVector3());
            _savannaEffect.Parameters["GrassColorDry"].SetValue(_savannaConfig.GrassDry.ToVector3());
            _savannaEffect.Parameters["GrassColorBare"].SetValue(_savannaConfig.GrassBare.ToVector3());
            _savannaEffect.Parameters["GrassTipColor"].SetValue(_savannaConfig.GrassTipColor.ToVector3());
            _savannaEffect.Parameters["GrassTipStrength"].SetValue(_savannaConfig.GrassTipStrength);
            _savannaEffect.Parameters["TuftSize"].SetValue(_savannaConfig.TuftSize);
            _savannaEffect.Parameters["TuftStrength"].SetValue(_savannaConfig.TuftStrength);
            _savannaEffect.Parameters["GrassSheenStrength"].SetValue(_savannaConfig.GrassSheenStrength);
            _savannaEffect.Parameters["GrassTranslucency"].SetValue(_savannaConfig.GrassTranslucency);
            _savannaEffect.Parameters["AmbientStrength"].SetValue(_savannaConfig.AmbientStrength);
            _savannaEffect.Parameters["WindDirection"].SetValue(_savannaConfig.Wind.ToVector2());
            _savannaEffect.Parameters["HorizonHazeDistance"].SetValue(_savannaConfig.HorizonHazeDistance);
            _savannaEffect.Parameters["WindRippleSpeed"].SetValue(_savannaConfig.WindRippleSpeed);
            _savannaEffect.Parameters["WindRippleFrequency"].SetValue(_savannaConfig.WindRippleFrequency);
            _savannaEffect.Parameters["WindRippleStrength"].SetValue(_savannaConfig.WindRippleStrength);
            _savannaEffect.Parameters["GrassReliefStrength"].SetValue(_savannaConfig.GrassReliefStrength);
            _savannaEffect.Parameters["GrassReliefFrequency"].SetValue(_savannaConfig.GrassReliefFrequency);
            _savannaEffect.Parameters["TrailStrength"].SetValue(_savannaConfig.TrailStrength);
            _savannaEffect.Parameters["TrailWidth"].SetValue(_savannaConfig.TrailWidth);
            _savannaEffect.Parameters["TrailFrequency"].SetValue(_savannaConfig.TrailFrequency);

            ApplyHearthParameters();
        }

        /// <summary>
        /// The hearth uniforms <c>Savanna.fx</c> burns the ground with (#282) and the lifts of the mounds the fires stand on (#887),
        /// pushed at config time rather than per frame: the fires stand on static terrain at config-derived places, so every one of these
        /// is constant until the config or the terrain changes — which is when this runs.
        /// <para>
        /// <c>HearthNear</c>/<c>HearthFar</c> are the ring's own extent, measured here <b>from the positions
        /// themselves</b> rather than re-derived from the config's ring rule in the shader: it is the early-out
        /// that keeps the per-pixel hearth loop off the rest of the field, and a second copy of the placement
        /// rule is exactly how a scene grows a fault nobody can see (#297).
        /// </para>
        /// </summary>
        private void ApplyHearthParameters()
        {
            CampfireConfig cf = _savannaConfig.Campfire;
            int fires = CampfireCount;

            (float moundTop, float moundFoot) = TerrainMirror.SavannaMoundShape(_savannaConfig);
            bool mounds = moundFoot > 0f;

            float near = float.MaxValue, far = 0f;
            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 at = CampfirePosition(fire);
                _hearthPositions[fire] = at;
                _hearthLifts[fire] = mounds ? TerrainMirror.SavannaMoundLift(_savannaConfig, fire) : 0f;

                float radius = MathF.Sqrt(at.X * at.X + at.Z * at.Z);
                near = MathF.Min(near, radius);
                far = MathF.Max(far, radius);
            }

            _savannaEffect.Parameters["HearthPosition"].SetValue(_hearthPositions);
            _savannaEffect.Parameters["HearthCount"].SetValue(fires);
            _savannaEffect.Parameters["HearthRadius"].SetValue(cf.FlameSize * cf.HearthRadiusScale);
            _savannaEffect.Parameters["HearthNear"].SetValue(near);
            _savannaEffect.Parameters["HearthFar"].SetValue(far);
            _savannaEffect.Parameters["HearthAsh"].SetValue(cf.HearthAsh.ToVector3());
            _savannaEffect.Parameters["HearthChar"].SetValue(cf.HearthChar.ToVector3());

            //The mounds the fires stand on (#887): the ground is raised under every fire by its own lift, so the lift is the mirror's own figure
            //(TerrainMirror.SavannaMoundLift) pushed once, and the shader adds the same smoothstep the mirror does round each hearth position.
            //With the mounds off every lift is 0 and the shape a harmless (0, 1), so the shader has no switch to read
            _savannaEffect.Parameters["HearthLift"].SetValue(_hearthLifts);
            _savannaEffect.Parameters["MoundTop"].SetValue(mounds ? moundTop : 0f);
            _savannaEffect.Parameters["MoundFoot"].SetValue(mounds ? moundFoot : 1f);
        }

        private void ApplyAcaciaParameters()
        {
            //The colours are the buckets' own now (SavannaScatter reads the config as it builds them); what
            //the effect takes at config time is the haze distance, so a far plant fades as the ground does.
            _acaciaHazeParam.SetValue(_savannaConfig.HorizonHazeDistance);
        }

        /// <summary>
        /// (Re)builds everything planted on the savanna (<see cref="SavannaScatter"/>): the mesh variants of
        /// every kind and the instance buffers, each thing planted on the terrain — a plant's height comes
        /// off the ground it stands on, so a terrain change re-plants the whole scatter. Deterministic seed,
        /// so the same config always gives the same savanna. The fires and their hearths are handed over as
        /// ground already taken, so nothing lands in a fire.
        /// </summary>
        private void BuildSavannaScatter()
        {
            DisposeAcacia();

            CampfireConfig cf = _savannaConfig.Campfire;
            int fires = CampfireCount;
            var reserved = new List<ScatterSpacing.Footprint>(fires);
            float hearth = cf.FlameSize * (cf.StoneRingScale + cf.StoneSizeScale) + 1f;
            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 at = CampfirePosition(fire);
                reserved.Add(new ScatterSpacing.Footprint(at.X, at.Z, hearth));
            }

            _savannaScatter = new SavannaScatter(_graphicsDevice, _savannaConfig, GroundHeight, reserved,
                SavannaScatter.DEFAULT_SEED + Services.SeedOffset);

            //And where the trails have to go round it (#476): built from the planting that has just been
            //done, so the field and the plants it bends for cannot disagree. Rebuilt with the scatter for
            //the same reason: a field left behind by a planting would send the paths round trees that are no
            //longer there.
            _trailWarp?.Dispose();
            _trailWarp = _savannaConfig.TrailAvoidOffset > 0f
                ? new TrailWarpField(_graphicsDevice, _savannaScatter.Standing,
                    _savannaConfig.TrailAvoidMinRadius, _savannaConfig.TrailAvoidReach,
                    _savannaConfig.TrailAvoidOffset, SAVANNA_EXTENT)
                : null;

            //⚠ And the uniforms are pushed HERE rather than in ApplySavannaParameters, which is where every
            //other savanna dial goes: that method runs BEFORE the planting does, so the texture it pushed
            //was always null and the warp never reached the shader. It cost one capture pair that looked
            //exactly like the feature not working — the paths were identical with it on and off, because
            //it was off both times.
            _savannaEffect.Parameters["TrailWarpTexture"].SetValue(_trailWarp?.Texture);
            _savannaEffect.Parameters["TrailWarpExtent"].SetValue(_trailWarp?.Extent ?? 1f);
            _savannaEffect.Parameters["TrailWarpAmount"].SetValue(_trailWarp == null ? 0f : _trailWarp.MaxOffset);
        }

        /// <summary>
        /// (Re)builds the ring of stones around each fire (#282): a few boulders of a handful of variants,
        /// set into the ground at their own spot on the terrain, rolled once and kept.
        /// <para>
        /// <b>Everything is sized off <see cref="CampfireConfig.FlameSize"/></b> rather than in world units,
        /// so a hearth belongs to the fire standing in it — these flames are 14 units tall at the shipped
        /// config, and a hearth measured once by hand would be a kerb of pebbles the day somebody widened
        /// them. The stones are sunk by a fraction of their own height, which is what makes a stone read as
        /// SET into the earth rather than resting on it: a lathe's flat underside meeting a rolling terrain
        /// at exactly ground level shows daylight under one side of every stone on a slope.
        /// </para>
        /// </summary>
        private void BuildHearthStones()
        {
            DisposeHearthStones();

            CampfireConfig cf = _savannaConfig.Campfire;
            _hearthStoneColor = cf.StoneColor.ToVector3();
            _hearthStoneFirelight = cf.StoneFirelight;

            int stones = Math.Max(0, cf.StoneCount);
            if (stones == 0) return;

            float size = cf.FlameSize * cf.StoneSizeScale;
            float ring = cf.FlameSize * cf.StoneRingScale;

            //Three shapes rather than one, for the reason the acacias have four: the eye reads the repeat
            //before it reads the stone. A ring takes one of them, so two neighbouring hearths differ as
            //wholes as well - which is what a camera walking the island past several of them shows.
            const int VARIANTS = 3;
            _hearthStoneMeshes = new RockMesh[VARIANTS];
            for (int v = 0; v < VARIANTS; v++)
            {
                _hearthStoneMeshes[v] = new RockMesh(_graphicsDevice,
                    radius: size * (0.82f + 0.18f * v),
                    height: size * (0.78f - 0.14f * v),
                    irregularityPhase: 1.7f * v);
            }

            int fires = CampfireCount;
            Random rng = new(28204 + Services.SeedOffset);
            _hearthStoneInstances = new ModelInstance[fires][];

            for (int fire = 0; fire < fires; fire++)
            {
                Vector3 at = CampfirePosition(fire);
                ModelInstance[] ring_ = new ModelInstance[stones];

                for (int s = 0; s < stones; s++)
                {
                    //Evenly spaced and then jittered, both in angle and in how far out it sits: a ring of
                    //stones laid by hand is regular in intent and irregular in fact.
                    float angle = (s + (float)rng.NextDouble() * 0.4f - 0.2f) * MathHelper.TwoPi / stones;
                    float radius = ring * (0.88f + 0.24f * (float)rng.NextDouble());

                    float x = at.X + MathF.Cos(angle) * radius;
                    float z = at.Z + MathF.Sin(angle) * radius;

                    float scale = 0.72f + 0.55f * (float)rng.NextDouble();
                    float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float tiltDir = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float tilt = 0.10f + 0.16f * (float)rng.NextDouble();

                    //Sunk by a fifth of its own height. The scale rides in the same matrix, so the sink has
                    //to be scaled with it or the small stones bury and the big ones float.
                    float y = GroundHeight(x, z) - size * scale * 0.2f;

                    Matrix world = Matrix.CreateScale(scale)
                        * Matrix.CreateFromAxisAngle(new Vector3(MathF.Cos(tiltDir), 0f, MathF.Sin(tiltDir)), tilt)
                        * Matrix.CreateRotationY(yaw)
                        * Matrix.CreateTranslation(x, y, z);

                    ring_[s] = new ModelInstance(world, Vector4.Zero);
                }

                _hearthStoneInstances[fire] = ring_;
            }
        }

        /// <summary>Disposes the hearth stone meshes — called on a rebuild and on teardown, like the acacias'.</summary>
        private void DisposeHearthStones()
        {
            if (_hearthStoneMeshes != null) foreach (RockMesh mesh in _hearthStoneMeshes) mesh?.Dispose();
            _hearthStoneMeshes = null;
            _hearthStoneInstances = null;
        }

        /// <summary>
        /// Disposes the acacia meshes and the shared instance buffer — called on a rebuild (a terrain or config
        /// change re-plants the scatter) and on the backdrop's own <see cref="Dispose"/>.
        /// </summary>
        private void DisposeAcacia()
        {
            _savannaScatter?.Dispose();
            _savannaScatter = null;
            _acaciaInstanceBuffer?.Dispose();
            _acaciaInstanceBuffer = null;
        }

        //The savanna's two programs (#281), the meadow's pair: the reduced one gives up the tuft gaps and the blade strokes
        private void SelectSavannaTechnique() =>
            _savannaEffect.CurrentTechnique = _savannaEffect.Techniques[_sceneDetail > 0.5f ? "Savanna" : "SavannaReduced"];

        /// <inheritdoc/>
        public override void OnDetailChanged(float sceneDetail)
        {
            _sceneDetail = sceneDetail;
            SelectSavannaTechnique();
        }

        /// <summary>The grassland, then everything planted on it and the hearths, then the flock over it.</summary>
        public override void Draw(in SceneFrame frame)
        {
            DrawSavanna(frame);
            DrawAcacias(frame);
            Services.Birds.Draw(frame, _savannaConfig.Birds);
        }

        /// <summary>The fires' flames and sparks, drawn with the setting in the Game, before the copy the ceiling's glass
        /// bends — they stand on the ground, so a fire seen through the plate is bent with it
        /// (<see cref="SceneRenderer.DrawGrounded"/>).</summary>
        public override void DrawGrounded(in SceneFrame frame) => DrawFlame(frame);

        /// <summary>
        /// Draws the savanna grassland: the grid pinned to the camera (snapped to a cell so it does not swim),
        /// rolled gently and shaded per-pixel (no grid) by the current dome, shadowed by the shared cloud field.
        /// </summary>
        private void DrawSavanna(in SceneFrame frame)
        {
            //The ring of campfires lights the grass around it (real point lights, present under every dome)
            int fires = CampfireCount;

            for (int fire = 0; fire < fires; fire++)
            {
                _savannaLightPos[fire] = CampfirePosition(fire);
                _savannaLightColor[fire] = CampfireColor(frame.Time, fire);
                _savannaLightRange[fire] = CampfireRange;
            }

            _savannaEffect.Parameters["SceneLightPosition"].SetValue(_savannaLightPos);
            _savannaEffect.Parameters["SceneLightColor"].SetValue(_savannaLightColor);
            _savannaEffect.Parameters["SceneLightRange"].SetValue(_savannaLightRange);
            _savannaEffect.Parameters["SceneLightCount"].SetValue(fires);

            //The camera, the sky, the clock, the clouds and the far fade are the pass's (#580); the fires are the savanna's own
            _savannaPass.Draw(frame, Services.TerrainHoleRadius);
        }

        /// <summary>
        /// Draws the scattered acacia trees and bushes: real 3D geometry (#202), one instanced draw per mesh
        /// variant per material — a tree's canopy (dappled green) and its trunk (brown) share the variant's
        /// per-plant matrices, a bush is its canopy alone. Shaded from the scene's own sun and dome, so a tree
        /// sits in the savanna's light. Opaque and depth-writing; savanna scene only, after the terrain.
        /// </summary>
        private void DrawAcacias(in SceneFrame frame)
        {
            _acaciaViewParam.SetValue(frame.Camera.View);
            _acaciaProjectionParam.SetValue(frame.Camera.Projection);
            _acaciaCameraParam.SetValue(frame.Camera.Position);
            _acaciaSunDirectionParam.SetValue(frame.SunDirection);
            _acaciaSunColorParam.SetValue(frame.SunColor);
            _acaciaZenithParam.SetValue(frame.ZenithLinear);
            _acaciaHorizonParam.SetValue(frame.HorizonLinear);

            //Per frame since #609: the plant material is one effect the meadow's planting shares, at its own haze —
            //and, since #670, with the plain's own bare earth up every trunk's foot, taken back to black after
            _acaciaHazeParam.SetValue(_savannaConfig.HorizonHazeDistance);
            Vector3 footEarth = _savannaConfig.GrassBare.ToVector3();

            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise; //real solids, wound like every lathe

            //Everything planted (#451): one instanced draw per bucket, each with its own material, off its
            //own static instance buffer. The Low tier skips the buckets marked as detail (the grass tufts).
            ScatterBucket[] buckets = _savannaScatter.Buckets;
            bool detail = _sceneDetail > 0.5f;
            bool fine = Services.FineMeshes;
            for (int b = 0; b < buckets.Length; b++)
            {
                ScatterBucket bucket = buckets[b];
                if (bucket.DetailOnly && !detail) continue;
                if (bucket.LowOnly && detail) continue;
                if (bucket.FineMeshes is bool meshes && meshes != fine) continue;

                _acaciaDiffuseParam.SetValue(bucket.Diffuse);
                _acaciaDiffuseDryParam.SetValue(bucket.DiffuseDry);
                _acaciaDappleParam.SetValue(bucket.Dapple);
                _acaciaBarkParam.SetValue(bucket.Bark);
                _acaciaLeavesParam.SetValue(bucket.Leaves);
                _acaciaFootEarthParam.SetValue(bucket.Earthed ? footEarth : Vector3.Zero);

                //Leaf cards are one-sided sheets seen from both sides (#610); everything else is a wound solid
                _graphicsDevice.RasterizerState = bucket.Leaves > 0f ? RasterizerState.CullNone : RasterizerState.CullCounterClockwise;
                _acaciaAddedLightParam.SetValue(Vector3.Zero);
                _acaciaEffect.CurrentTechnique.Passes[0].Apply();

                InstanceBinding.Set(_graphicsDevice, bucket.Mesh.VertexBuffer, 0, bucket.Instances, 0);
                _graphicsDevice.Indices = bucket.Mesh.IndexBuffer;
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, bucket.Mesh.PrimitiveCount, bucket.Count);
            }

            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            _acaciaLeavesParam.SetValue(0f);
            _acaciaFootEarthParam.SetValue(Vector3.Zero);

            //And the hearths the fires stand in: one draw per fire, because the firelight on a ring is its
            //own fire's and they do not flicker together.
            DrawHearthStones(frame);
        }

        /// <summary>
        /// Draws the ring of stones around each campfire (#282) on the acacia's own instanced path, one draw
        /// per fire.
        /// <para>
        /// <b>The firelight is a per-draw additive, not a ninth point light.</b> Every stone of a ring stands
        /// at one distance from one fire, so the attenuation a point light would solve per pixel is a
        /// constant here — worked out once against the same quadratic falloff <c>Savanna.fx</c> uses on the
        /// ground, so a stone and the grass beside it are lit by the one fire rather than by two rules. It is
        /// multiplied by the stone's own albedo, because what reaches the eye is firelight reflected off
        /// basalt and not the flame itself, and it rides <see cref="CampfireColor"/> at this frame's time so
        /// the ring breathes with the fire it belongs to.
        /// </para>
        /// </summary>
        private void DrawHearthStones(in SceneFrame frame)
        {
            if (_hearthStoneInstances == null || _hearthStoneMeshes == null) return;

            CampfireConfig cf = _savannaConfig.Campfire;
            float ring = cf.FlameSize * cf.StoneRingScale;

            //The ground's own falloff, at the one distance every stone of a ring stands at.
            float atten = MathHelper.Clamp(1f - ring / MathF.Max(CampfireRange, 1e-4f), 0f, 1f);
            atten *= atten;

            for (int fire = 0; fire < _hearthStoneInstances.Length; fire++)
            {
                ModelInstance[] instances = _hearthStoneInstances[fire];
                if (instances == null || instances.Length == 0) continue;

                Vector3 firelight = _hearthStoneColor * CampfireColor(frame.Time, fire) * (_hearthStoneFirelight * atten);

                DrawAcaciaPart(_hearthStoneMeshes[fire % _hearthStoneMeshes.Length], instances,
                    _hearthStoneColor, dappleStrength: 0f, addedLight: firelight);
            }
        }

        /// <summary>
        /// One instanced draw of a mesh part with its per-draw material: the instances are re-uploaded to the
        /// one shared dynamic buffer (<see cref="SetDataOptions.Discard"/>, so the GPU is not stalled on the
        /// last draw), the mesh's vertices bound at stream 0 and the instances at stream 1 — exactly as
        /// <see cref="InstancedModelRenderer"/> does it.
        /// </summary>
        private void DrawAcaciaPart(IProceduralMesh mesh, ModelInstance[] instances, Vector3 diffuse, float dappleStrength,
            Vector3 addedLight = default)
        {
            UploadHearthInstances(instances);

            _acaciaDiffuseParam.SetValue(diffuse);
            _acaciaDiffuseDryParam.SetValue(diffuse);   //no dryness on a stone: Custom.x is zero on every hearth instance
            _acaciaDappleParam.SetValue(dappleStrength);
            _acaciaBarkParam.SetValue(0f);
            _acaciaAddedLightParam.SetValue(addedLight);
            _acaciaEffect.CurrentTechnique.Passes[0].Apply();

            InstanceBinding.Set(_graphicsDevice, mesh.VertexBuffer, 0, _acaciaInstanceBuffer, 0);
            _graphicsDevice.Indices = mesh.IndexBuffer;
            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, instances.Length);
        }

        /// <summary>The hearth stones' per-draw upload into the one dynamic instance buffer, grown as needed.</summary>
        private void UploadHearthInstances(ModelInstance[] instances)
        {
            if (_acaciaInstanceBuffer == null || _acaciaInstanceBuffer.VertexCount < instances.Length)
            {
                _acaciaInstanceBuffer?.Dispose();
                _acaciaInstanceBuffer = new DynamicVertexBuffer(_graphicsDevice, ModelInstance.VertexDeclaration,
                    instances.Length, BufferUsage.WriteOnly);
            }
            _acaciaInstanceBuffer.SetData(instances, 0, instances.Length, SetDataOptions.Discard);
        }

        /// <summary>
        /// Draws the visible flames: one billboard per fire at its <see cref="CampfirePosition"/>, a
        /// procedural flickering flame in the shader, drawn additively and depth-read (the terrain or platform
        /// in front hides one) but writing no depth. The light each casts is a separate scene point light.
        /// Savanna scene only, drawn last with the overlays.
        /// <para>
        /// A draw per fire rather than one instanced pass: it is <see cref="FLAME_SUBFLAME_COUNT"/> quads
        /// each (#481, six triangles), eight fires at most, once a frame and only in this scene — and the
        /// alternative is an instance buffer and a vertex format for a quad that already has neither. What
        /// varies per fire is two uniforms; the sub-flames themselves are the one buffer built once.
        /// </para>
        /// </summary>
        private void DrawFlame(in SceneFrame frame)
        {
            _flameEffect.Parameters["View"].SetValue(frame.Camera.View);
            _flameEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _flameEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _flameEffect.Parameters["FlameSize"].SetValue(_savannaConfig.Campfire.FlameSize);
            _flameEffect.Parameters["FlameHeightScale"].SetValue(_savannaConfig.Campfire.FlameHeightScale);

            //⚠ ALPHA-BLENDED AND NOT ADDITIVE SINCE #468, and that is what lets a fire be RED.
            //Additive cannot make a red flame over a bright sky: the background's own green and blue
            //stay under whatever red is added to them, so a daylit savanna's fires washed to
            //yellow-white however the colour ramp was tuned - twice. The shader already returns its
            //colour PREMULTIPLIED by the coverage, which is exactly what BlendState.AlphaBlend takes
            //(One / InverseSourceAlpha), so the dense body now REPLACES what is behind it and the
            //thin edges still add. It goes on blooming, because the colours are linear radiance over 1
            //and the glare pass reads the scene target rather than the blend.
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_flameVertexBuffer);
            _graphicsDevice.Indices = _flameIndexBuffer;

            //Cached out of the loop: the by-name indexer is a linear scan, and this runs once per fire per
            //frame (BestPractices.md §1). Not fields, because nothing else in this class touches them.
            EffectParameter flamePosition = _flameEffect.Parameters["FlamePosition"];
            EffectParameter flameSeed = _flameEffect.Parameters["FlameSeed"];
            EffectParameter flameTime = _flameEffect.Parameters["FlameTime"];

            _flameEffect.CurrentTechnique = _flameTechnique;
            bool volume = Services.FineMeshes;
            float boxRadius = FLAME_VOLUME_RADIUS * _savannaConfig.Campfire.FlameSize + FLAME_VOLUME_CLEAR;
            float boxHeight = _savannaConfig.Campfire.FlameSize * _savannaConfig.Campfire.FlameHeightScale + FLAME_VOLUME_CLEAR;
            for (int fire = 0; fire < CampfireCount; fire++)
            {
                Vector3 position = CampfirePosition(fire);
                flamePosition.SetValue(position);

                //The same stride and rate stretch CampfireColor uses, so a flame and the light it casts are
                //the one fire rather than two things that happen to be in the same place.
                flameSeed.SetValue(1f + fire * 0.031f);
                flameTime.SetValue(frame.Time + fire * 3.77f);

                //High and Ultra march the fire's volume through its box, unless the lens stands inside the box (#886)
                Vector3 toLens = frame.Camera.Position - position;
                bool inside = MathF.Abs(toLens.X) < boxRadius && MathF.Abs(toLens.Z) < boxRadius
                    && toLens.Y > -FLAME_VOLUME_CLEAR && toLens.Y < boxHeight;
                if (volume && !inside)
                {
                    _flameEffect.CurrentTechnique = _flameVolumeTechnique;
                    _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
                    _graphicsDevice.SetVertexBuffer(_flameBoxVertexBuffer);
                    _graphicsDevice.Indices = _flameBoxIndexBuffer;
                    _flameEffect.CurrentTechnique.Passes[0].Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 12);
                    _flameEffect.CurrentTechnique = _flameTechnique;
                    _graphicsDevice.RasterizerState = RasterizerState.CullNone;
                    _graphicsDevice.SetVertexBuffer(_flameVertexBuffer);
                    _graphicsDevice.Indices = _flameIndexBuffer;
                    continue;
                }

                _flameEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, FLAME_SUBFLAME_COUNT * 2);
            }

            //The sparks (#468): the same per-fire uniforms over the shared spark buffer, one draw per fire.
            int sparks = Math.Clamp(_savannaConfig.Campfire.SparkCount, 0, MAX_SPARKS);
            if (sparks > 0 && _sparkVertexBuffer != null)
            {
                _flameEffect.CurrentTechnique = _sparkTechnique;
                _graphicsDevice.SetVertexBuffer(_sparkVertexBuffer);
                _graphicsDevice.Indices = _sparkIndexBuffer;
                for (int fire = 0; fire < CampfireCount; fire++)
                {
                    flamePosition.SetValue(CampfirePosition(fire));
                    flameSeed.SetValue(1f + fire * 0.031f);
                    flameTime.SetValue(frame.Time + fire * 3.77f);
                    _flameEffect.CurrentTechnique.Passes[0].Apply();
                    _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, sparks * 2);
                }
                _flameEffect.CurrentTechnique = _flameTechnique;
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //A real landmark, and the only one in this table that is also a LIGHT: the fire is what the
            //savanna's night rig is built around, so a shot that has it has the scene's whole character
            //in frame. Slot 0 of however many the config asks for.
            viewpoint = new SceneViewpoint(CampfirePosition(0), 1.7f, 11f, 35f, "the campfire");
            return true;
        }

        /// <inheritdoc/>
        public override IEnumerable<Effect> ShadowReceivers
        {
            get
            {
                //#469's two: the plain and what stands on it
                yield return _savannaEffect;
                yield return _acaciaEffect;
            }
        }

        /// <inheritdoc/>
        public override bool TryShadowFit(out float groundY, out float below, out float above)
        {
            //#469's own fit, kept to the digit: half a rise below the plain, and a baobab and a half
            //over the rises. It is the one that was measured and photographed, so it stays its own
            //expression rather than joining the shared headroom below.
            groundY = _savannaConfig.LevelY;
            below = _savannaConfig.HillHeight * 0.5f;
            above = _savannaConfig.HillHeight + _savannaConfig.Dressing.BaobabHeight * 1.5f;
            return true;
        }

        /// <summary>Everything planted and the hearth stones cast: #469's own casters, the first there were.</summary>
        public override bool HasShadowCasters => true;

        /// <summary>
        /// The savanna's own casters (#469): every bucket of the scatter and the ring of hearth stones,
        /// through <c>Acacia.fx</c>'s <c>ShadowCaster</c> technique. Puts the main technique back on the way
        /// out, the way <see cref="InstancedModelRenderer.DrawDepth(Matrix, ModelInstance[], int)"/> does.
        /// </summary>
        public override void DrawShadowCasters(Matrix shadowViewProjection)
        {
            if (_savannaScatter == null) return;

            _acaciaEffect.CurrentTechnique = _acaciaShadowTechnique;
            _acaciaEffect.Parameters["ShadowViewProjection"].SetValue(shadowViewProjection);
            _acaciaEffect.CurrentTechnique.Passes[0].Apply();

            ScatterBucket[] buckets = _savannaScatter.Buckets;
            for (int b = 0; b < buckets.Length; b++)
            {
                ScatterBucket bucket = buckets[b];

                //The map exists only at scene detail, so the plate crowns never cast into it and the leaf sprays
                //do, their leaflets cut out here as on screen (#610) — the dapple under a tree is the leaves'.
                //One wood a tree, the tier's (#758)
                if (!bucket.CastsShadow) continue;
                if (bucket.FineMeshes is bool meshes && meshes != Services.FineMeshes) continue;
                _acaciaLeavesParam.SetValue(bucket.Leaves);
                _acaciaEffect.CurrentTechnique.Passes[0].Apply();

                InstanceBinding.Set(_graphicsDevice, bucket.Mesh.VertexBuffer, 0, bucket.Instances, 0);
                _graphicsDevice.Indices = bucket.Mesh.IndexBuffer;
                _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, bucket.Mesh.PrimitiveCount, bucket.Count);
            }

            if (_hearthStoneInstances != null && _hearthStoneMeshes != null)
            {
                for (int fire = 0; fire < _hearthStoneInstances.Length; fire++)
                {
                    ModelInstance[] instances = _hearthStoneInstances[fire];
                    if (instances == null || instances.Length == 0) continue;
                    UploadHearthInstances(instances);
                    IProceduralMesh mesh = _hearthStoneMeshes[fire % _hearthStoneMeshes.Length];
                    InstanceBinding.Set(_graphicsDevice, mesh.VertexBuffer, 0, _acaciaInstanceBuffer, 0);
                    _graphicsDevice.Indices = mesh.IndexBuffer;
                    _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount, instances.Length);
                }
            }

            _acaciaLeavesParam.SetValue(0f);
            _acaciaEffect.CurrentTechnique = _acaciaTechnique;
        }

        /// <inheritdoc/>
        public override bool TryGetTerrainProbe(out Effect effect, out Func<float, float, float> mirror)
        {
            effect = _savannaEffect;
            mirror = (x, z) => TerrainMirror.Savanna(x, z, _savannaConfig);
            return true;
        }

        /// <summary>
        /// Frees the planting, the hearths, the flame's and the sparks' buffers and the trail-warp field; the grid is the
        /// cache's and the effects the content manager's.
        /// </summary>
        public override void Dispose()
        {
            DisposeAcacia();
            DisposeHearthStones();
            _flameVertexBuffer?.Dispose();
            _flameIndexBuffer?.Dispose();
            _flameBoxVertexBuffer?.Dispose();
            _flameBoxIndexBuffer?.Dispose();
            _flameNoise?.Dispose();
            _sparkVertexBuffer?.Dispose();
            _sparkIndexBuffer?.Dispose();
            _trailWarp?.Dispose();
        }
    }
}
