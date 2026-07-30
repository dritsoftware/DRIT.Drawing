using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Imaging
{
    [TestClass]
    public class BitmapTests
    {
        // -----------------------------------------------------------------------
        // Bitmap — constructors
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_DefaultCtor_Transparent()
        {
            using var bmp = new Bitmap(10, 10);
            Assert.AreEqual(10, bmp.Width);
            Assert.AreEqual(10, bmp.Height);
            Assert.AreEqual(PixelFormat.Format32bppArgb, bmp.PixelFormat);
            // All pixels should be transparent black
            var px = bmp.GetPixel(0, 0);
            Assert.AreEqual(0, px.A);
        }

        [TestMethod]
        public void Bitmap_24bppCtor()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
            Assert.AreEqual(PixelFormat.Format24bppRgb, bmp.PixelFormat);
            Assert.AreEqual(8, bmp.Width);
        }


        [TestMethod]
        public void Bitmap_FromImageCtor_CopiesPixels()
        {
            using var src = new Bitmap(4, 4);
            src.SetPixel(2, 2, Color.Red);
            using var dst = new Bitmap(src);
            Assert.AreEqual(Color.Red.ToArgb(), dst.GetPixel(2, 2).ToArgb());
        }

        // -----------------------------------------------------------------------
        // Bitmap — GetPixel / SetPixel round-trips
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_GetSetPixel_32bpp_RoundTrip()
        {
            using var bmp = new Bitmap(5, 5);
            bmp.SetPixel(3, 1, Color.FromArgb(200, 100, 50, 25));
            var c = bmp.GetPixel(3, 1);
            Assert.AreEqual(200, c.A);
            Assert.AreEqual(100, c.R);
            Assert.AreEqual(50,  c.G);
            Assert.AreEqual(25,  c.B);
        }

        [TestMethod]
        public void Bitmap_GetSetPixel_24bpp_RoundTrip()
        {
            using var bmp = new Bitmap(5, 5, PixelFormat.Format24bppRgb);
            bmp.SetPixel(1, 3, Color.Blue);
            var c = bmp.GetPixel(1, 3);
            Assert.AreEqual(255, (int)c.A);
            Assert.AreEqual(0,   (int)c.R);
            Assert.AreEqual(0,   (int)c.G);
            Assert.AreEqual(255, (int)c.B);
        }

        [TestMethod]
        public void Bitmap_GetSetPixel_Red_LayoutIsBGRA()
        {
            // Verify internal byte order: Red = [0, 0, 255, 255] = B=0, G=0, R=255, A=255
            using var bmp = new Bitmap(2, 2);
            bmp.SetPixel(0, 0, Color.Red);
            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(0, 0).ToArgb());
        }

        [TestMethod]
        public void Bitmap_SetPixel_OutOfRange_Throws()
        {
            using var bmp = new Bitmap(3, 3);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => bmp.SetPixel(5, 0, Color.Black));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => bmp.SetPixel(0, 5, Color.Black));
        }

        // -----------------------------------------------------------------------
        // Bitmap — LockBits / UnlockBits
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_LockBits_Scan0ReadsCorrectPixel()
        {
            using var bmp = new Bitmap(4, 4);
            bmp.SetPixel(1, 2, Color.Red); // B=0, G=0, R=255, A=255

            var rect = new Rectangle(0, 0, 4, 4);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            // Pixel (1,2): offset = row 2 * stride + col 1 * 4
            int offset = 2 * data.Stride + 1 * 4;
            var buf = new byte[4];
            Marshal.Copy(IntPtr.Add(data.Scan0, offset), buf, 0, 4);
            bmp.UnlockBits(data);

            Assert.AreEqual(0,   buf[0]); // B
            Assert.AreEqual(0,   buf[1]); // G
            Assert.AreEqual(255, buf[2]); // R
            Assert.AreEqual(255, buf[3]); // A
        }

        [TestMethod]
        public void Bitmap_LockBits_StrideMath()
        {
            using var bmp = new Bitmap(7, 3, PixelFormat.Format24bppRgb);
            var data = bmp.LockBits(new Rectangle(0, 0, 7, 3),
                                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            bmp.UnlockBits(data);
            // Stride must be 4-byte aligned: ((7*24+31)/32)*4 = (199/32)*4 = 6*4 = 24
            Assert.AreEqual(24, data.Stride);
        }

        [TestMethod]
        public void Bitmap_UnlockBits_DoesNotThrow()
        {
            using var bmp = new Bitmap(2, 2);
            var data = bmp.LockBits(new Rectangle(0, 0, 2, 2),
                                    ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            bmp.UnlockBits(data); // must not throw
        }

        // -----------------------------------------------------------------------
        // Bitmap — MakeTransparent
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_MakeTransparent_Color_RemovesMatchingPixels()
        {
            using var bmp = new Bitmap(4, 4);
            bmp.SetPixel(0, 0, Color.Lime);
            bmp.SetPixel(1, 0, Color.Red);
            bmp.MakeTransparent(Color.Lime);

            Assert.AreEqual(0, bmp.GetPixel(0, 0).A);  // made transparent
            Assert.AreNotEqual(0, bmp.GetPixel(1, 0).A); // untouched
        }

        [TestMethod]
        public void Bitmap_MakeTransparent_Default_UsesBottomLeft()
        {
            using var bmp = new Bitmap(3, 3);
            // Bottom-left = (0, H-1) = (0, 2)
            bmp.SetPixel(0, 2, Color.Blue);
            bmp.SetPixel(1, 0, Color.Blue);
            bmp.MakeTransparent();   // uses bottom-left pixel (Blue)

            Assert.AreEqual(0, bmp.GetPixel(0, 2).A);  // made transparent
            Assert.AreEqual(0, bmp.GetPixel(1, 0).A);  // same color also removed
        }

        // -----------------------------------------------------------------------
        // Bitmap — Clone
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_CloneRect_SubBitmap()
        {
            using var bmp = new Bitmap(10, 10);
            bmp.SetPixel(3, 3, Color.Red);
            using var clone = bmp.Clone(new Rectangle(2, 2, 5, 5), PixelFormat.Format32bppArgb);
            Assert.AreEqual(5, clone.Width);
            Assert.AreEqual(5, clone.Height);
            Assert.AreEqual(Color.Red.ToArgb(), clone.GetPixel(1, 1).ToArgb()); // relative coords
        }

        [TestMethod]
        public void Bitmap_Clone_IndependentCopy()
        {
            using var bmp = new Bitmap(4, 4);
            bmp.SetPixel(0, 0, Color.Green);
            using var clone = (Bitmap)bmp.Clone();
            bmp.SetPixel(0, 0, Color.Red);
            Assert.AreEqual(Color.Green.ToArgb(), clone.GetPixel(0, 0).ToArgb());
        }

        // -----------------------------------------------------------------------
        // Bitmap — RotateFlip
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_RotateFlip_180_InvertsCoords()
        {
            using var bmp = new Bitmap(4, 4);
            bmp.SetPixel(0, 0, Color.Red);  // top-left
            bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
            // After 180°: pixel that was at (0,0) is now at (W-1, H-1) = (3,3)
            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(3, 3).ToArgb());
            Assert.AreEqual(4, bmp.Width);
            Assert.AreEqual(4, bmp.Height);
        }

        [TestMethod]
        public void Bitmap_RotateFlip_90CW_SwapsDimensions()
        {
            using var bmp = new Bitmap(6, 4);
            bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
            Assert.AreEqual(4, bmp.Width);
            Assert.AreEqual(6, bmp.Height);
        }

        [TestMethod]
        public void Bitmap_RotateFlip_90CW_PixelPosition()
        {
            // Top-left pixel (0,0) after 90° CW goes to top-right: (H-1, 0) in new coords
            // new image: newW=H=4, newH=W=6
            // new(H-1, 0) = new(newW-1, 0) for a 4-wide image = (3, 0)
            using var bmp = new Bitmap(6, 4);  // W=6, H=4
            bmp.SetPixel(0, 0, Color.Red);
            bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
            // After 90 CW: new(nx=H-1-oldY, ny=oldX) = new(3, 0)
            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(3, 0).ToArgb());
        }

        [TestMethod]
        public void Bitmap_RotateFlip_FlipX_MirrorsHorizontally()
        {
            using var bmp = new Bitmap(5, 3);
            bmp.SetPixel(0, 1, Color.Red);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
            // FlipX: pixel at (0,1) moves to (W-1, 1) = (4, 1)
            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(4, 1).ToArgb());
        }

        [TestMethod]
        public void Bitmap_RotateFlip_NoOp_Unchanged()
        {
            using var bmp = new Bitmap(3, 3);
            bmp.SetPixel(1, 1, Color.Blue);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipNone);
            Assert.AreEqual(Color.Blue.ToArgb(), bmp.GetPixel(1, 1).ToArgb());
        }

        // -----------------------------------------------------------------------
        // Bitmap — BMP Save / Load round-trip
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_SaveLoad_Bmp_RoundTrip_32bpp()
        {
            using var original = new Bitmap(8, 8);
            original.SetPixel(2, 3, Color.FromArgb(255, 200, 100, 50));
            original.SetPixel(5, 5, Color.Blue);

            using var ms = new MemoryStream();
            original.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;

            using var loaded = new Bitmap(ms);
            Assert.AreEqual(8, loaded.Width);
            Assert.AreEqual(8, loaded.Height);

            var c1 = loaded.GetPixel(2, 3);
            Assert.AreEqual(200, c1.R, 1);
            Assert.AreEqual(100, c1.G, 1);
            Assert.AreEqual(50,  c1.B, 1);

            var c2 = loaded.GetPixel(5, 5);
            Assert.AreEqual(0,   c2.R);
            Assert.AreEqual(0,   c2.G);
            Assert.AreEqual(255, c2.B);
        }

        [TestMethod]
        public void Bitmap_SaveLoad_Bmp_24bpp_RoundTrip()
        {
            using var original = new Bitmap(6, 6, PixelFormat.Format24bppRgb);
            original.SetPixel(1, 1, Color.Red);

            using var ms = new MemoryStream();
            original.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;

            using var loaded = new Bitmap(ms);
            Assert.AreEqual(6, loaded.Width);
            Assert.AreEqual(6, loaded.Height);
            var c = loaded.GetPixel(1, 1);
            Assert.AreEqual(255, c.R);
            Assert.AreEqual(0,   c.G);
            Assert.AreEqual(0,   c.B);
        }

        [TestMethod]
        public void Bitmap_Save_UnsupportedFormat_Throws()
        {
            using var bmp = new Bitmap(4, 4);
            using var ms  = new MemoryStream();
            // Emf is not supported for encoding
            Assert.ThrowsException<NotSupportedException>(() => bmp.Save(ms, ImageFormat.Emf));
        }

        // -----------------------------------------------------------------------
        // Bitmap — Image base class API
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Bitmap_GetBounds_ReturnsSizeInPixels()
        {
            using var bmp = new Bitmap(20, 15);
            var unit = GraphicsUnit.Pixel;
            var b = bmp.GetBounds(ref unit);
            Assert.AreEqual(0f,  b.X);
            Assert.AreEqual(0f,  b.Y);
            Assert.AreEqual(20f, b.Width);
            Assert.AreEqual(15f, b.Height);
            Assert.AreEqual(GraphicsUnit.Pixel, unit);
        }

        [TestMethod]
        public void Bitmap_Resolution_Defaults96dpi()
        {
            using var bmp = new Bitmap(10, 10);
            Assert.AreEqual(96f, bmp.HorizontalResolution, 1f);
            Assert.AreEqual(96f, bmp.VerticalResolution,   1f);
        }

        [TestMethod]
        public void Bitmap_FrameCount_IsOne()
        {
            using var bmp = new Bitmap(4, 4);
            Assert.AreEqual(1, bmp.GetFrameCount(FrameDimension.Page));
        }

        [TestMethod]
        public void Bitmap_PropertyItems_Empty()
        {
            using var bmp = new Bitmap(4, 4);
            Assert.AreEqual(0, bmp.PropertyItems.Length);
            Assert.AreEqual(0, bmp.GetPropertyIdList().Length);
            Assert.IsNull(bmp.GetPropertyItem(271));
        }

        [TestMethod]
        public void Bitmap_GetHbitmap_ThrowsPlatformNotSupported()
        {
            using var bmp = new Bitmap(4, 4);
            Assert.ThrowsException<PlatformNotSupportedException>(() => bmp.GetHbitmap());
        }

        // -----------------------------------------------------------------------
        // Image.FromFile / FromStream static factory (via BMP)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Image_FromStream_BMP_Works()
        {
            using var original = new Bitmap(4, 4);
            original.SetPixel(1, 1, Color.Green);
            using var ms = new MemoryStream();
            original.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;

            using var img = Image.FromStream(ms);
            Assert.AreEqual(4, img.Width);
            Assert.AreEqual(4, img.Height);
        }
    }
}