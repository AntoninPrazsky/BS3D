using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The city's tower plans (#1066): slabs, Ls, towers on podiums, twins and leaning setbacks, where every tower had been a
    /// box with its setbacks round one centre. What the plans promise, held over both cities and a dozen rolls of their seeds:
    /// <list type="bullet">
    /// <item>the same city as before: the plots, their sizes and their tops are exactly those of the city built without plans;</item>
    /// <item>every box of a plan stands inside the plot's old footprint and no higher than its top, and the tallest reaches
    /// it, so the chapter intros' flights, the menu orbit and the arena's clearing stay clear by construction;</item>
    /// <item>no two boxes of one tower lay their window grids side by side in one wall: a wall shared by two boxes would meet
    /// in a doubled blank margin (each box lays out its own windows, <c>City.fxh</c>);</item>
    /// <item>every box of a tower carries its kind and its anchor, so the shader sees one building;</item>
    /// <item>the roll is the same on every run, and every plan is used.</item>
    /// </list>
    /// </summary>
    public class CityPlanTests
    {
        private const int ROLLS = 12;
        private const float EPSILON = 1e-3f;

        private static CitySceneConfig ConfigOf(int roll)
        {
            var config = new CitySceneConfig();
            config.Seed += roll;
            config.NeonLayout.Seed += roll;
            return config;
        }

        private static (Vector2 Min, Vector2 Max, float Bottom, float Top) Extent(Matrix world)
        {
            Vector3 half = new Vector3(MathF.Abs(world.M11), MathF.Abs(world.M22), MathF.Abs(world.M33)) * 0.5f;
            Vector3 centre = world.Translation;
            return (new Vector2(centre.X - half.X, centre.Z - half.Z), new Vector2(centre.X + half.X, centre.Z + half.Z),
                centre.Y - half.Y, centre.Y + half.Y);
        }

        //The default cities as the code before #1066 built them (origin/main cc5fcf45): the box count and an FNV-1a hash of
        //every box's size and centre, in order. The city built without plans must still be exactly this, which is what says
        //the layout's generator and the tiers' stream are drawn as they were (a comparison with the plain city alone could
        //not: both are built by the same code)
        private const int DAY_BOXES_BEFORE = 4328, NEON_BOXES_BEFORE = 5716;
        private const uint DAY_HASH_BEFORE = 4269295069u, NEON_HASH_BEFORE = 2169540085u;

        [Fact]
        public void The_city_without_plans_is_the_city_before_them()
        {
            foreach (bool neon in new[] { false, true })
            {
                var plain = new City(new CitySceneConfig(), neon, ArenaIsland.RADIUS, variedPlans: false);
                uint h = 2166136261u;
                unchecked
                {
                    foreach (ModelInstance box in plain.Buildings)
                    {
                        Matrix m = box.World;
                        foreach (float f in new[] { m.M11, m.M22, m.M33, m.M41, m.M42, m.M43 })
                            h = (h ^ (uint)BitConverter.SingleToInt32Bits(f)) * 16777619u;
                    }
                }
                Assert.Equal(neon ? NEON_BOXES_BEFORE : DAY_BOXES_BEFORE, plain.Buildings.Length);
                Assert.Equal(neon ? NEON_HASH_BEFORE : DAY_HASH_BEFORE, h);
            }
        }

        [Fact]
        public void The_plots_and_their_tops_are_the_city_without_plans()
        {
            for (int roll = 0; roll < ROLLS; roll++)
                foreach (bool neon in new[] { false, true })
                {
                    var plain = new City(ConfigOf(roll), neon, ArenaIsland.RADIUS, variedPlans: false);
                    var varied = new City(ConfigOf(roll), neon, ArenaIsland.RADIUS);

                    Assert.Equal(plain.Plots.Count, varied.Plots.Count);
                    Assert.Equal(plain.BlockBuilt, varied.BlockBuilt);
                    for (int p = 0; p < plain.Plots.Count; p++)
                    {
                        Assert.Equal(plain.Plots[p].Anchor, varied.Plots[p].Anchor);
                        Assert.Equal(plain.Plots[p].Side, varied.Plots[p].Side);
                        Assert.Equal(plain.Plots[p].Top, varied.Plots[p].Top);
                    }
                }
        }

        [Fact]
        public void Every_box_stands_inside_the_old_footprint_and_the_tallest_reaches_the_top()
        {
            for (int roll = 0; roll < ROLLS; roll++)
                foreach (bool neon in new[] { false, true })
                {
                    var plain = new City(ConfigOf(roll), neon, ArenaIsland.RADIUS, variedPlans: false);
                    var varied = new City(ConfigOf(roll), neon, ArenaIsland.RADIUS);

                    for (int p = 0; p < varied.Plots.Count; p++)
                    {
                        CityPlot plot = varied.Plots[p];
                        //The plain city's first box of a plot is its footprint, from the street to its first tier's top
                        var footprint = Extent(plain.Buildings[plain.Plots[p].FirstBox].World);
                        float highest = float.MinValue;

                        for (int b = plot.FirstBox; b < plot.FirstBox + plot.BoxCount; b++)
                        {
                            var box = Extent(varied.Buildings[b].World);
                            string where = $"roll {roll}, neon {neon}, plot {p} ({plot.Plan}), box {b - plot.FirstBox}";

                            Assert.True(box.Min.X >= footprint.Min.X - EPSILON && box.Min.Y >= footprint.Min.Y - EPSILON
                                && box.Max.X <= footprint.Max.X + EPSILON && box.Max.Y <= footprint.Max.Y + EPSILON,
                                $"{where}: {box.Min}-{box.Max} leaves the footprint {footprint.Min}-{footprint.Max}");
                            Assert.True(box.Top <= plot.Top + EPSILON, $"{where}: its top {box.Top} is over the plot's {plot.Top}");
                            //No needle: a roof under 1.8 across splits into cells of negative size in the dressing
                            Assert.True(box.Max.X - box.Min.X > 2f && box.Max.Y - box.Min.Y > 2f && box.Top - box.Bottom > 1f,
                                $"{where}: a sliver {box.Max - box.Min} by {box.Top - box.Bottom}");
                            highest = MathF.Max(highest, box.Top);
                        }
                        Assert.True(MathF.Abs(highest - plot.Top) < EPSILON, $"roll {roll}, neon {neon}, plot {p} ({plot.Plan}): reaches {highest}, not {plot.Top}");
                    }
                }
        }

        [Fact]
        public void No_two_boxes_of_a_tower_share_a_wall()
        {
            for (int roll = 0; roll < ROLLS; roll++)
                foreach (bool neon in new[] { false, true })
                {
                    var city = new City(ConfigOf(roll), neon, ArenaIsland.RADIUS);
                    foreach (CityPlot plot in city.Plots)
                        for (int i = plot.FirstBox; i < plot.FirstBox + plot.BoxCount; i++)
                            for (int j = i + 1; j < plot.FirstBox + plot.BoxCount; j++)
                            {
                                var a = Extent(city.Buildings[i].World);
                                var b = Extent(city.Buildings[j].World);

                                //Two roofs in one plane where the boxes overlap fight for the same pixels (an L's wing level
                                //with its main block's roof did, over the strip it reaches into it)
                                bool overlapInPlan = MathF.Min(a.Max.X, b.Max.X) - MathF.Max(a.Min.X, b.Min.X) > EPSILON
                                    && MathF.Min(a.Max.Y, b.Max.Y) - MathF.Max(a.Min.Y, b.Min.Y) > EPSILON;
                                Assert.False(overlapInPlan && MathF.Abs(a.Top - b.Top) < 0.05f,
                                    $"roll {roll}, neon {neon}, a {plot.Plan} at {plot.Anchor}: boxes {i - plot.FirstBox} and {j - plot.FirstBox} share a roof at {a.Top}");

                                //Boxes one over the other only touch; walls side by side need an overlap in height
                                if (MathF.Min(a.Top, b.Top) - MathF.Max(a.Bottom, b.Bottom) <= EPSILON) continue;

                                for (int axis = 0; axis < 2; axis++)
                                {
                                    float aLow = axis == 0 ? a.Min.X : a.Min.Y, aHigh = axis == 0 ? a.Max.X : a.Max.Y;
                                    float bLow = axis == 0 ? b.Min.X : b.Min.Y, bHigh = axis == 0 ? b.Max.X : b.Max.Y;
                                    //The wall's run along the other axis
                                    float aFrom = axis == 0 ? a.Min.Y : a.Min.X, aTo = axis == 0 ? a.Max.Y : a.Max.X;
                                    float bFrom = axis == 0 ? b.Min.Y : b.Min.X, bTo = axis == 0 ? b.Max.Y : b.Max.X;
                                    bool alongside = MathF.Min(aTo, bTo) - MathF.Max(aFrom, bFrom) > EPSILON;
                                    if (!alongside) continue;

                                    foreach (float wallA in new[] { aLow, aHigh })
                                        foreach (float wallB in new[] { bLow, bHigh })
                                            Assert.False(MathF.Abs(wallA - wallB) < 0.05f,
                                                $"roll {roll}, neon {neon}, a {plot.Plan} at {plot.Anchor}: boxes {i - plot.FirstBox} and {j - plot.FirstBox} share a wall at {wallA}");
                                }
                            }
                }
        }

        [Fact]
        public void Every_box_of_a_tower_carries_its_kind_and_anchor()
        {
            var city = new City(ConfigOf(0), false, ArenaIsland.RADIUS);
            foreach (CityPlot plot in city.Plots)
            {
                Vector4 first = city.Buildings[plot.FirstBox].Custom;
                for (int b = plot.FirstBox; b < plot.FirstBox + plot.BoxCount; b++)
                {
                    Vector4 style = city.Buildings[b].Custom;
                    Assert.Equal(first.X, style.X);
                    Assert.Equal(0f, style.Y);
                    Assert.Equal(plot.Anchor, new Vector2(style.Z, style.W));
                }
            }
            //The cornices carry it too, and are marked
            for (int b = city.TowerCount; b < city.Buildings.Length; b++)
                Assert.Equal(1f, city.Buildings[b].Custom.Y);
        }

        [Fact]
        public void The_roll_is_the_same_every_time_and_uses_every_plan()
        {
            foreach (bool neon in new[] { false, true })
            {
                var first = new City(ConfigOf(3), neon, ArenaIsland.RADIUS);
                var second = new City(ConfigOf(3), neon, ArenaIsland.RADIUS);
                Assert.Equal(first.Buildings.Length, second.Buildings.Length);
                for (int b = 0; b < first.Buildings.Length; b++)
                {
                    Assert.Equal(first.Buildings[b].World, second.Buildings[b].World);
                    Assert.Equal(first.Buildings[b].Custom, second.Buildings[b].Custom);
                }

                var used = new HashSet<CityPlan>();
                foreach (CityPlot plot in first.Plots) used.Add(plot.Plan);
                Assert.Equal(Enum.GetValues<CityPlan>().Length, used.Count);
            }
        }
    }
}
