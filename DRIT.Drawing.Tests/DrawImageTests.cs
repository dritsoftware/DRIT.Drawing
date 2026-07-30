using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;

namespace DRIT.Drawing.Tests
{
    /// <summary>
    /// Tests for DrawImage functionality including interpolation modes and scaling.
    /// </summary>
    [TestClass]
    public class DrawImageTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Creates a 4×4 gradient bitmap: left=red, right=blue.</summary>
        private static Bitmap MakeGradientBitmap()
        {
            var bmp = new Bitmap(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    int r = (int)(255 * (1f - x / 3f));
                    int b = (int)(255 * (x / 3f));
                    bmp.SetPixel(x, y, Color.FromArgb(255, r, 0, b));
                }
            return bmp;
        }

        // ── ImageInterpolator (nearest-neighbour via DrawImage) ───────────────

        /// <summary>
        /// Tests that nearest-neighbor interpolation produces exact pixel copy when source and destination sizes match.
        /// </summary>
        [TestMethod]
        public void DrawImage_NearestNeighbour_SameSizeExactCopy()
        {
            using var src = MakeGradientBitmap();
            using var dst = new Bitmap(4, 4);
            using var g   = Graphics.FromImage(dst);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(src, new Rectangle(0, 0, 4, 4));

            for (int x = 0; x < 4; x++)
                Assert.AreEqual(src.GetPixel(x, 0).ToArgb(), dst.GetPixel(x, 0).ToArgb(),
                    $"Pixel at ({x},0) should match source.");
        }

        /// <summary>
        /// Tests that nearest-neighbor interpolation scales 2x correctly with blocky pixels.
        /// </summary>
        [TestMethod]
        public void DrawImage_Scale2x_NearestNeighbour()
        {
            using var src = new Bitmap(2, 2);
            src.SetPixel(0, 0, Color.Red);
            src.SetPixel(1, 0, Color.Blue);
            src.SetPixel(0, 1, Color.Green);
            src.SetPixel(1, 1, Color.White);

            using var dst = new Bitmap(4, 4);
            using var g   = Graphics.FromImage(dst);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(src, new Rectangle(0, 0, 4, 4));

            // top-left quadrant should be red
            Assert.AreEqual(Color.Red.ToArgb(), dst.GetPixel(0, 0).ToArgb());
            Assert.AreEqual(Color.Red.ToArgb(), dst.GetPixel(1, 0).ToArgb());
            // top-right quadrant should be blue
            Assert.AreEqual(Color.Blue.ToArgb(), dst.GetPixel(2, 0).ToArgb());
            Assert.AreEqual(Color.Blue.ToArgb(), dst.GetPixel(3, 0).ToArgb());
        }

        // ── Bilinear interpolation ────────────────────────────────────────────

        /// <summary>
        /// Tests that bilinear interpolation produces blended colors at midpoint.
        /// </summary>
        [TestMethod]
        public void DrawImage_Bilinear_MidpointBlend()
        {
            // 1×2 source: left pixel red, right pixel blue
            using var src = new Bitmap(2, 1);
            src.SetPixel(0, 0, Color.FromArgb(255, 255, 0, 0));
            src.SetPixel(1, 0, Color.FromArgb(255, 0, 0, 255));

            using var dst = new Bitmap(3, 1);
            using var g   = Graphics.FromImage(dst);
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(src, new Rectangle(0, 0, 3, 1));

            // Middle pixel of a 3-wide render should be a mix of red and blue
            Color mid = dst.GetPixel(1, 0);
            Assert.IsTrue(mid.R > 50 && mid.R < 255, $"R channel should be partial red, got {mid.R}");
            Assert.IsTrue(mid.B > 50 && mid.B < 255, $"B channel should be partial blue, got {mid.B}");
        }

        // ── RectangleF overload ───────────────────────────────────────────────

