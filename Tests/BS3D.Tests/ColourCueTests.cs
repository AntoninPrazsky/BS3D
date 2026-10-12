using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The colour cue prototypes (#737). Two promises are checkable without a device: the table gives every pair the
    /// prototypes exist for a figure that differs as well as a colour, and the shader file that holds the figures
    /// declares no uniform - which is what lets it sit in front of the three ball bodies without moving a single
    /// offset in the constant buffer, and so what keeps the shipped programs' code what it was.
    /// </summary>
    public class ColourCueTests
    {
        //The tight pairs the issue measured under wool: the dark trio, and the beige white against the yellow
        private static readonly BallType[][] TightGroups =
        {
            new[] { BallType.Type8, BallType.Type10, BallType.Type11 }, //black, brown, silver
            new[] { BallType.Type4, BallType.Type7 },                   //white (beige), yellow
        };

        [Fact]
        public void EveryTightPairDiffersInItsFigure()
        {
            foreach (BallType[] group in TightGroups)
                foreach (BallType a in group)
                    foreach (BallType b in group.Where(other => other != a))
                        Assert.True(ColourCues.SlotOf(a) != ColourCues.SlotOf(b),
                            $"{a} and {b} are one of the tight pairs and carry the same figure ({ColourCues.SlotOf(a)})");
        }

        [Fact]
        public void NoTwoColoursShareAFigureAndEverySlotIsTheShadersOwn()
        {
            int[] slots = Enum.GetValues<BallType>().Select(ColourCues.SlotOf).Where(slot => slot != ColourCues.NONE).ToArray();

            Assert.Equal(slots.Length, slots.Distinct().Count());
            Assert.All(slots, slot => Assert.InRange(slot, 1, ColourCues.SLOT_COUNT));
        }

        [Theory]
        [InlineData("none", ColourCueForm.None)]
        [InlineData("off", ColourCueForm.None)]
        [InlineData("motif", ColourCueForm.Motif)]
        [InlineData("A", ColourCueForm.Motif)]
        [InlineData("shape", ColourCueForm.Shape)]
        [InlineData("b", ColourCueForm.Shape)]
        public void TheArgumentsSpellingsParse(string text, ColourCueForm expected)
        {
            Assert.True(ColourCues.TryParseForm(text, out ColourCueForm form));
            Assert.Equal(expected, form);
            Assert.True(ColourCues.TryParseForm(ColourCues.Name(form), out ColourCueForm back));
            Assert.Equal(form, back);
        }

        [Fact]
        public void AnUnknownSpellingIsRefused() => Assert.False(ColourCues.TryParseForm("paint", out _));

        [Fact]
        public void TheFiguresFileDeclaresNoUniform()
        {
            string code = Regex.Replace(File.ReadAllText(FiguresFile()), @"//[^\n]*", string.Empty);

            //A declaration at the top level that is not a static constant: a type, a name, then ';', '=' or ':' (a
            //function's name is followed by '(' instead). Anything this finds is a uniform, and it would move every
            //uniform declared after it in InstancedModel.fx.
            MatchCollection uniforms = Regex.Matches(code,
                @"^(?:uniform\s+)?(?:float|int|uint|bool|half|float[1-4](?:x[1-4])?|texture\w*|sampler\w*)\s+\w+\s*(?:\[[^\]]*\]\s*)?[;=:]",
                RegexOptions.Multiline);

            Assert.True(uniforms.Count == 0,
                "BallColourCue.fxh declares " + string.Join(", ", uniforms.Select(m => m.Value.Trim())) + ": a uniform here moves the "
                + "constant buffer of every program after it; it belongs in BallColourCueTechniques.fxh, last in the effect");
        }

        private static string FiguresFile()
        {
            string relative = Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "InstancedModel", "BallColourCue.fxh");

            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
