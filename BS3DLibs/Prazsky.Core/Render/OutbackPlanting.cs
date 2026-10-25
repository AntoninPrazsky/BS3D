using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>What grows on the outback's plain</b> (#749). The owner: "the scene lacks detail, there is no vegetation at all";
    /// the plain had its spinifex painted in the pixel shader and nothing standing on it. The references
    /// (<c>C:\Users\panrd\AI\sd\out\749-zimage</c>, <c>749-klein</c>) all stand the same few things on red earth, and this
    /// plants them as <see cref="ScatterBucket"/>s for the outback's <see cref="PlantPass"/>, casting into the sun's map:
    /// <list type="bullet">
    /// <item><b>Spinifex hummocks</b>, the bulk: domes of stiff grey-green needles radiating from one root
    /// (<see cref="SpinifexMesh"/>), the painted spinifex's own plant standing up out of it.</item>
    /// <item><b>Saltbush</b>, silvery and low: the savanna's scrub dome at Low and Medium, at High and Ultra the meadow's
    /// shrub with round leaves (as the tropical beach's sea grape, #753).</item>
    /// <item><b>Dead timber</b>, bleached grey-white, lying about (<see cref="DeadwoodMesh"/>).</item>
    /// <item><b>Mulga and dead trees</b>, few and upright, most of them at a formation's foot, where the water runs off the
    /// rock (<see cref="AcaciaMesh"/>'s young and dead kinds in the outback's own colours): not the savanna's umbrella.</item>
    /// </list>
    /// Nothing stands on a formation's footprint or on the island's bank. Deterministic off one seed; the formations are
    /// <see cref="TerrainMirror.OutbackFootprints"/>, the very ellipses the plain stops on.
    /// </summary>
    public sealed class OutbackPlanting : IDisposable
    {
        private readonly List<IDisposable> _owned = new();

        /// <summary>Everything, in draw order.</summary>
        public ScatterBucket[] Buckets { get; }

        //How far from the arena things grow, and how many of each
        private const float NEAREST = ArenaIsland.RADIUS + 8f, FARTHEST = 320f;
        private const int SALTBUSH_COUNT = 300, DEADWOOD_COUNT = 110, TREES = 40;

        //The hummocks are dense where the lens can tell one from the next and sparse past it, where the painted spinifex
        //carries the plain: so many out to SPINIFEX_NEAR_TO, so many more on to FARTHEST
        private const int SPINIFEX_NEAR = 1700, SPINIFEX_FAR = 700;
        private const float SPINIFEX_NEAR_TO = 140f;

        //The share of the trees planted at a formation's foot, and how far out from its outline (a share of its short axis)
        private const float TREES_AT_ROCK = 0.65f;

        //The colours, linear: the hummocks' grey-green and their straw-dry tips, the saltbush's silver, the bleached
        //timber, the mulga's dark grey-green phyllodes and its near-black bark, and a dead tree's grey
        private static readonly Vector3 SPINIFEX_GREEN = new(0.18f, 0.19f, 0.10f), SPINIFEX_DRY = new(0.34f, 0.28f, 0.13f);
        private static readonly Vector3 SALTBUSH = new(0.13f, 0.16f, 0.13f), SALTBUSH_DRY = new(0.20f, 0.20f, 0.15f);
        //Weathered grey, not bone: at the first cut's 0.42 a log with its stubs read as a white toy aeroplane on the red earth
        private static readonly Vector3 TIMBER = new(0.22f, 0.20f, 0.18f), TIMBER_DRY = new(0.30f, 0.28f, 0.25f);
        private static readonly Vector3 MULGA = new(0.055f, 0.075f, 0.045f), MULGA_DRY = new(0.10f, 0.10f, 0.06f);
        private static readonly Vector3 MULGA_BARK = new(0.07f, 0.045f, 0.032f), DEAD_TREE = new(0.30f, 0.28f, 0.25f);

        /// <param name="height">The outback's ground (<see cref="TerrainMirror.Outback"/>).</param>
        /// <param name="footprints">The formations (<see cref="TerrainMirror.OutbackFootprints"/>): nothing stands on one, and
        /// the trees gather round their feet.</param>
        public OutbackPlanting(GraphicsDevice device, Func<float, float, float> height, IReadOnlyList<TerrainMirror.Footprint> footprints,
            int seed)
        {
            var buckets = new List<ScatterBucket>();
            Random rng = new(seed);

            bool OnRock(float x, float z, float margin)
            {
                var p = new Vector2(x, z);
                foreach (TerrainMirror.Footprint f in footprints)
                {
                    Vector2 d = p - f.Centre;
                    float u = Vector2.Dot(d, f.Axis) / (f.SemiLong + margin);
                    float v = (d.X * -f.Axis.Y + d.Y * f.Axis.X) / (f.SemiShort + margin);
                    if (u * u + v * v < 1f) return true;
                }
                return false;
            }

            //Seated on the lowest ground under its footprint, so no side hangs over the swell
            float Ground(float x, float z, float r) => MathF.Min(height(x, z), MathF.Min(MathF.Min(height(x + r, z), height(x - r, z)),
                MathF.Min(height(x, z + r), height(x, z - r))));

            (float X, float Z) Anywhere(float from = NEAREST, float to = FARTHEST)
            {
                float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                float d = MathF.Sqrt(MathHelper.Lerp(from * from, to * to, (float)rng.NextDouble()));
                return (MathF.Cos(a) * d, MathF.Sin(a) * d);
            }

            ModelInstance Planted(float x, float z, float size, float reach, float sink, float tilt)
            {
                float yaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                float lean = ((float)rng.NextDouble() - 0.5f) * tilt;
                Vector4 tint = new((float)rng.NextDouble(), ((float)rng.NextDouble() - 0.5f) * 0.2f, 0f, 0f);
                return new ModelInstance(Matrix.CreateScale(size) * Matrix.CreateRotationX(lean) * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(x, Ground(x, z, reach * size) - sink * size, z), tint);
            }

            void Scatter(List<ModelInstance>[] into, int count, float sizeMin, float sizeMax, float reach, float margin, float sink, float tilt,
                float from = NEAREST, float to = FARTHEST)
            {
                for (int i = 0, tries = 0; i < count && tries < count * 10; tries++)
                {
                    (float x, float z) = Anywhere(from, to);
                    float size = MathHelper.Lerp(sizeMin, sizeMax, (float)rng.NextDouble());
                    int variant = rng.Next(into.Length);
                    if (OnRock(x, z, margin + reach * size)) continue;
                    into[variant].Add(Planted(x, z, size, reach, sink, tilt));
                    i++;
                }
            }

            //--- The spinifex hummocks: the bulk, a metre or two across. ⚠ The pass draws every instance every frame, into the
            //scene and into the sun's map: at one mesh of 180 needles for all of them the first cut cost the plain 2.5 ms. So
            //the near band's hummocks have NEEDLES at High and Ultra, the far band's (a few pixels across) and Low and Medium's
            //FAR_NEEDLES, and every one casts as a CASTER_NEEDLES stand-in of the same dome, which the map's texels cannot tell
            //from the whole
            var spinifex = new IProceduralMesh[3];
            var spinifexFar = new IProceduralMesh[3];
            var spinifexCaster = new IProceduralMesh[3];
            var spinifexAt = Lists(spinifex.Length);
            var spinifexFarAt = Lists(spinifex.Length);
            for (int m = 0; m < spinifex.Length; m++)
            {
                float hummock = 0.55f + 0.1f * m;
                spinifex[m] = Own(SpinifexMesh(device, hummock, 7490 + m, NEEDLES));
                spinifexFar[m] = Own(SpinifexMesh(device, hummock, 7490 + m, FAR_NEEDLES));
                spinifexCaster[m] = Own(SpinifexMesh(device, hummock, 7490 + m, CASTER_NEEDLES));
            }
            Scatter(spinifexAt, SPINIFEX_NEAR, 0.7f, 1.6f, 1f, 1f, 0.06f, 0.15f, NEAREST, SPINIFEX_NEAR_TO);
            Scatter(spinifexFarAt, SPINIFEX_FAR, 0.9f, 1.8f, 1f, 1f, 0.06f, 0.15f, SPINIFEX_NEAR_TO, FARTHEST);
            for (int m = 0; m < spinifex.Length; m++)
            {
                //The near band at full needles where FineMeshes is on, and as the far band's hummock below it
                if (spinifexAt[m].Count > 0)
                {
                    buckets.Add(Own(new ScatterBucket(device, spinifex[m], spinifexAt[m], SPINIFEX_GREEN, SPINIFEX_DRY, dapple: 0.25f,
                        bark: 0f, detailOnly: false, fineMeshes: true, casterMesh: spinifexCaster[m])));
                    buckets.Add(Own(new ScatterBucket(device, spinifexFar[m], spinifexAt[m], SPINIFEX_GREEN, SPINIFEX_DRY, dapple: 0.25f,
                        bark: 0f, detailOnly: false, fineMeshes: false, casterMesh: spinifexCaster[m])));
                }
                if (spinifexFarAt[m].Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, spinifexFar[m], spinifexFarAt[m], SPINIFEX_GREEN, SPINIFEX_DRY, dapple: 0.25f,
                        bark: 0f, detailOnly: false, casterMesh: spinifexCaster[m])));
            }

            //--- The saltbush: the dome at Low and Medium and on the Potato path, stems and round leaves at High and Ultra, off
            //the same instances (the sea grape's build, #753)
            const int SALT_VARIANTS = 2;
            var domes = new FoliageMesh[SALT_VARIANTS];
            var fine = new MeadowShrubMesh[SALT_VARIANTS];
            var saltAt = Lists(SALT_VARIANTS);
            for (int m = 0; m < SALT_VARIANTS; m++)
            {
                float r = 0.75f + 0.15f * m, hh = r * 0.62f;
                domes[m] = Own(new FoliageMesh(device, r, hh, hh * 0.7f, 7495 + m, FoliageStyle.Scrub));
                (float reach, float top) = MeadowScatter.FineShrubSize(r, hh, hh * 0.7f, 7495 + m);
                fine[m] = Own(new MeadowShrubMesh(device, reach, top, 7600 + m, SALT_CARD, MeadowShrubMesh.SHRUB_COVER));
            }
            Scatter(saltAt, SALTBUSH_COUNT, 0.8f, 1.5f, 0.9f, 2f, 0.1f, 0f);
            for (int m = 0; m < SALT_VARIANTS; m++)
            {
                if (saltAt[m].Count == 0) continue;
                buckets.Add(Own(new ScatterBucket(device, domes[m], saltAt[m], SALTBUSH, SALTBUSH_DRY, dapple: 0.5f, bark: 0f,
                    detailOnly: false, fineMeshes: false)));
                buckets.Add(Own(new ScatterBucket(device, fine[m].Wood, saltAt[m], MULGA_BARK * 2f, MULGA_BARK * 2.3f, dapple: 0f,
                    bark: 0.35f, detailOnly: false, castsShadow: false, fineMeshes: true)));
                buckets.Add(Own(new ScatterBucket(device, fine[m].Leaves, saltAt[m], SALTBUSH, SALTBUSH_DRY, dapple: 0f, bark: 0f,
                    detailOnly: false, leaves: 3f, fineMeshes: true)));
            }

            //--- The dead timber: logs and limbs lying where they fell, bleached
            var timber = new DeadwoodMesh[3];
            var timberAt = Lists(timber.Length);
            for (int m = 0; m < timber.Length; m++) timber[m] = Own(new DeadwoodMesh(device, 2.4f + 1.2f * m, 0.14f + 0.04f * m, 7500 + m));
            Scatter(timberAt, DEADWOOD_COUNT, 0.7f, 1.4f, 1.8f, 1f, 0.15f, 0.12f);
            for (int m = 0; m < timber.Length; m++)
                if (timberAt[m].Count > 0)
                    buckets.Add(Own(new ScatterBucket(device, timber[m], timberAt[m], TIMBER, TIMBER_DRY, dapple: 0f, bark: 0.5f,
                        detailOnly: false, earthed: true)));

            //--- The trees: mulga (upright, dark, feathery) and dead ones, most of them at a formation's foot
            var trees = new List<AcaciaMesh>();
            for (int m = 0; m < 3; m++)
                trees.Add(Own(new AcaciaMesh(device, AcaciaKind.Young, 0.16f, 5.5f + 1.2f * m, 2.6f + 0.4f * m, 7510 + m)));
            for (int m = 0; m < 2; m++)
                trees.Add(Own(new AcaciaMesh(device, AcaciaKind.Dead, 0.2f, 6f + 1.5f * m, 3f, 7520 + m)));
            var treeAt = Lists(trees.Count);
            var near = new List<TerrainMirror.Footprint>();
            foreach (TerrainMirror.Footprint f in footprints)
                if (f.Centre.Length() < FARTHEST) near.Add(f);
            for (int i = 0, tries = 0; i < TREES && tries < TREES * 20; tries++)
            {
                float x, z;
                if (near.Count > 0 && rng.NextDouble() < TREES_AT_ROCK)
                {
                    TerrainMirror.Footprint f = near[rng.Next(near.Count)];
                    float a = (float)rng.NextDouble() * MathHelper.TwoPi;
                    float outward = 1.08f + 0.3f * (float)rng.NextDouble();
                    Vector2 side = new(-f.Axis.Y, f.Axis.X);
                    Vector2 p = f.Centre + f.Axis * (MathF.Cos(a) * f.SemiLong * outward) + side * (MathF.Sin(a) * f.SemiShort * outward);
                    (x, z) = (p.X, p.Y);
                }
                else (x, z) = Anywhere();
                float r = new Vector2(x, z).Length();
                int variant = rng.Next(trees.Count);
                float size = 0.85f + 0.35f * (float)rng.NextDouble();
                if (r < NEAREST + 6f || r > FARTHEST || OnRock(x, z, 3f)) continue;
                treeAt[variant].Add(Planted(x, z, size, 0.3f, 0.05f, 0.08f));
                i++;
            }
            for (int m = 0; m < trees.Count; m++)
            {
                if (treeAt[m].Count == 0) continue;
                AcaciaMesh tree = trees[m];
                bool dead = tree.Kind == AcaciaKind.Dead;
                if (tree.Canopy != null)
                {
                    //The plate for the Low tier, the leaf sprays for every other (the savanna's acacias, #610)
                    buckets.Add(Own(new ScatterBucket(device, tree.Canopy, treeAt[m], MULGA, MULGA_DRY, dapple: 0.6f, bark: 0f,
                        detailOnly: false, lowOnly: true)));
                    buckets.Add(Own(new ScatterBucket(device, tree.Leaves, treeAt[m], MULGA, MULGA_DRY, dapple: 0.35f, bark: 0f,
                        detailOnly: true, leaves: 1f)));
                }
                Vector3 bark = dead ? DEAD_TREE : MULGA_BARK;
                buckets.Add(Own(new ScatterBucket(device, tree.Wood, treeAt[m], bark, bark * 1.15f, dapple: 0f, bark: 0.6f,
                    detailOnly: false, earthed: true, fineMeshes: false)));
                buckets.Add(Own(new ScatterBucket(device, tree.FineWood, treeAt[m], bark, bark * 1.15f, dapple: 0f, bark: 0.6f,
                    detailOnly: false, earthed: true, fineMeshes: true)));
            }

            Buckets = buckets.ToArray();
        }

        //A saltbush's leaf card against its clump (the meadow's shrub is 0.55): small round leaves, a little bigger than a
        //hawthorn's, on a bush a metre across
        private const float SALT_CARD = 1.1f;

        //The hummock's needles: how many, how wide at the root (a share of the radius), how far out of the root they start
        private const int NEEDLES = 130, FAR_NEEDLES = 50, CASTER_NEEDLES = 24;
        private const float NEEDLE_WIDTH = 0.022f, NEEDLE_ROOT = 0.12f;

        /// <summary>
        /// One spinifex hummock (#749): <paramref name="needles"/> stiff needles from one root out to a dome a unit
        /// across and <paramref name="height"/> high, low ones nearly flat over the ground, high ones standing up, every needle
        /// a thin triangle emitted once for each face (wound clockwise seen from that face, CLAUDE.md's rule). Its normal is
        /// half the face's and half the dome's, so the hummock shades as a mound and each needle still reads by its face.
        /// </summary>
        internal static UploadedMesh SpinifexMesh(GraphicsDevice device, float height, int seed, int needles)
        {
            const float radius = 1f;
            Random rng = new(seed);
            var v = new List<VertexPositionNormalTexture>();
            var idx = new List<short>();

            for (int n = 0; n < needles; n++)
            {
                float azimuth = (float)rng.NextDouble() * MathHelper.TwoPi;
                //Elevation from the ground to near upright, more of them low: a hummock is wider than it is high
                float elevation = MathHelper.PiOver2 * 0.92f * (float)rng.NextDouble();
                float reach = 0.8f + 0.25f * (float)rng.NextDouble();
                Vector3 tip = new(MathF.Cos(elevation) * MathF.Cos(azimuth) * radius * reach, MathF.Sin(elevation) * height * reach + 0.03f,
                    MathF.Cos(elevation) * MathF.Sin(azimuth) * radius * reach);
                Vector3 root = new Vector3(tip.X, 0f, tip.Z) * NEEDLE_ROOT * (float)rng.NextDouble() + new Vector3(0f, 0.02f, 0f);

                Vector3 along = Vector3.Normalize(tip - root);
                float turn = (float)rng.NextDouble() * MathHelper.Pi;
                Vector3 across = Vector3.Normalize(Vector3.Cross(along, MathF.Abs(along.Y) < 0.95f ? Vector3.Up : Vector3.UnitX));
                Vector3 up2 = Vector3.Cross(across, along);
                Vector3 side = across * MathF.Cos(turn) + up2 * MathF.Sin(turn);
                float w = NEEDLE_WIDTH * radius * (0.7f + 0.6f * (float)rng.NextDouble());

                Vector3 a = root - side * w, b = root + side * w, c = tip;
                Vector3 face = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                Vector3 dome = Vector3.Normalize(new Vector3(tip.X / (radius * radius), tip.Y / (height * height), tip.Z / (radius * radius)));

                foreach (float s in new[] { 1f, -1f })
                {
                    Vector3 normal = Vector3.Normalize(face * s + dome);
                    short i0 = (short)v.Count;
                    v.Add(new VertexPositionNormalTexture(a, normal, Vector2.Zero));
                    v.Add(new VertexPositionNormalTexture(b, normal, Vector2.UnitX));
                    v.Add(new VertexPositionNormalTexture(c, normal, Vector2.UnitY));
                    //Seen from the face's side `s`, (b - a) x (c - a) must point away (the renderer's front face): it is `face`,
                    //so the side facing +face takes a, c, b and the other a, b, c
                    if (s > 0f) { idx.Add(i0); idx.Add((short)(i0 + 2)); idx.Add((short)(i0 + 1)); }
                    else { idx.Add(i0); idx.Add((short)(i0 + 1)); idx.Add((short)(i0 + 2)); }
                }
            }

            return new UploadedMesh(device, v, idx, new BoundingSphere(new Vector3(0f, height * 0.4f, 0f), radius * 1.1f));
        }

        private static List<ModelInstance>[] Lists(int count)
        {
            var lists = new List<ModelInstance>[count];
            for (int i = 0; i < count; i++) lists[i] = new List<ModelInstance>();
            return lists;
        }

        private T Own<T>(T item) where T : IDisposable
        {
            _owned.Add(item);
            return item;
        }

        public void Dispose()
        {
            foreach (IDisposable d in _owned) d.Dispose();
            _owned.Clear();
        }
    }
}
