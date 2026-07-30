using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class ColorBlendTests
    {
        [TestMethod]
        public void ColorBlend_CountConstructor()
        {
            var cb = new ColorBlend(4);
            Assert.AreEqual(4, cb.Colors.Length);
            Assert.AreEqual(4, cb.Positions.Length);
        }
    }
}