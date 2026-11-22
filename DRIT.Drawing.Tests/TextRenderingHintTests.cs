using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class TextRenderingHintTests
    {
        /// <summary>Number of distinct colours in a drawing of some text: 2 means hard edges, more means smoothed edges.</summary>
        private static int DistinctColours(SmoothingMode smoothing, TextRenderingHint hint)
        {
            using var bmp = new Bitmap(300, 60);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.SmoothingMode = smoothing;
            g.TextRenderingHint = hint;
            using var family = FontFamily.GenericSansSerif;
            using var font = new DRIT.Drawing.Text.Font(family, 24f, FontStyle.Regular, GraphicsUnit.Point);
            using var brush = new SolidBrush(Color.Black);
            g.DrawString("Hello Wxyz", font, brush, 5, 5);

            var colours = new HashSet<int>();
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    colours.Add(bmp.GetPixel(x, y).ToArgb());
            return colours.Count;
        }

        private static void RequireFont()
        {
            if (GenericFontResolver.Resolve(GenericFontFamilies.SansSerif) == null)
                Assert.Inconclusive("No stand-in font for the generic sans-serif family is installed.");
        }

        [DataTestMethod]
        [DataRow(TextRenderingHint.AntiAlias)]
        [DataRow(TextRenderingHint.AntiAliasGridFit)]
        [DataRow(TextRenderingHint.ClearTypeGridFit)]
        public void AntiAliasingHints_SmoothGlyphEdges_EvenWithoutSmoothingMode(TextRenderingHint hint)
        {
            RequireFont();
            Assert.IsTrue(DistinctColours(SmoothingMode.None, hint) > 2);
        }

        [DataTestMethod]
        [DataRow(TextRenderingHint.SingleBitPerPixel)]
        [DataRow(TextRenderingHint.SingleBitPerPixelGridFit)]
        public void SingleBitHints_KeepGlyphEdgesHard_EvenWithAntiAliasSmoothing(TextRenderingHint hint)
        {
            RequireFont();
            Assert.AreEqual(2, DistinctColours(SmoothingMode.AntiAlias, hint));
        }

        [TestMethod]
        public void SystemDefaultHint_FollowsTheSmoothingMode()
        {
            RequireFont();
            Assert.AreEqual(2, DistinctColours(SmoothingMode.None, TextRenderingHint.SystemDefault));
            Assert.IsTrue(DistinctColours(SmoothingMode.AntiAlias, TextRenderingHint.SystemDefault) > 2);
        }

        [TestMethod]
        public void SmoothingMode_IsRestoredAfterDrawingText()
        {
            RequireFont();
            using var bmp = new Bitmap(100, 40);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            using var family = FontFamily.GenericSansSerif;
            using var font = new DRIT.Drawing.Text.Font(family, 12f, FontStyle.Regular, GraphicsUnit.Point);
            using var brush = new SolidBrush(Color.Black);
            g.DrawString("Hi", font, brush, 2, 2);
            Assert.AreEqual(SmoothingMode.None, g.SmoothingMode);
        }
    }
}
