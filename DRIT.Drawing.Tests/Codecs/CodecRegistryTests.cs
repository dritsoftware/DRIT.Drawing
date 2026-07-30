using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests.Codecs
{
    [TestClass]
    public class CodecRegistryTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // CodecRegistry
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void CodecRegistry_DetectFormat_Png()
        {
            using var src = CodecsTestHelpers.MakeRgb(2, 2, Color.Red);
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            using var dst = DRIT.Drawing.Codecs.CodecRegistry.Decode(ms);
            Assert.IsTrue(dst.RawFormat.Equals(ImageFormat.Png));
        }

        [TestMethod]
        public void CodecRegistry_DetectFormat_Tiff()
        {
            using var src = CodecsTestHelpers.MakeRgb(2, 2, Color.Blue);
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Tiff);
            ms.Position = 0;
            using var dst = DRIT.Drawing.Codecs.CodecRegistry.Decode(ms);
            Assert.IsTrue(dst.RawFormat.Equals(ImageFormat.Tiff));
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void CodecRegistry_UnknownFormat_Throws()
        {
            byte[] noise = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            using var ms = new MemoryStream(noise);
            DRIT.Drawing.Codecs.CodecRegistry.Decode(ms);
        }
    }
}
