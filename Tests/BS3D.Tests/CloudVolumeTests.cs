using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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

        private static readonly string Source = FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "CloudVolume.fx"));

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        private static float Constant(string name)
        {
            Match match = Regex.Match(File.ReadAllText(Source), $@"static const float {name} = ([0-9.]+);");
            Assert.True(match.Success, $"CloudVolume.fx has no {name}");
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The most the billows can make of a point at <paramref name="u"/> radii from the axis and <paramref name="h"/> of the
        /// height (CloudVolume.fx's HeapInside and HeapDensity, the noise at its extreme in the push's favour): its outline,
        /// plus the push of both octaves at the baked noise's largest value, faded by how far out the point stands.
        /// </summary>
        private static float HighestBody(float u, float h, float noiseMax, float reach, float fadeFrom, bool fades)
        {
            float waistAt = Constant("CLOUD_WAIST"), baseWidth = Constant("CLOUD_BASE_WIDTH");
            float push = Constant("CLOUD_BILLOW_PUSH") + Constant("CLOUD_RIM_PUSH");
            float inside, e;
            if (h < waistAt)
            {
                float waist = MathHelper.Lerp(baseWidth, 1f, h / waistAt);
                inside = waist - u;
                e = u / waist;
            }
            else
            {
                float v = (h - waistAt) / (1f - waistAt);
                e = MathF.Sqrt(u * u + v * v);
                inside = 1f - e;
            }
            float t = MathHelper.Clamp((e - fadeFrom) / (reach - fadeFrom), 0f, 1f);
            float weight = fades ? 1f - t * t * (3f - 2f * t) : 1f;
            return inside + weight * push * noiseMax;
        }

        //How far out (HeapInside's e, 1 on the outline) the billows can leave any density at all, at the waist where the
        //outline is widest: the first e past which the highest body the noise can make stays at or under 0
        private static float Support(float noiseMax, float reach, float fadeFrom, bool fades)
        {
            float waistAt = Constant("CLOUD_WAIST");
            for (float u = 1f; u < 3f; u += 0.001f)
                if (HighestBody(u, waistAt, noiseMax, reach, fadeFrom, fades) <= 0f) return u;
            return 3f;
        }

        /// <summary>
        /// A straight vertical edge on a heap's flank (#1145): the march stopped where the density could still be solid, so
        /// in the phases a billow rolled out to it the heap was cut off there by a wall. Now the billows' push fades out
        /// before the reach, so nothing a heap's noise can do leaves density at or past it (held against the baked noise's
        /// own extreme: a trilinear sample never exceeds a texel), and every face the march stops at stands at the reach:
        /// the box, the cylinder below the waist and the cap's ellipsoid over it. Seen to fire: the push unfaded reaches past
        /// the reach, and the bound before, a cylinder 1.12 radii round the axis inside a box whose sides stood at 1, left the
        /// billows solid at both.
        /// </summary>
        [Fact]
        public void NoNoiseLeavesDensityWhereTheMarchStops()
        {
            float noiseMax = 0f;
            foreach (float texel in ProxyVolumes.GradientNoiseTexels(CloudVolumes.NOISE_PERIOD, CloudVolumes.NOISE_TEXELS_PER_UNIT, CloudVolumes.NOISE_SEED))
                noiseMax = MathF.Max(noiseMax, MathF.Abs(texel));
            Assert.InRange(noiseMax, 0.3f, 1f);

            float reach = Constant("CLOUD_REACH"), fadeFrom = Constant("CLOUD_PUSH_FADE_FROM");
            float support = Support(noiseMax, reach, fadeFrom, fades: true);
            Assert.True(support < reach - 0.05f, $"the billows can leave density {support:0.000} out, against a reach of {reach}");

            //Seen to fire: unfaded, the push stands past the reach; and the bound before #1145 cut inside the support
            Assert.True(Support(noiseMax, reach, fadeFrom, fades: false) > reach, "the unfaded push stays inside the reach: the check cannot tell");
            Assert.True(Support(noiseMax, 1.12f, 1.12f, fades: false) > 1.12f, "the bound before #1145 held its billows: the check cannot tell a cut");

            //Every face the march stops at is the reach's: the push's fade, the box, the cylinder and the cap
            string source = File.ReadAllText(Source);
            Assert.Contains("float push = 1.0 - smoothstep(CLOUD_PUSH_FADE_FROM, CLOUD_REACH, e);", source);
            Assert.Contains("extent = float3(input.Cloud.w * CLOUD_REACH, input.Shape.x * CLOUD_TOP, input.Cloud.w * CLOUD_REACH)", source);
            Assert.Contains("float reachR = radius * CLOUD_REACH;", source);
            Assert.Contains("float capY = shape.x * (1.0 - CLOUD_WAIST) * CLOUD_REACH;", source);
            Assert.Contains("static const float CLOUD_TOP = CLOUD_WAIST + CLOUD_REACH * (1.0 - CLOUD_WAIST);", source);
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
