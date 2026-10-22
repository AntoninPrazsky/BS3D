using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The daytime forest's far stand on High and Ultra (#962, <see cref="ForestFarStandConfig"/>): the wood on the hills past
    /// the planted trees, out to the last ridges in the haze, as spires (<see cref="AuroraStandMesh"/>) and rounded broadleaf
    /// crowns (<see cref="ForestCrownStandMesh"/>) by the ten thousand, each kind at a near and a far detail, one instanced draw
    /// each through <c>Forest.fx</c>'s <c>ForestStand</c>, which lights and hazes them as the floor under them is. Before it
    /// the trees stopped at 340 units and the hills carried the forest as a painted canopy colour, which from the play
    /// camera read as bare green hills. It casts no shadow (the sun's map does not reach it) and is in no planting a host reads.
    /// </summary>
    internal sealed class ForestFarStand : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _technique;
        private readonly EffectParameter _albedo, _tipLight;
        private readonly Vector3 _spruceColor, _broadleafColor;
        private readonly float _tip;

        //The eight draws, each kind in two shapes (so a ridge is not one tree stamped out a thousand times) at two details:
        //the spires first (near A, near B, far A, far B), then the crowns in the same order
        private readonly IProceduralMesh[] _meshes;
        private readonly VertexBuffer[] _instances;
        private readonly int[] _counts;

        private const int CROWNS = 4;

        //A draw's index: kind (0 spire, 1 crown), detail (0 near, 1 far), shape (0, 1)
        private static int DrawOf(bool crown, bool far, int shape) => (crown ? CROWNS : 0) + (far ? 2 : 0) + shape;

        //The cells the planted wood stands in: the stand never puts a tree inside one (the aurora's rule)
        private const float TAKEN_CELL = 7f;

        /// <summary>How many trees the stand planted, all four draws together (for a test and the run log).</summary>
        public int Count { get; }

        /// <summary>How many triangles a frame draws for it.</summary>
        public long Triangles { get; }

        public ForestFarStand(GraphicsDevice device, Effect forestEffect, ForestSceneConfig config, ForestScatter planting)
        {
            _device = device;
            _effect = forestEffect;
            _technique = forestEffect.Techniques["ForestStand"];
            _albedo = forestEffect.Parameters["StandAlbedo"];
            _tipLight = forestEffect.Parameters["StandTipLight"];

            ForestFarStandConfig stand = config.FarStand;
            _spruceColor = stand.SpruceColor.ToVector3();
            _broadleafColor = stand.BroadleafColor.ToVector3();
            _tip = stand.TipLight;

            //Two spires (a broad one of five tiers, a narrow taller one of six) and two crowns rolled apart, each at a near and
            //a far detail
            var broad = new AuroraFarStandConfig { Height = stand.SpruceHeight, Radius = stand.SpruceRadius };
            var narrow = new AuroraFarStandConfig { Height = stand.SpruceHeight * 1.15f, Radius = stand.SpruceRadius * 0.78f };
            _meshes = new IProceduralMesh[]
            {
                new AuroraStandMesh(device, broad, tiers: 5, sides: 10),
                new AuroraStandMesh(device, narrow, tiers: 6, sides: 10),
                new AuroraStandMesh(device, broad, tiers: 3, sides: 8),
                new AuroraStandMesh(device, narrow, tiers: 3, sides: 8),
                new ForestCrownStandMesh(device, stand.CrownHeight, stand.CrownRadius, slices: 10, stacks: 6, seed: 96202),
                new ForestCrownStandMesh(device, stand.CrownHeight * 1.12f, stand.CrownRadius * 0.85f, slices: 10, stacks: 6, seed: 96203),
                new ForestCrownStandMesh(device, stand.CrownHeight, stand.CrownRadius, slices: 7, stacks: 4, seed: 96202),
                new ForestCrownStandMesh(device, stand.CrownHeight * 1.12f, stand.CrownRadius * 0.85f, slices: 7, stacks: 4, seed: 96203),
            };

            List<StandInstance>[] planted = Plant(config, planting);
            _instances = new VertexBuffer[planted.Length];
            _counts = new int[planted.Length];
            for (int k = 0; k < planted.Length; k++)
            {
                _counts[k] = planted[k].Count;
                _instances[k] = new VertexBuffer(device, StandInstance.Declaration, Math.Max(planted[k].Count, 1), BufferUsage.WriteOnly);
                if (planted[k].Count > 0) _instances[k].SetData(planted[k].ToArray());
                Count += planted[k].Count;
                Triangles += (long)planted[k].Count * _meshes[k].PrimitiveCount;
            }
        }

        /// <summary>
        /// Draws the eight through the forest's effect, whose view, projection and light the terrain pass has just set;
        /// the technique, the culling and the blend it found are put back.
        /// </summary>
        public void Draw()
        {
            EffectTechnique technique = _effect.CurrentTechnique;
            RasterizerState raster = _device.RasterizerState;
            BlendState blend = _device.BlendState;
            DepthStencilState depth = _device.DepthStencilState;

            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
            _effect.CurrentTechnique = _technique;
            _tipLight.SetValue(_tip);

            for (int k = 0; k < _meshes.Length; k++)
            {
                if (_counts[k] == 0) continue;
                _albedo.SetValue(k < CROWNS ? _spruceColor : _broadleafColor);
                _technique.Passes[0].Apply();
                InstanceBinding.Set(_device, _meshes[k].VertexBuffer, 0, _instances[k], 0);
                _device.Indices = _meshes[k].IndexBuffer;
                _device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, _meshes[k].PrimitiveCount, _counts[k]);
            }

            _effect.CurrentTechnique = technique;
            _device.RasterizerState = raster;
            _device.BlendState = blend;
            _device.DepthStencilState = depth;
        }

        public void Dispose()
        {
            foreach (IProceduralMesh mesh in _meshes) (mesh as IDisposable)?.Dispose();
            foreach (VertexBuffer buffer in _instances) buffer?.Dispose();
        }

        /// <summary>A far tree's instance: its foot and scale, then its yaw's sine and cosine, its height's stretch and its tint.</summary>
        private readonly struct StandInstance : IVertexType
        {
            public readonly Vector4 Place;
            public readonly Vector4 Look;

            public StandInstance(Vector4 place, Vector4 look)
            {
                Place = place;
                Look = look;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2));

            VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        /// <summary>
        /// Where the stand stands, by draw: a jittered grid over the ring from <see cref="ForestFarStandConfig.InnerRadius"/> out,
        /// a finer one inside <see cref="ForestFarStandConfig.NearRadius"/>; open where a broad field says glade, never in a cell
        /// the planted wood stands in, spruce or broadleaf by a field a grove or two across, planted a little sunk on the floor
        /// <c>Forest.fx</c> draws, and sorted nearest the arena first so the depth test turns away what the near ones hide.
        /// </summary>
        private static List<StandInstance>[] Plant(ForestSceneConfig config, ForestScatter planting)
        {
            ForestFarStandConfig stand = config.FarStand;
            var taken = new HashSet<(int, int)>();
            if (planting != null)
            {
                Take(taken, planting.FineConifers);
                Take(taken, planting.FineBroadleaves);
                Take(taken, planting.FineSnags);
            }

            var rng = new Random(stand.Seed);
            var planted = new List<StandInstance>[2 * CROWNS];
            for (int k = 0; k < planted.Length; k++) planted[k] = new List<StandInstance>();

            void Ring(float inner, float outer, float spacing, bool near)
            {
                int cells = (int)MathF.Ceiling(outer / spacing);
                float inner2 = inner * inner, outer2 = outer * outer;
                for (int gz = -cells; gz < cells; gz++)
                    for (int gx = -cells; gx < cells; gx++)
                    {
                        //Every cell rolls its dice whether or not it plants, so a change to one field moves no other tree
                        float jx = (float)rng.NextDouble(), jz = (float)rng.NextDouble();
                        float scaleRoll = (float)rng.NextDouble(), yawRoll = (float)rng.NextDouble();
                        float stretchRoll = (float)rng.NextDouble(), tintRoll = (float)rng.NextDouble();
                        int shape = rng.Next(2);

                        float x = (gx + 0.1f + 0.8f * jx) * spacing, z = (gz + 0.1f + 0.8f * jz) * spacing;
                        float r2 = x * x + z * z;
                        if (r2 < inner2 || r2 >= outer2) continue;

                        //Glades and rides: open ground where a broad field is low
                        float glade = 0.5f + 0.8f * ShaderMath.Noise(new Vector2(x * 0.013f + 17.3f, z * 0.013f + 41.9f));
                        if (glade < stand.Glades) continue;
                        if (taken.Contains(((int)MathF.Floor(x / TAKEN_CELL), (int)MathF.Floor(z / TAKEN_CELL)))) continue;

                        //Spruce stands and beech stands, in patches a grove or two across
                        float patch = 0.5f + 0.9f * ShaderMath.Noise(new Vector2(x * 0.009f + 3.1f, z * 0.009f + 77.7f));
                        bool broadleaf = patch < stand.BroadleafShare;

                        float scale = MathHelper.Lerp(stand.MinScale, stand.MaxScale, scaleRoll);
                        float yaw = yawRoll * MathHelper.TwoPi;
                        float stretch = 0.85f + 0.35f * stretchRoll;
                        float tint = 0.75f + 0.5f * tintRoll;
                        float y = TerrainMirror.Forest(x, z, config) - 0.4f * scale;

                        int draw = DrawOf(broadleaf, !near, shape);
                        planted[draw].Add(new StandInstance(new Vector4(x, y, z, scale),
                            new Vector4(MathF.Sin(yaw), MathF.Cos(yaw), stretch, tint)));
                    }
            }

            Ring(stand.InnerRadius, stand.NearRadius, stand.Spacing, near: true);
            Ring(stand.NearRadius, stand.OuterRadius, stand.FarSpacing, near: false);

            foreach (List<StandInstance> list in planted)
                list.Sort((p, q) => (p.Place.X * p.Place.X + p.Place.Z * p.Place.Z).CompareTo(q.Place.X * q.Place.X + q.Place.Z * q.Place.Z));
            return planted;
        }

        //The planted wood's cells and their neighbours
        private static void Take(HashSet<(int, int)> taken, ModelInstance[][] buckets)
        {
            foreach (ModelInstance[] bucket in buckets)
                foreach (ModelInstance tree in bucket)
                {
                    int cx = (int)MathF.Floor(tree.World.M41 / TAKEN_CELL), cz = (int)MathF.Floor(tree.World.M43 / TAKEN_CELL);
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++) taken.Add((cx + dx, cz + dz));
                }
        }
    }
}