        /// <summary>
        /// Tests that DrawImage with RectangleF correctly scales the source image.
        /// </summary>
        [TestMethod]
        public void DrawImage_RectangleF_ScalesCorrectly()
        {
            using var src = new Bitmap(2, 2);
            src.SetPixel(0, 0, Color.Red);
            src.SetPixel(1, 0, Color.Red);
            src.SetPixel(0, 1, Color.Red);
            src.SetPixel(1, 1, Color.Red);

            using var dst = new Bitmap(4, 4);
            using var g   = Graphics.FromImage(dst);
            g.DrawImage(src, new RectangleF(0f, 0f, 4f, 4f));

            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    Assert.AreEqual(255, dst.GetPixel(x, y).R, $"Pixel ({x},{y}) should be red.");
        }

        // ── PointF convenience overload ───────────────────────────────────────

        /// <summary>
        /// Tests that DrawImage with PointF places the image at the specified offset.
        /// </summary>
        [TestMethod]
        public void DrawImage_PointF_PlacesImageAtOffset()
        {
            using var src = new Bitmap(2, 2);
            src.SetPixel(0, 0, Color.Lime);
            src.SetPixel(1, 0, Color.Lime);
            src.SetPixel(0, 1, Color.Lime);
            src.SetPixel(1, 1, Color.Lime);

            using var dst = new Bitmap(6, 6);
            using var g   = Graphics.FromImage(dst);
            g.DrawImage(src, new PointF(3f, 3f));

            Assert.AreEqual(Color.Lime.ToArgb(), dst.GetPixel(3, 3).ToArgb());
            Assert.AreEqual(Color.Lime.ToArgb(), dst.GetPixel(4, 3).ToArgb());
            // Outside the placed region should be transparent
            Assert.AreEqual(0, dst.GetPixel(0, 0).A, "Pixel outside placed region should be transparent.");
        }

        // ── Sub-image overloads ───────────────────────────────────────────────

        /// <summary>
        /// Tests that DrawImage with source and destination rectangles correctly crops and scales.
        /// </summary>
        [TestMethod]
        public void DrawImage_SubImage_Rectangle_CropsCorrectly()
        {
            using var src = new Bitmap(4, 4);
            // Fill left half red, right half blue
            for (int y = 0; y < 4; y++)
            {
                src.SetPixel(0, y, Color.Red);
                src.SetPixel(1, y, Color.Red);
                src.SetPixel(2, y, Color.Blue);
                src.SetPixel(3, y, Color.Blue);
            }

            using var dst = new Bitmap(4, 4);
            using var g   = Graphics.FromImage(dst);
            // Draw only the blue half (x=2..3) into full dest
            var srcRect = new Rectangle(2, 0, 2, 4);
            var dstRect = new Rectangle(0, 0, 4, 4);
            g.DrawImage(src, dstRect, srcRect, GraphicsUnit.Pixel);

            for (int y = 0; y < 4; y++)
                Assert.IsTrue(dst.GetPixel(0, y).B > 128, $"Dest ({0},{y}) should be blue.");
        }

