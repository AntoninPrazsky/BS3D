using System;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The clouds as volumes on High and Ultra (#989): where a heap stands is the deck's own weather, so what shades the ground
    /// is still what is overhead. Held without a device: every heap stands on cloud the deck has, inside the reach, far ones
    /// first; the heaps ride the weather's drift; a lid of a sky (Overcast, Storm) has none and keeps its deck.
    /// </summary>
    public class CloudVolumeTests
    {
        private static CloudField Sky(WeatherPreset preset, float time)
        {
            var clouds = new CloudField();
            clouds.SetWeatherImmediately(preset);
            clouds.Time = time;
            return clouds;
        }

        private static (CloudVolumes.Heap[] Heaps, int Count) Place(Vector3 lens, CloudField clouds)
        {
            var heaps = new CloudVolumes.Heap[CloudVolumes.CAPACITY];
            int count = CloudVolumes.Place(lens, clouds, heaps, new float[CloudVolumes.CAPACITY]);
            return (heaps, count);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(137f)]
        [InlineData(2400f)]
        public void EveryHeapStandsOnTheDecksCloudInsideTheReachFarFirst(float time)
        {
            CloudField clouds = Sky(WeatherPreset.Scattered, time);
            var lens = new Vector3(12f, -4f, 30f);
            (CloudVolumes.Heap[] heaps, int count) = Place(lens, clouds);

            Assert.True(count > 10, $"{count} heaps in a scattered sky");
            float last = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector4 cloud = heaps[i].Cloud;
                var at = new Vector2(cloud.X, cloud.Z);
                float distance = Vector2.Distance(at, new Vector2(lens.X, lens.Z));
                Assert.True(clouds.Cover(at) >= CloudVolumes.COVER_THRESHOLD, $"heap {i} stands where the deck's cover is {clouds.Cover(at):0.000}");
                Assert.True(distance <= CloudVolumes.REACH_DISTANCE, $"heap {i} stands {distance:0} out");
                Assert.True(distance <= last + 1e-3f, $"heap {i} is nearer than the one before it: not far first");
                Assert.InRange(cloud.Y, clouds.PlaneY - 0.1f * heaps[i].Shape.X, clouds.PlaneY);
                last = distance;
            }

            //Seen to fire: a sky where the deck has nothing (Clear) places nothing
            Assert.Equal(0, Place(lens, Sky(WeatherPreset.Clear, time)).Count);
        }

        /// <summary>The heaps ride the weather's first octave, as the deck's own cloud does: a second later each stands Wind further.</summary>
        [Fact]
        public void TheHeapsDriftWithTheDeck()
        {
            var lens = new Vector3(0f, -4f, 30f);
            CloudField before = Sky(WeatherPreset.Scattered, 500f), after = Sky(WeatherPreset.Scattered, 501f);
            (CloudVolumes.Heap[] a, int countA) = Place(lens, before);
            (CloudVolumes.Heap[] b, int countB) = Place(lens, after);

            //Each heap of the first frame found in the second where its lattice point has drifted to (the seed and the height
            //say which it is); the cover moves too, slowly, so most and not all are there
            int found = 0;
            for (int i = 0; i < countA; i++)
            {
                Vector2 expected = new Vector2(a[i].Cloud.X, a[i].Cloud.Z) - before.Wind;
                for (int j = 0; j < countB; j++)
                    if (b[j].Shape.Y == a[i].Shape.Y && Vector2.Distance(new Vector2(b[j].Cloud.X, b[j].Cloud.Z), expected) < 1e-2f) { found++; break; }
            }
            Assert.True(found >= countA * 0.9f, $"{found} of {countA} heaps found a second later where the wind took them");
        }

        /// <summary>The layer's own far plane (it has no depth buffer, so the heaps may stand past the scene's): only the far plane moves.</summary>
        [Fact]
        public void TheLayersProjectionMovesOnlyTheFarPlane()
        {
            Matrix camera = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(60f), 16f / 9f, 0.1f, 2000f);
            Matrix far = CloudVolumes.FarProjection(camera, 4500f);
            Assert.Equal(0.1f, far.M43 / far.M33, 4);
            Assert.Equal(camera.M11, far.M11);
            Assert.Equal(camera.M22, far.M22);
            //A point 4400 ahead is inside it (depth under 1), and was past the camera's own
            Vector4 point = Vector4.Transform(new Vector4(0f, 0f, -4400f, 1f), far);
            Assert.InRange(point.Z / point.W, 0f, 1f);
            Vector4 before = Vector4.Transform(new Vector4(0f, 0f, -4400f, 1f), camera);
            Assert.True(before.Z / before.W > 1f, "the camera's own projection already reached 4400");
        }

        [Fact]
        public void ALidOfASkyStandsNoHeaps()
        {
            Assert.Equal(1f, CloudVolumes.Strength(Sky(WeatherPreset.Scattered, 0f)), 3);
            Assert.Equal(1f, CloudVolumes.Strength(Sky(WeatherPreset.Broken, 0f)), 3);
            Assert.Equal(0f, CloudVolumes.Strength(Sky(WeatherPreset.Overcast, 0f)), 3);
            Assert.Equal(0f, CloudVolumes.Strength(Sky(WeatherPreset.Storm, 0f)), 3);
            Assert.Equal(0, Place(new Vector3(0f, -4f, 30f), Sky(WeatherPreset.Overcast, 0f)).Count);
        }
    }
}
