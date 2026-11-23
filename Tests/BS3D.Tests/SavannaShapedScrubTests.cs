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
            float worstSphere = 0f, highestSkirt = 0f;
            float treelineGround = SavannaScatter.TREELINE_SINK * new SavannaSceneConfig().Dressing.TreelineSize;
            foreach (int layout in LAYOUTS)
            {
                SavannaScatter scatter = SavannaStoneTests.Plant(layout, out _);
                Assert.Equal(2 * SavannaScatter.SCRUB_SHAPES + 2 * SavannaScatter.TREELINE_SHAPES, scatter.Shaped.Count);
                foreach (SavannaScatter.ShapedPlant plant in scatter.Shaped)
                {
                    MeadowShrubGeometry shrub = MeadowShrubMesh.Build(plant.Reach, plant.Top, plant.Seed, plant.Card, plant.Cover, plant.Form);
                    BoundingSphere dome = plant.Dome.BoundingSphere;

                    float widest = 0f, top = float.MinValue, leafBottom = float.MaxValue, sphere = 0f;
                    foreach (var list in new[] { shrub.WoodVertices, shrub.LeafVertices })
                        foreach (VertexPositionNormalTexture v in list)
                        {
                            widest = MathF.Max(widest, MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z));
                            top = MathF.Max(top, v.Position.Y);
                            sphere = MathF.Max(sphere, Vector3.Distance(v.Position, dome.Center) / dome.Radius);
                        }
                    float woodBottom = float.MaxValue;
                    foreach (VertexPositionNormalTexture v in shrub.LeafVertices) leafBottom = MathF.Min(leafBottom, v.Position.Y);
                    foreach (VertexPositionNormalTexture v in shrub.WoodVertices) woodBottom = MathF.Min(woodBottom, v.Position.Y);

                    //The skirt: how low the leaf comes at the rim, the lowest card a quarter of the reach or more out from the axis
                    float skirt = float.MaxValue;
                    foreach (VertexPositionNormalTexture v in shrub.LeafVertices)
                        if (MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z) >= 0.25f * widest) skirt = MathF.Min(skirt, v.Position.Y);

                    //Where the ground stands over the pivot in the shrub's own units: a scrub is planted on it, a treeline mass sunk in it
                    string at = $"sceneseed={layout}, a {plant.Kind} shrub (seed {plant.Seed})";
                    float ground = plant.Kind == "treeline" ? treelineGround : 0f;
                    float over = (skirt - ground) / top;
                    if (layout == 0)
                        _output.WriteLine($"{at}: {shrub.LeafVertices.Count / 4} cards, {shrub.WoodIndices.Count / 3} wood and {shrub.LeafIndices.Count / 3} leaf triangles, "
                            + $"reach {widest:0.00} of {plant.Reach:0.00}, top {top:0.00} of {plant.Top:0.00}, {sphere:0.000} of the dome's sphere, "
                            + $"the rim's leaf {skirt - ground:0.00} over the ground, the lowest leaf {leafBottom - ground:0.00}, the lowest stem {woodBottom:0.00} on the pivot");

                    //Within the reach and the top it was fitted to (its dome's, MeadowScatter.FineShrubSize); the stems' feet alone under
                    //the pivot (MeadowShrubMesh.FOOT_DEPTH of the height, the foot's ring round it tipped as the stem leans out), and no
                    //leaf deep under the ground (a leaf no one sees is a leaf paid for; a little under it is the downhill rim's on a slope,
                    //the ground being the one at the pivot)
                    Assert.True(widest <= plant.Reach * 1.0001f, $"{at} reaches {widest:0.00} from its axis, over the {plant.Reach:0.00} it was fitted to");
                    Assert.True(top <= plant.Top * 1.0001f, $"{at} stands {top:0.00} high, over the {plant.Top:0.00} it was fitted to");
                    Assert.True(woodBottom >= -0.15f * plant.Top, $"{at}: a stem reaches {-woodBottom:0.00} under its pivot");
                    Assert.True(leafBottom - ground >= -0.3f * plant.Top, $"{at}: a leaf {ground - leafBottom:0.00} under the ground");

                    //Inside the dome's sphere, which is what SavannaScatter.Solids holds the chapter intro's lens off
                    Assert.True(sphere <= 1f, $"{at} reaches {sphere:0.000} of its dome's sphere");

                    //And standing on the ground as the dome did, not floating over it: a scrub's leaf comes down to within a fifth of its
                    //height of the grass, a treeline mass's under the ground it is sunk in
                    Assert.True(over <= (plant.Kind == "treeline" ? 0f : 0.2f), $"{at}: its leaf's rim stops {skirt - ground:0.00} over the ground ({over:0.00} of its height)");

                    worstSphere = MathF.Max(worstSphere, sphere);
                    highestSkirt = MathF.Max(highestSkirt, over);
                    shrubs++;
                }
            }
            _output.WriteLine($"{shrubs} shrubs: at most {worstSphere * 100f:0} % of the dome's sphere; the rim's leaf at most {highestSkirt:0.00} of the height over the ground");
        }
    }
}
