using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Where a worn path has to bend, and by how much (#476): a small CPU-built field of <b>which way to step
    /// aside</b>, handed to a terrain shader so its trails go round the things standing on the ground instead
    /// of through them.
    /// <para>
    /// <b>The fault it exists for.</b> The savanna's trails are painted by <c>Savanna.fx</c> as the contour
    /// lines of one low-frequency noise, and its plants are placed on the CPU by <see cref="SavannaScatter"/>.
    /// Neither knows about the other, so a path ran straight through a tree — which is the owner's report, and
    /// nonsense on its face: nobody wears a track through a trunk.
    /// </para>
    /// <para>
    /// <b>The answer is a domain warp, not a hole.</b> Cutting the trail where a tree stands would leave a
    /// path that stops and starts, which is a different wrong picture. Instead every point near a plant samples
    /// the trail noise as if it stood <i>further out</i>, so the contour that would have crossed the trunk is
    /// pushed aside and arrives as a <b>bend</b> — which is what a path round an obstacle actually is.
    /// </para>
    /// <para>
    /// ⚠ <b>And the bend is wide and early, which is the whole of the owner's second note:</b> "people have
    /// eyes and can see ahead, so they are already going around from a distance." So the falloff radius is
    /// many times a trunk's own width — <see cref="Reach"/> — and the displacement is a large fraction of it.
    /// A repulsion that started at the bark would read as a kink at the last moment, which is how an ant walks
    /// round a tree and not how a person does.
    /// </para>
    /// <para>
    /// One RG texture, built once when the planting is, sampled once per pixel inside the trail's existing
    /// <c>[branch]</c>. It is the same shape as the city's occupancy texture that <see cref="CityStreets"/>
    /// is handed: a small CPU-built field that tells a shader about geometry it cannot see.
    /// </para>
    /// </summary>
    public sealed class TrailWarpField : IDisposable
    {
        /// <summary>
        /// How many texels a side. The field is <b>smooth and low-frequency by construction</b> — it is a sum
        /// of falloffs several dozen units wide — so it is sampled far below the resolution of anything else
        /// here: 256 over the whole plain is about four world units a texel, well under the narrowest feature
        /// the field can hold.
        /// </summary>
        public const int SIZE = 256;

        /// <summary>The texture to hand the shader: RG, the step-aside vector encoded over 0…1.</summary>
        public Texture2D Texture { get; private set; }

        /// <summary>How many world units across the field covers, centred on the origin.</summary>
        public float Extent { get; }

        /// <summary>The largest step aside the field encodes, in world units — what the shader multiplies the
        /// decoded vector by.</summary>
        public float MaxOffset { get; }

        /// <summary>
        /// Builds the field from what is standing on the ground.
        /// </summary>
        /// <param name="device">The device the texture lives on.</param>
        /// <param name="standing">Everything planted, with its own footprint radius.</param>
        /// <param name="minRadius">
        /// How big a thing has to be before a path goes round it. A tuft of grass is walked <i>through</i>, so
        /// the field is built from the trees, the mounds, the kopjes and the fallen trunks and not from the
        /// ground cover — which is also what keeps this cheap, since the ground cover is most of the list.
        /// </param>
        /// <param name="reach">See <see cref="Reach"/>.</param>
        /// <param name="maxOffset">The largest step aside, in world units.</param>
        /// <param name="extent">How much ground the field covers, centred on the origin.</param>
        public TrailWarpField(GraphicsDevice device, IReadOnlyList<ScatterSpacing.Footprint> standing,
            float minRadius, float reach, float maxOffset, float extent)
        {
            Extent = extent;
            MaxOffset = maxOffset;
            Reach = reach;

            Texture = new Texture2D(device, SIZE, SIZE, false, SurfaceFormat.Color);

            //The obstacles worth going round, pulled out once: the loop below visits each of them per texel,
            //and the ground cover would multiply that by ten for plants nobody walks around.
            List<ScatterSpacing.Footprint> obstacles = new();
            if (standing != null)
                for (int i = 0; i < standing.Count; i++)
                    if (standing[i].Radius >= minRadius)
                        obstacles.Add(standing[i]);

            Color[] texels = new Color[SIZE * SIZE];

            float texelSize = extent / SIZE;
            float half = extent * 0.5f;

            for (int z = 0; z < SIZE; z++)
            {
                float worldZ = -half + (z + 0.5f) * texelSize;

                for (int x = 0; x < SIZE; x++)
                {
                    float worldX = -half + (x + 0.5f) * texelSize;

                    //Summed rather than nearest-only: two trees close together should push a path out round
                    //BOTH of them, and a nearest-only field would send it into the gap between their trunks,
                    //which is the one line a person would not take.
                    float offsetX = 0f, offsetZ = 0f;

                    for (int i = 0; i < obstacles.Count; i++)
                    {
                        ScatterSpacing.Footprint obstacle = obstacles[i];

                        float dx = worldX - obstacle.X;
                        float dz = worldZ - obstacle.Z;
                        float distanceSquared = dx * dx + dz * dz;

                        //The reach is measured from the obstacle's EDGE, not its centre, so a baobab is given
                        //the same margin past its own bulk as a sapling is past its stem.
                        float outer = obstacle.Radius + reach;
                        if (distanceSquared >= outer * outer) continue;

                        float distance = MathF.Sqrt(distanceSquared);

                        //Smooth all the way to zero at the rim: a falloff with a corner in it puts a corner in
                        //the path, and the eye finds a kink in a track faster than it finds the track.
                        float t = 1f - distance / outer;
                        float push = t * t * (3f - 2f * t);      //smoothstep

                        //Directly under the trunk there is no direction to step; the sum of its neighbours
                        //decides it, and at the centre of a lone tree the field is zero — which is right, since
                        //the bend is already complete by the time a path would reach it.
                        if (distance > 1e-3f)
                        {
                            offsetX += dx / distance * push;
                            offsetZ += dz / distance * push;
                        }
                    }

                    //Clamped rather than normalised: inside a dense clump the sum can exceed one obstacle's
                    //worth, and that is the field saying "further out still", which is correct until it would
                    //throw the path further than the shader's own offset allows.
                    float length = MathF.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                    if (length > 1f)
                    {
                        offsetX /= length;
                        offsetZ /= length;
                    }

                    texels[z * SIZE + x] = new Color(
                        offsetX * 0.5f + 0.5f,
                        offsetZ * 0.5f + 0.5f,
                        0f, 1f);
                }
            }

            Texture.SetData(texels);
        }

        /// <summary>
        /// How far past an obstacle's own edge a path starts bending, in world units. <b>It is the owner's
        /// "the bend will not be small at all"</b>: a person crossing open ground sees a tree from a long way
        /// off and drifts round it, so this is many times a trunk's width rather than a margin on it.
        /// </summary>
        public float Reach { get; }

        public void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
        }
    }
}