        /// <summary>
        /// Tests that DrawImage with RectangleF and ImageAttributes applies color matrix transformation.
        /// </summary>
        [TestMethod]
        public void DrawImage_SubImage_RectangleF_WithAttrs()
        {
            using var src = new Bitmap(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    src.SetPixel(x, y, Color.White);

            // Color matrix: make everything red by zeroing G and B channels
            var cm = new ColorMatrix();
            cm[0, 0] = 1f; cm[1, 1] = 0f; cm[2, 2] = 0f; cm[3, 3] = 1f;

            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(cm);

            using var dst = new Bitmap(4, 4);
            using var g   = Graphics.FromImage(dst);
            g.DrawImage(src, new RectangleF(0, 0, 4, 4), new RectangleF(0, 0, 4, 4), GraphicsUnit.Pixel, attrs);

            Color p = dst.GetPixel(0, 0);
            Assert.AreEqual(255, p.R, "Red channel should be 255.");
            Assert.AreEqual(0,   p.G, "Green channel should be 0.");
            Assert.AreEqual(0,   p.B, "Blue channel should be 0.");
        }

        // ── Affine warp (3-point PointF[]) ───────────────────────────────────

        /// <summary>
        /// Tests that 3-point affine warp produces same result as Rectangle overload for identity transform.
        /// </summary>
        [TestMethod]
        public void DrawImage_PointFArray_Identity_MatchesRectangle()
        {
            using var src = new Bitmap(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    src.SetPixel(x, y, Color.Cyan);

            using var dst1 = new Bitmap(4, 4);
            using var dst2 = new Bitmap(4, 4);

            using (var g = Graphics.FromImage(dst1))
                g.DrawImage(src, new Rectangle(0, 0, 4, 4));

            // 3-point warp: identity parallelogram maps src to same dest region
            using (var g = Graphics.FromImage(dst2))
                g.DrawImage(src, new PointF[]
                {
                    new PointF(0, 0), // top-left
                    new PointF(4, 0), // top-right
                    new PointF(0, 4), // bottom-left
                });

            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    Assert.AreEqual(dst1.GetPixel(x, y).ToArgb(), dst2.GetPixel(x, y).ToArgb(),
                        $"Affine-identity warp should match rectangle draw at ({x},{y}).");
        }

        [TestMethod]
        public void DrawImage_PointFArray_SubImage_WithAttrs()
        {
            using var src = new Bitmap(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    src.SetPixel(x, y, Color.White);

            // Remap white → yellow
            var map = new ColorMap[]
            {
                new ColorMap { OldColor = Color.White, NewColor = Color.Yellow }
            };
            using var attrs = new ImageAttributes();
            attrs.SetRemapTable(map);

            using var dst = new Bitmap(4, 4);
            using var g   = Graphics.FromImage(dst);
            g.DrawImage(src,
                new PointF[] { new PointF(0, 0), new PointF(4, 0), new PointF(0, 4) },
                new RectangleF(0, 0, 4, 4), GraphicsUnit.Pixel, attrs);

            Color p = dst.GetPixel(2, 2);
            Assert.AreEqual(Color.Yellow.R, p.R, "R should be yellow.R.");
            Assert.AreEqual(Color.Yellow.G, p.G, "G should be yellow.G.");
            Assert.AreEqual(0,              p.B, "B should be 0 for yellow.");
        }

        /// <summary>
        /// Tests that DrawImage with integer x,y coordinates places the image correctly.
        /// </summary>
        [TestMethod]
        public void DrawImage_IntXY_PlacesImageCorrectly()
        {
            using var src = new Bitmap(2, 2);
            src.SetPixel(0, 0, Color.Magenta);
            src.SetPixel(1, 0, Color.Magenta);
            src.SetPixel(0, 1, Color.Magenta);
            src.SetPixel(1, 1, Color.Magenta);

            using var dst = new Bitmap(6, 6);
            using var g   = Graphics.FromImage(dst);
            g.DrawImage(src, 4, 4);

            Assert.AreEqual(Color.Magenta.ToArgb(), dst.GetPixel(4, 4).ToArgb());
            Assert.AreEqual(Color.Magenta.ToArgb(), dst.GetPixel(5, 4).ToArgb());
        }

        // ── Explicit src-bounds integer overload ─────────────────────────────

        /// <summary>
        /// Tests that DrawImage with explicit source bounds correctly crops and scales the image.
        /// </summary>
        [TestMethod]
        public void DrawImage_ExplicitSrcBounds_Int_CropsAndScales()
        {
            using var src = new Bitmap(4, 4);
            for (int y = 0; y < 4; y++)
            {
                src.SetPixel(0, y, Color.Red);
                src.SetPixel(1, y, Color.Red);
                src.SetPixel(2, y, Color.Blue);
                src.SetPixel(3, y, Color.Blue);
            }

            using var dst = new Bitmap(4, 4);
            using var g   = Graphics.FromImage(dst);
            // Draw only the blue right-half (srcX=2, srcY=0, srcW=2, srcH=4) into dst
            g.DrawImage(src, new Rectangle(0, 0, 4, 4), 2, 0, 2, 4, GraphicsUnit.Pixel);

            for (int y = 0; y < 4; y++)
                Assert.IsTrue(dst.GetPixel(0, y).B > 128, $"All pixels should be blue, col 0 row {y}: {dst.GetPixel(0,y)}");
        }
    }
}
