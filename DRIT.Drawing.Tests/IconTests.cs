using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class IconTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a minimal valid 2×2 32-bpp ICO byte array (red pixels, opaque).
        /// </summary>
        internal static byte[] Make2x2RedIcoBytes() => new byte[]
        {
            // ICONDIR
            0x00, 0x00,              // reserved
            0x01, 0x00,              // type = 1 (ICO)
            0x01, 0x00,              // count = 1
            // ICONDIRENTRY (16 bytes)
            0x02,                    // width = 2
            0x02,                    // height = 2
            0x00,                    // colour count (0 = 256+)
            0x00,                    // reserved
            0x01, 0x00,              // planes = 1
            0x20, 0x00,              // bpp = 32
            0x50, 0x00, 0x00, 0x00,  // resource size = 80
            0x16, 0x00, 0x00, 0x00,  // image data offset = 22
            // BITMAPINFOHEADER (40 bytes)
            0x28, 0x00, 0x00, 0x00,  // header size = 40
            0x02, 0x00, 0x00, 0x00,  // width  = 2
            0x04, 0x00, 0x00, 0x00,  // height = 4  (ICO: 2 × actual height)
            0x01, 0x00,              // planes = 1
            0x20, 0x00,              // bpp = 32
            0x00, 0x00, 0x00, 0x00,  // compression = BI_RGB
            0x00, 0x00, 0x00, 0x00,  // image size
            0x00, 0x00, 0x00, 0x00,  // X ppm
            0x00, 0x00, 0x00, 0x00,  // Y ppm
            0x00, 0x00, 0x00, 0x00,  // colours used
            0x00, 0x00, 0x00, 0x00,  // colours important
            // XOR mask — BGRA, bottom-up (2 rows × 2 pixels × 4 bytes = 16 bytes)
            0x00, 0x00, 0xFF, 0xFF,  0x00, 0x00, 0xFF, 0xFF,  // row 0 (y=1): red, red
            0x00, 0x00, 0xFF, 0xFF,  0x00, 0x00, 0xFF, 0xFF,  // row 1 (y=0): red, red
            // AND mask — 1 bpp, bottom-up (2 rows × 4 bytes each, all 0 = opaque)
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        // ── Icon — construction ────────────────────────────────────────────────

        [TestMethod]
        public void Icon_FromStream_LoadsBitmap()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var icon = new Icon(ms);

            Assert.AreEqual(2, icon.Width);
            Assert.AreEqual(2, icon.Height);
            Assert.AreEqual(new Size(2, 2), icon.Size);
        }

        [TestMethod]
        public void Icon_Size_MatchesBitmapSize()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var icon = new Icon(ms);
            using var bmp  = icon.ToBitmap();

            Assert.AreEqual(bmp.Width,  icon.Width);
            Assert.AreEqual(bmp.Height, icon.Height);
        }

        [TestMethod]
        public void Icon_ToBitmap_PixelContent()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var icon = new Icon(ms);
            using var bmp  = icon.ToBitmap();

            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    var px = bmp.GetPixel(x, y);
                    Assert.AreEqual(255, px.R, $"R at ({x},{y})");
                    Assert.AreEqual(0,   px.G, $"G at ({x},{y})");
                    Assert.AreEqual(0,   px.B, $"B at ({x},{y})");
                    Assert.AreEqual(255, px.A, $"A at ({x},{y})");
                }
        }

        [TestMethod]
        public void Icon_ToBitmap_IsIndependentCopy()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var icon = new Icon(ms);

            using var bmp1 = icon.ToBitmap();
            using var bmp2 = icon.ToBitmap();

            Assert.IsFalse(ReferenceEquals(bmp1, bmp2),
                "Each ToBitmap() call must return a distinct object.");
        }

        [TestMethod]
        public void Icon_CopyConstructor_CreatesIndependentIcon()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var orig = new Icon(ms);
            using var copy = new Icon(orig, 2, 2);

            Assert.AreEqual(orig.Width,  copy.Width);
            Assert.AreEqual(orig.Height, copy.Height);
        }

        [TestMethod]
        public void Icon_CopyConstructor_Size_CreatesIndependentIcon()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var orig = new Icon(ms);
            using var copy = new Icon(orig, orig.Size);

            Assert.AreEqual(orig.Width,  copy.Width);
            Assert.AreEqual(orig.Height, copy.Height);
        }

        // ── Icon — Save / reload round-trip ────────────────────────────────────

        [TestMethod]
        public void Icon_Save_WritesIcoSignature()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var icon = new Icon(ms);

            using var saved = new MemoryStream();
            icon.Save(saved);
            saved.Position = 0;

            byte[] hdr = new byte[4];
            saved.Read(hdr, 0, 4);
            // ICO header: 00 00 01 00
            Assert.AreEqual(0, hdr[0], "ICO reserved[0]");
            Assert.AreEqual(0, hdr[1], "ICO reserved[1]");
            Assert.AreEqual(1, hdr[2], "ICO type lo");
            Assert.AreEqual(0, hdr[3], "ICO type hi");
        }

        [TestMethod]
        public void Icon_Save_CanBeReloaded()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            using var icon = new Icon(ms);

            using var saved = new MemoryStream();
            icon.Save(saved);
            saved.Position = 0;

            using var reloaded = new Icon(saved);
            Assert.AreEqual(icon.Width,  reloaded.Width,  "Width after round-trip");
            Assert.AreEqual(icon.Height, reloaded.Height, "Height after round-trip");
        }

        // ── Icon — disposal ────────────────────────────────────────────────────

        [TestMethod]
        public void Icon_Dispose_ThrowsOnToBitmap()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            var icon = new Icon(ms);
            icon.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => icon.ToBitmap());
        }

        [TestMethod]
        public void Icon_Dispose_ThrowsOnSave()
        {
            using var ms   = new MemoryStream(Make2x2RedIcoBytes());
            var icon = new Icon(ms);
            icon.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => icon.Save(new MemoryStream()));
        }

        // ── Icon — static factories ────────────────────────────────────────────

        [TestMethod]
        public void Icon_FromHandle_ThrowsPlatformNotSupported()
        {
            Assert.ThrowsException<PlatformNotSupportedException>(
                () => Icon.FromHandle(IntPtr.Zero));
        }

        [TestMethod]
        public void Icon_ExtractAssociatedIcon_ReturnsNull()
        {
            var icon = Icon.ExtractAssociatedIcon("nonexistent.exe");
            Assert.IsNull(icon);
        }

        // ── SystemIcons ────────────────────────────────────────────────────────

        [TestMethod]
        public void SystemIcons_AllProperties_AreNotNull()
        {
            Assert.IsNotNull(SystemIcons.Application);
            Assert.IsNotNull(SystemIcons.Error);
            Assert.IsNotNull(SystemIcons.Information);
            Assert.IsNotNull(SystemIcons.Question);
            Assert.IsNotNull(SystemIcons.Warning);
            Assert.IsNotNull(SystemIcons.WinLogo);
            Assert.IsNotNull(SystemIcons.Shield);
            Assert.IsNotNull(SystemIcons.Exclamation);
            Assert.IsNotNull(SystemIcons.Hand);
            Assert.IsNotNull(SystemIcons.Asterisk);
        }

        [TestMethod]
        public void SystemIcons_Application_HasPositiveSize()
        {
            var icon = SystemIcons.Application;
            Assert.IsTrue(icon.Width  > 0, "Width > 0");
            Assert.IsTrue(icon.Height > 0, "Height > 0");
        }

        [TestMethod]
        public void SystemIcons_AreCached_SameInstanceReturned()
        {
            Assert.AreSame(SystemIcons.Error,       SystemIcons.Error,       "Error");
            Assert.AreSame(SystemIcons.Information, SystemIcons.Information, "Information");
            Assert.AreSame(SystemIcons.Warning,     SystemIcons.Warning,     "Warning");
        }

        [TestMethod]
        public void SystemIcons_ToBitmap_ReturnsValidBitmap()
        {
            using var bmp = SystemIcons.Information.ToBitmap();
            Assert.IsNotNull(bmp);
            Assert.IsTrue(bmp.Width  > 0);
            Assert.IsTrue(bmp.Height > 0);
        }

        [TestMethod]
        public void SystemIcons_DifferentIcons_HaveDifferentColors()
        {
            using var errBmp  = SystemIcons.Error.ToBitmap();
            using var infoBmp = SystemIcons.Information.ToBitmap();

            var errPx  = errBmp.GetPixel(8, 8);
            var infoPx = infoBmp.GetPixel(8, 8);

            // Error is red-ish, Information is blue-ish — they must differ.
            Assert.AreNotEqual(errPx.R, infoPx.R, "Error and Information center colors should differ.");
        }

        
    }
}
