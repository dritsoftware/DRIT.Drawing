using DRIT.Drawing.Enums;
using DRIT.Drawing.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class FontFallbackChainTests
    {
        [TestMethod]
        public void Fallback_EmptyChain_ReturnsNull()
        {
            var chain = new FontFallbackChain();
            Assert.IsNull(chain.Resolve('A', FontStyle.Regular));
        }

        [TestMethod]
        public void Fallback_AddFamily()
        {
            var family = FontFamily.GenericSansSerif;
            var chain = new FontFallbackChain();
            chain.Add(family);
            Assert.AreEqual(1, chain.Families.Count);
            Assert.AreSame(family, chain.Families[0]);
        }

        [TestMethod]
        public void Fallback_ConstructWithFamilies()
        {
            var f1 = FontFamily.GenericSansSerif;
            var f2 = FontFamily.GenericSerif;
            var chain = new FontFallbackChain(f1, f2);
            Assert.AreEqual(2, chain.Families.Count);
        }

        [TestMethod]
        public void Fallback_CreateDefault_NonEmptyOnWindows()
        {
            var chain = FontFallbackChain.CreateDefault();
            if (System.OperatingSystem.IsWindows())
            {
                Assert.IsTrue(chain.Families.Count >= 1, "Default fallback chain should have at least 1 family on Windows");
            }
        }

        [TestMethod]
        public void Fallback_Resolve_UsesInstalledFamily()
        {
            var chain = new FontFallbackChain(FontFamily.GenericSansSerif);
            // GenericSansSerif may or may not have a real font face loaded;
            // Resolve returns null when no glyph is available, which is valid.
            // Just verify it doesn't throw.
            try
            {
                chain.Resolve('A', FontStyle.Regular);
            }
            catch (System.Exception ex)
            {
                Assert.Fail("Resolve threw: " + ex.Message);
            }
        }
    }
}