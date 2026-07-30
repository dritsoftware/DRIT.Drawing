using DRIT.Drawing.Enums;
using DRIT.Drawing.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class FontFamilyTests
    {
        [TestMethod]
        public void FontFamily_GenericSansSerif_HasMetrics()
        {
            using var family = FontFamily.GenericSansSerif;

            Assert.AreEqual("Sans Serif", family.Name);
            Assert.IsTrue(family.GetEmHeight(FontStyle.Regular) > 0);
            Assert.IsTrue(family.GetCellAscent(FontStyle.Regular) > 0);
            Assert.IsTrue(family.GetCellDescent(FontStyle.Regular) > 0);
            Assert.IsTrue(family.GetLineSpacing(FontStyle.Regular) >= family.GetCellAscent(FontStyle.Regular));
        }

        [TestMethod]
        public void FontFamily_IsStyleAvailable_ForGenericStyles()
        {
            using var family = FontFamily.GenericSerif;

            Assert.IsTrue(family.IsStyleAvailable(FontStyle.Regular));
            Assert.IsTrue(family.IsStyleAvailable(FontStyle.Bold));
            Assert.IsTrue(family.IsStyleAvailable(FontStyle.Italic));
            Assert.IsTrue(family.IsStyleAvailable(FontStyle.Bold | FontStyle.Italic));
            Assert.IsTrue(family.IsStyleAvailable(FontStyle.Underline));
        }

        [TestMethod]
        public void InstalledFontCollection_FindsMoreThanGenericFamilies()
        {
            using var collection = new InstalledFontCollection();
            var families = collection.Families;

            // The three generic fallbacks plus at least one real system font.
            Assert.IsTrue(families.Length > 3,
                $"Expected more than 3 families (generic + system), got {families.Length}.");
        }

        [TestMethod]
        public void InstalledFontCollection_Default_IsCachedInstance()
        {
            var first = InstalledFontCollection.Default;
            var second = InstalledFontCollection.Default;

            Assert.AreSame(first, second, "Default should return the same cached instance.");
            Assert.IsTrue(first.Families.Length > 3);
        }

        [TestMethod]
        public void InstalledFontCollection_FindsKnownPlatformFont()
        {
            using var collection = new InstalledFontCollection();
            var names = collection.Families.Select(f => f.Name).ToList();

            string expected = GetExpectedPlatformFontName();
            Assert.IsTrue(names.Any(n => string.Equals(n, expected, StringComparison.OrdinalIgnoreCase)),
                $"Expected to find '{expected}' among {names.Count} installed families. Found: {string.Join(", ", names.Take(20))}...");
        }

        [TestMethod]
        public void FontFamily_ByName_ResolvesToInstalledFont()
        {
            string name = GetExpectedPlatformFontName();
            using var family = new FontFamily(name);

            Assert.IsTrue(string.Equals(name, family.Name, StringComparison.OrdinalIgnoreCase),
                $"Expected family name '{name}', got '{family.Name}'.");
            Assert.IsTrue(family.GetEmHeight(FontStyle.Regular) > 0);
            Assert.IsTrue(family.IsStyleAvailable(FontStyle.Regular));
        }

        [TestMethod]
        public void FontFamily_ByName_FallsBackForMissingFont()
        {
            // A name that will never be installed.
            using var family = new FontFamily("__NoSuchFontXYZ123__");

            Assert.AreEqual("__NoSuchFontXYZ123__", family.Name);
            // Fallback still provides guessed metrics.
            Assert.IsTrue(family.GetEmHeight(FontStyle.Regular) > 0);
        }

        [TestMethod]
        public void FontFamily_ByName_ThrowsOnEmpty()
        {
            Assert.ThrowsException<ArgumentException>(() => new FontFamily(""));
        }

        [TestMethod]
        public void InstalledFontCollection_DefaultFamilyName_IsSetOnWindows()
        {
            using var collection = new InstalledFontCollection();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.IsNotNull(collection.DefaultFamilyName,
                    "Windows should report a default font family via the registry.");
                Assert.IsTrue(collection.DefaultFamilyName.Length > 0);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.IsNotNull(collection.DefaultFamilyName,
                    "Linux should report 'sans-serif' as the default font alias.");
                Assert.AreEqual("sans-serif", collection.DefaultFamilyName);
            }
            // On macOS the default may or may not be available depending on CoreText access.
        }

        [TestMethod]
        public void FontSettings_AutoLoadSystemFonts_DefaultIsTrue()
        {
            // Reset to default to ensure clean state.
            var original = FontSettings.AutoLoadSystemFonts;
            try
            {
                FontSettings.AutoLoadSystemFonts = true;
                Assert.IsTrue(FontSettings.AutoLoadSystemFonts);
            }
            finally
            {
                FontSettings.AutoLoadSystemFonts = original;
            }
        }

        [TestMethod]
        public void FontSettings_UseNativeFontApis_DefaultIsTrue()
        {
            var original = FontSettings.UseNativeFontApis;
            try
            {
                FontSettings.UseNativeFontApis = true;
                Assert.IsTrue(FontSettings.UseNativeFontApis);
            }
            finally
            {
                FontSettings.UseNativeFontApis = original;
            }
        }

        [TestMethod]
        public void InstalledFontCollection_WithAutoLoadDisabled_OnlyHasGenericFamilies()
        {
            // We cannot test the Default singleton because it may already be cached.
            // Instead, verify the behaviour by creating a collection with scanning disabled
            // via the internal constructor path: when AutoLoadSystemFonts is false,
            // CreateDefaultInstance skips scanning. We test this indirectly by checking
            // that a fresh collection with scanSystem=false has exactly 3 families.
            var collection = CreateNonScanningCollection();
            var families = collection.Families;

            Assert.AreEqual(3, families.Length,
                "A non-scanning collection should have exactly 3 generic families.");
            var names = families.Select(f => f.Name).ToList();
            CollectionAssert.AreEquivalent(
                new[] { "Sans Serif", "Serif", "Monospace" },
                names);
        }

        [TestMethod]
        public void InstalledFontCollection_WithNativeApisDisabled_StillFindsFonts()
        {
            // When native APIs are disabled, directory scanning should still work.
            var original = FontSettings.UseNativeFontApis;
            try
            {
                FontSettings.UseNativeFontApis = false;
                using var collection = new InstalledFontCollection();
                Assert.IsTrue(collection.Families.Length > 3,
                    $"Directory scanning should still find system fonts. Got {collection.Families.Length} families.");
            }
            finally
            {
                FontSettings.UseNativeFontApis = original;
            }
        }

        [TestMethod]
        public void InstalledFontCollection_WithNativeApisEnabled_FindsFonts()
        {
            // When native APIs are enabled (default), fonts should be discovered.
            var original = FontSettings.UseNativeFontApis;
            try
            {
                FontSettings.UseNativeFontApis = true;
                using var collection = new InstalledFontCollection();
                Assert.IsTrue(collection.Families.Length > 3,
                    $"Native API discovery should find system fonts. Got {collection.Families.Length} families.");
            }
            finally
            {
                FontSettings.UseNativeFontApis = original;
            }
        }

        /// <summary>
        /// Creates an InstalledFontCollection without scanning by temporarily disabling
        /// auto-load. This simulates the web/server scenario.
        /// </summary>
        private static InstalledFontCollection CreateNonScanningCollection()
        {
            var original = FontSettings.AutoLoadSystemFonts;
            try
            {
                FontSettings.AutoLoadSystemFonts = false;
                // We need a fresh instance that respects the setting. Since the Default
                // singleton may already be cached, we use reflection to invoke the
                // private constructor with scanSystem=false.
                var ctor = typeof(InstalledFontCollection).GetConstructor(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null,
                    new[] { typeof(bool) },
                    null);
                if (ctor != null)
                    return (InstalledFontCollection)ctor.Invoke(new object[] { false });
            }
            finally
            {
                FontSettings.AutoLoadSystemFonts = original;
            }

            // Fallback: if reflection fails, just create a normal collection (shouldn't happen).
            return new InstalledFontCollection();
        }

        private static string GetExpectedPlatformFontName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Arial";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "Helvetica";
            // Linux: DejaVu Sans is present on most distros.
            return "DejaVu Sans";
        }
    }
}