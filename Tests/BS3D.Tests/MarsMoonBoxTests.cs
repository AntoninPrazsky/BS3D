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
    /// Mars's moons scissored to their boxes (#1138). The pass was one full-screen quad, and a full-screen pass costs what its
    /// pixels cost to raster and start a program on, whatever the program does; scissored to each moon's box it touches the
    /// moons' pixels and nothing else. The picture stays the full screen's only if every pixel the box leaves out is one the
    /// program writes nothing to, which is a pixel whose ray <c>MoonDisc</c>'s cone test turns away. These hold that with no
    /// device: the GPU's own rays and footprints, worked out per pixel as the shader does, against
    /// <see cref="SkyRay.PixelsWithin"/>; the footprint's bound; and the cone's figure, one in the shader and the backdrop.
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
            float footprint = SkyRay.MaxPixelAngle(projection, viewport);
            Rectangle box = SkyRay.PixelsWithin(direction, angularRadius * MarsBackdrop.MOON_CONE_SCALE + footprint, view, projection,
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
        /// A moon behind the lens has no pixels; one whose cone reaches round beside the lens (where its outline on the screen
        /// has no bound) gets the whole viewport; one off the frame none.
        /// </summary>
        [Fact]
        public void BehindBesideAndOffTheFrame()
        {
            Viewport viewport = new(0, 0, 320, 180);
            (Matrix view, Matrix projection) = Lens(Phobos, 60f, 16f / 9f);
            float radius = MathHelper.ToRadians(Moons.PhobosAngularRadiusDegrees) * MarsBackdrop.MOON_CONE_SCALE;

            Assert.Equal(Rectangle.Empty, SkyRay.PixelsWithin(-Phobos, radius, view, projection, viewport));

            //Square to the lens's axis, the cone straddles the plane the screen cannot reach
            Vector3 beside = Vector3.Normalize(Vector3.Cross(Phobos, Vector3.Up));
            Assert.Equal(new Rectangle(0, 0, 320, 180), SkyRay.PixelsWithin(beside, radius, view, projection, viewport));

            //Forty degrees round from the centre of a 60-degree lens, past its side edge
            Vector3 off = Vector3.Transform(Phobos, Matrix.CreateRotationY(MathHelper.ToRadians(70f)));
            Assert.Equal(Rectangle.Empty, SkyRay.PixelsWithin(off, radius, view, projection, viewport));
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
