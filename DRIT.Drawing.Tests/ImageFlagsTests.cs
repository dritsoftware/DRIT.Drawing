using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class ImageFlagsTests
    {
        [TestMethod]
        public void ImageFlags_ColorSpaceRgb_IsSet()
        {
            var flags = ImageFlags.ColorSpaceRgb | ImageFlags.HasRealDpi;
            Assert.IsTrue((flags & ImageFlags.ColorSpaceRgb) != 0);
            Assert.IsTrue((flags & ImageFlags.HasRealDpi)    != 0);
            Assert.IsFalse((flags & ImageFlags.HasAlpha)     != 0);
        }
    }
}