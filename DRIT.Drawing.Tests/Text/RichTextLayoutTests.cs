using System;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class RichTextLayoutTests
    {
        [TestMethod]
        public void RichTextOptions_Construction()
        {
            var opts = new RichTextOptions(FontFamily.GenericSansSerif, 18f);
            Assert.AreEqual(18f, opts.EmSize);
            Assert.AreEqual(FontStyle.Regular, opts.FontStyle);
            Assert.AreEqual(0, opts.Runs.Count);
            Assert.IsNull(opts.Fallback);
        }

        [TestMethod]
        public void RichTextRun_Construction()
        {
            var run = new RichTextRun(5, 10);
            Assert.AreEqual(5, run.Start);
            Assert.AreEqual(10, run.Length);
            Assert.IsNull(run.FontFamily);
            Assert.IsNull(run.FontStyle);
            Assert.IsNull(run.EmSize);
            Assert.IsNull(run.Brush);
        }

        [TestMethod]
        public void RichTextRun_WithOverrides()
        {
            var run = new RichTextRun(0, 5)
            {
                FontStyle = FontStyle.Bold,
                EmSize = 24f,
                Brush = new SolidBrush(Color.Red)
            };
            Assert.AreEqual(FontStyle.Bold, run.FontStyle);
            Assert.AreEqual(24f, run.EmSize);
            Assert.IsNotNull(run.Brush);
        }

        [TestMethod]
        public void RichTextRun_NegativeStart_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RichTextRun(-1, 5));
        }

        [TestMethod]
        public void RichTextRun_NegativeLength_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RichTextRun(0, -1));
        }

        [TestMethod]
        public void DrawRichText_DoesNotThrow()
        {
            using var bmp = new Bitmap(400, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 400, 200);

            var rich = new RichTextOptions(FontFamily.GenericSansSerif, 16f);
            rich.Runs.Add(new RichTextRun(0, 6));
            rich.Runs.Add(new RichTextRun(6, 5) { FontStyle = FontStyle.Bold });

            try
            {
                g.DrawRichText("Hello World", rich, new SolidBrush(Color.Black),
                    new RectangleF(10, 10, 380, 180));
            }
            catch (Exception ex)
            {
                Assert.Fail("DrawRichText threw: " + ex.Message);
            }
        }

        [TestMethod]
        public void MeasureRichText_ReturnsPositiveSize()
        {
            using var bmp = new Bitmap(400, 200);
            using var g = Graphics.FromImage(bmp);

            var rich = new RichTextOptions(FontFamily.GenericSansSerif, 16f);
            var size = g.MeasureRichText("Hello World", rich, new SizeF(380, 180));
            Assert.IsTrue(size.Width > 0);
            Assert.IsTrue(size.Height > 0);
        }

        [TestMethod]
        public void MeasureRichText_EmptyString_ReturnsZero()
        {
            using var bmp = new Bitmap(400, 200);
            using var g = Graphics.FromImage(bmp);

            var rich = new RichTextOptions(FontFamily.GenericSansSerif, 16f);
            var size = g.MeasureRichText("", rich, new SizeF(380, 180));
            Assert.AreEqual(0f, size.Width);
            Assert.AreEqual(0f, size.Height);
        }

        [TestMethod]
        public void DrawString_WithBidi_DoesNotThrow()
        {
            using var bmp = new Bitmap(400, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 400, 200);

            var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft);
            var font = new DRIT.Drawing.Text.Font(FontFamily.GenericSansSerif, 16f);

            try
            {
                g.DrawString("\u05E9\u05DC\u05D5\u05DD", font,
                    new SolidBrush(Color.Black),
                    new RectangleF(10, 10, 380, 180), sf);
            }
            catch (Exception ex)
            {
                Assert.Fail("DrawString with bidi threw: " + ex.Message);
            }
        }

        [TestMethod]
        public void Graphics_FontFallback_Property()
        {
            using var bmp = new Bitmap(100, 100);
            using var g = Graphics.FromImage(bmp);
            Assert.IsNull(g.FontFallback);
            var chain = new FontFallbackChain();
            g.FontFallback = chain;
            Assert.AreSame(chain, g.FontFallback);
        }
    }
}