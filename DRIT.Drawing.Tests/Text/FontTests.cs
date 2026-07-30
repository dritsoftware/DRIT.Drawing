using DRIT.Drawing.Enums;
using DRIT.Drawing.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class FontTests
    {
        [TestMethod]
        public void Font_Properties_ReflectRequestedStyle()
        {
            using var family = FontFamily.GenericSansSerif;
            using var font = new Drawing.Text.Font(family, 12f, FontStyle.Bold | FontStyle.Italic | FontStyle.Underline, GraphicsUnit.Point);

            Assert.AreEqual("Sans Serif", font.Name);
            Assert.AreEqual(family, font.FontFamily);
            Assert.IsTrue(font.Bold);
            Assert.IsTrue(font.Italic);
            Assert.IsTrue(font.Underline);
            Assert.IsFalse(font.Strikeout);
            Assert.AreEqual(12f, font.Size, 0.001f);
            Assert.AreEqual(GraphicsUnit.Point, font.Unit);
            Assert.AreEqual(12f, font.SizeInPoints, 0.001f);
            Assert.IsTrue(font.Height > 0);
        }

        [TestMethod]
        public void Font_SizeInPoints_ConvertsPixels()
        {
            using var font = new Drawing.Text.Font(FontFamily.GenericMonospace, 16f, FontStyle.Regular, GraphicsUnit.Pixel);

            Assert.AreEqual(12f, font.SizeInPoints, 0.001f);
        }
    }
}
