using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna's scrub and treeline masses as shrubs near the lens on High and Ultra (#1195). The owner: "the green blobs. On
    /// High and Ultra it should already be shaped bushes. They should show as blobs like this only when they are very far from the
    /// camera and the detail would be too expensive." Each dome is drawn as the meadow's shrub (<see cref="MeadowShrubMesh"/>) within
    /// a radius of the lens and as itself beyond (<see cref="ScatterSwap"/>), off the dome's own instances. What is held here: every
    /// shrub stands inside the volume its dome was planted by and inside the dome's sphere (so nothing the planting or a chapter
    /// intro's lens was kept clear of moves), every plant is drawn once in a pass whichever side of the radius it stands, and the
    /// radii say what the docs say they do from the play camera. Planted with no device, as the game plants the plain.
    /// </summary>
    public class SavannaShapedScrubTests
    {
        private readonly ITestOutputHelper _output;

        public SavannaShapedScrubTests(ITestOutputHelper output) => _output = output;

        //The scene seeds the shrubs are built at (each one rolls its domes' sizes anew)
        private static readonly int[] LAYOUTS = { 0, 1, 2, 3, 4 };

        [Fact]
        public void EveryShrubStandsInsideItsDome()
        {
            int shrubs = 0;
            float worstSlabReach = 0f, worstSlabTop = 0f, worstSphere = 0f;
            foreach (int layout in LAYOUTS)
            {
                SavannaScatter scatter = SavannaStoneTests.Plant(layout, out _);
                Assert.Equal(2 * SavannaScatter.SCRUB_SHAPES + 2 * SavannaScatter.TREELINE_SHAPES, scatter.Shaped.Count);
                foreach (SavannaScatter.ShapedPlant plant in scatter.Shaped)
                {
                    MeadowShrubGeometry shrub = MeadowShrubMesh.Build(plant.Reach, plant.Top, plant.Seed, plant.Card, plant.Cover, plant.RootSpread);
                    Slab body = scatter.Bodies[plant.Dome].Body[0];
                    BoundingSphere dome = plant.Dome.BoundingSphere;

                    float widest = 0f, top = float.MinValue, bottom = float.MaxValue, sphere = 0f;
                    foreach (var list in new[] { shrub.WoodVertices, shrub.LeafVertices })
                        foreach (VertexPositionNormalTexture v in list)
                        {
                            widest = MathF.Max(widest, MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z));
                            top = MathF.Max(top, v.Position.Y);
                            bottom = MathF.Min(bottom, v.Position.Y);
                            sphere = MathF.Max(sphere, Vector3.Distance(v.Position, dome.Center) / dome.Radius);
                        }

                    string at = $"sceneseed={layout}, a {plant.Kind} shrub (seed {plant.Seed})";

                    //Inside the slab its dome was planted by (#653's volumes, #1157's rocks): as wide and as high as the slab at the most,
                    //and only its stems' feet under the pivot (MeadowShrubMesh.FOOT_DEPTH of its height, and the foot's ring round it,
                    //tipped as the stem leans out)
                    Assert.True(widest <= body.Radius * 1.0001f, $"{at} reaches {widest:0.00} from its axis, over its dome's slab's {body.Radius:0.00}");
                    Assert.True(top <= body.Top * 1.0001f, $"{at} stands {top:0.00} high, over its dome's slab's {body.Top:0.00}");
                    Assert.True(bottom >= -0.15f * body.Top, $"{at} reaches {-bottom:0.00} under its pivot");

                    //And inside the dome's sphere, which is what SavannaScatter.Solids holds the chapter intro's lens off
                    Assert.True(sphere <= 1f, $"{at} reaches {sphere:0.000} of its dome's sphere");

                    worstSlabReach = MathF.Max(worstSlabReach, widest / body.Radius);
                    worstSlabTop = MathF.Max(worstSlabTop, top / body.Top);
                    worstSphere = MathF.Max(worstSphere, sphere);
                    shrubs++;
                    if (layout == 0)
                        _output.WriteLine($"{at}: {shrub.LeafVertices.Count / 4} cards, {shrub.WoodIndices.Count / 3} wood and {shrub.LeafIndices.Count / 3} leaf triangles, "
                            + $"reach {widest:0.00} of {body.Radius:0.00}, top {top:0.00} of {body.Top:0.00}, {sphere:0.000} of the dome's sphere");
                }
            }
            _output.WriteLine($"{shrubs} shrubs: at most {worstSlabReach * 100f:0} % of the slab's radius and {worstSlabTop * 100f:0} % of its height, {worstSphere * 100f:0} % of the dome's sphere");
        }
    }
}
