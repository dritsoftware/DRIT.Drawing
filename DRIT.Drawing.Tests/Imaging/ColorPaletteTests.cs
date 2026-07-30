using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Colors;

namespace DRIT.Drawing.Tests.Imaging
{
    [TestClass]
    public class ColorPaletteTests
    {
        [TestMethod]
        public void ColorPalette_Default_EmptyEntries()
        {
            var cp = new ColorPalette();
            Assert.AreEqual(0, cp.Entries.Length);
            Assert.AreEqual(0, cp.Flags);
        }

        [TestMethod]
        public void ColorPalette_WithEntries()
        {
            var entries = new[] { Color.Red, Color.Green, Color.Blue };
            var cp = new ColorPalette(1, entries);
            Assert.AreEqual(3, cp.Entries.Length);
            Assert.AreEqual(1, cp.Flags);
            Assert.AreEqual(Color.Red, cp.Entries[0]);
        }
    }
}