using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Colors;

namespace DRIT.Drawing.Tests.Codecs
{
    [TestClass]
    public class CodecsTestHelpers
    {
        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        internal static void AssertColorClose(Color expected, Color actual, int tolerance = 10, string? msg = null)
        {
            int dr = Math.Abs(expected.R - actual.R);
            int dg = Math.Abs(expected.G - actual.G);
            int db = Math.Abs(expected.B - actual.B);
            Assert.IsTrue(dr <= tolerance && dg <= tolerance && db <= tolerance,
                $"Color mismatch{(msg != null ? " (" + msg + ")" : "")}: " +
                $"expected R={expected.R} G={expected.G} B={expected.B}, " +
                $"actual R={actual.R} G={actual.G} B={actual.B}, tolerance={tolerance}");
        }

        internal static Bitmap MakeRgb(int w, int h, Color c)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                bmp.SetPixel(x, y, c);
            return bmp;
        }

        internal static Bitmap MakeArgb(int w, int h, Color c)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                bmp.SetPixel(x, y, c);
            return bmp;
        }

        internal static Bitmap RoundTrip(Bitmap src, ImageFormat fmt)
        {
            using var ms = new MemoryStream();
            src.Save(ms, fmt);
            ms.Position = 0;
            return new Bitmap(ms);
        }

        

        

        

        

        

        
    }
}
