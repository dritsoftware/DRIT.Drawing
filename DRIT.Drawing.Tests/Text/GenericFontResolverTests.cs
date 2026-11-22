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
    public class GenericFontResolverTests
    {
        private static readonly GenericFontFamilies[] AllGenerics =
        {
            GenericFontFamilies.Serif, GenericFontFamilies.SansSerif, GenericFontFamilies.Monospace,
        };

        private static int CountInk(Action<Graphics, DRIT.Drawing.Text.Font, Brush> draw, FontFamily family)
        {
            using var bmp = new Bitmap(760, 360);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            using var font = new DRIT.Drawing.Text.Font(family, 24f, FontStyle.Regular, GraphicsUnit.Point);
            using var brush = new SolidBrush(Color.Black);
            draw(g, font, brush);
            int ink = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R != 255 || c.G != 255 || c.B != 255) ink++;
                }
            return ink;
        }

        private static void RequireStandIn(GenericFontFamilies generic)
        {
            if (GenericFontResolver.Resolve(generic) == null)
                Assert.Inconclusive($"No stand-in font for {generic} is installed.");
        }

        [TestMethod]
        public void Resolve_ReturnsFamilyWithRegularFace_WhenAvailable()
        {
            foreach (GenericFontFamilies generic in AllGenerics)
            {
                FontFamily? target = GenericFontResolver.Resolve(generic);
                if (target != null)
                    Assert.IsNotNull(target.GetFontFace(FontStyle.Regular), generic.ToString());
            }
        }

        [TestMethod]
        public void GetCandidates_IsNotEmptyForEveryGenericFamily()
        {
            foreach (GenericFontFamilies generic in AllGenerics)
                Assert.IsTrue(GenericFontResolver.GetCandidates(generic).Count > 0, generic.ToString());
        }

        [TestMethod]
        public void GenericFamilies_KeepTheirGenericNames()
        {
            Assert.AreEqual("Sans Serif", FontFamily.GenericSansSerif.Name);
            Assert.AreEqual("Serif", FontFamily.GenericSerif.Name);
            Assert.AreEqual("Monospace", FontFamily.GenericMonospace.Name);
        }

        [TestMethod]
        public void DrawStringOnPath_GenericFamily_DrawsGlyphs()
        {
            RequireStandIn(GenericFontFamilies.SansSerif);
            using var family = FontFamily.GenericSansSerif;
            int ink = CountInk((g, font, brush) =>
            {
                using var path = new GraphicsPath();
                path.AddArc(new RectangleF(100, 40, 560, 260), 180, 180);
                g.DrawStringOnPath("Text follows a path", font, brush, path);
            }, family);
            Assert.IsTrue(ink > 200, $"ink={ink}");
        }

        [TestMethod]
        public void DrawRichText_GenericFamily_DrawsGlyphs()
        {
            RequireStandIn(GenericFontFamilies.SansSerif);
            using var family = FontFamily.GenericSansSerif;
            int ink = CountInk((g, font, brush) =>
            {
                var options = new RichTextOptions(family, 24f, FontStyle.Regular);
                options.Runs.Add(new RichTextRun(0, 4) { FontStyle = FontStyle.Bold });
                g.DrawRichText("Rich text runs", options, brush, new RectangleF(20, 20, 600, 100));
            }, family);
            Assert.IsTrue(ink > 200, $"ink={ink}");
        }

        [TestMethod]
        public void GenericFamilies_RenderWithDistinctFaces()
        {
            foreach (GenericFontFamilies generic in AllGenerics)
                RequireStandIn(generic);

            int Measure(FontFamily family)
            {
                using (family)
                    return CountInk((g, font, brush) => g.DrawString("Hello world, quick fox", font, brush, 20, 20), family);
            }

            int sans = Measure(FontFamily.GenericSansSerif);
            int serif = Measure(FontFamily.GenericSerif);
            int mono = Measure(FontFamily.GenericMonospace);
            Assert.AreNotEqual(sans, serif, "serif renders like sans-serif");
            Assert.AreNotEqual(sans, mono, "monospace renders like sans-serif");
            Assert.AreNotEqual(serif, mono, "monospace renders like serif");
        }

        [TestMethod]
        public void GenericFamily_MetricsMatchTheStandInUsedForOutlines()
        {
            RequireStandIn(GenericFontFamilies.SansSerif);
            FontFamily target = GenericFontResolver.Resolve(GenericFontFamilies.SansSerif)!;
            using var generic = FontFamily.GenericSansSerif;
            Assert.AreEqual(target.GetEmHeight(FontStyle.Regular), generic.GetEmHeight(FontStyle.Regular));
            Assert.AreEqual(target.GetCellAscent(FontStyle.Regular), generic.GetCellAscent(FontStyle.Regular));
        }
    }
}
