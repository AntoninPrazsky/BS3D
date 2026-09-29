using System;
using System.IO;
using System.IO.Compression;

namespace BS3D.Tools.LatticeProbe
{
    /// <summary>
    /// A grey image as the probe sees it: gamma-encoded luminance in 0-255, row-major. What a player's eye is
    /// given is the encoded value, and a lattice is a property of what is on the screen, so no linearisation is done.
    /// </summary>
    internal sealed class LumaImage
    {
        public LumaImage(int width, int height, float[] luma)
        {
            Width = width;
            Height = height;
            Luma = luma;
        }

        public int Width { get; }
        public int Height { get; }
        public float[] Luma { get; }
    }

    /// <summary>
    /// The PNG reader for the game's own screenshots (<c>ScreenshotWriter</c>, 8-bit RGB or RGBA, never interlaced) and
    /// nothing else. Written out rather than taken from a library so the tool stays plain <c>net10.0</c> with no package,
    /// like <c>ScoreSim</c> and <c>DocDrift</c>; it refuses, by name, every variant it does not read.
    /// </summary>
    internal static class PngReader
    {
        private static readonly byte[] SIGNATURE = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static LumaImage Load(string path)
        {
            byte[] file = File.ReadAllBytes(path);

            for (int i = 0; i < SIGNATURE.Length; i++)
                if (i >= file.Length || file[i] != SIGNATURE[i]) throw new InvalidDataException($"'{path}' is not a PNG");

            int width = 0, height = 0, channels = 0;
            using var idat = new MemoryStream();

            int position = SIGNATURE.Length;
            while (position + 8 <= file.Length)
            {
                int length = ReadInt(file, position);
                string type = System.Text.Encoding.ASCII.GetString(file, position + 4, 4);
                int data = position + 8;

                if (type == "IHDR")
                {
                    width = ReadInt(file, data);
                    height = ReadInt(file, data + 4);
                    int depth = file[data + 8], colour = file[data + 9], interlace = file[data + 12];

                    if (depth != 8) throw new NotSupportedException($"'{path}': bit depth {depth} (only 8 is read)");
                    if (interlace != 0) throw new NotSupportedException($"'{path}': interlaced PNG");

                    channels = colour switch
                    {
                        0 => 1,
                        2 => 3,
                        6 => 4,
                        _ => throw new NotSupportedException($"'{path}': colour type {colour} (only grey, RGB and RGBA are read)"),
                    };
                }
                else if (type == "IDAT") idat.Write(file, data, length);
                else if (type == "IEND") break;

                position = data + length + 4;
            }

            if (width <= 0 || height <= 0) throw new InvalidDataException($"'{path}' has no header");

            idat.Position = 2; //skip the zlib header; DeflateStream reads the raw stream
            using var inflate = new DeflateStream(idat, CompressionMode.Decompress);

            int stride = width * channels;
            byte[] raw = new byte[(stride + 1) * height];
            int filled = 0;
            while (filled < raw.Length)
            {
                int read = inflate.Read(raw, filled, raw.Length - filled);
                if (read <= 0) throw new InvalidDataException($"'{path}': the pixel data ends early");
                filled += read;
            }

            byte[] pixels = new byte[stride * height];

            for (int y = 0; y < height; y++)
            {
                int filter = raw[y * (stride + 1)];
                int src = y * (stride + 1) + 1, dst = y * stride;

                for (int x = 0; x < stride; x++)
                {
                    int left = x >= channels ? pixels[dst + x - channels] : 0;
                    int up = y > 0 ? pixels[dst - stride + x] : 0;
                    int upLeft = y > 0 && x >= channels ? pixels[dst - stride + x - channels] : 0;

                    int value = raw[src + x];
                    pixels[dst + x] = (byte)(filter switch
                    {
                        0 => value,
                        1 => value + left,
                        2 => value + up,
                        3 => value + ((left + up) >> 1),
                        4 => value + Paeth(left, up, upLeft),
                        _ => throw new InvalidDataException($"'{path}': filter type {filter}"),
                    });
                }
            }

            float[] luma = new float[width * height];
            for (int i = 0; i < luma.Length; i++)
            {
                int p = i * channels;
                luma[i] = channels == 1
                    ? pixels[p]
                    : 0.2126f * pixels[p] + 0.7152f * pixels[p + 1] + 0.0722f * pixels[p + 2];
            }

            return new LumaImage(width, height, luma);
        }

        private static int ReadInt(byte[] data, int at) =>
            (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
