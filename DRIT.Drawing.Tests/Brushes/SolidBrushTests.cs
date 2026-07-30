using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Brushes
{
    [TestClass]
    public class SolidBrushTests
    {
        [TestMethod]
        public void SolidBrush_Color_Get()
        {
            var b = new SolidBrush(Color.Red);
            Assert.AreEqual(Color.Red, b.Color);
        }

        [TestMethod]
        public void SolidBrush_Color_Set()
        {
            var b = new SolidBrush(Color.Red);
            b.Color = Color.Blue;
            Assert.AreEqual(Color.Blue, b.Color);
        }

        [TestMethod]
        public void SolidBrush_Clone_IsSeparateInstance()
        {
            var b = new SolidBrush(Color.Green);
            var c = (SolidBrush)b.Clone();
            Assert.AreNotSame(b, c);
            Assert.AreEqual(b.Color, c.Color);
            c.Color = Color.Red;
            Assert.AreEqual(Color.Green, b.Color); // original unchanged
        }

        [TestMethod]
        public void SolidBrush_Dispose_ThrowsOnAccess()
        {
            var b = new SolidBrush(Color.Red);
            b.Dispose();
            Assert.ThrowsException<System.ObjectDisposedException>(() => _ = b.Color);
        }
    }
}