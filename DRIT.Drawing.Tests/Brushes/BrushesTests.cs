using DRIT.Drawing.Colors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Brushes
{
    [TestClass]
    public class BrushesTests
    {
        [TestMethod]
        public void Brushes_Red_IsRed()
        {
            Assert.AreEqual(Color.Red.ToArgb(), Drawing.Brushes.Brushes.Red.Color.ToArgb());
        }

        [TestMethod]
        public void Brushes_Blue_IsBlue()
        {
            Assert.AreEqual(Color.Blue.ToArgb(), Drawing.Brushes.Brushes.Blue.Color.ToArgb());
        }

        [TestMethod]
        public void Brushes_SameProperty_ReturnsSameInstance()
        {
            Assert.AreSame(Drawing.Brushes.Brushes.Green, Drawing.Brushes.Brushes.Green);
        }

        [TestMethod]
        public void Brushes_Transparent_IsTransparent()
        {
            Assert.AreEqual(0, Drawing.Brushes.Brushes.Transparent.Color.A);
        }
    }
}