using System;
using System.IO;
using System.Reflection;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Codecs.Jpeg
{
    [TestClass]
    public class JpegEncoderTests
    {
        private static Bitmap MakeBitmap(int width, int height, Func<int, int, Color> generator)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    bmp.SetPixel(x, y, generator(x, y));
            }

            return bmp;
        }

        private static void AssertColorClose(Color expected, Color actual, int tolerance, string? message = null)
        {
            int dr = Math.Abs(expected.R - actual.R);
            int dg = Math.Abs(expected.G - actual.G);
            int db = Math.Abs(expected.B - actual.B);
            Assert.IsTrue(
                dr <= tolerance && dg <= tolerance && db <= tolerance,
                $"Color mismatch{(message == null ? string.Empty : " (" + message + ")")}: expected ({expected.R},{expected.G},{expected.B}), actual ({actual.R},{actual.G},{actual.B}), tolerance={tolerance}");
        }

        [TestMethod]
        public void JpegEncoder_EncodeSolidColor8x8_DecodesWithinTolerance()
        {
            using var src = MakeBitmap(8, 8, (_, _) => Color.FromArgb(255, 180, 110, 40));
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Jpeg);

            byte[] bytes = ms.ToArray();
            Assert.IsTrue(bytes.Length > 64);
            Assert.AreEqual(0xFF, bytes[0]);
            Assert.AreEqual(0xD8, bytes[1]);

            ms.Position = 0;
            using var dst = new Bitmap(ms);
            Assert.AreEqual(8, dst.Width);
            Assert.AreEqual(8, dst.Height);
            AssertColorClose(Color.FromArgb(255, 180, 110, 40), dst.GetPixel(4, 4), tolerance: 20);
        }

        [TestMethod]
        public void JpegEncoder_Quality90_IsSmallerThanQuality100()
        {
            using var src = MakeBitmap(64, 64, (x, y) => Color.FromArgb(
                255,
                (x * 3 + y * 5) % 256,
                (x * 7 + y * 2) % 256,
                (x * 11 + y * 13) % 256));

            byte[] q100;
            byte[] q90;

            using (var ms100 = new MemoryStream())
            {
                var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(Encoder.Quality, 100L);
                src.Save(ms100, ImageFormat.Jpeg, ep);
                q100 = ms100.ToArray();
            }

            using (var ms90 = new MemoryStream())
            {
                var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
                src.Save(ms90, ImageFormat.Jpeg, ep);
                q90 = ms90.ToArray();
            }

            Assert.IsTrue(q90.Length < q100.Length, $"Expected quality=90 to be smaller than quality=100, but got {q90.Length} >= {q100.Length}");
        }

        [TestMethod]
        public void JpegEncoder_RoundTrip_OwnEncodeOwnDecode_AverageDeltaWithinTolerance()
        {
            using var src = MakeBitmap(24, 18, (x, y) => Color.FromArgb(
                255,
                (x * 17 + y * 9) % 256,
                (x * 5 + y * 23) % 256,
                (x * 13 + y * 7) % 256));

            using var ms = new MemoryStream();
            var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
            src.Save(ms, ImageFormat.Jpeg, ep);
            ms.Position = 0;

            using var dst = new Bitmap(ms);

            int[] sampleXs = { 0, src.Width / 3, src.Width / 2, (src.Width * 2) / 3, src.Width - 1 };
            int[] sampleYs = { 0, src.Height / 3, src.Height / 2, (src.Height * 2) / 3, src.Height - 1 };

            int totalDelta = 0;
            int count = 0;

            foreach (int y in sampleYs)
            {
                foreach (int x in sampleXs)
                {
                    Color expected = src.GetPixel(x, y);
                    Color actual = dst.GetPixel(x, y);
                    totalDelta += Math.Abs(expected.R - actual.R);
                    totalDelta += Math.Abs(expected.G - actual.G);
                    totalDelta += Math.Abs(expected.B - actual.B);
                    count++;
                }
            }

            double averageDelta = totalDelta / (double)(count * 3);
            Assert.IsTrue(averageDelta <= 22.0, $"Average channel delta too high: {averageDelta:0.00}");
        }
    }
}
