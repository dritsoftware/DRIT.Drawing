using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class FrameDimensionTests
    {
        [TestMethod]
        public void FrameDimension_StaticProps_NotNull()
        {
            Assert.IsNotNull(FrameDimension.Page);
            Assert.IsNotNull(FrameDimension.Time);
            Assert.IsNotNull(FrameDimension.Resolution);
        }

        [TestMethod]
        public void FrameDimension_PageGuid_Correct()
        {
            var expected = new Guid("7462dc86-6180-4c7e-8e3f-ee7333a7a483");
            Assert.AreEqual(expected, FrameDimension.Page.Guid);
        }
    }
}