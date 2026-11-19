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

        private static readonly string SourceText = File.ReadAllText(Source);

        private static float Constant(string name)
        {
            Match match = Regex.Match(SourceText, $@"static const float {name} = ([0-9.]+);");
            Assert.True(match.Success, $"CloudVolume.fx has no {name}");
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Where a point in a heap's own measure stands against its outline (CloudVolume.fx's HeapInside): how far inside it,
        /// in radii, and how far out towards the margin's bound (1 on it).
        /// </summary>
        private static readonly float WaistAt = Constant("CLOUD_WAIST"), BaseWidth = Constant("CLOUD_BASE_WIDTH"), Margin = Constant("CLOUD_MARGIN"),
            FadeFrom = Constant("CLOUD_PUSH_FADE_FROM");

        private static (float Inside, float E) Outline(Vector3 l, float aspect)
        {
            float waistAt = WaistAt, baseWidth = BaseWidth, margin = Margin;
            float waistY = waistAt * aspect, capY = (1f - waistAt) * aspect;
            float h = l.Y / aspect;
            var c = new Vector3(l.X, l.Y - waistY, l.Z);
            var axes = new Vector3(1f, capY, 1f);
            float k0 = (c / axes).Length(), k1 = MathF.Max((c / (axes * axes)).Length(), 1e-6f);
            float e = new Vector3(l.X, MathF.Max(c.Y, 0f) * (1f + margin) / (capY + margin), l.Z).Length() / (1f + margin);
            float inside = h < waistAt ? MathHelper.Lerp(baseWidth, 1f, h / waistAt) - new Vector2(l.X, l.Z).Length() : -k0 * (k0 - 1f) / k1;
            return (inside, e);
        }

        /// <summary>
        /// How far towards the margin's bound (e, 1 on it) the billows can leave any density in a heap of this height, the
        /// noise at its extreme in the push's favour everywhere (<paramref name="push"/>): the farthest e on any ray out from
        /// the cap's centre, up, out or between, where the outline plus the push, faded as the shader fades it, is above 0.
        /// </summary>
        private static float Support(float aspect, float push, bool fades)
        {
            float fadeFrom = FadeFrom, waistY = WaistAt * aspect;
            float farthest = 0f;
            for (int a = 0; a <= 90; a++)
            {
                float angle = MathHelper.ToRadians(a * 2f - 90f);
                var direction = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
                for (float s = 0f; s < 6f; s += 0.002f)
                {
                    Vector3 l = new Vector3(0f, waistY, 0f) + direction * s;
                    if (l.Y < 0f) break;
                    (float inside, float e) = Outline(l, aspect);
                    if (e > 1.5f) break;
                    float t = MathHelper.Clamp((e - fadeFrom) / (1f - fadeFrom), 0f, 1f);
                    float weight = fades ? 1f - t * t * (3f - 2f * t) : 1f;
                    if (inside + weight * push > 0f) farthest = MathF.Max(farthest, e);
                }
            }
            return farthest;
        }

        /// <summary>
        /// A straight vertical edge on a heap's flank (#1145): the march stopped where the density could still be solid, so
        /// in the phases a billow rolled out to it the heap was cut off there by a wall. Now the march is bounded by the
        /// outline grown by a margin, and the billows' push fades out before that bound, so in a heap of any height nothing
        /// its noise can do leaves density on a face the march stops at (held against the baked noise's own extreme: a
        /// trilinear sample never exceeds a texel). Seen to fire: the push unfaded stands on the bound, and the bound before,
        /// a cylinder 1.12 radii round the axis inside a box whose sides stood at 1, left the billows solid at both.
        /// </summary>
        [Fact]
        public void NoNoiseLeavesDensityWhereTheMarchStops()
        {
            float noiseMax = 0f;
            foreach (float texel in ProxyVolumes.GradientNoiseTexels(CloudVolumes.NOISE_PERIOD, CloudVolumes.NOISE_TEXELS_PER_UNIT, CloudVolumes.NOISE_SEED))
                noiseMax = MathF.Max(noiseMax, MathF.Abs(texel));
            Assert.InRange(noiseMax, 0.3f, 1f);

            //The billows' magnitude less its bias, and the rim's signed octave
            float push = Constant("CLOUD_BILLOW_PUSH") * (2f * noiseMax - Constant("CLOUD_PUFF_BIAS")) + Constant("CLOUD_RIM_PUSH") * noiseMax;
            foreach (float aspect in new[] { 0.5f, 0.75f, 1f, 1.5f, 2.4f, 3.5f })
            {
                float support = Support(aspect, push, fades: true);
                Assert.True(support < 0.99f, $"a heap {aspect} radii high can leave density {support:0.000} of the way to its bound");
                //Seen to fire: unfaded, the push stands on the bound
                Assert.True(Support(aspect, push, fades: false) >= 1f, $"unfaded, a heap {aspect} radii high stays inside its bound: the check cannot tell");
            }

            //And the bound before #1145 cut the billows (then signed: 0.6 and 0.25 of the noise) at the waist: the cylinder at
            //1.12 radii and the box's sides at 1
            float before = 0.6f * noiseMax + 0.25f * noiseMax;
            Assert.True(1f - 1.12f + before > 0f && 1f - 1f + before > 0f, "the bound before #1145 held its billows: the check cannot tell a cut");

            //Every face the march stops at is the margin's, in the heap's own measure: the push's fade, the bound's measure,
            //the box, the cylinder and the cap; and the billows are the magnitude the push above was taken from
            string source = SourceText;
            Assert.Contains("float push = 1.0 - smoothstep(CLOUD_PUSH_FADE_FROM, 1.0, e);", source);
            Assert.Contains("e = length(float3(l.x, max(c.y, 0.0) * (1.0 + CLOUD_MARGIN) / (capY + CLOUD_MARGIN), l.z)) / (1.0 + CLOUD_MARGIN);", source);
            Assert.Contains("float3 l = input.Position.xyz * float3(1.0 + CLOUD_MARGIN, aspect + CLOUD_MARGIN, 1.0 + CLOUD_MARGIN);", source);
            Assert.Contains("float reach = 1.0 + CLOUD_MARGIN;", source);
            Assert.Contains("capY = (1.0 - CLOUD_WAIST) * aspect + CLOUD_MARGIN;", source);
            Assert.Contains("float billow = lerp(n, abs(n) * 2.0 - CLOUD_PUFF_BIAS * saturate(1.0 - inside * 3.0), smoothstep(CLOUD_PUFF_FROM, CLOUD_PUFF_TO, h));", source);
        }

        private static float Percentile(float[] values, float share)
        {
            float[] sorted = (float[])values.Clone();
            Array.Sort(sorted);
            return sorted[(int)MathF.Min(sorted.Length - 1, share * sorted.Length)];
        }

        /// <summary>
        /// Not a grid of like buns (#1145): in either sky that stands as heaps the field is a spread, many small heaps and a few
        /// big ones (the biggest tenth at least three times the smallest tenth across), and among them flat heaps and towers.
        /// Seen to fire on the placement before #1145, whose heaps differed by the cover alone: 63 to 105 radii, a ratio of 1.6.
        /// </summary>
        [Theory]
        [InlineData(WeatherPreset.Scattered)]
        [InlineData(WeatherPreset.Broken)]
        public void ASkyIsASpreadOfSizesAndHeights(WeatherPreset preset)
        {
            foreach (float time in new[] { 0f, 300f, 1200f })
            {
                (CloudVolumes.Heap[] heaps, int count) = Place(new Vector3(0f, -4f, 30f), Sky(preset, time));
                var radii = new float[count];
                var aspects = new float[count];
                for (int i = 0; i < count; i++)
                {
                    radii[i] = heaps[i].Cloud.W;
                    aspects[i] = heaps[i].Shape.X / heaps[i].Cloud.W;
                }
                float small = Percentile(radii, 0.1f), big = Percentile(radii, 0.9f);
                Assert.True(big >= 3f * small, $"{preset} at {time}: the biggest tenth from {big:0} radii, the smallest to {small:0}");
                Assert.True(Percentile(aspects, 0.05f) < 0.8f, $"{preset} at {time}: no flat heaps (the flattest twentieth {Percentile(aspects, 0.05f):0.00} radii high)");
                Assert.True(Percentile(aspects, 0.99f) > 1.8f, $"{preset} at {time}: no towers (the tallest hundredth {Percentile(aspects, 0.99f):0.00} radii high)");
            }
        }

        /// <summary>
        /// A pixel marches about one heap, two where two meet (#1145): however big a heap grows, its outline and a whole
        /// neighbour's overlap by no more than the room allows, each heap's reach taken along the line between them (its radius
        /// across, stretched by how near its axis that line runs). Seen to fire: the same field with the room left out stands
        /// its big heaps in each other.
        /// </summary>
        [Theory]
        [InlineData(WeatherPreset.Scattered)]
        [InlineData(WeatherPreset.Broken)]
        public void HeapsStandApartSoAPixelMarchesOneOrTwo(WeatherPreset preset)
        {
            const float ALLOWED = 1.15f;
            foreach (float time in new[] { 0f, 300f, 1200f })
            {
                (CloudVolumes.Heap[] heaps, int count) = Place(new Vector3(0f, -4f, 30f), Sky(preset, time));
                int crowded = 0;
                float worst = 0f;
                for (int i = 0; i < count; i++)
                    for (int j = i + 1; j < count; j++)
                    {
                        //A heap still fading in (or out) is let into its neighbour as far as it has faded: two whole ones are held
                        if (heaps[i].Shape.W < 0.999f || heaps[j].Shape.W < 0.999f) continue;
                        Vector2 a = new(heaps[i].Cloud.X, heaps[i].Cloud.Z), b = new(heaps[j].Cloud.X, heaps[j].Cloud.Z);
                        float apart = Vector2.Distance(a, b);
                        if (apart < 1e-3f || apart > 1200f) continue;
                        Vector2 toward = (b - a) / apart;
                        float reach = heaps[i].Cloud.W * Extent(heaps[i].Form, toward) + heaps[j].Cloud.W * Extent(heaps[j].Form, toward);
                        float overlap = reach / apart;
                        worst = MathF.Max(worst, overlap);
                        if (overlap > ALLOWED + 1e-3f) crowded++;
                    }
                Assert.True(crowded == 0, $"{preset} at {time}: {crowded} pairs of heaps stand in each other, the worst reaching {worst:0.00} of the distance between them");
            }
        }

        private static float Extent(Vector4 form, Vector2 direction)
        {
            float along = form.X * direction.X + form.Y * direction.Y;
            return form.Z / MathF.Sqrt(form.Z * form.Z * MathF.Max(1f - along * along, 0f) + along * along);
        }

        /// <summary>
        /// Heaps grow in and thin out as the weather evolves, without popping (#1145 keeps #989's promise through the room and the
        /// spread): half a second later, a breath against the weather's minutes, every heap within the reach the shader still
        /// shows is found where the wind took it, its radius and height all but the same and its fade within a small step of
        /// what it was; one there at one moment and not the other had all but faded. Seen to fire: with the cover's fade-in
        /// left out, heaps the cover reaches stand in whole.
        /// </summary>
        [Theory]
        [InlineData(WeatherPreset.Scattered)]
        [InlineData(WeatherPreset.Broken)]
        public void NothingPopsFromFrameToFrame(WeatherPreset preset)
        {
            const float NEAR_ENOUGH = 3000f, STEP = 0.5f, FADE_STEP = 0.25f;
            var lens = new Vector3(0f, -4f, 30f);
            int matched = 0;
            for (int k = 0; k < 12; k++)
            {
                float time = 37f + k * 211.7f;
                CloudField before = Sky(preset, time), after = Sky(preset, time + STEP);
                (CloudVolumes.Heap[] a, int countA) = Place(lens, before);
                (CloudVolumes.Heap[] b, int countB) = Place(lens, after);
                var found = new bool[countB];
                for (int i = 0; i < countA; i++)
                {
                    Vector2 expected = new Vector2(a[i].Cloud.X, a[i].Cloud.Z) - before.Wind * STEP;
                    //Past FADE_NEAR the shader dissolves a heap by its distance, and by the reach it is gone whatever its fade
                    if (Vector2.Distance(expected, new Vector2(lens.X, lens.Z)) > NEAR_ENOUGH) continue;
                    int j = Array.FindIndex(b, 0, countB, h => h.Shape.Y == a[i].Shape.Y && Vector2.Distance(new Vector2(h.Cloud.X, h.Cloud.Z), expected) < 1e-2f);
                    if (j < 0)
                    {
                        Assert.True(a[i].Shape.W < FADE_STEP, $"{preset} at {time}: a heap faded {a[i].Shape.W:0.000} in was gone a moment later");
                        continue;
                    }
                    found[j] = true;
                    matched++;
                    Assert.True(MathF.Abs(b[j].Cloud.W - a[i].Cloud.W) < 0.05f * a[i].Cloud.W + 3f, $"{preset} at {time}: a heap's radius jumped {a[i].Cloud.W:0.00} to {b[j].Cloud.W:0.00}");
                    Assert.True(MathF.Abs(b[j].Shape.X - a[i].Shape.X) < 0.05f * a[i].Shape.X + 3f, $"{preset} at {time}: a heap's height jumped {a[i].Shape.X:0.00} to {b[j].Shape.X:0.00}");
                    Assert.True(MathF.Abs(b[j].Shape.W - a[i].Shape.W) < FADE_STEP, $"{preset} at {time}: a heap's fade jumped {a[i].Shape.W:0.000} to {b[j].Shape.W:0.000}");
                }
                for (int j = 0; j < countB; j++)
                    if (!found[j] && Vector2.Distance(new Vector2(b[j].Cloud.X, b[j].Cloud.Z), new Vector2(lens.X, lens.Z)) < NEAR_ENOUGH - 1f)
                        Assert.True(b[j].Shape.W < FADE_STEP, $"{preset} at {time}: a heap appeared {b[j].Shape.W:0.000} faded in");
            }
            Assert.True(matched > 1000, $"only {matched} heaps were followed from frame to frame");
        }

        /// <summary>The broken sky, the most heaps any sky stands, keeps well inside the instance buffer, so no heap is ever dropped for room.</summary>
        [Fact]
        public void TheHeapsStayInsideTheBuffer()
        {
            for (int k = 0; k < 20; k++)
            {
                (_, int count) = Place(new Vector3(k * 97f, -4f, 30f), Sky(WeatherPreset.Broken, k * 173.3f));
                Assert.True(count < CloudVolumes.CAPACITY * 0.9f, $"{count} heaps of {CloudVolumes.CAPACITY}");
            }
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
