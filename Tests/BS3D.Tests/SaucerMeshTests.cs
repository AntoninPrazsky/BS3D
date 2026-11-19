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
    /// The flying saucer's two hulls (#1173), with no device: Low and Medium's two lathes as #955 built them, and High and Ultra's
    /// parts (<see cref="SaucerFineMesh"/>), which must spend more triangles, stand where the shader's rings put their light and
    /// be shut solids wound clockwise seen from outside.
    /// </summary>
    public class SaucerMeshTests
    {
        private readonly ITestOutputHelper _output;

        public SaucerMeshTests(ITestOutputHelper output) => _output = output;

        private static (List<VertexPositionNormalTexture> Vertices, List<int> Indices) Part(SaucerFineMesh.Part part) =>
            SaucerFineMesh.Build(part).Geometry;

        private static IEnumerable<SaucerFineMesh.Part> Parts => Enum.GetValues<SaucerFineMesh.Part>();

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
            Assert.True(total > 4 * 2208, $"the fine hull has {total} triangles");
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
        /// The bulbs stand proud (#1173): every rim bulb reaches past the rim, and past its own socket, and they sit where
        /// <c>Saucer.fx</c>'s rings throw their halo on the hull (the rim's middle height, the top ring's radius).
        /// </summary>
        [Fact]
        public void TheBulbsStandProudWhereTheShaderLightsThem()
        {
            var (bulbs, _) = Part(SaucerFineMesh.Part.Bulbs);
            var (hull, _) = Part(SaucerFineMesh.Part.Hull);
            float hullReach = hull.Max(v => new Vector2(v.Position.X, v.Position.Z).Length());
            Assert.InRange(hullReach, SaucerMesh.RIM_RADIUS - 0.01f, SaucerMesh.RIM_RADIUS + 0.05f);

            float rimY = 0.5f * (SaucerMesh.RIM_BAND.X + SaucerMesh.RIM_BAND.Y);
            for (int i = 0; i < SaucerFineMesh.RIM_BULBS; i++)
            {
                Vector3[] bulb = bulbs.Where(v => v.TextureCoordinate.X == 0f && v.TextureCoordinate.Y == i).Select(v => v.Position).ToArray();
                Assert.NotEmpty(bulb);
                float reach = bulb.Max(p => new Vector2(p.X, p.Z).Length());
                Assert.True(reach > hullReach + 0.1f, $"rim bulb {i} reaches {reach}, the hull {hullReach}");
                Vector3 centre = new(bulb.Average(p => p.X), bulb.Average(p => p.Y), bulb.Average(p => p.Z));
                Assert.InRange(centre.Y, rimY - 0.01f, rimY + 0.01f);
                float bearing = MathF.Atan2(centre.Z, centre.X);
                Assert.InRange(MathHelper.WrapAngle(bearing - SaucerFineMesh.BulbBearing(i, SaucerFineMesh.RIM_BULBS)), -0.01f, 0.01f);
            }

            for (int i = 0; i < SaucerFineMesh.TOP_BULBS; i++)
            {
                Vector3[] bulb = bulbs.Where(v => v.TextureCoordinate.X == 1f && v.TextureCoordinate.Y == i).Select(v => v.Position).ToArray();
                Assert.NotEmpty(bulb);
                Vector3 centre = SaucerFineMesh.TopBulbCentre(i);
                Assert.InRange(new Vector2(centre.X, centre.Z).Length(), SaucerMesh.TOP_RING.X - 0.05f, SaucerMesh.TOP_RING.X + 0.05f);
                Assert.InRange(centre.Y, SaucerMesh.TOP_RING.Y - 0.06f, SaucerMesh.TOP_RING.Y + 0.12f);
                //Its top stands over the slope round it
                Assert.True(bulb.Max(p => p.Y) > SaucerFineMesh.TopY(SaucerMesh.TOP_RING.X) + 0.12f, $"top bulb {i} does not stand proud");
            }
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
    }
}
