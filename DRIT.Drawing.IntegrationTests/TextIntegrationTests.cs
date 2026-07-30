using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Text;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Brushes;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for text rendering that produce visual output.
    /// 
    /// Human Tester Instructions:
    /// - Images are saved to the Out folder with descriptive names
    /// - Verify that text is rendered clearly and legibly
    /// - Check that font styles (bold, italic) are distinguishable
    /// - Verify text alignment is correct
    /// - Check that word wrapping works as expected
    /// </summary>
    [TestClass]
    public class TextIntegrationTests : IntegrationTestBase
    {
        /// <summary>Loads a Windows font from the Fonts directory into a PrivateFontCollection using AddMemoryFont.</summary>
        private static FontFamily LoadWindowsFont(string fontFileName)
        {
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fontFileName);
            if (!File.Exists(fontPath))
                throw new FileNotFoundException($"Font file not found: {fontPath}", fontPath);

            byte[] fontData = File.ReadAllBytes(fontPath);
            IntPtr buffer = Marshal.AllocHGlobal(fontData.Length);
            try
            {
                Marshal.Copy(fontData, 0, buffer, fontData.Length);
                using var pfc = new PrivateFontCollection();
                pfc.AddMemoryFont(buffer, fontData.Length);
                return pfc.Families.Length > 0 ? pfc.Families[0] : FontFamily.GenericSansSerif;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>Tests font styles (Regular, Bold, Italic, BoldItalic) with sample text.</summary>
        [TestMethod]
        public void DrawString_FontStyles_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with text samples:
            // - Top row: "Sample Text" in Regular, Bold, Italic, BoldItalic
            // - Each style should be clearly distinguishable
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var blackBrush = new SolidBrush(Color.Black);
            using var family = LoadWindowsFont("arial.ttf");

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 400, 300);

            float y = 30;
            foreach (FontStyle style in Enum.GetValues(typeof(FontStyle)))
            {
                using var font = new Text.Font(family, 20f, style, GraphicsUnit.Point);
                g.DrawString($"Sample Text ({style})", font, blackBrush, 20, y);
                y += 60;
            }

            SaveImage(bmp, "DrawString_FontStyles_VisibleOutput");
        }

        [TestMethod]
        public void DrawString_Alignment_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with "Aligned Text" showing:
            // - Left-aligned text at left edge
            // - Center-aligned text in middle
            // - Right-aligned text at right edge
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var blackBrush = new SolidBrush(Color.Black);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 16f, FontStyle.Regular, GraphicsUnit.Point);
            using var format = new StringFormat();

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 400, 300);

            format.Alignment = StringAlignment.Near;
            g.DrawString("Left Aligned", font, blackBrush, new RectangleF(0, 50, 400, 40), format);

            format.Alignment = StringAlignment.Center;
            g.DrawString("Center Aligned", font, blackBrush, new RectangleF(0, 130, 400, 40), format);

            format.Alignment = StringAlignment.Far;
            g.DrawString("Right Aligned", font, blackBrush, new RectangleF(0, 210, 400, 40), format);

            SaveImage(bmp, "DrawString_Alignment_VisibleOutput");
        }

        /// <summary>Tests text wrapping behavior with long text strings.</summary>
        [TestMethod]
        public void DrawString_Wrapping_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - Top: Long text without wrapping (single line, truncated)
            // - Bottom: Same text with word wrapping (multiple lines)
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var blackBrush = new SolidBrush(Color.Black);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 16f, FontStyle.Regular, GraphicsUnit.Point);
            using var formatNoWrap = new StringFormat();
            using var formatWrap = new StringFormat();

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 400, 300);

            formatWrap.FormatFlags = StringFormatFlags.FitBlackBox;
            formatWrap.Trimming = StringTrimming.None;

            g.DrawString("This is a very long text string that demonstrates no wrapping behavior", font, blackBrush, new RectangleF(10, 20, 380, 40), formatNoWrap);
            g.DrawString("This is a very long text string that demonstrates word wrapping behavior", font, blackBrush, new RectangleF(10, 100, 380, 200), formatWrap);

            SaveImage(bmp, "DrawString_Wrapping_VisibleOutput");
        }

        /// <summary>Tests MeasureString by drawing text and outlining its measured bounds.</summary>
        [TestMethod]
        public void MeasureString_VisibleOutput()
        {
            // Human Tester: Should see a 400x200 image with:
            // - "Hello World" text drawn at (20,20)
            // - Red rectangle outlining the measured bounds
            // - The rectangle should tightly wrap the text
            using var bmp = new Bitmap(400, 200);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var blackBrush = new SolidBrush(Color.Black);
            using var redPen = new Pens.Pen(Color.Red, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 20f, FontStyle.Regular, GraphicsUnit.Point);

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 400, 200);

            string text = "Hello World";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, blackBrush, 20, 20);
            g.DrawRectangle(redPen, 20, 20, (int)size.Width, (int)size.Height);

            SaveImage(bmp, "MeasureString_VisibleOutput");
        }

        /// <summary>
        /// Demonstrates that system fonts are auto-discovered: this test uses
        /// <c>new Font("Arial", ...)</c> directly without any manual font loading,
        /// relying on <see cref="InstalledFontCollection"/> scanning the system fonts.
        /// </summary>
        [TestMethod]
        public void DrawString_SystemFontAutoDiscovered_VisibleOutput()
        {
            // Human Tester: Should see a 400x200 image with:
            // - "System Font!" rendered in Arial (or platform equivalent) at 24pt
            // - Text should be clearly legible with real glyphs (not fallback boxes)
            using var bmp = new Bitmap(400, 200);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var blackBrush = new SolidBrush(Color.Black);

            g.FillRectangle(whiteBrush, 0, 0, 400, 200);

            // No PrivateFontCollection, no AddFontFile — the family is resolved from
            // the installed system fonts automatically.
            string familyName = GetPlatformDefaultFontName();
            using var font = new Text.Font(familyName, 24f, FontStyle.Regular, GraphicsUnit.Point);
            g.DrawString("System Font!", font, blackBrush, 20, 80);

            SaveImage(bmp, "DrawString_SystemFontAutoDiscovered_VisibleOutput");
        }

        private static string GetPlatformDefaultFontName()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
                return "Arial";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX))
                return "Helvetica";
            return "DejaVu Sans";
        }
    }
}