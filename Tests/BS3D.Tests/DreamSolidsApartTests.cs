using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The dream's glass solids never pass through one another (#1219, the owner: "the 3D flying objects sometimes clip
    /// through each other. That should not happen"). The orbits had nothing keeping them apart: twelve solids of up to 22
    /// units, each on its own rate round the same band, met for 83 % of the clock, and the march shades only the nearest
    /// hit, so a meeting drew a hard crease and the solid behind vanished inside the other. They fly in two rings now, the
    /// rings apart by more than any two bounds and each ring turning as one (<see cref="DreamBackdrop.SolidCenter"/>, the
    /// host's copy of <c>Dream.fx</c>'s <c>ShapeCenter</c>, which <see cref="DreamSolidsSourceTests"/> holds to the
    /// shader). Held here over an hour of the clock at the frame steps of a slow game: no two bounds meet, and wherever
    /// two might, the shapes' own distance fields (the shader's, ported below) are sampled for a point inside both.
    /// </summary>
    public class DreamSolidsApartTests
    {
        private readonly ITestOutputHelper _output;

        public DreamSolidsApartTests(ITestOutputHelper output) => _output = output;

        //An hour of the scene's clock at a twentieth of a second: the solids move a few units a second at most
        private const float HOUR = 3600f, STEP = 0.05f;

        //The least room the bounds keep between them, in world units: the rings are SOLID_RING_GAP x Size (2.2) apart, and
        //the two nearest of a ring 17.9 (measured over the hour)
        private const float LEAST_GAP = 2f;

        //The grid a pair of overlapping bounds is searched on for a point inside both shapes, in world units: the issue's
        //measurement found the old laws' worst meeting 12 units deep at this spacing
        private const float PROBE_SPACING = 1.6f;

        [Fact]
        public void NoTwoSolidsBoundsEverMeet()
        {
            var shapes = new DreamShapesConfig();
            int n = SceneRenderer.DREAM_SOLID_COUNT;
            var centre = new Vector3[n];
            var bound = new float[n];
            float least = float.MaxValue, leastAt = 0f;
            int leastA = -1, leastB = -1;

            for (int k = 0; k * STEP <= HOUR; k++)
            {
                float t = k * STEP;
                for (int i = 0; i < n; i++) centre[i] = DreamBackdrop.SolidCenter(shapes, i, t, out bound[i]);

                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        float gap = Vector3.Distance(centre[i], centre[j]) - bound[i] - bound[j];
                        if (gap < least) (least, leastAt, leastA, leastB) = (gap, t, i, j);
                    }
            }

            _output.WriteLine($"over {HOUR} s at {STEP} s: the nearest two bounds, solids {leastA} and {leastB}, come {least:F2} apart at {leastAt:F2} s");
            Assert.True(least >= LEAST_GAP, $"solids {leastA} and {leastB}'s bounds come {least:F2} apart at {leastAt:F2} s (the least allowed is {LEAST_GAP})");
        }

        [Fact]
        public void NoTwoSolidsShapesEverInterpenetrate()
        {
            //The bounds hold every form (DreamSolidsSourceTests), so bounds apart are shapes apart. This asks the shapes
            //themselves, so that a law letting two bounds overlap (as the old one did for most of the clock) is judged by
            //what is drawn: wherever two bounds overlap, the lens they share is searched for a point inside both solids
            var shapes = new DreamShapesConfig();
            int n = SceneRenderer.DREAM_SOLID_COUNT;
            var centre = new Vector3[n];
            var bound = new float[n];
            int overlaps = 0;

            for (int k = 0; k * STEP <= HOUR; k++)
            {
                float t = k * STEP;
                for (int i = 0; i < n; i++) centre[i] = DreamBackdrop.SolidCenter(shapes, i, t, out bound[i]);

                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        if (Vector3.Distance(centre[i], centre[j]) >= bound[i] + bound[j]) continue;
                        overlaps++;

                        float depth = DeepestShared(shapes, i, j, centre[i], bound[i], centre[j], bound[j], t);
                        Assert.True(depth >= 0f, $"solids {i} and {j} pass through each other at {t:F2} s: a point lies {-depth:F1} inside both");
                    }
            }

            _output.WriteLine($"over {HOUR} s at {STEP} s: {overlaps} pair-steps with overlapping bounds searched, none with a point inside both shapes");
        }

        //The least of max(sdf a, sdf b) over a grid filling the box round the two bounds' shared lens: negative where a point
        //is inside both solids
        private static float DeepestShared(DreamShapesConfig shapes, int a, int b, Vector3 ca, float ra, Vector3 cb, float rb, float t)
        {
            Vector3 lo = Vector3.Max(ca - new Vector3(ra), cb - new Vector3(rb));
            Vector3 hi = Vector3.Min(ca + new Vector3(ra), cb + new Vector3(rb));
            float deepest = float.MaxValue;

            for (float x = lo.X; x <= hi.X; x += PROBE_SPACING)
                for (float y = lo.Y; y <= hi.Y; y += PROBE_SPACING)
                    for (float z = lo.Z; z <= hi.Z; z += PROBE_SPACING)
                    {
                        var p = new Vector3(x, y, z);
                        if (Vector3.DistanceSquared(p, ca) > ra * ra || Vector3.DistanceSquared(p, cb) > rb * rb) continue;

                        float inside = MathF.Max(ShapeSdf(shapes, p, a, ca, t), ShapeSdf(shapes, p, b, cb, t));
                        if (inside < deepest) deepest = inside;
                        if (deepest < 0f) return deepest;
                    }

            return deepest;
        }

        //Dream.fx's ShapeSdf, the tumble, the melt and the three forms, about a centre already placed. The forms' figures are
        //the shader's defines (DreamSolidsSourceTests holds them to the source); the tumble only turns a form about its
        //centre, so a drift in its rates moves no shape out of its bound
        internal const float BOX_HALF = 0.72f, BOX_ROUND = 0.14f, TORUS_MAJOR = 0.78f, TORUS_MINOR = 0.30f;

        internal static float ShapeSdf(DreamShapesConfig shapes, Vector3 p, int index, Vector3 centre, float t)
        {
            float i = index;
            Vector3 q = p - centre;

            float ya = t * (0.11f + 0.05f * Frac(i * 0.73f)) + i;
            float ca = MathF.Cos(ya), sa = MathF.Sin(ya);
            (q.X, q.Z) = (q.X * ca - q.Z * sa, q.X * sa + q.Z * ca);
            float xa = t * 0.07f + i * 1.7f;
            float cb = MathF.Cos(xa), sb = MathF.Sin(xa);
            (q.Y, q.Z) = (q.Y * cb - q.Z * sb, q.Y * sb + q.Z * cb);

            float size = DreamBackdrop.SolidRadius(shapes, index);
            float m = 0.5f + 0.5f * MathF.Sin(t * shapes.MorphSpeed + i * 11.3f);
            float s = Math.Clamp((m - 0.12f) / 0.76f, 0f, 1f);
            float blend = s * s * (3f - 2f * s);

            float sphere = q.Length() - size;

            Vector3 box = new Vector3(MathF.Abs(q.X), MathF.Abs(q.Y), MathF.Abs(q.Z)) - new Vector3(size * BOX_HALF);
            float boxD = Vector3.Max(box, Vector3.Zero).Length() + MathF.Min(MathF.Max(box.X, MathF.Max(box.Y, box.Z)), 0f) - size * BOX_ROUND;

            var tor = new Vector2(new Vector2(q.X, q.Z).Length() - size * TORUS_MAJOR, q.Y);
            float torus = tor.Length() - size * TORUS_MINOR;

            return Frac(i * 0.381f) < 0.5f ? MathHelper.Lerp(sphere, boxD, blend) : MathHelper.Lerp(boxD, torus, blend);
        }

        private static float Frac(float x) => x - MathF.Floor(x);
    }
}
