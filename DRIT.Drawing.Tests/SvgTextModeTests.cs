using System.Text;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Text;
using DRIT.Drawing.Vector.Svg;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class SvgTextModeTests
    {
        private static string WriteTextModeSvg(FontFamily family, float size, GraphicsUnit unit)
        {
            using var svg = new SvgImage(300, 100);
            using (var g = svg.CreateGraphics())
            using (var font = new DRIT.Drawing.Text.Font(family, size, FontStyle.Regular, unit))
            using (var brush = new SolidBrush(Color.Black))
                g.DrawString("Hello", font, brush, 10, 10);

            using var stream = new MemoryStream();
            svg.Save(stream, new SvgEncoderParameters { TextMode = SvgTextMode.Text });
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        [DataTestMethod]
        [DataRow("Sans Serif", "sans-serif")]
        [DataRow("Serif", "serif")]
        [DataRow("Monospace", "monospace")]
        [DataRow("Segoe UI", "Segoe UI")]
        public void CssFontFamily_MapsGenericFamilyNamesToCssKeywords(string name, string expected)
            => Assert.AreEqual(expected, SvgTextWriter.CssFontFamily(name));

        [TestMethod]
        public void TextMode_WritesGenericFamilyAsCssKeyword()
        {
            string xml = WriteTextModeSvg(FontFamily.GenericSansSerif, 12f, GraphicsUnit.Point);
            StringAssert.Contains(xml, "font-family=\"sans-serif\"");
            Assert.IsFalse(xml.Contains("font-family=\"Sans Serif\""));
        }

        [DataTestMethod]
        [DataRow(12f, GraphicsUnit.Point, "16")]     // 12 pt at 96 dpi
        [DataRow(20f, GraphicsUnit.Pixel, "20")]
        [DataRow(1f, GraphicsUnit.Inch, "96")]
        public void TextMode_WritesFontSizeInPixels(float size, GraphicsUnit unit, string expected)
        {
            string xml = WriteTextModeSvg(FontFamily.GenericSansSerif, size, unit);
            StringAssert.Contains(xml, $"font-size=\"{expected}\"");
        }

        [DataTestMethod]
        [DataRow("serif", "Serif")]
        [DataRow("monospace", "Monospace")]
        [DataRow("sans-serif", "Sans Serif")]
        [DataRow("'Courier New', monospace", "Courier New")]
        [DataRow("\"Segoe UI\", Arial", "Segoe UI")]
        public void ResolveFontFamily_UsesTheFirstEntryAndMapsCssKeywords(string css, string expectedName)
        {
            using FontFamily family = SvgTextReader.ResolveFontFamily(css);
            Assert.AreEqual(expectedName, family.Name);
        }
    }
}
