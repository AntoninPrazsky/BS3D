using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// Nothing on the savanna grows through a rock (#1157): every tree, bush, scrub, mound, fallen log, tuft and treeline mass
    /// the game plants stands clear of every rock it plants (a kopje's, a lone boulder, a stone on the bank), each held by the
    /// slabs it is made of (<see cref="SavannaScatter.Bodies"/>) against the rock's own surface (<see cref="RockMesh.Off"/>),
    /// over the plains of <see cref="SCENE_SEEDS"/> scene seeds rolled as the game rolls them, planted with no device.
    /// </summary>
    public class SavannaStoneTests
    {
        private readonly ITestOutputHelper _output;

        public SavannaStoneTests(ITestOutputHelper output) => _output = output;

        //The game rolls a new scene seed every launch (BS3DGame: Random.Shared.Next()); here off a fixed draw, so the test is the
        //same every run
        private const int SCENE_SEEDS = 200, SCENE_SEED_DRAW = 1157;

        [Fact]
        public void Nothing_planted_on_the_savanna_stands_in_a_rock()
        {
            var draw = new Random(SCENE_SEED_DRAW);
            var offenders = new Dictionary<string, List<float>>();
            var planted = new Dictionary<string, int>();
            var points = new List<Vector3>();

            for (int k = 0; k < SCENE_SEEDS; k++)
            {
                int layout = draw.Next();
                SavannaScatter scatter = Plant(layout, out Func<float, float, float> ground);

                //Every rock drawn, as it stands
                var stones = new List<PlantVolumes.Stone>();
                foreach (ScatterBucket bucket in scatter.Buckets)
                    if (bucket.Mesh is RockMesh rock)
                        foreach (ModelInstance instance in bucket.Placed) stones.Add(new PlantVolumes.Stone(rock, instance.World));

                //Every other thing drawn, by what its mesh is made of, at the frame it is drawn in
                foreach (ScatterBucket bucket in scatter.Buckets)
                {
                    if (!scatter.Bodies.TryGetValue(bucket.Mesh, out (string Kind, Slab[] Body) made)) continue;
                    foreach (ModelInstance instance in bucket.Placed)
                    {
                        planted[made.Kind] = planted.GetValueOrDefault(made.Kind) + 1;
                        float clearance = PlantVolumes.StoneClearance(instance.World, made.Body, stones, ground, points);
                        if (clearance >= 0f) continue;
                        if (!offenders.TryGetValue(made.Kind, out List<float> depths)) offenders[made.Kind] = depths = new List<float>();
                        depths.Add(-clearance);
                        if (depths.Count <= 3)
                            _output.WriteLine($"sceneseed={layout}: a {made.Kind} at ({instance.World.M41:0.0}, {instance.World.M43:0.0}) stands {-clearance:0.00} in a rock");
                    }
                }
            }

            foreach ((string kind, int count) in planted.OrderBy(p => p.Key))
                _output.WriteLine($"{kind}: {count} planted, {offenders.GetValueOrDefault(kind)?.Count ?? 0} in a rock"
                    + (offenders.TryGetValue(kind, out List<float> d) ? $", up to {d.Max():0.00} deep" : ""));
            Assert.Empty(offenders);

            //And every kind was asked about: a body left out of Bodies would pass unseen
            foreach (string kind in new[] { "acacia", "baobab", "doum", "bush", "scrub", "mound", "log", "tuft", "treeline" })
                Assert.True(planted.GetValueOrDefault(kind) > 0, kind);
        }

        [Fact]
        public void No_plant_settles_for_a_site_in_a_rock_or_a_neighbours_volume()
        {
            //Every site the plain settled for at negative clearance (SavannaScatter.Crowded, the last sweep's), at sceneseed 0 to
            //CROWDED_SEEDS - 1: none stands in a rock or in a neighbour's volume. The footprints alone may overlap, a spacing
            //radius over another's, and are reported (measured: one, a tree at sceneseed=140, 1.16 into a neighbour's)
            int footprintsOnly = 0;
            for (int layout = 0; layout < CROWDED_SEEDS; layout++)
            {
                SavannaScatter scatter = Plant(layout, out _);
                foreach (SavannaScatter.CrowdedSite site in scatter.Crowded)
                {
                    string where = $"sceneseed={layout}: a site at ({site.X:0.0}, {site.Z:0.0}), a footprint {site.HalfWidth:0.0} round, its answers "
                        + $"{site.Footprint:0.00} (footprints), {site.Volume:0.00} (volumes), {site.Stone:0.00} (rocks)";
                    _output.WriteLine(where);
                    Assert.True(site.Volume >= 0f, where);
                    Assert.True(site.Stone >= 0f, where);
                    footprintsOnly++;
                }
            }

            _output.WriteLine($"{footprintsOnly} sites settled for over the footprints alone, at sceneseed 0 to {CROWDED_SEEDS - 1}");
        }

        //#1157's range: sceneseed 0 to 199
        private const int CROWDED_SEEDS = 200;

        [Fact]
        public void A_rock_reads_its_own_surface()
        {
            //At the scatter's own sizes: a kopje's rocks (the shipped KopjeRockSize 8, 0.75 to 0.99 of it) and their phases
            for (int m = 0; m < 3; m++)
            {
                float radius = 8f * (0.75f + 0.12f * m);
                var rock = new RockMesh(null, radius, radius * 1.05f, 16, irregularityPhase: 1.7f * m + 0.4f);

                //On its own vertices it reads the surface, to within the facets' chord
                foreach (Vector3 p in rock.SurfacePoints)
                    Assert.InRange(rock.Off(p), -0.02f * radius, 0.02f * radius);

                //Inside, a point under the crown reads as deep as its nearer face; outside, as far as it stands off
                Assert.True(rock.Off(new Vector3(0f, rock.Height * 0.5f, 0f)) < -0.3f * rock.Height);
                foreach (Vector3 p in rock.SurfacePoints)
                {
                    if (p.Y <= 0f || p.Y >= rock.Height) continue;
                    Vector3 across = new(p.X, 0f, p.Z);
                    Assert.True(rock.Off(p - across * 0.1f) < 0f);
                    Assert.True(rock.Off(p + across * 0.1f) > 0f);
                }
                Assert.True(rock.Off(new Vector3(0f, rock.Height + 1f, 0f)) > 0.9f);
                Assert.True(rock.Off(new Vector3(0f, -1f, 0f)) > 0.9f);
            }
        }

        [Fact]
        public void A_logs_body_runs_its_whole_length()
        {
            //At the scatter's sizes: the shipped LogLength 9, 0.8 to 1.2 of it, a tenth of that thick
            foreach (float length in new[] { 7.2f, 10.8f })
                for (int seed = 4500; seed < 4502; seed++)
                {
                    Slab[] body = DeadwoodMesh.Body(length, length * 0.1f, seed);
                    Assert.Equal(-length * 0.5f, body[0].OffsetX, 3);
                    Assert.Equal(length * 0.5f, body[^1].OffsetX, 3);
                    for (int i = 1; i < body.Length; i++)
                    {
                        //Each slab's circle overlaps the next one's: no gap along the log a thing could stand in
                        float apart = new Vector2(body[i].OffsetX - body[i - 1].OffsetX, body[i].OffsetZ - body[i - 1].OffsetZ).Length();
                        Assert.True(apart < body[i].Radius + body[i - 1].Radius, $"{apart} apart");
                    }
                }
        }

        /// <summary>The plain the game plants at scene seed <paramref name="layout"/>, as <see cref="SavannaLandingTests"/> plants
        /// it, and the ground the scatter stands things on (the land, and the bank round the island where it rises over it).</summary>
        internal static SavannaScatter Plant(int layout, out Func<float, float, float> ground)
        {
            var config = new SavannaSceneConfig();
            float Height(float x, float z) => TerrainMirror.Savanna(x, z, config);
            CampfireConfig cf = config.Campfire;
            float hearth = cf.FlameSize * (cf.StoneRingScale + cf.StoneSizeScale) + 1f;
            var reserved = new List<ScatterSpacing.Footprint>();
            for (int i = 0; i < SavannaBackdrop.CampfireRingCount(config); i++)
            {
                Vector3 fire = SavannaBackdrop.CampfireRingPoint(config, i, cf.HeightAboveTerrain);
                reserved.Add(new ScatterSpacing.Footprint(fire.X, fire.Z, hearth));
            }

            int seed = SavannaScatter.DEFAULT_SEED + layout;
            ground = (x, z) => MathF.Max(Height(x, z), IslandBerm.SurfaceY(Height, seed, x, z));
            return new SavannaScatter(null, config, Height, reserved, seed);
        }
    }
}
