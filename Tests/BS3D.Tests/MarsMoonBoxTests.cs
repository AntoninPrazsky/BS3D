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
    /// Mars's moons scissored to their boxes (#1138). The pass was one full-screen quad whose cost sat in covering the frame,
    /// not in its program; scissored to each moon's box it touches the moons' pixels and nothing else. The picture stays the
    /// full screen's only if every pixel the box leaves out is one the program writes nothing to, which is a pixel whose ray
    /// <c>MoonDisc</c>'s cone test turns away. These hold that with no device: the GPU's own rays and footprints, worked out
    /// per pixel as the shader does, against the backdrop's own boxes (<see cref="MarsBackdrop.MoonBox"/>); no box for a moon
    /// off the frame, at any bearing; the footprint's bound; the cone's figure, one in the shader and the backdrop; and the
    /// draws the two boxes take, no pixel blended twice.
    /// </summary>
    public class MarsMoonBoxTests
    {
        private static readonly MarsMoonsConfig Moons = new();

        private static Vector3 Phobos => SceneRenderer.DirectionFromElevationAzimuth(Moons.PhobosElevation, Moons.PhobosAzimuth);
        private static Vector3 Deimos => SceneRenderer.DirectionFromElevationAzimuth(Moons.DeimosElevation, Moons.DeimosAzimuth);

        //A lens at the arena's edge looking along `look`, `fovDegrees` up and down, `turnDegrees` round the vertical off it
        private static (Matrix View, Matrix Projection) Lens(Vector3 look, float fovDegrees, float aspect, float turnDegrees = 0f)
        {
            Vector3 eye = new(0f, 0f, 30f);
            Vector3 along = Vector3.Transform(look, Matrix.CreateRotationY(MathHelper.ToRadians(turnDegrees)));
            return (Matrix.CreateLookAt(eye, eye + along * 100f, Vector3.Up),
                Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fovDegrees), aspect, 0.05f, 2000f));
        }

        //The normalised ray at the centre of the pixel (px, py) of the target, as the moons' vertex program and the
        //interpolator give it: the quad's corners through the basis, linear across the frame (w = 1 at every corner)
        private static Vector3 RayAt(Matrix basis, Viewport viewport, float px, float py)
        {
            float x = (px + 0.5f - viewport.X) / viewport.Width * 2f - 1f;
            float y = 1f - (py + 0.5f - viewport.Y) / viewport.Height * 2f;
            Vector3 xAxis = new(basis.M11, basis.M12, basis.M13);
            Vector3 yAxis = new(basis.M21, basis.M22, basis.M23);
            Vector3 zAxis = new(basis.M31, basis.M32, basis.M33);
            return Vector3.Normalize(xAxis * x + yAxis * y + zAxis);
        }

        //The shader's pixelAngle at (px, py), the larger of what a coarse derivative (the 2x2 block's first pixel) and a fine
        //one (the pixel's own row and column) give it: either may be what the GPU takes
        private static float FootprintAt(Matrix basis, Viewport viewport, int px, int py)
        {
            float Frobenius(int x, int y)
            {
                Vector3 here = RayAt(basis, viewport, x, y);
                Vector3 across = RayAt(basis, viewport, x + 1, y) - here;
                Vector3 down = RayAt(basis, viewport, x, y + 1) - here;
                return MathF.Sqrt(across.LengthSquared() + down.LengthSquared());
            }

            return MathF.Max(Frobenius(px & ~1, py & ~1), Frobenius(px, py));
        }

        //Every pixel of the viewport the shader's cone test lets through for a moon (`direction`, `angularRadius`) has to be
        //inside the box; returns how many it let through, the box, and its slack round them, so a test can see it fired and
        //the box bounds anything
        private static (int Inside, Rectangle Box, int Slack) Check(Vector3 direction, float angularRadius, Matrix view, Matrix projection,
            Viewport viewport)
        {
            Matrix basis = SkyRay.Basis(view, projection);
            Rectangle box = MarsBackdrop.MoonBox(direction, angularRadius, SkyRay.MaxPixelAngle(projection, viewport), view, projection,
                viewport);

            int inside = 0, left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
            for (int py = viewport.Y; py < viewport.Y + viewport.Height; py++)
            for (int px = viewport.X; px < viewport.X + viewport.Width; px++)
            {
                Vector3 ray = RayAt(basis, viewport, px, py);
                float gate = MathF.Cos(angularRadius * MarsBackdrop.MOON_CONE_SCALE + FootprintAt(basis, viewport, px, py));
                if (Vector3.Dot(ray, direction) <= gate) continue;

                inside++;
                Assert.True(box.Contains(px, py), $"pixel ({px}, {py}) is in the moon's cone and outside its box {box}");
                left = Math.Min(left, px);
                top = Math.Min(top, py);
                right = Math.Max(right, px);
                bottom = Math.Max(bottom, py);
            }

            int slack = inside == 0 ? box.Width * box.Height
                : Math.Max(Math.Max(left - box.Left, box.Right - 1 - right), Math.Max(top - box.Top, box.Bottom - 1 - bottom));
            return (inside, box, slack);
        }

        public static TheoryData<string, float, float, int, int, int, int> Frames => new()
        {
            //name, field of view, turn off the moon, viewport x, y, width, height
            { "both moons in a wide frame", 72f, 0f, 0, 0, 320, 180 },
            { "phobos filling a narrow lens", 8f, 0f, 0, 0, 320, 180 },
            { "a moon near the frame's side", 60f, 38f, 0, 0, 320, 180 },
            { "a moon half off the frame's edge", 8f, 9.05f, 0, 0, 320, 180 },
            { "an offset viewport", 10f, 3f, 40, 24, 200, 150 },
            { "a very wide lens", 120f, 50f, 0, 0, 240, 135 }
        };

        /// <summary>
        /// Every ray the cone test lets through for either moon lands in the moon's box, from wide and narrow lenses, with the
        /// moon at the centre, near a side, half off an edge and in an offset viewport; and the box is no more than a few pixels
        /// wider than what it holds, or the scissor saves nothing.
        /// </summary>
        [Theory]
        [MemberData(nameof(Frames))]
        public void EveryRayTheConeTestLetsThroughIsInTheMoonsBox(string name, float fovDegrees, float turnDegrees, int x, int y,
            int width, int height)
        {
            Viewport viewport = new(x, y, width, height);
            Vector3 aim = Vector3.Normalize(Phobos + (name.StartsWith("both") ? Deimos : Vector3.Zero));
            (Matrix view, Matrix projection) = Lens(aim, fovDegrees, width / (float)height, turnDegrees);

            //The two moons as shipped, and a cone six degrees across for the box's geometry
            int fired = 0;
            foreach ((Vector3 direction, float radiusDegrees, bool moon) in new[] { (Phobos, Moons.PhobosAngularRadiusDegrees, true),
                         (Deimos, Moons.DeimosAngularRadiusDegrees, true), (Phobos, 6f, false) })
            {
                (int inside, Rectangle box, int slack) = Check(direction, MathHelper.ToRadians(radiusDegrees), view, projection, viewport);
                if (inside == 0) continue;
                if (moon) fired++;

                //Rounding outwards, the margin, and the polygon's corners standing up to 2 % past the cone's section
                int allowed = 3 + SkyRay.MARGIN_PIXELS + (int)MathF.Ceiling(0.025f * Math.Max(box.Width, box.Height));
                Assert.True(slack <= allowed, $"{name}: the box {box} stands {slack} px off the pixels it holds ({allowed} allowed)");
            }

            Assert.True(fired > 0, $"{name}: neither moon was in the frame, so nothing was checked");
        }

        /// <summary>
        /// The bound <see cref="SkyRay.MaxPixelAngle"/> gives is at least every pixel's footprint, worked out as the shader
        /// works it out, and is reached at the centre of the frame (a bound far above it would widen every box for nothing).
        /// </summary>
        [Theory]
        [InlineData(72f, 320, 180)]
        [InlineData(8f, 320, 180)]
        [InlineData(120f, 240, 135)]
        [InlineData(40f, 180, 320)]
        public void NoPixelsFootprintIsLargerThanTheBound(float fovDegrees, int width, int height)
        {
            Viewport viewport = new(0, 0, width, height);
            (Matrix view, Matrix projection) = Lens(Phobos, fovDegrees, width / (float)height);
            Matrix basis = SkyRay.Basis(view, projection);
            float bound = SkyRay.MaxPixelAngle(projection, viewport);

            float largest = 0f;
            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
                largest = MathF.Max(largest, FootprintAt(basis, viewport, px, py));

            Assert.True(largest <= bound * 1.0001f, $"a pixel's footprint {largest} is over the bound {bound}");
            Assert.True(largest >= bound * 0.99f, $"the bound {bound} is far over the largest footprint {largest}");
        }

        /// <summary>
        /// A moon behind the lens has no pixels, nor one off the frame, nor one square to the lens's axis, whose cone straddles
        /// the plane through the eye where its outline on the screen has no bound; a cone that straddles that plane and
        /// reaches into the frame gets a box round what it reaches, not the whole viewport.
        /// </summary>
        [Fact]
        public void BehindBesideAndOffTheFrame()
        {
            Viewport viewport = new(0, 0, 320, 180);
            (Matrix view, Matrix projection) = Lens(Phobos, 60f, 16f / 9f);
            float radius = MathHelper.ToRadians(Moons.PhobosAngularRadiusDegrees);
            float footprint = SkyRay.MaxPixelAngle(projection, viewport);

            Assert.Equal(Rectangle.Empty, MarsBackdrop.MoonBox(-Phobos, radius, footprint, view, projection, viewport));

            //Square to the lens's axis, the cone straddles the plane the screen cannot reach
            Vector3 beside = Vector3.Normalize(Vector3.Cross(Phobos, Vector3.Up));
            Assert.Equal(Rectangle.Empty, MarsBackdrop.MoonBox(beside, radius, footprint, view, projection, viewport));

            //Round the lens's own up axis: this lens's side edges stand 45.7 degrees off its axis
            Vector3 Round(float degrees) =>
                Phobos * MathF.Cos(MathHelper.ToRadians(degrees)) + beside * MathF.Sin(MathHelper.ToRadians(degrees));

            //Fifty-four degrees round, past the side edge
            Assert.Equal(Rectangle.Empty, MarsBackdrop.MoonBox(Round(54f), radius, footprint, view, projection, viewport));

            //A cone forty degrees round, its axis eighty off the lens's: it straddles the eye's plane and reaches past the side
            //edge into a strip of the frame six degrees deep
            (int inside, Rectangle box, _) = Check(Round(80f), MathHelper.ToRadians(40f) / MarsBackdrop.MOON_CONE_SCALE, view, projection,
                viewport);
            Assert.True(inside > 0, "the wide cone reached no pixel, so nothing was checked");
            Assert.True(box.Width < viewport.Width / 4, $"the wide cone's box {box} is not the strip it reaches");
        }

        /// <summary>
        /// Round a whole turn of the Game's lens (<c>GameplayScreen.GAME_FOV</c>, a window 2.56 times as wide as it is tall),
        /// pitched from below the horizon to well above it: every pixel of either moon's cone is in its box, and a moon whose
        /// cone reaches no pixel gets no box, or a strip at most a few pixels deep along the frame's edge, never the frame.
        /// </summary>
        [Fact]
        public void NoMoonOffTheFrameGetsABoxAtAnyBearing()
        {
            Viewport viewport = new(0, 0, 256, 100);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.Pi / 4.2f, viewport.Width / (float)viewport.Height, 0.05f, 2000f);
            float footprint = SkyRay.MaxPixelAngle(projection, viewport);
            int allowed = 3 + SkyRay.MARGIN_PIXELS;

            int inFrame = 0, offFrame = 0;
            foreach (float pitch in new[] { -30f, -20f, -10f, 0f, 20f, 40f })
            for (int bearing = 0; bearing < 360; bearing += 2)
            {
                Matrix turn = Matrix.CreateRotationX(MathHelper.ToRadians(pitch)) * Matrix.CreateRotationY(MathHelper.ToRadians(bearing));
                Vector3 look = Vector3.Transform(Vector3.Forward, turn);
                Matrix view = Matrix.CreateLookAt(Vector3.Zero, look, Vector3.Up);
                Matrix rays = SkyRay.Basis(view, projection);

                foreach ((Vector3 direction, float radiusDegrees) in new[] { (Phobos, Moons.PhobosAngularRadiusDegrees),
                             (Deimos, Moons.DeimosAngularRadiusDegrees) })
                {
                    float cone = MarsBackdrop.MoonCone(MathHelper.ToRadians(radiusDegrees), footprint);
                    Rectangle box = MarsBackdrop.MoonBox(direction, MathHelper.ToRadians(radiusDegrees), footprint, view, projection, viewport);

                    //The cone the box is taken round, which holds the shader's at every pixel (TheBoxsConeHoldsTheShadersAtEveryPixel)
                    int inside = 0;
                    float gate = MathF.Cos(cone);
                    for (int py = 0; py < viewport.Height; py++)
                    for (int px = 0; px < viewport.Width; px++)
                    {
                        if (Vector3.Dot(RayAt(rays, viewport, px, py), direction) <= gate) continue;
                        inside++;
                        Assert.True(box.Contains(px, py), $"pitch {pitch}, bearing {bearing}: pixel ({px}, {py}) is outside the box {box}");
                    }

                    if (inside > 0)
                    {
                        inFrame++;
                        continue;
                    }

                    offFrame++;
                    Assert.True(Math.Min(box.Width, box.Height) <= allowed,
                        $"pitch {pitch}, bearing {bearing}: a moon off the frame got the box {box}");
                }
            }

            Assert.True(inFrame > 0 && offFrame > 0, $"the sweep saw {inFrame} moons in the frame and {offFrame} off it");
        }

        /// <summary>
        /// The cone a box is taken round (<see cref="MarsBackdrop.MoonCone"/>) holds the cone the shader tests at every pixel
        /// (<c>MOON_CONE_SCALE</c> of the radius and that pixel's own footprint, worked out as the shader works it out), and is
        /// that cone at the centre of the frame, where a pixel's footprint is the largest; a moon of no size has no box.
        /// </summary>
        [Theory]
        [InlineData(72f, 320, 180)]
        [InlineData(8f, 320, 180)]
        [InlineData(120f, 240, 135)]
        public void TheBoxsConeHoldsTheShadersAtEveryPixel(float fovDegrees, int width, int height)
        {
            Viewport viewport = new(0, 0, width, height);
            (Matrix view, Matrix projection) = Lens(Phobos, fovDegrees, width / (float)height);
            Matrix basis = SkyRay.Basis(view, projection);
            float footprint = SkyRay.MaxPixelAngle(projection, viewport);

            foreach (float radiusDegrees in new[] { Moons.PhobosAngularRadiusDegrees, Moons.DeimosAngularRadiusDegrees })
            {
                float radius = MathHelper.ToRadians(radiusDegrees);
                float cone = MarsBackdrop.MoonCone(radius, footprint);
                float widest = 0f;
                for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                    widest = MathF.Max(widest, radius * MarsBackdrop.MOON_CONE_SCALE + FootprintAt(basis, viewport, px, py));

                Assert.True(cone >= widest - 1e-4f * footprint, $"the box's cone {cone} is narrower than the shader's {widest}");
                Assert.True(cone <= widest + 0.01f * footprint, $"the box's cone {cone} is far wider than the shader's {widest}");
            }

            Assert.Equal(Rectangle.Empty, MarsBackdrop.MoonBox(Phobos, 0f, footprint, view, projection, viewport));
        }

        /// <summary>
        /// The two boxes' draws blend every pixel once: two boxes that share a pixel are drawn as one over both, two that only
        /// meet along an edge (<see cref="Rectangle.Intersects(Rectangle)"/> is strict) or stand apart each as its own, and a box with no
        /// area is not drawn.
        /// </summary>
        [Fact]
        public void TheTwoBoxesAreDrawnSoNoPixelIsBlendedTwice()
        {
            Rectangle a = new(10, 10, 20, 20);

            Assert.Equal((new Rectangle(10, 10, 30, 25), Rectangle.Empty), MarsBackdrop.MoonDraws(a, new Rectangle(25, 15, 15, 20)));
            Assert.Equal((new Rectangle(5, 10, 25, 20), Rectangle.Empty), MarsBackdrop.MoonDraws(a, new Rectangle(5, 12, 10, 3)));

            Rectangle beside = new(30, 10, 20, 20);
            Assert.Equal((a, beside), MarsBackdrop.MoonDraws(a, beside));
            Assert.Equal((a, new Rectangle(100, 50, 4, 4)), MarsBackdrop.MoonDraws(a, new Rectangle(100, 50, 4, 4)));

            Assert.Equal((a, Rectangle.Empty), MarsBackdrop.MoonDraws(a, Rectangle.Empty));
            Assert.Equal((Rectangle.Empty, a), MarsBackdrop.MoonDraws(Rectangle.Empty, a));
            Assert.Equal((Rectangle.Empty, a), MarsBackdrop.MoonDraws(new Rectangle(15, 15, 0, 10), a));
            Assert.Equal((Rectangle.Empty, Rectangle.Empty), MarsBackdrop.MoonDraws(Rectangle.Empty, new Rectangle(3, 3, 10, 0)));
        }

        /// <summary>
        /// The cone the backdrop scissors with is the one the shader tests: one figure, <c>MOON_CONE_SCALE</c>, in Mars.fx and
        /// <see cref="MarsBackdrop.MOON_CONE_SCALE"/>, and MoonDisc's test reads it with the pixel's footprint added.
        /// </summary>
        [Fact]
        public void TheShadersConeIsTheBackdrops()
        {
            string code = Regex.Replace(File.ReadAllText(FindFile(Path.Combine("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Mars.fx"))),
                @"//[^\n]*", string.Empty);

            Match constant = Regex.Match(code, @"static\s+const\s+float\s+MOON_CONE_SCALE\s*=\s*([0-9.]+)\s*;");
            Assert.True(constant.Success, "Mars.fx names no MOON_CONE_SCALE");
            Assert.Equal(MarsBackdrop.MOON_CONE_SCALE, float.Parse(constant.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Matches(@"dot\(dir,\s*moonDirection\)\s*>\s*cos\(angularRadius\s*\*\s*MOON_CONE_SCALE\s*\+\s*pixelAngle\)", code);
        }

        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }
    }
}
