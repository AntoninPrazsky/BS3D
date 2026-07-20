using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace Testbed.Backdrops
{
    /// <summary>
    /// A procedurally generated city, drawn as one instanced box per building. No assets: the layout,
    /// the footprints and the heights all come out of a seeded generator, and the windows are evaluated
    /// in the shader from world position, so a building carries no texture and no UVs.
    /// <para>
    /// The play surface sits at the height of the towers' tops, which is what the whole arrangement is
    /// for: the arena is a glass platform among the roofs, with the city falling away underneath it.
    /// Buildings are scaled non-uniformly through their instance matrix, which is safe here because the
    /// boxes are axis-aligned in object space — a diagonal scale maps each face normal onto itself, so
    /// normalizing in the pixel shader recovers it without an inverse transpose.
    /// </para>
    /// </summary>
    public sealed class City
    {
        /// <summary>One instanced draw call's worth of buildings.</summary>
        public ModelInstance[] Buildings { get; }

        /// <summary>Center-to-center spacing of the street grid, in world units.</summary>
        private const float BLOCK_PITCH = 30f;

        /// <summary>Width of the streets between blocks; the rest of the pitch is buildable.</summary>
        private const float STREET_WIDTH = 9f;

        /// <summary>How far the city reaches from the arena, in blocks.</summary>
        private const int CITY_RADIUS_BLOCKS = 14;

        /// <summary>
        /// Everything hangs off this: the tops of the towers immediately around the arena land near the
        /// play surface, so the platform reads as being level with the roofs rather than floating above
        /// a distant city.
        /// </summary>
        private const float ROOFLINE_Y = -26f;

        /// <summary>How far above and below the roofline the tops are allowed to wander.</summary>
        private const float ROOFLINE_SPREAD = 20f;

        /// <summary>
        /// Where every building starts. Far enough down that the streets between the towers read as
        /// canyons falling away into darkness rather than as a floor with blocks standing on it.
        /// </summary>
        private const float CITY_BASE_Y = -420f;

        /// <summary>
        /// The towers closest to the arena are the tallest, tapering outwards. Real skylines do this
        /// around a center, and it keeps the horizon from being a flat wall of equal boxes.
        /// </summary>
        private const float TAPER_PER_BLOCK = 2.6f;

        /// <param name="seed">Layout seed; the same seed always gives the same city.</param>
        /// <param name="arenaHalfExtent">
        /// Half-width of the play surface. Blocks whose footprint would reach into it are left out, so
        /// the arena sits in a clearing rather than inside a building.
        /// </param>
        public City(int seed, float arenaHalfExtent)
        {
            Random random = new(seed);
            List<ModelInstance> buildings = new();

            float buildable = BLOCK_PITCH - STREET_WIDTH;

            for (int blockX = -CITY_RADIUS_BLOCKS; blockX <= CITY_RADIUS_BLOCKS; blockX++)
                for (int blockZ = -CITY_RADIUS_BLOCKS; blockZ <= CITY_RADIUS_BLOCKS; blockZ++)
                {
                    Vector2 blockCenter = new(blockX * BLOCK_PITCH, blockZ * BLOCK_PITCH);

                    //Keep the arena clear, with a street's worth of margin so no facade grazes its edge
                    float clearance = arenaHalfExtent + STREET_WIDTH;
                    if (Math.Abs(blockCenter.X) < clearance + buildable * 0.5f &&
                        Math.Abs(blockCenter.Y) < clearance + buildable * 0.5f) continue;

                    //A gap in the grid every so often: a plaza, or something demolished. Without them the
                    //regularity of the streets reads as a spreadsheet rather than as a city.
                    if (random.NextDouble() < 0.07) continue;

                    int subdivisions = random.NextDouble() < 0.45 ? 2 : 1;

                    for (int sx = 0; sx < subdivisions; sx++)
                        for (int sz = 0; sz < subdivisions; sz++)
                        {
                            float plot = buildable / subdivisions;

                            //Inset each building inside its plot by a random margin, so neighbouring
                            //facades do not all line up flush along the street
                            float sizeX = plot * (float)(0.72 + random.NextDouble() * 0.24);
                            float sizeZ = plot * (float)(0.72 + random.NextDouble() * 0.24);

                            float offsetX = (sx - (subdivisions - 1) * 0.5f) * plot;
                            float offsetZ = (sz - (subdivisions - 1) * 0.5f) * plot;

                            float distanceInBlocks = MathF.Max(Math.Abs(blockX), Math.Abs(blockZ));

                            float top = ROOFLINE_Y
                                - distanceInBlocks * TAPER_PER_BLOCK
                                + (float)(random.NextDouble() * 2 - 1) * ROOFLINE_SPREAD;

                            float height = top - CITY_BASE_Y;
                            if (height <= 1f) continue;

                            Vector3 center = new(
                                blockCenter.X + offsetX,
                                CITY_BASE_Y + height * 0.5f,
                                blockCenter.Y + offsetZ);

                            //Scale then translate: no rotation, so the box stays axis-aligned and its
                            //normals survive the non-uniform scale
                            Matrix world = Matrix.CreateScale(sizeX, height, sizeZ) * Matrix.CreateTranslation(center);

                            buildings.Add(new ModelInstance(world, NO_OCCLUSION));
                        }
                }

            Buildings = buildings.ToArray();
        }

        /// <summary>The city has no neighboring-cell occlusion; the shader still expects the vector.</summary>
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);
    }
}
