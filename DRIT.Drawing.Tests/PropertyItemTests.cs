using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class PropertyItemTests
    {
        [TestMethod]
        public void PropertyItem_Properties()
        {
            var pi = new PropertyItem { Id = 271, Len = 10, Type = 2, Value = new byte[] { 1, 2 } };
            Assert.AreEqual(271, pi.Id);
            Assert.AreEqual(10,  pi.Len);
            Assert.AreEqual(2,   pi.Type);
            Assert.AreEqual(2,   pi.Value.Length);
        }
    }
}