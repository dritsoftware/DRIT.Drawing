using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class AdvancedTests
    {
       

        

        

        [TestMethod]
        public void Bitmap_PngBmpRoundTrip_CloneAndRotateFlip()
        {
            using var src = new Bitmap(16, 12);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    src.SetPixel(x, y, Color.FromArgb(255, (x * 17) % 256, (y * 23) % 256, (x * 7 + y * 5) % 256));
                }
            }

            using var pngStream = new MemoryStream();
            src.Save(pngStream, ImageFormat.Png);
            pngStream.Position = 0;
            using var png = new Bitmap(pngStream);
            Assert.AreEqual(src.Width, png.Width);
            Assert.AreEqual(src.Height, png.Height);
            Assert.AreEqual(src.GetPixel(5, 4).ToArgb(), png.GetPixel(5, 4).ToArgb());

            using var bmpStream = new MemoryStream();
            src.Save(bmpStream, ImageFormat.Bmp);
            bmpStream.Position = 0;
            using var bmp = new Bitmap(bmpStream);
            Assert.AreEqual(src.GetPixel(9, 8).ToArgb(), bmp.GetPixel(9, 8).ToArgb());

            using var clone = src.Clone(new Rectangle(4, 3, 6, 5), PixelFormat.Format32bppArgb);
            Assert.AreEqual(6, clone.Width);
            Assert.AreEqual(5, clone.Height);
            Assert.AreEqual(src.GetPixel(4, 3).ToArgb(), clone.GetPixel(0, 0).ToArgb());

            clone.RotateFlip(RotateFlipType.Rotate90FlipNone);
            Assert.AreEqual(5, clone.Width);
            Assert.AreEqual(6, clone.Height);
        }

        [TestMethod]
        public void DrawLine_BresenhamCorePixelsAreCovered()
        {
            using var bmp = new Bitmap(100, 100);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.None;
            using var pen = new Pen(Color.Black, 1f);

            int x0 = 7;
            int y0 = 13;
            int x1 = 83;
            int y1 = 57;
            g.DrawLine(pen, x0, y0, x1, y1);

            int total = 0;
            int hit = 0;
            foreach (Point p in EnumerateBresenham(x0, y0, x1, y1))
            {
                total++;
                if (HasInkInNeighborhood(bmp, p.X, p.Y, 1))
                    hit++;
            }

            Assert.IsTrue(hit >= (int)(total * 0.95f), $"Expected at least 95% Bresenham core hits in neighborhood, got {hit}/{total}.");
        }

        [TestMethod]
        public void FillEllipse_PixelAreaCloseToAnalyticArea()
        {
            using var bmp = new Bitmap(220, 180);
            using var g = Graphics.FromImage(bmp);
            using var brush = new SolidBrush(Color.Red);

            var rect = new Rectangle(40, 30, 120, 80);
            g.FillEllipse(brush, rect);

            int filled = CountPixels(bmp, c => c.A > 0 && c.R > 100);
            double expected = Math.PI * (rect.Width / 2.0) * (rect.Height / 2.0);
            double relativeError = Math.Abs(filled - expected) / expected;

            Assert.IsTrue(relativeError <= 0.22,
                $"Filled area too far from analytic ellipse area. filled={filled}, expected={expected:0.00}, relErr={relativeError:0.000}");
        }

        [TestMethod]
        public void DrawString_MeasureA_TracksGlyphAdvanceReasonably()
        {
            using var bmp = new Bitmap(240, 80);
            using var g = Graphics.FromImage(bmp);
            using var family = FontFamily.GenericSansSerif;
            using var font = new Drawing.Text.Font(family, 20f, FontStyle.Regular, GraphicsUnit.Point);
            using var brush = new SolidBrush(Color.Black);

            float wA = g.MeasureString("A", font).Width;
            float wAA = g.MeasureString("AA", font).Width;

            Assert.IsTrue(wA > 0f, "Width of 'A' should be positive.");
            Assert.IsTrue(wAA > wA, "Width of 'AA' should be larger than 'A'.");

            float ratio = wAA / wA;
            Assert.IsTrue(ratio >= 1.7f && ratio <= 2.3f, $"Unexpected width ratio for AA/A: {ratio:0.00}");

            // Smoke-check drawing path in integration suite.
            g.DrawString("A", font, brush, 10f, 10f);
        }

        [TestMethod]
        public void Gradient_LinearBrush_StartMidEndExpected()
        {
            using var brush = new LinearGradientBrush(
                new PointF(0f, 0f),
                new PointF(100f, 0f),
                Color.Red,
                Color.Blue);
            brush.WrapMode = WrapMode.Clamp;

            Color start = DRIT.Drawing.Rendering.BrushSampler.Sample(brush, 0f, 0f, InterpolationMode.NearestNeighbor);
            Color mid = DRIT.Drawing.Rendering.BrushSampler.Sample(brush, 50f, 0f, InterpolationMode.NearestNeighbor);
            Color end = DRIT.Drawing.Rendering.BrushSampler.Sample(brush, 100f, 0f, InterpolationMode.NearestNeighbor);

            Assert.IsTrue(start.R > start.B, "Start of gradient should be red-dominant.");
            Assert.IsTrue(end.B > end.R, "End of gradient should be blue-dominant.");
            Assert.IsTrue(mid.R > 40 && mid.B > 40, "Middle of gradient should contain both red and blue.");
            Assert.IsTrue(Math.Abs(mid.R - mid.B) < 120, "Middle of gradient should be blended.");
        }

        [TestMethod]
        public void Clip_SetAndIntersect_RestrictsFill()
        {
            using var bmp = new Bitmap(100, 100);
            using var g = Graphics.FromImage(bmp);

            g.SetClip(new Rectangle(0, 0, 40, 40));
            g.IntersectClip(new Rectangle(20, 20, 40, 40));
            Assert.IsTrue(g.IsVisible(new PointF(25f, 25f)), "Intersection should include (25,25).");
            Assert.IsFalse(g.IsVisible(new PointF(10f, 10f)), "Intersection should exclude (10,10).");

            using var fillBmp = new Bitmap(100, 100);
            using var fillG = Graphics.FromImage(fillBmp);
            using var brush = new SolidBrush(Color.Red);
            fillG.SetClip(new Rectangle(0, 0, 4, 4));
            fillG.FillRectangle(brush, new Rectangle(0, 0, 100, 100));

            Assert.IsTrue(fillBmp.GetPixel(1, 1).A > 0, "Clipped fill area should be filled.");
            Assert.AreEqual(0, fillBmp.GetPixel(8, 8).A, "Outside clip should remain empty.");
            Assert.AreEqual(0, fillBmp.GetPixel(20, 2).A, "Outside horizontal range should remain empty.");
        }

        [TestMethod]
        public void Transform_Translate_ShiftsRasterOutput()
        {
            using var bmp = new Bitmap(60, 60);
            using var g = Graphics.FromImage(bmp);
            using var pen = new Pen(Color.Black, 1f);

            g.TranslateTransform(15f, 12f);
            g.DrawLine(pen, 0f, 0f, 20f, 0f);

            bool foundTranslatedStroke = false;
            for (int x = 15; x <= 35 && !foundTranslatedStroke; x++)
            {
                for (int y = 11; y <= 13; y++)
                {
                    if (bmp.GetPixel(x, y).A > 0)
                    {
                        foundTranslatedStroke = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(foundTranslatedStroke, "Translated line band should contain visible stroke pixels.");
            Assert.AreEqual(0, bmp.GetPixel(0, 0).A, "Untranslated origin should remain empty.");
        }

        [TestMethod]
        public void ImageRender_DrawImage_MatchesExpectedWithinTolerance()
        {
            using var src = new Bitmap(8, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    bool even = ((x + y) & 1) == 0;
                    src.SetPixel(x, y, even ? Color.FromArgb(255, 250, 30, 30) : Color.FromArgb(255, 20, 30, 250));
                }
            }

            using var actual = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(actual))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(src, new Rectangle(0, 0, 16, 16));
            }

            using var expected = new Bitmap(16, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    expected.SetPixel(x, y, src.GetPixel(x / 2, y / 2));
                }
            }

            double avgDelta = ComputeAverageChannelDelta(expected, actual);
            Assert.IsTrue(avgDelta <= 1.0, $"Average channel delta too high: {avgDelta:0.000}");
        }

        private static IEnumerable<Point> EnumerateBresenham(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                yield return new Point(x0, y0);
                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static int CountPixels(Bitmap bmp, Func<Color, bool> predicate)
        {
            int count = 0;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (predicate(bmp.GetPixel(x, y)))
                        count++;
                }
            }

            return count;
        }

        private static bool HasInkInNeighborhood(Bitmap bmp, int cx, int cy, int radius)
        {
            for (int y = Math.Max(0, cy - radius); y <= Math.Min(bmp.Height - 1, cy + radius); y++)
            {
                for (int x = Math.Max(0, cx - radius); x <= Math.Min(bmp.Width - 1, cx + radius); x++)
                {
                    if (bmp.GetPixel(x, y).A > 0)
                        return true;
                }
            }

            return false;
        }

        private static double ComputeAverageChannelDelta(Bitmap expected, Bitmap actual)
        {
            Assert.AreEqual(expected.Width, actual.Width);
            Assert.AreEqual(expected.Height, actual.Height);

            long total = 0;
            int channels = expected.Width * expected.Height * 3;

            for (int y = 0; y < expected.Height; y++)
            {
                for (int x = 0; x < expected.Width; x++)
                {
                    Color e = expected.GetPixel(x, y);
                    Color a = actual.GetPixel(x, y);
                    total += Math.Abs(e.R - a.R);
                    total += Math.Abs(e.G - a.G);
                    total += Math.Abs(e.B - a.B);
                }
            }

            return total / (double)channels;
        }
    }
}
