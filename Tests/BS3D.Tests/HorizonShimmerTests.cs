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

        //The heights over the plain the Game's lenses fly at: the approach (2.4 over the ground), the play camera (6.1), 20
        //and 40, which the intro's crane climbs through (from 4 to some 60), 40 inside its wide shot's 30 to 52 as well, and
        //a lens far higher than any of them
        private static readonly float[] LensHeights = { 0.1f, 2.4f, 6.1f, 20f, 40f, 120f };

        private static float Degrees(float radians) => MathHelper.ToDegrees(radians);

        /// <summary>
        /// The strip is the skyline's: its axis is the plain where its last fade begins, the line the eye reads as the horizon,
        /// it reaches no more than two degrees over it, and its bottom is the plain at the ring (<see cref="HorizonShimmer.RING_SHARE"/>
        /// of the way out). So the plain the strip may touch lies past where every other ground's last fade begins, whatever
        /// the lens's height: nothing over the middle plain. The floor a read downward is held at lies between the bottom and
        /// the axis, so no read lands on the plain nearer than the ring.
        /// </summary>
        [Fact]
        public void TheBandIsAStripAtTheSkyline()
        {
            OutbackAirConfig air = new OutbackSceneConfig().Air;

            float ring = HorizonShimmer.RingRadius(air.HorizonHazeDistance);
            Assert.True(ring >= FarField.FAR_FADE_START_LATEST && ring < air.HorizonHazeDistance,
                $"the ring stands at {ring}, not between {FarField.FAR_FADE_START_LATEST} and the fade's start at {air.HorizonHazeDistance}");

            foreach (float height in LensHeights)
            {
                HorizonShimmer.Elevations band = HorizonShimmer.BandFor(height, air.HorizonHazeDistance);
                float lens = MathF.Max(height, HorizonShimmer.MIN_LENS_HEIGHT);

                Assert.True(band.Bottom < band.Axis && band.Axis < band.Top, $"at {height} the band is {band}");
                float floor = HorizonShimmer.Floor(band);
                Assert.True(band.Bottom < floor && floor < band.Axis, $"at {height} the floor {Degrees(floor):F3} is not between the band's bottom and its axis ({band})");
                Assert.True(Degrees(band.Top - band.Axis) <= 2f, $"at {height} the strip reaches {Degrees(band.Top - band.Axis):F2} degrees over the skyline");

                //The plain the band's bottom and axis look at, along a flat plain from the lens
                float bottomGround = lens / MathF.Tan(-band.Bottom);
                float axisGround = lens / MathF.Tan(-band.Axis);
                Assert.True(bottomGround >= FarField.FAR_FADE_START_LATEST, $"at {height} the strip's bottom looks at the plain {bottomGround:F0} out");
                Assert.InRange(bottomGround, ring - 0.5f, ring + 0.5f);
                Assert.InRange(axisGround, air.HorizonHazeDistance - 0.5f, air.HorizonHazeDistance + 0.5f);

                //And it is a strip, a degree or two either side of the skyline, at every height the Game's lenses fly at
                if (height <= 40f) Assert.True(Degrees(band.Axis - band.Bottom) <= 2f, $"at {height} the strip reaches {Degrees(band.Axis - band.Bottom):F2} degrees under the skyline");
            }

            //A fade begun past its own end is held to it, and the band does not turn over
            HorizonShimmer.Elevations turned = HorizonShimmer.BandFor(6.1f, FarField.FAR_FADE_END * 2f);
            Assert.True(turned.Bottom < turned.Axis, "a fade start past the fade's end turned the band over");
        }

        //The heights over the plain the intro's wide shot flies between (OutbackIntroShots' WIDE_FROM_HEIGHT and WIDE_TO_HEIGHT)
        private const float WIDE_SHOT_LOWEST = 30f, WIDE_SHOT_HIGHEST = 52f;

        //The waver the wide shot's first frame keeps (30 over the plain), which showed no teeth along the skyline: the
        //first shimmer at its whole 0.07 degrees showed a row of them from the wide shot
        private const float WIDE_SHOT_WAVER_DEGREES = 0.062f;

        /// <summary>
        /// It is the grazing rays' (<see cref="HorizonShimmer.GRAZE"/>): whole from the lenses low over the plain (the approach
        /// at 2.4, the play camera at 6.1), weaker the steeper a lens looks down at the skyline, and never stronger as a lens
        /// climbs. The host states the waver at the amplitude times that share and the mirage at the share squared, every
        /// frame. From every height of the intro's wide shot (30 to 52 over the plain) the waver keeps no more than its
        /// first frame's 0.061 degrees, which shows no row of dark teeth along the skyline, and the mirage under a twentieth
        /// of its strength. The crane climbs through every strength, the rock filling most of its frame, so no bound is its.
        /// </summary>
        [Fact]
        public void ItIsTheGrazingRays()
        {
            float fade = new OutbackSceneConfig().Air.HorizonHazeDistance;
            float Grazing(float height) => HorizonShimmer.Grazing(HorizonShimmer.BandFor(height, fade));

            Assert.Equal(1f, Grazing(2.4f));
            Assert.Equal(1f, Grazing(6.1f));

            string shimmer = CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "HorizonShimmer.cs"));
            string draw = shimmer.Substring(shimmer.IndexOf("public void Draw(", StringComparison.Ordinal));
            Assert.Matches(@"float\s+grazing\s*=\s*Grazing\(\s*band\s*\)\s*;", draw);
            Assert.Matches(@"_amplitude\.SetValue\(\s*AMPLITUDE\s*\*\s*grazing\s*\)\s*;", draw);
            Assert.Matches(@"_mirage\.SetValue\(\s*new\s+Vector2\(\s*MIRAGE_DEPTH\s*,\s*MIRAGE_STRENGTH\s*\*\s*grazing\s*\*\s*grazing\s*\)\s*\)\s*;", draw);

            for (float height = WIDE_SHOT_LOWEST; height <= WIDE_SHOT_HIGHEST; height += 0.5f)
            {
                float waver = Degrees(HorizonShimmer.AMPLITUDE * Grazing(height));
                float mirage = HorizonShimmer.MIRAGE_STRENGTH * Grazing(height) * Grazing(height);

                Assert.True(waver <= WIDE_SHOT_WAVER_DEGREES, $"from {height} over the plain the waver reaches {waver:F3} degrees");
                Assert.True(mirage < 0.05f, $"from {height} over the plain the mirage keeps {mirage:F3}");
            }

            for (float height = 0.5f; height < 200f; height += 0.5f)
                Assert.True(Grazing(height + 0.5f) <= Grazing(height), $"the shimmer grows as the lens climbs past {height}");
        }

        /// <summary>
        /// The outback hands the shimmer the plain's own figures: the distance where its last fade begins and the plain's
        /// height, and the host calls it after the sky and before the island's colour in both the Game and the Testbed.
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
        /// The offset is an angle: the figures are radians of a few pixels through the play camera's lens at 1080 rows (about
        /// a pixel is too small for a player to notice), the shader adds the offset to an elevation and projects the
        /// direction back through the camera, and the copy's size in texels reaches nothing but the pixel's own texel. The
        /// mirage's axis wavers by the boil's share alone, so the sheet holds the horizon while the swell lifts and sinks the
        /// skyline over it. The rows the copy is padded by follow from the angle: twice the rows, twice the pad.
        /// </summary>
        [Fact]
        public void TheOffsetIsAnAngle()
        {
            //A pixel's angle through the Game's play lens (GameplayScreen.GAME_FOV, pi / 4.2) at 1080 rows
            float pixel = MathF.PI / 4.2f / 1080f;
            Assert.InRange(HorizonShimmer.AMPLITUDE / pixel, 5f, 8f);
            Assert.InRange(HorizonShimmer.MIRAGE_DEPTH / pixel, 3f, 6f);

            string code = Shader();

            //The waver and the mirror move an elevation, by the amplitude (radians) and by the axis's own wobble
            Assert.Matches(@"float\s+read\s*=\s*max\(\s*elevation\s*\+\s*Amplitude\s*\*", code);
            Assert.Matches(@"GrabUv\(\s*heading\s*,\s*read\s*\)", code);
            Assert.Matches(@"float\s+axis\s*=\s*Band\.y\s*\+\s*Amplitude\s*\*\s*\(1\.0\s*-\s*SWELL_SHARE\)\s*\*\s*Boil\(heading,\s*0\.0\)\s*;", code);
            Assert.Contains("mul(float4(CameraPosition + direction * PROJECTED_DISTANCE, 1.0), ViewProjection)", Body(code, "float2 GrabUv("));

            //GrabSize finds the pixel's own texel and nothing else: once in its declaration and twice in the centre's line
            Assert.Equal(3, Regex.Matches(code, @"\bGrabSize\b").Count);
            Assert.Matches(@"float2\s+centre\s*=\s*\(floor\(uv\s*\*\s*GrabSize\)\s*\+\s*0\.5\)\s*/\s*GrabSize\s*;", code);
            Assert.DoesNotMatch(@"\bddx\b|\bddy\b|\bfwidth\b|VPOS", code);

            Matrix lens = Matrix.CreatePerspectiveFieldOfView(MathF.PI / 4.2f, 2.4f, 0.01f, 2000f);
            int pad1080 = HorizonShimmer.PadRows(lens, 1080) - 2, pad2160 = HorizonShimmer.PadRows(lens, 2160) - 2;
            Assert.InRange(pad2160, pad1080 * 2 - 1, pad1080 * 2 + 1);
        }

        //A static const float of the shader, by name
        private static float ShaderConstant(string code, string name)
        {
            Match match = Regex.Match(code, $@"static\s+const\s+float\s+{name}\s*=\s*([0-9.]+)\s*;");
            Assert.True(match.Success, $"HorizonShimmer.fx has no static const float {name}");
            return float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The skyline boils, it does not tear: each layer of the field moves a read by less than its own coarsest cells are
        /// tall. That alone does not keep rows in order, and this test does not claim it: the swell, which carries most of the
        /// offset, reads none out of order (its cells are 0.95 degrees tall), but the boil's finer octave has cells shorter
        /// than the boil's share, and in a numeric port of the shader at the play camera it reads rows up to about two pixels
        /// back at 1080 rows, the flicker in the boil. The boil alone at three times the first amplitude read them up to seven
        /// back, the formations' feet torn into horizontal slivers; hence the swell's share.
        /// </summary>
        [Fact]
        public void EachLayerMovesLessThanItsCells()
        {
            string code = Shader();
            float swellShare = ShaderConstant(code, "SWELL_SHARE");
            float swellCell = 1f / ShaderConstant(code, "SWELL_UP");
            float boilCell = 1f / ShaderConstant(code, "UP");

            Assert.True(HorizonShimmer.AMPLITUDE * swellShare < swellCell,
                $"the swell moves a read {Degrees(HorizonShimmer.AMPLITUDE * swellShare):F3} degrees, its cells are {Degrees(swellCell):F3} tall");
            Assert.True(HorizonShimmer.AMPLITUDE * (1f - swellShare) < boilCell,
                $"the boil moves a read {Degrees(HorizonShimmer.AMPLITUDE * (1f - swellShare)):F3} degrees, its cells are {Degrees(boilCell):F3} tall");

            //And the waver is the two layers mixed by that share
            Assert.Matches(@"float\s+wobble\s*=\s*lerp\(\s*Boil\(heading,\s*elevation\s*-\s*Band\.y\)\s*,\s*Swell\(heading,\s*elevation\s*-\s*Band\.y\)\s*,\s*SWELL_SHARE\s*\)\s*;",
                Body(code, "float4 RefractPS("));
        }

        /// <summary>
        /// No read lands on the plain nearer than the ring (which brings nothing back, and the skyline would tear there): a
        /// read is held at the floor the host computes (<see cref="HorizonShimmer.Floor"/>, the band's fourth figure), and the
        /// floor never moves a read where nothing moves, since a pixel under the floor is held at its own elevation. A read
        /// downward keeps its whole offset down to that floor, and only a read upward grows from nothing at the band's
        /// bottom: a downward read ramped in over the plain under the axis, thinner than the offset, read the rows nearer the
        /// horizon from lower down than the rows over them, and the skyline came apart into lumps outlined by rings. The
        /// mirage fades out before the floor, so it never ends on the ring's lower edge.
        /// </summary>
        [Fact]
        public void NoReadFallsNearerThanTheRing()
        {
            string shimmer = CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "Scenes", "HorizonShimmer.cs"));
            Assert.Matches(@"_band\.SetValue\(\s*new\s+Vector4\(\s*band\.Bottom\s*,\s*band\.Axis\s*,\s*band\.Top\s*,\s*Floor\(band\)\s*\)\s*\)", shimmer);

            string refract = Body(Shader(), "float4 RefractPS(");
            Assert.Matches(@"float\s+rise\s*=\s*wobble\s*<\s*0\.0\s*\?\s*1\.0\s*:\s*smoothstep\(\s*Band\.x\s*,\s*Band\.y\s*,\s*elevation\s*\)\s*;", refract);
            Assert.Matches(@"float\s+reach\s*=\s*rise\s*\*", refract);
            Assert.Matches(@"float\s+read\s*=\s*max\([^;]*,\s*min\(\s*elevation\s*,\s*Band\.w\s*\)\s*\)\s*;", refract);
            Assert.Matches(@"float\s+sheet\s*=[^;]*\*\s*smoothstep\(\s*Band\.x\s*,\s*Band\.w\s*,\s*elevation\s*\)\s*;", refract);
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

            //The boil's two octaves and the swell slide with the clock, at their tempos, cells a second
            string code = Shader();
            Assert.Equal(2, Regex.Matches(Body(code, "float Boil("), @"\bTime\s*\*\s*(FINE_)?TEMPO\b").Count);
            Assert.Single(Regex.Matches(Body(code, "float Swell("), @"\bTime\s*\*\s*SWELL_TEMPO\b"));
            Assert.Equal(4, Regex.Matches(code, @"\bTime\b").Count); //Its declaration and those three
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
            Assert.Matches(@"float4\s+FarShare\(float4\s+scene\)\s*\{\s*return\s+float4\(scene\.rgb,\s*1\.0\)\s*\*\s*saturate\(1\.0\s*-\s*scene\.a\)\s*;\s*\}", tonemap);

            //Every tap a point tap weighed by its own mark before anything averages it: the hardware's bilinear filter would
            //average the colour and the mark first, and a near sample's colour would land in the far share. The box's taps
            //(one, in its loop) and the hand-built bilinear's four on a target no whole multiple of the copy
            string far = tonemap.Substring(tonemap.IndexOf("float4 SampleSceneFar(", StringComparison.Ordinal));
            far = far.Substring(0, far.IndexOf("\n}", StringComparison.Ordinal));
            Assert.DoesNotContain("SceneSamplerLinear", far);
            Assert.Equal(5, Regex.Matches(far, @"tex2Dlod\(").Count);
            Assert.Equal(5, Regex.Matches(far, @"FarShare\(tex2Dlod\(SceneSampler,").Count);
            Assert.Matches(@"\*\s*\(\(1\.0\s*-\s*f\.x\)\s*\*\s*\(1\.0\s*-\s*f\.y\)\)", far);
            Assert.Matches(@"\*\s*\(f\.x\s*\*\s*\(1\.0\s*-\s*f\.y\)\)", far);
            Assert.Matches(@"\*\s*\(\(1\.0\s*-\s*f\.x\)\s*\*\s*f\.y\)", far);
            Assert.Matches(@"\*\s*\(f\.x\s*\*\s*f\.y\)", far);

            string refract = Body(Shader(), "float4 RefractPS(");
            Assert.Matches(@"color\s*=\s*color\s*\*\s*\(1\.0\s*-\s*shifted\.a\)\s*\+\s*shifted\.rgb\s*;", refract);
            Assert.Matches(@"image\s*=\s*color\s*\*\s*\(1\.0\s*-\s*mirrored\.a\)\s*\+\s*mirrored\.rgb\s*;", refract);
            Assert.Matches(@"float3\s+color\s*=\s*own\.rgb\s*/\s*max\(own\.a", refract);
        }

        /// <summary>
        /// The copy is scissored to the rows the ring covers, so it must hold every pixel the ring draws: from lenses at the
        /// heights the Game flies, turned every way, pitched up and down and rolled a little as the shake rolls it, through
        /// narrow and wide lenses, every point of the band that lands on the screen lies inside the rows. And the rows are
        /// the band's, not the frame's: what the ring reaches off the screen's sides does not widen them.
        /// </summary>
        [Fact]
        public void TheRowsHoldTheRing()
        {
            const int height = 1080;
            float radius = HorizonShimmer.RingRadius(1000f);
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

                HorizonShimmer.Elevations band = HorizonShimmer.BandFor(lens.Y + 13.5f, 1000f);
                float tanBottom = MathF.Tan(band.Bottom - HorizonShimmer.UNDER), tanTop = MathF.Tan(band.Top);

                bool any = HorizonShimmer.TryRows(viewProjection, lens, radius, tanBottom, tanTop, height, 0, out int first, out int count);
                float seenTop = float.MaxValue, seenBottom = float.MinValue;

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
                        seenTop = MathF.Min(seenTop, row);
                        seenBottom = MathF.Max(seenBottom, row);
                        checkedViews++;
                    }
                }

                //Tight: no more than a few rows past what the band shows (the samples fall a little inside its edges)
                if (seenBottom >= seenTop)
                    Assert.True(count <= seenBottom - seenTop + 0.02f * height + 2f,
                        $"view {view}: {count} rows for a band seen over {seenBottom - seenTop:F1}");
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

            Assert.False(HorizonShimmer.TryRows(viewProjection, lens, HorizonShimmer.RingRadius(1000f), MathF.Tan(band.Bottom - HorizonShimmer.UNDER), MathF.Tan(band.Top),
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

        /// <summary>
        /// The table is not the tier until something carries it: the renderer draws the shimmer until told otherwise, so the
        /// Game seeds it from the starting tier where it builds the renderer and states it again on every change of tier, and
        /// the renderer refuses it on a phone and on a multisampled scene target (whose resolve averages the far mark into
        /// the colour before the copy can weigh them) before the backdrop is asked. And the copy's target is preserved only
        /// where the shimmer may be drawn: a phone keeps the discarded target the glass alone had.
        /// </summary>
        [Fact]
        public void TheTierReachesTheRenderer()
        {
            Assert.Matches(@"_sceneRenderer\.HorizonShimmer\s*=\s*preset\.HorizonShimmer\s*;", CodeOf(Path.Combine("Game", "BS3DGame.Quality.cs")));

            string game = CodeOf(Path.Combine("Game", "BS3DGame.cs"));
            int built = game.IndexOf("new SceneRenderer(", StringComparison.Ordinal);
            Assert.True(built >= 0, "BS3DGame.cs builds no SceneRenderer");
            string initializer = game.Substring(built, game.IndexOf("};", built, StringComparison.Ordinal) - built);
            Assert.Matches(@"\bHorizonShimmer\s*=\s*QualityPreset\.Presets\[\(int\)_quality\]\.HorizonShimmer\b", initializer);

            string renderer = CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "SceneRenderer.cs"));
            int draw = renderer.IndexOf("public void DrawHorizonRefraction(", StringComparison.Ordinal);
            Assert.True(draw >= 0, "SceneRenderer has no DrawHorizonRefraction");
            int asked = renderer.IndexOf("DrawRefraction(frame, pipeline)", draw, StringComparison.Ordinal);
            Assert.True(asked > draw, "DrawHorizonRefraction never asks the backdrop");
            string guard = renderer.Substring(draw, asked - draw);
            Assert.Matches(@"if\s*\(\s*!HorizonShimmer\s*\|\|\s*!GraphicsSupport\.HorizonShimmer\s*\|\|[^)]*MultiSampleCount\s*>\s*1\s*\)\s*return\s*;", guard);

            Assert.Matches(@"public\s+static\s+bool\s+HorizonShimmer\s*=>\s*!OperatingSystem\.IsAndroid\(\)\s*;",
                CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "GraphicsSupport.cs")));

            //This host is a desktop, where the shimmer may be drawn and so the copy's target is preserved
            Assert.True(GraphicsSupport.HorizonShimmer);
            Assert.Equal(RenderTargetUsage.PreserveContents, PostProcessPipeline.GrabUsage);
            Assert.Matches(@"GrabUsage\s*=>\s*GraphicsSupport\.HorizonShimmer\s*\?\s*RenderTargetUsage\.PreserveContents\s*:\s*RenderTargetUsage\.DiscardContents\s*;",
                CodeOf(Path.Combine("BS3DLibs", "Prazsky.Core", "Render", "PostProcessPipeline.cs")));
        }
    }
}
