using System;
using NUnit.Framework;

namespace SceneContent.Tests
{
    public sealed class PngHeaderTests
    {
        [Test]
        public void TextureBudgetIsCheckedBeforeNativeDecoding()
        {
            byte[] header = new byte[33];
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 }.CopyTo(header, 0);
            header[18] = 4;
            header[22] = 4;
            PngHeader.ValidateDimensions(header);
            header[16] = 127;
            Assert.Throws<ArgumentException>(() => PngHeader.ValidateDimensions(header));
            header[16] = 0;
            header[18] = 32;
            header[22] = 32;
            Assert.Throws<ArgumentException>(() => PngHeader.ValidateDimensions(header));
            header[0] = 0;
            Assert.Throws<ArgumentException>(() => PngHeader.ValidateDimensions(header));
        }
    }
}
