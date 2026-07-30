using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Rendering;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class PixelCompositorTests
    {
        [TestMethod]
        public void PixelCompositor_SourceOver_BlendsAgainstDestination()
        {
            using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            bmp.SetPixel(0, 0, Color.FromArgb(255, 0, 0, 255));

            PixelCompositor.BlendPixel(
                bmp,
                0,
                0,
                Color.FromArgb(128, 255, 0, 0),
                CompositingMode.SourceOver);

            Color c = bmp.GetPixel(0, 0);
            Assert.IsTrue(c.R > 100);
            Assert.IsTrue(c.B > 60);
            Assert.AreEqual(255, c.A);
        }
    }
}