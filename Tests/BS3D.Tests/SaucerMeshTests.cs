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
    /// The flying saucer's two crafts (#1173, #1174), with no device: Low and Medium's two lathes as #955 built them, and High and
    /// Ultra's parts (<see cref="SaucerFineMesh"/>), which must spend more triangles, stand where the shader's rings put their
    /// light, keep the crew inside the glass and be shut solids wound clockwise seen from outside.
    /// </summary>
    public class SaucerMeshTests
    {
        private readonly ITestOutputHelper _output;

        public SaucerMeshTests(ITestOutputHelper output) => _output = output;

        private static (List<VertexPositionNormalTexture> Vertices, List<int> Indices) Part(SaucerFineMesh.Part part) =>
            SaucerFineMesh.Build(part).Geometry;

        private static IEnumerable<SaucerFineMesh.Part> Parts => Enum.GetValues<SaucerFineMesh.Part>();

        //Where Saucer.fx paints and lights things, written out here rather than read off the builder, so where the builder puts
        //a part is checked against the shader and not against the builder's own formula: RIM_LIGHTS and TOP_LIGHTS, whose
        //Bulbs and BulbHalo centre bulb i in the middle of its slot, and CREW, whose figure k stands where
        //frac(angle / 2pi * CREW + 0.25) is a half
        private const int RIM_LIGHTS = 24, TOP_LIGHTS = 16, CREW = 3;
        private static float PaintedBulb(int i, int count) => MathHelper.TwoPi * (i + 0.5f) / count;
        private static float PaintedCrew(int k) => MathHelper.TwoPi * (k + 0.25f) / CREW;

        //By number: the part enum is internal to the library, and a public theory cannot take it
        public static TheoryData<int> AllParts()
        {
            var data = new TheoryData<int>();
            foreach (SaucerFineMesh.Part part in Parts) data.Add((int)part);
            return data;
        }

        /// <summary>Low and Medium keep #955's craft to the triangle: the issue's count, 1056 in the hull and 1152 in the dome.</summary>
        [Fact]
        public void LowAndMediumKeepTheCoarseCraft()
        {
            using var coarse = new SaucerMesh(null);
            Assert.Equal(1056, coarse.Hull.PrimitiveCount);
            Assert.Equal(1152, coarse.Dome.PrimitiveCount);
        }

        /// <summary>High and Ultra spend many times the coarse craft's 2208 triangles, and each part has some.</summary>
        [Fact]
        public void HighAndUltraSpendMoreTriangles()
        {
            int total = 0;
            foreach (SaucerFineMesh.Part part in Parts)
            {
                int triangles = Part(part).Indices.Count / 3;
                Assert.True(triangles > 0, $"{part} has no triangles");
                _output.WriteLine($"{part}: {triangles} triangles");
                total += triangles;
            }
            _output.WriteLine($"in all {total}");
            Assert.True(total > 4 * 2208, $"the fine craft has {total} triangles");
        }

        /// <summary>Every part is shut: under back-face culling an open edge shows the inside of the craft through its wall.</summary>
        [Theory]
        [MemberData(nameof(AllParts))]
        public void EveryPartIsShut(int which)
        {
            var part = (SaucerFineMesh.Part)which;
            var (vertices, indices) = Part(part);
            List<(Vector3 A, Vector3 B)> open = MeshOpenness.OpenEdges(vertices, indices);
            Assert.True(open.Count == 0, $"{part}: {open.Count} open edges, the first {(open.Count > 0 ? open[0].ToString() : "")}");
        }

        /// <summary>
        /// Every triangle wound clockwise seen from outside (CLAUDE.md): its <c>(b - a) x (c - a)</c> points into the solid, against
        /// the outward normals the lighting uses (WindingCheck's first judgement, at its threshold).
        /// </summary>
        [Theory]
        [MemberData(nameof(AllParts))]
        public void EveryTriangleIsWoundClockwiseFromOutside(int which)
        {
            var part = (SaucerFineMesh.Part)which;
            var (vertices, indices) = Part(part);
            int wrong = 0;
            for (int t = 0; t < indices.Count; t += 3)
            {
                VertexPositionNormalTexture a = vertices[indices[t]], b = vertices[indices[t + 1]], c = vertices[indices[t + 2]];
                Vector3 geometric = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
                Vector3 normal = a.Normal + b.Normal + c.Normal;
                if (geometric.LengthSquared() < 1e-14f || normal.LengthSquared() < 1e-12f) continue;
                if (Vector3.Dot(Vector3.Normalize(geometric), Vector3.Normalize(normal)) > 0.25f) wrong++;
            }
            Assert.True(wrong == 0, $"{part}: {wrong} of {indices.Count / 3} triangles wound against their normals");
        }

        /// <summary>
        /// The crew stand inside the glass with room above their heads and beside them (#1174), on the cabin's floor: a figure
        /// through the glass would draw over the dome's mirror and be cut by its rim. And the whole cabin is under the glass.
        /// </summary>
        [Fact]
        public void TheCrewAndTheCabinStandInsideTheGlass()
        {
            //A figure keeps clear of the glass; the cabin's floor runs out to the glass's foot
            const float FIGURE_MARGIN = 0.05f, CABIN_MARGIN = 0.01f;
            var (vertices, _) = Part(SaucerFineMesh.Part.Crew);
            int figures = 0;
            foreach (VertexPositionNormalTexture v in vertices)
            {
                if (v.Position.Y <= SaucerMesh.DOME_BASE + 0.02f) continue;   //the floor's underside, in the hull's plate
                bool figure = v.TextureCoordinate.X == SaucerFineMesh.SKIN || v.TextureCoordinate.X == SaucerFineMesh.EYE;
                float margin = figure ? FIGURE_MARGIN : CABIN_MARGIN;
                float r = new Vector2(v.Position.X, v.Position.Z).Length();
                Assert.True(r < SaucerMesh.DOME_RADIUS - margin && v.Position.Y < SaucerFineMesh.GlassY(r) - margin,
                    $"a vertex of the cabin at {v.Position} (material {v.TextureCoordinate.X}) is not under the glass");
                if (v.TextureCoordinate.X == SaucerFineMesh.SKIN) figures++;
            }
            Assert.True(figures > 0);

            //Standing on the floor: the lowest point of the skin at the floor's height
            float lowest = vertices.Where(v => v.TextureCoordinate.X == SaucerFineMesh.SKIN).Min(v => v.Position.Y);
            Assert.InRange(lowest, SaucerFineMesh.FLOOR_Y - 0.02f, SaucerFineMesh.FLOOR_Y + 0.02f);
        }

        /// <summary>
        /// Three figures, one at each of the bearings the shader paints Low and Medium's crew at, each with two eyes on the side of
        /// its head that faces out of the dome.
        /// </summary>
        [Fact]
        public void TheCrewStandWhereTheyArePaintedLookingOut()
        {
            var (vertices, _) = Part(SaucerFineMesh.Part.Crew);
            Assert.Equal(CREW, SaucerFineMesh.CREW_BEARINGS.Length);
            for (int k = 0; k < CREW; k++)
            {
                float bearing = PaintedCrew(k);
                var outward = new Vector2(MathF.Cos(bearing), MathF.Sin(bearing));
                bool Near(Vector3 p) => Vector2.Dot(new Vector2(p.X, p.Z), outward) > SaucerFineMesh.CREW_RADIUS * 0.5f
                    && MathF.Abs(Vector2.Dot(new Vector2(p.X, p.Z), new Vector2(-outward.Y, outward.X))) < 0.6f;

                Vector3[] skin = vertices.Where(v => v.TextureCoordinate.X == SaucerFineMesh.SKIN && Near(v.Position)).Select(v => v.Position).ToArray();
                Vector3[] eyes = vertices.Where(v => v.TextureCoordinate.X == SaucerFineMesh.EYE && Near(v.Position)).Select(v => v.Position).ToArray();
                Assert.True(skin.Length > 0, $"no figure at {MathHelper.ToDegrees(bearing)} degrees");
                Assert.True(eyes.Length > 0, $"the figure at {MathHelper.ToDegrees(bearing)} degrees has no eyes");

                //Its middle on the painted bearing, and its eyes further out from the dome's axis than its middle: it looks out
                Vector2 middle = new(skin.Average(p => p.X), skin.Average(p => p.Z));
                Assert.InRange(MathHelper.WrapAngle(MathF.Atan2(middle.Y, middle.X) - bearing), -0.05f, 0.05f);
                Vector2 gaze = new(eyes.Average(p => p.X), eyes.Average(p => p.Z));
                Assert.True(Vector2.Dot(gaze - middle, outward) > 0.1f, $"the figure at {MathHelper.ToDegrees(bearing)} degrees looks in");
            }
        }

        /// <summary>
        /// The bulbs stand proud (#1173): every rim bulb reaches past the rim, and past its own socket, and every bulb's middle
        /// is where <c>Saucer.fx</c>'s rings throw their halo on the hull (the rim's middle height, the top ring's radius, and
        /// the middle of its slot round its ring), taken from the bulb's own vertices.
        /// </summary>
        [Fact]
        public void TheBulbsStandProudWhereTheShaderLightsThem()
        {
            Assert.Equal(RIM_LIGHTS, SaucerFineMesh.RIM_BULBS);
            Assert.Equal(TOP_LIGHTS, SaucerFineMesh.TOP_BULBS);
            var (bulbs, _) = Part(SaucerFineMesh.Part.Bulbs);
            var (hull, _) = Part(SaucerFineMesh.Part.Hull);
            float hullReach = hull.Max(v => new Vector2(v.Position.X, v.Position.Z).Length());
            Assert.InRange(hullReach, SaucerMesh.RIM_RADIUS - 0.01f, SaucerMesh.RIM_RADIUS + 0.05f);

            float rimY = 0.5f * (SaucerMesh.RIM_BAND.X + SaucerMesh.RIM_BAND.Y);
            for (int i = 0; i < RIM_LIGHTS; i++)
            {
                Vector3[] bulb = bulbs.Where(v => v.TextureCoordinate.X == 0f && v.TextureCoordinate.Y == i).Select(v => v.Position).ToArray();
                Assert.NotEmpty(bulb);
                float reach = bulb.Max(p => new Vector2(p.X, p.Z).Length());
                Assert.True(reach > hullReach + 0.1f, $"rim bulb {i} reaches {reach}, the hull {hullReach}");
                Vector3 centre = new(bulb.Average(p => p.X), bulb.Average(p => p.Y), bulb.Average(p => p.Z));
                Assert.InRange(centre.Y, rimY - 0.01f, rimY + 0.01f);
                Assert.InRange(MathHelper.WrapAngle(MathF.Atan2(centre.Z, centre.X) - PaintedBulb(i, RIM_LIGHTS)), -0.01f, 0.01f);
            }

            for (int i = 0; i < TOP_LIGHTS; i++)
            {
                Vector3[] bulb = bulbs.Where(v => v.TextureCoordinate.X == 1f && v.TextureCoordinate.Y == i).Select(v => v.Position).ToArray();
                Assert.NotEmpty(bulb);
                Vector3 centre = new(bulb.Average(p => p.X), bulb.Average(p => p.Y), bulb.Average(p => p.Z));
                Assert.InRange(new Vector2(centre.X, centre.Z).Length(), SaucerMesh.TOP_RING.X - 0.05f, SaucerMesh.TOP_RING.X + 0.05f);
                Assert.InRange(centre.Y, SaucerMesh.TOP_RING.Y - 0.06f, SaucerMesh.TOP_RING.Y + 0.12f);
                Assert.InRange(MathHelper.WrapAngle(MathF.Atan2(centre.Z, centre.X) - PaintedBulb(i, TOP_LIGHTS)), -0.01f, 0.01f);
                //Its top stands over the slope round it
                Assert.True(bulb.Max(p => p.Y) > SaucerFineMesh.TopY(SaucerMesh.TOP_RING.X) + 0.12f, $"top bulb {i} does not stand proud");
            }
        }

        /// <summary>
        /// The shader rolls a panel a shade apart only where the radial seams cut the hull (#1173): the texture coordinate's Y is
        /// <see cref="SaucerFineMesh.SEAM_CUT"/> on the top's and the underside's slopes alone, so the lip, the rim, the collar,
        /// the belly's ring, the sockets and the legs, which no seam crosses, mirror evenly and step no shade where the mesh has
        /// no edge. And the top's slope lies above the craft's middle plane and the underside's below it, which is how the
        /// shader tells the top's grooves from the underside's.
        /// </summary>
        [Fact]
        public void PanelsAreRolledOnlyWhereTheSeamsCut()
        {
            const float SLACK = 0.05f;
            var (hull, _) = Part(SaucerFineMesh.Part.Hull);
            int top = 0, under = 0;
            foreach (VertexPositionNormalTexture v in hull)
            {
                if (v.TextureCoordinate.Y == 0f) continue;
                Assert.Equal(SaucerFineMesh.SEAM_CUT, v.TextureCoordinate.Y);
                Assert.Equal(SaucerFineMesh.CHROME, v.TextureCoordinate.X);
                float r = new Vector2(v.Position.X, v.Position.Z).Length();
                if (v.Position.Y > 0f)
                {
                    Assert.InRange(r, SaucerFineMesh.SLOPE_FROM - SLACK, SaucerFineMesh.SLOPE_TO + SLACK);
                    top++;
                }
                else
                {
                    Assert.InRange(r, SaucerFineMesh.UNDER_TO - SLACK, SaucerFineMesh.UNDER_FROM + SLACK);
                    under++;
                }
            }
            Assert.True(top > 0 && under > 0, $"{top} vertices of the top's panels and {under} of the underside's");

            //The lip and the rim's bullnose, outside both slopes and over the underside's start, all even
            VertexPositionNormalTexture[] rim = hull.Where(v => new Vector2(v.Position.X, v.Position.Z).Length() > 6.7f && v.Position.Y > -0.3f).ToArray();
            Assert.NotEmpty(rim);
            Assert.All(rim, v => Assert.Equal(0f, v.TextureCoordinate.Y));
        }

        /// <summary>
        /// The hull's real parts (#1173): its seams cut into it (vertices sunk under the smooth slope, along each seam), its
        /// concentric grooves in the profile, its belly's lens recessed inside a ring that stands lower, and three legs reaching
        /// below everything else.
        /// </summary>
        [Fact]
        public void TheHullCarriesItsParts()
        {
            var (hull, _) = Part(SaucerFineMesh.Part.Hull);

            //The radial seams: on the top's slope, vertices sunk by the seam's depth under the curve at each panel's edge
            float panel = MathHelper.TwoPi / SaucerFineMesh.PANELS;
            for (int p = 0; p < SaucerFineMesh.PANELS; p++)
            {
                bool sunk = hull.Any(v =>
                {
                    float r = new Vector2(v.Position.X, v.Position.Z).Length();
                    float off = MathHelper.WrapAngle(MathF.Atan2(v.Position.Z, v.Position.X) - p * panel);
                    return r > 4.2f && r < 5.8f && MathF.Abs(off) < 1e-3f && v.Position.Y < SaucerFineMesh.TopY(r) - SaucerFineMesh.SEAM_DEPTH * 0.8f
                        && v.Position.Y > 0f;
                });
                Assert.True(sunk, $"no seam cut along {MathHelper.ToDegrees(p * panel)} degrees");
            }

            //The concentric grooves: a ring of the profile sunk under each curve at each groove's radius
            var profile = SaucerFineMesh.HullProfile();
            Vector3 grooves = SaucerFineMesh.Grooves;
            foreach ((float radius, Func<float, float> y, float sign) in new (float, Func<float, float>, float)[]
                { (grooves.X, SaucerFineMesh.TopY, 1f), (grooves.Y, SaucerFineMesh.TopY, 1f), (grooves.Z, SaucerFineMesh.UnderY, -1f) })
                Assert.True(profile.Any(ring => MathF.Abs(ring.R - radius) < 0.02f && sign * (y(ring.R) - ring.Y) > SaucerFineMesh.SEAM_DEPTH * 0.8f),
                    $"no groove cut at radius {radius}");

            //The lens, recessed: its lowest point above the lowest of the ring round it
            float lens = hull.Where(v => v.TextureCoordinate.X == SaucerFineMesh.LENS).Min(v => v.Position.Y);
            float ring = hull.Where(v => new Vector2(v.Position.X, v.Position.Z).Length() is > 2.8f and < 3.4f).Min(v => v.Position.Y);
            Assert.True(lens > ring + 0.05f, $"the lens ({lens}) is not recessed inside its ring ({ring})");

            //The legs: three pods, the lowest points of the craft
            Vector3[] low = hull.Where(v => v.Position.Y < -1.5f).Select(v => v.Position).ToArray();
            foreach (float bearing in SaucerFineMesh.LEG_BEARINGS)
                Assert.Contains(low, p => MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - bearing)) < 0.1f);
            Assert.All(low, p => Assert.Contains(SaucerFineMesh.LEG_BEARINGS,
                bearing => MathF.Abs(MathHelper.WrapAngle(MathF.Atan2(p.Z, p.X) - bearing)) < 0.1f));
        }

        /// <summary>
        /// The antenna is its own opaque mesh, standing on the glass's crown, and the glass has none (#1174): blended with the
        /// dome it would be see-through too.
        /// </summary>
        [Fact]
        public void TheAntennaStandsOnTheCrownAndTheGlassHasNone()
        {
            float crown = SaucerMesh.DOME_BASE + SaucerMesh.DOME_HEIGHT;
            var (antenna, _) = Part(SaucerFineMesh.Part.Antenna);
            var (glass, _) = Part(SaucerFineMesh.Part.Glass);
            Assert.True(antenna.Max(v => v.Position.Y) > crown + 0.9f);
            Assert.True(antenna.Min(v => v.Position.Y) < crown);
            Assert.True(glass.Max(v => v.Position.Y) <= crown + 1e-4f, "the glass reaches above its crown");
            Assert.All(glass, v => Assert.True(new Vector2(v.Position.X, v.Position.Z).Length() <= SaucerMesh.DOME_RADIUS + 1e-4f));
        }
    }
}
