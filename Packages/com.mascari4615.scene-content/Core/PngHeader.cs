using System;

namespace SceneContent
{
    public static class PngHeader
    {
        private const uint MAX_DIMENSION = 8192;
        private const ulong MAX_PIXELS = 16777216;
        private static readonly byte[] SIGNATURE = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static void ValidateDimensions(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 33)
            {
                throw new ArgumentException("Truncated PNG header");
            }
            for (int index = 0; index < SIGNATURE.Length; index++)
            {
                if (bytes[index] != SIGNATURE[index])
                {
                    throw new ArgumentException("Expected a PNG resource");
                }
            }
            if (Read(bytes, 8) != 13 || bytes[12] != 73 || bytes[13] != 72 || bytes[14] != 68 || bytes[15] != 82)
            {
                throw new ArgumentException("PNG must begin with an IHDR chunk");
            }
            uint width = Read(bytes, 16);
            uint height = Read(bytes, 20);
            if (width == 0 || height == 0 || width > MAX_DIMENSION || height > MAX_DIMENSION || (ulong)width * height > MAX_PIXELS)
            {
                throw new ArgumentException("PNG exceeds texture dimension or pixel budget");
            }
        }

        private static uint Read(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] << 24 | (uint)bytes[offset + 1] << 16 | (uint)bytes[offset + 2] << 8 | bytes[offset + 3];
        }
    }
}
