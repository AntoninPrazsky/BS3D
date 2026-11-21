using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The outback's heat shimmer (#1135): the far horizon refracted in a thin strip at the skyline, read from a copy of the
    /// frame. No test can run its passes (they need a device), so these hold what can be held without a frame: the strip's
    /// angular band and that it never reaches the middle plain, the offset as an angle and the clock as the scene's, the
    /// depth gate and the far mark that keep a near thing's colour out of the strip, the rows the copy is scissored to, and
    /// the tiers that draw it. The shader's promises are read off its source with the comments taken out, as
    /// <see cref="OutbackAirTests"/> reads <c>Outback.fx</c>.
    /// </summary>
    public class HorizonShimmerTests
    {
        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //The text of a source file without its comments, so a word in a note is not taken for code
        private static string CodeOf(string relative) => Regex.Replace(File.ReadAllText(FindFile(relative)), @"//[^\n]*", string.Empty);

        private static string Shader() => CodeOf(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "HorizonShimmer.fx"));

        //The body of the HLSL function or technique named, from its name to its closing brace at the start of a line
        private static string Body(string code, string head)
        {
            int start = code.IndexOf(head, StringComparison.Ordinal);
            Assert.True(start >= 0, $"HorizonShimmer.fx has no {head}");
            int end = code.IndexOf("\n}", start, StringComparison.Ordinal);
            return code.Substring(start, end - start);
        }

        //The heights over the plain the Game's lenses fly at: the approach (2.4 over the ground), the play camera (6.1), the
        //intro's wide shot and the crane (about 40), and a lens far higher than any of them
        private static readonly float[] LensHeights = { 0.1f, 2.4f, 6.1f, 20f, 40f, 120f };

        private static float Degrees(float radians) => MathHelper.ToDegrees(radians);

        /// <summary>
        /// The strip is the skyline's: its axis is the plain where its last fade ends, it reaches no more than two degrees
        /// over it, and its bottom is the plain at the ring's radius, which is where that fade begins. So the plain the strip
        /// may touch is all beyond the start of the last fade, whatever the lens's height: nothing over the middle plain.
        /// </summary>
        [Fact]
        public void TheBandIsAStripAtTheSkyline()
        {
            OutbackAirConfig air = new OutbackSceneConfig().Air;

            Assert.True(air.HorizonHazeDistance >= 1000f && air.HorizonHazeDistance < FarField.FAR_FADE_END,
                $"the ring stands at {air.HorizonHazeDistance}, not between 1000 and the fade's end at {FarField.FAR_FADE_END}");

            foreach (float height in LensHeights)
            {
                HorizonShimmer.Elevations band = HorizonShimmer.BandFor(height, air.HorizonHazeDistance);
                float lens = MathF.Max(height, HorizonShimmer.MIN_LENS_HEIGHT);

                Assert.True(band.Bottom < band.Axis && band.Axis < band.Top, $"at {height} the band is {band}");
                Assert.True(Degrees(band.Top - band.Axis) <= 2f, $"at {height} the strip reaches {Degrees(band.Top - band.Axis):F2} degrees over the skyline");

                //The plain the band's bottom and axis look at, along a flat plain from the lens
                float bottomGround = lens / MathF.Tan(-band.Bottom);
                float axisGround = lens / MathF.Tan(-band.Axis);
                Assert.True(bottomGround >= air.HorizonHazeDistance - 0.5f, $"at {height} the strip's bottom looks at the plain {bottomGround:F0} out");
                Assert.InRange(axisGround, FarField.FAR_FADE_END - 0.5f, FarField.FAR_FADE_END + 0.5f);

                //And it is a strip, a degree or two either side of the skyline, at every height the Game's lenses fly at
                if (height <= 40f) Assert.True(Degrees(band.Axis - band.Bottom) <= 2f, $"at {height} the strip reaches {Degrees(band.Axis - band.Bottom):F2} degrees under the skyline");
            }

            //A ring at or past the skyline's distance would turn the band over: it is held short of it
            HorizonShimmer.Elevations turned = HorizonShimmer.BandFor(6.1f, FarField.FAR_FADE_END * 2f);
            Assert.True(turned.Bottom < turned.Axis, "a ring past the skyline turned the band over");
        }

        /// <summary>
        /// The outback hands the shimmer the plain's own figures: the ring at the distance where its last fade begins and the
        /// plain's height, and the host calls it after the sky and before the island's colour in both the Game and the Testbed.
        /// </summary>
        [Fact]
        public void TheOutbackDrawsItAtTheLastFadeAfterTheSky()
        {
            string backdrop = CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "OutbackBackdrop.cs"));
            Assert.Matches(@"_shimmer\?\.Draw\(\s*frame\s*,\s*pipeline\s*,\s*_outbackConfig\.Terrain\.LevelY\s*,\s*_outbackConfig\.Air\.HorizonHazeDistance\s*\)", backdrop);

            foreach (string host in new[] { Path.Combine("Game", "BS3DGame.Scene.cs"), Path.Combine("Testbed", "Testbed.Draw.cs") })
            {
                string code = CodeOf(host);
                int sky = code.IndexOf("DrawEnvironmentBlended(_scene, sceneFrame)", StringComparison.Ordinal);
                int shimmer = code.IndexOf("DrawHorizonRefraction(_scene, sceneFrame, _pipeline)", StringComparison.Ordinal);
                int island = code.IndexOf("_island.DrawIsland(", StringComparison.Ordinal);

                Assert.True(sky >= 0 && shimmer > sky && island > shimmer, $"{host} does not draw the shimmer between the environment's second half and the island");
            }
        }

        /// <summary>
        /// The offset is an angle: the figures are radians of about a pixel through the play camera's lens at 1080 rows, the
        /// shader adds the offset to an elevation and projects the direction back through the camera, and the copy's size in
        /// texels reaches nothing but the pixel's own texel. The rows the copy is padded by follow from the angle: twice the
        /// rows, twice the pad.
        /// </summary>
        [Fact]
        public void TheOffsetIsAnAngle()
        {
            //A pixel's angle through the Game's play lens (GameplayScreen.GAME_FOV, pi / 4.2) at 1080 rows
            float pixel = MathF.PI / 4.2f / 1080f;
            Assert.InRange(HorizonShimmer.AMPLITUDE / pixel, 1f, 3f);
            Assert.InRange(HorizonShimmer.MIRAGE_DEPTH / pixel, 1f, 4f);

            string code = Shader();

            //The waver and the mirror move an elevation, by the amplitude (radians) and by the axis's own wobble
            Assert.Matches(@"GrabUv\(\s*heading\s*,\s*elevation\s*\+\s*Amplitude\s*\*", code);
            Assert.Matches(@"float\s+axis\s*=\s*Band\.y\s*\+\s*Amplitude\s*\*", code);
            Assert.Contains("mul(float4(CameraPosition + direction * PROJECTED_DISTANCE, 1.0), ViewProjection)", Body(code, "float2 GrabUv("));

            //GrabSize finds the pixel's own texel and nothing else: once in its declaration and twice in the centre's line
            Assert.Equal(3, Regex.Matches(code, @"\bGrabSize\b").Count);
            Assert.Matches(@"float2\s+centre\s*=\s*\(floor\(uv\s*\*\s*GrabSize\)\s*\+\s*0\.5\)\s*/\s*GrabSize\s*;", code);
            Assert.DoesNotMatch(@"\bddx\b|\bddy\b|\bfwidth\b|VPOS", code);

            Matrix lens = Matrix.CreatePerspectiveFieldOfView(MathF.PI / 4.2f, 2.4f, 0.01f, 2000f);
            int pad1080 = HorizonShimmer.PadRows(lens, 1080) - 2, pad2160 = HorizonShimmer.PadRows(lens, 2160) - 2;
            Assert.InRange(pad2160, pad1080 * 2 - 1, pad1080 * 2 + 1);
        }

        /// <summary>
        /// The clock is the scene's, not a frame count: the shimmer's only time is the frame's <see cref="SceneFrame.Time"/>,
        /// the Game builds every scene frame on its wall clock, and the shader's field moves with that time and nothing else.
        /// </summary>
        [Fact]
        public void TheClockIsTheScenes()
        {
            string shimmer = CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "HorizonShimmer.cs"));
            Assert.Matches(@"_time\.SetValue\(\s*frame\.Time\s*%\s*CLOCK_WRAP\s*\)", shimmer);
            Assert.DoesNotMatch(@"\+\+|\+=|-=", shimmer.Substring(shimmer.IndexOf("public void Draw(", StringComparison.Ordinal)));

            Assert.Matches(@"SceneFrame\s+BuildSceneFrame\(\)\s*=>\s*_rig\.BuildSceneFrame\(\s*_camera\s*,\s*_wallClock\s*\)", CodeOf(Path.Combine("Game", "BS3DGame.Scene.cs")));

            string wobble = Body(Shader(), "float Wobble(");
            Assert.Equal(2, Regex.Matches(wobble, @"\bTime\s*\*\s*(FINE_)?TEMPO\b").Count);
        }

        /// <summary>
        /// Nothing near reaches the strip. The ring is tested against the scene's depth and writes none; the mark writes the
        /// alpha alone; the copy weighs every tap by that mark; and the refraction composites a read over the pixel's own far
        /// colour by the read's far share, so a read that lands on a near pixel (alpha 1, share 0) brings nothing back.
        /// </summary>
        [Fact]
        public void TheGateKeepsNearThingsOut()
        {
            DepthStencilState gate = HorizonShimmer.Gate;
            Assert.True(gate.DepthBufferEnable, "the ring is not depth-tested");
            Assert.False(gate.DepthBufferWriteEnable, "the ring writes depth");
            Assert.Equal(CompareFunction.LessEqual, gate.DepthBufferFunction);

            Assert.Equal(ColorWriteChannels.Alpha, HorizonShimmer.MarkBlend.ColorWriteChannels);
            Assert.Matches(@"float4\s+MarkPS\(BandPixel\s+input\)\s*:\s*COLOR\s*\{\s*return\s+0\s*;\s*\}", Shader());

            string tonemap = CodeOf(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Tonemap.fx"));
            string far = tonemap.Substring(tonemap.IndexOf("float4 SampleSceneFar(", StringComparison.Ordinal));
            far = far.Substring(0, far.IndexOf("\n}", StringComparison.Ordinal));
            Assert.Equal(2, Regex.Matches(far, @"float4\(scene\.rgb,\s*1\.0\)\s*\*\s*saturate\(1\.0\s*-\s*scene\.a\)").Count);

            string refract = Body(Shader(), "float4 RefractPS(");
            Assert.Matches(@"color\s*=\s*color\s*\*\s*\(1\.0\s*-\s*shifted\.a\)\s*\+\s*shifted\.rgb\s*;", refract);
            Assert.Matches(@"image\s*=\s*color\s*\*\s*\(1\.0\s*-\s*mirrored\.a\)\s*\+\s*mirrored\.rgb\s*;", refract);
            Assert.Matches(@"float3\s+color\s*=\s*own\.rgb\s*/\s*max\(own\.a", refract);
        }

        /// <summary>
        /// The copy is scissored to the rows the ring covers, so it must hold every pixel the ring draws: from lenses at the
        /// heights the Game flies, turned every way, pitched up and down and rolled a little as the shake rolls it, through
        /// narrow and wide lenses, every point of the band that lands on the screen lies inside the rows.
        /// </summary>
        [Fact]
        public void TheRowsHoldTheRing()
        {
            const int height = 1080;
            const float radius = 1000f;
            Random random = new(1135);
            int checkedViews = 0;

            for (int view = 0; view < 400; view++)
            {
                Vector3 lens = new((float)(random.NextDouble() * 600 - 300), -13.5f + LensHeights[view % LensHeights.Length], (float)(random.NextDouble() * 600 - 300));
                float yaw = (float)(random.NextDouble() * MathHelper.TwoPi);
                float pitch = MathHelper.ToRadians((float)(random.NextDouble() * 80 - 40));
                float roll = MathHelper.ToRadians((float)(random.NextDouble() * 6 - 3));
                float fov = MathHelper.ToRadians(30f + (float)random.NextDouble() * 40f);
                float aspect = 1.3f + (float)random.NextDouble() * 1.2f;

                Vector3 forward = new(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Sin(yaw));
                Vector3 up = Vector3.Transform(Vector3.Up, Matrix.CreateFromAxisAngle(forward, roll));
                Matrix viewProjection = Matrix.CreateLookAt(lens, lens + forward, up) * Matrix.CreatePerspectiveFieldOfView(fov, aspect, 0.01f, 2000f);

                HorizonShimmer.Elevations band = HorizonShimmer.BandFor(lens.Y + 13.5f, radius);
                float tanBottom = MathF.Tan(band.Bottom - HorizonShimmer.UNDER), tanTop = MathF.Tan(band.Top);

                bool any = HorizonShimmer.TryRows(viewProjection, lens, radius, tanBottom, tanTop, height, 0, out int first, out int count);

                //The band sampled far finer than the ring's chords, on the chords themselves (the drawn geometry)
                for (int s = 0; s < HorizonShimmer.SEGMENTS * 8; s++)
                {
                    float along = (float)s / (HorizonShimmer.SEGMENTS * 8) * HorizonShimmer.SEGMENTS;
                    int segment = (int)along;
                    float t = along - segment;
                    float a0 = MathHelper.TwoPi * segment / HorizonShimmer.SEGMENTS, a1 = MathHelper.TwoPi * (segment + 1) / HorizonShimmer.SEGMENTS;
                    Vector2 round = Vector2.Lerp(new Vector2(MathF.Cos(a0), MathF.Sin(a0)), new Vector2(MathF.Cos(a1), MathF.Sin(a1)), t);

                    for (int k = 0; k <= 8; k++)
                    {
                        float lift = MathHelper.Lerp(tanBottom, tanTop, k / 8f);
                        Vector4 clip = Vector4.Transform(new Vector4(lens.X + round.X * radius, lens.Y + lift * radius, lens.Z + round.Y * radius, 1f), viewProjection);
                        if (clip.W <= 0.01f) continue;

                        float x = clip.X / clip.W, y = clip.Y / clip.W;
                        if (x < -1f || x > 1f || y < -1f || y > 1f) continue;

                        float row = (1f - y) * 0.5f * height;
                        Assert.True(any, $"view {view}: the band is on the screen at row {row:F1} and no rows were given");
                        Assert.True(row >= first - 0.01f && row <= first + count + 0.01f, $"view {view}: the band's row {row:F1} is outside {first}..{first + count}");
                        checkedViews++;
                    }
                }
            }

            Assert.True(checkedViews > 10000, $"only {checkedViews} points of the band landed on the screen");
        }

        /// <summary>
        /// Looking straight down (nothing of the skyline in the frame) gives no rows, so the frame takes no copy at all.
        /// </summary>
        [Fact]
        public void NoSkylineNoCopy()
        {
            Vector3 lens = new(0f, 30f, 0f);
            Matrix viewProjection = Matrix.CreateLookAt(lens, lens + new Vector3(0.01f, -1f, 0f), Vector3.Forward)
                * Matrix.CreatePerspectiveFieldOfView(MathF.PI / 4.2f, 16f / 9f, 0.01f, 2000f);
            HorizonShimmer.Elevations band = HorizonShimmer.BandFor(43.5f, 1000f);

            Assert.False(HorizonShimmer.TryRows(viewProjection, lens, 1000f, MathF.Tan(band.Bottom - HorizonShimmer.UNDER), MathF.Tan(band.Top),
                1080, 4, out _, out _));
        }

        /// <summary>
        /// High and Ultra draw it and nothing below them does: Medium is the weak machine's rung, and Potato has no outback
        /// to bend (docs/scenes/outback.md, "The heat shimmer").
        /// </summary>
        [Theory]
        [InlineData(QualityLevel.Potato, false)]
        [InlineData(QualityLevel.Low, false)]
        [InlineData(QualityLevel.Medium, false)]
        [InlineData(QualityLevel.High, true)]
        [InlineData(QualityLevel.Ultra, true)]
        public void OnlyHighAndUltraShimmer(QualityLevel quality, bool shimmers) =>
            Assert.Equal(shimmers, QualityPreset.Presets[(int)quality].HorizonShimmer);
    }
}
