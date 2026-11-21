using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The dream's solids live in a shader, <c>Dream.fx</c>, and the host keeps a copy of where they stand
    /// (<see cref="SceneRenderer.DreamSolidCenter"/>, which the chapter intro lays its lenses against, #559) and of how
    /// many there are (the intro's loops and the Potato path's low-poly bodies, #829). #1126 changed their count, their
    /// orbit and the bound their march is gated on, and the two sides agree only by hand: a count, an orbit or a bound
    /// changed on one side alone aims the intro at empty sky, lets a lens into a solid, or gives the Pi and the phones
    /// a different sky. No test runs the shader, so these read its source: its numbers against the host's, its placement
    /// and its radius evaluated from the figures in its source beside the host's copies, and the bound against the
    /// farthest of the forms the distance field melts between.
    /// </summary>
    public class DreamSolidsSourceTests
    {
        private static readonly string Source = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Dream.fx"));

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The text of the effect without its comments, so a word in a note is not taken for code
        private static string Code() => Regex.Replace(File.ReadAllText(Source), @"//[^\n]*", string.Empty);

        private static float Define(string name)
        {
            Match match = Regex.Match(Code(), $@"#define\s+{name}\s+([0-9.]+)");
            Assert.True(match.Success, $"Dream.fx defines no {name}");
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Fact]
        public void TheHostDrawsAsManySolidsAsTheShader()
        {
            int shader = (int)Define("SHAPE_COUNT");
            Assert.Equal(SceneRenderer.DREAM_SOLID_COUNT, shader);
            Assert.Equal(PotatoBackdrop.DREAM_SOLIDS, shader);
        }

        [Fact]
        public void TheHostOrbitsAndBoundsTheSolidsAsTheShaderDoes()
        {
            Assert.Equal(DreamBackdrop.SOLID_INNER_EDGE, Define("SHAPE_INNER_EDGE"), 5);
            Assert.Equal(DreamBackdrop.SOLID_RING_GAP, Define("SHAPE_RING_GAP"), 5);
            Assert.Equal(DreamBackdrop.SOLID_RATE_INNER, Define("SHAPE_RATE_INNER"), 5);
            Assert.Equal(DreamBackdrop.SOLID_RATE_OUTER, Define("SHAPE_RATE_OUTER"), 5);
            Assert.Equal(DreamBackdrop.SOLID_SLOT_JITTER, Define("SHAPE_SLOT_JITTER"), 5);
            Assert.Equal(DreamBackdrop.SOLID_BOUND_IN_RADII, Define("SHAPE_BOUND_IN_RADII"), 5);

            //And the shader gates the march where the host does
            Assert.Matches(@"float\s+bound\s*=\s*ShapeRadius\(\s*fi\s*\)\s*\*\s*SHAPE_BOUND_IN_RADII\s*;", Code());
        }

        //A number in the effect's source, captured
        private const string N = @"([0-9]+(?:\.[0-9]+)?)";

        private static float[] Numbers(Match match)
        {
            var numbers = new float[match.Groups.Count - 1];
            for (int g = 1; g < match.Groups.Count; g++)
                numbers[g - 1] = float.Parse(match.Groups[g].Value, CultureInfo.InvariantCulture);
            return numbers;
        }

        private static string Function(string signature)
        {
            Match match = Regex.Match(Code(), signature + @"\s*\{(?<body>[^}]*)\}");
            Assert.True(match.Success, $"Dream.fx has no {signature}");
            return match.Groups["body"].Value;
        }

        private static float Frac(float x) => x - MathF.Floor(x);

        [Fact]
        public void TheHostPlacesEverySolidWhereTheShaderDoes()
        {
            //ShapeCenter's own formula and the ring and orbit it is built on (#1219), their figures read off the source and
            //evaluated here beside the host's copy: a ring, an orbit, an angle or a height term changed on one side alone
            //moves the solids the intro lays its lenses against, and DreamSolidsApartTests keeps apart only the host's copy
            Match ring = Regex.Match(Function(@"bool\s+ShapeInner\(\s*float\s+i\s*\)"), $@"return\s+frac\(\s*i\s*\*\s*{N}\s*\)\s*>\s*{N}\s*;");
            Assert.True(ring.Success, "ShapeInner is no longer the ring this test reads");

            string orbitBody = Function(@"float\s+ShapeOrbit\(\s*float\s+i\s*\)");
            Match outer = Regex.Match(orbitBody, $@"if\s*\(\s*!\s*ShapeInner\(\s*i\s*\)\s*\)\s*edge\s*\+=\s*ShapeSize\s*\*\s*\(\s*{N}\s*\*\s*SHAPE_BOUND_IN_RADII\s*\+\s*SHAPE_RING_GAP\s*\)\s*;");
            Assert.True(outer.Success, "ShapeOrbit no longer sets the outer ring out as this test reads");
            Assert.Matches(@"float\s+edge\s*=\s*ShapeOrbitRadius\s*\*\s*SHAPE_INNER_EDGE\s*;", orbitBody);
            Assert.Matches(@"return\s+edge\s*\+\s*ShapeRadius\(\s*i\s*\)\s*\*\s*SHAPE_BOUND_IN_RADII\s*;", orbitBody);

            string body = Function(@"float3\s+ShapeCenter\(\s*float\s+i\s*,\s*float\s+t\s*\)");
            Match place = Regex.Match(body, $@"float\s+place\s*=\s*{N}\s*\*\s*PI\s*/\s*\(\s*SHAPE_COUNT\s*/\s*{N}\s*\)\s*;");
            Match angle = Regex.Match(body, $@"float\s+a\s*=\s*t\s*\*\s*\(\s*inner\s*\?\s*SHAPE_RATE_INNER\s*:\s*SHAPE_RATE_OUTER\s*\)\s*\+\s*floor\(\s*i\s*\*\s*{N}\s*\)\s*\*\s*place"
                + $@"\s*\+\s*\(\s*inner\s*\?\s*place\s*\*\s*{N}\s*:\s*{N}\s*\)\s*\+\s*SHAPE_SLOT_JITTER\s*\*\s*sin\(\s*i\s*\*\s*{N}\s*\)\s*;");
            Match height = Regex.Match(body, $@"float\s+y\s*=\s*{N}\s*\+\s*{N}\s*\*\s*sin\(\s*t\s*\*\s*{N}\s*\+\s*i\s*\*\s*{N}\s*\)\s*;");
            Assert.True(place.Success && angle.Success && height.Success, "ShapeCenter is no longer the places, angle and height this test reads");
            Assert.Matches(@"bool\s+inner\s*=\s*ShapeInner\(\s*i\s*\)\s*;", body);
            Assert.Matches(@"float\s+r\s*=\s*ShapeOrbit\(\s*i\s*\)\s*;", body);
            Assert.Matches(@"return\s+float3\(\s*cos\(\s*a\s*\)\s*\*\s*r\s*,\s*y\s*,\s*sin\(\s*a\s*\)\s*\*\s*r\s*\)\s*;", body);

            Match radiusHash = Regex.Match(Function(@"float\s+ShapeRadius\(\s*float\s+i\s*\)"),
                $@"return\s+ShapeSize\s*\*\s*\(\s*{N}\s*\+\s*{N}\s*\*\s*sin\(\s*i\s*\*\s*{N}\s*\)\s*\)\s*;");
            Assert.True(radiusHash.Success, "ShapeRadius is no longer the hash this test reads");

            float[] k = Numbers(ring), o = Numbers(outer), p = Numbers(place), a = Numbers(angle), y = Numbers(height), s = Numbers(radiusHash);
            float count = Define("SHAPE_COUNT"), innerEdge = Define("SHAPE_INNER_EDGE"), gap = Define("SHAPE_RING_GAP"), boundInRadii = Define("SHAPE_BOUND_IN_RADII");
            float rateInner = Define("SHAPE_RATE_INNER"), rateOuter = Define("SHAPE_RATE_OUTER"), jitter = Define("SHAPE_SLOT_JITTER");
            var shapes = new DreamShapesConfig();

            for (int index = 0; index < SceneRenderer.DREAM_SOLID_COUNT; index++)
                foreach (float t in new[] { 0f, 41.3f, 613.7f, 3599.1f })
                {
                    float i = index;
                    bool inner = Frac(i * k[0]) > k[1];
                    Assert.Equal(DreamBackdrop.SolidInner(index), inner);

                    float edge = shapes.OrbitRadius * innerEdge + (inner ? 0f : shapes.Size * (o[0] * boundInRadii + gap));
                    float radius = edge + shapes.Size * (s[0] + s[1] * MathF.Sin(i * s[2])) * boundInRadii;
                    float slot = p[0] * MathF.PI / MathF.Floor(count / p[1]);
                    float turn = t * (inner ? rateInner : rateOuter) + MathF.Floor(i * a[0]) * slot + (inner ? slot * a[1] : a[2]) + jitter * MathF.Sin(i * a[3]);
                    var shader = new Vector3(MathF.Cos(turn) * radius, y[0] + y[1] * MathF.Sin(t * y[2] + i * y[3]), MathF.Sin(turn) * radius);

                    Vector3 host = DreamBackdrop.SolidCenter(shapes, index, t, out _);
                    Assert.True(Vector3.Distance(shader, host) < 0.01f, $"solid {index} at {t} s: the shader puts it at {shader}, the host at {host}");
                }
        }

        [Fact]
        public void TheApartTestMeltsTheFormsTheShaderDoes()
        {
            //DreamSolidsApartTests ports the distance field to look for a point inside two solids; its forms are the shader's
            Assert.Equal(DreamSolidsApartTests.BOX_HALF, Define("SHAPE_BOX_HALF"), 5);
            Assert.Equal(DreamSolidsApartTests.BOX_ROUND, Define("SHAPE_BOX_ROUND"), 5);
            Assert.Equal(DreamSolidsApartTests.TORUS_MAJOR, Define("SHAPE_TORUS_MAJOR"), 5);
            Assert.Equal(DreamSolidsApartTests.TORUS_MINOR, Define("SHAPE_TORUS_MINOR"), 5);
        }

        [Fact]
        public void TheHostSizesEverySolidAsTheShaderDoes()
        {
            //ShapeRadius's hash, read off the source: the host's bound is this radius times SHAPE_BOUND_IN_RADII
            Match match = Regex.Match(Function(@"float\s+ShapeRadius\(\s*float\s+i\s*\)"),
                $@"return\s+ShapeSize\s*\*\s*\(\s*{N}\s*\+\s*{N}\s*\*\s*sin\(\s*i\s*\*\s*{N}\s*\)\s*\)\s*;");
            Assert.True(match.Success, "ShapeRadius is no longer the hash this test reads");
            float[] k = Numbers(match);
            var shapes = new DreamShapesConfig();

            for (int index = 0; index < SceneRenderer.DREAM_SOLID_COUNT; index++)
            {
                float shader = shapes.Size * (k[0] + k[1] * MathF.Sin(index * k[2]));
                Assert.Equal(shader, DreamBackdrop.SolidRadius(shapes, index), 3);

                DreamBackdrop.SolidCenter(shapes, index, 0f, out float bound);
                Assert.Equal(shader * Define("SHAPE_BOUND_IN_RADII"), bound, 3);
            }
        }

        [Fact]
        public void TheBoundHoldsEveryFormTheSolidMeltsBetween()
        {
            //In the solid's own radius: the sphere reaches 1, the rounded box its corner, the torus its outer rim. A melt is a
            //lerp of two of these distance fields, negative only where one of them is, so it never reaches past the farthest.
            float box = MathF.Sqrt(3f) * Define("SHAPE_BOX_HALF") + Define("SHAPE_BOX_ROUND");
            float torus = Define("SHAPE_TORUS_MAJOR") + Define("SHAPE_TORUS_MINOR");
            float farthest = MathF.Max(1f, MathF.Max(box, torus));

            Assert.True(DreamBackdrop.SOLID_BOUND_IN_RADII > farthest,
                $"the bound, {DreamBackdrop.SOLID_BOUND_IN_RADII} of a solid's radius, cuts its farthest form at {farthest:F3}: the march would clip it");

            //And the distance field is built of them, not of literals a change to the defines would miss
            string code = Code();
            Assert.Matches(@"abs\(q\)\s*-\s*size\s*\*\s*SHAPE_BOX_HALF", code);
            Assert.Matches(@"-\s*size\s*\*\s*SHAPE_BOX_ROUND", code);
            Assert.Matches(@"length\(q\.xz\)\s*-\s*size\s*\*\s*SHAPE_TORUS_MAJOR", code);
            Assert.Matches(@"length\(tor\)\s*-\s*size\s*\*\s*SHAPE_TORUS_MINOR", code);
        }
    }
}
