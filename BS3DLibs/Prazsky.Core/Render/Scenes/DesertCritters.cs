using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// THE DESERT'S CREATURES (#966). The owner: "beetles and scorpions crawling out of and back into the sand ... Players must
    /// feel: 'wow, that is beautiful!'" References: <c>966-zimage</c>, <c>966-klein</c> (a scorpion half out of its burrow with
    /// its tail raised, a darkling beetle leaving a trail, a beetle digging back in).
    /// <para>
    /// <b>Every creature's life is a pure function of the clock and its seed</b> (as the birds' and the fish's are), so nothing
    /// is tracked between frames and a capture at a given second is the same picture every run. Its cycle: hidden under the
    /// sand, it comes up out of a burrow nose first (<see cref="EMERGE_SECONDS"/>, a puff of sand and the hole's dark mouth
    /// round it), walks a short curved way on the dune's own height (<see cref="TerrainMirror.Desert"/>) with its legs in a
    /// tripod gait, stopping now and then, and digs back in nose down where it stands (<see cref="BURROW_SECONDS"/>, another
    /// puff and the hole). Each cycle comes up somewhere else round its home. The terrain writes depth, so the part still under
    /// the sand is hidden by the sand itself.
    /// </para>
    /// <para>
    /// They are larger than life (<see cref="BEETLE_SIZE"/>, <see cref="SCORPION_SIZE"/>): a real beetle is under a pixel from
    /// anywhere a lens stands in this game, which is #487's firefly lesson. They live in the clearing round the island and on
    /// the first dunes past it (<see cref="RING_IN"/> to <see cref="RING_OUT"/>), where the menu's flight and the chapter tour's
    /// close shot see sand.
    /// </para>
    /// </summary>
    internal sealed class DesertCritters : IDisposable
    {
        private const int BEETLES = 14, SCORPIONS = 6, COUNT = BEETLES + SCORPIONS;
        internal const float RING_IN = 34f, RING_OUT = 105f;
        internal const float BEETLE_SIZE = 0.55f, SCORPION_SIZE = 0.95f;

        //The cycle: its length (rolled per creature between the two), and its parts
        private const float PERIOD_MIN = 16f, PERIOD_SPREAD = 10f;
        internal const float EMERGE_SECONDS = 1.4f, BURROW_SECONDS = 1.6f;
        private const float HIDDEN_SHARE = 0.3f;
        //How far round its home a creature comes up, its walking speeds in body lengths a second and how sharply it turns
        private const float HOME_SPREAD = 7f;
        private const float BEETLE_SPEED = 1.3f, SCORPION_SPEED = 0.9f;
        private const float TURN = 0.35f;
        //Steps a body length: the gait's phase over the ground walked
        private const float STEPS_PER_LENGTH = 2.2f;

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly EffectTechnique _critterTechnique, _discTechnique;
        private readonly EffectParameter _view, _projection, _cameraPosition, _sunDirection, _sunColor, _zenith, _horizon, _time;
        private readonly VertexBuffer[] _meshes = new VertexBuffer[2];
        private readonly IndexBuffer[] _meshIndices = new IndexBuffer[2];
        private readonly int[] _meshTriangles = new int[2];
        private readonly DynamicVertexBuffer[] _instances = new DynamicVertexBuffer[2];
        private readonly ModelInstance[][] _instanceData = { new ModelInstance[BEETLES], new ModelInstance[SCORPIONS + 1] };

        //THE FEATURED SCORPION (#966): the chapter tour's close shot cues one to come up out of the sand where its lens is
        //framed (SceneRenderer.CueDesertCritter, DesertIntroShots), at the moment the shot comes on screen, so the shot need
        //not wait for one of the others' cycles to bring one there. It comes up, walks a few lengths across the frame and digs
        //back in (FEATURE_WALK_SECONDS), and is gone after. NaN onset: none cued.
        private Vector2 _featureAt;
        private float _featureYaw, _featureOnset = float.NaN;
        internal const float FEATURE_WALK_SECONDS = 4.5f;
        private readonly int[] _counts = new int[2];

        //The soft discs: a shadow under each creature, the burrow's hole, the puffs; four vertices a disc
        private const int MAX_DISCS = COUNT * 8 + BEETLES * TRAIL_PRINTS;

        //A beetle's trail (the references' double line of footprints): this many prints behind it, a print this many of its
        //lengths apart and this far either side of its line
        private const int TRAIL_PRINTS = 14;
        private const float PRINT_SPACING = 0.22f, PRINT_SIDE = 0.16f;
        private readonly VertexPositionColorTexture[] _discs = new VertexPositionColorTexture[MAX_DISCS * 4];
        private readonly DynamicVertexBuffer _discBuffer;
        private readonly IndexBuffer _discIndices;

        private readonly Func<float, float, float> _ground;
        private readonly float _hazeDistance;

        public DesertCritters(GraphicsDevice device, ContentManager content, DesertSceneConfig config)
        {
            _device = device;
            _ground = (x, z) => TerrainMirror.Desert(x, z, config);
            _hazeDistance = config.HorizonHazeDistance;

            _effect = content.Load<Effect>("Shaders/Critter");
            EffectParameterCollection p = _effect.Parameters;
            _view = p["View"];
            _projection = p["Projection"];
            _cameraPosition = p["CameraPosition"];
            _sunDirection = p["SunDirection"];
            _sunColor = p["SunColor"];
            _zenith = p["ZenithColor"];
            _horizon = p["HorizonColor"];
            _time = p["Time"];
            p["HazeDistance"].SetValue(_hazeDistance);
            _critterTechnique = _effect.Techniques["Critter"];
            _discTechnique = _effect.Techniques["Disc"];

            Upload(0, CritterMesh.Beetle());
            Upload(1, CritterMesh.Scorpion());
            _instances[0] = new DynamicVertexBuffer(device, ModelInstance.VertexDeclaration, BEETLES, BufferUsage.WriteOnly);
            _instances[1] = new DynamicVertexBuffer(device, ModelInstance.VertexDeclaration, SCORPIONS + 1, BufferUsage.WriteOnly);

            _discBuffer = new DynamicVertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, _discs.Length, BufferUsage.WriteOnly);
            var quad = new short[MAX_DISCS * 6];
            for (int d = 0; d < MAX_DISCS; d++)
            {
                int v = d * 4, i = d * 6;
                quad[i] = (short)v; quad[i + 1] = (short)(v + 1); quad[i + 2] = (short)(v + 2);
                quad[i + 3] = (short)v; quad[i + 4] = (short)(v + 2); quad[i + 5] = (short)(v + 3);
            }
            _discIndices = new IndexBuffer(device, IndexElementSize.SixteenBits, quad.Length, BufferUsage.WriteOnly);
            _discIndices.SetData(quad);
        }

        private void Upload(int kind, (VertexPositionNormalTexture[] Vertices, short[] Indices) mesh)
        {
            _meshes[kind] = new VertexBuffer(_device, VertexPositionNormalTexture.VertexDeclaration, mesh.Vertices.Length, BufferUsage.WriteOnly);
            _meshes[kind].SetData(mesh.Vertices);
            _meshIndices[kind] = new IndexBuffer(_device, IndexElementSize.SixteenBits, mesh.Indices.Length, BufferUsage.WriteOnly);
            _meshIndices[kind].SetData(mesh.Indices);
            _meshTriangles[kind] = mesh.Indices.Length / 3;
        }

        /// <summary>Where one creature is at a moment (see the class doc).</summary>
        internal readonly struct Pose
        {
            public readonly Vector3 Position;   //its foot, on the sand
            public readonly float Yaw, Pitch;   //its heading (radians round Y from +Z) and its nose's tilt (up positive)
            public readonly float Sunk;         //how far under the sand, in its own heights (0 standing on it)
            public readonly float Gait;         //the gait's phase
            public readonly Vector3 Burrow;     //the burrow this cycle came out of (or goes into)
            public readonly float Emerging, Burrowing; //0 to 1 while either is under way, else 0
            public readonly Vector2 Start;      //where this cycle's walk began, its heading and its turn, and how far it has walked
            public readonly float Heading, Turn, Walked;

            public Pose(Vector3 position, float yaw, float pitch, float sunk, float gait, Vector3 burrow, float emerging, float burrowing,
                Vector2 start, float heading, float turn, float walked)
            {
                Position = position; Yaw = yaw; Pitch = pitch; Sunk = sunk; Gait = gait; Burrow = burrow;
                Emerging = emerging; Burrowing = burrowing; Start = start; Heading = heading; Turn = turn; Walked = walked;
            }
        }

        internal static bool IsScorpion(int i) => i >= BEETLES;

        /// <summary>Cues the featured scorpion to come up at <paramref name="at"/> at <paramref name="onset"/>, walking along <paramref name="yaw"/>.</summary>
        internal void Feature(Vector2 at, float yaw, float onset)
        {
            _featureAt = at;
            _featureYaw = yaw;
            _featureOnset = onset;
        }

        /// <summary>The featured scorpion's pose, on its own short timeline from its cue; false before and after it.</summary>
        internal static bool TryFeaturePose(Vector2 at, float yaw, float onset, float time, Func<float, float, float> ground, out Pose pose)
        {
            pose = default;
            float local = time - onset;
            float total = EMERGE_SECONDS + FEATURE_WALK_SECONDS + BURROW_SECONDS;
            if (float.IsNaN(onset) || local < 0f || local > total) return false;

            float speed = SCORPION_SPEED * SCORPION_SIZE * 0.8f;
            float walked = Math.Clamp(local - EMERGE_SECONDS, 0f, FEATURE_WALK_SECONDS);
            float distance = speed * (walked - 0.35f * MathF.Sin(walked * 1.9f) / 1.9f);
            Vector2 p = Arc(at, yaw, 0f, distance, out float heading);
            float emerging = Math.Clamp(local / EMERGE_SECONDS, 0f, 1f);
            float burrowing = Math.Clamp((local - EMERGE_SECONDS - FEATURE_WALK_SECONDS) / BURROW_SECONDS, 0f, 1f);
            float sunk = (1f - Smooth(emerging)) + Smooth(burrowing);
            float pitch = 0.6f * (1f - Smooth(emerging)) * (emerging < 1f ? 1f : 0f) - 0.55f * Smooth(burrowing);
            pose = new Pose(new Vector3(p.X, ground(p.X, p.Y), p.Y), heading, pitch, sunk, distance / SCORPION_SIZE * STEPS_PER_LENGTH * MathHelper.TwoPi,
                new Vector3(burrowing > 0f ? p.X : at.X, 0f, burrowing > 0f ? p.Y : at.Y), emerging < 1f ? emerging : 0f, burrowing,
                at, yaw, 0f, distance);
            return true;
        }
        internal static int Count => COUNT;

        /// <summary>The pose of creature <paramref name="i"/> at <paramref name="time"/>, false while it is hidden.</summary>
        internal bool TryPose(int i, float time, out Pose pose) => TryPose(i, time, _ground, out pose);

        internal static bool TryPose(int i, float time, Func<float, float, float> ground, out Pose pose)
        {
            pose = default;
            float period = PERIOD_MIN + PERIOD_SPREAD * Unit(i, 1);
            float clock = time + period * Unit(i, 2) * 7f;
            int cycle = (int)MathF.Floor(clock / period);
            float local = clock - cycle * period;
            float hidden = period * HIDDEN_SHARE;
            if (local < hidden) return false;

            bool scorpion = IsScorpion(i);
            float size = scorpion ? SCORPION_SIZE : BEETLE_SIZE;
            float speed = (scorpion ? SCORPION_SPEED : BEETLE_SPEED) * size;

            //Its home round the island, and this cycle's burrow round its home
            float homeAngle = MathHelper.TwoPi * Unit(i, 3);
            float homeRadius = MathHelper.Lerp(RING_IN, RING_OUT, MathF.Sqrt(Unit(i, 4)));
            var home = new Vector2(MathF.Cos(homeAngle), MathF.Sin(homeAngle)) * homeRadius;
            float burrowAngle = MathHelper.TwoPi * Unit(i, 100 + cycle);
            Vector2 start = home + new Vector2(MathF.Cos(burrowAngle), MathF.Sin(burrowAngle)) * (HOME_SPREAD * Unit(i, 200 + cycle));

            //The walk: on an arc from the burrow, the heading rolled each cycle, the speed stopping and starting
            float walk = local - hidden - EMERGE_SECONDS;
            float walkSeconds = period - hidden - EMERGE_SECONDS - BURROW_SECONDS;
            float walked = Math.Clamp(walk, 0f, walkSeconds);
            float distance = speed * (walked - 0.35f * MathF.Sin(walked * 1.9f + i) / 1.9f + 0.35f * MathF.Sin(i) / 1.9f);
            float heading = MathHelper.TwoPi * Unit(i, 300 + cycle);
            float turn = TURN / size * (Unit(i, 400 + cycle) - 0.5f) * 2f;
            Vector2 at = Arc(start, heading, turn, distance, out float yaw);

            //Up out of the sand nose first, down into it nose first where it stands
            float emerging = Math.Clamp((local - hidden) / EMERGE_SECONDS, 0f, 1f);
            float burrowing = Math.Clamp((local - (period - BURROW_SECONDS)) / BURROW_SECONDS, 0f, 1f);
            float sunk = (1f - Smooth(emerging)) + Smooth(burrowing);
            float pitch = 0.6f * (1f - Smooth(emerging)) * (emerging < 1f ? 1f : 0f) - 0.55f * Smooth(burrowing);
            float y = ground(at.X, at.Y);
            pose = new Pose(new Vector3(at.X, y, at.Y), yaw, pitch, sunk, distance / size * STEPS_PER_LENGTH * MathHelper.TwoPi,
                new Vector3(burrowing > 0f ? at.X : start.X, 0f, burrowing > 0f ? at.Y : start.Y),
                emerging < 1f ? emerging : 0f, burrowing, start, heading, turn, distance);
            return true;
        }

        //Along an arc of curvature `turn` (radians a unit) from `start` heading `heading` (radians from +Z towards +X)
        private static Vector2 Arc(Vector2 start, float heading, float turn, float distance, out float yaw)
        {
            yaw = heading + turn * distance;
            if (MathF.Abs(turn) < 1e-3f) return start + new Vector2(MathF.Sin(heading), MathF.Cos(heading)) * distance;
            return start + new Vector2(MathF.Cos(heading) - MathF.Cos(yaw), MathF.Sin(yaw) - MathF.Sin(heading)) / turn;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        //A hash of a creature and a salt to 0..1
        private static float Unit(int i, int salt)
        {
            uint h = (uint)(i * 374761393 + salt * 668265263) ^ 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        /// <summary>Draws the creatures that are above the sand, their shadows, their burrows and the sand they throw.</summary>
        public void Draw(in SceneFrame frame)
        {
            int[] counts = _counts;
            counts[0] = counts[1] = 0;
            int discs = 0;
            Vector3 lens = frame.Camera.Position;
            Matrix view = frame.Camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);

            for (int i = 0; i <= COUNT; i++)
            {
                //The last is the featured scorpion, on its own timeline
                Pose pose;
                if (i == COUNT ? !TryFeaturePose(_featureAt, _featureYaw, _featureOnset, frame.Time, _ground, out pose) : !TryPose(i, frame.Time, out pose))
                    continue;
                bool scorpion = i == COUNT || IsScorpion(i);
                float size = scorpion ? SCORPION_SIZE : BEETLE_SIZE;
                int kind = scorpion ? 1 : 0;
                float height = size * 0.5f;

                Matrix world = Matrix.CreateScale(size) * Matrix.CreateRotationX(-pose.Pitch) * Matrix.CreateRotationY(pose.Yaw)
                    * Matrix.CreateTranslation(pose.Position - new Vector3(0f, pose.Sunk * height, 0f));
                _instanceData[kind][counts[kind]++] = new ModelInstance(world, new Vector4(pose.Gait, i * 0.137f, kind, 0f));

                //A beetle's trail of prints behind it, along the way it walked, fading with age
                if (!scorpion && pose.Walked > 0f)
                    for (int k = 1; k <= TRAIL_PRINTS && discs < MAX_DISCS; k++)
                    {
                        float back = pose.Walked - k * PRINT_SPACING * size;
                        if (back <= 0f) break;
                        Vector2 at = Arc(pose.Start, pose.Heading, pose.Turn, back, out float yawAt);
                        float side = (k & 1) == 0 ? 1f : -1f;
                        var print = new Vector3(at.X + MathF.Cos(yawAt) * side * PRINT_SIDE * size, 0f, at.Y - MathF.Sin(yawAt) * side * PRINT_SIDE * size);
                        print.Y = _ground(print.X, print.Z);
                        FlatDisc(ref discs, print, size * 0.06f, new Color(0.18f, 0.12f, 0.06f, 0.5f) * (1f - k / (float)(TRAIL_PRINTS + 1)));
                    }

                //Its shadow while it stands on the sand, the hole while it comes up or goes down, the sand thrown from it
                float above = 1f - Math.Clamp(pose.Sunk, 0f, 1f);
                if (above > 0.05f && discs < MAX_DISCS)
                    FlatDisc(ref discs, pose.Position, size * 0.55f, new Color(0f, 0f, 0f, 0.42f * above));
                float digging = MathF.Max(pose.Emerging > 0f ? 1f - pose.Emerging * 0.6f : 0f, pose.Burrowing);
                if (digging > 0f && discs < MAX_DISCS)
                {
                    Vector3 hole = new(pose.Burrow.X, _ground(pose.Burrow.X, pose.Burrow.Z), pose.Burrow.Z);
                    FlatDisc(ref discs, hole, size * 0.42f, new Color(0.05f, 0.03f, 0.015f, 0.8f) * digging);
                    //Four puffs of sand, rising and spreading as they thin
                    float age = pose.Emerging > 0f ? pose.Emerging : pose.Burrowing;
                    for (int k = 0; k < 4 && discs < MAX_DISCS; k++)
                    {
                        float a = MathHelper.TwoPi * (k / 4f + Unit(i, 500 + k));
                        Vector3 at = hole + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (size * (0.2f + 0.5f * age))
                            + Vector3.Up * (size * (0.15f + 0.45f * age));
                        //⚠ Larger and denser, the four ran into one soft blob that hid the creature coming out of it
                        float radius = size * (0.14f + 0.28f * age);
                        float alpha = 0.32f * MathF.Sin(MathF.PI * MathF.Min(age * 1.2f, 1f));
                        FacingDisc(ref discs, at, right, radius, new Color(0.62f, 0.47f, 0.3f) * alpha);
                    }
                }
            }

            _view.SetValue(view);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(lens);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(frame.SunColor);
            _zenith.SetValue(frame.ZenithLinear);
            _horizon.SetValue(frame.HorizonLinear);
            _time.SetValue(frame.Time);

            //The bodies, opaque, the part under the sand hidden by the sand's own depth
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            _effect.CurrentTechnique = _critterTechnique;
            for (int kind = 0; kind < 2; kind++)
            {
                if (counts[kind] == 0) continue;
                _instances[kind].SetData(_instanceData[kind], 0, counts[kind], SetDataOptions.Discard);
                InstanceBinding.Set(_device, _meshes[kind], 0, _instances[kind], 0);
                _device.Indices = _meshIndices[kind];
                _effect.CurrentTechnique.Passes[0].Apply();
                _device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, _meshTriangles[kind], counts[kind]);
            }

            //The discs over the sand and in front of the bodies, premultiplied, reading the depth and writing none
            if (discs > 0)
            {
                _discBuffer.SetData(_discs, 0, discs * 4, SetDataOptions.Discard);
                _device.BlendState = BlendState.AlphaBlend;
                _device.DepthStencilState = DepthStencilState.DepthRead;
                _device.RasterizerState = RasterizerState.CullNone;
                _device.SetVertexBuffer(_discBuffer);
                _device.Indices = _discIndices;
                _effect.CurrentTechnique = _discTechnique;
                _effect.CurrentTechnique.Passes[0].Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, discs * 2);
            }

            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        //A disc lying on the sand a hair over it
        private void FlatDisc(ref int discs, Vector3 centre, float radius, Color color)
        {
            centre.Y += 0.03f;
            Quad(ref discs, centre, Vector3.UnitX * radius, Vector3.UnitZ * radius, color);
        }

        //A disc turned to the lens about the vertical
        private void FacingDisc(ref int discs, Vector3 centre, Vector3 right, float radius, Color color) =>
            Quad(ref discs, centre, right * radius, Vector3.Up * radius, color);

        private void Quad(ref int discs, Vector3 centre, Vector3 u, Vector3 v, Color color)
        {
            int at = discs * 4;
            _discs[at] = new VertexPositionColorTexture(centre - u - v, color, new Vector2(-1f, -1f));
            _discs[at + 1] = new VertexPositionColorTexture(centre + u - v, color, new Vector2(1f, -1f));
            _discs[at + 2] = new VertexPositionColorTexture(centre + u + v, color, new Vector2(1f, 1f));
            _discs[at + 3] = new VertexPositionColorTexture(centre - u + v, color, new Vector2(-1f, 1f));
            discs++;
        }

        public void Dispose()
        {
            for (int k = 0; k < 2; k++)
            {
                _meshes[k]?.Dispose();
                _meshIndices[k]?.Dispose();
                _instances[k]?.Dispose();
            }
            _discBuffer?.Dispose();
            _discIndices?.Dispose();
        }
    }
}
