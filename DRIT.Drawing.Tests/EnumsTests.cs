using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Colors;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class EnumsTests
    {
        [TestMethod]
        public void FontStyle_Flags_Combine()
        {
            FontStyle combined = FontStyle.Bold | FontStyle.Italic;
            Assert.IsTrue((combined & FontStyle.Bold)   != 0);
            Assert.IsTrue((combined & FontStyle.Italic) != 0);
            Assert.IsFalse((combined & FontStyle.Underline) != 0);
        }

        [TestMethod]
        public void PixelFormat_Format32bppArgb_HasCorrectValue()
        {
            Assert.AreEqual(0x0026200A, (int)PixelFormat.Format32bppArgb);
        }

        [TestMethod]
        public void KnownColor_Blue_HasCorrectIntValue()
        {
            Assert.AreEqual(37, (int)KnownColor.Blue);
        }

        // Drawing2D enums spot-checks
        [TestMethod]
        public void LineCap_Values()
        {
            Assert.AreEqual(0,   (int)LineCap.Flat);
            Assert.AreEqual(20,  (int)LineCap.ArrowAnchor);
            Assert.AreEqual(255, (int)LineCap.Custom);
            Assert.AreEqual(240, (int)LineCap.AnchorMask);
        }

        [TestMethod]
        public void WrapMode_Values()
        {
            Assert.AreEqual(0, (int)WrapMode.Tile);
            Assert.AreEqual(4, (int)WrapMode.Clamp);
        }

        [TestMethod]
        public void PenType_Values()
        {
            Assert.AreEqual(0, (int)PenType.SolidColor);
            Assert.AreEqual(4, (int)PenType.LinearGradient);
        }

        [TestMethod]
        public void SmoothingMode_HasInvalid()
        {
            Assert.AreEqual(-1, (int)SmoothingMode.Invalid);
            Assert.AreEqual(4,  (int)SmoothingMode.AntiAlias);
        }

        [TestMethod]
        public void InterpolationMode_HighQualityBicubic_Is7()
        {
            Assert.AreEqual(7, (int)InterpolationMode.HighQualityBicubic);
        }
    }
}