using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The research camp on the polar ice</b> (#747). The owner: "improve it with objects and details, for example a polar
    /// station, a mast with a flag, a tracked vehicle"; nothing stood on the icesheet at all. The references
    /// (<c>C:\Users\panrd\AI\sd\out\747-zimage</c>, <c>747-klein</c>) all draw the same camp, and this builds it as hand-placed
    /// landmarks for the scene's <see cref="PlantPass"/>, casting into the sun's map:
    /// <list type="bullet">
    /// <item><b>The station</b>: three red modules on stilts with snow on their roofs, joined by a railed walkway, an antenna
    /// on the middle one.</item>
    /// <item><b>A mast with a white flag</b> stiff in the wind (the Moon's frozen cloth, #746, at the mast's scale), and a
    /// small <b>wind turbine</b>.</item>
    /// <item><b>A tracked vehicle</b>: an orange cab on a dark chassis over two wide track units, towing a sled of crates.</item>
    /// <item>Red <b>fuel drums</b>, and <b>snow drifts</b> banked against everything, so nothing stands on top of the ice (the
    /// fault #670 found with the trunks).</item>
    /// </list>
    /// One camp, on ice the terrain clears of crevasses round it (<see cref="PolarSceneConfig.CampClearRadius"/>), where the scene's
    /// orbit and its tour pass it; every object stands on its own
    /// spot of the ground (<see cref="TerrainMirror.Polar"/>), whose swells are metres high across a camp this size.
    /// </summary>
    public sealed class PolarPlanting : IDisposable
    {
        private readonly List<IDisposable> _owned = new();

        /// <summary>Everything, in draw order.</summary>
        public ScatterBucket[] Buckets { get; }

        //The materials, linear
        private static readonly Vector3 RED = new(0.42f, 0.05f, 0.025f), ORANGE = new(0.60f, 0.17f, 0.025f);
        private static readonly Vector3 SNOW = new(0.80f, 0.83f, 0.88f), STEEL = new(0.28f, 0.29f, 0.30f), DARK = new(0.035f, 0.04f, 0.045f);
        private static readonly Vector3 CRATE = new(0.20f, 0.13f, 0.06f), CRATE_GREEN = new(0.10f, 0.12f, 0.08f);
        private static readonly Vector3 FLAG = new(0.72f, 0.73f, 0.74f);

        /// <param name="height">The icesheet's ground (<see cref="TerrainMirror.Polar"/>).</param>
        /// <param name="centre">Where the camp stands (<see cref="PolarSceneConfig.CampCentre"/>), on ice the terrain clears of
        /// crevasses round it: they cover the plain past the clearing with parallel slots, and no spot was clear of them by luck.</param>
        public PolarPlanting(GraphicsDevice device, Func<float, float, float> height, Vector2 centre)
        {
            var buckets = new List<ScatterBucket>();

            //The camp's own frame: its front (local -Z) turned to the arena
            Vector2 toArena = Vector2.Normalize(-centre);
            float yaw = MathF.Atan2(-toArena.X, -toArena.Y);
            Matrix turn = Matrix.CreateRotationY(yaw);

            //An object at a local spot of the camp, turned with it and by its own turn, seated on the ground's mean under it
            Matrix At(float localX, float localZ, float ownTurn, float reach, float scale = 1f)
            {
                Vector3 offset = Vector3.Transform(new Vector3(localX, 0f, localZ), turn);
                float x = centre.X + offset.X, z = centre.Y + offset.Z;
                float ground = (height(x, z) + height(x + reach, z) + height(x - reach, z) + height(x, z + reach) + height(x, z - reach)) * 0.2f;
                return Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw + ownTurn) * Matrix.CreateTranslation(x, ground, z);
            }

            void Add(Dictionary<Vector3, MeshBuilder> parts, IReadOnlyList<Matrix> worlds, float radius)
            {
                var instances = new List<ModelInstance>();
                foreach (Matrix w in worlds) instances.Add(new ModelInstance(w, Vector4.Zero));
                foreach ((Vector3 colour, MeshBuilder part) in parts)
                {
                    (List<VertexPositionNormalTexture> v, List<int> idx) = part.Geometry;
                    var mesh = Own(new UploadedMesh(device, v, idx, new BoundingSphere(Vector3.Zero, radius)));
                    buckets.Add(Own(new ScatterBucket(device, mesh, instances, colour, colour * 1.08f, dapple: 0f, bark: 0f, detailOnly: false)));
                }
            }

            Add(Station(), new[] { At(0f, 0f, 0f, 9f) }, 16f);
            Add(Turbine(), new[] { At(-14f, 5f, 0.5f, 1f) }, 7f);
            Add(Vehicle(), new[] { At(-4f, -12f, 0.35f, 2.5f) }, 4f);
            Add(Sled(), new[] { At(-9.3f, -14.2f, 0.35f, 2f) }, 3f);
            Add(Mast(), new[] { At(13f, -3.5f, 0f, 0.5f) }, 12f);

            //The flag: the Moon's frozen cloth (#746), at the mast's top and twice its own size
            Matrix mast = At(13f, -3.5f, 0f, 0.5f);
            IProceduralMesh cloth = Own(MoonPlanting.FlagCloth(device));
            const float FLAG_SCALE = 2f;
            var flag = new List<ModelInstance>
            {
                new(Matrix.CreateScale(FLAG_SCALE) * Matrix.CreateRotationY(yaw + 1.2f)
                    * Matrix.CreateTranslation(mast.Translation + new Vector3(0f, MAST_HEIGHT - MoonPlanting.FlagBarY * FLAG_SCALE, 0f)), Vector4.Zero),
            };
            buckets.Add(Own(new ScatterBucket(device, cloth, flag, FLAG, FLAG, dapple: 0f, bark: 0f, detailOnly: false)));

            //The fuel drums: a cluster by the station's west end, a few toppled
            var drum = new MeshBuilder();
            drum.AddTube(new Vector3(0f, 0.45f, 0f), Vector3.UnitY, Vector3.UnitX, 0.45f, 0.29f, 12);
            var drums = new List<Matrix>();
            Random dice = new(7471);
            for (int i = 0; i < 11; i++)
            {
                float lx = -9.5f + (i % 4) * 0.66f + (float)dice.NextDouble() * 0.1f, lz = -3.2f + (i / 4) * 0.66f;
                Matrix world = At(lx, lz, (float)dice.NextDouble() * 6f, 0.3f);
                //The last one lies on its side: turned onto it, its axis then along -X, and lifted by its radius
                if (i == 10) world = Matrix.CreateRotationZ(MathHelper.PiOver2) * Matrix.CreateTranslation(0.45f, 0.29f, 0f) * At(-11.2f, -1.8f, 0.8f, 0.4f);
                drums.Add(world);
            }
            Add(new Dictionary<Vector3, MeshBuilder> { [RED * 0.85f] = drum }, drums, 1f);

            //The drifts: low mounds of snow banked against the stilts, the drums, the vehicle's tracks and the mast's foot
            var drift = Own(new RockMesh(device, 1f, 0.32f, 18, 3.3f));
            var drifts = new List<ModelInstance>();
            (float X, float Z, float Size)[] banks =
            {
                (-5f, 2.2f, 4.2f), (2.5f, 2.6f, 3.6f), (8.5f, 2.2f, 4f), (-9f, -1.5f, 2.4f), (13f, -3f, 1.2f),
                (-3f, -11f, 2.2f), (-14f, 5.5f, 1.4f), (6f, -4.5f, 2.6f), (-1f, 4.5f, 3f),
            };
            foreach ((float x, float z, float size) in banks)
            {
                Matrix world = At(x, z, x * 0.37f, size * 0.6f);
                drifts.Add(new ModelInstance(Matrix.CreateScale(size, size * 1.1f, size * 0.8f) * Matrix.CreateTranslation(0f, -size * 0.06f, 0f) * world,
                    Vector4.Zero));
            }
            buckets.Add(Own(new ScatterBucket(device, drift, drifts, SNOW, SNOW, dapple: 0f, bark: 0f, detailOnly: false)));

            Buckets = buckets.ToArray();
        }

        //The station's modules (local X along the camp, Z across it, -Z its front to the arena): centre X, half-length,
        //centre Z, and the deck's height they stand at
        private static readonly (float X, float HalfLength, float Z)[] MODULES = { (-5f, 3.2f, 0f), (2.4f, 2.4f, 0.4f), (8.6f, 2.8f, -0.2f) };
        private const float DECK = 1.4f, MODULE_HALF_HEIGHT = 1.3f, MODULE_HALF_DEPTH = 1.6f;
        private const float MAST_HEIGHT = 11f;

        private static Dictionary<Vector3, MeshBuilder> Station()
        {
            var red = new MeshBuilder();
            var snow = new MeshBuilder();
            var steel = new MeshBuilder();
            var dark = new MeshBuilder();

            foreach ((float x, float half, float z) in MODULES)
            {
                float y = DECK + MODULE_HALF_HEIGHT;
                red.AddBox(new Vector3(x, y, z), new Vector3(half, 0f, 0f), new Vector3(0f, MODULE_HALF_HEIGHT, 0f), new Vector3(0f, 0f, MODULE_HALF_DEPTH));
                //Snow on the roof, overhanging it a little
                snow.AddBox(new Vector3(x, y + MODULE_HALF_HEIGHT + 0.07f, z), new Vector3(half + 0.06f, 0f, 0f), new Vector3(0f, 0.07f, 0f),
                    new Vector3(0f, 0f, MODULE_HALF_DEPTH + 0.06f));
                //Windows on the front and a door at the walkway end
                for (int w = -1; w <= 1; w += 2)
                    dark.AddBox(new Vector3(x + w * half * 0.45f, y + 0.25f, z - MODULE_HALF_DEPTH - 0.02f), new Vector3(0.38f, 0f, 0f),
                        new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0f, 0.03f));
                //Stilts: four a module, down into the snow
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        steel.AddTube(new Vector3(x + sx * (half - 0.4f), DECK * 0.5f - 0.8f, z + sz * (MODULE_HALF_DEPTH - 0.3f)), Vector3.UnitY,
                            Vector3.UnitX, DECK * 0.5f + 0.8f, 0.09f, 8);
                //The module's floor frame
                steel.AddBox(new Vector3(x, DECK - 0.06f, z), new Vector3(half + 0.1f, 0f, 0f), new Vector3(0f, 0.06f, 0f),
                    new Vector3(0f, 0f, MODULE_HALF_DEPTH + 0.1f));
            }

            //The walkway along the front, its posts and rail
            const float WALK_Z = -MODULE_HALF_DEPTH - 0.85f, WALK_FROM = -8.4f, WALK_TO = 11.6f;
            steel.AddBox(new Vector3((WALK_FROM + WALK_TO) * 0.5f, DECK - 0.05f, WALK_Z), new Vector3((WALK_TO - WALK_FROM) * 0.5f, 0f, 0f),
                new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, 0.7f));
            for (float px = WALK_FROM + 0.2f; px <= WALK_TO; px += 1.6f)
            {
                steel.AddTube(new Vector3(px, DECK + 0.5f, WALK_Z - 0.62f), Vector3.UnitY, Vector3.UnitX, 0.5f, 0.035f, 6);
                steel.AddTube(new Vector3(px, DECK * 0.5f - 0.8f, WALK_Z), Vector3.UnitY, Vector3.UnitX, DECK * 0.5f + 0.8f, 0.07f, 6);
            }
            steel.AddTube(new Vector3((WALK_FROM + WALK_TO) * 0.5f + 0.1f, DECK + 1.0f, WALK_Z - 0.62f), Vector3.UnitX, Vector3.UnitY,
                (WALK_TO - WALK_FROM) * 0.5f, 0.035f, 6);

            //The antenna on the middle module's roof: a pole and two crossbars
            float roof = DECK + MODULE_HALF_HEIGHT * 2f + 0.14f;
            steel.AddTube(new Vector3(2.4f, roof + 1.6f, 0.6f), Vector3.UnitY, Vector3.UnitX, 1.6f, 0.05f, 6);
            steel.AddTube(new Vector3(2.4f, roof + 2.6f, 0.6f), Vector3.UnitX, Vector3.UnitY, 0.6f, 0.03f, 6);
            steel.AddTube(new Vector3(2.4f, roof + 3.0f, 0.6f), Vector3.UnitX, Vector3.UnitY, 0.4f, 0.03f, 6);

            return new Dictionary<Vector3, MeshBuilder> { [RED] = red, [SNOW] = snow, [STEEL] = steel, [DARK] = dark };
        }

        private static Dictionary<Vector3, MeshBuilder> Turbine()
        {
            var steel = new MeshBuilder();
            var white = new MeshBuilder();
            const float TOWER = 7.5f;
            steel.AddTube(new Vector3(0f, TOWER * 0.5f - 0.5f, 0f), Vector3.UnitY, Vector3.UnitX, TOWER * 0.5f + 0.5f, 0.11f, 8);
            white.AddBox(new Vector3(0f, TOWER + 0.1f, -0.15f), new Vector3(0.18f, 0f, 0f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0f, 0.5f));
            //Three blades round the hub, in the plane square to the wind (local Z)
            for (int b = 0; b < 3; b++)
            {
                float a = b * MathHelper.TwoPi / 3f + 0.3f;
                Vector3 along = new(MathF.Cos(a), MathF.Sin(a), 0f);
                Vector3 across = new(-MathF.Sin(a), MathF.Cos(a), 0f);
                white.AddBox(new Vector3(0f, TOWER + 0.1f, -0.68f) + along * 1.5f, along * 1.35f, across * 0.11f, new Vector3(0f, 0f, 0.025f));
            }
            return new Dictionary<Vector3, MeshBuilder> { [STEEL] = steel, [SNOW * 0.95f] = white };
        }

        private static Dictionary<Vector3, MeshBuilder> Mast()
        {
            var steel = new MeshBuilder();
            steel.AddTube(new Vector3(0f, MAST_HEIGHT * 0.5f - 0.6f, 0f), Vector3.UnitY, Vector3.UnitX, MAST_HEIGHT * 0.5f + 0.6f, 0.07f, 8);
            return new Dictionary<Vector3, MeshBuilder> { [STEEL] = steel };
        }

        //The tracked vehicle, nose to local +X: two track units, the chassis, the orange cab with its windows and a snowed roof,
        //the rear deck, and the dozer blade's frame in front
        private static Dictionary<Vector3, MeshBuilder> Vehicle()
        {
            var dark = new MeshBuilder();
            var orange = new MeshBuilder();
            var snow = new MeshBuilder();
            var steel = new MeshBuilder();

            foreach (float side in new[] { -1.15f, 1.15f })
            {
                dark.AddBox(new Vector3(0f, 0.48f, side), new Vector3(1.6f, 0f, 0f), new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0f, 0.38f));
                foreach (float end in new[] { -1.6f, 1.6f })
                    dark.AddTube(new Vector3(end, 0.48f, side), Vector3.UnitZ, Vector3.UnitY, 0.38f, 0.42f, 10);
            }
            dark.AddBox(new Vector3(0f, 1.05f, 0f), new Vector3(1.75f, 0f, 0f), new Vector3(0f, 0.18f, 0f), new Vector3(0f, 0f, 0.95f));
            orange.AddBox(new Vector3(0.55f, 1.95f, 0f), new Vector3(0.95f, 0f, 0f), new Vector3(0f, 0.72f, 0f), new Vector3(0f, 0f, 0.88f));
            orange.AddBox(new Vector3(-1.15f, 1.42f, 0f), new Vector3(0.6f, 0f, 0f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0f, 0.9f));
            snow.AddBox(new Vector3(0.55f, 2.71f, 0f), new Vector3(0.97f, 0f, 0f), new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, 0.9f));
            //The windscreen and the side windows
            dark.AddBox(new Vector3(1.51f, 2.15f, 0f), new Vector3(0.02f, 0f, 0f), new Vector3(0f, 0.4f, 0f), new Vector3(0f, 0f, 0.75f));
            foreach (float side in new[] { -0.9f, 0.9f })
                dark.AddBox(new Vector3(0.65f, 2.15f, side), new Vector3(0.6f, 0f, 0f), new Vector3(0f, 0.38f, 0f), new Vector3(0f, 0f, 0.02f));
            //The blade's frame and the blade
            steel.AddTube(new Vector3(2.15f, 0.9f, 0f), Vector3.UnitZ, Vector3.UnitY, 0.85f, 0.07f, 8);
            steel.AddBox(new Vector3(2.45f, 0.75f, 0f), new Vector3(0.05f, 0f, 0f), new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0f, 1.3f));
            return new Dictionary<Vector3, MeshBuilder> { [DARK] = dark, [ORANGE] = orange, [SNOW] = snow, [STEEL] = steel };
        }

        //The sled it tows: a deck on two runners, a tow bar to the vehicle, and crates lashed on
        private static Dictionary<Vector3, MeshBuilder> Sled()
        {
            var steel = new MeshBuilder();
            var crate = new MeshBuilder();
            var green = new MeshBuilder();
            steel.AddBox(new Vector3(0f, 0.42f, 0f), new Vector3(1.7f, 0f, 0f), new Vector3(0f, 0.07f, 0f), new Vector3(0f, 0f, 0.95f));
            foreach (float side in new[] { -0.8f, 0.8f })
                steel.AddBox(new Vector3(0f, 0.15f, side), new Vector3(1.85f, 0f, 0f), new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0f, 0.07f));
            steel.AddTube(new Vector3(2.45f, 0.42f, 0f), Vector3.UnitX, Vector3.UnitY, 0.75f, 0.05f, 6);
            crate.AddBox(new Vector3(-0.85f, 0.95f, -0.3f), new Vector3(0.55f, 0f, 0f), new Vector3(0f, 0.46f, 0f), new Vector3(0f, 0f, 0.5f));
            crate.AddBox(new Vector3(0.2f, 0.82f, 0.35f), new Vector3(0.45f, 0f, 0f), new Vector3(0f, 0.33f, 0f), new Vector3(0f, 0f, 0.45f));
            green.AddBox(new Vector3(0.25f, 0.95f, -0.4f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0.46f, 0f), new Vector3(0f, 0f, 0.42f));
            green.AddBox(new Vector3(1.15f, 0.8f, 0.05f), new Vector3(0.4f, 0f, 0f), new Vector3(0f, 0.31f, 0f), new Vector3(0f, 0f, 0.6f));
            return new Dictionary<Vector3, MeshBuilder> { [STEEL] = steel, [CRATE] = crate, [CRATE_GREEN] = green };
        }

        private T Own<T>(T item) where T : IDisposable
        {
            _owned.Add(item);
            return item;
        }

        public void Dispose()
        {
            foreach (IDisposable d in _owned) d.Dispose();
            _owned.Clear();
        }
    }
}
