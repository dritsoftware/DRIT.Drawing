using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Colors;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class HatchBrushTests
    {
        [TestMethod]
        public void HatchBrush_Properties()
        {
            var b = new HatchBrush(HatchStyle.Cross, Color.White, Color.Black);
            Assert.AreEqual(HatchStyle.Cross, b.HatchStyle);
            Assert.AreEqual(Color.White, b.ForegroundColor);
            Assert.AreEqual(Color.Black, b.BackgroundColor);
        }

        [TestMethod]
        public void HatchBrush_DefaultBackground_IsBlack()
        {
            var b = new HatchBrush(HatchStyle.Horizontal, Color.Red);
            Assert.AreEqual(Color.Black, b.BackgroundColor);
        }

        [TestMethod]
        public void HatchBrush_Clone()
        {
            var b = new HatchBrush(HatchStyle.DiagonalCross, Color.Blue, Color.Yellow);
            var c = (HatchBrush)b.Clone();
            Assert.AreNotSame(b, c);
            Assert.AreEqual(HatchStyle.DiagonalCross, c.HatchStyle);
            Assert.AreEqual(Color.Blue, c.ForegroundColor);
        }
    }
}