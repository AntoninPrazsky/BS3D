using BS3D.Capture;
using Microsoft.Xna.Framework;
using System;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The capture's stated lens (#946): a held pose and a keyframed path, as pure functions of the seconds into the window.</summary>
    public class CaptureCameraTests
    {
        private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
            Assert.True(Vector3.Distance(expected, actual) <= tolerance, $"expected {expected}, got {actual}");

        private const string TwoKeys = """
            {
              "format": "bs3d-camera-path",
              "keys": [
                { "t": 0, "position": [0, 0, 10], "target": [0, 0, 0], "fov": 40, "ease": "linear" },
                { "t": 2, "position": [10, 4, 10], "target": [0, 2, 0], "fov": 60 }
              ]
            }
            """;

        [Fact]
        public void AHeldLensStaysWhereItIsPutAtEveryTime()
        {
            CaptureCamera held = CaptureCamera.Held(new Vector3(1f, 2f, 3f), new Vector3(0f, 0f, 0f), 45f);

            foreach (float t in new[] { -5f, 0f, 1.5f, 999f })
            {
                held.Pose(t, out Vector3 position, out Vector3 target, out float fov);
                AssertNear(new Vector3(1f, 2f, 3f), position);
                AssertNear(Vector3.Zero, target);
                Assert.Equal(45f, fov);
            }

            Assert.Equal(1, held.KeyCount);
        }

        [Fact]
        public void AHeldLensWithNoFieldOfViewLeavesTheFieldToThePlayLens()
        {
            CaptureCamera held = CaptureCamera.Held(new Vector3(0f, 0f, 5f), Vector3.Zero, float.NaN);
            held.Pose(0f, out _, out _, out float fov);

            Assert.True(float.IsNaN(fov));
            Assert.False(held.HasFov);
        }

        [Fact]
        public void ALensOnItsOwnTargetHasNoDirectionAndIsRefused() =>
            Assert.Throws<FormatException>(() => CaptureCamera.Held(Vector3.One, Vector3.One, 40f));

        [Fact]
        public void APathHoldsItsFirstKeyBeforeItAndItsLastAfterIt()
        {
            CaptureCamera path = CaptureCamera.Parse(TwoKeys);

            path.Pose(-3f, out Vector3 before, out _, out float fovBefore);
            AssertNear(new Vector3(0f, 0f, 10f), before);
            Assert.Equal(40f, fovBefore);

            path.Pose(2f, out Vector3 at, out Vector3 target, out float fovAt);
            AssertNear(new Vector3(10f, 4f, 10f), at);
            AssertNear(new Vector3(0f, 2f, 0f), target);
            Assert.Equal(60f, fovAt, 3);

            path.Pose(50f, out Vector3 after, out _, out float fovAfter);
            AssertNear(new Vector3(10f, 4f, 10f), after);
            Assert.Equal(60f, fovAfter, 3);
        }

        [Fact]
        public void ALinearSegmentIsHalfWayAtHalfTheTime()
        {
            CaptureCamera path = CaptureCamera.Parse(TwoKeys);
            path.Pose(1f, out Vector3 position, out Vector3 target, out float fov);

            AssertNear(new Vector3(5f, 2f, 10f), position);
            AssertNear(new Vector3(0f, 1f, 0f), target);
            Assert.Equal(50f, fov, 3);
        }

        [Fact]
        public void TheSmoothEaseIsTheDefaultAndStartsSlowerThanLinear()
        {
            CaptureCamera smooth = CaptureCamera.Parse(TwoKeys.Replace("\"ease\": \"linear\"", "\"ease\": \"smooth\""));
            CaptureCamera byDefault = CaptureCamera.Parse(TwoKeys.Replace(", \"ease\": \"linear\"", ""));

            smooth.Pose(0.5f, out Vector3 a, out _, out _);
            byDefault.Pose(0.5f, out Vector3 b, out _, out _);
            AssertNear(a, b);

            //A quarter of the way through, smoothstep has covered 0.156 of the segment and a linear move 0.25
            Assert.Equal(10f * 0.15625f, a.X, 3);

            smooth.Pose(1f, out Vector3 half, out _, out _);
            Assert.Equal(5f, half.X, 3);
        }

        [Theory]
        [InlineData("Linear", 0.25f, 0.25f)]
        [InlineData("Smooth", 0.25f, 0.15625f)]
        [InlineData("In", 0.5f, 0.25f)]
        [InlineData("Out", 0.5f, 0.75f)]
        public void EachEaseCoversItsShareOfTheSegment(string name, float fraction, float share)
        {
            CaptureEase ease = Enum.Parse<CaptureEase>(name);

            Assert.Equal(share, CaptureCamera.Eased(ease, fraction), 5);
            Assert.Equal(0f, CaptureCamera.Eased(ease, 0f), 6);
            Assert.Equal(1f, CaptureCamera.Eased(ease, 1f), 6);
        }

        [Fact]
        public void ASplineGoesThroughItsKeysAndNeverStopsAtOne()
        {
            const string spline = """
                { "interpolation": "spline", "keys": [
                  { "t": 0, "position": [0, 0, 0], "target": [0, 0, -10] },
                  { "t": 1, "position": [1, 0, 0], "target": [0, 0, -10] },
                  { "t": 2, "position": [2, 0, 0], "target": [0, 0, -10] } ] }
                """;
            CaptureCamera path = CaptureCamera.Parse(spline);

            path.Pose(1f, out Vector3 middle, out _, out _);
            AssertNear(new Vector3(1f, 0f, 0f), middle);

            //Evenly spaced collinear keys: the curve is the straight line at a steady rate, through the middle key
            path.Pose(0.99f, out Vector3 before, out _, out _);
            path.Pose(1.01f, out Vector3 after, out _, out _);
            Assert.Equal(1f, (after.X - before.X) / 0.02f, 2);

            //The eased path comes to rest at that key
            CaptureCamera eased = CaptureCamera.Parse(spline.Replace("\"spline\"", "\"eased\""));
            eased.Pose(0.99f, out Vector3 easedBefore, out _, out _);
            eased.Pose(1.01f, out Vector3 easedAfter, out _, out _);
            Assert.True((easedAfter.X - easedBefore.X) / 0.02f < 0.1f, "an eased path is nearly at rest at its key");
        }

        [Fact]
        public void ASplineKeepsAFieldOfViewOfItsOwnOnlyWhenTheKeysState()
        {
            CaptureCamera withFov = CaptureCamera.Parse(TwoKeys.Replace("\"keys\"", "\"interpolation\": \"spline\", \"keys\""));
            withFov.Pose(1f, out _, out _, out float fov);
            Assert.InRange(fov, 40f, 60f);

            CaptureCamera none = CaptureCamera.Parse("""{ "interpolation": "spline", "keys": [ { "t": 0, "position": [0,0,1], "target": [0,0,0] }, { "t": 1, "position": [0,1,1], "target": [0,0,0] } ] }""");
            none.Pose(0.5f, out _, out _, out float noFov);
            Assert.True(float.IsNaN(noFov));
        }

        [Fact]
        public void CommentsAndTrailingCommasAreAllowedInTheFile()
        {
            CaptureCamera path = CaptureCamera.Parse("""
                // a slow push in
                { "keys": [
                  { "t": 0, "position": [0, 0, 10], "target": [0, 0, 0], }, /* the start */
                  { "t": 1, "position": [0, 0, 5], "target": [0, 0, 0], },
                ], }
                """);

            Assert.Equal(2, path.KeyCount);
        }

        [Theory]
        [InlineData("not json at all", "not JSON")]
        [InlineData("[]", "object")]
        [InlineData("""{ "keys": [] }""", "non-empty")]
        [InlineData("""{ "format": "other", "keys": [ { "t": 0, "position": [0,0,1], "target": [0,0,0] } ] }""", "format")]
        [InlineData("""{ "interpolation": "bezier", "keys": [ { "t": 0, "position": [0,0,1], "target": [0,0,0] } ] }""", "interpolation")]
        [InlineData("""{ "keys": [ { "position": [0,0,1], "target": [0,0,0] } ] }""", "\"t\"")]
        [InlineData("""{ "keys": [ { "t": -1, "position": [0,0,1], "target": [0,0,0] } ] }""", "\"t\"")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [0,0,1], "target": [0,0,0] }, { "t": 0, "position": [0,0,2], "target": [0,0,0] } ] }""", "must rise")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [0,0], "target": [0,0,0] } ] }""", "position")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [0,0,"x"], "target": [0,0,0] } ] }""", "position")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [0,0,1] } ] }""", "target")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [1,1,1], "target": [1,1,1] } ] }""", "same point")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [0,0,1], "target": [0,0,0], "fov": 0.5 } ] }""", "fov")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [0,0,1], "target": [0,0,0], "fov": 40 }, { "t": 1, "position": [0,0,2], "target": [0,0,0] } ] }""", "every key or on none")]
        [InlineData("""{ "keys": [ { "t": 0, "position": [0,0,1], "target": [0,0,0], "ease": "bounce" } ] }""", "ease")]
        public void AFileThatCannotBeFlownIsRefusedWithWhatIsWrongWithIt(string json, string mentions)
        {
            FormatException refused = Assert.Throws<FormatException>(() => CaptureCamera.Parse(json));

            Assert.Contains(mentions, refused.Message);
        }

        [Theory]
        [InlineData("0,-4,30", 0f, -4f, 30f, true)]
        [InlineData("1.5,2.25,-3", 1.5f, 2.25f, -3f, true)]
        [InlineData("1,2", 0f, 0f, 0f, false)]
        [InlineData("1,2,x", 0f, 0f, 0f, false)]
        [InlineData("", 0f, 0f, 0f, false)]
        public void AVectorIsReadTheWayTheTestbedWritesIt(string text, float x, float y, float z, bool ok)
        {
            Assert.Equal(ok, CaptureCamera.TryParseVector(text, out Vector3 vector));
            if (ok) AssertNear(new Vector3(x, y, z), vector);
        }
    }
}
