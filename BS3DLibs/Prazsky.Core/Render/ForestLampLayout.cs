using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>Where the forest's lamps stand and how they burn</b> (#1166). The owner: "for this scene we will also need some form
    /// of lighting for the play island; the moonlight is too little. It should make sense in the environment, e.g. old lamps on
    /// wooden poles, and somewhere farther off a wind turbine, so it can be explained where the electricity comes from." So
    /// <see cref="COUNT"/> old lamps on weathered wooden poles stand round the island at the clearing's edge, each an enamel
    /// shade on a gooseneck arm reaching towards the island with one warm bulb under it, and each bulb is a real point light
    /// (<see cref="SceneLights"/>' forest branch, the floor's and the plant material's own loops), so the stone, the cluster,
    /// the gun, the trees nearest the clearing and the ground under every pole take its light. Device-free: the backdrop, the
    /// hosts' <see cref="ForestLamps"/>, <see cref="SceneLights"/> and the tests all read this one copy.
    /// <para>
    /// <b>Outside every lens that plays</b>: the ring stands at <see cref="RING_RADIUS"/>, past the farthest the play lens
    /// stands from the axis (28 to 45 units, <c>IntroLanding</c>'s figures, held by <c>ForestLampsTests</c> for every shipped
    /// level at every window shape) and the front end's orbit, which keeps inside <see cref="OpenGroundRadius"/>, so no pole
    /// ever stands between a lens and the cluster or the gun: from the play view the poles on the far side stand behind the
    /// field. The ring is turned so the opening play view (the lens on +Z, looking towards -Z) looks into the gap between two
    /// lamps, which flank the cluster, and never at a lamp behind its middle.
    /// </para>
    /// <para>
    /// <b>Each pole stands where the planting leaves it room</b> (<see cref="Plan"/>): from its nominal place on the ring it
    /// walks round and in or out to the first spot where no trunk, crown, boulder, stump or dead wood comes within
    /// <see cref="CLEARANCE"/> of the pole, its arm or its shade, so a pole never stands in a spruce's skirt whatever
    /// <c>sceneseed=</c> rolled the wood. Because of that the layout belongs to a planting: the backdrop plans it once from its
    /// own (<c>SceneRenderer.ForestLamps</c>).
    /// </para>
    /// <para>
    /// <b>Imperfections, on purpose</b> (the owner's #640): the poles are not evenly spaced, each leans a little its own way,
    /// stands a little higher or lower, turns its arm a few degrees off the island, and the bulbs are not one colour; and one
    /// lamp is old (<see cref="OLD_LAMP"/>): every <see cref="STUTTER_PERIOD"/> seconds its filament stutters for a moment,
    /// glass and light together.
    /// </para>
    /// </summary>
    public sealed class ForestLampLayout
    {
        /// <summary>How many lamps stand round the island: under <see cref="SceneLights.MaxLights"/>, so a blast's flash always
        /// finds a slot.</summary>
        public const int COUNT = 5;

        /// <summary>The nominal ring the poles' feet stand on, from the axis: past the play lens's farthest (45) and inside the
        /// wood's first trunks (<see cref="ForestTreeConfig.MinRadius"/>, 52).</summary>
        public const float RING_RADIUS = 49f;

        /// <summary>How far in or out of <see cref="RING_RADIUS"/> a pole may stand to find room.</summary>
        public const float RING_GIVE = 2f;

        /// <summary>How far round its nominal bearing, radians either way, a pole may walk to find room.</summary>
        public const float BEARING_GIVE = 0.3f;

        /// <summary>
        /// The front end's lens keeps <c>BackdropScreen.OPEN_GROUND_MARGIN</c> inside this (<c>BS3DGame.OpenGroundRadius</c>,
        /// the city's clearing rule, #1039): the poles' nearest, less the shade's reach towards the island.
        /// </summary>
        public const float OpenGroundRadius = RING_RADIUS - RING_GIVE - ARM_REACH - SHADE_RADIUS;

        /// <summary>The pole's length from its foot (sunk <see cref="SINK"/> into the ground) to its top: about 9 m, so the bulbs
        /// hang over the cluster's lower half and light it from under their shades.</summary>
        public const float POLE_LENGTH = 19f;

        /// <summary>How deep a pole's foot is sunk, before its own <see cref="Lamp.Sink"/>: the floor is lumpy and the ground
        /// sampled at one point, so a foot planted on it would float off the downhill side.</summary>
        public const float SINK = 1f;

        /// <summary>The pole's radius at its foot and at its top (a pole tapers).</summary>
        public const float POLE_RADIUS = 0.30f, POLE_TOP_RADIUS = 0.21f;

        /// <summary>How far the arm reaches out from the pole's axis towards the island to the shade's middle.</summary>
        public const float ARM_REACH = 2.3f;

        /// <summary>How far below the pole's top the shade's rim hangs.</summary>
        public const float SHADE_DROP = 1.75f;

        /// <summary>How much bigger the shade is built than its profile's own figures (<see cref="ForestLampMeshes"/>): a rim
        /// <see cref="SHADE_RADIUS"/> across, so the lamp reads as a lamp from across the island.</summary>
        public const float SHADE_SCALE = 1.2f;

        /// <summary>The shade's rim radius.</summary>
        public const float SHADE_RADIUS = 0.64f * SHADE_SCALE;

        /// <summary>Where the bulb's middle stands, under the shade's crown (the light's position), in the pole's frame.</summary>
        public static readonly Vector3 BULB = new(0f, POLE_LENGTH - SHADE_DROP + 0.02f, -ARM_REACH);

        /// <summary>The bulb's radius.</summary>
        public const float BULB_RADIUS = 0.13f;

        /// <summary>
        /// Where a lamp's light has fallen to nothing (<c>saturate(1 - d / range)^2</c>): far enough to reach the island's far
        /// rim from the far side of the ring, so every lamp lights the whole stone and the cluster over it, the nearest rim most.
        /// </summary>
        public const float RANGE = 64f;

        /// <summary>
        /// A lamp's light at full (linear radiance): an old incandescent bulb, about 2700 K, warm against the overcast lid's
        /// blue-grey. Strong enough that the island under five of them reads at night; the pools under the poles and the
        /// stone carry it, the wood round the clearing takes the falloff.
        /// </summary>
        public static readonly Vector3 COLOUR = new(1.9f, 1.2f, 0.62f);

        /// <summary>The bulb's glow (linear) at full, times the lamp's own colour over <see cref="COLOUR"/>: far over the glare
        /// threshold, so the bloom makes the halo round a few pixels of filament.</summary>
        public const float GLOW = 7f;

        /// <summary>Each lamp's tint on <see cref="COLOUR"/>: no two bulbs alike, one a little paler and one a little redder.</summary>
        private static readonly Vector3[] TINTS =
        {
            new(1f, 1f, 1f), new(1.02f, 0.97f, 0.9f), new(0.96f, 0.97f, 1.05f), new(1.03f, 0.95f, 0.86f), new(0.98f, 1.0f, 1.02f),
        };

        /// <summary>The old lamp, whose filament stutters (<see cref="Brightness"/>).</summary>
        public const int OLD_LAMP = 3;

        /// <summary>How often the old lamp stutters, seconds, and for how long.</summary>
        public const float STUTTER_PERIOD = 19f, STUTTER_SECONDS = 0.7f;

        /// <summary>The nominal bearings, radians from +X towards +Z: the opening play view's lens stands on +Z, so the ring is
        /// turned to put the bearing straight behind the cluster from it (-pi/2) between two lamps, each nudged off the even
        /// spacing.</summary>
        private static readonly float[] BEARINGS = BuildBearings();

        private static float[] BuildBearings()
        {
            float[] nudge = { 0.05f, -0.07f, 0.09f, -0.04f, 0.02f };
            var bearings = new float[COUNT];
            for (int i = 0; i < COUNT; i++)
                bearings[i] = -MathHelper.PiOver2 + MathHelper.Pi / COUNT + i * MathHelper.TwoPi / COUNT + nudge[i];
            return bearings;
        }

        //Each lamp's own lean (radians), the bearing of the lean, the arm's turn off the island, and how much deeper or
        //shallower its foot is sunk (so the poles stand at different heights)
        private static readonly (float Lean, float LeanBearing, float Skew, float Sink)[] CHARACTER =
        {
            (0.025f, 1.1f, 0.10f, 0.2f), (0.040f, 4.0f, -0.14f, -0.3f), (0.018f, 2.6f, 0.06f, 0.5f),
            (0.050f, 5.3f, -0.08f, 0f), (0.030f, 0.4f, 0.16f, -0.5f),
        };

        /// <summary>How far, at least, every tree, boulder, stump and piece of dead wood keeps from the pole, its arm and its
        /// shade.</summary>
        public const float CLEARANCE = 0.8f;

        /// <summary>One lamp: where its pole's foot stands, its world matrix (the pole's frame: the foot at the origin, up +Y,
        /// the arm towards -Z), where its bulb is in the world, and how much room the planting left it.</summary>
        public readonly record struct Lamp(Vector3 Foot, float Yaw, Matrix World, Vector3 Bulb, float Room, float Sink);

        /// <summary>The lamps, in the order of their nominal bearings.</summary>
        public IReadOnlyList<Lamp> Lamps => _lamps;

        private readonly Lamp[] _lamps;

        private ForestLampLayout(Lamp[] lamps) => _lamps = lamps;

        /// <summary>
        /// The lamps for a wood: each pole at the first spot round and along its nominal bearing (<see cref="BEARING_GIVE"/>,
        /// <see cref="RING_GIVE"/>) that the planting leaves <see cref="CLEARANCE"/> of room, or where it leaves the most when no
        /// spot is clear; stood on <see cref="TerrainMirror.Forest"/>.
        /// </summary>
        public static ForestLampLayout Plan(ForestSceneConfig config, ForestScatter planting)
        {
            var obstacles = Obstacles(config, planting);
            var lamps = new Lamp[COUNT];

            for (int i = 0; i < COUNT; i++)
            {
                (float lean, float leanBearing, float skew, float sink) = CHARACTER[i];
                Lamp best = default;
                float bestRoom = float.NegativeInfinity;

                //Round first, then in and out: the nearest spot to the nominal one that is clear
                for (int step = 0; step <= 30 && bestRoom < CLEARANCE; step++)
                {
                    float turn = (step + 1) / 2 * (BEARING_GIVE / 15f) * (step % 2 == 0 ? 1f : -1f);
                    foreach (float give in new[] { 0f, -RING_GIVE * 0.5f, RING_GIVE * 0.5f, -RING_GIVE, RING_GIVE })
                    {
                        Lamp lamp = At(config, BEARINGS[i] + turn, RING_RADIUS + give, lean, leanBearing, skew, sink);
                        float room = Room(lamp, obstacles);
                        if (room > bestRoom)
                        {
                            bestRoom = room;
                            best = lamp with { Room = room };
                        }
                        if (room >= CLEARANCE) break;
                    }
                }

                lamps[i] = best;
            }

            return new ForestLampLayout(lamps);
        }

        //A lamp at a bearing and a radius: its foot on the ground, its arm towards the axis turned by its skew, leaned
        private static Lamp At(ForestSceneConfig config, float bearing, float radius, float lean, float leanBearing, float skew, float sink)
        {
            float x = MathF.Cos(bearing) * radius, z = MathF.Sin(bearing) * radius;
            float ground = config != null ? TerrainMirror.Forest(x, z, config) : 0f;
            Vector3 foot = new(x, ground - SINK - sink, z);

            //The arm (local -Z) towards the axis: CreateRotationY(yaw) takes -Z to (-sin, 0, -cos)
            float yaw = MathF.Atan2(x, z) + skew;
            Vector3 leanAxis = new(MathF.Cos(leanBearing), 0f, MathF.Sin(leanBearing));
            Matrix world = Matrix.CreateRotationY(yaw) * Matrix.CreateFromAxisAngle(leanAxis, lean) * Matrix.CreateTranslation(foot);

            return new Lamp(foot, yaw, world, Vector3.Transform(BULB, world), 0f, sink);
        }

        /// <summary>
        /// The bulb's light at <paramref name="time"/>, as a share of full: a breath so slight it is only life for every lamp,
        /// and for <see cref="OLD_LAMP"/> a stutter of its filament for <see cref="STUTTER_SECONDS"/> every
        /// <see cref="STUTTER_PERIOD"/> seconds. A pure function of the clock: the glass and the light read the same value.
        /// </summary>
        public static float Brightness(int lamp, float time)
        {
            float breath = 0.97f + 0.03f * MathF.Sin(time * 0.53f + lamp * 1.9f);
            if (lamp != OLD_LAMP) return breath;

            float phase = time / STUTTER_PERIOD;
            float into = (phase - MathF.Floor(phase)) * STUTTER_PERIOD;
            if (into >= STUTTER_SECONDS) return breath;

            //Three dips of the filament, deepest in the middle, never quite out (an incandescent filament cools, it does not
            //switch): its own envelope, times a fast irregular flutter
            float envelope = MathF.Sin(into / STUTTER_SECONDS * MathHelper.Pi);
            float flutter = 0.5f + 0.5f * MathF.Sin(into * 47f) * MathF.Sin(into * 23f + 1.3f);
            return breath * (1f - 0.7f * envelope * flutter);
        }

        /// <summary>Lamp <paramref name="lamp"/>'s light at <paramref name="time"/> (linear radiance): <see cref="COLOUR"/> in its
        /// own tint, at its <see cref="Brightness"/>.</summary>
        public static Vector3 LightColour(int lamp, float time) => COLOUR * TINTS[lamp] * Brightness(lamp, time);

        /// <summary>
        /// The lamps' lights at <paramref name="time"/>, written into the arrays from slot 0: at most <paramref name="max"/>,
        /// answered how many. Each range is <see cref="RANGE"/> NEGATED, which every scene-light loop reads as a shaded lamp
        /// (<c>SceneLamp.fxh</c>): its light goes out only below its shade's rim, so the crowns over a lamp stay dark. Allocates
        /// nothing.
        /// </summary>
        public int Lights(float time, Vector3[] position, Vector3[] colour, float[] range, int max)
        {
            int count = Math.Min(_lamps.Length, max);
            for (int i = 0; i < count; i++)
            {
                position[i] = _lamps[i].Bulb;
                colour[i] = LightColour(i, time);
                //Negative: a shaded lamp, whose light goes out only under its shade (SceneLamp.fxh)
                range[i] = -RANGE;
            }

            return count;
        }

        //--- Room -----------------------------------------------------------------------------------------------------

        //What stands near the ring: a column about a vertical axis (leaning by Lean a unit of height), whose radius at a height
        //over its foot is Radius(height)
        private readonly record struct Obstacle(Vector2 Foot, float FootY, float Lean, Func<float, float> Radius);

        private static List<Obstacle> Obstacles(ForestSceneConfig config, ForestScatter planting)
        {
            var list = new List<Obstacle>();
            if (planting == null || config == null) return list;

            ForestTreeConfig trees = config.Trees;
            float reach = RING_RADIUS + RING_GIVE + 20f, inner = RING_RADIUS - RING_GIVE - ARM_REACH - 20f;

            bool Near(Matrix world)
            {
                float r = new Vector2(world.M41, world.M43).Length();
                return r < reach && r > inner;
            }

            float ScaleOf(Matrix world) => new Vector3(world.M11, world.M12, world.M13).Length();

            //The spruces: a trunk to the skirt (drooping to 0.6 of it), then a cone to the tip; the lathe's whorls wander and
            //High and Ultra's boughs reach a little past its outline
            for (int v = 0; v < planting.FineConifers.Length; v++)
            {
                ForestScatterRenderer.SpruceVariant variant = ForestScatterRenderer.CONIFER_VARIANTS[Math.Min(v, ForestScatterRenderer.CONIFER_VARIANTS.Length - 1)];
                foreach (ModelInstance tree in planting.FineConifers[v])
                {
                    if (!Near(tree.World)) continue;
                    float s = ScaleOf(tree.World);
                    float stem = trees.ConiferTrunkHeight * variant.Height * (variant.Stand ? trees.ConiferStandTrunk : 1f) * s;
                    float crown = trees.ConiferCrownHeight * variant.Height * s;
                    float skirt = trees.ConiferCrownRadius * variant.Width * s * 1.15f + 0.4f;
                    float trunk = trees.TrunkBaseRadius * s;
                    list.Add(new Obstacle(new Vector2(tree.World.M41, tree.World.M43), tree.World.M42, ForestScatter.TREE_LEAN,
                        h => h < stem * 0.6f ? trunk : h < stem ? skirt : h < stem + crown ? skirt * (1f - (h - stem) / crown) + 0.2f : 0f));
                }
            }

            //The broadleaves: a trunk, then a crown as an ellipsoid over the trunk's top
            for (int v = 0; v < planting.FineBroadleaves.Length; v++)
            {
                (float trunkRadius, float trunkHeight, float crownRadius, float crownHeight) =
                    ForestScatterRenderer.BroadleafFigures(trees, ForestScatterRenderer.BROADLEAF_SHAPES[Math.Min(v, ForestScatterRenderer.BROADLEAF_SHAPES.Length - 1)]);
                foreach (ModelInstance tree in planting.FineBroadleaves[v])
                {
                    if (!Near(tree.World)) continue;
                    float s = ScaleOf(tree.World);
                    float stem = trunkHeight * s, half = crownHeight * s * 0.6f, middle = stem + crownHeight * s * 0.5f;
                    float r = crownRadius * s * 1.25f + 0.4f, trunk = trunkRadius * s;
                    list.Add(new Obstacle(new Vector2(tree.World.M41, tree.World.M43), tree.World.M42, ForestScatter.TREE_LEAN,
                        h =>
                        {
                            float t = (h - middle) / half;
                            return t * t < 1f ? MathF.Max(trunk, r * MathF.Sqrt(1f - t * t)) : h < middle ? trunk : 0f;
                        }));
                }
            }

            //What lies on the floor: its footprint, to a height a pole's foot could meet it at
            void Lying(ModelInstance[][] buckets, float radius, float height, float lean)
            {
                foreach (ModelInstance[] bucket in buckets)
                    foreach (ModelInstance item in bucket)
                    {
                        if (!Near(item.World)) continue;
                        float s = ScaleOf(item.World), r = radius * s + 0.3f, top = height * s;
                        list.Add(new Obstacle(new Vector2(item.World.M41, item.World.M43), item.World.M42, lean, h => h < top ? r : 0f));
                    }
            }

            Lying(planting.FineRocks, config.Rocks.Radius * 1.4f, config.Rocks.Height * 1.6f, 0f);
            Lying(planting.FineStumps, config.Stumps.Radius * 1.4f, config.Stumps.Height, 0f);
            Lying(planting.FineLogs, config.Logs.Length * 0.6f, config.Logs.Radius * 3f, 0f);
            Lying(planting.FineSnags, config.Snags.Radius * 2f + 0.4f, config.Snags.Length * 1.3f, 0.3f);
            return list;
        }

        //The pole, its arm and its shade as points with a radius each, in the world
        private static IEnumerable<(Vector3 Point, float Radius)> Body(Lamp lamp)
        {
            for (float h = 0f; h <= POLE_LENGTH; h += 1f)
                yield return (Vector3.Transform(new Vector3(0f, h, 0f), lamp.World), POLE_RADIUS);
            for (float a = 0.4f; a <= ARM_REACH; a += 0.4f)
                yield return (Vector3.Transform(new Vector3(0f, POLE_LENGTH - 1.1f, -a), lamp.World), 0.15f);
            yield return (Vector3.Transform(new Vector3(0f, POLE_LENGTH - SHADE_DROP + 0.3f, -ARM_REACH), lamp.World), SHADE_RADIUS + 0.1f);
        }

        //The least room any point of the lamp has from anything near it: the horizontal gap from the obstacle's axis at that
        //height, less its radius there and the point's own
        private static float Room(Lamp lamp, List<Obstacle> obstacles)
        {
            float room = float.PositiveInfinity;
            foreach ((Vector3 point, float radius) in Body(lamp))
                foreach (Obstacle o in obstacles)
                {
                    float h = point.Y - o.FootY;
                    float r = o.Radius(h);
                    if (r <= 0f) continue;
                    float gap = Vector2.Distance(new Vector2(point.X, point.Z), o.Foot) - r - radius - MathF.Max(0f, h) * o.Lean;
                    room = MathF.Min(room, gap);
                }

            return room;
        }
    }
}
