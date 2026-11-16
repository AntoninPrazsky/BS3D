using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;
using Xunit.Abstractions;

namespace BS3D.Tests
{
    /// <summary>
    /// The air over the desert's pyramids (#1111): up to <c>Pyramids.SandTop</c> a face takes the dunes' own haze, so the stone
    /// and the sand round its foot are one colour wherever they meet; over it the dust thins and the group keeps its contrast.
    /// Both halves rest on that one height, and it is held here on the shipped config without a device.
    /// </summary>
    public class PyramidAirTests
    {
        private readonly ITestOutputHelper _output;

        public PyramidAirTests(ITestOutputHelper output) => _output = output;

        /// <summary>No sand anywhere under a footprint, read about eight times as finely as <c>SandTop</c> reads it, stands over
        /// it: wherever the sand meets a face, the face is still in the dunes' air.</summary>
        [Fact]
        public void NoSandUnderAnyFootprintStandsOverTheSandTop()
        {
            var config = new DesertSceneConfig();
            float top = Pyramids.SandTop(config);
            float turn = Pyramids.PlanTurn + MathHelper.PiOver4;
            var u = new Vector2(MathF.Cos(turn), MathF.Sin(turn));
            var v = new Vector2(-u.Y, u.X);
            const float FINE = 1.25f;

            float highest = float.MinValue;
            foreach (var (centre, side, _, _) in Pyramids.Placement(config))
            {
                var c = new Vector2(centre.X, centre.Z);
                int n = (int)MathF.Ceiling(side / FINE) + 1;
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        Vector2 at = c + (u * (i / (n - 1f) - 0.5f) + v * (j / (n - 1f) - 0.5f)) * side;
                        highest = MathF.Max(highest, TerrainMirror.Desert(at.X, at.Y, config));
                    }
                }
            }

            _output.WriteLine($"sand top {top:F2}, highest sand under a footprint read finely {highest:F2}");
            Assert.True(highest <= top, $"sand at {highest:F2} stands over the sand top {top:F2}");
        }

        /// <summary>Khufu, Khafre and Menkaure, the group the eye reads, each rise most of their height over the sand top, so
        /// the dust takes their foot and not the pyramid: one mostly under it would take the dunes' air from foot to apex and
        /// dissolve into the murk. (The queens are let go into the haze with the ground round them.)</summary>
        [Fact]
        public void TheThreeGreatPyramidsRiseMostOfTheirHeightOverTheSandTop()
        {
            var config = new DesertSceneConfig();
            float top = Pyramids.SandTop(config);
            foreach (var (centre, _, height, _) in Pyramids.Placement(config).Take(3))
            {
                float over = centre.Y + height - top;
                _output.WriteLine($"apex {centre.Y + height:F1} (base {centre.Y:F1}, height {height:F1}): {over:F1} over the sand top");
                Assert.True(over >= 0.7f * height, $"a pyramid {height:F1} tall rises only {over:F1} over the sand top {top:F2}");
            }
        }
    }
}
