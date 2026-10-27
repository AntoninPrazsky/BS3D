using BS3D.Capture;
using Microsoft.Xna.Framework;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The capture's PNGs (#946): an encoder with no graphics device in it, and the writer that runs it on threads of its own.</summary>
    public class PngEncoderTests
    {
        /// <summary>A PNG read back by an independent reader: its chunks checked against their own CRCs, the Sub filter undone.</summary>
        private static (int Width, int Height, byte[] Rgb) Decode(byte[] png)
        {
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);

            int offset = 8, width = 0, height = 0;
            using MemoryStream idat = new();
            bool ended = false;

            while (offset < png.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
                string type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
                byte[] data = png[(offset + 8)..(offset + 8 + length)];
                uint stored = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length));

                //The CRC of the type and the data, bit by bit: nothing shared with the encoder's table
                uint crc = 0xFFFFFFFFu;
                foreach (byte b in System.Text.Encoding.ASCII.GetBytes(type).Concat(data))
                {
                    crc ^= b;
                    for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }

                Assert.Equal(~crc, stored);

                if (type == "IHDR")
                {
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
                    Assert.Equal(new byte[] { 8, 2, 0, 0, 0 }, data[8..]);
                }
                else if (type == "IDAT") idat.Write(data);
                else if (type == "IEND") ended = true;

                offset += 12 + length;
            }

            Assert.True(ended, "the file ends with IEND");

            idat.Position = 0;
            using ZLibStream inflate = new(idat, CompressionMode.Decompress);
            using MemoryStream raw = new();
            inflate.CopyTo(raw);

            byte[] rows = raw.ToArray();
            int stride = width * 3 + 1;
            Assert.Equal(stride * height, rows.Length);

            byte[] rgb = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                Assert.Equal(1, rows[y * stride]);
                for (int x = 0; x < width; x++)
                    for (int c = 0; c < 3; c++)
                    {
                        byte filtered = rows[y * stride + 1 + x * 3 + c];
                        byte left = x == 0 ? (byte)0 : rgb[(y * width + x - 1) * 3 + c];
                        rgb[(y * width + x) * 3 + c] = (byte)(filtered + left);
                    }
            }

            return (width, height, rgb);
        }

        private static byte[] Pixels(int width, int height, int seed)
        {
            byte[] rgba = new byte[width * height * 4];
            new Random(seed).NextBytes(rgba);

            //A gradient across each row too, which is what the filter is for, and some alpha that is not opaque
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    rgba[(y * width + x) * 4] = (byte)(x * 255 / Math.Max(1, width - 1));

            return rgba;
        }

        private static byte[] Expected(byte[] rgba)
        {
            byte[] rgb = new byte[rgba.Length / 4 * 3];
            for (int i = 0; i < rgba.Length / 4; i++)
            {
                rgb[i * 3] = rgba[i * 4];
                rgb[i * 3 + 1] = rgba[i * 4 + 1];
                rgb[i * 3 + 2] = rgba[i * 4 + 2];
            }

            return rgb;
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(7, 5)]
        [InlineData(64, 36)]
        [InlineData(257, 3)]
        public void AFrameReadsBackAsExactlyThePixelsItWasMadeOfWithoutTheAlpha(int width, int height)
        {
            byte[] rgba = Pixels(width, height, width * 31 + height);

            using MemoryStream file = new();
            new PngEncoder().Encode(rgba, width, height, file);

            (int w, int h, byte[] rgb) = Decode(file.ToArray());
            Assert.Equal(width, w);
            Assert.Equal(height, h);
            Assert.Equal(Expected(rgba), rgb);
        }

        [Fact]
        public void OneEncoderServesFramesOfDifferentSizesOneAfterAnother()
        {
            PngEncoder encoder = new();

            foreach ((int width, int height) in new[] { (32, 20), (8, 8), (40, 30) })
            {
                byte[] rgba = Pixels(width, height, width);
                using MemoryStream file = new();
                encoder.Encode(rgba, width, height, file);

                Assert.Equal(Expected(rgba), Decode(file.ToArray()).Rgb);
            }
        }

        [Fact]
        public void APixelBufferOfTheWrongLengthIsRefused() =>
            Assert.Throws<ArgumentException>(() => new PngEncoder().Encode(new byte[10], 2, 2, new MemoryStream()));

        [Fact]
        public void TheWriterWritesEveryFrameItIsHandedUnderItsNumberAndGivesTheBuffersBack()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bs3d-capture-writer-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                CaptureWriter writer = new(folder, 16, 8, threads: 2);
                Color[][] sent = new Color[7][];

                //More frames than buffers (four), so a frame waits for a buffer to come back: the bound
                for (int n = 1; n <= 7; n++)
                {
                    Color[] pixels = writer.Rent();
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(n * 30, i % 256, (n * 7 + i) % 256, 255);
                    sent[n - 1] = (Color[])pixels.Clone();
                    writer.Submit(pixels, n);
                }

                (int written, int failed, string firstFailure) = writer.Complete();
                Assert.Equal(7, written);
                Assert.Equal(0, failed);
                Assert.Null(firstFailure);

                for (int n = 1; n <= 7; n++)
                {
                    byte[] file = File.ReadAllBytes(Path.Combine(folder, CapturePlan.FrameFileName(n)));
                    (int w, int h, byte[] rgb) = Decode(file);
                    Assert.Equal((16, 8), (w, h));

                    for (int i = 0; i < 16 * 8; i++)
                    {
                        Assert.Equal(sent[n - 1][i].R, rgb[i * 3]);
                        Assert.Equal(sent[n - 1][i].G, rgb[i * 3 + 1]);
                        Assert.Equal(sent[n - 1][i].B, rgb[i * 3 + 2]);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void AFrameThatCannotBeWrittenIsCountedAndTheRestStillAre()
        {
            string folder = Path.Combine(Path.GetTempPath(), "bs3d-capture-writer-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                CaptureWriter writer = new(folder, 4, 4, threads: 1);

                //Frame 1's file name is taken by a folder, which a file cannot be created over
                Directory.CreateDirectory(Path.Combine(folder, CapturePlan.FrameFileName(1)));

                writer.Submit(writer.Rent(), 1);
                writer.Submit(writer.Rent(), 2);

                (int written, int failed, string firstFailure) = writer.Complete();
                Assert.Equal(1, written);
                Assert.Equal(1, failed);
                Assert.StartsWith("frame 1:", firstFailure);
                Assert.True(File.Exists(Path.Combine(folder, CapturePlan.FrameFileName(2))));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
        }
    }
}
