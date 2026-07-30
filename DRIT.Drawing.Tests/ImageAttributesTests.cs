using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class ImageAttributesTests
    {
        [TestMethod]
        public void ImageAttributes_Stub_Disposable()
        {
            using var ia = new ImageAttributes();
            Assert.IsNotNull(ia);
        }

        [TestMethod]
        public void ImageAttributes_Clone_ReturnsNewInstance()
        {
            var ia  = new ImageAttributes();
            var ia2 = ia.Clone();
            Assert.IsNotNull(ia2);
            Assert.AreNotSame(ia, ia2);
        }

        // ── ImageAttributes — color key ───────────────────────────────────────

        [TestMethod]
        public void ImageAttributes_ColorKey_MakesMatchingPixelsTransparent()
        {
            using var src = new Bitmap(2, 1);
            src.SetPixel(0, 0, Color.Lime);
            src.SetPixel(1, 0, Color.Red);

            using var attrs = new ImageAttributes();
            attrs.SetColorKey(Color.Lime, Color.Lime);

            using var dst = new Bitmap(2, 1);
            using var g = Graphics.FromImage(dst);
            g.DrawImage(src, new RectangleF(0, 0, 2, 1), new RectangleF(0, 0, 2, 1), GraphicsUnit.Pixel, attrs);

            Assert.AreEqual(0, dst.GetPixel(0, 0).A, "Lime pixel should be transparent.");
            Assert.AreEqual(255, dst.GetPixel(1, 0).A, "Red pixel should remain opaque.");
        }

        // ── ImageAttributes — gamma ───────────────────────────────────────────

        [TestMethod]
        public void ImageAttributes_Gamma_LightensMidGrey()
        {
            // Mid-grey source
            using var src = new Bitmap(1, 1);
            src.SetPixel(0, 0, Color.FromArgb(255, 128, 128, 128));

            using var attrs = new ImageAttributes();
            attrs.SetGamma(2.2f); // inverse-gamma = 1/2.2 ≈ 0.454 → lightens mid-grey

            using var dst = new Bitmap(1, 1);
            using var g = Graphics.FromImage(dst);
            g.DrawImage(src, new RectangleF(0, 0, 1, 1), new RectangleF(0, 0, 1, 1), GraphicsUnit.Pixel, attrs);

            Color p = dst.GetPixel(0, 0);
            // pow(128/255, 1/2.2) ≈ 0.735 → ~187
            Assert.IsTrue(p.R > 160, $"Gamma-corrected R should be > 160, got {p.R}.");
        }

        // ── ImageAttributes — color matrix (grayscale) ────────────────────────

        [TestMethod]
        public void ImageAttributes_ColorMatrix_ConvertToGrayscale()
        {
            // NTSC luminance weights: R=0.299, G=0.587, B=0.114
            float r = 0.299f, gr = 0.587f, b = 0.114f;
            var cm = new ColorMatrix(new float[][]
            {
                new float[] { r, r, r, 0, 0 },
                new float[] { gr, gr, gr, 0, 0 },
                new float[] { b, b, b, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 },
            });

            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(cm);

            // Pure red source
            using var src = new Bitmap(1, 1);
            src.SetPixel(0, 0, Color.FromArgb(255, 255, 0, 0));

            using var dst = new Bitmap(1, 1);
            using var g = Graphics.FromImage(dst);
            g.DrawImage(src, new RectangleF(0, 0, 1, 1), new RectangleF(0, 0, 1, 1), GraphicsUnit.Pixel, attrs);

            Color p = dst.GetPixel(0, 0);
            // pure red → luminance = 0.299*255 ≈ 76
            int expected = (int)(r * 255);
            Assert.AreEqual(p.R, p.G, $"Grayscale R==G, got R={p.R} G={p.G}.");
            Assert.AreEqual(p.R, p.B, $"Grayscale R==B, got R={p.R} B={p.B}.");
            Assert.IsTrue(Math.Abs(p.R - expected) <= 2, $"Expected ~{expected}, got {p.R}.");
        }

        // ── ImageAttributes — remap table ─────────────────────────────────────

        [TestMethod]
        public void ImageAttributes_RemapTable_SwapsColors()
        {
            using var src = new Bitmap(2, 1);
            src.SetPixel(0, 0, Color.Red);
            src.SetPixel(1, 0, Color.Blue);

            var map = new ColorMap[]
            {
                new ColorMap { OldColor = Color.Red,  NewColor = Color.Blue  },
                new ColorMap { OldColor = Color.Blue, NewColor = Color.Red   },
            };
            using var attrs = new ImageAttributes();
            attrs.SetRemapTable(map);

            using var dst = new Bitmap(2, 1);
            using var g = Graphics.FromImage(dst);
            g.DrawImage(src, new RectangleF(0, 0, 2, 1), new RectangleF(0, 0, 2, 1), GraphicsUnit.Pixel, attrs);

            Assert.AreEqual(Color.Blue.ToArgb(), dst.GetPixel(0, 0).ToArgb(), "Red should be remapped to Blue.");
            Assert.AreEqual(Color.Red.ToArgb(), dst.GetPixel(1, 0).ToArgb(), "Blue should be remapped to Red.");
        }

        // ── ImageAttributes — clone ───────────────────────────────────────────

        [TestMethod]
        public void ImageAttributes_Clone_IsIndependent()
        {
            using var attrs = new ImageAttributes();
            attrs.SetGamma(2f);
            using var clone = attrs.Clone();
            attrs.ClearGamma();

            // Clearing gamma on original should not affect clone
            using var src = new Bitmap(1, 1);
            src.SetPixel(0, 0, Color.FromArgb(255, 100, 100, 100));

            using var dst1 = new Bitmap(1, 1);
            using var dst2 = new Bitmap(1, 1);
            using var g1 = Graphics.FromImage(dst1);
            using var g2 = Graphics.FromImage(dst2);
            g1.DrawImage(src, new RectangleF(0, 0, 1, 1), new RectangleF(0, 0, 1, 1), GraphicsUnit.Pixel, attrs);
            g2.DrawImage(src, new RectangleF(0, 0, 1, 1), new RectangleF(0, 0, 1, 1), GraphicsUnit.Pixel, clone);

            // clone still has gamma=2 so pixels should differ
            Assert.AreNotEqual(dst1.GetPixel(0, 0).R, dst2.GetPixel(0, 0).R,
                "Cloned attrs with gamma should produce different pixel than cleared attrs.");
        }
    }
}