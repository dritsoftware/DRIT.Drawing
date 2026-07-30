using DRIT.Drawing.Colors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Pens
{
    [TestClass]
    public class PensTests
    {
        [TestMethod]
        public void Pens_Red_IsRed()
        {
            Assert.AreEqual(Color.Red.ToArgb(), Drawing.Pens.Pens.Red.Color.ToArgb());
        }

        [TestMethod]
        public void Pens_Width_Is1()
        {
            Assert.AreEqual(1f, Drawing.Pens.Pens.Black.Width);
        }

        [TestMethod]
        public void Pens_SameProperty_ReturnsSameInstance()
        {
            Assert.AreSame(Drawing.Pens.Pens.Blue, Drawing.Pens.Pens.Blue);
        }
    }
}