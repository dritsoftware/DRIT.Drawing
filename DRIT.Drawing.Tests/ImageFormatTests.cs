using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class ImageFormatTests
    {
        [TestMethod]
        public void ImageFormat_BmpGuid_Correct()
        {
            var expected = new Guid("b96b3cab-0728-11d3-9d7b-0000f81ef32e");
            Assert.AreEqual(expected, ImageFormat.Bmp.Guid);
        }

        [TestMethod]
        public void ImageFormat_AllStaticProps_NotNull()
        {
            Assert.IsNotNull(ImageFormat.Bmp);
            Assert.IsNotNull(ImageFormat.Png);
            Assert.IsNotNull(ImageFormat.Jpeg);
            Assert.IsNotNull(ImageFormat.Gif);
            Assert.IsNotNull(ImageFormat.Tiff);
            Assert.IsNotNull(ImageFormat.Icon);
            Assert.IsNotNull(ImageFormat.Wmf);
            Assert.IsNotNull(ImageFormat.Emf);
        }

        [TestMethod]
        public void ImageFormat_Equality_SameGuid()
        {
            Assert.IsTrue(ImageFormat.Bmp.Equals(ImageFormat.Bmp));
            Assert.IsFalse(ImageFormat.Bmp.Equals(ImageFormat.Png));
        }
    }
}