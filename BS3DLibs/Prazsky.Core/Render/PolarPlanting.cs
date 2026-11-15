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
    /// One camp, on ice the terrain clears of crevasses round it (<see cref="PolarSceneConfig.CampClearRadius"/>), where the play
    /// camera sees it over the island's deck and the tour films it; every object stands on its own spot of the ground
    /// (<see cref="TerrainMirror.Polar"/>), whose swells are metres high across a camp this size.
    /// <para>
    /// <b>Built at <see cref="SCALE"/> times its own figures</b> (#1128). The owner: "the camp should be bigger and placed so that
    /// it is visible even while playing". The play camera stands 1.1 over the deck (<c>GameCameraFit</c>'s lens floor), so the
    /// deck hides the plain out to about 300 units and everything on it lower than about 6 minus a fiftieth of its distance:
    /// at its own figures the modules' roofs (4 units up) were under that line wherever the camp could stand in the view. Every
    /// object, its place in the camp and its size are scaled together, so the camp is the one the owner accepted (#747), only
    /// bigger: its layout below is in the camp's own units, and <see cref="Seat"/> scales it. The figures it then has are in
    /// <c>docs/scenes/polar.md</c>.
    /// </para>
    /// </summary>
    public sealed class PolarPlanting : IDisposable
    {
        /// <summary>
        /// How much bigger the camp is built than its own figures (#1128): a module's roof 10 units up and 16 long, the mast 27.5
        /// tall, the vehicle 9 long. What it has to be is set by the play view: see the class remarks.
        /// </summary>
        public const float SCALE = 2.5f;

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
        /// <param name="turn">How far its front is turned off the arena, radians (<see cref="PolarSceneConfig.CampTurn"/>).</param>
        public PolarPlanting(GraphicsDevice device, Func<float, float, float> height, Vector2 centre, float turn = 0f)
        {
            var buckets = new List<ScatterBucket>();
            float yaw = Yaw(centre, turn);

            //An object at a local spot of the camp, turned with it and by its own turn, seated on the ground's mean under it
            Matrix At(float localX, float localZ, float ownTurn, float reach) => Seat(centre, yaw, height, localX, localZ, ownTurn, reach);

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

            Add(Station(), new[] { At(STATION.X, STATION.Z, 0f, STATION_REACH) }, 16f);
            Add(Turbine(), new[] { At(TURBINE.X, TURBINE.Z, TURBINE_TURN, 1f) }, 7f);
            Add(Vehicle(), new[] { At(VEHICLE.X, VEHICLE.Y, VEHICLE_TURN, VEHICLE_REACH) }, 4f);
            Add(Sled(), new[] { At(SledSpot.X, SledSpot.Y, VEHICLE_TURN, 2f) }, 3.3f);
            Add(Mast(), new[] { At(MAST.X, MAST.Y, 0f, MAST_REACH) }, 12f);

            //The flag: the Moon's frozen cloth (#746), at the mast's top and FLAG_SCALE times its own size
            IProceduralMesh cloth = Own(MoonPlanting.FlagCloth(device));
            var flag = new List<ModelInstance> { new(FlagLocal * At(MAST.X, MAST.Y, 0f, MAST_REACH), Vector4.Zero) };
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
            foreach ((float x, float z, float size) in BANKS)
            {
                Matrix world = At(x, z, x * 0.37f, size * 0.6f);
                drifts.Add(new ModelInstance(Matrix.CreateScale(size, size * 1.1f, size * 0.8f) * Matrix.CreateTranslation(0f, -size * 0.06f, 0f) * world,
                    Vector4.Zero));
            }
            buckets.Add(Own(new ScatterBucket(device, drift, drifts, SNOW, SNOW, dapple: 0f, bark: 0f, detailOnly: false)));

            Buckets = buckets.ToArray();
        }

        /// <summary>
        /// A landmark of the camp in the world (#1128): what the play view and the intro's camp shot hold in the frame. Its foot
        /// is where it meets what it stands on (a module's floor, the ground under the mast), its top the highest point of it.
        /// </summary>
        public readonly record struct Landmark(string Name, Vector3 Foot, Vector3 Top);

        /// <summary>
        /// The camp's landmarks as <see cref="PolarPlanting"/> stands them, without a device: each module's floor and roof at its
        /// middle, the station's two ends, the mast's foot and top, the flag's cloth from its hem to its bar, the vehicle's tracks
        /// and its cab's roof, and the turbine's foot and hub.
        /// </summary>
        public static Landmark[] Landmarks(Vector2 centre, float turn, Func<float, float, float> height)
        {
            float yaw = Yaw(centre, turn);
            Matrix station = Seat(centre, yaw, height, STATION.X, STATION.Z, 0f, STATION_REACH);
            Matrix mast = Seat(centre, yaw, height, MAST.X, MAST.Y, 0f, MAST_REACH);
            Matrix vehicle = Seat(centre, yaw, height, VEHICLE.X, VEHICLE.Y, VEHICLE_TURN, VEHICLE_REACH);
            Matrix turbine = Seat(centre, yaw, height, TURBINE.X, TURBINE.Z, TURBINE_TURN, 1f);
            Matrix flag = FlagLocal * mast;

            var marks = new List<Landmark>();
            const float ROOF = DECK + MODULE_HALF_HEIGHT * 2f + ROOF_SNOW * 2f;
            for (int i = 0; i < MODULES.Length; i++)
            {
                (float x, _, float z) = MODULES[i];
                marks.Add(new Landmark($"module {i + 1}", Vector3.Transform(new Vector3(x, DECK, z), station),
                    Vector3.Transform(new Vector3(x, ROOF, z), station)));
            }

            //The station's two ends: the first module's and the last's outer walls, at their front corners
            (float firstX, float firstHalf, float firstZ) = MODULES[0];
            (float lastX, float lastHalf, float lastZ) = MODULES[^1];
            marks.Add(new Landmark("station's end", Vector3.Transform(new Vector3(firstX - firstHalf, DECK, firstZ - MODULE_HALF_DEPTH), station),
                Vector3.Transform(new Vector3(firstX - firstHalf, ROOF, firstZ - MODULE_HALF_DEPTH), station)));
            marks.Add(new Landmark("station's other end", Vector3.Transform(new Vector3(lastX + lastHalf, DECK, lastZ - MODULE_HALF_DEPTH), station),
                Vector3.Transform(new Vector3(lastX + lastHalf, ROOF, lastZ - MODULE_HALF_DEPTH), station)));

            marks.Add(new Landmark("mast", mast.Translation, Vector3.Transform(new Vector3(0f, MAST_HEIGHT, 0f), mast)));
            marks.Add(new Landmark("flag", Vector3.Transform(new Vector3(MoonPlanting.FlagWidth * 0.5f, MoonPlanting.FlagBarY - MoonPlanting.FlagDrop, 0f), flag),
                Vector3.Transform(new Vector3(MoonPlanting.FlagWidth * 0.5f, MoonPlanting.FlagBarY, 0f), flag)));
            marks.Add(new Landmark("vehicle", vehicle.Translation, Vector3.Transform(new Vector3(CAB_X, CAB_ROOF, 0f), vehicle)));
            marks.Add(new Landmark("turbine", turbine.Translation, Vector3.Transform(new Vector3(0f, TURBINE_TOWER + 0.1f, 0f), turbine)));
            return marks.ToArray();
        }

        /// <summary>
        /// How far from the camp's centre, on the ground, its farthest object reaches (#1128): the turbine's blades, the sled and
        /// the drifts, at <see cref="SCALE"/>. The crevasses keep <see cref="PolarSceneConfig.CampClearRadius"/> from it, which must
        /// hold this, and the intro's lenses keep off it.
        /// </summary>
        public static float Reach
        {
            get
            {
                float reach = 0f;
                foreach ((float x, float half, float z) in MODULES)
                    reach = MathF.Max(reach, new Vector2(MathF.Abs(x) + half, MathF.Abs(z) + MODULE_HALF_DEPTH).Length());
                reach = MathF.Max(reach, new Vector2(MathF.Max(-WALK_FROM, WALK_TO), -WALK_Z + WALK_HALF_WIDTH).Length());
                reach = MathF.Max(reach, new Vector2(TURBINE.X, TURBINE.Z).Length() + BLADE_REACH);
                reach = MathF.Max(reach, VEHICLE.Length() + VEHICLE_REACH);
                reach = MathF.Max(reach, SledSpot.Length() + SLED_REACH);
                reach = MathF.Max(reach, MAST.Length() + MoonPlanting.FlagWidth * FLAG_SCALE);
                foreach ((float x, float z, float size) in BANKS) reach = MathF.Max(reach, new Vector2(x, z).Length() + size);
                return reach * SCALE;
            }
        }

        /// <summary>The camp's tallest point over the ground under it (#1128): the mast's top, at <see cref="SCALE"/>.</summary>
        public const float Tallest = MAST_HEIGHT * SCALE;

        //The camp's frame: its front (local -Z) turned to the arena, and on by `turn`
        private static float Yaw(Vector2 centre, float turn)
        {
            Vector2 toArena = Vector2.Normalize(-centre);
            return MathF.Atan2(-toArena.X, -toArena.Y) + turn;
        }

        //An object at a local spot of the camp (its own units), turned with it and by its own turn, scaled by SCALE, seated on
        //the ground's mean under it at its reach
        private static Matrix Seat(Vector2 centre, float yaw, Func<float, float, float> height, float localX, float localZ, float ownTurn, float reach)
        {
            Vector3 offset = Vector3.Transform(new Vector3(localX, 0f, localZ) * SCALE, Matrix.CreateRotationY(yaw));
            float x = centre.X + offset.X, z = centre.Y + offset.Z, r = reach * SCALE;
            float ground = (height(x, z) + height(x + r, z) + height(x - r, z) + height(x, z + r) + height(x, z - r)) * 0.2f;
            return Matrix.CreateScale(SCALE) * Matrix.CreateRotationY(yaw + ownTurn) * Matrix.CreateTranslation(x, ground, z);
        }

        //The flag in the mast's own frame: the Moon's cloth at FLAG_SCALE, its bar at the mast's top, turned off the camp's front
        private const float FLAG_SCALE = 2f, FLAG_TURN = 1.2f;
        private static Matrix FlagLocal => Matrix.CreateScale(FLAG_SCALE) * Matrix.CreateRotationY(FLAG_TURN)
            * Matrix.CreateTranslation(0f, MAST_HEIGHT - MoonPlanting.FlagBarY * FLAG_SCALE, 0f);

        //Where each object stands in the camp's own units (local X along it, Z across it, -Z its front), its own turn, and the
        //reach its seat reads the ground over. The vehicle is parked before the station's +X end, the end nearest the play
        //camera once the camp is turned (#1128): at the -X end it stood behind the gun in the play view, and out beyond the
        //station's end at the frame's edge. The mast stands before the last module rather than at that end, where its top and
        //the flag stood behind the HUD's cluster profile at 16:9.
        private static readonly (float X, float Z) STATION = (0f, 0f), TURBINE = (-14f, 5f);
        private static readonly Vector2 VEHICLE = new(17f, -8f), MAST = new(8.5f, -3.6f);
        private const float STATION_REACH = 9f, VEHICLE_REACH = 2.5f, MAST_REACH = 0.5f;
        private const float VEHICLE_TURN = 0.25f, TURBINE_TURN = 0.5f;

        //The sled behind the vehicle on its tow line: the vehicle's tail and the sled's tow bar meet SLED_BEHIND back along the
        //vehicle's nose (local +X turned by its own VEHICLE_TURN in the camp's frame, which CreateRotationY makes (cos, -sin))
        private static Vector2 SledSpot => VEHICLE - new Vector2(MathF.Cos(VEHICLE_TURN), -MathF.Sin(VEHICLE_TURN)) * SLED_BEHIND;

        //The snow drifts: where, and how big
        private static readonly (float X, float Z, float Size)[] BANKS =
        {
            (-5f, 2.2f, 4.2f), (2.5f, 2.6f, 3.6f), (8.5f, 2.2f, 4f), (-9f, -1.5f, 2.4f), (8.5f, -3.4f, 1.2f),
            (17.5f, -9.9f, 2.2f), (-14f, 5.5f, 1.4f), (6f, -4.5f, 2.6f), (-1f, 4.5f, 3f),
        };

        //The station's modules (local X along the camp, Z across it, -Z its front to the arena): centre X, half-length,
        //centre Z, and the deck's height they stand at
        private static readonly (float X, float HalfLength, float Z)[] MODULES = { (-5f, 3.2f, 0f), (2.4f, 2.4f, 0.4f), (8.6f, 2.8f, -0.2f) };
        private const float DECK = 1.4f, MODULE_HALF_HEIGHT = 1.3f, MODULE_HALF_DEPTH = 1.6f, ROOF_SNOW = 0.07f;
        private const float MAST_HEIGHT = 11f;

        //The walkway along the front: its line across the camp, its ends and its half-width
        private const float WALK_Z = -MODULE_HALF_DEPTH - 0.85f, WALK_FROM = -8.4f, WALK_TO = 11.6f, WALK_HALF_WIDTH = 0.7f;

        //The turbine's tower, and how far its blades reach round the hub (each 1.35 either side of a point 1.5 out)
        private const float TURBINE_TOWER = 7.5f, BLADE_REACH = 2.85f;

        //The vehicle's cab: its middle along the vehicle, and its roof's top (the snow on it); and how far the sled reaches
        //from its middle (the tow bar's end)
        private const float CAB_X = 0.55f, CAB_ROOF = 2.76f, SLED_REACH = 3.2f;

        //From the vehicle's middle to the sled's: the vehicle's tracks reach 2 back, the sled's tow bar 3.2 forward
        private const float SLED_BEHIND = 5.3f;

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
                snow.AddBox(new Vector3(x, y + MODULE_HALF_HEIGHT + ROOF_SNOW, z), new Vector3(half + 0.06f, 0f, 0f), new Vector3(0f, ROOF_SNOW, 0f),
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
            steel.AddBox(new Vector3((WALK_FROM + WALK_TO) * 0.5f, DECK - 0.05f, WALK_Z), new Vector3((WALK_TO - WALK_FROM) * 0.5f, 0f, 0f),
                new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, WALK_HALF_WIDTH));
            for (float px = WALK_FROM + 0.2f; px <= WALK_TO; px += 1.6f)
            {
                steel.AddTube(new Vector3(px, DECK + 0.5f, WALK_Z - 0.62f), Vector3.UnitY, Vector3.UnitX, 0.5f, 0.035f, 6);
                steel.AddTube(new Vector3(px, DECK * 0.5f - 0.8f, WALK_Z), Vector3.UnitY, Vector3.UnitX, DECK * 0.5f + 0.8f, 0.07f, 6);
            }
            steel.AddTube(new Vector3((WALK_FROM + WALK_TO) * 0.5f + 0.1f, DECK + 1.0f, WALK_Z - 0.62f), Vector3.UnitX, Vector3.UnitY,
                (WALK_TO - WALK_FROM) * 0.5f, 0.035f, 6);

            //The antenna on the middle module's roof: a pole and two crossbars
            float roof = DECK + MODULE_HALF_HEIGHT * 2f + ROOF_SNOW * 2f;
            steel.AddTube(new Vector3(2.4f, roof + 1.6f, 0.6f), Vector3.UnitY, Vector3.UnitX, 1.6f, 0.05f, 6);
            steel.AddTube(new Vector3(2.4f, roof + 2.6f, 0.6f), Vector3.UnitX, Vector3.UnitY, 0.6f, 0.03f, 6);
            steel.AddTube(new Vector3(2.4f, roof + 3.0f, 0.6f), Vector3.UnitX, Vector3.UnitY, 0.4f, 0.03f, 6);

            return new Dictionary<Vector3, MeshBuilder> { [RED] = red, [SNOW] = snow, [STEEL] = steel, [DARK] = dark };
        }

        private static Dictionary<Vector3, MeshBuilder> Turbine()
        {
            var steel = new MeshBuilder();
            var white = new MeshBuilder();
            const float TOWER = TURBINE_TOWER;
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
            orange.AddBox(new Vector3(CAB_X, 1.95f, 0f), new Vector3(0.95f, 0f, 0f), new Vector3(0f, 0.72f, 0f), new Vector3(0f, 0f, 0.88f));
            orange.AddBox(new Vector3(-1.15f, 1.42f, 0f), new Vector3(0.6f, 0f, 0f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0f, 0.9f));
            snow.AddBox(new Vector3(CAB_X, CAB_ROOF - 0.05f, 0f), new Vector3(0.97f, 0f, 0f), new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, 0.9f));
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
