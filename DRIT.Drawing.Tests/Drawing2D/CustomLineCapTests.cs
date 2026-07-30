using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Pens;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class CustomLineCapTests
    {
        [TestMethod]
        public void CustomLineCap_Properties()
        {
            var cap = new CustomLineCap(null, null, LineCap.Round, 1f);
            Assert.AreEqual(LineCap.Round, cap.BaseCap);
            Assert.AreEqual(1f, cap.BaseInset);
        }

        [TestMethod]
        public void CustomLineCap_Clone()
        {
            var cap = new CustomLineCap(null, null, LineCap.Square, 2f);
            cap.WidthScale = 3f;
            var clone = cap.Clone();
            Assert.AreNotSame(cap, clone);
            Assert.AreEqual(LineCap.Square, clone.BaseCap);
            Assert.AreEqual(3f, clone.WidthScale);
        }
    }
}
