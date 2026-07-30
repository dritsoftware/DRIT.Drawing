using DRIT.Drawing.Pens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class AdjustableArrowCapTests
    {
        // -----------------------------------------------------------------------
        // AdjustableArrowCap
        // -----------------------------------------------------------------------

        [TestMethod]
        public void AdjustableArrowCap_Properties()
        {
            var cap = new AdjustableArrowCap(5f, 10f, true);
            Assert.AreEqual(5f, cap.Width);
            Assert.AreEqual(10f, cap.Height);
            Assert.IsTrue(cap.Filled);
        }

        [TestMethod]
        public void AdjustableArrowCap_Clone()
        {
            var cap = new AdjustableArrowCap(5f, 10f, false);
            var clone = (AdjustableArrowCap)cap.Clone();
            Assert.AreEqual(5f, clone.Width);
            Assert.AreEqual(10f, clone.Height);
            Assert.IsFalse(clone.Filled);
        }
    }
}
