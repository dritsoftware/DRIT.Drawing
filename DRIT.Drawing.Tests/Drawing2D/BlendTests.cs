using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class BlendTests
    {
        [TestMethod]
        public void Blend_DefaultConstructor()
        {
            var bl = new Blend();
            Assert.AreEqual(0, bl.Factors.Length);
            Assert.AreEqual(0, bl.Positions.Length);
        }

        [TestMethod]
        public void Blend_CountConstructor()
        {
            var bl = new Blend(3);
            Assert.AreEqual(3, bl.Factors.Length);
            Assert.AreEqual(3, bl.Positions.Length);
        }
    }
}