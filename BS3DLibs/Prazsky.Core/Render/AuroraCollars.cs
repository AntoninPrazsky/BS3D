using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The snow drifted against the foot of everything the aurora's planting stands (#963): a collar of snow round every
    /// trunk, snag, boulder and stump, and along every log, rising steeply against the wood and falling away over a few
    /// units, longer downwind. Drawn by <c>Aurora.fx</c>'s <c>AuroraCollar</c> as one instanced mesh over the snow it
    /// stands on and shaded as that snow is, so its rim melts into the ground and only the rise against the wood shows.
    /// <para>
    /// <b>Why a mesh of its own and not the ground's.</b> A drift against a trunk rises over less than a unit, and the
    /// ground's grid is 0.75 a cell and the mound map a unit a texel: baked there (the first cut of #963) the flare came
    /// out at four fifths of its height smeared over two units, and every foot read as a cylinder stuck into a plane. A
    /// collar's rings are spaced by the drift's own reach.
    /// </para>
    /// <para>
    /// The profile is <see cref="Rise"/>, one copy for the shader's arithmetic and the tests: a collar starts inside the
    /// wood (<see cref="INNER"/> of the core's radius) at its full height and falls to nothing at its reach with a level
    /// tangent, so the ground's normal and the collar's agree where it meets the snow.
    /// </para>
    /// </summary>
    public static class AuroraCollars
    {
        /// <summary>Where a collar's inner ring stands, as a share of the core's radius: inside the bark, so the snow meets the wood below its surface.</summary>
        public const float INNER = 0.7f;

        /// <summary>How sharply the rise falls away from the wood: the profile is (1 - u) to this power.</summary>
        public const float PROFILE_POWER = 2.5f;

        /// <summary>How far under the snow the rim sits, so the two surfaces cross rather than fight where they meet.</summary>
        public const float SINK = 0.02f;

        /// <summary>
        /// One collar: <c>Place</c> is the core's centre (x, z), half the length of a log's core (0 for a round one) and the
        /// core's radius; <c>Shape</c> the rise against the wood, its reach outwards, and the core's axis (x, z).
        /// </summary>
        public readonly record struct Collar(Vector4 Place, Vector4 Shape);

        /// <summary>The rise at <paramref name="u"/>, 0 at the inner ring to 1 at the reach, as a share of the collar's height.</summary>
        public static float Rise(float u) => MathF.Pow(1f - Math.Clamp(u, 0f, 1f), PROFILE_POWER);

        /// <summary>How much further a collar reaches on its side facing <paramref name="downwind"/> (the outward direction's dot with the wind).</summary>
        public static float LeeStretch(float downwind, float lee) => 1f + lee * Math.Clamp(downwind, 0f, 1f);

        /// <summary>
        /// The collar's height over the snow it stands on at a <paramref name="distance"/> from its core, outward in a direction
        /// whose dot with the wind is <paramref name="downwind"/>: what <c>Aurora.fx</c>'s <c>CollarVS</c> adds, its rim's sink
        /// included. Inside the inner ring it is the full rise.
        /// </summary>
        public static float HeightAt(Collar collar, float distance, float downwind, float lee)
        {
            float inner = collar.Place.W * INNER;
            float u = (distance - inner) / (collar.Shape.Y * LeeStretch(downwind, lee));
            return collar.Shape.X * Rise(u) - SINK;
        }

        /// <summary>
        /// Every collar of <paramref name="planting"/>: the spruces, the snags, the boulders, the stumps and the logs, in that
        /// order. <paramref name="ground"/> is the snow the collars stand on (<c>TerrainMirror.Aurora</c>): a log's drift is
        /// measured from it to the log's own axis, so a thin log and a thick one lie equally deep.
        /// </summary>
        public static Collar[] Build(ForestScatter planting, AuroraSceneConfig config, Func<float, float, float> ground)
        {
            AuroraSnowfieldConfig snow = config.Snowfield;
            ForestSceneConfig terrain = config.Terrain;
            var collars = new List<Collar>(1200);

            static float ScaleOf(Matrix world) => new Vector3(world.M11, world.M12, world.M13).Length();
            static Collar Round(Matrix world, float core, float rise, float reach) =>
                new(new Vector4(world.M41, world.M43, 0f, core), new Vector4(rise, reach, 1f, 0f));

            for (int v = 0; v < planting.Conifers.Length; v++)
            {
                float width = ForestScatterRenderer.CONIFER_VARIANTS[v].Width;
                foreach (ModelInstance tree in planting.Conifers[v])
                {
                    float scale = ScaleOf(tree.World);
                    collars.Add(Round(tree.World, terrain.Trees.TrunkBaseRadius * width * scale, snow.TrunkDrift * scale, snow.TrunkDriftReach * scale));
                }
            }

            for (int v = 0; v < planting.Snags.Length; v++)
            {
                float radius = terrain.Snags.Radius * ForestScatterRenderer.SNAG_SHAPES[v].Y;
                foreach (ModelInstance snag in planting.Snags[v])
                {
                    float scale = ScaleOf(snag.World);
                    collars.Add(Round(snag.World, radius * scale, snow.TrunkDrift * 0.9f * scale, snow.TrunkDriftReach * 0.85f * scale));
                }
            }

            for (int v = 0; v < planting.Rocks.Length; v++)
            {
                Vector2 shape = ForestScatterRenderer.ROCK_SHAPES[v];
                foreach (ModelInstance rock in planting.Rocks[v])
                {
                    float scale = ScaleOf(rock.World);
                    float radius = terrain.Rocks.Radius * shape.X * scale, height = terrain.Rocks.Height * shape.Y * scale;
                    collars.Add(Round(rock.World, radius * 0.85f, height * 0.4f, radius * 1.2f));
                }
            }

            for (int v = 0; v < planting.Stumps.Length; v++)
            {
                Vector2 shape = ForestScatterRenderer.STUMP_SHAPES[v];
                foreach (ModelInstance stump in planting.Stumps[v])
                {
                    float scale = ScaleOf(stump.World);
                    float radius = terrain.Stumps.Radius * shape.X * scale, height = terrain.Stumps.Height * shape.Y * scale;
                    collars.Add(Round(stump.World, radius * 0.9f, height * 0.4f, radius * 1.3f));
                }
            }

            //A log's core is its axis on the ground, root to tip, moved halfway to its bend and widened by the other half, so
            //the stadium the collar runs round covers the bowed log the mesh draws (#963: a straight core left a log bowed a
            //metre sideways lying on top of its own drift)
            for (int v = 0; v < planting.Logs.Length; v++)
            {
                Vector2 shape = ForestScatterRenderer.LOG_SHAPES[v];
                float length = terrain.Logs.Length * shape.X, radius = terrain.Logs.Radius * shape.Y;
                float bend = DeadwoodMesh.Bend(length, ForestScatterRenderer.LOG_SEEDS[v]);
                foreach (ModelInstance log in planting.Logs[v])
                {
                    float scale = ScaleOf(log.World);
                    Vector3 root = Vector3.Transform(new Vector3(-length * 0.5f, 0f, 0f), log.World);
                    Vector3 tip = Vector3.Transform(new Vector3(length * 0.5f, 0f, 0f), log.World);
                    Vector3 bow = Vector3.Transform(new Vector3(0f, 0f, bend * 0.5f), log.World) - Vector3.Transform(Vector3.Zero, log.World);
                    Vector2 axis = new(tip.X - root.X, tip.Z - root.Z);
                    float half = axis.Length() * 0.5f;
                    axis = half > 1e-4f ? axis / (half * 2f) : Vector2.UnitX;
                    Vector2 centre = new((root.X + tip.X) * 0.5f + bow.X, (root.Z + tip.Z) * 0.5f + bow.Z);

                    //The snow against it stands LogDrift of its radius over its axis at the middle, where DeadwoodMesh lays
                    //the axis 0.7 of the root's radius over the planted foot
                    Vector3 middle = Vector3.Transform(new Vector3(0f, radius * 0.7f, bend), log.World);
                    float rise = MathF.Max(middle.Y + snow.LogDrift * radius * scale - ground(middle.X, middle.Z), 0.1f * radius * scale);
                    collars.Add(new Collar(new Vector4(centre.X, centre.Y, half, radius * scale + MathF.Abs(bend) * 0.5f * scale),
                        new Vector4(rise + SINK, snow.TrunkDriftReach * 0.7f * scale, axis.X, axis.Y)));
                }
            }

            return collars.ToArray();
        }
    }
}
