using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Tests.Codecs;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class PngTests
    {
        [TestMethod]
        public void Png_CanDecode_PngSignature()
        {
            byte[] header = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13 };
            var codec = new DRIT.Drawing.Codecs.PngCodec();
            Assert.IsTrue(codec.CanDecode(header));
        }

        [TestMethod]
        public void Png_RoundTrip_24bppRgb_ExactColor()
        {
            using var src = CodecsTestHelpers.MakeRgb(4, 4, Color.FromArgb(255, 200, 100, 50));
            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Png);

            Assert.AreEqual(4, dst.Width);
            Assert.AreEqual(4, dst.Height);
            var px = dst.GetPixel(2, 2);
            Assert.AreEqual(200, px.R);
            Assert.AreEqual(100, px.G);
            Assert.AreEqual(50, px.B);
        }

        [TestMethod]
        public void Png_RoundTrip_32bppArgb_ExactColor()
        {
            using var src = CodecsTestHelpers.MakeArgb(3, 3, Color.FromArgb(255, 12, 34, 56));
            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Png);

            Assert.AreEqual(3, dst.Width);
            Assert.AreEqual(3, dst.Height);
            var px = dst.GetPixel(1, 1);
            Assert.AreEqual(12, px.R);
            Assert.AreEqual(34, px.G);
            Assert.AreEqual(56, px.B);
        }

        [TestMethod]
        public void Png_RoundTrip_PreservesAlpha()
        {
            using var src = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));
            src.SetPixel(1, 0, Color.FromArgb(0, 0, 255, 0));
            src.SetPixel(0, 1, Color.FromArgb(255, 0, 0, 255));
            src.SetPixel(1, 1, Color.FromArgb(64, 255, 255, 0));

            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Png);

            Assert.AreEqual(128, dst.GetPixel(0, 0).A);
            Assert.AreEqual(0, dst.GetPixel(1, 0).A);
            Assert.AreEqual(255, dst.GetPixel(0, 1).A);
            Assert.AreEqual(64, dst.GetPixel(1, 1).A);
        }

        [TestMethod]
        public void Png_FromStream_SetsRawFormatToPng()
        {
            using var src = CodecsTestHelpers.MakeRgb(2, 2, Color.FromArgb(255, 100, 150, 200));
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            using var dst = new Bitmap(ms);
            Assert.IsTrue(dst.RawFormat.Equals(ImageFormat.Png));
        }
    }
}
