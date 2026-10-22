using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// High and Ultra's rocks on the Moon and the volcano (#777, <see cref="FracturedRockMesh"/>), built with no device. What the
    /// plantings rely on: every rock is one closed skin wound clockwise from outside (CLAUDE.md, "Triangle winding"), its
    /// normals face the way its triangles do, a rock at two densities is one rock (the shadow casts the coarser one), and it
    /// stands in the frame of the coarse mesh it replaces, so the instance that seated the coarse one seats it.
    /// </summary>
    public class FracturedRockTests
    {
        public static TheoryData<int, int, float> Rocks()
        {
            var data = new TheoryData<int, int, float>();
            foreach (int seed in new[] { 8125 * 31 + 777, 8125 * 31 + 777 + 101 * 5, 6795 * 31 + 777 + 202 })
                for (int subdivisions = 1; subdivisions <= 4; subdivisions++)
                {
                    data.Add(seed, subdivisions, 0f);
                    data.Add(seed + 5000, subdivisions, 0.7f);
                }
            return data;
        }

        [Theory]
        [MemberData(nameof(Rocks))]
        public void EveryRockIsOneShutSkinWoundClockwiseFromOutside(int seed, int subdivisions, float boulderHeight)
        {
            (List<VertexPositionNormalTexture> v, List<short> idx) = FracturedRockMesh.Build(seed, subdivisions, boulderHeight);

            Assert.Empty(MeshOpenness.OpenEdges(v, idx));
            Assert.Equal(0, MeshOpenness.NonManifoldEdges(v, idx));

            //Clockwise from outside: (b - a) x (c - a) points inward, so the volume the triangles enclose comes out negative
            float volume = 0f;
            for (int t = 0; t < idx.Count; t += 3)
                volume += Vector3.Dot(v[idx[t]].Position, Vector3.Cross(v[idx[t + 1]].Position, v[idx[t + 2]].Position));
            Assert.True(volume < 0f, $"seed {seed}, {subdivisions} subdivisions: enclosed volume {volume} is not negative");
        }

        [Theory]
        [MemberData(nameof(Rocks))]
        public void TheNormalsFaceTheWayTheTrianglesDo(int seed, int subdivisions, float boulderHeight)
        {
            (List<VertexPositionNormalTexture> v, List<short> idx) = FracturedRockMesh.Build(seed, subdivisions, boulderHeight);
            int against = 0;
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector3 a = v[idx[t]].Position, b = v[idx[t + 1]].Position, c = v[idx[t + 2]].Position;
                Vector3 inward = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                for (int k = 0; k < 3; k++)
                    if (Vector3.Dot(inward, v[idx[t + k]].Normal) > 0.25f) against++;   //WindingCheck's AGREEMENT_COS
            }
            Assert.True(against == 0, $"seed {seed}, {subdivisions} subdivisions: {against} vertex normals point into their triangle");
        }

        [Theory]
        [InlineData(8125 * 31 + 777, 0f)]
        [InlineData(8125 * 31 + 777 + 5000, 0.85f)]
        public void ARockAtTwoDensitiesIsOneRock(int seed, float boulderHeight)
        {
            for (int coarse = 0; coarse < 4; coarse++)
            {
                (List<VertexPositionNormalTexture> few, _) = FracturedRockMesh.Build(seed, coarse, boulderHeight);
                (List<VertexPositionNormalTexture> many, _) = FracturedRockMesh.Build(seed, coarse + 1, boulderHeight);
                for (int i = 0; i < few.Count; i++)
                    Assert.True(Vector3.Distance(few[i].Position, many[i].Position) < 1e-4f,
                        $"{coarse} against {coarse + 1} subdivisions: point {i} moved {Vector3.Distance(few[i].Position, many[i].Position)}");
            }
        }

        [Theory]
        [InlineData(8125 * 31 + 777)]
        [InlineData(8125 * 31 + 777 + 101 * 3)]
        [InlineData(6795 * 31 + 777 + 101 * 4)]
        public void ABlockStandsInTheCoarseBlocksFrame(int seed)
        {
            //VolcanoPlanting.BlockMesh: corners 0.31 to 0.585 out from (0, 0.18, 0), squashed in Y; the plantings sink a
            //block 0.2 to 0.25 of its size, so it must reach under that line and not stand far outside the coarse one
            (List<VertexPositionNormalTexture> v, _) = FracturedRockMesh.Build(seed, 3);
            float minY = float.MaxValue, reach = 0f;
            foreach (VertexPositionNormalTexture p in v)
            {
                minY = MathF.Min(minY, p.Position.Y);
                reach = MathF.Max(reach, Vector3.Distance(p.Position, new Vector3(0f, 0.18f, 0f)));
            }
            Assert.InRange(minY, -0.4f, 0.1f);
            Assert.InRange(reach, 0.45f, 0.8f);
        }

        [Fact]
        public void ABoulderStandsOnTheLathesFootprint()
        {
            //RockMesh(radius 1, height h): its base on the origin, about a unit out at the ground, its crown at the height
            const float height = 0.7f;
            (List<VertexPositionNormalTexture> v, _) = FracturedRockMesh.Build(8125 * 31 + 777 + 5000, 3, height);
            float minY = float.MaxValue, maxY = float.MinValue, wide = 0f;
            foreach (VertexPositionNormalTexture p in v)
            {
                minY = MathF.Min(minY, p.Position.Y);
                maxY = MathF.Max(maxY, p.Position.Y);
                wide = MathF.Max(wide, new Vector2(p.Position.X, p.Position.Z).Length());
            }
            Assert.InRange(minY, -0.12f * height - 1e-3f, 0f);
            Assert.InRange(maxY, height - 1e-3f, height + 1e-3f);
            Assert.InRange(wide, 0.8f, 1.5f);
        }

        [Fact]
        public void ABiggerRockNeverTakesAFewerTriangles()
        {
            int last = 0;
            for (float reach = 0.05f; reach < 6f; reach += 0.05f)
            {
                int density = FracturedRockSet.Density(reach);
                Assert.InRange(density, last, FracturedRockSet.DENSITIES - 1);
                last = density;
            }
            Assert.Equal(FracturedRockSet.DENSITIES - 1, last);
        }
    }
}
