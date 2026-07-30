using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Codecs.Jpeg
{
    [TestClass]
    public class JpegDecoderTests
    {
        private const string BaselineGray8x8JpegBase64 =
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/wAALCAAIAAgBAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/APDPhJ/yx/Cv/9k=";

        private static byte[] DecodeFixture(string base64) => Convert.FromBase64String(base64);

        private static void AssertColorClose(Color expected, Color actual, int tolerance, string? message = null)
        {
            int dr = Math.Abs(expected.R - actual.R);
            int dg = Math.Abs(expected.G - actual.G);
            int db = Math.Abs(expected.B - actual.B);
            Assert.IsTrue(
                dr <= tolerance && dg <= tolerance && db <= tolerance,
                $"Color mismatch{(message == null ? string.Empty : " (" + message + ")")}: expected ({expected.R},{expected.G},{expected.B}), actual ({actual.R},{actual.G},{actual.B}), tolerance={tolerance}");
        }

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

        [TestMethod]
        public void JpegDecoder_DecodeSolidColor8x8_CenterPixelClose()
        {
            using var src = MakeBitmap(8, 8, (_, _) => Color.FromArgb(255, 180, 90, 40));
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Jpeg);
            ms.Position = 0;

            using var dst = new Bitmap(ms);
            Assert.AreEqual(8, dst.Width);
            Assert.AreEqual(8, dst.Height);
            AssertColorClose(Color.FromArgb(255, 180, 90, 40), dst.GetPixel(4, 4), tolerance: 15);
        }

        [TestMethod]
        public void JpegDecoder_DecodeGrayscaleJpeg_CenterPixelIsGray()
        {
            using var ms = new MemoryStream(DecodeFixture(BaselineGray8x8JpegBase64));

            using var dst = new Bitmap(ms);
            var px = dst.GetPixel(4, 4);
            Assert.IsTrue(Math.Abs(px.R - px.G) <= 2);
            Assert.IsTrue(Math.Abs(px.G - px.B) <= 2);
            Assert.IsTrue(px.R >= 70 && px.R <= 110);
        }

        [TestMethod]
        public void JpegDecoder_Decode16x16_BlockBoundaryImage_StitchesCorrectly()
        {
            using var src = MakeBitmap(16, 16, (x, _) => x < 8
                ? Color.FromArgb(255, 220, 30, 30)
                : Color.FromArgb(255, 30, 220, 30));

            using var ms = new MemoryStream();
            var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
            src.Save(ms, ImageFormat.Jpeg, parameters);
            ms.Position = 0;

            using var dst = new Bitmap(ms);
            AssertColorClose(Color.FromArgb(255, 220, 30, 30), dst.GetPixel(4, 8), tolerance: 25, message: "left half");
            AssertColorClose(Color.FromArgb(255, 30, 220, 30), dst.GetPixel(12, 8), tolerance: 25, message: "right half");
        }

        [TestMethod]
        public void JpegDecoder_RoundTrip_OwnEncodeOwnDecode_AverageDeltaWithinTolerance()
        {
            using var src = MakeBitmap(17, 13, (x, y) =>
                Color.FromArgb(
                    255,
                    (x * 17 + y * 7) % 256,
                    (x * 9 + y * 19) % 256,
                    (x * 5 + y * 11) % 256));

            using var ms = new MemoryStream();
            var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, 85L);
            src.Save(ms, ImageFormat.Jpeg, parameters);
            byte[] jpegBytes = ms.ToArray();

            using var own = new Bitmap(new MemoryStream(jpegBytes));
            int[] sampleXs = { 1, own.Width / 2, own.Width - 2 };
            int[] sampleYs = { 1, own.Height / 2, own.Height - 2 };

            int totalDelta = 0;
            int count = 0;

            foreach (int y in sampleYs)
            {
                foreach (int x in sampleXs)
                {
                    Color expected = src.GetPixel(x, y);
                    var actual = own.GetPixel(x, y);
                    totalDelta += Math.Abs(expected.R - actual.R);
                    totalDelta += Math.Abs(expected.G - actual.G);
                    totalDelta += Math.Abs(expected.B - actual.B);
                    count++;
                }
            }

            double averageDelta = totalDelta / (double)(count * 3);
            Assert.IsTrue(averageDelta <= 25.0, $"Average channel delta too high: {averageDelta:0.00}");
        }
    }
}
