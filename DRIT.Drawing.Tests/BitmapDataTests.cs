using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using DRIT.Drawing;
using DRIT.Drawing.Enums;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class BitmapDataTests
    {
        [TestMethod]
        public void BitmapData_Properties()
        {
            var bd = new BitmapData
            {
                Width = 100, Height = 80, Stride = 400,
                PixelFormat = PixelFormat.Format32bppArgb,
            };
            Assert.AreEqual(100, bd.Width);
            Assert.AreEqual(80,  bd.Height);
            Assert.AreEqual(400, bd.Stride);
            Assert.AreEqual(PixelFormat.Format32bppArgb, bd.PixelFormat);
        }
    }
}