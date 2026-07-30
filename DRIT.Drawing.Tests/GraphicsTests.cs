using System;
using System.IO;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class GraphicsTests
    {
        [TestMethod]
        public void Graphics_FromImage_Bitmap_ReturnsInstance()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            Assert.IsNotNull(g);
            Assert.AreEqual(96f, g.DpiX, 0.001f);
            Assert.AreEqual(96f, g.DpiY, 0.001f);
        }

        [TestMethod]
        public void Graphics_FromImage_NonBitmap_Throws()
        {
            using var img = new FakeImage();
            Assert.ThrowsException<NotSupportedException>(() => Graphics.FromImage(img));
        }

        [TestMethod]
        public void Graphics_FromHdc_ThrowsPlatformNotSupported()
            => Assert.ThrowsException<PlatformNotSupportedException>(() => Graphics.FromHdc(IntPtr.Zero));

        [TestMethod]
        public void Graphics_FromHwnd_ThrowsPlatformNotSupported()
            => Assert.ThrowsException<PlatformNotSupportedException>(() => Graphics.FromHwnd(IntPtr.Zero));

        [TestMethod]
        public void Graphics_Clear_FillsWholeSurface()
        {
            using var bmp = new Bitmap(6, 5, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            g.Clear(Color.FromArgb(255, 12, 34, 56));

            Assert.AreEqual(Color.FromArgb(255, 12, 34, 56).ToArgb(), bmp.GetPixel(0, 0).ToArgb());
            Assert.AreEqual(Color.FromArgb(255, 12, 34, 56).ToArgb(), bmp.GetPixel(5, 4).ToArgb());
        }

        [TestMethod]
        public void Graphics_FillRectangle_RespectsClip()
        {
            using var bmp = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            using var brush = new SolidBrush(Color.Blue);

            g.SetClip(new Rectangle(0, 0, 4, 4));
            g.FillRectangle(brush, new Rectangle(0, 0, 10, 10));

            Assert.AreEqual(Color.Blue.ToArgb(), bmp.GetPixel(1, 1).ToArgb());
            Assert.AreEqual(0, bmp.GetPixel(8, 8).A);
        }

        [TestMethod]
        public void Graphics_DrawLine_DrawsStroke()
        {
            using var bmp = new Bitmap(20, 10, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            using var pen = new Pen(Color.Red, 1f);

            g.DrawLine(pen, 2f, 4f, 16f, 4f);

            bool foundStroke = false;
            for (int x = 2; x <= 16; x++)
            {
                if (bmp.GetPixel(x, 4).A != 0 || bmp.GetPixel(x, 3).A != 0 || bmp.GetPixel(x, 5).A != 0)
                {
                    foundStroke = true;
                    break;
                }
            }

            Assert.IsTrue(foundStroke, "Expected a visible stroke in the horizontal line band.");
        }

        [TestMethod]
        public void Graphics_TranslateTransform_AffectsDrawingCoordinates()
        {
            using var bmp = new Bitmap(12, 6, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            using var brush = new SolidBrush(Color.Lime);

            g.TranslateTransform(5f, 0f);
            g.FillRectangle(brush, new RectangleF(0, 0, 2, 2));

            Assert.AreEqual(Color.Lime.ToArgb(), bmp.GetPixel(5, 0).ToArgb());
            Assert.AreEqual(0, bmp.GetPixel(0, 0).A);
        }

        [TestMethod]
        public void Graphics_SaveRestore_RestoresTransformAndClip()
        {
            using var bmp = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            g.TranslateTransform(2f, 3f);
            g.SetClip(new Rectangle(0, 0, 8, 8));
            var state = g.Save();

            g.TranslateTransform(50f, 50f);
            g.SetClip(new Rectangle(1, 1, 2, 2));

            g.Restore(state);

            Assert.IsTrue(g.IsVisible(new PointF(3, 3)));
            Assert.IsFalse(g.IsVisible(new PointF(15, 15)));
            Assert.AreEqual(2f, g.Transform.OffsetX, 0.001f);
            Assert.AreEqual(3f, g.Transform.OffsetY, 0.001f);
        }

        [TestMethod]
        public void Graphics_BeginEndContainer_RestoresState()
        {
            using var bmp = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            var container = g.BeginContainer();
            g.TranslateTransform(10f, 0f);
            g.PageUnit = GraphicsUnit.Inch;

            g.EndContainer(container);

            Assert.AreEqual(GraphicsUnit.Pixel, g.PageUnit);
            Assert.AreEqual(0f, g.Transform.OffsetX, 0.001f);
        }

        [TestMethod]
        public void Graphics_DrawImage_ScalesNearestNeighbor()
        {
            using var src = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.Red);
            src.SetPixel(1, 0, Color.Green);
            src.SetPixel(0, 1, Color.Blue);
            src.SetPixel(1, 1, Color.Yellow);

            using var dst = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(dst);

            g.DrawImage(src, new Rectangle(0, 0, 4, 4));

            Assert.AreEqual(Color.Red.ToArgb(), dst.GetPixel(0, 0).ToArgb());
            Assert.AreEqual(Color.Green.ToArgb(), dst.GetPixel(3, 0).ToArgb());
            Assert.AreEqual(Color.Blue.ToArgb(), dst.GetPixel(0, 3).ToArgb());
            Assert.AreEqual(Color.Yellow.ToArgb(), dst.GetPixel(3, 3).ToArgb());
        }

        [TestMethod]
        public void Graphics_DrawImageUnscaledAndClipped_ClipsToDestinationRect()
        {
            using var src = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            using var dst = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(dst);

            src.SetPixel(0, 0, Color.Red);
            src.SetPixel(3, 3, Color.Blue);

            g.DrawImageUnscaledAndClipped(src, new Rectangle(0, 0, 2, 2));

            Assert.AreEqual(Color.Red.ToArgb(), dst.GetPixel(0, 0).ToArgb());
            Assert.AreEqual(0, dst.GetPixel(3, 3).A);
        }

        [TestMethod]
        public void Graphics_IsVisible_UsesClipRegion()
        {
            using var bmp = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            g.SetClip(new Rectangle(2, 2, 4, 4));

            Assert.IsTrue(g.IsVisible(new PointF(3, 3)));
            Assert.IsFalse(g.IsVisible(new PointF(10, 10)));
            Assert.IsTrue(g.IsVisible(new RectangleF(1, 1, 4, 4)));
            Assert.IsFalse(g.IsVisible(new RectangleF(10, 10, 2, 2)));
        }

        [TestMethod]
        public void Graphics_GetNearestColor_ReturnsInputColor()
        {
            using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            var c = Color.FromArgb(200, 11, 22, 33);
            Assert.AreEqual(c.ToArgb(), g.GetNearestColor(c).ToArgb());
        }

        [TestMethod]
        public void Graphics_Flush_NoThrow()
        {
            using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);

            g.Flush();
            g.Flush(FlushIntention.Sync);
        }

        [TestMethod]
        public void Graphics_Dispose_ThenUse_ThrowsObjectDisposed()
        {
            var bmp = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            var g = Graphics.FromImage(bmp);
            g.Dispose();
            bmp.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => g.Clear(Color.Black));
        }

        private sealed class FakeImage : Image
        {
            public override int Width => 1;
            public override int Height => 1;
            public override PixelFormat PixelFormat => PixelFormat.Format32bppArgb;
            public override ImageFormat RawFormat => ImageFormat.MemoryBmp;
            public override Image Clone() => new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            public override void Save(string filename) => throw new NotSupportedException();
            public override void Save(string filename, ImageFormat format) => throw new NotSupportedException();
            public override void Save(Stream stream, ImageFormat format) => throw new NotSupportedException();
            public override void RotateFlip(RotateFlipType rotateFlipType) { }
        }
    }
}
