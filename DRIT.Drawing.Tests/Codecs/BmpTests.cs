using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests.Codecs
{
    [TestClass]
    public class BmpTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // BMP via CodecRegistry
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Bmp_RoundTrip_ViaCodecRegistry()
        {
            using var src = CodecsTestHelpers.MakeRgb(4, 4, Color.FromArgb(255, 80, 160, 240));
            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Bmp);

            Assert.AreEqual(4, dst.Width);
            Assert.AreEqual(4, dst.Height);
            var px = dst.GetPixel(2, 2);
            Assert.AreEqual(80, px.R);
            Assert.AreEqual(160, px.G);
            Assert.AreEqual(240, px.B);
        }

        [TestMethod]
        public void Bmp_FromStream_SetsRawFormatToBmp()
        {
            using var src = CodecsTestHelpers.MakeRgb(2, 2, Color.Green);
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;
            using var dst = new Bitmap(ms);
            Assert.IsTrue(dst.RawFormat.Equals(ImageFormat.Bmp));
        }



        // ─────────────────────────────────────────────────────────────────────
        // Bitmap.Save extension
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Bitmap_Save_AsPng_RoundTrips()
        {
            using var src = CodecsTestHelpers.MakeArgb(5, 5, Color.FromArgb(200, 30, 60, 90));
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            using var dst = new Bitmap(ms);
            Assert.AreEqual(5, dst.Width);
            Assert.AreEqual(5, dst.Height);
            var px = dst.GetPixel(2, 2);
            Assert.AreEqual(30, px.R);
            Assert.AreEqual(60, px.G);
            Assert.AreEqual(90, px.B);
            Assert.AreEqual(200, px.A);
        }

        [TestMethod]
        public void Bitmap_Save_AsTiff_RoundTrips()
        {
            using var src = CodecsTestHelpers.MakeRgb(5, 5, Color.FromArgb(255, 70, 140, 210));
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Tiff);
            ms.Position = 0;
            using var dst = new Bitmap(ms);
            Assert.AreEqual(5, dst.Width);
            Assert.AreEqual(5, dst.Height);
            var px = dst.GetPixel(2, 2);
            Assert.AreEqual(70, px.R);
            Assert.AreEqual(140, px.G);
            Assert.AreEqual(210, px.B);
        }
    }
}
