using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Where the Grid's solids and its landmark ring stand (#393, #512, #559), off the config and the launch's roll alone, with
    /// no graphics device: the one placement every host draws. <c>GridBackdrop</c> builds its solids and their Life boards on it,
    /// the Potato path its boxes (#1044), and the light cycles ride clear of what it places (#1136), so where a cycle turns
    /// away from a cube is where that cube stands in the frame, on either path.
    /// </summary>
    internal static class GridLayout
    {
        //The floor left between two solids' footprint circles, and between a solid and the landmark's footprint
        private const float SOLID_GAP = 15f;

        //Twenty attempts to find a draw clear of everything; past them, any draw clear of the ring (see PlaceSolids). The
        //ceiling on those is a guard, not a budget: the ring's footprint takes a few percent of the band's area, so a draw that
        //misses it is the ordinary case.
        private const int MAX_PLACEMENT_ATTEMPTS = 20, MAX_RING_ATTEMPTS = 200;

        /// <summary>
        /// Where the landmark stands and how big it is, from the config alone; null when it is not built. What the solids keep
        /// off (#559) and what <c>GridBackdrop.BuildGridLandmark</c> builds there.
        /// </summary>
        public static GridRing? Landmark(GridSceneConfig config)
        {
            GridLandmarkConfig landmark = config.Landmark;
            if (!landmark.Enabled || landmark.Segments < 3) return null;

            float ringRadius = MathF.Max(landmark.Radius, 1f);
            float tube = MathF.Max(landmark.TubeRadius, 0.1f);
            float halfWidth = MathF.Max(landmark.Width, 0.1f) * 0.5f;

            float bearing = MathHelper.ToRadians(landmark.Bearing);
            Vector3 stand = new(MathF.Cos(bearing) * landmark.Distance, config.Terrain.LevelY, MathF.Sin(bearing) * landmark.Distance);

            //The ring's plane faces the arena, so the play camera sees it as a ring and not as an edge-on bar.
            Vector3 planeNormal = new(-stand.X, 0f, -stand.Z);
            planeNormal = planeNormal.LengthSquared() < 1e-6f ? Vector3.Forward : Vector3.Normalize(planeNormal);

            //Resting on the floor: the lowest point of the tube is the ground.
            Vector3 centre = stand + Vector3.Up * (ringRadius + tube);

            return new GridRing(centre, planeNormal, MathF.Max(ringRadius - tube, 0.2f), ringRadius + tube, halfWidth);
        }

        /// <summary>
        /// The solids on <paramref name="config"/> under the launch's roll <paramref name="seedOffset"/> (<c>sceneseed=</c>):
        /// <see cref="GridTowerConfig.Count"/> boxes on the ring from <see cref="GridTowerConfig.RadiusMin"/> to
        /// <see cref="GridTowerConfig.RadiusMax"/>, the first <see cref="GridTowerConfig.CubeFraction"/> of them near-cubes, the
        /// rest towers, each kept a gap off the ones before it (twenty draws, then any that clears the ring) and always off the
        /// landmark ring's footprint (#559). <paramref name="lifeSeeds"/>, when given, takes each solid's Life seed, drawn from
        /// the same stream after it, and <paramref name="landmarkLifeSeed"/> is the ring's, drawn after every solid: the boards a
        /// host that runs Life stamps (<c>GridBackdrop</c>), so every solid shows the same patterns wherever it is drawn.
        /// </summary>
        public static List<GridSolid> PlaceSolids(GridSceneConfig config, int seedOffset, List<int> lifeSeeds, out int landmarkLifeSeed)
        {
            GridTowerConfig towers = config.Towers;
            var solids = new List<GridSolid>(Math.Max(towers.Count, 0));

            //Fixed seed: placement is data every host must agree on, and so, since the boards are seeded from the same stream,
            //is what each solid shows.
            Random placement = new(towers.Seed + seedOffset);

            //The landmark's footprint, which every solid keeps off (#559): until the solids were told where it will stand,
            //sceneseed=3 stood a cube straight through the ring.
            GridRing? landmark = Landmark(config);

            //Footprint circles of everything placed so far, for the overlap retry: a plain list, because this runs once at load
            //on at most a few dozen solids.
            var placed = new List<(Vector2 Center, float Radius)>();

            //The first CubeFraction of Count are cubes, the rest towers: which member of the count gets which shape carries no
            //meaning (angle and radius are drawn independently either way), so there is nothing to gain from shuffling it.
            int cubeCount = (int)MathF.Round(towers.Count * MathHelper.Clamp(towers.CubeFraction, 0f, 1f));

            for (int i = 0; i < towers.Count; i++)
            {
                bool isCube = i < cubeCount;

                //Redrawn every attempt, so the accepted draw is whichever attempt broke out of (or exhausted) the loop
                Vector3 baseCenter = default;
                float sizeX = 0f, sizeY = 0f, sizeZ = 0f;
                Vector2 xz = default;
                float footprintRadius = 0f;

                for (int attempt = 0; attempt < MAX_RING_ATTEMPTS; attempt++)
                {
                    float angle = (float)(placement.NextDouble() * MathHelper.TwoPi);
                    float radius = MathHelper.Lerp(towers.RadiusMin, towers.RadiusMax, (float)placement.NextDouble());

                    if (isCube)
                    {
                        //A big, close-to-equilateral block: a face large enough to show the Life board whole, which reads as
                        //an abstract digital object rather than a building with windows. A little per-axis jitter keeps every
                        //cube from being a literally identical solid without approaching a tower's proportions.
                        float side = MathHelper.Lerp(towers.CubeSizeMin, towers.CubeSizeMax, (float)placement.NextDouble());
                        sizeX = side * (0.94f + 0.12f * (float)placement.NextDouble());
                        sizeY = side * (0.94f + 0.12f * (float)placement.NextDouble());
                        sizeZ = side * (0.94f + 0.12f * (float)placement.NextDouble());
                    }
                    else
                    {
                        sizeY = MathHelper.Lerp(towers.TowerHeightMin, towers.TowerHeightMax, (float)placement.NextDouble());
                        sizeX = MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)placement.NextDouble());
                        sizeZ = MathHelper.Lerp(towers.TowerFootprintMin, towers.TowerFootprintMax, (float)placement.NextDouble());
                    }

                    baseCenter = new Vector3(MathF.Cos(angle) * radius, config.Terrain.LevelY, MathF.Sin(angle) * radius);
                    xz = new Vector2(baseCenter.X, baseCenter.Z);
                    footprintRadius = 0.5f * MathF.Sqrt(sizeX * sizeX + sizeZ * sizeZ);

                    //The ring is a hard rule and the other solids a soft one: a solid standing through the ring is a solid
                    //visibly inside another, where two blocks barely touching is merely close.
                    if (landmark is GridRing ring && ring.FootprintDistance(xz) < footprintRadius + SOLID_GAP) continue;

                    bool overlaps = false;
                    foreach ((Vector2 otherXz, float otherRadius) in placed)
                    {
                        if (Vector2.Distance(xz, otherXz) < footprintRadius + otherRadius + SOLID_GAP) { overlaps = true; break; }
                    }

                    //Exhausting the twenty attempts falls through with the first later draw that clears the ring rather than
                    //dropping the solid: a rare, barely-touching pair reads better than a scene that asked for eighteen and
                    //quietly drew fewer.
                    if (!overlaps || attempt >= MAX_PLACEMENT_ATTEMPTS - 1) break;
                }

                placed.Add((xz, footprintRadius));
                solids.Add(new GridSolid(baseCenter, new Vector3(sizeX, sizeY, sizeZ), isCube));

                //Eight draws per tower and ten per cube after each solid, exactly as many as the reviewed layout spent, so
                //every later solid still stands where the owner saw it; the first seeds this solid's board.
                int lifeSeed = (int)(placement.NextDouble() * int.MaxValue);
                for (int draw = 1; draw < (isCube ? 10 : 8); draw++) placement.NextDouble();
                lifeSeeds?.Add(lifeSeed);
            }

            //The ring's board, seeded after every solid so no solid moved
            landmarkLifeSeed = (int)(placement.NextDouble() * int.MaxValue);
            return solids;
        }
    }
}
