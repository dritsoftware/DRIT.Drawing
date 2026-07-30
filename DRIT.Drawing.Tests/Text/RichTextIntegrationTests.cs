using System.IO;
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
    public class RichTextIntegrationTests
    {
        private static readonly string s_outDir = Path.Combine(
            Directory.GetCurrentDirectory(), "Out");

        private static void Save(Bitmap bmp, string name)
        {
            Directory.CreateDirectory(s_outDir);
            bmp.Save(Path.Combine(s_outDir, name + ".png"), ImageFormat.Png);
        }

        [TestMethod]
        public void RichText_MixedFonts()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var rich = new RichTextOptions(FontFamily.GenericSansSerif, 18f);
            rich.Runs.Add(new RichTextRun(0, 6));
            rich.Runs.Add(new RichTextRun(6, 5) { FontStyle = FontStyle.Bold });

            g.DrawRichText("Hello World", rich, new SolidBrush(Color.Black),
                new RectangleF(20, 20, 560, 160));
            Save(bmp, "RichText_MixedFonts");
        }

        [TestMethod]
        public void RichText_MixedSizes()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var rich = new RichTextOptions(FontFamily.GenericSansSerif, 14f);
            rich.Runs.Add(new RichTextRun(0, 6));
            rich.Runs.Add(new RichTextRun(6, 3) { EmSize = 28f });
            rich.Runs.Add(new RichTextRun(9, 6));

            g.DrawRichText("Small BIG Small", rich, new SolidBrush(Color.Black),
                new RectangleF(20, 20, 560, 160));
            Save(bmp, "RichText_MixedSizes");
        }

        [TestMethod]
        public void RichText_MixedColors()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var rich = new RichTextOptions(FontFamily.GenericSansSerif, 18f);
            rich.Runs.Add(new RichTextRun(0, 4) { Brush = new SolidBrush(Color.Red) });
            rich.Runs.Add(new RichTextRun(4, 6) { Brush = new SolidBrush(Color.Green) });
            rich.Runs.Add(new RichTextRun(10, 4) { Brush = new SolidBrush(Color.Blue) });

            g.DrawRichText("RedGreenBlue", rich, new SolidBrush(Color.Black),
                new RectangleF(20, 20, 560, 160));
            Save(bmp, "RichText_MixedColors");
        }

        [TestMethod]
        public void Bidi_PureHebrew()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft);
            var font = new DRIT.Drawing.Text.Font(FontFamily.GenericSansSerif, 24f);
            g.DrawString("\u05E9\u05DC\u05D5\u05DD \u05E2\u05D5\u05DC\u05DD", font,
                new SolidBrush(Color.Black), new RectangleF(20, 20, 560, 160), sf);
            Save(bmp, "Bidi_PureHebrew");
        }

        [TestMethod]
        public void Bidi_Mixed_LatinInRTL()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft);
            var font = new DRIT.Drawing.Text.Font(FontFamily.GenericSansSerif, 24f);
            g.DrawString("\u05E9\u05DC\u05D5\u05DD Hello \u05E2\u05D5\u05DC\u05DD", font,
                new SolidBrush(Color.Black), new RectangleF(20, 20, 560, 160), sf);
            Save(bmp, "Bidi_Mixed_LatinInRTL");
        }

        [TestMethod]
        public void Fallback_LatinPlusCJK()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var font = new DRIT.Drawing.Text.Font(FontFamily.GenericSansSerif, 24f);
            g.DrawString("Hello \u4E16\u754C", font,
                new SolidBrush(Color.Black), 20f, 20f);
            Save(bmp, "Fallback_LatinPlusCJK");
        }

        [TestMethod]
        public void AddString_Bidi_Fallback()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var path = new GraphicsPath();
            path.AddString("Hello \u4E16\u754C", FontFamily.GenericSansSerif,
                (int)FontStyle.Regular, 24f, new PointF(20, 20), null);
            g.FillPath(new SolidBrush(Color.Black), path);
            Save(bmp, "AddString_Bidi_Fallback");
        }

        [TestMethod]
        public void AddRichText_ToPath()
        {
            using var bmp = new Bitmap(600, 200);
            using var g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, 600, 200);

            var rich = new RichTextOptions(FontFamily.GenericSansSerif, 18f);
            rich.Runs.Add(new RichTextRun(0, 6));
            rich.Runs.Add(new RichTextRun(6, 5) { FontStyle = FontStyle.Italic });

            var path = new GraphicsPath();
            path.AddRichText("Hello World", FontFamily.GenericSansSerif,
                (int)FontStyle.Regular, 18f, rich,
                new PointF(20, 20), null);
            g.FillPath(new SolidBrush(Color.Black), path);
            Save(bmp, "AddRichText_ToPath");
        }
    }
}