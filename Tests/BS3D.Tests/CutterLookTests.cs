using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The Cutter's own look (#766): a steel ball with a saw blade through it, in a frame <c>BallRenderSet</c> builds each
    /// draw. The shaders are not run; what is held is what the look promises - the blade's teeth stand past the ball and
    /// inside the gun's bore, the frame faces the eye whatever the round's own turn, and on the Potato path the round
    /// glows in its accent and never beats.
    /// </summary>
    public class CutterLookTests
    {
        private const float BALL_RADIUS = 0.5f;

        [Fact]
        public void TheTeethStandPastTheBallAndInsideTheBore()
        {
            float[] reach = SawBladeMesh.Outline().Select(p => p.Length()).ToArray();

            Assert.True(reach.Min() > BALL_RADIUS, $"a gullet sinks into the ball ({reach.Min()}), so the teeth would not show");
            Assert.True(reach.Max() <= SawBladeMesh.TIP_RADIUS + 1e-5f, $"a tooth reaches {reach.Max()}, past the blade's stated tip");

            //A level disc inside a tilted tube reaches the tube's axis no further than its own radius, and the blade's frame is
            //only ever turned about the round's centre, so its reach is what has to clear the bore at every aim
            Assert.True(SawBladeMesh.TIP_RADIUS < CannonRig.BORE_RADIUS, "a loaded cutter's blade would cut through the barrel");

            //And the hub starts under the coarsest sphere's chords at the equator, so its inner edge never shows
            float coarsestChord = BALL_RADIUS * MathF.Cos(MathF.PI / BallRenderSet.SphereSlices(BallRenderSet.LodCount - 1));
            Assert.True(SawBladeMesh.HUB_RADIUS < coarsestChord);
        }

        [Fact]
        public void TheTeethAreNotAllAlike()
        {
            //#640: a blade with every tooth the same reads as a gear, not as a used tool
            Vector2[] outline = SawBladeMesh.Outline();
            int perTooth = outline.Length / SawBladeMesh.TEETH;
            float[] tips = Enumerable.Range(0, SawBladeMesh.TEETH)
                .Select(t => Enumerable.Range(0, perTooth).Max(k => outline[t * perTooth + k].Length())).ToArray();

            Assert.True(tips.Max() - tips.Min() > 0.01f, "every tooth stands as tall as the next");
        }

        [Theory]
        [InlineData(0f, 0f, 0f)]
        [InlineData(0.6f, 0.3f, -1.1f)]
        [InlineData(2.0f, -0.4f, 0.25f)]
        public void TheFrameFacesTheEyeWhateverTheRoundsOwnTurn(float yaw, float pitch, float roll)
        {
            Vector3 place = new(3f, 1.5f, -2f);
            Vector3 eye = new(-4f, 6f, 12f);
            Matrix world = Matrix.CreateScale(1.3f) * Matrix.CreateFromYawPitchRoll(yaw, pitch, roll) * Matrix.CreateTranslation(place);

            Matrix frame = BallRenderSet.CutterFrame(world, eye, 0.7f);
            Matrix level = BallRenderSet.CutterFrame(Matrix.CreateScale(1.3f) * Matrix.CreateTranslation(place), eye, 0.7f);

            //The place and the scale kept, the round's own turn thrown away
            Assert.Equal(place, frame.Translation);
            AssertNear(1.3f, frame.Right.Length());
            AssertNear(1.3f, frame.Up.Length());
            AssertNear(1.3f, frame.Backward.Length());
            AssertNear(level.Right, frame.Right);
            AssertNear(level.Up, frame.Up);
            AssertNear(level.Backward, frame.Backward);

            //Orthogonal and right-handed, as every instance matrix is
            AssertNear(0f, Vector3.Dot(frame.Right, frame.Up));
            AssertNear(0f, Vector3.Dot(frame.Up, frame.Backward));
            AssertNear(0f, Vector3.Dot(frame.Right, frame.Backward));
            Assert.True(Vector3.Dot(Vector3.Cross(frame.Right, frame.Up), frame.Backward) > 0f);

            //The blade's normal leans towards the eye and up from it: the saw is seen face-on, tipped back towards level
            Vector3 normal = Vector3.Normalize(frame.Up);
            Vector3 towards = Vector3.Normalize(eye - place);
            Assert.True(Vector3.Dot(normal, towards) > 0.8f, $"the blade does not face the eye ({Vector3.Dot(normal, towards)})");
            Assert.True(normal.Y > towards.Y, "the blade is not tipped back towards level");
        }

        [Fact]
        public void TheSpinTurnsTheRoundAboutTheBladesAxisAlone()
        {
            Matrix world = Matrix.CreateTranslation(1f, 2f, 3f);
            Vector3 eye = new(0f, 5f, 20f);

            Matrix a = BallRenderSet.CutterFrame(world, eye, 0f);
            Matrix b = BallRenderSet.CutterFrame(world, eye, 1.1f);

            AssertNear(a.Up, b.Up);
            AssertNear(MathF.Cos(1.1f), Vector3.Dot(a.Right, b.Right));
        }

        [Fact]
        public void OnThePotatoPathItGlowsInItsAccentAndNeverBeats()
        {
            Vector3 accent = Linear(BasicEffectParamsProvider.CutterAccent);

            InstancedModelRenderer.PotatoKindLook ball = InstancedModelRenderer.PotatoKindLookFor(BallShading.Cutter, accent, 0.6f);
            InstancedModelRenderer.PotatoKindLook blade = InstancedModelRenderer.PotatoKindLookFor(BallShading.CutterBlade, accent, 0.6f);

            Assert.Equal(0f, ball.BeatShare);
            Assert.Equal(0f, blade.BeatShare);
            Assert.True(Vector3.Dot(Vector3.Normalize(blade.Emission), Vector3.Normalize(accent)) > 0.999f, "the blade glows in another colour");

            //Steel, not the accent: the ball's casing has no hue to speak of
            Assert.True(MathF.Max(ball.Casing.X, MathF.Max(ball.Casing.Y, ball.Casing.Z)) - MathF.Min(ball.Casing.X, MathF.Min(ball.Casing.Y, ball.Casing.Z)) < 0.06f);
        }

        [Fact]
        public void TheAccentIsNoneOfTheThirteen()
        {
            //A beam or a collar in the accent must not be a colour the round could match: no ball's tint is within reach of it
            Vector3 accent = BasicEffectParamsProvider.CutterAccent;

            foreach (BallType type in Enum.GetValues<BallType>())
                Assert.True(Vector3.Distance(accent, BasicEffectParamsProvider.GetDiffuseTintByType(type)) > 0.3f, $"{type} is the accent's colour");
        }

        private static Vector3 Linear(Vector3 c) => c * (c * (c * 0.305306011f + new Vector3(0.682171111f)) + new Vector3(0.012522878f));

        private static void AssertNear(float expected, float actual) => Assert.InRange(actual, expected - 1e-4f, expected + 1e-4f);

        private static void AssertNear(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"{actual} is not {expected}");
    }
}
