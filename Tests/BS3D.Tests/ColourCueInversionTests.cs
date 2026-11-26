using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <b>No two colours wear each other's colour</b> (#1247). The owner mistook orange for brown on level 29: orange's
    /// figure is the orange darkened, a brown, and brown's was the brown brightened, an orange-brown, on a ground darkened
    /// nearly to black, so each ball was a two-tone of the other's colours and the angle it was seen from decided which
    /// dominated. Shading cannot be relied on to tell such a pair apart, since it moves a colour's lightness and keeps its
    /// hue and chroma. So a pair is refused when each colour's figure ink lies within <see cref="NEAR"/> dE00 of the other's
    /// ground and within <see cref="NEAR_HUE"/> of it at one lightness: each ball then carries the other's body as its own
    /// figure. Counted over the inks <see cref="ColourCues.Inks"/> gives, in CIELAB with linear 1 as white.
    /// <para>
    /// <b>Seen to fail</b> (<see cref="TheCheckFindsBrownsOldPaleInk"/>) on brown's paler ink as it was until #1247: orange
    /// and brown in all four sets.
    /// </para>
    /// </summary>
    public class ColourCueInversionTests
    {
        private const double NEAR = 25.0;
        private const double NEAR_HUE = 15.0;

        /// <summary>
        /// The one pair that sits on the line both ways and is left: blue against silver in the emblems and the spots, at
        /// 13.6-14.1 in hue and chroma. Silver on the darker ink would make black and silver one in every set (its figure a
        /// dark slate, black's a grey), and the two are 17 apart whole (#246).
        /// </summary>
        private static readonly (int Set, BallType A, BallType B)[] Accepted =
        {
            (2, BallType.Type3, BallType.Type11),
            (4, BallType.Type3, BallType.Type11),
        };

        public static TheoryData<int> Sets()
        {
            TheoryData<int> data = new();
            for (int set = 1; set <= ColourCues.SET_COUNT; set++) data.Add(set);
            return data;
        }

        [Theory]
        [MemberData(nameof(Sets))]
        public void NoTwoColoursWearEachOthersColour(int set)
        {
            BallType[] types = Enum.GetValues<BallType>();
            Dictionary<BallType, (Vector3 Figure, Vector3 Ground)> inks = types.ToDictionary(t => t, t => Inks(set, t));

            List<string> found = new();
            foreach (BallType a in types)
                foreach (BallType b in types.Where(other => other > a))
                {
                    bool inverted = Inverted(inks[a], inks[b], out string how);

                    //An accepted pair that no longer trips is a stale exception: it would hide the next one
                    if (Accepted.Contains((set, a, b))) Assert.True(inverted, $"set {set}: {a}/{b} is off the line now {how}; drop it from Accepted");
                    else if (inverted) found.Add($"{a}/{b} {how}");
                }

            Assert.True(found.Count == 0, $"set {set}: " + string.Join("; ", found));
        }

        /// <summary>The check fires on what it is meant to catch: brown's paler ink as it shipped until #1247, against orange.</summary>
        [Fact]
        public void TheCheckFindsBrownsOldPaleInk()
        {
            for (int set = 1; set <= ColourCues.SET_COUNT; set++)
            {
                Vector3 brown = Plain(BallType.Type10);
                float coverage = ColourCues.FigureOf(set, BallType.Type10).C.W;
                Vector3 figure = brown * 2.4f;
                float luminance = ColorSpace.Luminance(brown);
                float ground = (luminance - coverage * ColorSpace.Luminance(figure)) / ((1f - coverage) * luminance);

                Assert.True(Inverted(Inks(set, BallType.Type9), (figure, brown * MathF.Max(ground, ColourCues.GROUND_MIN)), out _),
                    $"set {set}: the old brown was not found against orange");
            }
        }

        /// <summary>The colour difference is the published one: three pairs of Sharma, Wu and Dalal's test data.</summary>
        [Theory]
        [InlineData(50.0, 2.6772, -79.7751, 50.0, 0.0, -82.7485, 2.0425)]
        [InlineData(50.0, 0.0, 0.0, 50.0, -1.0, 2.0, 2.3669)]
        [InlineData(50.0, 2.5, 0.0, 73.0, 25.0, -18.0, 27.1492)]
        public void TheColourDifferenceIsCiede2000(double l1, double a1, double b1, double l2, double a2, double b2, double expected) =>
            Assert.Equal(expected, DeltaE2000(new[] { l1, a1, b1 }, new[] { l2, a2, b2 }), 3);

        private static bool Inverted((Vector3 Figure, Vector3 Ground) a, (Vector3 Figure, Vector3 Ground) b, out string how)
        {
            double[] aFigure = Lab(a.Figure), aGround = Lab(a.Ground), bFigure = Lab(b.Figure), bGround = Lab(b.Ground);
            double one = DeltaE2000(aFigure, bGround), other = DeltaE2000(bFigure, aGround);
            double oneHue = HueAndChroma(aFigure, bGround), otherHue = HueAndChroma(bFigure, aGround);

            how = $"({one:0.0}/{oneHue:0.0}, {other:0.0}/{otherHue:0.0})";
            return one < NEAR && other < NEAR && oneHue < NEAR_HUE && otherHue < NEAR_HUE;
        }

        private static Vector3 Plain(BallType type) => ColorSpace.SrgbToLinear(BasicEffectParamsProvider.GetDiffuseTintByType(type));

        private static (Vector3, Vector3) Inks(int set, BallType type) =>
            ColourCues.Inks(type, Plain(type), ColourCues.FigureOf(set, type).C.W);

        //The two colours at one lightness, their mean: what shading cannot take away
        private static double HueAndChroma(double[] a, double[] b)
        {
            double mean = (a[0] + b[0]) / 2;
            return DeltaE2000(new[] { mean, a[1], a[2] }, new[] { mean, b[1], b[2] });
        }

        //Linear sRGB (D65) to CIELAB, a reflectance of 1 as white; a channel over 1 is clamped, as no surface reflects more
        private static double[] Lab(Vector3 linear)
        {
            double r = Math.Min(linear.X, 1f), g = Math.Min(linear.Y, 1f), b = Math.Min(linear.Z, 1f);
            double x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
            double y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            double z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
            double fx = F(x), fy = F(y), fz = F(z);
            return new[] { 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz) };

            static double F(double t) => t > 216.0 / 24389 ? Math.Cbrt(t) : t * 24389.0 / 27 / 116 + 16.0 / 116;
        }

        //CIEDE2000 (Sharma, Wu and Dalal's formulation)
        private static double DeltaE2000(double[] lab1, double[] lab2)
        {
            double l1 = lab1[0], a1 = lab1[1], b1 = lab1[2], l2 = lab2[0], a2 = lab2[1], b2 = lab2[2];
            double cBar = (Math.Sqrt(a1 * a1 + b1 * b1) + Math.Sqrt(a2 * a2 + b2 * b2)) / 2;
            double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cBar, 7) / (Math.Pow(cBar, 7) + Math.Pow(25, 7))));
            double a1p = (1 + g) * a1, a2p = (1 + g) * a2;
            double c1p = Math.Sqrt(a1p * a1p + b1 * b1), c2p = Math.Sqrt(a2p * a2p + b2 * b2);
            double h1p = Hue(b1, a1p), h2p = Hue(b2, a2p);

            double dLp = l2 - l1, dCp = c2p - c1p, dhp = h2p - h1p;
            if (c1p * c2p == 0) dhp = 0;
            else if (dhp > 180) dhp -= 360;
            else if (dhp < -180) dhp += 360;
            double dHp = 2 * Math.Sqrt(c1p * c2p) * Math.Sin(Radians(dhp / 2));

            double lBar = (l1 + l2) / 2, cBarP = (c1p + c2p) / 2;
            double hBarP = h1p + h2p;
            if (c1p * c2p != 0)
                hBarP = Math.Abs(h1p - h2p) <= 180 ? hBarP / 2 : (hBarP < 360 ? (hBarP + 360) / 2 : (hBarP - 360) / 2);

            double t = 1 - 0.17 * Math.Cos(Radians(hBarP - 30)) + 0.24 * Math.Cos(Radians(2 * hBarP))
                + 0.32 * Math.Cos(Radians(3 * hBarP + 6)) - 0.20 * Math.Cos(Radians(4 * hBarP - 63));
            double dTheta = 30 * Math.Exp(-Math.Pow((hBarP - 275) / 25, 2));
            double rc = 2 * Math.Sqrt(Math.Pow(cBarP, 7) / (Math.Pow(cBarP, 7) + Math.Pow(25, 7)));
            double sl = 1 + 0.015 * Math.Pow(lBar - 50, 2) / Math.Sqrt(20 + Math.Pow(lBar - 50, 2));
            double sc = 1 + 0.045 * cBarP, sh = 1 + 0.015 * cBarP * t;
            double rt = -Math.Sin(Radians(2 * dTheta)) * rc;

            return Math.Sqrt(Math.Pow(dLp / sl, 2) + Math.Pow(dCp / sc, 2) + Math.Pow(dHp / sh, 2) + rt * (dCp / sc) * (dHp / sh));

            static double Hue(double b, double a) => a == 0 && b == 0 ? 0 : (Math.Atan2(b, a) * 180 / Math.PI + 360) % 360;
            static double Radians(double degrees) => degrees * Math.PI / 180;
        }
    }
}
