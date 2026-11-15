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
    /// a different sky. No test runs the shader, so these read its source: its numbers against the host's, and the
    /// bound against the farthest of the forms the distance field melts between.
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
            Assert.Equal(DreamBackdrop.SOLID_ORBIT_BASE, Define("SHAPE_ORBIT_BASE"), 5);
            Assert.Equal(DreamBackdrop.SOLID_ORBIT_SWING, Define("SHAPE_ORBIT_SWING"), 5);
            Assert.Equal(DreamBackdrop.SOLID_BOUND_IN_RADII, Define("SHAPE_BOUND_IN_RADII"), 5);

            //And the shader uses them where the host does: the orbit's radius and the march's gate
            string code = Code();
            Assert.Matches(@"ShapeOrbitRadius\s*\*\s*\(\s*SHAPE_ORBIT_BASE\s*\+\s*SHAPE_ORBIT_SWING\s*\*\s*sin\(\s*i\s*\*\s*5\.3\s*\)\s*\)", code);
            Assert.Matches(@"float\s+bound\s*=\s*ShapeRadius\(\s*fi\s*\)\s*\*\s*SHAPE_BOUND_IN_RADII\s*;", code);
            Assert.Matches(@"return\s+ShapeSize\s*\*\s*\(\s*0\.7\s*\+\s*0\.3\s*\*\s*sin\(\s*i\s*\*\s*9\.1\s*\)\s*\)\s*;", code);
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
