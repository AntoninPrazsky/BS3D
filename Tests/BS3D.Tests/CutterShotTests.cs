using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Physics;
using System;
using Xunit;
using Xunit.Abstractions;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// #767's verdict: the Cut's slash is drawn square to the shot (<c>CutSlash</c>), so a cutter's landing has to say which
    /// way it flew (<see cref="BallLanding.ShotDirection"/>). Cutters are fired through the real simulation and the Game's own
    /// contact handler (<see cref="ShotRig"/>) at balls of a real level, from dead centre to just past a graze, and what the
    /// landing says is held to the line the round was fired along.
    /// </summary>
    public class CutterShotTests(ITestOutputHelper output)
    {
        /// <summary>
        /// The most the landing's heading may stand off the line fired along, in degrees. The flight bends under gravity
        /// between the muzzle and the strike (a third of a degree over the rig's ~25 units at the gun's 200 u/s), and nothing
        /// else may turn it: the heading is the one the striking step began with, before that step's contacts.
        /// </summary>
        private const float MOST_DEGREES = 1.5f;

        [Fact]
        public void ACuttersLandingSaysWhichWayItFlew()
        {
            Random rng = new(767);
            int landed = 0;
            float worst = 0f, worstGlance = 0f;

            for (int i = 0; i < 12; i++)
            {
                using ShotRig rig = new("Pennant.json");

                (XVector3 muzzle, XVector3 target, float glance) = ShotAgreementTests.RandomAim(rig.Hung.Balls, rng);
                rig.Fire(muzzle, target, glance, kind: BallKind.Cutter);

                if (rig.LastLanding is not BallLanding landing) continue;
                landed++;

                Assert.Equal(BallKind.Cutter, landing.Kind);
                Assert.True(MathF.Abs(landing.ShotDirection.Length() - 1f) < 1e-4f,
                    $"the landing's heading is not a unit vector ({landing.ShotDirection}), so the slash takes the gun's aim instead");

                XVector3 fired = XVector3.Normalize(target - muzzle);
                float degrees = MathF.Acos(Math.Clamp(XVector3.Dot(fired, landing.ShotDirection), -1f, 1f)) * 180f / MathF.PI;
                output.WriteLine($"glance {glance:0.00}: the landing's heading stands {degrees:0.000} degrees off the line fired along");

                if (degrees > worst)
                {
                    worst = degrees;
                    worstGlance = glance;
                }
            }

            output.WriteLine($"{landed} of 12 landed; worst {worst:0.000} degrees at glance {worstGlance:0.00}");
            Assert.True(landed >= 8, $"only {landed} of 12 cutters landed, too few to hold anything");
            Assert.True(worst < MOST_DEGREES, $"a landing's heading stood {worst:0.00} degrees off the line fired along (glance {worstGlance:0.00})");
        }
    }
}
