using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class HatchStyleTests
    {
        [TestMethod]
        public void HatchStyle_Values()
        {
            Assert.AreEqual(0,  (int)HatchStyle.Horizontal);
            Assert.AreEqual(4,  (int)HatchStyle.Cross);
            Assert.AreEqual(4,  (int)HatchStyle.LargeGrid);
            Assert.AreEqual(52, (int)HatchStyle.SolidDiamond);
        }
    }
}