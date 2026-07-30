using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Text;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class Batch12Tests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static string GetGlyphiconsPath() =>
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "DRIT.Drawing.Tests",
                "TestAssets",
                "Fonts",
                "glyphicons-halflings-regular.ttf"));

        /// <summary>Returns true when any pixel in the bitmap is not white.</summary>
        private static bool HasNonWhitePixel(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R < 255 || c.G < 255 || c.B < 255)
                        return true;
                }
            return false;
        }

        // ── MeasureString ──────────────────────────────────────────────────────

        [TestMethod]
        public void MeasureString_FallbackFont_ReturnsSizeF()
        {
            using var bmp = new Bitmap(200, 50);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f, FontStyle.Regular, GraphicsUnit.Point);

            var size = g.MeasureString("Hello", fnt);

            Assert.IsTrue(size.Width  > 0, "Width should be > 0");
            Assert.IsTrue(size.Height > 0, "Height should be > 0");
        }

        [TestMethod]
        public void MeasureString_MultiLine_HeightProportional()
        {
            using var bmp  = new Bitmap(200, 100);
            using var g    = Graphics.FromImage(bmp);
            using var fam  = FontFamily.GenericSansSerif;
            using var fnt  = new Drawing.Text.Font(fam, 12f, FontStyle.Regular, GraphicsUnit.Point);

            var area = new SizeF(200, 100);
            var s1   = g.MeasureString("Hello", fnt, area, null);
            var s2   = g.MeasureString("Hello\nWorld", fnt, area, null);

            Assert.IsTrue(s2.Height >= s1.Height * 1.5f,
                $"Two-line height ({s2.Height}) should be at least 1.5× single-line ({s1.Height})");
        }

        [TestMethod]
        public void MeasureString_Empty_ReturnsSizeEmpty()
        {
            using var bmp = new Bitmap(100, 50);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f);

            var size = g.MeasureString("", fnt);

            Assert.AreEqual(0f, size.Width,  0.001f);
            Assert.AreEqual(0f, size.Height, 0.001f);
        }

        [TestMethod]
        public void MeasureString_PointOverload_ReturnsSizeF()
        {
            using var bmp = new Bitmap(200, 50);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f);

            var size = g.MeasureString("Hello", fnt, new PointF(0, 0), null);

            Assert.IsTrue(size.Width  > 0);
            Assert.IsTrue(size.Height > 0);
        }

        [TestMethod]
        public void MeasureString_ConstrainedWidth_LimitsWidth()
        {
            using var bmp = new Bitmap(200, 200);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f);
            var sf = new StringFormat();

            // Constrain to narrow width so long string wraps.
            var size = g.MeasureString("Hello World Foo Bar", fnt, new SizeF(80f, 1000f), sf);

            Assert.IsTrue(size.Width  <= 80f + 1f, $"Width {size.Width} should be ≤ 81");
            Assert.IsTrue(size.Height > 0);
        }

        // ── DrawString (no-throw + smoke tests) ───────────────────────────────

        [TestMethod]
        public void DrawString_FallbackFont_DoesNotThrow()
        {
            using var bmp = new Bitmap(100, 30);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f);
            using var br  = new SolidBrush(Color.Black);

            // Must not throw even when no real IFont is available.
            g.DrawString("Hi", fnt, br, 0f, 0f);
        }

        [TestMethod]
        public void DrawString_PointF_DoesNotThrow()
        {
            using var bmp = new Bitmap(100, 30);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f);
            using var br  = new SolidBrush(Color.Black);

            g.DrawString("Hi", fnt, br, new PointF(2f, 2f));
        }

        [TestMethod]
        public void DrawString_RectangleF_DoesNotThrow()
        {
            using var bmp = new Bitmap(200, 50);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f);
            using var br  = new SolidBrush(Color.Black);

            g.DrawString("Hello World", fnt, br, new RectangleF(0, 0, 200, 50));
        }

        [TestMethod]
        public void DrawString_WithGlyphicons_ProducesNonWhitePixels()
        {
            string path = GetGlyphiconsPath();
            if (!File.Exists(path)) Assert.Inconclusive("Glyphicons font not found.");

            using var pfc = new PrivateFontCollection();
            pfc.AddFontFile(path);

            using var fam = pfc.Families[0];
            using var fnt = new Drawing.Text.Font(fam, 32f, FontStyle.Regular, GraphicsUnit.Point);
            using var bmp = new Bitmap(200, 80);
            using var g   = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            using var br = new SolidBrush(Color.Black);
            g.DrawString("\u0041", fnt, br, 0f, 0f);  // glyph for 'A'

            Assert.IsTrue(HasNonWhitePixel(bmp), "DrawString with a real font must write non-white pixels.");
        }

        [TestMethod]
        public void DrawString_WithStringFormat_DoesNotThrow()
        {
            using var bmp = new Bitmap(200, 50);
            using var g   = Graphics.FromImage(bmp);
            using var fam = FontFamily.GenericSansSerif;
            using var fnt = new Drawing.Text.Font(fam, 12f);
            using var br  = new SolidBrush(Color.Black);
            var sf = new StringFormat { Alignment = StringAlignment.Center };

            g.DrawString("Hello", fnt, br, new RectangleF(0, 0, 200, 50), sf);
        }

        // ── Text layout behavior ───────────────────────────────────────────────

        [TestMethod]
        public void TextLayout_HardLineBreaks_ProduceTwoLines()
        {
            using var bmp  = new Bitmap(200, 200);
            using var g    = Graphics.FromImage(bmp);
            using var fam  = FontFamily.GenericSansSerif;
            using var fnt  = new Drawing.Text.Font(fam, 12f);

            // Two-line text should be taller than single-line text.
            var s1 = g.MeasureString("Hello", fnt, new SizeF(200, 200), null);
            var s2 = g.MeasureString("Hello\nWorld", fnt, new SizeF(200, 200), null);

            Assert.IsTrue(s2.Height > s1.Height,
                "Hard newline must increase measured height.");
        }

        [TestMethod]
        public void TextLayout_WordWrap_BreaksAtMaxWidth()
        {
            using var bmp  = new Bitmap(200, 200);
            using var g    = Graphics.FromImage(bmp);
            using var fam  = FontFamily.GenericSansSerif;
            using var fnt  = new Drawing.Text.Font(fam, 12f);

            // A narrow rect forces wrapping → more lines → greater height.
            var narrowArea = new SizeF(40, 200);
            var wideArea   = new SizeF(500, 200);
            var sf         = new StringFormat();   // default: word-wrap enabled

            var sNarrow = g.MeasureString("Hello World", fnt, narrowArea, sf);
            var sWide   = g.MeasureString("Hello World", fnt, wideArea,   sf);

            Assert.IsTrue(sNarrow.Height >= sWide.Height,
                "Narrow rect must produce equal or greater height due to wrapping.");
        }

        [TestMethod]
        public void TextLayout_NoWrap_FitsSingleLine()
        {
            using var bmp  = new Bitmap(200, 200);
            using var g    = Graphics.FromImage(bmp);
            using var fam  = FontFamily.GenericSansSerif;
            using var fnt  = new Drawing.Text.Font(fam, 12f);

            var area    = new SizeF(30, 200);
            var sfWrap  = new StringFormat();
            var sfNoWrap = new StringFormat(StringFormatFlags.NoWrap);

            var sWrap   = g.MeasureString("Hello World", fnt, area, sfWrap);
            var sNoWrap = g.MeasureString("Hello World", fnt, area, sfNoWrap);

            Assert.IsTrue(sNoWrap.Height <= sWrap.Height + 1f,
                "NoWrap flag must produce a single line (no extra height).");
        }

        [TestMethod]
        public void TextLayout_CenterAlignment_DoesNotChangeHeight()
        {
            using var bmp  = new Bitmap(200, 100);
            using var g    = Graphics.FromImage(bmp);
            using var fam  = FontFamily.GenericSansSerif;
            using var fnt  = new Drawing.Text.Font(fam, 12f);
            var area = new SizeF(200, 100);

            var sfLeft   = new StringFormat { Alignment = StringAlignment.Near };
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center };

            var sLeft   = g.MeasureString("Hello", fnt, area, sfLeft);
            var sCenter = g.MeasureString("Hello", fnt, area, sfCenter);

            Assert.AreEqual(sLeft.Height, sCenter.Height, 0.001f,
                "Horizontal alignment must not change height.");
        }

        // ── Underline / Strikeout decorations ─────────────────────────────────

        [TestMethod]
        public void Underline_DrawsDecoration()
        {
            string path = GetGlyphiconsPath();
            if (!File.Exists(path)) Assert.Inconclusive("Glyphicons font not found.");

            using var pfc = new PrivateFontCollection();
            pfc.AddFontFile(path);
            using var fam = pfc.Families[0];
            using var fnt = new Drawing.Text.Font(fam, 32f, FontStyle.Underline, GraphicsUnit.Point);
            using var bmp = new Bitmap(300, 80);
            using var g   = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            using var br = new SolidBrush(Color.Black);
            g.DrawString("\u0041A", fnt, br, 0f, 0f);

            Assert.IsTrue(HasNonWhitePixel(bmp), "Underlined text should produce non-white pixels.");
        }

        [TestMethod]
        public void Strikeout_DrawsDecoration()
        {
            string path = GetGlyphiconsPath();
            if (!File.Exists(path)) Assert.Inconclusive("Glyphicons font not found.");

            using var pfc = new PrivateFontCollection();
            pfc.AddFontFile(path);
            using var fam = pfc.Families[0];
            using var fnt = new Drawing.Text.Font(fam, 32f, FontStyle.Strikeout, GraphicsUnit.Point);
            using var bmp = new Bitmap(300, 80);
            using var g   = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            using var br = new SolidBrush(Color.Black);
            g.DrawString("\u0041A", fnt, br, 0f, 0f);

            Assert.IsTrue(HasNonWhitePixel(bmp), "Strikeout text should produce non-white pixels.");
        }

        // ── GraphicsPath.AddString ─────────────────────────────────────────────

        [TestMethod]
        public void AddString_FallbackFont_DoesNotThrow()
        {
            using var fam  = FontFamily.GenericSansSerif;
            using var path = new GraphicsPath();

            // No real IFont → returns without adding points (fallback path, no throw).
            path.AddString("Hi", fam, (int)FontStyle.Regular, 20f,
                new PointF(0, 0), null);
        }

        [TestMethod]
        public void AddString_WithGlyphicons_PopulatesPath()
        {
            string ttfPath = GetGlyphiconsPath();
            if (!File.Exists(ttfPath)) Assert.Inconclusive("Glyphicons font not found.");

            using var pfc  = new PrivateFontCollection();
            pfc.AddFontFile(ttfPath);
            using var fam  = pfc.Families[0];
            using var gp   = new GraphicsPath();

            gp.AddString("\u0041", fam, (int)FontStyle.Regular, 48f,
                new PointF(0, 50), null);

            Assert.IsTrue(gp.PointCount > 0,
                "AddString with a real glyph font should add path points.");
        }

        [TestMethod]
        public void AddString_RectangleOverload_WithGlyphicons_PopulatesPath()
        {
            string ttfPath = GetGlyphiconsPath();
            if (!File.Exists(ttfPath)) Assert.Inconclusive("Glyphicons font not found.");

            using var pfc = new PrivateFontCollection();
            pfc.AddFontFile(ttfPath);
            using var fam = pfc.Families[0];
            using var gp  = new GraphicsPath();

            gp.AddString("\u0041\u0042", fam, (int)FontStyle.Regular, 48f,
                new RectangleF(0, 0, 200, 100), null);

            Assert.IsTrue(gp.PointCount > 0,
                "AddString (rect overload) should add path points.");
        }
    }
}
