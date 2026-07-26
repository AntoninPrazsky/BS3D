using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A procedurally generated city, drawn as one instanced box per building. No assets: the layout,
    /// the footprints and the heights all come out of a seeded generator, and the windows are evaluated
    /// in the shader from world position, so a building carries no texture and no UVs.
    /// <para>
    /// The play surface sits low among the towers, which is what the whole arrangement is for: the arena
    /// is the small round stone island in a clearing between skyscrapers, with the city standing over it
    /// and falling away underneath it at the same time.
    /// Buildings are scaled non-uniformly through their instance matrix, which is safe here because the
    /// boxes are axis-aligned in object space — a diagonal scale maps each face normal onto itself, so
    /// normalizing in the pixel shader recovers it without an inverse transpose.
    /// </para>
    /// Drawn by an <see cref="InstancedModelRenderer"/> with <c>CityWindowBrightness &gt; 0</c> (the shared
    /// InstancedModel city technique); the game and the map editor each own one, so the city takes part in
    /// their sky lighting like every other instanced object.
    /// </summary>
    public sealed class City
    {
        /// <summary>One instanced draw call's worth of buildings.</summary>
        public ModelInstance[] Buildings { get; }

        //Layout/generator parameters (block pitch, street width, radius, roofline, taper, base Y, under-arena
        //depth) live in CitySceneConfig; the constructor reads them from the config passed in. The window look
        //and the neon relight are wired too: InstancedModelRenderer.CityConfig pushes them to the shader on
        //every city draw, and the caller reads WindowBrightness/NeonLook for the day/neon switch.

        /// <summary>The city has no neighboring-cell occlusion; the shader still expects the vector.</summary>
        private static readonly Vector4 NO_OCCLUSION = new(0f, 0f, 0f, 1f);

        /// <param name="seed">Layout seed; the same seed always gives the same city.</param>
        /// <param name="arenaHalfExtent">
        /// Half-width of the play surface. Blocks whose footprint would reach into it are left out, so
        /// the arena sits in a clearing rather than inside a building.
        /// </param>
        /// <param name="config">The city's layout configuration (block pitch, radius, roofline, taper, base, under-arena depth).</param>
        public City(int seed, float arenaHalfExtent, CitySceneConfig config)
        {
            Random random = new(seed);
            List<ModelInstance> buildings = new();

            float buildable = config.BlockPitch - config.StreetWidth;

            for (int blockX = -config.RadiusBlocks; blockX <= config.RadiusBlocks; blockX++)
                for (int blockZ = -config.RadiusBlocks; blockZ <= config.RadiusBlocks; blockZ++)
                {
                    Vector2 blockCenter = new(blockX * config.BlockPitch, blockZ * config.BlockPitch);

                    //The city continues underneath the arena rather than stopping at its edge. It has to:
                    //the drop is seen past the island's rim and down through the drain funnel, and a
                    //clearing under the island would show nothing at all. What changes under the arena is
                    //the height — those towers are cut off far below, which is what opens the drop the
                    //island is suspended over.
                    float clearance = arenaHalfExtent + config.StreetWidth;
                    bool underArena = Math.Abs(blockCenter.X) < clearance + buildable * 0.5f &&
                        Math.Abs(blockCenter.Y) < clearance + buildable * 0.5f;

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

                            float top = config.RooflineY
                                - distanceInBlocks * config.TaperPerBlock
                                + (float)(random.NextDouble() * 2 - 1) * config.RooflineSpread;

                            //Under the island the roofs drop away into a shaft. The gap is what turns
                            //looking down from "there is a city there" into vertigo.
                            if (underArena) top = config.UnderArenaTopY - (float)random.NextDouble() * config.UnderArenaSpread;

                            float height = top - config.BaseY;
                            if (height <= 1f) continue;

                            Vector3 center = new(
                                blockCenter.X + offsetX,
                                config.BaseY + height * 0.5f,
                                blockCenter.Y + offsetZ);

                            //Scale then translate: no rotation, so the box stays axis-aligned and its
                            //normals survive the non-uniform scale
                            Matrix world = Matrix.CreateScale(sizeX, height, sizeZ) * Matrix.CreateTranslation(center);

                            buildings.Add(new ModelInstance(world, NO_OCCLUSION));
                        }
                }

            Buildings = buildings.ToArray();
        }
    }
}
