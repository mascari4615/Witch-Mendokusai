using System;
using System.IO;

namespace SceneContent
{
    public sealed class RgbaMipPayload
    {
        public const string MediaType = "application/vnd.scene.rgba-mips";
        public readonly int Width, Height, MipCount;
        public readonly bool Linear;
        public readonly byte[] Pixels;

        private RgbaMipPayload(int width, int height, int count, bool linear, byte[] pixels)
        {
            Width = width; Height = height; MipCount = count; Linear = linear; Pixels = pixels;
        }

        // RGMP, little-endian uint32 header and canonical contiguous level table
        // Header: revision, width, height, RGBA8=1, linear=0/sRGB=1, count, pixel bytes
        // Per level: width, height, pixel-relative offset, byte length
        // Pixel bytes: tightly packed RGBA8, bottom-origin rows, level zero first
        public static RgbaMipPayload Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 32 || bytes.Length > 268435456 ||
                bytes[0] != 'R' || bytes[1] != 'G' || bytes[2] != 'M' || bytes[3] != 'P')
                throw new InvalidDataException("Invalid RGBA mip header");
            uint revision = UInt(bytes, 4), width = UInt(bytes, 8), height = UInt(bytes, 12);
            uint format = UInt(bytes, 16), color = UInt(bytes, 20), count = UInt(bytes, 24), length = UInt(bytes, 28);
            if (revision != 1 || format != 1 || color > 1 || width == 0 || height == 0 ||
                width > 8192 || height > 8192 || (ulong)width * height > 16777216)
                throw new InvalidDataException("Unsupported RGBA mip contract or dimensions");
            uint expectedCount = 1, w = width, h = height;
            while (w > 1 || h > 1)
            {
                w = Math.Max(1u, w / 2); h = Math.Max(1u, h / 2); expectedCount++;
            }
            if (count != expectedCount) throw new InvalidDataException("A complete mip chain is required");
            long start = 32L + count * 16L;
            if (start + length != bytes.Length) throw new InvalidDataException("RGBA mip payload length mismatch");
            ulong offset = 0;
            w = width; h = height;
            for (int level = 0; level < (int)count; level++)
            {
                int entry = 32 + level * 16;
                ulong size = (ulong)w * h * 4;
                if (UInt(bytes, entry) != w || UInt(bytes, entry + 4) != h ||
                    UInt(bytes, entry + 8) != offset || UInt(bytes, entry + 12) != size || offset + size > length)
                    throw new InvalidDataException("Invalid RGBA mip dimensions, bounds or order");
                offset += size;
                w = Math.Max(1u, w / 2); h = Math.Max(1u, h / 2);
            }
            if (offset != length) throw new InvalidDataException("Unexpected RGBA mip trailing bytes");
            byte[] pixels = new byte[(int)length];
            Buffer.BlockCopy(bytes, (int)start, pixels, 0, pixels.Length);
            return new RgbaMipPayload((int)width, (int)height, (int)count, color == 0, pixels);
        }

        private static uint UInt(byte[] bytes, int offset)
        {
            if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("Truncated RGBA mip table");
            return (uint)bytes[offset] | (uint)bytes[offset + 1] << 8 |
                (uint)bytes[offset + 2] << 16 | (uint)bytes[offset + 3] << 24;
        }
    }
}
