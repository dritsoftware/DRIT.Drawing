using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests.Codecs
{
    internal class TiffTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // TIFF
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Tiff_CanDecode_LittleEndianSignature()
        {
            byte[] header = { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00 };
            var codec = new DRIT.Drawing.Codecs.TiffCodec();
            Assert.IsTrue(codec.CanDecode(header));
        }

        [TestMethod]
        public void Tiff_RoundTrip_RGB_ExactColor()
        {
            using var src = CodecsTestHelpers.MakeRgb(3, 3, Color.FromArgb(255, 10, 20, 30));
            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Tiff);

            Assert.AreEqual(3, dst.Width);
            Assert.AreEqual(3, dst.Height);
            var px = dst.GetPixel(1, 1);
            Assert.AreEqual(10, px.R);
            Assert.AreEqual(20, px.G);
            Assert.AreEqual(30, px.B);
        }

        [TestMethod]
        public void Tiff_RoundTrip_RGBA_ExactColor()
        {
            using var src = CodecsTestHelpers.MakeArgb(3, 3, Color.FromArgb(200, 50, 100, 150));
            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Tiff);

            Assert.AreEqual(3, dst.Width);
            Assert.AreEqual(3, dst.Height);
            var px = dst.GetPixel(1, 1);
            Assert.AreEqual(50, px.R);
            Assert.AreEqual(100, px.G);
            Assert.AreEqual(150, px.B);
            Assert.AreEqual(200, px.A);
        }

        [TestMethod]
        public void Tiff_FromStream_SetsRawFormatToTiff()
        {
            using var src = CodecsTestHelpers.MakeRgb(2, 2, Color.Blue);
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Tiff);
            ms.Position = 0;
            using var dst = new Bitmap(ms);
            Assert.IsTrue(dst.RawFormat.Equals(ImageFormat.Tiff));
        }
    }
}
