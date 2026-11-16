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
    /// and the sand round its foot are one colour wherever they meet; over it the dust thins in <c>Pyramids.DustThin</c> and
    /// the group keeps its contrast. Both halves rest on those two figures, held here on the shipped config without a device.
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

        //The most of the dunes' air the dust may lay on the apex of each great pyramid, and on Khufu and Khafre from
        //mid-height up: the two that fill the play view stand clear of the haze over their upper half, and all three keep
        //their tops in the group's own air
        private const float APEX_DUST = 0.15f;
        private const float MID_DUST = 0.1f;

        //The share of the dunes' air a face at this height takes: Pyramid.fx's dust over Pyramids.SandTop
        private static float Dust(float y, float sandTop) => MathF.Exp(-MathF.Max(y - sandTop, 0f) / Pyramids.DustThin);

        /// <summary>The dust takes the foot of Khufu, Khafre and Menkaure, not the pyramid: at each apex it lays at most
        /// <see cref="APEX_DUST"/> of the dunes' air, and on Khufu and Khafre at most <see cref="MID_DUST"/> from mid-height up.
        /// A <c>Pyramids.DustThin</c> long beside them would take the dunes' air up the faces and dissolve them into the murk.
        /// Menkaure, about 1400 out from the play lens where the ground has faded into the sky, takes about half of it at
        /// mid-height and its lower half melts into the horizon with the ground; the queens are let go with the ground round
        /// them.</summary>
        [Fact]
        public void TheDustTakesTheFootOfTheThreeGreatPyramidsNotThePyramid()
        {
            var config = new DesertSceneConfig();
            float top = Pyramids.SandTop(config);
            string[] names = { "Khufu", "Khafre", "Menkaure" };
            int k = 0;
            foreach (var (centre, _, height, _) in Pyramids.Placement(config).Take(3))
            {
                string name = names[k++];
                float apex = Dust(centre.Y + height, top), mid = Dust(centre.Y + 0.5f * height, top);
                _output.WriteLine($"{name}: base {centre.Y:F1}, height {height:F1}, dust at mid-height {mid:F3}, at the apex {apex:F3}");
                Assert.True(apex <= APEX_DUST, $"{name}'s apex takes {apex:F3} of the dunes' air (DustThin {Pyramids.DustThin})");
                if (name != "Menkaure")
                    Assert.True(mid <= MID_DUST, $"{name} takes {mid:F3} of the dunes' air at mid-height (DustThin {Pyramids.DustThin})");
            }
        }
    }
}
