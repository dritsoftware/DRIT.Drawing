using DRIT.Drawing.Text;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class Batch10Tests
    {
        

        

        [TestMethod]
        public void StringFormat_SetTabStops_AndRanges_RoundTrip()
        {
            using var format = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.NoClip)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Far,
                Trimming = StringTrimming.EllipsisCharacter,
                HotkeyPrefix = HotkeyPrefix.Show
            };

            format.SetTabStops(4f, new[] { 8f, 12f });
            format.SetMeasurableCharacterRanges(new[] { new CharacterRange(1, 3), new CharacterRange(5, 2) });

            float firstTabOffset;
            var tabStops = format.GetTabStops(out firstTabOffset);
            var ranges = format.GetMeasurableCharacterRanges();

            Assert.AreEqual(4f, firstTabOffset, 0.001f);
            CollectionAssert.AreEqual(new[] { 8f, 12f }, tabStops);
            Assert.AreEqual(2, ranges.Length);
            Assert.AreEqual(1, ranges[0].First);
            Assert.AreEqual(3, ranges[0].Length);
            Assert.AreEqual(StringAlignment.Center, format.Alignment);
            Assert.AreEqual(StringAlignment.Far, format.LineAlignment);
            Assert.AreEqual(StringTrimming.EllipsisCharacter, format.Trimming);
        }

        [TestMethod]
        public void StringFormat_GenericTypographic_HasExpectedFlags()
        {
            using var format = StringFormat.GenericTypographic;

            Assert.IsTrue((format.FormatFlags & StringFormatFlags.FitBlackBox) != 0);
            Assert.IsTrue((format.FormatFlags & StringFormatFlags.MeasureTrailingSpaces) != 0);
            Assert.IsTrue((format.FormatFlags & StringFormatFlags.NoClip) != 0);
        }

        [TestMethod]
        public void InstalledFontCollection_ContainsGenericFamilies()
        {
            using var fonts = new InstalledFontCollection();

            var names = fonts.Families.Select(x => x.Name).ToArray();

            CollectionAssert.Contains(names, "Sans Serif");
            CollectionAssert.Contains(names, "Serif");
            CollectionAssert.Contains(names, "Monospace");
        }

        [TestMethod]
        public void PrivateFontCollection_AddFontFile_LoadsFamily()
        {
            string path = GetGlyphiconsPath();
            if (!File.Exists(path)) Assert.Inconclusive("Glyphicons font not found.");
            using var fonts = new PrivateFontCollection();

            fonts.AddFontFile(path);

            Assert.IsTrue(fonts.Families.Length > 0);
            Assert.IsTrue(fonts.Families.Any(x => !string.IsNullOrWhiteSpace(x.Name)));
        }

        [TestMethod]
        public void PrivateFontCollection_AddMemoryFont_LoadsFamily()
        {
            string path = GetGlyphiconsPath();
            if (!File.Exists(path)) Assert.Inconclusive("Glyphicons font not found.");
            byte[] data = File.ReadAllBytes(path);
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);

            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                using var fonts = new PrivateFontCollection();

                fonts.AddMemoryFont(buffer, data.Length);

                Assert.IsTrue(fonts.Families.Length > 0);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string GetGlyphiconsPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "DRIT.Drawing.Tests",
                "TestAssets",
                "Fonts",
                "glyphicons-halflings-regular.ttf"));
        }
    }
}