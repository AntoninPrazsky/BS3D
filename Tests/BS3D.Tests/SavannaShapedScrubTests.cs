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

        //The scene seeds the shrubs are built at (each one rolls its domes' sizes anew), and the bearings a skirt is asked about
        private static readonly int[] LAYOUTS = { 0, 1, 2, 3, 4 };
        private const int SKIRT_SECTORS = 8;

        [Fact]
        public void EveryShrubStandsInsideItsDome()
        {
            int shrubs = 0;
            float worstSphere = 0f, highestSkirt = 0f, scrubSkirt = float.MinValue, treelineSkirt = float.MinValue;
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

                    //The skirt: how low the leaf comes all round the rim. The lowest card corner a quarter of the reach or more out from
                    //the axis in each of SKIRT_SECTORS bearings, and the highest of those: one card drooping on one side does not make a
                    //skirt, and a gap over the grass on any side is what reads as a shrub on a stick
                    var lowest = new float[SKIRT_SECTORS];
                    Array.Fill(lowest, float.MaxValue);
                    foreach (VertexPositionNormalTexture v in shrub.LeafVertices)
                    {
                        if (MathF.Sqrt(v.Position.X * v.Position.X + v.Position.Z * v.Position.Z) < 0.25f * widest) continue;
                        int sector = (int)((MathF.Atan2(v.Position.Z, v.Position.X) + MathF.PI) / MathHelper.TwoPi * SKIRT_SECTORS) % SKIRT_SECTORS;
                        lowest[sector] = MathF.Min(lowest[sector], v.Position.Y);
                    }
                    float skirt = lowest.Max();

                    //Where the ground stands over the pivot in the shrub's own units: a scrub is planted on it, a treeline mass sunk in it
                    string at = $"sceneseed={layout}, a {plant.Kind} shrub (seed {plant.Seed})";
                    float ground = plant.Kind == "treeline" ? treelineGround : 0f;
                    float over = (skirt - ground) / top;
                    if (layout == 0)
                        _output.WriteLine($"{at}: {shrub.LeafVertices.Count / 4} cards, {shrub.WoodIndices.Count / 3} wood and {shrub.LeafIndices.Count / 3} leaf triangles, "
                            + $"reach {widest:0.00} of {plant.Reach:0.00}, top {top:0.00} of {plant.Top:0.00}, {sphere:0.000} of the dome's sphere, "
                            + $"the rim's leaf {skirt - ground:0.00} over the ground on its highest side, the lowest leaf {leafBottom - ground:0.00}, the lowest stem {woodBottom:0.00} on the pivot");

                    //Within the reach and the top it was fitted to (its dome's, MeadowScatter.FineShrubSize); the stems' feet alone under
                    //the pivot (MeadowShrubMesh.FOOT_DEPTH of the height and what its form's FootSlope carries them on, the foot's ring
                    //round it tipped as the stem leans out; EveryStemsFootIsUnderTheGround holds them under the real ground), and no
                    //leaf deep under the ground (a leaf no one sees is a leaf paid for; a little under it is the downhill rim's on a slope,
                    //the ground being the one at the pivot)
                    Assert.True(widest <= plant.Reach * 1.0001f, $"{at} reaches {widest:0.00} from its axis, over the {plant.Reach:0.00} it was fitted to");
                    Assert.True(top <= plant.Top * 1.0001f, $"{at} stands {top:0.00} high, over the {plant.Top:0.00} it was fitted to");
                    Assert.True(woodBottom >= -0.15f * plant.Top, $"{at}: a stem reaches {-woodBottom:0.00} under its pivot");
                    Assert.True(leafBottom - ground >= -0.3f * plant.Top, $"{at}: a leaf {ground - leafBottom:0.00} under the ground");

                    //Inside the dome's sphere, which is what SavannaScatter.Solids holds the chapter intro's lens off
                    Assert.True(sphere <= 1f, $"{at} reaches {sphere:0.000} of its dome's sphere");

                    //And standing on the ground as the dome did, not floating over it: its leaf comes down all round to within an eighth of
                    //its height of the grass (a treeline mass's a tenth, of the ground it is sunk in). The meadow's form, a dome on a vase of
                    //bare stems, stops 0.15 to 0.28 of a scrub's height over it on its highest side; this one 0.11 at the most
                    Assert.True(over <= (plant.Kind == "treeline" ? 0.1f : 0.125f), $"{at}: its leaf's rim stops {skirt - ground:0.00} over the ground on one side ({over:0.00} of its height)");

                    worstSphere = MathF.Max(worstSphere, sphere);
                    highestSkirt = MathF.Max(highestSkirt, over);
                    if (plant.Kind == "treeline") treelineSkirt = MathF.Max(treelineSkirt, over); else scrubSkirt = MathF.Max(scrubSkirt, over);
                    shrubs++;
                }
            }
            _output.WriteLine($"{shrubs} shrubs: at most {worstSphere * 100f:0} % of the dome's sphere; the rim's leaf at most {scrubSkirt:0.00} of the height over the ground on any side of a scrub, {treelineSkirt:0.00} of a treeline mass");
        }

        //The scene seeds the feet are held on the ground over (each one plants every mass somewhere else on the band's hillsides),
        //and the bearings round a foot's end ring the ground is asked at
        private const int FOOT_LAYOUTS = 30, FOOT_RING_SAMPLES = 8;

        [Fact]
        public void EveryStemsFootIsUnderTheGround()
        {
            //The fault #658 and #938 fixed for the rest of the dressing: on a hillside the ground under a stem's foot is not the ground
            //at the plant's axis, and a treeline mass's stems leave the ground far apart (its form's RootSpread), so a foot only
            //FOOT_DEPTH under the ground at the axis hung its downhill trunks' ends in the air. Every stem of every planted scrub and
            //treeline mass, at its instance's frame on the ground the planting stands it on: the whole end ring of its foot (its
            //centre, up by the foot's radius, against the lowest ground round the ring) under the grass
            float scrubWorst = float.MinValue, treelineWorst = float.MinValue;
            string worstAt = "";
            int stems = 0, above = 0, scrubAbove = 0;
            for (int layout = 0; layout < FOOT_LAYOUTS; layout++)
            {
                SavannaScatter scatter = SavannaStoneTests.Plant(layout, out Func<float, float, float> ground);
                foreach (ScatterSwap swap in scatter.Swaps)
                {
                    ScatterBucket dome = Assert.Single(scatter.Buckets, b => b.Swap == swap && b.FineMeshes == null);
                    SavannaScatter.ShapedPlant[] shapes = scatter.Shaped.Where(p => ReferenceEquals(p.Dome, dome.Mesh)).ToArray();
                    Assert.Equal(swap.Shapes, shapes.Length);
                    MeadowShrubGeometry[] built = shapes.Select(p => MeadowShrubMesh.Build(p.Reach, p.Top, p.Seed, p.Card, p.Cover, p.Form)).ToArray();
                    bool treeline = shapes[0].Kind == "treeline";

                    for (int i = 0; i < swap.Placed.Count; i++)
                    {
                        Matrix world = swap.Placed[i].World;
                        float scale = MathF.Sqrt(world.M11 * world.M11 + world.M12 * world.M12 + world.M13 * world.M13);
                        MeadowShrubGeometry shrub = built[ScatterSwap.ShapeOf(i, swap.Shapes)];
                        float r = shrub.FootRadius * scale;
                        foreach (Vector3[] stem in shrub.Stems)
                        {
                            Vector3 foot = Vector3.Transform(stem[0], world);
                            float lowest = ground(foot.X, foot.Z);
                            for (int k = 0; k < FOOT_RING_SAMPLES; k++)
                            {
                                float a = k * MathHelper.TwoPi / FOOT_RING_SAMPLES;
                                lowest = MathF.Min(lowest, ground(foot.X + r * MathF.Cos(a), foot.Z + r * MathF.Sin(a)));
                            }
                            float over = foot.Y + r - lowest;
                            if (over > 0f) { above++; if (!treeline) scrubAbove++; }
                            if (treeline && over > treelineWorst)
                                worstAt = $"sceneseed={layout}, a {shapes[0].Kind} at ({world.M41:0.0}, {world.M43:0.0}) scale {scale:0.00}";
                            if (treeline) treelineWorst = MathF.Max(treelineWorst, over); else scrubWorst = MathF.Max(scrubWorst, over);
                            stems++;
                        }
                    }
                }
            }
            string worst = $"{stems} stems ({scrubAbove} of the scrub over): a scrub's foot at worst {-scrubWorst:0.00} under the ground, a treeline mass's {-treelineWorst:0.00} ({worstAt})";
            _output.WriteLine(worst);
            Assert.True(above == 0, $"{above} stems' feet stand over the ground; {worst}");
        }

        [Fact]
        public void TheFeetAreSunkForTheSteepestGroundTheyStandOn()
        {
            //What SCRUB_FOOT_SLOPE and TREELINE_FOOT_SLOPE say they are, so every foot is under the ground by construction and not
            //only at the seeds above: the steepest the ground rises anywhere the scrub is planted (the plain, AcaciaConfig's MinRadius
            //to MaxRadius) with the scrub's lean on top, and anywhere a treeline mass is (its band, and as far out as the rocks push
            //one, which PlayAndTheFrontEndNeverSwapAPlant measures under 480)
            var config = new SavannaSceneConfig();
            SavannaStoneTests.Plant(0, out Func<float, float, float> ground);
            float Steepest(float from, float to)
            {
                const float h = 0.25f;
                float steepest = 0f;
                for (float r = from; r <= to; r += 1f)
                    for (int k = 0; k < 1440; k++)
                    {
                        float a = k * MathHelper.TwoPi / 1440, x = MathF.Cos(a) * r, z = MathF.Sin(a) * r;
                        float gx = (ground(x + h, z) - ground(x - h, z)) / (2f * h), gz = (ground(x, z + h) - ground(x, z - h)) / (2f * h);
                        steepest = MathF.Max(steepest, MathF.Sqrt(gx * gx + gz * gz));
                    }
                return steepest;
            }
            float plain = Steepest(config.Acacia.MinRadius, config.Acacia.MaxRadius);
            float band = Steepest(config.Dressing.TreelineMinRadius, 480f);
            _output.WriteLine($"the plain's steepest {plain:0.000}, the band's {band:0.000}");
            Assert.True(plain + SavannaScatter.SCRUB_LEAN <= SavannaScatter.SCRUB_FOOT_SLOPE, $"the plain rises {plain:0.000} with the scrub's lean on top");
            Assert.True(band <= SavannaScatter.TREELINE_FOOT_SLOPE, $"the band rises {band:0.000}");
        }

        [Fact]
        public void EveryPlantIsDrawnOnceInAPassOnEveryTier()
        {
            SavannaScatter scatter = SavannaStoneTests.Plant(0, out _);
            Assert.Equal(4, scatter.Swaps.Length);     //the scrub's two dome meshes and the treeline's two

            int nearSeen = 0, farSeen = 0;
            foreach (ScatterSwap swap in scatter.Swaps)
            {
                //The buckets that carry it: the dome on the swap's whole planting, in its order, and each shape's wood and leaf on the
                //instances dealt to it
                ScatterBucket[] carrying = scatter.Buckets.Where(b => b.Swap == swap).ToArray();
                ScatterBucket dome = Assert.Single(carrying, b => b.FineMeshes == null);
                Assert.Equal(swap.Placed, dome.Placed);
                Assert.Equal(2 * swap.Shapes, carrying.Count(b => b.FineMeshes == true));
                foreach (ScatterBucket shaped in carrying.Where(b => b.FineMeshes == true))
                    Assert.Equal(ScatterSwap.Dealt(swap.Placed, swap.Shapes, shaped.SwapShape), shaped.Placed);

                //Below High the dome draws every plant from its own buffer, and nothing else draws one
                Assert.Equal((dome.Instances, dome.Count), SavannaBackdrop.Drawn(dome, fine: false));
                Assert.Equal(swap.Placed.Count, dome.Count);

                //On High and Ultra, from lenses on the plain, in the intro's far field and at the play camera, a camera's frustum and
                //one round the whole plain: every plant the frustum meets is drawn once, the shrub its shape is dealt near the lens
                //and the dome beyond, and no plant it misses is drawn at all
                foreach (Vector3 lens in new[] { new Vector3(0f, -7.4f, 30f), new Vector3(146f, 38f, -134f), new Vector3(-180f, 5f, 60f), new Vector3(320f, 4f, 40f) })
                    foreach (bool whole in new[] { false, true })
                    {
                        Matrix view = Matrix.CreateLookAt(lens, whole ? Vector3.Zero : lens + new Vector3(-lens.X, 0f, -lens.Z) * 0.5f + new Vector3(0f, -2f, 0f), Vector3.Up);
                        Matrix projection = whole
                            ? Matrix.CreateOrthographic(2000f, 2000f, -2000f, 2000f)
                            : Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f), 16f / 9f, 0.1f, 3000f);
                        var frustum = new BoundingFrustum(whole ? Matrix.CreateLookAt(new Vector3(0f, 500f, 0f), Vector3.Zero, Vector3.Forward) * projection : view * projection);
                        foreach (float radius in new[] { swap.Radius, swap.FarDetailRadius })
                        {
                            swap.Select(frustum, lens, radius);
                            var drawn = new Dictionary<Matrix, int>();
                            for (int k = 0; k < swap.Shapes; k++)
                                for (int n = 0; n < swap.NearCount[k]; n++)
                                {
                                    ModelInstance plant = swap.Near[k][n];
                                    Assert.True(ScatterSwap.IsNear(plant.World.Translation, lens, radius));
                                    Assert.Equal(k, ScatterSwap.ShapeOf(IndexOf(swap, plant), swap.Shapes));
                                    drawn[plant.World] = drawn.GetValueOrDefault(plant.World) + 1;
                                    nearSeen++;
                                }
                            for (int n = 0; n < swap.FarCount; n++)
                            {
                                Assert.False(ScatterSwap.IsNear(swap.Far[n].World.Translation, lens, radius));
                                drawn[swap.Far[n].World] = drawn.GetValueOrDefault(swap.Far[n].World) + 1;
                                farSeen++;
                            }

                            //What each side's buckets are handed is that side's count
                            Assert.Equal(swap.FarCount, SavannaBackdrop.Drawn(dome, fine: true).Count);
                            foreach (ScatterBucket shaped in carrying.Where(b => b.FineMeshes == true))
                                Assert.Equal(swap.NearCount[shaped.SwapShape], SavannaBackdrop.Drawn(shaped, fine: true).Count);

                            BoundingSphere local = dome.Mesh.BoundingSphere;
                            foreach (ScatterBucket b in carrying.Where(b => b.FineMeshes == true)) local = BoundingSphere.CreateMerged(local, b.Mesh.BoundingSphere);
                            foreach (ModelInstance plant in swap.Placed)
                            {
                                bool meets = frustum.Contains(Sphere(local, plant.World)) != ContainmentType.Disjoint;
                                Assert.Equal(meets ? 1 : 0, drawn.GetValueOrDefault(plant.World));
                            }
                        }
                    }
            }
            _output.WriteLine($"{nearSeen} shrubs and {farSeen} domes drawn over the passes");
            Assert.True(nearSeen > 0 && farSeen > 0);
        }

        [Fact]
        public void TheShrubsAreShutAndFitShortIndices()
        {
            //The meadow's rule for its shrub (MeadowMeshClosureTests): every stem and branch closed at both ends from its own rings, so
            //the wood's only open edges are the twigs' base rings, inside a stem's or a branch's end within three twig radii of a spray
            //base; and a few thousand triangles, the wood in 16-bit indices (the trap of #670), whatever form the savanna gives it
            SavannaScatter scatter = SavannaStoneTests.Plant(0, out _);
            foreach (SavannaScatter.ShapedPlant plant in scatter.Shaped)
            {
                MeadowShrubGeometry shrub = MeadowShrubMesh.Build(plant.Reach, plant.Top, plant.Seed, plant.Card, plant.Cover, plant.Form);
                string at = $"a {plant.Kind} shrub (seed {plant.Seed})";
                List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(shrub.WoodVertices, shrub.WoodIndices);
                Assert.NotEmpty(open);
                float near = shrub.TwigRadius * 3.05f;
                foreach ((Vector3 a, Vector3 b) in open)
                    Assert.True(shrub.SprayBases.Any(s => Vector3.Distance(s, a) <= near && Vector3.Distance(s, b) <= near),
                        $"{at}: an open edge of the wood at {a} is not a twig's base ring");

                int triangles = shrub.WoodIndices.Count / 3 + shrub.LeafIndices.Count / 3;
                Assert.True(shrub.WoodVertices.Count <= ushort.MaxValue + 1, $"{at}: {shrub.WoodVertices.Count} wood vertices");
                Assert.True(shrub.LeafVertices.Count <= ushort.MaxValue + 1, $"{at}: {shrub.LeafVertices.Count} leaf vertices");
                Assert.True(triangles <= 9000, $"{at}: {triangles} triangles");
            }
        }

        //The play camera stands about 30 out from the arena's axis on every savanna level (the Game's [camera] line: "camera 30,0 out")
        //and its walk takes it a few units further; held here with room to spare
        private const float PLAY_LENS_REACH = 45f;

        //The front end's orbit (the menu's, and the result page's, which hands its camera to it) stands further out the narrower the
        //window: its wide leg holds the island across 0.95 of the frame's horizontal half-angle under the 60-degree lens
        //(BackdropScreen.WideRadius; the map it frames binds only nearer), 46.5 out at the owner's half-screen window (aspect 1.25)
        //and 54.5 at a square one, the narrowest held here
        private const float SQUARE_WINDOW = 1f;
        private static float OrbitLensReach(float aspect) =>
            ArenaIsland.RADIUS / MathF.Sin(0.95f * MathF.Atan(MathF.Tan(MathHelper.ToRadians(30f)) * aspect));

        [Fact]
        public void PlayAndTheFrontEndNeverSwapAPlant()
        {
            //What the radii were chosen for (SavannaScatter.SCRUB_SWAP_RADIUS and the treeline's): from any lens the play camera or
            //the front end's orbit can stand at, every scrub is a shrub on High and Ultra, every treeline mass a dome on High and a
            //shrub on Ultra, so neither a turn of the gun nor the menu's slow orbit moves one across its radius; only a chapter
            //intro's lens far out on the plain does
            float scrubFarthest = 0f, treelineNearest = float.MaxValue, treelineFarthest = 0f;
            for (int layout = 0; layout < 40; layout++)
            {
                SavannaScatter scatter = SavannaStoneTests.Plant(layout, out _);
                foreach (ScatterSwap swap in scatter.Swaps)
                {
                    bool treeline = scatter.Buckets.Any(b => b.Swap == swap && b.FineMeshes == null && scatter.Bodies[b.Mesh].Kind == "treeline");
                    foreach (ModelInstance plant in swap.Placed)
                    {
                        float out_ = new Vector2(plant.World.M41, plant.World.M43).Length();
                        if (treeline)
                        {
                            treelineNearest = MathF.Min(treelineNearest, out_);
                            treelineFarthest = MathF.Max(treelineFarthest, out_);
                        }
                        else scrubFarthest = MathF.Max(scrubFarthest, out_);
                    }
                }
            }
            float orbit = OrbitLensReach(SQUARE_WINDOW);
            _output.WriteLine($"scrub out to {scrubFarthest:0.0} from the axis, the treeline's masses {treelineNearest:0.0} to {treelineFarthest:0.0}; "
                + $"the orbit's wide leg {OrbitLensReach(1.25f):0.0} out at aspect 1.25, {orbit:0.0} in a square window");
            foreach ((float reach, string from) in new[] { (PLAY_LENS_REACH, "a play lens"), (orbit, "the front end's orbit") })
            {
                Assert.True(scrubFarthest + reach < SavannaScatter.SCRUB_SWAP_RADIUS, $"a scrub {scrubFarthest:0.0} out is a dome from {from} on High");
                Assert.True(scrubFarthest + reach < SavannaScatter.SCRUB_FAR_DETAIL_SWAP_RADIUS, $"a scrub {scrubFarthest:0.0} out is a dome from {from} on Ultra");
                Assert.True(treelineNearest - reach > SavannaScatter.TREELINE_SWAP_RADIUS, $"a treeline mass {treelineNearest:0.0} out swaps on High from {from}");
                Assert.True(treelineFarthest + reach < SavannaScatter.TREELINE_FAR_DETAIL_SWAP_RADIUS, $"a treeline mass {treelineFarthest:0.0} out swaps on Ultra from {from}");
            }
        }

        private static int IndexOf(ScatterSwap swap, ModelInstance plant)
        {
            for (int i = 0; i < swap.Placed.Count; i++)
                if (swap.Placed[i].World == plant.World) return i;
            return -1;
        }

        private static BoundingSphere Sphere(BoundingSphere local, Matrix world)
        {
            float scale = MathF.Sqrt(world.M11 * world.M11 + world.M12 * world.M12 + world.M13 * world.M13);
            return new BoundingSphere(Vector3.Transform(local.Center, world), local.Radius * scale);
        }
    }
}
