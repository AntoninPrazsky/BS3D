using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace BS3D.Capture
{
    /// <summary>
    /// An 8-bit PNG written from raw pixels on <b>any thread</b> (#946). A capture reads each frame back on the render thread
    /// and encodes it elsewhere, and <c>Texture2D.SaveAsPng</c> needs a texture on the device, which belongs to the render
    /// thread; this is the encoder with nothing of the graphics device in it, so a pool of writer threads can run it beside
    /// the render. Truecolour (RGB, the back buffer's alpha is not a picture's), every row filtered with Sub, which the sky's
    /// and the fog's gradients compress much better with and which costs one subtraction a byte, deflated at the fastest level
    /// (a frame is on a thread of its own and the queue in front of it is bounded, so it is the rate a clip is written at).
    /// <para>
    /// An instance keeps its scratch buffers between frames (a 1080p frame is six megabytes of them, and a new one per frame is
    /// a large-object allocation a frame), so it belongs to one thread at a time.
    /// </para>
    /// </summary>
    internal sealed class PngEncoder
    {
        private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly uint[] CrcTable = BuildCrcTable();

        private byte[] _rows = [];
        private readonly MemoryStream _deflated = new();

        /// <summary>
        /// Writes <paramref name="rgba"/> (four bytes a pixel, red first, rows top to bottom: the layout of a <c>Color[]</c>) as a
        /// PNG to <paramref name="output"/>.
        /// </summary>
        internal void Encode(ReadOnlySpan<byte> rgba, int width, int height, Stream output)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "an image has a size");
            if (rgba.Length != width * height * 4) throw new ArgumentException($"{rgba.Length} bytes are not {width}x{height} pixels of four bytes", nameof(rgba));

            int stride = width * 3 + 1;
            if (_rows.Length < stride * height) _rows = new byte[stride * height];

            FilterSub(rgba, width, height, _rows);

            _deflated.SetLength(0);
            using (ZLibStream zlib = new(_deflated, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(_rows, 0, stride * height);

            output.Write(Signature);

            Span<byte> header = stackalloc byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, width);
            BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
            header[8] = 8;  //bits a channel
            header[9] = 2;  //truecolour: red, green, blue
            header[10] = 0; //deflate
            header[11] = 0; //adaptive filtering, the one method there is
            header[12] = 0; //no interlace
            WriteChunk(output, "IHDR"u8, header);

            WriteChunk(output, "IDAT"u8, _deflated.GetBuffer().AsSpan(0, (int)_deflated.Length));
            WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        }

        /// <summary>Each row as a filter byte (1, Sub) and its red, green and blue with the pixel before's taken off.</summary>
        private static void FilterSub(ReadOnlySpan<byte> rgba, int width, int height, byte[] rows)
        {
            int stride = width * 3 + 1;

            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<byte> source = rgba.Slice(y * width * 4, width * 4);
                Span<byte> row = rows.AsSpan(y * stride, stride);
                row[0] = 1;

                byte r0 = 0, g0 = 0, b0 = 0;
                for (int x = 0, s = 0, d = 1; x < width; x++, s += 4, d += 3)
                {
                    byte r = source[s], g = source[s + 1], b = source[s + 2];
                    row[d] = (byte)(r - r0);
                    row[d + 1] = (byte)(g - g0);
                    row[d + 2] = (byte)(b - b0);
                    r0 = r;
                    g0 = g;
                    b0 = b;
                }
            }
        }

        private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
        {
            Span<byte> number = stackalloc byte[4];

            BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
            output.Write(number);
            output.Write(type);
            output.Write(data);

            //The checksum covers the type and the data, not the length
            uint crc = Crc(Crc(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu;
            BinaryPrimitives.WriteUInt32BigEndian(number, crc);
            output.Write(number);
        }

        private static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
        {
            foreach (byte b in bytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }

            return table;
        }
    }
}
