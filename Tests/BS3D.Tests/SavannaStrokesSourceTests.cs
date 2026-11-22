using BS3D.Effects;
using Prazsky.Core.Render;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;
using static BS3D.Tests.MeadowStrokesSourceTests;

namespace BS3D.Tests
{
    /// <summary>
    /// The savanna's blades seen from the side are the meadow's <c>GrassStrokes</c> (#1234, after #1190). They were the call
    /// #1190 took out of the meadow, <c>Fbm2Combed(xz * f, lens.xz - xz)</c>: a domain turned about the world origin by the
    /// pixel's bearing to the lens, so every stroke was a whole ray from the point under the lens, a fan fixed to the screen
    /// and re-rolled as the lens moved, in a chapter scene whose intro flies low over its grass. The construction itself
    /// (<c>Grass.fxh</c>) is held by <see cref="MeadowStrokesSourceTests"/>; this holds that the savanna uses it, the way
    /// it uses it, and that its strokes are wide enough to show in its own near field. A still frame cannot tell the two
    /// calls apart, so a tidy-up that put the old one back would pass every capture of a held camera.
    /// </summary>
    public class SavannaStrokesSourceTests
    {
        [Fact]
        public void TheSavannaCombsNoNoiseAlongTheBearingToTheLens()
        {
            string savanna = Code("Savanna.fx");

            foreach (string call in Calls(savanna, "Fbm2Combed"))
                Assert.DoesNotContain("CameraPosition", call);

            //One call, with the ground position, its bearing to the lens, the ground's normal and the unit view for the flank,
            //the savanna's own frequency and the two screen steps for the footprint across the grain
            string[] strokes = Calls(savanna, "GrassStrokes");
            Assert.Single(strokes);
            Assert.Matches(@"^GrassStrokes\(\s*worldPosition\.xz\s*,\s*CameraPosition\.xz\s*-\s*worldPosition\.xz\s*,\s*baseNormal\s*,"
                + @"\s*normalize\(\s*CameraPosition\s*-\s*worldPosition\s*\)\s*,\s*GrassReliefFrequency\s*\*\s*GRASS_STROKE_FREQUENCY\s*,"
                + @"\s*ddx\(\s*worldPosition\.xz\s*\)\s*,\s*ddy\(\s*worldPosition\.xz\s*\)\s*\)$", strokes[0]);
        }

        [Fact]
        public void OnlyTheDetailProgramDrawsTheStrokesAndTheyStayOffTheWornGround()
        {
            string field = Body(Code("Savanna.fx"), "float4 SavannaField(");

            //Worked out in the detail program alone (SavannaReduced, the Low tier and the far ring, gives them up), and what
            //is worked out is what the grass reads, faded by tipCover over the trails and the char as the tips are
            Assert.Matches(@"float\s+strokes\s*=\s*0\.0\s*;\s*if\s*\(\s*detail\s*\)\s*\{\s*strokes\s*=\s*GrassStrokes\(", field);
            Assert.Equal(2, Regex.Matches(field, @"\bstrokes\s*=[^=]").Count);
            Assert.Matches(@"grass\s*\*=\s*1\.0\s*\+\s*0\.85\s*\*\s*GrassTipStrength\s*\*\s*strokes\s*\*\s*tipCover\s*;", field);
            Assert.Matches(@"saturate\(\s*strokes\s*\*\s*2\.0\s*\)\s*\*\s*tipCover", field);
        }

        [Fact]
        public void TheStrokesShowInTheGrassHomesNearField()
        {
            //The intro's grass home (SavannaLanding) flies up to ABOVE_MAX over the grass, looking ahead, so the nearest ground
            //in its frame is that height over the tangent of half the field of view out. On a 1080-row window supersampled as
            //High and Ultra draw the scene, GrassStrokes' footprint there (a step across the grain, the along step divided by
            //the stretch, grown by the bearing's spin) must leave the coarser octave at least half its strength: a frequency
            //the meadow's width (96 a unit) leaves the savanna's low shots with almost no strokes at all. Medium and the phones
            //draw the scene unsupersampled, where the same footprint is twice as wide and the nearest strokes fainter, and this
            //does not hold them: docs/scenes/savanna.md has what they keep there, measured.
            string grass = Code("Grass.fxh");
            double stretch = Constant(grass, "GRASS_STROKE_STRETCH");
            Match spin = Regex.Match(Body(grass, "float GrassStrokes("), @"\(\s*1\.0\s*\+\s*([0-9.]+)\s*\*\s*spacing\s*/\s*max\(\s*lensDistance");
            Assert.True(spin.Success, "GrassStrokes' footprint no longer grows by k * spacing / lensDistance");
            double reach = double.Parse(spin.Groups[1].Value, CultureInfo.InvariantCulture);

            double frequency = Constant(Code("Savanna.fx"), "GRASS_STROKE_FREQUENCY") * new SavannaSceneConfig().GrassReliefFrequency;
            double spacing = stretch / frequency;

            double height = SavannaLanding.ABOVE_MAX;
            double halfFov = MeadowIntroShotsTests.INTRO_FIELD_OF_VIEW / 2.0;
            double ahead = height / Math.Tan(halfFov);
            int rows = 1080 * Math.Min(QualityPreset.Presets[(int)QualityLevel.High].SupersampleFactor,
                QualityPreset.Presets[(int)QualityLevel.Ultra].SupersampleFactor);
            double pixel = 2.0 * Math.Tan(halfFov) / rows;

            //At the bottom of the frame, looking level: across the grain one pixel is the depth (here the distance ahead) times
            //a pixel; along it the step is foreshortened by the grazing view, and divided by the stretch as the domain is
            double across = ahead * pixel;
            double along = Math.Sqrt(ahead * ahead + height * height) * pixel / Math.Sin(halfFov) / stretch;
            double footprint = Math.Max(across, along) * frequency * (1.0 + reach * spacing / ahead);
            double strength = Math.Clamp(1.0 - 2.0 * footprint, 0.0, 1.0);

            Assert.True(strength >= 0.5,
                $"At {frequency:0} strokes a unit the grass home's nearest strokes, {ahead:0.0} units out, keep {strength:0.00} of their strength: too fine for the savanna's low shots (#1234)");
        }
    }
}
